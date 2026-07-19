# Unified Multi-Role Rally Decision Checkpoint

**Updated:** 2026-07-19

## Resume Point

- Worktree: `/Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles`
- Branch: `codex/blocking-roles`
- Feature spec: `docs/superpowers/specs/2026-07-19-unified-multi-role-rally-decision-design.md`
- Implementation plan: `docs/superpowers/plans/2026-07-19-unified-multi-role-rally-decision.md`
- Current implementation head before latest delivery documentation: `f3d2634 feat: add physical block impact feedback`
- Current phase: Tasks 1-7 are implemented and fully verified. No implementation step remains; the branch is ready for review and merge.

## User-Confirmed Rules

- Work is Match-only. Do not change Career, Shared contracts, Bootstrap, assembly names, scene paths, or `MatchResultV1`.
- Unity coordinates are fixed: `X` is world left/right, `Y` is height, and `Z` is court depth. Team-local tactical conversion mirrors only `Z`.
- World left/right is not mirrored between teams.
- Roles are preferences, not locks: all three players can receive, organize, attack, block, or cover when reachable.
- Blocks are real physical contacts but do not consume a team touch.
- After a block, whichever team receives the rebound begins a new zero-count possession.
- A counted Receive/Set/Attack must belong to the current possession team. A block and serve remain valid cross-possession physical contacts when their window allows them.
- The LLM may only provide bounded tactical weights or intent. It cannot bypass local legality, reachability, transform, or ball-velocity controls.

## Completed Commits

| Commit | Purpose | Verification |
| --- | --- | --- |
| `3552e3e` | Approved B-plan design specification | Spec committed |
| `5c5fdfe` | Detailed TDD implementation plan | Plan committed |
| `892c77d` | `TeamCourtFrame` and TeamId planner compatibility | EditMode 19/19 passed |
| `95b8fd8` | `RallyTouchState` and contact-window rules | EditMode 8/8 passed |
| `1af81c5` | State-boundary test coverage | EditMode 12/12 passed |
| `7c4bfca` | Reject counted touch from wrong possession | EditMode 15/15 passed |
| `f6fb878` | Net interception, tactical weights, multi-role planner | Focused tests passed |
| `d3f21fc` | Future attack set target and reachable approach quality | Focused tests passed |
| `c444dcc` | Smooth approach curve and decision-output validation | Focused tests passed |
| `e0a558e` | Pre-physics contact eligibility and ordered same-step events | EditMode 14/14 passed |
| `1d33c7b` | Physical block windows and bounded attack approaches | EditMode 19/19 passed |
| `33e6dc2` | Dynamic possession orchestration and real role attribution | EditMode 17/17 and PlayMode 1/1 passed |
| `f3d2634` | Team-colored physical block impact feedback | EditMode 2/2 and PlayMode 1/1 passed |

## Current Capabilities

- `TeamCourtFrame` is Unity-free and converts between world and team-local coordinates by mirroring only depth.
- `RallyTouchState` distinguishes stale candidates (`Ignore`) from immediate illegal-contact faults (`Fault`) before Presentation integration.
- `NetPlaneInterception` predicts the first physical `world Z = 0` crossing from a cloned ball state.
- `TeamRallyDecisionPlanner` is deterministic and Unity-free. It can select reachable receivers, organizers, and attackers; a nominal setter/attacker is a bounded preference only.
- Organize decisions target a future tactic attack contact point, not the current ball depth.
- Attack approach quality is based on post-reaction, reachable player-to-approach-start-to-takeoff distance and uses a smooth capped curve.
- `RallyTacticalWeights` is a local, bounded seam for a future MenShen integration; no live gateway call exists in the rally runtime.
- `SimulatedBall` resolves Ignore / Accept / Fault before applying a player response and preserves player, net, ground and net-plane event order inside one fixed step.
- `PrototypePlayerAgent` has dedicated physical block windows, bounded retargeting, continuous attack approaches and planned-contact previews that match the actual jump quality.
- `ThreeVsThreeRallyDirector` is a possession orchestrator. It records the actual actor and movement, supports non-setter sets and defender attacks, and starts a zero-count possession after a real block.
- An accepted physical Block plays a reusable code-generated impact core, expanding ring, point light and ball-trail pulse. The effect count must equal the physical block count.

## Completion State

- No feature implementation task remains.
- `CHG-20260719-002` records the Match-only boundary, behavior, verification paths, risk and rollback order.
- `CHG-20260719-003` records the accepted-Block-only visual feedback and focused/full test evidence.
- Latest full EditMode passed `216/216`; full PlayMode passed `3/3` on Unity `6000.0.43f1`.
- Physical3v3Rally evidence includes a non-setter Set, Defender Attack, real Block, post-block zero-touch possession and final `RESULT`.
- Review and merge `codex/blocking-roles`; handle the unrelated ProjectSettings change only as a separate user decision.

Each task must follow test-first red/green development, have a spec-compliance review, then a code-quality review before the next task starts.

## Important Safety State

- `ProjectSettings/ProjectSettings.asset` is intentionally modified by Unity but must remain unstaged and uncommitted. The user explicitly approved excluding it from Match feature commits.
- The observed changes set target pixel density and Apple-platform minimum OS/build defaults. They are unrelated to the Match feature and can affect platform build compatibility.
- Do not use destructive Git commands. Do not revert or stage that `ProjectSettings` file without explicit user direction.
- Unity test commands must not include `-quit`; this project can exit after compilation without actually running Test Runner when `-quit` is supplied.
- Do not rely on the Unity process exit code alone. Inspect the generated XML and require `failed="0"`.

## Verification Commands

Use the worktree as the current directory:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"

"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/EditMode-block-impact-final.xml" \
  -logFile "$PWD/TestResults/EditMode-block-impact-final.log"

"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/PlayMode-block-impact-final.xml" \
  -logFile "$PWD/TestResults/PlayMode-block-impact-final.log"

rg -n 'test-run|total=|failed=|result=' \
  "$PWD/TestResults/EditMode-block-impact-final.xml" \
  "$PWD/TestResults/PlayMode-block-impact-final.xml"
```

Verified outputs on 2026-07-19:

- `EditMode-block-impact-final.xml`: `total=216 passed=216 failed=0`.
- `PlayMode-block-impact-final.xml`: `total=3 passed=3 failed=0`.
- `PlayMode-block-impact-final.log`: `block-contact ... effect=1`, followed by
  `RESULT score=15:2 contacts=30 blocks=1 nonSetterSets=1 defenderAttacks=1`.

## Checkpoint Policy

The environment did not expose an account-credit or remaining-token balance at this checkpoint. Update and commit this file after every completed implementation task, review loop, or before a context/usage limit is reached. A resumed agent should read this checkpoint, the design spec, and the implementation plan before changing code.
