# Career x Match V5-B 报告与结算

- Status: active
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
