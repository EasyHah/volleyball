using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using MatchContextV1 = Volleyball.Shared.Contracts.MatchContextV1;
using MatchContextV2 = Volleyball.Shared.Contracts.MatchContextV2;
using MatchResultV1 = Volleyball.Shared.Contracts.MatchResultV1;
using MatchResultV2 = Volleyball.Shared.Contracts.MatchResultV2;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation
{
    public sealed class ReplayDecisionEvent
    {
        private ReplayDecisionEvent(
            float simulationTimeSeconds,
            RallyDecisionStage stage,
            TeamId team,
            float availableSeconds,
            SimVector3 predictedBallTarget,
            RallyTacticalWeights weights,
            TeamRallyDecision decision)
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

        internal static ReplayDecisionEvent Create(
            float simulationTimeSeconds,
            RallyDecisionStage stage,
            TeamId team,
            float availableSeconds,
            SimVector3 predictedBallTarget,
            RallyTacticalWeights weights,
            TeamRallyDecision decision)
        {
            return new ReplayDecisionEvent(
                simulationTimeSeconds,
                stage,
                team,
                availableSeconds,
                predictedBallTarget,
                weights,
                decision);
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
            TechniqueAction action)
            : base(kind, simulationTimeSeconds, team, playerId)
        {
            Action = action;
        }

        public TechniqueAction Action { get; }
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
            new PhysicalRallyTacticPlanner(7351);
        private readonly TeamRallyDecisionPlanner _decisionPlanner =
            new TeamRallyDecisionPlanner(7351);

        private SimulatedBall _ball;
        private ScoreDisplay _scoreDisplay;
        private BlockImpactFeedback _blockImpactFeedback;
        private AiDecisionTimeController _aiDecisionTimeController;
        private PhysicalRallyTactics _currentTactics;
        private MatchSet _set;
        private RallyTouchState _touchState;
        private TeamRallyDecision _scheduledDecision;
        private TeamRallyDecision _plannedAttackDecision;
        private PlayerId? _scheduledPrimaryActor;
        private PlayerId? _scheduledBlocker;
        private TeamId? _pendingCrossingTeam;
        private float _expectedContactTime;
        private float _scheduledSetFlightSeconds;
        private bool _contactDeadlineActive;
        private bool _rallyActive;
        private bool _restartScheduled;
        private bool _lastTouchWasBackSetAttack;
        private bool _controlledHandlingActive;
        private SetQualityAssessment? _lastSetQualityAssessment;
        private SimVector3? _lastPlannedAttackContactCenter;
        private SimVector3? _lastActualAttackContactCenter;
        private AttackOutcome _lastSetReplanOutcome;
        private AttackResponsibility _lastAttackResponsibility;
        private PlayerId? _lastSetAttackActor;
        private int _tacticRevision;
        private int _decisionIndex;
        private int _aiDecisionRequestVersion;
        private int _aiRequestSequence;
        private int _contactGroupSequence = 3000;
        private RallyTacticalWeights _activeTacticalWeights;
        private PhysicalMatchConfiguration _configuration;
        private string _status = "Preparing dynamic physical 3v3";

        public int CompletedCycles { get; private set; }

        public int SuccessfulContacts { get; private set; }

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

        public int NonSetterSetContacts { get; private set; }

        public int DefenderAttackContacts { get; private set; }

        public int BlueAttackContacts { get; private set; }

        public int OrangeAttackContacts { get; private set; }

        public int IllegalContactFaults { get; private set; }

        public SetQualityAssessment? LastSetQualityAssessment => _lastSetQualityAssessment;

        public SimVector3? LastPlannedAttackContactCenter => _lastPlannedAttackContactCenter;

        public SimVector3? LastActualAttackContactCenter => _lastActualAttackContactCenter;

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

        public MatchResultV1 Result { get; private set; }

        public MatchResultV2 ResultV2 { get; private set; }

        private bool HasResult => Result != null || ResultV2 != null;

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

        public MatchContextV1 MatchContext => _set?.Context;

        public MatchContextV2 MatchContextV2 => _set?.ContextV2;

        public bool IsFrontRow(PlayerId player)
        {
            return _set != null && _set.IsFrontRow(StableId(player));
        }

        public int RotationPositionFor(PlayerId player)
        {
            return _set == null ? 0 : _set.RotationPositionFor(StableId(player));
        }

        public void Initialize(
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> agents,
            MatchContextV1 context,
            ScoreDisplay scoreDisplay,
            IRallyTacticalWeightSource tacticalWeightSource = null,
            PhysicalMatchConfiguration configuration = null)
        {
            var matchContext = context ?? throw new ArgumentNullException(nameof(context));
            InitializeCore(
                ball,
                agents,
                scoreDisplay,
                tacticalWeightSource,
                configuration,
                matchContext.Home.Players.Count,
                matchContext.Away.Players.Count,
                () => new MatchSet(matchContext, TeamSide.Home, _configuration.SetRules));
        }

        public void InitializeV2(
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> agents,
            MatchContextV2 context,
            ScoreDisplay scoreDisplay,
            IRallyTacticalWeightSource tacticalWeightSource = null,
            PhysicalMatchConfiguration configuration = null)
        {
            var matchContext = context ?? throw new ArgumentNullException(nameof(context));
            InitializeCore(
                ball,
                agents,
                scoreDisplay,
                tacticalWeightSource,
                configuration,
                matchContext.Home.Players.Count,
                matchContext.Away.Players.Count,
                () => new MatchSet(matchContext, TeamSide.Home, _configuration.SetRules));
        }

        private void InitializeCore(
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> agents,
            ScoreDisplay scoreDisplay,
            IRallyTacticalWeightSource tacticalWeightSource,
            PhysicalMatchConfiguration configuration,
            int homeRosterSize,
            int awayRosterSize,
            Func<MatchSet> createSet)
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
            ApplyTactics(_tacticPlanner.Create(_tacticRevision), true);
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

        private void OnDestroy()
        {
            _aiDecisionRequestVersion++;
            _aiDecisionTimeController?.CancelPending();
            if (_ball == null)
            {
                return;
            }

            _ball.ContactCandidateResolver = null;
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
            _scheduledDecision = null;
            _plannedAttackDecision = null;
            _scheduledPrimaryActor = null;
            _scheduledBlocker = null;
            _pendingCrossingTeam = null;
            _contactDeadlineActive = false;
            _lastTouchWasBackSetAttack = false;
            _controlledHandlingActive = false;

            const float initialFlightSeconds = 0.90f;
            var nominalReceiver = FindPlayer(
                receivingTeam,
                role => role == PlayerRole.Defender);
            var receiveCenter = ContactCenter(
                nominalReceiver.PreviewContactFrames(TechniqueAction.Receive),
                TechniqueAction.Receive);
            var arrivalVelocity = receivingTeam == TeamId.Blue
                ? new SimVector3(0f, -7f, -9f)
                : new SimVector3(0f, -7f, 9f);
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

            DisableEmergencyReceiveWindows(TeamId.Blue);
            DisableEmergencyReceiveWindows(TeamId.Orange);
            DisablePhysicalBlockWindows();
            _touchState.BeginPossession(team);
            _plannedAttackDecision = null;
            _controlledHandlingActive = false;
            BeginPossessionDecision(team, availableSeconds);
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
            NotifyReplay(
                ReplayDecisionPlanned,
                ReplayDecisionEvent.Create(
                    _ball.SimulationTime,
                    stage,
                    team,
                    availableSeconds,
                    predictedBallCenter,
                    _activeTacticalWeights,
                    decision));
            return decision;
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
            var execution = SkillExecutionResolver.Resolve(
                actor.Ability,
                decision.Action,
                StablePlayerNumber(decision.Actor),
                _tacticRevision,
                SuccessfulContacts,
                7351,
                0.72f);
            ExecutionErrorApplications++;

            _expectedContactTime = _ball.SimulationTime + flightSeconds;
            var movementTarget = ToUnity(decision.MovementTarget);
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
            var accepted = _touchState.Accept(
                actorId,
                contact.Candidate.Action,
                contact.ContactSimulationTime);
            if (accepted.Disposition != RallyContactDisposition.Accept)
            {
                return;
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
            NotifyReplay(
                ReplayContactAccepted,
                new ReplayContactEvent(
                    contact.Candidate.Action == TechniqueAction.Block ? "Block" : "Contact",
                    contact.ContactSimulationTime,
                    actorId.Team,
                    StableId(actorId),
                    contact.Candidate.Action));

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
                    ScheduleAttackFromActualSet(attackDecision, setFlight);
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
        }

        private void ScheduleAttackFromActualSet(
            TeamRallyDecision provisionalDecision,
            float plannedFlightSeconds)
        {
            if (provisionalDecision == null || !provisionalDecision.HasDecision ||
                !provisionalDecision.AttackApproach.HasValue ||
                !provisionalDecision.AttackContactPlan.HasValue)
            {
                ScheduleDecision(provisionalDecision, plannedFlightSeconds);
                return;
            }

            var plannedContact = provisionalDecision.AttackContactPlan.Value;
            var prediction = TrajectoryPredictor.Predict(
                _ball.State,
                SimulationParameters,
                SimulatedBall.DefaultFixedStep,
                Mathf.Max(2f, plannedFlightSeconds + 0.5f),
                260,
                GroundHeight);
            var actualArrival = ClosestSetArrival(
                prediction,
                plannedContact.ContactCenter,
                plannedFlightSeconds);
            var horizontalError = GroundDistance(
                actualArrival.Position,
                plannedContact.ContactCenter);
            var quality = SetQualityAssessment.Evaluate(new SetQualityInput(
                horizontalError,
                Mathf.Abs(actualArrival.Position.Y - plannedContact.ContactCenter.Y),
                Mathf.Abs(actualArrival.TimeSeconds - plannedFlightSeconds),
                Mathf.Abs(actualArrival.Position.Z),
                actualArrival.TimeSeconds));
            var attacker = _players[provisionalDecision.Actor];
            var replan = SetAttackReplanner.Replan(
                provisionalDecision.AttackApproach.Value,
                plannedContact,
                actualArrival.Position,
                actualArrival.TimeSeconds,
                attacker.Ability.MaxAttackReach,
                quality);
            var replacement = new TeamRallyDecision(
                provisionalDecision.Actor,
                TechniqueAction.Attack,
                replan.ContactPlan.ContactCenter,
                replan.Approach.Takeoff,
                provisionalDecision.BallTarget,
                provisionalDecision.Score,
                provisionalDecision.Candidates,
                replan.Approach,
                replan.ContactPlan);

            _lastSetQualityAssessment = quality;
            _lastPlannedAttackContactCenter = plannedContact.ContactCenter;
            _lastActualAttackContactCenter = replan.ContactPlan.ContactCenter;
            _lastSetReplanOutcome = replan.Outcome;
            _lastSetAttackActor = provisionalDecision.Actor;
            _lastAttackResponsibility = SetQualityAssessment.PrimaryResponsibility(
                quality.Grade,
                replan.Outcome);
            Debug.Log(
                $"[{_configuration.LogTag}] set-quality team={provisionalDecision.Actor.Team} " +
                $"grade={quality.Grade} replan={replan.ContactPlan.Outcome} " +
                $"actual=({replan.ContactPlan.ContactCenter.X:0.00}," +
                $"{replan.ContactPlan.ContactCenter.Y:0.00}," +
                $"{replan.ContactPlan.ContactCenter.Z:0.00}) {quality.Reason}");

            if (replan.OpensSpikeContactWindow)
            {
                ScheduleDecision(replacement, Mathf.Max(0.1f, actualArrival.TimeSeconds));
                return;
            }

            ScheduleControlledHandling(
                replacement,
                replan,
                Mathf.Max(0.1f, actualArrival.TimeSeconds));
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
            _scheduledBlocker = null;
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
                TacticFor(decision.Actor.Team).AttackFlightSeconds,
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
                replan.Approach,
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
                _scheduledBlocker = null;
                return;
            }

            var defendingTeam = Opponent(attackingTeam);
            var blocker = SelectBlocker(defendingTeam, intercept.Point);
            var blockTarget = BlockRootTarget(defendingTeam, intercept.Point);
            _players[blocker].ScheduleBlockContact(
                attackContactTime + intercept.TimeSeconds,
                blockTarget,
                _ball.SimulationTime,
                BlockReboundVelocity(attackingTeam),
                NextContactGroup());
            _scheduledBlocker = blocker;
            BlockSupportAssignments++;

            var cover = SelectCoverPlayer(defendingTeam, blocker, intercept.Point);
            _players[cover].ScheduleSupportAction(
                TechniqueAction.Receive,
                attackContactTime + intercept.TimeSeconds + 0.12f,
                CoverageTarget(defendingTeam, intercept.Point),
                _ball.SimulationTime);
            CoverageSupportAssignments++;
            Debug.Log(
                $"[{_configuration.LogTag}] block-plan team={defendingTeam} blocker={blocker.Role} " +
                $"cover={cover.Role} intercept=({intercept.Point.X:0.00}," +
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
            if (!_scheduledBlocker.HasValue ||
                !NetPlaneInterception.TryPredict(
                    _ball.State,
                    SimulationParameters,
                    SimulatedBall.DefaultFixedStep,
                    1.5f,
                    out var intercept))
            {
                _scheduledBlocker = null;
                return;
            }

            var blocker = _scheduledBlocker.Value;
            var blockTime = attackContact.ContactSimulationTime + intercept.TimeSeconds;
            _players[blocker].RetargetBlockContact(
                blockTime,
                BlockRootTarget(blocker.Team, intercept.Point),
                BlockReboundVelocity(attackingTeam));
            var appliedBlockTime = _players[blocker].PhysicalBlockContactTime;
            _touchState.OpenWindow(new RallyContactWindow(
                blocker.Team,
                TechniqueAction.Block,
                appliedBlockTime - ContactWindowLead,
                appliedBlockTime + ContactWindowTail,
                new[] { blocker }));
            Debug.Log(
                $"[{_configuration.LogTag}] block-window team={blocker.Team} actor={blocker.Role} " +
                $"attack={attackDecision.Actor.Role} " +
                $"time={appliedBlockTime:0.00} predicted={blockTime:0.00} " +
                $"intercept=({intercept.Point.X:0.00},{intercept.Point.Y:0.00}," +
                $"{intercept.Point.Z:0.00})");
        }

        private void HandleAcceptedBlock(PlayerBallContactEvent contact)
        {
            var blocker = contact.Candidate.Actor.Value;
            _players[blocker].DisableBlockContactWindow();
            _scheduledBlocker = null;
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

            var reboundTeam = contact.TechniqueResponse.FinalOutgoing.Z < -0.01f
                ? TeamId.Blue
                : contact.TechniqueResponse.FinalOutgoing.Z > 0.01f
                    ? TeamId.Orange
                    : blocker.Team;
            PostBlockContinuations++;
            Debug.Log(
                $"[{_configuration.LogTag}] block-contact team={blocker.Team} actor={blocker.Role} " +
                $"rebound={reboundTeam} speed={contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0} " +
                $"effect={BlockImpactEffects}");
            BeginPossession(reboundTeam, ReceiveLeadTime());
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
            var loser = _scheduledPrimaryActor?.Team ?? _touchState.PossessionTeam;
            var outcome = new RallyOutcome(Opponent(ToSide(loser)), true, reason);
            ResolveRally(outcome, null, _scheduledPrimaryActor, reason);
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

            _restartScheduled = true;
            _rallyActive = false;
            _aiDecisionRequestVersion++;
            _aiDecisionTimeController?.CancelPending();
            _contactDeadlineActive = false;
            _pendingCrossingTeam = null;
            _scheduledDecision = null;
            _plannedAttackDecision = null;
            _scheduledPrimaryActor = null;
            _scheduledBlocker = null;
            DisableEmergencyReceiveWindows(TeamId.Blue);
            DisableEmergencyReceiveWindows(TeamId.Orange);
            foreach (var player in _players.Values)
            {
                player.CancelScheduledContact();
            }

            _set.ResolveRally(
                outcome.Winner,
                scorer.HasValue ? StableId(scorer.Value) : null,
                errorPlayer.HasValue ? StableId(errorPlayer.Value) : null);
            RenderScore();
            _status = $"{reason}  {_set.HomeScore}:{_set.AwayScore}";
            NotifyReplay(
                ReplayRallyResolved,
                new ReplayRallyResolvedEvent(
                    _ball.SimulationTime,
                    FromSide(outcome.Winner),
                    scorer.HasValue ? StableId(scorer.Value) : null,
                    errorPlayer.HasValue ? StableId(errorPlayer.Value) : null,
                    reason));
            Debug.Log(
                $"[{_configuration.LogTag}] rally={reason} winner={outcome.Winner} " +
                $"score={_set.HomeScore}:{_set.AwayScore}");
            if (_set.IsComplete)
            {
                if (_set.ContextV2 != null)
                {
                    ResultV2 = _set.CreateResultV2();
                }
                else
                {
                    Result = _set.CreateResult();
                }
                _ball.Stop();
                _status = $"RESULT READY  {_set.HomeScore}:{_set.AwayScore}";
                RenderScore();
                Debug.Log(
                    $"[{_configuration.LogTag}] RESULT score={_set.HomeScore}:{_set.AwayScore} " +
                    $"contacts={SuccessfulContacts} blocks={PhysicalBlockContacts} " +
                    $"nonSetterSets={NonSetterSetContacts} defenderAttacks={DefenderAttackContacts}");
                return;
            }

            _tacticRevision++;
            ApplyTactics(_tacticPlanner.Create(_tacticRevision), false);
            StartCoroutine(StartInitialLoop(0.55f));
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

        private SimVector3 NextContactCenter(TeamId team, TechniqueAction action)
        {
            var tactic = TacticFor(team);
            var point = action == TechniqueAction.Set ? tactic.SetterPosition : tactic.AttackerPosition;
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

        private PlayerId SelectCoverPlayer(TeamId team, PlayerId blocker, SimVector3 intercept)
        {
            var target = CoverageTarget(team, intercept);
            var best = default(PlayerId);
            var found = false;
            var bestDistance = float.PositiveInfinity;
            foreach (var pair in _players)
            {
                var id = pair.Key;
                if (id.Team != team)
                {
                    continue;
                }

                if (id.Equals(blocker))
                {
                    continue;
                }

                var distance = Vector3.Distance(pair.Value.transform.position, target);
                if (distance < bestDistance)
                {
                    best = id;
                    bestDistance = distance;
                    found = true;
                }
            }

            return found
                ? best
                : FindPlayer(team, role => role == PlayerRole.Defender).Id;
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
            _scheduledBlocker = null;
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
