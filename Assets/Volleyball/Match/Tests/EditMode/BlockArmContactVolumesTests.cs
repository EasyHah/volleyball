using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class BlockArmContactVolumesTests
    {
        [Test]
        public void Capture_BuildsSixVolumesFromVisibleArmJointsWithOneGroup()
        {
            var player = new GameObject("BlockArmVolumes");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.blue, "4");
                rig.SetPose(StickFigurePose.Block, 1f);

                var volumes = new BlockArmContactVolumes(rig).Capture(true, 801);

                Assert.That(volumes.Count, Is.EqualTo(6));
                Assert.That(volumes, Has.All.Matches<ContactCapsuleSnapshot>(volume =>
                    volume.Active && volume.ContactGroupId == 801));
                Assert.That(
                    volumes[0].Current.Start,
                    Is.EqualTo(ToSimulation(rig.GetJoint("LeftShoulder").position)));
                Assert.That(
                    volumes[0].Current.End,
                    Is.EqualTo(ToSimulation(rig.GetJoint("LeftElbow").position)));
                Assert.That(
                    volumes[2].Current.End,
                    Is.EqualTo(ToSimulation(rig.GetJoint("LeftPalm").position)));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Capture_PreservesPreviousFrameAndFollowsMovedRig()
        {
            var player = new GameObject("MovingBlockArmVolumes");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.red, "5");
                rig.SetPose(StickFigurePose.Block, 1f);
                var source = new BlockArmContactVolumes(rig);
                var first = source.Capture(true, 802);
                player.transform.position += new Vector3(0f, 0f, 0.2f);

                var second = source.Capture(true, 802);

                Assert.That(second[0].Previous.Start, Is.EqualTo(first[0].Current.Start));
                Assert.That(
                    second[0].Current.Start.Z - second[0].Previous.Start.Z,
                    Is.EqualTo(0.2f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void BlockPose_FromLegalClosePosition_ReachesTheNetWithVisiblePalms()
        {
            var player = new GameObject("NetReachingBlockPose");
            try
            {
                player.transform.position = new Vector3(
                    0f,
                    0f,
                    -PrototypePlayerAgent.NetClearance);
                var rig = StickFigureRig.Create(player.transform, Color.blue, "6");
                rig.SetPose(StickFigurePose.Block, 1f);

                Assert.That(
                    rig.GetJoint("LeftPalm").position.z,
                    Is.GreaterThanOrEqualTo(0f));
                Assert.That(
                    rig.GetJoint("RightPalm").position.z,
                    Is.GreaterThanOrEqualTo(0f));

                const float forearmDiameter = 0.13f;
                var closedSeamWidth = (SimulatedBall.DefaultRadius * 2f) + forearmDiameter;
                Assert.That(
                    Mathf.Abs(
                        rig.GetJoint("RightElbow").position.x -
                        rig.GetJoint("LeftElbow").position.x),
                    Is.LessThanOrEqualTo(closedSeamWidth));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void BlockPose_CloseNetStance_IntersectsRepresentativeAboveTapeBall()
        {
            var player = new GameObject("RepresentativeNetBlock");
            try
            {
                player.transform.position = new Vector3(
                    0f,
                    0.45f,
                    -PrototypePlayerAgent.NetClearance);
                var rig = StickFigureRig.Create(player.transform, Color.blue, "7");
                rig.SetPose(StickFigurePose.Block, 1f);
                var volumes = new BlockArmContactVolumes(rig).Capture(true, 803);
                var ballCenter = new SimVector3(0f, 2.45f, 0.06f);

                var minimumClearance = float.PositiveInfinity;
                foreach (var volume in volumes)
                {
                    var closest = volume.Current.ClosestPoint(ballCenter, out _);
                    minimumClearance = Mathf.Min(
                        minimumClearance,
                        (ballCenter - closest).Magnitude -
                        (SimulatedBall.DefaultRadius + volume.Current.Radius));
                }

                Assert.That(minimumClearance, Is.LessThanOrEqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static SimVector3 ToSimulation(Vector3 value)
        {
            return new SimVector3(value.x, value.y, value.z);
        }
    }
}
