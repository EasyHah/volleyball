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
