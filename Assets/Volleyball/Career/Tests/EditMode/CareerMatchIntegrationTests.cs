using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.MatchIntegration;
using Volleyball.Shared.Contracts;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchIntegrationTests
    {
        private const string FixtureDirectory =
            "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1";
        private const string ContextFileHash =
            "a33aefaef5860e68803fa0d3910638da661e777704d9981e3ffd910719126b93";
        private const string ResultFileHash =
            "301df25404a1358f7a56fdc22008f9f7515b3954e75296c3cf5ffe92a959ad12";
        private const string ContextHash =
            "da570cff972d280acb9307edb715bcef88a0f958e75ea615072a5be25edf0527";
        private const string ResultHash =
            "3fbb03380ce766a7695c7ad7d0697c5c631893d714b229abcf8c7c1017182d98";

        [Test]
        public void Mapper_PreservesEveryLaunchFieldEightAbilityAxesAndFatigueFormula()
        {
            var launch = LaunchWithFatigueEndpoints();
            var context = new CareerMatchV2Mapper().ToContext(launch);

            Assert.That(context.Versions.ContractVersion, Is.EqualTo(2));
            Assert.That(context.Versions.ContentVersion, Is.EqualTo(1));
            Assert.That(context.Versions.RulesetVersion, Is.EqualTo(1));
            Assert.That(context.Versions.CareerRandomAlgorithmVersion, Is.EqualTo(1));
            Assert.That(context.Versions.MatchSimulationVersion, Is.Null);
            Assert.That(context.Versions.MatchRandomAlgorithmVersion, Is.Null);
            Assert.That(context.SessionId, Is.EqualTo(launch.SessionId));
            Assert.That(context.ExecutionMode, Is.EqualTo(MatchExecutionModeV2.Fixture));
            Assert.That(context.FixtureId, Is.EqualTo(CareerMatchTestData.FixtureId));
            Assert.That(context.FixtureVersion, Is.EqualTo(1));
            Assert.That(context.MatchSeed, Is.EqualTo(CareerMatchTestData.MatchSeed));
            Assert.That(context.CompetitionId, Is.EqualTo(CareerMatchTestData.CompetitionId));
            Assert.That(context.ScheduleItemId, Is.EqualTo(CareerMatchTestData.ScheduleItemId));
            Assert.That(context.ImportanceBasisPoints, Is.EqualTo(7500));
            Assert.That(context.Format.Kind, Is.EqualTo("indoor_6v6"));
            Assert.That(context.Format.TeamSize, Is.EqualTo(6));
            Assert.That(context.Format.SetsToWin, Is.EqualTo(1));
            Assert.That(context.Format.SetTargetPoints, Is.EqualTo(25));
            Assert.That(context.Format.MinimumLeadPoints, Is.EqualTo(2));
            Assert.That(context.PreMatchPriority, Is.EqualTo(PreMatchPriorityV2.FirstContactSecurity));
            Assert.That(context.Teams.Select(team => team.Side),
                Is.EqualTo(new[] { TeamSideV2.Home, TeamSideV2.Away }));
            Assert.That(context.Teams.Select(team => team.TeamId.Value),
                Is.EqualTo(new[] { "team.university.first", "team.university.rival" }));
            Assert.That(context.Teams.SelectMany(team => team.Players).Select(player => player.PlayerId.Value),
                Is.EqualTo(new[]
                {
                    "dynamic.home.opposite", "player.career.protagonist", "dynamic.home.middle",
                    "dynamic.home.setter", "dynamic.home.outside.b", "dynamic.home.libero",
                    "dynamic.away.opposite", "dynamic.away.outside.a", "dynamic.away.middle",
                    "dynamic.away.setter", "dynamic.away.outside.b", "dynamic.away.libero"
                }));
            Assert.That(context.Teams.SelectMany(team => team.Players).Select(player => player.JerseyNumber),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 1, 2, 3, 4, 5, 6 }));
            Assert.That(context.Teams.SelectMany(team => team.Players).Select(player => player.Position),
                Is.EqualTo(new[]
                {
                    PlayerPositionV2.Opposite, PlayerPositionV2.OutsideHitter,
                    PlayerPositionV2.MiddleBlocker, PlayerPositionV2.Setter,
                    PlayerPositionV2.OutsideHitter, PlayerPositionV2.Libero,
                    PlayerPositionV2.Opposite, PlayerPositionV2.OutsideHitter,
                    PlayerPositionV2.MiddleBlocker, PlayerPositionV2.Setter,
                    PlayerPositionV2.OutsideHitter, PlayerPositionV2.Libero
                }));
            Assert.That(context.Teams.SelectMany(team => team.Players).Select(player => player.RotationSlot),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 1, 2, 3, 4, 5, 6 }));

            var protagonist = context.Teams[0].Players[1];
            Assert.That(protagonist.PlayerId.Value, Is.EqualTo("player.career.protagonist"));
            Assert.That(protagonist.Position, Is.EqualTo(PlayerPositionV2.OutsideHitter));
            Assert.That(protagonist.Abilities.SpikeBasisPoints, Is.EqualTo(7123));
            Assert.That(protagonist.Abilities.ServeBasisPoints, Is.EqualTo(6234));
            Assert.That(protagonist.Abilities.ReceptionBasisPoints, Is.EqualTo(7345));
            Assert.That(protagonist.Abilities.DefenseBasisPoints, Is.EqualTo(6456));
            Assert.That(protagonist.Abilities.BlockBasisPoints, Is.EqualTo(7567));
            Assert.That(protagonist.Abilities.MovementBasisPoints, Is.EqualTo(6678));
            Assert.That(protagonist.Abilities.JumpBasisPoints, Is.EqualTo(7789));
            Assert.That(protagonist.Abilities.StaminaBasisPoints, Is.EqualTo(6890));
            Assert.That(context.Teams[0].Players[0].FitnessBasisPoints, Is.EqualTo(10000));
            Assert.That(context.Teams[0].Players[3].FitnessBasisPoints, Is.EqualTo(5000));
            Assert.That(context.Teams[0].Players[5].FitnessBasisPoints, Is.EqualTo(0));
        }

        [Test]
        public void Mapper_PreservesUintMaxSeedAndDoesNotLeakGrowthExperience()
        {
            var first = LaunchWithProtagonistGrowth(101, uint.MaxValue);
            var second = LaunchWithProtagonistGrowth(9007199254740991L, uint.MaxValue);
            var mapper = new CareerMatchV2Mapper();
            var firstContext = mapper.ToContext(first);
            var secondContext = mapper.ToContext(second);

            Assert.That(firstContext.MatchSeed, Is.EqualTo(uint.MaxValue));
            Assert.That(MatchContractV2Json.SerializeContext(firstContext),
                Is.EqualTo(MatchContractV2Json.SerializeContext(secondContext)));
            Assert.That(firstContext.ContextHash, Is.EqualTo(secondContext.ContextHash));
        }

        [Test]
        public void Mapper_ExplicitlyMapsDirectAndQuickSimulationModes()
        {
            var mapper = new CareerMatchV2Mapper();
            var direct = mapper.ToContext(CareerMatchTestData.Launch(
                executionMode: CareerMatchExecutionMode.Direct,
                fixtureId: null,
                fixtureVersion: null));
            var quick = mapper.ToContext(CareerMatchTestData.Launch(
                versions: CareerMatchTestData.Versions(
                    matchSimulationVersion: 7,
                    matchRandomAlgorithmVersion: 8),
                executionMode: CareerMatchExecutionMode.QuickSimulation,
                fixtureId: null,
                fixtureVersion: null));

            Assert.That(direct.ExecutionMode, Is.EqualTo(MatchExecutionModeV2.Direct));
            Assert.That(direct.FixtureId, Is.Null);
            Assert.That(direct.Versions.MatchSimulationVersion, Is.Null);
            Assert.That(quick.ExecutionMode, Is.EqualTo(MatchExecutionModeV2.QuickSimulation));
            Assert.That(quick.FixtureId, Is.Null);
            Assert.That(quick.Versions.MatchSimulationVersion, Is.EqualTo(7));
            Assert.That(quick.Versions.MatchRandomAlgorithmVersion, Is.EqualTo(8));
        }

        [TestCase(CareerPreMatchPriority.AttackFirst, PreMatchPriorityV2.AttackFirst)]
        [TestCase(CareerPreMatchPriority.FirstContactSecurity, PreMatchPriorityV2.FirstContactSecurity)]
        [TestCase(CareerPreMatchPriority.StaminaControl, PreMatchPriorityV2.StaminaControl)]
        public void Mapper_ExplicitlyMapsEveryPriorityEnumCase(
            CareerPreMatchPriority careerPriority,
            PreMatchPriorityV2 expected)
        {
            var context = new CareerMatchV2Mapper().ToContext(
                CareerMatchTestData.Launch(priority: careerPriority));

            Assert.That(context.PreMatchPriority, Is.EqualTo(expected));
        }

        [Test]
        public void Mapper_MapsTheCompleteCommittedResultIntoIndependentCareerFacts()
        {
            var pair = FixturePair();
            var context = MatchContractV2Json.DeserializeContext(pair.ContextBytes);
            var result = MatchContractV2Json.DeserializeResult(pair.ResultBytes, context);
            var facts = new CareerMatchV2Mapper().ToCareerFacts(result);

            Assert.That(facts.Versions.ContractVersion, Is.EqualTo(2));
            Assert.That(facts.Versions.ContentVersion, Is.EqualTo(1));
            Assert.That(facts.Versions.RulesetVersion, Is.EqualTo(1));
            Assert.That(facts.Versions.CareerRandomAlgorithmVersion, Is.EqualTo(1));
            Assert.That(facts.Versions.MatchSimulationVersion, Is.Null);
            Assert.That(facts.Versions.MatchRandomAlgorithmVersion, Is.Null);
            Assert.That(facts.SessionId, Is.EqualTo(context.SessionId));
            Assert.That(facts.ContextDigest, Is.EqualTo(new Sha256Digest(ContextHash)));
            Assert.That(facts.ResultDigest, Is.EqualTo(new Sha256Digest(ResultHash)));
            Assert.That(facts.Status, Is.EqualTo(CareerMatchResultStatus.Completed));
            Assert.That(facts.WinnerTeamId, Is.EqualTo(new TeamId("team.university.first")));
            Assert.That(facts.Sets.Single().SetNumber, Is.EqualTo(1));
            Assert.That(facts.Sets.Single().HomePoints, Is.EqualTo(25));
            Assert.That(facts.Sets.Single().AwayPoints, Is.EqualTo(21));
            Assert.That(facts.Sets.Single().IsComplete, Is.True);
            Assert.That(facts.RallyCount, Is.EqualTo(46));
            Assert.That(facts.PlayerFacts.Select(player => player.PlayerId),
                Is.EqualTo(result.PlayerFacts.Select(player => player.PlayerId)));

            for (var index = 0; index < result.PlayerFacts.Count; index++)
            {
                AssertFactEqual(result.PlayerFacts[index], facts.PlayerFacts[index]);
            }
        }

        [Test]
        public void Mapper_PreservesIJsonSafeLongValuesAboveIntMax()
        {
            var context = MatchContractV2Json.DeserializeContext(FixturePair().ContextBytes);
            var sourceFacts = context.Teams.SelectMany(team => team.Players)
                .Select(player => ZeroV2Facts(player.PlayerId))
                .ToArray();
            sourceFacts[0] = new MatchPlayerFactsV2(
                sourceFacts[0].PlayerId,
                sourceFacts[0].Spike,
                sourceFacts[0].Serve,
                sourceFacts[0].Reception,
                sourceFacts[0].Defense,
                sourceFacts[0].Block,
                new MatchLoadFactsV2(46, 3000000000L, 4000000000L, 0, 0, 0, 0),
                sourceFacts[0].Stability);
            var result = MatchResultV2.CreateCompleted(
                context,
                context.Teams[0].TeamId,
                new[] { new SetScoreV2(1, 25, 21, true) },
                46,
                sourceFacts);

            var mapped = new CareerMatchV2Mapper().ToCareerFacts(result);

            Assert.That(mapped.PlayerFacts[0].Load.ActiveDurationMilliseconds, Is.EqualTo(3000000000L));
            Assert.That(mapped.PlayerFacts[0].Load.MovementDistanceMillimeters, Is.EqualTo(4000000000L));
        }

        [Test]
        public void Mapper_MapsAbandonedStatusAndRejectsNullInputs()
        {
            var context = MatchContractV2Json.DeserializeContext(FixturePair().ContextBytes);
            var abandoned = MatchResultV2.CreateAbandoned(
                context,
                Array.Empty<SetScoreV2>(),
                0,
                context.Teams.SelectMany(team => team.Players)
                    .Select(player => ZeroV2Facts(player.PlayerId)).ToArray());
            var mapper = new CareerMatchV2Mapper();

            var mapped = mapper.ToCareerFacts(abandoned);

            Assert.That(mapped.Status, Is.EqualTo(CareerMatchResultStatus.Abandoned));
            Assert.That(mapped.WinnerTeamId, Is.Null);
            Assert.That(mapped.Sets, Is.Empty);
            Assert.That(mapped.RallyCount, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => mapper.ToContext(null));
            Assert.Throws<ArgumentNullException>(() => mapper.ToCareerFacts(null));
        }

        [Test]
        public void Mapper_MapsAwayWinnerAndAllQuickSimulationResultVersionsWithoutHardcoding()
        {
            var template = MatchContractV2Json.DeserializeContext(FixturePair().ContextBytes);
            var quickContext = MatchContextV2.Create(
                new MatchVersionSetV2(2, 7, 8, 9, 10, 11),
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                MatchExecutionModeV2.QuickSimulation,
                null,
                null,
                uint.MaxValue,
                "competition.quick.v7",
                "schedule.quick.11",
                6789,
                template.Format,
                PreMatchPriorityV2.StaminaControl,
                template.Teams);
            var result = MatchResultV2.CreateCompleted(
                quickContext,
                quickContext.Teams[1].TeamId,
                new[] { new SetScoreV2(1, 21, 25, true) },
                46,
                quickContext.Teams.SelectMany(team => team.Players)
                    .Select(player => ZeroV2Facts(player.PlayerId)).ToArray());

            var mapped = new CareerMatchV2Mapper().ToCareerFacts(result);

            Assert.That(mapped.Status, Is.EqualTo(CareerMatchResultStatus.Completed));
            Assert.That(mapped.WinnerTeamId, Is.EqualTo(quickContext.Teams[1].TeamId));
            Assert.That(mapped.Versions.ContractVersion, Is.EqualTo(2));
            Assert.That(mapped.Versions.ContentVersion, Is.EqualTo(7));
            Assert.That(mapped.Versions.RulesetVersion, Is.EqualTo(8));
            Assert.That(mapped.Versions.CareerRandomAlgorithmVersion, Is.EqualTo(9));
            Assert.That(mapped.Versions.MatchSimulationVersion, Is.EqualTo(10));
            Assert.That(mapped.Versions.MatchRandomAlgorithmVersion, Is.EqualTo(11));
            Assert.That(mapped.Sets.Single().HomePoints, Is.EqualTo(21));
            Assert.That(mapped.Sets.Single().AwayPoints, Is.EqualTo(25));
        }

        [Test]
        public async Task FixtureRunner_RebindsFactsBySideAndRotationAndWinnerByTemplateSide()
        {
            var pair = FixturePair();
            var runner = new FixtureMatchRunnerV2(
                new VersionedMatchFixtureRepository(pair.ContextBytes, pair.ResultBytes));
            var context = DynamicContext(
                "team.dynamic.home",
                "team.dynamic.away",
                "runner.home",
                "runner.away",
                Guid.Parse("88888888-8888-8888-8888-888888888888"));

            var result = await runner.ExecuteAsync(context, CancellationToken.None);

            Assert.DoesNotThrow(() => result.ValidateAgainst(context));
            Assert.That(result.SessionId, Is.EqualTo(context.SessionId));
            Assert.That(result.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(result.WinnerTeamId, Is.EqualTo(new TeamId("team.dynamic.home")));
            Assert.That(result.WinnerTeamId, Is.Not.EqualTo(new TeamId("team.university.first")));
            Assert.That(result.PlayerFacts.Select(fact => fact.PlayerId),
                Is.EqualTo(context.Teams.SelectMany(team => team.Players).Select(player => player.PlayerId)));
            Assert.That(result.PlayerFacts[1].PlayerId.Value, Is.EqualTo("runner.home.outside.a"));
            Assert.That(result.PlayerFacts[1].Spike.Attempts, Is.EqualTo(12));
            Assert.That(result.PlayerFacts[1].Reception.Perfect, Is.EqualTo(3));
            Assert.That(result.PlayerFacts[7].PlayerId.Value, Is.EqualTo("runner.away.outside.a"));
            Assert.That(result.PlayerFacts[7].Spike.Attempts, Is.EqualTo(9));
            Assert.That(result.PlayerFacts[7].Reception.Perfect, Is.EqualTo(2));
            Assert.That(result.ResultHash, Is.Not.EqualTo(ResultHash));
            Assert.That(context.Teams.SelectMany(team => team.Players).Select(player => player.JerseyNumber),
                Is.EqualTo(new[] { 11, 12, 13, 14, 15, 16, 11, 12, 13, 14, 15, 16 }));
            Assert.That(context.Teams[0].Players[0].Abilities.SpikeBasisPoints, Is.EqualTo(6400));
            Assert.That(context.Teams[1].Players[0].Abilities.SpikeBasisPoints, Is.EqualTo(6500));
            Assert.That(context.Teams[0].Players[1].FitnessBasisPoints, Is.EqualTo(8900));
            Assert.That(context.PreMatchPriority, Is.EqualTo(PreMatchPriorityV2.StaminaControl));
            Assert.That(context.ImportanceBasisPoints, Is.EqualTo(4321));
        }

        [Test]
        public async Task FixtureRepository_UsesExactCanonicalBytesClonesInputsAndRejectsBadPairs()
        {
            var pair = FixturePair();
            Assert.That(Hash(pair.ContextBytes), Is.EqualTo(ContextFileHash));
            Assert.That(Hash(pair.ResultBytes), Is.EqualTo(ResultFileHash));
            var contextSource = (byte[])pair.ContextBytes.Clone();
            var resultSource = (byte[])pair.ResultBytes.Clone();
            var repository = new VersionedMatchFixtureRepository(contextSource, resultSource);
            contextSource[0] ^= 0x01;
            resultSource[0] ^= 0x01;
            var runner = new FixtureMatchRunnerV2(repository);
            var canonical = MatchContractV2Json.DeserializeContext(pair.ContextBytes);

            var canonicalResult = await runner.ExecuteAsync(canonical, CancellationToken.None);
            Assert.That(MatchContractV2Json.SerializeResult(canonicalResult), Is.EqualTo(pair.ResultBytes));
            Assert.That(canonicalResult.ResultHash, Is.EqualTo(ResultHash));
            Assert.Throws<MatchV2ContractException>(() =>
                new VersionedMatchFixtureRepository(new byte[] { 1, 2, 3 }, pair.ResultBytes));
            Assert.Throws<MatchV2ContractException>(() =>
                new VersionedMatchFixtureRepository(pair.ContextBytes, pair.ContextBytes));

            var dynamicContext = DynamicContext(
                "team.cross.home", "team.cross.away", "cross.home", "cross.away",
                Guid.Parse("99999999-9999-9999-9999-999999999999"));
            Assert.Throws<MatchV2ContractException>(() =>
                new VersionedMatchFixtureRepository(
                    MatchContractV2Json.SerializeContext(dynamicContext),
                    pair.ResultBytes));
        }

        [Test]
        public async Task FixtureRepository_RejectsEverySelfConsistentPairOtherThanCommittedAuthority()
        {
            var pair = FixturePair();
            var context = MatchContractV2Json.DeserializeContext(pair.ContextBytes);
            var original = MatchContractV2Json.DeserializeResult(pair.ResultBytes, context);
            var changedFacts = original.PlayerFacts.ToArray();
            var first = changedFacts[0];
            changedFacts[0] = new MatchPlayerFactsV2(
                first.PlayerId,
                new SpikeFactsV2(first.Spike.Attempts + 1, first.Spike.Points, first.Spike.Errors),
                first.Serve,
                first.Reception,
                first.Defense,
                first.Block,
                first.Load,
                first.Stability);
            var changedResult = MatchResultV2.CreateCompleted(
                context,
                original.WinnerTeamId.Value,
                original.Sets,
                original.RallyCount,
                changedFacts);
            var changedResultBytes = MatchContractV2Json.SerializeResult(changedResult);
            Assert.That(changedResult.ResultHash, Is.Not.EqualTo(ResultHash));
            Assert.Throws<MatchV2ContractException>(() =>
                new VersionedMatchFixtureRepository(pair.ContextBytes, changedResultBytes));

            var dynamicContext = DynamicContext(
                "team.authority.home", "team.authority.away", "authority.home", "authority.away",
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));
            var dynamicResult = await Runner().ExecuteAsync(dynamicContext, CancellationToken.None);
            Assert.Throws<MatchV2ContractException>(() =>
                new VersionedMatchFixtureRepository(
                    MatchContractV2Json.SerializeContext(dynamicContext),
                    MatchContractV2Json.SerializeResult(dynamicResult)));
        }

        [Test]
        public void FixtureRunner_RejectsWrongFixtureIdentityScheduleFormatAndPositionTopology()
        {
            var runner = Runner();
            AssertRunnerRejected(runner, V2Context(fixtureId: "fixture.unsupported"));
            AssertRunnerRejected(runner, V2Context(fixtureVersion: 2));
            AssertRunnerRejected(runner, V2Context(competitionId: "competition.other"));
            AssertRunnerRejected(runner, V2Context(scheduleItemId: "schedule.other"));
            AssertRunnerRejected(runner, V2Context(
                format: new MatchFormatV2("indoor_6v6", 6, 1, 15, 2)));
            AssertRunnerRejected(runner, V2Context(teams: SwappedPositionTeams()));
        }

        [Test]
        public async Task FixtureRunner_AcceptsEveryUintSeedWithStableFactsAndDistinctSemanticHashes()
        {
            var realDerivedSeed = checked((uint)new CareerDeterministicRandom().NextInt64(
                new CareerRandomRequest(
                    1,
                    CareerSeed.Parse(
                        "000102030405060708090a0b0c0d0e0f" +
                        "101112131415161718191a1b1c1d1e1f"),
                    "match_seed",
                    2,
                    3,
                    "schedule.u2w3.match.07",
                    new OccurrenceId(
                        Guid.Parse("00000000-0000-0000-0000-000000000005")),
                    0),
                0,
                4294967296L));
            var seeds = new[]
            {
                0u,
                CareerMatchTestData.MatchSeed,
                uint.MaxValue,
                realDerivedSeed
            };
            Assert.That(seeds.Distinct().Count(), Is.EqualTo(4),
                "The real domain-derived sample must exercise a fourth seed.");

            var runner = Runner();
            var contexts = seeds.Select(seed => V2Context(matchSeed: seed)).ToArray();
            var results = new MatchResultV2[contexts.Length];
            for (var index = 0; index < contexts.Length; index++)
            {
                results[index] = await runner.ExecuteAsync(contexts[index], CancellationToken.None);
            }

            Assert.That(contexts.Select(context => context.ContextHash).Distinct().Count(),
                Is.EqualTo(seeds.Length));
            Assert.That(results.Select(result => result.ResultHash).Distinct().Count(),
                Is.EqualTo(seeds.Length));
            for (var index = 1; index < results.Length; index++)
            {
                AssertFixturePayloadEqual(results[0], results[index]);
            }
        }

        [Test]
        public void FixtureRunner_LocksEveryVersionAxisThroughContractAndTemplateValidation()
        {
            var runner = Runner();
            Assert.Throws<MatchV2ContractException>(() => new MatchVersionSetV2(3, 1, 1, 1, null, null));
            AssertRunnerRejected(runner, V2Context(versions: new MatchVersionSetV2(2, 2, 1, 1, null, null)));
            AssertRunnerRejected(runner, V2Context(versions: new MatchVersionSetV2(2, 1, 2, 1, null, null)));
            AssertRunnerRejected(runner, V2Context(versions: new MatchVersionSetV2(2, 1, 1, 2, null, null)));
            Assert.Throws<MatchV2ContractException>(() => V2Context(
                versions: new MatchVersionSetV2(2, 1, 1, 1, 1, null)));
            Assert.Throws<MatchV2ContractException>(() => V2Context(
                versions: new MatchVersionSetV2(2, 1, 1, 1, null, 1)));
        }

        [Test]
        public void SharedAndCareerGuards_RejectInvalidSideAndRotationBeforeRunner()
        {
            var teams = MatchContractV2Json.DeserializeContext(FixturePair().ContextBytes).Teams;
            Assert.Throws<MatchV2ContractException>(() => MatchContextV2.Create(
                new MatchVersionSetV2(2, 1, 1, 1, null, null),
                CareerMatchTestData.SessionId,
                MatchExecutionModeV2.Fixture,
                CareerMatchTestData.FixtureId,
                1,
                CareerMatchTestData.MatchSeed,
                CareerMatchTestData.CompetitionId,
                CareerMatchTestData.ScheduleItemId,
                7500,
                new MatchFormatV2("indoor_6v6", 6, 1, 25, 2),
                PreMatchPriorityV2.AttackFirst,
                new[] { teams[1], teams[0] }));

            var players = CareerMatchTestData.Players("bad.rotation", false);
            players[1] = CareerMatchTestData.Player(
                "bad.rotation.outside", 2, CareerMatchPlayerPosition.OutsideHitter, 1, 0, 1000);
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Team(
                new TeamId("team.bad.rotation"),
                CareerMatchTeamSide.Home,
                "ignored",
                false,
                players));
        }

        [Test]
        public void FixtureRunner_PreCanceledCallReturnsCanceledTaskWithoutSynchronousThrow()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            Task<MatchResultV2> task = null;

            Assert.DoesNotThrow(() => task = Runner().ExecuteAsync(
                MatchContractV2Json.DeserializeContext(FixturePair().ContextBytes),
                source.Token));
            Assert.That(task, Is.Not.Null);
            Assert.That(task.IsCanceled, Is.True);
            Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
        }

        [Test]
        public void FixtureRunner_NullContextReturnsAFaultedTaskWithoutSynchronousThrow()
        {
            Task<MatchResultV2> task = null;

            Assert.DoesNotThrow(() => task = Runner().ExecuteAsync(null, CancellationToken.None));
            Assert.That(task, Is.Not.Null);
            Assert.That(task.IsFaulted, Is.True);
            Assert.ThrowsAsync<ArgumentNullException>(async () => await task);
        }

        [Test]
        public async Task FixtureRunner_IsByteDeterministicAcrossSequentialAndConcurrentCalls()
        {
            var runner = Runner();
            var firstContext = DynamicContext(
                "team.concurrent.home.1", "team.concurrent.away.1", "concurrent.one.home",
                "concurrent.one.away", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            var secondContext = DynamicContext(
                "team.concurrent.home.2", "team.concurrent.away.2", "concurrent.two.home",
                "concurrent.two.away", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            var first = await runner.ExecuteAsync(firstContext, CancellationToken.None);
            var retry = await runner.ExecuteAsync(firstContext, CancellationToken.None);
            var concurrent = await Task.WhenAll(
                runner.ExecuteAsync(firstContext, CancellationToken.None),
                runner.ExecuteAsync(firstContext, CancellationToken.None),
                runner.ExecuteAsync(secondContext, CancellationToken.None));

            var firstBytes = MatchContractV2Json.SerializeResult(first);
            Assert.That(MatchContractV2Json.SerializeResult(retry), Is.EqualTo(firstBytes));
            Assert.That(MatchContractV2Json.SerializeResult(concurrent[0]), Is.EqualTo(firstBytes));
            Assert.That(MatchContractV2Json.SerializeResult(concurrent[1]), Is.EqualTo(firstBytes));
            Assert.That(concurrent[0].ResultHash, Is.EqualTo(first.ResultHash));
            Assert.That(concurrent[2].ResultHash, Is.Not.EqualTo(first.ResultHash));
            Assert.DoesNotThrow(() => concurrent[2].ValidateAgainst(secondContext));
        }

        [Test]
        public async Task Executor_ExecutesTheExactPersistedContextAndReturnsCanonicalEvidence()
        {
            var actualRunner = Runner();
            var recordingRunner = new DelegateRunner(async (context, token) =>
            {
                Assert.That(token.CanBeCanceled, Is.True);
                return await actualRunner.ExecuteAsync(context, token);
            });
            var executor = new CareerMatchExecutorV2(recordingRunner);
            using var source = new CancellationTokenSource();
            var canonicalContext = executor.Encode(CareerMatchTestData.Launch());
            var persistedContextBytes = canonicalContext.CanonicalContextUtf8;

            var outcome = await executor.ExecuteAsync(canonicalContext, source.Token);
            var decodedResult = MatchContractV2Json.DeserializeResult(
                outcome.CanonicalResultUtf8,
                recordingRunner.ReceivedContext);

            Assert.That(recordingRunner.ReceivedToken, Is.EqualTo(source.Token));
            Assert.That(recordingRunner.ReceivedContext, Is.Not.Null);
            Assert.That(MatchContractV2Json.SerializeContext(recordingRunner.ReceivedContext),
                Is.EqualTo(persistedContextBytes),
                "The runner must receive the DTO decoded from the exact persisted bytes.");
            Assert.That(canonicalContext.ContextDigest.Value,
                Is.EqualTo(recordingRunner.ReceivedContext.ContextHash));
            Assert.That(canonicalContext.ContextDigest.Value, Is.Not.EqualTo(Hash(persistedContextBytes)),
                "The envelope owns the embedded semantic V2 hash, not a full-file SHA-256.");
            Assert.That(outcome.Context, Is.SameAs(canonicalContext));
            Assert.That(outcome.ResultDigest.Value, Is.EqualTo(decodedResult.ResultHash));
            Assert.That(outcome.ResultDigest.Value, Is.Not.EqualTo(Hash(outcome.CanonicalResultUtf8)),
                "The outcome owns the embedded semantic V2 hash, not a full-file SHA-256.");
            Assert.That(outcome.Facts.ContextDigest, Is.EqualTo(canonicalContext.ContextDigest));
            Assert.That(outcome.Facts.ResultDigest, Is.EqualTo(outcome.ResultDigest));
            Assert.That(outcome.Facts.WinnerTeamId, Is.EqualTo(new TeamId("team.university.first")));
            Assert.That(outcome.Facts.PlayerFacts, Has.Count.EqualTo(12));
        }

        [Test]
        public void Executor_RejectsTamperedEnvelopeHashSessionAndNonCanonicalBytesBeforeRunner()
        {
            var runner = new DelegateRunner((_, __) =>
                Task.FromException<MatchResultV2>(
                    new AssertionException("A rejected envelope must not reach the runner.")));
            var executor = new CareerMatchExecutorV2(runner);
            var canonical = executor.Encode(CareerMatchTestData.Launch());
            var bytes = canonical.CanonicalContextUtf8;
            var tamperedBytes = new CareerMatchV2Mapper().ToContext(
                CareerMatchTestData.Launch(matchSeed: 12u));
            var nonCanonicalBytes = bytes.Concat(new byte[] { 0x20 }).ToArray();

            Assert.ThrowsAsync<MatchV2ContractException>(async () =>
                await executor.ExecuteAsync(
                    new CareerCanonicalMatchContext(
                        canonical.SessionId,
                        canonical.ContextDigest,
                        MatchContractV2Json.SerializeContext(tamperedBytes)),
                    CancellationToken.None));
            Assert.ThrowsAsync<MatchV2ContractException>(async () =>
                await executor.ExecuteAsync(
                    new CareerCanonicalMatchContext(
                        canonical.SessionId,
                        new Sha256Digest(new string('c', 64)),
                        bytes),
                    CancellationToken.None));
            Assert.ThrowsAsync<MatchV2ContractException>(async () =>
                await executor.ExecuteAsync(
                    new CareerCanonicalMatchContext(
                        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                        canonical.ContextDigest,
                        bytes),
                    CancellationToken.None));
            Assert.ThrowsAsync<MatchV2ContractException>(async () =>
                await executor.ExecuteAsync(
                    new CareerCanonicalMatchContext(
                        canonical.SessionId,
                        canonical.ContextDigest,
                        nonCanonicalBytes),
                    CancellationToken.None));
            Assert.That(runner.CallCount, Is.Zero);
        }

        [Test]
        public async Task Executor_DecodesValidEvidenceAndRejectsNonCanonicalOrWrongPairs()
        {
            var executor = new CareerMatchExecutorV2(Runner());
            var firstContext = executor.Encode(CareerMatchTestData.Launch());
            var firstOutcome = await executor.ExecuteAsync(firstContext, CancellationToken.None);

            var decoded = executor.DecodeAndValidate(
                firstContext.CanonicalContextUtf8,
                firstOutcome.CanonicalResultUtf8);

            Assert.That(decoded.Context.SessionId, Is.EqualTo(firstContext.SessionId));
            Assert.That(decoded.Context.ContextDigest, Is.EqualTo(firstContext.ContextDigest));
            Assert.That(decoded.ResultDigest, Is.EqualTo(firstOutcome.ResultDigest));
            Assert.That(decoded.CanonicalResultUtf8, Is.EqualTo(firstOutcome.CanonicalResultUtf8));

            var nonCanonicalContext =
                firstContext.CanonicalContextUtf8.Concat(new byte[] { 0x20 }).ToArray();
            var nonCanonicalResult =
                firstOutcome.CanonicalResultUtf8.Concat(new byte[] { 0x20 }).ToArray();
            Assert.Throws<MatchV2ContractException>(() =>
                executor.DecodeAndValidate(nonCanonicalContext, firstOutcome.CanonicalResultUtf8));
            Assert.Throws<MatchV2ContractException>(() =>
                executor.DecodeAndValidate(firstContext.CanonicalContextUtf8, nonCanonicalResult));

            var secondContext = executor.Encode(CareerMatchTestData.Launch(
                sessionId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));
            var secondOutcome =
                await executor.ExecuteAsync(secondContext, CancellationToken.None);
            Assert.Throws<MatchV2ContractException>(() =>
                executor.DecodeAndValidate(
                    firstContext.CanonicalContextUtf8,
                    secondOutcome.CanonicalResultUtf8));
        }

        [Test]
        public void Executor_PropagatesCancellationOrdinaryErrorsNullAndWrongOwnerResults()
        {
            var launch = CareerMatchTestData.Launch();
            var persisted = new CareerMatchExecutorV2(Runner()).Encode(launch);
            using var source = new CancellationTokenSource();
            source.Cancel();
            var canceled = new CareerMatchExecutorV2(new DelegateRunner(
                (_, token) => Task.FromCanceled<MatchResultV2>(token)));
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await canceled.ExecuteAsync(persisted, source.Token));

            var expected = new InvalidOperationException("runner failed");
            var failed = new CareerMatchExecutorV2(new DelegateRunner(
                (_, __) => Task.FromException<MatchResultV2>(expected)));
            var thrown = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await failed.ExecuteAsync(persisted, CancellationToken.None));
            Assert.That(thrown, Is.SameAs(expected));

            var nullRunner = new CareerMatchExecutorV2(new DelegateRunner(
                (_, __) => Task.FromResult<MatchResultV2>(null)));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await nullRunner.ExecuteAsync(persisted, CancellationToken.None));

            var otherContext = DynamicContext(
                "team.other.home", "team.other.away", "other.home", "other.away",
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
            var otherResult = Runner().ExecuteAsync(otherContext, CancellationToken.None).Result;
            var wrongOwner = new CareerMatchExecutorV2(new DelegateRunner(
                (_, __) => Task.FromResult(otherResult)));
            Assert.ThrowsAsync<MatchV2ContractException>(async () =>
                await wrongOwner.ExecuteAsync(persisted, CancellationToken.None));
        }

        [Test]
        public void Executor_PropagatesRunnerCancellationWhenCallerTokenWasNotPreCanceled()
        {
            using var callerSource = new CancellationTokenSource();
            using var runnerSource = new CancellationTokenSource();
            runnerSource.Cancel();
            var executor = new CareerMatchExecutorV2(new DelegateRunner(
                (_, __) => Task.FromCanceled<MatchResultV2>(runnerSource.Token)));
            var persisted = executor.Encode(CareerMatchTestData.Launch());

            var exception = Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await executor.ExecuteAsync(persisted, callerSource.Token));

            Assert.That(callerSource.IsCancellationRequested, Is.False);
            Assert.That(exception.CancellationToken, Is.EqualTo(runnerSource.Token));
        }

        [Test]
        public void Executor_ObservesCallerCancellationWhenTheRunnerIgnoresItAndReturnsAResult()
        {
            using var callerSource = new CancellationTokenSource();
            var actualRunner = Runner();
            var ignoringRunner = new DelegateRunner(async (context, _) =>
            {
                callerSource.Cancel();
                return await actualRunner.ExecuteAsync(context, CancellationToken.None);
            });
            var executor = new CareerMatchExecutorV2(ignoringRunner);
            var persisted = executor.Encode(CareerMatchTestData.Launch());

            var exception = Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await executor.ExecuteAsync(persisted, callerSource.Token));

            Assert.That(exception.CancellationToken, Is.EqualTo(callerSource.Token));
            Assert.That(ignoringRunner.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Executor_RejectsNullsAndReturnsCanceledOrNullContextTasks()
        {
            Assert.Throws<ArgumentNullException>(() => new CareerMatchExecutorV2(null));
            var executor = new CareerMatchExecutorV2(Runner());
            Assert.Throws<ArgumentNullException>(() => executor.Encode(null));
            Assert.Throws<ArgumentNullException>(() =>
                executor.DecodeAndValidate(null, new byte[] { 1 }));
            Assert.Throws<ArgumentNullException>(() =>
                executor.DecodeAndValidate(new byte[] { 1 }, null));

            Task<CareerMatchExecutionOutcome> nullContextTask = null;
            Assert.DoesNotThrow(() =>
                nullContextTask = executor.ExecuteAsync(null, CancellationToken.None));
            Assert.ThrowsAsync<ArgumentNullException>(async () => await nullContextTask);

            using var source = new CancellationTokenSource();
            source.Cancel();
            Task<CareerMatchExecutionOutcome> canceledTask = null;
            var persisted = executor.Encode(CareerMatchTestData.Launch());
            Assert.DoesNotThrow(() => canceledTask = executor.ExecuteAsync(
                persisted, source.Token));
            Assert.That(canceledTask.IsCanceled, Is.True);
            Assert.ThrowsAsync<TaskCanceledException>(async () => await canceledTask);
        }

        private static CareerMatchLaunch LaunchWithFatigueEndpoints()
        {
            var teams = CareerMatchTestData.Teams();
            var home = teams[0].Players.ToArray();
            home[0] = CareerMatchTestData.Player(
                home[0].PlayerId.Value, 1, CareerMatchPlayerPosition.Opposite, 1, 0, 6100);
            teams[0] = CareerMatchTestData.Team(
                teams[0].TeamId, CareerMatchTeamSide.Home, "ignored", true, home);
            return CareerMatchTestData.Launch(
                priority: CareerPreMatchPriority.FirstContactSecurity,
                teams: teams);
        }

        private static CareerMatchLaunch LaunchWithProtagonistGrowth(long growth, uint seed)
        {
            var teams = CareerMatchTestData.Teams();
            var home = teams[0].Players.ToArray();
            var source = home[1];
            home[1] = new CareerMatchPlayerLaunch(
                source.PlayerId,
                source.JerseyNumber,
                source.Position,
                source.RotationSlot,
                source.Fatigue,
                AttributesWithGrowth(source.Attributes, growth));
            teams[0] = CareerMatchTestData.Team(
                teams[0].TeamId, CareerMatchTeamSide.Home, "ignored", true, home);
            return CareerMatchTestData.Launch(matchSeed: seed, teams: teams);
        }

        private static CareerPlayerAttributes AttributesWithGrowth(
            CareerPlayerAttributes source,
            long growth)
        {
            return new CareerPlayerAttributes(
                new CareerAttributeProgress(source.Spike.AbilityBasisPoints, growth),
                new CareerAttributeProgress(source.Serve.AbilityBasisPoints, growth),
                new CareerAttributeProgress(source.Reception.AbilityBasisPoints, growth),
                new CareerAttributeProgress(source.Defense.AbilityBasisPoints, growth),
                new CareerAttributeProgress(source.Block.AbilityBasisPoints, growth),
                new CareerAttributeProgress(source.Movement.AbilityBasisPoints, growth),
                new CareerAttributeProgress(source.Jump.AbilityBasisPoints, growth),
                new CareerAttributeProgress(source.Stamina.AbilityBasisPoints, growth));
        }

        private static MatchContextV2 DynamicContext(
            string homeTeamId,
            string awayTeamId,
            string homePrefix,
            string awayPrefix,
            Guid sessionId)
        {
            var careerTeams = new[]
            {
                DynamicTeam(new TeamId(homeTeamId), CareerMatchTeamSide.Home, homePrefix),
                DynamicTeam(new TeamId(awayTeamId), CareerMatchTeamSide.Away, awayPrefix)
            };
            return new CareerMatchV2Mapper().ToContext(CareerMatchTestData.Launch(
                sessionId: sessionId,
                importanceBasisPoints: 4321,
                priority: CareerPreMatchPriority.StaminaControl,
                teams: careerTeams));
        }

        private static CareerMatchTeamLaunch DynamicTeam(
            TeamId teamId,
            CareerMatchTeamSide side,
            string prefix)
        {
            var players = CareerMatchTestData.Players(prefix, false);
            for (var index = 0; index < players.Length; index++)
            {
                var player = players[index];
                players[index] = new CareerMatchPlayerLaunch(
                    player.PlayerId,
                    player.JerseyNumber + 10,
                    player.Position,
                    player.RotationSlot,
                    player.Fatigue,
                    ShiftAttributes(player.Attributes, side == CareerMatchTeamSide.Home ? 300 : 400));
            }

            return CareerMatchTestData.Team(teamId, side, "ignored", false, players);
        }

        private static CareerPlayerAttributes ShiftAttributes(
            CareerPlayerAttributes source,
            int amount)
        {
            return new CareerPlayerAttributes(
                new CareerAttributeProgress(source.Spike.AbilityBasisPoints + amount, 111),
                new CareerAttributeProgress(source.Serve.AbilityBasisPoints + amount, 222),
                new CareerAttributeProgress(source.Reception.AbilityBasisPoints + amount, 333),
                new CareerAttributeProgress(source.Defense.AbilityBasisPoints + amount, 444),
                new CareerAttributeProgress(source.Block.AbilityBasisPoints + amount, 555),
                new CareerAttributeProgress(source.Movement.AbilityBasisPoints + amount, 666),
                new CareerAttributeProgress(source.Jump.AbilityBasisPoints + amount, 777),
                new CareerAttributeProgress(source.Stamina.AbilityBasisPoints + amount, 888));
        }

        private static MatchContextV2 V2Context(
            MatchVersionSetV2 versions = null,
            string fixtureId = CareerMatchTestData.FixtureId,
            int? fixtureVersion = 1,
            uint matchSeed = CareerMatchTestData.MatchSeed,
            string competitionId = CareerMatchTestData.CompetitionId,
            string scheduleItemId = CareerMatchTestData.ScheduleItemId,
            MatchFormatV2 format = null,
            IReadOnlyList<MatchTeamSnapshotV2> teams = null)
        {
            var template = MatchContractV2Json.DeserializeContext(FixturePair().ContextBytes);
            return MatchContextV2.Create(
                versions ?? template.Versions,
                template.SessionId,
                MatchExecutionModeV2.Fixture,
                fixtureId,
                fixtureVersion,
                matchSeed,
                competitionId,
                scheduleItemId,
                template.ImportanceBasisPoints,
                format ?? template.Format,
                template.PreMatchPriority,
                teams ?? template.Teams);
        }

        private static MatchTeamSnapshotV2[] SwappedPositionTeams()
        {
            var template = MatchContractV2Json.DeserializeContext(FixturePair().ContextBytes);
            var homePlayers = template.Teams[0].Players.ToArray();
            homePlayers[0] = CopyPlayer(homePlayers[0], PlayerPositionV2.OutsideHitter);
            homePlayers[1] = CopyPlayer(homePlayers[1], PlayerPositionV2.Opposite);
            return new[]
            {
                new MatchTeamSnapshotV2(template.Teams[0].TeamId, TeamSideV2.Home, homePlayers),
                template.Teams[1]
            };
        }

        private static MatchPlayerSnapshotV2 CopyPlayer(
            MatchPlayerSnapshotV2 source,
            PlayerPositionV2 position)
        {
            return new MatchPlayerSnapshotV2(
                source.PlayerId,
                source.JerseyNumber,
                position,
                source.RotationSlot,
                source.FitnessBasisPoints,
                source.Abilities);
        }

        private static MatchPlayerFactsV2 ZeroV2Facts(PlayerId playerId)
        {
            return new MatchPlayerFactsV2(
                playerId,
                new SpikeFactsV2(0, 0, 0),
                new ServeFactsV2(0, 0, 0),
                new ReceptionFactsV2(0, 0, 0, 0, 0, 0),
                new DefenseFactsV2(0, 0),
                new BlockFactsV2(0, 0, 0),
                new MatchLoadFactsV2(0, 0, 0, 0, 0, 0, 0),
                new StabilityFactsV2(0, 0, 0, 0, 0));
        }

        private static void AssertFactEqual(MatchPlayerFactsV2 expected, CareerMatchPlayerFacts actual)
        {
            Assert.That(actual.PlayerId, Is.EqualTo(expected.PlayerId));
            Assert.That(new[] { actual.Spike.Attempts, actual.Spike.Points, actual.Spike.Errors },
                Is.EqualTo(new[] { expected.Spike.Attempts, expected.Spike.Points, expected.Spike.Errors }));
            Assert.That(new[] { actual.Serve.Attempts, actual.Serve.Aces, actual.Serve.Errors },
                Is.EqualTo(new[] { expected.Serve.Attempts, expected.Serve.Aces, expected.Serve.Errors }));
            Assert.That(new[]
                {
                    actual.Reception.Attempts, actual.Reception.Perfect, actual.Reception.Positive,
                    actual.Reception.Neutral, actual.Reception.Negative, actual.Reception.Errors
                }, Is.EqualTo(new[]
                {
                    expected.Reception.Attempts, expected.Reception.Perfect, expected.Reception.Positive,
                    expected.Reception.Neutral, expected.Reception.Negative, expected.Reception.Errors
                }));
            Assert.That(new[] { actual.Defense.Attempts, actual.Defense.Successes },
                Is.EqualTo(new[] { expected.Defense.Attempts, expected.Defense.Successes }));
            Assert.That(new[] { actual.Block.Attempts, actual.Block.EffectiveTouches, actual.Block.Points },
                Is.EqualTo(new[] { expected.Block.Attempts, expected.Block.EffectiveTouches, expected.Block.Points }));
            Assert.That(actual.Load.RalliesPlayed, Is.EqualTo(expected.Load.RalliesPlayed));
            Assert.That(actual.Load.ActiveDurationMilliseconds, Is.EqualTo(expected.Load.ActiveDurationMilliseconds));
            Assert.That(actual.Load.MovementDistanceMillimeters, Is.EqualTo(expected.Load.MovementDistanceMillimeters));
            Assert.That(actual.Load.JumpCount, Is.EqualTo(expected.Load.JumpCount));
            Assert.That(actual.Load.HighLoadJumpCount, Is.EqualTo(expected.Load.HighLoadJumpCount));
            Assert.That(actual.Load.LandingLoadBasisPoints, Is.EqualTo(expected.Load.LandingLoadBasisPoints));
            Assert.That(actual.Load.TotalWorkloadBasisPoints, Is.EqualTo(expected.Load.TotalWorkloadBasisPoints));
            Assert.That(actual.Stability.CriticalActions, Is.EqualTo(expected.Stability.CriticalActions));
            Assert.That(actual.Stability.CriticalSuccesses, Is.EqualTo(expected.Stability.CriticalSuccesses));
            Assert.That(actual.Stability.CriticalErrors, Is.EqualTo(expected.Stability.CriticalErrors));
            Assert.That(actual.Stability.ErrorStreakEpisodes, Is.EqualTo(expected.Stability.ErrorStreakEpisodes));
            Assert.That(actual.Stability.LongestErrorStreak, Is.EqualTo(expected.Stability.LongestErrorStreak));
        }

        private static void AssertFixturePayloadEqual(MatchResultV2 expected, MatchResultV2 actual)
        {
            Assert.That(actual.Status, Is.EqualTo(expected.Status));
            Assert.That(actual.WinnerTeamId, Is.EqualTo(expected.WinnerTeamId));
            Assert.That(actual.RallyCount, Is.EqualTo(expected.RallyCount));
            Assert.That(actual.Sets.Select(set => new[]
            {
                set.SetNumber,
                set.HomePoints,
                set.AwayPoints,
                set.IsComplete ? 1 : 0
            }), Is.EqualTo(expected.Sets.Select(set => new[]
            {
                set.SetNumber,
                set.HomePoints,
                set.AwayPoints,
                set.IsComplete ? 1 : 0
            })));
            Assert.That(actual.PlayerFacts.Select(facts => facts.PlayerId),
                Is.EqualTo(expected.PlayerFacts.Select(facts => facts.PlayerId)));
            for (var index = 0; index < expected.PlayerFacts.Count; index++)
            {
                AssertV2FactEqual(expected.PlayerFacts[index], actual.PlayerFacts[index]);
            }
        }

        private static void AssertV2FactEqual(MatchPlayerFactsV2 expected, MatchPlayerFactsV2 actual)
        {
            Assert.That(actual.PlayerId, Is.EqualTo(expected.PlayerId));
            Assert.That(new[] { actual.Spike.Attempts, actual.Spike.Points, actual.Spike.Errors },
                Is.EqualTo(new[] { expected.Spike.Attempts, expected.Spike.Points, expected.Spike.Errors }));
            Assert.That(new[] { actual.Serve.Attempts, actual.Serve.Aces, actual.Serve.Errors },
                Is.EqualTo(new[] { expected.Serve.Attempts, expected.Serve.Aces, expected.Serve.Errors }));
            Assert.That(new[]
            {
                actual.Reception.Attempts,
                actual.Reception.Perfect,
                actual.Reception.Positive,
                actual.Reception.Neutral,
                actual.Reception.Negative,
                actual.Reception.Errors
            }, Is.EqualTo(new[]
            {
                expected.Reception.Attempts,
                expected.Reception.Perfect,
                expected.Reception.Positive,
                expected.Reception.Neutral,
                expected.Reception.Negative,
                expected.Reception.Errors
            }));
            Assert.That(new[] { actual.Defense.Attempts, actual.Defense.Successes },
                Is.EqualTo(new[] { expected.Defense.Attempts, expected.Defense.Successes }));
            Assert.That(new[] { actual.Block.Attempts, actual.Block.EffectiveTouches, actual.Block.Points },
                Is.EqualTo(new[] { expected.Block.Attempts, expected.Block.EffectiveTouches, expected.Block.Points }));
            Assert.That(new[]
            {
                actual.Load.RalliesPlayed,
                actual.Load.ActiveDurationMilliseconds,
                actual.Load.MovementDistanceMillimeters,
                actual.Load.JumpCount,
                actual.Load.HighLoadJumpCount,
                actual.Load.LandingLoadBasisPoints,
                actual.Load.TotalWorkloadBasisPoints
            }, Is.EqualTo(new[]
            {
                expected.Load.RalliesPlayed,
                expected.Load.ActiveDurationMilliseconds,
                expected.Load.MovementDistanceMillimeters,
                expected.Load.JumpCount,
                expected.Load.HighLoadJumpCount,
                expected.Load.LandingLoadBasisPoints,
                expected.Load.TotalWorkloadBasisPoints
            }));
            Assert.That(new[]
            {
                actual.Stability.CriticalActions,
                actual.Stability.CriticalSuccesses,
                actual.Stability.CriticalErrors,
                actual.Stability.ErrorStreakEpisodes,
                actual.Stability.LongestErrorStreak
            }, Is.EqualTo(new[]
            {
                expected.Stability.CriticalActions,
                expected.Stability.CriticalSuccesses,
                expected.Stability.CriticalErrors,
                expected.Stability.ErrorStreakEpisodes,
                expected.Stability.LongestErrorStreak
            }));
        }

        private static void AssertRunnerRejected(FixtureMatchRunnerV2 runner, MatchContextV2 context)
        {
            Assert.ThrowsAsync<MatchV2ContractException>(async () =>
                await runner.ExecuteAsync(context, CancellationToken.None));
        }

        private static FixtureMatchRunnerV2 Runner()
        {
            var pair = FixturePair();
            return new FixtureMatchRunnerV2(
                new VersionedMatchFixtureRepository(pair.ContextBytes, pair.ResultBytes));
        }

        private static (byte[] ContextBytes, byte[] ResultBytes) FixturePair()
        {
            var root = Directory.GetCurrentDirectory();
            return (
                File.ReadAllBytes(Path.Combine(root, FixtureDirectory, "golden-context.json")),
                File.ReadAllBytes(Path.Combine(root, FixtureDirectory, "golden-result.json")));
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private sealed class DelegateRunner : IMatchRunnerV2
        {
            private readonly Func<MatchContextV2, CancellationToken, Task<MatchResultV2>> _execute;

            public DelegateRunner(Func<MatchContextV2, CancellationToken, Task<MatchResultV2>> execute)
            {
                _execute = execute;
            }

            public MatchContextV2 ReceivedContext { get; private set; }

            public CancellationToken ReceivedToken { get; private set; }

            public int CallCount { get; private set; }

            public Task<MatchResultV2> ExecuteAsync(
                MatchContextV2 context,
                CancellationToken cancellationToken)
            {
                CallCount++;
                ReceivedContext = context;
                ReceivedToken = cancellationToken;
                return _execute(context, cancellationToken);
            }
        }
    }
}
