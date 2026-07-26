# CHG-20260726-001：Full Rally V4 Gate G player component boundaries

- 日期：2026-07-26
- 状态：已完成
- 负责人：Match / Docs
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/full-rally-v4-gate-g-player-component-boundaries`
- 关联提交或 PR：`9b4e491` 至 `a82ff4d`（Gate G Task 1–6）、`4a28a6d`（首次 Task 7 诊断记录）、`3891f29` 与 `0d08692`（3v3 回归修复）

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
- 3v3 回归修复：`3891f29` 将攻击接触对齐改为 locomotion 持有的持久 offset，防止下一次 planned sample 覆盖 transform-only correction；`0d08692` 让 planned root motion 与 alignment 共用同一个 `MaximumSpeed * dt` 单步预算，并增加稳定的 3v3 接触序列回归。

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

- 修复后完整 EditMode（HEAD `0d08692`）：`TestResults/GateG-final-editmode.xml`，`576/576` passed，`0` failed/skipped/inconclusive，`5.0544863s`。
- 修复后完整 PlayMode（HEAD `0d08692`）：`TestResults/GateG-final-playmode.xml`，`30/30` passed，`0` failed/skipped/inconclusive，`540.9763152s`。
- 历史诊断事实：首次完整 PlayMode 为 `29/30` passed；失败用例 `ThreeVsThreeRallyPlayModeTests.PhysicalLoop_UsesSixPlayersOneBallAndSwitchableCameras` 以最终比分 `15:2` 触发最低比分应 `>= 5`、实际 `2` 的断言。`TestResults/GateG-task7-3v3-repro.xml` 又以 `0/1` 重现，确认并非缺失 XML 或偶发 seed。
- 根因与修复：planned root sample 会覆盖 transform-only 攻击对齐，使 correction 预算在接触窗口前被消耗；持久化 alignment offset 后，首轮修复仍允许 planned motion 与 alignment 各自消耗一次单步速度预算。`3891f29` 修复持久 offset，`0d08692` 统一单步预算；固定 3v3 路径恢复为 `15:6`，且攻击 correction 仍 `<= 0.18m`。
- 静态审查：五个组件均无 director/planner/rules/replay-recorder 引用；executor 无 `PlayerAbilityProfile`、base attribute 或 predictor 引用，且只保存 V4 输入；Gate G diff 不触及 6v6/3v3 bootstrap、Replay V4 JSON/hash 或 Gate F shadow 文件；攻击 correction 上限为 `PrototypePlayerAgent.NetClearance`（`0.18m`）并记录 `MaximumAppliedContactCorrection`/`MovementShortfall`。
- `git diff --check`：无输出。

未执行人工场景浏览。最终完整 EditMode/PlayMode 均在修复后的同一 HEAD `0d08692` 上生成通过 XML。

## 回滚与风险

已知 3v3 回归已由 persistent alignment offset 和共享单步速度预算修复，并由完整 PlayMode 与固定接触序列覆盖。后续风险是再次把 alignment 从 locomotion-owned state 退回 transform-only correction，或令 planned motion/alignment 分别消费速度预算；以 `MaximumSpeed * dt`、`0.18m` 上限和 3v3 contact 18/19 序列测试监测。若只回滚修复，应成对回滚 `3891f29` 与 `0d08692`；若回滚整个 Gate G，请整体回滚 `9b4e491..0d08692`。
