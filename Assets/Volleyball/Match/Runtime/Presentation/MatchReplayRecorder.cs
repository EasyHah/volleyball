using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Replay;

namespace Volleyball.Presentation
{
    public sealed class MatchReplayRecorder : MonoBehaviour
    {
        private PhysicalMatchRallyDirector _director;
        private SimulatedBall _ball;
        private List<PrototypePlayerAgent> _players;
        private MatchReplayV1 _replay;
        private float _captureStartTime;
        private int _nextSampleIndex;
        private int _eventSequence;
        private bool _capturing;
        private MatchReplayEventV1 _lastSetChainEvent;

        public bool IsComplete { get; private set; }

        public static MatchReplayRecorder Attach(
            PhysicalMatchRallyDirector director,
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> players)
        {
            if (director == null)
            {
                throw new ArgumentNullException(nameof(director));
            }

            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }

            var recorder = director.gameObject.AddComponent<MatchReplayRecorder>();
            recorder.Initialize(director, ball, players);
            return recorder;
        }

        public void StartCapture()
        {
            if (_capturing)
            {
                throw new InvalidOperationException("Replay capture is already running.");
            }

            _replay = CreateReplay();
            _captureStartTime = _ball.SimulationTime;
            _nextSampleIndex = 1;
            _eventSequence = 0;
            IsComplete = false;
            _capturing = true;
            _lastSetChainEvent = null;
            ForceSnapshot();
        }

        public MatchReplayV1 Complete()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("Replay capture has not completed.");
            }

            _replay.Validate();
            return _replay;
        }

        private void Initialize(
            PhysicalMatchRallyDirector director,
            SimulatedBall ball,
            IEnumerable<PrototypePlayerAgent> players)
        {
            _director = director;
            _ball = ball;
            _players = new List<PrototypePlayerAgent>(players ?? throw new ArgumentNullException(nameof(players)));
            if (_players.Count != 12)
            {
                throw new ArgumentException("Replay capture requires twelve players.", nameof(players));
            }

            _players.Sort((left, right) => string.CompareOrdinal(left.StableId.Value, right.StableId.Value));
            _director.ReplayServeStarted += RecordSimpleEvent;
            _director.ReplayDecisionPlanned += RecordDecision;
            _director.ReplayContactAccepted += RecordContact;
            _director.ReplayNetCrossed += RecordSimpleEvent;
            _director.ReplayGroundContact += RecordSimpleEvent;
            _director.ReplayRallyResolved += RecordResolution;
            _ball.SimulationStepped += CaptureRegularSamples;
        }

        private void OnDestroy()
        {
            if (_director == null)
            {
                return;
            }

            _director.ReplayServeStarted -= RecordSimpleEvent;
            _director.ReplayDecisionPlanned -= RecordDecision;
            _director.ReplayContactAccepted -= RecordContact;
            _director.ReplayNetCrossed -= RecordSimpleEvent;
            _director.ReplayGroundContact -= RecordSimpleEvent;
            _director.ReplayRallyResolved -= RecordResolution;
            _ball.SimulationStepped -= CaptureRegularSamples;
        }

        private void CaptureRegularSamples(float simulationTime)
        {
            if (!_capturing || IsComplete)
            {
                return;
            }

            CaptureScheduledSamplesThrough(simulationTime);
        }

        private MatchReplayV1 CreateReplay()
        {
            var players = new List<MatchReplayPlayerV1>(_players.Count);
            foreach (var player in _players)
            {
                players.Add(new MatchReplayPlayerV1
                {
                    PlayerId = player.StableId.Value,
                    PrototypeId = player.Id.Team + "/" + player.Id.Role + "/" + (player.Id.RosterSlot + 1),
                    DisplayName = player.StableId.Value,
                    Team = player.Id.Team.ToString(),
                    Role = player.Id.Role.ToString(),
                    RosterSlot = player.Id.RosterSlot + 1,
                    Ability = ToReplayAbility(player.Ability)
                });
            }

            return new MatchReplayV1
            {
                SourceScene = SceneManager.GetActiveScene().name,
                CapturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Court = new MatchReplayCourtV1
                {
                    WidthMeters = CourtBuilder.HalfWidth * 2f,
                    LengthMeters = _director.CourtHalfLength * 2f
                },
                TargetScore = _director.TargetScore,
                Players = players,
                InitialState = CreateInitialState()
            };
        }

        private MatchReplayInitialStateV1 CreateInitialState()
        {
            return new MatchReplayInitialStateV1
            {
                HomeScore = _director.HomeScore,
                AwayScore = _director.AwayScore,
                ServingTeam = _director.ServingTeam.ToString(),
                HomeRotationOffset = _director.HomeRotationOffset,
                AwayRotationOffset = _director.AwayRotationOffset
            };
        }

        private void RecordDecision(ReplayDecisionEvent replayEvent)
        {
            if (!_capturing || IsComplete)
            {
                return;
            }

            var simulationTime = MonotonicEventTime(replayEvent.SimulationTimeSeconds);
            CaptureScheduledSamplesThrough(simulationTime);
            var snapshotIndex = ForceSnapshot(simulationTime);
            _replay.Events.Add(new MatchReplayEventV1
            {
                Kind = "Decision",
                SimulationTimeSeconds = simulationTime,
                SnapshotIndex = snapshotIndex,
                Team = replayEvent.Team.ToString(),
                PlayerId = StableId(replayEvent.SelectedPlayer),
                Decision = new MatchReplayDecisionV1
                {
                    Stage = replayEvent.Stage.ToString(),
                    Team = replayEvent.Team.ToString(),
                    Action = replayEvent.SelectedAction.ToString(),
                    PredictedBallTarget = ToReplayVector(replayEvent.PredictedBallTarget),
                    AvailableSeconds = replayEvent.AvailableSeconds,
                    Weights = ToReplayWeights(replayEvent.Weights),
                    SelectedPlayerId = StableId(replayEvent.SelectedPlayer),
                    SelectedAction = replayEvent.SelectedAction.ToString(),
                    Candidates = ToReplayCandidates(replayEvent),
                    Diagnostics = new MatchReplayDecisionDiagnosticsV1
                    {
                        ConsumedAbilities = ToReplayConsumedAbilities(replayEvent),
                        Organization = ToReplayOrganizationDiagnostics(replayEvent.OrganizationDiagnostic)
                    }
                }
            });
        }

        private void RecordContact(ReplayContactEvent replayEvent)
        {
            if (!_capturing || IsComplete)
            {
                return;
            }

            var simulationTime = MonotonicEventTime(replayEvent.SimulationTimeSeconds);
            CaptureScheduledSamplesThrough(simulationTime);
            var replayRecord = new MatchReplayEventV1
            {
                Kind = replayEvent.Kind,
                SimulationTimeSeconds = simulationTime,
                SnapshotIndex = ForceSnapshot(simulationTime),
                Team = replayEvent.Team.ToString(),
                PlayerId = replayEvent.PlayerId?.Value,
                SetChain = ToReplaySetChain(replayEvent.SetChain)
            };
            _replay.Events.Add(replayRecord);
            if (replayRecord.SetChain != null)
            {
                _lastSetChainEvent = replayRecord;
            }
        }

        private void RecordSimpleEvent(ReplaySimpleEvent replayEvent)
        {
            RecordEvent(replayEvent.Kind, replayEvent.SimulationTimeSeconds, replayEvent.Team, replayEvent.PlayerId);
        }

        private void RecordResolution(ReplayRallyResolvedEvent replayEvent)
        {
            if (_lastSetChainEvent?.SetChain != null)
            {
                _lastSetChainEvent.SetChain.PrimaryResponsibility =
                    _director.LastAttackResponsibility.ToString();
            }

            RecordEvent(
                replayEvent.Kind,
                replayEvent.SimulationTimeSeconds,
                replayEvent.Team,
                replayEvent.PlayerId,
                replayEvent.ErrorPlayerId,
                replayEvent.Reason);
            _capturing = false;
            _replay.IsComplete = true;
            _replay.Seal();
            IsComplete = true;
        }

        private static MatchReplaySetChainV1 ToReplaySetChain(ReplaySetChainEvent setChain)
        {
            if (setChain == null)
            {
                return null;
            }

            return new MatchReplaySetChainV1
            {
                PlannedAttackContactCenter = ToReplayVector(setChain.PlannedAttackContactCenter),
                ActualAttackContactCenter = ToReplayVector(setChain.ActualAttackContactCenter),
                QualityGrade = setChain.QualityGrade.ToString(),
                ReplanOutcome = setChain.ReplanOutcome.ToString(),
                PrimaryResponsibility = setChain.PrimaryResponsibility.ToString(),
                Reason = setChain.Reason
            };
        }

        private void RecordEvent(
            string kind,
            float simulationTime,
            TeamId team,
            Volleyball.Shared.Contracts.PlayerId? playerId,
            Volleyball.Shared.Contracts.PlayerId? errorPlayerId = null,
            string reason = null)
        {
            if (!_capturing || IsComplete)
            {
                return;
            }

            simulationTime = MonotonicEventTime(simulationTime);
            CaptureScheduledSamplesThrough(simulationTime);
            var snapshotIndex = ForceSnapshot(simulationTime);
            _replay.Events.Add(new MatchReplayEventV1
            {
                Kind = kind,
                SimulationTimeSeconds = simulationTime,
                SnapshotIndex = snapshotIndex,
                Team = team.ToString(),
                PlayerId = playerId?.Value,
                ErrorPlayerId = errorPlayerId?.Value,
                Reason = reason
            });
        }

        private float MonotonicEventTime(float requestedTime)
        {
            if (_replay.Snapshots.Count == 0)
            {
                return requestedTime;
            }

            return Mathf.Max(
                requestedTime,
                _replay.Snapshots[_replay.Snapshots.Count - 1].SimulationTimeSeconds);
        }

        private int ForceSnapshot()
        {
            return CaptureSnapshot(_ball.SimulationTime);
        }

        private int ForceSnapshot(float simulationTime)
        {
            return CaptureSnapshot(simulationTime);
        }

        private void CaptureScheduledSamplesThrough(float simulationTime)
        {
            var scheduledTime = ScheduledSampleTime(_nextSampleIndex);
            while (scheduledTime <= simulationTime)
            {
                CaptureSnapshot(scheduledTime);
                _nextSampleIndex++;
                scheduledTime = ScheduledSampleTime(_nextSampleIndex);
            }
        }

        private float ScheduledSampleTime(int sampleIndex)
        {
            return (float)(_captureStartTime +
                (sampleIndex * (double)MatchReplayV1.SampleIntervalSeconds));
        }

        private int CaptureSnapshot(float simulationTime)
        {
            _replay.Snapshots.Add(new MatchReplaySnapshotV1
            {
                SimulationTimeSeconds = simulationTime,
                EventSequence = ++_eventSequence,
                HomeScore = _director.HomeScore,
                AwayScore = _director.AwayScore,
                ServingTeam = _director.ServingTeam.ToString(),
                HomeRotationOffset = _director.HomeRotationOffset,
                AwayRotationOffset = _director.AwayRotationOffset,
                RallyPhase = _director.ReplayRallyPhase,
                PossessionTeam = _director.PossessionTeam?.ToString(),
                LastTouchPlayerId = _director.LastTouchPlayer?.Value,
                Ball = new MatchReplayBallStateV1
                {
                    Position = ToReplayVector(_ball.State.Position),
                    Velocity = ToReplayVector(_ball.State.Velocity)
                },
                Players = CapturePlayers()
            });
            return _replay.Snapshots.Count - 1;
        }

        private List<MatchReplayPlayerStateV1> CapturePlayers()
        {
            var samples = new List<MatchReplayPlayerStateV1>(_players.Count);
            foreach (var player in _players)
            {
                samples.Add(new MatchReplayPlayerStateV1
                {
                    PlayerId = player.StableId.Value,
                    Position = ToReplayVector(player.transform.position),
                    YawDegrees = player.transform.eulerAngles.y,
                    ScheduledAction = player.ReplayScheduledAction,
                    MovementTarget = ToReplayVector(player.ScheduledMovementTarget)
                });
            }

            return samples;
        }

        private string StableId(PlayerId id)
        {
            foreach (var player in _players)
            {
                if (player.Id.Equals(id))
                {
                    return player.StableId.Value;
                }
            }

            throw new InvalidOperationException("Replay event references an unknown player.");
        }

        private static Dictionary<string, float> ToReplayWeights(Volleyball.AI.RallyTacticalWeights weights)
        {
            return new Dictionary<string, float>
            {
                ["rolePreference"] = weights.RolePreference,
                ["reachability"] = weights.Reachability,
                ["approachDistance"] = weights.ApproachDistance,
                ["directionTolerance"] = weights.DirectionTolerance
            };
        }

        private List<MatchReplayCandidateScoreV1> ToReplayCandidates(ReplayDecisionEvent replayEvent)
        {
            var candidates = new List<MatchReplayCandidateScoreV1>(replayEvent.Candidates.Count);
            foreach (var candidate in replayEvent.Candidates)
            {
                candidates.Add(new MatchReplayCandidateScoreV1
                {
                    PlayerId = StableId(candidate.Actor),
                    IsFeasible = candidate.IsFeasible,
                    ExclusionReason = candidate.IsFeasible
                        ? string.Empty
                        : candidate.Score.Reachability >= 0f ? "ConsecutiveTouch" : "Unreachable",
                    Reachability = candidate.Score.Reachability,
                    NominalRole = candidate.Score.NominalRole,
                    Approach = candidate.Score.Approach,
                    Angle = candidate.Score.Angle,
                    Technique = TechniqueFor(candidate.Actor, replayEvent.SelectedAction),
                    Total = candidate.Score.Total
                });
            }

            return candidates;
        }

        private List<MatchReplayConsumedAbilityV1> ToReplayConsumedAbilities(ReplayDecisionEvent replayEvent)
        {
            var abilities = new List<MatchReplayConsumedAbilityV1>(replayEvent.Candidates.Count);
            foreach (var candidate in replayEvent.Candidates)
            {
                var player = PlayerFor(candidate.Actor);
                var ability = player.Ability;
                abilities.Add(new MatchReplayConsumedAbilityV1
                {
                    PlayerId = player.StableId.Value,
                    Mobility = ability.Mobility,
                    Reaction = ability.Reaction,
                    Jump = ability.Jump,
                    ReceiveTechnique = ability.ReceiveTechnique,
                    SetTechnique = ability.SetTechnique,
                    AttackTechnique = ability.AttackTechnique,
                    AttackPower = ability.AttackPower,
                    MaxAttackReach = ability.MaxAttackReach
                });
            }

            return abilities;
        }

        private MatchReplayOrganizationDiagnosticsV1 ToReplayOrganizationDiagnostics(
            ReplayOrganizationDecisionDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                return null;
            }

            return new MatchReplayOrganizationDiagnosticsV1
            {
                Target = ToReplayVector(diagnostic.Target),
                FirstPassLanding = ToReplayVector(diagnostic.FirstPassLanding),
                ZoneGrade = diagnostic.ZoneGrade.ToString(),
                SetterPlayerId = StableId(diagnostic.Setter),
                SetterArrival = diagnostic.SetterReachStatus,
                SetterMovementMeters = diagnostic.SetterPrepositionMovementMeters,
                OrganizerPlayerId = StableId(diagnostic.Organizer),
                FallbackReason = diagnostic.FallbackReason
            };
        }

        private static MatchReplayAbilityV1 ToReplayAbility(PlayerAbilityProfile ability)
        {
            return new MatchReplayAbilityV1
            {
                Receive = ability.ReceiveTechnique,
                Set = ability.SetTechnique,
                Attack = ability.AttackTechnique,
                Block = ability.Jump,
                Serve = ability.AttackPower,
                Speed = ability.Mobility
            };
        }

        private float TechniqueFor(PlayerId playerId, TechniqueAction action)
        {
            var ability = PlayerFor(playerId).Ability;
            return action == TechniqueAction.Attack
                ? ability.AttackPower * ability.AttackTechnique
                : ability.TechniqueFor(action);
        }

        private PrototypePlayerAgent PlayerFor(PlayerId playerId)
        {
            foreach (var player in _players)
            {
                if (player.Id.Equals(playerId))
                {
                    return player;
                }
            }

            throw new InvalidOperationException("Replay decision references an unknown player.");
        }

        private static MatchReplayVector3V1 ToReplayVector(Vector3 value)
        {
            return new MatchReplayVector3V1 { X = value.x, Y = value.y, Z = value.z };
        }

        private static MatchReplayVector3V1 ToReplayVector(Volleyball.Domain.Simulation.SimVector3 value)
        {
            return new MatchReplayVector3V1 { X = value.X, Y = value.Y, Z = value.Z };
        }
    }
}
