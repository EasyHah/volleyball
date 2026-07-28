# CHG-20260723-003：Career 首周 UI 可玩垂直切片

- 日期：2026-07-23
- 状态：已完成
- 负责人：Career / Bootstrap
- 影响模块：Career.Application / Career.Presentation / Bootstrap / Project Settings / Tests / Docs
- 交互级别：跨模块（重点）
- 关联分支：`feature/shared-career-fake-contract`
- 关联提交：`4962d89` / `0295e10` / `09686f7`

> [!IMPORTANT]
> 本次在单一 Unity 工程内完成 Career UI 与真实本地仓储、Career Application 服务、
> Shared Match V2 固定 fixture 的运行时组装。Match 源码、场景和 Shared 契约／golden fixture
> 保持冻结；搭档无需改代码。后续物理 6v6 只需替换现有 runner，不得绕开
> PendingMatch 和 SettlementReceipt。

## 改动摘要

- 新增本地 UI workflow facade，统一档案创建、列举、载入、生涯索引刷新与安全状态保存。
- 新增单 Shell 的七类路由：本地档案、职业生涯、大学试训、生涯主页、比赛准备、
  比赛总结和周末结算；学业和社交继续只作为事件。
- 赛前页展示对手与 6v6 阵容；结算页展示选定重点、执行结果、个人事实和八轴成长，
  重启进入第二周后仍可查看最后一条 SettlementReceipt。
- 同步写异常不会让 UI 永久卡在 Saving；版本冲突采纳仓储返回的权威快照。比赛异步
  异常／取消后会 best-effort 重载当前生涯；若原子结算已完成，则恢复总结页而不会重打。
- 新增 `CareerVerticalSlice` 场景、PanelSettings、中文字体回退、运行时输入资源、菜单输入路由与
  可重建场景的 Editor 工具。场景成为构建列表首项，原有 4 个 Match 场景全部保留。

## 跨模块交互重点

- 接口提供方：Career Application/Persistence 提供权威快照与命令；Shared Match V2 只提供冻结 fixture。
- 接口使用方：Career Presentation 只通过 `ICareerUiUseCases`；Bootstrap 负责唯一的具体组装。
- 数据方向：`UI -> Career command -> local atomic snapshot -> Match V2 fixture -> Career settlement -> UI`。
- 兼容性：兼容。Career Domain/Application 没有新增 Shared V2 引用，Match 和 Shared 冻结树未修改。
- 消费方或后续负责人需要做什么：Match 无需改代码。真实 6v6 runner、伤病、合同、转会和在线账号仍在
  后续阶段。

## 验证

- [x] UI workflow focused EditMode：4/4。
- [x] UI controller focused EditMode：5/5，包含同步异常、权威快照和提交后异常恢复。
- [x] UI Toolkit focused PlayMode：1/1，覆盖真实面板、1280×720 / 1920×1080、运行时 action 启用与返回路由。
- [x] 完整 EditMode：1081/1081，Unity `6000.3.20f1`，6.75 秒。
- [x] 完整 PlayMode：12/12，249.39 秒；耗时主要来自冻结 Match 物理场景回归。
- [x] 两轮独立 agent 集中静态复核通过；未触发重复 Unity 全量运行。
- [ ] Windows x64 开发构建与纯键鼠／纯 XInput 完整闭环属于阶段 9。

## 回滚与风险

回滚时应同时移除 Career 场景、PanelSettings、运行时输入资源、Bootstrap adapter/router 和构建列表入口，
不能只删 UI 场景。已知剩余风险是完整设备闭环尚未在 Windows 构建中人工验收，以及首版仍使用固定
FakeMatch fixture；两项都不得被误记为真实物理生涯比赛已接入。
