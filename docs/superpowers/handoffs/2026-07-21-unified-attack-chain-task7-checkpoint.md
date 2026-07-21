# Unified Attack Chain Task 7 Checkpoint

**Updated:** 2026-07-21

## Resume Point

- Worktree: `/Users/wys/Documents/program/volleyball-match/.worktrees/unified-attack-chain-v2`
- Branch: `codex/unified-attack-chain-v2`
- Design: `docs/superpowers/specs/2026-07-21-unified-attack-chain-design.md`
- Plan: `docs/superpowers/plans/2026-07-21-unified-attack-chain.md`
- Completed implementation commits: Tasks 1-6 through `df4977c feat: record set quality and attack attribution`
- Current phase: Task 7 is partially implemented and not ready to merge.

## Completed in the Current Checkpoint

- Added a hard set cap of 50 points. Reaching 50 ends the set immediately even when the lead is only one point; 50:49 is valid and the team reaching 50 wins.
- Kept calibration configurations able to use targets above 50 by setting their maximum score to at least the calibration target.
- Added fixed-seed 3v3/6v6 attack-chain calibration tests and a 20-set first-server symmetry test skeleton.
- Added public in-system, attackable-set, A-grade no-contact, and normal-side-set counters.
- Made prepared normal sets use their full normal-action control scale and applied a smooth receive/set control curve.
- Changed the striking palm collision normal to face forward/up toward the incoming ball. This removed the deterministic 3v3 A-grade no-contact caused by the former forward/down one-sided plane.
- Added provisional attacker preparation while the pass travels to the setter. The actual set contact still replaces this preparation with the replanned attack.
- Added contact-root resolution for receivers so the visible forearm platform, rather than the player root, is aligned to the planned contact centre.

## Verification Completed

- `TestResults/Task7Checkpoint-EditMode.xml`: 53/53 passed. This includes the 50-point cap, receiver contact-root resolution, attack preparation, palm normal, set technique, and control policy tests.
- `TestResults/Task7Checkpoint-Scenes.xml`: 2/3 passed.
  - Formal 6v6 full-set regression passed.
  - Legacy V1 3v3 initialization/result regression passed.
  - The normal 3v3 scene regression failed only because `PhysicalBlockContacts` was 0; the set completed 6:15 with both teams producing attack contacts.
- Earlier 3v3 calibration using 100 total sets passed the attackability, A-grade no-contact, and normal-side-set thresholds after the palm-normal fix. The harness was then corrected to require 100 **in-system** setter contacts, so that earlier pass is not final Task 7 evidence.

## Unfinished Work for Tomorrow

1. Diagnose the formal 6v6 receive timing after rotation.
   - Before receiver-root correction, a 45-second run plateaued at 26 total sets / 16 in-system sets and score 150:7 because Orange repeatedly missed serves after rotating.
   - Root correction now places the rotated Orange defender at the proper root (`z=2.35`) and its forearm contact centre at the planned horizontal point.
   - A 12-second reproduction still ended at 21 total / 13 in-system contacts and score 18:10. At receive timeout, the ball trajectory was approximately 0.15-0.20 seconds later than the scheduled `expected=0.900` contact. Investigate the serve `ArrivalLaunchSolver` target/time versus the director's receive deadline and action timeline before changing tolerances.
   - Reproduction artifact: `TestResults/ContactRootFix-6v6-repro.xml`; detailed temporary trace: `TestResults/ContactRootFix-6v6-repro.log`.
2. Restore deterministic physical blocks in the ordinary 3v3 full-set regression without weakening its existing assertion.
   - Current artifact: `TestResults/Task7Checkpoint-Scenes.xml`.
   - The run scheduled block windows but accepted zero block contacts after the new attack-chain geometry and preparation changes.
3. Run both corrected 100-in-system fixed-seed tests to completion:
   - 3v3 attackable rate >= 0.95, A-grade no-contact rate < 0.02, normal side sets = 0.
   - 6v6 with the same thresholds.
4. Run the 20 symmetric formal sets and require Blue wins in the inclusive range 9-11.
5. Add mirrored reach assertions to the ordinary scene tests if needed; defaults are already symmetric and within the planned ranges.
6. Run full EditMode and the selected PlayMode suite, then complete Task 7 checkboxes.
7. Create `docs/changes/2026-07-21-001-unified-attack-chain.md`, update `docs/changes/README.md` and `docs/development.md`, and perform the final code-quality review.

## Important Notes

- The calibration loop now counts `InSystemSetterSets`, not `TotalSets`; do not revert this correction merely to shorten the test.
- The calibration tests have a 600-second NUnit timeout and a 420-second internal real-time timeout. Normal logs are disabled during the long samples.
- Temporary per-collision diagnostic allocation was removed before checkpointing. The retained A-grade timeout diagnostic is lightweight and only records the relevant failure.
- Do not enlarge the attack palm to hide no-contact errors. The failed palm-size experiment was reverted; the one-sided normal and movement preparation were the real 3v3 causes.
- Do not lower the planned thresholds or remove the existing 3v3 block assertion to make the suite green.
