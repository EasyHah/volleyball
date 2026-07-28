using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    /// <summary>
    /// Formal replay recorder. It records only native MatchReplayV4 evidence;
    /// prototype/legacy replay contracts are intentionally unsupported.
    /// </summary>
    public sealed class MatchReplayRecorder : MonoBehaviour
    {
        private FormalSixVsSixRallyDirector _director;
        private List<PrototypePlayerAgent> _players;
        private List<MatchReplayEventV4> _events;
        private ReplayShadowPlanPendingStore<RallyPlanV3> _pendingShadowPlans;
        private int _sourceSequenceAnchor;
        private bool _capturing;

        public bool IsComplete { get; private set; }
        public string CaptureFailureReason { get; private set; }

        public static MatchReplayRecorder Attach(
            FormalSixVsSixRallyDirector director,
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> players)
        {
            if (director == null)
            {
                throw new ArgumentNullException(nameof(director));
            }

            if (director.MatchContext == null)
            {
                throw new InvalidOperationException(
                    "The formal V4 director must be initialized before replay recording.");
            }

            if (director.V3RulesMode != V3RulesMode.Authority)
            {
                throw new InvalidOperationException(
                    "Formal V4 replay recording requires V3 rules authority.");
            }

            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }

            var recorder = director.gameObject.AddComponent<MatchReplayRecorder>();
            recorder.Initialize(director, players);
            return recorder;
        }

        public void StartCapture()
        {
            if (_capturing)
            {
                throw new InvalidOperationException(
                    "Replay capture is already running.");
            }

            _events = new List<MatchReplayEventV4>();
            _pendingShadowPlans = new ReplayShadowPlanPendingStore<RallyPlanV3>(
                _director.V3RuleTransitions);
            _sourceSequenceAnchor = CheckedInt(
                _director.V3RuleTransitions,
                nameof(_director.V3RuleTransitions));
            _capturing = true;
            IsComplete = false;
            CaptureFailureReason = null;
        }

        public MatchReplayV4 Complete()
        {
            if (!string.IsNullOrEmpty(CaptureFailureReason))
            {
                throw new InvalidOperationException(
                    "Replay capture is unavailable: " + CaptureFailureReason);
            }

            if (!IsComplete)
            {
                throw new InvalidOperationException(
                    "Replay capture has not completed.");
            }

            return MatchReplayV4.Create(
                ReplayId(_director.MatchContext),
                _director.MatchContext,
                _events,
                _sourceSequenceAnchor);
        }

        private void Initialize(
            FormalSixVsSixRallyDirector director,
            IEnumerable<PrototypePlayerAgent> players)
        {
            _director = director;
            _players = new List<PrototypePlayerAgent>(
                players ?? throw new ArgumentNullException(nameof(players)));
            if (_players.Count != 12)
            {
                throw new ArgumentException(
                    "Formal V4 replay capture requires twelve players.",
                    nameof(players));
            }

            _players.Sort(
                (left, right) => string.CompareOrdinal(
                    left.StableId.Value,
                    right.StableId.Value));
            _director.ReplayContactAccepted += RecordContact;
            _director.ReplayShadowPlanRecorded += RecordShadowPlan;
            _director.ReplayRallyResolved += RecordResolution;
        }

        private void OnDestroy()
        {
            if (_director == null)
            {
                return;
            }

            _director.ReplayContactAccepted -= RecordContact;
            _director.ReplayShadowPlanRecorded -= RecordShadowPlan;
            _director.ReplayRallyResolved -= RecordResolution;
        }

        private void RecordShadowPlan(RallyPlanV3 plan)
        {
            if (!_capturing || IsComplete || plan == null)
            {
                return;
            }

            if (!_pendingShadowPlans.TryAdd(plan.SourceSequence, plan))
            {
                Debug.LogWarning("Ignoring duplicate or stale shadow source sequence.");
            }
        }

        private void RecordContact(ReplayContactEvent replayEvent)
        {
            if (!_capturing || IsComplete)
            {
                return;
            }

            var sequenceNumber = _events.Count;
            if (_director.GateHAuthorityEnabled &&
                (replayEvent.Action == TechniqueAction.Receive ||
                 replayEvent.Action == TechniqueAction.Set) &&
                replayEvent.OrganizationAuthority == null)
            {
                InvalidateCapture(
                    "Gate H Receive/Set replay events require event-owned organization authority.");
                return;
            }
            if (_director.GateIAuthorityEnabled && MissingGateIAuthorityEvidence(replayEvent))
            {
                InvalidateCapture("Formal Gate I replay events require event-owned authority evidence.");
                return;
            }
            if (_director.GateJPerceptionEnabled &&
                MissingGateJPerceptionEvidence(replayEvent))
            {
                InvalidateCapture(
                    "Formal Gate J authority events require event-owned perception evidence.");
                return;
            }

            // V3 transitions are one-based; native replay contact indexes are zero-based.
            ReplayShadowRecordV4 shadow = null;
            if (_pendingShadowPlans.TryTakeForReplaySequence(
                    sequenceNumber,
                    out var pendingPlan))
            {
                try
                {
                    shadow = ToReplayShadow(pendingPlan);
                }
                catch (Exception exception)
                {
                    InvalidateCapture(
                        "Shadow plan conversion failed: " +
                        exception.GetType().FullName);
                    return;
                }
            }
            _events.Add(CreateContactRecordV4(
                sequenceNumber,
                replayEvent,
                _director.HomeScore,
                _director.AwayScore,
                shadow));
        }

        public static MatchReplayEventV4 CreateContactRecordV4(
            int sequenceNumber,
            ReplayContactEvent replayEvent,
            int homeScore,
            int awayScore)
        {
            return CreateContactRecordV4(
                sequenceNumber, replayEvent, homeScore, awayScore, null);
        }

        private static MatchReplayEventV4 CreateContactRecordV4(
            int sequenceNumber,
            ReplayContactEvent replayEvent,
            int homeScore,
            int awayScore,
            ReplayShadowRecordV4 shadow)
        {
            if (replayEvent == null)
            {
                throw new ArgumentNullException(nameof(replayEvent));
            }

            var actorId = replayEvent.PlayerId ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires an actor.");
            var classification =
                replayEvent.ExecutionClassification ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires the actor's actual sample classification.");
            var testedEnvelope = classification.TestedEnvelope;
            var executableEnvelope = classification.ExecutableEnvelope ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires the actor's executable envelope.");
            var trajectory = replayEvent.TrajectoryArtifact ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires its event-owned trajectory artifact.");
            var ruleTransition = replayEvent.RuleTransition ??
                throw new InvalidOperationException(
                    "Formal V4 contact replay requires its V3 rule decision.");
            if (replayEvent.GateISetIntentAuthority != null && replayEvent.AttackDefenseAuthority != null)
                throw new InvalidOperationException("A replay contact cannot map both Gate I receipt kinds.");
            var gateIAuthority = replayEvent.GateISetIntentAuthority != null
                ? ToReplayGateISetIntentAuthority(replayEvent.GateISetIntentAuthority, replayEvent, classification, trajectory)
                : replayEvent.AttackDefenseAuthority != null
                    ? ToReplayAttackDefenseAuthority(replayEvent.AttackDefenseAuthority, replayEvent, classification, trajectory)
                    : null;
            var perception = replayEvent.OrganizationAuthority?.Perception ??
                             replayEvent.AttackDefenseAuthority?.Perception ??
                             replayEvent.AttackDefenseAuthority?.Evidence
                                 ?.Perception;

            return new MatchReplayEventV4(
                sequenceNumber,
                replayEvent.Action.ToString(),
                actorId.Value,
                replayEvent.SimulationTimeSeconds,
                homeScore,
                awayScore,
                ToReplayEnvelope(testedEnvelope),
                ToReplayEnvelope(executableEnvelope),
                ToReplayTrajectory(trajectory),
                ToReplayConsumptions(
                    actorId.Value,
                    testedEnvelope.DerivedAttributesFingerprint,
                    testedEnvelope.AbilityConsumptions),
                ToReplayClassification(classification),
                ToReplayGeometry(replayEvent.ObservedAttackGeometry),
                new ReplayRuleDecisionRecordV4(
                    RulesVersions.FullRallyV3,
                    ruleTransition.Accepted,
                    ruleTransition.RejectionReason.ToString()),
                shadow,
                ToReplayOrganizationAuthority(
                    replayEvent.OrganizationAuthority,
                    replayEvent,
                    classification,
                    trajectory),
                gateIAuthority,
                ToReplayPerception(perception));
        }

        private static bool MissingGateIAuthorityEvidence(ReplayContactEvent replayEvent)
        {
            if (replayEvent.GateISetIntentAuthority != null || replayEvent.AttackDefenseAuthority != null)
                return false;
            return replayEvent.Action == TechniqueAction.Set ||
                   replayEvent.Action == TechniqueAction.Attack ||
                   replayEvent.Action == TechniqueAction.Block ||
                   (replayEvent.Action == TechniqueAction.Receive && replayEvent.OrganizationAuthority == null);
        }

        private static bool MissingGateJPerceptionEvidence(
            ReplayContactEvent replayEvent)
        {
            if (replayEvent.Action != TechniqueAction.Receive &&
                replayEvent.Action != TechniqueAction.Set &&
                replayEvent.Action != TechniqueAction.Attack &&
                replayEvent.Action != TechniqueAction.Block)
                return false;
            var perception = replayEvent.OrganizationAuthority?.Perception ??
                             replayEvent.AttackDefenseAuthority?.Perception ??
                             replayEvent.AttackDefenseAuthority?.Evidence
                                 ?.Perception;
            return perception == null;
        }

        private static ReplayPerceptionAuthorityRecordV4 ToReplayPerception(
            PerceptionReceiptV3 receipt)
        {
            if (receipt == null) return null;
            var observed = receipt.ObservedBall ??
                throw new InvalidOperationException(
                    "Gate J replay requires the event-owned ball observation.");
            return new ReplayPerceptionAuthorityRecordV4(
                receipt.ConfigurationIdentity,
                receipt.ViewIdentity,
                receipt.ObservingSide.ToString(),
                receipt.AuthoritativeArtifactIdentity,
                observed.ObservedAtSimulationTime,
                receipt.RecognitionDelaySeconds,
                observed.UncertaintyKey,
                observed.Uncertainty,
                observed.Confidence,
                receipt.View.Threats.Select(value =>
                    new ReplayPerceivedThreatRecordV4(
                        value.ThreatIdentity, value.Zone, value.Confidence,
                        value.ArrivalTime)).ToArray(),
                receipt.SupportDecision.SelectedPlayer.Value,
                receipt.SupportDecision.SupportZone,
                receipt.SupportDecision.IsConservativeFallback,
                CheckedInt(receipt.Revision, nameof(receipt.Revision)),
                CheckedInt(receipt.SourceSequence,
                    nameof(receipt.SourceSequence)));
        }

        private static ReplayAttackDefenseAuthorityRecordV4 ToReplayGateISetIntentAuthority(
            GateISetIntentReceiptV3 receipt, ReplayContactEvent replayEvent,
            ExecutionSampleClassificationV4 classification, BallTrajectoryPredictionArtifactV4 trajectory)
        {
            var intent = receipt?.Intent;
            if (intent == null || replayEvent.Action != TechniqueAction.Set || replayEvent.PlayerId == null ||
                !replayEvent.PlayerId.Value.Equals(intent.Organizer) ||
                intent.ExecutionClassification.ExecutableEnvelope.Identity != classification.ExecutableEnvelope.Identity ||
                intent.TrajectoryArtifact.ArtifactIdentity != trajectory.ArtifactIdentity)
                throw new InvalidOperationException("Gate I SetIntent receipt does not match the accepted Set evidence.");
            return new ReplayAttackDefenseAuthorityRecordV4(
                CheckedInt(receipt.PlanRevision, nameof(receipt.PlanRevision)), CheckedInt(receipt.SourceSequence, nameof(receipt.SourceSequence)),
                "SetIntentPlanned", "Primary", Vector(intent.Target),
                Array.Empty<ReplayAttackDefenseCandidateRecordV4>(), Array.Empty<ReplayPublicAttackThreatRecordV4>(),
                Array.Empty<ReplayDefenseResponsibilityRecordV4>(), string.Empty,
                classification.TestedEnvelope.Identity, classification.ExecutableEnvelope.Identity,
                classification.Sample.EnvelopeIdentity, trajectory.ArtifactIdentity, null,
                new ReplayCoverageDecisionRecordV4("Covered", 0f, "RallyOpen", Array.Empty<string>(), 0, "Primary"));
        }

        private static ReplayAttackDefenseAuthorityRecordV4 ToReplayAttackDefenseAuthority(
            AttackDefenseAuthorityReceipt receipt, ReplayContactEvent replayEvent,
            ExecutionSampleClassificationV4 classification, BallTrajectoryPredictionArtifactV4 trajectory)
        {
            if (receipt == null || receipt.ExecutionClassification == null || receipt.TrajectoryArtifact == null ||
                receipt.ExecutionClassification.ExecutableEnvelope.Identity != classification.ExecutableEnvelope.Identity ||
                receipt.TrajectoryArtifact.ArtifactIdentity != trajectory.ArtifactIdentity || replayEvent.PlayerId == null ||
                !replayEvent.PlayerId.Value.Equals(receipt.Actor) || !IsReplayActionFor(receipt.Kind, replayEvent.Action))
                throw new InvalidOperationException("Gate I contact receipt does not match the accepted contact evidence.");
            var evidence = receipt.Evidence;
            var plan = evidence.Plan ?? throw new InvalidOperationException("Gate I contact replay requires immutable plan evidence.");
            var selected = plan.SelectedAction;
            if (receipt.Kind == AttackDefenseCommandKind.AttackContact &&
                selected == null)
                throw new InvalidOperationException("Gate I attack replay requires the selected candidate.");
            return new ReplayAttackDefenseAuthorityRecordV4(
                CheckedInt(receipt.PlanRevision, nameof(receipt.PlanRevision)), CheckedInt(receipt.SourceSequence, nameof(receipt.SourceSequence)),
                receipt.Phase.ToString(), ToReplayBranch(receipt.Branch) ?? throw new InvalidOperationException("Gate I receipt branch is required."),
                Vector(plan.SetIntent.Target), ToReplayCandidates(plan.AttackCandidates), ToReplayThreat(plan.PublicThreat),
                ToReplayDefenseResponsibilities(plan.Defense), selected?.CandidateIdentity ?? string.Empty,
                classification.TestedEnvelope.Identity, classification.ExecutableEnvelope.Identity,
                classification.Sample.EnvelopeIdentity, trajectory.ArtifactIdentity,
                ToReplayRecovery(plan, selected, receipt), ToReplayCoverage(evidence.CoverageDecision));
        }

        private static bool IsReplayActionFor(AttackDefenseCommandKind kind, TechniqueAction action) =>
            (kind == AttackDefenseCommandKind.AttackContact && action == TechniqueAction.Attack) ||
            (kind == AttackDefenseCommandKind.BlockContact && action == TechniqueAction.Block) ||
            ((kind == AttackDefenseCommandKind.FloorDefense || kind == AttackDefenseCommandKind.AttackCover || kind == AttackDefenseCommandKind.Reorganization) && action == TechniqueAction.Receive);

        private static ReplayAttackDefenseCandidateRecordV4[] ToReplayCandidates(IReadOnlyList<AttackCandidateV3> candidates) => candidates.Select(candidate =>
            new ReplayAttackDefenseCandidateRecordV4(candidate.CandidateIdentity, candidate.Actor.Value, candidate.ActionClass.ToString(),
                Vector(candidate.Target), candidate.ExpectedRallyValue, candidate.LegalSampleRatio, candidate.IsQualifiedPowerRoute,
                candidate.EliminationReason, candidate.EnvelopeIdentity, candidate.TrajectoryArtifactIdentity, candidate.ReorganizationExitIdentity)).ToArray();

        private static ReplayPublicAttackThreatRecordV4[] ToReplayThreat(PublicAttackThreatV3 threat) => threat.Entries.Select(entry =>
            new ReplayPublicAttackThreatRecordV4(entry.ActionClass.ToString(), entry.Zone, entry.Probability, entry.ArrivalTime)).ToArray();

        private static ReplayDefenseResponsibilityRecordV4[] ToReplayDefenseResponsibilities(JointDefensePlanV3 defense) => defense.Responsibilities.Select(responsibility =>
            new ReplayDefenseResponsibilityRecordV4(responsibility.Actor.Value, responsibility.Kind.ToString(), responsibility.Zone, ToReplayBranch(responsibility.Branch))).ToArray();

        private static ReplayToolRecoveryRecordV4 ToReplayRecovery(AttackDefensePlanV3 plan, AttackCandidateV3 selected,
            AttackDefenseAuthorityReceipt receipt = null)
        {
            if (selected == null || selected.ActionClass != AttackActionClassV3.BlockToolRecovery) return null;
            var evidence = selected.ToolRecoveryEvidence ?? throw new InvalidOperationException(
                "Gate I tool recovery replay requires event-owned rebound evidence.");
            if (evidence.CandidateIdentity != selected.CandidateIdentity ||
                evidence.EnvelopeIdentity != selected.EnvelopeIdentity ||
                evidence.OutboundTrajectoryArtifactIdentity != selected.TrajectoryArtifactIdentity ||
                evidence.ReorganizationExitIdentity != selected.ReorganizationExitIdentity ||
                evidence.ReboundTrajectoryArtifactIdentity == selected.TrajectoryArtifactIdentity ||
                evidence.RemainingTouches <= 0)
                throw new InvalidOperationException("Gate I tool recovery evidence does not exactly bind the selected candidate.");
            var actual = receipt?.ToolRecoveryActualObservation;
            if (receipt?.Kind == AttackDefenseCommandKind.BlockContact && actual == null)
                throw new InvalidOperationException("Accepted Gate I tool Block replay requires event-owned actual rebound observation.");
            return new ReplayToolRecoveryRecordV4(
                evidence.CandidateIdentity,
                evidence.Blocker.Value,
                actual?.ReboundSide.ToString() ?? evidence.ReboundSide.ToString(),
                evidence.RecoveryActor.Value,
                evidence.ReorganizationExitIdentity,
                actual?.ReboundTrajectoryArtifactIdentity ?? evidence.ReboundTrajectoryArtifactIdentity,
                actual?.ReboundSampleIdentity ?? evidence.ReboundSampleIdentity,
                actual?.BlockContactIdentity ?? evidence.BlockContactIdentity,
                actual?.RemainingTouches ?? evidence.RemainingTouches);
        }

        private static ReplayCoverageDecisionRecordV4 ToReplayCoverage(PlanCoverageDecision coverage)
        {
            if (coverage == null) throw new InvalidOperationException("Gate I replay requires event-owned coverage evidence.");
            return new ReplayCoverageDecisionRecordV4(ToReplayCoverageKind(coverage.Kind),
                coverage.Kind == PlanCoverageDecisionKind.CoveredActivateBranch ? 1f : 0f,
                coverage.Reason.ToString(), coverage.InvalidationSet, coverage.ExpansionDepth, ToReplayBranch(coverage.ActivatedDeclaredBranch));
        }

        private static ReplayOrganizationAuthorityRecordV4
            ToReplayOrganizationAuthority(
                ReceiveOrganizationAuthorityReceipt receipt,
                ReplayContactEvent replayEvent,
                ExecutionSampleClassificationV4 classification,
                BallTrajectoryPredictionArtifactV4 trajectory)
        {
            if (receipt == null)
            {
                return null;
            }

            var evidence = receipt.Evidence;
            var plan = evidence.Plan;
            var assessmentPoint =
                evidence.ActualFirstPassLanding ??
                plan.OrganizationTarget;
            var runtimeTeam = plan.Side == TeamSide.Home
                ? Volleyball.Domain.Prototype.TeamId.Blue
                : Volleyball.Domain.Prototype.TeamId.Orange;
            var assessment = SetterOrganizationZone.AssessWorldTarget(
                runtimeTeam,
                assessmentPoint);
            var zoneGrade =
                assessment.LateralGrade == SetterOrganizationZoneGrade.Poor ||
                assessment.DepthGrade == SetterOrganizationZoneGrade.Poor
                    ? SetterOrganizationZoneGrade.Poor
                    : assessment.LateralGrade ==
                      SetterOrganizationZoneGrade.Best &&
                      assessment.DepthGrade ==
                      SetterOrganizationZoneGrade.Best
                        ? SetterOrganizationZoneGrade.Best
                        : SetterOrganizationZoneGrade.Secondary;
            var setterStatus = evidence.SetterEvidence.WasPreviousTouch
                ? "PreviousTouch"
                : !evidence.SetterEvidence.IsAvailable
                    ? "Unavailable"
                    : !evidence.SetterEvidence.IsLegal
                        ? "Illegal"
                        : evidence.SetterEvidence.IsReachable
                            ? "Reachable"
                            : "Unreachable";
            var coverage = evidence.CoverageDecision;
            return new ReplayOrganizationAuthorityRecordV4(
                CheckedInt(receipt.PlanRevision, nameof(receipt.PlanRevision)),
                CheckedInt(
                    receipt.SourceSequence,
                    nameof(receipt.SourceSequence)),
                replayEvent.Action == TechniqueAction.Receive
                    ? "Receive"
                    : "Organize",
                Vector(plan.OrganizationTarget),
                evidence.ActualFirstPassLanding.HasValue
                    ? Vector(
                        evidence.ActualFirstPassLanding.Value)
                    : null,
                zoneGrade.ToString(),
                plan.RegisteredSetter.Value,
                setterStatus,
                evidence.SetterEvidence.MovementMeters,
                evidence.SetterEvidence.ReactionDelaySeconds,
                evidence.SetterEvidence.ReachMarginMeters,
                replayEvent.Action == TechniqueAction.Receive
                    ? plan.RegisteredSetter.Value
                    : receipt.Actor.Value,
                evidence.FallbackReason.ToString(),
                receipt.Branch.ToString(),
                classification.TestedEnvelope.Identity,
                classification.ExecutableEnvelope.Identity,
                classification.Sample.EnvelopeIdentity,
                trajectory.ArtifactIdentity,
                new ReplayCoverageDecisionRecordV4(
                    ToReplayCoverageKind(coverage.Kind),
                    0f,
                    coverage.Reason.ToString(),
                    coverage.InvalidationSet,
                    coverage.ExpansionDepth,
                    coverage.ActivatedDeclaredBranch?.ToString()));
        }

        private static string ToReplayCoverageKind(
            PlanCoverageDecisionKind kind)
        {
            return kind switch
            {
                PlanCoverageDecisionKind.CoveredActivateBranch => "Covered",
                PlanCoverageDecisionKind.LocalRevision => "Local",
                PlanCoverageDecisionKind.ScopedReplan => "Scoped",
                PlanCoverageDecisionKind.GlobalReplan => "Global",
                PlanCoverageDecisionKind.TerminalNoPlan => "Terminal",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        private void RecordResolution(ReplayRallyResolvedEvent replayEvent)
        {
            if (!_capturing || IsComplete)
            {
                return;
            }

            if (_events.Count == 0)
            {
                InvalidateCapture(
                    "A formal V4 replay segment requires at least one contact event.");
                return;
            }

            var unresolvedPlans = _pendingShadowPlans.Clear();
            if (unresolvedPlans > 0)
            {
                InvalidateCapture(
                    "Unmatched shadow revisions at rally resolution: " +
                    unresolvedPlans);
                return;
            }

            _capturing = false;
            IsComplete = true;
        }

        private void InvalidateCapture(string reason)
        {
            _pendingShadowPlans?.Clear();
            _capturing = false;
            IsComplete = false;
            CaptureFailureReason = reason;
            Debug.LogWarning("Replay capture invalidated: " + reason);
        }

        private static ReplayShadowRecordV4 ToReplayShadow(
            RallyPlanV3 plan)
        {
            return new ReplayShadowRecordV4(
                CheckedInt(plan.Revision, nameof(plan.Revision)),
                CheckedInt(plan.SourceSequence, nameof(plan.SourceSequence)),
                plan.ArtifactIdentity,
                ToReplayTeamPlan(plan.HomePlan),
                ToReplayTeamPlan(plan.AwayPlan),
                new ReplayCoverageDecisionRecordV4(
                    ToReplayCoverageDecision(plan.CoverageDecision.Kind),
                    plan.CoverageDecision.Kind ==
                        PlanCoverageDecisionKind.CoveredActivateBranch
                        ? 1f
                        : 0f,
                    plan.CoverageDecision.Reason.ToString(),
                    plan.CoverageDecision.InvalidationSet,
                    plan.CoverageDecision.ExpansionDepth,
                    ToReplayBranch(plan.CoverageDecision.ActivatedDeclaredBranch)));
        }

        private static string ToReplayCoverageDecision(PlanCoverageDecisionKind kind)
        {
            switch (kind)
            {
                case PlanCoverageDecisionKind.CoveredActivateBranch: return "Covered";
                case PlanCoverageDecisionKind.LocalRevision: return "Local";
                case PlanCoverageDecisionKind.ScopedReplan: return "Scoped";
                case PlanCoverageDecisionKind.GlobalReplan: return "Global";
                default: return "Terminal";
            }
        }

        private static string ToReplayBranch(RallyPlanBranchV3? branch)
        {
            if (!branch.HasValue)
            {
                return null;
            }

            return branch.Value == RallyPlanBranchV3.Primary
                ? "Primary"
                : "Contingency";
        }

        private static ReplayTeamRallyPlanRecordV4 ToReplayTeamPlan(
            TeamRallyPlanV3 plan)
        {
            var assignments = new ReplayShadowAssignmentRecordV4[plan.Assignments.Count];
            for (var index = 0; index < assignments.Length; index++)
            {
                var assignment = plan.Assignments[index];
                assignments[index] = new ReplayShadowAssignmentRecordV4(
                    assignment.Rank,
                    assignment.PlayerId.Value,
                    ToReplayTask(assignment.Task),
                    ToReplayCondition(assignment.Condition),
                    ToReplayClaim(assignment.SpatialClaim),
                    assignment.Branch == RallyPlanBranchV3.Primary ? "Primary" : "Fallback",
                    assignment.Value);
            }

            return new ReplayTeamRallyPlanRecordV4(plan.Side.ToString(), assignments);
        }

        private static string ToReplayTask(RallyPlanTaskV3 task)
        {
            switch (task)
            {
                case RallyPlanTaskV3.Receive: return "Receive";
                case RallyPlanTaskV3.Set: return "Set";
                case RallyPlanTaskV3.Attack: return "Attack";
                case RallyPlanTaskV3.Block: return "Block";
                default: return "Cover";
            }
        }

        private static string ToReplayCondition(RallyPlanConditionV3 condition)
        {
            switch (condition)
            {
                case RallyPlanConditionV3.BallOnOurSide: return "IfBallIncoming";
                case RallyPlanConditionV3.BallOnOpponentSide: return "IfAttackIncoming";
                case RallyPlanConditionV3.AfterFirstContact: return "IfSetAvailable";
                case RallyPlanConditionV3.AfterSecondContact: return "IfSetAvailable";
                default: return "Always";
            }
        }

        private static string ToReplayClaim(RallyPlanSpatialClaimV3 claim)
        {
            switch (claim)
            {
                case RallyPlanSpatialClaimV3.FrontRight:
                case RallyPlanSpatialClaimV3.FrontCenter:
                case RallyPlanSpatialClaimV3.FrontLeft:
                    return "NetZone";
                default:
                    return "BackcourtZone";
            }
        }

        private static int CheckedInt(long value, string name)
        {
            if (value > int.MaxValue)
            {
                throw new InvalidOperationException(name + " exceeds the replay contract range.");
            }

            return (int)value;
        }

        private static ReplayExecutionEnvelopeRecordV4 ToReplayEnvelope(
            ExecutionEnvelopeV4 envelope)
        {
            return new ReplayExecutionEnvelopeRecordV4(
                envelope.Version,
                envelope.Identity,
                envelope.DerivedAttributesFingerprint,
                Sha256(ExecutionEnvelopePolicyV4.Default.ToCanonicalBytes()),
                envelope.SourceIntentIdentity,
                envelope.CandidateCategory.ToString(),
                Vector(envelope.BaselineTarget),
                Vector(envelope.BaselineVelocity),
                Vector(envelope.MaximumVelocity),
                Error(envelope.TargetError),
                Error(envelope.VelocityError),
                envelope.RequestedEffort,
                envelope.MaximumEffort,
                envelope.Sampling.SamplingKey,
                envelope.Sampling.PolicyVersion,
                envelope.Sampling.SampleCount,
                Names(envelope.Sampling.CandidateCategoryOrder),
                Names(envelope.Sampling.DegradationLadder),
                envelope.Expansion.MaximumExpansionCount,
                envelope.Expansion.AllowedExpansionCount,
                envelope.Expansion.CurrentExpansionCount,
                envelope.Expansion.PerStepExpansionFactor);
        }

        private static ReplayTrajectoryArtifactRecordV4 ToReplayTrajectory(
            BallTrajectoryPredictionArtifactV4 artifact)
        {
            var key = artifact.Key;
            return new ReplayTrajectoryArtifactRecordV4(
                artifact.ArtifactIdentity,
                artifact.PredictorSource,
                artifact.PredictorVersion,
                artifact.PredictorConfigurationHash,
                new ReplayTrajectoryCacheKeyRecordV4(
                    key.Identity,
                    key.BallStateVersion,
                    key.BallStateFingerprint,
                    key.PhysicsConfigurationHash,
                    key.SamplingKey,
                    key.PredictorVersion,
                    key.PredictorConfigurationHash,
                    key.EnvelopeIdentity,
                    ((ExecutionDegradationStepV4)key.DegradationStep)
                        .ToString()));
        }

        private static ReplaySampleClassificationRecordV4
            ToReplayClassification(
                ExecutionSampleClassificationV4 classification)
        {
            var sample = classification.Sample ??
                throw new InvalidOperationException(
                    "Formal V4 replay classification requires the actual sample.");
            return new ReplaySampleClassificationRecordV4(
                classification.Kind.ToString(),
                classification.TestedEnvelopeIdentity,
                classification.ExpandedEnvelopeIdentity ?? string.Empty,
                new ReplayActualSampleRecordV4(
                    sample.EnvelopeIdentity,
                    sample.SamplingKey,
                    sample.CandidateCategory.ToString(),
                    Vector(sample.Target),
                    Vector(sample.Velocity),
                    sample.Effort),
                Copy(classification.OffendingDimensions));
        }

        private static ReplayObservedP6GeometryRecordV4 ToReplayGeometry(
            AttackGeometryFactV3 geometry)
        {
            return geometry == null
                ? null
                : new ReplayObservedP6GeometryRecordV4(
                    geometry.Actor.Value,
                    geometry.Side.ToString(),
                    Vector(geometry.TakeoffPoint),
                    Vector(geometry.ContactPoint),
                    geometry.AttackLineDistanceFromCenter,
                    geometry.NetHeight);
        }

        private static ReplayBoundedErrorRecordV4 Error(
            BoundedErrorDistributionV4 value)
        {
            return new ReplayBoundedErrorRecordV4(
                value.Kind.ToString(),
                Vector(value.MinimumError),
                Vector(value.MaximumError));
        }

        private static ReplayVector3RecordV4 Vector(
            Volleyball.Domain.Simulation.SimVector3 value)
        {
            return new ReplayVector3RecordV4(value.X, value.Y, value.Z);
        }

        private static string[] Names<T>(IReadOnlyList<T> values)
        {
            var names = new string[values.Count];
            for (var index = 0; index < names.Length; index++)
            {
                names[index] = values[index].ToString();
            }

            return names;
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            var copy = new string[values.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }

        private static IReadOnlyList<ReplayAbilityConsumptionRecordV4>
            ToReplayConsumptions(
                string playerId,
                string derivedAttributesFingerprint,
                IReadOnlyList<ExecutionAbilityConsumptionV4> consumptions)
        {
            if (consumptions == null || consumptions.Count == 0)
            {
                throw new InvalidOperationException(
                    "Formal V4 replay requires runtime consumption evidence.");
            }

            var records =
                new ReplayAbilityConsumptionRecordV4[consumptions.Count];
            for (var index = 0; index < records.Length; index++)
            {
                var consumption = consumptions[index];
                records[index] = new ReplayAbilityConsumptionRecordV4(
                    playerId,
                    derivedAttributesFingerprint,
                    consumption.AttributeName,
                    consumption.Value,
                    consumption.EvidenceKind);
            }

            return records;
        }

        private static string ReplayId(MatchContextV4 context)
        {
            return "formal-v4-" +
                context.SessionId.ToString("D", CultureInfo.InvariantCulture);
        }

        private static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var output = new StringBuilder(64);
            for (var index = 0; index < hash.Length; index++)
            {
                output.Append(
                    hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return output.ToString();
        }
    }
}
