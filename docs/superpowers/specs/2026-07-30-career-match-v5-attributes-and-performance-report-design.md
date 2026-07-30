# Career x Match V5 属性与逐球员事实报告设计

- 日期：2026-07-30
- 状态：待双方确认
- 范围：Career / Match / Shared / Replay / Bootstrap
- 前置基线：`origin/main@028e263`
- 对应任务单：
  `docs/handoffs/active/2026-07-30-career-match-v5-attribute-contract-design.md`

## 1. 结论

本阶段不直接一次性替换 V4。后续实现拆成两个独立切片：

1. **切片 A：`MatchPerformanceReportV1`**
   - 保持 `MatchContextV4`、`MatchResultV4` 和 `MatchReplayV4` 不变；
   - Match 从实际物理事件累计逐球员事实；
   - 报告以 `sessionId + contextHash + resultHash` 绑定现有 V4 结果；
   - Career 以报告中的真实事实结算成长，不再从总得分、总触球和位置估算技术表现。
2. **切片 B：V5 球员输入合同**
   - 把 Career 明示能力调整为六项动作技术和一项运动能力；
   - 六项技术直接进入对应比赛动作，运动能力只进入移动、起跳和协调；
   - 原始疲劳留在 Career，只向 Match 传一次性 `readiness`；
   - 位置和培养方向不得覆盖玩家传入的能力。

切片 A 是第一个实现里程碑。它能先完善 Career 成长闭环，而且不要求立即迁移存档或重写全部
V4 合同。切片 B 只有在字段、消费路径和迁移方案确认后才进入代码实现。

## 2. 不变边界

- Career 拥有成长、潜力、培养方向、疲劳原值、伤病、心态、教练信任及成长后果。
- Match 只消费冻结的比赛输入，并报告比赛中实际发生的事实。
- Shared 只承载版本化 DTO、规范序列化、哈希和跨模块校验。
- Bootstrap 负责场景生命周期和跨模块编排。
- Match 与 Shared 不引用 Career；Career Domain/Application 不引用 Match 实现。
- 直接物理比赛不承诺逐帧确定性；相同事实流的报告聚合和快速模拟必须确定性可复现。

以下内容仍不进入本阶段：多局制、换人、自由人替换、海外联赛、在线功能和副职业实现。

## 3. V4 当前问题

### 3.1 输入问题

Career 有八项明示能力：

```text
扣球、发球、接发、防守、拦网、移动、弹跳、体能
```

V4 却要求 15 项 physical/technical base 字段。当前 Mapper 通过混合八项能力来填充 V4，并把疲劳
同时乘进每个字段。主要缺口是：

- 身高、站立摸高和惯用手是固定默认值，不是球员权威资料；
- `Reaction`、`Coordination`、`SetTechnique`、`SoftTouch` 和 `CourtAwareness` 被当作 base，
  但 Career 没有独立来源；
- 体能只被混入 `CourtAwareness`，没有表达真实的持续作战能力；
- Match 无法解释某项最终能力是基础成长、疲劳还是混合公式造成的。

这不仅是字段数量问题，也使双方联调缺少简单的因果关系。例如当前 `Reaction` 由接发和防守平均，
`Coordination` 又混合移动、接发和防守，随后这些中间值再次进入多个比赛属性。搭档很难只改变一项
选手数据并判断它应影响哪个比赛动作。

### 3.2 输出问题

`PlayerMatchStatsV4` 只有：

```text
points、contacts、errors、workload
```

这些字段无法证明得分来自扣球、发球或拦网，也无法区分接发与防守。正式 V4 Mapper 只能把技术事实
置零；旧 fixture 的按位置拆分仅用于恢复兼容，不是正式比赛统计。

### 3.3 同类经理游戏的启示

同类产品通常允许底层模拟保持细致，但不会要求玩家管理与模拟字段完全同构的大量数字：

- [CS Manager](https://store.steampowered.com/app/3418500/CS_Manager/) 把阵容决策集中在角色、地图、
  位置、指挥和战术倾向，并把个性、语言与团队默契作为独立管理因素；
- [Esports Manager 2026](https://store.steampowered.com/app/2749950/Esports_Manager_2026/) 将每周训练的
  技能成长与选手身心状态分开管理；
- [Football Manager 2024 Console](https://www.footballmanager.com/features/football-manager-2024-console-new-features-unveiled)
  即使底层属性很多，训练界面仍只突出该训练单元最相关的五项属性。

因此本项目不照搬这些游戏的具体属性数量，而采用相同的信息分层：Career 只维护玩家需要理解和培养
的少量长期能力；Match 可以从这些输入生成更细的执行参数，但派生规则必须固定、透明，并能通过
Replay 证明实际消费。

## 4. 切片 A：MatchPerformanceReportV1

### 4.1 顶层合同

新增独立 Shared 合同，不修改 V4 result：

```text
MatchPerformanceReportV1
  contractVersion = 1
  aggregationRulesVersion = 1
  sessionId
  contextHash
  resultHash
  playerReports[12]
  reportHash
```

约束：

- `sessionId/contextHash/resultHash` 必须与已验证的 Match result 完全一致；
- 正式 6v6 必须恰好覆盖 context 中的 12 名球员，按 `PlayerId` 规范排序；
- `reportHash` 由 Shared 对固定字段顺序的规范 JSON 自动计算；
- 禁止默认 JSON 序列化、扩展袋、可空技术分类和调用方手工写哈希；
- 同一个 `resultHash` 若出现不同 `reportHash`，Career 必须判定为冲突，不得重复或择一结算。

### 4.2 每名球员的最小事实

```text
PlayerPerformanceReportV1
  playerId
  spike:
    attempts, points, errors
  serve:
    attempts, aces, errors
  reception:
    attempts, perfect, positive, neutral, negative, errors
  defense:
    attempts, successes
  block:
    attempts, effectiveTouches, points
  setting:
    attempts, successful, errors
  load:
    ralliesOnCourt
    activeDurationMilliseconds
    movementDistanceMillimeters
    jumpCount
    highLoadJumpCount
    landingLoadBasisPoints
    totalWorkloadBasisPoints
  stability:
    criticalActions
    criticalSuccesses
    criticalErrors
    errorStreakEpisodes
    longestErrorStreak
```

所有计数使用非负整数；时长和距离使用 I-JSON 安全范围内的非负整数；负荷使用 `[0,10000]`
基点。质量桶之和必须等于接发尝试数，成功数不得超过尝试数，拦网得分必须是有效触球的子集，二传
成功与失误都必须是二传尝试的子集且不能重叠。

### 4.3 事实来源

报告必须由一个 Match-owned `MatchPerformanceAccumulatorV1` 在运行时累计。Replay recorder 和
报告累计器消费同一批已接受事实，不能分别从场景状态猜测结果。

| 报告字段 | 权威事实来源 |
| --- | --- |
| 扣球尝试 | 合法 `Attack` 动作进入执行窗口；接受接触或可归责的终止失败均只计一次 |
| 扣球得分/失误 | 该进攻后的回合归属，且在对方下一次已接受接触前直接结束 |
| 发球尝试 | 实际发球启动事件；无论后续成功、触网或出界均只计一次 |
| Ace/发球失误 | 发球后的回合归属及对方是否产生已接受接触 |
| 接发尝试 | 对方发球后被冻结为主接发责任且实际进入接触窗口的球员 |
| 接发质量 | 实际第一传落点与冻结组织目标的水平距离分桶 |
| 防守尝试 | 非发球接发阶段声明并实际执行的 floor-defense 尝试 |
| 防守成功 | 防守接触被规则接受且回合继续 |
| 拦网尝试 | 已提交并进入接触窗口的合法 `Block` 动作 |
| 有效拦网触球 | 球实际接触拦网面且产生可验证轨迹变化 |
| 拦网得分 | 该触球直接导致拦网方得分 |
| 二传尝试 | 合法组织动作进入执行窗口；接受接触或可归责的终止失败均只计一次 |
| 二传成功/失误 | 组织接触被规则接受且产生可执行进攻窗口，或直接终止本方回合 |
| 上场与负荷 | 场上阵容时间、实际移动轨迹、起跳/落地事件和动作负荷累计 |
| 稳定性 | 按事件顺序和比分状态聚合，不读取 Career 心态或教练信任 |

未发生的事实写零。无法证明的事实不能通过位置、总触球或总得分补齐。

`aggregationRulesVersion == 1` 的接发质量分桶为：

```text
perfect:  distance <= 0.75m
positive: 0.75m < distance <= 1.50m
neutral:  1.50m < distance <= 2.75m
negative: distance > 2.75m，但本次接触被接受且回合继续
error:    接发直接导致对方得分，或接触被规则拒绝
```

同一次接发只能进入一个桶。阈值变化必须升级 aggregation rules version 和 golden vector。

### 4.4 关键动作定义

`aggregationRulesVersion == 1` 时，先使用动作发生前的比分判断当前回合是否关键。满足任一条件即为
关键回合，该回合内可归责动作计为关键动作：

- 任一方达到 20 分且分差不超过 2；
- 主队再得 1 分会按冻结的 target/minimum-lead 规则结束本局；
- 客队再得 1 分会按冻结的 target/minimum-lead 规则结束本局。

关键成功和关键失误必须是关键动作的子集。连续失误按同一球员自己的可归责动作序列计算：该球员
发生可归责失误时递增，在该球员下一次非失误可归责动作时清零，其他球员动作不改变该球员的当前
失误串。一次失误串首次达到 2 时记一个 episode。

### 4.5 Result、Replay 与报告的关系

- Result 是比分与结算身份的规范事实；
- Performance Report 是 Career 消费的逐球员聚合事实；
- Replay 是逐事件解释和调试证据。

三者共享 `sessionId/contextHash/resultHash`。切片 A 不给 V4 Replay 增加字段；实现测试必须证明报告
与同一事实流生成的 Replay 在 actor、action、accepted/error 和事件顺序上相容。

Runner 的新版本化产物端口应一次返回：

```text
resultV4 + replayV4 + performanceReportV1
```

Career 只在 result 和 report 都验证通过后进行一次原子结算。Replay 可以由 Bootstrap/Match 保存，
但不能代替 Career 所需的报告。

## 5. 切片 B：V5 球员输入合同

### 5.1 V5 通用核心能力

`PlayerSnapshotV5` 保留身份、球衣号和位置，但通用成长能力改为七项 `[0,1]` 输入：

```text
VolleyballCoreAttributesV5
  Spike
  Serve
  Reception
  Defense
  Block
  Setting
  Athleticism
```

其中六项是排球动作技术，`Athleticism` 是唯一的长期运动能力。Career 明示数值仍为 `0–100`；
跨模块规范值使用 `[0,1]`。独立 `Setting` 是覆盖完整 12 人阵容所必需的：如果继续用接发代填二传，
不同二传手就无法拥有独立比赛能力。

| Career 明示属性 | V5 核心字段 | Match 权威消费 |
| --- | --- | --- |
| 扣球 | `Spike` | 扣球方向、速度和触球控制 |
| 发球 | `Serve` | 发球方向、速度和稳定性控制 |
| 接发 | `Reception` | 接发第一触与接发判断 |
| 防守 | `Defense` | 地面防守触球与防守判断 |
| 拦网 | `Block` | 拦网时机和手型控制 |
| 组织 | `Setting` | 二传落点、节奏和触球控制 |
| 运动能力 | `Athleticism` | 移动、起跳和协调 |

Mapper 对六项技术只做 `/10000` 转换，禁止先混合多个 Career 能力再生成技术输入。运动能力允许
一对多进入 Match 的物理参数，但不得与技术项平均成新的隐藏“综合能力”。例如扣球执行只读
`Spike`，助跑速度和起跳上限只读 `Athleticism`；一次扣球的最终效果可以同时受技术执行和物理过程
影响，但两条因果链在 Match 内保持独立。

### 5.2 玩家显示与培养口径

球员详情页显示七项精确的 `0–100` 数值。总评可以作为角色或培养方向相关的摘要，但不得作为 Match
输入，也不得反向覆盖七项能力。潜力继续使用模糊评级；培养方向只调整七项能力的成长效率。

### 5.3 球员身体资料

以下字段属于球员身份资料而不是成长能力：

```text
HeightMeters
StandingReachMeters
DominantHand
```

新生涯必须在创建球员时由 Career 生成并保存。旧档迁移可以明确采用 V4 默认值
`1.90m / 2.42m / Right`，但迁移决定属于后续存档设计，不能在 Match 内静默补默认值。

### 5.4 Readiness 与疲劳边界

原始疲劳继续是 Career 的 `0–100` 状态，不进入 Shared。Career 在冻结 context 时产生：

```text
readiness = 0.75 + 0.25 * (1 - fatigue / 100)
```

`PlayerSnapshotV5.Readiness` 使用 `[0,1]`，首版 Career 实际只产生 `[0.75,1]`。Match 在执行时统一
应用一次：

```text
effectiveInput = input * readiness
```

Match 不读取原始疲劳，不再次扣减 readiness；比赛中的能量消耗是独立的动态状态，不回写
`PlayerSnapshotV5`。伤病若只影响赛前可用能力，由 Career 在未来版本化规则中折算；伤病类型和原始
严重度不会因预留而进入 V5。

### 5.5 V4 base 字段在 V5 的归属

切片 B 不是把七项重新摊平为 15 个 V4 base 字段。V5 的 Match 接口直接读取七项，再在各动作内部
生成执行参数；V4 adapter 只服务旧 context 和旧 Pending，不作为 V5 的实现模板。

| V4 字段 | V5 处理 |
| --- | --- |
| `HeightMeters` / `StandingReachMeters` | 保留，改由 Career 身份资料提供 |
| `Jump` / `Mobility` / `Coordination` | 由 `Athleticism` 直接派生，不混入技术值 |
| `Reaction` | 不再作为 Career base；由当前动作技术直接承担该动作的判断和控制 |
| `AttackTechnique` | 由 `Spike` 直接派生 |
| `AttackPower` | 由 `Spike` 直接派生 |
| `BlockTechnique` | 由 `Block` 直接派生 |
| `DefenseTechnique` | 由 `Defense` 直接派生 |
| `ReceiveTechnique` | 由 `Reception` 直接派生 |
| `SetTechnique` / `SoftTouch` | 由 `Setting` 直接派生 |
| `ServeTechnique` | 由 `Serve` 直接派生 |
| `CourtAwareness` | 不再作为通用 Career base；接发读取 `Reception`，防守读取 `Defense` |
| 新增 `Readiness` | 单独保存并进入派生解释 |
| 新增 `Athleticism` 正式消费 | 进入移动、起跳和协调 |

如果七项 Career Schema 先于完整 V5 Match 上线，临时 V4 adapter 只能使用以下单一来源复制：

```text
Spike       -> AttackTechnique, AttackPower
Serve       -> ServeTechnique
Reception   -> ReceiveTechnique
Defense     -> DefenseTechnique, Reaction, CourtAwareness
Block       -> BlockTechnique
Setting     -> SetTechnique, SoftTouch
Athleticism -> Jump, Mobility, Coordination
```

adapter 先做基点到 `[0,1]` 的转换，最后统一应用一次 `Readiness`，不得添加加权平均。该表只是让旧 V4
可运行的兼容层：其中全局 `Reaction` 暂由防守提供；正式 V5 必须按动作分别读取 `Reception` 或
`Defense`。当前 V4 的 `Serve.PowerCapacity` 仍会间接读取 `AttackPower`，因此也不能宣称它已经
直连；V5 公式必须让 `Serve` 成为发球力量的唯一技术来源。

Match 侧最简接线如下；表内“直接”表示值只来自一个 V5 字段，不使用加权平均：

| Match 动作组 | 技术输入 | 物理输入 |
| --- | --- | --- |
| `Attack` | `Spike` 直接控制方向、速度、力量和触球控制 | `Athleticism` 控制助跑、起跳和触球高度上限 |
| `Serve` | `Serve` 直接控制方向、速度、力量和稳定性 | 无 |
| `Receive` | `Reception` 直接控制第一触、反应和判断 | `Athleticism` 控制覆盖移动 |
| `Defense` | `Defense` 直接控制触球、反应和判断 | `Athleticism` 控制覆盖移动 |
| `Block` | `Block` 直接控制时机和手型 | `Athleticism` 控制横移、起跳和触球高度上限 |
| `Set` | `Setting` 直接控制落点、节奏、触球和判断 | `Athleticism` 控制覆盖移动 |

V5 使用新的 formula/coefficient version 和 input/result fingerprint。具体平衡系数属于版本化配置，
不是规范 DTO 字段；实现前必须固定配置、golden vector 和单变量单调测试。

### 5.6 Athleticism 的正式消费门禁

`Athleticism` 必须在 Match 中影响可验证的移动、起跳和协调结果，并由 Replay 记录对应消费路径。
它不能替代任一技术能力：运动能力更高的球员可以更快到位或获得更高触球点，但不能单独提高扣球
落点控制、接发质量、二传落点或拦网手型。

首版 V5 不新增长期 `Endurance`。Career 疲劳通过一次性 `Readiness` 影响赛前状态；比赛内能量预算
等 Match 拥有正式消费者后再单独版本化，不能让 `Athleticism` 在没有证据的情况下同时代表耐力。

## 6. 培养方向、类型和 Career-only 状态

首版不在 Shared 增加 trait 列表。

| 信息 | 所有权与处理 |
| --- | --- |
| 进攻型/均衡型/保障型培养方向 | Career 成长效率与训练策略；不直接覆盖比赛能力 |
| 潜力 | Career 成长效率和上限；不进入 Match |
| 教练信任 | Career 出场与关键球机会；不作为执行能力 |
| 心态 | Career 状态和事件结果；没有正式消费者前不进入 Match |
| 原始疲劳、伤病 | Career 状态；只通过版本化 readiness/可用性规则影响赛前输入 |
| 学业、社交 | Career 事件；不进入 Match 合同 |
| 副职业 | 本阶段只记录设计可能性，不创建字段或枚举 |

未来某个特色属性只有在存在明确动作消费者、Replay 解释和测试后，才能以固定枚举和版本化结构进入
新合同；禁止 `Dictionary<string,float>`。

## 7. Career 结算边界

Match 报告事实，Career 计算后果：

```text
Match facts:
  实际动作、质量、得失分、负荷、关键阶段事件

Career consequences:
  七项成长经验、能力提升、教练信任、疲劳、伤病风险、事件与叙事
```

Career 结算必须同时保存：

- 原始 result 与 performance report 规范 bytes；
- `contextHash/resultHash/reportHash`；
- 结算规则版本和 `appliedRevision`；
- 可索引的 SettlementReceipt；
- 已清除的 Pending 状态。

这些内容属于一次原子快照提交。不同 report hash 绑定同一 result 时返回冲突证据，不能覆盖已有回执。

## 8. 兼容与迁移

### 8.1 V4 Pending

- 已持久化的 V4 Pending 不改写、不重算 context hash；
- 正式物理 V4 runner 可以生成 `MatchPerformanceReportV1`；
- 旧 fixture Pending 继续使用现有精确配置路由和 legacy estimated facts；
- legacy 回执必须明确事实来源，不能伪装成 performance report；
- 失败或取消仍回到赛前并复用原 context。

### 8.2 V4 存档与回放

- 切片 A 不需要存档结构变化即可定义 Shared 报告，但 Career 原子保存报告时必须另开 Schema 迁移任务；
- 已完成的 V4 结算不补算详细报告；
- V4 Replay 永久按 V4 reader 读取，不升级字节或重写哈希；
- V5 context/result/replay 使用独立类型和版本，不修改 V4 后继续称为 V4。

### 8.3 八项旧档到七项的迁移候选

切片 B 实现时必须升级 Career Schema，并以整数运算迁移现有字段：

```text
Spike      = old.Spike
Serve      = old.Serve
Reception  = old.Reception
Defense    = old.Defense
Block      = old.Block
Setting    = old.Reception
Athleticism = (old.Movement + old.Jump + old.Stamina + 1) / 3
```

`Setting = old.Reception` 只用于未发布开发存档的一次迁移，不是正式比赛映射；迁移后两项独立成长。
上式使用非负整数除法，等价于取最接近的整数。`Athleticism` 的成长经验同样取旧移动、弹跳和体能
经验的整数平均值，不求和，避免迁移后立即跨越多个升级阈值。
旧三项潜力上限和成长效率的合并规则需要在 Career Schema 迁移任务中用 golden save 固定。迁移只
发生一次；旧字段保留在旧版本 reader 中，不让 Match 读取或猜测旧档。

### 8.4 回滚

- 切片 A 可回滚到 `DirectAggregateOnly`，但已保存的 report bytes/readers 不得删除；
- 切片 B 可停止创建新 V5 比赛，仍保留 V4 reader 和 V4 Pending runner；
- 任何回滚都不能删除既有回执或让相同 session 重新结算。

## 9. 验证计划

### 9.1 Report V1

- constructor/invariant、严格 JSON、规范 bytes、hash 与 round-trip；
- 12 人覆盖、排序、重复 ID、外部 ID 和哈希绑定；
- 每类动作至少一个成功、失误和边界 fixture；
- 接发质量阈值和关键动作规则的 golden vector；
- 同一事实流两次聚合得到相同 bytes；
- report 与 replay 的 actor/action/order 一致性；
- context/result/report 任一哈希不一致时拒绝结算；
- 重复回调幂等、冲突 report hash、原子保存失败恢复；
- legacy fixture Pending 恢复不回归。

### 9.2 V5 输入

- 六项技术一对一映射的端点和单变量单调测试；
- `Athleticism` 到移动、起跳与协调的单变量单调测试；
- 单独提高技术能力不得改变无关动作，单独提高 `Athleticism` 不得提高技术控制；
- readiness 在 Career→context 边界只应用一次；
- 位置和培养方向不能改变冻结 base；
- derived formula/coefficient fingerprint golden vector；
- 八项旧档迁移为七项的 golden save 与幂等迁移；
- 每个新字段至少一个正式 Match 消费和 Replay 解释证据；
- V4/V5 reader、Pending 路由和拒绝错误版本测试。

## 10. 实施顺序

1. 冻结 `MatchPerformanceReportV1` 字段、规范 JSON 和 golden fixture。
2. 在 Match 建立事实累计器，先接扣球、发球、接发、防守、拦网和二传。
3. 接入负荷与稳定性事实，生成 report 并与 result/replay 绑定。
4. 扩展 Bootstrap runner 产物，Career 校验并原子保存 report。
5. Career 使用真实报告结算成长、信任、疲劳和伤病。
6. 完成球员身体资料和八项到七项的存档迁移设计。
7. 冻结 V5 七项 core/readiness/derived 合同及 Match 消费者。
8. 新建 V5 实现任务，分阶段接入 Shared、Match、Replay、Career 和 Bootstrap。

首个代码里程碑只执行步骤 1–3，输出一个可验证的 Match-owned 报告，不同时修改 Career 存档 Schema。

## 11. 待确认决策

本设计建议确认以下基线：

1. 同意先做独立报告 V1，再做 V5 输入合同；
2. 同意把八项明示能力调整为扣球、发球、接发、防守、拦网、组织、运动能力七项；
3. 同意培养方向只影响 Career 成长，不直接覆盖 Match 能力；
4. 同意原始疲劳留在 Career，Shared 只接收一次性 readiness；
5. 同意六项技术直接控制对应动作，运动能力只控制移动、起跳和协调；
6. 同意首个实现切片止于 Match 报告生成和验证，不同时迁移 Career 存档。
