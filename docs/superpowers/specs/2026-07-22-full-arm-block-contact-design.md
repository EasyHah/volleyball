# Full-Arm Block Contact Design

**Date:** 2026-07-22  
**Status:** Design approved  
**Related design:** `2026-07-21-unified-attack-chain-design.md`

## Goal

During an active physical block window, the ball can contact either player's
palms, forearms, or upper arms. Every accepted contact uses the existing
`TechniqueAction.Block` rules, shares one contact group for the blocker, does
not consume one of the team's three counted contacts, and produces at most one
accepted block event for that fixed step.

Head, torso, hips, legs, and feet are explicitly outside this change.

## Geometry

The visible rig remains the source of truth. Each active blocker exposes six
moving capsule volumes:

- left and right upper arms: shoulder joint to elbow joint;
- left and right forearms: elbow joint to hand joint;
- left and right hands/palms: hand joint to palm joint, with a palm-sized radius.

Capsule endpoints are captured before and after each fixed simulation step so
the contact model follows the animated block pose. All six capsules receive the
same block contact group ID. The existing one-sided palm planes are not used as
the complete block collision model because they cannot represent side or back
contacts.

## Collision and Response

The deterministic custom ball simulation remains authoritative; Unity physics
colliders stay disabled. A swept ball sphere is tested against each moving arm
capsule. The solver returns the earliest time of impact, closest contact point,
outward normal, and velocity of the contacted capsule point.

Arm capsule contacts compete with all other ball contacts through the existing
earliest-contact selection. Once accepted, the existing block response,
feedback, replay event, continuation, and referee handling are reused without
special counters or bypasses.

## Scheduling

Arm volumes are active only while the scheduled block action's contact surface
window is active. Block movement and jump timing remain driven by the existing
action timeline. The abandoned palm-height-specific jump adjustment is removed;
reachability comes from the actual animated arm volumes and the predicted block
position.

The blocker stands with its root 0.18 m from the center line, leaving the visible
torso on its own side of the net, and is reoriented square to the net when the
block is scheduled. The block pose presses both arms slightly forward and inward:
the visible palms penetrate across the net plane and the forearm seam is narrower
than a ball. This placement is part of the physical geometry, not an enlargement
of the collision capsules.

## Verification

Tests must prove all of the following before scene calibration:

1. The block pose exposes exactly six arm/hand capsule snapshots, all sharing
   one contact group and following visible joints.
2. A swept ball can hit a forearm or upper arm while missing both palms.
3. A side contact on a hand/palm is detected despite not crossing the old palm
   plane from its front.
4. Multiple overlapping arm capsules still produce one accepted block contact.
5. No arm block contacts are emitted outside the active block window.
6. Existing receive, set, attack, serve, replay, and touch-count behavior remains
   unchanged.
7. The fixed-seed ordinary 3v3 test records at least one physical block while
   retaining non-setter sets and defender attacks; the 100-sample 3v3/6v6
   calibration and 20-set symmetry tests continue to pass.
8. A player left facing an earlier set route is squared back toward the net before
   its block arm volumes become active.

## Compatibility and Risk

No Shared V1/V2 contract changes are required. Replay remains compatible because
the accepted action is still `Block`; the contacted limb does not become a new
serialized field. The main risk is duplicate contacts at elbow or wrist overlap,
mitigated by the shared contact group and earliest-contact selection.
