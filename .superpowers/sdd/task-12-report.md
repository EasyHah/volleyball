# Task 12 Report: Legacy Production Cleanup and Full Gates A–E Verification

## Outcome

Completed the Full Rally V4 hard cut. Career, Bootstrap, Match, result, replay,
and the physical 3v3 prototype now use concrete V4 contracts only. The retained
V3 volleyball rules authority is identified independently by
`RulesVersions.FullRallyV3`; it no longer depends on a V3 attribute, context,
result, or replay contract.

## Legacy audit and removal

The initial production search found the remaining V1/V2/V3 contracts, their
serializers, an isolated V2 3v3 prototype entry point, and unreachable replay
and ability-projection paths. Consumers were migrated before deletion.

Deleted production paths, including their Unity `.meta` files:

- Shared V1/V2/V3 ability, player, team, context, and result contracts;
- Shared Replay V2 and all V1/V2/V3 context/result/replay serializers;
- Match Replay V1 and its serializer;
- the prototype legacy match-set adapter;
- the unused V3 ability projection;
- the legacy V1 test fixture.

The replay EditMode fixture was renamed to V4. V3 lineup, eligibility, runtime
adapter, and world-snapshot tests now build V4 contexts while continuing to
test the independent V3 rule types.

## Native V4 3v3 path

The physical 3v3 bootstrap now creates a valid V4 context with six players per
team and supplies three explicit active player IDs per team to `MatchSet`.
Agents bind directly to V4 player snapshots and derived abilities, and the
director produces `MatchResultV4`. There is no prototype compatibility
exception.

Removing `MatchContextV3` also removed a generic canonical-string/SHA helper
that V4 had referenced. That implementation was extracted as
`CanonicalJsonHashV4`, so V4 canonical identity is self-contained rather than
retaining a legacy context file for utility code.

## Verification

Focused contract, boundary, lineup, runtime-adapter, and world-snapshot
EditMode tests:

- `79/79` passed;
- `0` failed, skipped, or inconclusive;
- result: `/tmp/volleyball-v4-task12-focused.xml`.

Complete EditMode suite on Unity `6000.0.43f1`:

- `507/507` passed, above the pre-migration baseline of 491;
- `0` failed, `0` skipped, `0` inconclusive;
- result: `/tmp/volleyball-v4-all-editmode.xml`.

Complete PlayMode suite on Unity `6000.0.43f1`:

- `24/24` passed in `525.099733s`;
- `0` failed, `0` skipped, `0` inconclusive;
- result: `/tmp/volleyball-v4-all-playmode.xml`.

Both complete suites were run from the isolated checkout under test without
`-quit`:

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults /tmp/volleyball-v4-all-editmode.xml \
  -logFile /tmp/volleyball-v4-all-editmode.log

/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/volleyball-v4-all-playmode.xml \
  -logFile /tmp/volleyball-v4-all-playmode.log
```

The broad audit search and the final required search both returned no
production matches:

```bash
rg -n "PlayerAbilitySnapshotV[123]|MatchContextV[123]|MatchResultV[123]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'

rg -n "PlayerAbilitySnapshotV[123]|MatchContextV[12]|MatchResultV[12]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'
```

`git diff --check` is clean. Documentation records V4-only support, independent
V3 rules versioning, frozen base and six-group derived fields,
formula/coefficient versioning, exact verification commands, Gates A–E
evidence, and the remaining Gates F–K.

## External-review corrections

The V4 active-roster `MatchSet` overload previously passed each
`IEnumerable<PlayerId>` into the delegated constructor and then enumerated the
original inputs again for V4 team-membership validation. A one-shot enumerable
regression first failed with the expected `InvalidOperationException` on the
second enumeration. The constructor now materializes each input exactly once
into an owned active-roster snapshot, then validates non-null and non-empty
inputs, equal counts, per-team uniqueness, cross-team uniqueness, and V4 team
membership against those same snapshots. The focused regression subsequently
passed `1/1`.

A second production audit proved that the following non-rule V3 Stage2 types
were referenced only by themselves and three legacy-only Phase0 contract
tests:

- `BallTrajectoryArtifactV3`;
- `BallTrajectoryPredictionProviderV3`;
- `DeterministicWorkBudgetV3`;
- `ExecutionEnvelopeV3`.

Those four source files and their `.meta` files were deleted. The three
legacy-only tests were removed, while the independent
`PlanCoverageDecision` rule test remains. Current V3 production files are
limited to rule authority, eligibility, rule facts, and world snapshots.

The consolidated roadmap now records the implemented state:
`PlayerAbilityProfile` remains only as a runtime wrapper around immutable V4
derived attributes, and physical 3v3 is hard-cut to a six-player V4 roster with
three explicit active player IDs.

Fresh post-review verification:

- focused MatchSet/Phase0 regression: `15/15` passed;
- complete EditMode: `505/505` passed, `0` failed/skipped/inconclusive
  (`507` before review, plus one regression and minus three obsolete tests);
- complete PlayMode: `24/24` passed in `524.8943798s`,
  `0` failed/skipped/inconclusive;
- result files:
  `/tmp/volleyball-v4-task12-review-all-editmode.xml` and
  `/tmp/volleyball-v4-task12-review-all-playmode.xml`.

The required legacy production searches and the added non-rule Stage2 V3
search return no matches. `git diff --check` remains clean.
