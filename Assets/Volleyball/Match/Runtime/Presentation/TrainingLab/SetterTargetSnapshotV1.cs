using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    // A frozen observation of the legal attackers considered before a set.
    public sealed class SetterTargetSnapshotV1
    {
        public const int FormatVersion = 1;
        public const string FeatureVersion = "setter-target-features-v1";

        private readonly IReadOnlyList<DecisionCandidateSnapshotV1> _candidates;

        public SetterTargetSnapshotV1(
            string scenarioId,
            string scenarioHash,
            int rulesVersion,
            long sourceSequence,
            float simulationTimeSeconds,
            TeamId team,
            int tacticRevision,
            int decisionIndex,
            float availableSeconds,
            SimVector3 ballPosition,
            SimVector3 ballVelocity,
            SimVector3 predictedBallTarget,
            int countedHits,
            StablePlayerId? lastLegalActor,
            StablePlayerId selectedAttacker,
            IReadOnlyList<DecisionCandidateSnapshotV1> candidates)
        {
            if (string.IsNullOrWhiteSpace(scenarioId) ||
                string.IsNullOrWhiteSpace(scenarioHash) ||
                rulesVersion <= 0 ||
                sourceSequence <= 0 ||
                tacticRevision < 0 ||
                decisionIndex < 0 ||
                !Finite(simulationTimeSeconds) ||
                !Finite(availableSeconds) || availableSeconds <= 0f ||
                !ballPosition.IsFinite ||
                !ballVelocity.IsFinite ||
                !predictedBallTarget.IsFinite ||
                countedHits < 0 || countedHits > 3 ||
                !Enum.IsDefined(typeof(TeamId), team) ||
                string.IsNullOrWhiteSpace(selectedAttacker.Value))
            {
                throw new ArgumentException("Setter target snapshot contains invalid visible state.");
            }

            var copy = (candidates ?? throw new ArgumentNullException(nameof(candidates)))
                .Select(candidate => candidate ?? throw new ArgumentException(
                    "Candidates cannot contain null.", nameof(candidates)))
                .OrderBy(candidate => candidate.PlayerId.Value, StringComparer.Ordinal)
                .ToArray();
            if (copy.Select(candidate => candidate.PlayerId).Distinct().Count() != copy.Length)
            {
                throw new ArgumentException(
                    "Candidates must have unique player IDs.", nameof(candidates));
            }

            if (copy.Length == 0 ||
                !copy.Any(candidate => candidate.PlayerId.Equals(selectedAttacker) &&
                                      candidate.IsFeasible))
            {
                throw new ArgumentException(
                    "The selected attacker must be a feasible visible candidate.",
                    nameof(candidates));
            }

            ScenarioId = scenarioId;
            ScenarioHash = scenarioHash;
            RulesVersion = rulesVersion;
            SourceSequence = sourceSequence;
            SimulationTimeSeconds = simulationTimeSeconds;
            Team = team;
            TacticRevision = tacticRevision;
            DecisionIndex = decisionIndex;
            AvailableSeconds = availableSeconds;
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            PredictedBallTarget = predictedBallTarget;
            CountedHits = countedHits;
            LastLegalActor = lastLegalActor;
            SelectedAttacker = selectedAttacker;
            _candidates = new ReadOnlyCollection<DecisionCandidateSnapshotV1>(copy);
            SnapshotHash = ComputeHash(this);
        }

        public int FormatVersionValue => FormatVersion;
        public string FeatureVersionValue => FeatureVersion;
        public string ScenarioId { get; }
        public string ScenarioHash { get; }
        public int RulesVersion { get; }
        public long SourceSequence { get; }
        public float SimulationTimeSeconds { get; }
        public TeamId Team { get; }
        public int TacticRevision { get; }
        public int DecisionIndex { get; }
        public float AvailableSeconds { get; }
        public SimVector3 BallPosition { get; }
        public SimVector3 BallVelocity { get; }
        public SimVector3 PredictedBallTarget { get; }
        public int CountedHits { get; }
        public StablePlayerId? LastLegalActor { get; }
        public StablePlayerId SelectedAttacker { get; }
        public IReadOnlyList<DecisionCandidateSnapshotV1> Candidates => _candidates;
        public string SnapshotHash { get; }

        public static byte[] ToCanonicalBytes(SetterTargetSnapshotV1 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var output = new StringBuilder();
            Append(output, "format", FormatVersion);
            Append(output, "feature", FeatureVersion);
            Append(output, "scenarioId", value.ScenarioId);
            Append(output, "scenarioHash", value.ScenarioHash);
            Append(output, "rulesVersion", value.RulesVersion);
            Append(output, "sourceSequence", value.SourceSequence);
            Append(output, "simulationTime", value.SimulationTimeSeconds);
            Append(output, "team", (int)value.Team);
            Append(output, "tacticRevision", value.TacticRevision);
            Append(output, "decisionIndex", value.DecisionIndex);
            Append(output, "availableSeconds", value.AvailableSeconds);
            AppendVector(output, "ball.position", value.BallPosition);
            AppendVector(output, "ball.velocity", value.BallVelocity);
            AppendVector(output, "predictedBallTarget", value.PredictedBallTarget);
            Append(output, "countedHits", value.CountedHits);
            Append(output, "lastLegalActor", value.LastLegalActor?.Value ?? string.Empty);
            Append(output, "selectedAttacker", value.SelectedAttacker.Value);
            Append(output, "candidateCount", value.Candidates.Count);
            for (var index = 0; index < value.Candidates.Count; index++)
            {
                var candidate = value.Candidates[index];
                var prefix = "candidates." + index.ToString(CultureInfo.InvariantCulture);
                Append(output, prefix + ".playerId", candidate.PlayerId.Value);
                Append(output, prefix + ".feasible", candidate.IsFeasible);
                Append(output, prefix + ".reachability", candidate.Reachability);
                Append(output, prefix + ".nominalRole", candidate.NominalRole);
                Append(output, prefix + ".approach", candidate.Approach);
                Append(output, prefix + ".angle", candidate.Angle);
                Append(output, prefix + ".total", candidate.Total);
            }

            return Encoding.UTF8.GetBytes(output.ToString());
        }

        private static string ComputeHash(SetterTargetSnapshotV1 value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(ToCanonicalBytes(value))
                .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void AppendVector(StringBuilder output, string key, SimVector3 value)
        {
            Append(output, key + ".x", value.X);
            Append(output, key + ".y", value.Y);
            Append(output, key + ".z", value.Z);
        }

        private static void Append(StringBuilder output, string key, object value)
        {
            var text = value switch
            {
                float number => number.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty
            };
            output.Append(key).Append('=').Append(text.Length).Append(':')
                .Append(text).Append('\n');
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
