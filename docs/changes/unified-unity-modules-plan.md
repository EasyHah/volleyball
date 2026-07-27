# 比赛与生涯模块统一 Unity 仓库规划

> 2026-07-27 更新：单仓库、单 Unity 工程和模块化依赖结论不变；公共比赛契约以
> 搭档维护的 `Volleyball.Shared` V3 为唯一权威，不再保留 Career 侧创建的
> `Shared.MatchV2` 兄弟程序集。完整决策见
> [CHG-20260727-001](2026-07-27-001-career-shared-v3-integration.md)。

## 结论

采用**一个 Git 仓库、一个 Unity 工程、一个最终游戏构建**。比赛与生涯不是两个独立 Unity
工程，而是在同一工程中以目录、Assembly Definition 和单向依赖隔离。

这样可以直接共享球员身份、属性配置、UI 基础设施、资源、输入系统和存档版本，也避免两个
Unity 工程之间重复导入资源、同步包版本和跨进程传递比赛结果。

## 目标目录

```text
Assets/Volleyball/
  Shared/
    Runtime/Contracts/       冻结的稳定 ID 与 legacy V1 DTO
    Runtime/Foundation/      冻结的 legacy V1 基础设施
    Tests/EditMode/
    MatchV2/
      Runtime/               V2 DTO、严格 codec、哈希与 IMatchRunnerV2
      Tests/EditMode/        V2 golden bytes/hash 与严格解析测试
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
    Runtime/MatchIntegration/ Career/V2 映射与首里程碑 Fake runner
    Runtime/Persistence/     本地快照、CAS、恢复；阶段 5 后可验证 V2 持久载荷
    Scenes/
    Tests/EditMode/
    Tests/PlayMode/
  Bootstrap/
    Runtime/                 依赖组装、应用状态机和场景切换
    Scenes/                  Boot、MainMenu、Loading
```

根目录只保留一套 `Packages/` 和 `ProjectSettings/`，实施基线锁定 Unity `6000.3.20f1`；
`origin/main@4bf9e4b` 同时作为当前里程碑的 Match 冻结锚点，其 `Assets/Volleyball/Match` tree 为
`1f0bbe976355ded867dcefadba61d538f77905b9`。截至该锚点，比赛代码、场景和测试已经迁移到
`Match/`，Career 与 Bootstrap 的程序集骨架也已建立；Match 随后完成了可完局的 3v3、共用物理 Director
和正式室内 6v6 单局，但 Career/Bootstrap 往返仍未接线。详细当前顺序以
`docs/career-development-roadmap.md` 为准。

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
Volleyball.Shared
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

首里程碑的详细比赛契约不改动上述 legacy V1，而新增兄弟程序集 `Volleyball.Shared.MatchV2`；它只被
阶段 5 后的 `Career.Persistence` 持久载荷层、`Career.MatchIntegration`/Fake runner 和对应测试引用。
Bootstrap 只引用 MatchIntegration 的公开装配入口，不直接处理 V2 DTO。物理 Match 的 V2 producer 延期，
不能为满足依赖图而让 Career 引用 Match 内部程序集，也不能让 V1 验证器接受 V2。

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
加载遮罩由 Bootstrap 管理。开发期间保留 `Physical3v3Rally` 和 `FormalIndoor6v6` 作为可直接运行的
比赛沙盒；后者当前仍创建硬编码上下文，不是 Career 的正式入口。

## 存档与可重放性

- 生涯存档必须包含 `saveVersion`，迁移器只允许从已知旧版本升级。
- `MatchContext` 和 `MatchResult` 分别包含 `contractVersion`、`sessionId` 和随机种子。
- 比赛结果只记录事实；成长公式、合同判断和长期疲劳结算只在 Career 执行。
- 保存比赛输入和关键事件摘要，使线上问题和随机失误可以用同一种子复现。
- 自动保存采用临时文件写入成功后再替换正式存档，避免中断导致存档损坏。

## 分阶段落地

### 阶段 1：冻结边界

- 在 Shared 新增稳定 `PlayerId`/`TeamId`；现有蓝/橙方 `PlayerId` 仅作为场上槽位，不能写入存档。
- 把能力快照、`MatchContext` 和 `MatchResult` 提升为 Shared 契约。
- 为比赛输入输出补序列化、版本兼容和固定种子测试。
- 完成状态：Shared 契约、稳定 ID、版本/哈希校验和边界测试已落地。

### 阶段 2：目录与程序集迁移

- 将现有比赛代码机械迁移到 `Match/`，不在同一提交修改玩法。
- 创建 Career、Shared、Bootstrap asmdef，并加入依赖方向测试。
- 更新测试命令、Build Settings、文档链接和场景路径。
- 完成状态：目录、程序集、Build Settings 和依赖方向测试已落地；原有代码 namespace 暂不改名，降低场景脚本失联风险。

### 阶段 3：生涯最小闭环

- 建立一支球队、六名球员、七天日历和一场可进入的比赛。
- 完成阵容选择、比赛加载、结果返回、属性/疲劳更新和存档读取。
- 先实现确定性规则与占位 UI，再扩展合同、球探、训练和转会。

### 阶段 4：产品化

- 加入完整赛季、AI 球队日程、伤病恢复、合同谈判和多存档槽。
- 为 Windows 构建增加 EditMode、PlayMode、存档迁移和比赛往返烟雾测试。
- 设置 GitHub `main` 分支保护，日常开发通过短分支和 Pull Request 合并。

## 第一批验收标准

首个 Career 一周技术闭环先使用与正式 6v6 相同的 12 人 fixture 完成 FakeMatch、存档和幂等结算；该
闭环完成后的物理场景接入再满足以下跨模块验收：

- 从 CareerHome 选择双方各六人的阵容后可以进入现有 `FormalIndoor6v6` 场景。
- 场景使用上下文中的稳定身份和能力，不按位置模板覆盖生涯映射值；主角控制权与 AI 边界明确。
- 比赛结束返回 MatchSummary，再回到 CareerHome，不重启应用。
- 物理接入前，`MatchContext.seed` 必须派生并传入所有使用随机性的 AI planner；纯确定性 planner 删除
  未使用的 seed 参数。相同输入与 seed 可复现相同纯 AI 决策序列，随机路径至少有一组不同 seed 的
  定向差异测试。该承诺不扩展为 Unity 物理逐帧或最终比分复现。
- Career 只根据 `MatchResult` 更新长期状态，Match 不写生涯存档。
- 退出并重新进入后，球队、赛程、成长和疲劳保持一致。
- Match 与 Career 的纯 Domain 测试均无需加载 Unity 场景。
