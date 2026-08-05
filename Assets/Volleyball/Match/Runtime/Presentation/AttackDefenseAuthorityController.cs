using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation
{
    public sealed class ToolRecoveryActualObservationV3
    {
        public ToolRecoveryActualObservationV3(TeamSide reboundSide,
            string reboundTrajectoryArtifactIdentity, string reboundSampleIdentity,
            string blockContactIdentity, int remainingTouches)
        {
            ReboundSide = reboundSide;
            ReboundTrajectoryArtifactIdentity = string.IsNullOrWhiteSpace(reboundTrajectoryArtifactIdentity)
                ? throw new ArgumentException("Identity is required.", nameof(reboundTrajectoryArtifactIdentity))
                : reboundTrajectoryArtifactIdentity;
            ReboundSampleIdentity = string.IsNullOrWhiteSpace(reboundSampleIdentity) ? throw new ArgumentException("Identity is required.", nameof(reboundSampleIdentity)) : reboundSampleIdentity;
            BlockContactIdentity = string.IsNullOrWhiteSpace(blockContactIdentity) ? throw new ArgumentException("Identity is required.", nameof(blockContactIdentity)) : blockContactIdentity;
            if (remainingTouches < 0 || remainingTouches > 3) throw new ArgumentOutOfRangeException(nameof(remainingTouches));
            RemainingTouches = remainingTouches;
        }
        public TeamSide ReboundSide { get; } public string ReboundTrajectoryArtifactIdentity { get; }
        public string ReboundSampleIdentity { get; } public string BlockContactIdentity { get; }
        public int RemainingTouches { get; }
    }

    public sealed class AttackDefenseAuthorityReceipt
    {
        public AttackDefenseAuthorityReceipt(long planRevision, long sourceSequence,
            AttackDefenseAuthorityPhaseV3 phase, AttackDefenseCommandKind kind,
            StablePlayerId actor, RallyPlanBranchV3 branch,
            ExecutionSampleClassificationV4 executionClassification,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact,
            AttackDefenseAuthorityEvidenceV3 evidence,
            ToolRecoveryActualObservationV3 toolRecoveryActualObservation = null,
            AttackDefenseCommandExecutionV4 execution = null,
            PerceptionReceiptV3 perception = null)
        {
            PlanRevision = planRevision;
            SourceSequence = sourceSequence;
            Phase = phase;
            Kind = kind;
            Actor = actor;
            Branch = branch;
            ExecutionClassification = executionClassification;
            TrajectoryArtifact = trajectoryArtifact;
            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
            ToolRecoveryActualObservation = toolRecoveryActualObservation;
            Execution = execution;
            Perception = perception;
        }

        public long PlanRevision { get; }
        public long SourceSequence { get; }
        public AttackDefenseAuthorityPhaseV3 Phase { get; }
        public AttackDefenseCommandKind Kind { get; }
        public StablePlayerId Actor { get; }
        public RallyPlanBranchV3 Branch { get; }
        public ExecutionSampleClassificationV4 ExecutionClassification { get; }
        public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
        public AttackDefenseAuthorityEvidenceV3 Evidence { get; }
        public ToolRecoveryActualObservationV3 ToolRecoveryActualObservation { get; }
        public AttackDefenseCommandExecutionV4 Execution { get; }
        public PerceptionReceiptV3 Perception { get; }
    }

    // Gate I starts after Gate H's Set command.  This controller therefore owns
    // no Set command surface; it only realizes already-approved post-Set work.
    public sealed class AttackDefenseAuthorityController :
        IAttackDefenseAuthorityCommandSink
    {
        private readonly IReadOnlyDictionary<StablePlayerId, PrototypePlayerAgent>
            _players;
        private readonly HashSet<CommandActorIdentity> _committed =
            new HashSet<CommandActorIdentity>();
        private readonly Dictionary<GateICommandIdentity, ScheduledGateICommand> _scheduled =
            new Dictionary<GateICommandIdentity, ScheduledGateICommand>();
        private readonly Dictionary<StablePlayerId, GateICommandIdentity> _latestScheduledByActor =
            new Dictionary<StablePlayerId, GateICommandIdentity>();
        private long _latestRevision = -1;
        private long _latestSourceSequence = -1;

        public AttackDefenseAuthorityController(
            IReadOnlyList<PrototypePlayerAgent> formalPlayers)
        {
            if (formalPlayers == null) throw new ArgumentNullException(nameof(formalPlayers));
            if (formalPlayers.Count != 6)
                throw new ArgumentException("Formal Gate I authority requires exactly six players.", nameof(formalPlayers));
            var players = new Dictionary<StablePlayerId, PrototypePlayerAgent>();
            foreach (var player in formalPlayers)
            {
                if (player == null || !players.TryAdd(player.StableId, player))
                    throw new ArgumentException("Formal players require distinct initialized runtime IDs.", nameof(formalPlayers));
            }
            _players = new ReadOnlyDictionary<StablePlayerId, PrototypePlayerAgent>(players);
        }

        public event Action<AttackDefenseAuthorityReceipt> AuthorityCommitted;

        public void Publish(AttackDefenseCommandBatch batch) => PreflightAndCommit(batch);

        public AttackDefenseAuthorityReceipt PreflightAndCommit(
            AttackDefenseCommandBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            ValidateBatchIdentity(batch);
            var prepared = new PreparedCommand[batch.Commands.Count];
            for (var index = 0; index < prepared.Length; index++)
                prepared[index] = Preflight(batch, batch.Commands[index]);

            var applied = new List<PreparedCommand>();
            var receipts = new List<AttackDefenseAuthorityReceipt>();
            try
            {
                foreach (var command in prepared)
                {
                    Apply(command);
                    applied.Add(command);
                    receipts.Add(Receipt(command, batch.Evidence));
                }
            }
            catch
            {
                // A committed action may already have crossed a physical/rules
                // boundary.  Only revert mutations explicitly marked uncommitted.
                for (var index = applied.Count - 1; index >= 0; index--)
                    if (!applied[index].Command.IsCommitted)
                        applied[index].Player.CancelScheduledContact();
                throw;
            }

            foreach (var command in prepared)
                if (command.Command.Kind ==
                    AttackDefenseCommandKind.InvalidateCommitted)
                {
                    _committed.Remove(new CommandActorIdentity(
                        batch.Evidence.PlanRevision,
                        command.Command.Actor));
                }
                else if (command.Command.IsCommitted)
                    _committed.Add(new CommandActorIdentity(
                        batch.Evidence.PlanRevision, command.Command.Actor));
                else if (command.Command.Kind != AttackDefenseCommandKind.CancelUncommitted)
                {
                    var identity = new GateICommandIdentity(command.Command);
                    _scheduled[identity] = new ScheduledGateICommand(command.Player, command.Command.IsCommitted);
                    _latestScheduledByActor[command.Command.Actor] = identity;
                }
                else if (command.CancellationTarget.HasValue)
                {
                    _scheduled.Remove(command.CancellationTarget.Value);
                    _latestScheduledByActor.Remove(command.Command.Actor);
                }
            _latestRevision = Math.Max(_latestRevision, batch.Evidence.PlanRevision);
            _latestSourceSequence = batch.Evidence.SourceSequence;
            foreach (var receipt in receipts) AuthorityCommitted?.Invoke(receipt);
            return receipts.FirstOrDefault();
        }

        private void ValidateBatchIdentity(AttackDefenseCommandBatch batch)
        {
            if (batch.Evidence.PlanRevision < _latestRevision ||
                batch.Evidence.SourceSequence <= _latestSourceSequence)
                throw new InvalidOperationException("Authority batch revision or source sequence is stale.");
            if (batch.Commands.Count == 0)
                throw new InvalidOperationException("Authority batch must contain a command.");
        }

        private PreparedCommand Preflight(AttackDefenseCommandBatch batch,
            AttackDefenseAuthorityCommand command)
        {
            if (command == null) throw new InvalidOperationException("Authority commands cannot be null.");
            if (command.PlanRevision != batch.Evidence.PlanRevision ||
                command.SourceSequence != batch.Evidence.SourceSequence)
                throw new InvalidOperationException("Command identity must match immutable batch evidence.");
            if (!_players.TryGetValue(command.Actor, out var player))
                throw new InvalidOperationException("Authority command actor is outside the owned formal six.");
            if (!IsAllowedInPhase(batch.Evidence.Phase, command.Kind))
                throw new InvalidOperationException("Command kind is incompatible with the authority phase.");
            ValidateDeclaredActor(batch.Evidence.Plan, command);
            if (command.Kind ==
                AttackDefenseCommandKind.InvalidateCommitted)
            {
                if (command.CancelTargetKind !=
                        AttackDefenseCommandKind.AttackContact ||
                    command.CancelTargetSourceSequence !=
                    _latestSourceSequence ||
                    !_committed.Contains(new CommandActorIdentity(
                        command.PlanRevision,
                        command.Actor)))
                    throw new InvalidOperationException(
                        "Committed invalidation requires the latest exact attack command.");
                return new PreparedCommand(command, player);
            }
            if (command.Kind == AttackDefenseCommandKind.CancelUncommitted)
            {
                if (!command.CancelTargetKind.HasValue || command.CancelTargetSourceSequence < 0)
                    throw new InvalidOperationException("Gate I cancellation requires an exact command identity.");
                var target = new GateICommandIdentity(command.PlanRevision,
                    command.CancelTargetSourceSequence, command.CancelTargetKind.Value,
                    command.Actor, command.Branch);
                if (!_scheduled.TryGetValue(target, out var scheduled))
                {
                    if (_committed.Contains(new CommandActorIdentity(command.PlanRevision, command.Actor)))
                        throw new InvalidOperationException("A committed authority command cannot be canceled.");
                    throw new InvalidOperationException("Cancellation may only target a live Gate I command.");
                }
                if (scheduled.IsCommitted)
                    throw new InvalidOperationException("A committed authority command cannot be canceled.");
                if (!_latestScheduledByActor.TryGetValue(command.Actor, out var latest) ||
                    !latest.Equals(target))
                    throw new InvalidOperationException("Cancellation cannot erase a newer scheduled contact.");
                return new PreparedCommand(command, player,
                    cancellationTarget: target);
            }

            var execution = command.Execution ?? throw new InvalidOperationException(
                "Post-Set authority commands require immutable execution inputs.");
            if (execution.MovementStartSimulationTime > execution.ScheduledSimulationTime)
                throw new InvalidOperationException("Movement cannot begin after the scheduled command.");
            var action = ActionFor(command.Kind);
            if (IsContact(command.Kind))
                player.ValidateGateIContact(action, execution.ExecutionClassification,
                    execution.TrajectoryArtifact, execution.AttackApproach,
                    execution.AttackContactPlan);
            else
                player.ValidateGateISupport(action, execution.ScheduledSimulationTime,
                    ToUnity(execution.MovementTarget));
            ValidateExactPlanEvidence(batch.Evidence.Plan, command, execution);
            return new PreparedCommand(command, player);
        }

        private static bool IsAllowedInPhase(AttackDefenseAuthorityPhaseV3 phase,
            AttackDefenseCommandKind kind) => phase switch
        {
            AttackDefenseAuthorityPhaseV3.AttackPlanned =>
                kind == AttackDefenseCommandKind.AttackPreparation || kind == AttackDefenseCommandKind.CancelUncommitted,
            AttackDefenseAuthorityPhaseV3.DefenseCommitted =>
                kind == AttackDefenseCommandKind.BlockContact || kind == AttackDefenseCommandKind.FloorDefense ||
                kind == AttackDefenseCommandKind.AttackCover || kind == AttackDefenseCommandKind.CancelUncommitted,
            AttackDefenseAuthorityPhaseV3.AttackCommitted =>
                kind == AttackDefenseCommandKind.AttackContact ||
                kind == AttackDefenseCommandKind.CancelUncommitted ||
                kind == AttackDefenseCommandKind.InvalidateCommitted,
            AttackDefenseAuthorityPhaseV3.AwaitingActualContact =>
                kind == AttackDefenseCommandKind.FloorDefense ||
                kind == AttackDefenseCommandKind.AttackCover ||
                kind == AttackDefenseCommandKind.CancelUncommitted,
            AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive =>
                kind == AttackDefenseCommandKind.AttackCover || kind == AttackDefenseCommandKind.CancelUncommitted,
            AttackDefenseAuthorityPhaseV3.ReorganizationPlanned =>
                kind == AttackDefenseCommandKind.Reorganization ||
                kind == AttackDefenseCommandKind.FloorDefense ||
                kind == AttackDefenseCommandKind.AttackCover ||
                kind == AttackDefenseCommandKind.CancelUncommitted,
            _ => false
        };

        private static void ValidateDeclaredActor(AttackDefensePlanV3 plan,
            AttackDefenseAuthorityCommand command)
        {
            if (plan == null) return; // Task 7 defense batches precede final plan materialization.
            var declared = command.Kind switch
            {
                AttackDefenseCommandKind.AttackPreparation =>
                    plan.AttackCandidates.Any(x => x.Actor.Equals(command.Actor)),
                AttackDefenseCommandKind.AttackContact =>
                    plan.SelectedAction != null && plan.SelectedAction.Actor.Equals(command.Actor) &&
                    plan.SelectedAction.CandidateIdentity == command.CandidateIdentity,
                AttackDefenseCommandKind.BlockContact => plan.Defense.Responsibilities.Any(x =>
                    x.Actor.Equals(command.Actor) && x.Branch == command.Branch &&
                    (x.Kind == DefenseResponsibilityKindV3.PrimaryBlock || x.Kind == DefenseResponsibilityKindV3.SupportingBlock)),
                AttackDefenseCommandKind.FloorDefense or AttackDefenseCommandKind.AttackCover =>
                    command.Kind == AttackDefenseCommandKind.FloorDefense
                        ? plan.Defense.Responsibilities.Any(x =>
                            x.Actor.Equals(command.Actor) &&
                            x.Branch == command.Branch)
                        : plan.AttackCoverageResponsibilities.Any(x =>
                            x.Actor.Equals(command.Actor) &&
                            x.Branch == command.Branch) ||
                          plan.Defense.Responsibilities.Any(x =>
                              x.Actor.Equals(command.Actor) &&
                              x.Branch == command.Branch) ||
                          (plan.SelectedAction?.ToolRecoveryEvidence != null &&
                           plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor
                               .Equals(command.Actor)),
                AttackDefenseCommandKind.Reorganization => plan.ReorganizationExits.Any(x =>
                    x.Actor.Equals(command.Actor) && x.Identity == command.ReorganizationExitIdentity),
                AttackDefenseCommandKind.CancelUncommitted or
                    AttackDefenseCommandKind.InvalidateCommitted => true,
                _ => false
            };
            if (!declared) throw new InvalidOperationException("Authority actor is not declared by the immutable plan.");
        }

        private static void ValidateExactPlanEvidence(AttackDefensePlanV3 plan,
            AttackDefenseAuthorityCommand command, AttackDefenseCommandExecutionV4 execution)
        {
            if (plan == null) return;
            AttackCandidateV3 candidate = command.Kind == AttackDefenseCommandKind.AttackContact
                ? plan.SelectedAction
                : command.Kind == AttackDefenseCommandKind.AttackPreparation
                    ? plan.AttackCandidates.FirstOrDefault(x => x.Actor.Equals(command.Actor))
                    : null;
            if (candidate == null) return;
            if (command.Kind == AttackDefenseCommandKind.AttackContact &&
                command.CandidateIdentity != candidate.CandidateIdentity)
                throw new InvalidOperationException("Attack command must retain the selected candidate identity.");
            if (candidate.EnvelopeIdentity != execution.ExecutionClassification.ExecutableEnvelope.Identity ||
                candidate.TrajectoryArtifactIdentity != execution.TrajectoryArtifact.ArtifactIdentity)
                throw new InvalidOperationException("Execution evidence must retain the plan envelope and trajectory identities.");
        }

        private static bool IsContact(AttackDefenseCommandKind kind) =>
            kind == AttackDefenseCommandKind.AttackPreparation ||
            kind == AttackDefenseCommandKind.AttackContact ||
            kind == AttackDefenseCommandKind.BlockContact ||
            kind == AttackDefenseCommandKind.FloorDefense ||
            kind == AttackDefenseCommandKind.AttackCover;

        private static TechniqueAction ActionFor(AttackDefenseCommandKind kind) => kind switch
        {
            AttackDefenseCommandKind.AttackPreparation or AttackDefenseCommandKind.AttackContact => TechniqueAction.Attack,
            AttackDefenseCommandKind.BlockContact => TechniqueAction.Block,
            AttackDefenseCommandKind.FloorDefense or AttackDefenseCommandKind.AttackCover or AttackDefenseCommandKind.Reorganization => TechniqueAction.Receive,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        private static void Apply(PreparedCommand prepared)
        {
            var command = prepared.Command;
            var execution = command.Execution;
            switch (command.Kind)
            {
                case AttackDefenseCommandKind.AttackPreparation:
                    prepared.Player.ScheduleAttackPreparation(execution.ScheduledSimulationTime,
                        ToUnity(execution.MovementTarget), execution.MovementStartSimulationTime);
                    break;
                case AttackDefenseCommandKind.AttackContact:
                    prepared.Player.ScheduleContact(TechniqueAction.Attack,
                        execution.ScheduledSimulationTime, execution.ExecutionClassification,
                        execution.ExecutionError, execution.ContactGroupId,
                        attackApproach: execution.AttackApproach,
                        attackContactPlan: execution.AttackContactPlan,
                        movementTarget: ToUnity(execution.MovementTarget),
                        movementStartSimulationTime: execution.MovementStartSimulationTime,
                        trajectoryArtifact: execution.TrajectoryArtifact,
                        allowGateISoftAttack: true);
                    break;
                case AttackDefenseCommandKind.BlockContact:
                    prepared.Player.ScheduleBlockContact(execution.ScheduledSimulationTime,
                        ToUnity(execution.MovementTarget), execution.MovementStartSimulationTime,
                        execution.ExecutionClassification.ExecutableSample.Velocity,
                        execution.ContactGroupId, execution.ExecutionClassification,
                        execution.TrajectoryArtifact);
                    break;
                case AttackDefenseCommandKind.FloorDefense:
                case AttackDefenseCommandKind.AttackCover:
                    prepared.Player.ScheduleContact(TechniqueAction.Receive,
                        execution.ScheduledSimulationTime,
                        execution.ExecutionClassification,
                        execution.ExecutionError, execution.ContactGroupId,
                        plannedContactCenter: execution.PhysicalContactCenter,
                        movementTarget: ToUnity(execution.MovementTarget),
                        movementStartSimulationTime: execution.MovementStartSimulationTime,
                        trajectoryArtifact: execution.TrajectoryArtifact,
                        preservePlannedContactRoot: true);
                    break;
                case AttackDefenseCommandKind.Reorganization:
                    prepared.Player.ScheduleSupportAction(TechniqueAction.Receive,
                        execution.ScheduledSimulationTime, ToUnity(execution.MovementTarget),
                        execution.MovementStartSimulationTime);
                    break;
                case AttackDefenseCommandKind.CancelUncommitted:
                    // The target is a controller-owned Gate I identity.  The player
                    // is touched only after preflight has proved it still owns that
                    // identity, preventing Gate H/legacy contacts from being erased.
                    prepared.Player.CancelScheduledContact();
                    break;
                case AttackDefenseCommandKind.InvalidateCommitted:
                    // A real environment collision invalidated the exact
                    // accepted-set attack before physical contact.
                    prepared.Player.CancelScheduledContact();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private static AttackDefenseAuthorityReceipt Receipt(PreparedCommand prepared,
            AttackDefenseAuthorityEvidenceV3 evidence) => new AttackDefenseAuthorityReceipt(
            prepared.Command.PlanRevision, prepared.Command.SourceSequence, evidence.Phase,
            prepared.Command.Kind, prepared.Command.Actor, prepared.Command.Branch,
            prepared.Command.Execution?.ExecutionClassification,
            prepared.Command.Execution?.TrajectoryArtifact, evidence,
            execution: prepared.Command.Execution,
            perception: evidence.Perception);

        private static Vector3 ToUnity(SimVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);

        private readonly struct PreparedCommand
        {
            public PreparedCommand(AttackDefenseAuthorityCommand command, PrototypePlayerAgent player,
                GateICommandIdentity? cancellationTarget = null)
            { Command = command; Player = player; CancellationTarget = cancellationTarget; }
            public AttackDefenseAuthorityCommand Command { get; }
            public PrototypePlayerAgent Player { get; }
            public GateICommandIdentity? CancellationTarget { get; }
        }
        private readonly struct CommandActorIdentity : IEquatable<CommandActorIdentity>
        {
            public CommandActorIdentity(long revision, StablePlayerId actor) { Revision = revision; Actor = actor; }
            public long Revision { get; } public StablePlayerId Actor { get; }
            public bool Equals(CommandActorIdentity other) => Revision == other.Revision && Actor.Equals(other.Actor);
            public override bool Equals(object obj) => obj is CommandActorIdentity other && Equals(other);
            public override int GetHashCode() => (Revision.GetHashCode() * 397) ^ Actor.GetHashCode();
        }
        private readonly struct GateICommandIdentity : IEquatable<GateICommandIdentity>
        {
            public GateICommandIdentity(AttackDefenseAuthorityCommand command)
                : this(command.PlanRevision, command.SourceSequence, command.Kind,
                    command.Actor, command.Branch) { }
            public GateICommandIdentity(long revision, long sourceSequence,
                AttackDefenseCommandKind kind, StablePlayerId actor, RallyPlanBranchV3 branch)
            { Revision = revision; SourceSequence = sourceSequence; Kind = kind; Actor = actor; Branch = branch; }
            public long Revision { get; } public long SourceSequence { get; }
            public AttackDefenseCommandKind Kind { get; } public StablePlayerId Actor { get; }
            public RallyPlanBranchV3 Branch { get; }
            public bool Equals(GateICommandIdentity other) => Revision == other.Revision &&
                SourceSequence == other.SourceSequence && Kind == other.Kind && Actor.Equals(other.Actor) && Branch == other.Branch;
            public override bool Equals(object obj) => obj is GateICommandIdentity other && Equals(other);
            public override int GetHashCode() => (((Revision.GetHashCode() * 397) ^ SourceSequence.GetHashCode()) * 397) ^
                ((int)Kind * 17) ^ Actor.GetHashCode() ^ (int)Branch;
        }
        private readonly struct ScheduledGateICommand
        {
            public ScheduledGateICommand(PrototypePlayerAgent player, bool isCommitted)
            { Player = player; IsCommitted = isCommitted; }
            public PrototypePlayerAgent Player { get; } public bool IsCommitted { get; }
        }
    }
}
