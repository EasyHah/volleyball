using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class ReplayActualSampleRecordV4
    {
        public ReplayActualSampleRecordV4(
            string envelopeIdentity,
            string samplingKey,
            string candidateCategory,
            ReplayVector3RecordV4 target,
            ReplayVector3RecordV4 velocity,
            float effort)
        {
            EnvelopeIdentity = ReplayContractGuardV4.Hash(
                envelopeIdentity,
                nameof(envelopeIdentity));
            SamplingKey = ReplayContractGuardV4.Required(
                samplingKey,
                nameof(samplingKey));
            CandidateCategory = ReplayContractGuardV4.ExecutionCandidateCategory(
                candidateCategory,
                nameof(candidateCategory));
            Target = target ??
                throw new ContractValidationException("target is required.");
            Velocity = velocity ??
                throw new ContractValidationException("velocity is required.");
            Effort = ReplayContractGuardV4.Finite(effort, nameof(effort));
        }

        public string EnvelopeIdentity { get; }
        public string SamplingKey { get; }
        public string CandidateCategory { get; }
        public ReplayVector3RecordV4 Target { get; }
        public ReplayVector3RecordV4 Velocity { get; }
        public float Effort { get; }
    }

    public sealed class ReplaySampleClassificationRecordV4
    {
        private readonly string[] _offendingDimensions;

        public ReplaySampleClassificationRecordV4(
            string kind,
            string testedEnvelopeIdentity,
            string expandedEnvelopeIdentity,
            ReplayActualSampleRecordV4 actualSample,
            IReadOnlyList<string> offendingDimensions)
        {
            Kind = ReplayContractGuardV4.OneOf(
                kind,
                nameof(kind),
                "UnexpectedExecutionSample",
                "Accepted",
                "EnvelopeExpanded",
                "EnvelopeExceeded");
            TestedEnvelopeIdentity = ReplayContractGuardV4.Hash(
                testedEnvelopeIdentity,
                nameof(testedEnvelopeIdentity));
            ExpandedEnvelopeIdentity = string.IsNullOrEmpty(expandedEnvelopeIdentity)
                ? string.Empty
                : ReplayContractGuardV4.Hash(
                    expandedEnvelopeIdentity,
                    nameof(expandedEnvelopeIdentity));
            ActualSample = actualSample ??
                throw new ContractValidationException("actualSample is required.");
            if (ActualSample.EnvelopeIdentity != TestedEnvelopeIdentity)
            {
                throw new ContractValidationException(
                    "Actual sample must identify the tested envelope.");
            }

            _offendingDimensions = ReplayContractGuardV4.CopyRequiredStrings(
                offendingDimensions,
                nameof(offendingDimensions),
                allowEmpty: true);
            if (Kind == "Accepted" && _offendingDimensions.Length != 0)
            {
                throw new ContractValidationException(
                    "Accepted classifications cannot contain offending dimensions.");
            }

            if (Kind == "EnvelopeExpanded" &&
                string.IsNullOrEmpty(ExpandedEnvelopeIdentity))
            {
                throw new ContractValidationException(
                    "EnvelopeExpanded requires expandedEnvelopeIdentity.");
            }

            if (Kind != "EnvelopeExpanded" &&
                !string.IsNullOrEmpty(ExpandedEnvelopeIdentity))
            {
                throw new ContractValidationException(
                    "Only EnvelopeExpanded may identify an expanded envelope.");
            }
        }

        public string Kind { get; }
        public string TestedEnvelopeIdentity { get; }
        public string ExpandedEnvelopeIdentity { get; }
        public ReplayActualSampleRecordV4 ActualSample { get; }
        public IReadOnlyList<string> OffendingDimensions =>
            new ReadOnlyCollection<string>(_offendingDimensions);
    }

    public sealed class ReplayObservedP6GeometryRecordV4
    {
        public ReplayObservedP6GeometryRecordV4(
            string actorPlayerId,
            string teamSide,
            ReplayVector3RecordV4 takeoffPoint,
            ReplayVector3RecordV4 contactPoint,
            float attackLineDistanceFromCenter,
            float netHeight)
        {
            ActorPlayerId = ReplayContractGuardV4.Required(
                actorPlayerId,
                nameof(actorPlayerId));
            TeamSide = ReplayContractGuardV4.OneOf(
                teamSide,
                nameof(teamSide),
                "Home",
                "Away");
            TakeoffPoint = takeoffPoint ??
                throw new ContractValidationException("takeoffPoint is required.");
            ContactPoint = contactPoint ??
                throw new ContractValidationException("contactPoint is required.");
            AttackLineDistanceFromCenter = ReplayContractGuardV4.PositiveFinite(
                attackLineDistanceFromCenter,
                nameof(attackLineDistanceFromCenter));
            NetHeight = ReplayContractGuardV4.PositiveFinite(
                netHeight,
                nameof(netHeight));
        }

        public string ActorPlayerId { get; }
        public string TeamSide { get; }
        public ReplayVector3RecordV4 TakeoffPoint { get; }
        public ReplayVector3RecordV4 ContactPoint { get; }
        public float AttackLineDistanceFromCenter { get; }
        public float NetHeight { get; }
        public bool IsTakeoffInFrontZone =>
            TeamSide == "Home"
                ? TakeoffPoint.Z >= -AttackLineDistanceFromCenter &&
                  TakeoffPoint.Z <= 0f
                : TakeoffPoint.Z <= AttackLineDistanceFromCenter &&
                  TakeoffPoint.Z >= 0f;
        public bool IsContactAboveNet => ContactPoint.Y > NetHeight;
    }

    public sealed class ReplayRuleDecisionRecordV4
    {
        public ReplayRuleDecisionRecordV4(
            int rulesVersion,
            bool accepted,
            string reasonCode)
        {
            if (rulesVersion != RulesVersions.FullRallyV3)
            {
                throw new ContractValidationException(
                    "Replay rule decisions must identify V3 rules.");
            }

            RulesVersion = rulesVersion;
            Accepted = accepted;
            ReasonCode = ReplayContractGuardV4.OneOf(
                reasonCode,
                nameof(reasonCode),
                "None",
                "DuplicateContactGroup",
                "RallyClosed",
                "ConsecutiveCountedContact",
                "FourthCountedContact",
                "ActorNotOnCourt",
                "ActionIneligible");
            if (Accepted != (ReasonCode == "None"))
            {
                throw new ContractValidationException(
                    "Accepted rule decisions require None; rejected decisions require a reason code.");
            }
        }

        public int RulesVersion { get; }
        public bool Accepted { get; }
        public string ReasonCode { get; }
    }

    public sealed class ReplayShadowAssignmentRecordV4
    {
        public ReplayShadowAssignmentRecordV4(
            int rank,
            string playerId,
            string task,
            string condition,
            string spatialClaim,
            string declaredBranch,
            float value)
        {
            Rank = ReplayContractGuardV4.Positive(rank, nameof(rank));
            PlayerId = ReplayContractGuardV4.Required(playerId, nameof(playerId));
            Task = ReplayContractGuardV4.OneOf(
                task,
                nameof(task),
                "Receive",
                "Set",
                "Attack",
                "Block",
                "Cover");
            Condition = ReplayContractGuardV4.OneOf(
                condition,
                nameof(condition),
                "Always",
                "IfBallIncoming",
                "IfSetAvailable",
                "IfAttackIncoming");
            SpatialClaim = ReplayContractGuardV4.OneOf(
                spatialClaim,
                nameof(spatialClaim),
                "CourtZone",
                "NetZone",
                "BackcourtZone",
                "AttackLane");
            DeclaredBranch = ReplayContractGuardV4.OneOf(
                declaredBranch,
                nameof(declaredBranch),
                "Primary",
                "Fallback");
            Value = ReplayContractGuardV4.Finite(value, nameof(value));
        }

        public int Rank { get; }
        public string PlayerId { get; }
        public string Task { get; }
        public string Condition { get; }
        public string SpatialClaim { get; }
        public string DeclaredBranch { get; }
        public float Value { get; }
    }

    public sealed class ReplayTeamRallyPlanRecordV4
    {
        private readonly ReplayShadowAssignmentRecordV4[] _primaryAssignments;

        public ReplayTeamRallyPlanRecordV4(
            string teamSide,
            IReadOnlyList<ReplayShadowAssignmentRecordV4> primaryAssignments)
        {
            TeamSide = ReplayContractGuardV4.OneOf(
                teamSide,
                nameof(teamSide),
                "Home",
                "Away");
            if (primaryAssignments == null || primaryAssignments.Count != 6)
            {
                throw new ContractValidationException(
                    "primaryAssignments must contain exactly six records.");
            }

            _primaryAssignments = new ReplayShadowAssignmentRecordV4[6];
            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < _primaryAssignments.Length; index++)
            {
                var assignment = primaryAssignments[index] ??
                    throw new ContractValidationException(
                        "primaryAssignments cannot contain null records.");
                if (assignment.DeclaredBranch != "Primary" ||
                    assignment.Rank != index + 1 ||
                    !playerIds.Add(assignment.PlayerId))
                {
                    throw new ContractValidationException(
                        "primaryAssignments must be distinct rank-ordered primary-branch assignments.");
                }

                _primaryAssignments[index] = assignment;
            }
        }

        public string TeamSide { get; }
        public IReadOnlyList<ReplayShadowAssignmentRecordV4> PrimaryAssignments =>
            new ReadOnlyCollection<ReplayShadowAssignmentRecordV4>(
                _primaryAssignments);
    }

    public sealed class ReplayCoverageDecisionRecordV4
    {
        public ReplayCoverageDecisionRecordV4(string decision, float score)
            : this(decision, score, "WithinConditionalEnvelope", Array.Empty<string>(), 0, null)
        {
        }

        public ReplayCoverageDecisionRecordV4(
            string decision,
            float score,
            string reason,
            IReadOnlyList<string> invalidationSet,
            int expansionDepth,
            string activatedDeclaredBranch)
        {
            Decision = ReplayContractGuardV4.OneOf(
                decision,
                nameof(decision),
                "Covered",
                "Local",
                "Scoped",
                "Global",
                "Terminal");
            Score = ReplayContractGuardV4.Finite(score, nameof(score));
            Reason = ReplayContractGuardV4.OneOf(
                reason,
                nameof(reason),
                "WithinConditionalEnvelope",
                "ResponsibleActorChanged",
                "BallEnvelopeExceeded",
                "EnvelopeExceeded",
                "EnvelopeExpanded",
                "UnexpectedExecutionSample",
                "RulesStateChanged",
                "CommittedResponsibilityInvalidated",
                "DependencyCascadeExceeded",
                "BudgetDegradationRequired",
                "RallyOpen",
                "RallyEnd");
            InvalidationSet = new ReadOnlyCollection<string>(
                ReplayContractGuardV4.CopyRequiredStrings(
                    invalidationSet,
                    nameof(invalidationSet),
                    true));
            if (expansionDepth < 0)
            {
                throw new ContractValidationException(
                    "expansionDepth cannot be negative.");
            }

            ExpansionDepth = expansionDepth;
            ActivatedDeclaredBranch = activatedDeclaredBranch == null
                ? null
                : ReplayContractGuardV4.OneOf(
                    activatedDeclaredBranch,
                    nameof(activatedDeclaredBranch),
                    "Primary",
                    "Contingency");
        }

        public string Decision { get; }
        public float Score { get; }
        public string Reason { get; }
        public IReadOnlyList<string> InvalidationSet { get; }
        public int ExpansionDepth { get; }
        public string ActivatedDeclaredBranch { get; }
    }

    public sealed class ReplayOrganizationAuthorityRecordV4
    {
        public ReplayOrganizationAuthorityRecordV4(
            int planRevision,
            int sourceSequenceNumber,
            string authorityPhase,
            ReplayVector3RecordV4 organizationTarget,
            ReplayVector3RecordV4 actualFirstPassLanding,
            string zoneGrade,
            string registeredSetterPlayerId,
            string setterStatus,
            float setterMovementMeters,
            float setterReactionDelaySeconds,
            float setterReachMarginMeters,
            string organizerPlayerId,
            string fallbackReason,
            string activatedBranch,
            string testedEnvelopeIdentity,
            string executableEnvelopeIdentity,
            string sampleEnvelopeIdentity,
            string trajectoryArtifactIdentity,
            ReplayCoverageDecisionRecordV4 coverage)
        {
            PlanRevision = ReplayContractGuardV4.NonNegative(
                planRevision,
                nameof(planRevision));
            SourceSequenceNumber = ReplayContractGuardV4.Positive(
                sourceSequenceNumber,
                nameof(sourceSequenceNumber));
            AuthorityPhase = ReplayContractGuardV4.OneOf(
                authorityPhase,
                nameof(authorityPhase),
                "Receive",
                "Organize");
            OrganizationTarget = organizationTarget ??
                throw new ContractValidationException(
                    "organizationTarget is required.");
            ActualFirstPassLanding = actualFirstPassLanding;
            ZoneGrade = ReplayContractGuardV4.OneOf(
                zoneGrade,
                nameof(zoneGrade),
                "Best",
                "Secondary",
                "Poor");
            RegisteredSetterPlayerId = ReplayContractGuardV4.Required(
                registeredSetterPlayerId,
                nameof(registeredSetterPlayerId));
            SetterStatus = ReplayContractGuardV4.OneOf(
                setterStatus,
                nameof(setterStatus),
                "Reachable",
                "PreviousTouch",
                "Unavailable",
                "Illegal",
                "Unreachable");
            SetterMovementMeters = NonNegativeFinite(
                setterMovementMeters,
                nameof(setterMovementMeters));
            SetterReactionDelaySeconds = NonNegativeFinite(
                setterReactionDelaySeconds,
                nameof(setterReactionDelaySeconds));
            SetterReachMarginMeters = ReplayContractGuardV4.Finite(
                setterReachMarginMeters,
                nameof(setterReachMarginMeters));
            OrganizerPlayerId = string.IsNullOrWhiteSpace(organizerPlayerId)
                ? null
                : ReplayContractGuardV4.Required(
                    organizerPlayerId,
                    nameof(organizerPlayerId));
            FallbackReason = ReplayContractGuardV4.OneOf(
                fallbackReason,
                nameof(fallbackReason),
                "None",
                "SetterPreviousTouch",
                "SetterUnavailable",
                "SetterIllegal",
                "SetterUnreachable",
                "NoLegalOrganizer");
            ActivatedBranch = activatedBranch == null
                ? null
                : ReplayContractGuardV4.OneOf(
                    activatedBranch,
                    nameof(activatedBranch),
                    "Primary",
                    "Contingency");
            TestedEnvelopeIdentity = ReplayContractGuardV4.Hash(
                testedEnvelopeIdentity,
                nameof(testedEnvelopeIdentity));
            ExecutableEnvelopeIdentity = ReplayContractGuardV4.Hash(
                executableEnvelopeIdentity,
                nameof(executableEnvelopeIdentity));
            SampleEnvelopeIdentity = ReplayContractGuardV4.Hash(
                sampleEnvelopeIdentity,
                nameof(sampleEnvelopeIdentity));
            TrajectoryArtifactIdentity = ReplayContractGuardV4.Hash(
                trajectoryArtifactIdentity,
                nameof(trajectoryArtifactIdentity));
            Coverage = coverage ??
                throw new ContractValidationException("coverage is required.");

            if (FallbackReason == "None" &&
                !string.Equals(
                    OrganizerPlayerId,
                    RegisteredSetterPlayerId,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "None fallback requires the registered setter organizer.");
            }

            if (FallbackReason == "NoLegalOrganizer" &&
                OrganizerPlayerId != null)
            {
                throw new ContractValidationException(
                    "NoLegalOrganizer cannot identify an organizer.");
            }
        }

        public int PlanRevision { get; }
        public int SourceSequenceNumber { get; }
        public string AuthorityPhase { get; }
        public ReplayVector3RecordV4 OrganizationTarget { get; }
        public ReplayVector3RecordV4 ActualFirstPassLanding { get; }
        public string ZoneGrade { get; }
        public string RegisteredSetterPlayerId { get; }
        public string SetterStatus { get; }
        public float SetterMovementMeters { get; }
        public float SetterReactionDelaySeconds { get; }
        public float SetterReachMarginMeters { get; }
        public string OrganizerPlayerId { get; }
        public string FallbackReason { get; }
        public string ActivatedBranch { get; }
        public string TestedEnvelopeIdentity { get; }
        public string ExecutableEnvelopeIdentity { get; }
        public string SampleEnvelopeIdentity { get; }
        public string TrajectoryArtifactIdentity { get; }
        public ReplayCoverageDecisionRecordV4 Coverage { get; }

        private static float NonNegativeFinite(float value, string parameterName)
        {
            value = ReplayContractGuardV4.Finite(value, parameterName);
            if (value < 0f)
            {
                throw new ContractValidationException(
                    parameterName + " must be non-negative.");
            }

            return value;
        }
    }

    public sealed class ReplayShadowRecordV4
    {
        public ReplayShadowRecordV4(
            int revision,
            int sourceSequenceNumber,
            string artifactIdentity,
            ReplayTeamRallyPlanRecordV4 home,
            ReplayTeamRallyPlanRecordV4 away,
            ReplayCoverageDecisionRecordV4 coverage)
        {
            Revision = ReplayContractGuardV4.NonNegative(revision, nameof(revision));
            if (sourceSequenceNumber < 1)
            {
                throw new ContractValidationException(
                    "sourceSequenceNumber must be positive.");
            }

            SourceSequenceNumber = sourceSequenceNumber;
            ArtifactIdentity = ReplayContractGuardV4.Hash(
                artifactIdentity,
                nameof(artifactIdentity));
            Home = home ?? throw new ContractValidationException("home is required.");
            Away = away ?? throw new ContractValidationException("away is required.");
            Coverage = coverage ??
                throw new ContractValidationException("coverage is required.");
            if (Home.TeamSide != "Home" || Away.TeamSide != "Away")
            {
                throw new ContractValidationException(
                    "Shadow records require exactly one Home and one Away plan.");
            }
        }

        public int Revision { get; }
        public int SourceSequenceNumber { get; }
        public string ArtifactIdentity { get; }
        public ReplayTeamRallyPlanRecordV4 Home { get; }
        public ReplayTeamRallyPlanRecordV4 Away { get; }
        public ReplayCoverageDecisionRecordV4 Coverage { get; }
    }

    public sealed class ReplayAttackDefenseCandidateRecordV4
    {
        public ReplayAttackDefenseCandidateRecordV4(
            string candidateIdentity,
            string actorPlayerId,
            string actionClass,
            ReplayVector3RecordV4 target,
            float expectedRallyValue,
            float legalSampleRatio,
            bool isQualifiedPowerRoute,
            string eliminationReason,
            string envelopeIdentity,
            string trajectoryArtifactIdentity,
            string reorganizationExitIdentity)
        {
            CandidateIdentity = ReplayContractGuardV4.Required(candidateIdentity, nameof(candidateIdentity));
            ActorPlayerId = ReplayContractGuardV4.Required(actorPlayerId, nameof(actorPlayerId));
            ActionClass = ReplayContractGuardV4.OneOf(actionClass, nameof(actionClass),
                "PowerLine", "PowerCross", "PowerEdge", "PowerOverHand", "Tip", "Roll",
                "Push", "HighSurvival", "BlockOut", "BlockToolRecovery");
            Target = target ?? throw new ContractValidationException("target is required.");
            ExpectedRallyValue = ReplayContractGuardV4.Finite(expectedRallyValue, nameof(expectedRallyValue));
            LegalSampleRatio = ReplayContractGuardV4.Finite(legalSampleRatio, nameof(legalSampleRatio));
            if (LegalSampleRatio < 0f || LegalSampleRatio > 1f)
                throw new ContractValidationException("legalSampleRatio must be in [0, 1].");
            IsQualifiedPowerRoute = isQualifiedPowerRoute;
            EliminationReason = eliminationReason == null ? string.Empty : eliminationReason.Trim();
            EnvelopeIdentity = ReplayContractGuardV4.Hash(envelopeIdentity, nameof(envelopeIdentity));
            TrajectoryArtifactIdentity = ReplayContractGuardV4.Hash(trajectoryArtifactIdentity, nameof(trajectoryArtifactIdentity));
            ReorganizationExitIdentity = reorganizationExitIdentity == null ? string.Empty : reorganizationExitIdentity.Trim();
            if (ActionClass != "BlockToolRecovery" && !string.IsNullOrEmpty(ReorganizationExitIdentity))
                throw new ContractValidationException("Only BlockToolRecovery may identify a reorganization exit.");
        }

        public string CandidateIdentity { get; }
        public string ActorPlayerId { get; }
        public string ActionClass { get; }
        public ReplayVector3RecordV4 Target { get; }
        public float ExpectedRallyValue { get; }
        public float LegalSampleRatio { get; }
        public bool IsQualifiedPowerRoute { get; }
        public string EliminationReason { get; }
        public string EnvelopeIdentity { get; }
        public string TrajectoryArtifactIdentity { get; }
        public string ReorganizationExitIdentity { get; }
    }

    // This deliberately exposes only public class/zone/probability/time facts.
    // It cannot carry the hidden final route or a future trajectory sample.
    public sealed class ReplayPublicAttackThreatRecordV4
    {
        public ReplayPublicAttackThreatRecordV4(string actionClass, string zone,
            float probability, float arrivalTime)
        {
            ActionClass = ReplayContractGuardV4.OneOf(actionClass, nameof(actionClass),
                "PowerLine", "PowerCross", "PowerEdge", "PowerOverHand", "Tip", "Roll",
                "Push", "HighSurvival", "BlockOut", "BlockToolRecovery");
            Zone = ReplayContractGuardV4.Required(zone, nameof(zone));
            Probability = ReplayContractGuardV4.Finite(probability, nameof(probability));
            if (Probability < 0f || Probability > 1f)
                throw new ContractValidationException("probability must be in [0, 1].");
            ArrivalTime = ReplayContractGuardV4.Finite(arrivalTime, nameof(arrivalTime));
        }

        public string ActionClass { get; }
        public string Zone { get; }
        public float Probability { get; }
        public float ArrivalTime { get; }
    }

    public sealed class ReplayDefenseResponsibilityRecordV4
    {
        public ReplayDefenseResponsibilityRecordV4(string actorPlayerId, string kind,
            string zone, string branch)
        {
            ActorPlayerId = ReplayContractGuardV4.Required(actorPlayerId, nameof(actorPlayerId));
            Kind = ReplayContractGuardV4.OneOf(kind, nameof(kind), "PrimaryBlock",
                "SupportingBlock", "LineDefense", "CrossDefense", "DeepDefense",
                "TipDefense", "BlockShadow", "ReboundCoverage");
            Zone = ReplayContractGuardV4.Required(zone, nameof(zone));
            Branch = ReplayContractGuardV4.OneOf(branch, nameof(branch), "Primary", "Contingency");
        }

        public string ActorPlayerId { get; }
        public string Kind { get; }
        public string Zone { get; }
        public string Branch { get; }
        internal string Identity => ActorPlayerId + "\n" + Kind + "\n" + Zone + "\n" + Branch;
    }

    public sealed class ReplayAttackCoverageResponsibilityRecordV4
    {
        public ReplayAttackCoverageResponsibilityRecordV4(
            string actorPlayerId,
            string branch)
        {
            ActorPlayerId = ReplayContractGuardV4.Required(
                actorPlayerId,
                nameof(actorPlayerId));
            Branch = ReplayContractGuardV4.OneOf(
                branch,
                nameof(branch),
                "Primary",
                "Contingency");
        }

        public string ActorPlayerId { get; }
        public string Branch { get; }
        internal string Identity => ActorPlayerId + "\n" + Branch;
    }

    public sealed class ReplayToolRecoveryRecordV4
    {
        public ReplayToolRecoveryRecordV4(string candidateIdentity,
            string blockerPlayerId, string reboundSide, string recoveryPlayerId,
            string reorganizationExitIdentity,
            string reboundTrajectoryArtifactIdentity, string reboundSampleIdentity,
            string blockContactIdentity, int remainingTouches)
        {
            CandidateIdentity = ReplayContractGuardV4.Required(candidateIdentity, nameof(candidateIdentity));
            BlockerPlayerId = ReplayContractGuardV4.Required(blockerPlayerId, nameof(blockerPlayerId));
            ReboundSide = ReplayContractGuardV4.OneOf(reboundSide, nameof(reboundSide), "Home", "Away");
            RecoveryPlayerId = ReplayContractGuardV4.Required(recoveryPlayerId, nameof(recoveryPlayerId));
            ReorganizationExitIdentity = ReplayContractGuardV4.Required(reorganizationExitIdentity, nameof(reorganizationExitIdentity));
            ReboundTrajectoryArtifactIdentity = ReplayContractGuardV4.Hash(reboundTrajectoryArtifactIdentity, nameof(reboundTrajectoryArtifactIdentity));
            ReboundSampleIdentity = ReplayContractGuardV4.Required(reboundSampleIdentity, nameof(reboundSampleIdentity));
            BlockContactIdentity = ReplayContractGuardV4.Required(blockContactIdentity, nameof(blockContactIdentity));
            RemainingTouches = ReplayContractGuardV4.Positive(remainingTouches, nameof(remainingTouches));
        }

        public string CandidateIdentity { get; }
        public string BlockerPlayerId { get; }
        public string ReboundSide { get; }
        public string RecoveryPlayerId { get; }
        public string ReorganizationExitIdentity { get; }
        public string ReboundTrajectoryArtifactIdentity { get; }
        public string ReboundSampleIdentity { get; }
        public string BlockContactIdentity { get; }
        public int RemainingTouches { get; }
    }

    public sealed class ReplayAttackDefenseAuthorityRecordV4
    {
        private readonly ReplayAttackDefenseCandidateRecordV4[] _candidates;
        private readonly ReplayPublicAttackThreatRecordV4[] _publicThreat;
        private readonly ReplayDefenseResponsibilityRecordV4[] _defenseResponsibilities;
        private readonly ReplayAttackCoverageResponsibilityRecordV4[]
            _attackCoverageResponsibilities;

        public ReplayAttackDefenseAuthorityRecordV4(int planRevision,
            int sourceSequenceNumber, string phase, string branch,
            ReplayVector3RecordV4 setTarget,
            IReadOnlyList<ReplayAttackDefenseCandidateRecordV4> candidates,
            IReadOnlyList<ReplayPublicAttackThreatRecordV4> publicThreat,
            IReadOnlyList<ReplayDefenseResponsibilityRecordV4> defenseResponsibilities,
            string selectedCandidateIdentity, string testedEnvelopeIdentity,
            string executableEnvelopeIdentity, string sampleEnvelopeIdentity,
            string trajectoryArtifactIdentity, ReplayToolRecoveryRecordV4 recovery,
            ReplayCoverageDecisionRecordV4 coverage,
            IReadOnlyList<ReplayAttackCoverageResponsibilityRecordV4>
                attackCoverageResponsibilities = null)
        {
            PlanRevision = ReplayContractGuardV4.NonNegative(planRevision, nameof(planRevision));
            SourceSequenceNumber = ReplayContractGuardV4.Positive(sourceSequenceNumber, nameof(sourceSequenceNumber));
            Phase = ReplayContractGuardV4.OneOf(phase, nameof(phase), "SetIntentPlanned",
                "AttackPlanned", "ThreatPublished", "DefenseCommitted", "AttackCommitted",
                "AwaitingActualContact", "ToolRecoveryAwaitingBlock", "ToolRecoveryAwaitingReceive",
                "ReorganizationPlanned", "HandedOff", "Terminal");
            Branch = ReplayContractGuardV4.OneOf(branch, nameof(branch), "Primary", "Contingency");
            SetTarget = setTarget ?? throw new ContractValidationException("setTarget is required.");
            _candidates = CopyCandidates(candidates);
            _publicThreat = CopyThreat(publicThreat);
            _defenseResponsibilities = CopyResponsibilities(defenseResponsibilities);
            _attackCoverageResponsibilities = CopyAttackCoverage(
                attackCoverageResponsibilities ??
                Array.Empty<ReplayAttackCoverageResponsibilityRecordV4>());
            SelectedCandidateIdentity = selectedCandidateIdentity == null ? string.Empty : selectedCandidateIdentity.Trim();
            TestedEnvelopeIdentity = ReplayContractGuardV4.Hash(testedEnvelopeIdentity, nameof(testedEnvelopeIdentity));
            ExecutableEnvelopeIdentity = ReplayContractGuardV4.Hash(executableEnvelopeIdentity, nameof(executableEnvelopeIdentity));
            SampleEnvelopeIdentity = ReplayContractGuardV4.Hash(sampleEnvelopeIdentity, nameof(sampleEnvelopeIdentity));
            TrajectoryArtifactIdentity = ReplayContractGuardV4.Hash(trajectoryArtifactIdentity, nameof(trajectoryArtifactIdentity));
            Recovery = recovery;
            Coverage = coverage ?? throw new ContractValidationException("coverage is required.");

            var selected = string.IsNullOrEmpty(SelectedCandidateIdentity) ? null :
                _candidates.SingleOrDefault(value => value.CandidateIdentity == SelectedCandidateIdentity);
            if (!string.IsNullOrEmpty(SelectedCandidateIdentity) && selected == null)
                throw new ContractValidationException("selectedCandidateIdentity must identify a candidate.");
            if (Phase == "SetIntentPlanned")
            {
                if (_candidates.Length != 0 || _publicThreat.Length != 0 ||
                    _defenseResponsibilities.Length != 0 ||
                    _attackCoverageResponsibilities.Length != 0 ||
                    selected != null || Recovery != null)
                    throw new ContractValidationException("SetIntentPlanned contains only the immutable SetIntent evidence.");
            }
            else if (_candidates.Length == 0)
                throw new ContractValidationException("Post-Set authority requires candidates.");

            if ((Phase == "AttackCommitted" || Phase == "AwaitingActualContact" ||
                 Phase == "ToolRecoveryAwaitingBlock" || Phase == "ToolRecoveryAwaitingReceive") && selected == null)
                throw new ContractValidationException("Committed attack authority requires a selected candidate.");
            if (Recovery != null)
            {
                var recoveryCandidate = _candidates.SingleOrDefault(value =>
                    value.CandidateIdentity == Recovery.CandidateIdentity);
                if (recoveryCandidate == null || recoveryCandidate.ActionClass != "BlockToolRecovery" ||
                    recoveryCandidate.ReorganizationExitIdentity != Recovery.ReorganizationExitIdentity ||
                    recoveryCandidate.TrajectoryArtifactIdentity == Recovery.ReboundTrajectoryArtifactIdentity)
                    throw new ContractValidationException("Tool recovery must link its declared tool candidate and exit.");
            }
        }

        public int PlanRevision { get; }
        public int SourceSequenceNumber { get; }
        public string Phase { get; }
        public string Branch { get; }
        public ReplayVector3RecordV4 SetTarget { get; }
        public IReadOnlyList<ReplayAttackDefenseCandidateRecordV4> Candidates => new ReadOnlyCollection<ReplayAttackDefenseCandidateRecordV4>(_candidates);
        public IReadOnlyList<ReplayPublicAttackThreatRecordV4> PublicThreat => new ReadOnlyCollection<ReplayPublicAttackThreatRecordV4>(_publicThreat);
        public IReadOnlyList<ReplayDefenseResponsibilityRecordV4> DefenseResponsibilities => new ReadOnlyCollection<ReplayDefenseResponsibilityRecordV4>(_defenseResponsibilities);
        public IReadOnlyList<ReplayAttackCoverageResponsibilityRecordV4>
            AttackCoverageResponsibilities =>
                new ReadOnlyCollection<
                    ReplayAttackCoverageResponsibilityRecordV4>(
                    _attackCoverageResponsibilities);
        public string SelectedCandidateIdentity { get; }
        public string TestedEnvelopeIdentity { get; }
        public string ExecutableEnvelopeIdentity { get; }
        public string SampleEnvelopeIdentity { get; }
        public string TrajectoryArtifactIdentity { get; }
        public ReplayToolRecoveryRecordV4 Recovery { get; }
        public ReplayCoverageDecisionRecordV4 Coverage { get; }

        private static ReplayAttackDefenseCandidateRecordV4[] CopyCandidates(IReadOnlyList<ReplayAttackDefenseCandidateRecordV4> source)
        {
            if (source == null) throw new ContractValidationException("candidates are required.");
            var copy = source.ToArray();
            if (copy.Any(value => value == null) || copy.Select(value => value.CandidateIdentity).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ContractValidationException("Candidates must be non-null with distinct identities.");
            Array.Sort(copy, (left, right) => string.CompareOrdinal(left.CandidateIdentity, right.CandidateIdentity));
            return copy;
        }

        private static ReplayPublicAttackThreatRecordV4[] CopyThreat(IReadOnlyList<ReplayPublicAttackThreatRecordV4> source)
        {
            if (source == null) throw new ContractValidationException("publicThreat is required.");
            var copy = source.ToArray();
            if (copy.Any(value => value == null)) throw new ContractValidationException("publicThreat cannot contain null records.");
            Array.Sort(copy, (left, right) => string.CompareOrdinal(left.ActionClass + "\n" + left.Zone, right.ActionClass + "\n" + right.Zone));
            return copy;
        }

        private static ReplayDefenseResponsibilityRecordV4[] CopyResponsibilities(IReadOnlyList<ReplayDefenseResponsibilityRecordV4> source)
        {
            if (source == null) throw new ContractValidationException("defenseResponsibilities are required.");
            var copy = source.ToArray();
            if (copy.Any(value => value == null) || copy.Select(value => value.Identity).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ContractValidationException("Defense responsibilities must be non-null with distinct identities.");
            Array.Sort(copy, (left, right) => string.CompareOrdinal(left.Identity, right.Identity));
            return copy;
        }

        private static ReplayAttackCoverageResponsibilityRecordV4[]
            CopyAttackCoverage(
                IReadOnlyList<ReplayAttackCoverageResponsibilityRecordV4>
                    source)
        {
            if (source == null)
                throw new ContractValidationException(
                    "attackCoverageResponsibilities are required.");
            var copy = source.ToArray();
            if (copy.Any(value => value == null) ||
                copy.Select(value => value.Identity)
                    .Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ContractValidationException(
                    "Attack coverage responsibilities must be non-null with distinct identities.");
            Array.Sort(copy, (left, right) =>
                string.CompareOrdinal(left.Identity, right.Identity));
            return copy;
        }
    }

    public sealed class ReplayPerceivedThreatRecordV4
    {
        public ReplayPerceivedThreatRecordV4(string identity, string zone,
            float confidence, float arrivalTime)
        {
            Identity = ReplayContractGuardV4.Required(identity, nameof(identity));
            Zone = ReplayContractGuardV4.Required(zone, nameof(zone));
            Confidence = ReplayContractGuardV4.Finite(confidence,
                nameof(confidence));
            ArrivalTime = ReplayContractGuardV4.Finite(arrivalTime,
                nameof(arrivalTime));
            if (Confidence < 0f || Confidence > 1f || ArrivalTime < 0f)
                throw new ContractValidationException(
                    "Perceived threat confidence and arrival time are out of range.");
        }

        public string Identity { get; }
        public string Zone { get; }
        public float Confidence { get; }
        public float ArrivalTime { get; }
    }

    public sealed class ReplayPerceptionAuthorityRecordV4
    {
        private readonly ReplayPerceivedThreatRecordV4[] _visibleThreats;

        public ReplayPerceptionAuthorityRecordV4(
            string configurationIdentity, string viewIdentity,
            string observingSide, string authoritativeArtifactIdentity,
            float observedAtSimulationTime, float recognitionDelaySeconds,
            string uncertaintyKey, float positionUncertaintyMeters,
            float confidence,
            IReadOnlyList<ReplayPerceivedThreatRecordV4> visibleThreats,
            string selectedSupportPlayerId, string selectedSupportZone,
            bool conservativeFallback, int affectedRevision,
            int sourceSequenceNumber)
        {
            ConfigurationIdentity = ReplayContractGuardV4.Required(
                configurationIdentity, nameof(configurationIdentity));
            ViewIdentity = ReplayContractGuardV4.Required(
                viewIdentity, nameof(viewIdentity));
            ObservingSide = ReplayContractGuardV4.Required(
                observingSide, nameof(observingSide));
            if (ObservingSide != "Home" && ObservingSide != "Away")
                throw new ContractValidationException(
                    "observingSide must be Home or Away.");
            AuthoritativeArtifactIdentity = ReplayContractGuardV4.Required(
                authoritativeArtifactIdentity,
                nameof(authoritativeArtifactIdentity));
            ObservedAtSimulationTime = ReplayContractGuardV4.Finite(
                observedAtSimulationTime, nameof(observedAtSimulationTime));
            RecognitionDelaySeconds = ReplayContractGuardV4.Finite(
                recognitionDelaySeconds, nameof(recognitionDelaySeconds));
            UncertaintyKey = ReplayContractGuardV4.Required(
                uncertaintyKey, nameof(uncertaintyKey));
            PositionUncertaintyMeters = ReplayContractGuardV4.Finite(
                positionUncertaintyMeters,
                nameof(positionUncertaintyMeters));
            Confidence = ReplayContractGuardV4.Finite(
                confidence, nameof(confidence));
            if (ObservedAtSimulationTime < 0f ||
                RecognitionDelaySeconds < 0f ||
                PositionUncertaintyMeters < 0f ||
                Confidence < 0f || Confidence > 1f)
                throw new ContractValidationException(
                    "Perception timing, uncertainty, or confidence is out of range.");
            if (visibleThreats == null)
                throw new ContractValidationException(
                    "visibleThreats are required.");
            _visibleThreats = visibleThreats.ToArray();
            if (_visibleThreats.Any(value => value == null) ||
                _visibleThreats.Select(value => value.Identity)
                    .Distinct(StringComparer.Ordinal).Count() !=
                _visibleThreats.Length)
                throw new ContractValidationException(
                    "Visible threats must be non-null with distinct identities.");
            Array.Sort(_visibleThreats, (left, right) =>
                string.CompareOrdinal(left.Identity, right.Identity));
            SelectedSupportPlayerId = ReplayContractGuardV4.Required(
                selectedSupportPlayerId, nameof(selectedSupportPlayerId));
            SelectedSupportZone = ReplayContractGuardV4.Required(
                selectedSupportZone, nameof(selectedSupportZone));
            ConservativeFallback = conservativeFallback;
            AffectedRevision = ReplayContractGuardV4.NonNegative(
                affectedRevision, nameof(affectedRevision));
            SourceSequenceNumber = ReplayContractGuardV4.NonNegative(
                sourceSequenceNumber, nameof(sourceSequenceNumber));
        }

        public string ConfigurationIdentity { get; }
        public string ViewIdentity { get; }
        public string ObservingSide { get; }
        public string AuthoritativeArtifactIdentity { get; }
        public float ObservedAtSimulationTime { get; }
        public float RecognitionDelaySeconds { get; }
        public string UncertaintyKey { get; }
        public float PositionUncertaintyMeters { get; }
        public float Confidence { get; }
        public IReadOnlyList<ReplayPerceivedThreatRecordV4> VisibleThreats =>
            new ReadOnlyCollection<ReplayPerceivedThreatRecordV4>(
                _visibleThreats);
        public string SelectedSupportPlayerId { get; }
        public string SelectedSupportZone { get; }
        public bool ConservativeFallback { get; }
        public int AffectedRevision { get; }
        public int SourceSequenceNumber { get; }
    }

    public sealed class ReplayWorkBudgetRecordV4
    {
        public ReplayWorkBudgetRecordV4(
            string configurationIdentity,
            int candidateCount,
            int sampleCount,
            int expansionCount,
            int deterministicWorkUnits,
            string degradationStep,
            string budgetOutcome)
        {
            ConfigurationIdentity = ReplayContractGuardV4.Hash(
                configurationIdentity, nameof(configurationIdentity));
            CandidateCount = ReplayContractGuardV4.Positive(
                candidateCount, nameof(candidateCount));
            SampleCount = ReplayContractGuardV4.Positive(
                sampleCount, nameof(sampleCount));
            ExpansionCount = ReplayContractGuardV4.NonNegative(
                expansionCount, nameof(expansionCount));
            DeterministicWorkUnits = ReplayContractGuardV4.Positive(
                deterministicWorkUnits, nameof(deterministicWorkUnits));
            var expected = (long)CandidateCount * SampleCount *
                           (ExpansionCount + 1L);
            if (expected > int.MaxValue ||
                DeterministicWorkUnits != (int)expected)
                throw new ContractValidationException(
                    "deterministicWorkUnits must match candidates * samples * expansion passes.");
            DegradationStep = ReplayContractGuardV4.DegradationStep(
                degradationStep, nameof(degradationStep));
            BudgetOutcome = ReplayContractGuardV4.OneOf(
                budgetOutcome, nameof(budgetOutcome),
                "WithinBudget", "Degraded", "SafeFallback");
            var expectedOutcome = DegradationStep == "FullSampling"
                ? "WithinBudget"
                : DegradationStep == "DeterministicSafeFallback"
                    ? "SafeFallback"
                    : "Degraded";
            if (BudgetOutcome != expectedOutcome)
                throw new ContractValidationException(
                    "budgetOutcome must match the selected degradation step.");
        }

        public string ConfigurationIdentity { get; }
        public int CandidateCount { get; }
        public int SampleCount { get; }
        public int ExpansionCount { get; }
        public int DeterministicWorkUnits { get; }
        public string DegradationStep { get; }
        public string BudgetOutcome { get; }
    }

    public sealed class MatchReplayEventV4
    {
        private readonly ReplayAbilityConsumptionRecordV4[] _abilityConsumptions;

        public MatchReplayEventV4(
            int sequenceNumber,
            string eventKind,
            string actorPlayerId,
            float simulationTimeSeconds,
            int homeScore,
            int awayScore,
            ReplayExecutionEnvelopeRecordV4 testedEnvelope,
            ReplayExecutionEnvelopeRecordV4 executableEnvelope,
            ReplayTrajectoryArtifactRecordV4 trajectory,
            IReadOnlyList<ReplayAbilityConsumptionRecordV4> abilityConsumptions,
            ReplaySampleClassificationRecordV4 classification,
            ReplayObservedP6GeometryRecordV4 observedP6Geometry,
            ReplayRuleDecisionRecordV4 ruleDecision)
            : this(
                sequenceNumber,
                eventKind,
                actorPlayerId,
                simulationTimeSeconds,
                homeScore,
                awayScore,
                testedEnvelope,
                executableEnvelope,
                trajectory,
                abilityConsumptions,
                classification,
                observedP6Geometry,
                ruleDecision,
                null,
                null,
                null)
        {
        }

        public MatchReplayEventV4(
            int sequenceNumber,
            string eventKind,
            string actorPlayerId,
            float simulationTimeSeconds,
            int homeScore,
            int awayScore,
            ReplayExecutionEnvelopeRecordV4 testedEnvelope,
            ReplayExecutionEnvelopeRecordV4 executableEnvelope,
            ReplayTrajectoryArtifactRecordV4 trajectory,
            IReadOnlyList<ReplayAbilityConsumptionRecordV4> abilityConsumptions,
            ReplaySampleClassificationRecordV4 classification,
            ReplayObservedP6GeometryRecordV4 observedP6Geometry,
            ReplayRuleDecisionRecordV4 ruleDecision,
            ReplayShadowRecordV4 shadow)
            : this(
                sequenceNumber,
                eventKind,
                actorPlayerId,
                simulationTimeSeconds,
                homeScore,
                awayScore,
                testedEnvelope,
                executableEnvelope,
                trajectory,
                abilityConsumptions,
                classification,
                observedP6Geometry,
                ruleDecision,
                shadow,
                null,
                null)
        {
        }

        public MatchReplayEventV4(
            int sequenceNumber,
            string eventKind,
            string actorPlayerId,
            float simulationTimeSeconds,
            int homeScore,
            int awayScore,
            ReplayExecutionEnvelopeRecordV4 testedEnvelope,
            ReplayExecutionEnvelopeRecordV4 executableEnvelope,
            ReplayTrajectoryArtifactRecordV4 trajectory,
            IReadOnlyList<ReplayAbilityConsumptionRecordV4> abilityConsumptions,
            ReplaySampleClassificationRecordV4 classification,
            ReplayObservedP6GeometryRecordV4 observedP6Geometry,
            ReplayRuleDecisionRecordV4 ruleDecision,
            ReplayShadowRecordV4 shadow,
            ReplayOrganizationAuthorityRecordV4 organizationAuthority)
            : this(sequenceNumber, eventKind, actorPlayerId, simulationTimeSeconds,
                homeScore, awayScore, testedEnvelope, executableEnvelope, trajectory,
                abilityConsumptions, classification, observedP6Geometry, ruleDecision,
                shadow, organizationAuthority, null)
        {
        }

        public MatchReplayEventV4(
            int sequenceNumber,
            string eventKind,
            string actorPlayerId,
            float simulationTimeSeconds,
            int homeScore,
            int awayScore,
            ReplayExecutionEnvelopeRecordV4 testedEnvelope,
            ReplayExecutionEnvelopeRecordV4 executableEnvelope,
            ReplayTrajectoryArtifactRecordV4 trajectory,
            IReadOnlyList<ReplayAbilityConsumptionRecordV4> abilityConsumptions,
            ReplaySampleClassificationRecordV4 classification,
            ReplayObservedP6GeometryRecordV4 observedP6Geometry,
            ReplayRuleDecisionRecordV4 ruleDecision,
            ReplayShadowRecordV4 shadow,
            ReplayOrganizationAuthorityRecordV4 organizationAuthority,
            ReplayAttackDefenseAuthorityRecordV4 attackDefenseAuthority,
            ReplayPerceptionAuthorityRecordV4 perceptionAuthority = null,
            ReplayWorkBudgetRecordV4 workBudget = null)
        {
            SequenceNumber = ReplayContractGuardV4.NonNegative(
                sequenceNumber,
                nameof(sequenceNumber));
            EventKind = ReplayContractGuardV4.EventKind(
                eventKind,
                nameof(eventKind));
            ActorPlayerId = ReplayContractGuardV4.Required(
                actorPlayerId,
                nameof(actorPlayerId));
            SimulationTimeSeconds = ReplayContractGuardV4.Finite(
                simulationTimeSeconds,
                nameof(simulationTimeSeconds));
            if (SimulationTimeSeconds < 0f)
            {
                throw new ContractValidationException(
                    "simulationTimeSeconds must be non-negative.");
            }

            HomeScore = ReplayContractGuardV4.NonNegative(
                homeScore,
                nameof(homeScore));
            AwayScore = ReplayContractGuardV4.NonNegative(
                awayScore,
                nameof(awayScore));
            TestedEnvelope = testedEnvelope ??
                throw new ContractValidationException(
                    "testedEnvelope is required.");
            ExecutableEnvelope = executableEnvelope ??
                throw new ContractValidationException(
                    "executableEnvelope is required.");
            Trajectory = trajectory ??
                throw new ContractValidationException("trajectory is required.");
            Classification = classification ??
                throw new ContractValidationException("classification is required.");
            RuleDecision = ruleDecision ??
                throw new ContractValidationException("ruleDecision is required.");
            if (!IsCandidateCategoryCompatibleWithEventKind(
                    EventKind,
                    TestedEnvelope.CandidateCategory) ||
                !IsCandidateCategoryCompatibleWithEventKind(
                    EventKind,
                    ExecutableEnvelope.CandidateCategory) ||
                !IsCandidateCategoryCompatibleWithEventKind(
                    EventKind,
                    Classification.ActualSample.CandidateCategory))
            {
                throw new ContractValidationException(
                    "Event kind is incompatible with an envelope or actual-sample category.");
            }

            if (TestedEnvelope.Identity !=
                    Classification.TestedEnvelopeIdentity)
            {
                throw new ContractValidationException(
                    "Tested envelope and classification must identify the same execution envelope.");
            }

            if (Classification.Kind == "EnvelopeExpanded")
            {
                if (ExecutableEnvelope.Identity !=
                        Classification.ExpandedEnvelopeIdentity ||
                    ExecutableEnvelope.Identity == TestedEnvelope.Identity ||
                    ExecutableEnvelope.CurrentExpansionCount !=
                        TestedEnvelope.CurrentExpansionCount + 1)
                {
                    throw new ContractValidationException(
                        "Expanded classification must identify its next distinct executable envelope.");
                }
            }
            else if (ExecutableEnvelope.Identity != TestedEnvelope.Identity ||
                     ExecutableEnvelope.CurrentExpansionCount !=
                        TestedEnvelope.CurrentExpansionCount)
            {
                throw new ContractValidationException(
                    "Non-expanded classification must execute the tested envelope.");
            }

            if (TestedEnvelope.SamplingKey !=
                    Classification.ActualSample.SamplingKey ||
                ExecutableEnvelope.SamplingKey !=
                    Classification.ActualSample.SamplingKey)
            {
                throw new ContractValidationException(
                    "Both envelopes and classification must identify the same sampling contract.");
            }

            if (TestedEnvelope.DerivedAttributesFingerprint !=
                    ExecutableEnvelope.DerivedAttributesFingerprint ||
                TestedEnvelope.PolicyIdentity !=
                    ExecutableEnvelope.PolicyIdentity ||
                TestedEnvelope.SourceIntentIdentity !=
                    ExecutableEnvelope.SourceIntentIdentity ||
                TestedEnvelope.MaximumExpansionCount !=
                    ExecutableEnvelope.MaximumExpansionCount ||
                TestedEnvelope.AllowedExpansionCount !=
                    ExecutableEnvelope.AllowedExpansionCount ||
                TestedEnvelope.PerStepExpansionFactor !=
                    ExecutableEnvelope.PerStepExpansionFactor)
            {
                throw new ContractValidationException(
                    "Tested and executable envelopes must share one derivation, intent, and expansion policy.");
            }

            _abilityConsumptions = CopyAndSortConsumptions(abilityConsumptions);
            for (var index = 0;
                 index < _abilityConsumptions.Length;
                 index++)
            {
                if (_abilityConsumptions[index].PlayerId != ActorPlayerId ||
                    _abilityConsumptions[index]
                        .DerivedAttributesFingerprint !=
                    TestedEnvelope.DerivedAttributesFingerprint)
                {
                    throw new ContractValidationException(
                        "Runtime-consumption evidence must identify the event actor and envelope derivation.");
                }
            }

            ObservedP6Geometry = observedP6Geometry;
            if (EventKind == "Attack")
            {
                if (ObservedP6Geometry == null)
                {
                    throw new ContractValidationException(
                        "Attack replay events require observed P6 geometry.");
                }

                if (ObservedP6Geometry.ActorPlayerId != ActorPlayerId)
                {
                    throw new ContractValidationException(
                        "Observed P6 geometry actor must match the event actor.");
                }
            }
            else if (ObservedP6Geometry != null)
            {
                throw new ContractValidationException(
                    "Observed P6 geometry must be null for a non-attack replay event.");
            }

            Shadow = shadow;
            if (Shadow != null &&
                Shadow.ArtifactIdentity != Trajectory.ArtifactIdentity)
            {
                throw new ContractValidationException(
                    "Shadow artifact identity must match the event trajectory.");
            }

            OrganizationAuthority = organizationAuthority;
            ValidateOrganizationAuthority();
            AttackDefenseAuthority = attackDefenseAuthority;
            ValidateAttackDefenseAuthority();
            PerceptionAuthority = perceptionAuthority;
            WorkBudget = workBudget;
            if (WorkBudget != null &&
                (WorkBudget.ConfigurationIdentity !=
                     TestedEnvelope.PolicyIdentity ||
                 WorkBudget.SampleCount != TestedEnvelope.SampleCount ||
                 WorkBudget.ExpansionCount !=
                     ExecutableEnvelope.CurrentExpansionCount ||
                 WorkBudget.DegradationStep !=
                     Trajectory.CacheKey.DegradationStep))
                throw new ContractValidationException(
                    "Work-budget evidence must match event-owned envelope and trajectory evidence.");
        }

        public int SequenceNumber { get; }
        public string EventKind { get; }
        public string ActorPlayerId { get; }
        public float SimulationTimeSeconds { get; }
        public int HomeScore { get; }
        public int AwayScore { get; }
        public ReplayExecutionEnvelopeRecordV4 TestedEnvelope { get; }
        public ReplayExecutionEnvelopeRecordV4 ExecutableEnvelope { get; }
        public ReplayTrajectoryArtifactRecordV4 Trajectory { get; }
        public IReadOnlyList<ReplayAbilityConsumptionRecordV4> AbilityConsumptions =>
            new ReadOnlyCollection<ReplayAbilityConsumptionRecordV4>(
                _abilityConsumptions);
        public ReplaySampleClassificationRecordV4 Classification { get; }
        public ReplayObservedP6GeometryRecordV4 ObservedP6Geometry { get; }
        public ReplayRuleDecisionRecordV4 RuleDecision { get; }
        public ReplayShadowRecordV4 Shadow { get; }
        public ReplayOrganizationAuthorityRecordV4 OrganizationAuthority { get; }
        public ReplayAttackDefenseAuthorityRecordV4 AttackDefenseAuthority { get; }
        public ReplayPerceptionAuthorityRecordV4 PerceptionAuthority { get; }
        public ReplayWorkBudgetRecordV4 WorkBudget { get; }

        private void ValidateOrganizationAuthority()
        {
            if (OrganizationAuthority == null)
            {
                return;
            }

            if (EventKind != "Receive" && EventKind != "Set")
            {
                throw new ContractValidationException(
                    "Organization authority is valid only for Receive or Set events.");
            }

            var expectedPhase = EventKind == "Receive"
                ? "Receive"
                : "Organize";
            if (OrganizationAuthority.AuthorityPhase != expectedPhase)
            {
                throw new ContractValidationException(
                    "Organization authority phase must match the replay event kind.");
            }

            if (OrganizationAuthority.TestedEnvelopeIdentity !=
                    TestedEnvelope.Identity ||
                OrganizationAuthority.ExecutableEnvelopeIdentity !=
                    ExecutableEnvelope.Identity ||
                OrganizationAuthority.SampleEnvelopeIdentity !=
                    Classification.ActualSample.EnvelopeIdentity ||
                OrganizationAuthority.TrajectoryArtifactIdentity !=
                    Trajectory.ArtifactIdentity)
            {
                throw new ContractValidationException(
                    "Organization authority identities must match event-owned evidence.");
            }

            if (EventKind == "Receive" &&
                OrganizationAuthority.ActualFirstPassLanding != null)
            {
                throw new ContractValidationException(
                    "Receive authority cannot contain an actual first-pass landing.");
            }

            if (EventKind == "Set")
            {
                if (OrganizationAuthority.ActualFirstPassLanding == null)
                {
                    throw new ContractValidationException(
                        "Set authority requires the actual first-pass landing.");
                }

                if (!string.Equals(
                        ActorPlayerId,
                        OrganizationAuthority.OrganizerPlayerId,
                        StringComparison.Ordinal))
                {
                    throw new ContractValidationException(
                        "Set event actor must match the authority organizer.");
                }

                if (OrganizationAuthority.FallbackReason ==
                    "NoLegalOrganizer")
                {
                    throw new ContractValidationException(
                        "An accepted Set cannot use NoLegalOrganizer.");
                }
            }
        }

        private void ValidateAttackDefenseAuthority()
        {
            if (AttackDefenseAuthority == null) return;
            if (AttackDefenseAuthority.TestedEnvelopeIdentity != TestedEnvelope.Identity ||
                AttackDefenseAuthority.ExecutableEnvelopeIdentity != ExecutableEnvelope.Identity ||
                AttackDefenseAuthority.SampleEnvelopeIdentity != Classification.ActualSample.EnvelopeIdentity ||
                AttackDefenseAuthority.TrajectoryArtifactIdentity != Trajectory.ArtifactIdentity)
                throw new ContractValidationException("Attack-defense authority identities must match event-owned evidence.");

            if (AttackDefenseAuthority.Phase == "SetIntentPlanned")
            {
                if (EventKind != "Set" || OrganizationAuthority == null)
                    throw new ContractValidationException("A Gate I SetIntent record requires the Gate H Set authority record.");
                return;
            }

            if (EventKind == "Set")
                throw new ContractValidationException("Only SetIntentPlanned may be attached to a Set event.");
            var selected = string.IsNullOrEmpty(
                AttackDefenseAuthority.SelectedCandidateIdentity)
                ? null
                : AttackDefenseAuthority.Candidates.Single(value =>
                    value.CandidateIdentity ==
                    AttackDefenseAuthority.SelectedCandidateIdentity);
            if (EventKind == "Attack")
            {
                if (selected == null || selected.ActorPlayerId != ActorPlayerId)
                    throw new ContractValidationException("Selected Gate I candidate must match the Attack event actor.");
                return;
            }

            if (EventKind != "Block" && EventKind != "Receive")
                throw new ContractValidationException("Gate I authority is valid only for contact events.");
            var plannedDefense = AttackDefenseAuthority.Phase ==
                "DefenseCommitted" && selected == null;
            var observedDefense = (AttackDefenseAuthority.Phase ==
                "AwaitingActualContact" || AttackDefenseAuthority.Phase ==
                "ReorganizationPlanned") && selected != null;
            var observedToolBlock = EventKind == "Block" &&
                AttackDefenseAuthority.Phase == "ToolRecoveryAwaitingReceive" &&
                selected?.ActionClass == "BlockToolRecovery" &&
                AttackDefenseAuthority.Recovery != null &&
                AttackDefenseAuthority.Recovery.BlockerPlayerId == ActorPlayerId;
            var observedToolReceive = EventKind == "Receive" &&
                (AttackDefenseAuthority.Phase ==
                    "ToolRecoveryAwaitingReceive" ||
                 AttackDefenseAuthority.Phase == "ReorganizationPlanned") &&
                selected?.ActionClass == "BlockToolRecovery" &&
                AttackDefenseAuthority.Recovery != null &&
                AttackDefenseAuthority.Recovery.RecoveryPlayerId == ActorPlayerId;
            var observedAttackCover = EventKind == "Receive" &&
                (AttackDefenseAuthority.Phase == "AwaitingActualContact" ||
                 AttackDefenseAuthority.Phase == "ReorganizationPlanned") &&
                AttackDefenseAuthority.AttackCoverageResponsibilities.Any(
                    value => value.ActorPlayerId == ActorPlayerId);
            if (!plannedDefense && !observedDefense && !observedToolBlock && !observedToolReceive)
                throw new ContractValidationException("Gate I defense evidence requires an awaiting or reorganization phase.");
            if (!observedToolReceive && !observedAttackCover &&
                !AttackDefenseAuthority.DefenseResponsibilities.Any(value =>
                value.ActorPlayerId == ActorPlayerId))
                throw new ContractValidationException("Gate I defense event actor must have a declared responsibility.");
        }

        public static bool IsCandidateCategoryCompatibleWithEventKind(
            string eventKind,
            string candidateCategory)
        {
            if (string.Equals(eventKind, candidateCategory, StringComparison.Ordinal))
            {
                return true;
            }

            return (string.Equals(eventKind, "Attack", StringComparison.Ordinal) &&
                    string.Equals(candidateCategory, "SoftAction", StringComparison.Ordinal)) ||
                (string.Equals(eventKind, "Receive", StringComparison.Ordinal) &&
                    string.Equals(candidateCategory, "Defense", StringComparison.Ordinal));
        }

        private static ReplayAbilityConsumptionRecordV4[] CopyAndSortConsumptions(
            IReadOnlyList<ReplayAbilityConsumptionRecordV4> source)
        {
            if (source == null || source.Count == 0)
            {
                throw new ContractValidationException(
                    "abilityConsumptions are required.");
            }

            var copy = new ReplayAbilityConsumptionRecordV4[source.Count];
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = source[index] ??
                    throw new ContractValidationException(
                        "abilityConsumptions cannot contain null records.");
                var key = copy[index].PlayerId + "\n" + copy[index].AttributeName;
                if (!keys.Add(key))
                {
                    throw new ContractValidationException(
                        "abilityConsumptions contain a duplicate player/field record.");
                }
            }

            Array.Sort(
                copy,
                (left, right) =>
                {
                    var field = ReplayAbilityConsumptionRecordV4
                        .FrozenIndex(left.AttributeName)
                        .CompareTo(
                            ReplayAbilityConsumptionRecordV4
                                .FrozenIndex(right.AttributeName));
                    return field != 0
                        ? field
                        : string.CompareOrdinal(left.PlayerId, right.PlayerId);
                });
            return copy;
        }
    }

    // Defense windows are not contact events: misses and deterministic losers
    // need canonical evidence without inventing a counted-touch replay event.
    public sealed class ReplayDefenseAttemptRecordV4
    {
        public ReplayDefenseAttemptRecordV4(
            string attemptIdentity, string kind, string commandKind,
            string actorPlayerId, string team, int planRevision,
            int sourceSequenceNumber, string envelopeIdentity,
            string trajectoryArtifactIdentity, float windowStartSimulationTime,
            float windowEndSimulationTime, float simulationTimeSeconds,
            ReplayVector3RecordV4 ballPosition,
            ReplayVector3RecordV4 ballVelocity, string continuationState,
            string reason, int? winningContactGroupId = null,
            string winningActorPlayerId = null)
        {
            AttemptIdentity = ReplayContractGuardV4.Required(attemptIdentity, nameof(attemptIdentity));
            Kind = ReplayContractGuardV4.OneOf(kind, nameof(kind),
                "DefenseAttemptOpened", "DefenseCandidateSampled",
                "DefenseContactAccepted", "DefenseContactRejected",
                "DefenseAttemptExpired", "PostBlockContinuationResolved");
            CommandKind = ReplayContractGuardV4.Required(commandKind, nameof(commandKind));
            ActorPlayerId = ReplayContractGuardV4.Required(actorPlayerId, nameof(actorPlayerId));
            Team = ReplayContractGuardV4.Required(team, nameof(team));
            PlanRevision = ReplayContractGuardV4.NonNegative(planRevision, nameof(planRevision));
            SourceSequenceNumber = ReplayContractGuardV4.NonNegative(sourceSequenceNumber, nameof(sourceSequenceNumber));
            EnvelopeIdentity = ReplayContractGuardV4.Hash(envelopeIdentity, nameof(envelopeIdentity));
            TrajectoryArtifactIdentity = ReplayContractGuardV4.Hash(trajectoryArtifactIdentity, nameof(trajectoryArtifactIdentity));
            WindowStartSimulationTime = ReplayContractGuardV4.Finite(windowStartSimulationTime, nameof(windowStartSimulationTime));
            WindowEndSimulationTime = ReplayContractGuardV4.Finite(windowEndSimulationTime, nameof(windowEndSimulationTime));
            SimulationTimeSeconds = ReplayContractGuardV4.Finite(simulationTimeSeconds, nameof(simulationTimeSeconds));
            if (WindowStartSimulationTime < 0f || WindowEndSimulationTime < WindowStartSimulationTime || SimulationTimeSeconds < 0f)
                throw new ContractValidationException("Defense-attempt times must be non-negative and ordered.");
            BallPosition = ballPosition ?? throw new ContractValidationException("ballPosition is required.");
            BallVelocity = ballVelocity ?? throw new ContractValidationException("ballVelocity is required.");
            ContinuationState = ReplayContractGuardV4.Required(continuationState, nameof(continuationState));
            Reason = reason ?? string.Empty;
            WinningContactGroupId = winningContactGroupId;
            WinningActorPlayerId = winningActorPlayerId;
        }

        public string AttemptIdentity { get; }
        public string Kind { get; }
        public string CommandKind { get; }
        public string ActorPlayerId { get; }
        public string Team { get; }
        public int PlanRevision { get; }
        public int SourceSequenceNumber { get; }
        public string EnvelopeIdentity { get; }
        public string TrajectoryArtifactIdentity { get; }
        public float WindowStartSimulationTime { get; }
        public float WindowEndSimulationTime { get; }
        public float SimulationTimeSeconds { get; }
        public ReplayVector3RecordV4 BallPosition { get; }
        public ReplayVector3RecordV4 BallVelocity { get; }
        public string ContinuationState { get; }
        public string Reason { get; }
        public int? WinningContactGroupId { get; }
        public string WinningActorPlayerId { get; }
    }

    public sealed class ReplayScenarioProvenanceV4
    {
        public const string DefaultScenarioId = "formal-indoor-6v6-default";
        public const int DefaultFormatVersion = 1;
        public const string DefaultContentHash =
            "0000000000000000000000000000000000000000000000000000000000000000";

        public ReplayScenarioProvenanceV4(
            string scenarioId,
            int formatVersion,
            string contentHash)
        {
            ScenarioId = ReplayContractGuardV4.Required(scenarioId,
                nameof(scenarioId));
            if (formatVersion < 1)
            {
                throw new ContractValidationException(
                    "Scenario format version must be positive.");
            }

            FormatVersion = formatVersion;
            ContentHash = ReplayContractGuardV4.Hash(contentHash,
                nameof(contentHash));
        }

        public string ScenarioId { get; }
        public int FormatVersion { get; }
        public string ContentHash { get; }

        public static ReplayScenarioProvenanceV4 Default { get; } =
            new ReplayScenarioProvenanceV4(
                DefaultScenarioId,
                DefaultFormatVersion,
                DefaultContentHash);
    }

    public sealed class MatchReplayV4
    {
        private readonly MatchReplayEventV4[] _events;
        private readonly ReplayDefenseAttemptRecordV4[] _defenseAttempts;

        private MatchReplayV4(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
            IReadOnlyList<ReplayDefenseAttemptRecordV4> defenseAttempts,
            ReplayScenarioProvenanceV4 scenario,
            int sourceSequenceAnchor,
            string suppliedReplayHash,
            bool allowLegacyShadowCoverageHash)
        {
            FormatVersion = ContractVersions.ReplayV4;
            ReplayId = ReplayContractGuardV4.Required(replayId, nameof(replayId));
            Context = context ??
                throw new ContractValidationException("context is required.");
            Context.Validate();
            if (Context.ContractVersion != ContractVersions.MatchV4 ||
                Context.RulesVersion != RulesVersions.FullRallyV3)
            {
                throw new ContractValidationException(
                    "Replay V4 requires native MatchContextV4 and V3 rules.");
            }

            _events = CopySortAndValidateEvents(events);
            _defenseAttempts = CopyDefenseAttempts(defenseAttempts);
            Scenario = scenario ?? ReplayScenarioProvenanceV4.Default;
            SourceSequenceAnchor = ReplayContractGuardV4.NonNegative(
                sourceSequenceAnchor,
                nameof(sourceSequenceAnchor));
            ValidateEventsAgainstContext(Context, _events, SourceSequenceAnchor);
            ReplayHash = CanonicalMatchReplayJsonV4.ComputeHash(this);
            if (suppliedReplayHash != null &&
                !string.Equals(
                    suppliedReplayHash,
                    ReplayHash,
                    StringComparison.Ordinal) &&
                (!allowLegacyShadowCoverageHash ||
                 !CanonicalMatchReplayJsonV4.MatchesSupportedLegacyHash(
                     this,
                     suppliedReplayHash)))
            {
                throw new ContractValidationException(
                    "replayHash does not match the canonical V4 replay segment.");
            }
        }

        public int FormatVersion { get; }
        public string ReplayId { get; }
        public MatchContextV4 Context { get; }
        public string ContextHash => Context.ContextHash;
        public int SourceSequenceAnchor { get; }
        public ReplayScenarioProvenanceV4 Scenario { get; }
        public IReadOnlyList<MatchReplayEventV4> Events =>
            new ReadOnlyCollection<MatchReplayEventV4>(_events);
        public IReadOnlyList<ReplayDefenseAttemptRecordV4> DefenseAttempts =>
            new ReadOnlyCollection<ReplayDefenseAttemptRecordV4>(
                _defenseAttempts);
        public string ReplayHash { get; }

        public static MatchReplayV4 Create(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events)
        {
            return new MatchReplayV4(replayId, context, events,
                Array.Empty<ReplayDefenseAttemptRecordV4>(),
                ReplayScenarioProvenanceV4.Default, 0, null, false);
        }

        public static MatchReplayV4 Create(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
            int sourceSequenceAnchor)
        {
            return new MatchReplayV4(
                replayId, context, events,
                Array.Empty<ReplayDefenseAttemptRecordV4>(),
                ReplayScenarioProvenanceV4.Default,
                sourceSequenceAnchor, null, false);
        }

        public static MatchReplayV4 Create(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
            IReadOnlyList<ReplayDefenseAttemptRecordV4> defenseAttempts,
            int sourceSequenceAnchor)
        {
            return new MatchReplayV4(replayId, context, events,
                defenseAttempts, ReplayScenarioProvenanceV4.Default,
                sourceSequenceAnchor, null, false);
        }

        public static MatchReplayV4 Create(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
            IReadOnlyList<ReplayDefenseAttemptRecordV4> defenseAttempts,
            ReplayScenarioProvenanceV4 scenario,
            int sourceSequenceAnchor)
        {
            return new MatchReplayV4(replayId, context, events,
                defenseAttempts, scenario, sourceSequenceAnchor, null, false);
        }

        internal static MatchReplayV4 Restore(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
            IReadOnlyList<ReplayDefenseAttemptRecordV4> defenseAttempts,
            ReplayScenarioProvenanceV4 scenario,
            int sourceSequenceAnchor,
            string replayHash,
            bool allowLegacyShadowCoverageHash)
        {
            if (replayHash != null)
            {
                ReplayContractGuardV4.Hash(replayHash, nameof(replayHash));
            }

            return new MatchReplayV4(
                replayId, context, events, defenseAttempts, scenario,
                sourceSequenceAnchor,
                replayHash,
                allowLegacyShadowCoverageHash);
        }

        internal void Validate()
        {
            Context.Validate();
            CopySortAndValidateEvents(_events);
            CopyDefenseAttempts(_defenseAttempts);
            _ = Scenario ?? throw new ContractValidationException(
                "scenario is required.");
            ValidateEventsAgainstContext(Context, _events, SourceSequenceAnchor);
            ReplayContractGuardV4.Hash(ReplayHash, nameof(ReplayHash));
            if (CanonicalMatchReplayJsonV4.ComputeHash(this) != ReplayHash)
            {
                throw new ContractValidationException(
                    "replayHash does not match the canonical V4 replay segment.");
            }
        }

        private static MatchReplayEventV4[] CopySortAndValidateEvents(
            IReadOnlyList<MatchReplayEventV4> source)
        {
            if (source == null || source.Count == 0)
            {
                throw new ContractValidationException("events are required.");
            }

            var copy = new MatchReplayEventV4[source.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = source[index] ??
                    throw new ContractValidationException(
                        "events cannot contain null records.");
            }

            Array.Sort(
                copy,
                (left, right) =>
                    left.SequenceNumber.CompareTo(right.SequenceNumber));
            for (var index = 0; index < copy.Length; index++)
            {
                if (copy[index].SequenceNumber != index)
                {
                    if (index > 0 &&
                        copy[index].SequenceNumber ==
                        copy[index - 1].SequenceNumber)
                    {
                        throw new ContractValidationException(
                            "Replay events contain a duplicate sequence number.");
                    }

                    throw new ContractValidationException(
                        "Replay event sequence contains a gap.");
                }
            }

            return copy;
        }

        private static ReplayDefenseAttemptRecordV4[] CopyDefenseAttempts(
            IReadOnlyList<ReplayDefenseAttemptRecordV4> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<ReplayDefenseAttemptRecordV4>();
            var copy = new ReplayDefenseAttemptRecordV4[source.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = source[index] ?? throw new ContractValidationException(
                    "defenseAttempts cannot contain null records.");
            return copy;
        }

        private static void ValidateEventsAgainstContext(
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
            int sourceSequenceAnchor)
        {
            var lastShadowSource = sourceSequenceAnchor;
            for (var eventIndex = 0;
                 eventIndex < events.Count;
                 eventIndex++)
            {
                var replayEvent = events[eventIndex];
                var player = FindPlayer(
                    context,
                    replayEvent.ActorPlayerId);
                if (player == null)
                {
                    throw new ContractValidationException(
                        "Replay event actor is absent from the V4 context.");
                }

                if (replayEvent.TestedEnvelope
                        .DerivedAttributesFingerprint !=
                    player.Derived.ResultFingerprint ||
                    replayEvent.ExecutableEnvelope
                        .DerivedAttributesFingerprint !=
                    player.Derived.ResultFingerprint)
                {
                    throw new ContractValidationException(
                        "Replay envelope derivation does not match its V4 context player.");
                }

                var key = replayEvent.Trajectory.CacheKey;
                var configuration =
                    context.TrajectoryPredictionProviderConfiguration;
                if (key.PhysicsConfigurationHash !=
                        context.PhysicsConfigurationHash ||
                    key.PredictorVersion !=
                        configuration.PredictorVersion ||
                    key.PredictorConfigurationHash !=
                        configuration.PredictorConfigurationHash)
                {
                    throw new ContractValidationException(
                        "Replay trajectory provenance does not match the V4 context configuration.");
                }

                ValidateShadowAssignments(context, replayEvent.Shadow);
                if (replayEvent.Shadow == null)
                {
                    continue;
                }

                var expectedSource = sourceSequenceAnchor + eventIndex + 1;
                if (replayEvent.Shadow.SourceSequenceNumber != expectedSource)
                {
                    throw new ContractValidationException(
                        "Shadow source sequence is inconsistent with the replay capture anchor.");
                }

                if (replayEvent.Shadow.SourceSequenceNumber <= lastShadowSource)
                {
                    throw new ContractValidationException(
                        "Shadow source sequences must be strictly increasing.");
                }

                lastShadowSource = replayEvent.Shadow.SourceSequenceNumber;
            }
        }

        private static void ValidateShadowAssignments(
            MatchContextV4 context,
            ReplayShadowRecordV4 shadow)
        {
            if (shadow == null)
            {
                return;
            }

            var assignedPlayerIds = new HashSet<string>(StringComparer.Ordinal);
            ValidateShadowPlanAssignments(
                shadow.Home,
                context.Home.RotationOrder,
                assignedPlayerIds);
            ValidateShadowPlanAssignments(
                shadow.Away,
                context.Away.RotationOrder,
                assignedPlayerIds);
            if (assignedPlayerIds.Count != 12)
            {
                throw new ContractValidationException(
                    "Shadow plans must assign twelve distinct context players.");
            }
        }

        private static void ValidateShadowPlanAssignments(
            ReplayTeamRallyPlanRecordV4 plan,
            IReadOnlyList<PlayerSnapshotV4> roster,
            ISet<string> assignedPlayerIds)
        {
            foreach (var assignment in plan.PrimaryAssignments)
            {
                if (!ContainsPlayer(roster, assignment.PlayerId))
                {
                    throw new ContractValidationException(
                        "Shadow assignment player is absent from its context team roster.");
                }

                if (!assignedPlayerIds.Add(assignment.PlayerId))
                {
                    throw new ContractValidationException(
                        "Shadow plans must assign twelve distinct context players.");
                }
            }
        }

        private static bool ContainsPlayer(
            IReadOnlyList<PlayerSnapshotV4> roster,
            string playerId)
        {
            foreach (var player in roster)
            {
                if (player.PlayerId.Value == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static PlayerSnapshotV4 FindPlayer(
            MatchContextV4 context,
            string playerId)
        {
            foreach (var player in context.Home.RotationOrder)
            {
                if (player.PlayerId.Value == playerId)
                {
                    return player;
                }
            }

            foreach (var player in context.Away.RotationOrder)
            {
                if (player.PlayerId.Value == playerId)
                {
                    return player;
                }
            }

            return null;
        }
    }

    internal static class ReplayContractGuardV4
    {
        public static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ContractValidationException(name + " is required.");
            }

            return value;
        }

        public static string Hash(string value, string name)
        {
            ContractGuard.Hash(value, name);
            return value;
        }

        public static float Finite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ContractValidationException(name + " must be finite.");
            }

            return value;
        }

        public static float PositiveFinite(float value, string name)
        {
            Finite(value, name);
            if (value <= 0f)
            {
                throw new ContractValidationException(name + " must be positive.");
            }

            return value;
        }

        public static float PositiveUnit(float value, string name)
        {
            PositiveFinite(value, name);
            if (value > 1f)
            {
                throw new ContractValidationException(
                    name + " must be in the range (0, 1].");
            }

            return value;
        }

        public static int Positive(int value, string name)
        {
            if (value <= 0)
            {
                throw new ContractValidationException(name + " must be positive.");
            }

            return value;
        }

        public static int NonNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ContractValidationException(
                    name + " must be non-negative.");
            }

            return value;
        }

        public static string EventKind(string value, string name)
        {
            return OneOf(value, name, "Receive", "Set", "Attack", "Block", "Serve");
        }

        public static string ExecutionCandidateCategory(string value, string name)
        {
            return OneOf(
                value,
                name,
                "Receive",
                "Set",
                "Attack",
                "Block",
                "Serve",
                "SoftAction",
                "Defense");
        }

        public static string DegradationStep(string value, string name)
        {
            return OneOf(
                value,
                name,
                "FullSampling",
                "ReducedSampleCount",
                "CachedCoarseDistribution",
                "DeterministicSafeFallback");
        }

        public static string OneOf(
            string value,
            string name,
            params string[] accepted)
        {
            Required(value, name);
            for (var index = 0; index < accepted.Length; index++)
            {
                if (string.Equals(value, accepted[index], StringComparison.Ordinal))
                {
                    return value;
                }
            }

            throw new ContractValidationException(name + " is not supported.");
        }

        public static string[] CopyRequiredStrings(
            IReadOnlyList<string> source,
            string name,
            bool allowEmpty)
        {
            if (source == null || (!allowEmpty && source.Count == 0))
            {
                throw new ContractValidationException(name + " is required.");
            }

            var copy = new string[source.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = Required(source[index], name + "[" + index + "]");
                if (!seen.Add(copy[index]))
                {
                    throw new ContractValidationException(
                        name + " must not contain duplicates.");
                }
            }

            return copy;
        }
    }

    internal static class CanonicalMatchReplayJsonV4
    {
        private const string HashFamily = "volleyball.match-replay.v4\n";

        public static string Serialize(MatchReplayV4 replay)
        {
            var payload = Payload(replay);
            return payload.Substring(0, payload.Length - 1) +
                ",\"replayHash\":" + Quote(replay.ReplayHash) + "}";
        }

        public static string ComputeHash(MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay));
        }

        public static string ComputeLegacyShadowCoverageHash(MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay, true, true, true));
        }

        public static string ComputeLegacyDefenseAttemptHash(MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay, false, false, true));
        }

        public static string ComputeLegacyShadowAndDefenseAttemptHash(
            MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay, true, false, true));
        }

        public static string ComputeLegacyWithoutScenarioHash(MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay, false, true, false));
        }

        public static string ComputeHistoricalLegacyShadowCoverageHash(
            MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay, true, true, false));
        }

        public static string ComputeHistoricalLegacyDefenseAttemptHash(
            MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay, false, false, false));
        }

        public static string ComputeHistoricalLegacyShadowAndDefenseAttemptHash(
            MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay, true, false, false));
        }

        public static string ComputeLegacyAttackCoverageHash(
            MatchReplayV4 replay)
        {
            return ComputeHash(Payload(replay, false, true, true, false));
        }

        public static bool MatchesSupportedLegacyHash(
            MatchReplayV4 replay,
            string suppliedHash)
        {
            for (var legacyShadowCoverage = 0;
                 legacyShadowCoverage <= 1;
                 legacyShadowCoverage++)
            {
                for (var includeDefenseAttempts = 0;
                     includeDefenseAttempts <= 1;
                     includeDefenseAttempts++)
                {
                    for (var includeScenario = 0;
                         includeScenario <= 1;
                         includeScenario++)
                    {
                        for (var includeAttackCoverage = 0;
                             includeAttackCoverage <= 1;
                             includeAttackCoverage++)
                        {
                            if (legacyShadowCoverage == 0 &&
                                includeDefenseAttempts == 1 &&
                                includeScenario == 1 &&
                                includeAttackCoverage == 1)
                            {
                                continue;
                            }

                            var candidate = ComputeHash(Payload(
                                replay,
                                legacyShadowCoverage == 1,
                                includeDefenseAttempts == 1,
                                includeScenario == 1,
                                includeAttackCoverage == 1));
                            if (string.Equals(
                                    candidate,
                                    suppliedHash,
                                    StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static string ComputeHash(string payload)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(HashFamily + payload);
            var digest = sha.ComputeHash(bytes);
            var output = new StringBuilder(64);
            for (var index = 0; index < digest.Length; index++)
            {
                output.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return output.ToString();
        }

        public static MatchReplayV4 Deserialize(StrictJsonObjectV4 root)
        {
            var hasSourceSequenceAnchor =
                root.Properties.ContainsKey("sourceSequenceAnchor");
            var hasDefenseAttempts = root.Properties.ContainsKey(
                "defenseAttempts");
            var hasScenario = root.Properties.ContainsKey("scenario");
            var expectedProperties = (hasSourceSequenceAnchor ? 6 : 5) +
                (hasDefenseAttempts ? 1 : 0) + (hasScenario ? 1 : 0);
            if (root.Properties.Count != expectedProperties)
            {
                throw new ContractValidationException(
                    "JSON object fields do not match the native V4 schema.");
            }
            var requiredRootFields = new[]
            {
                "formatVersion", "replayId", "context", "events", "replayHash"
            };
            for (var index = 0; index < requiredRootFields.Length; index++)
            {
                if (!root.Properties.ContainsKey(requiredRootFields[index]))
                {
                    throw new ContractValidationException(
                        "Required native V4 JSON field is missing: " +
                        requiredRootFields[index] + ".");
                }
            }
            var formatVersion = StrictJsonV4.RequiredInt(
                root,
                "formatVersion");
            if (formatVersion != ContractVersions.ReplayV4)
            {
                throw new ContractValidationException(
                    "Unsupported replay format version: " +
                    formatVersion +
                    ".");
            }

            var context = ContractJson.DeserializeMatchContextV4(
                StrictJsonV4.ToJson(
                    StrictJsonV4.RequiredObject(root, "context")));
            var eventValues = StrictJsonV4.RequiredArray(root, "events");
            var events = new MatchReplayEventV4[eventValues.Count];
            var hasLegacyShadowCoverage = false;
            var hasCurrentShadowCoverage = false;
            var hasLegacyAttackCoverageResponsibilities = false;
            var hasCurrentAttackCoverageResponsibilities = false;
            for (var index = 0; index < events.Length; index++)
            {
                var eventValue = StrictJsonV4.AsObject(
                    eventValues[index],
                    "events[" + index + "]");
                if (HasShadowCoverage(eventValue))
                {
                    if (HasLegacyShadowCoverage(eventValue))
                    {
                        hasLegacyShadowCoverage = true;
                    }
                    else
                    {
                        hasCurrentShadowCoverage = true;
                    }
                }
                if (HasLegacyAttackCoverageResponsibilities(eventValue))
                {
                    hasLegacyAttackCoverageResponsibilities = true;
                }
                else if (HasCurrentAttackCoverageResponsibilities(eventValue))
                {
                    hasCurrentAttackCoverageResponsibilities = true;
                }
                events[index] = ParseEvent(eventValue);
            }
            var attemptValues = hasDefenseAttempts
                ? StrictJsonV4.RequiredArray(root, "defenseAttempts")
                : new List<StrictJsonValueV4>();
            var defenseAttempts = new ReplayDefenseAttemptRecordV4[
                attemptValues.Count];
            for (var index = 0; index < defenseAttempts.Length; index++)
                defenseAttempts[index] = ParseDefenseAttempt(
                    StrictJsonV4.AsObject(attemptValues[index],
                        "defenseAttempts[" + index + "]"));
            var scenario = hasScenario
                ? ParseScenario(StrictJsonV4.RequiredObject(root, "scenario"))
                : ReplayScenarioProvenanceV4.Default;

            if (hasLegacyShadowCoverage && hasCurrentShadowCoverage)
            {
                throw new ContractValidationException(
                    "Mixed legacy and current shadow coverage is not supported.");
            }
            if (hasLegacyAttackCoverageResponsibilities &&
                hasCurrentAttackCoverageResponsibilities)
            {
                throw new ContractValidationException(
                    "Mixed legacy and current attack coverage responsibilities are not supported.");
            }

            return MatchReplayV4.Restore(
                StrictJsonV4.RequiredString(root, "replayId"),
                context,
                events,
                defenseAttempts,
                scenario,
                hasSourceSequenceAnchor
                    ? StrictJsonV4.RequiredInt(root, "sourceSequenceAnchor")
                    : 0,
                StrictJsonV4.RequiredString(root, "replayHash"),
                hasLegacyShadowCoverage ||
                hasLegacyAttackCoverageResponsibilities ||
                !hasDefenseAttempts ||
                !hasScenario);
        }

        private static ReplayScenarioProvenanceV4 ParseScenario(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(value,
                "scenarioId", "formatVersion", "contentHash");
            return new ReplayScenarioProvenanceV4(
                StrictJsonV4.RequiredString(value, "scenarioId"),
                StrictJsonV4.RequiredInt(value, "formatVersion"),
                StrictJsonV4.RequiredString(value, "contentHash"));
        }

        private static ReplayDefenseAttemptRecordV4 ParseDefenseAttempt(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(value,
                "attemptIdentity", "kind", "commandKind", "actorPlayerId",
                "team", "planRevision", "sourceSequenceNumber",
                "envelopeIdentity", "trajectoryArtifactIdentity",
                "windowStartSimulationTime", "windowEndSimulationTime",
                "simulationTimeSeconds", "ballPosition", "ballVelocity",
                "continuationState", "reason", "winningContactGroupId",
                "winningActorPlayerId");
            var group = StrictJsonV4.RequiredNullableInt(value,
                "winningContactGroupId");
            return new ReplayDefenseAttemptRecordV4(
                StrictJsonV4.RequiredString(value, "attemptIdentity"),
                StrictJsonV4.RequiredString(value, "kind"),
                StrictJsonV4.RequiredString(value, "commandKind"),
                StrictJsonV4.RequiredString(value, "actorPlayerId"),
                StrictJsonV4.RequiredString(value, "team"),
                StrictJsonV4.RequiredInt(value, "planRevision"),
                StrictJsonV4.RequiredInt(value, "sourceSequenceNumber"),
                StrictJsonV4.RequiredString(value, "envelopeIdentity"),
                StrictJsonV4.RequiredString(value, "trajectoryArtifactIdentity"),
                StrictJsonV4.RequiredFloat(value, "windowStartSimulationTime"),
                StrictJsonV4.RequiredFloat(value, "windowEndSimulationTime"),
                StrictJsonV4.RequiredFloat(value, "simulationTimeSeconds"),
                ParseVector(StrictJsonV4.RequiredObject(value, "ballPosition")),
                ParseVector(StrictJsonV4.RequiredObject(value, "ballVelocity")),
                StrictJsonV4.RequiredString(value, "continuationState"),
                StrictJsonV4.RequiredString(value, "reason"), group,
                StrictJsonV4.RequiredNullableString(value,
                    "winningActorPlayerId"));
        }

        private static bool HasShadowCoverage(StrictJsonObjectV4 value)
        {
            return StrictJsonV4.OptionalNullableObject(value, "shadow") != null;
        }

        private static bool HasLegacyShadowCoverage(StrictJsonObjectV4 value)
        {
            var shadow = StrictJsonV4.OptionalNullableObject(value, "shadow");
            if (shadow == null)
            {
                return false;
            }

            var coverage = StrictJsonV4.RequiredObject(shadow, "coverage");
            return coverage.Properties.Count == 2 &&
                coverage.Properties.ContainsKey("decision") &&
                coverage.Properties.ContainsKey("score");
        }

        private static bool HasLegacyAttackCoverageResponsibilities(
            StrictJsonObjectV4 value)
        {
            var authority = StrictJsonV4.OptionalNullableObject(
                value,
                "attackDefenseAuthority");
            return authority != null &&
                !authority.Properties.ContainsKey(
                    "attackCoverageResponsibilities");
        }

        private static bool HasCurrentAttackCoverageResponsibilities(
            StrictJsonObjectV4 value)
        {
            var authority = StrictJsonV4.OptionalNullableObject(
                value,
                "attackDefenseAuthority");
            return authority != null &&
                authority.Properties.ContainsKey(
                    "attackCoverageResponsibilities");
        }

        private static MatchReplayEventV4 ParseEvent(
            StrictJsonObjectV4 value)
        {
            RequireEventProperties(value);
            var consumptionValues = StrictJsonV4.RequiredArray(
                value,
                "abilityConsumptions");
            var consumptions =
                new ReplayAbilityConsumptionRecordV4[
                    consumptionValues.Count];
            for (var index = 0; index < consumptions.Length; index++)
            {
                var consumption = StrictJsonV4.AsObject(
                    consumptionValues[index],
                    "abilityConsumptions[" + index + "]");
                StrictJsonV4.RequireExactProperties(
                    consumption,
                    "playerId",
                    "derivedAttributesFingerprint",
                    "attributeName",
                    "value",
                    "evidenceKind");
                consumptions[index] =
                    new ReplayAbilityConsumptionRecordV4(
                        StrictJsonV4.RequiredString(
                            consumption,
                            "playerId"),
                        StrictJsonV4.RequiredString(
                            consumption,
                            "derivedAttributesFingerprint"),
                        StrictJsonV4.RequiredString(
                            consumption,
                            "attributeName"),
                        StrictJsonV4.RequiredFloat(
                            consumption,
                            "value"),
                        StrictJsonV4.RequiredString(
                            consumption,
                            "evidenceKind"));
            }

            var geometryValue = StrictJsonV4.RequiredNullableObject(
                value,
                "observedP6Geometry");
            return new MatchReplayEventV4(
                StrictJsonV4.RequiredInt(value, "sequenceNumber"),
                StrictJsonV4.RequiredString(value, "eventKind"),
                StrictJsonV4.RequiredString(value, "actorPlayerId"),
                StrictJsonV4.RequiredFloat(
                    value,
                    "simulationTimeSeconds"),
                StrictJsonV4.RequiredInt(value, "homeScore"),
                StrictJsonV4.RequiredInt(value, "awayScore"),
                ParseEnvelope(
                    StrictJsonV4.RequiredObject(
                        value,
                        "testedEnvelope")),
                ParseEnvelope(
                    StrictJsonV4.RequiredObject(
                        value,
                        "executableEnvelope")),
                ParseTrajectory(
                    StrictJsonV4.RequiredObject(value, "trajectory")),
                consumptions,
                ParseClassification(
                    StrictJsonV4.RequiredObject(
                        value,
                        "classification")),
                geometryValue == null
                    ? null
                    : ParseGeometry(geometryValue),
                ParseRuleDecision(
                    StrictJsonV4.RequiredObject(value, "ruleDecision")),
                ParseShadow(
                    StrictJsonV4.OptionalNullableObject(value, "shadow")),
                ParseOrganizationAuthority(
                    StrictJsonV4.OptionalNullableObject(
                        value,
                        "organizationAuthority")),
                ParseAttackDefenseAuthority(
                    StrictJsonV4.OptionalNullableObject(
                        value,
                        "attackDefenseAuthority")),
                ParsePerceptionAuthority(
                    StrictJsonV4.OptionalNullableObject(
                        value,
                        "perceptionAuthority")),
                ParseWorkBudget(
                    StrictJsonV4.OptionalNullableObject(
                        value,
                        "workBudget")));
        }

        private static void RequireEventProperties(StrictJsonObjectV4 value)
        {
            var hasShadow = value.Properties.ContainsKey("shadow");
            var hasAuthority =
                value.Properties.ContainsKey("organizationAuthority");
            var hasAttackDefenseAuthority =
                value.Properties.ContainsKey("attackDefenseAuthority");
            var hasPerceptionAuthority =
                value.Properties.ContainsKey("perceptionAuthority");
            var hasWorkBudget =
                value.Properties.ContainsKey("workBudget");
            if (value.Properties.Count !=
                13 + (hasShadow ? 1 : 0) + (hasAuthority ? 1 : 0) +
                (hasAttackDefenseAuthority ? 1 : 0) +
                (hasPerceptionAuthority ? 1 : 0) +
                (hasWorkBudget ? 1 : 0))
            {
                throw new ContractValidationException(
                    "JSON object fields do not match the native V4 schema.");
            }

            var required = new[]
            {
                "sequenceNumber", "eventKind", "actorPlayerId",
                "simulationTimeSeconds", "homeScore", "awayScore",
                "testedEnvelope", "executableEnvelope", "trajectory",
                "abilityConsumptions", "classification", "observedP6Geometry",
                "ruleDecision"
            };
            for (var index = 0; index < required.Length; index++)
            {
                if (!value.Properties.ContainsKey(required[index]))
                {
                    throw new ContractValidationException(
                        "Required native V4 JSON field is missing: " + required[index] + ".");
                }
            }
        }

        private static ReplayOrganizationAuthorityRecordV4
            ParseOrganizationAuthority(StrictJsonObjectV4 value)
        {
            if (value == null) return null;
            StrictJsonV4.RequireExactProperties(
                value,
                "planRevision",
                "sourceSequenceNumber",
                "authorityPhase",
                "organizationTarget",
                "actualFirstPassLanding",
                "zoneGrade",
                "registeredSetterPlayerId",
                "setterStatus",
                "setterMovementMeters",
                "setterReactionDelaySeconds",
                "setterReachMarginMeters",
                "organizerPlayerId",
                "fallbackReason",
                "activatedBranch",
                "testedEnvelopeIdentity",
                "executableEnvelopeIdentity",
                "sampleEnvelopeIdentity",
                "trajectoryArtifactIdentity",
                "coverage");
            var landing = StrictJsonV4.RequiredNullableObject(
                value,
                "actualFirstPassLanding");
            return new ReplayOrganizationAuthorityRecordV4(
                StrictJsonV4.RequiredInt(value, "planRevision"),
                StrictJsonV4.RequiredInt(value, "sourceSequenceNumber"),
                StrictJsonV4.RequiredString(value, "authorityPhase"),
                ParseVector(
                    StrictJsonV4.RequiredObject(
                        value,
                        "organizationTarget")),
                landing == null ? null : ParseVector(landing),
                StrictJsonV4.RequiredString(value, "zoneGrade"),
                StrictJsonV4.RequiredString(
                    value,
                    "registeredSetterPlayerId"),
                StrictJsonV4.RequiredString(value, "setterStatus"),
                StrictJsonV4.RequiredFloat(value, "setterMovementMeters"),
                StrictJsonV4.RequiredFloat(
                    value,
                    "setterReactionDelaySeconds"),
                StrictJsonV4.RequiredFloat(value, "setterReachMarginMeters"),
                StrictJsonV4.RequiredNullableString(
                    value,
                    "organizerPlayerId"),
                StrictJsonV4.RequiredString(value, "fallbackReason"),
                StrictJsonV4.RequiredNullableString(
                    value,
                    "activatedBranch"),
                StrictJsonV4.RequiredString(
                    value,
                    "testedEnvelopeIdentity"),
                StrictJsonV4.RequiredString(
                    value,
                    "executableEnvelopeIdentity"),
                StrictJsonV4.RequiredString(
                    value,
                    "sampleEnvelopeIdentity"),
                StrictJsonV4.RequiredString(
                    value,
                    "trajectoryArtifactIdentity"),
                ParseCoverage(
                    StrictJsonV4.RequiredObject(value, "coverage")));
        }

        private static ReplayAttackDefenseAuthorityRecordV4
            ParseAttackDefenseAuthority(StrictJsonObjectV4 value)
        {
            if (value == null) return null;
            var hasAttackCoverage = value.Properties.ContainsKey(
                "attackCoverageResponsibilities");
            if (hasAttackCoverage)
            {
                StrictJsonV4.RequireExactProperties(value, "planRevision",
                    "sourceSequenceNumber", "phase", "branch", "setTarget",
                    "candidates", "publicThreat", "defenseResponsibilities",
                    "attackCoverageResponsibilities",
                    "selectedCandidateIdentity", "testedEnvelopeIdentity",
                    "executableEnvelopeIdentity", "sampleEnvelopeIdentity",
                    "trajectoryArtifactIdentity", "recovery", "coverage");
            }
            else
            {
                StrictJsonV4.RequireExactProperties(value, "planRevision",
                    "sourceSequenceNumber", "phase", "branch", "setTarget",
                    "candidates", "publicThreat", "defenseResponsibilities",
                    "selectedCandidateIdentity", "testedEnvelopeIdentity",
                    "executableEnvelopeIdentity", "sampleEnvelopeIdentity",
                    "trajectoryArtifactIdentity", "recovery", "coverage");
            }
            var candidateValues = StrictJsonV4.RequiredArray(value, "candidates");
            var candidates = new ReplayAttackDefenseCandidateRecordV4[candidateValues.Count];
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = StrictJsonV4.AsObject(candidateValues[index], "candidates[" + index + "]");
                StrictJsonV4.RequireExactProperties(candidate, "candidateIdentity", "actorPlayerId",
                    "actionClass", "target", "expectedRallyValue", "legalSampleRatio",
                    "isQualifiedPowerRoute", "eliminationReason", "envelopeIdentity",
                    "trajectoryArtifactIdentity", "reorganizationExitIdentity");
                candidates[index] = new ReplayAttackDefenseCandidateRecordV4(
                    StrictJsonV4.RequiredString(candidate, "candidateIdentity"),
                    StrictJsonV4.RequiredString(candidate, "actorPlayerId"),
                    StrictJsonV4.RequiredString(candidate, "actionClass"),
                    ParseVector(StrictJsonV4.RequiredObject(candidate, "target")),
                    StrictJsonV4.RequiredFloat(candidate, "expectedRallyValue"),
                    StrictJsonV4.RequiredFloat(candidate, "legalSampleRatio"),
                    StrictJsonV4.RequiredBoolean(candidate, "isQualifiedPowerRoute"),
                    StrictJsonV4.RequiredString(candidate, "eliminationReason"),
                    StrictJsonV4.RequiredString(candidate, "envelopeIdentity"),
                    StrictJsonV4.RequiredString(candidate, "trajectoryArtifactIdentity"),
                    StrictJsonV4.RequiredString(candidate, "reorganizationExitIdentity"));
            }
            var threatValues = StrictJsonV4.RequiredArray(value, "publicThreat");
            var threat = new ReplayPublicAttackThreatRecordV4[threatValues.Count];
            for (var index = 0; index < threat.Length; index++)
            {
                var item = StrictJsonV4.AsObject(threatValues[index], "publicThreat[" + index + "]");
                StrictJsonV4.RequireExactProperties(item, "actionClass", "zone", "probability", "arrivalTime");
                threat[index] = new ReplayPublicAttackThreatRecordV4(
                    StrictJsonV4.RequiredString(item, "actionClass"),
                    StrictJsonV4.RequiredString(item, "zone"),
                    StrictJsonV4.RequiredFloat(item, "probability"),
                    StrictJsonV4.RequiredFloat(item, "arrivalTime"));
            }
            var defenseValues = StrictJsonV4.RequiredArray(value, "defenseResponsibilities");
            var defense = new ReplayDefenseResponsibilityRecordV4[defenseValues.Count];
            for (var index = 0; index < defense.Length; index++)
            {
                var item = StrictJsonV4.AsObject(defenseValues[index], "defenseResponsibilities[" + index + "]");
                StrictJsonV4.RequireExactProperties(item, "actorPlayerId", "kind", "zone", "branch");
                defense[index] = new ReplayDefenseResponsibilityRecordV4(
                    StrictJsonV4.RequiredString(item, "actorPlayerId"),
                    StrictJsonV4.RequiredString(item, "kind"), StrictJsonV4.RequiredString(item, "zone"),
                    StrictJsonV4.RequiredString(item, "branch"));
            }
            var attackCoverageValues = hasAttackCoverage
                ? StrictJsonV4.RequiredArray(
                    value,
                    "attackCoverageResponsibilities")
                : new List<StrictJsonValueV4>();
            var attackCoverage =
                new ReplayAttackCoverageResponsibilityRecordV4[
                    attackCoverageValues.Count];
            for (var index = 0;
                 index < attackCoverage.Length;
                 index++)
            {
                var item = StrictJsonV4.AsObject(
                    attackCoverageValues[index],
                    "attackCoverageResponsibilities[" + index + "]");
                StrictJsonV4.RequireExactProperties(
                    item,
                    "actorPlayerId",
                    "branch");
                attackCoverage[index] =
                    new ReplayAttackCoverageResponsibilityRecordV4(
                        StrictJsonV4.RequiredString(
                            item,
                            "actorPlayerId"),
                        StrictJsonV4.RequiredString(item, "branch"));
            }
            var recoveryValue = StrictJsonV4.RequiredNullableObject(value, "recovery");
            ReplayToolRecoveryRecordV4 recovery = null;
            if (recoveryValue != null)
            {
                StrictJsonV4.RequireExactProperties(recoveryValue, "candidateIdentity", "blockerPlayerId",
                    "reboundSide", "recoveryPlayerId", "reorganizationExitIdentity",
                    "reboundTrajectoryArtifactIdentity", "reboundSampleIdentity",
                    "blockContactIdentity", "remainingTouches");
                recovery = new ReplayToolRecoveryRecordV4(
                    StrictJsonV4.RequiredString(recoveryValue, "candidateIdentity"),
                    StrictJsonV4.RequiredString(recoveryValue, "blockerPlayerId"),
                    StrictJsonV4.RequiredString(recoveryValue, "reboundSide"),
                    StrictJsonV4.RequiredString(recoveryValue, "recoveryPlayerId"),
                    StrictJsonV4.RequiredString(recoveryValue, "reorganizationExitIdentity"),
                    StrictJsonV4.RequiredString(recoveryValue, "reboundTrajectoryArtifactIdentity"),
                    StrictJsonV4.RequiredString(recoveryValue, "reboundSampleIdentity"),
                    StrictJsonV4.RequiredString(recoveryValue, "blockContactIdentity"),
                    StrictJsonV4.RequiredInt(recoveryValue, "remainingTouches"));
            }
            return new ReplayAttackDefenseAuthorityRecordV4(
                StrictJsonV4.RequiredInt(value, "planRevision"), StrictJsonV4.RequiredInt(value, "sourceSequenceNumber"),
                StrictJsonV4.RequiredString(value, "phase"), StrictJsonV4.RequiredString(value, "branch"),
                ParseVector(StrictJsonV4.RequiredObject(value, "setTarget")), candidates, threat, defense,
                StrictJsonV4.RequiredString(value, "selectedCandidateIdentity"),
                StrictJsonV4.RequiredString(value, "testedEnvelopeIdentity"), StrictJsonV4.RequiredString(value, "executableEnvelopeIdentity"),
                StrictJsonV4.RequiredString(value, "sampleEnvelopeIdentity"), StrictJsonV4.RequiredString(value, "trajectoryArtifactIdentity"),
                recovery,
                ParseCoverage(StrictJsonV4.RequiredObject(value, "coverage")),
                attackCoverage);
        }

        private static ReplayPerceptionAuthorityRecordV4
            ParsePerceptionAuthority(StrictJsonObjectV4 value)
        {
            if (value == null) return null;
            StrictJsonV4.RequireExactProperties(value,
                "configurationIdentity", "viewIdentity", "observingSide",
                "authoritativeArtifactIdentity",
                "observedAtSimulationTime", "recognitionDelaySeconds",
                "uncertaintyKey", "positionUncertaintyMeters", "confidence",
                "visibleThreats", "selectedSupportPlayerId",
                "selectedSupportZone", "conservativeFallback",
                "affectedRevision", "sourceSequenceNumber");
            var values = StrictJsonV4.RequiredArray(value, "visibleThreats");
            var threats = new ReplayPerceivedThreatRecordV4[values.Count];
            for (var index = 0; index < threats.Length; index++)
            {
                var item = StrictJsonV4.AsObject(values[index],
                    "visibleThreats[" + index + "]");
                StrictJsonV4.RequireExactProperties(item, "identity", "zone",
                    "confidence", "arrivalTime");
                threats[index] = new ReplayPerceivedThreatRecordV4(
                    StrictJsonV4.RequiredString(item, "identity"),
                    StrictJsonV4.RequiredString(item, "zone"),
                    StrictJsonV4.RequiredFloat(item, "confidence"),
                    StrictJsonV4.RequiredFloat(item, "arrivalTime"));
            }
            for (var index = 1; index < threats.Length; index++)
                if (string.CompareOrdinal(threats[index - 1].Identity,
                        threats[index].Identity) >= 0)
                    throw new ContractValidationException(
                        "visibleThreats must use canonical identity order.");
            return new ReplayPerceptionAuthorityRecordV4(
                StrictJsonV4.RequiredString(value, "configurationIdentity"),
                StrictJsonV4.RequiredString(value, "viewIdentity"),
                StrictJsonV4.RequiredString(value, "observingSide"),
                StrictJsonV4.RequiredString(value,
                    "authoritativeArtifactIdentity"),
                StrictJsonV4.RequiredFloat(value,
                    "observedAtSimulationTime"),
                StrictJsonV4.RequiredFloat(value,
                    "recognitionDelaySeconds"),
                StrictJsonV4.RequiredString(value, "uncertaintyKey"),
                StrictJsonV4.RequiredFloat(value,
                    "positionUncertaintyMeters"),
                StrictJsonV4.RequiredFloat(value, "confidence"),
                threats,
                StrictJsonV4.RequiredString(value,
                    "selectedSupportPlayerId"),
                StrictJsonV4.RequiredString(value, "selectedSupportZone"),
                StrictJsonV4.RequiredBoolean(value,
                    "conservativeFallback"),
                StrictJsonV4.RequiredInt(value, "affectedRevision"),
                StrictJsonV4.RequiredInt(value, "sourceSequenceNumber"));
        }

        private static ReplayWorkBudgetRecordV4 ParseWorkBudget(
            StrictJsonObjectV4 value)
        {
            if (value == null) return null;
            StrictJsonV4.RequireExactProperties(value,
                "configurationIdentity", "candidateCount", "sampleCount",
                "expansionCount", "deterministicWorkUnits",
                "degradationStep", "budgetOutcome");
            return new ReplayWorkBudgetRecordV4(
                StrictJsonV4.RequiredString(value,
                    "configurationIdentity"),
                StrictJsonV4.RequiredInt(value, "candidateCount"),
                StrictJsonV4.RequiredInt(value, "sampleCount"),
                StrictJsonV4.RequiredInt(value, "expansionCount"),
                StrictJsonV4.RequiredInt(value,
                    "deterministicWorkUnits"),
                StrictJsonV4.RequiredString(value, "degradationStep"),
                StrictJsonV4.RequiredString(value, "budgetOutcome"));
        }

        private static ReplayShadowRecordV4 ParseShadow(StrictJsonObjectV4 value)
        {
            if (value == null) return null;
            StrictJsonV4.RequireExactProperties(
                value,
                "revision",
                "sourceSequenceNumber",
                "artifactIdentity",
                "home",
                "away",
                "coverage");
            return new ReplayShadowRecordV4(
                StrictJsonV4.RequiredInt(value, "revision"),
                StrictJsonV4.RequiredInt(value, "sourceSequenceNumber"),
                StrictJsonV4.RequiredString(value, "artifactIdentity"),
                ParseTeamPlan(StrictJsonV4.RequiredObject(value, "home")),
                ParseTeamPlan(StrictJsonV4.RequiredObject(value, "away")),
                ParseCoverage(StrictJsonV4.RequiredObject(value, "coverage")));
        }

        private static ReplayTeamRallyPlanRecordV4 ParseTeamPlan(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(value, "teamSide", "primaryAssignments");
            var values = StrictJsonV4.RequiredArray(value, "primaryAssignments");
            var assignments = new ReplayShadowAssignmentRecordV4[values.Count];
            for (var index = 0; index < assignments.Length; index++)
            {
                var assignment = StrictJsonV4.AsObject(
                    values[index], "primaryAssignments[" + index + "]");
                StrictJsonV4.RequireExactProperties(
                    assignment, "rank", "playerId", "task", "condition", "spatialClaim",
                    "declaredBranch", "value");
                assignments[index] = new ReplayShadowAssignmentRecordV4(
                    StrictJsonV4.RequiredInt(assignment, "rank"),
                    StrictJsonV4.RequiredString(assignment, "playerId"),
                    StrictJsonV4.RequiredString(assignment, "task"),
                    StrictJsonV4.RequiredString(assignment, "condition"),
                    StrictJsonV4.RequiredString(assignment, "spatialClaim"),
                    StrictJsonV4.RequiredString(assignment, "declaredBranch"),
                    StrictJsonV4.RequiredFloat(assignment, "value"));
            }

            return new ReplayTeamRallyPlanRecordV4(
                StrictJsonV4.RequiredString(value, "teamSide"), assignments);
        }

        private static ReplayCoverageDecisionRecordV4 ParseCoverage(
            StrictJsonObjectV4 value)
        {
            if (value.Properties.Count == 2 &&
                value.Properties.ContainsKey("decision") &&
                value.Properties.ContainsKey("score"))
            {
                var decision = NormalizeLegacyCoverageDecision(
                    StrictJsonV4.RequiredString(value, "decision"));
                return new ReplayCoverageDecisionRecordV4(
                    decision,
                    StrictJsonV4.RequiredFloat(value, "score"),
                    decision == "Terminal"
                        ? "RallyEnd"
                        : "WithinConditionalEnvelope",
                    Array.Empty<string>(),
                    0,
                    null);
            }

            StrictJsonV4.RequireExactProperties(
                value,
                "decision",
                "score",
                "reason",
                "invalidationSet",
                "expansionDepth",
                "activatedDeclaredBranch");
            var invalidationValues = StrictJsonV4.RequiredArray(value, "invalidationSet");
            var invalidationSet = new string[invalidationValues.Count];
            for (var index = 0; index < invalidationSet.Length; index++)
            {
                var invalidation = invalidationValues[index];
                if (invalidation.Kind != StrictJsonKindV4.String)
                {
                    throw new ContractValidationException(
                        "invalidationSet[" + index + "] must be a JSON string.");
                }

                invalidationSet[index] = (string)invalidation.Value;
            }

            var branchValue = StrictJsonV4.RequiredNullableString(
                value,
                "activatedDeclaredBranch");
            return new ReplayCoverageDecisionRecordV4(
                StrictJsonV4.RequiredString(value, "decision"),
                StrictJsonV4.RequiredFloat(value, "score"),
                StrictJsonV4.RequiredString(value, "reason"),
                invalidationSet,
                StrictJsonV4.RequiredInt(value, "expansionDepth"),
                branchValue);
        }

        private static string NormalizeLegacyCoverageDecision(string decision)
        {
            return decision == "Uncovered" ? "Terminal" : decision;
        }

        private static ReplayExecutionEnvelopeRecordV4 ParseEnvelope(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "version",
                "identity",
                "derivedAttributesFingerprint",
                "policyIdentity",
                "sourceIntentIdentity",
                "candidateCategory",
                "baselineTarget",
                "baselineVelocity",
                "maximumVelocity",
                "targetError",
                "velocityError",
                "requestedEffort",
                "maximumEffort",
                "samplingKey",
                "policyVersion",
                "sampleCount",
                "candidateCategoryOrder",
                "degradationLadder",
                "maximumExpansionCount",
                "allowedExpansionCount",
                "currentExpansionCount",
                "perStepExpansionFactor");
            return new ReplayExecutionEnvelopeRecordV4(
                StrictJsonV4.RequiredInt(value, "version"),
                StrictJsonV4.RequiredString(value, "identity"),
                StrictJsonV4.RequiredString(
                    value,
                    "derivedAttributesFingerprint"),
                StrictJsonV4.RequiredString(value, "policyIdentity"),
                StrictJsonV4.RequiredString(
                    value,
                    "sourceIntentIdentity"),
                StrictJsonV4.RequiredString(value, "candidateCategory"),
                ParseVector(
                    StrictJsonV4.RequiredObject(
                        value,
                        "baselineTarget")),
                ParseVector(
                    StrictJsonV4.RequiredObject(
                        value,
                        "baselineVelocity")),
                ParseVector(
                    StrictJsonV4.RequiredObject(
                        value,
                        "maximumVelocity")),
                ParseError(
                    StrictJsonV4.RequiredObject(value, "targetError")),
                ParseError(
                    StrictJsonV4.RequiredObject(value, "velocityError")),
                StrictJsonV4.RequiredFloat(value, "requestedEffort"),
                StrictJsonV4.RequiredFloat(value, "maximumEffort"),
                StrictJsonV4.RequiredString(value, "samplingKey"),
                StrictJsonV4.RequiredInt(value, "policyVersion"),
                StrictJsonV4.RequiredInt(value, "sampleCount"),
                ParseStrings(value, "candidateCategoryOrder"),
                ParseStrings(value, "degradationLadder"),
                StrictJsonV4.RequiredInt(
                    value,
                    "maximumExpansionCount"),
                StrictJsonV4.RequiredInt(
                    value,
                    "allowedExpansionCount"),
                StrictJsonV4.RequiredInt(
                    value,
                    "currentExpansionCount"),
                StrictJsonV4.RequiredFloat(
                    value,
                    "perStepExpansionFactor"));
        }

        private static ReplayTrajectoryArtifactRecordV4 ParseTrajectory(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "artifactIdentity",
                "predictorSource",
                "predictorVersion",
                "predictorConfigurationHash",
                "cacheKey");
            var cacheKey = StrictJsonV4.RequiredObject(value, "cacheKey");
            StrictJsonV4.RequireExactProperties(
                cacheKey,
                "identity",
                "ballStateVersion",
                "ballStateFingerprint",
                "physicsConfigurationHash",
                "samplingKey",
                "predictorVersion",
                "predictorConfigurationHash",
                "envelopeIdentity",
                "degradationStep");
            return new ReplayTrajectoryArtifactRecordV4(
                StrictJsonV4.RequiredString(value, "artifactIdentity"),
                StrictJsonV4.RequiredString(value, "predictorSource"),
                StrictJsonV4.RequiredInt(value, "predictorVersion"),
                StrictJsonV4.RequiredString(
                    value,
                    "predictorConfigurationHash"),
                new ReplayTrajectoryCacheKeyRecordV4(
                    StrictJsonV4.RequiredString(cacheKey, "identity"),
                    StrictJsonV4.RequiredLong(
                        cacheKey,
                        "ballStateVersion"),
                    StrictJsonV4.RequiredString(
                        cacheKey,
                        "ballStateFingerprint"),
                    StrictJsonV4.RequiredString(
                        cacheKey,
                        "physicsConfigurationHash"),
                    StrictJsonV4.RequiredString(
                        cacheKey,
                        "samplingKey"),
                    StrictJsonV4.RequiredInt(
                        cacheKey,
                        "predictorVersion"),
                    StrictJsonV4.RequiredString(
                        cacheKey,
                        "predictorConfigurationHash"),
                    StrictJsonV4.RequiredString(
                        cacheKey,
                        "envelopeIdentity"),
                    StrictJsonV4.RequiredString(
                        cacheKey,
                        "degradationStep")));
        }

        private static ReplaySampleClassificationRecordV4
            ParseClassification(StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "kind",
                "testedEnvelopeIdentity",
                "expandedEnvelopeIdentity",
                "actualSample",
                "offendingDimensions");
            var sample = StrictJsonV4.RequiredObject(
                value,
                "actualSample");
            StrictJsonV4.RequireExactProperties(
                sample,
                "envelopeIdentity",
                "samplingKey",
                "candidateCategory",
                "target",
                "velocity",
                "effort");
            return new ReplaySampleClassificationRecordV4(
                StrictJsonV4.RequiredString(value, "kind"),
                StrictJsonV4.RequiredString(
                    value,
                    "testedEnvelopeIdentity"),
                StrictJsonV4.RequiredString(
                    value,
                    "expandedEnvelopeIdentity"),
                new ReplayActualSampleRecordV4(
                    StrictJsonV4.RequiredString(
                        sample,
                        "envelopeIdentity"),
                    StrictJsonV4.RequiredString(sample, "samplingKey"),
                    StrictJsonV4.RequiredString(
                        sample,
                        "candidateCategory"),
                    ParseVector(
                        StrictJsonV4.RequiredObject(
                            sample,
                            "target")),
                    ParseVector(
                        StrictJsonV4.RequiredObject(
                            sample,
                            "velocity")),
                    StrictJsonV4.RequiredFloat(sample, "effort")),
                ParseStrings(value, "offendingDimensions"));
        }

        private static ReplayObservedP6GeometryRecordV4 ParseGeometry(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "actorPlayerId",
                "teamSide",
                "takeoffPoint",
                "contactPoint",
                "attackLineDistanceFromCenter",
                "netHeight",
                "isTakeoffInFrontZone",
                "isContactAboveNet");
            var geometry = new ReplayObservedP6GeometryRecordV4(
                StrictJsonV4.RequiredString(value, "actorPlayerId"),
                StrictJsonV4.RequiredString(value, "teamSide"),
                ParseVector(
                    StrictJsonV4.RequiredObject(value, "takeoffPoint")),
                ParseVector(
                    StrictJsonV4.RequiredObject(value, "contactPoint")),
                StrictJsonV4.RequiredFloat(
                    value,
                    "attackLineDistanceFromCenter"),
                StrictJsonV4.RequiredFloat(value, "netHeight"));
            if (geometry.IsTakeoffInFrontZone !=
                    StrictJsonV4.RequiredBoolean(
                        value,
                        "isTakeoffInFrontZone") ||
                geometry.IsContactAboveNet !=
                    StrictJsonV4.RequiredBoolean(
                        value,
                        "isContactAboveNet"))
            {
                throw new ContractValidationException(
                    "Observed P6 geometry diagnostic flags do not match its coordinates.");
            }

            return geometry;
        }

        private static ReplayRuleDecisionRecordV4 ParseRuleDecision(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "rulesVersion",
                "accepted",
                "reasonCode");
            return new ReplayRuleDecisionRecordV4(
                StrictJsonV4.RequiredInt(value, "rulesVersion"),
                StrictJsonV4.RequiredBoolean(value, "accepted"),
                StrictJsonV4.RequiredString(value, "reasonCode"));
        }

        private static ReplayBoundedErrorRecordV4 ParseError(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "distributionKind",
                "minimum",
                "maximum");
            return new ReplayBoundedErrorRecordV4(
                StrictJsonV4.RequiredString(value, "distributionKind"),
                ParseVector(
                    StrictJsonV4.RequiredObject(value, "minimum")),
                ParseVector(
                    StrictJsonV4.RequiredObject(value, "maximum")));
        }

        private static ReplayVector3RecordV4 ParseVector(
            StrictJsonObjectV4 value)
        {
            StrictJsonV4.RequireExactProperties(value, "x", "y", "z");
            return new ReplayVector3RecordV4(
                StrictJsonV4.RequiredFloat(value, "x"),
                StrictJsonV4.RequiredFloat(value, "y"),
                StrictJsonV4.RequiredFloat(value, "z"));
        }

        private static string[] ParseStrings(
            StrictJsonObjectV4 value,
            string name)
        {
            var values = StrictJsonV4.RequiredArray(value, name);
            var strings = new string[values.Count];
            for (var index = 0; index < strings.Length; index++)
            {
                if (values[index].Kind != StrictJsonKindV4.String)
                {
                    throw new ContractValidationException(
                        name + "[" + index + "] must be a JSON string.");
                }

                strings[index] = (string)values[index].Value;
            }

            return strings;
        }

        private static string Payload(
            MatchReplayV4 replay,
            bool legacyShadowCoverage = false,
            bool includeDefenseAttempts = true,
            bool includeScenario = true,
            bool includeAttackCoverageResponsibilities = true)
        {
            var output = new StringBuilder(32768);
            output.Append("{\"formatVersion\":4,\"replayId\":")
                .Append(Quote(replay.ReplayId));
            if (replay.SourceSequenceAnchor != 0)
            {
                output.Append(",\"sourceSequenceAnchor\":")
                    .Append(replay.SourceSequenceAnchor);
            }
            output.Append(",\"context\":")
                .Append(CanonicalMatchJsonV4.SerializeContext(replay.Context));
            output.Append(",\"events\":[");
            for (var index = 0; index < replay.Events.Count; index++)
            {
                if (index > 0) output.Append(',');
                AppendEvent(
                    output,
                    replay.Events[index],
                    legacyShadowCoverage,
                    includeAttackCoverageResponsibilities);
            }
            output.Append(']');
            if (includeDefenseAttempts)
            {
                output.Append(",\"defenseAttempts\":[");
                for (var index = 0; index < replay.DefenseAttempts.Count; index++)
                {
                    if (index > 0) output.Append(',');
                    AppendDefenseAttempt(output, replay.DefenseAttempts[index]);
                }
                output.Append(']');
            }
            if (includeScenario)
            {
                output.Append(",\"scenario\":{")
                    .Append("\"scenarioId\":")
                    .Append(Quote(replay.Scenario.ScenarioId))
                    .Append(",\"formatVersion\":")
                    .Append(replay.Scenario.FormatVersion)
                    .Append(",\"contentHash\":")
                    .Append(Quote(replay.Scenario.ContentHash))
                    .Append('}');
            }
            output.Append('}');
            return output.ToString();
        }

        private static void AppendDefenseAttempt(
            StringBuilder output,
            ReplayDefenseAttemptRecordV4 attempt)
        {
            output.Append("{\"attemptIdentity\":").Append(Quote(attempt.AttemptIdentity));
            output.Append(",\"kind\":").Append(Quote(attempt.Kind));
            output.Append(",\"commandKind\":").Append(Quote(attempt.CommandKind));
            output.Append(",\"actorPlayerId\":").Append(Quote(attempt.ActorPlayerId));
            output.Append(",\"team\":").Append(Quote(attempt.Team));
            output.Append(",\"planRevision\":").Append(attempt.PlanRevision);
            output.Append(",\"sourceSequenceNumber\":").Append(attempt.SourceSequenceNumber);
            output.Append(",\"envelopeIdentity\":").Append(Quote(attempt.EnvelopeIdentity));
            output.Append(",\"trajectoryArtifactIdentity\":").Append(Quote(attempt.TrajectoryArtifactIdentity));
            output.Append(",\"windowStartSimulationTime\":"); Float(output, attempt.WindowStartSimulationTime);
            output.Append(",\"windowEndSimulationTime\":"); Float(output, attempt.WindowEndSimulationTime);
            output.Append(",\"simulationTimeSeconds\":"); Float(output, attempt.SimulationTimeSeconds);
            output.Append(",\"ballPosition\":"); Vector(output, attempt.BallPosition);
            output.Append(",\"ballVelocity\":"); Vector(output, attempt.BallVelocity);
            output.Append(",\"continuationState\":").Append(Quote(attempt.ContinuationState));
            output.Append(",\"reason\":").Append(Quote(attempt.Reason));
            output.Append(",\"winningContactGroupId\":");
            if (attempt.WinningContactGroupId.HasValue) output.Append(attempt.WinningContactGroupId.Value); else output.Append("null");
            output.Append(",\"winningActorPlayerId\":");
            if (attempt.WinningActorPlayerId == null) output.Append("null"); else output.Append(Quote(attempt.WinningActorPlayerId));
            output.Append('}');
        }

        private static void AppendEvent(
            StringBuilder output,
            MatchReplayEventV4 replayEvent,
            bool legacyShadowCoverage,
            bool includeAttackCoverageResponsibilities)
        {
            output.Append("{\"sequenceNumber\":").Append(replayEvent.SequenceNumber);
            output.Append(",\"eventKind\":").Append(Quote(replayEvent.EventKind));
            output.Append(",\"actorPlayerId\":").Append(Quote(replayEvent.ActorPlayerId));
            output.Append(",\"simulationTimeSeconds\":");
            Float(output, replayEvent.SimulationTimeSeconds);
            output.Append(",\"homeScore\":").Append(replayEvent.HomeScore);
            output.Append(",\"awayScore\":").Append(replayEvent.AwayScore);
            output.Append(",\"testedEnvelope\":");
            AppendEnvelope(output, replayEvent.TestedEnvelope);
            output.Append(",\"executableEnvelope\":");
            AppendEnvelope(output, replayEvent.ExecutableEnvelope);
            output.Append(",\"trajectory\":");
            AppendTrajectory(output, replayEvent.Trajectory);
            output.Append(",\"abilityConsumptions\":[");
            for (var index = 0;
                 index < replayEvent.AbilityConsumptions.Count;
                 index++)
            {
                if (index > 0) output.Append(',');
                var record = replayEvent.AbilityConsumptions[index];
                output.Append("{\"playerId\":").Append(Quote(record.PlayerId));
                output.Append(",\"derivedAttributesFingerprint\":")
                    .Append(Quote(record.DerivedAttributesFingerprint));
                output.Append(",\"attributeName\":")
                    .Append(Quote(record.AttributeName));
                output.Append(",\"value\":");
                Float(output, record.Value);
                output.Append(",\"evidenceKind\":")
                    .Append(Quote(record.EvidenceKind))
                    .Append('}');
            }

            output.Append("],\"classification\":");
            AppendClassification(output, replayEvent.Classification);
            output.Append(",\"observedP6Geometry\":");
            if (replayEvent.ObservedP6Geometry == null)
            {
                output.Append("null");
            }
            else
            {
                AppendGeometry(output, replayEvent.ObservedP6Geometry);
            }

            output.Append(",\"ruleDecision\":{\"rulesVersion\":")
                .Append(replayEvent.RuleDecision.RulesVersion)
                .Append(",\"accepted\":")
                .Append(replayEvent.RuleDecision.Accepted ? "true" : "false")
                .Append(",\"reasonCode\":")
                .Append(Quote(replayEvent.RuleDecision.ReasonCode))
                .Append('}');
            if (replayEvent.Shadow != null)
            {
                output.Append(",\"shadow\":");
                AppendShadow(output, replayEvent.Shadow, legacyShadowCoverage);
            }

            if (replayEvent.OrganizationAuthority != null)
            {
                output.Append(",\"organizationAuthority\":");
                AppendOrganizationAuthority(
                    output,
                    replayEvent.OrganizationAuthority);
            }

            if (replayEvent.AttackDefenseAuthority != null)
            {
                output.Append(",\"attackDefenseAuthority\":");
                AppendAttackDefenseAuthority(
                    output,
                    replayEvent.AttackDefenseAuthority,
                    includeAttackCoverageResponsibilities);
            }

            if (replayEvent.PerceptionAuthority != null)
            {
                output.Append(",\"perceptionAuthority\":");
                AppendPerceptionAuthority(output,
                    replayEvent.PerceptionAuthority);
            }

            if (replayEvent.WorkBudget != null)
            {
                output.Append(",\"workBudget\":");
                AppendWorkBudget(output, replayEvent.WorkBudget);
            }

            output.Append('}');
        }

        private static void AppendWorkBudget(StringBuilder output,
            ReplayWorkBudgetRecordV4 work)
        {
            output.Append("{\"configurationIdentity\":")
                .Append(Quote(work.ConfigurationIdentity));
            output.Append(",\"candidateCount\":")
                .Append(work.CandidateCount);
            output.Append(",\"sampleCount\":")
                .Append(work.SampleCount);
            output.Append(",\"expansionCount\":")
                .Append(work.ExpansionCount);
            output.Append(",\"deterministicWorkUnits\":")
                .Append(work.DeterministicWorkUnits);
            output.Append(",\"degradationStep\":")
                .Append(Quote(work.DegradationStep));
            output.Append(",\"budgetOutcome\":")
                .Append(Quote(work.BudgetOutcome))
                .Append('}');
        }

        private static void AppendPerceptionAuthority(StringBuilder output,
            ReplayPerceptionAuthorityRecordV4 perception)
        {
            output.Append("{\"configurationIdentity\":")
                .Append(Quote(perception.ConfigurationIdentity));
            output.Append(",\"viewIdentity\":")
                .Append(Quote(perception.ViewIdentity));
            output.Append(",\"observingSide\":")
                .Append(Quote(perception.ObservingSide));
            output.Append(",\"authoritativeArtifactIdentity\":")
                .Append(Quote(perception.AuthoritativeArtifactIdentity));
            output.Append(",\"observedAtSimulationTime\":");
            Float(output, perception.ObservedAtSimulationTime);
            output.Append(",\"recognitionDelaySeconds\":");
            Float(output, perception.RecognitionDelaySeconds);
            output.Append(",\"uncertaintyKey\":")
                .Append(Quote(perception.UncertaintyKey));
            output.Append(",\"positionUncertaintyMeters\":");
            Float(output, perception.PositionUncertaintyMeters);
            output.Append(",\"confidence\":");
            Float(output, perception.Confidence);
            output.Append(",\"visibleThreats\":[");
            for (var index = 0;
                 index < perception.VisibleThreats.Count;
                 index++)
            {
                if (index > 0) output.Append(',');
                var threat = perception.VisibleThreats[index];
                output.Append("{\"identity\":")
                    .Append(Quote(threat.Identity));
                output.Append(",\"zone\":").Append(Quote(threat.Zone));
                output.Append(",\"confidence\":");
                Float(output, threat.Confidence);
                output.Append(",\"arrivalTime\":");
                Float(output, threat.ArrivalTime);
                output.Append('}');
            }
            output.Append("],\"selectedSupportPlayerId\":")
                .Append(Quote(perception.SelectedSupportPlayerId));
            output.Append(",\"selectedSupportZone\":")
                .Append(Quote(perception.SelectedSupportZone));
            output.Append(",\"conservativeFallback\":")
                .Append(perception.ConservativeFallback ? "true" : "false");
            output.Append(",\"affectedRevision\":")
                .Append(perception.AffectedRevision);
            output.Append(",\"sourceSequenceNumber\":")
                .Append(perception.SourceSequenceNumber)
                .Append('}');
        }

        private static void AppendOrganizationAuthority(
            StringBuilder output,
            ReplayOrganizationAuthorityRecordV4 authority)
        {
            output.Append("{\"planRevision\":")
                .Append(authority.PlanRevision);
            output.Append(",\"sourceSequenceNumber\":")
                .Append(authority.SourceSequenceNumber);
            output.Append(",\"authorityPhase\":")
                .Append(Quote(authority.AuthorityPhase));
            output.Append(",\"organizationTarget\":");
            Vector(output, authority.OrganizationTarget);
            output.Append(",\"actualFirstPassLanding\":");
            if (authority.ActualFirstPassLanding == null)
            {
                output.Append("null");
            }
            else
            {
                Vector(output, authority.ActualFirstPassLanding);
            }

            output.Append(",\"zoneGrade\":")
                .Append(Quote(authority.ZoneGrade));
            output.Append(",\"registeredSetterPlayerId\":")
                .Append(Quote(authority.RegisteredSetterPlayerId));
            output.Append(",\"setterStatus\":")
                .Append(Quote(authority.SetterStatus));
            output.Append(",\"setterMovementMeters\":");
            Float(output, authority.SetterMovementMeters);
            output.Append(",\"setterReactionDelaySeconds\":");
            Float(output, authority.SetterReactionDelaySeconds);
            output.Append(",\"setterReachMarginMeters\":");
            Float(output, authority.SetterReachMarginMeters);
            output.Append(",\"organizerPlayerId\":");
            AppendNullableString(output, authority.OrganizerPlayerId);
            output.Append(",\"fallbackReason\":")
                .Append(Quote(authority.FallbackReason));
            output.Append(",\"activatedBranch\":");
            AppendNullableString(output, authority.ActivatedBranch);
            output.Append(",\"testedEnvelopeIdentity\":")
                .Append(Quote(authority.TestedEnvelopeIdentity));
            output.Append(",\"executableEnvelopeIdentity\":")
                .Append(Quote(authority.ExecutableEnvelopeIdentity));
            output.Append(",\"sampleEnvelopeIdentity\":")
                .Append(Quote(authority.SampleEnvelopeIdentity));
            output.Append(",\"trajectoryArtifactIdentity\":")
                .Append(Quote(authority.TrajectoryArtifactIdentity));
            output.Append(",\"coverage\":");
            AppendCoverage(output, authority.Coverage);
            output.Append('}');
        }

        private static void AppendAttackDefenseAuthority(StringBuilder output,
            ReplayAttackDefenseAuthorityRecordV4 authority,
            bool includeAttackCoverageResponsibilities)
        {
            output.Append("{\"planRevision\":").Append(authority.PlanRevision);
            output.Append(",\"sourceSequenceNumber\":").Append(authority.SourceSequenceNumber);
            output.Append(",\"phase\":").Append(Quote(authority.Phase));
            output.Append(",\"branch\":").Append(Quote(authority.Branch));
            output.Append(",\"setTarget\":"); Vector(output, authority.SetTarget);
            output.Append(",\"candidates\":[");
            for (var index = 0; index < authority.Candidates.Count; index++)
            {
                if (index > 0) output.Append(',');
                var candidate = authority.Candidates[index];
                output.Append("{\"candidateIdentity\":").Append(Quote(candidate.CandidateIdentity));
                output.Append(",\"actorPlayerId\":").Append(Quote(candidate.ActorPlayerId));
                output.Append(",\"actionClass\":").Append(Quote(candidate.ActionClass));
                output.Append(",\"target\":"); Vector(output, candidate.Target);
                output.Append(",\"expectedRallyValue\":"); Float(output, candidate.ExpectedRallyValue);
                output.Append(",\"legalSampleRatio\":"); Float(output, candidate.LegalSampleRatio);
                output.Append(",\"isQualifiedPowerRoute\":").Append(candidate.IsQualifiedPowerRoute ? "true" : "false");
                output.Append(",\"eliminationReason\":").Append(Quote(candidate.EliminationReason));
                output.Append(",\"envelopeIdentity\":").Append(Quote(candidate.EnvelopeIdentity));
                output.Append(",\"trajectoryArtifactIdentity\":").Append(Quote(candidate.TrajectoryArtifactIdentity));
                output.Append(",\"reorganizationExitIdentity\":").Append(Quote(candidate.ReorganizationExitIdentity));
                output.Append('}');
            }
            output.Append("],\"publicThreat\":[");
            for (var index = 0; index < authority.PublicThreat.Count; index++)
            {
                if (index > 0) output.Append(',');
                var threat = authority.PublicThreat[index];
                output.Append("{\"actionClass\":").Append(Quote(threat.ActionClass));
                output.Append(",\"zone\":").Append(Quote(threat.Zone));
                output.Append(",\"probability\":"); Float(output, threat.Probability);
                output.Append(",\"arrivalTime\":"); Float(output, threat.ArrivalTime);
                output.Append('}');
            }
            output.Append("],\"defenseResponsibilities\":[");
            for (var index = 0; index < authority.DefenseResponsibilities.Count; index++)
            {
                if (index > 0) output.Append(',');
                var responsibility = authority.DefenseResponsibilities[index];
                output.Append("{\"actorPlayerId\":").Append(Quote(responsibility.ActorPlayerId));
                output.Append(",\"kind\":").Append(Quote(responsibility.Kind));
                output.Append(",\"zone\":").Append(Quote(responsibility.Zone));
                output.Append(",\"branch\":").Append(Quote(responsibility.Branch));
                output.Append('}');
            }
            output.Append(']');
            if (includeAttackCoverageResponsibilities)
            {
                output.Append(",\"attackCoverageResponsibilities\":[");
                for (var index = 0;
                     index < authority.AttackCoverageResponsibilities.Count;
                     index++)
                {
                    if (index > 0) output.Append(',');
                    var responsibility =
                        authority.AttackCoverageResponsibilities[index];
                    output.Append("{\"actorPlayerId\":")
                        .Append(Quote(responsibility.ActorPlayerId));
                    output.Append(",\"branch\":")
                        .Append(Quote(responsibility.Branch));
                    output.Append('}');
                }
                output.Append(']');
            }
            output.Append(",\"selectedCandidateIdentity\":").Append(Quote(authority.SelectedCandidateIdentity));
            output.Append(",\"testedEnvelopeIdentity\":").Append(Quote(authority.TestedEnvelopeIdentity));
            output.Append(",\"executableEnvelopeIdentity\":").Append(Quote(authority.ExecutableEnvelopeIdentity));
            output.Append(",\"sampleEnvelopeIdentity\":").Append(Quote(authority.SampleEnvelopeIdentity));
            output.Append(",\"trajectoryArtifactIdentity\":").Append(Quote(authority.TrajectoryArtifactIdentity));
            output.Append(",\"recovery\":");
            if (authority.Recovery == null) output.Append("null");
            else
            {
                var recovery = authority.Recovery;
                output.Append("{\"candidateIdentity\":").Append(Quote(recovery.CandidateIdentity));
                output.Append(",\"blockerPlayerId\":").Append(Quote(recovery.BlockerPlayerId));
                output.Append(",\"reboundSide\":").Append(Quote(recovery.ReboundSide));
                output.Append(",\"recoveryPlayerId\":").Append(Quote(recovery.RecoveryPlayerId));
                output.Append(",\"reorganizationExitIdentity\":").Append(Quote(recovery.ReorganizationExitIdentity));
                output.Append(",\"reboundTrajectoryArtifactIdentity\":").Append(Quote(recovery.ReboundTrajectoryArtifactIdentity));
                output.Append(",\"reboundSampleIdentity\":").Append(Quote(recovery.ReboundSampleIdentity));
                output.Append(",\"blockContactIdentity\":").Append(Quote(recovery.BlockContactIdentity));
                output.Append(",\"remainingTouches\":").Append(recovery.RemainingTouches);
                output.Append('}');
            }
            output.Append(",\"coverage\":"); AppendCoverage(output, authority.Coverage);
            output.Append('}');
        }

        private static void AppendCoverage(
            StringBuilder output,
            ReplayCoverageDecisionRecordV4 coverage)
        {
            output.Append("{\"decision\":")
                .Append(Quote(coverage.Decision));
            output.Append(",\"score\":");
            Float(output, coverage.Score);
            output.Append(",\"reason\":")
                .Append(Quote(coverage.Reason));
            output.Append(",\"invalidationSet\":");
            Strings(output, coverage.InvalidationSet);
            output.Append(",\"expansionDepth\":")
                .Append(coverage.ExpansionDepth);
            output.Append(",\"activatedDeclaredBranch\":");
            AppendNullableString(
                output,
                coverage.ActivatedDeclaredBranch);
            output.Append('}');
        }

        private static void AppendNullableString(
            StringBuilder output,
            string value)
        {
            if (value == null)
            {
                output.Append("null");
            }
            else
            {
                output.Append(Quote(value));
            }
        }

        private static void AppendShadow(
            StringBuilder output,
            ReplayShadowRecordV4 shadow,
            bool legacyShadowCoverage)
        {
            output.Append("{\"revision\":").Append(shadow.Revision);
            output.Append(",\"sourceSequenceNumber\":")
                .Append(shadow.SourceSequenceNumber);
            output.Append(",\"artifactIdentity\":")
                .Append(Quote(shadow.ArtifactIdentity));
            output.Append(",\"home\":");
            AppendTeamPlan(output, shadow.Home);
            output.Append(",\"away\":");
            AppendTeamPlan(output, shadow.Away);
            output.Append(",\"coverage\":{\"decision\":")
                .Append(Quote(
                    legacyShadowCoverage && shadow.Coverage.Decision == "Terminal"
                        ? "Uncovered"
                        : shadow.Coverage.Decision))
                .Append(",\"score\":");
            Float(output, shadow.Coverage.Score);
            if (legacyShadowCoverage)
            {
                output.Append("}}");
                return;
            }
            output.Append(",\"reason\":")
                .Append(Quote(shadow.Coverage.Reason));
            output.Append(",\"invalidationSet\":");
            Strings(output, shadow.Coverage.InvalidationSet);
            output.Append(",\"expansionDepth\":")
                .Append(shadow.Coverage.ExpansionDepth);
            output.Append(",\"activatedDeclaredBranch\":");
            if (shadow.Coverage.ActivatedDeclaredBranch == null)
            {
                output.Append("null");
            }
            else
            {
                output.Append(Quote(shadow.Coverage.ActivatedDeclaredBranch));
            }
            output.Append("}}");
        }

        private static void AppendTeamPlan(
            StringBuilder output,
            ReplayTeamRallyPlanRecordV4 plan)
        {
            output.Append("{\"teamSide\":").Append(Quote(plan.TeamSide));
            output.Append(",\"primaryAssignments\":[");
            for (var index = 0; index < plan.PrimaryAssignments.Count; index++)
            {
                if (index > 0) output.Append(',');
                var assignment = plan.PrimaryAssignments[index];
                output.Append("{\"rank\":").Append(assignment.Rank);
                output.Append(",\"playerId\":").Append(Quote(assignment.PlayerId));
                output.Append(",\"task\":").Append(Quote(assignment.Task));
                output.Append(",\"condition\":").Append(Quote(assignment.Condition));
                output.Append(",\"spatialClaim\":")
                    .Append(Quote(assignment.SpatialClaim));
                output.Append(",\"declaredBranch\":")
                    .Append(Quote(assignment.DeclaredBranch));
                output.Append(",\"value\":");
                Float(output, assignment.Value);
                output.Append('}');
            }

            output.Append("]}");
        }

        private static void AppendEnvelope(
            StringBuilder output,
            ReplayExecutionEnvelopeRecordV4 value)
        {
            output.Append("{\"version\":").Append(value.Version);
            output.Append(",\"identity\":").Append(Quote(value.Identity));
            output.Append(",\"derivedAttributesFingerprint\":")
                .Append(Quote(value.DerivedAttributesFingerprint));
            output.Append(",\"policyIdentity\":")
                .Append(Quote(value.PolicyIdentity));
            output.Append(",\"sourceIntentIdentity\":")
                .Append(Quote(value.SourceIntentIdentity));
            output.Append(",\"candidateCategory\":")
                .Append(Quote(value.CandidateCategory));
            output.Append(",\"baselineTarget\":");
            Vector(output, value.BaselineTarget);
            output.Append(",\"baselineVelocity\":");
            Vector(output, value.BaselineVelocity);
            output.Append(",\"maximumVelocity\":");
            Vector(output, value.MaximumVelocity);
            output.Append(",\"targetError\":");
            Error(output, value.TargetError);
            output.Append(",\"velocityError\":");
            Error(output, value.VelocityError);
            output.Append(",\"requestedEffort\":");
            Float(output, value.RequestedEffort);
            output.Append(",\"maximumEffort\":");
            Float(output, value.MaximumEffort);
            output.Append(",\"samplingKey\":").Append(Quote(value.SamplingKey));
            output.Append(",\"policyVersion\":").Append(value.PolicyVersion);
            output.Append(",\"sampleCount\":").Append(value.SampleCount);
            output.Append(",\"candidateCategoryOrder\":");
            Strings(output, value.CandidateCategoryOrder);
            output.Append(",\"degradationLadder\":");
            Strings(output, value.DegradationLadder);
            output.Append(",\"maximumExpansionCount\":")
                .Append(value.MaximumExpansionCount);
            output.Append(",\"allowedExpansionCount\":")
                .Append(value.AllowedExpansionCount);
            output.Append(",\"currentExpansionCount\":")
                .Append(value.CurrentExpansionCount);
            output.Append(",\"perStepExpansionFactor\":");
            Float(output, value.PerStepExpansionFactor);
            output.Append('}');
        }

        private static void AppendTrajectory(
            StringBuilder output,
            ReplayTrajectoryArtifactRecordV4 value)
        {
            output.Append("{\"artifactIdentity\":")
                .Append(Quote(value.ArtifactIdentity));
            output.Append(",\"predictorSource\":")
                .Append(Quote(value.PredictorSource));
            output.Append(",\"predictorVersion\":")
                .Append(value.PredictorVersion);
            output.Append(",\"predictorConfigurationHash\":")
                .Append(Quote(value.PredictorConfigurationHash));
            var key = value.CacheKey;
            output.Append(",\"cacheKey\":{\"identity\":")
                .Append(Quote(key.Identity));
            output.Append(",\"ballStateVersion\":")
                .Append(key.BallStateVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"ballStateFingerprint\":")
                .Append(Quote(key.BallStateFingerprint));
            output.Append(",\"physicsConfigurationHash\":")
                .Append(Quote(key.PhysicsConfigurationHash));
            output.Append(",\"samplingKey\":").Append(Quote(key.SamplingKey));
            output.Append(",\"predictorVersion\":").Append(key.PredictorVersion);
            output.Append(",\"predictorConfigurationHash\":")
                .Append(Quote(key.PredictorConfigurationHash));
            output.Append(",\"envelopeIdentity\":")
                .Append(Quote(key.EnvelopeIdentity));
            output.Append(",\"degradationStep\":")
                .Append(Quote(key.DegradationStep));
            output.Append("}}");
        }

        private static void AppendClassification(
            StringBuilder output,
            ReplaySampleClassificationRecordV4 value)
        {
            output.Append("{\"kind\":").Append(Quote(value.Kind));
            output.Append(",\"testedEnvelopeIdentity\":")
                .Append(Quote(value.TestedEnvelopeIdentity));
            output.Append(",\"expandedEnvelopeIdentity\":")
                .Append(Quote(value.ExpandedEnvelopeIdentity));
            var sample = value.ActualSample;
            output.Append(",\"actualSample\":{\"envelopeIdentity\":")
                .Append(Quote(sample.EnvelopeIdentity));
            output.Append(",\"samplingKey\":")
                .Append(Quote(sample.SamplingKey));
            output.Append(",\"candidateCategory\":")
                .Append(Quote(sample.CandidateCategory));
            output.Append(",\"target\":");
            Vector(output, sample.Target);
            output.Append(",\"velocity\":");
            Vector(output, sample.Velocity);
            output.Append(",\"effort\":");
            Float(output, sample.Effort);
            output.Append("},\"offendingDimensions\":");
            Strings(output, value.OffendingDimensions);
            output.Append('}');
        }

        private static void AppendGeometry(
            StringBuilder output,
            ReplayObservedP6GeometryRecordV4 value)
        {
            output.Append("{\"actorPlayerId\":")
                .Append(Quote(value.ActorPlayerId));
            output.Append(",\"teamSide\":").Append(Quote(value.TeamSide));
            output.Append(",\"takeoffPoint\":");
            Vector(output, value.TakeoffPoint);
            output.Append(",\"contactPoint\":");
            Vector(output, value.ContactPoint);
            output.Append(",\"attackLineDistanceFromCenter\":");
            Float(output, value.AttackLineDistanceFromCenter);
            output.Append(",\"netHeight\":");
            Float(output, value.NetHeight);
            output.Append(",\"isTakeoffInFrontZone\":")
                .Append(value.IsTakeoffInFrontZone ? "true" : "false");
            output.Append(",\"isContactAboveNet\":")
                .Append(value.IsContactAboveNet ? "true" : "false")
                .Append('}');
        }

        private static void Error(
            StringBuilder output,
            ReplayBoundedErrorRecordV4 value)
        {
            output.Append("{\"distributionKind\":")
                .Append(Quote(value.DistributionKind));
            output.Append(",\"minimum\":");
            Vector(output, value.Minimum);
            output.Append(",\"maximum\":");
            Vector(output, value.Maximum);
            output.Append('}');
        }

        private static void Vector(
            StringBuilder output,
            ReplayVector3RecordV4 value)
        {
            output.Append("{\"x\":");
            Float(output, value.X);
            output.Append(",\"y\":");
            Float(output, value.Y);
            output.Append(",\"z\":");
            Float(output, value.Z);
            output.Append('}');
        }

        private static void Strings(
            StringBuilder output,
            IReadOnlyList<string> values)
        {
            output.Append('[');
            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0) output.Append(',');
                output.Append(Quote(values[index]));
            }

            output.Append(']');
        }

        private static void Float(StringBuilder output, float value)
        {
            output.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string Quote(string value)
        {
            var output = new StringBuilder(value.Length + 2);
            output.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 32)
                        {
                            output.Append("\\u")
                                .Append(((int)character).ToString(
                                    "x4",
                                    CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            output.Append(character);
                        }

                        break;
                }
            }

            return output.Append('"').ToString();
        }
    }
}
