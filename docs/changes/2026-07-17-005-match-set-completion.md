# CHG-20260717-005：Physical3v3Rally 可结束单局与结果产出

- 日期：2026-07-17
- 状态：已完成
- 负责人：Match
- 影响模块：Match / Shared / Career
- 交互级别：跨模块（重点）
- 关联分支：`codex/match-set-completion`
- 关联提交或 PR：尚未创建

> [!IMPORTANT]
> Match 现在可在 `Physical3v3Rally` 完成一局 15 分且至少领先 2 分的比赛，并产出
> 既有 `MatchResultV1`。Shared DTO 字段未改；未来 Career/Bootstrap 只能消费完成后非空且已
> 通过上下文校验的结果，当前场景使用固定六人沙盒 `MatchContextV1`。

## 改动摘要

将物理 3v3 循环改为可结束的比赛单局。每球得分；接发方得分后取得发球权并轮转三名球员，
发球方得分不轮转。比赛结束时停止球体和后续回合，显示 `RESULT READY`，并保留完整六人
统计的 `MatchResultV1`。

不包含多局赛制、换人、暂停、存档、Career 场景或 Bootstrap 场景切换。

## 具体变更

- 文件、场景、资源或程序集：新增 Unity-free `MatchSet` 与落点/标志杆裁判；物理球新增
  net-plane crossing 事件；`Physical3v3Rally` 使用固定沙盒上下文、比分显示和结果状态。
- 新增或修改的公开类型/字段：`MatchSet`、`MatchRallyReferee`，以及
  `ThreeVsThreeRallyDirector.Result`。
- 行为变化：最后有效触球后，合法落在对方场内得分；本方场内、对方场外、标志杆外越网均
  判对方得分；合法触网继续；接球超时判预定接球队失误。

## 跨模块交互重点

- 接口提供方：Match
- 接口使用方：未来 Career / Bootstrap
- 数据方向：`Career -> MatchContextV1 -> MatchSet -> MatchResultV1 -> Career`
- 兼容性：Shared V1 契约字段未变化；新增 Match 侧结果可用时机。现有消费者无需修改 DTO。
- 对方开发者需要做什么：Bootstrap 接入真实上下文后，在 `ThreeVsThreeRallyDirector.Result`
  非空时读取结果并调用 `ValidateAgainst(context)`，不要从场景对象推导比分或统计。

## 验证

- [x] EditMode 测试：Unity `6000.0.43f1`，114/114 通过。
- [x] PlayMode 测试：Unity `6000.0.43f1`，3/3 通过。
- [x] 手动场景验证：由 PlayMode 结果覆盖物理 3v3 完局、停止与结果可读状态。
- [x] 序列化、存档或迁移验证（如适用）：`MatchResultV1.ValidateAgainst` 覆盖在 MatchSet 测试中。

## 回滚与风险

主要风险是物理球事件在同一帧重复结算；导演使用单回合终局保护，避免地面、过网与超时事件
重复记分。回滚时整体还原本改动，不能只移除结果输出而保留已改写的回合结算。
