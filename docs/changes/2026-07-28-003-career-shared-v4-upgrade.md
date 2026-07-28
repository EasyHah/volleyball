# CHG-20260728-003：Career 合入 Full Rally V4

- 日期：2026-07-28
- 状态：已自动验证，待人工验收
- 影响模块：Shared / Match / Career / Bootstrap / Packages / ProjectSettings / Tests / Docs
- 交互级别：跨模块（重点）

## 决策

- 合入 `origin/main@173f957`，继续使用 Unity `6000.3.20f1`。
- Shared/Match 的原生 V4 是唯一比赛契约权威。Career Domain 继续拥有八项整数成长
  属性，只在 `Career.MatchIntegration` 映射为 V4 的 physical/technical 基础输入。
- `CareerMatchExecutorV4` 与 `DeterministicFixtureMatchRunnerV4` 只读写 Shared 的
  规范 V4 JSON，不恢复 V1–V3 reader、adapter 或 fallback constructor。
- 存档 Schema 保持 V2；新比赛载荷必须是 V4。没有比赛载荷的旧开发档仍可读取，
  含 V1–V3 比赛载荷的档案按既有候选隔离和备份恢复流程处理。
- 离线 fixture 冻结自己的 physics/predictor 配置哈希。未来接真实物理比赛时，Match
  runner 必须提供实际配置哈希，不能沿用 fixture 标识。
- 保留 Career UI 所需的 Input System `1.17.0`，同时采用上游 Unity 6000.3 包基线。
- Career 与 Match 的传入、传出、取消恢复、规范序列化及后续详细报告边界统一记录在
  [`career-match-v4-integration-contract.md`](../career-match-v4-integration-contract.md)，
  供双方 Codex 在合并前读取。

## 兼容、回滚与所有权

- 兼容性：比赛载荷为有意硬切；Career 成长数据和无比赛载荷的 Schema V2 档案不变。
- 回滚：回滚本合并提交即可恢复升级前分支；不得把已生成的 V4 载荷降写为 V3。
- 所有权：Match/Shared V4 内容来自搭档上游；Career 映射、结算、存档恢复和 UI 由
  Career 侧维护；Bootstrap/Packages/ProjectSettings 属共同区域，合并仍需 PR。

## 验收

- [x] V4 映射、canonical round-trip、fixture 与模块边界 focused EditMode：`9/9`。
- [x] 生命周期与 Schema V2/V4 focused EditMode：`101/101`。
- [x] Career 完整 EditMode：`695/695`。
- [x] 合并后的完整 EditMode：`1436/1436`；完整 PlayMode：`40/40`。
- [x] Windows x64 IL2CPP Development 构建成功，manifest 记录 Unity
  `6000.3.20f1`、x86_64、IL2CPP、Development + AllowDebugging。
- [ ] Career 人工闭环：创建档案、完成试训、安排两次行动、处理事件、开始 fixture
  比赛、结算并重启恢复。
