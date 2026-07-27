using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;

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

        [TestCase(-1)]
        [TestCase(1)]
        public void Solve_FormalFrontRowServeTargetClearsTheNet(int receivingDepthSign)
        {
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);
            var target = new SimVector3(-3f, 1.40f, receivingDepthSign * 1.80f);
            var arrival = new SimVector3(
                0f,
                PhysicalMatchRallyDirector.ServeArrivalVerticalSpeed,
                receivingDepthSign * 9f);
            var solution = ArrivalLaunchSolver.Solve(
                target,
                arrival,
                0.90f,
                SimulatedBall.DefaultFixedStep,
                parameters);
            var ball = new BallState(
                solution.StartPosition,
                solution.InitialVelocity,
                SimulatedBall.DefaultRadius);
            var net = new NetCollisionGeometry(4.5f, 2.48f, 0.08f, 0.15f);

            for (var step = 0; step < solution.StepCount; step++)
            {
                BallIntegrator.Step(ball, solution.FixedStepSeconds, parameters);
                Assert.That(EnvironmentCollision.TryNet(ball, net, out _), Is.False);
            }

            Assert.That((ball.Position - target).Magnitude, Is.LessThan(0.0001f));
        }
    }
}
