# 生涯存档与恢复设计

## 1. 文档状态与边界

本文把项目架构中已经确认的持久化约束收束为可实施设计。除“当前代码事实”外，本文描述的是正式目标设计，尚未表示对应类型、仓储、迁移器、文件恢复器或 UI 已经实现。

状态标记：

- **已确认设计**：后续实现必须遵守；修改需经过架构评审。
- **当前代码事实**：已经存在于仓库，不能把规划字段误报成现状。
- **待实现**：类型或行为尚未落地。
- **Shared 升级门槛**：Career 接入正式比赛生命周期之前必须先完成的跨模块契约工作。

首版范围是 Windows 本地离线档案与多条生涯，不包含注册登录、云同步、跨设备冲突合并、在线比赛、完整事件溯源数据库，也不承诺比赛中途检查点恢复。

## 2. 当前代码事实与正式契约落差

### 2.1 当前 Shared V1 已有能力

`Volleyball.Shared.Contracts` 当前已经提供：

- `PlayerId`、`TeamId`，采用 ordinal 相等比较和受限字符串校验；
- `MatchContextV1`：`contractVersion`、`sessionId`、`seed`、主客队快照、`contextHash`；
- `MatchResultV1`：`contractVersion`、`sessionId`、`contextHash`、胜方、最终主客队比分、球员 `points/contacts/errors/workload`；
- `ContractJson` 的 JSON 往返校验；
- `contextHash` 的 SHA-256 小写十六进制校验；
- 当前只接受 `contractVersion == 1`。

`origin/main@4bf9e4b` 的 `FormalIndoor6v6` 已经用双方各六人的 V1 上下文跑完 25 分单局，并为 12 名
球员创建结果统计；3v3 与 6v6 共用 `PhysicalMatchRallyDirector`。这证明现有身份、比分和结果列表可作为
升级起点，但不增加任何 V1 字段或持久化保证。

当前 `MatchContextV1` 的哈希实现是该 V1 自身的固定实现：包含显示名、数值枚举和 IEEE `float` 的 round-trip 文本。当前 `PlayerAbilitySnapshotV1` 和 `PlayerMatchStatsV1.Workload` 也使用 `float`。这些是现状，不等于本文第 10 节的正式规范哈希已经实现。

### 2.2 不得假设当前 V1 已有的字段

当前 `MatchContextV1` **没有**以下字段：`contentVersion`、`rulesetVersion`、`matchSimulationVersion`、`matchRandomAlgorithmVersion`、直接比赛/快速模拟模式、赛制、赛事与轮次、比赛重要性、体能、伤病限制、首发状态、战术职责、轮转、赛前重点。

当前 `MatchResultV1` **没有** `resultHash`、结束状态（含 `abandoned`）、逐局比分、上场时间、回合数、移动/起跳/落地负荷、结构化伤病观察、战术/轮转/关键分信号或结构化关键事件。

当前 `FormalSixVsSixRallyBootstrap` 还会在场景内创建硬编码沙盒上下文，并按位置模板覆盖上下文中的
能力值；它不接受已持久化的 `PendingMatch`，也没有跨场景异步 runner。能够全 AI 自动完成 6v6 单局
不等于已经支持 Career 数值、玩家控制、恢复或结算。当前物理 Director 还固定以 `7351` 构造 AI
planner：物理战术 planner 会消费这个固定值，团队决策 planner 尚未使用；两者均不读取已序列化的
`MatchContextV1.seed`，因此该上下文 seed 没有实际驱动物理 AI。

`PlayerId`、`TeamId` 只保证字符串格式与值相等；稳定 ID 的注册、不可复用、tombstone 和内容资产校验仍待实现。Match 原型里的场上槽位 ID 不得直接写入生涯存档。

### 2.3 当前 Career 原型耦合

当前 `CareerPlayerRecord` 直接保存七项浮点 `PlayerAbilitySnapshotV1`，与已确认的八项整数
`CareerPlayerAttributes` 冲突；当前 `CareerMatchRequest`/`IMatchGateway.Play` 还让 Application 直接暴露
Shared V1 并同步返回结果。这些类型只代表原型，不得写入正式快照或作为跨场景生命周期。

阶段 1 必须先建立 Career 自有八项属性、请求和比赛事实类型；Shared DTO 只允许在
`Career.MatchIntegration` 映射。正式比赛端口为可取消的异步 `ExecuteAsync`，且取消/加载失败保留
`PendingMatch`，不生成伪结果。

### 2.4 Shared/FakeMatch 升级门槛

在 Career 可以正式创建 `PendingMatch` 或结算 FakeMatch 前，必须完成一次受控的增量契约升级。现有
`Volleyball.Shared` Runtime tree `61c7a928f2bf4740defea34c67e5cb108f6dfe76`、全部 V1 类型和
`ContractVersions.SupportsMatch` 行为保持冻结；新版放在兄弟程序集 `Volleyball.Shared.MatchV2` 与
`Volleyball.Shared.Contracts.V2` 命名空间：

完整 V2 上下文和原始结果由 `Volleyball.Career.Persistence` 的持久化 DTO 层拥有；该程序集从阶段 5
开始可依赖 `Shared.MatchV2` 来执行 codec 与语义验证，但只向 Application 的仓储端口映射 Career 自有
状态和不透明规范载荷，V2 类型不得进入 Career Domain/Application。`Career.MatchIntegration` 负责把该
载荷解码为 V2 runner 输入并把 V2 结果映射回 Career facts。这样既保存完整正式载荷，也不复制跨模块 DTO。

1. 定义包含第 9 节全部冻结输入的新版 `MatchContext`，并加入所有比赛相关版本轴。
2. 定义包含 `resultHash`、结束状态、逐局与结构化负荷事实的新版 `MatchResult`。
3. 将跨模块数值改为带明确量纲的定点整数，例如毫米、毫秒、0–10000 能力基点和整数负荷；禁止在正式规范哈希载荷中使用浮点数。
4. 实现第 10 节的规范 JSON、golden bytes 和 golden hashes。
5. 为 Shared、Career.MatchIntegration 与 FakeMatch 提供共同读取的 6v6 fixture，冻结双方各六人并返回 12 人事实；同时保留一组
   3v3 契约回归 fixture。未来直接比赛与快速模拟必须复用同一结构，各自在首次接入时新增自身 fixture
   和适用版本。
6. V1 保持兼容且不接受 V2；`ContractVersions.SupportsMatch(2)` 继续返回 `false`。V2 使用独立检查器，
   并以 V1 精确 JSON/hash、3v3/6v6 回归及 V2 golden bytes/hashes 共同证明隔离边界。

门槛完成前，Career 可以实现与测试本地档案、通用快照、CAS 仓储和不依赖正式比赛 DTO 的周状态，但不得声称比赛恢复与幂等结算闭环已经完成。

12 人 fixture 只改变固定契约数据的基数，不引入物理比赛或快速模拟算法；它用于避免首闭环完成后因
正式生涯 6v6 阵容而立刻迁移 `PendingMatch`、结果哈希和回执结构，不视为扩大首里程碑玩法范围。

## 3. 本地身份、多生涯索引与磁盘布局

### 3.1 LocalPlayerProfile

**已确认设计**：一名本地玩家对应一个 `LocalPlayerProfile`，一个档案可拥有多条独立生涯。

```text
LocalPlayerProfile
├─ schemaVersion
├─ profileId
├─ profileRevision
├─ profileSnapshotHash
├─ displayName
├─ createdAtUtcMs
├─ updatedAtUtcMs
└─ CareerIndexEntry[]
```

`CareerIndexEntry` 只保存可重建摘要，例如 `saveId`、球员显示摘要、阶段、赛季、周次、最后更新时间和最近可载入状态；不得嵌入完整 `CareerSaveSnapshot`。存档文件内部的 `profileId` 与 `saveId` 是归属权威，`profile.json` 的索引不是权威数据源。

`profiles-index.json` 同样只用于发现本地档案，并具有自己的 `catalogRevision` 与 `catalogHash`。索引缺项
不能成为删除档案或生涯文件的依据。

### 3.2 固定布局

```text
Application.persistentDataPath/
└─ Profiles/
   ├─ profiles-index.json
   └─ {profileId}/
      ├─ profile.json
      ├─ profile.bak
      ├─ profile.tmp.{operationId}
      ├─ profile.replace-backup.{operationId}
      └─ Careers/
         ├─ {saveId}.json
         ├─ {saveId}.bak
         ├─ {saveId}.tmp.{operationId}
         ├─ {saveId}.replace-backup.{operationId}
         ├─ {saveId}.recovery-intent.{operationId}.json
         └─ Quarantine/
```

所有路径组件使用校验后的稳定 ID，不接受显示名或任意用户输入拼接路径。临时文件必须和目标文件处于同一目录、同一卷。

`profile.json` 使用与生涯快照相同的“同卷临时文件 -> 重读验证 -> 带单备份原子替换”协议；它的
`.bak` 只保护档案元数据和可重建索引，不代替任何生涯备份。`profiles-index.json` 是可重建目录，仍需
原子替换，但损坏时以扫描有效 `profile.json` 重建，不能据其内容删除目录。

### 3.3 创建与索引修复

创建生涯的提交顺序固定为：

1. 在档案级独占锁内生成不可复用的 `saveId`、初始 `lineageId` 和初始 `revision`；
2. 原子创建并校验 `{saveId}.json`；
3. 再以 `expectedProfileRevision` 原子更新 `profile.json` 的索引。

若步骤 2 后崩溃，启动扫描根据存档内部 `profileId` 识别有效孤儿，把摘要重新挂回正确档案。禁止因索引暂缺而删除有效存档。

首个里程碑不提供删除本地档案、删除生涯或设置功能，因此初始 schema 不包含 `settings`，磁盘协议也不
实现删除标记或删除恢复。以后加入删除时必须单独设计玩家确认、锁顺序、索引 CAS、撤销边界和失败
恢复，并通过新变更记录与测试；不得把未调用的删除状态提前放入首版 DTO。

## 4. CareerSaveSnapshot

### 4.1 顶层结构

**已确认设计**：每条生涯是一个完整、版本化、可独立原子替换的 JSON 快照。首个实现阶段只加入
已有调用方需要的字段；Shared 契约门禁完成后，再以显式 schema 升级加入比赛生命周期字段。建议逻辑
结构如下；具体 C# DTO 尚待实现。

```text
CareerSaveSnapshot
├─ versions
│  ├─ schemaVersion
│  ├─ contentVersion
│  ├─ rulesetVersion
│  └─ careerRandomAlgorithmVersion
├─ identity
│  ├─ profileId
│  ├─ saveId
│  ├─ lineageId
│  ├─ revision
│  ├─ restoredFromVersionToken?
│  ├─ createdAtUtcMs
│  └─ updatedAtUtcMs
├─ integrity
│  └─ snapshotHash
├─ careerSeed
├─ progression
│  ├─ phase / season / week
│  ├─ onboardingState
│  ├─ weekPlan
│  ├─ weekExecutionState
│  ├─ pendingEvent
│  └─ trainingEmphases[]
├─ player(CareerPlayerAttributes) / team / coachTrust / fatigue / mindset
├─ schedule / matchHistory / aggregateStats
├─ notifications
└─ operationReceipts[]
```

Shared 门禁完成后的同一里程碑中，首个显式 schema 升级加入 `contractVersion`、`pendingMatch` 和
`settlementReceipts[]`。伤病、球探、潜力揭示、俱乐部兴趣、合同与报价只在对应玩法有真实调用方时由
后续 schema 版本加入；真实快速模拟实现时再加入 `matchSimulationVersion` 和
`matchRandomAlgorithmVersion`。首版不得为空字段、通用扩展字典或无类型 JSON 预留位置。

有业务顺序的集合使用数组并保留顺序；以 ID 查询的逻辑映射在序列化时写为按稳定 ID ordinal 排序的条目数组，不直接序列化或哈希字典。历史记录一经写入不得原地改写事实；修正通过新记录或显式迁移完成。

### 4.2 全部版本轴

| 版本轴 | 唯一职责 | 何时提升 |
| --- | --- | --- |
| `schemaVersion` | JSON 字段结构和逐级迁移 | 字段结构或表示法不兼容变化 |
| `contentVersion` | 训练、伤病、俱乐部、事件和数值配置 | 静态内容/数值包语义变化 |
| `rulesetVersion` | 成长、疲劳、信任、报价和结算算法 | 生涯规则计算变化 |
| `contractVersion` | Match 输入输出跨模块结构 | Match DTO 或统计口径变化 |
| `careerRandomAlgorithmVersion` | 生涯随机键编码、哈希、范围映射 | 任一确定性随机规则变化 |
| `matchSimulationVersion` | 快速模拟算法 | 快速模拟逻辑变化 |
| `matchRandomAlgorithmVersion` | 快速模拟 PRNG 与整数映射 | PRNG、状态推进或映射变化 |

版本轴不能互相代替。仅调整规则不能伪装成 `schemaVersion` 变化；JSON 可读也不表示规则兼容。
每份快照写入当前已实现且适用的全部版本，不为未实现系统写虚假默认值。与单场执行有关的版本还必须
冻结进新版 `MatchContext`、`PendingMatch` 并参与 `contextHash`。

载入只有三种合法结果：完全支持后载入；经显式兼容声明载入；经逐级迁移后载入。版本更高、缺少迁移器、必需业务 ID 缺失或随机/模拟版本不支持时，保留原文件并拒绝破坏性运行。旧版历史若需只读展示，也必须有显式旧版解析器。

### 4.3 开发期 Schema 策略

在首个可分发兼容基线冻结前，允许开发构建执行“清空开发存档并重建 fixture”，但必须满足：

- 只在明确的 Development/Editor 开关下可用；发布构建不可自动清档；
- 启动日志明确说明拒载原因，UI 要求开发者显式确认；
- fixture 与 schema 基线在同一变更中更新；
- 禁止让反序列化器静默填补会改变业务语义的缺失字段。

一旦产生需要保留的内部试玩档或首个外部分发构建，`schemaVersion` 进入兼容纪律：只允许 `N -> N+1` 的纯函数迁移器，逐级执行；每步验证输入版本、输出版本、稳定 ID、业务不变量与哈希，并有 golden fixture。迁移前创建唯一上一修订备份；迁移失败恢复原修订并隔离失败产物。不得保留一个“万能迁移到最新版”的跳跃迁移器。

## 5. 版本令牌、CAS 与串行队列

### 5.1 生涯版本令牌

正常业务提交保持同一 `lineageId`，并令 `revision = previousRevision + 1`。CAS 身份使用完整
`CareerVersionToken(lineageId, revision, snapshotHash)`；时间戳不能代替版本令牌，也不参与并发裁决。
`lineageId` 只在创建生涯或玩家明确批准从旧备份恢复时生成，普通保存不得更换。

`ICareerSaveRepository.Commit(saveId, expectedVersionToken, nextSnapshot)` 的语义是比较并交换：

1. 进入该 `saveId` 的进程内串行队列；
2. 获取该存档的跨进程独占文件锁；
3. 在锁内重读并验证主文件当前完整版本令牌；
4. 只有当前令牌等于 `expectedVersionToken`、下一快照保持同一 `lineageId`，且
   `nextSnapshot.revision == expectedVersionToken.revision + 1` 才写入；
5. 否则返回 `VersionConflict`，绝不覆盖；
6. 应用层重新载入最新快照，重新验证命令前置条件，再决定返回既有回执、重新计算或拒绝，不得盲目重试旧快照。

第二个进程无法获取锁时，只能拒绝打开该生涯，或由产品明确提供只读模式。只读模式不能排队写操作。

### 5.2 单写者队列

每个 `saveId` 只有一个应用命令与提交队列；不同 `saveId` 可独立运行。自动保存、手动保存、计划确认、
训练/休息/事件、比赛创建、比赛结算和周末结算都必须经过对应队列；后续合同等系统接入时也必须
复用同一队列。UI、场景和 Match 模块均不能绕过仓储直接写盘。

只有原子替换成功后，应用层才向 UI 发布新状态并处理下一命令。写盘中的内存草稿不是权威状态。

### 5.3 档案与总索引并发

`profile.json` 使用 `profileRevision + profileSnapshotHash` 做 CAS，并为每个 `profileId` 建立进程内队列和
跨进程文件锁。不同生涯可以独立提交权威快照，但随后重建 `CareerIndexEntry` 时必须进入所属档案队列；
索引更新失败不回滚已经成功的生涯提交，启动扫描会按权威快照修复摘要。

`profiles-index.json` 使用独立的全局目录队列、文件锁、`catalogRevision` 和 `catalogHash`。创建本地
档案必须走该 CAS；修复孤儿时按“档案锁 -> 生涯锁”的固定顺序取锁。普通生涯提交不在持有
生涯锁时等待档案锁，避免反向锁序。两个不同 `saveId` 的并发创建或摘要更新不得通过最后写入者
覆盖彼此。

## 6. 正式持久化入队与周状态机

新生涯在完成试训前使用 `OnboardingState`，至少保存当前试训段、每段稳定 `occurrenceId`、已确认选择、
已解析结果、随机算法版本和正式入队标记：

```text
CareerCreated
  -> Tryout(stageIndex = 1)
  -> Tryout(stageIndex = 2)
  -> Tryout(stageIndex = 3)
  -> 第一周 Planning
```

每段确认都以独立 `operationId` 原子提交选择、结果、回执和下一阶段；重启只恢复到尚未确认的下一段，
不能重复领取已确认结果。第三段提交同时建立完整球员、球队归属和第一周 `Planning`。初始结果页读取
该快照；“继续”只是 UI 导航，不存在 `AwaitingTryoutReview`，也不提交新 revision。

```text
Planning
  -> Planned(weekPlanId, slotActionIds, nextSlotIndex = 1)
  -> [ExecuteSlot(slotIndex = 1, operationId) 内存原子命令]
  -> AwaitingEventChoice(occurrenceId)
  -> Planned(..., nextSlotIndex = 2)
  -> [ExecuteSlot(slotIndex = 2, operationId) 内存原子命令]
  -> Planned(..., nextSlotIndex = 3)
  -> AwaitingMatch(sessionId)        // 仅比赛槽
  -> 下一周 Planning                 // 比赛、周末与推进同一原子提交
```

槽位 1 后果、事件实例和 `AwaitingEventChoice` 在同一 revision 提交。`pendingEvent` 至少冻结
`occurrenceId`、内容业务 ID、两项选项 ID、已解析参数、随机算法版本和随机键所需上下文；事件选择的
后果、回执、`pendingEvent` 清除与恢复到 `Planned(nextSlotIndex = 2)` 属于下一次原子提交。

`ExecuteSlot(operationId)` 与 `SettlingMatchAndWeek(operationId)` 都只是在应用命令执行期间的内存阶段，不是可单独保存或恢复的权威
状态。普通行动提交前崩溃时仍为原 `Planned`；比赛结算提交前崩溃时仍为 `AwaitingMatch`。提交成功后直接是
新 `Planned`/`AwaitingEventChoice` 或下一周 `Planning`，不存在只落盘部分后果的中间快照。

每周固定三个槽位；正式比赛预占槽位。整周计划确认前可编辑，确认后不能回退已完成行动。

持久化不变量：

- `weekPlanId`、`slotActionId`、`occurrenceId`、`operationId`、`sessionId` 均为创建后不变且不可复用的稳定 ID；
- 行动重排不改变 `occurrenceId`，删除再添加必须生成新 ID；
- 槽位后果、完成标记、`OperationReceipt` 与下一状态在同一 revision 提交；
- `AwaitingEventChoice(occurrenceId)` 与相同 ID 的 `pendingEvent` 必须同时存在或同时清除；
- `AwaitingMatch(sessionId)` 与相同 `sessionId` 的 `PendingMatch` 必须同时存在或同时清除；
- 周次递增和下一周 `Planning` 必须属于一次原子提交。

提交前崩溃时，重启仍看到原状态，并以相同 ID 与随机键重算；提交后崩溃时，回执与新状态阻止重复应用。

## 7. 稳定 ID 与事件确定性

### 7.1 稳定 ID

所有写入快照的业务实体和 ScriptableObject 配置都使用人工维护、跨导入稳定、不可复用的业务 ID。Unity Asset GUID、数组下标、场景对象实例 ID、显示名、文件路径和本地化文本均不能充当业务 ID。

删除的内容 ID 进入永久 tombstone 注册表。语义变化时新增 ID 或提升相应版本，禁止静默重命名或复用旧 ID。显式替代映射必须属于迁移器并有 fixture。

### 7.2 键控随机

每份生涯创建时通过注入的 `ICareerSeedSource` 固定 32 字节 `careerSeed`；生产实现使用系统密码学随机源，
测试实现传入固定字节。JSON 只接受 64 个小写十六进制字符，加载时解码为原始 32 字节。领域层不维护
全局顺序随机游标。每次抽取由以下键独立派生：

```text
careerRandomAlgorithmVersion + careerSeed + streamId + season + week
+ entityId + occurrenceId + drawIndex
```

`streamId` 来自版本控制注册表。首个里程碑只注册有真实随机调用的 `tryout`、`event` 和
`match_seed`；训练结果由配置和输入直接计算，不注册 `training`。`scouting`、`offer` 等未来流在首次
实现对应系统时新增。既有值不得改义或复用。同一操作多次抽取显式增加 `drawIndex`。没有实体时使用
注册表中的明确 sentinel ID，不使用空字符串。

`careerRandomAlgorithmVersion = 1` 的规范二进制键固定如下，任何字段或编码变化都必须提升版本：

1. 前缀为 ASCII `volleyball-career-rng`，随后一个 `0x00` 分隔字节和一个版本字节 `0x01`；
2. 之后依次写 TLV：一字节 tag、四字节无符号大端长度、原始 value；
3. tag `0x01` 为 32 字节 seed，`0x02` 为 `streamId` UTF-8，`0x03/0x04` 为四字节无符号大端
   `season/week`，`0x05` 为实体稳定 ID UTF-8，`0x06` 为 `occurrenceId` 的 36 字节小写 UUID D 格式
   ASCII，`0x07` 为四字节无符号大端 `drawIndex`；
4. 字符串按原始 Unicode 标量编码为严格 UTF-8，不做正规化；无效代理项、空 ID、超范围整数或未知
   stream 直接拒绝；
5. 每次哈希再追加 tag `0x08`、长度 `0x00000004` 和四字节无符号大端 `attempt`，从零开始，计算
   `SHA-256(baseKey || attemptTlv)`。

把结果映射到半开整数范围 `[minInclusive, maxExclusive)` 时，宽度必须在 `1..2^32`。取哈希前八字节为
无符号大端 `u64`，在数学整数域令 `remainder = 2^64 mod width`。`remainder = 0` 时所有 `u64` 都接受；
否则令可表示的 `limit = ulong.MaxValue - remainder + 1`，仅当 `u64 < limit` 时接受。被拒绝时递增
`attempt` 重算，接受时返回 `minInclusive + (u64 mod width)`。实现可以用 `UInt128` 直接计算，但禁止把
`2^64` 先存入 `ulong`；这保证 `width = 1`、`2^32` 等整除边界不会溢出或永久拒绝。

首个里程碑的实际调用注册表固定如下；未列出的调用不允许自行占用这些流：

| stream | `season/week` | `entityId` | `occurrenceId` | `drawIndex` 与范围 | 输出解释 |
| --- | --- | --- | --- | --- | --- |
| `tryout` | 固定 `1/0`；`week = 0` 是只允许该流使用的入队 sentinel | 全局唯一稳定 `choiceId` | 该段已保存的发生 ID | 版本化 `outputIds[]` 中的零基索引；`[0, 201)` | 减 `100` 得到 `-100..100` 内部属性单位的扰动，再由该输出配置钳制；每个输出只抽一次 |
| `event` | 事件首次创建时的赛季/周次 | 全局唯一稳定 `optionId` | 已保存的事件发生 ID | 首个样例固定 `0`；`[0, 10000)` | 万分位落入该选项版本化结果区间，解析出的具体后果立即保存 |
| `match_seed` | 赛程项冻结的赛季/周次 | 全局唯一稳定赛程项 ID | `sessionId` | 固定 `0`；`[0, 2^32)` | 结果作为 `uint32` 位模式，以 unchecked 二补码转换为 Shared 契约的 `int32 seed` 并冻结 |

试训的 `outputIds[]` 与事件结果区间属于对应内容版本；`choiceId`、`optionId` 和赛程项 ID 自身必须全局
唯一，不再运行时拼接复合字符串。任何新增抽取都必须分配新 `drawIndex`/流版本并补 fixture，不能插入
后改变既有输出的索引。

以下是锁定 TLV、哈希和范围映射的正式**算法** golden vector；它刻意使用 `[0, 100)`，不是上表
`event` 实际调用的结果区间：

```text
seedHex       = 000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f
streamId      = event
season/week   = 1 / 1
entityId      = event.team_meal
occurrenceId  = 00000000-0000-0000-0000-000000000001
drawIndex     = 0
attempt       = 0
range         = [0, 100)
sha256        = 0955c7f662dbf95ea75c1670c4888602c8844c9d01af69f7a3741bc3286104d4
firstU64      = 672663580396222814
result        = 14
```

golden 测试还必须锁定完整哈希输入十六进制，避免两个错误编码实现只在最终结果上偶然相等：

```text
766f6c6c657962616c6c2d6361726565722d726e6700010100000020000102030405060708090a0b0c0d0e0f101112131415
161718191a1b1c1d1e1f02000000056576656e74030000000400000001040000000400000001050000000f6576656e742e74
65616d5f6d65616c060000002430303030303030302d303030302d303030302d303030302d30303030303030303030303107
0000000400000000080000000400000000
```

拒绝采样单测除上述哈希向量外，还必须向范围映射器注入可控 `u64` 序列：覆盖 `width = 1` 与
`width = 2^32` 的零 remainder 路径，以及首值 `u64 >= limit`、次值 `u64 < limit` 时 `attempt` 恰好增加
一次的路径。不得只用几乎不会自然命中拒绝区间的 SHA-256 样本证明该分支。

实际调用集成测试还必须锁定以下三条向量（均使用上述 seed、`drawIndex/attempt = 0`）：

| stream | season/week | entity / occurrence | range | SHA-256 | raw result / 解释 |
| --- | --- | --- | --- | --- | --- |
| `tryout` | `1/0` | `tryout.attack.choice.power` / `00000000-0000-0000-0000-000000000002` | `[0, 201)` | `99908aaaed0e6c837a7fafb7e21796b716ecc384f5c3f7bcb8ac9a0d3e2ab1d4` | `39` / 扰动 `-61` |
| `event` | `1/1` | `event.team_meal.option.attend` / `00000000-0000-0000-0000-000000000003` | `[0, 10000)` | `498a5ad33f7737a79b2d489870aa5b9c32a287a44c5af6d94bad45877cd9de8d` | `6791` / 万分位 `6791` |
| `match_seed` | `1/1` | `schedule.u1w1.match.01` / `00000000-0000-0000-0000-000000000004` | `[0, 2^32)` | `9983cd3901876225ebe7e0fec528093f18a90471be144c6c4bc1d43aaa896b7d` | `25649701` / `int32 25649701` |

Domain 只通过 `IDeterministicRandom` 请求随机数，禁止直接使用时间、`UnityEngine.Random`、
`System.Random` 或无序集合迭代结果。

生涯为每场比赛派生并冻结独立 `matchSeed`。确定性承诺覆盖生涯领域计算、由相同正式契约驱动的快速
模拟，以及相同 `MatchResult` 下的生涯结算。未来物理接入必须让所有使用随机性的 AI planner 消费由
该 seed 派生的子种子，纯确定性 planner 则删除无效参数，以锁定相同输入下的纯 AI 决策序列；不承诺
Unity 物理直接比赛逐帧或最终比分重放。

事件首次生成时立即持久化 `occurrenceId`、内容业务 ID、已解析参数和随机版本。恢复时复用这些值，不因内容顺序或配置更新重新抽取。插入无关事件、重排、重启和删除后重建不能扰动其他事件结果。

## 8. OperationReceipt

`OperationReceipt` 是 Career 内部非比赛操作的幂等证据，以 `operationId` 为键，至少包含：

```text
operationId
operationKind
inputFingerprint
appliedLineageId
appliedRevision
completedAtUtcMs
outcomeSummary
```

`inputFingerprint` 由操作类型、目标稳定 ID、`occurrenceId`、所需前置状态及规则版本的规范载荷计算。
若同一 `operationId` 和相同 fingerprint 已存在，返回既有摘要；若 ID 相同而 fingerprint 不同，返回
`OperationConflict` 并保留冲突证据。回执、后果和状态转移必须在同一 lineage/revision 写入。

回执保留策略必须由规则版本明确规定；只要操作仍可能被 UI、恢复流程或待处理状态重试，就不得裁剪。首版可保留整条生涯的全部轻量回执，之后再以显式压缩版本设计归档。

## 9. PendingMatch 与 SettlementReceipt

### 9.1 PendingMatch

首个里程碑在调用 FakeMatch 前，Career 先构造冻结上下文并原子提交 `PendingMatch` 与
`AwaitingMatch(sessionId)`。未来 Match 场景或快速模拟接入时复用同一生命周期。至少保存：

- 完整、可反序列化的正式版 `MatchContext`，不是重新构建它所需的引用；
- `sessionId`、创建它的 `operationId`、创建 `lineageId/revision`；
- 全部适用版本轴及 `contextHash`；
- 首个里程碑适用的 6v6 FakeMatch fixture ID 与 fixture 版本，以及双方各六人的冻结身份；
- 按方向聚合后的 `trainingEmphasis` 定点整数映射，以及独立的 `preMatchPriority`；
- 本场已解析的具体数值和来源稳定 ID；
- 对应赛程项 ID、槽位 ID和允许的执行模式。

恢复时禁止重新读取已变化的 ScriptableObject 来重建上下文。不支持其版本组合时，只允许显式迁移或记录结构化放弃，不能猜测性重赛/结算。

`PendingMatch` 不是比赛内检查点，不保存局分、轮转位置、球状态、姿态或物理状态。首个里程碑仅让
FakeMatch 恢复到赛前确认，并复用原 `sessionId`、完整上下文和 `matchSeed` 重新读取固定 fixture。
未来直接比赛也只能从比赛开头重赛；放弃比赛的结果语义和中途续赛 `MatchCheckpoint` 都必须另行
版本化设计，不能由首个里程碑猜测实现。

### 9.2 SettlementReceipt

`SettlementReceipt` 是 Career 所有的比赛结算幂等证据，以 `sessionId` 为键，至少包含：

```text
sessionId
contextHash
resultHash
appliedLineageId
appliedRevision
settledAtUtcMs
settlementSummary
```

结算处理顺序固定为：

1. 从排除 `resultHash` 的规范载荷重算并校验 `resultHash`；
2. 先按 `sessionId` 查询回执；
3. 回执存在且两个哈希一致时，直接返回既有摘要，不再要求 `PendingMatch` 或赛程存在；
4. 回执存在但任一哈希不同，返回 `SessionResultConflict`，不覆盖旧回执；
5. 仅当回执不存在时，验证 `PendingMatch`、赛程、版本、`contextHash` 和
   `expectedVersionToken`，计算首次结算。

首次结算在一个新 revision 中同时写入：原始比赛结果、两个哈希、本里程碑实际实现的属性经验、疲劳、
心态和信任后果、全部周末后果、`SettlementReceipt`、比赛历史更新、已清除的 `PendingMatch` 与
`trainingEmphases[]`，以及下一周
`Planning`。不得拆成多次保存。后续伤病、球探等系统接入后，只能以规则和 schema 升级扩充同一
原子提交，不能另开旁路写盘。

## 10. 规范哈希与快照校验

### 10.1 正式规范

正式 `contextHash`、`resultHash`、`inputFingerprint` 和 `snapshotHash` 均使用 SHA-256、小写十六进制。
规范字节采用 [RFC 8785](https://www.rfc-editor.org/rfc/rfc8785.html) 的 I-JSON 字符串有效性与字符串序列化
规则；所有参与规范哈希的整数还必须位于 I-JSON 可互操作安全范围 `[-9007199254740991,
9007199254740991]`，各 schema 可定义更窄范围。对象字段顺序由每个 schema/contract 版本显式固定，不
使用 JCS 的属性排序；因此不得简单标注为
“完整 JCS”。输出是 UTF-8、无 BOM、JSON token 之间无空白：

- 每个版本固定字段顺序；
- 计算某哈希时排除该哈希字段自身；
- `contextHash`、`resultHash` 和 `inputFingerprint` 明确逐项排除日志时间、传输时间和显示文本等
  非业务元数据；
- 解析器拒绝重复属性名、尾随 token、未知必需字段、无效 UTF-8 和非法单独代理项；
- 字符串以双引号包围，双引号与反斜线分别写为 `\"` 与 `\\`；U+0008/0009/000A/000C/000D 分别写为
  `\b/\t/\n/\f/\r`，其余 U+0000–001F 写小写 `\u00xx`；斜线 `/` 不转义，其他 Unicode 标量按原值
  写 UTF-8，不进行 NFC/NFD 正规化；
- 枚举写契约规定的小写 ASCII 标识，布尔/null 只写 `true`、`false`、`null`；
- 数值只用十进制整数：零写 `0`，负数只带一个前导 `-`，非零数无前导零；不使用浮点、`+`、区域化
  格式或科学计数法；
- 可选字段显式写 `null` 或版本规定默认值；
- 有序列表保持业务顺序；逻辑映射先转为按稳定 ID ordinal 排序的条目数组；
- Unicode 按原始标量序列写 UTF-8，不做区域化比较或平台相关正规化。

`snapshotHash` 为损坏检测覆盖快照中除自身之外的全部已序列化字段，包括玩家输入的显示名、时间戳和
恢复来源；它不能套用比赛契约排除显示文本的规则。这样任意存档字段位翻转都能被发现。

必须提供跨模块共享的 golden byte vectors 与 golden hashes，覆盖引号、反斜线、斜线、全部控制字符
转义、BMP/非 BMP 字符、组合与分解 Unicode、`null`、空集合、列表顺序、映射排序、整数边界、时间字段
和排除字段；负例必须覆盖重复属性、非法 lone surrogate、无效 UTF-8、前导零和尾随 token。先比较规范
字节，再比较哈希，不能只测试同一实现自算自验。

`snapshotHash` 用于发现意外损坏，不是签名，不提供防作弊、防篡改或身份认证保证。

### 10.2 与当前 V1 的关系

当前 `MatchContextV1` 的哈希规则继续只代表 V1。不得在不提升 `contractVersion` 的情况下改为本节规则，也不得宣称当前 V1 的浮点规范化满足正式存档要求。正式比赛接入依赖第 2.4 节 Shared 升级门槛。

## 11. 文件提交协议与单备份

### 11.1 首次创建

1. 在目标同目录创建唯一临时文件；
2. 写入完整快照，调用 `FileStream.Flush(true)` 或经过 Windows 验证的等价耐久写入；
3. 关闭所有句柄；
4. 重新读取临时文件，验证 JSON、Schema、全部适用版本、归属 ID、预期版本令牌关系、业务不变量和
   `snapshotHash`；
5. 目标不存在时，以同卷原子移动发布；无论 API 正常返回还是抛出异常，都在锁内重扫目标与临时文件，
   只有目标文件验证为预期初始版本令牌才发布应用状态；若目标同时出现，返回冲突而不是覆盖。

### 11.2 更新已有存档

1. 在独占锁内完成 CAS；
2. 同目录写入并耐久刷新 `{saveId}.tmp.{operationId}`，关闭后重读并完整验证；
3. 确认 `{saveId}.replace-backup.{operationId}` 不存在；不得把固定 `{saveId}.bak` 直接作为
   `File.Replace` 的 backup destination，因为该 API 可以覆盖既有备份目标；
4. 调用 Windows `File.Replace(temp, main, operationBackup)`。主文件、临时文件和操作专用备份必须同卷；
5. 无论 API 返回成功、抛出异常还是调用进程在响应前终止，下次恢复都必须在相同 `saveId` 锁内重新扫描
   并完整验证主 `.json`、固定 `.bak`、操作专用备份与 `.tmp`，不能只根据返回值推断提交结果。

重扫后只允许以下裁决：

- 主文件等于预期 next version token：业务提交已经发布；若操作专用备份等于 expected token，将其原子
  收敛为唯一固定 `.bak`，旧 `.bak` 只有在收敛成功后才能删除；若找不到 expected 备份候选，有效 next
  主文件仍是权威，但生涯进入 `BackupDegraded` 只读诊断状态，不能回滚或伪造上一修订；
- 主文件仍等于 expected token：提交未发布，旧主文件继续权威；临时与操作专用候选保留到诊断完成后
  再隔离；
- 主文件缺失且操作专用备份等于 expected token：Windows 可能已经移动旧文件；从该候选复制到新的
  同卷修复临时文件，复验后原子移动回主路径，恢复原 expected token，不发布 next snapshot；
- 主文件为其他有效 token、候选归属冲突，或 expected/next 均无法证明：停止自动写入，保留所有证据并
  返回 `AmbiguousReplaceState`，不得猜测最新文件。

稳定状态每条生涯只保留一个已验证的上一 revision `.bak`；操作专用备份只允许存在于替换或恢复窗口。
备份收敛本身也使用操作专用路径和原子替换，并在异常后重扫。隔离文件名至少包含 `saveId`、来源类型、
`operationId` 和冲突序号，不能只用可能碰撞的时间戳。

该协议依据 Windows
[ReplaceFileW 失败状态](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)：部分
错误会让被替换文件改名到 backup path，故“抛异常后旧主文件必在原位置”不是合法不变量。

## 12. 启动恢复、损坏隔离与孤儿扫描

启动扫描按档案逐个执行，不让单个损坏存档阻止其他生涯加载。扫描先按 `operationId` 归组全部候选；
只要发现一个尚未收敛的 `{saveId}.recovery-intent.{operationId}.json`，就必须先在 `saveId` 锁内验证并执行本节的独立恢复裁决，
不得先套用普通临时文件或“有效主文件”规则。意图损坏、归属不符或同一 `saveId` 同时出现多个未收敛
意图时返回 `AmbiguousRestoreState` 并保留全部证据。以下通用分类只适用于没有未收敛恢复意图的候选：

| 发现 | 处理 |
| --- | --- |
| 存在 `{saveId}.recovery-intent.{operationId}.json` | 暂停通用扫描；有效时按本节恢复裁决表处理，损坏或冲突时进入 `AmbiguousRestoreState` |
| 主文件有效 | 作为权威；验证索引摘要，必要时重建摘要 |
| 主文件损坏，`.bak` 有效 | 保持两者原位并只读验证；向玩家提供从上一 revision 恢复，确认前不移动、覆盖或删除任一候选 |
| 主文件与 `.bak` 都损坏 | 以操作 ID 唯一路径隔离两者，生涯标记不可载入；不创建空白同 ID 存档 |
| 存在 `replace-backup.{operationId}` | 与 expected/next token 及主文件共同按第 11.2 节裁决；不能按时间戳猜测或直接删除 |
| 有有效主文件时发现任意临时文件 | 临时文件属于未越过提交点的产物，隔离；主文件保持权威，不提供提升选项 |
| 主文件缺失但 `.bak` 有效 | 保留备份并显示恢复提示；临时文件仍视为未提交，不优先于备份 |
| 只有临时文件且无主文件 | 视为首次创建未提交，隔离并不发布生涯；保留脱敏诊断供开发期排查 |
| 文件版本高于客户端 | 原样保留，拒绝破坏性载入 |
| 存档有效但索引缺项 | 作为孤儿重新挂回内部 `profileId` 对应档案 |
| 索引存在但文件缺失 | 索引项标记缺失，保留诊断；不能自动创建替代生涯 |
| 文件内部归属与目录冲突 | 隔离并报告，不移动到猜测档案 |

隔离操作本身失败时停止对该文件的自动恢复，不覆盖任何候选。恢复 `.bak` 的正式协议是：只读验证
备份，并向玩家明确说明将回退到哪个赛季、周次和更新时间；只有玩家确认后，恢复命令才进入该
`saveId` 队列、取得独占锁，并重新验证主文件状态、损坏文件字节指纹、备份版本令牌和目标路径。

恢复命令先耐久创建并复验唯一 `{saveId}.recovery-intent.{operationId}.json`，至少记录 `saveId`、恢复前主文件是
缺失还是损坏、损坏字节 SHA-256、已确认 `.bak` token、恢复 next token、临时文件名和隔离目标名；该
意图只用于崩溃后裁决，不是可载入生涯，也不能被提升为主档。随后以备份业务状态创建恢复快照：生成
全新 `lineageId`，令 `revision = B + 1`，写入
`restoredFromVersionToken`，更新时戳并重算哈希，再写入同卷临时文件并完整复验。损坏主文件仍存在时，
使用一次 Windows 原子替换发布恢复快照，并把被替换的损坏主文件写入事先确认不存在的
`Quarantine/{saveId}.corrupt.{operationId}.{conflictIndex}.json`；主文件缺失时则以同卷原子移动发布。
两种路径都不改写或删除已验证 `.bak`。无论 API 返回、抛出异常还是进程中断，下次恢复都在锁内按下表
独立裁决，不能复用第 11.2 节要求“操作备份等于有效 expected token”的普通更新规则：

| 重扫证据 | 恢复裁决 |
| --- | --- |
| 主文件等于本次恢复 next token，固定 `.bak` 仍等于确认时的备份 token | 恢复已发布；隔离文件若匹配原损坏字节指纹则保留为证据，缺失时只标记诊断证据降级，不回滚有效恢复主档 |
| 主文件字节指纹仍等于确认时的损坏指纹，固定 `.bak` 仍有效且 token 未变 | 恢复未发布；保留原状态并再次提示，临时恢复快照不得自行提升 |
| 主文件缺失、固定 `.bak` 仍有效且 token 未变、隔离文件匹配原损坏指纹 | 替换可能只完成了旧文件移动；恢复未发布，保留隔离证据并再次提示，不提升临时文件 |
| 恢复前主文件本就缺失，重扫后仍缺失且固定 `.bak` token 未变 | 原子移动未发布；再次提示，不提升临时文件 |
| 主文件为其他有效 token、`.bak` token 改变、损坏指纹不符或候选归属冲突 | 返回 `AmbiguousRestoreState`，保留全部证据并停止自动写入 |

表中“恢复前主文件本就缺失”必须由确认命令的持久诊断上下文证明，不能在异常后反推。恢复重扫覆盖
主文件、固定 `.bak`、本次恢复临时文件和本次隔离目标；任何临时文件都只用于证明/隔离，不作为权威
候选。恢复 token、备份 token 与损坏字节 SHA-256 指纹共同构成本次恢复回执的判定证据。只有裁决已
收敛并保存诊断结果后才能隔离或清理 recovery intent；意图损坏或缺失时不得猜测恢复前状态。

因此，确认前或原子发布前崩溃仍保留原“损坏主文件 + 有效备份”，或者“无主文件 + 有效备份”，下次
启动会再次提示；原子发布后崩溃则已有完整新主档。恢复临时文件永远不会自行提升，可在下次扫描隔离。

新 lineage 让恢复提交不会与曾经成功发布、后来才损坏的旧主档重用 CAS 身份。下一次业务提交必须以
恢复快照的完整版本令牌为 expected token，并在同一 lineage 写入下一 revision；在此之前保留原
`.bak` 与隔离证据。恢复诊断另行记录，但不会把未经确认的临时文件提升为权威状态。

## 13. 自动保存与手动保存边界

自动保存发生在每个具有持久后果的命令边界：确认计划、完成行动/创建待选事件、处理事件、创建
`PendingMatch`，以及比赛、周末和下一周建立的联合结算。它们都提交完整快照；后续合同等命令接入时
遵守相同边界。

手动保存不是绕开命令事务的“随时抓内存”：

- 仅在 `Planning` 且该 `saveId` 没有命令提交中时接受；
- `Planning` 可保存已确认进入领域状态的计划草稿；纯 UI 选择、悬停、未确认弹窗不持久化；
- `Planned` 的正式执行由状态机驱动，不能用手动保存插入半个槽位；
- `AwaitingMatch` 在进入比赛前已经耐久保存，手动保存命令不可用；界面可提示“已自动保存至赛前”；
- `ExecuteSlot` 命令和内存中的 `SettlingMatchAndWeek` 只允许当前原子命令完成或失败回滚，不能另存部分
  后果；
- 手动保存也进入单写者队列、执行 CAS 并递增 revision；若与当前持久快照无业务差异，可返回“已是最新”而不制造空 revision。

退出游戏必须先停止接收新命令并等待有界时间内的当前提交完成；超时则保留旧权威快照，下次按恢复协议处理，不能强行写入半成品。

## 14. 日志、诊断与隐私

日志用于诊断恢复决策，不是存档副本。默认记录：错误码、构建版本、各版本轴、revision、状态名、文件类别、操作阶段、异常类型和经过最小化处理的关联 ID。

隐私约束：

- 不记录 `displayName`、自由文本、完整档案设置、完整 JSON、完整阵容、合同内容或玩家可识别路径；
- `profileId`、`saveId`、`sessionId`、`operationId` 默认记录带本机诊断盐的截断哈希；仅本地显式诊断包可在用户同意后包含原始 ID；
- 路径只记录相对类别与文件扩展名，移除 Windows 用户目录；
- 冲突载荷证据保存到权限受限的本地隔离区，日志仅写其证据 ID 和哈希；
- 随机键只记录 `streamId`、版本和 draw 范围，不记录可反推出私人输入的完整载荷；
- 日志轮转、最大容量和保留天数必须配置；首个里程碑只在开发调试面板提供清理操作，正式设置入口
  随设置页面另行实现；
- 不宣称日志已匿名化，也不自动上传。未来上传需独立同意与隐私设计。

## 15. 失败注入测试矩阵

所有测试同时断言：旧权威文件未被破坏、同一 lineage 内 revision 单调、版本令牌不复用、回执与后果
同生共死、恢复不重复应用、不丢失有效孤儿，并验证 Windows 实际文件语义。

| 区域 | 注入点/场景 | 必须结果 |
| --- | --- | --- |
| 创建 | 临时写入前/中/Flush 后/移动前/移动后、索引更新前后 | 无半文件成为主档；移动后崩溃可扫描为孤儿并挂回 |
| CAS | 入队后、取锁前、锁内重读前后、版本令牌不匹配 | 单写者顺序稳定；冲突不覆盖并重新载入验证 |
| 跨进程 | 第二进程争锁、持锁进程强制终止 | 第二进程拒绝或只读；锁释放后按磁盘权威恢复 |
| 档案/目录索引 | 两条生涯并发更新摘要、并发创建、profile/catalog CAS 冲突 | 不丢索引项；冲突重读；可从权威文件重建 |
| 更新 | 临时写一半、Flush 失败、关闭失败、重读失败、Schema/哈希失败 | 临时文件隔离；主文件与 `.bak` 不被错误替换 |
| 原子替换 | 替换调用前、三个已知 ReplaceFileW 部分失败码、其他异常、成功但返回前终止 | 总是重扫；expected/next/歧义三类裁决正确，不凭异常猜测；稳定后仅一份 `.bak` |
| 操作专用备份 | 已存在目标、主档旧/新/缺失、候选损坏、备份收敛中断 | 路径不碰撞；可恢复旧主档或确认新提交；歧义时停止写入 |
| 周状态机 | 每条状态边的提交前与提交后，覆盖训练、休息、事件、比赛槽、周末 | 相同 ID 重算或由 `OperationReceipt` 去重；后果不重复、不跳过 |
| PendingMatch | 上下文提交前后、场景加载中、比赛退出/闪退 | 未提交不进比赛；已提交回到赛前并复用原上下文/seed/sessionId |
| 比赛结算 | resultHash 校验前后、查回执前后、生成快照后、替换前后 | 无回执才首次结算；成功后重试返回既有摘要 |
| 结算冲突 | 同 session 同哈希、同 session 不同 contextHash/resultHash | 前者幂等；后者 `SessionResultConflict` 且保留原回执和证据 |
| 版本 | 每个版本轴完全匹配、显式兼容、逐级迁移、不受支持、高于客户端 | 只执行声明路径；不支持时保留原件并拒载 |
| 迁移 | 每一级之前/之后失败、迁移输出 ID 缺失、哈希错误 | 恢复迁移前修订；失败产物隔离；不跳级 |
| 确定性 | 重启、插入无关行动、重排行动、同周多实体、删除重建、字段边界碰撞 | 相同键相同结果；其他流不偏移；重建获得新 occurrenceId |
| 规范哈希 | Unicode、null、空集合、列表顺序、映射排序、整数边界、排除字段 | 双端 golden bytes 与 golden hashes 完全一致 |
| 主档损坏 | JSON 截断、位翻转、错误归属、错误版本令牌、错误 hash | 确认恢复前候选保持原位；只读验证 `.bak`；不覆盖证据 |
| 备份损坏 | 主档有效/损坏与备份各组合 | 有效主档不受坏备份影响；双坏时拒载且全部隔离 |
| 备份恢复 | 确认前、取锁/复验、恢复临时文件、原子替换/移动前后，含主档已缺失 | 发布前下次仍提示；发布后新 lineage 唯一有效；损坏证据与 `.bak` 保留 |
| 临时清理 | 临时 revision 低/同/高、hash 有效/无效、主档存在/缺失 | 全部视为未提交产物并隔离；不越过原子提交点；有效 `.bak` 仍可提示恢复 |
| 索引 | 档案索引缺项、重复项、摘要陈旧、索引损坏 | 由有效存档重建；绝不因缺项删除生涯 |
| 手动保存 | Planning、Planned、AwaitingMatch、命令执行中、无业务变化、退出超时 | 只在 Planning 接受；比赛中不做检查点；空保存不增 revision |
| 日志隐私 | 所有错误与冲突路径 | 无显示名、完整 JSON、用户绝对路径或未经处理的关联 ID |

Windows 文件测试至少覆盖首次创建、覆盖替换、杀死子进程后的恢复、独占锁竞争，以及目标目录位于真实 `Application.persistentDataPath` 等价路径的情况。仅使用内存文件系统的测试不能替代这些验证。

## 16. 实施顺序与完成定义

### 16.1 建议顺序

1. Career 自有 `CareerPlayerAttributes`、稳定 ID 注册表、版本值对象、规范 JSON/哈希组件及 golden
   vectors；移除 Domain/Application 对 Shared 比赛 DTO 的引用；
2. `LocalPlayerProfile`、索引 DTO、`CareerSaveSnapshot` 最小骨架；
3. 每 `saveId` 队列、文件锁、CAS、临时验证、原子替换和单 `.bak`；
4. 启动扫描、替换异常裁决、隔离与孤儿重挂；
5. 周状态机、键控随机和 `OperationReceipt`；
6. 完成 Shared 升级门槛、`Career.MatchIntegration` DTO 映射和异步比赛端口；
7. `PendingMatch`、`SettlementReceipt` 与 12 人 6v6 FakeMatch 联合闭环；现有 `FormalIndoor6v6` 直接
   runner 和快速模拟是后续消费者；
8. 迁移器、恢复 UI、日志隐私和完整失败注入矩阵。

### 16.2 “已完成”的最低证据

只有满足以下证据，才能把本设计从“待实现”更新为“已实现”：

- 对应 DTO、仓储和状态机代码存在且模块边界正确；
- EditMode/PlayMode、golden fixture、Windows 文件恢复测试通过；
- Shared V2 由 Career.MatchIntegration 与 FakeMatch 共同消费，版本/哈希不匹配会拒绝；冻结 Match 继续
  通过 V1 回归，物理 V2 producer 不属于本里程碑；
- 重复比赛结果返回既有 `SettlementReceipt`，冲突结果不会覆盖；
- 每个周转移提交前后强制终止均不重复或丢失后果；
- 主档损坏后保留隔离原件，并可在玩家确认后以新 lineage 从唯一已验证上一 revision 备份恢复；
- 孤儿扫描和替换异常裁决均有可重复测试；
- 文档中的当前代码事实已按实现更新，且未把规划字段写成 V1 既有能力。
