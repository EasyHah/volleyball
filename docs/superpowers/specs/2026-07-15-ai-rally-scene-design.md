# All-AI 3v3 Prototype Scene Design

## Purpose

Build the first playable visual slice of Volleyball Match: a self-running 3v3
indoor volleyball rally. It validates court scale, player readability, camera
framing, ball trajectories, AI pacing and full rally rhythm before player
controls or production character assets.

The scene continuously runs complete all-AI rallies. Each rally starts with a
serve, progresses through volleyball contacts, awards exactly one point, and
automatically resets into the next serve.

## Confirmed Product Decisions

- Visual direction: **bright prototype gym**. Use high-contrast, simple
  geometry and team colors rather than realistic materials, spectators or
  lighting.
- Camera: **high tactical view**. Show the entire 3v3 court, ball and all six
  players throughout normal play. Do not use dynamic cuts, collision avoidance
  or a player-follow camera in the first version.
- Players: **procedural jointed stick figures**. Each player has an explicit
  head, torso, upper/lower arms, upper/lower legs, hands and feet, assembled
  from Unity primitives. Joint pivots allow smooth pose interpolation.
- Match control: **all AI**. Do not include keyboard, controller or user
  player control in this slice.
- Motion: use authored key poses and interpolation for locomotion and contacts.
  Do not require IK, imported character models, ragdolls or fully emergent
  rigidbody volleyball simulation.
- Rally control: use deterministic AI choices and controlled ball trajectories
  so every sequence stays readable and reaches a point reliably.

## Scene Layout

Create one playable scene under `Assets/VolleyballMatch/Scenes/`. It contains
a bright indoor environment, standard-proportion 3v3 court markings, a centered
net and posts, a directional light, a high orthographic or perspective camera,
and a small score display.

Use a compact 3v3 court rather than official 6v6 dimensions. Give it obvious
sidelines, end lines, center line, team halves and enough free space for camera
framing. Keep court dimensions and player locations in one configuration
component so later domain integration can map the board without scene rewrites.

Place three blue and three orange/red stick figures on opposite halves. Assign
each team a stable setter, left-side attacker and defender formation. Identify
players by color and jersey number or marker, not detailed meshes.

## Component Boundaries

| Component | Responsibility |
| --- | --- |
| `CourtBuilder` | Creates/configures court, net, markings, lighting and camera-safe environment. |
| `StickFigureRig` | Builds primitive hierarchy, exposes named joints, applies named key poses and interpolates transforms. |
| `AiRallyDirector` | Owns deterministic rally state, chooses next action, drives destinations/poses and launches controlled ball flights. |
| `BallFlight` | Moves the ball along a parameterized arc from one contact point to a target zone. |
| `ScoreDisplay` | Renders the director-owned score only. |

Supporting player-agent components may hold team, role, assigned destination
and rig reference. `AiRallyDirector` is the sole scene component permitted to
advance a rally or update score. The future Domain module replaces these local
choices; this prototype must not claim authority for contract results,
progression or persistent player state.

## Rally Flow

Each rally is a finite state sequence with explicit timeouts. A fixed random
seed supplies small repeatable variations for replayable defects.

```text
Reset formation
  -> AI serve
  -> receiver moves and receives/digs
  -> setter moves and sets
  -> attacker approaches, jumps and spikes
  -> defender blocks or digs
  -> repeat an eligible receive/set/attack exchange
  -> ball lands, is blocked out, or exceeds contact limit
  -> award point and update UI
  -> brief celebration/reset delay
  -> next rally
```

The director selects a target player before each ball flight. That player starts
moving before ball arrival and reaches contact pose at the planned time.
`BallFlight` creates a readable arc from outgoing hand/contact point to target
zone: an apex for serves, sets and attacks, or a short downward arc for points.
No uncontrolled Rigidbody collision decides a rally in this slice.

AI uses deterministic, small rules rather than pathfinding or tactics:

- Server selects a legal opposing receive zone.
- Nearest eligible player receives or digs.
- Setter takes second contact when reachable; otherwise nearest teammate makes
  an emergency set.
- Available attacker approaches and spikes to one of several opposing zones.
- Defender nearest the landing zone digs; near-net defender blocks attacks.
- Contact and exchange caps avoid deadlocks. If no response is legal, attacking
  team earns the point.

Movement interpolates toward assigned targets. Formation offsets avoid obvious
overlap; NavMesh is deferred.

## Motion Set

The rig needs the following named pose families. Each named action has a
defined eased transition duration. Locomotion loops alternating arms and legs.

| Motion | Visible requirement |
| --- | --- |
| Idle / ready | Bent knees, forward hands and subtle weight shift. |
| Move / run | Figure travels to target while arms and legs alternate. |
| Serve | One hand tosses/strikes as ball starts first arc. |
| Receive / dig | Low stance and forearms forward at ball arrival. |
| Set | Hands above head, brief extension at ball departure. |
| Approach / jump | Short approach, bend, upward translation and landing. |
| Spike | Jump with striking-arm wind-up and downward follow-through. |
| Block | Near-net jump with both arms extended upward. |
| Landing / celebrate | Return to ready, then short point acknowledgement. |

All endpoints return to stable ready/idle. Rally code passes a pose name and
normalized blend duration; it may not manipulate individual limbs directly.

## Acceptance Criteria

1. The high camera frames all players, net, court markings and ball without a
   major occlusion.
2. Six differentiated stick figures hold 3v3 formation and move to assigned
   targets with no limb snapping or prolonged overlap.
3. Each rally visibly includes serve, receive, set, attack, a defensive response
   or block opportunity, a point and reset.
4. Ball arcs are readable and align with the acting hand/contact pose.
5. Score increments once per rally, next serve starts without input, and the
   scene runs for at least ten rallies.
6. A configured seed reproduces action sequence and score progression across
   Play Mode runs.

## Deferred Scope

- Human movement, aiming and automatic action selection.
- Official 6v6 rotation and full rule enforcement.
- Imported humanoid models, clips, IK, foot placement and ragdolls.
- Rigidbody-led ball contacts, player collisions and advanced physics.
- NavMesh, strategic AI, difficult-ball recovery and learning systems.
- Audio, crowd, polished materials, menus, persistence and contract result
  submission.

## Testing and Verification

Write deterministic EditMode tests for rally sequence, one-point award, contact
ordering, exchange caps and seeded reproducibility. Add a PlayMode test that
loads the scene, advances simulated time through multiple completions, and
checks score changes exactly once per completion, a new rally begins and no
scene exceptions are logged.

Manually inspect pose readability, camera framing, ball-to-hand contact timing
and uninterrupted play through ten rallies. Record Unity `6000.0.43f1`, package
lock state and test commands in the implementation pull request.
