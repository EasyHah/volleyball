namespace Volleyball.Shared.Contracts
{
    public sealed class ReplayTrajectoryCacheKeyRecordV4
    {
        public ReplayTrajectoryCacheKeyRecordV4(
            string identity,
            long ballStateVersion,
            string ballStateFingerprint,
            string physicsConfigurationHash,
            string samplingKey,
            int predictorVersion,
            string predictorConfigurationHash,
            string envelopeIdentity,
            string degradationStep)
        {
            Identity = ReplayContractGuardV4.Hash(identity, nameof(identity));
            if (ballStateVersion < 0)
            {
                throw new ContractValidationException(
                    "ballStateVersion must be non-negative.");
            }

            BallStateVersion = ballStateVersion;
            BallStateFingerprint = ReplayContractGuardV4.Hash(
                ballStateFingerprint,
                nameof(ballStateFingerprint));
            PhysicsConfigurationHash = ReplayContractGuardV4.Hash(
                physicsConfigurationHash,
                nameof(physicsConfigurationHash));
            SamplingKey = ReplayContractGuardV4.Required(
                samplingKey,
                nameof(samplingKey));
            PredictorVersion = ReplayContractGuardV4.Positive(
                predictorVersion,
                nameof(predictorVersion));
            PredictorConfigurationHash = ReplayContractGuardV4.Hash(
                predictorConfigurationHash,
                nameof(predictorConfigurationHash));
            EnvelopeIdentity = ReplayContractGuardV4.Hash(
                envelopeIdentity,
                nameof(envelopeIdentity));
            DegradationStep = ReplayContractGuardV4.DegradationStep(
                degradationStep,
                nameof(degradationStep));
        }

        public string Identity { get; }
        public long BallStateVersion { get; }
        public string BallStateFingerprint { get; }
        public string PhysicsConfigurationHash { get; }
        public string SamplingKey { get; }
        public int PredictorVersion { get; }
        public string PredictorConfigurationHash { get; }
        public string EnvelopeIdentity { get; }
        public string DegradationStep { get; }
    }

    public sealed class ReplayTrajectoryArtifactRecordV4
    {
        public ReplayTrajectoryArtifactRecordV4(
            string artifactIdentity,
            string predictorSource,
            int predictorVersion,
            string predictorConfigurationHash,
            ReplayTrajectoryCacheKeyRecordV4 cacheKey)
        {
            ArtifactIdentity = ReplayContractGuardV4.Hash(
                artifactIdentity,
                nameof(artifactIdentity));
            PredictorSource = ReplayContractGuardV4.Required(
                predictorSource,
                nameof(predictorSource));
            PredictorVersion = ReplayContractGuardV4.Positive(
                predictorVersion,
                nameof(predictorVersion));
            PredictorConfigurationHash = ReplayContractGuardV4.Hash(
                predictorConfigurationHash,
                nameof(predictorConfigurationHash));
            CacheKey = cacheKey ??
                throw new ContractValidationException("cacheKey is required.");
            if (PredictorVersion != CacheKey.PredictorVersion ||
                PredictorConfigurationHash != CacheKey.PredictorConfigurationHash)
            {
                throw new ContractValidationException(
                    "Trajectory provider provenance must match its full cache key.");
            }
        }

        public string ArtifactIdentity { get; }
        public string PredictorSource { get; }
        public int PredictorVersion { get; }
        public string PredictorConfigurationHash { get; }
        public ReplayTrajectoryCacheKeyRecordV4 CacheKey { get; }
    }
}
