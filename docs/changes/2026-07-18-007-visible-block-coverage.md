# CHG-20260718-007：Physical3v3Rally 可见拦网与下撤覆盖

- 日期：2026-07-18
- 状态：已完成
- 负责人：Match
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/blocking-roles`
- 关联提交或 PR：尚未创建

## 改动摘要

为 `Physical3v3Rally` 增加三人排球下的可见战术拦网与下撤覆盖，并修正首版可见拦网暴露出的时间轴和站位稳定性问题。攻手承担主要拦网职责；当进攻线路更靠中路时，二传可以补拦，攻手下撤覆盖。支援动作仍是非触球展示，不消耗触球次数、不直接改写判分或 `MatchResultV1` 产出。

## 具体变更

- 文件、场景、资源或程序集：
  - 修改 `PhysicalRallyTacticPlanner`，为每队战术增加 `BlockCoveragePlan`，包含 blocker、blockPosition、coverReceiver、coverPosition。
  - 修改 `PrototypePlayerAgent`，新增非触球型 `ScheduleSupportAction`，只移动和播放 Block/Receive 姿态，不向 `SimulatedBall` 注册触球候选；支援动作改由球的模拟时间驱动，避免多回合后与 Unity 真实时间错位。
  - 修改 `ThreeVsThreeRallyDirector`，在每次攻击触球前安排防守方拦网和下撤覆盖，并在 UI/日志中显示 block 计数与实际激活计数。
  - 更新 EditMode 和 PlayMode 测试。
- 新增或修改的公开类型/字段：
  - `TeamSideSign`
  - `BlockCoveragePlan`
  - `TeamRallyTactic.BlockCoverage / Blocker / BlockPosition / CoverReceiver / CoverPosition`
  - `PhysicalRallyTacticPlanner.PlanBlockCoverage(...)`
  - `PrototypePlayerAgent.ScheduleSupportAction(...)`
  - `ThreeVsThreeRallyDirector.BlockSupportAssignments / CoverageSupportAssignments / BlockSupportActivations`
  - `ThreeVsThreeRallyDirector.BackSetAttackContacts / BackSetAttackFaults`
- 行为变化：
  - 宽位/边线进攻由攻手优先拦网。
  - 中路进攻可由二传补拦，攻手下撤覆盖。
  - 防守位仍主要承担一传/防守，不作为第一版拦网人。
  - 防守方拦网站位现在跟随对方进攻点，而不是错误地使用本方进攻点。
  - 主动作和支援动作的横向跑位起点都会投影到地面，避免把上一段扣球/拦网的空中根节点带入下一段移动。
  - BackSet/后三进攻使用更长攻击飞行时间，降低后排进攻被固定快攻时间压成系统性失误的风险。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；不修改 Shared DTO、Career 数据流、Bootstrap 场景入口或 MatchResultV1。
- 对方开发者需要做什么：无

## 验证

- [x] EditMode 测试：Unity `6000.0.43f1`，`151/151` passed，结果文件 `TestResults/EditMode-block-stability2.xml`。
- [x] PlayMode 测试：Unity `6000.0.43f1`，`3/3` passed，结果文件 `TestResults/PlayMode-block-stability2.xml`。
- [x] 手动场景验证：自动 PlayMode 日志出现攻手主拦与二传补拦，例如 `blocker=Attacker` 与 `blocker=Setter`。
- [x] 稳定性修复回归：新增模拟时间支援动作、无空中跑位起点、对方进攻点拦网、BackSet 非 100% fault 的测试；PlayMode 日志包含多次 `block-active`，验证后续回合仍进入拦网展示阶段。
- [x] 序列化、存档或迁移验证：不涉及。

## 回滚与风险

主要风险是支援动作让非触球球员移动过多，影响画面阅读；当前实现不会注册触球候选，因此不会消耗触球或直接改变计分。安全回滚方式：移除 `BlockCoveragePlan` 字段、`ScheduleSupportAction` 调用与 Director 中的 `ScheduleBlockCoverage`。若 BackSet 节奏显得偏慢，可单独调整 `AttackFlightSeconds`，但必须保留 BackSet 非 100% fault 的回归约束。
