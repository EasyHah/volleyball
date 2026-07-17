using System;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Prototype;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class BallFlightTests
    {
        [Test]
        public void Play_RejectsNonPositiveAndNonFiniteDurationImmediately()
        {
            var gameObject = new GameObject("BallFlightTests");
            var flight = gameObject.AddComponent<BallFlight>();
            var arc = new BallArc(
                new CourtPoint(0f, 0f),
                new CourtPoint(1f, 1f),
                1f,
                1f,
                1f);

            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => flight.Play(arc, 0f, null));
                Assert.Throws<ArgumentOutOfRangeException>(() => flight.Play(arc, -1f, null));
                Assert.Throws<ArgumentOutOfRangeException>(() => flight.Play(arc, float.NaN, null));
                Assert.Throws<ArgumentOutOfRangeException>(() => flight.Play(arc, float.PositiveInfinity, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
