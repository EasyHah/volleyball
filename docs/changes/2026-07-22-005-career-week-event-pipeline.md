# CHG-20260722-005：第一周事件选择与真实持久化赛前管线

- 日期：2026-07-22
- 状态：已完成
- 负责人：Career
- 影响模块：Career.Application / Career.EditModeTests / Docs
- 交互级别：模块内部
- 关联分支：`feature/career-week-execution`
- 关联提交或 PR：`feat(career): complete first-week event pipeline`

## 改动摘要

加入第一周第三个独立命令边界：消费 revision 6 `AwaitingEventChoice` 中已经冻结的
`event.team_meal` 选项后果，在一个完整 CAS revision 中应用所选 growth/status delta、写入事件选择
回执、清除 pending event，并推进到 revision 7 `Planned(nextSlotNumber = 2)`。随后复用既有 slot 2
命令推进到 revision 8 `Planned(nextSlotNumber = 3)`。真实 `LocalCareerSaveRepository` 集成测试由公共
onboarding 命令建立合法 revision 4，且在 4、5、6、7、8 每个边界重新载入 sealed snapshot 并用载入的
完整 token 推进下一步。

合法 immediate backup restore 可以在更高 revision 上保持同一业务 frontier；事件解决不绑定绝对
revision 6，只拒绝已经达到 I-JSON safe maximum、无法再创建完整 next revision 的状态。未加入 slot 3、
`PendingMatch`、Shared V2、Match/FakeMatch、比赛/周末结算、第二周、UI 或未来接口占位。

## 命令与 canonical fingerprint V1

- 新增唯一不可变 `ResolveEventChoiceCommand`，十一项只读字段严格为：`ProfileId`、`SaveId`、
  `ExpectedVersionToken`、`OperationId`、`CompletedAtUtcMs`、`WeekPlanId`、`SourceSlotActionId`、
  `SourceActionOccurrenceId`、`EventId`、`EventOccurrenceId`、`OptionId`。
- `CareerWeekCommandService` 保持唯一构造器
  `(ICareerSaveRepository, IDeterministicCareerRandom)`，公开实例方法严格为 `ConfirmWeekPlan`、
  `ExecuteWeekAction`、`ResolveEventChoice`。
- event-choice fingerprint 的字段顺序固定为：`fingerprintSchemaVersion, operationKind, profileId,
  saveId, expectedLineageId, expectedRevision, expectedSnapshotHash, weekPlanId, sourceSlotActionId,
  sourceActionOccurrenceId, eventId, eventOccurrenceId, optionId, schemaVersion, contentVersion,
  rulesetVersion, careerRandomAlgorithmVersion`。
- independently-authored 完整 UTF-8 vector 为：

```json
{"fingerprintSchemaVersion":1,"operationKind":"resolve_event_choice","profileId":"11111111-1111-1111-1111-111111111111","saveId":"22222222-2222-2222-2222-222222222222","expectedLineageId":"33333333-3333-3333-3333-333333333333","expectedRevision":6,"expectedSnapshotHash":"0000000000000000000000000000000000000000000000000000000000000000","weekPlanId":"44444444-4444-4444-4444-444444444444","sourceSlotActionId":"55555555-5555-5555-5555-555555555555","sourceActionOccurrenceId":"66666666-6666-6666-6666-666666666666","eventId":"event.team_meal","eventOccurrenceId":"00000000-0000-0000-0000-000000000003","optionId":"event.team_meal.option.extra_practice","schemaVersion":1,"contentVersion":1,"rulesetVersion":1,"careerRandomAlgorithmVersion":1}
```

- literal SHA-256：`0b268051bdeb3dd7a9b0999c801240a8bf6d2b31ea69147f521a655fdc820f81`。
  测试先比较完整 bytes 再比较 hash，并覆盖 profile/save、完整 expected token、plan/source/event/option
  identity 与四版本轴；严格字符串转义和 lone surrogate 拒绝沿用现有 canonical encoder。
- `operationId` 只用于回执查找，`completedAtUtcMs` 只属于非业务完成元数据；两者均不进入 bytes/hash，
  单独改变任一项保持同一 fingerprint。

## 冻结事件后果与状态转换

- 命令 shape 在 load 前验证稳定 ID、完整 I-JSON-safe token、非负安全时间及严格非空 event/option ID；
  load 后先查回执，再校验 expected token、归属、四版本、时间与权威 frontier。
- 权威状态必须是完整正式入队 University `AwaitingEventChoice`，确认计划与 slot-1 action receipt 完整；
  命令 plan/source/action occurrence/event occurrence/event ID 必须与 pending event 和当前 slot 1 完全相同。
- schema V1 fixture 必须严格为 `event.team_meal`、`resumeAtSlotNumber = 2`、random version 等于快照，
  且两个有序 option 只能是 `attend` 后 `extra_practice`。未知、换序、替换、重复、错误 source/event、
  不支持版本、非单调时间或错误 progression 均在 commit 前结构化拒绝。
- 选择后不重新打开 `CareerSocialEventCatalogV1`，不调用 `CareerEventRulesV1.Resolve`，也不调用随机依赖；
  persisted `CareerEventOptionEffect` 是 numeric authority。八项非负 growth delta 分别 checked-add 且不超过
  I-JSON safe maximum；八项 ability basis points 原样保留；疲劳/心态/信任使用冻结的实际 delta 做
  checked-add 并要求结果仍在 `[0,100]`，不再次 clamp 或缩放。
- distinctive `1..8` persisted effect 测试逐轴比较 snapshot 的 `prior + frozen delta`、逐轴确认 ability
  不变，并逐轴把 receipt/result summary 与 snapshot 实际差值互相校验；同时锁定实际 status 变化、完整
  emphasis、事件清除与零随机。另以八个 training direction 参数化覆盖每一轴已达 storage maximum、所选
  frozen option 对该轴 `+1` 的拒绝路径，保证漏加或错位到其他轴无法通过。
- 只应用被选 option，完整 `TrainingEmphasisLedger` 原样保留；一次
  `CareerWeekSnapshotFactory.Advance` 清 event 并进入 `Planned(nextSlotNumber = 2)`。新回执 target 含
  plan/source slot/source occurrence/event occurrence/option，outcome 为 `EventChoiceApplied` 且摘要精确复制
  所选冻结 growth/status delta。

## 真实 revision 4 -> 8 管线证据

集成 fixture 使用真实 `LocalCareerSaveRepository`、`SystemAtomicFileSystem`、唯一临时目录、固定业务
ID/时间、真实 `CareerDeterministicRandom` 与固定 seed
`000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f`：

```text
revision 4  Planning
  -- ConfirmWeekPlan --> revision 5  Planned(next = 1)
  -- Execute slot 1 --> revision 6  AwaitingEventChoice
  -- Resolve extra  --> revision 7  Planned(next = 2)
  -- Execute slot 2 --> revision 8  Planned(next = 3)
```

- 每个边界 disk/load hash 非零并等于 `CareerSaveJsonCodec.ComputeSnapshotHash`，disk bytes 等于 canonical
  `Serialize`；下一命令只使用刚 reload 的完整 version token。
- 两个 free slot 都是 Spike 专项训练，B 潜力下 slot 1/2 直接 growth 各 `+120`、疲劳各 `+8`；revision
  6 冻结 attend `growth 0 / +4/+6/+3` 与 extra `Spike +80 / +10/-2/+6`，选择 extra 后 revision 7
  Spike growth 为 `200`，slot 2 后 revision 8 为 `320`。八项 ability 全程不变，其余七项 growth 为零。
- 固定 event occurrence `00000000-0000-0000-0000-000000000003` 下，attend digest/roll 为
  `498a5ad33f7737a79b2d489870aa5b9c32a287a44c5af6d94bad45877cd9de8d / 6791`；extra 为
  `505b9fbea8a2ef2df237fdd58b1e2fe36c81a22af753e6314214795ad0d98318 / 7549`。请求顺序、seed、
  stream、season/week、entity、occurrence、draw、range 均锁定。
- event occurrence 在 revision 6 持久存在、revision 7 原子清除；emphasis 在 revision 6/7 保留首项
  `1000`，revision 8 以两个 source slot ID 有序达到 `1000 + 500 = 1500`。
- 最终八条回执严格按 applied revision `1..8` 排列，operation ID 全局唯一；每条 kind、target、
  canonical fingerprint、lineage、完成时间、outcome 与实际 delta 摘要均逐项验证。
- 四条命令在 reload 后重试均返回 `Existing`，主档 bytes/frontier 不变；slot-1 首次两次 keyed random
  后总调用数始终为 2，事件选择和其他 retry 不再抽取。
- 最终 revision 8 无 pending event、为 `Planned(next = 3)`；Career runtime 未出现名为
  `PendingMatch` 的 type、field 或 property。

## 幂等、CAS 与失败边界

- exact retry（包含只改 completion time）在 token/state 校验前返回既有持久摘要；同 operation ID 改
  option 或其他业务输入返回 `OperationConflict`；其他 stale operation 返回权威 `VersionConflict`。
- CAS conflict 只 reload 一次：exact/conflicting/missing receipt 分别返回
  `Existing/OperationConflict/VersionConflict`，不重复应用 growth/status、清 event、移除 emphasis 或调用
  random。
- `Committed`/`BackupDegraded` 只有在 payload 含 exact operation/fingerprint 回执时才映射 `Applied`。
  initial load、commit 或 race reload 的 null/throw，以及 not-found、not-committed、I/O、lock、malformed
  success/missing receipt 均返回结构化结果，不暴露 speculative revision 7 或 summary。
- confirm、slot 1、event choice、slot 2 的统一 frontier 测试证明：publication 前失败重载旧权威状态；
  并发 winner 已发布而 response 为 conflict 时，通过 persisted receipt 收敛为 `Existing`。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：仅 Career.Application -> Career.Domain -> Career repository CAS
- 兼容性：Career JSON shape、mapper/codec/golden、Schema/Content/Rules/Random version `1` 均不改变
- 消费方或后续负责人需要做什么：Stage 5 可从 revision 8 `Planned(next = 3)` 开始；Match、Shared、UI
  无需改代码

## 验证

- 有效 mutation RED：仅在验证期间临时将 production Serve 应用错接到 Reception delta，focused
  `102` 项中 `100` passed、`2` failed；distinctive snapshot 用例报告 Serve 期望 `24`、实际 `25`，
  Serve maximum 参数用例报告期望 `InvalidInputOrState`、实际 `Applied`。随后以 `apply_patch` 精确恢复
  正确映射；错误 production 不进入提交。证据为
  `TestResults/Stage4B3-Review-Fix-Mutation-Red.xml/.log`。
- Unity service matrix：`85/85` passed。
- Unity focused fingerprint/service/pipeline：`102/102` passed。
- Unity 全部 `Volleyball.Career.EditModeTests`：`388/388` passed。
- Unity 全项目 EditMode：`611/611` passed。
- Python policy：`python -B -m unittest discover -s tools/tests -p "test_*.py" -v`，`8/8` passed。
- repository validator：分别以 `a37f193` 与 Stage 4A base `6ef0a90` 执行，均 passed。
- `git diff --check`、forbidden/future API search 与 frozen path diff 均 passed；提交后再次确认 clean status。

冻结 base `a37f193` 的四项 hash 为：

- `Assets/Volleyball/Match`: `1f0bbe976355ded867dcefadba61d538f77905b9`
- `Assets/Volleyball/Match.meta`: `23d5e66a3e4158bd421c4d3ee573e0d4e7339627`
- `Assets/Volleyball/Shared/Runtime`: `61c7a928f2bf4740defea34c67e5cb108f6dfe76`
- `Assets/Volleyball/Shared/Runtime.meta`: `9085d85a3a423a82a6303df4ca3fe3819d8d30ea`

RED/GREEN 文件与最终验证、自审细节记录在 workspace-only
`.superpowers/sdd/task-4b3-report.md`。

## 回滚与风险

回滚本提交会移除事件选择命令与真实 revision 4 -> 8 集成证据；Schema V1 无字段变化，因此无需迁移或
数据回写。主要风险是调用方复用 operation ID、提交陈旧 token 或持有错误 event identity；服务优先用
权威回执/状态返回显式冲突或无效结果，不覆盖、不重抽也不重复应用。
