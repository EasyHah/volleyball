using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.Bootstrap;
using Volleyball.Career.MatchIntegration;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using Object = UnityEngine.Object;

namespace Volleyball.Career.PlayModeTests
{
    public sealed class FormalSixVsSixSceneRunnerPlayModeTests
    {
        [Test]
        public void FixtureRouting_RejectsPartialLegacyConfigurationMatch()
        {
            var template = SandboxContext();
            var unsupported = MatchContextV4.Create(
                Guid.Parse("19191919-1919-4919-8919-191919191919"),
                template.Seed,
                template.Home,
                template.Away,
                CareerMatchV4Mapper.FixturePhysicsConfigurationHash,
                new TrajectoryPredictionProviderConfigurationV4(
                    64,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    1,
                    CareerMatchV4Mapper.FixturePredictorConfigurationHash),
                template.RulesVersion);
            var host = new GameObject("CareerUnknownFixtureRunnerTestHost");
            var runner = new OfflineCareerMatchRunnerV4(host);

            Assert.Throws<InvalidOperationException>(() =>
                runner.ExecuteAsync(unsupported, CancellationToken.None));

            Object.DestroyImmediate(host);
        }

        [Test]
        public void PhysicalWorkload_NormalizesKnownRawUnitAtBoundaryOne()
        {
            var context = SandboxContext();
            var stats = context.Home.Players
                .Concat(context.Away.Players)
                .Select(player => new PlayerMatchStatsV4(
                    player.PlayerId,
                    0,
                    1,
                    0,
                    1f))
                .ToArray();
            var raw = MatchResultV4.Create(
                context,
                context.Home.TeamId,
                25,
                20,
                45,
                12,
                4,
                stats);
            var normalize = typeof(FormalSixVsSixSceneRunnerV4).GetMethod(
                "NormalizePhysicalWorkload",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(normalize, Is.Not.Null);
            var normalized = (MatchResultV4)normalize.Invoke(
                null,
                new object[] { context, raw });

            Assert.That(
                normalized.PlayerStats.Select(player => player.Workload),
                Has.All.EqualTo(1f / 45f).Within(0.000001f));
        }

        [Test]
        public void RuntimeRegistrationReset_ClearsAbandonedExternalContext()
        {
            var context = SandboxContext();
            var reset = typeof(FormalSixVsSixRallyBootstrap).GetMethod(
                "ResetExternalContextSlot",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);

            reset.Invoke(null, null);
            FormalSixVsSixRallyBootstrap.QueueExternalContext(context);
            reset.Invoke(null, null);
            Assert.DoesNotThrow(() =>
                FormalSixVsSixRallyBootstrap.QueueExternalContext(context));
            FormalSixVsSixRallyBootstrap.ClearQueuedExternalContext(context);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator LegacyFixtureContext_DoesNotLoadPhysicalScene()
        {
            var template = SandboxContext();
            var fixture = MatchContextV4.Create(
                System.Guid.Parse("18181818-1818-4818-8818-181818181818"),
                template.Seed,
                template.Home,
                template.Away,
                CareerMatchV4Mapper.FixturePhysicsConfigurationHash,
                new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    1,
                    CareerMatchV4Mapper.FixturePredictorConfigurationHash),
                template.RulesVersion);
            var host = new GameObject("CareerFixtureRunnerTestHost");
            var runner = new OfflineCareerMatchRunnerV4(host);

            var task = runner.ExecuteAsync(fixture, CancellationToken.None);
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.That(task.IsCompletedSuccessfully, Is.True);
            Assert.DoesNotThrow(() => task.Result.ValidateAgainst(fixture));
            var physical = SceneManager.GetSceneByName(
                FormalSixVsSixSceneRunnerV4.SceneName);
            Assert.That(!physical.IsValid() || !physical.isLoaded, Is.True);

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Cancellation_UnloadsMatchAndRestoresCareerScene()
        {
            var context = SandboxContext();
            var careerScene = SceneManager.GetActiveScene();
            var host = new GameObject("CareerRunnerTestHost");
            var runner = new FormalSixVsSixSceneRunnerV4(host);
            var cancellation = new CancellationTokenSource();

            var task = runner.ExecuteAsync(context, cancellation.Token);
            yield return new WaitUntil(() =>
            {
                var match = SceneManager.GetSceneByName(
                    FormalSixVsSixSceneRunnerV4.SceneName);
                return match.IsValid() && match.isLoaded;
            });
            cancellation.Cancel();
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.That(task.IsCanceled, Is.True);
            var unloaded = SceneManager.GetSceneByName(
                FormalSixVsSixSceneRunnerV4.SceneName);
            Assert.That(!unloaded.IsValid() || !unloaded.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(careerScene));

            cancellation.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        private static MatchContextV4 SandboxContext()
        {
            var contextFactory = typeof(FormalSixVsSixRallyBootstrap).GetMethod(
                "CreateSandboxContext",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(contextFactory, Is.Not.Null);
            return (MatchContextV4)contextFactory.Invoke(null, null);
        }
    }
}
