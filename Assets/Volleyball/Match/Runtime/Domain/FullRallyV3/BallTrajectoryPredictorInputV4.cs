using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class BallTrajectoryPredictorInputV4
    {
        internal BallTrajectoryPredictorInputV4(
            BallTrajectoryPredictionCacheKeyV4 key,
            BallState source,
            BallSimulationParameters parameters,
            float stepSeconds,
            float maximumTimeSeconds,
            int maximumSamples)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!string.Equals(
                    BallTrajectoryPredictionRequestV4
                        .BuildBallStateFingerprint(source),
                    key.BallStateFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Source ball state does not match the prediction key.",
                    nameof(source));
            }

            if (!string.Equals(
                    BallTrajectoryPredictionProviderV4
                        .BuildPhysicsConfigurationHash(parameters),
                    key.PhysicsConfigurationHash,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Simulation parameters do not match the prediction key.",
                    nameof(parameters));
            }

            if (!IsFinite(stepSeconds) || stepSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            }

            if (!IsFinite(maximumTimeSeconds) || maximumTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumTimeSeconds));
            }

            if (maximumSamples <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSamples));
            }

            Key = key;
            BallPosition = source.Position;
            BallVelocity = source.Velocity;
            BallRadius = source.Radius;
            Parameters = parameters;
            StepSeconds = stepSeconds;
            MaximumTimeSeconds = maximumTimeSeconds;
            MaximumSamples = maximumSamples;
        }

        public BallTrajectoryPredictionCacheKeyV4 Key { get; }

        public SimVector3 BallPosition { get; }

        public SimVector3 BallVelocity { get; }

        public float BallRadius { get; }

        public BallSimulationParameters Parameters { get; }

        public float StepSeconds { get; }

        public float MaximumTimeSeconds { get; }

        public int MaximumSamples { get; }

        public ExecutionDegradationStepV4 DegradationStep =>
            (ExecutionDegradationStepV4)Key.DegradationStep;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
