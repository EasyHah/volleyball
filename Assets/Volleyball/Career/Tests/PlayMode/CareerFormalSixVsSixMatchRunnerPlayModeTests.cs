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
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.MatchIntegration;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using Object = UnityEngine.Object;

namespace Volleyball.Career.PlayModeTests
{
    public sealed class CareerFormalSixVsSixMatchRunnerPlayModeTests
    {
        [Test]
        public void OfflineRouter_ExecutesOnlyTheExactLegacyFixtureConfiguration()
        {
            var template = FormalSixVsSixRallyBootstrap
                .CreateDefaultFormalContext();
            var fixtureContext = MatchContextV4.Create(
                Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
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
            var unsupported = MatchContextV4.Create(
                Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"),
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
            var host = new GameObject("CareerOfflineRouterTestHost");
            var formalRunner =
                host.AddComponent<CareerFormalSixVsSixMatchRunnerV4>();
            var router = new CareerOfflineMatchRouterV4(formalRunner);

            var execution = router.ExecuteAsync(
                fixtureContext,
                CancellationToken.None);

            Assert.That(execution.IsCompletedSuccessfully, Is.True);
            Assert.DoesNotThrow(() =>
                execution.Result.ValidateAgainst(fixtureContext));
            Assert.Throws<InvalidOperationException>(() =>
                router.ExecuteAsync(unsupported, CancellationToken.None));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void ContextStartup_SubsystemRegistrationClearsAbandonedContext()
        {
            var context = FormalSixVsSixRallyBootstrap
                .CreateDefaultFormalContext();
            var reset = typeof(FormalMatchContextStartupV4).GetMethod(
                "ResetPendingContextOnSubsystemRegistration",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);

            reset.Invoke(null, null);
            FormalMatchContextStartupV4.PrepareNextFormalStart(context);
            reset.Invoke(null, null);

            Assert.That(
                FormalMatchContextStartupV4.CancelPendingFormalStart(
                    context.SessionId),
                Is.False);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Runner_InjectsTheExactCareerContextAndCleansUpOnCancellation()
        {
            var careerScene = SceneManager.GetActiveScene();
            var host = new GameObject("CareerFormalRunnerTestHost");
            var runner = host.AddComponent<CareerFormalSixVsSixMatchRunnerV4>();
            var context = FormalSixVsSixRallyBootstrap.CreateDefaultFormalContext();
            using var cancellation = new CancellationTokenSource();
            var execution = runner.ExecuteAsync(context, cancellation.Token);

            var frames = 0;
            while (!SceneManager.GetSceneByName(
                       CareerFormalSixVsSixMatchRunnerV4.FormalSceneName).isLoaded &&
                   !execution.IsCompleted &&
                   frames++ < 600)
            {
                yield return null;
            }

            var formalScene = SceneManager.GetSceneByName(
                CareerFormalSixVsSixMatchRunnerV4.FormalSceneName);
            Assert.That(formalScene.isLoaded, Is.True);
            while (SceneManager.GetActiveScene().handle != formalScene.handle &&
                   !execution.IsCompleted &&
                   frames++ < 600)
            {
                yield return null;
            }
            var director = Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(director.MatchContext, Is.SameAs(context));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(formalScene.handle));

            cancellation.Cancel();
            while (!execution.IsCompleted && frames++ < 1200)
            {
                yield return null;
            }

            Assert.That(execution.IsCanceled, Is.True);
            Assert.That(SceneManager.GetSceneByName(
                CareerFormalSixVsSixMatchRunnerV4.FormalSceneName).isLoaded, Is.False);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(careerScene.handle));
            Assert.That(FormalMatchContextStartupV4.CancelPendingFormalStart(context.SessionId), Is.False);
            Object.Destroy(host);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Runner_CancellationDuringSceneLoadWaitsForAndUnloadsTheScene()
        {
            var host = new GameObject("CareerFormalRunnerEarlyCancellationTestHost");
            var runner = host.AddComponent<CareerFormalSixVsSixMatchRunnerV4>();
            var context = FormalSixVsSixRallyBootstrap.CreateDefaultFormalContext();
            using var cancellation = new CancellationTokenSource();
            var execution = runner.ExecuteAsync(context, cancellation.Token);
            cancellation.Cancel();

            var frames = 0;
            while (!execution.IsCompleted && frames++ < 1200)
            {
                yield return null;
            }

            Assert.That(execution.IsCanceled, Is.True);
            Assert.That(SceneManager.GetSceneByName(
                CareerFormalSixVsSixMatchRunnerV4.FormalSceneName).isLoaded, Is.False);
            Assert.That(FormalMatchContextStartupV4.CancelPendingFormalStart(context.SessionId), Is.False);
            Object.Destroy(host);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Runner_InvalidSceneFailsWithoutLeavingAStartupContext()
        {
            var host = new GameObject("CareerFormalRunnerMissingSceneTestHost");
            var runner = host.AddComponent<CareerFormalSixVsSixMatchRunnerV4>();
            var document = host.AddComponent<UnityEngine.UIElements.UIDocument>();
            var inputRouter = host.AddComponent<CareerMenuInputRouter>();
            runner.Initialize(document, inputRouter, "MissingFormalIndoor6v6Scene");
            var context = FormalSixVsSixRallyBootstrap.CreateDefaultFormalContext();
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var execution = runner.ExecuteAsync(context, CancellationToken.None);
                var frames = 0;
                while (!execution.IsCompleted && frames++ < 600)
                {
                    yield return null;
                }

                Assert.That(execution.IsFaulted, Is.True);
                Assert.That(SceneManager.GetSceneByName(
                    "MissingFormalIndoor6v6Scene").isLoaded, Is.False);
                Assert.That(FormalMatchContextStartupV4.CancelPendingFormalStart(context.SessionId), Is.False);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                Object.Destroy(host);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Runner_CompletesPhysicalSixVsSixWithContextBoundResultAndReplay()
        {
            var host = new GameObject("CareerFormalRunnerCompletionTestHost");
            var runner = host.AddComponent<CareerFormalSixVsSixMatchRunnerV4>();
            var executor = new CareerMatchExecutorV4(
                runner,
                CreateFormalMapper());
            var persistedContext = executor.Encode(CreateDirectLaunch());
            using var cancellation = new CancellationTokenSource();
            var originalTimeScale = Time.timeScale;
            Time.timeScale = 4f;
            var execution = executor.ExecuteAsync(persistedContext, cancellation.Token);

            var frames = 0;
            while (!execution.IsCompleted && frames++ < 6500)
            {
                yield return null;
            }

            Time.timeScale = originalTimeScale;
            if (!execution.IsCompleted)
            {
                cancellation.Cancel();
                while (!execution.IsCompleted && frames++ < 7200)
                {
                    yield return null;
                }

                Assert.Fail("The physical 6v6 scene did not complete within the test budget.");
            }

            Assert.That(execution.IsCanceled, Is.False);
            Assert.That(execution.IsFaulted, Is.False);
            var outcome = execution.GetAwaiter().GetResult();
            Assert.That(outcome.Context.SessionId, Is.EqualTo(persistedContext.SessionId));
            Assert.That(outcome.Context.ContextDigest, Is.EqualTo(persistedContext.ContextDigest));
            Assert.That(outcome.ResultDigest.Value, Has.Length.EqualTo(64));
            Assert.That(outcome.Facts.PlayerFacts.Count, Is.EqualTo(12));
            Assert.That(outcome.Facts.PlayerFacts.All(fact =>
                fact.Spike.Attempts == 0 && fact.Serve.Attempts == 0 &&
                fact.Reception.Attempts == 0 && fact.Defense.Attempts == 0 &&
                fact.Block.Attempts == 0 && fact.Stability.CriticalActions == 0), Is.True);
            Assert.That(SceneManager.GetSceneByName(
                CareerFormalSixVsSixMatchRunnerV4.FormalSceneName).isLoaded, Is.False);
            Object.Destroy(host);
        }

        private static CareerMatchV4Mapper CreateFormalMapper()
        {
            return new CareerMatchV4Mapper(new CareerMatchV4RuntimeConfiguration(
                FormalSixVsSixRallyBootstrap.FormalPhysicsConfigurationHash,
                FormalSixVsSixRallyBootstrap
                    .CreateFormalTrajectoryPredictionProviderConfiguration(),
                CareerMatchV4FactPolicy.DirectAggregateOnly));
        }

        private static CareerMatchLaunch CreateDirectLaunch()
        {
            return new CareerFirstMatchLaunchFactoryV1(
                CareerMatchExecutionMode.Direct).Create(
                new CareerFirstMatchLaunchRequest(
                    new CareerMatchVersions(4, 1, 1, 1, null, null),
                    Guid.Parse("a8fe2f4a-5a04-4bea-8668-c0f54aec93cb"),
                    192837u,
                    new TeamId("team.career.runner"),
                    new PlayerId("player.career.runner"),
                    8,
                    0,
                    CreateAttributes(),
                    CareerMatchPriority.AttackFirst));
        }

        private static CareerPlayerAttributes CreateAttributes()
        {
            return new CareerPlayerAttributes(
                Progress(7100, 101), Progress(6200, 202), Progress(7300, 303),
                Progress(6400, 404), Progress(7500, 505), Progress(6600, 606),
                Progress(7700, 707), Progress(6800, 808));
        }

        private static CareerAttributeProgress Progress(int ability, long growth)
        {
            return new CareerAttributeProgress(ability, growth);
        }
    }
}
