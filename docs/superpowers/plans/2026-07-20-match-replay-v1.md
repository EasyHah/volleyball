# Match Replay V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture one completed formal 6v6 rally as validated `MatchReplayV1` JSON and display it in an interactive HTML replay.

**Architecture:** Unity-free Domain DTOs own the versioned replay data, validation and canonical checksum. Presentation observes existing director and ball boundaries without changing behavior, records 10 Hz state plus event frames, and writes a viewer beside the JSON.

**Tech Stack:** Unity 6000.0.43f1, C#, NUnit, `DataContractJsonSerializer`, SHA-256, standalone HTML/CSS/JavaScript.

---

## File Structure

- Create `Assets/Volleyball/Match/Runtime/Domain/Replay/MatchReplayV1.cs`: DTOs, validation and checksum.
- Create `Assets/Volleyball/Match/Runtime/Domain/Replay/MatchReplayJson.cs`: JSON serialization.
- Create `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`: observer and sampling adapter.
- Create `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs`: JSON and HTML artifact writer.
- Modify `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`: read-only event hooks and match-state accessors.
- Create `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV1Tests.cs`: contract tests.
- Create `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`: formal-rally capture test.
- Create `docs/changes/2026-07-20-001-match-replay-v1.md`; modify `docs/changes/README.md` and `docs/development.md`.

### Task 1: Versioned Replay Contract

**Files:**
- Create `Assets/Volleyball/Match/Runtime/Domain/Replay/MatchReplayV1.cs`
- Create `Assets/Volleyball/Match/Runtime/Domain/Replay/MatchReplayJson.cs`
- Create `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV1Tests.cs`

- [ ] **Step 1: Write failing tests for the contract**

Create a fixture with twelve distinct player records, one snapshot and one `RallyResolved` event. Test JSON round-trip, tampered checksum, non-monotonic event time and nonexistent event snapshot index.

```csharp
[Test]
public void Json_RoundTripsASealedReplay()
{
    var replay = ReplayFixture.CreateValid();
    var restored = MatchReplayJson.Deserialize(MatchReplayJson.Serialize(replay));
    Assert.That(restored.ContentChecksum, Is.EqualTo(replay.ContentChecksum));
}

[Test]
public void Validate_RejectsAnEventWhoseSnapshotDoesNotExist()
{
    var replay = ReplayFixture.CreateValid(eventSnapshotIndex: 2);
    Assert.Throws<MatchReplayValidationException>(() => replay.Validate());
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.MatchReplayV1Tests" \
  -testResults "$PWD/TestResults/MatchReplayV1-red.xml" \
  -logFile "$PWD/TestResults/MatchReplayV1-red.log"
```

Expected: compile failure because replay contract types do not exist.

- [ ] **Step 3: Implement the smallest valid contract**

Define `[DataContract]` classes for metadata, players, snapshots, ball/player state, event, decision and candidate score. `MatchReplayV1.FormatVersion` is `1`; `SampleIntervalSeconds` is `0.1f`. `Validate()` rejects unsupported versions, non-finite values, missing/duplicate player IDs, non-monotonic times, invalid snapshot indexes, unknown event players and checksum mismatch. `Seal()` computes SHA-256 over canonical UTF-8 payload without the checksum. `MatchReplayJson` uses `DataContractJsonSerializer` like `Assets/Volleyball/Shared/Runtime/ContractJson.cs`.

```csharp
public sealed class MatchReplayV1
{
    public const int FormatVersion = 1;
    public const float SampleIntervalSeconds = 0.1f;
    public void Validate();
    public void Seal();
}

public static class MatchReplayJson
{
    public static string Serialize(MatchReplayV1 replay);
    public static MatchReplayV1 Deserialize(string json);
}
```

- [ ] **Step 4: Verify green**

Run Step 2 command. Expected: XML reports every added replay contract test with `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Domain/Replay Assets/Volleyball/Match/Tests/EditMode/MatchReplayV1Tests.cs
git commit -m "feat: add match replay v1 contract"
```

### Task 2: Read-Only Replay Recording

**Files:**
- Modify `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- Create `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- Create `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`

- [ ] **Step 1: Write the failing recorder test**

Load `FormalIndoor6v6`, attach a recorder before serve, wait for its completion, then assert it contains 12 players, 10 Hz snapshots, a serve, a decision with six candidates and `RallyResolved`.

```csharp
[UnityTest]
public IEnumerator Recorder_CapturesOneFormalRally()
{
    yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
    var recorder = MatchReplayRecorder.Attach(director, ball, players);
    recorder.StartCapture();
    yield return new WaitUntil(() => recorder.IsComplete);
    var replay = recorder.Complete();
    Assert.That(replay.Players, Has.Count.EqualTo(12));
    Assert.That(replay.Events, Has.Some.Matches<MatchReplayEventV1>(e => e.Kind == "RallyResolved"));
}
```

- [ ] **Step 2: Verify red**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests" \
  -testResults "$PWD/TestResults/FormalReplay-red.xml" \
  -logFile "$PWD/TestResults/FormalReplay-red.log"
```

Expected: compile failure because recorder and replay events do not exist.

- [ ] **Step 3: Add read-only director boundaries**

Add immutable payloads and events after existing behavior accepts a decision, contact, crossing, ground event, serve start and rally resolution. Add score, server, rotation and possession read-only accessors. Keep `DecisionPlanned` unchanged. Never change planner result, ball response, scoring, scheduling or time scale.

```csharp
public event Action<ReplayDecisionEvent> ReplayDecisionPlanned;
public event Action<ReplayContactEvent> ReplayContactAccepted;
public event Action<ReplayRallyResolvedEvent> ReplayRallyResolved;
public int HomeScore => _set.HomeScore;
public int AwayScore => _set.AwayScore;
```

- [ ] **Step 4: Implement `MatchReplayRecorder`**

Subscribe to the new events. Force an initial snapshot, then in `Update()` sample whenever ball simulation time reaches the next 0.1-second boundary. Before each event, force a snapshot. Include ball position/velocity, twelve player transform position/yaw/action/movement target, score/server/rotation/possession. At a decision, copy predicted target, available seconds, weights, selected actor/action and all candidates. Mark `ConsecutiveTouch` when infeasible despite nonnegative reachability, otherwise `Unreachable` when negative. On resolution write final event/snapshot and complete.

- [ ] **Step 5: Verify green and commit**

Run Step 2 command. Expected: XML reports `total="1" passed="1" failed="0"`.

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs \
  Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs
git commit -m "feat: capture formal rally replay state"
```

### Task 3: HTML Replay Artifacts

**Files:**
- Create `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs`
- Modify `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`

- [ ] **Step 1: Write a failing artifact test**

After capture, write output to a unique ignored `TestResults/decision-replay/` child. Assert `replay.json` and `index.html` exist; assert HTML contains `MatchReplayV1`, `timeline`, `score-panel`, `event-marker` and `replay.json`.

```csharp
MatchReplayArtifactWriter.Write(outputDirectory, replay);
Assert.That(File.Exists(Path.Combine(outputDirectory, "replay.json")), Is.True);
Assert.That(File.ReadAllText(Path.Combine(outputDirectory, "index.html")), Does.Contain("score-panel"));
```

- [ ] **Step 2: Verify red**

Run Task 2's command. Expected: compile failure because `MatchReplayArtifactWriter` does not exist.

- [ ] **Step 3: Implement the viewer writer**

Write `replay.json` with `MatchReplayJson.Serialize`. Generate one HTML file with inline CSS/JavaScript that loads sibling JSON, rejects other format versions, renders a 9x18m top-down SVG court and 12 labels (`BLUE/ORANGE P1-P6 ROLE`), ball and facing vectors. Render score, server, rotation, phase and time. Implement play/pause, 0.5x/1x/2x, range timeline, prior/next event and event markers. Interpolate only between ordinary snapshots. At a decision event, pause and show all candidates with component scores and selected/unreachable/consecutive-touch status.

- [ ] **Step 4: Verify green and manually inspect**

Run Task 2's command. Expected: XML `failed="0"` and `TestResults/decision-replay/<run>/` contains JSON/HTML. Open HTML locally and compare one decision table to the JSON event; confirm 12 labels, event pause and score display.

- [ ] **Step 5: Commit**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs
git commit -m "feat: add interactive match replay viewer"
```

### Task 4: Document and Regress

**Files:**
- Create `docs/changes/2026-07-20-001-match-replay-v1.md`
- Modify `docs/changes/README.md`
- Modify `docs/development.md`

- [ ] **Step 1: Document the Match-only change**

Record V1 compatibility, ignored artifact location, 10 Hz/event sampling, HTML controls, no Shared/Career/Bootstrap change, and deferred full-set capture/future Unity consumer.

- [ ] **Step 2: Document execution**

Add formal replay PlayMode command and local HTML path to development docs. State `TestResults/` remains diagnostic and ignored.

- [ ] **Step 3: Run focused verification**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.MatchReplayV1Tests" \
  -testResults "$PWD/TestResults/MatchReplayV1-final.xml" \
  -logFile "$PWD/TestResults/MatchReplayV1-final.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests" \
  -testResults "$PWD/TestResults/FormalReplay-final.xml" \
  -logFile "$PWD/TestResults/FormalReplay-final.log"
rg -n 'test-run|total=|passed=|failed=|result=' "$PWD/TestResults/MatchReplayV1-final.xml" "$PWD/TestResults/FormalReplay-final.xml"
```

Expected: both XML files report `failed="0"`.

- [ ] **Step 4: Run full suite and commit docs**

Run full EditMode and PlayMode commands with separate XML outputs; inspect both for `failed="0"`. Then:

```bash
git add docs/changes/2026-07-20-001-match-replay-v1.md docs/changes/README.md docs/development.md
git commit -m "docs: record match replay v1"
```

## Plan Self-Review

- Contract/version/checksum: Task 1.
- 10 Hz state plus event snapshots, decision scores and 12-player formal rally: Task 2.
- HTML controls, labels, event pause and candidate panel: Task 3.
- Artifact usage, boundaries and regression verification: Task 4.
