# CHG-20260730-004：MatchPerformanceReportV1 与正式比赛事实累计

- 日期：2026-07-30
- 状态：已自动验证
- 负责人：Shared / Bootstrap
- 影响模块：Shared / Bootstrap / Career Tests / Repository / Docs
- 交互级别：跨模块（重点）
- 关联分支：`milestone/match-performance-report-v1`
- 关联提交或 PR：尚未创建

> [!IMPORTANT]
> Shared 新增独立 `MatchPerformanceReportV1`，Bootstrap 从搭档冻结 Match 模块的现有公开事件生成报告。
> V4 context、result 和 replay 的规范 bytes 与哈希均未修改；Career 存档和结算暂不消费该报告。

## 改动摘要

新增严格、可哈希、恰好覆盖正式 12 人的逐球员事实报告，并将它接入正式 6v6 Runner 的一次性返回
结果。为遵守搭档模块冻结规则，累计器位于 Bootstrap 集成层，不修改 Match 代码；只记录现有事件
能够证明的事实，无法证明的物理负荷保持为零。

## 具体变更

- 文件、场景、资源或程序集：
  - Shared 新增表现报告值对象、规范 JSON codec、严格解析、哈希和身份校验。
  - Bootstrap 新增事件累计器，并让正式 Runner 返回 `Result + Replay + PerformanceReport`。
  - Bootstrap 显式引用 Match Domain、AI 与 Presentation 以读取公开权威事件。
  - Career EditMode 增加累计规则 focused 测试；Shared EditMode 增加合同与 golden hash 测试。
  - Repository policy 解除 integrator-owned Shared 冻结，仅保留搭档 Match 冻结边界。
- 新增或修改的公开类型/字段：
  - 新增 `MatchPerformanceReportV1` 及其逐项事实类型。
  - `CareerFormalMatchRunOutcomeV4` 新增 `PerformanceReport`，构造器要求三项产物身份一致。
  - `ContractJson` 新增 Performance Report V1 序列化与反序列化入口。
- 行为变化：
  - 正式比赛开始时累计器与 Replay recorder 同时订阅 Match 事件。
  - 比赛结束后报告绑定同一 `sessionId/contextHash/resultHash`，不一致立即拒绝。

## 跨模块交互重点

- 接口提供方：Shared 提供报告合同；Bootstrap 负责从 Match 公开事件累计并返回。
- 接口使用方：当前为 Bootstrap Runner 和验证测试；Career 原子结算消费延期到独立 Schema 迁移任务。
- 数据方向：`Match public facts -> Bootstrap accumulator -> Shared report -> future Career settlement`。
- 兼容性：V4 context/result/replay 不变；Runner 的扩展 outcome 构造器是编译期可见的新接口。
- 消费方或后续负责人需要做什么：
  - Career 后续以独立高风险任务保存并幂等结算报告。
  - Match 后续补 action-window、实际移动/起跳/落地事件，并修复全场 Replay 目前只捕获首回合的问题。

## 验证

- [x] Shared / Bootstrap focused EditMode：最终 7/7 通过。
- [x] 受影响模块 EditMode 回归：53/53 通过。
- [x] 正式场景关键 PlayMode：1/1 通过，真实 6v6 完赛并经过 Runner 报告生成路径。
- [x] Repository validation、8 个校验器自测与 `git diff --check` 通过。
- [x] 一次独立 Agent 全面复核；集中修复 2 个 P1、1 个 P2 后，针对性复审通过。

测试环境：Unity `6000.3.20f1`，Windows。未运行 Windows Player 构建；本任务未修改场景、输入、
Package、ProjectSettings 或平台文件系统。

## 回滚与风险

可回滚新增 Shared 合同、Bootstrap 累计器与 Runner outcome 扩展，不涉及存档迁移。当前主要限制
是冻结 Match 事件不足：无接触动作窗口和实际运动负荷不能证明，因此对应数据为零；现有 Replay
仍只覆盖第一回合，不能作为全场逐事件证据。不得在 Career 中把这些零值解释为球员没有移动或起跳。
