# TrainingLab Authoring UX Redesign

- Date: 2026-08-06
- Status: approved for planning
- Scope: `TrainingLab` presentation and authoring interaction only
- Audience: internal developers and designers
- Supersedes: the TrainingLab authoring interaction portions of
  `2026-08-01-training-lineup-position-fault-v5-ui-design.md`

## Goal

Make the formal TrainingLab a dependable, fast authoring tool for creating a
valid pre-serve scenario. The primary task is to arrange the two teams, resolve
any position faults, configure the serve, validate, and run. Authoring must not
depend on 3D physics colliders or screen-to-camera rays.

The existing Match-domain position-fault evaluator, frozen scenario data,
validation rules, V5 result/replay contracts, and formal-run integration remain
unchanged. This is a presentation and interaction redesign, not a rules or data
migration.

## User Workflow

The workbench exposes a strict, visible sequence:

1. **Rotation**: choose and lock each team's legal 1--6 rotation identities.
2. **Positioning**: arrange all twelve players on the primary 2D court board.
3. **Serve setup**: choose and configure the serve ball position and velocity.
4. **Validation**: freeze and inspect the complete pre-serve input.
5. **Run**: execute the existing formal rally with the frozen input.

The next step is unavailable until the current step's blocking requirements are
satisfied. In particular, any position fault blocks entry into **Serve setup**;
the UI says which relation must be corrected and offers a way to locate the
involved player. Validation remains the authority for all other invalid draft
states. Running continues to lock authoring exactly as it does today.

## Primary Authoring Surface

The main workspace is a top-down 2D tactical board, not a 3D camera image.

- The board maps UI-local coordinates directly to formal-court coordinates.
  Pointer selection and dragging use this mapping and never use
  `Physics.Raycast`, preview colliders, Game View scaling, or camera pixels.
- Each player token displays team color, registered role, and locked rotation
  slot. The ball is visually distinct and obeys its existing serve-zone limits.
- A selected token has a strong focus treatment. The contextual inspector shows
  only the selected object and the controls relevant to the current workflow
  step; complete diagnostics and low-frequency configuration are collapsed by
  default.
- The existing scenario library remains at the left. The primary board occupies
  the visual center. The step guide and current actionable instruction remain
  visible beside the board rather than buried in a long inspector.

The board is the only surface that edits player positions. Numeric fields remain
available for precise entry but edit the same draft values and do not introduce
a second authoring state.

## Position-Fault Feedback

While positioning, the board renders each live position-fault relation from the
existing evaluator as one coherent correction cue:

- every involved player token is red;
- a relation line connects the two tokens;
- an arrow indicates the permitted correction direction for the violating
  relation;
- the inspector gives the same advice in natural language, naming the team,
  slots, roles, violated front/back or left/right relation, and a valid movement
  direction.

For multiple faults, all involved tokens remain highlighted. The inspector lists
the deterministic evaluator order and selecting an item focuses its tokens and
relation on the board. The board does not award points or simulate a fault; it
is a preflight correction aid only.

## Serve Setup and Precision Editing

After the positioning gate is clear, **Serve setup** provides the existing
mutually exclusive tools: move ball, adjust velocity, and view trajectory.
The primary board stays intentionally simple. It exposes the ball and an
unambiguous entry point named **Precise adjustment (XY / ZY / XZ)**.

Precise adjustment replaces the board area with three synchronized orthographic
panes and a persistent **Return to tactical board** action. It preserves the
selected object and draft state.

- With a player selected, all panes edit that player's position. XY edits X/Y,
  ZY edits Z/Y, and XZ edits X/Z; each drag preserves the third position axis.
- With the ball selected, a clear mode switch selects either **Position** or
  **Velocity**. The same XY, ZY, and XZ panes edit the corresponding two axes
  of the selected vector, preserving its third axis.
- Values update all panes and the numeric inspector immediately. Constraints
  still run through existing draft mutation and validation paths.
- This surface is for precision work; it is not a fourth workflow step and does
  not bypass the positioning gate or serve-zone validation.

## Free 3D Observation

The workbench also exposes a separately named **Free 3D observation** mode.
It permits orbiting, panning, zooming, trajectory inspection, and camera-bookmark
creation/loading. It never edits the draft: no object dragging, transform gizmos,
or authoring shortcuts are active in this mode. Returning restores the tactical
board, selected object, current workflow step, and unsaved draft.

This preserves a useful spatial check without making 3D camera behavior a
dependency of routine authoring.

## Components and Boundaries

The redesign adds presentation-level components under `TrainingLab`:

- a tactical-board coordinate mapper and renderer;
- token, selection, and position-fault overlay presenters;
- a workflow gate presenter derived from existing controller validation and
  `PositionFaultPreview` data;
- a precise-adjustment presenter for synchronized position/velocity panes;
- a read-only free-3D observation presenter.

`TrainingScenarioLabController` remains the sole owner of draft mutations and
workflow state. The new presenters call its existing position, ball, velocity,
selection, rotation, validation, and run commands. If small controller queries
are needed to express a gate or diagnostic, they expose existing state only; no
new Match, Shared, Career, persistence, or formal-run contract is introduced.

The legacy 3D preview may remain as the backing renderer for observation, but it
must be removed from the authoring input path. Old preview-collider drag code is
deleted only after the tactical-board path is covered by tests.

## Error Handling and Accessibility

- A blocked step stays visible but disabled, with an adjacent reason and a
  focus action to the first unresolved item.
- Pointer drag uses pointer capture on the tactical board and has a numeric-field
  fallback for every editable coordinate.
- Team colors are supplemented by role/slot text and shapes or outlines so an
  error cue is not color-only.
- No visual claim of a successful correction is made until the existing
  evaluator reports no remaining relation for it.
- View changes, selected object, and tool-mode changes do not silently alter
  draft values.

## Acceptance Criteria

1. A developer can lock rotations, arrange any player by dragging its 2D token,
   clear a shown position fault, then enter Serve setup without using a 3D
   collider or numerical fields.
2. A generated position fault visibly marks both players, the relationship, and
   a correction direction; its textual explanation matches the evaluator's
   relation.
3. Position faults block Serve setup, validation/run remain blocked by the
   existing validator, and legal equality remains legal.
4. Precise adjustment edits a selected player's position and a selected ball's
   position or velocity through synchronized XY/ZY/XZ panes without changing
   untouched axes.
5. Free 3D observation supports camera bookmarks and trajectory inspection but
   cannot mutate player, ball, or velocity draft state.
6. Focused EditMode tests cover coordinate mapping, workflow gates, relation
   overlays, and axis preservation. PlayMode tests dispatch real UI pointer
   events for token dragging and verify the resulting draft changes, visible
   cues, and blocked/unblocked transitions.
7. macOS Editor and Windows x64 IL2CPP Development Player manual checks confirm
   full-resolution labels, drag reliability, three-view precision editing, and
   read-only 3D observation. These checks are recorded separately; they are not
   implied by automated tests.

## Non-Goals

- Changing volleyball rules, position-fault ordering, frozen scenario format,
  V5 evidence, Career behavior, or replay contracts.
- Player-facing training UX, gamepad authoring, import/export of user scenarios,
  or live mid-rally editing.
- Replacing formal Match rendering or physics with a second simulation.
