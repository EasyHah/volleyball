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
        }
    }
}
