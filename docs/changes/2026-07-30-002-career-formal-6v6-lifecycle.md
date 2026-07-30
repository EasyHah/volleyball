# CHG-20260730-002：Career 正式物理 6v6 V4 生命周期

- 日期：2026-07-30
- 状态：进行中（待 Windows x64 构建与人工验收）
- 负责人：Career / Match / Bootstrap
- 影响模块：Career.MatchIntegration / Career.Application / Match.Domain / Match.AI / Match.Presentation / Bootstrap / Tests / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/career-formal-6v6-lifecycle`
- 关联提交或 PR：待创建

> [!IMPORTANT]
> Bootstrap 新增 Career 持有的 V4 context 到 `FormalIndoor6v6` 的一次性启动桥；Match 只消费原
> context，返回原 context 绑定的 Result 与 Replay。Career 仍以已有 canonical context、`SessionId +
> ContextHash + ResultHash` 结算；没有修改 `MatchContextV4`、存档结构或 Shared 合同。

## 改动摘要

Career 垂直切片的首场比赛从固定 fixture runner 改为正式物理 6v6 场景的直接执行。启动、取消和
场景失败均不会制造中止 result；现有 `PendingMatch` 会保留并复用冻结 context。正式 V4 只把物理
比赛可证明的归一化 workload 回传给 Career，绝不把点数、contacts 或 errors 按位置估算成技术成长。

不包含多局制、换人、自由人替换、副职业、Career 属性体系重构或 V5 字段。

## 具体变更

- Bootstrap：`CareerFormalSixVsSixMatchRunnerV4` 加载 `FormalIndoor6v6`，注入同一 V4 context，
  捕获 context-bound replay，完成/取消后卸载 additive 场景并恢复 Career UI。
- Match Presentation：一次性 `FormalMatchContextStartupV4` 让正式 bootstrap 消费外部冻结 context；
  场景默认入口仍保留原有默认 context/scenario 行为。
- Match：正式执行随机采样使用 `MatchContextV4.Seed`；移除纯确定性 planner 的无效 seed 参数；
  set 输出将实际累积 workload 归一化至 V4 所需的 `[0,1]`。
- Career：正式 direct launch 不携带 fixture identity；mapper 使用正式物理配置，并采用
  `DirectAggregateOnly` 事实策略。
- Tests：覆盖 direct 映射不产生估算技术统计、direct launch、物理上下文注入、取消清理与真实 25 分
  物理整局的 Result/Replay 回传。

## 跨模块交互重点

- 接口提供方：Match Presentation 提供一次性 V4 启动桥和正式 runtime 配置；Bootstrap 提供 Career runner。
- 接口使用方：Career MatchIntegration 生成 canonical context；Career Pending/Settlement 生命周期继续消费
  原有 `ICareerMatchExecutor` 产物。
- 数据方向：`Career PendingMatch -> MatchContextV4 -> FormalIndoor6v6 -> MatchResultV4 + MatchReplayV4 -> Career`。
- 兼容性：兼容。无 Shared DTO、V4 schema、存档迁移或公共端口签名变更；fixture runner 保留给既有测试。
- 消费方或后续负责人需要做什么：V5 任务单再定义逐球员详细报告及属性重构；本次无需消费方改代码。

## 验证

- [x] focused EditMode：39/39，Unity `6000.3.20f1`。
- [x] 完整 EditMode：1469/1469，Unity `6000.3.20f1`，11.82 秒。
- [x] Career formal runner PlayMode：4/4；其中真实物理 25 分 6v6 从 Career canonical context 经
  `CareerMatchExecutorV4` 完整结束，runner 内部验证 context-bound Result/Replay，33.18 秒。
- [x] 取消路径：场景加载中及加载后取消均等待已排队加载完成后卸载，恢复 UI/active scene 且不残留
  启动 context。
- [x] 已有 Pending lifecycle 回归确认执行失败或取消保留可重试 Pending，并在本次 focused run 通过。
- [x] 无效 formal scene 验证为 fault 且不遗留启动 context；结合 Pending lifecycle 的执行失败回归，
  不产生中止 result 且保留原 canonical Pending bytes。
- [x] 独立代码复核：无 P0；两项 P1（Windows gate 状态、canonical context/scene failure 覆盖）已修复，
  并完成相应 PlayMode 复测。
- [ ] Windows x64 IL2CPP Development Build：在当前 macOS host 尝试运行
  `CareerWindowsDevelopmentBuild.Build` 返回 `BuildResult.Unknown`，未生成可验证 Windows 产物；需在
  指定 Windows x64 构建机重跑并完成键盘、手柄、图形与性能人工闭环。

## 回滚与风险

回滚可移除 Career bootstrap 中的 formal runner/mapper 配置并恢复 fixture runner；V4 context、存档和
Shared 合同不受影响。剩余风险是 Windows Player 的平台差异与长赛性能，不能由 macOS batchmode 代替。
