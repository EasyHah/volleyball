using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using MatchContextV4 = Volleyball.Shared.Contracts.MatchContextV4;
using MatchResultV4 = Volleyball.Shared.Contracts.MatchResultV4;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation
{
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
            ReplaySetChainEvent setChain = null)
            : base(kind, simulationTimeSeconds, team, playerId)
        {
            Action = action;
            SetChain = setChain;
        }

        public TechniqueAction Action { get; }
        public ReplaySetChainEvent SetChain { get; }
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

        private static readonly BallSimulationParameters SimulationParameters =
            new BallSimulationParameters(-9.8f, 0.9995f);

        private readonly Dictionary<PlayerId, PrototypePlayerAgent> _players =
            new Dictionary<PlayerId, PrototypePlayerAgent>();
        private readonly PhysicalRallyTacticPlanner _tacticPlanner =
            new PhysicalRallyTacticPlanner();
        private readonly TeamRallyDecisionPlanner _decisionPlanner =
            new TeamRallyDecisionPlanner(7351);

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
        private PlayerId? _scheduledBlockPrimary;
        private bool _awaitingPostBlockCrossing;
        private TeamId? _postBlockerTeam;
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
        private int _decisionIndex;
        private int _aiDecisionRequestVersion;
        private int _aiRequestSequence;
        private int _contactGroupSequence = 3000;
        private FullRallyV3RulesRuntimeAdapter _v3RulesAdapter;
        private MatchContextV4 _matchContext;
        private MatchSet _formalSet;
        private PendingV3AuthorityContact _pendingV3AuthorityContact;
        private StablePlayerId? _lastAcceptedV3Actor;
        private RallyContactClassificationV3? _lastAcceptedV3Classification;
        private RallyTacticalWeights _activeTacticalWeights;
        private PhysicalMatchConfiguration _configuration;
        private string _status = "Preparing dynamic physical 3v3";

        public int CompletedCycles { get; private set; }

        public int SuccessfulContacts { get; private set; }

        public V3RulesMode V3RulesMode { get; private set; }

        public int V3RuleTransitions { get; private set; }

        public int V3RuleParityMatches { get; private set; }

        public int V3RuleIntentionalCorrections { get; private set; }

        public int V3RuleUnexpectedMismatches { get; private set; }

        public string LastV3RuleDiagnostic { get; private set; } = string.Empty;

        public int MissedRallies { get; private set; }

        public int GroundResolvedRallies { get; private set; }

        public int TacticRevision => _tacticRevision;

        public int ExecutionErrorApplications { get; private set; }

        public int MovementAssignments { get; private set; }

        public int BlockSupportAssignments { get; private set; }

        public int CoverageSupportAssignments { get; private set; }

        public int BlockSupportActivations { get; private set; }

        public int BackSetAttackContacts { get; private set; }

        public int BackSetAttackFaults { get; private set; }

        public int EmergencyReceiveWindowAssignments { get; private set; }

        public int EmergencyReceiveContacts { get; private set; }

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
                "Only an explicitly isolated prototype director may complete a legacy match.");
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

        public event Action<ReplaySimpleEvent> ReplayServeStarted;

        public event Action<ReplaySimpleEvent> ReplayNetCrossed;

        public event Action<ReplaySimpleEvent> ReplayGroundContact;

        public event Action<ReplayRallyResolvedEvent> ReplayRallyResolved;

        public int HomeRotationOffset => _set == null ? 0 : _set.RotationOffsetFor(TeamSide.Home);

        public int AwayRotationOffset => _set == null ? 0 : _set.RotationOffsetFor(TeamSide.Away);

        public MatchContextV4 MatchContext => _matchContext;

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
            TeamSide firstServingSide = TeamSide.Home)
        {
            var matchContext = context ?? throw new ArgumentNullException(nameof(context));
            if (matchContext.RulesVersion != 3)
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
                        _configuration.SetRules);
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
            ApplyTactics(_tacticPlanner.Create(), true);
            RenderScore();

            _ball.ContactCandidateResolver = ResolveCandidate;
            _ball.PlayerContactRejected += HandleRejectedPlayerContact;
            _ball.PlayerContact += HandlePlayerContact;
            _ball.EnvironmentContact += HandleEnvironmentContact;
            _ball.NetPlaneCrossed += HandleNetPlaneCrossing;
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

            if (_matchContext.RulesVersion != 3)
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
            ResetV3Diagnostics();
        }

        private void ResetV3Diagnostics()
        {
            V3RuleTransitions = 0;
            V3RuleParityMatches = 0;
            V3RuleIntentionalCorrections = 0;
            V3RuleUnexpectedMismatches = 0;
            LastV3RuleDiagnostic = string.Empty;
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
            if (!_rallyActive || _restartScheduled || HasResult)
            {
                return;
            }

            if (_pendingCrossingTeam.HasValue &&
                _touchState?.ContactWindow != null &&
                _touchState.ContactWindow.Action == TechniqueAction.Block &&
                _ball.SimulationTime > _touchState.ContactWindow.EndSimulationTime)
            {
                var receivingTeam = _pendingCrossingTeam.Value;
                _pendingCrossingTeam = null;
                DisablePhysicalBlockWindows();
                BeginPossession(receivingTeam, ReceiveLeadTime());
                return;
            }

            if (_contactDeadlineActive && _ball.SimulationTime > _expectedContactTime + 0.35f)
            {
                ScheduleRestart("miss (contact timeout)");
            }

            else if (_configuration.RosterSize == 6)
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

        private IEnumerator StartInitialLoop(float delay)
        {
            _rallyActive = false;
            yield return new WaitForSeconds(delay);

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
            _scheduledPrimaryActor = null;
            _scheduledBlockers.Clear();
            _scheduledBlockPrimary = null;
            _awaitingPostBlockCrossing = false;
            _postBlockerTeam = null;
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
            var decision = PlanDecision(team, RallyDecisionStage.Receive, availableSeconds);
            ScheduleDecision(decision, availableSeconds);
            if (_tacticRevision == 0 && SuccessfulContacts == 0)
            {
                _status = $"Serve to {team.ToString().ToUpperInvariant()} possession";
            }
        }

        private RallyTacticalWeights LocalTacticalWeights()
        {
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
                PredictBallCenter(Mathf.Max(0.10f, availableSeconds)));
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

            var input = new TeamRallyDecisionInput(
                team,
                TacticFor(team),
                players,
                predictedBallCenter,
                availableSeconds,
                BaseMovementSpeed,
                countedTouches,
                lastCountedActor,
                _tacticRevision,
                _decisionIndex++,
                stage,
                _activeTacticalWeights);
            var decision = _decisionPlanner.Plan(input);
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

        private void ScheduleDecision(TeamRallyDecision decision, float flightSeconds)
        {
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
                _plannedAttackDecision = PlanDecisionAt(
                    decision.Actor.Team,
                    RallyDecisionStage.Attack,
                    attackFlight,
                    decision.BallTarget,
                    _touchState.CountedTeamTouches + 1,
                    decision.Actor);
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
                    outgoingTarget = SelectGeometricSetTarget(
                        decision,
                        outgoingTarget,
                        _ball.SimulationTime + flightSeconds);
                    _scheduledGeometricSetTarget = outgoingTarget;
                    GeometricSetTargetSelections++;
                }
                catch (InvalidOperationException exception)
                {
                    Debug.Log(
                        $"[{_configuration.LogTag}] geometric-set-fallback " +
                        $"team={decision.Actor.Team} reason={exception.Message}");
                }

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

            _expectedContactTime = _ball.SimulationTime + flightSeconds;
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
                outgoing,
                execution,
                NextContactGroup(),
                authoritativeContactCenter,
                movementTarget: movementTarget,
                movementStartSimulationTime: _ball.SimulationTime,
                attackApproach: decision.AttackApproach,
                attackContactPlan: decision.AttackContactPlan,
                normalSetRoute: decision.Action == TechniqueAction.Set
                    ? TacticFor(decision.Actor.Team).SetRoute
                    : (SetRoute?)null);
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
                return ToBallContactResolution(_v3RulesAdapter.EvaluateContact(
                    StableId(candidate.Actor.Value),
                    ToSide(candidate.Actor.Value.Team),
                    ToV3Classification(candidate.Action),
                    hit.ContactGroupId));
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
            var transition = _v3RulesAdapter.CommitContact(
                stableActor,
                ToSide(actor.Team),
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
                legacyEvaluation,
                legacyOutcome,
                scenario,
                diagnosticExceptionType);
            return resolution;
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

            switch (contact.Candidate.Action)
            {
                case TechniqueAction.Receive:
                    if (_controlledHandlingActive)
                    {
                        _controlledHandlingActive = false;
                        _scheduledDecision = null;
                        _scheduledPrimaryActor = null;
                        CompletedCycles++;
                        break;
                    }

                    ScheduleDecision(
                        PlanDecision(actorId.Team, RallyDecisionStage.Organize, ReceiveFlightSeconds),
                        ReceiveFlightSeconds);
                    break;
                case TechniqueAction.Set:
                    _lastSetQualityAssessment = null;
                    _activeSetChain = false;
                    var setFlight = _scheduledSetFlightSeconds > 0f
                        ? _scheduledSetFlightSeconds
                        : SetFlightSolver.PreferredFlightSeconds(TacticFor(actorId.Team).SetRhythm);
                    var attackDecision = _plannedAttackDecision;
                    _plannedAttackDecision = null;
                    if (attackDecision == null || !attackDecision.HasDecision)
                    {
                        attackDecision = PlanDecision(
                            actorId.Team,
                            RallyDecisionStage.Attack,
                            setFlight);
                    }
                    ScheduleAttackFromActualSet(attackDecision, setFlight, actorId);
                    RecordSetCalibration(actorId, actor.CurrentSetStyle, contact.Hit.Centeredness);
                    break;
                case TechniqueAction.Attack:
                    CompletedCycles++;
                    _lastTouchWasBackSetAttack = TacticFor(actorId.Team).SetRoute == SetRoute.BackSet;
                    if (_lastTouchWasBackSetAttack)
                    {
                        BackSetAttackContacts++;
                    }
                    SchedulePhysicalBlock(actorId.Team, _scheduledDecision, contact);
                    break;
                case TechniqueAction.Block:
                    HandleAcceptedBlock(contact);
                    break;
            }

            ObserveAcceptedContactV3(
                actorId,
                contact.Candidate.Action,
                contact.Hit.ContactGroupId,
                authorityContact);
            NotifyReplay(
                ReplayContactAccepted,
                new ReplayContactEvent(
                    contact.Candidate.Action == TechniqueAction.Block ? "Block" : "Contact",
                    contact.ContactSimulationTime,
                    actorId.Team,
                    StableId(actorId),
                    contact.Candidate.Action,
                    _pendingReplaySetChain));
        }

        private void ObserveAcceptedContactV3(
            PlayerId actor,
            TechniqueAction action,
            int contactGroup,
            PendingV3AuthorityContact authorityContact)
        {
            if (_v3RulesAdapter == null)
            {
                return;
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
            PlayerId setterActor)
        {
            if (provisionalDecision == null || !provisionalDecision.HasDecision ||
                !provisionalDecision.AttackApproach.HasValue ||
                !provisionalDecision.AttackContactPlan.HasValue)
            {
                ScheduleDecision(provisionalDecision, plannedFlightSeconds);
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
                ScheduleDecision(replacement, Mathf.Max(0.1f, actualArrival.TimeSeconds));
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
            float contactCenteredness)
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

            if (style == SetTechniqueStyle.SideLeftTwoHand ||
                style == SetTechniqueStyle.SideRightTwoHand)
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
            actor.ScheduleControlledHandlingContact(
                _expectedContactTime,
                outgoing,
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
                _players[blocker.Id].ScheduleBlockContact(
                    blockTime,
                    target,
                    _ball.SimulationTime,
                    BlockReboundVelocity(attackingTeam),
                    NextContactGroup());
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

            if (TrySelectCoverPlayer(defendingTeam, _scheduledBlockers, intercept.Point, out var cover))
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

        private void HandleAcceptedBlock(PlayerBallContactEvent contact)
        {
            var blocker = contact.Candidate.Actor.Value;
            DisablePhysicalBlockWindows();
            _scheduledBlockers.Clear();
            _scheduledBlockPrimary = null;
            _scheduledPrimaryActor = null;
            _scheduledDecision = null;
            _plannedAttackDecision = null;
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
            PostBlockPossessionDeferrals++;
            PostBlockContinuations++;
            Debug.Log(
                $"[{_configuration.LogTag}] block-contact team={blocker.Team} actor={blocker.Role} " +
                $"awaiting-crossing speed={contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0} " +
                $"effect={BlockImpactEffects}");
        }

        private void HandleEnvironmentContact(EnvironmentCollisionHit hit)
        {
            if (!_rallyActive || _restartScheduled || HasResult || hit.Kind == EnvironmentContactKind.Net)
            {
                return;
            }

            var last = _touchState?.LastPhysicalTouch;
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

            if (_touchState.ContactWindow != null &&
                _touchState.ContactWindow.Action == TechniqueAction.Block)
            {
                _pendingCrossingTeam = receivingTeam;
                return;
            }

            if (_awaitingPostBlockCrossing)
            {
                _awaitingPostBlockCrossing = false;
                _postBlockerTeam = null;
            }

            BeginPossession(receivingTeam, ReceiveLeadTime());
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
            ApplyTactics(_tacticPlanner.Create(), false);
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

        private SimVector3 SelectGeometricSetTarget(
            TeamRallyDecision setDecision,
            SimVector3 fallbackTarget,
            float setContactTime)
        {
            if (_plannedAttackDecision == null || !_plannedAttackDecision.HasDecision)
            {
                throw new InvalidOperationException("No planned attacker is available for target scoring.");
            }

            var setter = _players[setDecision.Actor];
            var setterPosition = new SimVector3(
                setter.transform.position.x,
                setter.transform.position.y,
                setter.transform.position.z);
            var setterDepth = -new TeamCourtFrame(setDecision.Actor.Team)
                .ToLocal(setterPosition).Z;
            var preferredX = fallbackTarget.X;
            var lateralCandidates = new[]
            {
                Mathf.Clamp(preferredX - 0.9f, -4.2f, 4.2f),
                preferredX,
                Mathf.Clamp(preferredX + 0.9f, -4.2f, 4.2f)
            };
            var selected = SetTargetSelector.Select(new SetTargetSelectionInput(
                setDecision.Actor.Team,
                _plannedAttackDecision.Actor.Role,
                Mathf.Max(0f, setterDepth),
                fallbackTarget.Y,
                preferredX,
                PredictedBlockArmFrames(
                    setDecision.Actor.Team,
                    fallbackTarget,
                    setContactTime + SetFlightSolver.PreferredFlightSeconds(
                        TacticFor(setDecision.Actor.Team).SetRhythm)),
                lateralCandidates));
            Debug.Log(
                $"[{_configuration.LogTag}] geometric-set-target team={setDecision.Actor.Team} " +
                $"target=({selected.Target.X:0.00},{selected.Target.Y:0.00}," +
                $"{selected.Target.Z:0.00}) clearance={selected.MinimumArmClearance:0.000}");
            return selected.Target;
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

        private bool TrySelectCoverPlayer(
            TeamId team,
            ISet<PlayerId> blockers,
            SimVector3 intercept,
            out PlayerId cover)
        {
            var target = CoverageTarget(team, intercept);
            cover = default;
            var found = false;
            var bestDistance = float.PositiveInfinity;
            foreach (var pair in _players)
            {
                var id = pair.Key;
                if (id.Team != team)
                {
                    continue;
                }

                if (blockers.Contains(id))
                {
                    continue;
                }

                var distance = Vector3.Distance(pair.Value.transform.position, target);
                if (distance < bestDistance)
                {
                    cover = id;
                    bestDistance = distance;
                    found = true;
                }
            }

            return found;
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
            var prediction = _ball.State.Clone();
            var steps = Mathf.Max(1, Mathf.RoundToInt(flightSeconds / SimulatedBall.DefaultFixedStep));
            for (var step = 0; step < steps; step++)
            {
                BallIntegrator.Step(prediction, SimulatedBall.DefaultFixedStep, SimulationParameters);
            }
            return prediction.Position;
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
    }
}
