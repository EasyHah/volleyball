using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class DeterministicRallyPlanComposerV3Tests
    {
        [Test]
        public void TeamRallyPlan_RequiresTheSixEligiblePlayersOfItsSide()
        {
            var snapshot = CreateSnapshot();
            var assignments = Assignments("home", 6);
            assignments[5] = Assignment("away-1", 6);

            Assert.That(
                () => new TeamRallyPlanV3(TeamSide.Home, assignments, Array.Empty<string>(), snapshot.Eligibility),
                Throws.ArgumentException);
        }

        [Test]
        public void TeamRallyPlan_DefensivelyCopiesCandidateEvidence()
        {
            var snapshot = CreateSnapshot();
            var evidence = new List<string> { "candidate-a" };
            var plan = new TeamRallyPlanV3(TeamSide.Home, Assignments("home", 6), evidence, snapshot.Eligibility);
            evidence[0] = "mutated";

            Assert.That(plan.CandidateEvidence.Single(), Is.EqualTo("candidate-a"));
        }

        [Test]
        public void TeamRallyPlan_DefensivelyCopiesAssignmentsAndExposesReadOnlyAssignments()
        {
            var snapshot = CreateSnapshot();
            var assignments = Assignments("home", 6);
            var plan = new TeamRallyPlanV3(TeamSide.Home, assignments, Array.Empty<string>(), snapshot.Eligibility);
            assignments[0] = Assignment("home-6", 1);

            Assert.That(plan.Assignments[0].PlayerId, Is.EqualTo(new PlayerId("home-1")));

            var exposedAssignments = plan.Assignments as IList<PlayerResponsibilityAssignmentV3>;
            Assert.That(exposedAssignments, Is.Not.Null);
            Assert.That(exposedAssignments.IsReadOnly, Is.True);
            Assert.That(
                () => exposedAssignments[0] = Assignment("home-6", 1),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void TeamRallyPlan_RejectsDuplicateRankOrClaim()
        {
            var snapshot = CreateSnapshot();
            var duplicateRank = Assignments("home", 6);
            duplicateRank[5] = Assignment("home-6", 1);
            var duplicateClaim = Assignments("home", 6);
            duplicateClaim[5] = new PlayerResponsibilityAssignmentV3(
                new PlayerId("home-6"), RallyPlanTaskV3.Cover, RallyPlanConditionV3.Always,
                RallyPlanSpatialClaimV3.BackLeft, RallyPlanBranchV3.Primary, 1f, 6);

            Assert.That(() => new TeamRallyPlanV3(TeamSide.Home, duplicateRank, Array.Empty<string>(), snapshot.Eligibility), Throws.ArgumentException);
            Assert.That(() => new TeamRallyPlanV3(TeamSide.Home, duplicateClaim, Array.Empty<string>(), snapshot.Eligibility), Throws.ArgumentException);
        }

        [Test]
        public void RallyPlan_PreservesSnapshotAndBothSidePlansWithMetadata()
        {
            var snapshot = CreateSnapshot();
            var home = new TeamRallyPlanV3(TeamSide.Home, Assignments("home", 6), new[] { "home-candidate" }, snapshot.Eligibility);
            var away = new TeamRallyPlanV3(TeamSide.Away, Assignments("away", 6), new[] { "away-candidate" }, snapshot.Eligibility);
            var coverage = PlanCoverageDecision.Covered("revision-3", PlanCoverageReason.RallyOpen);

            var plan = new RallyPlanV3(snapshot, home, away, "trajectory-artifact-1", 3, 7, coverage);

            Assert.That(plan.WorldSnapshot, Is.SameAs(snapshot));
            Assert.That(plan.HomePlan, Is.SameAs(home));
            Assert.That(plan.AwayPlan, Is.SameAs(away));
            Assert.That(plan.ArtifactIdentity, Is.EqualTo("trajectory-artifact-1"));
            Assert.That(plan.Revision, Is.EqualTo(3));
            Assert.That(plan.SourceSequence, Is.EqualTo(7));
            Assert.That(plan.CoverageDecision, Is.SameAs(coverage));
        }

        [Test]
        public void RallyPlan_RejectsInvalidMetadataAndMismatchedPlans()
        {
            var snapshot = CreateSnapshot();
            var home = new TeamRallyPlanV3(TeamSide.Home, Assignments("home", 6), Array.Empty<string>(), snapshot.Eligibility);
            var away = new TeamRallyPlanV3(TeamSide.Away, Assignments("away", 6), Array.Empty<string>(), snapshot.Eligibility);
            var coverage = PlanCoverageDecision.Covered("revision-0", PlanCoverageReason.RallyOpen);

            Assert.That(() => new RallyPlanV3(snapshot, home, away, "artifact", -1, 0, coverage), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RallyPlanV3(snapshot, home, away, "artifact", 0, -1, coverage), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RallyPlanV3(snapshot, away, home, "artifact", 0, 0, coverage), Throws.ArgumentException);
        }

        [Test]
        public void Composer_ComposesSixEligiblePlayersWithOneExclusiveClaimEach()
        {
            var snapshot = CreateSnapshot();

            var plan = DeterministicRallyPlanComposerV3.Compose(snapshot, TeamSide.Home, "artifact-1");

            Assert.That(plan.Assignments.Count, Is.EqualTo(6));
            Assert.That(plan.Assignments.Select(assignment => assignment.PlayerId).Distinct().Count(), Is.EqualTo(6));
            Assert.That(plan.Assignments.Select(assignment => assignment.SpatialClaim).Distinct().Count(), Is.EqualTo(6));
            Assert.That(plan.Assignments.All(assignment => snapshot.Eligibility.For(assignment.PlayerId).Side == TeamSide.Home), Is.True);
        }

        [Test]
        public void Composer_ExcludesBackRowPlayersFromAttackBeforeScoring()
        {
            var snapshot = CreateSnapshot();

            var plan = DeterministicRallyPlanComposerV3.Compose(snapshot, TeamSide.Home, "artifact-1");

            Assert.That(plan.Assignments.Where(assignment => assignment.Task == RallyPlanTaskV3.Attack)
                .All(assignment => snapshot.Eligibility.For(assignment.PlayerId).CanAttackAboveNetFromFrontZone), Is.True);
        }

        [Test]
        public void Composer_UsesStableTieOrderWhenSnapshotPlayersAreReversed()
        {
            var original = CreateSnapshot();
            var reversed = CreateSnapshot(original.Players.Reverse().ToList(), original.Eligibility);

            var first = DeterministicRallyPlanComposerV3.Compose(original, TeamSide.Home, "artifact-1");
            var second = DeterministicRallyPlanComposerV3.Compose(reversed, TeamSide.Home, "artifact-1");

            Assert.That(second.Assignments.Select(assignment => assignment.PlayerId.Value), Is.EqualTo(first.Assignments.Select(assignment => assignment.PlayerId.Value)));
            Assert.That(second.Assignments.Select(assignment => assignment.Rank), Is.EqualTo(first.Assignments.Select(assignment => assignment.Rank)));
        }

        [Test]
        public void Composer_UsesThePassedArtifactIdentityIndependentlyForBothSides()
        {
            var snapshot = CreateSnapshot();

            var home = DeterministicRallyPlanComposerV3.Compose(snapshot, TeamSide.Home, "shared-artifact");
            var away = DeterministicRallyPlanComposerV3.Compose(snapshot, TeamSide.Away, "shared-artifact");

            Assert.That(home.CandidateEvidence.Single(), Is.EqualTo("artifact=shared-artifact"));
            Assert.That(away.CandidateEvidence.Single(), Is.EqualTo("artifact=shared-artifact"));
        }

        [TestCase(PlanCoverageReason.WithinConditionalEnvelope, PlanCoverageDecisionKind.CoveredActivateBranch)]
        [TestCase(PlanCoverageReason.ResponsibleActorChanged, PlanCoverageDecisionKind.LocalRevision)]
        [TestCase(PlanCoverageReason.BallEnvelopeExceeded, PlanCoverageDecisionKind.ScopedReplan)]
        [TestCase(PlanCoverageReason.EnvelopeExceeded, PlanCoverageDecisionKind.GlobalReplan)]
        [TestCase(PlanCoverageReason.RallyEnd, PlanCoverageDecisionKind.TerminalNoPlan)]
        public void Composer_EvaluatesCoverageAsBoundedDiagnosticWithoutMutatingPlan(
            PlanCoverageReason reason, PlanCoverageDecisionKind expectedKind)
        {
            var snapshot = CreateSnapshot();
            var home = DeterministicRallyPlanComposerV3.Compose(snapshot, TeamSide.Home, "artifact-1");
            var away = DeterministicRallyPlanComposerV3.Compose(snapshot, TeamSide.Away, "artifact-1");
            var plan = new RallyPlanV3(snapshot, home, away, "artifact-1", 4, 9, PlanCoverageDecision.Covered("4", PlanCoverageReason.RallyOpen));
            var before = plan.HomePlan.Assignments.Select(assignment => assignment.PlayerId.Value + ":" + assignment.Rank).ToArray();

            var coverage = DeterministicRallyPlanComposerV3.EvaluateCoverage(plan, new AcceptedRuleEventV3(reason));

            Assert.That(coverage.Kind, Is.EqualTo(expectedKind));
            Assert.That(coverage.Reason, Is.EqualTo(reason));
            Assert.That(plan.HomePlan.Assignments.Select(assignment => assignment.PlayerId.Value + ":" + assignment.Rank), Is.EqualTo(before));
        }

        private static List<PlayerResponsibilityAssignmentV3> Assignments(string prefix, int count)
        {
            var assignments = new List<PlayerResponsibilityAssignmentV3>();
            for (var index = 1; index <= count; index++)
            {
                assignments.Add(Assignment(prefix + "-" + index, index));
            }

            return assignments;
        }

        private static PlayerResponsibilityAssignmentV3 Assignment(string playerId, int rank)
        {
            return new PlayerResponsibilityAssignmentV3(
                new PlayerId(playerId), RallyPlanTaskV3.Cover, RallyPlanConditionV3.Always,
                (RallyPlanSpatialClaimV3)rank, RallyPlanBranchV3.Primary, 1f, rank);
        }

        private static RallyWorldSnapshotV3 CreateSnapshot()
        {
            var players = new List<PlayerWorldSnapshotV3>();
            for (var index = 1; index <= 6; index++)
            {
                players.Add(Player("home-" + index, TeamSide.Home));
                players.Add(Player("away-" + index, TeamSide.Away));
            }

            var homeIds = Enumerable.Range(1, 6).Select(index => new PlayerId("home-" + index)).ToArray();
            var awayIds = Enumerable.Range(1, 6).Select(index => new PlayerId("away-" + index)).ToArray();
            var positions = Enumerable.Repeat(PlayerPosition.Setter, 6).ToArray();
            var context = MatchV4TestFixture.CreateContextForRotations(
                Guid.Parse("5e19eac4-5d3d-4d52-9c8f-f4dd7680c7bd"), 31, homeIds, positions, awayIds, positions);
            var eligibility = OnCourtLineupRulesV3.Create(
                context, homeIds, awayIds, homeIds[0], awayIds[0], Array.Empty<LiberoReplacementV3>());
            return CreateSnapshot(players, eligibility);
        }

        private static RallyWorldSnapshotV3 CreateSnapshot(IReadOnlyList<PlayerWorldSnapshotV3> players, OnCourtEligibilitySnapshot eligibility)
        {
            return new RallyWorldSnapshotV3(
                new BallWorldSnapshotV3(SimVector3.Zero, SimVector3.Zero, SimVector3.Zero, 0.1f, 0f),
                players,
                TouchSequenceStateV3.Initial,
                eligibility,
                new CourtConfigurationV3(),
                new AcceptedRuleEventV3(),
                0);
        }

        private static PlayerWorldSnapshotV3 Player(string playerId, TeamSide side)
        {
            return new PlayerWorldSnapshotV3(
                new PlayerId(playerId), side, PlayerPosition.Setter,
                SimVector3.Zero, SimVector3.Zero, SimVector3.Up,
                "ready", RallyCommitmentStateV3.Uncommitted, 0f);
        }
    }
}
