using NUnit.Framework;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.EditModeTests
{
    public sealed class SweptBallCollisionTests
    {
        private const float Step = 1f / 120f;

        [Test]
        public void TryFindContact_DetectsFastFrontFaceCrossing()
        {
            var ball = AdvanceBall(new SimVector3(0f, 1f, 0f), new SimVector3(0f, -180f, 0f));

            var found = SweptBallCollision.TryFindContact(ball, StaticSurface(7), Step, out var hit);

            Assert.That(found, Is.True);
            Assert.That(hit.TimeFraction, Is.InRange(0f, 1f));
            Assert.That(hit.ContactPoint.Y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(hit.ImpactCenter.Y, Is.EqualTo(ball.Radius).Within(0.00001f));
            Assert.That(hit.ContactGroupId, Is.EqualTo(7));
        }

        [Test]
        public void TryFindContact_AcceptsRadiusExpandedEdgeButRejectsOutside()
        {
            var edgeBall = AdvanceBall(new SimVector3(0.6f, 0.3f, 0f), new SimVector3(0f, -40f, 0f));
            var outsideBall = AdvanceBall(new SimVector3(0.7f, 0.3f, 0f), new SimVector3(0f, -40f, 0f));

            Assert.That(SweptBallCollision.TryFindContact(edgeBall, StaticSurface(1), Step, out _), Is.True);
            Assert.That(SweptBallCollision.TryFindContact(outsideBall, StaticSurface(1), Step, out _), Is.False);
        }

        [Test]
        public void TryFindContact_RejectsBallLeavingFromBackFace()
        {
            var ball = AdvanceBall(new SimVector3(0f, -0.2f, 0f), new SimVector3(0f, -20f, 0f));

            Assert.That(SweptBallCollision.TryFindContact(ball, StaticSurface(2), Step, out _), Is.False);
        }

        [Test]
        public void TryFindContact_UsesMovingSurfaceAndReportsItsVelocity()
        {
            var ball = AdvanceBall(new SimVector3(0f, 0.25f, 0f), SimVector3.Zero);
            var previous = Frame(new SimVector3(0f, 0f, 0f));
            var current = Frame(new SimVector3(0f, 0.2f, 0f));
            var surface = new ContactSurfaceSnapshot(previous, current, true, 3);

            var found = SweptBallCollision.TryFindContact(ball, surface, Step, out var hit);

            Assert.That(found, Is.True);
            Assert.That(hit.SurfaceVelocity.Y, Is.EqualTo(24f).Within(0.0001f));
        }

        [Test]
        public void TryFindContact_RejectsInactiveAndCoolingContactGroup()
        {
            var ball = AdvanceBall(new SimVector3(0f, 0.3f, 0f), new SimVector3(0f, -40f, 0f));
            ball.RegisterContact(9, 0.08f);

            Assert.That(SweptBallCollision.TryFindContact(ball, StaticSurface(9), Step, out _), Is.False);
            Assert.That(
                SweptBallCollision.TryFindContact(ball, new ContactSurfaceSnapshot(Frame(SimVector3.Zero), Frame(SimVector3.Zero), false, 4), Step, out _),
                Is.False);
            Assert.That(SweptBallCollision.TryFindContact(ball, StaticSurface(10), Step, out _), Is.True);
        }

        [Test]
        public void ContactSurfaceFrame_RejectsDegenerateBasis()
        {
            Assert.That(
                () => new ContactSurfaceFrame(
                    SimVector3.Zero,
                    SimVector3.Up,
                    SimVector3.Up,
                    new SimVector3(0f, 0f, 1f),
                    1f,
                    1f),
                Throws.TypeOf<System.ArgumentException>());
        }

        private static BallState AdvanceBall(SimVector3 position, SimVector3 velocity)
        {
            var ball = new BallState(position, velocity, 0.12f);
            BallIntegrator.Step(ball, Step, new BallSimulationParameters(0f, 1f));
            return ball;
        }

        private static ContactSurfaceSnapshot StaticSurface(int groupId)
        {
            var frame = Frame(SimVector3.Zero);
            return new ContactSurfaceSnapshot(frame, frame, true, groupId);
        }

        private static ContactSurfaceFrame Frame(SimVector3 origin)
        {
            return new ContactSurfaceFrame(
                origin,
                SimVector3.Up,
                new SimVector3(1f, 0f, 0f),
                new SimVector3(0f, 0f, 1f),
                1f,
                1f);
        }
    }
}
