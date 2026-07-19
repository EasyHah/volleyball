using System;
using NUnit.Framework;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class TeamCourtFrameTests
    {
        [TestCase(TeamId.Blue, -1, -4.5f)]
        [TestCase(TeamId.Orange, 1, 4.5f)]
        public void ToLocal_PreservesHorizontalCoordinatesAndMirrorsWorldDepth(
            TeamId team,
            int expectedWorldDepthSign,
            float worldZ)
        {
            var frame = new TeamCourtFrame(team);

            var local = frame.ToLocal(new SimVector3(2.25f, 1.75f, worldZ));

            Assert.That(frame.WorldDepthSign, Is.EqualTo(expectedWorldDepthSign));
            Assert.That(local.X, Is.EqualTo(2.25f));
            Assert.That(local.Y, Is.EqualTo(1.75f));
            Assert.That(local.Z, Is.EqualTo(-expectedWorldDepthSign * worldZ));
        }

        [TestCase(TeamId.Blue)]
        [TestCase(TeamId.Orange)]
        public void ToWorld_InvertsToLocal(TeamId team)
        {
            var frame = new TeamCourtFrame(team);
            var world = new SimVector3(-1.2f, 2.3f, 6.4f);

            Assert.That(frame.ToWorld(frame.ToLocal(world)), Is.EqualTo(world));
            Assert.That(frame.ToWorldDepth(frame.ToLocalDepth(world.Z)), Is.EqualTo(world.Z));
        }

        [TestCase(TeamId.Blue, -3.2f)]
        [TestCase(TeamId.Orange, 3.2f)]
        public void ToLocalDepth_MapsOwnCourtToNegativeDepth(TeamId team, float worldZ)
        {
            Assert.That(new TeamCourtFrame(team).ToLocalDepth(worldZ), Is.LessThan(0f));
        }

        [Test]
        public void Constructor_RejectsUnknownTeam()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamCourtFrame((TeamId)99));
        }

        [Test]
        public void Equality_UsesTeamIdentity()
        {
            Assert.That(new TeamCourtFrame(TeamId.Blue), Is.EqualTo(new TeamCourtFrame(TeamId.Blue)));
            Assert.That(new TeamCourtFrame(TeamId.Blue), Is.Not.EqualTo(new TeamCourtFrame(TeamId.Orange)));
        }
    }
}
