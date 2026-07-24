using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var director = UnityEngine.Object.FindFirstObjectByType<FormalSixVsSixRallyDirector>();
            var ball = UnityEngine.Object.FindFirstObjectByType<SimulatedBall>();
            var players = UnityEngine.Object.FindObjectsByType<PrototypePlayerAgent>(FindObjectsSortMode.None);
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
            Assert.That(replay.Players.Select(player => player.PlayerId).Distinct().Count(), Is.EqualTo(12));
            Assert.That(replay.Snapshots, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(replay.Events.First().Kind, Is.EqualTo("Serve"));
            Assert.That(replay.Events, Has.Some.Matches<MatchReplayEventV1>(replayEvent => replayEvent.Kind == "Serve"));
            Assert.That(replay.Events, Has.Some.Matches<MatchReplayEventV1>(replayEvent => replayEvent.Kind == "Contact"));
            Assert.That(replay.Events, Has.Some.Matches<MatchReplayEventV1>(replayEvent =>
                replayEvent.SetChain != null &&
                !string.IsNullOrWhiteSpace(replayEvent.SetChain.QualityGrade) &&
                replayEvent.SetChain.ActualAttackContactCenter != null));
            Assert.That(director.AttackableSetRate, Is.InRange(0f, 1f));
            var decisions = replay.Events.Where(replayEvent => replayEvent.Kind == "Decision").ToList();
            Assert.That(decisions, Is.Not.Empty);
            Assert.That(decisions, Has.All.Matches<MatchReplayEventV1>(replayEvent =>
                replayEvent.Decision.Candidates.Count == 6));
            AssertCandidateScores(replay, decisions);
            AssertOrganizationDiagnostics(replay, decisions);
            AssertReplayOrdering(replay);
            AssertRegularCadence(replay);

            var resolved = replay.Events.Last(replayEvent => replayEvent.Kind == "RallyResolved");
            var resolvedSnapshot = replay.Snapshots[resolved.SnapshotIndex];
            Assert.That(resolved, Is.SameAs(replay.Events.Last()));
            Assert.That(resolvedSnapshot.HomeScore + resolvedSnapshot.AwayScore,
                Is.EqualTo(replay.InitialState.HomeScore + replay.InitialState.AwayScore + 1));
            Assert.That(resolvedSnapshot.ServingTeam, Is.EqualTo(resolved.Team));
            Assert.DoesNotThrow(() => replay.Validate());

            var outputDirectory = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "TestResults",
                "decision-replay",
                Guid.NewGuid().ToString("N"));
            MatchReplayArtifactWriter.Write(outputDirectory, replay);
            var jsonPath = Path.Combine(outputDirectory, "replay.json");
            var htmlPath = Path.Combine(outputDirectory, "index.html");
            Assert.That(File.Exists(jsonPath), Is.True);
            Assert.That(File.Exists(htmlPath), Is.True);
            Assert.DoesNotThrow(() => MatchReplayJson.Deserialize(File.ReadAllText(jsonPath)).Validate());
            var html = File.ReadAllText(htmlPath);
            Assert.That(html, Does.Contain("MatchReplayV1"));
            Assert.That(html, Does.Contain("timeline"));
            Assert.That(html, Does.Contain("score-panel"));
            Assert.That(html, Does.Contain("event-marker"));
            Assert.That(html, Does.Contain("replay.json"));
            Assert.That(html, Does.Contain("set-quality"));
        }

        private static void AssertCandidateScores(
            MatchReplayV1 replay,
            IEnumerable<MatchReplayEventV1> decisions)
        {
            var abilities = replay.Players.ToDictionary(player => player.PlayerId, player => player.Ability);
            var sawExcludedCandidate = false;
            foreach (var decisionEvent in decisions)
            {
                Assert.That(decisionEvent.Decision.SelectedPlayerId, Is.EqualTo(decisionEvent.PlayerId));
                Assert.That(decisionEvent.Decision.Candidates,
                    Has.Some.Matches<MatchReplayCandidateScoreV1>(candidate =>
                        candidate.PlayerId == decisionEvent.Decision.SelectedPlayerId && candidate.IsFeasible));
                foreach (var candidate in decisionEvent.Decision.Candidates)
                {
                    var ability = abilities[candidate.PlayerId];
                    var expectedTechnique = decisionEvent.Decision.Action == "Attack"
                        ? ability.Serve * ability.Attack
                        : decisionEvent.Decision.Action == "Receive"
                            ? ability.Receive
                            : ability.Set;
                    Assert.That(candidate.Technique, Is.EqualTo(expectedTechnique).Within(0.0001f));
                    if (!candidate.IsFeasible)
                    {
                        sawExcludedCandidate = true;
                        Assert.That(candidate.ExclusionReason,
                            Is.EqualTo(candidate.Reachability >= 0f ? "ConsecutiveTouch" : "Unreachable"));
                    }
                }
            }

            Assert.That(sawExcludedCandidate, Is.True);
        }

        private static void AssertOrganizationDiagnostics(
            MatchReplayV1 replay,
            IEnumerable<MatchReplayEventV1> decisions)
        {
            var playerIds = replay.Players.Select(player => player.PlayerId).ToHashSet();
            var organizationDecisions = decisions
                .Where(replayEvent => replayEvent.Decision.Stage == "Organize")
                .ToList();
            Assert.That(organizationDecisions, Is.Not.Empty);
            foreach (var decisionEvent in organizationDecisions)
            {
                var organization = decisionEvent.Decision.Diagnostics?.Organization;
                Assert.That(organization, Is.Not.Null);
                Assert.That(organization.Target, Is.Not.Null);
                Assert.That(organization.FirstPassLanding, Is.Not.Null);
                Assert.That(
                    new[] { "Best", "Secondary", "Poor" },
                    Does.Contain(organization.ZoneGrade));
                Assert.That(playerIds.Contains(organization.SetterPlayerId), Is.True);
                Assert.That(playerIds.Contains(organization.OrganizerPlayerId), Is.True);
                Assert.That(
                    new[] { "Reachable", "Unreachable", "PreviousTouch" },
                    Does.Contain(organization.SetterArrival));
                Assert.That(organization.SetterMovementMeters, Is.GreaterThanOrEqualTo(0f));
            }
        }

        private static void AssertReplayOrdering(MatchReplayV1 replay)
        {
            for (var index = 1; index < replay.Snapshots.Count; index++)
            {
                var previous = replay.Snapshots[index - 1];
                var current = replay.Snapshots[index];
                Assert.That(current.SimulationTimeSeconds, Is.GreaterThanOrEqualTo(previous.SimulationTimeSeconds));
                if (Mathf.Approximately(current.SimulationTimeSeconds, previous.SimulationTimeSeconds))
                {
                    Assert.That(current.EventSequence, Is.GreaterThan(previous.EventSequence));
                }
            }

            for (var index = 0; index < replay.Events.Count; index++)
            {
                var replayEvent = replay.Events[index];
                Assert.That(replayEvent.SnapshotIndex, Is.InRange(0, replay.Snapshots.Count - 1));
                Assert.That(replay.Snapshots[replayEvent.SnapshotIndex].SimulationTimeSeconds,
                    Is.EqualTo(replayEvent.SimulationTimeSeconds).Within(0.00001f));
                if (index > 0)
                {
                    Assert.That(replayEvent.SimulationTimeSeconds,
                        Is.GreaterThanOrEqualTo(replay.Events[index - 1].SimulationTimeSeconds));
                }
            }
        }

        private static void AssertRegularCadence(MatchReplayV1 replay)
        {
            var eventSnapshots = new HashSet<int>(replay.Events.Select(replayEvent => replayEvent.SnapshotIndex));
            var regularSnapshots = replay.Snapshots
                .Where((snapshot, index) => !eventSnapshots.Contains(index))
                .ToList();
            Assert.That(regularSnapshots, Has.Count.GreaterThanOrEqualTo(2));
            for (var index = 1; index < regularSnapshots.Count; index++)
            {
                Assert.That(
                    regularSnapshots[index].SimulationTimeSeconds - regularSnapshots[index - 1].SimulationTimeSeconds,
                    Is.EqualTo(MatchReplayV1.SampleIntervalSeconds).Within(0.00001f));
            }
        }
    }
}
