# Unified Multi-Role Rally Decision Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Replace the fixed physical 3v3 contact sequence with deterministic, position-aware multi-role decisions, legal three-touch state, and real zero-count block contacts.

**Architecture:** Keep coordinate transforms and rally legality in Unity-free Match Domain. Keep candidate scoring, approach quality, and bounded tactical weights in Match AI. Presentation schedules movement and contact windows, but SimulatedBall rejects illegal contacts before applying a physical response. The director becomes a possession orchestrator rather than a role-indexed six-contact loop.

**Tech Stack:** Unity 6000.0.43f1, C#, Unity Assembly Definitions, NUnit EditMode tests, Unity PlayMode tests, existing MatchSet and MatchResultV1.

---

## File Structure

- Create: Assets/Volleyball/Match/Runtime/Domain/Prototype/TeamCourtFrame.cs - world/local court conversion; only Z mirrors.
- Create: Assets/Volleyball/Match/Runtime/Domain/Prototype/RallyTouchState.cs - legal-contact windows, counted touches, blocks, and immediate fault evaluation.
- Create: Assets/Volleyball/Match/Runtime/Domain/Simulation/NetPlaneInterception.cs - world-space net-plane trajectory prediction.
- Create: Assets/Volleyball/Match/Runtime/AI/RallyTacticalWeights.cs - validated, bounded future-LLM tactical preferences with deterministic fallback.
- Create: Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs - immutable player snapshots, candidate score breakdowns, and attack approach plans.
- Modify: Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs - use TeamCourtFrame for new team-depth conversion while retaining TeamSideSign compatibility.
- Modify: Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs - resolve Ignore / Accept / Fault before velocity response and order same-step events by time fraction.
- Modify: Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs - physical block windows, bounded block retargeting, and approach-plan consumption.
- Modify: Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs - dynamic possession planning, real player attribution, and block rebound handling.
- Create: Assets/Volleyball/Match/Tests/EditMode/TeamCourtFrameTests.cs.
- Create: Assets/Volleyball/Match/Tests/EditMode/RallyTouchStateTests.cs.
- Create: Assets/Volleyball/Match/Tests/EditMode/NetPlaneInterceptionTests.cs.
- Create: Assets/Volleyball/Match/Tests/EditMode/RallyTacticalWeightsTests.cs.
- Create: Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs.
- Modify: Assets/Volleyball/Match/Tests/EditMode/EnvironmentCollisionTests.cs.
- Modify: Assets/Volleyball/Match/Tests/EditMode/SimulatedBallTests.cs.
- Modify: Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs.
- Modify: Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs.
- Modify: Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs.
- Create: docs/changes/2026-07-19-002-unified-multi-role-rally-decision.md.
- Modify: docs/changes/README.md and docs/development.md.

Unity must generate and retain .meta files for every created Asset. Do not hand-author GUIDs.

### Task 1: Add the Coordinate Contract

**Files:**
- Create: Assets/Volleyball/Match/Runtime/Domain/Prototype/TeamCourtFrame.cs
- Create: Assets/Volleyball/Match/Tests/EditMode/TeamCourtFrameTests.cs
- Modify: Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs
- Modify: Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs

- [ ] **Step 1: Write failing coordinate tests**

Create TeamCourtFrameTests.cs:

~~~csharp
[TestCase(TeamId.Blue, -4f, -4f)]
[TestCase(TeamId.Orange, 4f, -4f)]
public void ToLocal_PreservesWorldXAndYAndMirrorsOnlyWorldZ(
    TeamId team,
    float worldZ,
    float expectedLocalZ)
{
    var local = new TeamCourtFrame(team).ToLocal(
        new SimVector3(2.25f, 1.75f, worldZ));

    Assert.That(local.X, Is.EqualTo(2.25f));
    Assert.That(local.Y, Is.EqualTo(1.75f));
    Assert.That(local.Z, Is.EqualTo(expectedLocalZ));
}

[TestCase(TeamId.Blue)]
[TestCase(TeamId.Orange)]
public void ToWorld_RoundTripsLocalCoordinates(TeamId team)
{
    var frame = new TeamCourtFrame(team);
    var world = new SimVector3(
        -2.5f,
        2.1f,
        team == TeamId.Blue ? -3.2f : 3.2f);

    Assert.That(frame.ToWorld(frame.ToLocal(world)), Is.EqualTo(world));
}

[TestCase(TeamId.Blue, -2.5f)]
[TestCase(TeamId.Orange, 2.5f)]
public void ToLocalDepth_MapsOwnCourtToNegativeZ(
    TeamId team,
    float worldDepth)
{
    Assert.That(
        new TeamCourtFrame(team).ToLocalDepth(worldDepth),
        Is.LessThan(0f));
}
~~~

Add a PhysicalRallyTacticPlannerTests assertion that the existing TeamId overload produces negative Blue world Z and positive Orange world Z.

- [ ] **Step 2: Run the focused suite and verify red**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.TeamCourtFrameTests" \
  -testResults "$PWD/TestResults/TeamCourtFrame-red.xml" \
  -logFile "$PWD/TestResults/TeamCourtFrame-red.log"
~~~

Expected: compilation fails because TeamCourtFrame does not exist.

- [ ] **Step 3: Implement the Domain frame and compatibility overload**

Create TeamCourtFrame.cs with this complete public API:

~~~csharp
using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.Domain.Prototype
{
    public readonly struct TeamCourtFrame : IEquatable<TeamCourtFrame>
    {
        public TeamCourtFrame(TeamId team)
        {
            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(nameof(team));
            }

            Team = team;
        }

        public TeamId Team { get; }

        public float WorldDepthSign => Team == TeamId.Blue ? -1f : 1f;

        public SimVector3 ToLocal(SimVector3 world)
        {
            return new SimVector3(world.X, world.Y, ToLocalDepth(world.Z));
        }

        public SimVector3 ToWorld(SimVector3 local)
        {
            return new SimVector3(local.X, local.Y, ToWorldDepth(local.Z));
        }

        public float ToLocalDepth(float worldDepth)
        {
            return -WorldDepthSign * worldDepth;
        }

        public float ToWorldDepth(float localDepth)
        {
            return -WorldDepthSign * localDepth;
        }

        public bool Equals(TeamCourtFrame other)
        {
            return Team == other.Team;
        }

        public override bool Equals(object obj)
        {
            return obj is TeamCourtFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Team;
        }
    }
}
~~~

Add PlanBlockCoverage(CourtPoint opponentAttackPosition, TeamId defendingTeam) in PhysicalRallyTacticPlanner. Derive the sign through new TeamCourtFrame(defendingTeam).WorldDepthSign. Retain the existing TeamSideSign overload as a forwarder to TeamId, so no existing public AI caller or current test changes behavior.

- [ ] **Step 4: Run green and commit**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.TeamCourtFrameTests|Volleyball.EditModeTests.PhysicalRallyTacticPlannerTests" \
  -testResults "$PWD/TestResults/TeamCourtFrame-green.xml" \
  -logFile "$PWD/TestResults/TeamCourtFrame-green.log"
git add Assets/Volleyball/Match/Runtime/Domain/Prototype/TeamCourtFrame.cs \
  Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs \
  Assets/Volleyball/Match/Tests/EditMode/TeamCourtFrameTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs
git commit -m "feat: add team court coordinate frame"
~~~

Expected: both test classes pass; Unity creates the new .meta files.

### Task 2: Add Pure Three-Touch and Block Rule State

**Files:**
- Create: Assets/Volleyball/Match/Runtime/Domain/Prototype/RallyTouchState.cs
- Create: Assets/Volleyball/Match/Tests/EditMode/RallyTouchStateTests.cs

- [ ] **Step 1: Write failing legality tests**

Create tests covering a normal three-touch sequence, the fourth-touch fault, consecutive actor fault, stale candidate ignore, and block reset:

~~~csharp
[Test]
public void Accept_CountsThreeNormalTouchesAndFaultsTheFourthBeforePhysics()
{
    var defender = new PlayerId(TeamId.Blue, PlayerRole.Defender);
    var setter = new PlayerId(TeamId.Blue, PlayerRole.Setter);
    var attacker = new PlayerId(TeamId.Blue, PlayerRole.Attacker);
    var state = new RallyTouchState(TeamId.Blue);

    state.OpenWindow(Window(TeamId.Blue, TechniqueAction.Receive, 0f, 1f, defender));
    Assert.That(state.Accept(defender, TechniqueAction.Receive, 0.5f).Disposition,
        Is.EqualTo(RallyContactDisposition.Accept));
    state.OpenWindow(Window(TeamId.Blue, TechniqueAction.Set, 1f, 2f, setter));
    Assert.That(state.Accept(setter, TechniqueAction.Set, 1.5f).Disposition,
        Is.EqualTo(RallyContactDisposition.Accept));
    state.OpenWindow(Window(TeamId.Blue, TechniqueAction.Attack, 2f, 3f, attacker));
    Assert.That(state.Accept(attacker, TechniqueAction.Attack, 2.5f).Disposition,
        Is.EqualTo(RallyContactDisposition.Accept));
    state.OpenWindow(Window(TeamId.Blue, TechniqueAction.Receive, 3f, 4f, defender));

    var fourth = state.Evaluate(defender, TechniqueAction.Receive, 3.5f);

    Assert.That(fourth.Disposition, Is.EqualTo(RallyContactDisposition.Fault));
    Assert.That(fourth.Reason, Is.EqualTo(RallyContactRejectionReason.FourthCountedTouch));
    Assert.That(state.CountedTeamTouches, Is.EqualTo(3));
}

[Test]
public void Evaluate_FaultsConsecutiveActorButIgnoresWrongWindowActor()
{
    var defender = new PlayerId(TeamId.Blue, PlayerRole.Defender);
    var setter = new PlayerId(TeamId.Blue, PlayerRole.Setter);
    var state = new RallyTouchState(TeamId.Blue);

    state.OpenWindow(Window(TeamId.Blue, TechniqueAction.Receive, 0f, 1f, defender));
    state.Accept(defender, TechniqueAction.Receive, 0.5f);
    state.OpenWindow(Window(TeamId.Blue, TechniqueAction.Set, 1f, 2f, defender));

    Assert.That(state.Evaluate(defender, TechniqueAction.Set, 1.5f).Disposition,
        Is.EqualTo(RallyContactDisposition.Fault));
    Assert.That(state.Evaluate(setter, TechniqueAction.Set, 1.5f).Disposition,
        Is.EqualTo(RallyContactDisposition.Ignore));
}

[TestCase(TeamId.Blue)]
[TestCase(TeamId.Orange)]
public void Accept_BlockConsumesNoTouchAndReboundPossessionStartsAtZero(TeamId reboundTeam)
{
    var blocker = new PlayerId(TeamId.Orange, PlayerRole.Attacker);
    var state = new RallyTouchState(TeamId.Blue);
    state.OpenWindow(Window(TeamId.Orange, TechniqueAction.Block, 0f, 1f, blocker));

    Assert.That(state.Accept(blocker, TechniqueAction.Block, 0.5f).Disposition,
        Is.EqualTo(RallyContactDisposition.Accept));
    Assert.That(state.LastPhysicalTouch, Is.EqualTo(blocker));
    Assert.That(state.CountedTeamTouches, Is.Zero);

    state.BeginPossession(reboundTeam);

    Assert.That(state.PossessionTeam, Is.EqualTo(reboundTeam));
    Assert.That(state.CountedTeamTouches, Is.Zero);
    Assert.That(state.LastCountedActor.HasValue, Is.False);
}
~~~

Add wrong-team, wrong-action, and expired-window cases that return Ignore and retain state.

- [ ] **Step 2: Run red**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.RallyTouchStateTests" \
  -testResults "$PWD/TestResults/RallyTouchState-red.xml" \
  -logFile "$PWD/TestResults/RallyTouchState-red.log"
~~~

Expected: compilation fails because the state and evaluation types do not exist.

- [ ] **Step 3: Implement immutable windows and pure evaluation**

Implement the following Domain types:

~~~csharp
public enum RallyContactDisposition { Ignore, Accept, Fault }

public enum RallyContactRejectionReason
{
    None,
    WindowClosed,
    WrongTeam,
    WrongAction,
    WrongActor,
    ConsecutiveCountedTouch,
    FourthCountedTouch
}

public readonly struct RallyContactEvaluation
{
    public RallyContactEvaluation(
        RallyContactDisposition disposition,
        RallyContactRejectionReason reason)
    {
        Disposition = disposition;
        Reason = reason;
    }

    public RallyContactDisposition Disposition { get; }
    public RallyContactRejectionReason Reason { get; }
}
~~~

RallyContactWindow accepts one team, one TechniqueAction, inclusive finite start/end times, and a defensive copy of the eligible PlayerId collection. It validates that every actor belongs to its Team.

RallyTouchState owns PossessionTeam, CountedTeamTouches, LastCountedActor, LastPhysicalTouch, and ContactWindow. Its required behavior is:

~~~csharp
public RallyContactEvaluation Evaluate(
    PlayerId actor,
    TechniqueAction action,
    float simulationTime);

public RallyContactEvaluation Accept(
    PlayerId actor,
    TechniqueAction action,
    float simulationTime);

public void BeginPossession(TeamId team);
public void OpenWindow(RallyContactWindow window);
~~~

Evaluate never mutates. Accept calls Evaluate and mutates only for Accept. Normal Receive, Set, and Attack increment the count and set LastCountedActor. Block only sets LastPhysicalTouch. Wrong window candidates Ignore; a same counted actor or fourth counted touch Faults before physics. BeginPossession resets count and LastCountedActor but retains LastPhysicalTouch for referee attribution.

- [ ] **Step 4: Run green and commit**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.RallyTouchStateTests|Volleyball.EditModeTests.LlmDecisionProtocolTests" \
  -testResults "$PWD/TestResults/RallyTouchState-green.xml" \
  -logFile "$PWD/TestResults/RallyTouchState-green.log"
git add Assets/Volleyball/Match/Runtime/Domain/Prototype/RallyTouchState.cs \
  Assets/Volleyball/Match/Tests/EditMode/RallyTouchStateTests.cs
git commit -m "feat: add rally touch legality state"
~~~

Expected: all state and existing LLM protocol tests pass.

### Task 3: Add Net Interception and the Pure Multi-Role Planner

**Files:**
- Create: Assets/Volleyball/Match/Runtime/Domain/Simulation/NetPlaneInterception.cs
- Create: Assets/Volleyball/Match/Tests/EditMode/NetPlaneInterceptionTests.cs
- Modify: Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs
- Modify: Assets/Volleyball/Match/Tests/EditMode/EnvironmentCollisionTests.cs
- Create: Assets/Volleyball/Match/Runtime/AI/RallyTacticalWeights.cs
- Create: Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs
- Create: Assets/Volleyball/Match/Tests/EditMode/RallyTacticalWeightsTests.cs
- Create: Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs

- [ ] **Step 1: Write failing interception, tactical-boundary, and planner tests**

Use these representative tests:

~~~csharp
[Test]
public void TryPredict_FindsFirstWorldZZeroCrossing()
{
    var ball = new BallState(
        new SimVector3(1f, 3f, -2f),
        new SimVector3(2f, 1f, 8f),
        SimulatedBall.DefaultRadius);

    Assert.That(NetPlaneInterception.TryPredict(
        ball,
        new BallSimulationParameters(-9.8f, 0.9995f),
        SimulatedBall.DefaultFixedStep,
        1f,
        out var intercept), Is.True);
    Assert.That(intercept.Point.Z, Is.EqualTo(0f).Within(0.0001f));
    Assert.That(intercept.TimeSeconds, Is.GreaterThan(0f));
}

[Test]
public void ResolveOrDefault_InvalidExternalWeightsUsesDeterministicDefault()
{
    var weights = RallyTacticalWeights.ResolveOrDefault(
        new RallyTacticalWeightProposal(float.NaN, 99f, -1f, 1f));

    Assert.That(weights, Is.EqualTo(RallyTacticalWeights.Default));
}

[Test]
public void Plan_PrefersNominalSetterWhenCandidatesAreEquallyReachable()
{
    var result = Planner().Plan(Input(
        TeamId.Blue,
        RallyDecisionStage.Organize,
        countedTouches: 1,
        lastCountedActor: new PlayerId(TeamId.Blue, PlayerRole.Defender),
        PlayersAtSameDistance()));

    Assert.That(result.HasDecision, Is.True);
    Assert.That(result.Action, Is.EqualTo(TechniqueAction.Set));
    Assert.That(result.Actor.Role, Is.EqualTo(PlayerRole.Setter));
}

[Test]
public void Plan_UsesDefenderToAttackWhenAttackerIsInfeasible()
{
    var result = Planner().Plan(Input(
        TeamId.Blue,
        RallyDecisionStage.Attack,
        countedTouches: 2,
        lastCountedActor: new PlayerId(TeamId.Blue, PlayerRole.Setter),
        PlayersWithUnreachableAttacker()));

    Assert.That(result.Actor.Role, Is.EqualTo(PlayerRole.Defender));
    Assert.That(result.AttackApproach.HasValue, Is.True);
}

[Test]
public void Plan_ApproachQualityCapsAndAnglePenaltyGrows()
{
    var shortApproach = Planner().Plan(AttackInput(0.4f, SpikeRoute.Line));
    var longApproach = Planner().Plan(AttackInput(4f, SpikeRoute.Line));
    var crossApproach = Planner().Plan(AttackInput(4f, SpikeRoute.CrossCourt));

    Assert.That(longApproach.AttackApproach.Value.JumpQuality,
        Is.GreaterThan(shortApproach.AttackApproach.Value.JumpQuality));
    Assert.That(longApproach.AttackApproach.Value.JumpQuality,
        Is.LessThanOrEqualTo(1f));
    Assert.That(crossApproach.AttackApproach.Value.AnglePenalty,
        Is.GreaterThanOrEqualTo(longApproach.AttackApproach.Value.AnglePenalty));
}
~~~

Also test planner determinism, last-counted-actor exclusion, non-setter organization when setter is infeasible, all candidates unreachable, and Orange output round-tripping to correct world Z.

- [ ] **Step 2: Run red**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.NetPlaneInterceptionTests|Volleyball.EditModeTests.RallyTacticalWeightsTests|Volleyball.EditModeTests.TeamRallyDecisionPlannerTests" \
  -testResults "$PWD/TestResults/MultiRolePlanner-red.xml" \
  -logFile "$PWD/TestResults/MultiRolePlanner-red.log"
~~~

Expected: compilation fails because the new Domain and AI types do not exist.

- [ ] **Step 3: Implement interception and backward-compatible net-crossing fraction**

Create:

~~~csharp
public readonly struct NetPlaneIntercept
{
    public NetPlaneIntercept(float timeSeconds, SimVector3 point)
    {
        TimeSeconds = timeSeconds;
        Point = point;
    }

    public float TimeSeconds { get; }
    public SimVector3 Point { get; }
}
~~~

NetPlaneInterception.TryPredict clones a BallState, steps it with BallIntegrator, and interpolates the first world Z sign change. It validates finite positive step and maximum time.

Add this overload to SimulatedBall and retain the existing three-output-argument overload as a forwarding call:

~~~csharp
public static bool TryNetPlaneCrossing(
    SimVector3 previous,
    SimVector3 current,
    out SimVector3 crossing,
    out float timeFraction)
{
    crossing = SimVector3.Zero;
    timeFraction = 0f;
    if ((previous.Z < 0f && current.Z < 0f) ||
        (previous.Z > 0f && current.Z > 0f) ||
        previous.Z == current.Z)
    {
        return false;
    }

    timeFraction = -previous.Z / (current.Z - previous.Z);
    if (timeFraction < 0f || timeFraction > 1f)
    {
        return false;
    }

    crossing = SimVector3.Lerp(previous, current, timeFraction);
    return true;
}
~~~

- [ ] **Step 4: Implement tactical weights and multi-role ranking**

RallyTacticalWeightProposal contains only four finite, bounded scalar preferences: role preference, reachability, approach distance, and direction tolerance. RallyTacticalWeights.ResolveOrDefault rejects invalid proposals and returns Default. It does not call MenShen and cannot contain a transform or velocity.

TeamRallyDecisionPlanner is Unity-free:

~~~csharp
public enum RallyDecisionStage { Receive, Organize, Attack, Block }

public readonly struct RallyPlayerSnapshot
{
    public RallyPlayerSnapshot(
        PlayerId id,
        SimVector3 worldPosition,
        PlayerAbilityProfile ability)
    {
        Id = id;
        WorldPosition = worldPosition;
        Ability = ability;
    }

    public PlayerId Id { get; }
    public SimVector3 WorldPosition { get; }
    public PlayerAbilityProfile Ability { get; }
}

public sealed class TeamRallyDecisionPlanner
{
    public TeamRallyDecisionPlanner(int seed)
    {
        _seed = seed;
    }

    public TeamRallyDecision Plan(TeamRallyDecisionInput input)
    {
        return PlanCandidateScores(input);
    }
}
~~~

TeamRallyDecisionInput contains TeamId, TeamRallyTactic, exactly three RallyPlayerSnapshot values, predicted ball center, available seconds, base movement speed, counted touches, optional last counted actor, tactic revision, decision index, stage, and RallyTacticalWeights. `TeamRallyDecision` exposes `HasDecision`, `Actor`, `Action`, `ContactTarget`, `MovementTarget`, `BallTarget`, `Score`, `Candidates`, and nullable `AttackApproach`; `RallyDecisionScore` exposes reachability, nominal-role, approach, angle, and total terms. The output never exposes a mutable transform or a ball velocity.

For each candidate, compute:

~~~csharp
var effectiveSeconds = Math.Max(
    0f,
    input.AvailableSeconds - (0.22f * (1f - player.Ability.Reaction)));
var maximumSpeed = input.BaseMovementSpeed *
    (0.65f + (player.Ability.Mobility * 0.5f));
var groundDistance = GroundDistance(player.WorldPosition, targetWorldPosition);
var reachabilityMargin = (maximumSpeed * effectiveSeconds) - groundDistance;
~~~

Candidates with a negative margin are infeasible. Exclude LastCountedActor for normal organization and attack. Give Setter only a bounded organize bonus and Attacker only a bounded attack bonus; tie-break by PlayerRole enum order for deterministic results.

For attack, use TeamCourtFrame to calculate an approach start and takeoff in local Z, then convert both to world coordinates. Calculate a capped smooth distance quality and a direction-angle penalty. Return AttackApproachPlan with world-space points, distance, jump quality, and penalty. Generate world-space route landing targets with X based on SpikeRoute instead of hard-coding X = 0.

- [ ] **Step 5: Run green and commit**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.NetPlaneInterceptionTests|Volleyball.EditModeTests.EnvironmentCollisionTests|Volleyball.EditModeTests.RallyTacticalWeightsTests|Volleyball.EditModeTests.TeamRallyDecisionPlannerTests" \
  -testResults "$PWD/TestResults/MultiRolePlanner-green.xml" \
  -logFile "$PWD/TestResults/MultiRolePlanner-green.log"
git add Assets/Volleyball/Match/Runtime/Domain/Simulation/NetPlaneInterception.cs \
  Assets/Volleyball/Match/Runtime/AI/RallyTacticalWeights.cs \
  Assets/Volleyball/Match/Runtime/AI/TeamRallyDecisionPlanner.cs \
  Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs \
  Assets/Volleyball/Match/Tests/EditMode/NetPlaneInterceptionTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/EnvironmentCollisionTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/RallyTacticalWeightsTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/TeamRallyDecisionPlannerTests.cs
git commit -m "feat: plan multi-role rally decisions"
~~~

Expected: planner tests show both role preferences and feasible role overrides.

### Task 4: Resolve Contact Legality Before Physics

**Files:**
- Modify: Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs
- Modify: Assets/Volleyball/Match/Tests/EditMode/SimulatedBallTests.cs

- [ ] **Step 1: Write failing pre-response resolver tests**

Add sources with two swept player candidates. The earlier candidate is rejected and the later one accepted:

~~~csharp
[Test]
public void AdvanceSimulation_IgnoresRejectedEarlyCandidateBeforeApplyingResponse()
{
    var ball = CreateBallWithTwoSweptCandidates(out var gameObject);
    try
    {
        ball.ContactCandidateResolver = (candidate, hit, contactTime) =>
            candidate.Actor.Value.Role == PlayerRole.Defender
                ? BallContactResolution.Ignore()
                : BallContactResolution.Accept();
        ball.Launch(new Vector3(0f, -40f, 0f));

        ball.AdvanceSimulation(1d / 120d);

        Assert.That(ball.State.LastContactGroupId, Is.EqualTo(78));
        Assert.That(ball.State.Velocity.Y, Is.GreaterThan(0f));
    }
    finally
    {
        Object.DestroyImmediate(gameObject);
    }
}

[Test]
public void AdvanceSimulation_FaultsBeforeAnyVelocityResponse()
{
    var ball = CreateBallWithTwoSweptCandidates(out var gameObject);
    try
    {
        PlayerContactRejectedEvent rejected = default;
        ball.ContactCandidateResolver = (_, __, ___) =>
            BallContactResolution.Fault("fourth counted touch");
        ball.PlayerContactRejected += value => rejected = value;
        ball.Launch(new Vector3(0f, -40f, 0f));

        ball.AdvanceSimulation(1d / 120d);

        Assert.That(rejected.Reason, Is.EqualTo("fourth counted touch"));
        Assert.That(ball.State.LastContactGroupId, Is.Null);
        Assert.That(ball.State.Velocity.Y, Is.LessThan(0f));
    }
    finally
    {
        Object.DestroyImmediate(gameObject);
    }
}
~~~

Add a block-at-net candidate test asserting a valid block at or before the crossing fraction suppresses NetPlaneCrossed. Add a legal crossing-first subscriber that calls ConsumeRemainingStep and prove no later player or environment response fires.

- [ ] **Step 2: Run red**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.SimulatedBallTests" \
  -testResults "$PWD/TestResults/SimulatedBallResolver-red.xml" \
  -logFile "$PWD/TestResults/SimulatedBallResolver-red.log"
~~~

Expected: compilation fails because BallContactResolution and the resolver/event APIs do not exist.

- [ ] **Step 3: Implement Ignore / Accept / Fault and ordered events**

Add these Presentation values near BallContactCandidate:

~~~csharp
public enum BallContactDisposition { Ignore, Accept, Fault }

public readonly struct BallContactResolution
{
    private BallContactResolution(BallContactDisposition disposition, string reason)
    {
        Disposition = disposition;
        Reason = reason;
    }

    public BallContactDisposition Disposition { get; }
    public string Reason { get; }

    public static BallContactResolution Ignore()
    {
        return new BallContactResolution(BallContactDisposition.Ignore, string.Empty);
    }

    public static BallContactResolution Accept()
    {
        return new BallContactResolution(BallContactDisposition.Accept, string.Empty);
    }

    public static BallContactResolution Fault(string reason)
    {
        return new BallContactResolution(BallContactDisposition.Fault,
            string.IsNullOrWhiteSpace(reason) ? "illegal player contact" : reason);
    }
}
~~~

Add:

~~~csharp
public Func<BallContactCandidate, SweptBallHit, float, BallContactResolution>
    ContactCandidateResolver { get; set; }

public event Action<PlayerContactRejectedEvent> PlayerContactRejected;
~~~

Make NetPlaneCrossingEvent mutable through:

~~~csharp
public bool IsRemainingStepConsumed { get; private set; }

public void ConsumeRemainingStep()
{
    IsRemainingStepConsumed = true;
}
~~~

Refactor StepSimulation in this order:

1. Integrate the fixed step.
2. Gather raw swept player hits, compute `contactSimulationTime = SimulationTime + (deltaSeconds * hit.TimeFraction)`, resolve each before any response, and retain the earliest non-Ignored player hit.
3. Gather ground, net, and net-plane fractions.
4. At equal fractions, physical ground/net/player events win over crossing.
5. If crossing is first, raise NetPlaneCrossed. If consumed, return without later response.
6. If the earliest player resolution is Fault, raise PlayerContactRejected and return without ContactResponse.Apply.
7. If it is Accept, apply physical and technique response and raise PlayerContact.
8. Otherwise apply the earliest ground or net response.

A null resolver means Accept, preserving PhysicsContactTraining and existing unfiltered sources.

- [ ] **Step 4: Run green and commit**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.SimulatedBallTests|Volleyball.EditModeTests.EnvironmentCollisionTests|Volleyball.EditModeTests.ContactResponseTests" \
  -testResults "$PWD/TestResults/SimulatedBallResolver-green.xml" \
  -logFile "$PWD/TestResults/SimulatedBallResolver-green.log"
git add Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs \
  Assets/Volleyball/Match/Tests/EditMode/SimulatedBallTests.cs
git commit -m "feat: resolve rally contact eligibility before physics"
~~~

Expected: the fault path never changes velocity or registers a contact group, while valid block ordering suppresses a same-step crossing.

### Task 5: Add Physical Block Windows and Bounded Attack Approaches

**Files:**
- Modify: Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs
- Modify: Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs

- [ ] **Step 1: Write failing agent tests**

Retain SupportAction_AdvancesFromSimulationTimeWithoutAddingContactCandidates. It protects the visual-only support API. Add distinct physical-block tests:

~~~csharp
[Test]
public void ScheduledBlockContact_EmitsTwoActivePalmsOnlyInsideItsWindow()
{
    var player = CreatePlayer("PhysicalBlocker", TeamId.Orange, PlayerRole.Attacker);
    try
    {
        player.ScheduleBlockContact(
            10f,
            new Vector3(1f, 0f, 0.28f),
            9f,
            new SimVector3(0f, 2f, -8f),
            701);

        var before = Collect(player, 9.5f);
        var atContact = Collect(player, 10f);

        Assert.That(before, Is.Empty);
        Assert.That(atContact, Has.Count.EqualTo(2));
        Assert.That(atContact, Has.All.Matches<BallContactCandidate>(candidate =>
            candidate.Action == TechniqueAction.Block &&
            candidate.Actor == player.Id &&
            candidate.Surface.ContactGroupId == 701 &&
            candidate.Surface.Active));
        Assert.That(player.transform.position.y, Is.GreaterThan(0.2f));
    }
    finally
    {
        Object.DestroyImmediate(player.gameObject);
    }
}

[Test]
public void RetargetBlockContact_ClampsLargeLateCorrection()
{
    var player = CreatePlayer("RetargetedBlocker", TeamId.Blue, PlayerRole.Setter);
    try
    {
        player.ScheduleBlockContact(
            8f,
            Vector3.zero,
            7f,
            new SimVector3(0f, 2f, 8f),
            702);

        Assert.That(player.RetargetBlockContact(
            8.8f,
            new Vector3(10f, 0f, -10f),
            new SimVector3(0f, 2f, 8f)), Is.True);
        Assert.That(player.BlockRetargetDistance, Is.LessThanOrEqualTo(0.55f));
        Assert.That(player.BlockRetargetTimeShift, Is.LessThanOrEqualTo(0.12f));
    }
    finally
    {
        Object.DestroyImmediate(player.gameObject);
    }
}
~~~

Add an attack approach test that samples a planned attack across movement, contact, and recovery. Assert continuous non-teleporting movement, jump height scales with JumpQuality, and the root returns to ground.

- [ ] **Step 2: Run red**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PrototypePlayerContactSourceTests" \
  -testResults "$PWD/TestResults/PrototypePlayerBlock-red.xml" \
  -logFile "$PWD/TestResults/PrototypePlayerBlock-red.log"
~~~

Expected: compilation fails because physical block APIs and the approach-plan overload do not exist.

- [ ] **Step 3: Implement dedicated paths**

Keep ScheduleSupportAction unchanged and visual-only. Add:

~~~csharp
public int PhysicalBlockContactAssignments { get; private set; }
public float BlockRetargetDistance { get; private set; }
public float BlockRetargetTimeShift { get; private set; }

public void ScheduleBlockContact(
    float scheduledSimulationTime,
    Vector3 movementTarget,
    float movementStartSimulationTime,
    SimVector3 targetVelocity,
    int contactGroupId);

public bool RetargetBlockContact(
    float scheduledSimulationTime,
    Vector3 movementTarget,
    SimVector3 targetVelocity);

public void DisableBlockContactWindow();
~~~

The block path uses an ActionTimeline, existing support ground movement, and EvaluateSupportBlockJump. During SurfaceActive it captures both Block palms with Actor = Id, Ability.TechniqueFor(Block), targetVelocity, and ResponseFor(Block). Each scheduled block uses a unique contact group. Retarget clamps time shift to 0.12 seconds and ground target correction to 0.55 metres.

Add an optional AttackApproachPlan argument to ScheduleContact. For an attack plan, move to the world-space approach start under existing movement speed limits, then interpolate continuously to the reachable takeoff. Multiply the existing attack jump curve by AttackApproachPlan.JumpQuality. Never set transform.position to an unreachable takeoff; keep existing behavior when the optional plan is absent.

- [ ] **Step 4: Run green and commit**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PrototypePlayerContactSourceTests|Volleyball.EditModeTests.PlayerContactSurfacesTests" \
  -testResults "$PWD/TestResults/PrototypePlayerBlock-green.xml" \
  -logFile "$PWD/TestResults/PrototypePlayerBlock-green.log"
git add Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "feat: add physical block contact windows"
~~~

Expected: generic support remains non-contacting; the dedicated block window creates only legal block candidates.

### Task 6: Replace the Fixed Director Sequence

**Files:**
- Modify: Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs
- Modify: Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs

- [ ] **Step 1: Add failing PlayMode diagnostics assertions**

Add these assertions while retaining all existing result, camera, and finite-state checks:

~~~csharp
Assert.That(director.PhysicalBlockContacts, Is.GreaterThan(0));
Assert.That(director.PostBlockContinuations, Is.GreaterThan(0));
Assert.That(director.NonSetterSetContacts, Is.GreaterThan(0));
Assert.That(director.DefenderAttackContacts, Is.GreaterThan(0));
Assert.That(director.IllegalContactFaults, Is.GreaterThanOrEqualTo(0));
Assert.That(director.MaximumAppliedMovementCorrection, Is.LessThanOrEqualTo(0.70f));
~~~

- [ ] **Step 2: Run red**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests" \
  -testResults "$PWD/TestResults/ThreeVsThreeDynamic-red.xml" \
  -logFile "$PWD/TestResults/ThreeVsThreeDynamic-red.log"
~~~

Expected: compilation fails because the new diagnostics do not exist.

- [ ] **Step 3: Implement possession orchestration**

Remove _sequence, _contactCenters, _rootTargets, _expectedIndex, ExpectedAction, and pending tactic promotion. Add:

~~~csharp
private readonly TeamRallyDecisionPlanner _decisionPlanner =
    new TeamRallyDecisionPlanner(7351);
private RallyTouchState _touchState;
private TeamRallyDecision? _scheduledDecision;
private int _decisionIndex;

public int PhysicalBlockContacts { get; private set; }
public int PostBlockContinuations { get; private set; }
public int NonSetterSetContacts { get; private set; }
public int DefenderAttackContacts { get; private set; }
public int IllegalContactFaults { get; private set; }
public float MaximumAppliedMovementCorrection { get; private set; }
~~~

In Initialize, attach the pre-physics resolver and rejection handler:

~~~csharp
_ball.ContactCandidateResolver = ResolveCandidate;
_ball.PlayerContactRejected += HandleRejectedPlayerContact;
_ball.PlayerContact += HandlePlayerContact;
_ball.EnvironmentContact += HandleEnvironmentContact;
_ball.NetPlaneCrossed += HandleNetPlaneCrossing;
~~~

ResolveCandidate maps actor-less candidates to Accept. For an actor candidate, it receives the candidate's actual swept `contactSimulationTime`, calls `_touchState.Evaluate` without mutation, and maps Domain Ignore / Accept / Fault to BallContactResolution. `HandlePlayerContact` calls `_touchState.Accept` with the same event contact time. HandleRejectedPlayerContact increments IllegalContactFaults and resolves the rally for the rejected actor's opponent without waiting for timeout or landing.

Use these methods:

~~~csharp
private void BeginPossession(TeamId team, float availableSeconds);
private TeamRallyDecision PlanDecision(
    TeamId team,
    RallyDecisionStage stage,
    float availableSeconds);
private void ScheduleDecision(TeamRallyDecision decision, float flightSeconds);
private void SchedulePhysicalBlock(
    TeamId attackingTeam,
    TeamRallyDecision attackDecision,
    PlayerBallContactEvent attackContact);
private void HandleAcceptedBlock(PlayerBallContactEvent blockContact);
~~~

Build planner input from all three agents on the requesting team, actual world positions, ability profiles, current tactic, state count/last actor, tactic revision, and decision index. Use TrajectoryPredictor for available time. No valid plan means no open contact window and the ball resolves normally at ground/net.

For Receive, schedule the primary agent and open emergency windows only for reachable backup candidates. For Organize, exclude the actual receiver. Plan the attack before scheduling the set, so the set target is the attack plan's future takeoff contact center rather than the attacker’s current world Z. For Attack, pass AttackApproachPlan to ScheduleContact and solve outbound velocity toward the route-specific world target.

On any accepted contact, call _touchState.Accept with the actual candidate actor, then call MatchSet.RecordContact for that same actor and actual scheduled movement distance. Update NonSetterSetContacts and DefenderAttackContacts from actual action/role.

Before an attack's contact, schedule approximate block movement from the planned route. After its actual technique response, calculate NetPlaneInterception from the true outgoing velocity and bounded-retarget the blocker. On accepted Block, call _touchState.Accept, disable its block window, choose rebound possession from the sign of the true outgoing world Z, call BeginPossession(reboundTeam), increment PhysicalBlockContacts and PostBlockContinuations, and schedule a fresh zero-touch receive decision. The blocker's later first normal touch is legal because BeginPossession clears LastCountedActor.

On legal crossing, begin zero-touch possession for the receiving team and schedule Receive. On a crossing antenna fault, ConsumeRemainingStep before resolving the rally. Resolve ground and net outcomes from _touchState.LastPhysicalTouch, never a nominal expected player. Generate a new tactic revision only after ResolveRally; the attack and its block therefore share a frozen tactic revision.

- [ ] **Step 4: Run PlayMode green and inspect evidence**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests" \
  -testResults "$PWD/TestResults/ThreeVsThreeDynamic-green.xml" \
  -logFile "$PWD/TestResults/ThreeVsThreeDynamic-green.log"
rg -n 'test-run|total=|failed=|result=' \
  "$PWD/TestResults/ThreeVsThreeDynamic-green.xml"
rg -n '\[Physical3v3\].*(block|contact|decision|fault|RESULT)' \
  "$PWD/TestResults/ThreeVsThreeDynamic-green.log"
~~~

Expected: XML exists with failed="0"; logs include a real block contact, post-block continuation, non-setter set, defender attack, and RESULT READY.

- [ ] **Step 5: Commit dynamic orchestration**

~~~bash
git add Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs \
  Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs
git commit -m "feat: use dynamic multi-role rally decisions"
~~~

### Task 7: Document and Fully Verify

**Files:**
- Create: docs/changes/2026-07-19-002-unified-multi-role-rally-decision.md
- Modify: docs/changes/README.md
- Modify: docs/development.md

- [ ] **Step 1: Write the Match-only change record**

Create CHG-20260719-002 from docs/changes/TEMPLATE.md. Mark it Match / Docs and module-internal. State explicitly that Shared, Career, Bootstrap, scene paths, assembly references, and MatchResultV1 did not change. Record the coordinate contract, RallyTouchState, local deterministic planner, pre-physics resolver, physical blocks, diagnostics, rollback order, and XML-backed verification paths. Add it as the first row of docs/changes/README.md.

Update docs/development.md so Physical3v3Rally documents dynamic roles, real blocks not consuming a team touch, and Unity X/Y/Z naming.

- [ ] **Step 2: Run full EditMode regression**

~~~bash
git diff --check
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/EditMode-unified-rally-final.xml" \
  -logFile "$PWD/TestResults/EditMode-unified-rally-final.log"
rg -n 'test-run|total=|failed=|result=' \
  "$PWD/TestResults/EditMode-unified-rally-final.xml"
~~~

Expected: diff check has no output; result XML exists and reports zero failures.

- [ ] **Step 3: Run full PlayMode regression and inspect logs**

~~~bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/PlayMode-unified-rally-final.xml" \
  -logFile "$PWD/TestResults/PlayMode-unified-rally-final.log"
rg -n 'test-run|total=|failed=|result=' \
  "$PWD/TestResults/PlayMode-unified-rally-final.xml"
rg -n '\[Physical3v3\].*(block|contact|decision|fault|RESULT)' \
  "$PWD/TestResults/PlayMode-unified-rally-final.log"
~~~

Expected: XML exists with zero failures; logs demonstrate dynamic roles and physical block continuation during a complete set.

- [ ] **Step 4: Check Unity metadata and commit delivery records**

~~~bash
git status --short
git diff -- ProjectSettings/ProjectSettings.asset
git add docs/changes/2026-07-19-002-unified-multi-role-rally-decision.md \
  docs/changes/README.md docs/development.md Assets/Volleyball/Match
git commit -m "docs: record unified multi-role rally decision"
~~~

Expected: every new Asset has a Unity-generated .meta. If Unity changed only known unrelated ProjectSettings.asset fields (runInBackground, targetPixelDensity, or target build/version fields), remove only those exact noise changes with a non-destructive patch and preserve all user work.

## Plan Self-Review

- Task 1 enforces agreed Unity X/Y/Z naming and Z-only team mirroring.
- Task 2 makes three-touch, same-player, and zero-count block rules pure and testable before Presentation integration.
- Task 3 provides deterministic dynamic-role scoring, bounded tactical-weight fallback, approach/angle quality, and real world-space net interception.
- Task 4 makes legality operate before ball velocity changes and fixes net/block same-step ordering.
- Task 5 keeps visual support behavior intact while adding separate physical block and bounded approach paths.
- Task 6 replaces the fixed role sequence, attributes actual contacts to MatchSet, freezes tactics per exchange, and proves the live scene behavior.
- Task 7 records the Match-only boundary and requires XML/log evidence from full Unity regression.

This plan intentionally does not make live MenShen requests. It creates only the validated local tactical-weight seam for a later gateway integration, retaining deterministic fallback during every rally.
