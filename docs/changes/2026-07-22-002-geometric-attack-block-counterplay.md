# CHG-20260722-002：几何进攻与拦网对抗

- 日期：2026-07-22
- 状态：已完成
- 负责人：Match
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/geometric-counterplay`
- 关联提交或 PR：`603b471`、`c692fdb`

## 改动摘要

把物理回合中的组织目标、进攻线路和拦网单元改为可重放的几何决策：
正常二传保持近网攻击带和连续助跑，攻击依据预测拦网手臂选择合法线路，
防守依据实际截击点安排一至三名可达拦网者。接受真实 Block 后不再提前
创建接球球权，合法越网和落地仍由既有裁判路径结算。

本改动不新增判分规则，不改变 Shared 契约，也不引入随机战术分支。

## 具体变更

- 文件、场景、资源或程序集：
  - 新增 `AttackBandPolicy`、`SetTargetSelector`、`AttackRouteSelector` 和
    `BlockUnitPlanner` 及对应 EditMode 测试。
  - 修改 `PhysicalMatchRallyDirector` 和 `PrototypePlayerAgent`，统一规划与
    执行使用的多人拦网胶囊、起跳高度和实际截击点。
  - 扣球挥臂在 Power 末段完成前伸；扫掠命中仍读取可见手掌轨迹，但
    Attack 物理响应不再吸收动画手掌速度，满质量触球可完整修正到目标方向。
  - 扩展 3v3 / 6v6 PlayMode 覆盖，包含真实 Block 后延迟球权、多人拦网、
    6v6 前排资格和二传不可行时的非二传组织。
- 新增或修改的公开类型/字段：
  - `BlockCandidateSnapshot`、`BlockUnitPlan`、`BlockUnitPlanner`。
  - `ContactResponse.ApplyWithSurfaceVelocity`，用于把真实碰撞诊断与响应冲量
    使用的表面速度明确分开。
  - 拦网、路线和 post-block 诊断计数器；不改变跨模块数据契约。
- 行为变化：
  - 战术基线不再按修订号随机切换。
  - 实际二传偏差不会把攻手起跳点拖离合法攻击带或重置已完成助跑。
  - 路线评估和真实执行读取同一个多人拦网单元及高度解析。
  - 6v6 仅前排球员可进入主动拦网单元。
  - Block 后等待真实越网或落地结果，再创建下一球权或结算得分。
  - 视觉挥臂可完整前伸，不再把动画速度注入 Attack 物理冲量；接近满居中的
    扣球才会从原 60° 修正上限平滑放宽到完整目标方向。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；Shared、Career、Bootstrap 无变化
- 对方开发者需要做什么：无

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [ ] 手动场景验证
- [x] 序列化、存档或迁移验证（不适用；未修改契约）

Unity `6000.0.43f1`，macOS 本地 batchmode，固定比赛种子：

- `BlockUnitPlannerTests`：3/3 通过。
- `PrototypePlayerContactSourceTests`：31/31 通过，包含预测/执行拦网高度一致性。
- `ThreeVsThreeRallyPlayModeTests`：4/4 通过；完整比赛 15:8，
  58 次成功触球、7 次真实 Block、12 次防守球员进攻；专项测试确认
  Block 后无提前接球/应急窗口，并由落地裁判结算。
- `FormalSixVsSixRallyPlayModeTests`：1/1 通过；完整比赛 25:10，
  86 次成功触球、11 次真实 Block、15 次防守球员进攻；
  固定种子内产生多人拦网且后排拦网者为 0。
- `PhysicsContactTrainingPlayModeTests`：1/1 通过；Receive / Set / Attack
  三次真实触球成功，扣球速度 23.7 m/s，球心前伸与出球 Z 方向比例约束
  同时通过。
- `AttackChainCalibrationPlayModeTests`：3v3 与 6v6 各 100 次正常二传均为
  100/100 可进攻，A 级二传无触球错误率均为 0，近网攻击计划均为 100/100。
- 完整 EditMode：338/338 通过。
- 完整 PlayMode：18/18 通过。
- 全量回归日志中的固定种子结果：3v3 为 15:8、7 次真实 Block、
  17 次多人拦网排程、最大 2 人；6v6 为 25:10、11 次真实 Block、
  25 次多人拦网排程、最大 2 人。两场路线计数均为 Line，其他路线为 0；
  需要在后续平衡阶段继续观察线路多样性。

## 回滚与风险

主要风险是高度自适应拦网提高防守成功率后改变固定种子比分和进攻线路分布，
以及接近满居中的扣球采用更大方向修正后改变高质量进攻分布。
观察 `PhysicalBlockContacts`、`ScheduledMultiBlockUnits`、路线计数、
攻手触球率和 3v3 / 6v6 比分。回滚时应整体撤回几何选择器与多人拦网集成，
不能只恢复 Block 后立即球权，因为那会重新违反 R-REF-004。
