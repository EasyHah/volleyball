using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public enum CareerApplicationStatus
    {
        Applied = 0,
        Existing = 1,
        OperationConflict = 2,
        InvalidInputOrState = 3,
        VersionConflict = 4,
        NotFound = 5,
        PersistenceFailure = 6
    }

    public sealed class CreateCareerCommand
    {
        private readonly OccurrenceId[] _tryoutOccurrenceIds;
        private readonly ReadOnlyCollection<OccurrenceId> _readOnlyTryoutOccurrenceIds;

        public CreateCareerCommand(
            ProfileId profileId,
            SaveId saveId,
            LineageId lineageId,
            string playerStableId,
            string careerName,
            string playerName,
            int jerseyNumber,
            IEnumerable<OccurrenceId> tryoutOccurrenceIds,
            OperationId operationId,
            long completedAtUtcMs)
        {
            ProfileId = profileId;
            SaveId = saveId;
            LineageId = lineageId;
            PlayerStableId = playerStableId;
            CareerName = careerName;
            PlayerName = playerName;
            JerseyNumber = jerseyNumber;
            _tryoutOccurrenceIds = tryoutOccurrenceIds == null
                ? null
                : new List<OccurrenceId>(tryoutOccurrenceIds).ToArray();
            _readOnlyTryoutOccurrenceIds = _tryoutOccurrenceIds == null
                ? null
                : Array.AsReadOnly(_tryoutOccurrenceIds);
            OperationId = operationId;
            CompletedAtUtcMs = completedAtUtcMs;
        }

        public ProfileId ProfileId { get; }
        public SaveId SaveId { get; }
        public LineageId LineageId { get; }
        public string PlayerStableId { get; }
        public string CareerName { get; }
        public string PlayerName { get; }
        public int JerseyNumber { get; }
        public IReadOnlyList<OccurrenceId> TryoutOccurrenceIds => _readOnlyTryoutOccurrenceIds;
        public OperationId OperationId { get; }
        public long CompletedAtUtcMs { get; }
    }

    public sealed class TryoutEnrollmentIds
    {
        public TryoutEnrollmentIds(
            WeekPlanId weekPlanId,
            SlotActionId matchSlotActionId,
            OccurrenceId matchOccurrenceId)
        {
            WeekPlanId = weekPlanId;
            MatchSlotActionId = matchSlotActionId;
            MatchOccurrenceId = matchOccurrenceId;
        }

        public WeekPlanId WeekPlanId { get; }
        public SlotActionId MatchSlotActionId { get; }
        public OccurrenceId MatchOccurrenceId { get; }
    }

    public sealed class ConfirmTryoutStageCommand
    {
        public ConfirmTryoutStageCommand(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            OperationId operationId,
            long completedAtUtcMs,
            int stageNumber,
            string choiceId,
            TryoutEnrollmentIds enrollmentIds = null)
        {
            ProfileId = profileId;
            SaveId = saveId;
            ExpectedVersionToken = expectedVersionToken;
            OperationId = operationId;
            CompletedAtUtcMs = completedAtUtcMs;
            StageNumber = stageNumber;
            ChoiceId = choiceId;
            EnrollmentIds = enrollmentIds;
        }

        public ProfileId ProfileId { get; }
        public SaveId SaveId { get; }
        public CareerVersionToken ExpectedVersionToken { get; }
        public OperationId OperationId { get; }
        public long CompletedAtUtcMs { get; }
        public int StageNumber { get; }
        public string ChoiceId { get; }
        public TryoutEnrollmentIds EnrollmentIds { get; }
    }

    public sealed class TryoutOutputExplanation
    {
        public TryoutOutputExplanation(
            string reasonId,
            string outputId,
            int baseValue,
            int appliedDelta,
            int finalValue)
        {
            ReasonId = reasonId ?? throw new ArgumentNullException(nameof(reasonId));
            OutputId = outputId ?? throw new ArgumentNullException(nameof(outputId));
            BaseValue = baseValue;
            AppliedDelta = appliedDelta;
            FinalValue = finalValue;
        }

        public string ReasonId { get; }
        public string OutputId { get; }
        public int BaseValue { get; }
        public int AppliedDelta { get; }
        public int FinalValue { get; }
    }

    public sealed class CareerApplicationResult
    {
        private readonly TryoutResolvedOutput[] _resolvedOutputs;
        private readonly TryoutOutputExplanation[] _explanations;
        private readonly ReadOnlyCollection<TryoutResolvedOutput> _readOnlyResolvedOutputs;
        private readonly ReadOnlyCollection<TryoutOutputExplanation> _readOnlyExplanations;

        public CareerApplicationResult(
            CareerApplicationStatus status,
            PersistenceResultKind? persistenceKind,
            CareerSaveSnapshot snapshot,
            OperationReceipt conflictingReceipt,
            IEnumerable<TryoutResolvedOutput> resolvedOutputs,
            IEnumerable<TryoutOutputExplanation> explanations)
        {
            Status = status;
            PersistenceKind = persistenceKind;
            Snapshot = snapshot;
            ConflictingReceipt = conflictingReceipt;
            _resolvedOutputs = CopyOutputs(resolvedOutputs);
            _explanations = explanations == null
                ? Array.Empty<TryoutOutputExplanation>()
                : new List<TryoutOutputExplanation>(explanations).ToArray();
            _readOnlyResolvedOutputs = Array.AsReadOnly(_resolvedOutputs);
            _readOnlyExplanations = Array.AsReadOnly(_explanations);
        }

        public CareerApplicationStatus Status { get; }
        public PersistenceResultKind? PersistenceKind { get; }
        public CareerSaveSnapshot Snapshot { get; }
        public OperationReceipt ConflictingReceipt { get; }
        public IReadOnlyList<TryoutResolvedOutput> ResolvedOutputs => _readOnlyResolvedOutputs;
        public IReadOnlyList<TryoutOutputExplanation> Explanations => _readOnlyExplanations;

        private static TryoutResolvedOutput[] CopyOutputs(
            IEnumerable<TryoutResolvedOutput> outputs)
        {
            if (outputs == null)
            {
                return Array.Empty<TryoutResolvedOutput>();
            }

            var copied = new List<TryoutResolvedOutput>();
            foreach (var output in outputs)
            {
                copied.Add(new TryoutResolvedOutput(output.OutputId, output.Perturbation));
            }

            return copied.ToArray();
        }
    }
}
