# Task 4 fix r3 report

Status: complete

Changes: `PlayerLocomotion.ConfigureScheduledMovement` now clears prior planned-attack state before a new scheduled command. Physical-block requested height is calculated before locomotion setup, passed to it, and propagated on block retarget. Support/block configuration and retargeting now refresh the public scheduled-movement distance projection.

TDD: added regressions for planned-to-unplanned attack isolation, initial and retargeted requested block heights, and stale scheduled movement distance. The RED run failed all three new behaviors; the final narrow regression passed 43/43.

Validation: `Volleyball.EditModeTests.PlayerLocomotionTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests|Volleyball.EditModeTests.Stage2AbilityEnvelopeTests` passed 43/43 in `TestResults/GateG-locomotion-r3-green.xml`. `git diff --check` passed.

Concern: none.
