# CHG-20260717-006：球员生涯首个技术里程碑实施基线

- 日期：2026-07-17
- 最近修订：2026-07-20
- 状态：进行中
- 负责人：多方（Career 主责，Match/Shared 联合评审）
- 影响模块：Career / Shared / Match / Bootstrap / Project Settings / Docs
- 交互级别：跨模块（重点）
- 关联分支：`docs/career-development-roadmap`
- 关联提交或 PR：本文件所在提交/PR；文档分支已变基到 `origin/main@4bf9e4b`

> [!IMPORTANT]
> 本次只建立实施基线，不修改运行时代码或现有 Shared 契约。后续 Shared 契约门禁将由双方在独立
> 分支共同批准；Match 负责人需要确认输入输出统计、fixture、版本和职责边界，Unity 版本升级也必须
> 先独立完成并由双方复核。当前 Career 原型仍耦合 Shared V1，周计划分支也仍包含已移除的
> `Academics/Social` 行动；文档已把二者列为开工前必须消除的已知偏差，不能按现状直接合并。搭档新增
> 的 `FormalIndoor6v6` 已是可完局的 Match 生产者基线，但没有升级 Shared 或实现 Career 场景生命周期，
> 因此不能据此跳过契约门禁。

## 改动摘要

为离线优先的球员职业生涯建立首个一周技术闭环路线：本地多档案/多生涯、大学试训、第一周三个
行动槽、一个固定社交事件、赛前重点、FakeMatch、原子结算、退出恢复以及进入第二周 Planning。

本次文档同时固定开发顺序、首里程碑交互、领域规则、确定性随机、正式存档及故障恢复约束。它不
实现真实比赛、在线登录、长期六赛季内容、伤病、学业系统、合同转会或最终平衡，也不提前修改
Shared DTO。

2026-07-18 根据独立复审补充：周计划分支需求对齐、Career 自有八项属性、明确 asmdef 依赖、异步比赛
端口、训练 emphasis 聚合、试训结果页导航语义、字节级随机/JSON 规范、Windows 替换异常重扫，以及
协作基础设施前置门禁。删除和设置协议移出首个里程碑。二次 agent 复核又收敛了 Shared 异步 runner
接线阶段、团队合练不生成无事实来源的 emphasis、三类随机流实际调用向量、拒绝采样整数边界，以及
普通替换与玩家确认备份恢复的独立裁决和 recovery intent。

2026-07-20 同步搭档 `main@4bf9e4b`：确认现有 3v3/6v6 共用物理 Director，6v6 可完成 25 分单局并
产出 12 人 V1 统计；首个 Career fixture 相应调整为双方各六人。同时保留 FakeMatch 优先顺序，因为
V1 仍缺详细技术事实、`resultHash`、异步 runner 和 `PendingMatch` 场景往返。现有物理 6v6 接入列为
首里程碑完成后的独立阶段；同时记录能力仍被位置模板覆盖、全 AI 运行以及上下文 seed 尚未进入 AI
决策的接入缺口。MenShen 客户端与凭据继续限定为 Editor 工具边界；全局 Newtonsoft 包程序集当前仍会
进入 Mono Player，这一包边界债务不成为 Career 的运行时依赖。

## 具体变更

- 文件、场景、资源或程序集：新增
  `docs/career-development-roadmap.md`、`docs/career-vertical-slice-spec.md`、
  `docs/career-domain-rules.md` 和 `docs/career-save-and-recovery.md`；同步更新
  `docs/architecture.md`、`docs/changes/unified-unity-modules-plan.md` 与正式 6v6 变更记录中的当前 Match
  状态及已知接入限制。
- 新增或修改的公开类型/字段：无运行时变更；文档规定未来的版本化存档、`PendingMatch`、
  `SettlementReceipt`、Career 自有 `CareerPlayerAttributes`、异步比赛端口与新版比赛上下文/结果需求。
- 行为变化：无；实现必须按路线中的阶段门禁和验收证据逐步落地。

## 跨模块交互重点

- 接口提供方：Career 提供赛前意图和长期状态；Match 提供直接比赛/快速模拟产生的事实；Shared
  提供双方共同批准的版本化契约。
- 接口使用方：Career / Match / Bootstrap。
- 数据方向：`Career -> Shared -> Match -> Shared -> Career`。
- 兼容性：本次文档兼容现有代码且不改 V1。未来 Shared 升级必须新增或明确废弃版本，不得在各自
  功能分支私自改变字段语义；比赛生命周期字段通过显式存档 schema 升级加入。
- 对方开发者需要做什么：评审 Unity `6000.3.20f1` 升级基线、Shared 契约门禁所需事实、计数口径、
  3v3 兼容与 6v6/12 人 golden fixtures、异步场景生命周期、职责分界、asmdef 表和公共目录所有权；
  本次文档合并无需改 Match 代码，后续 Shared/MatchIntegration 与物理 6v6 接入分支需要双方共同批准。

## 验证

- [ ] EditMode 测试：纯文档改动，不适用；实际阶段按路线门禁分别记录。
- [ ] PlayMode 测试：纯文档改动，不适用；UI 垂直切片阶段必须补齐。
- [ ] 手动场景验证：纯文档改动，不适用；最终 Windows 开发构建由双方游玩验证。
- [x] 序列化、存档或迁移验证（设计层）：已规定版本轴、CAS/原子替换、单 `.bak`、隔离恢复、
  确定性随机与幂等回执的最低验收矩阵；代码证据尚待实现。
- [x] 文档一致性初检：四份文档统一首里程碑范围、阶段依赖、术语和非目标。
- [x] 三路 agent 交叉复核：已修正比赛/周末事务边界、阶段依赖、能力定点数值、试训与事件恢复、
  备份 lineage、临时文件提交点和档案索引并发问题；定点回归无阻断项。
- [x] 2026-07-18 独立复审整改：已核对周计划分支、Career/Shared V1 耦合、Windows ReplaceFileW
  部分失败语义、恢复 intent、训练聚合、试训确认、三类 RNG 调用向量、规范 JSON 以及缺失的协作
  基础设施门禁；二次三路 agent 定点复核已无 P1 阻断项。
- [x] 2026-07-20 上游同步复核：文档分支已变基到 `main@4bf9e4b`，核对 3v3/6v6 实现、`224/224`
  EditMode、`11/11` PlayMode 上游记录、12 人 V1 结果、Build Settings、Newtonsoft 包锁与仍未改变的
  Shared/Career/Bootstrap 边界；本次未把上游历史测试冒充为目标 Unity/Windows 新验证。
- [ ] Career/Match 双方人工评审与批准。

没有运行 Unity 测试，因为该分支只新增 Markdown。当前工程仍为 Unity `6000.0.43f1`；目标版本
`6000.3.20f1` 必须在独立升级分支完成编译、EditMode、PlayMode 和 Windows x64 构建验证。

## 回滚与风险

主要风险是将路线中的未来设计误认为已经实现，或在 Shared 门禁前按猜测字段开发比赛恢复。文档使用
明确状态和阶段进入/退出条件降低该风险。回滚只需还原本次文档提交，不影响运行时项目；若实施中发现
需求改变，应更新同一基线和本变更记录，不创建相互矛盾的平行规范。

主线现已使用 `CHG-20260717-005` 记录 Match 完局功能；尚未合并的 `feature/career-week-plan` 仍错误
复用该编号。该分支变基时必须把文件、标题与索引统一重编号为当时尚未使用的唯一编号，不能仅在文字
中“预留”未落盘编号，也不能在索引中保留两个语义不同的 `005`。
