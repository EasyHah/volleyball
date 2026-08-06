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
        public IEnumerator AttributeHeightInput_IgnoresIntermediateInvalidValueThenSavesValidOverride()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<
                TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            var height = root.Q<VisualElement>("attribute-table")
                .Query<IntegerField>()
                .First();
            var player = view.Controller.Draft.Context.Home.Players.First();

            Assert.That(() => height.value = 1, Throws.Nothing);
            Assert.That(view.Controller.Draft.AttributeOverrides, Is.Empty);

            height.value = 1981;

            Assert.That(
                view.Controller.Draft.AttributeOverrides[player.PlayerId]
                    .HeightMillimeters,
                Is.EqualTo(1981));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PreviewPlayers_ShowRoleAndLockedSlotAboveTheirHeads()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<
                TrainingScenarioLabView>();
            var player = view.Controller.Draft.Players.First();
            var marker = Object.FindObjectsByType<
                    TrainingLabPreviewMarkerV1>(FindObjectsSortMode.None)
                .Single(value => value.ObjectId == player.PlayerId.Value);
            var label = marker.GetComponentInChildren<TextMesh>();

            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Does.Contain("号位"));
            Assert.That(label.text, Does.Not.Contain("待锁定"));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PrecisionAdjustment_OpensAndPreservesSelectedObject()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<
                TrainingScenarioLabView>();
            var selected = view.Controller.Draft.Players.First().PlayerId.Value;
            view.Controller.SelectObject(selected);
            view.OpenPrecisionAdjustment();

            Assert.That(view.IsPrecisionAdjustmentOpen, Is.True);
            Assert.That(view.Controller.SelectedObjectId, Is.EqualTo(selected));
            Assert.That(view.PrecisionVectorMode,
                Is.EqualTo(TrainingLabPrecisionVectorModeV1.Position));

            view.ReturnToTacticalBoard();

            Assert.That(view.IsPrecisionAdjustmentOpen, Is.False);
            Assert.That(view.Controller.SelectedObjectId, Is.EqualTo(selected));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PrecisionAdjustment_EditsVisibleAxesAndPreservesHiddenAxis()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<
                TrainingScenarioLabView>();
            view.Controller.SelectObject("ball");
            view.OpenPrecisionAdjustment();
            view.SetPrecisionVectorMode(
                TrainingLabPrecisionVectorModeV1.Velocity);
            var velocity = view.Controller.Draft.BallVelocity;

            view.ApplyPrecisionAdjustment(
                TrainingLabPrecisionPlaneV1.XY, 4f, 5f);

            Assert.That(view.Controller.Draft.BallVelocity,
                Is.EqualTo(new SimVector3(4f, 5f, velocity.Z)));

            var player = view.Controller.Draft.Players.First();
            view.Controller.SelectObject(player.PlayerId.Value);
            var position = player.Position;
            view.ApplyPrecisionAdjustment(
                TrainingLabPrecisionPlaneV1.XZ, 2f, -3f);

            Assert.That(player.Position,
                Is.EqualTo(new SimVector3(2f, position.Y, -3f)));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator FreeObservation_RendersReadOnlyCameraOutput()
        {
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<
                TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            var selected = view.Controller.Draft.Players.First();
            var selectedPosition = selected.Position;
            var ballPosition = view.Controller.Draft.BallPosition;
            var ballVelocity = view.Controller.Draft.BallVelocity;
            view.Controller.SelectObject(selected.PlayerId.Value);

            view.OpenFreeObservation();
            yield return null;

            Assert.That(root.Q<VisualElement>("free-observation")
                .ClassListContains("is-hidden"), Is.False);
            Assert.That(view.IsFreeObservationOpen, Is.True);
            Assert.That(view.ObservationOutputReady, Is.True);
            Assert.That(view.Controller.SelectedObjectId,
                Is.EqualTo(selected.PlayerId.Value));
            Assert.That(selected.Position, Is.EqualTo(selectedPosition));
            Assert.That(view.Controller.Draft.BallPosition,
                Is.EqualTo(ballPosition));
            Assert.That(view.Controller.Draft.BallVelocity,
                Is.EqualTo(ballVelocity));
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
