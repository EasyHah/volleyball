using System;

namespace Volleyball.Domain.Simulation
{
    public readonly struct NetPlaneIntercept : IEquatable<NetPlaneIntercept>
    {
        public NetPlaneIntercept(float timeSeconds, SimVector3 point)
        {
            if (!IsFinite(timeSeconds) || timeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            }

            if (!point.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }

            TimeSeconds = timeSeconds;
            Point = point;
        }

        public float TimeSeconds { get; }

        public SimVector3 Point { get; }

        public bool Equals(NetPlaneIntercept other)
        {
            return TimeSeconds.Equals(other.TimeSeconds) && Point.Equals(other.Point);
        }

        public override bool Equals(object obj)
        {
            return obj is NetPlaneIntercept other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (TimeSeconds.GetHashCode() * 397) ^ Point.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public static class NetPlaneInterception
    {
        public static bool TryPredict(
            BallState source,
            BallSimulationParameters parameters,
            float stepSeconds,
            float maxTimeSeconds,
            out NetPlaneIntercept intercept)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ValidatePositiveFinite(stepSeconds, nameof(stepSeconds));
            ValidatePositiveFinite(maxTimeSeconds, nameof(maxTimeSeconds));

            var simulated = source.Clone();
            var elapsed = 0f;
            while (elapsed < maxTimeSeconds)
            {
                var duration = Math.Min(stepSeconds, maxTimeSeconds - elapsed);
                BallIntegrator.Step(simulated, duration, parameters);
                if (TryGetPlaneCrossing(simulated.PreviousPosition, simulated.Position, out var point, out var fraction))
                {
                    intercept = new NetPlaneIntercept(elapsed + (duration * fraction), point);
                    return true;
                }

                elapsed += duration;
            }

            intercept = default;
            return false;
        }

        private static bool TryGetPlaneCrossing(
            SimVector3 previous,
            SimVector3 current,
            out SimVector3 point,
            out float fraction)
        {
            point = SimVector3.Zero;
            fraction = 0f;
            if ((previous.Z < 0f && current.Z < 0f) ||
                (previous.Z > 0f && current.Z > 0f) ||
                previous.Z == current.Z)
            {
                return false;
            }

            fraction = -previous.Z / (current.Z - previous.Z);
            if (fraction < 0f || fraction > 1f)
            {
                return false;
            }

            point = SimVector3.Lerp(previous, current, fraction);
            return true;
        }

        private static void ValidatePositiveFinite(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and positive.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
