using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class AttackBandPolicyTests
    {
        [TestCase(PlayerRole.Attacker)]
        [TestCase(PlayerRole.OutsideHitter)]
        [TestCase(PlayerRole.Opposite)]
        public void Resolve_UsesStandardNearNetBandForWingAttackers(PlayerRole role)
        {
            var band = AttackBandPolicy.Resolve(role, 1f);

            Assert.That(band.NearDepth, Is.EqualTo(0.75f));
            Assert.That(band.FarDepth, Is.EqualTo(1.50f));
        }

        [Test]
        public void Resolve_UsesMiddleBlockerNearNetBand()
        {
            var band = AttackBandPolicy.Resolve(PlayerRole.MiddleBlocker, 1f);

            Assert.That(band.NearDepth, Is.EqualTo(0.50f));
            Assert.That(band.FarDepth, Is.EqualTo(0.75f));
        }

        [TestCase(6f, 1.75f, 2.50f)]
        [TestCase(7f, 2.25f, 3.00f)]
        [TestCase(9f, 2.25f, 3.00f)]
        public void Resolve_ShiftsAndCapsStandardBandForDeepSetter(
            float setterDepth,
            float expectedNear,
            float expectedFar)
        {
            var band = AttackBandPolicy.Resolve(PlayerRole.Attacker, setterDepth);

            Assert.That(band.NearDepth, Is.EqualTo(expectedNear));
            Assert.That(band.FarDepth, Is.EqualTo(expectedFar));
        }

        [TestCase(1.5f, 0.75f, 1.50f)]
        [TestCase(4f, 0.75f, 1.50f)]
        [TestCase(4.2f, 0.85f, 1.60f)]
        public void Resolve_KeepsStandardBandUntilSetterIsPastFourMetres(
            float setterDepth,
            float expectedNear,
            float expectedFar)
        {
            var band = AttackBandPolicy.Resolve(PlayerRole.Attacker, setterDepth);

            Assert.That(band.NearDepth, Is.EqualTo(expectedNear).Within(0.0001f));
            Assert.That(band.FarDepth, Is.EqualTo(expectedFar).Within(0.0001f));
        }

        [Test]
        public void Resolve_ShiftsMiddleBlockerBandForDeepSetter()
        {
            var band = AttackBandPolicy.Resolve(PlayerRole.MiddleBlocker, 6f);

            Assert.That(band.NearDepth, Is.EqualTo(1.50f).Within(0.0001f));
            Assert.That(band.FarDepth, Is.EqualTo(1.75f).Within(0.0001f));
        }

        [Test]
        public void ConstrainTakeoff_ClampsBlueAttackerDepthAndPreservesX()
        {
            var actualCenter = new SimVector3(1.2f, 3.3f, -3.8f);
            var takeoff = AttackBandPolicy.Resolve(PlayerRole.Attacker, 1f)
                .ConstrainTakeoff(TeamId.Blue, actualCenter);

            Assert.That(takeoff.X, Is.EqualTo(actualCenter.X));
            Assert.That(takeoff.Y, Is.EqualTo(0f));
            Assert.That(takeoff.Z, Is.InRange(-1.50f, -0.75f));
        }

        [Test]
        public void ConstrainTakeoff_ClampsOrangeAttackerDepthAndPreservesX()
        {
            var actualCenter = new SimVector3(-1.2f, 3.3f, 3.8f);
            var takeoff = AttackBandPolicy.Resolve(PlayerRole.Attacker, 1f)
                .ConstrainTakeoff(TeamId.Orange, actualCenter);

            Assert.That(takeoff.X, Is.EqualTo(actualCenter.X));
            Assert.That(takeoff.Y, Is.EqualTo(0f));
            Assert.That(takeoff.Z, Is.InRange(0.75f, 1.50f));
        }

        [Test]
        public void Resolve_RejectsInvalidInputs()
        {
            Assert.That(
                () => AttackBandPolicy.Resolve((PlayerRole)999, 1f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => AttackBandPolicy.Resolve(PlayerRole.Attacker, -0.1f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(
                () => AttackBandPolicy.Resolve(PlayerRole.Attacker, float.NaN),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void ConstrainTakeoff_RejectsInvalidActualCenter()
        {
            var band = AttackBandPolicy.Resolve(PlayerRole.Attacker, 1f);

            Assert.That(
                () => band.ConstrainTakeoff(TeamId.Blue, new SimVector3(float.NaN, 3f, -1f)),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
