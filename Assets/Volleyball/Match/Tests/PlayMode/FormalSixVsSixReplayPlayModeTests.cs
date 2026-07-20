using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Volleyball.Domain.Replay;
using Volleyball.Presentation;

namespace Volleyball.PlayModeTests
{
    public sealed class FormalSixVsSixReplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator Recorder_CapturesOneFormalRally()
        {
            yield return SceneManager.LoadSceneAsync("FormalIndoor6v6", LoadSceneMode.Single);
            var director = Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = Object.FindFirstObjectByType<SimulatedBall>();
            var players = Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);
            Assert.That(director, Is.Not.Null);
            Assert.That(ball, Is.Not.Null);
            Assert.That(players, Has.Length.EqualTo(12));

            var recorder = MatchReplayRecorder.Attach(director, ball, players);
            recorder.StartCapture();

            var timeout = Time.realtimeSinceStartup + 30f;
            while (!recorder.IsComplete && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(recorder.IsComplete, Is.True, "The first formal rally did not resolve in real time.");
            var replay = recorder.Complete();
            Assert.That(replay.Players, Has.Count.EqualTo(12));
            Assert.That(replay.Snapshots, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(replay.Events, Has.Some.Matches<MatchReplayEventV1>(replayEvent => replayEvent.Kind == "Serve"));
            Assert.That(replay.Events, Has.Some.Matches<MatchReplayEventV1>(replayEvent =>
                replayEvent.Kind == "Decision" && replayEvent.Decision.Candidates.Count == 6));
            Assert.That(replay.Events, Has.Some.Matches<MatchReplayEventV1>(replayEvent => replayEvent.Kind == "RallyResolved"));
            Assert.DoesNotThrow(() => replay.Validate());
        }
    }
}
