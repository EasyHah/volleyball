# 二传教师审核流水线实施计划

- 前置规格：`docs/superpowers/specs/2026-07-31-setter-teacher-review-design.md`
- Active handoff：`docs/handoffs/active/2026-07-31-setter-teacher-review-implementation.md`
- 风险等级：高风险；新增 Editor 网络调用、本地不可逆数据记录和训练数据合同，但不得影响比赛权威。

## 1. 范围与完成定义

本计划仅交付训练实验室中 `RallyDecisionStage.Organization` 的二传攻手候选教师审核。复用
`DecisionSnapshotV1` 与 `MenShenChatClient`，在 Editor 内逐条请求、审核和写入本地 JSONL。

完成时必须满足：

- 教师请求完整镜像本地二传决策在该时刻可见的信息，且只有可行候选可被排序；
- 教师无法生成新候选或控制任何运行时对象；
- 人工只能接受教师首选或改选现有可行候选；
- 本地 JSONL 保存不可变教师尝试和人工确认记录，只有人工确认记录进入黄金集；
- 新网络代码只在 Editor assembly，正式 AI、Player、Replay、Shared 和 Career 不变。

## 2. 实施前门禁

1. `git fetch origin`，检查 `origin/main` 和当前分支差异。
2. 确认唯一 active handoff 是二传教师审核任务，工作区干净。
3. 确认 `TestResults/` 被 Git 忽略，且没有待提交的本地导出样本。
4. 运行现有快照和 MenShen 客户端 focused EditMode，先记录基线。

```bash
UNITY="/Applications/Unity/Unity-6000.3.20f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testFilter "Volleyball.EditModeTests.DecisionSnapshotV1Tests;Volleyball.EditModeTests.MenShenChatClientTests;Volleyball.EditModeTests.MenShenBenchmarkRunnerTests" \
  -testResults "$PWD/TestResults/SetterTeacher-Baseline.xml" \
  -logFile "$PWD/TestResults/SetterTeacher-Baseline.log"
```

## 3. 阶段 A：纯合同、信息集和严格解析

新增 Editor-only 的 `Assets/Volleyball/Match/Editor/AI/SetterTeacher/`：

- `SetterTeacherRequestV1.cs`
- `SetterTeacherResponseV1.cs`
- `SetterTeacherPromptV1.cs`
- `SetterTeacherResponseParserV1.cs`
- 对应 EditMode 测试。

实现：

- 工厂只接受 `Organization` 快照；从快照复制所有本地可见字段与可行候选的评分分量。
- 候选集 hash 基于按稳定 `PlayerId` 排序的可行候选及评分字段；请求 hash 包含快照 hash、候选集
  hash、规则/特征版本和提示词版本。
- 提示词要求一个严格 JSON 对象：schema 版本、完整 candidate ID ranking 和审核理由。它明确禁止模型
  选择未知 ID、建议动作或引用不可见信息。
- 解析器拒绝额外字段、错误 schema、未知/重复/遗漏 ID、非可行候选和空理由；不得尝试猜测或修复。
- `DecisionSnapshotV1` 不增加教师字段、不改变既有 hash 或 Player 代码。

测试：稳定 hash、仅 Organization 可请求、信息字段白名单、候选排序规范化，以及正常/未知/重复/遗漏/
额外字段/畸形 JSON 的拒绝。

阶段提交：`feat: add setter teacher review contracts`

## 4. 阶段 B：教师调用与不可变审核状态

新增：

- `SetterTeacherReviewServiceV1.cs`
- `SetterTeacherAttemptV1.cs`
- `SetterHumanReviewV1.cs`
- `SetterLabelRecordV1.cs`
- `SetterTeacherReviewSessionV1.cs`

实现：

- 服务只通过 `IMenShenChatClient` 调用既有客户端；配置继续复用 `MENSHEN_API_KEY`、HTTPS/localhost
  端点限制和既有错误状态。
- 每次成功、超时、限流、未授权、网关错误或解析失败都生成只读尝试结果；可重试，但不修改旧尝试。
- 仅含严格成功响应的尝试可创建审核。审核接受教师首选，或指定当前可行候选；其他 ID 一律抛错。
- 人工确认冻结原始响应、模型 ID、提示词版本、快照/候选集 hash、最终选择、一致性和可选原因。
- 服务与 session 只能读取冻结快照，绝不持有 Director、球员、Replay recorder 或运动控制引用。

测试：假客户端验证成功、超时、限流和畸形响应；重试历史不变；接受/纠正/非法候选；教师、本地和人工
选择的一致性计算。

阶段提交：`feat: add setter teacher review service`

## 5. 阶段 C：本地数据集与版本清单

新增：

- `SetterLabelDatasetWriterV1.cs`
- `SetterLabelDatasetManifestV1.cs`
- `SetterLabelDatasetReaderV1.cs`（只用于 Editor 验证）
- 测试 fixture 与 EditMode 测试。

实现：

- 固定根目录为 `TestResults/TrainingLab/SetterLabels/`，创建目录后 append-only 写 JSONL；不写
  `Assets/`、`ProjectSettings/` 或任意用户路径。
- 尝试和人工确认记录必须能独立校验 schema/hash，写入前验证；I/O 失败不得标记审核已保存。
- 黄金集导出仅筛选 `HumanConfirmed` 条目；teacher-only 状态本期不产生。
- 仓库内只保留清单 schema 和可复现统计格式，不含样本、原始响应、端点或密钥。

测试：写入顺序/规范化、拒绝重复或损坏记录、清单统计、黄金集过滤、临时目录 I/O 失败恢复及 Git
状态扫描。

阶段提交：`feat: persist reviewed setter labels locally`

## 6. 阶段 D：Unity Editor 审核界面

修改训练实验室 Editor-only UI，新增审核窗或侧栏：

- 从只读 Organization 决策快照打开审核；其他阶段入口禁用。
- 显示本地选择、候选评分、教师排序、理由、模型/提示词版本、调用状态和历史尝试。
- 请求期间禁用重复请求；成功后只能接受教师首选或选择可行候选；确认后刷新为只读历史。
- 网络/解析失败显示可重试错误，不改变训练运行、时间线或当前选择。

测试：Editor controller/UI focused 测试覆盖入口过滤、控件锁定、接受、纠正、失败和重试。人工检查用
假客户端验证一次完整“快照 -> 教师 -> 接受 -> 本地 JSONL”与一次“纠正 -> 本地 JSONL”流程。

阶段提交：`feat: add setter teacher review editor`

## 7. 冻结验证、回滚和交接

代码冻结后运行：

1. 新合同、服务、数据集和 UI focused EditMode。
2. 受影响训练实验室 EditMode 与既有 MenShen 客户端/benchmark tests。
3. 静态依赖扫描，证明网络代码只在 `Assets/Volleyball/Match/Editor/`，且 Shared/Player/Replay/Career
   无新增引用。
4. 一次 Editor 手动闭环，并检查 `git diff --check` 和 Git 忽略数据集未进入 diff。
5. 最多一次独立全面复核。

回滚按阶段提交倒序移除 Editor-only 合同、调用服务、数据集与 UI；`DecisionSnapshotV1`、正式运行时
和训练实验室的既有快照导出保持不变。
