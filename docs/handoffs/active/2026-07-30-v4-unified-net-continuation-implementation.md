# V4 统一触网续球实现

- Status: active
- Owner: Match collaborator
- Priority: P1
- Scope: Match / Replay / Formal 6v6 的首次有效触网续球
- Supersedes: 无；V5 属性合同设计暂缓实现

## 当前要求

按 `docs/superpowers/plans/2026-07-30-v4-unified-net-continuation-implementation-plan.md`
完成 Formal 6v6 的首次有效触网续球统一处理。覆盖发球、第二触球、第三触球和
Block 后触网；保持既有 V4 合同和 Career 生命周期不变。

## 非目标

- 不修改 `MatchContextV4`、`MatchResultV4`、`MatchReplayV4` 或存档。
- 不实现 V5 属性、逐球员报告、Career 结算、快速模拟、多局制、换人或自由人替换。
- 不改写网体物理，不用测试直接提交触球或比分。

## 交付物

- 单飞行段首次触网 guard 与按剩余触球额度的正式续球分派。
- 指定 Formal 6v6 物理情景与 fixed-seed 验证。
- 受影响的测试、变更记录及完成状态。

## 验收条件

- 发球触网不回归；第二触球留本方可由真实几何救球。
- 第三触球留本方不产生第四触球；越网时对方获得真实防守机会。
- Block 后只走既有 post-block continuation；同一飞行段最多一次续球分派。
- 指定情景、正式 6v6 smoke、受影响 EditMode 与 diff 检查通过。

## 依赖资料

- `AGENTS.md`
- `docs/superpowers/plans/2026-07-30-v4-unified-net-continuation-implementation-plan.md`
- `docs/superpowers/specs/2026-07-30-v4-unified-net-continuation-design.md`
- `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

## 结束处理

完成或替代时，将本文件移入 `docs/handoffs/completed/`，更新 Status、结束日期和后继任务单链接。
