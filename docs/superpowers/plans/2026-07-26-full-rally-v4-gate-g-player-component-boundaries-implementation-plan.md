# Full Rally V4 Gate G Player Component Boundaries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改变正式 6v6、legacy 3v3 或 Replay V4 行为的前提下，将 `PrototypePlayerAgent` 拆为五个可独立测试的 player runtime 组件，并保留 facade 兼容入口。

**Architecture:** `PrototypePlayerAgent` 继续是 director、bootstrap、replay 和测试的唯一调用入口，但只保存身份、配置、组件引用和兼容投影。`PlayerTechniqueExecutor` 提供 V4-only 的已解析执行状态，`PlayerActionTimeline` 管理窗口，`PlayerLocomotion` 产生唯一根运动，`PlayerPresentation` 设置 rig pose，`PlayerContactSurfaceProvider` 最后从该状态生成接触候选；数据单向流动，不允许组件访问 director、planner、rules 或 replay recorder。

**Tech Stack:** Unity `6000.0.43f1`、C#、Unity `MonoBehaviour`、NUnit EditMode/PlayMode、原生 `ExecutionEnvelopeV4`/`MatchReplayV4`。

---

## 文件结构

- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerActionTimeline.cs`：封装 scheduled/support/block/emergency action timeline 与窗口状态。
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerTechniqueExecutor.cs`：保存并验证 V4-only resolved execution state。
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerPresentation.cs`：创建 `StickFigureRig` 并应用 action pose。
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerLocomotion.cs`：持有普通、support/block、attack approach 的 root-motion state 和 court clamp。
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaceProvider.cs`：从 execution/timeline/locomotion/presentation 生成 frame、capsule 与 `BallContactCandidate`。
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`：保留公共 facade；将状态与实现逐任务迁移到组件。
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaces.cs`：只在 provider 需要时抽出无状态 response helper；不改变 frame 公式。
- Test: `Assets/Volleyball/Match/Tests/EditMode/PlayerActionTimelineTests.cs`、`PlayerTechniqueExecutorTests.cs`、`PlayerLocomotionTests.cs`、`PlayerContactSurfaceProviderTests.cs`、`PlayerPresentationTests.cs`。
- Modify Test: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`、`Stage2AbilityEnvelopeTests.cs`、`Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`、`FormalSixVsSixReplayPlayModeTests.cs`、`ThreeVsThreeRallyPlayModeTests.cs`。
- Create: `docs/changes/2026-07-26-001-full-rally-v4-gate-g-player-component-boundaries.md`，并更新 `docs/changes/README.md`。

## 通用命令

所有 Unity 命令均从项目根目录执行；不要添加 `-quit`，否则 Unity 6000 可能在写出 XML 前退出。

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
```

### Task 1：冻结 facade 基线并引入 action-timeline 组件

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerActionTimeline.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/PlayerActionTimelineTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [ ] **Step 1：写失败的 timeline 所有权和 cancel 测试。**

```csharp
[Test]
public void Schedule_ExposesActualTimelineAndCancelClearsWindow()
{
    var timeline = new PlayerActionTimeline();
    timeline.ScheduleContact(TechniqueAction.Attack, 2f, 0.03f);

    Assert.That(timeline.Sample(2.03f).SurfaceActive, Is.True);
    Assert.That(timeline.HasScheduledContact, Is.True);

    timeline.CancelContact();

    Assert.That(timeline.HasScheduledContact, Is.False);
    Assert.That(timeline.TrySampleContact(2.03f, out _), Is.False);
}
```

- [ ] **Step 2：运行测试，确认 RED。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerActionTimelineTests" \
  -testResults "$PWD/TestResults/GateG-action-red.xml" \
  -logFile "$PWD/TestResults/GateG-action-red.log"
```

预期：编译失败，因为 `PlayerActionTimeline` 尚不存在。

- [ ] **Step 3：实现最小的 action-timeline 状态机。**

`PlayerActionTimeline` 持有私有 `ActionTimeline _contactTimeline`、`ActionTimeline _supportTimeline`、scheduled action 和 emergency-window 时间；提供 `ScheduleContact`、`ScheduleSupport`、`CancelContact`、`DisableSupport`、`EnableEmergencyReceive`、`DisableEmergencyReceive`、`TrySampleContact` 与只读窗口属性。它只包装既有 `ActionTimeline`，不得写 `Transform`、`StickFigureRig` 或 `BallContactCandidate`。

```csharp
public bool TrySampleContact(float simulationTime, out ActionTimelineSample sample)
{
    if (_contactTimeline == null)
    {
        sample = default;
        return false;
    }

    sample = _contactTimeline.Sample(simulationTime);
    return true;
}
```

在 facade 中将 `_actionTimeline`、`_supportTimeline`、`_hasScheduledContact`、`_hasSupportAction` 和 emergency window 的读写替换为组件调用；保留 facade 的公开事件和属性名。

- [ ] **Step 4：运行 action 与既有 contact 窄回归。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.ActionTimelineTests|Volleyball.EditModeTests.PlayerActionTimelineTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests" \
  -testResults "$PWD/TestResults/GateG-action-green.xml" \
  -logFile "$PWD/TestResults/GateG-action-green.log"
```

预期：所有过滤测试通过。

- [ ] **Step 5：提交。**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PlayerActionTimeline.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerActionTimeline.cs.meta \
  Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerActionTimelineTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerActionTimelineTests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "refactor: extract player action timeline"
```

### Task 2：抽出 presentation/rig 组件并锁定 preview 状态恢复

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerPresentation.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/PlayerPresentationTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [ ] **Step 1：写失败的 rig 创建、pose 映射和 preview 恢复测试。**

```csharp
[Test]
public void PreviewPose_RestoresLiveRigRotations()
{
    var host = new GameObject("player");
    try
    {
        var presentation = new PlayerPresentation(host.transform, Color.blue, "1");
        presentation.ApplyPose(TechniqueAction.Receive, SetTechniqueStyle.FrontTwoHand, 1f);
        var before = presentation.Rig.CaptureLocalRotations();

        presentation.WithPreviewPose(TechniqueAction.Attack, SetTechniqueStyle.FrontTwoHand, () => { });

        CollectionAssert.AreEqual(before, presentation.Rig.CaptureLocalRotations());
    }
    finally { Object.DestroyImmediate(host); }
}
```

- [ ] **Step 2：运行测试，确认 RED。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerPresentationTests" \
  -testResults "$PWD/TestResults/GateG-presentation-red.xml" \
  -logFile "$PWD/TestResults/GateG-presentation-red.log"
```

预期：编译失败，因为 `PlayerPresentation` 不存在。

- [ ] **Step 3：实现 `PlayerPresentation`，并迁移 facade 的 rig 写入。**

组件构造函数创建 rig；`ApplyPose` 包含当前 `ContactPoseFor`、set-style 映射和 `Ready`/`Run`/`Block` 映射；`WithPreviewPose` 保存/恢复 local rotations。不得暴露允许外部写 pose 的可变 state，facade 的 `Rig` 属性仅转发 `Presentation.Rig`。

```csharp
public void WithPreviewPose(TechniqueAction action, SetTechniqueStyle style, Action capture)
{
    var rotations = Rig.CaptureLocalRotations();
    try
    {
        ApplyPose(action, style, 1f);
        capture();
    }
    finally
    {
        Rig.RestoreLocalRotations(rotations);
    }
}
```

- [ ] **Step 4：运行 presentation、surface 和 facade 回归。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerPresentationTests|Volleyball.EditModeTests.StickFigureRigTests|Volleyball.EditModeTests.PlayerContactSurfacesTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests" \
  -testResults "$PWD/TestResults/GateG-presentation-green.xml" \
  -logFile "$PWD/TestResults/GateG-presentation-green.log"
```

预期：所有过滤测试通过，preview 不修改 live rig。

- [ ] **Step 5：提交。**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PlayerPresentation.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerPresentation.cs.meta \
  Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerPresentationTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerPresentationTests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "refactor: extract player presentation"
```

### Task 3：抽出 V4-only technique executor 并禁止 legacy fallback

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerTechniqueExecutor.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/PlayerTechniqueExecutorTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs`

- [ ] **Step 1：写失败的 V4 identity 和拒绝路径测试。**

```csharp
[Test]
public void ScheduleV4_StoresExecutableEnvelopeAndRejectsUnacceptedSample()
{
    var executor = new PlayerTechniqueExecutor();
    var classification = CreateAcceptedClassification();

    executor.ScheduleV4(TechniqueAction.Attack, 2f, classification, default, 7, null, false, null);

    Assert.That(executor.ExecutionEnvelope, Is.SameAs(classification.ExecutableEnvelope));
    Assert.That(executor.ExecutionSample, Is.SameAs(classification.ExecutableSample));
    Assert.That(() => executor.ScheduleV4(
        TechniqueAction.Attack, 2f, CreateRejectedClassification(), default, 7, null, false, null),
        Throws.TypeOf<InvalidOperationException>());
}
```

- [ ] **Step 2：运行测试，确认 RED。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerTechniqueExecutorTests" \
  -testResults "$PWD/TestResults/GateG-technique-red.xml" \
  -logFile "$PWD/TestResults/GateG-technique-red.log"
```

预期：编译失败，因为 `PlayerTechniqueExecutor` 不存在。

- [ ] **Step 3：实现 immutable execution command 和 executor。**

新增内部不可变 `PlayerExecutionCommand`，包含 action、actual contact time、target velocity、error、group、planned center、set route、attack approach/contact plan、controlled handling、trajectory artifact。`PlayerTechniqueExecutor.ScheduleV4` 验证 classification kind、`ExecutableEnvelope`/`ExecutableSample` 非空，以及 `ExecutableSample.EnvelopeIdentity == ExecutableEnvelope.Identity`，然后保存这两个同一实例；它不得引用 `PlayerAbilityProfile`，也不得重新分类、预测或生成 sample。

将 facade 的 V4 `ScheduleContact` 重定向到 executor。保留旧 `SimVector3 targetVelocity` overload 仅供 legacy 3v3 兼容，但该 overload 必须显式标为 compatibility path，且正式 V4 director 调用链只能进入 `ScheduleV4`。V4 path 不得调用 `plannedEnvelope.Classify` 产生新的 command，也不得按 `Ability.AttackPowerCapacity` 重写 resolved sample velocity。

```csharp
if (classification.Kind is ExecutionSampleClassificationKindV4.UnexpectedExecutionSample
    or ExecutionSampleClassificationKindV4.EnvelopeExceeded)
{
    throw new InvalidOperationException("Only accepted or expanded V4 samples may be scheduled.");
}
```

- [ ] **Step 4：运行 executor、envelope 和 contact 回归。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerTechniqueExecutorTests|Volleyball.EditModeTests.Stage2AbilityEnvelopeTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests" \
  -testResults "$PWD/TestResults/GateG-technique-green.xml" \
  -logFile "$PWD/TestResults/GateG-technique-green.log"
```

预期：所有过滤测试通过，正式 V4 path 保持 envelope/sample object identity。

- [ ] **Step 5：提交。**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PlayerTechniqueExecutor.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerTechniqueExecutor.cs.meta \
  Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerTechniqueExecutorTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerTechniqueExecutorTests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs
git commit -m "refactor: isolate v4 player technique execution"
```

### Task 4：抽出 locomotion 并固定连续攻击与 `0.18m` 上限

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerLocomotion.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/PlayerLocomotionTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [ ] **Step 1：写失败的 attack 连续性、shortfall 和 correction 上限测试。**

```csharp
[Test]
public void AttackSamples_AreSpeedBoundedAndCorrectionNeverExceedsBaseline()
{
    var locomotion = CreateAttackLocomotion();
    var first = locomotion.Sample(1.90f);
    var second = locomotion.Sample(1.91f);

    Assert.That(Vector3.Distance(first.Position, second.Position),
        Is.LessThanOrEqualTo(locomotion.MaximumSpeed * 0.01f + 0.0001f));
    Assert.That(locomotion.MaximumAppliedContactCorrection,
        Is.LessThanOrEqualTo(PrototypePlayerAgent.NetClearance));
}
```

- [ ] **Step 2：运行测试，确认 RED。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerLocomotionTests" \
  -testResults "$PWD/TestResults/GateG-locomotion-red.xml" \
  -logFile "$PWD/TestResults/GateG-locomotion-red.log"
```

预期：编译失败，因为 `PlayerLocomotion` 不存在。

- [ ] **Step 3：迁移 root-motion 实现到 `PlayerLocomotion`。**

迁移 `ConfigureScheduledMovement`、`ConfigureContinuationMovement`、`EvaluateScheduledMovement`、support/block root movement、attack approach/takeoff/contact/landing、court clamp、`MoveTo` 和 takeoff observation。组件接收 root `Transform`、team side、court half length、移动速度和 immutable command；不得引用 timeline executor 以外的 live subsystem。

将攻击对齐逻辑保持为显式 clamp，未闭合距离更新 `MovementShortfall`/actual deviation：

```csharp
var requested = plannedCenter - actualCenter;
var applied = Vector3.ClampMagnitude(ToUnity(requested), PrototypePlayerAgent.NetClearance);
root.position += applied;
MaximumAppliedContactCorrection = Mathf.Max(
    MaximumAppliedContactCorrection,
    applied.magnitude);
```

facade 的位置、takeoff、movement 指标和 coroutine API 都转发 locomotion。不要改变 receive/set/controlled-handling 的既有校正策略。

- [ ] **Step 4：运行 locomotion 与原 contact 回归。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerLocomotionTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests|Volleyball.EditModeTests.Stage2AbilityEnvelopeTests" \
  -testResults "$PWD/TestResults/GateG-locomotion-green.xml" \
  -logFile "$PWD/TestResults/GateG-locomotion-green.log"
```

预期：所有过滤测试通过，包括既有最大攻击校正 `<= 0.18m` 断言。

- [ ] **Step 5：提交。**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PlayerLocomotion.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerLocomotion.cs.meta \
  Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerLocomotionTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerLocomotionTests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "refactor: extract player locomotion"
```

### Task 5：抽出 contact provider，并保持候选、frame 和取消语义

**Files:**
- Create: `Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaceProvider.cs`
- Create: `Assets/Volleyball/Match/Tests/EditMode/PlayerContactSurfaceProviderTests.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaces.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [ ] **Step 1：写失败的 provider candidate 和 cancel 后无幽灵接触测试。**

```csharp
[Test]
public void Collect_UsesResolvedSurfaceAndCancelProducesNoFutureCandidates()
{
    var provider = CreateProviderWithAcceptedAttack();
    var contacts = new List<BallContactCandidate>();

    provider.Collect(CreateActiveInput(2f), contacts);
    Assert.That(contacts, Is.Not.Empty);

    provider.Clear();
    contacts.Clear();
    provider.Collect(CreateActiveInput(2.01f), contacts);
    Assert.That(contacts, Is.Empty);
}
```

- [ ] **Step 2：运行测试，确认 RED。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerContactSurfaceProviderTests" \
  -testResults "$PWD/TestResults/GateG-contact-red.xml" \
  -logFile "$PWD/TestResults/GateG-contact-red.log"
```

预期：编译失败，因为 `PlayerContactSurfaceProvider` 不存在。

- [ ] **Step 3：实现 provider 并迁移 `CollectContacts`。**

provider 组合既有 `PlayerContactSurfaces` 与 `BlockArmContactVolumes`，接收 immutable `PlayerExecutionCommand`、`ActionTimelineSample`、locomotion/presentation 投影，生成 surface/capsule、`LastScheduledSurfaceCenter`、`LastScheduledSurfaceNormal`、minimum plan error 和候选。将 `ResponseFor` 移为 provider 私有静态方法或无状态 helper；不得从 provider 访问 `Ability`，所需 `playerTechnique` 必须是 facade/command 的已解析输入。

```csharp
public void Collect(PlayerContactInput input, ICollection<BallContactCandidate> contacts)
{
    if (!input.Sample.SurfaceActive || input.Execution == null)
    {
        return;
    }

    foreach (var surface in _surfaces.Capture(
        input.SurfaceAction, true, input.Execution.ContactGroupId,
        setContactHand: input.Execution.SetContactHand))
    {
        contacts.Add(CreateCandidate(surface, input));
    }
}
```

`PrototypePlayerAgent.CollectContacts` 只获取 timeline/locomotion/presentation sample 并将 provider 结果加入调用方集合；保留 physical block、support 和 emergency 分支的现有触发顺序，直到各分支同样委托到 provider。

- [ ] **Step 4：运行 contact、block、surface 和 envelope 回归。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerContactSurfaceProviderTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests|Volleyball.EditModeTests.PlayerContactSurfacesTests|Volleyball.EditModeTests.BlockArmContactVolumesTests|Volleyball.EditModeTests.Stage2AbilityEnvelopeTests" \
  -testResults "$PWD/TestResults/GateG-contact-green.xml" \
  -logFile "$PWD/TestResults/GateG-contact-green.log"
```

预期：所有过滤测试通过，contact group、surface frame、block capsule 和 response 不变。

- [ ] **Step 5：提交。**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaceProvider.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaceProvider.cs.meta \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaces.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerContactSurfaceProviderTests.cs \
  Assets/Volleyball/Match/Tests/EditMode/PlayerContactSurfaceProviderTests.cs.meta \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs
git commit -m "refactor: extract player contact surfaces"
```

### Task 6：收紧 facade，验证运行时不变性和 replay 证据

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`
- Modify: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [ ] **Step 1：写失败的 facade 无内部业务 state 和 fixed-seed 不变性测试。**

在 EditMode 通过反射检查 facade 仅保留 identity/configuration/组件字段，不再声明 `ActionTimeline`、`PlayerContactSurfaces`、`BlockArmContactVolumes`、attack/root-motion 或 V4 execution state 字段。PlayMode 新增同一 fixed seed 两次 formal capture：一轮使用完整 facade 委托，另一轮通过相同 bootstrap 正常运行；断言 score、accepted contact sequence、V3 transitions、Replay V4 JSON bytes/hash 和每个 shadow artifact identity 相等。

```csharp
Assert.That(second.HomeScore, Is.EqualTo(first.HomeScore));
CollectionAssert.AreEqual(first.ContactSequence, second.ContactSequence);
Assert.That(second.ReplayHash, Is.EqualTo(first.ReplayHash));
CollectionAssert.AreEqual(first.JsonBytes, second.JsonBytes);
```

- [ ] **Step 2：运行测试，确认 RED。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests" \
  -testResults "$PWD/TestResults/GateG-invariance-red.xml" \
  -logFile "$PWD/TestResults/GateG-invariance-red.log"
```

预期：新 facade-isolation 或 byte-invariance 测试失败，直到所有转发和 reset 时机正确。

- [ ] **Step 3：完成 facade 委托并删除重复实现。**

删除 facade 中已迁移的私有 state、直接 `transform.position` 写入、`Rig.SetPose`、frame/capsule 构造和 response 计算；其公共属性转发五个组件投影。确保所有 schedule/cancel/retarget 操作先验证 command，再原子地更新 executor、timeline、locomotion、presentation 和 provider；若验证失败，不得更改任何组件。

进行静态边界审查：

```bash
rg -n "PhysicalMatchRallyDirector|FullRallyV3RulesRuntimeAdapter|MatchReplayRecorder|TeamRallyDecisionPlanner" \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerActionTimeline.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerTechniqueExecutor.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerPresentation.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerLocomotion.cs \
  Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaceProvider.cs
```

预期：无输出。

- [ ] **Step 4：运行 Gate G 窄回归。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.PlayerActionTimelineTests|Volleyball.EditModeTests.PlayerPresentationTests|Volleyball.EditModeTests.PlayerTechniqueExecutorTests|Volleyball.EditModeTests.PlayerLocomotionTests|Volleyball.EditModeTests.PlayerContactSurfaceProviderTests|Volleyball.EditModeTests.PrototypePlayerContactSourceTests|Volleyball.EditModeTests.Stage2AbilityEnvelopeTests|Volleyball.EditModeTests.MatchReplayV4Tests" \
  -testResults "$PWD/TestResults/GateG-focused-editmode.xml" \
  -logFile "$PWD/TestResults/GateG-focused-editmode.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests|Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests|Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests" \
  -testResults "$PWD/TestResults/GateG-focused-playmode.xml" \
  -logFile "$PWD/TestResults/GateG-focused-playmode.log"
```

预期：零失败；正式 replay 仍带有每个 contact 的 Gate F shadow。

- [ ] **Step 5：提交。**

```bash
git add Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs \
  Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs \
  Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs
git commit -m "refactor: delegate player facade to components"
```

### Task 7：完整回归、变更记录和最终审查

**Files:**
- Create: `docs/changes/2026-07-26-001-full-rally-v4-gate-g-player-component-boundaries.md`
- Modify: `docs/changes/README.md`

- [ ] **Step 1：运行完整 EditMode 和 PlayMode。**

```bash
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/GateG-final-editmode.xml" \
  -logFile "$PWD/TestResults/GateG-final-editmode.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/GateG-final-playmode.xml" \
  -logFile "$PWD/TestResults/GateG-final-playmode.log"
rg -n '<test-run' TestResults/GateG-final-editmode.xml TestResults/GateG-final-playmode.xml
git diff --check
```

预期：两个 XML 都为 `result="Passed" failed="0"`，且 `git diff --check` 无输出。

- [ ] **Step 2：创建 change record。**

从 `docs/changes/TEMPLATE.md` 创建记录，状态为“已完成”，说明 facade 兼容边界、五个组件的责任、V4-only technique executor、`0.18m` 攻击对齐上限、未包含的战术权威迁移，以及精确的本轮 XML 计数/路径。更新变更索引；不可写入历史测试数量或声称手动场景验证。

- [ ] **Step 3：执行最终边界与需求审查。**

确认：

- facade 不保存迁移后的业务 state，也不直接修改 rig/root/contact response；
- technique executor 不引用 `PlayerAbilityProfile`、base attributes、director/planner/rules/predictor；
- 所有五个组件没有 director、planner、rules 或 replay recorder 引用；
- 6v6/3v3 公开 bootstrap API 未变，V4 JSON/hash 和 Gate F shadow 不变；
- 攻击 correction 从不超过 `0.18m`，未闭合误差可观测；
- `git diff --check` 无输出。

- [ ] **Step 4：提交。**

```bash
git add docs/changes/2026-07-26-001-full-rally-v4-gate-g-player-component-boundaries.md \
  docs/changes/README.md
git commit -m "test: verify gate g player component invariance"
```

## Final Review Checklist

- [ ] `PrototypePlayerAgent` 只作为 facade/兼容层，不复制五个组件的业务 state。
- [ ] `PlayerTechniqueExecutor` 只消费 V4 envelope/sample/classification，且不重新预测或分类。
- [ ] locomotion 的 attack approach/takeoff/contact/landing 连续、速度有界，攻击 correction `<= 0.18m`。
- [ ] contact provider 不移动 root、不选择战术；presentation 不产生 contact；timeline 不设置 pose。
- [ ] 正式 6v6、legacy 3v3、score、contact sequence、V3 transition、Replay V4 hash 和 Gate F shadow 均保持不变。
- [ ] 完整 EditMode/PlayMode 通过，`git diff --check` 清洁，change record 记录实际结果。
