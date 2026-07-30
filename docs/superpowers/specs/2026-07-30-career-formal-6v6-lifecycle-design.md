# Career × Match V4 正式物理 6v6 生命周期设计

**日期：** 2026-07-30

**状态：** 已批准，待实现

**范围：** 从 Career 已持久化 `PendingMatch` 启动真实
`FormalIndoor6v6`，回收原生 V4 result/replay，并由 Career 完成校验、
幂等结算、取消与失败恢复。

## 1. 目标与约束

本切片完成以下闭环：

```text
Career 创建并持久化 PendingMatch
  -> Bootstrap 读取其 canonical MatchContextV4
  -> 附加加载 FormalIndoor6v6
  -> Match 原样消费 context、12 人身份、Derived 和 matchSeed
  -> Match 返回 MatchResultV4 与一个 canonical MatchReplayV4 回合段
  -> Bootstrap 卸载比赛场景
  -> Career 校验 SessionId + ContextHash + ResultHash
  -> Career 原子、幂等结算
```

本切片不得：

- 修改 `MatchContextV4` 或把新增字段继续称为 V4；
- 加入多局制、换人、自由人替换、副职业或比赛中途存档；
- 让 Match 按位置重建、重排或覆盖 Career 传入的身份、基础属性或
  `DerivedMatchAttributesV4`；
- 把 V4 的 `points/contacts/errors` 按位置猜成扣球、拦网、接发等正式技术事实，
  并据此给予正式技术成长；
- 在 Match 中读取 Career 原始疲劳。

## 2. 选择的场景生命周期

Bootstrap 以 `LoadSceneMode.Additive` 加载 `FormalIndoor6v6`。Career 场景、
仓储、用例对象和等待中的异步调用保持存活；比赛期间只隐藏 Career
`UIDocument` 并暂停其菜单输入，不禁用 `CareerUiShell`，避免取消其生命周期
token。

选择附加加载而不是单场景切换，原因是单场景切换会销毁当前 Career UI 和
Bootstrap，对正在等待的执行任务触发取消，并可能在结算提交前重建一个仍显示
`PendingMatch` 的 Career 场景。附加加载让比赛完成、结算提交和 UI 恢复保持在同一
对象图内。

Bootstrap runner 在一次执行中按以下顺序工作：

1. 拒绝并发的第二场执行；
2. 验证 canonical context，并向 Match 的一次性启动入口登记同一对象；
3. 隐藏 Career UI，附加加载正式比赛场景并将其设为 active scene；
4. 在场景 `Awake` 初始化完成、首个回合开始前定位 director、球和 12 名球员，
   挂接并启动 `MatchReplayRecorder`；
5. 等待 `FormalSixVsSixRallyDirector.Result`；
6. 校验 result 属于原 context，同时取得已完成的 canonical V4 replay 回合段；
7. 卸载比赛场景、恢复 Career scene 和 UI，然后把 result/replay 返回调用方；
8. 既有 Career 服务用 canonical context/result 完成结算。

当前 `MatchReplayRecorder` 的发布语义是一段完整回合，而不是整场录像。本切片返回
该原生 `MatchReplayV4` 回合段作为 `base -> derived -> plan/envelope -> actual ->
rule` 的消费证据；不扩张 replay schema，也不把它写入 Career 存档。

## 3. 模块边界与公开 API

### 3.1 Match

Match 新增一个一次性 formal-context 启动入口。它只保存一个待消费的完整
`MatchContextV4`，由 `FormalSixVsSixRallyBootstrap.Awake` 消费；没有外部输入时，
现有直接打开场景的沙盒路径保持不变。

入口必须：

- 拒绝空 context 和重复登记；
- 成功消费后立即清空；
- 在场景加载失败或取消时允许 Bootstrap 按 session 清除未消费登记；
- 把同一 context 交给现有 `InitializeV4`，不得复制、重建或重排 roster。

Match 还公开正式 6v6 当前使用的 physics hash 与 trajectory predictor
configuration，供 Career 在冻结 context 时使用。它们来自正式运行时配置，不通过
创建一份沙盒 context 间接取得。

### 3.2 Bootstrap

新增 `CareerFormalSixVsSixMatchRunnerV4`，实现现有
`ICareerMatchRunnerV4`。具体类型另提供返回以下数据的公开执行方法：

```text
CareerFormalMatchRunOutcomeV4
  Result: MatchResultV4
  Replay: MatchReplayV4
```

接口实现把同一次执行的 `Result` 返回 Career；具体 API 保留 replay 供集成验证和
后续诊断使用。runner 只负责跨场景编排，不计算比赛规则或 Career 后果。

### 3.3 Career.MatchIntegration

`CareerMatchV4Mapper` 接受显式的 V4 runtime configuration：

```text
physicsConfigurationHash
trajectoryPredictionProviderConfiguration
resultFactPolicy
```

默认配置保持现有 fixture 行为和 fixture 测试。正式 Bootstrap 使用 Match 发布的真实
物理/预测配置与 Direct fact policy。

`CareerFirstMatchLaunchFactoryV1` 支持显式选择 Fixture 或 Direct；默认仍为 Fixture，
正式 Bootstrap 注入 Direct，避免改变现有测试和离线 fixture 的构造语义。

## 4. 结果事实与成长边界

物理 Direct 路径只把 V4 能直接证明的事实交给 Career：

- session、context、result identity；
- 胜方和单局比分；
- 12 人身份；
- Match 实际记录并归一化的 workload。

Direct policy 不把 `points/contacts/errors` 按位置拆成扣球、拦网、接发、防守或关键球
事实；对应 Career 技术和稳定性事实全部为零。因此本切片不会把 fixture 兼容估算当作
正式技术成长。fixture policy 保留旧映射，仅用于既有离线 fixture 基线。

`MatchSet` 内部继续累计实际的原始触球负荷。创建 V4 result 时，以本场 12 人中的最大
原始负荷为分母，把每人的负荷确定性归一化到 `[0,1]`；全员为零时全部返回零。该转换：

- 保持零值和相对顺序；
- 不伪造技术动作类型；
- 满足 Career × Match V4 的 workload 单位约束；
- 不修改 `MatchResultV4` schema。

逐球员扣球、发球、接发、防守、拦网、移动和起跳详细报告仍是 V5 后续输入。

## 5. 随机性与确定性

正式运行时中所有实际随机执行误差都使用冻结的 `MatchContextV4.Seed`。目前
`CourtPerceptionAdapterV3` 已使用该 seed；`SkillExecutionResolver` 的正式调用点将从
固定 `7351` 改为当前 context seed。

`TeamRallyDecisionPlanner` 当前保存 seed 但从不读取。该纯确定性路径删除 seed 字段和
构造参数；`RallyDecisionCoordinatorV3` 同步改为无 seed 构造。prototype 若仍需要一个
执行误差 seed，必须通过明确命名的 prototype fallback 提供，不得混入 formal path。

本切片不承诺 Unity 物理逐帧完全确定，但相同 context 的所有随机 AI 输入必须由冻结
seed 派生；canonical replay identity 继续记录实际执行证据。

## 6. 取消、失败与一次性完成

runner 为每次执行建立唯一活动状态：

- 调用方 cancellation token 被取消时，停止等待并卸载已加载的比赛场景；
- 键盘 Escape 或手柄返回键触发同一取消路径；
- 场景不存在、加载失败、缺少 director/球员/球、replay 无法完成或 result 校验失败时，
  抛出执行失败；
- 所有退出路径都恢复原 active scene、Career UI 与菜单输入，并清理未消费的 Match
  startup；
- 只有经过 `ValidateAgainst(context)` 的非空 result 可以成功返回；
- 单次执行只返回一次，runner 拒绝并发启动。

Career 现有 `CareerPendingMatchService` 在取消和执行失败时不提交 settlement，因此原
`PendingMatch`、canonical context 和 seed 保持不变。重试从同一 payload 重新开始。
`CareerMatchSettlementService` 继续以
`SessionId + ContextHash + ResultHash` 和 CAS receipt 处理重复提交；相同三元组返回
Existing，不重复成长或推进周次。

## 7. 兼容性、所有权与回滚

- Shared V4 schema、hash family 和 canonical JSON 不变，无存档迁移；
- 已有 fixture runner 和默认直接打开 `FormalIndoor6v6` 的沙盒路径保留；
- Career Domain/Application 不引用 Match；
- Match/Shared 不引用 Career；
- context 映射仍归 `Career.MatchIntegration`；
- 跨场景 runner 和实际装配归 Bootstrap；
- Match 只拥有 formal scene 的 context 消费、物理执行、result 与 replay 事实。

回滚时可以移除 Bootstrap runner 和一次性 Match startup，并把
`CareerVerticalSliceBootstrap` 恢复为 fixture factory/runner 注入。由于
`PendingMatch` canonical bytes、Shared schema 和 Career 存档结构没有改变，已存在的
待处理比赛仍可由 fixture 路径恢复，不需要数据迁移。

## 8. 验证

实现采用少量高价值测试：

### EditMode

- Direct launch 不带 fixture/simulation 版本；fixture 默认行为不变；
- physical mapper 写入 Match 发布的真实 physics/predictor 配置；
- Direct fact policy 不产生估算的技术/稳定性事实；
- Match startup 拒绝重复登记、按 session 清理并只消费一次；
- MatchSet workload 归一化位于 `[0,1]` 且保持零值和相对顺序；
- team decision planner 不再暴露无效 seed；
- formal `SkillExecutionResolver` 调用只取 context seed。

### PlayMode

- 从 Career 生成并 canonical round-trip 的 context 启动真实附加
  `FormalIndoor6v6`；
- director 持有完全相同的 session、context hash、12 人顺序、Derived fingerprints
  和 seed；
- 正式比赛产生可 `ValidateAgainst` 的 12 人 `MatchResultV4` 与同 context 的
  `MatchReplayV4`；
- 取消和无效场景加载后比赛场景被清理，原 `PendingMatch` canonical bytes 不变；
- 同一 Pending 重试可成功，首次结算推进一次，重复回调/提交返回 Existing 且 revision
  不再增加。

### 冻结后回归

- focused Career/Match/Bootstrap tests；
- 受影响 Career、Match、Shared EditMode；
- 完整 EditMode 一次；
- 正式 6v6 与新增 Career 生命周期 PlayMode；
- Windows x64 development build；
- `git diff --check`、程序集依赖和最终 diff 审查。

## 9. 完成条件

只有以下全部满足时，active handoff 才能完成：

1. 已持久化 Pending 的原 canonical V4 context 进入真实 formal director；
2. 12 人身份、顺序、Derived fingerprints 和 seed 未被重建或覆盖；
3. result/replay 均绑定同一 context，result 恰好覆盖 12 人；
4. 取消和场景失败不产生伪造 result，Pending 可原样重试；
5. 重复完成只发生一次 Career 结算；
6. Direct 结算不使用按位置估算的正式技术统计；
7. 自动验证与 Windows 构建通过，必要人工验收状态被如实记录；
8. active handoff 移入 `docs/handoffs/completed/`，并另建 V5 任务单记录详细球员报告和
   属性体系后续讨论。
