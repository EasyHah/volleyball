# Career × Match V4 集成协议

- 状态：当前合并基线
- 适用范围：Career 首个离线里程碑、直接比赛适配、快速模拟适配
- 权威契约：`Volleyball.Shared.Contracts.MatchContextV4` /
  `Volleyball.Shared.Contracts.MatchResultV4`
- 契约版本：`ContractVersions.MatchV4 == 4`
- 排球规则版本：`RulesVersions.FullRallyV3 == 3`

本文面向负责 Career、Match、Bootstrap 和版本合并的开发者及 Codex。实现和审查时，
必须以本文的 MUST、MUST NOT、SHOULD 和 MAY 为规范词；若本文与 Shared V4 代码校验
冲突，以 Shared V4 代码为最终权威，并同步修正文档。

## 1. 所有权和依赖方向

权威数据流：

```text
Career
  -> 冻结 MatchContextV4
  -> Bootstrap 场景适配器
  -> Match 执行比赛
  -> 返回 MatchResultV4
  -> Career 校验、结算和保存
```

模块要求：

- Match 和 Shared MUST NOT 引用 Career。
- Career Domain 和 Career Application MUST NOT 引用 Match 实现程序集。
- Match MUST 只接收 Shared `MatchContextV4`，并只返回 Shared
  `MatchResultV4`。
- Bootstrap SHOULD 持有跨场景生命周期适配器，连接 Career runner 和 Match
  的公开 V4 入口。
- `Career.MatchIntegration` MUST 负责 Career 自有模型与 Shared V4 之间的映射，
  但 MUST NOT 引用 Match 实现程序集。
- Match 负责报告比赛事实；Career 负责成长、疲劳、心态、教练信任、伤病和其他后果。

当前 Career runner 端口为：

```csharp
public interface ICareerMatchRunnerV4
{
    Task<MatchResultV4> ExecuteAsync(
        MatchContextV4 context,
        CancellationToken cancellationToken);
}
```

Match 程序集 SHOULD 提供自己的纯 V4 场景入口。Match MUST NOT 为了实现上面的
Career 接口而反向引用 `Career.MatchIntegration`；实现该接口的跨模块适配器应放在
Bootstrap。

## 2. Career 传入 Match 的权威输入

唯一权威输入为 `MatchContextV4`。Match MUST 使用收到的对象或其规范 V4 字节，
不得从显示名、位置或场景默认值重建另一份上下文。

### 2.1 顶层字段

| 字段 | 约束 |
| --- | --- |
| `ContractVersion` | MUST 等于 `4` |
| `RulesVersion` | MUST 等于 `RulesVersions.FullRallyV3`，即 `3` |
| `SessionId` | MUST 是非空 GUID；用作比赛生命周期和幂等标识 |
| `Seed` | Career 已冻结的有符号 32 位比赛种子 |
| `Home` | MUST 是 `TeamSide.Home` 的六人队伍 |
| `Away` | MUST 是 `TeamSide.Away` 的六人队伍 |
| `PhysicsConfigurationHash` | MUST 是 64 字符小写十六进制 SHA-256 |
| `TrajectoryPredictionProviderConfiguration` | MUST 是已冻结的轨迹预测配置 |
| `FormulaVersion` | 当前 MUST 等于 `1` |
| `CoefficientVersion` | 当前 MUST 等于 `1` |
| `ContextHash` | 由 Shared 自动计算；MUST NOT 手工覆盖 |

轨迹预测配置约束：

- `CacheCapacity` MUST 在 `[1, 4096]`；
- `CacheEvictionPolicy` MUST 是
  `TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut`；
- `PredictorVersion` MUST 为正数；
- `PredictorConfigurationHash` MUST 是 64 字符小写十六进制 SHA-256。

当前 Career fixture 使用固定的 physics/predictor 哈希。真实物理比赛接线后，Match
runner MUST 使用实际冻结配置的哈希，MUST NOT 把 fixture 哈希伪装成真实配置标识。

### 2.2 队伍和轮转

每队 MUST：

- 恰好包含六名非空球员；
- 使用 `RotationOrder` 表示轮转位置 1–6；
- 保持 Career 传入顺序，不得按位置、号码或显示名重新排序；
- 在队内保持 `PlayerId` 唯一；
- 与另一队保持 `TeamId` 不同；
- 与另一队共同保持全场 12 个 `PlayerId` 全局唯一。

Career 当前要求的六人位置拓扑为：

```text
Setter x1
OutsideHitter x2
MiddleBlocker x1
Opposite x1
Libero x1
```

### 2.3 球员身份和基础属性

每名 `PlayerSnapshotV4` 包含：

```text
PlayerId
DisplayName
JerseyNumber
Position
DominantHand
Physical
Technical
Derived
```

约束：

- `PlayerId` MUST 为 1–64 个字符，只允许字母、数字、`-`、`_`、`.`、`:`；
- `DisplayName` MUST 为 1–100 个字符；
- `JerseyNumber` MUST 在 `[0, 99]`；Career 当前只生成 `[1, 99]`；
- `Physical`、`Technical`、`Derived` MUST 非空；
- `DominantHand` 和 `Position` MUST 是已定义枚举值；
- Match MUST 消费已冻结的 `Derived`，MUST NOT 根据位置创建另一套能力。

物理基础属性：

| 字段 | 范围 |
| --- | ---: |
| `HeightMeters` | `[1.40, 2.30]` |
| `StandingReachMeters` | `[1.70, 3.10]`，且不得小于身高 |
| `Jump` | `[0, 1]` |
| `Mobility` | `[0, 1]` |
| `Reaction` | `[0, 1]` |
| `Coordination` | `[0, 1]` |

技术基础属性均 MUST 为有限的 `[0, 1]` 浮点数：

```text
AttackTechnique
AttackPower
BlockTechnique
DefenseTechnique
ReceiveTechnique
SetTechnique
ServeTechnique
SoftTouch
CourtAwareness
```

`Derived` MUST 由 Shared 的发布配置生成：

```csharp
MatchAttributeDerivationV4.Derive(
    physical,
    technical,
    dominantHand,
    MatchAttributeDerivationConfigV4.Version1);
```

任何 supplied derived 值都必须通过 Shared 的输入指纹和结果指纹复算。实现 MUST NOT
伪造、修改或绕过指纹校验。

Match 可消费的派生字段为：

```text
Attack:  DirectionControl, SpeedControl, PowerCapacity,
         ContactHeightMeters, ApproachMobility
Block:   Timing, HandControl, ReachHeightMeters, LateralMobility
Defense: Reaction, PlatformControl, CoverageMobility, Awareness
Receive: FirstTouchControl, Reaction, Movement, Awareness
Set:     PlacementControl, TempoControl, SoftTouch, Movement, Awareness
Serve:   DirectionControl, SpeedControl, PowerCapacity, Consistency
```

## 3. Career 八项属性到 V4 的当前映射

> [!NOTE]
> 当前 active 跨模块任务
> [`2026-07-29-career-match-integration-redesign`](handoffs/active/2026-07-29-career-match-integration-redesign.md)
> 优先完成 V4 生命周期联调。本节映射在该任务中作为临时基线：必须可追溯、单调，且不得被 Match
> 位置模板覆盖。全职业基础属性、职业特色属性与可能的副职业属于 V5 后续设计，不阻塞 V4。

Career 权威成长属性为：

```text
扣球、发球、接发、防守、拦网、移动、弹跳、体能
```

Career 先把 `[0, 10000]` 基点转换为 `[0, 1]`，再应用疲劳：

```text
readiness = 0.75 + 0.25 * (1 - fatigue / 100)
```

疲劳 0 时 `readiness == 1.00`；疲劳 100 时 `readiness == 0.75`。

当前映射：

```text
Physical.Jump         = 弹跳 * readiness
Physical.Mobility     = 移动 * readiness
Physical.Reaction     = (防守 + 接发) / 2 * readiness
Physical.Coordination = (移动 + 接发 + 防守) / 3 * readiness

Technical.AttackTechnique  = 扣球 * readiness
Technical.AttackPower      = (扣球 * 0.75 + 弹跳 * 0.25) * readiness
Technical.BlockTechnique   = 拦网 * readiness
Technical.DefenseTechnique = 防守 * readiness
Technical.ReceiveTechnique = 接发 * readiness
Technical.SetTechnique     = (接发 + 防守) / 2 * readiness
Technical.ServeTechnique   = 发球 * readiness
Technical.SoftTouch        = (接发 + 防守) / 2 * readiness
Technical.CourtAwareness   = (防守 + 体能) / 2 * readiness
```

首个里程碑的临时限制：

- `HeightMeters == 1.90f`；
- `StandingReachMeters == 2.42f`；
- `DominantHand == DominantHandV4.Right`；
- 球员和队伍的 V4 `DisplayName` 当前使用稳定 ID。

Match MUST 尊重上下文中的最终值，MUST NOT 自行替换这些临时值。后续解除限制应修改
Career 映射输入；不得在 Match 内偷偷增加推断规则。

## 4. Match 返回给 Career 的权威输出

Match MUST 通过 Shared 工厂创建结果：

```csharp
MatchResultV4.Create(
    context,
    winnerTeamId,
    homeScore,
    awayScore,
    ralliesPlayed,
    acceptedContacts,
    v3RuleTransitionCount,
    playerStats);
```

实现 MUST NOT 手工构造结果 JSON、手工计算 `ResultHash` 或复制上下文生成新
`ContextHash`。

### 4.1 顶层结果约束

| 字段 | 约束 |
| --- | --- |
| `ContractVersion` | MUST 等于 `4` |
| `SessionId` | MUST 与输入完全一致 |
| `ContextHash` | MUST 与输入完全一致 |
| `WinnerTeamId` | MUST 是输入的主队或客队 ID |
| `HomeScore` / `AwayScore` | MUST 非负、不得平局，胜方必须对应较高分 |
| `RalliesPlayed` | MUST 等于 `HomeScore + AwayScore` |
| `AcceptedContacts` | MUST 是规则系统接受的实际触球数 |
| `V3RuleTransitionCount` | MUST 非负且不得超过 `AcceptedContacts` |
| `PlayerStats` | Career 集成要求恰好覆盖全部 12 人 |
| `ResultHash` | 由 Shared 自动计算；MUST NOT 手工覆盖 |

正式六人 V3 Authority 模式 SHOULD 满足：

```text
V3RuleTransitionCount == AcceptedContacts
```

当前 `MatchResultV4` 只承载一局完成比分，没有逐局比分数组。首个里程碑 MUST 返回
一局 25 分、领先 2 分的结果；MUST NOT 把“赢下的局数”填入
`HomeScore`/`AwayScore`。

### 4.2 每名球员统计

每名球员必须返回：

```csharp
new PlayerMatchStatsV4(
    playerId,
    points,
    contacts,
    errors,
    workload);
```

Career 合并的强约束高于 Shared 的最低校验：

- MUST 恰好返回 12 条统计；
- MUST 为上下文中每名球员返回一条，即使全部数值为零；
- `PlayerId` MUST 唯一；
- MUST NOT 包含上下文之外的 ID；
- `points`、`contacts`、`errors` MUST 是非负整数；
- `workload` MUST 是有限的 `[0, 1]` 标准化值。

Shared 当前只要求 workload 非负，但 Career 对 `<=1` 和 `>1` 有不同换算语义。双方
协议因此统一要求 `[0, 1]`，避免把 0–1 和 0–10000 两种单位混用。

结果返回前 MUST 执行：

```csharp
result.ValidateAgainst(context);
```

## 5. 取消、退出、恢复和幂等

当前 `MatchResultV4` 只支持已完成且有胜方的比赛，不能表示平局或中止。

- 玩家中途退出时，Match MUST NOT 伪造 `0:0`、平局或随意胜方。
- 取消时适配器 SHOULD 取消任务并让 Career 保留 `PendingMatch`。
- 再次进入时从同一个赛前上下文重新比赛。
- 当前 `PendingMatch` 不包含局分、球位置、球员位置等中途检查点。
- Match 或 Bootstrap MUST 保证完成回调最多成功一次。
- 重复回调和重复提交 MUST NOT 产生第二次 Career 结算。
- Career 使用 `SessionId + ContextHash + ResultHash` 校验恢复和幂等。

快速模拟 MUST 在相同上下文、相同种子、相同 simulation/random 版本下产生完全相同的
规范结果字节。直接物理比赛不承诺逐帧确定性，但仍必须保持上下文身份和最终事实真实。

## 6. 规范序列化

持久化和跨恢复边界只能使用 Shared：

```csharp
ContractJson.SerializeV4(context);
ContractJson.SerializeV4(result);

ContractJson.DeserializeMatchContextV4(json);
ContractJson.DeserializeMatchResultV4(json);
```

要求：

- UTF-8，无 BOM；
- 使用 Shared 的固定字段顺序和浮点规范；
- 反序列化后再次序列化必须逐字节一致；
- MUST NOT 使用 `JsonUtility`、Newtonsoft 默认输出或自定义 JSON 代替；
- MUST NOT 静默改变 V4 字段或哈希语义；
- 需要改变规范载荷时必须增加新的契约版本。

## 7. 当前未进入 MatchContextV4 的 Career 信息

以下 Career 启动信息当前没有进入 `MatchContextV4`：

```text
competitionId
scheduleItemId
importanceBasisPoints
preMatchPriority
CareerMatchFormat 的完整赛制
训练重点来源
原始疲劳值
伤病限制
首发/替补状态
```

疲劳已经折算进 V4 能力，但 Match 看不到原始疲劳。

首个合并版本 MUST NOT 在 Match 中自行猜测这些信息。后续若必须使用：

1. 仅影响场景导航、不影响规范结果的信息，MAY 使用独立的版本化赛前信封由
   Bootstrap 传递；
2. 会影响规范比赛决策或结果的信息，必须进入新的 Shared Match 契约版本。

MUST NOT 直接给 `MatchContextV4` 增加字段而继续称其为 V4。

## 8. 正式 Career 成长所需的后续详细报告

`PlayerMatchStatsV4` 只有总得分、总触球、总失误和总负荷，不能准确区分扣球、发球、
接发、防守和拦网。当前 `CareerMatchV4Mapper` 对这些字段的拆分只是 fixture 兼容估算，
不得被描述成正式技术统计。

正式物理比赛接入 Career 成长前，Match 还需要提供每名球员的以下事实：

```text
扣球：尝试、得分、失误
发球：尝试、Ace、失误
接发：完美、到位、一般、不到位、直接失误
防守：尝试、成功
拦网：尝试、有效触球、得分
负荷：参与回合、上场时长、移动距离、起跳数、高负荷起跳数、
      落地负荷、总负荷
稳定性：关键动作、关键成功、关键失误、连续失误次数、最长失误串
```

推荐新增独立的版本化详细报告，例如 `CareerMatchReportV1`，并绑定：

```text
sessionId
contextHash
resultHash
```

该报告是后续决策，不属于当前 P0 合并范围。未经双方确认，Codex MUST NOT 擅自修改
`MatchResultV4` 或提前实现上述新契约。

## 9. 合并验收清单

合并前双方必须验证：

- [ ] Match 收到的 `MatchContextV4` 与 Career 生成的 session/hash 完全一致。
- [ ] Match 没有重建、重新排序或覆盖 12 人上下文。
- [ ] 每名球员的 `DerivedMatchAttributesV4` 确实进入比赛 AI 和执行层。
- [ ] 结果恰好包含 12 条唯一球员统计。
- [ ] `MatchResultV4.ValidateAgainst(context)` 通过。
- [ ] Context/Result 规范 JSON 可逐字节往返。
- [ ] 中途取消后 Career `PendingMatch` 仍存在。
- [ ] 重复完成回调只结算一次。
- [ ] Quick Simulation 固定种子结果一致。
- [ ] Match 和 Shared 不引用 Career。
- [ ] Career Domain/Application 不引用 Match 实现。
- [ ] 跨模块接线位于 Bootstrap。
- [ ] Shared、Bootstrap 或搭档区域改动通过 Pull Request 合并。

## 10. 实现依据

实现或审查前，Codex 应优先读取：

- `Assets/Volleyball/Shared/Runtime/MatchContextV4.cs`
- `Assets/Volleyball/Shared/Runtime/MatchResultV4.cs`
- `Assets/Volleyball/Shared/Runtime/PlayerSnapshotV4.cs`
- `Assets/Volleyball/Shared/Runtime/MatchAttributeDerivationV4.cs`
- `Assets/Volleyball/Shared/Runtime/ContractJson.cs`
- `Assets/Volleyball/Career/Runtime/Application/CareerMatchLaunchContracts.cs`
- `Assets/Volleyball/Career/Runtime/MatchIntegration/CareerMatchV4Mapper.cs`
- `Assets/Volleyball/Career/Runtime/MatchIntegration/CareerMatchExecutorV4.cs`
- `Assets/Volleyball/Career/Runtime/Persistence/CareerMatchPayloadValidator.cs`
- `Assets/Volleyball/Bootstrap/Runtime/CareerVerticalSliceBootstrap.cs`

历史 V1–V3 契约、旧 runner 和旧迁移设计不是当前生产入口，不能用来覆盖本文的 V4
要求。
