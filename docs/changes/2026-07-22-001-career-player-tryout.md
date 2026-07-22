# CHG-20260722-001：生涯球员创建与大学试训

- 日期：2026-07-22
- 状态：已完成
- 负责人：Career
- 影响模块：Volleyball.Career.Domain / Application / Persistence（兼容性验证）
- 交互级别：模块内部
- 关联分支：`feature/career-player-tryout`
- 关联提交或 PR：本分支提交

## 改动摘要

实现纯 C# 的生涯创建、三段大学试训和第三段原子入队。成功流程从 revision 1 的
`CareerCreated` 依次推进到 revision 2/3 的 `Tryout(2/3)`，并在 revision 4 同一回执、快照和提交中
建立完整球员、正式入队及大学第一赛季第一周未确认的 `Planning`。存档 schema 仍为 V1；主攻位置是
`CareerPlayerRecord` 的 Career 自有派生不变量，不新增序列化字段。

不包含 UI、结果页继续命令、场景或预制体、比赛执行、周行动执行、社交事件、伤病、账号或在线服务、
培养方向、球员类型、隐藏单项潜力上限和潜力揭示机制。

## 具体变更

- 新增 `careerRandomAlgorithmVersion = 1` 的严格 TLV/SHA-256/拒绝采样实现；生产种子由系统密码学随机源生成，测试注入固定种子。
- 注册随机流仅为 `tryout`、`event`、`match_seed`；本改动的业务流程只调用 `tryout`。
- 新增 fingerprint schema V1：固定字段顺序、RFC-8785 兼容字符串转义、严格 UTF-8、UUID-D 小写、十进制不受区域设置影响、SHA-256 小写十六进制。
- 新增不可变试训内容/规则 V1，数值明确标记为首个纵向切片调优 fixture，不代表最终平衡。
- 新增结构化应用结果：`Applied`、`Existing`、`OperationConflict`、`InvalidInputOrState`、`VersionConflict`、`NotFound`、`PersistenceFailure`。
- 创建前先验证输入并确认目标缺失，之后才消费 `ICareerSeedSource`；创建回执与 revision 1 同写。
- 确认阶段先加载权威快照并查询回执。重试从持久化 raw perturbation 与 V1 内容重建解释，不重抽；冲突和失败不返回推测快照。
- 第三段在 revision 4 同次提交中建立八项能力（XP 均为 0）、可见潜力/疲劳/心态/信任、固定主攻、固定大学队和仅槽位 3 为比赛的第一周计划。

### 稳定业务 ID

- 球队：`team.university.first`
- 阶段：`tryout.attack`、`tryout.reception_defense`、`tryout.scrimmage`
- 进攻选择：`tryout.attack.choice.power`、`tryout.attack.choice.serve`、`tryout.attack.choice.approach`
- 接防选择：`tryout.reception_defense.choice.first_touch`、`tryout.reception_defense.choice.floor_defense`、`tryout.reception_defense.choice.net_read`
- 对抗选择：`tryout.scrimmage.choice.endurance`、`tryout.scrimmage.choice.composure`、`tryout.scrimmage.choice.initiative`
- 输出：`tryout.output.spike`、`tryout.output.serve`、`tryout.output.jump`、`tryout.output.reception`、`tryout.output.defense`、`tryout.output.block`、`tryout.output.movement`、`tryout.output.stamina`、`tryout.output.fatigue`、`tryout.output.mindset`、`tryout.output.coach_trust`

### 版本与状态转换

- save schema/content/rules/random：`1/1/1/1`
- tryout catalog/rules：`1/1`
- operation fingerprint schema：`1`
- revision 1：`CareerProgressionState.Created()`，当前试训阶段 1
- revision 2：确认阶段 1，`CareerProgressionState.Tryout(2)`
- revision 3：确认阶段 2，`CareerProgressionState.Tryout(3)`
- revision 4：确认阶段 3 + 正式入队，`CareerProgressionState.Planning(...)`

## 跨模块交互重点

- 接口提供方：Career Domain / Application
- 接口使用方：后续 Career Presentation
- 数据方向：`Career Application -> ICareerSaveRepository -> Career Persistence`
- 兼容性：兼容。存档 schema V1、Shared 和 Match 冻结路径均不变；Application 仍只引用 Domain。
- 消费方或后续负责人需要做什么：后续 UI 只调用创建/确认命令；结果页“继续”仅导航，不得新增领域命令或 revision 5。

## 文件

- Domain：随机请求/接口、试训目录、入队快照工厂、固定位置派生属性。
- Application：密码学种子、确定性随机、命令/结果、fingerprint V1、`CareerOnboardingService`。
- Tests：`CareerRandomAndTryoutCatalogTests`、`CareerOnboardingServiceTests`。
- Persistence：无生产文件改动；通过真实 `LocalCareerSaveRepository` 集成往返验证。

## 验证

- [x] 新增 focused EditMode：20 passed / 0 failed / 0 skipped（Unity 6000.3.20f1，Windows batchmode；`TestResults/stage3-focused.xml`）
- [x] 全部 Career EditMode：216 passed / 0 failed / 0 skipped（`TestResults/stage3-career.xml`）
- [x] 真实本地存档创建、三段提交、schema V1 载入往返
- [x] Python：8 passed；仓库验证器通过；`git diff --check` 通过；Match/Shared Runtime 冻结路径相对 `97e17bb` 无差异
- [ ] PlayMode / 手动场景：不适用，本改动为纯 C# 且明确不包含 UI/场景

## 回滚与风险

主要风险是 fingerprint schema、随机 TLV 或内容 ID 在未来被无意改义；golden bytes/digest/result 与精确目录
fixture 测试用于阻止静默漂移。回滚可撤销本提交；由于 schema 未变化，不需要存档迁移或 Shared/Match 回滚。
