# Geometric Attack-Block Counterplay Design

**Date:** 2026-07-22
**Status:** Design approved
**Related designs:** `2026-07-19-unified-multi-role-rally-decision-design.md`,
`2026-07-21-unified-attack-chain-design.md`, and
`2026-07-22-full-arm-block-contact-design.md`

## Goal

Make attack and block outcomes arise from physical geometry and reachable
movement rather than a single blocker covering the same predictable ball path.
The attack AI chooses a route from the predicted active blocker arm volumes;
the defence can schedule one, two, or three blockers; and the attacker begins
its approach as soon as a receive has established the expected setter and
attack responsibilities.

This design does not introduce a new scoring or "tool the block" rule. An arm
collision changes only the ball's physical trajectory. The existing referee
continues to determine the winner from the final player touch, legal net-plane
crossing, and ground landing.

## Preserved Referee Boundary

An accepted block remains the latest physical touch by the blocking team and
does not consume one of its three counted contacts. Once a block is accepted,
the director must not immediately create a receive window for either team.
Instead, the ball remains in the ordinary fixed-step simulation:

- if it lands in the opponent court, the existing ground referee awards the
  blocking team the point;
- if it lands in the blocking team's court or out of bounds, the existing
  ground referee awards the opponent the point;
- if it crosses the net legally, only then does the receiving side begin a new
  zero-count possession; and
- an illegal antenna crossing remains a fault by the final touching team.

No player is offered a receive contact for a ball that has already landed in the
opponent court. This removes the current erroneous post-block possession
transition while retaining the existing `MatchRallyReferee` scoring code.

## Attack Route Selection

Before an attack contact is scheduled, the AI evaluates deterministic candidate
outgoing velocities against the current predicted block geometry:

1. direct line and cross-court attacks at normal attack tempo;
2. a higher, slower over-hand route that clears the tallest reachable arm;
3. legal routes passing beside the outer arm volume; and
4. optional edge-contact routes whose physical collision can redirect the ball.

Candidates are rejected when their predicted net crossing or landing is
illegal. Their score combines legal landing margin, minimum clearance from the
active arm capsules, attacker's available power/technique, and a bounded route
preference. Edge-contact candidates are not awarded points or given a special
scoring outcome: they are ordinary physical collision candidates and must still
cross and land legally to win the rally.

The selection remains fully local and deterministic. An LLM may not set an
outgoing velocity or choose a route that bypasses geometry, reachability,
contact legality, or the existing referee.

## Multi-Block Scheduling

When an attack is predicted to cross the net, the defence selects a bounded
block unit of one to three players. The primary blocker is closest to the
predicted intercept after jump reach is considered. Eligible secondary blockers
are distinct teammates that can reach laterally adjacent intercept lanes before
the contact window. Their roots remain on their own court and retain the
existing net clearance.

Each blocker receives its own scheduled block action and contact group. The ball
simulation considers every active arm capsule through its existing earliest-hit
selection, so a fixed step still accepts at most one player contact. A multi-arm
collision therefore records one `TechniqueAction.Block`, one feedback event,
and one final-touch identity; it never consumes extra team touches or creates
duplicate block statistics.

For formal 6v6, only current front-row players are eligible. The 3v3 prototype
has no front-row restriction. Blockers that cannot meet their movement and
timing bounds are excluded rather than teleported into coverage.

## Early Attack Preparation

When a receive decision is scheduled, the existing provisional attack planning
already identifies a likely attacker, setter and approach. The chosen attacker
must immediately move toward the approach start. Once the organiser/setter
decision is confirmed, that same attacker continues toward the staged pre-set
target; it must not be reset to a distant neutral point. At the actual set
contact, replanning uses the real trajectory only to make bounded corrections to
the current approach and contact target.

The approach planner continues to be the authority for takeoff and contact
height. The director must preserve accrued movement progress so an attacker is
not penalized with an avoidable low-contact, far-from-net attack or a timeout
after a normal A--C set.

The planned takeoff is constrained to a near-net attack band measured from the
net plane on the attacker's own side. Outside hitters, opposites, and the 3v3
`Attacker` use a 0.75--1.50 metre band. Middle blockers use a 0.50--0.75 metre
band. The actual set contact may shift the takeoff within the applicable band
and laterally toward a reachable ball, but it must never make the actual ball
contact position itself the new takeoff depth.

The setter has its own organization-depth policy. At 1.50 metres or nearer to
the net, it is in its best organization area. Eighty percent of normal sets
from this area must target the attacker's best handling point: the valid point
inside its takeoff band with the greatest predicted space from active block-arm
geometry. The remaining twenty percent may use another legal point in that
same attack band to retain deterministic tactical variation.

When the setter is farther than 1.50 metres but no farther than the four-metre
line, every normal set must still target its attacker's ordinary takeoff band.
When the setter is behind the four-metre line, the applicable takeoff band moves
away from the net by one half of the setter's excess depth: each additional
metre moves both band limits 0.50 metres. This displacement is capped at 1.50
metres. Lateral adjustment remains bounded by ball reachability and the
player's accrued approach progress.

## Diagnostics And Tests

The director exposes diagnostic counters or replay-visible records for selected
attack route, scheduled blocker count, accepted blocker identity, and post-block
transition reason. Tests must establish:

1. an accepted block travelling directly into the opponent court does not open a
   receiver window before normal landing resolution;
2. legal block rebounds that cross the net start the correct zero-touch
   possession only after the crossing;
3. the attack planner selects a legal non-central route when the central arm
   lane is blocked, while retaining deterministic output for a fixed input;
4. one, two, and three reachable blockers can be scheduled without duplicate
   accepted block contacts; unreachable or back-row 6v6 players are excluded;
5. an attacker identified after receive begins moving before the set contact and
   retains that progress through actual-set replanning; and
6. existing referee, touch-count, replay, 3v3, 6v6, calibration, and symmetry
   tests remain valid.

## Compatibility And Risk

No Shared contract or score-result schema changes are needed. Existing replay
events remain compatible because attack and block are still their existing
actions. New optional diagnostics must not alter a replay's authority over
outcomes.

The main calibration risk is making the multi-block unit too dense and reducing
attack options again. Selection must therefore prefer geometry and reachable
coverage over forced blocker count, and attack trajectories must be validated
through the existing physical solver and referee rather than probabilistic point
awards.
