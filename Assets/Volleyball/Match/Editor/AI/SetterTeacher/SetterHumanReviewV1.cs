using System;
using System.Linq;
using Volleyball.Shared.Contracts;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterHumanReviewV1
    {
        internal SetterHumanReviewV1(
            SetterTeacherAttemptV1 attempt,
            PlayerId selectedAttacker,
            string correctionReason)
        {
            Attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            if (!attempt.IsSuccessful)
                throw new InvalidOperationException("Only a successful teacher attempt can be reviewed.");
            if (!attempt.Request.Candidates.Any(candidate =>
                    candidate.PlayerId.Equals(selectedAttacker)))
            {
                throw new ArgumentException("The selected attacker must be a legal candidate.", nameof(selectedAttacker));
            }

            SelectedAttacker = selectedAttacker;
            CorrectionReason = correctionReason ?? string.Empty;
            ConfirmedUtc = DateTime.UtcNow;
        }

        public SetterTeacherAttemptV1 Attempt { get; }
        public PlayerId SelectedAttacker { get; }
        public string CorrectionReason { get; }
        public DateTime ConfirmedUtc { get; }
        public bool AcceptedTeacher => Attempt.Response.TopChoice.Equals(SelectedAttacker);
        public bool MatchesLocal => Attempt.Request.SelectedAttacker.Equals(SelectedAttacker);
    }
}
