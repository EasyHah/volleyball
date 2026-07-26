using System;
using System.Collections.Generic;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public interface IBallTrajectoryPredictorV4
    {
        string PredictorSource { get; }

        int PredictorVersion { get; }

        string PredictorConfigurationHash { get; }

        TrajectoryPrediction Predict(BallTrajectoryPredictorInputV4 input);
    }

    public sealed class BallTrajectoryPredictionProviderV4
    {
        public const int CurrentPredictorVersion = 4;
        public const string PredictorSource =
            "Volleyball.Domain.Simulation.TrajectoryPredictor";
        public const string DefaultPredictorConfigurationHash =
            "83c6de7001833621977e4765e07da2db87efa1a3f55d121186808bbb1524bb4c";

        private const float FullStepSeconds = 1f / 120f;
        private const float FullMaximumTimeSeconds = 2f;
        private const int FullMaximumSamples = 241;

        private readonly Dictionary<
            BallTrajectoryPredictionCacheKeyV4,
            BallTrajectoryPredictionArtifactV4> _cache;
        private readonly Queue<BallTrajectoryPredictionCacheKeyV4> _insertionOrder;
        private readonly IBallTrajectoryPredictorV4 _predictor;

        public BallTrajectoryPredictionProviderV4(
            TrajectoryPredictionProviderConfigurationV4 configuration)
            : this(configuration, DefaultTrajectoryPredictorV4.Instance)
        {
        }

        public BallTrajectoryPredictionProviderV4(
            TrajectoryPredictionProviderConfigurationV4 configuration,
            IBallTrajectoryPredictorV4 predictor)
        {
            Configuration = configuration ??
                throw new ArgumentNullException(nameof(configuration));
            _predictor = predictor ??
                throw new ArgumentNullException(nameof(predictor));
            if (configuration.CacheEvictionPolicy !=
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut)
            {
                throw new ArgumentException(
                    "Only deterministic FIFO eviction is supported.",
                    nameof(configuration));
            }

            if (configuration.PredictorVersion != predictor.PredictorVersion)
            {
                throw new ArgumentException(
                    "Provider configuration PredictorVersion does not match its predictor strategy.",
                    nameof(configuration));
            }

            if (!string.Equals(
                    configuration.PredictorConfigurationHash,
                    predictor.PredictorConfigurationHash,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Provider configuration PredictorConfigurationHash does not match its predictor strategy.",
                    nameof(configuration));
            }

            if (string.IsNullOrWhiteSpace(predictor.PredictorSource))
            {
                throw new ArgumentException(
                    "Predictor strategy source identity is required.",
                    nameof(predictor));
            }

            _cache = new Dictionary<
                BallTrajectoryPredictionCacheKeyV4,
                BallTrajectoryPredictionArtifactV4>();
            _insertionOrder =
                new Queue<BallTrajectoryPredictionCacheKeyV4>();
        }

        public TrajectoryPredictionProviderConfigurationV4 Configuration { get; }

        public int CacheCapacity => Configuration.CacheCapacity;

        public TrajectoryPredictionCacheEvictionPolicyV4 CacheEvictionPolicy =>
            Configuration.CacheEvictionPolicy;

        public int PredictorVersion => Configuration.PredictorVersion;

        public string PredictorConfigurationHash =>
            Configuration.PredictorConfigurationHash;

        public int CacheCount => _cache.Count;

        public BallTrajectoryPredictionArtifactV4 Predict(
            BallTrajectoryPredictionRequestV4 request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Key.PredictorVersion != PredictorVersion)
            {
                throw new ArgumentException(
                    "Request PredictorVersion does not match the provider configuration.",
                    nameof(request));
            }

            if (!string.Equals(
                    request.Key.PredictorConfigurationHash,
                    PredictorConfigurationHash,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Request PredictorConfigurationHash does not match the provider configuration.",
                    nameof(request));
            }

            if (_cache.TryGetValue(request.Key, out var cached))
            {
                return cached;
            }

            PredictionWorkV4 work = WorkFor(request.DegradationStep);
            var predictorInput = new BallTrajectoryPredictorInputV4(
                request.Key,
                request.Source,
                request.Parameters,
                work.StepSeconds,
                work.MaximumTimeSeconds,
                work.MaximumSamples);
            var prediction = _predictor.Predict(predictorInput);
            var artifact = new BallTrajectoryPredictionArtifactV4(
                request.Key,
                _predictor.PredictorSource,
                prediction);

            if (_cache.Count == CacheCapacity)
            {
                var oldest = _insertionOrder.Dequeue();
                _cache.Remove(oldest);
            }

            _cache.Add(request.Key, artifact);
            _insertionOrder.Enqueue(request.Key);
            return artifact;
        }

        public BallTrajectoryPredictionArtifactV4 PredictWithDegradation(
            BallTrajectoryPredictionRequestV4 request,
            IReadOnlyList<ExecutionDegradationStepV4> degradationLadder)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (degradationLadder == null || degradationLadder.Count == 0)
            {
                throw new ArgumentException(
                    "A deterministic degradation ladder is required.",
                    nameof(degradationLadder));
            }

            Exception lastFailure = null;
            var startingIndex = IndexOf(
                degradationLadder,
                request.DegradationStep);
            if (startingIndex < 0)
            {
                throw new ArgumentException(
                    "The request degradation step is absent from the ladder.",
                    nameof(degradationLadder));
            }

            for (var index = startingIndex;
                 index < degradationLadder.Count;
                 index++)
            {
                var step = degradationLadder[index];
                if (!Enum.IsDefined(typeof(ExecutionDegradationStepV4), step))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(degradationLadder));
                }

                try
                {
                    return Predict(request.WithDegradationStep(step));
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is ArithmeticException ||
                    exception is InvalidOperationException)
                {
                    lastFailure = exception;
                }
            }

            throw new InvalidOperationException(
                "Every deterministic V4 trajectory degradation step failed.",
                lastFailure);
        }

        public bool TryGetCached(
            BallTrajectoryPredictionCacheKeyV4 key,
            out BallTrajectoryPredictionArtifactV4 artifact)
        {
            return _cache.TryGetValue(key, out artifact);
        }

        public static string BuildPhysicsConfigurationHash(
            BallSimulationParameters parameters)
        {
            var canonical =
                "gravity=" + FloatBits(parameters.Gravity) +
                "\nlinearDampingPer60Hz=" +
                FloatBits(parameters.LinearDampingPer60Hz) + "\n";
            return ExecutionEnvelopeCanonicalV4.Sha256(
                System.Text.Encoding.UTF8.GetBytes(canonical));
        }

        private static PredictionWorkV4 WorkFor(
            ExecutionDegradationStepV4 degradationStep)
        {
            switch (degradationStep)
            {
                case ExecutionDegradationStepV4.FullSampling:
                    return new PredictionWorkV4(
                        FullStepSeconds,
                        FullMaximumTimeSeconds,
                        FullMaximumSamples);
                case ExecutionDegradationStepV4.ReducedSampleCount:
                    return new PredictionWorkV4(
                        FullStepSeconds,
                        FullMaximumTimeSeconds,
                        121);
                case ExecutionDegradationStepV4.CachedCoarseDistribution:
                    return new PredictionWorkV4(
                        1f / 60f,
                        FullMaximumTimeSeconds,
                        121);
                case ExecutionDegradationStepV4.DeterministicSafeFallback:
                    return new PredictionWorkV4(
                        1f / 30f,
                        0.25f,
                        9);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(degradationStep));
            }
        }

        private static int IndexOf(
            IReadOnlyList<ExecutionDegradationStepV4> values,
            ExecutionDegradationStepV4 value)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index] == value)
                {
                    return index;
                }
            }

            return -1;
        }

        private static string FloatBits(float value)
        {
            return BitConverter.ToInt32(
                    BitConverter.GetBytes(value),
                    0)
                .ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        }

        private readonly struct PredictionWorkV4
        {
            public PredictionWorkV4(
                float stepSeconds,
                float maximumTimeSeconds,
                int maximumSamples)
            {
                StepSeconds = stepSeconds;
                MaximumTimeSeconds = maximumTimeSeconds;
                MaximumSamples = maximumSamples;
            }

            public float StepSeconds { get; }

            public float MaximumTimeSeconds { get; }

            public int MaximumSamples { get; }
        }

        private sealed class DefaultTrajectoryPredictorV4 :
            IBallTrajectoryPredictorV4
        {
            public static DefaultTrajectoryPredictorV4 Instance { get; } =
                new DefaultTrajectoryPredictorV4();

            private DefaultTrajectoryPredictorV4()
            {
            }

            public string PredictorSource =>
                BallTrajectoryPredictionProviderV4.PredictorSource;

            public int PredictorVersion =>
                BallTrajectoryPredictionProviderV4.CurrentPredictorVersion;

            public string PredictorConfigurationHash =>
                BallTrajectoryPredictionProviderV4
                    .DefaultPredictorConfigurationHash;

            public TrajectoryPrediction Predict(
                BallTrajectoryPredictorInputV4 input)
            {
                if (input == null)
                {
                    throw new ArgumentNullException(nameof(input));
                }

                return TrajectoryPredictor.Predict(
                    new BallState(
                        input.BallPosition,
                        input.BallVelocity,
                        input.BallRadius),
                    input.Parameters,
                    input.StepSeconds,
                    input.MaximumTimeSeconds,
                    input.MaximumSamples);
            }
        }
    }
}
