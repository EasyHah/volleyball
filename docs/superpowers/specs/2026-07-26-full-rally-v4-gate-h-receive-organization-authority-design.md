# Full Rally V4 Gate H：接发与组织权威设计

**日期：** 2026-07-26

**状态：** 已确认设计，待用户复核后制定实施计划

**范围：** 正式 6v6 的接发、二传预备、应急代传、覆盖判断、有限重规划与攻击预备权威

**关联：**

- `2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`
- `2026-07-25-full-rally-v4-gate-f-shadow-plan-design.md`
- `2026-07-26-full-rally-v4-gate-g-player-component-boundaries-design.md`
- `../plans/2026-07-24-full-rally-v3-phase-2-9-roadmap.md`

## 1. 目标

Gate H 是 Gate F 责任计划第一次成为正式 gameplay authority 的纵向切片。正式 6v6
的 receive、setter preparation、emergency takeover、organization、
post-contact coverage/replan 和 attack preparation 由一个新的计划
coordinator/executor 负责；`PhysicalMatchRallyDirector` 不再为这些职责选择 actor、
排列 fallback 或直接编写对应 player command。

Gate H 使用 Gate G 的 `PrototypePlayerAgent` facade 作为唯一 player command 边界，
复用 V4 `ExecutionEnvelopeV4`、共享 trajectory artifact、V3 rules authority、
`SetterOrganizationZone` 和既有注册二传可达性规则。正式生产路径一次性切换，不保留
可在 legacy/new writer 之间切换的运行时 feature flag。

## 2. 范围边界

### 2.1 包含

- 为两队生成包含 receive、set、emergency receive、coverage 和 attack preparation
  的条件责任计划。
- 当前控球队计划产生 immutable player execution commands。
- 注册二传合法性、前一触排除、可达性和 fallback 选择。
- accepted contact 后的 coverage evaluation、declared branch 激活和有限重规划。
- 正式 Receive/Set 命令与 V4 envelope/sample/classification identity 的一一绑定。
- Replay V4 的 canonical organization-authority evidence。
- 删除 director 中对应的正式 6v6 legacy tactical writer。

### 2.2 不包含

- 不迁移 3v3；其现有兼容路径保持不变。
- 不迁移 Set accepted 后的攻击选择、攻击路线和攻击完成；Gate I 接管。
- 不加入 attack-defense counterplay、soft action、tool recovery 或重组；Gate I 接管。
- 不加入 perception uncertainty 或 `CourtAwareness`；Gate J 接管。
- 不让 coordinator 改写规则转换、计分、ball integration 或 replay dispatch。
- 不创建第二套 organization zone、setter reachability 公式或 V4 execution sampling。
- 不保留两个同时生效的 Receive/Organize writer。

## 3. 方案选择

### 3.1 采用：独立纵向 authority coordinator

Gate F 的计划域保持 pure、immutable 和 command-free。新增 coordinator 组合计划、
候选/几何计算和执行命令转换，并将命令提交到 Gate G facade。这样可以让 plan 成为
权威输入，同时保持 domain plan 不依赖 Unity、director、player agent 或 scheduler。

### 3.2 拒绝：让 Gate F composer 直接生成 player commands

该方案代码较少，但会破坏 Gate F 已冻结的 command-free 边界，使 replay/domain value
依赖 presentation command surface，并让纯组合测试无法隔离。

### 3.3 拒绝：保留 director 决策并只附加 plan revision

该方案不能迁移战术权威，也不能删除 legacy writer。plan 会继续只是事后标签，与
Gate H 目标冲突。

## 4. 架构

```text
PhysicalMatchRallyDirector
  物理事件、V3 rules、生命周期、得分、result、replay dispatch
                    |
                    | immutable world/rules/trajectory facts
                    v
ReceiveOrganizationPlanCoordinator
  plan revision、coverage/replan、command 原子提交、stale callback 拒绝
        |                  |                    |
        v                  v                    v
DeterministicRally   TeamRallyDecision    SetterOrganizationZone
PlanComposerV3       Planner/helper       单一坐标与分级
        |
        v immutable commands with V4 evidence
PrototypePlayerAgent facade
        |
        v
Gate G locomotion/timeline/contact/technique/presentation components
```

`PhysicalMatchRallyDirector` 只负责建立权威输入、把物理或 accepted-rule event 交给
coordinator、执行 coordinator 返回的生命周期结果，以及继续处理 Gate I 尚未迁移的
攻击下游。它不拥有 Gate H actor selection、fallback order、responsibility state 或
replan policy。

## 5. 计划域

### 5.1 责任表达

现有 `RallyPlanV3`、`TeamRallyPlanV3` 和
`PlayerResponsibilityAssignmentV3` 扩展为能明确表达：

- primary receive；
- 最多两个 conditional emergency receive；
- registered setter preparation；
- conditional backup organizer；
- post-receive coverage；
- attack preparation；
- 每项责任的 condition、declared branch、spatial claim、rank 和 evidence。

计划仍为十二人、每队六名当前场上球员的不可变组合。off-court、libero、连续触球、
轮转和 V3 action eligibility 在评分前过滤；非法候选不能由高分救回。稳定 ID、task、
claim 和 rank 构成完整稳定排序。

计划 assignment 不包含 player agent、delegate、coroutine、Unity object 或可变 command。
执行所需的实际 contact time、root target、envelope 和 sample 由 coordinator 在当前
权威事实上解析。

### 5.2 计划触发

在 rally 开始、accepted contact、声明的责任失效或 ball envelope 超界时，director
创建一次不可变 `RallyWorldSnapshotV3`。snapshot 包含：

- 十二名场上球员的身份、资格、实际 root、forward 和 commitment；
- 当前球状态与 simulation time；
- V3 touch/rules state 和最新 accepted event；
- court configuration；
- 当前共享 V4 trajectory artifact identity；
- revision/source sequence。

两队计划从同一 snapshot 和同一 shared artifact 创建。只有当前控球队计划可以生成
Gate H player commands；对手计划仍只用于责任解释，不能读取当前控球队隐藏 sample
或未来路线。

## 6. Coordinator 与 authority state

新增 `ReceiveOrganizationPlanCoordinator`，其依赖仅为纯计划/决策服务和显式 command
sink。它不引用 replay recorder、rules runtime、ball integrator 或 score state。

`ReceiveOrganizationAuthorityState` 持有：

- 当前 plan/source revision；
- 当前 authority phase；
- 活动 declared branch；
- 已提交 command identities；
- contact deadline；
- previous counted actor；
- latest coverage decision；
- invalidation/replan reason；
- commitment state。

状态转换为：

```text
PlanReceive
→ ReceiveCommitted
→ ReceiveAccepted
→ EvaluateCoverage
→ PlanOrganization
→ OrganizationCommitted
→ SetAccepted
→ HandOffToGateILegacyAttackSeam
```

Receive miss、负责球员改变或 ball envelope 超界只能进入声明的 emergency branch、
local revision、bounded scoped replan 或 terminal。Gate H 不执行 global tactical
rebuild。

旧 revision callback、重复 accepted event 或与当前 phase 不相容的 command 立即拒绝。
新 revision 只能取消尚未 committed 的责任；已起跳、已进入接触窗口或已被 Gate G
标记 committed 的动作不能瞬时取消、重定向或传送。

## 7. 接发权威流程

1. coordinator 从当前计划解析 primary receiver 和最多两个 emergency receiver。
2. primary receive 使用共享 trajectory artifact、实际 contact target 和 V4-derived
   Receive 属性生成唯一 envelope/sample/classification。
3. coordinator 验证 identity 后，原子提交 primary contact、movement、contact window、
   emergency branches、setter preparation 和 attack preparation commands。
4. primary 未触球但球进入已声明 emergency window 时，只能激活计划内的 emergency
   branch；禁止重新遍历全部球员临时选人。
5. accepted receive 后，以实际 first-pass landing、实际 actor、V3 transition 和
   execution classification 评估 coverage，再进入 organization revision。

Receive execution 继续消费 V4 `Receive.FirstTouchControl` 和 `Receive.Movement`。
Reaction delay 和 movement reachability 使用当前 V4-derived runtime wrapper 的既有
正式语义；不得直接读取 Career base attribute。

## 8. 组织权威流程

1. organization target 始终来自
   `SetterOrganizationZone.DefaultWorldTarget(team)`。
2. zone grade 始终由 `SetterOrganizationZone.AssessWorldTarget` 计算。
3. 注册二传使用实际 root、同一 movement speed、Reaction delay、first-pass arrival
   和 contact window 评估 legality/reachability。
4. 注册二传合法、不是 previous counted actor 且可达时，直接成为 organizer；普通
   candidate score 不能把它替换。
5. 只有注册二传 unavailable、illegal、previous touch 或 unreachable 时，才从计划
   声明的 legal backup branch 按稳定顺序选择。
6. 无 legal backup 时返回 `NoLegalOrganizer`，不制造虚假 Set contact，并保留现有
   contact-deadline/save/loss 行为。
7. organizer 的 Set command 使用现有 V4 Set envelope 和 Gate G technique executor。
8. Set accepted 后，将实际 set actor、contact、quality、trajectory 和 V3 transition
   交给 Gate I 前的临时 legacy attack seam；该 seam 不能反向选择 Gate H organizer。

二传预备和 attack preparation 都是 plan-owned command。Gate H 完成后 director 不得
直接调用对应 `ScheduleSetPreparation`、`ScheduleAttackPreparation` 或 emergency
receive writer。

## 9. Command 与原子提交

Coordinator 输出内部不可变命令：

- `ReceiveExecutionCommand`；
- `EmergencyReceiveCommand`；
- `SetterPreparationCommand`；
- `OrganizationExecutionCommand`；
- `AttackPreparationCommand`；
- `AuthorityCancellationCommand`。

每个执行命令包含 plan/source revision、actor、phase、target/time/window、trajectory
artifact identity，以及适用时的 exact `ExecutionEnvelopeV4`、
`ExecutionSampleV4` 和 `ExecutionSampleClassificationV4`。

命令先完成全部参数、资格和 identity 验证，再由 command sink 原子提交。任何验证失败
都不能留下部分 movement、timeline、emergency window 或 contact state。Gate G
`PlayerTechniqueExecutor` 必须保存同一 envelope/sample/classification 实例或其规范
identity，不重新 classify、sample 或读取基础属性。

## 10. Replay V4

Gate F 的 `shadow` record 保留其历史诊断语义，不改名、不伪装成 authority command。
Gate H 在 `MatchReplayEventV4` 增加可选 canonical
organization-authority record，包含：

- plan/source revision 和 authority phase；
- organization target、actual first-pass landing 和 zone grade；
- registered setter identity；
- setter legality、previous-touch status、reach margin、movement distance 和
  reaction delay；
- selected organizer 和 fallback reason；
- activated declared branch；
- command envelope/sample/classification identities；
- coverage decision、reason、invalidation set 和 bounded replan depth；
- exact Receive/Set derived ability consumption evidence。

旧 V4 replay 没有该 record 时继续按既有 strict reader 读取。Gate H 之后的新正式
Receive/Set event 必须带与其 action、actor、plan revision、envelope 和 trajectory
一致的 record；缺失或不一致使 capture 无效，但不能反向改变 live rally。

Canonical JSON 使用固定字段顺序、InvariantCulture、现有 V4 float 规范和 replay hash
family。相同 context、seed、configuration 和输入必须产生相同字节及 hash。

## 11. 失败语义

- malformed snapshot、重复/非场上球员、未知枚举、非有限值：参数异常，零提交。
- stale revision、phase mismatch、重复 event：状态异常，零提交。
- envelope/sample/classification 或 trajectory identity 不一致：状态异常，零提交。
- setter unreachable：正常计划结果，必须记录明确 fallback reason。
- no legal organizer：正常 terminal organization result，保留现有 save/loss。
- replay mapping/serialization 失败：只使 capture invalid，不影响 rally。
- player command sink 在提交前失败：整个 command batch 不生效。
- player command sink 在已 committed 动作后收到失效：保持 committed 动作并记录
  bounded coverage/replan 结果。

## 12. TDD 与验证

### 12.1 TDD 顺序

1. 先写 registered-setter reachability、previous-touch 和 fallback 的纯失败测试。
2. 扩展责任计划的 receive/set/emergency/preparation 条件分支并验证稳定组合。
3. 新增 coordinator state machine、stale revision、bounded replan 和原子提交测试。
4. 接入 Gate G facade，验证同一 V4 envelope/sample/classification identity。
5. 将正式 6v6 Receive/Organize writer 从 director 切换并删除旧写入逻辑。
6. 新增 Replay V4 authority schema、strict JSON/hash 和旧 V4 无该 record 的读取测试。
7. 新增 fixed-seed formal PlayMode 场景。

每个行为严格执行 RED、确认预期失败、最小 GREEN、回归、必要重构和提交。不得先写
production code 再补测试。

### 12.2 EditMode

- Home/Away organization target 与所有 zone band 镜像正确。
- reachable registered setter 始终优先于更高分 backup。
- previous-touch、illegal、unavailable 和 unreachable setter 激活正确 backup。
- no backup 返回 `NoLegalOrganizer`。
- receive/organization assignment、branch 和 tie order 确定。
- stale revision、重复 callback 和 phase mismatch 不产生命令。
- command batch 验证失败不产生部分 player state。
- coordinator、facade 和 technique executor 使用相同 envelope/sample identity。
- Receive/Set ability 单变量固定种子证据保持有界、单调且只影响声明路径。
- Replay V4 新旧 strict round-trip、canonical order、hash 和 identity validation。
- reflection/source boundary 证明 director 不再拥有 Gate H tactical writer。

### 12.3 PlayMode

- normal in-zone receive 到 registered setter。
- displaced-but-reachable setter 仍获组织权。
- setter previous touch 触发 emergency organizer。
- unreachable setter 触发 legal backup。
- no legal backup 沿用现有 save/loss。
- emergency receiver 只能来自 declared branch。
- attack preparation 由计划触发，但 Set 后 attack completion 仍经 Gate I legacy seam。
- V3 transitions、accepted contacts、touch counts 和 score 保持唯一权威。
- recorder on/off 不改变 authority fingerprints。
- 两次独立 fixed-seed run 的 Replay V4 bytes/hash 一致。
- legacy 3v3 回归保持现有公开 bootstrap 和行为边界。

### 12.4 验证深度

Gate H 跨越 live tactical authority、lifecycle ordering、Replay V4 canonical
serialization 和三个以上模块，采用升级验证：

- 每个 TDD cycle 的 focused EditMode；
- Gate H 相关正式 6v6 PlayMode；
- 完整 EditMode 与 PlayMode；
- fixed-seed recorder on/off 和 independent replay repetition；
- canonical/hash backward-read tests；
- 一次独立 requirements/correctness/test-gap review；
- review finding 的 focused regression 与定向复审；
- `git diff --check` 和静态 boundary scans。

## 13. 完成定义

Gate H 仅在以下条件全部满足时完成：

1. 正式 6v6 Receive、setter preparation、emergency takeover、organization、
   coverage/replan 和 attack preparation 只有 coordinator 一个 writer。
2. Director 不再为这些责任选择 actor、排序 fallback 或直接调度对应 player API。
3. `SetterOrganizationZone` 是唯一 organization 坐标和 grade 定义。
4. 注册二传优先与 fallback 使用唯一 legality/reachability 规则。
5. Receive/Set planner、coordinator 和 executor 的 V4 evidence identity 一致。
6. 无合法 organizer 不产生假触球或规则例外。
7. V3 rules transition、accepted contact、touch count 和 score 保持唯一权威。
8. Replay V4 能解释 plan → receive → actual landing → setter reachability →
   organizer → envelope/sample → coverage/result。
9. 相同 fixed seed 的正式 replay 字节和 hash 稳定。
10. 3v3、Gate I attack authority、Gate J perception 和 Gate K director/replay UI
    范围未被提前迁移。
