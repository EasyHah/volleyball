using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class CourtPerceptionConfigurationV3
    {
        public CourtPerceptionConfigurationV3(string identity, float minimumRecognitionDelay,
            float maximumRecognitionDelay, float minimumError, float maximumError)
        {
            Identity = CourtPerceptionValidationV3.RequireText(identity, nameof(identity));
            MinimumRecognitionDelay = CourtPerceptionValidationV3.RequireNonNegativeFinite(minimumRecognitionDelay, nameof(minimumRecognitionDelay));
            MaximumRecognitionDelay = CourtPerceptionValidationV3.RequireNonNegativeFinite(maximumRecognitionDelay, nameof(maximumRecognitionDelay));
            MinimumError = CourtPerceptionValidationV3.RequireNonNegativeFinite(minimumError, nameof(minimumError));
            MaximumError = CourtPerceptionValidationV3.RequireNonNegativeFinite(maximumError, nameof(maximumError));
            if (MinimumRecognitionDelay > MaximumRecognitionDelay)
                throw new ArgumentOutOfRangeException(nameof(maximumRecognitionDelay));
            if (MinimumError > MaximumError)
                throw new ArgumentOutOfRangeException(nameof(maximumError));
        }

        public string Identity { get; }
        public float MinimumRecognitionDelay { get; }
        public float MaximumRecognitionDelay { get; }
        public float MinimumError { get; }
        public float MaximumError { get; }
    }

    public sealed class PerceptionObservationV3<T>
    {
        public PerceptionObservationV3(T value, float confidence, float uncertainty,
            float observedAtSimulationTime, string uncertaintyKey,
            IReadOnlyList<PlayerId> sources)
        {
            if (ReferenceEquals(value, null)) throw new ArgumentNullException(nameof(value));
            Value = value;
            Confidence = CourtPerceptionValidationV3.RequireUnit(confidence, nameof(confidence));
            Uncertainty = CourtPerceptionValidationV3.RequireNonNegativeFinite(uncertainty, nameof(uncertainty));
            ObservedAtSimulationTime = CourtPerceptionValidationV3.RequireNonNegativeFinite(
                observedAtSimulationTime, nameof(observedAtSimulationTime));
            UncertaintyKey = CourtPerceptionValidationV3.RequireText(uncertaintyKey,
                nameof(uncertaintyKey));
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            var copy = sources.Select(source => PlayerWorldSnapshotV3.RequirePlayerId(source,
                    nameof(sources)))
                .OrderBy(source => source.Value, StringComparer.Ordinal).ToArray();
            if (copy.Select(source => source.Value).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Sources must be distinct.", nameof(sources));
            Sources = new ReadOnlyCollection<PlayerId>(copy);
        }

        public T Value { get; }
        public float Confidence { get; }
        public float Uncertainty { get; }
        public float ObservedAtSimulationTime { get; }
        public string UncertaintyKey { get; }
        public IReadOnlyList<PlayerId> Sources { get; }
    }

    public sealed class PlayerPerceptionSnapshotV3
    {
        public PlayerPerceptionSnapshotV3(PlayerId playerId, float confidence, float recognitionDelay)
        {
            PlayerId = PlayerWorldSnapshotV3.RequirePlayerId(playerId, nameof(playerId));
            Confidence = CourtPerceptionValidationV3.RequireUnit(confidence, nameof(confidence));
            RecognitionDelay = CourtPerceptionValidationV3.RequireNonNegativeFinite(recognitionDelay, nameof(recognitionDelay));
        }

        public PlayerId PlayerId { get; }
        public float Confidence { get; }
        public float RecognitionDelay { get; }
    }

    public sealed class TeamPerceptionSnapshotV3
    {
        public TeamPerceptionSnapshotV3(string viewIdentity, string authoritativeArtifactIdentity,
            TeamSide observingSide, long revision, long sourceSequence,
            IReadOnlyList<PlayerPerceptionSnapshotV3> players,
            IReadOnlyList<PerceivedThreatEntryV3> threats,
            IReadOnlyList<PerceivedSupportCandidateV3> supportCandidates)
        {
            ViewIdentity = CourtPerceptionValidationV3.RequireText(viewIdentity, nameof(viewIdentity));
            AuthoritativeArtifactIdentity = CourtPerceptionValidationV3.RequireText(authoritativeArtifactIdentity, nameof(authoritativeArtifactIdentity));
            ObservingSide = PlayerWorldSnapshotV3.RequireDefinedEnum(observingSide, nameof(observingSide));
            if (revision < 0 || sourceSequence < 0)
                throw new ArgumentOutOfRangeException(revision < 0 ? nameof(revision) : nameof(sourceSequence));
            Revision = revision;
            SourceSequence = sourceSequence;
            Players = CopyDistinct(players, value => value.PlayerId.Value, nameof(players));
            Threats = CopyDistinct(threats, value => value.ThreatIdentity, nameof(threats));
            SupportCandidates = CopyDistinct(supportCandidates, value => value.PlayerId.Value, nameof(supportCandidates));
        }

        public string ViewIdentity { get; }
        public string AuthoritativeArtifactIdentity { get; }
        public TeamSide ObservingSide { get; }
        public long Revision { get; }
        public long SourceSequence { get; }
        public IReadOnlyList<PlayerPerceptionSnapshotV3> Players { get; }
        public IReadOnlyList<PerceivedThreatEntryV3> Threats { get; }
        public IReadOnlyList<PerceivedSupportCandidateV3> SupportCandidates { get; }

        private static IReadOnlyList<T> CopyDistinct<T>(IReadOnlyList<T> source,
            Func<T, string> identity, string parameterName) where T : class
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var copy = source.Select(value => value ?? throw new ArgumentException("Entries cannot be null.", parameterName))
                .OrderBy(identity, StringComparer.Ordinal).ToArray();
            for (var index = 1; index < copy.Length; index++)
                if (string.Equals(identity(copy[index - 1]), identity(copy[index]), StringComparison.Ordinal))
                    throw new ArgumentException("Entries must have distinct identities.", parameterName);
            return new ReadOnlyCollection<T>(copy);
        }
    }

    public sealed class PerceivedThreatEntryV3
    {
        public PerceivedThreatEntryV3(string threatIdentity, string zone, float confidence, float arrivalTime)
        {
            ThreatIdentity = CourtPerceptionValidationV3.RequireText(threatIdentity, nameof(threatIdentity));
            Zone = CourtPerceptionValidationV3.RequireText(zone, nameof(zone));
            Confidence = CourtPerceptionValidationV3.RequireUnit(confidence, nameof(confidence));
            ArrivalTime = CourtPerceptionValidationV3.RequireNonNegativeFinite(arrivalTime, nameof(arrivalTime));
        }

        public string ThreatIdentity { get; }
        public string Zone { get; }
        public float Confidence { get; }
        public float ArrivalTime { get; }
    }

    public sealed class PerceivedSupportCandidateV3
    {
        public PerceivedSupportCandidateV3(PlayerId playerId, float confidence,
            float perceivedArrivalMargin, bool committedContinuity)
        {
            PlayerId = PlayerWorldSnapshotV3.RequirePlayerId(playerId, nameof(playerId));
            Confidence = CourtPerceptionValidationV3.RequireUnit(confidence, nameof(confidence));
            PerceivedArrivalMargin = CourtPerceptionValidationV3.RequireFinite(perceivedArrivalMargin, nameof(perceivedArrivalMargin));
            CommittedContinuity = committedContinuity;
        }

        public PlayerId PlayerId { get; }
        public float Confidence { get; }
        public float PerceivedArrivalMargin { get; }
        public bool CommittedContinuity { get; }
    }

    public sealed class PerceptionSupportDecisionV3
    {
        public PerceptionSupportDecisionV3(PlayerId selectedPlayer, bool isConservativeFallback,
            float confidence)
        {
            SelectedPlayer = PlayerWorldSnapshotV3.RequirePlayerId(selectedPlayer, nameof(selectedPlayer));
            IsConservativeFallback = isConservativeFallback;
            Confidence = CourtPerceptionValidationV3.RequireUnit(confidence, nameof(confidence));
        }

        public PlayerId SelectedPlayer { get; }
        public bool IsConservativeFallback { get; }
        public float Confidence { get; }
    }

    internal static class CourtPerceptionValidationV3
    {
        internal static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Text is required.", parameterName);
            return value;
        }

        internal static float RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        internal static float RequireNonNegativeFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        internal static float RequireUnit(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }
}
