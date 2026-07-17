# 排球大模型决策基准设计

**目标：** 用 20 个可人工审核的 3v3 排球局面，比较门神网关候选模型在
“一回合决策”和“每次触球决策”两种粒度下的战术合理性、结构化输出稳定性与
响应速度，为联网增强、本地 AI 兜底的比赛 AI 选型提供依据。

## 范围

本基准只设计案例、答案与评分方式，不接入比赛运行时，不修改现有球员 AI、
物理、得分、轮转或 `MatchResultV1`。第一轮候选模型为
`doubao-seed-2.0-mini`、`qwen3.7-plus` 和 `gpt-5-chat`；后续模型可复用同一
测试集。

基准分为两组：

- R01-R10：一回合开始前制定接球、二传和进攻计划，响应期限 2 秒。
- T01-T10：在一次触球完成后选择下一次触球，响应期限 500 毫秒。

模型结果不能直接驱动物理。即使输出通过评分，比赛运行时仍须验证球员可达性、
触球次数、动作合法性和枚举值；任何失败都立即使用本地确定性 AI。

## 固定语义

### 球场方向

- `front` 表示靠近球网，`back` 表示靠近底线。
- `left` 和 `right` 始终以当前行动球队站在本方底线、面向球网时的视角为准。
- 接球、二传和救球时，`target_zone` 表示本方场区的出球目标。
- 进攻和 `free_ball` 时，`target_zone` 表示对方场区的落点目标，但左右仍按当前
  行动球队面向球网的视角描述。
- `tempo` 表示下一次触球后的出球节奏，不表示球员跑动速度：`quick` 是低弧快速
  出球，`normal` 是常规弧度与速度，`high` 是用更高弧线换取处理时间。

### 角色

- `defender`：优先承担后场一传和防守。
- `setter`：优先承担第二次触球组织。
- `attacker`：优先承担第三次触球进攻。
- 角色是能力倾向，不是绝对限制；乱球时允许临时换人处理。

### 拦网触球

- 拦网形成的真实触球必须更新最后触球队伍和球员，用于落点、出界和标志杆违例
  判分。
- 拦网触球应记录球员触球统计和工作量，但不消耗本队三次触球次数。
- 拦网后本队仍有完整三次触球；完成拦网的球员可以立即再次触球，不构成连续
  触球违例。
- 球只碰球网不属于球员触球，不改变触球次数。

### 一回合输出

模型必须只返回一个 JSON 对象，且恰好包含以下字段：

```json
{
  "receiver": "defender | setter | attacker",
  "second_actor": "defender | setter | attacker",
  "set_route": "left_pin | middle_quick | right_pin | back_set",
  "third_actor": "defender | setter | attacker",
  "attack_route": "line | cross_court | deep_seam | roll_shot"
}
```

`receiver` 是计划承担第一次触球的球员，`second_actor` 是计划承担第二次触球的
球员，`set_route` 是第二次触球送往的进攻点，`third_actor` 是计划承担第三次
触球的球员，`attack_route` 是第三次触球线路。同一球员不得连续触球；如果站位和
时间允许，第一次触球球员可以再次承担第三次触球。

### 每次触球输出

模型必须只返回一个 JSON 对象，且恰好包含以下字段：

```json
{
  "next_actor": "defender | setter | attacker",
  "action": "receive | set | attack | free_ball | emergency_save",
  "target_zone": "left_front | middle_front | right_front | left_back | middle_back | right_back",
  "tempo": "quick | normal | high",
  "risk": "safe | balanced | aggressive"
}
```

动作语义：

- `receive`：可控的一传或防守起球，目标通常在本方组织区。
- `set`：为队友组织进攻，目标在本方攻击点。
- `attack`：有主动进攻条件的过网击球，目标在对方场区。
- `free_ball`：没有可靠强攻条件时的可控过网球。
- `emergency_save`：倒地、单手或其他以续球为首要目标的紧急处理。

## 评分规则

每题满分 10 分：

| 评分项 | 分值 | 判断标准 |
| --- | ---: | --- |
| 格式合法性 | 2 | JSON 可解析、字段恰好完整、值全部属于规定枚举 |
| 角色合理性 | 3 | 选人符合站位、能力、疲劳、触球次数和可达性 |
| 空间利用 | 2 | 识别空当、拦防倾向和可用进攻点 |
| 风险匹配 | 2 | 节奏和风险符合球质、比分与球员状态 |
| 首选契合 | 1 | 完全命中首选答案；可接受答案本项为 0 |

首选和可接受答案是评分锚点，不是穷举全部合理战术。没有逐字段命中，但能给出
一致、合法且符合局面的决策时，人工审核者可以按四个基础项给分并写明理由。

以下任一情况直接记 0 分：

- JSON 无法解析，或出现缺失字段、额外字段、非法枚举。
- 第三次触球仍选择 `receive`、`set` 或 `emergency_save`，没有合法过网意图。
- 一回合计划安排同一球员连续完成第一、第二次或第二、第三次触球。
- 选择题面明确标记为无法到达、倒地未恢复或已退出本回合的球员。
- 忽略题面明确给出的触球次数、球网、界线或标志杆约束。
- 原始输出经一次仅去除 Markdown 代码围栏的本地修复后仍无法解析。

超时不参与战术分计算，单独记为兜底事件；模型比较同时报告：平均分、最低分、
JSON 成功率、超时率、P50/P95 总延迟和平均 token 消耗。

## 一回合决策案例

### R01 常规深区接发

**局面**

- 比分 0:0，蓝队接发，触球数 0。
- 发球以中等速度落向本方 `middle_back`。
- Defender 位于 `middle_back`，一传 0.95、反应 0.92；Setter 和 Attacker
  均处于常规站位，体能正常。
- 对方拦防居中，没有明显偏移。

**首选答案**

```json
{"receiver":"defender","second_actor":"setter","set_route":"middle_quick","third_actor":"attacker","attack_route":"deep_seam"}
```

**可接受答案**

- Defender 接球、Setter 二传、Attacker 进攻；二传可选择任一边翼，进攻选择
  `deep_seam` 或与该边翼方向一致的 `line`/`cross_court`。

**禁止答案**

- Setter 或 Attacker 抢接一个 Defender 明确能稳定处理的深区中路球。

**评分理由**

常规局面应保持三人职责和完整进攻选择；中间快攻与后场结合部是没有防守倾向时
的高效首选，但边翼组织仍属合理变化。

### R02 短球迫使二传接一传

**局面**

- 比分 3:2，蓝队接发，触球数 0。
- 短发球快速下坠到 `middle_front`，Setter 距落点 0.7 米，Defender 距离
  3.8 米，Attacker 正在右侧进攻准备区。
- Setter 一传 0.80，Attacker 二传 0.72，Defender 二传 0.74。
- 对方中路拦网尚未移动。

**首选答案**

```json
{"receiver":"setter","second_actor":"defender","set_route":"right_pin","third_actor":"attacker","attack_route":"line"}
```

**可接受答案**

- Setter 接球后由 Defender 临时组织 `left_pin` 或 `right_pin`，Attacker 完成对应
  线路的进攻。
- Attacker 接球、Setter 组织、Defender 第三次触球，仅在答案选择 Defender 可达的
  另一侧进攻点时可接受。

**禁止答案**

- 让 Defender 跨越 3.8 米抢短球。
- Setter 接一传后仍安排 Setter 自己完成第二次触球。

**评分理由**

本题首先考察可达性，其次考察二传接一传后的替代组织意识。模型虽只能输出三项
回合计划，但应通过边翼路线体现角色调整，而不是机械坚持固定接传扣顺序。

### R03 对方防守明显偏左

**局面**

- 比分 8:8，蓝队接发，触球数 0。
- 深区中路来球，Defender 可稳定到位。
- 对方后排两人整体向蓝队进攻视角的左侧移动，右侧边线和右后区空出。
- 双方球员体能正常。

**首选答案**

```json
{"receiver":"defender","second_actor":"setter","set_route":"right_pin","third_actor":"attacker","attack_route":"line"}
```

**可接受答案**

- Defender、Setter、Attacker 保持标准分工，`back_set` 配合 `line`。
- Defender、Setter、Attacker 保持标准分工，`middle_quick` 配合 `deep_seam`，
  利用防守移动产生的结合部。

**禁止答案**

- `left_pin` 配合向左侧防守密集区的进攻线路，且没有任何风险收益理由。

**评分理由**

应直接利用右侧空当；右侧拉开后的直线是空间最清晰的选择。

### R04 对方防守明显偏右

**局面**

- 比分 5:6，蓝队接发，触球数 0。
- Defender 能完成到位一传。
- 对方后排和边网拦网整体偏向蓝队进攻视角的右侧，左后区空出。
- 蓝队 Attacker 左侧进攻适应性正常。

**首选答案**

```json
{"receiver":"defender","second_actor":"setter","set_route":"left_pin","third_actor":"attacker","attack_route":"line"}
```

**可接受答案**

- `middle_quick` 配合 `cross_court` 或 `deep_seam`，打向移动后的空当。

**禁止答案**

- 无视明显偏移，继续选择 `right_pin` 加右侧密集区线路。

**评分理由**

这是 R03 的镜像题，用于检查模型是否真正读取方向，而不是固定输出右侧进攻。

### R05 中路拦网提前移动

**局面**

- 比分 7:7，蓝队接发，触球数 0。
- 一传预计到位。
- 对方中路拦网在发球前已向 `middle_front` 压近，准备跟随快攻；两侧边网只有
  单人拦网条件。
- 蓝队 Attacker 边翼能力 0.93，弹跳 0.94。

**首选答案**

```json
{"receiver":"defender","second_actor":"setter","set_route":"back_set","third_actor":"attacker","attack_route":"cross_court"}
```

**可接受答案**

- 任一边翼拉开配合 `line` 或 `cross_court`。
- `middle_quick` 只有在选择 `roll_shot` 明确利用拦网身后空当时可接受。

**禁止答案**

- 机械选择中间快攻并强打中路拦网正面。

**评分理由**

对方提前承诺中路后，应把球分到边翼形成单人拦网；背传增加隐蔽性。

### R06 主攻疲劳

**局面**

- 比分 10:9，蓝队接发，触球数 0。
- Defender 和 Setter 状态正常。
- Attacker 疲劳 85%，本局连续三次强攻；弹跳临时下降 25%，扣球失误风险上升。
- 对方站位均衡，中前区吊球保护较弱。

**首选答案**

```json
{"receiver":"defender","second_actor":"setter","set_route":"back_set","third_actor":"attacker","attack_route":"roll_shot"}
```

**可接受答案**

- 高球到任一边翼配合 `roll_shot`。
- `middle_quick` 配合 `deep_seam`，前提是风险保持均衡而非追求最大力量。

**禁止答案**

- 继续选择要求最大助跑和弹跳的高风险强攻线路。

**评分理由**

回合级格式没有显式风险字段，因此应通过更长节奏的路线和轻拍线路表现负荷管理。

### R07 一传手状态较差

**局面**

- 比分 4:7，蓝队接发，触球数 0。
- 来球落向 `left_back`，Defender 距离 1.4 米但一传状态降至 0.52、连续两次失误。
- Attacker 距离 1.1 米，一传能力 0.72；Setter 距离 2.6 米且需要保留组织。
- Attacker 接球后无法在第三次触球前赶到前排进攻点，Defender 可以从后排进入
  `middle_front` 完成第三次触球。
- 对方中路防守较空。

**首选答案**

```json
{"receiver":"attacker","second_actor":"setter","set_route":"middle_quick","third_actor":"defender","attack_route":"deep_seam"}
```

**可接受答案**

- Defender 接球、Setter 组织、Attacker 进攻，但必须选择较安全的高弧边翼组织和
  `roll_shot`。

**禁止答案**

- Setter 跨越更远距离接球，导致无人可靠组织。
- Defender 接球后仍安排最快、容错最低的组合。
- Attacker 接球后仍安排 Attacker 自己完成第三次触球。

**评分理由**

较近且状态更好的 Attacker 可以接管一传；此后 Setter 组织，Defender 临时完成
第三次触球。答案重点是识别状态、可达性和三人换位，而非固守角色。

### R08 13:14 落后的赛点压力

**局面**

- 比分 13:14，蓝队接发，对方赛点，触球数 0。
- Defender 可完成到位一传。
- 对方整体防守均衡，但左侧边网拦网身高较弱。
- 蓝队左侧进攻成功率 68%，中路快攻成功率 54%、直接失误率更高。

**首选答案**

```json
{"receiver":"defender","second_actor":"setter","set_route":"left_pin","third_actor":"attacker","attack_route":"deep_seam"}
```

**可接受答案**

- `left_pin` 配合 `line` 或 `roll_shot`。
- `back_set` 配合 `deep_seam`，如果模型选择避开已被观察的左侧。

**禁止答案**

- 在明确高失误率下选择中路最快节奏并追求极限线路。

**评分理由**

赛点落后应优先高成功率弱拦网点，同时保持主动得分能力，不能只送无威胁安全球。

### R09 14:14 平分后的风险控制

**局面**

- 比分 14:14，必须领先两分，蓝队接发，触球数 0。
- 一传预计到位，双方无明显体能问题。
- 对方右后防守连续提前保护直线，右侧斜线与中后结合部出现空当。
- 蓝队 Attacker 的斜线成功率高于直线。

**首选答案**

```json
{"receiver":"defender","second_actor":"setter","set_route":"right_pin","third_actor":"attacker","attack_route":"cross_court"}
```

**可接受答案**

- `right_pin` 配合 `deep_seam`。
- `middle_quick` 配合 `deep_seam`。

**禁止答案**

- 继续打已被提前保护的右侧直线。
- 仅因比分紧张选择完全无进攻性的处理。

**评分理由**

平分后既要控制直接失误，也要针对已暴露的防守倾向保持主动进攻。

### R10 轮转后角色与站位错位

**局面**

- 比分 11:10，蓝队刚由接发得分并获得发球权，随后对方得分；蓝队现在接发，
  触球数 0。
- 轮转后 Setter 位于 `right_back`，Defender 位于 `middle_front`，Attacker 位于
  `left_back`。
- 发球落向 `left_back`，Attacker 距离 0.8 米；Defender 距离 3.2 米。
- Setter 可从右后快速进入中前组织区。

**首选答案**

```json
{"receiver":"attacker","second_actor":"setter","set_route":"middle_quick","third_actor":"defender","attack_route":"deep_seam"}
```

**可接受答案**

- Attacker 接球，Setter 组织任一 Defender 可达的进攻点，Defender 临时承担进攻。
- Setter 接球、Defender 组织、Attacker 第三次触球，仅在选择高弧边翼球时可接受。

**禁止答案**

- 让 Defender 仅因角色名称而跨越 3.2 米抢接。

**评分理由**

轮转改变实际站位，选人必须先服从可达性；角色分工只能作为第二优先级。

## 每次触球决策案例

### T01 高质量一传后的快速组织

**局面**

- 比分 6:6，蓝队本回合已触球 1 次。
- Defender 刚完成高质量一传，球以低速到达本方 `middle_front` 上方 2.4 米。
- Setter 距球 0.5 米且身体平衡；Attacker 已在 `left_front` 完成助跑准备。
- 对方中路拦网起步偏慢。

**首选答案**

```json
{"next_actor":"setter","action":"set","target_zone":"left_front","tempo":"quick","risk":"balanced"}
```

**可接受答案**

- Setter 快传 `middle_front`，由 Attacker 内切完成快攻。
- Setter 正常节奏传 `left_front`，风险为 `safe` 或 `balanced`。

**禁止答案**

- Defender 连续承担第二次触球而让到位的 Setter 失去组织机会。
- 第二次触球直接选择无必要的 `free_ball`。

**评分理由**

到位一传、二传平衡且攻击手准备完成，应利用快节奏形成时间优势。

### T02 一传偏网后的安全处理

**局面**

- 比分 2:3，蓝队已触球 1 次。
- 一传过冲到 `middle_front`，距离球网 0.25 米，球高 2.1 米并继续向网移动。
- Setter 距球 0.7 米可到达，但正面二传空间不足；Attacker 尚未完成助跑。
- 对方拦网已经贴近球网。

**首选答案**

```json
{"next_actor":"setter","action":"set","target_zone":"left_back","tempo":"high","risk":"safe"}
```

**可接受答案**

- Setter 高球调整到 `right_back` 或边翼后区，风险 `safe`。
- 若题面物理预测显示球将越网，可选择 Setter 的 `emergency_save` 到本方后区。

**禁止答案**

- `quick` 近网强行组织。
- 第二次触球直接 `attack` 对抗已经到位的拦网。

**评分理由**

偏网球首先要降低被拦死和触网风险，高弧调整为第三次触球创造空间。

### T03 二传被迫完成第一次触球

**局面**

- 比分 5:5，蓝队已触球 1 次。
- Setter 刚在 `right_back` 完成救球式一传，球飞向 `middle_front`，高 2.8 米。
- Setter 尚未恢复、明确不能连续处理下一球。
- Defender 距目标 1.0 米，二传能力 0.74；Attacker 已在 `left_front` 准备。

**首选答案**

```json
{"next_actor":"defender","action":"set","target_zone":"left_front","tempo":"high","risk":"safe"}
```

**可接受答案**

- Defender 正常节奏传向 `left_front`，风险 `balanced`。
- Attacker 二传给已恢复的 Setter 进攻，仅在 Setter 可合法完成第三次过网时可接受。

**禁止答案**

- 再次选择题面明确不能处理的 Setter。
- 第二次触球使用 `receive` 但目标不指向任何可第三次触球的队友。

**评分理由**

考察二传接一传后的临时组织者选择，以及对“下一次触球可达性”的读取。

### T04 主攻完成第一次触球后的替代组织

**局面**

- 比分 9:8，蓝队已触球 1 次。
- Attacker 刚在 `left_back` 完成一传，球到 `middle_front`，质量良好。
- Setter 位于球下且状态正常；Defender 已从右后移动到 `right_front`，具备中等
  进攻能力。
- Attacker 距离进攻点过远，无法完成本回合第三次触球。

**首选答案**

```json
{"next_actor":"setter","action":"set","target_zone":"right_front","tempo":"normal","risk":"balanced"}
```

**可接受答案**

- Setter 高球传 `right_front`，风险 `safe`。
- Setter 在对方无人封网且球高合适时二次 `attack` 到 `middle_back`，风险
  `aggressive`。

**禁止答案**

- 继续把球传给明确无法赶到的 Attacker。

**评分理由**

模型应根据本回合真实参与情况重分配终结者，而不是固守 Attacker 标签。

### T05 第二次触球离网过远

**局面**

- 比分 7:9，蓝队已触球 2 次。
- 第二次触球失控，球位于本方 `middle_back`、高 1.6 米并继续下降。
- Defender 距球 0.9 米，面向球网；Setter 和 Attacker 都超过 2.8 米。
- 这是蓝队最后一次合法触球机会。

**首选答案**

```json
{"next_actor":"defender","action":"free_ball","target_zone":"right_back","tempo":"high","risk":"safe"}
```

**可接受答案**

- Defender `free_ball` 到任一对方后区，`tempo` 为 `high` 或 `normal`，风险
  `safe`。

**禁止答案**

- 选择 `receive`、`set` 或 `emergency_save`，没有过网意图。
- 选择距离超过 2.8 米的球员。

**评分理由**

第三次触球且离网过远，首要目标是合法、可控地把球送入对方后场。

### T06 球触网后继续比赛

**局面**

- 比分 4:4，蓝队已触球 1 次。
- 蓝队一传擦网后回落到本方 `right_front`，没有越过标志杆外，也没有落地。
- 球高 1.9 米、下降较快；Setter 距球 1.2 米，Attacker 距球 0.6 米。
- Attacker 二传能力 0.72，Setter 可在下一拍恢复进攻位置。

**首选答案**

```json
{"next_actor":"attacker","action":"set","target_zone":"middle_front","tempo":"high","risk":"safe"}
```

**可接受答案**

- Attacker `emergency_save` 到本方 `middle_front` 或 `middle_back`，让 Setter 完成
  第三次过网处理。
- Setter 能在球落地前到达时，选择 Setter 高弧调整也可接受。

**禁止答案**

- 因为球碰网就判定回合结束或直接放弃。
- 将第二次触球目标设为本方无人可达区域。

**评分理由**

碰网本身不终止回合；应优先选择最近球员续球，并为最后一次触球保留合法处理人。

### T07 拦网反弹形成近网乱球

**局面**

- 比分 11:11，蓝队刚完成一次拦网触球；该触球不计入本队三次触球，因此当前
  已计数触球为 0，仍有完整三次触球。
- 球反弹到本方 `left_front`，高 1.3 米，距网 0.4 米。
- 完成拦网的 Attacker 刚落地，距球 0.5 米但身体失衡；Setter 距球 0.9 米且
  面向边线；Defender 距离 2.4 米。
- 对方拦网仍停留在左侧。

**首选答案**

```json
{"next_actor":"attacker","action":"emergency_save","target_zone":"middle_front","tempo":"high","risk":"safe"}
```

**可接受答案**

- Setter `receive` 到 `middle_front`，`tempo` 为 `high`、风险 `safe`，随后由其他
  球员完成第二次触球组织。
- Setter 直接 `set` 到 `right_front`，`tempo` 为 `high`、风险 `safe`，以本队
  第一次计数触球直接组织进攻。

**禁止答案**

- 让 2.4 米外的 Defender 抢球。
- 在身体失衡且对方左侧拦网未退时强行选择 Attacker 近网 `attack`。
- 把拦网计为第一次触球，并错误地认为本队只剩两次触球。
- 在仍有完整三次触球时，无紧急原因直接选择 `free_ball` 放弃组织。

**评分理由**

拦网不消耗三次触球，且拦网人可以立即再次触球，因此首选由最近的拦网人先救起
乱球，再由 Setter 完成第二次计数触球组织，并保留第三次触球进攻。

### T08 后场高速球的紧急救球

**局面**

- 比分 12:10，蓝队已触球 0 次。
- 对方扣球以 21 m/s 飞向本方 `right_back`，高 0.55 米，预计 0.28 秒落地。
- Defender 距预测点 0.9 米、反应 0.93；Attacker 距离 1.7 米；Setter 距离
  2.5 米。
- Defender 只能用单手或倒地动作触球。

**首选答案**

```json
{"next_actor":"defender","action":"emergency_save","target_zone":"middle_front","tempo":"high","risk":"safe"}
```

**可接受答案**

- Defender 救到 `middle_back`，为 Setter 留出追球空间。
- Attacker 紧急救球仅在物理可达性预测确认 Defender 被遮挡时可接受。

**禁止答案**

- 选择距离 2.5 米的 Setter。
- 使用 `receive` 并要求精确 `quick` 出球，忽略紧急触球条件。

**评分理由**

高速低球的唯一优先级是避免落地；目标应朝本方可组织区域抬高，而非追求精确快攻。

### T09 已完成两次触球时的强制过网

**局面**

- 比分 14:13，蓝队局点，已触球 2 次。
- 球位于本方 `right_front`，高 2.6 米，Attacker 已到位并完成起跳准备。
- 对方右侧单人拦网封直线，`left_back` 和 `middle_back` 之间有明显空当。
- Setter 和 Defender 不应再组织第四次触球。

**首选答案**

```json
{"next_actor":"attacker","action":"attack","target_zone":"middle_back","tempo":"quick","risk":"balanced"}
```

**可接受答案**

- Attacker `attack` 到 `left_back`，风险 `balanced` 或 `aggressive`。
- Attacker `free_ball` 到 `middle_back` 只有在攻击触球质量预测很低时可接受。

**禁止答案**

- 任何不过网动作。
- 选择 Setter 或 Defender 再次组织。

**评分理由**

第三次触球必须过网；已有良好进攻条件时应避开直线拦网攻击后场空当。

### T10 关键分下的可控高风险进攻

**局面**

- 比分 16:16，必须领先两分，蓝队已触球 2 次。
- 球在本方 `left_front`，高 3.0 米；Attacker 已到位，体能 72%，进攻技术
  0.94、力量 0.92。
- 对方双人拦网封住直线，但 `right_back` 大角和拦网手后方 `middle_front` 有空当。
- 轻拍成功率 61%，大斜线成功率 69%、直接失误率 12%。

**首选答案**

```json
{"next_actor":"attacker","action":"attack","target_zone":"right_back","tempo":"quick","risk":"aggressive"}
```

**可接受答案**

- Attacker `attack` 到 `middle_front`，`tempo` 为 `normal`，风险 `balanced`，采用
  可控轻拍越过拦网。
- Attacker 攻击 `right_back`、风险 `balanced`，体现降低直接失误倾向。

**禁止答案**

- 攻击已被双人拦网封死的直线区域。
- 因比分紧张而选择没有落点目的的安全送球。

**评分理由**

平分关键球需要承担经过计算的进攻风险。大斜线成功率更高，是首选；轻拍拦网后
方是更保守但合理的替代方案。

## 执行协议

每个模型对每个案例运行 3 次，案例顺序使用固定种子打乱。模型名称在人工审核
界面中匿名为 A、B、C。提示词只包含固定语义、当前案例和输出 schema，不包含
答案、可接受答案、禁止答案或评分理由。

一回合决策总超时 2 秒；每次触球决策总超时 500 毫秒。测试工具不得因为超时
中止比赛线程，只记录兜底并继续下一案例。对支持关闭思考的模型必须关闭思考：

- 豆包：`"thinking":{"type":"disabled"}`。
- 千问：`"enable_thinking":false`。
- GPT-5 Chat：使用默认非推理聊天模式，不发送不受支持的 `temperature`。

每次运行保存：模型、案例、原始响应、解析后决策、HTTP 状态、首字和总延迟、
token、是否超时、是否兜底、五项得分和人工备注。API Key 只从安全运行环境注入，
不得写入 Unity 客户端、日志、测试结果或 Git。

## 验收标准

候选模型只有同时满足以下条件，才可进入 Unity 联网增强原型：

- JSON 成功率至少 99%。
- 不出现直接 0 分的规则违规。
- 一回合案例平均分至少 8 分，最低分至少 6 分。
- 每次触球案例平均分至少 8 分，最低分至少 6 分。
- 一回合决策 P95 不超过 2 秒。
- 每次触球决策 P95 不超过 500 毫秒。

若没有模型满足每次触球时限，则第一版只实现一回合异步预取；每次触球决策保留
为离线评估，不进入比赛运行时。这一降级不改变现有本地 AI 的可玩性。
