using System;
using System.Collections;
using UnityEngine;
using VolleyballMatch.AI;
using VolleyballMatch.Domain.Players;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.Presentation
{
    public sealed class PhysicsTrainingDirector : MonoBehaviour
    {
        private static readonly BallSimulationParameters SimulationParameters =
            new BallSimulationParameters(-9.8f, 0.9995f);

        private SimulatedBall _ball;
        private PrototypePlayerAgent _receiver;
        private PrototypePlayerAgent _setter;
        private PrototypePlayerAgent _attacker;
        private Vector3 _receiverHome;
        private Vector3 _setterHome;
        private Vector3 _attackerHome;
        private bool _waitingForContact;
        private bool _currentSucceeded;
        private string _lastDetails = "Preparing physical contact drills";

        public int CompletedDrills { get; private set; }

        public int SuccessfulContacts { get; private set; }

        public int MissedDrills { get; private set; }

        public TechniqueAction CurrentAction { get; private set; }

        public void Initialize(
            SimulatedBall ball,
            PrototypePlayerAgent receiver,
            PrototypePlayerAgent setter,
            PrototypePlayerAgent attacker)
        {
            _ball = ball != null ? ball : throw new ArgumentNullException(nameof(ball));
            _receiver = receiver != null ? receiver : throw new ArgumentNullException(nameof(receiver));
            _setter = setter != null ? setter : throw new ArgumentNullException(nameof(setter));
            _attacker = attacker != null ? attacker : throw new ArgumentNullException(nameof(attacker));
            _receiverHome = receiver.transform.position;
            _setterHome = setter.transform.position;
            _attackerHome = attacker.transform.position;

            _ball.RegisterContactSource(receiver);
            _ball.RegisterContactSource(setter);
            _ball.RegisterContactSource(attacker);
            _ball.PlayerContact += HandlePlayerContact;
            _ball.EnvironmentContact += HandleEnvironmentContact;
            StartCoroutine(RunTrainingLoop());
        }

        private IEnumerator RunTrainingLoop()
        {
            yield return new WaitForSeconds(0.35f);
            while (true)
            {
                yield return RunDrill(_receiver, _receiverHome, TechniqueAction.Receive, 1f);
                yield return RunDrill(_setter, _setterHome, TechniqueAction.Set, 1f);
                yield return RunDrill(_attacker, _attackerHome, TechniqueAction.Attack, 0.6f);
            }
        }

        private IEnumerator RunDrill(
            PrototypePlayerAgent actor,
            Vector3 home,
            TechniqueAction action,
            float flightSeconds)
        {
            CurrentAction = action;
            _receiver.CancelScheduledContact();
            _setter.CancelScheduledContact();
            _attacker.CancelScheduledContact();
            actor.PrepareForTraining(home);

            var targetCenter = ContactCenter(actor.PreviewContactFrames(action));
            var arrivalVelocity = ArrivalVelocityFor(action);
            var launch = ArrivalLaunchSolver.Solve(
                targetCenter,
                arrivalVelocity,
                flightSeconds,
                SimulatedBall.DefaultFixedStep,
                SimulationParameters);
            var execution = new SkillExecutionError(
                0f,
                SimVector3.Zero,
                SimVector3.Zero,
                0f,
                1f,
                SimVector3.Zero,
                TechniqueControlPolicy.MaximumControlFor(action));

            _ball.ResetBall(ToUnity(launch.StartPosition));
            actor.ScheduleContact(
                action,
                flightSeconds,
                TargetVelocityFor(action),
                execution,
                1000 + CompletedDrills,
                targetCenter);
            _waitingForContact = true;
            _currentSucceeded = false;
            _lastDetails = $"{DisplayName(action)}: ball in physical flight";
            _ball.Launch(ToUnity(launch.InitialVelocity));

            var timeout = flightSeconds + 0.65f;
            while (_waitingForContact && _ball.SimulationTime < timeout)
            {
                yield return null;
            }

            if (_waitingForContact)
            {
                _waitingForContact = false;
                _lastDetails = $"{DisplayName(action)}: miss (no swept intersection)";
            }

            if (_currentSucceeded)
            {
                SuccessfulContacts++;
            }
            else
            {
                MissedDrills++;
            }

            CompletedDrills++;
            Debug.Log(
                $"[PhysicsTraining] action={action} success={_currentSucceeded} " +
                $"completed={CompletedDrills} contacts={SuccessfulContacts} misses={MissedDrills} " +
                $"details={_lastDetails}");
            yield return new WaitForSeconds(0.7f);
            _ball.ResetBall(_ball.transform.position);
        }

        private void HandlePlayerContact(PlayerBallContactEvent contact)
        {
            if (!_waitingForContact || contact.Candidate.Action != CurrentAction)
            {
                return;
            }

            _waitingForContact = false;
            _currentSucceeded = true;
            _lastDetails =
                $"{DisplayName(CurrentAction)}: HIT  quality {contact.Hit.Centeredness:0.00}  " +
                $"control {contact.TechniqueResponse.AppliedControl:0.00}";
        }

        private void HandleEnvironmentContact(EnvironmentCollisionHit hit)
        {
            if (!_waitingForContact)
            {
                return;
            }

            _waitingForContact = false;
            _currentSucceeded = false;
            _lastDetails = $"{DisplayName(CurrentAction)}: miss ({hit.Kind})";
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Box(new Rect(18f, 18f, 520f, 96f), string.Empty);
            GUI.Label(new Rect(34f, 28f, 490f, 32f), "PHYSICAL CONTACT TRAINING", style);
            GUI.Label(new Rect(34f, 62f, 490f, 28f), _lastDetails, style);
            GUI.Label(
                new Rect(34f, 88f, 490f, 24f),
                $"contacts {SuccessfulContacts}/{CompletedDrills}   misses {MissedDrills}",
                GUI.skin.label);
        }

        private static SimVector3 ContactCenter(System.Collections.Generic.IReadOnlyList<ContactSurfaceFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                throw new ArgumentException("At least one contact frame is required.", nameof(frames));
            }

            var origin = SimVector3.Zero;
            var normal = SimVector3.Zero;
            foreach (var frame in frames)
            {
                origin += frame.Origin;
                normal += frame.Normal;
            }

            origin /= frames.Count;
            normal = normal.Normalized;
            return origin + (normal * SimulatedBall.DefaultRadius);
        }

        private static SimVector3 ArrivalVelocityFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => new SimVector3(0f, -8f, -4f),
                TechniqueAction.Set => new SimVector3(0f, -7f, -2.5f),
                TechniqueAction.Attack => new SimVector3(0f, 2f, 0f),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private static SimVector3 TargetVelocityFor(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => new SimVector3(0f, 6f, 5f),
                TechniqueAction.Set => new SimVector3(2f, 7f, 6f),
                TechniqueAction.Attack => new SimVector3(0f, -7f, 16f),
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private static string DisplayName(TechniqueAction action)
        {
            return action switch
            {
                TechniqueAction.Receive => "FOREARM PASS",
                TechniqueAction.Set => "OVERHEAD SET",
                TechniqueAction.Attack => "JUMP SPIKE",
                _ => action.ToString().ToUpperInvariant()
            };
        }

        private static Vector3 ToUnity(SimVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }
    }
}
