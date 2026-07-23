# Full Rally V3 Phase 1 Facts, Eligibility, and Rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the immutable Full Rally V3 world/eligibility facts and an actual-event-driven V3 rules engine, prove it against the legacy formal 6v6 contact path in shadow mode, then make it authoritative for formal 6v6 while preserving the legacy 3v3 path.

**Architecture:** New pure C# types live under `Volleyball.Match.Domain/FullRallyV3` and never depend on Unity or presentation state. A presentation adapter translates accepted physical events into V3 facts and runs legacy/V3 comparison before authority changes. The final authority switch is limited to formal 6v6; current 3v3 behavior and public V1/V2 initialization APIs remain unchanged.

**Tech Stack:** Unity 6000.0.43f1, C#, NUnit EditMode/PlayMode tests, `Volleyball.Shared`, `Volleyball.Match.Domain`, `Volleyball.Match.Presentation`.

## Global Constraints

- `docs/rules.md` remains the only normative rules source; Phase 1 implementation links R-GOV-001, R-GOV-002, R-REF-001--006, and R-PLAY-001--003 instead of redefining them.
- Rules query methods never mutate state. Only accepted actual events advance `TouchSequenceStateV3`.
- Formal V3 eligibility contains exactly six distinct on-court players per team. Off-court players receive no eligibility entry.
- A libero and the player replaced by that libero cannot coexist in one on-court snapshot.
- A legal block consumes zero counted hits, clears the opponent's prior consecutive-contact sequence, and allows the blocker to make the next team's first counted contact.
- Counted-contact legality is based on the actual contact classification, not the planner's intended `Receive`, `Set`, or `Attack` label.
- Back-row players and liberos cannot block. Back-row/libero attack restrictions use actual takeoff/contact geometry, not role labels alone.
- All decisions are deterministic from explicit inputs. No random source, wall-clock duration, hash-map enumeration order, or presentation-only state may affect a result.
- The Domain assembly remains `noEngineReferences: true`.
- V3 remains CPU-authoritative for Phase 1.
- Existing 3v3 behavior and public V1/V2 APIs remain source-compatible.

---

## File Structure

- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyWorldSnapshotV3.cs`: immutable authoritative ball, player, court, event, and rule facts.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/OnCourtEligibilitySnapshot.cs`: formal-six lineup and action eligibility facts.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/OnCourtLineupRulesV3.cs`: deterministic construction and validation of two six-player lineups.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/TouchSequenceStateV3.cs`: actual-event classifications and immutable touch-sequence transitions.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyRulesEngineV3.cs`: query/apply boundary for contact rules.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ActionEligibilityRulesV3.cs`: attack and block restrictions.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BoundaryAndNetRulesV3.cs`: V3 environment-event outcomes reusing the canonical court semantics.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/LegacyRulesShadowComparatorV3.cs`: parity and intentional-correction classification.
- Create `Assets/Volleyball/Match/Runtime/Presentation/FullRallyV3RulesRuntimeAdapter.cs`: translation from formal 6v6 runtime events to V3 rule events.
- Modify `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`: formal-only shadow/authority hook and diagnostics.
- Modify `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`: explicitly enable V3 rules for the formal scene.
- Create `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3WorldSnapshotTests.cs`.
- Create `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs`.
- Create `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RulesEngineTests.cs`.
- Create `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3ShadowRulesTests.cs`.
- Modify `Assets/Volleyball/Match/Tests/EditMode/MatchRallyRefereeTests.cs`.
- Modify `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`.
- Modify `docs/rules.md`, `docs/development.md`, and `docs/changes/2026-07-23-001-full-rally-v3-architecture.md`.

## Task 1: Immutable Authoritative World Snapshot

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyWorldSnapshotV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyWorldSnapshotV3.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3WorldSnapshotTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3WorldSnapshotTests.cs.meta`

**Interfaces:**
- Consumes: `SimVector3`, `Volleyball.Shared.Contracts.PlayerId`, `TeamSide`, `PlayerPosition`.
- Produces: `BallWorldSnapshotV3`, `PlayerWorldSnapshotV3`, `RallyWorldSnapshotV3`, `RallyCommitmentStateV3`, `AcceptedRuleEventV3`.

- [ ] **Step 1: Write failing immutability and validation tests**

Add tests that construct twelve player facts and assert defensive copying, stable input order, finite ball/player vectors, distinct player IDs, exactly six players per side, non-negative physical time/event sequence, and rejection of a seventh player on one side.

```csharp
[Test]
public void RallyWorldSnapshot_ContainsExactlySixImmutablePlayersPerSide()
{
    var players = CreateTwelvePlayers();
    var snapshot = CreateSnapshot(players);
    players[0] = players[1];

    Assert.That(snapshot.Players, Has.Count.EqualTo(12));
    Assert.That(snapshot.Players.Count(p => p.Side == TeamSide.Home), Is.EqualTo(6));
    Assert.That(snapshot.Players.Count(p => p.Side == TeamSide.Away), Is.EqualTo(6));
    Assert.That(snapshot.Players[0].PlayerId.Value, Is.EqualTo("home-1"));
    Assert.Throws<NotSupportedException>(
        () => ((IList<PlayerWorldSnapshotV3>)snapshot.Players)[0] = snapshot.Players[1]);
}
```

- [ ] **Step 2: Run the focused test and verify RED**

```bash
UNITY=/Applications/Unity/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.FullRallyV3WorldSnapshotTests" \
  -testResults "$PWD/TestResults/FullRallyV3-Phase1-Task1-red.xml" \
  -logFile "$PWD/TestResults/FullRallyV3-Phase1-Task1-red.log"
```

Expected: compile failure because `RallyWorldSnapshotV3` does not exist.

- [ ] **Step 3: Add the minimal immutable snapshot contracts**

Use namespace `Volleyball.Match.Domain.FullRallyV3`. Store copied arrays behind `ReadOnlyCollection<T>`. The public constructors must validate all enum values, required IDs/text, finite vectors/scalars, non-negative times, and the exact 6+6 side count.

```csharp
public enum RallyCommitmentStateV3 { Uncommitted, Preparing, Committed, Recovering }

public readonly struct BallWorldSnapshotV3
{
    public BallWorldSnapshotV3(
        SimVector3 position, SimVector3 velocity, SimVector3 spin,
        float radius, float physicalTimeSeconds);
    public SimVector3 Position { get; }
    public SimVector3 Velocity { get; }
    public SimVector3 Spin { get; }
    public float Radius { get; }
    public float PhysicalTimeSeconds { get; }
}

public sealed class PlayerWorldSnapshotV3
{
    public PlayerWorldSnapshotV3(
        PlayerId playerId, TeamSide side, PlayerPosition registeredPosition,
        SimVector3 position, SimVector3 velocity, SimVector3 facing,
        string pose, RallyCommitmentStateV3 commitment, float recoverySeconds);
}

public sealed class RallyWorldSnapshotV3
{
    public RallyWorldSnapshotV3(
        BallWorldSnapshotV3 ball,
        IReadOnlyList<PlayerWorldSnapshotV3> players,
        TouchSequenceStateV3 touchSequence,
        OnCourtEligibilitySnapshot eligibility,
        CourtConfigurationV3 court,
        AcceptedRuleEventV3 latestEvent,
        long eventSequence);
}
```

For Task 1 compilation only, add a minimal immutable `TouchSequenceStateV3.Initial` and `OnCourtEligibilitySnapshot` constructor shell in the same file; Tasks 2 and 3 move them to their final files without changing signatures.

- [ ] **Step 4: Run the focused test and full EditMode suite**

Expected: focused fixture passes and the full existing EditMode suite remains green.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 \
  Assets/Volleyball/Match/Tests/EditMode/FullRallyV3WorldSnapshotTests.cs*
git commit -m "feat: add full rally v3 world snapshots"
```

## Task 2: Formal Six-Player Eligibility Snapshot

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/OnCourtEligibilitySnapshot.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/OnCourtEligibilitySnapshot.cs.meta`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/OnCourtLineupRulesV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/OnCourtLineupRulesV3.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyWorldSnapshotV3.cs`

**Interfaces:**
- Consumes: `MatchContextV3`, rotation positions 1--6, current server, optional libero replacement.
- Produces: `OnCourtPlayerEligibilityV3`, `LiberoReplacementV3`, `OnCourtEligibilitySnapshot`, `OnCourtLineupRulesV3.Create`.

- [ ] **Step 1: Write failing six-player, rotation, and libero tests**

Cover deterministic semantic rotation order, P1 server, P2--P4 front row, P5/P6/P1 back row, off-court exclusion from a roster larger than six, current-server membership, duplicate rejection, and libero/replaced-player coexistence rejection.

```csharp
[Test]
public void Create_MapsRotationAndBlockEligibilityForFormalSix()
{
    var snapshot = OnCourtLineupRulesV3.Create(
        CreateV3ContextWithSixPerSide(),
        homeRotationOrder: HomeIds,
        awayRotationOrder: AwayIds,
        homeServer: HomeIds[0],
        awayServer: AwayIds[0],
        liberoReplacements: Array.Empty<LiberoReplacementV3>());

    Assert.That(snapshot.Players, Has.Count.EqualTo(12));
    Assert.That(snapshot.For(HomeIds[0]).RotationPosition, Is.EqualTo(1));
    Assert.That(snapshot.For(HomeIds[0]).IsFrontRow, Is.False);
    Assert.That(snapshot.For(HomeIds[1]).CanBlock, Is.True);
    Assert.That(snapshot.For(HomeLiberoId).CanBlock, Is.False);
}
```

- [ ] **Step 2: Run the fixture and verify RED**

Expected: compile failure because the final eligibility types do not exist.

- [ ] **Step 3: Implement exact eligibility facts and lineup construction**

```csharp
public sealed class OnCourtPlayerEligibilityV3
{
    public PlayerId PlayerId { get; }
    public TeamSide Side { get; }
    public PlayerPosition RegisteredPosition { get; }
    public int RotationPosition { get; }
    public bool IsFrontRow { get; }
    public bool IsCurrentServer { get; }
    public bool CanBlock { get; }
    public bool CanAttackAboveNetFromFrontZone { get; }
    public PlayerId? ReplacedPlayerId { get; }
}

public static class OnCourtLineupRulesV3
{
    public static OnCourtEligibilitySnapshot Create(
        MatchContextV3 context,
        IReadOnlyList<PlayerId> homeRotationOrder,
        IReadOnlyList<PlayerId> awayRotationOrder,
        PlayerId homeServer,
        PlayerId awayServer,
        IReadOnlyList<LiberoReplacementV3> liberoReplacements);
}
```

`Create` must iterate the two supplied six-ID rotation arrays, resolve each ID in its matching V3 team, and never infer a player from role labels. `CanBlock` is false for liberos and rotation positions 1, 5, 6; all other front-row non-libero players are true.

- [ ] **Step 4: Run focused and full EditMode tests**

Expected: all eligibility tests and all existing EditMode tests pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 \
  Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs*
git commit -m "feat: add full rally v3 on-court eligibility"
```

## Task 3: Actual-Event Touch Sequence State Machine

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/TouchSequenceStateV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/TouchSequenceStateV3.cs.meta`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyRulesEngineV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyRulesEngineV3.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RulesEngineTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RulesEngineTests.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyWorldSnapshotV3.cs`

**Interfaces:**
- Consumes: accepted physical contact events and environment events.
- Produces: `RallyContactClassificationV3`, `ActualContactEventV3`, `RuleTransitionV3`, immutable `TouchSequenceStateV3`, `RallyRulesEngineV3.CanAttempt/Apply`.

- [ ] **Step 1: Write failing transition tests**

Cover: planning queries do not mutate; serve/team contacts count; intended technique does not affect count; same actor consecutive counted contact faults; fourth hit faults; block consumes zero hits; blocker may take next first counted hit; block rebound to either side starts a fresh sequence; duplicate contact-group events are ignored; simultaneous same-team contact counts once; terminal events cannot accept later contact.

```csharp
[Test]
public void BlockThenSameBlockerTeamContact_StartsFreshThreeHitSequence()
{
    var engine = RallyRulesEngineV3.Open(TeamSide.Home);
    var block = engine.Apply(Contact(AwayBlocker, RallyContactClassificationV3.BlockContact, 10));
    var first = engine.Apply(Contact(AwayBlocker, RallyContactClassificationV3.TeamContact, 11));

    Assert.That(block.After.CountedHits, Is.Zero);
    Assert.That(first.Accepted, Is.True);
    Assert.That(first.After.CurrentCountedSequenceTeam, Is.EqualTo(TeamSide.Away));
    Assert.That(first.After.CountedHits, Is.EqualTo(1));
    Assert.That(first.After.RemainingHits, Is.EqualTo(2));
}
```

- [ ] **Step 2: Run the fixture and verify RED**

Expected: compile failure for missing V3 rules engine types.

- [ ] **Step 3: Implement immutable event and transition types**

```csharp
public enum RallyContactClassificationV3
{
    ServeContact, TeamContact, BlockContact,
    SimultaneousTeamContact, EnvironmentContact
}

public enum RuleRejectionReasonV3
{
    None, DuplicateContactGroup, RallyClosed,
    ConsecutiveCountedContact, FourthCountedContact,
    ActorNotOnCourt, ActionIneligible
}

public sealed class TouchSequenceStateV3
{
    public TeamSide? LastLegalPhysicalContactTeam { get; }
    public TeamSide? CurrentCountedSequenceTeam { get; }
    public int CountedHits { get; }
    public PlayerId? LastCountedActor { get; }
    public RallyContactClassificationV3? LastContactClassification { get; }
    public long? LastContactGroup { get; }
    public int RemainingHits => 3 - CountedHits;
    public bool IsTerminal { get; }
}
```

`Apply` returns a new `RuleTransitionV3` and updates the engine only for accepted events. For `BlockContact`, clear `CurrentCountedSequenceTeam`, `CountedHits`, and `LastCountedActor`; retain the blocker's team as `LastLegalPhysicalContactTeam`. For a later counted contact by either team, begin that team's sequence at one hit.

- [ ] **Step 4: Run focused and full EditMode tests**

Expected: new tests pass; legacy `RallyTouchStateTests` continue to pass unchanged.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 \
  Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RulesEngineTests.cs*
git commit -m "feat: add actual-event v3 touch rules"
```

## Task 4: Attack, Block, Boundary, and Net Rule Modules

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ActionEligibilityRulesV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ActionEligibilityRulesV3.cs.meta`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BoundaryAndNetRulesV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BoundaryAndNetRulesV3.cs.meta`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/MatchRallyRefereeTests.cs`

**Interfaces:**
- Consumes: `OnCourtPlayerEligibilityV3`, actual takeoff/contact points, net/court geometry.
- Produces: `AttackAttemptFactsV3`, `BlockAttemptFactsV3`, `ActionEligibilityDecisionV3`, `BoundaryAndNetRulesV3`.

- [ ] **Step 1: Write failing geometry and environment tests**

Test a back-row takeoff behind the attack line as legal, a back-row above-net attack from the front zone as illegal, a below-net front-zone contact as legal, all libero blocks as illegal, front-row non-libero blocks as legal, legal antenna crossing as continuation, illegal antenna crossing as terminal opponent point, own-court landing as opponent point, and opponent-court landing as final-touch-side point.

- [ ] **Step 2: Run both focused fixtures and verify RED**

Expected: compile failure for missing action and boundary rule modules.

- [ ] **Step 3: Implement pure query modules**

```csharp
public static class AttackEligibilityRulesV3
{
    public static ActionEligibilityDecisionV3 CanAttempt(
        OnCourtPlayerEligibilityV3 player,
        SimVector3 takeoffPoint,
        SimVector3 contactPoint,
        float attackLineDistanceFromCenter,
        float netHeight);
}

public static class BlockEligibilityRulesV3
{
    public static ActionEligibilityDecisionV3 CanAttempt(
        OnCourtPlayerEligibilityV3 player);
}

public static class BoundaryAndNetRulesV3
{
    public static RallyOutcome ResolveGroundLanding(
        TeamSide finalTouchSide, SimVector3 landingPoint,
        float halfWidth, float halfLength);
    public static RallyOutcome? ResolveNetCrossing(
        TeamSide finalTouchSide, SimVector3 crossingPoint,
        float antennaHalfWidth, float netHeight);
}
```

Reuse `MatchRallyReferee` for outcome semantics rather than duplicating scoring branches. The V3 wrapper validates V3 event facts and delegates the canonical result.

- [ ] **Step 4: Run focused and full EditMode tests**

Expected: all new geometry tests and existing referee tests pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 \
  Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/MatchRallyRefereeTests.cs
git commit -m "feat: add v3 action and boundary rules"
```

## Task 5: Deterministic Legacy/V3 Shadow Comparator

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/LegacyRulesShadowComparatorV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/LegacyRulesShadowComparatorV3.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3ShadowRulesTests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3ShadowRulesTests.cs.meta`

**Interfaces:**
- Consumes: a legacy disposition/reason projection and `RuleTransitionV3`.
- Produces: `RulesShadowComparisonV3`, `RulesShadowDifferenceKindV3`.

- [ ] **Step 1: Write failing parity classification tests**

Cover exact parity, unexpected mismatch, intentional V3 correction for block-to-new-sequence, intentional V3 correction for incidental contact counting, and deterministic diagnostic strings.

```csharp
[Test]
public void Compare_BlockSequenceCorrection_IsClassifiedIntentional()
{
    var result = LegacyRulesShadowComparatorV3.Compare(
        LegacyRuleOutcomeV3.Fault("ConsecutiveCountedTouch"),
        AcceptedV3TransitionAfterBlock(),
        ShadowScenarioV3.BlockerFirstCountedContact);

    Assert.That(result.IsParity, Is.False);
    Assert.That(result.DifferenceKind,
        Is.EqualTo(RulesShadowDifferenceKindV3.IntentionalV3Correction));
}
```

- [ ] **Step 2: Run the fixture and verify RED**

- [ ] **Step 3: Implement an allow-list with exact scenario/reason pairs**

Do not classify by free-form substring. Only these two Phase 1 pairs are intentional:

```csharp
(ShadowScenarioV3.BlockerFirstCountedContact,
 "ConsecutiveCountedTouch",
 RuleRejectionReasonV3.None)

(ShadowScenarioV3.IncidentalCountedContact,
 "WrongAction",
 RuleRejectionReasonV3.None)
```

All other disagreement is `UnexpectedMismatch`. Diagnostics use enum names and stable IDs in fixed field order.

- [ ] **Step 4: Run focused and full EditMode tests**

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3 \
  Assets/Volleyball/Match/Tests/EditMode/FullRallyV3ShadowRulesTests.cs*
git commit -m "feat: compare legacy and v3 rally rules"
```

## Task 6: Formal 6v6 Shadow Runtime Integration

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/FullRallyV3RulesRuntimeAdapter.cs`
- Create: `Assets/Volleyball/Match/Runtime/Presentation/FullRallyV3RulesRuntimeAdapter.cs.meta`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`

**Interfaces:**
- Consumes: formal 6v6 V2 context upgraded explicitly with `MatchContextV3.UpgradeFromV2`, `MatchSet` rotation facts, accepted runtime contacts.
- Produces: `FullRallyV3RulesRuntimeAdapter`, `V3RulesMode.Disabled/Shadow/Authority`, parity counters and last diagnostic.

- [ ] **Step 1: Write failing integration tests**

EditMode reflection tests require a distinct `InitializeV3Rules`/configuration path without overloading existing V1/V2 APIs. PlayMode asserts formal 6v6 enables shadow mode, observes at least one V3 transition, reports zero unexpected mismatches over completed rallies, and preserves score/contact invariants.

- [ ] **Step 2: Run focused EditMode and PlayMode tests and verify RED**

- [ ] **Step 3: Add the runtime adapter and formal-only initialization**

```csharp
public enum V3RulesMode { Disabled, Shadow, Authority }

public sealed class FullRallyV3RulesRuntimeAdapter
{
    public FullRallyV3RulesRuntimeAdapter(
        MatchContextV3 context,
        OnCourtEligibilitySnapshot eligibility,
        TeamSide initialPossession,
        V3RulesMode mode);

    public RuleTransitionV3 ObserveAcceptedContact(
        PlayerId actor,
        TeamSide side,
        RallyContactClassificationV3 classification,
        long contactGroup);
}
```

The bootstrap must call an explicitly named configuration method after `InitializeV2`. Do not change literal-null overload behavior. `PhysicalMatchRallyDirector` exposes read-only counters:

```csharp
public int V3RuleTransitions { get; private set; }
public int V3RuleParityMatches { get; private set; }
public int V3RuleIntentionalCorrections { get; private set; }
public int V3RuleUnexpectedMismatches { get; private set; }
public string LastV3RuleDiagnostic { get; private set; }
```

In Shadow mode the legacy resolution remains authoritative. The adapter observes only after the physical event has passed legacy acceptance, so shadow observation cannot alter ball velocity or score.

- [ ] **Step 4: Run focused and full EditMode/PlayMode suites**

Expected: formal PlayMode produces V3 transitions with zero unexpected mismatches; existing 3v3 suites remain unchanged.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation \
  Assets/Volleyball/Match/Tests/EditMode/SharedBoundaryTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs
git commit -m "feat: run v3 rules in formal match shadow mode"
```

## Task 7: Formal 6v6 Authority Gate and Phase 1 Handoff

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/FullRallyV3RulesRuntimeAdapter.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `docs/rules.md`
- Modify: `docs/development.md`
- Modify: `docs/changes/2026-07-23-001-full-rally-v3-architecture.md`

**Interfaces:**
- Consumes: verified V3 rules adapter from Task 6.
- Produces: formal 6v6 V3-authoritative contact legality with a retained shadow diagnostic comparison.

- [ ] **Step 1: Add failing authority and regression tests**

Add PlayMode tests for:

- accepted block followed by the same blocker making the first counted contact;
- block rebound retained by the blocking side with three hits available;
- block rebound returned to the attacking side with three fresh hits available;
- a fourth counted contact fault before response application;
- formal 6v6 reports `V3RulesMode.Authority`;
- existing 3v3 reports `V3RulesMode.Disabled`;
- score advances exactly once per completed rally.

- [ ] **Step 2: Run focused PlayMode tests and verify RED**

- [ ] **Step 3: Switch only formal 6v6 contact legality to V3 authority**

Before applying technique response, translate the physical event, call `RallyRulesEngineV3.Apply`, and map rejected transitions to `BallContactResolution.Fault/Ignore`. Keep the legacy evaluator as a non-authoritative comparator for diagnostics. Environment scoring continues through `MatchRallyReferee`.

The authority selection must be explicit:

```csharp
director.ConfigureV3Rules(
    MatchContextV3.UpgradeFromV2(context),
    V3RulesMode.Authority);
```

Do not enable V3 for `Physical3v3Rally`.

- [ ] **Step 4: Update canonical rule traceability and handoff docs**

In `docs/rules.md`, update the compliance table so R-REF-002/003 list `RallyRulesEngineV3` as formal 6v6 authority while retaining `RallyTouchState` for legacy 3v3. Do not duplicate the rule text.

In `docs/development.md`, record the exact formal V3 test commands and local XML paths.

In the existing change record, mark Phase 1 facts/eligibility/rules complete, list created files, record Unity `6000.0.43f1`, test totals, shadow mismatch counts, and the formal-only authority boundary.

- [ ] **Step 5: Run final verification**

```bash
UNITY=/Applications/Unity/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/FullRallyV3-Phase1-final-edit.xml" \
  -logFile "$PWD/TestResults/FullRallyV3-Phase1-final-edit.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/FullRallyV3-Phase1-final-play.xml" \
  -logFile "$PWD/TestResults/FullRallyV3-Phase1-final-play.log"
git diff --check
```

Expected: zero failed tests, zero unexpected V3 shadow mismatches, and no whitespace errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Volleyball/Match docs/rules.md docs/development.md \
  docs/changes/2026-07-23-001-full-rally-v3-architecture.md
git commit -m "feat: make v3 rules authoritative for formal rallies"
```

## Final Review Checklist

- [ ] Every formal on-court snapshot has 6 home + 6 away distinct stable IDs.
- [ ] Query APIs are side-effect free; only accepted actual events advance rules.
- [ ] Block and post-block transitions satisfy R-REF-003 and R-REF-004.
- [ ] Back-row/libero restrictions use eligibility plus actual geometry.
- [ ] Shadow differences are either exact parity, one of two named intentional corrections, or unexpected failures.
- [ ] Formal 6v6 alone uses V3 authority; legacy 3v3 remains unchanged.
- [ ] Domain code has no `UnityEngine` dependency.
- [ ] Full EditMode and PlayMode XML evidence is present under ignored `TestResults/`.
- [ ] Rules, development guide, and change record match the implemented authority boundary.
