using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation.TrainingLab;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class SetterTargetSnapshotV1Tests
    {
        [Test]
        public void SameVisibleFacts_ProduceSortedCandidatesAndStableHash()
        {
            var first = Create(new[] { Candidate("home-b", .5f, true), Candidate("home-a", .8f, true) });
            var second = Create(new[] { Candidate("home-a", .8f, true), Candidate("home-b", .5f, true) });

            Assert.That(first.Candidates.Select(value => value.PlayerId.Value),
                Is.EqualTo(new[] { "home-a", "home-b" }));
            CollectionAssert.AreEqual(SetterTargetSnapshotV1.ToCanonicalBytes(first),
                SetterTargetSnapshotV1.ToCanonicalBytes(second));
            Assert.That(first.SnapshotHash, Is.EqualTo(second.SnapshotHash));
        }

        [Test]
        public void SelectedAttacker_MustBeAFeasibleCandidate()
        {
            Assert.That(() => Create(new[] { Candidate("home-a", .8f, false) }),
                Throws.ArgumentException);
        }

        [Test]
        public void DuplicateCandidatePlayerIds_AreRejectedAtFrozenSnapshotBoundary()
        {
            Assert.That(() => Create(new[]
                {
                    Candidate("home-a", .8f, true),
                    Candidate("home-a", .7f, true)
                }),
                Throws.ArgumentException.With.Message.Contains("unique player IDs"));
        }

        [Test]
        public void Contract_DoesNotExposeFutureOrOutcomeFields()
        {
            var names = typeof(SetterTargetSnapshotV1)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(value => value.Name)
                .ToArray();

            Assert.That(names, Does.Not.Contain("Outcome"));
            Assert.That(names, Does.Not.Contain("WinningTeam"));
            Assert.That(names, Does.Not.Contain("TrajectoryArtifact"));
            Assert.That(names, Does.Not.Contain("FinalRoute"));
        }

        private static SetterTargetSnapshotV1 Create(
            DecisionCandidateSnapshotV1[] candidates)
        {
            return new SetterTargetSnapshotV1(
                "training-v1/setter-target", new string('a', 64), 3, 17,
                .2f, TeamId.Blue, 4, 9, .5f,
                new SimVector3(0f, 2f, -1f), new SimVector3(0f, 0f, 3f),
                new SimVector3(0f, 1f, -2f), 2,
                new StablePlayerId("home-setter"), new StablePlayerId("home-a"),
                candidates);
        }

        private static DecisionCandidateSnapshotV1 Candidate(
            string id, float total, bool feasible)
        {
            return new DecisionCandidateSnapshotV1(new StablePlayerId(id), feasible,
                new RallyDecisionScore(total, .4f, .3f, .2f, total));
        }
    }
}
