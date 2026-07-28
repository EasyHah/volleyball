# Development Rules

## Git workflow

`main` is the only long-lived branch. Create short-lived
`feature/<module>-<description>`, `fix/<module>-<description>`,
`chore/<description>` or `docs/<description>` branches from the latest `main`.
Merge through pull requests only, then delete the branch.

Every code, resource, scene, configuration or contract change must add or update
a record under `docs/changes/` and link it from `docs/changes/README.md`. Start
from `docs/changes/TEMPLATE.md`. If Match and Career interact through Shared,
Bootstrap, a scene path, a save field or a public interface, mark the record as
`跨模块（重点）` and state what the other developer must do before merge.

The future remote `main` branch must require CI, up-to-date branches and at least
one review; direct push, force push and branch deletion must be disabled.

## Testing

`docs/rules.md` is the canonical Match-rule source. Before changing match
behavior, update its applicable rule ID and follow its modification checklist;
specifications and change records must link to it rather than duplicate rules.

Write pure rule, scoring, rotation and statistics tests as EditMode tests. Use
PlayMode tests for one full 3v3 rally with Unity scene integration. Use a fixed
random seed for deterministic simulation tests. Record the Unity editor and package
lock versions used to reproduce each test run.

## Full Rally V4 Gates A–E authority

Save/Career, Match, result and replay production paths support only concrete V4
contracts: `PlayerSnapshotV4`, `TeamSnapshotV4`, `MatchContextV4`,
`MatchResultV4` and `MatchReplayV4`. There are no V1/V2/V3 readers, upgrades,
fallback constructors or compatibility adapters. The 3v3 prototype also creates
a V4 six-player roster and declares its three active players explicitly; it is
not an exception to the contract boundary.

Attribute-contract version and volleyball-rules version are independent.
`ContractVersions.MatchV4` and `ContractVersions.ReplayV4` identify persisted
contracts; `RulesVersions.FullRallyV3` identifies the retained authoritative
touch, lineup, attack/block eligibility and boundary rules. A V4 contract change
does not silently rename the rules engine, and a rules change does not silently
change serialized attributes.

The frozen V4 base input is:

- physical: `HeightMeters`, `StandingReachMeters`, `Jump`, `Mobility`,
  `Reaction`, `Coordination`;
- technical: `AttackTechnique`, `AttackPower`, `BlockTechnique`,
  `DefenseTechnique`, `ReceiveTechnique`, `SetTechnique`, `ServeTechnique`,
  `SoftTouch`, `CourtAwareness`;
- identity: `DominantHandV4`.

The frozen derived output is:

- Attack: `DirectionControl`, `SpeedControl`, `PowerCapacity`,
  `ContactHeightMeters`, `ApproachMobility`;
- Block: `Timing`, `HandControl`, `ReachHeightMeters`, `LateralMobility`;
- Defense: `Reaction`, `PlatformControl`, `CoverageMobility`, `Awareness`;
- Receive: `FirstTouchControl`, `Reaction`, `Movement`, `Awareness`;
- Set: `PlacementControl`, `TempoControl`, `SoftTouch`, `Movement`,
  `Awareness`;
- Serve: `DirectionControl`, `SpeedControl`, `PowerCapacity`, `Consistency`.

The published formula and coefficient table are both version 1. Changing a
formula or coefficient increments its corresponding version and must change the
derived result fingerprint, even when rounded numeric outputs happen to match.
Adding or changing an authoritative base field requires V5. Runtime planning,
execution and replay consume the immutable derived snapshot and record consumed
fields separately from the serialized base/derived contract.

Run the complete Gate A–E verification from the checkout under test. Do not add
`-quit`; Unity 6000 may exit before writing test results when batch tests use it.

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

rg -n "PlayerAbilitySnapshotV[123]|MatchContextV[12]|MatchResultV[12]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'
git diff --check
```

The Task 12 full-suite evidence on Unity `6000.0.43f1` is:

- EditMode: `505/505` passed, `0` failed, `0` skipped, `0` inconclusive
  after replacing three obsolete V3 Stage2 contract tests with one active-roster
  enumeration regression;
- PlayMode: `24/24` passed, `0` failed, `0` skipped, `0` inconclusive;
- both legacy production searches returned no matches and `git diff --check`
  was clean.

Gates A–K and Full Rally V4 are complete. Gate H formal authority is enabled only for V3 Authority
with a complete twelve-player formal roster. Immutable Gate F responsibilities
select receive/organization ownership; the Gate H coordinator owns revision,
fallback and bounded coverage decisions; the controller is the single writer
through Gate G player facades. Accepted formal Receive/Set replay events carry
their own exact authority receipt. Legacy 3v3 and V3 Shadow/Disabled remain
outside Gate H, while accepted Set hands off to the temporary Gate I attack seam.

Gate H completion evidence on Unity `6000.0.43f1` is EditMode `627/627`,
PlayMode `31/31`, and fixed-seed determinism `2/2`, all with zero failures,
skips or inconclusive results. Gate I completion evidence on Unity `6000.0.43f1`
is EditMode `719/719`, PlayMode `34/34`, and fixed-seed determinism `2/2`, all
with zero failures, skips or inconclusive results. Gate J completion evidence on
Unity `6000.0.43f1` is EditMode `737/737` and PlayMode `35/35`, including
strict/canonical Replay compatibility, recorder invariance and independent
fixed-seed byte/hash stability, all with zero failures, skips or inconclusive
results. Gate J is restricted to formal twelve-player V3 Authority; 3v3 and
Shadow/Disabled emit no perception receipt. Gate K completion evidence is
EditMode `745/745` and PlayMode `39/39`, with Director ownership scans clean,
three-panel Replay HTML, deterministic work-budget evidence and a four-axis
fixed-seed calibration matrix. macOS browser policy prevented opening the local
`file://` artifact for a visual pass; Windows x64 and profiler validation remain
release checks.

## Windows delivery

Windows x64 is the release platform. The committed workflow is intentionally
disabled until Unity `6000.0.43f1` has generated complete `ProjectSettings` and
`Packages/packages-lock.json` files on a Windows x64 development machine. Then pin
a matching Unity Windows runner or self-hosted runner, configure Unity activation
as a repository secret, add the verified batch-mode build command, and enable the job.
CI artifacts do not replace regular testing on real Windows x64 hardware for
keyboard, controller, graphics and performance checks.

## All-AI prototype verification

Open `Assets/Volleyball/Match/Scenes/AiRallyPrototype.unity` with Unity
`6000.0.43f1`, enter Play Mode, and observe at least ten completed rallies.
Confirm that every rally has a serve, receive, set, spike and defensive
response; the tactical camera retains all players and the ball; score advances
once per rally; and the next rally begins automatically.

From the repository root, run both automated suites with the same Unity editor:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/EditMode.xml" \
  -logFile "$PWD/TestResults/EditMode.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/PlayMode.xml" \
  -logFile "$PWD/TestResults/PlayMode.log"
```

Preserve the generated XML and log files as local review evidence; do not
commit `TestResults/`.

## MenShen decision benchmark

The MenShen volleyball decision benchmark is Editor-only development tooling.
It reads `MENSHEN_API_KEY` from the current process environment, writes local
reports under ignored `TestResults/MenShen/`, and must not be wired into Unity
player builds.

Run it from the repository root with Unity `6000.0.43f1`:

```bash
source "$HOME/.zshrc"
test -n "$MENSHEN_API_KEY" || { echo "MENSHEN_API_KEY is missing"; exit 1; }
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -projectPath "$PWD" \
  -executeMethod Volleyball.Editor.AI.MenShenBenchmarkCommand.Run \
  -logFile "$PWD/TestResults/MenShen-benchmark.log"
```

`MENSHEN_BASE_URL` is optional and defaults to the coding gateway. Non-local
HTTP endpoints are rejected; use HTTPS for real gateway runs. Live gateway runs
are not part of EditMode or PlayMode regression, and production builds must not
receive a MenShen key.

## Physical contact training

Open `Assets/Volleyball/Match/Scenes/PhysicsContactTraining.unity` to inspect the
new physics path. Play Mode cycles through three isolated drills: forearm pass,
overhead set and jump spike. The overlay reports `HIT`, centeredness and applied
technique control. A miss is not repaired by moving the ball or advancing the
drill as if contact occurred.

The training scene is intentionally separate from `AiRallyPrototype`: the old
scene remains the controlled-arc comparison baseline until interception and the
six-player physical rally director pass their own acceptance tests.

## Physical 3v3 cooperative loop

Open `Assets/Volleyball/Match/Scenes/Physical3v3Rally.unity` and enter Play Mode.
One simulated ball runs position-aware possessions until one team reaches 15 points
with a two-point lead, or reaches the absolute 50-point cap first. At 50 points the
set ends immediately even if the lead is only one point. All three players are
evaluated for receive, set and attack
from their actual world positions, available time and abilities. Defender, setter
and attacker are scoring preferences rather than action locks, so a reachable
non-setter can organize and a reachable defender can attack.
The scene then stops and displays `RESULT READY`; new matches expose one validated
`MatchResultV4` with all six active-player statistics from a native V4 context.
A legal opponent-court
landing after the final touch scores, while an own-court landing, out-of-bounds
opponent-court landing, antenna fault or contact timeout gives the point away.
Net contact itself is legal when the ball later crosses the net inside the antenna
interval and above net height.

Receive, Set and Attack consume the current team's normal three-touch allowance.
The fourth counted touch and same-player consecutive counted contact are rejected
before the ball response is applied. A scheduled Block uses six swept capsules that
follow the visible left/right upper arms, forearms and palms; head, torso, hips, legs
and feet are excluded. It consumes zero team touches. Whichever team controls the
rebound
starts a fresh possession at zero counted touches; the remaining defenders can
move into non-contact coverage positions.

An accepted physical Block also plays a short team-colored impact core, expanding
ring, light flash and ball-trail pulse at the swept impact center. The feedback is
created in code and does not change the rebound or rally state. During automated
verification, `BlockImpactEffects` must equal `PhysicalBlockContacts`, and the test
must observe at least one frame where `BlockImpactFeedback.IsPlaying` is true.

Unity world coordinates use `X` for left/right, `Y` for height and `Z` for court
depth. Team-local tactics mirror only world `Z`; world left/right is never mirrored
between teams. Logs expose the selected actor/action, score terms, approach quality,
block assignment/contact, post-block possession and complete result for review.

Player roots are clamped to their own court with a small net and sideline margin,
and all six tactical roots are reset at the start of every rally. A scheduled blocker
stands 0.18 metres from the centre line, squares to the net, and uses a visible
forward/inward arm pose; arms may reach across during a legal spike or block while
the player root and visible torso remain on their own side.
Blue and Orange use the same role ability profiles, while seeded execution error
continues to model ordinary imperfect contacts without a team-specific advantage.

The default scene uses only the immediate deterministic planner. A runtime adapter
may implement `IRallyTacticalWeightSource` and be passed to
`ThreeVsThreeRallyDirector.InitializePrototypeV4` or
`ConfigureAiDecisionSource`. While that
optional request is pending, `AiDecisionTimeController` slows global simulation,
uses a real-time deadline, and restores the previous `Time.timeScale` and
`Time.fixedDeltaTime` after success or local fallback. Remote output remains limited
to bounded tactical weights; all legality and physics stay local.

Switch views with `1` for the tactical overhead camera, `2` for the sideline
broadcast camera, `3` for the smooth ball-follow camera, or `C` to cycle them.

Setters use a short elbow draw followed by a two-hand extension. Their local
target direction selects front, left/right side or back-set poses. Side sets
require `SetTechnique >= 0.55`, back sets require `0.78`, and emergency one-hand
sets require `0.90`; unavailable techniques fall back to a simpler visible pose
and receive an additional control penalty. One-hand setting is an explicit
emergency request, not the automatic result of an ordinary wide set.

New physical matches use `MatchContextV4` and each player carries one immutable
`DerivedMatchAttributesV4`. `AttackContactPlan` is the shared source for the planned takeoff,
setter target and actual striking-palm centre. Set flight time is solved inside the
selected rhythm's bounds; after the real set contact, the attacker replans from the
actual trajectory. A-E set quality, fallback, responsibility and counters are
recorded through Replay V4. No legacy initialization or result path is supported.

## Formal indoor 6v6 loop

Open `Assets/Volleyball/Match/Scenes/FormalIndoor6v6.unity` and enter Play Mode.
The scene creates one 9×18 metre court, one simulated ball and twelve visible players.
It plays a 25-point, win-by-two set, applies the same immediate 50-point cap, and
stops at `RESULT READY`; the result contains
one statistics entry for every player in the injected context.

The bottom roster panels show P1–P6, front/back row and the current server. A side-out
rotates the receiving team clockwise before its next serve. The setter, outside
hitters, opposite, middle blocker and libero profiles are mirrored across teams;
reachability can still choose an emergency non-specialist. Player roots remain in
their own half while hands can legally penetrate the net plane for attack or block.

Run the deterministic full-scene test with:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests" \
  -testResults "$PWD/TestResults/Formal6v6.xml" \
  -logFile "$PWD/TestResults/Formal6v6.log"
```

Formal 6v6 configures `V3RulesMode.Authority`; the V4 3v3 prototype remains
`Disabled` with no V3 adapter. Run the Phase 1 authority gate and retain its
local XML evidence with Unity `6000.0.43f1`:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/FullRallyV3-Phase1-final-edit.xml" \
  -logFile "$PWD/TestResults/FullRallyV3-Phase1-final-edit.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/FullRallyV3-Phase1-final-play.xml" \
  -logFile "$PWD/TestResults/FullRallyV3-Phase1-final-play.log"
```

The expected local artifacts are
`TestResults/FullRallyV3-Phase1-final-edit.xml` and
`TestResults/FullRallyV3-Phase1-final-play.xml`. The formal PlayMode assertions
require one V3 transition and replay event per committed accepted contact, zero
unexpected mismatches, and one score advance per completed rally.

Run the unified attack-chain calibration (30 in-system setter contacts in each
scene plus 20 symmetric formal sets) with:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests" \
  -testResults "$PWD/TestResults/AttackChainCalibration.xml" \
  -logFile "$PWD/TestResults/AttackChainCalibration.log"
```

## Match Replay V4 artifacts

The formal 6v6 replay test captures its first completed rally as validated
`MatchReplayV4` JSON and writes an interactive viewer beside it under the ignored
`TestResults/decision-replay/<run>/` directory. Sampling uses simulation time at
10 Hz plus an exact snapshot for each recorded event. These files are local
diagnostics; do not commit `TestResults/` or treat it as a save-game location.

Run the replay contract and artifact checks with Unity `6000.0.43f1`:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.MatchReplayV4Tests" \
  -testResults "$PWD/TestResults/MatchReplayV4.xml" \
  -logFile "$PWD/TestResults/MatchReplayV4.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests" \
  -testResults "$PWD/TestResults/FormalReplay.xml" \
  -logFile "$PWD/TestResults/FormalReplay.log"
find "$PWD/TestResults/decision-replay" -mindepth 2 -maxdepth 2 \
  -name index.html -print
```

Open one printed `index.html` path in a browser. The page loads its sibling
`replay.json`; direct local-file viewing also has an embedded fallback. Confirm
twelve player labels, score/server/rotation state, event navigation, decision
auto-pause and the six-row candidate table. Readers reject any format version
other than `4`; a future contract change must add a new version rather than
silently changing V4 semantics.

## Physics-contact upgrade baseline

The controlled-arc prototype remains the comparison baseline while the physical
ball is introduced behind a scene switch. On Unity `6000.0.43f1`, before the
upgrade began, the baseline produced:

- EditMode: 30/30 passing tests.
- PlayMode smoke: three rallies and three points in about 33.29 seconds.
- PlayMode soak: ten completed rallies in about 114.32 seconds.

These results prove the old rally loop, not physical contact quality. New runs
must additionally report ball discontinuities outside Reset, counted contacts
without a swept intersection, contact point error, predicted-versus-actual
landing error, maximum ball speed, and the current action phase. A regression is
any non-Reset position discontinuity, non-finite state, duplicate contact inside
one contact-group cooldown, or rally-state advance without a physical contact.
