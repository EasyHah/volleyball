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
camera, animation, Input System integration, physics presentation and audio. It
reads Domain state but does not decide scores or career consequences.

`Runtime/AI` reads the same domain state and supplies decisions for all athletes
except the created outside hitter. AI may not take control away from the player
during a direct rally.

## First 3v3 loop

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

The first code tasks must make the following domain rules testable without a Unity
scene: a rally begins from an AI serve, a point completes a rally, side-out rotates
the winning 3v3 team once, player eligibility automatically selects the correct
action, and all non-created athletes remain AI controlled.

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
`MatchSet` remains Unity-free and owns scoring, service transfer, rotation and
statistics. `RallyTouchState` continues to own three-touch legality, consecutive
contacts and block/serve exclusions.
