# CHG-20260719-002：Physical3v3Rally 统一多角色回合决策

- 日期：2026-07-19
- 状态：已完成
- 负责人：Match / Docs
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/blocking-roles`
- 关联提交或 PR：`892c77d` 至 `33e6dc2`

## 改动摘要

Physical3v3Rally 不再按固定的 `Defender -> Setter -> Attacker` 顺序推进触球，
而是根据实际位置、剩余时间、球员能力和当前合法触球状态，以固定种子在三名球员中选择
接球、组织和进攻人选。名义位置只提供有界的得分偏好，不再限制球员能否执行某项技术。

本次同时把拦网从视觉支持动作升级为真实物理触球，并在球速改变前统一判断候选触球是否合法。
改动严格位于 Match 与文档目录内；Shared、Career、Bootstrap、场景路径、程序集引用、
`MatchContextV1` 与 `MatchResultV1` 均未改变。

## 具体变更

- 文件、场景、资源或程序集：
  - 在 Match Domain 新增 `TeamCourtFrame` 与 `RallyTouchState`，统一坐标转换、球权、
    三次触球、连续触球和零计数拦网规则。
  - 在 Match AI 新增确定性的 `TeamRallyDecisionPlanner`、进攻助跑质量、战术权重边界和
    真实网平面拦网交点预测。
  - 修改 `SimulatedBall`，在应用物理响应前解析 `Ignore`、`Accept`、`Fault`，并统一同一步内
    球员、球网、地面和过网事件的先后顺序。
  - 修改 `PrototypePlayerAgent`、触球面和人体姿态，加入独立物理拦网窗口、有限重定向、
    连续助跑及与计划一致的进攻触球预览。
  - 重写 `ThreeVsThreeRallyDirector` 为球权编排器，记录真实触球人、真实移动距离、动态角色、
    拦网后球权和完整单局诊断。
  - 场景文件、场景路径和程序集定义未变化；没有新增需要迁移的序列化资源。
- 新增或修改的公开类型/字段：
  - 新增 `TeamCourtFrame`、`RallyTouchState`、`RallyContactWindow`、
    `TeamRallyDecisionPlanner`、`AttackApproachPlan`、`NetPlaneInterception`、
    `BallContactResolution` 与拒绝触球事件。
  - `ThreeVsThreeRallyDirector` 新增真实物理拦网、拦网后续球、非二传组织、防守者进攻、
    非法触球和最大有限修正诊断。
- 行为变化：
  - 普通 Receive、Set、Attack 计入当前球队触球次数；第四次触球和同一球员连续触球在改变
    球速前判罚。
  - Block 是真实触球但不消耗球队触球次数；反弹后实际控球队从零次触球开始新球权。
  - Unity 坐标约定固定为：`X` 表示世界左右，`Y` 表示高度，`Z` 表示场地纵深；球队本地
    战术坐标只镜像 `Z`，不镜像世界左右。
  - 每次进攻交换冻结同一战术修订；新战术只在回合结束后生成。运行时不调用 MenShen，
    只保留有界且可确定回退的战术权重入口。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；`MatchContextV1` 输入和 `MatchResultV1` 输出保持原版本与结构
- 对方开发者需要做什么：无；Shared、Career 与 Bootstrap 无需改代码、数据或资源

## 验证

- [x] EditMode 测试
- [x] PlayMode 测试
- [ ] 手动场景验证
- [ ] 序列化、存档或迁移验证（不适用：没有修改序列化契约或资源路径）

使用 Unity `6000.0.43f1`、固定随机种子和 macOS 批处理 Test Runner 验证：

- EditMode：`214/214` 通过，`failed=0`，证据为
  `TestResults/EditMode-unified-rally-final.xml` 与同名 `.log`。
- PlayMode：`3/3` 通过，`failed=0`，证据为
  `TestResults/PlayMode-unified-rally-final.xml` 与同名 `.log`。
- 完整 Physical3v3Rally 单局日志记录了 Attacker Set、Defender Attack、真实
  `block-contact`、拦网后 `touches=0` 球权和
  `RESULT score=15:2 contacts=30 blocks=1 nonSetterSets=1 defenderAttacks=1`。

`TestResults/` 继续保持忽略，不提交到仓库。自动化 PlayMode 已完整运行场景；本次没有另做
人工 Play Mode 验收，也没有需要验证的序列化、存档或迁移变更。

## 回滚与风险

主要风险是触球窗口过窄、进攻预览与实际助跑几何偏离、拦网重定向超界，或事件排序让非法
触球先改变球速。观察 `[Physical3v3] decision/contact/block-window/block-contact/possession/RESULT`
日志、非有限状态计数和最大移动修正即可定位。

需要回滚时按提交逆序撤销：先撤销动态 Director 与触球几何，再撤销物理拦网窗口、物理前
候选解析、多角色规划器、触球状态和坐标契约。各层提交保持 Match 内部边界，可逐层回滚；
不要同时回滚或提交无关的 `ProjectSettings/ProjectSettings.asset` 本地修改。
