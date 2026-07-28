# Full Rally V4 Gate K Calibration

**日期：** 2026-07-28

**平台：** macOS，Unity 6000.0.43f1，batchmode PlayMode

**固定 key：** `gate-k-calibration-seed-73421`

## 四轴 deterministic matrix

`GateKCalibrationMatrixPlayModeTests`：4/4 通过，suite wall-clock
0.0286s。单项 wall-clock 只用于本报告，不进入 gameplay、Replay hash 或任何决策。

| 轴 | low/high input fingerprint | 允许变化 | 已锁定不变 | wall-clock |
| --- | --- | --- | --- | ---: |
| AttackControl | `a9703541…` / `e54b0f01…` | attack target/velocity error | maximum velocity、effort | 12.541ms |
| SoftTouch | `defbf366…` / `5059f02d…` | soft-action target/velocity error | power-attack target error | 0.733ms |
| BlockTechnique | `199ce4a9…` / `79d7360c…` | block target/velocity error | maximum velocity、reach height | 0.564ms |
| CourtAwareness | 0.0 / 1.0，config `gate-j-v1` | recognition delay、confidence、uncertainty | public artifact、revision、source sequence | 4.022ms |

完整 input/result fingerprint 与 low/high envelope identity 保存在
`TestResults/GateK-calibration-matrix-final.xml` 的
`GateKCalibrationEvidence` 输出中。

## Replay、work budget 与 HTML 稳定性

两个独立 fixed-seed formal rally capture：

- Replay hash：
  `d244ee97c87c325350e63f615b2d592d5980d5e686405d2950c023af4ab4414b`；
- 3 个 event 的 deterministic work units：`35, 49, 49`；
- degradation：全部 `FullSampling`；
- budget outcome：全部 `WithinBudget`；
- canonical Replay bytes、accepted event sequence、Replay hash 和 HTML bytes
  两次完全一致；
- focused test 1/1 通过，wall-clock 7.5365s。

work units 由 `candidateCount * sampleCount * (expansionCount + 1)` 得出。
configuration identity 来自该事件实际使用的 envelope policy，而非固定默认值。
wall-clock、allocation、thread、cache hit、frame 和 profiler 数据不进入
`ReplayWorkBudgetRecordV4` 或 canonical hash。

## 缩短后的长校准

`AttackChainCalibrationPlayModeTests`：3/3 通过，suite wall-clock 50.6581s。

| 场景 | 样本 | 结果 | wall-clock |
| --- | ---: | --- | ---: |
| Formal 6v6 in-system | 30 | attackable 0.967；A-grade no-contact 0；normal-side set 0 | 6.7946s |
| 3v3 in-system | 30（8 matches / 40 total sets） | attackable 1.000；A-grade no-contact 0；near-net attack 33/33 | 34.6094s |
| Formal symmetry | 20 one-point sets | Blue wins 保持 9–11 验收窗 | 9.2469s |

这些数量是 Gate K 的缩短版回归预算；阈值未降低，50 分 rules safety cap
也未修改。

## 结论

四个 live ability axis 均产生可解释且范围受限的 delta。正式 Replay 的
deterministic work evidence 在固定 seed 下稳定且未触发 degradation；实际耗时只作为
交付观测记录，不参与 simulation authority。
