using System;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public sealed class CareerMatchSettlementResult
    {
        public CareerMatchSettlementResult(
            CareerMatchSettlementStatus status,
            PersistenceResultKind? persistenceKind,
            CareerSaveSnapshot snapshot,
            Guid? sessionId,
            Sha256Digest? contextDigest,
            Sha256Digest? resultDigest,
            long? committedRevision,
            CareerSettlementReceipt settlementReceipt,
            CareerMatchHistoryEntry historyEntry,
            CareerMatchSettlementConflictEvidence conflictEvidence,
            CareerMatchSettlementFailureKind failureKind,
            string failureCode)
        {
            Status = status;
            PersistenceKind = persistenceKind;
            Snapshot = snapshot;
            SessionId = sessionId;
            ContextDigest = contextDigest;
            ResultDigest = resultDigest;
            CommittedRevision = committedRevision;
            SettlementReceipt = settlementReceipt;
            HistoryEntry = historyEntry;
            ConflictEvidence = conflictEvidence;
            FailureKind = failureKind;
            FailureCode = failureCode;
        }

        public CareerMatchSettlementStatus Status { get; }
        public PersistenceResultKind? PersistenceKind { get; }
        public CareerSaveSnapshot Snapshot { get; }
        public Guid? SessionId { get; }
        public Sha256Digest? ContextDigest { get; }
        public Sha256Digest? ResultDigest { get; }
        public long? CommittedRevision { get; }
        public CareerSettlementReceipt SettlementReceipt { get; }
        public CareerMatchHistoryEntry HistoryEntry { get; }
        public CareerMatchSettlementConflictEvidence ConflictEvidence { get; }
        public CareerMatchSettlementFailureKind FailureKind { get; }
        public string FailureCode { get; }
    }
}
