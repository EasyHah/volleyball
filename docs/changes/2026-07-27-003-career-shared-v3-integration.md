# CHG-20260727-003：Career 接入 Shared V3

- 日期：2026-07-27
- 状态：已自动验证，待人工验收
- 影响模块：Shared / Career.Application / Career.MatchIntegration / Career.Persistence / Bootstrap / Tests / Docs
- 交互级别：跨模块（重点）

## 决策

搭档在 `origin/main@3b59772` 中维护的 `Volleyball.Shared` V3 是唯一比赛契约权威。
删除 Career 分支曾新增的 `Volleyball.Shared.MatchV2` 并行程序集、fixture 和测试，
避免同名 V2 类型、重复规范序列化以及后续双方契约漂移。

Career Domain/Application 继续只暴露 Career 自有的启动、待比赛、事实与结算类型。
`Career.MatchIntegration` 将八项生涯属性映射为 `MatchContextV3`，并将
`MatchResultV3` 映射为结算所需事实；它不依赖 Match Domain、AI 或 Presentation。

## 当前里程碑实现

- `CareerMatchExecutorV3` 负责 V3 规范字节、严格 UTF-8、哈希和 session 校验。
- `DeterministicFixtureMatchRunnerV3` 暂时产生固定 25–21、十二人完整统计的开发结果。
- Match contract 固定为 3；content、ruleset 和 Career RNG 固定为 1。
- 存档 Schema 保持 V2，`PendingMatch` 和历史证据改为保存 V3 规范载荷。
- Bootstrap 不再序列化引用已删除的 JSON fixture；场景已重建并只保留 Input Action 资源。

当前 V3 只有粗粒度球员统计。首里程碑的技术分类是确定性的兼容估算，用于完成离线
“比赛事实 → 生涯后果”闭环，不代表真实物理比赛已经提供逐技术事件。

## 兼容与恢复

- 不含比赛载荷的既有存档继续按 Schema V2 读取。
- 新建待比赛和结算记录只接受 Match V3。
- 含已删除并行 Match V2 载荷的开发档不做猜测性字段迁移；校验失败后沿用候选隔离、
  备份重扫和恢复流程。
- 回滚时可整体回退本次集成提交；不得单独恢复 `Shared.MatchV2`，否则会重新引入双权威。

## 验收条件

- 模块边界不再引用 `Volleyball.Shared.MatchV2`，仓库策略与 asmdef 一致。
- V3 上下文包含双方各六人，fixture 结果包含十二人事实且可重复。
- 创建待比赛、执行、结算、规范载荷持久化与恢复全部使用同一 V3 context/result 对。
- Career 与受影响 Shared/Match 回归通过；Career 场景能进入首周、比赛结算并推进第二周。

## 自动验证

- Career/Shared/Match 完整 EditMode：`1127/1127`。
- Career 与 Match 完整 PlayMode：`22/22`。
- V3 focused：模块边界 `3/3`、映射与执行 `4/4`、待比赛 `17/17`、
  载荷恢复 `6/6`、比赛结算 `82/82`。
- Windows x86_64 Development Player：Unity `6000.3.20f1`、IL2CPP，构建成功。

仍需人工从新建档案开始走完首周，确认比赛摘要、状态变化和第二周入口的画面与操作。
