# Unified Multi-Role Rally Decision Design

**Goal:** Replace the physical 3v3 scene's fixed
`Defender -> Setter -> Attacker` contact script with a deterministic, legal,
position-aware decision system. Every athlete can receive, set, attack, block,
or cover when reachable. Nominal roles remain preferences rather than locks.

## Scope

This is Match-only work inside `Assets/Volleyball/Match`. It preserves the
existing 15-point, win-by-two `MatchSet` and its `MatchResultV1` output. It does
not change Career, Shared contracts, the MenShen gateway protocol, scene
bootstrap, roster size, substitutions, or 6v6 rotation.

The milestone includes physical block contacts because a multi-role decision
system cannot correctly apply three-touch rules while block actions are visual
only. It replaces the current fixed six-contact loop, including its expected
actor/action fields, rather than layering exceptions on top of it.

## Coordinate Contract

Unity world coordinates are the sole representation for scene transforms,
ball integration, collision surfaces, net crossings, ground landings, and
movement distances:

- `Vector3.x` is world left/right and is never mirrored.
- `Vector3.y` is world height; ground is `0`.
- `Vector3.z` is world court depth; the net plane is `z = 0`.
- Blue occupies negative world depth and Orange occupies positive world depth.

Tactical evaluation uses a small team-local frame. It preserves world left/right
and height but mirrors depth so both teams reason with the same forward axis:

```text
local.x = world.x
local.y = world.y
local.z = -teamSideSign * world.z

Blue teamSideSign   = -1
Orange teamSideSign = +1
```

In local coordinates, `z < 0` is own court, `z = 0` is the net, and `z > 0`
is opponent court for either team. Code must use `worldHeight`, `worldDepth`,
`localDepth`, and `localForward`; it must not call court depth "y". A dedicated
Unity-free `TeamCourtFrame` owns this conversion so there are no scattered
Blue/Orange sign branches in planners or the director.

## Rule State

`RallyTouchState` is a Unity-free state object and the authority on legal
player contacts during an active rally. It owns:

- `LastPhysicalTouch`: the latest accepted physical player contact, including a
  block. It drives fault attribution and final landing/out-of-bounds scoring.
- `PossessionTeam` and `CountedTeamTouches`: the team currently organising the
  ball and its 0--3 counted contacts in that possession.
- `LastCountedActor`: the latest counted toucher. A normal contact by that same
  player is illegal; a prior block does not prohibit the blocker from making the
  following counted contact.
- `ContactWindow`: the allowed team, actions, candidate actors, response target,
  and expiry for the immediate next physical contact.
- `ExchangeTacticRevision`: a frozen tactic snapshot for one attack/defence
  exchange; it changes only when possession is reset or the rally ends.

Normal Receive, Set, and Attack contacts increment the current team's count.
The fourth attempted counted contact is a fault before it is allowed to alter
the ball. A block is a physical touch but increments neither team count.

A real block resets the team that next controls the ball to zero counted touches:

- If the blocked ball returns to the attacking team, that team begins a new
  possession with three available touches.
- If it stays or returns to the blocking team, that team begins a new possession
  with three available touches.
- If it lands or is ruled out directly after a block, the blocker's identity is
  the last physical touch for normal referee attribution.

## Candidate Filtering And Physics Order

`SimulatedBall` currently selects a collision candidate and applies its response
before `ThreeVsThreeRallyDirector` can reject it. The new path must expose a
candidate eligibility predicate or equivalent resolver to the ball simulation:

```text
all active contact surfaces
  -> RallyTouchState filters legal actor/action/window candidates
  -> choose earliest swept legal candidate
  -> apply physical and technique response
  -> notify director to update RallyTouchState and request the next decision
```

An illegal candidate must be excluded before any response velocity is applied.
This prevents a rejected fourth touch, a wrong-side block, or a stale action
window from physically redirecting the ball and then causing a timeout.

The ball simulation must compare the earliest legal player collision with the
earliest ground/net collision and net-plane crossing by time fraction. A block
that intercepts the ball at the net must suppress a later same-step net-plane
crossing event. The director handles events only after this ordering has been
resolved.

## Dynamic Team Decision

`TeamRallyDecisionPlanner` is Unity-free and deterministic for a seed, tactic
revision, team-local ball prediction, and player snapshots. It returns an
explainable decision containing selected actor, action, contact target, movement
target, outgoing ball intent, score breakdown, and backup candidates. It does
not move transforms or alter the ball.

Every possession evaluates these stages:

1. **Receive / dig:** all three athletes are candidates. Score reachable-time
   margin, reaction/mobility, receive technique, current location, and expected
   pass quality. The first physical receiver becomes the first counted actor.
2. **Set / organise:** exclude the latest counted actor. Prefer the nominal
   setter, but let the attacker or defender set when the setter is unreachable
   or scores lower. The set target is an attacker's future take-off point, not
   their current world-depth position.
3. **Attack:** exclude the latest counted actor. All three athletes can attack;
   nominal attacker preference is a score bonus, not an eligibility rule.
4. **Block / cover:** all valid near-net defenders are candidates. Blocker
   selection uses the predicted real net-plane intercept, not a nominal attacker
   lane. Other players receive coverage or transition tasks.

Role preference is deterministic, visible in diagnostics, and intentionally
weaker than infeasibility: an unreachable setter must lose to a reachable
defender, and an unreachable attacker must lose to a reachable setter or
defender.

## Movement, Approach, And Attack Quality

Movement uses world-space speed limits. A player is reachable only when its
ground distance can be covered before the action timeline's contact lead time,
after reaction delay. No position correction may teleport a transform; existing
limited alignment remains bounded and cannot turn an unreachable plan into a
contact.

For an attack plan, the planner determines a future take-off point and an
approach start point in team-local space. Its quality score includes:

- reachable approach distance, increasing jump-quality up to a capped,
  smooth curve;
- the angle between approach direction and desired spike direction, with larger
  separation reducing jump/attack quality;
- player mobility, jump, attack technique, and attack power;
- a technical tolerance that widens allowed spike direction choices rather than
  permitting impossible movement; and
- legal world-space landing targets derived from the selected local spike route.

The existing visual jump uses the resulting approach quality as an input. The
actual ball target and net crossing remain world-space calculations. A player
may run in any direction, but the quality penalty makes implausible long or
poorly aligned approaches uncompetitive.

## Physical Block Contacts

The existing support block pose becomes a scheduled physical block window. The
blocker moves toward a predicted net-plane intercept before the attack. After
the attack's actual outgoing velocity is known, the director re-predicts the
ball's net-plane intercept and adjusts only within a bounded movement/timing
window. During that window the block palms register `TechniqueAction.Block`
candidates.

The resulting block is fed through the same candidate eligibility and swept
collision path as every other contact. It updates `LastPhysicalTouch`, uses a
block-specific response, does not increase `CountedTeamTouches`, and opens the
correct zero-touch possession window based on which team receives the rebound.

## LLM Boundary

MenShen or another LLM may propose a tactical intent such as route preference,
aggression, preferred blocker, or bounded score-weight adjustment. It may not
select a transform, bypass a reachability check, choose an illegal candidate, or
write ball velocity. The local planner validates and deterministically resolves
the final plan. Gateway failure, timeout, malformed JSON, and quota limiting
fall back to the deterministic default weights without interrupting a rally.

## Presentation And Diagnostics

`ThreeVsThreeRallyDirector` becomes a presentation/orchestration adapter:
it asks for decisions, schedules agents, submits physical contacts to
`RallyTouchState`, calls `MatchSet`, and renders the result. It no longer owns
the fixed `_sequence`, `_expectedIndex`, or role-indexed contact centers.

The overlay and logs expose enough evidence to inspect decisions:

- possession team and counted touch number;
- selected actor/action, nominal-role override, and primary score terms;
- set take-off target, approach distance, angle penalty, and attack quality;
- physical block assignments, actual block contacts, and post-block possession;
- non-setter sets, defender attacks, and emergency receive contacts.

`MatchSet.RecordContact` receives the real candidate actor and their actual
assigned movement distance. Existing result statistics remain compatible.

## Tests And Acceptance

EditMode tests cover:

- team-local coordinate transforms: world `x` and `y` stay unchanged while
  only world `z` mirrors per team;
- reachability and deterministic candidate ranking;
- nominal role preference when candidates are equally reachable;
- a non-setter winning organisation when the setter is infeasible;
- a defender winning attack when the nominal attacker is infeasible;
- same-player consecutive counted contacts being rejected;
- counted contacts one through three being legal and a fourth rejected;
- a block recording a physical touch while consuming zero counted touches;
- post-block possession reset for either rebound direction;
- attack approach distance, capped jump-quality curve, and angle penalty;
- LLM tactical weight validation and deterministic fallback.

PlayMode acceptance for `Physical3v3Rally` confirms a complete, valid 15-point
result; all six player statistics; physical block contacts; at least one
non-setter organisation; at least one defender attack; at least one post-block
continued rally; no ball non-finite states; no unbounded position correction;
and stable camera switching. Tests must use Unity Test Runner without `-quit`
and inspect generated XML for zero failures.

## Risks And Boundaries

This is a substantial replacement of the fixed-loop prototype. The principal
risks are timing regressions (contact windows closing before a legal swept
collision), eligibility mistakes that let an invalid candidate alter the ball,
and tactical plans that ask for more distance than the player can cover. The
design contains those risks with pure-state unit tests, deterministic scoring,
bounded movement, candidate filtering before response, and a full PlayMode set
completion run.

The work deliberately does not attempt full FIVB positional faults, double
contacts, carries, substitutions, six-player rotations, human input control, or
networked simulation. They require separate rule and input specifications.
