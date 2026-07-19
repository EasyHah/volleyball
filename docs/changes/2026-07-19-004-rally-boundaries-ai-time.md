# CHG-20260719-004：3v3 半场边界、对局平衡与 AI 等待减速

- 日期：2026-07-19
- 状态：已完成
- 负责人：Match / Docs
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/blocking-roles`
- 关联提交或 PR：本变更提交（见分支 HEAD）

## 改动摘要

Physical3v3Rally 在连续回合中会沿用上一球结束时的球员根节点，助跑与触球对齐又没有统一的
己方半场约束，因而可能出现球员根节点穿过球网、阵型持续漂移和单方连续接触超时。此次为
六名球员增加球场与球网硬边界，每球按当前战术/轮转重新站位，并移除蓝方原有的 `+0.01`
能力偏置。固定种子完整单局由先前观测的 `15:2` 改善为 `15:10`，双方各完成 7 次 Attack。

同时新增可注入的异步战术权重来源。只有配置联网来源时，比赛才会在一次新球权开始时降低
全局 `Time.timeScale`；AI 响应、非法输出、异常或实时超时后均恢复原速。未配置来源时仍完全
使用原有本地确定性 AI，不增加等待或网络依赖。本次不把 Editor-only MenShen benchmark、
API 密钥或具体供应商客户端接入 Player 运行时。

## 具体变更

- 文件、场景、资源或程序集：
  - `PrototypePlayerAgent` 统一约束移动、助跑、拦网、支援、触球对齐与训练复位根节点：
    横向保留 `0.25m` 边界余量，球网两侧各保留 `0.45m` 根节点余量。
  - `ThreeVsThreeRallyDirector` 每球按当前战术和轮转复位六人，拦网目标使用同一球网余量，
    并记录蓝/橙双方实际 Attack 次数。
  - `ThreeVsThreeRallyBootstrap` 为相同位置的双方球员使用完全相同的能力档案。
  - 新增 Unity-free `IRallyTacticalWeightSource` 与 `RallyTacticalWeightRequest`；输出仍只能是
    `[0, 2]` 范围内的四项战术权重，不能绕过本地合法性、可达性、碰撞或判分。
  - 新增 `AiDecisionTimeController`。默认以 2 秒真实时间为截止，保留 0.18 秒模拟决策余量，
    同步缩放 `Time.fixedDeltaTime`，并以不受缩放影响的 0.18 秒过渡恢复原速。
  - `BlockImpactFeedback` 支持安全初始化已有场景组件；活动效果被禁用时立即终止并恢复球尾迹。
- 新增或修改的公开类型/字段：
  - `PrototypePlayerAgent.IsWithinOwnCourt`、`NetClearance`、`BoundaryClearance`。
  - `RallyTacticalWeightRequest`、`IRallyTacticalWeightSource`、
    `AiDecisionTimeController`、`AiDecisionWaitStatus`。
  - `ThreeVsThreeRallyDirector.ConfigureAiDecisionSource`、`IsWaitingForAi`、
    `AiDecisionRequests`、`AiDecisionFallbacks`、`BlueAttackContacts`、`OrangeAttackContacts`。
  - `RallyTacticalWeights.TryResolve` 允许调用方在非法联网输出时保留自己的本地权重。
- 行为变化：
  - 球员四肢仍可以在合法拦网/扣球动作中伸过网面，但决定站位与碰撞移动的根节点不能越网。
  - AI 等待使用真实时间截止，不会因为自身降低 `Time.timeScale` 而无限延长。
  - 每次球权只请求一次联网战术权重，接球、组织和进攻阶段复用该结果；新球权会取消旧请求。
  - 超时、取消、异常和越界/非有限输出均回退本地权重；比赛结束或组件禁用会强制恢复时间。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；Shared DTO、Career 数据、Bootstrap 场景入口、序列化资源和 asmdef 引用未变
- 对方开发者需要做什么：无；未来运行时联网适配器只需实现 Match 内部接口并显式注入

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [x] 完整固定种子 3v3 对局验证
- [ ] 序列化、存档或迁移验证（不适用：没有序列化或 Shared/Career 变更）

使用 Unity `6000.0.43f1` 与 macOS 批处理 Test Runner：

- 球员半场约束定向 EditMode：`18/18` 通过。
- 战术权重请求/校验定向 EditMode：`5/5` 通过。
- AI 时间控制定向 PlayMode：`2/2` 通过，覆盖成功响应、真实时间超时、本地回退与时间恢复。
- 拦网反馈定向 EditMode/PlayMode：分别 `3/3`、`1/1` 通过。
- 完整 Physical3v3Rally：`1/1` 通过，结果 `15:10`，蓝/橙各 7 次 Attack，真实 Block 9 次；
  测试逐帧确认六名球员根节点始终位于己方半场。
- 全量 EditMode：`220/220` 通过，`failed=0`。
- 全量 PlayMode：`6/6` 通过，`failed=0`；包含完整 3v3、AI 成功/超时和拦网反馈中断恢复。

测试结果写入 `/tmp/volleyball-edit-full-final.xml` 与 `/tmp/volleyball-play-full-final.xml`；
`TestResults/` 保持忽略，不提交到仓库。

## 回滚与风险

全局时间倍率属于进程级 Unity 状态，若多个系统同时修改 `Time.timeScale`，仍需要由上层统一
仲裁。本控制器只在显式配置异步来源后接管，并保存/恢复进入等待前的倍率与 fixed delta；
取消、超时、组件禁用和比赛结束均走恢复路径。联网输出只影响有界评分权重，本地规则仍是
唯一判定权威。

需要回滚时可分别移除异步来源/时间控制器集成和半场约束；不需要迁移数据。不要处理工作区
中无关的 `ProjectSettings/ProjectSettings.asset` 本地修改。
