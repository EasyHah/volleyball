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
using MatchResultV1 = Volleyball.Shared.Contracts.MatchResultV1;
using TeamSide = Volleyball.Shared.Contracts.TeamSide;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation
{
    public sealed class ThreeVsThreeRallyDirector : MonoBehaviour
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
        private PhysicalRallyTactics _currentTactics;
        private MatchSet _set;
        private RallyTouchState _touchState;
        private TeamRallyDecision _scheduledDecision;
        private TeamRallyDecision _plannedAttackDecision;
        private PlayerId? _scheduledPrimaryActor;
        private PlayerId? _scheduledBlocker;
        private TeamId? _pendingCrossingTeam;
        private float _expectedContactTime;
        private bool _contactDeadlineActive;
        private bool _rallyActive;
        private bool _restartScheduled;
        private bool _lastTouchWasBackSetAttack;
        private int _tacticRevision;
        private int _decisionIndex;
        private int _contactGroupSequence = 3000;
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

        public int PostBlockContinuations { get; private set; }

        public int NonSetterSetContacts { get; private set; }

        public int DefenderAttackContacts { get; private set; }

        public int IllegalContactFaults { get; private set; }

        public float MaximumAppliedMovementCorrection { get; private set; }

        public SetRoute BlueSetRoute => _currentTactics.Blue.SetRoute;

        public SpikeRoute BlueSpikeRoute => _currentTactics.Blue.SpikeRoute;

        public SetRoute OrangeSetRoute => _currentTactics.Orange.SetRoute;

        public SpikeRoute OrangeSpikeRoute => _currentTactics.Orange.SpikeRoute;

        public bool IsLoopRunning => _rallyActive && !_restartScheduled;

        public MatchResultV1 Result { get; private set; }

        public void Initialize(
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> agents,
            MatchContextV1 context,
            ScoreDisplay scoreDisplay)
        {
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
                    throw new ArgumentException("Six unique non-null agents are required.", nameof(agents));
                }

                _ball.RegisterContactSource(agent);
            }

            if (_players.Count != 6)
            {
                throw new ArgumentException("Exactly six agents are required.", nameof(agents));
            }

            _set = new MatchSet(context ?? throw new ArgumentNullException(nameof(context)), TeamSide.Home);
            ApplyTactics(_tacticPlanner.Create(_tacticRevision), true);
            RenderScore();

            _ball.ContactCandidateResolver = ResolveCandidate;
            _ball.PlayerContactRejected += HandleRejectedPlayerContact;
            _ball.PlayerContact += HandlePlayerContact;
            _ball.EnvironmentContact += HandleEnvironmentContact;
            _ball.NetPlaneCrossed += HandleNetPlaneCrossing;
            StartCoroutine(StartInitialLoop(0.35f));
        }

        private void OnDestroy()
        {
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
            if (!_rallyActive || _restartScheduled || Result != null)
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

            foreach (var player in _players.Values)
            {
                var grounded = player.transform.position;
                grounded.y = 0f;
                player.PrepareForTraining(grounded);
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

            const float initialFlightSeconds = 0.90f;
            var nominalReceiver = _players[new PlayerId(receivingTeam, PlayerRole.Defender)];
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
            _rallyActive = true;
            _restartScheduled = false;

            var decision = PlanDecision(receivingTeam, RallyDecisionStage.Receive, initialFlightSeconds);
            ScheduleDecision(decision, initialFlightSeconds);
            _status = $"Serve to {receivingTeam.ToString().ToUpperInvariant()} possession";
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
            var decision = PlanDecision(team, RallyDecisionStage.Receive, availableSeconds);
            ScheduleDecision(decision, availableSeconds);
            Debug.Log(
                $"[Physical3v3] possession team={team} touches=0 " +
                $"available={availableSeconds:0.00}");
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
            var players = new List<RallyPlayerSnapshot>(3);
            foreach (PlayerRole role in Enum.GetValues(typeof(PlayerRole)))
            {
                var id = new PlayerId(team, role);
                var player = _players[id];
                players.Add(new RallyPlayerSnapshot(
                    id,
                    ToSimulation(player.transform.position),
                    player.Ability));
            }

            var rolePreference = _tacticRevision % 4 == 3 ? 0.35f : 1f;
            var weights = new RallyTacticalWeights(rolePreference, 1.15f, 1f, 1f);
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
                weights);
            var decision = _decisionPlanner.Plan(input);
            if (!decision.HasDecision)
            {
                Debug.Log(
                    $"[Physical3v3] decision=none team={team} stage={stage} " +
                    $"touches={_touchState.CountedTeamTouches}");
                return decision;
            }

            Debug.Log(
                $"[Physical3v3] decision team={team} stage={stage} actor={decision.Actor.Role} " +
                $"score={decision.Score.Total:0.00} reach={decision.Score.Reachability:0.00} " +
                $"role={decision.Score.NominalRole:0.00} approach={decision.Score.Approach:0.00} " +
                $"angle={decision.Score.Angle:0.00}");
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
            var predictedContactCenter = PredictBallCenter(flightSeconds);
            if (decision.Action == TechniqueAction.Set)
            {
                var attackFlight = TacticFor(decision.Actor.Team).SetFlightSeconds;
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
                        $"[Physical3v3] attack-preplan team={decision.Actor.Team} " +
                        $"actor={_plannedAttackDecision.Actor.Role} " +
                        $"takeoff=({_plannedAttackDecision.MovementTarget.X:0.00}," +
                        $"{_plannedAttackDecision.MovementTarget.Y:0.00}," +
                        $"{_plannedAttackDecision.MovementTarget.Z:0.00})");
                }
            }
            var outgoingTarget = OutgoingTargetFor(decision);
            var outgoingFlightSeconds = OutgoingFlightSecondsFor(decision.Actor.Team, decision.Action);
            var outgoing = ReturnVelocitySolver.Solve(
                predictedContactCenter,
                outgoingTarget,
                outgoingFlightSeconds,
                SimulatedBall.DefaultFixedStep,
                SimulationParameters).InitialVelocity;
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
                predictedContactCenter,
                movementTarget: movementTarget,
                movementStartSimulationTime: _ball.SimulationTime,
                attackApproach: decision.AttackApproach);
            _scheduledPrimaryActor = decision.Actor;
            MovementAssignments++;
            TotalMovementShortfall += actor.MovementShortfall;

            var eligibleActors = new List<PlayerId> { decision.Actor };
            if (decision.Action == TechniqueAction.Receive)
            {
                foreach (var candidate in decision.Candidates)
                {
                    if (!candidate.IsFeasible || candidate.Actor.Equals(decision.Actor))
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
                    predictedContactCenter,
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

            if (!_rallyActive || _restartScheduled || Result != null || _touchState == null)
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
                $"[Physical3v3] fault team={actor.Team} actor={actor.Role} " +
                $"action={rejected.Candidate.Action} reason={rejected.Reason}");
            ResolveRally(
                new RallyOutcome(winner, true, rejected.Reason),
                null,
                actor,
                rejected.Reason);
        }

        private void HandlePlayerContact(PlayerBallContactEvent contact)
        {
            if (!_rallyActive || _restartScheduled || Result != null || !contact.Candidate.Actor.HasValue)
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

            var style = contact.Candidate.Action == TechniqueAction.Set
                ? actor.CurrentSetStyle.ToString()
                : string.Empty;
            _status =
                $"{actorId.Team} {actorId.Role} {contact.Candidate.Action} HIT  " +
                $"speed {contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0} m/s";
            Debug.Log(
                $"[Physical3v3] contact={SuccessfulContacts} team={actorId.Team} " +
                $"actor={actorId.Role} action={contact.Candidate.Action} style={style} " +
                $"touches={_touchState.CountedTeamTouches} quality={contact.Hit.Centeredness:0.00} " +
                $"speed={contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0}");

            switch (contact.Candidate.Action)
            {
                case TechniqueAction.Receive:
                    ScheduleDecision(
                        PlanDecision(actorId.Team, RallyDecisionStage.Organize, ReceiveFlightSeconds),
                        ReceiveFlightSeconds);
                    break;
                case TechniqueAction.Set:
                    var setFlight = TacticFor(actorId.Team).SetFlightSeconds;
                    var attackDecision = _plannedAttackDecision;
                    _plannedAttackDecision = null;
                    if (attackDecision == null || !attackDecision.HasDecision)
                    {
                        attackDecision = PlanDecision(
                            actorId.Team,
                            RallyDecisionStage.Attack,
                            setFlight);
                    }
                    ScheduleDecision(attackDecision, setFlight);
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
                $"[Physical3v3] block-plan team={defendingTeam} blocker={blocker.Role} " +
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
                $"[Physical3v3] block-window team={blocker.Team} actor={blocker.Role} " +
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

            var reboundTeam = contact.TechniqueResponse.FinalOutgoing.Z < -0.01f
                ? TeamId.Blue
                : contact.TechniqueResponse.FinalOutgoing.Z > 0.01f
                    ? TeamId.Orange
                    : blocker.Team;
            PostBlockContinuations++;
            Debug.Log(
                $"[Physical3v3] block-contact team={blocker.Team} actor={blocker.Role} " +
                $"rebound={reboundTeam} speed={contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0}");
            BeginPossession(reboundTeam, ReceiveLeadTime());
        }

        private void HandleEnvironmentContact(EnvironmentCollisionHit hit)
        {
            if (!_rallyActive || _restartScheduled || Result != null || hit.Kind == EnvironmentContactKind.Net)
            {
                return;
            }

            var last = _touchState?.LastPhysicalTouch;
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
                CourtBuilder.HalfLength);
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
            if (!_rallyActive || _restartScheduled || Result != null ||
                _touchState == null || !_touchState.LastPhysicalTouch.HasValue)
            {
                return;
            }

            var last = _touchState.LastPhysicalTouch.Value;
            var outcome = MatchRallyReferee.ResolveNetCrossing(
                ToSide(last.Team),
                crossing.Point,
                CourtBuilder.HalfWidth,
                CourtBuilder.NetHeight);
            if (outcome.HasValue)
            {
                crossing.ConsumeRemainingStep();
                ResolveRally(outcome.Value, null, last, outcome.Value.Reason);
                if (_lastTouchWasBackSetAttack && outcome.Value.IsFault)
                {
                    BackSetAttackFaults++;
                }
                return;
            }

            var receivingTeam = _ball.State.Velocity.Z >= 0f ? TeamId.Orange : TeamId.Blue;
            if (_touchState.ContactWindow != null &&
                _touchState.ContactWindow.Action == TechniqueAction.Block)
            {
                _pendingCrossingTeam = receivingTeam;
                return;
            }

            BeginPossession(receivingTeam, ReceiveLeadTime());
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
            if (_restartScheduled || Result != null)
            {
                return;
            }

            _restartScheduled = true;
            _rallyActive = false;
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
            Debug.Log(
                $"[Physical3v3] rally={reason} winner={outcome.Winner} " +
                $"score={_set.HomeScore}:{_set.AwayScore}");
            if (_set.IsComplete)
            {
                Result = _set.CreateResult();
                _ball.Stop();
                _status = $"RESULT READY  {_set.HomeScore}:{_set.AwayScore}";
                RenderScore();
                Debug.Log(
                    $"[Physical3v3] RESULT score={_set.HomeScore}:{_set.AwayScore} " +
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
                    var plannedAttacker = _players[_plannedAttackDecision.Actor];
                    if (_plannedAttackDecision.AttackApproach.HasValue)
                    {
                        return ContactCenter(
                            plannedAttacker.PreviewAttackContactFramesAt(
                                _plannedAttackDecision.AttackApproach.Value),
                            TechniqueAction.Attack);
                    }

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
            var role = action == TechniqueAction.Set ? PlayerRole.Setter : PlayerRole.Attacker;
            var point = action == TechniqueAction.Set ? tactic.SetterPosition : tactic.AttackerPosition;
            var player = _players[new PlayerId(team, role)];
            return ContactCenter(
                player.PreviewContactFramesAt(action, ToUnity(point)),
                action);
        }

        private float OutgoingFlightSecondsFor(TeamId team, TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => ReceiveFlightSeconds,
                TechniqueAction.Set => TacticFor(team).SetFlightSeconds,
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
            var best = new PlayerId(team, PlayerRole.Setter);
            var bestScore = float.PositiveInfinity;
            foreach (PlayerRole role in Enum.GetValues(typeof(PlayerRole)))
            {
                var id = new PlayerId(team, role);
                var player = _players[id];
                var groundDistance = Vector2.Distance(
                    new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(target.x, target.z));
                var score = groundDistance - (player.Ability.Jump * 0.35f);
                if (score < bestScore)
                {
                    best = id;
                    bestScore = score;
                }
            }

            return best;
        }

        private PlayerId SelectCoverPlayer(TeamId team, PlayerId blocker, SimVector3 intercept)
        {
            var target = CoverageTarget(team, intercept);
            var best = default(PlayerId);
            var found = false;
            var bestDistance = float.PositiveInfinity;
            foreach (PlayerRole role in Enum.GetValues(typeof(PlayerRole)))
            {
                var id = new PlayerId(team, role);
                if (id.Equals(blocker))
                {
                    continue;
                }

                var distance = Vector3.Distance(_players[id].transform.position, target);
                if (distance < bestDistance)
                {
                    best = id;
                    bestDistance = distance;
                    found = true;
                }
            }

            return found ? best : new PlayerId(team, PlayerRole.Defender);
        }

        private static Vector3 BlockRootTarget(TeamId team, SimVector3 intercept)
        {
            var worldDepth = team == TeamId.Blue ? -0.28f : 0.28f;
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
            foreach (PlayerRole role in Enum.GetValues(typeof(PlayerRole)))
            {
                _players[new PlayerId(team, role)].DisableEmergencyReceiveWindow();
            }
        }

        private Vector3 TacticalRootTarget(PlayerId player)
        {
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
            GUI.Box(new Rect(18f, 18f, 600f, 118f), string.Empty);
            var heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(34f, 28f, 560f, 30f), "DYNAMIC PHYSICAL 3v3", heading);
            GUI.Label(new Rect(34f, 60f, 560f, 28f), _status, heading);
            var possession = _touchState == null
                ? "none"
                : $"{_touchState.PossessionTeam}/{_touchState.CountedTeamTouches}";
            GUI.Label(
                new Rect(34f, 91f, 560f, 36f),
                $"possession {possession} contacts {SuccessfulContacts} " +
                $"blocks {PhysicalBlockContacts} non-setter sets {NonSetterSetContacts} " +
                $"defender attacks {DefenderAttackContacts}",
                GUI.skin.label);
        }

        private int NextContactGroup()
        {
            return _contactGroupSequence++;
        }

        private static int StablePlayerNumber(PlayerId player)
        {
            return ((int)player.Team * 10) + (int)player.Role;
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

        private static StablePlayerId StableId(PlayerId player)
        {
            var prefix = player.Team == TeamId.Blue ? "home-" : "away-";
            return new StablePlayerId(prefix + player.Role.ToString().ToLowerInvariant());
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
