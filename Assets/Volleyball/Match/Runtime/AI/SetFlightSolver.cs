using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public enum SetRhythm
    {
        CloseQuick,
        BackQuick,
        FastPin,
        Adjustment,
        HighBall
    }

    public readonly struct SetFlightRequest
    {
        public SetFlightRequest(
            SetRhythm rhythm,
            SimVector3 start,
            SimVector3 target,
            float passQuality,
            float approachReadiness,
            BallSimulationParameters parameters,
            float fixedStepSeconds)
        {
            if (!Enum.IsDefined(typeof(SetRhythm), rhythm))
            {
                throw new ArgumentOutOfRangeException(nameof(rhythm));
            }

            if (!start.IsFinite || !target.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            PassQuality = ValidateUnit(passQuality, nameof(passQuality));
            ApproachReadiness = ValidateUnit(approachReadiness, nameof(approachReadiness));
            if (!IsFinite(fixedStepSeconds) || fixedStepSeconds <= 0f ||
                !IsFinite(parameters.Gravity) ||
                !IsFinite(parameters.LinearDampingPer60Hz) ||
                parameters.LinearDampingPer60Hz <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedStepSeconds));
            }

            Rhythm = rhythm;
            Start = start;
            Target = target;
            Parameters = parameters;
            FixedStepSeconds = fixedStepSeconds;
        }

        public SetRhythm Rhythm { get; }
        public SimVector3 Start { get; }
        public SimVector3 Target { get; }
        public float PassQuality { get; }
        public float ApproachReadiness { get; }
        public BallSimulationParameters Parameters { get; }
        public float FixedStepSeconds { get; }

        private static float ValidateUnit(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct SetFlightSolution
    {
        public SetFlightSolution(
            float flightSeconds,
            int stepCount,
            SimVector3 initialVelocity,
            SimVector3 apex)
        {
            FlightSeconds = flightSeconds;
            StepCount = stepCount;
            InitialVelocity = initialVelocity;
            Apex = apex;
        }

        public float FlightSeconds { get; }
        public int StepCount { get; }
        public SimVector3 InitialVelocity { get; }
        public SimVector3 Apex { get; }
    }

    public static class SetFlightSolver
    {
        public static SetFlightSolution Solve(SetFlightRequest request)
        {
            var bounds = Bounds(request.Rhythm);
            var minimumStep = Math.Max(1, (int)Math.Ceiling(bounds.Minimum / request.FixedStepSeconds));
            var maximumStep = (int)Math.Floor(bounds.Maximum / request.FixedStepSeconds);
            var preferred = PreferredFlightSeconds(
                request.Rhythm,
                request.PassQuality,
                request.ApproachReadiness);
            var found = false;
            var bestDistance = float.MaxValue;
            var best = default(SetFlightSolution);

            for (var stepCount = minimumStep; stepCount <= maximumStep; stepCount++)
            {
                var flightSeconds = stepCount * request.FixedStepSeconds;
                ReturnVelocitySolution velocity;
                try
                {
                    velocity = ReturnVelocitySolver.Solve(
                        request.Start,
                        request.Target,
                        flightSeconds,
                        request.FixedStepSeconds,
                        request.Parameters);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                var state = new BallState(request.Start, velocity.InitialVelocity, 0.12f);
                var apex = state.Position;
                for (var index = 0; index < stepCount; index++)
                {
                    BallIntegrator.Step(state, request.FixedStepSeconds, request.Parameters);
                    if (state.Position.Y > apex.Y)
                    {
                        apex = state.Position;
                    }
                }

                if ((state.Position - request.Target).Magnitude > 0.0002f ||
                    apex.Y <= request.Target.Y + 0.005f ||
                    apex.Y > MaximumApex(request.Rhythm))
                {
                    continue;
                }

                var distance = Math.Abs(flightSeconds - preferred);
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    best = new SetFlightSolution(
                        flightSeconds,
                        stepCount,
                        velocity.InitialVelocity,
                        apex);
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("No physically plausible set flight exists for the requested rhythm.");
            }

            return best;
        }

        public static float PreferredFlightSeconds(
            SetRhythm rhythm,
            float passQuality = 1f,
            float approachReadiness = 1f)
        {
            var bounds = Bounds(rhythm);
            var degradation = ((1f - Clamp01(passQuality)) + (1f - Clamp01(approachReadiness))) * 0.25f;
            return Math.Min(bounds.Maximum, ((bounds.Minimum + bounds.Maximum) * 0.5f) +
                                            ((bounds.Maximum - bounds.Minimum) * degradation));
        }

        private static (float Minimum, float Maximum) Bounds(SetRhythm rhythm)
        {
            return rhythm switch
            {
                SetRhythm.CloseQuick => (0.35f, 0.50f),
                SetRhythm.BackQuick => (0.45f, 0.70f),
                SetRhythm.FastPin => (0.75f, 1.05f),
                SetRhythm.Adjustment => (1.05f, 1.35f),
                SetRhythm.HighBall => (1.30f, 1.80f),
                _ => throw new ArgumentOutOfRangeException(nameof(rhythm))
            };
        }

        private static float MaximumApex(SetRhythm rhythm)
        {
            return rhythm switch
            {
                SetRhythm.CloseQuick => 5.0f,
                SetRhythm.BackQuick => 5.5f,
                SetRhythm.FastPin => 6.0f,
                SetRhythm.Adjustment => 7.0f,
                SetRhythm.HighBall => 8.5f,
                _ => throw new ArgumentOutOfRangeException(nameof(rhythm))
            };
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
