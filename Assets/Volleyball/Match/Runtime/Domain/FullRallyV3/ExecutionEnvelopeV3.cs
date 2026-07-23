using System;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class ExecutionEnvelopeV3 : IEquatable<ExecutionEnvelopeV3>
    {
        public ExecutionEnvelopeV3(
            string version,
            string abilitySnapshotHash,
            string abilitySnapshotProvenance,
            string actionKind,
            string baselineTargetKey,
            string distributionKey,
            string deterministicSampleKey)
        {
            Version = Required(version, nameof(version));
            AbilitySnapshotHash = Required(abilitySnapshotHash, nameof(abilitySnapshotHash));
            AbilitySnapshotProvenance = Required(abilitySnapshotProvenance, nameof(abilitySnapshotProvenance));
            ActionKind = Required(actionKind, nameof(actionKind));
            BaselineTargetKey = Required(baselineTargetKey, nameof(baselineTargetKey));
            DistributionKey = Required(distributionKey, nameof(distributionKey));
            DeterministicSampleKey = Required(deterministicSampleKey, nameof(deterministicSampleKey));
        }

        public string Version { get; }

        public string AbilitySnapshotHash { get; }

        public string AbilitySnapshotProvenance { get; }

        public string ActionKind { get; }

        public string BaselineTargetKey { get; }

        public string DistributionKey { get; }

        public string DeterministicSampleKey { get; }

        public bool Equals(ExecutionEnvelopeV3 other)
        {
            return other != null
                && Version == other.Version
                && AbilitySnapshotHash == other.AbilitySnapshotHash
                && AbilitySnapshotProvenance == other.AbilitySnapshotProvenance
                && ActionKind == other.ActionKind
                && BaselineTargetKey == other.BaselineTargetKey
                && DistributionKey == other.DistributionKey
                && DeterministicSampleKey == other.DeterministicSampleKey;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ExecutionEnvelopeV3);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Version.GetHashCode();
                hashCode = (hashCode * 397) ^ AbilitySnapshotHash.GetHashCode();
                hashCode = (hashCode * 397) ^ AbilitySnapshotProvenance.GetHashCode();
                hashCode = (hashCode * 397) ^ ActionKind.GetHashCode();
                hashCode = (hashCode * 397) ^ BaselineTargetKey.GetHashCode();
                hashCode = (hashCode * 397) ^ DistributionKey.GetHashCode();
                hashCode = (hashCode * 397) ^ DeterministicSampleKey.GetHashCode();
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
