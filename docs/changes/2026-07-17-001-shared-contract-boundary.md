# CHG-20260717-001：Shared 比赛与生涯契约边界

- 日期：2026-07-17
- 状态：已完成
- 负责人：Shared / Match
- 影响模块：Shared / Match / Career
- 交互级别：跨模块（重点）
- 关联分支：`codex/shared-contract-boundary`
- 关联提交或 PR：`f6e8f33`

> [!IMPORTANT]
> Career 必须使用字符串形式的稳定 `PlayerId` 和 `TeamId` 生成本场快照；Match 只消费
> 冻结后的 `MatchContextV1`，并用同一 `sessionId + contractVersion + contextHash` 返回
> `MatchResultV1`。Match 原型中的蓝/橙方 `PlayerId` 只是场上槽位，禁止写入生涯存档。

## 改动摘要

建立 Unity-free 的 `VolleyballMatch.Shared` 程序集，作为 Match 与 Career 唯一的数据契约
边界。新增稳定身份、球员能力快照、比赛输入、比赛结果、JSON 序列化和 SHA-256 上下文
校验；Match 侧增加稳定生涯身份到临时场上槽位的绑定。

## 具体变更

- 契约源码：`Assets/VolleyballMatch/Shared/Runtime/`
- 主要类型：`PlayerAbilitySnapshotV1`、`MatchContextV1`、`MatchResultV1`
- 身份类型：Shared 的字符串 `PlayerId` / `TeamId`
- Match 适配：`MatchPlayerBinding`、`PlayerAbilityProfile` 快照转换
- 边界验证：Shared 不引用 UnityEngine、Match 或 Career 程序集

## 跨模块交互重点

- 接口提供方：Shared；Match 与 Career 分别负责填充和消费自己一侧的数据
- 接口使用方：Match / Career / Bootstrap
- 数据方向：`Career -> MatchContextV1 -> Match -> MatchResultV1 -> Career`
- 兼容性：当前版本为 V1；不支持的 `contractVersion` 必须拒绝，禁止静默降级
- 对方开发者需要做什么：Career 实现时直接使用 Shared 稳定 ID 和能力快照；不要引用
  `VolleyballMatch.Domain.Prototype.PlayerId`

## 验证

- Unity：`6000.0.43f1`
- EditMode：99/99 通过
- PlayMode：3/3 通过
- 已验证同种子可重复、不同种子参与结果、JSON 往返、篡改哈希拒绝和结果归属校验

## 回滚与风险

最大风险是两种 `PlayerId` 被误用。保留显式别名和 `MatchPlayerBinding`，并通过程序集边界
测试防止 Shared 反向引用 Match。契约一旦被 Career 存档使用，不应原地修改 V1 字段语义；
需要破坏性变化时新增版本。
