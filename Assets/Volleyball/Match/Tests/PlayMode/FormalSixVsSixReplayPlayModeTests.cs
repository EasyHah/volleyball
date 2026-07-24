using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.PlayModeTests
{
    public sealed class FormalSixVsSixReplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator Attach_BeforeInitializeV4RequiresNativeContext()
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
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("initialized"));
                Assert.That(host.GetComponent<MatchReplayRecorder>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator Attach_AfterInitializeV4CreatesFormalOnlyRecorder()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                FindObjectsSortMode.None);

            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));
            var recorder = MatchReplayRecorder.Attach(director, ball, players);

            Assert.That(recorder, Is.Not.Null);
            Assert.That(recorder.IsComplete, Is.False);
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<MatchReplayRecorder>(),
                Is.SameAs(recorder));
        }

        [UnityTest]
        public IEnumerator Capture_FirstFormalRallyProducesStrictNativeV4Replay()
        {
            yield return SceneManager.LoadSceneAsync(
                "FormalIndoor6v6",
                LoadSceneMode.Single);
            var director =
                UnityEngine.Object.FindFirstObjectByType<
                    FormalSixVsSixRallyDirector>();
            var ball =
                UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players =
                UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(
                    FindObjectsSortMode.None);
            var recorder = MatchReplayRecorder.Attach(
                director,
                ball,
                players);
            recorder.StartCapture();

            var timeout = Time.realtimeSinceStartup + 90f;
            while (!recorder.IsComplete &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(
                recorder.IsComplete,
                Is.True,
                "The first formal rally did not complete.");
            var replay = recorder.Complete();
            var json = ContractJson.SerializeV4(replay);
            var restored = ContractJson.DeserializeMatchReplayV4(json);

            Assert.That(
                restored.FormatVersion,
                Is.EqualTo(ContractVersions.ReplayV4));
            Assert.That(
                restored.Context.ContractVersion,
                Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(
                restored.Context.RulesVersion,
                Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(restored.Events, Is.Not.Empty);
            foreach (var replayEvent in restored.Events)
            {
                Assert.That(replayEvent.Envelope, Is.Not.Null);
                Assert.That(replayEvent.Trajectory, Is.Not.Null);
                Assert.That(replayEvent.AbilityConsumptions, Is.Not.Empty);
                Assert.That(replayEvent.Classification, Is.Not.Null);
                Assert.That(replayEvent.RuleDecision.RulesVersion, Is.EqualTo(3));
                Assert.That(replayEvent.RuleDecision.Accepted, Is.True);
                Assert.That(
                    replayEvent.EventKind == "Attack",
                    Is.EqualTo(replayEvent.ObservedP6Geometry != null));
            }
        }
    }
}
