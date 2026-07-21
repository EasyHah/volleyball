using System;

namespace Volleyball.Career.Domain
{
    public readonly struct CareerVersionToken : IEquatable<CareerVersionToken>
    {
        public CareerVersionToken(
            LineageId lineageId,
            long revision,
            Sha256Digest snapshotHash)
        {
            CareerSaveModelGuard.StableId(lineageId.Value, nameof(lineageId));
            CareerSaveModelGuard.PositiveRevision(revision, nameof(revision));
            if (string.IsNullOrEmpty(snapshotHash.Value))
            {
                throw new ArgumentException("A snapshot hash is required.", nameof(snapshotHash));
            }

            LineageId = lineageId;
            Revision = revision;
            SnapshotHash = snapshotHash;
        }

        public LineageId LineageId { get; }

        public long Revision { get; }

        public Sha256Digest SnapshotHash { get; }

        public bool Equals(CareerVersionToken other)
        {
            return LineageId.Equals(other.LineageId) &&
                   Revision == other.Revision &&
                   SnapshotHash.Equals(other.SnapshotHash);
        }

        public override bool Equals(object obj)
        {
            return obj is CareerVersionToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = LineageId.GetHashCode();
                hashCode = (hashCode * 397) ^ Revision.GetHashCode();
                hashCode = (hashCode * 397) ^ SnapshotHash.GetHashCode();
                return hashCode;
            }
        }
    }

    public sealed class CareerSaveIdentity
    {
        public CareerSaveIdentity(
            ProfileId profileId,
            SaveId saveId,
            LineageId lineageId,
            long revision,
            long createdAtUtcMs,
            long updatedAtUtcMs,
            Sha256Digest snapshotHash)
        {
            CareerSaveModelGuard.StableId(profileId.Value, nameof(profileId));
            CareerSaveModelGuard.StableId(saveId.Value, nameof(saveId));
            CareerSaveModelGuard.StableId(lineageId.Value, nameof(lineageId));
            CareerSaveModelGuard.PositiveRevision(revision, nameof(revision));
            CareerSaveModelGuard.NonNegativeUtcMilliseconds(createdAtUtcMs, nameof(createdAtUtcMs));
            CareerSaveModelGuard.NonNegativeUtcMilliseconds(updatedAtUtcMs, nameof(updatedAtUtcMs));
            if (updatedAtUtcMs < createdAtUtcMs)
            {
                throw new ArgumentException(
                    "The updated timestamp cannot precede the created timestamp.",
                    nameof(updatedAtUtcMs));
            }

            if (string.IsNullOrEmpty(snapshotHash.Value))
            {
                throw new ArgumentException("A snapshot hash is required.", nameof(snapshotHash));
            }

            ProfileId = profileId;
            SaveId = saveId;
            LineageId = lineageId;
            Revision = revision;
            CreatedAtUtcMs = createdAtUtcMs;
            UpdatedAtUtcMs = updatedAtUtcMs;
            SnapshotHash = snapshotHash;
        }

        public ProfileId ProfileId { get; }

        public SaveId SaveId { get; }

        public LineageId LineageId { get; }

        public long Revision { get; }

        public long CreatedAtUtcMs { get; }

        public long UpdatedAtUtcMs { get; }

        public Sha256Digest SnapshotHash { get; }

        public CareerVersionToken VersionToken => new CareerVersionToken(
            LineageId,
            Revision,
            SnapshotHash);
    }
}
