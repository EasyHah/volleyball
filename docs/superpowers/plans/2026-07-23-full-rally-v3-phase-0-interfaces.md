# Full Rally V3 Phase 0 Interfaces Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Phase 0 interface contracts and deterministic guardrails required before Full Rally V3 implementation starts.

**Architecture:** Keep Shared contract versioning explicit, following the existing V1/V2 DTO, JSON, validation, and hash pattern. Match receives CPU-authoritative placeholder interfaces for coverage decisions, execution envelopes, deterministic work budgets, and shared trajectory artifacts; implementation details stay skeletal until later phases.

**Tech Stack:** Unity C#, DataContract JSON, NUnit EditMode tests, existing `Volleyball.Shared`, `Volleyball.Match.Domain`, and `Volleyball.Match.AI` assemblies.

---

## File Structure

- Modify `Assets/Volleyball/Shared/Runtime/ContractPrimitives.cs`: add `ContractVersions.MatchV3` and `ReplayV2`.
- Create `Assets/Volleyball/Shared/Runtime/PlayerAbilitySnapshotV3.cs`: eleven V3 abilities plus migration provenance.
- Create `Assets/Volleyball/Shared/Runtime/PlayerSnapshotV3.cs`: V3 player DTO.
- Create `Assets/Volleyball/Shared/Runtime/TeamSnapshotV3.cs`: V3 team DTO.
- Create `Assets/Volleyball/Shared/Runtime/MatchContextV3.cs`: V3 context and `CanonicalMatchContextHashV3`.
- Create `Assets/Volleyball/Shared/Runtime/MatchResultV3.cs`: V3 result and `CanonicalMatchResultHashV3`.
- Modify `Assets/Volleyball/Shared/Runtime/ContractJson.cs`: add V3 and Replay V2 explicit serialization APIs.
- Create `Assets/Volleyball/Shared/Runtime/MatchReplayV2.cs`: persisted replay shell with reserved V3 diagnostic sections.
- Modify `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`: V3 contract, migration, hash, and version rejection tests.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionEnvelopeV3.cs`: planner/executor envelope identity.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/PlanCoverageDecision.cs`: deterministic contact coverage decision DTO.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/DeterministicWorkBudgetV3.cs`: work-unit caps and degradation mode.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BallTrajectoryArtifactV3.cs`: shared trajectory artifact identity.
- Create `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3Phase0ContractTests.cs`: Match-side Phase 0 interface tests.
- Modify `docs/changes/2026-07-23-001-full-rally-v3-architecture.md`: mark implemented files and verification after tasks complete.

## Task 1: Shared Version Constants

**Files:**
- Modify: `Assets/Volleyball/Shared/Runtime/ContractPrimitives.cs`
- Test: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Add failing version assertions**

Add this test to `MatchContractTests`:

```csharp
[Test]
public void ContractVersions_ReserveV3ContextAndReplayV2()
{
    Assert.That(ContractVersions.MatchV3, Is.EqualTo(3));
    Assert.That(ContractVersions.ReplayV2, Is.EqualTo(2));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: Unity EditMode tests for `Volleyball.Shared.EditModeTests` or the project test runner command used in this repo.

Expected: FAIL because `MatchV3` and `ReplayV2` are not defined.

- [ ] **Step 3: Add constants**

In `ContractPrimitives.cs`, extend `ContractVersions`:

```csharp
public const int MatchV3 = 3;
public const int ReplayV2 = 2;
```

- [ ] **Step 4: Run test to verify it passes**

Run the same EditMode test selection.

Expected: PASS for `ContractVersions_ReserveV3ContextAndReplayV2`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Shared/Runtime/ContractPrimitives.cs Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: reserve full rally v3 contract versions"
```

## Task 2: V3 Ability Snapshot and Migration Provenance

**Files:**
- Create: `Assets/Volleyball/Shared/Runtime/PlayerAbilitySnapshotV3.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Add failing migration tests**

Add tests covering deterministic migration, distinguishable axes, and collapsed-axis metadata:

```csharp
[Test]
public void PlayerAbilitySnapshotV3_MigrationIsDeterministicAndRecordsProvenance()
{
    var source = new PlayerAbilitySnapshotV2(0.7f, 0.6f, 0.8f, 0.5f, 0.9f, 0.75f, 0.85f, 3.42f);

    var first = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);
    var second = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);

    Assert.That(second, Is.EqualTo(first));
    Assert.That(first.SourceVersion, Is.EqualTo(ContractVersions.MatchV2));
    Assert.That(first.MigrationVersion, Is.EqualTo(PlayerAbilitySnapshotV3.CurrentMigrationVersion));
    Assert.That(first.IsCompatibilityEstimate, Is.True);
}

[Test]
public void PlayerAbilitySnapshotV3_MigrationDistinguishesAttackControlAndSoftTouchWhenRoleProxyExists()
{
    var source = new PlayerAbilitySnapshotV2(0.7f, 0.6f, 0.8f, 0.5f, 0.9f, 0.75f, 0.85f, 3.42f);

    var hitter = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.OutsideHitter);
    var setter = PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(source, PlayerPosition.Setter);

    Assert.That(hitter.AttackControl, Is.Not.EqualTo(hitter.SoftTouch));
    Assert.That(setter.AttackControl, Is.Not.EqualTo(setter.SoftTouch));
}

[Test]
public void PlayerAbilitySnapshotV3_MigrationRejectsNullSource()
{
    Assert.That(
        () => PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(null, PlayerPosition.Setter),
        Throws.TypeOf<ArgumentNullException>());
}
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL because `PlayerAbilitySnapshotV3` does not exist.

- [ ] **Step 3: Implement V3 ability DTO**

Create `PlayerAbilitySnapshotV3.cs` with:

```csharp
using System;
using System.Runtime.Serialization;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class PlayerAbilitySnapshotV3 : IEquatable<PlayerAbilitySnapshotV3>
    {
        public const int CurrentMigrationVersion = 1;

        [DataMember(Name = "mobility", Order = 1)] private float _mobility;
        [DataMember(Name = "reaction", Order = 2)] private float _reaction;
        [DataMember(Name = "jump", Order = 3)] private float _jump;
        [DataMember(Name = "maxAttackReach", Order = 4)] private float _maxAttackReach;
        [DataMember(Name = "receiveTechnique", Order = 5)] private float _receiveTechnique;
        [DataMember(Name = "setTechnique", Order = 6)] private float _setTechnique;
        [DataMember(Name = "attackControl", Order = 7)] private float _attackControl;
        [DataMember(Name = "attackPower", Order = 8)] private float _attackPower;
        [DataMember(Name = "softTouch", Order = 9)] private float _softTouch;
        [DataMember(Name = "blockTechnique", Order = 10)] private float _blockTechnique;
        [DataMember(Name = "courtAwareness", Order = 11)] private float _courtAwareness;
        [DataMember(Name = "sourceVersion", Order = 12)] private int _sourceVersion;
        [DataMember(Name = "migrationVersion", Order = 13)] private int _migrationVersion;
        [DataMember(Name = "isCompatibilityEstimate", Order = 14)] private bool _isCompatibilityEstimate;
        [DataMember(Name = "compatibilityCollapsedAxes", Order = 15)] private string[] _compatibilityCollapsedAxes;

        public PlayerAbilitySnapshotV3(
            float mobility,
            float reaction,
            float jump,
            float maxAttackReach,
            float receiveTechnique,
            float setTechnique,
            float attackControl,
            float attackPower,
            float softTouch,
            float blockTechnique,
            float courtAwareness,
            int sourceVersion,
            int migrationVersion,
            bool isCompatibilityEstimate,
            string[] compatibilityCollapsedAxes)
        {
            _mobility = ContractGuard.Unit(mobility, nameof(mobility));
            _reaction = ContractGuard.Unit(reaction, nameof(reaction));
            _jump = ContractGuard.Unit(jump, nameof(jump));
            _maxAttackReach = ContractGuard.AttackReach(maxAttackReach, nameof(maxAttackReach));
            _receiveTechnique = ContractGuard.Unit(receiveTechnique, nameof(receiveTechnique));
            _setTechnique = ContractGuard.Unit(setTechnique, nameof(setTechnique));
            _attackControl = ContractGuard.Unit(attackControl, nameof(attackControl));
            _attackPower = ContractGuard.Unit(attackPower, nameof(attackPower));
            _softTouch = ContractGuard.Unit(softTouch, nameof(softTouch));
            _blockTechnique = ContractGuard.Unit(blockTechnique, nameof(blockTechnique));
            _courtAwareness = ContractGuard.Unit(courtAwareness, nameof(courtAwareness));
            _sourceVersion = sourceVersion;
            _migrationVersion = migrationVersion;
            _isCompatibilityEstimate = isCompatibilityEstimate;
            _compatibilityCollapsedAxes = compatibilityCollapsedAxes ?? Array.Empty<string>();
            Validate();
        }

        public float Mobility => _mobility;
        public float Reaction => _reaction;
        public float Jump => _jump;
        public float MaxAttackReach => _maxAttackReach;
        public float ReceiveTechnique => _receiveTechnique;
        public float SetTechnique => _setTechnique;
        public float AttackControl => _attackControl;
        public float AttackPower => _attackPower;
        public float SoftTouch => _softTouch;
        public float BlockTechnique => _blockTechnique;
        public float CourtAwareness => _courtAwareness;
        public int SourceVersion => _sourceVersion;
        public int MigrationVersion => _migrationVersion;
        public bool IsCompatibilityEstimate => _isCompatibilityEstimate;
        public IReadOnlyList<string> CompatibilityCollapsedAxes => Array.AsReadOnly(_compatibilityCollapsedAxes);

        public static PlayerAbilitySnapshotV3 LegacyV2ToPlayerAbilitySnapshotV3(
            PlayerAbilitySnapshotV2 source,
            PlayerPosition position)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Validate();

            var attackControl = Clamp01(source.AttackTechnique + AttackControlRoleOffset(position));
            var softTouch = Clamp01(source.AttackTechnique + SoftTouchRoleOffset(position));
            var blockTechnique = Clamp01((source.Jump * 0.6f) + (source.ReceiveTechnique * 0.4f));
            var courtAwareness = Clamp01((source.Reaction * 0.7f) + (source.SetTechnique * 0.3f));

            return new PlayerAbilitySnapshotV3(
                source.Mobility,
                source.Reaction,
                source.Jump,
                source.MaxAttackReach,
                source.ReceiveTechnique,
                source.SetTechnique,
                attackControl,
                source.AttackPower,
                softTouch,
                blockTechnique,
                courtAwareness,
                ContractVersions.MatchV2,
                CurrentMigrationVersion,
                true,
                Array.Empty<string>());
        }

        internal void Validate()
        {
            ContractGuard.Unit(_mobility, nameof(Mobility));
            ContractGuard.Unit(_reaction, nameof(Reaction));
            ContractGuard.Unit(_jump, nameof(Jump));
            ContractGuard.AttackReach(_maxAttackReach, nameof(MaxAttackReach));
            ContractGuard.Unit(_receiveTechnique, nameof(ReceiveTechnique));
            ContractGuard.Unit(_setTechnique, nameof(SetTechnique));
            ContractGuard.Unit(_attackControl, nameof(AttackControl));
            ContractGuard.Unit(_attackPower, nameof(AttackPower));
            ContractGuard.Unit(_softTouch, nameof(SoftTouch));
            ContractGuard.Unit(_blockTechnique, nameof(BlockTechnique));
            ContractGuard.Unit(_courtAwareness, nameof(CourtAwareness));
            if (_migrationVersion < 0) throw new ContractValidationException("migrationVersion cannot be negative.");
            if (_compatibilityCollapsedAxes == null) throw new ContractValidationException("compatibilityCollapsedAxes is required.");
        }

        private static float AttackControlRoleOffset(PlayerPosition position)
        {
            return position == PlayerPosition.OutsideHitter || position == PlayerPosition.Opposite ? 0.03f : -0.01f;
        }

        private static float SoftTouchRoleOffset(PlayerPosition position)
        {
            return position == PlayerPosition.Setter || position == PlayerPosition.Libero ? 0.03f : -0.02f;
        }

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));

        public bool Equals(PlayerAbilitySnapshotV3 other)
        {
            return other != null &&
                _mobility.Equals(other._mobility) &&
                _reaction.Equals(other._reaction) &&
                _jump.Equals(other._jump) &&
                _maxAttackReach.Equals(other._maxAttackReach) &&
                _receiveTechnique.Equals(other._receiveTechnique) &&
                _setTechnique.Equals(other._setTechnique) &&
                _attackControl.Equals(other._attackControl) &&
                _attackPower.Equals(other._attackPower) &&
                _softTouch.Equals(other._softTouch) &&
                _blockTechnique.Equals(other._blockTechnique) &&
                _courtAwareness.Equals(other._courtAwareness) &&
                _sourceVersion == other._sourceVersion &&
                _migrationVersion == other._migrationVersion &&
                _isCompatibilityEstimate == other._isCompatibilityEstimate;
        }

        public override bool Equals(object obj) => Equals(obj as PlayerAbilitySnapshotV3);

        public override int GetHashCode() => _attackControl.GetHashCode();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Expected: PASS for the new V3 ability migration tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Shared/Runtime/PlayerAbilitySnapshotV3.cs Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: add full rally v3 ability snapshot"
```

## Task 3: V3 Match Context and Result Contracts

**Files:**
- Create: `Assets/Volleyball/Shared/Runtime/PlayerSnapshotV3.cs`
- Create: `Assets/Volleyball/Shared/Runtime/TeamSnapshotV3.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchContextV3.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchResultV3.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Add failing V3 round-trip/hash tests**

Add tests analogous to existing V2 tests:

```csharp
[Test]
public void MatchContextV3_RoundTripsWithV3AbilityFieldsAndContextHash()
{
    var context = CreateContextV3(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

    var restored = ContractJson.DeserializeContextV3(ContractJson.SerializeV3(context));

    Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
    Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
    Assert.That(restored.Home.Players[0].Ability.AttackControl, Is.EqualTo(0.86f));
    Assert.That(restored.Home.Players[0].Ability.SoftTouch, Is.EqualTo(0.72f));
}

[Test]
public void MatchContextV3_UpgradeFromV2UsesExplicitMigration()
{
    var legacy = CreateContextV2(new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"), 7351);

    var upgraded = MatchContextV3.UpgradeFromV2(legacy);

    Assert.That(upgraded.ContractVersion, Is.EqualTo(ContractVersions.MatchV3));
    Assert.That(upgraded.Home.Players[0].Ability.IsCompatibilityEstimate, Is.True);
    Assert.That(upgraded.Home.Players[0].Ability.SourceVersion, Is.EqualTo(ContractVersions.MatchV2));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL because V3 context/result types and JSON APIs do not exist.

- [ ] **Step 3: Implement DTOs following V2 pattern**

Implement:

- `PlayerSnapshotV3`: same shape as V2, but ability type is `PlayerAbilitySnapshotV3`.
- `TeamSnapshotV3`: same validation as V2, using `PlayerSnapshotV3`.
- `MatchContextV3`: same validation as V2, `_contractVersion = ContractVersions.MatchV3`, `UpgradeFromV2`, and `CanonicalMatchContextHashV3.Compute`.
- `MatchResultV3`: same result validation as V2, `_contractVersion = ContractVersions.MatchV3`, and `CanonicalMatchResultHashV3.Compute` or reserved method if result hash is stored later.

Use V2 code as the template and include all eleven V3 ability fields in canonical context hash order.

- [ ] **Step 4: Add helper methods in tests**

Add `CreateContextV3`, `CreateTeamV3`, and `CreatePlayerV3` helpers mirroring V2 helpers.

- [ ] **Step 5: Run tests to verify they pass**

Expected: PASS for V3 context round-trip and V2 upgrade tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/Volleyball/Shared/Runtime/PlayerSnapshotV3.cs Assets/Volleyball/Shared/Runtime/TeamSnapshotV3.cs Assets/Volleyball/Shared/Runtime/MatchContextV3.cs Assets/Volleyball/Shared/Runtime/MatchResultV3.cs Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: add full rally v3 match contracts"
```

## Task 4: Version-Explicit JSON and Rejection Tests

**Files:**
- Modify: `Assets/Volleyball/Shared/Runtime/ContractJson.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Add failing version-boundary tests**

Add tests:

```csharp
[Test]
public void ContractJson_DoesNotDeserializeV2AsV3OrV3AsV2()
{
    var v2Json = ContractJson.SerializeV2(CreateContextV2(Guid.NewGuid(), 7));
    var v3Json = ContractJson.SerializeV3(CreateContextV3(Guid.NewGuid(), 7));

    Assert.That(() => ContractJson.DeserializeContextV3(v2Json), Throws.TypeOf<ContractValidationException>());
    Assert.That(() => ContractJson.DeserializeContextV2(v3Json), Throws.TypeOf<ContractValidationException>());
}
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL if V3 JSON APIs are missing.

- [ ] **Step 3: Add V3 JSON APIs**

Extend `ContractJson`:

```csharp
public static string SerializeV3(MatchContextV3 context) { ... }
public static string SerializeV3(MatchResultV3 result) { ... }
public static MatchContextV3 DeserializeContextV3(string json) { ... }
public static MatchResultV3 DeserializeResultV3(string json) { ... }
```

Use the existing `SerializeValue` and `DeserializeValue` helpers. Do not alter V1/V2 methods.

- [ ] **Step 4: Run tests to verify they pass**

Expected: PASS for V1, V2, and V3 JSON tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Shared/Runtime/ContractJson.cs Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: add explicit v3 contract json APIs"
```

## Task 5: Replay V2 Shell

**Files:**
- Create: `Assets/Volleyball/Shared/Runtime/MatchReplayV2.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/ContractJson.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Add failing replay shell tests**

Add test:

```csharp
[Test]
public void MatchReplayV2_RoundTripsWithFormatVersionAndReservedDiagnostics()
{
    var replay = MatchReplayV2.Create(
        "replay-001",
        "context-hash-placeholder-000000000000000000000000000000000000000000",
        new[] { "PlanCoverageDecision", "ExecutionEnvelopeV3", "BallTrajectoryArtifactV3" });

    var restored = ContractJson.DeserializeReplayV2(ContractJson.SerializeReplayV2(replay));

    Assert.That(restored.FormatVersion, Is.EqualTo(ContractVersions.ReplayV2));
    Assert.That(restored.ReservedSections, Does.Contain("PlanCoverageDecision"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL because `MatchReplayV2` and replay JSON APIs do not exist.

- [ ] **Step 3: Implement replay shell**

Create a minimal DataContract:

```csharp
[DataContract]
public sealed class MatchReplayV2
{
    [DataMember(Name = "formatVersion", Order = 1)] private int _formatVersion;
    [DataMember(Name = "replayId", Order = 2)] private string _replayId;
    [DataMember(Name = "contextHash", Order = 3)] private string _contextHash;
    [DataMember(Name = "reservedSections", Order = 4)] private string[] _reservedSections;

    // Create, properties, Validate...
}
```

The shell reserves sections only; full payload fields are later tasks.

- [ ] **Step 4: Add replay JSON APIs**

Add:

```csharp
public static string SerializeReplayV2(MatchReplayV2 replay) { ... }
public static MatchReplayV2 DeserializeReplayV2(string json) { ... }
```

- [ ] **Step 5: Run tests to verify they pass**

Expected: PASS for replay shell round-trip.

- [ ] **Step 6: Commit**

```bash
git add Assets/Volleyball/Shared/Runtime/MatchReplayV2.cs Assets/Volleyball/Shared/Runtime/ContractJson.cs Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: reserve match replay v2 contract"
```

## Task 6: Match-Side Phase 0 Placeholder Interfaces

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionEnvelopeV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/PlanCoverageDecision.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/DeterministicWorkBudgetV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BallTrajectoryArtifactV3.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3Phase0ContractTests.cs`

- [ ] **Step 1: Add failing tests for placeholder contracts**

Create `FullRallyV3Phase0ContractTests.cs`:

```csharp
using NUnit.Framework;
using Volleyball.Match.Domain.FullRallyV3;

namespace Volleyball.Match.EditModeTests
{
    public sealed class FullRallyV3Phase0ContractTests
    {
        [Test]
        public void PlanCoverageDecision_IsDeterministicValueObject()
        {
            var first = PlanCoverageDecision.Covered("plan-1", PlanCoverageReason.WithinConditionalEnvelope);
            var second = PlanCoverageDecision.Covered("plan-1", PlanCoverageReason.WithinConditionalEnvelope);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Kind, Is.EqualTo(PlanCoverageDecisionKind.CoveredActivateBranch));
        }

        [Test]
        public void DeterministicWorkBudget_DoesNotContainWallClockDecisionFields()
        {
            var budget = DeterministicWorkBudgetV3.DefaultPhase0();

            Assert.That(budget.BeamWidth, Is.GreaterThan(0));
            Assert.That(budget.CandidatesPerResponsibility, Is.GreaterThan(0));
            Assert.That(budget.UsesWallClockForDecision, Is.False);
        }

        [Test]
        public void TrajectoryArtifactIdentity_MatchesForSameDeterministicInputs()
        {
            var first = BallTrajectoryArtifactV3.CreateIdentity("ball-1", "physics-1", "sample-1", "predictor-1", "normal");
            var second = BallTrajectoryArtifactV3.CreateIdentity("ball-1", "physics-1", "sample-1", "predictor-1", "normal");

            Assert.That(second, Is.EqualTo(first));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL because the `FullRallyV3` namespace/types do not exist.

- [ ] **Step 3: Implement `PlanCoverageDecision`**

Create enums and value object:

```csharp
public enum PlanCoverageDecisionKind
{
    CoveredActivateBranch,
    LocalRevision,
    ScopedReplan,
    GlobalReplan,
    TerminalNoPlan
}

public enum PlanCoverageReason
{
    WithinConditionalEnvelope,
    ResponsibleActorChanged,
    BallEnvelopeExceeded,
    EnvelopeExceeded,
    EnvelopeExpanded,
    UnexpectedExecutionSample,
    RulesStateChanged,
    CommittedResponsibilityInvalidated,
    DependencyCascadeExceeded,
    BudgetDegradationRequired,
    RallyOpen,
    RallyEnd
}
```

`PlanCoverageDecision` stores `Kind`, `PlanRevision`, `Reason`, `InvalidationSet`, and `ExpansionDepth`.

- [ ] **Step 4: Implement `DeterministicWorkBudgetV3`**

Include work-unit caps only:

```csharp
public sealed class DeterministicWorkBudgetV3
{
    public int BeamWidth { get; }
    public int CandidatesPerResponsibility { get; }
    public int PhysicalSamplesPerCandidate { get; }
    public int MaxCandidateEvaluations { get; }
    public int MaxInvalidationExpansionDepth { get; }
    public bool UsesWallClockForDecision => false;
}
```

- [ ] **Step 5: Implement envelope and trajectory identities**

`ExecutionEnvelopeV3` stores version, ability snapshot hash/provenance, action kind, baseline target key, distribution key, and deterministic sample key.

`BallTrajectoryArtifactV3` stores ball state version, physics config hash, sample key, predictor version, and degradation mode. Implement equality on all identity fields.

- [ ] **Step 6: Run tests to verify they pass**

Expected: PASS for `FullRallyV3Phase0ContractTests`.

- [ ] **Step 7: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 Assets/Volleyball/Match/Tests/EditMode/FullRallyV3Phase0ContractTests.cs
git commit -m "feat: add full rally v3 phase 0 match interfaces"
```

## Task 7: Documentation Handoff Update

**Files:**
- Modify: `docs/changes/2026-07-23-001-full-rally-v3-architecture.md`

- [ ] **Step 1: Update status and concrete file list**

Set status to `进行中` or `已完成` depending on implementation state. Replace "计划新增" phrasing with actual implemented files after Tasks 1-6.

- [ ] **Step 2: Record verification**

Add exact EditMode test counts and commands. If PlayMode was not run, keep it unchecked and state why.

- [ ] **Step 3: Run markdown/text checks**

Run:

```bash
git diff --check
rg -n "TBD|TODO|填写" docs/changes/2026-07-23-001-full-rally-v3-architecture.md
```

Expected: `git diff --check` exits 0. The `rg` command prints no unresolved placeholders.

- [ ] **Step 4: Commit**

```bash
git add docs/changes/2026-07-23-001-full-rally-v3-architecture.md
git commit -m "docs: update full rally v3 phase 0 handoff"
```

## Final Verification

- [ ] Run full Shared EditMode tests.
- [ ] Run Match EditMode tests covering `FullRallyV3Phase0ContractTests`.
- [ ] Run `git diff --check`.
- [ ] Confirm no V1/V2 method changed semantics.
- [ ] Confirm no GPU backend or GPU-dependent deterministic behavior was added.
- [ ] Confirm `docs/changes/README.md` still indexes `CHG-20260723-001`.

## Execution Choice

Plan complete and saved to `docs/superpowers/plans/2026-07-23-full-rally-v3-phase-0-interfaces.md`. Two execution options:

1. Subagent-Driven (recommended) - dispatch a fresh subagent per task and review between tasks.
2. Inline Execution - execute tasks in this session using executing-plans with checkpoints.

Which approach?
