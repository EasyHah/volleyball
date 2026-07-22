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

        private static SimVector3 ToSimulation(Vector3 value)
        {
            return new SimVector3(value.x, value.y, value.z);
        }
    }
}
