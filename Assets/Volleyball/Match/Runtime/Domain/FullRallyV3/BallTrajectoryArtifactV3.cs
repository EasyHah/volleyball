using System;
using System.Collections.Generic;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class BallTrajectoryArtifactV3 : IEquatable<BallTrajectoryArtifactV3>
    {
        private readonly string _ballStateVersion;
        private readonly string _physicsConfigHash;
        private readonly string _sampleKey;
        private readonly string _predictorVersion;
        private readonly string _degradationMode;
        private readonly TrajectoryPrediction _prediction;

        private BallTrajectoryArtifactV3(
            string ballStateVersion,
            string physicsConfigHash,
            string sampleKey,
            string predictorVersion,
            string degradationMode,
            TrajectoryPrediction prediction)
        {
            _ballStateVersion = Required(ballStateVersion, nameof(ballStateVersion));
            _physicsConfigHash = Required(physicsConfigHash, nameof(physicsConfigHash));
            _sampleKey = Required(sampleKey, nameof(sampleKey));
            _predictorVersion = Required(predictorVersion, nameof(predictorVersion));
            _degradationMode = Required(degradationMode, nameof(degradationMode));
            _prediction = prediction;
        }

        public string BallStateVersion => _ballStateVersion;

        public string PhysicsConfigHash => _physicsConfigHash;

        public string SampleKey => _sampleKey;

        public string PredictorVersion => _predictorVersion;

        public string DegradationMode => _degradationMode;

        public TrajectoryPrediction Prediction => _prediction;

        public static BallTrajectoryArtifactV3 CreateIdentity(
            string ballStateVersion,
            string physicsConfigHash,
            string sampleKey,
            string predictorVersion,
            string degradationMode)
        {
            return new BallTrajectoryArtifactV3(
                ballStateVersion,
                physicsConfigHash,
                sampleKey,
                predictorVersion,
                degradationMode,
                null);
        }

        public static BallTrajectoryArtifactV3 CreateWithPrediction(
            string ballStateVersion,
            string physicsConfigHash,
            string sampleKey,
            string predictorVersion,
            string degradationMode,
            TrajectoryPrediction prediction)
        {
            return new BallTrajectoryArtifactV3(
                ballStateVersion,
                physicsConfigHash,
                sampleKey,
                predictorVersion,
                degradationMode,
                prediction);
        }

        public bool Equals(BallTrajectoryArtifactV3 other)
        {
            return other != null
                && BallStateVersion == other.BallStateVersion
                && PhysicsConfigHash == other.PhysicsConfigHash
                && SampleKey == other.SampleKey
                && PredictorVersion == other.PredictorVersion
                && DegradationMode == other.DegradationMode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BallTrajectoryArtifactV3);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = BallStateVersion.GetHashCode();
                hashCode = (hashCode * 397) ^ PhysicsConfigHash.GetHashCode();
                hashCode = (hashCode * 397) ^ SampleKey.GetHashCode();
                hashCode = (hashCode * 397) ^ PredictorVersion.GetHashCode();
                hashCode = (hashCode * 397) ^ DegradationMode.GetHashCode();
                return hashCode;
            }
        }

        private static string Required(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", paramName);
            }

            return value;
        }
    }
}
