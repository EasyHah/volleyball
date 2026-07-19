using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Volleyball.Domain.Prototype;
using Volleyball.Presentation;

namespace Volleyball.PlayModeTests
{
    public sealed class BlockImpactFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator DisablingAnActiveEffect_RestoresTheBallTrailImmediately()
        {
            var root = new GameObject("DisabledBlockImpactTest");
            var ball = new GameObject("DisabledBlockImpactBallTrail");
            try
            {
                var trail = ball.AddComponent<TrailRenderer>();
                trail.startWidth = 0.065f;
                trail.endWidth = 0.01f;
                trail.startColor = Color.yellow;
                trail.endColor = Color.clear;
                var feedback = BlockImpactFeedback.Create(root.transform, trail);

                feedback.Play(TeamId.Orange, Vector3.one, Vector3.forward, 12f);
                Assert.That(trail.startWidth, Is.GreaterThan(0.065f));
                feedback.enabled = false;
                yield return null;

                Assert.That(feedback.IsPlaying, Is.False);
                Assert.That(trail.startWidth, Is.EqualTo(0.065f).Within(0.001f));
                Assert.That(trail.endWidth, Is.EqualTo(0.01f).Within(0.001f));
                Assert.That(trail.startColor, Is.EqualTo(Color.yellow));
                Assert.That(trail.endColor, Is.EqualTo(Color.clear));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(ball);
            }
        }
    }
}
