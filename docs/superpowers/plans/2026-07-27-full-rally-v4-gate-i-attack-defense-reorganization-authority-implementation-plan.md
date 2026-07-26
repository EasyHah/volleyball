# Full Rally V4 Gate I Attack, Defense, and Reorganization Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the formal 6v6 set-target, attack, joint-defense, tool-recovery, and direct-reorganization legacy writers with one deterministic Gate I plan authority while preserving V3 rules, Replay V4 compatibility, and legacy 3v3.

**Architecture:** Add immutable Gate I plan/evidence values in Match Domain, pure
set-intent/attack/fallback and joint-defense planners in Match AI, a revisioned
coordinator in Match AI, and one presentation controller that preflights immutable
post-set command batches before using Gate G player facades. Gate H asks Gate I
for one immutable `GateISetIntentV3` at `OrganizationPlanned`, but remains the
only writer of the Set actor, timing, and contact command. After V3 rules accept
that Set, formal 6v6 performs one threat → committed defense → final choice cycle,
then routes actual contacts back through V3 rules and Gate I coverage; 3v3 keeps
the existing path.

**Tech Stack:** Unity 6000.0.43f1, C# 9, NUnit EditMode/PlayMode tests, existing Full Rally V3 rules, Shared V4 contracts/canonical JSON, execution envelopes, shared trajectory prediction, Gate F plans, Gate G player facades, and Gate H organization handoff.

---

## Scope and execution rules

- Start from the confirmed design commits `bdbfead` and `ce16e69` on
  `codex/full-rally-v4-gate-i-attack-defense-reorganization-authority`.
- Execute RED → minimal GREEN → focused regression → commit for every task.
- Do not merge, rebase onto `main`, push, or open a PR.
- Do not enable Gate I for Shadow, Disabled, incomplete formal fixtures, or 3v3.
- Do not introduce a long-lived feature flag or run formal legacy and Gate I writers
  for the same responsibility.
- Gate I owns the formal Set target/envelope/trajectory and downstream attack
  preparation, but must never schedule a second Set contact command; Gate H owns
  the sole Set actor/timing/contact write.
- Keep `CourtAwareness` out of Gate I.
- Preserve historical Replay V4 canonical bytes/hash when Gate I evidence is absent.
- Store generated XML/log evidence under ignored `TestResults/`.

## File responsibility map

- `Domain/FullRallyV3/Authority/AttackDefensePlanV3.cs`: immutable Gate I plan,
  candidates, threat, defense, fallback, and reorganization values.
- `AI/AttackDefensePlanner.cs`: pure SetIntent and post-accepted-Set attack
  candidate generation, six-gate qualification, reliability gate, and fallback
  comparison.
- `AI/JointDefensePlanner.cs`: pure threat-weighted block/floor/exit composition.
- `AI/AttackDefenseAuthorityCoordinator.cs`: revisioned lifecycle and coverage.
- `Presentation/AttackDefenseAuthorityController.cs`: atomic preflight and Gate G
  facade commands.
- `Shared/Runtime/MatchReplayV4.cs`: optional strict Gate I replay record only.
- `Presentation/MatchReplayRecorder.cs`: event-owned receipt mapping only.
- `Presentation/PhysicalMatchRallyDirector.cs`: formal integration facts and
  lifecycle dispatch, not tactical selection.

## Task 1: Freeze the Gate H Baseline and Start the Change Record

**Files:**

- Create: `docs/changes/2026-07-27-001-full-rally-v4-gate-i-attack-defense-reorganization-authority.md`
- Modify: `docs/changes/README.md`

- [ ] **Step 1: Verify the isolated worktree and implementation base.**

Run:

```bash
git_dir=$(cd "$(git rev-parse --git-dir)" && pwd -P)
git_common=$(cd "$(git rev-parse --git-common-dir)" && pwd -P)
test "$git_dir" != "$git_common"
test "$(git branch --show-current)" = \
  "codex/full-rally-v4-gate-i-attack-defense-reorganization-authority"
test "$(git merge-base HEAD aaa7fc8)" = "aaa7fc8"
git status --short
```

Expected: all `test` commands exit `0`; status is clean.

- [ ] **Step 2: Run the complete Gate H baseline from the Gate I branch.**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/GateI-baseline-editmode.xml" \
  -logFile "$PWD/TestResults/GateI-baseline-editmode.log"
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/GateI-baseline-playmode.xml" \
  -logFile "$PWD/TestResults/GateI-baseline-playmode.log"
```

Expected: EditMode `627/627`, PlayMode `31/31`, zero
failed/skipped/inconclusive. If totals differ only because tests were added before
this step, record the actual clean totals and the base commit; do not copy Gate H
numbers.

- [ ] **Step 3: Write the in-progress change record.**

Create the record with:

```markdown
# CHG-20260727-001：Full Rally V4 Gate I 攻防与重组权威

- 日期：2026-07-27
- 状态：进行中
- 影响模块：Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gate-i-attack-defense-reorganization-authority`
- 关联提交或 PR：`bdbfead`（设计）、`ce16e69`（双阶段 SetIntent
  handoff 澄清）与本实施计划的直接提交

> [!IMPORTANT]
> Shared 只新增可选 Gate I Replay V4 evidence；历史 Replay V4 无该字段时
> 保持 canonical bytes/hash。Career 与 Bootstrap 无需修改代码。

Gate I 只切换正式 6v6。3v3、V3 Shadow/Disabled、Gate J perception 和
Gate K director/replay UI 不在本改动范围。
```

Add it as the newest row in `docs/changes/README.md`.

- [ ] **Step 4: Commit the baseline record.**

```bash
git add docs/changes/2026-07-27-001-full-rally-v4-gate-i-attack-defense-reorganization-authority.md \
  docs/changes/README.md
git commit -m "docs: start gate i change record"
```

## Task 2: Add Immutable Gate I Plan Values

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority/AttackDefensePlanV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority/AttackDefensePlanV3.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackDefensePlanV3Tests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackDefensePlanV3Tests.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/TeamRallyPlanV3.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/DeterministicRallyPlanComposerV3.cs`

- [ ] **Step 1: Write RED value-invariant tests.**

Add tests that construct a complete six-player plan and assert strict copying,
enum validation, distinct responsibility ownership, and no presentation types:

```csharp
[Test]
public void Create_CopiesCandidateThreatDefenseAndExitValues()
{
    var plan = GateIPlanFixture.CreatePlan(revision: 11);

    Assert.That(plan.Revision, Is.EqualTo(11));
    Assert.That(plan.AttackCandidates, Has.Count.EqualTo(3));
    Assert.That(plan.PublicThreat.Entries, Has.Count.EqualTo(2));
    Assert.That(plan.Defense.Responsibilities, Has.Count.EqualTo(6));
    Assert.That(plan.ReorganizationExits, Is.Not.Empty);
    Assert.That(plan.SelectedAction, Is.Null);
}

[Test]
public void PublicThreatShape_ExposesNoFinalRouteOrFutureSample()
{
    var names = typeof(PublicAttackThreatV3)
        .GetProperties()
        .Select(value => value.Name)
        .Concat(typeof(PublicAttackThreatEntryV3)
            .GetProperties()
            .Select(value => value.Name))
        .ToArray();

    Assert.That(names, Has.None.Matches<string>(
        value => value.Contains("Route") || value.Contains("Sample")));
}

[Test]
public void DomainAssembly_DoesNotReferencePresentationOrUnity()
{
    var references = typeof(AttackDefensePlanV3).Assembly
        .GetReferencedAssemblies()
        .Select(value => value.Name);
    Assert.That(references, Does.Not.Contain("Volleyball.Match.Presentation"));
    Assert.That(references, Does.Not.Contain("UnityEngine"));
}
```

Define `GateIPlanFixture` at the bottom of the same test file. It must create six
distinct stable on-court IDs, three distinct candidate identities, a public threat
containing only action class/zone/probability/time, six defense responsibilities,
and one non-attacking reorganization exit. No fixture or production constructor
accepts hidden final-route or future-sample inputs.

- [ ] **Step 2: Run RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.AttackDefensePlanV3Tests" \
  -testResults "$PWD/TestResults/GateI-task2-plan-red.xml" \
  -logFile "$PWD/TestResults/GateI-task2-plan-red.log"
```

Expected: compilation fails because the Gate I values do not exist.

- [ ] **Step 3: Implement the immutable values.**

Define these public shapes, using defensive copies and strict finite/enum/identity
validation:

```csharp
public enum AttackActionClassV3
{
    PowerLine,
    PowerCross,
    PowerEdge,
    PowerOverHand,
    Tip,
    Roll,
    Push,
    HighSurvival,
    BlockOut,
    BlockToolRecovery
}

public enum DefenseResponsibilityKindV3
{
    PrimaryBlock,
    SupportingBlock,
    LineDefense,
    CrossDefense,
    DeepDefense,
    TipDefense,
    BlockShadow,
    ReboundCoverage
}

public sealed class AttackCandidateV3
{
    public string CandidateIdentity { get; }
    public PlayerId Actor { get; }
    public AttackActionClassV3 ActionClass { get; }
    public SimVector3 ContactCenter { get; }
    public SimVector3 Target { get; }
    public float ExpectedRallyValue { get; }
    public float LegalSampleRatio { get; }
    public bool IsQualifiedPowerRoute { get; }
    public string EliminationReason { get; }
    public string EnvelopeIdentity { get; }
    public string TrajectoryArtifactIdentity { get; }
}

public sealed class PublicAttackThreatV3
{
    public string ThreatIdentity { get; }
    public IReadOnlyList<PublicAttackThreatEntryV3> Entries { get; }
}

public sealed class GateISetIntentV3
{
    public long PlanRevision { get; }
    public long SourceSequence { get; }
    public PlayerId Organizer { get; }
    public PlayerId PreparedAttacker { get; }
    public SimVector3 Target { get; }
    public float GateHExpectedContactTime { get; }
    public ExecutionSampleClassificationV4 ExecutionClassification { get; }
    public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
}

public sealed class JointDefensePlanV3
{
    public IReadOnlyList<DefenseResponsibilityV3> Responsibilities { get; }
    public IReadOnlyList<ReorganizationExitV3> ReorganizationExits { get; }
}

public sealed class AttackDefensePlanV3
{
    public TeamSide AttackingSide { get; }
    public long Revision { get; }
    public string SourcePlanIdentity { get; }
    public GateISetIntentV3 SetIntent { get; }
    public IReadOnlyList<AttackCandidateV3> AttackCandidates { get; }
    public PublicAttackThreatV3 PublicThreat { get; }
    public JointDefensePlanV3 Defense { get; }
    public AttackCandidateV3 SelectedAction { get; }
    public IReadOnlyList<ReorganizationExitV3> ReorganizationExits { get; }
}
```

Do not place callbacks, commands, `UnityEngine` values, MonoBehaviours, or mutable
collections in these types. `GateISetIntentV3` owns target, exact arrival
classification/trajectory, and attack preparation only; it deliberately has no
method capable of scheduling the Set contact.
`GateHExpectedContactTime` only echoes the Gate H-owned input as a correlation
guard; Gate I cannot alter it and Gate H does not source timing back from the
intent.

- [ ] **Step 4: Enrich Gate F team plans without commands.**

Add an optional `AttackDefensePlanV3 AttackDefense` final constructor parameter
and property to `TeamRallyPlanV3`. Existing constructors delegate `null`; composer
tests must prove old plans remain byte/value stable before Gate I materialization.

- [ ] **Step 5: Run GREEN and affected plan tests.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.AttackDefensePlanV3Tests|Volleyball.EditModeTests.DeterministicRallyPlanComposerV3Tests|Volleyball.EditModeTests.ReceiveOrganizationPlanV3Tests" \
  -testResults "$PWD/TestResults/GateI-task2-plan-green.xml" \
  -logFile "$PWD/TestResults/GateI-task2-plan-green.log"
```

Expected: all pass.

- [ ] **Step 6: Commit.**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/TeamRallyPlanV3.cs \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/DeterministicRallyPlanComposerV3.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefensePlanV3Tests.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefensePlanV3Tests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/DeterministicRallyPlanComposerV3Tests.cs
git commit -m "feat: define gate i attack defense plans"
```

## Task 3: Give Soft Actions and Floor Defense Unique V4 Envelope Categories

**Files:**

- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionEnvelopePolicyV4.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionEnvelopeFactoryV4.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/ReplayAbilityConsumptionRecordV4.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/CurrentAbilityBenchmarkTests.cs`

- [ ] **Step 1: Write RED category and ability-consumption tests.**

```csharp
[TestCase(
    ExecutionCandidateCategoryV4.SoftAction,
    "Set.SoftTouch")]
[TestCase(
    ExecutionCandidateCategoryV4.Defense,
    "Defense.PlatformControl")]
public void Create_GateICategoryConsumesOnlyDeclaredControl(
    ExecutionCandidateCategoryV4 category,
    string expectedControl)
{
    var envelope = CreateEnvelope(category);

    Assert.That(
        envelope.AbilityConsumptions.Select(value => value.AttributeName),
        Does.Contain(expectedControl));
    Assert.That(
        envelope.AbilityConsumptions.Select(value => value.AttributeName),
        Does.Not.Contain("Receive.FirstTouchControl"));
}

[Test]
public void HistoricalDefaultPolicyIdentityRemainsStable()
{
    var historical = new ExecutionEnvelopePolicyV4(
        ExecutionEnvelopeV4.CurrentVersion,
        1,
        new[]
        {
            ExecutionCandidateCategoryV4.Receive,
            ExecutionCandidateCategoryV4.Set,
            ExecutionCandidateCategoryV4.Attack,
            ExecutionCandidateCategoryV4.Block,
            ExecutionCandidateCategoryV4.Serve
        },
        7,
        2,
        0,
        1.5f,
        new[]
        {
            ExecutionDegradationStepV4.FullSampling,
            ExecutionDegradationStepV4.ReducedSampleCount,
            ExecutionDegradationStepV4.CachedCoarseDistribution,
            ExecutionDegradationStepV4.DeterministicSafeFallback
        },
        BoundedErrorDistributionKindV4.BoundedUniform,
        BoundedErrorDistributionKindV4.BoundedUniform);
    Assert.That(
        ExecutionEnvelopePolicyV4.Default,
        Is.EqualTo(historical));
    CollectionAssert.AreEqual(
        ExecutionEnvelopePolicyV4.Default.ToCanonicalBytes(),
        historical.ToCanonicalBytes());
}
```
This prevents adding categories from changing existing envelope hashes without
depending on an unpublished hash literal.

- [ ] **Step 2: Run RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.Stage2AbilityEnvelopeTests|Volleyball.EditModeTests.CurrentAbilityBenchmarkTests|Volleyball.Shared.EditModeTests.MatchContractTests" \
  -testResults "$PWD/TestResults/GateI-task3-envelope-red.xml" \
  -logFile "$PWD/TestResults/GateI-task3-envelope-red.log"
```

Expected: compile failure for `SoftAction` and `Defense`.

- [ ] **Step 3: Add categories without mutating the historical default policy.**

Extend the enum:

```csharp
public enum ExecutionCandidateCategoryV4
{
    Receive,
    Set,
    Attack,
    Block,
    Serve,
    SoftAction,
    Defense
}
```

Keep `ExecutionEnvelopePolicyV4.Default` candidate order exactly
`Receive/Set/Attack/Block/Serve`. Add:

```csharp
public static ExecutionEnvelopePolicyV4 GateI { get; } =
    new ExecutionEnvelopePolicyV4(
        ExecutionEnvelopeV4.CurrentVersion,
        policyVersion: 2,
        new[]
        {
            ExecutionCandidateCategoryV4.Receive,
            ExecutionCandidateCategoryV4.Set,
            ExecutionCandidateCategoryV4.Attack,
            ExecutionCandidateCategoryV4.Block,
            ExecutionCandidateCategoryV4.Serve,
            ExecutionCandidateCategoryV4.SoftAction,
            ExecutionCandidateCategoryV4.Defense
        },
        sampleCount: 7,
        maximumExpansionCount: 2,
        allowedExpansionCount: 0,
        perStepExpansionFactor: 1.5f,
        FrozenDegradationLadder,
        BoundedErrorDistributionKindV4.BoundedUniform,
        BoundedErrorDistributionKindV4.BoundedUniform);
```

Map `SoftAction` to `Set.SoftTouch` for direction/speed control and a bounded
soft-contact capacity; map `Defense` to `Defense.PlatformControl` for
direction/speed and `Defense.CoverageMobility` for bounded capacity. Keep
movement/reaction arrival outside the contact envelope and record them in planner
evidence.

- [ ] **Step 4: Extend strict Shared readers.**

Allow canonical strings `SoftAction` and `Defense` only where execution candidate
categories are validated. Add `Set.SoftTouch`, `Defense.PlatformControl`,
`Defense.Reaction`, and `Defense.CoverageMobility` to the allowed replay
consumption names without changing the ordering or bytes of old records.

Keep event/action compatibility strict rather than requiring category and event
kind to be textually identical: `Attack` events may carry `Attack` or
`SoftAction`, and physical `Receive` events may carry `Receive` or `Defense`.
All other mismatches remain invalid. Add RED/GREEN contract tests for both allowed
pairs and for representative rejected pairs such as `Serve` + `Defense` and
`Set` + `SoftAction`.

- [ ] **Step 5: Prove one-variable independence.**

Add fixed-key tests:

```csharp
[Test]
public void FixedKey_SoftTouchChangesOnlySoftActionError()
{
    var low = GateIEnvelope(Derived(softTouch: .1f), SoftAction);
    var high = GateIEnvelope(Derived(softTouch: .9f), SoftAction);
    Assert.That(high.TargetError.MaximumAbsoluteError.Magnitude,
        Is.LessThan(low.TargetError.MaximumAbsoluteError.Magnitude));
    Assert.That(
        GateIEnvelope(Derived(softTouch: .9f), Attack).TargetError,
        Is.EqualTo(GateIEnvelope(Derived(softTouch: .1f), Attack).TargetError));
}

[Test]
public void FixedKey_DefensePlatformChangesDefenseErrorNotArrival()
{
    var low = DefenseEvidence(platform: .1f, mobility: .8f);
    var high = DefenseEvidence(platform: .9f, mobility: .8f);
    Assert.That(high.ErrorMagnitude, Is.LessThan(low.ErrorMagnitude));
    Assert.That(high.ReachMargin, Is.EqualTo(low.ReachMargin));
}
```

- [ ] **Step 6: Run GREEN and commit.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.Stage2AbilityEnvelopeTests|Volleyball.EditModeTests.CurrentAbilityBenchmarkTests|Volleyball.Shared.EditModeTests.MatchContractTests" \
  -testResults "$PWD/TestResults/GateI-task3-envelope-green.xml" \
  -logFile "$PWD/TestResults/GateI-task3-envelope-green.log"
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 \
  Assets/Volleyball/Shared/Runtime \
  Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/CurrentAbilityBenchmarkTests.cs \
  Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: add gate i execution categories"
```

## Task 4: Implement Error-Aware Attack and Unified Fallback Planning

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/AI/AttackDefensePlanner.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/AttackDefensePlanner.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackDefensePlannerTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackDefensePlannerTests.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/AI/AttackRouteSelector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs`
- Modify: `Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs`

- [ ] **Step 1: Write RED qualification and fallback tests.**

```csharp
[Test]
public void PlanSetIntent_UsesGateHOrganizerButOwnsTargetAndEnvelope()
{
    var result = Planner().PlanSetIntent(Fixture.OrganizationPlanned());

    Assert.That(result.Organizer, Is.EqualTo(Fixture.GateHOrganizer));
    Assert.That(result.Target, Is.EqualTo(Fixture.ExpectedGateITarget));
    Assert.That(result.ExecutionClassification.ExecutableEnvelope.Identity,
        Is.EqualTo(Fixture.ExpectedSetEnvelopeIdentity));
    Assert.That(result.TrajectoryArtifact.ArtifactIdentity,
        Is.EqualTo(Fixture.ExpectedSetTrajectoryIdentity));
}

[Test]
public void PlanAttack_ASetKeepsReliablePowerRoute()
{
    var result = Planner().PlanAttack(Fixture.AcceptedASet());
    Assert.That(result.QualifiedPowerRoutes, Is.Not.Empty);
    Assert.That(result.PublicThreat.Entries,
        Has.Some.Property("ActionClass").EqualTo(AttackActionClassV3.PowerLine));
}

[Test]
public void PlanAttack_PoorSetEliminatesMostlyNonCrossingPowerBeforeScore()
{
    var result = Planner().PlanAttack(Fixture.AcceptedPoorSet());
    Assert.That(result.Candidates.Where(value => value.IsPower),
        Has.All.Property("IsQualifiedPowerRoute").False);
    Assert.That(result.Candidates.Where(value => value.IsPower),
        Has.All.Property("EliminationReason").EqualTo("InsufficientLegalCrossRatio"));
}

[Test]
public void FinalChoice_UsesOneFallbackPool()
{
    var result = Planner().ChooseFinal(
        Fixture.NoReliablePower(),
        Fixture.CommittedDefense());
    Assert.That(result.Candidate.ActionClass,
        Is.EqualTo(AttackActionClassV3.HighSurvival));
    Assert.That(result.ComparableFallbacks.Select(value => value.ActionClass),
        Is.EquivalentTo(new[]
        {
            AttackActionClassV3.Tip,
            AttackActionClassV3.Roll,
            AttackActionClassV3.Push,
            AttackActionClassV3.HighSurvival,
            AttackActionClassV3.BlockOut,
            AttackActionClassV3.BlockToolRecovery
        }));
}
```

- [ ] **Step 2: Run RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.AttackDefensePlannerTests|Volleyball.EditModeTests.AttackRouteSelectorTests|Volleyball.EditModeTests.SetQualityAssessmentTests" \
  -testResults "$PWD/TestResults/GateI-task4-attack-red.xml" \
  -logFile "$PWD/TestResults/GateI-task4-attack-red.log"
```

Expected: planner types missing.

- [ ] **Step 3: Extract route enumeration from selection.**

Keep `AttackRouteSelector.Select` for 3v3 compatibility. Add a pure method:

```csharp
public static IReadOnlyList<AttackRouteEvaluation> EvaluateAll(
    AttackRouteSelectionInput input,
    ExecutionEnvelopeV4 envelope,
    IReadOnlyList<BallTrajectoryPredictionArtifactV4> samples)
```

Each evaluation records legal crossing ratio, net/antenna/out ratio, arm
clearance, target, velocity, expected value, elimination reason, envelope identity,
and trajectory identities. Do not catch an illegal route and replace it with
`ReturnVelocitySolver` inside Gate I.

- [ ] **Step 4: Implement planning phases.**

Use explicit requests/results:

```csharp
public sealed class AttackPlanningRequestV3
{
    public long Revision { get; }
    public GateISetIntentV3 SetIntent { get; }
    public AcceptedSetEvidenceV3 ActualSet { get; }
    public IReadOnlyList<RallyPlayerSnapshot> Attackers { get; }
    public OnCourtEligibilitySnapshot Eligibility { get; }
}

public sealed class AttackPlanningResultV3
{
    public AttackDefensePlanV3 Plan { get; }
    public IReadOnlyList<AttackCandidateV3> QualifiedPowerRoutes { get; }
    public IReadOnlyList<AttackCandidateV3> FallbackCandidates { get; }
    public PublicAttackThreatV3 PublicThreat { get; }
}
```

Add `PlanSetIntent(SetIntentPlanningRequestV3)` as the only Gate I operation before
Set acceptance. Its request contains the Gate H organizer, expected Set contact
time, accepted first-pass facts, eligible attackers, and current rules facts. It
creates the immutable target, exact executable envelope, trajectory artifact, and
prepared attacker once. It does not create or schedule a contact command.

`PlanAttack` runs only after V3 rules accept the Gate H Set command. It receives
the original `GateISetIntentV3` plus immutable `AcceptedSetEvidenceV3`, verifies
that actor, envelope, and trajectory identities match, then creates the candidate
pool. Use deterministic ordering by qualified state, expected value, actor stable
ID, action enum, then candidate identity. `ChooseFinal` accepts a committed
defense value and runs once.

- [ ] **Step 5: Run GREEN and commit.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.AttackDefensePlannerTests|Volleyball.EditModeTests.AttackRouteSelectorTests|Volleyball.EditModeTests.SetQualityAssessmentTests|Volleyball.EditModeTests.TeamRallyDecisionPlannerTests" \
  -testResults "$PWD/TestResults/GateI-task4-attack-green.xml" \
  -logFile "$PWD/TestResults/GateI-task4-attack-green.log"
git add Assets/Volleyball/Match/Runtime/AI/AttackDefensePlanner.cs \
  Assets/Volleyball/Match/Runtime/AI/AttackDefensePlanner.cs.meta \
  Assets/Volleyball/Match/Runtime/AI/AttackRouteSelector.cs \
  Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs \
  Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefensePlannerTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefensePlannerTests.cs.meta
git commit -m "feat: plan gate i attack candidates"
```

## Task 5: Compose One Threat-Weighted Joint Defense

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/JointDefensePlannerTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/JointDefensePlannerTests.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/AI/BlockUnitPlanner.cs`

- [ ] **Step 1: Write RED no-clairvoyance and residual-coverage tests.**

```csharp
[Test]
public void Plan_ReadsPublicThreatWithoutFinalRoute()
{
    var request = Fixture.DefenseRequest(
        publicThreat: Fixture.LineHeavyThreat());
    var defense = Planner().Plan(request);

    Assert.That(defense.SourceThreatIdentity,
        Is.EqualTo(request.PublicThreat.ThreatIdentity));
    Assert.That(defense.Responsibilities, Has.Count.EqualTo(6));
    Assert.That(
        typeof(JointDefensePlanningRequestV3)
            .GetProperties()
            .Select(value => value.Name),
        Has.None.Matches<string>(
            value => value.Contains("FinalRoute") ||
                     value.Contains("Sample")));
}

[Test]
public void Plan_FloorCoverageTargetsResidualThreat()
{
    var defense = Planner().Plan(Fixture.LineHeavyDefenseRequest());
    Assert.That(defense.BlockedZones, Does.Contain("Line"));
    Assert.That(defense.FloorCoveredZones, Does.Contain("Cross"));
    Assert.That(defense.FloorCoveredZones, Does.Not.Contain("Line"));
}

[Test]
public void Plan_IsInvariantToHiddenFinalRoute()
{
    var first = Planner().Plan(Fixture.RequestWithHiddenFinal("line"));
    var second = Planner().Plan(Fixture.RequestWithHiddenFinal("cross"));
    Assert.That(second, Is.EqualTo(first));
}
```

The fixture must pass hidden values through a test-only wrapper outside the
planner request; `JointDefensePlanningRequestV3` has no final-route property.

- [ ] **Step 2: Run RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.JointDefensePlannerTests|Volleyball.EditModeTests.BlockUnitPlannerTests" \
  -testResults "$PWD/TestResults/GateI-task5-defense-red.xml" \
  -logFile "$PWD/TestResults/GateI-task5-defense-red.log"
```

- [ ] **Step 3: Implement bounded joint composition.**

```csharp
public sealed class JointDefensePlanningRequestV3
{
    public long Revision { get; }
    public TeamSide DefendingSide { get; }
    public PublicAttackThreatV3 PublicThreat { get; }
    public IReadOnlyList<DefensePlayerSnapshotV3> Players { get; }
    public IReadOnlyList<PlayerResponsibilityAssignmentV3> Assignments { get; }
    public IReadOnlyList<ReorganizationExitV3> Exits { get; }
}

public sealed class JointDefensePlanner
{
    public JointDefensePlanV3 Plan(JointDefensePlanningRequestV3 request);
}
```

Use bounded deterministic composition: enumerate legal primary block units, add
floor responsibilities for residual weighted zones, reject hard claim conflicts,
require one primary responsibility per on-court player, score continuation exits,
and tie-break by stable IDs and responsibility enum. Adapt `BlockUnitPlanner`
through a pure `EvaluateUnits` API; retain `Select` for 3v3.

- [ ] **Step 4: Run GREEN and commit.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.JointDefensePlannerTests|Volleyball.EditModeTests.BlockUnitPlannerTests|Volleyball.EditModeTests.AttackDefensePlanV3Tests" \
  -testResults "$PWD/TestResults/GateI-task5-defense-green.xml" \
  -logFile "$PWD/TestResults/GateI-task5-defense-green.log"
git add Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs \
  Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs.meta \
  Assets/Volleyball/Match/Runtime/AI/BlockUnitPlanner.cs \
  Assets/Volleyball/Match/Tests/EditMode/JointDefensePlannerTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/JointDefensePlannerTests.cs.meta
git commit -m "feat: compose gate i joint defense"
```

## Task 6: Add Tool-Recovery Qualification and Reorganization Exits

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/AI/BlockToolRecoveryPlanner.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/BlockToolRecoveryPlanner.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/BlockToolRecoveryPlannerTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/BlockToolRecoveryPlannerTests.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/AI/AttackDefensePlanner.cs`

- [ ] **Step 1: Write RED validity tests.**

```csharp
[Test]
public void Qualify_AcceptsLegalHomeReboundWithNonAttackerExit()
{
    var result = Planner().Qualify(Fixture.PlayableToolRecovery());
    Assert.That(result.IsQualified, Is.True);
    Assert.That(result.RecoveryActor,
        Is.Not.EqualTo(result.Attacker));
    Assert.That(result.ReorganizationExit, Is.Not.Null);
}

[TestCase(ToolRecoveryFailure.NoBlockContact)]
[TestCase(ToolRecoveryFailure.ReboundsAway)]
[TestCase(ToolRecoveryFailure.NoNonAttackerContinuation)]
[TestCase(ToolRecoveryFailure.NoRemainingTouch)]
[TestCase(ToolRecoveryFailure.NoReorganizationExit)]
public void Qualify_RejectsMissingRequiredLink(
    ToolRecoveryFailure failure)
{
    var result = Planner().Qualify(Fixture.InvalidRecovery(failure));
    Assert.That(result.IsQualified, Is.False);
    Assert.That(result.Failure, Is.EqualTo(failure));
}
```

- [ ] **Step 2: Run RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.BlockToolRecoveryPlannerTests" \
  -testResults "$PWD/TestResults/GateI-task6-tool-red.xml" \
  -logFile "$PWD/TestResults/GateI-task6-tool-red.log"
```

- [ ] **Step 3: Implement the five-link qualification.**

```csharp
public sealed class BlockToolRecoveryResultV3
{
    public bool IsQualified { get; }
    public ToolRecoveryFailure Failure { get; }
    public PlayerId Attacker { get; }
    public PlayerId? RecoveryActor { get; }
    public float BlockContactProbability { get; }
    public float HomeReboundProbability { get; }
    public float TeammateReachProbability { get; }
    public float ContinuationQuality { get; }
    public float ImmediateLossRisk { get; }
    public ReorganizationExitV3 ReorganizationExit { get; }
    public float Value =>
        BlockContactProbability *
        HomeReboundProbability *
        TeammateReachProbability *
        ContinuationQuality -
        ImmediateLossRisk;
}
```

Require actual rule eligibility and remaining touches in the request. Select only
non-attacking on-court teammates, use shared rebound trajectory samples, require
positive reach/control margin, and return a stable failure enum. Feed qualified
results into the same fallback list used by Task 4.

- [ ] **Step 4: Run GREEN and commit.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.BlockToolRecoveryPlannerTests|Volleyball.EditModeTests.AttackDefensePlannerTests" \
  -testResults "$PWD/TestResults/GateI-task6-tool-green.xml" \
  -logFile "$PWD/TestResults/GateI-task6-tool-green.log"
git add Assets/Volleyball/Match/Runtime/AI/BlockToolRecoveryPlanner.cs \
  Assets/Volleyball/Match/Runtime/AI/BlockToolRecoveryPlanner.cs.meta \
  Assets/Volleyball/Match/Runtime/AI/AttackDefensePlanner.cs \
  Assets/Volleyball/Match/Tests/EditMode/BlockToolRecoveryPlannerTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/BlockToolRecoveryPlannerTests.cs.meta
git commit -m "feat: qualify gate i tool recovery"
```

## Task 7: Coordinate Gate I Revisions and Coverage

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/AI/AttackDefenseAuthorityCoordinator.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/AttackDefenseAuthorityCoordinator.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityCoordinatorTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityCoordinatorTests.cs.meta`

- [ ] **Step 1: Write RED lifecycle tests.**

Cover the ordered phases:

```csharp
public enum AttackDefenseAuthorityPhaseV3
{
    Idle,
    SetIntentPlanned,
    AttackPlanned,
    ThreatPublished,
    DefenseCommitted,
    AttackCommitted,
    AwaitingActualContact,
    ReorganizationPlanned,
    HandedOff,
    Terminal
}
```

Tests:

```csharp
[Test]
public void ThreatDefenseFinalChoice_RunsOnceInOrder()
{
    var coordinator = Fixture.Coordinator();
    var setPlanning = coordinator.PlanSetIntent(
        Fixture.SetIntentRequest(4, 1));
    var intent = setPlanning.Intent;
    coordinator.AcceptSet(
        Fixture.AcceptedSet(4, 2, intent),
        Fixture.AttackPlan(intent));
    coordinator.PublishThreat(4, 3);
    coordinator.CommitDefense(4, 4, Fixture.Defense());
    coordinator.CommitFinalAttack(4, 5);

    Assert.That(coordinator.State.Phase,
        Is.EqualTo(AttackDefenseAuthorityPhaseV3.AttackCommitted));
    Assert.That(
        () => coordinator.CommitFinalAttack(4, 6),
        Throws.InvalidOperationException);
}

[Test]
public void PlanSetIntent_DuplicateOrMismatchedAcceptedSetPublishesNothing()
{
    var fixture = Fixture.Create();
    var intent = fixture.Coordinator.PlanSetIntent(
        Fixture.SetIntentRequest(4, 1)).Intent;
    var before = fixture.Sink.Batches.Count;

    Assert.That(
        () => fixture.Coordinator.AcceptSet(
            Fixture.AcceptedSetWithOtherEnvelope(4, 2, intent),
            Fixture.AttackPlan(intent)),
        Throws.InvalidOperationException);
    Assert.That(fixture.Sink.Batches, Has.Count.EqualTo(before));
}

[Test]
public void ActualBlockRebound_UsesDeclaredReorganizationExit()
{
    var state = Fixture.AdvanceToActualContact()
        .AcceptContact(Fixture.HomeBlockRebound());
    Assert.That(state.Phase,
        Is.EqualTo(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned));
    Assert.That(state.CoverageDecision.Kind,
        Is.EqualTo(PlanCoverageDecisionKind.CoveredActivateBranch));
}

[Test]
public void StaleAndDuplicateEventsPublishNothing()
{
    var fixture = Fixture.Create();
    var before = fixture.Sink.Batches.Count;
    Assert.That(
        () => fixture.Coordinator.AcceptContact(Fixture.StaleContact()),
        Throws.InvalidOperationException);
    Assert.That(fixture.Sink.Batches, Has.Count.EqualTo(before));
}
```

- [ ] **Step 2: Run RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.AttackDefenseAuthorityCoordinatorTests" \
  -testResults "$PWD/TestResults/GateI-task7-coordinator-red.xml" \
  -logFile "$PWD/TestResults/GateI-task7-coordinator-red.log"
```

- [ ] **Step 3: Implement commands, evidence, state, and transitions.**

Define immutable command batches:

```csharp
public enum AttackDefenseCommandKind
{
    AttackPreparation,
    AttackContact,
    BlockContact,
    FloorDefense,
    AttackCover,
    Reorganization,
    CancelUncommitted
}

public sealed class AttackDefenseAuthorityEvidenceV3
{
    public long PlanRevision { get; }
    public long SourceSequence { get; }
    public AttackDefenseAuthorityPhaseV3 Phase { get; }
    public AttackDefensePlanV3 Plan { get; }
    public PlanCoverageDecision CoverageDecision { get; }
}

public sealed class GateISetIntentReceiptV3
{
    public long PlanRevision { get; }
    public long SourceSequence { get; }
    public GateISetIntentV3 Intent { get; }
    public string EvidenceIdentity { get; }
}

public sealed class GateISetIntentPlanningResultV3
{
    public GateISetIntentV3 Intent { get; }
    public GateISetIntentReceiptV3 Receipt { get; }
}
```

Map `ResponsibleActorChanged` to local, `BallEnvelopeExceeded` to scoped,
budget/dependency exhaustion to global request, and `RallyEnd` to terminal.
Never cancel committed commands. Do not execute commands in the coordinator.
`PlanSetIntent` returns the immutable intent plus evidence-only receipt but
publishes no player command batch.
`AcceptSet` requires the actual accepted Gate H Set actor, classification, and
trajectory identities to match the active SetIntent before advancing to
`AttackPlanned`. There is no Gate I Set command kind.

- [ ] **Step 4: Run GREEN and commit.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.AttackDefenseAuthorityCoordinatorTests|Volleyball.EditModeTests.ReceiveOrganizationAuthorityCoordinatorTests" \
  -testResults "$PWD/TestResults/GateI-task7-coordinator-green.xml" \
  -logFile "$PWD/TestResults/GateI-task7-coordinator-green.log"
git add Assets/Volleyball/Match/Runtime/AI/AttackDefenseAuthorityCoordinator.cs \
  Assets/Volleyball/Match/Runtime/AI/AttackDefenseAuthorityCoordinator.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityCoordinatorTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityCoordinatorTests.cs.meta
git commit -m "feat: coordinate gate i authority revisions"
```

## Task 8: Execute Gate I Through Gate G Facades

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs`
- Create: `Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PlayerTechniqueExecutor.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`

- [ ] **Step 1: Write RED atomic-preflight tests.**

```csharp
[Test]
public void Preflight_InvalidLastCommandMutatesNoPlayer()
{
    var fixture = Fixture.Create();
    var before = fixture.Snapshots();
    var batch = fixture.ValidBatch().WithLast(
        fixture.Command(actor: new StablePlayerId("bench")));

    Assert.That(
        () => fixture.Controller.PreflightAndCommit(batch),
        Throws.InvalidOperationException);
    Assert.That(fixture.Snapshots(), Is.EqualTo(before));
}

[Test]
public void Preflight_RejectsCancelOfCommittedJump()
{
    var fixture = Fixture.WithCommittedAttack();
    Assert.That(
        () => fixture.Controller.PreflightAndCommit(
            fixture.CancelCommittedAttack()),
        Throws.InvalidOperationException);
}

[Test]
public void Commit_UsesExactEnvelopeAndTrajectory()
{
    var receipt = Fixture.Create().Controller.PreflightAndCommit(
        Fixture.ValidAttackBatch());
    Assert.That(receipt.ExecutionClassification.ExecutableEnvelope.Identity,
        Is.EqualTo(Fixture.ExecutableEnvelopeIdentity));
    Assert.That(receipt.TrajectoryArtifact.ArtifactIdentity,
        Is.EqualTo(Fixture.TrajectoryIdentity));
}

[Test]
public void Controller_HasNoSetContactCommandSurface()
{
    Assert.That(
        Enum.GetNames(typeof(AttackDefenseCommandKind)),
        Does.Not.Contain("SetTargetPreparation"));
    Assert.That(
        Enum.GetNames(typeof(AttackDefenseCommandKind)),
        Does.Not.Contain("SetContact"));
}
```

- [ ] **Step 2: Run RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.AttackDefenseAuthorityControllerTests" \
  -testResults "$PWD/TestResults/GateI-task8-controller-red.xml" \
  -logFile "$PWD/TestResults/GateI-task8-controller-red.log"
```

- [ ] **Step 3: Add validation-only Gate G entry points.**

Add facade methods that validate without mutation:

```csharp
public void ValidateGateIContact(
    TechniqueAction action,
    ExecutionSampleClassificationV4 classification,
    BallTrajectoryPredictionArtifactV4 trajectory,
    AttackApproachPlan? approach,
    AttackContactPlan? contactPlan);

public void ValidateGateISupport(
    TechniqueAction action,
    float scheduledTime,
    Vector3 target);
```

`PrototypePlayerAgent` delegates validation to `PlayerTechniqueExecutor` and its
existing locomotion/action/contact components. Do not add tactical selection.

- [ ] **Step 4: Implement controller and receipts.**

`AttackDefenseAuthorityReceipt` carries revision, sequence, phase, command kind,
actor, branch, exact execution classification, trajectory, and immutable evidence.
Preflight every command first, then apply in deterministic list order. Roll back
only uncommitted mutations if an unexpected apply exception occurs. The controller
accepts only post-Set preparation/contact/defense/reorganization commands; it has
no API that can schedule a Set.

- [ ] **Step 5: Run GREEN and commit.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.AttackDefenseAuthorityControllerTests|Volleyball.EditModeTests.PlayerTechniqueExecutorTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests" \
  -testResults "$PWD/TestResults/GateI-task8-controller-green.xml" \
  -logFile "$PWD/TestResults/GateI-task8-controller-green.log"
git add Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs \
  Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs.meta \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerTechniqueExecutor.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs.meta
git commit -m "feat: execute gate i plans through player facades"
```

## Task 9: Cut Formal 6v6 to the Gate I Single Writer

**Files:**

- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityControllerTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs`

- [ ] **Step 1: Write RED formal-cutover tests.**

Add public read-only diagnostics:

```csharp
public bool GateIAuthorityEnabled { get; private set; }
public int GateILegacyWriterInvocations { get; private set; }
public int AcceptedSetContactWriterCount { get; private set; }
public event Action<AttackDefenseAuthorityReceipt>
    AttackDefenseAuthorityCommitted;
public event Action<GateISetIntentReceiptV3>
    GateISetIntentCommitted;
```

PlayMode tests:

```csharp
[UnityTest]
public IEnumerator FormalAttackDefense_UsesOneAuthorityWriter()
{
    yield return LoadFormal();
    var traces = new List<AttackDefenseAuthorityReceipt>();
    var setIntents = new List<GateISetIntentReceiptV3>();
    Director.AttackDefenseAuthorityCommitted += traces.Add;
    Director.GateISetIntentCommitted += setIntents.Add;
    yield return WaitForAccepted(TechniqueAction.Attack);

    Assert.That(Director.GateIAuthorityEnabled, Is.True);
    Assert.That(Director.GateILegacyWriterInvocations, Is.Zero);
    Assert.That(setIntents, Has.Count.EqualTo(1));
    Assert.That(Director.AcceptedSetContactWriterCount, Is.EqualTo(1));
    Assert.That(traces, Has.Some.Property("Kind")
        .EqualTo(AttackDefenseCommandKind.AttackContact));
    Assert.That(traces.Select(value => value.PlanRevision),
        Is.Ordered.Ascending);
}

[UnityTest]
public IEnumerator ThreeVsThree_RemainsOutsideGateI()
{
    yield return LoadThreeVsThree();
    Assert.That(Director.GateIAuthorityEnabled, Is.False);
}
```

- [ ] **Step 2: Run RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
"Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests.FormalAttackDefense_UsesOneAuthorityWriter|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests.ThreeVsThree_RemainsOutsideGateI" \
  -testResults "$PWD/TestResults/GateI-task9-cutover-red.xml" \
  -logFile "$PWD/TestResults/GateI-task9-cutover-red.log"
```

- [ ] **Step 3: Initialize Gate I only at the formal authority boundary.**

Enable only when:

```csharp
GateIAuthorityEnabled =
    mode == V3RulesMode.Authority &&
    GateHAuthorityEnabled &&
    _configuration.RosterSize == 6 &&
    _players.Count == 12;
```

On disable, clear coordinator, controllers, event-owned pending receipts, and
uncommitted Gate I state. Incomplete/Shadow fixtures must not construct six-player
controllers.

- [ ] **Step 4: Route Gate H handoff into Gate I.**

When Gate H reaches `OrganizationPlanned`, retain its selected organizer and
expected Set contact time, then pass those plus accepted first-pass/rules facts to
`AttackDefenseAuthorityCoordinator.PlanSetIntent`. Snapshot the returned
`GateISetIntentV3` and its evidence-only `GateISetIntentReceiptV3`.

Build the one Gate H `OrganizationContact` command with the original Gate H
organizer/timing but the Gate I target, executable classification, trajectory, prepared
attacker, and preparation target. Extend the Gate H controller preflight tests to
prove those exact identities are consumed without creating another contact
command. Suppress the legacy Gate H attack-preparation selection for formal Gate I;
the Gate H batch merely executes the preparation already present in SetIntent.
`AttackDefenseAuthorityController` is not called for Set.

After V3 rules accept that Set, pass immutable actual Set actor/classification/
trajectory facts to `AttackDefenseAuthorityCoordinator.AcceptSet`. Only then may
Gate I create attack candidates, publish threat, commit joint defense, and choose
the final attack. The formal branch obtains set target, attack candidate, threat,
defense, final choice, attack cover, block/floor assignments, and reorganization
only from Gate I.

Guard the remaining legacy methods:

```csharp
if (GateIAuthorityEnabled)
{
    ScheduleGateICommands(batch);
    return;
}

GateILegacyWriterInvocations++;
// Existing 3v3 legacy path follows.
```

The formal branch must not call tactical selection in:

- `SelectGeometricSetTarget`;
- `AttackRouteSelector.Select`;
- `PreparePhysicalBlock`;
- `TrySelectCoverPlayer`;
- post-block continuation patches.

Pure geometry/execution helpers may remain and receive already-selected immutable
values.

- [ ] **Step 5: Feed actual contacts back after V3 rules acceptance.**

Snapshot the event-owned receipt before coordinator state changes. Attack, Block,
Defense/Receive, tool rebound, crossing, and ground events advance Gate I coverage
only after rule acceptance. A no-plan result schedules nothing and lets physics/
rules resolve naturally. The accepted Set consumes the pending
`GateISetIntentReceiptV3`; actor, classification, and trajectory mismatch is a
hard failure and publishes/schedules nothing.

- [ ] **Step 6: Run formal and 3v3 GREEN.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
"Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests" \
  -testResults "$PWD/TestResults/GateI-task9-cutover-green.xml" \
  -logFile "$PWD/TestResults/GateI-task9-cutover-green.log"
```

- [ ] **Step 7: Commit.**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityControllerTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs
git commit -m "feat: authorize formal attack defense plans"
```

## Task 10: Persist Event-Owned Gate I Replay Evidence

**Files:**

- Modify: `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`

- [ ] **Step 1: Write Shared RED strict/canonical tests.**

Define the intended optional record shape in tests:

```csharp
var authority = new ReplayAttackDefenseAuthorityRecordV4(
    planRevision: 7,
    sourceSequenceNumber: 19,
    phase: "AttackCommitted",
    branch: "Primary",
    setTarget: Vector(...),
    candidates: CandidateRecords(),
    publicThreat: ThreatRecords(),
    defenseResponsibilities: DefenseRecords(),
    selectedCandidateIdentity: "attack-7-line",
    testedEnvelopeIdentity: HashA,
    executableEnvelopeIdentity: HashB,
    sampleEnvelopeIdentity: HashB,
    trajectoryArtifactIdentity: HashC,
    recovery: null,
    coverage: Coverage());
```

Assert:

- canonical round trip and hash change when authority changes;
- event/action/actor/selected candidate identities align;
- threat records contain no final-route/sample fields;
- Gate H organization authority and the Gate I SetIntent record coexist on Set,
  with the same actor/classification/trajectory identities;
- old JSON without Gate I field serializes to identical historical bytes/hash.

- [ ] **Step 2: Run Shared RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.Shared.EditModeTests.MatchContractTests" \
  -testResults "$PWD/TestResults/GateI-task10-shared-red.xml" \
  -logFile "$PWD/TestResults/GateI-task10-shared-red.log"
```

- [ ] **Step 3: Implement the optional strict record.**

Append optional `AttackDefenseAuthority` after the existing Gate H organization
field in canonical event order. Existing constructors delegate `null`. Validate
finite values, sorted/distinct candidate/responsibility identities, allowed phase/
class/coverage strings, hash identities, actor/action alignment, and tool recovery
links. Do not add commands or runtime AI types to Shared.

- [ ] **Step 4: Write Match RED mapper and event-ownership tests.**

Extend `ReplayContactEvent`:

```csharp
public GateISetIntentReceiptV3 GateISetIntentAuthority { get; }
public AttackDefenseAuthorityReceipt AttackDefenseAuthority { get; }
```

Pass a real SetIntent receipt and a real post-Set command receipt through separate
`CreateContactRecordV4` cases. Assert exact set target/preparation, candidate,
threat, defense, envelope, sample, trajectory, recovery, coverage, and ability
consumption identities. Add recorder tests that invalidate a new formal Set
missing its SetIntent receipt and a new formal Attack/Block/Defense/recovery event
missing its command receipt. Reject events that carry both Gate I receipt kinds.

- [ ] **Step 5: Map accepted-contact evidence.**

Take/remove the pending receipt at accepted contact before coverage or replan
mutates coordinator state. For Set, the recorder maps only
`replayEvent.GateISetIntentAuthority`; for post-Set contacts, it maps only
`replayEvent.AttackDefenseAuthority`. Both map into the one optional Shared
`ReplayAttackDefenseAuthorityRecordV4` shape, but the Set record has phase
`SetIntentPlanned` and no selected attack candidate. For Set, preserve both
`OrganizationAuthority` and the mapped Gate I SetIntent authority.
Represent a physical defensive dig with its existing technique action plus a Gate I
`Defense` responsibility/phase.

- [ ] **Step 6: Run GREEN.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.Shared.EditModeTests.MatchContractTests|Volleyball.EditModeTests.MatchReplayV4Tests" \
  -testResults "$PWD/TestResults/GateI-task10-replay-edit-green.xml" \
  -logFile "$PWD/TestResults/GateI-task10-replay-edit-green.log"
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests" \
  -testResults "$PWD/TestResults/GateI-task10-replay-play-green.xml" \
  -logFile "$PWD/TestResults/GateI-task10-replay-play-green.log"
```

- [ ] **Step 7: Commit.**

```bash
git add Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs \
  Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs \
  Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs
git commit -m "feat: persist gate i replay authority evidence"
```

## Task 11: Complete the Scenario Matrix, Review, and Close Gate I

**Files:**

- Modify: `Assets/Volleyball/Match/Tests/EditMode/CurrentAbilityBenchmarkTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`
- Modify: `docs/changes/2026-07-27-001-full-rally-v4-gate-i-attack-defense-reorganization-authority.md`
- Modify: `docs/changes/README.md`
- Modify: `docs/development.md`
- Modify: `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

- [ ] **Step 1: Add explicit fixed-seed scenario tests.**

Create named fixtures for:

1. A-set reliable power route;
2. poor-set legal survival action;
3. complementary line/cross block-floor coverage;
4. hidden final-route defense invariance;
5. successful block-tool home rebound, non-attacker save, and reorganization;
6. tool rejection without teammate/remaining touch/exit;
7. ordinary block rebound to either side and correct fresh touch sequence;
8. incidental contact with explicit coverage/replan;
9. committed jump/block/contact continuity with no teleport;
10. recorder off/on identical Gate I authority fingerprints;
11. independent fixed-seed canonical Replay byte/hash stability;
12. legacy 3v3 unchanged and Gate I disabled.

Each fixture asserts ordered revisions/source sequences, selected actors/classes,
fallback comparisons, V3 transitions, accepted contacts, score/result termination,
zero duplicate writer trace, and bounded movement correction.

- [ ] **Step 2: Run the complete focused EditMode set.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.AttackDefensePlanV3Tests|Volleyball.EditModeTests.AttackDefensePlannerTests|Volleyball.EditModeTests.JointDefensePlannerTests|Volleyball.EditModeTests.BlockToolRecoveryPlannerTests|Volleyball.EditModeTests.AttackDefenseAuthorityCoordinatorTests|Volleyball.EditModeTests.AttackDefenseAuthorityControllerTests|Volleyball.EditModeTests.AttackRouteSelectorTests|Volleyball.EditModeTests.BlockUnitPlannerTests|Volleyball.EditModeTests.SetQualityAssessmentTests|Volleyball.EditModeTests.Stage2AbilityEnvelopeTests|Volleyball.EditModeTests.CurrentAbilityBenchmarkTests|Volleyball.EditModeTests.MatchReplayV4Tests|Volleyball.EditModeTests.SharedBoundaryTests|Volleyball.Shared.EditModeTests.MatchContractTests" \
  -testResults "$PWD/TestResults/GateI-focused-edit-green.xml" \
  -logFile "$PWD/TestResults/GateI-focused-edit-green.log"
```

- [ ] **Step 3: Run all affected PlayMode tests.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
"Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests|Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests|Volleyball.PlayModeTests.BlockImpactFeedbackPlayModeTests" \
  -testResults "$PWD/TestResults/GateI-focused-play-green.xml" \
  -logFile "$PWD/TestResults/GateI-focused-play-green.log"
```

- [ ] **Step 4: Run static authority scans.**

```bash
rg -n \
"SelectGeometricSetTarget|AttackRouteSelector\\.Select|PreparePhysicalBlock|TrySelectCoverPlayer|_awaitingPostBlockCrossing" \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs

rg -n \
"PhysicalMatchRallyDirector|PrototypePlayerAgent|UnityEngine|MatchReplayRecorder" \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority \
  Assets/Volleyball/Match/Runtime/AI/AttackDefense*.cs \
  Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs \
  Assets/Volleyball/Match/Runtime/AI/BlockToolRecoveryPlanner.cs

rg -n "CourtAwareness" \
  Assets/Volleyball/Match/Runtime/AI/AttackDefense*.cs \
  Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs \
  Assets/Volleyball/Match/Runtime/AI/BlockToolRecoveryPlanner.cs
```

Expected: first scan finds only explicit 3v3 legacy branches/helpers and no formal
selection; second and third scans return no matches.

- [ ] **Step 5: Run complete suites from one implementation HEAD.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/GateI-final-editmode.xml" \
  -logFile "$PWD/TestResults/GateI-final-editmode.log"
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/GateI-final-playmode.xml" \
  -logFile "$PWD/TestResults/GateI-final-playmode.log"
```

Record exact totals/durations; do not copy Gate H evidence.

- [ ] **Step 6: Run fresh determinism and legacy scans.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
"Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests.Formal6v6_GateIAuthorityIsRecorderInvariant|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests.Capture_TwoIndependentGateIFixedSeedRunsAreByteStable" \
  -testResults "$PWD/TestResults/GateI-final-determinism.xml" \
  -logFile "$PWD/TestResults/GateI-final-determinism.log"
rg -n \
"PlayerAbilitySnapshotV[123]|MatchContextV[12]|MatchResultV[12]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'
git diff --check
```

- [ ] **Step 7: Perform one combined review.**

Review `ce16e69..HEAD` against the confirmed design with focus on:

- formal legacy/Gate I duplicate writers;
- threat/final-route or future-sample leakage;
- stale lifecycle and committed-action cancellation;
- power/fallback/tool-recovery qualification;
- V4 ability consumption identity;
- event-owned Replay/canonical backward compatibility;
- 3v3 and Gate J/K scope leakage;
- missing scenario evidence.

If the chosen execution mode permits an independent reviewer, give it this exact
scope and use `gpt-5.6-terra`, medium. Otherwise perform and document a combined
inline review without claiming independence. Every Critical/Important finding
requires a new focused RED regression and GREEN re-check.

- [ ] **Step 8: Finalize documentation.**

Mark Gate I complete and Gate J–K pending. Record:

- implementation commit range;
- Unity `6000.0.43f1`;
- exact baseline/final/focused/determinism XML totals and durations;
- static scan results;
- review findings and whether review was independent;
- manual, Windows, and performance checks not run;
- rollback instruction that treats Shared replay, controller, coordinator, and
  formal cutover as one compatibility unit.

- [ ] **Step 9: Commit documentation.**

```bash
git add docs/changes/2026-07-27-001-full-rally-v4-gate-i-attack-defense-reorganization-authority.md \
  docs/changes/README.md \
  docs/development.md \
  docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md
git commit -m "docs: complete gate i verification"
```

- [ ] **Step 10: Verify the final committed tree.**

```bash
git status --short
git diff --check HEAD^ HEAD
git log --oneline --decorate ce16e69..HEAD
```

Expected: clean status, no diff-check output, only intentional Gate I commits.

## Final review checklist

- [ ] Gate I enables only for formal 6v6 V3 Authority with Gate H handoff.
- [ ] Formal set target, attack, joint defense, tool recovery, and direct
  reorganization each have one writer.
- [ ] Threat → defense → final choice runs once per opportunity.
- [ ] Public threat contains no hidden final route or future sample.
- [ ] Power routes fail error-aware legality before scoring.
- [ ] All fallback actions compete in one comparable pool.
- [ ] Tool recovery requires legal block contact, home rebound, non-attacker
  continuation, remaining touch, and reorganization exit.
- [ ] Floor defense covers residual threat rather than duplicating the block.
- [ ] V3 rules remain contact/lineup/touch/score authority.
- [ ] Committed actions cannot be canceled or teleported by replan.
- [ ] Attack/Set/SoftTouch/Block/Defense axes have unique fixed-key consumers.
- [ ] Gate G facade consumes the exact selected envelope/sample/trajectory.
- [ ] Set preserves both Gate H and Gate I event-owned evidence.
- [ ] Historical Replay V4 bytes/hash remain stable without Gate I evidence.
- [ ] 3v3 remains legacy; Gate J perception and Gate K slimming do not leak in.
- [ ] Complete EditMode, PlayMode, determinism, static scans, and combined review
  have no unresolved blocker.
