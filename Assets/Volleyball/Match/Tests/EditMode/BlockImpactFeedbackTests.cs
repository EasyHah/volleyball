using System;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Prototype;
using Volleyball.Presentation;

namespace Volleyball.EditModeTests
{
    public sealed class BlockImpactFeedbackTests
    {
        [Test]
        public void Play_ActivatesVisibleBurstAndPulsesTheBallTrailForTheBlockingTeam()
        {
            var root = new GameObject("BlockImpactFeedbackTest");
            var ball = new GameObject("BallTrail");
            try
            {
                var trail = ball.AddComponent<TrailRenderer>();
                trail.startWidth = 0.065f;
                trail.endWidth = 0.01f;
                trail.startColor = new Color(1f, 0.96f, 0.35f, 0.9f);
                trail.endColor = new Color(1f, 0.96f, 0.35f, 0f);
                var feedback = BlockImpactFeedback.Create(root.transform, trail);
                var impactPoint = new Vector3(1.25f, 2.8f, -0.1f);

                feedback.Play(TeamId.Blue, impactPoint, Vector3.back, 13f);

                Assert.That(feedback.PlayedCount, Is.EqualTo(1));
                Assert.That(feedback.IsPlaying, Is.True);
                Assert.That(feedback.LastBlockingTeam, Is.EqualTo(TeamId.Blue));
                Assert.That(feedback.LastImpactPoint, Is.EqualTo(impactPoint));
                Assert.That(feedback.LastReboundSpeed, Is.EqualTo(13f));
                Assert.That(feedback.CurrentAccentColor.b, Is.GreaterThan(feedback.CurrentAccentColor.r));
                Assert.That(feedback.VisibleElementCount, Is.GreaterThanOrEqualTo(3));
                Assert.That(feedback.transform.position, Is.EqualTo(impactPoint));
                Assert.That(trail.startWidth, Is.GreaterThan(0.065f));
                Assert.That(trail.startColor.b, Is.GreaterThan(trail.startColor.r));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(ball);
            }
        }

        [Test]
        public void Play_UsesWarmAccentForOrangeAndRejectsInvalidImpactData()
        {
            var root = new GameObject("BlockImpactValidationTest");
            try
            {
                var feedback = BlockImpactFeedback.Create(root.transform, null);

                feedback.Play(TeamId.Orange, Vector3.one, Vector3.forward, 8f);

                Assert.That(feedback.CurrentAccentColor.r, Is.GreaterThan(feedback.CurrentAccentColor.b));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    feedback.Play((TeamId)99, Vector3.zero, Vector3.forward, 8f));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    feedback.Play(TeamId.Blue, new Vector3(float.NaN, 0f, 0f), Vector3.forward, 8f));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    feedback.Play(TeamId.Blue, Vector3.zero, Vector3.zero, 8f));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    feedback.Play(TeamId.Blue, Vector3.zero, Vector3.forward, -1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
