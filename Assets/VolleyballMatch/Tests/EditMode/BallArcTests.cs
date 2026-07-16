using System;
using NUnit.Framework;
using VolleyballMatch.Domain.Prototype;

namespace VolleyballMatch.EditModeTests
{
    public sealed class BallArcTests
    {
        [Test]
        public void Evaluate_InterpolatesEndpointsAndParabolicApex()
        {
            var arc = new BallArc(
                new CourtPoint(-2f, -3f),
                new CourtPoint(2f, 3f),
                1.5f,
                2.5f,
                2f);

            Assert.That(arc.Evaluate(0f), Is.EqualTo(new BallArcPoint(-2f, 1.5f, -3f)));
            Assert.That(arc.Evaluate(1f), Is.EqualTo(new BallArcPoint(2f, 2.5f, 3f)));
            Assert.That(arc.Evaluate(0.5f).Y, Is.EqualTo(4f));
        }

        [Test]
        public void Evaluate_ClampsTimeOutsideNormalizedRange()
        {
            var arc = new BallArc(
                new CourtPoint(-2f, -3f),
                new CourtPoint(2f, 3f),
                1.5f,
                2.5f,
                2f);

            Assert.That(arc.Evaluate(-0.5f), Is.EqualTo(new BallArcPoint(-2f, 1.5f, -3f)));
            Assert.That(arc.Evaluate(1.5f), Is.EqualTo(new BallArcPoint(2f, 2.5f, 3f)));
        }

        [Test]
        public void Constructor_RejectsNegativeApexOffset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallArc(
                    new CourtPoint(0f, 0f),
                    new CourtPoint(1f, 1f),
                    1f,
                    1f,
                    -0.01f));
        }

        [Test]
        public void Constructor_RejectsNonFiniteCoordinatesAndHeights()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallArc(
                    new CourtPoint(float.NaN, 0f),
                    new CourtPoint(1f, 1f),
                    1f,
                    1f,
                    1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallArc(
                    new CourtPoint(0f, 0f),
                    new CourtPoint(1f, float.PositiveInfinity),
                    1f,
                    1f,
                    1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallArc(
                    new CourtPoint(0f, 0f),
                    new CourtPoint(1f, 1f),
                    float.NegativeInfinity,
                    1f,
                    1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallArc(
                    new CourtPoint(0f, 0f),
                    new CourtPoint(1f, 1f),
                    1f,
                    float.NaN,
                    1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BallArc(
                    new CourtPoint(0f, 0f),
                    new CourtPoint(1f, 1f),
                    1f,
                    1f,
                    float.PositiveInfinity));
        }

        [Test]
        public void BallArcPoint_HasValueEqualityAndConsistentHashes()
        {
            var point = new BallArcPoint(1f, 2f, 3f);
            var equal = new BallArcPoint(1f, 2f, 3f);

            Assert.That(point.Equals(equal), Is.True);
            Assert.That(point.Equals((object)equal), Is.True);
            Assert.That(point.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(point.Equals(new BallArcPoint(10f, 2f, 3f)), Is.False);
            Assert.That(point.Equals(new BallArcPoint(1f, 20f, 3f)), Is.False);
            Assert.That(point.Equals(new BallArcPoint(1f, 2f, 30f)), Is.False);
        }
    }
}
