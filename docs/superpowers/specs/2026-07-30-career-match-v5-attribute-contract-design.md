# Career x Match V5 Attribute and Report Contract Design

**Status:** Approved for V5-A implementation on 2026-07-31

## 1. Scope and Decisions

V5 hard-cuts the test version from Career's former eight attributes to twelve
Career-owned base attributes. V4 pending matches, saved match data, and replays
are rejected rather than migrated. Recovery is to discard the old pending match
and create a new V5 match.

V5 is delivered in two milestones:

1. **V5-A:** Career's twelve attributes, `MatchContextV5`, `MatchResultV5`,
   `MatchReplayV5`, Shared derivation, and formal physical 6v6 consumption.
2. **V5-B:** `CareerMatchReportV1`, physical replay aggregation, Career
   settlement consumption, then quick simulation and `QuickSimulationTraceV1`.

V5-A does not support quick simulation. Fixture runners remain test-only and
must not produce a formal growth report. Injury facts and injury outcomes are
deferred to a later `CareerMatchReportV2` milestone.

## 2. Ownership and Flow

```text
Career player and state
  -> frozen V5 effective attributes
  -> Shared canonical derivation and MatchContextV5
  -> Match formal 6v6 consumes derived values
  -> MatchResultV5 and MatchReplayV5
  -> CareerMatchReportV1 (V5-B)
  -> Career calculates growth, fatigue, mindset, and trust
```

The dependency direction is `Career -> Shared <- Match`. Shared owns every
cross-assembly DTO, canonical serialization, hashes, and validation. Match
reports objective facts and never calculates Career consequences. Career
calculates consequences and never rescans evidence to reclassify Match facts.

## 3. Career Base Attributes

All values are Career-authoritative. Eleven attributes use integer basis points
in `[0,10000]`; height is integer millimeters in `[1400,2300]`. Invalid values
are rejected, not silently clamped.

| Group | Attribute | Growth | Formal Match consumption |
| --- | --- | --- | --- |
| Physical | Strength | Training and qualifying facts | Attack power, serve speed, block suppression |
| Physical | Height | Creation only; future age growth is versioned | Attack/block geometry, high-ball set reach |
| Physical | Jump | Training and jump count | Attack/block height and air window |
| Physical | Movement | Training, movement/rally facts | Acceleration, coverage, approach reach |
| Physical | Reaction | Training, receive/defense/block facts | Response delay, action windows, block timing |
| Physical | Coordination | Training, controlled-contact facts | Moving-contact stability and chained error bounds |
| Technical | Attack | Training and attack facts | Attack direction, control, handling |
| Technical | Defense | Training and defense facts | Dig and recovery control |
| Technical | Court IQ | Tactical training and decision facts | Tactical selection, anticipation, emergency choice |
| Technical | Block | Training and block facts | Hand control and line sealing |
| Technical | Serve | Training and serve facts | Serve control and error stability |
| Technical | Set | Training and set facts | Placement, tempo, emergency setting |

Height never grows through normal training or matches. Position and cultivation
direction are Career metadata: they select initial allocations, training weights,
and tactical tendencies, but cannot overwrite frozen base values or provide a
hidden Match bonus. Dominant hand remains a creation-time identity field; it
affects contact side, serve geometry, one-hand emergency sets, and replay
explanations, but is not an ability axis. Body mass and arm span are absent.

Initial trials remain three phases: attack determines Attack/Strength/Jump;
receive-defense determines Defense/Reaction/Movement; full scrimmage determines
Court IQ/Block/Serve/Set/Height/Coordination.

## 4. Derivation and Runtime Consumption

Career applies fatigue exactly once before context freezing. Raw fatigue does
not enter Match and Match must not apply another fatigue multiplier. Shared uses
a pure derivation function:

```text
effective bases + height + dominant hand + formula version
+ coefficient version + frozen configuration -> DerivedMatchAttributesV5
```

Derived normalized values use basis points; contact geometry uses millimeters.
Formula and coefficient versions enter canonical context and replay identity.
Match consumes only derived attributes: no base rereads, position templates, or
hidden sources are permitted.

| Group | Main sources | Consumers |
| --- | --- | --- |
| Attack | Attack, Strength, Height, Jump, Movement, Coordination, Court IQ | Approach, contact, velocity, direction |
| Block | Block, Height, Jump, Movement, Reaction, Strength, Coordination, Court IQ | Candidate, jump timing, hand/contact resolution |
| Defense | Defense, Movement, Reaction, Coordination, Court IQ | Candidate, movement, dig execution |
| Receive | Defense, Movement, Reaction, Coordination, Court IQ | Candidate and first-contact execution |
| Set | Set, Court IQ, Coordination, Reaction, Movement, Height | Target, tempo, emergency setting |
| Serve | Serve, Strength, Coordination, Court IQ | Target, velocity, execution error |

## 5. V5 Contract Surface

| Contract | Milestone | Binding |
| --- | --- | --- |
| `MatchContextV5` | V5-A | Canonical V5 teams, bases, derived data, versions, seed, configuration; `contextHash` |
| `MatchResultV5` | V5-A | Binds `sessionId + contextHash`; has `resultHash` |
| `MatchReplayV5` | V5-A | Binds V5 context; V5 action and attribute evidence; `replayHash` |
| `CareerMatchReportV1` | V5-B | Binds session/context/result and evidence; `reportHash` |
| `QuickSimulationTraceV1` | V5-B | Quick-simulation evidence bound to V5 match; own canonical hash |

`MatchResultV5` is required because result validation requires the same contract
version as its context, even if score fields resemble V4. No optional fields,
default-value shims, extension bags, `Reserved` fields, or V4/V5 adapters are
allowed.

## 6. CareerMatchReportV1

`CareerMatchReportV1` is a `Volleyball.Shared.Contracts` DTO. Match produces it
after a completed V5 result; Career validates and consumes it as the detailed
settlement input. It has exactly twelve player reports in frozen context order,
including both teams.

Top-level fields are:

```text
reportVersion, sessionId, contextHash, resultHash,
evidenceKind (PhysicalReplay | QuickSimulationTrace), evidenceHash,
playerReports[12], reportHash
```

Unknown evidence types, noncanonical hashes, binding mismatches, duplicate or
missing players, and players outside the context reject the whole report and
the whole settlement. There is no partial settlement or default filling.

| Category | Per-player fields | Invariant |
| --- | --- | --- |
| Attack | attempts, points, errors | points + errors <= attempts |
| Serve | attempts, aces, errors | aces + errors <= attempts |
| Receive | attempts, perfect, positive, neutral, negative, errors | five buckets equal attempts |
| Defense | attempts, successes | successes <= attempts |
| Block | attempts, effective touches, points | points <= touches <= attempts |
| Set | attempts, successes, errors | successes + errors <= attempts |
| Load | rallies, movement millimeters, jumps, workload basis points, workload formula version | workload is `[0,10000]` |
| Stability | critical actions/successes/errors, streak episodes, longest streak | successes + errors <= actions |
| Decision | quality successes, quality errors | only eligible tactical branches |

All counters are non-negative and fields are present even when zero. Receive
quality is an objective Match classification; it is the only V1 quality bucket.
No rating, experience, fatigue delta, trust delta, mindset delta, or injury
conclusion is reported. Match calculates objective workload; Career applies its
own versioned fatigue rule. Active duration, high-load jumps, and landing load
are excluded from V1, so V1 cannot settle injuries.

A critical action occurs when a rally starts with either team at most two points
from that set's winning score. A direct player-caused cross-rally error can form
a streak, which any successful action by that player ends. Decision quality is
counted only with at least two legal and executable choices; evidence contains
the choices, selected choice, and reason. Physical success does not itself imply
a correct decision, nor failure an incorrect decision.

## 7. Evidence

Physical formal 6v6 reports use `PhysicalReplay` and the `MatchReplayV5` hash.
Replay records V5 attribute explanation plus facts sufficient to justify report
classifications. Report aggregation is deterministic, but Career verifies its
binding rather than recalculating it.

V5-B quick simulation uses `QuickSimulationTrace`. The trace retains only
report-recomputable per-rally facts: sequence, action, responsible player,
result classification, critical status, decision evidence, and workload
contribution. It excludes internal AI state and random draws. Fixed context,
configuration, and seed must reproduce trace and report bytes.

## 8. Settlement Boundary

Career calculates eleven-attribute growth, fatigue, mindset, and coach trust
from a verified report using its own versioned rules. Match cannot calculate or
write these consequences; Career cannot change score, fact classifications,
evidence, or hashes. Injury effects wait for V2 evidence.

## 9. Acceptance Evidence

### V5-A

- Each of the twelve bases has a Career source, declared derived path, real
  formal-6v6 consumer, and replay explanation.
- Per-attribute low/high single-variable vectors prove declared monotonicity,
  legal boundaries, finite output, and consumer change without random drift.
- Invalid bases/heights/version pairs and supplied-derived mismatches reject.
- Identical context, configuration, and seed produce byte-identical result and
  replay artifacts; golden vectors freeze high/low and mixed-team cases.
- No Match base reread, position override, or hidden ability source remains.

### V5-B

- Every completed physical V5 match yields one valid twelve-player report bound
  to its result and replay.
- Report and cross-report invariants reject malformed inputs before settlement.
- Golden aggregation vectors cover all action families, receive buckets,
  critical actions, decision classifications, workload boundaries, and zeroes.
- Fixed physical seeds reproduce report bytes; fixed quick seeds reproduce trace
  and report bytes.
- Career settles growth/fatigue/mindset/trust without regenerating Match facts.

## 10. Non-goals

- Multi-set play, substitutions, libero substitution, overseas leagues, online
  play, position-specific numerical abilities, or cultivation numerical values.
- Injury settlement and injury-specific workload facts.
- Fixture-generated formal growth reports.
- V4 reads, migration, or compatibility shims.
