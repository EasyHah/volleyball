# TrainingLab Unified Workbench and Native V5 Rally Implementation Plan

**Goal:** Deliver the confirmed page-based TrainingLab with Match-owned V5 pre-serve data, scenario-private V5 administrator overrides, persistent local working copies, editable top/side serve views, read-only 3D preview, automatic preflight and one-rally execution through the native V5 Match authority.

**Architecture:** Add Match-owned `MatchSetupDraftV1`, `MatchSetupEditorV1`, immutable `MatchSetupSnapshotV1`, `TrainingPlayerAttributeOverrideV2`, `TrainingRallyStartV5` and `TrainingRallyOutcomeV1`. TrainingLab retains only authoring/session UI state and persistent local-file management. Every editor view reads or writes one Match draft; native V5 training-rally startup receives the exact automatic-preflight snapshot. TrainingLab never creates formal V5 result/replay artifacts or Career data.

**Tech Stack:** Unity 6000.3.20f1, C#, UI Toolkit, JsonUtility DTOs, atomic local-file replacement, NUnit EditMode and PlayMode.

**Route:** High-risk. Preserve V4 Career/formal/3v3 behavior and validate native V5/TrainingLab boundaries in stages.

**Supersedes:** `docs/superpowers/plans/2026-08-08-training-lab-design-fidelity-implementation-plan.md` and the TrainingLab portions of `docs/superpowers/plans/2026-08-01-training-lineup-position-fault-v5-implementation-plan.md`. Source specification: `docs/superpowers/specs/2026-08-08-training-lab-unified-workbench-design.md`.

---

## Invariants

- Match owns players, roles, rotations, positions, court/serve limits, ball constraints, position faults, trajectory integration, validation, frozen input and native V5 rally execution.
- TrainingLab owns UI page/view/tool state, selected object, dirty state, local catalog, leave modal and bookmark presentation only.
- Opening a standard template creates an in-memory local copy. Only Save creates or overwrites a file under Application.persistentDataPath/TrainingLab/Scenarios.
- Save is explicit and atomic. Dirty Return, Switch and Close offer Save / Discard / Cancel. Running disables editing and leaving.
- Home/Away equivalent formations are 180-degree point-symmetric around court centre; every lateral/depth rule uses the same team-local transform.
- Player positioning is top-view only. Serve top edits X/Z, Serve side edits Z/Y, and both views synchronize one six-axis state. 3D preview cannot mutate Match data.
- Entering Preflight automatically validates and freezes. There is no standalone Validation click.
- Administrator overrides use the twelve V5 base attributes plus `DominantHandV5`, affect actual TrainingLab runtime ability/geometry/contact behavior, and never mutate the base V5 context.
- Every TrainingLab run stops after one rally and emits `TrainingRallyOutcomeV1`; it never emits `MatchResultV5`, `MatchReplayV5` or a Career report.
- Existing V4 Career, formal V4, 3v3, save/recovery and replay behavior remains byte/behavior compatible.

## File Map

| File | Responsibility |
| --- | --- |
| Create Assets/Volleyball/Match/Runtime/Domain/PreServe/MatchPlayerPoseDraftV1.cs | Mutable Match player pose |
| Create Assets/Volleyball/Match/Runtime/Domain/PreServe/MatchSetupDraftV1.cs | Rotations, poses, serving side, ball state |
| Create Assets/Volleyball/Match/Runtime/Domain/PreServe/MatchSetupEditorV1.cs | Match-owned edits, clamps, faults and validation |
| Create Assets/Volleyball/Match/Runtime/Domain/PreServe/MatchSetupSnapshotV1.cs | Frozen startup input and deterministic hash |
| Create Assets/Volleyball/Match/Runtime/Domain/PreServe/TrainingPlayerAttributeOverrideV2.cs | V5 12-base-attribute and dominant-hand scenario override |
| Create Assets/Volleyball/Match/Runtime/Domain/PreServe/TrainingRallyStartV5.cs | Match-owned frozen native-V5 training startup envelope |
| Create Assets/Volleyball/Match/Runtime/Domain/PreServe/TrainingRallyOutcomeV1.cs | Session-only one-rally result and diagnostic evidence |
| Create Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioPresetV2.cs | V5-backed built-in template asset and explicit V1 rejection |
| Create Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabLocalScenarioV2.cs | V5-backed file envelope and UI session data |
| Create Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabLocalScenarioRepositoryV2.cs | Persistent catalog, V1 diagnostics and atomic V2 persistence |
| Create Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLab3DPreviewWindowV1.cs | Read-only 3D modal |
| Modify FormalSixVsSixRallyBootstrap.cs, PhysicalMatchRallyDirector.cs | Enter native V5 core with the frozen setup and stop after one rally |
| Modify PrototypePlayerAgent.cs and player presentation/contact geometry | Apply effective V5 attributes, height and dominant hand to training agents |
| Replace TrainingScenarioRuntimeAdapterV1.cs usage | Remove V4 projection from the consolidated TrainingLab path |
| Modify TrainingScenarioDraftStoreV1.cs, TrainingScenarioLabController.cs | Template adaptation, local copies, dirty/leave flow |
| Modify TrainingScenarioLab.uxml, TrainingScenarioLab.uss, TrainingScenarioLabView.cs | Hub and page workbench |
| Create MatchSetupEditorV1Tests.cs, MatchSetupSnapshotV1Tests.cs, TrainingLabLocalScenarioRepositoryV2Tests.cs | New focused unit tests |
| Modify TrainingScenarioLabSceneTests.cs, TrainingScenarioLabControllerTests.cs, TrainingScenarioLabPlayModeTests.cs | Page and interaction verification |

## Compatibility and migration policy

- Keep `TrainingScenarioV1`, V4 Career/formal startup, V4 result/replay, V4 recovery and the 3v3 compatibility scene available only to their existing callers.
- No consolidated TrainingLab production path may reference `MatchContextV4`, `DominantHandV4`, `PhysicalBaseAttributesV4`, `TechnicalBaseAttributesV4`, `MatchAttributeDerivationV4`, `InitializeV4` or V4 eligibility.
- Regenerate committed built-in TrainingLab templates as V2/V5-backed assets. Do not silently convert V1/V4-backed assets.
- Local V1 files appear unavailable with an unsupported-version diagnostic and a delete/recreate action. Preserve their bytes until the user deletes them.
- Rollback reverts only this plan's TrainingLab commits. It never downgrades or reinterprets V2 data as V1.

## 2026-08-18 Corrective Reopen: Hub, Rotation and Positioning

The 2026-08-17 automated checkpoint proved controller and runtime behavior but did not prove the approved
user interactions. User acceptance found a serious gap: technical tests passed while the Rotation and
Positioning workflow was not usable as specified. Tasks 0--2 and 6--8 remain frozen; Tasks 3--5 and Task 9
are reopened through the corrective tasks below. Execute **R0 next**. Do not resume Task 9 or describe the
workbench as automatically validated until R0--R4 pass.

### Corrective invariants

- `ShowWorkbench(entryKey)` must consume the requested identity. It may never reopen the current controller
  while pretending to create a new standard working copy.
- Opening `builtin:standard-rotation`, `builtin:home-serve`, `builtin:away-serve` or
  `builtin:attribute-override` creates a fresh local working copy with zero position faults. Only
  `builtin:position-fault-home` and `builtin:position-fault-away` intentionally begin invalid, on the named
  side.
- A template open creates a new local ID and independent Match draft. A local open loads that exact local ID.
  Neither route mutates or aliases the previously open draft.
- Rotation and Positioning acceptance requires actual UI Toolkit pointer/click events. Direct controller calls
  remain unit evidence but cannot satisfy the user-interaction gate.
- Positioning never presents the ball as the selected editable position. Entering Positioning selects a stable
  on-court player when no player is already selected.
- A position-fault gate is acceptable only when the page exposes the facts and controls required to correct it:
  both players and slots, relation, direction, focus, court drag and selected-player ruler drag.
- Do not add one-click auto-correction, a fault bypass, a second position model or UI-owned legality rules.
  Continue using `MatchSetupEditorV1`, `PositionFaultEvaluatorV1` and the existing team-local transform.

### Corrective file map

| File | Corrective responsibility |
| --- | --- |
| `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs` | Consume entry identity; bind real pointer gestures; render Rotation/Positioning feedback |
| `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml` | Formal rotation court, interactive ruler points and fault-card host |
| `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss` | Fixed rotation slots, ruler/focus markers, relation/correction styles and bounded inspector layout |
| `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabWorkbenchControllerV2.cs` | Stable Positioning selection and existing Match-command orchestration only |
| `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioCatalogV2.cs` | Preserve the six named template semantics; no new template schema |
| `Assets/Volleyball/Match/Tests/EditMode/TrainingLabV2BoundaryTests.cs` | Explicit legal/intentional-fault template matrix |
| `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs` | Selection, focus and gate state contracts |
| `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabSceneTests.cs` | Required interactive nodes and absence of inert placeholders |
| `Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs` | Hub routing, real pointer gestures, correction-to-Serve and 1920x1080 layout |

## Task R0: Reproduce the User Failures and Reset Acceptance

**Outcome:** Failing tests demonstrate stale-template reuse and the missing Rotation/Positioning interactions
before production code changes.

- [x] **Step 1: Add a template identity and legality matrix.**

    In `TrainingLabV2BoundaryTests`, assert all six IDs load their matching V2 asset and a fresh Match draft.
    Assert the four ordinary templates have zero `EvaluatePositionFaults()` results. Assert each intentional
    fault template reports at least one fault only for its named side. Assert two opens of one built-in template
    have different local IDs and independent mutable drafts.

- [x] **Step 2: Add a failing stale-template PlayMode test.**

    Open `builtin:position-fault-home`, return to the Hub, click the actual
    `hub-new-from-standard-button`, and assert the new workbench displays `标准轮转`, has a different local ID,
    starts at Rotation, and has zero position faults. Do not call the controller as a substitute for the button.

- [x] **Step 3: Add failing gesture and feedback tests.**

    Use actual UI Toolkit pointer/click events to prove: a same-team Rotation drag swaps the rendered/current
    slots; a cross-team and blank drop make no change; a selected Positioning player exposes two ruler points;
    a fault card click focuses exactly the two related tokens and ruler markers; correcting the fault enables
    and clicking `positioning-next-button` enters Serve.

- [x] **Step 4: Record the expected red evidence.**

    Run only the new EditMode/PlayMode filters. Expected failure locations are entry routing in
    `ShowWorkbench`, absent ruler points/callbacks, absent fault cards/relation line/focus rendering, and missing
    gesture coverage. A test that passes without dispatching UI events is invalid evidence.

## Task R1: Make Scenario Entry Identity Authoritative

**Outcome:** Every Hub or public entry action opens the requested template/local working copy exactly once.

- [x] **Step 1: Centralize entry routing without adding another data model.**

    Make `ShowWorkbench(entryKey)` parse only `builtin:<scenarioId>` and `local:<localId>`, delegate to the
    existing `OpenTemplate` or `OpenLocal` path, and reject malformed/unknown prefixes with a stable exception.
    The `hub-new-from-standard-button` must route to `builtin:standard-rotation`. Keep one event-subscription
    owner when replacing controller/runtime instances; the disposed controller must not receive later renders.

- [x] **Step 2: Preserve working-copy and leave semantics.**

    Each built-in open creates a unique unsaved `TrainingLabLocalScenarioV2`; each local open loads the exact
    saved DTO. Switching cannot alias or mutate the prior Match draft. Existing Save/Discard/Cancel and
    running-state leave blocks remain authoritative.

- [x] **Step 3: Pass the R0 identity/legality tests.**

    In addition to the stale fault-to-standard sequence, cover ordinary-to-ordinary switching, local reopen,
    malformed entry rejection and two consecutive standard opens. The setup hash may match for identical
    templates; the local identity and object ownership must not.

## Task R2: Restore the Complete Rotation Interaction

**Outcome:** Rotation is a formal-court six-position editor whose real pointer gestures preserve Match rotation
semantics.

- [x] **Step 1: Render fixed formal-court slots.**

    Replace the free wrapping grids with one formal-court presentation containing Home/Away fixed slots in
    three rows and two columns. Facing the net, right-back is slot 1 and numbering proceeds counter-clockwise
    through slot 6. Reuse the existing court/net constants and do not create a second rotation geometry model.

- [x] **Step 2: Render complete, localized card identity.**

    Every card shows player display name, jersey, registered role and current slot. Map the existing V5
    registered-position enum to stable Chinese presentation text in the View only; never edit the registered
    role or copy it into TrainingLab state.

- [x] **Step 3: Make pointer exchange deterministic.**

    Capture pointer/drag source on a card, resolve the drop target, call
    `MatchSetupEditorV1.ExchangeRotation` only for two slots on the same team, and always clear drag state on
    up/cancel/detach. Cross-team, empty-court and outside-board drops leave both rotations byte-for-byte
    unchanged. Rendered slots must update from the Match draft after a successful exchange.

- [x] **Step 4: Verify lock/reopen through UI.**

    Click the real confirm button to enter Positioning; click reopen to return to Rotation; exchange and relock;
    assert player coordinates are preserved and position-fault relations are recomputed for the new slot
    identities. Capture a new 1920x1080 Rotation image only after the gesture test passes.

## Task R3: Complete Positioning Rulers, Fault Focus and the Serve Gate

**Outcome:** A user can understand and correct every position fault, then advance to Serve without hidden or
dead-end state.

- [x] **Step 1: Establish a valid Positioning selection.**

    On entry from confirmed Rotation, preserve an already selected on-court player; otherwise select Home slot
    1. Never use `ball` as the Positioning exact-input subject. Show X/Z only, with name, jersey, registered role
    and current slot derived from the Match setup/context.

- [x] **Step 2: Implement selected-player ruler points.**

    Replace inert `picking-mode="Ignore"` rulers with labeled/ticked rulers plus one selected-player point per
    axis. Horizontal drag calls `SetPlayerDepthFromHorizontalRuler`; vertical drag calls
    `SetPlayerLateralFromVerticalRuler`. Use pointer capture and clear drag state on up/cancel. Court, ruler and
    exact-field edits must converge to the same snapped Match coordinate and never change Y or the other axis.

- [x] **Step 3: Render complete Match fault facts.**

    For every `PositionFaultV1`, render an inspector card naming side, rule, both player names, both slots and a
    concrete side-local correction direction. Clicking a card calls `FocusPositionFault(index)` without
    mutation. Consume `FocusedPlayerIds` in the View so only the associated tokens and ruler markers receive
    focus styling.

- [x] **Step 4: Separate relation and correction overlays.**

    Render both participants as fault tokens, a red dashed line for the violated relation, and a distinct blue
    shortest-legal-correction arrow for the violating player. Both overlays use the same team-local transform
    and Match-provided facts; evidence coordinates remain world-space and unchanged.

- [x] **Step 5: Prove the gate is correct and escapable.**

    A standard working copy enters Positioning with zero faults and an enabled next action. An intentional
    fault template shows the exact fault and disables the action. After the user corrects it through court or
    ruler drag, fault cards/overlays clear, the button enables, and a real click enters Serve. No bypass or
    auto-correction button is added.

## Task R4: Corrective Validation, Visual Acceptance and Evidence

**Outcome:** Technical and user-goal gates both pass before Task 9 resumes.

- [x] **Step 1: Run focused corrective EditMode.**

    Run `TrainingLabV2BoundaryTests`, `TrainingLabCourtProjectionV1Tests`,
    `TrainingScenarioLabControllerTests` and `TrainingScenarioLabSceneTests`. Record a fresh XML; do not reuse
    the 2026-08-17 counts.

- [x] **Step 2: Run focused corrective PlayMode.**

    Run `TrainingScenarioLabPlayModeTests` with the new Hub click, Rotation pointer drag, Positioning court/ruler
    drag, fault-card focus and correction-to-Serve cases. Assert setup/local identities before and after each
    action so a visual-only animation cannot satisfy the test.

- [ ] **Step 3: Perform real 1920x1080 mouse acceptance.**

    Save new evidence under `TestResults/TrainingLab/VisualAcceptance/2026-08-18-corrective/` without overwriting
    the rejected 2026-08-17 captures. Required images: Hub after fault-template return, fresh standard Rotation,
    same-team exchange, legal Positioning with ruler points, focused position fault with card/relation/arrow,
    corrected legal Positioning, and Serve reached by the page button. Separately verify cross-team/blank drop
    rejection with real mouse input.

- [x] **Step 4: Freeze code and resume the existing Task 9 gates.**

    After R0--R3 code is frozen, run one complete EditMode suite, the native V5 TrainingLab PlayMode path, the
    existing V5 Career regression and 3v3 isolation smoke required by Task 9. Then recapture the remaining
    Preflight/Run/dirty-leave evidence. Windows x64 IL2CPP build/manual acceptance remains pending when Windows
    Build Support or a physical Windows environment is unavailable.

- [x] **Step 5: Independent review and status correction.**

    Use at most one read-only independent review focused on entry identity, actual pointer wiring, position-fault
    correction reachability and test authenticity. Update the change record and active handoff with exact fresh
    counts and both the automated and manual gate status. Do not mark complete while Windows or any required
    user-goal acceptance remains pending.

- [ ] **Step 6: Hygiene and corrective commit.**

    Run `git diff --check`, remove only Unity-generated `Assets/InitTestScene*.unity` and matching `.meta`, and
    stage only the corrective TrainingLab/tests/evidence docs. Preserve inherited Career/AI/older-plan changes,
    push the feature branch, create a PR for Bootstrap/shared-area integration, and never merge `main` without
    explicit authorization.

## Task 0: Establish the V5 TrainingLab Data and Isolation Boundary

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/PreServe/TrainingPlayerAttributeOverrideV2.cs`
- Create: `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioPresetV2.cs`
- Modify: TrainingLab catalog/store/canonicalization and built-in assets
- Test: Training scenario, static boundary and unsupported-version EditMode tests

- [ ] **Step 1: Add failing V4-exclusion and version tests.**

    Assert the consolidated TrainingLab source graph contains no V4 context, attribute, derivation, startup, eligibility or evidence dependency. Assert V1 files are reported unavailable without mutation and V2 templates load a V5 roster/context identity.

- [ ] **Step 2: Implement the exact override schema.**

    Store integer Strength, HeightMillimeters, Jump, Movement, Reaction, Coordination, Attack, Defense, CourtIq, Block, Serve and Set plus `DominantHandV5`. Use `[0, 10000]` for basis-point attributes and `[1400, 2300]` for height. Support absent per-field overrides, clear-one-field and reset-player operations.

- [ ] **Step 3: Define the team-local court transform.**

    With court centre at world origin, map Home `(lateral, depth)` to `(lateral, -depth)` and Away to `(-lateral, depth)`. Use this transform for default placements, editor projection, correction arrows and position-fault comparisons. Add Home/Away equivalent-vector tests, including equality, each relation and stable multi-fault ordering.

- [ ] **Step 4: Regenerate built-in templates and reject V1 input.**

    Replace committed V4-backed TrainingLab assets with V2/V5-backed templates. Keep the old loader only to return a stable unsupported-version diagnostic; do not construct a partial V5 draft from V1 fields.

- [ ] **Step 5: Run focused data/boundary tests.**

    Expected evidence: V2 template round-trip/hash passes, V1 rejection preserves source bytes, V4-exclusion scan passes, and formal V4/Career/3v3 smoke tests remain unchanged.

## Task 1: Create the Match-Owned Pre-Serve Boundary

**Files:**
- Create: Assets/Volleyball/Match/Runtime/Domain/PreServe/MatchPlayerPoseDraftV1.cs
- Create: Assets/Volleyball/Match/Runtime/Domain/PreServe/MatchSetupDraftV1.cs
- Create: Assets/Volleyball/Match/Runtime/Domain/PreServe/MatchSetupEditorV1.cs
- Create: Assets/Volleyball/Match/Runtime/Domain/PreServe/MatchSetupSnapshotV1.cs
- Create: Assets/Volleyball/Match/Runtime/Domain/PreServe/TrainingRallyStartV5.cs
- Create: Assets/Volleyball/Match/Runtime/Domain/PreServe/TrainingRallyOutcomeV1.cs
- Modify: Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs
- Modify: Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs
- Modify: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingSimulationControllerV1.cs
- Test: Assets/Volleyball/Match/Tests/EditMode/MatchSetupEditorV1Tests.cs
- Test: Assets/Volleyball/Match/Tests/EditMode/MatchSetupSnapshotV1Tests.cs
- Test: Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioRuntimePlayModeTests.cs

- [ ] **Step 1: Write failing editor tests.**

    Test same-team slot swap, invalid slot rejection, one-decimal player snapping, own-half clamp, Home/Away serve-band clamp, finite velocity validation, point-symmetric Home/Away legality, equal-projection legality, shortest fault correction, exact override hashing and stable frozen hash.

    public void ExchangeRotation_ChangesOnlySameTeamSlots()
    {
        var draft = MatchSetupFixture.ValidDraft();
        var editor = new MatchSetupEditorV1(draft);
        editor.ExchangeRotation(TeamSide.Home, 1, 4);
        Assert.That(draft.HomeRotation[0],
            Is.EqualTo(MatchSetupFixture.HomeSlotFour));
        Assert.That(() => editor.ExchangeRotation(TeamSide.Home, 1, 7),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

- [ ] **Step 2: Run tests and verify the absent Match types fail compilation.**

    UNITY_BIN="/Applications/Unity/Unity-6000.3.20f1/Unity.app/Contents/MacOS/Unity"
    "$UNITY_BIN" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
      -testFilter "Volleyball.EditModeTests.MatchSetupEditorV1Tests;Volleyball.EditModeTests.MatchSetupSnapshotV1Tests" \
      -testResults "$PWD/TestResults/TrainingLab-MatchSetup-red.xml" \
      -logFile "$PWD/TestResults/TrainingLab-MatchSetup-red.log"

    Expected: errors for MatchSetupDraftV1, MatchSetupEditorV1 and MatchSetupSnapshotV1.

- [ ] **Step 3: Implement mutable Match setup.**

    public sealed class MatchSetupDraftV1
    {
        public MatchContextV5 BaseContext { get; }
        public TeamSide FirstServingSide { get; set; }
        public List<PlayerId> HomeRotation { get; }
        public List<PlayerId> AwayRotation { get; }
        public List<MatchPlayerPoseDraftV1> Players { get; }
        public SimVector3 BallPosition { get; set; }
        public SimVector3 BallVelocity { get; set; }
        public Dictionary<PlayerId, TrainingPlayerAttributeOverrideV2> AttributeOverrides { get; }
        public bool RotationLocked { get; set; }
    }

    Constructor/factory rejects unknown player IDs, duplicate players and rotations other than six IDs per side. Keep UI metadata out.

- [ ] **Step 4: Implement MatchSetupEditorV1.**

    Implement exactly ExchangeRotation, SetPlayerPosition, SetBallPosition, SetBallVelocity, EvaluatePositionFaults, Validate and Freeze. Use CourtBuilder, PositionFaultEvaluatorV1, SimulatedBall and existing Match validation; no TrainingLab-owned limits.

- [ ] **Step 5: Implement MatchSetupSnapshotV1 and native V5 startup handoff.**

    Freeze deep copies in stable player/slot ordering. Use invariant numeric formatting to calculate the setup hash. Build `TrainingRallyStartV5` from the immutable snapshot and base V5 context identity. Add `FormalSixVsSixRallyBootstrap.InitializeTrainingRallyV5` and a matching director entry that delegates into the existing V5 `InitializeCore`, V5 authority, AI, physics, contact and referee path.

    Apply frozen poses and effective TrainingLab-only V5 attributes before the existing serve-contact position-fault gate. Native startup must not call default tactical placement over the snapshot or replace its ball state. A fault resolves exactly one point before serve/contact/AI evidence; a legal setup launches the frozen ball and follows the normal V5 rally.

    Stop after the first resolved rally and create `TrainingRallyOutcomeV1` containing setup hash, seed, winning side, one-rally score delta, completion reason, touch count, ordered domain fault facts, bounded timeline and outcome hash. Never create or attach `MatchReplayRecorderV5`, `MatchResultV5`, `MatchReplayV5` or a Career report.

- [ ] **Step 6: Run focused tests and commit.**

    git add Assets/Volleyball/Match/Runtime/Domain/PreServe Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingSimulationControllerV1.cs Assets/Volleyball/Match/Tests/EditMode/MatchSetupEditorV1Tests.cs Assets/Volleyball/Match/Tests/EditMode/MatchSetupSnapshotV1Tests.cs Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioRuntimePlayModeTests.cs
    git commit -m "feat: run frozen training setups through native v5"

    Expected: new setup tests and TrainingLab native-V5 PlayMode pass; a static boundary test proves no TrainingLab output enters Shared/Career/formal-V5 result/replay.

## Task 2: Persist Local Working Copies and Dirty Leave Decisions

**Files:**
- Create: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabLocalScenarioV2.cs
- Create: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLabLocalScenarioRepositoryV2.cs
- Modify: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioDraftStoreV1.cs
- Modify: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabController.cs
- Test: Assets/Volleyball/Match/Tests/EditMode/TrainingLabLocalScenarioRepositoryV2Tests.cs
- Test: Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs

- [ ] **Step 1: Write failing repository tests using an injected temporary root.**

    public void SaveThenReload_PreservesMatchHashAndUiSession()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var repository = new TrainingLabLocalScenarioRepositoryV2(root);
        var local = TrainingLabLocalScenarioV2.Create(
            "local-1", MatchSetupFixture.ValidDraft(), "Positioning", "Top",
            "MoveBall", "home-1");
        repository.Save(local);
        var reloaded = new TrainingLabLocalScenarioRepositoryV2(root).Load("local-1");
        Assert.That(reloaded.MatchSetupHash, Is.EqualTo(local.MatchSetupHash));
        Assert.That(reloaded.ActiveStep, Is.EqualTo("Positioning"));
    }

    Also test overwrite by same ID, malformed JSON diagnostic, V1 unsupported-without-rewrite behavior, replacement failure retaining prior file, and discard retaining saved bytes.

- [ ] **Step 2: Define JSON envelope.**

    [Serializable]
    public sealed class TrainingLabLocalScenarioFileV2
    {
        public int formatVersion;
        public string localId;
        public string displayName;
        public string createdUtc;
        public string modifiedUtc;
        public string matchSetupJson;
        public string matchSetupHash;
        public string activeStep;
        public string activeView;
        public string activeTool;
        public string selectedObjectId;
        public string bookmarksJson;
    }

    Match owns matchSetupJson serialization/deserialization and its hash validation. UI fields never reconstruct gameplay data.

- [ ] **Step 3: Implement atomic repository overwrite.**

    Root is Path.Combine(Application.persistentDataPath, "TrainingLab", "Scenarios"). Write UTF-8 to <id>.json.tmp, close it, then File.Replace destination when supported; otherwise use validated backup/move fallback. Never remove a valid old file before replacement is ready.

- [ ] **Step 4: Add controller dirty and leave APIs.**

    public enum TrainingLabLeaveDecisionV1 { Save, Discard, Cancel }
    public bool IsDirty { get; }
    public TrainingLabLeaveRequestV1 RequestLeaveToHub();
    public TrainingLabLeaveRequestV1 RequestSwitch(string entryKey);
    public TrainingLabLeaveRequestV1 ResolveLeave(TrainingLabLeaveDecisionV1 decision);
    public void SaveCurrentLocalScenario();

    Compare current Match draft hash to last saved hash. A built-in entry creates in-memory local state. Running returns blocked without showing a modal.

- [ ] **Step 5: Run persistence tests and commit.**

    git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab Assets/Volleyball/Match/Tests/EditMode/TrainingLabLocalScenarioRepositoryV2Tests.cs Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs
    git commit -m "feat: persist training lab local scenarios"

    Expected: Save/restart retains Match hash and UI data; failed Save remains dirty; Discard never writes.

## Task 3: Build Scenario Hub and Canvas-First Shell

**Files:**
- Modify: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml
- Modify: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss
- Modify: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs
- Test: Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabSceneTests.cs
- Test: Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs

- [ ] **Step 1: Write failing structural tests.**

    Require scenario-hub, continue-scenarios, standard-scenarios, workbench-shell, stage-rail, workbench-content, contextual-inspector, bottom-action-bar, save-button, return-to-hub-button, more-button and advanced-settings. Assert left-panel and editor-controls are absent.

- [ ] **Step 2: Add hub and workbench UXML.**

    Hub has separate local/template card groups plus hub-new-from-standard-button. Workbench has compact top status, stage rail, canvas + inspector and one-row bottom action bar. Do not retain a permanent scenario sidebar.

- [ ] **Step 3: Implement screen mode rendering.**

    Add _showingScenarioHub, ShowWorkbench(key), ShowScenarioHub(), RenderScenarioHub() and RenderScreenMode(). Template cards create local copies; local cards load their files; labels are 打开 and 继续编辑.

- [ ] **Step 4: Implement constrained 1920x1080 USS.**

    Hub has two wrapping card groups. Inspector is 344px and min-height:0; only context-scroll scrolls. Canvas grows while formal court retains its ratio. Bottom bar remains one row. No region-crossing overflow:visible.

- [ ] **Step 5: Add PlayMode no-overlap test.**

    At 1920x1080 open a workbench card. Assert board, inspector and action bar have non-zero world bounds and pairwise no overlap; assert context viewport is inside inspector.

- [ ] **Step 6: Run Scene/Edit/Play tests and commit.**

    git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabSceneTests.cs Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs
    git commit -m "feat: add training lab scenario hub shell"

## Task 4: Implement Rotation Page

**Files:** Match setup editor, TrainingLab View/UXML/USS, controller and PlayMode tests.

- [ ] **Step 1: Write failing page tests.**

    Assert unlocked setup opens Rotation; same-team card drag swaps Match slots; cross-team or empty drop cancels; confirmation enters Positioning; reopen preserves coordinates and relock recomputes faults.

- [ ] **Step 2: Render three rows and two columns per team.**

    Facing net, right-back is 1; count counter-clockwise; middle-back is 6. Card shows name, registered role and slot; no separate meaningless index.

- [ ] **Step 3: Implement pointer swap.**

    Source and target must be cards from one team. Invoke MatchSetupEditorV1.ExchangeRotation(side, firstSlot, secondSlot). Reject every other drop without altering the draft.

- [ ] **Step 4: Render rotation context and action.**

    Show both rotations, membership errors, confirm/reopen and primary 确认并锁定位次. Block Positioning until Match validates both six-player permutations.

- [ ] **Step 5: Run tests, capture 02-rotation.png, commit.**

    git add Assets/Volleyball/Match/Runtime/Presentation/TrainingLab Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs
    git commit -m "feat: add match backed rotation page"

## Task 5: Implement Positioning, External Rulers and Fault Focus

**Files:** TrainingLabCourtProjectionV1.cs, Match editor, View/UXML/USS, projection/controller/PlayMode tests.

- [ ] **Step 1: Write failing precision and side-local semantics tests.**

    Court drag and horizontal ruler drag give the same final Match coordinate; vertical ruler changes only its axis; values snap 0.1m; players stay in their own half; fault focus includes precisely associated participants. For every legal Home formation, the Away formation produced by `(x, z) -> (-x, -z)` must have identical legality and relation identities in team-local coordinates. A net-only mirror must fail at least one lateral-orientation vector that the point rotation passes.

- [ ] **Step 2: Extract one formal court presenter.**

    Positioning and Serve top view use one 18m x 9m court, net, three-meter lines, Home/Away and external rulers derived from Match/CourtBuilder constants. Player drag exists only in Positioning top view. Side view and 3D preview do not register player mutation callbacks.

- [ ] **Step 3: Add selected-player ruler points.**

    Horizontal ruler maps net-centered Z: net zero and both sides increase to 9m. Vertical ruler maps X: bottom zero and upward increase to 9m. Point userData holds player ID and axis; drag changes only that component through Match editor.

- [ ] **Step 4: Render Match fault overlays.**

    Mark both participants red, relation dashed red, correction arrow blue. Inspector card names players, slots, relation and direction; click focuses tokens/rulers with no mutation. Correction direction is derived in side-local coordinates and projected back to world/UI coordinates through the same transform used by the evaluator.

- [ ] **Step 5: Enforce page gate.**

    Continue to Serve is disabled whenever EvaluatePositionFaults is non-empty and explicitly states all position faults need correction.

- [ ] **Step 6: Run tests, capture 03-positioning.png and 04-position-fault-focus.png, commit.**

    git add Assets/Volleyball/Match/Runtime/Domain/PreServe Assets/Volleyball/Match/Runtime/Presentation/TrainingLab Assets/Volleyball/Match/Tests/EditMode/TrainingLabCourtProjectionV1Tests.cs Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs
    git commit -m "feat: add match backed positioning editor"

## Task 6: Implement Serve Top, Side, Vector, Trajectory and 3D Modal

**Files:**
- Create: Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingLab3DPreviewWindowV1.cs
- Modify: Match setup editor, View, UXML, USS and tests.

- [ ] **Step 1: Write failing six-axis tests.**

    Edit X/Z top, Z/Y side, VX/VZ top speed, VZ/VY side speed; assert the one Match draft has all expected values. Assert serving-side change moves legal 3m band and rejected edit restores prior legal value.

- [ ] **Step 2: Add co-located view selector.**

    Serve canvas has mutually exclusive 俯视 and 侧视. Top uses formal court with no height. Side shows ground, net, end line and height scale. Never render both editable views at once. Neither view may edit player positions in Serve.

- [ ] **Step 3: Add tool exclusivity.**

    移动球 enables yellow ball/XYZ. 调整速度 enables red endpoint/VX/VY/VZ. 查看轨迹 disables both. Every change calls Match editor and surfaces its rejected-value reason.

- [ ] **Step 4: Reuse Match integration.**

    Use existing BallIntegrator and Match parameters. Top is ground projection; side is Z/Y; no TrainingLab projectile equation.

- [ ] **Step 5: Implement read-only 3D modal.**

    Support orbit, zoom, reset and named bookmark save/load. Pointer callbacks update camera only; none may call Match draft mutations. Close restores prior 2D view.

- [ ] **Step 6: Run tests, capture 05-serve-top.png, 06-serve-side.png and 07-serve-3d-modal.png, commit.**

    git add Assets/Volleyball/Match/Runtime/Domain/PreServe Assets/Volleyball/Match/Runtime/Presentation/TrainingLab Assets/Volleyball/Match/Tests/EditMode/MatchSetupEditorV1Tests.cs Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs
    git commit -m "feat: add shared top side and readonly 3d serve views"

## Task 7: Implement V5 Administrator Overrides End to End

**Files:**
- Modify: Match setup draft/editor/snapshot and V5 training startup projection
- Modify: TrainingScenarioLabView.cs, UXML and USS advanced-settings inspector
- Modify: PrototypePlayerAgent.cs, StickFigureRig/contact geometry only through existing player-owned update APIs
- Test: Match setup, controller, ability/contact and TrainingLab PlayMode tests

- [ ] **Step 1: Write failing UI-to-runtime tests.**

    Cover editing each of the twelve V5 attributes and dominant hand, per-field clear, reset-player, freeze/hash, and re-edit after return. A PlayMode run must prove changed attack/block reach, visible/contact height and left/right serve-or-attack contact selection on the actual runtime agent. A label/hash-only change is a failure.

- [ ] **Step 2: Render one selected-player override inspector.**

    Show original, override and effective values. Persist canonical basis points and height millimetres; UI controls may display 0–100 values only through a reversible projection. Highlight only fields with explicit overrides. Keep name, jersey and registered role read-only.

- [ ] **Step 3: Apply effective V5 attributes at the TrainingLab startup boundary.**

    Derive the runtime `MatchAbilitySnapshot` from base V5 values plus scenario overrides before V5 authority begins. Update player-owned presentation/contact geometry from effective height and reach. Propagate `DominantHandV5` to contact selection. Do not mutate the base `MatchContextV5`, its player snapshots or fingerprints.

- [ ] **Step 4: Prove isolation.**

    Static tests reject TrainingLab override types in Shared, Career, formal V5 result/replay and V4 production paths. Runtime tests assert a TrainingLab run emits only `TrainingRallyOutcomeV1`; the same base context used by an ordinary formal V5 run retains its original attributes and formal fingerprints.

- [ ] **Step 5: Run focused override/UI/runtime tests and commit.**

    Expected: UI → draft → freeze/hash → native V5 training agent → geometry/contact behavior passes, reset restores base behavior, and isolation tests pass.

## Task 8: Implement Automatic Preflight, Native V5 Training Run, Result and Leave Modal

**Files:** startup bridge, Controller/View/UXML/USS and controller/PlayMode tests.

- [ ] **Step 1: Write failing identity and isolation tests.**

    Assert entering Preflight automatically freezes one snapshot hash; Run passes the exact snapshot/hash to native V5 training startup; invalid preflight creates no director; rerun uses the same snapshot. Assert legal runs use `_matchContextV5`, V5 ability projection and V5 eligibility, but attach no formal V5 replay recorder and emit no Shared/Career artifact.

- [ ] **Step 2: Build Preflight.**

    Entering or re-entering Preflight calls Match validation once and either freezes the immutable snapshot or shows blockers/warnings. Show its hash when valid. Every 返回修正 moves to Rotation, Positioning/fault focus, Serve/top-or-side or Advanced Settings. Remove duplicate `校验` controls and any controller state that exists only to await a manual validation click.

- [ ] **Step 3: Build Running and Result.**

    Lock Save, switch, close and editing. Reuse pause/resume/single-step controls without changing authority state. Stop after one resolved rally. Result renders `TrainingRallyOutcomeV1`: winner, one-rally score delta, reason, position-fault facts, touch count, seed, setup/outcome hashes and bounded timeline. Do not wait for set completion.

- [ ] **Step 4: Implement Return-to-Edit/current action bar.**

    Return restores the in-memory local copy without overwriting the saved file. Bottom action maps Rotation→lock, Positioning→Serve, Serve→automatic Preflight, valid Preflight→run, Running→pause/resume, Completed→rerun, Faulted→return edit. There is no Preflight→validate action.

- [ ] **Step 5: Implement dirty-leave modal.**

    Return hub, switch and close invoke controller leave APIs. Save navigates only after atomic success; Discard reloads last saved DTO; Cancel does nothing. Never show modal while running.

- [ ] **Step 6: Run tests, capture 08-preflight.png, 09-running-result.png and 10-unsaved-leave.png, commit.**

    git add Assets/Volleyball/Match/Runtime/Domain/PreServe Assets/Volleyball/Match/Runtime/Presentation/TrainingLab Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs
    git commit -m "feat: run match setup snapshots through native v5 training flow"

## Task 9: Final Acceptance and Evidence

**Files:** docs/changes/2026-08-04-001-training-lineup-position-fault-v5.md, active handoff and tests only for discovered gaps.

- [x] **Step 1: Run final focused EditMode.**

    "$UNITY_BIN" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode \
      -testFilter "Volleyball.EditModeTests.MatchSetupEditorV1Tests;Volleyball.EditModeTests.MatchSetupSnapshotV1Tests;Volleyball.EditModeTests.TrainingLabLocalScenarioRepositoryV2Tests;Volleyball.EditModeTests.TrainingLabCourtProjectionV1Tests;Volleyball.EditModeTests.TrainingScenarioLabControllerTests;Volleyball.EditModeTests.TrainingScenarioLabSceneTests" \
      -testResults "$PWD/TestResults/TrainingLab-Unified-Final-EditMode.xml" \
      -logFile "$PWD/TestResults/TrainingLab-Unified-Final-EditMode.log"

- [x] **Step 2: Run final TrainingLab PlayMode.**

    "$UNITY_BIN" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode \
      -testFilter "Volleyball.PlayModeTests.TrainingScenarioLabPlayModeTests" \
      -testResults "$PWD/TestResults/TrainingLab-Unified-Final-PlayMode.xml" \
      -logFile "$PWD/TestResults/TrainingLab-Unified-Final-PlayMode.log"

- [x] **Step 3: Run affected native V5 and frozen V4 regressions.**

    Run focused native V5 startup/position-fault/result/replay tests, Career V5 aggregation tests, existing V4 Career/formal runner tests and the 3v3 compatibility PlayMode smoke. TrainingLab tests must assert V5 authority is used; V4 regression tests must assert the new startup envelope is unreachable. Do not describe any earlier XML as fresh evidence.

- [x] **Step 4: Run one complete EditMode suite after code freeze.**

    Run the repository's complete EditMode suite once after all runtime and UI changes are frozen. If a material runtime fix follows, rerun only the affected regression unless the fix crosses Shared/V4 compatibility boundaries and makes the complete result stale.

- [x] **Step 5: Perform 1920x1080 screenshot acceptance.**

    Capture 01-scenario-hub.png, 02-rotation.png, 03-positioning.png, 04-position-fault-focus.png, 05-serve-top.png, 06-serve-side.png, 07-serve-3d-modal.png, 08-preflight.png, 09-running-result.png and 10-unsaved-leave.png under TestResults/TrainingLab/VisualAcceptance/2026-08-17/. Reject legacy all-controls UI, overlap, clipped text, wrong court ratio, missing gate, a manual Validation control, side/3D player editing or editable 3D preview.

- [x] **Step 6: Verify a real restart and V1 rejection.**

    Save a V2 local copy, close Unity, reopen the scene and local card, compare Match setup hash, active page, active view/tool and selected object. Exercise malformed-file diagnostic, write failure and an untouched V1 file that remains unavailable without byte changes.

- [ ] **Step 7: Build and manually accept the Windows Player.**

    Run `Volleyball.Editor.TrainingScenarioLabWindowsDevelopmentBuild.Build` in batch mode and verify `Builds/Windows/training-lab-build-manifest.json`. On a physical Windows x64 Player, verify keyboard/mouse interaction, top/side edits, read-only 3D, V5 attributes, automatic preflight, legal rally, position-fault rally, pause/step/reset and deterministic rerun. If the environment is unavailable, record `待人工验收` and do not claim completion.

- [x] **Step 8: Record actual evidence.**

    Update docs/changes/2026-08-04-001-training-lineup-position-fault-v5.md with exact XML counts, screenshot paths, restart result and the still-pending Windows x64 IL2CPP Player status.

- [ ] **Step 9: Run hygiene and commit.**

    git diff --check
    git status --short
    git add Assets/Volleyball/Match/Runtime/Domain/PreServe Assets/Volleyball/Match/Runtime/Presentation/TrainingLab Assets/Volleyball/Match/Tests/EditMode Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs docs/changes/2026-08-04-001-training-lineup-position-fault-v5.md docs/handoffs/active/2026-08-01-training-lineup-position-fault-v5-implementation.md
    git commit -m "feat: complete match backed training lab workbench"

    Remove only Unity-generated Assets/InitTestScene*.unity and matching meta files. Do not stage unrelated Career/AI changes and do not merge main.

## Rollback and Integration

- Revert only commits from this plan if a data-consistency, runtime or visual gate fails. Do not revert existing native V5 position-fault, V4 Career/formal/3v3, AI or unrelated user changes.
- The active handoff remains active; this plan does not complete its existing V4 regression or Windows Player gates.

## Self-Review

- Coverage: Task 0 V5 data/isolation; Task 1 Match ownership/native V5 rally; Task 2 persistent local save/leave; Task 3 hub/shell; Task 4 rotation; Task 5 positioning/rulers/fault focus; Task 6 top/side/3D Serve; Task 7 administrator overrides; Task 8 automatic preflight/run/result; Task 9 regression/visual/restart/Windows evidence.
- No placeholders: each task names files, types, interaction behavior, tests and commit boundary.
- Types: Task 1 defines Match setup types before later use; Task 2 defines persistence before hub use; page labels match the approved specification.
