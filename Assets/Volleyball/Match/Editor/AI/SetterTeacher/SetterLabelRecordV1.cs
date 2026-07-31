using System;
using System.Linq;
using Volleyball.Shared.Contracts;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterLabelRecordV1
    {
        public const int SchemaVersion = 1;

        public SetterLabelRecordV1(SetterHumanReviewV1 review)
        {
            var attempt = review?.Attempt ?? throw new ArgumentNullException(nameof(review));
            if (!attempt.IsSuccessful)
                throw new ArgumentException("Only a successful reviewed attempt can be recorded.", nameof(review));

            SnapshotHash = attempt.Request.SnapshotHash;
            CandidateSetHash = attempt.Request.CandidateSetHash;
            RequestHash = attempt.Request.RequestHash;
            ModelId = attempt.Model.ModelId;
            PromptVersion = SetterTeacherRequestV1.PromptVersion;
            RawResponse = attempt.Result.Content;
            TeacherRanking = attempt.Response.Ranking.Select(value => value.Value).ToArray();
            TeacherReason = attempt.Response.Reason;
            LocalAttacker = attempt.Request.SelectedAttacker.Value;
            SelectedAttacker = review.SelectedAttacker.Value;
            AcceptedTeacher = review.AcceptedTeacher;
            MatchesLocal = review.MatchesLocal;
            CorrectionReason = review.CorrectionReason;
            ConfirmedUtc = review.ConfirmedUtc;
        }

        public int SchemaVersionValue => SchemaVersion;
        public string SnapshotHash { get; }
        public string CandidateSetHash { get; }
        public string RequestHash { get; }
        public string ModelId { get; }
        public string PromptVersion { get; }
        public string RawResponse { get; }
        public string[] TeacherRanking { get; }
        public string TeacherReason { get; }
        public string LocalAttacker { get; }
        public string SelectedAttacker { get; }
        public bool AcceptedTeacher { get; }
        public bool MatchesLocal { get; }
        public string CorrectionReason { get; }
        public DateTime ConfirmedUtc { get; }
    }
}
