using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.PreServe
{
    public sealed class TrainingRallyTimelineEntryV1
    {
        public TrainingRallyTimelineEntryV1(
            int sequence,
            float simulationTime,
            string kind,
            string detail)
        {
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (float.IsNaN(simulationTime) || float.IsInfinity(simulationTime) ||
                simulationTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(simulationTime));
            if (string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException("Timeline kind is required.", nameof(kind));
            Sequence = sequence;
            SimulationTime = simulationTime;
            Kind = kind;
            Detail = detail ?? string.Empty;
        }

        public int Sequence { get; }
        public float SimulationTime { get; }
        public string Kind { get; }
        public string Detail { get; }
    }

    public sealed class TrainingRallyOutcomeV1
    {
        public TrainingRallyOutcomeV1(
            TrainingRallyStartV5 start,
            TeamSide winningSide,
            int homeScoreDelta,
            int awayScoreDelta,
            string completionReason,
            int touchCount,
            IReadOnlyList<PositionFaultV1> positionFaults,
            IReadOnlyList<TrainingRallyTimelineEntryV1> timeline)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));
            if (!Enum.IsDefined(typeof(TeamSide), winningSide))
                throw new ArgumentOutOfRangeException(nameof(winningSide));
            if (homeScoreDelta < 0 || awayScoreDelta < 0 ||
                homeScoreDelta + awayScoreDelta != 1)
                throw new ArgumentException("A training outcome must contain exactly one point.");
            if (string.IsNullOrWhiteSpace(completionReason))
                throw new ArgumentException("Completion reason is required.",
                    nameof(completionReason));
            if (touchCount < 0) throw new ArgumentOutOfRangeException(nameof(touchCount));
            SetupHash = start.SetupHash;
            Seed = start.Seed;
            WinningSide = winningSide;
            HomeScoreDelta = homeScoreDelta;
            AwayScoreDelta = awayScoreDelta;
            CompletionReason = completionReason;
            TouchCount = touchCount;
            PositionFaults = new ReadOnlyCollection<PositionFaultV1>(
                positionFaults == null
                    ? Array.Empty<PositionFaultV1>()
                    : new List<PositionFaultV1>(positionFaults).ToArray());
            Timeline = new ReadOnlyCollection<TrainingRallyTimelineEntryV1>(
                timeline == null
                    ? Array.Empty<TrainingRallyTimelineEntryV1>()
                    : new List<TrainingRallyTimelineEntryV1>(timeline).ToArray());
            OutcomeHash = ComputeHash(this);
        }

        public string SetupHash { get; }
        public int Seed { get; }
        public TeamSide WinningSide { get; }
        public int HomeScoreDelta { get; }
        public int AwayScoreDelta { get; }
        public string CompletionReason { get; }
        public int TouchCount { get; }
        public IReadOnlyList<PositionFaultV1> PositionFaults { get; }
        public IReadOnlyList<TrainingRallyTimelineEntryV1> Timeline { get; }
        public string OutcomeHash { get; }

        private static string ComputeHash(TrainingRallyOutcomeV1 value)
        {
            var text = new StringBuilder(2048)
                .Append("volleyball.training-rally-outcome.v1\n")
                .Append(value.SetupHash).Append('\n')
                .Append(value.Seed).Append('\n')
                .Append((int)value.WinningSide).Append('\n')
                .Append(value.HomeScoreDelta).Append(',')
                .Append(value.AwayScoreDelta).Append('\n')
                .Append(value.CompletionReason).Append('\n')
                .Append(value.TouchCount).Append('\n');
            foreach (var fault in value.PositionFaults)
                text.Append((int)fault.Side).Append(':')
                    .Append((int)fault.Rule).Append(':')
                    .Append(fault.RequiredAheadOrLeft.PlayerId.Value).Append(':')
                    .Append(fault.ViolatingBehindOrRight.PlayerId.Value).Append('\n');
            foreach (var entry in value.Timeline)
                text.Append(entry.Sequence).Append(':')
                    .Append(entry.SimulationTime.ToString("R",
                        CultureInfo.InvariantCulture)).Append(':')
                    .Append(entry.Kind).Append(':').Append(entry.Detail).Append('\n');
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
            var output = new StringBuilder(hash.Length * 2);
            foreach (var item in hash)
                output.Append(item.ToString("x2", CultureInfo.InvariantCulture));
            return output.ToString();
        }
    }
}
