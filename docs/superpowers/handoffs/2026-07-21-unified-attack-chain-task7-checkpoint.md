# Unified Attack Chain Task 7 Checkpoint

**Updated:** 2026-07-22

## Current State

- Worktree: `/Users/wys/Documents/program/volleyball-match/.worktrees/unified-attack-chain-v2`
- Branch: `codex/unified-attack-chain-v2`
- Tasks 1-7: implementation and automated verification complete.
- Full-arm block follow-up: implementation and automated verification complete.
- Integration state: committed only on the isolated branch; not merged into `main`.

## Completed

- Preserved V1 context/result/JSON/hash compatibility and added an explicit V2 family with
  player-specific `MaxAttackReach` plus deterministic V1→V2 migration defaults.
- Unified planned attack point, setter target and actual striking palm; added team-local
  normal-set orientation, bounded dynamic flight-time solving and actual-contact replanning.
- Added A-E set quality, handling fallback, primary responsibility, counters and replay data.
- Preserved historical replay JSON/checksum shape while carrying optional resolution reason and
  responsible-player data; exposed common read-only V1/V2 match context/result boundaries to Career.
- Preserved the real low contact height for D/E handling, added a net-clearing minimum handling arc,
  staged quick-hit approach before set contact with a reaction reserve, and allowed only the attack
  palm to accept a physically valid back-face sweep when the hand overtakes the set ball.
- Added the absolute 50-point cap: the first team to 50 wins even at 50:49.
- Corrected serve arrival timing and prepared setter/attacker movement used by both scenes.
- Added deterministic 100-in-system samples for 3v3 and 6v6 plus 20-set symmetry calibration.
- Replaced palm-only blocking with six rig-driven swept capsules for both upper arms,
  forearms and palms. No head, torso, hip, leg or foot block collision is emitted.
- Moved the blocker root to 0.18 m from the centre line, squared it toward the net and
  closed/pressed the visible arms so valid trajectories can physically contact the limbs.

## Final Verification

- `TestResults/UnifiedAttackChain-final6-editmode.xml`: 308/308 EditMode tests passed.
- `TestResults/UnifiedAttackChain-final6-playmode.xml`: selected PlayMode suite 7/7 passed.
  - Formal 6v6 full set: 25:7, 161 contacts, 19 blocks, 3 non-setter sets,
    3 defender attacks.
  - Ordinary 3v3: 7:15, 95 contacts, 7 blocks, 3 non-setter sets, 6 defender attacks.
  - Legacy V1 initialization/result and formal replay artifact tests passed.
- Calibration is included in `UnifiedAttackChain-final6-playmode.xml`: 3/3 passed.
  - Formal 6v6: 100 in-system sets, attackable 1.000, A-grade no-contact 0.000.
  - Physical 3v3: 100 in-system sets, attackable 1.000, A-grade no-contact 0.000.
  - Normal in-system side sets: zero in both scenes.
  - Twenty symmetric formal sets: Blue wins remained in the required 9-11 range.
- Unity: `6000.0.43f1`, macOS batch mode.

## Remaining Work

No planned implementation or automated-test item remains. Before release:

1. Review and merge `codex/unified-attack-chain-v2` into `main` through the repository's
   normal integration workflow.
2. Perform the optional manual visual pass in both scenes, focusing on the 0.18 m block
   stance and arm penetration over the net.
3. Perform the project-wide Windows x64 hardware/build validation required by
   `docs/development.md`; this repository does not yet provide a verified Windows runner.

Do not lower calibration thresholds, enlarge arm capsules, or restore palm-only block
contacts to address future failures. Diagnose stance, pose, timing and trajectory first.
