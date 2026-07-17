using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Shared.Contracts;

namespace Volleyball.Shared.EditModeTests
{
    public sealed class MatchContractTests
    {
        [Test]
        public void MatchContext_RoundTripsWithStableVersionIdentityAndHash()
        {
            var context = CreateContext(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

            var json = ContractJson.Serialize(context);
            var restored = ContractJson.DeserializeContext(json);

            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV1));
            Assert.That(restored.SessionId, Is.EqualTo(context.SessionId));
            Assert.That(restored.Seed, Is.EqualTo(7351));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(restored.Home.TeamId, Is.EqualTo(new TeamId("team-blue")));
            Assert.That(restored.Away.Players, Has.Count.EqualTo(3));
            Assert.That(restored.Home.Players[0].Ability.AttackPower, Is.EqualTo(0.87f));
        }

        [Test]
        public void MatchContext_ProducesSameHashForSamePayload()
        {
            var session = new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4");

            var first = CreateContext(session, 7351);
            var second = CreateContext(session, 7351);

            Assert.That(second.ContextHash, Is.EqualTo(first.ContextHash));
            Assert.That(second.ContextHash, Has.Length.EqualTo(64));
        }

        [Test]
        public void MatchContext_RejectsTamperedPayload()
        {
            var context = CreateContext(Guid.NewGuid(), 7351);
            var json = ContractJson.Serialize(context);
            var tampered = json.Replace("\"seed\":7351", "\"seed\":7352");

            Assert.That(tampered, Is.Not.EqualTo(json));
            Assert.That(
                () => ContractJson.DeserializeContext(tampered),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("contextHash"));
        }

        [Test]
        public void MatchContext_RejectsUnsupportedVersionAndMalformedJson()
        {
            var json = ContractJson.Serialize(CreateContext(Guid.NewGuid(), 12));
            var unsupported = json.Replace("\"contractVersion\":1", "\"contractVersion\":2");

            Assert.That(
                () => ContractJson.DeserializeContext(unsupported),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("Unsupported"));
            Assert.That(
                () => ContractJson.DeserializeContext("{not-json}"),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchContext_RejectsDuplicateStablePlayerIdsAcrossTeams()
        {
            var home = CreateTeam("team-blue", TeamSide.Home, "shared-player");
            var away = CreateTeam("team-orange", TeamSide.Away, "shared-player");

            Assert.That(
                () => MatchContextV1.Create(Guid.NewGuid(), 1, home, away),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("unique across"));
        }

        [Test]
        public void MatchResult_RoundTripsAndMustBelongToItsContext()
        {
            var context = CreateContext(Guid.NewGuid(), 99);
            var stats = new[]
            {
                new PlayerMatchStatsV1(
                    context.Home.Players[0].PlayerId,
                    points: 6,
                    contacts: 12,
                    errors: 1,
                    workload: 0.72f)
            };
            var result = MatchResultV1.Create(
                context,
                context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                stats);

            var restored = ContractJson.DeserializeResult(ContractJson.Serialize(result));

            Assert.DoesNotThrow(() => restored.ValidateAgainst(context));
            Assert.That(restored.WinnerTeamId, Is.EqualTo(context.Home.TeamId));
            Assert.That(restored.PlayerStats[0].Workload, Is.EqualTo(0.72f));
            var otherContext = CreateContext(Guid.NewGuid(), 99);
            Assert.That(
                () => restored.ValidateAgainst(otherContext),
                Throws.TypeOf<ContractValidationException>());
        }

        private static MatchContextV1 CreateContext(Guid sessionId, int seed)
        {
            return MatchContextV1.Create(
                sessionId,
                seed,
                CreateTeam("team-blue", TeamSide.Home, "blue"),
                CreateTeam("team-orange", TeamSide.Away, "orange"));
        }

        private static TeamSnapshotV1 CreateTeam(
            string teamId,
            TeamSide side,
            string playerPrefix)
        {
            var players = new List<PlayerSnapshotV1>
            {
                CreatePlayer(playerPrefix + "-setter", "Setter", 1, PlayerPosition.Setter),
                CreatePlayer(playerPrefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter),
                CreatePlayer(playerPrefix + "-defender", "Defender", 3, PlayerPosition.Defender)
            };
            return new TeamSnapshotV1(
                new TeamId(teamId),
                side == TeamSide.Home ? "Blue Team" : "Orange Team",
                side,
                players);
        }

        private static PlayerSnapshotV1 CreatePlayer(
            string playerId,
            string name,
            int jersey,
            PlayerPosition position)
        {
            return new PlayerSnapshotV1(
                new PlayerId(playerId),
                name,
                jersey,
                position,
                new PlayerAbilitySnapshotV1(
                    0.81f,
                    0.82f,
                    0.83f,
                    0.84f,
                    0.85f,
                    0.86f,
                    0.87f));
        }
    }
}
