# Full Rally V4 Gate K Completion Implementation Plan

**状态：已完成（2026-07-28）**

> 实施遵循 `proportionate-validation`：每个 slice 先 focused red/green；由于涉及
> lifecycle、canonical Replay 和四个模块，最终运行完整 EditMode/PlayMode 与一次
> combined independent review。共享 director 文件的工作保持串行。

## Task 1：冻结 Gate K 边界与基线

**Create**

- `docs/superpowers/specs/2026-07-28-full-rally-v4-gate-k-completion-design.md`
- `docs/superpowers/plans/2026-07-28-full-rally-v4-gate-k-completion-implementation-plan.md`
- `Assets/Volleyball/Match/Tests/EditMode/GateKDirectorBoundaryTests.cs`

**Steps**

1. 记录 Gate J HEAD 的完整 EditMode 737/737 与 PlayMode 35/35 基线。
2. 写 reflection red tests：formal authority facade 不得暴露 planner/candidate/route
   selection；director 不得直接持有 `TeamRallyDecisionPlanner`。
3. 写 static scan 清单：formal Authority 分支中 `GateHLegacyWriterInvocations` 与
   `GateILegacyWriterInvocations` 必须为零；AI/Domain 不得反向依赖 Presentation。
4. 提交设计和计划。

## Task 2：抽出 legacy decision 与 formal authority orchestration

**Create**

- `Assets/Volleyball/Match/Runtime/AI/RallyDecisionCoordinatorV3.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/FormalRallyAuthorityOrchestrator.cs`
- 对应 `.meta`

**Modify**

- `PhysicalMatchRallyDirector.cs`
- Gate H/I coordinator/controller focused tests
- `GateKDirectorBoundaryTests.cs`

**Steps**

1. 红测锁定 snapshot -> decision、无可行 candidate、setter-first-contact 与 stale
   request 的当前结果。
2. `RallyDecisionCoordinatorV3` 持有 planner、candidate ordering 和 legacy stage
   selection；director 只传只读 player/ball/touch snapshot。
3. `FormalRallyAuthorityOrchestrator` 持有 Gate H/I revision、receipt/perception
   lifecycle 与 command batch publication；director 只应用 command。
4. 按 Receive/Organize、SetIntent、Attack/Defense、tool recovery 顺序切换，并在每个
   slice 删除旧 writer/state。
5. focused EditMode 与 formal scenario PlayMode 全绿后提交。

## Task 3：新增 deterministic work-budget Replay evidence

**Create/Modify**

- `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`
- `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/DeterministicWorkBudgetV4.cs`
- `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`
- `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`

**Steps**

1. 红测：optional strict/canonical round-trip、unknown/missing/non-finite/negative
   rejection、历史 bytes/hash 不变。
2. 定义 event-owned optional `ReplayWorkBudgetRecordV4`，只含设计列出的确定性字段。
3. 从 envelope policy、candidate/sample/expansion 与 trajectory degradation 映射；
   禁止 wall-clock/cache-hit/allocation 字段。
4. 增加 inclusion/exclusion 测试：behavior config/work evidence 改变 hash，profiler
   observation 不进入 record/hash。
5. focused Replay EditMode 全绿后提交。

## Task 4：完成双方视角 Replay HTML

**Modify**

- `MatchReplayHtmlWriter.cs`
- `MatchReplayV4Tests.cs`
- `FormalSixVsSixReplayPlayModeTests.cs`

**Steps**

1. 红测要求 `AUTHORITATIVE / ACTUAL`、`HOME PERCEIVED`、`AWAY PERCEIVED`、
   Gate H/I/J evidence、work budget 和缺失视图标记。
2. 暴露纯 `Render(MatchReplayV4)`；`Write` 只负责 UTF-8 no-BOM 文件输出。
3. 仅从 event-owned perception/authority records 构造 perspective panel；不得显示
   final route/future sample/internal coordinator。
4. 两次相同 replay 的 HTML bytes 相等；两次独立 fixed-seed capture 的 replay hash
   与 HTML 相等。
5. focused EditMode/PlayMode 全绿后提交。

## Task 5：固定 seed 四轴 matrix 与性能报告

**Create**

- `Assets/Volleyball/Match/Tests/PlayMode/GateKCalibrationMatrixPlayModeTests.cs`
- `docs/performance/full-rally-v4-gate-k-calibration.md`

**Modify**

- 既有 ability/envelope/perception focused fixtures
- `AttackChainCalibrationPlayModeTests.cs`（只复用，不降低阈值）

**Steps**

1. 为 AttackControl、SoftTouch、BlockTechnique、CourtAwareness 建立低/高 profile
   fixed-seed cases。
2. 断言每个轴只改变许可 evidence；rules、artifact identity、movement/legality 等
   禁止项保持不变。
3. 每个 case 记录 deterministic work units、degradation、replay hash 与 wall-clock；
   wall-clock 只写测试输出/报告。
4. 运行既有缩短后的 30-sample formal/3v3 calibration 和 20-set symmetry。
5. 提交 matrix 与报告。

## Task 6：完整验证、审查与交付

**Create**

- `docs/changes/2026-07-28-001-full-rally-v4-gate-k-completion.md`

**Modify**

- `docs/changes/README.md`
- `docs/development.md`
- `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

**Validation**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode \
  -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults "$PWD/TestResults/GateK-final-editmode.xml" \
  -logFile "$PWD/TestResults/GateK-final-editmode.log"

/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode \
  -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testResults "$PWD/TestResults/GateK-final-playmode.xml" \
  -logFile "$PWD/TestResults/GateK-final-playmode.log"

rg -n "TeamRallyDecisionPlanner|OrderedCandidates|SelectGeometricSetTarget|SelectAttackRoute|TrySelectCoverPlayer" \
  Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs
rg -n "UnityEngine|Volleyball\\.Presentation|MatchReplayRecorder" \
  Assets/Volleyball/Match/Runtime/AI Assets/Volleyball/Match/Runtime/Domain
git diff --check
```

Review：duplicate writer、stale callback、formal/3v3 leakage、hidden field exposure、
canonical compatibility、work-counter determinism、wall-clock decision coupling、Replay/HTML
byte stability和遗漏场景。修复发现后按风险复跑 affected focused suite；material runtime
修复后重跑完整 PlayMode。

完成记录必须列出精确 totals/duration、校准 matrix、HTML/hash stability、静态扫描、
手动双视角检查、Windows x64 与 profiler 未完成项。全部自动门通过后将 Gate K 和
Full Rally V4 标记完成。
