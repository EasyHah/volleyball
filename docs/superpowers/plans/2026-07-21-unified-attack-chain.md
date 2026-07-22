# Unified Attack Chain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make both physical rally scenes use one physically reachable, team-local, quality-aware setter-to-attack chain with replayable responsibility.

**Architecture:** The shared player contract supplies metre-based maximum attack reach. Pure AI value objects calculate contact plans, setter style, flight time, and quality before the common director schedules Unity contact surfaces. The director replans from the accepted set trajectory and writes diagnostics through the existing replay pipeline; scene bootstraps only provide role-specific ability defaults.

**Tech Stack:** Unity 6000.0.43f1, C#, NUnit EditMode and PlayMode tests, existing deterministic ball integrator and MatchReplayV1 artifacts.

---

## File Structure

- `Assets/Volleyball/Shared/Runtime/PlayerAbilitySnapshotV1.cs`: serialized metre-based attack reach.
- `Assets/Volleyball/Shared/Runtime/PlayerAbilitySnapshotV2.cs`, `PlayerSnapshotV2.cs`,
  `TeamSnapshotV2.cs`, `MatchContextV2.cs`, and `MatchResultV2.cs`: new-match
  V2 contracts and explicit V1 migration; V1 contracts remain byte-for-byte
  canonical-compatible.
- `Assets/Volleyball/Match/Runtime/Domain/Players/PlayerAbilityProfile.cs`: immutable runtime ability projection.
- `Assets/Volleyball/Match/Runtime/AI/AttackContactPlanner.cs`: pure contact, adjustment, and handling plan.
- `Assets/Volleyball/Match/Runtime/AI/SetFlightSolver.cs`: discrete ballistic rhythm selection.
- `Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs`: A-E quality and responsibility rules.
- `Assets/Volleyball/Match/Runtime/AI/SetTechniqueSelector.cs`: normal and emergency local-frame set styles.
- `Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs`: set rhythm metadata.
- `Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs`: shared future attack contact plan.
- `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`: prepared facing and physical attack palm placement.
- `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`: scheduling, actual-set replan, statistics, and replay events.
- `Assets/Volleyball/Match/Runtime/Domain/Replay/MatchReplayV1.cs`, `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`, and `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs`: replay diagnostics.
- `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyBootstrap.cs` and `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`: role reach defaults.
- EditMode tests cover pure behaviour; existing 3v3/6v6 PlayMode tests and a new calibration test cover scenes.
- `docs/changes/2026-07-21-001-unified-attack-chain.md` documents the Shared contract migration.

### Task 1: Preserve V1 And Add Explicit V2 Migration

**Files:**
- Create: `Assets/Volleyball/Shared/Runtime/PlayerAbilitySnapshotV2.cs`
- Create: `Assets/Volleyball/Shared/Runtime/PlayerSnapshotV2.cs`
- Create: `Assets/Volleyball/Shared/Runtime/TeamSnapshotV2.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchContextV2.cs`
- Create: `Assets/Volleyball/Shared/Runtime/MatchResultV2.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/ContractJson.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/ContractPrimitives.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/Players/PlayerAbilityProfile.cs`
- Modify: every `PlayerAbilitySnapshotV1` and `PlayerAbilityProfile` construction site.
- Test: `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs`

- [x] **Step 1: Write the failing contract and projection tests.**

```csharp
var legacy = ContractJson.DeserializeContext(V1Fixture.Json);
Assert.That(legacy.ContextHash, Is.EqualTo(V1Fixture.ContextHash));
var ability = new PlayerAbilitySnapshotV2(0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 3.42f);
var restored = ContractJson.DeserializeContextV2(ContractJson.Serialize(CreateContextV2With(ability)));
Assert.That(restored.Home.Players[0].Ability.MaxAttackReach, Is.EqualTo(3.42f));
Assert.That(new PlayerAbilityProfile(ability).MaxAttackReach, Is.EqualTo(3.42f));
Assert.That(() => new PlayerAbilitySnapshotV2(0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 3.19f),
    Throws.TypeOf<ContractValidationException>());
```

- [x] **Step 2: Run the focused tests and verify red.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.Shared.EditModeTests.MatchContractTests|Volleyball.EditModeTests.SharedBoundaryTests" -testResults "$PWD/TestResults/Reach-red.xml" -logFile "$PWD/TestResults/Reach-red.log"`

Expected: compile failure for the V2 types and explicit V2 JSON methods.

- [x] **Step 3: Add the field consistently.**

```csharp
[DataMember(Name = "maxAttackReach", Order = 8)] private float _maxAttackReach;
public float MaxAttackReach => _maxAttackReach;
// V2 constructor, equality, hash, Validate(), profile projection, and V2 context
// hashing use the same finite inclusive 3.20f-3.55f validation.
```

Do not modify V1 classes or their canonical hash code. Add V2 context/result
counterparts and V2-only `ContractJson` methods; retain old V1 method signatures.
Implement `MatchContextV2.UpgradeFromV1` with deterministic migration defaults:
3.20 for setter/libero/defender, 3.42 for outside/opposite, and 3.48 for middle.
Use explicit V2 construction values in new 3v3/6v6 bootstraps; do not impose a
position restriction on the V2 ability type itself.

- [x] **Step 4: Run green and commit.**

Run: repeat Step 2 with `Reach-green` output names.

Expected: XML `failed="0"`.

```bash
git add Assets/Volleyball/Shared/Runtime/PlayerAbilitySnapshotV2.cs Assets/Volleyball/Shared/Runtime/PlayerSnapshotV2.cs Assets/Volleyball/Shared/Runtime/TeamSnapshotV2.cs Assets/Volleyball/Shared/Runtime/MatchContextV2.cs Assets/Volleyball/Shared/Runtime/MatchResultV2.cs Assets/Volleyball/Shared/Runtime/ContractJson.cs Assets/Volleyball/Shared/Runtime/ContractPrimitives.cs Assets/Volleyball/Match/Runtime/Domain/Players/PlayerAbilityProfile.cs Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs
git commit -m "feat: add v2 attack reach contract"
```

### Task 2: Use One Planned And Physical Contact Point

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/AI/AttackContactPlanner.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/AttackContactPlanner.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/AttackContactPlannerTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [x] **Step 1: Write the failing contact-plan and palm-alignment tests.**

```csharp
var plan = AttackContactPlanner.Plan(new AttackContactInput(
    3.50f, 1f, 1f, SetQualityGrade.A, new SimVector3(1f, 0f, -2f), 0.9f, 1.2f));
Assert.That(plan.ContactCenter.Y, Is.EqualTo(3.50f).Within(0.0001f));
Assert.That(plan.Outcome, Is.EqualTo(AttackContactOutcome.FullAttack));

var adjusted = AttackContactPlanner.Plan(new AttackContactInput(
    3.50f, 0.5f, 0.5f, SetQualityGrade.B, new SimVector3(1f, 0f, -2f), 0.9f, 1.2f));
Assert.That(adjusted.ContactCenter.Y, Is.InRange(3.20f, 3.50f));
```

Add an agent preview assertion that `AttackPalm` centre is within 0.05 metres of `plan.ContactCenter`.

- [x] **Step 2: Run red.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.AttackContactPlannerTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests" -testResults "$PWD/TestResults/ContactPlan-red.xml" -logFile "$PWD/TestResults/ContactPlan-red.log"`

Expected: compile failure for `AttackContactPlanner`.

- [x] **Step 3: Implement the immutable plan and route it everywhere.**

```csharp
public enum AttackContactOutcome { FullAttack, AdjustedAttack, Handling }
public readonly struct AttackContactPlan {
    public SimVector3 Takeoff { get; }
    public SimVector3 ContactCenter { get; }
    public float ApproachCompletion { get; }
    public float JumpTiming { get; }
    public AttackContactOutcome Outcome { get; }
}
```

The planner derives contact height from maximum reach, approach completion, jump timing, and quality. Clamp to `[3.20f, MaxAttackReach]`; return `Handling` when time cannot reach the minimum. Replace the fixed `AttackContactHeight` in the decision planner. Add `AttackContactPlan` to attack decisions, use its contact centre for set targeting, and make the agent's jump and palm preview use it instead of the previous jump-height formula.

- [x] **Step 4: Run green and commit.**

Run: repeat Step 2 with `ContactPlan-green` paths.

Expected: XML `failed="0"`; `rg -n 'AttackContactHeight = 2\.7f' Assets` returns no matches.

```bash
git add Assets/Volleyball/Match/Runtime/AI/AttackContactPlanner.cs Assets/Volleyball/Match/Runtime/AI/AttackContactPlanner.cs.meta Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs Assets/Volleyball/Match/Tests/EditMode/AttackContactPlannerTests.cs Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "feat: unify planned and physical attack contact"
```

### Task 3: Select Normal Setter Styles In Team-Local Coordinates

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/AI/SetTechniqueSelector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/SetTechniqueSelectorTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs`

- [x] **Step 1: Write failing normal-style and Blue/Orange mirror tests.**

```csharp
Assert.That(SetTechniqueSelector.SelectNormal(SetRoute.LeftPin, 0.95f).ExecutedStyle,
    Is.EqualTo(SetTechniqueStyle.FrontTwoHand));
Assert.That(SetTechniqueSelector.SelectNormal(SetRoute.MiddleQuick, 0.95f).ExecutedStyle,
    Is.EqualTo(SetTechniqueStyle.FrontTwoHand));
Assert.That(SetTechniqueSelector.SelectNormal(SetRoute.RightPin, 0.95f).ExecutedStyle,
    Is.EqualTo(SetTechniqueStyle.BackTwoHand));
Assert.That(SetTechniqueSelector.SelectNormal(SetRoute.BackSet, 0.95f).ExecutedStyle,
    Is.EqualTo(SetTechniqueStyle.BackTwoHand));
```

Assert a normal wide LeftPin never selects a side style. Build equal Blue/Orange requests, compare their local prepared forward vectors, then assert their world Z directions are mirror images.

- [x] **Step 2: Run red.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.SetTechniqueSelectorTests|Volleyball.EditModeTests.TeamRallyDecisionPlannerTests" -testResults "$PWD/TestResults/SetterFacing-red.xml" -logFile "$PWD/TestResults/SetterFacing-red.log"`

Expected: compile failure for `SelectNormal` and prepared-facing APIs.

- [x] **Step 3: Split normal and emergency selection.**

```csharp
public static SetTechniqueDecision SelectNormal(SetRoute route, float setTechnique) =>
    Select(route is SetRoute.RightPin or SetRoute.BackSet
        ? SetTechniqueStyle.BackTwoHand : SetTechniqueStyle.FrontTwoHand, setTechnique);
public static SetTechniqueDecision SelectEmergency(
    SimVector3 localTargetVelocity, float setTechnique, bool oneHand) => ...
```

Side and one-hand selection is available only through `SelectEmergency`. Add `SetPreparedFacing(TeamCourtFrame frame, SetRoute route)` to the agent. Its normal local forward faces four-position and its near-net shoulder faces the net. The director invokes it and uses `SelectNormal` for an in-system pass; it only passes the emergency selector for marked off-balance contact.

- [x] **Step 4: Run green and commit.**

Run: repeat Step 2 with `SetterFacing-green` paths.

Expected: XML `failed="0"`; both teams show front two-hand on LeftPin/MiddleQuick and back two-hand on RightPin/BackSet.

```bash
git add Assets/Volleyball/Match/Runtime/AI/SetTechniqueSelector.cs Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs Assets/Volleyball/Match/Tests/EditMode/SetTechniqueSelectorTests.cs Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs
git commit -m "feat: use team-local normal setter orientation"
```

### Task 4: Solve Dynamic Set Flight Time

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/AI/SetFlightSolver.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/SetFlightSolver.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/SetFlightSolverTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/ReturnVelocitySolverTests.cs`

- [x] **Step 1: Write failing rhythm and physical replay tests.**

```csharp
var solution = SetFlightSolver.Solve(new SetFlightRequest(SetRhythm.FastPin,
    new SimVector3(0f, 2.4f, -2f), new SimVector3(-3.1f, 3.42f, -2.45f),
    1f, 1f, new BallSimulationParameters(-9.8f, 0.9995f), 1f / 120f));
Assert.That(solution.FlightSeconds, Is.InRange(0.75f, 1.05f));
Assert.That(solution.Apex.Y, Is.GreaterThan(solution.Target.Y));
```

Replay the initial velocity through `BallIntegrator` for `StepCount` and assert distance below `0.0002f`. Assert the documented bounds for CloseQuick, BackQuick, FastPin, Adjustment, and HighBall. Add rejection for a target with no physically plausible apex.

- [x] **Step 2: Run red.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.SetFlightSolverTests|Volleyball.EditModeTests.PhysicalRallyTacticPlannerTests|Volleyball.EditModeTests.ReturnVelocitySolverTests" -testResults "$PWD/TestResults/SetFlight-red.xml" -logFile "$PWD/TestResults/SetFlight-red.log"`

Expected: compile failure for `SetFlightSolver` and `SetRhythm`.

- [x] **Step 3: Store rhythm, enumerate fixed-step solutions, and schedule unchanged velocity.**

```csharp
public enum SetRhythm { CloseQuick, BackQuick, FastPin, Adjustment, HighBall }
public readonly struct SetFlightSolution {
    public float FlightSeconds { get; }
    public int StepCount { get; }
    public SimVector3 InitialVelocity { get; }
    public SimVector3 Apex { get; }
}
```

Map MiddleQuick to CloseQuick, BackSet to BackQuick, and pins to FastPin. Use Adjustment/HighBall only for degraded pass state. Enumerate whole fixed-step durations in the selected range, use `ReturnVelocitySolver.Solve`, replay each candidate to obtain its apex, and reject non-arriving or implausible arcs. Choose the feasible time nearest the readiness-adjusted rhythm midpoint. Remove `SetFlightSeconds`; call the solver at each set schedule and never rescale `InitialVelocity` after solving.

- [x] **Step 4: Run green and commit.**

Run: repeat Step 2 with `SetFlight-green` paths.

Expected: XML `failed="0"`; no route owns a fixed authoritative set duration.

```bash
git add Assets/Volleyball/Match/Runtime/AI/SetFlightSolver.cs Assets/Volleyball/Match/Runtime/AI/SetFlightSolver.cs.meta Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs Assets/Volleyball/Match/Tests/EditMode/SetFlightSolverTests.cs Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs Assets/Volleyball/Match/Tests/EditMode/ReturnVelocitySolverTests.cs
git commit -m "feat: solve dynamic set flight rhythm"
```

### Task 5: Replan From Actual Set Contact And Grade It

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/SetQualityAssessmentTests.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs`

- [x] **Step 1: Write failing A-E, attribution, and fallback tests.**

```csharp
Assert.That(SetQualityAssessment.Evaluate(new SetQualityInput(0.02f, 0.03f, 0.02f, 1.1f, 0.8f)).Grade,
    Is.EqualTo(SetQualityGrade.A));
Assert.That(SetQualityAssessment.Evaluate(new SetQualityInput(0.45f, 0.35f, 0.35f, 0.7f, 0.1f)).Grade,
    Is.EqualTo(SetQualityGrade.D));
Assert.That(SetQualityAssessment.PrimaryResponsibility(SetQualityGrade.A, AttackOutcome.Out),
    Is.EqualTo(AttackResponsibility.Attacker));
Assert.That(SetQualityAssessment.PrimaryResponsibility(SetQualityGrade.E, AttackOutcome.NoNormalAttack),
    Is.EqualTo(AttackResponsibility.Setter));
```

Test that a B trajectory updates approach/takeoff and becomes `AdjustedAttack`; test that a D trajectory returns `Handling` and opens no spike contact window.

- [x] **Step 2: Run red.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.SetQualityAssessmentTests|Volleyball.EditModeTests.TeamRallyDecisionPlannerTests" -testResults "$PWD/TestResults/Quality-red.xml" -logFile "$PWD/TestResults/Quality-red.log"`

Expected: compile failure for `SetQualityAssessment` and its grade enums.

- [x] **Step 3: Grade actual contact, replace provisional attack, or handle.**

```csharp
public enum SetQualityGrade { A, B, C, D, E }
public enum AttackResponsibility { None, Setter, Attacker }
public readonly struct SetQualityAssessment {
    public float HorizontalError { get; }
    public float HeightError { get; }
    public float ArrivalTimeError { get; }
    public float NetDistance { get; }
    public bool IsAdjustable { get; }
    public float RemainingApproachSeconds { get; }
    public SetQualityGrade Grade { get; }
}
```

At accepted Set, predict arrival/apex from the contact's actual outgoing velocity, evaluate quality against the provisional contact plan, and build a replacement `AttackContactPlan`. A/B/C cancels and replaces the provisional attack. D/E cancels it and schedules a controlled handling path with `NoNormalAttack`, not a spike timeout. Preserve all contributing causes diagnostically but select one primary stats owner: A/B attack faults are attacker; D/E inability is setter.

- [x] **Step 4: Run green and commit.**

Run: repeat Step 2 with `Quality-green` paths.

Expected: XML `failed="0"`; attribution follows the table and impossible strong spikes are absent.

```bash
git add Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs.meta Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs Assets/Volleyball/Match/Tests/EditMode/SetQualityAssessmentTests.cs Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs
git commit -m "feat: replan attacks from set quality"
```

### Task 6: Record Quality, Replan, And Responsibility

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/Replay/MatchReplayV1.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV1Tests.cs`
- Test: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`

- [x] **Step 1: Write failing replay and counter tests.**

```csharp
Assert.That(replay.Events, Has.Some.Matches<MatchReplayEventV1>(e =>
    e.SetChain != null && e.SetChain.QualityGrade == "A" &&
    e.SetChain.ActualAttackContactCenter != null));
Assert.That(director.AttackableSetRate, Is.InRange(0f, 1f));
Assert.That(html, Does.Contain("set-quality"));
```

Create a fixture where `SetChain.QualityGrade` is missing and assert `Validate()` throws `MatchReplayValidationException`.

- [x] **Step 2: Run red.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.MatchReplayV1Tests" -testResults "$PWD/TestResults/ReplayChain-red.xml" -logFile "$PWD/TestResults/ReplayChain-red.log"`

Expected: compile failure for `SetChain`.

- [x] **Step 3: Extend replay; do not create a second reporter.**

```csharp
[DataContract]
public sealed class MatchReplaySetChainV1 {
    [DataMember(Name = "plannedAttackContactCenter", Order = 1)] public MatchReplayVector3V1 PlannedAttackContactCenter { get; set; }
    [DataMember(Name = "actualAttackContactCenter", Order = 2)] public MatchReplayVector3V1 ActualAttackContactCenter { get; set; }
    [DataMember(Name = "qualityGrade", Order = 3)] public string QualityGrade { get; set; }
    [DataMember(Name = "replanOutcome", Order = 4)] public string ReplanOutcome { get; set; }
    [DataMember(Name = "primaryResponsibility", Order = 5)] public string PrimaryResponsibility { get; set; }
    [DataMember(Name = "reason", Order = 6)] public string Reason { get; set; }
}
```

Attach this optional object to the actual set-contact event. Validate finite vectors and non-empty strings when present; canonical-copy it; map it in the recorder; render it below decision candidates. Add zero-safe director counters: total sets, A sets, A/B/C attackable sets, D/E direct set errors, A-set attack successes, and adjusted attack successes. Increment only at actual set contact or rally outcome.

- [x] **Step 4: Run green EditMode and formal replay tests, then commit.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "Volleyball.EditModeTests.MatchReplayV1Tests" -testResults "$PWD/TestResults/ReplayChain-green.xml" -logFile "$PWD/TestResults/ReplayChain-green.log"`

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests" -testResults "$PWD/TestResults/FormalReplayChain.xml" -logFile "$PWD/TestResults/FormalReplayChain.log"`

Expected: both XML reports have `failed="0"`; the artifact includes one chain record with reason and responsibility.

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs Assets/Volleyball/Match/Runtime/Domain/Replay/MatchReplayV1.cs Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs Assets/Volleyball/Match/Tests/EditMode/MatchReplayV1Tests.cs Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs
git commit -m "feat: record set quality and attack attribution"
```

### Task 7: Apply Both-Scene Defaults And Calibrate

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyBootstrap.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Create: `Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs`
- Create: `Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs.meta`
- Modify: `docs/development.md`
- Create: `docs/changes/2026-07-21-001-unified-attack-chain.md`
- Modify: `docs/changes/README.md`

- [x] **Step 1: Write the failing 100-first-pass and 20-set tests.**

```csharp
[UnityTest]
public IEnumerator Formal6v6_InSystemAttackChainMeetsInitialThresholds()
{
    var report = yield return RunInSystemFirstPasses("FormalIndoor6v6", 7351, 100);
    Assert.That(report.AttackableSetRate, Is.GreaterThanOrEqualTo(0.95f));
    Assert.That(report.AGradeNoContactErrorRate, Is.LessThan(0.02f));
    Assert.That(report.NormalSideSets, Is.Zero);
}
```

Add the equivalent 3v3 test. Add a symmetric 20-set test asserting Blue wins 9-11 inclusive and every non-A chain has a replay reason.

- [x] **Step 2: Run red and retain the baseline log.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests" -testResults "$PWD/TestResults/AttackChainCalibration-red.xml" -logFile "$PWD/TestResults/AttackChainCalibration-red.log"`

Expected: initial harness compile failure, then threshold failures that do not coincide with structural-test failures.

- [x] **Step 3: Set mirrored reaches and implement the deterministic harness.**

Use identical Blue/Orange defaults: attackers, outsides, and opposites 3.42-3.52; middles 3.48-3.55; setters 3.20-3.28; defenders/liberos 3.20. The harness runs the ordinary scene/director pipeline, controls only seed and first-pass quality, reads public counters, and attaches `MatchReplayRecorder` for abnormal chains. It must not modify outgoing velocity, bypass contacts, or edit success counters.

- [x] **Step 4: Tune only bounded coefficients and verify all criteria.**

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; mkdir -p TestResults; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults "$PWD/TestResults/UnifiedAttack-EditMode.xml" -logFile "$PWD/TestResults/UnifiedAttack-EditMode.log"`

Run: `UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"; "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests" -testResults "$PWD/TestResults/UnifiedAttack-PlayMode.xml" -logFile "$PWD/TestResults/UnifiedAttack-PlayMode.log"`

Expected: both XML reports have `failed="0"`; 100 in-system first passes meet 95%+ attackability and <2% A-grade no-contact errors; all 20 symmetric sets finish with Blue winning 9-11; every abnormal replay chain has a reason.

- [x] **Step 5: Create the required change record and commit.**

Create `docs/changes/2026-07-21-001-unified-attack-chain.md` from `docs/changes/TEMPLATE.md`, mark it `跨模块（重点）` because the Shared contract has a new eighth `maxAttackReach` field, update the change index, and update 3v3/6v6 verification instructions.

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyBootstrap.cs Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs.meta docs/development.md docs/changes/2026-07-21-001-unified-attack-chain.md docs/changes/README.md
git commit -m "feat: calibrate unified attack chain"
```

## Plan Review

- Spec coverage: Tasks 1-2 implement max reach and a single contact centre; Task 3 implements team-local orientation; Task 4 implements dynamic ballistic rhythm; Task 5 implements real-contact replanning, A-E quality, handling, and attribution; Task 6 records replay diagnostics and counters; Task 7 calibrates both scenes.
- Placeholder scan: all introduced public types, files, assertions, seeds, thresholds, and verification commands are explicit.
- Type consistency: `AttackContactPlan`, `SetFlightSolution`, `SetQualityAssessment`, `SetQualityGrade`, and `AttackResponsibility` are named consistently from creation through replay and calibration.
