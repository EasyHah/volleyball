# Career x Match V4 流程联调

- Status: active
- Owner: Match collaborator
- Priority: P0
- Scope: Career / Match / Shared / Bootstrap 的正式物理 6v6 生命周期联调
- Supersedes: 无；V4 属性映射保持当前临时基线，V5 属性重构另行设计。

## 当前要求

优先打通 Career 到真实 `FormalIndoor6v6` 再回到 Career 的完整 V4 生命周期：已持久化的
`PendingMatch` 创建 V4 context，Bootstrap 启动比赛，Match 原样消费 context 与 Derived，随后返回
V4 result/replay，最后由 Career 校验、幂等结算并支持恢复。

Career 八项属性到 V4 的当前映射在本任务中仅作为临时基线。它必须可追溯且单调：任一 Career 属性
提高不得使其映射到的 V4 输入降低；位置模板不得覆盖 context 已提供的能力。疲劳仅由 Career 在创建
context 时折算一次，Match 不读取或修改 Career 原始疲劳。

## 非目标

- 不重做 Career 属性体系、职业特色属性或副职业机制。
- 不修改 `MatchContextV4` 后继续称其为 V4，也不为属性重构提前实现 V5。
- 不以 fixture 的统计估算作为正式技术统计或成长结算依据。
- 不让 Match 按位置模板覆盖、重建或猜测 Career 传入的能力和身份。
- 不在本任务加入多局制、换人、自由人替换或副职业玩法。

## 交付物

- 从 `PendingMatch` 到物理 6v6 的公开 runner/Bootstrap 生命周期接线。
- Match 对冻结 `MatchContextV4`、Derived、12 人身份和 `matchSeed` 的实际消费证据。
- V4 result/replay 回传、取消/场景失败保留 PendingMatch、重复回调仅结算一次的验证。
- Career 正式成长所需的逐球员详细报告仅记录为 V5 后续设计输入，不阻塞 V4 生命周期联调。

## 验收条件

- Match 不重建、重排或覆盖 context 的 12 人身份与能力；Derived 实际进入 AI 和执行层。
- 冻结 `matchSeed` 驱动所有随机 AI 路径；纯确定性路径不保留无效 seed 参数。
- result/replay 与 context 身份一致，Career 能以 `SessionId + ContextHash + ResultHash` 完成幂等结算。
- 取消、场景加载失败或崩溃只回到赛前并复用原 context；不伪造中止结果。
- V4 完成后，另建 V5 任务单再讨论“全职业基础属性 + 职业特色属性”与可能的副职业。

## 当前检查点

- macOS 的 complete EditMode、Career formal runner PlayMode 与真实物理整局已通过。
- Windows 构建机已完成 Unity `6000.3.20f1` x64 IL2CPP Development Build：
  `Builds/Windows/VolleyballCareer.exe`，manifest 总大小 `1,527,420,656` bytes。
- 最终 Player 无图形启动烟雾已通过，8 秒内无 Bootstrap、场景、输入或崩溃异常。
- 仍需完成 Windows 可视化设备闭环（键盘、手柄、图形与完整物理比赛性能），之后才能把本任务单
  移入 completed 并创建 V5 后继任务。

## 依赖资料

- `AGENTS.md`
- `docs/career-development-roadmap.md` 第 10 阶段
- `docs/career-match-v4-integration-contract.md`
- `docs/career-domain-rules.md`
- `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`
