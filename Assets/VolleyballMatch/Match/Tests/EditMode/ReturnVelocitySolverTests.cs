using NUnit.Framework;
using VolleyballMatch.AI;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.EditModeTests
{
    public sealed class ReturnVelocitySolverTests
    {
        [Test]
        public void Solve_ReachesTargetWhenReplayedThroughRuntimeIntegrator()
        {
            var start = new SimVector3(-2f, 1.2f, -5f);
            var target = new SimVector3(3f, 2.7f, 3f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);
            var solution = ReturnVelocitySolver.Solve(start, target, 0.8f, 1f / 120f, parameters);
            var replay = new BallState(start, solution.InitialVelocity, 0.12f);

            for (var index = 0; index < solution.StepCount; index++)
            {
                BallIntegrator.Step(replay, solution.FixedStepSeconds, parameters);
            }

            Assert.That((replay.Position - target).Magnitude, Is.LessThan(0.0002f));
        }

        [Test]
        public void Solve_RejectsFlightTimeThatCannotBeRepresentedByWholeSteps()
        {
            Assert.That(
                () => ReturnVelocitySolver.Solve(
                    SimVector3.Zero,
                    SimVector3.Up,
                    0.81f,
                    1f / 120f,
                    new BallSimulationParameters(-9.8f, 0.9995f)),
                Throws.TypeOf<System.ArgumentException>());
        }
    }
}
