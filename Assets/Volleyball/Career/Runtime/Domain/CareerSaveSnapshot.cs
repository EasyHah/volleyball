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
        private readonly CareerMatchHistoryEntry[] _matchHistory;
        private readonly ReadOnlyCollection<CareerMatchHistoryEntry> _readOnlyMatchHistory;
        private readonly CareerSettlementReceipt[] _settlementReceipts;
        private readonly ReadOnlyCollection<CareerSettlementReceipt> _readOnlySettlementReceipts;

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
            : this(
                versions,
                identity,
                careerSeed,
                careerName,
                playerDraft,
                onboarding,
                progression,
                trainingEmphases,
                player,
                teamId,
                potentialGrade,
                fatigue,
                mindset,
                coachTrust,
                operationReceipts,
                null,
                Array.Empty<CareerMatchHistoryEntry>(),
                Array.Empty<CareerSettlementReceipt>())
        {
        }

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
            IEnumerable<OperationReceipt> operationReceipts,
            PendingCareerMatch pendingMatch,
            IEnumerable<CareerMatchHistoryEntry> matchHistory,
            IEnumerable<CareerSettlementReceipt> settlementReceipts)
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

            if (matchHistory == null)
            {
                throw new ArgumentNullException(nameof(matchHistory));
            }

            if (settlementReceipts == null)
            {
                throw new ArgumentNullException(nameof(settlementReceipts));
            }

            var copiedOnboarding = onboarding.Copy();
            var copiedProgression = progression.Copy();
            var copiedTrainingEmphases = new TrainingEmphasisLedger(
                trainingEmphases.Contributions);
            var copiedPendingMatch = pendingMatch?.Copy();
            var copiedMatchHistory = CopyHistory(matchHistory);
            var copiedSettlementReceipts = CopySettlementReceipts(settlementReceipts);
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
            ValidateMatchLifecycle(
                versions,
                identity,
                copiedProgression,
                copiedTrainingEmphases,
                copiedReceipts,
                player,
                teamId,
                fatigue,
                mindset,
                coachTrust,
                copiedPendingMatch,
                copiedMatchHistory,
                copiedSettlementReceipts);
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
                versions.ContractVersion,
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
            PendingMatch = copiedPendingMatch;
            _matchHistory = copiedMatchHistory;
            _readOnlyMatchHistory = Array.AsReadOnly(_matchHistory);
            _settlementReceipts = copiedSettlementReceipts;
            _readOnlySettlementReceipts = Array.AsReadOnly(_settlementReceipts);
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

        public PendingCareerMatch PendingMatch { get; }

        public IReadOnlyList<CareerMatchHistoryEntry> MatchHistory => _readOnlyMatchHistory;

        public IReadOnlyList<CareerSettlementReceipt> SettlementReceipts =>
            _readOnlySettlementReceipts;

        public bool HasCompletePlayer => Player != null;

        private static CareerMatchHistoryEntry[] CopyHistory(
            IEnumerable<CareerMatchHistoryEntry> history)
        {
            var copied = new List<CareerMatchHistoryEntry>();
            foreach (var entry in history)
            {
                if (entry == null)
                {
                    throw new ArgumentException(
                        "Match history cannot contain null.",
                        nameof(history));
                }

                copied.Add(entry.Copy());
            }

            return copied.ToArray();
        }

        private static CareerSettlementReceipt[] CopySettlementReceipts(
            IEnumerable<CareerSettlementReceipt> receipts)
        {
            var copied = new List<CareerSettlementReceipt>();
            foreach (var receipt in receipts)
            {
                if (receipt == null)
                {
                    throw new ArgumentException(
                        "Settlement receipts cannot contain null.",
                        nameof(receipts));
                }

                copied.Add(receipt.Copy());
            }

            return copied.ToArray();
        }

        private static void ValidateMatchLifecycle(
            CareerSaveVersions versions,
            CareerSaveIdentity identity,
            CareerProgressionState progression,
            TrainingEmphasisLedger trainingEmphases,
            IReadOnlyList<OperationReceipt> operationReceipts,
            CareerPlayerRecord player,
            TeamId? teamId,
            int? fatigue,
            int? mindset,
            int? coachTrust,
            PendingCareerMatch pendingMatch,
            IReadOnlyList<CareerMatchHistoryEntry> history,
            IReadOnlyList<CareerSettlementReceipt> settlementReceipts)
        {
            var isAwaitingMatch = progression.Kind == CareerProgressionKind.AwaitingMatch;
            if (isAwaitingMatch != (pendingMatch != null))
            {
                throw new ArgumentException(
                    "AwaitingMatch progression and PendingMatch must coexist.",
                    nameof(pendingMatch));
            }

            if (pendingMatch != null)
            {
                ValidatePendingMatch(
                    versions,
                    identity,
                    progression,
                    trainingEmphases,
                    operationReceipts,
                    player,
                    teamId,
                    pendingMatch);
            }

            var historyBySession = new Dictionary<Guid, CareerMatchHistoryEntry>();
            for (var index = 0; index < history.Count; index++)
            {
                var entry = history[index];
                if (!historyBySession.TryAdd(entry.SessionId, entry))
                {
                    throw new ArgumentException(
                        "Match history session IDs must be unique.",
                        nameof(history));
                }

                if (!entry.AppliedLineageId.Equals(identity.LineageId) ||
                    entry.AppliedRevision > identity.Revision)
                {
                    throw new ArgumentException(
                        "Match history must belong to this lineage and cannot be newer than the snapshot.",
                        nameof(history));
                }
            }

            var receiptBySession = new Dictionary<Guid, CareerSettlementReceipt>();
            for (var index = 0; index < settlementReceipts.Count; index++)
            {
                var receipt = settlementReceipts[index];
                if (!receiptBySession.TryAdd(receipt.SessionId, receipt))
                {
                    throw new ArgumentException(
                        "Settlement receipt session IDs must be unique.",
                        nameof(settlementReceipts));
                }

                if (!receipt.AppliedLineageId.Equals(identity.LineageId) ||
                    receipt.AppliedRevision > identity.Revision)
                {
                    throw new ArgumentException(
                        "Settlement receipts must belong to this lineage and cannot be newer than the snapshot.",
                        nameof(settlementReceipts));
                }
            }

            if (historyBySession.Count != receiptBySession.Count)
            {
                throw new ArgumentException(
                    "Match history and settlement receipts must be one-to-one.",
                    nameof(settlementReceipts));
            }

            foreach (var pair in historyBySession)
            {
                if (!receiptBySession.TryGetValue(pair.Key, out var receipt) ||
                    !HistoryMatchesReceipt(pair.Value, receipt))
                {
                    throw new ArgumentException(
                        "Every history entry must exactly match its settlement receipt.",
                        nameof(settlementReceipts));
                }

                ValidateSettledCareerState(
                    pair.Value,
                    player,
                    fatigue,
                    mindset,
                    coachTrust);
                ValidateSettledCreationReceipt(pair.Value, operationReceipts);
            }

            if (pendingMatch != null && historyBySession.ContainsKey(pendingMatch.SessionId))
            {
                throw new ArgumentException(
                    "A pending session cannot already be settled.",
                    nameof(pendingMatch));
            }

            ValidateNoOrphanPendingCreationReceipts(
                operationReceipts,
                pendingMatch,
                historyBySession);

            var hasSettlementEvidence = history.Count != 0 || settlementReceipts.Count != 0;
            if (hasSettlementEvidence)
            {
                var plan = progression.WeekPlan;
                if (progression.Kind != CareerProgressionKind.Planning ||
                    plan == null || plan.Season != 1 || plan.Week != 2 || plan.IsConfirmed ||
                    plan.Slots[0] != null || plan.Slots[1] != null || plan.Slots[2] != null ||
                    trainingEmphases.Contributions.Count != 0 || pendingMatch != null)
                {
                    throw new ArgumentException(
                        "Settled first-match evidence requires the empty university season 1 week 2 Planning state.",
                        nameof(progression));
                }
            }
        }

        private static void ValidatePendingMatch(
            CareerSaveVersions versions,
            CareerSaveIdentity identity,
            CareerProgressionState progression,
            TrainingEmphasisLedger trainingEmphases,
            IReadOnlyList<OperationReceipt> operationReceipts,
            CareerPlayerRecord player,
            TeamId? teamId,
            PendingCareerMatch pendingMatch)
        {
            var plan = progression.WeekPlan;
            var matchAction = plan?.Slots[2];
            if (!progression.MatchSessionId.HasValue ||
                progression.MatchSessionId.Value != pendingMatch.SessionId ||
                plan == null || plan.Season != 1 || plan.Week != 1 || !plan.IsConfirmed ||
                plan.Slots[0] == null || plan.Slots[1] == null ||
                matchAction == null || !matchAction.IsMatch ||
                !pendingMatch.SourceWeekPlanId.Equals(plan.PlanId) ||
                !pendingMatch.SourceSlotActionId.Equals(matchAction.SlotActionId) ||
                !pendingMatch.SourceActionOccurrenceId.Equals(matchAction.OccurrenceId) ||
                !string.Equals(pendingMatch.ScheduleItemId, matchAction.ContentId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "PendingMatch must exactly bind the completed first-week plan and slot 3 match.",
                    nameof(pendingMatch));
            }

            var isImmediateRestore = identity.RestoredFromVersionToken.HasValue &&
                                     identity.Revision ==
                                     identity.RestoredFromVersionToken.Value.Revision + 1 &&
                                     pendingMatch.CreatedRevision ==
                                     identity.RestoredFromVersionToken.Value.Revision;
            if (!pendingMatch.CreatedLineageId.Equals(identity.LineageId) ||
                (pendingMatch.CreatedRevision != identity.Revision && !isImmediateRestore))
            {
                throw new ArgumentException(
                    "PendingMatch creation identity must equal the containing snapshot, except for its preserved source revision on an immediate restore.",
                    nameof(pendingMatch));
            }

            ValidatePendingCareerIdentity(player, teamId, pendingMatch);

            if (pendingMatch.Versions.ContractVersion != CareerMatchLifecycleVersions.ContractV2 ||
                pendingMatch.Versions.ContractVersion != versions.ContractVersion ||
                pendingMatch.Versions.ContentVersion != versions.ContentVersion ||
                pendingMatch.Versions.RulesetVersion != versions.RulesetVersion ||
                pendingMatch.Versions.CareerRandomAlgorithmVersion !=
                    versions.CareerRandomAlgorithmVersion)
            {
                throw new ArgumentException(
                    "PendingMatch Career-applicable versions must equal snapshot versions.",
                    nameof(pendingMatch));
            }

            var frozen = trainingEmphases.Freeze();
            if (frozen.Count != pendingMatch.FrozenTrainingEmphases.Count)
            {
                throw new ArgumentException(
                    "PendingMatch frozen emphasis must equal the current training ledger.",
                    nameof(pendingMatch));
            }

            for (var index = 0; index < frozen.Count; index++)
            {
                var expected = frozen[index];
                var actual = pendingMatch.FrozenTrainingEmphases[index];
                if (expected.Direction != actual.Direction ||
                    expected.TotalBonusBasisPoints != actual.TotalBonusBasisPoints ||
                    !SlotIdsEqual(expected.SourceSlotActionIds, actual.SourceSlotActionIds))
                {
                    throw new ArgumentException(
                        "PendingMatch frozen emphasis direction, source order and total are derived invariants.",
                        nameof(pendingMatch));
                }
            }

            OperationReceipt creationReceipt = null;
            for (var index = 0; index < operationReceipts.Count; index++)
            {
                if (operationReceipts[index].OperationId.Equals(pendingMatch.CreationOperationId))
                {
                    creationReceipt = operationReceipts[index];
                    break;
                }
            }

            if (creationReceipt == null ||
                creationReceipt.OperationKind != OperationKind.CreatePendingMatch ||
                creationReceipt.AppliedRevision != pendingMatch.CreatedRevision ||
                !creationReceipt.AppliedLineageId.Equals(pendingMatch.CreatedLineageId) ||
                !PendingTargetMatches(creationReceipt.Target, pendingMatch) ||
                creationReceipt.OutcomeKind != OperationOutcomeKind.PendingMatchCreated ||
                creationReceipt.OutcomeSummary.MatchSessionId != pendingMatch.SessionId ||
                !creationReceipt.OutcomeSummary.ContextDigest.HasValue ||
                !creationReceipt.OutcomeSummary.ContextDigest.Value.Equals(pendingMatch.ContextDigest))
            {
                throw new ArgumentException(
                    "PendingMatch requires its exact pending-creation operation receipt.",
                    nameof(operationReceipts));
            }
        }

        private static void ValidatePendingCareerIdentity(
            CareerPlayerRecord player,
            TeamId? teamId,
            PendingCareerMatch pendingMatch)
        {
            if (player == null || !teamId.HasValue ||
                !pendingMatch.ProtagonistPlayerId.Equals(player.PlayerId))
            {
                throw new ArgumentException(
                    "PendingMatch protagonist must equal the current Career player.",
                    nameof(pendingMatch));
            }

            var isHomeTeam = teamId.Value.Equals(pendingMatch.HomeTeamId);
            var isAwayTeam = teamId.Value.Equals(pendingMatch.AwayTeamId);
            if (!isHomeTeam && !isAwayTeam)
            {
                throw new ArgumentException(
                    "PendingMatch must include the current Career team.",
                    nameof(pendingMatch));
            }

            var firstRosterIndex = isHomeTeam ? 0 : 6;
            var rosterContainsPlayer = false;
            for (var index = firstRosterIndex; index < firstRosterIndex + 6; index++)
            {
                if (pendingMatch.OrderedPlayerIds[index].Equals(player.PlayerId))
                {
                    rosterContainsPlayer = true;
                    break;
                }
            }

            if (!rosterContainsPlayer)
            {
                throw new ArgumentException(
                    "PendingMatch protagonist must be in the six-player roster segment owned by the current Career team.",
                    nameof(pendingMatch));
            }
        }

        private static void ValidateSettledCareerState(
            CareerMatchHistoryEntry history,
            CareerPlayerRecord player,
            int? fatigue,
            int? mindset,
            int? coachTrust)
        {
            if (player == null || !fatigue.HasValue || !mindset.HasValue ||
                !coachTrust.HasValue)
            {
                throw new ArgumentException(
                    "Settled match evidence requires complete authoritative Career state.",
                    nameof(history));
            }

            var summary = history.SettlementSummary;
            if (!summary.AfterAttributes.Equals(player.Attributes) ||
                summary.WeekendFatigueChange.NewValue != fatigue.Value ||
                summary.WeekendMindsetChange.NewValue != mindset.Value ||
                summary.WeekendCoachTrustChange.NewValue != coachTrust.Value)
            {
                throw new ArgumentException(
                    "Settled match after-values must equal the authoritative Career snapshot state.",
                    nameof(history));
            }
        }

        private static void ValidateNoOrphanPendingCreationReceipts(
            IReadOnlyList<OperationReceipt> operationReceipts,
            PendingCareerMatch pendingMatch,
            IReadOnlyDictionary<Guid, CareerMatchHistoryEntry> historyBySession)
        {
            var expectedSessions = new HashSet<Guid>(historyBySession.Keys);
            if (pendingMatch != null)
            {
                expectedSessions.Add(pendingMatch.SessionId);
            }

            var actualSessions = new HashSet<Guid>();
            for (var index = 0; index < operationReceipts.Count; index++)
            {
                var receipt = operationReceipts[index];
                if (receipt.OperationKind != OperationKind.CreatePendingMatch)
                {
                    continue;
                }

                if (!receipt.Target.MatchSessionId.HasValue ||
                    !actualSessions.Add(receipt.Target.MatchSessionId.Value) ||
                    !expectedSessions.Contains(receipt.Target.MatchSessionId.Value))
                {
                    throw new ArgumentException(
                        "Every pending-creation operation receipt must belong to the current pending match or settled history.",
                        nameof(operationReceipts));
                }
            }

            if (!actualSessions.SetEquals(expectedSessions))
            {
                throw new ArgumentException(
                    "Every pending or settled match requires exactly one pending-creation operation receipt.",
                    nameof(operationReceipts));
            }
        }

        private static bool PendingTargetMatches(
            OperationReceiptTarget target,
            PendingCareerMatch pending)
        {
            return target.MatchSessionId == pending.SessionId &&
                   target.WeekPlanId.HasValue &&
                   target.WeekPlanId.Value.Equals(pending.SourceWeekPlanId) &&
                   target.SlotActionId.HasValue &&
                   target.SlotActionId.Value.Equals(pending.SourceSlotActionId) &&
                   target.ActionOccurrenceId.HasValue &&
                   target.ActionOccurrenceId.Value.Equals(pending.SourceActionOccurrenceId) &&
                   string.Equals(target.ScheduleItemId, pending.ScheduleItemId, StringComparison.Ordinal) &&
                   target.ContextDigest.HasValue &&
                   target.ContextDigest.Value.Equals(pending.ContextDigest);
        }

        private static bool HistoryMatchesReceipt(
            CareerMatchHistoryEntry history,
            CareerSettlementReceipt receipt)
        {
            return history.ContextDigest.Equals(receipt.ContextDigest) &&
                   history.ResultDigest.Equals(receipt.ResultDigest) &&
                   history.AppliedLineageId.Equals(receipt.AppliedLineageId) &&
                   history.AppliedRevision == receipt.AppliedRevision &&
                   history.SettledAtUtcMs == receipt.SettledAtUtcMs &&
                   history.SettlementSummary.Equals(receipt.SettlementSummary);
        }

        private static void ValidateSettledCreationReceipt(
            CareerMatchHistoryEntry history,
            IReadOnlyList<OperationReceipt> operationReceipts)
        {
            OperationReceipt creation = null;
            for (var index = 0; index < operationReceipts.Count; index++)
            {
                var candidate = operationReceipts[index];
                if (candidate.OperationKind == OperationKind.CreatePendingMatch &&
                    candidate.Target.MatchSessionId == history.SessionId)
                {
                    creation = candidate;
                    break;
                }
            }

            if (creation == null ||
                !creation.Target.WeekPlanId.HasValue ||
                !creation.Target.WeekPlanId.Value.Equals(history.SourceWeekPlanId) ||
                !creation.Target.SlotActionId.HasValue ||
                !creation.Target.SlotActionId.Value.Equals(history.SourceSlotActionId) ||
                !string.Equals(
                    creation.Target.ScheduleItemId,
                    history.ScheduleItemId,
                    StringComparison.Ordinal) ||
                !creation.Target.ContextDigest.HasValue ||
                !creation.Target.ContextDigest.Value.Equals(history.ContextDigest) ||
                creation.OutcomeSummary.MatchSessionId != history.SessionId ||
                !creation.OutcomeSummary.ContextDigest.HasValue ||
                !creation.OutcomeSummary.ContextDigest.Value.Equals(history.ContextDigest) ||
                !creation.AppliedLineageId.Equals(history.AppliedLineageId) ||
                creation.AppliedRevision >= history.AppliedRevision)
            {
                throw new ArgumentException(
                    "Settled match history requires its earlier exact pending-creation receipt.",
                    nameof(operationReceipts));
            }

            ValidateSettledWeekOperationOrder(history, creation, operationReceipts);
        }

        private static void ValidateSettledWeekOperationOrder(
            CareerMatchHistoryEntry history,
            OperationReceipt creation,
            IReadOnlyList<OperationReceipt> operationReceipts)
        {
            OperationReceipt confirmation = null;
            OperationReceipt sourceAction = null;
            OperationReceipt eventChoice = null;
            OperationReceipt secondAction = null;
            var actionCount = 0;
            for (var index = 0; index < operationReceipts.Count; index++)
            {
                var receipt = operationReceipts[index];
                var target = receipt.Target;
                if (!target.WeekPlanId.HasValue ||
                    !target.WeekPlanId.Value.Equals(history.SourceWeekPlanId))
                {
                    continue;
                }

                switch (receipt.OperationKind)
                {
                    case OperationKind.ConfirmWeekPlan:
                        if (confirmation != null) throw InvalidSettledWeekChain();
                        confirmation = receipt;
                        break;
                    case OperationKind.ResolveEventChoice:
                        if (eventChoice != null) throw InvalidSettledWeekChain();
                        eventChoice = receipt;
                        break;
                    case OperationKind.ExecuteWeekAction:
                        actionCount++;
                        break;
                }
            }

            if (confirmation == null || eventChoice == null || actionCount != 2 ||
                !eventChoice.Target.SlotActionId.HasValue ||
                !eventChoice.Target.ActionOccurrenceId.HasValue)
            {
                throw InvalidSettledWeekChain();
            }

            for (var index = 0; index < operationReceipts.Count; index++)
            {
                var receipt = operationReceipts[index];
                if (receipt.OperationKind != OperationKind.ExecuteWeekAction ||
                    !receipt.Target.WeekPlanId.HasValue ||
                    !receipt.Target.WeekPlanId.Value.Equals(history.SourceWeekPlanId))
                {
                    continue;
                }

                if (receipt.Target.SlotActionId.Equals(eventChoice.Target.SlotActionId) &&
                    receipt.Target.ActionOccurrenceId.Equals(eventChoice.Target.ActionOccurrenceId))
                {
                    sourceAction = receipt;
                }
                else
                {
                    if (secondAction != null) throw InvalidSettledWeekChain();
                    secondAction = receipt;
                }
            }

            if (sourceAction == null || secondAction == null ||
                creation.Target.SlotActionId.Equals(sourceAction.Target.SlotActionId) ||
                creation.Target.SlotActionId.Equals(secondAction.Target.SlotActionId) ||
                !(confirmation.AppliedRevision < sourceAction.AppliedRevision &&
                  sourceAction.AppliedRevision < eventChoice.AppliedRevision &&
                  eventChoice.AppliedRevision < secondAction.AppliedRevision &&
                  secondAction.AppliedRevision < creation.AppliedRevision))
            {
                throw InvalidSettledWeekChain();
            }
        }

        private static ArgumentException InvalidSettledWeekChain()
        {
            return new ArgumentException(
                "A settled pending creation must follow confirmation, slot 1, event choice and slot 2 in strict order.",
                "operationReceipts");
        }

        private static bool SlotIdsEqual(
            IReadOnlyList<SlotActionId> left,
            IReadOnlyList<SlotActionId> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }

            return true;
        }

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
            else if (progression.Kind == CareerProgressionKind.Planned ||
                     progression.Kind == CareerProgressionKind.AwaitingMatch)
            {
                if (progression.Kind == CareerProgressionKind.AwaitingMatch)
                {
                    executedFreeSlots = 2;
                }
                else if (progression.NextSlotNumber == 2)
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
                case OperationKind.CreatePendingMatch:
                    return "pending|" + target.MatchSessionId.Value.ToString("D");
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
            if (progression.Kind == CareerProgressionKind.AwaitingMatch)
            {
                var pendingReceipt = FindReceipt(
                    receipts,
                    OperationKind.CreatePendingMatch,
                    plan.PlanId,
                    plan.Slots[2].SlotActionId);
                frontierRevision = RequireLaterRevision(
                    frontierRevision,
                    pendingReceipt,
                    "Pending match creation must strictly follow slot 2 execution.");
            }

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
            var pendingMatchCreationCount = 0;

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

                    case OperationKind.CreatePendingMatch:
                        if (!MatchesAction(target, plan.Slots[2]))
                        {
                            throw new ArgumentException(
                                "Pending match creation must target current-plan slot 3 exactly.",
                                nameof(receipts));
                        }

                        pendingMatchCreationCount++;
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
                    resolvedSlotOneEventCount != 0 || pendingMatchCreationCount != 0)
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
                    if (pendingMatchCreationCount != 0)
                    {
                        throw new ArgumentException(
                            "A Planned state cannot already have a pending-match creation receipt.",
                            nameof(receipts));
                    }

                    ValidatePlannedReceiptFrontier(
                        progression.NextSlotNumber,
                        slotOneExecuted,
                        slotTwoExecuted,
                        resolvedSlotOneEventCount,
                        nameof(receipts));
                    return;

                case CareerProgressionKind.AwaitingEventChoice:
                    if (!slotOneExecuted || slotTwoExecuted || resolvedSlotOneEventCount != 0 ||
                        pendingMatchCreationCount != 0)
                    {
                        throw new ArgumentException(
                            "AwaitingEventChoice requires completed slot 1, no slot 2 execution and no resolved current event.",
                            nameof(receipts));
                    }

                    ValidatePendingEventSource(progression.PendingEvent, plan);
                    return;

                case CareerProgressionKind.AwaitingMatch:
                    if (!slotOneExecuted || !slotTwoExecuted || resolvedSlotOneEventCount != 1 ||
                        pendingMatchCreationCount != 1)
                    {
                        throw new ArgumentException(
                            "AwaitingMatch requires both free slots, the fixed event and one pending creation receipt.",
                            nameof(receipts));
                    }

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
