# Full Rally V4 Gate H Receive and Organization Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the formal 6v6 responsibility plan the sole authority for receive, setter preparation, emergency takeover, organization, coverage/replan, and attack preparation while preserving V3 rules, Gate G execution identities, legacy 3v3, and deterministic Replay V4.

**Architecture:** Keep the Gate F plan domain immutable and command-free, enrich it with explicit receive/organization responsibilities, and put pure selection plus lifecycle state in Match AI. A presentation-side authority controller converts approved plans into preflighted Gate G facade commands; `PhysicalMatchRallyDirector` supplies immutable physical/rules facts and retains only the Gate I attack handoff. Replay V4 adds an optional canonical organization-authority record that is mandatory for new formal Receive/Set events but absent from older V4 payloads.

**Tech Stack:** Unity 6000.0.43f1, C#, NUnit EditMode/PlayMode, `Volleyball.Match.Domain`, `Volleyball.Match.AI`, `Volleyball.Match.Presentation`, `Volleyball.Shared`, strict canonical JSON/SHA-256 Replay V4.

---

## Scope and Validation Level

This is one vertical authority slice, not several independent projects: the plan
domain, coordinator, player-command adapter, director cutover, and replay evidence
must agree on the same revision and execution identities to produce working
software.

Validation is escalated because the change crosses live runtime authority,
lifecycle ordering, canonical serialization/backward reading, and more than three
modules. Use focused RED/GREEN tests for each task, the affected formal PlayMode
fixtures, both complete Unity suites, fixed-seed recorder invariance, one
independent combined review, and a focused re-review of any Important finding.

Do not use subagents unless the user explicitly requests them. If that changes,
all implementation/review agents must use `gpt-5.6-terra` with medium reasoning.

## File Structure

### Create

- `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority/ReceiveOrganizationPlanV3.cs`
  Immutable task/branch/fallback/reachability values attached to a team plan.
- `Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationResponsibilityPlanner.cs`
  Pure receive and organization selection using the existing scorer,
  `SetterOrganizationZone`, eligibility, and stable ordering.
- `Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationAuthorityCoordinator.cs`
  Pure phase/revision/coverage state machine and immutable command batches.
- `Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs`
  Formal-only adapter that preflights and commits coordinator batches to Gate G
  player facades with exact V4 evidence.
- `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationPlanV3Tests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationResponsibilityPlannerTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityCoordinatorTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityControllerTests.cs`
- `docs/changes/2026-07-26-002-full-rally-v4-gate-h-receive-organization-authority.md`

Unity must generate and commit the `.meta` file for every new `.cs` file.

### Modify

- `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/TeamRallyPlanV3.cs`
  Carry optional Gate H responsibilities without adding command dependencies.
- `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/DeterministicRallyPlanComposerV3.cs`
  Accept an enriched responsibility value when composing an authority revision.
- `Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs`
  Expose deterministic candidate ordering/reach margins without owning authority.
- `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
  Delegate the formal Gate H slice and remove its receive/organization writers.
- `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
  Map exact authority evidence from accepted contacts.
- `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
  Add strict organization-authority values, validation, parser, canonical writer,
  and hash coverage.
- `Assets/Volleyball/Match/Tests/EditMode/DeterministicRallyPlanComposerV3Tests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs`
- `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`
- `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`
- `docs/changes/README.md`
- `docs/development.md`
- `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

## Task 0: Verify the Gate G Baseline in This Isolated Worktree

**Files:**

- Create: `docs/changes/2026-07-26-002-full-rally-v4-gate-h-receive-organization-authority.md`
- Modify: `docs/changes/README.md`

- [ ] **Step 1: Confirm the branch and clean checkout.**

```bash
test "$(git branch --show-current)" = \
  "codex/full-rally-v4-gate-h-receive-organization-authority"
test -z "$(git status --short)"
test "$(git merge-base HEAD 2d22d9d)" = \
  "2d22d9dd29da498ffaa1ff0c9ce341c177fcc215"
```

Expected: every command exits `0`; the checkout has no uncommitted files.

- [ ] **Step 2: Run the complete Gate G EditMode baseline.**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/GateH-baseline-editmode.xml" \
  -logFile "$PWD/TestResults/GateH-baseline-editmode.log"
```

Expected: exit `0`, `590/590` passed, zero failed/skipped/inconclusive.

- [ ] **Step 3: Run the complete Gate G PlayMode baseline.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/GateH-baseline-playmode.xml" \
  -logFile "$PWD/TestResults/GateH-baseline-playmode.log"
```

Expected: exit `0`, `30/30` passed, zero failed/skipped/inconclusive. If either
baseline differs, stop and report the exact XML counts before implementation.

- [ ] **Step 4: Open the required in-progress cross-module change record.**

Create the record from `docs/changes/TEMPLATE.md` with:

```text
编号: CHG-20260726-002
状态: 进行中
负责人: Shared / Match / Replay / Docs
影响模块: Shared / Match / Replay / Docs
交互级别: 跨模块（重点）
分支: codex/full-rally-v4-gate-h-receive-organization-authority
关联提交或 PR: 818ac52（设计）
```

State that Shared provides an optional canonical Replay V4 organization record,
Match produces and consumes live authority evidence, old V4 replay stays readable,
and Career/Bootstrap require no code changes. Add the new record as the first index
row in `docs/changes/README.md`; leave all verification checkboxes unchecked.

- [ ] **Step 5: Commit the in-progress record before production changes.**

```bash
git add \
  docs/changes/2026-07-26-002-full-rally-v4-gate-h-receive-organization-authority.md \
  docs/changes/README.md
git commit -m "docs: start gate h change record"
```

## Task 1: Add Immutable Gate H Responsibility Values

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority/ReceiveOrganizationPlanV3.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationPlanV3Tests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/TeamRallyPlanV3.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/DeterministicRallyPlanComposerV3.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/DeterministicRallyPlanComposerV3Tests.cs`

- [ ] **Step 1: Write failing immutable-contract tests.**

Add tests that construct one Home responsibility set and assert exact ordering,
setter identity, two emergency receivers, backup organizers, attack preparation,
and rejection of duplicates/off-court players:

```csharp
[Test]
public void Responsibilities_PreserveDeclaredAuthorityOrder()
{
    var value = new ReceiveOrganizationPlanV3(
        TeamSide.Home,
        revision: 4,
        primaryReceiver: Id("home-6"),
        registeredSetter: Id("home-1"),
        emergencyReceivers: new[] { Id("home-4"), Id("home-5") },
        backupOrganizers: new[] { Id("home-2"), Id("home-3") },
        attackPreparation: Id("home-4"),
        organizationTarget: new SimVector3(1.5f, 0f, -1.1f));

    Assert.That(value.Revision, Is.EqualTo(4));
    Assert.That(value.EmergencyReceivers.Select(id => id.Value),
        Is.EqualTo(new[] { "home-4", "home-5" }));
    Assert.That(value.BackupOrganizers.Select(id => id.Value),
        Is.EqualTo(new[] { "home-2", "home-3" }));
    Assert.That(value.RegisteredSetter.Value, Is.EqualTo("home-1"));
}

[Test]
public void TeamPlan_RejectsResponsibilityForPlayerOutsideEligibility()
{
    var snapshot = CreateSnapshot();
    var responsibility = Responsibilities(primaryReceiver: Id("bench-player"));

    Assert.That(
        () => new TeamRallyPlanV3(
            TeamSide.Home,
            Assignments("home", 6),
            Array.Empty<string>(),
            snapshot.Eligibility,
            responsibility),
        Throws.ArgumentException);
}
```

- [ ] **Step 2: Run the new tests and verify RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.ReceiveOrganizationPlanV3Tests|Volleyball.EditModeTests.DeterministicRallyPlanComposerV3Tests" \
  -testResults "$PWD/TestResults/GateH-task1-red.xml" \
  -logFile "$PWD/TestResults/GateH-task1-red.log"
```

Expected: compile failure because `ReceiveOrganizationPlanV3` and the new
`TeamRallyPlanV3` overload do not exist.

- [ ] **Step 3: Implement the immutable values and validation.**

Create these exact public value types:

```csharp
namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum OrganizationFallbackReasonV3
    {
        None,
        SetterPreviousTouch,
        SetterUnavailable,
        SetterIllegal,
        SetterUnreachable,
        NoLegalOrganizer
    }

    public sealed class ReceiveOrganizationPlanV3
    {
        public ReceiveOrganizationPlanV3(
            TeamSide side,
            long revision,
            PlayerId primaryReceiver,
            PlayerId registeredSetter,
            IReadOnlyList<PlayerId> emergencyReceivers,
            IReadOnlyList<PlayerId> backupOrganizers,
            PlayerId attackPreparation,
            SimVector3 organizationTarget)
        {
            Side = PlayerWorldSnapshotV3.RequireDefinedEnum(side, nameof(side));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision;
            PrimaryReceiver = PlayerWorldSnapshotV3.RequirePlayerId(
                primaryReceiver, nameof(primaryReceiver));
            RegisteredSetter = PlayerWorldSnapshotV3.RequirePlayerId(
                registeredSetter, nameof(registeredSetter));
            AttackPreparation = PlayerWorldSnapshotV3.RequirePlayerId(
                attackPreparation, nameof(attackPreparation));
            if (!organizationTarget.IsFinite)
                throw new ArgumentOutOfRangeException(nameof(organizationTarget));
            OrganizationTarget = organizationTarget;
            EmergencyReceivers = CopyDistinct(
                emergencyReceivers, 0, 2, nameof(emergencyReceivers));
            BackupOrganizers = CopyDistinct(
                backupOrganizers, 0, 5, nameof(backupOrganizers));
            ValidateNoRoleCollision();
        }

        public TeamSide Side { get; }
        public long Revision { get; }
        public PlayerId PrimaryReceiver { get; }
        public PlayerId RegisteredSetter { get; }
        public IReadOnlyList<PlayerId> EmergencyReceivers { get; }
        public IReadOnlyList<PlayerId> BackupOrganizers { get; }
        public PlayerId AttackPreparation { get; }
        public SimVector3 OrganizationTarget { get; }

        private static IReadOnlyList<PlayerId> CopyDistinct(
            IReadOnlyList<PlayerId> source,
            int minimum,
            int maximum,
            string parameterName)
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);
            if (source.Count < minimum || source.Count > maximum)
                throw new ArgumentException(
                    $"Expected {minimum} to {maximum} players.",
                    parameterName);
            var copy = new PlayerId[source.Count];
            var seen = new HashSet<PlayerId>();
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = PlayerWorldSnapshotV3.RequirePlayerId(
                    source[index], parameterName);
                if (!seen.Add(copy[index]))
                    throw new ArgumentException(
                        "Responsibility players must be distinct.",
                        parameterName);
            }
            return new ReadOnlyCollection<PlayerId>(copy);
        }

        private void ValidateNoRoleCollision()
        {
            if (EmergencyReceivers.Contains(PrimaryReceiver))
                throw new ArgumentException(
                    "Primary receiver cannot also be an emergency receiver.");
            if (BackupOrganizers.Contains(RegisteredSetter))
                throw new ArgumentException(
                    "Registered setter cannot also be a backup organizer.");
        }
    }
}
```

Add `System.Collections.Generic`, `System.Collections.ObjectModel`, and
`System.Linq` imports. The same player may be both primary receiver and registered
setter because the previous-touch rule intentionally handles setter-first-contact
rallies.

Add an optional `ReceiveOrganizationPlanV3 receiveOrganization = null` final
constructor argument and read-only property to `TeamRallyPlanV3`. When non-null,
validate the side and every referenced player against `OnCourtEligibilitySnapshot`.
Add a composer overload:

```csharp
public static TeamRallyPlanV3 Compose(
    RallyWorldSnapshotV3 snapshot,
    TeamSide side,
    string trajectoryIdentity,
    ReceiveOrganizationPlanV3 receiveOrganization)
```

The existing three-argument overload delegates with `null`, preserving Gate F
shadow behavior.

- [ ] **Step 4: Run the focused GREEN regression.**

Use the Step 2 command with `GateH-task1-green.xml`.

Expected: all new responsibility and existing Gate F composer tests pass.

- [ ] **Step 5: Commit Task 1.**

```bash
git add \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority/ReceiveOrganizationPlanV3.cs \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority/ReceiveOrganizationPlanV3.cs.meta \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/TeamRallyPlanV3.cs \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/DeterministicRallyPlanComposerV3.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationPlanV3Tests.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationPlanV3Tests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/DeterministicRallyPlanComposerV3Tests.cs
git commit -m "feat: define gate h receive organization plans"
```

## Task 2: Build the Pure Receive and Organization Responsibility Planner

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationResponsibilityPlanner.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationResponsibilityPlannerTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/SetterOrganizationZoneTests.cs`

- [ ] **Step 1: Write failing selection and evidence tests.**

Cover both teams and these exact behaviors:

```csharp
[Test]
public void PlanOrganization_ReachableRegisteredSetterBeatsHigherScoreBackup()
{
    var input = CreateOrganizationInput(
        setterPosition: new SimVector3(1.2f, 0f, -1.8f),
        backupPosition: new SimVector3(1.5f, 0f, -1.1f),
        previousActor: null,
        availableSeconds: 0.7f);

    var result = CreatePlanner().PlanOrganization(
        input,
        CreateAttackPreparationInput(input.Team),
        Eligibility(),
        revision: 9);

    Assert.That(result.Decision.Actor.Role, Is.EqualTo(PlayerRole.Setter));
    Assert.That(result.SetterEvidence.IsReachable, Is.True);
    Assert.That(result.FallbackReason,
        Is.EqualTo(OrganizationFallbackReasonV3.None));
}

[Test]
public void PlanOrganization_PreviousTouchSetterUsesFirstDeclaredLegalBackup()
{
    var input = CreateOrganizationInput(
        setterPosition: SimVector3.Zero,
        backupPosition: SimVector3.Zero,
        previousActor: SetterId(),
        availableSeconds: 1f);

    var result = CreatePlanner().PlanOrganization(
        input,
        CreateAttackPreparationInput(input.Team),
        Eligibility(),
        revision: 10);

    Assert.That(result.Decision.Actor, Is.EqualTo(FirstBackupId()));
    Assert.That(result.FallbackReason,
        Is.EqualTo(OrganizationFallbackReasonV3.SetterPreviousTouch));
}

[Test]
public void PlanOrganization_NoReachableBackupReturnsNoLegalOrganizer()
{
    var input = CreateAllUnreachableOrganizationInput();
    var result = CreatePlanner().PlanOrganization(
        input,
        CreateAttackPreparationInput(input.Team),
        Eligibility(),
        revision: 11);

    Assert.That(result.Decision.HasDecision, Is.False);
    Assert.That(result.FallbackReason,
        Is.EqualTo(OrganizationFallbackReasonV3.NoLegalOrganizer));
}
```

Also assert: receive primary and two emergency receivers use stable feasible
candidate order; Home/Away use mirrored `SetterOrganizationZone` targets; setter
reach margin uses the same `TeamRallyDecisionPlanner` movement/reaction inputs;
off-court and previous-touch candidates are filtered before ordering.

- [ ] **Step 2: Run the planner tests and verify RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.ReceiveOrganizationResponsibilityPlannerTests|Volleyball.EditModeTests.TeamRallyDecisionPlannerTests|Volleyball.EditModeTests.SetterOrganizationZoneTests" \
  -testResults "$PWD/TestResults/GateH-task2-red.xml" \
  -logFile "$PWD/TestResults/GateH-task2-red.log"
```

Expected: compile failure because the responsibility planner/result/evidence
types do not exist.

- [ ] **Step 3: Expose deterministic ordered candidate evidence.**

Add this read-only helper to `TeamRallyDecisionPlanner`; it must reuse `Plan`
scoring and never select a second authority:

```csharp
public IReadOnlyList<RallyDecisionCandidate> OrderedCandidates(
    TeamRallyDecisionInput input)
{
    var decision = Plan(input);
    return decision.Candidates
        .OrderByDescending(candidate => candidate.IsFeasible)
        .ThenByDescending(candidate => candidate.Score.Total)
        .ThenBy(candidate => candidate.Actor.Role)
        .ThenBy(candidate => candidate.Actor.RosterSlot)
        .ToArray();
}
```

Add a regression proving reversed input player order yields the same ordered IDs.

- [ ] **Step 4: Implement the pure responsibility planner.**

Define:

```csharp
public readonly struct SetterReachabilityEvidenceV3
{
    public SetterReachabilityEvidenceV3(
        PlayerId setter,
        bool isAvailable,
        bool isLegal,
        bool wasPreviousTouch,
        bool isReachable,
        float movementMeters,
        float reactionDelaySeconds,
        float reachMarginMeters)
    {
        if (!Enum.IsDefined(typeof(TeamId), setter.Team))
            throw new ArgumentOutOfRangeException(nameof(setter));
        if (!IsFinite(movementMeters) || movementMeters < 0f)
            throw new ArgumentOutOfRangeException(nameof(movementMeters));
        if (!IsFinite(reactionDelaySeconds) || reactionDelaySeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(reactionDelaySeconds));
        if (!IsFinite(reachMarginMeters))
            throw new ArgumentOutOfRangeException(nameof(reachMarginMeters));
        Setter = setter;
        IsAvailable = isAvailable;
        IsLegal = isLegal;
        WasPreviousTouch = wasPreviousTouch;
        IsReachable = isReachable;
        MovementMeters = movementMeters;
        ReactionDelaySeconds = reactionDelaySeconds;
        ReachMarginMeters = reachMarginMeters;
    }

    public PlayerId Setter { get; }
    public bool IsAvailable { get; }
    public bool IsLegal { get; }
    public bool WasPreviousTouch { get; }
    public bool IsReachable { get; }
    public float MovementMeters { get; }
    public float ReactionDelaySeconds { get; }
    public float ReachMarginMeters { get; }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed class ReceiveOrganizationPlanningResult
{
    public ReceiveOrganizationPlanningResult(
        ReceiveOrganizationPlanV3 plan,
        TeamRallyDecision decision,
        TeamRallyDecision attackPreparationDecision,
        SetterReachabilityEvidenceV3 setterEvidence,
        OrganizationFallbackReasonV3 fallbackReason)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Decision = decision ?? throw new ArgumentNullException(nameof(decision));
        AttackPreparationDecision = attackPreparationDecision ??
            throw new ArgumentNullException(nameof(attackPreparationDecision));
        if (!Enum.IsDefined(
                typeof(OrganizationFallbackReasonV3),
                fallbackReason))
            throw new ArgumentOutOfRangeException(nameof(fallbackReason));
        SetterEvidence = setterEvidence;
        FallbackReason = fallbackReason;
    }

    public ReceiveOrganizationPlanV3 Plan { get; }
    public TeamRallyDecision Decision { get; }
    public TeamRallyDecision AttackPreparationDecision { get; }
    public SetterReachabilityEvidenceV3 SetterEvidence { get; }
    public OrganizationFallbackReasonV3 FallbackReason { get; }
}

public sealed class ReceiveOrganizationResponsibilityPlanner
{
    public ReceiveOrganizationPlanningResult PlanReceive(
        TeamRallyDecisionInput receiveInput,
        TeamRallyDecisionInput attackPreparationInput,
        OnCourtEligibilitySnapshot eligibility,
        long revision);

    public ReceiveOrganizationPlanningResult PlanOrganization(
        TeamRallyDecisionInput organizationInput,
        TeamRallyDecisionInput attackPreparationInput,
        OnCourtEligibilitySnapshot eligibility,
        long revision);
}
```

`PlanReceive` uses the existing scorer for the primary, takes the first two other
feasible ordered candidates as emergency receivers, resolves exactly one registered
setter from eligibility, and uses the separate Attack-stage input to choose the
attack-preparation decision/actor/approach. `PlanOrganization` queries the registered
setter candidate first; if feasible and not the previous actor it selects that
candidate regardless of score. Otherwise it chooses the first feasible declared
backup and records one of the exact fallback enum values. It also refreshes attack
preparation from the separate Attack-stage input. If no organizer exists it returns
`TeamRallyDecision.NoDecision` plus `NoLegalOrganizer`; if no attack-preparation
candidate exists it returns `TeamRallyDecision.NoDecision` in
`AttackPreparationDecision` and publishes no preparation command.

Do not duplicate organization coordinates or movement formulas. Read the target
from `SetterOrganizationZone.DefaultWorldTarget`, and obtain movement/reaction/
reach-margin evidence from the same candidate score used for feasibility.

- [ ] **Step 5: Run the focused GREEN regression.**

Use the Step 2 command with `GateH-task2-green.xml`.

Expected: all responsibility planner, existing decision planner, and zone tests
pass.

- [ ] **Step 6: Commit Task 2.**

```bash
git add \
  Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationResponsibilityPlanner.cs \
  Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationResponsibilityPlanner.cs.meta \
  Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationResponsibilityPlannerTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationResponsibilityPlannerTests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs
git commit -m "feat: plan receive and organization responsibilities"
```

## Task 3: Add the Pure Authority Coordinator and Revision State Machine

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationAuthorityCoordinator.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityCoordinatorTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/PlanCoverageDecision.cs`

- [ ] **Step 1: Write failing phase, branch, and stale-revision tests.**

```csharp
[Test]
public void AcceptReceive_AdvancesToOrganizationWithActualLanding()
{
    var coordinator = CreateCoordinator();
    var receive = coordinator.PlanReceive(CreateReceiveRequest(revision: 3));

    var next = coordinator.AcceptReceive(new AcceptedReceiveV3(
        revision: 3,
        receive.PrimaryActor,
        new SimVector3(1.4f, 2.2f, -1.3f),
        PlanCoverageReason.WithinConditionalEnvelope));

    Assert.That(next.Phase,
        Is.EqualTo(ReceiveOrganizationAuthorityPhaseV3.OrganizationPlanned));
    Assert.That(next.ActualFirstPassLanding,
        Is.EqualTo(new SimVector3(1.4f, 2.2f, -1.3f)));
}

[Test]
public void AcceptReceive_RejectsStaleRevisionWithoutPublishingCommands()
{
    var sink = new RecordingAuthorityCommandSink();
    var coordinator = CreateCoordinator(sink);
    coordinator.PlanReceive(CreateReceiveRequest(revision: 7));

    Assert.That(
        () => coordinator.AcceptReceive(AcceptedReceive(revision: 6)),
        Throws.InvalidOperationException);
    Assert.That(sink.PublishedBatches, Is.Empty);
}

[Test]
public void MissPrimary_ActivatesOnlyDeclaredEmergencyBranch()
{
    var coordinator = CreateCoordinator();
    var planned = coordinator.PlanReceive(CreateReceiveRequest(revision: 8));

    var branch = coordinator.ActivateEmergency(
        revision: 8, planned.Plan.EmergencyReceivers[0]);

    Assert.That(branch.ActivatedBranch,
        Is.EqualTo(RallyPlanBranchV3.Contingency));
    Assert.That(
        () => coordinator.ActivateEmergency(8, UndeclaredPlayer()),
        Throws.InvalidOperationException);
}
```

Add cases for duplicate accepted event, committed-action invalidation, bounded
local/scoped replan, terminal `NoLegalOrganizer`, and rejection of global rebuild.

- [ ] **Step 2: Run the coordinator tests and verify RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.ReceiveOrganizationAuthorityCoordinatorTests|Volleyball.EditModeTests.RallyPlanTests" \
  -testResults "$PWD/TestResults/GateH-task3-red.xml" \
  -logFile "$PWD/TestResults/GateH-task3-red.log"
```

Expected: compile failure because coordinator/state/command batch types do not
exist.

- [ ] **Step 3: Implement state and immutable command batches.**

Define these exact phases:

```csharp
public enum ReceiveOrganizationAuthorityPhaseV3
{
    Idle,
    ReceivePlanned,
    ReceiveCommitted,
    OrganizationPlanned,
    OrganizationCommitted,
    HandedOffToAttack,
    Terminal
}
```

Define `ReceiveOrganizationAuthorityStateV3` with read-only phase, revision,
plan, active branch, actual first-pass landing, coverage decision, fallback reason,
committed actor, and command identity. Define command kinds `PrimaryReceive`,
`EmergencyReceive`, `SetterPreparation`, `OrganizationContact`,
`AttackPreparation`, and `CancelUncommitted`.

Define the coordinator boundary with these exact values:

```csharp
public enum ReceiveOrganizationCommandKind
{
    PrimaryReceive,
    EmergencyReceive,
    SetterPreparation,
    OrganizationContact,
    AttackPreparation,
    CancelUncommitted
}

public sealed class ReceiveOrganizationAuthorityCommand
{
    public long PlanRevision { get; }
    public long SourceSequence { get; }
    public ReceiveOrganizationCommandKind Kind { get; }
    public PlayerId Actor { get; }
    public RallyPlanBranchV3 Branch { get; }
    public TeamRallyDecision Decision { get; }
    public bool IsCommitted { get; }
}

public sealed class ReceiveOrganizationAuthorityEvidenceV3
{
    public long PlanRevision { get; }
    public long SourceSequence { get; }
    public ReceiveOrganizationAuthorityPhaseV3 Phase { get; }
    public ReceiveOrganizationPlanV3 Plan { get; }
    public SetterReachabilityEvidenceV3 SetterEvidence { get; }
    public OrganizationFallbackReasonV3 FallbackReason { get; }
    public PlanCoverageDecision CoverageDecision { get; }
    public SimVector3? ActualFirstPassLanding { get; }
}

public sealed class ReceiveOrganizationCommandBatch
{
    public long PlanRevision { get; }
    public long SourceSequence { get; }
    public IReadOnlyList<ReceiveOrganizationAuthorityCommand> Commands { get; }
    public ReceiveOrganizationAuthorityEvidenceV3 Evidence { get; }
}

public interface IReceiveOrganizationAuthorityCommandSink
{
    void Publish(ReceiveOrganizationCommandBatch batch);
}
```

Give both classes validating constructors that reject negative revisions,
non-positive source sequences, null plan/coverage/commands, undefined enums, and
commands whose revision differs from the batch. `AcceptedReceiveV3` is an immutable
value containing revision, source sequence, actor, actual first-pass landing, V3
coverage reason, accepted trajectory identity, and accepted
execution-classification identity. Its constructor rejects non-finite landing
coordinates, invalid revision/source sequence, empty identities, and undefined
reason values.

`ReceiveOrganizationAuthorityCoordinator` must:

- require monotonically increasing revisions;
- publish one fully validated immutable batch per transition;
- reject stale/duplicate/incompatible events before publishing;
- activate only declared emergency actors;
- preserve committed commands on invalidation;
- map `WithinConditionalEnvelope` to declared branch activation,
  `ResponsibleActorChanged` to local revision,
  `BallEnvelopeExceeded` to scoped replan, and no organizer/rally end to terminal;
- reject `EnvelopeExceeded` as outside Gate H rather than silently performing a
  global tactical rebuild.

- [ ] **Step 4: Run focused GREEN tests.**

Use the Step 2 command with `GateH-task3-green.xml`.

Expected: all state-machine and existing plan coverage tests pass.

- [ ] **Step 5: Commit Task 3.**

```bash
git add \
  Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationAuthorityCoordinator.cs \
  Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationAuthorityCoordinator.cs.meta \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/PlanCoverageDecision.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityCoordinatorTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityCoordinatorTests.cs.meta
git commit -m "feat: coordinate gate h authority revisions"
```

## Task 4: Commit Authority Commands Through the Gate G Facade

**Files:**

- Create: `Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityControllerTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/PlayerTechniqueExecutorTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [ ] **Step 1: Write failing preflight, identity, and rollback tests.**

```csharp
[Test]
public void CommitReceive_StoresTheExactV4EvidenceOnTheFacade()
{
    var fixture = CreateControllerFixture();
    var command = fixture.ValidReceiveBatch();

    var receipt = fixture.Controller.PreflightAndCommit(command);

    Assert.That(
        fixture.Primary.ScheduledExecutionEnvelopeV4,
        Is.SameAs(receipt.ExecutionClassification.ExecutableEnvelope));
    Assert.That(
        fixture.Primary.ScheduledExecutionSampleV4,
        Is.SameAs(receipt.ExecutionClassification.ExecutableSample));
    Assert.That(receipt.PlanRevision, Is.EqualTo(command.PlanRevision));
}

[Test]
public void PreflightFailure_LeavesEveryPlayerUnscheduled()
{
    var fixture = CreateControllerFixture();
    var command = fixture.BatchWithUndeclaredEmergencyActor();

    Assert.That(
        () => fixture.Controller.PreflightAndCommit(command),
        Throws.InvalidOperationException);
    Assert.That(fixture.Players.All(player =>
        player.ScheduledExecutionEnvelopeV4 == null), Is.True);
    Assert.That(fixture.Players.All(player =>
        !player.EmergencyReceiveWindowEnabled), Is.True);
}
```

Add tests that committed contact cannot be canceled by a stale revision; attack
preparation and setter preparation use the declared actors; formal commands reject
the legacy target-velocity overload; receive and set consume only their V4
envelope fields.

- [ ] **Step 2: Run the controller tests and verify RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.ReceiveOrganizationAuthorityControllerTests|Volleyball.EditModeTests.PlayerTechniqueExecutorTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests" \
  -testResults "$PWD/TestResults/GateH-task4-red.xml" \
  -logFile "$PWD/TestResults/GateH-task4-red.log"
```

Expected: compile failure because the authority controller and receipt do not
exist.

- [ ] **Step 3: Add a facade preflight API without changing scheduling behavior.**

Add:

```csharp
public void ValidateV4Schedule(
    TechniqueAction action,
    ExecutionSampleClassificationV4 classification,
    AttackApproachPlan? attackApproach = null,
    AttackContactPlan? attackContactPlan = null)
{
    ValidateScheduleContactArguments(action, attackApproach, attackContactPlan);
    PlayerTechniqueExecutor.ValidateV4(classification);
}
```

`PlayerTechniqueExecutor.ValidateV4` contains the same non-mutating validation
used by `ScheduleV4`: accepted/expanded kind, non-null executable envelope/sample,
matching envelope identity and matching candidate category. `ScheduleV4` calls
the validator before changing fields.

- [ ] **Step 4: Implement the formal authority controller.**

The controller owns no tactical scorer. Its `PreflightAndCommit` method:

1. resolves every actor from an owned formal six-player map;
2. verifies revision, declared actor, times, targets, windows, trajectory identity,
   and every V4 classification;
3. computes no new sample after preflight;
4. calls facade methods only after the entire batch passes;
5. wraps mutation in `try/catch`; if a facade call throws, cancels every
   uncommitted command already applied from that batch, then rethrows;
6. returns a receipt containing plan/source revision, selected actor, exact
   envelope/sample/classification, trajectory artifact, organization evidence,
   and coverage decision.

Use:

```csharp
public sealed class ReceiveOrganizationAuthorityReceipt
{
    public long PlanRevision { get; }
    public long SourceSequence { get; }
    public ReceiveOrganizationCommandKind Kind { get; }
    public PlayerId Actor { get; }
    public TechniqueAction Action { get; }
    public ExecutionSampleClassificationV4 ExecutionClassification { get; }
    public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
    public ReceiveOrganizationAuthorityEvidenceV3 Evidence { get; }
}
```

Primary Receive and Organization Contact call the V4 `ScheduleContact` overload.
Emergency windows call `EnableEmergencyReceiveWindow` only for declared actors.
Setter/attack preparation call `ScheduleSetPreparation` and
`ScheduleAttackPreparation`. Cancellation calls `CancelScheduledContact` only for
uncommitted commands from the exact invalidated revision.

- [ ] **Step 5: Run focused GREEN tests.**

Use the Step 2 command with `GateH-task4-green.xml`.

Expected: controller, technique executor, and facade regressions pass.

- [ ] **Step 6: Commit Task 4.**

```bash
git add \
  Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs \
  Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs.meta \
  Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityControllerTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityControllerTests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/PlayerTechniqueExecutorTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "feat: execute gate h plans through player facades"
```

## Task 5: Cut Formal 6v6 Receive and Organization Authority Over

**Files:**

- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`

- [ ] **Step 1: Write failing director-boundary and formal-flow tests.**

Add reflection/source assertions that formal Receive/Organize no longer call
director-owned `PrepareSetterForReceive`, `PrepareAttackerForReceive`, emergency
candidate loops, or any Organize-stage `PlanDecision` call. Add a formal PlayMode
trace:

```csharp
[UnityTest]
public IEnumerator FormalReceiveAndOrganization_UseOnePlanAuthorityWriter()
{
    yield return SceneManager.LoadSceneAsync(
        "FormalIndoor6v6", LoadSceneMode.Single);
    var director = Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
    Assert.That(director, Is.Not.Null);
    var traces = new List<ReceiveOrganizationAuthorityReceipt>();
    director.ReceiveOrganizationAuthorityCommitted += traces.Add;

    var timeout = Time.realtimeSinceStartup + 90f;
    while (!traces.Any(trace =>
               trace.Kind ==
               ReceiveOrganizationCommandKind.OrganizationContact) &&
           Time.realtimeSinceStartup < timeout)
    {
        yield return null;
    }

    Assert.That(traces.Select(trace => trace.PlanRevision),
        Is.Ordered.Ascending);
    Assert.That(traces.Count(trace =>
        trace.Kind == ReceiveOrganizationCommandKind.PrimaryReceive),
        Is.EqualTo(1));
    Assert.That(traces.Count(trace =>
        trace.Kind == ReceiveOrganizationCommandKind.OrganizationContact),
        Is.EqualTo(1));
    Assert.That(director.GateHLegacyWriterInvocations, Is.Zero);
}
```

In the 3v3 fixture assert `GateHAuthorityEnabled == false` and the public bootstrap
still completes.

- [ ] **Step 2: Run the boundary/EditMode portion and verify RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.SharedBoundaryTests|Volleyball.EditModeTests.ReceiveOrganizationAuthorityControllerTests" \
  -testResults "$PWD/TestResults/GateH-task5-edit-red.xml" \
  -logFile "$PWD/TestResults/GateH-task5-edit-red.log"
```

Expected: boundary assertions fail because the legacy director writers remain.

- [ ] **Step 3: Integrate the formal-only authority path.**

During `InitializeV4`, create the planner/coordinator/controller only when:

```csharp
GateHAuthorityEnabled =
    _configuration.RosterSize == 6 &&
    _matchContext != null &&
    _v3RulesAdapter != null;
```

This is a formal-boundary predicate, not a runtime feature flag. For formal
possessions:

- `ScheduleReceiveDecision` builds the snapshot/input once and asks the coordinator
  for the Receive batch;
- accepted Receive passes actual landing/actor/V3 facts to the coordinator and
  commits its Organization batch;
- contact timeout/alternate actor passes the bounded coverage reason;
- accepted Set completes Gate H state and calls the existing attack handoff.

Delete the formal use of:

- `PrepareSetterForReceive`;
- `PrepareAttackerForReceive`;
- the receive emergency-candidate scheduling loop;
- any `PlanDecision` call with `RallyDecisionStage.Organize` after accepted Receive;
- organization fallback ordering inside the director.

Keep the legacy branch only under `!GateHAuthorityEnabled` for 3v3. Do not add a
configuration toggle.

- [ ] **Step 4: Run focused EditMode GREEN.**

Use the Step 2 command with `GateH-task5-edit-green.xml`.

Expected: boundary and authority-controller tests pass.

- [ ] **Step 5: Run the focused formal and 3v3 PlayMode tests.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
"Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests.FormalReceiveAndOrganization_UseOnePlanAuthorityWriter|Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests.Formal6v6_ReplayDiagnosticRecordingPreservesFixedSeedAuthority|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests.PhysicalLoop_UsesSixPlayersOneBallAndSwitchableCameras" \
  -testResults "$PWD/TestResults/GateH-task5-play-green.xml" \
  -logFile "$PWD/TestResults/GateH-task5-play-green.log"
```

Expected: all three pass; formal traces use Gate H and 3v3 remains legacy.

- [ ] **Step 6: Commit Task 5.**

```bash
git add \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs
git commit -m "feat: authorize formal receive organization plans"
```

## Task 6: Add Canonical Replay V4 Organization Authority Evidence

**Files:**

- Modify: `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`

- [ ] **Step 1: Write strict contract, hash, and backward-read RED tests.**

Construct a native Receive event with:

```csharp
var authority = new ReplayOrganizationAuthorityRecordV4(
    planRevision: 7,
    sourceSequenceNumber: 3,
    authorityPhase: "Receive",
    organizationTarget: new ReplayVector3V4(1.5f, 0f, -1.1f),
    actualFirstPassLanding: null,
    zoneGrade: "Best",
    registeredSetterPlayerId: "home-setter",
    setterStatus: "Reachable",
    setterMovementMeters: 1.2f,
    setterReactionDelaySeconds: 0.04f,
    setterReachMarginMeters: 0.3f,
    organizerPlayerId: "home-setter",
    fallbackReason: "None",
    activatedBranch: "Primary",
    testedEnvelopeIdentity: envelope.Identity,
    executableEnvelopeIdentity: envelope.Identity,
    sampleEnvelopeIdentity: envelope.Identity,
    trajectoryArtifactIdentity: trajectory.ArtifactIdentity,
    coverage: CoveredReplayCoverage());
```

Assert:

- canonical round-trip preserves every field and byte order;
- changing plan revision, fallback, reach evidence, branch, envelope identity, or
  coverage changes `ReplayHash`;
- Receive/Set actor/envelope/trajectory mismatches reject;
- old V4 JSON without `organizationAuthority` still deserializes and reserializes
  under its historical canonical mode;
- the formal recorder refuses to complete a new Receive/Set capture when its
  event-owned authority evidence is missing;
- non-Receive/Set events reject the authority record.

- [ ] **Step 2: Run strict replay tests and verify RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.MatchReplayV4Tests|Volleyball.Shared.EditModeTests.MatchContractTests" \
  -testResults "$PWD/TestResults/GateH-task6-red.xml" \
  -logFile "$PWD/TestResults/GateH-task6-red.log"
```

Expected: compile failure because Replay organization authority types and event
properties do not exist.

- [ ] **Step 3: Implement strict Shared values and event validation.**

Add immutable Shared values for replay vectors and organization authority. Use
strict string sets:

```text
authorityPhase: Receive | Organize
zoneGrade: Best | Secondary | Poor
setterStatus: Reachable | PreviousTouch | Unavailable | Illegal | Unreachable
fallbackReason: None | SetterPreviousTouch | SetterUnavailable |
                SetterIllegal | SetterUnreachable | NoLegalOrganizer
activatedBranch: Primary | Contingency | null
```

Add `ReplayOrganizationAuthorityRecordV4 OrganizationAuthority` to
`MatchReplayEventV4` as the final constructor argument after `Shadow`. Preserve
the existing constructor overloads by delegating with `null`.

Validation must require:

- actor/organizer equality for Set;
- event kind Receive or Set;
- tested/executable/sample/trajectory identities equal the event-owned records;
- source sequence positive and plan revision non-negative;
- actual first-pass landing absent for Receive and present for Set;
- `None` fallback only with registered setter organizer;
- `NoLegalOrganizer` has no organizer and cannot appear on an accepted Set event.

- [ ] **Step 4: Extend strict parsing and canonical writing.**

Add optional property `organizationAuthority` after `shadow` in the current
canonical event order. Parsing accepts its absence for historical V4 payloads.
The current writer emits it when non-null. Include every behavior field in the
existing `volleyball.match-replay.v4` hash input; do not change contract or replay
version numbers.

Because the canonical writer omits null optional properties, old payloads without
the property retain their historical bytes and stored hash without a new mode
flag.

- [ ] **Step 5: Run strict replay GREEN tests.**

Use the Step 2 command with `GateH-task6-green.xml`.

Expected: all Shared and Match replay contract tests pass.

- [ ] **Step 6: Commit Task 6.**

```bash
git add \
  Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs \
  Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs \
  Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs
git commit -m "feat: persist gate h replay authority evidence"
```

## Task 7: Map Event-Owned Gate H Evidence Into the Recorder

**Files:**

- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`

- [ ] **Step 1: Write mapping and event-ownership RED tests.**

Add an EditMode mapper test that passes a real Gate H receipt through
`ReplayContactEvent` and verifies exact plan/envelope/sample/trajectory/coverage
identity. Add PlayMode assertions:

```csharp
foreach (var replayEvent in restored.Events
    .Where(value => value.EventKind is "Receive" or "Set"))
{
    Assert.That(replayEvent.OrganizationAuthority, Is.Not.Null);
    Assert.That(
        replayEvent.OrganizationAuthority.ExecutableEnvelopeIdentity,
        Is.EqualTo(replayEvent.ExecutableEnvelope.Identity));
    Assert.That(
        replayEvent.OrganizationAuthority.TrajectoryArtifactIdentity,
        Is.EqualTo(replayEvent.Trajectory.ArtifactIdentity));
}
```

Add recorder-on/off comparison of ordered Gate H authority fingerprints.

- [ ] **Step 2: Run mapper/replay fixtures and verify RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.MatchReplayV4Tests" \
  -testResults "$PWD/TestResults/GateH-task7-edit-red.xml" \
  -logFile "$PWD/TestResults/GateH-task7-edit-red.log"
```

Expected: mapper assertion fails because `ReplayContactEvent` does not carry Gate H
evidence.

- [ ] **Step 3: Make accepted-contact evidence event-owned.**

Extend `ReplayContactEvent` with:

```csharp
public ReceiveOrganizationAuthorityReceipt OrganizationAuthority { get; }
```

At accepted Receive/Set, snapshot the receipt before any follow-up plan mutates
coordinator state. Do not let the recorder read the director's current/latest
authority state.

Map the receipt to `ReplayOrganizationAuthorityRecordV4` in
`MatchReplayRecorder.CreateContactRecordV4`, using the event's exact envelope,
sample, trajectory, actor, actual landing, setter evidence, fallback, branch, and
coverage.

- [ ] **Step 4: Run focused EditMode GREEN.**

Use the Step 2 command with `GateH-task7-edit-green.xml`.

Expected: mapper and canonical replay tests pass.

- [ ] **Step 5: Run formal replay PlayMode GREEN.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
"Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests.Formal6v6_ReplayDiagnosticRecordingPreservesFixedSeedAuthority" \
  -testResults "$PWD/TestResults/GateH-task7-play-green.xml" \
  -logFile "$PWD/TestResults/GateH-task7-play-green.log"
```

Expected: all formal replay and recorder-invariance fixtures pass.

- [ ] **Step 6: Commit Task 7.**

```bash
git add \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs \
  Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs
git commit -m "feat: record event-owned gate h authority"
```

## Task 8: Complete the Formal Gate H Scenario Matrix and Boundaries

**Files:**

- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/CurrentAbilityBenchmarkTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs`

- [ ] **Step 1: Add missing fixed-seed scenarios as RED tests.**

Create explicit deterministic fixtures for:

1. normal in-zone receive to registered setter;
2. displaced-but-reachable registered setter;
3. setter previous first touch to declared backup;
4. unreachable setter to declared backup;
5. no legal organizer preserving contact-timeout/save/loss;
6. emergency receive only from a declared branch;
7. plan-owned attack preparation followed by the existing Gate I attack seam.

Each scenario must assert ordered plan revisions, selected actors, fallback reason,
V3 transition count, accepted contacts, touch count, score result, no duplicate
writer trace, and no teleport/canceled committed action.

Add one-variable fixed-key EditMode checks proving:

- Receive Movement increases reach margin without changing FirstTouchControl error;
- Receive FirstTouchControl reduces receive execution error without changing reach;
- Set Movement increases organizer reach margin;
- Set PlacementControl/TempoControl change only their declared set envelope bounds.

- [ ] **Step 2: Run the scenario filters and verify RED.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.CurrentAbilityBenchmarkTests|Volleyball.EditModeTests.SharedBoundaryTests" \
  -testResults "$PWD/TestResults/GateH-task8-edit-red.xml" \
  -logFile "$PWD/TestResults/GateH-task8-edit-red.log"
```

Then run the named new formal scenario methods with a PlayMode filter and write
`GateH-task8-play-red.xml`.

Expected: each new assertion fails for the specific missing scenario seam or
boundary, not from fixture setup.

- [ ] **Step 3: Make only the minimal scenario corrections.**

Correct coordinator/controller/director behavior required by the RED evidence.
Do not move Set target selection, attack route choice, block/defense, soft action,
tool recovery, perception, or replay UI into Gate H.

- [ ] **Step 4: Run the complete Gate H focused EditMode set.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
"Volleyball.EditModeTests.ReceiveOrganizationPlanV3Tests|Volleyball.EditModeTests.ReceiveOrganizationResponsibilityPlannerTests|Volleyball.EditModeTests.ReceiveOrganizationAuthorityCoordinatorTests|Volleyball.EditModeTests.ReceiveOrganizationAuthorityControllerTests|Volleyball.EditModeTests.TeamRallyDecisionPlannerTests|Volleyball.EditModeTests.SetterOrganizationZoneTests|Volleyball.EditModeTests.DeterministicRallyPlanComposerV3Tests|Volleyball.EditModeTests.PlayerTechniqueExecutorTests|Volleyball.EditModeTests.MatchReplayV4Tests|Volleyball.EditModeTests.CurrentAbilityBenchmarkTests|Volleyball.EditModeTests.SharedBoundaryTests|Volleyball.Shared.EditModeTests.MatchContractTests" \
  -testResults "$PWD/TestResults/GateH-task8-focused-edit-green.xml" \
  -logFile "$PWD/TestResults/GateH-task8-focused-edit-green.log"
```

Expected: all filtered tests pass.

- [ ] **Step 5: Run the formal/3v3 affected PlayMode set.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
"Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests" \
  -testResults "$PWD/TestResults/GateH-task8-focused-play-green.xml" \
  -logFile "$PWD/TestResults/GateH-task8-focused-play-green.log"
```

Expected: every affected formal and legacy 3v3 fixture passes.

- [ ] **Step 6: Run static authority-boundary scans.**

```bash
rg -n \
"PrepareSetterForReceive|PrepareAttackerForReceive|RallyDecisionStage\\.Organize" \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs

rg -n \
"PhysicalMatchRallyDirector|PrototypePlayerAgent|UnityEngine|MatchReplayRecorder" \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Authority \
  Assets/Volleyball/Match/Runtime/AI/ReceiveOrganization*.cs
```

Expected: the first command finds only the explicit 3v3 legacy branch or Gate I
handoff comments/assertions, never the formal branch; the second has no matches.

- [ ] **Step 7: Commit Task 8.**

```bash
git add \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs \
  Assets/Volleyball/Match/Tests/EditMode/CurrentAbilityBenchmarkTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs
git commit -m "test: cover gate h authority scenarios"
```

## Task 9: Document, Verify, Review, and Close Gate H

**Files:**

- Modify: `docs/changes/2026-07-26-002-full-rally-v4-gate-h-receive-organization-authority.md`
- Modify: `docs/changes/README.md`
- Modify: `docs/development.md`
- Modify: `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

- [ ] **Step 1: Run the complete EditMode suite from the implementation HEAD.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/GateH-final-editmode.xml" \
  -logFile "$PWD/TestResults/GateH-final-editmode.log"
```

Expected: exit `0`; all tests pass with zero failed/skipped/inconclusive. Record
the exact total and duration from XML.

- [ ] **Step 2: Run the complete PlayMode suite from the same HEAD.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/GateH-final-playmode.xml" \
  -logFile "$PWD/TestResults/GateH-final-playmode.log"
```

Expected: exit `0`; all tests pass with zero failed/skipped/inconclusive. Record
the exact total and duration from XML.

- [ ] **Step 3: Run fresh deterministic and static completion checks.**

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
"Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests.Formal6v6_ReplayDiagnosticRecordingPreservesFixedSeedAuthority|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests.Capture_TwoIndependentFixedSeedFormalRunsAreByteStable" \
  -testResults "$PWD/TestResults/GateH-final-determinism.xml" \
  -logFile "$PWD/TestResults/GateH-final-determinism.log"

rg -n \
"PlayerAbilitySnapshotV[123]|MatchContextV[12]|MatchResultV[12]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'

git diff --check
```

Expected: both determinism tests pass; legacy production search and
`git diff --check` produce no output.

- [ ] **Step 4: Request one independent combined review.**

Give the reviewer:

```text
Description: Gate H formal 6v6 receive/organization single-writer authority,
event-owned Replay V4 evidence, legacy 3v3 preservation.
Requirements: confirmed Gate H design spec and this implementation plan.
Base SHA: 818ac52
Head SHA: current implementation HEAD
Review focus: authority duplication, stale lifecycle callbacks, setter priority,
V4 evidence identity, canonical/backward replay, 3v3/Gate I scope leakage,
missing tests.
```

Fix every Critical/Important finding with a new failing focused regression, run
its GREEN filter, and ask the same reviewer to re-check that finding. Do not split
the review into separate speculative roles.

- [ ] **Step 5: Re-run suites only when findings make prior evidence stale.**

If a material finding changes authority behavior, canonical JSON/hash, lifecycle,
or more than one module, rerun Steps 1–3 and record the replacement XML. For a
localized non-material correction, run the focused regression, affected suite,
determinism pair when relevant, and `git diff --check`.

- [ ] **Step 6: Finalize documentation with actual evidence.**

Update the consolidated roadmap:

```text
Gate H: completed
Gate I–K: pending
```

Update `docs/development.md` with the Gate H authority boundary and exact current
suite totals. Mark the change record completed, list the exact commit range,
Unity `6000.0.43f1`, XML paths/totals/durations, static scans, review findings,
manual checks not run, and rollback instructions. Do not copy old Gate G totals
as Gate H evidence.

- [ ] **Step 7: Commit final Gate H documentation.**

```bash
git add \
  docs/changes/2026-07-26-002-full-rally-v4-gate-h-receive-organization-authority.md \
  docs/changes/README.md \
  docs/development.md \
  docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md
git commit -m "docs: complete gate h verification"
```

- [ ] **Step 8: Verify the final committed tree before completion claims.**

```bash
git status --short
git diff --check HEAD^ HEAD
git log --oneline --decorate 818ac52..HEAD
```

Expected: clean status, no diff-check output, and only intentional Gate H commits.
Do not merge, rebase onto main, push, or open a PR unless the user separately
requests it.

## Final Review Checklist

- [ ] Formal 6v6 has one Receive/Organization writer.
- [ ] Director contains no formal Gate H actor/fallback/replan selection.
- [ ] Gate F plan values remain command-free.
- [ ] Gate G facade consumes the exact selected V4 evidence.
- [ ] Registered setter priority and all fallback reasons are explicit.
- [ ] Emergency receive activates only a declared branch.
- [ ] No legal organizer preserves the existing terminal behavior.
- [ ] Set accepted hands off only to the temporary Gate I attack seam.
- [ ] V3 rules/touch/score remain the sole rules authority.
- [ ] Old Replay V4 reads; new formal Receive/Set events carry authority evidence.
- [ ] Canonical bytes/hash and recorder on/off authority fingerprints are stable.
- [ ] Legacy 3v3 remains outside Gate H.
- [ ] Full current EditMode and PlayMode suites pass from the implementation HEAD.
- [ ] One independent combined review has no unresolved blocker.
- [ ] Change record and roadmap contain actual, fresh evidence.
