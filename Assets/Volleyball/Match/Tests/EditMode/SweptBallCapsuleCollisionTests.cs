using NUnit.Framework;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class SweptBallCapsuleCollisionTests
    {
        private const float Step = 1f / 120f;

        [Test]
        public void TryFindContact_BallCrossesForearmCapsule_ReturnsEarliestHit()
        {
            var ball = AdvanceBall(
                new SimVector3(0f, 2.2f, 0.20f),
                new SimVector3(0f, 0f, -24f));
            var frame = new ContactCapsuleFrame(
                new SimVector3(-0.35f, 2.0f, 0f),
                new SimVector3(0.35f, 2.4f, 0f),
                0.065f);
            var capsule = new ContactCapsuleSnapshot(frame, frame, true, 701);

            var found = SweptBallCapsuleCollision.TryFindContact(
                ball,
                capsule,
                Step,
                out var hit);

            Assert.That(found, Is.True);
            Assert.That(hit.ContactGroupId, Is.EqualTo(701));
            Assert.That(hit.TimeFraction, Is.InRange(0f, 1f));
            Assert.That(hit.Normal.IsFinite, Is.True);
        }

        [Test]
        public void TryFindContact_SideSwipeOnPalmCapsule_IsTwoSided()
        {
            var ball = AdvanceBall(
                new SimVector3(-0.30f, 2.5f, 0f),
                new SimVector3(36f, 0f, 0f));
            var frame = new ContactCapsuleFrame(
                new SimVector3(0f, 2.46f, -0.04f),
                new SimVector3(0f, 2.54f, 0.04f),
                0.11f);

            var found = SweptBallCapsuleCollision.TryFindContact(
                ball,
                new ContactCapsuleSnapshot(frame, frame, true, 702),
                Step,
                out _);

            Assert.That(found, Is.True);
        }

        [Test]
        public void TryFindContact_InactiveCapsule_ReturnsFalse()
        {
            var ball = AdvanceBall(
                new SimVector3(0f, 2.2f, 0.20f),
                new SimVector3(0f, 0f, -24f));
            var frame = new ContactCapsuleFrame(
                new SimVector3(-0.35f, 2.0f, 0f),
                new SimVector3(0.35f, 2.4f, 0f),
                0.065f);

            var found = SweptBallCapsuleCollision.TryFindContact(
                ball,
                new ContactCapsuleSnapshot(frame, frame, false, 703),
                Step,
                out _);

            Assert.That(found, Is.False);
        }

        private static BallState AdvanceBall(SimVector3 position, SimVector3 velocity)
        {
            var ball = new BallState(position, velocity, 0.12f);
            BallIntegrator.Step(ball, Step, new BallSimulationParameters(0f, 1f));
            return ball;
        }
    }
}
