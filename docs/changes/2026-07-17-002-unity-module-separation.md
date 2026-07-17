# CHG-20260717-002：Unity 模块目录与程序集迁移

- 日期：2026-07-17
- 状态：已完成
- 负责人：Match / Career / Bootstrap
- 影响模块：Match / Career / Shared / Bootstrap
- 交互级别：跨模块（重点）
- 关联分支：`codex/shared-contract-boundary`
- 关联提交或 PR：`09f5318`

> [!IMPORTANT]
> Match 场景、代码和测试已移入 `Assets/VolleyballMatch/Match/`，Match 程序集名称增加
> `VolleyballMatch.Match.*` 前缀，但现有 C# namespace 暂时不变。Career 开发者应引用
> Shared 契约；只有 Bootstrap 可以同时组装 Match 与 Career 的运行时入口。

## 改动摘要

在一个 Unity 工程内建立 Match、Career、Shared 和 Bootstrap 四个明确模块。迁移保留所有
Unity `.meta` GUID，没有修改比赛动作、球路、AI 或物理行为。

## 具体变更

- Match 运行时代码：`Assets/VolleyballMatch/Match/Runtime/`
- Match 场景：`Assets/VolleyballMatch/Match/Scenes/`
- Match 测试：`Assets/VolleyballMatch/Match/Tests/`
- Career 骨架：Domain、Application、Presentation 与测试程序集
- Bootstrap 骨架：跨模块依赖组装程序集及未来入口场景目录
- Build Settings 已更新三个 Match 场景的新路径
- 新增程序集依赖方向测试，禁止 Match 反向引用 Career

## 跨模块交互重点

- 接口提供方：Shared 提供契约；Bootstrap 提供组装位置
- 接口使用方：Match / Career
- 数据方向：`Career -> Shared -> Bootstrap -> Match`，结果按反方向返回
- 兼容性：场景和源码路径发生变化；GUID 保持不变。程序集名称发生变化，C# namespace
  保持不变
- 对方开发者需要做什么：新 asmdef 引用必须使用 `VolleyballMatch.Match.Domain`、
  `VolleyballMatch.Match.AI`、`VolleyballMatch.Match.Presentation`；打开场景时使用新路径。
  Career 代码不得直接引用 Match 场景对象

## 验证

- Unity：`6000.0.43f1`
- EditMode：100/100 通过
- PlayMode：3/3 通过
- 三个原型场景均从 `Match/Scenes` 新路径成功加载

## 回滚与风险

主要风险是外部文档或未纳入仓库的本地工具仍使用旧路径。仓库内路径已经统一替换；如需
回滚必须同时恢复 Build Settings、asmdef 名称和目录，不可只移动场景文件。
