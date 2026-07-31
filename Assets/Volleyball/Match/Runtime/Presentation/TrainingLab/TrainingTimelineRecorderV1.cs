using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class TrainingTimelineRecorderV1 : IDisposable
    {
        private readonly FormalSixVsSixRallyDirector _director;
        private readonly SimulatedBall _ball;
        private readonly Dictionary<PlayerId, StablePlayerId> _stableByRuntime;
        private readonly Dictionary<StablePlayerId, TeamId> _teamByStable;
        private readonly List<TrainingTimelineEventV1> _timeline =
            new List<TrainingTimelineEventV1>();
        private readonly List<DecisionSnapshotV1> _decisions =
            new List<DecisionSnapshotV1>();
        private bool _disposed;
        private bool _resolved;
        private TeamId? _winner;
        private string _resolutionReason = string.Empty;

        public TrainingTimelineRecorderV1(
            FormalSixVsSixRallyDirector director,
            SimulatedBall ball,
            IReadOnlyCollection<PrototypePlayerAgent> players)
        {
            _director = director ??
                        throw new ArgumentNullException(nameof(director));
            _ball = ball ?? throw new ArgumentNullException(nameof(ball));
            if (director.TrainingScenario == null)
                throw new ArgumentException(
                    "Timeline recording requires a training run.",
                    nameof(director));
            if (players == null || players.Count != 12)
                throw new ArgumentException(
                    "Timeline recording requires twelve formal players.",
                    nameof(players));
            _stableByRuntime = players.ToDictionary(
                player => player.Id,
                player => player.StableId);
            _teamByStable = players.ToDictionary(
                player => player.StableId,
                player => player.Id.Team);

            _director.ReplayDecisionPlanned += OnDecision;
            _director.ReplayContactAccepted += OnContact;
            _director.ReceiveOrganizationAuthorityCommitted += OnGateH;
            _director.GateISetIntentCommitted += OnGateISetIntent;
            _director.AttackDefenseAuthorityCommitted += OnGateIContact;
            _director.ReplayNetCrossed += OnNetCrossing;
            _director.ReplayDefenseAttemptRecorded += OnDefenseAttempt;
            _director.ReplayRallyResolved += OnResolved;
            _ball.EnvironmentContact += OnEnvironmentContact;
        }

        public IReadOnlyList<TrainingTimelineEventV1> Timeline => _timeline;
        public IReadOnlyList<DecisionSnapshotV1> Decisions => _decisions;

        public TrainingRunEvidenceV1 Capture()
        {
            ThrowIfDisposed();
            var scenario = _director.TrainingScenario;
            return new TrainingRunEvidenceV1(
                scenario.ScenarioId,
                scenario.ContentHash,
                scenario.Context.Seed,
                _timeline,
                _decisions,
                _resolved,
                _winner,
                _resolutionReason);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _director.ReplayDecisionPlanned -= OnDecision;
            _director.ReplayContactAccepted -= OnContact;
            _director.ReceiveOrganizationAuthorityCommitted -= OnGateH;
            _director.GateISetIntentCommitted -= OnGateISetIntent;
            _director.AttackDefenseAuthorityCommitted -= OnGateIContact;
            _director.ReplayNetCrossed -= OnNetCrossing;
            _director.ReplayDefenseAttemptRecorded -= OnDefenseAttempt;
            _director.ReplayRallyResolved -= OnResolved;
            _ball.EnvironmentContact -= OnEnvironmentContact;
        }

        private void OnDecision(ReplayDecisionEvent value)
        {
            var candidates = value.Candidates
                .Select(candidate => new DecisionCandidateSnapshotV1(
                    Stable(candidate.Actor),
                    candidate.IsFeasible,
                    candidate.Score))
                .ToArray();
            var snapshot = new DecisionSnapshotV1(
                _director.TrainingScenario.ScenarioId,
                _director.TrainingScenario.ContentHash,
                _director.MatchContext.RulesVersion,
                _director.CurrentAuthoritySourceSequence,
                value.SimulationTimeSeconds,
                value.Stage,
                value.Team,
                value.AvailableSeconds,
                _ball.State.Position,
                _ball.State.Velocity,
                value.PredictedBallTarget,
                _director.V3CountedHits,
                _director.V3LastCountedActor,
                Stable(value.SelectedPlayer),
                value.SelectedAction,
                candidates);
            _decisions.Add(snapshot);
            Add(
                value.SimulationTimeSeconds,
                TrainingTimelineEventKindV1.Decision,
                value.Team,
                snapshot.SelectedPlayer,
                value.Stage + ":" + snapshot.SelectedPlayer.Value,
                snapshot.SourceSequence,
                snapshot);
        }

        private void OnContact(ReplayContactEvent value)
        {
            var source = value.OrganizationAuthority?.SourceSequence ??
                         value.GateISetIntentAuthority?.SourceSequence ??
                         value.AttackDefenseAuthority?.SourceSequence ?? -1;
            Add(
                value.SimulationTimeSeconds,
                TrainingTimelineEventKindV1.ContactAccepted,
                value.Team,
                value.PlayerId,
                value.Action.ToString(),
                source);
        }

        private void OnGateH(ReceiveOrganizationAuthorityReceipt value)
        {
            Add(
                _ball.SimulationTime,
                TrainingTimelineEventKindV1.GateHAuthority,
                Team(value.Actor),
                value.Actor,
                value.Kind + ":" + value.Branch,
                value.SourceSequence);
        }

        private void OnGateISetIntent(GateISetIntentReceiptV3 value)
        {
            Add(
                _ball.SimulationTime,
                TrainingTimelineEventKindV1.GateISetIntentAuthority,
                Team(value.Intent.Organizer),
                value.Intent.Organizer,
                "SetIntentPlanned",
                value.SourceSequence);
        }

        private void OnGateIContact(AttackDefenseAuthorityReceipt value)
        {
            Add(
                _ball.SimulationTime,
                TrainingTimelineEventKindV1.GateIContactAuthority,
                Team(value.Actor),
                value.Actor,
                value.Kind + ":" + value.Phase,
                value.SourceSequence);
        }

        private void OnEnvironmentContact(EnvironmentCollisionHit value)
        {
            Add(
                _ball.SimulationTime,
                value.Kind == EnvironmentContactKind.Net
                    ? TrainingTimelineEventKindV1.NetContact
                    : TrainingTimelineEventKindV1.GroundContact,
                _director.PossessionTeam ?? TeamId.Blue,
                _director.LastTouchPlayer,
                value.Kind.ToString());
        }

        private void OnNetCrossing(ReplaySimpleEvent value)
        {
            Add(
                value.SimulationTimeSeconds,
                TrainingTimelineEventKindV1.NetCrossing,
                value.Team,
                value.PlayerId,
                value.Kind);
        }

        private void OnDefenseAttempt(ReplayDefenseAttemptEvent value)
        {
            Add(
                value.SimulationTimeSeconds,
                TrainingTimelineEventKindV1.DefenseAttempt,
                value.Team,
                value.WinningActor,
                value.Kind + ":" + value.Reason,
                value.Receipt.SourceSequence);
        }

        private void OnResolved(ReplayRallyResolvedEvent value)
        {
            _resolved = true;
            _winner = value.Team;
            _resolutionReason = value.Reason;
            Add(
                value.SimulationTimeSeconds,
                TrainingTimelineEventKindV1.RallyResolved,
                value.Team,
                value.PlayerId,
                value.Reason);
        }

        private void Add(
            float time,
            TrainingTimelineEventKindV1 kind,
            TeamId team,
            StablePlayerId? player,
            string summary,
            long source = -1,
            DecisionSnapshotV1 decision = null)
        {
            _timeline.Add(new TrainingTimelineEventV1(
                _timeline.Count,
                source,
                time,
                kind,
                team,
                player,
                summary,
                decision));
        }

        private StablePlayerId Stable(PlayerId player)
        {
            return _stableByRuntime.TryGetValue(player, out var stable)
                ? stable
                : throw new InvalidOperationException(
                    "Timeline actor is outside the formal roster.");
        }

        private TeamId Team(StablePlayerId player)
        {
            return _teamByStable.TryGetValue(player, out var team)
                ? team
                : throw new InvalidOperationException(
                    "Timeline actor is outside the formal roster.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(TrainingTimelineRecorderV1));
        }
    }
}
