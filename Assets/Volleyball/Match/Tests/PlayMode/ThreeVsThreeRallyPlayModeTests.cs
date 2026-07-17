using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.Presentation;

namespace Volleyball.PlayModeTests
{
    public sealed class ThreeVsThreeRallyPlayModeTests
    {
        [UnityTest]
        public IEnumerator PhysicalLoop_UsesSixPlayersOneBallAndSwitchableCameras()
        {
            yield return SceneManager.LoadSceneAsync("Physical3v3Rally", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<ThreeVsThreeRallyDirector>();
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var cameras = Object.FindFirstObjectByType<RallyCameraController>();

            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(cameras, Is.Not.Null);
            Assert.That(
                Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None),
                Has.Length.EqualTo(6));
            Assert.That(
                Object.FindObjectsByType<SimulatedBall>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));

            var timeout = Time.realtimeSinceStartup + 20f;
            while (director.CompletedCycles < 1 && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                director.CompletedCycles,
                Is.GreaterThanOrEqualTo(1),
                $"contacts={director.SuccessfulContacts}, misses={director.MissedRallies}");
            Assert.That(director.SuccessfulContacts, Is.GreaterThanOrEqualTo(6));
            Assert.That(director.ExecutionErrorApplications, Is.GreaterThanOrEqualTo(6));
            Assert.That(director.MovementAssignments, Is.GreaterThanOrEqualTo(6));
            Assert.That(director.TacticRevision, Is.GreaterThanOrEqualTo(1));
            Assert.That(director.TotalMovementShortfall, Is.GreaterThanOrEqualTo(0f));
            Assert.That(ball.Diagnostics.NonFiniteStates, Is.Zero);

            cameras.SetView(RallyCameraView.Sideline);
            yield return null;
            Assert.That(cameras.CurrentView, Is.EqualTo(RallyCameraView.Sideline));
            Assert.That(Camera.main.orthographic, Is.False);

            cameras.SetView(RallyCameraView.BallFollow);
            yield return null;
            Assert.That(cameras.CurrentView, Is.EqualTo(RallyCameraView.BallFollow));

            cameras.SetView(RallyCameraView.Tactical);
            yield return null;
            Assert.That(cameras.CurrentView, Is.EqualTo(RallyCameraView.Tactical));
            Assert.That(Camera.main.orthographic, Is.True);
            Assert.That(cameras.ViewSwitchCount, Is.GreaterThanOrEqualTo(4));

            var variationTimeout = Time.realtimeSinceStartup + 16f;
            while (director.MissedRallies < 1 && Time.realtimeSinceStartup < variationTimeout)
            {
                yield return null;
            }

            Assert.That(
                director.MissedRallies,
                Is.GreaterThanOrEqualTo(1),
                "Seeded route changes should eventually produce a natural execution miss.");
            Assert.That(director.TacticRevision, Is.GreaterThanOrEqualTo(2));
            Assert.That(director.MovementAssignments, Is.GreaterThan(6));
        }
    }
}
