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

            var timeout = Time.realtimeSinceStartup + 120f;
            while (director.Result == null && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(director.Result, Is.Not.Null);
            Assert.That(
                Mathf.Max(director.Result.HomeScore, director.Result.AwayScore),
                Is.GreaterThanOrEqualTo(15));
            Assert.That(
                Mathf.Abs(director.Result.HomeScore - director.Result.AwayScore),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(director.Result.PlayerStats, Has.Count.EqualTo(6));
            Assert.That(director.IsLoopRunning, Is.False);
            Assert.That(director.GroundResolvedRallies, Is.GreaterThan(0));
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

        }
    }
}
