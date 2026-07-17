using System;

namespace Volleyball.Domain.Simulation
{
    public readonly struct BallSimulationParameters
    {
        public BallSimulationParameters(float gravity, float linearDampingPer60Hz)
        {
            if (!IsFinite(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "Gravity must be finite.");
            }

            if (!IsFinite(linearDampingPer60Hz) || linearDampingPer60Hz <= 0f || linearDampingPer60Hz > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(linearDampingPer60Hz),
                    linearDampingPer60Hz,
                    "Linear damping must be finite and in the range (0, 1].");
            }

            Gravity = gravity;
            LinearDampingPer60Hz = linearDampingPer60Hz;
        }

        public float Gravity { get; }

        public float LinearDampingPer60Hz { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public static class BallIntegrator
    {
        public static void Step(BallState state, float deltaSeconds, BallSimulationParameters parameters)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!IsFinite(deltaSeconds) || deltaSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Step duration must be finite and positive.");
            }

            state.PreviousPosition = state.Position;
            var gravityVelocity = state.Velocity + new SimVector3(0f, parameters.Gravity * deltaSeconds, 0f);
            var damping = (float)Math.Pow(parameters.LinearDampingPer60Hz, deltaSeconds * 60f);
            state.Velocity = gravityVelocity * damping;
            state.Position += state.Velocity * deltaSeconds;
            state.CollisionCooldownSeconds = Math.Max(0f, state.CollisionCooldownSeconds - deltaSeconds);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
