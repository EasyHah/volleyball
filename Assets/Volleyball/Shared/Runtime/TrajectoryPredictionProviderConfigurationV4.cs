using System;

namespace Volleyball.Shared.Contracts
{
    public enum TrajectoryPredictionCacheEvictionPolicyV4
    {
        FirstInFirstOut = 0
    }

    public sealed class TrajectoryPredictionProviderConfigurationV4 :
        IEquatable<TrajectoryPredictionProviderConfigurationV4>
    {
        public const int MaximumCacheCapacity = 4096;

        public TrajectoryPredictionProviderConfigurationV4(
            int cacheCapacity,
            TrajectoryPredictionCacheEvictionPolicyV4 cacheEvictionPolicy,
            int predictorVersion,
            string predictorConfigurationHash)
        {
            if (cacheCapacity <= 0 || cacheCapacity > MaximumCacheCapacity)
            {
                throw new ContractValidationException(
                    "cacheCapacity must be in the range [1, " +
                    MaximumCacheCapacity + "].");
            }

            ContractGuard.DefinedEnum(
                cacheEvictionPolicy,
                nameof(cacheEvictionPolicy));
            if (cacheEvictionPolicy !=
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut)
            {
                throw new ContractValidationException(
                    "Only deterministic FIFO trajectory cache eviction is supported.");
            }

            if (predictorVersion <= 0)
            {
                throw new ContractValidationException(
                    "predictorVersion must be positive.");
            }

            ContractGuard.Hash(
                predictorConfigurationHash,
                nameof(predictorConfigurationHash));

            CacheCapacity = cacheCapacity;
            CacheEvictionPolicy = cacheEvictionPolicy;
            PredictorVersion = predictorVersion;
            PredictorConfigurationHash = predictorConfigurationHash;
        }

        public int CacheCapacity { get; }

        public TrajectoryPredictionCacheEvictionPolicyV4 CacheEvictionPolicy { get; }

        public int PredictorVersion { get; }

        public string PredictorConfigurationHash { get; }

        public bool Equals(TrajectoryPredictionProviderConfigurationV4 other)
        {
            return other != null &&
                CacheCapacity == other.CacheCapacity &&
                CacheEvictionPolicy == other.CacheEvictionPolicy &&
                PredictorVersion == other.PredictorVersion &&
                string.Equals(
                    PredictorConfigurationHash,
                    other.PredictorConfigurationHash,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TrajectoryPredictionProviderConfigurationV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CacheCapacity;
                hash = (hash * 397) ^ (int)CacheEvictionPolicy;
                hash = (hash * 397) ^ PredictorVersion;
                hash = (hash * 397) ^ PredictorConfigurationHash.GetHashCode();
                return hash;
            }
        }
    }
}
