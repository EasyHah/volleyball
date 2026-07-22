using System;
using System.Collections.Generic;
using Volleyball.Shared.Contracts;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Shared.MatchV2.EditModeTests
{
    internal static class MatchV2TestFactory
    {
        public static MatchVersionSetV2 Versions(int? simulation = null, int? random = null)
        {
            return new MatchVersionSetV2(2, 1, 1, 1, simulation, random);
        }

        public static MatchAbilitySnapshotV2 Abilities(int start)
        {
            return new MatchAbilitySnapshotV2(
                start, start + 1, start + 2, start + 3,
                start + 4, start + 5, start + 6, start + 7);
        }

        public static MatchTeamSnapshotV2[] CreateTeams()
        {
            return new[]
            {
                Team("team.university.first", TeamSideV2.Home, "home"),
                Team("team.university.rival", TeamSideV2.Away, "away")
            };
        }

        public static MatchContextV2 CreateContext(
            IReadOnlyList<MatchTeamSnapshotV2> teams,
            MatchExecutionModeV2 mode = MatchExecutionModeV2.Fixture,
            string fixtureId = "fixture.career.u1w1.6v6",
            int? fixtureVersion = 1,
            int? simulationVersion = null,
            int? randomVersion = null)
        {
            return MatchContextV2.Create(
                Versions(simulationVersion, randomVersion),
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                mode,
                fixtureId,
                fixtureVersion,
                25649701u,
                "competition.university.v1",
                "schedule.u1w1.match.01",
                7500,
                new MatchFormatV2("indoor_6v6", 6, 1, 25, 2),
                PreMatchPriorityV2.AttackFirst,
                teams);
        }

        public static MatchPlayerFactsV2 ZeroFacts(PlayerId id)
        {
            return new MatchPlayerFactsV2(
                id,
                new SpikeFactsV2(0, 0, 0),
                new ServeFactsV2(0, 0, 0),
                new ReceptionFactsV2(0, 0, 0, 0, 0, 0),
                new DefenseFactsV2(0, 0),
                new BlockFactsV2(0, 0, 0),
                new MatchLoadFactsV2(0, 0, 0, 0, 0, 0, 0),
                new StabilityFactsV2(0, 0, 0, 0, 0));
        }

        public static MatchPlayerFactsV2[] ZeroFacts(MatchContextV2 context)
        {
            var facts = new List<MatchPlayerFactsV2>();
            foreach (var team in context.Teams)
            {
                foreach (var player in team.Players)
                {
                    facts.Add(ZeroFacts(player.PlayerId));
                }
            }

            return facts.ToArray();
        }

        private static MatchTeamSnapshotV2 Team(string id, TeamSideV2 side, string prefix)
        {
            var players = new[]
            {
                Player(prefix + ".opposite", 1, PlayerPositionV2.Opposite, 1, 6100),
                Player(side == TeamSideV2.Home ? "player.career.protagonist" : prefix + ".outside.a",
                    2, PlayerPositionV2.OutsideHitter, 2,
                    side == TeamSideV2.Home ? 7123 : 6200),
                Player(prefix + ".middle", 3, PlayerPositionV2.MiddleBlocker, 3, 6300),
                Player(prefix + ".setter", 4, PlayerPositionV2.Setter, 4, 6400),
                Player(prefix + ".outside.b", 5, PlayerPositionV2.OutsideHitter, 5, 6500),
                Player(prefix + ".libero", 6, PlayerPositionV2.Libero, 6, 6600)
            };
            if (side == TeamSideV2.Home)
            {
                players[1] = new MatchPlayerSnapshotV2(
                    new PlayerId("player.career.protagonist"), 2,
                    PlayerPositionV2.OutsideHitter, 2, 8800,
                    new MatchAbilitySnapshotV2(7123, 6234, 7345, 6456, 7567, 6678, 7789, 6890));
            }

            return new MatchTeamSnapshotV2(new TeamId(id), side, players);
        }

        private static MatchPlayerSnapshotV2 Player(
            string id,
            int jersey,
            PlayerPositionV2 position,
            int rotation,
            int abilityStart)
        {
            return new MatchPlayerSnapshotV2(
                new PlayerId("player." + id), jersey, position, rotation, 9000, Abilities(abilityStart));
        }
    }
}
