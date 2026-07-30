# CHG-20260730-003：Career x Match V5 属性与事实报告设计

- 日期：2026-07-30
- 状态：待确认
- 负责人：Career / Match / Shared / Replay
- 影响模块：Docs；后续实现将影响 Career / Match / Shared / Replay / Bootstrap
- 交互级别：跨模块（重点）
- 关联分支：`milestone/career-match-v5-contract-design`
- 关联提交或 PR：待创建

> [!IMPORTANT]
> 本次只冻结候选设计，不修改 Shared DTO、Career 存档或 Match 运行时代码。建议先以独立
> `MatchPerformanceReportV1` 补齐真实逐球员事实，再实现 V5 八项核心能力、readiness 和体能消费。

## 改动摘要

- 盘点 V4 临时属性映射和 `PlayerMatchStatsV4` 输出缺口。
- 定义独立逐球员事实报告的最小字段、来源、不变量、哈希绑定与原子结算边界。
- 定义 Career 八项明示属性到 V5 核心能力的一对一单调映射。
- 明确原始疲劳、培养方向、潜力、信任、心态与伤病的模块所有权。
- 规定 Endurance 必须拥有 Match 动态能量预算消费者和 Replay 解释后才能发布。
- 给出 V4 Pending、旧存档、回放、回滚和分阶段验证方案。

## 跨模块交互重点

- 接口提供方：Shared 提供版本化报告/V5 DTO；Match 提供实际事实和消费证据。
- 接口使用方：Career 校验报告并计算成长后果；Bootstrap 编排一次性产物。
- 数据方向：`Career frozen input -> Match -> Result + Replay + Performance Report -> Career settlement`。
- 兼容性：本设计本身无运行时变化；首个实现切片保持 V4 context/result/replay 不变。
- 消费方或后续负责人需要做什么：双方先确认设计文档第 11 节，再建立独立实现任务。

## 验证

- [x] 对照当前 Career 八项领域模型、V4 Mapper、PlayerSnapshot/Result/Replay 和正式 Match 事实流。
- [x] 重新抓取全部远端分支；搭档最新 PR #5 已在 `main`，无未同步的新提交。
- [x] 文档明确字段来源、正式消费者、事实/后果边界、兼容、回滚与测试计划。
- [ ] 用户或双方负责人确认六项设计基线。

## 风险与回滚

当前只有文档，可直接回滚文档提交。主要设计风险是把报告、V5 输入和存档迁移一次实现导致范围失控；
因此首个代码切片明确只生成并验证 Match-owned 报告。
