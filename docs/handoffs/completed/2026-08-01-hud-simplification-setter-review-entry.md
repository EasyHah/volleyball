# HUD 精简与训练室二传审核入口

- Status: superseded
- Owner: Match collaborator
- Priority: P1
- Scope: Match HUD、TrainingLab UI、Editor-only 二传教师审核入口
- Supersedes:
  `docs/handoffs/completed/2026-07-31-setter-teacher-review-implementation.md`

## 当前要求

精简训练室和相关比赛画面的 HUD，使决策、回合状态与操作重点易于阅读；在训练室内提供明确的入口，
打开既有 Editor-only 二传教师审核流程。审核仍以 Gate I 攻手选择点冻结的
`SetterTargetSnapshotV1` 为唯一输入，并只允许人工在既有合法候选中确认或纠正。

HUD 精简不得削弱训练审核所需的上下文、候选可读性或回合安全反馈。入口仅服务开发者在 Unity Editor
中审核本地训练快照，不得进入 Player 或正式比赛运行时。

## 非目标

- 不修改正式 AI 选择、物理、触球规则、裁判、Replay 或比赛结果。
- 不修改 Shared、Career、V5 合同、存档、结算或 Windows 构建配置。
- 不在 Player、Windows Development Build 或运行中的训练回合调用网络。
- 不允许教师或审核者创建本地规则未生成的候选。
- 不实现批量预标注、抽检阈值、本地学生模型训练、推理或蒸馏。
- 不将原始教师响应、API 密钥或本地数据集提交到 Git。

## 交付物

- HUD 信息层级与可见项的精简实现，保留训练、回合和错误反馈所需的最小信息。
- 训练室内 Editor-only 的二传审核入口；入口能对当前/已冻结的合法二传决策快照启动审核。
- 复用既有二传教师审核合同、严格响应解析、人工确认和 Git 忽略的本地 JSONL 存储。
- 少量 focused EditMode 测试、Editor 实际交互验收和里程碑变更记录。

## 验收条件

- HUD 只展示当前操作、回合状态和训练审核必需信息，且不丢失可操作反馈。
- 审核入口只在 Unity Editor 的训练室可用；Player 与正式比赛入口不存在网络或审核依赖。
- 教师请求字段与本地二传判断器可读字段逐项一致；排序只包含当前合法候选，畸形响应被拒绝。
- 人工确认只能选择教师首选或已有合法候选；记录冻结快照/候选集 hash、模型和提示词版本、原始响应及最终选择。
- 网络失败、超时、限流和无效响应不改变回合、Replay、当前选择或既有确认历史。
- focused EditMode 通过，并完成一次 Unity Editor 训练室人工入口与 HUD 验收。

## 依赖资料

- `AGENTS.md`
- `docs/superpowers/specs/2026-07-31-setter-teacher-review-design.md`
- `docs/superpowers/plans/2026-07-31-setter-teacher-review-implementation-plan.md`
- `docs/handoffs/completed/2026-07-31-setter-teacher-review-implementation.md`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/DecisionSnapshotV1.cs`
- `Assets/Volleyball/Match/Editor/AI/MenShenChatClient.cs`

## 结束处理

完成后将本文件移入 `docs/handoffs/completed/`，更新 Status、结束日期、验证记录和后继任务链接。

## 进度记录（2026-08-01）

- 已实现：训练室 HUD 已精简；运行产生 Gate I `SetterTargetSnapshotV1` 时启用“审核二传”，并在
  Unity Editor 中打开既有审核窗口、预选最新快照。
- 已自动验证：focused EditMode 11/11，包含入口、Editor assembly 边界与既有严格审核合同；
  `git diff --check` 通过。
- 待人工验收：在 Unity Editor 中确认 HUD 可读性，并完成“训练回合 -> 审核二传 -> 预选最新快照”的
  实际交互闭环。
- 2026-08-01：审核中发现的重复候选会使严格教师响应无法提交；现已在 `SetterTargetSnapshotV1` 冻结
  边界拒绝重复 PlayerId，并由 focused EditMode 13/13 覆盖。该任务由后继高风险任务单替代，未完成的
  视觉验收保留为后续里程碑的人工验收项，不阻塞其已验证的模块内交付。
