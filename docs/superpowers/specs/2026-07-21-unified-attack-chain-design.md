# Unified Attack Chain Design

## Goal

Correct the setter-to-attack chain in both Physical3v3Rally and FormalIndoor6v6
before tuning error rates. The implementation shares the same ability model,
kinematics, orientation rules, quality classification, attribution, statistics,
and replay diagnostics across both scenes. The scenes continue to differ only in
court configuration, roster size, and tactical candidates.

## Contract Compatibility

`MatchContextV1`, its player and ability snapshots, `MatchResultV1`, and their
canonical hash algorithm remain immutable. Existing V1 JSON must deserialize,
validate against its original hash, and produce the same canonical hash as before.
V1 is not extended with `MaxAttackReach`.

New matches use a parallel V2 contract family: `PlayerAbilitySnapshotV2`,
`PlayerSnapshotV2`, `TeamSnapshotV2`, `MatchContextV2`, and `MatchResultV2`.
V2 contains the metre-based `MaxAttackReach` and computes a V2-only canonical
hash. `ContractJson` exposes explicit V1 and V2 serialize/deserialize methods;
it never silently treats one version as the other. Match runtime and Career
request boundaries accept a common read-only match-context abstraction so a
legacy V1 match can still finish while new attack-chain matches use V2.

`MatchContextV2.UpgradeFromV1` is the only legacy upgrade path. It creates a new
V2 context with a new V2 hash and assigns conservative deterministic reach
defaults from the old declared position only because V1 has no reach data:
setter/libero/defender 3.20m, outside/opposite 3.42m, and middle 3.48m. Those
values are migration defaults, not position restrictions; V2 callers may set any
valid player-specific reach. Upgrade tests retain an old V1 JSON fixture and
verify both unchanged V1 validation and explicit V2 migration.

## Scope And Order

Implementation proceeds strictly in this order:

1. Attack contact height.
2. Setter orientation and set technique.
3. Dynamic set-flight time.
4. Attacker replanning after the real set contact.
5. Set-quality classification and error attribution.
6. Fixed-seed calibration.

Error-rate adjustments are forbidden until the first five stages have passing
structural tests.

## Shared Ability And Contact Point

`PlayerAbilitySnapshotV2` and `PlayerAbilityProfile` gain a finite, metre-based
`MaxAttackReach` field. It is independent of position. Roster defaults give
ordinary attackers at least 3.20 metres, while strong attackers are in the
3.40-3.55 metre range.

An immutable `AttackContactPlan` is the single source of truth for an attack:

- intended takeoff point and attack contact centre;
- planned attack height, derived from maximum reach, approach completion,
  jump timing, and set quality;
- required and available approach time; and
- a reachable / adjustment / handling outcome.

The attack planner, the setter's ball target, and `PrototypePlayerAgent`'s palm
contact preview all consume this plan. At the scheduled contact instant, the
real attack-palm centre must be within 0.05 metres of its plan's contact centre.
No planner keeps the former fixed 2.7 metre attack height.

## Team-Local Setter Orientation

Every player has a prepared facing in the active `TeamCourtFrame`; it is separate
from transient movement heading. The normal setter stance is neutral: it faces
the local four-position direction with the near-net shoulder toward the net.
Blue and Orange obtain exact mirrored world orientations by converting this
single local stance through `TeamCourtFrame`.

Set style selection accepts the local target and an explicit ball-state class:

- `LeftPin` and `MiddleQuick`: `FrontTwoHand`;
- `RightPin` and `BackSet`: `BackTwoHand`;
- side two-hand and one-hand styles: only an off-target or emergency state.

Consequently, a normal in-system pass can never request or execute a side set.
The setting pose is driven from the same prepared facing used to classify the
style rather than from arbitrary world movement direction.

## Dynamic Set Rhythm And Flight

`TeamRallyTactic` stores a set rhythm, not an authoritative fixed set-flight
duration. Available ranges are:

| Rhythm | Range (seconds) |
| --- | --- |
| Close quick | 0.35-0.50 |
| Back quick / short fast | 0.45-0.70 |
| Fast pin | 0.75-1.05 |
| Adjustment | 1.05-1.35 |
| High ball | 1.30-1.80 |

`SetFlightSolver` evaluates discrete fixed-step times inside the chosen range.
It uses setter contact centre, `AttackContactPlan` contact centre, pass quality,
attacker distance and approach readiness. A candidate is valid only when the
existing ballistic solver can reach the target without a post-solve velocity
override and the trajectory has a legal, plausible apex. The selected time
balances the rhythm preference with attacker readiness and set precision.

The ordinary ball solver remains the sole producer of outgoing velocity. No
component may rescale that velocity after `SetFlightSolver` has selected its
time.

## Post-Contact Attack Replanning

Preplanning reserves an attacker and provisional contact plan so the setter has
a valid target. On the actual accepted set contact, the director predicts the
real outgoing ball trajectory and runs `AttackContactPlanner` again using the
actual arrival time, apex, and reachable contact region.

For a reachable A/B/C result, the selected attacker updates approach start,
takeoff, jump timing, and hand target before the attack window. For a D/E result,
the director cancels the spike window and schedules a controlled handling action
instead. This prevents an impossible strong spike from timing out merely because
the initial plan was imperfect.

## Set Quality, Attribution, And Replay

`SetQualityAssessment` records horizontal, height, and arrival-time error;
net distance; adjustment feasibility; remaining approach time; grade; and a
primary responsibility.

| Grade | Meaning |
| --- | --- |
| A | In system; full attack available. |
| B | Attackable with a small adjustment. |
| C | Adjustment attack or tip only. |
| D | Normal attack unavailable; handling only. |
| E | Direct setting error. |

After the attacking outcome, attribution is resolved as follows:

- an A/B set followed by a miss, net fault, or out ball is an attacker error;
- a D/E set that prevents a normal attack is a setter error;
- mixed causes retain both diagnostic reasons, but technical statistics record
  one primary responsible player.

The existing replay event pipeline is extended rather than duplicated. It records
the planned and actual attack contact centres, set quality measurements and grade,
the replan result, handling fallback, outcome attribution, and per-rally reason.
The match exposes counters for set in-system rate, attackable-set rate, direct
set errors, A-grade attack success, and attacker adjustment success.

## Tests And Calibration

EditMode tests cover ability serialization, contact-height agreement, normal
set-style selection, Blue/Orange local-frame mirroring, rhythm bounds and
ballistic reachability, replan versus handling fallback, quality boundaries, and
primary attribution.

PlayMode tests exercise both existing scenes. A deterministic in-system
first-pass harness runs 100 attacks and records replay evidence. A deterministic
symmetric-match harness runs 20 sets. Initial acceptance thresholds are:

- high-skill setter attackable-set rate on in-system first passes: at least 95%;
- no-contact errors after A-grade sets: below 2%;
- side sets only for marked off-balance or emergency states;
- worsening first-pass quality degrades set quality smoothly;
- symmetric 20-set win rate: 45%-55%; and
- every abnormal ball has an explicit replay reason and primary responsibility.

Calibration changes only bounded quality/error coefficients and roster ability
defaults. It never compensates for a failing contact, orientation, flight, or
replan structural test.
