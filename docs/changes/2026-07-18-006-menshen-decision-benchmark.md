# CHG-20260718-006：MenShen 排球决策基准

- 日期：2026-07-18
- 状态：已完成
- 负责人：Match
- 影响模块：Match / Docs
- 交互级别：模块内部
- 关联分支：`codex/match-set-completion`
- 关联提交或 PR：本分支提交 `7dca394` 至 `34d4827`，以及后续 Task 9 验证提交

## 改动摘要

新增 Editor-only MenShen 决策基准，用 20 个已批准排球局面比较候选模型在一回合决策与每次触球决策上的 JSON 稳定性、延迟、token 与人工评分素材。该工具只在 Unity Editor 中通过本地 MenShen key 环境变量读取凭证，报告写入本地忽略目录 `TestResults/MenShen/`。

本改动不让任何模型输出驱动 `ThreeVsThreeRallyDirector`、物理、计分、发球轮转或 `MatchResultV1`。运行时接入必须等人工评分后另开设计与计划。

## 具体变更

- 程序集与包：新增 `Volleyball.Match.AI.Editor`，添加 `com.unity.nuget.newtonsoft-json` `3.2.1`。
- 决策协议：新增 `RoundDecisionV1`、`TouchDecisionV1`、触球动作/区域/节奏/风险枚举、第三次计数触球过网校验。
- JSON codec：严格解析一回合与每次触球 JSON，拒绝重复字段、额外字段、非法枚举、解释文本；只允许 runner 执行一次外层 Markdown fence repair。
- 基准数据：新增 `LlmDecisionBenchmarkV1.json`，包含 R01-R10 与 T01-T10；T07 明确拦网触球不计入本队三次触球。
- MenShen 网关：新增 model profile、request builder、SSE accumulator、Bearer 鉴权 streaming client、timeout/error 分类。
- runner/report：新增 deterministic 180-attempt runner，按 A/B/C 匿名输出 `review.md`/`review.csv`，实际模型映射单独写入 `model-map.json`。
- 命令入口：新增菜单 `Volleyball/AI/Run MenShen Decision Benchmark` 与 batch `Volleyball.Editor.AI.MenShenBenchmarkCommand.Run`，支持 `-menshenSmokeCase R03`。
- 文档：`docs/development.md` 新增本地执行命令与 key 安全边界。

## 跨模块交互重点

- 接口提供方：无
- 接口使用方：无
- 数据方向：无
- 兼容性：兼容；新增 Editor-only 工具和 Match 内部 AI 类型，不修改 Shared DTO、Career 数据流或 Bootstrap 场景启动。
- 对方开发者需要做什么：无

## 验证

- [x] EditMode 测试：Unity `6000.0.43f1`，`143/143` passed，结果文件 `TestResults/EditMode-menshen-final.xml`。
- [x] PlayMode 测试：Unity `6000.0.43f1`，`3/3` passed，结果文件 `TestResults/PlayMode-menshen-final.xml`。
- [x] MenShen smoke：`TestResults/MenShen/20260718-003347`，R03 三模型各 1 次，1 success / 2 timeout，无 401，报告未写入 API key。
- [x] MenShen full benchmark：`TestResults/MenShen/20260718-003819`，180 attempts，60 success / 120 timeout，P50 1564 ms，P95 1975 ms。
- [x] 仓库卫生：`git diff --check` 通过；`TestResults/MenShen/*` 由 ignore 规则排除；报告与日志未包含 MenShen key、legacy key header、`Authorization` 或 `Bearer` header。

完整 benchmark 分模型状态：

| 模型 | Success | Timeout |
| --- | ---: | ---: |
| `doubao-seed-2.0-mini` | 30 | 30 |
| `qwen3.7-plus` | 24 | 36 |
| `gpt-5-chat` | 6 | 54 |

这些数字只表示自动格式与延迟采样的原始执行结果；模型是否达标必须等人工补齐 role/space/risk 分数后再判断。

## 回滚与风险

主要风险是本地 live benchmark 消耗 MenShen 配额、候选模型超时率高，以及 Editor-only 代码被误接入 Player build。安全回滚方式：删除 `Assets/Volleyball/Match/Editor/AI/` 下 MenShen benchmark 文件、移除 `com.unity.nuget.newtonsoft-json` 依赖和 EditMode test assembly 的 Newtonsoft 引用；不需要修改比赛运行时、场景或 Shared/Career 契约。
