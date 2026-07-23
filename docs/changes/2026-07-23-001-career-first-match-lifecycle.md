# CHG-20260723-001：Career 首场比赛持久化生命周期

- 日期：2026-07-23
- 状态：已完成
- 负责人：Career / 共同模块负责人
- 影响模块：Career.Domain / Career.Application / Career.MatchIntegration / Tests / Docs
- 交互级别：跨模块（重点）
- 关联分支：`feature/shared-career-fake-contract`
- 关联提交：6C1 `7452f8283bb24175b4de03fcbb494c1e36942691`；
  6C2 `b7b0e3aea932bd74982c932e2b7ed3458b0900c5`；
  6C2 roster 修正 `810a0f177072f5d881644fcac76974f3a426cf7c`；
  6C3 提交主题 `feat(career): settle first week atomically`

> [!IMPORTANT]
> 本次完成 Career 首场固定 6v6 fixture 从 canonical context 持久化、执行、失败重试到
> canonical context/result 原子结算的闭环。Shared Match V2 的 DTO、canonical codec 和
> golden fixture 没有修改；Career.Application / Domain 也没有引用 Shared V2。
> Match 模块无需改代码。后续真实物理比赛生产者只需履行现有 V2 契约，不得绕过 PendingMatch
> 和 SettlementReceipt 生命周期。

## 改动摘要

- `ICareerMatchExecutor` 以 Career 自有的不可变 canonical envelope 接收和返回精确字节；
  Shared V2 转换继续只存在于 Career.MatchIntegration。
- 创建比赛时先将 PendingMatch、AwaitingMatch 和创建回执单次 CAS 提交，再执行已提交字节；
  执行失败或取消不会清除 PendingMatch，可从存档原字节重试。
- 首场 roster 保留主角权威球衣号；主队 NPC 按冻结顺序使用排除主角后的最小五个正整数，
  客队固定 `1..6`。
- 结算先严格联合验证 canonical context/result，再按 session 查询历史和 SettlementReceipt。
  完成比赛只计算一次规则，并在单次 CAS 中同时写入历史、结算回执、成长与状态后果，清除
  PendingMatch 和训练重点，进入大学第 1 季第 2 周空白 Planning。
- 结算没有 command `OperationId`、fingerprint 或新的 generic `OperationReceipt`。
  由于现有本地仓库的临时文件隔离 API 需要 correlation ID，唯一一次 repository commit 使用
  `new OperationId(sessionId)`；该值不参与任何结算业务判断，也不写入新的业务回执。

## 冻结身份与兼容性

- Career 存档：schema `2`、content `1`、ruleset `1`、contract `2`、
  career random algorithm `1`。
- 比赛生命周期：Shared Match contract `2`；首场 fixture
  `fixture.career.u1w1.6v6@1`；赛程 `schedule.u1w1.match.01`；
  竞争 `competition.university.v1`。
- 结算规则：`CareerMatchSettlementRulesV1`，只应用八轴成长、疲劳、心态和教练信任；
  伤病及其他长期后果仍未实现。
- canonical `contextHash` / `resultHash` 是 V2 语义哈希，不是整个 JSON 文件的 SHA-256。
- 已结算 session 的相同 hash 对返回 Existing；不同 pair 返回 SessionResultConflict；
  CAS 冲突只重载结算索引，不重新计算或二次提交。

## 跨模块交互重点

- 接口提供方：Shared Match V2 提供已冻结的 schema、canonical codec 与 fixture producer；
  Career.MatchIntegration 提供 Career-owned executor adapter。
- 接口使用方：Career.Application 的 pending 与 settlement 服务。
- 数据方向：`Career launch -> canonical context -> persisted PendingMatch -> Match producer ->
  canonical result -> Career atomic settlement`。
- 兼容性证据：Application / Domain 无 Shared V2 引用；V2 golden fixture 保持冻结；
  canonical pair 必须由同一 executor 联合验证。
- 消费方或后续负责人需要做什么：Match 当前无需改代码。未来真实比赛接入必须返回同一 V2
  canonical result 契约；UI、Bootstrap、在线、伤病和第二周玩法不在本次范围。

## 验证

- [x] Stage 6C1 persisted execution 适配器聚焦测试与全量 EditMode 通过。
- [x] Stage 6C2 pending 创建/重试、CAS、失败恢复与球衣号 `1/2/6/99` 测试通过。
- [x] Stage 6C3 覆盖 pair-before-load、原子结算、Existing、结果冲突、Abandoned、
  普通版本冲突及 CAS exact/conflict/missing 不重算。
- [x] 全量 Career/仓库 EditMode 回归通过（精确数量见 6C3 ignored report）。
- [x] Python 8 项仓库检查、冻结哈希、程序集边界、变更索引与 `git diff --check` 通过
  （精确命令与输出见 6C3 ignored report）。

## 回滚与风险

回滚 6C3 会恢复到可持久化并重试 PendingMatch、但不会消费 canonical result 的状态。不得只回滚
Domain 快照工厂或只回滚 Application 服务，否则会破坏存档生命周期的一致边界。当前主要剩余风险是
真实物理 Match producer 尚未接入，以及第二周以后、伤病、合同和在线账号仍未实现；这些必须作为
独立阶段开发，不能修改本次已冻结的首场结算语义。
