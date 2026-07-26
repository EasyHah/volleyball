# CHG-20260726-001：Full Rally V4 Gate G player component boundaries

- 日期：2026-07-26
- 状态：进行中
- 负责人：Match / Docs
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/full-rally-v4-gate-g-player-component-boundaries`
- 关联提交或 PR：`9b4e491` 至 `a82ff4d`（Gate G Task 1–6）；Task 7 验证提交待回归修复后完成

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

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；公开 6v6/3v3 bootstrap API 未改。
- 对方开发者需要做什么：无。

## 验证

- [x] EditMode 测试
- [ ] PlayMode 测试（发现 Gate G 3v3 回归）
- [ ] 手动场景验证
- [x] 序列化、存档或迁移验证（静态确认 Replay V4 JSON/hash 与 Gate F shadow 文件未改）

Unity `6000.0.43f1`，2026-07-26 的实际结果如下（`TestResults/` 已忽略，均为本地证据）：

- 完整 EditMode：`TestResults/GateG-final-editmode.xml`，`572/572` passed，`0` failed/skipped/inconclusive，`5.6573s`。
- 完整 PlayMode：`TestResults/GateG-final-playmode.xml`，`29/30` passed，`1` failed，`0` skipped/inconclusive，`447.2793779s`。失败为 `ThreeVsThreeRallyPlayModeTests.PhysicalLoop_UsesSixPlayersOneBallAndSwitchableCameras`：断言最低比分应 `>= 5`，实际为 `2`（最终 `15:2`）。
- 重现：`TestResults/GateG-task7-3v3-repro.xml`，同一用例 `0/1` passed，`1` failed，`43.5717359s`；故不是一次性日志或无 XML 的结果。
- 静态审查：五个组件均无 director/planner/rules/replay-recorder 引用；executor 无 `PlayerAbilityProfile`、base attribute 或 predictor 引用，且只保存 V4 输入；Gate G diff 不触及 6v6/3v3 bootstrap、Replay V4 JSON/hash 或 Gate F shadow 文件；攻击 correction 上限为 `PrototypePlayerAgent.NetClearance`（`0.18m`）并记录 `MaximumAppliedContactCorrection`/`MovementShortfall`。
- `git diff --check`：无输出。

未执行人工场景浏览。由于完整 PlayMode 稳定失败，本记录保持“进行中”，不得将 Gate G 标注为完成或声称完整回归通过。

## 回滚与风险

当前主要风险是 Gate G component extraction 改变 legacy 3v3 的回合平衡：在稳定复现中最低比分从测试下限 `5` 降至 `2`。Gate F 记录的完整 PlayMode 曾为 `30/30`，而 Gate G 当前完整和单用例 XML 均失败，故应在更改基线前定位并修复该回归。若需回滚，整体回滚 Gate G Task 1–6 提交 `9b4e491..a82ff4d`，不要仅回滚单一 facade/组件提交。
