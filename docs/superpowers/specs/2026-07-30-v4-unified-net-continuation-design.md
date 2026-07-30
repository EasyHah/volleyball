# V4 统一触网续球设计

- 状态：已确认设计，待实现
- 范围：Formal 6v6 中发球、普通回球、二传和扣球的合法 Net contact 后续球
- 不改变：`MatchContextV4`、`MatchResultV4`、V3 规则契约、Shared Replay schema 或网体物理响应

## 1. 目标

球触网本身不是自动死球。系统必须根据第一次真实 Net contact 后的实际物理轨迹，安排合法的
防守或救球机会；没有实际球员接触时，仍由落地裁判正常判分。

该设计修复先前只处理发球触网、没有统一处理扣球和其他回球触网的问题。

## 2. 核心模型：一个飞行段只处理一次有效触网

每次发球开始或规则接受的球员触球后，创建一个 flight segment。一个 flight segment 在下一次
规则接受的球员触球、落地/出界、规则终止或 rally reset 时结束。

在每个 flight segment 中：

- 第一次真实 `EnvironmentContactKind.Net` 是唯一的 effective net deflection；
- 它 MUST 取消基于触网前轨迹、尚未实际发生的接球或防守窗口；
- 它 MUST 从触网后的球状态和速度重新判断可处理的一侧；
- 该 flight segment 的后续 Net contact MUST 保留物理响应，但 MUST NOT 再次创建计划、revision 或
  接触窗口；
- 这样不会把网带微小抖动扩大为多个战术状态机转换。

## 3. 续球判定

第一次有效触网后，续球只能由触网后的实际球速、真实过网和剩余规则触球资格决定。计划不得
直接改变球轨迹、球权或裁判结果。

| 来源 | 触网后的实际走向 | 必须行为 |
| --- | --- | --- |
| 发球 | 越过球网 | 以实际轨迹重新组织接发；物理 Receive 只能在真实 crossing 后生效。 |
| 发球 | 留在发球方 | 不创建救球窗口；按发球失误由裁判结束回合。 |
| 普通第 1/2 次计数触球 | 留在本方且仍有触球额度 | 本方可按真实轨迹组织救球；成功必须来自实际几何接触。 |
| 普通第 1/2 次计数触球 | 越过球网 | 对方按真实轨迹组织防守。 |
| 第 3 次计数触球 | 留在本方 | 不创建救球窗口；没有剩余触球，落地后判对方得分。 |
| 第 3 次计数触球 | 越过球网 | 对方按真实轨迹组织防守。 |
| 已接受 Block 后 | 任一走向 | 继续由既有 post-block continuation 处理；通用触网路径不得重复接管。 |

“普通触球”包含二传、调整球、自由球和扣球。扣球通常是第三次触球，但实现 MUST 以 V3
实际 touch budget 判断，不得按动作名称猜测。

## 4. 状态与所有权

`PhysicalMatchRallyDirector` 保持物理事件编排者；V3 仍只记录已经接受的玩家触球。

实现应新增一个内部、每飞行段重置的 net-continuation guard，至少表达：

```text
FlightSegmentIdentity
OriginKind (Serve | AcceptedPlayerContact)
OriginTeam
EffectiveNetDeflectionHandled
```

它 MUST 在 accepted player contact、serve launch、ground/out/terminal cleanup 和 rally reset 的现有
边界被确定性地创建或清除。它 MUST NOT 成为 Shared、Career 或 Replay 的新公共合同。

第一次有效触网的通用处理顺序为：

```text
Net contact
-> 保留 SimulatedBall 的物理回弹
-> 识别当前 flight segment 与来源
-> Block continuation 已拥有该球？是：交回既有路径
-> 取消旧的未激活窗口/过期 scheduling evidence
-> 用后触网轨迹判断留本方、越网或不可达
-> 仅在 V3 touch budget 合法时发布新的 Gate H/Gate I 机会
-> 等待真实 crossing 与球员几何接触
-> 无接触时由 ground/out referee 判分
```

发球的初始物理来源继续只用于落地归因，不伪造 accepted player contact，也不消耗 V3 触球次数。

## 5. 失败与边界

- 不可达球 MUST NOT 因为已规划而自动起球。
- 在触网前已建立、但与新轨迹不一致的 Receive/Block/FloorDefense 窗口 MUST 失效。
- 同一 Net contact MUST NOT 同时触发通用路径和 post-block continuation。
- 天线、界外和地面等终端裁判事实维持现有优先级；本设计不得把合法触网变成规则接受的玩家触球。
- 若轨迹尚未实际 crossing，面向对方的防守机会可以处于 pending，但不得提前接受物理接触。
- 现有 Replay V4 不支持零玩家触球 flight segment 时，不得伪造 accepted Serve 或普通触球来满足
  recorder；该 schema 限制须由确定性物理/裁判测试覆盖，除非另行批准 Shared Replay 升级。

## 6. 验收与测试

新增或扩展 Formal 6v6 PlayMode 场景，至少证明：

1. 发球触网过网后，旧接发计划失效并以实际球重新接发；留本方时按发球失误判分。
2. 第二次触球（含二传）触网留本方时，仍有额度的本方球员可以实际救球；越网时由对方防守。
3. 第三次触球的扣球触网留本方时，不产生非法第四触球；越网时对方可正常防守。
4. 拦网后触网只走 post-block continuation，且 Block 不计入三次触球的规则不变。
5. 不可达情景零 accepted Receive，且 ground/out referee 产生唯一结果。
6. 同一 flight segment 多次 Net physical contact 最多产生一次 continuation replan/window publication。
7. 相同 seed 的 DirectPhysical 运行在同平台保持相同规则结果、accepted-contact 序列和内部
   continuation evidence；canonical Replay 只在现有 schema 能合法表示时比较字节。

EditMode 还必须覆盖 flight-segment guard 的 reset、第三触球剩余额度、Block 路径排他性和
stale window 失效。实现冻结后，运行受影响的 PlayMode、完整 Match PlayMode 和完整 EditMode。

## 7. 非目标

- 不模拟网带连续抖动的多轮战术重规划；
- 不修改网体碰撞弹性、球体积分器或浮点物理模型；
- 不扩展 Shared Replay schema；
- 不改变 Career × Match V4 生命周期任务的公共合同或非目标；
- 不引入多局、换人、自由人或新的得分规则。
