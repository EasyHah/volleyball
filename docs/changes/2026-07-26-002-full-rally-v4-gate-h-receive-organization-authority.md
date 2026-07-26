# CHG-20260726-002：Full Rally V4 Gate H 接发与组织权威

- 日期：2026-07-26
- 状态：进行中
- 负责人：Shared / Match / Replay / Docs
- 影响模块：Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gate-h-receive-organization-authority`
- 关联提交或 PR：`818ac52`（设计）、`1f8529c`（实施计划）

> [!IMPORTANT]
> Shared 将提供可选的 canonical Replay V4 organization-authority record；
> Match 负责生成和消费 live authority evidence，Replay recorder 只映射
> accepted contact 自身携带的证据。旧 V4 replay 保持可读；Career 与 Bootstrap
> 无需修改代码。

## 改动摘要

Gate H 将正式 6v6 的接发、二传预备、应急代传、组织、coverage/replan 和攻击预备
切换为责任计划的单一权威路径，并删除 director 中对应的正式 legacy tactical writer。
Gate F 计划域保持不可变、无命令；Gate G facade 继续作为唯一 player command 边界。

本改动不迁移 3v3，不接管 Set 后的攻击完成、攻防、soft action、tool recovery、
CourtAwareness 或 director 最终瘦身。

## 具体变更

- 文件、场景、资源或程序集：实施中。
- 新增或修改的公开类型/字段：实施中。
- 行为变化：正式 6v6 Receive/Organize 将一次性切换为 Gate H 单 writer；3v3 保持兼容。

## 跨模块交互重点

- 接口提供方：Shared（Replay V4 organization-authority value、strict JSON/hash）
- 接口使用方：Match authority controller 与 Match replay recorder
- 数据方向：`Match plan/actual contact -> Match replay mapping -> Shared Replay V4`
- 兼容性：旧 V4 replay 无新增 record 时保持可读；新正式 Receive/Set capture 必须提供事件自有证据。
- 对方开发者需要做什么：Career/Bootstrap 无需改代码；新增 replay reader 必须保持 canonical 字段顺序和 strict validation。

## 验证

- [ ] EditMode 测试
- [ ] PlayMode 测试
- [ ] 手动场景验证
- [ ] 序列化、存档或迁移验证（Replay V4 canonical JSON/hash 与旧 V4 读取）

Gate G 起点基线已在 Unity `6000.0.43f1`、当前隔离 worktree 中复验：

- EditMode：`590/590` passed，0 failed/skipped/inconclusive，
  `TestResults/GateH-baseline-editmode.xml`。
- PlayMode：`30/30` passed，0 failed/skipped/inconclusive，
  `TestResults/GateH-baseline-playmode.xml`。

这些是实施前基线，不作为 Gate H 完成证据。

## 回滚与风险

主要风险是 formal/legacy 双 writer、stale revision callback、registered setter 优先
退化、event-owned V4 identity 丢失或 canonical replay 兼容性破坏。实现完成后记录
精确提交边界；回滚时必须同时评估 authority controller、director cutover 与 Shared
reader/writer，不能只回滚其中一端。
