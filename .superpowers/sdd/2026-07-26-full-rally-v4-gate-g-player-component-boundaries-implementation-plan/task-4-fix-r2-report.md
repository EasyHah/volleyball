# Task 4 fix r2 report

Status: complete

Changes: moved scheduled, continuation, planned and unplanned attack sampling,
takeoff observation, support movement, physical-block jump movement, retargeting,
speed limits, and motion accounting into `PlayerLocomotion`. The facade now
projects locomotion metrics and routes its real attack-alignment path through the
component. Attack correction resets for each configured attack, is cumulative and
limited to `0.18m`, and uses the component's court-aware root application.

TDD: added failing regression coverage for a fresh attack correction budget and
court-clamped component alignment. The initial focused run failed 2/4 as expected;
the final narrow regression passed 40/40.

Validation: `Volleyball.EditModeTests.PlayerLocomotionTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests|Volleyball.EditModeTests.Stage2AbilityEnvelopeTests` passed 40/40 in `TestResults/GateG-locomotion-r2-final.xml`. This includes scheduled movement, support/block, attack continuation, cumulative correction, facade integration, and court-clamp coverage. `git diff --check` passed.

Concern: none.
