using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Presentation;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.PlayModeTests
{
    public sealed class TrainingScenarioLabPlayModeTests
    {
        private const string SceneName = "FormalTrainingScenarioLab";

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Scene_StartsInV5ScenarioHubWithoutLegacyRuntime()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(view.Controller, Is.Null);
            Assert.That(view.V5Controller, Is.Not.Null);
            Assert.That(view.V5Controller.MatchSetup.BaseContext.ContractVersion,
                Is.EqualTo(5));
            Assert.That(root.Q("scenario-hub").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q("standard-scenarios").childCount,
                Is.EqualTo(6));
            Assert.That(Object.FindObjectsByType<FormalSixVsSixRallyDirector>(
                FindObjectsSortMode.None), Is.Empty);
            Assert.That(GameObject.Find("TrainingWorldHostV1"), Is.Null);
            Assert.That(root.Q<Button>("validate-button"), Is.Null);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator NativeV5Workbench_AutoPreflightsAndKeepsRegionsSeparate()
        {
            var originalWidth = Screen.width;
            var originalHeight = Screen.height;
            Screen.SetResolution(1920, 1080, false);
            try
            {
                yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
                yield return null;
                var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
                var controller = view.V5Controller;
                var root = view.GetComponent<UIDocument>().rootVisualElement;
                view.ShowWorkbench("builtin:standard-rotation");
                yield return null;

                Assert.That(root.Q("rotation-home-grid").childCount,
                    Is.EqualTo(6));
                Assert.That(root.Q("rotation-away-grid").childCount,
                    Is.EqualTo(6));
                controller.ConfirmRotation();
                controller.ContinueToServeSetup();
                controller.SetServeTool(TrainingServeToolV1.ViewTrajectory);
                yield return null;
                Assert.That(root.Q("serve-top-trajectory-layer").childCount,
                    Is.GreaterThan(0));
                controller.SetServeView(TrainingServeViewV1.Side);
                yield return null;
                Assert.That(root.Q("serve-side-board").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(root.Q("serve-side-trajectory-layer").childCount,
                    Is.GreaterThan(0));
                var beforePreview = new MatchSetupEditorV1(
                    controller.MatchSetup).Freeze().SetupHash;
                view.OpenReadonly3dPreview();
                yield return null;
                Assert.That(root.Q("preview-3d-modal").resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.Flex));
                var preview = GameObject.Find(
                    "TrainingLabReadonly3DPreviewV5");
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.GetComponentsInChildren<Renderer>(),
                    Has.Length.GreaterThanOrEqualTo(13));
                Assert.That(preview.GetComponentInChildren<Camera>()
                    .targetTexture, Is.Not.Null);
                view.CloseReadonly3dPreview();
                Assert.That(new MatchSetupEditorV1(controller.MatchSetup)
                    .Freeze().SetupHash, Is.EqualTo(beforePreview));
                Assert.That(controller.EnterPreflight(), Is.True);
                yield return null;
                Assert.That(root.Q<Label>("hash-label").text,
                    Does.Contain(controller.PreflightSnapshot.SetupHash
                        .Substring(0, 16)));

                var board = root.Q<VisualElement>("world-viewport");
                var inspector = root.Q<VisualElement>("contextual-inspector");
                var actions = root.Q<VisualElement>("bottom-action-bar");
                Assert.That(board.worldBound.width, Is.GreaterThan(0f));
                Assert.That(inspector.worldBound.width, Is.GreaterThan(0f));
                Assert.That(actions.worldBound.height, Is.GreaterThan(0f));
                Assert.That(board.worldBound.Overlaps(inspector.worldBound),
                    Is.False);
                Assert.That(board.worldBound.Overlaps(actions.worldBound),
                    Is.False);
                Assert.That(inspector.worldBound.Overlaps(actions.worldBound),
                    Is.False);
            }
            finally
            {
                Screen.SetResolution(originalWidth, originalHeight, false);
            }
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NativeV5Run_UsesExactSnapshotAndStopsWithTrainingOutcome()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
            var controller = view.V5Controller;
            view.ShowWorkbench("builtin:standard-rotation");
            controller.ConfirmRotation();
            controller.ContinueToServeSetup();
            Assert.That(controller.EnterPreflight(), Is.True);
            var snapshot = controller.PreflightSnapshot;

            Assert.That(controller.Run(), Is.True);
            Assert.That(controller.RunSnapshot, Is.SameAs(snapshot));
            Assert.That(view.GetComponent<UIDocument>().rootVisualElement
                .Q<Button>("return-to-hub-button").enabledSelf, Is.False);
            var deadline = Time.realtimeSinceStartup + 20f;
            while (controller.State != TrainingScenarioLabStateV1.Completed &&
                   controller.State != TrainingScenarioLabStateV1.Faulted &&
                   Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();

            Assert.That(controller.State,
                Is.EqualTo(TrainingScenarioLabStateV1.Completed));
            Assert.That(controller.Outcome, Is.Not.Null);
            Assert.That(controller.Outcome.SetupHash,
                Is.EqualTo(snapshot.SetupHash));
            Assert.That(controller.Outcome.HomeScoreDelta +
                controller.Outcome.AwayScoreDelta, Is.EqualTo(1));
            var director = Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            Assert.That(director.MatchContextV5, Is.Not.Null);
            Assert.That(director.ResultV5, Is.Null);
            Assert.That(director.GetComponent<MatchReplayRecorderV5>(), Is.Null);
        }
    }
}
