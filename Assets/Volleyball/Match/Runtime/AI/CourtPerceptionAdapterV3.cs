using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.AI
{
    public sealed class CourtPerceptionRequestV3
    {
        public CourtPerceptionRequestV3(string matchSeed, long revision,
            long sourceSequence, TeamSide observingSide, PlayerId observer,
            float observerAwareness, string authoritativeArtifactIdentity,
            SimVector3 ballPosition, IReadOnlyList<PerceivedThreatEntryV3> threats,
            IReadOnlyList<PerceivedSupportCandidateV3> supportCandidates,
            PlayerId conservativeSupport, float simulationTime = 0f)
        {
            if (string.IsNullOrWhiteSpace(matchSeed) || revision < 0 || sourceSequence < 0 ||
                !ballPosition.IsFinite || float.IsNaN(observerAwareness) ||
                float.IsInfinity(observerAwareness) || observerAwareness < 0f ||
                observerAwareness > 1f || string.IsNullOrWhiteSpace(authoritativeArtifactIdentity))
                throw new ArgumentOutOfRangeException(nameof(matchSeed));
            if (float.IsNaN(simulationTime) || float.IsInfinity(simulationTime) ||
                simulationTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(simulationTime));
            MatchSeed = matchSeed; Revision = revision; SourceSequence = sourceSequence;
            ObservingSide = observingSide; Observer = observer;
            ObserverAwareness = observerAwareness;
            AuthoritativeArtifactIdentity = authoritativeArtifactIdentity;
            BallPosition = ballPosition;
            Threats = Copy(threats, nameof(threats));
            SupportCandidates = Copy(supportCandidates, nameof(supportCandidates));
            ConservativeSupport = conservativeSupport;
            SimulationTime = simulationTime;
        }

        public string MatchSeed { get; } public long Revision { get; }
        public long SourceSequence { get; } public TeamSide ObservingSide { get; }
        public PlayerId Observer { get; } public float ObserverAwareness { get; }
        public string AuthoritativeArtifactIdentity { get; } public SimVector3 BallPosition { get; }
        public IReadOnlyList<PerceivedThreatEntryV3> Threats { get; }
        public IReadOnlyList<PerceivedSupportCandidateV3> SupportCandidates { get; }
        public PlayerId ConservativeSupport { get; }
        public float SimulationTime { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, string name)
            where T : class
        {
            if (source == null || source.Any(value => value == null))
                throw new ArgumentException("Public observations are required.", name);
            return new ReadOnlyCollection<T>(source.ToArray());
        }
    }

    public sealed class CourtPerceptionResultV3
    {
        public CourtPerceptionResultV3(TeamPerceptionSnapshotV3 view,
            PerceptionObservationV3<SimVector3> observedBall,
            PerceptionSupportDecisionV3 supportDecision, float recognitionDelaySeconds)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            ObservedBall = observedBall ?? throw new ArgumentNullException(nameof(observedBall));
            SupportDecision = supportDecision ?? throw new ArgumentNullException(nameof(supportDecision));
            RecognitionDelaySeconds = recognitionDelaySeconds;
        }
        public TeamPerceptionSnapshotV3 View { get; }
        public PerceptionObservationV3<SimVector3> ObservedBall { get; }
        public PerceptionSupportDecisionV3 SupportDecision { get; }
        public float RecognitionDelaySeconds { get; }
    }

    public sealed class CourtPerceptionAdapterV3
    {
        private const float ConservativeConfidence = .50f;
        private readonly CourtPerceptionConfigurationV3 _configuration;

        public CourtPerceptionAdapterV3(CourtPerceptionConfigurationV3 configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public CourtPerceptionResultV3 Observe(CourtPerceptionRequestV3 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var awareness = request.ObserverAwareness;
            var delay = Lerp(_configuration.MaximumRecognitionDelay,
                _configuration.MinimumRecognitionDelay, awareness);
            var uncertainty = Lerp(_configuration.MaximumError,
                _configuration.MinimumError, awareness);
            var confidence = Math.Max(0f, Math.Min(1f, 1f -
                ((uncertainty - _configuration.MinimumError) /
                Math.Max(.00001f, _configuration.MaximumError - _configuration.MinimumError))));
            var key = Key(request, "ball");
            var offset = SignedUnit(key) * uncertainty;
            var observedBall = new PerceptionObservationV3<SimVector3>(
                new SimVector3(request.BallPosition.X + offset, request.BallPosition.Y,
                    request.BallPosition.Z - offset), confidence, uncertainty,
                Math.Max(0f, request.SimulationTime - delay), key,
                new[] { request.Observer });
            var players = new[] { new PlayerPerceptionSnapshotV3(request.Observer,
                confidence, delay) };
            var view = new TeamPerceptionSnapshotV3("gate-j-view:" + Key(request, "view"),
                request.AuthoritativeArtifactIdentity, request.ObservingSide, request.Revision,
                request.SourceSequence, players, request.Threats, request.SupportCandidates);
            var conservative = confidence < ConservativeConfidence;
            var selected = conservative ? request.ConservativeSupport : request.SupportCandidates
                .OrderByDescending(candidate => candidate.PerceivedArrivalMargin)
                .ThenByDescending(candidate => candidate.Confidence)
                .ThenByDescending(candidate => candidate.CommittedContinuity)
                .ThenBy(candidate => candidate.PlayerId.Value, StringComparer.Ordinal)
                .First().PlayerId;
            return new CourtPerceptionResultV3(view, observedBall,
                new PerceptionSupportDecisionV3(selected, conservative, confidence), delay);
        }

        private static float Lerp(float maximum, float minimum, float awareness) =>
            maximum + ((minimum - maximum) * awareness);

        private static string Key(CourtPerceptionRequestV3 request, string kind) =>
            request.MatchSeed + ":" + request.Revision + ":" + request.SourceSequence + ":" +
            request.ObservingSide + ":" + request.Observer.Value + ":" + kind;

        private static float SignedUnit(string value)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
                var integer = BitConverter.ToUInt32(bytes, 0);
                return ((integer / (float)uint.MaxValue) * 2f) - 1f;
            }
        }
    }
}
