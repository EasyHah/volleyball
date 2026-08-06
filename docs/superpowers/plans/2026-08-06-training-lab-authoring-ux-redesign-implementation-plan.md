# TrainingLab Authoring UX Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Replace unreliable 3D-collider authoring with a gated 2D tactical board, three-view precision editing, and read-only free 3D observation.

**Architecture:** UI Toolkit owns authoring input. A pure geometry seam maps board-local points to the existing formal-court values. Presenters render tokens, position-fault relations, precision panes, and observation; \`TrainingScenarioLabController\` remains the sole draft mutation authority.

**Tech Stack:** Unity 6000.3.20f1, C#, UI Toolkit, NUnit EditMode/PlayMode, existing \`PositionFaultEvaluatorV1\`.

---

## Preconditions And File Map

Read \`docs/superpowers/specs/2026-08-06-training-lab-authoring-ux-redesign-design.md\` and \`docs/handoffs/active/2026-08-01-training-lineup-position-fault-v5-implementation.md\`. Do not change Match rules, frozen scenario data, V5 evidence, Career, or formal runner behavior. The worktree already has uncommitted TrainingLab changes; inspect each hunk and stage only task-owned files.

| Area | Files | Responsibility |
| --- | --- | --- |
| Geometry | Create \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabTacticalBoardGeometryV1.cs\` | Board/court mapping and axis preservation. |
| Gate | Create \`TrainingLabPositionFaultDiagnosticV1.cs\`; modify controller | Block Serve setup while preview faults exist and generate correction data. |
| Editor | Create tactical, precision, and observation presenters; modify view/UXML/USS | Present and operate each authoring mode. |
| Tests/docs | Add EditMode/PlayMode tests; modify change record/handoff after actual checks | Prevent raycast regressions and capture evidence. |

### Task 1: Add Pure Board Geometry

**Files:**
- Create: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabTacticalBoardGeometryV1.cs\`
- Create: \`Assets/Volleyball/Match/Tests/EditMode/TrainingLabTacticalBoardGeometryTests.cs\`

- [ ] **Step 1: Write failing coordinate and axis tests.**

\`\`\`csharp
[Test]
public void BoardToCourt_MapsEdgesToFormalPlayerBounds()
{
    var board = new Rect(100f, 40f, 900f, 600f);
    Assert.That(TrainingLabTacticalBoardGeometryV1.BoardToCourt(board, board.min, 0f),
        Is.EqualTo(new SimVector3(-4.25f, 0f, -8.75f)));
    Assert.That(TrainingLabTacticalBoardGeometryV1.BoardToCourt(board, board.max, 0f),
        Is.EqualTo(new SimVector3(4.25f, 0f, 8.75f)));
}

[Test]
public void ReplaceVisibleAxes_PreservesTheHiddenAxis()
{
    var source = new SimVector3(1f, 2f, 3f);
    Assert.That(TrainingLabTacticalBoardGeometryV1.ReplaceVisibleAxes(
        TrainingLabPrecisionPlaneV1.XY, source, 7f, 8f),
        Is.EqualTo(new SimVector3(7f, 8f, 3f)));
    Assert.That(TrainingLabTacticalBoardGeometryV1.ReplaceVisibleAxes(
        TrainingLabPrecisionPlaneV1.ZY, source, 7f, 8f),
        Is.EqualTo(new SimVector3(3f, 8f, 7f)));
    Assert.That(TrainingLabTacticalBoardGeometryV1.ReplaceVisibleAxes(
        TrainingLabPrecisionPlaneV1.XZ, source, 7f, 8f),
        Is.EqualTo(new SimVector3(7f, 2f, 8f)));
}
\`\`\`

- [ ] **Step 2: Run the test and confirm the types are absent.**

\`\`\`bash
UNITY_APP="/Applications/Unity/Unity-6000.3.20f1/Unity.app/Contents/MacOS/Unity"
"$UNITY_APP" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.TrainingLabTacticalBoardGeometryTests" -testResults "$PWD/TestResults/TrainingLabBoardGeometry.xml" -logFile "$PWD/TestResults/TrainingLabBoardGeometry.log"
\`\`\`

Expected: compilation failures naming \`TrainingLabTacticalBoardGeometryV1\` and \`TrainingLabPrecisionPlaneV1\`.

- [ ] **Step 3: Implement the pure seam.**

\`\`\`csharp
public enum TrainingLabPrecisionPlaneV1 { XY, ZY, XZ }

public static class TrainingLabTacticalBoardGeometryV1
{
    private const float PlayerXLimit =
        CourtBuilder.HalfWidth - PrototypePlayerAgent.BoundaryClearance;
    private const float PlayerZLimit =
        CourtBuilder.FormalHalfLength - PrototypePlayerAgent.BoundaryClearance;

    public static SimVector3 BoardToCourt(Rect board, Vector2 point, float y)
    {
        EnsureBoard(board);
        var x = Mathf.Lerp(-PlayerXLimit, PlayerXLimit,
            Mathf.Clamp01((point.x - board.xMin) / board.width));
        var z = Mathf.Lerp(-PlayerZLimit, PlayerZLimit,
            Mathf.Clamp01((board.yMax - point.y) / board.height));
        return new SimVector3(x, y, z);
    }

    public static Vector2 CourtToBoard(Rect board, SimVector3 point)
    {
        EnsureBoard(board);
        return new Vector2(
            Mathf.Lerp(board.xMin, board.xMax,
                Mathf.InverseLerp(-PlayerXLimit, PlayerXLimit, point.X)),
            Mathf.Lerp(board.yMax, board.yMin,
                Mathf.InverseLerp(-PlayerZLimit, PlayerZLimit, point.Z)));
    }

    public static SimVector3 ReplaceVisibleAxes(
        TrainingLabPrecisionPlaneV1 plane, SimVector3 current,
        float horizontal, float vertical)
    {
        return plane switch
        {
            TrainingLabPrecisionPlaneV1.XY =>
                new SimVector3(horizontal, vertical, current.Z),
            TrainingLabPrecisionPlaneV1.ZY =>
                new SimVector3(current.X, vertical, horizontal),
            TrainingLabPrecisionPlaneV1.XZ =>
                new SimVector3(horizontal, current.Y, vertical),
            _ => throw new ArgumentOutOfRangeException(nameof(plane))
        };
    }

    private static void EnsureBoard(Rect board)
    {
        if (board.width <= 0f || board.height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(board));
    }
}
\`\`\`

- [ ] **Step 4: Add clamping, round-trip, and invalid-rect tests; rerun Step 2.**

\`\`\`csharp
[Test]
public void BoardToCourt_ClampsOutsidePointer()
{
    var board = new Rect(0f, 0f, 100f, 100f);
    var result = TrainingLabTacticalBoardGeometryV1.BoardToCourt(
        board, new Vector2(-10f, 120f), 0f);

    Assert.That(result, Is.EqualTo(new SimVector3(-4.25f, 0f, -8.75f)));
}
\`\`\`

Expected: all geometry tests pass.

- [ ] **Step 5: Commit the seam.**

\`\`\`bash
git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabTacticalBoardGeometryV1.cs Assets/Volleyball/Match/Tests/EditMode/TrainingLabTacticalBoardGeometryTests.cs
git commit -m "feat: add training lab tactical board geometry"
\`\`\`

### Task 2: Gate Serve Setup And Create Correction Diagnostics

**Files:**
- Create: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabPositionFaultDiagnosticV1.cs\`
- Modify: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabController.cs\`
- Modify: \`Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs\`
- Create: \`Assets/Volleyball/Match/Tests/EditMode/TrainingLabPositionFaultDiagnosticTests.cs\`

- [ ] **Step 1: Write failing gate and correction tests.**

\`\`\`csharp
[Test]
public void SelectServeTool_RejectsAStillFaultedRotation()
{
    using var controller =
        new TrainingScenarioLabController(Store(), new FakeSimulation());
    controller.SetPlayerPosition(controller.Draft.HomeRotation[0],
        new SimVector3(2f, 0f, -7f));

    Assert.That(controller.CanEnterServeSetup, Is.False);
    Assert.That(() => controller.SelectServeTool(
        TrainingServeToolV1.MoveBall),
        Throws.InvalidOperationException.With.Message.Contains("position fault"));
}

[Test]
public void Describe_NamesBothSlotsAndARepairDirection()
{
    var value = TrainingLabPositionFaultDiagnosticV1.Describe(
        CreateFault(PositionFaultRuleV1.Slot2BehindSlot1));

    Assert.That(value.Text, Does.Contain("2号位"));
    Assert.That(value.Text, Does.Contain("1号位"));
    Assert.That(value.Axis,
        Is.EqualTo(TrainingLabCorrectionAxisV1.Depth));
    Assert.That(value.CourtDirection, Is.Not.EqualTo(0));
}
\`\`\`

- [ ] **Step 2: Run focused tests and confirm missing APIs.**

\`\`\`bash
UNITY_APP="/Applications/Unity/Unity-6000.3.20f1/Unity.app/Contents/MacOS/Unity"
"$UNITY_APP" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.TrainingScenarioLabControllerTests;Volleyball.EditModeTests.TrainingLabPositionFaultDiagnosticTests" -testResults "$PWD/TestResults/TrainingLabWorkflow.xml" -logFile "$PWD/TestResults/TrainingLabWorkflow.log"
\`\`\`

Expected: compilation failures for the gate and diagnostic APIs.

- [ ] **Step 3: Implement controller gate without modifying the validator.**

\`\`\`csharp
public bool CanEnterServeSetup =>
    Draft.RotationLocked && PositionFaultPreview.Count == 0;

public string ServeSetupBlockReason => !Draft.RotationLocked
    ? "Confirm rotation before configuring the serve."
    : PositionFaultPreview.Count > 0
        ? "Resolve every position fault before configuring the serve."
        : string.Empty;

public void SelectServeTool(TrainingServeToolV1 tool)
{
    EnsureEditable();
    if (!Enum.IsDefined(typeof(TrainingServeToolV1), tool))
        throw new ArgumentOutOfRangeException(nameof(tool));
    if (!CanEnterServeSetup)
        throw new InvalidOperationException(ServeSetupBlockReason);
    ServeTool = tool;
    CurrentStep = TrainingLabStepV1.ServeBall;
    Changed?.Invoke();
}
\`\`\`

Existing \`Validate\` continues to report every issue; this gate only makes fault-free positioning a prerequisite to Serve setup.

- [ ] **Step 4: Implement deterministic presentation diagnostics.**

\`\`\`csharp
public enum TrainingLabCorrectionAxisV1 { Depth, Lateral }

public sealed class TrainingLabPositionFaultDiagnosticV1
{
    public PositionFaultV1 Fault { get; }
    public TrainingLabCorrectionAxisV1 Axis { get; }
    public int CourtDirection { get; }
    public string Text { get; }

    public static TrainingLabPositionFaultDiagnosticV1 Describe(
        PositionFaultV1 fault);

    public static IReadOnlyList<TrainingLabPositionFaultDiagnosticV1>
        DescribeAll(IReadOnlyList<PositionFaultV1> faults);
}
\`\`\`

Calculate axis from the rule and direction from the frozen projections. Text must name team, violating slot, required slot, relationship, and a valid Chinese movement direction. \`DescribeAll\` preserves evaluator order. Test all seven rules and both sides.

- [ ] **Step 5: Rerun Step 2, then commit.**

\`\`\`bash
git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabController.cs Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabPositionFaultDiagnosticV1.cs Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs Assets/Volleyball/Match/Tests/EditMode/TrainingLabPositionFaultDiagnosticTests.cs
git commit -m "feat: gate training serve setup on position faults"
\`\`\`

### Task 3: Implement Tactical Board And Precision Panes

**Files:**
- Create: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabTacticalBoardPresenterV1.cs\`
- Create: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabPrecisionAdjustmentPresenterV1.cs\`
- Modify: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs\`
- Modify: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml\`
- Modify: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss\`
- Modify: \`Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabSceneTests.cs\`
- Modify: \`Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs\`

- [ ] **Step 1: Write failing visual-tree and real UI pointer-event tests.**

\`\`\`csharp
[Test]
public void VisualTree_HasBoardFaultAndPrecisionSurfaces()
{
    var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
        TreePath).CloneTree();

    Assert.That(root.Q("tactical-board"), Is.Not.Null);
    Assert.That(root.Q("tactical-token-layer"), Is.Not.Null);
    Assert.That(root.Q("position-fault-layer"), Is.Not.Null);
    Assert.That(root.Q<Button>("open-precision-button"), Is.Not.Null);
    Assert.That(root.Q("precision-xy-pane"), Is.Not.Null);
    Assert.That(root.Q("precision-zy-pane"), Is.Not.Null);
    Assert.That(root.Q("precision-xz-pane"), Is.Not.Null);
}

[UnityTest]
public IEnumerator TacticalBoard_DraggingTokenUpdatesDraft()
{
    yield return LoadLab();
    var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
    var root = view.GetComponent<UIDocument>().rootVisualElement;
    var player = view.Controller.Draft.Players[0];
    var token = root.Q<Button>("tactical-token-" + player.PlayerId.Value);
    var before = player.Position;

    SendPointerDown(token, token.worldBound.center);
    SendPointerMove(root.Q("tactical-board"),
        token.worldBound.center + new Vector2(80f, -45f));
    SendPointerUp(root.Q("tactical-board"),
        token.worldBound.center + new Vector2(80f, -45f));

    Assert.That(view.Controller.Draft.Players[0].Position,
        Is.Not.EqualTo(before));
}
\`\`\`

Implement helpers with concrete pooled \`PointerDownEvent\`, \`PointerMoveEvent\`, and \`PointerUpEvent\` calls. Add a known-fault test asserting two \`fault\` tokens plus relation/arrow elements; add ball XY velocity and player XZ-position tests proving third-axis preservation.

- [ ] **Step 2: Run visual-tree test and observe absent nodes.**

\`\`\`bash
UNITY_APP="/Applications/Unity/Unity-6000.3.20f1/Unity.app/Contents/MacOS/Unity"
"$UNITY_APP" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.TrainingScenarioLabSceneTests" -testResults "$PWD/TestResults/TrainingLabVisualTree.xml" -logFile "$PWD/TestResults/TrainingLabVisualTree.log"
\`\`\`

Expected: failed named-node assertions.

- [ ] **Step 3: Add authoring markup and styles.**

\`\`\`xml
<ui:VisualElement name="tactical-board" class="tactical-board" focusable="true">
  <ui:VisualElement name="court-lines" picking-mode="Ignore" />
  <ui:VisualElement name="position-fault-layer" picking-mode="Ignore" />
  <ui:VisualElement name="tactical-token-layer" />
  <ui:Label name="board-instruction" class="board-instruction" picking-mode="Ignore" />
  <ui:Button name="open-precision-button" text="精确调整 (XY / ZY / XZ)" />
  <ui:Button name="open-observation-button" text="自由 3D 观察" />
</ui:VisualElement>
<ui:VisualElement name="precision-adjustment" class="mode-surface is-hidden">
  <ui:Button name="return-to-board-button" text="返回战术板" />
  <ui:VisualElement name="precision-mode-row" />
  <ui:VisualElement name="precision-xy-pane" class="precision-pane" />
  <ui:VisualElement name="precision-zy-pane" class="precision-pane" />
  <ui:VisualElement name="precision-xz-pane" class="precision-pane" />
</ui:VisualElement>
\`\`\`

Use \`position: relative\` board, absolute token/line/arrow placement, non-color-only role/slot labels, and \`.is-hidden { display: none; }\`. Preserve all existing IDs used by lifecycle and test code.

- [ ] **Step 4: Implement direct board authoring.**

\`\`\`csharp
public sealed class TrainingLabTacticalBoardPresenterV1
{
    public void Render();

    // Token pointer move converts board-local UI coordinates with
    // BoardToCourt(board.contentRect, event.localPosition, 0f), then calls
    // controller.SetPlayerPosition. It never uses Camera or Physics.
}
\`\`\`

Tokens display team, role, and slot; click selects; pointer capture drags player tokens. \`DescribeAll(PositionFaultPreview)\` drives red tokens, relation line, and correction arrow. The ball board token selects the ball but cannot bypass the selected Serve tool.

- [ ] **Step 5: Implement synchronized precision editing.**

\`\`\`csharp
public enum TrainingLabPrecisionVectorModeV1 { Position, Velocity }

public void ApplyDrag(TrainingLabPrecisionPlaneV1 plane,
    float horizontal, float vertical)
{
    var next = TrainingLabTacticalBoardGeometryV1.ReplaceVisibleAxes(
        plane, SelectedVector(), horizontal, vertical);
    if (_controller.SelectedObjectId == "ball" &&
        VectorMode == TrainingLabPrecisionVectorModeV1.Velocity)
        _controller.SetBallVelocity(next);
    else if (_controller.SelectedObjectId == "ball")
        _controller.SetBallPosition(next);
    else
        _controller.SetPlayerPosition(new StablePlayerId(
            _controller.SelectedObjectId), next);
}
\`\`\`

Players expose Position only; ball exposes Position and Velocity. Every pane labels its axis pair, updates all panes/numeric fields after mutation, and preserves selection/current step/draft when opening or returning.

- [ ] **Step 6: Replace legacy raycast input only after new PlayMode tests pass.**

Delete \`OnPointerDown\`, \`OnPointerMove\`, \`OnPointerUp\`, \`ScreenRay\`, \`DragPlane\`, \`DragPosition\`, and \`TrainingLabViewportProjectionV1\`. Keep 3D preview only for Task 4 observation. The tactical presenter must not reference \`Camera\`, \`ScreenPointToRay\`, \`Physics.Raycast\`, or \`TrainingLabPreviewMarkerV1\`.

- [ ] **Step 7: Run tests, static check, and commit.**

\`\`\`bash
"$UNITY_APP" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "Volleyball.PlayModeTests.TrainingScenarioLabPlayModeTests" -testResults "$PWD/TestResults/TrainingLabBoardPlayMode.xml" -logFile "$PWD/TestResults/TrainingLabBoardPlayMode.log"
rg -n "Physics\\.Raycast|ScreenPointToRay|TrainingLabViewportProjectionV1" Assets/Volleyball/Match/Runtime/Presentation/TrainingLab && exit 1 || true
git diff --check
git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabTacticalBoardPresenterV1.cs Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabPrecisionAdjustmentPresenterV1.cs Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabSceneTests.cs Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs
git commit -m "feat: author training scenarios on tactical board"
\`\`\`

Expected: all TrainingLab PlayMode tests pass and no legacy authoring-ray symbols remain.

### Task 4: Add Read-Only 3D Observation And Validate

**Files:**
- Create: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabFreeObservationPresenterV1.cs\`
- Modify: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs\`
- Modify: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml\`
- Modify: \`Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss\`
- Modify: \`Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs\`
- Modify: \`docs/changes/2026-08-04-001-training-lineup-position-fault-v5.md\`
- Modify: \`docs/handoffs/active/2026-08-01-training-lineup-position-fault-v5-implementation.md\`

- [ ] **Step 1: Write failing non-mutation observation tests.**

\`\`\`csharp
[UnityTest]
public IEnumerator FreeObservation_MovesCameraWithoutMutatingDraft()
{
    yield return LoadLab();
    var view = Object.FindFirstObjectByType<TrainingScenarioLabView>();
    var player = view.Controller.Draft.Players[0].Position;
    var ball = view.Controller.Draft.BallPosition;
    var velocity = view.Controller.Draft.BallVelocity;

    view.OpenFreeObservation();
    DragObservation(view, new Vector2(70f, 30f));
    ScrollObservation(view, -3f);

    Assert.That(view.ObservationCameraChanged, Is.True);
    Assert.That(view.Controller.Draft.Players[0].Position,
        Is.EqualTo(player));
    Assert.That(view.Controller.Draft.BallPosition, Is.EqualTo(ball));
    Assert.That(view.Controller.Draft.BallVelocity,
        Is.EqualTo(velocity));
}
\`\`\`

Also assert saving/reloading an observation bookmark and returning preserves the previously selected object.

- [ ] **Step 2: Run the test and confirm observation APIs are missing.**

\`\`\`bash
UNITY_APP="/Applications/Unity/Unity-6000.3.20f1/Unity.app/Contents/MacOS/Unity"
"$UNITY_APP" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "Volleyball.PlayModeTests.TrainingScenarioLabPlayModeTests.FreeObservation" -testResults "$PWD/TestResults/TrainingLabObservation.xml" -logFile "$PWD/TestResults/TrainingLabObservation.log"
\`\`\`

Expected: compile failures for \`OpenFreeObservation\` and \`ObservationCameraChanged\`.

- [ ] **Step 3: Implement presenter with no controller or draft reference.**

\`\`\`csharp
public sealed class TrainingLabFreeObservationPresenterV1
{
    public TrainingLabFreeObservationPresenterV1(
        VisualElement surface, Camera camera);

    // Pointer capture adjusts private yaw/pitch; wheel adjusts private distance.
    // ApplyCamera orbits the supplied camera around Vector3.zero.
}
\`\`\`

Clamp pitch to 10--80 degrees and distance to 6--40 meters. The constructor receives only \`VisualElement\` and \`Camera\`. View code persists existing bookmarks as camera pose, never gameplay data.

- [ ] **Step 4: Wire explicit observation mode.**

Show a “仅观察” instruction and return button; hide board and precision controls. Orbit/pan/zoom, trajectory inspection, and bookmarks work. No token/pane input is active. Return restores board mode, selection, and current step.

- [ ] **Step 5: Run frozen automated validation.**

\`\`\`bash
"$UNITY_APP" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.TrainingLabTacticalBoardGeometryTests;Volleyball.EditModeTests.TrainingLabPositionFaultDiagnosticTests;Volleyball.EditModeTests.TrainingScenarioLabControllerTests;Volleyball.EditModeTests.TrainingScenarioLabSceneTests" -testResults "$PWD/TestResults/TrainingLabAuthoringEditMode.xml" -logFile "$PWD/TestResults/TrainingLabAuthoringEditMode.log"
"$UNITY_APP" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "Volleyball.PlayModeTests.TrainingScenarioLabPlayModeTests" -testResults "$PWD/TestResults/TrainingLabAuthoringPlayMode.xml" -logFile "$PWD/TestResults/TrainingLabAuthoringPlayMode.log"
rg -n "Physics\\.Raycast|ScreenPointToRay|TrainingLabViewportProjectionV1" Assets/Volleyball/Match/Runtime/Presentation/TrainingLab && exit 1 || true
git diff --check
\`\`\`

Expected: both selected suites pass, no legacy authoring-ray symbols, and no whitespace error.

- [ ] **Step 6: Manually validate at 1920x1080 in macOS Editor and Windows x64 IL2CPP Development Player.**

Verify: lock rotation; drag a 2D token; create fault; observe red tokens/line/arrow/text and disabled Serve setup; correct it; adjust ball XY velocity and XZ position while preserving third axes; orbit/zoom/save/reload a free 3D bookmark without draft changes; validate/run legal scenario. Record OS, Unity version, commit, and each pass/fail. Do not complete the active handoff if Windows validation is unavailable.

- [ ] **Step 7: Record only fresh evidence and commit.**

\`\`\`bash
git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabFreeObservationPresenterV1.cs Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs docs/changes/2026-08-04-001-training-lineup-position-fault-v5.md docs/handoffs/active/2026-08-01-training-lineup-position-fault-v5-implementation.md
git commit -m "feat: add read only training lab observation"
\`\`\`

Keep the handoff \`Status: active\` until earlier V5 criteria and new Windows Player evidence are complete.

## Plan Self-Review

- Tasks 1 and 3 remove camera-ray dependency from 2D authoring.
- Task 2 fulfills the required fault gate and correction directions.
- Task 3 covers ball/player precision axes and synchronized views.
- Task 4 makes 3D explicitly read-only and requires real Player validation.
- No task modifies Match rules, frozen scenarios, V5 contracts, Career, or formal simulation.

