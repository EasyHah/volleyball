using System;

namespace Volleyball.Career.Persistence
{
    [Serializable]
    public sealed class CareerSaveDocumentV1
    {
        public CareerSaveVersionsDocumentV1 versions;
        public CareerSaveIdentityDocumentV1 identity;
        public CareerSaveIntegrityDocumentV1 integrity;
        public string careerSeed;
        public string careerName;
        public CareerPlayerDraftDocumentV1 playerDraft;
        public TryoutOnboardingDocumentV1 onboarding;
        public CareerProgressionDocumentV1 progression;
        public TrainingEmphasisContributionDocumentV1[] trainingEmphases;
        public CareerPlayerDocumentV1 player;
        public string teamId;
        public string potentialGrade;
        public int? fatigue;
        public int? mindset;
        public int? coachTrust;
        public OperationReceiptDocumentV1[] operationReceipts;
    }

    [Serializable]
    public sealed class CareerSaveVersionsDocumentV1
    {
        public int schemaVersion;
        public int contentVersion;
        public int rulesetVersion;
        public int careerRandomAlgorithmVersion;
    }

    [Serializable]
    public sealed class CareerSaveIdentityDocumentV1
    {
        public string profileId;
        public string saveId;
        public string lineageId;
        public long revision;
        public CareerVersionTokenDocumentV1 restoredFromVersionToken;
        public long createdAtUtcMs;
        public long updatedAtUtcMs;
    }

    [Serializable]
    public sealed class CareerVersionTokenDocumentV1
    {
        public string lineageId;
        public long revision;
        public string snapshotHash;
    }

    [Serializable]
    public sealed class CareerSaveIntegrityDocumentV1
    {
        public string snapshotHash;
    }

    [Serializable]
    public sealed class CareerPlayerDraftDocumentV1
    {
        public string playerId;
        public string displayName;
        public int jerseyNumber;
    }

    [Serializable]
    public sealed class TryoutOnboardingDocumentV1
    {
        public TryoutStageDocumentV1[] stages;
        public int nextStageNumber;
        public bool isFormallyEnrolled;
    }

    [Serializable]
    public sealed class TryoutStageDocumentV1
    {
        public int stageNumber;
        public string occurrenceId;
        public int randomVersion;
        public string choiceId;
        public TryoutResolvedOutputDocumentV1[] resolvedOutputs;
    }

    [Serializable]
    public sealed class TryoutResolvedOutputDocumentV1
    {
        public string outputId;
        public int perturbation;
    }

    [Serializable]
    public sealed class CareerProgressionDocumentV1
    {
        public string kind;
        public string phase;
        public int tryoutStage;
        public CareerWeekPlanDocumentV1 weekPlan;
        public int nextSlotNumber;
        public PendingCareerEventDocumentV1 pendingEvent;
    }

    [Serializable]
    public sealed class CareerWeekPlanDocumentV1
    {
        public string planId;
        public int season;
        public int week;
        public CareerWeekActionDocumentV1[] slots;
        public bool isConfirmed;
    }

    [Serializable]
    public sealed class CareerWeekActionDocumentV1
    {
        public string slotActionId;
        public string occurrenceId;
        public string kind;
        public string contentId;
    }

    [Serializable]
    public sealed class TrainingEmphasisContributionDocumentV1
    {
        public string sourceSlotActionId;
        public string direction;
        public int bonusBasisPoints;
    }

    [Serializable]
    public sealed class PendingCareerEventDocumentV1
    {
        public string sourceWeekPlanId;
        public string sourceSlotActionId;
        public string sourceActionOccurrenceId;
        public string occurrenceId;
        public string eventId;
        public int randomVersion;
        public int resumeAtSlotNumber;
        public CareerEventOptionEffectDocumentV1[] options;
    }

    [Serializable]
    public sealed class CareerEventOptionEffectDocumentV1
    {
        public string optionId;
        public CareerAttributeGrowthDeltaDocumentV1 growthExperienceDelta;
        public int fatigueDelta;
        public int mindsetDelta;
        public int coachTrustDelta;
    }

    [Serializable]
    public sealed class CareerAttributeGrowthDeltaDocumentV1
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
    public sealed class CareerPlayerDocumentV1
    {
        public string playerId;
        public string displayName;
        public int jerseyNumber;
        public CareerPlayerAttributesDocumentV1 attributes;
    }

    [Serializable]
    public sealed class CareerPlayerAttributesDocumentV1
    {
        public CareerAttributeProgressDocumentV1 spike;
        public CareerAttributeProgressDocumentV1 serve;
        public CareerAttributeProgressDocumentV1 reception;
        public CareerAttributeProgressDocumentV1 defense;
        public CareerAttributeProgressDocumentV1 block;
        public CareerAttributeProgressDocumentV1 movement;
        public CareerAttributeProgressDocumentV1 jump;
        public CareerAttributeProgressDocumentV1 stamina;
    }

    [Serializable]
    public sealed class CareerAttributeProgressDocumentV1
    {
        public int abilityBasisPoints;
        public long growthExperience;
    }

    [Serializable]
    public sealed class OperationReceiptDocumentV1
    {
        public string operationId;
        public string operationKind;
        public OperationReceiptTargetDocumentV1 target;
        public string inputFingerprint;
        public string appliedLineageId;
        public long appliedRevision;
        public long completedAtUtcMs;
        public string outcomeKind;
        public OperationOutcomeSummaryDocumentV1 outcomeSummary;
    }

    [Serializable]
    public sealed class OperationReceiptTargetDocumentV1
    {
        public int tryoutStage;
        public string tryoutOccurrenceId;
        public string choiceId;
        public string weekPlanId;
        public string slotActionId;
        public string actionOccurrenceId;
        public string eventOccurrenceId;
        public string optionId;
    }

    [Serializable]
    public sealed class OperationOutcomeSummaryDocumentV1
    {
        public TryoutResolvedOutputDocumentV1[] tryoutResolvedOutputs;
        public CareerAttributeGrowthDeltaDocumentV1 growthExperienceDelta;
        public int? fatigueDelta;
        public int? mindsetDelta;
        public int? coachTrustDelta;
    }
}
