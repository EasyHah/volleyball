using System;

namespace Volleyball.Shared.Contracts
{
    public sealed class TrajectoryPredictionProviderConfigurationV5 :
        IEquatable<TrajectoryPredictionProviderConfigurationV5>
    {
        public TrajectoryPredictionProviderConfigurationV5(
            int cacheCapacity,
            TrajectoryPredictionCacheEvictionPolicyV4 cacheEvictionPolicy,
            int predictorVersion,
            string predictorConfigurationHash)
        {
            if (cacheCapacity <= 0 || predictorVersion <= 0)
            {
                throw new ContractValidationException(
                    "V5 trajectory cache capacity and predictor version must be positive.");
            }

            ContractGuard.DefinedEnum(cacheEvictionPolicy, nameof(cacheEvictionPolicy));
            ContractGuard.Hash(predictorConfigurationHash, nameof(predictorConfigurationHash));
            CacheCapacity = cacheCapacity;
            CacheEvictionPolicy = cacheEvictionPolicy;
            PredictorVersion = predictorVersion;
            PredictorConfigurationHash = predictorConfigurationHash;
        }

        public int CacheCapacity { get; }
        public TrajectoryPredictionCacheEvictionPolicyV4 CacheEvictionPolicy { get; }
        public int PredictorVersion { get; }
        public string PredictorConfigurationHash { get; }

        public bool Equals(TrajectoryPredictionProviderConfigurationV5 other)
        {
            return other != null && CacheCapacity == other.CacheCapacity &&
                CacheEvictionPolicy == other.CacheEvictionPolicy &&
                PredictorVersion == other.PredictorVersion && string.Equals(
                    PredictorConfigurationHash, other.PredictorConfigurationHash,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as TrajectoryPredictionProviderConfigurationV5);
        public override int GetHashCode() => HashCode.Combine(CacheCapacity, CacheEvictionPolicy,
            PredictorVersion, PredictorConfigurationHash);
    }
}
