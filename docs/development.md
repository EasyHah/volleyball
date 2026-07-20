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

Write pure rule, scoring, rotation and statistics tests as EditMode tests. Keep
PlayMode regression coverage for complete 3v3 and formal 6v6 sets with Unity scene
integration. Use a fixed random seed for deterministic simulation tests. Record
the Unity editor and package lock versions used to reproduce each test run.

## Windows delivery

Windows x64 is the release platform. Unity `6000.3.20f1` has generated the committed
`ProjectSettings` and `Packages/packages-lock.json` baseline on a Windows x64
development machine. The Unity workflow remains intentionally disabled until a
matching runner and activation are configured and a dedicated build entry point
explicitly selects Standalone Windows x64, IL2CPP and Development options before
checking its `BuildReport`.
CI artifacts do not replace regular testing on real Windows x64 hardware for
keyboard, controller, graphics and performance checks.

## All-AI prototype verification

Open `Assets/Volleyball/Match/Scenes/AiRallyPrototype.unity` with Unity
`6000.3.20f1`, enter Play Mode, and observe at least ten completed rallies.
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

Run it from the repository root with Unity `6000.3.20f1`:

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
with a two-point lead. All three players are evaluated for receive, set and attack
from their actual world positions, available time and abilities. Defender, setter
and attacker are scoring preferences rather than action locks, so a reachable
non-setter can organize and a reachable defender can attack.
The scene then stops and displays `RESULT READY`; `ThreeVsThreeRallyDirector.Result`
contains one `MatchResultV1` with all six player statistics. A legal opponent-court
landing after the final touch scores, while an own-court landing, out-of-bounds
opponent-court landing, antenna fault or contact timeout gives the point away.
Net contact itself is legal when the ball later crosses the net inside the antenna
interval and above net height.

Receive, Set and Attack consume the current team's normal three-touch allowance.
The fourth counted touch and same-player consecutive counted contact are rejected
before the ball response is applied. A scheduled Block is a real physical palm
contact, but it consumes zero team touches. Whichever team controls the rebound
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
and all six tactical roots are reset at the start of every rally. Arms may still
reach across the net during a legal spike or block; the player root may not cross.
Blue and Orange use the same role ability profiles, while seeded execution error
continues to model ordinary imperfect contacts without a team-specific advantage.

The default scene uses only the immediate deterministic planner. A runtime adapter
may implement `IRallyTacticalWeightSource` and be passed to
`ThreeVsThreeRallyDirector.Initialize` or `ConfigureAiDecisionSource`. While that
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

## Formal indoor 6v6 loop

Open `Assets/Volleyball/Match/Scenes/FormalIndoor6v6.unity` and enter Play Mode.
The scene creates one 9×18 metre court, one simulated ball and twelve visible players.
It plays a 25-point, win-by-two set and stops at `RESULT READY`; the result contains
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
