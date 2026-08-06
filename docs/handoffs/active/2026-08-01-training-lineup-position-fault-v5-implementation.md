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

TrainingLab 管理员覆盖仅作用于本训练情景的 Match 测试数值能力、身高与惯用手。训练室还须提供受限制的
发球球位置/速度设置、自由镜头书签、持续回合监视器及五步工作台。V5 result/replay 必须记录与
context/result/replay hash 绑定的可验证位置错误事实；Career 仅消费 Match 结论而不重算位置。

## 已授权边界

- Match 域纯规则、正式 V5 发球接触路径、TrainingLab 数据/UI/场景接线和 Replay recorder。
- Shared V5 context/result/replay 合同、ContractJson、Career V5 恢复/消费边界及必要 Bootstrap 接线。
- 相关程序集、测试、Windows x64 IL2CPP Development 构建与 Player 验收记录。

## 非目标

- V4 的规则、存档、结果、Replay、恢复和行为完全不变。
- 不允许任意回合中插球；发球球只能位于当前发球方己方底线后。
- 训练室覆盖、镜头或编辑器审核数据不得流入 Career、V5 正式 context 或正式比赛。
- 不提供位置错误容差或绕过判罚的用户开关；相等投影合法，只有严格反转违法。
- 不编辑姓名、号码、注册职业或 Career 权威属性。

## V5 版本与恢复决策

本阶段引入新的必填 V5 位置错误证据版本，并同时更新 context/result/replay 的规范 JSON、hash 和验证。
旧版本的 V5 pending/result/replay 绝不以默认字段或可选兼容字段静默读取；恢复时必须以明确、可恢复的
“不支持的 V5 证据版本，请放弃此待处理比赛并新建比赛”路径拒绝，保留原始工件供诊断。回滚方式是回退
本阶段分支；回退后的程序同样拒绝该新版本，而不是试图降级解释。

## 验收条件

- TrainingLab 完成轮转锁定、站位拖拽、角色/位次标识、受限发球设置、三正交编辑、镜头书签、管理员
  覆盖隔离、预检和运行时位置错误闭环。
- 纯规则覆盖 Home/Away、相等合法、每条前后/左右关系、并发错误、输入拒绝及稳定排序。
- V5 仅在发球触球前裁决，违规回合零触球且只计一次对手得分；result/replay/context 证据可确定性绑定，
  Career 不重算。
- V4 回归保持不变；Shared/Career/Match 受影响测试、完整 EditMode、必要 PlayMode、独立复核通过。
- 完成 Windows x64 IL2CPP Development Build 与 Windows Player 人工验收，或如环境不可用则如实保持待验收，
  不声明任务完成。

## 依赖资料

- `AGENTS.md`
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

## 执行记录（2026-08-05 续作）

- V4 回归已处理：Career runner 固定 `Time.captureDeltaTime = 1/60s` 并在 `finally` 恢复；工具恢复候选按可达
  拦网能力、轨迹距离和稳定 ID 排序。新鲜验证为 V4 回归 `2/2`、`AttackDefensePlannerTests` `23/23`。
- 发现并修复 TrainingLab 管理员身高输入边界：`IntegerField` 输入中间值不再直接构造越界覆盖对象；非法值提示并保持草稿不变，
  合法值可保存。新增 PlayMode 回归 `AttributeHeightInput_IgnoresIntermediateInvalidValueThenSavesValidOverride`，修复后 `1/1`。
- macOS Editor 人工证据：FormalTrainingScenarioLab 在 Play Mode 完成轮转锁定；交换 Home1/Home2 后校验红卡显示
  `TRAINING_POSITION_FAULT` 与 `Slot2BehindSlot1 is reversed at serve contact`；恢复原轮转后绿色校验通过。轮转行显示
  角色/位次标签；VX/VY/VZ 调整为 `1/3/9` 后轨迹响应；保存并重载 `机位 1`；身高覆盖非法中间值不抛异常；合法正式回合
  显示 `TRAINING COMPLETE 0:1`，底部日志记录 own-court landing。
- 本次会话坐标拖拽未稳定命中预览碰撞体，因此拖拽本身不计为人工通过；Windows x64 IL2CPP Player 仍待执行。handoff 继续保持
  `Status: active`。

## 执行记录（2026-08-06 交互修复）

- 根因确认：TrainingLab 将 UI Toolkit 的面板坐标直接当成屏幕像素传给 `Camera.ScreenPointToRay`，导致在
  Game View 缩放或 UI 偏移时射线无法稳定命中预览球员；三正交编辑此前只是静态提示文字，预览也没有创建
  头顶文本对象。
- 已修复面板坐标投影；提供可选 XY/ZY/XZ 正交视图，拖拽严格保留第三轴；预览球员创建并更新
  `职业 · N号位` 头顶标签。新增场景/UI EditMode 回归 `8/8` 与 TrainingLab PlayMode `5/5`。
- `ProjectSettings` 和 TrainingLab `PanelSettings` 已分别设为默认 1920x1080 与参考 1920x1080；Editor 的
  `Free Aspect / Scale` 是本地预览缩放，不代表 Player 分辨率。当前环境重新打开 Unity Editor 时异常退出，
  因此最新拖拽、三视图和标签的人工视觉验收仍待补；Windows x64 IL2CPP Player 同样待验收。handoff 保持
  `Status: active`。

## 执行记录（2026-08-06 自由 3D 观察）

- 新增独立只读观察 presenter：仅持有 `VisualElement` 与 `Camera`，不持有 controller 或草稿；它将预览相机输出至
  RenderTexture UI 背景，左键轨道、右键/中键平移、滚轮缩放。返回战术板或销毁预览会释放 RenderTexture，且选中对象、
  当前工作流和草稿均由 View 保持。
- 新鲜自动验证：受影响 EditMode `26/26`（含观察入口/表面树断言）与 TrainingLab PlayMode `7/7`（含观察窗口
  输出、选中项和球员/球/速度草稿不变）通过；旧 authoring ray 符号检索为空，`git diff --check` 通过。
- Unity `6000.3.20f1` 的 `-nographics` PlayMode 在此 RenderTexture 路径触发 Unity 原生图形渲染崩溃，常规 batch
  PlayMode 正常通过；这不替代 1920x1080 macOS Editor 和 Windows x64 IL2CPP Player 的观察交互人工验收。任务单保持
  `Status: active`。
