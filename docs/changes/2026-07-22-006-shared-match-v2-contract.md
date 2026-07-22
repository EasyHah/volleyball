# CHG-20260722-006：Shared Match V2 规范契约与 golden fixture

- 日期：2026-07-22
- 状态：已完成
- 负责人：Shared / 当前里程碑负责人
- 影响模块：Shared.MatchV2 / Career.MatchIntegration（后续消费） / Match（兼容冻结） / Docs
- 交互级别：跨模块（重点）
- 关联分支：`feature/shared-career-fake-contract`
- 关联提交或 PR：`feat(shared): add canonical match v2 contract`

> [!IMPORTANT]
> 本次没有修改 legacy `Volleyball.Shared` V1 或 Match。新增兄弟程序集
> `Volleyball.Shared.MatchV2`，为 Career fixture adapter 与未来比赛 producer 提供独立 V2 DTO、严格
> canonical codec 和异步 `IMatchRunnerV2`。`ContractVersions.SupportsMatch(2)` 继续为 `false`；任何
> V1 消费方都不能把 V2 当作 V1 载荷接受。

## 改动摘要

新增无 Unity 引用的 `Volleyball.Shared.MatchV2` 程序集与命名空间
`Volleyball.Shared.Contracts.V2`。上下文冻结版本轴、session、执行模式、fixture 身份、uint32
`matchSeed`、赛程身份、重要度、6v6 赛制、赛前重点及双方 12 人的整数能力。结果提供结束状态、逐局
比分、rally 数和按上下文顺序排列的 12 人细分事实；Shared 只记录比赛观察，不计算成长、疲劳、心态、
信任、伤病或周推进后果。

公开 producer 端口只有：

```csharp
Task<MatchResultV2> ExecuteAsync(
    MatchContextV2 context,
    CancellationToken cancellationToken);
```

本阶段只定义端口，不提供 runner 实现。fixture runner、Career 自有端口和双向 mapper 属于 Stage 5A2。

## V2 schema 与计数口径

- 上下文字段顺序固定为 `versions, sessionId, executionMode, fixtureId, fixtureVersion, matchSeed,
  competitionId, scheduleItemId, importanceBasisPoints, format, preMatchPriority, teams, contextHash`；hash
  只排除最后的自身字段。
- 结果字段顺序固定为 `versions, sessionId, contextHash, status, winnerTeamId, sets, rallyCount,
  playerFacts, resultHash`；hash 同样只排除自身。
- 版本轴顺序固定为 `contract/content/ruleset/careerRandom/matchSimulation/matchRandom`。fixture/direct
  不携带 Match 算法版本；quick simulation 必须同时携带两个 Match 算法版本且不携带 fixture 身份。
- 6v6 阵容按 home 后 away、各自 rotation slot `1..6` 排列；每队恰有 1 二传、2 主攻、1 副攻、
  1 接应、1 自由人，12 个球员 ID 全局唯一。
- 能力、fitness、importance 与归一化负荷均是 `0..10000` 整数基点；时间用毫秒、距离用毫米；规范
  payload 无浮点。
- reception 的 `perfect + positive + neutral + negative + errors == attempts`；扣球/发球得分和失误不
  超过 attempts，拦网得分 ⊆ 有效触球 ⊆ attempts，high-load jump 不超过 jump，关键成功与错误不超过
  关键行动。全部加法使用扩宽整数校验，不能以 Int32 溢出绕过子集约束。
- completed 结果要求合法目标分/领先分、winner 与赢局一致，且 rallyCount 等于逐局双方得分和；每队
  扣球得分 + ace + 拦网得分不得超过该队比分。abandoned 无 winner，只允许无局或一个未完成局。

## canonical JSON、fixture 与 literal hashes

codec 以 UTF-8 byte array 为边界；输出无 BOM、无 token 空白、字段顺序固定，枚举为规定的小写 ASCII，
只允许 I-JSON-safe 十进制整数。字符串按 RFC 8785/I-JSON scalar 规则转义，不转义 `/`，非控制 Unicode
直接编码且不正规化。反序列化先严格解析并拒绝重复/未知/缺失/换序字段、无效 UTF-8、非法代理项、
float/exponent/leading zero、非规范转义与尾随 token，再重建并验证不变量、常量时间比较 hash，最后做
完整 canonical byte comparison。

V2 fixture `fixture.career.u1w1.6v6@1`：

- session：`55555555-5555-5555-5555-555555555555`
- match seed：`25649701`
- canonical context bytes：`4820`
- context file SHA-256：`a33aefaef5860e68803fa0d3910638da661e777704d9981e3ffd910719126b93`
- `contextHash`：`da570cff972d280acb9307edb715bcef88a0f958e75ea615072a5be25edf0527`
- canonical result bytes：`8082`
- result file SHA-256：`301df25404a1358f7a56fdc22008f9f7515b3954e75296c3cf5ffe92a959ad12`
- `resultHash`：`3fbb03380ce766a7695c7ad7d0697c5c631893d714b229abcf8c7c1017182d98`
- 比分/rallies：`25:21 / 46`，12 条事实；主角是 home slot 2 唯一
  `player.career.protagonist`，八项能力为 `7123/6234/7345/6456/7567/6678/7789/6890`。

V1 `Physical3v3Rally` compatibility fixture：

- context bytes/file SHA-256：`1665 / c042a2af98d9dbcaa5b455924e9e8bb1ca6ea2b9a46a06a4c86efe890bd4d646`
- V1 `contextHash`：`c15741e3e6509130e9249a6ccaf9f9ef05f9593403571c6feb57b33f6e9291da`
- result bytes/file compatibility SHA-256：
  `711 / f1de13c7e040fd454875cbcef6545d1c40f242dd0063f64682a190a2fa23af8a`
- result 文件指纹不是 V1 `resultHash`；V1 本身没有该字段。

仓库文本文件统一带一个 LF 终止符；fixture 测试先精确验证该 carrier 约束，再剥离这一个非载荷终止符，
之后才进行上面的 literal byte length/hash、codec 和 byte-for-byte round-trip 验证。运行时 canonical API
本身从不接收或输出该终止符。

## 跨模块交互重点

- 接口提供方：Shared.MatchV2 提供 immutable DTO、strict byte codec、独立版本检查和 `IMatchRunnerV2`。
- 接口使用方：Stage 5A2 的 Career.MatchIntegration/Fake fixture runner；未来物理 Match V2 producer 与
  quick simulation producer。
- 数据方向：未来为 `Career-owned launch -> Career.MatchIntegration -> MatchContextV2 -> producer ->
  MatchResultV2 -> Career.MatchIntegration -> Career-owned facts`。
- 兼容性：legacy Shared/Match 未改，V1 3v3 fixture 通过冻结 `ContractJson` 往返；V2 有独立程序集、
  命名空间、版本 authority、fixture 和 hash。
- 消费方或后续负责人需要做什么：Stage 5A2 必须直接消费同一组已提交 V2 canonical bytes/hash，不得
  复制或重新生成事实。当前 Match 无需改代码；物理 V2 producer、能力绑定、AI seed 与真实技术事件在
  5B 另行实现。

## 验证

- TDD 初始 RED：Shared V2 类型尚不存在时，focused Unity 编译以缺失命名空间/16 类 public type 失败；
  随后另有 default stable-ID 与整数溢出 mutation RED，均在生产修复前真实失败。
- focused Shared.MatchV2 EditMode：`57/57` passed；legacy Shared EditMode：`6/6` passed；完整项目
  EditMode：`668/668` passed。
- Python repository policy tests：`8/8` passed；repository validator 以 `e1a56f2` 与长期 Stage 4A base
  `6ef0a90` 分别执行，均 passed。
- `git diff --check`、frozen path diff/tree hash、forbidden dependency/API 与 clean-status 检查在提交前后
  执行；独立复核前不得以本记录代替 reviewer 的新鲜验证。

## 回滚、风险与延期

回滚本提交会完整移除兄弟程序集、fixtures 与记录，不需要迁移 V1 或 Career 存档。fixture byte 任一变化
都必须提升 fixture version、同步 manifest/literal hashes 与跨模块变更记录。Stage 5A1 明确不实现
Career mapper、Fake runner、PendingMatch、结算、随机抽取、物理场景、quick simulation、Bootstrap、UI、
账号或在线功能；这些延期不能通过在 Shared 添加无调用方字段或默认推断来提前占位。
