# Full Rally V3 Architecture Design

**Date:** 2026-07-23

**Status:** User-approved design

**Scope:** Six-player rally decision, rules, execution, attack-defense counterplay, recovery, and staged migration

## 1. Context

The current rally implementation has grown around a sequential `Receive -> Organize -> Attack` flow. `PhysicalMatchRallyDirector` now owns rally lifecycle, AI requests, actor selection, movement scheduling, set and attack trajectories, blocking, contact windows, rules, replay, metrics, and presentation coordination. `TeamRallyDecisionPlanner` selects one actor and one action at a time, while the director adds support players through special-case methods.

This design creates four connected problems:

1. Other players do not receive a coherent team responsibility plan after every contact.
2. Real rallies that deviate from the three-stage script require special flags and fallback paths.
3. Planning evaluates ideal trajectories while execution may apply a different error model afterward.
4. Block contacts and unplanned physical contacts do not naturally create a new coordinated rally state.

The immediate symptom was repeated attacks into the net. The cause was not blocker selection: route selection and attack execution used mismatched assumptions. The desired solution has since expanded into a full V3 rally architecture in which both teams re-evaluate responsibilities after every actual contact.

## 2. Design Principles

1. **Facts, plans, execution, and rules are separate.**
2. **Every actual player contact triggers a whole-court replan for both teams.**
3. **A team decision is a compatible six-player responsibility plan, not a single actor selection.**
4. **Only currently legal on-court players may receive responsibilities.**
5. **Planning and execution share the same ability-driven execution distribution.**
6. **Attack legality is evaluated before scoring threat or block avoidance.**
7. **Defense responds to an attack threat distribution, not the attacker's hidden final choice.**
8. **The physical result, not the planned action, advances the rules state.**
9. **Planning uses perceived state, never an opponent's hidden plan or future random sample.**
10. **Migration proceeds in runnable vertical slices with one authoritative writer per subsystem.**

## 3. Goals

- Replan responsibilities for all twelve on-court players after every actual contact.
- Support standard receive-set-attack chains and non-standard recovery chains through the same architecture.
- Coordinate blocking and backcourt defense as a joint coverage plan.
- Compare safe attacks and deliberate block-tool recovery by expected rally value.
- Preserve legal three-touch sequences after a block contact.
- Incorporate player abilities into feasibility, perception, execution error, and recovery quality.
- Maintain deterministic fixed-seed simulation and replay.
- Reduce `PhysicalMatchRallyDirector` to event orchestration.
- Keep every migration phase testable and playable.

## 4. Non-goals

- Tactical substitutions selected by the rally planner. V3 consumes the current legal lineup; match-level substitution strategy remains outside the first implementation.
- A large named-trait or perk system. V3 initially differentiates players through ability values.
- Per-frame global replanning. Contacts always trigger replanning; flight deviations use bounded thresholds.
- Unlimited offense-defense iteration or game-theoretic equilibrium solving.
- Full verbal communication simulation in the first version.
- Scripted or teleported block-tool rebounds. All rebounds remain physical.

## 5. Current Architecture Assessment

### 5.1 Components to retain

- Ball integration, trajectory prediction, net-plane interception, and environment collision.
- `RallyTouchState` rule concepts, rewritten behind a V3 event-based rules engine.
- Deterministic seeded execution-error generation.
- Pure geometric helpers such as set targeting, attack route generation, and block interception.
- Physical player contact surfaces and collision reporting.
- Replay events, fixed-seed diagnostics, and calibration tests.

### 5.2 Components to split

- `PhysicalMatchRallyDirector` becomes an event orchestrator.
- `TeamRallyDecisionPlanner` is replaced by full-team candidate composition.
- `PrototypePlayerAgent` is split into movement, action, contact, technique, runtime identity, and presentation components.
- `PhysicalRallyTacticPlanner` remains a formation/default-tactic provider, not a live decision owner.

### 5.3 Legacy behavior to remove after migration

- Fixed `Receive -> Organize -> Attack` stage chaining.
- `_plannedAttackDecision` as a cross-contact single-line plan.
- Director-owned selection of setter, blocker, cover player, route, and set target.
- A contact window that makes non-planned physical contacts disappear.
- Post-block possession booleans used instead of a correct contact sequence model.
- Independent planning and execution error calculations.

## 6. Authoritative Data Layers

### 6.1 `RallyWorldSnapshot`

An immutable authoritative snapshot containing:

- ball position, velocity, spin, and physical time;
- twelve on-court players' positions, velocities, facing, pose, action commitment, and recovery;
- current lineup, rotations, front/back-row eligibility, and libero replacement state;
- actual contact sequence and remaining legal team hits;
- court, net, boundary, and match configuration;
- latest accepted physical and rule events.

Only physics, rules, snapshot generation, replay, and diagnostics may read authoritative facts directly.

### 6.2 `PlayerPerceptionSnapshot`

Each player receives estimates of visible facts:

- ball trajectory and contact timing;
- opponent position, movement, readiness, and visible action cues;
- teammate position and apparent support readiness;
- possible set, attack, block, and recovery outcomes;
- confidence, uncertainty, observation time, and information sources.

### 6.3 `TeamPerceptionSnapshot`

Team perception combines relevant observations using:

```text
personal observation
× CourtAwareness
× sight quality
× responsibility relevance
× information freshness
→ team estimate
```

It must not expose:

- the opponent's final selected route;
- the opponent's `TeamRallyPlan`;
- hidden conditional tasks;
- the actual future execution sample;
- decisions that are not yet physically observable;
- precise opponent internal readiness or ability values without an observation model.

Own-team assignments are shared accurately, but future execution results and teammates' actual recoverability remain uncertain.

### 6.4 Perceived values

Uncertain estimates use a common representation:

```text
PerceivedValue<T>
- Estimate
- Uncertainty
- Confidence
- ObservedAt
- SourcePlayers
```

All perception uncertainty is deterministic from match seed, plan revision, observer, subject, observation kind, and event sequence.

## 7. On-Court Eligibility

`OnCourtEligibilitySnapshot` is created before candidate generation. It contains exactly six players per team and records:

- player identity and registered position;
- rotation position;
- front/back-row status;
- libero identity and replaced player;
- serve order and current server;
- legal attack and block eligibility;
- any restriction caused by a libero's front-zone overhead set.

Hard constraints:

- off-court players receive no candidate or responsibility;
- a libero and the player currently replaced by that libero cannot coexist in a plan;
- libero and back-row block restrictions are enforced;
- back-row and libero attack restrictions are evaluated from actual takeoff/contact geometry;
- each team plan contains exactly one primary responsibility for each of the six current players;
- role labels never create virtual players.

Lineup and substitution decisions remain match-layer responsibilities during the first V3 rollout.

## 8. V3 Ability System

V3 uses eleven abilities:

| Ability | Responsibility |
|---|---|
| Mobility | movement, approach, coverage, braking, transition |
| Reaction | response delay to new observations and plans |
| Jump | jump formation, airtime, and block execution |
| MaxAttackReach | maximum reachable attack contact height |
| ReceiveTechnique | reception, digging, and recovery control |
| SetTechnique | normal and emergency organization control |
| AttackControl | power-attack aim, direction, and velocity control |
| AttackPower | maximum output and effort required for a target speed |
| SoftTouch | tip, roll, push, unloading, and controlled rebound |
| BlockTechnique | hand shape, coverage area, and block rebound quality |
| CourtAwareness | observation latency and uncertainty about space, intent, and support |

Ability layers have distinct meanings:

- physical abilities affect whether a responsibility is feasible;
- technical abilities affect the distribution of execution results;
- `CourtAwareness` affects perceived inputs and recognition delay.

Abilities do not directly add arbitrary bonuses to a route's total score.

### 8.1 V2 compatibility

Legacy V2 data maps initially as:

```text
AttackControl = AttackTechnique
SoftTouch = AttackTechnique
BlockTechnique = average(Jump, ReceiveTechnique)
CourtAwareness = Reaction
```

Other fields map directly. These defaults preserve old saves and are not the final roster-balance values. Newly-authored rosters provide explicit V3 values.

### 8.2 Effort

Contact actions select an effort level rather than always using maximum output:

- lower effort reduces speed and error;
- medium effort balances pressure and control;
- near-maximum effort increases error nonlinearly.

A powerful player can create a given ball speed at lower relative effort, providing a natural control advantage without making power itself an accuracy bonus.

## 9. Rules Engine

### 9.1 Rule facts versus plans

The planner may query:

```text
rules.CanAttempt(candidate, snapshot)
```

Only actual events may advance state:

```text
rules.Apply(actualContactEvent)
```

Planning an action never consumes a hit or changes possession.

### 9.2 Contact classification

V3 classifies physical events as:

- `ServeContact`;
- `TeamContact`;
- `BlockContact`;
- `SimultaneousTeamContact`;
- `EnvironmentContact`.

All legal or accidental team contacts count unless classified as a legal block contact. Counting does not depend on whether the intended technique was receive, set, or attack.

### 9.3 Touch sequence

`TouchSequenceState` contains:

```text
- LastLegalPhysicalContactTeam
- CurrentCountedSequenceTeam
- CountedHits
- LastCountedActor
- LastContactClassification
- LastContactGroup
- RemainingHits
```

A legal block contact:

- breaks the opponent's previous consecutive contact sequence;
- consumes no team hit for the blocker;
- permits the blocker to make the team's first counted contact afterward;
- starts a new three-hit sequence for whichever team next makes a counted contact.

Thus an attack blocked back to the attacker side gives that side a new legal sequence, while a blocked ball retained by the blocking team leaves that team three hits.

### 9.4 Rule modules

- `OnCourtLineupRules`
- `ContactRules`
- `AttackEligibilityRules`
- `BlockEligibilityRules`
- `BoundaryAndNetRules`

Rules include lineup count, rotation eligibility, libero replacement, consecutive contacts, four hits, simultaneous contacts, back-row attack, libero attack and set restrictions, block eligibility, net crossing, antennas, boundaries, and future net/center-line extensions.

### 9.5 Planned versus actual contacts

Physical contact surfaces exist from actual pose and body geometry. A contact is categorized as:

- planned;
- approved conditional backup;
- incidental/unplanned.

All three produce physical events. Planned contacts use the selected execution profile, backups use approved fallback profiles, and incidental contacts use a low-control profile. The rules engine then accepts or rejects the actual event.

## 10. Situation Interpretation

`RallySituationInterpreter` converts facts into opportunities without imposing a fixed rally stage. It determines:

- the team most likely to make the next contact;
- remaining team hits;
- receive, organization, attack, block, recovery, or emergency opportunities;
- whether an attack may be formed now or after another contact;
- whether a block rebound may create a new sequence;
- whether the situation only supports a survival ball.

Situation labels guide candidate generation but do not select actors.

## 11. Action Candidates

### 11.1 Contact candidates

- reception and dig;
- overhead set, back set, jump set, bump set, emergency set;
- power attack by line and target;
- tip, roll, push, and high survival ball;
- block-out and deliberate block-tool recovery;
- block contact.

### 11.2 Off-ball candidates

- yield/leave;
- setter preparation;
- attack approach;
- decoy approach;
- attack cover;
- line, cross, deep, and tip defense;
- block close;
- abandon block and retreat;
- defensive-to-offensive transition.

### 11.3 Candidate contract

```text
PlayerActionCandidate
- Actor
- Responsibility
- ActionKind
- Technique
- StartState
- MovementPath
- ContactWindow
- ContactGeometry
- TargetOrRoute
- Effort
- Preconditions
- RuleEligibility
- ExecutionEnvelope
- PredictedOutcomes
- FollowUpRequirements
```

Responsibility describes why a player acts; technique describes how.

## 12. Ability Outcome Prediction

Each candidate is evaluated through six ordered gates:

1. **Rule eligibility** — illegal actions are removed.
2. **Arrival feasibility** — reaction, movement, path conflict, recovery, and available time produce an arrival margin.
3. **Contact geometry** — reach, jump, facing, approach, body-relative contact, and posture produce readiness and controllability.
4. **Execution distribution** — ability, difficulty, posture, pressure, and effort produce position, direction, speed, spin, and timing error.
5. **Physical samples** — a small deterministic set of center, lateral, vertical, and speed samples runs through the official trajectory and collision predictors.
6. **Next-state value** — samples are evaluated for score, legal continuation, organization quality, opponent counterattack, or immediate loss.

The error scale follows:

```text
base technique difficulty
× arrival penalty
× contact posture penalty
× pressure penalty
× ability modifier
× effort modifier
```

### 12.1 Prediction-execution contract

The selected candidate carries its `ExecutionEnvelope`. Planning evaluates representative samples from that envelope; actual execution samples a concrete result from the same envelope using a deterministic seed.

No director or execution component may replace the baseline target, velocity, or error model after selection.

## 13. Team Responsibility Composition

### 13.1 Assignment

```text
PlayerResponsibilityAssignment
- PlayerId
- PrimaryResponsibility
- ConditionalResponsibilities
- MovementIntent
- ActionIntent
- SpatialClaim
- TimeWindow
- Priority
- ActivationCondition
- CancellationCondition
- ExpectedOutcome
```

Each on-court player has exactly one primary responsibility and may have bounded conditional responsibilities.

### 13.2 Conditions

Conditions are serializable data, not arbitrary callbacks:

- `BallEntersRegion`;
- `PrimaryActorUnavailable`;
- `ArrivalMarginBelow`;
- `BlockContactByTeam`;
- `ReboundTowardRegion`;
- `ActualContactByPlayer`;
- `TrajectoryDeviationExceeded`;
- `TouchesRemainingEquals`;
- `PlanRevisionMatches`.

Conditional tasks use exclusivity groups, priority, claim tokens, activation deadlines, and cancellation conditions to prevent multiple players from taking the same backup responsibility.

### 13.3 Spatial claims

Responsibilities declare path corridors, target areas, takeoff/contact space, and landing/recovery space. Relationships are:

- hard conflicts;
- soft conflicts with quality penalties;
- cooperative fits such as adjacent blockers.

### 13.4 Composition algorithm

V3 uses deterministic constrained beam search:

1. identify required responsibilities;
2. retain a bounded set of feasible player candidates per responsibility;
3. expand partial team plans;
4. reject illegal and hard-conflicting combinations immediately;
5. keep a fixed beam width with deterministic ordering and tie-breaking;
6. score complete plans by expected rally state.

Pure greedy assignment is insufficient, while exhaustive enumeration is too expensive for six-player candidate sets.

### 13.5 Plan scoring

```text
TeamPlanValue =
    probability of current-contact success
  × probability of organized continuation
  × next attack or counterattack value
  + cooperative coverage
  - uncovered-space risk
  - responsibility conflict
  - immediate-loss risk
```

Weak critical links constrain the whole chain. A strong first contact cannot hide an unreachable setter or absent protection.

## 14. Attack-Defense Counterplay

V3 uses a bounded threat-response-choice process.

### 14.1 Attack threat distribution

The attacking side removes illegal, unreachable, and predominantly non-crossing routes, then publishes a perceived threat distribution over:

- power routes;
- safe attacks;
- block-out attempts;
- block-tool recovery.

The highest-success qualified lane is the easiest attack lane and the center of the defense's block response.

### 14.2 Joint defense plan

The defense composes blocking and floor coverage together:

- primary and supporting blockers;
- line, cross, deep, and tip responsibilities;
- block-shadow and rebound coverage;
- post-dig organization and counterattack exits.

Defense value is the threat-weighted save or continuation probability minus overlap, empty zones, and illegal assignments. Backcourt coverage should emphasize routes left open by the block rather than duplicate covered space.

### 14.3 Final attack choice

The attacker evaluates qualified power routes against the formed defense. If a reliable power route exists, the best qualified power route is selected.

If no power route meets the reliability gate, the following candidates share one fallback pool:

- tip;
- roll;
- push;
- high survival ball;
- block-out;
- block-tool recovery.

Safe attacks and block-tool recovery compete directly by expected rally value; neither category has a fixed ordering.

### 14.4 Block-tool recovery validity

A recovery candidate is valid only when its sampled outcomes sufficiently support:

1. legal contact with a predicted blocker hand or arm;
2. rebound to the attacking side;
3. a reachable non-attacking teammate before the next floor contact;
4. positive control margin, playable height, and playable time;
5. a legal remaining hit and a plausible reorganization exit.

A block collision without a playable home continuation is a failure.

The value is:

```text
toolRecoveryValue =
    blockContactProbability
  × homeReboundProbability
  × teammateReachProbability
  × continuationQuality
  - immediateLossRisk
```

Safe-attack value is:

```text
safeValue =
    legalCrossProbability
  × opponentCourtOrContactProbability
  × continuationOrWinValue
  - immediateLossRisk
```

### 14.5 No defensive clairvoyance

The process runs once:

```text
attack threat distribution
→ joint defense response
→ final attack choice
→ attack-cover adjustment
```

The defense does not read the hidden final route and reposition again. It may react after the actual attack contact, subject to reaction, commitment, and movement limits.

## 15. Route Legality and Net Safety

Route selection applies expected execution error before accepting a route.

For every candidate, deterministic samples record:

- net-plane crossing and clearance;
- in-bounds first landing;
- predicted blocker contact;
- post-block trajectory;
- next reachable player and arrival margin.

Power candidates require configured legal-crossing and opponent-court probabilities. Scoring or block-clearance rewards cannot compensate for failing the legality gate.

If no normal attack is reliable, fallback selection still prefers legal survival over restoring a high-threat route predicted to hit the net.

## 16. Full Rally Plan

```text
RallyPlan
- Revision
- SourceSnapshotVersion
- TriggerEvent
- CreatedAtSimulationTime
- ValidUntil
- BlueTeamPlan
- OrangeTeamPlan
- CounterplayPlan
- InvalidationConditions
- Diagnostics
```

Each `TeamRallyPlan` includes:

- team and lineup version;
- exactly six assignments;
- threat or coverage map;
- expected next contacts;
- team-plan value.

Each player assignment includes:

- one primary task;
- bounded conditional tasks;
- current commitment;
- expected follow-up.

Tasks carry movement, action, spatial claim, timing, activation, cancellation, exclusivity, priority, and execution profile.

## 17. Replanning

### 17.1 Mandatory whole-court replans

- serve contact;
- any actual legal or accidental player contact;
- block contact;
- lineup or rotation change;
- rally-ending ground, boundary, or fault event.

### 17.2 Flight-time replans

- trajectory position, velocity, or arrival-time deviation exceeds tolerance;
- primary actor becomes unreachable;
- net-plane crossing changes the likely next-contact team;
- blocker unexpectedly commits, retreats, or loses availability;
- collision, fall, or recovery delay invalidates responsibility.

Flight-time checks are thresholded and do not run the global planner every frame.

### 17.3 Commitment states

Tasks progress through:

```text
Planned -> Active -> Committed -> Recovery
```

- planned tasks may be replaced freely;
- active tasks pay braking and redirection costs;
- committed actions permit only limited physical correction;
- recovery tasks cannot immediately accept incompatible actions.

Every command carries a plan revision. Stale callbacks cannot reactivate an old plan.

### 17.4 Conditional safety

Executors may activate only planner-approved conditional tasks. Uncovered situations request a replan rather than allowing local tactical AI. Existing active motion may continue while a new plan is computed.

## 18. Execution Architecture

### 18.1 `RallyPlanExecutor`

Responsibilities:

1. validate plan revision and lineup;
2. diff new and old player tasks;
3. dispatch commands to twelve players;
4. evaluate approved conditions and claim tokens;
5. report failures, incidental contacts, and deviations.

It cannot select routes, actors, blockers, setters, or error parameters.

### 18.2 Player component split

`PrototypePlayerAgent` becomes a compatibility facade over:

#### `PlayerRuntime`

- identity;
- eligibility;
- V3 ability profile;
- active plan revision;
- component aggregation and runtime status.

#### `PlayerLocomotionMotor`

- movement paths;
- acceleration, braking, and redirection;
- collision avoidance;
- arrival estimates and actual shortfall.

#### `PlayerActionMotor`

- technique timelines;
- approach, jump, swing, contact, landing, and recovery;
- commitment state;
- bounded timing, facing, and hand-shape correction.

#### `PlayerContactSurfaceController`

- current physical palm, forearm, block-arm, and incidental body surfaces;
- surface positions and velocities derived from actual pose;
- classification of planned, conditional, or incidental contact.

#### `PlayerTechniqueExecutor`

- selected `ExecutionEnvelope`;
- fixed-seed actual sample;
- final correction from actual arrival and readiness;
- outgoing contact response.

#### `PlayerPresentationView`

- rig pose, facing, color, number, debug display, and feedback;
- no authority over rules, targets, or outgoing ball state.

### 18.3 Contact pipeline

```text
actual pose exposes physical surfaces
→ ball collision occurs
→ contact intent is classified
→ technique response is resolved
→ rules engine adjudicates
→ ActualContactEvent is emitted
→ whole-court replan occurs
```

## 19. Director End State

`PhysicalMatchRallyDirector` is reduced to:

```text
receive physical event
→ apply rule transition
→ create authoritative snapshot
→ create team perceptions
→ request V3 rally plan
→ apply plan
→ record replay and diagnostics
→ resolve rally and match lifecycle
```

It no longer selects players, set targets, attack routes, blockers, cover players, or skill error.

## 20. Observability and Second Perspective

Diagnostics record:

- plan and snapshot revisions;
- trigger event and invalidation reason;
- selected responsibilities for all twelve players;
- attack threat probabilities;
- block and floor-defense coverage;
- legal-crossing and net-clearance probabilities;
- safe-attack and block-tool values;
- predicted block surface, rebound point, recovery actor, and arrival margin;
- perceived versus authoritative positions and trajectories;
- actual result and deviation from prediction.

Visual overlays may show:

- white authoritative trajectory;
- blue attack-team perceived trajectory and uncertainty;
- orange defense-team perceived trajectory and uncertainty;
- solid actual responsibility regions;
- translucent perceived coverage;
- route values and selected reasoning.

This supports screenshots that explain why each side made its decision rather than merely showing the final collision.

## 21. Migration Plan

### Phase 0: baseline and interfaces

- freeze fixed-seed rally, contact, route, replay, and failure baselines;
- introduce V3 interfaces backed by legacy adapters;
- preserve one authoritative writer per subsystem.

### Phase 1: facts, eligibility, and rules

- add world and on-court snapshots;
- run V3 rules in shadow mode;
- compare old and new transitions, classifying known block-sequence and incidental-contact differences as intentional V3 corrections rather than requiring false parity;
- switch V3 rules to authority after parity and new block-sequence tests.

### Phase 2: abilities and shared execution distribution

- add snapshot/profile V3 and V2 migration;
- add execution envelopes and outcome prediction;
- make current planning and execution share the envelope.

### Phase 3: shadow full-team planning

- generate candidates and compose six-player plans for both teams;
- record but do not execute;
- compare responsibility coverage and legality with legacy outcomes.

### Phase 4: player component extraction

- extract locomotion, action, contact, technique, and presentation behind the existing facade;
- preserve legacy call compatibility and PlayMode behavior.

### Phase 5: receive and organization vertical slice

- add plan executor and replan coordinator;
- migrate serve receive, emergency takeover, setter preparation, post-contact replan, and attack preparation;
- retain legacy attack completion temporarily.

### Phase 6: attack, defense, and reorganization vertical slice

- migrate set target and attacker choice;
- add threat distribution and joint block-floor defense;
- add error-aware power routes and shared fallback competition;
- add physical block-tool recovery and post-block reorganization.

### Phase 7: perception and awareness

- replace zero-error perception adapters with player and team perception;
- hide opponent plans and future samples;
- enable deterministic observation uncertainty and opponent estimates.

### Phase 8: director slimming and legacy removal

- remove fixed stage chaining, legacy decision state, director tactical selection, narrow contact filtering, and post-block patches;
- keep only event orchestration, match lifecycle, replay, and result handling.

### Phase 9: visualization and calibration

- add second-perspective overlays;
- calibrate across ability profiles and fixed-seed batches;
- measure plan stability, deterministic replay, and runtime cost.

## 22. Test Strategy

### 22.1 Pure EditMode

- exact six-player assignments from current lineup;
- libero replacement and action restrictions;
- back-row attack and block restrictions;
- block contact starts correct new hit sequence;
- blocker may make the first counted contact;
- deterministic perception and execution distributions;
- illegal candidates cannot be rescued by score bonuses;
- constrained composition rejects spatial conflicts;
- safe attack and block-tool recovery compete directly;
- recovery is rejected without a non-attacker arrival;
- stale plan revisions cannot activate.

### 22.2 Simulation integration

- prediction and actual execution use the same baseline and error envelope;
- fixed poor-contact attack does not repeatedly select a net-fault route;
- block centers on the easiest qualified attack lane;
- backcourt covers residual threat rather than duplicating block coverage;
- unplanned contacts advance rules and trigger replan;
- all twelve players receive refreshed responsibilities after each contact.

### 22.3 PlayMode

- standard receive-set-attack rally;
- setter takes first contact and another player organizes;
- poor set produces safe fallback rather than forced net fault;
- physical tool recovery touches a block, returns home, is recovered, and reorganizes;
- block rebound to either side yields the correct three-hit sequence;
- committed players do not teleport or cancel jumps on replanning;
- lineup, rotation, replay, and match scoring remain functional.

### 22.4 Calibration and invariants

- fixed-seed replay hash stability;
- no off-court responsibility;
- exactly six primary responsibilities per team;
- no illegal fourth hit or consecutive contact;
- no hidden opponent final-route access;
- bounded plan churn under small flight deviations;
- recorded planner runtime and allocation budgets.

## 23. Rollout and Failure Containment

- Feature switches exist only at subsystem and vertical-slice boundaries.
- Shadow systems are read-only and never advance authoritative state.
- A slice switches authority only after its invariants and PlayMode scenarios pass.
- Compatibility adapters are removed after all consumers migrate; two permanent sources of truth are prohibited.
- Existing geometric attack and block work remains regression input and may be adapted behind V3 interfaces rather than discarded.

## 24. Success Criteria

V3 is complete when:

1. every actual contact produces a rule transition; every non-rally-ending accepted player contact produces a whole-court plan revision;
2. both teams' current six players always have compatible responsibilities;
3. attack, block, floor defense, cover, and reorganization share one outcome model;
4. route prediction and physical execution share one execution envelope;
5. safe attacks and block-tool recovery compete by expected rally value;
6. block contacts create correct new touch sequences;
7. player ability and awareness produce reproducible, explainable differences;
8. the director no longer contains tactical selection logic;
9. fixed-seed replays remain deterministic;
10. diagnostics can explain both teams' perceived choices and the actual outcome.
