using System;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Presentation
{
    /// <summary>
    /// Simplified-Chinese presentation copy for the first career vertical slice.
    /// Stable business identifiers remain outside the save/domain model.
    /// </summary>
    public static class CareerTextTable
    {
        public static string Get(string id)
        {
            switch (id)
            {
                case "app.title": return "排球职业生涯";
                case "app.prototype": return "首个可玩里程碑 · 开发版";
                case "action.back": return "返回";
                case "action.save": return "立即保存";
                case "action.continue": return "继续";
                case "action.confirm": return "确认";
                case "action.create": return "创建";
                case "action.select": return "进入";
                case "action.execute": return "执行下一行动";
                case "action.open_match": return "进入比赛准备";
                case "action.last_summary": return "查看上一场比赛总结";
                case "action.retry_match": return "重试本场比赛";
                case "action.close_notice": return "完成周末结算";
                case "field.profile_name": return "档案名称";
                case "field.career_name": return "生涯名称";
                case "field.player_name": return "球员姓名";
                case "field.jersey_number": return "球衣号码";
                case "field.first_action": return "行动槽 1";
                case "field.second_action": return "行动槽 2";
                case "profile.empty": return "还没有本地档案。创建一个档案即可开始。";
                case "profile.create_title": return "新建本地档案";
                case "career.empty": return "这个档案中还没有职业生涯。";
                case "career.create_title": return "新建职业生涯";
                case "career.list_title": return "已有职业生涯";
                case "career.unavailable": return "该存档当前不可载入";
                case "tryout.result_title": return "大学试训结果";
                case "tryout.result_hint": return "初始结果已经保存。确认后进入第一周。";
                case "tryout.stage_hint": return "选择本阶段的侧重点。选择会写入存档并影响初始能力。";
                case "week.attributes": return "球员八项能力";
                case "week.status": return "当前状态";
                case "week.plan": return "本周安排";
                case "week.plan_hint": return "比赛固定占用第 3 个行动槽；前两个行动由你安排。";
                case "week.plan_confirmed": return "本周计划已确认";
                case "week.milestone_complete": return "第一周闭环已经完成。第二周将在下一里程碑继续扩展。";
                case "week.slot_empty": return "尚未安排";
                case "week.match_slot": return "第 3 槽 · 校队比赛";
                case "event.title": return "临时事件";
                case "event.hint": return "事件会影响球员状态。请选择一种处理方式。";
                case "prematch.title": return "赛前重点";
                case "prematch.hint": return "选择本场比赛的执行重点。比赛结束后会立即进行原子结算。";
                case "prematch.pending_hint": return "本场赛前上下文已经保存。请按原有重点重试，避免产生两次结算。";
                case "prematch.opponent": return "对手";
                case "prematch.home_roster": return "我方阵容";
                case "prematch.away_roster": return "对方阵容";
                case "prematch.preview_unavailable": return "阵容预览暂时不可用，不影响比赛流程。";
                case "summary.title": return "比赛总结";
                case "summary.win": return "比赛结果：胜利";
                case "summary.loss": return "比赛结果：失利";
                case "summary.sets": return "局分";
                case "summary.growth": return "能力成长";
                case "summary.performance": return "个人表现";
                case "summary.priority": return "赛前重点";
                case "summary.priority_executed": return "执行结果：成功落实";
                case "summary.priority_not_executed": return "执行结果：未充分落实";
                case "weekend.title": return "周末结算";
                case "weekend.hint": return "比赛与成长结果已写入存档。下一步将进入第二周。";
                case "status.fatigue": return "疲劳";
                case "status.mindset": return "心态";
                case "status.coach_trust": return "教练信任";
                case "status.potential": return "潜力";
                case "status.unknown": return "待揭示";
                case "save.label": return "存档状态";
                case "save.ready": return "就绪";
                case "save.saving": return "正在保存…";
                case "save.saved": return "已保存";
                case "save.failed": return "保存失败";
                case "save.read_only": return "只读／需要恢复";
                case "feedback.ready": return "请选择或创建本地档案";
                case "feedback.loaded": return "已载入";
                case "feedback.saving": return "正在写入本地存档";
                case "feedback.navigation_only": return "页面已切换，没有新增存档修订";
                case "feedback.back": return "已返回上一层";
                case "feedback.choose_match_priority": return "请选择赛前重点";
                case "feedback.pending_match_retry": return "检测到未完成比赛，请重试";
                case "feedback.operation_in_progress": return "操作仍在进行中";
                case "feedback.week_plan_requires_completion": return "当前周流程尚未完成，不能离开";
                case "feedback.pending_match_requires_retry": return "存在未完成比赛，不能返回";
                case "feedback.summary_requires_confirmation": return "请先确认本页结果";
                case "feedback.match_cancelled": return "比赛已取消，可从赛前页面重试";
                case "feedback.unknown_failure": return "操作未完成，请查看开发诊断";
                case "diagnostics.title": return "开发诊断";
                case "diagnostics.route": return "页面";
                case "diagnostics.feedback": return "反馈码";
                case "diagnostics.revision": return "存档修订";
                case "diagnostics.profile_id": return "档案 ID";
                case "diagnostics.save_id": return "存档 ID";
                default: return id;
            }
        }

        public static string Format(string id, params object[] values)
        {
            switch (id)
            {
                case "profile.greeting": return string.Format("当前档案：{0}", values);
                case "career.card": return string.Format("{0} · 球员 {1}", values);
                case "career.progress": return string.Format("赛季 {0} · 第 {1} 周", values);
                case "tryout.stage_title": return string.Format("大学试训 · 第 {0}/3 阶段", values);
                case "player.identity": return string.Format("{0} · #{1}", values);
                case "week.title": return string.Format("大学第 {0} 赛季 · 第 {1} 周", values);
                case "week.slot": return string.Format("第 {0} 槽 · {1}", values);
                case "attribute.value": return string.Format("{0}　{1}", values);
                case "attribute.growth": return string.Format("{0}　+{1} 成长经验", values);
                case "status.value": return string.Format("{0}　{1}/100", values);
                case "event.name": return string.Format("事件：{0}", values);
                case "summary.set_score": return string.Format("第 {0} 局　{1} : {2}", values);
                case "summary.spike": return string.Format("扣球：{0} 次尝试，{1} 分，{2} 次失误", values);
                case "summary.serve": return string.Format("发球：{0} 次，{1} 个 ACE，{2} 次失误", values);
                case "summary.reception": return string.Format("接发：{0} 次，{1} 次到位，{2} 次失误", values);
                case "summary.change": return string.Format("{0}　{1:+#;-#;0} → {2}", values);
                case "feedback.technical": return string.Format("反馈码：{0}", values);
                case "prematch.team": return string.Format("{0}：{1}", values);
                case "prematch.player": return string.Format("#{0} · {1}{2}", values);
                default: return string.Format(Get(id), values);
            }
        }

        public static string Route(CareerUiRoute route)
        {
            switch (route)
            {
                case CareerUiRoute.ProfileHub: return "本地档案";
                case CareerUiRoute.CareerHub: return "职业生涯";
                case CareerUiRoute.Onboarding: return "大学试训";
                case CareerUiRoute.WeekHome: return "生涯主页";
                case CareerUiRoute.PreMatch: return "比赛准备";
                case CareerUiRoute.MatchSummary: return "比赛总结";
                case CareerUiRoute.WeekendNotice: return "周末结算";
                default: return route.ToString();
            }
        }

        public static string SaveState(CareerUiSaveState state)
        {
            switch (state)
            {
                case CareerUiSaveState.Ready: return Get("save.ready");
                case CareerUiSaveState.Saving: return Get("save.saving");
                case CareerUiSaveState.Saved: return Get("save.saved");
                case CareerUiSaveState.Failed: return Get("save.failed");
                case CareerUiSaveState.ReadOnly: return Get("save.read_only");
                default: return state.ToString();
            }
        }

        public static string Feedback(string code)
        {
            var value = Get("feedback." + code);
            return string.Equals(value, "feedback." + code, StringComparison.Ordinal)
                ? Get("feedback.unknown_failure")
                : value;
        }

        public static string ProfileLoadability(ProfileLoadability loadability)
        {
            switch (loadability)
            {
                case Application.ProfileLoadability.Loadable: return "可载入";
                case Application.ProfileLoadability.RecoveryAvailable: return "可恢复";
                case Application.ProfileLoadability.Missing: return "档案缺失";
                case Application.ProfileLoadability.Corrupt: return "档案损坏";
                case Application.ProfileLoadability.UnsupportedVersion: return "版本不支持";
                case Application.ProfileLoadability.Ambiguous: return "需要人工确认";
                default: return "状态未知";
            }
        }

        public static string Position(CareerMatchPlayerPosition position)
        {
            switch (position)
            {
                case CareerMatchPlayerPosition.Setter: return "二传";
                case CareerMatchPlayerPosition.OutsideHitter: return "主攻";
                case CareerMatchPlayerPosition.MiddleBlocker: return "副攻";
                case CareerMatchPlayerPosition.Opposite: return "接应";
                case CareerMatchPlayerPosition.Libero: return "自由人";
                default: return "未知位置";
            }
        }

        public static string Team(string teamId)
        {
            switch (teamId)
            {
                case "team.university.player": return "大学校队";
                case "team.university.rival": return "大学联赛对手";
                default: return "球队";
            }
        }

        public static string Attribute(CareerAttributeKind kind)
        {
            switch (kind)
            {
                case CareerAttributeKind.Spike: return "扣球";
                case CareerAttributeKind.Serve: return "发球";
                case CareerAttributeKind.Reception: return "接发";
                case CareerAttributeKind.Defense: return "防守";
                case CareerAttributeKind.Block: return "拦网";
                case CareerAttributeKind.Movement: return "移动";
                case CareerAttributeKind.Jump: return "弹跳";
                case CareerAttributeKind.Stamina: return "体能";
                default: return kind.ToString();
            }
        }

        public static string Potential(PotentialGrade? grade)
        {
            return grade.HasValue ? grade.Value.ToString() : Get("status.unknown");
        }

        public static string WeekAction(string contentId)
        {
            switch (contentId)
            {
                case "week_action.specialized.spike": return "专项训练 · 扣球";
                case "week_action.specialized.serve": return "专项训练 · 发球";
                case "week_action.specialized.reception": return "专项训练 · 接发";
                case "week_action.specialized.defense": return "专项训练 · 防守";
                case "week_action.specialized.block": return "专项训练 · 拦网";
                case "week_action.strength.movement": return "力量训练 · 移动";
                case "week_action.strength.jump": return "力量训练 · 弹跳";
                case "week_action.strength.stamina": return "力量训练 · 体能";
                case "week_action.team_practice.standard": return "团队合练";
                case "week_action.rest.standard": return "休息";
                case "schedule.u1w1.match.01": return "校队比赛";
                default: return contentId;
            }
        }

        public static string TryoutChoice(string choiceId)
        {
            switch (choiceId)
            {
                case "tryout.attack.choice.power": return "强化扣球力量";
                case "tryout.attack.choice.serve": return "突出发球";
                case "tryout.attack.choice.approach": return "打磨助跑与起跳";
                case "tryout.reception_defense.choice.first_touch": return "保障第一触球";
                case "tryout.reception_defense.choice.floor_defense": return "投入地面防守";
                case "tryout.reception_defense.choice.net_read": return "强化网前判断";
                case "tryout.scrimmage.choice.endurance": return "稳定体能分配";
                case "tryout.scrimmage.choice.composure": return "保持比赛冷静";
                case "tryout.scrimmage.choice.initiative": return "主动争取关键球";
                default: return choiceId;
            }
        }

        public static string Event(string eventId)
        {
            return eventId == "event.team_meal" ? "球队聚餐邀请" : eventId;
        }

        public static string EventOption(string optionId)
        {
            switch (optionId)
            {
                case "event.team_meal.option.attend": return "参加聚餐，融入球队";
                case "event.team_meal.option.extra_practice": return "婉拒邀请，留下加练";
                default: return optionId;
            }
        }

        public static string MatchPriority(CareerMatchPriority priority)
        {
            switch (priority)
            {
                case CareerMatchPriority.AttackFirst: return "优先进攻";
                case CareerMatchPriority.FirstContactSecurity: return "保障一传";
                case CareerMatchPriority.StaminaControl: return "控制体能";
                default: return priority.ToString();
            }
        }
    }
}
