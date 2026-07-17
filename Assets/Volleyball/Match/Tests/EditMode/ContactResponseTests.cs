using NUnit.Framework;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class ContactResponseTests
    {
        [Test]
        public void Apply_ReflectsRelativeVelocityAndTransfersMovingSurfaceVelocity()
        {
            var ball = new BallState(new SimVector3(0f, 0.1f, 0f), new SimVector3(0f, -5f, 0f), 0.12f);
            var hit = new SweptBallHit(
                0.5f,
                new SimVector3(0f, 0.12f, 0f),
                SimVector3.Zero,
                SimVector3.Up,
                new SimVector3(0f, 2f, 0f),
                12,
                1f);
            var parameters = new ContactResponseParameters(0.8f, 1f, 0f, 0.08f);

            var result = ContactResponse.Apply(ball, hit, parameters);

            Assert.That(result.PhysicalOutgoing.Y, Is.EqualTo(7.6f).Within(0.0001f));
            Assert.That(ball.Velocity.Y, Is.EqualTo(7.6f).Within(0.0001f));
            Assert.That(ball.Position.Y, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(ball.LastContactGroupId, Is.EqualTo(12));
            Assert.That(ball.CollisionCooldownSeconds, Is.EqualTo(0.08f).Within(0.0001f));
        }

        [Test]
        public void Apply_UsesSurfaceFrictionOnTangentialRelativeVelocity()
        {
            var ball = new BallState(new SimVector3(0f, 0.12f, 0f), new SimVector3(4f, -5f, 0f), 0.12f);
            var hit = new SweptBallHit(
                1f,
                ball.Position,
                SimVector3.Zero,
                SimVector3.Up,
                SimVector3.Zero,
                1,
                1f);

            ContactResponse.Apply(ball, hit, new ContactResponseParameters(0.8f, 1f, 0.25f, 0.08f));

            Assert.That(ball.Velocity.X, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(ball.Velocity.Y, Is.EqualTo(4f).Within(0.0001f));
        }
    }
}
