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
   - Career 直接保存并传递身体层与技术层基础属性，不再从少量核心值混合生成；
   - Match 按进攻、拦网、防守、接发、二传、发球六类派生实际比赛属性；
   - 六维展示合并身高/摸高与防守/接发，并增加力量和球商轴；
   - 展示汇总不得参与模拟；
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

因此本项目不照搬这些游戏的具体属性数量，而采用相同的信息分层：Career 可以保存足够细的权威基础
属性，但默认界面用分层明细和六维图降低阅读负担；Match 从同名基础字段生成执行参数，派生规则必须
固定、透明，并能通过 Replay 证明实际消费。

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
| 接发质量 | 实际第一传落点与冻结二传目标的水平距离分桶 |
| 防守尝试 | 非发球接发阶段声明并实际执行的 floor-defense 尝试 |
| 防守成功 | 防守接触被规则接受且回合继续 |
| 拦网尝试 | 已提交并进入接触窗口的合法 `Block` 动作 |
| 有效拦网触球 | 球实际接触拦网面且产生可验证轨迹变化 |
| 拦网得分 | 该触球直接导致拦网方得分 |
| 二传尝试 | 合法二传动作进入执行窗口；接受接触或可归责的终止失败均只计一次 |
| 二传成功/失误 | 二传接触被规则接受且产生可执行进攻窗口，或直接终止本方回合 |
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

## 5. 切片 B：V5 基础属性输入与比赛属性派生

### 5.1 两层基础属性

Career 保存球员的权威基础属性，Shared 原样传输，Match 只负责派生。V5 不再设置需要 Mapper
二次混合的“通用核心能力”。

```text
PhysicalBaseAttributesV5
  HeightMeters
  StandingReachMeters
  Strength
  Jump
  Mobility
  Reaction
  Coordination

TechnicalBaseAttributesV5
  AttackTechnique
  BlockTechnique
  DefenseTechnique
  ReceiveTechnique
  SetTechnique
  ServeTechnique
  SoftTouch
  VolleyballIQ
```

V5 相对 V4 有两项明确调整：`AttackPower` 不再作为技术基础，改为身体层的通用 `Strength`；
`CourtAwareness` 改名为语义更清晰的 `VolleyballIQ`。其余字段范围与 V4 保持连续：

- `HeightMeters` 使用 `[1.40,2.30]` 米；
- `StandingReachMeters` 使用 `[1.70,3.10]` 米，且不得低于身高；
- 其余基础属性跨模块使用 `[0,1]`，Career 界面显示为精确 `0–100`；
- `DominantHand` 与球员 ID、姓名、号码和位置一起属于身份资料；
- 身高、站立摸高和惯用手由 Career 在球员创建时生成并保存，不属于训练成长项；
- 其他身体与技术字段各自拥有成长经验、潜力效率和上限。

Career→Shared 只做单位转换和范围验证，字段名称与语义保持一对一。禁止在 Mapper 中用接发代填二传、
用防守代填反应，或用多项平均生成协调、软触球和球商。

### 5.2 基础属性六维展示

球员页使用同一个图表区域在“身体”和“技术”两个页签间切换，避免同时堆叠大量数字。

身体层保留七个权威明细，但六维图将身高和站立摸高合并为“身体尺寸”：

```text
身体尺寸、力量、弹跳、移动、反应、协调
```

```text
BodySize = Mean(
  Normalize(HeightMeters),
  Normalize(StandingReachMeters))
```

身高和站立摸高在文字中仍分别显示真实米数。雷达归一化使用版本化 `RadarNormalizationV1` 和联赛
固定参考区间，不能按当前阵容动态缩放，否则转会或换队会改变同一名球员的图形。

技术页签将八个明细合并为六个展示轴：

| 技术六维轴 | 仅用于显示的 V1 汇总 |
| --- | --- |
| 进攻 | `AttackTechnique` |
| 拦网 | `BlockTechnique` |
| 后场 | `Mean(DefenseTechnique, ReceiveTechnique)` |
| 二传 | `Mean(SetTechnique, SoftTouch)` |
| 发球 | `ServeTechnique` |
| 球商 | `VolleyballIQ` |

所有八项技术明细仍在图表下方显示。防守和接发只在六维展示中合并为“后场”，Match 的动作输入、
比赛统计和成长事实仍保持分开。雷达汇总没有成长经验、潜力或存档字段，也不得作为比赛输入。

### 5.3 Readiness 与疲劳边界

原始疲劳继续是 Career 的 `0–100` 状态，不进入 Shared。Career 在冻结 context 时产生：

```text
readiness = 0.75 + 0.25 * (1 - fatigue / 100)
```

`PlayerSnapshotV5.Readiness` 使用 `[0,1]`，首版 Career 实际只产生 `[0.75,1]`。Match 只对 `[0,1]`
能力字段统一应用一次；身高、站立摸高和身份资料不乘 readiness：

```text
effectiveAbility = baseAbility * readiness
```

Match 不读取原始疲劳，不再次扣减 readiness；比赛中的动态能量状态不得回写基础属性。伤病若只影响
赛前可用能力，由 Career 在未来版本化规则中折算；伤病类型和原始严重度不因预留进入 V5。

### 5.4 六类比赛属性

Match 使用版本化公式从冻结的基础属性派生六类实际比赛字段：

| Match 派生动作组 | 派生明细 |
| --- | --- |
| 进攻 `Attack` | 线路控制、速度控制、力量上限、击球高度、助跑移动 |
| 拦网 `Block` | 时机、手型控制、拦网高度、横向移动 |
| 防守 `Defense` | 反应、垫球控制、覆盖移动、场上判断 |
| 接发 `Receive` | 第一触控制、反应、移动、场上判断 |
| 二传 `Set` | 落点控制、节奏控制、软触球、移动、场上判断 |
| 发球 `Serve` | 线路控制、速度控制、力量上限、稳定性 |

字段继续使用 V4 已有语义，V5 通过新的 formula/coefficient version 和 input/result fingerprint 固定
派生规则。进攻和发球的力量上限读取身体层 `Strength`；各动作的判断、选择和 awareness 消费
`VolleyballIQ`。Career 不保存、不覆盖这些派生值；位置、培养方向和 NPC 模板也不能替换玩家传入的
基础属性。

### 5.5 比赛属性六维展示

比赛六维图固定使用进攻、拦网、后场、二传、发球、球商六个轴：

```text
AttackRadar = Mean(normalized Attack fields)
BlockRadar = Mean(normalized Block fields)
BackcourtRadar = Mean(normalized Defense and Receive fields)
SetRadar = Mean(normalized Set fields)
ServeRadar = Mean(normalized Serve fields)
VolleyballIQRadar = effective VolleyballIQ
```

原生 `[0,1]` 字段直接进入平均；击球高度和拦网高度由同一个 `RadarNormalizationV1` 转为 `[0,1]`。
六个结果最终统一做 `Round(100 * value)`。该六维值只用于 UI、赛前对比和赛后说明，不进入 AI、
物理、快速模拟、成长或结算。若以后调整展示权重，只升级 radar summary version，不升级比赛公式
版本。

### 5.6 数据流与直接接线原则

```text
Career authoritative physical/technical base
  -> Shared PlayerSnapshotV5 + Readiness
  -> Match versioned derivation
  -> six groups of derived match attributes
  -> gameplay + replay evidence
```

这条链路只有 Match derivation 一处允许组合公式。Career Mapper、UI 六维图和培养方向都不能再创建
第二套派生规则。搭档可以直接读取 Shared 的基础字段，也可以直接消费 Match 已派生的六类动作属性，
无需解释 Career 专属的八项或七项中间模型。

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
  可训练身体/技术基础属性的成长经验与提升、教练信任、疲劳、伤病风险、事件与叙事
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

### 8.3 八项旧档到两层基础属性的迁移候选

切片 B 实现时必须升级 Career Schema，并以整数运算迁移现有字段：

```text
HeightMeters        = 1.90
StandingReachMeters = 2.42
DominantHand        = Right
Jump                = old.Jump
Mobility            = old.Movement
Reaction            = Mean2(old.Reception, old.Defense)
Coordination        = Mean3(old.Movement, old.Reception, old.Defense)

Strength        = Weighted4(old.Spike, old.Spike, old.Spike, old.Jump)
AttackTechnique = old.Spike
BlockTechnique  = old.Block
DefenseTechnique = old.Defense
ReceiveTechnique = old.Reception
SetTechnique    = Mean2(old.Reception, old.Defense)
ServeTechnique  = old.Serve
SoftTouch       = Mean2(old.Reception, old.Defense)
VolleyballIQ    = Mean2(old.Defense, old.Stamina)
```

`Mean2/Mean3/Weighted4` 都使用非负整数和固定的就近舍入，具体分子偏移由迁移规则版本和 golden
save 固定。直接字段复制原成长经验、潜力效率和上限；组合字段对相同来源值应用同一组合函数。该
混合只允许出现在未发布开发存档的一次迁移中，迁移后每项基础属性独立成长，运行时 Mapper 不再
重复计算。旧字段保留在旧版本 reader 中，不让 Match 读取或猜测旧档。

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

- 两层全部基础字段的端点、严格 round-trip、单位和范围测试；
- 身高/站立摸高约束，以及 readiness 不修改身体尺寸的测试；
- Career→Shared 同名字段逐项相等，不允许 Mapper 加权或位置覆盖；
- 每个基础能力至少一个正式 Match 消费与单变量单调测试；
- readiness 在 Match 派生前只应用一次；
- 位置和培养方向不能改变冻结 base；
- derived formula/coefficient fingerprint golden vector；
- 基础/比赛六维图的 normalization、平均、舍入和 golden vector；
- 修改 radar summary version 不得改变 context/result fingerprint 或比赛结果；
- 八项旧档迁移为两层基础属性的 golden save 与幂等迁移；
- 每个派生比赛字段至少一个正式消费和 Replay 解释证据；
- V4/V5 reader、Pending 路由和拒绝错误版本测试。

## 10. 实施顺序

1. 冻结 `MatchPerformanceReportV1` 字段、规范 JSON 和 golden fixture。
2. 在 Match 建立事实累计器，先接扣球、发球、接发、防守、拦网和二传。
3. 接入负荷与稳定性事实，生成 report 并与 result/replay 绑定。
4. 扩展 Bootstrap runner 产物，Career 校验并原子保存 report。
5. Career 使用真实报告结算成长、信任、疲劳和伤病。
6. 完成 Career 两层基础属性和旧八项存档迁移设计。
7. 冻结 V5 physical/technical base、readiness、六类 derived 与 radar summary 规则。
8. 新建 V5 实现任务，分阶段接入 Shared、Match、Replay、Career 和 Bootstrap。

首个代码里程碑只执行步骤 1–3，输出一个可验证的 Match-owned 报告，不同时修改 Career 存档 Schema。

## 11. 待确认决策

本设计建议确认以下基线：

1. 同意先做独立报告 V1，再做 V5 输入合同；
2. 同意 Career 直接保存身体七项与技术八项明细，Shared 同名直传；
3. 同意培养方向只影响 Career 成长，不直接覆盖 Match 能力；
4. 同意原始疲劳留在 Career，Shared 只接收一次性 readiness；
5. 同意力量属于身体层，球商属于技术层，并由 Match 正式消费；
6. 同意 Match 内部保留防守与接发的独立派生字段；
7. 同意基础与比赛六维图分别合并身高/摸高和防守/接发，不参与模拟、成长或结算；
8. 同意首个实现切片止于 Match 报告生成和验证，不同时迁移 Career 存档。
