# Full Rally V4 Gate J 感知与 CourtAwareness 设计

## 1. 状态与目标

- 日期：2026-07-27
- 状态：设计已确认
- 起点：`main` 的 Gate I 完成提交 `d3121b9`
- 范围：正式 6v6、`V3RulesMode.Authority`

Gate J 以确定性观察视图替代 Gate H/I 当前的 zero-error perception adapter，
使 `CourtAwareness` 仅影响对可见事实的识别延迟、估计误差、置信度和本方支援
责任选择。它不改变权威物理、规则、执行 envelope、移动速度、触球机制或攻击/拦防
路线合法性。

本阶段不实现“对对手可见动作的预测”。防守只基于延迟且不确定的公开威胁、实际球和
可见站位作出支援选择；它不会推断对手最终路线、未来 execution sample 或内部准备状态。

## 2. 已确认决策

1. 采用独立的确定性感知适配层，而不是将 awareness 系数散落在 Gate H/I planner。
2. 每位观察者和每支队伍从同一权威物理工件生成只含可见字段的不可变 view；不能为
   观察者重新计算 trajectory 或 reachability。
3. `CourtAwareness` 的唯一正式作用域为 observation delay、bounded uncertainty、
   confidence、visible-action interpretation 与 support selection。
4. 本方已提交 responsibility/command 可精确共享；本方未来执行结果和实际可达性仍不是
   已观察事实。
5. Gate J 只切换 formal 6v6 Authority；3v3、Shadow、Disabled 和不完整 roster 保留
   Gate I 的 zero-error adapter。
6. 新 Replay evidence 必须区分 authoritative artifact、perceived view 和由 view 引发的
   support decision；不得把隐藏值写作 observed fact。

## 3. 信息边界

### 3.1 可观察输入

- 已接受的球 state、trajectory artifact identity、物理 simulation time；
- 已公开的 `PublicAttackThreatV3`：action class、zone、probability、arrival time；
- 已物理可见的对手/队友 position、movement、pose、已开始 action cue；
- 本方已提交的 assignment、contact command 和 responsibility；
- 观察者自身派生 `Defense.Awareness`、`Receive.Awareness` 或 `Set.Awareness`。

### 3.2 严格隐藏的输入

- `AttackDefensePlanV3.SelectedAction`、未公开 candidate value 与 final route；
- 对手 conditional branch、future execution sample、envelope error、实际未来 rebound；
- 对手内部能力、未开始 action、真实 future reachability；
- 来自另一个观察者的未过滤权威 snapshot。

隐藏字段不进入 perception constructor，且不得在 adapter 之后以 side channel 回读。

## 4. 架构与数据流

### 4.1 Domain perception values

在 `Match/Runtime/Domain/FullRallyV3` 新增无 Unity 依赖的不可变值：

- `PerceptionObservationV3<T>`：estimate、uncertainty、confidence、observed-at、
  source identity、deterministic uncertainty key；
- `PlayerPerceptionSnapshotV3`：某位球员在某时刻对 ball、visible threat 与 teammate
  support readiness 的观察；
- `TeamPerceptionSnapshotV3`：按 team 聚合的公开 threat 和可选支援候选；
- `PerceptionSupportDecisionV3`：由 team view 选择的 support actor/zone、revision、
  view identity 和 fallback reason。

这些类型只保存观察结果和 provenance，不保存 `GameObject`、coordinator 或原始
隐藏 plan。所有 identity 使用 revision、event/source sequence、observer、subject、
observation kind 和 canonical configuration 组成。

### 4.2 `CourtPerceptionAdapterV3`

放在 `Match/Runtime/AI` 的纯 adapter 接收：已过滤的 authoritative public inputs、
observer derived awareness、match deterministic seed 和 source sequence。它执行：

1. 从 awareness 导出有界 delay、position/arrival uncertainty 与 confidence；
2. 使用确定性 hash/key 选择误差符号和幅度；同输入重复得到相同 view；
3. 只对可见字段施加 delay/error；不更改传入的 trajectory artifact；
4. 聚合本队观察，按 freshness、confidence、assignment relevance 生成 team view；
5. 在固定的责任候选集合中选择 support actor/zone，并将 low confidence 显式降级为
   conservative/已有责任，而不是伪造精确对手路线。

它不发布 player command、不调用 Unity、不读取 `PhysicalMatchRallyDirector`，也不改变
`PublicAttackThreatV3` 本身。

### 4.3 Gate H/I integration

`PhysicalMatchRallyDirector` 仍读取权威 ball/rules/roster facts，但只在 formal Authority
边界构造 public perception input。接发/组织与攻防 coordinator 继续拥有 revision、
fallback、command 和 lifecycle。

- Gate H：用 `TeamPerceptionSnapshotV3` 选择支持型 receive/organization responsibility，
  已提交 primary receive 与 registered setter 的规则资格不被 perception 推翻。
- Gate I：用 perceived public threat 为 `JointDefensePlanner` 的 support/floor coverage
  选择输入；拦网 eligibility、公开 threat identity 和实际 Block/Receive acceptance 仍由
  原有 authority/rules 处理。
- command 一经 coordinator committed 不因后来观察刷新而取消；新 view 只能在现有
  coverage/revision boundary 触发可允许的 support replan。

## 5. CourtAwareness 模型

以已有派生 awareness 字段为唯一输入，所有量范围固定并写入 configuration identity：

```text
normalized awareness a in [0, 1]
observation delay     = lerp(0.30 s, 0.05 s, a)
position uncertainty  = lerp(1.20 m, 0.08 m, a)
arrival uncertainty   = lerp(0.35 s, 0.03 s, a)
confidence            = clamp01(1 - normalized uncertainty)
```

具体常数作为 versioned `CourtPerceptionConfigurationV3` 的字段，不能散落在 planner。
不同 observation kinds 可声明不同 bounded multiplier，但必须单调：高 awareness 不得
增加 delay/uncertainty 或降低同等可见输入的 confidence。

随机性由 canonical deterministic key 的 hash 映射到有界误差；不读取 wall clock、
`UnityEngine.Random` 或缓存命中时间。相同 seed/revision/input 产生相同 bytes/decision。

## 6. 支援选择与失败语义

`PerceptionSupportSelectorV3` 只在 Gate F/H/I 已给出的合法 roster、claim 和责任候选内
排序：先排除 rules-ineligible/hard-conflict actor，再按 perceived arrival margin、
confidence、已提交 responsibility continuity 与 stable player id 选择。

- 高置信 view 可选择最优的可见 support candidate；
- 低置信或 observation 尚未 fresh 时保留已有 committed responsibility，或选择预先声明的
  conservative coverage；
- 没有合法 actor 时返回 no-support evidence，让已有 rules/physical outcome 结算；
- 不得以“感知失败”为由 teleport、生成 legacy command、重采样 trajectory 或读取 hidden
  final route。

## 7. Replay 与兼容性

Replay V4 以可选 Gate J record 记录：perception configuration identity、observer/team、
authoritative artifact identity、observed-at、delay、uncertainty key/range、confidence、
visible threat entries、support decision 和被其影响的 authority revision。

历史 Replay 没有 Gate J record 时 canonical bytes/hash 和 strict reader 保持不变。新 record
使用固定 canonical field order；reader 验证它不含 hidden selected route、future sample 或
对手内部能力。recorder 只映射 event-owned perception receipt，不读取 coordinator 的最新
view 以补齐旧事件。

## 8. 非目标

- 不预测对手可见动作的下一步路线；
- 不改变强攻、soft action、block 或 receive 的 execution envelope；
- 不改变 movement speed、contact geometry、rules legality 或实际 trajectory；
- 不接管 Gate K 的 director slimming、Replay UI、双视角可视化或全量 calibration；
- 不迁移 3v3、Shadow/Disabled 或非完整 fixture。

## 9. 验收与验证

1. EditMode：相同 seed/input 产生 byte-equivalent player/team view 和 support decision。
2. EditMode：仅提高 awareness 时 delay/uncertainty 单调下降、confidence 单调上升；不可
   改变 trajectory/envelope/route legality/physical movement input。
3. EditMode：reflection/data tests 证明 perception contracts 不保存 hidden final route、
   future sample、Unity presentation 或 coordinator reference。
4. EditMode：相同 public threat、不同 hidden final route 得到完全相同 view/decision。
5. EditMode：低置信 view 选择 conservative/committed support，且 deterministic tie-break
   不依赖 enumeration order。
6. PlayMode：formal 6v6 覆盖 delayed and normal recognition，均通过同一 V3 rule
   authority，无 duplicate writer、teleport 或 hidden physical-fact mutation。
7. PlayMode：3v3 与 V3 Shadow/Disabled 不创建 Gate J adapter 或 perception receipt。
8. Replay：canonical Gate J capture round-trip，recorder on/off authority invariance 和两次
   fixed-seed bytes/hash stability。
9. 完整 EditMode、PlayMode、static dependency/hidden-field scans、combined review 与
   `git diff --check` 全绿。

## 10. 实施分段

1. 先建立 Domain contracts、configuration 和纯 adapter/selector，并以红绿 EditMode
   固定信息边界与确定性。
2. 再为 Gate H/I 增加 event-owned perception handoff，在正式 6v6 的允许 revision
   boundary 消费 support decision；先做 focused PlayMode。
3. 最后扩展 Replay V4、完整 fixed-seed/regression suites、change record 和路线图。

这是一项跨越 live runtime、authority lifecycle、replay canonicalization 和三个以上双向
模块边界的改动；验证采用完整受影响 suite、独立 review 和 fresh determinism evidence。
