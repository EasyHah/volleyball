using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.MatchIntegration;
using Volleyball.Career.Persistence;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchIntegrationTests
    {
        [Test]
        public void Mapper_ProducesAuthoritativeV4ContextWithTwelveFrozenPlayers()
        {
            var context = new CareerMatchV4Mapper().ToContext(CareerMatchTestData.Launch());

            Assert.That(context.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(context.SessionId, Is.EqualTo(CareerMatchTestData.SessionId));
            Assert.That(context.Home.RotationOrder.Count, Is.EqualTo(6));
            Assert.That(context.Away.RotationOrder.Count, Is.EqualTo(6));
            Assert.That(
                context.Home.RotationOrder.Concat(context.Away.RotationOrder)
                    .Select(player => player.PlayerId)
                    .Distinct()
                    .Count(),
                Is.EqualTo(12));
            Assert.That(context.ContextHash, Has.Length.EqualTo(64));
        }

        [Test]
        public async Task FixtureRunner_IsDeterministicAndReturnsCompleteV4Facts()
        {
            var context = new CareerMatchV4Mapper().ToContext(CareerMatchTestData.Launch());
            var runner = new DeterministicFixtureMatchRunnerV4();

            var first = await runner.ExecuteAsync(context, CancellationToken.None);
            var second = await runner.ExecuteAsync(context, CancellationToken.None);

            Assert.That(first.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(first.HomeScore, Is.EqualTo(25));
            Assert.That(first.AwayScore, Is.EqualTo(21));
            Assert.That(first.PlayerStats.Count, Is.EqualTo(12));
            Assert.That(ContractJson.SerializeV4(second), Is.EqualTo(ContractJson.SerializeV4(first)));
        }

        [Test]
        public async Task Executor_RoundTripsCanonicalV4AndMapsCareerFacts()
        {
            var executor = new CareerMatchExecutorV4(new DeterministicFixtureMatchRunnerV4());
            var encoded = executor.Encode(CareerMatchTestData.Launch());

            var executed = await executor.ExecuteAsync(encoded, CancellationToken.None);
            var decoded = executor.DecodeAndValidate(
                encoded.CanonicalContextUtf8,
                executed.CanonicalResultUtf8);
            var context = ContractJson.DeserializeMatchContextV4(
                Encoding.UTF8.GetString(encoded.CanonicalContextUtf8));

            Assert.That(context.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(decoded.Context.ContextDigest, Is.EqualTo(encoded.ContextDigest));
            Assert.That(decoded.ResultDigest, Is.EqualTo(executed.ResultDigest));
            Assert.That(decoded.Facts.Versions.ContractVersion, Is.EqualTo(4));
            Assert.That(decoded.Facts.PlayerFacts.Count, Is.EqualTo(12));
            Assert.That(decoded.Facts.Sets.Single().HomePoints, Is.EqualTo(25));
        }

        [Test]
        public async Task DirectMapper_OnlyMapsMeasuredAggregateWorkloadIntoCareerFacts()
        {
            var mapper = new CareerMatchV4Mapper(
                new CareerMatchV4RuntimeConfiguration(
                    CareerMatchV4Mapper.FixturePhysicsConfigurationHash,
                    new TrajectoryPredictionProviderConfigurationV4(
                        128,
                        TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                        1,
                        CareerMatchV4Mapper.FixturePredictorConfigurationHash),
                    CareerMatchV4FactPolicy.DirectAggregateOnly));
            var launch = CareerMatchTestData.Launch(
                executionMode: CareerMatchExecutionMode.Direct,
                fixtureId: null,
                fixtureVersion: null);
            var context = mapper.ToContext(launch);
            var result = await new DeterministicFixtureMatchRunnerV4()
                .ExecuteAsync(context, CancellationToken.None);

            var facts = mapper.ToCareerFacts(context, result);

            Assert.That(facts.PlayerFacts.All(fact =>
                fact.Spike.Attempts == 0 &&
                fact.Serve.Attempts == 0 &&
                fact.Reception.Attempts == 0 &&
                fact.Defense.Attempts == 0 &&
                fact.Block.Attempts == 0 &&
                fact.Stability.CriticalActions == 0), Is.True);
            Assert.That(facts.PlayerFacts
                    .Select(fact => fact.Load.TotalWorkloadBasisPoints),
                Has.All.InRange(0, 10000));
            Assert.That(facts.PlayerFacts
                    .Select(fact => fact.Load.TotalWorkloadBasisPoints),
                Has.Some.GreaterThan(0));
        }

        [Test]
        public async Task CompatibilityPolicy_PreservesLegacyFixtureFactsOnlyForExactFixtureConfiguration()
        {
            var fixtureMapper = new CareerMatchV4Mapper();
            var fixtureContext = fixtureMapper.ToContext(CareerMatchTestData.Launch());
            var fixtureResult = await new DeterministicFixtureMatchRunnerV4()
                .ExecuteAsync(fixtureContext, CancellationToken.None);
            var compatibilityMapper = new CareerMatchV4Mapper(
                new CareerMatchV4RuntimeConfiguration(
                    new string('c', 64),
                    new TrajectoryPredictionProviderConfigurationV4(
                        128,
                        TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                        2,
                        new string('d', 64)),
                    CareerMatchV4FactPolicy
                        .DirectAggregateWithLegacyFixtureCompatibility));

            var fixtureFacts = compatibilityMapper.ToCareerFacts(
                fixtureContext,
                fixtureResult);
            Assert.That(
                fixtureFacts.PlayerFacts.Any(fact =>
                    fact.Spike.Attempts > 0 ||
                    fact.Reception.Attempts > 0 ||
                    fact.Defense.Attempts > 0 ||
                    fact.Block.Attempts > 0),
                Is.True);

            var directLaunch = CareerMatchTestData.Launch(
                executionMode: CareerMatchExecutionMode.Direct,
                fixtureId: null,
                fixtureVersion: null);
            var directContext = compatibilityMapper.ToContext(directLaunch);
            var directResult = await new DeterministicFixtureMatchRunnerV4()
                .ExecuteAsync(directContext, CancellationToken.None);
            var directFacts = compatibilityMapper.ToCareerFacts(
                directContext,
                directResult);

            Assert.That(directFacts.PlayerFacts.All(fact =>
                fact.Spike.Attempts == 0 &&
                fact.Serve.Attempts == 0 &&
                fact.Reception.Attempts == 0 &&
                fact.Defense.Attempts == 0 &&
                fact.Block.Attempts == 0 &&
                fact.Stability.CriticalActions == 0), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void CareerAttributeMapping_IsMonotonicForEveryCareerAxis(
            int raisedAxis)
        {
            var baseline = Enumerable.Repeat(5000, 8).ToArray();
            var raised = baseline.ToArray();
            raised[raisedAxis] = 6000;

            var baselineInputs = V4Inputs(ContextForAttributes(
                baseline,
                fatigue: 40));
            var raisedInputs = V4Inputs(ContextForAttributes(
                raised,
                fatigue: 40));

            Assert.That(
                raisedInputs.Zip(
                    baselineInputs,
                    (higher, lower) => higher + 0.000001f >= lower),
                Has.All.True);
            Assert.That(
                raisedInputs.Zip(
                    baselineInputs,
                    (higher, lower) => higher > lower + 0.000001f),
                Has.Some.True);
        }

        [Test]
        public void CareerFatigue_IsAppliedExactlyOnceWhenCreatingV4Inputs()
        {
            var attributes = Enumerable.Repeat(8000, 8).ToArray();
            var fresh = V4Inputs(ContextForAttributes(attributes, fatigue: 0));
            var fatigued = V4Inputs(ContextForAttributes(
                attributes,
                fatigue: 100));

            Assert.That(
                fatigued.Zip(
                    fresh,
                    (actual, baseline) =>
                        Math.Abs(actual - (baseline * 0.75f)) < 0.000001f),
                Has.All.True);
        }

        [Test]
        public void V5Mapper_FreezesAllTwelveBasesAfterApplyingFatigueOnce()
        {
            var source = new CareerBaseAttributesV5(
                8000, 1900, 7900, 7800, 7700, 7600,
                7500, 7400, 7300, 7200, 7100, 7000);
            var launch = V5Launch(source, fatigue: 100);
            var mapper = new CareerMatchV5Mapper(
                new string('a', 64), V5TrajectoryConfiguration());

            var context = mapper.ToContext(launch);
            var player = context.Home.RotationOrder[0];

            Assert.That(context.ContractVersion, Is.EqualTo(ContractVersions.MatchV5));
            Assert.That(player.Bases.Strength, Is.EqualTo(6000));
            Assert.That(player.Bases.Jump, Is.EqualTo(5925));
            Assert.That(player.Bases.Set, Is.EqualTo(5250));
            Assert.That(player.Bases.HeightMillimeters, Is.EqualTo(1900));
            Assert.That(player.Derived.AttackPower,
                Is.EqualTo((5625 + 6000 + 5925 + 5700 + 3) / 4));
        }

        [Test]
        public void V5Mapper_ChangingEachCareerBaseChangesADeclaredDerivedConsumer()
        {
            var mapper = new CareerMatchV5Mapper(
                new string('a', 64), V5TrajectoryConfiguration());
            var baseline = new[] { 5000, 1900, 5000, 5000, 5000, 5000,
                5000, 5000, 5000, 5000, 5000, 5000 };
            for (var index = 0; index < baseline.Length; index++)
            {
                var raised = baseline.ToArray();
                raised[index] = index == 1 ? 2000 : 6000;
                var low = mapper.ToContext(V5Launch(V5Bases(baseline), 0))
                    .Home.RotationOrder[0].Derived;
                var high = mapper.ToContext(V5Launch(V5Bases(raised), 0))
                    .Home.RotationOrder[0].Derived;
                Assert.That(high.ResultFingerprint, Is.Not.EqualTo(low.ResultFingerprint),
                    "V5 base index " + index + " must affect the frozen consumer inputs.");
            }
        }

        [Test]
        public void V5Pending_RejectsLegacyPendingWithRecoverableDiscardAction()
        {
            var context = new CareerMatchV5Mapper(new string('a', 64),
                V5TrajectoryConfiguration()).ToContext(V5Launch(V5Bases(new[] { 5000, 1900, 5000, 5000, 5000, 5000,
                    5000, 5000, 5000, 5000, 5000, 5000 }), 0));
            var pending = CareerPendingMatchV5.Create(context);

            Assert.That(CareerV5PendingRecovery.Read(pending).Kind,
                Is.EqualTo(CareerV5PendingRecoveryKind.Ready));
            Assert.That(CareerV5PendingRecovery.RejectLegacy(CreateLegacyPending()).Kind,
                Is.EqualTo(CareerV5PendingRecoveryKind.DiscardLegacyPendingAndCreateV5));
        }

        [Test]
        public void V5Profile_RoundTripsAllCareerOwnedBasesWithoutDefaults()
        {
            var profile = new Volleyball.Career.Domain.CareerPlayerProfileV5(
                new PlayerId("v5.profile.player"), "V5 Player", 8,
                DominantHandV5.Left, new CareerBaseAttributesV5(
                    6100, 1975, 6200, 6300, 6400, 6500,
                    6600, 6700, 6800, 6900, 7000, 7100), 12, 64, 77);

            var bytes = CareerPlayerProfileV5JsonCodec.Serialize(profile);
            var restored = CareerPlayerProfileV5JsonCodec.Deserialize(bytes);

            CollectionAssert.AreEqual(bytes, CareerPlayerProfileV5JsonCodec.Serialize(restored));
            Assert.That(restored.DominantHand, Is.EqualTo(DominantHandV5.Left));
            Assert.That(restored.Bases.HeightMillimeters, Is.EqualTo(1975));
            Assert.That(restored.Bases.Set, Is.EqualTo(7100));
            Assert.That(restored.Fatigue, Is.EqualTo(12));
            Assert.That(restored.Mindset, Is.EqualTo(64));
            Assert.That(restored.CoachTrust, Is.EqualTo(77));
        }

        [Test]
        public void V5Settlement_ConsumesVerifiedReportWithoutRescanningReplay()
        {
            var profile = new Volleyball.Career.Domain.CareerPlayerProfileV5(
                new PlayerId("v5.settlement.player"), "Settlement Player", 8,
                DominantHandV5.Right, V5Bases(new[] { 5000, 1900, 5000, 5000, 5000, 5000,
                    5000, 5000, 5000, 5000, 5000, 5000 }));
            var launch = new CareerFirstMatchLaunchFactoryV5().Create(profile, new TeamId("v5.settlement.home"),
                0, new Guid("99999999-9999-9999-9999-999999999999"), 10);
            var context = new CareerMatchV5Mapper(new string('a', 64), V5TrajectoryConfiguration()).ToContext(launch);
            var result = MatchResultV5.Create(context, context.Home.TeamId, 25, 20, 45);
            var reports = context.Home.RotationOrder.Concat(context.Away.RotationOrder).Select(player =>
                new CareerMatchPlayerReportV1(player.PlayerId, player.PlayerId.Equals(profile.PlayerId) ? 3 : 0,
                    player.PlayerId.Equals(profile.PlayerId) ? 2 : 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 45, 0, 1, 300, 1, 1, 1, 0, 0, 0, 0, 0)).ToArray();
            var replay = MatchReplayV5.Create("v5.settlement", context);
            var report = CareerMatchReportV1.Create(context, result, CareerMatchEvidenceKindV1.PhysicalReplay,
                replay.ReplayHash, reports);

            var settlement = CareerV5MatchSettlementRules.Apply(profile, context, result, report,
                replay, null);

            Assert.That(settlement.Profile.Bases.Attack, Is.GreaterThan(profile.Bases.Attack));
            Assert.That(settlement.FatigueDelta, Is.EqualTo(3));
            Assert.That(settlement.Profile.Fatigue, Is.EqualTo(profile.Fatigue + settlement.FatigueDelta));
            Assert.That(settlement.ReportHash, Is.EqualTo(report.ReportHash));
        }

        [Test]
        public void V5PendingStore_RoundTripsOnlyCanonicalV5Artifacts()
        {
            var root = Path.Combine(Path.GetTempPath(), "volleyball-v5-store-" + Guid.NewGuid().ToString("N"));
            try
            {
                var profile = new Volleyball.Career.Domain.CareerPlayerProfileV5(
                    new PlayerId("v5.store.player"), "Stored Player", 12,
                    DominantHandV5.Left, V5Bases(new[] { 6100, 1975, 6200, 6300, 6400, 6500,
                        6600, 6700, 6800, 6900, 7000, 7100 }));
                var store = new CareerV5PendingStore(new CareerStoragePaths(root),
                    new SystemAtomicFileSystem());
                var context = new CareerMatchV5Mapper(new string('a', 64),
                    V5TrajectoryConfiguration()).ToContext(V5Launch(profile.Bases, 0));
                var pending = CareerPendingMatchV5.Create(context);

                store.SaveProfile(profile);
                store.SavePending(profile.PlayerId, pending.CanonicalContextUtf8);

                Assert.That(store.LoadProfile(profile.PlayerId).Bases.Set, Is.EqualTo(7100));
                var restored = CareerPendingMatchV5.FromCanonicalContext(store.LoadPending(profile.PlayerId));
                Assert.That(restored.ContextHash, Is.EqualTo(pending.ContextHash));
                Assert.That(store.DiscardPending(profile.PlayerId), Is.True);
                Assert.That(store.LoadPending(profile.PlayerId), Is.Null);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void V5FirstMatchFactory_UsesThePersistedProfileWithoutPositionOverrides()
        {
            var profile = new Volleyball.Career.Domain.CareerPlayerProfileV5(
                new PlayerId("v5.factory.player"), "Factory Player", 9,
                DominantHandV5.Left, new CareerBaseAttributesV5(
                    6100, 2010, 6200, 6300, 6400, 6500,
                    6600, 6700, 6800, 6900, 7000, 7100));
            var launch = new CareerFirstMatchLaunchFactoryV5().Create(profile,
                new TeamId("team.factory.home"), 20,
                new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), 99);
            var context = new CareerMatchV5Mapper(new string('a', 64),
                V5TrajectoryConfiguration()).ToContext(launch);
            var player = context.Home.RotationOrder.Single(value =>
                value.PlayerId.Equals(profile.PlayerId));

            Assert.That(player.DominantHand, Is.EqualTo(DominantHandV5.Left));
            Assert.That(player.Bases.HeightMillimeters, Is.EqualTo(2010));
            Assert.That(player.Bases.Attack, Is.EqualTo(6270));
            Assert.That(player.Bases.Set, Is.EqualTo(6745));
        }

        [Test]
        public async Task V5Lifecycle_ExecutesTheFrozenPendingContextWithoutApplyingFatigueTwice()
        {
            var profile = new Volleyball.Career.Domain.CareerPlayerProfileV5(
                new PlayerId("v5.lifecycle.player"), "Lifecycle Player", 10,
                DominantHandV5.Right, new CareerBaseAttributesV5(
                    8000, 1900, 8000, 8000, 8000, 8000,
                    8000, 8000, 8000, 8000, 8000, 8000));
            var mapper = new CareerMatchV5Mapper(new string('a', 64), V5TrajectoryConfiguration());
            var service = new CareerV5MatchLifecycleService(
                new CareerFirstMatchLaunchFactoryV5(), mapper,
                new CareerMatchExecutorV5(mapper, new TestV5Runner()));
            var pending = service.CreatePending(profile, new TeamId("team.lifecycle.home"),
                100, new Guid("cccccccc-dddd-eeee-ffff-000000000000"), 7);

            var result = await service.ExecuteAsync(pending, CancellationToken.None);

            Assert.That(pending.Context.Home.RotationOrder.Single(player =>
                player.PlayerId.Equals(profile.PlayerId)).Bases.Attack, Is.EqualTo(6000));
            Assert.That(result.Result.ContextHash, Is.EqualTo(pending.ContextHash));
        }

        [Test]
        public async Task V5QuickSimulation_RepeatsTraceAndReportBytesForTheSameContext()
        {
            var profile = new Volleyball.Career.Domain.CareerPlayerProfileV5(
                new PlayerId("v5.quick.player"), "Quick Player", 10,
                DominantHandV5.Right, V5Bases(new[] { 6000, 1900, 6000, 6000, 6000, 6000,
                    6000, 6000, 6000, 6000, 6000, 6000 }));
            var context = new CareerMatchV5Mapper(new string('a', 64),
                V5TrajectoryConfiguration()).ToContext(V5Launch(profile.Bases, 0));
            var runner = new DeterministicQuickSimulationRunnerV5();
            var first = await runner.ExecuteAsync(context, CancellationToken.None);
            var second = await runner.ExecuteAsync(context, CancellationToken.None);

            Assert.That(ContractJson.SerializeV1(first.QuickTrace),
                Is.EqualTo(ContractJson.SerializeV1(second.QuickTrace)));
            Assert.That(ContractJson.SerializeV1(first.Report),
                Is.EqualTo(ContractJson.SerializeV1(second.Report)));
            first.QuickTrace.ValidateAgainst(context);
            first.Report.ValidateAgainst(context, first.Result);
            Assert.That(first.Report.EvidenceHash, Is.EqualTo(first.QuickTrace.TraceHash));
            Assert.That(ContractJson.SerializeV1(DeterministicQuickSimulationRunnerV5.RebuildReport(
                context, first.Result, first.QuickTrace)), Is.EqualTo(ContractJson.SerializeV1(first.Report)));
            Assert.That(DeterministicQuickSimulationRunnerV5.RebuildResult(context, first.QuickTrace).ResultHash,
                Is.EqualTo(first.Result.ResultHash));
        }

        [Test]
        public async Task V5SettlementStore_IsIdempotentPerSessionAndAllowsTheNextSession()
        {
            var root = Path.Combine(Path.GetTempPath(), "volleyball-v5-settlement-" + Guid.NewGuid().ToString("N"));
            try
            {
                var profile = new Volleyball.Career.Domain.CareerPlayerProfileV5(
                    new PlayerId("v5.home.p0"), "Settled Player", 8, DominantHandV5.Right,
                    V5Bases(new[] { 5000, 1900, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000 }));
                var context = new CareerMatchV5Mapper(new string('a', 64),
                    V5TrajectoryConfiguration()).ToContext(V5Launch(profile.Bases, 0));
                var runner = new DeterministicQuickSimulationRunnerV5();
                var first = await runner.ExecuteAsync(context, CancellationToken.None);
                var store = new CareerV5PendingStore(new CareerStoragePaths(root), new SystemAtomicFileSystem());
                var firstSettlement = CareerV5MatchSettlementRules.Apply(profile, context, first.Result,
                    first.Report, null, first.QuickTrace);

                store.SavePending(profile.PlayerId, Encoding.UTF8.GetBytes(ContractJson.SerializeV5(context)));
                store.CommitSettlement(firstSettlement.Profile, context, first.Result, first.Report, first.QuickTrace);
                store.CommitSettlement(firstSettlement.Profile, context, first.Result, first.Report, first.QuickTrace);
                Assert.That(store.LoadPending(profile.PlayerId), Is.Null);
                Assert.That(store.LoadProfile(profile.PlayerId).Fatigue, Is.EqualTo(firstSettlement.Profile.Fatigue));
                var latestReceiptPath = Directory.GetFiles(root, "settlement-receipt.json", SearchOption.AllDirectories).Single();
                File.Delete(latestReceiptPath);
                Assert.That(store.LoadProfile(profile.PlayerId).Fatigue, Is.EqualTo(firstSettlement.Profile.Fatigue));
                Assert.That(File.Exists(latestReceiptPath), Is.True);

                var nextContext = MatchContextV5.Create(
                    new Guid("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"), context.Seed, context.Home, context.Away,
                    context.PhysicsConfigurationHash, context.TrajectoryPredictionProviderConfiguration);
                var second = await runner.ExecuteAsync(nextContext, CancellationToken.None);
                store.SavePending(profile.PlayerId, Encoding.UTF8.GetBytes(ContractJson.SerializeV5(nextContext)));
                Assert.That(store.LoadPending(profile.PlayerId), Is.Not.Null);
                var secondSettlement = CareerV5MatchSettlementRules.Apply(firstSettlement.Profile, nextContext,
                    second.Result, second.Report, null, second.QuickTrace);
                store.CommitSettlement(secondSettlement.Profile, nextContext, second.Result, second.Report, second.QuickTrace);

                Assert.That(store.LoadPending(profile.PlayerId), Is.Null);
                Assert.That(store.LoadProfile(profile.PlayerId).Fatigue, Is.EqualTo(secondSettlement.Profile.Fatigue));
                store.CommitSettlement(firstSettlement.Profile, context, first.Result, first.Report, first.QuickTrace);
                Assert.That(store.LoadProfile(profile.PlayerId).Fatigue, Is.EqualTo(secondSettlement.Profile.Fatigue));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public async Task V5SettlementStore_RejectsTamperedDurableReceipt()
        {
            var root = Path.Combine(Path.GetTempPath(), "volleyball-v5-tamper-" + Guid.NewGuid().ToString("N"));
            try
            {
                var profile = new Volleyball.Career.Domain.CareerPlayerProfileV5(
                    new PlayerId("v5.home.p0"), "Receipt Player", 8, DominantHandV5.Right,
                    V5Bases(new[] { 5000, 1900, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000 }));
                var context = new CareerMatchV5Mapper(new string('a', 64),
                    V5TrajectoryConfiguration()).ToContext(V5Launch(profile.Bases, 0));
                var outcome = await new DeterministicQuickSimulationRunnerV5().ExecuteAsync(context, CancellationToken.None);
                var settlement = CareerV5MatchSettlementRules.Apply(profile, context, outcome.Result,
                    outcome.Report, null, outcome.QuickTrace);
                var store = new CareerV5PendingStore(new CareerStoragePaths(root), new SystemAtomicFileSystem());
                store.CommitSettlement(settlement.Profile, context, outcome.Result, outcome.Report, outcome.QuickTrace);

                var receiptPath = Directory.GetFiles(root, "settlement-receipt.json", SearchOption.AllDirectories).Single();
                var bytes = File.ReadAllBytes(receiptPath);
                bytes[bytes.Length / 2] ^= 0x01;
                File.WriteAllBytes(receiptPath, bytes);

                Assert.That(() => store.LoadProfile(profile.PlayerId), Throws.Exception);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static TrajectoryPredictionProviderConfigurationV5 V5TrajectoryConfiguration()
        {
            return new TrajectoryPredictionProviderConfigurationV5(
                128, TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                1, new string('b', 64));
        }

        private static CareerBaseAttributesV5 V5Bases(int[] values)
        {
            return new CareerBaseAttributesV5(values[0], values[1], values[2], values[3],
                values[4], values[5], values[6], values[7], values[8], values[9],
                values[10], values[11]);
        }

        private static CareerMatchLaunchV5 V5Launch(CareerBaseAttributesV5 protagonist, int fatigue)
        {
            var home = new CareerMatchTeamLaunchV5(new TeamId("v5.home"), "Home",
                CareerMatchTeamSide.Home, V5Team("v5.home", protagonist, fatigue));
            var away = new CareerMatchTeamLaunchV5(new TeamId("v5.away"), "Away",
                CareerMatchTeamSide.Away, V5Team("v5.away", V5Bases(new[] { 5000, 1900, 5000, 5000, 5000, 5000,
                    5000, 5000, 5000, 5000, 5000, 5000 }), 0));
            return new CareerMatchLaunchV5(new Guid("11111111-2222-3333-4444-555555555555"),
                1234, new[] { home, away });
        }

        private static CareerMatchPlayerLaunchV5[] V5Team(string prefix,
            CareerBaseAttributesV5 first, int firstFatigue)
        {
            var positions = new[]
            {
                CareerMatchPlayerPosition.OutsideHitter, CareerMatchPlayerPosition.Opposite,
                CareerMatchPlayerPosition.MiddleBlocker, CareerMatchPlayerPosition.Setter,
                CareerMatchPlayerPosition.OutsideHitter, CareerMatchPlayerPosition.Libero
            };
            var players = new CareerMatchPlayerLaunchV5[6];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = new CareerMatchPlayerLaunchV5(new PlayerId(prefix + ".p" + index),
                    "P" + index, index + 1, positions[index], index + 1,
                    index == 0 ? firstFatigue : 0, DominantHandV5.Right,
                    index == 0 ? first : V5Bases(new[] { 5000, 1900, 5000, 5000, 5000, 5000,
                        5000, 5000, 5000, 5000, 5000, 5000 }));
            }
            return players;
        }

        private static Volleyball.Career.Domain.PendingCareerMatch CreateLegacyPending()
        {
            var launch = CareerMatchTestData.Launch();
            var context = new CareerMatchV4Mapper().ToContext(launch);
            return new Volleyball.Career.Domain.PendingCareerMatch(context.SessionId,
                new Volleyball.Career.Domain.OperationId(Guid.NewGuid()),
                new Volleyball.Career.Domain.LineageId(Guid.NewGuid()), 1,
                new Volleyball.Career.Domain.CareerMatchLifecycleVersions(4, 1, 1, 1, null, null),
                Volleyball.Career.Domain.CareerMatchLifecycleExecutionMode.Direct, null, null,
                launch.MatchSeed, launch.CompetitionId, launch.ScheduleItemId,
                new Volleyball.Career.Domain.WeekPlanId(Guid.NewGuid()),
                new Volleyball.Career.Domain.SlotActionId(Guid.NewGuid()),
                new Volleyball.Career.Domain.OccurrenceId(Guid.NewGuid()),
                Volleyball.Career.Domain.CareerMatchPriority.AttackFirst,
                new Volleyball.Career.Domain.Sha256Digest(context.ContextHash),
                Encoding.UTF8.GetBytes(ContractJson.SerializeV4(context)),
                context.Home.TeamId, context.Away.TeamId,
                context.Home.RotationOrder.Concat(context.Away.RotationOrder).Select(player => player.PlayerId),
                context.Home.RotationOrder[0].PlayerId,
                Array.Empty<Volleyball.Career.Domain.FrozenCareerTrainingEmphasis>());
        }

        private sealed class TestV5Runner : ICareerMatchRunnerV5
        {
            public Task<CareerMatchRunOutcomeV5> ExecuteAsync(MatchContextV5 context,
                CancellationToken cancellationToken)
            {
                var result = MatchResultV5.Create(context, context.Home.TeamId, 25, 20, 45);
                return Task.FromResult(new CareerMatchRunOutcomeV5(result,
                    MatchReplayV5.Create("test-v5-" + context.SessionId.ToString("D"), context)));
            }
        }

        [Test]
        public void Executor_RejectsNonCanonicalPersistedContext()
        {
            var executor = new CareerMatchExecutorV4(new DeterministicFixtureMatchRunnerV4());
            var encoded = executor.Encode(CareerMatchTestData.Launch());
            var original = encoded.CanonicalContextUtf8;
            var tampered = new byte[original.Length + 1];
            original.CopyTo(tampered, 0);
            tampered[tampered.Length - 1] = (byte)' ';
            var envelope = new Volleyball.Career.Application.CareerCanonicalMatchContext(
                encoded.SessionId,
                encoded.ContextDigest,
                tampered);

            Assert.ThrowsAsync<ContractValidationException>(
                async () => await executor.ExecuteAsync(envelope, CancellationToken.None));
        }

        private static MatchContextV4 ContextForAttributes(
            int[] values,
            int fatigue)
        {
            var teams = CareerMatchTestData.Teams();
            var homePlayers = teams[0].Players.ToArray();
            var protagonist = homePlayers.Single(player =>
                player.PlayerId.Value == "player.career.protagonist");
            var index = Array.IndexOf(homePlayers, protagonist);
            homePlayers[index] = new CareerMatchPlayerLaunch(
                protagonist.PlayerId,
                protagonist.JerseyNumber,
                protagonist.Position,
                protagonist.RotationSlot,
                fatigue,
                CareerMatchTestData.Attributes(
                    values[0],
                    values[1],
                    values[2],
                    values[3],
                    values[4],
                    values[5],
                    values[6],
                    values[7]));
            var customTeams = new[]
            {
                new CareerMatchTeamLaunch(
                    teams[0].TeamId,
                    teams[0].Side,
                    homePlayers),
                teams[1]
            };
            return new CareerMatchV4Mapper().ToContext(
                CareerMatchTestData.Launch(teams: customTeams));
        }

        private static float[] V4Inputs(MatchContextV4 context)
        {
            var player = context.Home.RotationOrder.Single(value =>
                value.PlayerId.Value == "player.career.protagonist");
            return new[]
            {
                player.Physical.Jump,
                player.Physical.Mobility,
                player.Physical.Reaction,
                player.Physical.Coordination,
                player.Technical.AttackTechnique,
                player.Technical.AttackPower,
                player.Technical.BlockTechnique,
                player.Technical.DefenseTechnique,
                player.Technical.ReceiveTechnique,
                player.Technical.SetTechnique,
                player.Technical.ServeTechnique,
                player.Technical.SoftTouch,
                player.Technical.CourtAwareness
            };
        }
    }
}
