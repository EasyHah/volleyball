# V4 防守接触、拦网与发球触网延续设计

## 目标

修复正式比赛中普通扣球、拦网反弹和发球触网后的防守接触空窗。`FloorDefense`、`AttackCover`、`BlockRecovery` 和 `ServeReceive` 必须只通过球体与合法球员接触面的真实 swept geometry 产生 `Receive`；不能由预测、计划或测试代码直接制造成功触球。

同时提供完整固化、运行时可加载的正式比赛情景预设，用于重放和验证这些延续路径。预设是正式比赛的开局输入，而不是中途事件脚本。

## 范围与非目标

本次包含：

- 完整固化的运行时情景预设和正式启动适配层。
- Gate I 已提交防守职责到实际 Receive 接触面的接线。
- 扣球 crossing、拦网反弹侧和落地之间的单调 continuation 状态机。
- 发球触网后在同一接发 possession 内按实际反弹轨迹重规划接发移动与接触窗口。
- 相同 fixed step 内物理接触候选的稳定、显式排序。
- 防守尝试、continuation 结果和真实接触的 replay 证据。
- 按完整比赛生命周期运行的固定输入 PlayMode 验证。

本次不包含：

- 玩家可见的情景预设选择 UI。
- 角色胶囊碰撞体作为触球面。
- 预测落点、计划结果或测试代码直接修改球速、触球数、球权、比分或规则状态。
- 用独立测试场景复制 `FormalIndoor6v6` 的比赛接线。
- 与本目标无关的 `PhysicalMatchRallyDirector` 重构。

## 术语与职责

| 名称 | 职责 | 明确不负责 |
| --- | --- | --- |
| Gate H / Gate I | 选择并提交职责、移动和合法接触窗口 | 制造球接触或修改规则账本 |
| `PrototypePlayerAgent` | 在已提交窗口内提供 Receive 接触面 | 根据位置“自动成功” |
| `SimulatedBall` | fixed-step swept geometry、候选选择、产生一个物理接触 | 决定比赛流程或落地得分 |
| Full Rally V3 | 判定已发生物理接触是否合法并更新 counted-touch ledger | 几何、crossing、窗口、移动、ground 判定 |
| `PhysicalMatchRallyDirector` | crossing、block rebound side、窗口生命周期、清理与证据关联 | 覆盖裁判判罚 |
| `MatchRallyReferee` | 唯一 ground/out-of-bounds/result 权威 | 被防守计划替代 |

正式因果链固定为：

```text
情景预设输入 -> 正常开局和发球 -> Gate H/Gate I 提交职责
-> 球员移动和 Receive surface 窗口 -> SimulatedBall 实际相交并选定赢家
-> V3 提交该物理接触 -> Director 推进延续/清理 -> Referee 判定落地或出界
```

Gate I 的 receipt 只能证明尝试已被授权，不能证明球已被接起。

Gate H 的接发 receipt 同样只能证明接发责任和窗口已提交。发球触网后，触网前的轨迹与到达时间已经失效；旧 receipt 不得通过扩大窗口或放宽几何条件继续生效。重规划必须产生新的 revision、source sequence 和轨迹证据，并使旧窗口过期。

## 情景预设

### 合同

新增一个运行时可加载的 `FormalMatchScenarioPresetV4` `ScriptableObject`，并为它提供纯的启动适配层。预设完整固化以下内容：

- 预设标识、格式版本、规则版本与内容哈希。
- 确定性随机种子。
- 双方完整 `TeamSnapshotV4`，包括阵容顺序、位置、球员能力快照。
- 发球方、初始轮次/站位，以及开局发球的飞行时间、到达速度和相对接发人深度偏移。
- 场地和物理配置版本。
- 双方的战术与 AI 输入。

预设不得包含球在比赛中途的位置、速度、轨迹、指定触球者、强制 contact window、规则提交、比分或预期赛果。
开局发球深度偏移只决定 ArrivalLaunchSolver 的冻结目标，不直接移动球、不指定接球人，也不保证触球成功。

适配层将预设转换为现有 `MatchContextV4` 和正式启动参数，再通过现有 `FormalIndoor6v6` 生命周期开始比赛。默认场景仍可走既有默认输入；预设加载是独立 API，不应把测试特判混入 `FormalSixVsSixRallyBootstrap`。

预设 ID、格式/规则版本和内容哈希写入 replay 的开头，保证诊断产物能说明其开局来源。

### 完整性和兼容性

加载前验证版本、必填字段、球队侧别、6 人阵容、唯一稳定 player ID、合法轮次/站位和配置哈希。无效预设在开局前明确失败，不能退回到默认数据继续比赛。

预设是完整快照，不是对默认 `FormalIndoor6v6` 的覆盖。未来默认球员或能力发生变化时，已保存预设的输入不得悄然改变。

## 防守接触

普通 `FloorDefense` 在 Gate I 提交后必须同时拥有移动计划和 `Receive` 接触窗口，不能只走 `ScheduleSupportAction`。`AttackCover` 和 `BlockRecovery` 使用相同原则：已有职责只能打开相应球员的合法 Receive surface，不能直接修改球。

`PhysicalContactCenter` 仅帮助安排球员/平台的目标位置，绝不是命中条件。真实命中只能由 `SimulatedBall` 对球和 `PlayerContactSurfaces` 的 swept geometry 得出。未到窗口、窗口过期、移动不可达或没有实际相交时，不产生 `Receive`。

发生一个物理接触后，V3 对该接触判定：

- `BlockContact` 可被接受，但不增加任一方 counted touches。
- 拦网后该方首个合法 `Receive` 的 counted touch 是 1。
- 同一拦网者随后完成首个 counted touch 合法。
- 同一 physical contact group 绝不能重复提交。

## 发球触网延续

发球与网发生真实环境接触时，Net contact 本身是非终局事件。Director 必须在
`EnvironmentCollision.ApplyResponse` 已经写入实际反弹位置和速度后处理延续，并根据实际状态执行：

1. 保持原接发方和当前 V3 发球后的规则账本，不新建 possession，不增加触球数。
2. 立即使触网前的 Gate H 接发窗口和异步决策回调失效。
3. 使用触网后的实际球位置、实际速度和重新计算的剩余飞行时间，创建同一接发方的新 Gate H planning revision/source sequence。
4. 新计划只能移动已由新 receipt 授权的接发球员并打开其合法 `Receive` surface。
5. 若实际反弹轨迹无法提供正数、有限且足够调度的剩余时间，则不得制造接发窗口；球继续由 ground、out-of-bounds 和 net-crossing 权威裁决。

触网后可能出现三种结果：

```text
Serve net contact
  -> crosses legally into receiving court
     -> replanned ServeReceive may create one actual Receive
     -> miss remains a normal ground point
  -> rebounds to serving court
     -> no receiving-side Receive may be opened
     -> ground/out-of-bounds resolves the serve
  -> crosses outside the legal antenna/height boundary
     -> net-crossing referee resolves the fault
```

重规划不能调用 `BeginPossession`，因为发球开始时接发 possession 已经建立；重复调用会清理或重建不属于新球权的状态。重规划也不能让普通角色 capsule 成为触球面，不能直接改变球速，不能因为球员站在新落点附近而自动接受触球。

为避免对即将弹回发球方的球提前开放错误窗口，只有触网后的实际速度方向仍指向接发方时才允许重规划。新 Gate H 计划可以提交移动，但其物理 Receive window 必须等后续合法 net crossing 才激活。触网后实际速度指向发球队时，不创建接发方新计划或物理候选。

## Continuation 状态机

Director 维护 presentation 内部、单调推进的 continuation state，并记录触发 contact group、block actor/team、block simulation time、impact center、接触后速度、最近 crossing、已决 side、Gate I revision/source sequence 和已开窗口 identity。它不进入 V3 domain 账本。

```text
Attack accepted
  -> AwaitingAttackCrossing
  -> DefendingSideFloorDefense (实际 crossing 已确认)

Block accepted
  -> AwaitingBlockOutcome / PendingSideResolution
  -> AttackingSideCoverage (实际回到进攻方)
  -> BlockingSideRecovery (实际留在拦网方)

任意未决状态
  -> ResolvedByContact
  -> ResolvedByGround
  -> rally reset

Serve accepted
  -> ReceivingPossessionPlanned
  -> NetDeflectionObserved
  -> ServeReceiveReplanned (仍朝接发方)
  -> ResolvedByContact / ResolvedByGround / ResolvedByCrossingFault
```

反弹侧的固定优先级是：

1. 同一 fixed step 内、早于下一物理事件的实际 net crossing；
2. block 接触点相对网面的位置；
3. block 后实际 `Velocity.Z` 的严格符号；
4. 仍在网面未决走廊则保持 `PendingSideResolution`；
5. 后续 crossing、离开走廊或 ground event 决定结果。

未决时不得同时为两侧开放窗口。若球先落地，直接结束为 `ResolvedByGround` 并交给 `MatchRallyReferee`。接触、落地和 rally reset 都一次性关闭关联窗口。

`AttackCover` 只能来自计划显式声明的进攻保护职责；`BlockRecovery` 只能来自已提交的恢复职责。没有合法 actor 时保持可落地，不能临时从全队创造候选。

## 确定性候选选择

同一 fixed step 中，`SimulatedBall` 对真实几何命中的候选使用精确排序键：

```text
SweptBallHit.TimeFraction
-> contact group id
-> action priority
-> team
-> roster slot
-> role
-> stable surface index
```

该排序不得读取 contact source 注册顺序、GameObject instance ID、哈希集合遍历顺序、渲染 frame count、wall-clock 或 epsilon 合并。完整键相同的候选视为同一物理事件，仅提交一个；其余记录为被确定性赢家取代。未选候选不得改变球速、产生 V3 transition 或改变结果。

## Replay 与诊断

为每个授权的防守尝试建立可关联的 canonical 证据，至少涵盖：

- `DefenseAttemptOpened`
- `DefenseCandidateSampled`
- `DefenseContactAccepted`
- `DefenseContactRejected`
- `DefenseAttemptExpired`
- `PostBlockContinuationResolved`
- `ServeNetDeflectionObserved`
- `ServeReceiveReplanned`

每项绑定预设 ID/哈希、Gate I command/receipt、actor、revision、source sequence、artifact/envelope identity、窗口起止、simulation time、球位置/速度、continuation side/state、赢家或淘汰原因。

发球触网证据必须额外关联触网环境 contact group、触网前后速度、新旧 Gate H revision/source sequence、旧窗口过期原因和新窗口 identity。优先使用现有非 canonical 诊断及既有 replay 事件；本次不为发球触网单独升级 Shared V4 schema。只有现有 canonical 结构无法证明最终 accepted Receive 与新 receipt 的绑定时，才另行评估 Replay 合同升级。

优先复用现有 `ReplayContactEvent`、fixed-rate samples 和 simple events。仅当 canonical replay 无法承载 continuation side、rejected/expired reason 与 receipt 绑定、或 HTML 无法从 canonical payload 重建时间线时，才升级 Shared V4 schema。升级时必须定义默认值、旧 payload 的读取行为、固定 canonical property order、round-trip 与 hash fixtures。HTML 只渲染 canonical 事实，不能反推或补造比赛事实。

## 验收与验证

提供三组完整固化的正式情景预设，并从正常正式开局运行：

| 预设 | 必须证明 |
| --- | --- |
| `ReachableFloorDefense` | 扣球实际 crossing 后，已提交 FloorDefense 经 Receive surface 起球 |
| `AttackSideBlockRebound` | 拦网实际回到进攻方后，已提交 AttackCover 完成新的 Receive |
| `BlockingSideBlockRebound` | 拦网实际留在拦网方后，已提交 BlockRecovery/FloorDefense 完成本方第一个 counted touch |
| `ServeNetDeflection` | 发球真实触网后仍合法进入接发方，旧窗口过期并由新 Gate H receipt 建立实际 Receive |

预设输入还应覆盖不可达/过晚和漏球结果，而不是由测试在中途改球状态。完整 PlayMode 验收包括：

- 可达 `FloorDefense` 成功；过晚或不可达时绝不产生磁吸式接球。
- 两个 block rebound side 都只在相应实际 side 上开放窗口。
- post-block miss 仍触发唯一的 ground/referee result。
- 重叠接触面只产生一个稳定排序的 Receive。
- 成功同时具有 `PlayerBallContactEvent`、V3 accepted transition、replay accepted event 和正确 counted-touch delta。
- miss 具有零 accepted Receive、拒绝或过期证据、ground event 与唯一 rally result。
- 发球触网后合法越网且可达时产生一次真实 Receive；不能复用触网前的窗口。
- 发球触网后弹回发球队时，接发方不会获得 physical candidate。
- 发球触网后合法越网但无人接到时，ground/referee 正常产生唯一得分。
- 发球触网后的重规划不会重复 BeginPossession、规则触球、统计或 replay accepted contact。

EditMode 覆盖预设适配/完整性、V3 block 计数、候选排序不受注册顺序影响和 replay codec/hash 边界。

每套固定预设至少独立运行两次，比较 canonical replay JSON、HTML/report、result、accepted contact ordering、continuation diagnostics 和 canonical hash。代码冻结后运行受影响模块回归，以及完整 EditMode 和 PlayMode。Unity 临时 `Assets/InitTestScene*.unity` 及其 `.meta` 在开发时忽略，并在合并分支前删除。

## 风险与完成条件

该工作改变 Match、确定性和可能的 Shared replay 合同，属于高风险修改。实现按独立阶段验证；若 Shared 或公共契约改动，使用功能分支和 PR，不未经明确授权合并 `main`。

只有当所有正式预设均走完整生命周期、每一项成功/失败证据跨物理/V3/replay/referee 一致、固定输入产物稳定、相关和完整测试完成，并经一次独立复核后，才能称为完成。
