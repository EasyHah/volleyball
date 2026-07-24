# CHG-20260724-002：Full Rally V4 Gates A–E contract cut

- 日期：2026-07-24
- 状态：进行中
- 负责人：多方
- 影响模块：Career / Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gates-a-e`
- 关联提交或 PR：尚未创建

> [!IMPORTANT]
> Shared 提供原生 V4 合同与 canonical identity；Career 只创建/消费具体 V4 context/result；Match 和 replay 只接受 V4 输入。合并前，Career、Shared、Match 与 replay 的开发者必须确认没有宽泛 `IMatchContext`/`IMatchResult` 或 V1/V2/V3 合同继续作为生产入口。

## 改动摘要

以 [Gates A–E implementation plan](../superpowers/plans/2026-07-24-full-rally-v4-gates-a-e-implementation-plan.md) 和 [consolidated design](../superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md) 为准，冻结从 V3 到原生 V4 的硬切边界。Gate A 先加入可执行的红线测试；实现随后分 gate 推进，不在此 gate 增加兼容层。

## 具体变更

- Career：gateway/request 改为具体 `MatchContextV4`/`MatchResultV4`，移除旧能力和 context 的生产入口。
- Shared：提供 V4 player/team/context/result/replay 合同及独立 canonical hash。
- Match：正式 6v6 只从 V4 context 初始化；execution envelope、trajectory cache 与 P6 实际几何进入权威路径。
- Replay：只记录和读取 V4 identity、样本分类、完整预测 key 与实际攻击几何。
- 行为变化：R-REF-002 要求攻击资格基于实际物理几何，非法数据必须拒绝而非修正。

## 跨模块交互重点

- 接口提供方：Shared
- 接口使用方：Career / Match / Replay / Bootstrap
- 数据方向：`Career -> Shared -> Match -> Shared -> Career`，并由 Match 写入 Replay。
- 兼容性：不兼容；Gate E 后不保留 V1/V2/V3 到 V4 的 loader、adapter、默认值或宽泛入口。
- 对方开发者需要做什么：同步使用具体 V4 类型，删除旧生产构造器/方法并运行 Gate A 的边界反射测试。

## 验证

- [x] EditMode 测试（Gate A 预期红线）
- [ ] PlayMode 测试
- [ ] 手动场景验证
- [ ] 序列化、存档或迁移验证（Gate E 完成后；不迁移旧存档）

Gate A 使用 Unity `6000.0.43f1` 批处理 EditMode 命令记录当前缺口；红线测试在相应 gate 实现前预期失败。

## 回滚与风险

风险是硬切期间跨模块入口短暂不一致。以小 gate 提交和 deterministic boundary tests 观察；若需回滚，回滚整个 V4 gate 提交，不恢复任何新的 V1/V2/V3 兼容路径。
