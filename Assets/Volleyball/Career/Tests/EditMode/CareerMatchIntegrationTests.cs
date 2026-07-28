using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
        public void Mapper_UsesInjectedPhysicalRuntimeConfiguration()
        {
            const string physicsHash =
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string predictorHash =
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var predictor = new TrajectoryPredictionProviderConfigurationV4(
                64,
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                7,
                predictorHash);

            var context = new CareerMatchV4Mapper(physicsHash, predictor)
                .ToContext(CareerMatchTestData.Launch());

            Assert.That(context.PhysicsConfigurationHash, Is.EqualTo(physicsHash));
            Assert.That(
                context.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                Is.EqualTo(7));
            Assert.That(
                context.TrajectoryPredictionProviderConfiguration
                    .PredictorConfigurationHash,
                Is.EqualTo(predictorHash));
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
        public async Task Mapper_RejectsUnnormalizedPhysicalWorkload()
        {
            var mapper = new CareerMatchV4Mapper();
            var context = mapper.ToContext(CareerMatchTestData.Launch());
            var fixture = await new DeterministicFixtureMatchRunnerV4()
                .ExecuteAsync(context, CancellationToken.None);
            var stats = fixture.PlayerStats
                .Select((item, index) => new PlayerMatchStatsV4(
                    item.PlayerId,
                    item.Points,
                    item.Contacts,
                    item.Errors,
                    index == 0 ? 2f : item.Workload))
                .ToArray();
            var unnormalized = MatchResultV4.Create(
                context,
                fixture.WinnerTeamId,
                fixture.HomeScore,
                fixture.AwayScore,
                fixture.RalliesPlayed,
                fixture.AcceptedContacts,
                fixture.V3RuleTransitionCount,
                stats);

            Assert.Throws<ContractValidationException>(
                () => mapper.ToCareerFacts(context, unnormalized));
        }

        [Test]
        public void WorkloadNormalization_UsesPerRallyScaleAndClamps()
        {
            Assert.That(
                CareerMatchV4Mapper.NormalizeAccumulatedWorkload(23f, 46),
                Is.EqualTo(0.5f));
            Assert.That(
                CareerMatchV4Mapper.NormalizeAccumulatedWorkload(100f, 25),
                Is.EqualTo(1f));
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
    }
}
