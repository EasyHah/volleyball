# CHG-20260722-006：Shared Match V2 规范契约与 golden fixture

- 日期：2026-07-22
- 状态：已完成
- 负责人：Shared / 当前里程碑负责人
- 影响模块：Shared.MatchV2 / Career.MatchIntegration（后续消费） / Match（兼容冻结） / Docs
- 交互级别：跨模块（重点）
- 关联分支：`feature/shared-career-fake-contract`
- 关联提交或 PR：`f87ce2f feat(shared): add canonical match v2 contract`；
  `fix(shared): harden match v2 contract`
- 独立复核修正日期：2026-07-23

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

## V2 schema 的唯一字段顺序

canonical writer 和 strict reader 共用下列完整顺序；任何缺失、多余或换序都是契约错误：

- context root：`versions, sessionId, executionMode, fixtureId, fixtureVersion, matchSeed,
  competitionId, scheduleItemId, importanceBasisPoints, format, preMatchPriority, teams, contextHash`。
- result root：`versions, sessionId, contextHash, status, winnerTeamId, sets, rallyCount,
  playerFacts, resultHash`。
- `versions`：`contractVersion, contentVersion, rulesetVersion, careerRandomAlgorithmVersion,
  matchSimulationVersion, matchRandomAlgorithmVersion`。
- `format`：`kind, teamSize, setsToWin, setTargetPoints, minimumLeadPoints`。
- team：`teamId, side, players`。
- player：`playerId, jerseyNumber, position, rotationSlot, fitnessBasisPoints, abilities`。
- `abilities`：`spikeBasisPoints, serveBasisPoints, receptionBasisPoints, defenseBasisPoints,
  blockBasisPoints, movementBasisPoints, jumpBasisPoints, staminaBasisPoints`。
- set：`setNumber, homePoints, awayPoints, isComplete`。
- player fact：`playerId, spike, serve, reception, defense, block, load, stability`。
- `spike`：`attempts, points, errors`；`serve`：`attempts, aces, errors`。
- `reception`：`attempts, perfect, positive, neutral, negative, errors`。
- `defense`：`attempts, successes`；`block`：`attempts, effectiveTouches, points`。
- `load`：`ralliesPlayed, activeDurationMilliseconds, movementDistanceMillimeters, jumpCount,
  highLoadJumpCount, landingLoadBasisPoints, totalWorkloadBasisPoints`。
- `stability`：`criticalActions, criticalSuccesses, criticalErrors, errorStreakEpisodes,
  longestErrorStreak`。

`contextHash` 是不写入最后 `contextHash` 字段时的 context canonical bytes 之 SHA-256；
`resultHash` 同理不写入最后 `resultHash` 字段。两者均为 64 位小写十六进制。

## 值域、单位与事实口径

- fixture/direct 不携带 Match 算法版本；quick simulation 必须同时携带两个 Match
  算法版本且不携带 fixture 身份。`matchSeed` 覆盖完整 uint32 `0..4294967295`。
- 6v6 阵容按 home 后 away、各自 rotation slot `1..6` 排列；每队恰有 1 二传、2 主攻、
  1 副攻、1 接应、1 自由人。12 个球员 ID 全局唯一；每队球衣号是 `1..99`
  且队内唯一。
- 能力、fitness、importance、landing load 与 total workload 均是 `0..10000` 整数基点。
  active duration 用整数毫秒，movement distance 用整数毫米；规范 payload 不存在浮点。
- reception 分桶互斥且总和必须等于 attempts：`perfect` 是完全到位、可使用全部进攻选项的
  一传；`positive` 是受控且可执行计划进攻、但选项受限；`neutral` 是仅保持可继续处理；
  `negative` 是球仍被救回但对手获得明显优势，例如无攻过网或探头，不含直接丢分；
  `errors` 是一传直接结束该 rally 并丢分。
- defense 是非接发球的防守尝试；`successes` 表示防起后本方仍可继续处理，未成功不等同于
  必然记为个人正式失误。block `effectiveTouches` 是合法且明显改变进攻球路的可观测触球；
  `points` 是其子集，普通拦网尝试或触球不自动记分。
- `ralliesPlayed` 是球员在场参与的 rally 数，不得超过比赛 `rallyCount`。
  `activeDurationMilliseconds` 是有效 rally 内的在场活动时间，不是菜单、暂停或整场墙钟时间；
  `movementDistanceMillimeters` 是同一活动口径下的移动距离。landing load 与 total workload
  是归一化比赛观测，不是 Career 的疲劳、伤病概率或后果。
- `criticalActions` 是 producer 按当前 ruleset 标记的关键分行动；关键成功与关键错误是其子集。
  `errorStreakEpisodes` 计数至少连续 2 次已计入技术错误的极大连续段；无 episode 时
  `longestErrorStreak == 0`，有 episode 时必须 `>= 2`。
- 扣球/发球得分和失误不超过 attempts，拦网得分 ⊆ 有效触球 ⊆ attempts，high-load jump
  不超过 jump，关键成功与错误不超过关键行动。全部加法使用扩宽整数校验，不能以
  Int32 溢出绕过子集约束。
- 当前 V2 **不提供伤病观测**。伤病观测与事件语义延后到有实际消费方时的后续契约版本；
  Career 仍是伤病状态和长期后果的唯一权威。

## 比赛结束不变量

- `completed` 必须有 winner 和至少一局，所有局已完成并满足目标分/最小领先分，winner
  与 `setsToWin` 赢局一致，`rallyCount` 等于逐局双方得分和。
- `abandoned` 必须无 winner；只有两种合法形态：`sets=[]` 且 `rallyCount=0`，或恰好一个
  `isComplete=false` 的局，其 `rallyCount=homePoints+awayPoints`，且当前分数尚未同时满足目标分与
  最小领先分。已完成局、多局、rally 不相等或伪装成未完成的终局分数都必须拒绝。
- 无论状态，每队的扣球得分 + ace + 拦网得分都不得超过该队逐局总分。
  Stage 5A2 Fake fixture runner 只返回已提交的 `completed` golden result，不生成 `abandoned`。

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

上述四个 payload fixture 文件的**完整已提交文件字节就是 canonical codec bytes**，无 BOM、
无尾随 LF 或任何 carrier byte；因此 manifest 中的 file SHA-256 是直接对整个 payload 文件求值。
manifest 自身是常规文本文件，不是 codec payload。测试使用预先锁定的完整 literal bytes
在调用 codec 之前比对四个 payload，并独立验证 manifest 文本与完整文件哈希。

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
- 独立复核回归 RED：`Stage5A1-Review-Red.xml` 为 `59/74` passed、15 failed，直接暴露
  payload 尾随 LF、球衣号 `0`/重号被接受，以及 abandoned rally/终局约束不完整。
- 最终 focused Shared.MatchV2 EditMode：`75/75` passed；legacy Shared EditMode：`6/6` passed；
  完整项目 EditMode：`686/686` passed。
- Python repository policy tests：`8/8` passed；repository validator 以本复核 base `f87ce2f` 与阶段 base
  `e1a56f2` 分别执行，均 passed。
- 四个 payload 先与测试程序中的独立完整 literal bytes 比对，再进入 codec；manifest
  文本、实际整文件 SHA-256、literal 长度/hash 均有分层断言。
- `git diff --check`、frozen path diff/tree hash、forbidden dependency/API 与 clean-status 检查在提交前后
  执行；独立复核前不得以本记录代替 reviewer 的新鲜验证。

## 回滚、风险与延期

回滚本提交会完整移除兄弟程序集、fixtures 与记录，不需要迁移 V1 或 Career 存档。fixture byte 任一变化
都必须提升 fixture version、同步 manifest/literal hashes 与跨模块变更记录。Stage 5A1 明确不实现
Career mapper、Fake runner、PendingMatch、结算、随机抽取、物理场景、quick simulation、Bootstrap、UI、
账号或在线功能；这些延期不能通过在 Shared 添加无调用方字段或默认推断来提前占位。
