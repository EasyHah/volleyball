# CHG-20260724-001：Stage 2 能力投影、共享执行 envelope 与物理预测 cache

- 日期：2026-07-24
- 状态：已落地核心域类型与 EditMode 测试；AttackControl 接线、PlayMode shadow 与回放完整化待后续子阶段
- 负责人：Match
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联路线图：`docs/superpowers/plans/2026-07-24-full-rally-v3-phase-2-9-roadmap.md` Stage 2

## 改动摘要

Stage 2 将 Phase 0 的 identity-only 接口扩展为正式域合同，并新增确定性物理预测 cache。
`AbilityProjectionV3` 记录每个 V3 能力轴的 `Active` / `CompatibilityMapped` / `Reserved` 状态
与所属动作类别。`ExecutionEnvelopeV3` 从 identity-only 升级为携带 baseline target/velocity、
bounded error 分布、effort、samples 与 provenance 的不可变合同，并实现
`ClassifySample` 返回 `WithinEnvelope` / `EnvelopeExceeded` / `EnvelopeExpanded` /
`UnexpectedExecutionSample`。`BallTrajectoryPredictionProviderV3` 以 ball-state version hash +
physics hash + sample key 为键缓存 `TrajectoryPredictor` 结果，两队用相同输入得到同一
artifact 实例。`AttackGeometryFactV3` 携带实际起跳点、触球点、进攻线距与网高，提供
`IsContactAboveNet` / `IsTakeoffInFrontZone` 事实。

## 具体变更

- 文件、场景、资源或程序集：
  - `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/AbilityProjectionV3.cs`
  - `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/ExecutionEnvelopeV3.cs`（扩展）
  - `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BallTrajectoryArtifactV3.cs`（扩展）
  - `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/BallTrajectoryPredictionProviderV3.cs`
  - `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/AttackGeometryFactV3.cs`
  - `Assets/Volleyball/Match/Tests/EditMode/Stage2AbilityEnvelopeTests.cs`
- 新增或修改的公开类型/字段：
  - `AbilityAxisStatus { Active, CompatibilityMapped, Reserved }`
  - `AbilityAxisName`（11 个轴）
  - `AbilityAxisProjectionV3`：单轴投影，带 status、value、sourceName、actionCategory
  - `AbilityProjectionV3`：完整投影，`FromSnapshot(PlayerAbilitySnapshotV3)` 和
    `FromV2Snapshot(PlayerAbilitySnapshotV2)` 工厂方法
  - `ExecutionSampleClassification { WithinEnvelope, EnvelopeExceeded, EnvelopeExpanded, UnexpectedExecutionSample }`
  - `EnvelopeBoundsV3`：偏差/速度/effort 边界
  - `ExecutionSampleV3`：执行样本
  - `ExecutionEnvelopeV3` 扩展构造函数：baselineTarget/Velocity、bounds、effort、samples、
    provenance、lastSampleClassification；`ClassifySample(sample)` 方法
  - `BallTrajectoryPredictionProviderV3`：确定性 cache + `Predict(source, params, sampleKey, degradationMode)`
  - `BallTrajectoryArtifactV3.CreateWithPrediction(...)` 工厂方法
  - `AttackGeometryFactV3`：实际起跳点/触球点/进攻线距/网高与 above-net/front-zone 事实
- 回放诊断：
  - Stage 1.5 的 `MatchReplayDecisionDiagnosticsV1` 已在 V2 回放中持久化 consumed abilities
    和 organization 诊断；Stage 2 的 envelope/trajectory/ability-consumption 待 Stage 5-6
    在 V3 规则权威接线后以 additive V2 sections 补齐。

## 验收

- EditMode（静态分析）：
  - `FromSnapshot` 将 Mobility、Reaction、Jump、MaxAttackReach、ReceiveTechnique、
    SetTechnique、AttackControl、AttackPower 标记为 `Active`，SoftTouch、BlockTechnique、
    CourtAwareness 标记为 `Reserved`。
  - `FromV2Snapshot` 将 AttackControl 标记为 `CompatibilityMapped`（值来自 `AttackTechnique`）。
  - 同输入 envelope 产生相同 identity 与 hash；不同 `AttackControl` 产生不同 envelope。
  - `ClassifySample` 对 within-bounds 样本返回 `WithinEnvelope`，超界返回 `EnvelopeExceeded`，
    错 key 返回 `UnexpectedExecutionSample`。
  - 同 ball-state + physics + sample-key 两次调用 `Predict` 返回同一 cache 实例；不同 key 产生不同 artifact。
  - 两队用相同输入得到同一 trajectory artifact 实例。
  - `AttackGeometryFactV3` 正确检测 above-net / front-zone。
  - Phase 0 identity-only 构造函数和 `BallTrajectoryArtifactV3.CreateIdentity` 保持向后兼容。

- PlayMode：尚未覆盖（Stage 2 的 PlayMode shadow 验证待 V3 规则权威接线后进行）。
- 回放：additive V2 sections 待 Stage 5-6 接线。

## 后续

- 在 Stage 5（receive/organization 权威切片）将 `AbilityProjectionV3.AttackTechniqueValue()`
  接入正式攻击执行路径，替代 `PlayerAbilityProfile.TechniqueFor(Attack)`。
- 在 Stage 6（攻防权威切片）使用 `AttackGeometryFactV3` 通过 V3 规则权威执行 above-net/front-zone 限制。
- 在 Stage 2 完整闸门中，将 `ExecutionEnvelopeV3` 与 `BallTrajectoryPredictionProviderV3`
  接入 planner/executor，并添加 PlayMode shadow 与回放 additive sections。
