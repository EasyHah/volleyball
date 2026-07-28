# Full Rally V4 Gate J Perception and CourtAwareness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give formal 6v6 Gate H/I deterministic, filtered perception views so CourtAwareness changes only recognition delay, uncertainty, confidence, and legal support selection.

**Architecture:** Add pure immutable perception contracts and a deterministic AI adapter that consumes only public threat, visible state, own-team assignments, and existing derived awareness. The director creates event-owned perception receipts at existing Gate H/I revision boundaries; coordinators remain the sole command/lifecycle writers. Replay V4 serializes those receipts in a new optional canonical record without changing old bytes.

**Tech Stack:** Unity 6000.0.43f1, C#, NUnit EditMode/PlayMode, MatchReplayV4 strict canonical JSON.

---

## File Structure

| Path | Responsibility |
| --- | --- |
| `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Perception/CourtPerceptionV3.cs` | Immutable public observation, player/team view, configuration, support-decision contracts. |
| `Assets/Volleyball/Match/Runtime/AI/CourtPerceptionAdapterV3.cs` | Pure deterministic delay/error/confidence adapter and support selector. |
| `Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationAuthorityCoordinator.cs` | Accepts a filtered Gate J support input only at declared Gate H coverage boundaries. |
| `Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs` | Receives a filtered perceived public threat/support constraint, never a hidden final attack choice. |
| `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs` | Builds public perception inputs for formal Authority, stores event-owned receipts, and passes only support decisions downstream. |
| `Assets/Volleyball/Match/Runtime/Presentation/*AuthorityController.cs` | Carries the immutable perception receipt into accepted command receipts without tactical reselection. |
| `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs` | Maps event-owned receipts into Replay V4; refuses missing Gate J evidence only when Gate J is active. |
| `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs` | Optional strict/canonical Replay Gate J record, serializer, parser, equality/hash validation. |
| `Assets/Volleyball/Match/Tests/EditMode/CourtPerceptionAdapterV3Tests.cs` | Pure contract, information-boundary, monotonicity, deterministic-selection tests. |
| `Assets/Volleyball/Match/Tests/EditMode/*Authority*Tests.cs` | Coordinator/controller receipt and stale/hidden-data regressions. |
| `Assets/Volleyball/Shared/Tests/EditMode/MatchReplayV4Tests.cs` | Gate J replay canonical order, strict reader, historical-byte compatibility tests. |
| `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs` | Formal delayed/normal recognition, no writer duplication, legacy isolation. |
| `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs` | Recorder invariance and independent fixed-seed Gate J byte stability. |
| `docs/changes/2026-07-27-002-full-rally-v4-gate-j-perception-court-awareness.md` | Change record with fresh evidence and residual risks. |
| `docs/changes/README.md`, `docs/development.md`, `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md` | Gate J status and next-stage roadmap. |

### Task 1: Freeze Gate J contracts and information boundary

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Perception.meta`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Perception/CourtPerceptionV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Perception/CourtPerceptionV3.cs.meta`
- Create: `Assets/Volleyball/Match/Tests/EditMode/CourtPerceptionAdapterV3Tests.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/CourtPerceptionAdapterV3Tests.cs.meta`

- [ ] **Step 1: Write failing contract tests**

```csharp
[Test]
public void PerceptionContracts_ExposeOnlyObservedPublicFacts()
{
    var forbidden = new[] { "SelectedAction", "AttackCandidate", "ExecutionSample",
        "AttackDefenseAuthorityCoordinator", "MonoBehaviour", "GameObject" };
    var exposed = typeof(TeamPerceptionSnapshotV3).GetProperties()
        .Concat(typeof(PlayerPerceptionSnapshotV3).GetProperties())
        .Select(value => value.PropertyType.Name + ":" + value.Name).ToArray();
    CollectionAssert.IsEmpty(exposed.Where(value => forbidden.Any(value.Contains)));
}

[Test]
public void Configuration_RejectsNonMonotonicOrNonFiniteBounds()
{
    Assert.That(() => new CourtPerceptionConfigurationV3("gate-j-v1", .05f, .30f,
        .08f, 1.20f, .03f, .35f), Throws.ArgumentException);
}
```

- [ ] **Step 2: Run the new test to verify it fails**

Run:
```bash
UNITY='/Applications/Unity/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.CourtPerceptionAdapterV3Tests" \
  -testResults "$PWD/TestResults/GateJ-contract-red.xml" \
  -logFile "$PWD/TestResults/GateJ-contract-red.log"
```

Expected: compile failure because `CourtPerceptionConfigurationV3`, `PlayerPerceptionSnapshotV3`, and `TeamPerceptionSnapshotV3` do not exist.

- [ ] **Step 3: Implement immutable contracts**

Define only public observation values, including the following required shape:

```csharp
public sealed class CourtPerceptionConfigurationV3
{
    public CourtPerceptionConfigurationV3(string identity, float minimumDelaySeconds,
        float maximumDelaySeconds, float minimumPositionUncertaintyMeters,
        float maximumPositionUncertaintyMeters, float minimumArrivalUncertaintySeconds,
        float maximumArrivalUncertaintySeconds) { /* validate finite, non-negative, min <= max */ }
}

public sealed class PerceptionObservationV3<T>
{
    public PerceptionObservationV3(T estimate, float uncertainty, float confidence,
        float observedAtSimulationTime, string uncertaintyKey,
        IReadOnlyList<StablePlayerId> sources) { /* immutable validation/copy */ }
}

public sealed class TeamPerceptionSnapshotV3
{
    public TeamPerceptionSnapshotV3(string identity, long revision, long sourceSequence,
        TeamSide observingSide, string authoritativeArtifactIdentity,
        IReadOnlyList<PerceivedThreatEntryV3> visibleThreat,
        IReadOnlyList<PerceivedSupportCandidateV3> supportCandidates) { /* copy/sort */ }
}
```

Keep contract types in `Volleyball.Match.Domain.FullRallyV3`; reference `SimVector3`, `TeamSide`, and stable player IDs only. Sort all collections by stable canonical identity and validate every float is finite. Do not add any final-route, envelope sample, coordinator, Unity, or presentation field.

- [ ] **Step 4: Re-run contract tests**

Run the Task 1 command. Expected: all selected tests pass.

- [ ] **Step 5: Commit contract boundary**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Perception* \
  Assets/Volleyball/Match/Tests/EditMode/CourtPerceptionAdapterV3Tests*
git commit -m "feat: add gate j perception contracts"
```

### Task 2: Implement deterministic observation and support selection

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/AI/CourtPerceptionAdapterV3.cs`
- Create: `Assets/Volleyball/Match/Runtime/AI/CourtPerceptionAdapterV3.cs.meta`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/CourtPerceptionAdapterV3Tests.cs`

- [ ] **Step 1: Add failing deterministic and monotonic tests**

```csharp
[Test]
public void Observe_SameSeedAndPublicInput_IsByteEquivalent()
{
    var first = Fixture.Adapter.Observe(Fixture.Request(awareness: .45f, hiddenRoute: "line"));
    var second = Fixture.Adapter.Observe(Fixture.Request(awareness: .45f, hiddenRoute: "cross"));
    Assert.That(second.Identity, Is.EqualTo(first.Identity));
    CollectionAssert.AreEqual(first.VisibleThreat, second.VisibleThreat);
    Assert.That(second.SupportDecision, Is.EqualTo(first.SupportDecision));
}

[Test]
public void Observe_HigherAwareness_ReducesDelayAndUncertaintyWithoutChangingArtifact()
{
    var low = Fixture.Adapter.Observe(Fixture.Request(awareness: 0f));
    var high = Fixture.Adapter.Observe(Fixture.Request(awareness: 1f));
    Assert.That(high.ObservedBall.Uncertainty, Is.LessThan(low.ObservedBall.Uncertainty));
    Assert.That(high.ObservedBall.Confidence, Is.GreaterThan(low.ObservedBall.Confidence));
    Assert.That(high.AuthoritativeArtifactIdentity, Is.EqualTo(low.AuthoritativeArtifactIdentity));
}
```

- [ ] **Step 2: Verify the tests fail**

Run the Task 1 EditMode command. Expected: `CourtPerceptionAdapterV3` is missing.

- [ ] **Step 3: Implement the pure adapter and selector**

Implement `CourtPerceptionAdapterV3.Observe(CourtPerceptionRequestV3 request)` with:

```csharp
var awareness = Math.Max(0f, Math.Min(1f, request.ObserverAwareness));
var delay = Lerp(_configuration.MaximumDelaySeconds,
    _configuration.MinimumDelaySeconds, awareness);
var uncertainty = Lerp(_configuration.MaximumPositionUncertaintyMeters,
    _configuration.MinimumPositionUncertaintyMeters, awareness);
var key = DeterministicKey(request.MatchSeed, request.Revision,
    request.SourceSequence, request.ObservingSide, request.Observer, "ball");
```

Hash `key` with an explicit stable UTF-8/SHA-256 helper and map bytes into `[-1, 1]`; do not use `GetHashCode`, `Random`, wall-clock time, or Unity. Estimate only public ball/threat/candidate positions. The selector must first remove illegal/hard-conflict candidates, then sort by perceived arrival margin descending, confidence descending, committed-continuity descending, and stable player ID ordinal ascending. If confidence is below the named conservative threshold, return the declared committed/conservative candidate rather than a guessed opponent route.

- [ ] **Step 4: Add edge-case tests and run green**

Add tests for permutation-invariant candidate ordering, no legal support result, low-confidence conservative fallback, and exactly bounded error. Run the Task 1 command; expected selected tests pass.

- [ ] **Step 5: Commit adapter**

```bash
git add Assets/Volleyball/Match/Runtime/AI/CourtPerceptionAdapterV3* \
  Assets/Volleyball/Match/Tests/EditMode/CourtPerceptionAdapterV3Tests*
git commit -m "feat: add deterministic court perception"
```

### Task 3: Feed perception into Gate H/I at existing authority boundaries

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationAuthorityCoordinator.cs`
- Modify: `Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs`
- Modify: `Assets/Volleyball/Match/Runtime/AI/AttackDefenseAuthorityCoordinator.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityCoordinatorTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/JointDefensePlannerTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityCoordinatorTests.cs`

- [ ] **Step 1: Write failing authority-boundary tests**

```csharp
[Test]
public void JointDefense_PerceptionMayChangeSupportButNotHiddenFinalRouteOrEligibility()
{
    var line = Fixture.DefenseRequest(perception: Fixture.Perception("line"));
    var cross = Fixture.DefenseRequest(perception: Fixture.Perception("cross"));
    var first = new JointDefensePlanner().Plan(line);
    var second = new JointDefensePlanner().Plan(cross);
    CollectionAssert.AreEqual(first.Responsibilities, second.Responsibilities);
}

[Test]
public void ReceiveAuthority_RejectsPerceptionReceiptFromAnotherRevision()
{
    var coordinator = Fixture.PlannedCoordinator();
    Assert.That(() => coordinator.ApplyPerception(Fixture.Receipt(revision: 99)),
        Throws.InvalidOperationException);
}
```

- [ ] **Step 2: Verify the tests fail**

Run:
```bash
UNITY='/Applications/Unity/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.ReceiveOrganizationAuthorityCoordinatorTests|Volleyball.EditModeTests.JointDefensePlannerTests|Volleyball.EditModeTests.AttackDefenseAuthorityCoordinatorTests" \
  -testResults "$PWD/TestResults/GateJ-authority-red.xml" \
  -logFile "$PWD/TestResults/GateJ-authority-red.log"
```

Expected: compile failure for Gate J perception input/receipt APIs.

- [ ] **Step 3: Add receipt validation and narrowly scoped consumption**

Add an immutable `PerceptionReceiptV3` containing view identity, configuration identity, revision, source sequence, observing side, authoritative artifact identity, and `PerceptionSupportDecisionV3`. Require coordinator methods to validate revision/source sequence/side before retaining it. Do not replace `ReceiveOrganizationAuthorityRequestV3`, `PublicAttackThreatV3`, rules eligibility, execution envelope, or physical trajectory.

Change `JointDefensePlanningRequestV3` to accept the filtered `TeamPerceptionSnapshotV3` and require its `AuthoritativeArtifactIdentity` equal the public threat identity supplied by the same event. Use only its declared support choice to rank legal floor-support candidates after existing blocker selection. Preserve the existing primary/supporting block identity and public-threat-only behavior.

In `PhysicalMatchRallyDirector`, construct the request only when Gate J eligibility is true:

```csharp
var gateJEnabled = GateIAuthorityEnabled && _v3RulesAdapter.Mode == V3RulesMode.Authority &&
    _players.Count == 12;
```

For all other modes, retain the existing zero-error adapter and emit no Gate J receipt. Never call legacy scheduling as a fallback for a rejected perception receipt.

- [ ] **Step 4: Run focused authority tests**

Run the Task 3 command. Expected: selected suites pass with stale/mismatched receipts rejected before command publication.

- [ ] **Step 5: Commit authority integration**

```bash
git add Assets/Volleyball/Match/Runtime/AI/ReceiveOrganizationAuthorityCoordinator.cs \
  Assets/Volleyball/Match/Runtime/AI/JointDefensePlanner.cs \
  Assets/Volleyball/Match/Runtime/AI/AttackDefenseAuthorityCoordinator.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityCoordinatorTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/JointDefensePlannerTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityCoordinatorTests.cs
git commit -m "feat: apply gate j perception to formal support"
```

### Task 4: Carry event-owned Gate J receipts through controllers and replay

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- Modify: `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- Modify: `Assets/Volleyball/Shared/Tests/EditMode/MatchReplayV4Tests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityControllerTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs`

- [ ] **Step 1: Write failing event-owned replay tests**

```csharp
[Test]
public void GateJRecord_RoundTripsAndRejectsHiddenFinalRouteField()
{
    var replay = Fixture.ReplayWithGateJPerception();
    var json = MatchReplayV4Json.Serialize(replay);
    Assert.That(MatchReplayV4Json.Deserialize(json).CanonicalHash, Is.EqualTo(replay.CanonicalHash));
    Assert.That(() => MatchReplayV4Json.Deserialize(json.Replace("\"confidence\"", "\"selectedRoute\"")),
        Throws.InstanceOf<ContractValidationException>());
}

[Test]
public void HistoricalReplayWithoutGateJRecord_RetainsCanonicalBytes()
{
    CollectionAssert.AreEqual(Fixture.HistoricalBytes, MatchReplayV4Json.Serialize(Fixture.HistoricalReplay));
}
```

- [ ] **Step 2: Verify the tests fail**

Run:
```bash
UNITY='/Applications/Unity/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.Shared.EditModeTests.MatchReplayV4Tests|Volleyball.EditModeTests.ReceiveOrganizationAuthorityControllerTests|Volleyball.EditModeTests.AttackDefenseAuthorityControllerTests" \
  -testResults "$PWD/TestResults/GateJ-replay-red.xml" \
  -logFile "$PWD/TestResults/GateJ-replay-red.log"
```

Expected: compile failure because the Gate J replay record and receipt fields do not exist.

- [ ] **Step 3: Implement optional canonical replay record**

Add `ReplayPerceptionAuthorityRecordV4` with only: configuration identity, view identity, observing side, authoritative artifact identity, observed-at, delay, uncertainty key/range, confidence, visible threat records, selected support actor/zone, and affected revision/source sequence. Add it as the final optional constructor/property argument of `MatchReplayEventV4`, preserving all existing overload behavior and legacy JSON bytes when null.

Update strict parser/serializer in the same canonical field position. Reject unknown/missing required fields inside a non-null Gate J record, non-finite values, unsorted visible threats, and strings/properties naming final routes, samples, envelope errors, ability values, or internal plans.

Extend both authority receipts with the same immutable `PerceptionReceiptV3` reference. Controllers copy it from the preflighted batch evidence and never synthesize a receipt. `MatchReplayRecorder` maps only the event receipt; if Gate J is enabled and the formal event has no receipt, invalidate capture.

- [ ] **Step 4: Run replay/controller green tests**

Run the Task 4 command. Expected: selected tests pass, including historical canonical bytes.

- [ ] **Step 5: Commit replay evidence**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/ReceiveOrganizationAuthorityController.cs \
  Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs \
  Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs \
  Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs \
  Assets/Volleyball/Shared/Tests/EditMode/MatchReplayV4Tests.cs \
  Assets/Volleyball/Match/Tests/EditMode/ReceiveOrganizationAuthorityControllerTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs
git commit -m "feat: record gate j perception evidence"
```

### Task 5: Prove formal runtime behavior and legacy isolation

**Files:**
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`

- [ ] **Step 1: Write failing PlayMode scenarios**

Add these tests:

```csharp
[UnityTest]
public IEnumerator Formal6v6_GateJDelayedAndNormalRecognitionUseSameRuleAuthority()
{
    var low = yield return Fixture.RunFormalAwareness(.0f);
    var high = yield return Fixture.RunFormalAwareness(1f);
    Assert.That(low.RuleTransitions, Is.EqualTo(low.SuccessfulContacts));
    Assert.That(high.RuleTransitions, Is.EqualTo(high.SuccessfulContacts));
    Assert.That(low.HiddenArtifactIdentities, Is.Empty);
    Assert.That(high.HiddenArtifactIdentities, Is.Empty);
}

[UnityTest]
public IEnumerator ThreeVsThree_DoesNotEmitGateJReceipt()
{
    var result = yield return Fixture.RunLegacyThreeVsThree();
    Assert.That(result.GateJReceipts, Is.Empty);
}
```

- [ ] **Step 2: Verify scenarios fail**

Run:
```bash
UNITY='/Applications/Unity/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests.Formal6v6_GateJDelayedAndNormalRecognitionUseSameRuleAuthority|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests.ThreeVsThree_DoesNotEmitGateJReceipt" \
  -testResults "$PWD/TestResults/GateJ-focused-playmode-red.xml" \
  -logFile "$PWD/TestResults/GateJ-focused-playmode-red.log"
```

Expected: failure because Gate J receipts/awareness fixture are unavailable.

- [ ] **Step 3: Add minimal formal fixtures and assertions**

Use the existing formal bootstrap and fixed seed. Capture Gate J event receipts, V3 transition counts, contact groups, authority writer counters, applied movement correction, and replay records. Assert low/high awareness may choose different legal support but neither alters authoritative trajectory identity, rules contact sequence invariants, hidden-field exposure, or committed-action cancellation. Keep `Time.timeScale` cleanup in `finally`/postcondition assertions.

- [ ] **Step 4: Run focused PlayMode green**

Run the Task 5 command. Expected: `2/2` pass with no duplicate Gate H/I writers and no Gate J evidence in 3v3.

- [ ] **Step 5: Add fixed-seed recorder tests and commit**

Add one recorder-on/off invariance test and one two-independent-capture byte-equality test that both include non-null Gate J records. Then run:

```bash
UNITY='/Applications/Unity/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests.Formal6v6_GateJAuthorityIsRecorderInvariant|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests.Capture_TwoIndependentGateJFixedSeedRunsAreByteStable" \
  -testResults "$PWD/TestResults/GateJ-determinism.xml" \
  -logFile "$PWD/TestResults/GateJ-determinism.log"
```

Expected: `2/2` pass.

```bash
git add Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs
git commit -m "test: cover gate j formal perception"
```

### Task 6: Full validation, review, and delivery records

**Files:**
- Create: `docs/changes/2026-07-27-002-full-rally-v4-gate-j-perception-court-awareness.md`
- Modify: `docs/changes/README.md`
- Modify: `docs/development.md`
- Modify: `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

- [ ] **Step 1: Run complete EditMode suite**

```bash
UNITY='/Applications/Unity/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/GateJ-final-editmode.xml" \
  -logFile "$PWD/TestResults/GateJ-final-editmode.log"
```

Expected: zero failures, skips, and inconclusive tests. Record the exact XML totals and duration; do not copy Gate I figures.

- [ ] **Step 2: Run complete PlayMode suite**

```bash
UNITY='/Applications/Unity/Unity.app/Contents/MacOS/Unity'
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/GateJ-final-playmode.xml" \
  -logFile "$PWD/TestResults/GateJ-final-playmode.log"
```

Expected: zero failures, skips, and inconclusive tests. Record exact XML totals and duration.

- [ ] **Step 3: Run static boundary scans**

```bash
rg -n "SelectedAction|AttackCandidate|ExecutionSample|AttackDefenseAuthorityCoordinator|MonoBehaviour|GameObject" \
  Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Perception \
  Assets/Volleyball/Match/Runtime/AI/CourtPerceptionAdapterV3.cs
rg -n "UnityEngine|Volleyball\\.Presentation|MatchReplayRecorder" \
  Assets/Volleyball/Match/Runtime/AI Assets/Volleyball/Match/Runtime/Domain || true
rg -n "CourtAwareness" Assets/Volleyball/Match/Runtime --glob '*.cs'
git diff --check
```

Expected: first scan has no matches; AI/Domain reverse-dependency scan has no matches; CourtAwareness matches only Gate J contracts/adapter/integration; diff check exits zero.

- [ ] **Step 4: Perform combined independent review**

Review `d3121b9..HEAD` for: hidden-field leakage; any observer-specific physical recomputation; non-deterministic hash/random use; stale receipt acceptance; duplicate Gate H/I writers; command cancellation; replay historical compatibility; 3v3/Shadow leakage; and missing red-green coverage. Fix each finding with its focused test, then re-run the affected suite.

- [ ] **Step 5: Write change record and update roadmap**

Record exact full/focused/determinism evidence, Replay compatibility, reviewer findings, and outstanding manual/Windows/performance checks. Mark Gate J complete only after Steps 1--4 pass; leave Gate K as the next pending stage.

- [ ] **Step 6: Commit delivery records**

```bash
git add docs/changes/2026-07-27-002-full-rally-v4-gate-j-perception-court-awareness.md \
  docs/changes/README.md docs/development.md \
  docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md
git commit -m "docs: record gate j perception completion"
```

## Plan Self-Review

- Design coverage: Tasks 1--2 implement public immutable views and deterministic awareness; Task 3 restricts their formal Gate H/I consumption; Task 4 preserves event-owned replay and history compatibility; Task 5 covers formal runtime and legacy isolation; Task 6 provides escalated lifecycle/replay validation and documentation.
- Information boundary: every source and test rejects hidden final route, future sample, internal ability, Unity/presentation coupling, and observer-specific physics.
- Type consistency: `CourtPerceptionAdapterV3` produces `TeamPerceptionSnapshotV3` and `PerceptionSupportDecisionV3`; their event-owned carrier is `PerceptionReceiptV3`, used by coordinators, controllers, recorder, and Replay V4.
- Validation level: this crosses live authority lifecycle, canonical replay, and three bidirectional modules, so it requires focused red/green tests, full suites, fresh determinism, one independent review, static scans, and `git diff --check`.
