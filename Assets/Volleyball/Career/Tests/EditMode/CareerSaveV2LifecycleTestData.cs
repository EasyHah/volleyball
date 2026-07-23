using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.EditModeTests
{
    internal static class CareerSaveV2LifecycleTestData
    {
        private const string FixtureDirectory =
            "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1";

        public static CareerSaveSnapshot AwaitingMatchSnapshot()
        {
            var before = MatchReadyBase();
            var contextBytes = ContextBytes();
            var context = MatchContractV2Json.DeserializeContext(contextBytes);
            var identity = NextIdentity(before, 9);
            var operationId = new OperationId(
                Guid.Parse("99999999-9999-9999-9999-999999999999"));
            var plan = before.Progression.WeekPlan;
            var contextDigest = new Sha256Digest(context.ContextHash);
            var frozen = before.TrainingEmphases.Freeze()
                .Select(item => new FrozenCareerTrainingEmphasis(
                    item.Direction,
                    item.SourceSlotActionIds,
                    item.TotalBonusBasisPoints))
                .ToArray();
            var pending = new PendingCareerMatch(
                context.SessionId,
                operationId,
                identity.LineageId,
                identity.Revision,
                new CareerMatchLifecycleVersions(
                    context.Versions.ContractVersion,
                    context.Versions.ContentVersion,
                    context.Versions.RulesetVersion,
                    context.Versions.CareerRandomAlgorithmVersion,
                    context.Versions.MatchSimulationVersion,
                    context.Versions.MatchRandomAlgorithmVersion),
                CareerMatchLifecycleExecutionMode.Fixture,
                context.FixtureId,
                context.FixtureVersion,
                context.MatchSeed,
                context.CompetitionId,
                context.ScheduleItemId,
                plan.PlanId,
                plan.Slots[2].SlotActionId,
                plan.Slots[2].OccurrenceId,
                CareerMatchPriority.AttackFirst,
                contextDigest,
                contextBytes,
                context.Teams[0].TeamId,
                context.Teams[1].TeamId,
                context.Teams.SelectMany(team => team.Players)
                    .Select(player => player.PlayerId),
                before.Player.PlayerId,
                frozen);
            var receipts = new List<OperationReceipt>(before.OperationReceipts)
            {
                new OperationReceipt(
                    operationId,
                    OperationKind.CreatePendingMatch,
                    OperationReceiptTarget.ForPendingMatch(
                        plan.PlanId,
                        plan.Slots[2].SlotActionId,
                        plan.Slots[2].OccurrenceId,
                        context.SessionId,
                        context.ScheduleItemId,
                        contextDigest),
                    new Sha256Digest(new string('f', 64)),
                    identity.LineageId,
                    identity.Revision,
                    100,
                    OperationOutcomeKind.PendingMatchCreated,
                    OperationOutcomeSummary.ForPendingMatchCreated(
                        context.SessionId,
                        contextDigest))
            };

            return new CareerSaveSnapshot(
                before.Versions,
                identity,
                before.CareerSeed,
                before.CareerName,
                before.PlayerDraft,
                before.Onboarding,
                CareerProgressionState.AwaitingMatch(plan, context.SessionId),
                before.TrainingEmphases,
                before.Player,
                before.TeamId,
                before.PotentialGrade,
                before.Fatigue,
                before.Mindset,
                before.CoachTrust,
                receipts,
                pending,
                Array.Empty<CareerMatchHistoryEntry>(),
                Array.Empty<CareerSettlementReceipt>());
        }

        public static CareerSaveSnapshot MatchReadySnapshot()
        {
            return MatchReadyBase();
        }

        public static CareerSaveSnapshot SettledSnapshot()
        {
            var awaiting = AwaitingMatchSnapshot();
            var contextBytes = awaiting.PendingMatch.CanonicalContextUtf8;
            var resultBytes = ResultBytes();
            var context = MatchContractV2Json.DeserializeContext(contextBytes);
            var result = MatchContractV2Json.DeserializeResult(resultBytes, context);
            var summary = Summary(awaiting, result);
            var identity = NextIdentity(awaiting, 10);
            var resultDigest = new Sha256Digest(result.ResultHash);
            var history = new CareerMatchHistoryEntry(
                awaiting.PendingMatch.SessionId,
                awaiting.PendingMatch.ScheduleItemId,
                awaiting.PendingMatch.SourceWeekPlanId,
                awaiting.PendingMatch.SourceSlotActionId,
                awaiting.PendingMatch.ContextDigest,
                resultDigest,
                contextBytes,
                resultBytes,
                identity.LineageId,
                identity.Revision,
                200,
                summary);
            var receipt = new CareerSettlementReceipt(
                history.SessionId,
                history.ContextDigest,
                history.ResultDigest,
                history.AppliedLineageId,
                history.AppliedRevision,
                history.SettledAtUtcMs,
                summary);
            var weekTwo = new CareerWeekPlanState(
                new WeekPlanId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                1,
                2,
                new CareerWeekActionState[] { null, null, null },
                false);
            var player = new CareerPlayerRecord(
                awaiting.Player.PlayerId,
                awaiting.Player.DisplayName,
                awaiting.Player.JerseyNumber,
                summary.AfterAttributes);

            return new CareerSaveSnapshot(
                awaiting.Versions,
                identity,
                awaiting.CareerSeed,
                awaiting.CareerName,
                awaiting.PlayerDraft,
                awaiting.Onboarding,
                CareerProgressionState.Planning(weekTwo),
                TrainingEmphasisLedger.Empty,
                player,
                awaiting.TeamId,
                awaiting.PotentialGrade,
                summary.WeekendFatigueChange.NewValue,
                summary.WeekendMindsetChange.NewValue,
                summary.WeekendCoachTrustChange.NewValue,
                awaiting.OperationReceipts,
                null,
                new[] { history },
                new[] { receipt });
        }

        public static byte[] ContextBytes()
        {
            return File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                FixtureDirectory,
                "golden-context.json"));
        }

        public static byte[] ResultBytes()
        {
            return File.ReadAllBytes(Path.Combine(
                Directory.GetCurrentDirectory(),
                FixtureDirectory,
                "golden-result.json"));
        }

        private static CareerSaveSnapshot MatchReadyBase()
        {
            var profileId = new ProfileId(
                Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var saveId = new SaveId(
                Guid.Parse("22222222-2222-2222-2222-222222222222"));
            var lineageId = new LineageId(
                Guid.Parse("33333333-3333-3333-3333-333333333333"));
            var draft = new CareerPlayerDraft(
                new PlayerId("player.career.protagonist"),
                "Career Protagonist",
                2);
            var player = new CareerPlayerRecord(
                draft.PlayerId,
                draft.DisplayName,
                draft.JerseyNumber,
                FixtureAttributes());
            var stages = new[]
            {
                ConfirmedStage(1),
                ConfirmedStage(2),
                ConfirmedStage(3)
            };
            var onboarding = new TryoutOnboardingState(stages, 0, true);
            var planId = new WeekPlanId(StableGuid(600));
            var firstAction = new CareerWeekActionState(
                new SlotActionId(StableGuid(610)),
                new OccurrenceId(StableGuid(611)),
                CareerWeekActionKind.SpecializedTraining,
                "week_action.specialized.spike");
            var secondAction = new CareerWeekActionState(
                new SlotActionId(StableGuid(620)),
                new OccurrenceId(StableGuid(621)),
                CareerWeekActionKind.StrengthTraining,
                "week_action.strength.jump");
            var matchAction = new CareerWeekActionState(
                new SlotActionId(StableGuid(630)),
                new OccurrenceId(StableGuid(631)),
                CareerWeekActionKind.Match,
                "schedule.u1w1.match.01");
            var plan = new CareerWeekPlanState(
                planId,
                1,
                1,
                new[] { firstAction, secondAction, matchAction },
                true);
            var receipts = new List<OperationReceipt>
            {
                Receipt(
                    1,
                    OperationKind.CreateCareer,
                    OperationReceiptTarget.ForCreateCareer(),
                    lineageId,
                    OperationOutcomeSummary.ForCareerCreated()),
                Receipt(
                    2,
                    OperationKind.ConfirmTryoutStage,
                    OperationReceiptTarget.ForTryoutStage(
                        1,
                        stages[0].OccurrenceId,
                        stages[0].ChoiceId),
                    lineageId,
                    OperationOutcomeSummary.ForTryoutAdvanced(stages[0].ResolvedOutputs)),
                Receipt(
                    3,
                    OperationKind.ConfirmTryoutStage,
                    OperationReceiptTarget.ForTryoutStage(
                        2,
                        stages[1].OccurrenceId,
                        stages[1].ChoiceId),
                    lineageId,
                    OperationOutcomeSummary.ForTryoutAdvanced(stages[1].ResolvedOutputs)),
                Receipt(
                    4,
                    OperationKind.ConfirmTryoutStage,
                    OperationReceiptTarget.ForTryoutStage(
                        3,
                        stages[2].OccurrenceId,
                        stages[2].ChoiceId),
                    lineageId,
                    OperationOutcomeSummary.ForTryoutAdvanced(stages[2].ResolvedOutputs)),
                Receipt(
                    5,
                    OperationKind.ConfirmWeekPlan,
                    OperationReceiptTarget.ForWeekPlanConfirmation(planId),
                    lineageId,
                    OperationOutcomeSummary.ForWeekPlanConfirmed()),
                Receipt(
                    6,
                    OperationKind.ExecuteWeekAction,
                    OperationReceiptTarget.ForWeekAction(
                        planId,
                        firstAction.SlotActionId,
                        firstAction.OccurrenceId),
                    lineageId,
                    OperationOutcomeSummary.ForSlotCompleted(
                        new CareerAttributeGrowthDelta(120, 0, 0, 0, 0, 0, 0, 0),
                        8,
                        0,
                        0)),
                Receipt(
                    7,
                    OperationKind.ResolveEventChoice,
                    OperationReceiptTarget.ForEventChoice(
                        planId,
                        firstAction.SlotActionId,
                        firstAction.OccurrenceId,
                        new OccurrenceId(StableGuid(640)),
                        "event.team_meal.option.attend"),
                    lineageId,
                    OperationOutcomeSummary.ForEventChoiceApplied(
                        new CareerAttributeGrowthDelta(0, 0, 0, 0, 0, 0, 0, 0),
                        4,
                        6,
                        3)),
                Receipt(
                    8,
                    OperationKind.ExecuteWeekAction,
                    OperationReceiptTarget.ForWeekAction(
                        planId,
                        secondAction.SlotActionId,
                        secondAction.OccurrenceId),
                    lineageId,
                    OperationOutcomeSummary.ForSlotCompleted(
                        new CareerAttributeGrowthDelta(0, 0, 0, 0, 0, 0, 100, 0),
                        12,
                        0,
                        0))
            };
            var emphases = new TrainingEmphasisLedger(new[]
            {
                new TrainingEmphasisContribution(
                    firstAction.SlotActionId,
                    CareerTrainingDirection.Spike,
                    1000),
                new TrainingEmphasisContribution(
                    secondAction.SlotActionId,
                    CareerTrainingDirection.Jump,
                    1000)
            });
            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    profileId,
                    saveId,
                    lineageId,
                    8,
                    0,
                    14,
                    new Sha256Digest(new string('0', 64))),
                new CareerSeed(new byte[CareerSeed.ByteLength]),
                "Lifecycle Golden",
                draft,
                onboarding,
                CareerProgressionState.Planned(plan, 3),
                emphases,
                player,
                new TeamId("team.university.first"),
                PotentialGrade.B,
                12,
                56,
                63,
                receipts);
        }

        private static TryoutStageState ConfirmedStage(int stageNumber)
        {
            return new TryoutStageState(
                stageNumber,
                new OccurrenceId(StableGuid(100 + stageNumber)),
                1,
                "stage-" + stageNumber + "-choice",
                new[]
                {
                    new TryoutResolvedOutput(
                        "stage-" + stageNumber + "-result",
                        stageNumber)
                });
        }

        private static OperationReceipt Receipt(
            int revision,
            OperationKind kind,
            OperationReceiptTarget target,
            LineageId lineageId,
            OperationOutcomeSummary outcome)
        {
            return new OperationReceipt(
                new OperationId(StableGuid(800 + revision)),
                kind,
                target,
                new Sha256Digest(new string("abcdef"[revision % 6], 64)),
                lineageId,
                revision,
                revision,
                outcome.OutcomeKind,
                outcome);
        }

        private static Guid StableGuid(int value)
        {
            return Guid.Parse(
                "00000000-0000-0000-0000-" + value.ToString("D12"));
        }

        private static CareerSettlementSummary Summary(
            CareerSaveSnapshot awaiting,
            MatchResultV2 result)
        {
            var facts = result.PlayerFacts.Single(
                item => item.PlayerId.Equals(awaiting.Player.PlayerId));
            var growth = new CareerAttributeGrowthChange[8];
            for (var index = 0; index < growth.Length; index++)
            {
                var kind = (CareerAttributeKind)index;
                var before = Attribute(awaiting.Player.Attributes, kind);
                growth[index] = new CareerAttributeGrowthChange(
                    kind,
                    "match.growth." + AttributeToken(kind),
                    before,
                    5,
                    5,
                    new CareerAttributeProgress(
                        before.AbilityBasisPoints,
                        before.GrowthExperience + 5));
            }

            var matchFatigue = new CareerReasonedIntegerChange(
                "match.fatigue",
                awaiting.Fatigue.Value,
                1,
                1,
                awaiting.Fatigue.Value + 1);
            var matchMindset = new CareerReasonedIntegerChange(
                "match.mindset",
                awaiting.Mindset.Value,
                2,
                2,
                awaiting.Mindset.Value + 2);
            var matchTrust = new CareerReasonedIntegerChange(
                "match.coach_trust",
                awaiting.CoachTrust.Value,
                3,
                3,
                awaiting.CoachTrust.Value + 3);
            return new CareerSettlementSummary(
                result.Sets.Select(set => new CareerMatchSetScoreSummary(
                    set.SetNumber,
                    set.HomePoints,
                    set.AwayPoints,
                    set.IsComplete)),
                new CareerProtagonistMatchFacts(
                    new CareerSpikeFactSummary(
                        facts.Spike.Attempts,
                        facts.Spike.Points,
                        facts.Spike.Errors),
                    new CareerServeFactSummary(
                        facts.Serve.Attempts,
                        facts.Serve.Aces,
                        facts.Serve.Errors),
                    new CareerReceptionFactSummary(
                        facts.Reception.Attempts,
                        facts.Reception.Perfect,
                        facts.Reception.Positive,
                        facts.Reception.Neutral,
                        facts.Reception.Negative,
                        facts.Reception.Errors),
                    new CareerDefenseFactSummary(
                        facts.Defense.Attempts,
                        facts.Defense.Successes),
                    new CareerBlockFactSummary(
                        facts.Block.Attempts,
                        facts.Block.EffectiveTouches,
                        facts.Block.Points),
                    new CareerMatchLoadFactSummary(
                        facts.Load.RalliesPlayed,
                        facts.Load.ActiveDurationMilliseconds,
                        facts.Load.MovementDistanceMillimeters,
                        facts.Load.JumpCount,
                        facts.Load.HighLoadJumpCount,
                        facts.Load.LandingLoadBasisPoints,
                        facts.Load.TotalWorkloadBasisPoints),
                    new CareerStabilityFactSummary(
                        facts.Stability.CriticalActions,
                        facts.Stability.CriticalSuccesses,
                        facts.Stability.CriticalErrors,
                        facts.Stability.ErrorStreakEpisodes,
                        facts.Stability.LongestErrorStreak)),
                CareerMatchPriority.AttackFirst,
                true,
                result.WinnerTeamId.Value.Equals(awaiting.TeamId.Value),
                growth,
                matchFatigue,
                matchMindset,
                matchTrust,
                ZeroWeekend("weekend.fatigue", matchFatigue.NewValue),
                ZeroWeekend("weekend.mindset", matchMindset.NewValue),
                ZeroWeekend("weekend.coach_trust", matchTrust.NewValue));
        }

        private static CareerReasonedIntegerChange ZeroWeekend(string reason, int value)
        {
            return new CareerReasonedIntegerChange(reason, value, 0, 0, value);
        }

        private static CareerPlayerAttributes FixtureAttributes()
        {
            return new CareerPlayerAttributes(
                new CareerAttributeProgress(7123, 101),
                new CareerAttributeProgress(6234, 202),
                new CareerAttributeProgress(7345, 303),
                new CareerAttributeProgress(6456, 404),
                new CareerAttributeProgress(7567, 505),
                new CareerAttributeProgress(6678, 606),
                new CareerAttributeProgress(7789, 707),
                new CareerAttributeProgress(6890, 808));
        }

        private static CareerAttributeProgress Attribute(
            CareerPlayerAttributes attributes,
            CareerAttributeKind kind)
        {
            switch (kind)
            {
                case CareerAttributeKind.Spike: return attributes.Spike;
                case CareerAttributeKind.Serve: return attributes.Serve;
                case CareerAttributeKind.Reception: return attributes.Reception;
                case CareerAttributeKind.Defense: return attributes.Defense;
                case CareerAttributeKind.Block: return attributes.Block;
                case CareerAttributeKind.Movement: return attributes.Movement;
                case CareerAttributeKind.Jump: return attributes.Jump;
                case CareerAttributeKind.Stamina: return attributes.Stamina;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static string AttributeToken(CareerAttributeKind kind)
        {
            return kind.ToString().ToLowerInvariant();
        }

        private static CareerSaveIdentity NextIdentity(
            CareerSaveSnapshot source,
            long revision)
        {
            return new CareerSaveIdentity(
                source.Identity.ProfileId,
                source.Identity.SaveId,
                source.Identity.LineageId,
                revision,
                source.Identity.CreatedAtUtcMs,
                source.Identity.UpdatedAtUtcMs + 1,
                new Sha256Digest(new string('0', 64)));
        }
    }
}
