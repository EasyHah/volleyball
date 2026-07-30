# Career x Match V5 属性合同设计

- Status: active
- Owner: Career / Match collaborator
- Priority: P1
- Scope: Career / Match / Shared / Replay 的 V5 属性与逐球员报告边界
- Supersedes: 无；承接已完成的
  `docs/handoffs/completed/2026-07-29-career-match-integration-redesign.md`

## 当前要求

先完成 V5 的设计决策和消费证据计划，再决定是否修改 Shared 合同。设计必须从 Career 已确认的
八项明示属性出发，区分全职业基础属性、位置或培养方向带来的特色属性，以及只属于 Career 的状态；
每个进入 V5 的权威字段都必须有确定的 Career 来源、确定性映射、Match 正式消费路径、Replay 解释
和固定种子验证方案。

逐球员比赛报告需要支持 Career 的成长、教练信任、疲劳与伤病结算，但不能把无法由物理比赛证明的
技术统计重新估算出来。开始实现前必须确认 PR #6 已合并，并从最新 `origin/main` 建立新的里程碑分支。

## 非目标

- 不在设计确认前修改 `MatchContextV4`、`MatchResultV4`、`MatchReplayV4` 或存档 Schema。
- 不用 optional field、默认值、字符串扩展袋或 `Reserved` 字段伪装 V5。
- 不同时加入多局制、换人、自由人替换、海外联赛或在线功能。
- 不预先实现副职业；只评估它是否需要合同支持以及需要哪些正式消费证据。
- 不因位置模板或培养方向覆盖 Career 传入的球员身份与权威基础能力。

## 交付物

- V4 → V5 字段差异表及每个新字段的权威来源、单位、范围和版本规则。
- Career 八项属性到 V5 base/derived 属性的单调映射与疲劳边界。
- Match 对每个新增字段的正式消费路径和 Replay 解释方案。
- 逐球员 Result/Replay 报告最小字段集，以及 Career 结算的事实/后果边界。
- 兼容、迁移、回滚、fixture/golden vector 和固定种子验证方案。
- 设计评审通过后，独立的实现里程碑与验收清单。

## 验收条件

- 每个拟新增权威字段同时具备 Career 来源、确定性推导、正式消费、Replay 解释和测试证据计划。
- Career、Match、Shared、Replay 的所有权和数据流无循环依赖。
- 明确旧 V4 Pending、存档和回放是继续支持、迁移还是拒绝，且失败行为可恢复。
- 逐球员报告只记录比赛可证明的事实，Career 独立计算成长、信任、疲劳和伤病后果。
- 用户或双方负责人确认 V5 字段表与首个实现切片后，才进入代码实现。

## 依赖资料

- `AGENTS.md`
- `docs/career-domain-rules.md`
- `docs/career-match-v4-integration-contract.md`
- `docs/changes/2026-07-30-002-career-formal-6v6-lifecycle.md`
- `docs/handoffs/completed/2026-07-29-career-match-integration-redesign.md`
- `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

## 结束处理

设计确认后，将本文件移入 `docs/handoffs/completed/`，更新 Status、结束日期和后继实现任务单链接。
