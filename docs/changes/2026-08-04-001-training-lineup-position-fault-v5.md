# CHG-20260804-001：训练室阵容与 V5 位置错误接线

- 日期：2026-08-04
- 状态：已自动验证，macOS Editor 核心验收待补，Windows Player 待验收
- 负责人：Career / Match collaborator
- 影响模块：Match / Shared / Career / Bootstrap / TrainingLab / Replay / Tests / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/training-lineup-position-fault-v5`
- 合并提交：`7f570e7`（合入 `origin/main`，包含 `9a3897e`）

## 改动摘要

- TrainingLab 提供轮转锁定、站位编辑、受限发球起点、镜头/属性覆盖和运行时位置错误诊断。
- Match 纯规则在发球触球前按 Home/Away 投影轴裁决严格位置错误；违规回合只计一次对手得分，不启动
  物理、AI 或触球链路。
- V5 context/result/replay 保存确定性位置错误证据，Career 只消费 Match 结论；每条事实绑定违规队的
  context 轮转位次，且 Career 拒绝 result/replay 证据不一致。
- 修复 V4 兼容性：版本中立能力快照保留 Receive/Set/Defense movement 与 defense reaction 的原始
  V4 来源；V5 Gate I 使用快照而不是空的 V4 derived 属性。

## 自动验证

- 完整 EditMode：`1573/1573`。
- V5 位置错误契约及 Career 聚合：`50/50`；V5 runner PlayMode：`1/1`；TrainingLab 运行时 PlayMode：`4/4`。
- 同批目标 PlayMode：修复后两个 V4 回归均通过（`2/2`）；正式 runner 使用固定 `Time.captureDeltaTime =
  1/60s`，校准首攻的工具恢复分支按可达拦网能力排序后选择 `BlockToolRecovery`。
- `AttackDefensePlannerTests`：`23/23`；TrainingLab 高度覆盖输入回归：`1/1`（非法中间值不抛异常、合法值写入覆盖）。
- TrainingLab 编辑交互补丁：场景/UI EditMode `8/8`；TrainingLab PlayMode `5/5`，覆盖职业/位次头顶标签和
  XY/ZY/XZ 正交视图切换。
- 自由 3D 观察：场景/UI 和训练室聚焦自动验证为 EditMode `26/26`、PlayMode `7/7`。观察窗口将预览相机输出
  到 UI Toolkit 的 RenderTexture 背景，支持轨道、平移和缩放；观察 presenter 不接收 controller 或草稿引用。
- `git diff --check`：通过。
- Career V5 runner 使用固定 `Time.captureDeltaTime = 1/60s`，避免 headless 帧率导致测试预算漂移；原值在用例结束后恢复。

## 待人工验收与风险

- macOS Unity Editor 手工验收已完成：锁定/恢复轮转；交换 Home1/Home2 制造并校验位置错误，红卡显示
  `TRAINING_POSITION_FAULT · Slot2BehindSlot1 is reversed at serve contact`；恢复后绿色提示
  `校验通过：12 人、球、规则起点与权威配方合法`；右侧轮转行显示角色与位次；VX/VY/VZ 改为
  `1/3/9` 后红色轨迹同步变化；保存并点击重载 `机位 1`；训练专属身高覆盖的非法输入被提示而不抛异常，
  合法 `1981` 写入覆盖；合法正式回合显示 `TRAINING COMPLETE 0:1`，底部记录 own-court landing。
- 已修复 UI Toolkit 面板坐标到屏幕射线的转换，并将 XY/ZY/XZ 从静态文案改为可点击的正交编辑视图；拖拽
  按当前视图只写入对应的两个坐标轴。头顶 `职业 · N号位` 标签也已接入预览。自动化已覆盖这些行为；由于
  重新打开 macOS Editor 的进程在该环境中异常退出，尚未将拖拽和标签可读性计为最新人工通过。
- 自由 3D 观察的可见输出与只读入口已自动验证；`-nographics` 渲染 RenderTexture 时 Unity `6000.3.20f1`
  会在原生图形管线中崩溃，故使用常规 batch PlayMode 执行该部分自动测试。观察模式的轨道/平移/缩放、轨迹可见性
  与书签操作仍需在 macOS Editor 和 Windows Player 人工确认。
- 本机为 macOS arm64，安装的 Unity `6000.3.20f1` 只含 `MacStandaloneSupport`，工作区也没有 Windows Player
  产物；因此 Windows x64 IL2CPP 构建与 Player 验收无法在本机执行，保持待验收。
- 尚未在 Windows Player 中执行本计划新增 TrainingLab 的 IL2CPP 构建、输入、渲染及 pending 恢复验收；现有
  V5 Windows IL2CPP 构建记录保留在 `9a3897e` 对应改动文档中。
- 回滚方式：回退本里程碑分支；新 V5 位置错误证据版本继续按 handoff 规则拒绝旧 pending/result/replay。
