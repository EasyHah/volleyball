# 正式训练情景实验室实施计划

- 前置规格：
  `docs/superpowers/specs/2026-07-31-formal-training-scenario-lab-design.md`
- Active handoff：
  `docs/handoffs/active/2026-07-31-formal-training-scenario-lab-implementation.md`
- 风险等级：高风险；涉及 Match Domain、正式比赛生命周期、独立场景、运行时 UI、输入、构建列表
  和 Windows Player。
- 验证等级：升级验证；新行为测试先行，代码冻结后一次完整 EditMode、受影响 PlayMode、Windows
  Development Build、人工键鼠闭环和一次独立复核。

## 1. 范围与完成定义

本计划只交付第一个里程碑：可视化正式训练情景实验室基础、五类触网情景和只读
`DecisionSnapshotV1`。大模型教师、人工标注数据集、本地深度学习模型及玩家接管均不进入本轮。

完成时必须满足：

- 训练 UI 只能编辑语义化合法起点，不能写底层触球、窗口、比分或裁判状态；
- 训练运行复用正式 6v6 的球、12 名球员、AI、V3 rules authority、真实物理与裁判；
- 运行开始后编辑锁定，暂停/单步只推进现有固定步模拟；
- 旧 `FormalMatchScenarioDefinitionV4`、既有资源 hash、Replay V4 schema 和正式开球行为不变；
- 五类触网情景及不可达变体使用真实球体、几何碰撞和裁判完成 fixed-seed 双跑；
- 独立场景、一体化指挥台、Windows Development Build 和键鼠人工验收闭环；
- 决策快照只读、Match-owned、Editor-only 导出，不改变正式选择。

## 2. 实施前门禁

开始代码前：

1. `git fetch origin`，核对 `origin/main` 和搭档远端分支最新提交。
2. 从当前包含触网实现和已确认设计的提交创建
   `codex/formal-training-scenario-lab`；不继续在已被替代的里程碑分支开发。
3. 确认 worktree 干净、只有一个 `Status: active` handoff。
4. 确认没有 `Assets/InitTestScene*.unity` 及对应 `.meta`。
5. 记录 Unity `6000.3.20f1` 与当前 package lock。

基线检查：

```bash
python3 tools/validate_repository.py

UNITY="/Applications/Unity/Unity-6000.3.20f1/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
  "Volleyball.EditModeTests.RallyTouchStateTests;Volleyball.EditModeTests.FullRallyV3RulesEngineTests;Volleyball.EditModeTests.FormalMatchScenarioPresetV4Tests" \
  -testResults "$PWD/TestResults/TrainingLab-Baseline-EditMode.xml" \
  -logFile "$PWD/TestResults/TrainingLab-Baseline-EditMode.log"

"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.DefensiveContactContinuationPlayModeTests" \
  -testResults "$PWD/TestResults/TrainingLab-Baseline-PlayMode.xml" \
  -logFile "$PWD/TestResults/TrainingLab-Baseline-PlayMode.log"
```

若基线不绿，停止新功能并先报告；不得把既有失败吸收到训练实验室实现。

## 3. 阶段 A：语义化规则起点

### Task A1：先写纯 Domain 失败测试

新增：

- `Assets/Volleyball/Match/Tests/EditMode/TrainingRallyStartV3Tests.cs`

覆盖：

1. `ServeFlight` 创建零计数触球状态。
2. `AfterReceive` 创建一次计数触球并保留 last actor。
3. `AfterSet` 创建两次计数触球。
4. `AfterAttack` 创建三次计数触球，下一次本方计数触球被拒绝。
5. `AfterAcceptedBlock` 创建 block-owned、零计数触球的 post-block 起点。
6. actor 不在来源队、actor 不在 12 人 on-court eligibility、未知 recipe 和不一致 action 被拒绝。
7. 创建起点不发布 accepted-contact event，也不增加 replay source sequence。

先运行：

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.TrainingRallyStartV3Tests" \
  -testResults "$PWD/TestResults/TrainingRallyStart-red.xml" \
  -logFile "$PWD/TestResults/TrainingRallyStart-red.log"
```

### Task A2：实现单一语义工厂

新增：

- `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyStartStateV3.cs`

修改：

- `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/TouchSequenceStateV3.cs`
- `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyRulesEngineV3.cs`
- `Assets/Volleyball/Match/Runtime/Domain/Prototype/RallyTouchState.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/FullRallyV3RulesRuntimeAdapter.cs`

实现边界：

- `RallyStartRecipeV3` 只提供 `ServeFlight`、`AfterReceive`、`AfterSet`、`AfterAttack` 和
  `AfterAcceptedBlock`。
- `RallyStartRequestV3` 接收来源队和最后合法 actor；用户不传 counted hits、窗口或 authority flags。
- `RallyStartStateV3Factory` 同时派生 V3 `TouchSequenceStateV3` 与兼容
  `RallyTouchState` 所需的不可变 seed。
- `RallyRulesEngineV3.Open(startState)` 和 runtime adapter 的 seeded `BeginRally` 只设置起点，不
  伪造或重放历史 contact。
- `RallyTouchState` 增加从同一 seed 建立状态的工厂；禁止 UI 调用
  `SynchronizeAuthoritativeContact` 逐字段拼状态。
- post-block seed 必须包含现有规则和 Director continuation 都需要的语义事实；不得暴露私有布尔
  字段给 UI。

绿测：

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
  "Volleyball.EditModeTests.TrainingRallyStartV3Tests;Volleyball.EditModeTests.RallyTouchStateTests;Volleyball.EditModeTests.FullRallyV3RulesEngineTests;Volleyball.EditModeTests.FullRallyV3RuntimeAdapterTests" \
  -testResults "$PWD/TestResults/TrainingRallyStart-green.xml" \
  -logFile "$PWD/TestResults/TrainingRallyStart-green.log"
```

阶段提交：`feat: add semantic formal rally starts`

## 4. 阶段 B：训练情景合同、规范 hash 与校验

### Task B1：先写合同和校验失败测试

新增：

- `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioV1Tests.cs`
- `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioValidatorV1Tests.cs`

测试必须覆盖：

- 相同完整输入产生相同 canonical bytes 与 content hash；
- 任一球员位置、球位置/线速度、recipe、last actor、战术或
  `MatchContextV4.Seed` 变化都会改变 hash；
- supplied hash 不匹配、非有限数、12 人缺失/重复、actor 侧别错误、出界、球/球员非法穿插被拒绝；
- `AfterAttack` 不能携带第四触球窗口，post-block 不能同时声明普通 continuation；
- 未知格式版本拒绝；
- `FormalMatchScenarioPresetV4` 既有固定资源的 content hash 不变；
- 没有角速度、预期结果、强制成功、比分或裁判字段；
- `TrainingScenarioV1` 不出现在 Shared/Career asmdef 或合同 codec 中。

### Task B2：实现 Match-owned 值与 validator

新增目录：

- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/`

新增：

- `TrainingScenarioV1.cs`
- `TrainingScenarioCanonicalizerV1.cs`
- `TrainingScenarioValidatorV1.cs`
- `TrainingScenarioPresetV1.cs`
- `TrainingScenarioDraftV1.cs`

职责：

- `TrainingScenarioDraftV1` 是可变 UI 状态，允许暂时无效。
- `TrainingScenarioV1` 是不可变运行输入，完整复制 Context、战术、12 人 pose、球位置/线速度和
  semantic recipe。
- canonicalizer 复用 `ContractJson.SerializeV4(context)`，其余字段固定顺序、invariant culture、
  明确格式版本。
- validator 返回稳定 issue code、对象 ID、属性路径和 severity；不自动修改草稿。
- 球员 pose 按稳定 `PlayerId` 规范排序；碰撞和边界使用正式球半径、球员根间距及场地尺寸。
- `matchSeed` 只来自 `MatchContextV4.Seed`，不得增加第二 RNG。
- `TrainingScenarioPresetV1.ToDefinition()` 必须验证 supplied hash。

绿测：

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter \
  "Volleyball.EditModeTests.TrainingScenarioV1Tests;Volleyball.EditModeTests.TrainingScenarioValidatorV1Tests;Volleyball.EditModeTests.FormalMatchScenarioPresetV4Tests" \
  -testResults "$PWD/TestResults/TrainingScenarioContract-green.xml" \
  -logFile "$PWD/TestResults/TrainingScenarioContract-green.log"
```

阶段提交：`feat: add formal training scenario contracts`

## 5. 阶段 C：无 UI 正式运行器与可行性门禁

### Task C1：建立一次性启动和运行测试

新增：

- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioStartupV1.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioRuntimeAdapterV1.cs`
- `Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioRuntimePlayModeTests.cs`

修改：

- `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`

启动边界：

- `TrainingScenarioStartupV1` 与现有 pending Context/scenario 互斥，并在 subsystem registration 清空。
- Bootstrap 仍使用正式 Court、ball、roster、score、director、camera 构建路径；只把训练起点作为
  可选的初始化输入。
- `PhysicalMatchRallyDirector.ConfigureTrainingStart` 只能在 `_set == null` 且 rally 未启动时调用。
- 抽取标准发球与训练起点共同使用的 per-rally reset；旧 `StartInitialLoop` 标准分支必须保持原样。
- 训练分支先建立 V3/compatibility seeded state，再应用球员 pose，最后使用现有
  `SimulatedBall.ResetBall` + `Launch` 设置球并开启 rally。
- 建立与语义 recipe 一致的 flight-segment origin；不得在首帧发布历史 accepted contact。
- `AfterSet` 等 recipe 通过抽取出的现有 Gate H/I 规划入口，从冻结 world state 重建下一动作计划；
  不允许测试或 adapter 直接创建成功 contact receipt。
- `AfterAcceptedBlock` 必须通过专用 post-block semantic initializer 建立现有
  `PostAttackContinuationStateV4`、blocker identity 和合法 Gate I continuation 所需事实；禁止只
  设置 `_awaitingPostBlockCrossing`。
- training provenance 使用 `training-v1/` ID 和训练 content hash，但沿用既有 Replay provenance
  envelope 版本。
- 训练运行启用显式 single-rally 模式：裁判仍正常记一分并发布唯一 `RallyResolved`，随后停止球和
  自动重开协程；不伪造完整 set result。正式比赛默认关闭该模式并继续原有 set 生命周期。

运行测试：

1. 首帧前一次性消费，重复 pending start 拒绝。
2. 初始化不产生 accepted contact/replay contact。
3. 下一次接触必须来自 `SimulatedBall` swept geometry。
4. V3 与 `RallyTouchState` 对 counted hits、last actor、possession 一致。
5. 标准 `InitializeScenario` 的发球、资源 hash 和 provenance 不变。
6. training observer on/off 不改变结果。
7. training single-rally 在首次结算后停止；标准正式比赛仍自动进入下一回合。

### Task C2：强制可行性检查点

在继续 UI 前，必须证明两个无 UI 情景：

- `AfterSet`：球触网后本方只剩一次合法计数触球，真实几何第三触球才能续球。
- `AfterAcceptedBlock`：触网不增加通用 net dispatch，post-block continuation 唯一。

若必须伪造 contact、直接写规则私有字段或复制 Gate I planner 才能通过，立即停止并回到设计；
不得用反射、测试 hook 或条件编译绕过该门禁。

运行：

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.TrainingScenarioRuntimePlayModeTests" \
  -testResults "$PWD/TestResults/TrainingScenarioRuntime-gate.xml" \
  -logFile "$PWD/TestResults/TrainingScenarioRuntime-gate.log"
```

阶段提交：`feat: start formal rallies from training scenarios`

## 6. 阶段 D：五类触网资源与自动验收

新增：

- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioCatalogV1.cs`
- `Assets/Volleyball/Match/Editor/TrainingScenarioAssetCreatorV1.cs`
- `Assets/Volleyball/Match/Runtime/Resources/TrainingScenariosV1/`
- `Assets/Volleyball/Match/Tests/PlayMode/TrainingNetContinuationPlayModeTests.cs`

资源由 Editor creator 从 catalog 的完整输入生成并刷新 content hash；运行时不回退到 catalog。
不要手写 `.asset` YAML 中的 hash，也不要把测试专用资源放入正式情景库。

指定情景：

1. `second-touch-net-own-side`
2. `third-touch-net-own-side`
3. `third-touch-net-cross`
4. `post-block-net`
5. `net-deflection-single-dispatch`
6. 至少一个明确命名的不可达变体

断言：

- 第二触球情景只有真实 swept 第三触球能继续；
- 第三触球留本方没有第四触球窗口，只有一个 ground/out result；
- 第三触球越网只在真实 crossing 后给对方防守机会；
- post-block 的 `NetDeflectionDispatches == 0`，既有 continuation 唯一；
- 同 flight segment 多次 net contact 的 dispatch 总数为一，后续只保留物理回弹；
- 不可达变体 accepted Receive 为零且只有一个裁判结果；
- 每个关键情景同平台、同 build、同 scenario hash、同 seed 双跑，结果、accepted-contact sequence
  和 continuation evidence 一致。

运行：

```bash
"$UNITY" -batchmode -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter \
  "Volleyball.PlayModeTests.TrainingNetContinuationPlayModeTests;Volleyball.PlayModeTests.DefensiveContactContinuationPlayModeTests" \
  -testResults "$PWD/TestResults/TrainingNetContinuation.xml" \
  -logFile "$PWD/TestResults/TrainingNetContinuation.log"
```

阶段提交：`test: add formal net training scenarios`

## 7. 阶段 E：只读时间线与决策快照

新增：

- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingTimelineRecorderV1.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingRunEvidenceV1.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/DecisionSnapshotV1.cs`
- `Assets/Volleyball/Match/Editor/TrainingDecisionSnapshotExporterV1.cs`
- `Assets/Volleyball/Match/Tests/EditMode/DecisionSnapshotV1Tests.cs`
- `Assets/Volleyball/Match/Tests/PlayMode/TrainingTimelinePlayModeTests.cs`

实现：

- recorder 只订阅 `ReplayDecisionPlanned`、`ReplayContactAccepted`、authority receipts、
  net/ground/crossing、defense attempt 和 rally resolved 事件。
- `DecisionSnapshotV1` 合并事件当时可见状态、合法候选、当前选择、scenario hash、规则/特征版本
  和 source sequence。
- 不读取 hidden final route、未来 trajectory sample、最终物理结果或运行结束后的反推值。
- snapshot 按稳定字段顺序和候选 ID 排序；同输入生成相同 hash。
- exporter 只存在于 Editor assembly，固定写入被忽略的
  `TestResults/TrainingLab/DecisionSnapshots/`；Player 不写数据集文件。
- observer 的订阅、导出开关和 UI 展示不得改变正式运行结果、事件数量或顺序。

绿测后提交：`feat: record training decisions without changing authority`

## 8. 阶段 F：一体化指挥台与训练生命周期

新增：

- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabController.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabView.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioDraftStoreV1.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingSimulationControllerV1.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uxml`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLab.uss`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabPanelSettings.asset`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/TrainingScenarioLabRuntimeTheme.tss`
- `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabControllerTests.cs`

界面：

- 左：项目内置情景和当前会话内存草稿；新建、复制、重置。
- 中：正式 3D 球场和轨迹预览；pointer raycast 拖放球/球员。
- 右：对象属性、semantic recipe、last actor、战术、`matchSeed`、派生只读规则状态及 issue 列表。
- 底：校验、运行、暂停、固定步单步、重置、时间线和运行对比摘要。

交互约束：

- UI Toolkit pointer/key events 完成首期键鼠交互，不为开发工具新增 Input System assembly 依赖。
- Editing/Ready 使用独立 preview root：场地、12 个可选球员标记、球标记和由现有
  `BallIntegrator` 计算的只读轨迹预览。preview 不注册 contact source，也不持有 Director。
- 所有编辑命令只修改 `TrainingScenarioDraftV1`；preview 对象是草稿投影，不是运行权威。
- 点击运行必须重新校验并冻结 `TrainingScenarioV1`。
- 进入 Running 时销毁 preview root，并由正式 6v6 composition root 创建唯一 runtime root；返回
  Editing 时销毁整个 runtime root，再从未改变的草稿重建 preview。
- `Running` 和 `Paused` 禁用所有编辑命令；返回编辑销毁运行实例并从草稿重建。
- 暂停将 `Time.timeScale` 置零；单步只调用唯一 `SimulatedBall.AdvanceSimulation` 一个
  `SimulatedBall.DefaultFixedStep`，让现有 `SimulationStepped` 驱动 player/Director 生命周期。
  不改变 AI wall-clock 决策内容，离开场景时恢复原始全局 time scale。
- validation issue 点击后同时聚焦球场对象和属性路径。
- 首版不导入、不跨会话保存草稿、不支持玩家权限页面或手柄编辑。

阶段提交：`feat: add visual formal training lab`

## 9. 阶段 G：场景、构建列表与 UI/输入验收

新增：

- `Assets/Volleyball/Match/Editor/TrainingScenarioLabSceneBuilder.cs`
- `Assets/Volleyball/Match/Scenes/FormalTrainingScenarioLab.unity`
- `Assets/Volleyball/Match/Tests/EditMode/TrainingScenarioLabSceneTests.cs`
- `Assets/Volleyball/Match/Tests/PlayMode/TrainingScenarioLabPlayModeTests.cs`
- `Assets/Volleyball/Match/Editor/TrainingScenarioLabWindowsDevelopmentBuild.cs`

修改：

- `ProjectSettings/EditorBuildSettings.asset`

场景生成：

- Editor builder 创建空 scene、训练根、`UIDocument`、Match-owned panel/theme、实验室 controller、
  相机和启动组件。
- builder 以确定顺序把训练场景加入构建列表，但不删除或重排既有 Career/Match 场景。
- scene、PanelSettings、UXML/USS 和资源引用必须由 EditMode 测试打开验证。
- Unity 生成的 `.meta` 必须保留；`InitTestScene*` 必须删除。

PlayMode：

1. 场景加载后恰好一个训练 controller、一个 preview root，且没有 active formal Director。
2. preview 恰好显示 Context 中的 12 个稳定 ID 和一个球标记。
3. 默认处于 Editing；无效草稿禁用运行。
4. 合法 preset 可进入 Ready/Running；Running 中恰好一个正式 ball、12 名正式 player 和一个
   formal Director，preview 已销毁。
5. Running/Paused 编辑控件锁定，Reset 销毁 runtime 并从原草稿重建 preview。
6. 暂停不推进 simulation time；单步只推进一个固定步。
7. 同 seed 重跑证据一致。
8. Editor exporter 不进入 Player/runtime assembly。

Windows build entry：

- 固定 `StandaloneWindows64`、IL2CPP、Development；
- 场景列表以训练实验室为启动场景并包含 `FormalIndoor6v6`；
- 检查 `BuildReport`，失败返回非零；
- 不修改 Packages、输入设置或现有 Career build entry。

阶段提交：`build: add formal training lab scene`

## 10. 阶段 H：冻结后验证、复核与交付

### 自动验证顺序

1. focused EditMode：
   - `TrainingRallyStartV3Tests`
   - `TrainingScenarioV1Tests`
   - `TrainingScenarioValidatorV1Tests`
   - `DecisionSnapshotV1Tests`
   - `TrainingScenarioLabControllerTests`
   - `TrainingScenarioLabSceneTests`
2. 受影响 Match EditMode：
   - `RallyTouchStateTests`
   - `FullRallyV3RulesEngineTests`
   - `FullRallyV3RuntimeAdapterTests`
   - `FormalMatchScenarioPresetV4Tests`
   - `MatchReplayV4Tests`
3. 指定 PlayMode：
   - `TrainingScenarioRuntimePlayModeTests`
   - `TrainingNetContinuationPlayModeTests`
   - `TrainingTimelinePlayModeTests`
   - `TrainingScenarioLabPlayModeTests`
   - `DefensiveContactContinuationPlayModeTests`
4. 正式 6v6 一条整局 smoke。
5. 完整 EditMode 只在代码冻结后运行一次。
6. Repository validator、模块依赖静态扫描和 `git diff --check`。

不默认运行完整 PlayMode；只有指定情景或正式 smoke 显示跨场景回归时才扩大。

### 视觉和输入验收

在 Unity Editor 以 `1920×1080` 检查：

- 一体化四区布局无遮挡；
- 12 名球员和球可选中、拖放并实时反映属性；
- 非法位置、重叠、非有限输入和 recipe 冲突清晰定位；
- Running/Paused 编辑锁定；
- pause、step、reset、同 seed rerun 和 timeline 正确；
- 中文系统字体可读，长 issue 文本不会溢出。

### 独立复核

本任务跨越 canonical hash、规则起点、生命周期和正式运行行为，应在实现冻结后进行一次独立全面
复核。由于当前线程规则禁止未获明确授权时创建 subagent，冻结时先请求用户授权；若用户不授权，
记录未执行，不以本地自审冒充独立复核。

复核只检查：

- UI 是否能绕过 semantic factory；
- V3/compatibility state 是否一致；
- post-block 是否存在第二 writer；
- observer 是否改变正式行为；
- old scenario/replay/hash 兼容；
- tests 是否确实使用真实物理。

高优先级发现集中修复后只做一次针对性复审和相关回归。

### Windows

运行新 build entry，保留 manifest、BuildReport 摘要和 Player log。实机检查：

- 训练场景启动；
- 鼠标选择/拖放与键盘 pause/step/reset；
- 校验错误定位；
- 全 AI 情景完成；
- Player log 无 Bootstrap、输入、图形或崩溃异常。

正式 6v6 的兼容性由构建内容检查、指定 PlayMode 和整局 smoke 证明；本里程碑不为训练实验室新增
跳转正式比赛的开发者菜单。

### 文档和交付

新增或更新：

- `docs/development.md`：打开实验室、focused 测试、快照输出和 Windows build 命令。
- `docs/changes/2026-07-31-001-formal-training-scenario-lab.md`
- `docs/changes/README.md`
- 当前 active handoff 的验证状态。

只有自动验证和 Windows 人工验收全部通过后，才将 handoff 移到 completed 并标记 `completed`。

最终检查：

```bash
python3 tools/validate_repository.py
rg -n "Volleyball\\.Career|Career\\." \
  Assets/Volleyball/Match/Runtime \
  Assets/Volleyball/Match/Editor
git diff --check
git status --short
```

确认没有 `Assets/InitTestScene*.unity`、对应 `.meta`、`TestResults/`、Builds、Library 或其他临时文件
进入提交。

## 11. 提交与 PR

建议提交：

1. `feat: add semantic formal rally starts`
2. `feat: add formal training scenario contracts`
3. `feat: start formal rallies from training scenarios`
4. `test: add formal net training scenarios`
5. `feat: record training decisions without changing authority`
6. `feat: add visual formal training lab`
7. `build: add formal training lab scene`
8. `docs: record formal training scenario lab`

同一批共享文件只由当前实现者顺序修改。重要检查点推送功能分支。因为本任务修改场景、构建列表和
公共 Match 运行生命周期，最终必须通过 Pull Request；推送不代表合并，未经明确授权不合并 main。

## 12. 降级与停止条件

允许延期：

- 决策快照的非必要展示字段；
- timeline 的非权威装饰信息；
- UI 动画和非关键快捷键；
- 除五个触网情景外的额外训练模板。

不得降级：

- semantic factory；
- 12 人/球/规则合法性校验；
- 真实几何接触和裁判；
- 运行后编辑锁定；
- third-touch 与 post-block 排他性；
- old scenario/hash/replay 兼容；
- fixed-seed 双跑；
- Windows build 与键鼠验收。

出现以下任一情况立即停止当前阶段并报告：

- 必须直接提交 contact、写比分或修改裁判结果才能构造情景；
- 必须复制 Gate H/I planner 或使用反射写 Director 私有字段；
- observer 开关改变正式结果；
- old scenario content hash 或 Replay V4 bytes 变化；
- 场景/输入修改导致正式 6v6 smoke 回归；
- 同平台同输入双跑不一致。
