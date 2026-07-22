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
