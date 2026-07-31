# Career x Match V5-A: 实施计划

- 前置规格：`docs/superpowers/specs/2026-07-30-career-match-v5-attribute-contract-design.md`
- 前置任务单：`docs/handoffs/active/2026-07-30-career-match-v5-attribute-contract-design.md`
- 范围：V5-A；不包含 `CareerMatchReportV1`、快速模拟追踪或 V5-B 结算。
- 开始条件：用户确认本计划后，从最新 `origin/main` 建立新的 V5 里程碑分支；工作树必须干净。

## 完成定义

新的 Career 正式比赛冻结并执行独立的 V5 context，使用 Career 的十二项有效基础属性，经过 Shared
确定性推导后由 Formal 6v6 消费，并返回与 context 绑定的 V5 result/replay。V4 DTO 和旧物理 runner
路径不修改；任何读到的 V4 pending context、V4 match result 或 V4 replay 都被显式拒绝，并向用户提供
放弃旧 pending、重新创建 V5 对局的可恢复路径。V5-A 不结算成长、信任、疲劳、心态或伤病。

## 阶段 0：冻结基线与契约切换策略

1. 创建 `codex/career-match-v5-a`（或该里程碑约定名称）并记录 `origin/main` 基线。
2. 对现有 Shared V4 contract、Career pending/settlement 生命周期和 Formal scene startup 运行 focused
   回归，确认 V4 基线为绿。
3. 制定并测试存档读取 gate：检测 V4 pending 或 V4 match artifacts 时，不解析为 V5，也不尝试迁移；
   返回可展示的拒绝原因和唯一恢复操作（放弃 pending 后重新创建）。

若基线不绿或不能给 V4 旧数据提供可恢复的拒绝路径，停止合同实现并记录阻塞。

## 阶段 1：Shared V5 纯合同与推导

1. 新增 V5 专用基础属性、球员/队伍 snapshot、派生属性、解释、context、result、replay 和 canonical
   JSON/hash 类型。不得编辑既有 V4 DTO 或序列化表面。
2. 验证十一项 BP 属性为 `[0,10000]`、身高为 `[1400,2300]` mm；非法值、未知字段、错误版本、非规范
   hash 和 supplied-derived mismatch 必须拒绝，不得 clamp 或填默认值。
3. 实现纯 `MatchAttributeDerivationV5`：只接受 Career 冻结后的 bases、惯用手、版本和冻结配置；输出
   derived values 和 replay explanation。Match 不读取 bases、位置模板或培养方向。
4. 添加 Shared EditMode 测试：每属性单变量低/高向量、边界/非法值、版本 identity、canonical bytes、
   hash 绑定和篡改拒绝。

阶段完成条件：V5 纯合同可独立 round-trip，固定输入产生字节稳定产物，且无 Shared -> Career/Match 引用。

## 阶段 2：Career V5 冻结与存档恢复边界

1. 将 Career 的当前属性模型映射到 V5 十二项 bases；在 context 冻结前精确应用一次 fatigue，禁止 Match
   侧二次折算。
2. 建立 V5 pending lifecycle 与其 schema/version gate；V4 pending 只能走废弃恢复路径，不得继续进入 V4
   执行或 V5 settlement。
3. V5-A 仅记录已执行的 V5 result/replay 绑定，不调用 V4 的统计估算或成长/疲劳/信任结算。将 V5-B
   report 作为明确不支持的状态处理。
4. 添加 Career focused EditMode：十二项映射单调、疲劳单次应用、V4 旧 pending 拒绝/放弃、幂等创建与
   V5 context bytes 保持。

阶段完成条件：Career 新比赛不再产生 V4 context；旧数据不被静默转换或丢失。

## 阶段 3：Formal 6v6 V5 运行时接线

1. 新增 V5 一次性 scene startup 和 runner；由 Bootstrap 在 additive Formal 场景加载 V5 context，捕获相同
   session/context 绑定的 V5 result/replay，并在取消和故障时清理 startup state、场景和 UI。
2. 将 Formal 6v6 所有属性消费者转为 V5 derived values；逐项验证攻击、拦网、防守/接发、二传、发球的
   指定 consumers 均没有回读 base、位置数值覆盖或隐藏能力来源。
3. Replay 写入 V5 attribute explanation 和 V5 运行时消费证据；固定 context/configuration/seed 必须得到
   byte-identical result/replay。
4. 保留 V4 formal startup 只为 V4 生命周期恢复；fixture 永远不能产生 V5 正式报告或伪造 V5 result。

阶段完成条件：一条 Career -> Formal 6v6 -> V5 Result/Replay 路径可完成，并在成功、取消和错误场景中
   保持绑定与清理不变量。

## 阶段 4：验证、回归与交接

按顺序执行：

1. Shared、Career、Match 的新增 focused EditMode；
2. V5 Formal runner focused PlayMode（至少成功、取消、启动失败与固定种子重放）；
3. 所有受影响模块 EditMode；
4. 一次完整 EditMode 和必要的 PlayMode；
5. 因场景、启动和 Player 行为受影响，执行 Windows Development IL2CPP build 及存档拒绝/恢复验证；
6. 一次独立全面复核，修复高优先级问题后只重跑相关验证；
7. `git diff --check`、程序集引用扫描、变更记录和 handoff 状态更新。

## 提交边界

1. `test: specify V5 contract vectors`
2. `feat: add Shared Match V5 contracts`
3. `feat: freeze Career V5 match contexts`
4. `feat: run formal 6v6 with V5 contexts`
5. `test: verify Career Match V5 lifecycle`

Shared、Bootstrap 和公共契约的改动必须以 Pull Request 合并；不得合并到 `main`，直到所有验收完成。
