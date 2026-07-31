using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation.TrainingLab;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class DecisionSnapshotV1Tests
    {
        [Test]
        public void SameVisibleFacts_ProduceSortedCandidatesAndStableHash()
        {
            var first = Create(
                new[]
                {
                    Candidate("home-c", .3f, false),
                    Candidate("home-a", .8f, true),
                    Candidate("home-b", .5f, true)
                });
            var second = Create(
                new[]
                {
                    Candidate("home-b", .5f, true),
                    Candidate("home-a", .8f, true),
                    Candidate("home-c", .3f, false)
                });

            Assert.That(
                first.Candidates.Select(value => value.PlayerId.Value),
                Is.EqualTo(new[] { "home-a", "home-b", "home-c" }));
            CollectionAssert.AreEqual(
                DecisionSnapshotV1.ToCanonicalBytes(first),
                DecisionSnapshotV1.ToCanonicalBytes(second));
            Assert.That(first.SnapshotHash, Is.EqualTo(second.SnapshotHash));
        }

        [Test]
        public void ChangingVisibleDecisionFact_ChangesHash()
        {
            var baseline = Create(DefaultCandidates());
            var changed = new DecisionSnapshotV1(
                baseline.ScenarioId,
                baseline.ScenarioHash,
                baseline.RulesVersion,
                baseline.SourceSequence + 1,
                baseline.SimulationTimeSeconds,
                baseline.Stage,
                baseline.Team,
                baseline.AvailableSeconds,
                baseline.BallPosition,
                baseline.BallVelocity,
                baseline.PredictedBallTarget,
                baseline.CountedHits,
                baseline.LastLegalActor,
                baseline.SelectedPlayer,
                baseline.SelectedAction,
                baseline.Candidates);

            Assert.That(changed.SnapshotHash,
                Is.Not.EqualTo(baseline.SnapshotHash));
        }

        [Test]
        public void Contract_DoesNotExposeFutureTrajectoryOrOutcome()
        {
            var names = typeof(DecisionSnapshotV1)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(value => value.Name)
                .ToArray();

            Assert.That(names, Does.Not.Contain("Outcome"));
            Assert.That(names, Does.Not.Contain("WinningTeam"));
            Assert.That(names, Does.Not.Contain("TrajectoryArtifact"));
            Assert.That(names, Does.Not.Contain("FutureSample"));
            Assert.That(names, Does.Not.Contain("FinalRoute"));
        }

        [Test]
        public void SelectedPlayer_MustBeAFeasibleVisibleCandidate()
        {
            Assert.That(
                () => new DecisionSnapshotV1(
                    "training-v1/test",
                    new string('a', 64),
                    3,
                    1,
                    .2f,
                    RallyDecisionStage.Receive,
                    TeamId.Blue,
                    .5f,
                    new SimVector3(0f, 2f, -1f),
                    new SimVector3(0f, 0f, 3f),
                    new SimVector3(0f, 1f, -2f),
                    1,
                    new StablePlayerId("home-a"),
                    new StablePlayerId("home-a"),
                    TechniqueAction.Receive,
                    new[] { Candidate("home-a", .8f, false) }),
                Throws.ArgumentException);
        }

        [Test]
        public void EditorExporter_WritesOneJsonLineToFixedIgnoredFolder()
        {
            var exporter = AppDomain.CurrentDomain.GetAssemblies()
                .Single(value =>
                    value.GetName().Name == "Volleyball.Match.Editor")
                .GetType(
                    "Volleyball.Editor.TrainingDecisionSnapshotExporterV1",
                    true);
            Assert.That(
                exporter.Assembly.GetName().Name,
                Is.EqualTo("Volleyball.Match.Editor"));
            var evidence = new TrainingRunEvidenceV1(
                "training-v1/export-test",
                new string('b', 64),
                71,
                new List<TrainingTimelineEventV1>(),
                new[] { Create(DefaultCandidates()) },
                new List<SetterTargetSnapshotV1>(),
                false,
                null,
                string.Empty);
            var root = Path.Combine(
                Path.GetTempPath(),
                "volleyball-training-export-" + Guid.NewGuid().ToString("N"));
            try
            {
                var path = (string)exporter
                    .GetMethod("Export", BindingFlags.Public |
                                          BindingFlags.Static)
                    .Invoke(null, new object[] { evidence, root });
                Assert.That(
                    path,
                    Does.StartWith(Path.Combine(
                        root,
                        "TestResults",
                        "TrainingLab",
                        "DecisionSnapshots")));
                var lines = File.ReadAllLines(path);
                Assert.That(lines, Has.Length.EqualTo(1));
                Assert.That(
                    lines[0],
                    Does.Contain(evidence.Decisions[0].SnapshotHash));
                Assert.That(
                    File.ReadAllBytes(path).Take(3),
                    Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf }));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static DecisionSnapshotV1 Create(
            DecisionCandidateSnapshotV1[] candidates)
        {
            return new DecisionSnapshotV1(
                "training-v1/test",
                new string('a', 64),
                3,
                17,
                .2f,
                RallyDecisionStage.Receive,
                TeamId.Blue,
                .5f,
                new SimVector3(0f, 2f, -1f),
                new SimVector3(0f, 0f, 3f),
                new SimVector3(0f, 1f, -2f),
                1,
                new StablePlayerId("home-last"),
                new StablePlayerId("home-a"),
                TechniqueAction.Receive,
                candidates);
        }

        private static DecisionCandidateSnapshotV1[] DefaultCandidates()
        {
            return new[]
            {
                Candidate("home-a", .8f, true),
                Candidate("home-b", .5f, true)
            };
        }

        private static DecisionCandidateSnapshotV1 Candidate(
            string id,
            float total,
            bool feasible)
        {
            return new DecisionCandidateSnapshotV1(
                new StablePlayerId(id),
                feasible,
                new RallyDecisionScore(
                    total,
                    .4f,
                    .3f,
                    .2f,
                    total));
        }
    }
}
