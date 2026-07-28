# CHG-20260727-001：Full Rally V4 Gate I 攻防与重组权威

- 日期：2026-07-27
- 状态：已完成
- 负责人：Shared / Match / Replay / Docs
- 影响模块：Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gate-i-recovery`
- 关联提交或 PR：`bdbfead`（设计）、`ce16e69`（双阶段 SetIntent handoff 澄清）、`dd28368..15123ad` 与后续 recovery 提交

> [!IMPORTANT]
> Shared 只新增可选 Gate I Replay V4 evidence；历史 Replay V4 无该字段时保持 canonical bytes/hash。Career 与 Bootstrap 无需修改代码。

## 改动摘要

Gate I 只切换正式 6v6。3v3、V3 Shadow/Disabled、Gate J perception 和 Gate K director/replay UI 不在本改动范围。

## 具体变更

- 正式 6v6 使用不可变 attack/defense plan、revisioned coordinator 与 Gate G facade controller；3v3 保持 legacy。
- Gate H 仍是 Set actor、timing 与 contact command 唯一 writer；Gate I 只发布 SetIntent target/envelope 并接管 post-set 攻防、联合拦防、tool recovery 与重组。
- Tool recovery 使用实际 accepted block rebound 计算 event-owned immutable Receive execution；不可达 rebound fail-closed 到 ordinary reorganization，不 teleport 或回退 legacy continuation。
- 防守 contact evidence 使用实际发布 execution envelope identity；Attack、Block、Receive 使用按 revision 区分的 contact group，且 joint block primary/support roots 分离。

## 跨模块交互重点

- 接口提供方：Shared（可选 Replay V4 authority record）与 Match（accepted-contact evidence）。
- 接口使用方：Match recorder；Career 与 Bootstrap 无需改代码。
- 数据方向：`Match plan/accepted contact -> Match replay mapping -> Shared Replay V4`。
- 兼容性：历史 V4 缺少 Gate I record 时 canonical bytes/hash 不变。
- 对方开发者需要做什么：新增 reader 必须保持严格 canonical 字段顺序与旧 record 兼容。

## 验证

- [x] Gate H 基线 EditMode：627/627 passed，0 failed/skipped/inconclusive，6.282779s（Unity 6000.0.43f1，`TestResults/GateI-baseline-editmode.xml`）。
- [x] Gate H 基线 PlayMode：31/31 passed，0 failed/skipped/inconclusive，705.0057159s（Unity 6000.0.43f1，`TestResults/GateI-baseline-playmode.xml`）。
- [x] Gate I focused EditMode：39/39 passed，0 failed/skipped/inconclusive，1.4691015s（Unity 6000.0.43f1，`TestResults/GateI-focused-editmode-green.xml`）。
- [x] Gate I focused Formal 6v6 PlayMode：8/8 passed，0 failed/skipped/inconclusive，132.3709569s（Unity 6000.0.43f1，`TestResults/GateI-formal6v6-calibration-green.xml`）。
- [x] 完整 EditMode：719/719 passed，0 failed/skipped/inconclusive，8.2205148s（Unity 6000.0.43f1，`TestResults/GateI-final-editmode-lifecycle.xml`）。
- [x] 完整 PlayMode：34/34 passed，0 failed/skipped/inconclusive，423.5076327s（Unity 6000.0.43f1，`TestResults/GateI-final-playmode-lifecycle.xml`）。
- [x] 固定 seed determinism：2/2 passed，0 failed/skipped/inconclusive，8.7885832s（Unity 6000.0.43f1，`TestResults/GateI-final-determinism.xml`）。
- [x] 静态扫描：正式 Gate I legacy writer 仅保留在 Gate I disabled 分支；Domain/AI 未命中 `UnityEngine`、`Volleyball.Presentation` 或 recorder 反向依赖；`git diff --check` clean。
- [x] Combined review：独立审查发现并修复两项 P1（实际 Receive surface 可达性硬约束、Tool Receive evidence 与已发布 immutable execution 精确绑定）；复跑 coordinator 20/20、实际 tool recovery 1/1，并新增生命周期回归以保证每个物理 command contact group 只接受一次。
- [ ] 手动场景、Windows x64 与性能 calibration（不在自动验证范围）。

## 回滚与风险

Gate I 的 Shared replay、controller、coordinator 与正式 cutover 必须作为一个兼容性单元回滚；不得单独移除一个 writer 而保留其余路径。主要风险为正式双 writer、stale lifecycle、隐藏路线泄漏、event-owned identity 丢失与历史 Replay canonical 破坏。

未执行：手动场景视觉检查、键盘/控制器、Windows x64 build 与性能 calibration；这些不属于本次自动验收证据。
