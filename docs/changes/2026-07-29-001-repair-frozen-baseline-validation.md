# CHG-20260729-001：恢复冻结基线校验

- 日期：2026-07-29
- 状态：已自动验证
- 负责人：Repository / CI
- 影响模块：Repository / CI / Match / Shared / Docs
- 交互级别：跨模块（重点）

> [!IMPORTANT]
> 本次只将仓库策略中的冻结树哈希对齐到已经合并的 `main` 树；不修改任何
> Match 或 Shared 源码、公共契约或运行时行为。

## 改动摘要

`repository_policy.json` 仍保存早于已合并 Full Rally V4 和防守接触延续工作的
Match/Shared Runtime 树哈希，导致 Repository Validation 在所有平台失败。更新这两个
策略哈希，使其匹配当前已批准的 `main` 基线。

## 跨模块交互重点

- 接口提供方：无。
- 接口使用方：Repository Validation。
- 数据方向：已合并 Git tree -> repository policy frozen tree hash -> CI validation。
- 兼容性：不改变 Match、Shared 或 Career 的运行时输入输出。
- 消费方或后续负责人需要做什么：后续获准合并 Match 或 Shared Runtime 改动时，必须通过独立变更
  同步更新冻结基线和本地/CI 校验；不得把策略哈希作为绕过冻结规则的手段。

## 验证

- [x] `python3 -B tools/validate_repository.py --base 5ea4619`。
- [x] `python3 -B tools/validate_repository.py --base 495df70`。
- [x] `git diff --check 5ea4619 HEAD`。

## 回滚与风险

回滚本改动会恢复旧的、无法验证当前主线的哈希。风险仅在于错误对齐未经批准的树；本次值直接来自
当前 `main` 已合并树。
