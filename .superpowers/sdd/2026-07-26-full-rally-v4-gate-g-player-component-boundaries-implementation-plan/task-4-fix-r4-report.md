# Task 4 fix r4 report

Status: complete

Root cause: `ConfigureSupportAction` passed the stored physical-block root
height for every block action. A later ordinary support block therefore reused
the previous physical block's explicit height.

Change: only `ScheduleBlockContact` passes its explicit physical-block height
to `ConfigureSupportAction`; ordinary support blocks retain the locomotion
default height (`0f` input).

TDD: added `OrdinarySupportBlock_DoesNotReusePhysicalBlockRequestedHeight`.
The focused RED run failed as intended: the ordinary support block remained at
`0.16m`; its focused GREEN run passed after the minimal change.

Validation: `Volleyball.EditModeTests.PlayerLocomotionTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests|Volleyball.EditModeTests.Stage2AbilityEnvelopeTests` passed 44/44 in `TestResults/GateG-locomotion-r4-final.xml`. `git diff --check` passed.

Concern: none.
