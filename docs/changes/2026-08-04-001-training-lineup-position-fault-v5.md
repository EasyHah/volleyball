# CHG-20260804-001：训练室阵容与 V5 位置错误接线

- 日期：2026-08-04
- 状态：已实现，自动验收待处理 V4 PlayMode 回归
- 负责人：Career / Match collaborator
- 影响模块：Match / Shared / Career / Bootstrap / TrainingLab / Replay / Tests / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/training-lineup-position-fault-v5`
- 合并提交：`7f570e7`（合入 `origin/main`，包含 `9a3897e`）

## 改动摘要

- TrainingLab 提供轮转锁定、站位编辑、受限发球起点、镜头/属性覆盖和运行时位置错误诊断。
- Match 纯规则在发球触球前按 Home/Away 投影轴裁决严格位置错误；违规回合只计一次对手得分，不启动
  物理、AI 或触球链路。
- V5 context/result/replay 保存确定性位置错误证据，Career 只消费 Match 结论；每条事实绑定违规队的
  context 轮转位次，且 Career 拒绝 result/replay 证据不一致。
- 修复 V4 兼容性：版本中立能力快照保留 Receive/Set/Defense movement 与 defense reaction 的原始
  V4 来源；V5 Gate I 使用快照而不是空的 V4 derived 属性。

## 自动验证

- 完整 EditMode：`1573/1573`。
- V5 位置错误契约及 Career 聚合：`50/50`；V5 runner PlayMode：`1/1`；TrainingLab 运行时 PlayMode：`4/4`。
- 同批目标 PlayMode：`20/22`。两个 V4 回归待处理：正式 runner 未在预算内结束，以及校准首攻选择 `Tip`
  而非预期的 `BlockToolRecovery`。
- `git diff --check`：通过。
- Career V5 runner 使用固定 `Time.captureDeltaTime = 1/60s`，避免 headless 帧率导致测试预算漂移；原值在用例结束后恢复。

## 待人工验收与风险

- 尚未在本会话执行 1920x1080 macOS Editor 的完整训练室手工流程。
- 尚未在 Windows Player 中执行本计划新增 TrainingLab 的 IL2CPP 构建、输入、渲染及 pending 恢复验收；现有
  V5 Windows IL2CPP 构建记录保留在 `9a3897e` 对应改动文档中。
- 回滚方式：回退本里程碑分支；新 V5 位置错误证据版本继续按 handoff 规则拒绝旧 pending/result/replay。
