# MenShen AI Decision Benchmark Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Editor-only, reproducible benchmark that sends the approved 20 volleyball decision cases through MenShen, validates strict JSON decisions, measures latency and token usage, and exports anonymous reports for human scoring.

**Architecture:** Pure decision contracts and rule validation live in `Volleyball.Match.AI`; Editor-only gateway, streaming, fixture, runner, and report code live in a new `Volleyball.Match.AI.Editor` assembly. The tool reads `MENSHEN_API_KEY` only from the process environment, calls the HTTPS `/v1/chat/completions` endpoint sequentially, and writes all raw and review artifacts under ignored `TestResults/MenShen/`. No model output reaches `ThreeVsThreeRallyDirector` in this phase; runtime integration is a separate plan gated on the benchmark thresholds.

**Tech Stack:** Unity `6000.0.43f1`, C# Unity Assembly Definitions, NUnit EditMode tests, `System.Net.Http`, `com.unity.nuget.newtonsoft-json` `3.2.1`, MenShen OpenAI-compatible streaming chat completions.

---

## Scope And Gate

This plan implements only the benchmark phase approved in
`docs/superpowers/specs/2026-07-17-llm-volleyball-decision-benchmark-design.md`.
It deliberately excludes:

- changes to `ThreeVsThreeRallyDirector`, `PhysicalRallyTacticPlanner`, player movement, physics, scoring, or scenes;
- direct API keys in serialized assets, `PlayerPrefs`, source code, logs, builds, or Git;
- automatic use of model output during a live match;
- the team-touch counter and playable block behavior, which require their own rule/runtime plan;
- production backend/proxy deployment.

After the benchmark is reviewed, write a separate runtime-integration design and plan only for models that meet the approved JSON, score, and latency gates. If no model meets the 500 ms touch-decision P95, runtime integration must remain round-level asynchronous prefetch with deterministic local fallback.

## File Structure

- Modify: `Packages/manifest.json` - add the pinned Newtonsoft JSON package used for strict parsing.
- Modify: `Packages/packages-lock.json` - commit Unity's resolved package lock.
- Create: `Assets/Volleyball/Match/Editor/AI/Volleyball.Match.AI.Editor.asmdef` - Editor-only gateway and benchmark assembly.
- Modify: `Assets/Volleyball/Match/Tests/EditMode/Volleyball.Match.EditModeTests.asmdef` - reference the Editor benchmark assembly.
- Create: `Assets/Volleyball/Match/Runtime/AI/LlmDecisionProtocol.cs` - Unity-free decision enums, immutable values, equality, adjacent-touch validation, and third-touch action validation.
- Create: `Assets/Volleyball/Match/Editor/AI/DecisionJsonCodec.cs` - strict JSON parsing with exact keys, duplicate-key rejection, and snake-case enum mapping.
- Create: `Assets/Volleyball/Match/Editor/AI/BenchmarkCaseCatalog.cs` - fixture DTOs, catalog invariants, and prompt construction that excludes answer keys.
- Create: `Assets/Volleyball/Match/Editor/AI/Fixtures/LlmDecisionBenchmarkV1.json` - machine-readable R01-R10 and T01-T10 cases transcribed from the approved specification.
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenModelProfile.cs` - per-model request parameters without review aliases.
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenRequestBuilder.cs` - provider-compatible request JSON without unsupported parameters.
- Create: `Assets/Volleyball/Match/Editor/AI/SseStreamAccumulator.cs` - incremental SSE parsing and first-content timing.
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenChatClient.cs` - HTTPS streaming transport, deadline cancellation, error classification, and metrics.
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkRunner.cs` - deterministic shuffled execution of 3 models x 20 cases x 3 repetitions.
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkReportWriter.cs` - raw JSON, anonymous Markdown/CSV, and alias-map export under `TestResults/`.
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkCommand.cs` - menu and batch-mode entrypoints with environment-only credentials.
- Create: `Assets/Volleyball/Match/Tests/EditMode/LlmDecisionProtocolTests.cs` - pure protocol and rule tests.
- Create: `Assets/Volleyball/Match/Tests/EditMode/DecisionJsonCodecTests.cs` - strict JSON parser tests.
- Create: `Assets/Volleyball/Match/Tests/EditMode/BenchmarkCaseCatalogTests.cs` - 20-case completeness, answer-key isolation, and block-rule tests.
- Create: `Assets/Volleyball/Match/Tests/EditMode/MenShenRequestBuilderTests.cs` - model-specific parameter tests.
- Create: `Assets/Volleyball/Match/Tests/EditMode/SseStreamAccumulatorTests.cs` - fragmented SSE, usage, error, and timing tests.
- Create: `Assets/Volleyball/Match/Tests/EditMode/MenShenBenchmarkRunnerTests.cs` - deterministic order, timeout capture, no retry scoring, and anonymity tests.
- Create: `docs/changes/2026-07-18-006-menshen-decision-benchmark.md` - Match-internal change and verification record.
- Modify: `docs/changes/README.md` - add CHG-006 to the index.
- Modify: `docs/development.md` - document local benchmark prerequisites and commands without secret values.

Unity must generate and commit `.meta` files for every new folder and asset. Do not hand-author GUIDs.

### Task 1: Add The Editor-Only Benchmark Assembly

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `Packages/packages-lock.json`
- Create: `Assets/Volleyball/Match/Editor/AI/Volleyball.Match.AI.Editor.asmdef`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/Volleyball.Match.EditModeTests.asmdef`

- [ ] **Step 1: Add the strict JSON package**

Add this dependency to `Packages/manifest.json` and preserve the existing dependencies:

```json
"com.unity.nuget.newtonsoft-json": "3.2.1"
```

Open the project once with Unity `6000.0.43f1` so `Packages/packages-lock.json` resolves the package. Do not manually invent the lock entry.

- [ ] **Step 2: Create the Editor assembly definition**

Create `Volleyball.Match.AI.Editor.asmdef` with exactly:

```json
{
  "name": "Volleyball.Match.AI.Editor",
  "rootNamespace": "Volleyball.Editor.AI",
  "references": [
    "Volleyball.Match.Domain",
    "Volleyball.Match.AI",
    "Unity.Newtonsoft.Json"
  ],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

Add `"Volleyball.Match.AI.Editor"` and `"Unity.Newtonsoft.Json"` to the EditMode test assembly references. Do not add either reference to runtime Presentation or PlayMode assemblies.

- [ ] **Step 3: Run an Editor compilation smoke check**

Run:

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -quit -projectPath "$PWD" \
  -logFile "$PWD/TestResults/MenShen-assembly-import.log"
```

Expected: Unity exits 0, resolves Newtonsoft `3.2.1`, and imports the empty Editor assembly without compilation errors.

- [ ] **Step 4: Commit package and assembly boundaries**

```bash
git add Packages/manifest.json Packages/packages-lock.json \
  Assets/Volleyball/Match/Editor Assets/Volleyball/Match/Tests/EditMode/Volleyball.Match.EditModeTests.asmdef
git commit -m "build: add menshen benchmark assembly"
```

### Task 2: Add Pure Decision Contracts And Rule Validation

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/AI/LlmDecisionProtocol.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/LlmDecisionProtocolTests.cs`

- [ ] **Step 1: Write failing protocol tests**

Create tests covering the approved fields and adjacent-touch rule:

```csharp
[Test]
public void RoundDecision_ValidActorsAndRoutes_PreservesValues()
{
    var decision = new RoundDecisionV1(
        PlayerRole.Defender,
        PlayerRole.Setter,
        SetRoute.RightPin,
        PlayerRole.Attacker,
        SpikeRoute.Line);

    Assert.That(decision.Receiver, Is.EqualTo(PlayerRole.Defender));
    Assert.That(decision.SecondActor, Is.EqualTo(PlayerRole.Setter));
    Assert.That(decision.ThirdActor, Is.EqualTo(PlayerRole.Attacker));
    Assert.That(decision.SetRoute, Is.EqualTo(SetRoute.RightPin));
    Assert.That(decision.AttackRoute, Is.EqualTo(SpikeRoute.Line));
}

[Test]
public void RoundDecision_AdjacentActorRepeats_Throws()
{
    Assert.Throws<ArgumentException>(() => new RoundDecisionV1(
        PlayerRole.Setter,
        PlayerRole.Setter,
        SetRoute.LeftPin,
        PlayerRole.Attacker,
        SpikeRoute.CrossCourt));

    Assert.Throws<ArgumentException>(() => new RoundDecisionV1(
        PlayerRole.Defender,
        PlayerRole.Setter,
        SetRoute.LeftPin,
        PlayerRole.Setter,
        SpikeRoute.CrossCourt));
}

[Test]
public void RoundDecision_FirstActorMayReturnForThirdContact()
{
    Assert.DoesNotThrow(() => new RoundDecisionV1(
        PlayerRole.Attacker,
        PlayerRole.Setter,
        SetRoute.BackSet,
        PlayerRole.Attacker,
        SpikeRoute.RollShot));
}

[Test]
public void TouchDecision_ThirdCountedTouch_MustGoOverNet()
{
    var set = new TouchDecisionV1(
        PlayerRole.Setter,
        TouchDecisionAction.Set,
        TargetZone.RightFront,
        DecisionTempo.High,
        DecisionRisk.Safe);

    Assert.That(TouchDecisionRules.Validate(set, countedTeamTouches: 2).IsValid, Is.False);

    var attack = new TouchDecisionV1(
        PlayerRole.Attacker,
        TouchDecisionAction.Attack,
        TargetZone.MiddleBack,
        DecisionTempo.Quick,
        DecisionRisk.Balanced);

    Assert.That(TouchDecisionRules.Validate(attack, countedTeamTouches: 2).IsValid, Is.True);
}

```

- [ ] **Step 2: Run the focused suite and verify it fails**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.LlmDecisionProtocolTests" \
  -testResults "$PWD/TestResults/LlmDecisionProtocol-red.xml" \
  -logFile "$PWD/TestResults/LlmDecisionProtocol-red.log"
```

Expected: compilation fails because the decision protocol types do not exist.

- [ ] **Step 3: Implement the protocol values**

Implement these exact public enums and immutable types in `Volleyball.AI`:

```csharp
public enum TouchDecisionAction { Receive, Set, Attack, FreeBall, EmergencySave }
public enum TargetZone { LeftFront, MiddleFront, RightFront, LeftBack, MiddleBack, RightBack }
public enum DecisionTempo { Quick, Normal, High }
public enum DecisionRisk { Safe, Balanced, Aggressive }

public readonly struct RoundDecisionV1
{
    public RoundDecisionV1(
        PlayerRole receiver,
        PlayerRole secondActor,
        SetRoute setRoute,
        PlayerRole thirdActor,
        SpikeRoute attackRoute)
    {
        ValidateRole(receiver, nameof(receiver));
        ValidateRole(secondActor, nameof(secondActor));
        ValidateRole(thirdActor, nameof(thirdActor));
        if (receiver == secondActor || secondActor == thirdActor)
            throw new ArgumentException("Adjacent contacts require different actors.");
        if (!Enum.IsDefined(typeof(SetRoute), setRoute))
            throw new ArgumentOutOfRangeException(nameof(setRoute));
        if (!Enum.IsDefined(typeof(SpikeRoute), attackRoute))
            throw new ArgumentOutOfRangeException(nameof(attackRoute));
        Receiver = receiver;
        SecondActor = secondActor;
        SetRoute = setRoute;
        ThirdActor = thirdActor;
        AttackRoute = attackRoute;
    }

    public PlayerRole Receiver { get; }
    public PlayerRole SecondActor { get; }
    public SetRoute SetRoute { get; }
    public PlayerRole ThirdActor { get; }
    public SpikeRoute AttackRoute { get; }

    private static void ValidateRole(PlayerRole role, string parameterName)
    {
        if (!Enum.IsDefined(typeof(PlayerRole), role))
            throw new ArgumentOutOfRangeException(parameterName);
    }
}

public readonly struct TouchDecisionV1
{
    public TouchDecisionV1(
        PlayerRole nextActor,
        TouchDecisionAction action,
        TargetZone targetZone,
        DecisionTempo tempo,
        DecisionRisk risk)
    {
        if (!Enum.IsDefined(typeof(PlayerRole), nextActor))
            throw new ArgumentOutOfRangeException(nameof(nextActor));
        if (!Enum.IsDefined(typeof(TouchDecisionAction), action))
            throw new ArgumentOutOfRangeException(nameof(action));
        if (!Enum.IsDefined(typeof(TargetZone), targetZone))
            throw new ArgumentOutOfRangeException(nameof(targetZone));
        if (!Enum.IsDefined(typeof(DecisionTempo), tempo))
            throw new ArgumentOutOfRangeException(nameof(tempo));
        if (!Enum.IsDefined(typeof(DecisionRisk), risk))
            throw new ArgumentOutOfRangeException(nameof(risk));
        NextActor = nextActor;
        Action = action;
        TargetZone = targetZone;
        Tempo = tempo;
        Risk = risk;
    }

    public PlayerRole NextActor { get; }
    public TouchDecisionAction Action { get; }
    public TargetZone TargetZone { get; }
    public DecisionTempo Tempo { get; }
    public DecisionRisk Risk { get; }
}
```

Add `DecisionValidationResult` and `TouchDecisionRules.Validate`. The validator
accepts the already-adjudicated `countedTeamTouches` from 0 through 2; it must
allow every action before the third counted touch and require `Attack` or
`FreeBall` when `countedTeamTouches == 2`. The benchmark fixture, not this phase,
sets T07 to zero after a block. Do not introduce live rally touch state here.

Implement `IEquatable<RoundDecisionV1>` and `IEquatable<TouchDecisionV1>`, typed
`Equals`, `Equals(object)`, and consistent `GetHashCode` so preferred-answer
matching does not rely on reflection-based value equality.

- [ ] **Step 4: Run the focused suite and verify it passes**

Repeat Step 2 with `LlmDecisionProtocol-green.xml` and `LlmDecisionProtocol-green.log`.

Expected: all protocol tests pass.

- [ ] **Step 5: Commit the protocol**

```bash
git add Assets/Volleyball/Match/Runtime/AI/LlmDecisionProtocol.cs \
  Assets/Volleyball/Match/Tests/EditMode/LlmDecisionProtocolTests.cs
git commit -m "feat: add llm decision protocol"
```

### Task 3: Parse Decisions With An Exact JSON Schema

**Files:**
- Create: `Assets/Volleyball/Match/Editor/AI/DecisionJsonCodec.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/DecisionJsonCodecTests.cs`

- [ ] **Step 1: Write failing strict-parser tests**

```csharp
[Test]
public void ParseRound_ExactApprovedObject_ReturnsDecision()
{
    var json = "{\"receiver\":\"defender\",\"second_actor\":\"setter\"," +
               "\"set_route\":\"right_pin\",\"third_actor\":\"attacker\"," +
               "\"attack_route\":\"line\"}";

    var decision = DecisionJsonCodec.ParseRound(json);

    Assert.That(decision.Receiver, Is.EqualTo(PlayerRole.Defender));
    Assert.That(decision.SetRoute, Is.EqualTo(SetRoute.RightPin));
    Assert.That(decision.AttackRoute, Is.EqualTo(SpikeRoute.Line));
}

[TestCase("{\"receiver\":\"defender\"}")]
[TestCase("{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"right_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"line\",\"reason\":\"open\"}")]
[TestCase("```json\\n{\"receiver\":\"defender\"}\\n```")]
public void ParseRound_MissingExtraOrWrappedContent_Throws(string json)
{
    Assert.Throws<DecisionFormatException>(() => DecisionJsonCodec.ParseRound(json));
}

[Test]
public void ParseTouch_DuplicateProperty_Throws()
{
    var json = "{\"next_actor\":\"setter\",\"next_actor\":\"attacker\"," +
               "\"action\":\"set\",\"target_zone\":\"right_front\"," +
               "\"tempo\":\"high\",\"risk\":\"safe\"}";

    Assert.Throws<DecisionFormatException>(() => DecisionJsonCodec.ParseTouch(json));
}

[Test]
public void TryStripSingleMarkdownFence_RepairsOnlyOneJsonFence()
{
    var wrapped = "```json\n{\"next_actor\":\"setter\",\"action\":\"set\"," +
                  "\"target_zone\":\"right_front\",\"tempo\":\"high\",\"risk\":\"safe\"}\n```";

    Assert.That(DecisionJsonRepair.TryStripSingleMarkdownFence(wrapped, out var repaired), Is.True);
    Assert.DoesNotThrow(() => DecisionJsonCodec.ParseTouch(repaired));
    Assert.That(DecisionJsonRepair.TryStripSingleMarkdownFence("prefix " + wrapped, out _), Is.False);
}
```

- [ ] **Step 2: Run the parser suite and verify it fails**

Use the Task 2 Unity command with test filter
`Volleyball.EditModeTests.DecisionJsonCodecTests` and red result/log names.

Expected: compilation fails because `DecisionJsonCodec` does not exist.

- [ ] **Step 3: Implement strict parsing**

Use `JsonTextReader` and `JObject.Load` with:

```csharp
new JsonLoadSettings
{
    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
    CommentHandling = CommentHandling.Ignore,
    LineInfoHandling = LineInfoHandling.Load
}
```

Before converting values, compare the property-name set exactly against:

```csharp
private static readonly string[] RoundKeys =
{
    "attack_route", "receiver", "second_actor", "set_route", "third_actor"
};

private static readonly string[] TouchKeys =
{
    "action", "next_actor", "risk", "target_zone", "tempo"
};
```

Map only the approved lower snake-case values. Do not use permissive
`Enum.Parse`; explicit dictionaries must reject casing changes, numeric values,
nulls, arrays, Markdown fences, leading explanations, and trailing content.
Construct `RoundDecisionV1` or `TouchDecisionV1` so protocol validation remains
the final authority.

Implement `DecisionJsonRepair.TryStripSingleMarkdownFence` as the only repair:
it accepts one outer ` ```json ` or ` ``` ` fence with no prefix/suffix, returns
the unchanged interior, and rejects every other transformation. The runner must
record whether this repair was used.

- [ ] **Step 4: Run the parser suite and protocol suite**

Expected: both suites pass and malformed decisions consistently throw
`DecisionFormatException` without leaking the full model response into the exception message.

- [ ] **Step 5: Commit the strict codec**

```bash
git add Assets/Volleyball/Match/Editor/AI/DecisionJsonCodec.cs \
  Assets/Volleyball/Match/Tests/EditMode/DecisionJsonCodecTests.cs
git commit -m "feat: validate llm decision json"
```

### Task 4: Encode And Validate The Approved 20 Cases

**Files:**
- Create: `Assets/Volleyball/Match/Editor/AI/Fixtures/LlmDecisionBenchmarkV1.json`
- Create: `Assets/Volleyball/Match/Editor/AI/BenchmarkCaseCatalog.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/BenchmarkCaseCatalogTests.cs`

- [ ] **Step 1: Write failing catalog tests**

```csharp
[Test]
public void Load_ApprovedFixture_ContainsExactlyR01ThroughT10()
{
    var catalog = BenchmarkCaseCatalog.Load(ApprovedFixturePath);

    Assert.That(catalog.Cases, Has.Count.EqualTo(20));
    CollectionAssert.AreEqual(
        Enumerable.Range(1, 10).Select(i => $"R{i:00}")
            .Concat(Enumerable.Range(1, 10).Select(i => $"T{i:00}")),
        catalog.Cases.Select(item => item.Id));
}

[Test]
public void BuildPrompt_NeverContainsAnswerKeyOrScoringText()
{
    var item = BenchmarkCaseCatalog.Load(ApprovedFixturePath).Cases.Single(c => c.Id == "R03");

    var prompt = BenchmarkPromptBuilder.Build(item);

    Assert.That(prompt, Does.Contain("对方后排两人整体向蓝队进攻视角的左侧移动"));
    Assert.That(prompt, Does.Not.Contain(item.PreferredJson));
    Assert.That(prompt, Does.Not.Contain(item.Rationale));
    Assert.That(prompt, Does.Not.Contain("首选答案"));
}

[Test]
public void Load_T07_DeclaresBlockAsZeroCountedTouches()
{
    var item = BenchmarkCaseCatalog.Load(ApprovedFixturePath).Cases.Single(c => c.Id == "T07");

    Assert.That(item.CountedTeamTouches, Is.Zero);
    Assert.That(item.State, Does.Contain("拦网触球不计入本队三次触球"));
    Assert.That(DecisionJsonCodec.ParseTouch(item.PreferredJson).NextActor,
        Is.EqualTo(PlayerRole.Attacker));
}
```

- [ ] **Step 2: Run the catalog suite and verify it fails**

Use the Task 2 command with filter
`Volleyball.EditModeTests.BenchmarkCaseCatalogTests`.

Expected: compilation fails because catalog types and fixture do not exist.

- [ ] **Step 3: Create the fixture schema and transcribe every approved case**

Use this top-level shape:

```json
{
  "version": 1,
  "cases": [
    {
      "id": "R01",
      "kind": "round",
      "deadline_ms": 2000,
      "counted_team_touches": 0,
      "state": "比分 0:0；蓝队接发；中速发球落向 middle_back；Defender 位于 middle_back，一传 0.95、反应 0.92；Setter 与 Attacker 常规站位且体能正常；对方拦防居中。",
      "preferred_json": "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"middle_quick\",\"third_actor\":\"attacker\",\"attack_route\":\"deep_seam\"}",
      "acceptable_guidance": [
        "Defender 接球、Setter 二传、Attacker 进攻；边翼路线与 line、cross_court 或 deep_seam 合理配对。"
      ],
      "forbidden_guidance": [
        "Setter 或 Attacker 抢接 Defender 明确能稳定处理的深区中路球。"
      ],
      "rationale": "常规局面保持完整三次触球分工，同时利用均衡防守的中后结合部。"
    },
    {
      "id": "T07",
      "kind": "touch",
      "deadline_ms": 500,
      "counted_team_touches": 0,
      "state": "比分 11:11；蓝队刚完成拦网，拦网触球不计入本队三次触球；球反弹到 left_front，高 1.3 米、距网 0.4 米；完成拦网的 Attacker 刚落地且距球 0.5 米；Setter 距球 0.9 米；Defender 距球 2.4 米。",
      "preferred_json": "{\"next_actor\":\"attacker\",\"action\":\"emergency_save\",\"target_zone\":\"middle_front\",\"tempo\":\"high\",\"risk\":\"safe\"}",
      "acceptable_guidance": [
        "Setter 高弧 receive 到 middle_front。",
        "Setter 以本队第一次计数触球直接 set 到 right_front。"
      ],
      "forbidden_guidance": [
        "让 2.4 米外的 Defender 抢球。",
        "把拦网计为第一次触球。",
        "无紧急原因直接 free_ball。"
      ],
      "rationale": "拦网人可以立即再次触球，且本队仍保留完整三次触球。"
    }
  ]
}
```

The final file must contain all 20 cases in exact ID order. Transcribe R01-R10
and T01-T10 from the approved spec; the two objects above define the exact field
shape, escaping, deadlines, and block rule. Do not infer new cases or shorten
state facts that determine the answer.
Treat
`docs/superpowers/specs/2026-07-17-llm-volleyball-decision-benchmark-design.md`
as the authoritative content source: every `preferred_json` value must be copied
character-for-character from the matching case's **首选答案** block after minified
JSON normalization, and no `state`, guidance, or rationale text may be invented.
Add catalog assertions for exact ID order, exact kind counts, exact deadlines,
and exact preferred JSON for all 20 IDs so fixture transcription mistakes fail in
EditMode before any gateway call is made.

- [ ] **Step 4: Implement catalog invariants and prompt construction**

`BenchmarkCaseCatalog.Load` must reject any fixture unless:

- version equals 1;
- IDs are exactly R01-R10 followed by T01-T10 with no duplicates;
- round deadlines equal 2000 and touch deadlines equal 500;
- every preferred JSON parses with the codec for its kind;
- `counted_team_touches` is in 0..2;
- state, guidance, and rationale are non-empty;
- T07 uses zero counted touches and mentions the block exemption.

`BenchmarkPromptBuilder.Build` must combine only fixed rule/schema text and the
case `state`. It must never include `preferred_json`, acceptable/forbidden
guidance, rationale, model aliases, or score fields.

- [ ] **Step 5: Run the catalog, codec, and protocol suites**

Expected: all three suites pass with 20 valid preferred decisions and no answer-key leakage.

- [ ] **Step 6: Commit the approved catalog**

```bash
git add Assets/Volleyball/Match/Editor/AI/Fixtures \
  Assets/Volleyball/Match/Editor/AI/BenchmarkCaseCatalog.cs \
  Assets/Volleyball/Match/Tests/EditMode/BenchmarkCaseCatalogTests.cs
git commit -m "test: encode llm decision benchmark cases"
```

### Task 5: Build Model-Specific MenShen Requests

**Files:**
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenModelProfile.cs`
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenRequestBuilder.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/MenShenRequestBuilderTests.cs`

- [ ] **Step 1: Write failing request-shape tests**

```csharp
[Test]
public void Build_DoubaoMini_DisablesThinkingAndUsesMaxTokens()
{
    var json = JObject.Parse(MenShenRequestBuilder.Build(
        MenShenModelProfile.DoubaoMini, "system", "case"));

    Assert.That((string)json["model"], Is.EqualTo("doubao-seed-2.0-mini"));
    Assert.That((string)json["thinking"]?["type"], Is.EqualTo("disabled"));
    Assert.That((int)json["max_tokens"], Is.EqualTo(128));
    Assert.That(json["max_completion_tokens"], Is.Null);
    Assert.That((bool)json["stream"], Is.True);
    Assert.That((bool)json["stream_options"]?["include_usage"], Is.True);
}

[Test]
public void Build_QwenPlus_DisablesThinkingWithoutDoubaoShape()
{
    var json = JObject.Parse(MenShenRequestBuilder.Build(
        MenShenModelProfile.QwenPlus, "system", "case"));

    Assert.That((bool)json["enable_thinking"], Is.False);
    Assert.That(json["thinking"], Is.Null);
}

[Test]
public void Build_Gpt5Chat_UsesCompletionLimitAndOmitsTemperature()
{
    var json = JObject.Parse(MenShenRequestBuilder.Build(
        MenShenModelProfile.Gpt5Chat, "system", "case"));

    Assert.That((int)json["max_completion_tokens"], Is.EqualTo(128));
    Assert.That(json["max_tokens"], Is.Null);
    Assert.That(json["temperature"], Is.Null);
    Assert.That(json["thinking"], Is.Null);
    Assert.That(json["enable_thinking"], Is.Null);
}
```

- [ ] **Step 2: Run the request-builder suite and verify it fails**

Use the Task 2 command with filter
`Volleyball.EditModeTests.MenShenRequestBuilderTests`.

- [ ] **Step 3: Implement fixed profiles and structured request generation**

Define only these profiles:

```csharp
public static readonly MenShenModelProfile DoubaoMini =
    new MenShenModelProfile("doubao-seed-2.0-mini", 128, ModelParameterStyle.Doubao);
public static readonly MenShenModelProfile QwenPlus =
    new MenShenModelProfile("qwen3.7-plus", 128, ModelParameterStyle.Qwen);
public static readonly MenShenModelProfile Gpt5Chat =
    new MenShenModelProfile("gpt-5-chat", 128, ModelParameterStyle.Gpt5);
```

Build requests with `JObject` and `JArray`, not string concatenation. Every
request must include `model`, `stream: true`, exactly two messages, and the
profile-specific token/thinking fields proven by tests. Include
`"stream_options":{"include_usage":true}` for final token metrics. Do not send
`temperature`, answer keys, API keys, or scoring guidance in the body.

- [ ] **Step 4: Run the request-builder suite and verify it passes**

Expected: all model shapes pass and unsupported fields are absent rather than serialized with defaults.

- [ ] **Step 5: Commit model request profiles**

```bash
git add Assets/Volleyball/Match/Editor/AI/MenShenModelProfile.cs \
  Assets/Volleyball/Match/Editor/AI/MenShenRequestBuilder.cs \
  Assets/Volleyball/Match/Tests/EditMode/MenShenRequestBuilderTests.cs
git commit -m "feat: build menshen model requests"
```

### Task 6: Stream Responses With Deadlines And Metrics

**Files:**
- Create: `Assets/Volleyball/Match/Editor/AI/SseStreamAccumulator.cs`
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenChatClient.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/SseStreamAccumulatorTests.cs`

- [ ] **Step 1: Write failing SSE and timing tests**

```csharp
[Test]
public void Accept_FragmentedContent_RecordsFirstContentAndAssemblesJson()
{
    var accumulator = new SseStreamAccumulator();

    accumulator.Accept("data: {\"choices\":[{\"delta\":{\"content\":\"{\\\"receiver\\\":\"}}]}", 120);
    accumulator.Accept("data: {\"choices\":[{\"delta\":{\"content\":\"\\\"defender\\\"}\"}}]}", 145);
    accumulator.Accept("data: [DONE]", 150);

    Assert.That(accumulator.Content, Is.EqualTo("{\"receiver\":\"defender\"}"));
    Assert.That(accumulator.FirstContentMilliseconds, Is.EqualTo(120));
    Assert.That(accumulator.IsComplete, Is.True);
}

[Test]
public void Accept_ReasoningOnly_DoesNotCountAsVisibleFirstContent()
{
    var accumulator = new SseStreamAccumulator();

    accumulator.Accept("data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"think\"}}]}", 80);
    accumulator.Accept("data: {\"choices\":[{\"delta\":{\"content\":\"{}\"}}]}", 210);

    Assert.That(accumulator.FirstContentMilliseconds, Is.EqualTo(210));
}

[Test]
public void Accept_FinalUsage_PreservesTokenCounts()
{
    var accumulator = new SseStreamAccumulator();
    accumulator.Accept("data: {\"choices\":[],\"usage\":{\"prompt_tokens\":90,\"completion_tokens\":20,\"total_tokens\":110}}", 300);

    Assert.That(accumulator.PromptTokens, Is.EqualTo(90));
    Assert.That(accumulator.CompletionTokens, Is.EqualTo(20));
    Assert.That(accumulator.TotalTokens, Is.EqualTo(110));
}
```

- [ ] **Step 2: Run the SSE suite and verify it fails**

Use the Task 2 command with filter
`Volleyball.EditModeTests.SseStreamAccumulatorTests`.

- [ ] **Step 3: Implement incremental SSE parsing**

`SseStreamAccumulator.Accept(string line, long elapsedMilliseconds)` must:

- ignore empty lines and SSE comments;
- require the `data: ` prefix for payload lines;
- treat `[DONE]` as completion;
- parse each JSON payload with Newtonsoft;
- append `choices[0].delta.content` exactly as delivered;
- store reasoning separately but never use it as decision content;
- set first-content time once, on the first non-empty content delta;
- capture final usage when present;
- preserve a sanitized gateway error category without embedding API keys or request headers.

- [ ] **Step 4: Implement the HTTPS streaming client**

Use one reusable `HttpClient` and this endpoint constant:

```csharp
public static readonly Uri DefaultEndpoint =
    new Uri("https://menshen-code.test.xdf.cn/v1/chat/completions");
```

Expose this transport contract from `MenShenChatClient.cs` so the runner can be
tested without reaching the real gateway:

```csharp
public interface IMenShenChatClient
{
    Task<MenShenChatResult> CompleteAsync(
        MenShenModelProfile profile,
        string systemPrompt,
        string casePrompt,
        string apiKey,
        TimeSpan deadline,
        CancellationToken cancellationToken);
}

public enum MenShenChatStatus
{
    Success,
    Timeout,
    RateLimited,
    Unauthorized,
    GatewayError,
    MalformedStream
}

public readonly struct MenShenChatResult
{
    public MenShenChatResult(
        MenShenChatStatus status,
        string content,
        int httpStatusCode,
        long firstContentMilliseconds,
        long totalMilliseconds,
        int promptTokens,
        int completionTokens,
        int totalTokens,
        int reasoningCharacterCount,
        string retryAfter,
        string errorCategory)
    {
        Status = status;
        Content = content ?? string.Empty;
        HttpStatusCode = httpStatusCode;
        FirstContentMilliseconds = firstContentMilliseconds;
        TotalMilliseconds = totalMilliseconds;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens;
        ReasoningCharacterCount = reasoningCharacterCount;
        RetryAfter = retryAfter ?? string.Empty;
        ErrorCategory = errorCategory ?? string.Empty;
    }

    public MenShenChatStatus Status { get; }
    public string Content { get; }
    public int HttpStatusCode { get; }
    public long FirstContentMilliseconds { get; }
    public long TotalMilliseconds { get; }
    public int PromptTokens { get; }
    public int CompletionTokens { get; }
    public int TotalTokens { get; }
    public int ReasoningCharacterCount { get; }
    public string RetryAfter { get; }
    public string ErrorCategory { get; }
}

```

Implement `public sealed class MenShenChatClient : IMenShenChatClient`. Use
`HttpCompletionOption.ResponseHeadersRead`, register deadline cancellation, read
the SSE stream line by line, and feed elapsed milliseconds to the accumulator.
Return one of `Success`, `Timeout`, `RateLimited`, `Unauthorized`,
`GatewayError`, or `MalformedStream`. Record HTTP status, first-content latency,
total latency, content, reasoning character count, token counts, and retry-after
header. Do not retry a scored request; the runner will pace attempts to avoid QPS
limits and preserve failure evidence.

- [ ] **Step 5: Run SSE tests and an injected cancellation test**

Add a fake delayed `HttpMessageHandler` test that exceeds a 50 ms deadline and
asserts `MenShenChatStatus.Timeout` with no content. No test may access the real gateway.

Expected: SSE, usage, malformed-event, and timeout tests pass offline.

- [ ] **Step 6: Commit streaming transport**

```bash
git add Assets/Volleyball/Match/Editor/AI/SseStreamAccumulator.cs \
  Assets/Volleyball/Match/Editor/AI/MenShenChatClient.cs \
  Assets/Volleyball/Match/Tests/EditMode/SseStreamAccumulatorTests.cs
git commit -m "feat: stream menshen benchmark responses"
```

### Task 7: Run Deterministic Anonymous Benchmarks

**Files:**
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkRunner.cs`
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkReportWriter.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/MenShenBenchmarkRunnerTests.cs`

- [ ] **Step 1: Write failing runner tests with a fake client**

```csharp
private const string ApprovedFixturePath =
    "Assets/Volleyball/Match/Editor/AI/Fixtures/LlmDecisionBenchmarkV1.json";

private static BenchmarkCaseCatalog Catalog => BenchmarkCaseCatalog.Load(ApprovedFixturePath);

private static MenShenBenchmarkRunner CreateRunner(IMenShenChatClient client)
{
    return new MenShenBenchmarkRunner(
        client,
        new[]
        {
            MenShenModelProfile.DoubaoMini,
            MenShenModelProfile.QwenPlus,
            MenShenModelProfile.Gpt5Chat
        },
        apiKey: "sentinel-fake-key",
        pacingInterval: TimeSpan.Zero);
}

private sealed class SuccessfulFakeClient : IMenShenChatClient
{
    public Task<MenShenChatResult> CompleteAsync(
        MenShenModelProfile profile,
        string systemPrompt,
        string casePrompt,
        string apiKey,
        TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        var content = deadline <= TimeSpan.FromMilliseconds(500)
            ? "{\"next_actor\":\"setter\",\"action\":\"set\",\"target_zone\":\"right_front\",\"tempo\":\"high\",\"risk\":\"safe\"}"
            : "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"right_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"line\"}";

        return Task.FromResult(new MenShenChatResult(
            MenShenChatStatus.Success,
            content,
            httpStatusCode: 200,
            firstContentMilliseconds: 25,
            totalMilliseconds: 50,
            promptTokens: 90,
            completionTokens: 20,
            totalTokens: 110,
            reasoningCharacterCount: 0,
            retryAfter: string.Empty,
            errorCategory: string.Empty));
    }
}

private sealed class FirstCallTimeoutFakeClient : IMenShenChatClient
{
    private int calls;

    public Task<MenShenChatResult> CompleteAsync(
        MenShenModelProfile profile,
        string systemPrompt,
        string casePrompt,
        string apiKey,
        TimeSpan deadline,
        CancellationToken cancellationToken)
    {
        calls++;
        if (calls == 1)
        {
            return Task.FromResult(new MenShenChatResult(
                MenShenChatStatus.Timeout,
                string.Empty,
                httpStatusCode: 0,
                firstContentMilliseconds: -1,
                totalMilliseconds: (long)deadline.TotalMilliseconds,
                promptTokens: 0,
                completionTokens: 0,
                totalTokens: 0,
                reasoningCharacterCount: 0,
                retryAfter: string.Empty,
                errorCategory: "deadline"));
        }

        return new SuccessfulFakeClient().CompleteAsync(
            profile,
            systemPrompt,
            casePrompt,
            apiKey,
            deadline,
            cancellationToken);
    }
}

[Test]
public async Task RunAsync_ThreeProfilesTwentyCasesThreeRepetitions_Produces180Attempts()
{
    var runner = CreateRunner(new SuccessfulFakeClient());

    var result = await runner.RunAsync(Catalog, repetitions: 3, seed: 7351, CancellationToken.None);

    Assert.That(result.Attempts, Has.Count.EqualTo(180));
    Assert.That(result.Attempts.Select(a => a.CaseId).Distinct(), Has.Count.EqualTo(20));
    Assert.That(result.Attempts.Select(a => a.ModelId).Distinct(), Has.Count.EqualTo(3));
}

[Test]
public async Task RunAsync_SameSeed_ReplaysAttemptOrderExactly()
{
    var first = await CreateRunner(new SuccessfulFakeClient())
        .RunAsync(Catalog, 3, 7351, CancellationToken.None);
    var second = await CreateRunner(new SuccessfulFakeClient())
        .RunAsync(Catalog, 3, 7351, CancellationToken.None);

    CollectionAssert.AreEqual(
        first.Attempts.Select(a => (a.ModelId, a.CaseId, a.Repetition)),
        second.Attempts.Select(a => (a.ModelId, a.CaseId, a.Repetition)));
}

[Test]
public async Task RunAsync_Timeout_IsRecordedAndNextAttemptContinues()
{
    var result = await CreateRunner(new FirstCallTimeoutFakeClient())
        .RunAsync(Catalog, 1, 7351, CancellationToken.None);

    Assert.That(result.Attempts[0].Status, Is.EqualTo(MenShenChatStatus.Timeout));
    Assert.That(result.Attempts, Has.Count.EqualTo(60));
}
```

- [ ] **Step 2: Run the runner suite and verify it fails**

Use the Task 2 command with filter
`Volleyball.EditModeTests.MenShenBenchmarkRunnerTests`.

- [ ] **Step 3: Implement deterministic sequential execution**

Use this constructor and keep all mutable benchmark state local to `RunAsync`:

```csharp
public MenShenBenchmarkRunner(
    IMenShenChatClient client,
    IReadOnlyList<MenShenModelProfile> profiles,
    string apiKey,
    TimeSpan pacingInterval)
```

The runner must:

- use exactly the three fixed profiles;
- execute every case three times;
- shuffle attempt order with seed 7351 while preserving reproducibility;
- send only one request at a time;
- use each case's 2000 ms or 500 ms deadline;
- wait a configurable 250 ms pacing interval outside measured request latency;
- never retry a scored attempt;
- parse success content with the strict codec;
- if strict parsing fails, try the single-fence repair once, record
  `UsedMarkdownFenceRepair`, and parse the repaired content; never repair field
  names, enum values, prose, missing fields, or extra fields;
- assign automatic format score 2 only for valid schema;
- assign preferred-match score 1 only when the parsed decision equals the preferred decision;
- apply automatic hard-zero flags for schema failure, adjacent round actors, or a non-over-net action on counted touch 3;
- leave role, space, and risk scores empty for human review;
- continue after timeout, 401, 429, gateway errors, malformed streams, or invalid JSON.

Before writing review files, create a seeded permutation of the three model IDs
and assign A/B/C from that permutation. Profiles must not contain fixed aliases.
Use a separate alias seed derived from the run seed so execution order and alias
mapping are reproducible but independent.

- [ ] **Step 4: Implement local report output**

Write four files to a timestamped directory under `TestResults/MenShen/`:

```text
raw-results.json     actual model IDs, prompts, responses, status, metrics, and auto flags
review.md            model aliases only, grouped by case with answer key after anonymous outputs
review.csv           one row per attempt plus blank role/space/risk/manual-note columns
model-map.json       alias-to-model mapping, kept separate from review.md
```

Never write API keys, authorization headers, environment dumps, or request
headers. Store reasoning only as `reasoning_character_count`, not full reasoning
text. Raw model content is required for JSON auditing.

- [ ] **Step 5: Run runner and report tests**

Use a temporary output directory under `TestResults/` and assert:

- 180 attempts are exported;
- `review.md` contains A/B/C but no real model IDs;
- `raw-results.json` contains model IDs but no value of a sentinel fake API key;
- all four files exist;
- P50/P95 are calculated from successful attempts and timeout rate includes timed-out attempts.

- [ ] **Step 6: Commit runner and reports**

```bash
git add Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkRunner.cs \
  Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkReportWriter.cs \
  Assets/Volleyball/Match/Tests/EditMode/MenShenBenchmarkRunnerTests.cs
git commit -m "feat: run anonymous menshen benchmarks"
```

### Task 8: Add Secure Editor And Batch Entrypoints

**Files:**
- Create: `Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkCommand.cs`
- Modify: `docs/development.md`

- [ ] **Step 1: Add entrypoint tests around configuration resolution**

Extract a pure `MenShenBenchmarkConfiguration.Resolve(Func<string,string> readEnvironment)`
and test:

```csharp
[Test]
public void Resolve_MissingKey_DisablesRunWithoutPrintingSecret()
{
    var config = MenShenBenchmarkConfiguration.Resolve(_ => null);

    Assert.That(config.CanRun, Is.False);
    Assert.That(config.Error, Is.EqualTo("MENSHEN_API_KEY is not configured."));
}

[Test]
public void Resolve_HttpNonLocalEndpoint_RejectsInsecureTransport()
{
    string Read(string name) => name switch
    {
        "MENSHEN_API_KEY" => "sentinel-secret",
        "MENSHEN_BASE_URL" => "http://menshen-code.test.xdf.cn/v1/chat/completions",
        _ => null
    };

    var config = MenShenBenchmarkConfiguration.Resolve(Read);

    Assert.That(config.CanRun, Is.False);
    Assert.That(config.Error, Does.Not.Contain("sentinel-secret"));
}
```

- [ ] **Step 2: Implement the command**

Expose:

```csharp
[MenuItem("Volleyball/AI/Run MenShen Decision Benchmark")]
public static async void RunFromMenu()
{
    await RunCoreAsync(exitEditor: false);
}

public static void Run()
{
    RunCoreAsync(exitEditor: true).GetAwaiter().GetResult();
}
```

`RunCoreAsync` must read `MENSHEN_API_KEY` and optional `MENSHEN_BASE_URL`,
require HTTPS unless the host is exactly `localhost` or `127.0.0.1`, load the
approved fixture, run the benchmark, write reports, print only the report path
and aggregate counts, and exit non-zero in batch mode on missing credentials or
runner failure. It must never echo the key or full environment.

- [ ] **Step 3: Document local execution without embedding credentials**

Add this command to `docs/development.md`:

```bash
source "$HOME/.zshrc"
test -n "$MENSHEN_API_KEY" || { echo "MENSHEN_API_KEY is missing"; exit 1; }
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -projectPath "$PWD" \
  -executeMethod Volleyball.Editor.AI.MenShenBenchmarkCommand.Run \
  -logFile "$PWD/TestResults/MenShen-benchmark.log"
```

State explicitly that reports are local/ignored, live gateway runs are not part
of EditMode or PlayMode regression, and Unity player builds must not receive a
MenShen key.

- [ ] **Step 4: Run configuration and command tests**

Expected: missing-key and insecure-endpoint tests fail closed, and logs never contain the sentinel secret.

- [ ] **Step 5: Commit secure entrypoints**

```bash
git add Assets/Volleyball/Match/Editor/AI/MenShenBenchmarkCommand.cs \
  Assets/Volleyball/Match/Tests/EditMode/MenShenBenchmarkRunnerTests.cs \
  docs/development.md
git commit -m "feat: add menshen benchmark command"
```

### Task 9: Verify Offline Regressions And Run The Live Benchmark

**Files:**
- Create: `docs/changes/2026-07-18-006-menshen-decision-benchmark.md`
- Modify: `docs/changes/README.md`

- [ ] **Step 1: Run all EditMode tests**

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/EditMode-menshen.xml" \
  -logFile "$PWD/TestResults/EditMode-menshen.log"
```

Expected: all EditMode tests pass, including the new offline gateway/runner tests.

- [ ] **Step 2: Run all PlayMode tests**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/PlayMode-menshen.xml" \
  -logFile "$PWD/TestResults/PlayMode-menshen.log"
```

Expected: all existing PlayMode tests pass unchanged, proving the benchmark assembly did not affect live scenes.

- [ ] **Step 3: Run one authenticated smoke case**

Add a `-menshenSmokeCase R03` command-line option that restricts the command to
one case and one repetition for all three models. Run it with the Step 8 command.

Expected: three attempts are recorded; the process does not expose the API key;
each success either parses to strict JSON or records a precise invalid-output
status; failures do not crash Unity.

- [ ] **Step 4: Run the complete 180-attempt benchmark**

Run the Step 8 batch command without `-menshenSmokeCase`.

Expected: reports contain 180 attempts. Do not claim that a model passes until
the user fills the human role/space/risk scores and the report is re-aggregated.
Record actual P50/P95, JSON success, timeout, and token metrics from the report.

- [ ] **Step 5: Create the Match-internal change record**

Create CHG-006 with:

- status `已完成` only after Tasks 1-4 pass;
- owner and affected module `Match`;
- interaction level `模块内部`;
- exact Unity/package versions;
- EditMode and PlayMode counts from fresh XML;
- live smoke/full benchmark report directories without raw API responses;
- explicit statement that no model drives gameplay yet;
- rollback: remove the Editor assembly and Newtonsoft dependency without touching match runtime behavior.

Add CHG-006 at the top of `docs/changes/README.md`.

- [ ] **Step 6: Verify repository hygiene**

```bash
git status --short
git diff --check
git check-ignore TestResults/MenShen/*
rg -n "MENSHEN_API_KEY|api-key" Assets docs Packages \
  --glob '!**/*.meta' --glob '!docs/development.md'
```

Expected: only intended source/docs/package changes are present; `TestResults`
is ignored; no literal key value exists; the only `api-key` source usage builds
an HTTP header from the injected runtime string.

- [ ] **Step 7: Commit verification documentation**

```bash
git add docs/changes/2026-07-18-006-menshen-decision-benchmark.md \
  docs/changes/README.md
git commit -m "docs: record menshen benchmark verification"
```

## Runtime Integration Follow-Up Gate

After the user completes anonymous scoring, compare each model against the
approved acceptance criteria. The next plan may proceed only after recording one
of these outcomes:

1. A model meets both round and touch gates: design round prefetch plus touch-level decisions, with local validation and fallback.
2. A model meets only the round gate: design round-level asynchronous prefetch only; touch decisions remain local.
3. No model meets the round gate: keep the deterministic planner and retain the benchmark as offline evaluation tooling.

The next plan must introduce a backend/proxy boundary before production builds.
Editor-only direct MenShen access is a development probe, not a shipping architecture.
