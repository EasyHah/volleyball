using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Career.Domain
{
    public enum OperationKind
    {
        CreateCareer = 0,
        ConfirmTryoutStage = 1,
        ConfirmWeekPlan = 2,
        ExecuteWeekAction = 3,
        ResolveEventChoice = 4
    }

    public enum OperationOutcomeKind
    {
        CareerCreated = 0,
        TryoutAdvanced = 1,
        WeekPlanConfirmed = 2,
        SlotCompleted = 3,
        EventChoiceApplied = 4
    }

    public sealed class OperationOutcomeSummary
    {
        private readonly TryoutResolvedOutput[] _tryoutResolvedOutputs;
        private readonly ReadOnlyCollection<TryoutResolvedOutput> _readOnlyTryoutResolvedOutputs;

        private OperationOutcomeSummary(
            OperationOutcomeKind outcomeKind,
            IEnumerable<TryoutResolvedOutput> tryoutResolvedOutputs,
            CareerAttributeGrowthDelta growthExperienceDelta,
            int? fatigueDelta,
            int? mindsetDelta,
            int? coachTrustDelta)
        {
            CareerSaveModelGuard.DefinedEnum(outcomeKind, nameof(outcomeKind));
            if (tryoutResolvedOutputs == null)
            {
                throw new ArgumentNullException(nameof(tryoutResolvedOutputs));
            }

            var copiedOutputs = new List<TryoutResolvedOutput>();
            foreach (var output in tryoutResolvedOutputs)
            {
                if (output == null)
                {
                    throw new ArgumentException(
                        "Outcome tryout outputs cannot contain null.",
                        nameof(tryoutResolvedOutputs));
                }

                copiedOutputs.Add(output.Copy());
            }

            switch (outcomeKind)
            {
                case OperationOutcomeKind.CareerCreated:
                case OperationOutcomeKind.WeekPlanConfirmed:
                    if (copiedOutputs.Count != 0 || growthExperienceDelta != null ||
                        fatigueDelta.HasValue || mindsetDelta.HasValue || coachTrustDelta.HasValue)
                    {
                        throw new ArgumentException(
                            "This outcome requires an empty summary.",
                            nameof(outcomeKind));
                    }

                    break;

                case OperationOutcomeKind.TryoutAdvanced:
                    if (copiedOutputs.Count == 0 || growthExperienceDelta != null ||
                        fatigueDelta.HasValue || mindsetDelta.HasValue || coachTrustDelta.HasValue)
                    {
                        throw new ArgumentException(
                            "A tryout outcome requires only a non-empty ordered output list.",
                            nameof(outcomeKind));
                    }

                    break;

                case OperationOutcomeKind.SlotCompleted:
                case OperationOutcomeKind.EventChoiceApplied:
                    if (copiedOutputs.Count != 0 || growthExperienceDelta == null ||
                        !fatigueDelta.HasValue || !mindsetDelta.HasValue ||
                        !coachTrustDelta.HasValue)
                    {
                        throw new ArgumentException(
                            "Action and event outcomes require complete applied deltas.",
                            nameof(outcomeKind));
                    }

                    CareerSaveModelGuard.InclusiveRange(
                        fatigueDelta.Value,
                        -100,
                        100,
                        nameof(fatigueDelta));
                    CareerSaveModelGuard.InclusiveRange(
                        mindsetDelta.Value,
                        -100,
                        100,
                        nameof(mindsetDelta));
                    CareerSaveModelGuard.InclusiveRange(
                        coachTrustDelta.Value,
                        -100,
                        100,
                        nameof(coachTrustDelta));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(outcomeKind),
                        outcomeKind,
                        "Unknown operation outcome kind.");
            }

            OutcomeKind = outcomeKind;
            _tryoutResolvedOutputs = copiedOutputs.ToArray();
            _readOnlyTryoutResolvedOutputs = Array.AsReadOnly(_tryoutResolvedOutputs);
            GrowthExperienceDelta = growthExperienceDelta?.Copy();
            FatigueDelta = fatigueDelta;
            MindsetDelta = mindsetDelta;
            CoachTrustDelta = coachTrustDelta;
        }

        public OperationOutcomeKind OutcomeKind { get; }

        public IReadOnlyList<TryoutResolvedOutput> TryoutResolvedOutputs =>
            _readOnlyTryoutResolvedOutputs;

        public CareerAttributeGrowthDelta GrowthExperienceDelta { get; }

        public int? FatigueDelta { get; }

        public int? MindsetDelta { get; }

        public int? CoachTrustDelta { get; }

        public static OperationOutcomeSummary ForCareerCreated()
        {
            return Empty(OperationOutcomeKind.CareerCreated);
        }

        public static OperationOutcomeSummary ForWeekPlanConfirmed()
        {
            return Empty(OperationOutcomeKind.WeekPlanConfirmed);
        }

        public static OperationOutcomeSummary ForTryoutAdvanced(
            IEnumerable<TryoutResolvedOutput> resolvedOutputs)
        {
            return new OperationOutcomeSummary(
                OperationOutcomeKind.TryoutAdvanced,
                resolvedOutputs,
                null,
                null,
                null,
                null);
        }

        public static OperationOutcomeSummary ForSlotCompleted(
            CareerAttributeGrowthDelta growthExperienceDelta,
            int fatigueDelta,
            int mindsetDelta,
            int coachTrustDelta)
        {
            return ForAppliedDeltas(
                OperationOutcomeKind.SlotCompleted,
                growthExperienceDelta,
                fatigueDelta,
                mindsetDelta,
                coachTrustDelta);
        }

        public static OperationOutcomeSummary ForEventChoiceApplied(
            CareerAttributeGrowthDelta growthExperienceDelta,
            int fatigueDelta,
            int mindsetDelta,
            int coachTrustDelta)
        {
            return ForAppliedDeltas(
                OperationOutcomeKind.EventChoiceApplied,
                growthExperienceDelta,
                fatigueDelta,
                mindsetDelta,
                coachTrustDelta);
        }

        internal OperationOutcomeSummary Copy()
        {
            return new OperationOutcomeSummary(
                OutcomeKind,
                _tryoutResolvedOutputs,
                GrowthExperienceDelta,
                FatigueDelta,
                MindsetDelta,
                CoachTrustDelta);
        }

        private static OperationOutcomeSummary Empty(OperationOutcomeKind outcomeKind)
        {
            return new OperationOutcomeSummary(
                outcomeKind,
                Array.Empty<TryoutResolvedOutput>(),
                null,
                null,
                null,
                null);
        }

        private static OperationOutcomeSummary ForAppliedDeltas(
            OperationOutcomeKind outcomeKind,
            CareerAttributeGrowthDelta growthExperienceDelta,
            int fatigueDelta,
            int mindsetDelta,
            int coachTrustDelta)
        {
            if (growthExperienceDelta == null)
            {
                throw new ArgumentNullException(nameof(growthExperienceDelta));
            }

            return new OperationOutcomeSummary(
                outcomeKind,
                Array.Empty<TryoutResolvedOutput>(),
                growthExperienceDelta,
                fatigueDelta,
                mindsetDelta,
                coachTrustDelta);
        }
    }

    public sealed class OperationReceiptTarget
    {
        private OperationReceiptTarget(
            OperationKind operationKind,
            int tryoutStage,
            OccurrenceId? tryoutOccurrenceId,
            string choiceId,
            WeekPlanId? weekPlanId,
            SlotActionId? slotActionId,
            OccurrenceId? actionOccurrenceId,
            OccurrenceId? eventOccurrenceId,
            string optionId)
        {
            OperationKind = operationKind;
            TryoutStage = tryoutStage;
            TryoutOccurrenceId = tryoutOccurrenceId;
            ChoiceId = choiceId;
            WeekPlanId = weekPlanId;
            SlotActionId = slotActionId;
            ActionOccurrenceId = actionOccurrenceId;
            EventOccurrenceId = eventOccurrenceId;
            OptionId = optionId;
        }

        public OperationKind OperationKind { get; }

        public int TryoutStage { get; }

        public OccurrenceId? TryoutOccurrenceId { get; }

        public string ChoiceId { get; }

        public WeekPlanId? WeekPlanId { get; }

        public SlotActionId? SlotActionId { get; }

        public OccurrenceId? ActionOccurrenceId { get; }

        public OccurrenceId? EventOccurrenceId { get; }

        public string OptionId { get; }

        public static OperationReceiptTarget ForCreateCareer()
        {
            return new OperationReceiptTarget(
                OperationKind.CreateCareer,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        public static OperationReceiptTarget ForTryoutStage(
            int stage,
            OccurrenceId occurrenceId,
            string choiceId)
        {
            CareerSaveModelGuard.InclusiveRange(stage, 1, 3, nameof(stage));
            CareerSaveModelGuard.StableId(occurrenceId.Value, nameof(occurrenceId));
            return new OperationReceiptTarget(
                OperationKind.ConfirmTryoutStage,
                stage,
                occurrenceId,
                CareerSaveModelGuard.BusinessId(choiceId, nameof(choiceId)),
                null,
                null,
                null,
                null,
                null);
        }

        public static OperationReceiptTarget ForWeekPlanConfirmation(WeekPlanId weekPlanId)
        {
            CareerSaveModelGuard.StableId(weekPlanId.Value, nameof(weekPlanId));
            return new OperationReceiptTarget(
                OperationKind.ConfirmWeekPlan,
                0,
                null,
                null,
                weekPlanId,
                null,
                null,
                null,
                null);
        }

        public static OperationReceiptTarget ForWeekAction(
            WeekPlanId weekPlanId,
            SlotActionId slotActionId,
            OccurrenceId actionOccurrenceId)
        {
            CareerSaveModelGuard.StableId(weekPlanId.Value, nameof(weekPlanId));
            CareerSaveModelGuard.StableId(slotActionId.Value, nameof(slotActionId));
            CareerSaveModelGuard.StableId(
                actionOccurrenceId.Value,
                nameof(actionOccurrenceId));
            return new OperationReceiptTarget(
                OperationKind.ExecuteWeekAction,
                0,
                null,
                null,
                weekPlanId,
                slotActionId,
                actionOccurrenceId,
                null,
                null);
        }

        public static OperationReceiptTarget ForEventChoice(
            WeekPlanId weekPlanId,
            SlotActionId sourceSlotActionId,
            OccurrenceId sourceActionOccurrenceId,
            OccurrenceId eventOccurrenceId,
            string optionId)
        {
            CareerSaveModelGuard.StableId(weekPlanId.Value, nameof(weekPlanId));
            CareerSaveModelGuard.StableId(
                sourceSlotActionId.Value,
                nameof(sourceSlotActionId));
            CareerSaveModelGuard.StableId(
                sourceActionOccurrenceId.Value,
                nameof(sourceActionOccurrenceId));
            CareerSaveModelGuard.StableId(
                eventOccurrenceId.Value,
                nameof(eventOccurrenceId));
            return new OperationReceiptTarget(
                OperationKind.ResolveEventChoice,
                0,
                null,
                null,
                weekPlanId,
                sourceSlotActionId,
                sourceActionOccurrenceId,
                eventOccurrenceId,
                CareerSaveModelGuard.BusinessId(optionId, nameof(optionId)));
        }

        internal OperationReceiptTarget Copy()
        {
            return new OperationReceiptTarget(
                OperationKind,
                TryoutStage,
                TryoutOccurrenceId,
                ChoiceId,
                WeekPlanId,
                SlotActionId,
                ActionOccurrenceId,
                EventOccurrenceId,
                OptionId);
        }
    }

    public sealed class OperationReceipt
    {
        public OperationReceipt(
            OperationId operationId,
            OperationKind operationKind,
            OperationReceiptTarget target,
            Sha256Digest inputFingerprint,
            LineageId appliedLineageId,
            long appliedRevision,
            long completedAtUtcMs,
            OperationOutcomeKind outcomeKind,
            OperationOutcomeSummary outcomeSummary)
        {
            CareerSaveModelGuard.StableId(operationId.Value, nameof(operationId));
            CareerSaveModelGuard.DefinedEnum(operationKind, nameof(operationKind));
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.OperationKind != operationKind)
            {
                throw new ArgumentException(
                    "The receipt target does not match the operation kind.",
                    nameof(target));
            }

            if (string.IsNullOrEmpty(inputFingerprint.Value))
            {
                throw new ArgumentException(
                    "An operation input fingerprint is required.",
                    nameof(inputFingerprint));
            }

            CareerSaveModelGuard.StableId(appliedLineageId.Value, nameof(appliedLineageId));
            CareerSaveModelGuard.PositiveRevision(appliedRevision, nameof(appliedRevision));
            CareerSaveModelGuard.NonNegativeUtcMilliseconds(completedAtUtcMs, nameof(completedAtUtcMs));
            CareerSaveModelGuard.DefinedEnum(outcomeKind, nameof(outcomeKind));
            if (outcomeKind != ExpectedOutcomeFor(operationKind))
            {
                throw new ArgumentException(
                    "The outcome kind does not match the operation kind.",
                    nameof(outcomeKind));
            }

            if (outcomeSummary == null)
            {
                throw new ArgumentNullException(nameof(outcomeSummary));
            }

            if (outcomeSummary.OutcomeKind != outcomeKind)
            {
                throw new ArgumentException(
                    "The outcome summary does not match the outcome kind.",
                    nameof(outcomeSummary));
            }

            OperationId = operationId;
            OperationKind = operationKind;
            Target = target.Copy();
            InputFingerprint = inputFingerprint;
            AppliedLineageId = appliedLineageId;
            AppliedRevision = appliedRevision;
            CompletedAtUtcMs = completedAtUtcMs;
            OutcomeKind = outcomeKind;
            OutcomeSummary = outcomeSummary.Copy();
        }

        public OperationId OperationId { get; }

        public OperationKind OperationKind { get; }

        public OperationReceiptTarget Target { get; }

        public Sha256Digest InputFingerprint { get; }

        public LineageId AppliedLineageId { get; }

        public long AppliedRevision { get; }

        public long CompletedAtUtcMs { get; }

        public OperationOutcomeKind OutcomeKind { get; }

        public OperationOutcomeSummary OutcomeSummary { get; }

        internal OperationReceipt Copy()
        {
            return new OperationReceipt(
                OperationId,
                OperationKind,
                Target,
                InputFingerprint,
                AppliedLineageId,
                AppliedRevision,
                CompletedAtUtcMs,
                OutcomeKind,
                OutcomeSummary);
        }

        private static OperationOutcomeKind ExpectedOutcomeFor(OperationKind operationKind)
        {
            switch (operationKind)
            {
                case OperationKind.CreateCareer:
                    return OperationOutcomeKind.CareerCreated;
                case OperationKind.ConfirmTryoutStage:
                    return OperationOutcomeKind.TryoutAdvanced;
                case OperationKind.ConfirmWeekPlan:
                    return OperationOutcomeKind.WeekPlanConfirmed;
                case OperationKind.ExecuteWeekAction:
                    return OperationOutcomeKind.SlotCompleted;
                case OperationKind.ResolveEventChoice:
                    return OperationOutcomeKind.EventChoiceApplied;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(operationKind),
                        operationKind,
                        "Unknown operation kind.");
            }
        }
    }
}
