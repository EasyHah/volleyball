using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VolleyballMatch.AI;
using VolleyballMatch.Domain.Players;
using VolleyballMatch.Domain.Prototype;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.Presentation
{
    public sealed class ThreeVsThreeRallyDirector : MonoBehaviour
    {
        private static readonly BallSimulationParameters SimulationParameters =
            new BallSimulationParameters(-9.8f, 0.9995f);

        private readonly Dictionary<PlayerId, PrototypePlayerAgent> _players =
            new Dictionary<PlayerId, PrototypePlayerAgent>();
        private readonly Dictionary<PlayerId, Vector3> _homes =
            new Dictionary<PlayerId, Vector3>();
        private readonly List<LoopContact> _sequence = new List<LoopContact>(6);
        private readonly List<SimVector3> _contactCenters = new List<SimVector3>(6);

        private SimulatedBall _ball;
        private int _expectedIndex;
        private float _expectedContactTime;
        private bool _waitingForContact;
        private bool _restartScheduled;
        private string _status = "Preparing physical 3v3 loop";

        public int CompletedCycles { get; private set; }

        public int SuccessfulContacts { get; private set; }

        public int MissedRallies { get; private set; }

        public bool IsLoopRunning => _waitingForContact && !_restartScheduled;

        public TechniqueAction ExpectedAction => _sequence.Count == 0
            ? TechniqueAction.Receive
            : _sequence[_expectedIndex].Action;

        public void Initialize(SimulatedBall ball, IEnumerable<PrototypePlayerAgent> agents)
        {
            _ball = ball != null ? ball : throw new ArgumentNullException(nameof(ball));
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

                _homes.Add(agent.Id, agent.transform.position);
                _ball.RegisterContactSource(agent);
            }

            if (_players.Count != 6)
            {
                throw new ArgumentException("Exactly six agents are required.", nameof(agents));
            }

            BuildSequence();
            CacheContactCenters();
            _ball.PlayerContact += HandlePlayerContact;
            _ball.EnvironmentContact += HandleEnvironmentContact;
            StartCoroutine(StartInitialLoop(0.35f));
        }

        private void Update()
        {
            if (_waitingForContact &&
                !_restartScheduled &&
                _ball.SimulationTime > _expectedContactTime + 0.35f)
            {
                ScheduleRestart("miss (contact timeout)");
            }
        }

        private void BuildSequence()
        {
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Blue, PlayerRole.Defender), TechniqueAction.Receive, 0.70f));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Blue, PlayerRole.Setter), TechniqueAction.Set, 0.75f));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Blue, PlayerRole.Attacker), TechniqueAction.Attack, 0.40f));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Orange, PlayerRole.Defender), TechniqueAction.Receive, 0.70f));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Orange, PlayerRole.Setter), TechniqueAction.Set, 0.75f));
            _sequence.Add(new LoopContact(
                new PlayerId(TeamId.Orange, PlayerRole.Attacker), TechniqueAction.Attack, 0.40f));
        }

        private void CacheContactCenters()
        {
            _contactCenters.Clear();
            foreach (var contact in _sequence)
            {
                var actor = _players[contact.Actor];
                actor.PrepareForTraining(_homes[contact.Actor]);
                _contactCenters.Add(ContactCenter(
                    actor.PreviewContactFrames(contact.Action),
                    contact.Action));
            }
        }

        private IEnumerator StartInitialLoop(float delay)
        {
            _waitingForContact = false;
            yield return new WaitForSeconds(delay);

            foreach (var player in _players.Values)
            {
                player.CancelScheduledContact();
            }

            _expectedIndex = 0;
            const float initialFlightSeconds = 0.90f;
            var launch = ArrivalLaunchSolver.Solve(
                _contactCenters[0],
                new SimVector3(0f, -7f, -9f),
                initialFlightSeconds,
                SimulatedBall.DefaultFixedStep,
                SimulationParameters);
            _ball.ResetBall(ToUnity(launch.StartPosition));
            ScheduleExpectedContact(_expectedIndex, initialFlightSeconds, false);
            _ball.Launch(ToUnity(launch.InitialVelocity));
            _waitingForContact = true;
            _restartScheduled = false;
            _status = "Initial ball to BLUE DEFENDER";
        }

        private void ScheduleExpectedContact(
            int index,
            float secondsFromNow,
            bool followCurrentTrajectory)
        {
            foreach (var player in _players.Values)
            {
                player.CancelScheduledContact();
            }

            var contact = _sequence[index];
            var actor = _players[contact.Actor];
            actor.PrepareForTraining(_homes[contact.Actor]);
            var plannedContactCenter = followCurrentTrajectory
                ? PredictBallCenter(secondsFromNow)
                : _contactCenters[index];
            var nextIndex = (index + 1) % _sequence.Count;
            var outgoing = ReturnVelocitySolver.Solve(
                plannedContactCenter,
                _contactCenters[nextIndex],
                contact.FlightToNextSeconds,
                SimulatedBall.DefaultFixedStep,
                SimulationParameters).InitialVelocity;
            var execution = new SkillExecutionError(
                0f,
                SimVector3.Zero,
                SimVector3.Zero,
                0f,
                1f,
                SimVector3.Zero,
                TechniqueControlPolicy.MaximumControlFor(contact.Action));
            _expectedContactTime = _ball.SimulationTime + secondsFromNow;
            actor.ScheduleContact(
                contact.Action,
                _expectedContactTime,
                outgoing,
                execution,
                3000 + SuccessfulContacts,
                plannedContactCenter);
            if (followCurrentTrajectory)
            {
                var adjustment = plannedContactCenter - _contactCenters[index];
                Debug.Log(
                    $"[Physical3v3] aim team={contact.Actor.Team} action={contact.Action} " +
                    $"adjust=({adjustment.X:0.00},{adjustment.Y:0.00},{adjustment.Z:0.00})");
            }
        }

        private void HandlePlayerContact(PlayerBallContactEvent contact)
        {
            if (!_waitingForContact || _restartScheduled || contact.Candidate.Action != ExpectedAction)
            {
                return;
            }

            SuccessfulContacts++;
            var completed = _sequence[_expectedIndex];
            var completedIndex = _expectedIndex;
            _expectedIndex = (_expectedIndex + 1) % _sequence.Count;
            if (_expectedIndex == 0)
            {
                CompletedCycles++;
            }

            _status =
                $"{completed.Actor.Team} {completed.Action} HIT  " +
                $"speed {contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0} m/s";
            var impactFrame = contact.Candidate.Surface.At(contact.Hit.TimeFraction);
            var impactFromOrigin = contact.Hit.ContactPoint - impactFrame.Origin;
            var rightOffset = SimVector3.Dot(impactFromOrigin, impactFrame.Right);
            var upOffset = SimVector3.Dot(impactFromOrigin, impactFrame.Up);
            Debug.Log(
                $"[Physical3v3] contact={SuccessfulContacts} cycle={CompletedCycles} " +
                $"team={completed.Actor.Team} action={completed.Action} " +
                $"quality={contact.Hit.Centeredness:0.00} " +
                $"offset=({rightOffset:0.00},{upOffset:0.00}) " +
                $"physical={contact.PhysicalResponse.PhysicalOutgoing.Magnitude:0.0} " +
                $"target={contact.Candidate.TargetVelocity.Magnitude:0.0} " +
                $"speed={contact.TechniqueResponse.FinalOutgoing.Magnitude:0.0}");

            ScheduleExpectedContact(
                _expectedIndex,
                _sequence[completedIndex].FlightToNextSeconds,
                true);
        }

        private void HandleEnvironmentContact(EnvironmentCollisionHit hit)
        {
            if (_waitingForContact && !_restartScheduled)
            {
                ScheduleRestart($"miss ({hit.Kind})");
            }
        }

        private void ScheduleRestart(string reason)
        {
            if (_restartScheduled)
            {
                return;
            }

            _restartScheduled = true;
            _waitingForContact = false;
            MissedRallies++;
            _status = reason + " - restarting";
            Debug.LogWarning($"[Physical3v3] {_status}");
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
                $"cycles {CompletedCycles}   contacts {SuccessfulContacts}   restarts {MissedRallies}",
                GUI.skin.label);
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

        private readonly struct LoopContact
        {
            public LoopContact(PlayerId actor, TechniqueAction action, float flightToNextSeconds)
            {
                Actor = actor;
                Action = action;
                FlightToNextSeconds = flightToNextSeconds;
            }

            public PlayerId Actor { get; }

            public TechniqueAction Action { get; }

            public float FlightToNextSeconds { get; }
        }
    }
}
