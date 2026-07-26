# Full Rally V4 Gate I 攻防与重组权威设计

## 1. 状态与目标

- 日期：2026-07-27
- 状态：设计已确认
- 起点：Gate H 完成提交 `aaa7fc8`
- 分支：`codex/full-rally-v4-gate-i-attack-defense-reorganization-authority`

Gate I 将正式 6v6 的 set target、攻击候选、攻击路线、联合拦防、soft
fallback、block-tool recovery、attack cover 和直接重组迁移到一条可解释、
确定性的计划权威路径。它必须复用 Gate F 的不可变责任计划、Gate G player
facade、Gate H handoff、V4 execution envelope、共享 trajectory artifact 和
V3 rules authority。

Gate I 完成后，正式 6v6 对上述职责只有一个 writer。3v3 保持 legacy，Gate J
继续负责 perception 与 `CourtAwareness`，Gate K 继续负责 director 最终瘦身、
Replay UI 与 calibration 收口。

## 2. 已确认的设计决策

1. Gate I 只迁移正式 6v6；3v3 保留 legacy 并作为回归基线。
2. Gate I 在一个设计内分三个可独立验证的内部里程碑：
   - 攻击候选、路线合法性与 soft fallback；
   - threat distribution 与联合拦网/地面防守；
   - block-tool recovery 与触球后重组。
3. 三个里程碑完成后一次性切换正式 6v6 authority，不保留长期 feature flag
   或正式双 writer。
4. Gate I 允许正式固定 seed 的比分和触球序列相对 Gate H 改变；必须保持规则
   合法、比赛可终止、确定性、Replay 字节稳定、无 duplicate writer、无瞬移，
   并保持 3v3 行为。
5. 采用“分层不可变工件 + 单一 coordinator”方案。现有几何与物理算法作为纯
   评估器复用，不以 facade 包装伪装成新权威，也不做无界的攻防全局搜索。

## 3. 范围

### 3.1 Gate I 接管

- set target 与 arrival envelope；
- attacker preparation 与最终 attacker；
- power line、cross、edge、over-hand route；
- tip、roll、push、high-survival、block-out 与 block-tool recovery；
- 公开 attack threat distribution；
- 主/协同拦网与 line/cross/deep/tip 地面覆盖；
- block shadow、rebound coverage 与 attack cover；
- 实际 Attack、Block、Defense、recovery contact 后的 coverage/replan；
- block/dig/tool rebound 后的直接 organization 或 re-attack exit；
- 上述选择、执行和重组的 Replay V4 authority evidence。

### 3.2 Gate I 不接管

- 3v3 tactical writer；
- lineup、rotation、触球、libero、前后排、block eligibility 与得分规则；
- Gate H 的 receive、registered setter、organization fallback；
- Gate J 的 perception latency、uncertainty、confidence 和
  `CourtAwareness`；
- director 的比赛生命周期、物理事件、得分、result 和 Replay dispatch；
- HTML/双视角 overlay、性能 calibration 和最终 legacy 清理。

## 4. 总体架构

### 4.1 `AttackDefensePlanV3`

`AttackDefensePlanV3` 是不可变、无命令的计划值。它保存：

- side、revision、source plan identity；
- organization handoff 与 set target；
- attacker preparation 与合法 attack candidates；
- 对外可见 threat distribution；
- joint defense responsibilities；
- power reliability gate 与统一 fallback pool；
- selected action、attack cover 与 reorganization exits；
- declared conditional branches、coverage envelope 和预算。

该类型不引用 `UnityEngine`、`PhysicalMatchRallyDirector`、
`PrototypePlayerAgent`、controller 或 recorder。

### 4.2 `AttackDefensePlanner`

纯规划器依次执行六道门：

1. rules eligibility；
2. arrival feasibility；
3. contact geometry；
4. V4 execution envelope；
5. shared trajectory samples；
6. next-state value。

它复用并纯化现有：

- `SetQualityAssessment`、`SetAttackReplanner` 与 `SetFlightSolver`；
- `AttackContactPlanner` 与 `AttackRouteSelector`；
- `BlockUnitPlanner`、net interception 和 block geometry；
- trajectory provider、execution envelope factory 和 V3 eligibility。

非法、不可达、几何不成立或多数样本不过网的候选在评分前移除。planner 不发
player 命令，不读取 MonoBehaviour 状态，也不采样 wall-clock 随机数。

### 4.3 `AttackDefenseAuthorityCoordinator`

coordinator 是 Gate I 唯一生命周期权威，管理：

- monotonically increasing revision/source sequence；
- organization handoff；
- threat publication；
- defense commitment；
- final attack commitment；
- actual Attack/Block/Defense/recovery contact；
- conditional branch activation；
- local/scoped/global/terminal coverage；
- tool recovery 与 post-contact reorganization。

所有事件必须携带 plan identity、revision、source sequence、actor、branch、
envelope identity 和 trajectory identity。stale、duplicate 或与当前 phase
不兼容的事件在发布命令前拒绝。

### 4.4 `AttackDefenseAuthorityController`

Presentation controller 将 coordinator 的已批准命令批次映射到 Gate G facade。
它必须先完整 preflight：

- actor 属于当前正式六人；
- actor/责任/branch 与 plan 一致；
- rules eligibility 与 action kind 一致；
- execution classification、sample 和 trajectory identity 一致；
- movement/contact/block/support 时间有限且有序；
- committed command 不被取消。

只有整个批次通过后才原子提交 set preparation、attack、block、floor defense、
cover 或 reorganization。controller 不重新选择 actor、target、route、fallback
或 envelope。

### 4.5 `PhysicalMatchRallyDirector`

director 只提供：

- authoritative ball/rules/lineup facts；
- actual contact、environment contact 与 crossing；
- simulation/lifecycle time；
- score、result 与 Replay event dispatch。

Gate I 正式路径中，director 不直接调用 legacy selection 来决定 set target、
attack route、block unit、cover player 或 post-block continuation。现有算法可由
planner 作为纯评估器复用。Gate H 与 Gate I 使用双阶段 handoff：

1. Gate H 进入 `OrganizationPlanned` 后，把 organizer、预计 Set 触球、实际一传
   和当前规则事实交给 Gate I。Gate I 返回不可变 `SetIntent`，只拥有 set target、
   arrival envelope 与下游攻击准备。
2. Gate H 仍是 Set actor、timing 和 contact command 的唯一 writer，并执行该
   `SetIntent`。Set 实际被 V3 rules 接受后，Gate H 终结 organization 生命周期，
   Gate I 继续 threat → defense → final attack。

Gate I controller 不直接向 setter 再发第二个 Set contact command。实际
Attack/Block/Defense 接触仍先经 V3 rules authority。

Gate I 仅在以下条件全部成立时启用：

- `V3RulesMode.Authority`；
- formal roster size 为 6；
- 当前完整加载两队十二名球员；
- Gate H authority 已建立有效 organization handoff。

Shadow、Disabled、3v3 或不完整 fixture 不创建 Gate I controller。

## 5. Threat–Response–Choice 数据流

每个攻击机会只运行一次：

1. 从 Gate H `OrganizationPlanned` 的预执行 handoff 选择合法 set target，并把
   不可变 `SetIntent` 返回给 Gate H 的唯一 Set contact writer。
2. 生成 power、soft、block-out 与 tool-recovery candidates。
3. 用六道门得到候选资格、sample outcomes 与 expected rally value。
4. 发布 threat distribution，内容仅限类别、区域、概率、时序和可见准备事实。
5. 防守方基于 distribution 一次性组合 block 与 floor coverage。
6. 防守计划 committed 后，攻击方完成唯一一次 final choice。
7. final choice 之后只允许本方 attack-cover adjustment。
8. 实际攻击触球后，防守方才能根据可见轨迹、反应时间和已提交移动做有限调整。

防守方不能读取：

- 最终隐藏 route；
- 对手未来 execution sample；
- 对手未公开 plan branch；
- final choice 后为重新站位而生成的第二份 threat distribution。

Gate I 使用显式 zero-error perception adapter。双方可引用同一个权威物理预测
工件，但 adapter 仍执行隐藏字段过滤。Gate J 才增加 observation latency、
uncertainty 与 `CourtAwareness`。

## 6. 攻击候选与统一 fallback pool

### 6.1 Power routes

power line、cross、edge 与 over-hand 候选必须在评分前满足：

- V3 attack eligibility；
- attacker arrival 和 contact geometry；
- expected execution error 后仍具有可靠净高与落点；
- 绝大多数确定性物理样本合法过网；
- effort 不超过 `Attack.PowerCapacity`；
- 使用与执行相同的 envelope 和 trajectory identity。

达到 reliability gate 的 power candidates 在 power 集内竞争。若没有可靠 power
route，不能强制选择最高原始分的危险线路。

### 6.2 Fallback pool

以下候选进入同一个 fallback pool：

- tip；
- roll；
- push；
- high-survival；
- block-out；
- block-tool recovery。

所有候选用 expected rally value 比较；soft action 与 tool recovery 没有固定优先
顺序。每个候选保留淘汰原因、有效 sample 比例、continuation value 和
immediate-loss risk。

## 7. 联合拦防

joint defense 同时组合：

- primary/supporting blockers；
- line、cross、deep、tip floor defenders；
- block shadow；
- rebound coverage；
- post-dig organization 与 counterattack exits。

防守价值是 threat-weighted save/continuation probability，减去重叠、空区、
非法 assignment 和无法重组风险。floor coverage 优先覆盖 block 未封住的残余威胁，
不能与 block 重复堆叠同一区域来虚增分数。

blocker 与 defender 的 movement corridor、contact space 和 recovery space 使用
Gate F claim/conflict 数据。hard conflict 在组合时淘汰；cooperative adjacent
block 与互补 coverage 获得有限加分。

## 8. Block-tool recovery 与重组

tool recovery 只有在全部条件成立时才有效：

1. sampled route 合法触及预测 blocker 手臂；
2. 球反弹回 attacking side；
3. 除 attacker 外至少一名合法队友在落地前可达；
4. rebound 有正控制 margin、可打高度和可用时间；
5. 仍有合法 counted contact；
6. 存在明确 organization 或 re-attack exit。

缺少任一条件即在评分前淘汰。block collision 本身不等于成功 recovery。

成功 tool contact、普通 block rebound、dig 或 incidental contact 必须先由 V3
rules 确认 actual touch sequence，再由 coordinator 处理：

- declared condition 内：activate covered branch；
- responsible actor changed：local revision；
- ball envelope exceeded：scoped replan；
- Gate I 预算或责任结构失效：request global rebuild；
- rally end：terminal。

重组从 actual contact、remaining touches、ball state 与 current responsibilities
重新识别 recovery/organization/attack opportunity，不恢复固定
`Receive → Organize → Attack` 阶段链。

committed jump、contact、block 或 support action 不能被 revision 瞬间取消或传送。
新计划从 committed action 的实际 continuation state 开始。

## 9. V4 能力消费

Gate I 的唯一消费路径为：

- `Attack.DirectionControl`：power route direction error；
- `Attack.SpeedControl`：power route speed error；
- `Attack.PowerCapacity`：power velocity/effort capacity；
- `Set.PlacementControl`：set target error；
- `Set.TempoControl`：set arrival-time/velocity error；
- `Set.Movement`：organizer/set arrival；
- `SoftTouch`：tip、roll、push、high-survival、block-out controlled touch 与
  tool-rebound control；
- `Block.HandControl`：block contact/deflection direction；
- `Block.Timing`：block contact window；
- `Block.LateralMobility`：block arrival；
- `Defense.Reaction`：floor-defense reaction delay；
- `Defense.PlatformControl`：dig/defense execution error；
- `Defense.CoverageMobility`：floor-defense arrival。

`CourtAwareness` 不进入 Gate I planner score、movement speed、route legality、
execution envelope 或隐藏信息。它只在 Gate J perception 中激活。

固定 key 的单变量 tests 必须证明每个轴只改变声明的 reach、error 或 capacity。

## 10. Replay V4

Replay V4 增加可选 Gate I authority record，至少包含：

- plan revision/source sequence、phase、branch 与 coverage；
- set target、candidate class/value、eligibility 与 elimination reason；
- public threat distribution；
- joint defense responsibilities 与 coverage distribution；
- selected actor/action/route/target；
- tested/executable/sample envelope identity；
- trajectory artifact identity；
- blocker contact、rebound side、recovery teammate 与 reorganization exit；
- fallback pool comparable values 与 winner；
- consumed V4 ability field names；
- actual contact 与 V3 rule transition reference。

新正式 Set/Attack/Block/Defense/recovery event 必须携带 event-owned Gate I
receipt。recorder 只映射该 receipt，不读取 coordinator 当前或最新状态。
Set event 同时保留 Gate H organization receipt；Gate I record 只补充 set target
及下游攻防计划证据，不能替代或重复解释 Gate H organizer authority。物理 dig
仍可使用现有 `TechniqueAction.Receive`，但 Gate I receipt 必须以 responsibility
和 phase 明确标记其 Defense/Recovery 语义。

历史 Replay V4 没有 Gate I record 时保持原 canonical bytes/hash 和 strict reader
兼容。新字段使用固定 canonical 顺序；actor、action、route、envelope、trajectory、
rule transition 或 branch 不一致时拒绝读取。

## 11. 错误与失败处理

- planner 无合法 power route：进入统一 fallback pool；
- fallback pool 也为空：发布 terminal/no-plan，不伪造 contact；
- 无合法 defender：保留 uncovered risk，由实际物理与规则结算；
- trajectory/envelope identity mismatch：整个命令批次 preflight 失败；
- stale/duplicate callback：无 player mutation；
- committed responsibility invalidated：保留 committed actor/action，只从后继状态
  规划；
- global rebuild request：Gate I 停止本地扩张并交上层计划生命周期；
- Replay event 缺少新正式 Gate I receipt：capture 明确 invalid，不读取最新状态补齐；
- 3v3 或非 Authority：不创建 Gate I 状态，不影响 legacy writer。

## 12. 实施与切换顺序

### 里程碑 A：攻击与 set authority

- 定义不可变 plan/candidate/threat values；
- 迁移 set target、attacker、power legality 与 final route；
- 建立统一 fallback pool 和 soft-action envelopes；
- 保留 legacy defense 作为未切换下游；
- 完成纯 EditMode 与正式 poor-set PlayMode。

### 里程碑 B：threat 与 joint defense

- 发布经过隐藏字段过滤的 threat distribution；
- 组合 block 与 floor coverage；
- 提交 defense 后只运行一次 final attack choice；
- 迁移 block、floor defense 与 attack cover writer；
- 验证无 final-route clairvoyance 和 committed motion 稳定。

### 里程碑 C：tool recovery 与 reorganization

- 添加 tool collision/rebound/teammate/remaining-touch/exit validity；
- 迁移 post-block、dig 与 incidental-contact coverage；
- 从 actual continuation state 重组；
- 迁移 event-owned Replay evidence。

三个里程碑都完成后，一次性删除或隔离正式 legacy tactical writers。不得以
feature flag 长期同时运行新旧正式 writer。

## 13. TDD 与验收矩阵

每个任务执行 RED → minimal GREEN → focused regression → commit。

### 13.1 EditMode

- A 级 set 产生可靠 power candidates；
- poor set 在评分前淘汰多数不合法过网的 power route；
- tip/roll/push/high-survival/block-out/tool recovery 在同一 pool；
- tool recovery 无非攻击者 continuation、无剩余触球或无组织出口时淘汰；
- threat distribution 不含 final route 或 future sample；
- joint defense 覆盖 block 的残余 threat，不重复堆叠；
- attack/soft/block/defense/set 能力轴固定 key 单调且互相独立；
- stale/duplicate/undeclared/canceled-committed batch 在 mutation 前失败；
- same envelope/trajectory identity 从 plan 到 executor 到 receipt；
- Replay canonical round trip、hash、旧 V4 compatibility 与 strict mismatch。

### 13.2 PlayMode

- A-set 执行可靠 power route；
- poor set 选择合法 survival action，而不是 forced net fault；
- line/cross threat 形成互补 block-floor coverage；
- defense 不因隐藏 final route 二次站位；
- 成功 tool recovery 触 block、回本方、由非攻击者救起并重组；
- 普通 block rebound 到任一侧产生正确新 touch sequence；
- incidental contact 产生明确 coverage/replan；
- revision 不取消 committed jump/block/contact，且不 teleport；
- Replay on/off authority fingerprints 相同；
- 两次固定 seed Replay canonical bytes/hash 相同；
- 3v3 legacy 全套保持通过。

### 13.3 最终验证

- 从最终实现 HEAD 运行完整 EditMode；
- 从同一 HEAD 运行完整 PlayMode；
- 运行 Gate I fixed-seed determinism pair；
- 扫描正式 legacy attack/defense/reorganization writers；
- 扫描 Domain/AI 对 Presentation、Unity 与 recorder 的反向依赖；
- `git diff --check`；
- 对完整 Gate I diff 做 authority、lifecycle、hidden-information、Replay 与
  3v3/Gate J/K scope 复审。

人工视觉、键盘/控制器、Windows x64 与性能 calibration 不作为 Gate I 自动完成
证据；若未执行，change record 必须明确记录。

## 14. 成功标准

Gate I 完成必须同时满足：

1. 正式 6v6 set target、attack、joint defense、tool recovery 与直接重组只有一个
   writer；Gate H 是 Set contact actor/timing writer，Gate I 是其 `SetIntent`
   target/envelope writer；
2. threat → defense → final choice 每个机会只运行一次；
3. defense 不读取隐藏 final route 或 future sample；
4. power route 先过 error-aware legality，fallback candidates 在同一 pool；
5. tool recovery 必须包含合法 home continuation 与 reorganization exit；
6. Attack/Block/Defense/SoftTouch/Set 能力有唯一、可测试的消费路径；
7. actual contacts 仍由 V3 rules、score 与 lifecycle authority 结算；
8. committed actions 不被取消或瞬移；
9. event-owned Replay evidence 完整、canonical、确定且向后兼容；
10. 3v3 保持 legacy，Gate J perception 与 Gate K slimming 不越界；
11. 完整 EditMode、PlayMode、determinism、静态扫描与复审无 unresolved blocker。
