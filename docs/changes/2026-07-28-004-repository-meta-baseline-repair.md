# CHG-20260728-004：合并后 Unity 目录元数据与冻结基线修复

- 日期：2026-07-28
- 状态：已自动验证
- 负责人：多方
- 影响模块：Repository / Bootstrap / Career / Match / Docs
- 交互级别：跨模块（重点）
- 关联分支：`fix/repository-meta-baseline`
- 关联提交或 PR：本修复分支 Pull Request

> [!IMPORTANT]
> 本次修复不修改运行时代码、场景内容、程序集或 Career × Match 公共契约。Bootstrap
> 与 Career 恢复合并前已使用的目录 GUID；Match 仅确认废弃空 Replay 目录的删除结果，
> 并让仓库冻结基线与当前权威树一致。

## 改动摘要

PR #2 合并前删除了四个目录 `.meta`。其中三个目录仍包含受 Git 管理的场景或测试内容，
删除后会破坏 Unity 目录元数据配对；旧 `Match/Runtime/Domain/Replay` 已无现行文件，
其 `.meta` 删除可以保留，但仓库策略仍保存着删除前的 Match 树哈希。

本次改动恢复三个有效目录的原始 GUID，并把 Match 冻结哈希对齐到已经合并的权威树。
不恢复废弃 Replay 目录，也不新增运行时功能。

## 具体变更

- 文件、场景、资源或程序集：
  - 原样恢复 `Assets/Volleyball/Bootstrap/Scenes.meta`；
  - 原样恢复 `Assets/Volleyball/Career/Scenes.meta`；
  - 原样恢复 `Assets/Volleyball/Career/Tests/PlayMode.meta`；
  - 更新 `tools/repository_policy.json` 中的 Match 冻结树哈希；
  - 更新本改动记录及索引。
- 新增或修改的公开类型/字段：无。
- 行为变化：仓库校验可正确识别现存 Unity 目录；游戏运行时行为不变。

## 跨模块交互重点

- 接口提供方：无。
- 接口使用方：无。
- 数据方向：无。
- 兼容性：兼容；保留三个目录合并前的原 GUID，不产生资源引用迁移。
- 消费方或后续负责人需要做什么：无需改代码；后续 Match 如重新建立专用 Replay
  目录，应由 Match 负责人同时提交目录内容、`.meta` 和新的冻结基线决策。

## 验证

- [x] 仓库校验器单元测试：`8/8`。
- [x] `validate_repository.py --base origin/main`：通过。
- [x] `git diff --check` 与最终范围检查：通过。

不运行 Unity EditMode、PlayMode 或 Windows 构建：本次没有修改 C#、场景内容、程序集、
包、项目设置或 Player 行为。

## 回滚与风险

主要风险是误换目录 GUID 或冻结哈希与 Git 树不一致。验证通过后，回滚本修复提交即可
恢复合并后的状态；不涉及存档或数据迁移。
