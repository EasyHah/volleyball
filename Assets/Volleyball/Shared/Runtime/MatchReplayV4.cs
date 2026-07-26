using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
            CandidateCategory = ReplayContractGuardV4.EventKind(
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
            if (TestedEnvelope.CandidateCategory != EventKind ||
                ExecutableEnvelope.CandidateCategory != EventKind ||
                Classification.ActualSample.CandidateCategory != EventKind)
            {
                throw new ContractValidationException(
                    "Event kind must match both envelopes and the actual-sample category.");
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

    public sealed class MatchReplayV4
    {
        private readonly MatchReplayEventV4[] _events;

        private MatchReplayV4(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
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
                 !string.Equals(
                     suppliedReplayHash,
                     CanonicalMatchReplayJsonV4.ComputeLegacyShadowCoverageHash(this),
                     StringComparison.Ordinal)))
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
        public IReadOnlyList<MatchReplayEventV4> Events =>
            new ReadOnlyCollection<MatchReplayEventV4>(_events);
        public string ReplayHash { get; }

        public static MatchReplayV4 Create(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events)
        {
            return new MatchReplayV4(replayId, context, events, 0, null, false);
        }

        public static MatchReplayV4 Create(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
            int sourceSequenceAnchor)
        {
            return new MatchReplayV4(
                replayId, context, events, sourceSequenceAnchor, null, false);
        }

        internal static MatchReplayV4 Restore(
            string replayId,
            MatchContextV4 context,
            IReadOnlyList<MatchReplayEventV4> events,
            int sourceSequenceAnchor,
            string replayHash,
            bool allowLegacyShadowCoverageHash)
        {
            if (replayHash != null)
            {
                ReplayContractGuardV4.Hash(replayHash, nameof(replayHash));
            }

            return new MatchReplayV4(
                replayId,
                context,
                events,
                sourceSequenceAnchor,
                replayHash,
                allowLegacyShadowCoverageHash);
        }

        internal void Validate()
        {
            Context.Validate();
            CopySortAndValidateEvents(_events);
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
            return ComputeHash(Payload(replay, true));
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
            if (root.Properties.Count != (hasSourceSequenceAnchor ? 6 : 5))
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
                events[index] = ParseEvent(eventValue);
            }

            if (hasLegacyShadowCoverage && hasCurrentShadowCoverage)
            {
                throw new ContractValidationException(
                    "Mixed legacy and current shadow coverage is not supported.");
            }

            return MatchReplayV4.Restore(
                StrictJsonV4.RequiredString(root, "replayId"),
                context,
                events,
                hasSourceSequenceAnchor
                    ? StrictJsonV4.RequiredInt(root, "sourceSequenceAnchor")
                    : 0,
                StrictJsonV4.RequiredString(root, "replayHash"),
                hasLegacyShadowCoverage);
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
                    StrictJsonV4.OptionalNullableObject(value, "shadow")));
        }

        private static void RequireEventProperties(StrictJsonObjectV4 value)
        {
            var hasShadow = value.Properties.ContainsKey("shadow");
            if (value.Properties.Count != (hasShadow ? 14 : 13))
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
            bool legacyShadowCoverage = false)
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
                AppendEvent(output, replay.Events[index], legacyShadowCoverage);
            }

            output.Append("]}");
            return output.ToString();
        }

        private static void AppendEvent(
            StringBuilder output,
            MatchReplayEventV4 replayEvent,
            bool legacyShadowCoverage)
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

            output.Append('}');
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
