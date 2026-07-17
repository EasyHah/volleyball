using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class ArrivalLaunchSolverTests
    {
        [Test]
        public void Solve_ReplaysToRequestedPositionAndArrivalVelocity()
        {
            var target = new SimVector3(1.2f, 2.4f, -3f);
            var arrivalVelocity = new SimVector3(0.5f, -7f, -4f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);
            var solution = ArrivalLaunchSolver.Solve(
                target,
                arrivalVelocity,
                1f,
                1f / 120f,
                parameters);
            var replay = new BallState(solution.StartPosition, solution.InitialVelocity, 0.12f);

            for (var index = 0; index < solution.StepCount; index++)
            {
                BallIntegrator.Step(replay, solution.FixedStepSeconds, parameters);
            }

            Assert.That((replay.Position - target).Magnitude, Is.LessThan(0.0001f));
            Assert.That((replay.Velocity - arrivalVelocity).Magnitude, Is.LessThan(0.0001f));
        }

        [Test]
        public void Solve_RejectsFractionalStepFlightTime()
        {
            Assert.That(
                () => ArrivalLaunchSolver.Solve(
                    SimVector3.Up,
                    SimVector3.Zero,
                    0.81f,
                    1f / 120f,
                    new BallSimulationParameters(-9.8f, 0.9995f)),
                Throws.TypeOf<System.ArgumentException>());
        }
    }
}
