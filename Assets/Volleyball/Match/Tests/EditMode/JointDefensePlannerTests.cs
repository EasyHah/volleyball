using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using ContractPlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class JointDefensePlannerTests
    {
        [Test]
        public void Plan_ReadsPublicThreatWithoutFinalRoute()
        {
            var request = Fixture.DefenseRequest(Fixture.LineHeavyThreat());
            var defense = new JointDefensePlanner().Plan(request);

            Assert.That(defense.SourceThreatIdentity, Is.EqualTo(request.PublicThreat.ThreatIdentity));
            Assert.That(defense.Responsibilities, Has.Count.EqualTo(6));
            Assert.That(typeof(JointDefensePlanningRequestV3).GetProperties().Select(value => value.Name),
                Has.None.Matches<string>(value => value.Contains("FinalRoute") || value.Contains("Sample")));
        }

        [Test]
        public void Plan_FloorCoverageTargetsResidualThreat()
        {
            var defense = new JointDefensePlanner().Plan(Fixture.DefenseRequest(Fixture.LineHeavyThreat()));

            Assert.That(defense.BlockedZones, Does.Contain("Line"));
            Assert.That(defense.FloorCoveredZones, Does.Contain("Cross"));
            Assert.That(defense.FloorCoveredZones, Does.Not.Contain("Line"));
        }

        [Test]
        public void Plan_IsInvariantToHiddenFinalRoute()
        {
            var first = new JointDefensePlanner().Plan(Fixture.RequestWithHiddenFinal("line"));
            var second = new JointDefensePlanner().Plan(Fixture.RequestWithHiddenFinal("cross"));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Plan_UsesStableContractPlayerIdentityWhenEquivalentPlayersArePermuted()
        {
            var original = Fixture.TiedDefenseRequest();
            var permuted = Fixture.Permute(original);

            var first = new JointDefensePlanner().Plan(original);
            var second = new JointDefensePlanner().Plan(permuted);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Responsibilities.Single(value => value.Kind == DefenseResponsibilityKindV3.PrimaryBlock).Actor,
                Is.EqualTo(new ContractPlayerId("orange-defense-0")));
        }

        [Test]
        public void Request_RejectsPerceptionFromAnotherRevisionOrPublicThreat()
        {
            var request = Fixture.DefenseRequest(Fixture.LineHeavyThreat());
            var mismatchedRevision = Fixture.PerceptionReceipt(request, revision: 13,
                artifact: "trajectory-12");
            var mismatchedThreat = Fixture.PerceptionReceipt(request,
                revision: request.Revision, artifact: "trajectory-12",
                zone: "Other");

            Assert.That(() => new JointDefensePlanningRequestV3(request.Revision,
                request.DefendingSide, request.PublicThreat, request.Players,
                request.Assignments, request.Exits, mismatchedRevision),
                Throws.ArgumentException);
            Assert.That(() => new JointDefensePlanningRequestV3(request.Revision,
                request.DefendingSide, request.PublicThreat, request.Players,
                request.Assignments, request.Exits, mismatchedThreat),
                Throws.ArgumentException);
        }

        [Test]
        public void Request_AcceptsBoundedPerceivedArrivalDifference()
        {
            var request = Fixture.DefenseRequest(Fixture.LineHeavyThreat());
            var receipt = Fixture.PerceptionReceipt(request,
                request.Revision, "trajectory-12", arrivalOffset: .2f);

            Assert.That(() => Fixture.WithPerception(request, receipt),
                Throws.Nothing);
        }

        [Test]
        public void Perception_MayChangeFloorSupportButNotBlockerIdentity()
        {
            var baselineRequest = Fixture.DefenseRequest(Fixture.LineHeavyThreat());
            var baseline = new JointDefensePlanner().Plan(baselineRequest);
            var receipt = Fixture.PerceptionReceipt(baselineRequest,
                baselineRequest.Revision, "trajectory-12");
            var perceived = new JointDefensePlanner().Plan(
                Fixture.WithPerception(baselineRequest, receipt));

            CollectionAssert.AreEqual(
                baseline.Responsibilities.Where(IsBlock)
                    .Select(value => value.Actor),
                perceived.Responsibilities.Where(IsBlock)
                    .Select(value => value.Actor));
            Assert.That(perceived.Responsibilities.Single(value =>
                    value.Actor.Equals(
                        receipt.SupportDecision.SelectedPlayer)).Kind,
                Is.EqualTo(DefenseResponsibilityKindV3.CrossDefense));
            CollectionAssert.AreEqual(
                baseline.Responsibilities.Select(value => value.Actor),
                perceived.Responsibilities.Select(value => value.Actor),
                "Perception must not reorder command publication identities.");
        }

        private static bool IsBlock(DefenseResponsibilityV3 value) =>
            value.Kind == DefenseResponsibilityKindV3.PrimaryBlock ||
            value.Kind == DefenseResponsibilityKindV3.SupportingBlock;

        private static class Fixture
        {
            public static JointDefensePlanningRequestV3 DefenseRequest(PublicAttackThreatV3 threat)
            {
                var players = new List<DefensePlayerSnapshotV3>();
                var assignments = new List<PlayerResponsibilityAssignmentV3>();
                for (var index = 0; index < 6; index++)
                {
                    var id = new ContractPlayerId("orange-defense-" + index);
                    players.Add(new DefensePlayerSnapshotV3(id, new SimVector3(index - 2.5f, 0f, 0.25f), 5f, .8f, index < 3));
                    assignments.Add(new PlayerResponsibilityAssignmentV3(id, RallyPlanTaskV3.Defend,
                        RallyPlanConditionV3.BallOnOpponentSide, (RallyPlanSpatialClaimV3)(index + 1),
                        RallyPlanBranchV3.Primary, 1f, index + 1));
                }

                return new JointDefensePlanningRequestV3(12, TeamSide.Home, threat, players, assignments,
                    new[] { new ReorganizationExitV3("exit-dig", players[5].Id, "Dig" ) });
            }

            public static PublicAttackThreatV3 LineHeavyThreat() => new PublicAttackThreatV3("threat-12", new[]
            {
                new PublicAttackThreatEntryV3(AttackActionClassV3.PowerLine, "Line", .8f, .4f),
                new PublicAttackThreatEntryV3(AttackActionClassV3.PowerCross, "Cross", .2f, .4f)
            });

            // The hidden route lives outside the request; this mirrors an unavailable future outcome.
            public static JointDefensePlanningRequestV3 RequestWithHiddenFinal(string hiddenFinalRoute) =>
                DefenseRequest(LineHeavyThreat());

            public static JointDefensePlanningRequestV3 TiedDefenseRequest()
            {
                var request = DefenseRequest(LineHeavyThreat());
                var tiedPlayers = request.Players.Select(value => new DefensePlayerSnapshotV3(
                    value.Id, new SimVector3(0f, 0f, .25f), 5f, .8f, value.IsFrontRow)).ToArray();
                return new JointDefensePlanningRequestV3(request.Revision, request.DefendingSide, request.PublicThreat,
                    tiedPlayers, request.Assignments, request.Exits);
            }

            public static JointDefensePlanningRequestV3 Permute(JointDefensePlanningRequestV3 request) =>
                new JointDefensePlanningRequestV3(request.Revision, request.DefendingSide, request.PublicThreat,
                    request.Players.Reverse().ToArray(), request.Assignments.Reverse().ToArray(), request.Exits);

            public static JointDefensePlanningRequestV3 WithPerception(
                JointDefensePlanningRequestV3 request,
                PerceptionReceiptV3 receipt) =>
                new JointDefensePlanningRequestV3(request.Revision,
                    request.DefendingSide, request.PublicThreat, request.Players,
                    request.Assignments, request.Exits, receipt);

            public static PerceptionReceiptV3 PerceptionReceipt(
                JointDefensePlanningRequestV3 request, long revision,
                string artifact, string zone = null, float arrivalOffset = 0f)
            {
                var selected = request.Players[5].Id;
                var threats = request.PublicThreat.Entries.Select((entry, index) =>
                    new PerceivedThreatEntryV3("threat-" + index,
                        zone ?? entry.Zone, entry.Probability,
                        entry.ArrivalTime + arrivalOffset)).ToArray();
                var view = new TeamPerceptionSnapshotV3("view-" + revision,
                    artifact, request.DefendingSide, revision, 9,
                    new[] { new PlayerPerceptionSnapshotV3(selected, .8f, .1f) },
                    threats,
                    new[] { new PerceivedSupportCandidateV3(selected, .8f,
                        .5f, false) });
                return new PerceptionReceiptV3("gate-j-v1", view,
                    new PerceptionSupportDecisionV3(selected, false, .8f));
            }
        }
    }
}
