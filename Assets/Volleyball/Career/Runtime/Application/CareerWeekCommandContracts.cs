using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public sealed class ConfirmWeekPlanCommand
    {
        public ConfirmWeekPlanCommand(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            OperationId operationId,
            long completedAtUtcMs,
            CareerWeekPlanState candidatePlan)
        {
            ProfileId = profileId;
            SaveId = saveId;
            ExpectedVersionToken = expectedVersionToken;
            OperationId = operationId;
            CompletedAtUtcMs = completedAtUtcMs;
            CandidatePlan = candidatePlan == null
                ? null
                : new CareerWeekPlanState(
                    candidatePlan.PlanId,
                    candidatePlan.Season,
                    candidatePlan.Week,
                    candidatePlan.Slots,
                    candidatePlan.IsConfirmed);
        }

        public ProfileId ProfileId { get; }

        public SaveId SaveId { get; }

        public CareerVersionToken ExpectedVersionToken { get; }

        public OperationId OperationId { get; }

        public long CompletedAtUtcMs { get; }

        public CareerWeekPlanState CandidatePlan { get; }
    }

    public sealed class ExecuteWeekActionCommand
    {
        public ExecuteWeekActionCommand(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            OperationId operationId,
            long completedAtUtcMs,
            WeekPlanId weekPlanId,
            int slotNumber,
            SlotActionId slotActionId,
            OccurrenceId actionOccurrenceId,
            string contentId,
            OccurrenceId? triggeredEventOccurrenceId)
        {
            ProfileId = profileId;
            SaveId = saveId;
            ExpectedVersionToken = expectedVersionToken;
            OperationId = operationId;
            CompletedAtUtcMs = completedAtUtcMs;
            WeekPlanId = weekPlanId;
            SlotNumber = slotNumber;
            SlotActionId = slotActionId;
            ActionOccurrenceId = actionOccurrenceId;
            ContentId = contentId;
            TriggeredEventOccurrenceId = triggeredEventOccurrenceId;
        }

        public ProfileId ProfileId { get; }

        public SaveId SaveId { get; }

        public CareerVersionToken ExpectedVersionToken { get; }

        public OperationId OperationId { get; }

        public long CompletedAtUtcMs { get; }

        public WeekPlanId WeekPlanId { get; }

        public int SlotNumber { get; }

        public SlotActionId SlotActionId { get; }

        public OccurrenceId ActionOccurrenceId { get; }

        public string ContentId { get; }

        public OccurrenceId? TriggeredEventOccurrenceId { get; }
    }

    public sealed class ResolveEventChoiceCommand
    {
        public ResolveEventChoiceCommand(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            OperationId operationId,
            long completedAtUtcMs,
            WeekPlanId weekPlanId,
            SlotActionId sourceSlotActionId,
            OccurrenceId sourceActionOccurrenceId,
            string eventId,
            OccurrenceId eventOccurrenceId,
            string optionId)
        {
            ProfileId = profileId;
            SaveId = saveId;
            ExpectedVersionToken = expectedVersionToken;
            OperationId = operationId;
            CompletedAtUtcMs = completedAtUtcMs;
            WeekPlanId = weekPlanId;
            SourceSlotActionId = sourceSlotActionId;
            SourceActionOccurrenceId = sourceActionOccurrenceId;
            EventId = eventId;
            EventOccurrenceId = eventOccurrenceId;
            OptionId = optionId;
        }

        public ProfileId ProfileId { get; }

        public SaveId SaveId { get; }

        public CareerVersionToken ExpectedVersionToken { get; }

        public OperationId OperationId { get; }

        public long CompletedAtUtcMs { get; }

        public WeekPlanId WeekPlanId { get; }

        public SlotActionId SourceSlotActionId { get; }

        public OccurrenceId SourceActionOccurrenceId { get; }

        public string EventId { get; }

        public OccurrenceId EventOccurrenceId { get; }

        public string OptionId { get; }
    }

    public sealed class CareerWeekCommandResult
    {
        public CareerWeekCommandResult(
            CareerApplicationStatus status,
            PersistenceResultKind? persistenceKind,
            CareerSaveSnapshot snapshot,
            OperationReceipt conflictingReceipt,
            OperationOutcomeSummary outcomeSummary)
        {
            Status = status;
            PersistenceKind = persistenceKind;
            Snapshot = snapshot;
            ConflictingReceipt = conflictingReceipt;
            OutcomeSummary = outcomeSummary;
        }

        public CareerApplicationStatus Status { get; }

        public PersistenceResultKind? PersistenceKind { get; }

        public CareerSaveSnapshot Snapshot { get; }

        public OperationReceipt ConflictingReceipt { get; }

        public OperationOutcomeSummary OutcomeSummary { get; }
    }
}
