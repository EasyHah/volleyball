# CHG-20260731-001：正式训练情景实验室

- 日期：2026-07-31
- 状态：已自动验证，待人工验收
- 负责人：Match
- 影响模块：Match / ProjectSettings / Tests / Docs / Build
- 交互级别：跨模块（重点）
- 关联分支：`codex/formal-training-scenario-lab`
- 关联提交或 PR：`65e8d40`、`35fae3a`、`f8a9839`、`f1c5927`、
  `5ac02c7`、`c38d74e`、`074e5bf`；PR 待创建

> [!IMPORTANT]
> Match 新增独立训练场景和 Windows Development Build 入口，并仅在既有
> `EditorBuildSettings` 末尾追加场景；Career 场景及原有顺序不变。没有修改 Shared
> 合同、Career 存档或 Bootstrap 入口，现有消费方无需改代码。

## 改动摘要

新增一个受约束的可视化正式训练情景实验室。开发者可以编辑球、十二名球员、球线速度、
语义化回合起点、战术与 `matchSeed`，通过严格校验后运行一条真实正式 6v6 回合。运行阶段
复用既有球体、AI、Gate H/I、V3 规则、真实几何碰撞和裁判，不开放触球、比分或裁判写入口。

首个里程碑覆盖二触触网留本方、三触触网留本方、三触触网后越网、拦网后触网、同飞行段多次
触网单次分派，以及二触回弹不可达变体。它不包含玩家接管、手柄编辑、任意检查点恢复、大模型
调用或本地深度学习模型。

## 具体变更

- 新增 `RallyStartStateV3` 语义工厂、`TrainingScenarioV1`、可变草稿、严格 issue 定位、
  canonical bytes/hash 和一次性训练启动入口。
- 正式 6v6 Director 可从五类合法语义起点建立 V3/compatibility 状态，并在一条真实裁判结果后
  停止；UI 不持有 contact、score 或 referee command surface。
- 新增六个项目训练资源及固定种子双跑，使用真实网体碰撞、过网事件、accepted contact、
  Gate H/I receipt 和正式裁判断言。
- 新增只读时间线、稳定 `DecisionSnapshotV1` hash、候选排序和 Editor-only JSONL 导出。
- 新增一体化四区 UI、独立 preview root、球/球员选择拖放、属性与 issue 聚焦、编辑锁定、
  暂停、精确固定步单步、同 seed 重跑与运行比较。
- 新增 `FormalTrainingScenarioLab.unity`、场景生成器和 Windows x64 IL2CPP Development
  Build 入口；构建列表只在原有场景后追加训练实验室。
- 将原正式整局 smoke 的实时预算从 360 秒提高到 480 秒；固定结果条件和生产行为不变。

## 跨模块交互重点

- 接口提供方：Match
- 接口使用方：Unity Editor / Development Build；Career 无新消费
- 数据方向：训练草稿 -> Match immutable scenario -> Match formal runtime -> 只读诊断
- 兼容性：兼容；`MatchContextV4`、`MatchResultV4`、`MatchReplayV4`、旧场景 hash、
  Replay provenance 和正式比赛入口未改。
- 消费方或后续负责人需要做什么：无需改代码。Windows 验收机需要 Unity
  Windows Build Support (IL2CPP) 模块。

## 验证

- [x] 完整 EditMode：Unity `6000.3.20f1`，macOS arm64，1518/1518。
- [x] 指定 PlayMode：19/19，覆盖训练运行器、六类触网双跑、observer 等价性、
  实验室生命周期和原有防守续球。
- [x] 独立实验室场景：EditMode 10/10；PlayMode 2/2。验证单一 preview/runtime、
  13 个预览标记、编辑锁定、键盘暂停、精确 `1/120s` 单步、双跑一致和 reset。
- [x] 正式 6v6 整局 smoke：1/1，387.47 秒，25:23。
- [x] Repository validator、Match/Career 依赖扫描和 `git diff --check`。
- [ ] Windows x64 IL2CPP Development Build：入口已执行，但当前 macOS Unity 只安装
  `MacStandaloneSupport`，明确失败为 `build target was unsupported`；未生成 manifest，
  不能记为通过。
- [ ] 1920×1080 键鼠画面验收：自动化 UI 操作被 macOS 锁屏阻止，待解锁后执行。
- [ ] 独立全面复核：当前线程规则要求用户明确授权 subagent，尚未执行。

## 回滚与风险

按提交倒序回滚可分别移除场景/构建入口、UI、observer、情景资源、运行器与契约。旧正式场景、
Replay 和 Shared 合同没有迁移或回滚数据。当前剩余发布风险仅是 Windows 目标模块缺失导致的
Player 构建/实机验收空缺、锁屏导致的视觉验收空缺，以及尚未获授权的独立复核；在三项完成前
状态保持“待人工验收”，active handoff 不移入 completed。
