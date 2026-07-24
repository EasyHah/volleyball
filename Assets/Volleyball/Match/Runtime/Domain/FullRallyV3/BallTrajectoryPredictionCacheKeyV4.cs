using System;
using System.Globalization;
using System.Text;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public readonly struct BallTrajectoryPredictionCacheKeyV4 :
        IEquatable<BallTrajectoryPredictionCacheKeyV4>
    {
        private readonly byte[] _canonicalBytes;

        public BallTrajectoryPredictionCacheKeyV4(
            long ballStateVersion,
            string ballStateFingerprint,
            string physicsConfigurationHash,
            string samplingKey,
            int predictorVersion,
            string predictorConfigurationHash,
            string envelopeIdentity,
            int degradationStep)
        {
            if (ballStateVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ballStateVersion));
            }

            BallStateVersion = ballStateVersion;
            BallStateFingerprint = RequiredHash(
                ballStateFingerprint,
                nameof(ballStateFingerprint));
            PhysicsConfigurationHash = RequiredHash(
                physicsConfigurationHash,
                nameof(physicsConfigurationHash));
            SamplingKey = Required(samplingKey, nameof(samplingKey));
            if (predictorVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(predictorVersion));
            }

            PredictorVersion = predictorVersion;
            PredictorConfigurationHash = RequiredHash(
                predictorConfigurationHash,
                nameof(predictorConfigurationHash));
            EnvelopeIdentity = RequiredHash(
                envelopeIdentity,
                nameof(envelopeIdentity));
            if (!Enum.IsDefined(
                    typeof(ExecutionDegradationStepV4),
                    degradationStep))
            {
                throw new ArgumentOutOfRangeException(nameof(degradationStep));
            }

            DegradationStep = degradationStep;

            var canonical = new StringBuilder(768);
            Append(canonical, "schema", "volleyball.trajectory-cache-key.v4");
            Append(
                canonical,
                "ballStateVersion",
                BallStateVersion.ToString(CultureInfo.InvariantCulture));
            Append(canonical, "ballStateFingerprint", BallStateFingerprint);
            Append(canonical, "physicsConfigurationHash", PhysicsConfigurationHash);
            Append(canonical, "samplingKey", SamplingKey);
            Append(
                canonical,
                "predictorVersion",
                PredictorVersion.ToString(CultureInfo.InvariantCulture));
            Append(
                canonical,
                "predictorConfigurationHash",
                PredictorConfigurationHash);
            Append(canonical, "envelopeIdentity", EnvelopeIdentity);
            Append(
                canonical,
                "degradationStep",
                DegradationStep.ToString(CultureInfo.InvariantCulture));
            _canonicalBytes = Encoding.UTF8.GetBytes(canonical.ToString());
            Identity = ExecutionEnvelopeCanonicalV4.Sha256(_canonicalBytes);
        }

        public long BallStateVersion { get; }

        public string BallStateFingerprint { get; }

        public string PhysicsConfigurationHash { get; }

        public string SamplingKey { get; }

        public int PredictorVersion { get; }

        public string PredictorConfigurationHash { get; }

        public string EnvelopeIdentity { get; }

        public int DegradationStep { get; }

        public string Identity { get; }

        public byte[] ToCanonicalBytes()
        {
            return _canonicalBytes == null
                ? Array.Empty<byte>()
                : (byte[])_canonicalBytes.Clone();
        }

        public bool Equals(BallTrajectoryPredictionCacheKeyV4 other)
        {
            return BallStateVersion == other.BallStateVersion &&
                string.Equals(
                    BallStateFingerprint,
                    other.BallStateFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    PhysicsConfigurationHash,
                    other.PhysicsConfigurationHash,
                    StringComparison.Ordinal) &&
                string.Equals(SamplingKey, other.SamplingKey, StringComparison.Ordinal) &&
                PredictorVersion == other.PredictorVersion &&
                string.Equals(
                    PredictorConfigurationHash,
                    other.PredictorConfigurationHash,
                    StringComparison.Ordinal) &&
                string.Equals(
                    EnvelopeIdentity,
                    other.EnvelopeIdentity,
                    StringComparison.Ordinal) &&
                DegradationStep == other.DegradationStep;
        }

        public override bool Equals(object obj)
        {
            return obj is BallTrajectoryPredictionCacheKeyV4 other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = BallStateVersion.GetHashCode();
                hash = (hash * 397) ^
                    (BallStateFingerprint == null
                        ? 0
                        : StringComparer.Ordinal.GetHashCode(BallStateFingerprint));
                hash = (hash * 397) ^
                    (PhysicsConfigurationHash == null
                        ? 0
                        : StringComparer.Ordinal.GetHashCode(PhysicsConfigurationHash));
                hash = (hash * 397) ^
                    (SamplingKey == null
                        ? 0
                        : StringComparer.Ordinal.GetHashCode(SamplingKey));
                hash = (hash * 397) ^ PredictorVersion;
                hash = (hash * 397) ^
                    (PredictorConfigurationHash == null
                        ? 0
                        : StringComparer.Ordinal.GetHashCode(PredictorConfigurationHash));
                hash = (hash * 397) ^
                    (EnvelopeIdentity == null
                        ? 0
                        : StringComparer.Ordinal.GetHashCode(EnvelopeIdentity));
                hash = (hash * 397) ^ DegradationStep;
                return hash;
            }
        }

        public static bool operator ==(
            BallTrajectoryPredictionCacheKeyV4 left,
            BallTrajectoryPredictionCacheKeyV4 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            BallTrajectoryPredictionCacheKeyV4 left,
            BallTrajectoryPredictionCacheKeyV4 right)
        {
            return !left.Equals(right);
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Value is required.",
                    parameterName);
            }

            return value;
        }

        private static string RequiredHash(
            string value,
            string parameterName)
        {
            Required(value, parameterName);
            if (value.Length != 64)
            {
                throw new ArgumentException(
                    "Value must be a 64-character SHA-256 identity.",
                    parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    throw new ArgumentException(
                        "Value must use lowercase hexadecimal characters.",
                        parameterName);
                }
            }

            return value;
        }

        private static void Append(
            StringBuilder output,
            string name,
            string value)
        {
            output.Append(name.Length.ToString(CultureInfo.InvariantCulture));
            output.Append(':').Append(name);
            output.Append('=');
            output.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            output.Append(':').Append(value).Append('\n');
        }
    }
}
