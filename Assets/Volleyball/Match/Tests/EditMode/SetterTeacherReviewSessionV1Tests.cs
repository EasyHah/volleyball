using System;
using System.Linq;
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
    public sealed class SetterTeacherReviewSessionV1Tests
    {
        [Test]
        public async Task RequestAndConfirm_PreservesAttemptAndLegalCorrection()
        {
            var session = new SetterTeacherReviewSessionV1(
                new SetterTeacherReviewServiceV1(new FakeClient(MenShenChatStatus.Success,
                    "{\"schemaVersion\":1,\"ranking\":[\"home-b\",\"home-a\"],\"reason\":\"Open lane.\"}"),
                    MenShenModelProfile.DoubaoMini, "test-key", TimeSpan.FromSeconds(1)),
                SetterTeacherRequestV1.Create(Snapshot()));

            var attempt = await session.RequestAsync(CancellationToken.None);
            var review = session.Confirm(attempt, new StablePlayerId("home-a"), "Prefer local timing.");

            Assert.That(attempt.IsSuccessful, Is.True);
            Assert.That(session.Attempts, Has.Count.EqualTo(1));
            Assert.That(review.AcceptedTeacher, Is.False);
            Assert.That(review.MatchesLocal, Is.True);
            Assert.That(() => session.Confirm(attempt, new StablePlayerId("home-b")),
                Throws.InvalidOperationException);
        }

        [Test]
        public async Task FailedAttempt_CannotBeConfirmedAndRetryKeepsHistory()
        {
            var client = new SequencedClient();
            var session = new SetterTeacherReviewSessionV1(
                new SetterTeacherReviewServiceV1(client, MenShenModelProfile.DoubaoMini,
                    "test-key", TimeSpan.FromSeconds(1)),
                SetterTeacherRequestV1.Create(Snapshot()));

            var failed = await session.RequestAsync(CancellationToken.None);
            var successful = await session.RequestAsync(CancellationToken.None);

            Assert.That(failed.IsSuccessful, Is.False);
            Assert.That(successful.IsSuccessful, Is.True);
            Assert.That(session.Attempts.Select(value => value),
                Is.EqualTo(new[] { failed, successful }));
            Assert.That(() => session.Confirm(failed, new StablePlayerId("home-a")),
                Throws.InvalidOperationException);
        }

        private static SetterTargetSnapshotV1 Snapshot()
        {
            return new SetterTargetSnapshotV1(
                "training-v1/setter-target", new string('a', 64), 3, 17, .2f,
                TeamId.Blue, 4, 9, .5f, new SimVector3(0f, 2f, -1f),
                new SimVector3(0f, 0f, 3f), new SimVector3(0f, 1f, -2f), 2,
                new StablePlayerId("home-setter"), new StablePlayerId("home-a"),
                new[] { Candidate("home-a", .8f), Candidate("home-b", .5f) });
        }

        private static DecisionCandidateSnapshotV1 Candidate(string id, float score)
        {
            return new DecisionCandidateSnapshotV1(new StablePlayerId(id), true,
                new RallyDecisionScore(score, .4f, .3f, .2f, score));
        }

        private sealed class FakeClient : IMenShenChatClient
        {
            private readonly MenShenChatStatus _status;
            private readonly string _content;

            public FakeClient(MenShenChatStatus status, string content)
            {
                _status = status;
                _content = content;
            }

            public Task<MenShenChatResult> CompleteAsync(MenShenModelProfile profile,
                string systemPrompt, string casePrompt, string apiKey, TimeSpan deadline,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new MenShenChatResult(_status, _content, 200,
                    1, 2, 3, 4, 7, 0, string.Empty, _status.ToString()));
            }
        }

        private sealed class SequencedClient : IMenShenChatClient
        {
            private int _calls;

            public Task<MenShenChatResult> CompleteAsync(MenShenModelProfile profile,
                string systemPrompt, string casePrompt, string apiKey, TimeSpan deadline,
                CancellationToken cancellationToken)
            {
                _calls++;
                return new FakeClient(
                    _calls == 1 ? MenShenChatStatus.Timeout : MenShenChatStatus.Success,
                    _calls == 1 ? string.Empty : "{\"schemaVersion\":1,\"ranking\":[\"home-a\",\"home-b\"],\"reason\":\"Timing.\"}")
                    .CompleteAsync(profile, systemPrompt, casePrompt, apiKey, deadline, cancellationToken);
            }
        }
    }
}
