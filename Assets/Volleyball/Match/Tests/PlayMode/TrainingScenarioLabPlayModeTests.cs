using System.Collections;
using System.IO;
using System.Linq;
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
        public IEnumerator HubNewFromStandardButton_CreatesFreshZeroFaultStandardCopyAfterFaultTemplate()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            var faultName = TrainingScenarioCatalogV2.Create(
                "position-fault-home").DisplayName;
            Click(ScenarioButton(root, faultName));
            yield return null;

            var faultController = view.V5Controller;
            var faultLocalId = faultController.LocalScenario.LocalId;
            var faultSetup = faultController.MatchSetup;
            Assert.That(faultController.PositionFaults, Is.Not.Empty);
            Assert.That(faultController.PositionFaults.All(fault =>
                fault.Side == Volleyball.Shared.Contracts.TeamSide.Home),
                Is.True);

            Click(root.Q<Button>("return-to-hub-button"));
            yield return null;
            Assert.That(root.Q("unsaved-leave-modal").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));
            Click(root.Q<Button>("leave-discard-button"));
            yield return null;
            Assert.That(root.Q("scenario-hub").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));

            Click(root.Q<Button>("hub-new-from-standard-button"));
            yield return null;

            var standard = view.V5Controller;
            Assert.That(root.Q("scenario-hub").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q("workbench-shell").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.Flex));
            Assert.That(standard.LocalScenario.DisplayName, Is.EqualTo(
                TrainingScenarioCatalogV2.Create("standard-rotation")
                    .DisplayName));
            Assert.That(root.Q<Label>("scenario-name-label").text,
                Does.Contain("标准轮转"));
            Assert.That(standard.LocalScenario.LocalId,
                Is.Not.EqualTo(faultLocalId));
            Assert.That(standard.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.Rotation));
            Assert.That(standard.PositionFaults, Is.Empty);
            Assert.That(standard.MatchSetup, Is.Not.SameAs(faultSetup));
            Assert.That(standard.MatchSetup.HomeRotation,
                Is.Not.SameAs(faultSetup.HomeRotation));
            Assert.That(standard.MatchSetup.Players,
                Is.Not.SameAs(faultSetup.Players));
            Assert.That(() => view.ShowWorkbench("standard-rotation"),
                Throws.ArgumentException);
            Assert.That(() => view.ShowWorkbench("builtin:"),
                Throws.ArgumentException);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator WorkbenchEntries_AlwaysReplaceIdentityAndSavedLocalReloadsFromDisk()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            view.ShowWorkbench("builtin:home-serve");
            yield return null;
            var home = view.V5Controller;
            var homeSetup = home.MatchSetup;

            view.ShowWorkbench("builtin:away-serve");
            yield return null;
            var away = view.V5Controller;
            Assert.That(away, Is.Not.SameAs(home));
            Assert.That(away.LocalScenario.LocalId,
                Is.Not.EqualTo(home.LocalScenario.LocalId));
            Assert.That(away.MatchSetup, Is.Not.SameAs(homeSetup));

            view.ShowWorkbench("builtin:standard-rotation");
            yield return null;
            var firstStandard = view.V5Controller;
            var firstStandardId = firstStandard.LocalScenario.LocalId;
            var firstStandardSetup = firstStandard.MatchSetup;
            view.ShowWorkbench("builtin:standard-rotation");
            yield return null;
            var savedStandard = view.V5Controller;
            Assert.That(savedStandard.LocalScenario.LocalId,
                Is.Not.EqualTo(firstStandardId));
            Assert.That(savedStandard.MatchSetup,
                Is.Not.SameAs(firstStandardSetup));

            var localId = savedStandard.LocalScenario.LocalId;
            var savedSetup = savedStandard.MatchSetup;
            var savedHash = new MatchSetupEditorV1(savedSetup).Freeze().SetupHash;
            var path = Path.Combine(Application.persistentDataPath,
                "TrainingLab", "Scenarios", localId + ".json");
            try
            {
                Click(root.Q<Button>("save-button"));
                yield return null;
                Assert.That(File.Exists(path), Is.True);
                view.ShowWorkbench("builtin:home-serve");
                yield return null;
                view.ShowWorkbench("local:" + localId);
                yield return null;
                Assert.That(view.V5Controller.LocalScenario.LocalId,
                    Is.EqualTo(localId));
                Assert.That(view.V5Controller.MatchSetup,
                    Is.Not.SameAs(savedSetup));
                Assert.That(new MatchSetupEditorV1(view.V5Controller.MatchSetup)
                    .Freeze().SetupHash, Is.EqualTo(savedHash));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator RotationPointerDrag_OnlySameTeamDropChangesRenderedSlots()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("hub-new-from-standard-button"));
            yield return null;

            var controller = view.V5Controller;
            var homeGrid = root.Q("rotation-home-grid");
            var awayGrid = root.Q("rotation-away-grid");
            var homeBefore = controller.MatchSetup.HomeRotation.ToArray();
            var awayBefore = controller.MatchSetup.AwayRotation.ToArray();
            Drag(homeGrid.Q("rotation-home-slot-1"),
                homeGrid.Q("rotation-home-slot-4"));
            yield return null;

            Assert.That(controller.MatchSetup.HomeRotation[0],
                Is.EqualTo(homeBefore[3]));
            Assert.That(controller.MatchSetup.HomeRotation[3],
                Is.EqualTo(homeBefore[0]));
            Assert.That(homeGrid.Q("rotation-home-slot-1").Q<Label>().text,
                Does.Contain("1号位"));
            var movedPlayerName = controller.MatchSetup.BaseContext.Home
                .RotationOrder.Single(player =>
                    player.PlayerId.Equals(homeBefore[3])).DisplayName;
            Assert.That(homeGrid.Q("rotation-home-slot-1")
                    .Query<Label>().ToList()[1].text,
                Does.Contain(movedPlayerName));

            var homeAfterSwap = controller.MatchSetup.HomeRotation.ToArray();
            Drag(homeGrid.Q("rotation-home-slot-1"),
                awayGrid.Q("rotation-away-slot-1"));
            yield return null;
            Assert.That(controller.MatchSetup.HomeRotation,
                Is.EqualTo(homeAfterSwap));
            Assert.That(controller.MatchSetup.AwayRotation,
                Is.EqualTo(awayBefore));

            SendPointerDown(homeGrid.Q("rotation-home-slot-1"));
            var source = homeGrid.Q("rotation-home-slot-1");
            SendPointerMove(source, root.Q("rotation-board").worldBound.center);
            SendPointerUp(source, root.Q("rotation-board").worldBound.center);
            SendPointerUp(homeGrid.Q("rotation-home-slot-2"));
            yield return null;
            Assert.That(controller.MatchSetup.HomeRotation,
                Is.EqualTo(homeAfterSwap),
                "A blank/outside drop must clear the pending drag source.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator RotationReopenExchangeAndRelock_PreservesCoordinatesAndRecomputesFaults()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("hub-new-from-standard-button"));
            yield return null;
            Click(root.Q<Button>("confirm-rotation-button"));
            yield return null;
            var controller = view.V5Controller;
            var positionsBefore = controller.MatchSetup.Players.ToDictionary(
                player => player.PlayerId, player => player.Position);

            Click(root.Q<Button>("step-rotation"));
            yield return null;
            Assert.That(controller.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.Rotation));
            Drag(root.Q("rotation-home-slot-1"),
                root.Q("rotation-home-slot-4"));
            yield return null;
            Click(root.Q<Button>("confirm-rotation-button"));
            yield return null;

            Assert.That(controller.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.Positioning));
            Assert.That(controller.MatchSetup.Players.All(player =>
                positionsBefore[player.PlayerId].Equals(player.Position)),
                Is.True);
            Assert.That(controller.PositionFaults, Is.Not.Empty,
                "Relocking must recompute relations using the exchanged slots.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PositioningPointerFeedback_ExposesPlayerRulersAndFaultFocus()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("hub-new-from-standard-button"));
            yield return null;
            Click(root.Q<Button>("confirm-rotation-button"));
            yield return null;

            Assert.That(view.V5Controller.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.Positioning));
            Assert.That(view.V5Controller.SelectedObjectId,
                Is.Not.EqualTo("ball"));
            Assert.That(root.Q("horizontal-ruler")
                .Query(className: "selected-ruler-point").ToList(),
                Has.Count.EqualTo(1));
            Assert.That(root.Q("vertical-ruler")
                .Query(className: "selected-ruler-point").ToList(),
                Has.Count.EqualTo(1));
            var selectedPlayer = new Volleyball.Shared.Contracts.PlayerId(
                view.V5Controller.SelectedObjectId);
            var court = root.Q("court-surface");
            var courtWidth = court.resolvedStyle.width;
            var courtHeight = court.resolvedStyle.height;
            var courtLocal = new Vector2(courtWidth * .37f,
                courtHeight * .37f);
            var expectedCourtPosition = TrainingLabCourtProjectionV1
                .BoardToPlayerPosition(new Rect(0f, 0f,
                        courtWidth, courtHeight),
                    courtLocal, Volleyball.Shared.Contracts.TeamSide.Home);
            SendPointerDown(root.Q("tactical-token-layer")
                .Q(className: "selected-token"));
            SendPointerMove(root.Q("tactical-board"),
                court.LocalToWorld(courtLocal));
            SendPointerUp(root.Q("tactical-board"),
                court.LocalToWorld(courtLocal));
            yield return null;
            var actualCourtPosition = view.V5Controller.MatchSetup.Players
                .Single(player => player.PlayerId.Equals(selectedPlayer))
                .Position;
            Assert.That(actualCourtPosition.X,
                Is.EqualTo(expectedCourtPosition.X).Within(.001f));
            Assert.That(actualCourtPosition.Y,
                Is.EqualTo(expectedCourtPosition.Y).Within(.001f));
            Assert.That(actualCourtPosition.Z,
                Is.EqualTo(expectedCourtPosition.Z).Within(.001f));

            Click(root.Q<Button>("return-to-hub-button"));
            yield return null;
            Click(root.Q<Button>("leave-discard-button"));
            yield return null;
            Click(ScenarioButton(root, TrainingScenarioCatalogV2.Create(
                "position-fault-home").DisplayName));
            yield return null;
            Click(root.Q<Button>("confirm-rotation-button"));
            yield return null;

            var controller = view.V5Controller;
            var firstFault = controller.PositionFaults[0];
            var faultCard = root.Q("position-fault-summary")
                .Q<Button>(className: "position-fault-card");
            Assert.That(faultCard, Is.Not.Null);
            var requiredName = controller.MatchSetup.BaseContext.Home
                .RotationOrder.Concat(controller.MatchSetup.BaseContext.Away
                    .RotationOrder).Single(player => player.PlayerId.Equals(
                    firstFault.RequiredAheadOrLeft.PlayerId)).DisplayName;
            var violatingName = controller.MatchSetup.BaseContext.Home
                .RotationOrder.Concat(controller.MatchSetup.BaseContext.Away
                    .RotationOrder).Single(player => player.PlayerId.Equals(
                    firstFault.ViolatingBehindOrRight.PlayerId)).DisplayName;
            Assert.That(faultCard.text,
                Does.Contain(requiredName));
            Assert.That(faultCard.text,
                Does.Contain(violatingName));
            Assert.That(faultCard.text,
                Does.Contain(ExpectedCorrectionDirection(firstFault)));
            Assert.That(root.Q("position-fault-layer")
                .Query(className: "fault-relation").ToList(),
                Is.Not.Empty);
            Assert.That(root.Q("position-fault-layer")
                .Query(className: "fault-arrow").ToList(),
                Is.Not.Empty);

            Click(faultCard);
            yield return null;
            Assert.That(controller.FocusedPlayerIds, Is.EquivalentTo(new[]
            {
                firstFault.RequiredAheadOrLeft.PlayerId,
                firstFault.ViolatingBehindOrRight.PlayerId
            }));
            Assert.That(root.Q("tactical-token-layer")
                .Query(className: "focused-fault-token").ToList(),
                Has.Count.EqualTo(2));

            Click(root.Q<Button>("return-to-hub-button"));
            yield return null;
            Click(root.Q<Button>("leave-discard-button"));
            yield return null;
            Click(root.Q<Button>("hub-new-from-standard-button"));
            yield return null;
            Click(root.Q<Button>("confirm-rotation-button"));
            yield return null;
            controller = view.V5Controller;
            var slot4 = controller.MatchSetup.HomeRotation[3];
            var slot3 = controller.MatchSetup.HomeRotation[2];
            var slot4Player = controller.MatchSetup.Players.Single(player =>
                player.PlayerId.Equals(slot4));
            var slot3Player = controller.MatchSetup.Players.Single(player =>
                player.PlayerId.Equals(slot3));
            var slot4Local = TrainingTeamCourtTransformV1.ToLocal(
                Volleyball.Shared.Contracts.TeamSide.Home,
                slot4Player.Position);
            var slot3Local = TrainingTeamCourtTransformV1.ToLocal(
                Volleyball.Shared.Contracts.TeamSide.Home,
                slot3Player.Position);
            controller.SetPlayerPosition(slot4,
                TrainingTeamCourtTransformV1.ToWorld(
                    Volleyball.Shared.Contracts.TeamSide.Home,
                    new Volleyball.Domain.Simulation.SimVector3(
                        slot3Local.X + 1f, slot4Local.Y, slot4Local.Z)));
            yield return null;
            var lateralFaultIndex = controller.PositionFaults.ToList()
                .FindIndex(fault => fault.Rule == Volleyball.Match.Domain
                    .FullRallyV3.PositionFaultRuleV1.Slot4RightOfSlot3);
            Assert.That(lateralFaultIndex, Is.GreaterThanOrEqualTo(0));
            var lateralCard = root.Q("position-fault-summary")
                .Query<Button>(className: "position-fault-card").ToList()[
                    lateralFaultIndex];
            Assert.That(lateralCard.text,
                Does.Contain(ExpectedCorrectionDirection(
                    controller.PositionFaults[lateralFaultIndex])));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PositioningRulerCorrection_EnablesAndClicksServe()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
            var root = view.GetComponent<UIDocument>().rootVisualElement;
            Click(ScenarioButton(root, TrainingScenarioCatalogV2.Create(
                "position-fault-home").DisplayName));
            yield return null;
            Click(root.Q<Button>("confirm-rotation-button"));
            yield return null;

            var controller = view.V5Controller;
            Assert.That(controller.PositionFaults, Has.Count.EqualTo(1));
            Click(root.Q("position-fault-summary")
                .Q<Button>(className: "position-fault-card"));
            yield return null;
            var fault = controller.PositionFaults[0];
            var correction = TrainingLabCourtProjectionV1
                .ShortestLegalCorrection(fault);
            var depthFault = fault.Rule is
                Volleyball.Match.Domain.FullRallyV3.PositionFaultRuleV1
                    .Slot4BehindSlot5 or
                Volleyball.Match.Domain.FullRallyV3.PositionFaultRuleV1
                    .Slot3BehindSlot6 or
                Volleyball.Match.Domain.FullRallyV3.PositionFaultRuleV1
                    .Slot2BehindSlot1;
            var ruler = root.Q(depthFault
                ? "horizontal-ruler" : "vertical-ruler");
            var point = ruler.Q(className: "selected-ruler-point");
            Assert.That(point, Is.Not.Null);
            var local = depthFault
                ? new Vector2(Mathf.InverseLerp(-8.7f, 8.7f,
                    correction.Z) * ruler.contentRect.width, 7f)
                : new Vector2(15f, (1f - Mathf.InverseLerp(-4.2f, 4.2f,
                    correction.X)) * ruler.contentRect.height);
            var destination = ruler.LocalToWorld(local);

            SendPointerDown(point);
            SendPointerMove(ruler, destination);
            SendPointerUp(ruler, destination);
            yield return null;

            Assert.That(controller.PositionFaults, Is.Empty);
            var next = root.Q<Button>("positioning-next-button");
            Assert.That(next.enabledSelf, Is.True);
            Click(next);
            yield return null;
            Assert.That(controller.CurrentStep,
                Is.EqualTo(TrainingLabStepV1.ServeBall));
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
                var root = view.GetComponent<UIDocument>().rootVisualElement;
                view.ShowWorkbench("builtin:standard-rotation");
                yield return null;
                var controller = view.V5Controller;

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
            view.ShowWorkbench("builtin:standard-rotation");
            var controller = view.V5Controller;
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

        private static Button ScenarioButton(VisualElement root,
            string displayName)
        {
            var card = root.Q("standard-scenarios").Children()
                .First(element => string.Equals(
                    element.Q<Label>()?.text, displayName,
                    System.StringComparison.Ordinal));
            return card.Q<Button>();
        }

        private static string ExpectedCorrectionDirection(
            Volleyball.Match.Domain.FullRallyV3.PositionFaultV1 fault)
        {
            var current = TrainingTeamCourtTransformV1.ToLocal(fault.Side,
                fault.ViolatingBehindOrRight.FootProjection);
            var target = TrainingTeamCourtTransformV1.ToLocal(fault.Side,
                TrainingLabCourtProjectionV1.ShortestLegalCorrection(fault));
            var deltaX = target.X - current.X;
            var deltaZ = target.Z - current.Z;
            return Mathf.Abs(deltaZ) >= Mathf.Abs(deltaX)
                ? deltaZ < 0f ? "向球网方向" : "向本方底线方向"
                : deltaX < 0f ? "向队伍局部左侧" : "向队伍局部右侧";
        }

        private static void Click(VisualElement target)
        {
            SendPointerDown(target);
            SendPointerUp(target);
        }

        private static void Drag(VisualElement source, VisualElement target)
        {
            SendPointerDown(source);
            SendPointerMove(source, target.worldBound.center);
            SendPointerUp(source, target.worldBound.center);
        }

        private static void SendPointerDown(VisualElement target)
        {
            using var evt = PointerDownEvent.GetPooled(new Event
            {
                type = EventType.MouseDown,
                button = 0,
                clickCount = 1,
                mousePosition = target.worldBound.center
            });
            target.SendEvent(evt);
        }

        private static void SendPointerUp(VisualElement target)
        {
            SendPointerUp(target, target.worldBound.center);
        }

        private static void SendPointerMove(VisualElement target,
            Vector2 position)
        {
            using var evt = PointerMoveEvent.GetPooled(new Event
            {
                type = EventType.MouseMove,
                button = 0,
                mousePosition = position
            });
            target.SendEvent(evt);
        }

        private static void SendPointerUp(VisualElement target,
            Vector2 position)
        {
            using var evt = PointerUpEvent.GetPooled(new Event
            {
                type = EventType.MouseUp,
                button = 0,
                clickCount = 1,
                mousePosition = position
            });
            target.SendEvent(evt);
        }
    }
}
