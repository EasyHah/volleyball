# Full Rally V4 Gate G：Player 组件边界设计

> 状态：已确认设计，待用户复核后制定实施计划
>
> 关联：[V4 consolidated design and roadmap](2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md)、[Gate F shadow plan design](2026-07-25-full-rally-v4-gate-f-shadow-plan-design.md)、[V3 Phase 2--9 roadmap](../plans/2026-07-24-full-rally-v3-phase-2-9-roadmap.md)

## 1. 目标与范围

Gate G 将 `PrototypePlayerAgent`（当前约 1,800 行）拆为职责单一的运行时组件，同时保留它作为唯一兼容 facade。现有 director、bootstrap、replay、测试和场景仍只通过 facade 调度球员；facade 将调用转发给组件，不再拥有运动、动作时间线、接触几何、技术执行或 rig pose 的业务状态。

本 gate 的目标是建立可被 Gate H/I 的 plan executor 直接消费的稳定执行边界，而不是改写接发、组织、进攻、拦防或计分权威。正式 6v6 与 legacy 3v3 的公开 bootstrap API、已接受触球顺序、V3 rules transition、比分和 Replay V4 语义必须保持不变。

### 非目标

- 不接管 director 的战术选择、责任计划执行或 replan policy。
- 不增加 V4 body model、感知模型或新的动画资产。
- 不放宽接发、二传或 controlled handling 的现有对齐策略。
- 不删除 facade 的公开 API；Gate H/I 迁移消费者后才评估移除兼容入口。

## 2. 目标结构

```text
Formal/3v3 Director, Bootstrap, Replay, Tests
                    |
                    v
       PrototypePlayerAgent (兼容 facade)
  身份/注册、参数验证、跨组件路由、只读观测
     |          |           |           |           |
     v          v           v           v           v
Locomotion   Action       Contact     Technique   Presentation
根运动       Timeline     Surface     Executor    Rig/pose
```

facade 只保存 `PlayerId`、`StablePlayerId`、能力配置、court 配置、组件引用及对外只读投影。它不得直接写 `transform.position`、计算 action phase、构造 contact frame、产生 ball response 或设置 rig pose。每个组件只接受相邻组件公开的不可变 command/state；组件之间不持有 director、planner、replay recorder 或规则 runtime 的引用。

## 3. 组件职责和接口

### 3.1 PlayerLocomotion

负责所有 root motion：普通 scheduled movement、support/block movement、attack approach、takeoff、contact root、landing、court clamp 和 movement shortfall。输入为已验证的 movement/attack command 和 simulation time；输出为当前位置、forward、速度受限的 root-motion sample、takeoff 观测和已应用的攻击对齐修正。

攻击 approach/takeoff/contact/landing 必须是一条连续、速度受限的序列。相邻物理样本位移不得超过该样本时间和允许速度所给的上界。为接触应用的 root correction 仅限攻击，且累计/最大值始终不超过 `0.18m`；不能闭合的距离保留为 movement shortfall 和 execution/contact deviation，禁止 teleport。

### 3.2 PlayerActionTimeline

负责 `ActionTimeline`、scheduled contact/support/block/emergency window 生命周期与 action phase sampling。它不移动 root、不设置 pose、不构造接触面、不决定动作，也不写球。输出是当前 action、phase、phase weight、scheduled/actual contact time 和窗口状态。

### 3.3 PlayerContactSurfaceProvider

负责基于当前或预览 root/pose 生成 `ContactSurfaceFrame`、block arm capsule 和 surface response 参数，并在 contact window 内提交 `BallContactCandidate`。它不改 root、不改变 timeline、不会自行选择动作。其输入是 technique executor 已确认的 execution state、timeline sample、locomotion sample 和 presentation pose；预览路径使用临时 rig 状态后必须恢复，不得泄漏状态。

### 3.4 PlayerTechniqueExecutor

负责将 facade 收到的已选择 `TechniqueAction` 与 V4 执行证据转为 immutable execution state：set style/hand、target velocity、contact group、planned center、trajectory artifact、attack approach/contact plan、受控 handling 和 execution error。它是唯一持有 `ScheduledExecutionEnvelopeV4`、`ExecutionSampleV4` 与 `ExecutionSampleClassificationV4` 的组件。

技术执行器只接受 `ExecutionEnvelopeV4` 及其 `ExecutionSampleClassificationV4`/`ExecutionSampleV4`，并验证 envelope 与 sample/classification 的同一性。它不得读取 `PlayerAbilityProfile`、基础属性、director、planner、rules runtime 或 trajectory provider，也不得重新 classify、预测或生成新 sample。缺失、不匹配或无效 V4 证据必须在 facade 调度时失败，不能回退到 legacy ability 或默认 envelope。

### 3.5 PlayerPresentation

负责 `StickFigureRig` 创建、pose 映射、颜色/号码和按 timeline/locomotion sample 应用可视状态。它只消费 action phase、set style、attack/block 状态与根位置；不能更改 timeline、运动、接触或任何球响应。`Rig` 仍由 facade 以只读方式暴露，保留既有测试和显示消费者。

## 4. 调度与数据流

1. Director 或 bootstrap 仍调用 facade 的 `ScheduleContact`、support/block、prepare、cancel 和 preview API。
2. Facade 验证稳定身份、参数和 V4 执行证据，构建 command，并顺序配置 technique executor、timeline 和 locomotion；不解释战术意图。
3. 每个 simulation step，facade 读取 timeline sample；locomotion 推进唯一 root-motion state；presentation 应用同一 sample 的 pose；contact provider 使用最终 root/pose 生成候选。
4. `CollectContacts` 只合并 contact provider 的候选并保留既有 group、response、surface center/normal 和 Replay V4 观测语义。
5. 取消、重定向和异常路径必须原子清理相应组件 state，不能留下能在后续帧产生幽灵接触的窗口。

动作、运动与接触的单向关系为：`TechniqueExecutor -> ActionTimeline -> Locomotion/Presentation -> ContactSurfaceProvider`。facade 可以读取投影用于兼容属性，但不存在从 contact/presentation 反写到技术或战术的通道。

## 5. 兼容性、错误与可观测性

- `PrototypePlayerAgent` 的公共方法、属性、事件、`IBallContactSource` 实现和序列化 `_moveSpeed` 保持兼容；新组件由 facade 在初始化时创建/注入，不要求场景手工挂载组件。
- 既有只读属性（例如 scheduled envelope/sample/classification、movement shortfall、surface center/normal、takeoff、block metrics）转发组件投影，保持相同的 null/reset 时机。
- `SupportActionActivated` 的触发次序与同一帧的 contact 收集语义不变。
- 组件构造或 command 验证失败必须抛出可定位的 `ArgumentException`/`InvalidOperationException`，并且不部分改变其他组件状态。
- 新的内部 diagnostics 至少包含 action、source envelope identity、contact group、movement shortfall、attack correction 和实际 contact deviation；它们仅供测试/replay 投影，不构成新的 live authority。

## 6. 测试与验收

### EditMode

- facade contract：初始化、能力/court 配置、schedule/cancel、support/block/emergency、preview 和公开投影与 Gate G 前的代表性行为一致。
- component isolation：每个组件只引用允许的运行时类型；technique executor 拒绝无 V4 envelope、mismatched sample/classification 和任何 legacy ability 输入路径。
- locomotion：attack sample 连续且速度有界；takeoff、planned contact、landing 顺序正确；最大攻击对齐修正 `<= 0.18m`，未闭合误差可观测。
- action/contact：timeline phase、surface frame、block capsule、response、contact group 和取消后的零幽灵接触与基线一致。
- presentation：pose 映射和 preview 恢复不改变 live root/rig state。

### PlayMode 与 Replay

- 正式 6v6 和 legacy 3v3 用原有 bootstrap API 完成 rally；比分、accepted-contact 序列、V3 transitions 和固定种子 Replay V4 JSON/hash 保持不变。
- 正式攻击相邻物理样本满足 root-motion 位移上界，Replay/diagnostic 记录 approach start、takeoff、planned contact、actual deviation 与 correction，并验证 correction 上限。
- Gate F replay shadow 仍按已接受 contact 的 source sequence 捕获；附加的 player components 不得改变 shadow plan、artifact identity 或 capture integrity。

## 7. 分阶段实施和回滚

按依赖从低到高迁移：先抽出 action timeline 和 presentation，再抽 contact provider，再抽 locomotion，最后抽 V4-only technique executor 和 facade 路由。每一步先添加独立测试、迁移一组 facade 方法、执行窄回归；禁止在同一提交里改策略逻辑。

实施完成前，facade 内保留委托边界而非复制的 legacy 实现。若出现回归，回滚最近一个组件迁移提交即可恢复原 facade 行为；不回滚 Gate A--F 的 V4 contracts、规则或 Replay V4。
