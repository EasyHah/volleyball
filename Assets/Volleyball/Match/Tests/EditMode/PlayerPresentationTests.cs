using NUnit.Framework;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class PlayerPresentationTests
    {
        [Test]
        public void Constructor_CreatesRigUnderPlayerRoot()
        {
            var host = new GameObject("player");
            try
            {
                var presentation = new PlayerPresentation(host.transform, Color.blue, "1");

                Assert.That(presentation.Rig, Is.Not.Null);
                Assert.That(presentation.Rig.transform.parent, Is.EqualTo(host.transform));
                Assert.That(presentation.Rig.HasJoint("RightPalm"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ApplyPose_MapsActionAndSetStyleToExpectedRigPose()
        {
            var host = new GameObject("player");
            var expectedHost = new GameObject("expected");
            try
            {
                var presentation = new PlayerPresentation(host.transform, Color.blue, "1");
                var expected = StickFigureRig.Create(expectedHost.transform, Color.blue, "1");

                presentation.ApplyPose(TechniqueAction.Attack, SetTechniqueStyle.FrontTwoHand, 1f);
                expected.SetPose(StickFigurePose.Spike, 1f);
                AssertRotationsEqual(expected.CaptureLocalRotations(), presentation.Rig.CaptureLocalRotations());

                presentation.ApplyPose(TechniqueAction.Set, SetTechniqueStyle.OneHandLeft, 1f);
                expected.SetPose(StickFigurePose.SetOneHandLeft, 1f);
                AssertRotationsEqual(expected.CaptureLocalRotations(), presentation.Rig.CaptureLocalRotations());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(expectedHost);
            }
        }

        [Test]
        public void PreviewPose_RestoresLiveRigRotations()
        {
            var host = new GameObject("player");
            try
            {
                var presentation = new PlayerPresentation(host.transform, Color.blue, "1");
                presentation.ApplyPose(TechniqueAction.Receive, SetTechniqueStyle.FrontTwoHand, 1f);
                var before = presentation.Rig.CaptureLocalRotations();
                var previewWasApplied = false;

                presentation.WithPreviewPose(
                    TechniqueAction.Attack,
                    SetTechniqueStyle.FrontTwoHand,
                    () => previewWasApplied = presentation.Rig.GetJoint("RightShoulder").localRotation !=
                                            before["RightShoulder"]);

                Assert.That(previewWasApplied, Is.True);
                AssertRotationsEqual(before, presentation.Rig.CaptureLocalRotations());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void AssertRotationsEqual(
            System.Collections.Generic.IReadOnlyDictionary<string, Quaternion> expected,
            System.Collections.Generic.IReadOnlyDictionary<string, Quaternion> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            foreach (var rotation in expected)
            {
                Assert.That(actual.ContainsKey(rotation.Key), Is.True);
                Assert.That(Quaternion.Angle(rotation.Value, actual[rotation.Key]), Is.EqualTo(0f).Within(0.001f));
            }
        }
    }
}
