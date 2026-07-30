# V4 防守接触情景预设与延续验证实施计划

> **前置规格：** `docs/superpowers/specs/2026-07-29-v4-defensive-contact-scenario-design.md`
>
> **风险等级：** 高风险。工作涉及 Match 正式生命周期、固定种子确定性与可能的 Shared replay 合同。所有实现留在独立功能分支；若 Shared 或公共契约有修改，创建 PR，未经明确授权不合并 `main`。

## 0. 实施基线与边界

当前 `main` 的 `5196363` 已经实现以下内容，实施时必须保留并以测试验证，而不是重写：

- `AttackDefenseAuthorityController` 已把 `FloorDefense` 与 `AttackCover` 作为 Gate I `Receive` 接触，排程到 `PrototypePlayerAgent.ScheduleContact`。
- `PhysicalMatchRallyDirector` 已有 `PostAttackContinuationStateV4`、pending/active Gate I Receive 窗口、post-block side handling 与 defense-attempt 事件。
- `SimulatedBall.PlayerContactHitComparer` 已按 hit fraction、contact group、action、actor、surface 与物理快照进行稳定排序。
- `MatchReplayV4` 已有 `ReplayDefenseAttemptRecordV4`；recorder 和 HTML 已能序列化及展示其记录。

本阶段的主要修复对象是：可信的完整开局输入、预设归因、正式生命周期验证，以及现有实现中被这些验证暴露出的 continuation/窗口/回放边界缺口。禁止为达到测试预期注入中途球状态、调用 V3 commit、强制窗口开启或改写裁判结果。

## 1. 建立完整固化预设合同与适配层

### 文件

- 新增 `Assets/Volleyball/Match/Runtime/Presentation/FormalMatchScenarioPresetV4.cs`
- 新增 `Assets/Volleyball/Match/Runtime/Presentation/FormalMatchScenarioBootstrapperV4.cs`
- 修改 `Assets/Volleyball/Match/Runtime/Domain/MatchSet.cs`
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/FormalSixVsSixRallyBootstrap.cs`
- 新增 `Assets/Volleyball/Match/Runtime/Presentation/FormalMatchScenarioDefinitionV4.cs`，承载可测试的非 Unity 数据转换、canonical hash 与验证。
- 新增 `Assets/Volleyball/Match/Tests/EditMode/FormalMatchScenarioPresetV4Tests.cs`

### 设计与实现

1. 定义 `FormalMatchScenarioPresetV4 : ScriptableObject`，作为正式 6v6 开局的完整、不可隐式回退输入。它用 Unity 可序列化 DTO 保存以下输入；运行时 adapter 才将 DTO 还原为 `TeamSnapshotV4` 和其他不可变合同对象：
   - scenario ID、格式版本、V3 rules version、确定性 seed、内容 hash；
   - 完整双方 `TeamSnapshotV4`、阵容顺序和位置；
   - first serving side、初始 rotation/站位；
   - 物理/场地配置身份；
   - 双方战术/AI 配置身份及完整输入。
2. 把实际转换和验证放在普通 C# definition/adapter 中；`ScriptableObject` 只负责持有序列化的 DTO，不能直接依赖 Unity 对只读 V4 合同属性的序列化行为。实现 canonical 内容 hash，顺序固定，不用 Unity object ID、资产路径或编辑时间。
3. 在 `MatchSet` 增加明确的、仅初始化时可用的 initial rotation offset 输入（每侧 0--5）。它必须先旋转稳定的 rotation order，再设置 first serving side；不能通过模拟得分或回合改变初始站位。适配层在创建球、队员和 `FormalSixVsSixRallyDirector` 之前验证预设，并生成 `MatchContextV4`、first serving side、rotation offsets 和 AI 输入。无效预设应在比赛开局前抛出明确定义的验证错误；绝不改用 `CreateSandboxContext()` 或默认种子继续运行。
4. 让 `FormalSixVsSixRallyBootstrap` 保留默认 `FormalIndoor6v6` 行为，但可以通过显式 `InitializeFromScenario(FormalMatchScenarioPresetV4)` 或等价 API 使用适配层。不要把测试名称、seed 分支或预期结果塞进 `Awake()`。
5. 确认配置对象当前只表达固定的 `FormalIndoorSixVsSix`；如果预设需要场地/物理参数，先用版本化配置 identity 验证它等于该正式配置。除非现有配置确实不能表示所需正式输入，不扩展为可任意修改场地/物理的测试开关。

### 先写的 EditMode 测试

- 相同完整输入得到相同 scenario content hash 和 `MatchContextV4`；改变任何已声明输入得到不同 hash。
- 预设完整保留玩家快照、side、顺序、seed、first serve、rotation offsets 与规则/配置版本；转换不读取默认场景数据。
- 非法规则版本、重复 player ID、非 6 人 roster、错误 side、非法 rotation、空战术配置或 hash 不匹配在启动前拒绝。
- 默认 bootstrap 不带预设仍生成当前的默认正式输入；显式预设启动不调用默认 context builder。

### 检查点

运行新增测试及 `FullRallyV3RuntimeAdapterTests`、相关 context/contract tests。仅在这些通过后提交：

`feat: add immutable formal match scenarios`

## 2. 将预设来源接入 replay 和运行时诊断

### 文件

- 修改 `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayHtmlWriter.cs`
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- 修改 `Assets/Volleyball/Shared/Tests/EditMode/MatchContractTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`

### 设计与实现

1. 在 canonical replay 顶层或明确的 metadata record 中增加 scenario identity、scenario format version 与 scenario content hash。它们必须来自正式启动适配层，不允许 recorder 从场景名、环境变量或测试名称猜测。
2. 保持无预设运行的兼容：用显式稳定默认值表示“默认正式输入”，而非省略字段导致序列化分歧。定义旧 V4 payload 的读取规则及 hash compatibility 路径。
3. 在 `PhysicalMatchRallyDirector` 暴露只读 scenario provenance，供 recorder 捕捉；不得使 director 根据预设名称改变任何比赛决策。
4. HTML 标明 scenario ID/hash，并继续只渲染 canonical payload。不要将 runtime diagnostics 或 wall-clock 放入 HTML。
5. 当现有 `ReplayDefenseAttemptRecordV4` 已能表达某项事实时，继续复用它。若新测试证明缺少“相同 fixed-step 内落选原因/赢家”或“post-block actual side evidence”，以最小字段扩展 record；同时更新严格 JSON property count、排序、canonical payload、legacy hash、parse、round-trip fixtures 和 HTML。

### 先写的 EditMode 测试

- scenario replay JSON round-trip 后 provenance 完整保留，canonical hash 稳定。
- 旧无 scenario 字段的 replay 仍可读取，重序列化的兼容行为明确且固定。
- 改变 scenario hash、defense attempt 或 accepted contact 都改变 canonical hash。
- HTML 只包含来自 replay 的 scenario/attempt 证据，重复 render 输出字节一致。

### 检查点

运行 `MatchContractTests`、`MatchReplayV4Tests` 及 Shared EditMode；检查 `git diff --check`。若 Shared 文件有改动，该提交必须走 PR：

`feat: record formal scenario provenance in replay`

## 3. 创建正式情景资产与无注入 PlayMode 观察工具

### 文件

- 新增 `Assets/Volleyball/Match/Tests/Resources/FormalMatchScenariosV4/ReachableFloorDefense.asset`
- 新增 `Assets/Volleyball/Match/Tests/Resources/FormalMatchScenariosV4/LateFloorDefense.asset`
- 新增 `Assets/Volleyball/Match/Tests/Resources/FormalMatchScenariosV4/AttackSideBlockRebound.asset`
- 新增 `Assets/Volleyball/Match/Tests/Resources/FormalMatchScenariosV4/BlockingSideBlockRebound.asset`
- 新增 `Assets/Volleyball/Match/Tests/Resources/FormalMatchScenariosV4/PostBlockMiss.asset`
- 新增 `Assets/Volleyball/Match/Tests/Resources/FormalMatchScenariosV4/OverlappingDefenders.asset`
- 修改 `Assets/Volleyball/Match/Tests/PlayMode/DefensiveContactContinuationPlayModeTests.cs`
- 必要时新增 `Assets/Volleyball/Match/Tests/PlayMode/FormalMatchScenarioProbeV4.cs`

### 设计与实现

1. 每个 `.asset` 只保存第 1 阶段的正式输入。由预设内完整快照决定可重复的攻防倾向；它不保存球的中途状态、接触者、结果或时间线。
2. 重写现有 `LoadFormalProbe`：加载正常 `FormalIndoor6v6` 场景后，以公开的 scenario bootstrap API 启动一个正式 director。它只订阅 `SimulatedBall`、director 和 recorder 的真实事件。
3. 删除/替换依赖 `Time.timeScale = 8`、`Time.realtimeSinceStartup`、全回合“任意 Receive”计数的弱断言。超时使用 PlayMode yield 的统一安全上限；断言只关联同一 attack/block group、Gate I receipt、attempt identity 与 subsequent physical event。
4. probe 应捕捉：scenario provenance、Gate I receipt、attempt/window event、fixed-step ball samples、net crossing、ground event、physical player contact、V3 transition、replay event 和 result。probe 不得调 `AdvanceGateI...`、`CommitContact`、`AcceptContact`、`Launch` 或改球状态。
5. 为每个情景连续跑两次并导出 canonical artifacts 到临时 `TestResults` 路径；运行日志和产物不提交。

### 先写的 PlayMode 测试

1. `CommittedFloorDefense_DigsReachableSpike`：同一 receipt 对应的 Gate I `FloorDefense` 在真实 crossing 后出现 actual `Receive`；物理 contact、V3 accepted transition、replay accepted attempt 和 counted-touch delta 完全一致。
2. `LateFloorDefense_DoesNotCreateMagicDig`：窗口外/不可达球员不产生该 attempt 的 `Receive`，并出现 rejected/expired attempt、ground event 与唯一 result。
3. `BlockReboundToAttackingSide_AllowsAttackCoverage`：block 后的实际 crossing/velocity 证据解析为进攻方；同一个已声明 `AttackCover` receipt 产生新的 Receive；block delta 为 0，Receive delta 为 1。
4. `BlockReboundToDefendingSide_AllowsBlockRecovery`：block 后实际留在拦网方；已声明 recovery/FloorDefense 接触；blocker 可成为首个 counted-touch actor。
5. `PostBlockMiss_StillLetsGroundRefereeScore`：post-block 没有 accepted Receive，ground/referee 只结算一次，Director 没有覆盖结果。
6. `OverlappingDefenders_AcceptOnlyOneReceive`：同 fixed step 的多个合法表面只接受一个可解释的稳定赢家，V3/replay 各只记录一次。

首次引入时允许这些测试红灯，但失败信息必须证明“授权已发生、实际交叉/落地发生、缺的是物理候选或正确 continuation”，而不是“固定 seed 恰好没打出预期”。

### 检查点

运行这个 fixture 两次，确认每一个红/绿状态和输入 artifact 稳定。提交：

`test: add formal defensive continuation scenarios`

## 4. 基于情景测试补正 Receive 窗口和 continuation 生命周期

### 文件

- 修改 `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/AttackDefenseAuthorityController.cs`（仅在预检/取消语义未保持时）
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs` 或 `PlayerActionTimeline.cs`（仅在 window 排程证据显示生命周期不足时）
- 修改 `Assets/Volleyball/Match/Runtime/AI/AttackDefenseAuthorityCoordinator.cs`（仅为已声明的 coverage/recovery actor 补发布接口时）
- 修改 `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityControllerTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/EditMode/AttackDefenseAuthorityCoordinatorTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/EditMode/GateKDirectorBoundaryTests.cs`

### 设计与实现

1. 审查 `PublishGateIBatch`、`OpenPendingGateIReceiveWindow`、`ActivatePostBlockReceive`、`BeginPossession` 与 accepted/ground cleanup 的时序。确保 Gate I 已提交 `FloorDefense`、`AttackCover`、恢复职责先进入 pending evidence，随后只在实际 continuation side 变为 active window；不会在 planning 时提前开放。
2. 对普通 attack：accepted attack 进入 `AwaitingAttackCrossing`；合法 crossing 后仅在 receiving team 激活 pending FloorDefense。保留已 committed receive scheduling，不让 possession 初始化无条件清除它。
3. 对 block：保存实际 impact center、outgoing velocity、block team/actor/contact group/time。优先按同 fixed step crossing，其次 impact side、严格 Z velocity、最后离开未决走廊推进状态；未决时双方都不开窗口。
4. block 回到攻击方时，从 `AttackCoverageResponsibilities` 选择已声明 actor；留在拦网方时只选择已声明 FloorDefense/BlockRecovery actor。删除/收窄任何“last toucher”或全队回退使未授权球员凭空具备 receive 的分支。
5. 接触、ground、rally reset 关闭同一 attempt 的所有 active window；所有未发生的窗口发出一次且仅一次 expired record。被 V3 或 action window 拒绝的实际几何命中发出 rejection record，但不改写 referee path。
6. 把 V3 当作已经发生接触的判定账本：不要将 continuation 侧别、crossing 或 ground 判定移进 V3。

### 聚焦 EditMode 测试

- committed `FloorDefense` 同时拥有移动与 physical Receive 排程；尚未到窗口、已过期、不可达或被取消时不会提供候选。
- receipt revision/source/artifact 不匹配时 batch 原子失败，且不破坏之前已 committed contact。
- 普通 crossing、attack-side rebound、blocking-side rebound、pending corridor、ground-first 各只进行允许的状态转换。
- 没有显式 declared actor 的 attack cover/recovery 不开放窗口；已声明 blocker 后续 Receive 合法。
- accepted contact、ground 和 reset 都不遗留 active/pending attempt。

### 检查点

先跑第 3 阶段六个 PlayMode 情景，再跑上述 EditMode。只修复由正式场景稳定重现的行为，不扩大为全局 AI 重构。提交：

`fix: resolve physical defensive continuation windows`

## 5. 验证和收紧真实几何候选的确定性

### 文件

- 修改 `Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs`（仅当第 3 阶段发现排序键仍不足）
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/PlayerContactSurfaceProvider.cs` 或相关 surface 文件（仅当 stable surface index 未真正稳定）
- 修改 `Assets/Volleyball/Match/Tests/EditMode/SimulatedBallTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/EditMode/SweptBallCollisionTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/PlayMode/DefensiveContactContinuationPlayModeTests.cs`

### 设计与实现

1. 以现有 `PlayerContactHitComparer` 为起点验证全部排序字段来自不可变比赛数据。若 `ComparePhysicalEvent` 的最后平局仍可返回 0，定义“同一物理事件”的可观测淘汰记录；不得让 `List.Sort` 的保留顺序决定结果。
2. 固定 surface identity/index 的产生位置，使其不依赖 contact source 注册顺序、Unity hierarchy 顺序、instance ID 或 HashSet/Dictionary 遍历。禁止 epsilon 合并相近 time fraction。
3. 每个 physical contact group 只提交一次：winner 才能改变球速、触发 V3 或写 accepted replay；lossers 只能产生 diagnostic evidence。
4. 保持角色 capsule 不进入 Receive candidate；若 capsule 用于其他动作，不能借此绕过 Receive surface 限制。

### 聚焦 EditMode 测试

- 反转 contact source 注册顺序，winner 与 replay winner 完全不变。
- 相同 time fraction、不同 group/action/team/roster/role/surface 的排序符合精确键。
- 完全相同物理 snapshot 只提交一个 contact group；其余不改球速、不提交 V3。
- 真正较晚 hit 从不击败较早 hit；没有 epsilon 造成的合并。

### 检查点

运行 `SimulatedBallTests`、`SweptBallCollisionTests` 和重叠防守情景。若无需代码修改，以测试提交记录已验证的既有行为：

`test: lock deterministic defensive contact selection`

## 6. 补齐跨层一致性与完整回归

### 文件

- 修改 `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3RulesEngineTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/EditMode/FullRallyV3EligibilityTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/EditMode/MatchRallyRefereeTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`
- 修改 `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixRallyPlayModeTests.cs`
- 修改 `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`（仅在公共 replay/director 改动影响它时）

### 设计与实现

1. 为成功情景交叉断言：accepted physical Receive 数、accepted V3 counted-touch transition 数、result/stat receive 数及 replay accepted receive 数一致。Block 物理接触为 1，但 counted-touch delta 为 0；首个 post-block Receive 为 1。
2. 为 miss 情景断言：accepted Receive 为 0、ground referee resolve 恰好一次、replay 同时有 attempt expiration/rejection、ground 与 result。
3. 让 `FormalSixVsSixReplayPlayModeTests` 使用至少一个预设的两次独立正式 run，对比 canonical replay JSON、HTML/report、result、accepted-contact sequence、defense-attempt diagnostics 和 hash。仅比较 canonical artifact；不删除证据以迎合字节对比。
4. 继续验证无 scenario 的现有 3v3/6v6 正式流程，防止 provenance 或 continuation 变更引入回归。

### 最终验证命令

在代码冻结后按顺序运行，并将新鲜产物写入 `TestResults/V4-DefensiveContact-*`：

```bash
UNITY="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/wys/Documents/program/volleyball-match"

"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.FormalMatchScenarioPresetV4Tests;Volleyball.EditModeTests.AttackDefenseAuthorityControllerTests;Volleyball.EditModeTests.AttackDefenseAuthorityCoordinatorTests;Volleyball.EditModeTests.FullRallyV3RulesEngineTests;Volleyball.EditModeTests.SimulatedBallTests;Volleyball.EditModeTests.SweptBallCollisionTests;Volleyball.EditModeTests.MatchRallyRefereeTests;Volleyball.EditModeTests.MatchReplayV4Tests" \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-Focused-EditMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-Focused-EditMode.log"

"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform PlayMode \
  -testFilter "Volleyball.PlayModeTests.DefensiveContactContinuationPlayModeTests;Volleyball.PlayModeTests.FormalSixVsSixReplayPlayModeTests" \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-Focused-PlayMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-Focused-PlayMode.log"

"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-All-EditMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-All-EditMode.log"

"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform PlayMode \
  -testResults "$PROJECT/TestResults/V4-DefensiveContact-All-PlayMode.xml" \
  -logFile "$PROJECT/TestResults/V4-DefensiveContact-All-PlayMode.log"

git diff --check
rg -n "error CS|Unhandled Exception|AssertionException|UnityException|FAIL|Failed" \
  "$PROJECT"/TestResults/V4-DefensiveContact-*.log
```

运行后人工检查最后一条搜索命中的测试框架汇总，确认没有新的 Unity Console error。开发期间忽略 `Assets/InitTestScene*.unity` 和同名 `.meta`；合并分支前删除它们。

## 7. 冻结、复核与交付

1. 在所有修复提交之后，检查最终 diff 只包含预设、Match/Shared 合同、验证和必要文档。
2. 对 Shared、Bootstrap 或公共运行时修改创建 PR；不直接合并 `main`。
3. 进行一次独立 Agent 全面复核，重点检查：中途注入、V3 越权、ground referee 被覆盖、非确定性排序、旧 replay 兼容性和测试仅证明“发生过某个 Receive”。
4. 若有高优先级问题，集中修复后执行一次针对性复审和受影响测试；不要无限重复审查。
5. 只有全部自动验证完成、必要的正式 PlayMode 观察完成、临时 Unity 场景文件清理完毕后，状态才能是“完成”。此前仅可称“已实现”或“已自动验证”。

## 8. 补齐发球触网后的接发延续

### 文件

- 修改 `Assets/Volleyball/Match/Runtime/Presentation/FormalMatchScenarioDefinitionV4.cs`
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/FormalMatchScenarioCatalogV4.cs`
- 修改 `Assets/Volleyball/Match/Editor/FormalMatchScenarioAssetCreatorV4.cs`
- 修改 `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
- 修改 `Assets/Volleyball/Match/Tests/EditMode/FormalMatchScenarioPresetV4Tests.cs`
- 修改 `Assets/Volleyball/Match/Tests/PlayMode/DefensiveContactContinuationPlayModeTests.cs`
- 新增 `Assets/Volleyball/Match/Tests/Resources/FormalMatchScenariosV4/ServeNetDeflection.asset`

### 红测

1. 新增完整启动预设，使初次 Serve 通过真实 `EnvironmentCollision.TryNet` 触网后仍朝接发方运动；预设只控制开局发球飞行参数，不在比赛中途改球。
2. `ServeNetDeflection_ReplansAndReceivesActualBall` 必须观察到 Net environment contact、旧 Gate H revision 失效、新 revision/source sequence、合法 crossing 和新 receipt 对应的真实 Receive。
3. `ServeNetDeflection_MissStillLetsGroundRefereeScore` 使用不可达接发输入，断言零 accepted Receive、唯一 ground 和唯一 result。
4. EditMode 固定预设的版本、canonical hash 和非法发球飞行参数拒绝；旧预设必须显式迁移，不能依赖新增字段的隐式 Unity 默认值。

### 实现

1. 把初次发球 flight seconds 作为完整情景开局输入加入情景 canonical payload；默认正式场景仍使用 `0.90s`。该字段只供完整情景预设选择开局，不修改 `MatchContextV4`。
2. `HandleEnvironmentContact(Net)` 仅在最后规则触球为 Serve、当前 possession 为接发方且球仍朝接发方运动时处理：
   - 递增异步请求版本并取消旧 AI 请求；
   - 取消旧 Primary/Emergency Receive 排程并清空旧 Gate H receipt；
   - 从触网后的真实 state 重新预测 ground landing 和剩余时间；
   - 产生新的 Gate H plan revision/source sequence；
   - 不调用 `BeginPossession`，不更新 V3，不增加统计。
3. 新计划的移动可以立即提交；physical Receive 只有在后续合法 net crossing 后才能成为候选。若现有按时间窗口和接发方几何已经严格保证这一点，测试必须以 Net contact、crossing、contact simulation time 的顺序证明；否则增加显式 pending activation。
4. 球触网后弹回发球队、剩余时间非法或无可达候选时，不创建新窗口；ground/out-of-bounds/crossing referee 保持唯一权威。
5. 不新增 Shared replay 字段。用现有 net crossing、Gate H receipt、physical/replay contact、ground/result 和测试探针证明因果链。

### 验证

先运行新增 EditMode/PlayMode 红绿，再运行：

- `DefensiveContactContinuationPlayModeTests`
- `FormalSixVsSixRallyPlayModeTests`
- `FormalSixVsSixReplayPlayModeTests`
- 完整 EditMode
- 完整 PlayMode

代码冻结后执行一次独立全面复核；任何影响时序、规则或确定性的发现修复后，重跑受影响 focused 套件和一轮新的完整套件。

## 验收矩阵

| 场景 | 物理 | V3 | Replay | Referee |
| --- | --- | --- | --- | --- |
| 可达 FloorDefense | Receive surface swept hit | counted touch 增加 1 | accepted attempt 与 receipt 可关联 | 无提前 ground result |
| 过晚/不可达 | 无 Receive hit | 无新 transition | rejected/expired | ground 仅一次结算 |
| 回到进攻方的 block | AttackCover hit | block 0，Receive 1 | side resolution + accepted | 无覆盖 |
| 留在拦网方的 block | recovery/FloorDefense hit | blocker 可做 Receive 1 | side resolution + accepted | 无覆盖 |
| post-block miss | 无 Receive hit | 无 post-block receive | expiration/rejection | 结果唯一 |
| 重叠表面 | 一个稳定 winner | 一个 transition | 一个 accepted，其余 loser evidence | 不受影响 |
| 发球触网后可达 | 新 Gate H Receive surface swept hit | Serve 后首个 counted touch 为 1 | 新 receipt 与 accepted contact 可关联 | 触网不提前终局 |
| 发球触网后漏接 | 无 Receive hit | 无新增 transition | 旧 receipt 失效且无伪造 accepted | ground 仅一次结算 |
| 发球触网后弹回 | 接发方无 candidate | 无接发 transition | 无接发 accepted | 发球队侧 ground/out-of-bounds 权威结算 |
