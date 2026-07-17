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

当前 `MatchContextV1` 的哈希实现是该 V1 自身的固定实现：包含显示名、数值枚举和 IEEE `float` 的 round-trip 文本。当前 `PlayerAbilitySnapshotV1` 和 `PlayerMatchStatsV1.Workload` 也使用 `float`。这些是现状，不等于本文第 10 节的正式规范哈希已经实现。

### 2.2 不得假设当前 V1 已有的字段

当前 `MatchContextV1` **没有**以下字段：`contentVersion`、`rulesetVersion`、`matchSimulationVersion`、`matchRandomAlgorithmVersion`、直接比赛/快速模拟模式、赛制、赛事与轮次、比赛重要性、体能、伤病限制、首发状态、战术职责、轮转、赛前重点。

当前 `MatchResultV1` **没有** `resultHash`、结束状态（含 `abandoned`）、逐局比分、上场时间、回合数、移动/起跳/落地负荷、结构化伤病观察、战术/轮转/关键分信号或结构化关键事件。

`PlayerId`、`TeamId` 只保证字符串格式与值相等；稳定 ID 的注册、不可复用、tombstone 和内容资产校验仍待实现。Match 原型里的场上槽位 ID 不得直接写入生涯存档。

### 2.3 Shared 升级门槛

在 Career 可以正式创建 `PendingMatch` 或结算 FakeMatch 前，必须完成一次受控 Shared 契约升级：

1. 定义包含第 9 节全部冻结输入的新版 `MatchContext`，并加入所有比赛相关版本轴。
2. 定义包含 `resultHash`、结束状态、逐局与结构化负荷事实的新版 `MatchResult`。
3. 将跨模块数值改为带明确量纲的定点整数，例如毫米、毫秒、0–10000 能力基点和整数负荷；禁止在正式规范哈希载荷中使用浮点数。
4. 实现第 10 节的规范 JSON、golden bytes 和 golden hashes。
5. 为 Career 与 FakeMatch 提供双方读取的同一组 fixture；未来直接比赛与快速模拟必须复用同一结构，
   各自在首次接入时新增自身 fixture 和适用版本。
6. 明确 V1 兼容策略。若 V1 已被持久化或被外部调用方使用，不能原地改字段语义，必须新增契约版本和显式迁移/只读解析器；若在任何可分发存档产生前废弃 V1，也必须以变更文档、fixture 更新和双方模块联合测试记录该决定。

门槛完成前，Career 可以实现与测试本地档案、通用快照、CAS 仓储和不依赖正式比赛 DTO 的周状态，但不得声称比赛恢复与幂等结算闭环已经完成。

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
├─ settings
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
      └─ Careers/
         ├─ {saveId}.json
         ├─ {saveId}.bak
         ├─ {saveId}.tmp.{operationId}
         ├─ {saveId}.delete.{operationId}
         └─ Quarantine/
```

所有路径组件使用校验后的稳定 ID，不接受显示名或任意用户输入拼接路径。临时文件必须和目标文件处于同一目录、同一卷。

`profile.json` 使用与生涯快照相同的“同卷临时文件 -> 重读验证 -> 带单备份原子替换”协议；它的
`.bak` 只保护档案元数据和可重建索引，不代替任何生涯备份。`profiles-index.json` 是可重建目录，仍需
原子替换，但损坏时以扫描有效 `profile.json` 重建，不能据其内容删除目录。

### 3.3 创建、索引修复与删除

创建生涯的提交顺序固定为：

1. 在档案级独占锁内生成不可复用的 `saveId`、初始 `lineageId` 和初始 `revision`；
2. 原子创建并校验 `{saveId}.json`；
3. 再以 `expectedProfileRevision` 原子更新 `profile.json` 的索引。

若步骤 2 后崩溃，启动扫描根据存档内部 `profileId` 识别有效孤儿，把摘要重新挂回正确档案。禁止因索引暂缺而删除有效存档。

删除生涯的提交顺序固定为：

1. 依次获取档案级锁和该生涯锁，把主文件原子重命名为 `{saveId}.delete.{operationId}`；
2. 以档案 CAS 原子移除索引项；
3. 索引成功后物理删除删除标记与对应 `.bak`。

启动扫描遇到删除标记时：若索引已无该 `saveId`，完成物理清理；若索引仍指向它且删除操作没有完成证据，则恢复原名并保留生涯。扫描器不能同时保留索引又删除唯一有效文件。若主文件与删除标记均存在或归属冲突，全部保留并隔离冲突副本，要求显式恢复决策。

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
│  └─ pendingEvent
├─ player / team / coachTrust / fatigue / mindset
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
训练/休息/事件、比赛创建、比赛结算、周末结算和删除都必须经过对应队列；后续合同等系统接入时也必须
复用同一队列。UI、场景和 Match 模块均不能绕过仓储直接写盘。

只有原子替换成功后，应用层才向 UI 发布新状态并处理下一命令。写盘中的内存草稿不是权威状态。

### 5.3 档案与总索引并发

`profile.json` 使用 `profileRevision + profileSnapshotHash` 做 CAS，并为每个 `profileId` 建立进程内队列和
跨进程文件锁。不同生涯可以独立提交权威快照，但随后重建 `CareerIndexEntry` 时必须进入所属档案队列；
索引更新失败不回滚已经成功的生涯提交，启动扫描会按权威快照修复摘要。

`profiles-index.json` 使用独立的全局目录队列、文件锁、`catalogRevision` 和 `catalogHash`。创建/删除本地
档案必须走该 CAS；删除生涯或修复孤儿时按“档案锁 -> 生涯锁”的固定顺序取锁。普通生涯提交不在持有
生涯锁时等待档案锁，避免反向锁序。两个不同 `saveId` 的并发创建、删除或摘要更新不得通过最后写入者
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
不能重复领取已确认结果。第三段提交同时建立完整球员、球队归属和第一周 `Planning`。

```text
Planning
  -> Planned(weekPlanId, slotActionIds, nextSlotIndex = 1)
  -> ExecutingSlot(slotIndex = 1, operationId)
  -> AwaitingEventChoice(occurrenceId)
  -> Planned(..., nextSlotIndex = 2)
  -> ExecutingSlot(slotIndex = 2, operationId)
  -> Planned(..., nextSlotIndex = 3)
  -> AwaitingMatch(sessionId)        // 仅比赛槽
  -> 下一周 Planning                 // 比赛、周末与推进同一原子提交
```

槽位 1 后果、事件实例和 `AwaitingEventChoice` 在同一 revision 提交。`pendingEvent` 至少冻结
`occurrenceId`、内容业务 ID、两项选项 ID、已解析参数、随机算法版本和随机键所需上下文；事件选择的
后果、回执、`pendingEvent` 清除与恢复到 `Planned(nextSlotIndex = 2)` 属于下一次原子提交。

`SettlingMatchAndWeek(operationId)` 只是在应用命令执行期间的内存阶段，不是可单独保存或恢复的权威
状态。提交前崩溃仍为 `AwaitingMatch`；提交成功后直接是下一周 `Planning`，不存在只结算比赛或只推进
周次的中间快照。

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

每份生涯创建时固定 `careerSeed`，领域层不维护全局顺序随机游标。每次抽取由以下键独立派生：

```text
careerRandomAlgorithmVersion + careerSeed + streamId + season + week
+ entityId + occurrenceId + drawIndex
```

`streamId` 来自版本控制注册表。首个里程碑只注册有真实调用方的 `tryout`、`training` 和 `event`；
`scouting`、`offer` 等未来流在首次实现对应系统时新增。既有值不得改义或复用。同一操作多次抽取显式
增加 `drawIndex`。没有实体时使用注册表中的明确空实体 ID，不使用空字符串。

键编码使用域分隔符 `volleyball-career-rng`，每个字段含类型标签与长度前缀；整数固定端序，字符串为 UTF-8。Domain 只通过 `IDeterministicRandom` 请求随机数，禁止直接使用时间、`UnityEngine.Random`、`System.Random` 或无序集合迭代结果。

生涯为每场比赛派生并冻结独立 `matchSeed`。确定性承诺覆盖生涯领域计算、由相同正式契约驱动的快速
模拟，以及相同 `MatchResult` 下的生涯结算；不承诺 Unity 物理直接比赛逐帧重放。

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
- 首个里程碑适用的 FakeMatch fixture ID 与 fixture 版本；
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
心态和信任后果、全部周末后果、`SettlementReceipt`、比赛历史更新、已清除的 `PendingMatch` 和下一周
`Planning`。不得拆成多次保存。后续伤病、球探等系统接入后，只能以规则和 schema 升级扩充同一
原子提交，不能另开旁路写盘。

## 10. 规范哈希与快照校验

### 10.1 正式规范

正式 `contextHash`、`resultHash`、`inputFingerprint` 和 `snapshotHash` 均使用 SHA-256、小写十六进制。规范字节是 UTF-8、无 BOM、无额外空白的规范 JSON：

- 每个版本固定字段顺序；
- 计算某哈希时排除该哈希字段自身；
- `contextHash`、`resultHash` 和 `inputFingerprint` 明确逐项排除日志时间、传输时间和显示文本等
  非业务元数据；
- 枚举写契约规定的小写 ASCII 标识；
- 数值只用整数，不使用浮点数、区域化格式或科学计数法；
- 可选字段显式写 `null` 或版本规定默认值；
- 有序列表保持业务顺序；逻辑映射先转为按稳定 ID ordinal 排序的条目数组；
- Unicode 按原始标量序列写 UTF-8，不做区域化比较或平台相关正规化。

`snapshotHash` 为损坏检测覆盖快照中除自身之外的全部已序列化字段，包括玩家输入的显示名、时间戳和
恢复来源；它不能套用比赛契约排除显示文本的规则。这样任意存档字段位翻转都能被发现。

必须提供跨模块共享的 golden byte vectors 与 golden hashes，覆盖 Unicode、`null`、空集合、列表顺序、映射排序、整数边界、时间字段和排除字段。先比较规范字节，再比较哈希，不能只测试同一实现自算自验。

`snapshotHash` 用于发现意外损坏，不是签名，不提供防作弊、防篡改或身份认证保证。

### 10.2 与当前 V1 的关系

当前 `MatchContextV1` 的哈希规则继续只代表 V1。不得在不提升 `contractVersion` 的情况下改为本节规则，也不得宣称当前 V1 的浮点规范化满足正式存档要求。正式比赛接入依赖第 2.3 节 Shared 升级门槛。

## 11. 文件提交协议与单备份

### 11.1 首次创建

1. 在目标同目录创建唯一临时文件；
2. 写入完整快照，调用 `FileStream.Flush(true)` 或经过 Windows 验证的等价耐久写入；
3. 关闭所有句柄；
4. 重新读取临时文件，验证 JSON、Schema、全部适用版本、归属 ID、预期版本令牌关系、业务不变量和
   `snapshotHash`；
5. 目标不存在时，以同卷原子移动发布；若目标同时出现，返回冲突而不是覆盖。

### 11.2 更新已有存档

1. 在独占锁内完成 CAS；
2. 同目录写入并耐久刷新临时文件；
3. 关闭后重读并完整验证；
4. 使用 Windows 带备份的原子替换：新临时文件替换 `{saveId}.json`，被替换的已验证主文件成为 `{saveId}.bak`；
5. 替换成功后再发布应用状态。

每条生涯只保留一个已验证的上一 revision `.bak`，不滚动多代。不得先删除旧 `.bak` 再进行非原子覆盖。备份也必须验证内部 `profileId/saveId/revision` 和哈希。

替换前失败，旧主文件仍是权威；替换后响应前崩溃，重试通过完整版本令牌或回执识别已经成功的提交。

## 12. 启动恢复、损坏隔离与孤儿扫描

启动扫描按档案逐个执行，不让单个损坏存档阻止其他生涯加载。扫描结果是明确分类，不猜测覆盖：

| 发现 | 处理 |
| --- | --- |
| 主文件有效 | 作为权威；验证索引摘要，必要时重建摘要 |
| 主文件损坏，`.bak` 有效 | 保持两者原位并只读验证；向玩家提供从上一 revision 恢复，确认前不移动、覆盖或删除任一候选 |
| 主文件与 `.bak` 都损坏 | 两者隔离，生涯标记不可载入；不创建空白同 ID 存档 |
| 有有效主文件时发现任意临时文件 | 临时文件属于未越过提交点的产物，隔离；主文件保持权威，不提供提升选项 |
| 主文件缺失但 `.bak` 有效 | 保留备份并显示恢复提示；临时文件仍视为未提交，不优先于备份 |
| 只有临时文件且无主文件 | 视为首次创建未提交，隔离并不发布生涯；保留脱敏诊断供开发期排查 |
| 文件版本高于客户端 | 原样保留，拒绝破坏性载入 |
| 存档有效但索引缺项 | 作为孤儿重新挂回内部 `profileId` 对应档案 |
| 索引存在但文件缺失 | 索引项标记缺失，保留诊断；不能自动创建替代生涯 |
| 删除标记存在 | 按第 3.3 节完成删除或回滚，不凭文件名猜测 |
| 文件内部归属与目录冲突 | 隔离并报告，不移动到猜测档案 |

隔离操作本身失败时停止对该文件的自动恢复，不覆盖任何候选。恢复 `.bak` 的正式协议是：只读验证
备份，并向玩家明确说明将回退到哪个赛季、周次和更新时间；只有玩家确认后，恢复命令才进入该
`saveId` 队列、取得独占锁，并重新验证主文件状态、损坏文件字节指纹、备份版本令牌和目标路径。

恢复命令以备份业务状态创建恢复快照：生成全新 `lineageId`，令 `revision = B + 1`，写入
`restoredFromVersionToken`，更新时戳并重算哈希，再写入同卷临时文件并完整复验。损坏主文件仍存在时，
使用一次 Windows 原子替换发布恢复快照，并把被替换的损坏主文件作为该替换的备份目标直接写入
`Quarantine/{saveId}.corrupt.{utcMs}.json`；主文件缺失时则以同卷原子移动发布。两种路径都不改写或
删除已验证 `.bak`。

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
- `ExecutingSlot` 和内存中的 `SettlingMatchAndWeek` 只允许当前原子命令完成或失败回滚，不能另存部分
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
| 档案/目录索引 | 两条生涯并发更新摘要、并发创建/删除、profile/catalog CAS 冲突 | 不丢索引项；冲突重读；可从权威文件重建 |
| 更新 | 临时写一半、Flush 失败、关闭失败、重读失败、Schema/哈希失败 | 临时文件隔离；主文件与 `.bak` 不被错误替换 |
| 原子替换 | 替换调用前、系统调用失败、替换成功但返回前终止 | 前者旧主档有效；后者新版本令牌有效且重试幂等；仅一份上一 revision 备份 |
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
| 删除 | 重命名前后、索引更新前后、物理清理前后、主档与标记并存 | 扫描可完成或回滚；不会同时丢失索引和唯一有效文件 |
| 手动保存 | Planning、Planned、AwaitingMatch、命令执行中、无业务变化、退出超时 | 只在 Planning 接受；比赛中不做检查点；空保存不增 revision |
| 日志隐私 | 所有错误与冲突路径 | 无显示名、完整 JSON、用户绝对路径或未经处理的关联 ID |

Windows 文件测试至少覆盖首次创建、覆盖替换、杀死子进程后的恢复、独占锁竞争，以及目标目录位于真实 `Application.persistentDataPath` 等价路径的情况。仅使用内存文件系统的测试不能替代这些验证。

## 16. 实施顺序与完成定义

### 16.1 建议顺序

1. 稳定 ID 注册表、版本值对象、规范 JSON/哈希组件及 golden vectors；
2. `LocalPlayerProfile`、索引 DTO、`CareerSaveSnapshot` 最小骨架；
3. 每 `saveId` 队列、文件锁、CAS、临时验证、原子替换和单 `.bak`；
4. 启动扫描、隔离、孤儿重挂与删除恢复；
5. 周状态机、键控随机和 `OperationReceipt`；
6. 完成 Shared 升级门槛；
7. `PendingMatch`、`SettlementReceipt` 与 FakeMatch 联合闭环；真实直接比赛和快速模拟是后续消费者；
8. 迁移器、恢复 UI、日志隐私和完整失败注入矩阵。

### 16.2 “已完成”的最低证据

只有满足以下证据，才能把本设计从“待实现”更新为“已实现”：

- 对应 DTO、仓储和状态机代码存在且模块边界正确；
- EditMode/PlayMode、golden fixture、Windows 文件恢复测试通过；
- Shared 新契约由 Career 与 Match 两端共同消费，版本/哈希不匹配会拒绝；
- 重复比赛结果返回既有 `SettlementReceipt`，冲突结果不会覆盖；
- 每个周转移提交前后强制终止均不重复或丢失后果；
- 主档损坏后保留隔离原件，并可在玩家确认后以新 lineage 从唯一已验证上一 revision 备份恢复；
- 孤儿扫描和删除中断均有可重复测试；
- 文档中的当前代码事实已按实现更新，且未把规划字段写成 V1 既有能力。
