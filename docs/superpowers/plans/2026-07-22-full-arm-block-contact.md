# Full-Arm Block Contact Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace palm-only block collision with deterministic swept capsule contact for both palms, forearms, and upper arms.

**Architecture:** Add an immutable moving-capsule snapshot and a fixed-step swept sphere-versus-capsule solver in the simulation domain. Extend ball contact candidates to dispatch either the existing oriented plane solver or the new capsule solver, then expose six rig-driven block capsules from each active blocker while reusing the existing block referee, response, replay, and feedback path.

**Tech Stack:** Unity 6000.0.43f1, C# 9, NUnit EditMode tests, Unity PlayMode tests, custom 120 Hz deterministic ball simulation.

## Global Constraints

- Only left/right palms, forearms, and upper arms participate in this change.
- Head, torso, hips, legs, and feet remain excluded.
- All six arm volumes use `TechniqueAction.Block` and one shared contact group.
- Block contact does not consume one of the team's three counted contacts.
- Unity physics colliders remain disabled; custom fixed-step physics is authoritative.
- Shared V1/V2 contracts and replay schema stay unchanged.
- Remove the abandoned palm-height-specific jump adjustment before adding the new model.

---

### Task 1: Moving Capsule Snapshot and Swept Collision

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/Simulation/ContactCapsuleSnapshot.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/Simulation/ContactCapsuleSnapshot.cs.meta`
- Create: `Assets/Volleyball/Match/Runtime/Domain/Simulation/SweptBallCapsuleCollision.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/Simulation/SweptBallCapsuleCollision.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/SweptBallCapsuleCollisionTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/SweptBallCapsuleCollisionTests.cs.meta`

**Interfaces:**
- Consumes: `BallState`, `SimVector3`, and `SweptBallHit` from `Volleyball.Domain.Simulation`.
- Produces: `ContactCapsuleFrame`, `ContactCapsuleSnapshot`, and `SweptBallCapsuleCollision.TryFindContact(BallState, ContactCapsuleSnapshot, float, out SweptBallHit)`.

- [x] **Step 1: Write failing capsule collision tests**

Add tests that construct real ball states and capsule snapshots:

```csharp
[Test]
public void TryFindContact_BallCrossesForearmCapsule_ReturnsEarliestHit()
{
    var ball = new BallState(new SimVector3(0f, 2.2f, 0.20f), new SimVector3(0f, 0f, -24f), 0.12f);
    ball.Step(new BallSimulationParameters(0f, 1f), 1f / 120f);
    var frame = new ContactCapsuleFrame(
        new SimVector3(-0.35f, 2.0f, 0f),
        new SimVector3(0.35f, 2.4f, 0f),
        0.065f);
    var capsule = new ContactCapsuleSnapshot(frame, frame, true, 701);

    Assert.That(
        SweptBallCapsuleCollision.TryFindContact(ball, capsule, 1f / 120f, out var hit),
        Is.True);
    Assert.That(hit.ContactGroupId, Is.EqualTo(701));
    Assert.That(hit.TimeFraction, Is.InRange(0f, 1f));
    Assert.That(hit.Normal.IsFinite, Is.True);
}

[Test]
public void TryFindContact_SideSwipeOnPalmCapsule_IsTwoSided()
{
    var ball = new BallState(new SimVector3(-0.20f, 2.5f, 0f), new SimVector3(24f, 0f, 0f), 0.12f);
    ball.Step(new BallSimulationParameters(0f, 1f), 1f / 120f);
    var frame = new ContactCapsuleFrame(
        new SimVector3(0f, 2.46f, -0.04f),
        new SimVector3(0f, 2.54f, 0.04f),
        0.11f);

    Assert.That(
        SweptBallCapsuleCollision.TryFindContact(
            ball,
            new ContactCapsuleSnapshot(frame, frame, true, 702),
            1f / 120f,
            out _),
        Is.True);
}

[Test]
public void TryFindContact_InactiveCapsule_ReturnsFalse()
{
    var ball = new BallState(new SimVector3(0f, 2.2f, 0.20f), new SimVector3(0f, 0f, -24f), 0.12f);
    ball.Step(new BallSimulationParameters(0f, 1f), 1f / 120f);
    var frame = new ContactCapsuleFrame(
        new SimVector3(-0.35f, 2.0f, 0f),
        new SimVector3(0.35f, 2.4f, 0f),
        0.065f);

    Assert.That(
        SweptBallCapsuleCollision.TryFindContact(
            ball,
            new ContactCapsuleSnapshot(frame, frame, false, 703),
            1f / 120f,
            out _),
        Is.False);
}
```

- [x] **Step 2: Run tests and verify RED**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.SweptBallCapsuleCollisionTests" \
  -testResults "$PWD/TestResults/ArmCapsule-red.xml" \
  -logFile "$PWD/TestResults/ArmCapsule-red.log"
```

Expected: compilation fails because `ContactCapsuleFrame`, `ContactCapsuleSnapshot`, and `SweptBallCapsuleCollision` do not exist.

- [x] **Step 3: Implement immutable capsule snapshots**

Create `ContactCapsuleSnapshot.cs` with validated finite endpoints, positive radius, linear interpolation, and point velocity:

```csharp
public readonly struct ContactCapsuleFrame
{
    public ContactCapsuleFrame(SimVector3 start, SimVector3 end, float radius) { /* validate and assign */ }
    public SimVector3 Start { get; }
    public SimVector3 End { get; }
    public float Radius { get; }
    public static ContactCapsuleFrame Lerp(ContactCapsuleFrame previous, ContactCapsuleFrame current, float alpha) { /* linear interpolation */ }
    public SimVector3 ClosestPoint(SimVector3 point, out float segmentFraction) { /* clamped projection */ }
}

public readonly struct ContactCapsuleSnapshot
{
    public ContactCapsuleSnapshot(ContactCapsuleFrame previous, ContactCapsuleFrame current, bool active, int contactGroupId) { /* assign */ }
    public ContactCapsuleFrame Previous { get; }
    public ContactCapsuleFrame Current { get; }
    public bool Active { get; }
    public int ContactGroupId { get; }
    public ContactCapsuleFrame At(float alpha) => ContactCapsuleFrame.Lerp(Previous, Current, alpha);
    public SimVector3 VelocityAt(float segmentFraction, float deltaSeconds) { /* same axis fraction in previous/current */ }
}
```

- [x] **Step 4: Implement deterministic swept sphere-versus-moving-capsule collision**

Create `SweptBallCapsuleCollision.cs`. Evaluate clearance at 16 equal time subdivisions, find the first interval whose expanded capsule contains the ball center, and refine its first impact with 10 bisection iterations. Build `SweptBallHit` from the closest capsule-axis point, outward normal, capsule-point velocity, shared group ID, and centeredness `1f`. If the normal length is zero, use the negative relative velocity; if that is also zero, use `SimVector3.Up`.

- [x] **Step 5: Run tests and verify GREEN**

Run the Task 1 command again.

Expected: `SweptBallCapsuleCollisionTests` passes with no compiler errors or warnings.

- [x] **Step 6: Commit Task 1**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/Simulation/ContactCapsuleSnapshot.cs* \
  Assets/Volleyball/Match/Runtime/Domain/Simulation/SweptBallCapsuleCollision.cs* \
  Assets/Volleyball/Match/Tests/EditMode/SweptBallCapsuleCollisionTests.cs*
git commit -m "feat: add swept arm capsule collision"
```

### Task 2: Capsule Ball Contact Candidates

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/SimulatedBallTests.cs`

**Interfaces:**
- Consumes: `ContactCapsuleSnapshot` and `SweptBallCapsuleCollision.TryFindContact` from Task 1.
- Produces: a `BallContactCandidate` capsule constructor, `IsCapsule`, `Capsule`, and earliest-contact dispatch for plane or capsule candidates.

- [x] **Step 1: Write a failing ball integration test**

Add a test contact source that emits two overlapping capsule candidates with the same group, then assert that one fixed step raises exactly one `PlayerContact` and records the capsule group ID. Construct candidates with:

```csharp
new BallContactCandidate(
    capsuleSnapshot,
    TechniqueAction.Block,
    actor,
    0.8f,
    new SimVector3(0f, 5.5f, -6.5f),
    new SimVector3(0f, 0f, -1f),
    new ContactResponseParameters(0.65f, 0.8f, 0.22f, 0.08f));
```

- [x] **Step 2: Run the integration test and verify RED**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.SimulatedBallTests" \
  -testResults "$PWD/TestResults/ArmCandidate-red.xml" \
  -logFile "$PWD/TestResults/ArmCandidate-red.log"
```

Expected: compilation fails because `BallContactCandidate` has no capsule constructor.

- [x] **Step 3: Add the capsule candidate variant**

Keep all existing surface constructors and properties compatible. Add a capsule constructor and discriminant:

```csharp
public bool IsCapsule { get; }
public ContactCapsuleSnapshot Capsule { get; }
```

The existing `Surface` property remains unchanged for existing candidates. Capsule candidates set `IsCapsule = true`; plane candidates keep it false.

- [x] **Step 4: Dispatch the matching collision solver**

In `TryFindEarliestPlayerContact`, replace the direct plane call with:

```csharp
SweptBallHit hit;
var hasContact = candidate.IsCapsule
    ? SweptBallCapsuleCollision.TryFindContact(State, candidate.Capsule, deltaSeconds, out hit)
    : SweptBallCollision.TryFindContact(State, candidate.Surface, deltaSeconds, out hit);
if (!hasContact)
{
    continue;
}
```

Keep resolver evaluation and earliest `TimeFraction` selection unchanged.

- [x] **Step 5: Run Task 2 and Task 1 tests and verify GREEN**

Run with filter:

```text
Volleyball.EditModeTests.SimulatedBallTests|Volleyball.EditModeTests.SweptBallCapsuleCollisionTests
```

Expected: both fixtures pass and overlapping capsules emit one accepted contact.

- [x] **Step 6: Commit Task 2**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs \
  Assets/Volleyball/Match/Tests/EditMode/SimulatedBallTests.cs
git commit -m "feat: resolve capsule ball contacts"
```

### Task 3: Rig-Driven Full-Arm Block Volumes

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/BlockArmContactVolumes.cs`
- Create: `Assets/Volleyball/Match/Runtime/Presentation/BlockArmContactVolumes.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/BlockArmContactVolumesTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/BlockArmContactVolumesTests.cs.meta`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

**Interfaces:**
- Consumes: `ContactCapsuleFrame`, `ContactCapsuleSnapshot`, the capsule `BallContactCandidate` constructor, and visible `StickFigureRig` joints.
- Produces: `BlockArmContactVolumes.Capture(bool active, int contactGroupId)` returning six capsule snapshots.

- [x] **Step 1: Remove the abandoned palm-height experiment**

Restore `ScheduleBlockContact` and `RetargetBlockContact` to their original signatures without `desiredContactHeight`; remove `_physicalBlockJumpHeight`, `ResolveBlockJumpHeight`, the two director height arguments, and `ScheduledBlockContact_UsesAStandingBlockForALowNetCrossing`. Restore `EvaluateSupportBlockJump` to:

```csharp
var jumpHeight = 0.30f + (Ability.Jump * 0.20f);
return jumpHeight * 4f * jumpProgress * (1f - jumpProgress);
```

- [x] **Step 2: Write failing visible-arm volume tests**

Create `BlockArmContactVolumesTests` and assert:

```csharp
var rig = StickFigureRig.Create(player.transform, Color.blue, "4");
rig.SetPose(StickFigurePose.Block, 1f);
var volumes = new BlockArmContactVolumes(rig).Capture(true, 801);

Assert.That(volumes, Has.Count.EqualTo(6));
Assert.That(volumes, Has.All.Matches<ContactCapsuleSnapshot>(volume =>
    volume.Active && volume.ContactGroupId == 801));
Assert.That(volumes[0].Current.Start, Is.EqualTo(ToSimulation(rig.GetJoint("LeftShoulder").position)));
Assert.That(volumes[0].Current.End, Is.EqualTo(ToSimulation(rig.GetJoint("LeftElbow").position)));
```

Move the player between captures and assert the second snapshot's `Previous` is the first capture and `Current` follows the moved visible joints.

- [x] **Step 3: Run volume tests and verify RED**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.BlockArmContactVolumesTests" \
  -testResults "$PWD/TestResults/BlockArmVolumes-red.xml" \
  -logFile "$PWD/TestResults/BlockArmVolumes-red.log"
```

Expected: compilation fails because `BlockArmContactVolumes` does not exist.

- [x] **Step 4: Implement six rig-driven capsule snapshots**

Create `BlockArmContactVolumes` with a previous-frame dictionary and these exact segments:

```csharp
("LeftUpperArm", "LeftShoulder", "LeftElbow", 0.065f)
("LeftForearm", "LeftElbow", "LeftHand", 0.065f)
("LeftPalm", "LeftHand", "LeftPalm", 0.11f)
("RightUpperArm", "RightShoulder", "RightElbow", 0.065f)
("RightForearm", "RightElbow", "RightHand", 0.065f)
("RightPalm", "RightHand", "RightPalm", 0.11f)
```

Each capture builds `ContactCapsuleFrame` from current joint world positions, uses the stored frame as `Previous` when available, stores the new frame, and returns snapshots with the supplied active flag and group ID.

- [x] **Step 5: Write the failing player-source test**

Replace `ScheduledBlockContact_EmitsTwoActivePalmsOnlyInsideItsWindow` with `ScheduledBlockContact_EmitsSixArmVolumesOnlyInsideItsWindow`. Assert the pre-contact list is empty, the active list has six capsule candidates, every candidate is `TechniqueAction.Block`, every candidate has `IsCapsule == true`, and all capsule snapshots share one contact group.

- [x] **Step 6: Integrate arm volumes into the player agent**

Initialize one `BlockArmContactVolumes` beside `PlayerContactSurfaces`. In `CollectPhysicalBlockContacts`, capture the six arm volumes instead of `ContactSurfaces.Capture(TechniqueAction.Block, ...)`, and emit one capsule `BallContactCandidate` for each active volume using the existing block target velocity, technique, strike direction, and response parameters. Do not add new counters or referee paths.

- [x] **Step 7: Run focused tests and verify GREEN**

Run with filter:

```text
Volleyball.EditModeTests.BlockArmContactVolumesTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests|Volleyball.EditModeTests.SimulatedBallTests|Volleyball.EditModeTests.SweptBallCapsuleCollisionTests
```

Expected: all focused fixtures pass; no arm candidates appear outside the action window.

- [x] **Step 8: Commit Task 3**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/BlockArmContactVolumes.cs* \
  Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Tests/EditMode/BlockArmContactVolumesTests.cs* \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "feat: use full-arm block contact volumes"
```

### Task 4: Scene Calibration, Documentation, and Final Verification

**Files:**
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs` only if diagnostics expose a test defect; do not lower thresholds.
- Modify: `docs/superpowers/plans/2026-07-21-unified-attack-chain.md`
- Modify: `docs/superpowers/handoffs/2026-07-21-unified-attack-chain-task7-checkpoint.md`
- Create: `docs/changes/2026-07-21-001-unified-attack-chain.md`
- Modify: `docs/changes/README.md`
- Modify: `docs/development.md`

**Interfaces:**
- Consumes: the completed capsule contact path and all existing Task 7 counters.
- Produces: final regression evidence, completed Task 7 documentation, and a clean feature branch checkpoint.

**Calibration note:** blockers use a 0.18 m root clearance from the center line,
face square to the net when scheduled, and use a visible forward/inward block
pose whose palms cross the net plane and whose forearms close the central seam.
Capsule radii remain tied to the visible arm and palm dimensions.

- [x] **Step 1: Run the ordinary 3v3 regression**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests.PhysicalLoop_UsesSixPlayersOneBallAndSwitchableCameras" \
  -testResults "$PWD/TestResults/FullArmBlock-3v3.xml" \
  -logFile "$PWD/TestResults/FullArmBlock-3v3.log"
```

Expected: PASS with `PhysicalBlockContacts > 0`, `NonSetterSetContacts > 0`, and `DefenderAttackContacts > 0` without weakening assertions.

- [x] **Step 2: Run both 100-sample calibrations and 20-set symmetry**

Run the complete `Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests` fixture.

Expected: 3/3 pass; both scenes collect 100 in-system setter contacts, attackable rate is at least 0.95, A-grade no-contact rate is below 0.02, normal side sets are zero, and Blue wins 9-11 of 20 symmetric sets.

- [x] **Step 3: Run full EditMode**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/UnifiedAttack-EditMode-final.xml" \
  -logFile "$PWD/TestResults/UnifiedAttack-EditMode-final.log"
```

Expected: every discovered EditMode test passes.

- [x] **Step 4: Run the selected final PlayMode suite**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.AttackChainCalibrationPlayModeTests|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests" \
  -testResults "$PWD/TestResults/UnifiedAttack-PlayMode-final.xml" \
  -logFile "$PWD/TestResults/UnifiedAttack-PlayMode-final.log"
```

Expected: all selected tests pass with no time-scale leakage, non-finite ball state, replay validation error, or score-cap regression.

- [x] **Step 5: Complete documentation**

Record the full-arm capsule model, the absolute 50-point cap, final XML counts, calibration rates, branch name, compatibility statement, and any remaining integration work. Mark Task 7 complete only after Steps 1-4 pass.

- [x] **Step 6: Commit final calibration and documentation**

```bash
git add Assets/Volleyball/Match/Tests/PlayMode/AttackChainCalibrationPlayModeTests.cs \
  docs/superpowers/plans/2026-07-21-unified-attack-chain.md \
  docs/superpowers/handoffs/2026-07-21-unified-attack-chain-task7-checkpoint.md \
  docs/changes/2026-07-21-001-unified-attack-chain.md \
  docs/changes/README.md docs/development.md
git commit -m "feat: complete unified attack-chain calibration"
```

## Plan Self-Review

- Spec coverage: Tasks 1-3 cover all six approved arm/hand volumes, deterministic collision, one shared group, existing block semantics, and exclusion of other body parts. Task 4 covers every required scene, calibration, replay, score-cap, and documentation check.
- Placeholder scan: all public types, signatures, segment radii, solver subdivision counts, test filters, expected outcomes, and commit scopes are explicit.
- Type consistency: `ContactCapsuleFrame`, `ContactCapsuleSnapshot`, `SweptBallCapsuleCollision`, `BallContactCandidate.IsCapsule`, and `BlockArmContactVolumes.Capture` are named consistently from creation through integration.
