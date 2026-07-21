# CHG-20260721-003：建立正式生涯快照模型

- 日期：2026-07-21
- 状态：已完成
- 负责人：Career
- 影响模块：Career.Domain / Career.Application / Career.Persistence / Career.Tests / Bootstrap
- 交互级别：跨模块（重点）
- 关联分支：`feature/career-save-model`
- 关联提交或 PR：待本地提交

> [!IMPORTANT]
> Career 核心从 Shared V1 的七项浮点能力、`MatchContextV1/MatchResultV1` 与同步 `IMatchGateway.Play`
> 解耦。Shared V1 和 Match 保持冻结；本变更不新增 V2、比赛 runner、磁盘 I/O 或任何物理比赛接线。

## 改动摘要

建立首个正式但尚不落盘的 Schema V1 生涯模型：八项整数能力与成长经验、身份/版本值对象、32 字节
生涯 seed、可恢复试训、周权威状态、已解析事件、带稳定目标的操作回执、完整版本令牌和
`CareerSaveSnapshot`。普通行动执行中是 Application 内存态，不落入权威快照。新增纯 C#
Persistence DTO/映射层，用显式字段完成深复制往返与非法状态拒绝；文件系统、CAS、原子替换、JSON
codec 与哈希重算属于下一持久化阶段。

Schema DTO 只是领域映射边界，不是 JSON 解析器。其中的非空数值字段无法单独区分
“JSON 字段缺失”与数值默认值；阶段 2 的 presence-aware 严格 codec 必须在构造 DTO 前验证字段存在性、
重复属性、未知属性和尾随 token，不得直接把宽松反序列化结果视为已校验存档。

## 具体变更

- `Career.Domain`：新增八项 `0..10000` 能力、经验、稳定 ID、版本、进度状态、回执和快照不变量；
  `CareerPlayerRecord` 不再保存 `PlayerAbilitySnapshotV1`。
- 试训保存三段稳定发生 ID、选择和已解析输出；已确认周与草稿均固定第三槽比赛；普通行动、事件
  与试训回执保存类型化目标和具体后果摘要。
- 快照交叉校验 Create / 三段试训 / 周确认 / 槽 1 / 事件 / 槽 2 回执的严格修订链、当前状态前沿、
  全局 `occurrenceId` 唯一性和随机版本一致性。
- `Career.Application`：删除 `CareerMatchRequest` 与同步 `IMatchGateway`；保留真实的回执查询/冲突判断
  应用逻辑，不创建无人调用的异步比赛接口。
- `Career.Persistence`：新增 `noEngineReferences` 程序集、Schema V1 DTO 与 Domain 往返映射；仅因
  `PlayerId/TeamId` 复用 Shared 稳定 ID，不使用 Shared 比赛 DTO；不调用 `System.IO`、不接受路径，
  也不实现仓储。
- `Career.Tests`：覆盖数值边界、跨字段状态、稳定 ID、版本/seed、回执幂等、DTO 深复制和程序集边界。
- `Bootstrap/Presentation`：把旧比赛请求占位类型替换为新的 Career 应用边界类型；不改变场景行为。

## 跨模块交互重点

- 接口提供方：Career Domain/Application/Persistence。
- 接口使用方：Career Presentation 与 Bootstrap 仅引用公开 Career 边界；Match 无消费变化。
- 数据方向：本阶段没有比赛数据流；仅 `Career Domain <-> Persistence Schema V1 DTO`。
- 兼容性：现有 Shared V1/Match 完全不变；旧 Career 原型没有正式存档，因此删除同步网关和浮点属性不
  提供迁移层。
- 消费方或后续负责人需要做什么：Match 无需改代码。下一阶段以该模型实现本地文件与 CAS，不得把
  Shared V1 DTO 重新放回 Career Domain/Application。

## 验证

- [x] Unity `6000.3.20f1` 完整 EditMode：`310/310` 通过。
- [x] Schema V1 Domain/DTO 往返、非法状态和深复制测试通过。
- [x] Domain/Application 公共签名无 Shared 比赛 DTO 或同步 `Play`。
- [x] 仓库门禁、asmdef 依赖、Match/V1 冻结检查通过；策略测试 `8/8` 通过。
- [x] 独立 agent 复核无 P1/P2。
- [x] PlayMode、场景、磁盘 I/O：本阶段不适用。

## 回滚与风险

主要风险是过早加入无调用方字段，或让可变 DTO/集合绕过快照不变量。Schema V1 只包含创建、入队、
Planning、普通行动执行和事件等待所需状态；`PendingMatch`、`SettlementReceipt`、伤病、合同与未来系统
均不占位。映射必须深复制并对未知枚举、重复 ID、非法 seed/hash/版本及矛盾状态失败。回滚可整体撤销
本变更；不会修改冻结 Match/V1 或磁盘上的正式存档。
