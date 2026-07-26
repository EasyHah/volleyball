# CHG-20260727-001：Full Rally V4 Gate I 攻防与重组权威

- 日期：2026-07-27
- 状态：进行中
- 负责人：Shared / Match / Replay / Docs
- 影响模块：Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gate-i-attack-defense-reorganization-authority`（隔离 worktree 当前为 `dd28368` detached HEAD）
- 关联提交或 PR：`bdbfead`（设计）、`ce16e69`（双阶段 SetIntent handoff 澄清）与本实施计划的直接提交

> [!IMPORTANT]
> Shared 只新增可选 Gate I Replay V4 evidence；历史 Replay V4 无该字段时保持 canonical bytes/hash。Career 与 Bootstrap 无需修改代码。

## 改动摘要

Gate I 只切换正式 6v6。3v3、V3 Shadow/Disabled、Gate J perception 和 Gate K director/replay UI 不在本改动范围。

## 具体变更

- 待实施：不可变 attack/defense plan、纯 planner、revisioned coordinator 与 Gate G facade controller。
- 待实施：正式 6v6 的 Gate H SetIntent 双阶段交接；Gate H 保留 Set actor、timing 与 contact command 唯一写入权。
- 待实施：event-owned Gate I Replay V4 evidence，旧 V4 canonical 记录保持兼容。

## 跨模块交互重点

- 接口提供方：Shared（可选 Replay V4 authority record）与 Match（accepted-contact evidence）。
- 接口使用方：Match recorder；Career 与 Bootstrap 无需改代码。
- 数据方向：`Match plan/accepted contact -> Match replay mapping -> Shared Replay V4`。
- 兼容性：历史 V4 缺少 Gate I record 时 canonical bytes/hash 不变。
- 对方开发者需要做什么：新增 reader 必须保持严格 canonical 字段顺序与旧 record 兼容。

## 验证

- [x] Gate H 基线 EditMode：627/627 passed，0 failed/skipped/inconclusive，6.282779s（Unity 6000.0.43f1，`TestResults/GateI-baseline-editmode.xml`）。
- [x] Gate H 基线 PlayMode：31/31 passed，0 failed/skipped/inconclusive，705.0057159s（Unity 6000.0.43f1，`TestResults/GateI-baseline-playmode.xml`）。
- [ ] Gate I focused / complete EditMode 与 PlayMode。
- [ ] Gate I determinism、static scans 与 combined review。
- [ ] 手动场景、Windows x64 与性能 calibration（不在自动验证范围）。

## 回滚与风险

Gate I 的 Shared replay、controller、coordinator 与正式 cutover 必须作为一个兼容性单元回滚；不得单独移除一个 writer 而保留其余路径。主要风险为正式双 writer、stale lifecycle、隐藏路线泄漏、event-owned identity 丢失与历史 Replay canonical 破坏。
