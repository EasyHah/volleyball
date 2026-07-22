# 改动文档中心

本目录是 Match、Career 与共同模块共享的改动交接入口。所有代码、资源、场景、配置、
Shared 契约或模块编排改动，都必须在这里新增或更新一份改动文档，并在提交或 Pull Request
中附上链接。

架构说明与长期开发规范仍保留在 `docs/` 根目录；可以和一次具体实现、提交或 Pull Request
对应的改动记录、实现计划和设计稿统一保存在本目录。启用本流程之前的 Match 原型文档已
迁入 `archive/match-prototype/`，仅作为历史依据。

## 改动索引

| 编号 | 日期 | 状态 | 影响模块 | 交互级别 | 改动 |
| --- | --- | --- | --- | --- | --- |
| CHG-20260722-007 | 2026-07-22 | 已完成 | Career.Application / Career.MatchIntegration / Shared.MatchV2 / Docs | **跨模块（重点）** | [Career Match V2 自有端口、映射与动态 fixture runner](2026-07-22-007-career-match-integration.md) |
| CHG-20260722-006 | 2026-07-22 | 已完成 | Shared.MatchV2 / Career.MatchIntegration / Match / Docs | **跨模块（重点）** | [Shared Match V2 规范契约、golden fixture 与复核证据加固](2026-07-22-006-shared-match-v2-contract.md) |
| CHG-20260722-005 | 2026-07-22 | 已完成 | Career.Application / Persistence integration tests / Docs | 模块内部 | [第一周事件选择、真实持久化赛前管线与八轴应用证明](2026-07-22-005-career-week-event-pipeline.md) |
| CHG-20260722-004 | 2026-07-22 | 已完成 | Career.Application / Tests / Docs | 模块内部 | [第一周普通行动执行与固定事件冻结（含 restore/RNG/frontier 复核加固）](2026-07-22-004-career-week-action-execution.md) |
| CHG-20260722-003 | 2026-07-22 | 已完成 | Career.Domain / Application / Tests / Docs | 模块内部 | [第一周计划原子确认（含独立复核边界加固）](2026-07-22-003-career-week-plan-confirmation.md) |
| CHG-20260722-002 | 2026-07-22 | 已完成 | Career.Domain / Application / Persistence / Tests / Docs | 模块内部 | [第一周领域与持久化基础](2026-07-22-002-career-week-domain-foundation.md) |
| CHG-20260722-001 | 2026-07-22 | 已完成 | Career.Domain / Application / Persistence / Tests / Docs | 模块内部 | [实现生涯球员创建与大学试训](2026-07-22-001-career-player-tryout.md) |
| CHG-20260721-004 | 2026-07-21 | 已完成 | Career.Application / Persistence / Tests / Docs | **跨模块（重点）** | [实现本地档案与生涯持久化](2026-07-21-004-career-local-persistence.md) |
| CHG-20260721-003 | 2026-07-21 | 已完成 | Career.Domain / Application / Persistence / Tests / Bootstrap | **跨模块（重点）** | [建立正式生涯快照模型](2026-07-21-003-career-save-model.md) |
| CHG-20260721-002 | 2026-07-21 | 已完成 | Career.Domain / Career.EditModeTests | 模块内部 | [对齐球员生涯周行动计划领域规则](2026-07-21-002-career-week-plan-alignment.md) |
| CHG-20260721-001 | 2026-07-21 | 已完成 | Repository / CI / Docs / Shared / Match | **跨模块（重点）** | [首里程碑单负责人协作与冻结基线](2026-07-21-001-solo-integration-and-collaboration-baseline.md) |
| CHG-20260720-001 | 2026-07-20 | 已完成 | Project Settings / Packages / Docs / Build | **跨模块（重点）** | [升级并锁定 Unity 6000.3.20f1](2026-07-20-001-unity-6000.3.20f1-upgrade.md) |
| CHG-20260719-005 | 2026-07-19 | 已完成 | Match / Docs / Build Settings | 模块内部 | [正式室内 6v6 单局](2026-07-19-005-formal-indoor-6v6.md) |
| CHG-20260719-004 | 2026-07-19 | 已完成 | Match / Docs | 模块内部 | [3v3 半场边界、对局平衡与 AI 等待减速](2026-07-19-004-rally-boundaries-ai-time.md) |
| CHG-20260719-003 | 2026-07-19 | 已完成 | Match / Docs | 模块内部 | [Physical3v3Rally 拦网命中反馈](2026-07-19-003-physical-block-impact-feedback.md) |
| CHG-20260719-002 | 2026-07-19 | 已完成 | Match / Docs | 模块内部 | [Physical3v3Rally 统一多角色回合决策](2026-07-19-002-unified-multi-role-rally-decision.md) |
| CHG-20260719-001 | 2026-07-19 | 已完成 | Match / Docs | 模块内部 | [Physical3v3Rally 防守候选触球池](2026-07-19-001-defensive-contact-candidates.md) |
| CHG-20260718-007 | 2026-07-18 | 已完成 | Match / Docs | 模块内部 | [Physical3v3Rally 可见拦网与下撤覆盖](2026-07-18-007-visible-block-coverage.md) |
| CHG-20260718-006 | 2026-07-18 | 已完成 | Match / Docs | 模块内部 | [MenShen 排球决策基准](2026-07-18-006-menshen-decision-benchmark.md) |
| CHG-20260717-006 | 2026-07-17 | 已完成 | Career / Shared / Match / Bootstrap / Project Settings / Docs | **跨模块（重点）** | [球员生涯首个技术里程碑实施基线](2026-07-17-006-career-development-baseline.md) |
| CHG-20260717-005 | 2026-07-17 | 已完成 | Match / Shared / Career | **跨模块（重点）** | [Physical3v3Rally 可结束单局与结果产出](2026-07-17-005-match-set-completion.md) |
| CHG-20260717-004 | 2026-07-17 | 已完成 | Match / Career / Shared / Bootstrap / Docs / Build Settings | **跨模块（重点）** | [Volleyball 全项目命名统一](2026-07-17-004-volleyball-naming-unification.md) |
| CHG-20260717-003 | 2026-07-17 | 已完成 | Docs | 协作流程（重点） | [建立统一改动文档流程](2026-07-17-003-change-document-workflow.md) |
| CHG-20260717-002 | 2026-07-17 | 已完成 | Match / Career / Bootstrap | **跨模块（重点）** | [Unity 模块目录与程序集迁移](2026-07-17-002-unity-module-separation.md) |
| CHG-20260717-001 | 2026-07-17 | 已完成 | Shared / Match / Career | **跨模块（重点）** | [Shared 比赛与生涯契约边界](2026-07-17-001-shared-contract-boundary.md) |

当前跨模块实施路线：[比赛与生涯模块统一 Unity 仓库规划](unified-unity-modules-plan.md)。球员生涯首个
技术里程碑见[开发路线图](../career-development-roadmap.md)。

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

满足以下任一条件，就属于“跨模块交互”的改动：

- 修改 `Shared` 中的 DTO、ID、枚举、序列化、版本号、校验或测试夹具。
- 修改 MatchContext、MatchResult 或比赛前后数据流。
- 修改 Match、Career 或 Bootstrap 之间的 asmdef 引用和公开接口。
- 修改由另一模块加载的场景、资源路径、存档字段或启动流程。
- 要求另一模块同步改代码、改数据、重新生成资源或执行迁移。

这类文档必须同时做到：

- 索引的“交互级别”写成 `跨模块（重点）`。
- 正文紧接元数据放置 `> [!IMPORTANT]` 区块。
- 明确写出接口提供方、使用方、兼容性证据，以及消费方或后续负责人需要做什么。
- 如果没有额外动作，也必须明确写“无需改代码”，不能留空。

模块内部改动的“跨模块交互重点”章节填写“无”，方便对方快速判断是否需要阅读。
