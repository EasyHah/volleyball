# CHG-20260804-001：训练室阵容与 V5 位置错误接线

- 日期：2026-08-04
- 状态：已自动验证，待 Windows 与剩余人工验收
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
- 同批目标 PlayMode：`20/22`。两个 V4 回归待处理：正式 runner 未在预算内结束，以及校准首攻选择 `Tip`
  而非预期的 `BlockToolRecovery`。
- `git diff --check`：通过。
- Career V5 runner 使用固定 `Time.captureDeltaTime = 1/60s`，避免 headless 帧率导致测试预算漂移；原值在用例结束后恢复。

## 统一 TrainingLab V5 工作台增量（2026-08-17）

- 已将 TrainingLab 的生产入口、工作台、冻结快照、启动与单回合结果切换为原生 V5 路径。运行复用正式
  V5 authority、AI、物理与裁判回调，在首个 rally 结束边界产生 `TrainingRallyOutcomeV1`，不产生
  `MatchResultV5`，也不接入 Replay 或 Career。
- 新 V2 本地情景明确拒绝 V1 字节且不执行静默迁移；保存、覆盖、失败恢复、identity/hash、dirty leave
  已由 focused tests 覆盖。V5 管理员覆盖包含 12 项整数能力、身高与 `DominantHandV5`，冻结后实际写入
  agent 能力与接触几何，不修改 `MatchContextV5`。
- Home/Away 摆位、UI 投影与 V5 发球前位置错误比较统一采用场中心 180 度队伍局部坐标；V4 validator
  显式保留旧共享横轴，V4 Career、正式 V4 与 3v3 生产语义未迁移。
- 独立复核后的新鲜自动验证：TrainingLab focused EditMode `76/76`，TrainingLab PlayMode `3/3`，
  完整 EditMode `1624/1624`，V5 Career PlayMode `8/8`，3v3 隔离复跑 `1/1`。结果文件分别为
  `TestResults/TrainingLab-Unified-Final-EditMode-4.xml`、
  `TestResults/TrainingLab-Unified-Final-PlayMode-4.xml`、
  `TestResults/TrainingLab-Unified-Complete-EditMode-4.xml`、
  `TestResults/TrainingLab-Unified-V5-Career-PlayMode-3.xml` 与
  `TestResults/TrainingLab-Unified-3v3-PlayMode-2.xml`。
- 独立复核收口补齐：setup 变更会使旧 preflight 失效；Match 边界拒绝恢复出的越界站位、非法发球带、
  反向或超速发球；六个内置模板从实际 V2 Resources 加载并具有各自语义；俯视/侧视速度与轨迹、精确
  输入、只读 3D 书签已接线。`-nographics` 下禁用离屏 Camera 实际绘制以规避 Unity 原生渲染崩溃，
  GUI/Player 仍启用 RenderTexture 预览。
- V2 场景、工作台、controller 与 runtime 的生产图不引用旧 V1/V4 startup/evidence；旧 V1 直接运行
  夹具仅作为隔离兼容代码保留，不能由 V2 场景或本地目录进入。位置错误公共 API 默认坐标保持 legacy，
  V5 Match setup 与正式 V5 director 均显式选择 team-local point-symmetric frame。
- macOS Editor 已人工覆盖情景库、轮转、摆位、位置错误聚焦、发球俯视/侧视和只读 3D；截图保存在
  `TestResults/TrainingLab/VisualAcceptance/2026-08-17/01-07-*.png`。编辑器窗口截图为
  `1338x745`；另有 PlayMode `1920x1080` 无重叠自动布局门禁，不能将前者冒充为 1920x1080 截图。
- Windows x64 IL2CPP Development Build 已执行但环境不支持该目标：本机 Unity 仅安装
  `MacStandaloneSupport`，日志为 `TestResults/TrainingLab-Unified-Windows-Build.log`。因此 Windows
  Player 构建和实体 Windows x64 人工验收仍待具备 Windows Build Support 的环境执行，阶段不得标记完成。

## 待人工验收与风险

- 尚未补齐计划要求的 `08-preflight.png`、`09-running-result.png`、`10-unsaved-leave.png`，且现有
  01--07 截图为 `1338x745`，因此 1920x1080 macOS Editor 完整手工流程仍待验收。
- 尚未在 Windows Player 中执行本计划新增 TrainingLab 的 IL2CPP 构建、输入、渲染及 pending 恢复验收；现有
  V5 Windows IL2CPP 构建记录保留在 `9a3897e` 对应改动文档中。
- 回滚方式：回退本里程碑分支；新 V5 位置错误证据版本继续按 handoff 规则拒绝旧 pending/result/replay。

## 统一工作台交互纠偏（2026-08-20）

- 修复情景入口复用旧 controller/草稿的问题：`ShowWorkbench` 只接受明确的 `builtin:`/`local:` identity；
  built-in 每次创建独立 local ID 与 Match draft，saved local 按原 ID 从磁盘重载。普通模板均从零位置错误
  开始，只有两项命名 fault 模板故意非法。
- 恢复批准的轮转和站位体验：正式六点位轮转卡显示位次、姓名、号码和中文角色；真实 UI Toolkit
  captured-pointer 仅允许同队交换并清理空白/跨队 drop；站位页选择场上球员，提供 court 与双轴 ruler
  编辑、故障双方/位次/规则/方向、红色关系线、蓝色同源修正箭头和可达的 Serve 门禁。
- fresh automated evidence：focused EditMode `46/46`、focused PlayMode `9/9`、完整 EditMode
  `1625/1625`，分别见 `TestResults/TrainingLab-Corrective-ReviewFix-EditMode.xml`、
  `TestResults/TrainingLab-Corrective-ReviewFix-PlayMode-2.xml`、
  `TestResults/TrainingLab-Corrective-Complete-EditMode-2.xml`。此前同一代码冻结阶段的 V5 Career
  PlayMode `8/8` 与 3v3 isolation `1/1` 仍有效；末次复核修复只影响 TrainingLab View 与其 PlayMode
  用例。`git diff --check` 通过。
- 只读独立复核已完成，发现的纠正方向与 pointer-capture 测试缺口均已修复。人工截图保存在
  `TestResults/TrainingLab/VisualAcceptance/2026-08-18-corrective/00-current-hub.png` 至
  `07-serve-reached-by-page-button.png`；其中 `03-same-team-exchange-attempt.png` 是未成功的物理拖拽尝试，
  不作为通过证据。
- 新增九张逐张核验为 `1920x1080` 的帧缓冲截图：
  `TestResults/TrainingLab/VisualAcceptance/2026-08-20-exact-1920x1080/00-hub.png` 至
  `08-unsaved-leave.png`，覆盖 Hub、标准轮转、合法站位、故障聚焦、精确字段修正、Serve、自动
  preflight、原生 V5 单回合结果和 dirty-leave modal。新增 `09-serve-side-view.png` 与
  `10-read-only-3d.png`，补齐 Serve 侧视和只读 3D；十一张文件均已逐张核验内容和
  `1920x1080` 像素尺寸。
- fresh pointer follow-up PlayMode `9/9`，证据为
  `TestResults/TrainingLab-Corrective-PhysicalFollowup-PlayMode.xml`。computer-use 的坐标点击可驱动 Game
  View，但 drag 未向 Unity 发送 PointerDown/Move/Up，因此不能作为物理拖拽通过证据。
- 仍待门禁：真实物理鼠标 rotation/court/ruler drag、Serve 侧视/只读 3D 精确截图，以及安装 Windows
  Build Support (IL2CPP) 后的 Windows x64 Development Build 与 Player 验收。当前状态保持
  “已自动验证，待 Windows 与剩余人工验收”。
