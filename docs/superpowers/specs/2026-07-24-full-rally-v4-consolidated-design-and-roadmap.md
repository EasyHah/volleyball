# Full Rally V4 单轨属性、共享执行与后续回合架构整合设计

**日期：** 2026-07-24

**状态：** Gate A–E 已完成；Gate F–K 待规划

**范围：** 当前完成度审计、V4 双端权威属性、Career/Match 硬切、共享执行与预测、V3 规则复用、Full Rally 后续交付顺序

## 1. 本文档的权威性

本文档整合并更新下列设计与计划：

- `2026-07-23-full-rally-v3-architecture-design.md`
- `2026-07-24-full-rally-v3-phase-2-9-roadmap.md`
- `2026-07-24-v3-ability-semantics-and-v4-layered-attributes-design.md`
- `2026-07-24-rally-organization-and-ability-benchmark-design.md`
- `2026-07-24-001-stage2-ability-envelope-and-prediction.md`

发生冲突时，以本文档为准。既有文档仍用于解释历史决策、V3 规则设计和 Stage 1.5
基线，但下列旧方向被本文档明确取代：

1. 不再先完成 V3 属性运行时，再迁移到 V4。
2. 不再保留 V1/V2/V3 属性或比赛上下文的正式兼容入口。
3. 不再迁移旧存档和旧回放；现有数据允许失效。
4. 不再把 `CompatibilityMapped` 作为生产运行时状态。
5. 不再以 `MatchReplayV1`/`MatchReplayV2` 的增量扩展作为最终回放方向。
6. V4 属性合同与 V3 触球规则版本相互独立；属性升级不要求重写已经稳定的规则引擎。

## 2. 已批准的新决策

### 2.1 单轨版本边界

正式数据流硬切为：

```text
Career V4 authored player
→ MatchContextV4
→ Full Rally runtime
→ MatchResultV4 + MatchReplayV4
→ Career V4 result consumption
```

正式 6v6 只接受原生 `MatchContextV4`。Career gateway 使用具体的 V4 context/result
类型，不再使用可以承载旧版本的通用 `IMatchContext` / `IMatchResult` 运行时入口。

V1/V2/V3 属性、context、result 和 replay DTO 已从生产源码删除。Bootstrap、Career、
MatchSet、3v3 prototype、正式 6v6 和 replay 只使用 V4；不存在 V1/V2/V3 到 V4 的
loader、adapter、fallback 或自动迁移。

### 2.2 规则版本与属性版本分离

下列 V3 规则资产继续作为权威实现：

- `RallyWorldSnapshotV3`
- `OnCourtEligibilitySnapshot`
- `OnCourtLineupRulesV3`
- `TouchSequenceStateV3`
- `RallyRulesEngineV3`
- `ActionEligibilityRulesV3`
- `BoundaryAndNetRulesV3`
- `FullRallyV3RulesRuntimeAdapter`

这些类型表达触球序列、场上资格、攻拦资格和边界规则，不依赖球员属性 DTO 的版本。
V4 context 负责向规则层提供球员身份、阵容、轮转和实际几何事实。除非排球规则本身
发生不兼容变化，不创建重复的 `RallyRulesEngineV4`。

### 2.3 V4 发布后的扩展规则

- V4 持久化 DTO 在发布后冻结。
- 调整既有属性的系数或组合公式，只升级公式版本和系数表版本。
- 新增权威基础属性或正式派生比赛属性时，创建 V5。
- 新属性必须同时具备 Career 来源、确定性推导、正式消费路径、回放解释和固定种子
  benchmark；缺少任一项时不得进入正式合同。
- 不使用 `Dictionary<string, float>` 或其他字符串扩展袋承载正式属性。
- 不提前序列化 `Reserved` 属性；实验指标先留在 benchmark/解释层。

## 3. Gate A–E 完成审计

审计基于当前 `codex/full-rally-v4-gates-a-e` 源码、变更记录以及 Unity
`6000.0.43f1` 的完整 EditMode/PlayMode 证据。

| 范围 | Gate A–E 冻结状态 | 证据 |
| --- | --- | --- |
| Shared / Career / Bootstrap | 只暴露具体 V4 player/team/context/result/replay；无兼容 reader 或 upgrade | 边界反射测试与零结果 legacy production `rg` |
| V4 derivation | 基础字段、六组派生字段、公式 V1、系数 V1 和 fingerprint 已冻结 | 边界、单变量、确定性、解释顺序与 strict JSON EditMode |
| V3 rules authority | `RulesVersions.FullRallyV3 = 3` 独立于 Match/Replay V4 | V4 context 向 V3 lineup/touch/eligibility/boundary rules 提供身份与事实 |
| Execution | planner 与 executor 使用同一 `ExecutionEnvelopeV4` 实例/identity；非法或越界样本显式分类且不修复 | envelope identity、分类、expanded/exceeded EditMode |
| Prediction | 两队对相同完整 key 获得相同 trajectory artifact；key 覆盖全部行为输入 | cache key、provider config、same-key artifact EditMode |
| P6 | attack eligibility 使用 observed takeoff/contact geometry | adapter EditMode 与正式 fixed-seed PlayMode |
| Replay | 仅 `MatchReplayV4`，记录派生解释、envelope、trajectory、sample classification、P6 和 V3 reason | canonical segment/hash EditMode 与两次 fixed-seed PlayMode |
| Shadow diagnostics | 不改变比分、accepted contacts 或 V3 transitions | diagnostics on/off PlayMode |

完整验证使用 Unity `6000.0.43f1`，从被验证 checkout 执行无 `-quit` 的
batch test 命令。删除三条仅覆盖旧 V3 Stage2 合同的测试并新增 one-shot
active-roster 枚举回归后，EditMode 为 `505/505` 通过；PlayMode 为 `24/24`
通过。两者均为 `0` failed、`0` skipped、`0` inconclusive。以下两个 production
搜索均无结果，且 `git diff --check` clean：

```bash
rg -n "PlayerAbilitySnapshotV[123]|MatchContextV[123]|MatchResultV[123]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'
rg -n "PlayerAbilitySnapshotV[123]|MatchContextV[12]|MatchResultV[12]|MatchReplayV[12]|InitializeV2|UpgradeFromV2" \
  Assets/Volleyball --glob '!**/Tests/**'
```

### 3.1 字段冻结与消费边界

V4 序列化冻结所有 physical/technical base 字段、`DominantHandV4` 和 Attack、Block、
Defense、Receive、Set、Serve 六组派生字段。每个 base 字段均有解释链和单变量派生
测试；`HeightMeters` 是结构性输入，只影响校验和 input fingerprint，数值触高只读取
`StandingReachMeters`，避免双重计数。

“已序列化/已派生”和“已由正式动作消费”分别记录。Gate A–E 已冻结当前
execution-envelope consumption evidence；soft action、完整攻防和 CourtAwareness 的
新增正式消费者仍属于 Gate I/J，不能因为字段已进入 V4 DTO 就宣称后续 gameplay
authority 已完成。

### 3.2 版本规则

- `ContractVersions.MatchV4 = 4` 与 `ContractVersions.ReplayV4 = 4` 只表示合同版本。
- `RulesVersions.FullRallyV3 = 3` 只表示排球规则 authority。
- 修改公式必须升级 formula version；修改系数表必须升级 coefficient version。
- 两类版本变化都必须进入 derived result fingerprint，即使数值结果偶然相同。
- 新增或改变权威 base 字段必须创建 V5，不得用 optional field、default 或扩展袋绕过。

## 4. V4 权威球员合同

### 4.1 球员身份与权威特征

`PlayerSnapshotV4` 包含：

```text
PlayerId
DisplayName
JerseyNumber
PlayerPosition
DominantHandV4
PhysicalBaseAttributesV4
TechnicalBaseAttributesV4
DerivedMatchAttributesV4
```

`DominantHandV4` 是必填枚举：

```text
Left
Right
```

它不是能力值，不参与总体能力评分。它必须实际影响攻击助跑/触球侧、发球触球几何、
单手应急二传选择和回放解释。普通双手二传与双臂拦网不获得惯用手数值加成。

### 4.2 身体基础属性

`PhysicalBaseAttributesV4` 是 Career 权威来源：

| 字段 | 单位/范围 | 权威含义 |
| --- | --- | --- |
| `HeightMeters` | `1.40–2.30m` | 身体高度输入 |
| `StandingReachMeters` | `1.70–3.10m` | 静止伸臂触及高度 |
| `Jump` | `[0,1]` | 垂直冲量与空中窗口 |
| `Mobility` | `[0,1]` | 加速、转移速度与覆盖 |
| `Reaction` | `[0,1]` | 识别和响应速度 |
| `Coordination` | `[0,1]` | 移动中身体控制和稳定性 |

`StandingReachMeters` 必须大于或等于 `HeightMeters`。任何非有限或越界输入都拒绝
构造，不做 clamp。

`Endurance`、体重、臂展和单独的助跑/拦网弹跳不进入 V4。它们只有在独立的权威消费
系统存在后才能进入 V5。

### 4.3 技术基础属性

`TechnicalBaseAttributesV4` 的全部值为 `[0,1]`：

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

这些是 Career 可生成、成长和训练的唯一技术权威源。比赛运行时不得重新解释同名
基础属性；运行时只读取派生比赛属性。

### 4.4 不进入 V4 的候选属性

- `BodyMass`：当前没有身体惯性或身体碰撞权威。
- `Endurance/Stamina`：当前没有疲劳权威。
- `ArmSpan`：现阶段由站立摸高表达实际接触价值。
- `Balance/Agility`：与 Coordination/Mobility 重叠。
- `Consistency/Composure`：当前没有压力状态；普通稳定性由协调和专项技术表达。
- `Strength`：当前由 AttackPower 表达；尚无独立拦网对抗或身体碰撞消费。
- `Vision/DecisionMaking`：由 CourtAwareness 和 Reaction 表达。
- `Aggression/Tendency`：属于战术倾向，不属于能力合同。

## 5. 确定性派生层

`MatchAttributeDerivationV4` 是纯函数：

```text
(PhysicalBaseAttributesV4,
 TechnicalBaseAttributesV4,
 DominantHandV4,
 MatchAttributeFormulaVersion,
 MatchAttributeCoefficientVersion,
 MatchConfigurationV4)
→ DerivedMatchAttributesV4
```

`DerivedMatchAttributesV4` 包含：

- 不可变 `MatchAttributesV4`
- 公式版本
- 系数表版本
- 基础输入规范哈希
- 派生结果规范哈希
- 确定性解释项

### 5.1 六组正式比赛属性

| 组 | 必需子属性 | 主要来源 |
| --- | --- | --- |
| Attack | `AttackContactHeight`, `AttackWindow`, `AttackControl`, `AttackPower` | height/reach/jump/reaction/coordination/attack technique/power |
| Block | `BlockContactHeight`, `BlockCoverage`, `BlockControl` | height/reach/jump/mobility/reaction/block technique |
| Defense | `DefenseCoverage`, `DigControl`, `RecoveryControl` | mobility/reaction/coordination/defense technique |
| Receive | `ReceiveCoverage`, `ReceiveControl`, `ReceiveStability` | mobility/reaction/coordination/receive technique |
| Set | `SetReachability`, `SetControl`, `EmergencySetControl` | mobility/reaction/coordination/set technique/court awareness |
| Serve | `ServeControl`, `ServePressure` | serve technique/attack power/coordination |

正式 Match runtime 只能读取 `MatchAttributesV4`。基础身高、弹跳和技术值不得在 planner、
executor、物理组件或回放 recorder 中被二次读取并产生另一套隐藏公式。

### 5.2 C 型权衡策略

- 身高、站立摸高和弹跳提高攻击/拦网接触几何。
- 弹跳不直接提高攻击力量。
- 技术和力量仍是 AttackControl/AttackPower 的主要输入。
- 高大体型对接发和防守只有小幅、有上限的修正。
- Coordination、Mobility、Reaction 和专项技术可以抵消该修正。
- 主要位置权衡来自 Career 的球员生成/成长预算，不在比赛内按位置添加隐藏 bonus。
- 所有系数集中在版本化系数表，满足边界、有界性和声明的单调性。

### 5.3 发布闸门

V4 可以分步开发，但在下列条件全部满足前，不宣称 V4 属性权威完成：

1. 六组派生属性均有正式运行时消费者。
2. 九个技术基础字段和六个身体基础字段均能通过解释链追踪到至少一个派生值。
3. 每个派生值都有边界和单变量测试。
4. 任何基础字段都不是仅序列化、仅展示或仅 benchmark 的装饰字段。
5. replay 能记录 `base → formula → derived → action/envelope → outcome`。

## 6. V4 Shared 合同与 canonical identity

新增并固定：

```text
ContractVersions.MatchV4 = 4
ContractVersions.ReplayV4 = 4
PhysicalBaseAttributesV4
TechnicalBaseAttributesV4
MatchAttributesV4
DerivedMatchAttributesV4
PlayerSnapshotV4
TeamSnapshotV4
MatchContextV4
PlayerMatchStatsV4
MatchResultV4
MatchReplayV4
```

`MatchContextV4` 的 canonical hash 包含：

- 两队原生 V4 球员与阵容顺序
- 基础属性和 DominantHand
- 派生属性及其公式/系数版本
- seed、规则配置、物理配置
- 影响确定性行为的 envelope policy、candidate policy、work budget 和 predictor config

`MatchResultV4` 与 `MatchReplayV4` 使用独立 hash family。三类 hash 不得复用同一
包含表。所有 canonical JSON 使用固定字段顺序、语义数组顺序、InvariantCulture、
明确浮点规范、UTF-8 和 SHA-256。

## 7. 共享执行合同

### 7.1 `ExecutionEnvelopeV4`

每次动作由唯一工厂生成：

```text
DerivedMatchAttributesV4
+ action/candidate class
+ baseline target/velocity
+ effort
+ deterministic policy/config
+ seed/event/sample identity
→ ExecutionEnvelopeV4
```

planner 创建 envelope，并把同一实例放入选中 plan/decision。executor 只能消费该实例
或其规范序列化身份；不得重新构造 baseline、误差分布或样本。

envelope identity/hash 覆盖：

- envelope/schema version
- 派生属性 fingerprint 和实际消费字段
- action/candidate class
- baseline target/velocity
- 基础边界和最大扩展边界
- effort 及其上限
- candidate-class policy
- sample count、sample ordering 和全部样本
- degradation ladder
- source/provenance

相等性与 hash 必须覆盖完整不可变合同，不能只比较 Phase 0 identity 字段。

### 7.2 样本分类

```text
UnexpectedExecutionSample
  sample key、candidate class 或 envelope identity 不属于所选 envelope

WithinEnvelope
  目标、速度、方向、时机和 effort 均在基础边界

EnvelopeExpanded
  超出基础边界，但仍在声明的最大扩展边界

EnvelopeExceeded
  超过最大扩展边界、力量上限或 effort 上限
```

分类后保留实际样本。任何路径都不得静默 clamp、重采样或替换为较安全样本。

### 7.3 属性责任

- `AttackControl` 改变正常强攻的瞄准、方向和速度误差。
- `AttackPower` 改变允许的力量/速度上限和达到目标速度所需的相对 effort。
- 固定 AttackPower 时，改变 AttackControl 不得改变力量上限。
- `SoftTouch` 只消费于 tip/roll/push/survival/tool 等对应动作。
- `BlockControl`/`BlockCoverage` 只消费于拦网手型、覆盖和受控反弹。
- CourtAwareness 通过派生的组织/感知属性影响可见事实解释和支援选择，不直接改变移动速度、
  触球力学或规则合法性。

## 8. 共享物理预测

每个 rally 建立一个 `BallTrajectoryPredictionProviderV4`。两队门 5 查询同一 provider。

缓存键完整覆盖：

```text
ball-state version
physics config hash
sample key
predictor version
step seconds
maximum time
maximum samples
deterministic work/degradation mode
```

工件规范身份覆盖同一组行为输入。相同输入返回同一工件实例和 identity；任何行为输入
不同都必须产生不同键。cache hit/miss 时间和 profiler 数据不进入 canonical hash。

## 9. P6 实际攻击几何

物理接触候选在碰撞时提供：

- actor 和 team
- actual takeoff point
- actual contact point
- attack-line distance
- net height
- contact group

`FullRallyV3RulesRuntimeAdapter` 在 attack contact 的 evaluate/commit 路径中，用这些
实际事实调用 `AttackEligibilityRulesV3`。计划点、派生接触高度、角色或动作名称不能替代
实际起跳/触球几何。拦网资格继续复用现有 V3 rule path。

## 10. Career 硬切

`CareerPlayerRecord` 改为保存：

```text
PlayerId
DisplayName
PlayerPosition
DominantHandV4
PhysicalBaseAttributesV4
TechnicalBaseAttributesV4
```

Career 创建比赛时调用 V4 derivation，并生成 `MatchContextV4`。Career 读取
`MatchResultV4`；gateway 签名使用具体 V4 类型。

不实现：

- V1/V2/V3 存档读取
- 旧属性估算
- 默认填充缺失 V4 字段
- 通用接口中的运行时版本猜测

## 11. Match 与 Bootstrap 硬切

- `FormalSixVsSixRallyBootstrap` 直接创建原生 `MatchContextV4`。
- `PhysicalMatchRallyDirector.InitializeV4` 是正式 6v6 唯一入口。
- `MatchSet` 原生持有 `MatchContextV4` 并生成 `MatchResultV4`。
- 正式 director 不暴露 V1/V2 result/context。
- V3 rules adapter 直接从 V4 context 创建 eligibility，不要求 `UpgradeFromV2` hash parity。
- `PlayerAbilityProfile` 保留为 `DerivedMatchAttributesV4` 的 runtime wrapper；它不持有
  V1/V2/V3 属性合同，也不绕过 V4 derivation。
- 3v3 已同步硬切 V4：每队创建六人 V4 roster，并向 `MatchSet` 显式传入三名
  active player ID，不保留旧属性或 context 入口。

## 12. Replay V4

Replay V4 记录：

```text
base attributes + DominantHand
→ formula/coefficient versions
→ derived match attributes and explanations
→ world/rules snapshot identity
→ planner candidates/responsibilities/routes
→ selected ExecutionEnvelopeV4
→ shared trajectory artifact
→ actual execution sample and classification
→ actual takeoff/contact geometry
→ V3 rules transition
→ score/result
```

固定 seed、context 和输入必须产生字节一致的规范 replay 段。可视化状态、cache hit
时间、wall-clock profiler 数据和 HTML 本地 UI 状态不进入 canonical replay hash。

## 13. 后续 Full Rally 架构保留项

原 V3 架构中的以下目标继续有效：

- 每次 accepted contact 产生规则转换和确定性 `PlanCoverageDecision`。
- 两队各六名场上球员拥有兼容责任。
- 候选先过合法性、到位、几何、执行、物理采样、下一状态六道门。
- 两队只共享权威物理工件，不共享对手隐藏计划和未来实际样本。
- 防守对攻击威胁分布响应，不读取隐藏最终路线。
- 安全球和 block-tool recovery 在同一 fallback pool 竞争。
- committed 动作不能被 replan 瞬间取消或传送。
- `PhysicalMatchRallyDirector` 最终只保留事件编排、规则、生命周期、得分和 replay dispatch。

责任计划、感知、executor 和 director 瘦身不因属性硬切而重写；它们改为消费
`MatchAttributesV4`、`ExecutionEnvelopeV4` 和 Replay V4 identity。

## 14. 新交付路线

### Gate A：冻结历史基线（已完成）

- 固定当前 Phase 1 规则、Stage 1.5 组织、连续攻击动作和正式 6v6 回归。
- 为当前 Stage 2 缺陷添加失败测试，证明新 V4 实现必须解决的问题。
- 记录当前 EditMode/PlayMode XML 作为硬切前证据。

### Gate B：V4 Shared + Derivation（已完成）

- 新增全部 V4 DTO、版本显式 JSON 和独立 canonical hash family。
- 实现 DominantHand、基础范围和严格验证。
- 实现版本化纯 derivation、解释项和六组派生属性。
- 建立边界、单调性、C-policy 和 archetype EditMode tests。

### Gate C：Career/Match 原生 V4 硬切（已完成）

- Career record/gateway、正式 Bootstrap、MatchSet、player binding 和 result 全部切 V4。
- 删除正式 V1/V2/V3 context/result 路径；`PlayerAbilityProfile` 仅保留为
  `DerivedMatchAttributesV4` 的 runtime wrapper。
- 正式 6v6 在原有 V3 rules authority 下完成比赛并生成 V4 result。
- 当前已有动作全部改为消费对应的 V4 派生属性，且 runtime 不得直接读取基础属性。
- V4 全属性权威在 Gate I/J 激活 soft action、完整攻防和 CourtAwareness 前不得宣告完成。

### Gate D：共享执行、预测与 P6（已完成）

- 以 TDD 重建 `ExecutionEnvelopeV4`、policy hash、分类和 planner/executor identity。
- 建立 per-rally shared trajectory provider 和完整 cache key。
- 把实际起跳/触球几何接入 V3 attack eligibility。
- EditMode 验证 AttackControl 与 AttackPower 独立、两队同工件、非法样本不修复。
- PlayMode 验证 diagnostics on/off 不改变比分、accepted contacts 或 V3 transitions。

### Gate E：Replay V4（已完成）

- 建立规范事件/段 schema、canonical replay/frame hashes 和 recorder。
- 写入 V4 派生解释、envelope、predictor、sample classification、实际几何和规则原因码。
- 固定 seed 重复运行产生相同字节和 hash。
- HTML overlay 只渲染实际消费字段和明确来源。

### Gate F：十二人责任计划 Shadow（已完成）

- 复用原 Stage 3 的 `RallyPlan`、`TeamRallyPlan`、六人 assignment、claims 和 beam composition。
- 两队从同一 V4 world/physical artifact 创建 shadow plans。
- shadow 只写 Replay V4，不能发 movement/contact command。

### Gate G：Player 组件边界（已完成）

- 在现有 facade 后拆出 locomotion、action、contact surface、technique execution 和 presentation。
- 保持 Stage 1.5 连续动作与 `0.18m` 攻击对齐基线。
- technique executor 只消费 `ExecutionEnvelopeV4`。

### Gate H：接发与组织权威（已完成）

- 责任 plan 接管接发、二传预备、应急代传、覆盖和攻击预备。
- 复用唯一 `SetterOrganizationZone` 和注册二传可达性规则。
- 移除对应 legacy tactical writer。

### Gate I：攻防与重组权威（已完成）

- plan/executor 接管 set target、攻击路线、联合拦防、soft action、tool recovery 和重组。
- V4 Attack/Block/Defense/SoftTouch 派生属性进入各自唯一消费路径。
- 实际防守只能在攻击触球后基于可见事实反应。

### Gate J：感知与 CourtAwareness

- 在共享权威物理工件外层建立确定性感知视图。
- CourtAwareness 只影响观察延迟、不确定性、可见动作解释和支援选择。
- 对手隐藏计划、最终路线和未来样本保持不可见。

### Gate K：Director 瘦身与 Replay/Calibration 完成

- 删除固定 `Receive → Organize → Attack` tactical ownership 和重复 writers。
- director 只保留物理事件、规则、生命周期、得分、result 和 replay dispatch。
- 完成双视角 overlay、固定种子矩阵、work-budget/degradation 证据和性能报告。

## 15. 验收矩阵

### 15.1 Shared EditMode

- V4 DTO 拒绝非有限值、越界值和未知枚举。
- StandingReach/Height 关系验证准确。
- Context/Result/Replay V4 round-trip 和 canonical hash 字节稳定。
- 正式 API 无法接受 V1/V2/V3 context。
- 公式版本、系数版本和派生 fingerprint 均进入正确 hash。

### 15.2 Derivation EditMode

- 每个派生字段在最小、最大和代表性输入下有界。
- Height/StandingReach/Jump 提高声明的几何属性。
- Jump 不改变 AttackPower。
- 高大体型的一传/防守修正小且有上限。
- Coordination/专项技术可以抵消该修正。
- 同输入与版本产生完全相同的派生值和解释。

### 15.3 Match EditMode

- planner/executor 使用同一 envelope 实例/identity。
- AttackControl 改变误差，不改变力量上限。
- effort、速度、目标和扩展边界全部参与分类。
- candidate policy、sample count 和 degradation ladder 进入 hash。
- 两队对相同 sample 获得同一 trajectory artifact。
- predictor/config/degradation 任一变化产生不同 cache key。
- P6 根据实际起跳/触球点判断资格。
- DominantHand 改变声明的触球侧，不改变无关属性。

### 15.4 PlayMode

- 正式 6v6 从原生 V4 context 开始并生成 V4 result/replay。
- V3 rule transitions、accepted contacts 和比分保持一一对应。
- diagnostics/cache/replay 开关不改变比赛权威结果。
- 正常组织、二传首触、代传、连续攻击、拦网反弹和 P6 非法攻击均有场景。
- committed player 不传送、不取消已起跳动作。

### 15.5 Benchmark

- 技术型与力量型主攻在等预算下显示控制/速度差异。
- 高弹跳球员在固定力量/技术下只改变几何和窗口。
- 高身高/摸高球员显示攻击/拦网收益和小幅一传/防守修正。
- 高大且高协调/接发技术球员仍能成为优秀一传。
- 二传、副攻、自由人、接应和主攻的关键派生属性分别改变其正式路径。
- 固定 seed 的 replay 规范段和 hash 重复运行一致。

## 16. 明确非目标

- 不迁移或读取旧 V1/V2/V3 存档和 replay。
- 不创建 V4 触球规则引擎副本。
- 不加入疲劳、体重、身体碰撞、伤病或耐力。
- 不在一次比分结果上做平衡结论。
- 不让 wall-clock 时间改变确定性决策。
- 不在 runtime 直接读取基础属性绕过 derivation。
- 不在同一子系统长期保留新旧两个权威 writer。

## 17. 完成定义

本路线只有在以下条件全部满足时完成：

1. Career、正式 6v6、result 和 replay 都只使用 V4 合同。
2. V4 的全部基础属性与 DominantHand 都有可追踪的正式消费路径。
3. Match runtime 只读取派生比赛属性。
4. planner 与 executor 共享同一 execution envelope。
5. 两队共享同一权威物理预测工件。
6. P6 攻击资格使用实际物理几何。
7. 每次 accepted contact 由既有 V3 rule authority 处理。
8. Replay V4 可解释 `base → derived → plan/envelope → actual → rule/result`。
9. fixed-seed canonical replay 字节和 hash 稳定。
10. director 不再包含战术候选、角色、路线或误差选择逻辑。
