using System.Linq;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    // Authoring catalog for the fixed formal openings used by runtime-loaded
    // scenario assets. These values are copied into the assets, never read at
    // scenario startup as an implicit fallback.
    public static class FormalMatchScenarioCatalogV4
    {
        public static FormalMatchScenarioDefinitionV4 Create(string scenarioId)
        {
            var context = FormalSixVsSixRallyBootstrap.CreateDefaultFormalContext();
            return scenarioId switch
            {
                "reachable-floor-defense" => Scenario(
                    scenarioId, context, TeamSide.Home,
                    Team(SpikeRoute.CrossCourt, -3.15f, -1.2f, 2.45f, 5.25f),
                    Team(SpikeRoute.Line, -3.15f, 1.2f, -2.45f, -5.25f)),
                "late-floor-defense" => Scenario(
                    scenarioId,
                    WithLateDefenseTeam(context, TeamSide.Home),
                    TeamSide.Home,
                    Team(SpikeRoute.Line, -3.15f, -1.2f, 3.45f, 5.25f),
                    Team(SpikeRoute.Line, -3.15f, 1.2f, -3.45f, -5.25f)),
                "attack-side-block-rebound" => Scenario(
                    scenarioId, context, TeamSide.Away,
                    Team(SpikeRoute.CrossCourt, -3.15f, -1.2f, 2.45f, 5.25f),
                    Team(SpikeRoute.RollShot, 3.15f, 1.2f, -1.10f, -4.05f)),
                "blocking-side-block-rebound" => Scenario(
                    scenarioId, context, TeamSide.Away,
                    Team(SpikeRoute.RollShot, -3.15f, -1.2f, 1.10f, 4.05f),
                    Team(SpikeRoute.CrossCourt, 3.15f, 1.2f, -2.45f, -5.25f)),
                "post-block-miss" => Scenario(
                    scenarioId, context, TeamSide.Home,
                    Team(SpikeRoute.Line, -3.15f, -1.2f, 3.50f, 5.25f),
                    Team(SpikeRoute.Line, 3.15f, 1.2f, -3.50f, -5.25f),
                    homeRotationOffset: 2),
                "overlapping-defenders" => Scenario(
                    scenarioId, context, TeamSide.Home,
                    Team(SpikeRoute.DeepSeam, -0.35f, -0.65f, 0f, 5.25f),
                    Team(SpikeRoute.DeepSeam, -0.35f, 0.65f, 0f, -5.25f)),
                "serve-net-deflection" => Scenario(
                    scenarioId, context, TeamSide.Home,
                    Team(SpikeRoute.CrossCourt, -3.15f, -1.2f, 2.45f, 5.25f),
                    Team(SpikeRoute.Line, -3.15f, 1.2f, -2.45f, -5.25f),
                    awayRotationOffset: 3,
                    initialServeArrivalVerticalSpeed: -7.5f),
                "serve-net-deflection-miss" => Scenario(
                    scenarioId,
                    WithLateDefenseTeam(context, TeamSide.Away),
                    TeamSide.Home,
                    Team(SpikeRoute.CrossCourt, -3.15f, -1.2f, 2.45f, 5.25f),
                    Team(SpikeRoute.Line, -3.15f, 1.2f, -2.45f, -5.25f),
                    initialServeArrivalVerticalSpeed: -10f,
                    initialServeTargetDepthOffsetMeters: -4f),
                "serve-net-rebound" => Scenario(
                    scenarioId, context, TeamSide.Home,
                    Team(SpikeRoute.CrossCourt, -3.15f, -1.2f, 2.45f, 5.25f),
                    Team(SpikeRoute.Line, -3.15f, 1.2f, -2.45f, -5.25f),
                    initialServeArrivalVerticalSpeed: -4.5f),
                _ => throw new System.ArgumentOutOfRangeException(nameof(scenarioId), scenarioId,
                    "Unknown formal match scenario."),
            };
        }

        private static MatchContextV4 WithLateDefenseTeam(
            MatchContextV4 context,
            TeamSide side)
        {
            var home = side == TeamSide.Home
                ? WithLateMovement(context.Home)
                : context.Home;
            var away = side == TeamSide.Away
                ? WithLateMovement(context.Away)
                : context.Away;
            return MatchContextV4.Create(
                context.SessionId,
                context.Seed,
                home,
                away,
                context.PhysicsConfigurationHash,
                context.TrajectoryPredictionProviderConfiguration,
                context.RulesVersion);
        }

        private static TeamSnapshotV4 WithLateMovement(TeamSnapshotV4 team)
        {
            var players = team.RotationOrder.Select(player =>
            {
                var physical = player.Physical;
                return new PlayerSnapshotV4(
                    player.PlayerId,
                    player.DisplayName,
                    player.JerseyNumber,
                    player.Position,
                    player.DominantHand,
                    new PhysicalBaseAttributesV4(
                        physical.HeightMeters,
                        physical.StandingReachMeters,
                        physical.Jump,
                        0f,
                        0f,
                        physical.Coordination),
                    player.Technical,
                    MatchAttributeDerivationConfigV4.Version1);
            }).ToArray();
            return new TeamSnapshotV4(
                team.TeamId,
                team.DisplayName,
                team.Side,
                players);
        }

        private static FormalMatchScenarioDefinitionV4 Scenario(
            string id,
            MatchContextV4 context,
            TeamSide servingSide,
            FormalMatchTacticInputV4 home,
            FormalMatchTacticInputV4 away,
            int homeRotationOffset = 0,
            int awayRotationOffset = 0,
            float initialServeFlightSeconds =
                FormalMatchScenarioDefinitionV4
                    .DefaultInitialServeFlightSeconds,
            float initialServeArrivalVerticalSpeed =
                FormalMatchScenarioDefinitionV4
                    .DefaultInitialServeArrivalVerticalSpeed,
            float initialServeTargetDepthOffsetMeters =
                FormalMatchScenarioDefinitionV4
                    .DefaultInitialServeTargetDepthOffsetMeters)
        {
            return new FormalMatchScenarioDefinitionV4(
                id, FormalMatchScenarioDefinitionV4.FormatVersion, context,
                servingSide, homeRotationOffset, awayRotationOffset,
                FormalMatchScenarioDefinitionV4.FormalIndoorConfigurationIdentity,
                home, away, new FormalMatchAiInputV4(),
                initialServeFlightSeconds,
                initialServeArrivalVerticalSpeed,
                initialServeTargetDepthOffsetMeters);
        }

        private static FormalMatchTacticInputV4 Team(
            SpikeRoute spikeRoute, float attackerX, float attackerZ,
            float defenderX, float defenderZ)
        {
            var defendingBlue = defenderZ < 0f;
            var blockerX = attackerX;
            var blockerZ = defendingBlue ? -0.65f : 0.65f;
            return new FormalMatchTacticInputV4
            {
                SetRoute = SetRoute.LeftPin,
                SpikeRoute = spikeRoute,
                SetterX = -0.45f,
                SetterZ = attackerZ < 0f ? -3.35f : 3.35f,
                AttackerX = attackerX,
                AttackerZ = attackerZ,
                DefenderX = defenderX,
                DefenderZ = defenderZ,
                Blocker = PlayerRole.Attacker,
                BlockX = blockerX,
                BlockZ = blockerZ,
                CoverReceiver = PlayerRole.Setter,
                CoverX = -attackerX * 0.35f,
                CoverZ = defenderZ < 0f ? -4.15f : 4.15f,
                SetRhythm = SetRhythm.FastPin,
                AttackFlightSeconds = spikeRoute == SpikeRoute.RollShot ? 0.60f : 0.45f,
            };
        }
    }
}
