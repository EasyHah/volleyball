using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VolleyballMatch.Domain.Simulation
{
    public readonly struct TrajectorySample
    {
        public TrajectorySample(float timeSeconds, SimVector3 position, SimVector3 velocity)
        {
            TimeSeconds = timeSeconds;
            Position = position;
            Velocity = velocity;
        }

        public float TimeSeconds { get; }

        public SimVector3 Position { get; }

        public SimVector3 Velocity { get; }
    }

    public readonly struct GroundLanding
    {
        public GroundLanding(float timeSeconds, SimVector3 position)
        {
            TimeSeconds = timeSeconds;
            Position = position;
        }

        public float TimeSeconds { get; }

        public SimVector3 Position { get; }
    }

    public sealed class TrajectoryPrediction
    {
        public TrajectoryPrediction(IReadOnlyList<TrajectorySample> samples, GroundLanding? groundLanding)
        {
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            GroundLanding = groundLanding;
        }

        public IReadOnlyList<TrajectorySample> Samples { get; }

        public GroundLanding? GroundLanding { get; }
    }

    public static class TrajectoryPredictor
    {
        public static TrajectoryPrediction Predict(
            BallState source,
            BallSimulationParameters parameters,
            float stepSeconds,
            float maximumTimeSeconds,
            int maximumSamples,
            float groundHeight = 0f)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!IsFinite(stepSeconds) || stepSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            }

            if (!IsFinite(maximumTimeSeconds) || maximumTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTimeSeconds));
            }

            if (maximumSamples <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSamples));
            }

            if (!IsFinite(groundHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(groundHeight));
            }

            var predictedState = source.Clone();
            var samples = new List<TrajectorySample>(maximumSamples)
            {
                new TrajectorySample(0f, predictedState.Position, predictedState.Velocity)
            };
            var elapsedSeconds = 0f;

            while (samples.Count < maximumSamples && elapsedSeconds < maximumTimeSeconds)
            {
                var deltaSeconds = Math.Min(stepSeconds, maximumTimeSeconds - elapsedSeconds);
                if (deltaSeconds <= 0f)
                {
                    break;
                }

                var previousPosition = predictedState.Position;
                var previousBottom = previousPosition.Y - predictedState.Radius - groundHeight;
                BallIntegrator.Step(predictedState, deltaSeconds, parameters);
                elapsedSeconds += deltaSeconds;
                samples.Add(new TrajectorySample(elapsedSeconds, predictedState.Position, predictedState.Velocity));

                var currentBottom = predictedState.Position.Y - predictedState.Radius - groundHeight;
                if (previousBottom > 0f && currentBottom <= 0f)
                {
                    var alpha = previousBottom / (previousBottom - currentBottom);
                    var landingPosition = SimVector3.Lerp(previousPosition, predictedState.Position, alpha);
                    landingPosition = new SimVector3(
                        landingPosition.X,
                        groundHeight + predictedState.Radius,
                        landingPosition.Z);
                    var landingTime = elapsedSeconds - deltaSeconds + (deltaSeconds * alpha);
                    return new TrajectoryPrediction(
                        new ReadOnlyCollection<TrajectorySample>(samples),
                        new GroundLanding(landingTime, landingPosition));
                }
            }

            return new TrajectoryPrediction(new ReadOnlyCollection<TrajectorySample>(samples), null);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
