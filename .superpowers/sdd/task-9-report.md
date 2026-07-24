# Task 9 Report: Observed P6 Geometry Authority

## Scope implemented

- Added an observed attack-takeoff snapshot containing the physical trajectory's
  resolved takeoff point and simulation time.
- Built `AttackGeometryFactV3` only at an actual swept-ball collision from:
  - stable actor and team;
  - the observed takeoff snapshot;
  - the collision-derived shared player/ball contact point, with the ball impact
    center validated at the same boundary;
  - the formal three-metre attack line and `CourtBuilder.NetHeight`.
- Routed that fact through the V3 adapter's attack-specific evaluate and commit
  overloads.
- Added a V3 eligibility overload that accepts the same fact, validates its
  actor/team against the current on-court eligibility record, and queries the
  existing V3 attack rule with its fields unchanged.
- Preserved the exact existing `RuleTransitionV3` acceptance result and
  `RuleRejectionReasonV3.ActionIneligible` rejection code.

The observed-fact construction path does not read a planned contact centre,
planned contact height, derived contact-height capability, envelope,
trajectory artifact, or replay state.

## Observed boundary

`PrototypePlayerAgent` snapshots the authoritative simulated root trajectory at
the resolved takeoff time. It does not copy the requested approach takeoff into
the observation. This matters when the requested point cannot be reached:
tests cover both a planned back-zone takeoff that physically remains in the
front zone and a planned front-zone takeoff that physically remains behind the
line.

At collision, `PhysicalMatchRallyDirector` combines that takeoff observation
with `SweptBallHit`. `hit.ContactPoint`, the shared physical collision point,
supplies the observed attack contact point. The boundary also validates
`hit.ImpactCenter` and rejects a takeoff timestamp later than the contact
timestamp. Current lineup eligibility remains the authority for front/back-row
status.

Both candidate evaluation and selected-candidate commit reconstruct the same
fact from the same immutable collision/takeoff observations. Non-attack
contacts remain on the existing four-argument adapter path.

## Test coverage

- Back-row above-net attack with takeoff behind the line: accepted.
- Back-row above-net attack with takeoff in the front zone: rejected with
  `ActionIneligible`.
- Contact exactly at net height: accepted.
- Contact strictly above net height: rejected for an otherwise identical
  back-row front-zone attack.
- Geometry actor or side mismatch: rejected before the V3 query.
- Planned legal / observed illegal takeoff mismatch: observed fact is front
  zone.
- Planned illegal / observed legal takeoff mismatch: observed fact is back
  zone and the exact V3 transition is accepted.
- Planned legal / observed illegal and planned illegal / observed legal cases
  both pass only the observed fact into the adapter.
- Collision contact point and ball centre straddle the net threshold: the
  collision contact point controls the V3 fact.
- Five-argument evaluate and commit return the same decision/reason while only
  commit advances engine state.

## TDD evidence

### Initial RED

The pre-existing Task 1 gate was run before implementation:

- result: `10/11` passed;
- sole failure:
  `CommitContact_ObservedGeometryDecidesOtherwiseIdenticalAttackEligibility`;
- cause: the required
  `CommitContact(..., AttackGeometryFactV3)` overload did not exist.

After adding the full Task 9 tests, the targeted run failed compilation only on
the intentionally missing five-argument adapter methods, fact-based eligibility
query, and observed-takeoff API.

### Initial targeted GREEN

Adapter, eligibility, and contact-source suites:

- result: `69/69` passed;
- results: `/tmp/task9-observed-green2.xml`;
- log: `/tmp/task9-observed-green2.log`.

### Review RED/GREEN

Read-only review identified that the first conversion used
`SweptBallHit.ImpactCenter` instead of the collision `ContactPoint`, and that
the mismatch tests stopped before asserting the V3 transition.

New regression tests produced `17/18` with only
`CreateObservedAttackGeometryFact_UsesCollisionPointAndValidatesTakeoffTime`
failing because the collision conversion seam did not exist.

After the correction:

- adapter review suite: `18/18` passed;
- results: `/tmp/task9-review-green.xml`;
- log: `/tmp/task9-review-green.log`.

### Required Task 1 focused gate

The exact Task 1 filter, using this isolated worktree as `-projectPath`:

- result: `123/123` passed;
- results: `/tmp/task9-focused-final.xml`;
- log: `/tmp/task9-focused-final.log`;
- no compiler errors or warnings.

### Full EditMode

- result: `558/558` passed;
- results: `/tmp/task9-full-editmode-final.xml`;
- log: `/tmp/task9-full-editmode-final.log`;
- no compiler errors or warnings.

### Formal runtime integration

The formal 6v6 set test exercised collision-to-observed-geometry authority:

- result: `1/1` passed;
- results: `/tmp/task9-formal-playmode.xml`;
- log: `/tmp/task9-formal-playmode.log`.

## Final checks

- `git diff --check` passed.
- No envelope, trajectory-provider, or replay behavior was added.
- Commit message: `fix: authorize p6 attacks from observed geometry`.

## External strict-time review correction

External review found that the first temporal guard rejected a takeoff after
contact but still allowed takeoff and contact at the exact same simulation
time. The observed boundary now requires:

```text
takeoff.SimulationTime < contactSimulationTime
```

The regression covers all three cases explicitly: earlier takeoff succeeds,
equal-time takeoff rejects, and later takeoff rejects. TDD and final evidence:

- strict-time RED: adapter `17/18`, with only the equal-time assertion failing;
- strict-time targeted GREEN: `18/18`;
- required focused GREEN: `123/123`;
- full EditMode GREEN: `558/558`;
- formal runtime integration GREEN: `1/1`;
- results/logs:
  - `/tmp/task9-strict-time-red.xml` and `.log`;
  - `/tmp/task9-strict-time-green.xml` and `.log`;
  - `/tmp/task9-strict-time-focused.xml` and `.log`;
  - `/tmp/task9-strict-time-full-editmode.xml` and `.log`;
  - `/tmp/task9-strict-time-formal-playmode.xml` and `.log`.
