using System;

namespace Volleyball.Domain.Simulation
{
    public sealed class FixedStepAccumulator
    {
        private readonly double _fixedStepSeconds;
        private readonly int _maximumStepsPerAdvance;
        private double _accumulatedSeconds;

        public FixedStepAccumulator(double fixedStepSeconds, int maximumStepsPerAdvance)
        {
            if (!IsFinite(fixedStepSeconds) || fixedStepSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixedStepSeconds),
                    fixedStepSeconds,
                    "Fixed step must be finite and positive.");
            }

            if (maximumStepsPerAdvance <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumStepsPerAdvance),
                    maximumStepsPerAdvance,
                    "Maximum steps must be positive.");
            }

            _fixedStepSeconds = fixedStepSeconds;
            _maximumStepsPerAdvance = maximumStepsPerAdvance;
        }

        public double FixedStepSeconds => _fixedStepSeconds;

        public double AccumulatedSeconds => _accumulatedSeconds;

        public double InterpolationAlpha => Math.Min(1d, _accumulatedSeconds / _fixedStepSeconds);

        public int Advance(double elapsedSeconds, Action<float> step)
        {
            if (!IsFinite(elapsedSeconds) || elapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds),
                    elapsedSeconds,
                    "Elapsed time must be finite and non-negative.");
            }

            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            _accumulatedSeconds += elapsedSeconds;
            var completedSteps = 0;
            var epsilon = _fixedStepSeconds * 1e-9d;
            while (_accumulatedSeconds + epsilon >= _fixedStepSeconds
                   && completedSteps < _maximumStepsPerAdvance)
            {
                step((float)_fixedStepSeconds);
                _accumulatedSeconds -= _fixedStepSeconds;
                if (_accumulatedSeconds < 0d && _accumulatedSeconds > -epsilon)
                {
                    _accumulatedSeconds = 0d;
                }

                completedSteps++;
            }

            return completedSteps;
        }

        public void Reset()
        {
            _accumulatedSeconds = 0d;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
