# Career x Match 联调重设计

- Status: active
- Owner: Match collaborator
- Priority: P0
- Scope: Career / Match / Shared / Bootstrap 的正式物理 6v6 联调边界
- Supersedes: `docs/career-match-v4-integration-contract.md` 第 3 节的当前属性映射；该映射在本任务完成前不得实现或作为新功能依据。

## 当前要求

在启动 Career 正式物理 6v6 接入前，重新设计 Career 长期成长属性、位置或角色因素、
短期比赛状态与 Match 输入之间的边界。

现有“Career 八项属性直接映射到 14 项 V4 属性”的一对多平均公式不可接受。新设计必须先
说明属性来源、语义、可培养范围和正式消费路径，再决定是否保留 V4、使用独立版本化赛前信封，
或升级 Shared Match 契约版本。

## 非目标

- 不直接实现 Career 正式物理 6v6 联调或修改 `FormalIndoor6v6` 入口。
- 不修改 `MatchContextV4` 后继续称其为 V4。
- 不以 fixture 的统计估算作为正式技术统计或成长结算依据。
- 不自行假设缺失的二传、判断、控球、体能或位置能力来源。
- 不启动路线图阶段 10 的代码实现。

## 交付物

- 两到三个属性边界方案、取舍和推荐方案。
- 推荐方案的 Career、Match、位置或角色、短期状态与派生属性责任划分。
- 契约版本策略和对 Shared、Bootstrap、Career、Match 的影响。
- Career 正式成长所需的逐球员比赛报告边界、字段语义与身份绑定策略。
- 经负责人确认后的设计文档；确认前不得编写实现计划或代码。

## 验收条件

- 明确不再使用旧映射，并消除每个 Match 输入的来源歧义。
- 每个新增正式属性或报告字段都具有 Career 来源、确定性推导或生成方式、Match 消费或生产路径、
  Replay 解释和固定种子验证策略。
- 明确哪些决策需要 Shared 新版本，哪些可在不影响规范比赛结果的独立信封中表达。
- 负责人书面确认设计后，才可创建后续实施任务单。

## 依赖资料

- `AGENTS.md`
- `docs/career-development-roadmap.md` 第 10 阶段
- `docs/career-match-v4-integration-contract.md`
- `docs/career-domain-rules.md`
- `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

## 结束处理

完成或被替代时，将本文件移入 `docs/handoffs/completed/`，更新 Status、结束日期和后继任务单链接。
