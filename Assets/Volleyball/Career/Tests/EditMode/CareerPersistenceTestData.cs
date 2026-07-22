using System;
using System.Collections.Generic;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    internal static class CareerPersistenceTestData
    {
        public static CareerSaveSnapshot CreatedSnapshot(
            ProfileId profileId,
            SaveId saveId,
            LineageId lineageId,
            string careerName = "First Career")
        {
            var draft = new CareerPlayerDraft(
                new PlayerId("career-player-1"),
                "Player One",
                12);
            var onboarding = new TryoutOnboardingState(
                new[]
                {
                    UnconfirmedStage(1),
                    UnconfirmedStage(2),
                    UnconfirmedStage(3)
                },
                1,
                false);
            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    profileId,
                    saveId,
                    lineageId,
                    1,
                    0,
                    1,
                    Hash('0')),
                new CareerSeed(new byte[CareerSeed.ByteLength]),
                careerName,
                draft,
                onboarding,
                CareerProgressionState.Created(),
                TrainingEmphasisLedger.Empty,
                null,
                null,
                null,
                null,
                null,
                null,
                new[]
                {
                    Receipt(
                        OperationKind.CreateCareer,
                        OperationReceiptTarget.ForCreateCareer(),
                        lineageId,
                        1,
                        OperationOutcomeSummary.ForCareerCreated())
                });
        }

        public static CareerSaveSnapshot AfterFirstTryoutStage(CareerSaveSnapshot source)
        {
            var sourceStage = source.Onboarding.Stages[0];
            var output = new TryoutResolvedOutput("stage-1-result", 1);
            var confirmed = new TryoutStageState(
                1,
                sourceStage.OccurrenceId,
                sourceStage.RandomVersion,
                "stage-1-choice",
                new[] { output });
            var onboarding = new TryoutOnboardingState(
                new[]
                {
                    confirmed,
                    source.Onboarding.Stages[1],
                    source.Onboarding.Stages[2]
                },
                2,
                false);
            var receipts = new List<OperationReceipt>(source.OperationReceipts)
            {
                Receipt(
                    OperationKind.ConfirmTryoutStage,
                    OperationReceiptTarget.ForTryoutStage(
                        1,
                        confirmed.OccurrenceId,
                        confirmed.ChoiceId),
                    source.Identity.LineageId,
                    2,
                    OperationOutcomeSummary.ForTryoutAdvanced(confirmed.ResolvedOutputs))
            };
            return new CareerSaveSnapshot(
                source.Versions,
                new CareerSaveIdentity(
                    source.Identity.ProfileId,
                    source.Identity.SaveId,
                    source.Identity.LineageId,
                    2,
                    source.Identity.CreatedAtUtcMs,
                    source.Identity.UpdatedAtUtcMs + 1,
                    source.Identity.SnapshotHash),
                source.CareerSeed,
                source.CareerName,
                source.PlayerDraft,
                onboarding,
                CareerProgressionState.Tryout(2),
                source.TrainingEmphases,
                null,
                null,
                null,
                null,
                null,
                null,
                receipts);
        }

        public static CareerSaveSnapshot PlanningSnapshot(
            ProfileId profileId,
            SaveId saveId,
            LineageId lineageId,
            long revision = 4,
            string careerName = "First Career",
            long updatedAtUtcMs = 10)
        {
            var draft = new CareerPlayerDraft(
                new PlayerId("career-player-1"),
                "Player One",
                12);
            var stages = new[]
            {
                ConfirmedStage(1),
                ConfirmedStage(2),
                ConfirmedStage(3)
            };
            var onboarding = new TryoutOnboardingState(stages, 0, true);
            var plan = new CareerWeekPlanState(
                new WeekPlanId(Guid.NewGuid()),
                1,
                1,
                new CareerWeekActionState[]
                {
                    new CareerWeekActionState(
                        new SlotActionId(Guid.NewGuid()),
                        new OccurrenceId(Guid.NewGuid()),
                        CareerWeekActionKind.SpecializedTraining,
                        "week_action.specialized.spike"),
                    null,
                    new CareerWeekActionState(
                        new SlotActionId(Guid.NewGuid()),
                        new OccurrenceId(Guid.NewGuid()),
                        CareerWeekActionKind.Match,
                        "schedule.u1w1.match.01")
                },
                false);
            var receipts = new List<OperationReceipt>
            {
                Receipt(
                    OperationKind.CreateCareer,
                    OperationReceiptTarget.ForCreateCareer(),
                    lineageId,
                    1,
                    OperationOutcomeSummary.ForCareerCreated())
            };
            for (var index = 0; index < stages.Length; index++)
            {
                var stage = stages[index];
                receipts.Add(
                    Receipt(
                        OperationKind.ConfirmTryoutStage,
                        OperationReceiptTarget.ForTryoutStage(
                            stage.StageNumber,
                            stage.OccurrenceId,
                            stage.ChoiceId),
                        lineageId,
                        index + 2,
                        OperationOutcomeSummary.ForTryoutAdvanced(stage.ResolvedOutputs)));
            }

            var progress = new CareerAttributeProgress(5000, 10);
            var player = new CareerPlayerRecord(
                draft.PlayerId,
                draft.DisplayName,
                draft.JerseyNumber,
                new CareerPlayerAttributes(
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress));
            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    profileId,
                    saveId,
                    lineageId,
                    revision,
                    0,
                    updatedAtUtcMs,
                    Hash('0')),
                new CareerSeed(new byte[CareerSeed.ByteLength]),
                careerName,
                draft,
                onboarding,
                CareerProgressionState.Planning(plan),
                TrainingEmphasisLedger.Empty,
                player,
                new TeamId("university-blue"),
                PotentialGrade.B,
                20,
                50,
                60,
                receipts);
        }

        public static CareerSaveSnapshot NextPlanningSnapshot(
            CareerSaveSnapshot source,
            string careerName = null,
            long? updatedAtUtcMs = null)
        {
            var identity = new CareerSaveIdentity(
                source.Identity.ProfileId,
                source.Identity.SaveId,
                source.Identity.LineageId,
                source.Identity.Revision + 1,
                source.Identity.CreatedAtUtcMs,
                updatedAtUtcMs ?? source.Identity.UpdatedAtUtcMs + 1,
                source.Identity.SnapshotHash,
                source.Identity.RestoredFromVersionToken);
            return new CareerSaveSnapshot(
                source.Versions,
                identity,
                source.CareerSeed,
                careerName ?? source.CareerName,
                source.PlayerDraft,
                source.Onboarding,
                source.Progression,
                source.TrainingEmphases,
                source.Player,
                source.TeamId,
                source.PotentialGrade,
                source.Fatigue,
                source.Mindset,
                source.CoachTrust,
                source.OperationReceipts);
        }

        public static CareerSaveSnapshot PlannedAfterFirstTraining(
            ProfileId profileId,
            SaveId saveId,
            LineageId lineageId)
        {
            var planning = PlanningSnapshot(profileId, saveId, lineageId);
            var plan = new CareerWeekPlanState(
                planning.Progression.WeekPlan.PlanId,
                1,
                1,
                new[]
                {
                    planning.Progression.WeekPlan.Slots[0],
                    new CareerWeekActionState(
                        new SlotActionId(Guid.NewGuid()),
                        new OccurrenceId(Guid.NewGuid()),
                        CareerWeekActionKind.StrengthTraining,
                        "week_action.strength.jump"),
                    planning.Progression.WeekPlan.Slots[2]
                },
                true);
            var receipts = new List<OperationReceipt>(planning.OperationReceipts)
            {
                Receipt(
                    OperationKind.ConfirmWeekPlan,
                    OperationReceiptTarget.ForWeekPlanConfirmation(plan.PlanId),
                    lineageId,
                    5,
                    OperationOutcomeSummary.ForWeekPlanConfirmed()),
                Receipt(
                    OperationKind.ExecuteWeekAction,
                    OperationReceiptTarget.ForWeekAction(
                        plan.PlanId,
                        plan.Slots[0].SlotActionId,
                        plan.Slots[0].OccurrenceId),
                    lineageId,
                    6,
                    OperationOutcomeSummary.ForSlotCompleted(
                        new CareerAttributeGrowthDelta(120, 0, 0, 0, 0, 0, 0, 0),
                        8,
                        0,
                        0)),
                Receipt(
                    OperationKind.ResolveEventChoice,
                    OperationReceiptTarget.ForEventChoice(
                        plan.PlanId,
                        plan.Slots[0].SlotActionId,
                        plan.Slots[0].OccurrenceId,
                        new OccurrenceId(Guid.NewGuid()),
                        "event.team_meal.option.attend"),
                    lineageId,
                    7,
                    OperationOutcomeSummary.ForEventChoiceApplied(
                        new CareerAttributeGrowthDelta(0, 0, 0, 0, 0, 0, 0, 0),
                        4,
                        6,
                        3))
            };
            var emphases = TrainingEmphasisLedger.Empty.AddExecutedTraining(
                plan.Slots[0],
                CareerWeekActionCatalogV1.Create());
            return new CareerSaveSnapshot(
                planning.Versions,
                new CareerSaveIdentity(
                    profileId,
                    saveId,
                    lineageId,
                    7,
                    planning.Identity.CreatedAtUtcMs,
                    planning.Identity.UpdatedAtUtcMs + 3,
                    Hash('0')),
                planning.CareerSeed,
                planning.CareerName,
                planning.PlayerDraft,
                planning.Onboarding,
                CareerProgressionState.Planned(plan, 2),
                emphases,
                planning.Player,
                planning.TeamId,
                planning.PotentialGrade,
                planning.Fatigue,
                planning.Mindset,
                planning.CoachTrust,
                receipts);
        }

        public static CareerSaveSnapshot AfterSecondTraining(CareerSaveSnapshot source)
        {
            var plan = source.Progression.WeekPlan;
            var receipts = new List<OperationReceipt>(source.OperationReceipts)
            {
                Receipt(
                    OperationKind.ExecuteWeekAction,
                    OperationReceiptTarget.ForWeekAction(
                        plan.PlanId,
                        plan.Slots[1].SlotActionId,
                        plan.Slots[1].OccurrenceId),
                    source.Identity.LineageId,
                    source.Identity.Revision + 1,
                    OperationOutcomeSummary.ForSlotCompleted(
                        new CareerAttributeGrowthDelta(0, 0, 0, 0, 0, 0, 100, 0),
                        12,
                        0,
                        0))
            };
            var emphases = source.TrainingEmphases.AddExecutedTraining(
                plan.Slots[1],
                CareerWeekActionCatalogV1.Create());
            return new CareerSaveSnapshot(
                source.Versions,
                new CareerSaveIdentity(
                    source.Identity.ProfileId,
                    source.Identity.SaveId,
                    source.Identity.LineageId,
                    source.Identity.Revision + 1,
                    source.Identity.CreatedAtUtcMs,
                    source.Identity.UpdatedAtUtcMs + 1,
                    Hash('0')),
                source.CareerSeed,
                source.CareerName,
                source.PlayerDraft,
                source.Onboarding,
                CareerProgressionState.Planned(plan, 3),
                emphases,
                source.Player,
                source.TeamId,
                source.PotentialGrade,
                source.Fatigue,
                source.Mindset,
                source.CoachTrust,
                receipts);
        }

        public static LocalPlayerProfile Profile(
            ProfileId profileId,
            long revision = 1,
            params CareerIndexEntry[] entries)
        {
            return new LocalPlayerProfile(
                LocalPlayerProfile.CurrentSchemaVersion,
                profileId,
                revision,
                Hash('0'),
                "Local Player",
                0,
                10 + revision,
                entries ?? Array.Empty<CareerIndexEntry>());
        }

        public static Sha256Digest Hash(char value)
        {
            return new Sha256Digest(new string(value, 64));
        }

        private static TryoutStageState ConfirmedStage(int stageNumber)
        {
            return new TryoutStageState(
                stageNumber,
                new OccurrenceId(Guid.NewGuid()),
                CareerSaveVersions.Current.CareerRandomAlgorithmVersion,
                "stage-" + stageNumber + "-choice",
                new[]
                {
                    new TryoutResolvedOutput("stage-" + stageNumber + "-result", stageNumber)
                });
        }

        private static TryoutStageState UnconfirmedStage(int stageNumber)
        {
            return new TryoutStageState(
                stageNumber,
                new OccurrenceId(Guid.NewGuid()),
                CareerSaveVersions.Current.CareerRandomAlgorithmVersion,
                null,
                Array.Empty<TryoutResolvedOutput>());
        }

        private static OperationReceipt Receipt(
            OperationKind operationKind,
            OperationReceiptTarget target,
            LineageId lineageId,
            long revision,
            OperationOutcomeSummary summary)
        {
            return new OperationReceipt(
                new OperationId(Guid.NewGuid()),
                operationKind,
                target,
                Hash((char)('a' + (revision % 6))),
                lineageId,
                revision,
                revision,
                summary.OutcomeKind,
                summary);
        }
    }
}
