using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class FullRallyV3EligibilityTests
    {
        [Test]
        public void Create_MapsSuppliedRotationOrderAndEligibilityForFormalSix()
        {
            var snapshot = CreateSnapshot(CreateV3ContextWithBenchPlayers());

            Assert.That(snapshot.Players, Has.Count.EqualTo(12));
            Assert.That(snapshot.Players.Take(6).Select(player => player.PlayerId), Is.EqualTo(HomeRotationOrder));
            Assert.That(snapshot.Players.Skip(6).Select(player => player.PlayerId), Is.EqualTo(AwayRotationOrder));
            Assert.That(snapshot.For(HomeRotationOrder[0]).RotationPosition, Is.EqualTo(1));
            Assert.That(snapshot.For(HomeRotationOrder[0]).IsFrontRow, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[0]).IsCurrentServer, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[0]).CanBlock, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[1]).RotationPosition, Is.EqualTo(2));
            Assert.That(snapshot.For(HomeRotationOrder[1]).IsFrontRow, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[1]).IsCurrentServer, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[1]).CanBlock, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[2]).RotationPosition, Is.EqualTo(3));
            Assert.That(snapshot.For(HomeRotationOrder[2]).IsFrontRow, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[2]).CanAttackAboveNetFromFrontZone, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[3]).RotationPosition, Is.EqualTo(4));
            Assert.That(snapshot.For(HomeRotationOrder[3]).IsFrontRow, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[3]).CanBlock, Is.True);
            Assert.That(snapshot.For(HomeRotationOrder[4]).RotationPosition, Is.EqualTo(5));
            Assert.That(snapshot.For(HomeRotationOrder[4]).IsFrontRow, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[5]).RotationPosition, Is.EqualTo(6));
            Assert.That(snapshot.For(HomeRotationOrder[5]).CanBlock, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[5]).CanAttackAboveNetFromFrontZone, Is.False);
            Assert.That(snapshot.For(HomeRotationOrder[5]).RegisteredPosition, Is.EqualTo(PlayerPosition.Libero));
        }

        [Test]
        public void Create_ExcludesRosteredPlayersOutsideTheSuppliedSix()
        {
            var snapshot = CreateSnapshot(CreateV3ContextWithBenchPlayers());

            Assert.That(snapshot.Players, Has.None.Matches<OnCourtPlayerEligibilityV3>(
                player => player.PlayerId.Equals(new PlayerId("home-bench"))));
            Assert.Throws<KeyNotFoundException>(() => snapshot.For(new PlayerId("home-bench")));
        }

        [Test]
        public void Create_RejectsServerOutsideItsSuppliedRotation()
        {
            var context = CreateV3ContextWithBenchPlayers();

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, HomeRotationOrder, AwayRotationOrder, new PlayerId("home-bench"), AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>()));
        }

        [Test]
        public void Create_RejectsDuplicateIdsWithinASuppliedRotation()
        {
            var context = CreateV3ContextWithBenchPlayers();
            var duplicateHomeRotation = new[]
            {
                HomeRotationOrder[0], HomeRotationOrder[0], HomeRotationOrder[2], HomeRotationOrder[3], HomeRotationOrder[4], HomeRotationOrder[5]
            };

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, duplicateHomeRotation, AwayRotationOrder, HomeRotationOrder[0], AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>()));
        }

        [Test]
        public void Create_RequiresExactlySixIdsPerSuppliedRotation()
        {
            var context = CreateV3ContextWithBenchPlayers();

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, new[] { HomeRotationOrder[0], HomeRotationOrder[1], HomeRotationOrder[2], HomeRotationOrder[3], HomeRotationOrder[4] },
                AwayRotationOrder, HomeRotationOrder[0], AwayRotationOrder[0], Array.Empty<LiberoReplacementV3>()));
        }

        [Test]
        public void Create_RejectsLiberoAndReplacedPlayerCoexistingOnCourt()
        {
            var context = CreateV3ContextWithBenchPlayers();
            var replacement = new LiberoReplacementV3(HomeLiberoId, HomeReplacedPlayerId);

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, HomeRotationOrder, AwayRotationOrder, HomeRotationOrder[0], AwayRotationOrder[0], new[] { replacement }));
        }

        [Test]
        public void Create_MapsValidLiberoReplacementAndKeepsOnlySixPlayersPerSide()
        {
            var context = CreateV3ContextWithBenchPlayers();
            var snapshot = OnCourtLineupRulesV3.Create(
                context, HomeReplacementRotationOrder, AwayRotationOrder,
                HomeReplacementRotationOrder[0], AwayRotationOrder[0],
                new[] { new LiberoReplacementV3(HomeLiberoId, HomeReplacedPlayerId) });

            var libero = snapshot.For(HomeLiberoId);
            Assert.That(snapshot.Players, Has.Count.EqualTo(12));
            Assert.That(snapshot.Players.Count(player => player.Side == TeamSide.Home), Is.EqualTo(6));
            Assert.That(snapshot.Players.Count(player => player.Side == TeamSide.Away), Is.EqualTo(6));
            Assert.That(libero.RotationPosition, Is.EqualTo(2));
            Assert.That(libero.IsFrontRow, Is.True);
            Assert.That(libero.CanBlock, Is.False);
            Assert.That(libero.ReplacedPlayerId, Is.EqualTo((PlayerId?)HomeReplacedPlayerId));
            Assert.That(snapshot.Players, Has.None.Matches<OnCourtPlayerEligibilityV3>(
                player => player.PlayerId.Equals(HomeReplacedPlayerId)));
            Assert.Throws<KeyNotFoundException>(() => snapshot.For(HomeReplacedPlayerId));
        }

        [Test]
        public void Create_RejectsRotationIdsFromTheOpposingOrUnknownRoster()
        {
            var context = CreateV3ContextWithBenchPlayers();
            var opposingIdRotation = new[]
            {
                AwayRotationOrder[0], HomeRotationOrder[1], HomeRotationOrder[2],
                HomeRotationOrder[3], HomeRotationOrder[4], HomeRotationOrder[5]
            };
            var unknownIdRotation = new[]
            {
                new PlayerId("unknown-player"), HomeRotationOrder[1], HomeRotationOrder[2],
                HomeRotationOrder[3], HomeRotationOrder[4], HomeRotationOrder[5]
            };

            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, opposingIdRotation, AwayRotationOrder, opposingIdRotation[0], AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>()));
            Assert.Throws<ArgumentException>(() => OnCourtLineupRulesV3.Create(
                context, unknownIdRotation, AwayRotationOrder, unknownIdRotation[0], AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>()));
        }

        [Test]
        public void OnCourtEligibilitySnapshot_HasNoParameterlessConstructor()
        {
            Assert.That(typeof(OnCourtEligibilitySnapshot).GetConstructor(Type.EmptyTypes), Is.Null);
        }

        private static readonly PlayerId HomeLiberoId = new PlayerId("home-libero");
        private static readonly PlayerId HomeReplacedPlayerId = new PlayerId("home-5");

        private static readonly PlayerId[] HomeRotationOrder =
        {
            new PlayerId("home-4"), new PlayerId("home-1"), new PlayerId("home-3"),
            new PlayerId("home-2"), HomeReplacedPlayerId, HomeLiberoId
        };

        private static readonly PlayerId[] HomeReplacementRotationOrder =
        {
            new PlayerId("home-4"), HomeLiberoId, new PlayerId("home-1"),
            new PlayerId("home-2"), new PlayerId("home-3"), new PlayerId("home-bench")
        };

        private static readonly PlayerId[] AwayRotationOrder =
        {
            new PlayerId("away-3"), new PlayerId("away-1"), new PlayerId("away-libero"),
            new PlayerId("away-4"), new PlayerId("away-5"), new PlayerId("away-2")
        };

        private static OnCourtEligibilitySnapshot CreateSnapshot(MatchContextV3 context)
        {
            return OnCourtLineupRulesV3.Create(
                context, HomeRotationOrder, AwayRotationOrder, HomeRotationOrder[0], AwayRotationOrder[0],
                Array.Empty<LiberoReplacementV3>());
        }

        private static MatchContextV3 CreateV3ContextWithBenchPlayers()
        {
            return MatchContextV3.Create(
                Guid.Parse("d2719a73-5270-4d89-84a5-2040cdd86210"),
                17,
                CreateTeam("home", TeamSide.Home, "home"),
                CreateTeam("away", TeamSide.Away, "away"));
        }

        private static TeamSnapshotV3 CreateTeam(string teamId, TeamSide side, string prefix)
        {
            return new TeamSnapshotV3(
                new TeamId(teamId), prefix + " team", side,
                new[]
                {
                    CreatePlayer(prefix + "-1", 1, PlayerPosition.Setter),
                    CreatePlayer(prefix + "-2", 2, PlayerPosition.OutsideHitter),
                    CreatePlayer(prefix + "-3", 3, PlayerPosition.MiddleBlocker),
                    CreatePlayer(prefix + "-4", 4, PlayerPosition.Opposite),
                    CreatePlayer(prefix + "-5", 5, PlayerPosition.Defender),
                    CreatePlayer(prefix + "-libero", 6, PlayerPosition.Libero),
                    CreatePlayer(prefix + "-bench", 7, PlayerPosition.OutsideHitter)
                });
        }

        private static PlayerSnapshotV3 CreatePlayer(string playerId, int jerseyNumber, PlayerPosition position)
        {
            return new PlayerSnapshotV3(
                new PlayerId(playerId), playerId, jerseyNumber, position,
                new PlayerAbilitySnapshotV3(
                    0.5f, 0.5f, 0.5f, 3.3f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
                    ContractVersions.MatchV3, 0, false, Array.Empty<string>()));
        }
    }
}
