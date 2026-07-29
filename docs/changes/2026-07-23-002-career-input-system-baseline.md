# CHG-20260723-002：Career 菜单输入基线

- 日期：2026-07-23
- 状态：已完成
- 负责人：Career / 共同模块负责人
- 影响模块：Packages / Project Settings / Career.Presentation / Career.EditModeTests / Tools / Docs
- 交互级别：跨模块（重点）
- 关联分支：`feature/shared-career-fake-contract`
- 关联提交：提交主题 `chore(input): add career menu action baseline`

> [!IMPORTANT]
> 本次仅为 Career 菜单引入 Input System 1.17.0 和冻结的输入动作资源。项目输入处理设为
> `Both`，因此 Match 可继续使用既有 `UnityEngine.Input`，无需改代码；Career 的运行时
> UI 接线、EventSystem、InputSystemUIInputModule、焦点管理和重绑定全部延期到 Stage 8。

## 改动摘要

- 锁定 `com.unity.inputsystem@1.17.0`，并将 Active Input Handling 设为 `Both`。
- 在 Career Presentation 下新增唯一的 `CareerMenu.inputactions`：只含 `CareerMenu`
  动作图及 `Navigate`、`Submit`、`Cancel`、`Back`、`PageLeft`、`PageRight` 六个动作。
- 冻结 `KeyboardMouse`、`Gamepad` 两种控制方案和键鼠/手柄必需绑定；所有 map、action
  与 binding ID 均为资源内人工维护的稳定 UUID，鼠标为可选设备，且不生成 C# wrapper。
- 只让 Career EditMode 测试程序集引用 `Unity.InputSystem`；没有修改 Career 运行时程序集
  引用，也没有新增运行时 UI、输入模块、场景对象或 Match action map。

## 具体变更

- 包与项目配置：`Packages/manifest.json`、`Packages/packages-lock.json`、
  `ProjectSettings/ProjectSettings.asset`。
- 输入资源：`Assets/Volleyball/Career/Runtime/Presentation/Input/CareerMenu.inputactions`。
- 测试：导入资源契约、包/项目设置锁定，以及真实 Keyboard/Gamepad state 驱动的最小线性
  Navigate/Submit 验证；测试结束后关闭动作图、移除新增设备并恢复 EditMode 玩家输入更新标志。
- 公开类型或存档字段：无。

## 跨模块交互重点

- 接口提供方：Unity Input System 包与 Career Presentation 的 `CareerMenu` 动作资源。
- 接口使用方：当前仅 Career EditMode 契约测试；Stage 8 Career UI 将成为运行时使用方。
- 数据方向：`Keyboard/Gamepad -> CareerMenu InputActionAsset -> future Career UI`。
- 兼容性：兼容。`activeInputHandler: 2` 同时保留旧输入后端和新输入后端，Match 源码与资源保持冻结。
- 消费方或后续负责人需要做什么：Match 无需改代码。Stage 8 再负责 UI Toolkit 面板接线、
  EventSystem/InputSystemUIInputModule、焦点与滚动行为；不得在本基线中提前实现。

## 验证

- [x] 聚焦 Career 输入基线 EditMode：3/3 通过。
- [x] 全量 EditMode 回归：1072/1072 通过。
- [x] Python 8/8 仓库检查、repository validator、冻结边界、依赖扫描与
  `git diff --check` 通过。
- [x] Unity 版本：6000.3.20f1；Input System：1.17.0。
- [x] 手动 UI/场景验证不适用：本阶段没有运行时面板或 EventSystem。

## 回滚与风险

安全回滚必须同时移除 Input System manifest/lock 条目、恢复 Active Input Handling、删除 Career
输入资源及测试程序集引用，不能只删除动作资源。主要风险是 Stage 8 接线前运行时没有输入消费者；
这是明确延期而非缺失。本次以 `Both` 换取 Match 旧输入兼容，后续若准备完全迁移输入后端，必须在
独立改动中验证并协调 Match。
