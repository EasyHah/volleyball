using NUnit.Framework;
using UnityEngine;
using VolleyballMatch.Domain.Players;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.EditModeTests
{
    public sealed class PlayerContactSurfacesTests
    {
        [Test]
        public void Capture_BuildsOneForearmPlatformThatFollowsVisibleRig()
        {
            var player = new GameObject("ContactSurfacePlayer");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.blue, "1");
                rig.SetPose(StickFigurePose.Receive, 1f);
                var surfaces = new PlayerContactSurfaces(rig, player.transform);
                var first = surfaces.Capture(TechniqueAction.Receive, true, 10)[0];
                player.transform.position = new Vector3(0f, 0f, 0.2f);
                var second = surfaces.Capture(TechniqueAction.Receive, true, 10)[0];

                Assert.That(first.Active, Is.True);
                Assert.That(first.Current.Width, Is.GreaterThan(0.2f));
                Assert.That(second.Current.Origin.Z - second.Previous.Origin.Z, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(second.VelocityAt(0f, 0f, 1f / 120f).Z, Is.EqualTo(24f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Capture_SetHandsShareGroupAndAttackUsesSinglePalm()
        {
            var player = new GameObject("ContactSurfaceGroups");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.blue, "2");
                var surfaces = new PlayerContactSurfaces(rig, player.transform);

                var set = surfaces.Capture(TechniqueAction.Set, true, 22);
                var attack = surfaces.Capture(TechniqueAction.Attack, true, 23);

                Assert.That(set.Count, Is.EqualTo(2));
                Assert.That(set[0].ContactGroupId, Is.EqualTo(set[1].ContactGroupId));
                Assert.That(attack.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
