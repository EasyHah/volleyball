# CHG-20260721-005：首里程碑单负责人协作与冻结基线

- 日期：2026-07-21
- 状态：已完成
- 负责人：Career / Shared / Bootstrap / Repository
- 影响模块：Repository / CI / Docs / Shared / Match
- 交互级别：跨模块（重点）
- 关联分支：`chore/collaboration-baseline`
- 关联提交或 PR：本文件所在提交

> [!IMPORTANT]
> 项目负责人授权当前里程碑由一名集成人员全权实现 Career 与共同模块。搭档现有 Match 和 legacy Shared
> V1 不做猜测性修改，而以精确 Git tree 哈希冻结；详细新版契约以后通过兄弟程序集增量提供。

## 改动摘要

在不等待暂不可用搭档确认的情况下建立可执行协作门禁：记录真实 GitHub 所有权，冻结已上传的正式 6v6
Match 与 V1 契约，配置 Unity Smart Merge、Git LFS 和跨平台无 Unity 许可证 CI，并把人工双人门禁改为
项目授权、变更记录、自动化、完整 Unity 回归和独立 agent 审计。此变更不修改任何比赛玩法或现有契约。

## 具体变更

- `.github/CODEOWNERS`：`@hoshisora1` 负责 Career 与共同区域，`@EasyHah` 保留 Match 所有权。
- `.github/workflows/repository-validation.yml` 与 `tools/`：检查 `.meta`、GUID、asmdef、CHG、Unity/包锁、
  Git 属性、冻结树和工作区非变异性；Ubuntu/Windows 均运行。
- `.gitattributes`：Unity YAML 使用 Smart Merge；二进制资源使用 Git LFS，源格式标记 `lockable`。
- `docs/`：记录临时单负责人授权、远端分支保护限制、V1/V2 隔离策略及后续物理 Match 延期边界。
- 冻结 `Assets/Volleyball/Match` tree `1f0bbe976355ded867dcefadba61d538f77905b9`，冻结 legacy
  `Assets/Volleyball/Shared/Runtime` tree `61c7a928f2bf4740defea34c67e5cb108f6dfe76`。
- 同时冻结 `Match.meta` blob `23d5e66a3e4158bd421c4d3ee573e0d4e7339627` 与
  `Shared/Runtime.meta` blob `9085d85a3a423a82a6303df4ca3fe3819d8d30ea`；CI 还直接比较 PR base，
  因此不能用同一提交修改 policy 期望值绕过冻结。
- LFS 规则覆盖混合大小写扩展名；检查器验证 HEAD/index 中受管 blob 是严格 LFS pointer，并拒绝以删除
  旧 CHG 冒充当前变更记录。

## 跨模块交互重点

- 接口提供方：现有 Shared V1；后续 `Volleyball.Shared.MatchV2`。
- 接口使用方：现有 Match V1 消费者；后续 Career.MatchIntegration 与 FakeMatch。
- 数据方向：当前无运行时变化；后续 `Career -> Shared V2 -> FakeMatch -> Shared V2 -> Career`。
- 兼容性：完全保留 V1；`ContractVersions.SupportsMatch(2)` 不得被改为接受 V2。新版类型使用独立兄弟
  程序集和命名空间。
- 消费方或后续负责人需要做什么：当前 Match 无需改代码。物理 Match 的 V2 producer、能力映射、AI seed
  与详细技术事件全部延期，恢复协作后另立变更。

## 验证

- [x] 仓库验证器 `8/8` 单元测试全绿，覆盖缺失/重复 meta、asmdef 循环、CHG 索引与删除绕过、
  未跟踪预提交文件以及 LFS raw blob 拒绝。
- [x] `python -B tools/validate_repository.py --base d603219` 通过；检查器包含工作树但不改写文件。
- [x] UnityYAMLMerge driver 与 Git LFS 的仓库本地精确配置检查通过，Git LFS `3.5.1` 可执行。
- [x] UnityYAMLMerge 对现有 `FormalIndoor6v6.unity` 做相同三方输入烟测：退出码 `0`、无冲突标记，
  除 Unity 空值行尾随空格规范化外语义逐行一致。
- [x] Match、Match.meta、legacy Shared V1 Runtime 及其 meta 哈希精确匹配冻结值，并通过相对
  `d603219` 的独立零差异检查。
- [x] 独立 agent 首轮发现 2 个 P1/5 个 P2 后已全部修复；两路定点复核最终无剩余 P1/P2。

2026-07-21 通过已认证 GitHub 连接再次核对远端：`origin/main` 最新仍为 `4bf9e4b`，没有更新分支或 PR
需要纳入本阶段；当前账号 `@hoshisora1` 具备 WRITE 但非 ADMIN，因此没有伪造远端 ruleset 已启用。

阶段 0B 不改运行时代码，因此不重复执行刚在 Unity 升级提交上完成的 `224/224` EditMode、`11/11`
PlayMode 和 Windows Mono Development 构建；冻结哈希证明 Match 与 V1 没有变化。进入首个功能提交前会再按
风险运行完整 Unity 回归。

## 回滚与风险

主要风险是错误的跨平台 shell、过严的 asmdef 例外或错误 Smart Merge 范围让 CI 误报。检查器自身以
stdlib 单元测试覆盖，CI 在 Ubuntu/Windows 双跑；`.meta` 明确不用 Smart Merge。远端 `main` 当前没有
ruleset，且当前写权限账号不能设置必需检查；这是仓库管理员 `@EasyHah` 后续动作，不阻塞本地里程碑，
也不得在文档中宣称远端保护已启用。回滚时可整体撤销本变更，不影响 Match 或 V1 运行时内容。
