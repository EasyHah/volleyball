using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Volleyball.Career.MatchIntegration;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Bootstrap
{
    public sealed class OfflineCareerMatchRunnerV4 : ICareerMatchRunnerV4
    {
        private static readonly TrajectoryPredictionProviderConfigurationV4
            FixtureTrajectoryConfiguration =
                new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    1,
                    CareerMatchV4Mapper.FixturePredictorConfigurationHash);

        private readonly DeterministicFixtureMatchRunnerV4 _fixtureRunner =
            new DeterministicFixtureMatchRunnerV4();
        private readonly FormalSixVsSixSceneRunnerV4 _physicalRunner;

        public OfflineCareerMatchRunnerV4(GameObject careerRoot)
        {
            _physicalRunner = new FormalSixVsSixSceneRunnerV4(careerRoot);
        }

        public Task<MatchResultV4> ExecuteAsync(
            MatchContextV4 context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.Equals(
                    context.PhysicsConfigurationHash,
                    CareerMatchV4Mapper.FixturePhysicsConfigurationHash,
                    StringComparison.Ordinal) &&
                context.TrajectoryPredictionProviderConfiguration.Equals(
                    FixtureTrajectoryConfiguration))
            {
                return _fixtureRunner.ExecuteAsync(context, cancellationToken);
            }

            if (string.Equals(
                    context.PhysicsConfigurationHash,
                    FormalSixVsSixRallyBootstrap.RuntimePhysicsConfigurationHash,
                    StringComparison.Ordinal))
            {
                return _physicalRunner.ExecuteAsync(context, cancellationToken);
            }

            throw new InvalidOperationException(
                "The persisted Match V4 context uses an unsupported offline runtime configuration.");
        }
    }

    public sealed class FormalSixVsSixSceneRunnerV4 : ICareerMatchRunnerV4
    {
        public const string SceneName = "FormalIndoor6v6";
        public const string ScenePath =
            "Assets/Volleyball/Match/Scenes/FormalIndoor6v6.unity";

        private readonly GameObject _careerRoot;
        private bool _running;

        public FormalSixVsSixSceneRunnerV4(GameObject careerRoot)
        {
            _careerRoot = careerRoot != null
                ? careerRoot
                : throw new ArgumentNullException(nameof(careerRoot));
        }

        public async Task<MatchResultV4> ExecuteAsync(
            MatchContextV4 context,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_running)
            {
                throw new InvalidOperationException(
                    "Only one formal 6v6 scene may run at a time.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var existing = SceneManager.GetSceneByName(SceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                throw new InvalidOperationException(
                    "The formal 6v6 scene is already loaded.");
            }

            _running = true;
            var careerScene = _careerRoot.scene;
            var previousActiveScene = SceneManager.GetActiveScene();
            var document = _careerRoot.GetComponent<UIDocument>();
            var inputRouter = _careerRoot.GetComponent<CareerMenuInputRouter>();
            var rootElement = document?.rootVisualElement;
            var previousDisplay = rootElement != null
                ? rootElement.style.display
                : default;
            var routerWasEnabled = inputRouter != null && inputRouter.enabled;
            var loadedScene = default(Scene);
            FormalSixVsSixRallyBootstrap matchBootstrap = null;
            Exception loadFailure = null;

            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (!string.Equals(scene.name, SceneName, StringComparison.Ordinal))
                {
                    return;
                }

                loadedScene = scene;
                try
                {
                    if (!SceneManager.SetActiveScene(scene))
                    {
                        throw new InvalidOperationException(
                            "The formal 6v6 scene could not become active.");
                    }

                    matchBootstrap = FindMatchBootstrap(scene);
                }
                catch (Exception exception)
                {
                    loadFailure = exception;
                }
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            try
            {
                FormalSixVsSixRallyBootstrap.QueueExternalContext(context);
                if (rootElement != null)
                {
                    rootElement.style.display = DisplayStyle.None;
                }

                if (inputRouter != null)
                {
                    inputRouter.enabled = false;
                }

                var loading = SceneManager.LoadSceneAsync(
                    SceneName,
                    LoadSceneMode.Additive);
                if (loading == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not start loading the formal 6v6 scene.");
                }

                while (!loading.isDone)
                {
                    await Task.Yield();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (loadFailure != null)
                {
                    throw new InvalidOperationException(
                        "The formal 6v6 scene rejected the Career context.",
                        loadFailure);
                }

                if (matchBootstrap == null)
                {
                    throw new InvalidOperationException(
                        "The formal 6v6 scene has no bootstrap.");
                }

                while (matchBootstrap.Director == null &&
                       matchBootstrap.InitializationException == null)
                {
                    ThrowIfPlayerCancelled(cancellationToken);
                    await Task.Yield();
                }

                if (matchBootstrap.InitializationException != null)
                {
                    throw new InvalidOperationException(
                        "The formal 6v6 runtime failed to initialize.",
                        matchBootstrap.InitializationException);
                }

                while (matchBootstrap.Director.Result == null)
                {
                    ThrowIfPlayerCancelled(cancellationToken);
                    await Task.Yield();
                }

                return NormalizePhysicalWorkload(
                    context,
                    matchBootstrap.Director.Result);
            }
            finally
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                FormalSixVsSixRallyBootstrap.ClearQueuedExternalContext(context);

                var restoreScene = careerScene.IsValid() && careerScene.isLoaded
                    ? careerScene
                    : previousActiveScene;
                if (restoreScene.IsValid() && restoreScene.isLoaded)
                {
                    SceneManager.SetActiveScene(restoreScene);
                }

                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    var unloading = SceneManager.UnloadSceneAsync(loadedScene);
                    if (unloading != null)
                    {
                        while (!unloading.isDone)
                        {
                            await Task.Yield();
                        }
                    }
                }

                if (rootElement != null)
                {
                    rootElement.style.display = previousDisplay;
                }

                if (inputRouter != null)
                {
                    inputRouter.enabled = routerWasEnabled;
                }

                _running = false;
            }
        }

        private static FormalSixVsSixRallyBootstrap FindMatchBootstrap(
            Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var bootstrap =
                    root.GetComponentInChildren<FormalSixVsSixRallyBootstrap>(true);
                if (bootstrap != null)
                {
                    return bootstrap;
                }
            }

            throw new InvalidOperationException(
                "The formal 6v6 scene has no FormalSixVsSixRallyBootstrap.");
        }

        private static void ThrowIfPlayerCancelled(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((Keyboard.current != null &&
                 Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null &&
                 Gamepad.current.buttonEast.wasPressedThisFrame))
            {
                throw new OperationCanceledException(
                    "The player returned to the pre-match screen.",
                    cancellationToken);
            }
        }

        private static MatchResultV4 NormalizePhysicalWorkload(
            MatchContextV4 context,
            MatchResultV4 result)
        {
            result.ValidateAgainst(context);
            var divisor = Math.Max(1, result.RalliesPlayed);
            var normalized = result.PlayerStats
                .Select(stats => new PlayerMatchStatsV4(
                    stats.PlayerId,
                    stats.Points,
                    stats.Contacts,
                    stats.Errors,
                    CareerMatchV4Mapper.NormalizeAccumulatedWorkload(
                        stats.Workload,
                        divisor)))
                .ToArray();
            return MatchResultV4.Create(
                context,
                result.WinnerTeamId,
                result.HomeScore,
                result.AwayScore,
                result.RalliesPlayed,
                result.AcceptedContacts,
                result.V3RuleTransitionCount,
                normalized);
        }
    }
}
