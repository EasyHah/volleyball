using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using MatchContextV4 = Volleyball.Shared.Contracts.MatchContextV4;
using MatchResultV4 = Volleyball.Shared.Contracts.MatchResultV4;
using RulesVersions = Volleyball.Shared.Contracts.RulesVersions;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation
{
    public enum PostAttackContinuationStateV4
    {
        None,
        AwaitingAttackCrossing,
        AwaitingBlockOutcome,
        PendingSideResolution,
        DefendingSideFloorDefense,
        AttackingSideCoverage,
        BlockingSideRecovery,
        ResolvedByContact,
        ResolvedByGround
    }

    public sealed class ReplayOrganizationDecisionDiagnostic
    {
        public ReplayOrganizationDecisionDiagnostic(
            SimVector3 target,
            SimVector3 firstPassLanding,
            SetterOrganizationZoneGrade zoneGrade,
            PlayerId setter,
            string setterReachStatus,
            float setterPrepositionMovementMeters,
            PlayerId organizer,
            string fallbackReason)
        {
            Target = target;
            FirstPassLanding = firstPassLanding;
            ZoneGrade = zoneGrade;
            Setter = setter;
            SetterReachStatus = setterReachStatus;
            SetterPrepositionMovementMeters = setterPrepositionMovementMeters;
            Organizer = organizer;
            FallbackReason = fallbackReason;
        }

        public SimVector3 Target { get; }
        public SimVector3 FirstPassLanding { get; }
        public SetterOrganizationZoneGrade ZoneGrade { get; }
        public PlayerId Setter { get; }
        public string SetterReachStatus { get; }
        public float SetterPrepositionMovementMeters { get; }
        public PlayerId Organizer { get; }
        public string FallbackReason { get; }
    }

    public sealed class ReplayDecisionEvent
    {
        private ReplayDecisionEvent(
            float simulationTimeSeconds,
            RallyDecisionStage stage,
            TeamId team,
            float availableSeconds,
            SimVector3 predictedBallTarget,
            RallyTacticalWeights weights,
            TeamRallyDecision decision,
            ReplayOrganizationDecisionDiagnostic organizationDiagnostic)
        {
            SimulationTimeSeconds = simulationTimeSeconds;
            Stage = stage;
            Team = team;
            AvailableSeconds = availableSeconds;
            PredictedBallTarget = predictedBallTarget;
            Weights = weights;
            SelectedPlayer = decision.Actor;
            SelectedAction = decision.Action;
            Candidates = new List<RallyDecisionCandidate>(decision.Candidates).AsReadOnly();
            OrganizationDiagnostic = organizationDiagnostic;
        }

        public float SimulationTimeSeconds { get; }
        public RallyDecisionStage Stage { get; }
        public TeamId Team { get; }
        public float AvailableSeconds { get; }
        public SimVector3 PredictedBallTarget { get; }
        public RallyTacticalWeights Weights { get; }
        public PlayerId SelectedPlayer { get; }
        public TechniqueAction SelectedAction { get; }
        public IReadOnlyList<RallyDecisionCandidate> Candidates { get; }
        public ReplayOrganizationDecisionDiagnostic OrganizationDiagnostic { get; }

        internal static ReplayDecisionEvent Create(
            float simulationTimeSeconds,
            RallyDecisionStage stage,
            TeamId team,
            float availableSeconds,
            SimVector3 predictedBallTarget,
            RallyTacticalWeights weights,
            TeamRallyDecision decision,
            ReplayOrganizationDecisionDiagnostic organizationDiagnostic = null)
        {
            return new ReplayDecisionEvent(
                simulationTimeSeconds,
                stage,
                team,
                availableSeconds,
                predictedBallTarget,
                weights,
                decision,
                organizationDiagnostic);
        }
    }

    public class ReplaySimpleEvent
    {
        public ReplaySimpleEvent(string kind, float simulationTimeSeconds, TeamId team, StablePlayerId? playerId)
        {
            Kind = kind;
            SimulationTimeSeconds = simulationTimeSeconds;
            Team = team;
            PlayerId = playerId;
        }

        public string Kind { get; }
        public float SimulationTimeSeconds { get; }
        public TeamId Team { get; }
        public StablePlayerId? PlayerId { get; }
    }

    public sealed class ReplayContactEvent : ReplaySimpleEvent
    {
        public ReplayContactEvent(
            string kind,
            float simulationTimeSeconds,
            TeamId team,
            StablePlayerId? playerId,
            TechniqueAction action,
            ReplaySetChainEvent setChain = null,
            AttackGeometryFactV3 observedAttackGeometry = null,
            RuleTransitionV3 ruleTransition = null,
            ExecutionSampleClassificationV4 executionClassification = null,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact = null,
            ReceiveOrganizationAuthorityReceipt organizationAuthority = null,
            GateISetIntentReceiptV3 gateISetIntentAuthority = null,
            AttackDefenseAuthorityReceipt attackDefenseAuthority = null)
            : base(kind, simulationTimeSeconds, team, playerId)
        {
            if (gateISetIntentAuthority != null && attackDefenseAuthority != null)
                throw new ArgumentException("A replay contact cannot carry both Gate I receipt kinds.");
            Action = action;
            SetChain = setChain;
            ObservedAttackGeometry = observedAttackGeometry;
            RuleTransition = ruleTransition;
            ExecutionClassification = executionClassification;
            TrajectoryArtifact = trajectoryArtifact;
            OrganizationAuthority = organizationAuthority;
            GateISetIntentAuthority = gateISetIntentAuthority;
            AttackDefenseAuthority = attackDefenseAuthority;
        }

        public TechniqueAction Action { get; }
        public ReplaySetChainEvent SetChain { get; }
        public AttackGeometryFactV3 ObservedAttackGeometry { get; }
        public RuleTransitionV3 RuleTransition { get; }
        public ExecutionSampleClassificationV4 ExecutionClassification { get; }
        public BallTrajectoryPredictionArtifactV4 TrajectoryArtifact { get; }
        public ReceiveOrganizationAuthorityReceipt OrganizationAuthority { get; }
        public GateISetIntentReceiptV3 GateISetIntentAuthority { get; }
        public AttackDefenseAuthorityReceipt AttackDefenseAuthority { get; }
    }

    public sealed class ReplayDefenseAttemptEvent
    {
        public ReplayDefenseAttemptEvent(
            string kind, string attemptIdentity,
            AttackDefenseAuthorityReceipt receipt, TeamId team,
            float windowStartSimulationTime, float windowEndSimulationTime,
            float simulationTimeSeconds, SimVector3 ballPosition,
            SimVector3 ballVelocity,
            PostAttackContinuationStateV4 continuationState, string reason,
            int? winningContactGroupId = null,
            StablePlayerId? winningActor = null)
        {
            Kind = kind;
            AttemptIdentity = attemptIdentity;
            Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
            Team = team;
            WindowStartSimulationTime = windowStartSimulationTime;
            WindowEndSimulationTime = windowEndSimulationTime;
            SimulationTimeSeconds = simulationTimeSeconds;
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            ContinuationState = continuationState;
            Reason = reason ?? string.Empty;
            WinningContactGroupId = winningContactGroupId;
            WinningActor = winningActor;
        }

        public string Kind { get; }
        public string AttemptIdentity { get; }
        public AttackDefenseAuthorityReceipt Receipt { get; }
        public TeamId Team { get; }
        public float WindowStartSimulationTime { get; }
        public float WindowEndSimulationTime { get; }
        public float SimulationTimeSeconds { get; }
        public SimVector3 BallPosition { get; }
        public SimVector3 BallVelocity { get; }
        public PostAttackContinuationStateV4 ContinuationState { get; }
        public string Reason { get; }
        public int? WinningContactGroupId { get; }
        public StablePlayerId? WinningActor { get; }
    }

    public sealed class ReplaySetChainEvent
    {
        public ReplaySetChainEvent(
            SimVector3 plannedAttackContactCenter,
            SimVector3 replannedAttackContactCenter,
            SetQualityGrade qualityGrade,
            AttackContactOutcome replanOutcome,
            AttackResponsibility primaryResponsibility,
            string reason)
        {
            PlannedAttackContactCenter = plannedAttackContactCenter;
            ReplannedAttackContactCenter = replannedAttackContactCenter;
            QualityGrade = qualityGrade;
            ReplanOutcome = replanOutcome;
            PrimaryResponsibility = primaryResponsibility;
            Reason = reason;
        }

        public SimVector3 PlannedAttackContactCenter { get; }
        public SimVector3 ReplannedAttackContactCenter { get; }
        public SetQualityGrade QualityGrade { get; }
        public AttackContactOutcome ReplanOutcome { get; }
        public AttackResponsibility PrimaryResponsibility { get; }
        public string Reason { get; }
    }

    public sealed class ReplayRallyResolvedEvent : ReplaySimpleEvent
    {
        public ReplayRallyResolvedEvent(
            float simulationTimeSeconds,
            TeamId winningTeam,
            StablePlayerId? scorerId,
            StablePlayerId? errorPlayerId,
            string reason)
            : base("RallyResolved", simulationTimeSeconds, winningTeam, scorerId)
        {
            ErrorPlayerId = errorPlayerId;
            Reason = reason;
        }

        public StablePlayerId? ErrorPlayerId { get; }
        public string Reason { get; }
    }

    public class PhysicalMatchRallyDirector : MonoBehaviour
    {
        public const float ServeArrivalVerticalSpeed = -8f;
        public const float ControlledHandlingMinimumFlightSeconds = 1f;

        private const float BaseMovementSpeed = 7f;
        private const float ReceiveFlightSeconds = 0.70f;
        private const float GroundHeight = 0.15f;
        private const float ContactWindowLead = 0.10f;
        private const float ContactWindowTail = 0.14f;
        private const float PostBlockSideResolutionDistance = 0.20f;
        private const float MaximumContinuationSearchSeconds = 4f;

        private static readonly BallSimulationParameters SimulationParameters =
            new BallSimulationParameters(-9.8f, 0.9995f);

        private readonly Dictionary<PlayerId, PrototypePlayerAgent> _players =
            new Dictionary<PlayerId, PrototypePlayerAgent>();
        private readonly PhysicalRallyTacticPlanner _tacticPlanner =
            new PhysicalRallyTacticPlanner();
        private readonly RallyDecisionCoordinatorV3 _decisionCoordinator =
            new RallyDecisionCoordinatorV3(7351);

        private SimulatedBall _ball;
        private ScoreDisplay _scoreDisplay;
        private BlockImpactFeedback _blockImpactFeedback;
        private AiDecisionTimeController _aiDecisionTimeController;
        private PhysicalRallyTactics _currentTactics;
        private IMatchSetRuntime _set;
        private RallyTouchState _touchState;
        private TeamRallyDecision _scheduledDecision;
        private TeamRallyDecision _plannedAttackDecision;
        private PlayerId? _scheduledPrimaryActor;
        private readonly HashSet<PlayerId> _scheduledBlockers = new HashSet<PlayerId>();
        private readonly List<AttackDefenseAuthorityReceipt> _pendingGateIReceiveWindows =
            new List<AttackDefenseAuthorityReceipt>();
        private readonly List<AttackDefenseAuthorityReceipt> _activePostAttackReceives =
            new List<AttackDefenseAuthorityReceipt>();
        private PlayerId? _scheduledBlockPrimary;
        private float _committedGateIBlockTime = -1f;
        private bool _awaitingPostBlockCrossing;
        private TeamId? _postBlockerTeam;
        private StablePlayerId? _postBlockerActor;
        private SimVector3 _postBlockImpactCenter;
        private SimVector3 _postBlockOutgoing;
        private PostAttackContinuationStateV4 _postAttackContinuationState;
        private TeamId? _pendingCrossingTeam;
        private float _expectedContactTime;
        private float _scheduledSetFlightSeconds;
        private bool _contactDeadlineActive;
        private bool _rallyActive;
        private bool _restartScheduled;
        private bool _lastTouchWasBackSetAttack;
        private bool _controlledHandlingActive;
        private SetQualityAssessment? _lastSetQualityAssessment;
        private SimVector3? _scheduledGeometricSetTarget;
        private SimVector3? _lastPlannedAttackContactCenter;
        private SimVector3? _lastReplannedAttackContactCenter;
        private AttackOutcome _lastSetReplanOutcome;
        private AttackResponsibility _lastAttackResponsibility;
        private PlayerId? _lastSetAttackActor;
        private PlayerId? _lastSetSetterActor;
        private ReplaySetChainEvent _pendingReplaySetChain;
        private AttackContactOutcome _lastAttackContactOutcome;
        private bool _lastSetChainSuccessRecorded;
        private bool _activeSetChain;
        private bool _lastSetWasSetter;
        private bool _forceInSystemReceiveExecution;
        private int _tacticRevision;
        private int _aiDecisionRequestVersion;
        private int _aiRequestSequence;
        private int _contactGroupSequence = 3000;
        private FullRallyV3RulesRuntimeAdapter _v3RulesAdapter;
        private MatchContextV4 _matchContext;
        private MatchSet _formalSet;
        private ExecutionEnvelopeV4 _lastPlannedExecutionEnvelopeV4;
        private ExecutionSampleClassificationV4 _lastExecutionSampleClassificationV4;
        private BallTrajectoryPredictionProviderV4 _trajectoryPredictionProviderV4;
        private BallTrajectoryPredictionArtifactV4 _lastTrajectoryPredictionArtifactV4;
        private BallTrajectoryPredictionArtifactV4
            _plannedAttackTrajectoryArtifactV4;
        private PendingV3AuthorityContact _pendingV3AuthorityContact;
        private StablePlayerId? _lastAcceptedV3Actor;
        private RallyContactClassificationV3? _lastAcceptedV3Classification;
        private RallyTacticalWeights _activeTacticalWeights;
        private PhysicalRallyTactics? _initialScenarioTactics;
        private RallyTacticalWeights? _initialScenarioAiWeights;
        private PhysicalMatchConfiguration _configuration;
        private readonly FormalRallyAuthorityOrchestrator
            _formalAuthority = new FormalRallyAuthorityOrchestrator();
        private static readonly CourtPerceptionConfigurationV3
            GateJPerceptionConfiguration =
                new CourtPerceptionConfigurationV3(
                    "gate-j-v1", .05f, .30f, .08f, 1.20f, .03f, .35f);
        private string _status = "Preparing dynamic physical 3v3";

        public int CompletedCycles { get; private set; }

        public int SuccessfulContacts { get; private set; }

        public V3RulesMode V3RulesMode { get; private set; }

        public int V3RuleTransitions { get; private set; }

        public int V3RuleParityMatches { get; private set; }

        public int V3RuleIntentionalCorrections { get; private set; }

        public int V3RuleUnexpectedMismatches { get; private set; }

        public string LastV3RuleDiagnostic { get; private set; } = string.Empty;

        public int ShadowPlanRecordingFailures { get; private set; }

        public string LastShadowPlanRecordingDiagnostic { get; private set; } = string.Empty;

        public int MissedRallies { get; private set; }

        public int GroundResolvedRallies { get; private set; }

        public int TacticRevision => _tacticRevision;

        public void ConfigureFormalScenario(
            PhysicalRallyTactics tactics,
            RallyTacticalWeights aiWeights,
            FormalMatchScenarioProvenanceV4 provenance)
        {
            if (_set != null || _rallyActive || provenance == null)
            {
                throw new InvalidOperationException(
                    "Formal scenario configuration must happen before match initialization.");
            }

            _initialScenarioTactics = tactics;
            _initialScenarioAiWeights = aiWeights;
            FormalScenarioProvenance = provenance;
        }

        public FormalMatchScenarioProvenanceV4 FormalScenarioProvenance { get; private set; }

        public int ExecutionErrorApplications { get; private set; }

        public int MovementAssignments { get; private set; }

        public int BlockSupportAssignments { get; private set; }

        public int CoverageSupportAssignments { get; private set; }

        public int BlockSupportActivations { get; private set; }

        public int BackSetAttackContacts { get; private set; }

        public int BackSetAttackFaults { get; private set; }

        public int EmergencyReceiveWindowAssignments { get; private set; }

        public int EmergencyReceiveContacts { get; private set; }

        public bool GateHAuthorityEnabled { get; private set; }

        public int GateHLegacyWriterInvocations { get; private set; }

        public bool GateIAuthorityEnabled { get; private set; }
        public bool GateJPerceptionEnabled => GateJEnabled;

        // A read-only lifecycle diagnostic.  It exposes no coordinator command
        // surface and lets formal integration tests observe V3-accepted
        // contact hand-offs without reaching into private authority state.
        public AttackDefenseAuthorityPhaseV3 GateIAuthorityPhase =>
            _formalAuthority.AttackCoordinator == null
                ? AttackDefenseAuthorityPhaseV3.Idle
                : _formalAuthority.AttackCoordinator.State.Phase;

        public int GateILegacyWriterInvocations { get; private set; }

        public int AcceptedSetContactWriterCount { get; private set; }

        public float TotalMovementShortfall { get; private set; }

        public int PhysicalBlockContacts { get; private set; }

        public int BlockImpactEffects => _blockImpactFeedback == null
            ? 0
            : _blockImpactFeedback.PlayedCount;

        public int PostBlockContinuations { get; private set; }

        public int PostBlockPossessionDeferrals { get; private set; }

        public int PrematurePostBlockReceiveWindows { get; private set; }

        public int PrematurePostBlockEmergencyWindows { get; private set; }

        public int PostBlockGroundPoints { get; private set; }

        public PostAttackContinuationStateV4 PostAttackContinuationState =>
            _postAttackContinuationState;

        public int ScheduledMultiBlockUnits { get; private set; }

        public int ScheduledBackRowBlockers { get; private set; }

        public int MaximumScheduledBlockers { get; private set; }

        public int NonSetterSetContacts { get; private set; }

        public int DefenderAttackContacts { get; private set; }

        public int BlueAttackContacts { get; private set; }

        public int OrangeAttackContacts { get; private set; }

        public int IllegalContactFaults { get; private set; }

        public int TotalSets { get; private set; }

        public int GradeASets { get; private set; }

        public int AttackableSets { get; private set; }

        public int DirectSetErrors { get; private set; }

        public int GradeASetAttackSuccesses { get; private set; }

        public int AdjustedAttackSuccesses { get; private set; }

        public int InSystemSetterSets { get; private set; }

        public int InSystemAttackableSets { get; private set; }

        public int GradeASetterSets { get; private set; }

        public int AGradeNoContactErrors { get; private set; }

        public int NormalSideSets { get; private set; }

        public int NormalAttackPlans { get; private set; }

        public int NearNetAttackPlans { get; private set; }

        public int GeometricSetTargetSelections { get; private set; }

        public int GeometricAttackRouteSelections { get; private set; }

        public int GeometricLineRoutes { get; private set; }

        public int GeometricCrossCourtRoutes { get; private set; }

        public int GeometricOverHandRoutes { get; private set; }

        public int GeometricEdgeLeftRoutes { get; private set; }

        public int GeometricEdgeRightRoutes { get; private set; }

        public string LastAGradeNoContactDiagnostic { get; private set; } = string.Empty;

        public float GradeASetRate => TotalSets == 0 ? 0f : (float)GradeASets / TotalSets;

        public float AttackableSetRate => TotalSets == 0 ? 0f : (float)AttackableSets / TotalSets;

        public float GradeASetAttackSuccessRate => GradeASets == 0
            ? 0f
            : (float)GradeASetAttackSuccesses / GradeASets;

        public float InSystemAttackableSetRate => InSystemSetterSets == 0
            ? 0f
            : (float)InSystemAttackableSets / InSystemSetterSets;

        public float AGradeNoContactErrorRate => GradeASetterSets == 0
            ? 0f
            : (float)AGradeNoContactErrors / GradeASetterSets;

        public SetQualityAssessment? LastSetQualityAssessment => _lastSetQualityAssessment;

        public SimVector3? LastPlannedAttackContactCenter => _lastPlannedAttackContactCenter;

        public SimVector3? LastReplannedAttackContactCenter => _lastReplannedAttackContactCenter;

        public AttackOutcome LastSetReplanOutcome => _lastSetReplanOutcome;

        public AttackResponsibility LastAttackResponsibility => _lastAttackResponsibility;

        public bool HasOpenSpikeContactWindow => _touchState?.ContactWindow != null &&
                                                 _touchState.ContactWindow.Action == TechniqueAction.Attack &&
                                                 !_controlledHandlingActive;

        public int CrowdingCorrections { get; private set; }

        public bool IsWaitingForAi => _aiDecisionTimeController != null &&
                                      _aiDecisionTimeController.IsWaiting;

        public int AiDecisionRequests => _aiDecisionTimeController == null
            ? 0
            : _aiDecisionTimeController.RequestCount;

        public int AiDecisionFallbacks => _aiDecisionTimeController == null
            ? 0
            : _aiDecisionTimeController.FallbackCount;

        public float MaximumAppliedMovementCorrection { get; private set; }

        public SetRoute BlueSetRoute => _currentTactics.Blue.SetRoute;

        public SpikeRoute BlueSpikeRoute => _currentTactics.Blue.SpikeRoute;

        public SetRoute OrangeSetRoute => _currentTactics.Orange.SetRoute;

        public SpikeRoute OrangeSpikeRoute => _currentTactics.Orange.SpikeRoute;

        public bool IsLoopRunning => _rallyActive && !_restartScheduled;

        public MatchResultV4 Result { get; private set; }

        private bool HasResult => Result != null || HasPrototypeResult;

        protected virtual bool HasPrototypeResult => false;

        protected virtual void CompletePrototypeMatch()
        {
            throw new InvalidOperationException(
                "Only an explicitly isolated prototype director may complete a prototype match.");
        }

        public int PlayerCount => _players.Count;

        public int RosterSize => _configuration == null ? 0 : _configuration.RosterSize;

        public int TargetScore => _set == null ? 0 : _set.SetTargetScore;

        public float CourtHalfLength => _configuration?.CourtHalfLength ?? CourtBuilder.HalfLength;

        public StablePlayerId CurrentServer => _set.ServerFor(_set.ServingSide);

        public int HomeScore => _set == null ? 0 : _set.HomeScore;

        public int AwayScore => _set == null ? 0 : _set.AwayScore;

        public TeamId ServingTeam => _set == null ? TeamId.Blue : FromSide(_set.ServingSide);

        public TeamId? PossessionTeam => _touchState?.PossessionTeam;

        public StablePlayerId? LastTouchPlayer => _touchState?.LastPhysicalTouch.HasValue == true
            ? StableId(_touchState.LastPhysicalTouch.Value)
            : null;

        public string ReplayRallyPhase => _restartScheduled
            ? "Resolved"
            : _touchState?.ContactWindow?.Action.ToString() ?? "Preparing";

        public event Action<ReplayDecisionEvent> ReplayDecisionPlanned;

        public event Action<ReplayContactEvent> ReplayContactAccepted;

        public event Action<ReplayDefenseAttemptEvent> ReplayDefenseAttemptRecorded;

        public event Action<RallyPlanV3> ReplayShadowPlanRecorded;

        public event Action<ReceiveOrganizationAuthorityReceipt>
            ReceiveOrganizationAuthorityCommitted;

        public event Action<AttackDefenseAuthorityReceipt> AttackDefenseAuthorityCommitted;

        public event Action<GateISetIntentReceiptV3> GateISetIntentCommitted;

        public event Action<ReplaySimpleEvent> ReplayServeStarted;

        public event Action<ReplaySimpleEvent> ReplayNetCrossed;

        public event Action<ReplaySimpleEvent> ReplayGroundContact;

        public event Action<ReplayRallyResolvedEvent> ReplayRallyResolved;

        public int HomeRotationOffset => _set == null ? 0 : _set.RotationOffsetFor(TeamSide.Home);

        public int AwayRotationOffset => _set == null ? 0 : _set.RotationOffsetFor(TeamSide.Away);

        public MatchContextV4 MatchContext => _matchContext;

        public ExecutionEnvelopeV4 LastPlannedExecutionEnvelopeV4 =>
            _lastPlannedExecutionEnvelopeV4;

        public ExecutionSampleClassificationV4 LastExecutionSampleClassificationV4 =>
            _lastExecutionSampleClassificationV4;

        public BallTrajectoryPredictionArtifactV4 LastTrajectoryPredictionArtifactV4 =>
            _lastTrajectoryPredictionArtifactV4;

        public static BallTrajectoryPredictionProviderV4
            CreateTrajectoryPredictionProviderV4(MatchContextV4 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var runtimePhysicsHash =
                BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(
                    SimulationParameters);
            if (!string.Equals(
                    context.PhysicsConfigurationHash,
                    runtimePhysicsHash,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "MatchContextV4 PhysicsConfigurationHash does not match the formal runtime physics.",
                    nameof(context));
            }

            return new BallTrajectoryPredictionProviderV4(
                context.TrajectoryPredictionProviderConfiguration);
        }

        public static BallTrajectoryPredictionArtifactV4
            PredictSharedGate5TrajectoryV4(
                BallTrajectoryPredictionProviderV4 provider,
                BallTrajectoryPredictionRequestV4 request,
                ExecutionEnvelopePolicyV4 policy)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            return provider.PredictWithDegradation(
                request,
                policy.DegradationLadder);
        }

        public static ExecutionEnvelopeV4 PlanExecutionEnvelopeV4(
            Volleyball.Shared.Contracts.DerivedMatchAttributesV4 derivedAttributes,
            ExecutionIntentV4 selectedIntent,
            string samplingKey,
            ExecutionEnvelopePolicyV4 policy)
        {
            return ExecutionEnvelopeFactoryV4.Create(
                derivedAttributes,
                selectedIntent,
                samplingKey,
                policy);
        }

        public static ExecutionSampleClassificationV4 ExecuteExecutionSampleV4(
            ExecutionEnvelopeV4 plannedEnvelope,
            ExecutionSampleV4 sample)
        {
            if (plannedEnvelope == null)
            {
                throw new ArgumentNullException(nameof(plannedEnvelope));
            }

            return plannedEnvelope.Classify(sample);
        }

        public bool IsFrontRow(PlayerId player)
        {
            return _set != null && _set.IsFrontRow(StableId(player));
        }

        public int RotationPositionFor(PlayerId player)
        {
            return _set == null ? 0 : _set.RotationPositionFor(StableId(player));
        }

        public void InitializeV4(
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> agents,
            MatchContextV4 context,
            ScoreDisplay scoreDisplay,
            IRallyTacticalWeightSource tacticalWeightSource = null,
            PhysicalMatchConfiguration configuration = null,
            TeamSide firstServingSide = TeamSide.Home,
            int homeInitialRotationOffset = 0,
            int awayInitialRotationOffset = 0)
        {
            var matchContext = context ?? throw new ArgumentNullException(nameof(context));
            if (matchContext.RulesVersion != RulesVersions.FullRallyV3)
            {
                throw new ArgumentException(
                    "Formal match runtime requires the independently versioned V3 rules.",
                    nameof(context));
            }

            _matchContext = matchContext;
            InitializeCore(
                ball,
                agents,
                scoreDisplay,
                tacticalWeightSource,
                configuration ?? PhysicalMatchConfiguration.FormalIndoorSixVsSix,
                matchContext.Home.Players.Count,
                matchContext.Away.Players.Count,
                () =>
                {
                    _formalSet = new MatchSet(
                        matchContext,
                        firstServingSide,
                        _configuration.SetRules,
                        homeInitialRotationOffset,
                        awayInitialRotationOffset);
                    return _formalSet;
                });
        }

        protected void InitializeCore(
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> agents,
            ScoreDisplay scoreDisplay,
            IRallyTacticalWeightSource tacticalWeightSource,
            PhysicalMatchConfiguration configuration,
            int homeRosterSize,
            int awayRosterSize,
            Func<IMatchSetRuntime> createSet)
        {
            _configuration = configuration ?? PhysicalMatchConfiguration.ThreeVsThree;
            _ball = ball != null ? ball : throw new ArgumentNullException(nameof(ball));
            _scoreDisplay = scoreDisplay ?? throw new ArgumentNullException(nameof(scoreDisplay));
            if (agents == null)
            {
                throw new ArgumentNullException(nameof(agents));
            }

            foreach (var agent in agents)
            {
                if (agent == null || !_players.TryAdd(agent.Id, agent))
                {
                    throw new ArgumentException("Unique non-null court agents are required.", nameof(agents));
                }

                _ball.RegisterContactSource(agent);
            }

            var expectedPlayerCount = _configuration.RosterSize * 2;
            if (_players.Count != expectedPlayerCount)
            {
                throw new ArgumentException(
                    $"Exactly {expectedPlayerCount} agents are required for {_configuration.DisplayName}.",
                    nameof(agents));
            }

            if (homeRosterSize != _configuration.RosterSize ||
                awayRosterSize != _configuration.RosterSize)
            {
                throw new ArgumentException("The context roster size does not match the match configuration.", nameof(createSet));
            }

            _set = (createSet ?? throw new ArgumentNullException(nameof(createSet)))();
            _activeTacticalWeights = LocalTacticalWeights();
            _aiDecisionTimeController = GetComponent<AiDecisionTimeController>() ??
                                        gameObject.AddComponent<AiDecisionTimeController>();
            if (tacticalWeightSource != null)
            {
                ConfigureAiDecisionSource(tacticalWeightSource);
            }
            var ballTrail = _ball.GetComponent<TrailRenderer>();
            _blockImpactFeedback = GetComponentInChildren<BlockImpactFeedback>();
            if (_blockImpactFeedback == null)
            {
                _blockImpactFeedback = BlockImpactFeedback.Create(transform, ballTrail);
            }
            else
            {
                _blockImpactFeedback.Initialize(ballTrail);
            }
            ApplyTactics(InitialTactics(), true);
            RenderScore();

            _ball.ContactCandidateResolver = ResolveCandidate;
            _ball.PlayerContactRejected += HandleRejectedPlayerContact;
            _ball.PlayerContact += HandlePlayerContact;
            _ball.EnvironmentContact += HandleEnvironmentContact;
            _ball.NetPlaneCrossed += HandleNetPlaneCrossing;
            _ball.SimulationStepped += HandleSimulationStepped;
            StartCoroutine(StartInitialLoop(0.35f));
        }

        public void ConfigureAiDecisionSource(
            IRallyTacticalWeightSource source,
            float realTimeTimeoutSeconds = AiDecisionTimeController.DefaultRealTimeTimeoutSeconds,
            float minimumTimeScaleFactor = AiDecisionTimeController.DefaultMinimumTimeScaleFactor,
            float safetyReserveSeconds = AiDecisionTimeController.DefaultSafetyReserveSeconds,
            float restoreDurationSeconds = AiDecisionTimeController.DefaultRestoreDurationSeconds,
            float minimumSimulationWindowSeconds =
                AiDecisionTimeController.DefaultMinimumSimulationWindowSeconds)
        {
            _aiDecisionTimeController = GetComponent<AiDecisionTimeController>() ??
                                        gameObject.AddComponent<AiDecisionTimeController>();
            _aiDecisionTimeController.Configure(
                source,
                realTimeTimeoutSeconds,
                minimumTimeScaleFactor,
                safetyReserveSeconds,
                restoreDurationSeconds,
                minimumSimulationWindowSeconds);
        }

        public void ConfigureV3Rules(V3RulesMode mode)
        {
            if (_set == null)
            {
                throw new InvalidOperationException(
                    "V3 rules must be configured after normal match initialization.");
            }
            if (_rallyActive || _restartScheduled || _touchState != null || HasResult)
            {
                throw new InvalidOperationException(
                    "V3 rules must be configured before the first rally starts.");
            }

            if (!Enum.IsDefined(typeof(V3RulesMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (mode == V3RulesMode.Disabled)
            {
                _v3RulesAdapter = null;
                GateHAuthorityEnabled = false;
                GateIAuthorityEnabled = false;
                _formalAuthority.ReceiveCoordinator = null;
                _formalAuthority.ReceiveControllers.Clear();
                _formalAuthority.AttackCoordinator = null;
                _formalAuthority.AttackControllers.Clear();
                _formalAuthority.ClearGateI();
                _pendingV3AuthorityContact = null;
                if (_ball != null)
                {
                    _ball.SelectedContactCommitter = null;
                }
                V3RulesMode = V3RulesMode.Disabled;
                ResetV3Diagnostics();
                return;
            }

            if (_configuration.RosterSize != 6 || _matchContext == null)
            {
                throw new InvalidOperationException(
                    "V3 rules can only be configured for a V4 formal six-player match.");
            }

            if (_matchContext.RulesVersion != RulesVersions.FullRallyV3)
            {
                throw new InvalidOperationException(
                    "The formal V4 context must select V3 rules.");
            }

            var eligibility = CreateV3Eligibility(_matchContext);
            var adapter = new FullRallyV3RulesRuntimeAdapter(
                _matchContext.RulesVersion,
                eligibility,
                _set.ServingSide,
                mode);
            _v3RulesAdapter = adapter;
            V3RulesMode = mode;
            _pendingV3AuthorityContact = null;
            if (_ball != null)
            {
                _ball.SelectedContactCommitter = mode == V3RulesMode.Authority
                    ? CommitSelectedCandidateV3
                    : null;
            }
            GateHAuthorityEnabled =
                mode == V3RulesMode.Authority &&
                _configuration.RosterSize == 6 &&
                _matchContext != null &&
                _v3RulesAdapter != null &&
                _players.Count == 12;
            if (GateHAuthorityEnabled)
            {
                var responsibilityPlanner =
                    _decisionCoordinator.CreateReceiveOrganizationPlanner();
                _formalAuthority.ReceiveCoordinator =
                    new ReceiveOrganizationAuthorityCoordinator(
                        responsibilityPlanner,
                        NullReceiveOrganizationAuthorityCommandSink.Instance);
                _formalAuthority.ReceiveControllers.Clear();
                foreach (var team in new[] { TeamId.Blue, TeamId.Orange })
                {
                    var formalPlayers = _players
                        .Where(pair => pair.Key.Team == team)
                        .Select(pair => pair.Value)
                        .ToArray();
                    var controller =
                        new ReceiveOrganizationAuthorityController(formalPlayers);
                    controller.AuthorityCommitted += HandleGateHAuthorityCommitted;
                    _formalAuthority.ReceiveControllers.Add(team, controller);
                }
            }
            else
            {
                _formalAuthority.ReceiveCoordinator = null;
                _formalAuthority.ReceiveControllers.Clear();
                _formalAuthority.ClearGateH();
            }
            GateIAuthorityEnabled =
                mode == V3RulesMode.Authority && GateHAuthorityEnabled &&
                _configuration.RosterSize == 6 && _players.Count == 12;
            if (GateIAuthorityEnabled)
            {
                _formalAuthority.AttackCoordinator = new AttackDefenseAuthorityCoordinator(
                    new AttackDefensePlanner(), new DirectorAttackDefenseCommandSink(this));
                _formalAuthority.AttackControllers.Clear();
                foreach (var team in new[] { TeamId.Blue, TeamId.Orange })
                {
                    var controller = new AttackDefenseAuthorityController(_players
                        .Where(pair => pair.Key.Team == team).Select(pair => pair.Value).ToArray());
                    controller.AuthorityCommitted += HandleGateIAuthorityCommitted;
                    _formalAuthority.AttackControllers.Add(team, controller);
                }
            }
            else
            {
                _formalAuthority.AttackCoordinator = null;
                _formalAuthority.AttackControllers.Clear();
                _formalAuthority.ClearGateI();
            }
            ResetV3Diagnostics();
        }

        private void ResetV3Diagnostics()
        {
            V3RuleTransitions = 0;
            V3RuleParityMatches = 0;
            V3RuleIntentionalCorrections = 0;
            V3RuleUnexpectedMismatches = 0;
            LastV3RuleDiagnostic = string.Empty;
            ShadowPlanRecordingFailures = 0;
            LastShadowPlanRecordingDiagnostic = string.Empty;
            _lastAcceptedV3Actor = null;
            _lastAcceptedV3Classification = null;
        }

        public void ConfigureInSystemFirstPassCalibration(bool enabled)
        {
            _forceInSystemReceiveExecution = enabled;
        }

        private void OnDestroy()
        {
            _aiDecisionRequestVersion++;
            _aiDecisionTimeController?.CancelPending();
            if (_ball == null)
            {
                return;
            }

            _ball.ContactCandidateResolver = null;
            _ball.SelectedContactCommitter = null;
            _ball.PlayerContactRejected -= HandleRejectedPlayerContact;
            _ball.PlayerContact -= HandlePlayerContact;
            _ball.EnvironmentContact -= HandleEnvironmentContact;
            _ball.NetPlaneCrossed -= HandleNetPlaneCrossing;
            _ball.SimulationStepped -= HandleSimulationStepped;
            foreach (var player in _players.Values)
            {
                if (player != null)
                {
                    _ball.UnregisterContactSource(player);
                }
            }
        }

        private void Update()
        {
            if (GateIAuthorityEnabled || _ball == null)
            {
                return;
            }

            AdvancePresentationLifecycle(_ball.SimulationTime);
        }

        private void HandleSimulationStepped(float simulationTime)
        {
            if (!GateIAuthorityEnabled)
            {
                return;
            }

            AdvancePresentationLifecycle(simulationTime);
        }

        private void AdvancePresentationLifecycle(float simulationTime)
        {
            if (!_rallyActive || _restartScheduled || HasResult)
            {
                return;
            }

            if (TryResolvePostBlockSideFromLiveBall())
            {
                return;
            }

            if (_contactDeadlineActive &&
                simulationTime > _expectedContactTime + 0.35f)
            {
                ExpireActivePostAttackReceiveWindow("ContactTimeout");
            }

            if (_configuration.RosterSize == 6)
            {
                ResolvePlayerCrowding();
            }
        }

        private void ResolvePlayerCrowding()
        {
            const float minimumRootSeparation = 0.32f;
            var players = new List<PrototypePlayerAgent>(_players.Values);
            for (var first = 0; first < players.Count; first++)
            {
                for (var second = first + 1; second < players.Count; second++)
                {
                    var firstPlayer = players[first];
                    var secondPlayer = players[second];
                    if (firstPlayer.Id.Team != secondPlayer.Id.Team)
                    {
                        continue;
                    }

                    var delta = secondPlayer.transform.position - firstPlayer.transform.position;
                    delta.y = 0f;
                    var distance = delta.magnitude;
                    if (distance >= minimumRootSeparation)
                    {
                        continue;
                    }

                    var direction = distance > 0.0001f
                        ? delta / distance
                        : ((firstPlayer.Id.RosterSlot + secondPlayer.Id.RosterSlot) % 2 == 0
                            ? Vector3.right
                            : Vector3.left);
                    var correction = Mathf.Min(0.08f, (minimumRootSeparation - distance) * 0.5f);
                    firstPlayer.ApplyCrowdingOffset(-direction * correction);
                    secondPlayer.ApplyCrowdingOffset(direction * correction);
                    CrowdingCorrections++;
                }
            }
        }

        private void ApplyTactics(PhysicalRallyTactics tactics, bool placePlayers)
        {
            _currentTactics = tactics;
            if (!placePlayers)
            {
                return;
            }

            foreach (var pair in _players)
            {
                pair.Value.PrepareForTraining(TacticalRootTarget(pair.Key));
            }
        }

        private PhysicalRallyTactics InitialTactics()
        {
            return _initialScenarioTactics ?? _tacticPlanner.Create();
        }

        private IEnumerator StartInitialLoop(float delay)
        {
            _rallyActive = false;
            yield return new WaitForSeconds(delay);

            if (_matchContext != null)
            {
                _trajectoryPredictionProviderV4 =
                    CreateTrajectoryPredictionProviderV4(_matchContext);
                _lastTrajectoryPredictionArtifactV4 = null;
            }

            foreach (var pair in _players)
            {
                pair.Value.PrepareForTraining(TacticalRootTarget(pair.Key));
            }

            var receivingTeam = FromSide(_set.ReceivingSide);
            _touchState = new RallyTouchState(receivingTeam);
            if (_v3RulesAdapter != null)
            {
                _v3RulesAdapter.BeginRally(
                    CreateV3Eligibility(_matchContext),
                    _set.ServingSide);
                _pendingV3AuthorityContact = null;
                _lastAcceptedV3Actor = null;
                _lastAcceptedV3Classification = null;
            }
            _scheduledDecision = null;
            _plannedAttackDecision = null;
            _plannedAttackTrajectoryArtifactV4 = null;
            if (GateIAuthorityEnabled)
            {
                // A pending intent belongs to the just-resolved rally and must not
                // survive into the next receive/organization lifecycle.
                _formalAuthority.AttackCoordinator = new AttackDefenseAuthorityCoordinator(
                    new AttackDefensePlanner(), new DirectorAttackDefenseCommandSink(this));
                _formalAuthority.ClearGateI();
            }
            _scheduledPrimaryActor = null;
            _scheduledBlockers.Clear();
            _pendingGateIReceiveWindows.Clear();
            _activePostAttackReceives.Clear();
            _scheduledBlockPrimary = null;
            _awaitingPostBlockCrossing = false;
            _postBlockerTeam = null;
            _postBlockerActor = null;
            _postBlockImpactCenter = SimVector3.Zero;
            _postBlockOutgoing = SimVector3.Zero;
            _postAttackContinuationState = PostAttackContinuationStateV4.None;
            _pendingCrossingTeam = null;
            _contactDeadlineActive = false;
            _lastTouchWasBackSetAttack = false;
            _controlledHandlingActive = false;
            _activeSetChain = false;

            const float initialFlightSeconds = 0.90f;
            var nominalReceiver = FindPlayer(
                receivingTeam,
                role => role == PlayerRole.Defender);
            var receiveCenter = ContactCenter(
                nominalReceiver.PreviewContactFrames(TechniqueAction.Receive),
                TechniqueAction.Receive);
            var arrivalVelocity = receivingTeam == TeamId.Blue
                ? new SimVector3(0f, ServeArrivalVerticalSpeed, -9f)
                : new SimVector3(0f, ServeArrivalVerticalSpeed, 9f);
            var launch = ArrivalLaunchSolver.Solve(
                receiveCenter,
                arrivalVelocity,
                initialFlightSeconds,
                SimulatedBall.DefaultFixedStep,
                SimulationParameters);
            _ball.ResetBall(ToUnity(launch.StartPosition));
            _ball.Launch(ToUnity(launch.InitialVelocity));
            var stableServer = _set.ServerFor(_set.ServingSide);
            if (_configuration.RosterSize == 6)
            {
                FindPlayer(stableServer).Rig.SetPose(StickFigurePose.Serve, 1f);
                _set.RecordContact(stableServer, 0f);
                _status = $"{stableServer.Value} SERVE to {receivingTeam}";
            }
            _rallyActive = true;
            _restartScheduled = false;

            NotifyReplay(
                ReplayServeStarted,
                new ReplaySimpleEvent("Serve", _ball.SimulationTime, FromSide(_set.ServingSide), stableServer));
            BeginPossessionDecision(receivingTeam, initialFlightSeconds);
        }

        private void BeginPossession(TeamId team, float availableSeconds)
        {
            if (!_rallyActive || _restartScheduled)
            {
                return;
            }

            var prematurePostBlockPossession = _awaitingPostBlockCrossing;
            if (prematurePostBlockPossession)
            {
                PrematurePostBlockReceiveWindows++;
            }

            DisableEmergencyReceiveWindows(TeamId.Blue);
            DisableEmergencyReceiveWindows(TeamId.Orange);
            DisablePhysicalBlockWindows();
            _touchState.BeginPossession(team);
            _plannedAttackDecision = null;
            _plannedAttackTrajectoryArtifactV4 = null;
            _controlledHandlingActive = false;
            _activeSetChain = false;
            BeginPossessionDecision(team, availableSeconds);
            if (prematurePostBlockPossession)
            {
                foreach (var player in _players.Values)
                {
                    if (player.EmergencyReceiveWindowEnabled)
                    {
                        PrematurePostBlockEmergencyWindows++;
                    }
                }
            }
            Debug.Log(
                $"[{_configuration.LogTag}] possession team={team} touches=0 " +
                $"available={availableSeconds:0.00}");
        }

        private void BeginPossessionDecision(TeamId team, float availableSeconds)
        {
            _activeTacticalWeights = LocalTacticalWeights();
            var requestVersion = ++_aiDecisionRequestVersion;
            if (_aiDecisionTimeController != null &&
                _aiDecisionTimeController.CanRequest(availableSeconds))
            {
                var requestSimulationTime = _ball.SimulationTime;
                var request = new RallyTacticalWeightRequest(
                    team,
                    RallyDecisionStage.Receive,
                    _tacticRevision,
                    _aiRequestSequence++,
                    _touchState.CountedTeamTouches,
                    availableSeconds,
                    _ball.State.Position,
                    _ball.State.Velocity);
                _status = $"{team} AI THINKING";
                var requested = _aiDecisionTimeController.TryRequestWeights(
                    request,
                    _activeTacticalWeights,
                    (weights, status) => CompletePossessionDecision(
                        team,
                        availableSeconds,
                        requestSimulationTime,
                        requestVersion,
                        weights,
                        status));
                if (requested)
                {
                    if (_aiDecisionTimeController.IsWaiting)
                    {
                        _status = $"{team} AI THINKING  time x" +
                                  $"{_aiDecisionTimeController.LastTargetTimeScale:0.00}";
                    }
                    Debug.Log(
                        $"[{_configuration.LogTag}] ai-wait team={team} available={availableSeconds:0.00} " +
                        $"scale={_aiDecisionTimeController.LastTargetTimeScale:0.00}");
                    return;
                }
            }

            ScheduleReceiveDecision(team, availableSeconds);
        }

        private void CompletePossessionDecision(
            TeamId team,
            float originalAvailableSeconds,
            float requestSimulationTime,
            int requestVersion,
            RallyTacticalWeights weights,
            AiDecisionWaitStatus status)
        {
            if (requestVersion != _aiDecisionRequestVersion ||
                !_rallyActive ||
                _restartScheduled ||
                HasResult ||
                _touchState == null ||
                _touchState.PossessionTeam != team)
            {
                return;
            }

            _activeTacticalWeights = weights;
            var elapsedSimulationSeconds = Mathf.Max(
                0f,
                _ball.SimulationTime - requestSimulationTime);
            var remainingSimulationSeconds = Mathf.Max(
                0.10f,
                originalAvailableSeconds - elapsedSimulationSeconds);
            ScheduleReceiveDecision(team, remainingSimulationSeconds);
            _status = status == AiDecisionWaitStatus.Success
                ? $"{team} AI READY  restoring match speed"
                : $"{team} LOCAL FALLBACK  {status}";
            Debug.Log(
                $"[{_configuration.LogTag}] ai-ready team={team} status={status} " +
                $"wait={_aiDecisionTimeController.LastRealWaitSeconds:0.00}s " +
                $"remaining={remainingSimulationSeconds:0.00}");
        }

        private void ScheduleReceiveDecision(TeamId team, float availableSeconds)
        {
            if (GateHAuthorityEnabled)
            {
                ScheduleGateHReceive(team, availableSeconds);
                if (_tacticRevision == 0 && SuccessfulContacts == 0)
                {
                    _status =
                        $"Serve to {team.ToString().ToUpperInvariant()} possession";
                }

                return;
            }

            var decision = PlanDecision(team, RallyDecisionStage.Receive, availableSeconds);
            ScheduleDecision(decision, availableSeconds);
            if (_tacticRevision == 0 && SuccessfulContacts == 0)
            {
                _status = $"Serve to {team.ToString().ToUpperInvariant()} possession";
            }
        }

        private void ScheduleGateHReceive(TeamId team, float availableSeconds)
        {
            var planning = PlanGateHReceive(team, availableSeconds);
            if (planning == null)
            {
                _scheduledDecision = null;
                _scheduledPrimaryActor = null;
                _contactDeadlineActive = false;
                return;
            }

            ScheduleDecision(
                planning.Decision,
                Mathf.Max(0.10f, availableSeconds));
        }

        private ReceiveOrganizationPlanningResult PlanGateHReceive(
            TeamId team,
            float availableSeconds,
            StablePlayerId? committedContinuationReceiver = null)
        {
            var receiveSeconds = Mathf.Max(0.10f, availableSeconds);
            var playerSnapshots = CaptureTeamPlayerSnapshots(team);
            var receiveInput = _decisionCoordinator.CreateInput(
                team,
                TacticFor(team),
                playerSnapshots,
                PredictGate5BallCenterV4(
                    team,
                    RallyDecisionStage.Receive,
                    receiveSeconds),
                receiveSeconds,
                BaseMovementSpeed,
                _touchState.CountedTeamTouches,
                _touchState.LastCountedActor,
                _tacticRevision,
                RallyDecisionStage.Receive,
                _activeTacticalWeights);
            if (!_decisionCoordinator.HasFeasibleCandidate(receiveInput))
            {
                return null;
            }

            var organizationInput = _decisionCoordinator.CreateInput(
                team,
                TacticFor(team),
                playerSnapshots,
                PredictGate5BallCenterV4(
                    team,
                    RallyDecisionStage.Organize,
                    ReceiveFlightSeconds),
                ReceiveFlightSeconds,
                BaseMovementSpeed,
                1,
                null,
                _tacticRevision,
                RallyDecisionStage.Organize,
                _activeTacticalWeights);
            var attackFlightSeconds = SetFlightSolver.PreferredFlightSeconds(
                TacticFor(team).SetRhythm);
            var attackInput = _decisionCoordinator.CreateInput(
                team,
                TacticFor(team),
                playerSnapshots,
                PredictGate5BallCenterV4(
                    team,
                    RallyDecisionStage.Attack,
                    attackFlightSeconds),
                attackFlightSeconds,
                BaseMovementSpeed,
                2,
                FindPlayer(team, role => role == PlayerRole.Setter).Id,
                _tacticRevision,
                RallyDecisionStage.Attack,
                _activeTacticalWeights);
            var bindings = _players
                .Where(pair => pair.Key.Team == team)
                .OrderBy(pair => pair.Key.RosterSlot)
                .Select(pair => new ReceiveOrganizationPlayerBindingV3(
                    pair.Key,
                    pair.Value.StableId))
                .ToArray();
            var request = new ReceiveOrganizationAuthorityRequestV3(
                _formalAuthority.NextPlanRevision(),
                _formalAuthority.NextSourceSequence(),
                receiveInput,
                organizationInput,
                attackInput,
                CreateV3Eligibility(_matchContext),
                bindings);
            _formalAuthority.ReceiveCoordinator.PlanReceive(
                request,
                committedContinuationReceiver);
            var planning = _formalAuthority.ReceiveCoordinator.CurrentPlanning;
            if (GateJEnabled && _lastTrajectoryPredictionArtifactV4 != null)
            {
                _formalAuthority.ReceiveCoordinator.ApplyPerception(
                    CreateGateJPerceptionReceipt(
                        team,
                        request.Revision,
                        request.SourceSequence,
                        _lastTrajectoryPredictionArtifactV4.ArtifactIdentity,
                        null,
                        planning.Plan.EmergencyReceivers
                            .Concat(planning.Plan.BackupOrganizers)
                            .Append(planning.Plan.PrimaryReceiver)
                            .Distinct()
                            .ToArray(),
                        planning.Plan.PrimaryReceiver,
                        false));
            }
            return planning;
        }

        private RallyTacticalWeights LocalTacticalWeights()
        {
            if (_initialScenarioAiWeights.HasValue)
            {
                return _initialScenarioAiWeights.Value;
            }

            var rolePreference = _tacticRevision % 4 == 3 ? 0.35f : 1f;
            return new RallyTacticalWeights(rolePreference, 1.15f, 1f, 1f);
        }

        private TeamRallyDecision PlanDecision(
            TeamId team,
            RallyDecisionStage stage,
            float availableSeconds)
        {
            return PlanDecisionAt(
                team,
                stage,
                Mathf.Max(0.10f, availableSeconds),
                PredictGate5BallCenterV4(
                    team,
                    stage,
                    Mathf.Max(0.10f, availableSeconds)));
        }

        private TeamRallyDecision PlanDecisionAt(
            TeamId team,
            RallyDecisionStage stage,
            float availableSeconds,
            SimVector3 predictedBallCenter)
        {
            return PlanDecisionAt(
                team,
                stage,
                availableSeconds,
                predictedBallCenter,
                _touchState.CountedTeamTouches,
                _touchState.LastCountedActor);
        }

        private TeamRallyDecision PlanDecisionAt(
            TeamId team,
            RallyDecisionStage stage,
            float availableSeconds,
            SimVector3 predictedBallCenter,
            int countedTouches,
            PlayerId? lastCountedActor)
        {
            var input = _decisionCoordinator.CreateInput(
                team,
                TacticFor(team),
                CaptureTeamPlayerSnapshots(team),
                predictedBallCenter,
                availableSeconds,
                BaseMovementSpeed,
                countedTouches,
                lastCountedActor,
                _tacticRevision,
                stage,
                _activeTacticalWeights);
            var decision = _decisionCoordinator.Plan(input);
            if (!decision.HasDecision)
            {
                Debug.Log(
                    $"[{_configuration.LogTag}] decision=none team={team} stage={stage} " +
                    $"touches={_touchState.CountedTeamTouches}");
                return decision;
            }

            Debug.Log(
                $"[{_configuration.LogTag}] decision team={team} stage={stage} actor={decision.Actor.Role} " +
                $"score={decision.Score.Total:0.00} reach={decision.Score.Reachability:0.00} " +
                $"role={decision.Score.NominalRole:0.00} approach={decision.Score.Approach:0.00} " +
                $"angle={decision.Score.Angle:0.00}");
            var organizationDiagnostic = stage == RallyDecisionStage.Organize
                ? CreateOrganizationDiagnostic(team, decision, predictedBallCenter, lastCountedActor)
                : null;
            NotifyReplay(
                ReplayDecisionPlanned,
                ReplayDecisionEvent.Create(
                    _ball.SimulationTime,
                    stage,
                    team,
                    availableSeconds,
                    predictedBallCenter,
                    _activeTacticalWeights,
                    decision,
                    organizationDiagnostic));
            return decision;
        }

        private IReadOnlyList<RallyPlayerSnapshot> CaptureTeamPlayerSnapshots(
            TeamId team)
        {
            var players = new List<RallyPlayerSnapshot>(_configuration.RosterSize);
            foreach (var pair in _players)
            {
                var id = pair.Key;
                if (id.Team != team)
                {
                    continue;
                }

                var player = pair.Value;
                players.Add(new RallyPlayerSnapshot(
                    id,
                    ToSimulation(player.transform.position),
                    player.Ability));
            }

            return players;
        }

        private ReplayOrganizationDecisionDiagnostic CreateOrganizationDiagnostic(
            TeamId team,
            TeamRallyDecision decision,
            SimVector3 firstPassLanding,
            PlayerId? lastCountedActor)
        {
            var target = SetterOrganizationZone.DefaultWorldTarget(team);
            var assessment = SetterOrganizationZone.AssessWorldTarget(team, firstPassLanding);
            var setter = FindPlayer(team, role => role == PlayerRole.Setter);
            var setterCandidate = FindCandidate(decision.Candidates, setter.Id);
            var setterWasPreviousTouch = lastCountedActor.HasValue && lastCountedActor.Value.Equals(setter.Id);
            var setterReachStatus = setterWasPreviousTouch
                ? "PreviousTouch"
                : setterCandidate.IsFeasible ? "Reachable" : "Unreachable";
            var fallbackReason = decision.Actor.Equals(setter.Id)
                ? string.Empty
                : setterWasPreviousTouch ? "SetterPreviousTouch" : "SetterUnreachable";
            var settingContactCenter = NextContactCenter(team, TechniqueAction.Set);
            var settingRoot = setter.ResolveContactRootTarget(
                TechniqueAction.Set,
                settingContactCenter,
                ToUnity(target));
            var movement = Vector3.Distance(setter.transform.position, settingRoot);
            return new ReplayOrganizationDecisionDiagnostic(
                target,
                firstPassLanding,
                CombineOrganizationGrade(assessment),
                setter.Id,
                setterReachStatus,
                movement,
                decision.Actor,
                fallbackReason);
        }

        private static RallyDecisionCandidate FindCandidate(
            IReadOnlyList<RallyDecisionCandidate> candidates,
            PlayerId player)
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].Actor.Equals(player))
                {
                    return candidates[index];
                }
            }

            throw new InvalidOperationException("Organization decision did not include the registered setter.");
        }

        private static SetterOrganizationZoneGrade CombineOrganizationGrade(
            SetterOrganizationZoneAssessment assessment)
        {
            if (assessment.LateralGrade == SetterOrganizationZoneGrade.Poor ||
                assessment.DepthGrade == SetterOrganizationZoneGrade.Poor)
            {
                return SetterOrganizationZoneGrade.Poor;
            }

            return assessment.LateralGrade == SetterOrganizationZoneGrade.Best &&
                   assessment.DepthGrade == SetterOrganizationZoneGrade.Best
                ? SetterOrganizationZoneGrade.Best
                : SetterOrganizationZoneGrade.Secondary;
        }

        private void ScheduleDecision(
            TeamRallyDecision decision,
            float flightSeconds,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact = null)
        {
            trajectoryArtifact ??= _lastTrajectoryPredictionArtifactV4;
            if (GateIAuthorityEnabled && decision != null && decision.HasDecision &&
                decision.Action == TechniqueAction.Set)
            {
                var intent = PlanGateISetIntent(decision, flightSeconds, trajectoryArtifact);
                decision = new TeamRallyDecision(decision.Actor, TechniqueAction.Set,
                    intent.Intent.Target, decision.MovementTarget, intent.Intent.Target,
                    decision.Score, decision.Candidates, null);
                trajectoryArtifact = intent.Intent.TrajectoryArtifact;
            }
            _scheduledDecision = decision;
            _scheduledPrimaryActor = null;
            _contactDeadlineActive = false;
            if (decision == null || !decision.HasDecision)
            {
                return;
            }

            var actor = _players[decision.Actor];
            if (decision.Action == TechniqueAction.Set)
            {
                actor.SetPreparedFacing(
                    new TeamCourtFrame(decision.Actor.Team),
                    TacticFor(decision.Actor.Team).SetRoute);
            }
            var predictedContactCenter = PredictBallCenter(flightSeconds);
            if (decision.Action == TechniqueAction.Set)
            {
                var attackFlight = SetFlightSolver.PreferredFlightSeconds(
                    TacticFor(decision.Actor.Team).SetRhythm);
                _plannedAttackDecision = GateIAuthorityEnabled
                    ? TeamRallyDecision.NoDecision
                    : GateHAuthorityEnabled
                    ? _formalAuthority.ReceiveCoordinator.CurrentPlanning
                        .AttackPreparationDecision
                    : PlanDecisionAt(
                        decision.Actor.Team,
                        RallyDecisionStage.Attack,
                        attackFlight,
                        decision.BallTarget,
                        _touchState.CountedTeamTouches + 1,
                        decision.Actor);
                _plannedAttackTrajectoryArtifactV4 = trajectoryArtifact;
                if (_plannedAttackDecision.HasDecision)
                {
                    Debug.Log(
                        $"[{_configuration.LogTag}] attack-preplan team={decision.Actor.Team} " +
                        $"actor={_plannedAttackDecision.Actor.Role} " +
                        $"takeoff=({_plannedAttackDecision.MovementTarget.X:0.00}," +
                        $"{_plannedAttackDecision.MovementTarget.Y:0.00}," +
                        $"{_plannedAttackDecision.MovementTarget.Z:0.00})");
                }
            }
            var authoritativeContactCenter = decision.AttackContactPlan?.ContactCenter ?? predictedContactCenter;
            var outgoingTarget = OutgoingTargetFor(decision);
            SimVector3 outgoing;
            if (decision.Action == TechniqueAction.Set)
            {
                _scheduledGeometricSetTarget = null;
                try
                {
                    if (!GateIAuthorityEnabled)
                    {
                        if (_plannedAttackDecision == null ||
                            !_plannedAttackDecision.HasDecision)
                        {
                            throw new InvalidOperationException(
                                "No planned attacker is available for target scoring.");
                        }

                        var setter = _players[decision.Actor];
                        var setterPosition = ToSimulation(
                            setter.transform.position);
                        var setterDepth = -new TeamCourtFrame(
                                decision.Actor.Team)
                            .ToLocal(setterPosition).Z;
                        var preferredX = outgoingTarget.X;
                        var setTargetSelection =
                            _decisionCoordinator.SelectSetTarget(
                                new SetTargetSelectionInput(
                                    decision.Actor.Team,
                                    _plannedAttackDecision.Actor.Role,
                                    Mathf.Max(0f, setterDepth),
                                    outgoingTarget.Y,
                                    preferredX,
                                    PredictedBlockArmFrames(
                                        decision.Actor.Team,
                                        outgoingTarget,
                                        _ball.SimulationTime +
                                        flightSeconds +
                                        SetFlightSolver
                                            .PreferredFlightSeconds(
                                                TacticFor(
                                                    decision.Actor.Team)
                                                    .SetRhythm)),
                                    new[]
                                    {
                                        Mathf.Clamp(
                                            preferredX - 0.9f,
                                            -4.2f,
                                            4.2f),
                                        preferredX,
                                        Mathf.Clamp(
                                            preferredX + 0.9f,
                                            -4.2f,
                                            4.2f)
                                    }));
                        outgoingTarget = setTargetSelection.Target;
                        _scheduledGeometricSetTarget = outgoingTarget;
                        GeometricSetTargetSelections++;
                        Debug.Log(
                            $"[{_configuration.LogTag}] geometric-set-target " +
                            $"team={decision.Actor.Team} target=(" +
                            $"{outgoingTarget.X:0.00},{outgoingTarget.Y:0.00}," +
                            $"{outgoingTarget.Z:0.00}) clearance=" +
                            $"{setTargetSelection.MinimumArmClearance:0.000}");
                    }
                }
                catch (InvalidOperationException exception)
                {
                    Debug.Log(
                        $"[{_configuration.LogTag}] geometric-set-fallback " +
                        $"team={decision.Actor.Team} reason={exception.Message}");
                }

                if (GateIAuthorityEnabled &&
                    _formalAuthority.ActiveSetIntent != null)
                {
                    // Gate H remains the Set contact writer, but executes Gate I's
                    // immutable physical target/velocity exactly.
                    outgoing = _formalAuthority.ActiveSetIntent.Intent
                        .ExecutionClassification.ExecutableSample.Velocity;
                    _scheduledSetFlightSeconds =
                        _formalAuthority.ActiveSetIntent.Intent.SetFlightSeconds;
                }
                else
                {
                    var readiness = _plannedAttackDecision?.AttackContactPlan?.ApproachCompletion ?? 0.5f;
                    var setSolution = SolveSetFlightWithFallback(new SetFlightRequest(
                        TacticFor(decision.Actor.Team).SetRhythm,
                        authoritativeContactCenter,
                        outgoingTarget,
                        actor.Ability.SetTechnique,
                        readiness,
                        SimulationParameters,
                        SimulatedBall.DefaultFixedStep));
                    outgoing = setSolution.InitialVelocity;
                    _scheduledSetFlightSeconds = setSolution.FlightSeconds;
                }
            }
            else if (decision.Action == TechniqueAction.Attack)
            {
                try
                {
                    var route = AttackRouteSelector.Select(new AttackRouteSelectionInput(
                        decision.Actor.Team,
                        authoritativeContactCenter,
                        OutgoingFlightSecondsFor(decision.Actor.Team, decision.Action),
                        PredictedBlockArmFrames(
                            decision.Actor.Team,
                            authoritativeContactCenter,
                            _ball.SimulationTime + flightSeconds),
                        SimulationParameters,
                        SimulatedBall.DefaultFixedStep));
                    outgoingTarget = route.Target;
                    outgoing = route.InitialVelocity;
                    RecordGeometricAttackRoute(route.Route);
                    Debug.Log(
                        $"[{_configuration.LogTag}] geometric-attack-route " +
                        $"team={decision.Actor.Team} route={route.Route} " +
                        $"clearance={route.MinimumArmClearance:0.000}");
                }
                catch (InvalidOperationException exception)
                {
                    Debug.Log(
                        $"[{_configuration.LogTag}] geometric-attack-fallback " +
                        $"team={decision.Actor.Team} reason={exception.Message}");
                    var outgoingFlightSeconds = OutgoingFlightSecondsFor(
                        decision.Actor.Team,
                        decision.Action);
                    outgoing = ReturnVelocitySolver.Solve(
                        authoritativeContactCenter,
                        outgoingTarget,
                        outgoingFlightSeconds,
                        SimulatedBall.DefaultFixedStep,
                        SimulationParameters).InitialVelocity;
                }
            }
            else
            {
                var outgoingFlightSeconds = OutgoingFlightSecondsFor(decision.Actor.Team, decision.Action);
                outgoing = ReturnVelocitySolver.Solve(
                    authoritativeContactCenter,
                    outgoingTarget,
                    outgoingFlightSeconds,
                    SimulatedBall.DefaultFixedStep,
                    SimulationParameters).InitialVelocity;
            }
            var executionCandidateCategory = ToExecutionCandidateCategoryV4(decision.Action);
            var executionIntentIdentity =
                $"execution:{(_matchContext == null ? "prototype" : _matchContext.SessionId.ToString("D"))}:" +
                $"{_tacticRevision}:{_decisionCoordinator.DecisionIndex}:{SuccessfulContacts}:{(int)decision.Actor.Team}:" +
                $"{(int)decision.Actor.Role}:{decision.Actor.RosterSlot}:{(int)decision.Action}";
            var executionSamplingKey = executionIntentIdentity + ":sample";
            var plannedExecutionEnvelope = PlanExecutionEnvelopeV4(
                actor.Ability.Derived,
                new ExecutionIntentV4(
                    executionIntentIdentity,
                    executionCandidateCategory,
                    outgoingTarget,
                    outgoing,
                    requestedEffort: 0.6f),
                executionSamplingKey,
                ExecutionEnvelopePolicyV4.Default);
            _lastPlannedExecutionEnvelopeV4 = plannedExecutionEnvelope;
            var execution = _forceInSystemReceiveExecution &&
                            decision.Action == TechniqueAction.Receive
                ? InSystemReceiveExecution()
                : SkillExecutionResolver.Resolve(
                    actor.Ability,
                    decision.Action,
                    StablePlayerNumber(decision.Actor),
                    _tacticRevision,
                    SuccessfulContacts,
                    7351,
                    0.72f);
            ExecutionErrorApplications++;
            var executionSample = new ExecutionSampleV4(
                plannedExecutionEnvelope.Identity,
                executionSamplingKey,
                executionCandidateCategory,
                outgoingTarget + execution.ContactPositionError,
                (outgoing * execution.SurfaceSpeedScale) + execution.TargetVelocityError,
                effort: plannedExecutionEnvelope.RequestedEffort);
            _lastExecutionSampleClassificationV4 = ExecuteExecutionSampleV4(
                plannedExecutionEnvelope,
                executionSample);
            if (_lastExecutionSampleClassificationV4.Kind ==
                    ExecutionSampleClassificationKindV4.UnexpectedExecutionSample ||
                _lastExecutionSampleClassificationV4.Kind ==
                    ExecutionSampleClassificationKindV4.EnvelopeExceeded)
            {
                return;
            }

            _expectedContactTime = _ball.SimulationTime + flightSeconds;
            if (GateHAuthorityEnabled &&
                (decision.Action == TechniqueAction.Receive ||
                 decision.Action == TechniqueAction.Set))
            {
                ScheduleGateHDecision(
                    decision,
                    execution,
                    outgoing,
                    trajectoryArtifact,
                    authoritativeContactCenter);
                return;
            }

            if (decision.Action == TechniqueAction.Receive)
            {
                if (_configuration.RosterSize == 6 || _tacticRevision % 4 != 3)
                {
                    PrepareSetterForReceive(decision);
                }
                PrepareAttackerForReceive(decision, flightSeconds);
            }
            if (decision.Action == TechniqueAction.Set &&
                _plannedAttackDecision != null &&
                _plannedAttackDecision.HasDecision &&
                _plannedAttackDecision.AttackApproach.HasValue &&
                _plannedAttackDecision.AttackContactPlan.HasValue)
            {
                var plannedAttacker = _players[_plannedAttackDecision.Actor];
                var approach = _plannedAttackDecision.AttackApproach.Value;
                var contactPlan = _plannedAttackDecision.AttackContactPlan.Value;
                var movementSpeed = BaseMovementSpeed *
                                    (0.65f + (plannedAttacker.Ability.Mobility * 0.5f));
                var reactionReserve = SkillExecutionResolver.MaximumReactionDelaySeconds *
                                      (1f - plannedAttacker.Ability.Reaction) * 0.72f;
                var jumpLead = Mathf.Lerp(0.24f, 0.38f, contactPlan.JumpTiming) +
                               reactionReserve;
                var stagingTarget = AttackApproachStaging.TargetAtSetContact(
                    approach,
                    _scheduledSetFlightSeconds,
                    movementSpeed,
                    jumpLead);
                plannedAttacker.ScheduleAttackPreparation(
                    _expectedContactTime,
                    ToUnity(stagingTarget),
                    _ball.SimulationTime);
                MovementAssignments++;
            }

            var movementTarget = ToUnity(decision.MovementTarget);
            if (decision.Action == TechniqueAction.Receive)
            {
                movementTarget = actor.ResolveContactRootTarget(
                    decision.Action,
                    authoritativeContactCenter,
                    movementTarget);
            }
            actor.ScheduleContact(
                decision.Action,
                _expectedContactTime,
                _lastExecutionSampleClassificationV4,
                execution,
                NextContactGroup(),
                authoritativeContactCenter,
                movementTarget: movementTarget,
                movementStartSimulationTime: _ball.SimulationTime,
                attackApproach: decision.AttackApproach,
                attackContactPlan: decision.AttackContactPlan,
                normalSetRoute: decision.Action == TechniqueAction.Set
                    ? TacticFor(decision.Actor.Team).SetRoute
                    : (SetRoute?)null,
                trajectoryArtifact: trajectoryArtifact);
            _scheduledPrimaryActor = decision.Actor;
            MovementAssignments++;
            TotalMovementShortfall += actor.MovementShortfall;

            var eligibleActors = new List<PlayerId> { decision.Actor };
            if (decision.Action == TechniqueAction.Receive)
            {
                var emergencyCandidates = 0;
                foreach (var candidate in decision.Candidates)
                {
                    if (!candidate.IsFeasible || candidate.Actor.Equals(decision.Actor) ||
                        emergencyCandidates >= 2)
                    {
                        continue;
                    }

                    eligibleActors.Add(candidate.Actor);
                    _players[candidate.Actor].EnableEmergencyReceiveWindow(
                        _expectedContactTime - ContactWindowLead,
                        _expectedContactTime + ContactWindowTail,
                        outgoing,
                        NextContactGroup());
                    EmergencyReceiveWindowAssignments++;
                    emergencyCandidates++;
                }
            }

            _touchState.OpenWindow(new RallyContactWindow(
                decision.Actor.Team,
                decision.Action,
                _expectedContactTime - ContactWindowLead,
                _expectedContactTime + ContactWindowTail,
                eligibleActors));
            _contactDeadlineActive = true;

            if (decision.Action == TechniqueAction.Attack)
            {
                PreparePhysicalBlock(
                    decision.Actor.Team,
                    authoritativeContactCenter,
                    outgoing,
                    _expectedContactTime);
            }
        }

        private void ScheduleGateHDecision(
            TeamRallyDecision decision,
            SkillExecutionError executionError,
            SimVector3 outgoing,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact,
            SimVector3 authoritativeContactCenter)
        {
            var planning = _formalAuthority.ReceiveCoordinator.CurrentPlanning;
            var state = _formalAuthority.ReceiveCoordinator.State;
            var contactGroup = NextContactGroup();
            var gateIIntent = GateIAuthorityEnabled && decision.Action == TechniqueAction.Set
                ? _formalAuthority.ActiveSetIntent?.Intent : null;
            // Gate H still owns the Set contact and timing, while its Gate I
            // receipt carries an already-solved immutable velocity.  Applying a
            // second skill-error perturbation would make its physical endpoint
            // differ from the intent's envelope/trajectory evidence.
            var gateISetExecutionError = gateIIntent != null ? default : executionError;
            var payload = new ReceiveOrganizationCommandExecutionV4(
                _expectedContactTime,
                _ball.SimulationTime,
                gateISetExecutionError,
                contactGroup,
                gateIIntent?.ExecutionClassification ?? _lastExecutionSampleClassificationV4,
                gateIIntent?.TrajectoryArtifact ?? trajectoryArtifact,
                _expectedContactTime - ContactWindowLead,
                _expectedContactTime + ContactWindowTail,
                outgoing,
                authoritativeContactCenter);
            var commands = new List<ReceiveOrganizationAuthorityCommand>();
            var stableActor = StableId(decision.Actor);
            if (decision.Action == TechniqueAction.Receive)
            {
                commands.Add(new ReceiveOrganizationAuthorityCommand(
                    state.Revision,
                    _formalAuthority.CurrentSourceSequence,
                    ReceiveOrganizationCommandKind.PrimaryReceive,
                    stableActor,
                    RallyPlanBranchV3.Primary,
                    decision,
                    false,
                    payload));
                for (var index = 0;
                     index < planning.Plan.EmergencyReceivers.Count;
                     index++)
                {
                    commands.Add(new ReceiveOrganizationAuthorityCommand(
                        state.Revision,
                        _formalAuthority.CurrentSourceSequence,
                        ReceiveOrganizationCommandKind.EmergencyReceive,
                        planning.Plan.EmergencyReceivers[index],
                        RallyPlanBranchV3.Contingency,
                        TeamRallyDecision.NoDecision,
                        false,
                        new ReceiveOrganizationCommandExecutionV4(
                            _expectedContactTime,
                            _ball.SimulationTime,
                            executionError,
                            NextContactGroup(),
                            _lastExecutionSampleClassificationV4,
                            trajectoryArtifact,
                            _expectedContactTime - ContactWindowLead,
                            _expectedContactTime + ContactWindowTail,
                            outgoing)));
                }

                commands.Add(new ReceiveOrganizationAuthorityCommand(
                    state.Revision,
                    _formalAuthority.CurrentSourceSequence,
                    ReceiveOrganizationCommandKind.SetterPreparation,
                    planning.Plan.RegisteredSetter,
                    RallyPlanBranchV3.Primary,
                    TeamRallyDecision.NoDecision,
                    false,
                    payload));
            }
            else
            {
                commands.Add(new ReceiveOrganizationAuthorityCommand(
                    state.Revision,
                    _formalAuthority.CurrentSourceSequence,
                    ReceiveOrganizationCommandKind.OrganizationContact,
                    stableActor,
                    stableActor.Equals(planning.Plan.RegisteredSetter)
                        ? RallyPlanBranchV3.Primary
                        : RallyPlanBranchV3.Contingency,
                    decision,
                    false,
                    payload,
                    gateIIntent));
            }

            if (!GateIAuthorityEnabled && planning.AttackPreparationDecision.HasDecision)
            {
                commands.Add(new ReceiveOrganizationAuthorityCommand(
                    state.Revision,
                    _formalAuthority.CurrentSourceSequence,
                    ReceiveOrganizationCommandKind.AttackPreparation,
                    planning.Plan.AttackPreparation,
                    RallyPlanBranchV3.Primary,
                    planning.AttackPreparationDecision,
                    false,
                    payload));
            }

            var evidence = new ReceiveOrganizationAuthorityEvidenceV3(
                state.Revision,
                _formalAuthority.CurrentSourceSequence,
                state.Phase,
                planning.Plan,
                planning.SetterEvidence,
                planning.FallbackReason,
                state.CoverageDecision,
                state.ActualFirstPassLanding,
                _formalAuthority.ReceiveCoordinator.CurrentPerception);
            _formalAuthority.ReceiveControllers[decision.Actor.Team]
                .PreflightAndCommit(new ReceiveOrganizationCommandBatch(
                    state.Revision,
                    _formalAuthority.CurrentSourceSequence,
                    commands,
                    evidence));
            _scheduledPrimaryActor = decision.Actor;
            MovementAssignments += commands.Count(command =>
                command.Kind !=
                ReceiveOrganizationCommandKind.EmergencyReceive);
            EmergencyReceiveWindowAssignments += commands.Count(command =>
                command.Kind ==
                ReceiveOrganizationCommandKind.EmergencyReceive);
            TotalMovementShortfall += _players[decision.Actor].MovementShortfall;

            var eligibleActors = new List<PlayerId> { decision.Actor };
            if (decision.Action == TechniqueAction.Receive)
            {
                foreach (var emergency in planning.Plan.EmergencyReceivers)
                {
                    eligibleActors.Add(PlayerForStableId(emergency).Id);
                }
            }

            _touchState.OpenWindow(new RallyContactWindow(
                decision.Actor.Team,
                decision.Action,
                _expectedContactTime - ContactWindowLead,
                _expectedContactTime + ContactWindowTail,
                eligibleActors));
            _contactDeadlineActive = true;
        }

        private GateISetIntentPlanningResultV3 PlanGateISetIntent(
            TeamRallyDecision gateHDecision, float flightSeconds,
            BallTrajectoryPredictionArtifactV4 passPrediction)
        {
            if (_formalAuthority.AttackCoordinator == null || _trajectoryPredictionProviderV4 == null ||
                _matchContext == null)
                throw new InvalidOperationException("Formal Gate I requires initialized authority facts.");
            if (_formalAuthority.AttackCoordinator.State.Phase != AttackDefenseAuthorityPhaseV3.Idle)
                throw new InvalidOperationException("Gate I cannot plan a second SetIntent before the accepted Set.");
            var side = ToSide(gateHDecision.Actor.Team);
            var organizer = _players[gateHDecision.Actor];
            var players = _players.OrderBy(pair => pair.Key.Team).ThenBy(pair => pair.Key.RosterSlot)
                .Select(pair => new GateITacticalPlayerV3(pair.Value.StableId, ToSide(pair.Key.Team),
                    ToSimulation(pair.Value.transform.position),
                    pair.Key.Team == gateHDecision.Actor.Team &&
                    !pair.Value.StableId.Equals(organizer.StableId) &&
                    _v3RulesAdapter.Eligibility.For(pair.Value.StableId)
                        .CanAttackAboveNetFromFrontZone,
                    _v3RulesAdapter.Eligibility.For(pair.Value.StableId).CanBlock,
                    pair.Value.Ability.Derived)).ToArray();
            var result = _formalAuthority.AttackCoordinator.PlanSetIntent(new SetIntentPlanningRequestV3(
                _formalAuthority.CurrentPlanRevision, _formalAuthority.NextSourceSequence(), side, organizer.StableId,
                _ball.SimulationTime + flightSeconds, PredictBallState(flightSeconds), players, organizer.Ability.Derived,
                passPrediction ?? _lastTrajectoryPredictionArtifactV4 ?? throw new InvalidOperationException("Accepted pass trajectory is required."),
                _trajectoryPredictionProviderV4, SimulationParameters, _matchContext.PhysicsConfigurationHash,
                _formalAuthority.CurrentSourceSequence));
            _formalAuthority.ActiveSetIntent = result;
            _formalAuthority.StoreGateISetIntent(
                GateHReceiptKey(organizer.StableId, TechniqueAction.Set),
                result.Receipt);
            GateISetIntentCommitted?.Invoke(result.Receipt);
            return result;
        }

        private void HandleGateHAuthorityCommitted(
            ReceiveOrganizationAuthorityReceipt receipt)
        {
            if (receipt.Kind ==
                    ReceiveOrganizationCommandKind.PrimaryReceive ||
                receipt.Kind ==
                    ReceiveOrganizationCommandKind.EmergencyReceive ||
                receipt.Kind ==
                    ReceiveOrganizationCommandKind.OrganizationContact)
            {
                _formalAuthority.StoreGateH(
                    GateHReceiptKey(receipt.Actor, receipt.Action),
                    receipt);
            }

            ReceiveOrganizationAuthorityCommitted?.Invoke(receipt);
        }

        private ReceiveOrganizationAuthorityReceipt TakeGateHContactReceipt(
            StablePlayerId actor,
            TechniqueAction action)
        {
            if (!GateHAuthorityEnabled)
            {
                return null;
            }

            var key = GateHReceiptKey(actor, action);
            return _formalAuthority.TakeGateH(key);
        }

        private GateISetIntentReceiptV3 TakeGateISetIntentReceipt(StablePlayerId actor)
        {
            var key = GateHReceiptKey(actor, TechniqueAction.Set);
            return _formalAuthority.TakeGateISetIntent(key);
        }

        private void HandleGateIAuthorityCommitted(AttackDefenseAuthorityReceipt receipt)
        {
            if (receipt.Kind == AttackDefenseCommandKind.AttackContact ||
                receipt.Kind == AttackDefenseCommandKind.BlockContact ||
                receipt.Kind == AttackDefenseCommandKind.FloorDefense ||
                receipt.Kind == AttackDefenseCommandKind.AttackCover)
            {
                var key = GateIReceiptKey(receipt.Actor, receipt.Kind);
                _formalAuthority.StoreGateIContact(key, receipt);
            }
            AttackDefenseAuthorityCommitted?.Invoke(receipt);
        }

        private AttackDefenseAuthorityReceipt TakeGateIContactReceipt(
            StablePlayerId actor, AttackDefenseCommandKind kind)
        {
            var key = GateIReceiptKey(actor, kind);
            return _formalAuthority.TakeGateIContact(key);
        }

        private AttackDefenseAuthorityReceipt TakeGateIContactReceiptForAction(
            StablePlayerId actor, TechniqueAction action)
        {
            var kind = action == TechniqueAction.Attack
                ? AttackDefenseCommandKind.AttackContact
                : action == TechniqueAction.Block
                    ? AttackDefenseCommandKind.BlockContact
                    : action == TechniqueAction.Receive
                        ? _activePostAttackReceives.FirstOrDefault(receipt =>
                            receipt.Actor.Equals(actor))?.Kind ??
                          AttackDefenseCommandKind.AttackCover
                        : (AttackDefenseCommandKind?)null;
            return kind.HasValue
                ? TakeGateIContactReceipt(actor, kind.Value)
                : null;
        }

        private AttackDefenseAuthorityReceipt CreateIncidentalGateIDefenseReceipt(
            StablePlayerId actor,
            ExecutionSampleClassificationV4 classification,
            BallTrajectoryPredictionArtifactV4 trajectory)
        {
            if (classification == null || trajectory == null)
                throw new InvalidOperationException(
                    "Incidental Gate I defense requires accepted execution evidence.");
            var plan = _formalAuthority.AttackCoordinator.State.Plan;
            var responsibility = plan?.Defense.Responsibilities.FirstOrDefault(
                value => value.Actor.Equals(actor));
            if (responsibility == null)
                throw new InvalidOperationException(
                    "Incidental Gate I defense actor is outside the committed roster.");
            var sourceSequence = _formalAuthority.PeekNextSourceSequence();
            var evidence = _formalAuthority.AttackCoordinator
                .PreviewIncidentalDefenseContact(
                    plan.Revision,
                    sourceSequence,
                    actor,
                    responsibility.Branch,
                    classification.ExecutableEnvelope.Identity,
                    trajectory.ArtifactIdentity,
                    true);
            return new AttackDefenseAuthorityReceipt(
                plan.Revision,
                sourceSequence,
                evidence.Phase,
                AttackDefenseCommandKind.FloorDefense,
                actor,
                responsibility.Branch,
                classification,
                trajectory,
                evidence);
        }

        private static string GateIReceiptKey(StablePlayerId actor,
            AttackDefenseCommandKind kind) => actor.Value + ":" + (int)kind;

        private void PublishGateIBatch(AttackDefenseCommandBatch batch)
        {
            if (batch == null || batch.Commands.Count == 0)
                throw new InvalidOperationException("Gate I must publish a non-empty command batch.");
            var team = PlayerForStableId(batch.Commands[0].Actor).Id.Team;
            _formalAuthority.AttackControllers[team].PreflightAndCommit(batch);
            // A Gate I contact has already been atomically committed to the
            // player controller.  It still needs the physical rally window that
            // legacy ScheduleDecision normally owns; do not synthesize a new
            // tactical decision or open defense windows before the attack.
            var attack = batch.Commands.SingleOrDefault(command =>
                command.Kind == AttackDefenseCommandKind.AttackContact);
            if (attack != null)
            {
                var execution = attack.Execution ?? throw new InvalidOperationException(
                    "Gate I attack requires immutable execution facts.");
                var actor = PlayerForStableId(attack.Actor).Id;
                _scheduledDecision = null;
                _scheduledPrimaryActor = actor;
                _expectedContactTime = execution.ScheduledSimulationTime;
                _touchState.OpenWindow(new RallyContactWindow(actor.Team,
                    TechniqueAction.Attack,
                    _expectedContactTime - ContactWindowLead,
                    _expectedContactTime + ContactWindowTail,
                    new[] { actor }));
                _contactDeadlineActive = true;
            }
            var toolRecoveryReceive = batch.Evidence.Phase ==
                AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive
                ? batch.Commands.SingleOrDefault(command =>
                    command.Kind == AttackDefenseCommandKind.AttackCover)
                : null;
            if (toolRecoveryReceive != null)
            {
                var execution = toolRecoveryReceive.Execution ??
                    throw new InvalidOperationException(
                        "Tool recovery Receive requires immutable actual execution facts.");
                var actor = PlayerForStableId(toolRecoveryReceive.Actor).Id;
                _scheduledDecision = null;
                _scheduledPrimaryActor = actor;
                _expectedContactTime = execution.ScheduledSimulationTime;
                _touchState.OpenWindow(new RallyContactWindow(actor.Team,
                    TechniqueAction.Receive,
                    _expectedContactTime - ContactWindowLead,
                    _expectedContactTime + ContactWindowTail,
                    new[] { actor }));
                _contactDeadlineActive = true;
            }
            if (batch.Evidence.Phase == AttackDefenseAuthorityPhaseV3.DefenseCommitted)
            {
                _pendingGateIReceiveWindows.Clear();
                _pendingGateIReceiveWindows.AddRange(batch.Commands
                    .Where(command =>
                        command.Kind == AttackDefenseCommandKind.FloorDefense ||
                        command.Kind == AttackDefenseCommandKind.AttackCover)
                    .Select(command => new AttackDefenseAuthorityReceipt(
                        command.PlanRevision,
                        command.SourceSequence,
                        batch.Evidence.Phase,
                        command.Kind,
                        command.Actor,
                        command.Branch,
                        command.Execution.ExecutionClassification,
                        command.Execution.TrajectoryArtifact,
                        batch.Evidence,
                        execution: command.Execution,
                        perception: batch.Evidence.Perception)));
            }
            var blockCount = batch.Commands.Count(command =>
                command.Kind == AttackDefenseCommandKind.BlockContact);
            if (blockCount > 0)
            {
                var blocks = batch.Commands.Where(command =>
                    command.Kind == AttackDefenseCommandKind.BlockContact).ToArray();
                _scheduledBlockers.Clear();
                foreach (var block in blocks)
                {
                    _scheduledBlockers.Add(PlayerForStableId(block.Actor).Id);
                }
                var primary = _formalAuthority.AttackCoordinator?.State.Plan?.Defense.Responsibilities
                    .FirstOrDefault(responsibility =>
                        responsibility.Kind == DefenseResponsibilityKindV3.PrimaryBlock &&
                        _scheduledBlockers.Contains(PlayerForStableId(responsibility.Actor).Id));
                _scheduledBlockPrimary = primary == null
                    ? (PlayerId?)null
                    : PlayerForStableId(primary.Actor).Id;
                _committedGateIBlockTime = blocks
                    .Select(block => block.Execution.ScheduledSimulationTime).Min();
                BlockSupportAssignments += blockCount;
                MaximumScheduledBlockers = Mathf.Max(MaximumScheduledBlockers,
                    blockCount);
                if (blockCount >= 2)
                {
                    ScheduledMultiBlockUnits++;
                }
            }
        }

        private void OpenCommittedGateIBlockWindow(PlayerId attackingActor)
        {
            if (!GateIAuthorityEnabled || _scheduledBlockers.Count == 0 ||
                _committedGateIBlockTime < 0f)
            {
                return;
            }

            var blockers = _scheduledBlockers.OrderBy(blocker => blocker.Role)
                .ThenBy(blocker => blocker.Team).ToArray();
            var blockTime = _committedGateIBlockTime;
            if (NetPlaneInterception.TryPredict(
                    _ball.State,
                    SimulationParameters,
                    SimulatedBall.DefaultFixedStep,
                    1.5f,
                    out var intercept))
            {
                // Gate I selects the block unit before the attack, but its
                // public trajectory cannot replace the actual swept ball.
                // Retarget the already committed players to the accepted
                // attack's real net intercept; it neither adds actors nor
                // opens a receive window.
                blockTime = _ball.SimulationTime + intercept.TimeSeconds;
                var primary = _scheduledBlockPrimary.HasValue &&
                              _scheduledBlockers.Contains(_scheduledBlockPrimary.Value)
                    ? _scheduledBlockPrimary.Value
                    : blockers[0];
                var primarySnapshot = BlockSnapshot(primary);
                foreach (var blocker in blockers)
                {
                    var target = UnitBlockRootTarget(
                        blocker.Team,
                        intercept.Point,
                        BlockSnapshot(blocker),
                        primarySnapshot);
                    target = _players[blocker].ResolveBlockRootTarget(
                        intercept.Point,
                        target);
                    _players[blocker].RetargetBlockContact(
                        blockTime,
                        target,
                        BlockReboundVelocity(attackingActor.Team));
                }

                PrepositionDeclaredAttackCoverage(
                    attackingActor.Team,
                    intercept.Point,
                    blockTime);
            }

            _touchState.OpenWindow(new RallyContactWindow(
                blockers[0].Team,
                TechniqueAction.Block,
                blockTime - ContactWindowLead,
                blockTime + ContactWindowTail,
                blockers));
            _contactDeadlineActive = false;
        }

        private void PrepositionDeclaredAttackCoverage(
            TeamId attackingTeam,
            SimVector3 actualNetIntercept,
            float blockTime)
        {
            var plan = _formalAuthority?.AttackCoordinator?.State.Plan;
            if (plan == null)
            {
                return;
            }

            // Gate I's tool-recovery qualification declares the only actors
            // allowed to save a rebound on the attacking side. Move those
            // players into the same short, behind-the-hitter corridor used by
            // the shared rebound planner, but do not open a contact window.
            // The live post-block trajectory still decides whether any of them
            // can receive.
            var frame = new TeamCourtFrame(attackingTeam);
            var coverageTarget = frame.ToWorld(
                new SimVector3(actualNetIntercept.X, 0f, -1.25f));
            foreach (var responsibility in plan.AttackCoverageResponsibilities
                         .Where(value =>
                             PlayerForStableId(value.Actor).Id.Team ==
                             attackingTeam)
                         .OrderBy(value => value.Actor.Value,
                             StringComparer.Ordinal)
                         .ThenBy(value => value.Branch))
            {
                var player = PlayerForStableId(responsibility.Actor);
                player.CancelScheduledContact();
                player.ScheduleSupportAction(
                    TechniqueAction.Receive,
                    blockTime,
                    ToUnity(coverageTarget),
                    _ball.SimulationTime);
            }
        }

        private bool OpenPendingGateIReceiveWindow(
            TeamId receivingTeam,
            SimVector3 actualPosition,
            SimVector3 actualVelocity,
            out AttackDefenseAuthorityReceipt openedReceipt)
        {
            openedReceipt = null;
            var receives = _pendingGateIReceiveWindows
                .Where(receipt =>
                    PlayerForStableId(receipt.Actor).Id.Team == receivingTeam)
                .OrderBy(receipt => receipt.Execution.ScheduledSimulationTime)
                .ThenBy(receipt => receipt.Execution.ContactGroupId)
                .ThenBy(receipt => receipt.Actor.Value, StringComparer.Ordinal)
                .ToArray();
            var excluded = _pendingGateIReceiveWindows
                .Where(receipt =>
                    PlayerForStableId(receipt.Actor).Id.Team != receivingTeam)
                .ToArray();
            _pendingGateIReceiveWindows.Clear();
            foreach (var receipt in excluded)
            {
                PlayerForStableId(receipt.Actor).CancelScheduledContact();
                TakeGateIContactReceipt(receipt.Actor, receipt.Kind);
                RecordDefenseAttempt(
                    "DefenseAttemptExpired",
                    receipt,
                    PlayerForStableId(receipt.Actor).Id.Team,
                    "ContinuationResolvedToOtherSide");
            }
            if (receives.Length == 0)
            {
                return false;
            }

            // Pre-crossing executions authorize actors and commit movement, but
            // their predicted contact time cannot replace the live ball. Try
            // the declared actors in the stable Gate I order and bind the first
            // physically reachable one to the observed crossing. Consuming the
            // opportunity is distinct from opening a window: if no declared
            // actor can reach, the ball must remain available to the referee
            // instead of falling back to a newly invented receive decision.
            for (var index = 0; index < receives.Length; index++)
            {
                var receipt = receives[index];
                var execution = CreateActualContinuationReceiveExecution(
                    receipt.Actor,
                    receipt.Kind,
                    actualPosition,
                    actualVelocity);
                TakeGateIContactReceipt(receipt.Actor, receipt.Kind);
                if (execution == null)
                {
                    PlayerForStableId(receipt.Actor).CancelScheduledContact();
                    RecordDefenseAttempt(
                        "DefenseAttemptExpired",
                        receipt,
                        receivingTeam,
                        "ActualContinuationUnreachable");
                    continue;
                }

                openedReceipt = ActivatePostAttackReceive(
                    receivingTeam,
                    receipt.Kind,
                    receipt.Actor,
                    receipt.Branch,
                    execution);
                for (var remaining = index + 1;
                     remaining < receives.Length;
                     remaining++)
                {
                    var superseded = receives[remaining];
                    PlayerForStableId(superseded.Actor).CancelScheduledContact();
                    TakeGateIContactReceipt(superseded.Actor, superseded.Kind);
                    RecordDefenseAttempt(
                        "DefenseAttemptExpired",
                        superseded,
                        receivingTeam,
                        "ReachableActorSelected");
                }

                return true;
            }

            return true;
        }

        private bool OpenDeclaredAttackCoverageWindow(
            TeamId receivingTeam,
            SimVector3 actualPosition,
            SimVector3 actualVelocity,
            out AttackDefenseAuthorityReceipt openedReceipt)
        {
            openedReceipt = null;
            var state = _formalAuthority.AttackCoordinator.State;
            var plan = state.Plan;
            if (plan == null)
            {
                return false;
            }

            var declaredCoverage = plan.AttackCoverageResponsibilities;
            var toolRecovery = state.Phase ==
                    AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive
                ? plan.SelectedAction?.ToolRecoveryEvidence
                : null;
            if (toolRecovery != null)
            {
                // A successful tool block owns one immutable recovery actor.
                // Other ordinary attack-cover responsibilities remain useful
                // for non-tool rebounds, but cannot consume this lifecycle.
                declaredCoverage = new[]
                {
                    new AttackCoverageResponsibilityV3(
                        toolRecovery.RecoveryActor,
                        RallyPlanBranchV3.Primary)
                };
            }

            var coverage = declaredCoverage
                .Where(value =>
                    PlayerForStableId(value.Actor).Id.Team == receivingTeam)
                .OrderBy(value => value.Actor.Value, StringComparer.Ordinal)
                .ThenBy(value => value.Branch)
                .ToArray();
            for (var index = 0; index < coverage.Length; index++)
            {
                var opportunity = coverage[index];
                var execution = CreateActualContinuationReceiveExecution(
                    opportunity.Actor,
                    AttackDefenseCommandKind.AttackCover,
                    actualPosition,
                    actualVelocity);
                if (execution == null)
                {
                    continue;
                }

                var replaced = TakeGateIContactReceipt(
                    opportunity.Actor,
                    AttackDefenseCommandKind.AttackCover);
                if (replaced != null)
                {
                    PlayerForStableId(opportunity.Actor)
                        .CancelScheduledContact();
                    RecordDefenseAttempt(
                        "DefenseAttemptExpired",
                        replaced,
                        receivingTeam,
                        "ReboundBoundToActualBall");
                }

                openedReceipt = ActivatePostAttackReceive(
                    receivingTeam,
                    AttackDefenseCommandKind.AttackCover,
                    opportunity.Actor,
                    opportunity.Branch,
                    execution);
                return true;
            }

            return coverage.Length > 0;
        }

        private AttackDefenseAuthorityReceipt ActivatePostAttackReceive(
            TeamId receivingTeam,
            AttackDefenseCommandKind kind,
            StablePlayerId actor,
            RallyPlanBranchV3 branch,
            AttackDefenseCommandExecutionV4 execution)
        {
            PreparePostAttackPossession(
                receivingTeam,
                actor,
                execution.ScheduledSimulationTime - _ball.SimulationTime);
            CommitActualGateHReceive(
                receivingTeam,
                actor,
                execution);
            var source = _formalAuthority.NextSourceSequence();
            _formalAuthority.AttackCoordinator.PublishActualContinuation(
                source, kind, actor, execution, branch);
            var receipt = TakeGateIContactReceipt(actor, kind);
            if (receipt == null)
                throw new InvalidOperationException(
                    "Actual continuation must publish an event-owned receipt.");
            _formalAuthority.StoreGateIContact(GateIReceiptKey(actor, kind), receipt);

            _activePostAttackReceives.Clear();
            _activePostAttackReceives.Add(receipt);
            var runtimeActor = PlayerForStableId(actor).Id;
            _scheduledDecision = null;
            _scheduledPrimaryActor = runtimeActor;
            _expectedContactTime = execution.ScheduledSimulationTime;
            _touchState.OpenWindow(new RallyContactWindow(
                receivingTeam,
                TechniqueAction.Receive,
                _expectedContactTime - ContactWindowLead,
                _expectedContactTime + ContactWindowTail,
                new[] { runtimeActor }));
            _contactDeadlineActive = true;
            RecordDefenseAttempt("DefenseAttemptOpened", receipt,
                receivingTeam, "ActualContinuation");
            return receipt;
        }

        private void CommitActualGateHReceive(
            TeamId receivingTeam,
            StablePlayerId receivingActor,
            AttackDefenseCommandExecutionV4 execution)
        {
            var planning = _formalAuthority.ReceiveCoordinator.CurrentPlanning;
            var state = _formalAuthority.ReceiveCoordinator.State;
            if (!planning.Decision.HasDecision ||
                !planning.Plan.PrimaryReceiver.Equals(receivingActor) ||
                !StableId(planning.Decision.Actor).Equals(receivingActor))
            {
                throw new InvalidOperationException(
                    "The actual continuation requires its Gate H primary Receive decision.");
            }

            // Gate H owns the new possession and organization lifecycle while
            // Gate I owns eligibility for this defensive continuation. Publish
            // one shared physical execution to both authorities so replay can
            // prove the Receive without scheduling a second predicted contact.
            var payload = new ReceiveOrganizationCommandExecutionV4(
                execution.ScheduledSimulationTime,
                execution.MovementStartSimulationTime,
                execution.ExecutionError,
                execution.ContactGroupId,
                execution.ExecutionClassification,
                execution.TrajectoryArtifact,
                execution.ScheduledSimulationTime - ContactWindowLead,
                execution.ScheduledSimulationTime + ContactWindowTail,
                execution.ExecutionClassification.ExecutableSample.Velocity,
                execution.PhysicalContactCenter);
            var command = new ReceiveOrganizationAuthorityCommand(
                state.Revision,
                _formalAuthority.CurrentSourceSequence,
                ReceiveOrganizationCommandKind.PrimaryReceive,
                receivingActor,
                RallyPlanBranchV3.Primary,
                planning.Decision,
                false,
                payload);
            var evidence = new ReceiveOrganizationAuthorityEvidenceV3(
                state.Revision,
                _formalAuthority.CurrentSourceSequence,
                state.Phase,
                planning.Plan,
                planning.SetterEvidence,
                planning.FallbackReason,
                state.CoverageDecision,
                state.ActualFirstPassLanding,
                _formalAuthority.ReceiveCoordinator.CurrentPerception);
            _formalAuthority.ReceiveControllers[receivingTeam]
                .PreflightAndCommit(
                    new ReceiveOrganizationCommandBatch(
                        state.Revision,
                        _formalAuthority.CurrentSourceSequence,
                        new[] { command },
                        evidence));
        }

        private void PreparePostAttackPossession(
            TeamId receivingTeam,
            StablePlayerId receivingActor,
            float availableSeconds)
        {
            _aiDecisionRequestVersion++;
            _aiDecisionTimeController?.CancelPending();
            DisableEmergencyReceiveWindows(TeamId.Blue);
            DisableEmergencyReceiveWindows(TeamId.Orange);
            DisablePhysicalBlockWindows();
            _touchState.BeginPossession(receivingTeam);
            _plannedAttackDecision = null;
            _plannedAttackTrajectoryArtifactV4 = null;
            _controlledHandlingActive = false;
            _activeSetChain = false;
            _activeTacticalWeights = LocalTacticalWeights();

            var planning = PlanGateHReceive(
                receivingTeam,
                Mathf.Max(0.10f, availableSeconds),
                receivingActor);
            if (planning == null)
            {
                throw new InvalidOperationException(
                    "A reachable Gate I continuation requires a Gate H possession plan.");
            }

            var declaredByGateH =
                planning.Plan.PrimaryReceiver.Equals(receivingActor) ||
                planning.Plan.EmergencyReceivers.Contains(receivingActor);
            if (!declaredByGateH)
            {
                throw new InvalidOperationException(
                    "The Gate I continuation actor must be declared by the new Gate H possession.");
            }
        }

        private void RecordDefenseAttempt(
            string kind, AttackDefenseAuthorityReceipt receipt, TeamId team,
            string reason, int? winningContactGroupId = null,
            StablePlayerId? winningActor = null)
        {
            if (receipt == null || _ball == null)
            {
                return;
            }

            var attemptContactTime =
                receipt.Execution?.ScheduledSimulationTime ??
                _expectedContactTime;
            var start = attemptContactTime - ContactWindowLead;
            var end = attemptContactTime + ContactWindowTail;
            var contactGroup = receipt.Execution?.ContactGroupId ??
                winningContactGroupId ?? -1;
            var identity = receipt.PlanRevision + ":" +
                receipt.SourceSequence + ":" + receipt.Kind + ":" +
                receipt.Actor.Value + ":" + contactGroup;
            NotifyReplay(ReplayDefenseAttemptRecorded,
                new ReplayDefenseAttemptEvent(kind, identity, receipt, team,
                    start, end, _ball.SimulationTime, _ball.State.Position,
                    _ball.State.Velocity, _postAttackContinuationState, reason,
                    winningContactGroupId, winningActor));
        }

        private static string GateHReceiptKey(
            StablePlayerId actor,
            TechniqueAction action)
        {
            return actor.Value + ":" + action;
        }

        private void PrepareSetterForReceive(TeamRallyDecision receiveDecision)
        {
            var setter = FindPlayer(
                receiveDecision.Actor.Team,
                role => role == PlayerRole.Setter);
            if (setter.Id.Equals(receiveDecision.Actor))
            {
                return;
            }

            var settingContactCenter = NextContactCenter(
                receiveDecision.Actor.Team,
                TechniqueAction.Set);
            var settingRoot = setter.ResolveContactRootTarget(
                TechniqueAction.Set,
                settingContactCenter,
                ToUnity(SetterOrganizationZone.DefaultWorldTarget(receiveDecision.Actor.Team)));
            setter.ScheduleSetPreparation(
                _expectedContactTime,
                settingRoot,
                _ball.SimulationTime);
            MovementAssignments++;
        }

        private static SkillExecutionError InSystemReceiveExecution()
        {
            return new SkillExecutionError(
                0f,
                SimVector3.Zero,
                SimVector3.Zero,
                0f,
                1f,
                SimVector3.Zero,
                TechniqueControlPolicy.MaximumControlFor(TechniqueAction.Receive));
        }

        private static ExecutionCandidateCategoryV4 ToExecutionCandidateCategoryV4(
            TechniqueAction action)
        {
            switch (action)
            {
                case TechniqueAction.Receive:
                    return ExecutionCandidateCategoryV4.Receive;
                case TechniqueAction.Set:
                    return ExecutionCandidateCategoryV4.Set;
                case TechniqueAction.Attack:
                    return ExecutionCandidateCategoryV4.Attack;
                case TechniqueAction.Block:
                    return ExecutionCandidateCategoryV4.Block;
                case TechniqueAction.Serve:
                    return ExecutionCandidateCategoryV4.Serve;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private void PrepareAttackerForReceive(
            TeamRallyDecision receiveDecision,
            float receiveArrivalSeconds)
        {
            var setter = FindPlayer(
                receiveDecision.Actor.Team,
                role => role == PlayerRole.Setter);
            var setFlightSeconds = SetFlightSolver.PreferredFlightSeconds(
                TacticFor(receiveDecision.Actor.Team).SetRhythm);
            var preparation = PlanDecisionAt(
                receiveDecision.Actor.Team,
                RallyDecisionStage.Attack,
                receiveArrivalSeconds + ReceiveFlightSeconds + setFlightSeconds,
                NextContactCenter(receiveDecision.Actor.Team, TechniqueAction.Attack),
                _touchState.CountedTeamTouches + 2,
                setter.Id);
            if (!preparation.HasDecision ||
                !preparation.AttackApproach.HasValue ||
                preparation.Actor.Equals(receiveDecision.Actor) ||
                preparation.Actor.Equals(setter.Id))
            {
                return;
            }

            _players[preparation.Actor].ScheduleAttackPreparation(
                _expectedContactTime + ReceiveFlightSeconds,
                ToUnity(preparation.AttackApproach.Value.ApproachStart),
                _ball.SimulationTime);
            MovementAssignments++;
        }

        private BallContactResolution ResolveCandidate(
            BallContactCandidate candidate,
            SweptBallHit hit,
            float contactSimulationTime)
        {
            if (!candidate.Actor.HasValue)
            {
                return BallContactResolution.Accept();
            }

            if (!_rallyActive || _restartScheduled || HasResult || _touchState == null)
            {
                return BallContactResolution.Ignore();
            }

            if (V3RulesMode == V3RulesMode.Authority)
            {
                var stableActor = StableId(candidate.Actor.Value);
                var side = ToSide(candidate.Actor.Value.Team);
                var classification = ToV3Classification(candidate.Action);
                var transition = candidate.Action == TechniqueAction.Attack
                    ? _v3RulesAdapter.EvaluateContact(
                        stableActor,
                        side,
                        classification,
                        hit.ContactGroupId,
                        CreateObservedAttackGeometry(
                            candidate,
                            hit,
                            contactSimulationTime))
                    : _v3RulesAdapter.EvaluateContact(
                        stableActor,
                        side,
                        classification,
                        hit.ContactGroupId);
                return ToBallContactResolution(transition);
            }

            var evaluation = _touchState.Evaluate(
                candidate.Actor.Value,
                candidate.Action,
                contactSimulationTime);
            return evaluation.Disposition switch
            {
                RallyContactDisposition.Accept => BallContactResolution.Accept(),
                RallyContactDisposition.Fault => BallContactResolution.Fault(evaluation.Reason.ToString()),
                _ => BallContactResolution.Ignore()
            };
        }

        private BallContactResolution CommitSelectedCandidateV3(
            BallContactCandidate candidate,
            SweptBallHit hit,
            float contactSimulationTime)
        {
            if (!candidate.Actor.HasValue)
            {
                return BallContactResolution.Accept();
            }
            if (V3RulesMode != V3RulesMode.Authority || _v3RulesAdapter == null)
            {
                throw new InvalidOperationException(
                    "The selected V3 contact committer requires Authority mode.");
            }

            var actor = candidate.Actor.Value;
            var stableActor = StableId(actor);
            var classification = ToV3Classification(candidate.Action);
            var side = ToSide(actor.Team);
            var observedAttackGeometry = candidate.Action == TechniqueAction.Attack
                ? CreateObservedAttackGeometry(
                    candidate,
                    hit,
                    contactSimulationTime)
                : null;
            var transition = observedAttackGeometry != null
                ? _v3RulesAdapter.CommitContact(
                    stableActor,
                    side,
                    classification,
                    hit.ContactGroupId,
                    observedAttackGeometry)
                : _v3RulesAdapter.CommitContact(
                    stableActor,
                    side,
                    classification,
                    hit.ContactGroupId);
            var resolution = ToBallContactResolution(transition);
            if (!transition.Accepted)
            {
                _pendingV3AuthorityContact = null;
                return resolution;
            }

            RallyContactEvaluation? legacyEvaluation = null;
            string diagnosticExceptionType = null;
            try
            {
                legacyEvaluation = _touchState.Evaluate(
                    actor,
                    candidate.Action,
                    contactSimulationTime);
            }
            catch (Exception exception)
            {
                diagnosticExceptionType = exception.GetType().FullName;
            }

            var legacyOutcome = legacyEvaluation.HasValue
                ? ToLegacyOutcome(legacyEvaluation.Value)
                : null;
            var scenario = DetermineShadowScenario(
                stableActor,
                classification,
                legacyOutcome);
            _pendingV3AuthorityContact = new PendingV3AuthorityContact(
                actor,
                candidate.Action,
                hit.ContactGroupId,
                contactSimulationTime,
                transition,
                observedAttackGeometry,
                legacyEvaluation,
                legacyOutcome,
                scenario,
                diagnosticExceptionType);
            return resolution;
        }

        private AttackGeometryFactV3 CreateObservedAttackGeometry(
            BallContactCandidate candidate,
            SweptBallHit hit,
            float contactSimulationTime)
        {
            if (candidate.Action != TechniqueAction.Attack || !candidate.Actor.HasValue)
            {
                throw new ArgumentException(
                    "Observed attack geometry requires an attack candidate with an actor.",
                    nameof(candidate));
            }

            var actor = candidate.Actor.Value;
            if (!_players[actor].TryGetObservedAttackTakeoff(out var takeoff))
            {
                throw new InvalidOperationException(
                    "The attack contact source did not capture an observed takeoff.");
            }

            return CreateObservedAttackGeometryFact(
                StableId(actor),
                ToSide(actor.Team),
                takeoff,
                hit,
                contactSimulationTime);
        }

        private static AttackGeometryFactV3 CreateObservedAttackGeometryFact(
            Volleyball.Shared.Contracts.PlayerId actor,
            TeamSide side,
            ObservedAttackTakeoff takeoff,
            SweptBallHit hit,
            float contactSimulationTime)
        {
            if (!hit.ContactPoint.IsFinite || !hit.ImpactCenter.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hit),
                    "Observed player and ball contact points must be finite.");
            }
            if (float.IsNaN(contactSimulationTime) ||
                float.IsInfinity(contactSimulationTime))
            {
                throw new ArgumentOutOfRangeException(nameof(contactSimulationTime));
            }
            if (takeoff.SimulationTime >= contactSimulationTime)
            {
                throw new InvalidOperationException(
                    "Observed attack takeoff must occur before physical contact.");
            }

            // ContactPoint is the actual shared player/ball collision point.
            // ImpactCenter is also validated so malformed ball geometry cannot
            // enter the observed authority boundary.
            return new AttackGeometryFactV3(
                actor,
                side,
                takeoff.Point,
                hit.ContactPoint,
                attackLineDistanceFromCenter: 3f,
                netHeight: CourtBuilder.NetHeight);
        }

        private static BallContactResolution ToBallContactResolution(RuleTransitionV3 transition)
        {
            if (transition.Accepted)
            {
                return BallContactResolution.Accept();
            }

            return transition.RejectionReason == RuleRejectionReasonV3.DuplicateContactGroup ||
                   transition.RejectionReason == RuleRejectionReasonV3.RallyClosed
                ? BallContactResolution.Ignore()
                : BallContactResolution.Fault(transition.RejectionReason.ToString());
        }

        private void HandleRejectedPlayerContact(PlayerContactRejectedEvent rejected)
        {
            if (!_rallyActive || _restartScheduled || !rejected.Candidate.Actor.HasValue)
            {
                return;
            }

            IllegalContactFaults++;
            var actor = rejected.Candidate.Actor.Value;
            var receipt = _activePostAttackReceives.FirstOrDefault(value =>
                value.Actor.Equals(StableId(actor)));
            if (receipt != null)
            {
                RecordDefenseAttempt("DefenseContactRejected", receipt,
                    actor.Team, rejected.Reason);
            }
            var winner = Opponent(ToSide(actor.Team));
            Debug.Log(
                $"[{_configuration.LogTag}] fault team={actor.Team} actor={actor.Role} " +
                $"action={rejected.Candidate.Action} reason={rejected.Reason}");
            ResolveRally(
                new RallyOutcome(winner, true, rejected.Reason),
                null,
                actor,
                rejected.Reason);
        }

        private void HandlePlayerContact(PlayerBallContactEvent contact)
        {
            if (!_rallyActive || _restartScheduled || HasResult || !contact.Candidate.Actor.HasValue)
            {
                return;
            }

            var actorId = contact.Candidate.Actor.Value;
            PendingV3AuthorityContact authorityContact = null;
            if (V3RulesMode == V3RulesMode.Authority)
            {
                authorityContact = _pendingV3AuthorityContact;
                _pendingV3AuthorityContact = null;
                if (authorityContact == null ||
                    !authorityContact.Actor.Equals(actorId) ||
                    authorityContact.Action != contact.Candidate.Action ||
                    authorityContact.ContactGroup != contact.Hit.ContactGroupId)
                {
                    throw new InvalidOperationException(
                        "The accepted physical contact does not match the committed V3 contact.");
                }

                SynchronizeLegacyCompatibility(authorityContact);
            }
            else
            {
                var accepted = _touchState.Accept(
                    actorId,
                    contact.Candidate.Action,
                    contact.ContactSimulationTime);
                if (accepted.Disposition != RallyContactDisposition.Accept)
                {
                    return;
                }
            }

            var actor = _players[actorId];
            var movementDistance = (_scheduledPrimaryActor.HasValue &&
                                    _scheduledPrimaryActor.Value.Equals(actorId)) ||
                                   contact.Candidate.Action == TechniqueAction.Block
                ? actor.ScheduledMovementDistance
                : 0f;
            _set.RecordContact(StableId(actorId), movementDistance);
            SuccessfulContacts++;
            _contactDeadlineActive = false;
            MaximumAppliedMovementCorrection = Mathf.Max(
                MaximumAppliedMovementCorrection,
                actor.MaximumAppliedContactCorrection);
            DisableEmergencyReceiveWindows(actorId.Team);

            if (contact.Candidate.Action == TechniqueAction.Receive &&
                (!_scheduledPrimaryActor.HasValue || !_scheduledPrimaryActor.Value.Equals(actorId)))
            {
                EmergencyReceiveContacts++;
            }
            if (contact.Candidate.Action == TechniqueAction.Set && actorId.Role != PlayerRole.Setter)
            {
                NonSetterSetContacts++;
            }
            if (contact.Candidate.Action == TechniqueAction.Attack && actorId.Role == PlayerRole.Defender)
            {
                DefenderAttackContacts++;
            }
            if (contact.Candidate.Action == TechniqueAction.Attack)
            {
                if (actorId.Team == TeamId.Blue)
                {
                    BlueAttackContacts++;
                }
                else
                {
                    OrangeAttackContacts++;
                }
            }

            var style = contact.Candidate.Action == TechniqueAction.Set
                ? actor.CurrentSetStyle.ToString()
                : string.Empty;
            _status =
                $"{actorId.Team} {actorId.Role} {contact.Candidate.Action} HIT  " +
                $"speed {contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0} m/s";
            Debug.Log(
                $"[{_configuration.LogTag}] contact={SuccessfulContacts} team={actorId.Team} " +
                $"actor={actorId.Role} action={contact.Candidate.Action} style={style} " +
                $"touches={_touchState.CountedTeamTouches} quality={contact.Hit.Centeredness:0.00} " +
                $"speed={contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0}");
            _pendingReplaySetChain = null;
            var acceptedExecutionClassification =
                actor.ScheduledExecutionClassificationV4;
            var acceptedTrajectoryArtifact =
                actor.ScheduledTrajectoryPredictionArtifactV4;
            var gateHAuthorityReceipt = TakeGateHContactReceipt(
                StableId(actorId),
                contact.Candidate.Action);
            acceptedExecutionClassification ??=
                gateHAuthorityReceipt?.ExecutionClassification;
            acceptedTrajectoryArtifact ??=
                gateHAuthorityReceipt?.TrajectoryArtifact;

            // These immutable receipts are captured before any Gate I state
            // transition.  Replay must never reconstruct this contact from a
            // newer coordinator plan after coverage/replanning has run.
            var gateISetIntentReceipt = GateIAuthorityEnabled &&
                contact.Candidate.Action == TechniqueAction.Set
                ? TakeGateISetIntentReceipt(StableId(actorId))
                : null;
            var gateIContactReceipt = GateIAuthorityEnabled &&
                contact.Candidate.Action != TechniqueAction.Set
                ? TakeGateIContactReceiptForAction(
                    StableId(actorId), contact.Candidate.Action)
                : null;
            if (contact.Candidate.Action == TechniqueAction.Receive &&
                gateIContactReceipt != null)
            {
                RecordDefenseAttempt("DefenseCandidateSampled",
                    gateIContactReceipt, actorId.Team, "SweptGeometryHit");
            }
            // A Gate I attack command is a one-shot physical ticket.  The
            // contact timeline can outlive its collision after an opposing
            // block, so consume it here before the rebound returns to the same
            // attack surface. Later continuation must arrive as a new command.
            if (GateIAuthorityEnabled &&
                contact.Candidate.Action == TechniqueAction.Attack)
            {
                actor.CancelScheduledContact();
                _postAttackContinuationState =
                    PostAttackContinuationStateV4.AwaitingAttackCrossing;
            }
            AttackDefenseCommandExecutionV4 toolRecoveryReceiveExecution = null;
            if (gateIContactReceipt == null && GateIAuthorityEnabled &&
                contact.Candidate.Action == TechniqueAction.Receive &&
                (_formalAuthority.AttackCoordinator.State.Phase ==
                    AttackDefenseAuthorityPhaseV3.AwaitingActualContact ||
                 _formalAuthority.AttackCoordinator.State.Phase ==
                    AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive))
            {
                gateIContactReceipt = _formalAuthority.AttackCoordinator.State.Phase ==
                    AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive
                    ? CreateToolRecoveryReceiveReceipt(StableId(actorId), acceptedExecutionClassification,
                        acceptedTrajectoryArtifact)
                    : CreateIncidentalGateIDefenseReceipt(StableId(actorId),
                        acceptedExecutionClassification, acceptedTrajectoryArtifact);
            }
            if (gateIContactReceipt != null && GateIAuthorityEnabled &&
                contact.Candidate.Action == TechniqueAction.Block &&
                _formalAuthority.AttackCoordinator.State.Phase ==
                    AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock)
            {
                toolRecoveryReceiveExecution = CreateToolRecoveryReceiveExecution(
                    contact.Hit.ImpactCenter,
                    contact.TechniqueResponse.FinalOutgoing);
                gateIContactReceipt = CreateActualToolRecoveryBlockReceipt(
                    gateIContactReceipt, acceptedExecutionClassification,
                    acceptedTrajectoryArtifact, contact.TechniqueResponse.FinalOutgoing,
                    authorityContact?.Transition.After.RemainingHits ?? 0);
            }

            AdvanceGateIAfterAcceptedContact(
                actorId, contact.Candidate.Action, gateIContactReceipt,
                contact.TechniqueResponse.FinalOutgoing, authorityContact?.Transition,
                toolRecoveryReceiveExecution);
            if (GateIAuthorityEnabled &&
                contact.Candidate.Action == TechniqueAction.Attack)
            {
                PrepositionPendingGateIReceivers(
                    contact.Hit.ImpactCenter,
                    contact.TechniqueResponse.FinalOutgoing);
            }
            if (gateIContactReceipt != null && GateIAuthorityEnabled &&
                ((contact.Candidate.Action == TechniqueAction.Block &&
                  _formalAuthority.AttackCoordinator.State.Phase ==
                      AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive) ||
                 (contact.Candidate.Action == TechniqueAction.Receive &&
                  _formalAuthority.AttackCoordinator.State.Phase ==
                      AttackDefenseAuthorityPhaseV3.ReorganizationPlanned &&
                  _formalAuthority.AttackCoordinator.State.Plan?.SelectedAction?.ToolRecoveryEvidence != null)))
            {
                gateIContactReceipt = SnapshotToolRecoveryReceipt(gateIContactReceipt);
            }
            var completedToolRecoveryReceive = GateIAuthorityEnabled &&
                contact.Candidate.Action == TechniqueAction.Receive &&
                gateIContactReceipt?.Kind == AttackDefenseCommandKind.AttackCover &&
                _formalAuthority.AttackCoordinator.State.Phase ==
                    AttackDefenseAuthorityPhaseV3.ReorganizationPlanned &&
                _formalAuthority.AttackCoordinator.State.Plan?.SelectedAction?.ToolRecoveryEvidence != null;
            if (completedToolRecoveryReceive)
            {
                _formalAuthority.AttackCoordinator.CompleteReorganizationAndReset(
                    _formalAuthority.AttackCoordinator.State.Revision, _formalAuthority.NextSourceSequence());
                _formalAuthority.ClearGateIContacts();
            }

            switch (contact.Candidate.Action)
            {
                case TechniqueAction.Receive:
                    if (_activePostAttackReceives.Count > 0)
                    {
                        _postAttackContinuationState =
                            PostAttackContinuationStateV4.ResolvedByContact;
                        _activePostAttackReceives.Clear();
                    }
                    var acceptedToolRecoveryReceive = GateIAuthorityEnabled &&
                        gateIContactReceipt?.Kind == AttackDefenseCommandKind.AttackCover &&
                        gateIContactReceipt.Evidence.Plan?.SelectedAction?.ToolRecoveryEvidence != null;
                    if (_controlledHandlingActive)
                    {
                        _controlledHandlingActive = false;
                        _scheduledDecision = null;
                        _scheduledPrimaryActor = null;
                        CompletedCycles++;
                        break;
                    }

                    // This physical Receive is the event-owned continuation of
                    // a Gate I tool block.  It has no Gate H receive authority
                    // to commit, so treating it as an ordinary first contact
                    // creates a second writer and violates Gate H's phase.
                    if (acceptedToolRecoveryReceive)
                    {
                        _scheduledDecision = null;
                        _scheduledPrimaryActor = null;
                        break;
                    }

                    if (GateHAuthorityEnabled)
                    {
                        AdvanceGateHAfterReceive(
                            actorId,
                            acceptedExecutionClassification,
                            acceptedTrajectoryArtifact);
                    }
                    else
                    {
                        ScheduleDecision(
                            PlanDecision(
                                actorId.Team,
                                RallyDecisionStage.Organize,
                                ReceiveFlightSeconds),
                            ReceiveFlightSeconds);
                    }
                    break;
                case TechniqueAction.Set:
                    if (GateHAuthorityEnabled)
                    {
                        _formalAuthority.ReceiveCoordinator.CommitOrganization(
                            _formalAuthority.ReceiveCoordinator.State.Revision,
                            _formalAuthority.NextSourceSequence());
                        _formalAuthority.ReceiveCoordinator.HandOffToAttack(
                            _formalAuthority.ReceiveCoordinator.State.Revision,
                            _formalAuthority.NextSourceSequence());
                    }

                    if (GateIAuthorityEnabled)
                    {
                        AdvanceGateIAfterAcceptedSet(actorId,
                            acceptedExecutionClassification,
                            acceptedTrajectoryArtifact,
                            gateISetIntentReceipt);
                        RecordGateISetCalibration(actorId, actor.CurrentSetStyle,
                            contact.Hit.Centeredness);
                        _formalAuthority.ActiveSetIntent = null;
                        break;
                    }

                    _lastSetQualityAssessment = null;
                    _activeSetChain = false;
                    var setFlight = _scheduledSetFlightSeconds > 0f
                        ? _scheduledSetFlightSeconds
                        : SetFlightSolver.PreferredFlightSeconds(TacticFor(actorId.Team).SetRhythm);
                    var attackDecision = _plannedAttackDecision;
                    var attackTrajectoryArtifact =
                        _plannedAttackTrajectoryArtifactV4;
                    _plannedAttackDecision = null;
                    _plannedAttackTrajectoryArtifactV4 = null;
                    if (attackDecision == null || !attackDecision.HasDecision)
                    {
                        attackDecision = PlanDecision(
                            actorId.Team,
                            RallyDecisionStage.Attack,
                            setFlight);
                        attackTrajectoryArtifact =
                            _lastTrajectoryPredictionArtifactV4;
                    }
                    ScheduleAttackFromActualSet(
                        attackDecision,
                        setFlight,
                        actorId,
                        attackTrajectoryArtifact);
                    RecordSetCalibration(actorId, actor.CurrentSetStyle, contact.Hit.Centeredness);
                    break;
                case TechniqueAction.Attack:
                    CompletedCycles++;
                    _lastTouchWasBackSetAttack = TacticFor(actorId.Team).SetRoute == SetRoute.BackSet;
                    if (_lastTouchWasBackSetAttack)
                    {
                        BackSetAttackContacts++;
                    }
                    if (GateIAuthorityEnabled)
                    {
                        // The committed Gate I joint-defense batch has already
                        // scheduled block/floor responsibility.  Open only its
                        // committed physical contact window; never rebuild a
                        // legacy block unit from the hidden final route.
                        OpenCommittedGateIBlockWindow(actorId);
                        break;
                    }
                    GateILegacyWriterInvocations++;
                    SchedulePhysicalBlock(actorId.Team, _scheduledDecision, contact);
                    break;
                case TechniqueAction.Block:
                    HandleAcceptedBlock(contact);
                    break;
            }

            var acceptedV3Transition = ObserveAcceptedContactV3(
                actorId,
                contact.Candidate.Action,
                contact.Hit.ContactGroupId,
                authorityContact);
            RecordShadowPlanV3(
                acceptedV3Transition,
                acceptedTrajectoryArtifact,
                StableId(actorId),
                ToSide(actorId.Team),
                ToV3Classification(contact.Candidate.Action),
                contact.Hit.ContactGroupId);
            NotifyReplay(
                ReplayContactAccepted,
                new ReplayContactEvent(
                    contact.Candidate.Action == TechniqueAction.Block ? "Block" : "Contact",
                    contact.ContactSimulationTime,
                    actorId.Team,
                    StableId(actorId),
                    contact.Candidate.Action,
                    _pendingReplaySetChain,
                    authorityContact?.ObservedAttackGeometry,
                    authorityContact?.Transition,
                    acceptedExecutionClassification,
                    acceptedTrajectoryArtifact,
                    gateHAuthorityReceipt,
                    gateISetIntentReceipt,
                    gateIContactReceipt));
            if (contact.Candidate.Action == TechniqueAction.Receive &&
                gateIContactReceipt != null)
            {
                RecordDefenseAttempt("DefenseContactAccepted",
                    gateIContactReceipt, actorId.Team, "Accepted",
                    contact.Hit.ContactGroupId, StableId(actorId));
            }
        }

        private void AdvanceGateHAfterReceive(
            PlayerId actor,
            ExecutionSampleClassificationV4 acceptedClassification,
            BallTrajectoryPredictionArtifactV4 acceptedTrajectory)
        {
            var state = _formalAuthority.ReceiveCoordinator.State;
            var stableActor = StableId(actor);
            if (!stableActor.Equals(state.PrimaryActor))
            {
                _formalAuthority.ReceiveCoordinator.ActivateEmergency(
                    state.Revision,
                    _formalAuthority.NextSourceSequence(),
                    stableActor);
            }
            else
            {
                _formalAuthority.ReceiveCoordinator.CommitReceive(
                    state.Revision,
                    _formalAuthority.NextSourceSequence());
            }

            var landing = PredictGate5BallCenterV4(
                actor.Team,
                RallyDecisionStage.Organize,
                ReceiveFlightSeconds);
            _formalAuthority.ReceiveCoordinator.AcceptReceive(
                new AcceptedReceiveV3(
                    state.Revision,
                    _formalAuthority.NextSourceSequence(),
                    stableActor,
                    landing,
                    PlanCoverageReason.WithinConditionalEnvelope,
                    acceptedTrajectory?.ArtifactIdentity ??
                    "gate-h-accepted-trajectory",
                    acceptedClassification?.ExecutableEnvelope?.Identity ??
                    "gate-h-accepted-classification"));
            if (GateIAuthorityEnabled &&
                _formalAuthority.AttackCoordinator.State.Phase ==
                    AttackDefenseAuthorityPhaseV3.ReorganizationPlanned)
            {
                _formalAuthority.AttackCoordinator.CompleteReorganizationAndReset(
                    _formalAuthority.AttackCoordinator.State.Revision,
                    _formalAuthority.NextSourceSequence());
                // Receipts for committed block/floor commands that did not
                // become the accepted physical contact belong to the completed
                // opportunity. The accepted contact already owns its snapshot,
                // so carrying the remaining receipts into the next possession
                // would either misattribute evidence or block the next writer.
                _formalAuthority.ClearGateIContacts();
            }
            var organization = _formalAuthority.ReceiveCoordinator.CurrentPlanning;
            if (organization.Decision.HasDecision)
            {
                ScheduleDecision(
                    organization.Decision,
                    ReceiveFlightSeconds,
                    acceptedTrajectory);
            }
        }

        private void AdvanceGateIAfterAcceptedContact(PlayerId actor,
            TechniqueAction action, AttackDefenseAuthorityReceipt receipt,
            SimVector3 actualOutgoing, RuleTransitionV3 v3Transition,
            AttackDefenseCommandExecutionV4 toolRecoveryReceiveExecution = null)
        {
            if (!GateIAuthorityEnabled || action == TechniqueAction.Set)
                return;
            var kind = action == TechniqueAction.Attack
                ? AttackDefenseCommandKind.AttackContact
                : action == TechniqueAction.Block
                    ? AttackDefenseCommandKind.BlockContact
                    : action == TechniqueAction.Receive
                        ? receipt?.Kind
                        : (AttackDefenseCommandKind?)null;
            if (!kind.HasValue)
                return;
            var phase = _formalAuthority.AttackCoordinator?.State.Phase;
            if (phase != AttackDefenseAuthorityPhaseV3.AttackCommitted &&
                phase != AttackDefenseAuthorityPhaseV3.AwaitingActualContact &&
                phase != AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock &&
                phase != AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive)
                return;
            if (receipt == null)
            {
                // A formal Gate I contact may not invent command evidence or read
                // coordinator state back after the fact.
                throw new InvalidOperationException("Accepted formal Gate I contact has no event-owned receipt.");
            }
            var plan = receipt.Evidence.Plan;
            var exit = plan?.ReorganizationExits.OrderBy(value => value.Identity,
                StringComparer.Ordinal).FirstOrDefault();
            if (exit == null)
                throw new InvalidOperationException("Accepted Gate I contact requires a declared reorganization exit.");
            _formalAuthority.AttackCoordinator.AcceptContact(new GateIContactEvidenceV3(
                receipt.PlanRevision, _formalAuthority.NextSourceSequence(), receipt.Actor,
                receipt.Evidence.CoverageDecision.Reason, receipt.Kind,
                receipt.Branch,
                receipt.ExecutionClassification.ExecutableEnvelope.Identity,
                receipt.TrajectoryArtifact.ArtifactIdentity, true, exit.Identity,
                action == TechniqueAction.Block &&
                phase == AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock
                    ? (toolRecoveryReceiveExecution != null &&
                       ReturnsToAttackingSide(actualOutgoing,
                        _formalAuthority.AttackCoordinator.State.AttackingSide)
                        ? ToolRecoveryReboundObservationV3.ReturnsToAttackingSide
                        : ToolRecoveryReboundObservationV3.ReturnsAway)
                    : ToolRecoveryReboundObservationV3.NotApplicable,
                v3Transition?.After.RemainingHits ?? -1,
                action == TechniqueAction.Receive &&
                phase == AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive
                    ? receipt.Execution
                    : toolRecoveryReceiveExecution));
        }

        private static bool ReturnsToAttackingSide(SimVector3 outgoing,
            TeamSide attackingSide) => attackingSide == TeamSide.Home
            ? outgoing.Z < 0f : outgoing.Z > 0f;

        private static bool ReturnsToTeam(SimVector3 outgoing, TeamId team) =>
            team == TeamId.Blue ? outgoing.Z < 0f : outgoing.Z > 0f;

        private AttackDefenseCommandExecutionV4 CreateToolRecoveryReceiveExecution(
            SimVector3 actualImpactCenter, SimVector3 actualBlockOutgoing)
        {
            var state = _formalAuthority.AttackCoordinator.State;
            var recovery = state.Plan?.SelectedAction?.ToolRecoveryEvidence
                ?? throw new InvalidOperationException("Tool recovery execution requires declared evidence.");
            return CreateActualContinuationReceiveExecution(
                recovery.RecoveryActor,
                AttackDefenseCommandKind.AttackCover,
                actualImpactCenter,
                actualBlockOutgoing);
        }

        private AttackDefenseCommandExecutionV4 CreateActualContinuationReceiveExecution(
            StablePlayerId receiverId,
            AttackDefenseCommandKind kind,
            SimVector3 actualImpactCenter,
            SimVector3 actualBlockOutgoing)
        {
            var state = _formalAuthority.AttackCoordinator.State;
            if (kind != AttackDefenseCommandKind.FloorDefense &&
                kind != AttackDefenseCommandKind.AttackCover)
                throw new ArgumentOutOfRangeException(nameof(kind));
            var receiver = PlayerForStableId(receiverId);
            var identity = "gate-i-rebound:" + state.Revision + ":" +
                (int)kind + ":" + receiverId.Value + ":" +
                _formalAuthority.CurrentSourceSequence;
            var executionError = _forceInSystemReceiveExecution
                ? InSystemReceiveExecution()
                : SkillExecutionResolver.Resolve(
                    receiver.Ability,
                    TechniqueAction.Receive,
                    StablePlayerNumber(receiver.Id),
                    _tacticRevision,
                    SuccessfulContacts,
                    7351,
                    0.72f);
            // Bind an already-declared receive opportunity to the live ball.
            // This works for both a block rebound and an ordinary attack
            // crossing; it creates no actor or window without a Gate I receipt.
            var rebound = new BallState(actualImpactCenter, actualBlockOutgoing,
                SimulatedBall.DefaultRadius);
            var recoveryRoot = ToSimulation(receiver.transform.position);
            var recoverySpeed = BaseMovementSpeed * (.65f +
                (receiver.Ability.Mobility * .5f));
            // PlayerLocomotion reserves this interval before a non-attack
            // contact.  The live recovery search must use the same interval,
            // otherwise it can publish a contact the receiver cannot reach.
            const float contactPreparationLead = .10f;
            var maximumSteps = Mathf.CeilToInt(
                MaximumContinuationSearchSeconds /
                SimulatedBall.DefaultFixedStep);
            var recoveryLead = 0f;
            var recoveryRootTarget = Vector3.zero;
            for (var step = 1; step <= maximumSteps; step++)
            {
                BallIntegrator.Step(rebound, SimulatedBall.DefaultFixedStep,
                    SimulationParameters);
                if (rebound.Position.Y <=
                    GroundHeight + SimulatedBall.DefaultRadius)
                {
                    break;
                }

                var elapsed = step * SimulatedBall.DefaultFixedStep;
                var rootTarget = receiver.ResolveContactRootTarget(
                    TechniqueAction.Receive, rebound.Position,
                    new Vector3(rebound.Position.X, 0f, rebound.Position.Z));
                var horizontal = new SimVector3(rootTarget.x - recoveryRoot.X,
                    0f, rootTarget.z - recoveryRoot.Z).Magnitude;
                var movementSeconds = Mathf.Max(
                    0f,
                    elapsed -
                    contactPreparationLead -
                    executionError.ReactionDelay);
                var surfaceReachable = CanReachReceiveSurface(
                    rebound.Position,
                    rebound.Velocity,
                    receiver.PreviewContactFramesAt(
                        TechniqueAction.Receive,
                        rootTarget));

                if (rebound.Position.Y >= SimulatedBall.DefaultRadius &&
                    movementSeconds > 0f &&
                    horizontal <= recoverySpeed * movementSeconds &&
                    surfaceReachable)
                {
                    recoveryLead = elapsed;
                    recoveryRootTarget = rootTarget;
                    break;
                }
            }
            if (recoveryLead <= 0f)
            {
                return null;
            }
            // Re-integrate to the selected reachable continuation sample; this
            // makes the contact center/time a single immutable actual-rebound
            // fact rather than an independently guessed movement target.
            rebound = new BallState(actualImpactCenter, actualBlockOutgoing,
                SimulatedBall.DefaultRadius);
            var selectedSteps = Mathf.Max(1, Mathf.RoundToInt(recoveryLead /
                SimulatedBall.DefaultFixedStep));
            for (var step = 0; step < selectedSteps; step++)
                BallIntegrator.Step(rebound, SimulatedBall.DefaultFixedStep,
                    SimulationParameters);
            var contactCenter = rebound.Position;
            // The declared reorganization exit supplies the receiving side;
            // the setter organization zone is a pure geometric output target,
            // not a new tactical Set selection.
            var setterZone = SetterOrganizationZone.DefaultWorldTarget(receiver.Id.Team);
            var target = new SimVector3(setterZone.X, 2.5f, setterZone.Z);
            var outgoing = ReturnVelocitySolver.Solve(contactCenter, target, .60f,
                SimulatedBall.DefaultFixedStep, SimulationParameters).InitialVelocity;
            var envelope = PlanExecutionEnvelopeV4(receiver.Ability.Derived,
                new ExecutionIntentV4(identity, ExecutionCandidateCategoryV4.Receive,
                    target, outgoing, .5f), identity + ":sample",
                ExecutionEnvelopePolicyV4.GateI);
            var sample = new ExecutionSampleV4(envelope.Identity,
                envelope.Sampling.SamplingKey, ExecutionCandidateCategoryV4.Receive,
                target + executionError.ContactPositionError,
                (outgoing * executionError.SurfaceSpeedScale) +
                executionError.TargetVelocityError,
                envelope.RequestedEffort);
            var classification = ExecuteExecutionSampleV4(envelope, sample);
            if (classification.Kind is ExecutionSampleClassificationKindV4.UnexpectedExecutionSample or
                ExecutionSampleClassificationKindV4.EnvelopeExceeded)
                throw new InvalidOperationException("Actual rebound Receive must be executable.");
            var stateVersion = (long)(uint)BitConverter.ToInt32(
                BitConverter.GetBytes(_ball.SimulationTime), 0);
            var trajectory = PredictSharedGate5TrajectoryV4(
                _trajectoryPredictionProviderV4,
                new BallTrajectoryPredictionRequestV4(ToSide(receiver.Id.Team), stateVersion,
                    new BallState(contactCenter, outgoing, SimulatedBall.DefaultRadius),
                    SimulationParameters, _matchContext.PhysicsConfigurationHash,
                    identity + ":trajectory",
                    _matchContext.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                    _matchContext.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                    envelope.Identity, ExecutionDegradationStepV4.FullSampling),
                ExecutionEnvelopePolicyV4.Default);
            // Resolve the receiving platform's root from the actual rebound.
            // Scheduling the ball's ground projection makes the player stop a
            // palm-width away because a Receive surface is offset from its root.
            var root = recoveryRootTarget;
            var group = checked(1000000000 + ((int)state.Revision * 16) +
                (kind == AttackDefenseCommandKind.AttackCover ? 4 : 3));
            return new AttackDefenseCommandExecutionV4(_ball.SimulationTime + recoveryLead,
                _ball.SimulationTime, executionError, group, classification, trajectory,
                ToSimulation(root), physicalContactCenter: contactCenter);
        }

        private void PrepositionPendingGateIReceivers(
            SimVector3 actualAttackCenter,
            SimVector3 actualAttackOutgoing)
        {
            const float laneSpacing = .18f;
            var reachable = new List<(
                AttackDefenseAuthorityReceipt Receipt,
                AttackDefenseCommandExecutionV4 Execution,
                PrototypePlayerAgent Player)>();
            foreach (var receipt in _pendingGateIReceiveWindows)
            {
                var player = PlayerForStableId(receipt.Actor);
                // Pending prediction-owned contacts must never remain live
                // while side resolution waits for the real ball.
                player.CancelScheduledContact();
                var execution = CreateActualContinuationReceiveExecution(
                    receipt.Actor,
                    receipt.Kind,
                    actualAttackCenter,
                    actualAttackOutgoing);
                if (execution == null)
                {
                    continue;
                }

                reachable.Add((receipt, execution, player));
            }

            var ordered = reachable
                .OrderBy(value => value.Player.transform.position.x)
                .ThenBy(value => value.Receipt.Actor.Value,
                    StringComparer.Ordinal)
                .ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                // All declared reachable defenders follow the actual flight,
                // but retain stable neighboring lanes until the crossing
                // selects the exact contact root.  Keeping their current
                // horizontal order prevents paths from crossing or converging
                // to the same point during this preposition phase.
                var movementTarget =
                    ToUnity(ordered[index].Execution.MovementTarget);
                movementTarget.x +=
                    (index - ((ordered.Length - 1) * .5f)) * laneSpacing;
                ordered[index].Player.ScheduleSupportAction(
                    TechniqueAction.Receive,
                    ordered[index].Execution.ScheduledSimulationTime,
                    movementTarget,
                    _ball.SimulationTime);
            }
        }

        private static bool CanReachReceiveSurface(
            SimVector3 ballCenter,
            SimVector3 ballVelocity,
            IReadOnlyList<ContactSurfaceFrame> frames)
        {
            const float planeTolerance = .10f;
            foreach (var frame in frames)
            {
                if (SimVector3.Dot(ballVelocity, frame.Normal) >= 0f)
                {
                    continue;
                }

                var centerDistance = SimVector3.Dot(
                    ballCenter - frame.Origin,
                    frame.Normal);
                if (Mathf.Abs(centerDistance - SimulatedBall.DefaultRadius) >
                    planeTolerance)
                {
                    continue;
                }

                var contactPoint =
                    ballCenter -
                    (frame.Normal * SimulatedBall.DefaultRadius);
                var fromOrigin = contactPoint - frame.Origin;
                var rightOffset = Mathf.Abs(SimVector3.Dot(
                    fromOrigin,
                    frame.Right));
                var upOffset = Mathf.Abs(SimVector3.Dot(
                    fromOrigin,
                    frame.Up));
                if (rightOffset <=
                        (frame.Width * .5f) + SimulatedBall.DefaultRadius &&
                    upOffset <=
                        (frame.Height * .5f) + SimulatedBall.DefaultRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private AttackDefenseAuthorityReceipt CreateToolRecoveryReceiveReceipt(
            StablePlayerId actor, ExecutionSampleClassificationV4 classification,
            BallTrajectoryPredictionArtifactV4 trajectory)
        {
            var state = _formalAuthority.AttackCoordinator.State;
            var recovery = state.Plan?.SelectedAction?.ToolRecoveryEvidence;
            if (recovery == null || !actor.Equals(recovery.RecoveryActor) ||
                classification == null || trajectory == null)
                throw new InvalidOperationException("Tool recovery Receive requires the declared actual saver evidence.");
            var source = _formalAuthority.PeekNextSourceSequence();
            return new AttackDefenseAuthorityReceipt(state.Revision, source, state.Phase,
                AttackDefenseCommandKind.AttackCover, actor, RallyPlanBranchV3.Primary,
                classification, trajectory, new AttackDefenseAuthorityEvidenceV3(
                    state.Revision, source, state.Phase, state.Plan,
                    state.CoverageDecision,
                    _formalAuthority.AttackCoordinator.CurrentPerception),
                perception: _formalAuthority.AttackCoordinator.CurrentPerception);
        }

        private AttackDefenseAuthorityReceipt CreateActualToolRecoveryBlockReceipt(
            AttackDefenseAuthorityReceipt planned,
            ExecutionSampleClassificationV4 classification,
            BallTrajectoryPredictionArtifactV4 trajectory, SimVector3 actualOutgoing,
            int remainingTouches)
        {
            var state = _formalAuthority.AttackCoordinator.State;
            var recovery = state.Plan?.SelectedAction?.ToolRecoveryEvidence;
            if (planned == null || recovery == null ||
                !planned.Actor.Equals(recovery.Blocker) || classification == null ||
                trajectory == null)
                throw new InvalidOperationException("Tool recovery Block requires the declared blocker and actual accepted evidence.");
            var source = _formalAuthority.PeekNextSourceSequence();
            return new AttackDefenseAuthorityReceipt(state.Revision, source, state.Phase,
                AttackDefenseCommandKind.BlockContact, planned.Actor, planned.Branch,
                classification, trajectory, new AttackDefenseAuthorityEvidenceV3(
                    state.Revision, source, state.Phase, state.Plan,
                    state.CoverageDecision,
                    _formalAuthority.AttackCoordinator.CurrentPerception),
                new ToolRecoveryActualObservationV3(
                    ReturnsToAttackingSide(actualOutgoing, state.AttackingSide)
                        ? state.AttackingSide : Opponent(state.AttackingSide),
                    trajectory.ArtifactIdentity,
                    classification.Sample.SamplingKey,
                    "actual-tool-block:" + planned.Actor.Value + ":" + source,
                    remainingTouches),
                perception: _formalAuthority.AttackCoordinator.CurrentPerception);
        }

        private AttackDefenseAuthorityReceipt SnapshotToolRecoveryReceipt(
            AttackDefenseAuthorityReceipt receipt)
        {
            var state = _formalAuthority.AttackCoordinator.State;
            return new AttackDefenseAuthorityReceipt(state.Revision, _formalAuthority.CurrentSourceSequence,
                state.Phase, receipt.Kind, receipt.Actor, receipt.Branch,
                receipt.ExecutionClassification, receipt.TrajectoryArtifact,
                new AttackDefenseAuthorityEvidenceV3(state.Revision,
                    _formalAuthority.CurrentSourceSequence, state.Phase, state.Plan,
                    state.CoverageDecision,
                    _formalAuthority.AttackCoordinator.CurrentPerception),
                receipt.ToolRecoveryActualObservation,
                perception: _formalAuthority.AttackCoordinator.CurrentPerception);
        }

        private void AdvanceGateIAfterAcceptedSet(PlayerId actor,
            ExecutionSampleClassificationV4 classification,
            BallTrajectoryPredictionArtifactV4 trajectory,
            GateISetIntentReceiptV3 receipt)
        {
            var intent = _formalAuthority.ActiveSetIntent?.Intent;
            if (intent == null || receipt == null || classification == null || trajectory == null ||
                !StableId(actor).Equals(intent.Organizer) ||
                classification.ExecutableEnvelope.Identity != intent.ExecutionClassification.ExecutableEnvelope.Identity ||
                trajectory.ArtifactIdentity != intent.TrajectoryArtifact.ArtifactIdentity)
                throw new InvalidOperationException("Accepted Set does not exactly match the pending Gate I intent.");
            AcceptedSetContactWriterCount++;
            var accepted = new AcceptedSetEvidenceV3(StableId(actor),
                classification.ExecutableEnvelope.Identity, trajectory.ArtifactIdentity);
            var players = _players.OrderBy(pair => pair.Key.Team).ThenBy(pair => pair.Key.RosterSlot)
                .Select(pair => new GateITacticalPlayerV3(pair.Value.StableId, ToSide(pair.Key.Team),
                    ToSimulation(pair.Value.transform.position), pair.Key.Team == actor.Team,
                    _v3RulesAdapter.Eligibility.For(pair.Value.StableId).CanBlock,
                    pair.Value.Ability.Derived)).ToArray();
            _formalAuthority.AttackCoordinator.AcceptSet(new GateIAcceptedSetV3(intent.PlanRevision,
                _formalAuthority.NextSourceSequence(), accepted), new AttackPlanningRequestV3(intent.PlanRevision,
                intent, accepted, players, _trajectoryPredictionProviderV4,
                SimulationParameters, _matchContext.PhysicsConfigurationHash,
                // Gate I candidate trajectories start from this accepted Set's
                // physical ball snapshot, not from a planner-invented version.
                (long)(uint)BitConverter.ToInt32(
                    BitConverter.GetBytes(_ball.SimulationTime), 0)));
            _formalAuthority.AttackCoordinator.PublishThreat(intent.PlanRevision, _formalAuthority.NextSourceSequence());
            var defending = Opponent(actor.Team);
            var defensePlayers = _players.Where(pair => pair.Key.Team == defending)
                .OrderBy(pair => pair.Key.RosterSlot).Select(pair => new DefensePlayerSnapshotV3(
                    pair.Value.StableId, ToSimulation(pair.Value.transform.position), BaseMovementSpeed,
                    pair.Value.Ability.Jump, IsFrontRow(pair.Key))).ToArray();
            var claims = new[] { RallyPlanSpatialClaimV3.FrontRight, RallyPlanSpatialClaimV3.FrontCenter,
                RallyPlanSpatialClaimV3.FrontLeft, RallyPlanSpatialClaimV3.BackLeft,
                RallyPlanSpatialClaimV3.BackCenter, RallyPlanSpatialClaimV3.BackRight };
            var assignments = defensePlayers.Select((player, index) => new PlayerResponsibilityAssignmentV3(
                player.Id, RallyPlanTaskV3.Defend, RallyPlanConditionV3.Always, claims[index],
                RallyPlanBranchV3.Primary, 1f, index + 1)).ToArray();
            var exits = new[] { new ReorganizationExitV3("gate-i-exit-" + intent.PlanRevision,
                defensePlayers[0].Id, "recover") };
            var publicThreat = _formalAuthority.AttackCoordinator.State.Phase ==
                               AttackDefenseAuthorityPhaseV3.ThreatPublished
                ? GetGateIPublicThreat()
                : throw new InvalidOperationException("Gate I threat was not published.");
            var perception = GateJEnabled
                ? CreateGateJPerceptionReceipt(
                    defending,
                    intent.PlanRevision,
                    _formalAuthority.CurrentSourceSequence,
                    intent.TrajectoryArtifact.ArtifactIdentity,
                    publicThreat,
                    defensePlayers.Where(player => !player.IsFrontRow)
                        .Select(player => player.Id).ToArray(),
                    defensePlayers.First(player => !player.IsFrontRow).Id,
                    true)
                : null;
            if (perception != null)
                _formalAuthority.AttackCoordinator.ApplyPerception(perception);
            var defense = new JointDefensePlanner().Plan(new JointDefensePlanningRequestV3(intent.PlanRevision,
                Opponent(ToSide(actor.Team)), publicThreat,
                defensePlayers, assignments, exits, perception));
            _formalAuthority.AttackCoordinator.CommitDefense(intent.PlanRevision, _formalAuthority.NextSourceSequence(), defense);
            _formalAuthority.AttackCoordinator.CommitFinalAttack(intent.PlanRevision, _formalAuthority.NextSourceSequence());
        }

        private void RecordGateISetCalibration(
            PlayerId setter, SetTechniqueStyle style, float contactCenteredness)
        {
            var intent = _formalAuthority.AttackCoordinator.State.Plan?.SetIntent;
            var selected = _formalAuthority.AttackCoordinator.State.Plan?.SelectedAction;
            if (intent == null || selected == null)
                throw new InvalidOperationException(
                    "Accepted Gate I Set requires its committed final attack plan.");

            var prediction = TrajectoryPredictor.Predict(
                _ball.State,
                SimulationParameters,
                SimulatedBall.DefaultFixedStep,
                Mathf.Max(2f, intent.SetFlightSeconds + .5f),
                260,
                GroundHeight);
            var actualArrival = ClosestSetArrival(
                prediction,
                intent.Target,
                intent.SetFlightSeconds);
            var quality = SetQualityAssessment.Evaluate(new SetQualityInput(
                GroundDistance(actualArrival.Position, intent.Target),
                Mathf.Abs(actualArrival.Position.Y - intent.Target.Y),
                Mathf.Abs(actualArrival.TimeSeconds - intent.SetFlightSeconds),
                Mathf.Abs(actualArrival.Position.Z),
                actualArrival.TimeSeconds));

            _lastSetQualityAssessment = quality;
            _lastSetAttackActor = PlayerForStableId(selected.Actor).Id;
            _lastSetSetterActor = setter;
            _lastAttackContactOutcome = AttackContactOutcome.FullAttack;
            _lastSetChainSuccessRecorded = false;
            _activeSetChain = true;
            _lastAttackResponsibility = SetQualityAssessment.PrimaryResponsibility(
                quality.Grade,
                AttackOutcome.InPlay);
            TotalSets++;
            if (quality.Grade == SetQualityGrade.A)
                GradeASets++;
            if (quality.IsAdjustable)
                AttackableSets++;
            else
                DirectSetErrors++;

            // Gate I owns the immutable set target and final route. These retain
            // the calibration observability previously recorded by the legacy
            // set-to-attack scheduler without reopening that writer.
            GeometricSetTargetSelections++;
            GeometricAttackRouteSelections++;
            RecordSetCalibration(setter, style, contactCenteredness,
                recordLegacyRouteStyle: false);
        }

        // The coordinator keeps candidates private; the committed public threat is
        // the only cross-team input used for joint defense.
        private PublicAttackThreatV3 GetGateIPublicThreat()
        {
            var plan = _formalAuthority.AttackCoordinator.State.Plan;
            // Before defense commitment the plan is intentionally not materialized.
            // The coordinator's public threat is carried by the current intent's
            // attack result, so this accessor is populated by the coordinator API.
            return _formalAuthority.AttackCoordinator.PublicThreat;
        }

        private RuleTransitionV3 ObserveAcceptedContactV3(
            PlayerId actor,
            TechniqueAction action,
            int contactGroup,
            PendingV3AuthorityContact authorityContact)
        {
            if (_v3RulesAdapter == null)
            {
                return null;
            }

            RuleTransitionV3 transition;
            LegacyRuleOutcomeV3 legacyOutcome;
            ShadowScenarioV3 scenario;
            string diagnosticExceptionType;
            if (V3RulesMode == V3RulesMode.Authority)
            {
                transition = authorityContact.Transition;
                legacyOutcome = authorityContact.LegacyOutcome;
                scenario = authorityContact.Scenario;
                diagnosticExceptionType = authorityContact.DiagnosticExceptionType;
            }
            else
            {
                transition = _v3RulesAdapter.ObserveAcceptedContact(
                    StableId(actor),
                    ToSide(actor.Team),
                    ToV3Classification(action),
                    contactGroup);
                legacyOutcome = LegacyRuleOutcomeV3.Accept();
                scenario = DetermineShadowScenario(
                    StableId(actor),
                    ToV3Classification(action),
                    legacyOutcome);
                diagnosticExceptionType = null;
            }

            V3RuleTransitions++;
            try
            {
                if (!string.IsNullOrEmpty(diagnosticExceptionType))
                {
                    throw new InvalidOperationException(diagnosticExceptionType);
                }

                var comparison = LegacyRulesShadowComparatorV3.Compare(
                    legacyOutcome,
                    transition,
                    scenario);
                switch (comparison.DifferenceKind)
                {
                    case RulesShadowDifferenceKindV3.ExactParity:
                        V3RuleParityMatches++;
                        break;
                    case RulesShadowDifferenceKindV3.IntentionalV3Correction:
                        V3RuleIntentionalCorrections++;
                        break;
                    case RulesShadowDifferenceKindV3.UnexpectedMismatch:
                        V3RuleUnexpectedMismatches++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                LastV3RuleDiagnostic = comparison.Diagnostic;
            }
            catch (Exception exception)
            {
                if (string.IsNullOrEmpty(diagnosticExceptionType))
                {
                    diagnosticExceptionType = exception.GetType().FullName;
                }

                V3RuleUnexpectedMismatches++;
                LastV3RuleDiagnostic =
                    $"scenario={scenario};exceptionType={diagnosticExceptionType};" +
                    $"actor={StableId(actor).Value};classification={ToV3Classification(action)};" +
                    $"contactGroup={contactGroup}";
            }

            _lastAcceptedV3Actor = StableId(actor);
            _lastAcceptedV3Classification = ToV3Classification(action);
            return transition;
        }

        private void RecordShadowPlanV3(
            RuleTransitionV3 acceptedTransition,
            BallTrajectoryPredictionArtifactV4 acceptedTrajectoryArtifact,
            StablePlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            int contactGroup)
        {
            if (_v3RulesAdapter == null)
            {
                return;
            }

            if (acceptedTransition == null || !acceptedTransition.Accepted)
            {
                RecordShadowPlanDiagnostic("v3TransitionNotAccepted");
                return;
            }

            if (acceptedTrajectoryArtifact == null)
            {
                RecordShadowPlanDiagnostic("missingTrajectoryArtifact");
                return;
            }

            try
            {
                RecordAcceptedShadowPlanV3(
                    acceptedTransition,
                    acceptedTrajectoryArtifact,
                    actor,
                    side,
                    classification,
                    contactGroup);
            }
            catch (Exception exception)
            {
                // Shadow output is diagnostic-only; never let it change a live rally.
                RecordShadowPlanDiagnostic(exception.GetType().FullName);
            }
        }

        private void RecordAcceptedShadowPlanV3(
            RuleTransitionV3 acceptedTransition,
            BallTrajectoryPredictionArtifactV4 acceptedTrajectoryArtifact,
            StablePlayerId actor,
            TeamSide side,
            RallyContactClassificationV3 classification,
            int contactGroup)
        {

            var eligibility = CreateV3Eligibility(_matchContext);
            var playerFacts = new List<PlayerWorldSnapshotV3>(eligibility.Players.Count);
            for (var index = 0; index < eligibility.Players.Count; index++)
            {
                var eligiblePlayer = eligibility.Players[index];
                var player = PlayerForStableId(eligiblePlayer.PlayerId);
                playerFacts.Add(new PlayerWorldSnapshotV3(
                    eligiblePlayer.PlayerId,
                    eligiblePlayer.Side,
                    eligiblePlayer.RegisteredPosition,
                    ToSimulation(player.transform.position),
                    SimVector3.Zero,
                    player.PreparedForward,
                    player.ReplayScheduledAction,
                    RallyCommitmentStateV3.Uncommitted,
                    0f));
            }

            var ball = _ball.State;
            var snapshot = new RallyWorldSnapshotV3(
                new BallWorldSnapshotV3(
                    ball.Position,
                    ball.Velocity,
                    SimVector3.Zero,
                    ball.Radius,
                    _ball.SimulationTime),
                playerFacts,
                acceptedTransition.After,
                eligibility,
                new CourtConfigurationV3(
                    CourtBuilder.HalfWidth,
                    CourtHalfLength,
                    CourtBuilder.NetHeight),
                new AcceptedRuleEventV3(
                    PlanCoverageReason.WithinConditionalEnvelope,
                    RallyPlanConditionV3.Always,
                    actor,
                    side,
                    classification,
                    contactGroup),
                V3RuleTransitions);
            var artifactIdentity = acceptedTrajectoryArtifact.ArtifactIdentity;
            var revision = V3RuleTransitions;
            var provisional = new RallyPlanV3(
                snapshot,
                DeterministicRallyPlanComposerV3.Compose(snapshot, TeamSide.Home, artifactIdentity),
                DeterministicRallyPlanComposerV3.Compose(snapshot, TeamSide.Away, artifactIdentity),
                artifactIdentity,
                revision,
                V3RuleTransitions,
                PlanCoverageDecision.Covered(
                    revision.ToString(),
                    PlanCoverageReason.WithinConditionalEnvelope));
            var coverage = DeterministicRallyPlanComposerV3.EvaluateCoverage(
                provisional,
                snapshot.LatestEvent);
            NotifyShadowPlanRecorded(new RallyPlanV3(
                snapshot,
                provisional.HomePlan,
                provisional.AwayPlan,
                artifactIdentity,
                revision,
                V3RuleTransitions,
                coverage));
        }

        private void NotifyShadowPlanRecorded(RallyPlanV3 plan)
        {
            var handlers = ReplayShadowPlanRecorded;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<RallyPlanV3> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(plan);
                }
                catch (Exception exception)
                {
                    RecordShadowPlanDiagnostic(exception.GetType().FullName);
                }
            }
        }

        private void RecordShadowPlanDiagnostic(string reason)
        {
            ShadowPlanRecordingFailures++;
            LastShadowPlanRecordingDiagnostic = reason;
        }

        private PrototypePlayerAgent PlayerForStableId(StablePlayerId stableId)
        {
            foreach (var player in _players.Values)
            {
                if (player.StableId.Equals(stableId))
                {
                    return player;
                }
            }

            throw new InvalidOperationException(
                "Shadow plan eligibility player is not registered in the formal match.");
        }

        private void SynchronizeLegacyCompatibility(PendingV3AuthorityContact authorityContact)
        {
            try
            {
                if (authorityContact.LegacyEvaluation.HasValue &&
                    authorityContact.LegacyEvaluation.Value.Disposition ==
                    RallyContactDisposition.Accept)
                {
                    var accepted = _touchState.Accept(
                        authorityContact.Actor,
                        authorityContact.Action,
                        authorityContact.ContactSimulationTime);
                    if (accepted.Disposition == RallyContactDisposition.Accept)
                    {
                        return;
                    }
                }

                _touchState.SynchronizeAuthoritativeContact(
                    authorityContact.Actor,
                    authorityContact.Action,
                    authorityContact.Transition.After.CountedHits);
            }
            catch (Exception exception)
            {
                authorityContact.SetDiagnosticException(exception.GetType().FullName);
            }
        }

        private ShadowScenarioV3 DetermineShadowScenario(
            StablePlayerId actor,
            RallyContactClassificationV3 classification,
            LegacyRuleOutcomeV3 legacyOutcome)
        {
            if (_lastAcceptedV3Actor.HasValue &&
                _lastAcceptedV3Actor.Value.Equals(actor) &&
                _lastAcceptedV3Classification == RallyContactClassificationV3.BlockContact &&
                classification == RallyContactClassificationV3.TeamContact &&
                legacyOutcome?.Disposition == LegacyRuleDispositionV3.Fault &&
                legacyOutcome.Reason == "ConsecutiveCountedTouch")
            {
                return ShadowScenarioV3.BlockerFirstCountedContact;
            }

            if (classification == RallyContactClassificationV3.TeamContact &&
                legacyOutcome?.Disposition == LegacyRuleDispositionV3.Ignore &&
                legacyOutcome.Reason == "WrongAction")
            {
                return ShadowScenarioV3.IncidentalCountedContact;
            }

            return ShadowScenarioV3.Other;
        }

        private static LegacyRuleOutcomeV3 ToLegacyOutcome(RallyContactEvaluation evaluation)
        {
            return evaluation.Disposition switch
            {
                RallyContactDisposition.Accept => LegacyRuleOutcomeV3.Accept(),
                RallyContactDisposition.Fault =>
                    LegacyRuleOutcomeV3.Fault(evaluation.Reason.ToString()),
                _ => LegacyRuleOutcomeV3.Ignore(evaluation.Reason.ToString())
            };
        }

        private sealed class PendingV3AuthorityContact
        {
            public PendingV3AuthorityContact(
                PlayerId actor,
                TechniqueAction action,
                int contactGroup,
                float contactSimulationTime,
                RuleTransitionV3 transition,
                AttackGeometryFactV3 observedAttackGeometry,
                RallyContactEvaluation? legacyEvaluation,
                LegacyRuleOutcomeV3 legacyOutcome,
                ShadowScenarioV3 scenario,
                string diagnosticExceptionType)
            {
                Actor = actor;
                Action = action;
                ContactGroup = contactGroup;
                ContactSimulationTime = contactSimulationTime;
                Transition = transition;
                ObservedAttackGeometry = observedAttackGeometry;
                LegacyEvaluation = legacyEvaluation;
                LegacyOutcome = legacyOutcome;
                Scenario = scenario;
                DiagnosticExceptionType = diagnosticExceptionType;
            }

            public PlayerId Actor { get; }

            public TechniqueAction Action { get; }

            public int ContactGroup { get; }

            public float ContactSimulationTime { get; }

            public RuleTransitionV3 Transition { get; }

            public AttackGeometryFactV3 ObservedAttackGeometry { get; }

            public RallyContactEvaluation? LegacyEvaluation { get; }

            public LegacyRuleOutcomeV3 LegacyOutcome { get; }

            public ShadowScenarioV3 Scenario { get; }

            public string DiagnosticExceptionType { get; private set; }

            public void SetDiagnosticException(string exceptionType)
            {
                if (string.IsNullOrEmpty(DiagnosticExceptionType))
                {
                    DiagnosticExceptionType = exceptionType;
                }
            }
        }

        private static RallyContactClassificationV3 ToV3Classification(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Block => RallyContactClassificationV3.BlockContact,
                TechniqueAction.Serve => RallyContactClassificationV3.ServeContact,
                TechniqueAction.Receive => RallyContactClassificationV3.TeamContact,
                TechniqueAction.Set => RallyContactClassificationV3.TeamContact,
                TechniqueAction.Attack => RallyContactClassificationV3.TeamContact,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
        }

        private void ScheduleAttackFromActualSet(
            TeamRallyDecision provisionalDecision,
            float plannedFlightSeconds,
            PlayerId setterActor,
            BallTrajectoryPredictionArtifactV4 trajectoryArtifact)
        {
            if (provisionalDecision == null || !provisionalDecision.HasDecision ||
                !provisionalDecision.AttackApproach.HasValue ||
                !provisionalDecision.AttackContactPlan.HasValue)
            {
                ScheduleDecision(
                    provisionalDecision,
                    plannedFlightSeconds,
                    trajectoryArtifact);
                return;
            }

            var plannedContact = provisionalDecision.AttackContactPlan.Value;
            var intendedContactCenter = _scheduledGeometricSetTarget ?? plannedContact.ContactCenter;
            _scheduledGeometricSetTarget = null;
            var prediction = TrajectoryPredictor.Predict(
                _ball.State,
                SimulationParameters,
                SimulatedBall.DefaultFixedStep,
                Mathf.Max(2f, plannedFlightSeconds + 0.5f),
                260,
                GroundHeight);
            var actualArrival = ClosestSetArrival(
                prediction,
                intendedContactCenter,
                plannedFlightSeconds);
            var horizontalError = GroundDistance(
                actualArrival.Position,
                intendedContactCenter);
            var quality = SetQualityAssessment.Evaluate(new SetQualityInput(
                horizontalError,
                Mathf.Abs(actualArrival.Position.Y - intendedContactCenter.Y),
                Mathf.Abs(actualArrival.TimeSeconds - plannedFlightSeconds),
                Mathf.Abs(actualArrival.Position.Z),
                actualArrival.TimeSeconds));
            var attacker = _players[provisionalDecision.Actor];
            var setter = _players[setterActor];
            var setterPosition = new SimVector3(
                setter.transform.position.x,
                setter.transform.position.y,
                setter.transform.position.z);
            var setterDepthFromNet = new TeamCourtFrame(provisionalDecision.Actor.Team)
                .ToLocal(setterPosition).Z * -1f;
            var replan = SetAttackReplanner.Replan(
                provisionalDecision.AttackApproach.Value,
                plannedContact,
                actualArrival.Position,
                actualArrival.TimeSeconds,
                attacker.Ability.PlannedAttackContactHeightMeters,
                provisionalDecision.Actor.Role,
                provisionalDecision.Actor.Team,
                setterDepthFromNet,
                quality);
            var resumedApproachStart = new SimVector3(
                attacker.transform.position.x,
                0f,
                attacker.transform.position.z);
            var resumedApproach = new AttackApproachPlan(
                resumedApproachStart,
                replan.Approach.Takeoff,
                GroundDistance(resumedApproachStart, replan.Approach.Takeoff),
                replan.Approach.JumpQuality,
                replan.Approach.AnglePenalty);
            var replacement = new TeamRallyDecision(
                provisionalDecision.Actor,
                TechniqueAction.Attack,
                replan.ContactPlan.ContactCenter,
                replan.Approach.Takeoff,
                provisionalDecision.BallTarget,
                provisionalDecision.Score,
                provisionalDecision.Candidates,
                resumedApproach,
                replan.ContactPlan);

            _lastSetQualityAssessment = quality;
            _lastPlannedAttackContactCenter = intendedContactCenter;
            _lastReplannedAttackContactCenter = replan.ContactPlan.ContactCenter;
            _lastSetReplanOutcome = replan.Outcome;
            _lastSetAttackActor = provisionalDecision.Actor;
            _lastSetSetterActor = setterActor;
            _lastAttackContactOutcome = replan.ContactPlan.Outcome;
            _lastSetChainSuccessRecorded = false;
            _activeSetChain = true;
            _lastAttackResponsibility = SetQualityAssessment.PrimaryResponsibility(
                quality.Grade,
                replan.Outcome);
            TotalSets++;
            if (quality.Grade == SetQualityGrade.A)
            {
                GradeASets++;
            }

            if (quality.IsAdjustable)
            {
                AttackableSets++;
            }
            else
            {
                DirectSetErrors++;
            }

            _pendingReplaySetChain = new ReplaySetChainEvent(
                intendedContactCenter,
                replan.ContactPlan.ContactCenter,
                quality.Grade,
                replan.ContactPlan.Outcome,
                _lastAttackResponsibility,
                quality.Reason);
            Debug.Log(
                $"[{_configuration.LogTag}] set-quality team={provisionalDecision.Actor.Team} " +
                $"grade={quality.Grade} replan={replan.ContactPlan.Outcome} " +
                $"replanned=({replan.ContactPlan.ContactCenter.X:0.00}," +
                $"{replan.ContactPlan.ContactCenter.Y:0.00}," +
                $"{replan.ContactPlan.ContactCenter.Z:0.00}) {quality.Reason}");

            if (replan.OpensSpikeContactWindow)
            {
                RecordNormalAttackPlanBand(
                    provisionalDecision.Actor.Role,
                    provisionalDecision.Actor.Team,
                    setterDepthFromNet,
                    replan.Approach.Takeoff);
                attacker.ContinueAttackPreparation(
                    resumedApproach,
                    replan.ContactPlan,
                    _ball.SimulationTime + Mathf.Max(0.1f, actualArrival.TimeSeconds));
                ScheduleDecision(
                    replacement,
                    Mathf.Max(0.1f, actualArrival.TimeSeconds),
                    trajectoryArtifact);
                return;
            }

            ScheduleControlledHandling(
                replacement,
                replan,
                Mathf.Max(0.1f, actualArrival.TimeSeconds));
        }

        private void RecordNormalAttackPlanBand(
            PlayerRole attackerRole,
            TeamId attackingTeam,
            float setterDepthFromNet,
            SimVector3 takeoff)
        {
            var band = AttackBandPolicy.Resolve(attackerRole, setterDepthFromNet);
            var localTakeoff = new TeamCourtFrame(attackingTeam).ToLocal(takeoff);
            var takeoffDepth = -localTakeoff.Z;
            NormalAttackPlans++;
            if (takeoffDepth + 0.0001f >= band.NearDepth &&
                takeoffDepth <= band.FarDepth + 0.0001f)
            {
                NearNetAttackPlans++;
            }
        }

        private void RecordSetCalibration(
            PlayerId setter,
            SetTechniqueStyle style,
            float contactCenteredness,
            bool recordLegacyRouteStyle = true)
        {
            const float inSystemCenteredness = 0.85f;
            _lastSetWasSetter = setter.Role == PlayerRole.Setter &&
                                contactCenteredness >= inSystemCenteredness;
            if (_lastSetWasSetter && _lastSetQualityAssessment.HasValue)
            {
                InSystemSetterSets++;
                if (_lastSetQualityAssessment.Value.IsAdjustable)
                {
                    InSystemAttackableSets++;
                }

                if (_lastSetQualityAssessment.Value.Grade == SetQualityGrade.A)
                {
                    GradeASetterSets++;
                }
            }

            if (recordLegacyRouteStyle &&
                (style == SetTechniqueStyle.SideLeftTwoHand ||
                style == SetTechniqueStyle.SideRightTwoHand)
            )
            {
                NormalSideSets++;
            }
        }

        private static SetFlightSolution SolveSetFlightWithFallback(SetFlightRequest request)
        {
            try
            {
                return SetFlightSolver.Solve(request);
            }
            catch (InvalidOperationException)
            {
                var fallbackRhythms = new[] { SetRhythm.Adjustment, SetRhythm.HighBall };
                foreach (var rhythm in fallbackRhythms)
                {
                    if (rhythm == request.Rhythm)
                    {
                        continue;
                    }

                    try
                    {
                        return SetFlightSolver.Solve(new SetFlightRequest(
                            rhythm,
                            request.Start,
                            request.Target,
                            request.PassQuality,
                            request.ApproachReadiness,
                            request.Parameters,
                            request.FixedStepSeconds));
                    }
                    catch (InvalidOperationException)
                    {
                        // Try the next slower rhythm before declaring the target impossible.
                    }
                }

                var emergencySteps = Math.Max(
                    1,
                    Mathf.RoundToInt(1.8f / request.FixedStepSeconds));
                var emergencySeconds = emergencySteps * request.FixedStepSeconds;
                var emergencyVelocity = ReturnVelocitySolver.Solve(
                    request.Start,
                    request.Target,
                    emergencySeconds,
                    request.FixedStepSeconds,
                    request.Parameters);
                var emergencyState = new BallState(
                    request.Start,
                    emergencyVelocity.InitialVelocity,
                    SimulatedBall.DefaultRadius);
                var emergencyApex = emergencyState.Position;
                for (var index = 0; index < emergencySteps; index++)
                {
                    BallIntegrator.Step(emergencyState, request.FixedStepSeconds, request.Parameters);
                    if (emergencyState.Position.Y > emergencyApex.Y)
                    {
                        emergencyApex = emergencyState.Position;
                    }
                }

                return new SetFlightSolution(
                    emergencySeconds,
                    emergencySteps,
                    emergencyVelocity.InitialVelocity,
                    emergencyApex);
            }
        }

        private void ScheduleControlledHandling(
            TeamRallyDecision decision,
            SetAttackReplan replan,
            float arrivalSeconds)
        {
            _scheduledDecision = decision;
            _scheduledPrimaryActor = decision.Actor;
            _scheduledBlockers.Clear();
            _contactDeadlineActive = true;
            _controlledHandlingActive = true;
            _expectedContactTime = _ball.SimulationTime + arrivalSeconds;
            var target = new SimVector3(
                decision.BallTarget.X,
                GroundHeight + SimulatedBall.DefaultRadius,
                decision.BallTarget.Z);
            var outgoing = ReturnVelocitySolver.Solve(
                replan.ContactPlan.ContactCenter,
                target,
                Mathf.Max(
                    ControlledHandlingMinimumFlightSeconds,
                    TacticFor(decision.Actor.Team).AttackFlightSeconds),
                SimulatedBall.DefaultFixedStep,
                SimulationParameters).InitialVelocity;
            var actor = _players[decision.Actor];
            var execution = SkillExecutionResolver.Resolve(
                actor.Ability,
                TechniqueAction.Receive,
                StablePlayerNumber(decision.Actor),
                _tacticRevision,
                SuccessfulContacts,
                7351,
                0.72f);
            ExecutionErrorApplications++;
            var executionCandidateCategory = ExecutionCandidateCategoryV4.Receive;
            var executionIntentIdentity =
                $"execution:{(_matchContext == null ? "prototype" : _matchContext.SessionId.ToString("D"))}:" +
                $"controlled-handling:{_tacticRevision}:{_decisionCoordinator.DecisionIndex}:{SuccessfulContacts}:" +
                $"{(int)decision.Actor.Team}:{(int)decision.Actor.Role}:{decision.Actor.RosterSlot}";
            var executionSamplingKey = executionIntentIdentity + ":sample";
            var plannedExecutionEnvelope = PlanExecutionEnvelopeV4(
                actor.Ability.Derived,
                new ExecutionIntentV4(
                    executionIntentIdentity,
                    executionCandidateCategory,
                    target,
                    outgoing,
                    requestedEffort: 0.6f),
                executionSamplingKey,
                ExecutionEnvelopePolicyV4.Default);
            _lastPlannedExecutionEnvelopeV4 = plannedExecutionEnvelope;
            var executionSample = new ExecutionSampleV4(
                plannedExecutionEnvelope.Identity,
                executionSamplingKey,
                executionCandidateCategory,
                target + execution.ContactPositionError,
                (outgoing * execution.SurfaceSpeedScale) + execution.TargetVelocityError,
                plannedExecutionEnvelope.RequestedEffort);
            _lastExecutionSampleClassificationV4 = ExecuteExecutionSampleV4(
                plannedExecutionEnvelope,
                executionSample);
            if (_lastExecutionSampleClassificationV4.Kind is
                ExecutionSampleClassificationKindV4.UnexpectedExecutionSample or
                ExecutionSampleClassificationKindV4.EnvelopeExceeded)
            {
                return;
            }
            actor.ScheduleControlledHandlingContact(
                _expectedContactTime,
                _lastExecutionSampleClassificationV4,
                execution,
                NextContactGroup(),
                decision.AttackApproach.Value,
                replan.ContactPlan,
                _ball.SimulationTime);
            MovementAssignments++;
            TotalMovementShortfall += actor.MovementShortfall;
            _touchState.OpenWindow(new RallyContactWindow(
                decision.Actor.Team,
                TechniqueAction.Receive,
                _expectedContactTime - ContactWindowLead,
                _expectedContactTime + ContactWindowTail,
                new[] { decision.Actor }));
        }

        private static TrajectorySample ClosestSetArrival(
            TrajectoryPrediction prediction,
            SimVector3 plannedContactCenter,
            float plannedFlightSeconds)
        {
            var best = prediction.Samples[0];
            var bestScore = float.PositiveInfinity;
            foreach (var sample in prediction.Samples)
            {
                if (sample.TimeSeconds < 0.08f)
                {
                    continue;
                }

                var offset = sample.Position - plannedContactCenter;
                var timeError = sample.TimeSeconds - plannedFlightSeconds;
                var score = offset.SqrMagnitude + (timeError * timeError * 0.04f);
                if (score < bestScore)
                {
                    best = sample;
                    bestScore = score;
                }
            }

            return best;
        }

        private static float GroundDistance(SimVector3 left, SimVector3 right)
        {
            var x = left.X - right.X;
            var z = left.Z - right.Z;
            return Mathf.Sqrt((x * x) + (z * z));
        }

        private StablePlayerId[] RotationOrder(TeamSide side)
        {
            var rotation = new StablePlayerId[6];
            for (var position = 1; position <= rotation.Length; position++)
            {
                rotation[position - 1] = _set.PlayerAtRotationPosition(side, position);
            }

            return rotation;
        }

        private OnCourtEligibilitySnapshot CreateV3Eligibility(MatchContextV4 context)
        {
            return OnCourtLineupRulesV3.Create(
                context,
                RotationOrder(TeamSide.Home),
                RotationOrder(TeamSide.Away),
                _set.ServerFor(TeamSide.Home),
                _set.ServerFor(TeamSide.Away),
                Array.Empty<LiberoReplacementV3>());
        }

        private void PreparePhysicalBlock(
            TeamId attackingTeam,
            SimVector3 contactCenter,
            SimVector3 intendedOutgoing,
            float attackContactTime)
        {
            var projected = new BallState(
                contactCenter,
                intendedOutgoing,
                SimulatedBall.DefaultRadius);
            if (!NetPlaneInterception.TryPredict(
                    projected,
                    SimulationParameters,
                    SimulatedBall.DefaultFixedStep,
                    1.5f,
                    out var intercept))
            {
                _scheduledBlockers.Clear();
                _scheduledBlockPrimary = null;
                return;
            }

            var defendingTeam = Opponent(attackingTeam);
            var blockTime = attackContactTime + intercept.TimeSeconds;
            var availableSeconds = Mathf.Max(0f, blockTime - _ball.SimulationTime);
            var blockTarget = BlockRootTarget(defendingTeam, intercept.Point);
            var unit = SelectBlockUnit(
                defendingTeam,
                ToSimulation(blockTarget),
                availableSeconds);
            _scheduledBlockers.Clear();
            _scheduledBlockPrimary = unit.Blockers.Count == 0
                ? (PlayerId?)null
                : unit.Blockers[0].Id;
            for (var index = 0; index < unit.Blockers.Count; index++)
            {
                var blocker = unit.Blockers[index];
                var target = UnitBlockRootTarget(
                    defendingTeam,
                    intercept.Point,
                    blocker,
                    unit.Blockers[0]);
                target = _players[blocker.Id].ResolveBlockRootTarget(intercept.Point, target);
                var rebound = BlockReboundVelocity(attackingTeam);
                var classification = CreateBlockExecutionClassification(
                    blocker.Id,
                    intercept.Point,
                    rebound,
                    out var trajectoryArtifact);
                _players[blocker.Id].ScheduleBlockContact(
                    blockTime,
                    target,
                    _ball.SimulationTime,
                    rebound,
                    NextContactGroup(),
                    classification,
                    trajectoryArtifact);
                _scheduledBlockers.Add(blocker.Id);
                BlockSupportAssignments++;
                if (_configuration.RosterSize == 6 && !IsFrontRow(blocker.Id))
                {
                    ScheduledBackRowBlockers++;
                }
            }

            MaximumScheduledBlockers = Mathf.Max(MaximumScheduledBlockers, _scheduledBlockers.Count);
            if (_scheduledBlockers.Count >= 2)
            {
                ScheduledMultiBlockUnits++;
            }

            if (_decisionCoordinator.TrySelectCoveragePlayer(
                    CaptureTeamPlayerSnapshots(defendingTeam),
                    _scheduledBlockers,
                    ToSimulation(CoverageTarget(
                        defendingTeam,
                        intercept.Point)),
                    out var cover))
            {
                _players[cover].ScheduleSupportAction(
                    TechniqueAction.Receive,
                    blockTime + 0.12f,
                    CoverageTarget(defendingTeam, intercept.Point),
                    _ball.SimulationTime);
                CoverageSupportAssignments++;
            }
            Debug.Log(
                $"[{_configuration.LogTag}] block-plan team={defendingTeam} " +
                $"blockers={_scheduledBlockers.Count} intercept=({intercept.Point.X:0.00}," +
                $"{intercept.Point.Y:0.00},{intercept.Point.Z:0.00})");
        }

        private void SchedulePhysicalBlock(
            TeamId attackingTeam,
            TeamRallyDecision attackDecision,
            PlayerBallContactEvent attackContact)
        {
            _scheduledDecision = null;
            _scheduledPrimaryActor = null;
            _contactDeadlineActive = false;
            if (_scheduledBlockers.Count == 0 ||
                !NetPlaneInterception.TryPredict(
                    _ball.State,
                    SimulationParameters,
                    SimulatedBall.DefaultFixedStep,
                    1.5f,
                    out var intercept))
            {
                _scheduledBlockers.Clear();
                _scheduledBlockPrimary = null;
                return;
            }

            var blockTime = attackContact.ContactSimulationTime + intercept.TimeSeconds;
            var blockers = OrderedScheduledBlockers();
            var primary = _scheduledBlockPrimary.HasValue &&
                          _scheduledBlockers.Contains(_scheduledBlockPrimary.Value)
                ? _scheduledBlockPrimary.Value
                : blockers[0];
            var primarySnapshot = BlockSnapshot(primary);
            var appliedBlockTime = float.MaxValue;
            foreach (var blocker in blockers)
            {
                var blockerSnapshot = BlockSnapshot(blocker);
                var target = UnitBlockRootTarget(
                    blocker.Team,
                    intercept.Point,
                    blockerSnapshot,
                    primarySnapshot);
                target = _players[blocker].ResolveBlockRootTarget(intercept.Point, target);
                _players[blocker].RetargetBlockContact(
                    blockTime,
                    target,
                    BlockReboundVelocity(attackingTeam));
                appliedBlockTime = Mathf.Min(
                    appliedBlockTime,
                    _players[blocker].PhysicalBlockContactTime);
            }

            _touchState.OpenWindow(new RallyContactWindow(
                blockers[0].Team,
                TechniqueAction.Block,
                appliedBlockTime - ContactWindowLead,
                appliedBlockTime + ContactWindowTail,
                blockers));
            Debug.Log(
                $"[{_configuration.LogTag}] block-window team={blockers[0].Team} " +
                $"actors={blockers.Count} " +
                $"attack={attackDecision.Actor.Role} " +
                $"time={appliedBlockTime:0.00} predicted={blockTime:0.00} " +
                $"intercept=({intercept.Point.X:0.00},{intercept.Point.Y:0.00}," +
                $"{intercept.Point.Z:0.00})");
        }

        private ExecutionSampleClassificationV4 CreateBlockExecutionClassification(
            PlayerId blocker,
            SimVector3 interceptPoint,
            SimVector3 rebound,
            out BallTrajectoryPredictionArtifactV4 trajectoryArtifact)
        {
            trajectoryArtifact = null;
            if (_matchContext == null)
            {
                return null;
            }

            var identity = $"execution:{_matchContext.SessionId:D}:block:{_tacticRevision}:{_decisionCoordinator.DecisionIndex}:" +
                           $"{SuccessfulContacts}:{(int)blocker.Team}:{(int)blocker.Role}:{blocker.RosterSlot}";
            var samplingKey = identity + ":sample";
            var envelope = PlanExecutionEnvelopeV4(
                _players[blocker].Ability.Derived,
                new ExecutionIntentV4(
                    identity,
                    ExecutionCandidateCategoryV4.Block,
                    interceptPoint,
                    rebound,
                    .6f),
                samplingKey,
                ExecutionEnvelopePolicyV4.Default);
            var sample = new ExecutionSampleV4(
                envelope.Identity,
                samplingKey,
                ExecutionCandidateCategoryV4.Block,
                interceptPoint,
                rebound,
                envelope.RequestedEffort);
            var classification = ExecuteExecutionSampleV4(envelope, sample);
            if (classification.Kind is ExecutionSampleClassificationKindV4.UnexpectedExecutionSample or
                ExecutionSampleClassificationKindV4.EnvelopeExceeded)
            {
                throw new InvalidOperationException("Formal Block V4 sample must be executable.");
            }

            _lastPlannedExecutionEnvelopeV4 = envelope;
            _lastExecutionSampleClassificationV4 = classification;
            var stateVersion = (long)(uint)BitConverter.ToInt32(
                BitConverter.GetBytes(_ball.SimulationTime), 0);
            var samplingKeyForTrajectory = samplingKey + ":trajectory";
            var request = new BallTrajectoryPredictionRequestV4(
                blocker.Team == TeamId.Blue ? TeamSide.Home : TeamSide.Away,
                stateVersion,
                new BallState(interceptPoint, rebound, SimulatedBall.DefaultRadius),
                SimulationParameters,
                _matchContext.PhysicsConfigurationHash,
                samplingKeyForTrajectory,
                _matchContext.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                _matchContext.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                envelope.Identity,
                ExecutionDegradationStepV4.FullSampling);
            trajectoryArtifact = PredictSharedGate5TrajectoryV4(
                _trajectoryPredictionProviderV4,
                request,
                ExecutionEnvelopePolicyV4.Default);
            return classification;
        }

        private void HandleAcceptedBlock(PlayerBallContactEvent contact)
        {
            var blocker = contact.Candidate.Actor.Value;
            DisablePhysicalBlockWindows();
            _scheduledBlockers.Clear();
            _scheduledBlockPrimary = null;
            _scheduledPrimaryActor = null;
            _scheduledDecision = null;
            _plannedAttackDecision = null;
            _plannedAttackTrajectoryArtifactV4 = null;
            _pendingCrossingTeam = null;
            PhysicalBlockContacts++;
            BlockSupportActivations++;
            _blockImpactFeedback.Play(
                blocker.Team,
                ToUnity(contact.Hit.ImpactCenter),
                ToUnity(contact.Hit.Normal),
                contact.TechniqueResponse.FinalOutgoing.Magnitude);
            _status = $"{blocker.Team} {blocker.Role} BLOCK  " +
                      $"speed {contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0} m/s";

            _awaitingPostBlockCrossing = true;
            _postBlockerTeam = blocker.Team;
            _postBlockerActor = StableId(blocker);
            _postBlockImpactCenter = contact.Hit.ImpactCenter;
            _postBlockOutgoing = contact.TechniqueResponse.FinalOutgoing;
            _postAttackContinuationState =
                PostAttackContinuationStateV4.AwaitingBlockOutcome;
            PostBlockPossessionDeferrals++;
            PostBlockContinuations++;
            Debug.Log(
                $"[{_configuration.LogTag}] block-contact team={blocker.Team} actor={blocker.Role} " +
                $"awaiting-crossing impact=({contact.Hit.ImpactCenter.X:0.00}," +
                $"{contact.Hit.ImpactCenter.Y:0.00}," +
                $"{contact.Hit.ImpactCenter.Z:0.00}) outgoing=(" +
                $"{contact.TechniqueResponse.FinalOutgoing.X:0.00}," +
                $"{contact.TechniqueResponse.FinalOutgoing.Y:0.00}," +
                $"{contact.TechniqueResponse.FinalOutgoing.Z:0.00}) " +
                $"effect={BlockImpactEffects}");
        }

        private void HandleEnvironmentContact(EnvironmentCollisionHit hit)
        {
            if (!_rallyActive || _restartScheduled || HasResult || hit.Kind == EnvironmentContactKind.Net)
            {
                return;
            }

            var last = _touchState?.LastPhysicalTouch;
            _postAttackContinuationState =
                PostAttackContinuationStateV4.ResolvedByGround;
            ExpirePendingGateIReceiveWindows("GroundBeforeDefenseContact");
            ExpireActivePostAttackReceiveWindow("GroundBeforeDefenseContact");
            NotifyReplay(
                ReplayGroundContact,
                new ReplaySimpleEvent(
                    "GroundContact",
                    _ball.SimulationTime,
                    last.HasValue ? last.Value.Team : _touchState?.PossessionTeam ?? TeamId.Blue,
                    last.HasValue ? StableId(last.Value) : null));
            if (!last.HasValue)
            {
                var loser = _touchState?.PossessionTeam ?? FromSide(_set.ReceivingSide);
                var winner = Opponent(ToSide(loser));
                ResolveRally(
                    new RallyOutcome(winner, true, "ground before legal contact"),
                    null,
                    _scheduledPrimaryActor,
                    "ground before legal contact");
                GroundResolvedRallies++;
                return;
            }

            var outcome = MatchRallyReferee.ResolveGroundLanding(
                ToSide(last.Value.Team),
                hit.ContactPoint,
                CourtBuilder.HalfWidth,
                _configuration.CourtHalfLength);
            if (_awaitingPostBlockCrossing &&
                _postBlockerTeam.HasValue &&
                outcome.Winner == ToSide(_postBlockerTeam.Value))
            {
                PostBlockGroundPoints++;
            }
            _awaitingPostBlockCrossing = false;
            _postBlockerTeam = null;
            _postBlockerActor = null;
            AttributeAttackFault(last.Value, outcome);
            ResolveRally(
                outcome,
                outcome.IsFault ? null : last,
                outcome.IsFault ? last : null,
                outcome.Reason);
            if (_lastTouchWasBackSetAttack && outcome.IsFault)
            {
                BackSetAttackFaults++;
            }
            GroundResolvedRallies++;
        }

        private void ExpirePendingGateIReceiveWindows(string reason)
        {
            foreach (var receipt in _pendingGateIReceiveWindows)
            {
                PlayerForStableId(receipt.Actor).CancelScheduledContact();
                TakeGateIContactReceipt(receipt.Actor, receipt.Kind);
                RecordDefenseAttempt(
                    "DefenseAttemptExpired",
                    receipt,
                    PlayerForStableId(receipt.Actor).Id.Team,
                    reason);
            }

            _pendingGateIReceiveWindows.Clear();
        }

        private void ExpireActivePostAttackReceiveWindow(string reason)
        {
            if (_activePostAttackReceives.Count == 0)
            {
                _contactDeadlineActive = false;
                return;
            }

            foreach (var receipt in _activePostAttackReceives)
            {
                var player = PlayerForStableId(receipt.Actor);
                player.CancelScheduledContact();
                RecordDefenseAttempt(
                    "DefenseAttemptExpired",
                    receipt,
                    PlayerForStableId(receipt.Actor).Id.Team,
                    reason);
            }

            _activePostAttackReceives.Clear();
            _touchState?.CloseWindow();
            _scheduledDecision = null;
            _scheduledPrimaryActor = null;
            _contactDeadlineActive = false;
        }

        private bool TryResolvePostBlockSideFromLiveBall()
        {
            if (!_awaitingPostBlockCrossing || !_postBlockerTeam.HasValue ||
                _ball?.State == null)
            {
                return false;
            }

            if (Mathf.Abs(_ball.State.Position.Z) <=
                PostBlockSideResolutionDistance)
            {
                _postAttackContinuationState =
                    PostAttackContinuationStateV4.PendingSideResolution;
                return false;
            }

            var receivingTeam = _ball.State.Position.Z < 0f
                ? TeamId.Blue
                : TeamId.Orange;
            var blockerTeam = _postBlockerTeam.Value;
            var movingTowardNet =
                _ball.State.Position.Z * _ball.State.Velocity.Z < 0f;
            var legacyMovingFromBlockerTowardAttacker =
                receivingTeam == blockerTeam &&
                (blockerTeam == TeamId.Blue
                    ? _ball.State.Velocity.Z > 0f
                    : _ball.State.Velocity.Z < 0f);
            if (GateIAuthorityEnabled
                    ? movingTowardNet
                    : legacyMovingFromBlockerTowardAttacker)
            {
                // A block can be struck just outside the net corridor on
                // either side while already travelling through the plane.
                // Formal Gate I treats current position as non-destination
                // evidence and waits symmetrically. Legacy 3v3 retains its
                // established one-sided handoff semantics.
                _postAttackContinuationState =
                    PostAttackContinuationStateV4.PendingSideResolution;
                return false;
            }

            ResolvePostBlockContinuation(
                receivingTeam,
                _ball.State.Position,
                _ball.State.Velocity);
            return true;
        }

        private void ResolvePostBlockContinuation(
            TeamId receivingTeam,
            SimVector3 actualPosition,
            SimVector3 actualVelocity)
        {
            if (!_awaitingPostBlockCrossing || !_postBlockerTeam.HasValue)
            {
                return;
            }

            var blockerTeam = _postBlockerTeam.Value;
            _awaitingPostBlockCrossing = false;
            _postBlockerTeam = null;
            _postBlockerActor = null;
            _pendingCrossingTeam = null;

            if (!GateIAuthorityEnabled ||
                _formalAuthority?.AttackCoordinator == null)
            {
                BeginPossession(receivingTeam, ReceiveLeadTime());
                return;
            }

            AttackDefenseAuthorityReceipt openedReceipt;
            bool hadDeclaredOpportunity;
            if (receivingTeam == blockerTeam)
            {
                _postAttackContinuationState =
                    PostAttackContinuationStateV4.BlockingSideRecovery;
                hadDeclaredOpportunity = OpenPendingGateIReceiveWindow(
                    receivingTeam,
                    actualPosition,
                    actualVelocity,
                    out openedReceipt);
            }
            else
            {
                _postAttackContinuationState =
                    PostAttackContinuationStateV4.AttackingSideCoverage;
                ExpirePendingGateIReceiveWindows(
                    "ContinuationResolvedToAttackingSide");
                hadDeclaredOpportunity = OpenDeclaredAttackCoverageWindow(
                    receivingTeam,
                    actualPosition,
                    actualVelocity,
                    out openedReceipt);
            }

            if (openedReceipt != null)
            {
                RecordDefenseAttempt(
                    "PostBlockContinuationResolved",
                    openedReceipt,
                    receivingTeam,
                    _postAttackContinuationState.ToString());
            }
            else if (!hadDeclaredOpportunity)
            {
                Debug.Log(
                    $"[{_configuration.LogTag}] post-block continuation " +
                    $"team={receivingTeam} state={_postAttackContinuationState} " +
                    "has no declared receive opportunity");
            }
        }

        private void HandleNetPlaneCrossing(NetPlaneCrossingEvent crossing)
        {
            if (!_rallyActive || _restartScheduled || HasResult ||
                _touchState == null || !_touchState.LastPhysicalTouch.HasValue)
            {
                return;
            }

            var last = _touchState.LastPhysicalTouch.Value;
            var receivingTeam = _ball.State.Velocity.Z >= 0f ? TeamId.Orange : TeamId.Blue;
            NotifyReplay(
                ReplayNetCrossed,
                new ReplaySimpleEvent("NetCrossing", _ball.SimulationTime, receivingTeam, StableId(last)));
            var outcome = MatchRallyReferee.ResolveNetCrossing(
                ToSide(last.Team),
                crossing.Point,
                CourtBuilder.HalfWidth,
                CourtBuilder.NetHeight);
            if (outcome.HasValue)
            {
                AttributeAttackFault(last, outcome.Value);
                crossing.ConsumeRemainingStep();
                ResolveRally(outcome.Value, null, last, outcome.Value.Reason);
                if (_lastTouchWasBackSetAttack && outcome.Value.IsFault)
                {
                    BackSetAttackFaults++;
                }
                return;
            }

            if (GateIAuthorityEnabled && _awaitingPostBlockCrossing &&
                _postBlockerTeam.HasValue &&
                _formalAuthority?.AttackCoordinator != null)
            {
                ResolvePostBlockContinuation(
                    receivingTeam,
                    crossing.Point,
                    _ball.State.Velocity);
                return;
            }

            if (_touchState.ContactWindow != null &&
                _touchState.ContactWindow.Action == TechniqueAction.Block)
            {
                // Once the ball has crossed the net without a swept block hit,
                // no block surface can legally claim it later in this flight.
                // Hand off to the committed receiving defense immediately.
                DisablePhysicalBlockWindows();
                _postAttackContinuationState =
                    PostAttackContinuationStateV4.DefendingSideFloorDefense;
                if (GateIAuthorityEnabled)
                {
                    OpenPendingGateIReceiveWindow(
                        receivingTeam,
                        crossing.Point,
                        _ball.State.Velocity,
                        out _);
                }
                else
                {
                    BeginPossession(receivingTeam, ReceiveLeadTime());
                }
                return;
            }

            if (_postAttackContinuationState ==
                PostAttackContinuationStateV4.AwaitingAttackCrossing)
            {
                _postAttackContinuationState =
                    PostAttackContinuationStateV4.DefendingSideFloorDefense;
            }

            if (GateIAuthorityEnabled)
            {
                OpenPendingGateIReceiveWindow(
                    receivingTeam,
                    crossing.Point,
                    _ball.State.Velocity,
                    out _);
            }
            else
            {
                BeginPossession(receivingTeam, ReceiveLeadTime());
            }
        }

        private void AttributeAttackFault(PlayerId lastTouch, RallyOutcome outcome)
        {
            if (!outcome.IsFault || !_lastSetAttackActor.HasValue ||
                !_lastSetAttackActor.Value.Equals(lastTouch) ||
                !_lastSetQualityAssessment.HasValue)
            {
                return;
            }

            _lastSetReplanOutcome = AttackOutcome.Out;
            _lastAttackResponsibility = SetQualityAssessment.PrimaryResponsibility(
                _lastSetQualityAssessment.Value.Grade,
                AttackOutcome.Out);
        }

        private void ScheduleRestart(string reason)
        {
            if (_restartScheduled)
            {
                return;
            }

            MissedRallies++;
            if (_activeSetChain && _lastSetWasSetter &&
                _lastSetQualityAssessment?.Grade == SetQualityGrade.A &&
                _touchState?.ContactWindow?.Action == TechniqueAction.Attack)
            {
                AGradeNoContactErrors++;
                _lastSetReplanOutcome = AttackOutcome.Out;
                _lastAttackResponsibility = SetQualityAssessment.PrimaryResponsibility(
                    _lastSetQualityAssessment.Value.Grade,
                    AttackOutcome.Out);
                var scheduledActor = _scheduledPrimaryActor.HasValue
                    ? _players[_scheduledPrimaryActor.Value]
                    : null;
                var actorLabel = _scheduledPrimaryActor.HasValue
                    ? $"{_scheduledPrimaryActor.Value.Team}/{_scheduledPrimaryActor.Value.Role}"
                    : "none";
                LastAGradeNoContactDiagnostic =
                    $"actor={actorLabel}; movementShortfall=" +
                    $"{scheduledActor?.MovementShortfall ?? -1f:0.000}; correction=" +
                    $"{scheduledActor?.MaximumAppliedContactCorrection ?? -1f:0.000}; " +
                    $"surfacePlanError={scheduledActor?.MinimumActiveSurfacePlanError ?? -1f:0.000}; " +
                    $"root={scheduledActor?.transform.position}; surface=" +
                    $"{FormatDiagnosticVector(scheduledActor?.LastScheduledSurfaceCenter)}; normal=" +
                    $"{FormatDiagnosticVector(scheduledActor?.LastScheduledSurfaceNormal)}; replannedContact=" +
                    $"{FormatDiagnosticVector(_lastReplannedAttackContactCenter)}; " +
                    $"ball=({_ball.State.Position.X:0.000},{_ball.State.Position.Y:0.000}," +
                    $"{_ball.State.Position.Z:0.000}); expected={_expectedContactTime:0.000}; " +
                    $"now={_ball.SimulationTime:0.000}";
                LastAGradeNoContactDiagnostic +=
                    $"; setQuality={_lastSetQualityAssessment.Value.Reason}";
            }

            var loser = _scheduledPrimaryActor?.Team ?? _touchState.PossessionTeam;
            var outcome = new RallyOutcome(Opponent(ToSide(loser)), true, reason);
            ResolveRally(outcome, null, _scheduledPrimaryActor, reason);
        }

        private static string FormatDiagnosticVector(SimVector3? value)
        {
            return value.HasValue
                ? $"({value.Value.X:0.000},{value.Value.Y:0.000},{value.Value.Z:0.000})"
                : "none";
        }

        private void ResolveRally(
            RallyOutcome outcome,
            PlayerId? scorer,
            PlayerId? errorPlayer,
            string reason)
        {
            if (_restartScheduled || HasResult)
            {
                return;
            }

            RecordSetChainSuccess(scorer);
            var resolvedErrorPlayer = errorPlayer;
            if (_activeSetChain && _lastSetAttackActor.HasValue &&
                outcome.Winner != ToSide(_lastSetAttackActor.Value.Team))
            {
                resolvedErrorPlayer = SetChainAttribution.ResponsiblePlayer(
                    _lastAttackResponsibility,
                    _lastSetSetterActor,
                    _lastSetAttackActor,
                    errorPlayer);
            }
            _activeSetChain = false;

            _restartScheduled = true;
            _rallyActive = false;
            _aiDecisionRequestVersion++;
            _aiDecisionTimeController?.CancelPending();
            _contactDeadlineActive = false;
            _pendingCrossingTeam = null;
            _scheduledDecision = null;
            _plannedAttackDecision = null;
            _plannedAttackTrajectoryArtifactV4 = null;
            _scheduledPrimaryActor = null;
            _scheduledBlockers.Clear();
            _scheduledBlockPrimary = null;
            _awaitingPostBlockCrossing = false;
            _postBlockerTeam = null;
            DisableEmergencyReceiveWindows(TeamId.Blue);
            DisableEmergencyReceiveWindows(TeamId.Orange);
            foreach (var player in _players.Values)
            {
                player.CancelScheduledContact();
            }

            _set.ResolveRally(
                outcome.Winner,
                scorer.HasValue ? StableId(scorer.Value) : null,
                resolvedErrorPlayer.HasValue ? StableId(resolvedErrorPlayer.Value) : null);
            RenderScore();
            _status = $"{reason}  {_set.HomeScore}:{_set.AwayScore}";
            NotifyReplay(
                ReplayRallyResolved,
                new ReplayRallyResolvedEvent(
                    _ball.SimulationTime,
                    FromSide(outcome.Winner),
                    scorer.HasValue ? StableId(scorer.Value) : null,
                    resolvedErrorPlayer.HasValue ? StableId(resolvedErrorPlayer.Value) : null,
                    reason));
            Debug.Log(
                $"[{_configuration.LogTag}] rally={reason} winner={outcome.Winner} " +
                $"score={_set.HomeScore}:{_set.AwayScore}");
            if (_set.IsComplete)
            {
                if (_formalSet != null)
                {
                    Result = _formalSet.CreateResult(
                        SuccessfulContacts,
                        V3RuleTransitions);
                }
                else
                {
                    CompletePrototypeMatch();
                }
                _ball.Stop();
                _status = $"RESULT READY  {_set.HomeScore}:{_set.AwayScore}";
                RenderScore();
                Debug.Log(
                    $"[{_configuration.LogTag}] RESULT score={_set.HomeScore}:{_set.AwayScore} " +
                    $"contacts={SuccessfulContacts} blocks={PhysicalBlockContacts} " +
                    $"multiBlocks={ScheduledMultiBlockUnits} maxBlockers={MaximumScheduledBlockers} " +
                    $"routes={GeometricLineRoutes}/{GeometricCrossCourtRoutes}/" +
                    $"{GeometricOverHandRoutes}/{GeometricEdgeLeftRoutes}/" +
                    $"{GeometricEdgeRightRoutes} nonSetterSets={NonSetterSetContacts} " +
                    $"defenderAttacks={DefenderAttackContacts}");
                return;
            }

            _tacticRevision++;
            ApplyTactics(InitialTactics(), false);
            StartCoroutine(StartInitialLoop(0.55f));
        }

        private void RecordSetChainSuccess(PlayerId? scorer)
        {
            if (_lastSetChainSuccessRecorded || !scorer.HasValue ||
                !_lastSetAttackActor.HasValue ||
                !_lastSetAttackActor.Value.Equals(scorer.Value) ||
                !_lastSetQualityAssessment.HasValue)
            {
                return;
            }

            _lastSetChainSuccessRecorded = true;
            if (_lastSetQualityAssessment.Value.Grade == SetQualityGrade.A)
            {
                GradeASetAttackSuccesses++;
            }

            if (_lastAttackContactOutcome == AttackContactOutcome.AdjustedAttack)
            {
                AdjustedAttackSuccesses++;
            }
        }

        private SimVector3 OutgoingTargetFor(TeamRallyDecision decision)
        {
            if (decision.Action == TechniqueAction.Receive)
            {
                return NextContactCenter(decision.Actor.Team, TechniqueAction.Set);
            }

            if (decision.Action == TechniqueAction.Set)
            {
                if (_plannedAttackDecision != null && _plannedAttackDecision.HasDecision)
                {
                    if (_plannedAttackDecision.AttackContactPlan.HasValue)
                    {
                        return _plannedAttackDecision.AttackContactPlan.Value.ContactCenter;
                    }

                    var plannedAttacker = _players[_plannedAttackDecision.Actor];
                    return ContactCenter(
                        plannedAttacker.PreviewContactFramesAt(
                            TechniqueAction.Attack,
                            ToUnity(_plannedAttackDecision.MovementTarget)),
                        TechniqueAction.Attack);
                }

                return NextContactCenter(decision.Actor.Team, TechniqueAction.Attack);
            }

            var landing = decision.BallTarget;
            return new SimVector3(
                landing.X,
                GroundHeight + SimulatedBall.DefaultRadius,
                landing.Z);
        }

        private IReadOnlyList<ContactCapsuleFrame> PredictedBlockArmFrames(
            TeamId attackingTeam,
            SimVector3 attackContactCenter,
            float predictionTime)
        {
            var defendingTeam = Opponent(attackingTeam);
            var intercept = new SimVector3(
                attackContactCenter.X,
                Mathf.Max(CourtBuilder.NetHeight + 0.35f, attackContactCenter.Y - 0.2f),
                0f);
            var rootTarget = BlockRootTarget(defendingTeam, intercept);
            var unit = SelectBlockUnit(
                defendingTeam,
                ToSimulation(rootTarget),
                Mathf.Max(0f, predictionTime - _ball.SimulationTime));
            if (unit.Blockers.Count == 0)
            {
                return Array.Empty<ContactCapsuleFrame>();
            }

            var frames = new List<ContactCapsuleFrame>(unit.Blockers.Count * 6);
            var primary = unit.Blockers[0];
            foreach (var blocker in unit.Blockers)
            {
                var target = UnitBlockRootTarget(
                    defendingTeam,
                    intercept,
                    blocker,
                    primary);
                target = _players[blocker.Id].ResolveBlockRootTarget(intercept, target);
                frames.AddRange(_players[blocker.Id].PreviewBlockArmFrames(
                    predictionTime,
                    target));
            }

            return frames;
        }

        private BlockUnitPlan SelectBlockUnit(
            TeamId defendingTeam,
            SimVector3 blockTarget,
            float availableSeconds)
        {
            var candidates = new List<BlockCandidateSnapshot>();
            foreach (var pair in _players)
            {
                if (pair.Key.Team != defendingTeam)
                {
                    continue;
                }

                var player = pair.Value;
                candidates.Add(new BlockCandidateSnapshot(
                    pair.Key,
                    ToSimulation(player.transform.position),
                    BaseMovementSpeed * (0.65f + (player.Ability.Mobility * 0.5f)),
                    player.Ability.Jump,
                    _configuration.RosterSize != 6 || IsFrontRow(pair.Key)));
            }

            return BlockUnitPlanner.Select(
                candidates,
                blockTarget,
                availableSeconds,
                requireFrontRow: _configuration.RosterSize == 6);
        }

        private void RecordGeometricAttackRoute(GeometricAttackRoute route)
        {
            GeometricAttackRouteSelections++;
            switch (route)
            {
                case GeometricAttackRoute.Line:
                    GeometricLineRoutes++;
                    break;
                case GeometricAttackRoute.CrossCourt:
                    GeometricCrossCourtRoutes++;
                    break;
                case GeometricAttackRoute.OverHand:
                    GeometricOverHandRoutes++;
                    break;
                case GeometricAttackRoute.EdgeLeft:
                    GeometricEdgeLeftRoutes++;
                    break;
                case GeometricAttackRoute.EdgeRight:
                    GeometricEdgeRightRoutes++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }
        }

        private SimVector3 NextContactCenter(TeamId team, TechniqueAction action)
        {
            var tactic = TacticFor(team);
            SimVector3 point = action == TechniqueAction.Set
                ? SetterOrganizationZone.DefaultWorldTarget(team)
                : new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            var player = FindPlayer(
                team,
                role => action == TechniqueAction.Set
                    ? role == PlayerRole.Setter
                    : PhysicalMatchConfiguration.IsAttackSpecialist(role));
            return ContactCenter(
                player.PreviewContactFramesAt(action, ToUnity(point)),
                action);
        }

        private float OutgoingFlightSecondsFor(TeamId team, TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => ReceiveFlightSeconds,
                TechniqueAction.Attack => TacticFor(team).AttackFlightSeconds,
                _ => ReceiveFlightSeconds
            };
        }

        private float ReceiveLeadTime()
        {
            var prediction = TrajectoryPredictor.Predict(
                _ball.State,
                SimulationParameters,
                SimulatedBall.DefaultFixedStep,
                1.5f,
                220,
                GroundHeight);
            return prediction.GroundLanding.HasValue
                ? Mathf.Clamp(prediction.GroundLanding.Value.TimeSeconds - 0.12f, 0.12f, 0.75f)
                : 0.45f;
        }

        private PlayerId SelectBlocker(TeamId team, SimVector3 intercept)
        {
            var target = BlockRootTarget(team, intercept);
            var best = default(PlayerId);
            var found = false;
            var bestScore = float.PositiveInfinity;
            foreach (var pair in _players)
            {
                var id = pair.Key;
                if (id.Team != team || (_configuration.RosterSize == 6 && !IsFrontRow(id)))
                {
                    continue;
                }

                var player = pair.Value;
                var groundDistance = Vector2.Distance(
                    new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(target.x, target.z));
                var score = groundDistance - (player.Ability.Jump * 0.35f);
                if (score < bestScore)
                {
                    best = id;
                    bestScore = score;
                    found = true;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("No eligible front-row blocker is available.");
            }

            return best;
        }

        private PrototypePlayerAgent FindPlayer(TeamId team, Predicate<PlayerRole> preferredRole)
        {
            PrototypePlayerAgent fallback = null;
            foreach (var pair in _players)
            {
                if (pair.Key.Team != team)
                {
                    continue;
                }

                fallback ??= pair.Value;
                if (preferredRole(pair.Key.Role))
                {
                    return pair.Value;
                }
            }

            return fallback ?? throw new InvalidOperationException("The requested team has no active player.");
        }

        private PrototypePlayerAgent FindPlayer(StablePlayerId stablePlayerId)
        {
            foreach (var player in _players.Values)
            {
                if (player.StableId.Equals(stablePlayerId))
                {
                    return player;
                }
            }

            throw new InvalidOperationException("The rotation server is not bound to an active player.");
        }

        private static Vector3 BlockRootTarget(TeamId team, SimVector3 intercept)
        {
            var worldDepth = team == TeamId.Blue
                ? -PrototypePlayerAgent.NetClearance
                : PrototypePlayerAgent.NetClearance;
            return new Vector3(Mathf.Clamp(intercept.X, -4.1f, 4.1f), 0f, worldDepth);
        }

        private static Vector3 UnitBlockRootTarget(
            TeamId team,
            SimVector3 intercept,
            BlockCandidateSnapshot blocker,
            BlockCandidateSnapshot primary)
        {
            var target = BlockRootTarget(team, intercept);
            var laneOffset = blocker.Id.Equals(primary.Id)
                ? 0f
                : blocker.Position.X < primary.Position.X ? -0.42f : 0.42f;
            target.x = Mathf.Clamp(target.x + laneOffset, -4.1f, 4.1f);
            return target;
        }

        private List<PlayerId> OrderedScheduledBlockers()
        {
            var blockers = new List<PlayerId>(_scheduledBlockers);
            blockers.Sort((left, right) =>
            {
                var slot = left.RosterSlot.CompareTo(right.RosterSlot);
                return slot != 0
                    ? slot
                    : ((int)left.Role).CompareTo((int)right.Role);
            });
            return blockers;
        }

        private BlockCandidateSnapshot BlockSnapshot(PlayerId blocker)
        {
            var player = _players[blocker];
            return new BlockCandidateSnapshot(
                blocker,
                ToSimulation(player.transform.position),
                BaseMovementSpeed * (0.65f + (player.Ability.Mobility * 0.5f)),
                player.Ability.Jump,
                _configuration.RosterSize != 6 || IsFrontRow(blocker));
        }

        private static Vector3 CoverageTarget(TeamId team, SimVector3 intercept)
        {
            var worldDepth = team == TeamId.Blue ? -3.6f : 3.6f;
            return new Vector3(Mathf.Clamp(-intercept.X * 0.35f, -3f, 3f), 0f, worldDepth);
        }

        private static SimVector3 BlockReboundVelocity(TeamId attackingTeam)
        {
            return attackingTeam == TeamId.Blue
                ? new SimVector3(0f, 5.5f, -6.5f)
                : new SimVector3(0f, 5.5f, 6.5f);
        }

        private void DisablePhysicalBlockWindows()
        {
            foreach (var player in _players.Values)
            {
                player.DisableBlockContactWindow();
            }
            _scheduledBlockers.Clear();
            _scheduledBlockPrimary = null;
            _committedGateIBlockTime = -1f;
        }

        private void DisableEmergencyReceiveWindows(TeamId team)
        {
            foreach (var pair in _players)
            {
                if (pair.Key.Team == team)
                {
                    pair.Value.DisableEmergencyReceiveWindow();
                }
            }
        }

        private Vector3 TacticalRootTarget(PlayerId player)
        {
            if (_configuration.RosterSize == 6)
            {
                var rotationPosition = _set.RotationPositionFor(StableId(player));
                return _configuration.PositionFor(ToSide(player.Team), rotationPosition);
            }

            var tactic = TacticFor(player.Team);
            var positions = new[]
            {
                ToUnity(tactic.DefenderPosition),
                ToUnity(tactic.SetterPosition),
                ToUnity(tactic.AttackerPosition)
            };
            var offset = _set == null ? 0 : _set.RotationOffsetFor(ToSide(player.Team));
            return positions[(RoleIndex(player.Role) + offset) % positions.Length];
        }

        private TeamRallyTactic TacticFor(TeamId team)
        {
            return team == TeamId.Blue ? _currentTactics.Blue : _currentTactics.Orange;
        }

        private SimVector3 PredictBallCenter(float flightSeconds)
        {
            return PredictBallState(flightSeconds).Position;
        }

        private BallState PredictBallState(float flightSeconds)
        {
            var prediction = _ball.State.Clone();
            var steps = Mathf.Max(1, Mathf.RoundToInt(flightSeconds / SimulatedBall.DefaultFixedStep));
            for (var step = 0; step < steps; step++)
            {
                BallIntegrator.Step(prediction, SimulatedBall.DefaultFixedStep, SimulationParameters);
            }
            return prediction;
        }

        private SimVector3 PredictGate5BallCenterV4(
            TeamId requestingTeam,
            RallyDecisionStage stage,
            float flightSeconds)
        {
            if (_matchContext == null)
            {
                return PredictBallCenter(flightSeconds);
            }

            if (_trajectoryPredictionProviderV4 == null)
            {
                throw new InvalidOperationException(
                    "The per-rally V4 trajectory provider is not initialized.");
            }

            var stateVersion =
                (long)(uint)BitConverter.ToInt32(
                    BitConverter.GetBytes(_ball.SimulationTime),
                    0);
            var samplingKey =
                "gate-5:" + ((int)stage).ToString(
                    System.Globalization.CultureInfo.InvariantCulture) + ":" +
                stateVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) + ":" +
                BitConverter.ToInt32(
                    BitConverter.GetBytes(flightSeconds),
                    0).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            var envelopeIdentity = BuildGate5EnvelopeIdentityV4(
                stage,
                flightSeconds);
            var request = new BallTrajectoryPredictionRequestV4(
                requestingTeam == TeamId.Blue
                    ? TeamSide.Home
                    : TeamSide.Away,
                stateVersion,
                _ball.State,
                SimulationParameters,
                _matchContext.PhysicsConfigurationHash,
                samplingKey,
                _matchContext.TrajectoryPredictionProviderConfiguration
                    .PredictorVersion,
                _matchContext.TrajectoryPredictionProviderConfiguration
                    .PredictorConfigurationHash,
                envelopeIdentity,
                ExecutionDegradationStepV4.FullSampling);
            _lastTrajectoryPredictionArtifactV4 =
                PredictSharedGate5TrajectoryV4(
                    _trajectoryPredictionProviderV4,
                    request,
                    ExecutionEnvelopePolicyV4.Default);
            return ClosestTrajectoryPosition(
                _lastTrajectoryPredictionArtifactV4.PredictionSnapshot,
                flightSeconds);
        }

        private bool GateJEnabled =>
            GateIAuthorityEnabled &&
            _v3RulesAdapter != null &&
            _v3RulesAdapter.Mode == V3RulesMode.Authority &&
            _players.Count == 12;

        private PerceptionReceiptV3 CreateGateJPerceptionReceipt(
            TeamId observingTeam,
            long revision,
            long sourceSequence,
            string artifactIdentity,
            PublicAttackThreatV3 publicThreat,
            IReadOnlyList<StablePlayerId> legalSupport,
            StablePlayerId conservativeSupport,
            bool defenseAwareness)
        {
            if (!GateJEnabled)
                throw new InvalidOperationException(
                    "Gate J perception is restricted to formal Authority.");
            var observers = _players
                .Where(pair => pair.Key.Team == observingTeam)
                .OrderByDescending(pair => defenseAwareness
                    ? pair.Value.Ability.Attributes.Defense.Awareness
                    : pair.Value.Ability.Attributes.Receive.Awareness)
                .ThenBy(pair => pair.Value.StableId.Value,
                    StringComparer.Ordinal)
                .ToArray();
            var observer = observers[0].Value;
            var awareness = defenseAwareness
                ? observer.Ability.Attributes.Defense.Awareness
                : observer.Ability.Attributes.Receive.Awareness;
            var allowed = legalSupport
                .Distinct()
                .OrderBy(value => value.Value, StringComparer.Ordinal)
                .ToArray();
            if (allowed.Length == 0)
                throw new InvalidOperationException(
                    "Gate J requires a declared legal support candidate.");
            if (!allowed.Contains(conservativeSupport))
                throw new InvalidOperationException(
                    "Gate J conservative support must be a legal candidate.");
            var supports = allowed.Select(playerId =>
            {
                var agent = _players.Values.Single(value =>
                    value.StableId.Equals(playerId));
                var distance = Vector3.Distance(
                    agent.transform.position, _ball.transform.position);
                return new PerceivedSupportCandidateV3(
                    playerId, awareness,
                    (BaseMovementSpeed * .5f) - distance,
                    playerId.Equals(conservativeSupport));
            }).ToArray();
            var threats = publicThreat == null
                ? Array.Empty<PerceivedThreatEntryV3>()
                : publicThreat.Entries.Select((entry, index) =>
                    new PerceivedThreatEntryV3(
                        publicThreat.ThreatIdentity + ":" + index,
                        entry.Zone, entry.Probability, entry.ArrivalTime))
                    .ToArray();
            var request = new CourtPerceptionRequestV3(
                _matchContext.Seed.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                revision, sourceSequence, ToSide(observingTeam),
                observer.StableId, awareness, artifactIdentity,
                _ball.State.Position, threats, supports, conservativeSupport,
                _ball.SimulationTime);
            var result = new CourtPerceptionAdapterV3(
                GateJPerceptionConfiguration).Observe(request);
            return new PerceptionReceiptV3(
                GateJPerceptionConfiguration.Identity,
                result.View, result.SupportDecision, result.ObservedBall,
                result.RecognitionDelaySeconds);
        }

        private static string BuildGate5EnvelopeIdentityV4(
            RallyDecisionStage stage,
            float flightSeconds)
        {
            var canonical =
                "volleyball.gate-5-sampling-envelope.v4\nstage=" +
                ((int)stage).ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                "\nflightSeconds=" +
                BitConverter.ToInt32(
                    BitConverter.GetBytes(flightSeconds),
                    0).ToString(
                    "x8",
                    System.Globalization.CultureInfo.InvariantCulture) +
                "\nexecutionEnvelopePolicy=" +
                Convert.ToBase64String(
                    ExecutionEnvelopePolicyV4.Default.ToCanonicalBytes()) +
                "\n";
            using var sha256 =
                System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(canonical));
            var output = new System.Text.StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++)
            {
                output.Append(
                    hash[index].ToString(
                        "x2",
                        System.Globalization.CultureInfo.InvariantCulture));
            }

            return output.ToString();
        }

        private static SimVector3 ClosestTrajectoryPosition(
            TrajectoryPrediction prediction,
            float flightSeconds)
        {
            if (prediction == null || prediction.Samples.Count == 0)
            {
                throw new InvalidOperationException(
                    "V4 trajectory prediction returned no samples.");
            }

            var best = prediction.Samples[0];
            var bestTimeError = Mathf.Abs(best.TimeSeconds - flightSeconds);
            for (var index = 1; index < prediction.Samples.Count; index++)
            {
                var candidate = prediction.Samples[index];
                var timeError = Mathf.Abs(
                    candidate.TimeSeconds - flightSeconds);
                if (timeError < bestTimeError)
                {
                    best = candidate;
                    bestTimeError = timeError;
                }
            }

            return best.Position;
        }

        private static SimVector3 ContactCenter(
            IReadOnlyList<ContactSurfaceFrame> frames,
            TechniqueAction action)
        {
            if (action == TechniqueAction.Set)
            {
                var palm = frames[0];
                return palm.Origin + (palm.Normal * SimulatedBall.DefaultRadius);
            }

            var origin = SimVector3.Zero;
            var normal = SimVector3.Zero;
            foreach (var frame in frames)
            {
                origin += frame.Origin;
                normal += frame.Normal;
            }

            return (origin / frames.Count) +
                   ((normal / frames.Count).Normalized * SimulatedBall.DefaultRadius);
        }

        private void RenderScore()
        {
            _scoreDisplay.RenderSet(
                _set.HomeScore,
                _set.AwayScore,
                _set.ServingSide,
                _set.IsComplete);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 680f, 138f), string.Empty);
            var heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(34f, 28f, 640f, 30f), _configuration.DisplayName, heading);
            GUI.Label(new Rect(34f, 60f, 640f, 28f), _status, heading);
            var possession = _touchState == null
                ? "none"
                : $"{_touchState.PossessionTeam}/{_touchState.CountedTeamTouches}";
            GUI.Label(
                new Rect(34f, 91f, 640f, 56f),
                $"possession {possession} contacts {SuccessfulContacts} " +
                $"blocks {PhysicalBlockContacts} non-setter sets {NonSetterSetContacts} " +
                $"defender attacks {DefenderAttackContacts}\n" +
                $"server {_set?.ServerFor(_set.ServingSide).Value ?? "none"}  " +
                $"rotation H{_set?.RotationOffsetFor(TeamSide.Home) ?? 0} " +
                $"A{_set?.RotationOffsetFor(TeamSide.Away) ?? 0}",
                GUI.skin.label);
        }

        private int NextContactGroup()
        {
            return _contactGroupSequence++;
        }

        private static void NotifyReplay<T>(Action<T> handler, T payload)
        {
            handler?.Invoke(payload);
        }

        private static int StablePlayerNumber(PlayerId player)
        {
            return player.RosterSlot == (int)player.Role
                ? ((int)player.Team * 10) + (int)player.Role
                : ((int)player.Team * 100) + player.RosterSlot;
        }

        private static TeamSide ToSide(TeamId team)
        {
            return team == TeamId.Blue ? TeamSide.Home : TeamSide.Away;
        }

        private static TeamId FromSide(TeamSide side)
        {
            return side == TeamSide.Home ? TeamId.Blue : TeamId.Orange;
        }

        private static TeamId Opponent(TeamId team)
        {
            return team == TeamId.Blue ? TeamId.Orange : TeamId.Blue;
        }

        private static TeamSide Opponent(TeamSide side)
        {
            return side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        }

        private StablePlayerId StableId(PlayerId player)
        {
            if (!_players.TryGetValue(player, out var agent))
            {
                throw new ArgumentException("The player is not registered for this match.", nameof(player));
            }

            return agent.StableId;
        }

        private static int RoleIndex(PlayerRole role)
        {
            return role switch
            {
                PlayerRole.Defender => 0,
                PlayerRole.Setter => 1,
                PlayerRole.Attacker => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
        }

        private static Vector3 ToUnity(SimVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static Vector3 ToUnity(CourtPoint value)
        {
            return new Vector3(value.X, 0f, value.Z);
        }

        private static SimVector3 ToSimulation(Vector3 value)
        {
            return new SimVector3(value.x, value.y, value.z);
        }

        private sealed class NullReceiveOrganizationAuthorityCommandSink :
            IReceiveOrganizationAuthorityCommandSink
        {
            public static NullReceiveOrganizationAuthorityCommandSink Instance {
                get;
            } = new NullReceiveOrganizationAuthorityCommandSink();

            public void Publish(ReceiveOrganizationCommandBatch batch)
            {
            }
        }

        private sealed class DirectorAttackDefenseCommandSink : IAttackDefenseAuthorityCommandSink
        {
            private readonly PhysicalMatchRallyDirector _director;
            public DirectorAttackDefenseCommandSink(PhysicalMatchRallyDirector director)
            { _director = director ?? throw new ArgumentNullException(nameof(director)); }
            public void Publish(AttackDefenseCommandBatch batch) => _director.PublishGateIBatch(batch);
        }
    }
}
