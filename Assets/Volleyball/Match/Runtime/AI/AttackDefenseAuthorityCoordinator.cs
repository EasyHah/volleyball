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
        AttackCommitted, AwaitingActualContact, ReorganizationPlanned, HandedOff, Terminal
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
        {
            if (planRevision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(planRevision < 0 ? nameof(planRevision) : nameof(sourceSequence));
            if (!Enum.IsDefined(typeof(PlanCoverageReason), coverageReason)) throw new ArgumentOutOfRangeException(nameof(coverageReason));
            PlanRevision = planRevision; SourceSequence = sourceSequence; Actor = actor;
            CoverageReason = coverageReason; ReorganizationExitIdentity = reorganizationExitIdentity ?? string.Empty;
        }
        public long PlanRevision { get; } public long SourceSequence { get; } public PlayerId Actor { get; }
        public PlanCoverageReason CoverageReason { get; } public string ReorganizationExitIdentity { get; }
    }

    public sealed class AttackDefenseAuthorityCommand
    {
        public AttackDefenseAuthorityCommand(long planRevision, long sourceSequence, AttackDefenseCommandKind kind, PlayerId actor, bool isCommitted,
            AttackDefenseCommandExecutionV4 execution = null, RallyPlanBranchV3 branch = RallyPlanBranchV3.Primary)
        {
            if (planRevision < 0 || sourceSequence < 0) throw new ArgumentOutOfRangeException(planRevision < 0 ? nameof(planRevision) : nameof(sourceSequence));
            if (!Enum.IsDefined(typeof(AttackDefenseCommandKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(RallyPlanBranchV3), branch)) throw new ArgumentOutOfRangeException(nameof(branch));
            PlanRevision = planRevision; SourceSequence = sourceSequence; Kind = kind; Actor = actor; IsCommitted = isCommitted; Execution = execution; Branch = branch;
        }
        public long PlanRevision { get; } public long SourceSequence { get; } public AttackDefenseCommandKind Kind { get; }
        public PlayerId Actor { get; } public bool IsCommitted { get; }
        public AttackDefenseCommandExecutionV4 Execution { get; }
        public RallyPlanBranchV3 Branch { get; }
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
            AttackContactPlan? attackContactPlan = null)
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
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        private static bool Finite(SimVector3 value) => value.IsFinite;
    }

    public sealed class AttackDefenseAuthorityEvidenceV3
    {
        public AttackDefenseAuthorityEvidenceV3(long planRevision, long sourceSequence, AttackDefenseAuthorityPhaseV3 phase,
            AttackDefensePlanV3 plan, PlanCoverageDecision coverageDecision)
        { PlanRevision = planRevision; SourceSequence = sourceSequence; Phase = phase; Plan = plan; CoverageDecision = coverageDecision; }
        public long PlanRevision { get; } public long SourceSequence { get; } public AttackDefenseAuthorityPhaseV3 Phase { get; }
        public AttackDefensePlanV3 Plan { get; } public PlanCoverageDecision CoverageDecision { get; }
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
        private TeamSide _attackingSide;
        private long _lastSequence = -1;
        public AttackDefenseAuthorityCoordinator(AttackDefensePlanner planner, IAttackDefenseAuthorityCommandSink sink)
        { _planner = planner ?? throw new ArgumentNullException(nameof(planner)); _sink = sink ?? throw new ArgumentNullException(nameof(sink)); State = AttackDefenseAuthorityStateV3.Idle; }
        public AttackDefenseAuthorityStateV3 State { get; private set; }

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
            State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.AttackPlanned, State.Revision, _attackingSide, null, State.CoverageDecision);
            return State;
        }

        public AttackDefenseAuthorityStateV3 PublishThreat(long revision, long sourceSequence)
        { Require(AttackDefenseAuthorityPhaseV3.AttackPlanned, revision, sourceSequence); _lastSequence = sourceSequence; State = New(AttackDefenseAuthorityPhaseV3.ThreatPublished); return State; }

        public AttackDefenseAuthorityStateV3 CommitDefense(long revision, long sourceSequence, JointDefensePlanV3 defense)
        {
            Require(AttackDefenseAuthorityPhaseV3.ThreatPublished, revision, sourceSequence);
            if (defense == null || defense.SourceThreatIdentity != _attack.PublicThreat.ThreatIdentity) throw new InvalidOperationException("Defense must be composed from the published threat.");
            _defense = defense; _lastSequence = sourceSequence; State = New(AttackDefenseAuthorityPhaseV3.DefenseCommitted);
            Publish(sourceSequence, State, defense.Responsibilities.Select(x => new AttackDefenseAuthorityCommand(revision, sourceSequence,
                x.Kind == DefenseResponsibilityKindV3.PrimaryBlock || x.Kind == DefenseResponsibilityKindV3.SupportingBlock ? AttackDefenseCommandKind.BlockContact : AttackDefenseCommandKind.FloorDefense, x.Actor, true)));
            return State;
        }

        public AttackDefenseAuthorityStateV3 CommitFinalAttack(long revision, long sourceSequence)
        {
            Require(AttackDefenseAuthorityPhaseV3.DefenseCommitted, revision, sourceSequence);
            var selected = _planner.ChooseFinal(_attack, _defense).Candidate; _lastSequence = sourceSequence;
            var plan = new AttackDefensePlanV3(_attackingSide, revision, "gate-i-plan-" + revision, _intent, _attack.Candidates, _attack.PublicThreat, _defense, selected, _defense.ReorganizationExits);
            State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.AttackCommitted, revision, _attackingSide, plan, State.CoverageDecision);
            Publish(sourceSequence, State, new[] { new AttackDefenseAuthorityCommand(revision, sourceSequence, AttackDefenseCommandKind.AttackContact, selected.Actor, true) });
            return State;
        }

        public AttackDefenseAuthorityStateV3 AcceptContact(GateIContactEvidenceV3 contact)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));
            if (State.Phase != AttackDefenseAuthorityPhaseV3.AttackCommitted && State.Phase != AttackDefenseAuthorityPhaseV3.AwaitingActualContact) throw new InvalidOperationException("No Gate I contact is awaiting acceptance.");
            if (contact.PlanRevision != State.Revision || contact.SourceSequence <= _lastSequence) throw new InvalidOperationException("Stale or mismatched contact evidence.");
            var coverage = Coverage(contact.CoverageReason);
            if (coverage.Kind == PlanCoverageDecisionKind.TerminalNoPlan) { _lastSequence = contact.SourceSequence; State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.Terminal, State.Revision, _attackingSide, State.Plan, coverage); return State; }
            var exit = State.Plan.ReorganizationExits.OrderBy(x => x.Identity, StringComparer.Ordinal).FirstOrDefault(x => string.IsNullOrEmpty(contact.ReorganizationExitIdentity) || x.Identity == contact.ReorganizationExitIdentity);
            if (exit == null) throw new InvalidOperationException("Actual contact does not select a declared reorganization exit.");
            _lastSequence = contact.SourceSequence; State = new AttackDefenseAuthorityStateV3(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned, State.Revision, _attackingSide, State.Plan, coverage);
            Publish(contact.SourceSequence, State, new[] { new AttackDefenseAuthorityCommand(State.Revision, contact.SourceSequence, AttackDefenseCommandKind.Reorganization, exit.Actor, true) });
            return State;
        }

        private AttackDefenseAuthorityStateV3 New(AttackDefenseAuthorityPhaseV3 phase) => new AttackDefenseAuthorityStateV3(phase, State.Revision, _attackingSide, State.Plan, State.CoverageDecision);
        private void Require(AttackDefenseAuthorityPhaseV3 phase, long revision, long sourceSequence)
        { if (State.Phase != phase || revision != State.Revision || sourceSequence <= _lastSequence) throw new InvalidOperationException("Phase, revision, or source sequence is invalid."); }
        private void Publish(long sequence, AttackDefenseAuthorityStateV3 state, IEnumerable<AttackDefenseAuthorityCommand> commands) => _sink.Publish(new AttackDefenseCommandBatch(commands.ToArray(), new AttackDefenseAuthorityEvidenceV3(state.Revision, sequence, state.Phase, state.Plan, state.CoverageDecision)));
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
    }
}
