# CHG-20260728-001：Full Rally V4 Gate K 完成

- 日期：2026-07-28
- 状态：已完成
- 负责人：Shared / Match / Replay / Docs
- 影响模块：Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gate-k-completion`
- 关联提交或 PR：`3798807..52eb749`，以及本交付记录提交

> [!IMPORTANT]
> Replay V4 只新增可选 strict/canonical `workBudget` record；历史 Replay
> 不含该字段时 bytes/hash 保持不变。Career、Bootstrap 无需修改代码。

## 改动摘要

Gate K 完成 Full Rally V4 的架构收口：Director 不再直接拥有 tactical planner、
candidate ordering、正式 authority coordinator/store/revision 或 legacy set/coverage
selector；正式 Gate H/I/J authority 的 event-owned evidence 由单一 orchestrator 管理。

Replay HTML 增加 `AUTHORITATIVE / ACTUAL`、`HOME PERCEIVED` 和
`AWAY PERCEIVED` 三个分区，并记录确定性 work budget/degradation。固定 seed 四轴
matrix 验证 AttackControl、SoftTouch、BlockTechnique 和 CourtAwareness 只产生声明
范围内的变化。

## 具体变更

- 新增纯 AI `RallyDecisionCoordinatorV3`，持有 planner、decision index、不可变输入
  构造、candidate feasibility、legacy set target 与 cover player selection。
- 新增 `FormalRallyAuthorityOrchestrator`，集中管理 Gate H/I coordinator、
  controller、revision、source sequence、SetIntent 与 event-scoped receipt。
- Replay event 新增可选 `ReplayWorkBudgetRecordV4`：configuration identity、
  candidate/sample/expansion count、work units、degradation 和 outcome。
- HTML renderer 成为纯 `Render(MatchReplayV4)`；双方视角只读 event-owned evidence，
  缺失时显示 `No event-owned view`，不暴露 selected route/future sample。
- 新增 Gate K 四轴 PlayMode matrix 与性能报告：
  `docs/performance/full-rally-v4-gate-k-calibration.md`。

## 跨模块交互重点

- 接口提供方：Shared Replay V4 optional work-budget record。
- 接口使用方：Match recorder、HTML renderer 和测试；Career、Bootstrap 无需改代码。
- 数据方向：`execution/trajectory evidence -> deterministic work record -> Replay/HTML`。
- 兼容性：历史无 `workBudget` 的 Replay JSON 与 hash 不变；新字段使用严格字段集合、
  canonical 顺序及 `candidate * sample * expansion passes` 一致性校验。
- 对方开发者需要做什么：Replay consumer 可选择读取新 record；不得把 wall-clock、
  profiler、allocation 或 cache hit 写回 gameplay/canonical hash。

## 验证

- [x] 完整 EditMode：745/745 passed，0 failed/skipped/inconclusive，
  7.5910004s（`TestResults/GateK-policy-identity-editmode-green.xml`）。
- [x] 完整 PlayMode：39/39 passed，0 failed/skipped/inconclusive，
  322.8971697s（`TestResults/GateK-final-playmode-reviewed.xml`）。
- [x] Gate K 四轴 matrix：4/4 passed，0.0285654s；完整 profile/result/envelope
  fingerprints 在 `TestResults/GateK-calibration-matrix-final.xml`。
- [x] 缩短后的长校准：formal 30、3v3 30、对称 20，3/3 passed，
  50.6581248s；阈值未降低。
- [x] 两次独立 formal capture 的 canonical bytes、event sequence、Replay hash 与
  HTML bytes 相同；work units `35,49,49`，全部
  `FullSampling / WithinBudget`；record 使用事件实际 envelope policy identity。
- [x] Director 静态扫描未命中 `TeamRallyDecisionPlanner`、
  `OrderedCandidates`、`SelectGeometricSetTarget`、`SelectAttackRoute`、
  `TrySelectCoverPlayer` 或 `SetTargetSelector`。
- [x] AI/Domain 反向依赖扫描未命中 Unity、Presentation 或 recorder；
  `git diff --check` clean。
- [x] HTML 自动结构检查覆盖双方视角、缺失视图、work budget、隐藏 route/sample
  排除和 byte stability。
- [ ] 浏览器人工视觉检查：本地 `file://` 测试产物被浏览器安全策略拦截。
- [ ] Windows x64 与 profiler：当前 macOS 自动验收范围外。

## 回滚与风险

work-budget contract、recorder 映射和 HTML renderer 应作为一个兼容性单元回滚；
formal orchestrator 与 Director ownership 迁移也应成组回滚。剩余发布风险限于
Windows x64 浮点/渲染差异和真实 profiler 分布；这些观测不得参与确定性决策。
