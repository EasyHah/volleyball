using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.Presentation;

namespace Volleyball.PlayModeTests
{
    public sealed class FormalSixVsSixReplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator FormalV4Match_DoesNotProjectIntoLegacyV1Replay()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);

            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));
            Assert.That(
                () => MatchReplayRecorder.Attach(director, ball, players),
                Throws.TypeOf<NotSupportedException>()
                    .With.Message.Contains("V4"));
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<MatchReplayRecorder>(),
                Is.Null);
        }
    }
}
