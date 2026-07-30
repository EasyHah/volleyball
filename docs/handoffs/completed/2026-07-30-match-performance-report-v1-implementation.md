# MatchPerformanceReportV1 实现

- Status: completed
- Completed: 2026-07-30
- Owner: Shared / Bootstrap integration
- Priority: P1
- Scope: 独立逐球员事实报告合同、Bootstrap 事件累计器及 Result/Replay 绑定
- Supersedes:
  `docs/handoffs/completed/2026-07-30-career-match-v5-attribute-contract-design.md`

## 当前要求

实现已确认设计的第一个代码切片：

1. Shared 提供 immutable `MatchPerformanceReportV1`、逐球员事实值对象、严格规范序列化、哈希和
   context/result 绑定校验；
2. Bootstrap 从冻结 Match 已公开的正式物理事实流累计扣球、发球、接发、防守、拦网、二传、
   可证明负荷和稳定性事实；
3. Result、Replay 与 Performance Report 共享 `sessionId/contextHash`；Performance Report 另以
   `resultHash` 绑定 Result，冻结的 Replay V4 本身不新增该字段；
4. 首个实现切片止于报告生成和验证，不修改 Career 存档 Schema，不实现 V5 基础属性
   输入合同。

实现遵循：
`docs/superpowers/specs/2026-07-30-career-match-v5-attributes-and-performance-report-design.md`。

## 非目标

- 不修改 `MatchContextV4`、`MatchResultV4`、`MatchReplayV4` 的既有规范 bytes 或哈希。
- 不在本任务迁移 Career 八项属性、保存 Performance Report 或修改结算 Schema。
- 不加入多局制、换人、自由人替换、海外联赛、在线功能或副职业。
- 不根据总得分、总触球、位置或场景最终状态估算无法证明的技术事实。
- 不为尚无正式物理事件的字段伪造默认成功或失败。

## 实现阶段

1. Shared 合同、严格 codec、hash、constructor/invariant 和 golden fixture。
2. Bootstrap-owned 累计器与冻结 Match 正式事件适配。
3. Runner 产物绑定和 Result/Replay/Report 一致性验证。
4. focused EditMode；仅对真实场景接线运行必要的关键 PlayMode。

## 验收条件

- 正式 6v6 报告恰好覆盖 context 的 12 名球员并按 `PlayerId` 规范排序。
- 每项非零事实都能追溯到 Match 已接受事件；未知事实保持零，不进行推测。
- 相同事实流产生相同规范 bytes 和 `reportHash`。
- context/result/report 任一身份或哈希不一致时拒绝绑定。
- 报告和 Replay 共同覆盖的已捕获事件对 actor、action 与顺序相容。
- 既有 V4 context/result/replay fixtures 和正式 6v6 回合不回归。

## 冻结边界与延期

- 本任务不修改 `Assets/Volleyball/Match`；累计器只订阅其现有公开事件。
- 现有 Match Replay recorder 在首次 `RallyResolved` 后结束，因此全场 Replay 捕获需由搭档后续在
  Match 模块修复；本任务只保证 Result、Replay 与 Report 的 context 身份一致。
- 当前事件无法证明的移动距离、起跳、高负荷起跳、落地负荷和总负荷保持为 0。
- 无接触的扣球、二传和拦网窗口需要 Match 后续新增权威 window-opened 事件，当前不估算。

## 依赖资料

- `AGENTS.md`
- `docs/superpowers/specs/2026-07-30-career-match-v5-attributes-and-performance-report-design.md`
- `docs/career-match-v4-integration-contract.md`
- `docs/handoffs/completed/2026-07-30-career-match-v5-attribute-contract-design.md`

## 结束处理

实现与验证已完成：

- focused EditMode：7/7；
- 受影响 EditMode 回归：53/53；
- 正式 6v6 Runner PlayMode：1/1；
- Repository validator 自测：8/8，仓库校验和 diff 检查通过；
- 独立全面复核发现的 2 个 P1 和 1 个 P2 已集中修复，针对性复审通过。

后继任务必须独立开分支处理：

1. Career 存档 Schema、幂等结算与报告持久化；
2. V5 两层基础属性输入及比赛六类派生；
3. 搭档 Match 模块补 action-window、实际负荷事件和全场 Replay 捕获。
