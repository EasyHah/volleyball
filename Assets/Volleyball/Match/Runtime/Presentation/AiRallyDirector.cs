using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Prototype;

namespace Volleyball.Presentation
{
    public sealed class AiRallyDirector : MonoBehaviour
    {
        private readonly Dictionary<PlayerId, PrototypePlayerAgent> _players =
            new Dictionary<PlayerId, PrototypePlayerAgent>();

        private PrototypeMatch _match;
        private DeterministicRallyPlanner _planner;
        private BallFlight _ball;
        private ScoreDisplay _scoreDisplay;
        private bool _initialized;

        public int CompletedRallies { get; private set; }

        public int TotalScore => _match == null ? 0 : _match.BlueScore + _match.OrangeScore;

        public bool IsRallyActive { get; private set; }

        public void Initialize(
            int seed,
            IEnumerable<PrototypePlayerAgent> agents,
            BallFlight visibleBall,
            ScoreDisplay display)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("The rally director is already initialized.");
            }

            if (agents == null)
            {
                throw new ArgumentNullException(nameof(agents));
            }

            _ball = visibleBall != null ? visibleBall : throw new ArgumentNullException(nameof(visibleBall));
            _scoreDisplay = display != null ? display : throw new ArgumentNullException(nameof(display));

            foreach (var agent in agents)
            {
                if (agent == null)
                {
                    throw new ArgumentException("Agents cannot contain null entries.", nameof(agents));
                }

                if (!_players.TryAdd(agent.Id, agent))
                {
                    throw new ArgumentException("Agents must have unique player IDs.", nameof(agents));
                }
            }

            if (_players.Count != 6)
            {
                throw new ArgumentException("Exactly six player agents are required.", nameof(agents));
            }

            _match = new PrototypeMatch(TeamId.Blue);
            _planner = new DeterministicRallyPlanner(seed);
            _scoreDisplay.Render(_match);
            _initialized = true;
            StartCoroutine(RunMatch());
        }

        private IEnumerator RunMatch()
        {
            while (true)
            {
                _match.BeginNextRally();
                IsRallyActive = true;
                var plan = _planner.CreatePlan(_match.ServingTeam, _match.RallyNumber);

                for (var index = 0; index < plan.Contacts.Count; index++)
                {
                    var isFinalContact = index == plan.Contacts.Count - 1;
                    yield return PlayContact(plan.Contacts[index], isFinalContact);
                }

                if (_match.TryAwardPoint(plan.WinningTeam))
                {
                    CompletedRallies++;
                    _scoreDisplay.Render(_match);
                }

                IsRallyActive = false;
                SetTeamPose(plan.WinningTeam, StickFigurePose.Celebrate, 0.5f);
                yield return new WaitForSeconds(0.75f);
                SetAllPlayersPose(StickFigurePose.Ready, 0.5f);
            }
        }

        private IEnumerator PlayContact(ContactPlan contact, bool isFinalContact)
        {
            var actor = _players[contact.Actor];
            var destination = ToWorld(contact.ActorTarget);
            if (contact.Kind == RallyActionKind.Block)
            {
                var teamSign = contact.Actor.Team == TeamId.Blue ? -1f : 1f;
                destination.z = teamSign * 0.6f;
            }

            yield return actor.MoveTo(destination);
            actor.Rig.SetPose(ToPose(contact.Kind), 0.35f);
            yield return new WaitForSeconds(0.16f);

            var arc = new BallArc(
                new CourtPoint(actor.transform.position.x, actor.transform.position.z),
                contact.BallTarget,
                contact.ContactHeight,
                contact.BallTargetHeight,
                contact.ArcHeight);
            yield return _ball.Play(arc, contact.DurationSeconds, null);

            if (isFinalContact && contact.Kind == RallyActionKind.Spike)
            {
                actor.Rig.SetPose(StickFigurePose.Landing, 0.45f);
                yield return new WaitForSeconds(0.25f);
            }

            actor.Rig.SetPose(StickFigurePose.Ready, 0.35f);
        }

        private void SetTeamPose(TeamId team, StickFigurePose pose, float blend)
        {
            foreach (var player in _players)
            {
                if (player.Key.Team == team)
                {
                    player.Value.Rig.SetPose(pose, blend);
                }
            }
        }

        private void SetAllPlayersPose(StickFigurePose pose, float blend)
        {
            foreach (var player in _players.Values)
            {
                player.Rig.SetPose(pose, blend);
            }
        }

        private static StickFigurePose ToPose(RallyActionKind kind)
        {
            return kind switch
            {
                RallyActionKind.Serve => StickFigurePose.Serve,
                RallyActionKind.Receive => StickFigurePose.Receive,
                RallyActionKind.Dig => StickFigurePose.Receive,
                RallyActionKind.Set => StickFigurePose.Set,
                RallyActionKind.Approach => StickFigurePose.Approach,
                RallyActionKind.Spike => StickFigurePose.Spike,
                RallyActionKind.Block => StickFigurePose.Block,
                _ => StickFigurePose.Ready
            };
        }

        private static Vector3 ToWorld(CourtPoint point)
        {
            return new Vector3(point.X, 0f, point.Z);
        }
    }
}
