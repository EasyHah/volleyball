# CHG-20260725-001：Full Rally V4 Gate F 十二人责任计划 Shadow

- 日期：2026-07-26
- 状态：已完成
- 负责人：Shared / Match / Replay / Docs
- 影响模块：Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gate-f-shadow-plans`
- 关联提交或 PR：`9bb2a3d` 至 `91f0f8f`（Gate F Task 1-5）

> [!IMPORTANT]
> Shared 的 Replay V4 合同现在可选地携带 canonical shadow 记录；Match 在每个已接受的正式触球后生成只读计划，Replay recorder 按 source sequence 消费并持久化它。合并前，Shared/Match/Replay 开发者必须保持 V4 shadow JSON 字段顺序、完整 Home/Away 十二人覆盖和 artifact identity 的一致性；shadow 不得成为 player agent、调度器、计分或 V3 规则的输入。

## 改动摘要

Gate F 将确定性、无命令的十二人责任计划作为诊断 shadow 写入原生 Replay V4。每个已捕获的正式触球保存共享 trajectory artifact identity 下的 Home/Away 各六人计划、覆盖决策和来源序列；Replay V4 hash 因而覆盖此证据。

本改动不新增 HTML overlay，不修改战术权威，不移动玩家，不改变球、规则转换或计分。旧版没有 `shadow` 的 V4 记录仍可读取；F4-era 的完整 legacy coverage 仍用历史 canonical hash 验证，混合 legacy/current coverage 会被拒绝。

## 具体变更

- `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/`：增加不可变的 assignment、team plan、rally plan 和纯 deterministic composer；它只读取 world snapshot/eligibility/facts，且没有 Unity、player agent、director、scheduler 或 contact API 引用。
- `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs` 与 `ContractJson.cs`：增加严格验证的 shadow replay value、固定 canonical JSON 顺序、source/artifact provenance 和 legacy coverage 兼容读取。
- `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs`：在 accepted contact 后以一个共享 artifact 产生两队 shadow，安全地发布 `ReplayShadowPlanRecorded`；观察者失败只记录诊断，不能中止 live contact。
- `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`：按 source sequence 暂存/消费 shadow；缺失、重复、不可转换、未匹配或零触球 capture 均只使 capture 无效，不干预 rally resolution。
- `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs` 与 `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`：覆盖 canonical round trip/hash、legacy coverage、provenance、capture integrity、live-rally invariance 和重复 fixed-seed capture 字节稳定性。

## 跨模块交互重点

- 接口提供方：Shared（Replay V4 value/JSON）与 Match（只读 shadow 事件）
- 接口使用方：Match Replay recorder 与 V4 replay reader
- 数据方向：`Match formal rule/world facts -> Match shadow event -> Shared Replay V4 -> replay diagnostics`
- 兼容性：兼容旧 V4 无 shadow 记录；完整 legacy coverage 使用历史 hash 验证；不得混合 legacy/current coverage。
- 对方开发者需要做什么：新增 V4 读取器必须保留 canonical order 和 strict validation；不得将 shadow 用作 live command 或 authority 输入。

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [ ] 手动场景验证
- [x] 序列化、存档或迁移验证（Replay V4 canonical JSON/hash；无存档迁移）

Unity `6000.0.43f1`，2026-07-26 的结果如下（`TestResults/` 已忽略，均为本地证据）：

- 定向 EditMode：`TestResults/GateF-focused-editmode.xml`，`74/74` passed，`0` failed/skipped/inconclusive，`4.3970005s`。包含 `MatchReplayV4Tests`、composer、V3 adapter 与 world snapshot 回归。
- 定向 PlayMode：`TestResults/GateF-formal-replay-playmode.xml`，`10/10` passed，`0` failed/skipped/inconclusive，`25.9999682s`。包含正式回放、live-rally 不变性和 capture integrity。
- 完整 EditMode：`TestResults/GateF-final-editmode.xml`，`551/551` passed，`0` failed/skipped/inconclusive，`5.6274638s`。
- 完整 PlayMode：`TestResults/GateF-final-playmode.xml`，`30/30` passed，`0` failed/skipped/inconclusive，`540.5691226s`。
- 字节/hash 证据：`Capture_TwoIndependentFixedSeedFormalRunsAreByteStable` 在同一 fixed seed 的两次独立 capture 上以 `CollectionAssert.AreEqual` 验证 UTF-8 canonical V4 JSON 字节完全一致，并验证每个 event 的 artifact/envelope identity 相等；`Capture_FirstFormalRallyProducesStrictNativeV4Replay` 验证 deserialize 后 canonical JSON 不变且 `ReplayHash` 与 capture hash 相等。

最终 `git diff --check` 无输出；final review 复查了 shadow 域的无 authority 依赖、Home/Away 共同 artifact identity、旧 V4 无 shadow 读取，以及 recorder invalidation 不影响 live resolution。未执行人工场景浏览；没有新增 HTML overlay 或 tactical authority。

## 回滚与风险

主要风险是后续 V4 JSON reader 改变 shadow canonical 字段顺序，或将诊断 shadow 错接到 live authority。以 strict parser/hash、source sequence、artifact provenance 和正式 fixed-seed invariance 测试监测。若需回滚，请整体回滚 Gate F 提交序列 `9bb2a3d..91f0f8f`，不要在保留 V4 shadow writer 的情况下只回滚其 reader 或 recorder。
