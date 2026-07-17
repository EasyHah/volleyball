using NUnit.Framework;
using UnityEngine;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.EditModeTests
{
    public sealed class StickFigureRigTests
    {
        [Test]
        public void Create_BuildsRequiredNamedJoints()
        {
            var root = new GameObject("RigTestRoot");

            try
            {
                var rig = StickFigureRig.Create(root.transform, Color.blue, "7");

                Assert.That(rig.HasJoint("Head"), Is.True);
                Assert.That(rig.HasJoint("LeftElbow"), Is.True);
                Assert.That(rig.HasJoint("RightElbow"), Is.True);
                Assert.That(rig.HasJoint("RightPalm"), Is.True);
                Assert.That(rig.HasJoint("LeftKnee"), Is.True);
                Assert.That(rig.HasJoint("RightKnee"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetPose_UsesFiniteJointRotationsForEveryPose()
        {
            var root = new GameObject("RigTestRoot");

            try
            {
                var rig = StickFigureRig.Create(root.transform, Color.blue, "7");

                foreach (StickFigurePose pose in System.Enum.GetValues(typeof(StickFigurePose)))
                {
                    rig.SetPose(pose, 1f);
                    var rotation = rig.GetJoint("RightShoulder").localRotation;
                    Assert.That(IsFinite(rotation), Is.True, pose.ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReceivePose_BringsBothHandsTogetherForAVisibleForearmPlatform()
        {
            var root = new GameObject("ReceivePoseRig");

            try
            {
                var rig = StickFigureRig.Create(root.transform, Color.blue, "3");
                rig.SetPose(StickFigurePose.Receive, 1f);

                var handSeparation = Vector3.Distance(
                    rig.GetJoint("LeftPalm").position,
                    rig.GetJoint("RightPalm").position);

                Assert.That(handSeparation, Is.LessThan(0.08f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool IsFinite(Quaternion rotation)
        {
            return !float.IsNaN(rotation.x)
                && !float.IsNaN(rotation.y)
                && !float.IsNaN(rotation.z)
                && !float.IsNaN(rotation.w)
                && !float.IsInfinity(rotation.x)
                && !float.IsInfinity(rotation.y)
                && !float.IsInfinity(rotation.z)
                && !float.IsInfinity(rotation.w);
        }
    }
}
