# Full Rally V3 Phase 2--9 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete Full Rally V3 as a deterministic, explainable formal-6v6 rally system without treating V4 as a delivery dependency.

**Architecture:** Phase 1 remains the authority for lineup eligibility and accepted-contact rules. An immediately following compatibility-runtime stabilization stage fixes visible continuous attack motion, the single setter organization policy, and evidence for currently live attributes without waiting for the full planner. Subsequent work makes V3 ability input and the planning/execution envelope real, then replaces the fixed three-stage rally chain slice by slice. Every slice writes compatible `MatchReplayV2` evidence as it becomes authoritative; Phase 9 completes presentation, replay hashing, and calibration rather than inventing a second simulation path.

**Tech Stack:** Unity 6000.0.43f1, C#, NUnit EditMode/PlayMode, `Volleyball.Shared`, Match Domain/AI/Presentation assemblies, versioned `MatchReplayV2` contracts.

---

## Baseline and Release Rules

- Formal 6v6 keeps `V3RulesMode.Authority`; 3v3 stays explicitly disabled and source-compatible.
- `docs/rules.md` remains the sole normative source for rule changes. Every stage that changes match behavior updates its applicable rule traceability and `docs/changes/2026-07-23-001-full-rally-v3-architecture.md`.
- A shadow planner, comparator, or replay writer is read-only. It cannot move a player, change ball velocity, advance rules, or award a point.
- Every authoritative slice is guarded by a subsystem/vertical-slice flag. Remove its legacy writer only after its shadow and authority acceptance gates pass; never retain two tactical writers indefinitely.
- All sampled behavior is a pure function of persisted match inputs, explicit version/configuration, and deterministic sample keys. Wall-clock telemetry may fail a performance gate but must not choose a different match result.
- `MatchReplayV2` starts with additive optional sections. A section is not advertised in the HTML overlay until its producer is authoritative or visibly marked `Shadow`.

## Dependency Graph

```text
P1 V3 rule authority
  -> P1.5 compatibility runtime stabilization
       (continuous attack roots + SetterOrganizationZone + live-ability evidence)
  -> P2 ability projection + shared envelope + trajectory cache
  -> P3 12-player shadow responsibility plans
  -> P4 PlayerAgent component facade
  -> P5 receive / organization V3-plan authority (reuses P1.5 policy)
  -> P6 attack / defense / reorganization authority slice
  -> P7 perceived inputs and CourtAwareness
  -> P8 director removal of tactical decisions
  -> P9 replay overlays, replay hash, fixed-seed calibration

P4 can start after P3 has frozen the facade-facing plan/envelope interface. P5
does not redefine first-pass or setter preference: it adopts P1.5's tested policy
as a V3 plan/executor responsibility. P9's instrumentation begins additively in
P1.5/P2, but its calibration and completion gate waits for P8.
```

The pending Phase-1 attack-geometry fact is a P2 prerequisite for the P6 attack
authority switch: actual takeoff point, actual contact point, and net/front-zone
facts must reach `FullRallyV3RulesRuntimeAdapter`. Do not claim front-zone or
above-net back-row/libero restrictions are authoritative before that fact exists.

## Stage 1.5: Compatibility Runtime Stabilization

**Depends on:** Phase 1 formal 6v6 V3-rules authority. It is deliberately before
the planner stages because all three problems are already visible in the current
runtime and have no dependency on V4 or twelve-player composition.

**Scope**

- Create pure `SetterOrganizationZone` as the only definition of the 2.5-position
  organization area. In attack-team coordinates it owns default `(x=1.5m,
  depth=1.1m)`, lateral quality bands `5--7m` best, `3--5m`/`7--8m` secondary,
  otherwise poor (measured from the position-4 sideline), and depth quality bands
  `0--1.5m` best, `1.5--4m` secondary, otherwise poor. It converts through
  `TeamCourtFrame`; no director or tactic literal may duplicate those values.
- Make the registered setter wait/preposition at that zone, and make a normal
  first pass target its touchable center. Preserve current attack locations,
  attack-line constraints, and the rule that an approach band moves back when the
  **actual** setter is more than 4m from the net.
- Replace score-first backup organization with a legal/reachable registered-setter
  gate: evaluate the setter against the same move speed, reaction delay, actual
  pass arrival, and contact window used by the decision path. If reachable, the
  setter is the only normal organizer even when displaced from the zone. Only an
  unavailable, unreachable, or current-first-contact setter permits a legal
  non-setter fallback; otherwise retain the existing save/loss path.
- Make attack approach, takeoff, contact, and landing one speed-bounded root-motion
  trajectory. Limit only attack contact alignment to `0.18m`; convert remaining
  contact mismatch into actual contact/execution deviation. Do not change the
  existing receive/set/controlled-handling correction bounds in this stage.
- Add fixed-seed, one-variable benchmarks and compatible replay diagnostics for
  **currently live** fields: `Mobility`, `Reaction`, `Jump`, `ReceiveTechnique`,
  `SetTechnique`, compatibility-mapped `AttackTechnique`, `AttackPower`, and
  `MaxAttackReach`. These establish the baseline that later V3-axis tests extend.

**Non-goals**

- No V3 12-player plan/executor authority, shared execution envelope, or new
  V3-axis activation. `AttackControl`, `SoftTouch`, `BlockTechnique`, and
  `CourtAwareness` remain visibly `Reserved`.
- No change to legal contact sequence, libero/back-row restriction, existing attack
  target geometry, or a fake setter touch when physical/rule constraints reject it.
- No V4 attribute contract, height/reach derivation, or broad component extraction.

**Acceptance**

- EditMode: test the two mirrored organization points; every lateral/depth band
  boundary; default normal-pass target; reachable setter priority over a
  higher-scoring non-setter; unreachable, same-contact, and illegal setter fallback;
  and no-candidate preservation of the existing error path.
- EditMode: sample planned attack motion across approach, takeoff, contact, and
  landing. Assert adjacent root positions remain speed-bounded, attack correction
  never exceeds `0.18m`, and residual error remains observable instead of moving
  the root.
- EditMode: fixed seeds hold all inputs except one listed live ability; high values
  demonstrate the documented monotonic mechanism (reach/delay/error/approach or
  contact height). The technical-versus-power hitter comparison reports candidate
  score, execution error, and target speed, not a single-match winner.
- PlayMode: formal 6v6 covers normal in-zone pass, displaced-but-reachable setter,
  unreachable setter fallback, and a continuous attack. Phase-1 rule transition,
  contact, and score counters remain unchanged except for legitimate changed play.
- Replay: serialize/render organization target, actual first-pass landing, zone
  grade, setter reachability/movement/fallback reason, attack approach/takeoff/
  planned-contact/actual-deviation/correction, and only the attributes actually
  consumed. The identical seed produces equal diagnostic data.

**Handoff to later stages**

- Stage 4 may use the motion tests as the facade-preservation baseline, but it must
  not loosen the `0.18m` attack correction limit.
- Stage 5 must consume `SetterOrganizationZone` and the same setter-reachability
  policy rather than recreating it in `RallyPlan` or `PhysicalMatchRallyDirector`.
- Stage 9 extends these benchmark/replay fields; it does not replace their tests.

## Stage 2: Ability Projection, Shared Execution, and Physical Prediction

**Depends on:** completed Phase 0/1 contracts, Stage 1.5 compatibility evidence,
and authoritative formal V3 rules.

**Scope**

- Add an explicit `PlayerAbilitySnapshotV3 ->` live V3 execution projection. It records each field as `Active`, `CompatibilityMapped`, or `Reserved`; a formal match must not silently construct a V2 profile from a V3 context.
- Expand `ExecutionEnvelopeV3` from identity-only data into a versioned immutable baseline target/velocity, bounded error distribution, effort, samples, and provenance contract. Planner and executor consume the same envelope instance/serialized identity.
- Implement one deterministic `BallTrajectoryPredictionProviderV3`/cache keyed by ball-state version, physics hash, sample key, and predictor version. Both teams use its artifact for gate-5 samples.
- Define and hash deterministic candidate-class policies, sample counts, envelope expansion limits, and the degradation ladder. Implement `EnvelopeExceeded`, `EnvelopeExpanded`, and `UnexpectedExecutionSample` diagnostics.
- Repair formal contact translation so P6 can query actual takeoff/contact geometry through the existing V3 rule authority.

**Non-goals**

- No 12-player tactical authority, perception uncertainty, V4 attributes, height/standing-reach model, or balance retuning.
- No claim that every V3 field is live merely because the projection serializes it.

**Attribute result**

- `AttackControl` becomes the active technical input for normal power-attack aim/direction/speed error. V2 `AttackTechnique` is mapped explicitly only for compatibility contexts and replay labels show `CompatibilityMapped`.
- `SoftTouch`, `BlockTechnique`, and `CourtAwareness` remain reserved in this stage; their action categories do not yet exist in the authoritative slice.

**Acceptance**

- EditMode: same input/key produces identical envelope, samples, trajectory artifact, execution result, and reason code; different V3 `AttackControl` changes attack error but not power cap; invalid/out-of-envelope samples are classified rather than silently repaired.
- EditMode: both teams receive the exact same trajectory artifact identity for an identical physical sample; attack eligibility tests cover the newly translated geometry facts.
- PlayMode: formal 6v6 shadows envelopes/cache diagnostics without changing score, accepted-contact count, or V3-rule transition count.
- Replay: persist envelope identity/bounds, ability-consumption status, predictor provenance, and actual-sample classification in additive V2 sections. Fixed seed yields byte-stable canonical section data.

## Stage 3: Twelve-Player Responsibility Plan in Shadow

**Depends on:** Stage 2's real envelopes, cache, and geometry facts.

**Scope**

- Add immutable `RallyPlan`, `TeamRallyPlan`, `PlayerResponsibilityAssignment`, spatial claims, conditional task data, and deterministic constrained beam composition.
- Generate exactly six primary responsibilities per current on-court team from `OnCourtEligibilitySnapshot`; candidates exclude off-court players and illegal actions before scoring.
- Build both teams' candidates from the same world snapshot and shared physical artifacts, then record plans, coverage decisions, and legacy-outcome comparisons without issuing movement/contact commands.
- Implement deterministic `PlanCoverageDecision` evaluation for accepted contacts and the approved branch/local/scoped/global/terminal reason codes.

**Non-goals**

- No replacement of director movement, attack choice, receive choice, or rules authority.
- No access to the opponent's hidden final route, future execution sample, or internal readiness beyond the current zero-error observation adapter.

**Acceptance**

- EditMode: six distinct current players per team receive one primary task; composition rejects line-up, libero, back-row, and spatial conflicts; stable ordering/ties produce the same plan revision.
- EditMode: legal candidates cannot be rescued by score; a covered contact activates only a declared branch, while an out-of-envelope contact reports a bounded scoped/global replan reason.
- PlayMode: a complete formal rally records two shadow plans per relevant revision with no changes to legacy tactical command, score, or rule transition.
- Replay: persist plan revision, assignments, claims, conditions, values, shared trajectory references, and coverage decisions as `Shadow` sections.

## Stage 4: PlayerAgent Component Extraction

**Depends on:** Stage 3 interfaces are frozen. It may be implemented alongside Stage 5 only after the facade contract is accepted.

**Scope**

- Split `PrototypePlayerAgent` behind its existing facade into focused locomotion, action-timeline, contact-surface, technique/execution, and presentation/rig components.
- Keep the facade as the only caller-facing bridge until Stage 8 removes director tactical ownership; preserve visible poses, physical contact surfaces, and scheduled-contact semantics.
- Make attack approach/takeoff/contact/landing one speed-bounded root-motion sequence. Restrict attack contact alignment to `0.18m`; unclosed error becomes an execution/contact deviation rather than root teleportation.

**Non-goals**

- No tactical replan policy in a component and no new V4 body model.
- Do not alter the existing bounded alignment policy for receive, set, or controlled handling in this stage.

**Acceptance**

- EditMode: facade compatibility, surface frames, scheduled action/timeline, and ability-dependent movement behavior stay equivalent for representative contacts.
- PlayMode: formal 6v6 and legacy 3v3 complete with the same public bootstrap APIs; an attack's adjacent physical samples respect the root-motion displacement bound and recorded correction never exceeds `0.18m`.
- Replay: report approach start, takeoff, planned contact, actual contact deviation, and applied alignment correction.

## Stage 5: Receive and Organization Authority Slice

**Depends on:** Stage 4 facade; Stage 2 envelope; Phase 1 rules; and the Stage 1.5
organization policy. Stage 3 plan data is consumed first in shadow, then by this
slice.

**Scope**

- Adopt Stage 1.5's `SetterOrganizationZone`, normal-pass target, and registered-setter reachability gate as planner-owned responsibility data; do not add a second coordinate system or fallback ordering.
- Introduce plan executor/replan coordinator for receive, setter preparation, emergency takeover, post-contact coverage decision, and attack preparation. Legacy attack completion remains the temporary downstream writer.

**Non-goals**

- No attack-defense counterplay, soft-attack choice, block-technique consumption, or perception uncertainty.
- No bypass of V3 consecutive-touch, libero, or availability rules to force a setter contact.

**Acceptance**

- EditMode: both teams' organization points/bands mirror correctly; normal receive targets the zone; reachable setter wins even if another player scores higher; unreachable/illegal setter enables the best legal backup; no backup yields existing save/loss behavior.
- EditMode: fixed-seed current-live ability benchmarks prove monotonic `Mobility`, `Reaction`, `ReceiveTechnique`, and `SetTechnique` effects in reachability, delay, receive error, and set error.
- PlayMode: a formal receive-set sequence shows zone arrival, a displaced-but-reachable setter run, and an unreachable-setter takeover; Phase-1 authority counters remain correct.
- Replay: organization target, actual first-pass landing, band, setter reachability/movement, fallback reason, active consumed abilities, and coverage/replan result are serialized and rendered.

## Stage 6: Attack, Defense, and Reorganization Authority Slice

**Depends on:** Stage 5 organization executor, Stage 2 geometry/envelope/cache, and Stage 3 responsibilities.

**Scope**

- Migrate set target, attacker preparation, route evaluation, and final attack selection to the envelope/plan executor.
- Produce a perceived attack threat distribution, compose one joint block-plus-floor response, then choose the attack once. Defense may react only after actual attack contact; it may not inspect a hidden final route.
- Add error-aware power-route eligibility, one fallback pool, and physical block-tool recovery requiring a legal, reachable non-attacking teammate plus reorganization exit.
- Activate `SoftTouch` only for tip/roll/push/high-survival/block-tool controlled rebound envelopes, and `BlockTechnique` only for block hand/coverage/rebound envelopes. Retire their compatibility proxies for these actions.

**Non-goals**

- No CourtAwareness uncertainty yet; use the Stage-7 zero-error perception adapter explicitly.
- No rule exception for a visually successful but illegal/out-of-bounds/non-recoverable block tool.

**Acceptance**

- EditMode: `AttackControl` changes normal power-route error/control at fixed power; `SoftTouch` changes only soft-action/rebound control at fixed power attack; `BlockTechnique` changes only block coverage/deflection control at fixed jump. Each relationship is bounded, monotonic across fixed keys, and independently observable.
- EditMode: illegal/mostly non-crossing power routes fail before score; fallback candidates compete in one pool; tool recovery is rejected without a non-attacker continuation; defense covers residual threat rather than duplicating the block.
- PlayMode: poor set uses a legal survival option instead of a forced net route; block contact creates the correct new touch sequence; a successful tool recovery returns home and reorganizes; committed motions do not teleport.
- Replay: persist candidate classes/values, threat and coverage distributions, selected envelope/action, blocker contact/rebound diagnostics, fallback comparisons, and the consumption status of the three live technical axes.

## Stage 7: Perception and CourtAwareness

**Depends on:** Stage 6's authoritative action categories and replay plan data.

**Scope**

- Replace zero-error perception adapters with deterministic player/team perception views over the Stage-2 authoritative artifact. The physical artifact is never recomputed per observer.
- Model observation latency, bounded uncertainty, confidence, visible-action interpretation, and teammate support estimates; own assignments are accurate while opponent plans/future samples remain hidden.
- Activate `CourtAwareness` only in perception/recognition/support selection. It does not directly boost movement speed, touch mechanics, route legality, or hidden tactical knowledge.

**Non-goals**

- No omniscient opponent route choice, random wall-clock sensing, or V4 derived attributes.

**Acceptance**

- EditMode: high awareness yields lower bounded observation delay/uncertainty and a better supported selection from identical visible facts; neither observer receives the opponent's final sampled route; same seed exactly reproduces the view and decision.
- PlayMode: a delayed-recognition and a normal-recognition rally replan through the same V3 rule authority without changing hidden physical facts.
- Replay: distinguish authoritative artifact, each team's/player's perceived view, confidence/delay, uncertainty key, and the decision it caused; never render hidden values as observed facts.

## Stage 8: Director Slimming and Legacy Writer Removal

**Depends on:** Stage 5--7 authority gates and their replay parity/acceptance evidence.

**Scope**

- Move tactical candidate generation, responsibility selection, movement/action command selection, and replan policy out of `PhysicalMatchRallyDirector` into the V3 planner/executor/coordinator.
- Keep the director limited to physical-event orchestration, lifecycle/rotation, score/result handling, replay dispatch, and feature-boundary setup.
- Remove fixed `Receive -> Organize -> Attack` chaining, legacy tactical selection state, narrow contact filters, and post-block tactical patches only when the new single writer replaces each responsibility.

**Non-goals**

- No API break for formal bootstrap or an unreviewed cleanup of legacy 3v3.

**Acceptance**

- EditMode/reflection: director exposes no tactical candidate/scoring/route-selection responsibility; no duplicate active tactical writer remains.
- PlayMode: standard, setter-first-contact, emergency organization, poor-set fallback, block rebound, and tool-recovery rallies all complete with correct rules/score/replay.
- Replay: every command references a plan revision and an envelope; stale callbacks cannot reactivate old plan state.

## Stage 9: Replay Completion, Second Perspective, and Calibration

**Depends on:** Stage 8's sole authoritative tactical path. Additive telemetry from earlier stages is an input, not a second implementation.

**Scope**

- Complete `MatchReplayV2` payload and canonical replay/frame hash inclusion/exclusion tables. Add a second-perspective overlay for plan, perception, trajectory, execution, responsibility, threat/coverage, organization, and actual outcome.
- Add fixed-seed benchmark runner/fixtures and bounded performance telemetry. Record deterministic work counters/degradation mode separately from wall-clock profiler measurements.
- Calibrate only coefficients already consumed by V3; publish fixed seeds, profiles, configuration hashes, and expected deltas.

**Non-goals**

- Do not introduce V4 fields, rebalance by one match score, or use runtime timing to change deterministic decisions.

**Acceptance**

- EditMode: canonical replay/context/result hashes are stable for fixed inputs; inclusion/exclusion tests prove profiler/cache-hit data is excluded while behavior-affecting config and persisted diagnostics are included.
- Fixed-seed matrix: each of the four independently live V3 axes changes only its stated envelope/perception behavior with a reproducible, explainable delta. Current active-axis fixtures from Stage 5 remain green.
- PlayMode: two replay runs with the same context/seed produce equal hashes and rendered diagnostic content; scenario matrix meets plan-stability/work-budget thresholds. Report wall-clock budget separately.
- Manual: inspect formal-6v6 overlay from both team perspectives and verify a user can trace `ability -> perception/plan -> envelope -> actual result` without seeing a non-live field as active.

## V3 Ability Activation Contract

| Axis | First live authority stage | Permitted behavior | Explicitly not allowed |
| --- | --- | --- | --- |
| `AttackControl` | Stage 2 | Normal power-attack target direction, velocity, and error envelope | Power cap, reach, or soft-touch quality bonus |
| `SoftTouch` | Stage 6 | Tip, roll, push, high survival, controlled rebound/tool envelope | Normal power-attack aim or block hand coverage |
| `BlockTechnique` | Stage 6 | Hand coverage/seal and controlled block-deflection envelope | Block reach/vertical jump or floor-defense bonus |
| `CourtAwareness` | Stage 7 | Observation delay/uncertainty, visible-action interpretation, support choice | Omniscience, route legality, movement speed, or touch mechanics |

Before its listed stage, every axis is marked `Reserved` in match/replay diagnostics.
For migrated V2 contexts, the stage that first uses an axis labels its source
`CompatibilityMapped`; an authored V3 input labels it `Active`. No UI or benchmark
may describe a reserved or compatibility estimate as an independently authored live
balance value.

## Execution Order and Checkpoints

1. [ ] Implement Stage 1.5 first. Record its visible runtime/replay baseline and run focused EditMode plus formal-6v6 PlayMode under Phase-1 V3-rules authority.
2. [ ] Implement and review Stage 2. Update the existing change record with envelope/cache/geometry status and run focused EditMode plus formal-6v6 shadow PlayMode.
3. [ ] Implement Stage 3 in read-only shadow mode. Compare coverage/legality diagnostics against completed formal rallies before freezing its interfaces.
4. [ ] Extract Stage 4 components and prove facade compatibility before modifying organization authority.
5. [ ] Implement Stage 5 as the first V3-plan authority vertical slice. Re-run the Stage 1.5 organization gates against the plan executor and close `V3_MIGRATION_COMPATIBILITY_WINDOW` only after its save/loader test is added and passing.
6. [ ] Implement Stage 6, then Stage 7. Each newly active ability requires an independent fixed-seed fixture and contact-level replay proof before progressing.
7. [ ] Remove duplicate tactical writers in Stage 8 only after all affected Stage 5--7 scenarios pass under the new authority path.
8. [ ] Complete Stage 9 replay/hash/calibration, record Unity version and test totals, then mark V3 complete in the change record.

For every checkpoint, run focused NUnit tests while developing, then the affected
formal-6v6 PlayMode fixtures. Before declaring a stage complete, run the full
EditMode and PlayMode suites using the commands in `docs/development.md`, preserve
the local XML/log evidence, and record exact totals and Unity version in the change
record. Do not commit `TestResults/`.
