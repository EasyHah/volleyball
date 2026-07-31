# 正式训练情景实验室实现

- Status: active
- Owner: Match collaborator
- Priority: P1
- Scope: Match / Formal 6v6 / 独立训练场景 / 开发者 UI / 诊断
- Supersedes:
  `docs/handoffs/completed/2026-07-30-v4-unified-net-continuation-implementation.md`

## 当前要求

按 `docs/superpowers/specs/2026-07-31-formal-training-scenario-lab-design.md` 实现独立的正式训练情景
实验室。第一阶段以受约束的语义化回合起点、正式 6v6 运行器复用和五类触网情景完成可独立验收的
功能闭环。

训练 UI 只构造并校验启动输入。运行开始后必须使用现有球体、12 名球员、AI、V3 规则、真实几何
接触和裁判链路，且编辑器完全锁定。

## 非目标

- 不实现玩家接管、玩家训练菜单或手柄编辑。
- 不开放底层规则字段、强制触球、比分或裁判结果编辑。
- 不实现任意物理检查点恢复或从时间线节点继续模拟。
- 不调用大模型、不管理标注数据集、不训练或加载本地深度学习模型。
- 不修改 `MatchContextV4`、`MatchResultV4`、`MatchReplayV4`、Career 存档或结算。
- 不复制、简化或替换正式物理、AI、触球规则和裁判。

## 交付物

- `TrainingScenarioV1`、语义化回合起点、严格校验、规范 hash 和一次性启动入口。
- 独立训练实验室场景及一体化开发者指挥台。
- 全 AI 运行、暂停、固定步单步、只读时间线、重置、同 `matchSeed` 重跑与对比。
- `DecisionSnapshotV1` 的只读查看与导出入口。
- 五类触网情景及不可达变体。
- 自动验证、Windows Development Build、人工验收记录和变更记录。

## 验收条件

- 旧正式情景资源、内容 hash、Replay provenance 和正式比赛入口不变。
- 训练情景不能构造非法触球数、第四次触球窗口或冲突的 post-block 权威。
- 运行时不能从 UI 直接提交触球、移动实体、修改比分或指定裁判结果。
- 同平台固定输入双跑得到相同规则结果、accepted-contact 序列、续球证据和决策输出。
- 五类触网情景、受影响 Match 回归和正式 6v6 整局 smoke 通过。
- Windows Development Build 与键鼠实机验收通过。

## 依赖资料

- `AGENTS.md`
- `docs/rules.md`
- `docs/superpowers/specs/2026-07-31-formal-training-scenario-lab-design.md`
- `docs/superpowers/specs/2026-07-30-v4-unified-net-continuation-design.md`
- `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`

## 结束处理

完成或替代时，将本文件移入 `docs/handoffs/completed/`，更新 Status、结束日期和后继任务单链接。
