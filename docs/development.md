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

Write pure rule, scoring, rotation and statistics tests as EditMode tests. Use
PlayMode tests for one full 3v3 rally with Unity scene integration. Use a fixed
random seed for deterministic simulation tests. Record the Unity editor and package
lock versions used to reproduce each test run.

## Windows delivery

Windows x64 is the release platform. The committed workflow is intentionally
disabled until Unity `6000.0.43f1` has generated complete `ProjectSettings` and
`Packages/packages-lock.json` files on a Windows x64 development machine. Then pin
a matching Unity Windows runner or self-hosted runner, configure Unity activation
as a repository secret, add the verified batch-mode build command, and enable the job.
CI artifacts do not replace regular testing on real Windows x64 hardware for
keyboard, controller, graphics and performance checks.

## All-AI prototype verification

Open `Assets/VolleyballMatch/Match/Scenes/AiRallyPrototype.unity` with Unity
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

## Physical contact training

Open `Assets/VolleyballMatch/Match/Scenes/PhysicsContactTraining.unity` to inspect the
new physics path. Play Mode cycles through three isolated drills: forearm pass,
overhead set and jump spike. The overlay reports `HIT`, centeredness and applied
technique control. A miss is not repaired by moving the ball or advancing the
drill as if contact occurred.

The training scene is intentionally separate from `AiRallyPrototype`: the old
scene remains the controlled-arc comparison baseline until interception and the
six-player physical rally director pass their own acceptance tests.

## Physical 3v3 cooperative loop

Open `Assets/VolleyballMatch/Match/Scenes/Physical3v3Rally.unity` and enter Play Mode.
One simulated ball continuously follows blue receive-set-spike, orange
receive-set-spike, then repeats. A missed body contact, net touch or ground touch
ends that attempt and restarts the loop; no scripted ball teleport advances a hit.

Switch views with `1` for the tactical overhead camera, `2` for the sideline
broadcast camera, `3` for the smooth ball-follow camera, or `C` to cycle them.

Setters use a short elbow draw followed by a two-hand extension. Their local
target direction selects front, left/right side or back-set poses. Side sets
require `SetTechnique >= 0.55`, back sets require `0.78`, and emergency one-hand
sets require `0.90`; unavailable techniques fall back to a simpler visible pose
and receive an additional control penalty. One-hand setting is an explicit
emergency request, not the automatic result of an ordinary wide set.

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
