using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Shared.Contracts;

namespace Volleyball.Shared.EditModeTests
{
    public sealed class MatchContractTests
    {
        [Test]
        public void ProductionContractSurface_RequiresConcreteV4ContextAndResult()
        {
            var assembly = typeof(MatchContextV1).Assembly;
            var contextV4 = assembly.GetType("Volleyball.Shared.Contracts.MatchContextV4");
            var resultV4 = assembly.GetType("Volleyball.Shared.Contracts.MatchResultV4");

            Assert.That(contextV4, Is.Not.Null);
            Assert.That(resultV4, Is.Not.Null);
            AssertNoPublicEntryPointAccepts(typeof(ContractJson).Assembly, typeof(IMatchContext), typeof(IMatchResult));
        }

        private static void AssertNoPublicEntryPointAccepts(Assembly assembly, params Type[] prohibited)
        {
            var publicTypes = assembly.GetTypes().Where(type => type.IsPublic).ToArray();
            var exposedTypes = publicTypes
                .SelectMany(type => type.GetConstructors().Cast<MethodBase>()
                    .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)))
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                    .Concat(new[] { method is MethodInfo methodInfo ? methodInfo.ReturnType : null }))
                .Concat(publicTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    .Select(property => property.PropertyType))
                .Where(type => type != null)
                .ToArray();

            foreach (var type in prohibited)
            {
                Assert.That(exposedTypes, Has.None.EqualTo(type), "Production entry points must not expose " + type.Name + ".");
            }
        }

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

        [Test]
        public void LegacyV1Json_DeserializesWithItsOriginalHash()
        {
            var context = ContractJson.DeserializeContext(LegacyMatchContextV1Fixture.Json);

            Assert.That(context.ContractVersion, Is.EqualTo(ContractVersions.MatchV1));
            Assert.That(context.ContextHash, Is.EqualTo(LegacyMatchContextV1Fixture.ContextHash));
        }

        [Test]
        public void MatchContextV2_RoundTripsWithAttackReach()
        {
            var context = CreateContextV2(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

            var restored = ContractJson.DeserializeContextV2(ContractJson.SerializeV2(context));

            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(restored.Home.Players[0].Ability.MaxAttackReach, Is.EqualTo(3.42f));
        }

        [TestCase(PlayerPosition.Setter, 3.20f)]
        [TestCase(PlayerPosition.OutsideHitter, 3.42f)]
        [TestCase(PlayerPosition.MiddleBlocker, 3.48f)]
        [TestCase(PlayerPosition.Opposite, 3.42f)]
        [TestCase(PlayerPosition.Libero, 3.20f)]
        [TestCase(PlayerPosition.Defender, 3.20f)]
        public void MatchContextV2_UpgradeFromV1UsesDeterministicPositionReach(
            PlayerPosition position,
            float expectedReach)
        {
            var legacy = MatchContextV1.Create(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351,
                CreateTeam("team-blue", TeamSide.Home, "blue", position),
                CreateTeam("team-orange", TeamSide.Away, "orange", position));

            var upgraded = MatchContextV2.UpgradeFromV1(legacy);

            Assert.That(upgraded.ContractVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(upgraded.Home.Players[0].Ability.MaxAttackReach, Is.EqualTo(expectedReach));
            Assert.That(upgraded.Away.Players[0].Ability.MaxAttackReach, Is.EqualTo(expectedReach));
        }

        [Test]
        public void PlayerAbilitySnapshotV2_RejectsReachBelowMinimum()
        {
            Assert.That(
                () => new PlayerAbilitySnapshotV2(
                    0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 3.19f),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchContextV2_RejectsReachBelowMinimumAfterDeserialization()
        {
            var json = ContractJson.SerializeV2(CreateContextV2(Guid.NewGuid(), 1));
            var invalid = json.Replace("\"maxAttackReach\":3.42", "\"maxAttackReach\":3.19");

            Assert.That(invalid, Is.Not.EqualTo(json));
            Assert.That(
                () => ContractJson.DeserializeContextV2(invalid),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("[3.20, 3.55]"));
        }

        [Test]
        public void MatchResultV2_RoundTripsWithV2ContextIdentity()
        {
            var context = CreateContextV2(Guid.NewGuid(), 99);
            var result = MatchResultV2.Create(
                context,
                context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV2(context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) });

            var restored = ContractJson.DeserializeResultV2(ContractJson.SerializeV2(result));

            Assert.DoesNotThrow(() => restored.ValidateAgainst(context));
            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
        }

        [Test]
        public void ContractVersions_ReserveV3ContextAndReplayV2()
        {
            Assert.That(ContractVersions.MatchV3, Is.EqualTo(3));
            Assert.That(ContractVersions.ReplayV2, Is.EqualTo(2));
        }

        [Test]
        public void PlayerAbilitySnapshotV3_MigrationIsDeterministicAndRecordsProvenance()
        {
            var source = new PlayerAbilitySnapshotV2(0.7f, 0.6f, 0.8f, 0.5f, 0.9f, 0.75f, 0.85f, 3.42f);

            var first = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);
            var second = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.SourceVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(first.MigrationVersion, Is.EqualTo(PlayerAbilitySnapshotV3.CurrentMigrationVersion));
            Assert.That(first.IsCompatibilityEstimate, Is.True);
            Assert.That(first.CompatibilityCollapsedAxes, Is.Empty);
        }

        [Test]
        public void PlayerAbilitySnapshotV3_MigrationDistinguishesAttackControlAndSoftTouchWhenRoleProxyExists()
        {
            var source = new PlayerAbilitySnapshotV2(0.7f, 0.6f, 0.8f, 0.5f, 0.9f, 0.75f, 0.85f, 3.42f);

            var hitter = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);
            var setter = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.Setter);

            Assert.That(hitter.AttackControl, Is.Not.EqualTo(hitter.SoftTouch));
            Assert.That(setter.AttackControl, Is.Not.EqualTo(setter.SoftTouch));
        }

        [Test]
        public void PlayerAbilitySnapshotV3_MigrationRejectsNullSource()
        {
            Assert.That(
                () => PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(null, PlayerPosition.Setter),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void PlayerAbilitySnapshotV3_MigrationRejectsUndefinedPosition()
        {
            var source = new PlayerAbilitySnapshotV2(0.7f, 0.6f, 0.8f, 0.5f, 0.9f, 0.75f, 0.85f, 3.42f);

            Assert.That(
                () => PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, (PlayerPosition)999),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchContextV3_ConstructsWithStableHashAndAllV3AbilityFields()
        {
            var sessionId = new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4");

            var first = CreateContextV3(sessionId, 7351);
            var second = CreateContextV3(sessionId, 7351);

            Assert.That(first.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(first.ContextHash, Is.EqualTo(second.ContextHash));
            Assert.That(first.ContextHash, Has.Length.EqualTo(64));
            Assert.That(first.ContextHash, Is.EqualTo(CanonicalMatchContextHashV3.Compute(first)));
            Assert.That(first.Home.Players[0].Ability.Mobility, Is.EqualTo(0.71f));
            Assert.That(first.Home.Players[0].Ability.Reaction, Is.EqualTo(0.72f));
            Assert.That(first.Home.Players[0].Ability.Jump, Is.EqualTo(0.73f));
            Assert.That(first.Home.Players[0].Ability.MaxAttackReach, Is.EqualTo(3.42f));
            Assert.That(first.Home.Players[0].Ability.ReceiveTechnique, Is.EqualTo(0.74f));
            Assert.That(first.Home.Players[0].Ability.SetTechnique, Is.EqualTo(0.75f));
            Assert.That(first.Home.Players[0].Ability.AttackControl, Is.EqualTo(0.76f));
            Assert.That(first.Home.Players[0].Ability.AttackPower, Is.EqualTo(0.77f));
            Assert.That(first.Home.Players[0].Ability.SoftTouch, Is.EqualTo(0.78f));
            Assert.That(first.Home.Players[0].Ability.BlockTechnique, Is.EqualTo(0.79f));
            Assert.That(first.Home.Players[0].Ability.CourtAwareness, Is.EqualTo(0.80f));
            Assert.That(first.Home.Players[0].Ability.SourceVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(first.Home.Players[0].Ability.MigrationVersion, Is.Zero);
            Assert.That(first.Home.Players[0].Ability.IsCompatibilityEstimate, Is.False);
            Assert.That(first.Home.Players[0].Ability.CompatibilityCollapsedAxes, Is.EqualTo(new[] { "none" }));
        }

        [Test]
        public void MatchContextV3_HashIncludesAbilityProvenanceAndCompatibilityFields()
        {
            var sessionId = new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4");

            var native = CreateContextV3(sessionId, 7351);
            var compatibilityEstimate = MatchContextV3.Create(
                sessionId,
                7351,
                CreateTeamV3("team-blue", TeamSide.Home, "blue", sourceVersion: ContractVersions.MatchV2, migrationVersion: 1, isCompatibilityEstimate: true),
                CreateTeamV3("team-orange", TeamSide.Away, "orange", sourceVersion: ContractVersions.MatchV2, migrationVersion: 1, isCompatibilityEstimate: true));

            Assert.That(compatibilityEstimate.ContextHash, Is.Not.EqualTo(native.ContextHash));
        }

        [Test]
        public void MatchContextV3_UpgradeFromV2UsesExplicitMigration()
        {
            var legacy = CreateContextV2(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

            var upgraded = MatchContextV3.UpgradeFromV2(legacy);

            Assert.That(upgraded.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(upgraded.ContextHash, Is.EqualTo(CanonicalMatchContextHashV3.Compute(upgraded)));
            Assert.That(upgraded.Home.Players[0].Ability.SourceVersion, Is.EqualTo(ContractVersions.MatchV2));
            Assert.That(upgraded.Home.Players[0].Ability.MigrationVersion, Is.EqualTo(PlayerAbilitySnapshotV3.CurrentMigrationVersion));
            Assert.That(upgraded.Home.Players[0].Ability.IsCompatibilityEstimate, Is.True);
            Assert.That(upgraded.Home.Players[0].Ability.AttackControl, Is.EqualTo(
                PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(
                    legacy.Home.Players[0].Ability,
                    legacy.Home.Players[0].Position).AttackControl));
            Assert.That(upgraded.Home.Players[0].Ability.SoftTouch, Is.EqualTo(
                PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(
                    legacy.Home.Players[0].Ability,
                    legacy.Home.Players[0].Position).SoftTouch));
        }

        [Test]
        public void MatchResultV3_UsesV3ContextIdentityAndValidatesWinner()
        {
            var context = CreateContextV3(Guid.NewGuid(), 99);
            var result = MatchResultV3.Create(
                context,
                context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV3(context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) });

            Assert.DoesNotThrow(() => result.ValidateAgainst(context));
            Assert.That(result.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(result.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(result.ResultHash, Is.EqualTo(CanonicalMatchResultHashV3.Compute(result)));
            Assert.That(
                () => MatchResultV3.Create(
                    context,
                    context.Away.TeamId,
                    homeScore: 25,
                    awayScore: 21,
                    new[] { new PlayerMatchStatsV3(context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) }),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("higher final score"));
        }

        [Test]
        public void MatchContextV3_RoundTripsThroughContractJson()
        {
            var context = CreateContextV3(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

            var restored = ContractJson.DeserializeContextV3(ContractJson.SerializeV3(context));

            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(restored.SessionId, Is.EqualTo(context.SessionId));
            Assert.That(restored.Seed, Is.EqualTo(context.Seed));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(restored.ContextHash, Is.EqualTo(CanonicalMatchContextHashV3.Compute(restored)));
            Assert.That(restored.Home.Players[0].Ability.AttackControl, Is.EqualTo(0.76f));
            Assert.That(restored.Home.Players[0].Ability.SoftTouch, Is.EqualTo(0.78f));
            Assert.That(restored.Home.Players[0].Ability.CompatibilityCollapsedAxes, Is.EqualTo(new[] { "none" }));
        }

        [Test]
        public void MatchResultV3_RoundTripsThroughContractJson()
        {
            var context = CreateContextV3(Guid.NewGuid(), 99);
            var result = MatchResultV3.Create(
                context,
                context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV3(context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) });

            var restored = ContractJson.DeserializeResultV3(ContractJson.SerializeV3(result));

            Assert.DoesNotThrow(() => restored.ValidateAgainst(context));
            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(restored.ResultHash, Is.EqualTo(result.ResultHash));
            Assert.That(restored.ResultHash, Is.EqualTo(CanonicalMatchResultHashV3.Compute(restored)));
            Assert.That(restored.PlayerStats[0].Workload, Is.EqualTo(0.72f));
        }

        [Test]
        public void ContractJson_DoesNotDeserializeV2AsV3OrV3AsV2()
        {
            var v2Context = CreateContextV2(Guid.NewGuid(), 7);
            var v3Context = CreateContextV3(Guid.NewGuid(), 7);
            var v2Json = ContractJson.SerializeV2(v2Context);
            var v3Json = ContractJson.SerializeV3(v3Context);
            var v2ResultJson = ContractJson.SerializeV2(MatchResultV2.Create(
                v2Context,
                v2Context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV2(v2Context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) }));
            var v3ResultJson = ContractJson.SerializeV3(MatchResultV3.Create(
                v3Context,
                v3Context.Home.TeamId,
                homeScore: 25,
                awayScore: 21,
                new[] { new PlayerMatchStatsV3(v3Context.Home.Players[0].PlayerId, 6, 12, 1, 0.72f) }));

            Assert.That(() => ContractJson.DeserializeContextV3(v2Json), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeContextV2(v3Json), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeResultV3(v2ResultJson), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeResultV2(v3ResultJson), Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchReplayV2_RoundTripsWithFormatVersionAndReservedDiagnostics()
        {
            var replay = MatchReplayV2.Create(
                "replay-001",
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                new[] { "PlanCoverageDecision", "ExecutionEnvelopeV3", "BallTrajectoryArtifactV3" });

            var restored = ContractJson.DeserializeReplayV2(ContractJson.SerializeReplayV2(replay));

            Assert.That(restored.FormatVersion, Is.EqualTo(ContractVersions.ReplayV2));
            Assert.That(restored.ReplayId, Is.EqualTo("replay-001"));
            Assert.That(restored.ContextHash, Is.EqualTo(replay.ContextHash));
            Assert.That(restored.ReservedSections, Does.Contain("PlanCoverageDecision"));
            Assert.That(restored.ReservedSections, Does.Contain("ExecutionEnvelopeV3"));
            Assert.That(restored.ReservedSections, Does.Contain("BallTrajectoryArtifactV3"));
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
            string playerPrefix,
            PlayerPosition firstPlayerPosition = PlayerPosition.Setter)
        {
            var players = new List<PlayerSnapshotV1>
            {
                CreatePlayer(playerPrefix + "-setter", "Setter", 1, firstPlayerPosition),
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

        private static MatchContextV2 CreateContextV2(Guid sessionId, int seed)
        {
            return MatchContextV2.Create(
                sessionId,
                seed,
                CreateTeamV2("team-blue", TeamSide.Home, "blue"),
                CreateTeamV2("team-orange", TeamSide.Away, "orange"));
        }

        private static TeamSnapshotV2 CreateTeamV2(string teamId, TeamSide side, string playerPrefix)
        {
            return new TeamSnapshotV2(
                new TeamId(teamId),
                side == TeamSide.Home ? "Blue Team" : "Orange Team",
                side,
                new[]
                {
                    CreatePlayerV2(playerPrefix + "-setter", "Setter", 1, PlayerPosition.Setter, 3.42f),
                    CreatePlayerV2(playerPrefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter, 3.42f),
                    CreatePlayerV2(playerPrefix + "-defender", "Defender", 3, PlayerPosition.Defender, 3.20f)
                });
        }

        private static PlayerSnapshotV2 CreatePlayerV2(
            string playerId,
            string name,
            int jersey,
            PlayerPosition position,
            float maxAttackReach)
        {
            return new PlayerSnapshotV2(
                new PlayerId(playerId),
                name,
                jersey,
                position,
                new PlayerAbilitySnapshotV2(
                    0.81f,
                    0.82f,
                    0.83f,
                    0.84f,
                    0.85f,
                    0.86f,
                    0.87f,
                    maxAttackReach));
        }

        private static MatchContextV3 CreateContextV3(Guid sessionId, int seed)
        {
            return MatchContextV3.Create(
                sessionId,
                seed,
                CreateTeamV3("team-blue", TeamSide.Home, "blue"),
                CreateTeamV3("team-orange", TeamSide.Away, "orange"));
        }

        private static TeamSnapshotV3 CreateTeamV3(
            string teamId,
            TeamSide side,
            string playerPrefix,
            int sourceVersion = ContractVersions.MatchV3,
            int migrationVersion = 0,
            bool isCompatibilityEstimate = false)
        {
            return new TeamSnapshotV3(
                new TeamId(teamId),
                side == TeamSide.Home ? "Blue Team" : "Orange Team",
                side,
                new[]
                {
                    CreatePlayerV3(playerPrefix + "-setter", "Setter", 1, PlayerPosition.Setter, 3.42f, sourceVersion, migrationVersion, isCompatibilityEstimate),
                    CreatePlayerV3(playerPrefix + "-attacker", "Attacker", 2, PlayerPosition.OutsideHitter, 3.42f, sourceVersion, migrationVersion, isCompatibilityEstimate),
                    CreatePlayerV3(playerPrefix + "-defender", "Defender", 3, PlayerPosition.Defender, 3.20f, sourceVersion, migrationVersion, isCompatibilityEstimate)
                });
        }

        private static PlayerSnapshotV3 CreatePlayerV3(
            string playerId,
            string name,
            int jersey,
            PlayerPosition position,
            float maxAttackReach,
            int sourceVersion,
            int migrationVersion,
            bool isCompatibilityEstimate)
        {
            return new PlayerSnapshotV3(
                new PlayerId(playerId),
                name,
                jersey,
                position,
                new PlayerAbilitySnapshotV3(
                    0.71f,
                    0.72f,
                    0.73f,
                    maxAttackReach,
                    0.74f,
                    0.75f,
                    0.76f,
                    0.77f,
                    0.78f,
                    0.79f,
                    0.80f,
                    sourceVersion,
                    migrationVersion,
                    isCompatibilityEstimate,
                    isCompatibilityEstimate ? new[] { "attackTechnique" } : new[] { "none" }));
        }
    }
}
