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

namespace Volleyball.Presentation
{
    public sealed class ReceiveOrganizationAuthorityReceipt
    {
        public ReceiveOrganizationAuthorityReceipt(
            long planRevision,
            long sourceSequence,
            ReceiveOrganizationCommandKind kind,
            StablePlayerId actor,
            RallyPlanBranchV3 branch,
            TechniqueAction action,
            ExecutionSampleClassificationV4 executionClassification,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact,
            ReceiveOrganizationAuthorityEvidenceV3 evidence)
        {
            PlanRevision = planRevision;
            SourceSequence = sourceSequence;
            Kind = kind;
            Actor = actor;
            Branch = branch;
            Action = action;
            ExecutionClassification = executionClassification;
            TrajectoryArtifact = trajectoryArtifact;
            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        }

        public long PlanRevision { get; }

        public long SourceSequence { get; }

        public ReceiveOrganizationCommandKind Kind { get; }

        public StablePlayerId Actor { get; }

        public RallyPlanBranchV3 Branch { get; }

        public TechniqueAction Action { get; }

        public ExecutionSampleClassificationV4 ExecutionClassification { get; }

        public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }

        public ReceiveOrganizationAuthorityEvidenceV3 Evidence { get; }
    }

    public sealed class ReceiveOrganizationAuthorityController :
        IReceiveOrganizationAuthorityCommandSink
    {
        private readonly IReadOnlyDictionary<StablePlayerId, PrototypePlayerAgent>
            _players;
        private readonly HashSet<CommandActorIdentity> _committed =
            new HashSet<CommandActorIdentity>();
        private long _latestRevision = -1;
        private long _latestSourceSequence;

        public ReceiveOrganizationAuthorityController(
            IReadOnlyList<PrototypePlayerAgent> formalPlayers)
        {
            if (formalPlayers == null)
            {
                throw new ArgumentNullException(nameof(formalPlayers));
            }

            if (formalPlayers.Count != 6)
            {
                throw new ArgumentException(
                    "Formal Gate H authority requires exactly six players.",
                    nameof(formalPlayers));
            }

            var players = new Dictionary<StablePlayerId, PrototypePlayerAgent>();
            for (var index = 0; index < formalPlayers.Count; index++)
            {
                var player = formalPlayers[index] ??
                             throw new ArgumentException(
                                 "Formal players cannot contain null.",
                                 nameof(formalPlayers));
                if (string.IsNullOrWhiteSpace(player.StableId.Value) ||
                    !players.TryAdd(player.StableId, player))
                {
                    throw new ArgumentException(
                        "Formal players require distinct stable IDs.",
                        nameof(formalPlayers));
                }
            }

            _players =
                new ReadOnlyDictionary<StablePlayerId, PrototypePlayerAgent>(
                    players);
        }

        public event Action<ReceiveOrganizationAuthorityReceipt>
            AuthorityCommitted;

        public void Publish(ReceiveOrganizationCommandBatch batch)
        {
            PreflightAndCommit(batch);
        }

        public ReceiveOrganizationAuthorityReceipt PreflightAndCommit(
            ReceiveOrganizationCommandBatch batch)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            ValidateBatchIdentity(batch);
            var prepared = new PreparedCommand[batch.Commands.Count];
            for (var index = 0; index < batch.Commands.Count; index++)
            {
                prepared[index] = Preflight(batch, batch.Commands[index]);
            }

            var applied = new List<PreparedCommand>();
            var receipts = new List<ReceiveOrganizationAuthorityReceipt>();
            try
            {
                for (var index = 0; index < prepared.Length; index++)
                {
                    Apply(prepared[index], batch.Evidence);
                    applied.Add(prepared[index]);
                    var receipt = Receipt(prepared[index], batch.Evidence);
                    receipts.Add(receipt);
                }
            }
            catch
            {
                for (var index = applied.Count - 1; index >= 0; index--)
                {
                    if (!applied[index].Command.IsCommitted)
                    {
                        applied[index].Player.CancelScheduledContact();
                    }
                }

                throw;
            }

            for (var index = 0; index < prepared.Length; index++)
            {
                if (prepared[index].Command.IsCommitted)
                {
                    _committed.Add(new CommandActorIdentity(
                        batch.PlanRevision,
                        prepared[index].Command.Actor));
                }
            }

            _latestRevision = Math.Max(_latestRevision, batch.PlanRevision);
            _latestSourceSequence = batch.SourceSequence;
            for (var index = 0; index < receipts.Count; index++)
            {
                AuthorityCommitted?.Invoke(receipts[index]);
            }

            return receipts.FirstOrDefault(receipt =>
                       receipt.Kind ==
                       ReceiveOrganizationCommandKind.PrimaryReceive ||
                       receipt.Kind ==
                       ReceiveOrganizationCommandKind.OrganizationContact) ??
                   receipts.FirstOrDefault();
        }

        private void ValidateBatchIdentity(
            ReceiveOrganizationCommandBatch batch)
        {
            if (batch.PlanRevision < _latestRevision)
            {
                throw new InvalidOperationException(
                    "Authority batches cannot move to an older revision.");
            }

            if (batch.SourceSequence <= _latestSourceSequence)
            {
                throw new InvalidOperationException(
                    "Authority batches cannot repeat a stale source sequence.");
            }
        }

        private PreparedCommand Preflight(
            ReceiveOrganizationCommandBatch batch,
            ReceiveOrganizationAuthorityCommand command)
        {
            if (!_players.TryGetValue(command.Actor, out var player))
            {
                throw new InvalidOperationException(
                    "Authority command actor is outside the owned formal six.");
            }

            ValidateDeclaredActor(batch.Evidence.Plan, command);
            var action = ActionFor(command.Kind);
            switch (command.Kind)
            {
                case ReceiveOrganizationCommandKind.PrimaryReceive:
                case ReceiveOrganizationCommandKind.OrganizationContact:
                    ValidateContact(command, player, action);
                    break;
                case ReceiveOrganizationCommandKind.EmergencyReceive:
                    RequireExecution(command);
                    break;
                case ReceiveOrganizationCommandKind.SetterPreparation:
                    RequireExecution(command);
                    break;
                case ReceiveOrganizationCommandKind.AttackPreparation:
                    RequireExecution(command);
                    if (!command.Decision.HasDecision ||
                        !command.Decision.AttackApproach.HasValue)
                    {
                        throw new InvalidOperationException(
                            "Attack preparation requires the declared approach decision.");
                    }

                    ValidateRuntimeActor(command, player);
                    break;
                case ReceiveOrganizationCommandKind.CancelUncommitted:
                    if (_committed.Contains(new CommandActorIdentity(
                            batch.PlanRevision,
                            command.Actor)))
                    {
                        throw new InvalidOperationException(
                            "A committed authority command cannot be canceled.");
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command.Kind));
            }

            return new PreparedCommand(command, player, action);
        }

        private static void ValidateDeclaredActor(
            ReceiveOrganizationPlanV3 plan,
            ReceiveOrganizationAuthorityCommand command)
        {
            var declared = command.Kind switch
            {
                ReceiveOrganizationCommandKind.PrimaryReceive =>
                    command.Actor.Equals(plan.PrimaryReceiver),
                ReceiveOrganizationCommandKind.EmergencyReceive =>
                    plan.EmergencyReceivers.Contains(command.Actor),
                ReceiveOrganizationCommandKind.SetterPreparation =>
                    command.Actor.Equals(plan.RegisteredSetter),
                ReceiveOrganizationCommandKind.OrganizationContact =>
                    command.Actor.Equals(plan.RegisteredSetter) ||
                    plan.BackupOrganizers.Contains(command.Actor),
                ReceiveOrganizationCommandKind.AttackPreparation =>
                    command.Actor.Equals(plan.AttackPreparation),
                ReceiveOrganizationCommandKind.CancelUncommitted => true,
                _ => false
            };
            if (!declared)
            {
                throw new InvalidOperationException(
                    "Authority command actor is not declared for this command kind.");
            }
        }

        private static void ValidateContact(
            ReceiveOrganizationAuthorityCommand command,
            PrototypePlayerAgent player,
            TechniqueAction action)
        {
            var execution = RequireExecution(command);
            if (execution.ExecutionClassification == null ||
                execution.TrajectoryArtifact == null)
            {
                throw new InvalidOperationException(
                    "Formal contact commands require V4 classification and trajectory evidence.");
            }

            if (!command.Decision.HasDecision ||
                command.Decision.Action != action)
            {
                throw new InvalidOperationException(
                    "Formal contact command decision does not match its kind.");
            }

            ValidateRuntimeActor(command, player);
            player.ValidateV4Schedule(
                action,
                execution.ExecutionClassification,
                command.Decision.AttackApproach,
                command.Decision.AttackContactPlan);
            if (string.IsNullOrWhiteSpace(
                    execution.TrajectoryArtifact.ArtifactIdentity))
            {
                throw new InvalidOperationException(
                    "Trajectory artifact identity is required.");
            }
        }

        private static void ValidateRuntimeActor(
            ReceiveOrganizationAuthorityCommand command,
            PrototypePlayerAgent player)
        {
            if (!command.Decision.Actor.Equals(player.Id))
            {
                throw new InvalidOperationException(
                    "Stable command actor and runtime decision actor must resolve to the same player.");
            }
        }

        private static ReceiveOrganizationCommandExecutionV4 RequireExecution(
            ReceiveOrganizationAuthorityCommand command)
        {
            return command.Execution ??
                   throw new InvalidOperationException(
                       "The authority command requires immutable execution inputs.");
        }

        private static TechniqueAction ActionFor(
            ReceiveOrganizationCommandKind kind)
        {
            return kind switch
            {
                ReceiveOrganizationCommandKind.PrimaryReceive =>
                    TechniqueAction.Receive,
                ReceiveOrganizationCommandKind.EmergencyReceive =>
                    TechniqueAction.Receive,
                ReceiveOrganizationCommandKind.SetterPreparation =>
                    TechniqueAction.Set,
                ReceiveOrganizationCommandKind.OrganizationContact =>
                    TechniqueAction.Set,
                ReceiveOrganizationCommandKind.AttackPreparation =>
                    TechniqueAction.Attack,
                ReceiveOrganizationCommandKind.CancelUncommitted =>
                    TechniqueAction.Receive,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        private static void Apply(
            PreparedCommand prepared,
            ReceiveOrganizationAuthorityEvidenceV3 evidence)
        {
            var command = prepared.Command;
            var player = prepared.Player;
            var execution = command.Execution;
            switch (command.Kind)
            {
                case ReceiveOrganizationCommandKind.PrimaryReceive:
                case ReceiveOrganizationCommandKind.OrganizationContact:
                    var movementTarget = ToUnity(command.Decision.MovementTarget);
                    if (prepared.Action == TechniqueAction.Receive)
                    {
                        var contactCenter =
                            execution.PlannedContactCenter ??
                            command.Decision.ContactTarget;
                        movementTarget = player.ResolveContactRootTarget(
                            prepared.Action,
                            contactCenter,
                            movementTarget);
                    }

                    player.ScheduleContact(
                        prepared.Action,
                        execution.ScheduledSimulationTime,
                        execution.ExecutionClassification,
                        execution.ExecutionError,
                        execution.ContactGroupId,
                        execution.PlannedContactCenter ??
                        command.Decision.ContactTarget,
                        movementTarget: movementTarget,
                        movementStartSimulationTime:
                        execution.MovementStartSimulationTime,
                        attackApproach: command.Decision.AttackApproach,
                        attackContactPlan: command.Decision.AttackContactPlan,
                        trajectoryArtifact: execution.TrajectoryArtifact);
                    break;
                case ReceiveOrganizationCommandKind.EmergencyReceive:
                    player.EnableEmergencyReceiveWindow(
                        execution.EmergencyWindowStart,
                        execution.EmergencyWindowEnd,
                        execution.EmergencyTargetVelocity,
                        execution.ContactGroupId);
                    break;
                case ReceiveOrganizationCommandKind.SetterPreparation:
                    player.ScheduleSetPreparation(
                        execution.ScheduledSimulationTime,
                        ToUnity(evidence.Plan.OrganizationTarget),
                        execution.MovementStartSimulationTime);
                    break;
                case ReceiveOrganizationCommandKind.AttackPreparation:
                    player.ScheduleAttackPreparation(
                        execution.ScheduledSimulationTime,
                        ToUnity(
                            command.Decision.AttackApproach.Value.ApproachStart),
                        execution.MovementStartSimulationTime);
                    break;
                case ReceiveOrganizationCommandKind.CancelUncommitted:
                    player.CancelScheduledContact();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static ReceiveOrganizationAuthorityReceipt Receipt(
            PreparedCommand prepared,
            ReceiveOrganizationAuthorityEvidenceV3 evidence)
        {
            return new ReceiveOrganizationAuthorityReceipt(
                prepared.Command.PlanRevision,
                prepared.Command.SourceSequence,
                prepared.Command.Kind,
                prepared.Command.Actor,
                prepared.Command.Branch,
                prepared.Action,
                prepared.Command.Execution?.ExecutionClassification,
                prepared.Command.Execution?.TrajectoryArtifact,
                evidence);
        }

        private static Vector3 ToUnity(SimVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private readonly struct PreparedCommand
        {
            public PreparedCommand(
                ReceiveOrganizationAuthorityCommand command,
                PrototypePlayerAgent player,
                TechniqueAction action)
            {
                Command = command;
                Player = player;
                Action = action;
            }

            public ReceiveOrganizationAuthorityCommand Command { get; }

            public PrototypePlayerAgent Player { get; }

            public TechniqueAction Action { get; }
        }

        private readonly struct CommandActorIdentity :
            IEquatable<CommandActorIdentity>
        {
            public CommandActorIdentity(long revision, StablePlayerId actor)
            {
                Revision = revision;
                Actor = actor;
            }

            public long Revision { get; }

            public StablePlayerId Actor { get; }

            public bool Equals(CommandActorIdentity other)
            {
                return Revision == other.Revision && Actor.Equals(other.Actor);
            }

            public override bool Equals(object obj)
            {
                return obj is CommandActorIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Revision.GetHashCode() * 397) ^ Actor.GetHashCode();
                }
            }
        }
    }
}
