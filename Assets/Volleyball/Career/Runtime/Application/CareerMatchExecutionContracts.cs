using System;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public sealed class CareerCanonicalMatchContext
    {
        private readonly byte[] _canonicalContextUtf8;

        public CareerCanonicalMatchContext(
            Guid sessionId,
            Sha256Digest contextDigest,
            byte[] canonicalContextUtf8)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "A non-empty match session ID is required.",
                    nameof(sessionId));
            }

            CareerMatchGuard.Digest(contextDigest, nameof(contextDigest));
            if (canonicalContextUtf8 == null)
            {
                throw new ArgumentNullException(nameof(canonicalContextUtf8));
            }

            if (canonicalContextUtf8.Length == 0)
            {
                throw new ArgumentException(
                    "Canonical match context bytes cannot be empty.",
                    nameof(canonicalContextUtf8));
            }

            SessionId = sessionId;
            ContextDigest = contextDigest;
            _canonicalContextUtf8 = (byte[])canonicalContextUtf8.Clone();
        }

        public Guid SessionId { get; }

        public Sha256Digest ContextDigest { get; }

        public byte[] CanonicalContextUtf8 => (byte[])_canonicalContextUtf8.Clone();
    }

    public sealed class CareerMatchExecutionOutcome
    {
        private readonly byte[] _canonicalResultUtf8;

        public CareerMatchExecutionOutcome(
            CareerCanonicalMatchContext context,
            Sha256Digest resultDigest,
            byte[] canonicalResultUtf8,
            CareerMatchFacts facts)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            CareerMatchGuard.Digest(resultDigest, nameof(resultDigest));
            if (canonicalResultUtf8 == null)
            {
                throw new ArgumentNullException(nameof(canonicalResultUtf8));
            }

            if (canonicalResultUtf8.Length == 0)
            {
                throw new ArgumentException(
                    "Canonical match result bytes cannot be empty.",
                    nameof(canonicalResultUtf8));
            }

            Facts = facts ?? throw new ArgumentNullException(nameof(facts));
            if (facts.SessionId != context.SessionId)
            {
                throw new ArgumentException(
                    "Match facts session does not match the canonical context.",
                    nameof(facts));
            }

            if (!facts.ContextDigest.Equals(context.ContextDigest))
            {
                throw new ArgumentException(
                    "Match facts context digest does not match the canonical context.",
                    nameof(facts));
            }

            if (!facts.ResultDigest.Equals(resultDigest))
            {
                throw new ArgumentException(
                    "Match facts result digest does not match the canonical result.",
                    nameof(facts));
            }

            ResultDigest = resultDigest;
            _canonicalResultUtf8 = (byte[])canonicalResultUtf8.Clone();
        }

        public CareerCanonicalMatchContext Context { get; }

        public Sha256Digest ResultDigest { get; }

        public byte[] CanonicalResultUtf8 => (byte[])_canonicalResultUtf8.Clone();

        public CareerMatchFacts Facts { get; }
    }
}
