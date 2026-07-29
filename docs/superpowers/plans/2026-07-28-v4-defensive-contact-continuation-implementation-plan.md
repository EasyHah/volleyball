# V4 确定性防守接触与拦网延续实现计划

**目标：** 修复普通扣球和拦网反弹后的防守接触空窗，使 `FloorDefense`、`AttackCover` 和 `BlockRecovery` 都通过真实球体/球员接触面几何产生合法 `Receive`，并让规则、物理、Result 与 Replay 对同一事件给出一致、可复核、可重放的结论。

**非目标：**

- 不把角色胶囊碰撞体当作排球触球面。
- 不根据预测落点或规划结果直接修改球速、触球数或球权。
- 不把防守成功降级为“球员到达某点即成功”。
- 不顺带重构整个 `PhysicalMatchRallyDirector`。
- 不引入依赖渲染帧率、wall-clock、非固定 epsilon 或容差累积的判定。

**技术栈：** Unity 6000.0.43f1、C# 9、NUnit EditMode、Unity PlayMode、现有 Full Rally V3 rules、Gate I authority/controller、120 Hz `SimulatedBall`、Shared V4 Replay/canonical codec。

---

## 1. 当前故障边界

实现前先固定以下已确认的结构性缺口，避免把现象误判为单纯的碰撞面大小问题。

### 1.1 普通 FloorDefense 只移动，不注册物理 Receive

当前 `AttackDefenseAuthorityController` 仅在
`ToolRecoveryAwaitingReceive + FloorDefense` 时把命令视为物理接触：

- 普通 `FloorDefense` 走 `ScheduleSupportAction`。
- `AttackCover` 也只走 `ScheduleSupportAction`。
- `IsContact` 不承认普通 `FloorDefense` 或 `AttackCover`。

因此 Gate I 已经承诺了球员和移动，但该球员不会向 `SimulatedBall` 提供 Receive 接触面候选。

### 1.2 Director 只为特殊 tool recovery 打开 Gate I Receive 窗口

`PhysicalMatchRallyDirector.PublishGateIBatch` 当前只为：

- `AttackContact` 打开 Attack window；
- `ToolRecoveryAwaitingReceive` 的 `FloorDefense` 打开 Receive window。

普通 `FloorDefense` receipt 虽被保存，却不会建立与实际防守接触对应的 continuation/contact-window 生命周期。

### 1.3 Post-block 状态只表达“等待 crossing”

`_awaitingPostBlockCrossing` 无法区分：

- 球被拦回进攻方；
- 球留在拦网方；
- 球刚好处于网平面附近尚未决；
- crossing 已在同一 fixed step 内发生；
- 球先落地。

球留在当前一侧时可能永远没有新的 crossing，于是已承诺的保护/防守接触没有被激活。

### 1.4 重叠候选仍隐含依赖 contact source 注册顺序

`SimulatedBall.TryFindEarliestPlayerContact` 只按 `TimeFraction` 选择最早碰撞；相同时间的候选保留先枚举者。该枚举顺序来自 contact source 注册顺序，不是显式的比赛规则排序证据。

### 1.5 Replay 能记录接受事件，但缺少完整尝试审计

现有 `ReplayContactEvent` 可以绑定：

- Rule transition；
- execution classification；
- trajectory artifact；
- Gate H/Gate I receipt；
- revision/source sequence。

但普通防守漏球没有统一记录：

- continuation side；
- contact window 起止；
- 实际球样本；
- 候选被拒绝或未相交的原因。

先复用现有事件和 frame samples；只有无法完整表达上述证据时才升级 Shared V4 schema。

---

## 2. 设计原则与状态模型

### 2.1 接触资格与接触成功严格分离

资格由 authority/controller 建立，成功由 `SimulatedBall` 的 swept geometry 决定：

```text
Gate I command committed
→ player movement + legal receive surface scheduled
→ continuation resolves eligible side
→ time window and side eligibility allow candidate
→ swept ball/surface geometry intersects
→ deterministic winner selected
→ V3 rules commit exactly one contact
→ technique response changes ball velocity
```

任意一环失败都不得产生 Receive。

### 2.2 用明确 continuation state 替换布尔等待

在 Presentation 边界增加内部状态，不先改 Domain 规则：

```csharp
internal enum PostAttackContinuationStateV4
{
    None,
    AwaitingAttackCrossing,
    AwaitingBlockOutcome,
    PendingSideResolution,
    DefendingSideFloorDefense,
    AttackingSideCoverage,
    BlockingSideRecovery,
    ResolvedByContact,
    ResolvedByGround
}
```

同时保存不可变/单调递增证据：

- 触发 contact group；
- Block actor/team；
- Block contact simulation time；
- Block impact center；
- contact 后实际速度；
- 最近一次合法 crossing time/side；
- resolved continuation side；
- 对应 Gate I revision/source sequence；
- 激活的 contact-window identities。

状态只由 fixed-step 物理事件推进：

- Attack accepted；
- net crossing；
- Block accepted；
- player contact accepted/rejected；
- ground landing。

### 2.3 continuation side 判定

优先级必须固定：

1. 同一 fixed step 内已发生且早于下一物理事件的实际 net crossing；
2. Block 接触点位于网的哪一侧；
3. Block 后实际 `Velocity.Z` 的严格符号；
4. 若球中心/速度仍在未决走廊内，保持 `PendingSideResolution`；
5. 后续 crossing、离开未决走廊或 ground event 完成判定。

不得用规划轨迹提前赋予球权。预测只用于：

- 判断候选是否有足够剩余移动时间；
- 选择已提交球员中的接触时刻与移动目标；
- 生成可审计 artifact。

### 2.4 候选确定性排序

`SimulatedBall` 对同一 fixed step 内的实际几何命中按以下精确键排序：

1. `SweptBallHit.TimeFraction`；
2. contact group id；
3. action priority（Block 与 Receive 不共享同一 continuation window）；
4. team；
5. roster slot；
6. role；
7. surface/capsule 的稳定索引。

不使用 epsilon 合并“近似同时”。排序键完全相等时视为同一物理事件，只提交一个候选；其余记录为 `SupersededByDeterministicContact`。

---

## 3. 实现任务

### Task 1：建立固定种子故障基线

**新增：**

- `Assets/Volleyball/Match/Tests/PlayMode/DefensiveContactContinuationPlayModeTests.cs`
- 对应 Unity 自动生成的 `.meta`

**修改：**

- `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
  - 只增加只读诊断快照/事件，不改变比赛行为。

新增固定 seed fixture，使用正式 6v6 V4 context、实际 `SimulatedBall` 和实际 `PrototypePlayerAgent` 接触面。每个场景保存：

- Gate I command/receipt；
- window start/end；
- ball fixed-step samples；
- net crossing；
- ground landing；
- accepted/rejected physical contact；
- rally result。

首批三个基线场景：

1. `ReachableSpike_BaselineShowsCommittedFloorDefenseWithoutPhysicalCandidate`
2. `BlockReturnToAttackingSide_BaselineShowsCoverageDeadZone`
3. `BlockStaysOnDefendingSide_BaselineShowsRecoveryDeadZone`

基线测试应明确失败在“资格未注册/continuation 未激活”，而不是只断言没有 Receive。基线证据写入 `TestResults/V4-DefensiveContact-Baseline-*`，不提交大体积运行日志。

**完成标准：**

- 三个场景连续运行至少三次得到相同 Gate I revision/source sequence、crossing/landing 分类和失败原因。
- 此任务不修改规则、球速或触球数。

### Task 2：先写物理 PlayMode 红测

在同一测试文件加入最终行为测试：

1. `CommittedFloorDefense_DigsReachableSpike`
2. `LateFloorDefense_DoesNotCreateMagicDig`
3. `BlockReboundToAttackingSide_AllowsAttackCoverage`
4. `BlockReboundToDefendingSide_AllowsBlockRecovery`
5. `PostBlockMiss_StillLetsGroundRefereeScore`
6. `OverlappingDefenders_AcceptOnlyOneReceive`

测试约束：

- 不直接调用 rules engine 的 `CommitContact`/`AcceptContact` 来制造 Receive。
- 球必须经 `SimulatedBall.AdvanceSimulation` 或正常 PlayMode 帧推进。
- 玩家必须由 `PrototypePlayerAgent.CollectContacts` 提供接触面。
- 成功必须同时观察到 `PlayerBallContactEvent`、V3 accepted transition 和 `ReplayContactEvent`。
- 失败必须观察到 ground event 与唯一 rally result。
- Block 场景必须断言 Block 不增加 counted touches，blocker 可再次完成本方第一次 counted touch。

同时增加/保留以下 EditMode 回归：

- `RallyRulesEngineV3`：Block 不计三次触球，blocker 后续触球合法。
- `SimulatedBall`：每个 physical event 只接受一个接触。
- `MatchRallyReferee`：最后触球队伍与落地判分不变。

**红测标准：**

- 可达 FloorDefense 因无物理 Receive 失败。
- 两个 post-block 场景因 continuation dead zone 失败。
- late/miss 测试不得为了制造红测而放宽几何或时间。

### Task 3：让普通 FloorDefense 成为物理候选

**修改：**

- `Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/PlayerActionTimeline.cs`（仅在现有 timeline 无法表达 committed receive window 时）
- `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/PlayerTechniqueExecutorTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/PlayerContactSurfaceProviderTests.cs`

改动：

1. `FloorDefense` 始终经过 `ValidateGateIContact(Receive, ...)`。
2. `Apply` 对 committed `FloorDefense` 调用 `ScheduleContact(Receive, ...)`，而不是 `ScheduleSupportAction`。
3. movement start、scheduled contact time、contact group、classification、trajectory artifact 全部沿用 command execution。
4. `PhysicalContactCenter` 有值时只用于安排合法平台位置，不作为命中判定。
5. `FloorDefense` 的 Receive surface 继续使用 `PlayerContactSurfaces`/`PlayerContactSurfaceProvider`，不启用角色 capsule。
6. 不让普通 FloorDefense 复用 tool-recovery 特判；删除或收窄 `PhysicalToolRecoveryReceive` 分支。

controller 级测试必须证明：

- committed FloorDefense 同时具有 movement 与 scheduled physical contact；
- 尚未到 window、已经超时、移动不可达时不会产生候选；
- command revision/source sequence/artifact 不匹配时 preflight 原子失败；
- cancellation 不得删除已 committed 的 Receive。

### Task 4：建立 post-block continuation controller

**修改：**

- `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- 可新增：
  `Assets/Volleyball/Match/Runtime/Presentation/PostAttackContinuationV4.cs`
- `Assets/Volleyball/Match/Runtime/AI/AttackDefenseAuthorityCoordinator.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs`
- `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityCoordinatorTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/GateKDirectorBoundaryTests.cs`

#### 4.1 扣球合法进入防守方

- Attack accepted 后 continuation 进入 `AwaitingAttackCrossing`。
- 实际合法 crossing 确认防守方后，激活该侧已 committed FloorDefense。
- `BeginPossession` 不再无条件清除正在生效的 committed Gate I Receive。
- 若 Receive 未实际命中，ground referee 仍正常结束回合。

#### 4.2 拦回进攻方

- Block accepted 后保存 actual contact point/velocity。
- 若实际速度或 crossing 证明回到进攻方，进入 `AttackingSideCoverage`。
- 从已提交的进攻保护集合中发布/激活 `AttackCover` Receive command。
- AttackCover 是新的 counted touch；Block 不计入进攻方或拦网方的三次触球。
- 不能复用已消费的 Attack contact ticket。

进攻保护 actor 的来源按优先级选择：

1. 已有 selected tool-recovery evidence 的 recovery actor；
2. Gate I 计划中已声明的 attacking-side reorganization/coverage actor；
3. 若计划未提供合法 actor，则 continuation 保持可落地，不临时从全队生成魔法候选。

若现有 `AttackDefensePlanV3` 无法表达普通 AttackCover actor，最小扩展计划模型，增加显式 `AttackCoverageResponsibilities`；不要把进攻球员塞入 `Defense.Responsibilities`。

#### 4.3 球留在拦网方

- 若 Block 后实际轨迹留在拦网方，进入 `BlockingSideRecovery`。
- 激活该侧已 committed `FloorDefense`/`BlockRecovery` Receive。
- blocker 本人可成为合法 recovery actor；V3 rules 继续允许 Block 后连续 counted touch。

#### 4.4 未决与落地

- 网平面未决走廊内不立即开启两侧窗口。
- 每个 fixed step 根据实际位置、速度和 crossing evidence 单调推进。
- ground event 可从任何未决状态直接进入 `ResolvedByGround`。
- 所有 continuation window 在 accepted contact、ground 或 rally reset 时一次性关闭。

### Task 5：显式、确定性地解决重叠候选

**修改：**

- `Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs`
- 必要时扩展 `BallContactCandidate`，加入稳定 surface identity/index。
- `Assets/Volleyball/Match/Tests/EditMode/SimulatedBallTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/SweptBallCollisionTests.cs`
- `Assets/Volleyball/Match/Tests/PlayMode/DefensiveContactContinuationPlayModeTests.cs`

实现一个纯比较器，不读取 frame count、对象 instance id、哈希表枚举顺序或 wall-clock。

测试：

- 反转 contact source 注册顺序，接受者不变。
- 多名防守球员 window 重叠，只发出一个 `PlayerContact`。
- 未选候选不修改 ball velocity、不提交 rules transition。
- 同一 contact group 的重复候选被忽略而不是判犯规。

### Task 6：补齐尝试与 continuation 证据

**优先修改 Presentation 层：**

- `PhysicalMatchRallyDirector.cs`
- `MatchReplayRecorder.cs`
- `MatchReplayHtmlWriter.cs`

增加只读诊断事件/记录：

```text
DefenseAttemptOpened
DefenseCandidateSampled
DefenseContactAccepted
DefenseContactRejected
DefenseAttemptExpired
PostBlockContinuationResolved
```

每条记录至少包含：

- attempt/window identity；
- Gate I command kind、actor、revision、source sequence；
- artifact/envelope identity；
- window start/end；
- actual simulation time、ball position、ball velocity；
- continuation side/state；
- accepted/rejected/expired reason；
- winning contact group/actor（若被确定性排序淘汰）。

#### Replay schema 升级判定

先验证现有组合是否足够：

- `ReplayContactEvent` + Gate I receipt 表达 accepted contact；
- fixed-rate frame samples 表达 actual ball state；
- `ReplaySimpleEvent` 表达 crossing/landing/continuation diagnostic。

只有以下任一条件成立才升级 Shared schema：

- continuation side 无法进入 canonical Replay；
- rejected/expired reason 无法与 revision/source sequence 绑定；
- HTML 不能从 canonical payload 重建尝试时间线。

若升级：

**修改：**

- `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- `Assets/Volleyball/Shared/Runtime/CanonicalJsonHashV4.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs`
- `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`

要求：

- 新字段有明确默认值和旧 payload 兼容行为；
- canonical property order 固定；
- round-trip、旧版本读取、canonical hash fixture 同步更新；
- 不从 HTML writer 反向推导比赛事实。

### Task 7：规则、Result 与 Replay 一致性回归

**修改测试：**

- `FullRallyV3RulesEngineTests.cs`
- `FullRallyV3EligibilityTests.cs`
- `MatchRallyRefereeTests.cs`
- `MatchReplayV4Tests.cs`
- `FormalSixVsSixReplayPlayModeTests.cs`
- `ThreeVsThreeRallyPlayModeTests.cs`
- `FormalSixVsSixRallyPlayModeTests.cs`

增加交叉断言：

```text
accepted physical Receive count
== accepted V3 counted touch transition count
== Result/statistics receive count
== Replay accepted Receive event count
```

对于 miss：

```text
accepted physical Receive count == 0
ground referee resolves exactly once
Replay contains attempt expiration/rejection + landing + result
```

对于 Block：

```text
Block physical event count == 1
counted touch delta at Block == 0
first post-block Receive counted touch == 1
same blocker Receive is legal
```

---

## 4. 测试执行顺序

所有结果必须写入新的、带本阶段名称的结果文件。禁止引用 Gate K 之前的 745/745 或 39/39。

先创建结果目录：

```bash
mkdir -p TestResults
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/wys/Documents/program/volleyball-match"
```

### 4.1 聚焦 EditMode

```bash
"$UNITY" -batchmode -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.AttackDefenseAuthorityControllerTests;Volleyball.EditModeTests.AttackDefenseAuthorityCoordinatorTests;Volleyball.EditModeTests.FullRallyV3RulesEngineTests;Volleyball.EditModeTests.SimulatedBallTests;Volleyball.EditModeTests.MatchRallyRefereeTests;Volleyball.EditModeTests.MatchReplayV4Tests" \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-Focused-EditMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-Focused-EditMode.log"
```

### 4.2 聚焦 PlayMode

```bash
"$UNITY" -batchmode -projectPath "$PROJECT" \
  -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.DefensiveContactContinuationPlayModeTests" \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-Focused-PlayMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-Focused-PlayMode.log"
```

### 4.3 相关 PlayMode 回归

```bash
"$UNITY" -batchmode -projectPath "$PROJECT" \
  -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.FormalSixVsSixRallyPlayModeTests;Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests;Volleyball.PlayModeTests.ThreeVsThreeRallyPlayModeTests" \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-Related-PlayMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-Related-PlayMode.log"
```

### 4.4 完整 fresh run

```bash
"$UNITY" -batchmode -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-All-EditMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-All-EditMode.log"

"$UNITY" -batchmode -projectPath "$PROJECT" \
  -runTests -testPlatform PlayMode \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-All-PlayMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-All-PlayMode.log"
```

### 4.5 确定性验证

对三个固定 seed 场景各运行至少两次，比较：

- Replay JSON bytes；
- Replay HTML/report bytes；
- canonical hash；
- accepted contact 顺序；
- continuation diagnostics；
- Result bytes。

若运行产物包含 wall-clock、临时绝对路径或非 canonical 日志字段，只比较正式 canonical artifact；不得为了通过字节比较而删除比赛证据。

最后执行：

```bash
git diff --check
```

并检查所有本次 fresh logs：

```bash
rg -n "error CS|Unhandled Exception|AssertionException|UnityException|FAIL|Failed" \
  TestResults/V4-DefensiveContact-*.log
```

允许测试框架的汇总文本命中，但必须人工核对没有新增 Unity Console error。

---

## 5. 提交拆分

在每个提交前运行对应聚焦测试；最后一个提交前运行完整 fresh suite。

1. `test: reproduce missing floor defense and block continuation`
   - 固定 seed fixture；
   - 三个故障基线；
   - 六个最终物理红测；
   - 只读诊断，不改行为。

2. `fix: activate physical receive for committed floor defense`
   - 普通 FloorDefense 物理候选；
   - controller/timeline/surface tests；
   - late/magic-dig 与 overlap 回归。

3. `fix: resolve post-block continuation without contact dead zone`
   - continuation state；
   - AttackCover/BlockRecovery；
   - crossing/ground 优先级；
   - rules/Result 一致性。

4. `test: cover deterministic replay and full rally regressions`
   - Replay 尝试证据；
   - codec/hash 兼容性（若 schema 升级）；
   - fixed seed bytes；
   - ThreeVsThree/FormalSixVsSix/full-suite 结果记录。

---

## 6. 审查清单

### 物理

- [ ] Receive 只由球与合法接触面 swept intersection 产生。
- [ ] 角色 capsule 不会产生 Receive。
- [ ] late/unreachable player 不会起球。
- [ ] miss 不会抑制 ground event。

### 规则

- [ ] Block 不计三次触球。
- [ ] blocker 可在 Block 后再次完成 counted touch。
- [ ] post-block 第一次 Receive 将本方 counted touches 置为 1。
- [ ] 每个 physical event 只提交一次 V3 transition。

### continuation

- [ ] 普通扣球 crossing 激活防守方。
- [ ] 拦回进攻方激活 AttackCover。
- [ ] 留在拦网方激活 BlockRecovery/FloorDefense。
- [ ] 未决状态不会同时给两侧资格。
- [ ] crossing 与 ground 在同一 fixed step 时按物理 time fraction 决定先后。

### 确定性

- [ ] 候选排序不依赖 GameObject instance id、哈希表顺序或注册顺序。
- [ ] 不使用 wall-clock/frame count 判定触球。
- [ ] 不新增漂移 epsilon。
- [ ] fixed seed Replay/HTML/Result bytes 稳定。

### Replay

- [ ] accepted dig 能追溯到 Gate I command、actor、window、ball sample 和 rule transition。
- [ ] missed dig 能看到 rejected/expired reason 与 ground result。
- [ ] post-block continuation side 可审计。
- [ ] revision/source sequence/artifact identities 完整绑定。
- [ ] schema 升级时旧 payload、codec 和 canonical hash 测试同步通过。

---

## 7. 完成判定

只有以下条件全部满足才关闭修复：

- 可达扣球由真实 Receive 起球，且 Receive 早于 ground adjudication。
- 不可达扣球落地，不出现磁吸式防守。
- 拦回进攻方后，已提交保护球员可完成第一次新触球。
- 球留在拦网方后，blocker 或队友可完成本方第一次 counted touch。
- Block 始终不计三次触球，blocker 连续触球合法。
- 多名接触面重叠时只接受确定排序后的一个 Receive。
- 防守计划从不覆盖 ground referee authority。
- Replay 能解释成功、拒绝、超时和漏球。
- fixed seed 执行、序列化、HTML/report 和 canonical hash 稳定。
- 本次修改后的全量 EditMode、全量 PlayMode fresh run 通过。
- `git diff --check` 通过，且无新增 Unity Console error。
