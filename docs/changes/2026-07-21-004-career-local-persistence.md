# CHG-20260721-004：实现本地档案与生涯持久化

- 日期：2026-07-21
- 状态：已完成
- 负责人：Career
- 影响模块：Career.Application / Career.Persistence / Career.Tests / Docs
- 交互级别：跨模块（重点）
- 关联分支：`feature/career-local-persistence`
- 关联提交或 PR：`98e9535`（主体实现）→ `28698af`（review-fix）→ 本记录所在的后继 Critical fix 提交

> [!IMPORTANT]
> 本变更实现离线首版的本地档案和生涯文件协议。`Career.Persistence` 从本阶段起依赖
> `Career.Application` 以实现其仓储端口；Match、Shared V1、账号登录、云同步和跨电脑识别均不改动。

## 改动摘要

为 Schema V1 快照加入固定字段顺序的严格 UTF-8 JSON、规范 SHA-256、完整版本令牌 CAS、同目录耐久
临时写入、Windows 原子替换后重扫、唯一上一修订备份、损坏分类和索引重建。建立多个本地档案、每档案
多条独立生涯的不可变模型与仓储端口，并使用可注入文件系统覆盖失败窗口。

## 范围约束

- 只实现本地 `Profiles` 目录、`profile.json`、`profiles-index.json` 和 Career Schema V1 文件。
- 不实现账号、密码、在线请求、云同步、删除、设置、加密或遥测。
- 不加入 `PendingMatch`、比赛结果或 Shared V2；Match 与 Shared V1 冻结目录保持不变。
- 临时文件永不自动提升为权威主档；索引可以由有效主档重建，但不能删除或覆盖孤儿生涯。

## 跨模块交互重点

- 接口提供方：`Career.Application` 提供本地档案/生涯仓储端口和结果类型。
- 接口实现方：`Career.Persistence` 实现严格 codec、文件协议、CAS 和扫描恢复。
- 使用方：后续 Career 创建、试训、周执行和 UI 用例；本阶段不接入 Match。
- 兼容性：只有当前开发期 Schema V1；未知、缺失、重复字段或不支持版本均拒载并保留原字节。
- 后续负责人无需修改 Match 或 Shared V1 代码。

## 验证

- [x] Career/Profile/Catalog 规范字节与 golden hash；Career 完整版本令牌以及 Profile/Catalog
  revision+hash CAS 均有回归测试。
- [x] 严格解析负例覆盖重复/未知/缺失属性、非法 UTF-8/代理项、非法整数、非规范空白和尾随 token。
- [x] 创建、载入、CAS 更新、版本冲突、多个档案与多条生涯互不影响；创建不会绕过固定备份、
  遗留临时文件、替换备份或恢复意图。
- [x] 主档/固定备份/操作临时文件的故障注入覆盖替换前后抛错、部分替换、备份收敛失败与
  lost-success；损坏主档恢复要求确认备份令牌和主档字节指纹，并发布新 lineage。
- [x] Profile/Catalog 索引可从权威文件重建；缺失 Career 只标记摘要，不制造替代存档，也不删除孤儿。
- [x] Windows 实际文件语义与锁竞争通过；最终 `LocalPersistenceRepositoryTests` 为 57/57，
  `Volleyball.Career.EditModeTests` 为 196/196，仓库工具测试为 8/8，
  `validate_repository.py --base 22745cc` 通过。
- [x] 独立 agent 首轮复核发现的 1 个 P1 与 4 个 P2、后续 re-review 发现的 fixed backup
  关系数据丢失 Critical，均经 focused RED/GREEN 修复；最终无已知未关闭 finding。

阶段 2 审计新增的 TDD 证据保存在 `TestResults/stage2-red-*.xml` 与对应
`TestResults/stage2-green-*.xml`：仅固定备份 0/1→1/1，三类创建遗留工件 22/25→25/25，三类跨
operation 提交工件 25/28→3/3；恢复 authority 0/2→2/2、Career/Profile convergence rescan 各
0/1→1/1、Profile authority 工件 0/3→3/3、unsupported rebuild 0/2→2/2、跨 lineage convergence
0/1→1/1。review-fix 继续覆盖 unsupported 固定备份 0/2→2/2、Career Create 遗漏的 convergence/
repair/recovery temp 0/3→3/3、Catalog 外来 temp/repair/replace 0/3→3/3、未知 Career version axis
0/2→2/2，以及 Profile Load 孤立 temp/repair 0/2→2/2。第三轮 fixed backup 关系回归 0/4→7/7：
Career/Profile 均拒绝更新或同 revision 冲突的 Valid backup，同时允许严格前一 revision，Career 另允许
fixed token 精确等于恢复来源；最终 repository fixture 57/57。测试结果目录不进入版本控制。

## 回滚与风险

主要风险是把 API 异常误判成磁盘状态、宽松 JSON 接受语义缺失，以及索引更新覆盖已经成功的生涯。
所有提交以重读验证后的文件为准；发生歧义时进入只读诊断状态。回滚可整体移除此持久化实现，不改动
阶段 1 的纯领域快照或冻结比赛模块。
