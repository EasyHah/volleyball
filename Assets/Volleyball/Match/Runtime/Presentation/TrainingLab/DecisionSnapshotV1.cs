using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class DecisionCandidateSnapshotV1
    {
        public DecisionCandidateSnapshotV1(
            StablePlayerId playerId,
            bool isFeasible,
            RallyDecisionScore score)
        {
            if (string.IsNullOrWhiteSpace(playerId.Value))
                throw new ArgumentException(
                    "Candidate player ID is required.",
                    nameof(playerId));
            PlayerId = playerId;
            IsFeasible = isFeasible;
            Reachability = score.Reachability;
            NominalRole = score.NominalRole;
            Approach = score.Approach;
            Angle = score.Angle;
            Total = score.Total;
        }

        public StablePlayerId PlayerId { get; }
        public bool IsFeasible { get; }
        public float Reachability { get; }
        public float NominalRole { get; }
        public float Approach { get; }
        public float Angle { get; }
        public float Total { get; }
    }

    public sealed class DecisionSnapshotV1
    {
        public const int FormatVersion = 1;
        public const string FeatureVersion =
            "training-decision-features-v1";

        private readonly IReadOnlyList<DecisionCandidateSnapshotV1> _candidates;

        public DecisionSnapshotV1(
            string scenarioId,
            string scenarioHash,
            int rulesVersion,
            long sourceSequence,
            float simulationTimeSeconds,
            RallyDecisionStage stage,
            TeamId team,
            float availableSeconds,
            SimVector3 ballPosition,
            SimVector3 ballVelocity,
            SimVector3 predictedBallTarget,
            int countedHits,
            StablePlayerId? lastLegalActor,
            StablePlayerId selectedPlayer,
            TechniqueAction selectedAction,
            IReadOnlyList<DecisionCandidateSnapshotV1> candidates)
        {
            if (string.IsNullOrWhiteSpace(scenarioId) ||
                string.IsNullOrWhiteSpace(scenarioHash) ||
                rulesVersion <= 0 ||
                sourceSequence < 0 ||
                !Finite(simulationTimeSeconds) ||
                !Finite(availableSeconds) ||
                !ballPosition.IsFinite ||
                !ballVelocity.IsFinite ||
                !predictedBallTarget.IsFinite ||
                countedHits < 0 ||
                countedHits > 3 ||
                string.IsNullOrWhiteSpace(selectedPlayer.Value) ||
                !Enum.IsDefined(typeof(RallyDecisionStage), stage) ||
                !Enum.IsDefined(typeof(TeamId), team) ||
                !Enum.IsDefined(typeof(TechniqueAction), selectedAction))
            {
                throw new ArgumentException(
                    "Decision snapshot contains invalid visible state.");
            }

            var copy = (candidates ??
                        throw new ArgumentNullException(nameof(candidates)))
                .Select(value => value ??
                    throw new ArgumentException(
                        "Candidates cannot contain null.",
                        nameof(candidates)))
                .OrderBy(value => value.PlayerId.Value, StringComparer.Ordinal)
                .ToArray();
            if (copy.Length == 0 ||
                !copy.Any(value =>
                    value.PlayerId.Equals(selectedPlayer) &&
                    value.IsFeasible))
            {
                throw new ArgumentException(
                    "The selected player must be a feasible visible candidate.",
                    nameof(candidates));
            }

            ScenarioId = scenarioId;
            ScenarioHash = scenarioHash;
            RulesVersion = rulesVersion;
            SourceSequence = sourceSequence;
            SimulationTimeSeconds = simulationTimeSeconds;
            Stage = stage;
            Team = team;
            AvailableSeconds = availableSeconds;
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            PredictedBallTarget = predictedBallTarget;
            CountedHits = countedHits;
            LastLegalActor = lastLegalActor;
            SelectedPlayer = selectedPlayer;
            SelectedAction = selectedAction;
            _candidates =
                new ReadOnlyCollection<DecisionCandidateSnapshotV1>(copy);
            SnapshotHash = ComputeHash(this);
        }

        public int FormatVersionValue => FormatVersion;
        public string FeatureVersionValue => FeatureVersion;
        public string ScenarioId { get; }
        public string ScenarioHash { get; }
        public int RulesVersion { get; }
        public long SourceSequence { get; }
        public float SimulationTimeSeconds { get; }
        public RallyDecisionStage Stage { get; }
        public TeamId Team { get; }
        public float AvailableSeconds { get; }
        public SimVector3 BallPosition { get; }
        public SimVector3 BallVelocity { get; }
        public SimVector3 PredictedBallTarget { get; }
        public int CountedHits { get; }
        public StablePlayerId? LastLegalActor { get; }
        public StablePlayerId SelectedPlayer { get; }
        public TechniqueAction SelectedAction { get; }
        public IReadOnlyList<DecisionCandidateSnapshotV1> Candidates =>
            _candidates;
        public string SnapshotHash { get; }

        public static byte[] ToCanonicalBytes(DecisionSnapshotV1 value)
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
            Append(output, "stage", (int)value.Stage);
            Append(output, "team", (int)value.Team);
            Append(output, "availableSeconds", value.AvailableSeconds);
            AppendVector(output, "ball.position", value.BallPosition);
            AppendVector(output, "ball.velocity", value.BallVelocity);
            AppendVector(
                output,
                "predictedBallTarget",
                value.PredictedBallTarget);
            Append(output, "countedHits", value.CountedHits);
            Append(
                output,
                "lastLegalActor",
                value.LastLegalActor?.Value ?? string.Empty);
            Append(output, "selectedPlayer", value.SelectedPlayer.Value);
            Append(output, "selectedAction", (int)value.SelectedAction);
            Append(output, "candidateCount", value.Candidates.Count);
            for (var index = 0;
                 index < value.Candidates.Count;
                 index++)
            {
                var candidate = value.Candidates[index];
                var prefix = "candidates." +
                             index.ToString(CultureInfo.InvariantCulture);
                Append(output, prefix + ".playerId", candidate.PlayerId.Value);
                Append(output, prefix + ".feasible", candidate.IsFeasible);
                Append(output, prefix + ".reachability",
                    candidate.Reachability);
                Append(output, prefix + ".nominalRole",
                    candidate.NominalRole);
                Append(output, prefix + ".approach", candidate.Approach);
                Append(output, prefix + ".angle", candidate.Angle);
                Append(output, prefix + ".total", candidate.Total);
            }

            return Encoding.UTF8.GetBytes(output.ToString());
        }

        private static string ComputeHash(DecisionSnapshotV1 value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(ToCanonicalBytes(value));
            var result = new StringBuilder(bytes.Length * 2);
            foreach (var item in bytes)
                result.Append(item.ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static void AppendVector(
            StringBuilder output,
            string key,
            SimVector3 value)
        {
            Append(output, key + ".x", value.X);
            Append(output, key + ".y", value.Y);
            Append(output, key + ".z", value.Z);
        }

        private static void Append(
            StringBuilder output,
            string key,
            object value)
        {
            var text = value switch
            {
                float number => number.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                double number => number.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty
            };
            output.Append(key)
                .Append('=')
                .Append(text.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(text)
                .Append('\n');
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value >= 0f;
        }
    }
}
