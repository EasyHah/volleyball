# CHG-20260726-001：Full Rally V4 Gate G player component boundaries

- 日期：2026-07-26
- 状态：已完成
- 负责人：Match / Docs
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/full-rally-v4-gate-g-player-component-boundaries`
- 关联提交或 PR：`9b4e491` 至 `a82ff4d`（Gate G Task 1–6）、`4a28a6d` 至 `fb6c3f9`（Task 7 验证、边界修复与最终集成修复）

## 改动摘要

Gate G 将 `PrototypePlayerAgent` 收敛为兼容 facade：身份、配置和对外 API 保持兼容，迁移后的执行、时间线、接触、运动和表现业务状态分别由组件持有。正式 V4 调度只通过 technique executor 消费既有 envelope/sample/classification；legacy 3v3 兼容路径仍保留。

本改动不迁移战术权威：director、planner、rules、predictor 和 replay recorder 仍在既有边界之外。它不改变公开 6v6/3v3 bootstrap API，也不修改 Replay V4 JSON/hash 或 Gate F shadow 数据。

## 具体变更

- `PrototypePlayerAgent`：兼容 facade 和组件编排；不持有已迁移的业务 state。
- `PlayerTechniqueExecutor`：持有 V4 execution envelope/sample/classification 和已解析 command；不重新预测或分类。
- `PlayerActionTimeline`：持有接触/support timeline 及窗口状态。
- `PlayerContactSurfaceProvider`：持有已解析接触输入、surface/capsule、physical-block 及接触诊断；不选择战术且不移动 root。
- `PlayerLocomotion`：持有 root motion、attack approach/takeoff/contact/landing、shortfall 和攻击 correction 记账；单次攻击 correction 累计受 `0.18m` 上限约束，未闭合运动误差由 `MovementShortfall` 暴露。
- `PlayerPresentation`：持有 rig/pose 表现；不生成 contact；timeline 不设置 pose。
- 集成修复：攻击接触对齐由 locomotion 持有持久 offset；`MaximumSpeed` 发布计划轨迹及实际对齐步骤的可观测速度上界，但不以追赶式积分器改写既有计划轨迹。首次攻击对齐继续保持基线预算语义。
- Surface 历史隔离：alignment probe、普通 surface preview 和 block capsule preview 均使用无历史采样，不再提前改写正式接触的 previous frame；正式接触速度、6v6 对称校准和 3v3 物理路径因此保持基线行为。
- Formal block 路径生成并保存原生 V4 envelope/sample/classification 与 trajectory artifact，strict replay 不再依赖缺失执行证据。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；公开 6v6/3v3 bootstrap API 未改。
- 对方开发者需要做什么：无。

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [ ] 手动场景验证
- [x] 序列化、存档或迁移验证（静态确认 Replay V4 JSON/hash 与 Gate F shadow 文件未改）

Unity `6000.0.43f1`，2026-07-26 的实际结果如下（`TestResults/` 已忽略，均为本地证据）：

- 修复后完整 EditMode（HEAD `fb6c3f9`）：`TestResults/GateG-final2-editmode.xml`，`590/590` passed，`0` failed/skipped/inconclusive，`5.1393783s`。
- 修复后完整 PlayMode（HEAD `fb6c3f9`）：`TestResults/GateG-final2-playmode.xml`，`30/30` passed，`0` failed/skipped/inconclusive，`540.8883032s`。
- 3v3 顺序敏感性复验（HEAD `28a0706`）：`TestResults/GateG-post-repair-3v3-run1.xml` 与 `run2.xml`，连续两轮均 `3/3` passed。
- 6v6 对称校准复验（HEAD `28a0706`）：`TestResults/gate-g-provider-history-formal.xml`，`1/1` passed，20 局胜负回到断言范围 `9–11`。
- 历史诊断事实：首次完整 PlayMode 为 `29/30` passed；失败用例 `ThreeVsThreeRallyPlayModeTests.PhysicalLoop_UsesSixPlayersOneBallAndSwitchableCameras` 以最终比分 `15:2` 触发最低比分应 `>= 5`、实际 `2` 的断言。`TestResults/GateG-task7-3v3-repro.xml` 又以 `0/1` 重现，确认并非缺失 XML 或偶发 seed。
- 最终根因与修复：live-root `MoveTowards` 把诊断性的速度上界误用为计划轨迹裁剪器，先在 Set 对齐后产生追赶误差；alignment probe 又复用了正式 surface history，提前覆盖 previous frame 并改变表面速度。修复后计划采样保持权威，实际步骤只提升公开速度上界；probe 不写正式历史，攻击 correction 仍 `<= 0.18m`。
- 静态审查：五个组件均无 director/planner/rules/replay-recorder 引用；executor 无 `PlayerAbilityProfile`、base attribute 或 predictor 引用，且只保存 V4 输入；Gate G diff 不触及 6v6/3v3 bootstrap、Replay V4 JSON/hash 或 Gate F shadow 文件；攻击 correction 上限为 `PrototypePlayerAgent.NetClearance`（`0.18m`）并记录 `MaximumAppliedContactCorrection`/`MovementShortfall`。
- `git diff --check`：无输出。

未执行人工场景浏览。最终完整 EditMode/PlayMode 均在修复后的同一代码 HEAD `fb6c3f9` 上生成通过 XML。

## 回滚与风险

后续风险是再次把 alignment 从 locomotion-owned state 退回 transform-only correction、把 `MaximumSpeed` 重新用作追赶式裁剪器，或让任何 preview/probe 写入正式 surface/capsule history。以速度上界、`0.18m` correction、provider previous-frame、6v6 对称校准和重复 3v3 测试监测。若回滚最终集成修复，应整体评估 `d362e5c..fb6c3f9`；若回滚整个 Gate G，请整体回滚 `9b4e491..fb6c3f9`。
