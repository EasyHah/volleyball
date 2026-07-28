# CHG-20260726-002：Full Rally V4 Gate H 接发与组织权威

- 日期：2026-07-26
- 状态：已完成
- 负责人：Shared / Match / Replay / Docs
- 影响模块：Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gate-h-receive-organization-authority`
- 关联提交或 PR：`818ac52..c3be951`（设计、实现与测试；最终文档提交另计）

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

- `ReceiveOrganizationPlanV3`、responsibility planner 与 authority coordinator
  固化 primary、最多两名 emergency、registered setter、backup organizer、
  attack preparation、revision/source sequence、coverage 与 fallback。
- `ReceiveOrganizationAuthorityController` 先完整 preflight，再经 Gate G player
  facade 原子提交接发、二传预备、组织触球和攻击预备；拒绝 stale/duplicate、
  阵容外 actor、未声明分支与取消已提交动作。
- `PhysicalMatchRallyDirector` 仅在 V3 Authority、正式 6v6 且完整十二人阵容下
  启用 Gate H；Shadow、Disabled 和 3v3 均不启用。无可行接球人时不发布命令，
  保留原有落地结算；Set accepted 后只交给现有 Gate I 临时攻击 seam。
- `MatchReplayV4` 新增可选 strict organization-authority record。正式 Receive/Set
  从 accepted contact 自身携带的 receipt 映射精确 plan、envelope、sample、
  trajectory、setter、fallback、branch 与 coverage identity；历史 V4 缺省字段
  继续按原 canonical bytes/hash 读取。
- 新增 plan/planner/coordinator/controller、Replay、能力独立性与正式/legacy
  场景回归；未修改场景或资源。

## 跨模块交互重点

- 接口提供方：Shared（Replay V4 organization-authority value、strict JSON/hash）
- 接口使用方：Match authority controller 与 Match replay recorder
- 数据方向：`Match plan/actual contact -> Match replay mapping -> Shared Replay V4`
- 兼容性：旧 V4 replay 无新增 record 时保持可读；新正式 Receive/Set capture 必须提供事件自有证据。
- 对方开发者需要做什么：Career/Bootstrap 无需改代码；新增 replay reader 必须保持 canonical 字段顺序和 strict validation。

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [ ] 手动场景验证（未执行；由自动化正式整局、录像与 3v3 场景覆盖）
- [x] 序列化、存档或迁移验证（Replay V4 canonical JSON/hash 与旧 V4 读取）

Gate G 起点基线已在 Unity `6000.0.43f1`、当前隔离 worktree 中复验：

- EditMode：`590/590` passed，0 failed/skipped/inconclusive，
  `TestResults/GateH-baseline-editmode.xml`。
- PlayMode：`30/30` passed，0 failed/skipped/inconclusive，
  `TestResults/GateH-baseline-playmode.xml`。

这些是实施前基线，不作为 Gate H 完成证据。

Gate H 最终实现 HEAD `c3be951` 在同一 Unity 版本与隔离 worktree 的新鲜证据：

- EditMode：`627/627` passed，0 failed/skipped/inconclusive，耗时
  `6.7276322s`，`TestResults/GateH-final-editmode.xml`。
- PlayMode：`31/31` passed，0 failed/skipped/inconclusive，耗时
  `705.0343568s`，`TestResults/GateH-final-playmode.xml`。
- 确定性双测：`2/2` passed，耗时 `7.883702s`，
  `TestResults/GateH-final-determinism.xml`；覆盖 recorder on/off authority
  fingerprint 与两次固定 seed canonical bytes/hash。
- Gate H 聚焦 EditMode：`180/180`；受影响 formal/legacy PlayMode：
  `19/19`。
- legacy production symbol scan、Domain/AI 反向依赖扫描与
  `git diff --check` 均无输出。
- 合并差异由当前主智能体按 authority duplication、stale lifecycle、
  setter/fallback、Replay identity/backward compatibility、3v3 与 Gate I
  scope 完成复审，无 unresolved blocker。按用户确认的 inline 执行方式未启用
  独立子智能体，因此不声称独立 reviewer 复审。

## 回滚与风险

主要风险是 formal/legacy 双 writer、stale revision callback、registered setter 优先
退化、event-owned V4 identity 丢失或 canonical replay 兼容性破坏。回滚应整体回滚
`7883724..c3be951` 的代码提交，并同时评估 Shared reader/writer 与历史新格式 replay；
不能只回滚 controller、director 或 Shared 其中一端。设计与计划文档可保留为历史，
不得在未恢复旧 writer 的情况下只移除 Gate H cutover。
