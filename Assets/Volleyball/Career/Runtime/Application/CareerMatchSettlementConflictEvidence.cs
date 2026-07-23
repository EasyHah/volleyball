using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public sealed class CareerMatchSettlementConflictEvidence
    {
        public CareerMatchSettlementConflictEvidence(
            Sha256Digest storedContextDigest,
            Sha256Digest storedResultDigest,
            Sha256Digest incomingContextDigest,
            Sha256Digest incomingResultDigest)
        {
            RequireDigest(storedContextDigest, nameof(storedContextDigest));
            RequireDigest(storedResultDigest, nameof(storedResultDigest));
            RequireDigest(incomingContextDigest, nameof(incomingContextDigest));
            RequireDigest(incomingResultDigest, nameof(incomingResultDigest));
            StoredContextDigest = storedContextDigest;
            StoredResultDigest = storedResultDigest;
            IncomingContextDigest = incomingContextDigest;
            IncomingResultDigest = incomingResultDigest;
        }

        public Sha256Digest StoredContextDigest { get; }
        public Sha256Digest StoredResultDigest { get; }
        public Sha256Digest IncomingContextDigest { get; }
        public Sha256Digest IncomingResultDigest { get; }

        private static void RequireDigest(Sha256Digest value, string parameterName)
        {
            if (string.IsNullOrEmpty(value.Value))
            {
                throw new System.ArgumentException(
                    "A SHA-256 digest is required.",
                    parameterName);
            }
        }
    }
}
