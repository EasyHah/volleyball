using NUnit.Framework;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class EnvironmentCollisionTests
    {
        [Test]
        public void TryGround_DetectsFirstRadiusOffsetCrossing()
        {
            var ball = Advance(new SimVector3(0f, 0.4f, 0f), new SimVector3(0f, -40f, 0f));

            var found = EnvironmentCollision.TryGround(ball, 0.15f, out var hit);

            Assert.That(found, Is.True);
            Assert.That(hit.Kind, Is.EqualTo(EnvironmentContactKind.Ground));
            Assert.That(hit.ImpactCenter.Y, Is.EqualTo(0.27f).Within(0.00001f));
            Assert.That(hit.TimeFraction, Is.InRange(0f, 1f));
        }

        [Test]
        public void TryNet_DetectsHighSpeedCrossingButRejectsBallAboveNet()
        {
            var net = new NetCollisionGeometry(4.5f, 2.48f, 0.08f, 0.15f);
            var crossing = Advance(new SimVector3(0f, 1.8f, -0.5f), new SimVector3(0f, 0f, 120f));
            var above = Advance(new SimVector3(0f, 3f, -0.5f), new SimVector3(0f, 0f, 120f));

            Assert.That(EnvironmentCollision.TryNet(crossing, net, out var hit), Is.True);
            Assert.That(hit.Kind, Is.EqualTo(EnvironmentContactKind.Net));
            Assert.That(hit.Normal.Z, Is.LessThan(0f));
            Assert.That(EnvironmentCollision.TryNet(above, net, out _), Is.False);
        }

        [Test]
        public void ApplyResponse_ReflectsNormalAndReducesTangentialVelocity()
        {
            var ball = new BallState(new SimVector3(0f, 0.2f, 0f), new SimVector3(4f, -5f, 2f), 0.12f);
            var hit = new EnvironmentCollisionHit(
                EnvironmentContactKind.Ground,
                0.5f,
                new SimVector3(0f, 0.27f, 0f),
                new SimVector3(0f, 0.15f, 0f),
                SimVector3.Up);

            EnvironmentCollision.ApplyResponse(ball, hit, 0.6f, 0.25f);

            Assert.That(ball.Velocity.X, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(ball.Velocity.Y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(ball.Velocity.Z, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(ball.Position, Is.EqualTo(hit.ImpactCenter));
        }

        private static BallState Advance(SimVector3 start, SimVector3 velocity)
        {
            var ball = new BallState(start, velocity, 0.12f);
            BallIntegrator.Step(ball, 1f / 120f, new BallSimulationParameters(0f, 1f));
            return ball;
        }
    }
}
