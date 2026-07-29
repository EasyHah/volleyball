using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.AI
{
    // Gate I deliberately begins after Gate H has selected and executed Set.  In
    // particular, this enum has no Set contact command: Gate H remains its writer.
    public enum AttackDefenseCommandKind
    {
        AttackPreparation, AttackContact, BlockContact, FloorDefense, AttackCover,
        Reorganization, CancelUncommitted
    }

    public enum AttackDefenseAuthorityPhaseV3
    {
        Idle, SetIntentPlanned, AttackPlanned, ThreatPublished, DefenseCommitted,
        AttackCommitted, AwaitingActualContact, ToolRecoveryAwaitingBlock,
        ToolRecoveryAwaitingReceive, ReorganizationPlanned, HandedOff, Terminal
    }

    public enum ToolRecoveryReboundObservationV3
    {
        NotApplicable,
        ReturnsToAttackingSide,
        ReturnsAway
    }

    public sealed class GateISetIntentReceiptV3
    {
        public GateISetIntentReceiptV3(long planRevision, long sourceSequence, GateISetIntentV3 intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (planRevision != intent.PlanRevision || sourceSequence != intent.SourceSequence)
                throw new ArgumentException("Receipt identity must match SetIntent.");
            PlanRevision = planRevision; SourceSequence = sourceSequence; Intent = intent;
            EvidenceIdentity = "gate-i-set-intent-" + planRevision + "-" + sourceSequence;
        }
        public long PlanRevision { get; } public long SourceSequence { get; }
        public GateISetIntentV3 Intent { get; } public string EvidenceIdentity { get; }
    }

    public sealed class GateISetIntentPlanningResultV3
    {
        public GateISetIntentPlanningResultV3(GateISetIntentV3 intent, GateISetIntentReceiptV3 receipt)
        { Intent = intent ?? throw new ArgumentNullException(nameof(intent)); Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt)); }
        public GateISetIntentV3 Intent { get; } public GateISetIntentReceiptV3 Receipt { get; }
    }

    public sealed class GateIAcceptedSetV3
    {
        public GateIAcceptedSetV3(long planRevision, long sourceSequence, AcceptedSetEvidenceV3 acceptedSet)
        {
            if (planRevision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(planRevision < 0 ? nameof(planRevision) : nameof(sourceSequence));
            PlanRevision = planRevision; SourceSequence = sourceSequence; AcceptedSet = acceptedSet ?? throw new ArgumentNullException(nameof(acceptedSet));
        }
        public long PlanRevision { get; } public long SourceSequence { get; } public AcceptedSetEvidenceV3 AcceptedSet { get; }
    }

    public sealed class GateIContactEvidenceV3
    {
        public GateIContactEvidenceV3(long planRevision, long sourceSequence, PlayerId actor,
            PlanCoverageReason coverageReason, string reorganizationExitIdentity = null)
            : this(planRevision, sourceSequence, actor, coverageReason,
                AttackDefenseCommandKind.AttackContact, RallyPlanBranchV3.Primary,
                string.Empty, string.Empty, false, reorganizationExitIdentity)
        {
        }

        public GateIContactEvidenceV3(long planRevision, long sourceSequence, PlayerId actor,
            PlanCoverageReason coverageReason, AttackDefenseCommandKind actionKind,
            RallyPlanBranchV3 branch, string envelopeIdentity,
            string trajectoryArtifactIdentity, bool v3Accepted,
            string reorganizationExitIdentity = null,
            ToolRecoveryReboundObservationV3 toolRecoveryRebound = ToolRecoveryReboundObservationV3.NotApplicable,
            int remainingTouchesAfterContact = -1,
            AttackDefenseCommandExecutionV4 toolRecoveryReceiveExecution = null)
        {
            if (planRevision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(planRevision < 0 ? nameof(planRevision) : nameof(sourceSequence));
            if (!Enum.IsDefined(typeof(PlanCoverageReason), coverageReason)) throw new ArgumentOutOfRangeException(nameof(coverageReason));
            PlanRevision = planRevision; SourceSequence = sourceSequence; Actor = actor;
            if (!Enum.IsDefined(typeof(AttackDefenseCommandKind), actionKind)) throw new ArgumentOutOfRangeException(nameof(actionKind));
            if (!Enum.IsDefined(typeof(RallyPlanBranchV3), branch)) throw new ArgumentOutOfRangeException(nameof(branch));
            CoverageReason = coverageReason; ActionKind = actionKind; Branch = branch;
            EnvelopeIdentity = envelopeIdentity ?? string.Empty;
            TrajectoryArtifactIdentity = trajectoryArtifactIdentity ?? string.Empty;
            V3Accepted = v3Accepted;
            ReorganizationExitIdentity = reorganizationExitIdentity ?? string.Empty;
            if (!Enum.IsDefined(typeof(ToolRecoveryReboundObservationV3), toolRecoveryRebound))
                throw new ArgumentOutOfRangeException(nameof(toolRecoveryRebound));
            if (remainingTouchesAfterContact < -1 || remainingTouchesAfterContact > 3)
                throw new ArgumentOutOfRangeException(nameof(remainingTouchesAfterContact));
            ToolRecoveryRebound = toolRecoveryRebound;
            RemainingTouchesAfterContact = remainingTouchesAfterContact;
            ToolRecoveryReceiveExecution = toolRecoveryReceiveExecution;
        }
        public long PlanRevision { get; } public long SourceSequence { get; } public PlayerId Actor { get; }
        public PlanCoverageReason CoverageReason { get; } public string ReorganizationExitIdentity { get; }
        public AttackDefenseCommandKind ActionKind { get; }
        public RallyPlanBranchV3 Branch { get; }
        public string EnvelopeIdentity { get; }
        public string TrajectoryArtifactIdentity { get; }
        public bool V3Accepted { get; }
        public ToolRecoveryReboundObservationV3 ToolRecoveryRebound { get; }
        public int RemainingTouchesAfterContact { get; }
        // A successful physical tool block supplies the receive execution from
        // its actual rebound state.  The coordinator publishes this immutable
        // command; presentation may not replace it at the net crossing.
        public AttackDefenseCommandExecutionV4 ToolRecoveryReceiveExecution { get; }
    }

    public sealed class AttackDefenseAuthorityCommand
    {
        public AttackDefenseAuthorityCommand(long planRevision, long sourceSequence, AttackDefenseCommandKind kind, PlayerId actor, bool isCommitted,
            AttackDefenseCommandExecutionV4 execution = null, RallyPlanBranchV3 branch = RallyPlanBranchV3.Primary,
            long cancelTargetSourceSequence = -1, AttackDefenseCommandKind? cancelTargetKind = null,
            string reorganizationExitIdentity = null, string candidateIdentity = null)
        {
            if (planRevision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(planRevision < 0 ? nameof(planRevision) : nameof(sourceSequence));
            if (!Enum.IsDefined(typeof(AttackDefenseCommandKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(RallyPlanBranchV3), branch)) throw new ArgumentOutOfRangeException(nameof(branch));
            PlanRevision = planRevision; SourceSequence = sourceSequence; Kind = kind; Actor = actor; IsCommitted = isCommitted; Execution = execution; Branch = branch;
            CancelTargetSourceSequence = cancelTargetSourceSequence;
            CancelTargetKind = cancelTargetKind;
            ReorganizationExitIdentity = reorganizationExitIdentity ?? string.Empty;
            CandidateIdentity = candidateIdentity ?? string.Empty;
        }
        public long PlanRevision { get; } public long SourceSequence { get; } public AttackDefenseCommandKind Kind { get; }
        public PlayerId Actor { get; } public bool IsCommitted { get; }
        public AttackDefenseCommandExecutionV4 Execution { get; }
        public RallyPlanBranchV3 Branch { get; }
        public long CancelTargetSourceSequence { get; }
        public AttackDefenseCommandKind? CancelTargetKind { get; }
        public string ReorganizationExitIdentity { get; }
        public string CandidateIdentity { get; }
    }

    // Immutable execution inputs are supplied by the coordinator boundary; the
    // presentation controller only validates and executes them.
    public sealed class AttackDefenseCommandExecutionV4
    {
        public AttackDefenseCommandExecutionV4(float scheduledSimulationTime,
            float movementStartSimulationTime, SkillExecutionError executionError,
            int contactGroupId, ExecutionSampleClassificationV4 executionClassification,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact, SimVector3 movementTarget,
            AttackApproachPlan? attackApproach = null,
            AttackContactPlan? attackContactPlan = null,
            SimVector3? physicalContactCenter = null)
        {
            if (!Finite(scheduledSimulationTime) || !Finite(movementStartSimulationTime) ||
                contactGroupId <= 0 || !Finite(movementTarget))
                throw new ArgumentOutOfRangeException("Execution inputs must be finite and ordered.");
            if (executionClassification == null || trajectoryArtifact == null)
                throw new ArgumentNullException(executionClassification == null ? nameof(executionClassification) : nameof(trajectoryArtifact));
            ScheduledSimulationTime = scheduledSimulationTime;
            MovementStartSimulationTime = movementStartSimulationTime;
            ExecutionError = executionError;
            ContactGroupId = contactGroupId;
            ExecutionClassification = executionClassification;
            TrajectoryArtifact = trajectoryArtifact;
            MovementTarget = movementTarget;
            AttackApproach = attackApproach;
            AttackContactPlan = attackContactPlan;
            if (physicalContactCenter.HasValue && !Finite(physicalContactCenter.Value))
                throw new ArgumentOutOfRangeException(nameof(physicalContactCenter));
            PhysicalContactCenter = physicalContactCenter;
        }
        public float ScheduledSimulationTime { get; }
        public float MovementStartSimulationTime { get; }
        public SkillExecutionError ExecutionError { get; }
        public int ContactGroupId { get; }
        public ExecutionSampleClassificationV4 ExecutionClassification { get; }
        public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
        public SimVector3 MovementTarget { get; }
        public AttackApproachPlan? AttackApproach { get; }
        public AttackContactPlan? AttackContactPlan { get; }
        public SimVector3? PhysicalContactCenter { get; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        private static bool Finite(SimVector3 value) => value.IsFinite;
    }

        public sealed class AttackDefenseAuthorityEvidenceV3
    {
        public AttackDefenseAuthorityEvidenceV3(long planRevision, long sourceSequence, AttackDefenseAuthorityPhaseV3 phase,
            AttackDefensePlanV3 plan, PlanCoverageDecision coverageDecision,
            PerceptionReceiptV3 perception = null)
        { PlanRevision = planRevision; SourceSequence = sourceSequence; Phase = phase; Plan = plan; CoverageDecision = coverageDecision; Perception = perception; }
        public long PlanRevision { get; } public long SourceSequence { get; } public AttackDefenseAuthorityPhaseV3 Phase { get; }
        public AttackDefensePlanV3 Plan { get; } public PlanCoverageDecision CoverageDecision { get; }
        public PerceptionReceiptV3 Perception { get; }
    }

    public sealed class AttackDefenseCommandBatch
    {
        public AttackDefenseCommandBatch(IReadOnlyList<AttackDefenseAuthorityCommand> commands, AttackDefenseAuthorityEvidenceV3 evidence)
        { Commands = new ReadOnlyCollection<AttackDefenseAuthorityCommand>((commands ?? throw new ArgumentNullException(nameof(commands))).ToArray()); Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence)); }
        public IReadOnlyList<AttackDefenseAuthorityCommand> Commands { get; } public AttackDefenseAuthorityEvidenceV3 Evidence { get; }
    }
    public interface IAttackDefenseAuthorityCommandSink { void Publish(AttackDefenseCommandBatch batch); }

    public sealed class AttackDefenseAuthorityStateV3
    {
        internal AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3 phase, long revision, TeamSide attackingSide, AttackDefensePlanV3 plan, PlanCoverageDecision coverage)
        { Phase = phase; Revision = revision; AttackingSide = attackingSide; Plan = plan; CoverageDecision = coverage; }
        public AttackDefenseAuthorityPhaseV3 Phase { get; } public long Revision { get; } public TeamSide AttackingSide { get; } public AttackDefensePlanV3 Plan { get; }
        public PlanCoverageDecision CoverageDecision { get; }
        public static AttackDefenseAuthorityStateV3 Idle { get; } = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.Idle, -1, TeamSide.Home, null, null);
    }

    public sealed class AttackDefenseAuthorityCoordinator
    {
        private readonly AttackDefensePlanner _planner; private readonly IAttackDefenseAuthorityCommandSink _sink;
        private GateISetIntentV3 _intent; private AttackPlanningResultV3 _attack; private JointDefensePlanV3 _defense;
        private IReadOnlyDictionary<PlayerId, GateITacticalPlayerV3> _players;
        private readonly Dictionary<string, AttackDefenseCommandExecutionV4>
            _committedDefenseExecutions = new Dictionary<string, AttackDefenseCommandExecutionV4>();
        private AttackDefenseCommandExecutionV4 _toolRecoveryReceiveExecution;
        private TeamSide _attackingSide;
        private long _lastSequence = -1;
        private PerceptionReceiptV3 _perception;
        public AttackDefenseAuthorityCoordinator(AttackDefensePlanner planner, IAttackDefenseAuthorityCommandSink sink)
        { _planner = planner ?? throw new ArgumentNullException(nameof(planner)); _sink = sink ?? throw new ArgumentNullException(nameof(sink)); State = AttackDefenseAuthorityStateV3.Idle; }
        public AttackDefenseAuthorityStateV3 State { get; private set; }
        // Exposes only the already-generated public distribution for the joint
        // defense handoff; it never exposes the selected route or future sample.
        public PublicAttackThreatV3 PublicThreat => _attack?.PublicThreat;
        public PerceptionReceiptV3 CurrentPerception => _perception;

        public GateISetIntentPlanningResultV3 PlanSetIntent(SetIntentPlanningRequestV3 request)
        {
            if (State.Phase != AttackDefenseAuthorityPhaseV3.Idle) throw new InvalidOperationException("SetIntent is already planned.");
            var intent = _planner.PlanSetIntent(request ?? throw new ArgumentNullException(nameof(request)));
            if (intent.SourceSequence <= _lastSequence) throw new InvalidOperationException("Source sequence must increase.");
            _intent = intent; _lastSequence = intent.SourceSequence;
            _attackingSide = request.AttackingSide;
            State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.SetIntentPlanned, intent.PlanRevision, _attackingSide, null, PlanCoverageDecision.Covered(intent.PlanRevision.ToString(), PlanCoverageReason.RallyOpen));
            return new GateISetIntentPlanningResultV3(intent, new GateISetIntentReceiptV3(intent.PlanRevision, intent.SourceSequence, intent));
        }

        public AttackDefenseAuthorityStateV3 AcceptSet(GateIAcceptedSetV3 accepted, AttackPlanningRequestV3 request)
        {
            if (accepted == null || request == null) throw new ArgumentNullException(accepted == null ? nameof(accepted) : nameof(request));
            Require(AttackDefenseAuthorityPhaseV3.SetIntentPlanned, accepted.PlanRevision, accepted.SourceSequence);
            if (request.Revision != State.Revision || !SameSet(request.SetIntent, _intent) || !SameSetEvidence(accepted.AcceptedSet, _intent) || !SameSetEvidence(request.ActualSet, _intent))
                throw new InvalidOperationException("Accepted Set evidence does not match the active SetIntent.");
            _attack = _planner.PlanAttack(request); _lastSequence = accepted.SourceSequence;
            _players = request.Players.GroupBy(player => player.Player).ToDictionary(group => group.Key, group => group.First());
            State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.AttackPlanned, State.Revision, _attackingSide, null, State.CoverageDecision);
            return State;
        }

        public AttackDefenseAuthorityStateV3 PublishThreat(long revision, long sourceSequence)
        { Require(AttackDefenseAuthorityPhaseV3.AttackPlanned, revision, sourceSequence); _lastSequence = sourceSequence; State = New(AttackDefenseAuthorityPhaseV3.ThreatPublished); return State; }

        public void ApplyPerception(PerceptionReceiptV3 receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            var defendingSide = _attackingSide == TeamSide.Home
                ? TeamSide.Away
                : TeamSide.Home;
            if (State.Phase != AttackDefenseAuthorityPhaseV3.ThreatPublished ||
                receipt.Revision != State.Revision ||
                receipt.SourceSequence != _lastSequence ||
                receipt.ObservingSide != defendingSide ||
                _intent == null ||
                receipt.AuthoritativeArtifactIdentity !=
                _intent.TrajectoryArtifact.ArtifactIdentity)
                throw new InvalidOperationException(
                    "Perception must belong to the published threat event.");
            _perception = receipt;
        }

        public AttackDefenseAuthorityStateV3 CommitDefense(long revision, long sourceSequence, JointDefensePlanV3 defense)
        {
            Require(AttackDefenseAuthorityPhaseV3.ThreatPublished, revision, sourceSequence);
            if (defense == null || defense.SourceThreatIdentity != _attack.PublicThreat.ThreatIdentity) throw new InvalidOperationException("Defense must be composed from the published threat.");
            _defense = defense; _lastSequence = sourceSequence;
            var defensePlan = new AttackDefensePlanV3(_attackingSide, revision,
                "gate-i-plan-" + revision, _intent, _attack.Candidates,
                _attack.PublicThreat, defense, null, MergeExits(defense),
                AttackCoverageFor(_attack));
            State = new AttackDefenseAuthorityStateV3(
                AttackDefenseAuthorityPhaseV3.DefenseCommitted, revision,
                _attackingSide, defensePlan, State.CoverageDecision);
            Publish(sourceSequence, State, defense.Responsibilities.Select(x =>
            {
                var kind = x.Kind == DefenseResponsibilityKindV3.PrimaryBlock || x.Kind == DefenseResponsibilityKindV3.SupportingBlock
                    ? AttackDefenseCommandKind.BlockContact : AttackDefenseCommandKind.FloorDefense;
                var execution = ExecutionFor(x.Actor, kind, x.Zone, x.Kind,
                        x.Kind == DefenseResponsibilityKindV3.SupportingBlock
                            ? defense.Responsibilities.Where(value =>
                                value.Kind == DefenseResponsibilityKindV3.SupportingBlock)
                                .OrderBy(value => value.Actor.ToString(), StringComparer.Ordinal)
                                .ToList().FindIndex(value => value.Actor.Equals(x.Actor))
                            : 0);
                _committedDefenseExecutions[DefenseExecutionKey(x.Actor, kind, x.Branch)] = execution;
                return new AttackDefenseAuthorityCommand(revision, sourceSequence, kind, x.Actor, true,
                    execution, x.Branch);
            }));
            return State;
        }

        public AttackDefenseAuthorityStateV3 CommitFinalAttack(long revision, long sourceSequence)
        {
            Require(AttackDefenseAuthorityPhaseV3.DefenseCommitted, revision, sourceSequence);
            var selected = _planner.ChooseFinal(_attack, _defense).Candidate; _lastSequence = sourceSequence;
            var plan = new AttackDefensePlanV3(_attackingSide, revision, "gate-i-plan-" + revision, _intent, _attack.Candidates, _attack.PublicThreat, _defense, selected, MergeExits(_defense),
                AttackCoverageFor(_attack));
            State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.AttackCommitted, revision, _attackingSide, plan, State.CoverageDecision);
            Publish(sourceSequence, State, new[] { new AttackDefenseAuthorityCommand(revision, sourceSequence, AttackDefenseCommandKind.AttackContact, selected.Actor, true, ExecutionFor(selected.Actor, AttackDefenseCommandKind.AttackContact), candidateIdentity: selected.CandidateIdentity) });
            return State;
        }

        // A post-attack rebound is not allowed to revive a predicted defense
        // window. It publishes a new execution derived from the accepted live
        // ball state while retaining the current immutable plan and authority
        // phase until the physical receive is accepted.
        public AttackDefenseAuthorityStateV3 PublishActualContinuation(
            long sourceSequence,
            AttackDefenseCommandKind kind,
            PlayerId actor,
            AttackDefenseCommandExecutionV4 execution,
            RallyPlanBranchV3 branch = RallyPlanBranchV3.Primary)
        {
            if ((State.Phase != AttackDefenseAuthorityPhaseV3.AwaitingActualContact &&
                 State.Phase != AttackDefenseAuthorityPhaseV3.ReorganizationPlanned) ||
                sourceSequence <= _lastSequence ||
                (kind != AttackDefenseCommandKind.FloorDefense &&
                 kind != AttackDefenseCommandKind.AttackCover) ||
                execution == null)
                throw new InvalidOperationException(
                    "Actual continuation requires a current physical receive opportunity.");

            var isDeclared = kind == AttackDefenseCommandKind.FloorDefense
                ? State.Plan.Defense.Responsibilities.Any(value =>
                    value.Actor.Equals(actor) && value.Branch == branch)
                : State.Plan.AttackCoverageResponsibilities.Any(value =>
                    value.Actor.Equals(actor) && value.Branch == branch);
            if (!isDeclared)
                throw new InvalidOperationException(
                    "Actual continuation actor is outside the declared opportunity.");

            _lastSequence = sourceSequence;
            _committedDefenseExecutions[DefenseExecutionKey(actor, kind, branch)] =
                execution;
            State = new AttackDefenseAuthorityStateV3(
                AttackDefenseAuthorityPhaseV3.AwaitingActualContact,
                State.Revision,
                State.AttackingSide,
                State.Plan,
                State.CoverageDecision);
            Publish(sourceSequence, State, new[]
            {
                new AttackDefenseAuthorityCommand(
                    State.Revision, sourceSequence, kind, actor, true,
                    execution, branch)
            });
            return State;
        }

        private IReadOnlyList<ReorganizationExitV3> MergeExits(JointDefensePlanV3 defense) =>
            defense.ReorganizationExits.Concat(_attack.ReorganizationExits)
                .GroupBy(value => value.Identity, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.First()).ToArray();

        private static IReadOnlyList<AttackCoverageResponsibilityV3> AttackCoverageFor(
            AttackPlanningResultV3 attack)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            return attack.ReorganizationExits
                .OrderBy(exit => exit.Identity, StringComparer.Ordinal)
                .ThenBy(exit => exit.Actor.Value, StringComparer.Ordinal)
                .GroupBy(exit => exit.Actor)
                .Select(group => new AttackCoverageResponsibilityV3(
                    group.Key, RallyPlanBranchV3.Primary))
                .ToArray();
        }

        // An actual V3-accepted dig may be made by a player whose committed
        // Gate I responsibility was block/coverage rather than a scheduled
        // FloorDefense contact.  This is a pure preview: presentation can bind
        // its returned immutable evidence to the event before any state change.
        public AttackDefenseAuthorityEvidenceV3 PreviewIncidentalDefenseContact(
            long revision, long sourceSequence, PlayerId actor,
            RallyPlanBranchV3 branch, string envelopeIdentity,
            string trajectoryArtifactIdentity, bool v3Accepted)
        {
            if (State.Phase != AttackDefenseAuthorityPhaseV3.AwaitingActualContact ||
                revision != State.Revision || sourceSequence <= _lastSequence ||
                !v3Accepted || string.IsNullOrWhiteSpace(envelopeIdentity) ||
                string.IsNullOrWhiteSpace(trajectoryArtifactIdentity))
                throw new InvalidOperationException(
                    "Incidental defense preview requires current V3-accepted evidence.");
            if (!Enum.IsDefined(typeof(RallyPlanBranchV3), branch) ||
                !State.Plan.Defense.Responsibilities.Any(value =>
                    value.Actor.Equals(actor)))
                throw new InvalidOperationException(
                    "Incidental defense actor is outside the committed defense roster.");

            return new AttackDefenseAuthorityEvidenceV3(
                State.Revision,
                sourceSequence,
                State.Phase,
                State.Plan,
                Coverage(PlanCoverageReason.ResponsibleActorChanged),
                _perception);
        }

        public AttackDefenseAuthorityStateV3 AcceptContact(GateIContactEvidenceV3 contact)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));
            if (State.Phase != AttackDefenseAuthorityPhaseV3.AttackCommitted &&
                State.Phase != AttackDefenseAuthorityPhaseV3.AwaitingActualContact &&
                State.Phase != AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock &&
                State.Phase != AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive)
                throw new InvalidOperationException("No Gate I contact is awaiting acceptance.");
            if (contact.PlanRevision != State.Revision || contact.SourceSequence <= _lastSequence) throw new InvalidOperationException("Stale or mismatched contact evidence.");
            var coverage = Coverage(contact.CoverageReason);
            if (coverage.Kind == PlanCoverageDecisionKind.TerminalNoPlan) { _lastSequence = contact.SourceSequence; State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.Terminal, State.Revision, _attackingSide, State.Plan, coverage); return State; }
            ValidateContactEvidence(contact);
            if (State.Phase == AttackDefenseAuthorityPhaseV3.AttackCommitted)
            {
                if (contact.ActionKind != AttackDefenseCommandKind.AttackContact)
                    throw new InvalidOperationException("The committed attack must be accepted before defensive coverage.");
                _lastSequence = contact.SourceSequence;
                State = new AttackDefenseAuthorityStateV3(
                    IsSelectedToolRecovery() ? AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock : AttackDefenseAuthorityPhaseV3.AwaitingActualContact,
                    State.Revision, _attackingSide, State.Plan, coverage);
                return State;
            }

            if (State.Phase == AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock)
            {
                if (!IsExpectedToolBlock(contact) ||
                    contact.ToolRecoveryRebound != ToolRecoveryReboundObservationV3.ReturnsToAttackingSide ||
                    contact.RemainingTouchesAfterContact <= 0)
                    return CompleteOrdinaryDefense(contact, PlanCoverageReason.ResponsibleActorChanged);
                if (contact.ToolRecoveryReceiveExecution == null)
                    return CompleteOrdinaryDefense(contact,
                        PlanCoverageReason.BallEnvelopeExceeded);
                _lastSequence = contact.SourceSequence;
                _toolRecoveryReceiveExecution = contact.ToolRecoveryReceiveExecution;
                State = new AttackDefenseAuthorityStateV3(
                    AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive,
                    State.Revision, _attackingSide, State.Plan, coverage);
                var recovery = State.Plan.SelectedAction.ToolRecoveryEvidence;
                Publish(contact.SourceSequence, State, new[] {
                    new AttackDefenseAuthorityCommand(State.Revision, contact.SourceSequence,
                        AttackDefenseCommandKind.AttackCover, recovery.RecoveryActor, true,
                        contact.ToolRecoveryReceiveExecution, RallyPlanBranchV3.Primary)
                });
                return State;
            }

            if (State.Phase == AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive)
            {
                if (contact.ActionKind != AttackDefenseCommandKind.AttackCover ||
                    !contact.Actor.Equals(State.Plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor) ||
                    contact.RemainingTouchesAfterContact <= 0)
                    throw new InvalidOperationException("Tool recovery requires the declared non-attacker V3-accepted receive.");
                return CompleteReorganization(contact, coverage,
                    State.Plan.SelectedAction.ToolRecoveryEvidence.ReorganizationExitIdentity);
            }

            if (contact.ActionKind != AttackDefenseCommandKind.BlockContact &&
                contact.ActionKind != AttackDefenseCommandKind.FloorDefense &&
                contact.ActionKind != AttackDefenseCommandKind.AttackCover)
                throw new InvalidOperationException("Awaiting Gate I coverage requires a defensive contact.");
            return CompleteReorganization(contact, coverage, contact.ReorganizationExitIdentity);
        }

        private bool IsSelectedToolRecovery() => State.Plan?.SelectedAction?.ToolRecoveryEvidence != null;
        private bool IsExpectedToolBlock(GateIContactEvidenceV3 contact) =>
            contact.ActionKind == AttackDefenseCommandKind.BlockContact &&
            contact.Actor.Equals(State.Plan.SelectedAction.ToolRecoveryEvidence.Blocker);

        private AttackDefenseAuthorityStateV3 CompleteOrdinaryDefense(GateIContactEvidenceV3 contact,
            PlanCoverageReason reason)
        {
            ValidateContactEvidence(contact);
            var ordinaryExit = State.Plan.Defense.ReorganizationExits.OrderBy(value => value.Identity, StringComparer.Ordinal).FirstOrDefault()
                ?? throw new InvalidOperationException("Ordinary block coverage requires a declared defense exit.");
            return CompleteReorganization(contact, Coverage(reason), ordinaryExit.Identity);
        }

        private AttackDefenseAuthorityStateV3 CompleteReorganization(GateIContactEvidenceV3 contact,
            PlanCoverageDecision coverage, string exitIdentity)
        {
            var exit = State.Plan.ReorganizationExits.OrderBy(x => x.Identity, StringComparer.Ordinal).FirstOrDefault(x => x.Identity == exitIdentity);
            if (exit == null) throw new InvalidOperationException("Actual contact does not select a declared reorganization exit.");
            _lastSequence = contact.SourceSequence; State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned, State.Revision, _attackingSide, State.Plan, coverage);
            Publish(contact.SourceSequence, State, new[] { new AttackDefenseAuthorityCommand(State.Revision, contact.SourceSequence, AttackDefenseCommandKind.Reorganization, exit.Actor, true, ExecutionFor(exit.Actor, AttackDefenseCommandKind.Reorganization), contact.Branch, reorganizationExitIdentity: exit.Identity) });
            return State;
        }

        public AttackDefenseAuthorityStateV3 CompleteReorganizationAndReset(
            long revision, long sourceSequence)
        {
            if (State.Phase != AttackDefenseAuthorityPhaseV3.ReorganizationPlanned ||
                revision != State.Revision ||
                sourceSequence <= _lastSequence)
                throw new InvalidOperationException(
                    "Only the current Gate I reorganization may hand off.");

            _lastSequence = sourceSequence;
            // Preserve the ordered handoff as a semantic state boundary before
            // clearing immutable opportunity data for the next possession.
            State = new AttackDefenseAuthorityStateV3(
                AttackDefenseAuthorityPhaseV3.HandedOff,
                State.Revision,
                _attackingSide,
                State.Plan,
                State.CoverageDecision);
            _intent = null;
            _attack = null;
            _defense = null;
            _players = null;
            _committedDefenseExecutions.Clear();
            _toolRecoveryReceiveExecution = null;
            State = AttackDefenseAuthorityStateV3.Idle;
            _perception = null;
            return State;
        }

        private AttackDefenseAuthorityStateV3 New(AttackDefenseAuthorityPhaseV3 phase) => new AttackDefenseAuthorityStateV3(phase, State.Revision, _attackingSide, State.Plan, State.CoverageDecision);
        private void Require(AttackDefenseAuthorityPhaseV3 phase, long revision, long sourceSequence)
        { if (State.Phase != phase || revision != State.Revision || sourceSequence <= _lastSequence) throw new InvalidOperationException("Phase, revision, or source sequence is invalid."); }
        private void Publish(long sequence, AttackDefenseAuthorityStateV3 state, IEnumerable<AttackDefenseAuthorityCommand> commands) => _sink.Publish(new AttackDefenseCommandBatch(commands.ToArray(), new AttackDefenseAuthorityEvidenceV3(state.Revision, sequence, state.Phase, state.Plan, state.CoverageDecision, _perception)));
        private PlanCoverageDecision Coverage(PlanCoverageReason reason)
        {
            var kind = reason == PlanCoverageReason.ResponsibleActorChanged ? PlanCoverageDecisionKind.LocalRevision :
                reason == PlanCoverageReason.BallEnvelopeExceeded ? PlanCoverageDecisionKind.ScopedReplan :
                reason == PlanCoverageReason.RallyEnd ? PlanCoverageDecisionKind.TerminalNoPlan :
                reason == PlanCoverageReason.EnvelopeExceeded || reason == PlanCoverageReason.DependencyCascadeExceeded || reason == PlanCoverageReason.BudgetDegradationRequired ? PlanCoverageDecisionKind.GlobalReplan : PlanCoverageDecisionKind.CoveredActivateBranch;
            return new PlanCoverageDecision(kind, State.Revision.ToString(), reason, Array.Empty<string>(), kind == PlanCoverageDecisionKind.LocalRevision ? 1 : kind == PlanCoverageDecisionKind.ScopedReplan ? 2 : kind == PlanCoverageDecisionKind.GlobalReplan ? 3 : 0);
        }
        private static bool SameSet(GateISetIntentV3 a, GateISetIntentV3 b) => a != null && b != null && a.PlanRevision == b.PlanRevision && a.SourceSequence == b.SourceSequence;
        private static bool SameSetEvidence(AcceptedSetEvidenceV3 evidence, GateISetIntentV3 intent) => evidence != null && evidence.Actor.Equals(intent.Organizer) && evidence.EnvelopeIdentity == intent.ExecutionClassification.ExecutableEnvelope.Identity && evidence.TrajectoryArtifactIdentity == intent.TrajectoryArtifact.ArtifactIdentity;

        private void ValidateContactEvidence(GateIContactEvidenceV3 contact)
        {
            if (!contact.V3Accepted) throw new InvalidOperationException("Gate I contacts require a V3-accepted marker.");
            if (State.Phase == AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive &&
                contact.ActionKind == AttackDefenseCommandKind.AttackCover &&
                State.Plan?.SelectedAction?.ToolRecoveryEvidence != null &&
                contact.Actor.Equals(State.Plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor) &&
                MatchesToolRecoveryExecution(contact.ToolRecoveryReceiveExecution))
                return;
            if (State.Phase == AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock &&
                contact.ActionKind == AttackDefenseCommandKind.BlockContact &&
                State.Plan.Defense.Responsibilities.Any(value =>
                    value.Actor.Equals(contact.Actor) &&
                    (value.Kind == DefenseResponsibilityKindV3.PrimaryBlock ||
                     value.Kind == DefenseResponsibilityKindV3.SupportingBlock)) &&
                !string.IsNullOrWhiteSpace(contact.EnvelopeIdentity) &&
                !string.IsNullOrWhiteSpace(contact.TrajectoryArtifactIdentity))
                return;
            if (contact.ActionKind == AttackDefenseCommandKind.AttackContact)
            {
                if (!State.Plan.SelectedAction.Actor.Equals(contact.Actor) ||
                    contact.Branch != RallyPlanBranchV3.Primary ||
                    contact.EnvelopeIdentity != State.Plan.SelectedAction.EnvelopeIdentity ||
                    contact.TrajectoryArtifactIdentity != State.Plan.SelectedAction.TrajectoryArtifactIdentity)
                    throw new InvalidOperationException("Attack evidence must exactly match the committed Gate I attack.");
                return;
            }
            var responsibility = State.Plan.Defense.Responsibilities.SingleOrDefault(x =>
                x.Actor.Equals(contact.Actor) && x.Branch == contact.Branch);
            var attackCoverage = State.Plan.AttackCoverageResponsibilities
                .SingleOrDefault(x => x.Actor.Equals(contact.Actor) &&
                    x.Branch == contact.Branch);
            var kindMatches = contact.ActionKind == AttackDefenseCommandKind.BlockContact
                ? responsibility != null && (responsibility.Kind == DefenseResponsibilityKindV3.PrimaryBlock || responsibility.Kind == DefenseResponsibilityKindV3.SupportingBlock)
                : contact.ActionKind == AttackDefenseCommandKind.FloorDefense
                    ? responsibility != null
                    : contact.ActionKind == AttackDefenseCommandKind.AttackCover
                        ? attackCoverage != null
                        : false;
            var expectedEnvelope = _committedDefenseExecutions.TryGetValue(
                DefenseExecutionKey(contact.Actor, contact.ActionKind, contact.Branch),
                out var committedExecution)
                ? committedExecution.ExecutionClassification.ExecutableEnvelope.Identity
                : "gate-i-" + State.Revision + "-" + (int)contact.ActionKind + "-" + contact.Actor.Value;
            var incidental = contact.ActionKind ==
                AttackDefenseCommandKind.FloorDefense &&
                contact.CoverageReason ==
                    PlanCoverageReason.ResponsibleActorChanged;
            if (!kindMatches || (!incidental &&
                (contact.EnvelopeIdentity != expectedEnvelope ||
                 contact.TrajectoryArtifactIdentity !=
                    _intent.TrajectoryArtifact.ArtifactIdentity)) ||
                (incidental && (string.IsNullOrWhiteSpace(contact.EnvelopeIdentity) ||
                 string.IsNullOrWhiteSpace(contact.TrajectoryArtifactIdentity))))
                throw new InvalidOperationException("Defense evidence must exactly match a committed responsibility.");
        }

        private static string DefenseExecutionKey(PlayerId actor,
            AttackDefenseCommandKind kind, RallyPlanBranchV3 branch) =>
            actor.Value + ":" + (int)kind + ":" + (int)branch;

        private bool MatchesToolRecoveryExecution(
            AttackDefenseCommandExecutionV4 actual)
        {
            var expected = _toolRecoveryReceiveExecution;
            return actual != null && expected != null &&
                actual.ScheduledSimulationTime.Equals(expected.ScheduledSimulationTime) &&
                actual.MovementStartSimulationTime.Equals(expected.MovementStartSimulationTime) &&
                actual.ContactGroupId == expected.ContactGroupId &&
                actual.MovementTarget.Equals(expected.MovementTarget) &&
                Nullable.Equals(actual.PhysicalContactCenter, expected.PhysicalContactCenter) &&
                actual.ExecutionClassification.ExecutableEnvelope.Identity ==
                    expected.ExecutionClassification.ExecutableEnvelope.Identity &&
                actual.TrajectoryArtifact.ArtifactIdentity ==
                    expected.TrajectoryArtifact.ArtifactIdentity;
        }

        private AttackDefenseCommandExecutionV4 ExecutionFor(PlayerId actor, AttackDefenseCommandKind kind,
            string defenseZone = null, DefenseResponsibilityKindV3? responsibilityKind = null,
            int supportingBlockIndex = 0)
        {
            if (_players == null || !_players.TryGetValue(actor, out var player))
                throw new InvalidOperationException("Every command actor must have immutable Gate I execution inputs.");
            if (kind == AttackDefenseCommandKind.AttackContact)
            {
                var candidate = State.Plan?.SelectedAction ?? throw new InvalidOperationException(
                    "Final attack execution requires the committed selected candidate.");
                var exact = _attack.EvidenceFor(candidate);
                // The attacker approaches the set's contact point, never the
                // candidate's far-side landing target.  Sending it to the latter
                // made the formal player miss the immutable third-touch window.
                var takeoff = new SimVector3(candidate.ContactCenter.X,
                    player.WorldPosition.Y, candidate.ContactCenter.Z);
                var approach = new AttackApproachPlan(player.WorldPosition, takeoff,
                    (takeoff - player.WorldPosition).Magnitude, .8f, 0f);
                var attackContact = AttackContactPlanner.Plan(new AttackContactInput(
                    _intent.Target.Y, .8f, 1f, SetQualityGrade.A, takeoff, .6f, 1f));
                var scheduled = _intent.AttackReadyArrivalTime;
                var approachLead = Math.Min(.6f, Math.Max(.05f, scheduled));
                return new AttackDefenseCommandExecutionV4(scheduled,
                    scheduled - approachLead, default,
                    ContactGroupFor(State.Revision, AttackDefenseCommandKind.AttackContact),
                    exact.ExecutionClassification, exact.TrajectoryArtifact,
                    takeoff, approach, attackContact);
            }
            var category = kind == AttackDefenseCommandKind.AttackContact ? ExecutionCandidateCategoryV4.Attack :
                kind == AttackDefenseCommandKind.BlockContact ? ExecutionCandidateCategoryV4.Block : ExecutionCandidateCategoryV4.Receive;
            // Block responsibility is an already-public joint-defense fact.
            // Reserve a net-corridor root from that zone, rather than leaving
            // a scheduled block at its formation snapshot or deriving a target
            // from the hidden final attack route.
            var target = player.WorldPosition;
            if (kind == AttackDefenseCommandKind.BlockContact)
            {
                var x = string.Equals(defenseZone, "Line", StringComparison.Ordinal)
                    ? -1f
                    : string.Equals(defenseZone, "Cross", StringComparison.Ordinal)
                        ? 1f
                        : 0f;
                if (responsibilityKind == DefenseResponsibilityKindV3.SupportingBlock)
                    x += supportingBlockIndex % 2 == 0 ? -.45f : .45f;
                var z = player.Side == TeamSide.Home ? -.35f : .35f;
                target = new SimVector3(x, player.WorldPosition.Y, z);
            }
            var envelope = ExecutionEnvelopeFactoryV4.Create(player.Attributes,
                new ExecutionIntentV4("gate-i-" + State.Revision + "-" + (int)kind + "-" + actor.Value, category, target,
                    new SimVector3(0f, 1f, 1f), .5f),
                "gate-i-" + State.Revision + "-" + (int)kind + "-" + actor.Value, ExecutionEnvelopePolicyV4.GateI);
            var sample = new ExecutionSampleV4(envelope.Identity, envelope.Sampling.SamplingKey, category,
                envelope.BaselineTarget, envelope.BaselineVelocity, envelope.RequestedEffort);
            // Defense commitment reserves movement immediately, but its physical
            // contact must follow the final attack launch.  A defense contact at
            // Set+epsilon can otherwise become an illegal third rally touch.
            var defenseTime = Math.Max(_intent.AttackReadyArrivalTime + .01f,
                _intent.GateHExpectedContactTime + .25f);
            if (kind == AttackDefenseCommandKind.BlockContact)
            {
                var publishedArrival = State.Plan.PublicThreat.Entries
                    .Where(entry => string.Equals(entry.Zone, defenseZone,
                        StringComparison.Ordinal))
                    .Select(entry => entry.ArrivalTime)
                    // A middle-only threat still needs a deterministic block
                    // window for a line/cross responsibility: use the earliest
                    // already-public arrival, never a hidden final route.
                    .DefaultIfEmpty(State.Plan.PublicThreat.Entries
                        .Select(entry => entry.ArrivalTime).Min())
                    .Min();
                defenseTime = Math.Max(_intent.AttackReadyArrivalTime + .01f,
                    publishedArrival);
            }
            // A committed block unit shares one V3 physical-contact group, but
            // it must never reuse the attack group's identity or a later Gate-I
            // opportunity's identity.
            var contactGroup = ContactGroupFor(State.Revision, kind);
            return new AttackDefenseCommandExecutionV4(defenseTime, 0f, default, contactGroup,
                envelope.Classify(sample), _intent.TrajectoryArtifact, target);
        }

        private static int ContactGroupFor(long revision, AttackDefenseCommandKind kind)
        {
            const int baseGroup = 1000000000;
            const int revisionStride = 16;
            if (revision < 0 || revision > (int.MaxValue - baseGroup) / revisionStride)
                throw new InvalidOperationException("Gate I contact-group revision exceeds the deterministic range.");
            var kindCode = kind == AttackDefenseCommandKind.AttackContact ? 1 :
                kind == AttackDefenseCommandKind.BlockContact ? 2 :
                kind == AttackDefenseCommandKind.FloorDefense ? 3 : 4;
            return checked(baseGroup + ((int)revision * revisionStride) + kindCode);
        }
    }
}
