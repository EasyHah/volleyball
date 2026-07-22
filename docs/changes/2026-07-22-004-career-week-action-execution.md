# CHG-20260722-004：第一周普通行动执行与固定事件冻结

- 日期：2026-07-22
- 状态：已完成
- 负责人：Career
- 影响模块：Career.Application / Career.EditModeTests / Docs
- 交互级别：模块内部
- 关联分支：`feature/career-week-execution`
- 关联提交或 PR：`feat(career): execute first-week free actions`

## 改动摘要

加入第一周第二个独立命令边界：通常权威 revision 5 的 slot 1 普通行动在一个 CAS revision 中结算直接
后果、可选训练 emphasis、行动回执与已解析但尚未应用的固定 `event.team_meal`，推进到 revision 6
`AwaitingEventChoice`；通常 revision 7 `Planned(nextSlotNumber = 2)` 同样原子结算 slot 2，推进到
revision 8 `Planned(nextSlotNumber = 3)`。合法 immediate backup restore 会因恢复 revision 多加一而从
revision 6/8 分别推进到 7/9；服务依赖完整 expected token 与权威状态/回执 frontier，不绑定绝对
revision。本次不处理事件选择、slot 3 或比赛。

## 契约与 canonical fingerprint

- 新增不可变 `ExecuteWeekActionCommand`，字段固定为：`ProfileId`、`SaveId`、
  `ExpectedVersionToken`、`OperationId`、`CompletedAtUtcMs`、`WeekPlanId`、`SlotNumber`、
  `SlotActionId`、`ActionOccurrenceId`、`ContentId`、`TriggeredEventOccurrenceId`。
- `CareerWeekCommandService` 的唯一公开构造器固定为
  `(ICareerSaveRepository, IDeterministicCareerRandom)`，公开方法只有 `ConfirmWeekPlan` 与
  `ExecuteWeekAction`；已有确认路径在新构造器下仍为零随机调用。
- execute fingerprint V1 字段顺序固定为：`fingerprintSchemaVersion, operationKind, profileId,
  saveId, expectedLineageId, expectedRevision, expectedSnapshotHash, weekPlanId, slotNumber,
  slotActionId, actionOccurrenceId, contentId, triggeredEventOccurrenceId, schemaVersion,
  contentVersion, rulesetVersion, careerRandomAlgorithmVersion`。slot 2 的事件发生 ID 明确写 JSON `null`。
- `operationId` 只用于查回执，`completedAtUtcMs` 只属于非业务提交元数据，两者排除在 fingerprint 外。
  slot-1/non-null canonical SHA-256 为
  `ad62ae072ff9cbefecb9934d18a4456fa99c4e9467a228bbdae3891e3cb1cd88`；slot-2/null 为
  `ce66e7b0fe43edf4c833e99a050e0d765cd12c8fdba5237a4d4b981999162852`。测试先锁完整独立 UTF-8 bytes，
  再锁 literal hash，并覆盖 expected token、plan/slot/action occurrence/content/event identity 与 slot
  number 的业务敏感性。

## 行动、emphasis 与事件结果

- 权威校验完成后仅调用一次 `CareerWeekActionRulesV1.Calculate`，reason 固定为
  `reason.week_action`。直接 XP、疲劳、心态、教练信任均保存实际截断值；行动回执中的
  `SlotCompleted` 摘要使用相同实际 delta。
- 专项与力量训练各追加一个 `TrainingEmphasisLedger` contribution；同方向第一/第二次贡献保持
  `1000/500` basis points，不同方向各保留 `1000`。团队合练与休息保持 ledger 不变；emphasis 不削减
  行动直接 XP 或状态结果。
- slot 1 只创建 `event.team_meal`，固定选项顺序为
  `event.team_meal.option.attend`、`event.team_meal.option.extra_practice`。两项分别以相同 post-action
  属性/状态为基准解析，第一项不会改变第二项基准；解析后果冻结到 `PendingCareerEvent`，但尚不应用。
- 固定 seed `000102...1f`、发生 ID `00000000-0000-0000-0000-000000000003`、season/week `1/1`、
  draw/attempt `0` 下：attend digest
  `498a5ad33f7737a79b2d489870aa5b9c32a287a44c5af6d94bad45877cd9de8d`、roll `6791`，请求/实际
  状态为 `+4/+6/+3`；extra-practice digest
  `505b9fbea8a2ef2df237fdd58b1e2fe36c81a22af753e6314214795ad0d98318`、roll `7549`，Spike 基础
  XP `+80`（按潜力缩放/上限）及状态 `+10/-2/+6`。插入无关键控请求不改变两项冻结结果。

## 原子状态、幂等与失败语义

- slot 1 从 revision 5 一次推进到 revision 6：行动直接后果、可选 emphasis、一个执行回执、两项冻结
  event effect 与 `AwaitingEventChoice(resumeAtSlotNumber = 2)` 同生共死。
- slot 2 的合法测试 frontier 含 slot-1 行动与已解决事件回执；它从 revision 7 一次推进到 revision 8，
  保存 slot-2 后果/可选第二 emphasis/一个执行回执并进入 `Planned(nextSlotNumber = 3)`。它零随机、无
  pending event，也不创建 `PendingMatch`。
- immediate restore 使用新 lineage、`source revision + 1` 与完整 `RestoredFromVersionToken`；slot 1/2
  执行保留恢复来源与新 lineage，新回执写入新 lineage 及实际 next revision。错误 progression、next
  slot、plan/action identity 或缺失 slot-1/event receipt 的状态仍会在随机与 commit 前拒绝。
- 每个命令恰好一次 repository CAS。load 后先查 operation receipt：exact 返回 `Existing`，同 ID 不同
  fingerprint 返回 `OperationConflict`，其他陈旧 token 返回权威 `VersionConflict`。CAS race 只 reload
  一次并按 exact/conflicting/missing receipt 收敛，不自动重算或重试；slot-1 race 最多保留提交前已发生
  的两次 keyed random call，reload 后不会再抽。
- `Committed` 与 `BackupDegraded` 只在返回 payload 含相同 operation/fingerprint 回执时映射为
  `Applied`。load/commit/race reload 的 null、异常、I/O、lock、not-committed 或 malformed result 均返回
  结构化失败；不会暴露推测 revision、outcome、XP、状态、emphasis 或 event。
- `_random.NextInt64` 是独立基础设施边界。该依赖抛出的 `IOException`、`InvalidOperationException` 或
  其他异常只由私有 marker 包装并映射为 `PersistenceFailure`：返回 prior authoritative snapshot、
  `PersistenceKind = null`，不返回 outcome/conflict，不 commit，也不重抽。确定性 Build 本身不再被宽
  `InvalidOperationException` catch 覆盖，规则/编程错误不会被误标为随机依赖失败。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：仅 Career 内部 Application -> Domain -> repository CAS
- 兼容性：不改变 Career JSON shape、mapper/codec/golden 或 Schema/Content/Rules/Random 版本
- 消费方或后续负责人需要做什么：Stage 4B3 消费 revision 6 的 frozen event；Match、Shared 与 UI 无需改代码

## 验证

- [x] contract/fingerprint：exact public surface、两条 complete UTF-8 vector/literal hash、敏感字段与
  `operationId`/时间排除项
- [x] slot 1/2：四类直接后果、same/different emphasis、两条 event random vector、同一 post-action
  basis、revision `5 -> 6` / `7 -> 8` 以及 immediate restore `6 -> 7` / `8 -> 9`
- [x] CAS/幂等/失败：Applied、BackupDegraded、Existing、OperationConflict、VersionConflict、两种
  transition 的三类 race、null/throw/malformed/not-committed/I/O/lock
- [x] 边界：Confirm 零随机、slot 2 零随机、无 event application、无 slot 3/PendingMatch/future shell

最终验证命令与结果直接记录如下：

- `E:\UnityEditor\6000.3.20f1\Editor\Unity.exe -batchmode -projectPath
  C:\Users\chen\Documents\球队经理\volleyball -runTests -testPlatform EditMode -testFilter
  'Volleyball.Career.EditModeTests.CareerWeekOperationFingerprintV1Tests;Volleyball.Career.EditModeTests.CareerWeekCommandServiceTests'`
  — focused `68/68` passed。
- 同一 Unity 命令使用 `-testFilter 'Volleyball.Career.EditModeTests'` — Career `354/354` passed。
- 同一 Unity 命令省略 `-testFilter` — full EditMode `577/577` passed。
- `python -B -m unittest discover -s tools/tests -p "test_*.py" -v` — `8/8` passed。
- `python -B tools/validate_repository.py --base c09fc43` — repository validation passed。
- `git diff --check c09fc43..HEAD`、forbidden/future API search 与 frozen path diff — clean。

Frozen `c09fc43` base 与 review-fix HEAD 的四项 hash 完全相同：

- `Assets/Volleyball/Match`: `1f0bbe976355ded867dcefadba61d538f77905b9`
- `Assets/Volleyball/Match.meta`: `23d5e66a3e4158bd421c4d3ee573e0d4e7339627`
- `Assets/Volleyball/Shared/Runtime`: `61c7a928f2bf4740defea34c67e5cb108f6dfe76`
- `Assets/Volleyball/Shared/Runtime.meta`: `9085d85a3a423a82a6303df4ca3fe3819d8d30ea`

完整 RED/GREEN 日志文件名与自审细节另记录于 workspace-only
`.superpowers/sdd/task-4b2-report.md`。

## 回滚与风险

回滚本提交会移除 revision 5/7 普通行动执行边界；由于未改变 Schema V1，无迁移或数据回写。主要风险
是调用方复用 operation ID、事件 occurrence 或陈旧 version token；服务在随机与提交前验证这些输入，
并以显式 conflict/invalid 结果保留旧权威快照。
