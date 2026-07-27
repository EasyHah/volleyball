using System;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class BallTrajectoryArtifactV3 : IEquatable<BallTrajectoryArtifactV3>
    {
        private BallTrajectoryArtifactV3(
            string ballStateVersion,
            string physicsConfigHash,
            string sampleKey,
            string predictorVersion,
            string degradationMode)
        {
            BallStateVersion = Required(ballStateVersion, nameof(ballStateVersion));
            PhysicsConfigHash = Required(physicsConfigHash, nameof(physicsConfigHash));
            SampleKey = Required(sampleKey, nameof(sampleKey));
            PredictorVersion = Required(predictorVersion, nameof(predictorVersion));
            DegradationMode = Required(degradationMode, nameof(degradationMode));
        }

        public string BallStateVersion { get; }

        public string PhysicsConfigHash { get; }

        public string SampleKey { get; }

        public string PredictorVersion { get; }

        public string DegradationMode { get; }

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
                degradationMode);
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
