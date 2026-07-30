using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.MatchIntegration;
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
