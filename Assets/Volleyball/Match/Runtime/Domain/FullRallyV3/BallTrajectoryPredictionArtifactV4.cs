using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class BallTrajectoryPredictionArtifactV4 :
        IEquatable<BallTrajectoryPredictionArtifactV4>
    {
        private readonly byte[] _canonicalBytes;
        private readonly IReadOnlyList<float> _sampleTimestamps;
        private readonly IReadOnlyList<SimVector3> _samplePositions;

        internal BallTrajectoryPredictionArtifactV4(
            BallTrajectoryPredictionCacheKeyV4 key,
            string predictorSource,
            TrajectoryPrediction prediction)
        {
            if (string.IsNullOrWhiteSpace(predictorSource))
            {
                throw new ArgumentException(
                    "Predictor source is required.",
                    nameof(predictorSource));
            }

            Key = key;
            KeyIdentity = key.Identity;
            PredictorSource = predictorSource;
            PredictorVersion = key.PredictorVersion;
            PredictorConfigurationHash = key.PredictorConfigurationHash;
            if (prediction == null)
            {
                throw new ArgumentNullException(nameof(prediction));
            }

            var timestamps = new float[prediction.Samples.Count];
            var positions = new SimVector3[prediction.Samples.Count];
            var samples = new TrajectorySample[prediction.Samples.Count];
            for (var index = 0; index < prediction.Samples.Count; index++)
            {
                samples[index] = prediction.Samples[index];
            }

            PredictionSnapshot = new TrajectoryPrediction(
                new ReadOnlyCollection<TrajectorySample>(samples),
                prediction.GroundLanding);
            var canonical = new StringBuilder(
                1024 + (PredictionSnapshot.Samples.Count * 160));
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "schema",
                "volleyball.trajectory-artifact.v4");
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "keyIdentity",
                KeyIdentity);
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "keyCanonical",
                Convert.ToBase64String(key.ToCanonicalBytes()));
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "predictorSource",
                PredictorSource);
            ExecutionEnvelopeCanonicalV4.AppendInt(
                canonical,
                "predictorVersion",
                PredictorVersion);
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "predictorConfigurationHash",
                PredictorConfigurationHash);
            ExecutionEnvelopeCanonicalV4.AppendInt(
                canonical,
                "samples.count",
                PredictionSnapshot.Samples.Count);
            for (var index = 0;
                 index < PredictionSnapshot.Samples.Count;
                 index++)
            {
                var sample = PredictionSnapshot.Samples[index];
                timestamps[index] = sample.TimeSeconds;
                positions[index] = sample.Position;
                ExecutionEnvelopeCanonicalV4.AppendFloat(
                    canonical,
                    "samples." + index + ".time",
                    sample.TimeSeconds);
                ExecutionEnvelopeCanonicalV4.AppendVector(
                    canonical,
                    "samples." + index + ".position",
                    sample.Position);
                ExecutionEnvelopeCanonicalV4.AppendVector(
                    canonical,
                    "samples." + index + ".velocity",
                    sample.Velocity);
            }

            ExecutionEnvelopeCanonicalV4.AppendInt(
                canonical,
                "groundLanding.present",
                PredictionSnapshot.GroundLanding.HasValue ? 1 : 0);
            if (PredictionSnapshot.GroundLanding.HasValue)
            {
                ExecutionEnvelopeCanonicalV4.AppendFloat(
                    canonical,
                    "groundLanding.time",
                    PredictionSnapshot.GroundLanding.Value.TimeSeconds);
                ExecutionEnvelopeCanonicalV4.AppendVector(
                    canonical,
                    "groundLanding.position",
                    PredictionSnapshot.GroundLanding.Value.Position);
            }

            _sampleTimestamps =
                new ReadOnlyCollection<float>(timestamps);
            _samplePositions =
                new ReadOnlyCollection<SimVector3>(positions);
            _canonicalBytes = Encoding.UTF8.GetBytes(canonical.ToString());
            ArtifactIdentity =
                ExecutionEnvelopeCanonicalV4.Sha256(_canonicalBytes);
        }

        public BallTrajectoryPredictionCacheKeyV4 Key { get; }

        public string KeyIdentity { get; }

        public string PredictorSource { get; }

        public int PredictorVersion { get; }

        public string PredictorConfigurationHash { get; }

        public IReadOnlyList<float> SampleTimestamps => _sampleTimestamps;

        public IReadOnlyList<SimVector3> SamplePositions => _samplePositions;

        public TrajectoryPrediction PredictionSnapshot { get; }

        public string ArtifactIdentity { get; }

        public byte[] ToCanonicalBytes()
        {
            return (byte[])_canonicalBytes.Clone();
        }

        public bool Equals(BallTrajectoryPredictionArtifactV4 other)
        {
            return other != null &&
                string.Equals(
                    ArtifactIdentity,
                    other.ArtifactIdentity,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BallTrajectoryPredictionArtifactV4);
        }

        public override int GetHashCode()
        {
            return ArtifactIdentity.GetHashCode();
        }
    }
}
