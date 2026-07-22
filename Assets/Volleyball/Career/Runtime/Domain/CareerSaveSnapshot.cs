using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Domain
{
    public enum PotentialGrade
    {
        D = 0,
        C = 1,
        B = 2,
        A = 3,
        S = 4
    }

    public sealed class TryoutResolvedOutput
    {
        public TryoutResolvedOutput(string outputId, int perturbation)
        {
            OutputId = CareerSaveModelGuard.BusinessId(outputId, nameof(outputId));
            Perturbation = CareerSaveModelGuard.InclusiveRange(
                perturbation,
                -100,
                100,
                nameof(perturbation));
        }

        public string OutputId { get; }

        public int Perturbation { get; }

        internal TryoutResolvedOutput Copy()
        {
            return new TryoutResolvedOutput(OutputId, Perturbation);
        }
    }

    public sealed class TryoutStageState
    {
        private readonly TryoutResolvedOutput[] _resolvedOutputs;
        private readonly ReadOnlyCollection<TryoutResolvedOutput> _readOnlyResolvedOutputs;

        public TryoutStageState(
            int stageNumber,
            OccurrenceId occurrenceId,
            int randomVersion,
            string choiceId,
            IEnumerable<TryoutResolvedOutput> resolvedOutputs)
        {
            StageNumber = CareerSaveModelGuard.InclusiveRange(
                stageNumber,
                1,
                3,
                nameof(stageNumber));
            CareerSaveModelGuard.StableId(occurrenceId.Value, nameof(occurrenceId));
            RandomVersion = CareerSaveModelGuard.PositiveVersion(
                randomVersion,
                nameof(randomVersion));
            if (resolvedOutputs == null)
            {
                throw new ArgumentNullException(nameof(resolvedOutputs));
            }

            var copiedOutputs = new List<TryoutResolvedOutput>();
            var outputIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var output in resolvedOutputs)
            {
                if (output == null)
                {
                    throw new ArgumentException(
                        "Resolved tryout outputs cannot contain null.",
                        nameof(resolvedOutputs));
                }

                if (!outputIds.Add(output.OutputId))
                {
                    throw new ArgumentException(
                        "Resolved output IDs must be unique inside one tryout stage.",
                        nameof(resolvedOutputs));
                }

                copiedOutputs.Add(output.Copy());
            }

            if (choiceId == null)
            {
                if (copiedOutputs.Count != 0)
                {
                    throw new ArgumentException(
                        "An unconfirmed tryout stage cannot contain resolved outputs.",
                        nameof(resolvedOutputs));
                }
            }
            else
            {
                ChoiceId = CareerSaveModelGuard.BusinessId(choiceId, nameof(choiceId));
                if (copiedOutputs.Count == 0)
                {
                    throw new ArgumentException(
                        "A confirmed tryout stage requires at least one resolved output.",
                        nameof(resolvedOutputs));
                }
            }

            OccurrenceId = occurrenceId;
            _resolvedOutputs = copiedOutputs.ToArray();
            _readOnlyResolvedOutputs = Array.AsReadOnly(_resolvedOutputs);
        }

        public int StageNumber { get; }

        public OccurrenceId OccurrenceId { get; }

        public int RandomVersion { get; }

        public string ChoiceId { get; }

        public IReadOnlyList<TryoutResolvedOutput> ResolvedOutputs => _readOnlyResolvedOutputs;

        public bool IsConfirmed => ChoiceId != null;

        internal TryoutStageState Copy()
        {
            return new TryoutStageState(
                StageNumber,
                OccurrenceId,
                RandomVersion,
                ChoiceId,
                _resolvedOutputs);
        }
    }

    public sealed class TryoutOnboardingState
    {
        private readonly TryoutStageState[] _stages;
        private readonly ReadOnlyCollection<TryoutStageState> _readOnlyStages;

        public TryoutOnboardingState(
            IEnumerable<TryoutStageState> stages,
            int nextStageNumber,
            bool isFormallyEnrolled)
        {
            if (stages == null)
            {
                throw new ArgumentNullException(nameof(stages));
            }

            var copiedStages = new List<TryoutStageState>(3);
            var occurrences = new HashSet<OccurrenceId>();
            foreach (var stage in stages)
            {
                if (stage == null)
                {
                    throw new ArgumentException(
                        "Tryout onboarding stages cannot contain null.",
                        nameof(stages));
                }

                if (copiedStages.Count == 3)
                {
                    throw new ArgumentException(
                        "Tryout onboarding contains exactly three ordered stages.",
                        nameof(stages));
                }

                if (stage.StageNumber != copiedStages.Count + 1)
                {
                    throw new ArgumentException(
                        "Tryout onboarding stages must be ordered 1, 2, 3.",
                        nameof(stages));
                }

                if (!occurrences.Add(stage.OccurrenceId))
                {
                    throw new ArgumentException(
                        "Tryout stage occurrences must be unique.",
                        nameof(stages));
                }

                copiedStages.Add(stage.Copy());
            }

            if (copiedStages.Count != 3)
            {
                throw new ArgumentException(
                    "Tryout onboarding contains exactly three ordered stages.",
                    nameof(stages));
            }

            ValidateSequence(copiedStages, nextStageNumber, isFormallyEnrolled);

            NextStageNumber = nextStageNumber;
            IsFormallyEnrolled = isFormallyEnrolled;
            _stages = copiedStages.ToArray();
            _readOnlyStages = Array.AsReadOnly(_stages);
        }

        public IReadOnlyList<TryoutStageState> Stages => _readOnlyStages;

        public int NextStageNumber { get; }

        public int CurrentStageNumber => IsFormallyEnrolled ? 0 : NextStageNumber;

        public bool IsFormallyEnrolled { get; }

        public bool IsComplete => IsFormallyEnrolled;

        internal TryoutOnboardingState Copy()
        {
            return new TryoutOnboardingState(_stages, NextStageNumber, IsFormallyEnrolled);
        }

        private static void ValidateSequence(
            IReadOnlyList<TryoutStageState> stages,
            int nextStageNumber,
            bool isFormallyEnrolled)
        {
            if (isFormallyEnrolled)
            {
                if (nextStageNumber != 0)
                {
                    throw new ArgumentException(
                        "Completed onboarding has no next stage.",
                        nameof(nextStageNumber));
                }

                for (var index = 0; index < stages.Count; index++)
                {
                    if (!stages[index].IsConfirmed)
                    {
                        throw new ArgumentException(
                            "Formal enrollment requires all three tryout stages to be confirmed.",
                            nameof(stages));
                    }
                }

                return;
            }

            CareerSaveModelGuard.InclusiveRange(nextStageNumber, 1, 3, nameof(nextStageNumber));
            for (var index = 0; index < stages.Count; index++)
            {
                var shouldBeConfirmed = index + 1 < nextStageNumber;
                if (stages[index].IsConfirmed != shouldBeConfirmed)
                {
                    throw new ArgumentException(
                        "Before enrollment, only stages before the current stage may be confirmed.",
                        nameof(stages));
                }
            }
        }
    }

    public sealed class CareerPlayerDraft
    {
        public CareerPlayerDraft(PlayerId playerId, string displayName, int jerseyNumber)
        {
            if (string.IsNullOrWhiteSpace(playerId.Value))
            {
                throw new ArgumentException(
                    "A player draft requires a stable player ID.",
                    nameof(playerId));
            }

            DisplayName = CareerSaveModelGuard.RequiredText(displayName, nameof(displayName));
            JerseyNumber = CareerSaveModelGuard.InclusiveRange(
                jerseyNumber,
                1,
                99,
                nameof(jerseyNumber));
            PlayerId = playerId;
        }

        public PlayerId PlayerId { get; }

        public string DisplayName { get; }

        public int JerseyNumber { get; }

        internal CareerPlayerDraft Copy()
        {
            return new CareerPlayerDraft(PlayerId, DisplayName, JerseyNumber);
        }
    }

    public sealed class CareerSaveSnapshot
    {
        private readonly OperationReceipt[] _operationReceipts;
        private readonly ReadOnlyCollection<OperationReceipt> _readOnlyOperationReceipts;

        public CareerSaveSnapshot(
            CareerSaveVersions versions,
            CareerSaveIdentity identity,
            CareerSeed careerSeed,
            string careerName,
            CareerPlayerDraft playerDraft,
            TryoutOnboardingState onboarding,
            CareerProgressionState progression,
            TrainingEmphasisLedger trainingEmphases,
            CareerPlayerRecord player,
            TeamId? teamId,
            PotentialGrade? potentialGrade,
            int? fatigue,
            int? mindset,
            int? coachTrust,
            IEnumerable<OperationReceipt> operationReceipts)
        {
            if (versions == null)
            {
                throw new ArgumentNullException(nameof(versions));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (careerSeed == null)
            {
                throw new ArgumentNullException(nameof(careerSeed));
            }

            CareerName = CareerSaveModelGuard.RequiredText(careerName, nameof(careerName));
            if (playerDraft == null)
            {
                throw new ArgumentNullException(nameof(playerDraft));
            }

            if (onboarding == null)
            {
                throw new ArgumentNullException(nameof(onboarding));
            }

            if (progression == null)
            {
                throw new ArgumentNullException(nameof(progression));
            }

            if (trainingEmphases == null)
            {
                throw new ArgumentNullException(nameof(trainingEmphases));
            }

            if (operationReceipts == null)
            {
                throw new ArgumentNullException(nameof(operationReceipts));
            }

            var copiedOnboarding = onboarding.Copy();
            var copiedProgression = progression.Copy();
            var copiedTrainingEmphases = new TrainingEmphasisLedger(
                trainingEmphases.Contributions);
            ValidateCompletePlayerState(
                copiedOnboarding,
                copiedProgression,
                playerDraft,
                player,
                teamId,
                potentialGrade,
                fatigue,
                mindset,
                coachTrust);
            ValidateRandomVersions(
                versions.CareerRandomAlgorithmVersion,
                copiedOnboarding,
                copiedProgression.PendingEvent);
            ValidateOccurrenceRegistry(copiedOnboarding, copiedProgression);
            ValidateWeekActionContent(copiedProgression);
            ValidateTrainingEmphases(copiedProgression, copiedTrainingEmphases);

            var copiedReceipts = CopyAndValidateReceipts(operationReceipts, identity);
            ValidateOnboardingRecovery(copiedOnboarding, copiedReceipts);
            ValidateCurrentPlanRecovery(copiedProgression, copiedReceipts);
            ValidateReceiptRevisionChain(
                copiedOnboarding,
                copiedProgression,
                copiedReceipts,
                identity);

            Versions = new CareerSaveVersions(
                versions.SchemaVersion,
                versions.ContentVersion,
                versions.RulesetVersion,
                versions.CareerRandomAlgorithmVersion);
            Identity = new CareerSaveIdentity(
                identity.ProfileId,
                identity.SaveId,
                identity.LineageId,
                identity.Revision,
                identity.CreatedAtUtcMs,
                identity.UpdatedAtUtcMs,
                identity.SnapshotHash,
                identity.RestoredFromVersionToken);
            CareerSeed = new CareerSeed(careerSeed.ToBytes());
            PlayerDraft = playerDraft.Copy();
            Onboarding = copiedOnboarding;
            Progression = copiedProgression;
            TrainingEmphases = copiedTrainingEmphases;
            Player = CopyPlayer(player);
            TeamId = teamId;
            PotentialGrade = potentialGrade;
            Fatigue = fatigue;
            Mindset = mindset;
            CoachTrust = coachTrust;
            _operationReceipts = copiedReceipts;
            _readOnlyOperationReceipts = Array.AsReadOnly(_operationReceipts);
        }

        public CareerSaveVersions Versions { get; }

        public CareerSaveIdentity Identity { get; }

        public CareerSeed CareerSeed { get; }

        public string CareerName { get; }

        public CareerPlayerDraft PlayerDraft { get; }

        public TryoutOnboardingState Onboarding { get; }

        public CareerProgressionState Progression { get; }

        public TrainingEmphasisLedger TrainingEmphases { get; }

        public CareerPlayerRecord Player { get; }

        public TeamId? TeamId { get; }

        public PotentialGrade? PotentialGrade { get; }

        public int? Fatigue { get; }

        public int? Mindset { get; }

        public int? CoachTrust { get; }

        public IReadOnlyList<OperationReceipt> OperationReceipts => _readOnlyOperationReceipts;

        public bool HasCompletePlayer => Player != null;

        private static void ValidateWeekActionContent(CareerProgressionState progression)
        {
            if (progression.WeekPlan == null)
            {
                return;
            }

            var catalog = CareerWeekActionCatalogV1.Create();
            for (var index = 0; index < progression.WeekPlan.Slots.Count; index++)
            {
                var action = progression.WeekPlan.Slots[index];
                if (action == null)
                {
                    continue;
                }

                var definition = catalog.Find(action.ContentId);
                if (definition == null || definition.Kind != action.Kind)
                {
                    throw new ArgumentException(
                        "Every Schema V1 week action must match the closed content catalog.",
                        nameof(progression));
                }
            }
        }

        private static void ValidateTrainingEmphases(
            CareerProgressionState progression,
            TrainingEmphasisLedger actual)
        {
            var executedFreeSlots = 0;
            if (progression.Kind == CareerProgressionKind.AwaitingEventChoice)
            {
                executedFreeSlots = 1;
            }
            else if (progression.Kind == CareerProgressionKind.Planned)
            {
                if (progression.NextSlotNumber == 2)
                {
                    executedFreeSlots = 1;
                }
                else if (progression.NextSlotNumber == 3)
                {
                    executedFreeSlots = 2;
                }
            }

            var expected = TrainingEmphasisLedger.Empty;
            if (executedFreeSlots != 0)
            {
                var catalog = CareerWeekActionCatalogV1.Create();
                for (var index = 0; index < executedFreeSlots; index++)
                {
                    var action = progression.WeekPlan.Slots[index];
                    var definition = catalog.Find(action.ContentId);
                    if (definition.Direction.HasValue)
                    {
                        expected = expected.AddExecutedTraining(action, catalog);
                    }
                }
            }

            if (actual.Contributions.Count != expected.Contributions.Count)
            {
                throw new ArgumentException(
                    "Training emphases must exactly match executed current-plan training sources.",
                    nameof(actual));
            }

            for (var index = 0; index < expected.Contributions.Count; index++)
            {
                var actualContribution = actual.Contributions[index];
                var expectedContribution = expected.Contributions[index];
                if (!actualContribution.SourceSlotActionId.Equals(
                        expectedContribution.SourceSlotActionId) ||
                    actualContribution.Direction != expectedContribution.Direction ||
                    actualContribution.BonusBasisPoints !=
                        expectedContribution.BonusBasisPoints)
                {
                    throw new ArgumentException(
                        "Training emphasis source, direction, order, repeat coefficient, and cap are derived invariants.",
                        nameof(actual));
                }
            }
        }

        private static void ValidateCompletePlayerState(
            TryoutOnboardingState onboarding,
            CareerProgressionState progression,
            CareerPlayerDraft playerDraft,
            CareerPlayerRecord player,
            TeamId? teamId,
            PotentialGrade? potentialGrade,
            int? fatigue,
            int? mindset,
            int? coachTrust)
        {
            var hasCompletePlayer = player != null;
            var allOtherCompleteFields = teamId.HasValue &&
                                         potentialGrade.HasValue &&
                                         fatigue.HasValue &&
                                         mindset.HasValue &&
                                         coachTrust.HasValue;
            var anyOtherCompleteField = teamId.HasValue ||
                                        potentialGrade.HasValue ||
                                        fatigue.HasValue ||
                                        mindset.HasValue ||
                                        coachTrust.HasValue;

            if ((hasCompletePlayer && !allOtherCompleteFields) ||
                (!hasCompletePlayer && anyOtherCompleteField))
            {
                throw new ArgumentException(
                    "Player, team, potential, fatigue, mindset and coach trust must be present or absent together.",
                    nameof(player));
            }

            switch (progression.Kind)
            {
                case CareerProgressionKind.CareerCreated:
                    if (onboarding.IsComplete || onboarding.CurrentStageNumber != 1)
                    {
                        throw new ArgumentException(
                            "A newly created career must begin at unconfirmed tryout stage 1.",
                            nameof(onboarding));
                    }

                    break;

                case CareerProgressionKind.Tryout:
                    if (onboarding.IsComplete ||
                        progression.TryoutStage != onboarding.CurrentStageNumber)
                    {
                        throw new ArgumentException(
                            "Tryout progression must match the onboarding current stage.",
                            nameof(onboarding));
                    }

                    break;

                default:
                    if (!onboarding.IsComplete || !onboarding.IsFormallyEnrolled)
                    {
                        throw new ArgumentException(
                            "Planning and later states require completed tryout onboarding and formal enrollment.",
                            nameof(onboarding));
                    }

                    break;
            }

            var beforeEnrollment = progression.Kind == CareerProgressionKind.CareerCreated ||
                                   progression.Kind == CareerProgressionKind.Tryout;
            if (beforeEnrollment && hasCompletePlayer)
            {
                throw new ArgumentException(
                    "CareerCreated and Tryout snapshots cannot contain a complete player or team.",
                    nameof(player));
            }

            if (!beforeEnrollment && !hasCompletePlayer)
            {
                throw new ArgumentException(
                    "Planning and later snapshots require a complete player and team.",
                    nameof(player));
            }

            if (!hasCompletePlayer)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(teamId.Value.Value))
            {
                throw new ArgumentException("A stable team ID is required.", nameof(teamId));
            }

            CareerSaveModelGuard.DefinedEnum(potentialGrade.Value, nameof(potentialGrade));
            CareerSaveModelGuard.InclusiveRange(fatigue.Value, 0, 100, nameof(fatigue));
            CareerSaveModelGuard.InclusiveRange(mindset.Value, 0, 100, nameof(mindset));
            CareerSaveModelGuard.InclusiveRange(coachTrust.Value, 0, 100, nameof(coachTrust));

            if (!player.PlayerId.Equals(playerDraft.PlayerId) ||
                !string.Equals(player.DisplayName, playerDraft.DisplayName, StringComparison.Ordinal) ||
                player.JerseyNumber != playerDraft.JerseyNumber)
            {
                throw new ArgumentException(
                    "The complete player identity must match the persisted player draft.",
                    nameof(player));
            }
        }

        private static OperationReceipt[] CopyAndValidateReceipts(
            IEnumerable<OperationReceipt> operationReceipts,
            CareerSaveIdentity identity)
        {
            var copied = new List<OperationReceipt>();
            var operationIds = new HashSet<OperationId>();
            var targetKeys = new HashSet<string>(StringComparer.Ordinal);
            var appliedRevisions = new HashSet<long>();
            foreach (var receipt in operationReceipts)
            {
                if (receipt == null)
                {
                    throw new ArgumentException(
                        "Operation receipts cannot contain null.",
                        nameof(operationReceipts));
                }

                if (!operationIds.Add(receipt.OperationId))
                {
                    throw new ArgumentException(
                        "Operation receipt IDs must be unique within a snapshot.",
                        nameof(operationReceipts));
                }

                if (!targetKeys.Add(TargetUniquenessKey(receipt.Target)))
                {
                    throw new ArgumentException(
                        "Each operation business target may be applied only once.",
                        nameof(operationReceipts));
                }

                if (!appliedRevisions.Add(receipt.AppliedRevision))
                {
                    throw new ArgumentException(
                        "Each applied operation receipt must have a unique revision.",
                        nameof(operationReceipts));
                }

                if (!receipt.AppliedLineageId.Equals(identity.LineageId))
                {
                    throw new ArgumentException(
                        "Every operation receipt must belong to the snapshot lineage.",
                        nameof(operationReceipts));
                }

                if (receipt.AppliedRevision > identity.Revision)
                {
                    throw new ArgumentException(
                        "An operation receipt cannot be newer than the snapshot revision.",
                        nameof(operationReceipts));
                }

                copied.Add(receipt.Copy());
            }

            return copied.ToArray();
        }

        private static string TargetUniquenessKey(OperationReceiptTarget target)
        {
            switch (target.OperationKind)
            {
                case OperationKind.CreateCareer:
                    return "create";
                case OperationKind.ConfirmTryoutStage:
                    return "tryout|" + target.TryoutStage;
                case OperationKind.ConfirmWeekPlan:
                    return "plan|" + target.WeekPlanId.Value.Value.ToString("D");
                case OperationKind.ExecuteWeekAction:
                    return "action|" + target.WeekPlanId.Value.Value.ToString("D") + "|" +
                           target.SlotActionId.Value.Value.ToString("D");
                case OperationKind.ResolveEventChoice:
                    return "event|" + target.WeekPlanId.Value.Value.ToString("D") + "|" +
                           target.EventOccurrenceId.Value.Value.ToString("D");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(target),
                        target.OperationKind,
                        "Unknown operation target kind.");
            }
        }

        private static void ValidateOnboardingRecovery(
            TryoutOnboardingState onboarding,
            IReadOnlyList<OperationReceipt> receipts)
        {
            var createCareerCount = 0;
            var tryoutReceiptByStage = new OperationReceipt[3];
            for (var index = 0; index < receipts.Count; index++)
            {
                var receipt = receipts[index];
                switch (receipt.OperationKind)
                {
                    case OperationKind.CreateCareer:
                        createCareerCount++;
                        break;

                    case OperationKind.ConfirmTryoutStage:
                        var stageIndex = receipt.Target.TryoutStage - 1;
                        if (stageIndex < 0 || stageIndex >= tryoutReceiptByStage.Length ||
                            tryoutReceiptByStage[stageIndex] != null)
                        {
                            throw new ArgumentException(
                                "Each tryout stage may have exactly one confirmation receipt.",
                                nameof(receipts));
                        }

                        tryoutReceiptByStage[stageIndex] = receipt;
                        break;
                }
            }

            if (createCareerCount != 1)
            {
                throw new ArgumentException(
                    "Every snapshot requires exactly one CreateCareer receipt.",
                    nameof(receipts));
            }

            for (var index = 0; index < onboarding.Stages.Count; index++)
            {
                var stage = onboarding.Stages[index];
                var receipt = tryoutReceiptByStage[index];
                if (!stage.IsConfirmed)
                {
                    if (receipt != null)
                    {
                        throw new ArgumentException(
                            "An unconfirmed tryout stage cannot have a confirmation receipt.",
                            nameof(receipts));
                    }

                    continue;
                }

                if (receipt == null ||
                    !receipt.Target.TryoutOccurrenceId.HasValue ||
                    !receipt.Target.TryoutOccurrenceId.Value.Equals(stage.OccurrenceId) ||
                    !string.Equals(
                        receipt.Target.ChoiceId,
                        stage.ChoiceId,
                        StringComparison.Ordinal) ||
                    !ResolvedOutputsEqual(
                        receipt.OutcomeSummary.TryoutResolvedOutputs,
                        stage.ResolvedOutputs))
                {
                    throw new ArgumentException(
                        "A confirmed tryout stage requires one receipt matching its stage, occurrence, choice and resolved outputs.",
                        nameof(receipts));
                }
            }
        }

        private static bool ResolvedOutputsEqual(
            IReadOnlyList<TryoutResolvedOutput> left,
            IReadOnlyList<TryoutResolvedOutput> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!string.Equals(
                        left[index].OutputId,
                        right[index].OutputId,
                        StringComparison.Ordinal) ||
                    left[index].Perturbation != right[index].Perturbation)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateRandomVersions(
            int careerRandomAlgorithmVersion,
            TryoutOnboardingState onboarding,
            PendingCareerEvent pendingEvent)
        {
            for (var index = 0; index < onboarding.Stages.Count; index++)
            {
                if (onboarding.Stages[index].RandomVersion != careerRandomAlgorithmVersion)
                {
                    throw new ArgumentException(
                        "Every tryout stage must use the snapshot career random algorithm version.",
                        nameof(onboarding));
                }
            }

            if (pendingEvent != null &&
                pendingEvent.RandomVersion != careerRandomAlgorithmVersion)
            {
                throw new ArgumentException(
                    "The pending event must use the snapshot career random algorithm version.",
                    nameof(pendingEvent));
            }
        }

        private static void ValidateOccurrenceRegistry(
            TryoutOnboardingState onboarding,
            CareerProgressionState progression)
        {
            var occurrences = new HashSet<OccurrenceId>();
            for (var index = 0; index < onboarding.Stages.Count; index++)
            {
                RegisterOccurrence(
                    occurrences,
                    onboarding.Stages[index].OccurrenceId,
                    "tryout onboarding");
            }

            if (progression.WeekPlan != null)
            {
                for (var index = 0; index < progression.WeekPlan.Slots.Count; index++)
                {
                    var action = progression.WeekPlan.Slots[index];
                    if (action != null)
                    {
                        RegisterOccurrence(
                            occurrences,
                            action.OccurrenceId,
                            "current week plan");
                    }
                }
            }

            if (progression.PendingEvent != null)
            {
                RegisterOccurrence(
                    occurrences,
                    progression.PendingEvent.OccurrenceId,
                    "pending event");
            }
        }

        private static void RegisterOccurrence(
            ISet<OccurrenceId> registry,
            OccurrenceId occurrenceId,
            string source)
        {
            if (!registry.Add(occurrenceId))
            {
                throw new ArgumentException(
                    "Occurrence IDs must be globally unique across tryout stages, current week actions and the pending event (collision in " +
                    source + ").",
                    nameof(occurrenceId));
            }
        }

        private static void ValidateReceiptRevisionChain(
            TryoutOnboardingState onboarding,
            CareerProgressionState progression,
            IReadOnlyList<OperationReceipt> receipts,
            CareerSaveIdentity identity)
        {
            var snapshotRevision = identity.Revision;
            var requiredBusinessFrontier = snapshotRevision;
            var isImmediateRestore = identity.RestoredFromVersionToken.HasValue &&
                                     snapshotRevision ==
                                     identity.RestoredFromVersionToken.Value.Revision + 1;
            if (isImmediateRestore)
            {
                requiredBusinessFrontier = identity.RestoredFromVersionToken.Value.Revision;
            }

            var createReceipt = FindReceipt(receipts, OperationKind.CreateCareer, null, null);
            var frontierRevision = createReceipt.AppliedRevision;
            for (var index = 0; index < onboarding.Stages.Count; index++)
            {
                var stage = onboarding.Stages[index];
                if (!stage.IsConfirmed)
                {
                    continue;
                }

                var stageReceipt = FindTryoutReceipt(receipts, stage.StageNumber);
                frontierRevision = RequireLaterRevision(
                    frontierRevision,
                    stageReceipt,
                    "Tryout stage receipts must strictly follow career creation and prior stages.");
            }

            if (progression.Kind == CareerProgressionKind.CareerCreated ||
                progression.Kind == CareerProgressionKind.Tryout)
            {
                for (var index = 0; index < receipts.Count; index++)
                {
                    if (receipts[index].Target.WeekPlanId.HasValue)
                    {
                        throw new ArgumentException(
                            "Onboarding snapshots cannot contain week-plan operation receipts.",
                            nameof(receipts));
                    }
                }

                RequireFrontierRevision(
                    frontierRevision,
                    requiredBusinessFrontier,
                    progression.Kind,
                    isImmediateRestore);
                return;
            }

            if (progression.Kind == CareerProgressionKind.Planning)
            {
                return;
            }

            var plan = progression.WeekPlan;
            var planReceipt = FindReceipt(
                receipts,
                OperationKind.ConfirmWeekPlan,
                plan.PlanId,
                null);
            frontierRevision = RequireLaterRevision(
                frontierRevision,
                planReceipt,
                "Week confirmation must strictly follow completed onboarding.");

            if (progression.Kind == CareerProgressionKind.Planned &&
                progression.NextSlotNumber == 1)
            {
                RequireFrontierRevision(
                    frontierRevision,
                    requiredBusinessFrontier,
                    progression.Kind,
                    isImmediateRestore);
                return;
            }

            var slotOneReceipt = FindReceipt(
                receipts,
                OperationKind.ExecuteWeekAction,
                plan.PlanId,
                plan.Slots[0].SlotActionId);
            frontierRevision = RequireLaterRevision(
                frontierRevision,
                slotOneReceipt,
                "Slot 1 execution must strictly follow week confirmation.");

            if (progression.Kind == CareerProgressionKind.AwaitingEventChoice)
            {
                RequireFrontierRevision(
                    frontierRevision,
                    requiredBusinessFrontier,
                    progression.Kind,
                    isImmediateRestore);
                return;
            }

            var eventReceipt = FindReceipt(
                receipts,
                OperationKind.ResolveEventChoice,
                plan.PlanId,
                null);
            frontierRevision = RequireLaterRevision(
                frontierRevision,
                eventReceipt,
                "The fixed event choice must strictly follow slot 1 execution.");

            if (progression.NextSlotNumber == 2)
            {
                RequireFrontierRevision(
                    frontierRevision,
                    requiredBusinessFrontier,
                    progression.Kind,
                    isImmediateRestore);
                return;
            }

            var slotTwoReceipt = FindReceipt(
                receipts,
                OperationKind.ExecuteWeekAction,
                plan.PlanId,
                plan.Slots[1].SlotActionId);
            frontierRevision = RequireLaterRevision(
                frontierRevision,
                slotTwoReceipt,
                "Slot 2 execution must strictly follow the fixed event choice.");
            RequireFrontierRevision(
                frontierRevision,
                requiredBusinessFrontier,
                progression.Kind,
                isImmediateRestore);
        }

        private static OperationReceipt FindTryoutReceipt(
            IReadOnlyList<OperationReceipt> receipts,
            int stageNumber)
        {
            for (var index = 0; index < receipts.Count; index++)
            {
                if (receipts[index].OperationKind == OperationKind.ConfirmTryoutStage &&
                    receipts[index].Target.TryoutStage == stageNumber)
                {
                    return receipts[index];
                }
            }

            throw new ArgumentException(
                "A required tryout stage receipt is missing.",
                nameof(receipts));
        }

        private static OperationReceipt FindReceipt(
            IReadOnlyList<OperationReceipt> receipts,
            OperationKind operationKind,
            WeekPlanId? weekPlanId,
            SlotActionId? slotActionId)
        {
            for (var index = 0; index < receipts.Count; index++)
            {
                var receipt = receipts[index];
                if (receipt.OperationKind != operationKind)
                {
                    continue;
                }

                if (weekPlanId.HasValue &&
                    (!receipt.Target.WeekPlanId.HasValue ||
                     !receipt.Target.WeekPlanId.Value.Equals(weekPlanId.Value)))
                {
                    continue;
                }

                if (slotActionId.HasValue &&
                    (!receipt.Target.SlotActionId.HasValue ||
                     !receipt.Target.SlotActionId.Value.Equals(slotActionId.Value)))
                {
                    continue;
                }

                return receipt;
            }

            throw new ArgumentException(
                "A required operation receipt is missing from the recovery chain.",
                nameof(receipts));
        }

        private static long RequireLaterRevision(
            long priorRevision,
            OperationReceipt receipt,
            string message)
        {
            if (receipt.AppliedRevision <= priorRevision)
            {
                throw new ArgumentException(message, nameof(receipt));
            }

            return receipt.AppliedRevision;
        }

        private static void RequireFrontierRevision(
            long businessRevision,
            long snapshotRevision,
            CareerProgressionKind progressionKind,
            bool allowOlderRestoreFrontier = false)
        {
            var isValid = allowOlderRestoreFrontier
                ? businessRevision <= snapshotRevision
                : businessRevision == snapshotRevision;
            if (!isValid)
            {
                throw new ArgumentException(
                    "The latest business receipt for " + progressionKind +
                    (allowOlderRestoreFrontier
                        ? " cannot be newer than the restored source revision."
                        : " must equal the snapshot revision."),
                    nameof(snapshotRevision));
            }
        }

        private static void ValidateCurrentPlanRecovery(
            CareerProgressionState progression,
            IReadOnlyList<OperationReceipt> receipts)
        {
            var plan = progression.WeekPlan;
            if (plan == null)
            {
                return;
            }

            var confirmCount = 0;
            var slotOneExecuted = false;
            var slotTwoExecuted = false;
            var resolvedSlotOneEventCount = 0;

            for (var index = 0; index < receipts.Count; index++)
            {
                var receipt = receipts[index];
                var target = receipt.Target;
                if (!target.WeekPlanId.HasValue ||
                    !target.WeekPlanId.Value.Equals(plan.PlanId))
                {
                    continue;
                }

                switch (receipt.OperationKind)
                {
                    case OperationKind.ConfirmWeekPlan:
                        confirmCount++;
                        break;

                    case OperationKind.ExecuteWeekAction:
                        if (MatchesAction(target, plan.Slots[0]))
                        {
                            slotOneExecuted = true;
                        }
                        else if (MatchesAction(target, plan.Slots[1]))
                        {
                            slotTwoExecuted = true;
                        }
                        else
                        {
                            throw new ArgumentException(
                                "A current-plan action receipt must target slot 1 or slot 2 exactly.",
                                nameof(receipts));
                        }

                        break;

                    case OperationKind.ResolveEventChoice:
                        if (!MatchesAction(target, plan.Slots[0]))
                        {
                            throw new ArgumentException(
                                "The schema V1 fixed event must be sourced from current-plan slot 1.",
                                nameof(receipts));
                        }

                        resolvedSlotOneEventCount++;
                        break;

                    default:
                        throw new ArgumentException(
                            "This operation kind cannot target a week plan.",
                            nameof(receipts));
                }
            }

            if (!plan.IsConfirmed)
            {
                if (confirmCount != 0 || slotOneExecuted || slotTwoExecuted ||
                    resolvedSlotOneEventCount != 0)
                {
                    throw new ArgumentException(
                        "An unconfirmed current plan cannot have confirmation, action or event receipts.",
                        nameof(receipts));
                }

                return;
            }

            if (confirmCount != 1)
            {
                throw new ArgumentException(
                    "A confirmed current plan requires exactly one matching confirmation receipt.",
                    nameof(receipts));
            }

            switch (progression.Kind)
            {
                case CareerProgressionKind.Planned:
                    ValidatePlannedReceiptFrontier(
                        progression.NextSlotNumber,
                        slotOneExecuted,
                        slotTwoExecuted,
                        resolvedSlotOneEventCount,
                        nameof(receipts));
                    return;

                case CareerProgressionKind.AwaitingEventChoice:
                    if (!slotOneExecuted || slotTwoExecuted || resolvedSlotOneEventCount != 0)
                    {
                        throw new ArgumentException(
                            "AwaitingEventChoice requires completed slot 1, no slot 2 execution and no resolved current event.",
                            nameof(receipts));
                    }

                    ValidatePendingEventSource(progression.PendingEvent, plan);
                    return;

                default:
                    throw new ArgumentException(
                        "Only confirmed-plan progression states may carry a confirmed plan.",
                        nameof(progression));
            }
        }

        private static void ValidatePlannedReceiptFrontier(
            int nextSlotNumber,
            bool slotOneExecuted,
            bool slotTwoExecuted,
            int resolvedSlotOneEventCount,
            string parameterName)
        {
            switch (nextSlotNumber)
            {
                case 1:
                    if (slotOneExecuted || slotTwoExecuted || resolvedSlotOneEventCount != 0)
                    {
                        throw new ArgumentException(
                            "Planned next slot 1 cannot have current-plan action or event receipts.",
                            parameterName);
                    }

                    return;

                case 2:
                    if (!slotOneExecuted || slotTwoExecuted || resolvedSlotOneEventCount != 1)
                    {
                        throw new ArgumentException(
                            "Planned next slot 2 requires slot 1 execution and its resolved fixed event only.",
                            parameterName);
                    }

                    return;

                case 3:
                    if (!slotOneExecuted || !slotTwoExecuted || resolvedSlotOneEventCount != 1)
                    {
                        throw new ArgumentException(
                            "Planned next slot 3 requires slot 1 and slot 2 execution plus the resolved slot 1 event.",
                            parameterName);
                    }

                    return;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(nextSlotNumber),
                        nextSlotNumber,
                        "The next slot must be 1, 2, or 3.");
            }
        }

        private static void ValidatePendingEventSource(
            PendingCareerEvent pendingEvent,
            CareerWeekPlanState plan)
        {
            var slotOne = plan.Slots[0];
            if (!pendingEvent.SourceWeekPlanId.Equals(plan.PlanId) ||
                !pendingEvent.SourceSlotActionId.Equals(slotOne.SlotActionId) ||
                !pendingEvent.SourceActionOccurrenceId.Equals(slotOne.OccurrenceId) ||
                pendingEvent.ResumeAtSlotNumber != 2)
            {
                throw new ArgumentException(
                    "The pending event source must exactly match current-plan slot 1 and resume at slot 2.",
                    nameof(pendingEvent));
            }
        }

        private static bool MatchesAction(
            OperationReceiptTarget target,
            CareerWeekActionState action)
        {
            return action != null &&
                   target.SlotActionId.HasValue &&
                   target.ActionOccurrenceId.HasValue &&
                   target.SlotActionId.Value.Equals(action.SlotActionId) &&
                   target.ActionOccurrenceId.Value.Equals(action.OccurrenceId);
        }

        private static CareerPlayerRecord CopyPlayer(CareerPlayerRecord player)
        {
            if (player == null)
            {
                return null;
            }

            var attributes = player.Attributes;
            return new CareerPlayerRecord(
                player.PlayerId,
                player.DisplayName,
                player.JerseyNumber,
                new CareerPlayerAttributes(
                    attributes.Spike,
                    attributes.Serve,
                    attributes.Reception,
                    attributes.Defense,
                    attributes.Block,
                    attributes.Movement,
                    attributes.Jump,
                    attributes.Stamina));
        }
    }
}
