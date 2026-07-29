# CHG-20260722-007：Career Match V2 端口、映射与 fixture runner

- 日期：2026-07-22
- 状态：已完成
- 负责人：Career / Shared integration
- 影响模块：Career.Application / Career.MatchIntegration / Shared.MatchV2 / Docs / Repository policy
- 交互级别：跨模块（重点）
- 关联分支：`feature/shared-career-fake-contract`
- 关联提交或 PR：本分支 Stage 5A2 实现提交（未推送）
- 最终验证日期：2026-07-23

> [!IMPORTANT]
> Career.Application 新增自有比赛启动/事实 DTO 与异步端口，但不暴露任何 Shared.MatchV2 类型。
> `Volleyball.Career.MatchIntegration` 是唯一同时依赖 Career DTO 与 Shared.MatchV2 的无引擎适配层；
> Match、legacy Shared V1、Persistence、Bootstrap 和现有生涯周流程均未修改。

## 改动摘要

本次完成 Stage 5A2 契约闸门：Career 可以准备完整的室内 6v6 启动数据，通过 mapper 生成
`MatchContextV2`，交给确定性的 fixture-only `IMatchRunnerV2`，验证返回结果确实属于该上下文后，
再映射成 Career 自有的详细比赛事实。此端口尚未被周流程调用；它不保存数据、不结算成长，也不推进周次。

Career.Application 公开端口固定为：

```csharp
Task<CareerMatchFacts> ExecuteAsync(
    CareerMatchLaunch launch,
    CancellationToken cancellationToken);
```

公开 Career DTO 包含六项版本轴、执行模式、赛前重点、赛制、双方 12 人启动快照，以及比分、扣球、
发球、接发、防守、拦网、负荷和稳定性事实。所有集合在构造时防御性复制；阵容构造器独立拒绝默认 ID、
非法值域、队内重复球衣号/轮转位、全局重复球员 ID、错误的 home/away 顺序与非法 6v6 位置拓扑。
对手双方可以合法使用相同球衣号。

由于公开 DTO 直接使用稳定 `PlayerId`/`TeamId`，`Volleyball.Career.Application` 现在直接引用 legacy
`Volleyball.Shared`，不依赖 asmdef 的传递引用；它仍不引用 `Volleyball.Shared.MatchV2`。

## 精确映射规则

`CareerMatchV2Mapper` 对六项版本、session、fixture ID/version、完整 uint32 seed、competition、schedule、
importance、format、priority、team/side、稳定 ID、球衣号、位置与轮转位逐项映射，不推断默认值。
Career 自有五位置枚举使用显式 `switch` 转换，不以整数强转共享枚举。

八项能力只读取 `AbilityBasisPoints`，顺序固定为：

1. spike
2. serve
3. reception
4. defense
5. block
6. movement
7. jump
8. stamina

`GrowthExperience` 不进入比赛上下文；仅改变成长经验时，canonical context bytes 与 `contextHash` 不变。
Career fatigue `0..100` 映射为 Shared fitness basis points：

```text
fitnessBasisPoints = (100 - fatigue) * 100
```

因此 fatigue `0 / 50 / 100` 分别得到 fitness `10000 / 5000 / 0`。反向映射逐项保留全部结果版本、
session、context/result digest、status、winner、逐局比分、rally 数、12 个球员 ID 与所有嵌套事实整数；
毫秒/毫米继续使用 I-JSON-safe `long`，不会截断到 Int32。

`CareerMatchFacts` 自身只校验在没有赛前上下文时仍能独立证明的本地不变量：稳定 ID/digest、状态与 winner
是否存在、局号/完成状态/rally 总和、12 条唯一球员事实及个人 rallies 上限。它不宣称重建 format、胜方赢局、
技术得分上限等完整上下文规则；正式 executor 必须先对原始 `MatchResultV2` 调用
`ValidateAgainst(exactContext)`，通过后才允许映射。

## fixture repository 与动态 runner

`VersionedMatchFixtureRepository` 只接受调用方提供的 canonical context/result byte arrays。它在解析前复制
两组输入，并使用确定性 SHA-256 allowlist 同时锁定 `fixture.career.u1w1.6v6@1`、完整 context file hash
`a33a...b93` 与完整 result file hash `301d...d12`，随后再通过 `MatchContractV2Json` 严格解析并交叉验证。
因此即便 ID/version 不变且替代 context/result 对自洽，只要任一完整文件 byte 改变也会被拒绝。生产代码
不查找路径、不读写文件，构造完成后保持只读；Stage 6/Bootstrap 将另行决定如何注入已提交 bytes。

`FixtureMatchRunnerV2` 只接受 `fixture` 模式，并锁定 canonical template 的完整六项版本元组、fixture
ID/version、seed `25649701`、competition `competition.university.v1`、schedule
`schedule.u1w1.match.01`、format、side、position 与 rotation topology。它允许动态 session、球队/球员 ID、
球衣号、能力、fitness、importance 和 pre-match priority。

事实重绑定键明确为 `(team side, rotationSlot)`，不是 template player ID。完成结果的 winner 经过：

```text
template winnerTeamId -> template team side -> incoming team on that side -> incoming TeamId
```

因此动态球队 ID 不会泄漏 template 的 `team.university.first`。runner 用动态上下文重新构造
`MatchResultV2`，使 context/result hash 均属于真实输入，并在返回前再次验证。相同上下文的串行重试与并发调用
产生 byte-identical canonical result；不同上下文不会共享可变缓存。

## canonical fixture authority

唯一 fixture authority 继续是 Shared.MatchV2 已提交文件：

- context canonical bytes / file SHA-256：
  `4820 / a33aefaef5860e68803fa0d3910638da661e777704d9981e3ffd910719126b93`
- contextHash：`da570cff972d280acb9307edb715bcef88a0f958e75ea615072a5be25edf0527`
- result canonical bytes / file SHA-256：
  `8082 / 301df25404a1358f7a56fdc22008f9f7515b3954e75296c3cf5ffe92a959ad12`
- resultHash：`3fbb03380ce766a7695c7ad7d0697c5c631893d714b229abcf8c7c1017182d98`

Career 测试直接读取这些已提交 bytes；生产 repository 不包含文件系统路径或 fixture 副本。

## 取消与错误语义

- fixture runner 的预取消调用返回带原 token 的 canceled `Task`，不在调用点同步抛出；开始构造合法结果后，
  晚到的取消不会把结果改写成 abandoned。
- Career executor 将同一 token 原样传给 runner，不吞掉取消或普通异常。
- null result、错误 owner context/result、错误 fixture/version/seed/赛程/赛制/版本/拓扑全部失败；没有
  fallback、伪造 completed 或伪造 abandoned。
- runner 没有随机源、clock、`Task.Run`、Unity、Match、Persistence、Bootstrap 或生产文件系统依赖。

## 跨模块交互重点

- 接口提供方：Career.Application 提供 Career-owned port/DTO；Shared.MatchV2 提供 V2 DTO/codec/runner port；
  Career.MatchIntegration 提供双向 mapper、adapter 与 fixture-only runner。
- 接口使用方：Stage 6 的 durable Career orchestrator；未来 physical Match V2 producer 与 quick simulation
  producer 将实现同一 Shared runner port。
- 数据方向：`Career launch -> Career.MatchIntegration -> MatchContextV2 -> IMatchRunnerV2 ->
  MatchResultV2 -> ValidateAgainst -> Career facts`。
- 兼容性：legacy Shared/Match 完全冻结，`ContractVersions.SupportsMatch(2)` 仍为 `false`；现有 V1 消费方
  无需改代码。
- 消费方或后续负责人需要做什么：Stage 6 负责从资源层注入 fixture bytes、建立 `PendingMatch` 和原子结算
  调用点。当前 Match 搭档无需改代码；物理 Match 和 quick simulation producer 后续另行实现。

## 验证

- TDD Application RED：focused Unity 编译因 Career match DTO/port 不存在而失败；最小实现后 Application
  contract tests `18/18` 通过。
- TDD Integration RED：focused Unity 编译因 `Volleyball.Career.MatchIntegration` 与 runner 类型不存在而
  失败；实现 mapper/repository/runner/executor 后，contract + integration tests `31/31` 通过。
- TDD boundary RED：仓库策略未登记新程序集时 `3/4` 通过、1 项按预期失败；登记 Application 直接 Shared
  引用、MatchIntegration 与 test dependencies 后 `4/4` 通过。
- 独立预审 authority RED：同 fixture ID/version 下的合法替代事实/resultHash 曾被 repository 接受，
  单项测试 `0/1`；加入精确完整文件 SHA-256 allowlist 后 `1/1`，并同时拒绝动态 context + 匹配 result
  的另一组自洽 bytes。
- 六项预审证据补强后的最终 focused match-port/adapter suite：`45/45`；其中 Application contracts
  `18/18`、Integration `23/23`、boundary `4/4`。
- 首次完整 Career 回归为 `426/427`，唯一失败是历史 `ModuleBoundaryTests` 仍假设 Application 不引用
  legacy Shared；根因是本次公开稳定 ID 按 binding 新增了直接引用。旧断言已改为要求 legacy Shared、
  同时禁止 Shared.MatchV2，单项复验 `1/1`。
- Unity `6000.3.20f1` 最终验证：focused `45/45`、Shared.MatchV2 `83/83`、完整 Career
  `433/433`、全项目 EditMode `739/739`。
- Python repository policy tests `8/8`；`validate_repository.py` 分别以阶段基线 `e1a56f2` 与批准的
  Stage 5A1 head `b61c807` 执行。最终结果、frozen hashes、forbidden scans 与 status 见 Stage 5A2
  实现报告；不得以本文替代独立 reviewer 的新鲜验证。

## 回滚、风险与明确延期

回滚本次提交会移除 Career-owned match port、MatchIntegration 程序集、相关 tests/policy/docs；未产生存档
迁移或 scene 依赖。主要风险是未来 producer 误把 template ID 或 Career consequence 混进事实层，当前动态
ID/side+slot/边界测试会对此失败。

本阶段明确延期：Persistence 与 Bootstrap 调用点、`PendingMatch`、`SettlementReceipt`、比赛历史、slot-3
执行、周推进、成长/疲劳/心态/信任/伤病后果、物理 Match、quick simulation、随机抽取、UI、账号、云与在线。
Training emphasis 继续是 Career 后续结算输入，不进入 Career launch 或 Shared V2。
