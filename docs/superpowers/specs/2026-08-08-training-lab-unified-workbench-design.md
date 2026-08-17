# TrainingLab Unified Workbench Design

- Date: 2026-08-08
- Status: confirmed consolidated design; implementation plan ready for review
- Scope: TrainingLab local authoring/persistence, native V5 pre-serve/runtime boundary, and UI Toolkit page redesign
- References:
  - `/Users/wys/Downloads/统一界面结构设计.png`
  - `/Users/wys/Downloads/统一界面结构设计-2.png`
  - `docs/handoffs/active/2026-08-01-training-lineup-position-fault-v5-implementation.md`

## Goal

Replace the TrainingLab's legacy parameter-editor layout with a page-based volleyball workbench. Developers select or create a local scenario, establish a legal rotation, edit real pre-serve positions and serve ball state, automatically preflight one frozen Match setup, and run one rally through the native V5 Match authority, AI, physics and referee path. The design preserves one source of truth for gameplay data, emits only TrainingLab-owned one-rally evidence, and lets local scenarios survive Unity restarts.

## Non-goals

- Do not change V4 Career, formal V4, 3v3 compatibility, persistence, result/replay, recovery or rule behavior.
- Do not add TrainingLab data to `MatchContextV5`, `MatchResultV5`, `MatchReplayV5` or Career reports.
- Do not create a TrainingLab-specific copy of player, court, serve, rotation, position-fault, ball physics, or validator rules.
- Do not add mid-rally insertion, a position-fault bypass, or editing of registered player identity, jersey, role, or Career attributes.
- Do not implement generated 3D player art or copy the reference images' visual effects literally.
- Do not continue a TrainingLab run into a full 25-point set.

## Authoritative Data Boundary

Match is the sole owner of all gameplay meaning:

- players, registered roles, on-court rotations, actual court positions, serving side, ball position, ball velocity, court geometry, serve zones, position faults, ball integration, validation, frozen startup input, native V5 rally authority and rally resolution.

TrainingLab owns only authoring/session presentation:

- page/navigation state, selected object, active 2D view/tool, pending save state, local file catalog, modal visibility, and named observation bookmarks.

The implementation adds a Match-owned TrainingLab boundary because the current `TrainingScenarioV1` path is V4-backed and must not remain on the consolidated path:

- `MatchSetupDraftV1`: mutable pre-serve input backed by a V5 roster/context identity, Match types and Match limits.
- `TrainingPlayerAttributeOverrideV2`: scenario-private Strength, Height, Jump, Movement, Reaction, Coordination, Attack, Defense, Court IQ, Block, Serve, Set and `DominantHandV5` values.
- `MatchSetupEditorV1`: Match-owned operations for rotation exchange, player position, serve ball position/velocity, clamping and validation.
- `MatchSetupSnapshotV1`: immutable validated input with a deterministic setup hash.
- `TrainingRallyStartV5`: native-V5 startup envelope containing the base V5 context identity plus the frozen TrainingLab setup and effective Match-only test attributes.
- `TrainingRallyOutcomeV1`: session-only one-rally outcome, ordered fault/contact/decision evidence and hashes. It is not a Shared result or replay contract and is never consumed by Career.

TrainingLab 2D top-down, side view, exact fields, and read-only 3D preview all read one `MatchSetupDraftV1`; only the two editable 2D views and exact fields may write it. UI coordinate projection has no game-rule authority. A snapshot created by automatic preflight must be the exact snapshot passed into native V5 training-rally startup.

`TrainingRallyStartV5` enters the same V5 director initialization, position-fault gate, V5 ability projection, AI, ball physics, contact and `ResolveRally` path as a native formal V5 rally. It differs only at the paid boundaries: it applies the frozen pre-serve setup and scenario-private effective attributes before the serve gate, stops after one resolved rally, and records `TrainingRallyOutcomeV1` instead of formal V5 result/replay artifacts.

All TrainingLab runs use this one training outcome path, whether or not administrator overrides are present. There is no second "official TrainingLab" mode.

## Global Information Architecture

### Scenario hub

The initial page is not a permanent left sidebar. It has two independent card groups:

- `继续编辑`: local persistent working copies.
- `标准情景`: read-only project templates.

Opening a standard scenario immediately creates an in-memory local working copy with a unique local ID. It becomes persistent only when the user clicks Save. Opening a local scenario loads and validates its local file. The hub also exposes `从标准轮转开始` for a new local working copy.

### Workbench shell

At 1920x1080 the workbench has exactly five regions:

| Region | Responsibility | Forbidden content |
| --- | --- | --- |
| Top status strip | scenario name, dirty/saved state, Save, return-to-hub, Help, More | page forms or execution controls |
| Stage rail | Rotation, Positioning, Serve, Preflight, Run, Result and active/blocked status | full monitor text |
| Primary canvas | formal court, players, ball, rulers, local annotations and immediate visual feedback | global metadata forms |
| Context inspector | only active page's details, field errors and next action; bounded vertical scrolling | hidden legacy all-controls stack |
| Bottom action bar | current selection, concise instruction and one main action; capped run evidence | duplicate navigation or full forms |

Low-frequency settings appear only in the closed-by-default `高级设置` drawer opened from More. They include display name, seed, rally metadata, tactics, administrator overrides, and bookmarks. Its own scroll never expands beyond the inspector viewport or obscures the primary action.

### Administrator overrides

- Selecting a player exposes original, override and effective values for the twelve V5 base attributes plus `DominantHandV5`.
- Each field can be overridden independently. The inspector supports clearing one field and restoring all overrides for one player; absent overrides derive from the scenario's base V5 roster.
- Basis-point attributes use the V5 range `[0, 10000]`; height uses `[1400, 2300]` millimetres. UI may display friendlier units but persists canonical integer values.
- Freeze includes overrides in the setup hash. Runtime projection derives an effective V5 `MatchAbilitySnapshot` for TrainingLab agents without mutating the base `MatchContextV5`.
- Height changes derived attack/block reach, visual scale and contact/block geometry. Dominant hand changes serve/attack contact selection and presentation. A value that is merely displayed or hashed but not consumed by runtime does not satisfy acceptance.
- Overrides, bookmarks and UI state remain Match/TrainingLab-owned and never enter Shared, Career or formal V5 artifacts.

## Page Flow

```text
Scenario hub
  -> Rotation
  -> Positioning
  -> Serve setup (top / side / 3D read-only)
  -> Preflight
  -> Native V5 training rally
  -> Result
```

Navigation backward preserves the draft unless the user explicitly reopens rotation. Reopening rotation retains actual player coordinates but unlocks rotation identities; relocking immediately reruns position checks using the new slot identities. Only Positioning to Serve is gated by position faults.

## Rotation Page

- The formal court shows six fixed places per team in three rows and two columns.
- Slot numbering is fixed: facing the net, right-back is slot 1; subsequent slots increase counter-clockwise; middle-back is slot 6.
- Every player card shows name, registered role, and locked/current slot. The court does not add unrelated floating labels.
- Dragging only exchanges two players on the same team; cross-team and arbitrary placement are invalid. The action calls Match setup rotation exchange, then updates cards and frozen identities.
- `确认并锁定位次` validates one six-player permutation per team. Success enters Positioning. `重新编辑轮转` is only available while editing.

Acceptance:

- a same-team exchange changes the Match setup rotation and the shown slots identically;
- no cross-team exchange can alter the setup;
- relocking after rotation changes preserves player coordinates but recomputes position-fault relations.

## Positioning Page

World origin is the court centre, X is the lateral court axis and Z is the net-to-end-line axis. Match evaluates legal relations in team-local coordinates:

- Home local `(lateral, depth)` maps to world `(lateral, -depth)`.
- Away local `(lateral, depth)` maps to world `(-lateral, depth)`.
- Therefore equivalent Home/Away formations are related by a 180-degree point rotation around court centre, not a mirror across the net.

The same side-local transform is used by default placement, editor projection, correction arrows and the serve-contact position-fault evaluator. Player positioning is editable only in the top view; side view and 3D preview cannot mutate player positions.

- Reuse the exact court component used by Serve: formal 18m by 9m layout, center net, three-meter lines, HOME/AWAY, and external rulers.
- In-court dragging is continuous; release calls Match setup position editing and snaps to 0.1m.
- Players remain in their own half. Match clamps invalid input and returns the resulting legal coordinate.
- Two external precision rulers appear only while a player is selected:
  - horizontal: net is 0; both directions increase to 9m;
  - vertical: bottom is 0; values increase upward to 9m.
- Each ruler has one draggable selected-player point. Dragging changes only that axis; the other horizontal coordinate and height remain unchanged.
- Position faults are returned by the Match evaluator used by formal serve contact. Every relation shows both tokens in red, a red dashed relation line, and a blue shortest legal correction arrow for the violating player.
- Inspector fault cards name both players, slots, violated relation, and concrete correction direction. Selecting one focuses both tokens and ruler points without changing data.
- `继续设置发球球` is disabled while any position fault exists, with the explicit reason that all position faults must be corrected first.

Acceptance:

- court and ruler edits write the same final Match coordinate;
- an invalid formation is identical in the editor preview and formal serve-contact evaluator;
- focused fault annotations identify only the associated relation;
- no canvas, inspector, ruler, or action-bar overlap exists at 1920x1080.

## Serve Setup Page

Serve setup is accessible only after legal positioning. It edits the same Match `BallPosition` and `BallVelocity` used by the frozen setup.

### Views

- The main canvas uses one location for the mutually exclusive `俯视` and `侧视` view buttons.
- Top view shows the formal court and serving bands. Dragging the yellow ball edits X/Z. Dragging the red vector endpoint edits VX/VZ. Height is not represented.
- Side view shows ground, net, serving end line, height ruler and serving band. Dragging the yellow ball edits Z/Y. Dragging the red vector endpoint edits VZ/VY.
- Both 2D views synchronize the same six scalar values. Exact fields always reflect the same draft.
- `3D 预览` opens a modal. It is read-only; the user can rotate, zoom, reset, save and load named camera bookmarks. It cannot drag the ball, players, or velocity vector. Closing returns to the prior 2D view unchanged.

### Tools and constraints

- `移动球`: ball editable; velocity vector display-only; X/Y/Z fields enabled.
- `调整速度`: velocity vector editable; ball display-only; VX/VY/VZ fields enabled.
- `查看轨迹`: all editing disabled; predicted trajectory displayed.
- Match setup editing enforces: current server's end line behind the court, inside sidelines, at most 3m outside the end line, ball height at least radius, finite valid velocity.
- Rejected field or drag values restore the last legal Match value and display a local reason.
- Trajectories use the existing Match ball integrator/parameters. Top shows ground projection; side shows Z/Y; 3D shows spatial path.

Acceptance:

- edits through both 2D views and exact fields result in identical six-axis draft values;
- server changes update the allowed band immediately;
- the 3D modal cannot mutate the draft;
- corresponding trajectory projections derive from the same Match path.

## Preflight Page

- Entering or re-entering Preflight automatically calls the Match setup validator. There is no standalone `校验` button or second manual validation action.
- Preflight displays prioritized blockers, warnings and one read-only setup summary.
- A valid preflight creates `MatchSetupSnapshotV1` and displays its hash.
- Each error has `返回修正`:
  - rotation to Rotation;
  - position fault to Positioning and its relation focus;
  - ball/velocity to Serve and the appropriate top/side view;
  - metadata/attributes/tactics to Advanced Settings.
- `运行训练回合` remains disabled until Match validation succeeds. Pressing it consumes the already-frozen snapshot; it does not run a second UI-owned validation implementation.

Acceptance:

- preflight snapshot fields exactly match the input received by native V5 TrainingLab startup;
- every error reaches its correct page and selected object;
- the issue list is internally scrollable and does not cover canvas/action controls.

## Run and Result Pages

- Run calls the native V5 TrainingLab startup boundary with the frozen `MatchSetupSnapshotV1`. Bootstrap/director create the same V5 roster, authority, AI, physics, contact and referee components used by formal V5; TrainingLab does not rebuild those systems.
- Native startup must not overwrite frozen player poses or compute a replacement serve. At the existing V5 serve gate it captures the applied runtime transforms, evaluates side-relative faults, and either resolves one point before ball/AI/contact startup or launches the frozen ball state and continues through the normal V5 rally path.
- Running locks all editing, saving and scenario switching. It permits pause, resume and single-step through existing run controls.
- One rally resolution stops the training runtime. Result displays `TrainingRallyOutcomeV1`: winner, one-rally score delta, reason, position-fault facts, touch count, seed, setup/outcome hashes and bounded timeline.
- TrainingLab never waits for a full formal set and never creates `MatchResultV5`, `MatchReplayV5` or a Career report.
- `同 seed 重跑` uses the same frozen snapshot. `返回编辑` restores the current in-memory local working copy, not an automatically overwritten saved file.
- Full evidence is shown in a capped, expand-on-demand run-evidence region rather than a persistent 178px console during authoring.

Acceptance:

- run input equals preflight snapshot;
- rerun uses same snapshot/hash;
- no editing controls become active during run;
- result evidence remains within a bounded scroll region;
- static and runtime boundary checks prove no TrainingLab override, bookmark, setup or outcome type reaches Shared/Career/formal-V5 artifacts.

## Local Persistence

### Storage

- Local files live below `Application.persistentDataPath` in a TrainingLab-owned directory and are ignored by Git.
- A file contains a format version, local ID, display name, created/modified timestamps, Match setup draft/snapshot representation, UI session metadata, content hash and validation metadata.
- UI session metadata contains active page, active 2D view/tool, selected object and named 3D bookmarks. It cannot alter Match meaning.

### Save behavior

- Save is user-triggered only. It atomically overwrites the current local working-copy file.
- Successful writes update modification time and clear dirty state.
- Failed writes preserve the in-memory draft and dirty state, show a failure reason, and never report a false saved state.
- On restart, the hub scans local files and delegates deserialization/validation of Match setup data to Match-owned code.
- Corrupt or unsupported local files appear as unavailable cards with diagnostics and a delete-local-file action; they are never passed into native V5 TrainingLab startup.
- Existing V1/V4-backed TrainingLab assets are not silently converted. Built-in templates are regenerated as the new V5-backed format; local V1 files remain unavailable with an explicit unsupported-version diagnostic and may be deleted or recreated by the user.

### Unsaved leave confirmation

- Returning to the hub, switching scenarios, or closing the lab with a dirty local working copy opens one modal: `保存`, `放弃`, `取消`.
- Save writes successfully before running the requested leave action.
- Discard restores the last saved local version before leaving. For an unsaved working copy created from a template, discard removes only its in-memory copy.
- Cancel leaves page and draft untouched.
- The modal is unavailable during Run because leaving is unavailable.

Acceptance:

- saving, restarting Unity and reopening preserves Match data and UI session data, with stable content hash;
- discard never modifies the saved file;
- a failed save cannot leave the lab as though it succeeded;
- local data never reads or writes Career/official Match persistence.

## Visual and Automated Acceptance

- Run focused Match setup/persistence/EditMode tests and TrainingLab PlayMode tests after final edits. Record exact final XML results; do not reuse earlier results.
- At 1920x1080 capture:
  1. scenario hub;
  2. rotation;
  3. positioning;
  4. focused position fault;
  5. serve top view;
  6. serve side view and 3D modal;
  7. preflight;
  8. running/result;
  9. unsaved leave modal.
- Save screenshots under `TestResults/TrainingLab/VisualAcceptance/2026-08-17/`.
- Inspect every capture against the two approved references: no legacy permanent scenario sidebar or all-controls form; no overlap/clip; clear stage state and primary action; current inspector scroll contained; court ratio correct.
- Windows x64 IL2CPP Player validation remains tracked by the active handoff and is not asserted complete by this design.

## Rollback

The implementation uses focused commits. Rollback reverts only consolidated TrainingLab setup/runtime/presentation/persistence commits. Existing native formal V5 position-fault behavior, all V4 Career/formal/3v3 compatibility paths and unrelated Career/AI work remain intact. Unsupported V1 TrainingLab files stay rejected after rollback rather than being interpreted under another schema. Main branch merge remains separately authorized.

## Self-Review

- The design defines one data owner, all pages, navigation gates, persistence, failure behavior, visual structure and acceptance evidence.
- Match owns every gameplay decision; TrainingLab owns no competing gameplay model or formal result/replay contract.
- Save location, trigger, overwrite semantics, template-copy behavior and dirty-leave options have one unambiguous definition.
- The work is one coherent user flow, but implementation will be split by page with a testable checkpoint and screenshot acceptance at each page.
