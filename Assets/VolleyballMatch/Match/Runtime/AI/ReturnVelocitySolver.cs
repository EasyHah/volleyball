using System;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.AI
{
    public readonly struct ReturnVelocitySolution
    {
        public ReturnVelocitySolution(SimVector3 initialVelocity, int stepCount, float fixedStepSeconds)
        {
            InitialVelocity = initialVelocity;
            StepCount = stepCount;
            FixedStepSeconds = fixedStepSeconds;
        }

        public SimVector3 InitialVelocity { get; }

        public int StepCount { get; }

        public float FixedStepSeconds { get; }
    }

    public static class ReturnVelocitySolver
    {
        public static ReturnVelocitySolution Solve(
            SimVector3 start,
            SimVector3 target,
            float flightSeconds,
            float fixedStepSeconds,
            BallSimulationParameters parameters)
        {
            if (!start.IsFinite || !target.IsFinite)
            {
                throw new ArgumentOutOfRangeException("Positions must be finite.");
            }

            if (!IsFinite(flightSeconds) || flightSeconds <= 0f ||
                !IsFinite(fixedStepSeconds) || fixedStepSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException("Flight time and fixed step must be finite and positive.");
            }

            var exactSteps = flightSeconds / fixedStepSeconds;
            var stepCount = (int)Math.Round(exactSteps);
            if (stepCount < 1 || Math.Abs(exactSteps - stepCount) > 0.0001f)
            {
                throw new ArgumentException("Flight time must contain a whole number of fixed simulation steps.");
            }

            var baseline = Replay(start, SimVector3.Zero, stepCount, fixedStepSeconds, parameters);
            var unitX = Replay(start, new SimVector3(1f, 0f, 0f), stepCount, fixedStepSeconds, parameters);
            var velocityResponse = unitX.X - baseline.X;
            if (Math.Abs(velocityResponse) <= 0.000001f)
            {
                throw new InvalidOperationException("Simulation parameters produced no usable velocity response.");
            }

            return new ReturnVelocitySolution(
                (target - baseline) / velocityResponse,
                stepCount,
                fixedStepSeconds);
        }

        private static SimVector3 Replay(
            SimVector3 start,
            SimVector3 velocity,
            int stepCount,
            float fixedStepSeconds,
            BallSimulationParameters parameters)
        {
            var state = new BallState(start, velocity, 0.12f);
            for (var index = 0; index < stepCount; index++)
            {
                BallIntegrator.Step(state, fixedStepSeconds, parameters);
            }

            return state.Position;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
