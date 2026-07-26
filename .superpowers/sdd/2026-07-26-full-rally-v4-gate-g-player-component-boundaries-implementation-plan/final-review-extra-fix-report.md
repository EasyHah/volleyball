# Gate G extra speed fix report

## Fix

- Attack alignment is now incorporated into the one desired live-root target before the single `MaximumSpeed * dt` `MoveTowards` operation.
- The first live sample consumes the elapsed interval since scheduled movement began instead of bypassing the live-root speed budget.
- Attack contact alignment shares the remaining budget from the first fixed step as well as later fixed steps.

## TDD evidence

- `PersistentAttackAlignment_NextFixedSampleKeepsTheActualRootWithinOneSpeedBudget` and `FirstFixedLiveSample_UsesElapsedSimulationTimeWithoutTeleporting` were RED together in `TestResults/GateG-extra-speed-red.xml` (0/2 passed), then GREEN in the focused locomotion run.

## Completed validation

- Focused locomotion EditMode: `TestResults/GateG-extra-speed-green.xml` — 12/12 passed.
- Focused component/facade EditMode: `TestResults/GateG-extra-speed-components.xml` — 62/62 passed.
- Full EditMode: `TestResults/GateG-extra-speed-editmode.xml` — 580/580 passed.
- Focused 3v3 PlayMode was run twice. Both runs failed in the existing scene-level assertions with different random NUnit seeds: `GateG-extra-speed-3v3.xml` failed the contact-index assertion; `GateG-extra-speed-3v3-r2.xml` failed the minimum-score assertion. These runs do not establish a fixed deterministic replay failure because their seeds differ and the failures differ.

## Remaining validation concerns

- Full PlayMode: `TestResults/GateG-extra-speed-playmode.xml` — 23/30 passed, 7 failed. The failures include the two flaky 3v3 scene-level outcomes and Formal V4 replay recorder failures caused by `InvalidOperationException: Formal V4 contact replay requires the actor's actual sample classification.`
- Fixed-seed replay invariance: `TestResults/GateG-extra-speed-fixed-seed.xml` — 0/1 passed, blocked by that same pre-assertion replay-recorder exception. It therefore does not provide byte-invariance evidence.
- Static facade scan found no forbidden presentation implementation references in `PrototypePlayerAgent.cs`; `git diff --check` passed.
