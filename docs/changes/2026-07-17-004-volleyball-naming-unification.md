# CHG-20260717-004：Volleyball 全项目命名统一

- 日期：2026-07-17
- 状态：已完成
- 负责人：多方
- 影响模块：Match / Career / Shared / Bootstrap / Docs / Build Settings
- 交互级别：跨模块（重点）
- 关联分支：`codex/unify-volleyball-naming`
- 关联提交或 PR：尚未创建

> [!IMPORTANT]
> 本次将游戏根目录、程序集和 C# namespace 从 `VolleyballMatch` 统一为
> `Volleyball`。Match、Career、Shared 和 Bootstrap 的提供方与消费者都必须在同一
> 提交中更新引用；合并后不得继续创建 `VolleyballMatch.*` 类型或程序集引用。

## 改动摘要

`Assets/VolleyballMatch/` 已包含 Match、Career、Shared 和 Bootstrap，实际代表完整
排球游戏而非单一比赛模块。趁 Career 闭环、存档和更多场景尚未落地，统一为
`Assets/Volleyball/`、`Volleyball.*` 程序集和 `Volleyball.*` namespace。

本次是纯命名和路径迁移：不修改比赛玩法、Shared 契约字段、场景序列化内容或模块依赖方向。
历史归档文档保持原始路径以保留历史上下文。

## 具体变更

- 文件、场景、资源或程序集：移动 `Assets/VolleyballMatch/` 及其 `.meta` 到
  `Assets/Volleyball/`，保留根文件夹 GUID 和所有资产 GUID；将 12 个 asmdef 名称和引用
  统一为 `Volleyball.*`。
- 新增或修改的公开类型/字段：仅更新 C# namespace、`using` 和程序集限定名；不改类型名、
  方法签名或 DTO 字段。
- 行为变化：Build Settings 和文档改用新场景路径；场景仍按原 GUID 绑定脚本。

## 跨模块交互重点

- 接口提供方：Shared、Match、Career、Bootstrap
- 接口使用方：Shared、Match、Career、Bootstrap
- 数据方向：`Career -> Shared -> Match -> Shared -> Career`
- 兼容性：不兼容旧的程序集和 namespace 名称；这是开发初期的单提交源码迁移，不提供别名程序集。
- 对方开发者需要做什么：合并后将本地分支的 `VolleyballMatch.*` `using`、asmdef 引用和
  场景路径改为 `Volleyball.*` / `Assets/Volleyball/...`，然后重新导入 Unity 工程。

## 验证

- [x] 迁移前 EditMode 测试：Unity `6000.0.43f1`，100/100 通过。
- [x] 迁移后 EditMode 测试：Unity `6000.0.43f1`，100/100 通过。
- [x] 迁移后 PlayMode 测试：Unity `6000.0.43f1`，3/3 通过，覆盖三个登记场景。
- [x] 场景与 Build Settings 路径验证：三条路径更新至 `Assets/Volleyball/...`，场景 GUID 保持不变。
- [x] 序列化、存档或迁移验证（如适用）：根目录及资产 `.meta` 随移动保留，未改 DTO 或存档字段。

## 回滚与风险

主要风险是遗漏字符串化的程序集、namespace 或场景路径，导致 Unity 编译、测试发现或 Build
Settings 失效。回滚时整体还原这一次命名迁移提交；不要只移动目录而保留新程序集引用，或反之。
