using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Prototype;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public enum TrainingTimelineEventKindV1
    {
        Decision,
        ContactAccepted,
        GateHAuthority,
        GateISetIntentAuthority,
        GateIContactAuthority,
        NetContact,
        NetCrossing,
        GroundContact,
        DefenseAttempt,
        PositionFault,
        RallyResolved
    }

    public sealed class TrainingTimelineEventV1
    {
        public TrainingTimelineEventV1(
            int sequence,
            long authoritySourceSequence,
            float simulationTimeSeconds,
            TrainingTimelineEventKindV1 kind,
            TeamId team,
            StablePlayerId? playerId,
            string summary,
            DecisionSnapshotV1 decision = null)
        {
            if (sequence < 0 ||
                authoritySourceSequence < -1 ||
                float.IsNaN(simulationTimeSeconds) ||
                float.IsInfinity(simulationTimeSeconds) ||
                simulationTimeSeconds < 0f ||
                !Enum.IsDefined(typeof(TrainingTimelineEventKindV1), kind) ||
                !Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Sequence = sequence;
            AuthoritySourceSequence = authoritySourceSequence;
            SimulationTimeSeconds = simulationTimeSeconds;
            Kind = kind;
            Team = team;
            PlayerId = playerId;
            Summary = summary ?? string.Empty;
            Decision = decision;
            if ((kind == TrainingTimelineEventKindV1.Decision) !=
                (decision != null))
            {
                throw new ArgumentException(
                    "Only Decision timeline events carry a decision snapshot.",
                    nameof(decision));
            }
        }

        public int Sequence { get; }
        public long AuthoritySourceSequence { get; }
        public float SimulationTimeSeconds { get; }
        public TrainingTimelineEventKindV1 Kind { get; }
        public TeamId Team { get; }
        public StablePlayerId? PlayerId { get; }
        public string Summary { get; }
        public DecisionSnapshotV1 Decision { get; }
    }

    public sealed class TrainingRunEvidenceV1
    {
        private readonly IReadOnlyList<TrainingTimelineEventV1> _timeline;
        private readonly IReadOnlyList<DecisionSnapshotV1> _decisions;
        private readonly IReadOnlyList<SetterTargetSnapshotV1> _setterTargets;

        public TrainingRunEvidenceV1(
            string scenarioId,
            string scenarioHash,
            int matchSeed,
            IReadOnlyList<TrainingTimelineEventV1> timeline,
            IReadOnlyList<DecisionSnapshotV1> decisions,
            IReadOnlyList<SetterTargetSnapshotV1> setterTargets,
            bool isResolved,
            TeamId? winningTeam,
            string resolutionReason)
        {
            if (string.IsNullOrWhiteSpace(scenarioId) ||
                string.IsNullOrWhiteSpace(scenarioHash))
                throw new ArgumentException(
                    "Run evidence requires scenario provenance.");
            ScenarioId = scenarioId;
            ScenarioHash = scenarioHash;
            MatchSeed = matchSeed;
            _timeline =
                new ReadOnlyCollection<TrainingTimelineEventV1>(
                    (timeline ??
                     throw new ArgumentNullException(nameof(timeline)))
                    .Select(value => value ??
                        throw new ArgumentException(
                            "Timeline values cannot be null.",
                            nameof(timeline)))
                    .ToArray());
            _decisions =
                new ReadOnlyCollection<DecisionSnapshotV1>(
                    (decisions ??
                     throw new ArgumentNullException(nameof(decisions)))
                    .Select(value => value ??
                        throw new ArgumentException(
                            "Decision values cannot be null.",
                            nameof(decisions)))
                    .ToArray());
            _setterTargets =
                new ReadOnlyCollection<SetterTargetSnapshotV1>(
                    (setterTargets ??
                     throw new ArgumentNullException(nameof(setterTargets)))
                    .Select(value => value ??
                        throw new ArgumentException(
                            "Setter target values cannot be null.",
                            nameof(setterTargets)))
                    .ToArray());
            IsResolved = isResolved;
            WinningTeam = winningTeam;
            ResolutionReason = resolutionReason ?? string.Empty;
            if (isResolved != winningTeam.HasValue)
                throw new ArgumentException(
                    "Resolved evidence requires exactly one winning team.");
        }

        public string ScenarioId { get; }
        public string ScenarioHash { get; }
        public int MatchSeed { get; }
        public IReadOnlyList<TrainingTimelineEventV1> Timeline => _timeline;
        public IReadOnlyList<DecisionSnapshotV1> Decisions => _decisions;
        public IReadOnlyList<SetterTargetSnapshotV1> SetterTargets => _setterTargets;
        public bool IsResolved { get; }
        public TeamId? WinningTeam { get; }
        public string ResolutionReason { get; }
    }
}
