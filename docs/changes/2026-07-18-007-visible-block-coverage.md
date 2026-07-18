# CHG-20260718-007：Physical3v3Rally 可见拦网与下撤覆盖

- 日期：2026-07-18
- 状态：已完成
- 负责人：Match
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/blocking-roles`
- 关联提交或 PR：尚未创建

## 改动摘要

为 `Physical3v3Rally` 增加三人排球下的可见战术拦网与下撤覆盖。攻手承担主要拦网职责；当进攻线路更靠中路时，二传可以补拦，攻手下撤覆盖。该改动只改变站位、姿态和 UI/日志可见性，不改变球路、触球计数、落点判分或 `MatchResultV1` 产出。

## 具体变更

- 文件、场景、资源或程序集：
  - 修改 `PhysicalRallyTacticPlanner`，为每队战术增加 `BlockCoveragePlan`，包含 blocker、blockPosition、coverReceiver、coverPosition。
  - 修改 `PrototypePlayerAgent`，新增非触球型 `ScheduleSupportAction`，只移动和播放 Block/Receive 姿态，不向 `SimulatedBall` 注册触球候选。
  - 修改 `ThreeVsThreeRallyDirector`，在每次攻击触球前安排防守方拦网和下撤覆盖，并在 UI/日志中显示 block 计数。
  - 更新 EditMode 和 PlayMode 测试。
- 新增或修改的公开类型/字段：
  - `TeamSideSign`
  - `BlockCoveragePlan`
  - `TeamRallyTactic.BlockCoverage / Blocker / BlockPosition / CoverReceiver / CoverPosition`
  - `PhysicalRallyTacticPlanner.PlanBlockCoverage(...)`
  - `PrototypePlayerAgent.ScheduleSupportAction(...)`
  - `ThreeVsThreeRallyDirector.BlockSupportAssignments / CoverageSupportAssignments`
- 行为变化：
  - 宽位/边线进攻由攻手优先拦网。
  - 中路进攻可由二传补拦，攻手下撤覆盖。
  - 防守位仍主要承担一传/防守，不作为第一版拦网人。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；不修改 Shared DTO、Career 数据流、Bootstrap 场景入口或 MatchResultV1。
- 对方开发者需要做什么：无

## 验证

- [x] EditMode 测试：Unity `6000.0.43f1`，`145/145` passed，结果文件 `TestResults/EditMode-block-green2.xml`。
- [x] PlayMode 测试：Unity `6000.0.43f1`，`3/3` passed，结果文件 `TestResults/PlayMode-block.xml`。
- [x] 手动场景验证：自动 PlayMode 日志出现攻手主拦与二传补拦，例如 `blocker=Attacker` 与 `blocker=Setter`。
- [x] 序列化、存档或迁移验证：不涉及。

## 回滚与风险

主要风险是支援动作让非触球球员移动过多，影响画面阅读；当前实现不会注册触球候选，因此不会改变球路或计分。安全回滚方式：移除 `BlockCoveragePlan` 字段、`ScheduleSupportAction` 调用与 Director 中的 `ScheduleBlockCoverage`。
