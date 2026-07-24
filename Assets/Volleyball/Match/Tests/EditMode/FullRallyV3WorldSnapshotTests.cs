using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class FullRallyV3WorldSnapshotTests
    {
        [Test]
        public void RallyWorldSnapshot_ContainsExactlySixImmutablePlayersPerSide()
        {
            var players = CreateTwelvePlayers();
            var snapshot = CreateSnapshot(players);
            players[0] = players[1];

            Assert.That(snapshot.Players, Has.Count.EqualTo(12));
            Assert.That(snapshot.Players.Count(player => player.Side == TeamSide.Home), Is.EqualTo(6));
            Assert.That(snapshot.Players.Count(player => player.Side == TeamSide.Away), Is.EqualTo(6));
            Assert.That(snapshot.Players[0].PlayerId.Value, Is.EqualTo("home-1"));
            Assert.Throws<NotSupportedException>(
                () => ((IList<PlayerWorldSnapshotV3>)snapshot.Players)[0] = snapshot.Players[1]);
        }

        [Test]
        public void RallyWorldSnapshot_RejectsSeventhPlayerOnOneSide()
        {
            var players = CreateTwelvePlayers();
            players[11] = CreatePlayer("home-7", TeamSide.Home);

            Assert.Throws<ArgumentException>(() => CreateSnapshot(players));
        }

        [Test]
        public void BallWorldSnapshot_RejectsNonFiniteVectorsAndScalars()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallWorldSnapshotV3(new SimVector3(float.NaN, 0f, 0f), SimVector3.Zero, SimVector3.Zero, 0.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallWorldSnapshotV3(SimVector3.Zero, SimVector3.Zero, SimVector3.Zero, float.PositiveInfinity, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallWorldSnapshotV3(SimVector3.Zero, SimVector3.Zero, SimVector3.Zero, 0.1f, -0.01f));
        }

        [Test]
        public void PlayerWorldSnapshot_RejectsInvalidFacts()
        {
            Assert.Throws<ArgumentException>(
                () => new PlayerWorldSnapshotV3(
                    default(PlayerId), TeamSide.Home, PlayerPosition.Setter,
                    SimVector3.Zero, SimVector3.Zero, SimVector3.Up,
                    "ready", RallyCommitmentStateV3.Uncommitted, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerWorldSnapshotV3(
                    new PlayerId("home-1"), TeamSide.Home, PlayerPosition.Setter,
                    new SimVector3(float.NaN, 0f, 0f), SimVector3.Zero, SimVector3.Up,
                    "ready", RallyCommitmentStateV3.Uncommitted, 0f));
            Assert.Throws<ArgumentException>(
                () => new PlayerWorldSnapshotV3(
                    new PlayerId("home-1"), TeamSide.Home, PlayerPosition.Setter,
                    SimVector3.Zero, SimVector3.Zero, SimVector3.Up,
                    " ", RallyCommitmentStateV3.Uncommitted, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerWorldSnapshotV3(
                    new PlayerId("home-1"), TeamSide.Home, PlayerPosition.Setter,
                    SimVector3.Zero, SimVector3.Zero, SimVector3.Up,
                    "ready", RallyCommitmentStateV3.Uncommitted, -0.01f));
        }

        [Test]
        public void PlayerWorldSnapshot_RejectsUndefinedEnumValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerWorldSnapshotV3(
                    new PlayerId("home-1"), (TeamSide)99, PlayerPosition.Setter,
                    SimVector3.Zero, SimVector3.Zero, SimVector3.Up,
                    "ready", RallyCommitmentStateV3.Uncommitted, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerWorldSnapshotV3(
                    new PlayerId("home-1"), TeamSide.Home, (PlayerPosition)99,
                    SimVector3.Zero, SimVector3.Zero, SimVector3.Up,
                    "ready", RallyCommitmentStateV3.Uncommitted, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlayerWorldSnapshotV3(
                    new PlayerId("home-1"), TeamSide.Home, PlayerPosition.Setter,
                    SimVector3.Zero, SimVector3.Zero, SimVector3.Up,
                    "ready", (RallyCommitmentStateV3)99, 0f));
        }

        [Test]
        public void RallyWorldSnapshot_RejectsDuplicatePlayerIds()
        {
            var players = CreateTwelvePlayers();
            players[11] = CreatePlayer("home-1", TeamSide.Away);

            Assert.Throws<ArgumentException>(() => CreateSnapshot(players));
        }

        [Test]
        public void RallyWorldSnapshot_RejectsNegativeEventSequence()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSnapshot(CreateTwelvePlayers(), -1));
        }

        private static RallyWorldSnapshotV3 CreateSnapshot(IReadOnlyList<PlayerWorldSnapshotV3> players, long eventSequence = 0)
        {
            return new RallyWorldSnapshotV3(
                new BallWorldSnapshotV3(SimVector3.Zero, SimVector3.Zero, SimVector3.Zero, 0.1f, 0f),
                players,
                TouchSequenceStateV3.Initial,
                CreateFormalEligibility(),
                new CourtConfigurationV3(),
                new AcceptedRuleEventV3(),
                eventSequence);
        }

        private static List<PlayerWorldSnapshotV3> CreateTwelvePlayers()
        {
            var players = new List<PlayerWorldSnapshotV3>();
            for (var index = 1; index <= 6; index++)
            {
                players.Add(CreatePlayer("home-" + index, TeamSide.Home));
            }

            for (var index = 1; index <= 6; index++)
            {
                players.Add(CreatePlayer("away-" + index, TeamSide.Away));
            }

            return players;
        }

        private static PlayerWorldSnapshotV3 CreatePlayer(string playerId, TeamSide side)
        {
            return new PlayerWorldSnapshotV3(
                new PlayerId(playerId), side, PlayerPosition.Setter,
                SimVector3.Zero, SimVector3.Zero, SimVector3.Up,
                "ready", RallyCommitmentStateV3.Uncommitted, 0f);
        }

        private static OnCourtEligibilitySnapshot CreateFormalEligibility()
        {
            var homeIds = new[]
            {
                new PlayerId("home-1"), new PlayerId("home-2"), new PlayerId("home-3"),
                new PlayerId("home-4"), new PlayerId("home-5"), new PlayerId("home-6")
            };
            var awayIds = new[]
            {
                new PlayerId("away-1"), new PlayerId("away-2"), new PlayerId("away-3"),
                new PlayerId("away-4"), new PlayerId("away-5"), new PlayerId("away-6")
            };
            var positions = Enumerable.Repeat(PlayerPosition.Setter, 6).ToArray();
            var context = MatchV4TestFixture.CreateContextForRotations(
                Guid.Parse("5e19eac4-5d3d-4d52-9c8f-f4dd7680c7bd"),
                31,
                homeIds,
                positions,
                awayIds,
                positions);
            return OnCourtLineupRulesV3.Create(
                context, homeIds, awayIds, homeIds[0], awayIds[0], Array.Empty<LiberoReplacementV3>());
        }
    }
}
