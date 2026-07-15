# Match Architecture

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

## Future expansion

6v6 replaces simplified rotation only after the 3v3 control loop is reliable. The
V1 contract already includes a 6v6 fixture as an acceptance sample; it is not a
requirement to build 6v6 in this repository's first feature branch.
