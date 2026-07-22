# CHG-20260722-003：第一周计划原子确认

- 日期：2026-07-22
- 状态：已完成
- 负责人：Career
- 影响模块：Career.Domain / Career.Application / Career.EditModeTests / Docs
- 交互级别：模块内部
- 关联分支：`feature/career-week-execution`
- 关联提交或 PR：`feat(career): add atomic week plan confirmation`

## 改动摘要

加入第一周第一个独立命令边界：调用方提交完整、不可变的候选计划，Career 在一次 CAS 中把已正式
入队的 revision 4 `Planning` 快照推进为 revision 5 `Planned(nextSlotNumber = 1)`，并在同一快照保存
规范 `ConfirmWeekPlan` 回执。未加入行动执行、事件、随机依赖、`PendingMatch`、Match/Shared/UI 或未来
命令占位。

## 具体变更

- `ConfirmWeekPlanCommand` 的必需只读字段固定为：`ProfileId`、`SaveId`、
  `ExpectedVersionToken`、`OperationId`、`CompletedAtUtcMs`、`CandidatePlan`；构造时重新构建候选
  `CareerWeekPlanState` 及其槽位，绝不保留调用方对象。
- `CareerWeekCommandResult` 的只读字段固定为：`Status`、`PersistenceKind`、`Snapshot`、
  `ConflictingReceipt`、`OutcomeSummary`。`Applied/Existing` 返回持久回执中的
  `WeekPlanConfirmed` 摘要；无匹配成功回执的无效、持久化失败和冲突结果不伪造摘要。
- 确认 fingerprint V1 的字段顺序固定为：
  `fingerprintSchemaVersion, operationKind, profileId, saveId, expectedLineageId,
  expectedRevision, expectedSnapshotHash, planId, season, week, slots[], schemaVersion,
  contentVersion, rulesetVersion, careerRandomAlgorithmVersion`。每个槽位固定为
  `slotActionId, occurrenceId, kind, contentId`，kind 标识只允许
  `specialized_training/strength_training/team_practice/rest/match`。
- `operationId` 仅作为回执查找键，`completedAtUtcMs` 仅作为提交元数据；两者明确排除在
  `inputFingerprint` 之外。固定 canonical UTF-8 vector 的 SHA-256 为
  `08596f8a683fea343de747058bd8d8cdbfd3d8ec34308212a70f2aaf590516fe`。改变 expected token、
  计划/槽位/发生/content/kind/顺序或版本字段会改变规范载荷或 hash。
- 成功路径严格验证当前四版本组合、调用时间、归属、正式入队完整状态、空 emphasis ledger、当前
  未确认计划、三槽完整候选、封闭行动目录、全局 occurrence 防碰撞及保留的 slot 3 正式比赛，然后
  由 `CareerWeekSnapshotFactory.Advance` 复制全部无关状态、写零 hash 占位、只递增一次 revision。
- 新回执固定使用调用方 `operationId`、`ConfirmWeekPlan`、候选 `planId`、规范 fingerprint、当前
  lineage、revision 5、调用方时间及空的 `WeekPlanConfirmed` 摘要。服务没有随机依赖，确认路径零随机
  调用、零中间快照、恰好一次 repository `Commit`。
- 命令先计算 fingerprint 并加载权威快照，再先查回执后查 expected token/状态。相同 operation ID 与
  fingerprint 返回 `Existing`；同 ID 不同业务 fingerprint 返回 `OperationConflict`；其他陈旧命令返回
  `VersionConflict`。CAS race 后重载并按 exact/conflicting/missing receipt 分别收敛为
  `Existing/OperationConflict/VersionConflict`；未提交或 I/O 失败只返回旧权威 revision 4。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：仅 Career 内部 Application -> Domain -> repository CAS
- 兼容性：不改变 Career JSON shape、Schema/Content/Rules/Random 版本或既有 golden
- 消费方或后续负责人需要做什么：Stage 4B2 以 revision 5 `Planned(nextSlotNumber = 1)` 为权威输入；
  无需修改 Match、Shared 或 UI

## 验证

- [x] EditMode 测试（Unity `6000.3.20f1`：focused `17/17`、Career `303/303`、全项目 `526/526`）
- [ ] PlayMode 测试（纯 Career Domain/Application 命令边界不需要）
- [ ] 手动场景验证（无 UI/Scene 变更）
- [x] canonical fingerprint：固定 bytes/hash、五种 kind、成员/槽位顺序、业务敏感字段，以及
  `operationId`/`completedAtUtcMs` 排除项
- [x] CAS/幂等性：Applied、Existing、OperationConflict、陈旧版本、三种 commit race、
  NotCommitted/I/O/lock failure 与 BackupDegraded

Python policy 测试、repository validator、diff/禁用 API/frozen checks 的最终证据记录在
`.superpowers/sdd/task-4b1-report.md`。冻结树保持 reviewed base `6ef0a90` 的四个 hash：

- `Assets/Volleyball/Match`: `1f0bbe976355ded867dcefadba61d538f77905b9`
- `Assets/Volleyball/Match.meta`: `23d5e66a3e4158bd421c4d3ee573e0d4e7339627`
- `Assets/Volleyball/Shared/Runtime`: `61c7a928f2bf4740defea34c67e5cb108f6dfe76`
- `Assets/Volleyball/Shared/Runtime.meta`: `9085d85a3a423a82a6303df4ca3fe3819d8d30ea`

## 回滚与风险

回滚本提交会移除 revision 4 -> 5 的正式确认边界；既有 Stage 4A Schema V1 数据无需迁移。主要风险是
调用方错误重用 operation ID；服务会保留既有权威回执并显式返回 `OperationConflict`，不会覆盖或重复
确认。
