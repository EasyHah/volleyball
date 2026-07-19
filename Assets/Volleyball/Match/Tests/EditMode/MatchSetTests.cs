using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Domain;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class MatchSetTests
    {
        [Test]
        public void ResolveRally_ReceivingTeamWins_TakesServiceAndRotatesOnce()
        {
            var set = CreateSet(TeamSide.Home);

            set.ResolveRally(TeamSide.Away, null, null);

            Assert.That(set.AwayScore, Is.EqualTo(1));
            Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Away));
            Assert.That(set.RotationOffsetFor(TeamSide.Away), Is.EqualTo(1));
            Assert.That(set.RotationOffsetFor(TeamSide.Home), Is.Zero);
        }

        [Test]
        public void ResolveRally_ServingTeamWins_KeepsServiceWithoutRotation()
        {
            var set = CreateSet(TeamSide.Home);

            set.ResolveRally(TeamSide.Home, null, null);

            Assert.That(set.HomeScore, Is.EqualTo(1));
            Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Home));
            Assert.That(set.RotationOffsetFor(TeamSide.Home), Is.Zero);
        }

        [Test]
        public void ReceivingSide_IsAlwaysTheOpponentOfTheServingSide()
        {
            var set = CreateSet(TeamSide.Home);

            Assert.That(set.ReceivingSide, Is.EqualTo(TeamSide.Away));

            set.ResolveRally(TeamSide.Away, null, null);

            Assert.That(set.ReceivingSide, Is.EqualTo(TeamSide.Home));
        }

        [Test]
        public void ResolveRally_AtFourteenAll_RequiresTwoPointLeadToComplete()
        {
            var set = CreateSet(TeamSide.Home);
            Resolve(set, TeamSide.Home, 14);
            Resolve(set, TeamSide.Away, 14);

            set.ResolveRally(TeamSide.Home, null, null);
            Assert.That(set.IsComplete, Is.False);

            set.ResolveRally(TeamSide.Home, null, null);

            Assert.That(set.IsComplete, Is.True);
            Assert.That(set.HomeScore, Is.EqualTo(16));
            Assert.That(set.AwayScore, Is.EqualTo(14));
        }

        [Test]
        public void CreateResult_CompletedSet_ContainsAllSixPlayersAndValidatedStatistics()
        {
            var set = CreateSet(TeamSide.Home);
            var homeSetter = new StablePlayerId("home-setter");
            var awayDefender = new StablePlayerId("away-defender");

            set.RecordContact(homeSetter, 3.5f);
            set.ResolveRally(TeamSide.Home, homeSetter, awayDefender);
            Resolve(set, TeamSide.Home, 14);
            var result = set.CreateResult();

            Assert.That(result.PlayerStats, Has.Count.EqualTo(6));
            Assert.That(Stat(result, homeSetter).Points, Is.EqualTo(1));
            Assert.That(Stat(result, homeSetter).Contacts, Is.EqualTo(1));
            Assert.That(Stat(result, homeSetter).Workload, Is.EqualTo(4.5f));
            Assert.That(Stat(result, awayDefender).Errors, Is.EqualTo(1));
            Assert.DoesNotThrow(() => result.ValidateAgainst(set.Context));
        }

        [Test]
        public void CreateResult_IncompleteSet_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => CreateSet(TeamSide.Home).CreateResult());
        }

        [Test]
        public void FormalIndoorSixPlayerSet_UsesTwentyFivePointsAndAllSixRotationPositions()
        {
            var set = new MatchSet(CreateSixPlayerContext(), TeamSide.Home);
            var initialServers = new[]
            {
                set.ServerFor(TeamSide.Away),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.FrontRight),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.FrontCenter),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.FrontLeft),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.BackLeft),
                set.PlayerAtRotationPosition(TeamSide.Away, IndoorRotationPosition.BackCenter)
            };

            Assert.That(set.SetTargetScore, Is.EqualTo(25));
            Assert.That(set.RosterSize, Is.EqualTo(6));
            Assert.That(set.IsFrontRow(initialServers[1]), Is.True);
            Assert.That(set.IsFrontRow(initialServers[2]), Is.True);
            Assert.That(set.IsFrontRow(initialServers[3]), Is.True);
            Assert.That(set.IsFrontRow(initialServers[0]), Is.False);
            Assert.That(set.IsFrontRow(initialServers[4]), Is.False);
            Assert.That(set.IsFrontRow(initialServers[5]), Is.False);

            set.ResolveRally(TeamSide.Away, null, null);

            Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Away));
            Assert.That(set.ServerFor(TeamSide.Away), Is.EqualTo(initialServers[1]));
            Assert.That(set.RotationPositionFor(initialServers[0]), Is.EqualTo(6));
            Assert.That(set.RotationOffsetFor(TeamSide.Away), Is.EqualTo(1));
        }

        [Test]
        public void FormalIndoorSet_AtTwentyFourAllRequiresTwoPointLead()
        {
            var set = new MatchSet(CreateSixPlayerContext(), TeamSide.Home);
            Resolve(set, TeamSide.Home, 24);
            Resolve(set, TeamSide.Away, 24);

            set.ResolveRally(TeamSide.Home, null, null);
            Assert.That(set.IsComplete, Is.False);
            set.ResolveRally(TeamSide.Home, null, null);

            Assert.That(set.IsComplete, Is.True);
            Assert.That(set.HomeScore, Is.EqualTo(26));
            Assert.That(set.AwayScore, Is.EqualTo(24));
            Assert.That(set.CreateResult().PlayerStats, Has.Count.EqualTo(12));
        }

        private static void Resolve(MatchSet set, TeamSide winner, int count)
        {
            for (var index = 0; index < count; index++)
            {
                set.ResolveRally(winner, null, null);
            }
        }

        private static PlayerMatchStatsV1 Stat(MatchResultV1 result, StablePlayerId playerId)
        {
            foreach (var stat in result.PlayerStats)
            {
                if (stat.PlayerId.Equals(playerId))
                {
                    return stat;
                }
            }

            Assert.Fail("Missing stats for " + playerId.Value);
            return null;
        }

        private static MatchSet CreateSet(TeamSide servingSide)
        {
            return new MatchSet(CreateContext(), servingSide);
        }

        private static MatchContextV1 CreateContext()
        {
            return MatchContextV1.Create(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                7351,
                CreateTeam("home", "Home", TeamSide.Home, "home"),
                CreateTeam("away", "Away", TeamSide.Away, "away"));
        }

        private static MatchContextV1 CreateSixPlayerContext()
        {
            return MatchContextV1.Create(
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                7351,
                CreateSixPlayerTeam("home6", "Home 6", TeamSide.Home, "home"),
                CreateSixPlayerTeam("away6", "Away 6", TeamSide.Away, "away"));
        }

        private static TeamSnapshotV1 CreateSixPlayerTeam(
            string id,
            string name,
            TeamSide side,
            string prefix)
        {
            return new TeamSnapshotV1(
                new TeamId(id),
                name,
                side,
                new List<PlayerSnapshotV1>
                {
                    CreatePlayer(prefix + "-opposite", "Opposite", 1, PlayerPosition.Opposite),
                    CreatePlayer(prefix + "-outside-a", "Outside A", 2, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-middle-a", "Middle A", 3, PlayerPosition.MiddleBlocker),
                    CreatePlayer(prefix + "-setter", "Setter", 4, PlayerPosition.Setter),
                    CreatePlayer(prefix + "-outside-b", "Outside B", 5, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-libero", "Libero", 6, PlayerPosition.Libero)
                });
        }

        private static TeamSnapshotV1 CreateTeam(string id, string name, TeamSide side, string prefix)
        {
            return new TeamSnapshotV1(
                new TeamId(id),
                name,
                side,
                new List<PlayerSnapshotV1>
                {
                    CreatePlayer(prefix + "-setter", "Setter", 1, PlayerPosition.Setter),
                    CreatePlayer(prefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-defender", "Defender", 3, PlayerPosition.Defender)
                });
        }

        private static PlayerSnapshotV1 CreatePlayer(string id, string name, int number, PlayerPosition position)
        {
            return new PlayerSnapshotV1(
                new StablePlayerId(id),
                name,
                number,
                position,
                new PlayerAbilitySnapshotV1(0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f));
        }
    }
}
