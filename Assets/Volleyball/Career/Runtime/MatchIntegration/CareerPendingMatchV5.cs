using System;
using System.Text;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    /// <summary>Durable V5 pending payload; it never accepts a V4 lifecycle envelope.</summary>
    public sealed class CareerPendingMatchV5
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly byte[] _canonicalContextUtf8;

        private CareerPendingMatchV5(MatchContextV5 context, byte[] canonicalContextUtf8)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            _canonicalContextUtf8 = canonicalContextUtf8 ?? throw new ArgumentNullException(nameof(canonicalContextUtf8));
            SessionId = context.SessionId;
            ContextHash = context.ContextHash;
        }

        public Guid SessionId { get; }
        public string ContextHash { get; }
        public MatchContextV5 Context { get; }
        public byte[] CanonicalContextUtf8 => (byte[])_canonicalContextUtf8.Clone();

        public static CareerPendingMatchV5 Create(MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var bytes = StrictUtf8.GetBytes(ContractJson.SerializeV5(context));
            return FromCanonicalContext(bytes);
        }

        public static CareerPendingMatchV5 FromCanonicalContext(byte[] canonicalContextUtf8)
        {
            if (canonicalContextUtf8 == null) throw new ArgumentNullException(nameof(canonicalContextUtf8));
            var context = ContractJson.DeserializeMatchContextV5(
                StrictUtf8.GetString(canonicalContextUtf8));
            var expected = StrictUtf8.GetBytes(ContractJson.SerializeV5(context));
            if (!Equal(canonicalContextUtf8, expected))
                throw new ContractValidationException("Career V5 pending context bytes are not canonical.");
            return new CareerPendingMatchV5(context, expected);
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }

    public enum CareerV5PendingRecoveryKind
    {
        Ready = 0,
        DiscardLegacyPendingAndCreateV5 = 1,
        DiscardUnsupportedEvidenceAndCreateNewMatch = 2
    }

    public sealed class CareerV5PendingRecovery
    {
        private CareerV5PendingRecovery(CareerV5PendingRecoveryKind kind,
            CareerPendingMatchV5 pending, string reason)
        {
            Kind = kind;
            Pending = pending;
            Reason = reason;
        }

        public CareerV5PendingRecoveryKind Kind { get; }
        public CareerPendingMatchV5 Pending { get; }
        public string Reason { get; }

        public static CareerV5PendingRecovery Read(CareerPendingMatchV5 pending) =>
            new CareerV5PendingRecovery(CareerV5PendingRecoveryKind.Ready, pending, null);

        public static CareerV5PendingRecovery RejectLegacy(PendingCareerMatch legacy) =>
            legacy == null
                ? Read(null)
                : new CareerV5PendingRecovery(
                    CareerV5PendingRecoveryKind.DiscardLegacyPendingAndCreateV5,
                    null,
                    "legacy_v4_pending_requires_discard_and_v5_recreate");

        public static CareerV5PendingRecovery RejectUnsupportedEvidence() =>
            new CareerV5PendingRecovery(
                CareerV5PendingRecoveryKind.DiscardUnsupportedEvidenceAndCreateNewMatch,
                null,
                "unsupported_v5_position_fault_evidence_requires_discard_and_new_match");
    }
}
