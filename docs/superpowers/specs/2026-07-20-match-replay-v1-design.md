# Match Replay V1 Design

**Date:** 2026-07-20
**Status:** Proposed

## Goal

Validate a reusable match replay pipeline using one complete physical 6v6 rally,
from serve through score resolution. The captured replay must be viewable in a
standalone HTML page now and consumable by a future Unity replay player without
re-running match physics.

## Scope

- Capture exactly one completed rally from `FormalIndoor6v6` in a deterministic
  PlayMode test.
- Store a versioned `MatchReplayV1` JSON file under the ignored
  `TestResults/decision-replay/` directory.
- Capture state at 10 Hz and insert an additional snapshot at every recorded
  event so contact and scoring boundaries are never omitted.
- Generate an HTML replay viewer next to the JSON file.
- Capture the ball and all twelve players, including their court transform and
  display identity.
- Capture score, serving team, rotation, rally phase and the full local planner
  candidate ranking at decision events.

This version does not provide full-set capture, video recording, a Unity replay
scene, persistence outside TestResults, network transport, or replay seeking in
the live game.

## Replay Contract

`MatchReplayV1` is JSON with a fixed `formatVersion` of `1`.

The root document contains:

- Replay metadata: schema version, source scene, capture time, sample rate and
  completion/checksum data.
- Match setup: court dimensions, target score, participating players and their
  stable IDs, prototype IDs, role, roster slot and ability profile.
- Initial match state: score, server and rotation positions before the serve.
- Ordered snapshots: simulation time, score, server, rotations, ball state and
  all player transforms/action state.
- Ordered events: serve, decision, accepted player contact, block, net crossing,
  ground contact, rally resolution and rotation.

Every event references its snapshot index and uses a monotonically increasing
simulation time. The file includes a deterministic content checksum over its
canonical payload. Readers reject unknown major versions, missing players,
non-monotonic time, invalid references and checksum mismatches.

## State Sampling

The recorder starts before the first serve and stops only once the rally score
has resolved. It samples at `0.1` seconds of simulation time, independent of
frame rate. A snapshot is also written immediately before each event. Duplicate
times are allowed only where they represent distinct ordered events in one
physics step; snapshots retain their event sequence number for ordering.

Each player sample includes world position, yaw, scheduled action and movement
target. The ball sample includes world position and velocity. Match state
includes home/away score, serving team, rotation offset and possession/touch
state needed to explain the next decision.

## Decision Events

Every Receive, Organize and Attack plan emits a decision event containing:

- stage, team, action, predicted ball target, available time and active weights;
- selected player and resulting action;
- every candidate's player ID, feasibility, exclusion reason and score
  components: reachability, nominal role, approach, angle, technique and total.

An excluded candidate remains in the event so the viewer can distinguish an
illegal consecutive touch from an unreachable player with a high theoretical
score.

## HTML Viewer

The generated self-contained HTML reads its sibling replay JSON and presents a
top-down SVG court. It provides play/pause, 0.5x/1x/2x speed, a draggable
timeline, event markers and step-to-event controls.

At every decision event it pauses automatically and expands a score panel. The
court shows all twelve labels as `team / P1-P6 / role`, current ball location,
player facing direction and selected player highlight. The header displays
score, server, rotations, stage and simulation time. The score panel shows all
candidates and marks selected, unreachable and legality-excluded candidates.

The viewer interpolates between regular 10 Hz snapshots for animation but never
interpolates across an event boundary. It renders event snapshots exactly.

## Integration Boundaries

The recorder is a diagnostic observer of `PhysicalMatchRallyDirector`. It cannot
change the planner result, player movement, ball physics, score or time scale.
The director emits read-only replay hooks after planning and after existing
physics/match events have been accepted. Domain replay DTOs remain Unity-free;
the Presentation recorder adapts Unity transforms into them.

The current `DecisionPlanned` diagnostic event remains compatible and is either
used directly or superseded by a richer read-only event without changing its
ordering.

## Verification

A dedicated PlayMode test loads `FormalIndoor6v6`, captures the first completed
rally and validates the generated `MatchReplayV1` before writing the viewer.
It asserts twelve distinct players, monotonic snapshots/events, a serve and
score-resolution event, at least one decision event with all six candidates,
and a valid checksum.

The test verifies that the HTML and JSON are written to
`TestResults/decision-replay/`. A local browser/manual check verifies labels,
timeline navigation, event pause behavior and score-panel values against the
JSON.

## Risks and Follow-up

Ten-hertz sampling favors compact files over exact continuous animation; event
snapshots retain all critical game boundaries. A future full-set capture must
segment or stream data to avoid loading a large document at once. A future Unity
replay player consumes `MatchReplayV1` snapshots/events and does not invoke the
live planner or physics simulation.
