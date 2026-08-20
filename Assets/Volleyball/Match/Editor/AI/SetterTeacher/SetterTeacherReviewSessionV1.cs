using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Shared.Contracts;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterTeacherReviewSessionV1
    {
        private readonly SetterTeacherReviewServiceV1 _service;
        private readonly List<SetterTeacherAttemptV1> _attempts = new List<SetterTeacherAttemptV1>();

        public SetterTeacherReviewSessionV1(
            SetterTeacherReviewServiceV1 service,
            SetterTeacherRequestV1 request)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Attempts = new ReadOnlyCollection<SetterTeacherAttemptV1>(_attempts);
        }

        public SetterTeacherRequestV1 Request { get; }
        public IReadOnlyList<SetterTeacherAttemptV1> Attempts { get; }
        public SetterHumanReviewV1 ConfirmedReview { get; private set; }

        public async Task<SetterTeacherAttemptV1> RequestAsync(CancellationToken cancellationToken)
        {
            if (ConfirmedReview != null)
                throw new InvalidOperationException("A confirmed review is immutable.");
            var attempt = await _service.RequestAsync(Request, cancellationToken).ConfigureAwait(false);
            _attempts.Add(attempt);
            return attempt;
        }

        public SetterHumanReviewV1 Confirm(
            SetterTeacherAttemptV1 attempt,
            PlayerId selectedAttacker,
            string correctionReason = null)
        {
            return Confirm(new SetterHumanReviewV1(
                attempt, selectedAttacker, correctionReason));
        }

        internal SetterHumanReviewV1 Confirm(SetterHumanReviewV1 review)
        {
            if (ConfirmedReview != null)
                throw new InvalidOperationException("A confirmed review is immutable.");
            if (review == null || !_attempts.Contains(review.Attempt))
                throw new ArgumentException("The review attempt is not part of this session.", nameof(review));
            ConfirmedReview = review;
            return review;
        }
    }
}
