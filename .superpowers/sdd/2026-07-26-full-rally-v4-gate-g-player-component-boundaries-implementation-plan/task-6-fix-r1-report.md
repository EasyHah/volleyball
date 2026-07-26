# Task 6 fix report — Gate G facade boundaries, round 1

## Root cause

The previous extraction placed a `PlayerAgentRuntimeState` bag in
`PlayerActionTimeline` and retained facade private property proxies into that
bag.  This made the timeline a cross-component owner and imported AI/planner
types into a timeline-only component.

## Change

- Removed `PlayerAgentRuntimeState` and the facade's private business-state
  proxies, including `PrototypePlayerAgent.ScheduledContactExecution`.
- `PlayerTechniqueExecutor` owns execution schedule/error/technique/attack
  command state; `PlayerContactSurfaceProvider` owns immutable contact inputs,
  contact-center diagnostics, and physical-block contact state; `PlayerLocomotion`
  owns motion and observed-takeoff state; `PlayerActionTimeline` owns only
  timelines and support activation state.
- Strengthened the facade reflection boundary test to reject declared private
  properties as well as non-allowed fields.
- The fixed-seed byte-stability fixture now explicitly compares accepted
  contact sequence and V3 transition counts in addition to JSON bytes, replay
  hashes, scores, and V4 artifact identities.

## Verification

- Gate G focused EditMode filter: passed, 84/84.
- `Capture_TwoIndependentFixedSeedFormalRunsAreByteStable`: passed, 1/1
  (6.814s), including accepted sequence and V3 transition assertions.
- Forbidden runtime dependency scan: no director/rules-adapter/recorder/planner
  references in the five presentation components; timeline has no AI, attack
  plan, set-decision, or facade scheduled-execution references.
- `git diff --check`: passed.

## Scope note

The requested three-fixture PlayMode aggregate was not run in this fix round.
This report intentionally makes no claim about an XML aggregate result.
