# CHG-20260723-004：Career 恢复、开发诊断与 Windows 构建

- 日期：2026-07-23
- 状态：进行中（自动化与构建已完成，实体输入验收待完成）
- 负责人：Career / Bootstrap
- 影响模块：Career.Application / Career.Persistence / Career.Presentation / Bootstrap / Tests / Tools / Docs
- 交互级别：跨模块（重点）
- 关联分支：`feature/shared-career-fake-contract`
- 关联提交：`bff4dee` / `08faa8b` / `820198d` / `8266332` / `0a28bf7`

> [!IMPORTANT]
> 本次没有修改 Match 或 legacy Shared V1。Windows 构建继续使用 Shared Match V2 的固定
> 6v6 fixture；真实物理 6v6 runner、直接控制和 Match 场景切换仍延期。恢复和诊断入口由
> Bootstrap 组装，Career Presentation 不直接读写磁盘。

## 改动摘要

- 将已经存在于 Persistence 的 Career 备份恢复协议贯通到本地 UI。恢复必须携带界面检查时得到的
  唯一备份 token 和损坏主文件指纹，并在仓储锁内重新验证；成功后建立新 lineage，而不是静默覆盖。
- 增加非权威的最近生涯指针。指针只保存两个本地 ID，损坏或失效时自动清除并返回档案列表，不能替代
  权威 Profile/Career JSON。
- Editor/Development Build 增加重载权威存档、模拟下一次权威写入失败和导出脱敏诊断。诊断不包含
  档案／存档／球员 ID、姓名、生涯名称、完整存档 JSON 或绝对路径。
- 增加固定的 Windows x64 IL2CPP Development Build 入口，只构建 Career 垂直切片场景，并在成功后
  原子发布脱敏 manifest。失败前先撤销旧 manifest；构建结束后恢复原 Editor PlayerSettings。
- 不新增玩法，不实现登录、云存档、合同、转会、伤病或真实 6v6 接入。

## 具体变更

- `CareerPersistenceResult` 在 `RecoveryAvailable` 时同时返回精确的损坏主文件指纹；
  `CareerLocalUiWorkflow` 与仓储复用既有加锁恢复协议。
- `CareerRecentSessionStore` 管理 `<persistentDataPath>/CareerUi/recent-session.v1`，仅作为启动导航提示。
- `CareerDiagnosticExporter` 在 `<persistentDataPath>/Diagnostics/` 写 UTF-8 JSON；开发 UI 不再显示原始
  profile/save ID。
- 故障注入只在前置校验通过、即将调用权威写服务时消费；普通载入、诊断导出、最近生涯读取和
  `SaveNow` 的只读校验不会消费。
- `CareerWindowsDevelopmentBuild.Build` 固定 Career 单场景、`StandaloneWindows64`、IL2CPP、x86_64、
  `Development | AllowDebugging`，输出 `Builds/Windows/VolleyballCareer.exe` 和
  `Builds/Windows/build-manifest.json`。
- repository policy 同步 Bootstrap Editor 与 Career 测试程序集的实际引用。

## 跨模块交互重点

- 接口提供方：Career Application/Persistence 提供恢复、权威重载和快照；Bootstrap 提供本地文件与
  Windows 构建适配。
- 接口使用方：Career Presentation 只使用 `ICareerUiUseCases`；Windows Player 只包含 Career 入口场景。
- 数据方向：`Career UI -> Bootstrap adapter -> Career workflow/repository -> authoritative snapshot -> UI`。
- 兼容性：兼容。没有修改存档 schema、Match 树、legacy Shared V1 或 Shared Match V2 fixture。
- 消费方或后续负责人需要做什么：Match 无需改代码。后续真实 runner 必须继续遵守 PendingMatch、
  resultHash 和 SettlementReceipt 边界，不能绕过本次恢复协议。

## 验证

- [x] 恢复／最近生涯聚焦 EditMode：3/3。
- [x] 脱敏诊断聚焦 EditMode：1/1。
- [x] Windows build contract 聚焦 EditMode：1/1。
- [x] 阶段末完整 EditMode 首次运行：1085/1086；唯一失败是边界测试仍期望旧的测试程序集引用列表。
  更新期望后失败用例单独 1/1 通过；按精简测试策略未重复运行其余已经通过的 1085 条。
- [x] Career UI PlayMode：1/1，0.97 秒。没有重跑耗时的完整 Match PlayMode；最近一次冻结基线仍为
  12/12，Match 源码和场景树未改变。
- [x] `python -B tools/validate_repository.py`：通过。
- [x] Windows x64 IL2CPP Development Build：Unity `6000.3.20f1`，构建成功；manifest 报告
  `1,538,767,094` 字节，EXE PE machine 为 `0x8664`（x64）。
- [x] Windows 构建鼠标功能闭环与退出重开恢复：已完成“新建职业生涯 → 三段试训 → 周计划 →
  临时事件 → 快速比赛 → 原子结算 → 第二周”，重启后从最近生涯指针恢复到第二周权威快照。
- [x] 2026-08-03 Windows x64 IL2CPP Development Build 复验：Unity `6000.3.20f1`、
  Windows IL2CPP Build Support、Visual Studio 2022 Build Tools 与 Windows SDK `10.0.19041.0`
  已安装；`CareerWindowsDevelopmentBuild.Build` 成功生成
  `Builds/Windows/VolleyballCareer.exe` 与 manifest。manifest 记录
  `StandaloneWindows64`、`x86_64`、`IL2CPP`、Development、Allow Debugging，产物总大小
  `1,444,637,468` bytes。
- [x] 2026-08-03 编译边界修复：`Career.Persistence` 曾直接调用已依赖其的
  `Career.MatchIntegration` 重建器，导致程序集循环方向无法编译。收据读取保留 canonical
  trace、result 与 report 的绑定校验，移除了重复的跨层重建调用；Windows Player 编译与构建
  已证明修复生效。
- [ ] Windows 构建纯键盘完整闭环：自动化工具注入的 Return/Tab 没有进入 Unity Player 的键盘事件链，
  不能据此判定实体键盘结果；现有动作图与聚焦 PlayMode 仍为 1/1，需人工使用物理键盘确认。
- [ ] 实体 XInput 手柄完整闭环：尚未执行，不能用 Input System 自动化绑定测试替代。1920×1080
  鼠标闭环未观察到明显交互停顿。

本轮独立 agent 对恢复、诊断和构建三个切片进行集中静态复核；全部 P1/P2 已修复并批准。

## 回滚与风险

回滚时应同时移除确认恢复 UI、最近生涯指针、诊断适配器、Windows Build Editor 入口和相应 asmdef／
repository policy 引用，不能只隐藏按钮。Profile 主文件自身的玩家确认恢复协议仍延期；Profile 双损坏
当前保持只读拒绝。构建产物位于被 Git 忽略的 `Builds/Windows/`，不会进入仓库。

当前阶段尚不能标记为完全完成：只有在实体 Windows 构建中完成纯键鼠和 XInput 两条闭环后，才可把
本记录与路线图阶段 9 更新为“已完成”。
