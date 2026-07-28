# CHG-20260721-002：对齐球员生涯周行动计划领域规则

- 日期：2026-07-21
- 状态：已完成
- 负责人：Career
- 影响模块：Career.Domain / Career.EditModeTests
- 交互级别：模块内部
- 关联分支：`feature/career-week-plan-aligned`
- 关联提交或 PR：本文件所在提交

## 改动摘要

把本地旧分支 `09f7e61` 的周计划实现与测试重放到 Unity `6000.3.20f1` 和正式 6v6/冻结 Match 基线。
每周固定三个行动槽，比赛由赛程预留并占用槽位，玩家可在确认前安排、替换和移动其他行动，确认后
整份计划锁定。

需求对齐时删除旧分支中的常驻 `Academics`、`Social` 行动：学业和社交只由事件系统产生，不进入玩家
每周常驻行动列表。旧分支从未形成可分发存档，因此不保留旧枚举值或迁移兼容层。

## 具体变更

- 文件、场景、资源或程序集：新增 `CareerWeekAction`、`CareerWeekPlan` 及其 EditMode 测试。
- 新增公开类型：`CareerWeekActionKind`、`WeekPlanId`、`SlotActionId`、`OccurrenceId`、
  `CareerWeekAction` 和 `CareerWeekPlan`。
- 行为变化：玩家只可安排专项训练、力量训练、团队合练、休息；`Match` 只能通过 `ReserveMatch` 加入，
  预留后不能覆盖、移除或移动；三个槽全部填满后才能确认，确认后不能继续修改。
- 明确不包含：训练结算、事件生成、周状态机、存档、Shared/Match 接线、场景和 UI。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容当前主线；旧未合并分支的 `Academics/Social` 无正式存档兼容承诺。
- 消费方或后续负责人需要做什么：无需改代码。

## 验证

- [x] Unity `6000.3.20f1` 完整 EditMode：`237/237` 通过、失败 `0`、跳过 `0`；结果与日志保存在
  忽略的 `TestResults/CareerWeekPlan-EditMode.*`。
- [x] 仓库无许可证验证与相对 `b89a9ad` 的 Match/Match.meta/Shared V1 冻结检查通过。
- [x] Unity 导入后没有产生其他 tracked 资源或 ProjectSettings 漂移。
- [x] 独立 agent 定点代码复核无 P1/P2。
- [x] PlayMode：纯领域切片不适用。
- [x] 手动场景验证：纯领域切片不适用。
- [x] 序列化/迁移：本切片尚未进入持久化，不适用；稳定 ID 责任已记录为后续门禁。

## 回滚与风险

主要风险是后续恢复已确认计划时必须原样持久化三个稳定 ID，不能由槽位索引或显示名重建。当前类型
使用 `Guid` 与只读属性，后续由正式 Persistence DTO 映射；不得直接依赖 Unity `JsonUtility`。回滚可整体
删除本次新增领域类型和测试，不影响冻结 Match/V1，也没有旧存档迁移风险。
