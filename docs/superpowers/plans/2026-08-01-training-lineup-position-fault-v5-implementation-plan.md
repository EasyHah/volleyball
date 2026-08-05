# Training Lineup, Position Fault, and V5 Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a serve-only TrainingLab lineup workbench and apply the same strict volleyball position-fault rule to TrainingLab and native V5 formal matches, with deterministic V5 result/replay evidence.

**Architecture:** A Match-domain pure evaluator accepts frozen rotation slots and foot-projection court positions and returns zero or more deterministic position-fault relations. TrainingLab owns editable scenario-only rotation, pose, serve-ball, camera, and attribute-override data, then freezes a validated serve-start snapshot. The native V5 runner captures authoritative on-court positions at serve contact, applies the same evaluator before physics/AI starts, immediately awards the point when required, and records a versioned fault event in V5 result/replay artifacts. V4 remains untouched.

**Tech Stack:** Unity 6000.3.20f1; C#; UI Toolkit; NUnit EditMode/PlayMode; existing `Volleyball.Match.*`, `Volleyball.Shared`, `Volleyball.Career.*`, and Bootstrap assemblies; Windows x64 IL2CPP Development Player.

---

## Implementation Gate

Do not start this plan in the current HUD/Setter-review handoff. Before Task 1 implementation:

- [ ] Confirm the HUD/Setter-review task is completed or superseded without losing its outstanding defect fix.
- [ ] Fetch `origin`, verify that the V5-B implementation commit is merged in `origin/main`, and create a new milestone branch from the current `origin/main`.
- [ ] Create the one active high-risk handoff for this plan. It must explicitly authorize Match, Shared, Career, Bootstrap, Replay, TrainingLab, scene/UI, and Windows validation work; state V4 is untouched, attribute overrides are TrainingLab-only, and rollback is a branch revert plus rejection of unsupported fault-event versions.
- [ ] Record the V5 context/result/replay version decision in that handoff before changing `MatchContextV5`, `MatchResultV5`, or `MatchReplayV5`.
- [ ] Start with a clean working tree, except files explicitly carried into the new milestone by their owners.

## File Map

| Area | Files | Responsibility |
| --- | --- | --- |
| Pure rule | `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/PositionFaultEvaluatorV1.cs` | Strict front/back and left/right comparison, ordered evidence, no Unity references. |
| Rule tests | `Assets/Volleyball/Match/Tests/EditMode/PositionFaultEvaluatorV1Tests.cs` | Legal equality, each violation direction, multi-fault ordering, team isolation. |
| Serve-start adapter | `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs` | Capture authoritative feet at serve start, call evaluator before ball launch, resolve immediate point. |
| V5 replay/result contract | `Assets/Volleyball/Shared/Runtime/MatchResultV5.cs`, `Assets/Volleyball/Shared/Runtime/MatchReplayV5.cs`, `Assets/Volleyball/Shared/Runtime/ContractJson.cs` | Canonical, versioned position-fault summary/events and JSON/hash validation. |
| V5 evidence recorder | `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorderV5.cs` | Collect a fault event from the formal director and include it in the native V5 replay. |
| Training data | `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioDraftV1.cs`, `TrainingScenarioV1.cs`, `TrainingScenarioPresetV1.cs`, `TrainingScenarioCanonicalizerV1.cs`, `TrainingScenarioValidatorV1.cs` | Serve-only scenario data, locked rotation, scenario-only overrides, camera bookmarks, canonical validation. |
| Training controller/UI | `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabController.cs`, `TrainingScenarioLabView.cs`, `TrainingScenarioLab.uxml`, `TrainingScenarioLab.uss` | Five-step workbench, role/slot labels, direct drag, tool modes, monitor, camera and attribute table. |
| Training runtime bridge | `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioRuntimeAdapterV1.cs`, `TrainingSimulationControllerV1.cs`, `TrainingTimelineRecorderV1.cs` | Freeze one validated serve-start input and surface position-fault diagnostics without leaking editor data. |
| Tests | Existing `TrainingScenario*Tests.cs`, `TrainingScenarioLab*Tests.cs`, `FormalSixVsSixRallyPlayModeTests.cs`, `CareerFormalSixVsSixMatchRunnerPlayModeTests.cs`, `MatchContractTests.cs` | Unit, UI, formal V5, context/replay, and boundary regression coverage. |

### Task 1: Define strict position-fault domain values and evaluator

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/PositionFaultEvaluatorV1.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/PositionFaultEvaluatorV1Tests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Volleyball.Match.Domain.asmdef` only if the new file needs an already-approved assembly reference; otherwise do not change asmdefs.

- [ ] **Step 1: Write failing rule vectors before implementation.**

```csharp
[Test]
public void Evaluate_EqualFootProjectionIsLegal()
{
    var faults = PositionFaultEvaluatorV1.Evaluate(HomeSlots(
        slot1: new SimVector3(2f, 0f, -2f),
        slot2: new SimVector3(0f, 0f, -2f),
        slot3: new SimVector3(-2f, 0f, -2f),
        slot4: new SimVector3(2f, 0f, -4f),
        slot5: new SimVector3(0f, 0f, -2f),
        slot6: new SimVector3(-2f, 0f, -4f)));

    Assert.That(faults, Is.Empty);
}

[Test]
public void Evaluate_BackRowAheadOfItsFrontRowReportsOrderedRelation()
{
    var faults = PositionFaultEvaluatorV1.Evaluate(HomeSlots(
        slot1: new SimVector3(2f, 0f, -2f),
        slot2: new SimVector3(0f, 0f, -2f),
        slot3: new SimVector3(-2f, 0f, -2f),
        slot4: new SimVector3(2f, 0f, -1f),
        slot5: new SimVector3(0f, 0f, -4f),
        slot6: new SimVector3(-2f, 0f, -4f)));

    Assert.That(faults.Single().Rule, Is.EqualTo(PositionFaultRuleV1.Slot4BehindSlot5));
}
```

- [ ] **Step 2: Run the focused test and verify compilation fails because the evaluator does not exist.**

Run:

```bash
UNITY="/Applications/Unity/Unity-6000.3.20f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PositionFaultEvaluatorV1Tests" \
  -testResults "$PWD/TestResults/PositionFault-Rule.xml" \
  -logFile "$PWD/TestResults/PositionFault-Rule.log"
```

Expected: compile failure naming `PositionFaultEvaluatorV1`.

- [ ] **Step 3: Implement immutable rule input and result values.**

```csharp
public enum PositionFaultRuleV1
{
    Slot4BehindSlot5, Slot3BehindSlot6, Slot2BehindSlot1,
    Slot1LeftOfSlot6, Slot6LeftOfSlot5, Slot5LeftOfSlot4,
    Slot4LeftOfSlot3, Slot3LeftOfSlot2, Slot2LeftOfSlot1
}

public sealed class PositionFaultV1
{
    public TeamSide Side { get; }
    public PlayerId RequiredAheadOrRight { get; }
    public PlayerId ViolatingBehindOrLeft { get; }
    public PositionFaultRuleV1 Rule { get; }
    public SimVector3 RequiredProjection { get; }
    public SimVector3 ViolatingProjection { get; }
}

public static IReadOnlyList<PositionFaultV1> Evaluate(
    IReadOnlyList<ServePositionSlotV1> slots)
```

The evaluator must require exactly six unique slots per team, reject non-finite positions, compare Home and Away with their net-facing axis correctly, return fault rows in a documented stable order, and use one private non-configurable floating-point comparison constant solely to avoid representation noise. Equality returns legal.

- [ ] **Step 4: Add all pure-rule vectors.**

Cover legal canonical rotations for each side; nine individual relation failures; several simultaneous failures; cross-team independence; duplicate slot/player rejection; wrong team/slot; non-finite vectors; equality; and deterministic ordering of results.

- [ ] **Step 5: Run focused tests and inspect output.**

Run the Step 2 command. Expected: all `PositionFaultEvaluatorV1Tests` pass.

- [ ] **Step 6: Commit the pure rule.**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/PositionFaultEvaluatorV1.cs \
  Assets/Volleyball/Match/Tests/EditMode/PositionFaultEvaluatorV1Tests.cs
git commit -m "feat: evaluate strict volleyball position faults"
```

### Task 2: Make TrainingLab serve-start data explicit and frozen

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingServeStartV1.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioDraftV1.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioV1.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioCanonicalizerV1.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioValidatorV1.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioV1Tests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioValidatorV1Tests.cs`

- [ ] **Step 1: Add failing tests for locked rotation and serve-zone boundary.**

```csharp
[Test]
public void Validate_ServeBallInsideCourtRejectsTheScenario()
{
    var draft = ValidServeDraft(TeamSide.Away);
    draft.BallPosition = new SimVector3(0f, 2f, 8.9f);

    var result = TrainingScenarioValidatorV1.Validate(draft);

    Assert.That(result.Issues.Select(value => value.Code),
        Does.Contain(TrainingScenarioIssueCodesV1.BallNotBehindServingEndLine));
}

[Test]
public void Freeze_UsesLockedRotationAndRejectsUnconfirmedRotation()
{
    var draft = ValidServeDraft(TeamSide.Home);
    draft.RotationLocked = false;

    Assert.That(() => TrainingScenarioV1.Freeze(draft), Throws.ArgumentException);
}
```

- [ ] **Step 2: Run the TrainingLab validator tests and confirm the new symbols fail.**

- [ ] **Step 3: Add `TrainingServeStartV1`.**

```csharp
public sealed class TrainingServeStartV1
{
    public TeamSide ServingSide { get; }
    public IReadOnlyList<ServePositionSlotV1> Slots { get; }
    public SimVector3 BallPosition { get; }
    public SimVector3 BallVelocity { get; }

    public void ValidateServeZone(float halfCourtLength)
}
```

Use current `CourtBuilder.FormalHalfLength` / formal court constants rather than duplicating dimensions. Require a ball behind the serving side's end line and within the configured lateral/vertical bounds. Keep ball position and velocity as one serve-start unit; remove any TrainingLab API that describes arbitrary mid-rally initialization.

- [ ] **Step 4: Extend draft/freeze/canonical bytes.**

Add per-team editable rotation orders and a `RotationLocked` state to the draft. Freeze `TrainingServeStartV1` only after both rotation orders are exact six-player permutations and locked. Include rotations, player foot positions, serve side, ball position, and velocity in canonical bytes/hash; include no camera or override data in formal runtime inputs.

- [ ] **Step 5: Add validator rules.**

Add stable issue codes for unlocked rotation, invalid rotation membership, ball outside serving zone, and strict position-fault preflight. In editing, preserve a structured fault list so UI can warn and explain; in freeze/run, block all position faults.

- [ ] **Step 6: Run focused TrainingLab tests.**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.TrainingScenarioV1Tests;Volleyball.EditModeTests.TrainingScenarioValidatorV1Tests" \
  -testResults "$PWD/TestResults/TrainingServeStart.xml" \
  -logFile "$PWD/TestResults/TrainingServeStart.log"
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit TrainingLab serve-start data.**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab \
  Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioV1Tests.cs \
  Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioValidatorV1Tests.cs
git commit -m "feat: freeze validated training serve starts"
```

### Task 3: Add TrainingLab-only attribute overrides and camera bookmarks

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingPlayerAttributeOverrideV1.cs`
- Create: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingCameraBookmarkV1.cs`
- Modify: `TrainingScenarioDraftV1.cs`, `TrainingScenarioPresetV1.cs`, `TrainingScenarioCanonicalizerV1.cs`, `TrainingScenarioValidatorV1.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioV1Tests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioValidatorV1Tests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs`

- [ ] **Step 1: Write failing isolation tests.**

```csharp
[Test]
public void AttributeOverride_ChangesOnlyTheTrainingSnapshot()
{
    var draft = ValidServeDraft(TeamSide.Home);
    draft.SetOverride("home-setter", new TrainingPlayerAttributeOverrideV1(
        heightMillimeters: 1880, dominantHand: DominantHandV4.Left,
        physical: Physical(6200), technical: Technical(9000)));

    var scenario = TrainingScenarioV1.Freeze(draft);

    Assert.That(scenario.TrainingOverrides["home-setter"].DominantHand,
        Is.EqualTo(DominantHandV4.Left));
    Assert.That(draft.Context.Home.RotationOrder.Single(p => p.PlayerId.Value == "home-setter").DominantHand,
        Is.EqualTo(DominantHandV4.Right));
}
```

- [ ] **Step 2: Run the focused tests and verify missing override/bookmark APIs fail.**

- [ ] **Step 3: Implement explicit TrainingLab-only models.**

`TrainingPlayerAttributeOverrideV1` may include only height, dominant hand, and existing Match V4 test attributes. It must not expose player IDs from Career persistence, V5 base attributes, or mutable professional position. `TrainingCameraBookmarkV1` contains a bounded name, pose, projection settings, and no gameplay data.

- [ ] **Step 4: Validate bounds and enforce ownership.**

Reject unknown training player IDs, non-finite or out-of-range values, duplicate overrides, and duplicate bookmark names. Serialization/canonicalization must preserve deterministic TrainingLab saves. The runtime adapter may derive temporary test abilities from overrides, but no Career, Shared V5, Bootstrap V5, or formal V5 file may reference either type.

- [ ] **Step 5: Add a static boundary test.**

Scan source/assembly references and fail if `TrainingPlayerAttributeOverrideV1` or `TrainingCameraBookmarkV1` appears outside TrainingLab presentation/test code.

- [ ] **Step 6: Run focused tests and commit.**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab \
  Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioV1Tests.cs \
  Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioValidatorV1Tests.cs \
  Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs
git commit -m "feat: isolate training attribute overrides and cameras"
```

### Task 4: Connect strict position faults to the TrainingLab runtime

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioRuntimeAdapterV1.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingSimulationControllerV1.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingTimelineRecorderV1.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs`
- Test: `Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioRuntimePlayModeTests.cs`

- [ ] **Step 1: Write failing runtime tests.**

```csharp
[Test]
public void Run_PositionFaultDoesNotLaunchTheServe()
{
    var runtime = new FakeSimulation();
    using var controller = new TrainingScenarioLabController(StoreWithFault(), runtime);

    Assert.That(controller.Run(), Is.False);
    Assert.That(runtime.Starts, Is.Empty);
    Assert.That(controller.Validation.Issues.Select(issue => issue.Code),
        Does.Contain(TrainingScenarioIssueCodesV1.PositionFault));
}
```

For PlayMode, freeze one legal serve start and one invalid position start. The legal case must emit `ReplayServeStarted`; the invalid case must resolve with the receiving team as winner before `ReplayServeStarted`, leave counted touches at zero, and surface the exact fault relations in TrainingLab evidence.

- [ ] **Step 2: Run tests and confirm they fail before integration.**

- [ ] **Step 3: Add a serve-start injection surface to the director.**

Use one immutable startup parameter rather than setting ball/player transforms from UI after the director starts. At serve initiation, capture all runtime player foot positions, map stable IDs to frozen rotation slots, evaluate faults, and call the existing `ResolveRally` path with a structured position-fault reason before `ReplayServeStarted`/ball launch when invalid.

- [ ] **Step 4: Preserve normal formal behaviour.**

The adapter must only set this startup parameter for TrainingLab. Existing V4/formal callers without a TrainingLab start continue unchanged. Do not use `#if UNITY_EDITOR` to change referee semantics.

- [ ] **Step 5: Record structured TrainingLab evidence.**

Add a `PositionFault` timeline kind containing fault side, involved player IDs/slots, rule and sampled positions. The timeline remains diagnostic-only and does not become a Shared or Career contract.

- [ ] **Step 6: Run focused EditMode and PlayMode checks.**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.TrainingScenarioLabControllerTests" \
  -testResults "$PWD/TestResults/TrainingPositionFault-EditMode.xml" \
  -logFile "$PWD/TestResults/TrainingPositionFault-EditMode.log"
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.TrainingScenarioRuntimePlayModeTests" \
  -testResults "$PWD/TestResults/TrainingPositionFault-PlayMode.xml" \
  -logFile "$PWD/TestResults/TrainingPositionFault-PlayMode.log"
```

- [ ] **Step 7: Commit TrainingLab runtime integration.**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioRuntimePlayModeTests.cs
git commit -m "feat: enforce position faults in training serve starts"
```

### Task 5: Implement the five-step TrainingLab UI

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabController.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabSceneTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs`
- Test: `Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs`

- [ ] **Step 1: Write failing UI/controller tests.**

```csharp
[Test]
public void Controller_LocksRotationBeforePositionEdits()
{
    using var controller = new TrainingScenarioLabController(Store(), new FakeSimulation());

    controller.SetRotation(TeamSide.Home, OrderedHomeSlots());
    controller.ConfirmRotation();

    Assert.That(() => controller.SetRotation(TeamSide.Home, SwappedHomeSlots()),
        Throws.InvalidOperationException);
    Assert.That(controller.CurrentStep, Is.EqualTo(TrainingLabStepV1.Positioning));
}

[Test]
public void VisualTree_ExposesServeToolsAndRallyMonitor()
{
    var root = Tree().CloneTree();
    Assert.That(root.Q<Button>("move-serve-ball-button"), Is.Not.Null);
    Assert.That(root.Q<Button>("adjust-serve-velocity-button"), Is.Not.Null);
    Assert.That(root.Q<Button>("view-serve-trajectory-button"), Is.Not.Null);
    Assert.That(root.Q<VisualElement>("rally-monitor"), Is.Not.Null);
}
```

- [ ] **Step 2: Run the selected tests and verify they fail.**

- [ ] **Step 3: Implement the controller state machine.**

```csharp
public enum TrainingLabStepV1
{
    Rotation, Positioning, ServeBall, Validation, Running
}

public void ConfirmRotation();
public void ReopenRotation();
public void SelectServeTool(TrainingServeToolV1 tool);
public IReadOnlyList<PositionFaultV1> PositionFaultPreview { get; }
```

`ReopenRotation` clears position confirmation, returns to `Rotation`, and blocks running until the edited rotation and positions are reconfirmed. `Running` locks all scenario edits. The controller owns state transitions; the view must not infer validity by looking at controls.

- [ ] **Step 4: Implement UI Toolkit workbench.**

Render: step rail; rotation list with read-only professional role; player head labels with role plus locked slot; drag handles in positioning only; position-fault relation cards; mutually exclusive serve tools; XY/ZY/XZ view selectors; precise position/velocity fields; camera bookmark controls; per-team horizontally scrollable admin table; and persistent rally monitor. Make red markings show all participants in a fault relation and provide a Chinese corrective sentence.

- [ ] **Step 5: Apply direct manipulation safely.**

Only the `MoveServeBall` tool drags the yellow ball. Only `AdjustServeVelocity` drags the red vector. Trajectory is read-only. Each orthographic view updates precisely two components and all three synchronize against the one `TrainingServeStartV1`. Invalid drag/input reverts to the last valid serve-zone value and shows a field-level error.

- [ ] **Step 6: Add manual visual acceptance checklist to the change record.**

At 1920x1080, verify the step rail, 12 role/slot labels, Home/Away list, red position-fault explanation, three serve tools, valid/invalid serve-zone feedback, monitor changes, camera save/load, table horizontal scroll, and Player-safe absence of admin-only controls.

- [ ] **Step 7: Run focused tests and commit.**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.TrainingScenarioLabSceneTests;Volleyball.EditModeTests.TrainingScenarioLabControllerTests" \
  -testResults "$PWD/TestResults/TrainingWorkbench-EditMode.xml" \
  -logFile "$PWD/TestResults/TrainingWorkbench-EditMode.log"
git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab \
  Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabSceneTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs
git commit -m "feat: add staged training lineup workbench"
```

### Task 6: Extend V5 contracts with canonical position-fault evidence

**Files:**
- Create: `Assets/Volleyball/Shared/Runtime/MatchPositionFaultV5.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/MatchResultV5.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/MatchReplayV5.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/ContractJson.cs`
- Modify: canonical JSON/hash helpers associated with `MatchResultV5` and `MatchReplayV5`
- Test: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Write failing Shared contract vectors.**

```csharp
[Test]
public void V5Replay_PositionFaultRoundTripsAndChangesTheReplayHash()
{
    var fault = PositionFault("home", "home-setter", 4, "home-outside-b", 5,
        PositionFaultRuleV1.Slot4BehindSlot5);
    var replay = MatchReplayV5.Create("replay", Context(), Array.Empty<MatchReplayAttributeEvidenceV5>(),
        Array.Empty<MatchReplayReportFactV1>(), new[] { fault });

    var parsed = ContractJson.DeserializeMatchReplayV5(ContractJson.Serialize(replay));

    Assert.That(parsed.PositionFaults.Single().Rule, Is.EqualTo(fault.Rule));
    Assert.That(parsed.ReplayHash, Is.EqualTo(replay.ReplayHash));
}
```

- [ ] **Step 2: Run `MatchContractTests` and confirm missing types/factory overload fail.**

- [ ] **Step 3: Implement canonical `MatchPositionFaultV5`.**

Include event sequence, rally number, violating side, awarded side, serving side, both player IDs and slots, `PositionFaultRuleV1` equivalent stable contract enum/string, X/Z foot-projection values in millimetres, and a rule version. Reject absent context players, nonmatching slots, duplicate event identity, nonfinite values, and invalid score/rally linkage.

- [ ] **Step 4: Version V5 deliberately.**

Because V5 payload/hash changes, do not add optional fields or default-value shims. Introduce the next approved V5 contract discriminator/version and update canonical JSON/validation together for context/result/replay. Reject existing persisted V5 pending/result/replay that cannot declare the new version with a recoverable “discard old pending and create new match” path; document this migration/rollback decision in the active handoff and change record.

- [ ] **Step 5: Bind result and replay.**

`MatchResultV5` receives deterministic aggregate position-fault facts sufficient to validate score/rally totals; `MatchReplayV5` receives ordered per-rally facts. Both include the canonical additions in their hashes and cross-validation. A completed V5 match with no fault produces empty ordered collections, not null.

- [ ] **Step 6: Add invalid and golden tests.**

Cover Home/Away fault, multiple faults, malformed player/slot/side combinations, supplied score mismatch, JSON unknown/missing fields, canonical ordering, hash changes, byte-identical identical input, and rejection of old discriminator versions.

- [ ] **Step 7: Run Shared tests and commit.**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.MatchContractTests" \
  -testResults "$PWD/TestResults/V5PositionFault-Shared.xml" \
  -logFile "$PWD/TestResults/V5PositionFault-Shared.log"
git add Assets/Volleyball/Shared/Runtime Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: record canonical V5 position fault evidence"
```

### Task 7: Enforce position faults at native V5 serve contact and record evidence

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorderV5.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/OnCourtLineupRulesV5.cs`
- Test: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Test: `Assets/Volleyball/Career/Tests/PlayMode/CareerFormalSixVsSixMatchRunnerPlayModeTests.cs`

- [ ] **Step 1: Write failing V5 integration tests.**

```csharp
[UnityTest]
public IEnumerator V5PositionFault_AwardsOpponentBeforeServePhysicsAndRecordsReplay()
{
    var director = StartV5Director(ContextWithHomeSlot4BehindSlot5());
    yield return WaitForResolvedRally(director);

    Assert.That(director.SuccessfulContacts, Is.EqualTo(0));
    Assert.That(director.HomeScore, Is.EqualTo(0));
    Assert.That(director.AwayScore, Is.EqualTo(1));
    Assert.That(recorder.Complete().PositionFaults.Single().ViolatingSide,
        Is.EqualTo(TeamSide.Home));
}
```

- [ ] **Step 2: Run selected PlayMode tests and confirm fail.**

- [ ] **Step 3: Add one V5-only serve-position provider.**

Use `OnCourtLineupRulesV5` and frozen V5 rotation IDs to construct evaluator slots from runtime foot projection at the exact serve-contact point. Do not let position templates, role heuristics, TrainingLab overrides, or future frames supply the data. Run before `_ball.ResetBall`, `_ball.Launch`, `ReplayServeStarted`, and possession/AI initialization.

- [ ] **Step 4: Resolve the fault through existing scoring lifecycle.**

Translate the evaluator output to a structured director event and invoke the existing `ResolveRally` path with opponent winner, no scorer/error contact, reason `PositionFault`. Ensure serve ownership/next rally updates once, no contact/decision data is created for the failed rally, and multiple relations for one team create one point plus ordered evidence rows.

- [ ] **Step 5: Record V5 evidence.**

Add a dedicated director event subscribed by `MatchReplayRecorderV5`. The recorder maps runtime IDs to context IDs and produces `MatchPositionFaultV5` records without synthesizing a contact/report fact. Update `Complete()` and result creation to cross-validate all fault records.

- [ ] **Step 6: Preserve V4.**

Add regression tests proving a V4 startup remains on its existing serve behaviour and uses no V5 fault contract path. Do not edit V4 context/result/replay schemas.

- [ ] **Step 7: Run focused V5 and V4 PlayMode tests and commit.**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests;Volleyball.PlayModeTests.CareerFormalSixVsSixMatchRunnerPlayModeTests" \
  -testResults "$PWD/TestResults/V5PositionFault-PlayMode.xml" \
  -logFile "$PWD/TestResults/V5PositionFault-PlayMode.log"
git add Assets/Volleyball/Match/Runtime Assets/Volleyball/Match/Tests/PlayMode \
  Assets/Volleyball/Career/Tests/PlayMode
git commit -m "feat: enforce V5 position faults at serve contact"
```

### Task 8: Update V5 lifecycle rejection/recovery and Career consumption boundaries

**Files:**
- Modify: `Assets/Volleyball/Career/Runtime/MatchIntegration/CareerV5MatchLifecycleService.cs`
- Modify: `Assets/Volleyball/Career/Runtime/Persistence/CareerV5PendingStore.cs`
- Modify: `Assets/Volleyball/Career/Runtime/MatchIntegration/CareerV5MatchSettlement.cs`
- Modify: `Assets/Volleyball/Career/Tests/EditMode/CareerMatchIntegrationTests.cs`
- Modify: `Assets/Volleyball/Career/Tests/EditMode/CareerMatchBoundaryTests.cs`

- [ ] **Step 1: Write failing persistence/boundary tests.**

```csharp
[Test]
public void OldV5PendingContract_IsRecoverablyDiscardedRatherThanMigrated()
{
    var store = StoreWithSerializedPreviousV5Pending();

    var result = new CareerV5MatchLifecycleService(store, Runner()).RecoverPending();

    Assert.That(result.Status, Is.EqualTo(CareerV5RecoveryStatus.DiscardRequired));
    Assert.That(result.Message, Does.Contain("create a new V5 match"));
}
```

- [ ] **Step 2: Run Career focused tests and verify they fail.**

- [ ] **Step 3: Version-gate persisted V5 artifacts.**

Reject the previous V5 discriminator before execution or settlement; preserve bytes for diagnostics/recovery; expose one explicit discard/create-new path. Do not silently fill empty position-fault lists into historical canonical payloads.

- [ ] **Step 4: Preserve fact/consequence ownership.**

Career validates result/replay binding, accepts a position-fault as Match fact, and must not recompute player positions, position rules, or fault winners. Settlement outcome may use normal result facts only under existing policy; do not invent growth/trust/fatigue consequences for a no-contact rally without a separately approved Career rule.

- [ ] **Step 5: Run focused Career tests and commit.**

```bash
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.CareerMatchIntegrationTests;Volleyball.EditModeTests.CareerMatchBoundaryTests" \
  -testResults "$PWD/TestResults/V5PositionFault-Career.xml" \
  -logFile "$PWD/TestResults/V5PositionFault-Career.log"
git add Assets/Volleyball/Career/Runtime Assets/Volleyball/Career/Tests/EditMode
git commit -m "fix: reject incompatible V5 position fault pending data"
```

### Task 9: Freeze validation, documentation, review, and Windows acceptance

**Files:**
- Create: `docs/changes/2026-08-01-002-training-lineup-position-fault-v5.md`
- Modify: `docs/changes/README.md`
- Modify: active high-risk handoff, then move it to `docs/handoffs/completed/` only after all gates pass.
- Modify: `docs/development.md` with the TrainingLab manual workflow and test/build commands.

- [ ] **Step 1: Run affected focused suites after code freeze.**

Run Tasks 1--8 focused test commands again, then:

```bash
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/Full-EditMode.xml" \
  -logFile "$PWD/TestResults/Full-EditMode.log"
```

Expected: full EditMode passes once after implementation is frozen.

- [ ] **Step 2: Run necessary PlayMode regression once.**

Run TrainingLab, formal V4 regression, V5 formal lifecycle, fixed-seed replay and report settlement suites. Save named XML/log artifacts and record exact counts; do not reuse earlier results after later edits.

- [ ] **Step 3: Perform manual macOS Editor acceptance.**

At 1920x1080: create/lock a rotation; deliberately make and repair a legal/illegal relation; inspect role/slot labels; drag the serve ball in legal zone; reject an illegal numeric/drag position; edit each velocity plane; save/reload named camera; edit/save one override; run a legal serve; run a position fault; inspect the timeline/monitor and position-fault evidence.

- [ ] **Step 4: Perform one independent high-risk review.**

Review canonical contract/version behavior, V4 non-regression, fault ordering, score ownership, TrainingLab-to-V5 isolation, and Player assembly boundaries. Resolve P0/P1 findings in one batch and rerun only affected focused tests plus `git diff --check`.

- [ ] **Step 5: Perform Windows x64 IL2CPP Development build and Player acceptance.**

On Windows Unity `6000.3.20f1` with IL2CPP support, run focused EditMode and affected PlayMode; build the configured Development Player; exercise legal/illegal TrainingLab serve starts, V5 normal/faulted runs, logs, input, rendering, and V5 pending rejection/recovery. Record build report and manual results.

- [ ] **Step 6: Update records and complete handoff.**

Record version/compatibility decision, test counts, review findings, Windows evidence, remaining risks, and rollback. Set the high-risk handoff `Status: completed` and move it to `docs/handoffs/completed/` only after all required automatic and manual checks pass.

- [ ] **Step 7: Commit documentation only after final evidence is recorded.**

```bash
git add docs/changes docs/development.md docs/handoffs
git commit -m "docs: record training lineup and V5 position fault acceptance"
```

## 执行进度（2026-08-04）

Task 1--8 的实现已存在于本里程碑分支并完成针对性复核；本次续作还修复了 V4 版本中立能力投影
回归和 Career V5 PlayMode 的固定仿真帧预算。Task 9 的自动验证步骤已完成，人工 Editor 验收、
独立复核和本计划新增 TrainingLab Windows Player 验收仍待执行，因此 handoff 保持 active。

## Self-Review

- Spec coverage: Tasks 1 and 4 cover strict equality/ordering and immediate point behaviour; Tasks 2, 3 and 5 cover all TrainingLab editing, serve-only, camera and isolated override requirements; Tasks 6--8 cover V5 canonical evidence, replay/result binding, V5 lifecycle recovery and Career ownership; Task 9 covers full validation and handoff.
- Boundary coverage: V4 non-regression appears in Tasks 6, 7 and 9. Training-only data isolation appears in Tasks 3, 5, 6 and 9. No task adds a configurable tolerance, arbitrary mid-rally start, or writable Career/V5 player attributes.
- Version coverage: Task 6 explicitly requires a deliberate new V5 discriminator rather than optional/default compatibility fields; Task 8 provides the recoverable old-pending path.
- Placeholder scan: no `TBD`, `TODO`, deferred implementation markers, or undefined future work remain in task steps.
