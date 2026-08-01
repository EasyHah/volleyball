using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Editor.AI;
using Volleyball.Editor.AI.SetterTeacher;
using Volleyball.Presentation.TrainingLab;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class SetterLabelDatasetWriterV1Tests
    {
        [Test]
        public async Task Append_WritesIgnoredJsonlAndManifestCountsConfirmedLabels()
        {
            var root = Path.Combine(Path.GetTempPath(), "setter-labels-" + Guid.NewGuid().ToString("N"));
            try
            {
                var review = await CreateReview();
                var writer = new SetterLabelDatasetWriterV1(root);
                var path = writer.Append(new SetterLabelRecordV1(review));
                var manifest = SetterLabelDatasetManifestV1.Read(path);

                Assert.That(path, Does.StartWith(Path.Combine(root, "TestResults", "TrainingLab", "SetterLabels")));
                Assert.That(File.ReadAllLines(path), Has.Length.EqualTo(1));
                Assert.That(manifest.SampleCount, Is.EqualTo(1));
                Assert.That(manifest.GoldenSetCount, Is.EqualTo(1));
                Assert.That(() => writer.Append(new SetterLabelRecordV1(review)),
                    Throws.InvalidOperationException);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static async Task<SetterHumanReviewV1> CreateReview()
        {
            var request = SetterTeacherRequestV1.Create(new SetterTargetSnapshotV1(
                "training-v1/setter-target", new string('a', 64), 3, 17, .2f,
                TeamId.Blue, 4, 9, .5f, new SimVector3(0f, 2f, -1f),
                new SimVector3(0f, 0f, 3f), new SimVector3(0f, 1f, -2f), 2,
                new StablePlayerId("home-setter"), new StablePlayerId("home-a"),
                new[] { Candidate("home-a", .8f), Candidate("home-b", .5f) }));
            var session = new SetterTeacherReviewSessionV1(
                new SetterTeacherReviewServiceV1(new Client(), MenShenModelProfile.DoubaoMini,
                    "test-key", TimeSpan.FromSeconds(1)), request);
            var attempt = await session.RequestAsync(CancellationToken.None);
            return session.Confirm(attempt, new StablePlayerId("home-b"), "Human correction.");
        }

        private static DecisionCandidateSnapshotV1 Candidate(string id, float score)
        {
            return new DecisionCandidateSnapshotV1(new StablePlayerId(id), true,
                new RallyDecisionScore(score, .4f, .3f, .2f, score));
        }

        private sealed class Client : IMenShenChatClient
        {
            public Task<MenShenChatResult> CompleteAsync(MenShenModelProfile profile,
                string systemPrompt, string casePrompt, string apiKey, TimeSpan deadline,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new MenShenChatResult(MenShenChatStatus.Success,
                    "{\"schemaVersion\":1,\"ranking\":[\"home-a\",\"home-b\"],\"reason\":\"Timing.\"}",
                    200, 1, 2, 3, 4, 7, 0, string.Empty, string.Empty));
            }
        }
    }
}
