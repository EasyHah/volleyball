# 二传教师审核流水线

- Status: active
- Owner: Match collaborator
- Priority: P1
- Scope: Match Editor / TrainingLab / MenShen / 本地标注数据
- Supersedes:
  `docs/handoffs/completed/2026-07-31-formal-training-scenario-lab-implementation.md`

## 当前要求

按 `docs/superpowers/specs/2026-07-31-setter-teacher-review-design.md` 实现 Editor-only 的二传
教师审核流水线。它将训练实验室的冻结 `DecisionSnapshotV1` 中“二传组织”决策提交给现有 MenShen
接口，由开发者逐条接受或从既有合法候选中纠正，并记录人工确认标签。

教师输入必须和本地二传判断器在同一时刻可见的信息集一致。教师只给候选排序与理由，永不成为
比赛权威、永不发送命令，也不得进入 Player、Shared、Replay、Career 或正式运行时程序集。

## 非目标

- 不修改正式 AI 选择、物理、触球规则、裁判或比赛结果。
- 不在 Player、Windows Development Build 或运行中的训练回合调用网络。
- 不审核接发、拦网、防守或进攻路线决策。
- 不允许教师或审核者创建本地规则未生成的候选。
- 不实现批量预标注、抽检阈值、本地学生模型训练、推理或蒸馏。
- 不将原始教师响应、API 密钥或本地数据集提交到 Git。

## 交付物

- 二传信息集和合法候选的规范请求/响应/审核合同及稳定 hash。
- 复用现有 Editor-only MenShen 客户端的单条教师请求、严格响应解析和安全失败处理。
- Unity Editor 审核界面：本地选择、教师排序、理由、接受/纠正和人工确认状态。
- Git 忽略的本地 JSONL 数据集与仓库内不含样本的版本清单。
- focused EditMode、Editor 交互验收和变更记录。

## 验收条件

- 教师请求字段与本地二传判断器可读字段逐项一致，且不包含未来/隐藏/赛后信息。
- 教师排序只包含当前合法、可行的攻手候选；未知、重复、遗漏或畸形响应被拒绝。
- 人工确认只能接受教师首选或选择已有合法候选；确认记录冻结快照/候选集 hash、模型和提示词版本、
  原始响应及最终选择。
- 网络失败、超时、限流和无效响应不会改变回合、Replay、当前选择或已确认历史。
- 数据集与原始响应只写入 Git 忽略的本地路径；黄金集只包含人工确认条目。
- 不新增 Player 网络依赖，不修改 Shared Replay V4、Career 存档或正式比赛入口。

## 依赖资料

- `AGENTS.md`
- `docs/superpowers/specs/2026-07-31-setter-teacher-review-design.md`
- `Assets/Volleyball/Match/Runtime/Presentation/TrainingLab/DecisionSnapshotV1.cs`
- `Assets/Volleyball/Match/Editor/AI/MenShenChatClient.cs`

## 结束处理

完成或替代时，将本文件移入 `docs/handoffs/completed/`，更新 Status、结束日期和后继任务单链接。
