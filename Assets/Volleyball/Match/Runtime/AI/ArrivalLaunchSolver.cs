using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public readonly struct ArrivalLaunchSolution
    {
        public ArrivalLaunchSolution(
            SimVector3 startPosition,
            SimVector3 initialVelocity,
            int stepCount,
            float fixedStepSeconds)
        {
            StartPosition = startPosition;
            InitialVelocity = initialVelocity;
            StepCount = stepCount;
            FixedStepSeconds = fixedStepSeconds;
        }

        public SimVector3 StartPosition { get; }

        public SimVector3 InitialVelocity { get; }

        public int StepCount { get; }

        public float FixedStepSeconds { get; }
    }

    public static class ArrivalLaunchSolver
    {
        public static ArrivalLaunchSolution Solve(
            SimVector3 targetPosition,
            SimVector3 arrivalVelocity,
            float flightSeconds,
            float fixedStepSeconds,
            BallSimulationParameters parameters)
        {
            if (!targetPosition.IsFinite || !arrivalVelocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException("Target and velocity must be finite.");
            }

            if (!IsFinite(flightSeconds) || flightSeconds <= 0f ||
                !IsFinite(fixedStepSeconds) || fixedStepSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException("Flight time and step must be finite and positive.");
            }

            var exactSteps = flightSeconds / fixedStepSeconds;
            var stepCount = (int)Math.Round(exactSteps);
            if (stepCount < 1 || Math.Abs(exactSteps - stepCount) > 0.0001f)
            {
                throw new ArgumentException("Flight time must contain a whole number of simulation steps.");
            }

            var damping = (float)Math.Pow(parameters.LinearDampingPer60Hz, fixedStepSeconds * 60f);
            var gravityStep = new SimVector3(0f, parameters.Gravity * fixedStepSeconds, 0f);
            var position = targetPosition;
            var velocity = arrivalVelocity;
            for (var index = 0; index < stepCount; index++)
            {
                position -= velocity * fixedStepSeconds;
                velocity = (velocity / damping) - gravityStep;
            }

            return new ArrivalLaunchSolution(position, velocity, stepCount, fixedStepSeconds);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
