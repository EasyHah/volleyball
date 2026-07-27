using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class SetFlightSolverTests
    {
        private static readonly BallSimulationParameters Parameters =
            new BallSimulationParameters(-9.8f, 0.9995f);

        [TestCase(SetRhythm.CloseQuick, 0.35f, 0.50f)]
        [TestCase(SetRhythm.BackQuick, 0.45f, 0.70f)]
        [TestCase(SetRhythm.FastPin, 0.75f, 1.05f)]
        [TestCase(SetRhythm.Adjustment, 1.05f, 1.35f)]
        [TestCase(SetRhythm.HighBall, 1.30f, 1.80f)]
        public void Solve_UsesDocumentedRhythmBoundsAndReplaysExactly(
            SetRhythm rhythm,
            float minimum,
            float maximum)
        {
            var request = new SetFlightRequest(
                rhythm,
                new SimVector3(0f, 2.4f, -2f),
                new SimVector3(-3.1f, 3.42f, -2.45f),
                1f,
                1f,
                Parameters,
                1f / 120f);

            var solution = SetFlightSolver.Solve(request);
            var replay = new BallState(request.Start, solution.InitialVelocity, 0.12f);
            for (var index = 0; index < solution.StepCount; index++)
            {
                BallIntegrator.Step(replay, request.FixedStepSeconds, request.Parameters);
            }

            Assert.That(solution.FlightSeconds, Is.InRange(minimum, maximum));
            Assert.That(solution.Apex.Y, Is.GreaterThan(request.Target.Y));
            Assert.That((replay.Position - request.Target).Magnitude, Is.LessThan(0.0002f));
        }

        [Test]
        public void Solve_DegradedReadinessSelectsNoFasterThanFullReadiness()
        {
            var full = SetFlightSolver.Solve(Request(SetRhythm.FastPin, 1f, 1f));
            var degraded = SetFlightSolver.Solve(Request(SetRhythm.FastPin, 0.45f, 0.35f));

            Assert.That(degraded.FlightSeconds, Is.GreaterThanOrEqualTo(full.FlightSeconds));
        }

        [Test]
        public void Solve_RejectsTargetWithoutAPhysicallyPlausibleApex()
        {
            var request = new SetFlightRequest(
                SetRhythm.CloseQuick,
                new SimVector3(0f, 2.4f, -2f),
                new SimVector3(0f, 6.4f, -2f),
                1f,
                1f,
                Parameters,
                1f / 120f);

            Assert.Throws<System.InvalidOperationException>(() => SetFlightSolver.Solve(request));
        }

        private static SetFlightRequest Request(SetRhythm rhythm, float passQuality, float readiness)
        {
            return new SetFlightRequest(
                rhythm,
                new SimVector3(0f, 2.4f, -2f),
                new SimVector3(-3.1f, 3.42f, -2.45f),
                passQuality,
                readiness,
                Parameters,
                1f / 120f);
        }
    }
}
