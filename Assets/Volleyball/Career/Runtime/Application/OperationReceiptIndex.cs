using System;
using System.Collections.Generic;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public enum OperationReceiptLookupKind
    {
        Missing = 0,
        Existing = 1,
        Conflict = 2
    }

    public readonly struct OperationReceiptLookup
    {
        public OperationReceiptLookup(
            OperationReceiptLookupKind kind,
            OperationReceipt receipt)
        {
            Kind = kind;
            Receipt = receipt;
        }

        public OperationReceiptLookupKind Kind { get; }

        public OperationReceipt Receipt { get; }
    }

    public sealed class OperationReceiptIndex
    {
        private readonly Dictionary<OperationId, OperationReceipt> _receipts;

        public OperationReceiptIndex(IEnumerable<OperationReceipt> receipts)
        {
            if (receipts == null)
            {
                throw new ArgumentNullException(nameof(receipts));
            }

            _receipts = new Dictionary<OperationId, OperationReceipt>();
            foreach (var receipt in receipts)
            {
                if (receipt == null)
                {
                    throw new ArgumentException("Operation receipts cannot contain null.", nameof(receipts));
                }

                if (_receipts.ContainsKey(receipt.OperationId))
                {
                    throw new ArgumentException(
                        "Operation receipt IDs must be unique.",
                        nameof(receipts));
                }

                _receipts.Add(receipt.OperationId, receipt);
            }
        }

        public OperationReceiptLookup Find(
            OperationId operationId,
            Sha256Digest inputFingerprint)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A non-empty operation ID is required.",
                    nameof(operationId));
            }

            if (string.IsNullOrEmpty(inputFingerprint.Value))
            {
                throw new ArgumentException(
                    "A valid input fingerprint is required.",
                    nameof(inputFingerprint));
            }

            if (!_receipts.TryGetValue(operationId, out var receipt))
            {
                return new OperationReceiptLookup(OperationReceiptLookupKind.Missing, null);
            }

            var kind = receipt.InputFingerprint.Equals(inputFingerprint)
                ? OperationReceiptLookupKind.Existing
                : OperationReceiptLookupKind.Conflict;
            return new OperationReceiptLookup(kind, receipt);
        }
    }
}
