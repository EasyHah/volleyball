# 训练室阵容编辑、严格位置错误与 V5 接线实现

- Status: active
- Owner: Career / Match collaborator
- Priority: P1
- Scope: TrainingLab / Match / Shared / Career / Bootstrap / Replay / 场景 UI / Windows 验证
- Supersedes:
  `docs/handoffs/completed/2026-08-01-hud-simplification-setter-review-entry.md`

## 当前要求

依照已确认的 UI 设计和实施计划，实现仅从发球前开始的训练室阵容工作台，以及 TrainingLab 与原生 V5
正式比赛共用的严格排球位置错误判定。轮转位次先编辑并确认锁定，之后才能拖拽实际站位；位置错误在发球
触球瞬间裁决，违规队立即失回合、对手得一分，且不得启动正常物理、AI 或触球链路。

TrainingLab 管理员覆盖仅作用于本训练情景的 V5 Match 测试数值能力、身高与惯用手。训练室还须提供受限制的
发球球位置/速度设置、自由镜头书签、持续回合监视器及分页面工作台。正式 V5 result/replay 必须记录与
context/result/replay hash 绑定的可验证位置错误事实；Career 仅消费 Match 结论而不重算位置。TrainingLab
运行复用原生 V5 authority/AI/物理/裁判，但只生成训练专用单回合结果，不生成正式 V5 result/replay。

## 已授权边界

- Match 域纯规则、正式 V5 发球接触路径、TrainingLab V5 数据/UI/场景接线、训练专用单回合结果和原生
  V5 训练启动边界。
- 已有 Shared V5 context/result/replay 合同、ContractJson、Career V5 恢复/消费边界只允许为既有正式
  V5 位置错误证据做缺陷修复；TrainingLab 新数据不得扩展或进入这些合同。
- 相关程序集、测试、Windows x64 IL2CPP Development 构建与 Player 验收记录。

## 非目标

- V4 Career、正式 V4、3v3 兼容场景的规则、存档、结果、Replay、恢复和行为完全不变。
- 不允许任意回合中插球；发球球只能位于当前发球方己方底线后。
- 训练室覆盖、镜头、启动快照、训练结果或编辑器审核数据不得流入 Career、V5 正式 context/result/replay
  或正式比赛。
- 不提供位置错误容差或绕过判罚的用户开关；相等投影合法，只有严格反转违法。
- 不编辑姓名、号码、注册职业或 Career 权威属性。

## V5 版本与恢复决策

本阶段引入新的必填 V5 位置错误证据版本，并同时更新 context/result/replay 的规范 JSON、hash 和验证。
旧版本的 V5 pending/result/replay 绝不以默认字段或可选兼容字段静默读取；恢复时必须以明确、可恢复的
“不支持的 V5 证据版本，请放弃此待处理比赛并新建比赛”路径拒绝，保留原始工件供诊断。回滚方式是回退
本阶段分支；回退后的程序同样拒绝该新版本，而不是试图降级解释。

## 统一 TrainingLab / V5 决策（2026-08-17）

- `docs/superpowers/specs/2026-08-08-training-lab-unified-workbench-design.md` 是 TrainingLab UX、数据边界和
  运行语义的权威设计；对应 unified implementation plan 是当前唯一可执行 TrainingLab 计划。
- 世界原点为场中心。Home 队伍局部 `(lateral, depth)` 映射为世界 `(lateral, -depth)`，Away 映射为
  `(-lateral, depth)`；等价阵型关于场中心旋转 180 度，不关于球网镜像。默认摆位、编辑投影、修正箭头和
  发球触球位置错误判定必须共用该变换。
- 摆位只在俯视图编辑。发球俯视编辑 X/Z 与 VX/VZ，侧视编辑 Z/Y 与 VZ/VY，两者同步一份六轴状态；
  3D 预览只允许观察和镜头书签，不得修改球员、球或速度。
- 进入预检时自动验证并冻结唯一 `MatchSetupSnapshotV1`；不保留独立 `校验` 按钮或第二套 UI 校验状态。
- 管理员覆盖使用 V5 的 Strength、Height、Jump、Movement、Reaction、Coordination、Attack、Defense、
  Court IQ、Block、Serve、Set 与 `DominantHandV5`。覆盖必须实际影响训练运行时能力、身高/接触几何和
  惯用手接触选择，并支持单字段清除与单球员恢复。
- TrainingLab 从 V4 context、属性推导、startup、eligibility 和 evidence 路径迁移到原生 V5 回合核心。
  所有 TrainingLab 运行统一在一个回合结束后停止并产生 `TrainingRallyOutcomeV1`；不等待完整局、不生成
  `MatchResultV5`、`MatchReplayV5` 或 Career report。
- 内置 V1/V4 TrainingLab 资产重建为 V2/V5 格式；本地 V1 文件显式显示为不支持并保留原字节，禁止
  静默转换或默认字段读取。

## 验收条件

- TrainingLab 完成轮转锁定、场中心对称站位、角色/位次标识、俯视/侧视发球编辑、只读 3D、镜头书签、
  V5 管理员覆盖、自动预检、原生 V5 单回合和运行时位置错误闭环；整页必须按已确认设计稿重构，不能以替换中间场地而保留旧右侧
  堆叠表单作为完成标准。右栏必须是按当前步骤变化、限高可滚动的上下文检查器与属性面板。
- 训练室生产路径不再引用 V4 context、属性、startup、eligibility 或 evidence；V4 Career/正式/3v3 回归
  保持不变。
- 纯规则覆盖 Home/Away、相等合法、每条前后/左右关系、并发错误、输入拒绝及稳定排序。
- V5 仅在发球触球前裁决，违规回合零触球且只计一次对手得分；result/replay/context 证据可确定性绑定，
  Career 不重算。
- V4 回归保持不变；Shared/Career/Match 受影响测试、完整 EditMode、必要 PlayMode、独立复核通过。
- 完成 Windows x64 IL2CPP Development Build 与 Windows Player 人工验收，或如环境不可用则如实保持待验收，
  不声明任务完成。

## 依赖资料

- `AGENTS.md`
- `docs/superpowers/specs/2026-08-08-training-lab-unified-workbench-design.md`
- `docs/superpowers/plans/2026-08-08-training-lab-unified-workbench-implementation-plan.md`
- `docs/superpowers/specs/2026-08-01-training-lineup-position-fault-v5-ui-design.md`
- `docs/superpowers/plans/2026-08-01-training-lineup-position-fault-v5-implementation-plan.md`
- `docs/handoffs/deferred/2026-08-01-career-match-v5-windows-validation.md`

## 结束处理

完成后将本文件移入 `docs/handoffs/completed/`，更新 Status、结束日期、每类验证证据、Windows 状态和
回滚/恢复结果；不得删除。

## 执行记录（2026-08-04）

- 已将 `origin/main`（含 `9a3897e` Windows IL2CPP 验证记录）合入
  `codex/training-lineup-position-fault-v5`。
- 已完成并验证 Task 1--8 主体实现：严格位置错误纯规则、训练发球冻结、TrainingLab 运行时/UI、V5
  result/replay 证据、Career V5 边界与正式发球接触裁决。
- 额外修复版本中立能力投影回归：V4 的 Receive/Set/Defense movement 与 defense reaction 字段保持
  原语义；V5 正式 Gate I 使用 `MatchAbilitySnapshot`，不读取空的 V4 derived DTO。
- 独立高风险复核发现并已修复两项 V5 证据契约问题：位置错误事实现在严格绑定违规队的 context 轮转
  槽位，Career 聚合拒绝 result/replay 的不一致事实；另将 V5 PlayMode 的全局时钟恢复改为 `finally`。
- 新鲜自动验证：完整 EditMode `1573/1573`；V5 契约与 Career 聚合测试 `50/50`；V5 runner PlayMode
  `1/1`；TrainingLab 运行时 PlayMode `4/4`；`git diff --check` 通过。
- 同批目标 PlayMode 的 V4 非确定性回归未全绿：22 项中 20 项通过，`Runner_CompletesPhysicalSixVsSixWithContextBoundResultAndReplay`
  在预算内未结束，`Formal6v6_CalibratedToolRecovery_UsesPhysicalBlockAndNonAttackerSave` 首攻选为 `Tip`
  而非 `BlockToolRecovery`。两项均不在本次 V5 契约改动路径，须在最终自动验收前单独稳定复现并处理。
- 当前仍待：上述 V4 回归调查、1920x1080 macOS Editor 人工闭环，以及本计划新增 TrainingLab 的 Windows
  x64 IL2CPP Player 验收。handoff 保持 `Status: active`，不得宣称阶段完成。

## 设计实现偏差（2026-08-07）

- 当前 2D 场地投影、站位门禁和发球带已接入，但右侧仍沿用旧版全量堆叠表单，未实现设计稿中的分步
  上下文检查器、信息层级和限高滚动结构。因此该 UI 只能标记为“交互底层已实现”，不得标记为设计稿
  已实现或进入视觉验收。
- 后续 UI 工作必须先重构整页信息架构：中间栏只承载主编辑画布；右栏只显示当前步骤所需的状态、错误、
  精确输入和下一步动作；跨步骤的低频设置收纳，不得重新堆回右侧。完成后以 1920x1080 实际截图逐项
  对照设计稿验收，确认无重叠、无遮挡、无溢出后，才可继续扩展 3D/侧视/轨迹等交互。

## 统一工作台实施检查点（2026-08-17）

- unified implementation plan Task 0--9 代码已实现：V2/V5 数据边界、本地持久化、分步工作台、队伍局部
  坐标、V5 覆盖、自动 preflight、原生 V5 单 rally 与 `TrainingRallyOutcomeV1` 已接通；V1 本地资产按
  稳定诊断拒绝并保留原字节。
- 独立复核收口后的新鲜门禁：TrainingLab focused EditMode `76/76`、TrainingLab PlayMode `3/3`、
  完整 EditMode `1624/1624`、V5 Career PlayMode `8/8`、3v3 隔离复跑 `1/1`。证据为
  `TrainingLab-Unified-Final-EditMode-4.xml`、`TrainingLab-Unified-Final-PlayMode-4.xml`、
  `TrainingLab-Unified-Complete-EditMode-4.xml`、`TrainingLab-Unified-V5-Career-PlayMode-3.xml` 与
  `TrainingLab-Unified-3v3-PlayMode-2.xml`；`git diff --check` 通过。
- 独立复核发现的 stale preflight、恢复数据边界、V2 模板实际加载、俯视/侧视轨迹与精确编辑、只读 3D
  书签均已修复并纳入上述 focused 门禁。Unity `-nographics` 对离屏 Camera 的原生崩溃通过仅在 Null
  graphics device 下禁用实际绘制解决，GUI/Player 路径不受影响。
- V2 场景、工作台、controller 和 runtime 的生产图不含旧 V1/V4 startup/evidence。旧 V1 runtime 类型
  仅保留为隔离的直接兼容测试夹具，不由 V2 场景或本地目录进入；公共位置错误 API 的默认 frame 恢复为
  legacy，V5 Match setup/director 显式使用 team-local point-symmetric frame。
- macOS Editor 已验收情景库、轮转、摆位、位置错误聚焦、发球俯视/侧视及只读 3D。只读 3D 使用独立
  RenderTexture，显示球场、球网、12 名球员与发球球，不向 Match setup 写回。现有人工截图为编辑器
  窗口 `1338x745`；`1920x1080` 由 PlayMode 布局门禁覆盖，但仍需按计划补足同分辨率人工截图证据。
- Windows build 已真实执行并因当前 Unity 安装仅含 `MacStandaloneSupport` 而失败为 unsupported target；
  仍需在安装 Windows Build Support (IL2CPP) 的环境完成 x64 Development Build 与实体 Player 验收。
- macOS 锁屏后已通过正常退出 Unity 解除工程锁并完成自动回归；自动预检、单 rally 结果和 dirty-leave
  的 08--10 截图仍未补齐。handoff 因 Windows 与剩余人工验收保持 `Status: active`，当前状态为
  “已自动验证 / 待人工验收”，不得称完成。

## 交互验收纠偏与重开（2026-08-18）

- 用户验收确认 2026-08-17 的 controller/结构测试不能证明 Rotation 与 Positioning 可用；此前“Task
  0--9 已实现”和“macOS Editor 已验收轮转/摆位/位置错误聚焦”的表述对 Task 3--5 无效。Task 0--2、
  6--8 的数据与运行边界保持冻结，Task 3--5 和 Task 9 重新打开。
- 已确认的 P1 根因：`ShowWorkbench(entryKey)` 忽略 entry identity，导致“从标准轮转开始”可能复用先前
  的位置错误模板或旧草稿；selected-player ruler points 与 pointer wiring 不存在；fault cards、关系线和
  `FocusedPlayerIds` 的 View 接线缺失。Rotation 控制器交换正确，但没有真实 UI pointer 验收，页面也未按
  正式球场固定点位呈现完整身份。
- 唯一 unified implementation plan 新增 R0--R4 corrective increment，下一步从 R0 failing tests 开始。
  完成条件包括：普通模板默认零 fault、模板/本地 identity 不复用、真实轮转/球员/标尺 Pointer 手势、
  可理解且可修正的位置错误、按钮实际进入 Serve、全新 1920x1080 人工证据，以及代码冻结后的既有 Task
  9 回归。
- 当前状态回退为“修复计划已批准范围内完善 / 自动与人工验收均待重跑”。不得引用 2026-08-17 的
  `76/76`、`3/3` 或 `1624/1624` 结果证明 R0--R4，也不得在 R0--R4 完成前重新宣称已自动验证。

## 交互纠偏自动验证检查点（2026-08-20）

- R0--R3 已实现：六个内置模板逐一验证 identity、默认合法性和独立草稿；所有公开入口按
  `builtin:`/`local:` 身份替换 controller/runtime；轮转使用正式六点位卡片与同队交换；站位页默认选择
  Home 1 号位，提供两轴标尺、故障卡、关系线、同源纠正箭头及可实际点击的 Serve 推进按钮。
- 新鲜 focused 结果为 EditMode `46/46` 与 PlayMode `9/9`：
  `TestResults/TrainingLab-Corrective-ReviewFix-EditMode.xml`、
  `TestResults/TrainingLab-Corrective-ReviewFix-PlayMode-2.xml`。PlayMode 覆盖故障模板返回后点击标准入口、
  普通模板互换、连续两次标准入口、已保存 local 重载、captured-pointer 同队/跨队/空白 drop、轮转重开、
  站位 court/ruler drag、故障聚焦以及纠正后通过真实页面按钮进入 Serve。
- 代码冻结后的完整 EditMode 为 `1625/1625`：
  `TestResults/TrainingLab-Corrective-Complete-EditMode-2.xml`。冻结前已通过 V5 Career PlayMode `8/8`
  和 3v3 隔离 PlayMode `1/1`；纠偏末次改动仅位于 TrainingLab View/PlayMode 测试，并由上述 focused
  编译与交互测试覆盖。
- 独立只读复核发现的纠正方向文案与 pointer-capture 测试真实性问题已修复；故障卡方向现由蓝色箭头
  使用的同一 `ShortestLegalCorrection` 向量生成，深度和横向两类均有断言。复核未发现 V4 Career、
  正式 Match 或 3v3 语义变化。
- 1920x1080 Game View 中已人工确认标准模板零 fault、故障卡/聚焦/箭头、精确字段纠正和页面按钮进入
  Serve；截图位于 `TestResults/TrainingLab/VisualAcceptance/2026-08-18-corrective/`。但真实物理鼠标
  rotation/court/ruler drag、精确 1920x1080 像素截图、Preflight/Run/dirty-leave 补图仍未形成完整证据。
- Windows x64 IL2CPP build 再次真实执行并因本机仅有 `MacStandaloneSupport` 报 unsupported target，
  日志为 `TestResults/TrainingLab-Corrective-Windows-Build.log`。因此 handoff 保持 `Status: active`，
  当前状态为“已自动验证 / 待人工验收”，不得称完成。
