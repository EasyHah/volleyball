using NUnit.Framework;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class BallSimulationTests
    {
        [Test]
        public void Step_AppliesGravityThenDampingThenPosition()
        {
            var state = new BallState(
                new SimVector3(0f, 1f, 0f),
                new SimVector3(2f, 0f, 4f),
                0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);

            BallIntegrator.Step(state, 1f / 60f, parameters);

            var expectedVelocity = new SimVector3(
                2f * 0.9995f,
                (-9.8f / 60f) * 0.9995f,
                4f * 0.9995f);
            Assert.That(state.PreviousPosition, Is.EqualTo(new SimVector3(0f, 1f, 0f)));
            Assert.That(state.Velocity.X, Is.EqualTo(expectedVelocity.X).Within(0.000001f));
            Assert.That(state.Velocity.Y, Is.EqualTo(expectedVelocity.Y).Within(0.000001f));
            Assert.That(state.Velocity.Z, Is.EqualTo(expectedVelocity.Z).Within(0.000001f));
            Assert.That(state.Position.X, Is.EqualTo(expectedVelocity.X / 60f).Within(0.000001f));
            Assert.That(state.Position.Y, Is.EqualTo(1f + expectedVelocity.Y / 60f).Within(0.000001f));
            Assert.That(state.Position.Z, Is.EqualTo(expectedVelocity.Z / 60f).Within(0.000001f));
        }

        [Test]
        public void Step_DecreasesCollisionCooldownWithoutGoingNegative()
        {
            var state = new BallState(SimVector3.Zero, SimVector3.Zero, 0.12f);
            state.StartCollisionCooldown(0.08f);

            BallIntegrator.Step(state, 0.03f, new BallSimulationParameters(0f, 1f));
            Assert.That(state.CollisionCooldownSeconds, Is.EqualTo(0.05f).Within(0.000001f));

            BallIntegrator.Step(state, 0.1f, new BallSimulationParameters(0f, 1f));
            Assert.That(state.CollisionCooldownSeconds, Is.Zero);
        }

        [Test]
        public void FixedStepAccumulator_PartitionsFrameTimeWithoutLosingSteps()
        {
            var accumulator = new FixedStepAccumulator(1d / 120d, 4);
            var count = 0;

            accumulator.Advance(1d / 240d, _ => count++);
            accumulator.Advance(1d / 240d, _ => count++);
            Assert.That(count, Is.EqualTo(1));

            accumulator.Advance(8d / 120d, _ => count++);
            Assert.That(count, Is.EqualTo(5));
            accumulator.Advance(0d, _ => count++);
            Assert.That(count, Is.EqualTo(9));
        }

        [Test]
        public void Constructors_RejectInvalidSimulationValues()
        {
            Assert.That(
                () => new BallState(SimVector3.Zero, SimVector3.Zero, 0f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => new BallSimulationParameters(float.NaN, 1f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => new BallSimulationParameters(-9.8f, 0f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => new FixedStepAccumulator(0d, 1),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
