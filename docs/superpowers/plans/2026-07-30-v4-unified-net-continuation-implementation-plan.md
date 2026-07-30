# V4 统一触网续球：限时实施计划

- 前置规格：`docs/superpowers/specs/2026-07-30-v4-unified-net-continuation-design.md`
- 总时限：一个开发日，目标不超过 4 小时工程时间；超过时限时保留可验证的核心修复，禁止扩展范围。
- 开始条件：新的 active handoff 明确授权本计划；从最新 `origin/main` 建立实现分支。

## 范围与完成定义

本次只统一 Formal 6v6 中每个飞行段第一次有效 Net contact 的续球处理。必须覆盖发球、第二触球、
第三触球和 Block 后触网的排他性；每种行为用指定回合情景验证。

完成时必须满足：

- 发球触网既有行为不回归；
- 第二触球触网留本方时，只要仍有触球额度，就能由实际几何接触救球；
- 第三触球触网留本方时，绝不创建第四触球；越网时给对方真实防守机会；
- Block 后触网仍由 post-block continuation 唯一处理；
- 一个飞行段最多发布一次触网续球重算；
- 不可达球仍由 ground/out referee 唯一判分。

本次明确不做：网体物理重写、多轮弹网战术状态、Shared Replay schema 升级、Career/V5 合同、
多局赛制、换人或自由人。

## 阶段 0：基线与入口确认（20 分钟上限）

1. 从最新 `origin/main` 创建分支；确认 worktree 干净。
2. 运行现有 `DefensiveContactContinuationPlayModeTests` 的发球触网三例，保留当前行为基线。
3. 定位并记录以下边界：
   - `PhysicalMatchRallyDirector.HandleEnvironmentContact`；
   - `HandleServeNetContact`；
   - accepted player contact、serve launch、ground/out、rally reset；
   - post-block continuation 入口。

若基线不绿，停止后续实现并先报告现有回归；不得把非本次引入的问题混入修复。

## 阶段 1：抽取单次触网飞行段 guard（60 分钟上限）

1. 在 `PhysicalMatchRallyDirector` 内增加私有、不可跨回合泄漏的 flight-segment guard：
   `origin kind`、origin team、已处理有效触网标记和可选 sequence。
2. 在 serve launch 与每个 accepted player contact 创建新飞行段。
3. 在 accepted player contact、ground/out、terminal cleanup 和 rally reset 清理或替换 guard。
4. 将 `HandleServeNetContact` 抽为通用的首次触网分派入口；保留发球的特殊规则：
   发球留本方是发球失误，不产生本方救球。
5. guard 已处理时仅保留 `SimulatedBall` 物理回弹，不再取消窗口、推进 revision 或发布新机会。

最小 EditMode：guard 重置、第一次触网允许分派、同飞行段第二次触网被抑制。

## 阶段 2：按剩余触球额度分派续球（75 分钟上限）

1. 从 V3 `RallyTouchState` 读取实际 source team 与剩余计数触球额度，不得从动作名称推断。
2. 第一次触网后使旧轨迹相关、未激活的 Receive/FloorDefense 窗口和异步 scheduling evidence 失效。
3. 触网后球速指向对方时：按实际轨迹为对方建立 pending 防守机会；physical window 仍等待真实
   net crossing。
4. 触网后球留在来源方时：
   - 有剩余触球额度：为来源方建立实际轨迹救球机会；
   - 没有额度：不创建窗口，交给 ground/out referee。
5. 已处于 `_awaitingPostBlockCrossing` 或已有 post-block authority 的球，跳过通用分派；只调用
   已有 post-block continuation。

实现 MUST 不直接提交 Receive、不得修改球速度、不得伪造 V3 transition 或 accepted contact。

## 阶段 3：指定回合情景测试（75 分钟上限）

基于现有 `FormalMatchScenarioV4` 和 `DefensiveContactContinuationPlayModeTests`，只新增必要的冻结
场景资源与探针断言：

1. `SecondTouchNetOwnSide`：第二触球擦网留本方，第三触球实际 swept hit 才能续球。
2. `ThirdTouchNetOwnSide`：第三触球擦网留本方，零非法第四触球，唯一 referee result。
3. `ThirdTouchNetCross`：第三触球擦网越网，对方在实际 crossing 后有防守机会。
4. `PostBlockNet`：断言通用触网分派次数为零，post-block continuation 仍唯一。
5. `NetDeflectionSingleDispatch`：同一 flight segment 的多次物理 Net contact 最多一次 replan/window
   publication。

每个情景都必须用真实 `SimulatedBall`、几何接触和裁判结算，禁止在中途手动改球、手动 Commit
或手动写比分。每个关键情景双跑同一 key，比对 result、accepted-contact sequence 与 continuation
evidence；Replay byte 比较只用于现有 schema 可合法记录的情景。

## 阶段 4：限时验证与审查（50 分钟上限）

按顺序执行：

1. 新增/受影响 EditMode；
2. 指定回合 PlayMode fixture；
3. 既有正式 6v6 单局 smoke；
4. 一次完整 EditMode；
5. `git diff --check` 与静态扫描：不得新增 Match/Shared → Career 引用、不得修改 Shared Replay schema。

进行一次合并审查，确认规则、窗口、revision、Block 排他性和测试缺口。若审查发现高优先级问题，
只修复该问题并重跑相关 fixture；不重新启动全量 PlayMode，除非 smoke 或审查表明影响已越出指定
情景边界。

## 时间盒与降级规则

| 时间点 | 必须达到的状态 | 未达到时的处理 |
| --- | --- | --- |
| 20 分钟 | 基线绿、入口已定位 | 停止并报告基线阻塞。 |
| 80 分钟 | guard 绿、发球不回归 | 不进入新情景，先收敛 guard。 |
| 155 分钟 | 第二/第三触球分派与核心情景绿 | 放弃额外诊断字段，不放弃规则边界。 |
| 230 分钟 | 所有指定情景、smoke、完整 EditMode 完成 | 若失败，只处理明确回归；其余工作另开任务。 |
| 240 分钟 | 审查、diff、提交准备完成 | 停止新增范围，记录未完成项。 |

禁止的“降级”：放宽规则断言、把球员碰撞体当作合法触球、用测试强制成功接球、跳过第三触球
边界、把 Replay 缺口伪装成玩家触球。

## 提交拆分

1. `test: add specified net continuation rallies`
2. `fix: unify first net deflection continuation`
3. `test: verify net continuation determinism`

若阶段 1 与阶段 2 必须共同修改同一 Director 生命周期，可合并为一个完整行为提交；不得为了
形式拆出无法独立运行的中间提交。
