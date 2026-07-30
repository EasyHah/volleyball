# V4 发球触网续球修复

- 状态：已自动验证
- 分支：`codex/v4-defensive-contact-scenarios`
- 范围：正式 6v6 开局发球、Gate H 接发重规划、物理接触与场景回归

## 结果

- 发球擦网后仍合法进入接发方时，触网前 Gate H 窗口失效，并以新的
  plan revision、source sequence 和实际反弹轨迹重规划接发。
- 新接发必须发生在合法过网之后，并继续通过真实 swept geometry、V3 authority
  和 Replay accepted contact。
- 擦网过网但接球队员赶不到时，系统仍产生防守反应，但不产生磁吸式触球；
  ground referee 唯一结算并保留发球者得分归因。
- 发球正面弹回发球方时，不创建新 Gate H revision 或接发候选，并由接发方得分。
- 初始发球的飞行时间、到达垂直速度和相对接发人深度偏移成为完整场景输入，
  全部进入场景 canonical hash；正式默认值保持原行为。

## 验证

- `FormalMatchScenarioPresetV4Tests`：12/12。
- 发球触网 focused PlayMode：3/3。
- `DefensiveContactContinuationPlayModeTests`：11/11。
- 完整 EditMode：1467/1467，13.45 秒。
- 完整 PlayMode：51/51，749.32 秒。
- 25 分正式整局：1/1，25:14，287.08 秒。
- 独立审查未发现 P0/P1 规则、possession 或 revision 生命周期缺陷。
- `git diff --check`：通过。

## 已知边界

Replay V4 当前要求一个 segment 至少包含一个 accepted player contact。因此擦网漏接和
弹回发球方这类合法的零球员触球回合不能单独形成正式 Replay segment。本次没有伪造
Serve contact，也没有修改 Shared schema；两类结局使用独立双运行物理结果验证。

若产品要求零触球回合也必须拥有正式 Replay，需要在获得 Shared 公共合同任务授权后，
升级 Replay 表达能力和 backward-compatibility 测试。
