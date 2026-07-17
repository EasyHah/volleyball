# Match Set Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the `Physical3v3Rally` scene finish one 15-point, win-by-two set with standard service transfer/three-player rotation and expose a validated six-player `MatchResultV1`.

**Architecture:** A Unity-free `MatchSet` in `Volleyball.Match.Domain` owns every rule and statistic required to finish a set. `ThreeVsThreeRallyDirector` adapts physical events into one resolved rally and delegates score, rotation, and result production to the aggregate; its bootstrap creates the deterministic sandbox context needed by the aggregate.

**Tech Stack:** Unity `6000.0.43f1`, C# Unity Assembly Definitions, NUnit EditMode tests, Unity Test Framework PlayMode tests, `Volleyball.Shared.Contracts`.

---

## File Structure

- Create: `Assets/Volleyball/Match/Runtime/Domain/MatchSet.cs` - pure set state, rotation, stat accumulation, and `MatchResultV1` construction.
- Create: `Assets/Volleyball/Match/Tests/EditMode/MatchSetTests.cs` - deterministic rule and result tests.
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyBootstrap.cs` - create one stable six-player sandbox context and inject it into the director.
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs` - resolve physical rallies through `MatchSet`, apply rotations, stop at completion, and expose result state.
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ScoreDisplay.cs` - render live set score and final-result-ready state without depending on `PrototypeMatch`.
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs` - verify a scene set reaches a valid terminal result.
- Create: `docs/changes/2026-07-17-005-match-set-completion.md` - cross-module delivery record.
- Modify: `docs/changes/README.md` - add CHG-005 at the top of the change index.
- Modify: `docs/development.md` - explain the completed-set scene acceptance criteria.

### Task 1: Add The Pure Set Rules

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Domain/MatchSet.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/MatchSetTests.cs`

- [ ] **Step 1: Write the failing tests for scoring, service, and rotation**

```csharp
[Test]
public void ResolveRally_ReceivingTeamWins_TakesServiceAndRotatesOnce()
{
    var set = CreateSet(servingTeam: TeamSide.Home);

    set.ResolveRally(TeamSide.Away, null, null);

    Assert.That(set.AwayScore, Is.EqualTo(1));
    Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Away));
    Assert.That(set.RotationOffsetFor(TeamSide.Away), Is.EqualTo(1));
    Assert.That(set.RotationOffsetFor(TeamSide.Home), Is.Zero);
}

[Test]
public void ResolveRally_ServingTeamWins_KeepsServiceWithoutRotation()
{
    var set = CreateSet(servingTeam: TeamSide.Home);

    set.ResolveRally(TeamSide.Home, null, null);

    Assert.That(set.HomeScore, Is.EqualTo(1));
    Assert.That(set.ServingSide, Is.EqualTo(TeamSide.Home));
    Assert.That(set.RotationOffsetFor(TeamSide.Home), Is.Zero);
}

[Test]
public void ResolveRally_AtFourteenAll_RequiresTwoPointLeadToComplete()
{
    var set = CreateSetAtScore(14, 14, TeamSide.Home);

    set.ResolveRally(TeamSide.Home, null, null);
    Assert.That(set.IsComplete, Is.False);

    set.ResolveRally(TeamSide.Home, null, null);
    Assert.That(set.IsComplete, Is.True);
    Assert.That(set.HomeScore, Is.EqualTo(16));
    Assert.That(set.AwayScore, Is.EqualTo(14));
}
```

- [ ] **Step 2: Run the focused EditMode suite to verify it fails**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.MatchSetTests" \
  -testResults "$PWD/TestResults/MatchSet-red.xml" \
  -logFile "$PWD/TestResults/MatchSet-red.log"
```

Expected: compilation failure because `MatchSet` does not exist.

- [ ] **Step 3: Implement the minimal `MatchSet` aggregate**

```csharp
public sealed class MatchSet
{
    public const int TargetScore = 15;
    public const int MinimumLead = 2;

    public MatchSet(MatchContextV1 context, TeamSide firstServer) { /* initialize all six stats */ }

    public int HomeScore { get; }
    public int AwayScore { get; }
    public TeamSide ServingSide { get; }
    public bool IsComplete { get; }

    public int RotationOffsetFor(TeamSide side) { /* return 0..2 */ }

    public void RecordContact(PlayerId playerId, float movementDistance) { /* add contact/workload */ }

    public void ResolveRally(
        TeamSide winner,
        PlayerId? pointScorer,
        PlayerId? errorPlayer) { /* score, attribution, service, rotation, completion */ }

    public MatchResultV1 CreateResult() { /* require complete and map Home/Away to TeamId */ }
}
```

Use `TeamSide` and stable `PlayerId` from `Volleyball.Shared.Contracts`; reject unknown players, negative or non-finite movement distance, and any resolution after `IsComplete`. Award `points` only to a winning-side scorer, and award `errors` only to a losing-side player.

- [ ] **Step 4: Run the focused EditMode suite to verify it passes**

Run the command from Step 2 with `MatchSet-green.xml` and `MatchSet-green.log`.

Expected: all `MatchSetTests` pass.

- [ ] **Step 5: Commit the pure rules**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/MatchSet.cs Assets/Volleyball/Match/Tests/EditMode/MatchSetTests.cs
git commit -m "feat: add match set scoring rules"
```

### Task 2: Complete Statistics And Result Validation

**Files:**
- Modify: `Assets/Volleyball/Match/Tests/EditMode/MatchSetTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Domain/MatchSet.cs`

- [ ] **Step 1: Write failing result-statistics tests**

```csharp
[Test]
public void CreateResult_CompletedSet_ContainsAllSixPlayersAndValidatedStatistics()
{
    var set = CreateSet(servingTeam: TeamSide.Home);
    var homeSetter = Player("home-setter");
    var awayDefender = Player("away-defender");

    set.RecordContact(homeSetter, 3.5f);
    set.ResolveRally(TeamSide.Home, homeSetter, awayDefender);
    ResolveUntilHomeWins(set);

    var result = set.CreateResult();

    Assert.That(result.PlayerStats, Has.Count.EqualTo(6));
    Assert.That(Stat(result, homeSetter).Points, Is.EqualTo(1));
    Assert.That(Stat(result, homeSetter).Contacts, Is.EqualTo(1));
    Assert.That(Stat(result, homeSetter).Workload, Is.EqualTo(4.5f));
    Assert.That(Stat(result, awayDefender).Errors, Is.EqualTo(1));
    Assert.DoesNotThrow(() => result.ValidateAgainst(Context));
}

[Test]
public void CreateResult_IncompleteSet_ThrowsInvalidOperationException()
{
    Assert.Throws<InvalidOperationException>(() => CreateSet(TeamSide.Home).CreateResult());
}
```

- [ ] **Step 2: Run the focused suite and verify the expected failure**

Run the Task 1 command with `MatchSet-result-red.xml` and filter
`Volleyball.EditModeTests.MatchSetTests`.

Expected: failure because `CreateResult` and stat accumulation are incomplete.

- [ ] **Step 3: Implement result production and attribution guards**

```csharp
public MatchResultV1 CreateResult()
{
    if (!IsComplete)
    {
        throw new InvalidOperationException("A result is available only after the set completes.");
    }

    return MatchResultV1.Create(
        _context,
        WinnerSide == TeamSide.Home ? _context.Home.TeamId : _context.Away.TeamId,
        HomeScore,
        AwayScore,
        _statsByPlayer.Values.OrderBy(stat => stat.PlayerId.Value).ToArray());
}
```

Represent mutable counters privately and build fresh immutable `PlayerMatchStatsV1` values when creating the result. The six entries must be initialized from both context rosters, not inferred from contacts.

- [ ] **Step 4: Run the focused suite to verify it passes**

Run the Task 1 command with `MatchSet-result-green.xml`.

Expected: all `MatchSetTests` pass, including context validation.

- [ ] **Step 5: Commit the result behavior**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/MatchSet.cs Assets/Volleyball/Match/Tests/EditMode/MatchSetTests.cs
git commit -m "feat: produce match set results"
```

### Task 3: Inject The Deterministic Sandbox Context

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyBootstrap.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs`

- [ ] **Step 1: Write the failing director state test**

```csharp
[Test]
public void Initialize_CreatesSetWithSandboxContextAndBlueServing()
{
    var director = new GameObject().AddComponent<ThreeVsThreeRallyDirector>();

    director.InitializeForTests(CreateContext(), TeamSide.Home);

    Assert.That(director.SetHomeScore, Is.Zero);
    Assert.That(director.SetAwayScore, Is.Zero);
    Assert.That(director.ServingSide, Is.EqualTo(TeamSide.Home));
    Assert.That(director.Result, Is.Null);
}
```

- [ ] **Step 2: Run the focused director test to verify it fails**

Run the Task 1 command with test filter
`Volleyball.EditModeTests.ThreeVsThreeRallyDirectorStateTests`.

Expected: compilation failure because the injected context API and state properties do not exist.

- [ ] **Step 3: Create the sandbox context in the bootstrap and pass it to the director**

```csharp
var context = SandboxMatchContext.Create();
director.Initialize(ball, agents, context, TeamSide.Home);
```

Put `SandboxMatchContext` in `ThreeVsThreeRallyBootstrap.cs` as a private factory. Its Home/Away teams must use the six stable IDs consumed by `MatchSet`; ability values must correspond to the profiles assigned to the scene agents; and its seed must remain `7351`. Do not introduce a Bootstrap assembly dependency.

- [ ] **Step 4: Add minimal director state for the injected set**

```csharp
public int SetHomeScore => _set.HomeScore;
public int SetAwayScore => _set.AwayScore;
public TeamSide ServingSide => _set.ServingSide;
public MatchResultV1 Result { get; private set; }
```

Create `_set` before scheduling the initial ball. Preserve the existing `Initialize` null and six-agent validation.

- [ ] **Step 5: Run the director test to verify it passes**

Run the Task 3 Step 2 command with `Director-set-green.xml`.

Expected: the director exposes the untouched sandbox set state.

- [ ] **Step 6: Commit sandbox integration**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyBootstrap.cs Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs Assets/Volleyball/Match/Tests/EditMode/ThreeVsThreeRallyDirectorStateTests.cs
git commit -m "feat: initialize physical set context"
```

### Task 4: Resolve Physical Rallies And Stop On Completion

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ScoreDisplay.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyBootstrap.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`

- [ ] **Step 1: Write the failing set-completion PlayMode test**

```csharp
[UnityTest]
public IEnumerator PhysicalScene_CompletesOneSetAndExposesValidatedResult()
{
    yield return SceneManager.LoadSceneAsync("Physical3v3Rally", LoadSceneMode.Single);
    var director = Object.FindFirstObjectByType<ThreeVsThreeRallyDirector>();

    var timeout = Time.realtimeSinceStartup + 120f;
    while (director.Result == null && Time.realtimeSinceStartup < timeout)
    {
        yield return null;
    }

    Assert.That(director.Result, Is.Not.Null);
    Assert.That(Mathf.Max(director.Result.HomeScore, director.Result.AwayScore), Is.GreaterThanOrEqualTo(15));
    Assert.That(Mathf.Abs(director.Result.HomeScore - director.Result.AwayScore), Is.GreaterThanOrEqualTo(2));
    Assert.That(director.Result.PlayerStats, Has.Count.EqualTo(6));
    Assert.That(director.IsLoopRunning, Is.False);
}
```

- [ ] **Step 2: Run the PlayMode test and verify it fails**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests" \
  -testResults "$PWD/TestResults/PhysicalSet-red.xml" \
  -logFile "$PWD/TestResults/PhysicalSet-red.log"
```

Expected: compilation failure or timeout because a completed result is not yet produced.

- [ ] **Step 3: Route accepted contacts and terminal faults into `MatchSet`**

```csharp
private void ResolveRally(TeamSide winner, PlayerId? scorer, PlayerId? errorPlayer, string reason)
{
    _set.ResolveRally(winner, StableId(scorer), StableId(errorPlayer));
    _status = reason + $"  {SetHomeScore}:{SetAwayScore}";

    if (_set.IsComplete)
    {
        Result = _set.CreateResult();
        StopCompletedSet();
        return;
    }

    ApplyRotationIfChanged();
    StartCoroutine(StartNextRally(0.65f));
}
```

On each accepted `HandlePlayerContact`, call `RecordContact` with the agent's movement distance before changing expected contact. On timeout/environment collision, use the currently expected actor as `errorPlayer`, award the other side, and use the most recent accepted actor only when that actor belongs to the winning team as `scorer`; otherwise pass null. Ensure one terminal event cannot resolve the same rally twice.

`StopCompletedSet` must cancel all scheduled contacts, set `_waitingForContact` and `_restartScheduled` so no coroutine restarts, stop/reset the ball velocity through a dedicated `SimulatedBall.Stop()` API if one does not exist, and display `RESULT READY` plus final score.

- [ ] **Step 4: Apply roster rotations and score display**

Use each team's `RotationOffsetFor` to map its stable players into its three existing court targets before `PrepareForTraining`. Keep the six agents and visual jersey labels; rotate their court assignments rather than reassigning stable IDs. Replace `ScoreDisplay.Render(PrototypeMatch)` with a render method accepting score, serving side, and completion state. Create and update the display from `ThreeVsThreeRallyBootstrap`/director.

- [ ] **Step 5: Run the focused PlayMode test to verify it passes**

Run the Task 4 Step 2 command with `PhysicalSet-green.xml`.

Expected: one physical scene completes a valid 15-point win-by-two set, returns six stats entries, and schedules no subsequent rally.

- [ ] **Step 6: Run the full suites**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/EditMode.xml" -logFile "$PWD/TestResults/EditMode.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/PlayMode.xml" -logFile "$PWD/TestResults/PlayMode.log"
```

Expected: both XML roots report `result="Passed"` with zero failed tests.

- [ ] **Step 7: Commit the scene behavior**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs
git commit -m "feat: complete physical 3v3 sets"
```

### Task 5: Document The Cross-Module Result Contract

**Files:**
- Create: `docs/changes/2026-07-17-005-match-set-completion.md`
- Modify: `docs/changes/README.md`
- Modify: `docs/development.md`

- [ ] **Step 1: Add the change record**

Create CHG-005 with `状态：已完成`, `负责人：Match`, `影响模块：Match / Shared / Career`, and `交互级别：跨模块（重点）`. State that Match now emits the existing V1 result after one completed 15-point win-by-two set, that Shared fields are unchanged, and that Career can consume only a non-null validated result after its future Bootstrap handoff.

- [ ] **Step 2: Add the index row and scene instructions**

Insert CHG-005 at the top of `docs/changes/README.md`. In `docs/development.md`, state the accepted `Physical3v3Rally` behavior: a result-ready final score at 15 with a two-point lead, complete six-player stats, and no further rally after completion.

- [ ] **Step 3: Verify documentation and tracked files**

```bash
git diff --check
rg -n 'CHG-20260717-005|15-point|RESULT READY|MatchResultV1' docs Assets/Volleyball/Match
git status --short
```

Expected: no whitespace errors; the new record, index, and scene acceptance terminology are present; no `TestResults/` files are tracked.

- [ ] **Step 4: Commit the delivery record**

```bash
git add docs/changes/2026-07-17-005-match-set-completion.md docs/changes/README.md docs/development.md
git commit -m "docs: record completed match set flow"
```

## Plan Self-Review

- Scope coverage: Tasks 1-2 cover pure scoring, rotation, all-six stats and V1 result output; Tasks 3-4 connect only Match presentation to the physical scene and verify completion; Task 5 records the Career-facing handoff without modifying Career.
- No-placeholder check: every code task names concrete files, test names, commands and expected outcomes.
- Type consistency: all aggregate APIs use `TeamSide` and `Volleyball.Shared.Contracts.PlayerId`; scene-only slot identities remain `Volleyball.Domain.Prototype.PlayerId` and are mapped in the director.
