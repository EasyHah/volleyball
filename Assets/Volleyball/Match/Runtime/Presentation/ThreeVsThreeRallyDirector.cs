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
        private static readonly BallSimulationParameters SimulationParameters =
            new BallSimulationParameters(-9.8f, 0.9995f);

        private readonly Dictionary<PlayerId, PrototypePlayerAgent> _players =
            new Dictionary<PlayerId, PrototypePlayerAgent>();
        private readonly List<LoopContact> _sequence = new List<LoopContact>(6);
        private readonly List<SimVector3> _contactCenters = new List<SimVector3>(6);
        private readonly List<Vector3> _rootTargets = new List<Vector3>(6);

        private SimulatedBall _ball;
        private ScoreDisplay _scoreDisplay;
        private readonly PhysicalRallyTacticPlanner _tacticPlanner =
            new PhysicalRallyTacticPlanner(7351);
        private PhysicalRallyTactics _currentTactics;
        private PhysicalRallyTactics _pendingTactics;
        private SimVector3 _pendingBlueReceiveCenter;
        private bool _hasPendingTactics;
        private int _tacticRevision;
        private int _expectedIndex;
        private float _expectedContactTime;
        private bool _waitingForContact;
        private bool _restartScheduled;
        private bool _waitingForLanding;
        private string _status = "Preparing physical 3v3 loop";
        private MatchSet _set;
        private PlayerId? _lastTouch;

        public int CompletedCycles { get; private set; }

        public int SuccessfulContacts { get; private set; }

        public int MissedRallies { get; private set; }

        public int GroundResolvedRallies { get; private set; }

        public int TacticRevision => _tacticRevision;

        public int ExecutionErrorApplications { get; private set; }

        public int MovementAssignments { get; private set; }

        public int BlockSupportAssignments { get; private set; }

        public int CoverageSupportAssignments { get; private set; }

        public float TotalMovementShortfall { get; private set; }

        public SetRoute BlueSetRoute => _currentTactics.Blue.SetRoute;

        public SpikeRoute BlueSpikeRoute => _currentTactics.Blue.SpikeRoute;

        public SetRoute OrangeSetRoute => _currentTactics.Orange.SetRoute;

        public SpikeRoute OrangeSpikeRoute => _currentTactics.Orange.SpikeRoute;

        public bool IsLoopRunning => _waitingForContact && !_restartScheduled;

        public MatchResultV1 Result { get; private set; }

        public TechniqueAction ExpectedAction => _sequence.Count == 0
            ? TechniqueAction.Receive
            : _sequence[_expectedIndex].Action;

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

            BuildSequence();
            _set = new MatchSet(context ?? throw new ArgumentNullException(nameof(context)), TeamSide.Home);
            RenderScore();
            ApplyTactics(_tacticPlanner.Create(_tacticRevision), true);
            _ball.PlayerContact += HandlePlayerContact;
            _ball.EnvironmentContact += HandleEnvironmentContact;
            _ball.NetPlaneCrossed += HandleNetPlaneCrossing;
            StartCoroutine(StartInitialLoop(0.35f));
        }

        private void Update()
        {
            if (_waitingForContact &&
                !_waitingForLanding &&
                !_restartScheduled &&
                _ball.SimulationTime > _expectedContactTime + 0.35f)
            {
                ScheduleRestart("miss (contact timeout)");
            }
        }

        private void BuildSequence()
        {
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Blue, PlayerRole.Defender), TechniqueAction.Receive));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Blue, PlayerRole.Setter), TechniqueAction.Set));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Blue, PlayerRole.Attacker), TechniqueAction.Attack));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Orange, PlayerRole.Defender), TechniqueAction.Receive));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Orange, PlayerRole.Setter), TechniqueAction.Set));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Orange, PlayerRole.Attacker), TechniqueAction.Attack));
        }

        private void ApplyTactics(PhysicalRallyTactics tactics, bool placePlayers)
        {
            _currentTactics = tactics;
            _rootTargets.Clear();
            var blueTargets = new[]
            {
                ToUnity(tactics.Blue.DefenderPosition),
                ToUnity(tactics.Blue.SetterPosition),
                ToUnity(tactics.Blue.AttackerPosition)
            };
            var orangeTargets = new[]
            {
                ToUnity(tactics.Orange.DefenderPosition),
                ToUnity(tactics.Orange.SetterPosition),
                ToUnity(tactics.Orange.AttackerPosition)
            };
            for (var index = 0; index < _sequence.Count; index++)
            {
                var contact = _sequence[index];
                var targets = contact.Actor.Team == TeamId.Blue ? blueTargets : orangeTargets;
                var side = ToSide(contact.Actor.Team);
                var roleIndex = RoleIndex(contact.Actor.Role);
                _rootTargets.Add(targets[(roleIndex + _set.RotationOffsetFor(side)) % 3]);
            }
            _contactCenters.Clear();
            for (var index = 0; index < _sequence.Count; index++)
            {
                var contact = _sequence[index];
                var actor = _players[contact.Actor];
                if (placePlayers)
                {
                    actor.PrepareForTraining(_rootTargets[index]);
                }

                _contactCenters.Add(ContactCenter(
                    actor.PreviewContactFramesAt(contact.Action, _rootTargets[index]),
                    contact.Action));
            }

            _hasPendingTactics = false;
        }

        private IEnumerator StartInitialLoop(float delay)
        {
            _waitingForContact = false;
            yield return new WaitForSeconds(delay);

            foreach (var player in _players.Values)
            {
                player.CancelScheduledContact();
            }

            _expectedIndex = _set.ReceivingSide == TeamSide.Home ? 0 : 3;
            _lastTouch = null;
            _waitingForLanding = false;
            const float initialFlightSeconds = 0.90f;
            var launch = ArrivalLaunchSolver.Solve(
                _contactCenters[_expectedIndex],
                _set.ReceivingSide == TeamSide.Home
                    ? new SimVector3(0f, -7f, -9f)
                    : new SimVector3(0f, -7f, 9f),
                initialFlightSeconds,
                SimulatedBall.DefaultFixedStep,
                SimulationParameters);
            _ball.ResetBall(ToUnity(launch.StartPosition));
            ScheduleExpectedContact(_expectedIndex, initialFlightSeconds, false);
            _ball.Launch(ToUnity(launch.InitialVelocity));
            _waitingForContact = true;
            _restartScheduled = false;
            _status = "Serve to " + _set.ReceivingSide.ToString().ToUpperInvariant() + " DEFENDER";
        }

        private void ScheduleExpectedContact(
            int index,
            float secondsFromNow,
            bool followCurrentTrajectory)
        {
            var contact = _sequence[index];
            var actor = _players[contact.Actor];
            if (index == _sequence.Count - 1 && !_hasPendingTactics)
            {
                PreparePendingTactics();
            }

            var plannedContactCenter = followCurrentTrajectory
                ? PredictBallCenter(secondsFromNow)
                : _contactCenters[index];
            var nextIndex = (index + 1) % _sequence.Count;
            var terminalAttack = contact.Action == TechniqueAction.Attack;
            var outgoing = ReturnVelocitySolver.Solve(
                plannedContactCenter,
                terminalAttack
                    ? AttackLandingTarget(contact.Actor.Team)
                    : _contactCenters[nextIndex],
                FlightTimeFor(index),
                SimulatedBall.DefaultFixedStep,
                SimulationParameters).InitialVelocity;
            var execution = SkillExecutionResolver.Resolve(
                actor.Ability,
                contact.Action,
                StablePlayerId(contact.Actor),
                _tacticRevision,
                SuccessfulContacts,
                7351,
                0.72f);
            ExecutionErrorApplications++;
            _expectedContactTime = _ball.SimulationTime + secondsFromNow;
            actor.ScheduleContact(
                contact.Action,
                _expectedContactTime,
                outgoing,
                execution,
                3000 + SuccessfulContacts,
                plannedContactCenter,
                movementTarget: _rootTargets[index],
                movementStartSimulationTime: _ball.SimulationTime);
            MovementAssignments++;
            TotalMovementShortfall += actor.MovementShortfall;
            if (terminalAttack)
            {
                ScheduleBlockCoverage(contact.Actor.Team, _expectedContactTime);
            }

            if (followCurrentTrajectory)
            {
                var adjustment = plannedContactCenter - _contactCenters[index];
                Debug.Log(
                    $"[Physical3v3] aim team={contact.Actor.Team} action={contact.Action} " +
                    $"adjust=({adjustment.X:0.00},{adjustment.Y:0.00},{adjustment.Z:0.00})");
            }
        }

        private void ScheduleBlockCoverage(TeamId attackingTeam, float attackContactTime)
        {
            var defendingTeam = attackingTeam == TeamId.Blue ? TeamId.Orange : TeamId.Blue;
            var defendingTactic = defendingTeam == TeamId.Blue ? _currentTactics.Blue : _currentTactics.Orange;
            var blocker = new PlayerId(defendingTeam, defendingTactic.Blocker);
            var cover = new PlayerId(defendingTeam, defendingTactic.CoverReceiver);
            _players[blocker].ScheduleSupportAction(
                TechniqueAction.Block,
                attackContactTime,
                ToUnity(defendingTactic.BlockPosition),
                _ball.SimulationTime);
            _players[cover].ScheduleSupportAction(
                TechniqueAction.Receive,
                attackContactTime + 0.12f,
                ToUnity(defendingTactic.CoverPosition),
                _ball.SimulationTime);
            BlockSupportAssignments++;
            CoverageSupportAssignments++;
            Debug.Log(
                $"[Physical3v3] block team={defendingTeam} blocker={defendingTactic.Blocker} " +
                $"cover={defendingTactic.CoverReceiver} lane=({defendingTactic.BlockPosition.X:0.00},{defendingTactic.BlockPosition.Z:0.00})");
        }

        private void HandlePlayerContact(PlayerBallContactEvent contact)
        {
            if (!_waitingForContact || _restartScheduled || contact.Candidate.Action != ExpectedAction)
            {
                return;
            }

            SuccessfulContacts++;
            var completed = _sequence[_expectedIndex];
            _set.RecordContact(
                StableId(completed.Actor),
                _players[completed.Actor].ScheduledMovementDistance);
            var completedIndex = _expectedIndex;
            var completedFlightSeconds = FlightTimeFor(completedIndex);
            _lastTouch = completed.Actor;
            _expectedIndex = (_expectedIndex + 1) % _sequence.Count;
            if (_expectedIndex == 0)
            {
                CompletedCycles++;
            }

            var styleLabel = completed.Action == TechniqueAction.Set
                ? $" {_players[completed.Actor].CurrentSetStyle}"
                : string.Empty;
            _status =
                $"{completed.Actor.Team} {completed.Action}{styleLabel} HIT  " +
                $"speed {contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0} m/s";
            var impactFrame = contact.Candidate.Surface.At(contact.Hit.TimeFraction);
            var impactFromOrigin = contact.Hit.ContactPoint - impactFrame.Origin;
            var rightOffset = SimVector3.Dot(impactFromOrigin, impactFrame.Right);
            var upOffset = SimVector3.Dot(impactFromOrigin, impactFrame.Up);
            Debug.Log(
                $"[Physical3v3] contact={SuccessfulContacts} cycle={CompletedCycles} " +
                $"team={completed.Actor.Team} action={completed.Action} " +
                $"style={styleLabel.Trim()} " +
                $"quality={contact.Hit.Centeredness:0.00} " +
                $"offset=({rightOffset:0.00},{upOffset:0.00}) " +
                $"physical={contact.PhysicalResponse.PhysicalOutgoing.Magnitude:0.0} " +
                $"target={contact.Candidate.TargetVelocity.Magnitude:0.0} " +
                $"speed={contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0}");

            if (completed.Action == TechniqueAction.Attack)
            {
                PromotePendingTactics();
                _waitingForLanding = true;
                return;
            }

            ScheduleExpectedContact(
                _expectedIndex,
                completedFlightSeconds,
                true);
        }

        private void HandleEnvironmentContact(EnvironmentCollisionHit hit)
        {
            if (!_waitingForContact || _restartScheduled || Result != null)
            {
                return;
            }

            if (hit.Kind == EnvironmentContactKind.Net)
            {
                return;
            }

            var last = _lastTouch;
            var outcome = last.HasValue
                ? MatchRallyReferee.ResolveGroundLanding(ToSide(last.Value.Team), hit.ContactPoint, CourtBuilder.HalfWidth, CourtBuilder.HalfLength)
                : new RallyOutcome(TeamSide.Away, true, "ground before contact");
            ResolveRally(
                outcome,
                outcome.IsFault ? null : last,
                outcome.IsFault ? last : null,
                outcome.Reason);
            GroundResolvedRallies++;
        }

        private void ScheduleRestart(string reason)
        {
            if (_restartScheduled)
            {
                return;
            }

            var expected = _sequence[_expectedIndex].Actor;
            ResolveRally(new RallyOutcome(ToSide(expected.Team) == TeamSide.Home ? TeamSide.Away : TeamSide.Home, true, reason), null, expected, reason);
        }

        private void HandleNetPlaneCrossing(NetPlaneCrossingEvent crossing)
        {
            if (!_waitingForContact || _restartScheduled || Result != null || !_lastTouch.HasValue)
            {
                return;
            }

            var outcome = MatchRallyReferee.ResolveNetCrossing(
                ToSide(_lastTouch.Value.Team), crossing.Point, CourtBuilder.HalfWidth, CourtBuilder.NetHeight);
            if (outcome.HasValue)
            {
                ResolveRally(outcome.Value, null, _lastTouch, outcome.Value.Reason);
            }
        }

        private void ResolveRally(RallyOutcome outcome, PlayerId? scorer, PlayerId? errorPlayer, string reason)
        {
            if (_restartScheduled || Result != null)
            {
                return;
            }

            _restartScheduled = true;
            _waitingForContact = false;
            _waitingForLanding = false;
            _set.ResolveRally(outcome.Winner, scorer.HasValue ? StableId(scorer.Value) : null, errorPlayer.HasValue ? StableId(errorPlayer.Value) : null);
            RenderScore();
            _status = $"{reason}  {_set.HomeScore}:{_set.AwayScore}";
            if (_set.IsComplete)
            {
                Result = _set.CreateResult();
                foreach (var player in _players.Values) player.CancelScheduledContact();
                _ball.Stop();
                _status = $"RESULT READY  {_set.HomeScore}:{_set.AwayScore}";
                RenderScore();
                return;
            }

            _tacticRevision++;
            ApplyTactics(_tacticPlanner.Create(_tacticRevision), false);
            StartCoroutine(StartInitialLoop(0.65f));
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18f, 18f, 560f, 104f), string.Empty);
            var heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(34f, 28f, 520f, 30f), "PHYSICAL 3v3 COOP LOOP", heading);
            GUI.Label(new Rect(34f, 60f, 520f, 28f), _status, heading);
            GUI.Label(
                new Rect(34f, 91f, 520f, 24f),
                $"{BlueSetRoute}/{BlueSpikeRoute} vs {OrangeSetRoute}/{OrangeSpikeRoute}   " +
                $"cycles {CompletedCycles} contacts {SuccessfulContacts} blocks {BlockSupportAssignments}",
                GUI.skin.label);
        }

        private void PreparePendingTactics()
        {
            _pendingTactics = _tacticPlanner.Create(_tacticRevision + 1);
            var blueDefender = _players[new PlayerId(TeamId.Blue, PlayerRole.Defender)];
            var target = ToUnity(_pendingTactics.Blue.DefenderPosition);
            _pendingBlueReceiveCenter = ContactCenter(
                blueDefender.PreviewContactFramesAt(TechniqueAction.Receive, target),
                TechniqueAction.Receive);
            _hasPendingTactics = true;
        }

        private void PromotePendingTactics()
        {
            if (!_hasPendingTactics)
            {
                return;
            }

            _tacticRevision++;
            ApplyTactics(_pendingTactics, false);
        }

        private float FlightTimeFor(int contactIndex)
        {
            return contactIndex switch
            {
                0 => 0.70f,
                1 => _currentTactics.Blue.SetFlightSeconds,
                2 => _currentTactics.Blue.AttackFlightSeconds,
                3 => 0.70f,
                4 => _currentTactics.Orange.SetFlightSeconds,
                5 => _currentTactics.Orange.AttackFlightSeconds,
                _ => throw new ArgumentOutOfRangeException(nameof(contactIndex))
            };
        }

        private static int StablePlayerId(PlayerId player)
        {
            return ((int)player.Team * 10) + (int)player.Role;
        }

        private static TeamSide ToSide(TeamId team)
        {
            return team == TeamId.Blue ? TeamSide.Home : TeamSide.Away;
        }

        private static StablePlayerId StableId(PlayerId player)
        {
            var prefix = player.Team == TeamId.Blue ? "home-" : "away-";
            return new StablePlayerId(prefix + player.Role.ToString().ToLowerInvariant());
        }

        private void RenderScore()
        {
            _scoreDisplay.RenderSet(_set.HomeScore, _set.AwayScore, _set.ServingSide, _set.IsComplete);
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

        private static SimVector3 AttackLandingTarget(TeamId attackerTeam)
        {
            var targetZ = attackerTeam == TeamId.Blue ? 4.5f : -4.5f;
            return new SimVector3(0f, SimulatedBall.DefaultRadius + 0.15f, targetZ);
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

        private SimVector3 PredictBallCenter(float flightSeconds)
        {
            var prediction = _ball.State.Clone();
            var steps = Mathf.RoundToInt(flightSeconds / SimulatedBall.DefaultFixedStep);
            for (var step = 0; step < steps; step++)
            {
                BallIntegrator.Step(prediction, SimulatedBall.DefaultFixedStep, SimulationParameters);
            }

            return prediction.Position;
        }

        private static Vector3 ToUnity(SimVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static Vector3 ToUnity(CourtPoint value)
        {
            return new Vector3(value.X, 0f, value.Z);
        }

        private readonly struct LoopContact
        {
            public LoopContact(PlayerId actor, TechniqueAction action)
            {
                Actor = actor;
                Action = action;
            }

            public PlayerId Actor { get; }

            public TechniqueAction Action { get; }
        }
    }
}
