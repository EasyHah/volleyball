# Task 10 Report: Native Canonical MatchReplayV4

## Outcome

Implemented a native, immutable `MatchReplayV4` contract and replaced the
formal replay path's legacy V1 projection with strict V4 recording and HTML
diagnostics.

The V4 replay segment now stores:

- the complete native `MatchContextV4`, including base attributes, dominant
  hand, derivation versions, and input/result fingerprints;
- ordered event sequence numbers and scores;
- the selected execution-envelope identity, derived fingerprint, source
  intent, complete target/velocity/effort bounds, sampling contract,
  expansion state, and deterministic policy identity;
- runtime-read-only derived-field consumption evidence, sorted by the frozen
  26-field V4 derivation order;
- trajectory artifact/provider provenance and every field of the cache key,
  including the degradation step;
- the actual execution sample, classification, expansion identity, and
  offending diagnostic dimensions;
- observed attack takeoff/contact geometry and computed P6 flags for attack
  events only;
- the independently versioned V3 rule decision and reason code.

## Shared boundary

Created:

- `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- `Assets/Volleyball/Shared/Runtime/ReplayExecutionEnvelopeRecordV4.cs`
- `Assets/Volleyball/Shared/Runtime/ReplayTrajectoryArtifactRecordV4.cs`
- `Assets/Volleyball/Shared/Runtime/ReplayAbilityConsumptionRecordV4.cs`

The new Shared DTO files reference only `System` and
`Volleyball.Shared.Contracts`; they do not reference Match, Domain, Unity, or
Presentation assemblies.

`MatchReplayV4.Create` sorts events by sequence, rejects duplicate/gapped
sequences, and validates each event against its embedded V4 context. Every
event requires envelope, trajectory, actual-sample classification,
runtime-consumption, and V3 rule evidence. Attack events require observed P6
geometry; non-attack events reject it.

## Canonical JSON and hash

Added:

```csharp
ContractJson.SerializeV4(MatchReplayV4 value)
ContractJson.DeserializeMatchReplayV4(string json)
```

Serialization uses fixed field order, invariant round-trip floats, semantic
array order, UTF-8, and a separate
`volleyball.match-replay.v4` SHA-256 hash family. The lowercase replay digest
is stored as `replayHash`.

Strict deserialization rejects:

- replay versions 1, 2, and 3;
- unknown or missing fields at every schema level;
- missing mandatory evidence records;
- duplicate or gapped event sequences;
- unsupported derived-field consumption claims;
- mismatched actor/derivation/context or predictor/physics provenance;
- inconsistent P6 diagnostic flags;
- a tampered replay hash.

## Formal recorder and HTML

`MatchReplayRecorder` now accepts only an initialized formal V4 director
running V3 rules authority. It reads the accepted actor's exact scheduled
execution envelope and classification rather than the director's mutable
global “last planned” values.

`ReplayContactEvent` was minimally extended in
`PhysicalMatchRallyDirector.cs` to dispatch the exact authority
`AttackGeometryFactV3` and `RuleTransitionV3`. This was necessary to avoid
reconstructing observed geometry or inferring a rule result in the recorder.

The HTML writer accepts only `MatchReplayV4`. It labels contract version 4
and rules version 3 separately and renders envelope, trajectory,
classification, runtime-consumption, P6, and rule diagnostics. The legacy
V1/V2-reserved presentation is gone from the formal output.

## TDD evidence

Initial RED:

- `/tmp/task10-red.xml`
- `/tmp/task10-red.log`
- expected compiler failures only: the new V4 replay/record types did not
  exist.

First focused GREEN:

- `/tmp/task10-green-1.xml`
- `8/8` ReplayV4 EditMode tests passed.

Formal integration RED:

- `/tmp/task10-formal-play-1.xml`
- `2/3` passed;
- the failing first-rally capture proved the global last-envelope value had
  been overwritten by later planning before the earlier actor contacted the
  ball.

Formal integration correction:

- recorder now reads the accepted actor's scheduled envelope and
  classification;
- `/tmp/task10-formal-play-2.xml`: `3/3` passed.

Fresh verification before commit is recorded below.

## Verification

- focused Shared contract + ReplayV4 EditMode: `66/66` passed,
  `/tmp/task10-final-focused-editmode.xml`
- full EditMode: `544/544` passed,
  `/tmp/task10-final-full-editmode.xml`
- formal ReplayV4 PlayMode, including first-rally native V4
  serialize/deserialize: `3/3` passed,
  `/tmp/task10-final-replay-playmode.xml`
- `git diff --check`: clean
- Shared dependency scan for Match/Domain/Unity references: no matches

The V1-named test source file remains at its historical path for plan and
Unity asset continuity, but its fixture is now
`Volleyball.EditModeTests.MatchReplayV4Tests`.

## External-review corrections

The original implementation was subsequently reviewed at the runtime
authority boundary. Four Important findings were corrected; this section
supersedes the earlier descriptions of runtime-read evidence, selected
envelope semantics, trajectory capture, and byte-stability coverage.

### Exact consumption evidence

`MatchReplayRecorder` no longer reads `PrototypePlayerAgent.Ability` and no
longer creates generic `RuntimeRead` records while serializing.

`ExecutionEnvelopeFactoryV4` now emits immutable
`ExecutionAbilityConsumptionV4` records at the exact derived-field reads that
produce the execution envelope. Each record identifies the genuinely consumed
field, exact value, and `ExecutionEnvelopeFactoryRead` evidence kind. The
records are owned by the tested envelope, preserved across explicit expansion,
snapshotted with the accepted classification, and only then mapped into Shared
replay DTOs.

The category-specific evidence is:

- Receive: `Receive.FirstTouchControl`, `Receive.Movement`
- Set: `Set.PlacementControl`, `Set.TempoControl`, `Set.Movement`
- Attack: `Attack.DirectionControl`, `Attack.SpeedControl`,
  `Attack.PowerCapacity`
- Block: `Block.HandControl`, `Block.Timing`, `Block.LateralMobility`
- Serve: `Serve.DirectionControl`, `Serve.SpeedControl`,
  `Serve.PowerCapacity`

### Event-owned trajectory provenance

The recorder no longer reads
`PhysicalMatchRallyDirector.LastTrajectoryPredictionArtifactV4`.
`ScheduleDecision` passes the exact trajectory artifact used by that action
into `PrototypePlayerAgent`; an accepted contact snapshots it before any
receive/set/attack follow-up planning occurs. `ReplayContactEvent` carries that
event-owned artifact to the recorder. Preplanned attacks retain the artifact
that produced their originating decision instead of consulting mutable global
state after contact.

### Truthful expansion identity

Every serialized event now contains explicit, complete `testedEnvelope` and
`executableEnvelope` records. Validation requires:

- classification and actual sample to identify the tested envelope;
- a non-expanded classification to execute the same identity and expansion
  count;
- an expanded classification to identify a distinct executable envelope whose
  expansion count is exactly the tested count plus one;
- both records to share the same derivation, source intent, sampling contract,
  and expansion policy.

The HTML diagnostics render both identities and the tested-to-executable
expansion transition.

`RecorderMapping_ExpandedContactPersistsTestedAndExecutableEnvelopes` creates a
real expansion-authorized envelope, classifies an out-of-base/in-expanded-bound
sample, supplies an actual V4 trajectory artifact and accepted V3 transition,
and passes the resulting contact through the recorder mapper. It verifies the
two distinct identities and exact factory-read evidence.

### Independent fixed-seed stability

`Capture_TwoIndependentFixedSeedFormalRunsAreByteStable` loads the formal scene
twice, independently attaches and starts a recorder for each run, completes the
first rally in each fresh match, and compares the canonical UTF-8 replay bytes.
It also explicitly compares ordered event numbers, tested/executable envelope
identities, trajectory artifact/cache-key provenance, classification kinds,
and every ordered consumption field/value/evidence record.

### Correction verification

- focused Shared contract + ReplayV4 EditMode: `67/67` passed,
  `/tmp/task10-review-final-focused.xml`
- full EditMode: `546/546` passed,
  `/tmp/task10-review-final2-full-editmode.xml`
- formal ReplayV4 PlayMode, including two independent fixed-seed scene runs:
  `4/4` passed, `/tmp/task10-review-final2-replay-playmode.xml`
- `git diff --check`: clean
- Shared dependency scan for Match/Domain/Unity references: no matches
