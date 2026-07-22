# CHG-20260722-002：几何进攻与拦网对抗

- 日期：2026-07-22
- 状态：进行中
- 负责人：Match
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/geometric-counterplay`
- 关联提交或 PR：`d5391ab`、`2c3d871`、`a21a350`

## 改动摘要

本改动按 `docs/rules.md` 的 R-GOV、R-REF、R-PLAY 和 R-OFF 规则，逐步替换硬随机战术、
远网扣球重规划、单人拦网和拦网后即时球权切换。当前已完成确定性战术基线、角色起跳带
和实际传球后的起跳带约束；后续继续实现助跑连续性、几何线路选择、多人拦网和拦网后裁判
时序修复。

## 具体变更

- 文件、场景、资源或程序集：
  - `Assets/Volleyball/Match/Runtime/AI/PhysicalRallyTacticPlanner.cs`
  - `Assets/Volleyball/Match/Runtime/AI/AttackBandPolicy.cs`
  - `Assets/Volleyball/Match/Runtime/AI/SetQualityAssessment.cs`
  - `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`
  - `Assets/Volleyball/Match/Tests/EditMode/PhysicalRallyTacticPlannerTests.cs`
  - `Assets/Volleyball/Match/Tests/EditMode/AttackBandPolicyTests.cs`
  - `Assets/Volleyball/Match/Tests/EditMode/SetQualityAssessmentTests.cs`
  - `docs/rules.md`
- 新增或修改的公开类型/字段：
  - 新增 `AttackBand` 和 `AttackBandPolicy`。
  - `SetAttackReplanner.Replan` 增加攻手角色、进攻方和二传距网深度参数。
- 行为变化：
  - 回合战术基线不再通过随机或 revision 变化选择线路。
  - 实际传球重规划时，起跳点约束到角色攻击带；二传超过 4m 时攻击带按规则后移。
  - 正常扣球接触中心保留真实球预测位置；若真实球相对近网起跳点已超出可处理范围，则不开
    正常扣球窗口，避免把深球凭空搬到近网接触。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；当前改动仅限 Match 内部 AI/物理调度与测试。
- 对方开发者需要做什么：无

## 验证

- [x] EditMode 测试
- [ ] PlayMode 测试
- [ ] 手动场景验证
- [ ] 序列化、存档或迁移验证（如适用）

已运行 Unity 6000.0.43f1 / macOS：

- `Volleyball.EditModeTests.PhysicalRallyTacticPlannerTests`：9/9 通过。
- `Volleyball.EditModeTests.AttackBandPolicyTests`：通过。
- `Volleyball.EditModeTests.SetQualityAssessmentTests`：通过。
- Task 2 初始实现后曾运行完整 EditMode：312/312 通过；后续总回归将在本改造完成后补记。

## 回滚与风险

主要风险在于实际深球被降级为不可正常扣球后，攻手成功率可能短期下降，需要后续的最佳传球
点选择与几何线路选择补齐进攻收益。回滚方式是撤销本分支提交或回到 `a21a350` 前的 replan
行为，但那会恢复远网假接触风险，不建议单独回滚。
