using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.Editor
{
    public static class TrainingDecisionSnapshotExporterV1
    {
        public static string Export(
            TrainingRunEvidenceV1 evidence,
            string projectRoot = null)
        {
            if (evidence == null)
                throw new ArgumentNullException(nameof(evidence));
            var root = string.IsNullOrWhiteSpace(projectRoot)
                ? Directory.GetCurrentDirectory()
                : projectRoot;
            var directory = Path.Combine(
                root,
                "TestResults",
                "TrainingLab",
                "DecisionSnapshots");
            Directory.CreateDirectory(directory);
            var safeId = new string(evidence.ScenarioId
                .Select(character =>
                    char.IsLetterOrDigit(character) ||
                    character == '-' ||
                    character == '_'
                        ? character
                        : '-')
                .ToArray());
            var path = Path.Combine(
                directory,
                safeId + "-" +
                evidence.ScenarioHash.Substring(0, 12) +
                ".jsonl");
            File.WriteAllLines(
                path,
                evidence.Decisions.Select(Serialize),
                new UTF8Encoding(false));
            return path;
        }

        private static string Serialize(DecisionSnapshotV1 value)
        {
            var output = new StringBuilder();
            output.Append('{');
            Field(output, "formatVersion", value.FormatVersionValue);
            Field(output, "featureVersion", value.FeatureVersionValue);
            Field(output, "scenarioId", value.ScenarioId);
            Field(output, "scenarioHash", value.ScenarioHash);
            Field(output, "snapshotHash", value.SnapshotHash);
            Field(output, "rulesVersion", value.RulesVersion);
            Field(output, "sourceSequence", value.SourceSequence);
            Field(output, "simulationTimeSeconds",
                value.SimulationTimeSeconds);
            Field(output, "stage", value.Stage.ToString());
            Field(output, "team", value.Team.ToString());
            Field(output, "availableSeconds", value.AvailableSeconds);
            Field(output, "countedHits", value.CountedHits);
            Field(
                output,
                "lastLegalActor",
                value.LastLegalActor?.Value ?? string.Empty);
            Field(output, "selectedPlayer", value.SelectedPlayer.Value);
            Field(output, "selectedAction", value.SelectedAction.ToString());
            Vector(output, "ballPosition", value.BallPosition);
            Vector(output, "ballVelocity", value.BallVelocity);
            Vector(
                output,
                "predictedBallTarget",
                value.PredictedBallTarget);
            output.Append("\"candidates\":[");
            for (var index = 0;
                 index < value.Candidates.Count;
                 index++)
            {
                if (index > 0) output.Append(',');
                var candidate = value.Candidates[index];
                output.Append('{');
                Field(output, "playerId", candidate.PlayerId.Value);
                Field(output, "isFeasible", candidate.IsFeasible);
                Field(output, "reachability", candidate.Reachability);
                Field(output, "nominalRole", candidate.NominalRole);
                Field(output, "approach", candidate.Approach);
                Field(output, "angle", candidate.Angle);
                LastField(output, "total", candidate.Total);
                output.Append('}');
            }

            output.Append("]}");
            return output.ToString();
        }

        private static void Vector(
            StringBuilder output,
            string name,
            Volleyball.Domain.Simulation.SimVector3 value)
        {
            output.Append('"').Append(Escape(name)).Append("\":{");
            Field(output, "x", value.X);
            Field(output, "y", value.Y);
            LastField(output, "z", value.Z);
            output.Append("},");
        }

        private static void Field(
            StringBuilder output,
            string name,
            object value)
        {
            Write(output, name, value);
            output.Append(',');
        }

        private static void LastField(
            StringBuilder output,
            string name,
            object value)
        {
            Write(output, name, value);
        }

        private static void Write(
            StringBuilder output,
            string name,
            object value)
        {
            output.Append('"').Append(Escape(name)).Append("\":");
            switch (value)
            {
                case string text:
                    output.Append('"').Append(Escape(text)).Append('"');
                    break;
                case bool flag:
                    output.Append(flag ? "true" : "false");
                    break;
                case float number:
                    output.Append(number.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                    break;
                case double number:
                    output.Append(number.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                    break;
                case IFormattable formattable:
                    output.Append(formattable.ToString(
                        null,
                        CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new ArgumentException(
                        "Unsupported snapshot JSON value.",
                        nameof(value));
            }
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
