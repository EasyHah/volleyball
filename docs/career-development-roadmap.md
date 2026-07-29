# 球员生涯开发路线图

> 2026-07-28 集成基线：搭档在 `origin/main@173f957` 提供的 Shared V4 是唯一
> 比赛契约权威。Career 保留自己的八项成长属性、启动事实和结算模型，只在
> `Career.MatchIntegration` 中映射具体 `MatchContextV4`/`MatchResultV4`。V1–V3
> 生产载荷不迁移；含旧比赛载荷的开发档进入隔离/恢复流程。历史 V3 接入见
> [CHG-20260727-003](changes/2026-07-27-003-career-shared-v3-integration.md)。

- 状态：已确认，实施中
- 适用范围：离线首版的首个一周技术闭环
- Unity 基线目标：`6000.3.20f1`
- 已同步上游基线：`origin/main@173f957`（Full Rally V4 与 Unity 6000.3）
- 当前里程碑 Match 冻结树：`c7d047bb2018c8faa4a9c5b939f76adaaca8d369`
- 当前里程碑 Shared Runtime 冻结树：`76d70c3c229a9834471f0eed7874bda22efa7f63`
- 对应目录 `.meta` blob：Match `23d5e66a3e4158bd421c4d3ee573e0d4e7339627`；Shared V1
  `9085d85a3a423a82a6303df4ca3fe3819d8d30ea`
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
                              -> Shared/FakeMatch 契约门禁
                                  -> career-fake-match
                                      -> Input System 独立基线
                                          -> career-ui-vertical-slice
                                              -> career-recovery-build
                                                  -> （里程碑完成后）career-formal-6v6-integration
```

URP、NavMesh、物理比赛 runner 及任何 Match 内部迁移均移出本里程碑。首个闭环只使用 FakeMatch，
因此这些工作不再作为 Career Domain、Application、Persistence 或 UI 的前置条件。

任何阶段未达到退出条件，都不得用下一阶段的功能来掩盖缺口，也不得把两个阶段合并成大型 PR。

### 2.1 临时单负责人执行授权

自 2026-07-21 起，项目负责人授权当前里程碑负责人全权实现 Career、Shared、Bootstrap、Packages、
ProjectSettings、CI 和公共文档，不再等待暂时无法参与的 Match 开发者确认。该授权不扩大为改写 Match
内部代码：`Assets/Volleyball/Match/**` 固定为 `origin/main@4bf9e4b` 的上述树哈希，首里程碑任何提交
只要相对 PR 基线改变该树或目录 `.meta` 就直接失败；不能通过同一提交更新 policy 哈希绕过。

原“双方批准”门禁统一替换为五类证据：项目负责人授权、独立变更记录、无许可证结构检查、适用的
Unity 全量回归/Windows 构建，以及独立 agent 复核无 P1/P2。V1 契约保持兼容；新版详细事实和异步
runner 只由 Shared、Career.MatchIntegration 与 FakeMatch 消费。物理 Match 适配器、能力绑定、AI seed
接线和玩家控制全部延期，不得为完成首里程碑绕过冻结规则。

## 3. 前置基线

### 3.1 路线文档门禁

建议分支：`docs/career-development-roadmap`

进入条件：本轮产品与技术取舍已经确认。

退出条件：

- 四份生涯开发文档职责互不冲突，并经过 agent 复核；
- 项目负责人确认路线、临时单负责人授权、Match 冻结范围和验收机制；
- 文档 PR 不夹带功能代码、包升级或资源重导入。

### 3.2 待对齐的周计划前置切片

`feature/career-week-plan` 已实现固定三个行动槽、比赛槽保护、确认锁定和稳定行动 ID，并在 Unity
`6000.0.43f1` 上通过完整 EditMode 测试；但它当前仍把 `Academics`、`Social` 作为常驻行动，测试也
明确允许安排它们。这与已确认的“四类常驻行动；学业和社交只通过事件出现”冲突，因此不能按原样合并。

Unity 升级与协作基线完成后，只把该分支的实现/测试补丁重放到当前集成基线，禁止自动吸收新的 Match
提交。重放时直接删除 `Academics`、`Social` 枚举值，并把相关测试
改为只接受专项训练、力量训练、团队合练、休息和系统预留的比赛。该分支尚未形成需要兼容的正式存档，
因此不保留“可反序列化但在领域入口拒绝”的旧枚举值。该分支原有
`CHG-20260717-005` 已与主线的 Match 完局记录发生编号冲突；变基时必须把文件、标题和索引统一重编号为
变基当时尚未使用的唯一编号，并记录需求对齐与新基线提交；文档不通过文字“预留”尚未落盘的编号。
之后使用 `6000.3.20f1` 重跑
完整 EditMode。只有代码、测试、变更记录和新基线证据全部一致才能合并，不得把旧枚举保留为“以后
可能使用”的无调用方占位。

### 3.3 当前 Career 耦合与目标程序集依赖

当前 `CareerPlayerRecord` 直接保存七项浮点 `PlayerAbilitySnapshotV1`，`Career.Application` 也直接暴露
同步 `IMatchGateway.Play`。它们是原型代码，不是本里程碑的正式领域模型或场景生命周期接口。正式目标
依赖如下：

| 程序集 | Unity API | 允许依赖 |
| --- | --- | --- |
| `Volleyball.Shared` | 禁止 | legacy V1 与稳定 ID，当前里程碑保持冻结；无业务模块依赖 |
| `Volleyball.Shared.MatchV2` | 禁止 | 只依赖 `Volleyball.Shared` 的稳定 ID；提供 V2 DTO、规范 codec 与 `IMatchRunnerV2` |
| `Volleyball.Career.Domain` | 禁止 | `Volleyball.Shared` 中的稳定 ID；禁止引用 `MatchContextV*`、`MatchResultV*`、`PlayerAbilitySnapshotV*` |
| `Volleyball.Career.Application` | 禁止 | `Volleyball.Career.Domain`；定义用例、仓储端口和 Career 自有比赛端口 |
| `Volleyball.Career.Persistence` | 禁止 | 阶段 1 只依赖 Career Domain；阶段 2 起额外依赖 Career Application 以实现其仓储端口，并仅复用 `Volleyball.Shared` 的稳定 `PlayerId/TeamId`；禁止使用 V1 比赛 DTO。阶段 5 起额外依赖 `Volleyball.Shared.MatchV2`，仅在持久化 DTO/校验层保存完整上下文与原始结果，不向 Domain/Application 暴露 V2 类型；路径由外部注入 |
| `Volleyball.Career.Content` | 允许 | `Volleyball.Career.Domain`；只把 ScriptableObject 映射为纯 C# 配置 |
| `Volleyball.Career.MatchIntegration` | 禁止 | Career Domain/Application、`Volleyball.Shared` 与 `Volleyball.Shared.MatchV2`；独占 Career/Shared DTO 映射并承载首里程碑 Fake runner，不引用 Match 内部 Domain |
| `Volleyball.Career.Presentation` | 允许 | Career Domain/Application；不得直接写存档或 Shared 比赛 DTO |
| `Volleyball.Bootstrap` | 允许 | 组装 Shared V2、Career.MatchIntegration、Fake runner、现有公开适配器与应用状态机，不承载领域规则 |

新增物理路径固定为：

- `Assets/Volleyball/Shared/MatchV2/Runtime/Volleyball.Shared.MatchV2.asmdef`；
- `Assets/Volleyball/Shared/MatchV2/Tests/EditMode/Volleyball.Shared.MatchV2.EditModeTests.asmdef`；
- `Assets/Volleyball/Career/Runtime/Persistence/Volleyball.Career.Persistence.asmdef`；
- `Assets/Volleyball/Career/Runtime/MatchIntegration/Volleyball.Career.MatchIntegration.asmdef`。

V2 目录是 `Shared/Runtime` 的兄弟目录，因此不会改变 legacy V1 冻结 tree。首里程碑的
`FixtureMatchRunnerV2` 与 fixture repository 位于 `Career.MatchIntegration`；Bootstrap 只引用该公开程序集
完成注入。阶段 5 给 Persistence 增加 V2 引用时，依赖只用于完整载荷的序列化/语义验证；应用端继续以
Career 自有 `PendingMatch`/`CareerMatchFacts` 和不透明规范载荷交接，禁止 V2 DTO 穿透到 Domain/Application。

Career Domain 建立自有的八项 `0–10000` 整数能力模型；Shared 比赛能力只在
`Career.MatchIntegration` 中转换。允许 Domain 复用 Shared 的稳定 `PlayerId/TeamId`，但模块边界测试
必须禁止任何 Shared 比赛 DTO 回流到 Domain/Application。

阶段 1 直接删除当前 `CareerMatchRequest` 与同步 `IMatchGateway.Play`，只允许先定义尚未接线的 Career 自有
异步端口，例如 `Task<CareerMatchFacts> ExecuteAsync(CareerMatchLaunch, CancellationToken)`。请求和返回
都是 Career 自有类型。`Volleyball.Shared.MatchV2` 另行定义 `IMatchRunnerV2`，只接收/返回 V2 DTO；
FakeMatch 与未来真实比赛各自实现该契约。`Career.MatchIntegration` 依赖注入 `IMatchRunnerV2`，负责映射并
实现 Career 端口；Bootstrap 只组装实现，不复制映射规则。正式流程调用前必须已经耐久提交
`PendingMatch`；取消或场景加载失败只返回错误并保留待处理比赛，不能伪造比赛结果。

### 3.4 搭档最新 Match 基线与仍缺的接入条件

截至 `origin/main@4bf9e4b`，Match 已经完成可结束的 3v3 单局、统一多角色 AI、触球合法性、物理拦网，
以及 `FormalIndoor6v6` 的 25 分且领先 2 分单局。6v6 使用双方各六名球员的 `MatchContextV1`，并为全部
12 人产生 `MatchResultV1` 统计；`PhysicalMatchRallyDirector` 已成为 3v3/6v6 共用的场景适配器。这些
实现可以作为后续生涯正式比赛的真实生产者基线，不再把“是否能跑完 6v6 单局”列为 Career 风险。

但当前 `FormalSixVsSixRallyBootstrap` 仍创建硬编码沙盒上下文：它保留上下文的稳定 `PlayerId` 和位置，
却用 `AbilityFor(position)` 覆盖 `PlayerSnapshotV1.Ability`，因此生涯养成能力尚不会进入物理表现。当前
3v3/6v6 还是全 AI 自动单局，运行时输入只覆盖相机切换；回合内的异步 AI 权重请求也不是整场
`IMatchRunnerV*`。物理 Director 还以固定值 `7351` 构造双方 planner：`PhysicalRallyTacticPlanner` 会消费
该固定值，`TeamRallyDecisionPlanner` 则尚未使用；二者都没有读取 `MatchContextV1.seed`。Match 只输出 V1
的 `points/contacts/errors/workload`，Shared 没有 `resultHash` 或详细
技术事实，也没有证明从 Career 的 `PendingMatch` 跨场景启动并返回结果。当前 6v6 自动化验证还是
macOS、Unity `6000.0.43f1`；Windows x64 实机输入与性能尚未完成。因此“比赛玩法已存在”不等于 Shared
门禁或 Career 接入已经完成，首个一周闭环仍先使用 12 人 6v6 fixture 的 FakeMatch。

这里把原先的 3v3 固定结果样例调整为 12 人 6v6 数据形状，只增加 fixture 中的阵容与事实条目，不增加
物理比赛、快速模拟算法、多局制或 UI 玩法；目的是让首次正式 Shared 契约直接覆盖最终生涯比赛阵容，
避免闭环完成后立刻重做上下文、存档与幂等回执。3v3 仍作为兼容回归 fixture 保留。

搭档新增的 MenShen 客户端、命令和凭据只属于 Editor 工具。当前全局 UPM 包会让
`Newtonsoft.Json.dll` 进入 Mono Player，即使 `Volleyball.Match.AI.Editor` 本身不会进入；Career 不得据此
调用网络客户端或依赖默认 JSON 序列化。Player 构建不得包含 API key、网关地址字面量或实时模型依赖；
规范存档/契约哈希也不能直接改用 Newtonsoft 默认序列化。若以后要从 Player 剔除 DLL，应另立包边界
变更并验证 MenShen Editor 工具，不在 Unity 升级中强行处理。

## 4. 实施阶段

### 阶段 0A：锁定 Unity Editor

- 建议分支：`chore/unity-6000.3.20f1`
- 负责人：当前里程碑负责人
- 状态：本机实施与验证已完成（`d603219`）

进入条件：精确 Editor 与 Windows Build Support 已安装；以 `main@4bf9e4b` 为升级前唯一 Match 锚点，
不得自动跟随其后继提交，当前 Match 基线测试证据已记录。

范围：只升级 Editor、必要的项目设置、包锁与兼容修复。不得同时迁移 URP、引入 Input System、修改玩法、调整 Shared 契约或批量整理资源。

版本依据：[Unity 6000.3.20f1 官方发布说明](https://unity.com/releases/editor/whats-new/6000.3.20f1)；
支持周期以 [Unity 6 官方支持页面](https://unity.com/releases/unity-6/support) 为准。

退出条件：

- `ProjectVersion.txt` 精确锁定 `6000.3.20f1`，README、开发命令和变更记录一致；
- 项目完成重导入且不存在由升级造成的编译错误；
- 完整 EditMode、PlayMode 与 Windows x64 开发构建通过；重点回归 3v3、`FormalIndoor6v6`、12 人结果
  校验和 Editor-only MenShen 程序集边界。上游记录的 `224/224` EditMode、`11/11` PlayMode 只作为
  升级前参考，测试数下降必须解释，不能把未运行误报为通过；
- 两次 batchmode 导入后的 tracked diff 稳定，场景/Prefab 未被重写；独立 agent 审计确认包锁和
  ProjectSettings 变化属于 6.3 必要迁移且无 P1/P2。

### 阶段 0B：协作基础设施基线

- 建议分支：`chore/collaboration-baseline`
- 负责人：当前里程碑负责人

进入条件：Unity `6000.3.20f1` 升级已合并，避免基础设施分支同时携带项目重导入差异。

范围：在功能开发前落地 `CODEOWNERS`、UnityYAMLMerge、Git LFS/锁约定和不需要 Unity 许可证的基础
CI。当前仓库只有最小 `.gitattributes`，Unity Windows workflow 整个 job 仍为禁用状态，不能把它们写成
已经完成。

退出条件：

- `CODEOWNERS` 的默认、Career 与共同区域负责人为当前已认证且具有写权限的 `@hoshisora1`；Match
  路径仍归 `@EasyHah`，并由 CI 校验冻结树哈希，不以离线人员批准作为可执行门禁；
- `.unity`、`.prefab`、`.asset` 配置 UnityYAMLMerge，并在当前 Windows 开发机完成 merge driver 烟测；大型二进制
  资源进入 Git LFS，需要独占编辑的源资产标记 `lockable`，YAML 资产不进入 LFS；
- 启用独立、非 `if: false` 的无许可证 CI job，至少执行变更记录检查、`.meta` 配对、asmdef JSON/依赖方向、
  禁止业务代码落入 `Assembly-CSharp` 和 `git diff --check`；检查器必须识别现有 Editor-only
  `Volleyball.Match.AI.Editor`、`Unity.Newtonsoft.Json`、测试程序集和预编译引用，不能误报合法工具依赖；
- CI 与本地检查都验证 Match 树哈希、Unity/包锁一致性且运行后工作区保持干净；检查名称和本地
  UnityYAMLMerge/LFS 规则写入 `docs/development.md`。

### 阶段 1：正式存档模型

- 建议分支：`feature/career-save-model`
- 负责人：Career

进入条件：Unity 升级与协作基础设施已合并；`career-week-plan` 已完成需求对齐、在新版本上重放测试并
合并。

范围：建立最小但正式的 `CareerSaveSnapshot`、版本字段、`revision`、周状态、操作回执与必要稳定 ID，
并把 `CareerPlayerRecord` 从 `PlayerAbilitySnapshotV1` 解耦为 Career 自有八项整数属性模型。删除当前
`CareerMatchRequest` 与同步 `IMatchGateway.Play`；若为了后续编译先建立 Career 自有异步端口，本阶段不得
接线或调用它。
字段只覆盖本里程碑已确认的创建、试训、Planning 和已原子提交的行动后果，但结构不得是一次性临时存档。
`ExecutingSlot` 只是 Application 命令执行期的内存态：提交前权威快照仍为原 `Planned`，提交成功后直接变为新
`Planned` 或 `AwaitingEventChoice`，不得持久化半个槽位。正式
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

### 阶段 5：Shared/FakeMatch 契约门禁

- 建议分支：独立的跨模块 `feature/shared-career-fake-contract`
- 负责人：当前里程碑负责人；高风险契约 diff 由独立 agent 复核

进入条件：前四个 Career 阶段不再依赖猜测性比赛字段；Career 提供真实消费需求；现有 Match V1 与
完整 3v3/6v6 测试只作为兼容回归，Match 内部仍保持冻结。

范围分为 5A 与 5B。当前里程碑只做 5A：保持现有 `Volleyball.Shared` 程序集、全部 V1 类型和字段语义
不变，新增兄弟程序集 `Volleyball.Shared.MatchV2`（命名空间 `Volleyball.Shared.Contracts.V2`），承载
生产者中立的版本轴、整数能力/负荷、冻结上下文、详细比赛事实、规范哈希、固定 12 人 fixture 和
`IMatchRunnerV2` 异步执行接口；建立 `Career.MatchIntegration` 的双向 DTO 映射，并提供只读取版本控制
fixture 的 FakeMatch runner。`ContractVersions.SupportsMatch(2)` 必须继续返回 `false`，避免现有 V1
验证器把 V2 当作 V1 接受。5B 是物理 Match 生产者、能力绑定、AI seed、真实技术事件和场景 runner，
全部延期且不阻塞本里程碑。本阶段不修改 Match、不从 V1 汇总值反推详细事实，也不启动物理场景。

退出条件：

- 字段定义、计数口径、规范字节、兼容策略与 golden hashes 写入 Shared 测试和变更记录，并经独立
  agent 复核无 P1/P2；
- Shared、`Career.MatchIntegration` 和 FakeMatch 分别读取同一组 golden fixture 并通过契约与哈希测试；
  fixture 至少包含一组 3v3 V1/兼容回归和一组新版 6v6、12 人结果；
- 3v3 和 6v6 的 V1 兼容回归继续通过；新版 6v6 fixture 包含 12 个不重号的稳定球员 ID，主角事实能
  唯一映射回 Career 自有类型；
- 现有 `Volleyball.Shared` Runtime 树哈希保持上述 V1 冻结值；V2 只能通过兄弟程序集增量提供；
- `Career.MatchIntegration` 只负责 Career 八项属性与 Shared 新版能力 DTO 的映射，不引用 Match Domain；
  Shared DTO 到物理 `PlayerAbilityProfile`、AI seed 和真实技术事件的映射明确延期；
- FakeMatch 不推进随机流，只校验冻结 `matchSeed` 已进入上下文哈希与 fixture 匹配；快速模拟和物理 AI
  对 seed 的消费不属于本阶段；
- FakeMatch runner 的 fixture 结果能经适配器转换为 Career 自有事实，但尚不由生涯流程调用；同步
  `IMatchGateway.Play` 已在阶段 1 删除；
- Career 只准备输入和计算长期后果，Match 只执行比赛/快速模拟并报告事实；
- Match 树哈希保持冻结值；变更记录标记为 `跨模块（重点）`，明确物理消费者延期。

### 阶段 6：FakeMatch 与幂等结算

- 建议分支：`feature/career-fake-match`
- 负责人：当前里程碑负责人；契约行为由自动化与独立 agent 复核

进入条件：Shared 契约升级已合并；双方各六人的固定上下文与 12 人结果 fixture 可用。

范围：以显式 schema 升级加入正式 `PendingMatch` 和 `SettlementReceipt`，并完成赛前重点、
FakeMatch 固定 6v6 单局事实、结果哈希校验、回执查询和从比赛前重试。只有 `PendingMatch` 成功提交后，生涯流程
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
- 负责人：当前里程碑负责人

进入条件：Unity 升级稳定；UI 垂直切片即将开始。

范围：独立加入官方 Input System 包和 Career Action Map。首轮只定义键鼠与 XInput 手柄需要的菜单
导航、确认、取消、返回和页面切换，不创建或修改 Match Action Map，不制作按键重绑定。

退出条件：

- 包锁、输入资源和程序集 diff 经自动化及独立 agent 复核；
- 现有 Match 操作没有未说明的回归；
- Career 的最小焦点导航测试夹具可用。

### 阶段 8：UI 垂直切片

- 建议分支：`feature/career-ui-vertical-slice`
- 负责人：Career

进入条件：从本地档案到 FakeMatch 结算的无 UI 用例已经全绿；Input System 基线已合并。

范围：只实现完成一周闭环所需的七类 UI、简体中文文案资源、键鼠与手柄导航、保存状态提示以及仅 Editor/Development Build 可见的诊断面板。视觉采用统一开发期样式，不进行正式美术生产。

退出条件：

- 1280×720 不裁切关键操作，1920×1080 完成主要验证；
- 键鼠与 XInput 的动作图、绑定和控制器流程由聚焦 EditMode 覆盖；PlayMode
  只保留真实面板、两档分辨率、运行时 action 与返回路由烟雾。完整的纯键鼠／纯手柄
  闭环改为阶段 9 Windows 构建的单次人工验收，不在每个子切片重复跑耗时 PlayMode；
- UI 只提交应用命令，不直接改写生涯状态或磁盘文件；
- 退出并重启后进入第二周 Planning，所有显示与权威快照一致。

### 阶段 9：恢复、回归与 Windows 构建

- 建议分支：`feature/career-recovery-build`
- 负责人：当前里程碑负责人
- 实施状态：自动化恢复、脱敏诊断、Career PlayMode、Windows x64 IL2CPP 构建、鼠标功能闭环及
  退出重开恢复已通过；自动化键盘注入未进入 Unity Player 事件链，实体键盘与 XInput 验收尚未执行，
  因此阶段 9 仍为进行中。

进入条件：七类 UI 已能在 Editor 中完成闭环，所有前置变更记录均已完成。

范围：集中验证中断恢复、诊断导出、完整回归和 Windows x64 开发构建，不新增玩法。

退出条件：

- EditMode、相关 PlayMode、契约 fixture 与程序集边界检查全部通过；
- 强制中断覆盖新建、临时写入、原子替换、比赛待处理和结算后的关键窗口；
- Windows 10/11 x64 实机由当前负责人完成一次完整闭环试玩，并保存构建/诊断证据；
- 完整 Match 回归仍覆盖现有 3v3 与 `FormalIndoor6v6`，Career 变更不得降低搭档已建立的测试基线；
- 1920×1080 的菜单交互无明显停顿，自动保存不会无提示冻结界面；
- 所有测试版本、结果和已知限制写入变更记录，失败门禁不得标记为完成。

### 阶段 10：现有 FormalIndoor6v6 生涯接入（首里程碑完成后的后续阶段）

- 建议分支：`feature/career-formal-6v6-integration`
- 负责人：后续重新授权；需要修改 Match 内部，当前冻结期间不得启动

进入条件：阶段 9 已完成；Shared runner 与 12 人事实契约稳定；上下文能力已真实绑定到物理球员；Match
已明确主角控制模式并完成 Windows x64 基本输入/性能验证。当前按位置覆盖能力、自动运行的沙盒 6v6
不满足这些条件。

范围：让现有 `FormalIndoor6v6` 从已提交的 `PendingMatch` 接收外部上下文，移除正式入口中的硬编码
沙盒创建，通过同一 `IMatchRunnerV*` 返回结果，并验证取消、场景加载失败、从赛前重试与成功结算。
runner 必须把冻结的 `matchSeed` 派生并传入所有使用随机性的 AI planner，不能继续使用固定 `7351`；
纯确定性 planner 删除未使用的 seed 参数。主角控制启用时，必须保证该球员不会同时收到 AI 决策。
仍只覆盖单局，不在该分支加入多局制、换人、自由人替换程序或比赛中途存档。

退出条件：FakeMatch 与物理 6v6 可在不改 Career Domain 规则的情况下替换；12 名球员身份、能力和主角
统计往返一致；物理比赛退出/崩溃只回到赛前并复用原上下文；完成单独的 Windows 构建实机验收。

## 5. 独立基础设施迁移

### URP

URP 需要触及 Match 场景、材质和相机，当前明确冻结并移出首个里程碑。以后恢复该工作时必须使用
独立 `chore/urp-baseline` 分支，不能与 Input System、Career UI 或比赛玩法改动混合。

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
真实比赛目标 `60 FPS`。现有 `FormalIndoor6v6` 已能自动完成单局，但首个 Career 里程碑只包含 FakeMatch，
因此当前只验证菜单交互和保存不出现明显停顿；`60 FPS` 在阶段 10 的物理 6v6 生涯接入时才成为硬门禁。
4K、超宽屏和低端集成显卡专项优化留待后续。

## 6. 所有权与合并规则

| 区域 | 主要负责人 | 合并要求 |
| --- | --- | --- |
| `Assets/Volleyball/Career/**` | 当前里程碑负责人 | 可实现；不得反向依赖 Match 内部代码 |
| `Assets/Volleyball/Match/**` | 冻结上游 | 保持树哈希 `1f0bbe...`；任何变化直接拒绝 |
| `Assets/Volleyball/Shared/**`、`Assets/Volleyball/Bootstrap/**`、`Packages/**`、`ProjectSettings/**`、公共场景与 workflow | 当前里程碑负责人 | 变更记录、自动化兼容证据和独立 agent 复核 |
| 大型场景和 Prefab | 当前里程碑负责人 | 首里程碑不编辑既有 Match 场景；新 Career 资源使用独立路径 |

只有 `main` 是长期分支。每个阶段从当前已验证集成基线建立短分支，通过 PR 合并并附带独立变更记录；
共同区域必须标记 `跨模块（重点）`。每次开始新阶段都重新获取远端状态，但不得自动合入改变冻结 Match
树的提交；发现新 Match 上传时先单独审计。不得直接推送、强制推送或把无关资源重导入混入功能 PR。

## 7. 硬性验收门禁

- 每个分支的新增规则必须有 EditMode 测试，且完整 EditMode 套件全绿；
- 每次验证都必须确认 Match 树哈希仍为冻结值，V1 契约和现有 3v3/6v6 回归继续通过；
- 阶段 0B 的 CODEOWNERS、UnityYAMLMerge/LFS 规则和无许可证 CI 未通过前，不开始功能分支；
- 存档阶段必须覆盖往返、损坏、备份恢复、版本令牌冲突和原子替换失败；
- 周执行阶段必须覆盖每个状态提交前后的失败，证明不会重复或跳过结算；
- FakeMatch 必须覆盖重复结果幂等、哈希冲突和 `PendingMatch` 恢复；
- UI 必须通过键鼠和手柄的 PlayMode 完整流程；
- 最终必须生成 Windows x64 开发构建，并由当前负责人完成一次闭环试玩和独立 agent diff 审计；
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
- 把现有 `FormalIndoor6v6` 接入 Career、主角实时控制、完整快速模拟、多局制、换人或自由人替换程序；
- 完整六赛季、伤病、合同、转会、球探和退役结算；
- 复杂潜力揭示、逐项隐藏上限、完整打法评价与培养方向切换；
- 正式美术、外观编辑、按键重绑定、超宽屏和 4K 专项适配；
- 注册登录、云存档、跨电脑识别、在线对战、遥测 SDK、反作弊和存档加密；
- 生产后端、数据库或多仓库拆分；
- 本地档案/生涯删除、设置页和玩家可编辑设置；
- 从比赛中途继续；`PendingMatch` 只支持返回赛前并从本场开头重试。

这些能力需要在一周技术闭环稳定后分别重新设计和评审，不得以“预留接口”为由提前创建没有调用方的空实现。
