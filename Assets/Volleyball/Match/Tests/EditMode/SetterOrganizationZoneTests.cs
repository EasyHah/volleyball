using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class SetterOrganizationZoneTests
    {
        [TestCase(TeamId.Blue, -1.1f)]
        [TestCase(TeamId.Orange, 1.1f)]
        public void DefaultWorldTarget_UsesTheFixedTwoPointFiveOrganizationZone(
            TeamId team,
            float expectedWorldZ)
        {
            var target = SetterOrganizationZone.DefaultWorldTarget(team);

            Assert.That(target.X, Is.EqualTo(1.5f));
            Assert.That(target.Y, Is.EqualTo(0f));
            Assert.That(target.Z, Is.EqualTo(expectedWorldZ));
        }

        [Test]
        public void DefaultLocalTarget_IsOnePointFiveMetresAcrossAndOnePointOneMetresFromNet()
        {
            var target = SetterOrganizationZone.DefaultLocalTarget;

            Assert.That(target.X, Is.EqualTo(1.5f));
            Assert.That(target.Z, Is.EqualTo(-1.1f));
        }

        [TestCase(0.5f, SetterOrganizationZoneGrade.Poor)]
        [TestCase(3f, SetterOrganizationZoneGrade.Secondary)]
        [TestCase(5f, SetterOrganizationZoneGrade.Best)]
        [TestCase(6f, SetterOrganizationZoneGrade.Best)]
        [TestCase(7f, SetterOrganizationZoneGrade.Best)]
        [TestCase(8f, SetterOrganizationZoneGrade.Secondary)]
        [TestCase(8.01f, SetterOrganizationZoneGrade.Poor)]
        public void AssessLocalTarget_UsesExactLateralBoundariesMeasuredFromPositionFourSideline(
            float lateralDistanceFromPositionFourSideline,
            SetterOrganizationZoneGrade expectedGrade)
        {
            var localX = -4.5f + lateralDistanceFromPositionFourSideline;

            var assessment = SetterOrganizationZone.AssessLocalTarget(
                new SimVector3(localX, 2.4f, -1.1f));

            Assert.That(assessment.LateralGrade, Is.EqualTo(expectedGrade));
            Assert.That(assessment.LateralDistanceFromPositionFourSideline,
                Is.EqualTo(lateralDistanceFromPositionFourSideline));
        }

        [TestCase(0f, SetterOrganizationZoneGrade.Best)]
        [TestCase(1.5f, SetterOrganizationZoneGrade.Best)]
        [TestCase(1.5001f, SetterOrganizationZoneGrade.Secondary)]
        [TestCase(4f, SetterOrganizationZoneGrade.Secondary)]
        [TestCase(4.0001f, SetterOrganizationZoneGrade.Poor)]
        public void AssessLocalTarget_UsesExactDepthBoundaries(
            float depthFromNet,
            SetterOrganizationZoneGrade expectedGrade)
        {
            var assessment = SetterOrganizationZone.AssessLocalTarget(
                new SimVector3(1.5f, 2.4f, -depthFromNet));

            Assert.That(assessment.DepthGrade, Is.EqualTo(expectedGrade));
            Assert.That(assessment.DepthFromNet, Is.EqualTo(depthFromNet));
        }

        [TestCase(TeamId.Blue, -2.6f)]
        [TestCase(TeamId.Orange, 2.6f)]
        public void AssessWorldTarget_UsesTheAttackingTeamsMirroredCourtFrame(
            TeamId team,
            float worldZ)
        {
            var assessment = SetterOrganizationZone.AssessWorldTarget(
                team,
                new SimVector3(1.5f, 2.4f, worldZ));

            Assert.That(assessment.LateralGrade, Is.EqualTo(SetterOrganizationZoneGrade.Best));
            Assert.That(assessment.DepthGrade, Is.EqualTo(SetterOrganizationZoneGrade.Secondary));
            Assert.That(assessment.DepthFromNet, Is.EqualTo(2.6f));
        }
    }
}
