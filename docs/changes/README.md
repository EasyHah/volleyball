# 改动文档中心

本目录是 Match 与 Career 两边开发者共享的改动交接入口。所有代码、资源、场景、配置、
Shared 契约或模块编排改动，都必须在这里新增或更新一份改动文档，并在提交或 Pull Request
中附上链接。

架构说明与长期开发规范仍保留在 `docs/` 根目录；可以和一次具体实现、提交或 Pull Request
对应的改动记录、实现计划和设计稿统一保存在本目录。启用本流程之前的 Match 原型文档已
迁入 `archive/match-prototype/`，仅作为历史依据。

## 改动索引

| 编号 | 日期 | 状态 | 影响模块 | 交互级别 | 改动 |
| --- | --- | --- | --- | --- | --- |
| CHG-20260726-002 | 2026-07-26 | 进行中 | Shared / Match / Replay / Docs | **跨模块（重点）** | [Full Rally V4 Gate H 接发与组织权威](2026-07-26-002-full-rally-v4-gate-h-receive-organization-authority.md) |
| CHG-20260726-001 | 2026-07-26 | 已完成 | Match / Docs | 模块内部 | [Full Rally V4 Gate G player component boundaries](2026-07-26-001-full-rally-v4-gate-g-player-component-boundaries.md) |
| CHG-20260725-001 | 2026-07-26 | 已完成 | Shared / Match / Replay / Docs | **跨模块（重点）** | [Full Rally V4 Gate F 十二人责任计划 Shadow](2026-07-25-001-full-rally-v4-gate-f-shadow-plans.md) |
| CHG-20260724-002 | 2026-07-24 | 已完成 | Career / Shared / Match / Replay / Docs | **跨模块（重点）** | [Full Rally V4 Gates A–E contract cut](2026-07-24-full-rally-v4-gates-a-e.md) |
| CHG-20260724-001 | 2026-07-24 | 进行中 | Match / Docs | 模块内部 | [Stage 2 能力投影、共享执行 envelope 与物理预测 cache](2026-07-24-001-stage2-ability-envelope-and-prediction.md) |
| CHG-20260723-001 | 2026-07-23 | 已完成 | Shared / Match / Career / Docs | **跨模块（重点）** | [Full Rally V3 架构与 Shared 契约预留](2026-07-23-001-full-rally-v3-architecture.md) |
| CHG-20260722-002 | 2026-07-22 | 已完成 | Match / Docs | 模块内部 | [几何进攻与拦网对抗](2026-07-22-002-geometric-attack-block-counterplay.md) |
| CHG-20260721-001 | 2026-07-21 | 已完成 | Shared / Match / Docs | **跨模块（重点）** | [统一进攻链 V2、50 分封顶与全手臂拦网](2026-07-21-001-unified-attack-chain.md) |
| CHG-20260720-001 | 2026-07-20 | 已完成 | Match / Docs | 模块内部 | [Match Replay V1 回放产物与 HTML 查看器](2026-07-20-001-match-replay-v1.md) |
| CHG-20260719-005 | 2026-07-19 | 已完成 | Match / Docs / Build Settings | 模块内部 | [正式室内 6v6 单局](2026-07-19-005-formal-indoor-6v6.md) |
| CHG-20260719-004 | 2026-07-19 | 已完成 | Match / Docs | 模块内部 | [3v3 半场边界、对局平衡与 AI 等待减速](2026-07-19-004-rally-boundaries-ai-time.md) |
| CHG-20260719-003 | 2026-07-19 | 已完成 | Match / Docs | 模块内部 | [Physical3v3Rally 拦网命中反馈](2026-07-19-003-physical-block-impact-feedback.md) |
| CHG-20260719-002 | 2026-07-19 | 已完成 | Match / Docs | 模块内部 | [Physical3v3Rally 统一多角色回合决策](2026-07-19-002-unified-multi-role-rally-decision.md) |
| CHG-20260719-001 | 2026-07-19 | 已完成 | Match / Docs | 模块内部 | [Physical3v3Rally 防守候选触球池](2026-07-19-001-defensive-contact-candidates.md) |
| CHG-20260718-007 | 2026-07-18 | 已完成 | Match / Docs | 模块内部 | [Physical3v3Rally 可见拦网与下撤覆盖](2026-07-18-007-visible-block-coverage.md) |
| CHG-20260718-006 | 2026-07-18 | 已完成 | Match / Docs | 模块内部 | [MenShen 排球决策基准](2026-07-18-006-menshen-decision-benchmark.md) |
| CHG-20260717-005 | 2026-07-17 | 已完成 | Match / Shared / Career | **跨模块（重点）** | [Physical3v3Rally 可结束单局与结果产出](2026-07-17-005-match-set-completion.md) |
| CHG-20260717-004 | 2026-07-17 | 已完成 | Match / Career / Shared / Bootstrap / Docs / Build Settings | **跨模块（重点）** | [Volleyball 全项目命名统一](2026-07-17-004-volleyball-naming-unification.md) |
| CHG-20260717-003 | 2026-07-17 | 已完成 | Docs | 协作流程（重点） | [建立统一改动文档流程](2026-07-17-003-change-document-workflow.md) |
| CHG-20260717-002 | 2026-07-17 | 已完成 | Match / Career / Bootstrap | **跨模块（重点）** | [Unity 模块目录与程序集迁移](2026-07-17-002-unity-module-separation.md) |
| CHG-20260717-001 | 2026-07-17 | 已完成 | Shared / Match / Career | **跨模块（重点）** | [Shared 比赛与生涯契约边界](2026-07-17-001-shared-contract-boundary.md) |

当前跨模块实施路线：[比赛与生涯模块统一 Unity 仓库规划](unified-unity-modules-plan.md)。

## 历史改动文档

以下文档早于统一模板，内容仅涉及 Match 原型，不包含 Career/Shared 交互要求：

- [AI 回合场景实现计划](archive/match-prototype/2026-07-15-all-ai-rally-scene.md)
- [物理接触回合升级计划](archive/match-prototype/2026-07-16-physics-contact-rally-upgrade.md)
- [AI 回合场景设计](archive/match-prototype/2026-07-15-ai-rally-scene-design.md)

## 新增规则

1. 从 [TEMPLATE.md](TEMPLATE.md) 复制新文档，文件名使用
   `YYYY-MM-DD-NNN-short-description.md`。
2. 编号使用 `CHG-YYYYMMDD-NNN`；同一天的 `NNN` 从 `001` 递增。
3. 开发开始时状态写“计划中”或“进行中”，合并前补齐实际文件、验证结果和提交/PR。
4. 新文档必须加入上方索引，最新记录放在最上方。
5. 一次改动可以持续更新同一文档，不要为同一个目的拆出互相矛盾的多份记录。

## 跨模块重点标注

满足以下任一条件，就属于“与对方交互”的改动：

- 修改 `Shared` 中的 DTO、ID、枚举、序列化、版本号、校验或测试夹具。
- 修改 MatchContext、MatchResult 或比赛前后数据流。
- 修改 Match、Career 或 Bootstrap 之间的 asmdef 引用和公开接口。
- 修改由另一模块加载的场景、资源路径、存档字段或启动流程。
- 要求另一边开发者同步改代码、改数据、重新生成资源或执行迁移。

这类文档必须同时做到：

- 索引的“交互级别”写成 `跨模块（重点）`。
- 正文紧接元数据放置 `> [!IMPORTANT]` 区块。
- 明确写出接口提供方、使用方、兼容性和“对方开发者需要做什么”。
- 如果没有额外动作，也必须明确写“无需改代码”，不能留空。

模块内部改动的“跨模块交互重点”章节填写“无”，方便对方快速判断是否需要阅读。
