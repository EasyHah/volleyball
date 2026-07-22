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
    }
}
