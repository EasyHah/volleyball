using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Editor.AI.SetterTeacher;
using Volleyball.Presentation.TrainingLab;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class SetterTeacherRequestV1Tests
    {
        [Test]
        public void Create_UsesOnlySetterTargetVisibleFactsAndFeasibleCandidates()
        {
            var request = SetterTeacherRequestV1.Create(CreateSnapshot(
                new[]
                {
                    Candidate("home-c", .3f, false),
                    Candidate("home-b", .5f, true),
                    Candidate("home-a", .8f, true)
                }));

            Assert.That(request.SnapshotHash, Is.Not.Empty);
            Assert.That(request.Candidates.Select(value => value.PlayerId.Value),
                Is.EqualTo(new[] { "home-a", "home-b" }));
            Assert.That(request.SelectedAttacker.Value, Is.EqualTo("home-a"));
            Assert.That(request.BallPosition, Is.EqualTo(new SimVector3(0f, 2f, -1f)));
            Assert.That(SetterTeacherRequestV1.PromptVersion, Is.Not.Empty);
        }

        [Test]
        public void Create_ProducesStableCandidateAndRequestHashes()
        {
            var first = SetterTeacherRequestV1.Create(CreateSnapshot(
                new[] { Candidate("home-b", .5f, true), Candidate("home-a", .8f, true) }));
            var second = SetterTeacherRequestV1.Create(CreateSnapshot(
                new[] { Candidate("home-a", .8f, true), Candidate("home-b", .5f, true) }));

            Assert.That(second.CandidateSetHash, Is.EqualTo(first.CandidateSetHash));
            Assert.That(second.RequestHash, Is.EqualTo(first.RequestHash));
        }

        [Test]
        public void Parse_RequiresAnExactRankingOfFeasibleCandidates()
        {
            var request = SetterTeacherRequestV1.Create(CreateSnapshot(
                new[] { Candidate("home-a", .8f, true), Candidate("home-b", .5f, true) }));

            var response = SetterTeacherResponseParserV1.Parse(
                "{\"schemaVersion\":1,\"ranking\":[\"home-b\",\"home-a\"],\"reason\":\"Better approach.\"}",
                request);

            Assert.That(response.Ranking.Select(value => value.Value),
                Is.EqualTo(new[] { "home-b", "home-a" }));
            Assert.That(response.TopChoice.Value, Is.EqualTo("home-b"));
            Assert.That(() => SetterTeacherResponseParserV1.Parse(
                    "{\"schemaVersion\":1,\"ranking\":[\"home-a\",\"home-a\"],\"reason\":\"Duplicate.\"}",
                    request),
                Throws.ArgumentException);
            Assert.That(() => SetterTeacherResponseParserV1.Parse(
                    "{\"schemaVersion\":1,\"ranking\":[\"home-a\"],\"reason\":\"Missing.\"}",
                    request),
                Throws.ArgumentException);
            Assert.That(() => SetterTeacherResponseParserV1.Parse(
                    "{\"schemaVersion\":1,\"ranking\":[\"home-a\",\"unknown\"],\"reason\":\"Unknown.\"}",
                    request),
                Throws.ArgumentException);
        }

        private static SetterTargetSnapshotV1 CreateSnapshot(
            IReadOnlyList<DecisionCandidateSnapshotV1> candidates)
        {
            return new SetterTargetSnapshotV1(
                "training-v1/setter-review",
                new string('a', 64),
                3,
                17,
                .2f,
                TeamId.Blue,
                2,
                5,
                .5f,
                new SimVector3(0f, 2f, -1f),
                new SimVector3(0f, 0f, 3f),
                new SimVector3(0f, 1f, -2f),
                1,
                new StablePlayerId("home-last"),
                new StablePlayerId("home-a"),
                candidates);
        }

        private static DecisionCandidateSnapshotV1 Candidate(
            string id,
            float total,
            bool feasible)
        {
            return new DecisionCandidateSnapshotV1(
                new StablePlayerId(id),
                feasible,
                new RallyDecisionScore(total, .4f, .3f, .2f, total));
        }
    }
}
