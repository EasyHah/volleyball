# Unified Game Architecture

The repository now targets one Unity project containing isolated Match, Career,
Shared and Bootstrap modules. The staged folder and Assembly Definition migration
is specified in `changes/unified-unity-modules-plan.md`; the sections below describe the
existing Match module boundary.

## Boundaries

`Runtime/Domain` is deterministic, Unity-free match logic. It owns rally phase,
score, simplified rotation, action eligibility and the facts collected for a result.
It receives a frozen match context and returns match facts.

`Runtime/Presentation` is the Unity adapter boundary. It owns scene objects,
camera, animation, the future Input System adapter, physics presentation and
audio. It reads Domain state but does not decide scores or career consequences.
At the current baseline, runtime input is limited to legacy camera shortcuts;
neither physical match scene has player-athlete controls yet.

`Runtime/AI` reads the same domain state. It currently supplies decisions for all
athletes in both the 3v3 and 6v6 physical sandboxes. Reserving the created outside
hitter for player control is a target integration rule, not current behavior; once
implemented, AI may not take control away from the player during a direct rally.

## Target player-controlled loop

The following loop is the intended direct-control experience and is not yet
implemented. The current 3v3 and 6v6 scenes are full-AI automated set prototypes.

```text
AI serve
  -> player receives/digs automatically if inside eligibility range
  -> AI setter chooses a playable set
  -> player approaches; spike jumps and contacts automatically in range
  -> player aiming reticle selects the attack target
  -> opponent blocks/defends through AI
  -> continue until a point
  -> rotate one position after a side-out
```

Before this target loop is connected, the control boundary, input actions and
player/AI hand-off must be specified and tested independently of the Unity scene.
The existing automated rally, scoring, side-out and rotation behavior remains the
regression baseline.

## Physical match formats

`PhysicalMatchRallyDirector` is the single Unity adapter for both physical formats.
The retained `ThreeVsThreeRallyDirector` and the formal
`FormalSixVsSixRallyDirector` are thin scene-facing types and contain no duplicated
rally logic. `PhysicalMatchConfiguration` supplies roster size, set rules, formation
and HUD identity.

The 3v3 scene remains a 15-point, win-by-two regression target. The independent
`FormalIndoor6v6` scene uses six rotation positions, a 25-point win-by-two set and
all twelve context players in the result. Position 1 serves; positions 2–4 are
front row; a receiving team that wins the rally rotates clockwise before serving.

Prototype court identity is team + role + roster slot, while `PrototypePlayerAgent`
stores the stable Shared `PlayerId` separately. This permits multiple outside
hitters with the same role without conflating physics identity and career identity.
The current scene bootstraps still create fixed sandbox contexts, and the formal
6v6 bootstrap derives the runtime `PlayerAbilityProfile` from position instead of
using `PlayerSnapshotV1.Ability`. Career integration must remove that override so
the frozen context actually drives physical performance.

`MatchSet` remains Unity-free and owns scoring, service transfer, rotation and
statistics. `RallyTouchState` continues to own three-touch legality, consecutive
contacts and block/serve exclusions.

The physical director also constructs both tactical planners with the fixed value
`7351`. `PhysicalRallyTacticPlanner` consumes that fixed value, while
`TeamRallyDecisionPlanner` currently does not; neither receives
`MatchContext.seed`. Context-seed determinism is therefore an integration gate,
not an existing guarantee. Every planner that uses randomness must consume a
context-derived seed; deterministic planners should remove unused seed parameters.
The target is reproducible pure-AI decision sequences for equal inputs and seeds,
with a directed different-seed test for randomized paths; Unity physics is not
promised to be frame-deterministic or to reproduce the final score exactly.
