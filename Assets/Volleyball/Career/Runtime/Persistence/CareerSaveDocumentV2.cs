using System;

namespace Volleyball.Career.Persistence
{
    [Serializable]
    public sealed class CareerSaveDocumentV2
    {
        public CareerSaveVersionsDocumentV2 versions;
        public CareerSaveIdentityDocumentV2 identity;
        public CareerSaveIntegrityDocumentV2 integrity;
        public string careerSeed;
        public string careerName;
        public CareerPlayerDraftDocumentV2 playerDraft;
        public TryoutOnboardingDocumentV2 onboarding;
        public CareerProgressionDocumentV2 progression;
        public TrainingEmphasisContributionDocumentV2[] trainingEmphases;
        public PendingCareerMatchDocumentV2 pendingMatch;
        public CareerPlayerDocumentV2 player;
        public string teamId;
        public string potentialGrade;
        public int? fatigue;
        public int? mindset;
        public int? coachTrust;
        public CareerMatchHistoryEntryDocumentV2[] matchHistory;
        public OperationReceiptDocumentV2[] operationReceipts;
        public CareerSettlementReceiptDocumentV2[] settlementReceipts;
    }

    [Serializable]
    public sealed class CareerSaveVersionsDocumentV2
    {
        public int schemaVersion;
        public int contentVersion;
        public int rulesetVersion;
        public int contractVersion;
        public int careerRandomAlgorithmVersion;
    }

    [Serializable]
    public sealed class CareerSaveIdentityDocumentV2
    {
        public string profileId;
        public string saveId;
        public string lineageId;
        public long revision;
        public CareerVersionTokenDocumentV2 restoredFromVersionToken;
        public long createdAtUtcMs;
        public long updatedAtUtcMs;
    }

    [Serializable]
    public sealed class CareerVersionTokenDocumentV2
    {
        public string lineageId;
        public long revision;
        public string snapshotHash;
    }

    [Serializable]
    public sealed class CareerSaveIntegrityDocumentV2
    {
        public string snapshotHash;
    }

    [Serializable]
    public sealed class CareerPlayerDraftDocumentV2
    {
        public string playerId;
        public string displayName;
        public int jerseyNumber;
    }

    [Serializable]
    public sealed class TryoutOnboardingDocumentV2
    {
        public TryoutStageDocumentV2[] stages;
        public int nextStageNumber;
        public bool isFormallyEnrolled;
    }

    [Serializable]
    public sealed class TryoutStageDocumentV2
    {
        public int stageNumber;
        public string occurrenceId;
        public int randomVersion;
        public string choiceId;
        public TryoutResolvedOutputDocumentV2[] resolvedOutputs;
    }

    [Serializable]
    public sealed class TryoutResolvedOutputDocumentV2
    {
        public string outputId;
        public int perturbation;
    }

    [Serializable]
    public sealed class CareerProgressionDocumentV2
    {
        public string kind;
        public string phase;
        public int tryoutStage;
        public CareerWeekPlanDocumentV2 weekPlan;
        public int nextSlotNumber;
        public PendingCareerEventDocumentV2 pendingEvent;
        public string matchSessionId;
    }

    [Serializable]
    public sealed class CareerWeekPlanDocumentV2
    {
        public string planId;
        public int season;
        public int week;
        public CareerWeekActionDocumentV2[] slots;
        public bool isConfirmed;
    }

    [Serializable]
    public sealed class CareerWeekActionDocumentV2
    {
        public string slotActionId;
        public string occurrenceId;
        public string kind;
        public string contentId;
    }

    [Serializable]
    public sealed class TrainingEmphasisContributionDocumentV2
    {
        public string sourceSlotActionId;
        public string direction;
        public int bonusBasisPoints;
    }

    [Serializable]
    public sealed class PendingCareerMatchDocumentV2
    {
        public string sessionId;
        public string creationOperationId;
        public string createdLineageId;
        public long createdRevision;
        public CareerMatchLifecycleVersionsDocumentV2 versions;
        public string executionMode;
        public string fixtureId;
        public int? fixtureVersion;
        public long matchSeed;
        public string competitionId;
        public string scheduleItemId;
        public string sourceWeekPlanId;
        public string sourceSlotActionId;
        public string sourceActionOccurrenceId;
        public string preMatchPriority;
        public string contextHash;
        public string canonicalContextUtf8Base64;
        public string homeTeamId;
        public string awayTeamId;
        public string[] orderedPlayerIds;
        public string protagonistPlayerId;
        public FrozenCareerTrainingEmphasisDocumentV2[] frozenTrainingEmphases;
    }

    [Serializable]
    public sealed class CareerMatchLifecycleVersionsDocumentV2
    {
        public int contractVersion;
        public int contentVersion;
        public int rulesetVersion;
        public int careerRandomAlgorithmVersion;
        public int? matchSimulationVersion;
        public int? matchRandomAlgorithmVersion;
    }

    [Serializable]
    public sealed class FrozenCareerTrainingEmphasisDocumentV2
    {
        public string direction;
        public string[] sourceSlotActionIds;
        public int totalBonusBasisPoints;
    }

    [Serializable]
    public sealed class PendingCareerEventDocumentV2
    {
        public string sourceWeekPlanId;
        public string sourceSlotActionId;
        public string sourceActionOccurrenceId;
        public string occurrenceId;
        public string eventId;
        public int randomVersion;
        public int resumeAtSlotNumber;
        public CareerEventOptionEffectDocumentV2[] options;
    }

    [Serializable]
    public sealed class CareerEventOptionEffectDocumentV2
    {
        public string optionId;
        public CareerAttributeGrowthDeltaDocumentV2 growthExperienceDelta;
        public int fatigueDelta;
        public int mindsetDelta;
        public int coachTrustDelta;
    }

    [Serializable]
    public sealed class CareerAttributeGrowthDeltaDocumentV2
    {
        public long spike;
        public long serve;
        public long reception;
        public long defense;
        public long block;
        public long movement;
        public long jump;
        public long stamina;
    }

    [Serializable]
    public sealed class CareerPlayerDocumentV2
    {
        public string playerId;
        public string displayName;
        public int jerseyNumber;
        public CareerPlayerAttributesDocumentV2 attributes;
    }

    [Serializable]
    public sealed class CareerPlayerAttributesDocumentV2
    {
        public CareerAttributeProgressDocumentV2 spike;
        public CareerAttributeProgressDocumentV2 serve;
        public CareerAttributeProgressDocumentV2 reception;
        public CareerAttributeProgressDocumentV2 defense;
        public CareerAttributeProgressDocumentV2 block;
        public CareerAttributeProgressDocumentV2 movement;
        public CareerAttributeProgressDocumentV2 jump;
        public CareerAttributeProgressDocumentV2 stamina;
    }

    [Serializable]
    public sealed class CareerAttributeProgressDocumentV2
    {
        public int abilityBasisPoints;
        public long growthExperience;
    }

    [Serializable]
    public sealed class CareerMatchHistoryEntryDocumentV2
    {
        public string sessionId;
        public string scheduleItemId;
        public string sourceWeekPlanId;
        public string sourceSlotActionId;
        public string contextHash;
        public string resultHash;
        public string canonicalContextUtf8Base64;
        public string canonicalResultUtf8Base64;
        public string appliedLineageId;
        public long appliedRevision;
        public long settledAtUtcMs;
        public CareerSettlementSummaryDocumentV2 settlementSummary;
    }

    [Serializable]
    public sealed class CareerSettlementReceiptDocumentV2
    {
        public string sessionId;
        public string contextHash;
        public string resultHash;
        public string appliedLineageId;
        public long appliedRevision;
        public long settledAtUtcMs;
        public CareerSettlementSummaryDocumentV2 settlementSummary;
    }

    [Serializable]
    public sealed class CareerSettlementSummaryDocumentV2
    {
        public CareerMatchSetScoreSummaryDocumentV2[] sets;
        public CareerProtagonistMatchFactsDocumentV2 protagonistFacts;
        public string selectedPriority;
        public bool priorityExecuted;
        public bool won;
        public CareerAttributeGrowthChangeDocumentV2[] growthChanges;
        public CareerReasonedIntegerChangeDocumentV2 matchFatigueChange;
        public CareerReasonedIntegerChangeDocumentV2 matchMindsetChange;
        public CareerReasonedIntegerChangeDocumentV2 matchCoachTrustChange;
        public CareerReasonedIntegerChangeDocumentV2 weekendFatigueChange;
        public CareerReasonedIntegerChangeDocumentV2 weekendMindsetChange;
        public CareerReasonedIntegerChangeDocumentV2 weekendCoachTrustChange;
    }

    [Serializable]
    public sealed class CareerMatchSetScoreSummaryDocumentV2
    {
        public int setNumber;
        public int homePoints;
        public int awayPoints;
        public bool isComplete;
    }

    [Serializable]
    public sealed class CareerProtagonistMatchFactsDocumentV2
    {
        public CareerSpikeFactSummaryDocumentV2 spike;
        public CareerServeFactSummaryDocumentV2 serve;
        public CareerReceptionFactSummaryDocumentV2 reception;
        public CareerDefenseFactSummaryDocumentV2 defense;
        public CareerBlockFactSummaryDocumentV2 block;
        public CareerMatchLoadFactSummaryDocumentV2 load;
        public CareerStabilityFactSummaryDocumentV2 stability;
    }

    [Serializable]
    public sealed class CareerSpikeFactSummaryDocumentV2
    {
        public int attempts;
        public int points;
        public int errors;
    }

    [Serializable]
    public sealed class CareerServeFactSummaryDocumentV2
    {
        public int attempts;
        public int aces;
        public int errors;
    }

    [Serializable]
    public sealed class CareerReceptionFactSummaryDocumentV2
    {
        public int attempts;
        public int perfect;
        public int positive;
        public int neutral;
        public int negative;
        public int errors;
    }

    [Serializable]
    public sealed class CareerDefenseFactSummaryDocumentV2
    {
        public int attempts;
        public int successes;
    }

    [Serializable]
    public sealed class CareerBlockFactSummaryDocumentV2
    {
        public int attempts;
        public int effectiveTouches;
        public int points;
    }

    [Serializable]
    public sealed class CareerMatchLoadFactSummaryDocumentV2
    {
        public int ralliesPlayed;
        public long activeDurationMilliseconds;
        public long movementDistanceMillimeters;
        public int jumpCount;
        public int highLoadJumpCount;
        public int landingLoadBasisPoints;
        public int totalWorkloadBasisPoints;
    }

    [Serializable]
    public sealed class CareerStabilityFactSummaryDocumentV2
    {
        public int criticalActions;
        public int criticalSuccesses;
        public int criticalErrors;
        public int errorStreakEpisodes;
        public int longestErrorStreak;
    }

    [Serializable]
    public sealed class CareerAttributeGrowthChangeDocumentV2
    {
        public string attribute;
        public string reasonId;
        public CareerAttributeProgressDocumentV2 before;
        public long requestedDelta;
        public long actualDelta;
        public CareerAttributeProgressDocumentV2 after;
    }

    [Serializable]
    public sealed class CareerReasonedIntegerChangeDocumentV2
    {
        public string reasonId;
        public int oldValue;
        public int requestedDelta;
        public int actualDelta;
        public int newValue;
    }

    [Serializable]
    public sealed class OperationReceiptDocumentV2
    {
        public string operationId;
        public string operationKind;
        public OperationReceiptTargetDocumentV2 target;
        public string inputFingerprint;
        public string appliedLineageId;
        public long appliedRevision;
        public long completedAtUtcMs;
        public string outcomeKind;
        public OperationOutcomeSummaryDocumentV2 outcomeSummary;
    }

    [Serializable]
    public sealed class OperationReceiptTargetDocumentV2
    {
        public int tryoutStage;
        public string tryoutOccurrenceId;
        public string choiceId;
        public string weekPlanId;
        public string slotActionId;
        public string actionOccurrenceId;
        public string eventOccurrenceId;
        public string optionId;
        public string matchSessionId;
        public string scheduleItemId;
        public string contextHash;
    }

    [Serializable]
    public sealed class OperationOutcomeSummaryDocumentV2
    {
        public TryoutResolvedOutputDocumentV2[] tryoutResolvedOutputs;
        public CareerAttributeGrowthDeltaDocumentV2 growthExperienceDelta;
        public int? fatigueDelta;
        public int? mindsetDelta;
        public int? coachTrustDelta;
        public string matchSessionId;
        public string contextHash;
    }
}
