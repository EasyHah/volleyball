using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using RuntimePlayerId = Volleyball.Domain.Prototype.PlayerId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.AI
{
    public enum ReceiveOrganizationAuthorityPhaseV3
    {
        Idle,
        ReceivePlanned,
        ReceiveCommitted,
        OrganizationPlanned,
        OrganizationCommitted,
        HandedOffToAttack,
        Terminal
    }

    public enum ReceiveOrganizationCommandKind
    {
        PrimaryReceive,
        EmergencyReceive,
        SetterPreparation,
        OrganizationContact,
        AttackPreparation,
        CancelUncommitted
    }

    public sealed class ReceiveOrganizationCommandExecutionV4
    {
        public ReceiveOrganizationCommandExecutionV4(
            float scheduledSimulationTime,
            float movementStartSimulationTime,
            SkillExecutionError executionError,
            int contactGroupId,
            ExecutionSampleClassificationV4 executionClassification,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact,
            float emergencyWindowStart,
            float emergencyWindowEnd,
            SimVector3 emergencyTargetVelocity,
            SimVector3? plannedContactCenter = null)
        {
            ValidateFiniteNonNegative(
                scheduledSimulationTime,
                nameof(scheduledSimulationTime));
            ValidateFiniteNonNegative(
                movementStartSimulationTime,
                nameof(movementStartSimulationTime));
            if (contactGroupId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contactGroupId));
            }

            ValidateFiniteNonNegative(
                emergencyWindowStart,
                nameof(emergencyWindowStart));
            ValidateFiniteNonNegative(
                emergencyWindowEnd,
                nameof(emergencyWindowEnd));
            if (emergencyWindowEnd < emergencyWindowStart)
            {
                throw new ArgumentException(
                    "Emergency window end cannot precede its start.",
                    nameof(emergencyWindowEnd));
            }

            if (!emergencyTargetVelocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(emergencyTargetVelocity));
            }

            if (plannedContactCenter.HasValue &&
                !plannedContactCenter.Value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(plannedContactCenter));
            }

            ScheduledSimulationTime = scheduledSimulationTime;
            MovementStartSimulationTime = movementStartSimulationTime;
            ExecutionError = executionError;
            ContactGroupId = contactGroupId;
            ExecutionClassification = executionClassification;
            TrajectoryArtifact = trajectoryArtifact;
            EmergencyWindowStart = emergencyWindowStart;
            EmergencyWindowEnd = emergencyWindowEnd;
            EmergencyTargetVelocity = emergencyTargetVelocity;
            PlannedContactCenter = plannedContactCenter;
        }

        public float ScheduledSimulationTime { get; }

        public float MovementStartSimulationTime { get; }

        public SkillExecutionError ExecutionError { get; }

        public int ContactGroupId { get; }

        public ExecutionSampleClassificationV4 ExecutionClassification { get; }

        public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }

        public float EmergencyWindowStart { get; }

        public float EmergencyWindowEnd { get; }

        public SimVector3 EmergencyTargetVelocity { get; }

        public SimVector3? PlannedContactCenter { get; }

        private static void ValidateFiniteNonNegative(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class ReceiveOrganizationAuthorityCommand
    {
        public ReceiveOrganizationAuthorityCommand(
            long planRevision,
            long sourceSequence,
            ReceiveOrganizationCommandKind kind,
            StablePlayerId actor,
            RallyPlanBranchV3 branch,
            TeamRallyDecision decision,
            bool isCommitted,
            ReceiveOrganizationCommandExecutionV4 execution = null,
            GateISetIntentV3 gateISetIntent = null)
        {
            ValidateRevisionAndSequence(planRevision, sourceSequence);
            RequireDefined(kind, nameof(kind));
            RequireDefined(branch, nameof(branch));
            PlanRevision = planRevision;
            SourceSequence = sourceSequence;
            Kind = kind;
            Actor = RequirePlayer(actor, nameof(actor));
            Branch = branch;
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            IsCommitted = isCommitted;
            Execution = execution;
            GateISetIntent = gateISetIntent;
            if (gateISetIntent != null && kind != ReceiveOrganizationCommandKind.OrganizationContact)
                throw new ArgumentException("Only the Gate H OrganizationContact may carry a Gate I SetIntent.", nameof(gateISetIntent));
        }

        public long PlanRevision { get; }

        public long SourceSequence { get; }

        public ReceiveOrganizationCommandKind Kind { get; }

        public StablePlayerId Actor { get; }

        public RallyPlanBranchV3 Branch { get; }

        public TeamRallyDecision Decision { get; }

        public bool IsCommitted { get; }

        public ReceiveOrganizationCommandExecutionV4 Execution { get; }
        public GateISetIntentV3 GateISetIntent { get; }

        internal ReceiveOrganizationAuthorityCommand WithCommitted(
            long sourceSequence)
        {
            return new ReceiveOrganizationAuthorityCommand(
                PlanRevision,
                sourceSequence,
                Kind,
                Actor,
                Branch,
                Decision,
                true,
                Execution,
                GateISetIntent);
        }

        internal static void ValidateRevisionAndSequence(
            long revision,
            long sourceSequence)
        {
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            if (sourceSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSequence));
            }
        }

        internal static StablePlayerId RequirePlayer(
            StablePlayerId player,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(player.Value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return player;
        }

        internal static T RequireDefined<T>(T value, string parameterName)
            where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    public sealed class ReceiveOrganizationAuthorityEvidenceV3
    {
        public ReceiveOrganizationAuthorityEvidenceV3(
            long planRevision,
            long sourceSequence,
            ReceiveOrganizationAuthorityPhaseV3 phase,
            ReceiveOrganizationPlanV3 plan,
            SetterReachabilityEvidenceV3 setterEvidence,
            OrganizationFallbackReasonV3 fallbackReason,
            PlanCoverageDecision coverageDecision,
            SimVector3? actualFirstPassLanding,
            PerceptionReceiptV3 perception = null)
        {
            ReceiveOrganizationAuthorityCommand.ValidateRevisionAndSequence(
                planRevision,
                sourceSequence);
            ReceiveOrganizationAuthorityCommand.RequireDefined(
                phase,
                nameof(phase));
            ReceiveOrganizationAuthorityCommand.RequireDefined(
                fallbackReason,
                nameof(fallbackReason));
            if (actualFirstPassLanding.HasValue &&
                !actualFirstPassLanding.Value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actualFirstPassLanding));
            }

            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (plan.Revision != planRevision)
            {
                throw new ArgumentException(
                    "Evidence and plan revisions must match.",
                    nameof(plan));
            }

            PlanRevision = planRevision;
            SourceSequence = sourceSequence;
            Phase = phase;
            SetterEvidence = setterEvidence;
            FallbackReason = fallbackReason;
            CoverageDecision = coverageDecision ??
                               throw new ArgumentNullException(
                                   nameof(coverageDecision));
            if (!string.Equals(
                    CoverageDecision.PlanRevision,
                    planRevision.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Coverage and evidence revisions must match.",
                    nameof(coverageDecision));
            }

            ActualFirstPassLanding = actualFirstPassLanding;
            if (perception != null &&
                (perception.Revision != planRevision ||
                 perception.ObservingSide != plan.Side))
                throw new ArgumentException(
                    "Perception and authority evidence must identify the same revision and side.",
                    nameof(perception));
            Perception = perception;
        }

        public long PlanRevision { get; }

        public long SourceSequence { get; }

        public ReceiveOrganizationAuthorityPhaseV3 Phase { get; }

        public ReceiveOrganizationPlanV3 Plan { get; }

        public SetterReachabilityEvidenceV3 SetterEvidence { get; }

        public OrganizationFallbackReasonV3 FallbackReason { get; }

        public PlanCoverageDecision CoverageDecision { get; }

        public SimVector3? ActualFirstPassLanding { get; }
        public PerceptionReceiptV3 Perception { get; }
    }

    public sealed class ReceiveOrganizationCommandBatch
    {
        public ReceiveOrganizationCommandBatch(
            long planRevision,
            long sourceSequence,
            IReadOnlyList<ReceiveOrganizationAuthorityCommand> commands,
            ReceiveOrganizationAuthorityEvidenceV3 evidence)
        {
            ReceiveOrganizationAuthorityCommand.ValidateRevisionAndSequence(
                planRevision,
                sourceSequence);
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
            if (evidence.PlanRevision != planRevision ||
                evidence.SourceSequence != sourceSequence)
            {
                throw new ArgumentException(
                    "Batch and evidence identities must match.",
                    nameof(evidence));
            }

            var copy = new ReceiveOrganizationAuthorityCommand[commands.Count];
            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index] ??
                              throw new ArgumentException(
                                  "Commands cannot contain null.",
                                  nameof(commands));
                if (command.PlanRevision != planRevision ||
                    command.SourceSequence != sourceSequence)
                {
                    throw new ArgumentException(
                        "Every command must match the batch identity.",
                        nameof(commands));
                }

                copy[index] = command;
            }

            PlanRevision = planRevision;
            SourceSequence = sourceSequence;
            Commands =
                new ReadOnlyCollection<ReceiveOrganizationAuthorityCommand>(copy);
        }

        public long PlanRevision { get; }

        public long SourceSequence { get; }

        public IReadOnlyList<ReceiveOrganizationAuthorityCommand> Commands { get; }

        public ReceiveOrganizationAuthorityEvidenceV3 Evidence { get; }
    }

    public interface IReceiveOrganizationAuthorityCommandSink
    {
        void Publish(ReceiveOrganizationCommandBatch batch);
    }

    public sealed class AcceptedReceiveV3
    {
        public AcceptedReceiveV3(
            long revision,
            long sourceSequence,
            StablePlayerId actor,
            SimVector3 actualFirstPassLanding,
            PlanCoverageReason coverageReason,
            string acceptedTrajectoryIdentity,
            string acceptedExecutionClassificationIdentity)
        {
            ReceiveOrganizationAuthorityCommand.ValidateRevisionAndSequence(
                revision,
                sourceSequence);
            ReceiveOrganizationAuthorityCommand.RequireDefined(
                coverageReason,
                nameof(coverageReason));
            if (!actualFirstPassLanding.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actualFirstPassLanding));
            }

            Revision = revision;
            SourceSequence = sourceSequence;
            Actor = ReceiveOrganizationAuthorityCommand.RequirePlayer(
                actor,
                nameof(actor));
            ActualFirstPassLanding = actualFirstPassLanding;
            CoverageReason = coverageReason;
            AcceptedTrajectoryIdentity = RequireText(
                acceptedTrajectoryIdentity,
                nameof(acceptedTrajectoryIdentity));
            AcceptedExecutionClassificationIdentity = RequireText(
                acceptedExecutionClassificationIdentity,
                nameof(acceptedExecutionClassificationIdentity));
        }

        public long Revision { get; }

        public long SourceSequence { get; }

        public StablePlayerId Actor { get; }

        public SimVector3 ActualFirstPassLanding { get; }

        public PlanCoverageReason CoverageReason { get; }

        public string AcceptedTrajectoryIdentity { get; }

        public string AcceptedExecutionClassificationIdentity { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", parameterName);
            }

            return value;
        }
    }

    public sealed class ReceiveOrganizationAuthorityRequestV3
    {
        public ReceiveOrganizationAuthorityRequestV3(
            long revision,
            long sourceSequence,
            TeamRallyDecisionInput receiveInput,
            TeamRallyDecisionInput organizationInput,
            TeamRallyDecisionInput attackPreparationInput,
            OnCourtEligibilitySnapshot eligibility,
            IReadOnlyList<ReceiveOrganizationPlayerBindingV3> bindings)
        {
            ReceiveOrganizationAuthorityCommand.ValidateRevisionAndSequence(
                revision,
                sourceSequence);
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            Revision = revision;
            SourceSequence = sourceSequence;
            ReceiveInput = receiveInput ??
                           throw new ArgumentNullException(nameof(receiveInput));
            OrganizationInput = organizationInput ??
                                throw new ArgumentNullException(
                                    nameof(organizationInput));
            AttackPreparationInput = attackPreparationInput ??
                                     throw new ArgumentNullException(
                                         nameof(attackPreparationInput));
            Eligibility = eligibility ??
                          throw new ArgumentNullException(nameof(eligibility));
            Bindings =
                new ReadOnlyCollection<ReceiveOrganizationPlayerBindingV3>(
                    bindings.ToArray());
        }

        public long Revision { get; }

        public long SourceSequence { get; }

        public TeamRallyDecisionInput ReceiveInput { get; }

        public TeamRallyDecisionInput OrganizationInput { get; }

        public TeamRallyDecisionInput AttackPreparationInput { get; }

        public OnCourtEligibilitySnapshot Eligibility { get; }

        public IReadOnlyList<ReceiveOrganizationPlayerBindingV3> Bindings { get; }
    }

    public sealed class ReceiveOrganizationAuthorityStateV3
    {
        internal ReceiveOrganizationAuthorityStateV3(
            ReceiveOrganizationAuthorityPhaseV3 phase,
            long revision,
            ReceiveOrganizationPlanV3 plan,
            RallyPlanBranchV3 activeBranch,
            SimVector3? actualFirstPassLanding,
            PlanCoverageDecision coverageDecision,
            OrganizationFallbackReasonV3 fallbackReason,
            StablePlayerId? committedActor,
            string commandIdentity)
        {
            Phase = phase;
            Revision = revision;
            Plan = plan;
            ActiveBranch = activeBranch;
            ActualFirstPassLanding = actualFirstPassLanding;
            CoverageDecision = coverageDecision;
            FallbackReason = fallbackReason;
            CommittedActor = committedActor;
            CommandIdentity = commandIdentity;
        }

        public static ReceiveOrganizationAuthorityStateV3 Idle { get; } =
            new ReceiveOrganizationAuthorityStateV3(
                ReceiveOrganizationAuthorityPhaseV3.Idle,
                -1,
                null,
                RallyPlanBranchV3.Primary,
                null,
                null,
                OrganizationFallbackReasonV3.None,
                null,
                null);

        public ReceiveOrganizationAuthorityPhaseV3 Phase { get; }

        public long Revision { get; }

        public ReceiveOrganizationPlanV3 Plan { get; }

        public StablePlayerId PrimaryActor => Plan == null
            ? default
            : Plan.PrimaryReceiver;

        public RallyPlanBranchV3 ActiveBranch { get; }

        public SimVector3? ActualFirstPassLanding { get; }

        public PlanCoverageDecision CoverageDecision { get; }

        public OrganizationFallbackReasonV3 FallbackReason { get; }

        public StablePlayerId? CommittedActor { get; }

        public string CommandIdentity { get; }
    }

    public sealed class ReceiveOrganizationAuthorityCoordinator
    {
        private readonly ReceiveOrganizationResponsibilityPlanner _planner;
        private readonly IReceiveOrganizationAuthorityCommandSink _sink;
        private readonly List<ReceiveOrganizationAuthorityCommand> _activeCommands =
            new List<ReceiveOrganizationAuthorityCommand>();
        private ReceiveOrganizationAuthorityRequestV3 _request;
        private ReceiveOrganizationPlanningResult _planning;
        private StablePlayerId _activeReceiveActor;
        private long _lastSourceSequence;
        private PerceptionReceiptV3 _perception;

        public ReceiveOrganizationAuthorityCoordinator(
            ReceiveOrganizationResponsibilityPlanner planner,
            IReceiveOrganizationAuthorityCommandSink sink)
        {
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            State = ReceiveOrganizationAuthorityStateV3.Idle;
        }

        public ReceiveOrganizationAuthorityStateV3 State { get; private set; }

        public ReceiveOrganizationPlanningResult CurrentPlanning => _planning;

        public PerceptionReceiptV3 CurrentPerception => _perception;

        public ReceiveOrganizationAuthorityStateV3 PlanReceive(
            ReceiveOrganizationAuthorityRequestV3 request)
        {
            return PlanReceive(request, null);
        }

        public ReceiveOrganizationAuthorityStateV3 PlanReceive(
            ReceiveOrganizationAuthorityRequestV3 request,
            StablePlayerId? committedContinuationReceiver)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Revision <= State.Revision)
            {
                throw new InvalidOperationException(
                    "Plan revisions must increase monotonically.");
            }

            if (request.SourceSequence <= _lastSourceSequence)
            {
                throw new InvalidOperationException(
                    "Source sequences must increase monotonically.");
            }

            // A perception receipt is event-owned. It must never leak from the
            // preceding revision/side into the new planning batch.
            _perception = null;
            var planning = _planner.PlanReceive(
                request.ReceiveInput,
                request.AttackPreparationInput,
                request.Eligibility,
                request.Bindings,
                request.Revision,
                committedContinuationReceiver);
            var coverage = Coverage(
                request.Revision,
                PlanCoverageDecisionKind.CoveredActivateBranch,
                PlanCoverageReason.RallyOpen,
                RallyPlanBranchV3.Primary,
                0);
            var commands = ReceiveCommands(
                request,
                planning,
                request.SourceSequence);
            var next = NewState(
                ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned,
                planning,
                RallyPlanBranchV3.Primary,
                null,
                coverage,
                null,
                Identity(
                    request.Revision,
                    request.SourceSequence,
                    ReceiveOrganizationCommandKind.PrimaryReceive,
                    planning.Plan.PrimaryReceiver));
            Publish(next, planning, commands, request.SourceSequence);
            _request = request;
            _planning = planning;
            _activeReceiveActor = planning.Plan.PrimaryReceiver;
            _activeCommands.Clear();
            _activeCommands.AddRange(commands);
            _lastSourceSequence = request.SourceSequence;
            State = next;
            return State;
        }

        public void ApplyPerception(PerceptionReceiptV3 receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            if (_request == null ||
                State.Phase != ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned ||
                receipt.Revision != State.Revision ||
                receipt.SourceSequence != _request.SourceSequence ||
                receipt.ObservingSide != State.Plan.Side)
                throw new InvalidOperationException(
                    "Perception must belong to the active receive planning event.");
            var selected = receipt.SupportDecision.SelectedPlayer;
            if (!State.Plan.PrimaryReceiver.Equals(selected) &&
                !State.Plan.EmergencyReceivers.Contains(selected) &&
                !State.Plan.BackupOrganizers.Contains(selected))
                throw new InvalidOperationException(
                    "Perceived support is outside the declared Gate H support set.");
            _perception = receipt;
        }

        public ReceiveOrganizationAuthorityStateV3 CommitReceive(
            long revision,
            long sourceSequence)
        {
            ValidateTransition(
                revision,
                sourceSequence,
                ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned);
            var primaryIndex = _activeCommands.FindIndex(command =>
                command.Kind == ReceiveOrganizationCommandKind.PrimaryReceive &&
                command.Actor.Equals(State.PrimaryActor));
            if (primaryIndex < 0)
            {
                throw new InvalidOperationException(
                    "The primary receive command is not active.");
            }

            var committed = _activeCommands[primaryIndex].WithCommitted(sourceSequence);
            _activeCommands[primaryIndex] = committed;
            var next = NewState(
                ReceiveOrganizationAuthorityPhaseV3.ReceiveCommitted,
                _planning,
                State.ActiveBranch,
                State.ActualFirstPassLanding,
                State.CoverageDecision,
                committed.Actor,
                Identity(revision, sourceSequence, committed.Kind, committed.Actor));
            Publish(next, _planning, new[] { committed }, sourceSequence);
            _lastSourceSequence = sourceSequence;
            State = next;
            return State;
        }

        public ReceiveOrganizationAuthorityStateV3
            SeedPlannedReceiveAsAlreadyAccepted(
                AcceptedReceiveV3 accepted)
        {
            if (accepted == null)
                throw new ArgumentNullException(nameof(accepted));
            ValidateTransition(
                accepted.Revision,
                accepted.SourceSequence,
                ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned);
            if (!IsDeclaredReceiveActor(accepted.Actor))
                throw new InvalidOperationException(
                    "Semantic Receive actor is outside the planned authority.");

            _activeReceiveActor = accepted.Actor;
            State = NewState(
                ReceiveOrganizationAuthorityPhaseV3.ReceiveCommitted,
                _planning,
                State.ActiveBranch,
                accepted.ActualFirstPassLanding,
                State.CoverageDecision,
                accepted.Actor,
                Identity(
                    accepted.Revision,
                    accepted.SourceSequence,
                    ReceiveOrganizationCommandKind.PrimaryReceive,
                    accepted.Actor));
            return AcceptReceive(accepted);
        }

        public ReceiveOrganizationAuthorityStateV3 ActivateEmergency(
            long revision,
            long sourceSequence,
            StablePlayerId actor)
        {
            ValidateTransition(
                revision,
                sourceSequence,
                ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned);
            if (!State.Plan.EmergencyReceivers.Contains(actor))
            {
                throw new InvalidOperationException(
                    "Only a declared emergency receiver may be activated.");
            }

            var commands = new List<ReceiveOrganizationAuthorityCommand>();
            AddCancelForUncommittedPrimary(commands, sourceSequence);
            var emergency = Command(
                revision,
                sourceSequence,
                ReceiveOrganizationCommandKind.EmergencyReceive,
                actor,
                RallyPlanBranchV3.Contingency,
                TeamRallyDecision.NoDecision,
                false);
            commands.Add(emergency);
            _activeCommands.Add(emergency);
            var coverage = Coverage(
                revision,
                PlanCoverageDecisionKind.CoveredActivateBranch,
                PlanCoverageReason.WithinConditionalEnvelope,
                RallyPlanBranchV3.Contingency,
                0);
            var next = NewState(
                ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned,
                _planning,
                RallyPlanBranchV3.Contingency,
                State.ActualFirstPassLanding,
                coverage,
                State.CommittedActor,
                Identity(revision, sourceSequence, emergency.Kind, actor));
            Publish(next, _planning, commands, sourceSequence);
            _activeReceiveActor = actor;
            _lastSourceSequence = sourceSequence;
            State = next;
            return State;
        }

        public ReceiveOrganizationAuthorityStateV3 AcceptReceive(
            AcceptedReceiveV3 accepted)
        {
            if (accepted == null)
            {
                throw new ArgumentNullException(nameof(accepted));
            }

            ValidateReceivePhase(accepted.Revision, accepted.SourceSequence);
            if (accepted.CoverageReason == PlanCoverageReason.EnvelopeExceeded)
            {
                throw new InvalidOperationException(
                    "Global tactical rebuild is outside Gate H authority.");
            }

            if (!IsDeclaredReceiveActor(accepted.Actor))
            {
                throw new InvalidOperationException(
                    "The accepted receive actor is not declared by the active plan.");
            }

            var coverage = CoverageFor(
                accepted.Revision,
                accepted.CoverageReason,
                State.ActiveBranch);
            if (accepted.CoverageReason == PlanCoverageReason.RallyEnd)
            {
                var terminal = NewState(
                    ReceiveOrganizationAuthorityPhaseV3.Terminal,
                    _planning,
                    State.ActiveBranch,
                    accepted.ActualFirstPassLanding,
                    coverage,
                    State.CommittedActor,
                    Identity(
                        accepted.Revision,
                        accepted.SourceSequence,
                        ReceiveOrganizationCommandKind.CancelUncommitted,
                        accepted.Actor));
                Publish(
                    terminal,
                    _planning,
                    CancelUncommitted(accepted.SourceSequence),
                    accepted.SourceSequence);
                _lastSourceSequence = accepted.SourceSequence;
                State = terminal;
                return State;
            }

            var organizationInput = OrganizationInputFor(accepted);
            var organization = _planner.PlanOrganization(
                organizationInput,
                _request.AttackPreparationInput,
                _request.Eligibility,
                _request.Bindings,
                accepted.Revision);
            var commands = new List<ReceiveOrganizationAuthorityCommand>();
            var phase = ReceiveOrganizationAuthorityPhaseV3.OrganizationPlanned;
            if (organization.Decision.HasDecision)
            {
                var organizer = StableFor(organization.Decision.Actor);
                commands.Add(Command(
                    accepted.Revision,
                    accepted.SourceSequence,
                    ReceiveOrganizationCommandKind.OrganizationContact,
                    organizer,
                    organizer.Equals(organization.Plan.RegisteredSetter)
                        ? RallyPlanBranchV3.Primary
                        : RallyPlanBranchV3.Contingency,
                    organization.Decision,
                    false));
                AddAttackPreparation(
                    commands,
                    organization,
                    accepted.SourceSequence);
            }
            else
            {
                phase = ReceiveOrganizationAuthorityPhaseV3.Terminal;
            }

            var next = NewState(
                phase,
                organization,
                State.ActiveBranch,
                accepted.ActualFirstPassLanding,
                coverage,
                State.CommittedActor,
                commands.Count == 0
                    ? Identity(
                        accepted.Revision,
                        accepted.SourceSequence,
                        ReceiveOrganizationCommandKind.CancelUncommitted,
                        accepted.Actor)
                    : Identity(
                        accepted.Revision,
                        accepted.SourceSequence,
                        commands[0].Kind,
                        commands[0].Actor));
            Publish(next, organization, commands, accepted.SourceSequence);
            _activeCommands.Clear();
            _activeCommands.AddRange(commands);
            _planning = organization;
            _lastSourceSequence = accepted.SourceSequence;
            State = next;
            return State;
        }

        public ReceiveOrganizationAuthorityStateV3 CommitOrganization(
            long revision,
            long sourceSequence)
        {
            ValidateTransition(
                revision,
                sourceSequence,
                ReceiveOrganizationAuthorityPhaseV3.OrganizationPlanned);
            var index = _activeCommands.FindIndex(command =>
                command.Kind ==
                ReceiveOrganizationCommandKind.OrganizationContact);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    "The organization command is not active.");
            }

            var committed = _activeCommands[index].WithCommitted(sourceSequence);
            _activeCommands[index] = committed;
            var next = NewState(
                ReceiveOrganizationAuthorityPhaseV3.OrganizationCommitted,
                _planning,
                State.ActiveBranch,
                State.ActualFirstPassLanding,
                State.CoverageDecision,
                committed.Actor,
                Identity(revision, sourceSequence, committed.Kind, committed.Actor));
            Publish(next, _planning, new[] { committed }, sourceSequence);
            _lastSourceSequence = sourceSequence;
            State = next;
            return State;
        }

        public ReceiveOrganizationAuthorityStateV3 HandOffToAttack(
            long revision,
            long sourceSequence)
        {
            ValidateTransition(
                revision,
                sourceSequence,
                ReceiveOrganizationAuthorityPhaseV3.OrganizationCommitted);
            var next = NewState(
                ReceiveOrganizationAuthorityPhaseV3.HandedOffToAttack,
                _planning,
                State.ActiveBranch,
                State.ActualFirstPassLanding,
                State.CoverageDecision,
                State.CommittedActor,
                State.CommandIdentity);
            Publish(
                next,
                _planning,
                Array.Empty<ReceiveOrganizationAuthorityCommand>(),
                sourceSequence);
            _lastSourceSequence = sourceSequence;
            State = next;
            return State;
        }

        public ReceiveOrganizationAuthorityStateV3 Invalidate(
            long revision,
            long sourceSequence,
            PlanCoverageReason reason)
        {
            ValidateCurrentIdentity(revision, sourceSequence);
            ReceiveOrganizationAuthorityCommand.RequireDefined(reason, nameof(reason));
            if (reason == PlanCoverageReason.EnvelopeExceeded)
            {
                throw new InvalidOperationException(
                    "Global tactical rebuild is outside Gate H authority.");
            }

            var kind = reason switch
            {
                PlanCoverageReason.ResponsibleActorChanged =>
                    PlanCoverageDecisionKind.LocalRevision,
                PlanCoverageReason.BallEnvelopeExceeded =>
                    PlanCoverageDecisionKind.ScopedReplan,
                PlanCoverageReason.RallyEnd =>
                    PlanCoverageDecisionKind.TerminalNoPlan,
                _ => PlanCoverageDecisionKind.ScopedReplan
            };
            var coverage = Coverage(
                revision,
                kind,
                reason,
                null,
                kind == PlanCoverageDecisionKind.LocalRevision ? 1 :
                kind == PlanCoverageDecisionKind.ScopedReplan ? 2 : 0);
            var commands = CancelUncommitted(sourceSequence);
            var phase = kind == PlanCoverageDecisionKind.TerminalNoPlan
                ? ReceiveOrganizationAuthorityPhaseV3.Terminal
                : State.Phase;
            var next = NewState(
                phase,
                _planning,
                State.ActiveBranch,
                State.ActualFirstPassLanding,
                coverage,
                State.CommittedActor,
                commands.Count == 0
                    ? State.CommandIdentity
                    : Identity(
                        revision,
                        sourceSequence,
                        commands[0].Kind,
                        commands[0].Actor));
            Publish(next, _planning, commands, sourceSequence);
            _lastSourceSequence = sourceSequence;
            State = next;
            return State;
        }

        private IReadOnlyList<ReceiveOrganizationAuthorityCommand> ReceiveCommands(
            ReceiveOrganizationAuthorityRequestV3 request,
            ReceiveOrganizationPlanningResult planning,
            long sourceSequence)
        {
            var commands = new List<ReceiveOrganizationAuthorityCommand>
            {
                Command(
                    request.Revision,
                    sourceSequence,
                    ReceiveOrganizationCommandKind.PrimaryReceive,
                    planning.Plan.PrimaryReceiver,
                    RallyPlanBranchV3.Primary,
                    planning.Decision,
                    false)
            };
            for (var index = 0;
                 index < planning.Plan.EmergencyReceivers.Count;
                 index++)
            {
                commands.Add(Command(
                    request.Revision,
                    sourceSequence,
                    ReceiveOrganizationCommandKind.EmergencyReceive,
                    planning.Plan.EmergencyReceivers[index],
                    RallyPlanBranchV3.Contingency,
                    TeamRallyDecision.NoDecision,
                    false));
            }

            commands.Add(Command(
                request.Revision,
                sourceSequence,
                ReceiveOrganizationCommandKind.SetterPreparation,
                planning.Plan.RegisteredSetter,
                RallyPlanBranchV3.Primary,
                TeamRallyDecision.NoDecision,
                false));
            AddAttackPreparation(commands, planning, sourceSequence);
            return commands;
        }

        private void AddAttackPreparation(
            ICollection<ReceiveOrganizationAuthorityCommand> commands,
            ReceiveOrganizationPlanningResult planning,
            long sourceSequence)
        {
            if (!planning.AttackPreparationDecision.HasDecision)
            {
                return;
            }

            commands.Add(Command(
                planning.Plan.Revision,
                sourceSequence,
                ReceiveOrganizationCommandKind.AttackPreparation,
                planning.Plan.AttackPreparation,
                RallyPlanBranchV3.Primary,
                planning.AttackPreparationDecision,
                false));
        }

        private void AddCancelForUncommittedPrimary(
            ICollection<ReceiveOrganizationAuthorityCommand> commands,
            long sourceSequence)
        {
            var primary = _activeCommands.FirstOrDefault(command =>
                command.Kind == ReceiveOrganizationCommandKind.PrimaryReceive);
            if (primary != null && !primary.IsCommitted)
            {
                commands.Add(Command(
                    State.Revision,
                    sourceSequence,
                    ReceiveOrganizationCommandKind.CancelUncommitted,
                    primary.Actor,
                    primary.Branch,
                    TeamRallyDecision.NoDecision,
                    false));
            }
        }

        private List<ReceiveOrganizationAuthorityCommand> CancelUncommitted(
            long sourceSequence)
        {
            return _activeCommands
                .Where(command =>
                    !command.IsCommitted &&
                    command.Kind !=
                    ReceiveOrganizationCommandKind.CancelUncommitted)
                .Select(command => Command(
                    State.Revision,
                    sourceSequence,
                    ReceiveOrganizationCommandKind.CancelUncommitted,
                    command.Actor,
                    command.Branch,
                    TeamRallyDecision.NoDecision,
                    false))
                .ToList();
        }

        private TeamRallyDecisionInput OrganizationInputFor(
            AcceptedReceiveV3 accepted)
        {
            var template = _request.OrganizationInput;
            var runtimeActor = _request.Bindings
                .Single(binding =>
                    binding.StablePlayerId.Equals(accepted.Actor))
                .RuntimePlayerId;
            return new TeamRallyDecisionInput(
                template.Team,
                template.Tactic,
                template.Players,
                accepted.ActualFirstPassLanding,
                template.AvailableSeconds,
                template.BaseMovementSpeed,
                1,
                runtimeActor,
                template.TacticRevision,
                template.DecisionIndex,
                RallyDecisionStage.Organize,
                template.Weights);
        }

        private StablePlayerId StableFor(RuntimePlayerId runtimePlayerId)
        {
            return _request.Bindings
                .Single(binding =>
                    binding.RuntimePlayerId.Equals(runtimePlayerId))
                .StablePlayerId;
        }

        private bool IsDeclaredReceiveActor(StablePlayerId actor)
        {
            return actor.Equals(_activeReceiveActor);
        }

        private void ValidateReceivePhase(long revision, long sourceSequence)
        {
            ValidateCurrentIdentity(revision, sourceSequence);
            if (State.Phase !=
                    ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned &&
                State.Phase !=
                    ReceiveOrganizationAuthorityPhaseV3.ReceiveCommitted)
            {
                throw new InvalidOperationException(
                    "The event is incompatible with the current receive phase.");
            }
        }

        private void ValidateTransition(
            long revision,
            long sourceSequence,
            ReceiveOrganizationAuthorityPhaseV3 required)
        {
            ValidateCurrentIdentity(revision, sourceSequence);
            if (State.Phase != required)
            {
                throw new InvalidOperationException(
                    "The event is incompatible with the current authority phase.");
            }
        }

        private void ValidateCurrentIdentity(long revision, long sourceSequence)
        {
            if (revision != State.Revision)
            {
                throw new InvalidOperationException(
                    "The event revision is stale or unknown.");
            }

            if (sourceSequence <= _lastSourceSequence)
            {
                throw new InvalidOperationException(
                    "The event source sequence is stale or duplicated.");
            }
        }

        private static PlanCoverageDecision CoverageFor(
            long revision,
            PlanCoverageReason reason,
            RallyPlanBranchV3 activeBranch)
        {
            return reason switch
            {
                PlanCoverageReason.WithinConditionalEnvelope => Coverage(
                    revision,
                    PlanCoverageDecisionKind.CoveredActivateBranch,
                    reason,
                    activeBranch,
                    0),
                PlanCoverageReason.ResponsibleActorChanged => Coverage(
                    revision,
                    PlanCoverageDecisionKind.LocalRevision,
                    reason,
                    null,
                    1),
                PlanCoverageReason.BallEnvelopeExceeded => Coverage(
                    revision,
                    PlanCoverageDecisionKind.ScopedReplan,
                    reason,
                    null,
                    2),
                PlanCoverageReason.RallyEnd => Coverage(
                    revision,
                    PlanCoverageDecisionKind.TerminalNoPlan,
                    reason,
                    null,
                    0),
                _ => Coverage(
                    revision,
                    PlanCoverageDecisionKind.ScopedReplan,
                    reason,
                    null,
                    2)
            };
        }

        private static PlanCoverageDecision Coverage(
            long revision,
            PlanCoverageDecisionKind kind,
            PlanCoverageReason reason,
            RallyPlanBranchV3? branch,
            int depth)
        {
            return new PlanCoverageDecision(
                kind,
                revision.ToString(CultureInfo.InvariantCulture),
                reason,
                Array.Empty<string>(),
                depth,
                branch);
        }

        private static ReceiveOrganizationAuthorityCommand Command(
            long revision,
            long sourceSequence,
            ReceiveOrganizationCommandKind kind,
            StablePlayerId actor,
            RallyPlanBranchV3 branch,
            TeamRallyDecision decision,
            bool committed)
        {
            return new ReceiveOrganizationAuthorityCommand(
                revision,
                sourceSequence,
                kind,
                actor,
                branch,
                decision,
                committed);
        }

        private static string Identity(
            long revision,
            long sourceSequence,
            ReceiveOrganizationCommandKind kind,
            StablePlayerId actor)
        {
            return string.Join(
                ":",
                revision.ToString(CultureInfo.InvariantCulture),
                sourceSequence.ToString(CultureInfo.InvariantCulture),
                kind.ToString(),
                actor.Value);
        }

        private ReceiveOrganizationAuthorityStateV3 NewState(
            ReceiveOrganizationAuthorityPhaseV3 phase,
            ReceiveOrganizationPlanningResult planning,
            RallyPlanBranchV3 branch,
            SimVector3? landing,
            PlanCoverageDecision coverage,
            StablePlayerId? committedActor,
            string identity)
        {
            return new ReceiveOrganizationAuthorityStateV3(
                phase,
                planning.Plan.Revision,
                planning.Plan,
                branch,
                landing,
                coverage,
                planning.FallbackReason,
                committedActor,
                identity);
        }

        private void Publish(
            ReceiveOrganizationAuthorityStateV3 state,
            ReceiveOrganizationPlanningResult planning,
            IReadOnlyList<ReceiveOrganizationAuthorityCommand> commands,
            long sourceSequence)
        {
            var evidence = new ReceiveOrganizationAuthorityEvidenceV3(
                state.Revision,
                sourceSequence,
                state.Phase,
                state.Plan,
                planning.SetterEvidence,
                state.FallbackReason,
                state.CoverageDecision,
                state.ActualFirstPassLanding,
                _perception);
            _sink.Publish(new ReceiveOrganizationCommandBatch(
                state.Revision,
                sourceSequence,
                commands,
                evidence));
        }
    }
}
