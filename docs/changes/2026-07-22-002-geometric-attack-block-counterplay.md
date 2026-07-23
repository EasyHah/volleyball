# CHG-20260722-002：几何进攻与拦网对抗

- 日期：2026-07-22
- 状态：进行中
- 负责人：Match
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/geometric-counterplay`
- 关联提交或 PR：实现提交待创建

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
  - 扩展 3v3 / 6v6 PlayMode 覆盖，包含真实 Block 后延迟球权、多人拦网、
    6v6 前排资格和二传不可行时的非二传组织。
- 新增或修改的公开类型/字段：
  - `BlockCandidateSnapshot`、`BlockUnitPlan`、`BlockUnitPlanner`。
  - 拦网、路线和 post-block 诊断计数器；不改变跨模块数据契约。
- 行为变化：
  - 战术基线不再按修订号随机切换。
  - 实际二传偏差不会把攻手起跳点拖离合法攻击带或重置已完成助跑。
  - 路线评估和真实执行读取同一个多人拦网单元及高度解析。
  - 6v6 仅前排球员可进入主动拦网单元。
  - Block 后等待真实越网或落地结果，再创建下一球权或结算得分。

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
- `FormalSixVsSixRallyPlayModeTests`：1/1 通过；完整比赛 25:14，
  130 次成功触球、13 次真实 Block、29 次防守球员进攻；
  固定种子内产生多人拦网且后排拦网者为 0。
- 完整 EditMode：335/335 通过。
- 完整 PlayMode：17/18 通过。几何对抗相关用例全部通过；唯一失败为既有
  `PhysicsContactTrainingPlayModeTests` 的扣球球心本地 Z 前伸约束
  （要求 `> 0.25m`，实际 `-0.044m`）。该训练仍完成 Receive / Set /
  Attack 三次真实触球且出球方向约束通过。尝试提前完成挥臂可改善前伸，
  但会把出球 Z 方向比例从 `>0.9` 降至约 `0.652`，因此未保留试验修正。
- 全量回归日志中的固定种子结果：3v3 为 15:8、7 次真实 Block、
  17 次多人拦网排程、最大 2 人；6v6 为 25:14、13 次真实 Block、
  39 次多人拦网排程、最大 2 人。两场路线计数均为 Line，其他路线为 0；
  需要在后续平衡阶段继续观察线路多样性。

## 回滚与风险

主要风险是高度自适应拦网提高防守成功率后改变固定种子比分和进攻线路分布，
以及训练场视觉前伸与物理冲量共用手掌法线所造成的既有测试冲突。
观察 `PhysicalBlockContacts`、`ScheduledMultiBlockUnits`、路线计数、
攻手触球率和 3v3 / 6v6 比分。回滚时应整体撤回几何选择器与多人拦网集成，
不能只恢复 Block 后立即球权，因为那会重新违反 R-REF-004。
