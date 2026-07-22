# Geometric Attack-Block Counterplay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make physical attacks and blocks state-driven and deterministic: preserve normal referee scoring after a block, use near-net attack bands and continuous approaches, select legal geometric attack routes, and schedule reachable one-to-three-player blocks.

**Architecture:** Keep referee and touch-count authority in the existing Domain layer. Add small Unity-free AI policy types for attack bands, set targets, route scoring, and block-unit selection; the presentation director supplies live player/arm snapshots and schedules their selected decisions. Existing `SimulatedBall`, `MatchRallyReferee`, and `RallyTouchState` remain the only authorities for collision, legality, possession, and scoring.

**Tech Stack:** Unity 6000.0.43f1, C#, NUnit EditMode tests, Unity PlayMode tests, existing deterministic custom ball simulation.

**Rules:** [docs/rules.md](../../rules.md) R-GOV-001--002, R-REF-001--006, R-PLAY-001--003, and R-OFF-001--005 are normative. Update its compliance table when every listed legacy deviation is removed.

---

## File Structure

- Create `Assets/Volleyball/Match/Runtime/AI/AttackBandPolicy.cs`: Unity-free attack-band resolution from role, team-local setter depth, and actual set target.
- Create `Assets/Volleyball/Match/Runtime/AI/SetTargetSelector.cs`: deterministic candidate generation and best-handling-point selection from legal attack bands and predicted arm capsules.
- Create `Assets/Volleyball/Match/Runtime/AI/AttackRouteSelector.cs`: deterministic attack velocity-route scoring against predicted arm capsules.
- Create `Assets/Volleyball/Match/Runtime/AI/BlockUnitPlanner.cs`: selection of one to three reachable blockers.
- Modify `Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs`: remove seeded random route selection and retain only deterministic baseline tactical metadata.
- Modify `Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs`: use `AttackBandPolicy` during actual-set replanning instead of copying actual ball depth into takeoff depth.
- Modify `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`: expose a read-only preview of block arm capsules and preserve approach position when an attack is scheduled after preparation.
- Modify `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`: integrate target/route/block selectors, schedule a block unit, and delay possession transition after an accepted block.
- Modify `Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs`: replace random-coverage assertions with deterministic baseline assertions.
- Modify `Assets/Volleyball/Match/Tests/EditMode/SetQualityAssessmentTests.cs`: cover near-net attack-band replanning and behind-four-metre shift caps.
- Create `Assets/Volleyball/Match/Tests/EditMode/AttackBandPolicyTests.cs`, `SetTargetSelectorTests.cs`, `AttackRouteSelectorTests.cs`, and `BlockUnitPlannerTests.cs`: pure policy coverage.
- Modify `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`: prove prepared approach progress is not reset.
- Modify `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs` and `FormalSixVsSixRallyPlayModeTests.cs`: assert post-block referee timing, multi-block scheduling, near-net attack contacts, and no illegal back-row 6v6 blocker.
- Modify `Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs`: retain existing chain thresholds and add attack-band/route diagnostics.
- Modify `docs/rules.md`: remove resolved deviations from the compliance list and link focused tests.
- Add `docs/changes/2026-07-22-002-geometric-attack-block-counterplay.md`: completed behavior, migration risk, and verification evidence.

## Task 1: Make Tactical Baselines Deterministic Without Hard Randomness

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs`
- Modify: `docs/rules.md`

- [x] **Step 1: Write the failing deterministic-baseline test**

Replace the random-route coverage test with the following test in `PhysicalRallyTacticPlannerTests.cs`:

```csharp
[Test]
public void Create_UsesTheSameStateDerivedBaselineForEveryRevision()
{
    var planner = new PhysicalRallyTacticPlanner(7351);

    var first = planner.Create(0);
    var later = planner.Create(57);

    Assert.That(later, Is.EqualTo(first));
    Assert.That(first.Blue.SetRoute, Is.EqualTo(SetRoute.LeftPin));
    Assert.That(first.Blue.SpikeRoute, Is.EqualTo(SpikeRoute.CrossCourt));
    Assert.That(first.Orange.SetRoute, Is.EqualTo(SetRoute.LeftPin));
    Assert.That(first.Orange.SpikeRoute, Is.EqualTo(SpikeRoute.CrossCourt));
}
```

- [x] **Step 2: Run the focused EditMode test and confirm RED**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter Volleyball.EditModeTests.PhysicalRallyTacticPlannerTests.Create_UsesTheSameStateDerivedBaselineForEveryRevision \
  -testResults "$PWD/TestResults/tactic-baseline-red.xml" \
  -logFile "$PWD/TestResults/tactic-baseline-red.log"
```

Expected: FAIL because `Create(57)` uses `Random` and returns a different tactic.

- [x] **Step 3: Replace the random tactic selection with the explicit baseline**

In `PhysicalRallyTacticPlanner.Create`, remove the `Random` construction and the four `random.Next` calls. Begin the method body after validation with:

```csharp
var blueSet = SetRoute.LeftPin;
var blueSpike = SpikeRoute.CrossCourt;
var orangeSet = SetRoute.LeftPin;
var orangeSpike = SpikeRoute.CrossCourt;
```

Keep `revision` validated because the director still supplies it for diagnostics, but do not use it to create an artificial tactical variation. Delete the unused `_seed` field and constructor assignment while retaining the constructor signature for compatibility.

- [x] **Step 4: Run the focused EditMode test and confirm GREEN**

Run the command from Step 2 with result file `TestResults/tactic-baseline-green.xml`.

Expected: PASS.

- [x] **Step 5: Update the rule compliance list**

Remove the `PhysicalRallyTacticPlanner` `Random` row from `docs/rules.md` and add `PhysicalRallyTacticPlannerTests` to the R-GOV/R-OFF validation mapping.

- [x] **Step 6: Run the planner test fixture**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter Volleyball.EditModeTests.PhysicalRallyTacticPlannerTests \
  -testResults "$PWD/TestResults/tactic-baseline.xml" \
  -logFile "$PWD/TestResults/tactic-baseline.log"
```

Expected: PASS. Update or remove earlier tests that required 32 revisions to cover every random route; they contradict R-GOV-001.

- [x] **Step 7: Commit the deterministic baseline**

```bash
git add Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs docs/rules.md
git commit -m "fix: remove random rally tactics"
```

## Task 2: Add Attack-Band Policy and Preserve Near-Net Takeoff During Replanning

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/AI/AttackBandPolicy.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackBandPolicyTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/SetQualityAssessmentTests.cs`

- [x] **Step 1: Write failing band-policy tests**

Create `AttackBandPolicyTests.cs` with these behaviors:

```csharp
[TestCase(PlayerRole.Attacker, 0.75f, 1.50f)]
[TestCase(PlayerRole.OutsideHitter, 0.75f, 1.50f)]
[TestCase(PlayerRole.Opposite, 0.75f, 1.50f)]
[TestCase(PlayerRole.MiddleBlocker, 0.50f, 0.75f)]
public void Resolve_UsesTheRoleAttackBand(PlayerRole role, float near, float far)
{
    var band = AttackBandPolicy.Resolve(role, setterDepthFromNet: 1.5f);
    Assert.That(band.NearDepth, Is.EqualTo(near).Within(0.0001f));
    Assert.That(band.FarDepth, Is.EqualTo(far).Within(0.0001f));
}

[Test]
public void Resolve_BehindFourMetresMovesBandByHalfTheExcessUpToOneAndHalfMetres()
{
    var shifted = AttackBandPolicy.Resolve(PlayerRole.Attacker, setterDepthFromNet: 6f);
    var capped = AttackBandPolicy.Resolve(PlayerRole.Attacker, setterDepthFromNet: 9f);

    Assert.That(shifted.NearDepth, Is.EqualTo(1.75f).Within(0.0001f));
    Assert.That(shifted.FarDepth, Is.EqualTo(2.50f).Within(0.0001f));
    Assert.That(capped.NearDepth, Is.EqualTo(2.25f).Within(0.0001f));
    Assert.That(capped.FarDepth, Is.EqualTo(3.00f).Within(0.0001f));
}

[Test]
public void ConstrainTakeoff_PreservesBandDepthWhileAcceptingTheActualSetLateralError()
{
    var band = AttackBandPolicy.Resolve(PlayerRole.Attacker, 1f);
    var takeoff = band.ConstrainTakeoff(TeamId.Blue, new SimVector3(1.2f, 3.3f, -3.8f));

    Assert.That(takeoff.X, Is.EqualTo(1.2f).Within(0.0001f));
    Assert.That(takeoff.Z, Is.InRange(-1.5f, -0.75f));
}
```

- [x] **Step 2: Run the new fixture and confirm RED**

Run the Unity EditMode command from Task 1 with test filter `Volleyball.EditModeTests.AttackBandPolicyTests`.

Expected: compilation failure because `AttackBandPolicy` does not exist.

- [x] **Step 3: Implement the minimal pure policy**

Create `AttackBandPolicy.cs` with a validated `AttackBand` value type and these public members:

```csharp
public readonly struct AttackBand
{
    public AttackBand(float nearDepth, float farDepth) { /* validate 0 <= near <= far */ }
    public float NearDepth { get; }
    public float FarDepth { get; }
    public SimVector3 ConstrainTakeoff(TeamId team, SimVector3 actualSetCenter) { /* preserve X; clamp local depth */ }
}

public static class AttackBandPolicy
{
    public static AttackBand Resolve(PlayerRole role, float setterDepthFromNet) { /* role band plus capped shift */ }
}
```

Use `TeamCourtFrame` to convert to local coordinates. Use `excess = Math.Max(0f, setterDepthFromNet - 4f)` and `shift = Math.Min(1.5f, excess * 0.5f)`. For roles other than middle blocker, use `(0.75f + shift, 1.50f + shift)`; middle blocker uses `(0.50f + shift, 0.75f + shift)`.

- [x] **Step 4: Run the new fixture and confirm GREEN**

Run the Task 2 focused command again.

Expected: PASS.

- [x] **Step 5: Write the failing actual-set replan test**

Replace `Replan_BTrajectoryMovesTakeoffAndCreatesAdjustedAttack` with a test that calls an overload accepting `PlayerRole` and setter depth, then asserts:

```csharp
Assert.That(replan.Approach.Takeoff.Z, Is.InRange(-1.50f, -0.75f));
Assert.That(replan.ContactPlan.Takeoff, Is.EqualTo(replan.Approach.Takeoff));
Assert.That(replan.ContactPlan.ContactCenter.Z, Is.InRange(-1.50f, -0.75f));
```

Use `actualCenter = new SimVector3(0.16f, 3.40f, -3.80f)`, `PlayerRole.Attacker`, `TeamId.Blue`, and `setterDepthFromNet: 1f` to prove an off-target deep set does not create a 3.8m takeoff.

- [x] **Step 6: Run the focused replan test and confirm RED**

Run the Unity EditMode command with test filter `Volleyball.EditModeTests.SetQualityAssessmentTests.Replan_BTrajectoryKeepsAttackerInNearNetBand`.

Expected: compilation failure until the replan API accepts attack-band context, or assertion failure because current code assigns `actualCenter.Z` to takeoff.

- [x] **Step 7: Replan against the attack band instead of actual ball depth**

Add the following parameters to `SetAttackReplanner.Replan` after `maxAttackReach`:

```csharp
PlayerRole attackerRole,
TeamId attackingTeam,
float setterDepthFromNet
```

Inside `Replan`, replace the direct construction of `takeoff` with:

```csharp
var band = AttackBandPolicy.Resolve(attackerRole, setterDepthFromNet);
var takeoff = band.ConstrainTakeoff(attackingTeam, actualContactCenter);
```

Build the reachable contact center from `takeoff.X` and `takeoff.Z`, while retaining the clamped real height. Update every existing call site and test to pass the role, team, and setter depth measured in team-local coordinates.

- [x] **Step 8: Run the full set-quality fixture and confirm GREEN**

Run the Unity EditMode command with test filter `Volleyball.EditModeTests.SetQualityAssessmentTests`.

Expected: PASS, including handling behavior for D/E-quality sets.

- [x] **Step 9: Commit the band policy and replan correction**

```bash
git add Assets/Volleyball/Match/Runtime/AI/AttackBandPolicy.cs Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs Assets/Volleyball/Match/Tests/EditMode/AttackBandPolicyTests.cs Assets/Volleyball/Match/Tests/EditMode/SetQualityAssessmentTests.cs
git commit -m "fix: keep attack replans in near-net bands"
```

## Task 3: Preserve Early Approach Progress Across Actual-Set Replanning

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs`

- [x] **Step 1: Write the failing prepared-approach test**

Add a test to `PrototypePlayerContactSourceTests.cs` that schedules `ScheduleAttackPreparation`, advances contact collection to the preparation end, then schedules the actual attack with an approach whose takeoff is closer to the net. Assert that the actor's new `ScheduledMovementDistance` is the remaining distance only and that its world Z never moves farther from the net after the second schedule.

Use these values for Blue:

```csharp
var preparedStart = new Vector3(0f, 0f, -3.1f);
var takeoff = new SimVector3(0f, 0f, -1.1f);
var contact = AttackContactPlanner.Plan(new AttackContactInput(
    3.42f, 1f, 1f, SetQualityGrade.A, takeoff, 0.4f, 1f));
```

The assertion must fail under the old reset-to-approach-start scheduling path.

- [x] **Step 2: Run the focused test and confirm RED**

Run the Unity EditMode command with its fully qualified test filter.

Expected: FAIL because `ScheduleContact` rebuilds `_movementStartPosition` and moves toward `AttackApproach.ApproachStart` again.

- [x] **Step 3: Add an explicit continuation scheduling path**

In `PrototypePlayerAgent`, add:

```csharp
public void ContinueAttackPreparation(
    AttackApproachPlan approach,
    AttackContactPlan contactPlan,
    float actualContactTime)
```

It must retain the current constrained root position as `_movementStartPosition`, set `_movementTargetPosition` to the staged point between the current position and `approach.Takeoff`, and call `ConfigureAttackApproach` without targeting `approach.ApproachStart`. It must not call `CancelScheduledContact` or reset the support-motion origin.

In `PhysicalMatchRallyDirector.ScheduleAttackFromActualSet`, call this continuation method before scheduling the contact. Construct the `resumedApproach` from the current root position only for the remaining path length; keep `replan.Approach.Takeoff` from Task 2.

- [x] **Step 4: Run the focused test and confirm GREEN**

Run the Task 3 focused command again.

Expected: PASS.

- [x] **Step 5: Add a PlayMode invariant for normal sets**

Extend `AttackChainCalibrationPlayModeTests` to record each A/B/C set's replay set-chain and expose a director counter `NearNetAttackPlans`. Assert that all normal attack plans use a takeoff depth in the role's resolved band and that `AGradeNoContactErrorRate` remains below the existing threshold.

- [x] **Step 6: Run focused calibration and commit**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests \
  -testResults "$PWD/TestResults/attack-approach.xml" \
  -logFile "$PWD/TestResults/attack-approach.log"
```

Expected: PASS for both 3v3 and 6v6 calibration cases.

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs
git commit -m "fix: preserve attack approach through set replans"
```

**Current status saved 2026-07-22:** Tasks 1--3 are complete on branch
`codex/geometric-counterplay`.

- Task 1 commits: `d5391ab`, `2c3d871`.
- Task 2 commits: `a21a350`, follow-up `7c9e580`.
- Task 3 commit: `e62008b`.
- Latest verification: `Volleyball.EditModeTests.PrototypePlayerContactSourceTests`
  28/28 passed; full EditMode 324/324 passed; `Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests`
  3/3 passed with 3v3 and 6v6 `nearNetAttackPlans=100/100` and
  `AGradeNoContactErrorRate=0.000`.
- Next task to execute: Task 4, starting at `SetTargetSelectorTests`.

## Task 4: Select Best Set Targets and Geometry-Driven Attack Routes

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/AI/SetTargetSelector.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/AttackRouteSelector.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/SetTargetSelectorTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/AttackRouteSelectorTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`

- [x] **Step 1: Write failing best-handling-point tests**

Create `SetTargetSelectorTests.cs` with a static arm capsule centred over the middle candidate and assert that the selector chooses a legal lateral point with the largest arm clearance:

```csharp
[Test]
public void Select_NearNetSetterTargetsTheLegalBandPointWithLargestArmClearance()
{
    var input = new SetTargetSelectionInput(
        TeamId.Blue,
        PlayerRole.Attacker,
        setterDepthFromNet: 1f,
        targetHeight: 3.35f,
        preferredX: 0f,
        predictedArms: ArmsAtCenterX(),
        lateralCandidates: new[] { -1f, 0f, 1f });

    var selected = SetTargetSelector.Select(input);

    Assert.That(selected.Target.X, Is.Not.EqualTo(0f));
    Assert.That(-selected.Target.Z, Is.InRange(0.75f, 1.5f));
    Assert.That(selected.MinimumArmClearance, Is.GreaterThan(0f));
}
```

Add a second test with setter depth `2.5f` asserting the selected depth remains within the ordinary band, and a third with depth `6f` asserting it uses the shifted band.

- [x] **Step 2: Run the selector fixture and confirm RED**

Run the Unity EditMode command with test filter `Volleyball.EditModeTests.SetTargetSelectorTests`.

Expected: compilation failure because the selector types do not exist.

- [x] **Step 3: Implement deterministic candidate scoring**

Create `SetTargetSelector.cs`. Its input accepts `IReadOnlyList<ContactCapsuleFrame>` rather than a Unity object. Generate depths `{NearDepth, midpoint, FarDepth}` and supplied lateral candidates. Reject candidates outside the court or whose capsule clearance is non-positive. Calculate clearance as:

```csharp
var closest = arm.ClosestPoint(candidate, out _);
var clearance = (candidate - closest).Magnitude -
                SimulatedBall.DefaultRadius - arm.Radius;
```

Score candidates by their minimum clearance, then by smaller lateral deviation from `preferredX`, then by closer-to-net depth; use lexical coordinates only as the final stable tie breaker. Return the highest scored legal candidate. Throw `InvalidOperationException` when no candidate is legal instead of inventing a random fallback.

- [x] **Step 4: Write the failing attack-route test**

Create `AttackRouteSelectorTests.cs` that supplies one central predicted arm capsule and asserts `AttackRouteSelector.Select` chooses `CrossCourt` or `OverHand` over the obstructed `Line` route, and returns the same result for two identical inputs.

- [x] **Step 5: Run the route fixture and confirm RED**

Run the Unity EditMode command with test filter `Volleyball.EditModeTests.AttackRouteSelectorTests`.

Expected: compilation failure because `AttackRouteSelector` does not exist.

- [x] **Step 6: Implement route scoring with real ball geometry**

Create `AttackRouteSelector.cs` with route candidates `Line`, `CrossCourt`, `OverHand`, and `EdgeLeft`/`EdgeRight`. For each candidate, use `ReturnVelocitySolver.Solve` with a route-specific legal landing target and flight time. Integrate a cloned `BallState` in fixed steps until it reaches the net plane or ground. Reject an out-of-bounds landing or antenna-width crossing. Score by sampled minimum capsule clearance; `OverHand` receives a slower valid flight time, while edge routes are allowed to have near-zero clearance but never receive points or a score bonus for collision. Choose the greatest legal score with a fixed enum-order tie break.

- [x] **Step 7: Expose predicted arm snapshots without changing collision authority**

Add this read-only preview to `PrototypePlayerAgent`:

```csharp
public IReadOnlyList<ContactCapsuleFrame> PreviewBlockArmFrames(
    float simulationTime,
    Vector3 rootPosition)
```

It temporarily applies the block pose and root position, captures `BlockArmContactVolumes`, returns their `Current` frames, and restores the transform and rig rotations in `finally`. It must not open a contact window or mutate a live ball state.

- [x] **Step 8: Integrate target and route selectors in the director**

In `PhysicalMatchRallyDirector`, collect predicted defender arm frames at the future attack time, pass them to `SetTargetSelector` while scheduling a Set, and pass the selected target to `SetFlightSolver`. Before scheduling an Attack, call `AttackRouteSelector` and use its selected outgoing velocity. If either selector finds no legal geometry candidate, retain the existing deterministic solver path and log the explicit fallback reason.

- [x] **Step 9: Run focused EditMode suites and commit**

Run both new fixtures plus `SetFlightSolverTests` and `TeamRallyDecisionPlannerTests`; all must pass.

```bash
git add Assets/Volleyball/Match/Runtime/AI/SetTargetSelector.cs Assets/Volleyball/Match/Runtime/AI/AttackRouteSelector.cs Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs Assets/Volleyball/Match/Tests/EditMode/SetTargetSelectorTests.cs Assets/Volleyball/Match/Tests/EditMode/AttackRouteSelectorTests.cs
git commit -m "feat: select sets and attacks from block geometry"
```

## Task 5: Plan Multi-Block Units and Correct Post-Block Possession Timing

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/AI/BlockUnitPlanner.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/BlockUnitPlannerTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `docs/rules.md`
- Create: `docs/changes/2026-07-22-002-geometric-attack-block-counterplay.md`

- [ ] **Step 1: Write failing block-unit tests**

Create `BlockUnitPlannerTests.cs` with three pure cases:

```csharp
[Test]
public void Select_UsesThreeDistinctReachablePlayersWhenTheyCoverAdjacentLanes()
{
    var unit = BlockUnitPlanner.Select(ThreeReachableFrontPlayers(), intercept: new SimVector3(0f, 3.1f, 0f), 0.8f);
    Assert.That(unit.Blockers.Count, Is.EqualTo(3));
    Assert.That(unit.Blockers.Select(player => player.Id).Distinct().Count(), Is.EqualTo(3));
}

[Test]
public void Select_ExcludesUnreachablePlayers()
{
    var unit = BlockUnitPlanner.Select(OneReachableAndTwoDistantPlayers(), new SimVector3(0f, 3.1f, 0f), 0.35f);
    Assert.That(unit.Blockers.Count, Is.EqualTo(1));
}

[Test]
public void Select_ExcludesBackRowPlayersWhenFormalSixVsSixIsRequested()
{
    var unit = BlockUnitPlanner.Select(FrontAndBackRowPlayers(), new SimVector3(0f, 3.1f, 0f), 0.8f, requireFrontRow: true);
    Assert.That(unit.Blockers.All(player => player.IsFrontRow), Is.True);
}
```

- [ ] **Step 2: Run the block-unit fixture and confirm RED**

Run the Unity EditMode command with test filter `Volleyball.EditModeTests.BlockUnitPlannerTests`.

Expected: compilation failure because block-unit types do not exist.

- [ ] **Step 3: Implement the pure unit selector**

Create `BlockUnitPlanner.cs` with a `BlockCandidateSnapshot` (`PlayerId`, position, movement speed, jump, `IsFrontRow`) and `BlockUnitPlan`. Filter candidates by team, front-row requirement, and `distance <= movementSpeed * availableSeconds`. Sort reachable candidates by distance minus jump bonus, then player identity. Choose the primary closest candidate; select at most one candidate on each side of its X lane, requiring an X separation of at least `0.35f`. Return one to three entries in stable order.

- [ ] **Step 4: Run the block-unit fixture and confirm GREEN**

Run the Task 5 focused command again.

Expected: PASS.

- [ ] **Step 5: Write a failing post-block state test**

Add a PlayMode test that creates a real accepted block whose outbound velocity points into the opponent court, then observes before ground contact that the director has no receive `ContactWindow` and that no emergency receiver is enabled. Continue until ground contact and assert the blocking team receives the point through the existing rally result.

- [ ] **Step 6: Run the focused PlayMode test and confirm RED**

Run the Unity PlayMode command with the new test filter.

Expected: FAIL because `HandleAcceptedBlock` immediately calls `BeginPossession`.

- [ ] **Step 7: Schedule all selected blockers and defer possession transition**

Replace the single `_scheduledBlocker` field with `HashSet<PlayerId> _scheduledBlockers`. `PreparePhysicalBlock` selects a `BlockUnitPlan` and schedules each player with its own contact group. `SchedulePhysicalBlock` retargets each selected blocker and opens one `RallyContactWindow` whose eligible actors are all selected blockers.

In `HandleAcceptedBlock`, disable all scheduled block windows and clear scheduling state, but remove the direct `BeginPossession(reboundTeam, ReceiveLeadTime())` call. Retain the block as `LastPhysicalTouch`. In `HandleNetPlaneCrossing`, when the final touch is Block and the crossing is legal, start `BeginPossession(receivingTeam, ReceiveLeadTime())`; ground and antenna paths already resolve through `MatchRallyReferee`. Ensure the existing pending-crossing timeout path never opens a possession merely because a block window expired.

- [ ] **Step 8: Add and run 3v3/6v6 integration assertions**

Extend 3v3 PlayMode coverage to observe at least one scheduled two-or-more-player block unit in a fixed-seed match. Extend 6v6 coverage to assert every scheduled blocker is front row and that a fixed-seed match records at least one two-player unit. Preserve the existing assertion that a single physical collision produces only one block contact/effect.

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests \
  -testResults "$PWD/TestResults/block-3v3.xml" \
  -logFile "$PWD/TestResults/block-3v3.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests \
  -testResults "$PWD/TestResults/block-6v6.xml" \
  -logFile "$PWD/TestResults/block-6v6.log"
```

Expected: both fixtures PASS; no duplicate block events and no back-row blocker in 6v6.

- [ ] **Step 9: Remove resolved deviations, document evidence, and commit**

Remove the R-REF-004 and R-OFF-002/R-OFF-004 resolved rows from `docs/rules.md`. Write the change record with the exact Unity test counts, fixed-seed scene result, attack route counts, multi-block unit counts, and any remaining calibration risk.

```bash
git add Assets/Volleyball/Match/Runtime/AI/BlockUnitPlanner.cs Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs Assets/Volleyball/Match/Tests/EditMode/BlockUnitPlannerTests.cs Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs docs/rules.md docs/changes/2026-07-22-002-geometric-attack-block-counterplay.md
git commit -m "feat: add geometric multi-block counterplay"
```

## Task 6: Full Regression, Calibration, and Documentation Verification

**Files:**
- Modify: `docs/changes/2026-07-22-002-geometric-attack-block-counterplay.md`
- Modify: `docs/rules.md`

- [ ] **Step 1: Run complete EditMode regression**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/geometric-counterplay-edit.xml" \
  -logFile "$PWD/TestResults/geometric-counterplay-edit.log"
```

Expected: all EditMode tests PASS.

- [ ] **Step 2: Run complete PlayMode regression**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/geometric-counterplay-play.xml" \
  -logFile "$PWD/TestResults/geometric-counterplay-play.log"
```

Expected: all PlayMode tests PASS, including 100-sample 3v3/6v6 calibration and 20-set symmetry.

- [ ] **Step 3: Inspect results and document actual evidence**

Read the XML result files and logs. Record only observed values in the change record: passed/failed totals, fixed-seed scores, physical block count, multi-block-unit count, selected route counts, attackable-set rate, A-grade no-contact error rate, and symmetry wins. Do not copy historical test counts.

- [ ] **Step 4: Run repository hygiene checks**

```bash
git diff --check
git status --short
```

Expected: no whitespace error; only the intended change record/rules edits remain before the final commit.

- [ ] **Step 5: Commit verification documentation**

```bash
git add docs/rules.md docs/changes/2026-07-22-002-geometric-attack-block-counterplay.md
git commit -m "docs: verify geometric attack block counterplay"
```

## Plan Self-Review

- R-GOV-001 is covered by Task 1 and its compliance-row removal.
- R-REF-004/005 are covered by Task 5's deferred possession state test and existing referee paths.
- R-PLAY-003 is covered by Task 5 pure and scene tests.
- R-OFF-001/002/003 are covered by Task 2 and Task 4 pure selectors.
- R-OFF-004 is covered by Task 3's no-reset test and calibration assertion.
- R-OFF-005 is covered by Task 4's capsule-clearance route test and director integration.
- The plan contains no intentional probabilistic variation, random tactical choice, score bypass, or new scoring rule.
