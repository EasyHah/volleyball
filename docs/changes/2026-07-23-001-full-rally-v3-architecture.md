# CHG-20260723-001：Full Rally V3 架构与 Shared 契约预留

- 日期：2026-07-23
- 状态：已完成
- 负责人：Shared / Match
- 影响模块：Shared / Match / Career / Docs
- 交互级别：跨模块（重点）
- 关联分支：`geometric-counterplay`（设计稿）/ `main`（Phase 0 接口落地）
- 关联提交或 PR：本地提交（见当前 commit）

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

本记录声明 Phase 0 接口、测试夹具和跨模块协作边界已落地，不表示 Full Rally V3 planner、
cache、完整 replay payload、平衡公式或游戏内执行已实现。GPU backend 不进入当前 Phase 0/P1；
第一版以 CPU deterministic backend 为权威。

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
- 行为变化：
  - 每次 accepted contact 先推进 rules transition，再 deterministic 判断 covered branch、
    local revision、scoped replan、global replan 或 terminal no-plan。
  - fallback 行为由 deterministic work-unit caps 决定，wall-clock budget 只作为 profiling target。
  - planner 与 executor 必须共享同一份 execution envelope；两队 gate-5 必须读取同一份
    authoritative trajectory prediction artifact。
  - V2 能力迁移只能作为 compatibility estimate，并记录 provenance 与 collapsed axes 限制。

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
- [ ] PlayMode 测试
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

后续阶段仍需补齐：

- `CanonicalMatchReplayHashV2` / frame hash 的完整 payload inclusion/exclusion 表。
- shanked pass、block touch、terminal event 等真实 executor/planner 场景测试。
- 两队 gate-5 共享物理预测 cache 的真实实现与 perf calibration。
- PlayMode 与手动场景验证。

## 回滚与风险

主要风险是 P0 接口闸门范围较大，若拆得过晚会造成 Shared、Match、Career 同时返工。缓解方式是
Phase 0 只锁名称、版本、hash 边界、diagnostic hooks 和验收 fixtures，不提前锁定采样数、迁移
系数、cache storage、replay 完整 payload 或平衡参数。

性能风险先用 CPU deterministic backend、bounded work、scoped/diff replanning、shared trajectory
cache 和 gate pruning 控制。GPU 不作为当前权威执行路径；未来若加入 GPU backend，必须保持可复现
canonical artifacts 和 replay 行为。
