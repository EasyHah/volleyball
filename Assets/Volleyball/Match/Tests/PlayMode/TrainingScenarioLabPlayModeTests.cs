using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.PlayModeTests
{
    public sealed class TrainingScenarioLabPlayModeTests
    {
        private const string SceneName = "FormalTrainingScenarioLab";

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Scene_StartsAsOneEditablePreviewWithoutFormalAuthority()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<
                TrainingScenarioLabView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(
                view.Controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Editing));
            Assert.That(
                Object.FindObjectsByType<TrainingScenarioLabView>(
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<FormalSixVsSixRallyDirector>(
                    FindObjectsSortMode.None),
                Is.Empty);
            Assert.That(
                GameObject.Find("TrainingPreviewRootV1"),
                Is.Not.Null);
            var markers = Object.FindObjectsByType<
                TrainingLabPreviewMarkerV1>(
                FindObjectsSortMode.None);
            Assert.That(markers, Has.Length.EqualTo(13));
            Assert.That(
                markers.Select(value => value.ObjectId).Distinct().Count(),
                Is.EqualTo(13));

            view.Controller.SetBallPosition(
                new SimVector3(float.NaN, 2f, 0f));
            yield return null;
            var run = view.GetComponent<UIDocument>()
                .rootVisualElement.Q<Button>("run-button");
            Assert.That(run.enabledSelf, Is.False);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ContextualInspector_ChangesWithEachWorkbenchStepAt1920x1080()
        {
            var originalWidth = Screen.width;
            var originalHeight = Screen.height;
            Screen.SetResolution(1920, 1080, false);
            try
            {
                yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
                yield return null;

                var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
                var controller = view.Controller;
                var root = view.GetComponent<UIDocument>().rootVisualElement;
                view.ShowWorkbench(controller.SelectedEntryKey);
                yield return null;
                var scroll = root.Q<ScrollView>("context-scroll");

                Assert.That(scroll, Is.Not.Null);
                controller.ReopenRotation();
                yield return null;
                var inspector = root.Q<VisualElement>("contextual-inspector");
                Assert.That(scroll.resolvedStyle.height, Is.GreaterThan(0f));
                Assert.That(scroll.resolvedStyle.height,
                    Is.LessThanOrEqualTo(inspector.resolvedStyle.height));
                Assert.That(root.Q("context-rotation").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("context-positioning").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.None));

                controller.ConfirmRotation();
                yield return null;
                Assert.That(root.Q("context-positioning").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("context-rotation").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.None));

                controller.SelectServeTool(TrainingServeToolV1.MoveBall);
                yield return null;
                Assert.That(root.Q("context-serve-ball").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));

                controller.GoToValidation();
                yield return null;
                Assert.That(root.Q("context-validation").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));

                Assert.That(controller.Run(), Is.True);
                yield return null;
                Assert.That(root.Q("context-running").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("timeline-list"), Is.Not.Null);
            }
            finally
            {
                Screen.SetResolution(originalWidth, originalHeight, false);
            }
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator WorkbenchRegions_DoNotOverlapAt1920x1080()
        {
            var originalWidth = Screen.width;
            var originalHeight = Screen.height;
            Screen.SetResolution(1920, 1080, false);
            try
            {
                yield return SceneManager.LoadSceneAsync(
                    SceneName, LoadSceneMode.Single);
                yield return null;
                var view = Object.FindFirstObjectByType<
                    TrainingScenarioLabView>();
                var root = view.GetComponent<UIDocument>()
                    .rootVisualElement;
                Assert.That(root.Q("scenario-hub").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("workbench-shell").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q("standard-scenarios").childCount,
                    Is.GreaterThan(0));

                view.ShowWorkbench(view.Controller.SelectedEntryKey);
                yield return null;
                yield return null;
                Assert.That(root.Q("scenario-hub").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.None));
                Assert.That(root.Q("workbench-shell").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));

                var board = root.Q<VisualElement>("world-viewport");
                var inspector = root.Q<VisualElement>(
                    "contextual-inspector");
                var actions = root.Q<VisualElement>("bottom-action-bar");
                var context = root.Q<ScrollView>("context-scroll");

                Assert.That(board.worldBound.width, Is.GreaterThan(0f));
                Assert.That(board.worldBound.height, Is.GreaterThan(0f));
                Assert.That(inspector.worldBound.width, Is.GreaterThan(0f));
                Assert.That(inspector.worldBound.height, Is.GreaterThan(0f));
                Assert.That(actions.worldBound.width, Is.GreaterThan(0f));
                Assert.That(actions.worldBound.height, Is.GreaterThan(0f));
                Assert.That(board.worldBound.Overlaps(inspector.worldBound),
                    Is.False);
                Assert.That(board.worldBound.Overlaps(actions.worldBound),
                    Is.False);
                Assert.That(inspector.worldBound.Overlaps(actions.worldBound),
                    Is.False);
                Assert.That(inspector.worldBound.Contains(
                    context.worldBound.min), Is.True);
                Assert.That(inspector.worldBound.Contains(
                    context.worldBound.max), Is.True);
            }
            finally
            {
                Screen.SetResolution(originalWidth, originalHeight, false);
            }
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator RunPauseStepResetAndRerun_KeepOneFormalWorld()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            var view = Object.FindFirstObjectByType<
                TrainingScenarioLabView>();
            var controller = view.Controller;
            controller.SelectDraftEntry(
                "builtin:" +
                TrainingScenarioCatalogV1.SecondTouchNetOwnSide);
            Assert.That(controller.Run(), Is.True);
            yield return null;

            Assert.That(
                GameObject.Find("TrainingPreviewRootV1"),
                Is.Null);
            AssertFormalWorld();
            Assert.That(
                () => controller.SetBallPosition(SimVector3.Zero),
                Throws.InvalidOperationException);

            var director = Object.FindFirstObjectByType<
                FormalSixVsSixRallyDirector>();
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!director.IsLoopRunning &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(director.IsLoopRunning, Is.True);
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var beforePause = ball.SimulationTime;
            SendKey(view, KeyCode.P, 'p');
            Assert.That(
                controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Paused));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(ball.SimulationTime, Is.EqualTo(beforePause));

            SendKey(view, KeyCode.Period, '.');
            Assert.That(
                ball.SimulationTime - beforePause,
                Is.EqualTo(SimulatedBall.DefaultFixedStep).Within(.00001f));
            SendKey(view, KeyCode.P, 'p');
            Assert.That(
                controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Running));
            yield return WaitForCompletion(controller, 12f);
            Assert.That(controller.LastEvidence, Is.Not.Null);

            controller.RerunSameSeed();
            yield return WaitForCompletion(controller, 12f);
            Assert.That(
                controller.RunComparisonSummary,
                Is.EqualTo("同 seed 双跑一致"));

            controller.ReturnToEditing();
            yield return null;
            Assert.That(
                Object.FindObjectsByType<FormalSixVsSixRallyDirector>(
                    FindObjectsSortMode.None),
                Is.Empty);
            Assert.That(
                GameObject.Find("TrainingPreviewRootV1"),
                Is.Not.Null);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
        }

        private static IEnumerator WaitForCompletion(
            TrainingScenarioLabController controller,
            float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while (controller.State !=
                       TrainingScenarioLabStateV1.Completed &&
                   controller.State !=
                       TrainingScenarioLabStateV1.Faulted &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(
                controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Completed));
        }


        private static void AssertFormalWorld()
        {
            Assert.That(
                Object.FindObjectsByType<FormalSixVsSixRallyDirector>(
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<SimulatedBall>(
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<PrototypePlayerAgent>(
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(12));
        }

        private static void SendKey(
            TrainingScenarioLabView view,
            KeyCode key,
            char character)
        {
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            using var value = KeyDownEvent.GetPooled(
                character,
                key,
                EventModifiers.None);
            value.target = root;
            root.SendEvent(value);
        }
    }
}
