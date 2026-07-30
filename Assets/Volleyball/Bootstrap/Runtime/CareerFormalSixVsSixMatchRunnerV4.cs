using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Volleyball.Career.Application;
using Volleyball.Career.MatchIntegration;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Bootstrap
{
    public sealed class CareerOfflineMatchRouterV4 : ICareerMatchRunnerV4
    {
        private static readonly TrajectoryPredictionProviderConfigurationV4
            FixtureTrajectoryConfiguration =
                new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    1,
                    CareerMatchV4Mapper.FixturePredictorConfigurationHash);

        private static readonly TrajectoryPredictionProviderConfigurationV4
            FormalTrajectoryConfiguration =
                FormalSixVsSixRallyBootstrap
                    .CreateFormalTrajectoryPredictionProviderConfiguration();

        private readonly DeterministicFixtureMatchRunnerV4 _fixtureRunner =
            new DeterministicFixtureMatchRunnerV4();
        private readonly CareerFormalSixVsSixMatchRunnerV4 _formalRunner;

        public CareerOfflineMatchRouterV4(
            CareerFormalSixVsSixMatchRunnerV4 formalRunner)
        {
            _formalRunner = formalRunner ??
                throw new ArgumentNullException(nameof(formalRunner));
        }

        public Task<MatchResultV4> ExecuteAsync(
            MatchContextV4 context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (MatchesConfiguration(
                    context,
                    CareerMatchV4Mapper.FixturePhysicsConfigurationHash,
                    FixtureTrajectoryConfiguration))
            {
                return _fixtureRunner.ExecuteAsync(context, cancellationToken);
            }

            if (MatchesConfiguration(
                    context,
                    FormalSixVsSixRallyBootstrap.FormalPhysicsConfigurationHash,
                    FormalTrajectoryConfiguration))
            {
                return _formalRunner.ExecuteAsync(context, cancellationToken);
            }

            throw new InvalidOperationException(
                "The persisted Match V4 context uses an unsupported offline runtime configuration.");
        }

        private static bool MatchesConfiguration(
            MatchContextV4 context,
            string physicsConfigurationHash,
            TrajectoryPredictionProviderConfigurationV4
                trajectoryConfiguration)
        {
            return string.Equals(
                       context.PhysicsConfigurationHash,
                       physicsConfigurationHash,
                       StringComparison.Ordinal) &&
                   context.TrajectoryPredictionProviderConfiguration.Equals(
                       trajectoryConfiguration);
        }
    }

    public sealed class CareerFormalMatchRunOutcomeV4
    {
        public CareerFormalMatchRunOutcomeV4(
            MatchResultV4 result,
            MatchReplayV4 replay,
            MatchPerformanceReportV1 performanceReport)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Replay = replay ?? throw new ArgumentNullException(nameof(replay));
            PerformanceReport = performanceReport ??
                throw new ArgumentNullException(nameof(performanceReport));
            if (!string.Equals(
                    result.ContextHash,
                    replay.ContextHash,
                    StringComparison.Ordinal) ||
                result.SessionId != replay.Context.SessionId ||
                result.SessionId != performanceReport.SessionId ||
                !string.Equals(
                    result.ContextHash,
                    performanceReport.ContextHash,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    result.ResultHash,
                    performanceReport.ResultHash,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Formal result, replay and performance report must share one identity.");
            }

            performanceReport.ValidateAgainst(replay.Context, result);
        }

        public MatchResultV4 Result { get; }

        public MatchReplayV4 Replay { get; }

        public MatchPerformanceReportV1 PerformanceReport { get; }
    }

    /// <summary>
    /// Bootstrap-owned bridge from a persisted Career V4 context to the real
    /// formal 6v6 scene. Match remains unaware of Career and only consumes its
    /// native V4 startup context.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CareerFormalSixVsSixMatchRunnerV4 : MonoBehaviour,
        ICareerMatchRunnerV4
    {
        public const string FormalSceneName = "FormalIndoor6v6";
        public const string FormalScenePath =
            "Assets/Volleyball/Match/Scenes/FormalIndoor6v6.unity";

        private UIDocument _careerDocument;
        private CareerMenuInputRouter _menuInputRouter;
        private string _formalSceneName = FormalSceneName;
        private bool _executing;
        private CancellationTokenSource _activeCancellation;

        public void Initialize(
            UIDocument careerDocument,
            CareerMenuInputRouter menuInputRouter,
            string formalSceneName = FormalSceneName)
        {
            _careerDocument = careerDocument ??
                throw new ArgumentNullException(nameof(careerDocument));
            _menuInputRouter = menuInputRouter ??
                throw new ArgumentNullException(nameof(menuInputRouter));
            if (string.IsNullOrWhiteSpace(formalSceneName))
            {
                throw new ArgumentException(
                    "A formal match scene name is required.",
                    nameof(formalSceneName));
            }

            _formalSceneName = formalSceneName;
        }

        public async Task<MatchResultV4> ExecuteAsync(
            MatchContextV4 context,
            CancellationToken cancellationToken)
        {
            return (await ExecuteWithReplayAsync(context, cancellationToken)).Result;
        }

        public async Task<CareerFormalMatchRunOutcomeV4> ExecuteWithReplayAsync(
            MatchContextV4 context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_executing)
            {
                throw new InvalidOperationException(
                    "Only one Career formal match may execute at a time.");
            }

            _executing = true;
            var careerScene = gameObject.scene;
            var formalScene = default(Scene);
            var loadedFormalScene = false;
            AsyncOperation loadOperation = null;
            var documentWasEnabled = _careerDocument != null && _careerDocument.enabled;
            var menuWasEnabled = _menuInputRouter != null && _menuInputRouter.enabled;
            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeCancellation = linkedCancellation;
            try
            {
                FormalMatchContextStartupV4.PrepareNextFormalStart(context);
                SetCareerPresentationEnabled(false);
                loadOperation = SceneManager.LoadSceneAsync(
                    _formalSceneName,
                    LoadSceneMode.Additive);
                if (loadOperation == null)
                {
                    throw new InvalidOperationException(
                        _formalSceneName + " scene could not be scheduled for loading.");
                }

                // Unity cannot cancel a scheduled scene load. Finish it before
                // observing cancellation so the finally block can unload it.
                await WaitForOperationAsync(loadOperation, CancellationToken.None);
                formalScene = SceneManager.GetSceneByName(_formalSceneName);
                if (!formalScene.IsValid() || !formalScene.isLoaded)
                {
                    throw new InvalidOperationException(
                        _formalSceneName + " scene did not finish loading.");
                }

                loadedFormalScene = true;
                linkedCancellation.Token.ThrowIfCancellationRequested();
                if (!SceneManager.SetActiveScene(formalScene))
                {
                    throw new InvalidOperationException(
                        _formalSceneName + " could not become the active scene.");
                }

                var director = FindRequired<FormalSixVsSixRallyDirector>(formalScene);
                if (!ReferenceEquals(director.MatchContext, context) ||
                    director.MatchContext.SessionId != context.SessionId ||
                    !string.Equals(
                        director.MatchContext.ContextHash,
                        context.ContextHash,
                        StringComparison.Ordinal))
                {
                    throw new ContractValidationException(
                        _formalSceneName + " did not consume the persisted MatchContextV4.");
                }

                var recorder = MatchReplayRecorder.Attach(director);
                using var performance =
                    MatchPerformanceAccumulatorV1.Attach(director);
                performance.StartCapture();
                recorder.StartCapture();
                while (director.Result == null)
                {
                    ThrowIfCancelledByPlayer(linkedCancellation);
                    linkedCancellation.Token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                director.Result.ValidateAgainst(context);
                if (!recorder.IsComplete)
                {
                    throw new InvalidOperationException(
                        "Formal V4 replay did not complete before the match result.");
                }

                var replay = recorder.Complete();
                if (!string.Equals(
                        replay.ContextHash,
                        context.ContextHash,
                        StringComparison.Ordinal) ||
                    replay.Context.SessionId != context.SessionId)
                {
                    throw new ContractValidationException(
                        "Formal V4 replay does not belong to the persisted context.");
                }

                var performanceReport = performance.Complete(director.Result);
                return new CareerFormalMatchRunOutcomeV4(
                    director.Result,
                    replay,
                    performanceReport);
            }
            finally
            {
                FormalMatchContextStartupV4.CancelPendingFormalStart(context.SessionId);
                if (loadOperation != null && !loadOperation.isDone)
                {
                    await WaitForOperationAsync(loadOperation, CancellationToken.None);
                }

                if (!loadedFormalScene)
                {
                    formalScene = SceneManager.GetSceneByName(_formalSceneName);
                    loadedFormalScene = formalScene.IsValid() && formalScene.isLoaded;
                }
                if (loadedFormalScene && formalScene.IsValid() && formalScene.isLoaded)
                {
                    await WaitForOperationAsync(
                        SceneManager.UnloadSceneAsync(formalScene),
                        CancellationToken.None);
                }

                if (careerScene.IsValid() && careerScene.isLoaded)
                {
                    SceneManager.SetActiveScene(careerScene);
                }

                SetCareerPresentationEnabled(documentWasEnabled, menuWasEnabled);
                _activeCancellation = null;
                _executing = false;
            }
        }

        private void OnDestroy()
        {
            _activeCancellation?.Cancel();
        }

        private void SetCareerPresentationEnabled(bool enabled)
        {
            SetCareerPresentationEnabled(enabled, enabled);
        }

        private void SetCareerPresentationEnabled(
            bool documentEnabled,
            bool menuEnabled)
        {
            if (_careerDocument != null)
            {
                _careerDocument.enabled = documentEnabled;
            }

            if (_menuInputRouter != null)
            {
                _menuInputRouter.enabled = menuEnabled;
            }
        }

        private static async Task WaitForOperationAsync(
            AsyncOperation operation,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                throw new InvalidOperationException("Unity scene operation was not created.");
            }

            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private static void ThrowIfCancelledByPlayer(
            CancellationTokenSource cancellation)
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true)
            {
                cancellation.Cancel();
            }
        }

        private static T FindRequired<T>(Scene scene) where T : Component
        {
            var values = FindAll<T>(scene);
            if (values.Count != 1)
            {
                throw new InvalidOperationException(
                    scene.name + " must contain exactly one " + typeof(T).Name + ".");
            }

            return values[0];
        }

        private static List<T> FindAll<T>(Scene scene) where T : Component
        {
            var values = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                values.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return values;
        }
    }
}
