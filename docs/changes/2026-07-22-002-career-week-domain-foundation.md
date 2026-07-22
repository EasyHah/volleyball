# CHG-20260722-002：第一周领域与持久化基础

- 日期：2026-07-22
- 状态：已完成
- 负责人：Career
- 影响模块：Career.Domain / Career.Application / Career.Persistence / Career.EditModeTests / Docs
- 交互级别：模块内部
- 关联分支：`feature/career-week-execution`
- 关联提交或 PR：本分支 Stage 4A 提交

## 改动摘要

完成第一周执行命令之前的纯领域与 Schema V1 基础：行动保存稳定 `contentId`，快照保存未消费
训练强调项，加入封闭的第一周行动/社交事件内容和确定性数值规则。未实现周确认/执行/事件应用命令、
PendingMatch、Match/Shared/UI 或第二周持久化。

> [!IMPORTANT]
> 这是可分发基线冻结前对不完整 Schema V1 的原位完成。`schemaVersion` 仍为 1，但 pre-Stage-4
> 开发存档缺少必需 `contentId` 或 `trainingEmphases`，会被严格拒绝并必须显式删除后重建。
> 当前没有需保留的内部试玩基线或外部构建，因此不提供迁移，也绝不静默推断业务方向。

## 具体变更

- 每个非空 `progression.weekPlan.slots[]` 固定按
  `slotActionId, occurrenceId, kind, contentId` 写入；空槽仍为 `null`。
- 顶层 `trainingEmphases` 紧随 `progression`，始终为数组；项固定按
  `sourceSlotActionId, direction, bonusBasisPoints` 写入。
- 第一周行动封闭为 11 个稳定 ID：五个专项、三个力量、团队合练、休息及
  `schedule.u1w1.match.01`。专项/力量首次与同向重复强调为 `1000 + 500`，方向上限 `1500`。
- 直接规则：专项 120 XP/+8 fatigue，力量 100/+12，团队八项各 20/+6/+5 trust，休息
  -18 fatigue 且 mindset 每次最多 5 向 50 靠近。D/C/B/A/S 乘数为
  8000/9000/10000/11000/12000 basis points。
- 社交事件固定为 `event.team_meal` 及 attend/extra_practice 两选项；区间与效果按
  `career-domain-rules.md` 的封闭 V1 表执行，规范 attend roll 6791 得到 `0 XP/+4/+6/+3`。
- Schema golden hash 重建为
  `d9e9464b0eeeea3c848efff9522e2d41e3c14db40de2c7db40b2daaa5378d237`；旧不完整 V1
  golden 被保留为拒载回归证据。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：Career 开发期 Schema V1 原位完成；旧开发存档必须重建，无迁移
- 消费方或后续负责人需要做什么：Stage 4B 只消费本次纯规则与 ledger，不得在本次范围反向加入命令/UI

## 验证

- [x] EditMode 测试（Unity `6000.3.20f1`：Career `284/284`，全项目 `507/507`）
- [ ] PlayMode 测试（纯 Career 领域/持久化改动不需要）
- [ ] 手动场景验证（无 UI/Scene 变更）
- [x] 序列化、存档或迁移验证：canonical bytes/hash、旧 V1 拒载、create/commit/recovery ledger 往返

工具 Python 单元测试 `8/8` 通过。Match 与 Shared Runtime 冻结路径未修改；最终 repository
validator、diff 与冻结哈希证据记录在
`.superpowers/sdd/task-4a-report.md`。

## 回滚与风险

回滚本提交会恢复不完整的开发期 V1，并丢失保存训练方向/强调项的能力。主要风险是本地残留旧开发
存档被拒载；这是明确选择，处理方式是开发者确认后重建，而不是迁移或默认值。
