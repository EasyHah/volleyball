# CHG-20260801-001：HUD 精简与训练室二传审核入口

- 日期：2026-08-01
- 状态：已自动验证，待 Unity Editor 人工验收
- 负责人：Match
- 影响模块：Match / Tests / Docs
- 交互级别：模块内部

## 改动摘要

训练实验室收紧了 HUD 信息层级：顶部状态区与底部运行摘要占用更少空间，世界预览只保留当前可执行的
拖放提示，运行证据区改为摘要。编辑字段、校验问题、回合状态和错误反馈保持原有功能。

在运行产生 Gate I `SetterTargetSnapshotV1` 后，工具栏启用“审核二传”。它仅在 Unity Editor 内通过
反射调用 `Volleyball.Match.AI.Editor` 中既有的审核窗口，并预选最新快照。Player 编译路径没有对
`UnityEditor` 或审核程序集的引用；它不会调用网络、修改运行回合或改变 Replay。

## 验证

- [x] focused EditMode：Unity `6000.3.20f1`，macOS arm64，11/11。
  覆盖训练室 UXML 入口、审核窗口 Editor assembly 边界，以及既有教师请求和审核 session 的严格
  候选/确认合同。
- [x] `git diff --check`。
- [ ] Unity Editor 人工闭环：加载训练室，确认精简 HUD 仍可读；完成产生 Gate I 快照的回合后，
  点击“审核二传”，确认审核窗口预选最新快照并完成一次接受或纠正流程。

## 回滚与风险

回滚此改动可移除入口按钮、反射调用、审核窗口预选 API 和相关样式，不影响既有教师审核菜单或训练
回合。剩余风险仅为实际 Editor 布局和窗口预选交互尚需人工确认。
