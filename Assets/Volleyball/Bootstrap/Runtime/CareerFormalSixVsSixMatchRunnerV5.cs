using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Bootstrap
{
    /// <summary>Result of a native V5 formal run; no V4 contract is involved.</summary>
    public sealed class CareerFormalMatchRunOutcomeV5
    {
        public CareerFormalMatchRunOutcomeV5(MatchResultV5 result, MatchReplayV5 replay)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Replay = replay ?? throw new ArgumentNullException(nameof(replay));
            if (result.SessionId != replay.Context.SessionId ||
                !string.Equals(result.ContextHash, replay.ContextHash, StringComparison.Ordinal))
                throw new ArgumentException("Formal V5 result and replay must share their context.");
        }

        public MatchResultV5 Result { get; }
        public MatchReplayV5 Replay { get; }
    }

    /// <summary>Bootstrap bridge for a persisted V5 context and the Formal 6v6 scene.</summary>
    [DisallowMultipleComponent]
    public sealed class CareerFormalSixVsSixMatchRunnerV5 : MonoBehaviour
    {
        private UIDocument _careerDocument;
        private CareerMenuInputRouter _menuInputRouter;
        private string _formalSceneName = CareerFormalSixVsSixMatchRunnerV4.FormalSceneName;
        private bool _executing;
        private CancellationTokenSource _activeCancellation;

        public void Initialize(UIDocument careerDocument, CareerMenuInputRouter menuInputRouter,
            string formalSceneName = CareerFormalSixVsSixMatchRunnerV4.FormalSceneName)
        {
            _careerDocument = careerDocument ?? throw new ArgumentNullException(nameof(careerDocument));
            _menuInputRouter = menuInputRouter ?? throw new ArgumentNullException(nameof(menuInputRouter));
            if (string.IsNullOrWhiteSpace(formalSceneName))
                throw new ArgumentException("A formal match scene name is required.", nameof(formalSceneName));
            _formalSceneName = formalSceneName;
        }

        public async Task<CareerFormalMatchRunOutcomeV5> ExecuteWithReplayAsync(
            MatchContextV5 context, CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_executing) throw new InvalidOperationException("Only one Career formal match may execute at a time.");

            _executing = true;
            var careerScene = gameObject.scene;
            var formalScene = default(Scene);
            var loadedFormalScene = false;
            AsyncOperation loadOperation = null;
            var documentWasEnabled = _careerDocument != null && _careerDocument.enabled;
            var menuWasEnabled = _menuInputRouter != null && _menuInputRouter.enabled;
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeCancellation = linkedCancellation;
            try
            {
                FormalMatchContextStartupV5.PrepareNextFormalStart(context);
                SetCareerPresentationEnabled(false, false);
                loadOperation = SceneManager.LoadSceneAsync(_formalSceneName, LoadSceneMode.Additive) ??
                    throw new InvalidOperationException(_formalSceneName + " scene could not be scheduled for loading.");
                await WaitForOperationAsync(loadOperation, CancellationToken.None);
                formalScene = SceneManager.GetSceneByName(_formalSceneName);
                if (!formalScene.IsValid() || !formalScene.isLoaded)
                    throw new InvalidOperationException(_formalSceneName + " scene did not finish loading.");
                loadedFormalScene = true;
                linkedCancellation.Token.ThrowIfCancellationRequested();
                if (!SceneManager.SetActiveScene(formalScene))
                    throw new InvalidOperationException(_formalSceneName + " could not become the active scene.");

                var director = FindRequired<FormalSixVsSixRallyDirector>(formalScene);
                if (!ReferenceEquals(director.MatchContextV5, context) ||
                    director.MatchContextV5.SessionId != context.SessionId ||
                    !string.Equals(director.MatchContextV5.ContextHash, context.ContextHash, StringComparison.Ordinal))
                    throw new ContractValidationException(_formalSceneName + " did not consume the persisted MatchContextV5.");

                var recorder = MatchReplayRecorderV5.Attach(director);
                while (director.ResultV5 == null)
                {
                    ThrowIfCancelledByPlayer(linkedCancellation);
                    linkedCancellation.Token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                director.ResultV5.ValidateAgainst(context);
                var replay = recorder.Complete();
                return new CareerFormalMatchRunOutcomeV5(director.ResultV5, replay);
            }
            finally
            {
                FormalMatchContextStartupV5.CancelPendingFormalStart(context.SessionId);
                if (loadOperation != null && !loadOperation.isDone)
                    await WaitForOperationAsync(loadOperation, CancellationToken.None);
                if (!loadedFormalScene)
                {
                    formalScene = SceneManager.GetSceneByName(_formalSceneName);
                    loadedFormalScene = formalScene.IsValid() && formalScene.isLoaded;
                }
                if (loadedFormalScene && formalScene.IsValid() && formalScene.isLoaded)
                    await WaitForOperationAsync(SceneManager.UnloadSceneAsync(formalScene), CancellationToken.None);
                if (careerScene.IsValid() && careerScene.isLoaded) SceneManager.SetActiveScene(careerScene);
                SetCareerPresentationEnabled(documentWasEnabled, menuWasEnabled);
                _activeCancellation = null;
                _executing = false;
            }
        }

        private void OnDestroy() => _activeCancellation?.Cancel();

        private void SetCareerPresentationEnabled(bool documentEnabled, bool menuEnabled)
        {
            if (_careerDocument != null) _careerDocument.enabled = documentEnabled;
            if (_menuInputRouter != null) _menuInputRouter.enabled = menuEnabled;
        }

        private static async Task WaitForOperationAsync(AsyncOperation operation, CancellationToken cancellationToken)
        {
            if (operation == null) throw new InvalidOperationException("Unity scene operation was not created.");
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private static void ThrowIfCancelledByPlayer(CancellationTokenSource cancellation)
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true) cancellation.Cancel();
        }

        private static T FindRequired<T>(Scene scene) where T : Component
        {
            var matches = new List<T>();
            foreach (var root in scene.GetRootGameObjects()) matches.AddRange(root.GetComponentsInChildren<T>(true));
            if (matches.Count != 1)
                throw new InvalidOperationException(scene.name + " must contain exactly one " + typeof(T).Name + ".");
            return matches[0];
        }
    }
}
