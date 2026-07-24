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
        public IEnumerator Attach_BeforeInitializeV4RejectsFormalDirector()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);
            var host = new GameObject("UninitializedFormalDirector");
            try
            {
                var director = host.AddComponent<FormalSixVsSixRallyDirector>();

                Assert.That(director.MatchContext, Is.Null);
                Assert.That(
                    () => MatchReplayRecorder.Attach(director, ball, players),
                    Throws.TypeOf<NotSupportedException>()
                        .With.Message.Contains("V4"));
                Assert.That(host.GetComponent<MatchReplayRecorder>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator Attach_AfterInitializeV4RejectsFormalDirector()
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
