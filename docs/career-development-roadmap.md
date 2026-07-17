# 球员生涯开发路线图

- 状态：待双方评审
- 适用范围：离线首版的首个一周技术闭环
- Unity 基线目标：`6000.3.20f1`
- 发布日期：无硬性日期；完成存档模型与本地持久化后再依据实际速度估算

## 1. 文档职责

本文只规定开发顺序、依赖关系、分支边界、负责人和阶段门禁。具体页面与交互写入
`career-vertical-slice-spec.md`，领域数值与不变量写入 `career-domain-rules.md`，存档提交与故障恢复写入
`career-save-and-recovery.md`。项目级模块边界继续以 `docs/changes/unified-unity-modules-plan.md` 和既有架构基线为准。

当前只交付一个可保存、可恢复的一周技术闭环：

```text
创建/选择本地档案
  -> 创建/选择球员生涯
  -> 大学试训
  -> 第一周规划（两个自由行动槽 + 一个比赛预留槽）
  -> 执行行动与一个社交事件样例
  -> 赛前重点
  -> FakeMatch 固定比赛事实
  -> 原子结算并进入第二周 Planning
  -> 退出并恢复完全相同的状态
```

一周闭环只证明技术管线完整，不代表长期玩法已经验证。后续可另立“6 周大学玩法验证”里程碑，检验行动取舍和成长反馈；本路线不预先规定其首发竞争、赛程或事件细节。

## 2. 总体依赖关系

```text
路线文档评审
  -> Unity 6000.3.20f1 独立升级
      -> 协作基础设施基线
          -> 重放并合并 career-week-plan
              -> career-save-model
                  -> career-local-persistence
                      -> career-player-tryout
                          -> career-week-execution
                              -> Shared 契约联合门禁
                                  -> career-fake-match
                                      -> Input System 独立基线
                                          -> career-ui-vertical-slice
                                              -> career-recovery-build
```

URP 必须在 Unity 升级完成后使用独立分支迁移，由 Match 负责人主导。它不阻塞纯 C# 的 Career Domain、Application 与 Persistence 工作，但必须在依赖 URP 的比赛场景或最终集成构建验收前完成。NavMesh 只有在 Match AI 出现真实调用方时才立项。

任何阶段未达到退出条件，都不得用下一阶段的功能来掩盖缺口，也不得把两个阶段合并成大型 PR。

## 3. 前置基线

### 3.1 路线文档门禁

建议分支：`docs/career-development-roadmap`

进入条件：本轮产品与技术取舍已经确认。

退出条件：

- 四份生涯开发文档职责互不冲突，并经过 agent 复核；
- Career 与 Match 两位负责人确认路线、跨模块门禁和验收范围；
- 文档 PR 不夹带功能代码、包升级或资源重导入。

### 3.2 待对齐的周计划前置切片

`feature/career-week-plan` 已实现固定三个行动槽、比赛槽保护、确认锁定和稳定行动 ID，并在 Unity
`6000.0.43f1` 上通过完整 EditMode 测试；但它当前仍把 `Academics`、`Social` 作为常驻行动，测试也
明确允许安排它们。这与已确认的“四类常驻行动；学业和社交只通过事件出现”冲突，因此不能按原样合并。

Unity 升级合并后，将该分支变基到最新 `main`，直接删除 `Academics`、`Social` 枚举值，并把相关测试
改为只接受专项训练、力量训练、团队合练、休息和系统预留的比赛。该分支尚未形成需要兼容的正式存档，
因此不保留“可反序列化但在领域入口拒绝”的旧枚举值。`CHG-005` 必须
同步记录这次需求对齐；之后使用 `6000.3.20f1` 重跑完整 EditMode。只有代码、测试、变更记录和新基线
证据全部一致才能合并，不得把旧枚举保留为“以后可能使用”的无调用方占位。

### 3.3 当前 Career 耦合与目标程序集依赖

当前 `CareerPlayerRecord` 直接保存七项浮点 `PlayerAbilitySnapshotV1`，`Career.Application` 也直接暴露
同步 `IMatchGateway.Play`。它们是原型代码，不是本里程碑的正式领域模型或场景生命周期接口。正式目标
依赖如下：

| 程序集 | Unity API | 允许依赖 |
| --- | --- | --- |
| `Volleyball.Shared` | 禁止 | 无业务模块依赖；只放稳定跨模块 ID、版本化比赛 DTO 与 `IMatchRunnerV*` 异步执行契约 |
| `Volleyball.Career.Domain` | 禁止 | `Volleyball.Shared` 中的稳定 ID；禁止引用 `MatchContextV*`、`MatchResultV*`、`PlayerAbilitySnapshotV*` |
| `Volleyball.Career.Application` | 禁止 | `Volleyball.Career.Domain`；定义用例、仓储端口和 Career 自有比赛端口 |
| `Volleyball.Career.Persistence` | 禁止 | `Volleyball.Career.Domain`、`Volleyball.Career.Application`；路径由外部注入 |
| `Volleyball.Career.Content` | 允许 | `Volleyball.Career.Domain`；只把 ScriptableObject 映射为纯 C# 配置 |
| `Volleyball.Career.MatchIntegration` | 允许 | Career Domain/Application 与 `Volleyball.Shared`；独占 Career/Shared DTO 映射，不引用 Match 内部 Domain |
| `Volleyball.Career.Presentation` | 允许 | Career Domain/Application；不得直接写存档或 Shared 比赛 DTO |
| `Volleyball.Bootstrap` | 允许 | 组装双方公开适配器、场景和应用状态机，不承载领域规则 |

Career Domain 建立自有的八项 `0–10000` 整数能力模型；Shared 比赛能力只在
`Career.MatchIntegration` 中转换。允许 Domain 复用 Shared 的稳定 `PlayerId/TeamId`，但模块边界测试
必须禁止任何 Shared 比赛 DTO 回流到 Domain/Application。

阶段 1 直接删除当前 `CareerMatchRequest` 与同步 `IMatchGateway.Play`，只允许先定义尚未接线的 Career 自有
异步端口，例如 `Task<CareerMatchFacts> ExecuteAsync(CareerMatchLaunch, CancellationToken)`。请求和返回
都是 Career 自有类型。Shared 另行定义版本化 `IMatchRunnerV*`，只接收/返回 Shared DTO；FakeMatch 与未来
真实比赛各自实现该契约。`Career.MatchIntegration` 依赖注入 `IMatchRunnerV*`，负责 Career/Shared 映射并
实现 Career 端口；Bootstrap 只组装实现，不复制映射规则。正式流程调用前必须已经耐久提交
`PendingMatch`；取消或场景加载失败只返回错误并保留待处理比赛，不能伪造比赛结果。

## 4. 实施阶段

### 阶段 0A：锁定 Unity Editor

- 建议分支：`chore/unity-6000.3.20f1`
- 负责人：双方共同确认，一人执行

进入条件：路线文档已合并；两台开发机可安装精确版本；当前 `main` 的测试基线已记录。

范围：只升级 Editor、必要的项目设置、包锁与兼容修复。不得同时迁移 URP、引入 Input System、修改玩法、调整 Shared 契约或批量整理资源。

版本依据：[Unity 6000.3.20f1 官方发布说明](https://unity.com/releases/editor/whats-new/6000.3.20f1)；
支持周期以 [Unity 6 官方支持页面](https://unity.com/releases/unity-6/support) 为准。

退出条件：

- `ProjectVersion.txt` 精确锁定 `6000.3.20f1`，README、开发命令和变更记录一致；
- 项目完成重导入且不存在由升级造成的编译错误；
- 完整 EditMode、PlayMode 与 Windows x64 开发构建通过；
- 两位负责人确认场景、Prefab 与包锁中的变化都是升级所必需。

### 阶段 0B：协作基础设施基线

- 建议分支：`chore/collaboration-baseline`
- 负责人：双方

进入条件：Unity `6000.3.20f1` 升级已合并，避免基础设施分支同时携带项目重导入差异。

范围：在功能开发前落地 `CODEOWNERS`、UnityYAMLMerge、Git LFS/锁约定和不需要 Unity 许可证的基础
CI。当前仓库只有最小 `.gitattributes`，Unity Windows workflow 整个 job 仍为禁用状态，不能把它们写成
已经完成。

退出条件：

- `CODEOWNERS` 使用双方真实 GitHub handle，Career/Match 各自目录归对应负责人，Shared、Bootstrap、
  Packages、ProjectSettings、公共场景和 workflow 要求双方评审；
- `.unity`、`.prefab`、`.asset` 配置 UnityYAMLMerge，并在两台开发机验证相同的 merge driver；大型二进制
  资源进入 Git LFS，需要独占编辑的源资产标记 `lockable`，YAML 资产不进入 LFS；
- 启用独立、非 `if: false` 的无许可证 CI job，至少执行变更记录检查、`.meta` 配对、asmdef JSON/依赖方向、
  禁止业务代码落入 `Assembly-CSharp` 和 `git diff --check`；
- 两位负责人验证分支保护所需检查名称，并把本地安装/锁文件规则写入 `docs/development.md`。

### 阶段 1：正式存档模型

- 建议分支：`feature/career-save-model`
- 负责人：Career

进入条件：Unity 升级与协作基础设施已合并；`career-week-plan` 已完成需求对齐、在新版本上重放测试并
合并。

范围：建立最小但正式的 `CareerSaveSnapshot`、版本字段、`revision`、周状态、操作回执与必要稳定 ID，
并把 `CareerPlayerRecord` 从 `PlayerAbilitySnapshotV1` 解耦为 Career 自有八项整数属性模型。删除当前
`CareerMatchRequest` 与同步 `IMatchGateway.Play`；若为了后续编译先建立 Career 自有异步端口，本阶段不得
接线或调用它。
字段只覆盖此时已有调用方的创建、Planning 和行动执行状态，但结构不得是一次性临时存档。正式
`PendingMatch` 与 `SettlementReceipt` 依赖新版 Shared 契约，在阶段 6 以显式 schema 升级加入；本阶段
不得用原始 JSON、空接口或猜测字段提前占位。

退出条件：

- 模型能表达从新建生涯到比赛槽执行前的每个权威状态；
- Domain/Application 不再引用 Shared 的能力、比赛上下文或比赛结果 DTO；
- 周状态与操作回执可以证明行动恢复和幂等行为；
- 序列化映射、版本默认值与非法状态均有 EditMode 测试；
- 没有磁盘 I/O、UI、场景或 Match 内部引用进入 Domain/Application 模型。

### 阶段 2：本地档案与持久化

- 建议分支：`feature/career-local-persistence`
- 负责人：Career

进入条件：存档模型已合并且 Schema 字段通过评审。

范围：多本地档案、每档案多条生涯、版本化 JSON、完整版本令牌 CAS、同卷原子替换、唯一上一修订
备份、损坏隔离和基本本地诊断。正式账号、密码、云同步和跨电脑识别不在此阶段。

退出条件：

- 创建、列举、保存、载入和重建索引均可在纯测试中往返；
- 损坏主档、有效/无效备份、遗留临时文件、版本令牌冲突和替换失败均有故障注入测试；
- 恢复不会静默覆盖损坏原件，也不会静默新建档案掩盖错误；
- 自动保存与 Planning 状态下“立即保存”均走同一提交边界。

完成本阶段后，根据实际工作量首次评估后续排期；评估不是发布日期承诺。

### 阶段 3：新建球员与大学试训

- 建议分支：`feature/career-player-tryout`
- 负责人：Career

进入条件：本地档案和生涯快照可稳定创建、提交和恢复。

范围：最小新建字段、固定主攻位置、三次试训选择、Career 自有八项初始属性、可见综合潜力等级、初始
教练信任与心态。静态数值从带稳定业务 ID 和版本的内容配置进入纯 C# 规则。第三段提交直接建立入队和
第一周 `Planning`；结果页“继续”只导航，不产生额外 revision。

退出条件：

- 同一 `careerSeed`、选择和版本产生相同初始结果；
- 创建过程中断不会留下无法恢复的半条生涯；
- 玩家不能直接选择打法评价，复杂潜力揭示与逐项上限未被偷偷引入；
- Domain 不使用 `UnityEngine.Random` 或依赖调用顺序的 `System.Random`。

### 阶段 4：第一周执行与事件管线

- 建议分支：`feature/career-week-execution`
- 负责人：Career

进入条件：试训结果能够保存并进入大学第一赛季第一周 Planning。

范围：两个自由行动槽、第三槽比赛预留、四类常驻行动、疲劳/心态/信任/成长的最小真实后果、一个
社交事件样例，以及截至比赛槽执行前的周状态转换。事件随机必须可复现且不可通过反复读档刷新。
周末规则可以先作为纯 Domain 计算单测，但其持久化和第二周初始化必须等待阶段 6 的比赛闭环。

退出条件：

- 每种常驻行动都有可解释且合法的结算结果；
- 多次专项/力量训练按 `trainingEmphasis` 聚合规则产生唯一结果；同方向递减/上限、不同方向共存、
  团队合练不生成 emphasis、冻结、消费和清除都有 EditMode 测试，且不与 `preMatchPriority` 混名；
- 确认计划、执行自由槽和处理事件的每个持久化边界均有提交前后失败测试；
- 已应用的行动和事件不会重复，未应用的步骤不会被跳过；
- 完成比赛前步骤后能稳定停在“比赛槽尚未执行”的权威状态，但在 Shared 门禁前不创建
  `PendingMatch`。

### 阶段 5：Shared 契约联合门禁

- 建议分支：独立的跨模块 `feature/shared-match-career-contract`
- 负责人：双方

进入条件：前四个 Career 阶段不再依赖猜测性比赛字段；Career 提供真实消费需求，Match 提供可生成事实。

范围：集中确认并升级一次 Shared 契约、版本、冻结上下文、比赛统计、规范哈希、固定 fixture 和
`IMatchRunnerV*` 异步执行契约，并建立 `Career.MatchIntegration` 的双向 DTO 映射及适配器。Career 与
Match 均不得在各自功能分支里私自追加 Shared 字段。本阶段只做适配器/fixture 级验证，不从生涯流程
启动比赛。

退出条件：

- 两位负责人共同批准字段定义、计数口径和兼容策略；
- 两个模块分别读取同一组 golden fixture 并通过契约与哈希测试；
- FakeMatch runner 的 fixture 结果能经适配器转换为 Career 自有事实，但尚不由生涯流程调用；同步
  `IMatchGateway.Play` 已在阶段 1 删除；
- Career 只准备输入和计算长期后果，Match 只执行比赛/快速模拟并报告事实；
- 变更记录标记为 `跨模块（重点）`，并写明两端升级动作。

### 阶段 6：FakeMatch 与幂等结算

- 建议分支：`feature/career-fake-match`
- 负责人：Career；契约行为由双方复核

进入条件：Shared 契约升级已合并；固定上下文与结果 fixture 可用。

范围：以显式 schema 升级加入正式 `PendingMatch` 和 `SettlementReceipt`，并完成赛前重点、
FakeMatch 固定事实、结果哈希校验、回执查询和从比赛前重试。只有 `PendingMatch` 成功提交后，生涯流程
才首次通过 Career 异步端口调用 FakeMatch runner。第一周的比赛结算、全部周末后果与
第二周初始化必须生成同一版下一快照并一次原子提交。FakeMatch 是可替换的开发期 runner 实现，不包含
真实物理或快速模拟算法。

退出条件：

- 同一结果重复提交返回原回执，不重复应用成长、疲劳、信任或周推进；
- 同一 `sessionId` 的不同上下文/结果哈希明确冲突并保留证据；
- 首次结算在一个新修订中同时记录事实、应用比赛及周末后果、写入回执、清除 `PendingMatch` 并进入
  第二周 `Planning`；
- 写盘前后故障恢复结果均满足既定权威状态。

### 阶段 7：Input System 基线

- 建议分支：`chore/input-system-baseline`
- 负责人：双方

进入条件：Unity 升级稳定；UI 垂直切片即将开始。

范围：独立加入官方 Input System 包和共用输入资源；Career 与 Match 使用各自 Action Map。首轮只定义键鼠与 XInput 手柄需要的导航、确认、取消、返回和页面切换，不制作按键重绑定。

退出条件：

- 包与输入资源变更经过双方确认；
- 现有 Match 操作没有未说明的回归；
- Career 的最小焦点导航测试夹具可用。

### 阶段 8：UI 垂直切片

- 建议分支：`feature/career-ui-vertical-slice`
- 负责人：Career

进入条件：从本地档案到 FakeMatch 结算的无 UI 用例已经全绿；Input System 基线已合并。

范围：只实现完成一周闭环所需的七类 UI、简体中文文案资源、键鼠与手柄导航、保存状态提示以及仅 Editor/Development Build 可见的诊断面板。视觉采用统一开发期样式，不进行正式美术生产。

退出条件：

- 1280×720 不裁切关键操作，1920×1080 完成主要验证；
- 仅键鼠和仅 XInput 手柄都能走完闭环，焦点、弹窗、滚动和返回行为有 PlayMode 覆盖；
- UI 只提交应用命令，不直接改写生涯状态或磁盘文件；
- 退出并重启后进入第二周 Planning，所有显示与权威快照一致。

### 阶段 9：恢复、回归与 Windows 构建

- 建议分支：`feature/career-recovery-build`
- 负责人：Career 主导，双方验收

进入条件：七类 UI 已能在 Editor 中完成闭环，所有前置变更记录均已完成。

范围：集中验证中断恢复、诊断导出、完整回归和 Windows x64 开发构建，不新增玩法。

退出条件：

- EditMode、相关 PlayMode、契约 fixture 与程序集边界检查全部通过；
- 强制中断覆盖新建、临时写入、原子替换、比赛待处理和结算后的关键窗口；
- Windows 10/11 x64 实机完成两次独立闭环试玩（两位负责人各一次）；
- 1920×1080 的菜单交互无明显停顿，自动保存不会无提示冻结界面；
- 所有测试版本、结果和已知限制写入变更记录，失败门禁不得标记为完成。

## 5. 独立基础设施迁移

### URP

建议分支：`chore/urp-baseline`。必须在 Unity `6000.3.20f1` 升级之后进行，由 Match 负责人主导，双方验证场景、材质、相机和构建。不得与 Input System、Career UI 或比赛玩法改动混合。若首个一周闭环仍可使用现有渲染基线，URP 不阻塞纯生涯阶段；但任何依赖 URP 的集成验收必须等待该分支完成。

### CI

CI 按能力分阶段推进：

1. 阶段 0B 启用独立且不受 Unity job 开关影响的无许可证检查：变更记录、`.meta` 完整性、程序集
   引用方向、默认程序集污染和基础结构检查；
2. 在固定执行环境可用前，每个分支由开发者使用精确 Unity 版本本地跑测试，并把版本与结果写入变更记录；
3. Shared 契约升级前，建立可用的 Windows Unity 测试环境，至少自动执行编译、程序集边界、EditMode 和契约 fixture；
4. UI 或场景变更追加 PlayMode；最终阶段由 CI 或固定 Windows 测试机执行 EditMode、PlayMode 与 Windows x64 构建。

CI 暂不可用不等于测试门禁可跳过；只能用可复现的本地/固定测试机证据临时替代。

### Windows 性能基线

正式 Windows 性能基线为 Windows 10/11 x64、16 GB 内存、GTX 1650 级独立显卡、`1920×1080`，
真实比赛目标 `60 FPS`。首个里程碑只包含 FakeMatch，因此当前只验证菜单交互和保存不出现明显停顿；
`60 FPS` 在真实比赛首次接入时才成为硬门禁。4K、超宽屏和低端集成显卡专项优化留待后续。

## 6. 所有权与合并规则

| 区域 | 主要负责人 | 合并要求 |
| --- | --- | --- |
| `Assets/Volleyball/Career/**` | 生涯开发者 | 生涯负责人评审；不得反向依赖 Match 内部代码 |
| `Assets/Volleyball/Match/**` | 比赛开发者 | 比赛负责人评审；不得写生涯长期状态 |
| `Assets/Volleyball/Shared/**`、`Assets/Volleyball/Bootstrap/**`、`Packages/**`、`ProjectSettings/**`、公共场景与 workflow | 双方 | 两位负责人共同批准 |
| 各自大型场景和 Prefab | 对应所有者 | 对方修改前先协调，避免并行编辑同一 YAML 资源 |

只有 `main` 是长期分支。每个阶段从最新 `main` 建立短分支，通过 PR 合并并附带独立变更记录；共同区域必须标记 `跨模块（重点）`。每周至少进行一次双方集成同步和闭环回归。不得直接推送、强制推送或把无关资源重导入混入功能 PR。

## 7. 硬性验收门禁

- 每个分支的新增规则必须有 EditMode 测试，且完整 EditMode 套件全绿；
- 阶段 0B 的 CODEOWNERS、UnityYAMLMerge/LFS 规则和无许可证 CI 未通过前，不开始功能分支；
- 存档阶段必须覆盖往返、损坏、备份恢复、版本令牌冲突和原子替换失败；
- 周执行阶段必须覆盖每个状态提交前后的失败，证明不会重复或跳过结算；
- FakeMatch 必须覆盖重复结果幂等、哈希冲突和 `PendingMatch` 恢复；
- UI 必须通过键鼠和手柄的 PlayMode 完整流程；
- 最终必须生成 Windows x64 开发构建，并由两位负责人各自完成一次闭环试玩；
- 任何门禁失败时，对应变更记录不得标记为“已完成”。

## 8. 范围控制

若工作量超出预期，按以下顺序削减：

1. UI 动画和视觉润色；
2. 试训与事件文案数量；
3. 训练配置数量；
4. 额外统计页面；
5. 非必要音效和演出。

不得削减正式存档与恢复、三个行动槽、确定性随机、幂等比赛结算、模块边界、自动化测试以及最小键鼠/手柄流程。

## 9. 本里程碑非目标

- 6 周玩法验证的具体首发竞争、赛程与事件内容；
- 完整 6v6、真实比赛接入或完整快速模拟；
- 完整六赛季、伤病、合同、转会、球探和退役结算；
- 复杂潜力揭示、逐项隐藏上限、完整打法评价与培养方向切换；
- 正式美术、外观编辑、按键重绑定、超宽屏和 4K 专项适配；
- 注册登录、云存档、跨电脑识别、在线对战、遥测 SDK、反作弊和存档加密；
- 生产后端、数据库或多仓库拆分；
- 本地档案/生涯删除、设置页和玩家可编辑设置；
- 从比赛中途继续；`PendingMatch` 只支持返回赛前并从本场开头重试。

这些能力需要在一周技术闭环稳定后分别重新设计和评审，不得以“预留接口”为由提前创建没有调用方的空实现。
