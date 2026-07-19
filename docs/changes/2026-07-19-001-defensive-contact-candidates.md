# CHG-20260719-001：Physical3v3Rally 防守候选触球池

- 日期：2026-07-19
- 状态：已完成
- 负责人：Match
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/blocking-roles`
- 关联提交或 PR：尚未创建

## 改动摘要

Physical3v3Rally 原先只接受剧本里预定的接球队员触球，导致对方扣球砸到二传或攻手身体附近时，物理层即使发生交汇也不会生成起球。本次改动增加防守方三人共同参与的一传候选窗口：扣球后等待落地期间，防守方任意球员只要真实被球扫中，就可以作为 `Receive` 接起，并继续进入该队二传阶段。

本次不包含多攻手战术、自由换位二传、助跑阈值、助跑角度削弱或扣球方向限制；这些留给后续“多角色决策 + 进攻模型”阶段。

## 具体变更

- 文件、场景、资源或程序集：
  - 修改 `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
  - 修改 `Assets/Volleyball/Match/Runtime/Presentation/SimulatedBall.cs`
  - 修改 `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs`
  - 修改 `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`
  - 修改 `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`
  - 新增 `docs/superpowers/plans/2026-07-18-defensive-contact-candidates.md`
- 新增或修改的公开类型/字段：
  - `PrototypePlayerAgent.EnableEmergencyReceiveWindow(...)`
  - `PrototypePlayerAgent.DisableEmergencyReceiveWindow()`
  - `BallContactCandidate.Actor`
  - `ThreeVsThreeRallyDirector.EmergencyReceiveWindowAssignments`
  - `ThreeVsThreeRallyDirector.EmergencyReceiveContacts`
- 行为变化：
  - 预定扣球触球完成后，防守方三名球员都会获得限时应急一传候选窗口。
  - 应急候选窗口只在没有预定触球动作时提交 `Receive` 接触面，避免覆盖正常二传、扣球、拦网等预定触球。
  - 物理球命中应急接球候选后，导演记录实际触球人，并按当前球轨迹调度下一次二传。
  - PlayMode 回归增加应急防守窗口和应急接球计数断言。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；未修改 `Shared` 契约、`MatchContextV1` 或 `MatchResultV1`
- 对方开发者需要做什么：无

## 验证

- [x] EditMode 测试：`153/153 passed`
  - `TestResults/EditMode-defensive-contact-final2.xml`
  - 命令：`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles -runTests -testPlatform EditMode -testResults /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/EditMode-defensive-contact-final2.xml -logFile /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/EditMode-defensive-contact-final2.log`
- [x] PlayMode 测试：`3/3 passed`
  - `TestResults/PlayMode-defensive-contact-final2.xml`
  - 命令：`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles -runTests -testPlatform PlayMode -testResults /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/PlayMode-defensive-contact-final2.xml -logFile /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/PlayMode-defensive-contact-final2.log`
- [x] 手动场景验证：由 PlayMode `PhysicalLoop_UsesSixPlayersOneBallAndSwitchableCameras` 覆盖，日志包含 `emergency-receive contact=4 team=Blue role=Attacker`
- [x] 序列化、存档或迁移验证：不适用；未修改持久化契约

## 回滚与风险

- 主要风险：
  - 当前应急起球后仍回到固定“本方二传”阶段；如果实际起球人就是二传，会暂时出现二传连续处理的简化逻辑。
  - 应急一传目标仍是固定回二传点，尚未根据实际站位、角色冲突、技术系数做质量评分。
- 观察指标：
  - `EmergencyReceiveWindowAssignments`
  - `EmergencyReceiveContacts`
  - PlayMode 日志中的 `[Physical3v3] emergency-receive ...`
- 回滚方式：
  - 移除 `EnableEmergencyReceiveWindows` 调用和 `HandleEmergencyReceiveContact` 分支。
  - 保留或删除 `BallContactCandidate.Actor` 均可；若没有应急候选逻辑，旧固定序列仍可从 `_expectedIndex` 推断触球人。
