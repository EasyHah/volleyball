using System;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Application
{
    public sealed class SettleCareerMatchCommand
    {
        private readonly byte[] _canonicalContextUtf8;
        private readonly byte[] _canonicalResultUtf8;

        public SettleCareerMatchCommand(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            long settledAtUtcMs,
            Guid sessionId,
            byte[] canonicalContextUtf8,
            byte[] canonicalResultUtf8)
        {
            ProfileId = profileId;
            SaveId = saveId;
            ExpectedVersionToken = expectedVersionToken;
            SettledAtUtcMs = settledAtUtcMs;
            SessionId = sessionId;
            _canonicalContextUtf8 = canonicalContextUtf8 == null
                ? null
                : (byte[])canonicalContextUtf8.Clone();
            _canonicalResultUtf8 = canonicalResultUtf8 == null
                ? null
                : (byte[])canonicalResultUtf8.Clone();
        }

        public ProfileId ProfileId { get; }
        public SaveId SaveId { get; }
        public CareerVersionToken ExpectedVersionToken { get; }
        public long SettledAtUtcMs { get; }
        public Guid SessionId { get; }
        public byte[] CanonicalContextUtf8 => _canonicalContextUtf8 == null
            ? null
            : (byte[])_canonicalContextUtf8.Clone();
        public byte[] CanonicalResultUtf8 => _canonicalResultUtf8 == null
            ? null
            : (byte[])_canonicalResultUtf8.Clone();
    }
}
