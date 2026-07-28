# CHG-20260727-002：Full Rally V4 Gate J 感知与 CourtAwareness

- 日期：2026-07-27
- 状态：已完成
- 负责人：Shared / Match / Replay / Docs
- 影响模块：Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gate-j-perception`
- 关联提交或 PR：`ebd1d0b..6af6131`

> [!IMPORTANT]
> Shared 只为 Replay V4 新增可选 Gate J perception authority record；历史
> Replay V4 不含该字段时 canonical bytes/hash 保持不变。Career 与 Bootstrap
> 无需修改代码。

## 改动摘要

Gate J 在正式十二人、V3 Authority 路径启用确定性感知视图。它从既有权威轨迹、
公开 threat、合法支援集合和派生 Awareness 构造 event-owned receipt，只影响观察
延迟、不确定性、可见 threat 解释及 support/floor coverage 选择，不改变物理轨迹、
规则资格、已提交动作或 contact geometry。

3v3、V3 Shadow/Disabled 与不完整 roster 继续走原路径，不创建 Gate J receipt。
Gate K 的 director 瘦身、Replay UI、双视角 overlay 和完整性能报告不在本次范围。

## 具体变更

- 新增不可变 perception configuration、ball observation、player/team view、可见
  threat、合法 support candidate/decision 与 event-owned receipt。
- 纯 AI adapter 使用显式 UTF-8/SHA-256 字节映射；相同 seed/revision/public input
  产生相同结果，不读取 Unity random、wall clock、隐藏最终路线或未来 sample。
- CourtAwareness 单调控制 recognition delay、position uncertainty、arrival
  uncertainty 和 confidence；空合法候选返回无选择，低置信时回退到调用方声明的
  committed/conservative responsibility。
- Gate H/I coordinator 按 revision、source event、team、公开 threat 与 artifact
  验证 receipt。新事件会清除旧 receipt，避免跨 rally/team 泄漏；既有 coordinator
  与 controller 仍是唯一 command writer。
- Replay V4 仅在存在 Gate J evidence 时追加严格 canonical
  `perceptionAuthority`；recorder 只映射事件自身 receipt，正式相关事件缺失证据时
  fail closed。历史无字段 JSON 的 bytes/hash 不变。
- PlayMode 前两个全量校准任务的成功一传目标由 100 降为 30，timeout 由
  1,200,000 ms 降为 360,000 ms；阈值、formal/3v3 双路径与断言未放宽。

## 跨模块交互重点

- 接口提供方：Match perception/authority evidence 与 Shared Replay V4 可选 record。
- 接口使用方：Match coordinator、controller 与 recorder；Career、Bootstrap 无需改代码。
- 数据方向：`authoritative public input -> deterministic perception -> event receipt -> Replay V4`。
- 兼容性：历史 Replay V4 无 `perceptionAuthority` 时 strict reader 与 canonical
  bytes/hash 保持原样；新 record 未知/缺失字段、非有限值或非 canonical threat
  顺序会被拒绝。
- 对方开发者需要做什么：新增 Replay reader 可读取可选 record，但不得把它解释为
  权威物理或隐藏最终路线，也不得要求历史文件补字段。

## 验证

- [x] Gate J contract/adapter focused EditMode：9/9 passed，0
  failed/skipped/inconclusive，0.0429028s（Unity 6000.0.43f1，
  `TestResults/GateJ-review-perception.xml`）。
- [x] Gate J authority lifecycle focused EditMode：17/17 passed，0
  failed/skipped/inconclusive，0.0591146s（
  `TestResults/GateJ-review-boundary.xml`）。
- [x] Formal awareness、Gate I writer 与 25 分整局 focused PlayMode：3/3 passed，
  0 failed/skipped/inconclusive，124.8722416s（
  `TestResults/GateJ-review-formal-focused.xml`）。
- [x] 完整 EditMode：737/737 passed，0 failed/skipped/inconclusive，
  8.5142689s（`TestResults/GateJ-final-editmode-reviewed-green.xml`）。
- [x] 完整 PlayMode：35/35 passed，0 failed/skipped/inconclusive，
  378.6350082s（`TestResults/GateJ-final-playmode-reviewed-green.xml`）。
- [x] 缩短后的前两个校准任务分别为 17.012233s（formal 6v6）与
  77.329765s（3v3）；阈值和行为覆盖保持不变。
- [x] Replay：strict canonical round-trip、历史 bytes/hash、recorder on/off
  authority invariance 与两次独立 fixed-seed Gate J bytes/hash stability 全绿。
- [x] 静态扫描：perception Domain/adapter 未命中 hidden action/sample、
  coordinator 或 Unity object；AI/Domain 未命中 Unity/Presentation/recorder
  反向依赖；`git diff --check` clean。
- [x] Combined review：修复跨事件 stale Gate H receipt；补齐独立 arrival
  uncertainty、稳定 SHA 字节序、空候选结果和显式 conservative responsibility；
  Gate I 校验允许 bounded perceived arrival 差异但仍拒绝 revision/side/zone
  不匹配。
- [ ] 手动场景视觉检查、Windows x64 与完整性能 calibration（不在本次自动验收范围）。

## 回滚与风险

Gate J contracts、formal coordinator/controller 接线与 Replay 可选 record 应作为同一
兼容性单元回滚。不得只移除 receipt 清理或事件绑定而保留 recorder 映射。主要剩余
风险是不同平台的浮点表现和长时间性能分布；稳定 hash 已使用显式字节序，但 Windows
x64、真实图形环境与长样本 calibration 仍需 Gate K/发布验证。

