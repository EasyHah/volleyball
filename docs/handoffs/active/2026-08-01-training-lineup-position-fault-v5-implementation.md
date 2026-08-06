# 训练室阵容编辑、严格位置错误与 V5 接线实现

- Status: active
- Owner: Career / Match collaborator
- Priority: P1
- Scope: TrainingLab / Match / Shared / Career / Bootstrap / Replay / 场景 UI / Windows 验证
- Supersedes:
  `docs/handoffs/completed/2026-08-01-hud-simplification-setter-review-entry.md`

## 当前要求

依照已确认的 UI 设计和实施计划，实现仅从发球前开始的训练室阵容工作台，以及 TrainingLab 与原生 V5
正式比赛共用的严格排球位置错误判定。轮转位次先编辑并确认锁定，之后才能拖拽实际站位；位置错误在发球
触球瞬间裁决，违规队立即失回合、对手得一分，且不得启动正常物理、AI 或触球链路。

TrainingLab 管理员覆盖仅作用于本训练情景的 Match 测试数值能力、身高与惯用手。训练室还须提供受限制的
发球球位置/速度设置、自由镜头书签、持续回合监视器及五步工作台。V5 result/replay 必须记录与
context/result/replay hash 绑定的可验证位置错误事实；Career 仅消费 Match 结论而不重算位置。

## 已授权边界

- Match 域纯规则、正式 V5 发球接触路径、TrainingLab 数据/UI/场景接线和 Replay recorder。
- Shared V5 context/result/replay 合同、ContractJson、Career V5 恢复/消费边界及必要 Bootstrap 接线。
- 相关程序集、测试、Windows x64 IL2CPP Development 构建与 Player 验收记录。

## 非目标

- V4 的规则、存档、结果、Replay、恢复和行为完全不变。
- 不允许任意回合中插球；发球球只能位于当前发球方己方底线后。
- 训练室覆盖、镜头或编辑器审核数据不得流入 Career、V5 正式 context 或正式比赛。
- 不提供位置错误容差或绕过判罚的用户开关；相等投影合法，只有严格反转违法。
- 不编辑姓名、号码、注册职业或 Career 权威属性。

## V5 版本与恢复决策

本阶段引入新的必填 V5 位置错误证据版本，并同时更新 context/result/replay 的规范 JSON、hash 和验证。
旧版本的 V5 pending/result/replay 绝不以默认字段或可选兼容字段静默读取；恢复时必须以明确、可恢复的
“不支持的 V5 证据版本，请放弃此待处理比赛并新建比赛”路径拒绝，保留原始工件供诊断。回滚方式是回退
本阶段分支；回退后的程序同样拒绝该新版本，而不是试图降级解释。

## 验收条件

- TrainingLab 完成轮转锁定、站位拖拽、角色/位次标识、受限发球设置、三正交编辑、镜头书签、管理员
  覆盖隔离、预检和运行时位置错误闭环。
- 纯规则覆盖 Home/Away、相等合法、每条前后/左右关系、并发错误、输入拒绝及稳定排序。
- V5 仅在发球触球前裁决，违规回合零触球且只计一次对手得分；result/replay/context 证据可确定性绑定，
  Career 不重算。
- V4 回归保持不变；Shared/Career/Match 受影响测试、完整 EditMode、必要 PlayMode、独立复核通过。
- 完成 Windows x64 IL2CPP Development Build 与 Windows Player 人工验收，或如环境不可用则如实保持待验收，
  不声明任务完成。

## 依赖资料

- `AGENTS.md`
- `docs/superpowers/specs/2026-08-01-training-lineup-position-fault-v5-ui-design.md`
- `docs/superpowers/plans/2026-08-01-training-lineup-position-fault-v5-implementation-plan.md`
- `docs/handoffs/deferred/2026-08-01-career-match-v5-windows-validation.md`

## 结束处理

完成后将本文件移入 `docs/handoffs/completed/`，更新 Status、结束日期、每类验证证据、Windows 状态和
回滚/恢复结果；不得删除。

## 执行记录（2026-08-04）

- 已将 `origin/main`（含 `9a3897e` Windows IL2CPP 验证记录）合入
  `codex/training-lineup-position-fault-v5`。
- 已完成并验证 Task 1--8 主体实现：严格位置错误纯规则、训练发球冻结、TrainingLab 运行时/UI、V5
  result/replay 证据、Career V5 边界与正式发球接触裁决。
- 额外修复版本中立能力投影回归：V4 的 Receive/Set/Defense movement 与 defense reaction 字段保持
  原语义；V5 正式 Gate I 使用 `MatchAbilitySnapshot`，不读取空的 V4 derived DTO。
- 独立高风险复核发现并已修复两项 V5 证据契约问题：位置错误事实现在严格绑定违规队的 context 轮转
  槽位，Career 聚合拒绝 result/replay 的不一致事实；另将 V5 PlayMode 的全局时钟恢复改为 `finally`。
- 新鲜自动验证：完整 EditMode `1573/1573`；V5 契约与 Career 聚合测试 `50/50`；V5 runner PlayMode
  `1/1`；TrainingLab 运行时 PlayMode `4/4`；`git diff --check` 通过。
- 同批目标 PlayMode 的 V4 非确定性回归未全绿：22 项中 20 项通过，`Runner_CompletesPhysicalSixVsSixWithContextBoundResultAndReplay`
  在预算内未结束，`Formal6v6_CalibratedToolRecovery_UsesPhysicalBlockAndNonAttackerSave` 首攻选为 `Tip`
  而非 `BlockToolRecovery`。两项均不在本次 V5 契约改动路径，须在最终自动验收前单独稳定复现并处理。
- 当前仍待：上述 V4 回归调查、1920x1080 macOS Editor 人工闭环，以及本计划新增 TrainingLab 的 Windows
  x64 IL2CPP Player 验收。handoff 保持 `Status: active`，不得宣称阶段完成。
