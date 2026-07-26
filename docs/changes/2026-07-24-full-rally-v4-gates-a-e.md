# CHG-20260724-002：Full Rally V4 Gates A–E contract cut

- 日期：2026-07-24
- 状态：已完成
- 负责人：多方
- 影响模块：Career / Shared / Match / Replay / Docs
- 交互级别：跨模块（重点）
- 关联分支：`codex/full-rally-v4-gates-a-e`
- 关联提交或 PR：`codex/full-rally-v4-gates-a-e` Task 1–12 提交序列

> [!IMPORTANT]
> Shared 提供原生 V4 合同与 canonical identity；Career 只创建/消费具体 V4 context/result；Match 和 replay 只接受 V4 输入。合并前，Career、Shared、Match 与 replay 的开发者必须确认没有宽泛 `IMatchContext`/`IMatchResult` 或 V1/V2/V3 合同继续作为生产入口。

## 改动摘要

以 [Gates A–E implementation plan](../superpowers/plans/2026-07-24-full-rally-v4-gates-a-e-implementation-plan.md) 和 [consolidated design](../superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md) 为准，已完成原生 V4 硬切。V1/V2/V3 属性、context、result、replay 生产文件与 serializer 已删除；3v3 prototype 也使用 V4 六人 roster 和显式三名 active player，不保留 prototype 兼容例外。

## 具体变更

- Career：gateway/request 改为具体 `MatchContextV4`/`MatchResultV4`，移除旧能力和 context 的生产入口。
- Shared：提供 V4 player/team/context/result/replay 合同及独立 canonical hash。
- Shared：`RulesVersions.FullRallyV3` 与 `ContractVersions.MatchV4` /
  `ContractVersions.ReplayV4` 分离；通用 SHA/canonical string helper 由 V4
  源码拥有，不再依赖已删除的 V3 context 文件。
- Shared / Match（Task 8）：`MatchContextV4` 现要求不可变的 trajectory
  provider 配置（容量、FIFO eviction、predictor version/config hash），并将其写入
  strict canonical JSON/hash。正式 director 每个 rally 从该配置建立一个共享 provider；
  完整 key 覆盖 ball version/fingerprint、physics、sampling、predictor、envelope 与
  degradation step，请求方队伍不进入 key。
- Match：正式 6v6 只从 V4 context 初始化；execution envelope、trajectory cache 与 P6 实际几何进入权威路径。
- Match：删除已被 V4 envelope/prediction 取代且无生产消费者的
  `ExecutionEnvelopeV3`、`BallTrajectoryArtifactV3`、
  `BallTrajectoryPredictionProviderV3` 和 `DeterministicWorkBudgetV3`；
  V3 命名只保留规则 authority 及其输入事实。
- Replay：只记录和读取 V4 identity、样本分类、完整预测 key 与实际攻击几何。
- 行为变化：R-REF-002 要求攻击资格基于实际物理几何，非法数据必须拒绝而非修正。

## 跨模块交互重点

- 接口提供方：Shared
- 接口使用方：Career / Match / Replay / Bootstrap
- 数据方向：`Career -> Shared -> Match -> Shared -> Career`，并由 Match 写入 Replay。
- 兼容性：不兼容；不保留 V1/V2/V3 到 V4 的 loader、adapter、默认值或宽泛入口。
- 对方开发者需要做什么：只使用具体 V4 类型；规则层使用独立
  `RulesVersions.FullRallyV3`，不得以旧 match contract 常量代替。

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [ ] 手动场景验证
- [x] V4 序列化验证（不迁移旧存档）

Task 12 的完整 EditMode 使用 Unity `6000.0.43f1` 和无 `-quit` batch 命令：

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults /tmp/volleyball-v4-all-editmode.xml \
  -logFile /tmp/volleyball-v4-all-editmode.log
```

外部审查清理三条仅覆盖已删除 V3 Stage2 合同的测试，并新增 one-shot active-roster
枚举回归后，最新结果为 `505/505` 通过、`0` failed、`0` skipped、
`0` inconclusive，高于迁移前 `491` 测试基线。完整 PlayMode 使用同样命令并把
platform/result/log 改为
`PlayMode`、`/tmp/volleyball-v4-all-playmode.xml` 和
`/tmp/volleyball-v4-all-playmode.log`，结果为 `24/24` 通过、`0` failed、
`0` skipped、`0` inconclusive；external-review 修正后的 fresh run 耗时
`524.894380s`。

legacy production `rg` 无结果；`git diff --check` 无错误。V4 predictor 只接收不含
requester provenance 的 key-covered input；artifact 在 canonical identity 冻结前
复制 predictor samples，director 只消费 artifact-owned snapshot。

## 回滚与风险

风险是硬切期间跨模块入口短暂不一致。以小 gate 提交和 deterministic boundary tests 观察；若需回滚，回滚整个 V4 gate 提交，不恢复任何新的 V1/V2/V3 兼容路径。
