# CHG-20260719-003：Physical3v3Rally 拦网命中反馈

- 日期：2026-07-19
- 状态：已完成
- 负责人：Match / Docs
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/blocking-roles`
- 关联提交或 PR：`f3d2634`

## 改动摘要

Physical3v3Rally 的真实拦网原本只有球路变化、状态栏和日志，命中瞬间缺少独立的可见反馈。
本次为已接受的 Block 增加球队色命中核心、扩散环、短时点光和球尾迹脉冲，让物理拦网与普通
触球在画面上可直接区分。

反馈完全由代码在运行时创建，不依赖新增贴图、音频、材质或场景对象。它只读取已完成的
拦网结果，不改变球速、触球次数、球权或得分。Shared、Career、Bootstrap、场景路径、
程序集引用、`MatchContextV1` 与 `MatchResultV1` 均未改变。

## 具体变更

- 文件、场景、资源或程序集：
  - 新增 `BlockImpactFeedback`，以一个可复用实例渲染约 `0.48` 秒的命中核心、扩散环、
    点光和尾迹脉冲；结束后恢复球原始尾迹宽度与颜色。
  - `ThreeVsThreeRallyDirector` 在 `RallyTouchState.Accept` 接受真实 Block 后调用反馈，
    并公开 `BlockImpactEffects` 诊断计数。
  - PlayMode 在完整单局内逐帧观察 `IsPlaying`，同时要求效果次数与真实物理拦网次数相等。
  - Unity 为新增运行时代码和 EditMode 测试生成并保留对应 `.meta`；场景文件未修改。
- 新增或修改的公开类型/字段：
  - 新增 `BlockImpactFeedback`，公开播放次数、播放状态、最后拦网球队、命中位置、反弹速度、
    当前强调色和可见元素数量。
  - `ThreeVsThreeRallyDirector.BlockImpactEffects` 返回当前完整单局已播放的真实拦网效果数。
- 行为变化：
  - 蓝队与橙队使用不同强调色，同时通过核心、环形扩散和尾迹宽度提供非纯颜色提示。
  - 反弹速度只调节视觉强度；最终物理速度仍由原有 ContactResponse 和
    TechniqueControlPolicy 决定。
  - Ignore、Fault、过期窗口或普通触球不会播放拦网效果。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；比赛输入、结果、场景路径和序列化结构均未变化
- 对方开发者需要做什么：无；Shared、Career 与 Bootstrap 无需同步

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [ ] 手动场景验证
- [ ] 序列化、存档或迁移验证（不适用：没有序列化或资源路径变化）

使用 Unity `6000.0.43f1` 与 macOS 批处理 Test Runner：

- 定向 EditMode：`BlockImpactFeedbackTests` 为 `2/2` 通过。
- 定向 PlayMode：Physical3v3Rally 为 `1/1` 通过；测试逐帧观察到活动效果，日志包含
  `block-contact ... effect=1`，并完成 `RESULT`。
- 全量 EditMode：`216/216` 通过，`failed=0`；证据为
  `TestResults/EditMode-block-impact-final.xml` 与同名 `.log`。
- 全量 PlayMode：`3/3` 通过，`failed=0`；证据为
  `TestResults/PlayMode-block-impact-final.xml` 与同名 `.log`。

完整 3v3 日志记录 `block-contact team=Blue actor=Attacker rebound=Orange speed=11.3 effect=1`，
随后正常产生 `RESULT score=15:2 contacts=30 blocks=1 nonSetterSets=1 defenderAttacks=1`。
`TestResults/` 保持忽略，不提交到仓库。

## 回滚与风险

主要风险是透明材质在不同图形后端的显示差异、场景卸载时临时材质未释放，或反馈误由非法
候选触发。组件集中管理两个临时材质，销毁时清理并恢复球尾迹；Director 仅从 accepted Block
分支调用，PlayMode 同时比较效果计数和真实拦网计数。

需要回滚时撤销 `f3d2634` 即可。该提交只增加 Presentation 反馈及测试，不需要迁移数据，
也不要处理无关的 `ProjectSettings/ProjectSettings.asset` 本地修改。
