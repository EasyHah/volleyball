# CHG-20260723-001：Full Rally V3 架构与 Shared 契约预留

- 日期：2026-07-23
- 状态：Phase 0 接口与 Phase 1 事实、资格、规则及正式 6v6 权威闸门已完成
- 负责人：Shared / Match
- 影响模块：Shared / Match / Career / Docs
- 交互级别：跨模块（重点）
- 关联分支：`geometric-counterplay`（设计稿）/ `codex/full-rally-v3-phase-1`
- 关联提交或 PR：本地提交（见当前 commit）

> 后续阶段路线图：`docs/superpowers/plans/2026-07-24-full-rally-v3-phase-2-9-roadmap.md`。它将
> Phase 2--9 拆为可独立验收的 envelope/影子规划/组件/组织/攻防/感知/导演瘦身/回放校准闸门；
> V4 不是本期依赖。

> [!IMPORTANT]
> Full Rally V3 将新增独立的 V3 比赛上下文、结果、球员能力快照与 Replay V2 契约。
> Shared 提供版本显式的 DTO、JSON、哈希和迁移边界；Match 消费这些契约实现回合规划与回放；
> Career 在创建比赛、读取结果和升级旧存档时必须显式选择版本，不能把 V1/V2/V3 静默互当。

## 改动摘要

Full Rally V3 把排球回合从固定 `Receive -> Organize -> Attack` 链路升级为十二名场上球员的
责任计划、规则转换、执行 envelope、攻击防守博弈和回放诊断共同驱动的架构。设计稿已补齐
Phase 0 接口闸门：V3 契约族、canonical hash 拆分、V2->V3 能力迁移约束、contact coverage
decision、deterministic work budget、Replay V2 方向、shared execution envelope 和 shared
trajectory prediction contract。

本记录声明 Phase 0 接口、测试夹具和跨模块协作边界，以及 Phase 1 的世界事实、场上资格、
实际触球转换、动作资格、边界规则、shadow 比较与正式 6v6 权威闸门已落地。不表示 Full Rally
V3 planner、cache、完整 replay payload 或平衡公式已实现。GPU backend 不进入当前 Phase 0/P1；
第一版以 CPU deterministic backend 为权威。

2026-07-24 属性语义修正：`PlayerAbilitySnapshotV3` 是兼容的混合输入契约，并非已经完全接入的
“比赛属性层”。正式运行时仍消费 V2-shaped `PlayerAbilityProfile`；`AttackControl`、`SoftTouch`、
`BlockTechnique`、`CourtAwareness` 当前为 reserved axes，不能表述为已独立影响正式比赛。
`MaxAttackReach` 仅为进攻触球高度输入，不是身高或拦网高度。V4 将引入基础身体层、技术基础层、
派生比赛属性层及版本化确定性换算；详见
`docs/superpowers/specs/2026-07-24-v3-ability-semantics-and-v4-layered-attributes-design.md`。

SP/GM 审查结论：用户原始 P0 四条仍是核心阻塞项；GM 追加的 Replay 契约方向、hash 拆族、
deterministic work budget、migration invariants、`PlanCoverageDecision`、shared envelope 和
shared trajectory artifact 是 Phase 0 interface addenda。它们必须在接口设计前命名并预留
验证边界，但不要求在 Phase 0 前完成完整 planner、cache、replay payload 或平衡公式。

## 具体变更

- 文件、场景、资源或程序集：
  - 设计稿：`.worktrees/geometric-counterplay/docs/superpowers/specs/2026-07-23-full-rally-v3-architecture-design.md`
  - 实施计划：`docs/superpowers/plans/2026-07-23-full-rally-v3-phase-0-interfaces.md`
  - 本交接文档：`docs/changes/2026-07-23-001-full-rally-v3-architecture.md`
  - 索引：`docs/changes/README.md`
  - Phase 1 规则事实：`Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/RallyWorldSnapshotV3.cs`
  - Phase 1 场上资格：`OnCourtEligibilitySnapshot.cs`、`OnCourtLineupRulesV3.cs`
  - Phase 1 触球规则：`TouchSequenceStateV3.cs`、`RallyRulesEngineV3.cs`
  - Phase 1 动作与边界：`ActionEligibilityRulesV3.cs`、`BoundaryAndNetRulesV3.cs`
  - Phase 1 shadow：`LegacyRulesShadowComparatorV3.cs`
  - Phase 1 运行时适配：`Assets/Volleyball/Match/Runtime/Presentation/FullRallyV3RulesRuntimeAdapter.cs`
  - Phase 1 测试：`FullRallyV3WorldSnapshotTests.cs`、`FullRallyV3EligibilityTests.cs`、
    `FullRallyV3RulesEngineTests.cs`、`FullRallyV3ShadowRulesTests.cs`、
    `FullRallyV3RuntimeAdapterTests.cs`
- 新增或修改的公开类型/字段：
  - 已新增 `ContractVersions.MatchV3 = 3` 和 `ContractVersions.ReplayV2 = 2`。
  - 已新增 `PlayerAbilitySnapshotV3`、`PlayerSnapshotV3`、`TeamSnapshotV3`、`MatchContextV3`、
    `MatchResultV3`。
  - 已新增 `MatchReplayV2` shell，`formatVersion = 2`，含 reserved diagnostic sections。
  - 已新增 canonical hash family 的 Phase 0 接口：`CanonicalMatchContextHashV3`、
    `CanonicalMatchResultHashV3`；`MatchReplayV2` 当前以 `contextHash` 锚定，详细 replay/frame
    hash payload 留到 Replay V2 schema 阶段。
  - 已新增版本显式 `ContractJson` V3 context/result 与 Replay V2 API，并通过测试拒绝 V2/V3
    静默互读。
  - 已新增 `LegacyV2ToPlayerAbilitySnapshotV3`，记录 source/migration/provenance 和
    compatibility estimate 状态。
  - 已新增 Match 侧 Phase 0 占位契约：`ExecutionEnvelopeV3`、`PlanCoverageDecision`、
    `DeterministicWorkBudgetV3`、`BallTrajectoryArtifactV3`。
  - 已新增 `RallyWorldSnapshotV3`、`OnCourtEligibilitySnapshot`、`OnCourtLineupRulesV3`、
    `TouchSequenceStateV3`、`RallyRulesEngineV3`、`ActionEligibilityRulesV3`、
    `BoundaryAndNetRulesV3`、`LegacyRulesShadowComparatorV3`。
  - 已新增 `FullRallyV3RulesRuntimeAdapter.EvaluateContact` / `CommitContact` 纯查询与提交边界。
  - `SimulatedBall.SelectedContactCommitter` 只在选出最早且未忽略的真实碰撞后调用一次，并在任何
    接触响应前返回权威结果。
- 行为变化：
  - 每次 accepted contact 先推进 rules transition，再 deterministic 判断 covered branch、
    local revision、scoped replan、global replan 或 terminal no-plan。
  - fallback 行为由 deterministic work-unit caps 决定，wall-clock budget 只作为 profiling target。
  - planner 与 executor 必须共享同一份 execution envelope；两队 gate-5 必须读取同一份
    authoritative trajectory prediction artifact。
  - V2 能力迁移只能作为 compatibility estimate，并记录 provenance 与 collapsed axes 限制。
  - 正式 6v6 显式配置 `V3RulesMode.Authority`；碰撞候选筛选只查询 V3，选中碰撞只提交一次，
    旧规则仅保留兼容状态和诊断比较，不能否决已由 V3 接受的物理事件。
  - 旧版 3v3 继续使用 `V3RulesMode.Disabled` 和空 V3 adapter，行为边界不变。
  - V3 拒绝在球速响应前映射为 fault/ignore；计数、比较、回放接触和规则转换按已提交接受触球
    各发生一次。

## 跨模块交互重点

- 接口提供方：Shared
- 接口使用方：Match；Career 在比赛创建、结果读取、存档升级时显式接入
- 数据方向：`Career -> Shared MatchContextV3 -> Match -> Shared MatchResultV3/MatchReplayV2 -> Career`
- 兼容性：计划兼容旧版本，但不允许静默互换。V1/V2 保留显式读写；V1 默认经 V2 再迁移到
  V3。V2 能力通过 `LegacyV2ToPlayerAbilitySnapshotV3` 迁移为兼容估算，之后 V3 authored
  roster 必须提供显式 V3 值。
- 对方开发者需要做什么：
  - Career 侧确认是否需要批量升级历史 V2 存档，以及升级失败时的用户路径。
  - Career/Shared 调用方准备按版本显式调用 V1/V2/V3 context/result/replay API。
  - Match 侧在后续 planner 实现中消费本次已落地的 `MatchReplayV2`、
    deterministic work-budget config、envelope identity、trajectory artifact provenance。
  - 双方不要新增“自动猜版本”的 parser、adapter 或 fallback。

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [ ] 手动场景验证
- [x] 序列化、存档或迁移验证（如适用）

Phase 0 接口已完成的最低验证：

- Shared EditMode（最终验证）：`TestResults/EditMode-shared-final.xml`，30/30 passed。
- Match EditMode（最终验证）：`TestResults/EditMode-match-final.xml`，293/293 passed。
- 已覆盖 V1/V2/V3 reader/writer 拒绝静默版本替代。
- 已覆盖 `CanonicalMatchContextHashV3`、`CanonicalMatchResultHashV3` 的独立 deterministic hash。
- 已覆盖 V2->V3 能力迁移的 deterministic、provenance、bounded/invalid input 和 role-proxy
  distinguishability fixtures。
- 已覆盖 `PlanCoverageDecision` deterministic value object、`ExecutionEnvelopeV3` shared identity、
  `BallTrajectoryArtifactV3` deterministic identity。
- 已覆盖 deterministic work budget 不用 wall-clock 改变 deterministic decision。

Phase 1 最终验证使用 Unity `6000.0.43f1`：

- 全量 EditMode：`TestResults/FullRallyV3-Phase1-final-edit.xml`，433/433 passed，
  failed/inconclusive/skipped 均为 0。
- 全量 PlayMode：`TestResults/FullRallyV3-Phase1-final-play.xml`，21/21 passed，
  failed/inconclusive/skipped 均为 0，耗时 445.685 秒。
- 正式 6v6 完整单局为 25:9，50 次已提交接受触球对应 50 次 V3 transition 与 50 次
  `ReplayContactAccepted`；parity 50、intentional correction 0、unexpected mismatch 0。
- 正式 6v6 每个 `ReplayRallyResolved` 恰对应一次比分增长；旧版 3v3 仍验证 Disabled、
  零 V3 计数和空 adapter。
- authority 回归覆盖同一拦网者的首个计数触球、拦网反弹后任一方重新获得三次触球，以及第四次
  计数触球在响应前被拒绝且不改变出球速度。

后续阶段仍需补齐：

- `CanonicalMatchReplayHashV2` / frame hash 的完整 payload inclusion/exclusion 表。
- shanked pass、terminal event 等更多真实 executor/planner 场景测试。
- 两队 gate-5 共享物理预测 cache 的真实实现与 perf calibration。
- 手动场景验证。
- 已确认后续顺序先加入 Phase 1.5 兼容运行时稳定化：连续攻手根节点、唯一的 2.5 号位
  `SetterOrganizationZone`、二传优先代传回退，以及当前已生效属性的固定种子/回放证据。它不等待
  planner，也不激活四个 reserved V3 轴。随后真实接线 `AttackControl` 和共享 execution envelope，
  再做十二人 shadow、组件边界、接发组织的 V3-plan 迁移、攻防重组、`CourtAwareness` 感知、director
  清理与回放校准。`SoftTouch` 与 `BlockTechnique` 只在 Phase 6 的对应动作 envelope 独立生效；
  `CourtAwareness` 只在 Phase 7 的感知输入独立生效。此前四轴必须继续显示为 reserved，不能冒充
  正式比赛属性。

Phase 1.5 兼容运行时稳定化已完成（Unity `6000.0.43f1`）：

- 新增唯一纯策略来源 `SetterOrganizationZone`：以进攻方坐标定义默认组织点
  `(x=1.5m, depth=1.1m)`，经 `TeamCourtFrame` 镜像到两队世界坐标；横向和深度质量分级在该类型
  中集中维护。正常一传、二传预备根节点和回放诊断共用此策略，既有攻点/攻线与“实际二传离网超过
  4m 后移助跑带”规则未改。
- `Organize` 阶段优先选择合法可达的注册二传；仅当其不可达或为上一次触球者时保留非二传代传。
  回放只在实际 `Organize` 决策上写入策略目标、预测一传落点、区域等级、二传可达状态/预备移动、
  实际组织者和回退原因；不把预测值伪称为接触后的实测结果。
- 攻手根节点运动从助跑、起跳、接触到落地均受速度/时间约束；攻击触球对齐的单次修正上限为
  `0.18m`，余量保留为可观察的运动短缺。接发、二传和 controlled-handling 的既有修正上限未修改。
- 新增固定种子 (`73421`) 的单变量基准，覆盖当前真正生效的 V2-shaped 字段：`Mobility`、
  `Reaction`、`Jump`、`ReceiveTechnique`、`SetTechnique`、`AttackTechnique`、`AttackPower`、
  `MaxAttackReach`。回放同时列出决策候选实际消费的这些字段。`AttackControl`、`SoftTouch`、
  `BlockTechnique`、`CourtAwareness` 仍为 reserved；接线顺序分别为 Stage 2、Stage 6、Stage 6、
  Stage 7。
- 最终验证：全量 EditMode `TestResults/Stage15-final-edit.xml` 为 467/467 passed；正式 6v6
  PlayMode `TestResults/Stage15-formal-play.xml` 为 5/5 passed（69.116 秒）；组织回放 HTML 标签
  定向回归 `TestResults/Stage15-label-green.xml` 为 1/1 passed。`git diff --check` 无输出。

## 回滚与风险

主要风险是 P0 接口闸门范围较大，若拆得过晚会造成 Shared、Match、Career 同时返工。缓解方式是
Phase 0 只锁名称、版本、hash 边界、diagnostic hooks 和验收 fixtures，不提前锁定采样数、迁移
系数、cache storage、replay 完整 payload 或平衡参数。

性能风险先用 CPU deterministic backend、bounded work、scoped/diff replanning、shared trajectory
cache 和 gate pruning 控制。GPU 不作为当前权威执行路径；未来若加入 GPU backend，必须保持可复现
canonical artifacts 和 replay 行为。

Phase 1 已用实际场上资格执行 Block 资格判断。Attack 资格事实需要起跳点、触球点、进攻线和网高；
当前 `BallContactCandidate` 只有碰撞面、动作与 actor，未携带权威起跳点。Task 7 没有伪造几何，
因此正式运行时的 above-net/front-zone Attack 资格接线留给补充该碰撞事实的后续阶段；完成前不得
宣称该几何限制已经由 V3 authority 执行。
