# Full Rally V4 Gate K Director Slimming、Replay 与 Calibration 收口设计

**日期：** 2026-07-28

**状态：** 已完成

**前置：** Gate A–J 已完成；正式十二人 V3 Authority 已拥有 rules、plan、
execution、Gate H/I authority 与 Gate J perception 的单一 writer 和 event-owned
Replay evidence。

## 1. 目标

Gate K 完成 Full Rally V4 的最后一个架构门：

1. 将战术候选、责任选择、动作命令选择和 replan policy 从
   `PhysicalMatchRallyDirector` 移入纯 AI coordinator/authority bridge；
2. director 只保留物理事件编排、比赛生命周期、轮转、计分/result、Replay dispatch
   和 feature-boundary setup；
3. Replay HTML 从单一事件清单升级为双方视角诊断，能追踪
   `ability -> perception/plan -> envelope -> actual result`；
4. 持久化 deterministic work budget/degradation evidence，但不把 wall-clock、
   allocation、cache hit 或 profiler 数据放进 canonical gameplay hash；
5. 用固定 seed/能力轴矩阵验证稳定性、工作预算和可解释 delta，并单独报告实际耗时。

## 2. 不做什么

- 不改变 Shared 的 V4 contract/version discriminator，不新增 V5 gameplay 字段；
- 不修改 V3 rules、球场尺寸、contact geometry、动作合法性或 50 分安全上限；
- 不用运行时耗时、帧率、cache hit 或 profiler 结果改变确定性决策；
- 不删除 3v3 legacy gameplay；只把它隔离到明确的 legacy coordinator；
- 不因一次比分结果调整能力系数；
- 不把隐藏 final route、未来 execution sample 或对手内部 plan 渲染成已观察事实。

## 3. Director 边界

### 3.1 保留在 director

- Unity component/bootstrap、player/ball scene binding；
- simulation tick、物理 contact/crossing/ground/net 事件；
- rules transition dispatch、rotation、score、result 与 rally restart；
- 把只读 world snapshot 和 accepted physical evidence 交给 coordinator；
- 应用 coordinator/controller 已发布的 movement/contact command；
- Replay event dispatch。

### 3.2 移出 director

- `TeamRallyDecisionPlanner` 的创建和 candidate ordering；
- Receive/Organize/Attack 固定链的输入构造与 next-stage selection；
- Gate H/I planning request、revision 和 perception receipt 组装；
- geometric set/attack route、block/floor support 和 recovery actor selection；
- stale callback、coverage/replan 与 authority receipt 的战术 lifecycle 状态。

迁移按 vertical slice 进行。每次先由新 coordinator 产生与当前完全相同的 immutable
command/evidence，再删除 director 中对应 writer。3v3 legacy 可调用同一个 facade 的
legacy mode，但不得重新进入 formal Authority。

## 4. Replay Work Evidence

在 Replay V4 event 上新增可选 strict/canonical `workBudget` record：

- configuration identity；
- deterministic candidate count；
- deterministic sample count；
- envelope expansion count；
- deterministic work units；
- selected degradation step；
- budget outcome（within budget / degraded / safe fallback）。

work units 是由离散候选、样本和扩展次数计算的确定性整数，不是耗时换算。record 中不
允许 wall-clock、allocation、thread、cache-hit、frame 或 profiler 字段。历史 Replay
没有该 record 时 bytes/hash 完全不变。

## 5. 双视角 HTML

HTML 对每个 event 渲染三个明确分区：

- `AUTHORITATIVE / ACTUAL`：规则、共享 trajectory、execution、actual sample/outcome；
- `HOME PERCEIVED`：仅当 event-owned perception/organization/defense evidence 属于
  Home 时显示 view identity、delay/confidence/uncertainty、visible threats、
  responsibilities 与支援选择；
- `AWAY PERCEIVED`：同上，属于 Away 时显示；没有 evidence 明确写
  `No event-owned view`，不得用 authoritative 值填充。

Attack/defense、organization、coverage 与 work-budget 使用现有 event-owned record；
不会从最终碰撞反推某方在决策时“应该看到”的值。HTML 生成是纯函数，相同 Replay
bytes 必须产生相同 HTML bytes。

## 6. Calibration Matrix

固定 seed matrix 至少覆盖四个独立 live axis：

| 轴 | 允许变化 | 必须不变 |
| --- | --- | --- |
| AttackControl | power attack direction/velocity/error envelope | rules、reach、soft touch |
| SoftTouch | tip/roll/push/rebound envelope | normal power aim、block reach |
| BlockTechnique | hand coverage/seal/deflection envelope | jump/reach、floor defense |
| CourtAwareness | delay/uncertainty/visible interpretation/support | movement、legality、trajectory artifact |

每个 case 记录 seed、profile fingerprint、configuration hash、plan/replay hash、
deterministic work units/degradation 和 wall-clock。前六项用于可重复断言，wall-clock
只进入测试输出和交付报告。

## 7. 验收

1. EditMode reflection/static scan：director 不再持有 decision planner、战术 candidate
   scoring 或 formal route/support selector；formal path 没有 duplicate writer。
2. EditMode：work-budget strict round-trip、canonical ordering、历史 bytes/hash、
   tamper rejection 与 inclusion/exclusion 表全绿。
3. EditMode：相同 Replay 两次渲染 HTML bytes 相等；双方 panel 只显示各自
   event-owned evidence，未出现隐藏 route/future sample。
4. PlayMode：standard、setter-first-contact、emergency organization、poor-set
   fallback、block rebound、tool recovery、25 分整局、Replay capture 全绿。
5. PlayMode：固定 seed 两次产生相同 replay hash、HTML 和 deterministic work evidence。
6. Calibration matrix：四个 live axis 只产生声明范围内 delta，work budget 不超界；
   wall-clock 单独报告。
7. 完整 EditMode、PlayMode、dependency/legacy writer scans 与 `git diff --check`
   全绿；手动双视角检查和 Windows x64 结果在 change record 中明确。
