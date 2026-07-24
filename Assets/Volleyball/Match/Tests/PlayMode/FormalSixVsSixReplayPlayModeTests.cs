using System;
using System.Collections;
using System.Text;
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
            AssertV4Identity(restored.Context.ContextHash, "formal context");
            AssertV4Identity(restored.ReplayHash, "formal replay");
            Assert.That(restored.Events, Is.Not.Empty);
            foreach (var replayEvent in restored.Events)
            {
                Assert.That(replayEvent.TestedEnvelope, Is.Not.Null);
                Assert.That(
                    replayEvent.ExecutableEnvelope,
                    Is.Not.Null);
                Assert.That(replayEvent.Trajectory, Is.Not.Null);
                Assert.That(replayEvent.AbilityConsumptions, Is.Not.Empty);
                Assert.That(replayEvent.Classification, Is.Not.Null);
                Assert.That(
                    replayEvent.TestedEnvelope.Version,
                    Is.EqualTo(ContractVersions.ReplayV4));
                Assert.That(
                    replayEvent.ExecutableEnvelope.Version,
                    Is.EqualTo(ContractVersions.ReplayV4));
                AssertV4Identity(
                    replayEvent.TestedEnvelope.Identity,
                    "tested execution envelope");
                AssertV4Identity(
                    replayEvent.ExecutableEnvelope.Identity,
                    "executable execution envelope");
                AssertV4Identity(
                    replayEvent.TestedEnvelope.DerivedAttributesFingerprint,
                    "derived V4 attributes");
                AssertV4Identity(
                    replayEvent.Trajectory.ArtifactIdentity,
                    "trajectory artifact");
                AssertV4Identity(
                    replayEvent.Trajectory.CacheKey.Identity,
                    "trajectory cache key");
                Assert.That(
                    replayEvent.Trajectory.CacheKey.BallStateVersion,
                    Is.GreaterThanOrEqualTo(0));
                Assert.That(replayEvent.RuleDecision.RulesVersion, Is.EqualTo(3));
                Assert.That(replayEvent.RuleDecision.Accepted, Is.True);
                Assert.That(
                    replayEvent.EventKind == "Attack",
                    Is.EqualTo(replayEvent.ObservedP6Geometry != null));
            }
        }

        [UnityTest]
        public IEnumerator Capture_TwoIndependentFixedSeedFormalRunsAreByteStable()
        {
            var payloads = new byte[2][];
            MatchReplayV4 first = null;
            MatchReplayV4 second = null;
            for (var run = 0; run < 2; run++)
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
                    UnityEngine.Object.FindObjectsByType<
                        PrototypePlayerAgent>(
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
                    "Independent formal run " + run +
                    " did not complete its first rally.");
                var replay = recorder.Complete();
                payloads[run] = Encoding.UTF8.GetBytes(
                    ContractJson.SerializeV4(replay));
                if (run == 0)
                {
                    first = replay;
                }
                else
                {
                    second = replay;
                }
            }

            CollectionAssert.AreEqual(payloads[0], payloads[1]);
            Assert.That(second.Events.Count, Is.EqualTo(first.Events.Count));
            for (var eventIndex = 0;
                 eventIndex < first.Events.Count;
                 eventIndex++)
            {
                var left = first.Events[eventIndex];
                var right = second.Events[eventIndex];
                Assert.That(left.SequenceNumber, Is.EqualTo(eventIndex));
                Assert.That(right.SequenceNumber, Is.EqualTo(eventIndex));
                Assert.That(
                    right.TestedEnvelope.Identity,
                    Is.EqualTo(left.TestedEnvelope.Identity));
                Assert.That(
                    right.ExecutableEnvelope.Identity,
                    Is.EqualTo(left.ExecutableEnvelope.Identity));
                Assert.That(
                    right.Trajectory.ArtifactIdentity,
                    Is.EqualTo(left.Trajectory.ArtifactIdentity));
                Assert.That(
                    right.Trajectory.CacheKey.Identity,
                    Is.EqualTo(left.Trajectory.CacheKey.Identity));
                Assert.That(
                    right.Classification.Kind,
                    Is.EqualTo(left.Classification.Kind));
                Assert.That(
                    right.AbilityConsumptions.Count,
                    Is.EqualTo(left.AbilityConsumptions.Count));
                for (var consumptionIndex = 0;
                     consumptionIndex <
                     left.AbilityConsumptions.Count;
                     consumptionIndex++)
                {
                    var leftConsumption =
                        left.AbilityConsumptions[consumptionIndex];
                    var rightConsumption =
                        right.AbilityConsumptions[consumptionIndex];
                    Assert.That(
                        rightConsumption.AttributeName,
                        Is.EqualTo(leftConsumption.AttributeName));
                    Assert.That(
                        rightConsumption.Value,
                        Is.EqualTo(leftConsumption.Value));
                    Assert.That(
                        rightConsumption.EvidenceKind,
                        Is.EqualTo("ExecutionEnvelopeFactoryRead"));
                }
            }
        }

        private static void AssertV4Identity(string value, string subject)
        {
            Assert.That(value, Is.Not.Null.And.Length.EqualTo(64), subject);
        }
    }
}
