# Full Rally V4 Gates A–E Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hard-cut Career, Match, execution, prediction, and replay onto one deterministic V4 attribute contract while retaining the existing V3 rules engine as an independently versioned authority.

**Architecture:** Shared owns immutable V4 input, derived-attribute, match, and replay DTOs plus canonical serialization. Career persists only V4 base attributes. Match derives frozen V4 match attributes at its boundary, shares one execution-envelope identity between planning and execution, caches prediction artifacts by every behavior-affecting input, and supplies observed P6 geometry to the V3 rules query. Replay records the resulting V4 identities and classifications without referencing Match-domain types.

**Tech Stack:** Unity 6000.0.43f1, C#/.NET Standard Unity assemblies, Newtonsoft.Json, NUnit EditMode and PlayMode tests.

## Global Constraints

- This is a hard cut. Do not add V1/V2/V3 save, Career, Match, or replay compatibility readers, adapters, fallback constructors, or silent defaults.
- `PlayerSnapshotV4`, `TeamSnapshotV4`, `MatchContextV4`, `MatchResultV4`, and `MatchReplayV4` are the only accepted production contracts after Gate E.
- `FullRallyRulesV3` remains the authoritative rules engine. Attribute-contract version and rules version must stay separate in names, hashes, replay fields, and diagnostics.
- Every public V4 constructor validates required identifiers, finite numeric values, declared ranges, collection cardinality/order, and enum membership. Invalid data throws `ArgumentException` or `ArgumentOutOfRangeException`; it is never clamped or upgraded.
- Canonical JSON uses invariant culture, explicit property order, stable enum text, no incidental whitespace, and ordinal ordering for dictionaries/sets before serialization.
- A formula or coefficient change increments its own version and changes the derived fingerprint. Adding or changing an authoritative base field requires V5.
- Runtime code must not read raw base attributes after `MatchAttributeDerivationV4.Derive`. Planning, execution, and replay consume the same immutable derived snapshot.
- Unity `.meta` files created for new assets are committed with their source files.
- Run Unity tests without `-quit`; Unity 6000 can exit before writing results when `-quit` is combined with batch test execution.

---

## Task 1: Freeze the Current Defects as Failing Tests

**Files:**

- Modify: `Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RuntimeAdapterTests.cs`
- Modify: `Assets/Volleyball/Career/Tests/EditMode/ModuleBoundaryTests.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`
- Modify: `docs/rules.md`
- Create: `docs/changes/2026-07-24-full-rally-v4-gates-a-e.md`
- Modify: `docs/changes/README.md`

- [ ] **Step 1: Add envelope identity and classification regression tests**

Add tests proving that all behavior-affecting fields participate in identity and that invalid samples are classified, not repaired:

```csharp
[Test]
public void EnvelopeIdentity_ChangesWhenEffortOrMaximumEffortChanges()
{
    ExecutionEnvelopeV3 baseline = Stage2Fixtures.Envelope(effort: 0.5f, maximumEffort: 0.8f);
    ExecutionEnvelopeV3 changedEffort = Stage2Fixtures.Envelope(effort: 0.6f, maximumEffort: 0.8f);
    ExecutionEnvelopeV3 changedMaximum = Stage2Fixtures.Envelope(effort: 0.5f, maximumEffort: 0.9f);

    Assert.That(changedEffort.Identity, Is.Not.EqualTo(baseline.Identity));
    Assert.That(changedMaximum.Identity, Is.Not.EqualTo(baseline.Identity));
}

[Test]
public void ClassifySample_DoesNotClampVelocityBackIntoEnvelope()
{
    ExecutionEnvelopeV3 envelope = Stage2Fixtures.Envelope();
    ExecutionSampleV3 sample = Stage2Fixtures.Sample(
        velocity: envelope.MaximumVelocity + new Vector3(0.01f, 0f, 0f));

    ExecutionSampleClassificationV3 result = envelope.Classify(sample);

    Assert.That(result.Code, Is.EqualTo(ExecutionDiagnosticCodeV3.EnvelopeExceeded));
    Assert.That(result.AcceptedSample, Is.Null);
}
```

- [ ] **Step 2: Add trajectory cache-key completeness tests**

For otherwise identical requests, vary predictor version, predictor configuration hash, and degradation step independently. Assert a cache miss and a different artifact identity for each variation. Add a same-key test asserting reference-equivalent or identity-equivalent cached artifacts.

- [ ] **Step 3: Add a failing P6 geometry integration test**

Construct a V3 attack eligibility query where all non-geometry facts pass but actual contact is below the legal attack threshold. Invoke `FullRallyV3RulesRuntimeAdapter`; assert the rules engine receives the actual geometry and rejects with the existing geometry reason code.

- [ ] **Step 4: Replace compatibility-friendly boundary assertions**

Change `ModuleBoundaryTests` and `MatchContractTests` so they require concrete V4 types and reject broad `IMatchContext`/`IMatchResult` production entry points. Add reflection assertions that production Career and Match assemblies do not expose constructors or methods accepting `PlayerAbilitySnapshotV1`, `PlayerAbilitySnapshotV2`, `PlayerAbilitySnapshotV3`, `MatchContextV2`, or `MatchContextV3`.

- [ ] **Step 5: Register the authoritative behavior change**

Update the applicable attack/contact rule IDs in `docs/rules.md` before implementation. Create the change record from `docs/changes/TEMPLATE.md`, mark it `跨模块（重点）`, link this plan and the consolidated design, list the Career/Shared/Match/replay boundary changes, and link it from `docs/changes/README.md`.

- [ ] **Step 6: Run the focused tests and capture the expected red state**

Run:

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/wys/Documents/program/volleyball-match \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.Stage2AbilityProjectionTests;Volleyball.EditModeTests.Stage2ExecutionEnvelopeTests;Volleyball.EditModeTests.Stage2TrajectoryPredictionProviderTests;Volleyball.EditModeTests.Stage2AttackGeometryFactTests;Volleyball.EditModeTests.FullRallyV3RuntimeAdapterTests;Volleyball.Career.EditModeTests.ModuleBoundaryTests;Volleyball.Shared.EditModeTests.MatchContractTests" \
  -testResults /tmp/volleyball-v4-gate-a-red.xml \
  -logFile /tmp/volleyball-v4-gate-a-red.log
```

Expected: the new identity, cache-key, P6 geometry, and V4-only boundary tests fail for the current implementation. Existing tests must continue compiling.

- [ ] **Step 7: Commit the executable baseline**

```bash
git add Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RuntimeAdapterTests.cs \
  Assets/Volleyball/Career/Tests/EditMode/ModuleBoundaryTests.cs \
  Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs \
  docs/rules.md docs/changes/2026-07-24-full-rally-v4-gates-a-e.md docs/changes/README.md
git commit -m "test: freeze full rally v4 migration gaps"
```

---

## Task 2: Add Immutable V4 Base Attribute Contracts

**Files:**

- Create: `Assets/Volleyball/Shared/Runtime/DominantHandV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/PhysicalBaseAttributesV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/TechnicalBaseAttributesV4.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/ContractPrimitives.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Write constructor validation tests**

Cover every field, all enum values, `NaN`, infinities, values immediately below/above the allowed range, and both valid hands. Use these frozen ranges:

| Field | Inclusive range |
|---|---:|
| `HeightMeters` | 1.40–2.30 |
| `StandingReachMeters` | 1.70–3.10 |
| `Jump`, `Mobility`, `Reaction`, `Coordination` | 0–1 |
| Every technical attribute | 0–1 |

Also assert `StandingReachMeters >= HeightMeters`; implausible combinations fail rather than being corrected.

- [ ] **Step 2: Implement the V4 types**

Use immutable get-only properties and complete value equality:

```csharp
public enum DominantHandV4
{
    Left = 0,
    Right = 1
}

public sealed class PhysicalBaseAttributesV4 : IEquatable<PhysicalBaseAttributesV4>
{
    public float HeightMeters { get; }
    public float StandingReachMeters { get; }
    public float Jump { get; }
    public float Mobility { get; }
    public float Reaction { get; }
    public float Coordination { get; }
}

public sealed class TechnicalBaseAttributesV4 : IEquatable<TechnicalBaseAttributesV4>
{
    public float AttackTechnique { get; }
    public float AttackPower { get; }
    public float BlockTechnique { get; }
    public float DefenseTechnique { get; }
    public float ReceiveTechnique { get; }
    public float SetTechnique { get; }
    public float ServeTechnique { get; }
    public float SoftTouch { get; }
    public float CourtAwareness { get; }
}
```

Add `ContractVersions.MatchV4 = 4` and `ContractVersions.ReplayV4 = 4`; do not change the V3 rules constant.

- [ ] **Step 3: Run Shared tests**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/wys/Documents/program/volleyball-match \
  -runTests -testPlatform EditMode \
  -testFilter Volleyball.Shared.EditModeTests.MatchContractTests \
  -testResults /tmp/volleyball-v4-base.xml \
  -logFile /tmp/volleyball-v4-base.log
```

Expected: all Shared contract tests pass.

- [ ] **Step 4: Commit**

```bash
git add Assets/Volleyball/Shared/Runtime Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: add immutable v4 base attributes"
```

---

## Task 3: Implement Versioned V4 Match-Attribute Derivation

**Files:**

- Create: `Assets/Volleyball/Shared/Runtime/MatchAttributesV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchAttributeDerivationConfigV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchAttributeExplanationV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchAttributeDerivationV4.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Write determinism, sensitivity, and authority tests**

Tests must prove:

- identical base input and config produce byte-identical canonical result and fingerprints;
- each base attribute changes at least one documented derived output;
- `AttackPower` changes attack speed/power capacity but not attack direction error;
- `AttackTechnique` changes attack direction/speed error but not power capacity;
- `SoftTouch`, `BlockTechnique`, and `CourtAwareness` are present in V4 and affect their declared V4 groups;
- left/right hand changes the handedness value and derived fingerprint;
- formula/coefficient version changes alter the result fingerprint even when numeric outputs happen to match;
- invalid outputs throw and are never clamped.

`HeightMeters` is a structural input rather than a direct V1 formula term. It
constrains and influences authored physical attributes, especially
`StandingReachMeters`, and participates in input validation and the canonical
input fingerprint. The frozen V1 formulas use `StandingReachMeters` directly
for contact geometry and must not add `HeightMeters` again, which would
double-count that geometry. Tests therefore prove that height changes input
identity and remains validated, while every formula-participating base field
changes at least one declared numeric output.

- [ ] **Step 2: Define the frozen six-group output**

```csharp
public sealed class MatchAttributesV4 : IEquatable<MatchAttributesV4>
{
    public AttackAttributesV4 Attack { get; }
    public BlockAttributesV4 Block { get; }
    public DefenseAttributesV4 Defense { get; }
    public ReceiveAttributesV4 Receive { get; }
    public SetAttributesV4 Set { get; }
    public ServeAttributesV4 Serve { get; }
    public DominantHandV4 DominantHand { get; }
}
```

Each group contains only normalized `[0,1]` ratings plus explicitly named meter/second quantities where geometry needs physical units. Freeze these outputs:

- Attack: `DirectionControl`, `SpeedControl`, `PowerCapacity`, `ContactHeightMeters`, `ApproachMobility`
- Block: `Timing`, `HandControl`, `ReachHeightMeters`, `LateralMobility`
- Defense: `Reaction`, `PlatformControl`, `CoverageMobility`, `Awareness`
- Receive: `FirstTouchControl`, `Reaction`, `Movement`, `Awareness`
- Set: `PlacementControl`, `TempoControl`, `SoftTouch`, `Movement`, `Awareness`
- Serve: `DirectionControl`, `SpeedControl`, `PowerCapacity`, `Consistency`

- [ ] **Step 3: Freeze formula and coefficient version 1**

Use pure weighted sums for normalized ratings. Weights in each formula sum to `1.0`; validate rather than renormalize:

```text
Attack.DirectionControl = .65 AttackTechnique + .20 Coordination + .15 CourtAwareness
Attack.SpeedControl     = .55 AttackTechnique + .25 Coordination + .20 SoftTouch
Attack.PowerCapacity   = .70 AttackPower + .20 Jump + .10 Coordination
Attack.ApproachMobility= .70 Mobility + .30 Coordination
Attack.ContactHeightM  = StandingReachMeters + (.25 + .60 Jump)

Block.Timing            = .50 BlockTechnique + .30 Reaction + .20 CourtAwareness
Block.HandControl       = .65 BlockTechnique + .25 Coordination + .10 SoftTouch
Block.LateralMobility   = .70 Mobility + .30 Reaction
Block.ReachHeightM      = StandingReachMeters + (.20 + .55 Jump)

Defense.Reaction        = .70 Reaction + .30 CourtAwareness
Defense.PlatformControl = .65 DefenseTechnique + .25 Coordination + .10 SoftTouch
Defense.CoverageMobility= .70 Mobility + .20 Reaction + .10 CourtAwareness
Defense.Awareness       = CourtAwareness

Receive.FirstTouchControl = .65 ReceiveTechnique + .20 Coordination + .15 SoftTouch
Receive.Reaction          = .70 Reaction + .30 CourtAwareness
Receive.Movement          = .70 Mobility + .30 Coordination
Receive.Awareness         = CourtAwareness

Set.PlacementControl = .55 SetTechnique + .25 Coordination + .20 CourtAwareness
Set.TempoControl     = .50 SetTechnique + .30 Reaction + .20 CourtAwareness
Set.SoftTouch        = .60 SoftTouch + .25 SetTechnique + .15 Coordination
Set.Movement         = .70 Mobility + .30 Coordination
Set.Awareness        = CourtAwareness

Serve.DirectionControl = .65 ServeTechnique + .20 Coordination + .15 CourtAwareness
Serve.SpeedControl     = .55 ServeTechnique + .25 Coordination + .20 SoftTouch
Serve.PowerCapacity   = .60 AttackPower + .25 ServeTechnique + .15 Coordination
Serve.Consistency     = .60 ServeTechnique + .25 Coordination + .15 Reaction
```

`MatchAttributeDerivationConfigV4` exposes `FormulaVersion = 1`, `CoefficientVersion = 1`, and the immutable coefficient set. A config with missing/duplicate coefficients, non-finite values, or non-unit formula weights fails construction.

- [ ] **Step 4: Produce explanations and fingerprints**

`DerivedMatchAttributesV4` contains:

```csharp
public MatchAttributesV4 Attributes { get; }
public int FormulaVersion { get; }
public int CoefficientVersion { get; }
public string InputFingerprint { get; }
public string ResultFingerprint { get; }
public IReadOnlyList<MatchAttributeExplanationV4> Explanations { get; }
```

Emit explanations in fixed group/field order. Each explanation records the output name, input names, exact coefficients, and result. Hash canonical UTF-8 bytes with SHA-256 and lowercase hex.

- [ ] **Step 5: Run tests and commit**

Run the Shared test command from Task 2. Expected: all pass.

```bash
git add Assets/Volleyball/Shared/Runtime Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: derive deterministic v4 match attributes"
```

---

## Task 4: Add Native V4 Match Contracts and Canonical JSON

**Files:**

- Create: `Assets/Volleyball/Shared/Runtime/PlayerSnapshotV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/TeamSnapshotV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchContextV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchResultV4.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/ContractJson.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Write round-trip and byte-stability tests**

Use fixed fixtures and assert:

- `SerializeV4` output is byte-identical across 100 repetitions;
- deserialize/serialize preserves the exact bytes;
- team player order is explicit and stable;
- changing one base attribute, hand, derivation version, seed, or rules version changes the appropriate fingerprint;
- V4 deserializers reject missing fields, extra legacy ability blocks, wrong versions, and V1–V3 JSON.

- [ ] **Step 2: Implement concrete V4 contracts**

`PlayerSnapshotV4` owns identity, physical base attributes, technical base attributes, dominant hand, and the derived snapshot. Its constructor recomputes derivation from the supplied base/config and rejects a supplied derived fingerprint mismatch.

`TeamSnapshotV4` requires exactly six unique players in explicit rotation order. `MatchContextV4` requires two different teams, deterministic seed, physics configuration hash, derivation versions, and independent `RulesVersion = 3`. `MatchResultV4` records the context identity, winner, set/rally summary, accepted contacts, and V3 rule-transition count.

- [ ] **Step 3: Implement strict V4 JSON entry points**

Expose concrete methods rather than generic fallback parsing:

```csharp
public static string SerializeV4(MatchContextV4 value);
public static MatchContextV4 DeserializeMatchContextV4(string json);
public static string SerializeV4(MatchResultV4 value);
public static MatchResultV4 DeserializeMatchResultV4(string json);
```

Do not route V4 through `UpgradeFromV2`, `IMatchContext`, or `IMatchResult`.

- [ ] **Step 4: Run Shared tests and commit**

Run the Shared test command from Task 2. Expected: all pass.

```bash
git add Assets/Volleyball/Shared/Runtime Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: add native v4 match contracts"
```

---

## Task 5: Hard-Cut Career to V4

**Files:**

- Modify: `Assets/Volleyball/Career/Runtime/Domain/CareerPlayerRecord.cs`
- Modify: `Assets/Volleyball/Career/Runtime/Application/CareerMatchRequest.cs`
- Modify: `Assets/Volleyball/Career/Tests/EditMode/ModuleBoundaryTests.cs`

- [ ] **Step 1: Write failing V4-only Career tests**

Assert that a Career player record exposes V4 physical/technical attributes and dominant hand, and that `CareerMatchRequest.Context`/result callback use concrete `MatchContextV4`/`MatchResultV4`. Reflection tests must find no public legacy ability or broad match-interface entry point.

- [ ] **Step 2: Replace the Career model**

Use this production boundary:

```csharp
public sealed class CareerPlayerRecord
{
    public string PlayerId { get; }
    public PhysicalBaseAttributesV4 Physical { get; }
    public TechnicalBaseAttributesV4 Technical { get; }
    public DominantHandV4 DominantHand { get; }
}

public sealed class CareerMatchRequest
{
    public MatchContextV4 Context { get; }
    public Action<MatchResultV4> Complete { get; }
}
```

Delete legacy constructor overloads rather than marking them obsolete. Update fixtures at compile errors; do not write converters.

- [ ] **Step 3: Run Career tests**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/wys/Documents/program/volleyball-match \
  -runTests -testPlatform EditMode \
  -assemblyNames Volleyball.Career.EditModeTests \
  -testResults /tmp/volleyball-v4-career.xml \
  -logFile /tmp/volleyball-v4-career.log
```

Expected: all Career EditMode tests pass.

- [ ] **Step 4: Commit**

```bash
git add Assets/Volleyball/Career
git commit -m "refactor: hard cut career contracts to v4"
```

---

## Task 6: Hard-Cut Match Domain and Formal Bootstrap to V4

**Files:**

- Modify: `Assets/Volleyball/Match/Runtime/Domain/Players/PlayerAbilityProfile.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/Players/MatchPlayerBinding.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/MatchSet.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/MatchSetTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`

- [ ] **Step 1: Convert Match tests to V4 fixtures**

Replace V1/V2/V3 ability fixtures with one canonical V4 fixture builder. Assert that a binding exposes `DerivedMatchAttributesV4` and never raw legacy ability values. Add an end-to-end test that formal bootstrap accepts `MatchContextV4` and produces `MatchResultV4`.

- [ ] **Step 2: Replace runtime ability profiles and bindings**

`PlayerAbilityProfile` becomes a thin immutable view over `DerivedMatchAttributesV4`. `MatchPlayerBinding` accepts a `PlayerSnapshotV4`, verifies its derived fingerprint, then exposes only identity, rotation/side, dominant hand, and derived match attributes.

- [ ] **Step 3: Replace MatchSet contracts**

`MatchSet` constructor accepts only `MatchContextV4`; its completion method returns only `MatchResultV4`. Remove the V1/V2 context branches and result factories.

- [ ] **Step 4: Replace formal initialization**

Rename `InitializeV2` to `InitializeV4`, remove `MatchContextV3.UpgradeFromV2`, and pass the V4 context unchanged from bootstrap to director. Keep the rules adapter explicitly named V3 and initialize it from `context.RulesVersion`; reject any value other than `3`.

- [ ] **Step 5: Route attack authority correctly**

Replace runtime reads of V2 `AttackTechnique` with:

- `Attack.DirectionControl` for direction-error distribution;
- `Attack.SpeedControl` for speed-error distribution;
- `Attack.PowerCapacity` for the upper speed/effort limit;
- `Attack.ContactHeightMeters` for planned geometric capability only, never as observed contact geometry.

Add sensitivity assertions proving control changes error but not power capacity and power changes capacity but not direction error.

- [ ] **Step 6: Run Match EditMode tests**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/wys/Documents/program/volleyball-match \
  -runTests -testPlatform EditMode \
  -assemblyNames Volleyball.Match.EditModeTests \
  -testResults /tmp/volleyball-v4-match-domain.xml \
  -logFile /tmp/volleyball-v4-match-domain.log
```

Expected: Match EditMode suite passes; no production compile references remain to legacy match initialization.

- [ ] **Step 7: Commit**

```bash
git add Assets/Volleyball/Match
git commit -m "refactor: hard cut formal match runtime to v4"
```

---

## Task 7: Replace Stage 2 Projection with Shared V4 Execution Envelopes

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionEnvelopeV4.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionEnvelopeFactoryV4.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionEnvelopePolicyV4.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionSampleV4.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionSampleClassificationV4.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs`

- [ ] **Step 1: Rewrite Stage 2 tests around V4 semantics**

Require:

- same derived snapshot, intent, policy, and sampling key produce equal envelope identity and canonical bytes;
- planner and executor receive the same `ExecutionEnvelopeV4` instance;
- every boundary, effort value, error distribution, sampling policy, source identity, and version affects identity;
- `Attack.DirectionControl`/`SpeedControl` change error bounds without changing maximum speed;
- `Attack.PowerCapacity` changes maximum speed without changing error bounds;
- non-finite samples return `UnexpectedExecutionSample`;
- finite out-of-envelope samples return `EnvelopeExceeded`;
- only an explicit policy step may produce `EnvelopeExpanded`, with old/new identities recorded;
- no classification returns a clamped or repaired sample.

- [ ] **Step 2: Define the immutable envelope**

```csharp
public sealed class ExecutionEnvelopeV4 : IEquatable<ExecutionEnvelopeV4>
{
    public int Version { get; }
    public string Identity { get; }
    public string DerivedAttributesFingerprint { get; }
    public string SourceIntentIdentity { get; }
    public Vector3 BaselineTarget { get; }
    public Vector3 BaselineVelocity { get; }
    public Vector3 MaximumVelocity { get; }
    public BoundedErrorDistributionV4 TargetError { get; }
    public BoundedErrorDistributionV4 VelocityError { get; }
    public float RequestedEffort { get; }
    public float MaximumEffort { get; }
    public SamplingContractV4 Sampling { get; }
    public EnvelopeExpansionPolicyV4 Expansion { get; }
}
```

Define deterministic candidate-category order, sample count, maximum expansion count, per-step expansion factor, and degradation ladder in `ExecutionEnvelopePolicyV4`. Hash its canonical bytes into every envelope.

- [ ] **Step 3: Implement one factory and one object flow**

`ExecutionEnvelopeFactoryV4.Create(derivedAttributes, intent, samplingKey, policy)` is the only construction path. The planner returns the constructed envelope with the selected candidate. The executor accepts that exact object; it must not reconstruct an envelope from attributes or intent.

- [ ] **Step 4: Implement explicit classification**

Classification order is fixed:

1. non-finite/malformed → `UnexpectedExecutionSample`;
2. within current envelope → `Accepted`;
3. outside current but within the next explicitly allowed expansion → `EnvelopeExpanded`;
4. otherwise → `EnvelopeExceeded`.

Record the offending dimensions and tested envelope identity. Never mutate the sample.

- [ ] **Step 5: Run focused tests and commit**

Run Task 1’s focused command, expecting envelope tests to pass. Then:

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs
git commit -m "feat: share deterministic v4 execution envelopes"
```

---

## Task 8: Add Complete V4 Trajectory Prediction Identity

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BallTrajectoryPredictionRequestV4.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BallTrajectoryPredictionArtifactV4.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BallTrajectoryPredictionProviderV4.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BallTrajectoryPredictionCacheKeyV4.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs`

- [ ] **Step 1: Specify the complete key in tests**

The key contains:

```csharp
public readonly struct BallTrajectoryPredictionCacheKeyV4
{
    public long BallStateVersion { get; }
    public string BallStateFingerprint { get; }
    public string PhysicsConfigurationHash { get; }
    public string SamplingKey { get; }
    public int PredictorVersion { get; }
    public string PredictorConfigurationHash { get; }
    public string EnvelopeIdentity { get; }
    public int DegradationStep { get; }
}
```

Vary every field independently and assert a cache miss/different artifact identity. Assert home and away requests with the exact same key receive the exact same artifact identity and canonical bytes.

- [ ] **Step 2: Implement deterministic provider and bounded cache**

The provider is pure with respect to the request. The cache uses full-key equality, has a deterministic capacity/eviction policy configured by match context, and does not include requesting team in its key. Artifacts record key identity, predictor source/version/configuration, sample timestamps/positions, and artifact identity.

- [ ] **Step 3: Route Gate 5 sampling through the provider**

Both teams request the provider’s artifact before candidate comparison. Remove local/team-specific trajectory recomputation from that path. On provider failure, follow the hashed degradation ladder; record the step in the next key.

- [ ] **Step 4: Run focused tests and commit**

Run Task 1’s focused command. Expected: all Stage 2 tests pass.

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs
git commit -m "feat: cache deterministic v4 trajectory artifacts"
```

---

## Task 9: Feed Observed P6 Geometry into V3 Rules Authority

**Files:**

- Modify: `Assets/Volleyball/Match/Runtime/Presentation/FullRallyV3RulesRuntimeAdapter.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RuntimeAdapterTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [ ] **Step 1: Define the observed geometry boundary**

At actual contact, construct `AttackGeometryFactV3` from observed contact position, player takeoff position/time, player contact point, ball contact point, net plane, and front/back-row state. Planned contact height and derived capability must not populate observed fields.

- [ ] **Step 2: Pass the fact through the adapter**

Change the attack-authority call to require `AttackGeometryFactV3`. The adapter passes it unchanged to the existing V3 rules query and returns the exact V3 decision/reason code.

- [ ] **Step 3: Cover legal and illegal conversions**

Tests must include:

- back-row player actually taking off behind the line and contacting above the net;
- back-row player actually taking off in front of the line;
- contact below/above the relevant height threshold;
- planned legal geometry followed by observed illegal geometry;
- planned illegal geometry followed by observed legal geometry.

Only observed geometry controls the V3 transition.

- [ ] **Step 4: Run focused tests and commit**

Run Task 1’s focused command. Expected: adapter and eligibility suites pass.

```bash
git add Assets/Volleyball/Match/Runtime/Presentation \
  Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RuntimeAdapterTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "fix: authorize p6 attacks from observed geometry"
```

---

## Task 10: Add Native Canonical MatchReplayV4

**Files:**

- Create: `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/ReplayExecutionEnvelopeRecordV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/ReplayTrajectoryArtifactRecordV4.cs`
- Create: `Assets/Volleyball/Shared/Runtime/ReplayAbilityConsumptionRecordV4.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/ContractJson.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV1Tests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`

- [ ] **Step 1: Write canonical replay tests**

Build a fixed-seed replay and assert byte stability across repeated runs. The canonical V4 segment must persist:

- V4 context and derived-attribute fingerprints;
- formula/coefficient versions and dominant hand;
- envelope identity, all boundaries, policy identity, and source intent;
- consumed derived attribute names/values, with no false claim that serialization equals consumption;
- trajectory artifact identity, provider source/version/configuration, full cache key, and degradation step;
- actual sample classification and diagnostic dimensions;
- observed P6 geometry and V3 rule decision/reason code.

V4 replay deserialization rejects V1/V2/V3 payloads and missing required records.

- [ ] **Step 2: Define Shared replay DTOs without Match dependencies**

Replay DTOs use primitive values and Shared types only. The Match recorder maps domain envelopes/artifacts/classifications into immutable replay records. Shared must not reference a Match assembly.

- [ ] **Step 3: Implement strict canonical serialization**

Add:

```csharp
public static string SerializeV4(MatchReplayV4 value);
public static MatchReplayV4 DeserializeMatchReplayV4(string json);
```

Sort events by sequence number and reject duplicates or gaps. Sort ability-consumption entries by the frozen derived-field order. Hash the canonical segment bytes and store the lowercase SHA-256 digest.

- [ ] **Step 4: Update recorder and HTML output**

Recorder accepts only `MatchReplayV4`. HTML displays contract version 4 and rules version 3 separately, plus envelope, trajectory, classification, and P6 geometry diagnostics. Remove “reserved V2 section” output from the formal path.

- [ ] **Step 5: Run replay tests and commit**

Run:

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/wys/Documents/program/volleyball-match \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.Shared.EditModeTests.MatchContractTests;Volleyball.EditModeTests.MatchReplayV1Tests" \
  -testResults /tmp/volleyball-v4-replay-edit.xml \
  -logFile /tmp/volleyball-v4-replay-edit.log
```

Expected: all selected tests pass after renaming V1-specific test fixtures/classes to V4 where appropriate.

```bash
git add Assets/Volleyball/Shared Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs \
  Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs \
  Assets/Volleyball/Match/Tests/EditMode/MatchReplayV1Tests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs
git commit -m "feat: record canonical native v4 replays"
```

---

## Task 11: Prove Formal Diagnostic Invariance and End-to-End V4

**Files:**

- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs`

- [ ] **Step 1: Add paired fixed-seed runs**

Run the same formal 6v6 fixture with shadow diagnostics/cache recording disabled and enabled. Assert identical:

- winner and score;
- accepted contact count;
- V3 rules transition count and ordered reason codes;
- ball-state versions at accepted contacts.

Diagnostics may add records but may not consume authoritative RNG, mutate the ball, or alter candidate selection.

- [ ] **Step 2: Add end-to-end V4 assertions**

Assert formal initialization, player binding, derivation, execution envelope, trajectory artifact, result, and replay all carry V4 identities while rules decisions explicitly carry V3 identity.

- [ ] **Step 3: Run PlayMode tests**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/wys/Documents/program/volleyball-match \
  -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests;Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests;Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests" \
  -testResults /tmp/volleyball-v4-playmode.xml \
  -logFile /tmp/volleyball-v4-playmode.log
```

Expected: all selected PlayMode tests pass, and the invariance test reports identical authority summaries.

- [ ] **Step 4: Commit**

```bash
git add Assets/Volleyball/Match/Tests/PlayMode
git commit -m "test: prove v4 formal diagnostic invariance"
```

---

## Task 12: Remove Legacy Production Paths and Verify Gates A–E

**Files:**

- Delete only after `rg` verification: obsolete production V1/V2/V3 attribute/context/result/replay files that have no remaining rule-engine role
- Modify: `docs/development.md`
- Modify: `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

- [ ] **Step 1: Find remaining legacy production references**

Run:

```bash
rg -n "PlayerAbilitySnapshotV[123]|MatchContextV[123]|MatchResultV[123]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'
```

Expected: no Career or formal Match production entry points remain. References retained solely for the V3 rules engine must be rules types, not attribute/match/replay contracts.

- [ ] **Step 2: Delete unreachable legacy contract paths**

Delete a legacy file only when `rg` proves it has no production or required test consumer. Delete its `.meta` in the same commit. Update tests to V4 names; do not preserve compatibility fixtures.

- [ ] **Step 3: Run all EditMode tests**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/wys/Documents/program/volleyball-match \
  -runTests -testPlatform EditMode \
  -testResults /tmp/volleyball-v4-all-editmode.xml \
  -logFile /tmp/volleyball-v4-all-editmode.log
```

Expected: zero failures, zero ignored migration tests, and at least the pre-migration baseline of 491 passing tests after accounting for intentional replacements.

- [ ] **Step 4: Run all PlayMode tests**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/wys/Documents/program/volleyball-match \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/volleyball-v4-all-playmode.xml \
  -logFile /tmp/volleyball-v4-all-playmode.log
```

Expected: zero failures and no change to the fixed-seed authority summaries.

- [ ] **Step 5: Verify compilation and repository state**

```bash
rg -n "PlayerAbilitySnapshotV[123]|MatchContextV[12]|MatchResultV[12]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'
git status --short
git diff --check
```

Expected: the first command returns no forbidden production paths, `git diff --check` is clean, and only intended documentation/test artifacts remain uncommitted.

- [ ] **Step 6: Update documentation**

Document:

- V4-only save/Career/Match/replay support;
- independent V3 rules version;
- frozen base and derived fields;
- formula/coefficient versioning rule;
- exact Unity verification commands;
- Gate A–E completion evidence and remaining Gate F–K work.

- [ ] **Step 7: Final commit**

```bash
git add Assets/Volleyball docs/development.md \
  docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md
git commit -m "chore: complete full rally v4 gates a through e"
```

## Gate A–E Definition of Done

- [ ] Production Career, Match, and replay entry points accept only concrete V4 contracts.
- [ ] Existing V3 rules authority remains independently identified and covered.
- [ ] Every base attribute has a documented, tested V4 effect; consumption is recorded separately from serialization.
- [ ] Planner and executor consume the same envelope instance and identity.
- [ ] Invalid or exceeded samples produce explicit diagnostics and are never silently repaired.
- [ ] Both teams receive identical trajectory artifacts for identical complete keys.
- [ ] P6 decisions use observed takeoff/contact geometry.
- [ ] Fixed-seed replay canonical segment bytes are stable.
- [ ] Shadow diagnostics do not change scoring, accepted contacts, or V3 transitions.
- [ ] Full EditMode and PlayMode suites pass.

## Deferred Plans

Gate F–K intentionally remain outside this implementation plan:

- Gate F: authoritative 12-player shadow roster and substitution lifecycle;
- Gate G: component split and ownership boundaries;
- Gate H: organization/tactical authority;
- Gate I: attack execution expansion;
- Gate J: defense execution expansion;
- Gate K: perception, director slimming, and calibration.

Write those plans only after Gate E freezes V4 contract, replay, envelope, prediction, and P6 geometry interfaces.
