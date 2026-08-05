# Career x Match V5 Windows 验收

- Status: deferred
- Owner: Career / Match collaborator
- Priority: P1
- Scope: 已实现 V5-A / V5-B 的 Windows Unity 验证；不修改 V5 合同或游戏逻辑
- Depends on:
  `docs/handoffs/completed/2026-07-30-career-match-v5-attribute-contract-design.md`、
  `docs/handoffs/completed/2026-07-31-career-match-v5-b-report-settlement.md`

## 当前要求

在具备 Unity `6000.3.20f1` Windows Build Support (IL2CPP) 的 Windows 环境中，验证已合并到
`main` 的 V5-A 与 V5-B 交付物。该任务只补齐平台验证证据；不得以验收为由重构、扩展或改变已冻结的
V5 数据合同、结算规则或训练室功能。

## 非目标

- 不实现伤病结算、多局制、换人、自由人替换或其他后续 V5 功能。
- 不迁移 V4 pending、result 或 replay 到 V5。
- 不在 macOS 上安装或模拟 Windows IL2CPP 支持模块。
- 不修改 Shared、Career、Match、Bootstrap、场景或构建设置，除非验证先暴露了可复现缺陷并另行激活
  修复任务。

## 交付物

- Windows focused EditMode 与受影响模块回归结果。
- V5 Formal 成功、取消、启动失败与固定种子重放的 PlayMode 结果。
- Windows x64 IL2CPP Development Build 产物及构建报告。
- Windows Player 的 V5 创建/恢复、物理比赛、报告结算、快速模拟和 V4 pending 拒绝/恢复的人工验收记录。
- 仅记录验证结果的变更记录；若发现缺陷，附可复现步骤并暂停本任务等待修复任务。

## 验收条件

- V5-A 与 V5-B 的 focused 和受影响模块测试在 Windows 上通过。
- V5 Formal PlayMode 覆盖成功、取消、启动失败与固定种子重放，且结果/回放/报告字节保持确定性。
- Windows x64 IL2CPP Development Build 成功，Player 无 Bootstrap、场景、输入或崩溃异常。
- 人工完成 V5 生命周期闭环，并确认 V4 pending 被可恢复地拒绝，未发生静默迁移或数据丢失。
- 验收通过后更新两个已完成 V5 任务单的 Windows 状态；若失败，不将本任务标为 completed。

## 依赖资料

- `AGENTS.md`
- `docs/superpowers/specs/2026-07-30-career-match-v5-attribute-contract-design.md`
- `docs/superpowers/plans/2026-07-31-career-match-v5-a-implementation-plan.md`
- `docs/handoffs/completed/2026-07-30-career-match-v5-attribute-contract-design.md`
- `docs/handoffs/completed/2026-07-31-career-match-v5-b-report-settlement.md`

## 结束处理

Windows 环境具备后，移动本文件至 `docs/handoffs/active/` 并将 Status 改为 `active`，且当时必须没有
其他 active 任务单。验证完成后移入 `docs/handoffs/completed/` 并记录结果。
