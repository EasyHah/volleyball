# 比赛与生涯模块统一 Unity 仓库规划

## 结论

采用**一个 Git 仓库、一个 Unity 工程、一个最终游戏构建**。比赛与生涯不是两个独立 Unity
工程，而是在同一工程中以目录、Assembly Definition 和单向依赖隔离。

这样可以直接共享球员身份、属性配置、UI 基础设施、资源、输入系统和存档版本，也避免两个
Unity 工程之间重复导入资源、同步包版本和跨进程传递比赛结果。

## 目标目录

```text
Assets/VolleyballMatch/
  Shared/
    Runtime/Contracts/       跨模块 ID、MatchContext、MatchResult、版本化 DTO
    Runtime/Foundation/      时间、随机种子、序列化和通用值对象
    Tests/EditMode/
  Match/
    Runtime/Domain/          比分、轮转、回合和可测试的比赛规则
    Runtime/AI/              战术、跑位和动作决策
    Runtime/Presentation/    球、角色、动画、镜头和 Unity 场景适配
    Scenes/
    Tests/EditMode/
    Tests/PlayMode/
  Career/
    Runtime/Domain/          日历、阵容、成长、疲劳、合同和教练信任
    Runtime/Application/     用例编排、存档、训练与比赛调度
    Runtime/Presentation/    生涯 UI、菜单和可视化
    Scenes/
    Tests/EditMode/
    Tests/PlayMode/
  Bootstrap/
    Runtime/                 依赖组装、应用状态机和场景切换
    Scenes/                  Boot、MainMenu、Loading
```

根目录只保留一套 `Packages/` 和 `ProjectSettings/`，并继续固定 Unity
`6000.0.43f1`。当前 `Runtime/`、`Scenes/` 和 `Tests/` 暂时保持原位，等模块边界测试齐全后再
进行机械迁移，避免目录改造和玩法开发同时发生。

## 模块职责

| 模块 | 拥有 | 不拥有 |
| --- | --- | --- |
| Match | 实时物理、回合、规则、动作执行、比赛 AI、统计事实 | 成长结算、合同、赛程和长期存档 |
| Career | 球员档案、阵容、赛程、训练、成长、疲劳、伤病、合同、存档 | 球的逐帧物理和回合判定 |
| Shared | 稳定 ID、不可变输入输出契约、版本号、通用值对象 | 具体界面、场景和模块业务流程 |
| Bootstrap | 应用启动、依赖组装、模块切换、比赛前后交接 | 比赛规则和生涯规则 |

禁止 `Match` 直接引用 `Career`，也禁止 `Career` 直接操作比赛场景对象。两者只能通过
`Shared` 契约和 `Bootstrap` 编排通信。

## 比赛与生涯的数据闭环

```text
Career 选择赛事和阵容
  -> 生成不可变 MatchContext
  -> Bootstrap 加载 Match 场景
  -> Match 只读取本场快照并完成比赛
  -> 产出 MatchResult（比分、统计、负荷、关键事件）
  -> Bootstrap 返回 Career
  -> Career 根据结果更新成长、疲劳、伤病、信任和赛程
  -> 写入版本化存档
```

快速比赛和自动化测试可以由 `Bootstrap` 创建沙盒 `MatchContext`，无需依赖一份真实生涯
存档。比赛过程中不允许生涯系统修改本场球员属性；临场状态必须在开赛前冻结进输入快照。

## Assembly Definition 依赖

```text
VolleyballMatch.Shared
  ^             ^
  |             |
Match.Domain  Career.Domain       （均禁止 UnityEngine 引用）
  ^             ^
  |             |
Match.*       Career.*
       ^       ^
        \     /
       Bootstrap
```

- `Shared` 不引用任何业务模块。
- `Match.Domain` 与 `Career.Domain` 使用 `noEngineReferences`，保证规则可在 EditMode 快速测试。
- Presentation 层可以引用 Unity API，但不能反向成为 Domain 依赖。
- 跨模块事件使用稳定 DTO，不传递 `GameObject`、`MonoBehaviour` 或场景引用。

## 场景流程

第一阶段使用以下状态机：

```text
Boot -> MainMenu -> CareerHome -> MatchLoading -> Match -> MatchSummary -> CareerHome
                    \-> Training / Roster / Calendar / Contract
```

`Boot` 是唯一进入点，持有跨场景的应用状态。生涯和比赛场景均可卸载；存档服务、音频和
加载遮罩由 Bootstrap 管理。开发期间保留 `Physical3v3Rally` 作为可直接运行的比赛沙盒。

## 存档与可重放性

- 生涯存档必须包含 `saveVersion`，迁移器只允许从已知旧版本升级。
- `MatchContext` 和 `MatchResult` 分别包含 `contractVersion`、`sessionId` 和随机种子。
- 比赛结果只记录事实；成长公式、合同判断和长期疲劳结算只在 Career 执行。
- 保存比赛输入和关键事件摘要，使线上问题和随机失误可以用同一种子复现。
- 自动保存采用临时文件写入成功后再替换正式存档，避免中断导致存档损坏。

## 分阶段落地

### 阶段 1：冻结边界

- 把现有 `PlayerId`、能力快照、`MatchContext` 和 `MatchResult` 提升为 Shared 契约。
- 为比赛输入输出补序列化、版本兼容和固定种子测试。
- 保持现有场景路径不变，优先确保比赛原型继续可玩。

### 阶段 2：目录与程序集迁移

- 将现有比赛代码机械迁移到 `Match/`，不在同一提交修改玩法。
- 创建 Career、Shared、Bootstrap asmdef，并加入依赖方向测试。
- 更新测试命令、Build Settings、文档链接和场景路径。

### 阶段 3：生涯最小闭环

- 建立一支球队、六名球员、七天日历和一场可进入的比赛。
- 完成阵容选择、比赛加载、结果返回、属性/疲劳更新和存档读取。
- 先实现确定性规则与占位 UI，再扩展合同、球探、训练和转会。

### 阶段 4：产品化

- 加入完整赛季、AI 球队日程、伤病恢复、合同谈判和多存档槽。
- 为 Windows 构建增加 EditMode、PlayMode、存档迁移和比赛往返烟雾测试。
- 设置 GitHub `main` 分支保护，日常开发通过短分支和 Pull Request 合并。

## 第一批验收标准

- 从 CareerHome 选择阵容后可以进入现有 3v3 场景。
- 比赛结束返回 MatchSummary，再回到 CareerHome，不重启应用。
- 同一个 `MatchContext + seed` 可复现相同 AI 决策序列。
- Career 只根据 `MatchResult` 更新长期状态，Match 不写生涯存档。
- 退出并重新进入后，球队、赛程、成长和疲劳保持一致。
- Match 与 Career 的纯 Domain 测试均无需加载 Unity 场景。
