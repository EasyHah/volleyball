using System;
using NUnit.Framework;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class NetPlaneInterceptionTests
    {
        [Test]
        public void TryPredict_InterpolatesFirstForwardCrossingAndLeavesSourceUnchanged()
        {
            var source = new BallState(
                new SimVector3(1f, 2f, -1f),
                new SimVector3(4f, 0f, 4f),
                0.12f);

            var found = NetPlaneInterception.TryPredict(
                source,
                new BallSimulationParameters(0f, 1f),
                0.5f,
                1f,
                out var intercept);

            Assert.That(found, Is.True);
            Assert.That(intercept.TimeSeconds, Is.EqualTo(0.25f).Within(0.00001f));
            Assert.That(intercept.Point.X, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(intercept.Point.Y, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(intercept.Point.Z, Is.Zero);
            Assert.That(source.Position, Is.EqualTo(new SimVector3(1f, 2f, -1f)));
            Assert.That(source.PreviousPosition, Is.EqualTo(new SimVector3(1f, 2f, -1f)));
            Assert.That(source.Velocity, Is.EqualTo(new SimVector3(4f, 0f, 4f)));
        }

        [Test]
        public void TryPredict_InterpolatesBackwardCrossing()
        {
            var source = new BallState(
                new SimVector3(-2f, 3f, 1f),
                new SimVector3(2f, 0f, -2f),
                0.12f);

            var found = NetPlaneInterception.TryPredict(
                source,
                new BallSimulationParameters(0f, 1f),
                1f,
                1f,
                out var intercept);

            Assert.That(found, Is.True);
            Assert.That(intercept.TimeSeconds, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(intercept.Point, Is.EqualTo(new SimVector3(-1f, 3f, 0f)));
        }

        [Test]
        public void TryPredict_ReturnsFalseWhenTheBallDoesNotReachTheNetPlane()
        {
            var source = new BallState(
                new SimVector3(0f, 2f, -2f),
                new SimVector3(1f, 0f, 1f),
                0.12f);

            var found = NetPlaneInterception.TryPredict(
                source,
                new BallSimulationParameters(0f, 1f),
                0.25f,
                1f,
                out _);

            Assert.That(found, Is.False);
        }

        [Test]
        public void TryPredict_RejectsNullAndNonPositiveOrNonFiniteDurations()
        {
            var source = new BallState(SimVector3.Zero, SimVector3.Zero, 0.12f);
            var parameters = new BallSimulationParameters(0f, 1f);

            Assert.That(
                () => NetPlaneInterception.TryPredict(null, parameters, 0.1f, 1f, out _),
                Throws.ArgumentNullException);
            Assert.That(
                () => NetPlaneInterception.TryPredict(source, parameters, 0f, 1f, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => NetPlaneInterception.TryPredict(source, parameters, 0.1f, float.NaN, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
