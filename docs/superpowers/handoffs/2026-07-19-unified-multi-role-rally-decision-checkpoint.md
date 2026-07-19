# Unified Multi-Role Rally Decision Checkpoint

**Updated:** 2026-07-19

## Resume Point

- Worktree: `/Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles`
- Branch: `codex/blocking-roles`
- Feature spec: `docs/superpowers/specs/2026-07-19-unified-multi-role-rally-decision-design.md`
- Implementation plan: `docs/superpowers/plans/2026-07-19-unified-multi-role-rally-decision.md`
- Current implementation head: `c444dcc fix: validate smooth rally decision outputs`
- Current phase: Task 3 is implemented and spec-reviewed. Its final code-quality review was in progress when this checkpoint was written; rerun it before starting Task 4 if no review result is available in the active conversation.

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

## Current Capabilities

- `TeamCourtFrame` is Unity-free and converts between world and team-local coordinates by mirroring only depth.
- `RallyTouchState` distinguishes stale candidates (`Ignore`) from immediate illegal-contact faults (`Fault`) before Presentation integration.
- `NetPlaneInterception` predicts the first physical `world Z = 0` crossing from a cloned ball state.
- `TeamRallyDecisionPlanner` is deterministic and Unity-free. It can select reachable receivers, organizers, and attackers; a nominal setter/attacker is a bounded preference only.
- Organize decisions target a future tactic attack contact point, not the current ball depth.
- Attack approach quality is based on post-reaction, reachable player-to-approach-start-to-takeoff distance and uses a smooth capped curve.
- `RallyTacticalWeights` is a local, bounded seam for a future MenShen integration; no live gateway call exists in the rally runtime.

## Next Work

1. Obtain or rerun Task 3 code-quality review for `f6fb878..c444dcc`.
2. Execute Task 4 from the implementation plan: add `SimulatedBall` pre-physics candidate resolution (`Ignore` / `Accept` / `Fault`), player-rejection events, and same-step player/net/ground/net-plane event ordering.
3. Execute Task 5: add dedicated physical block contact windows and consume attack approach plans in `PrototypePlayerAgent` while retaining visual-only support actions.
4. Execute Task 6: replace the director's fixed `Defender -> Setter -> Attacker` sequence with dynamic possession planning and actual player attribution.
5. Execute Task 7: update Match change records and run complete EditMode and PlayMode XML-backed regressions.

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
  -testResults "$PWD/TestResults/EditMode-unified-rally-final.xml" \
  -logFile "$PWD/TestResults/EditMode-unified-rally-final.log"

"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/PlayMode-unified-rally-final.xml" \
  -logFile "$PWD/TestResults/PlayMode-unified-rally-final.log"

rg -n 'test-run|total=|failed=|result=' \
  "$PWD/TestResults/EditMode-unified-rally-final.xml" \
  "$PWD/TestResults/PlayMode-unified-rally-final.xml"
```

## Checkpoint Policy

The environment did not expose an account-credit or remaining-token balance at this checkpoint. Update and commit this file after every completed implementation task, review loop, or before a context/usage limit is reached. A resumed agent should read this checkpoint, the design spec, and the implementation plan before changing code.
