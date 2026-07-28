# CHG-20260728-005：Career 接入正式 6v6 V4

- 日期：2026-07-28
- 状态：已自动验证，待人工验收
- 负责人：Career / Bootstrap / Match
- 影响模块：Career.MatchIntegration / Bootstrap / Match.Presentation / Tests / Docs / Tools
- 交互级别：跨模块（重点）
- 关联分支：`milestone/career-formal-6v6-v4`
- 关联提交或 PR：`e04bc61` / [PR #4](https://github.com/EasyHah/volleyball/pull/4)

> [!IMPORTANT]
> Match 提供生产者中立的外部 `MatchContextV4` 场景入口，Bootstrap 负责 additive 场景生命周期，
> Career 继续只消费 Shared V4。Shared Runtime 与存档 Schema 不变；Match 目录解除首里程碑冻结，
> 但改动必须通过 Pull Request 由 `@EasyHah` 复核。

## 改动摘要

把第一周比赛从固定 Fixture runner 切换为现有 `FormalIndoor6v6`。`PendingMatch` 仍在加载场景前
原子提交；正式比赛完成后返回同一 session/context 的 `MatchResultV4` 并沿用既有幂等结算。取消、
加载失败或运行时初始化失败不伪造结果，返回赛前并保留原 PendingMatch。

本阶段仍是单局 25 分自动 6v6，不加入多局制、换人、自由人替换、比赛中途存档或玩家直接控制。

## 具体变更

- `Career.MatchIntegration`：
  - mapper 可注入物理与预测器配置，Fixture 默认行为保持兼容；
  - 首场工厂支持 `Direct`，Fixture 默认构造保持测试与开发工具兼容；
  - V4 workload 强制要求 `[0,1]`，拒绝单位混用。
- `Match.Presentation`：
  - Formal bootstrap 在 `Start` 前接受外部 V4 上下文，无外部上下文时仍运行原沙盒；
  - 外部上下文槽在 Unity `SubsystemRegistration` 时重置，兼容禁用 Domain Reload 与中断 Play；
  - 暴露初始化状态和 Director，供场景 runner 观察结果；
  - V4 正式路径把 `context.Seed` 用于 execution error 等随机执行入口；纯确定性决策 planner
    删除未使用的 seed 参数。
- `Bootstrap`：
  - additive 加载 `FormalIndoor6v6`，隐藏但不销毁 Career UI/控制器；
  - 完成、Escape/B 取消或失败后卸载 Match 场景并恢复 Career；
  - 旧开发档中已提交的 Fixture PendingMatch 继续路由至固定 runner，新建 Direct 比赛走物理场景；
  - 将物理累计 workload 按 `rawWorkload / ralliesPlayed` 归一化到 `[0,1]`。
  - Windows Career 构建显式包含 Career 与 FormalIndoor6v6 两个场景。
- `CareerVerticalSlice.unity` 与场景生成器：
  - 修复丢失的 `CareerMenuRuntime` 序列化引用；
  - 增加场景引用检查，防止构建成功但 Player 启动时 Bootstrap 被禁用。
- `tools/repository_policy.json`：
  - 移除 Match 目录的阶段性冻结树；保留 Match CODEOWNER 与目录 `.meta` 冻结。
- `.github/workflows/repository-validation.yml`：
  - 同步解除旧的整目录 Match 冻结门禁，继续冻结 Match 根 `.meta` 与 Shared Runtime。

## 跨模块交互重点

- 接口提供方：Match / Shared V4
- 接口使用方：Bootstrap / Career.MatchIntegration
- 数据方向：`Career -> Shared V4 -> Match -> Shared V4 -> Career`
- 兼容性：兼容。Shared DTO、规范 JSON、存档 Schema 和 Fixture 默认构造均不变；已有开发档中的
  Fixture PendingMatch 使用已冻结 canonical context 继续由固定 runner 执行；未知配置明确失败并保留
  PendingMatch。
- 消费方或后续负责人需要做什么：`@EasyHah` 复核 Formal bootstrap 与 seed 改动；无需修改 Shared。

## 验证

- [x] Career MatchIntegration / PendingMatch EditMode：24/24
- [x] 最终 mapper / workload focused EditMode：7/7
- [x] 场景引用与 Windows 构建参数 EditMode：2/2
- [x] Formal 外部上下文 PlayMode：1/1
- [x] runner 配置路由、workload 边界、静态槽重置与取消卸载 PlayMode：5/5
- [x] Match planner 受影响 EditMode：64/64
- [x] Formal 真实 25 分单局 PlayMode：1/1，`25:2`，约 `122.6s`
- [x] `python tools/validate_repository.py --base origin/main`
- [x] 独立 Agent 全面复核及一次针对性复审：无剩余 P0/P1/P2
- [x] Unity `6000.3.20f1` Windows x64 IL2CPP Development 构建：
  `Builds/Windows/VolleyballCareer.exe`，manifest 总大小 `1,522,532,642` bytes
- [x] 无图形 Player 启动烟雾：8 秒内无 Bootstrap/场景/输入异常
- [ ] Windows 可视化人工闭环：创建/载入生涯、进入比赛、Escape/B 返回赛前、完成比赛并查看结算

自动验证已完成；仍需用户执行可视化与实际输入验收，因此状态保持“待人工验收”。

## 回滚与风险

主要风险是 additive 场景恢复顺序、取消与结果同时发生、物理配置哈希不一致，以及后续新增随机
入口未绑定 `MatchContextV4.Seed`。安全回滚为恢复 Bootstrap 的 Fixture runner、恢复 Match 冻结策略
并删除场景 runner；PendingMatch 与存档不需要迁移，未结算比赛可按原上下文重试。
