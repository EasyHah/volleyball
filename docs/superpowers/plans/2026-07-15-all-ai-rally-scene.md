# All-AI 3v3 Rally Scene Implementation Plan

> **Stage status:** This readable-trajectory prototype has been implemented.
> Its `BallArc/BallFlight` and single-pose motion approach is now a comparison
> baseline only. Continue physics, contact synchronization and detailed action
> work in
> [`2026-07-16-physics-contact-rally-upgrade.md`](2026-07-16-physics-contact-rally-upgrade.md).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a Unity scene that continuously plays readable, seeded, all-AI 3v3 volleyball rallies with procedural stick figures, high tactical camera, deterministic score and automatic reset.

**Architecture:** Keep score, rally sequencing and trajectory math in Unity-free `Runtime/Domain`; put repeatable target and action selection in `Runtime/AI`; keep primitives, animation poses, camera, score mesh and scene orchestration in `Runtime/Presentation`. `AiRallyDirector` consumes an immutable rally plan and drives presentation only; `PrototypeMatch` remains the single in-memory score owner. This prototype is presentation-local and does not emit contract results or establish persistent authority.

**Tech Stack:** Unity `6000.0.43f1`, C# 9-compatible syntax, built-in 3D primitives and `TextMesh`, Unity Test Framework `1.1.33`, NUnit, EditMode and PlayMode tests.

---

## File Structure

| Path | Responsibility |
| --- | --- |
| `Assets/VolleyballMatch/Runtime/Domain/VolleyballMatch.Domain.asmdef` | Unity-free domain assembly. |
| `Assets/VolleyballMatch/Runtime/Domain/Prototype/RallyPrimitives.cs` | Teams, player IDs, roles, court coordinates and action enums. |
| `Assets/VolleyballMatch/Runtime/Domain/Prototype/RallyPlan.cs` | Immutable contact sequence and rally winner. |
| `Assets/VolleyballMatch/Runtime/Domain/Prototype/PrototypeMatch.cs` | Score, serving team and once-only point award. |
| `Assets/VolleyballMatch/Runtime/Domain/Prototype/BallArc.cs` | Pure quadratic ball-arc evaluation. |
| `Assets/VolleyballMatch/Runtime/AI/VolleyballMatch.AI.asmdef` | AI assembly referencing Domain only. |
| `Assets/VolleyballMatch/Runtime/AI/DeterministicRallyPlanner.cs` | Seeded, bounded choices that construct legal rally plans. |
| `Assets/VolleyballMatch/Runtime/Presentation/VolleyballMatch.Presentation.asmdef` | Unity presentation assembly referencing Domain and AI. |
| `Assets/VolleyballMatch/Runtime/Presentation/PrototypeSceneBootstrap.cs` | Creates the scene objects and connects runtime components. |
| `Assets/VolleyballMatch/Runtime/Presentation/CourtBuilder.cs` | Builds the bright court, net, markings, lighting and high camera. |
| `Assets/VolleyballMatch/Runtime/Presentation/StickFigureRig.cs` | Builds named joint hierarchy and blends named poses. |
| `Assets/VolleyballMatch/Runtime/Presentation/PrototypePlayerAgent.cs` | Moves a rig to court targets and schedules poses. |
| `Assets/VolleyballMatch/Runtime/Presentation/BallFlight.cs` | Uses `BallArc` to move the visible ball between contact points. |
| `Assets/VolleyballMatch/Runtime/Presentation/ScoreDisplay.cs` | Renders score from `PrototypeMatch` with `TextMesh`. |
| `Assets/VolleyballMatch/Runtime/Presentation/AiRallyDirector.cs` | Converts a rally plan into movement, pose and ball-flight presentation. |
| `Assets/VolleyballMatch/Scenes/AiRallyPrototype.unity` | Empty saved Unity scene with `PrototypeSceneBootstrap` root. |
| `Assets/VolleyballMatch/Tests/EditMode/VolleyballMatch.EditModeTests.asmdef` | EditMode tests referencing runtime assemblies. |
| `Assets/VolleyballMatch/Tests/EditMode/*Tests.cs` | Deterministic domain, planner, arc and rig tests. |
| `Assets/VolleyballMatch/Tests/PlayMode/VolleyballMatch.PlayModeTests.asmdef` | PlayMode scene-integration test assembly. |
| `Assets/VolleyballMatch/Tests/PlayMode/AiRallyPrototypePlayModeTests.cs` | Multi-rally scene test. |
| `docs/development.md` | Reproducible test commands and manual ten-rally check. |

Unity generates `.meta` files when the editor first imports new assets; include every generated `.meta` and the saved `.unity` file in the relevant commit.

### Task 1: Establish Assembly Boundaries and Test Runner

**Files:**
- Create: `Assets/VolleyballMatch/Runtime/Domain/VolleyballMatch.Domain.asmdef`
- Create: `Assets/VolleyballMatch/Runtime/AI/VolleyballMatch.AI.asmdef`
- Create: `Assets/VolleyballMatch/Runtime/Presentation/VolleyballMatch.Presentation.asmdef`
- Create: `Assets/VolleyballMatch/Tests/EditMode/VolleyballMatch.EditModeTests.asmdef`
- Create: `Assets/VolleyballMatch/Tests/PlayMode/VolleyballMatch.PlayModeTests.asmdef`

- [ ] **Step 1: Create the runtime assembly definitions.**

```json
// Assets/VolleyballMatch/Runtime/Domain/VolleyballMatch.Domain.asmdef
{
  "name": "VolleyballMatch.Domain",
  "rootNamespace": "VolleyballMatch.Domain"
}
```

```json
// Assets/VolleyballMatch/Runtime/AI/VolleyballMatch.AI.asmdef
{
  "name": "VolleyballMatch.AI",
  "rootNamespace": "VolleyballMatch.AI",
  "references": ["VolleyballMatch.Domain"]
}
```

```json
// Assets/VolleyballMatch/Runtime/Presentation/VolleyballMatch.Presentation.asmdef
{
  "name": "VolleyballMatch.Presentation",
  "rootNamespace": "VolleyballMatch.Presentation",
  "references": ["VolleyballMatch.Domain", "VolleyballMatch.AI"]
}
```

- [ ] **Step 2: Create test assembly definitions.**

```json
// Assets/VolleyballMatch/Tests/EditMode/VolleyballMatch.EditModeTests.asmdef
{
  "name": "VolleyballMatch.EditModeTests",
  "references": [
    "VolleyballMatch.Domain",
    "VolleyballMatch.AI",
    "VolleyballMatch.Presentation"
  ],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"]
}
```

```json
// Assets/VolleyballMatch/Tests/PlayMode/VolleyballMatch.PlayModeTests.asmdef
{
  "name": "VolleyballMatch.PlayModeTests",
  "references": [
    "VolleyballMatch.Domain",
    "VolleyballMatch.AI",
    "VolleyballMatch.Presentation"
  ],
  "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] **Step 3: Import the assemblies in Unity and run the empty EditMode test assembly once.**

Run:

```bash
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.0.43f1/Unity.app/Contents/MacOS/Unity}"
test -x "$UNITY"
mkdir -p TestResults
"$UNITY" -batchmode -quit -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/EditMode.xml" -logFile "$PWD/TestResults/EditMode.log"
```

Expected: exit `0` with no compilation errors. This establishes the assembly
boundary before adding tests with runtime-type references in later tasks.

- [ ] **Step 4: Commit the assembly setup.**

```bash
git add Assets/VolleyballMatch/Runtime/*/*.asmdef Assets/VolleyballMatch/Tests/*/*.asmdef
git commit -m "chore: add volleyball runtime assemblies"
```

### Task 2: Implement Deterministic Match State

**Files:**
- Create: `Assets/VolleyballMatch/Runtime/Domain/Prototype/RallyPrimitives.cs`
- Create: `Assets/VolleyballMatch/Runtime/Domain/Prototype/RallyPlan.cs`
- Create: `Assets/VolleyballMatch/Runtime/Domain/Prototype/PrototypeMatch.cs`
- Create: `Assets/VolleyballMatch/Tests/EditMode/PrototypeMatchTests.cs`

- [ ] **Step 1: Write failing tests for initial serve, once-only score award and winning-team next serve.**

```csharp
using NUnit.Framework;
using VolleyballMatch.Domain;

namespace VolleyballMatch.EditModeTests
{
public sealed class PrototypeMatchTests
{
    [Test]
    public void NewMatch_StartsAtZeroWithBlueServing()
    {
        var match = new PrototypeMatch(TeamId.Blue);

        Assert.That(match.BlueScore, Is.Zero);
        Assert.That(match.OrangeScore, Is.Zero);
        Assert.That(match.ServingTeam, Is.EqualTo(TeamId.Blue));
        Assert.That(match.RallyNumber, Is.Zero);
    }

    [Test]
    public void AwardPoint_OnlyResolvesTheActiveRallyOnce()
    {
        var match = new PrototypeMatch(TeamId.Blue);

        Assert.That(match.TryAwardPoint(TeamId.Orange), Is.True);
        Assert.That(match.TryAwardPoint(TeamId.Orange), Is.False);
        Assert.That(match.OrangeScore, Is.EqualTo(1));
        Assert.That(match.ServingTeam, Is.EqualTo(TeamId.Orange));
        Assert.That(match.RallyNumber, Is.EqualTo(1));
    }
}
}
```

- [ ] **Step 2: Run the specific test file and confirm the class is missing.**

Run the Task 1 Unity command, then inspect `TestResults/EditMode.log`.

Expected: compiler error for missing `PrototypeMatch` and `TeamId`.

- [ ] **Step 3: Add Unity-free primitives and immutable rally-plan types.**

```csharp
// RallyPrimitives.cs
namespace VolleyballMatch.Domain
{
public enum TeamId { Blue, Orange }
public enum PlayerRole { Setter, Attacker, Defender }
public enum RallyActionKind { Serve, Receive, Set, Approach, Spike, Block, Dig }

public readonly struct PlayerId : System.IEquatable<PlayerId>
{
    public PlayerId(TeamId team, PlayerRole role) { Team = team; Role = role; }
    public TeamId Team { get; }
    public PlayerRole Role { get; }
    public bool Equals(PlayerId other) => Team == other.Team && Role == other.Role;
    public override bool Equals(object other) => other is PlayerId && Equals((PlayerId)other);
    public override int GetHashCode() => ((int)Team * 397) ^ (int)Role;
}

public readonly struct CourtPoint : System.IEquatable<CourtPoint>
{
    public CourtPoint(float x, float z) { X = x; Z = z; }
    public float X { get; }
    public float Z { get; }
    public bool Equals(CourtPoint other) => X.Equals(other.X) && Z.Equals(other.Z);
    public override bool Equals(object other) => other is CourtPoint && Equals((CourtPoint)other);
    public override int GetHashCode() => X.GetHashCode() ^ Z.GetHashCode();
}
}
```

```csharp
// RallyPlan.cs
using System.Collections.Generic;

namespace VolleyballMatch.Domain
{
public sealed class ContactPlan : System.IEquatable<ContactPlan>
{
    public ContactPlan(PlayerId actor, RallyActionKind kind, CourtPoint actorTarget, CourtPoint ballTarget, float contactHeight, float ballTargetHeight, float durationSeconds, float arcHeight)
    {
        Actor = actor; Kind = kind; ActorTarget = actorTarget; BallTarget = ballTarget;
        ContactHeight = contactHeight; BallTargetHeight = ballTargetHeight;
        DurationSeconds = durationSeconds; ArcHeight = arcHeight;
    }
    public PlayerId Actor { get; }
    public RallyActionKind Kind { get; }
    public CourtPoint ActorTarget { get; }
    public CourtPoint BallTarget { get; }
    public float ContactHeight { get; }
    public float BallTargetHeight { get; }
    public float DurationSeconds { get; }
    public float ArcHeight { get; }
    public bool Equals(ContactPlan other) => other != null && Actor.Equals(other.Actor) && Kind == other.Kind && ActorTarget.Equals(other.ActorTarget) && BallTarget.Equals(other.BallTarget) && ContactHeight.Equals(other.ContactHeight) && BallTargetHeight.Equals(other.BallTargetHeight) && DurationSeconds.Equals(other.DurationSeconds) && ArcHeight.Equals(other.ArcHeight);
    public override bool Equals(object other) => Equals(other as ContactPlan);
    public override int GetHashCode() => Actor.GetHashCode() ^ (int)Kind ^ ActorTarget.GetHashCode() ^ BallTarget.GetHashCode();
}

public sealed class RallyPlan
{
    public RallyPlan(IReadOnlyList<ContactPlan> contacts, TeamId winningTeam)
    {
        Contacts = contacts;
        WinningTeam = winningTeam;
    }

    public IReadOnlyList<ContactPlan> Contacts { get; }
    public TeamId WinningTeam { get; }
}
}
```

- [ ] **Step 4: Implement the minimal score owner.**

```csharp
// PrototypeMatch.cs
namespace VolleyballMatch.Domain
{
public sealed class PrototypeMatch
{
    private bool activeRally = true;

    public PrototypeMatch(TeamId firstServer) => ServingTeam = firstServer;

    public int BlueScore { get; private set; }
    public int OrangeScore { get; private set; }
    public int RallyNumber { get; private set; }
    public TeamId ServingTeam { get; private set; }

    public bool TryAwardPoint(TeamId winner)
    {
        if (!activeRally) return false;
        if (winner == TeamId.Blue) BlueScore++; else OrangeScore++;
        ServingTeam = winner;
        RallyNumber++;
        activeRally = false;
        return true;
    }

    public void BeginNextRally() => activeRally = true;
}
}
```

- [ ] **Step 5: Run all EditMode tests and commit the domain state.**

Expected: `PrototypeMatchTests` passes.

```bash
git add Assets/VolleyballMatch/Runtime/Domain Assets/VolleyballMatch/Tests/EditMode/PrototypeMatchTests.cs
git commit -m "feat: add deterministic prototype match state"
```

### Task 3: Build the Seeded All-AI Rally Planner

**Files:**
- Create: `Assets/VolleyballMatch/Runtime/AI/DeterministicRallyPlanner.cs`
- Create: `Assets/VolleyballMatch/Tests/EditMode/DeterministicRallyPlannerTests.cs`

- [ ] **Step 1: Write failing tests for reproducibility, mandatory volleyball order and bounded contact count.**

```csharp
using System.Linq;
using NUnit.Framework;
using VolleyballMatch.AI;
using VolleyballMatch.Domain;

namespace VolleyballMatch.EditModeTests
{
public sealed class DeterministicRallyPlannerTests
{
    [Test]
    public void CreatePlan_SameSeedAndRallyNumber_ProducesSamePlan()
    {
        var planner = new DeterministicRallyPlanner(7429);

        var first = planner.CreatePlan(TeamId.Blue, 4);
        var second = planner.CreatePlan(TeamId.Blue, 4);

        Assert.That(second.WinningTeam, Is.EqualTo(first.WinningTeam));
        CollectionAssert.AreEqual(first.Contacts, second.Contacts);
    }

    [Test]
    public void CreatePlan_ContainsServeReceiveSetAttackAndDefense()
    {
        var plan = new DeterministicRallyPlanner(7429).CreatePlan(TeamId.Blue, 0);
        var kinds = plan.Contacts.Select(contact => contact.Kind).ToArray();

        Assert.That(kinds[0], Is.EqualTo(RallyActionKind.Serve));
        Assert.That(kinds, Does.Contain(RallyActionKind.Receive));
        Assert.That(kinds, Does.Contain(RallyActionKind.Set));
        Assert.That(kinds, Does.Contain(RallyActionKind.Spike));
        Assert.That(kinds, Does.Contain(RallyActionKind.Block).Or.Contain(RallyActionKind.Dig));
        Assert.That(kinds.Length, Is.InRange(5, 9));
    }
}
}
```

- [ ] **Step 2: Run the tests and confirm `DeterministicRallyPlanner` is missing.**

Expected: compiler error naming `DeterministicRallyPlanner`.

- [ ] **Step 3: Implement a fixed-shape plan with seeded lane, defense and winner choices.**

```csharp
using System;
using System.Collections.Generic;
using VolleyballMatch.Domain;

namespace VolleyballMatch.AI
{
public sealed class DeterministicRallyPlanner
{
    private readonly int seed;

    public DeterministicRallyPlanner(int seed) => this.seed = seed;

    public RallyPlan CreatePlan(TeamId servingTeam, int rallyNumber)
    {
        var random = new Random(unchecked(seed + rallyNumber * 7919));
        var receivingTeam = servingTeam == TeamId.Blue ? TeamId.Orange : TeamId.Blue;
        var lane = random.Next(0, 3) - 1;
        var defense = random.Next(0, 2) == 0 ? RallyActionKind.Block : RallyActionKind.Dig;
        var winner = random.Next(0, 2) == 0 ? receivingTeam : servingTeam;
        var defenseContactHeight = defense == RallyActionKind.Block ? 2.7f : 1.3f;
        var receivePoint = new CourtPoint(lane * 2.2f, receivingTeam == TeamId.Blue ? -5f : 5f);
        var receivingSetPoint = new CourtPoint(-1.4f, receivingTeam == TeamId.Blue ? -1.8f : 1.8f);
        var receivingAttackPoint = new CourtPoint(-2.4f, receivingTeam == TeamId.Blue ? -3.2f : 3.2f);
        var defensePoint = new CourtPoint(lane * -1.8f, servingTeam == TeamId.Blue ? 5.6f : -5.6f);
        var servingSetPoint = new CourtPoint(1.4f, servingTeam == TeamId.Blue ? -1.8f : 1.8f);
        var servingAttackPoint = new CourtPoint(2.4f, servingTeam == TeamId.Blue ? -3.2f : 3.2f);
        var receivingEnd = receivingTeam == TeamId.Blue ? -1f : 1f;
        var finalBallTarget = winner == servingTeam
            ? new CourtPoint(lane * 2.4f, receivingEnd * 6.3f)
            : new CourtPoint(lane * 2.4f, receivingEnd * 8.2f);

        var contacts = new List<ContactPlan>
        {
            new ContactPlan(new PlayerId(servingTeam, PlayerRole.Attacker), RallyActionKind.Serve, new CourtPoint(-2.4f, servingTeam == TeamId.Blue ? -5.8f : 5.8f), receivePoint, 1.8f, 1.1f, 1.1f, 3.2f),
            new ContactPlan(new PlayerId(receivingTeam, PlayerRole.Defender), RallyActionKind.Receive, receivePoint, receivingSetPoint, 1.3f, 1.9f, 0.7f, 1.4f),
            new ContactPlan(new PlayerId(receivingTeam, PlayerRole.Setter), RallyActionKind.Set, receivingSetPoint, receivingAttackPoint, 2.2f, 2.7f, 0.8f, 2.5f),
            new ContactPlan(new PlayerId(receivingTeam, PlayerRole.Attacker), RallyActionKind.Spike, receivingAttackPoint, defensePoint, 2.7f, defenseContactHeight, 0.8f, 2.0f),
            new ContactPlan(new PlayerId(servingTeam, PlayerRole.Attacker), defense, defensePoint, servingSetPoint, defenseContactHeight, 2.7f, 0.7f, 1.5f),
            new ContactPlan(new PlayerId(servingTeam, PlayerRole.Setter), RallyActionKind.Set, servingSetPoint, servingAttackPoint, 2.2f, 2.7f, 0.8f, 2.5f),
            new ContactPlan(new PlayerId(servingTeam, PlayerRole.Attacker), RallyActionKind.Spike, servingAttackPoint, finalBallTarget, 2.7f, 0.15f, 0.8f, 1.9f)
        };

        return new RallyPlan(contacts, winner);
    }
}
}
```

- [ ] **Step 4: Run the planner tests and then the full EditMode suite.**

Expected: both planner assertions pass and no plan contains more than seven
contacts.

- [ ] **Step 5: Commit the deterministic AI policy.**

```bash
git add Assets/VolleyballMatch/Runtime/AI Assets/VolleyballMatch/Tests/EditMode/DeterministicRallyPlannerTests.cs
git commit -m "feat: plan seeded all-ai rallies"
```

### Task 4: Implement Testable Ball Arc Math and Visible Ball Flights

**Files:**
- Create: `Assets/VolleyballMatch/Runtime/Domain/Prototype/BallArc.cs`
- Create: `Assets/VolleyballMatch/Runtime/Presentation/BallFlight.cs`
- Create: `Assets/VolleyballMatch/Tests/EditMode/BallArcTests.cs`

- [ ] **Step 1: Write failing tests for arc endpoints and raised midpoint.**

```csharp
using NUnit.Framework;
using VolleyballMatch.Domain;

namespace VolleyballMatch.EditModeTests
{
public sealed class BallArcTests
{
    [Test]
    public void Evaluate_ReturnsExactEndpointsAndRaisesMidpoint()
    {
        var arc = new BallArc(new CourtPoint(-2f, -3f), new CourtPoint(2f, 3f), 1.5f, 2.5f, 2f);

        Assert.That(arc.Evaluate(0f), Is.EqualTo(new BallArcPoint(-2f, 1.5f, -3f)));
        Assert.That(arc.Evaluate(1f), Is.EqualTo(new BallArcPoint(2f, 2.5f, 3f)));
        Assert.That(arc.Evaluate(0.5f).Y, Is.EqualTo(4f).Within(0.0001f));
    }
}
}
```

- [ ] **Step 2: Implement the pure quadratic evaluator.**

```csharp
namespace VolleyballMatch.Domain
{
public readonly struct BallArcPoint : System.IEquatable<BallArcPoint>
{
    public BallArcPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public bool Equals(BallArcPoint other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    public override bool Equals(object other) => other is BallArcPoint && Equals((BallArcPoint)other);
    public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
}

public readonly struct BallArc
{
    private readonly CourtPoint start;
    private readonly CourtPoint end;
    private readonly float startHeight;
    private readonly float endHeight;
    private readonly float apexOffset;

    public BallArc(CourtPoint start, CourtPoint end, float startHeight, float endHeight, float apexOffset)
    {
        this.start = start;
        this.end = end;
        this.startHeight = startHeight;
        this.endHeight = endHeight;
        this.apexOffset = apexOffset;
    }

    public BallArcPoint Evaluate(float normalizedTime)
    {
        var t = normalizedTime < 0f ? 0f : normalizedTime > 1f ? 1f : normalizedTime;
        var x = start.X + (end.X - start.X) * t;
        var z = start.Z + (end.Z - start.Z) * t;
        var y = startHeight + (endHeight - startHeight) * t + apexOffset * 4f * t * (1f - t);
        return new BallArcPoint(x, y, z);
    }
}
}
```

- [ ] **Step 3: Run `BallArcTests` and confirm the exact endpoint and midpoint checks pass.**

- [ ] **Step 4: Add the presentation adapter that animates a sphere from a `BallArc`.**

```csharp
using System;
using System.Collections;
using UnityEngine;
using VolleyballMatch.Domain;

namespace VolleyballMatch.Presentation
{
public sealed class BallFlight : MonoBehaviour
{
    public IEnumerator Play(BallArc arc, float durationSeconds, Action onCompleted)
    {
        for (var elapsed = 0f; elapsed < durationSeconds; elapsed += Time.deltaTime)
        {
            var point = arc.Evaluate(elapsed / durationSeconds);
            transform.position = new Vector3(point.X, point.Y, point.Z);
            yield return null;
        }

        var finalPoint = arc.Evaluate(1f);
        transform.position = new Vector3(finalPoint.X, finalPoint.Y, finalPoint.Z);
        onCompleted?.Invoke();
    }
}
}
```

- [ ] **Step 5: Commit arc math and flight adapter.**

```bash
git add Assets/VolleyballMatch/Runtime/Domain/Prototype/BallArc.cs Assets/VolleyballMatch/Runtime/Presentation/BallFlight.cs Assets/VolleyballMatch/Tests/EditMode/BallArcTests.cs
git commit -m "feat: animate controlled volleyball arcs"
```

### Task 5: Build and Animate the Jointed Stick Figure

**Files:**
- Create: `Assets/VolleyballMatch/Runtime/Presentation/StickFigurePose.cs`
- Create: `Assets/VolleyballMatch/Runtime/Presentation/StickFigureRig.cs`
- Create: `Assets/VolleyballMatch/Runtime/Presentation/PrototypePlayerAgent.cs`
- Create: `Assets/VolleyballMatch/Tests/EditMode/StickFigureRigTests.cs`

- [ ] **Step 1: Write failing rig tests for all named joints and a non-snapping spike pose.**

```csharp
using NUnit.Framework;
using UnityEngine;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.EditModeTests
{
public sealed class StickFigureRigTests
{
    [Test]
    public void Create_BuildsEveryNamedJoint()
    {
        var root = new GameObject("Rig");
        var rig = StickFigureRig.Create(root.transform, Color.blue, "7");

        Assert.That(rig.HasJoint("Head"), Is.True);
        Assert.That(rig.HasJoint("LeftElbow"), Is.True);
        Assert.That(rig.HasJoint("RightElbow"), Is.True);
        Assert.That(rig.HasJoint("LeftKnee"), Is.True);
        Assert.That(rig.HasJoint("RightKnee"), Is.True);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void SetPose_UsesFiniteJointRotations()
    {
        var root = new GameObject("Rig");
        var rig = StickFigureRig.Create(root.transform, Color.blue, "7");

        rig.SetPose(StickFigurePose.Spike, 1f);

        Assert.That(float.IsNaN(rig.GetJoint("RightShoulder").localEulerAngles.x), Is.False);
        Assert.That(float.IsInfinity(rig.GetJoint("RightShoulder").localEulerAngles.x), Is.False);
        Object.DestroyImmediate(root);
    }
}
}
```

- [ ] **Step 2: Define one enum for every confirmed action-visible pose.**

```csharp
namespace VolleyballMatch.Presentation
{
public enum StickFigurePose
{
    Ready,
    Run,
    Serve,
    Receive,
    Set,
    Approach,
    Spike,
    Block,
    Landing,
    Celebrate
}
}
```

- [ ] **Step 3: Implement the primitive hierarchy and pose contract.**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace VolleyballMatch.Presentation
{
public sealed class StickFigureRig : MonoBehaviour
{
    private readonly Dictionary<string, Transform> joints = new Dictionary<string, Transform>();
    private readonly Dictionary<StickFigurePose, Dictionary<string, Vector3>> poses = new Dictionary<StickFigurePose, Dictionary<string, Vector3>>();

    public static StickFigureRig Create(Transform parent, Color teamColor, string jerseyNumber)
    {
        var root = new GameObject("StickFigureRig");
        root.transform.SetParent(parent, false);
        var rig = root.AddComponent<StickFigureRig>();
        rig.CreateJoint("Hips", null, Vector3.zero, Vector3.one * 0.22f, teamColor);
        rig.CreateJoint("Torso", "Hips", Vector3.up * 0.45f, new Vector3(0.32f, 0.6f, 0.22f), teamColor);
        rig.CreateJoint("Head", "Torso", Vector3.up * 0.48f, Vector3.one * 0.28f, new Color(1f, 0.78f, 0.61f));
        rig.CreateLimb("Left", "Torso", new Vector3(-0.25f, 0.32f, 0f), new Vector3(-0.36f, -0.34f, 0f), new Vector3(-0.34f, -0.34f, 0f));
        rig.CreateLimb("Right", "Torso", new Vector3(0.25f, 0.32f, 0f), new Vector3(0.36f, -0.34f, 0f), new Vector3(0.34f, -0.34f, 0f));
        rig.CreateLimb("LeftLeg", "Hips", new Vector3(-0.12f, -0.12f, 0f), new Vector3(-0.14f, -0.48f, 0f), new Vector3(-0.1f, -0.48f, 0.05f));
        rig.CreateLimb("RightLeg", "Hips", new Vector3(0.12f, -0.12f, 0f), new Vector3(0.14f, -0.48f, 0f), new Vector3(0.1f, -0.48f, 0.05f));
        rig.CreatePoseLibrary();
        rig.SetPose(StickFigurePose.Ready, 1f);
        return rig;
    }

    public bool HasJoint(string name) => joints.ContainsKey(name);
    public Transform GetJoint(string name) => joints[name];
    public void SetPose(StickFigurePose pose, float normalizedBlend)
    {
        foreach (var target in poses[pose])
            joints[target.Key].localRotation = Quaternion.Slerp(joints[target.Key].localRotation, Quaternion.Euler(target.Value), Mathf.Clamp01(normalizedBlend));
    }
}
}
```

Implement `CreateJoint`, `CreateLimb` and `CreatePoseLibrary` in the same file.
`CreateLimb` must name the shoulder/hip, elbow/knee and hand/foot transforms;
the pose library must give all joints a local Euler target for every enum value.
Use `Quaternion.Slerp(current, target, Mathf.Clamp01(normalizedBlend))` per
joint so no pose transition snaps.

- [ ] **Step 4: Implement player movement and pose scheduling.**

```csharp
using System.Collections;
using UnityEngine;
using VolleyballMatch.Domain;

namespace VolleyballMatch.Presentation
{
public sealed class PrototypePlayerAgent : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    public PlayerId Id { get; private set; }
    public StickFigureRig Rig { get; private set; }

    public void Initialize(PlayerId id, Color color, string number)
    {
        Id = id;
        Rig = StickFigureRig.Create(transform, color, number);
    }

    public IEnumerator MoveTo(Vector3 destination)
    {
        while ((transform.position - destination).sqrMagnitude > 0.01f)
        {
            Rig.SetPose(StickFigurePose.Run, Time.deltaTime * 8f);
            transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
            yield return null;
        }
        Rig.SetPose(StickFigurePose.Ready, 0.25f);
    }
}
}
```

- [ ] **Step 5: Run rig and existing EditMode tests, then commit the rig, movement agent and tests.**

```bash
git add Assets/VolleyballMatch/Runtime/Presentation/StickFigurePose.cs Assets/VolleyballMatch/Runtime/Presentation/StickFigureRig.cs Assets/VolleyballMatch/Runtime/Presentation/PrototypePlayerAgent.cs Assets/VolleyballMatch/Tests/EditMode/StickFigureRigTests.cs
git commit -m "feat: add animated jointed stick figures"
```

### Task 6: Create the Bright Prototype Gym and High Tactical Camera

**Files:**
- Create: `Assets/VolleyballMatch/Runtime/Presentation/PrototypeSceneBootstrap.cs`
- Create: `Assets/VolleyballMatch/Runtime/Presentation/CourtBuilder.cs`
- Create: `Assets/VolleyballMatch/Runtime/Presentation/ScoreDisplay.cs`
- Create: `Assets/VolleyballMatch/Scenes/AiRallyPrototype.unity`
- Create: `Assets/VolleyballMatch/Tests/EditMode/CourtBuilderTests.cs`

- [ ] **Step 1: Write a failing builder test for court, net and high camera.**

```csharp
using NUnit.Framework;
using UnityEngine;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.EditModeTests
{
public sealed class CourtBuilderTests
{
    [Test]
    public void Build_CreatesCourtNetAndOrthographicCamera()
    {
        var root = new GameObject("Court");
        var court = CourtBuilder.Build(root.transform);

        Assert.That(court.Find("Net"), Is.Not.Null);
        Assert.That(court.GetComponentInChildren<Camera>().orthographic, Is.True);
        Object.DestroyImmediate(root);
    }
}
}
```

- [ ] **Step 2: Implement the court builder with constants centralized at its top.**

```csharp
using UnityEngine;

namespace VolleyballMatch.Presentation
{
public static class CourtBuilder
{
    public const float HalfWidth = 4.5f;
    public const float HalfLength = 7.5f;

    public static Transform Build(Transform parent)
    {
        var root = new GameObject("Court").transform;
        root.SetParent(parent, false);
        CreateBox(root, "Floor", Vector3.zero, new Vector3(HalfWidth * 2f + 2f, 0.2f, HalfLength * 2f + 2f), new Color(0.36f, 0.76f, 0.94f));
        CreateBox(root, "PlayingSurface", Vector3.up * 0.11f, new Vector3(HalfWidth * 2f, 0.04f, HalfLength * 2f), new Color(0.93f, 0.71f, 0.4f));
        CreateNet(root);
        CreateLines(root);
        CreateLight(root);
        CreateCamera(root);
        return root;
    }

    private static void CreateCamera(Transform parent)
    {
        var cameraObject = new GameObject("TacticalCamera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 16f, -13f), Quaternion.Euler(52f, 0f, 0f));
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 10.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.65f, 0.88f, 1f);
        camera.tag = "MainCamera";
    }
}
}
```

Implement `CreateBox`, `CreateNet`, `CreateLines` and `CreateLight` in the same
file. `CreateNet` creates a named `Net` at world `z = 0`, two posts and a thin
semi-transparent mesh/grid. `CreateLines` creates sidelines, end lines and
center line as raised white boxes.

- [ ] **Step 3: Implement a world-space score display.**

```csharp
using UnityEngine;
using VolleyballMatch.Domain;

namespace VolleyballMatch.Presentation
{
public sealed class ScoreDisplay : MonoBehaviour
{
    private TextMesh label;

    public static ScoreDisplay Create(Transform parent)
    {
        var scoreObject = new GameObject("ScoreDisplay");
        scoreObject.transform.SetParent(parent, false);
        scoreObject.transform.SetPositionAndRotation(new Vector3(0f, 8f, 2f), Quaternion.Euler(60f, 0f, 0f));
        var display = scoreObject.AddComponent<ScoreDisplay>();
        display.label = scoreObject.AddComponent<TextMesh>();
        display.label.anchor = TextAnchor.MiddleCenter;
        display.label.alignment = TextAlignment.Center;
        display.label.characterSize = 0.25f;
        display.label.fontSize = 64;
        display.label.color = Color.white;
        return display;
    }

    public void Render(PrototypeMatch match) => label.text = $"BLUE {match.BlueScore}  :  {match.OrangeScore} ORANGE";
}
}
```

- [ ] **Step 4: Implement scene bootstrap creation of court, ball, six agents and score display.**

```csharp
using UnityEngine;

namespace VolleyballMatch.Presentation
{
public sealed class PrototypeSceneBootstrap : MonoBehaviour
{
    [SerializeField] private int seed = 7429;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        CourtBuilder.Build(transform);
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Volleyball";
        ball.transform.localScale = Vector3.one * 0.32f;
        ball.transform.position = new Vector3(0f, 2.8f, -4.5f);
        ball.AddComponent<BallFlight>();
        ScoreDisplay.Create(transform);
        CreateSixAgents();
    }
}
}
```

The six exact starting positions are Blue: `(-2.5, 0, -4.5)`, `(0, 0, -2.5)`,
`(2.5, 0, -4.5)` and Orange: `(-2.5, 0, 4.5)`, `(0, 0, 2.5)`, `(2.5, 0, 4.5)`.
Assign stable roles Setter, Attacker, Defender in this order for each team.
Add `CreateSixAgents` in this task; it creates and initializes all six
`PrototypePlayerAgent` objects and returns them as a
`List<PrototypePlayerAgent>` for Task 7 to connect to the director.

- [ ] **Step 5: In Unity Editor, create and save the scene; do not hand-edit YAML.**

Open Unity `6000.0.43f1`, choose **File > New Scene > Basic (Built-in)**, delete
the default camera and light, create one empty root named `PrototypeBootstrap`,
attach `PrototypeSceneBootstrap`, then save it as
`Assets/VolleyballMatch/Scenes/AiRallyPrototype.unity`. Add it to Build Settings
so the PlayMode test can call `SceneManager.LoadSceneAsync("AiRallyPrototype")`.

- [ ] **Step 6: Run `CourtBuilderTests`, enter Play Mode and verify the static scene shows all six players, court, net and score framing. Commit the scene.**

```bash
git add Assets/VolleyballMatch/Runtime/Presentation/PrototypeSceneBootstrap.cs Assets/VolleyballMatch/Runtime/Presentation/CourtBuilder.cs Assets/VolleyballMatch/Runtime/Presentation/ScoreDisplay.cs Assets/VolleyballMatch/Scenes/AiRallyPrototype.unity Assets/VolleyballMatch/Tests/EditMode/CourtBuilderTests.cs ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: add tactical 3v3 prototype court"
```

### Task 7: Drive Continuous All-AI Rallies in the Scene

**Files:**
- Create: `Assets/VolleyballMatch/Runtime/Presentation/AiRallyDirector.cs`
- Modify: `Assets/VolleyballMatch/Runtime/Presentation/PrototypeSceneBootstrap.cs`

- [ ] **Step 1: Write the failing director state test.**

```csharp
using NUnit.Framework;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.EditModeTests
{
public sealed class AiRallyDirectorStateTests
{
    [Test]
    public void NewDirector_HasNoCompletedRallies()
    {
        var director = new UnityEngine.GameObject("Director").AddComponent<AiRallyDirector>();

        Assert.That(director.CompletedRallies, Is.Zero);
        UnityEngine.Object.DestroyImmediate(director.gameObject);
    }
}
}
```

- [ ] **Step 2: Run the test and confirm `AiRallyDirector` is missing, then implement the director with one coroutine per complete rally and immutable plan consumption.**

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VolleyballMatch.AI;
using VolleyballMatch.Domain;

namespace VolleyballMatch.Presentation
{
public sealed class AiRallyDirector : MonoBehaviour
{
    private readonly Dictionary<PlayerId, PrototypePlayerAgent> players = new Dictionary<PlayerId, PrototypePlayerAgent>();
    private PrototypeMatch match;
    private DeterministicRallyPlanner planner;
    private BallFlight ball;
    private ScoreDisplay scoreDisplay;

    public int CompletedRallies { get; private set; }
    public int TotalScore => match.BlueScore + match.OrangeScore;
    public bool IsRallyActive { get; private set; }

    public void Initialize(int seed, IEnumerable<PrototypePlayerAgent> agents, BallFlight visibleBall, ScoreDisplay display)
    {
        foreach (var agent in agents) players.Add(agent.Id, agent);
        ball = visibleBall;
        scoreDisplay = display;
        match = new PrototypeMatch(TeamId.Blue);
        planner = new DeterministicRallyPlanner(seed);
        scoreDisplay.Render(match);
        StartCoroutine(RunMatch());
    }

    private IEnumerator RunMatch()
    {
        while (true)
        {
            match.BeginNextRally();
            IsRallyActive = true;
            var plan = planner.CreatePlan(match.ServingTeam, match.RallyNumber);
            foreach (var contact in plan.Contacts) yield return PlayContact(contact);
            if (match.TryAwardPoint(plan.WinningTeam))
            {
                CompletedRallies++;
                scoreDisplay.Render(match);
            }
            IsRallyActive = false;
            yield return new WaitForSeconds(0.75f);
        }
    }
}
}
```

- [ ] **Step 3: Implement `PlayContact` without direct limb manipulation.**

```csharp
private IEnumerator PlayContact(ContactPlan contact)
{
    var actor = players[contact.Actor];
    var destination = ToWorld(contact.ActorTarget, contact.Actor.Team);
    yield return actor.MoveTo(destination);
    actor.Rig.SetPose(ToPose(contact.Kind), 0.35f);
    yield return new WaitForSeconds(0.16f);

    var arc = new BallArc(
        new CourtPoint(actor.transform.position.x, actor.transform.position.z),
        contact.BallTarget,
        contact.ContactHeight,
        contact.BallTargetHeight,
        contact.ArcHeight);
    yield return ball.Play(arc, contact.DurationSeconds, null);
    actor.Rig.SetPose(StickFigurePose.Ready, 0.35f);
}

private static StickFigurePose ToPose(RallyActionKind kind) => kind switch
{
    RallyActionKind.Serve => StickFigurePose.Serve,
    RallyActionKind.Receive => StickFigurePose.Receive,
    RallyActionKind.Dig => StickFigurePose.Receive,
    RallyActionKind.Set => StickFigurePose.Set,
    RallyActionKind.Approach => StickFigurePose.Approach,
    RallyActionKind.Spike => StickFigurePose.Spike,
    RallyActionKind.Block => StickFigurePose.Block,
    _ => StickFigurePose.Ready
};

private static Vector3 ToWorld(CourtPoint point, TeamId team) => new Vector3(point.X, 0f, point.Z);
```

For `Block`, move the actor to `(contact.ActorTarget.X, 0, sign of actor team * 0.6)` before applying the block pose. For a point-producing final spike, add a 0.25-second landing pose and make the winner's three agents play `Celebrate` before the reset delay.

- [ ] **Step 4: Replace the Task 6 bootstrap comment with exact initialization.**

```csharp
var agents = CreateSixAgents();
var director = gameObject.AddComponent<AiRallyDirector>();
director.Initialize(seed, agents, ball.GetComponent<BallFlight>(), GetComponentInChildren<ScoreDisplay>());
```

Reuse the `CreateSixAgents` method added in Task 6. It creates the six roles at
the specified positions, calls `Initialize(id, teamColor, number)`, and returns
a `List<PrototypePlayerAgent>`.

- [ ] **Step 5: Run EditMode tests, then start the saved scene and observe three uninterrupted rallies.**

Expected: every rally has the seven planned contacts, exactly one score update,
a reset delay and a next serve by the winning side.

- [ ] **Step 6: Commit full scene orchestration and director state test.**

```bash
git add Assets/VolleyballMatch/Runtime/Presentation/AiRallyDirector.cs Assets/VolleyballMatch/Runtime/Presentation/PrototypeSceneBootstrap.cs Assets/VolleyballMatch/Tests/EditMode/AiRallyDirectorStateTests.cs
git commit -m "feat: run continuous all-ai volleyball rallies"
```

### Task 8: Add PlayMode Coverage and Record Reproducible Verification

**Files:**
- Create: `Assets/VolleyballMatch/Tests/PlayMode/AiRallyPrototypePlayModeTests.cs`
- Modify: `docs/development.md`

- [ ] **Step 1: Write the failing PlayMode test against the saved scene.**

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.PlayModeTests
{
public sealed class AiRallyPrototypePlayModeTests
{
    [UnityTest]
    public IEnumerator PrototypeScene_CompletesThreeRalliesWithOnePointEach()
    {
        yield return SceneManager.LoadSceneAsync("AiRallyPrototype", LoadSceneMode.Single);
        var director = Object.FindFirstObjectByType<AiRallyDirector>();
        Assert.That(director, Is.Not.Null);

        var timeout = Time.realtimeSinceStartup + 35f;
        while (director.CompletedRallies < 3 && Time.realtimeSinceStartup < timeout)
            yield return null;

        Assert.That(director.CompletedRallies, Is.EqualTo(3));
        Assert.That(director.TotalScore, Is.EqualTo(3));
        Assert.That(director.IsRallyActive, Is.True);
    }
}
}
```

- [ ] **Step 2: Run this test and confirm it fails if the scene is missing from Build Settings, no director is found, or rally time exceeds the 35-second budget.**

Run:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.0.43f1/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -quit -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/PlayMode.xml" -logFile "$PWD/TestResults/PlayMode.log"
```

- [ ] **Step 3: Make only the timing or scene-registration corrections needed for the PlayMode test to pass, then run both test platforms.**

Expected: both Unity commands exit `0`; each XML result file reports no failed
tests; neither log contains `Exception` or `Error` emitted by the prototype.

- [ ] **Step 4: Add exact verification instructions to `docs/development.md`.**

Append this section:

```markdown
## All-AI prototype verification

Open `Assets/VolleyballMatch/Scenes/AiRallyPrototype.unity` with Unity
`6000.0.43f1`, enter Play Mode, and observe at least ten completed rallies.
Confirm that every rally has a serve, receive, set, spike and defensive
response; the tactical camera retains all players and the ball; score advances
once per rally; and the next rally begins automatically.

Run EditMode and PlayMode tests with the Unity batch commands recorded in the
implementation pull request. Preserve the generated XML and log files as
local review evidence; do not commit `TestResults/`.
```

- [ ] **Step 5: Perform the manual ten-rally check, inspect `git diff --check`, then commit tests and documentation.**

```bash
git diff --check
git add Assets/VolleyballMatch/Tests/PlayMode/AiRallyPrototypePlayModeTests.cs Assets/VolleyballMatch/Tests/PlayMode/VolleyballMatch.PlayModeTests.asmdef docs/development.md ProjectSettings/EditorBuildSettings.asset
git commit -m "test: verify all-ai rally prototype"
```

## Final Review Checklist

- [ ] `PrototypeMatch` is the only score owner; `AiRallyDirector` requests one point award per plan.
- [ ] Domain and AI compile without `UnityEngine` references.
- [ ] The planner always creates serve, receive, set, spike and defense within seven contacts.
- [ ] All six rigs expose the required joints and use named pose interpolation.
- [ ] The saved scene is in Build Settings and PlayMode test completes three rallies in 35 seconds.
- [ ] EditMode and PlayMode results are recorded with Unity `6000.0.43f1` and pass before a pull request.
