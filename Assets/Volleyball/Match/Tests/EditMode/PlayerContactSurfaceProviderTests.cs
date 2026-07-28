using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class PlayerContactSurfaceProviderTests
    {
        [Test]
        public void Collect_UsesResolvedSurfaceAndCancelProducesNoFutureCandidates()
        {
            var player = new GameObject("ProviderCandidatePlayer");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.blue, "2");
                var provider = new PlayerContactSurfaceProvider(rig, player.transform);
                var contacts = new List<BallContactCandidate>();
                var input = new PlayerContactInput(
                    new PlayerId(TeamId.Blue, PlayerRole.Attacker),
                    TechniqueAction.Attack,
                    TechniqueAction.Attack,
                    new ActionTimelineSample(ActionPhase.Contact, 0.5f, 0f, 1f, true),
                    801,
                    1f,
                    new SimVector3(0f, 2f, 8f),
                    SetContactHand.Both);

                provider.Collect(input, contacts);

                Assert.That(contacts, Has.Count.EqualTo(1));
                Assert.That(contacts[0].Surface.Active, Is.True);
                Assert.That(contacts[0].Surface.ContactGroupId, Is.EqualTo(801));
                Assert.That(contacts[0].Surface.Current.Origin.Y, Is.GreaterThan(0f));

                provider.Clear();
                contacts.Clear();
                provider.Collect(input, contacts);

                Assert.That(contacts, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void CaptureSurfaceCenter_DoesNotReplacePreviousFrameUsedByFormalContactCapture()
        {
            var player = new GameObject("ProviderAlignmentProbeHistory");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.blue, "2");
                var provider = new PlayerContactSurfaceProvider(rig, player.transform);
                var contacts = new List<BallContactCandidate>();
                var input = new PlayerContactInput(
                    new PlayerId(TeamId.Blue, PlayerRole.Attacker),
                    TechniqueAction.Attack,
                    TechniqueAction.Attack,
                    new ActionTimelineSample(ActionPhase.Contact, 0.5f, 0f, 1f, true),
                    802,
                    1f,
                    new SimVector3(0f, 2f, 8f),
                    SetContactHand.Both);

                provider.Collect(input, contacts);
                var formalPrevious = contacts[0].Surface.Current;

                player.transform.position = new Vector3(0f, 0f, 0.4f);
                var captureSurfaceCenter = typeof(PlayerContactSurfaceProvider).GetMethod(
                    "CaptureSurfaceCenter",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(captureSurfaceCenter, Is.Not.Null);
                captureSurfaceCenter.Invoke(provider, new object[]
                {
                    TechniqueAction.Attack,
                    802,
                    SetContactHand.Both
                });
                provider.Collect(input, contacts);

                Assert.That(contacts[1].Surface.Previous.Origin.X, Is.EqualTo(formalPrevious.Origin.X));
                Assert.That(contacts[1].Surface.Previous.Origin.Y, Is.EqualTo(formalPrevious.Origin.Y));
                Assert.That(contacts[1].Surface.Previous.Origin.Z, Is.EqualTo(formalPrevious.Origin.Z));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PreviewFrames_DoesNotReplacePreviousFrameUsedByFormalContactCapture()
        {
            var player = new GameObject("ProviderPreviewHistory");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.blue, "3");
                var provider = new PlayerContactSurfaceProvider(rig, player.transform);
                var contacts = new List<BallContactCandidate>();
                var input = new PlayerContactInput(
                    new PlayerId(TeamId.Blue, PlayerRole.Attacker),
                    TechniqueAction.Attack,
                    TechniqueAction.Attack,
                    new ActionTimelineSample(ActionPhase.Contact, 0.5f, 0f, 1f, true),
                    803,
                    1f,
                    new SimVector3(0f, 2f, 8f),
                    SetContactHand.Both);

                provider.Collect(input, contacts);
                var firstFormalCurrent = contacts[0].Surface.Current;

                player.transform.position = new Vector3(0f, 0f, 0.4f);
                var preview = provider.PreviewFrames(TechniqueAction.Attack);
                provider.Collect(input, contacts);

                Assert.That(contacts[1].Surface.Previous.Origin, Is.EqualTo(firstFormalCurrent.Origin));
                Assert.That(contacts[1].Surface.Previous.Origin, Is.Not.EqualTo(preview[0].Origin));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PreviewBlockFrames_DoesNotReplacePreviousFrameUsedByFormalBlockCapture()
        {
            var player = new GameObject("ProviderBlockPreviewHistory");
            try
            {
                var rig = StickFigureRig.Create(player.transform, Color.red, "4");
                rig.SetPose(StickFigurePose.Block, 1f);
                var provider = new PlayerContactSurfaceProvider(rig, player.transform);
                var firstFormal = provider.CaptureBlock(
                    new ActionTimelineSample(ActionPhase.Contact, 0.5f, 0f, 1f, true),
                    804);

                player.transform.position = new Vector3(0f, 0f, 0.4f);
                var preview = provider.PreviewBlockFrames();
                var secondFormal = provider.CaptureBlock(
                    new ActionTimelineSample(ActionPhase.Contact, 0.5f, 0f, 1f, true),
                    804);

                Assert.That(secondFormal[0].Previous.Start, Is.EqualTo(firstFormal[0].Current.Start));
                Assert.That(secondFormal[0].Previous.Start, Is.Not.EqualTo(preview[0].Start));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
