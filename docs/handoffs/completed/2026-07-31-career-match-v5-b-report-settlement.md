# Career x Match V5-B 报告与结算

- Status: completed
- Owner: Career / Match collaborator
- Priority: P1
- Scope: `CareerMatchReportV1`、物理 replay 聚合、Career V5 结算与 quick simulation trace
- Depends on: `docs/handoffs/completed/2026-07-30-career-match-v5-attribute-contract-design.md`

## 当前要求

按已批准的 V5 规格实现 V5-B：每场已完成的物理 V5 Formal 6v6 必须生成一个与
context/result/replay 绑定的十二人 `CareerMatchReportV1`；Career 只消费已验证报告，
并独立结算成长、疲劳、心态和教练信任。随后补齐 quick simulation trace，保持固定
context/configuration/seed 下的可重现字节。

## 非目标

- 不迁移或读取 V4 pending/result/replay 作为 V5 输入。
- 不实现伤病结论、伤病结算、多局制、换人或自由人替换。
- 不让 fixture 生成正式成长报告。
- 不让 Career 重算或改写 Match 事实、比分、证据或 hash。

## 验收

- 物理 V5 完成时生成一份有效、完整的十二人报告。
- 报告/证据/上下文/result 任一绑定或不变量失效时，整体拒绝结算。
- golden vectors 覆盖 action、接发、关键球、决策、负荷和零值。
- 固定物理与快速模拟输入均可重现 trace/report bytes。
- Career 在不重扫证据的前提下完成成长、疲劳、心态和信任结算。

## 结束处理

完成后移至 `docs/handoffs/completed/`，记录验证、Windows 验收状态及后继任务。

## 完成记录

- 已实现：物理 V5 Formal 6v6 replay 聚合为 context/result/replay 绑定的十二人报告；Career
  仅消费已验证报告并独立结算成长、疲劳、心态和教练信任。
- 已实现：quick simulation trace 以 canonical 回合胜方重建 result/report，固定输入保持 trace/report
  字节可重现；物理证据一对一绑定 attribute evidence，收据支持同 session 幂等和中断恢复。
- 已自动验证：Shared、Career、Match 临时 .NET 编译无错误；物理 golden vector、收据完整性烟测和
  `git diff --check` 通过；独立复核无 P1/P2。
- Windows 验收：按当前授权滞后执行，不在 macOS 进行。后续在 Windows Unity `6000.3.20f1` 完成
  focused EditMode、受影响模块回归、Formal PlayMode 与 Windows x64 IL2CPP Development Build。
