# CHG-20260730-003：V4 统一首次触网续球

- 日期：2026-07-30
- 状态：已自动验证
- 负责人：Match
- 影响模块：Match / Tests / Docs
- 交互级别：模块内部
- 关联分支：`codex/v4-unified-net-continuation`
- 关联提交或 PR：尚未创建

## 改动摘要

Formal 6v6 现在以“发球或已接受球员触球”划分飞行段。每段第一次真实触网会按
触网后的物理轨迹处理续球，后续触网只保留球体物理回弹而不重复发布战术状态。

本改动不修改 `MatchContextV4`、`MatchResultV4`、`MatchReplayV4`、Shared、Career、
存档或网体物理。

## 具体变更

- `PhysicalMatchRallyDirector`：增加私有飞行段 guard，并在发球、已接受触球、落地和回合
  结束时确定性地创建或清除。
- 普通已接受触球后的第一次触网：取消过期窗口；留本方时仅在还有计数触球额度且物理轨迹
  可达时重建 Receive；越网时仍等待真实过网，再交给既有防守接管。
- 第三次计数触球留本方时不创建第四次触球窗口，继续由 ground/out 裁判结算。
- 已接受 Block 后的触网不记为通用触网分派，仍仅由既有 post-block continuation 处理。
- `DefensiveContactContinuationPlayModeTests`：为既有发球触网冻结场景增加单次分派断言。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；没有公共合同或序列化变更。
- 消费方或后续负责人需要做什么：无。

## 验证

- [x] focused EditMode：`RallyTouchStateTests;FullRallyV3RulesEngineTests`，30/30。
- [x] 完整 EditMode：1480/1480。
- [x] `DefensiveContactContinuationPlayModeTests`：11/11；含本次计数排他性修正后的重跑。
- [x] 指定中回合触网情景：`FormalMatchScenarioDefinitionV4` 保持只表达开球输入；所需覆盖由
  [CHG-20260731-001](2026-07-31-001-formal-training-scenario-lab.md) 的语义化正式训练起点补齐；
  `SecondTouchNetOwnSide`、`ThirdTouchNetOwnSide`、`ThirdTouchNetCross`、
  `PostBlockNet`、多次触网和不可达变体的固定种子双跑及受影响 PlayMode 回归通过。
- [x] Formal 6v6 单局 smoke：关闭该测试中逐条诊断 `Log` 的堆栈追踪、并在 `finally` 恢复
  原设置后通过；固定种子赛局在 333.19 秒以 21:25、346 次已接受接触完成。此前 360 秒停在
  23:22 是测试日志堆栈追踪耗尽实时预算，不是规则断言或回合停滞。
- [x] `git diff --check`。

测试环境：Unity 6000.3.20f1，macOS arm64。

## 回滚与风险

回滚本改动即可恢复仅发球触网的续球路径。中回合触网的真实物理验证缺口已由
`docs/handoffs/active/2026-07-31-formal-training-scenario-lab-implementation.md`
补齐；训练实验室自身的 Windows、视觉和独立复核门禁仍由该 active handoff 跟踪。
