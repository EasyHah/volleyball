using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public enum CareerLoadability
    {
        Loadable = 0,
        RecoveryAvailable = 1,
        Missing = 2,
        Corrupt = 3,
        UnsupportedVersion = 4,
        Ambiguous = 5,
        BackupDegraded = 6
    }

    public enum ProfileLoadability
    {
        Loadable = 0,
        Missing = 1,
        Corrupt = 2,
        UnsupportedVersion = 3,
        Ambiguous = 4,
        RecoveryAvailable = 5
    }

    public readonly struct ProfileVersionToken : IEquatable<ProfileVersionToken>
    {
        public ProfileVersionToken(long revision, Sha256Digest snapshotHash)
        {
            LocalPersistenceGuard.PositiveRevision(revision, nameof(revision));
            LocalPersistenceGuard.Hash(snapshotHash, nameof(snapshotHash));
            Revision = revision;
            SnapshotHash = snapshotHash;
        }

        public long Revision { get; }

        public Sha256Digest SnapshotHash { get; }

        public bool Equals(ProfileVersionToken other)
        {
            return Revision == other.Revision && SnapshotHash.Equals(other.SnapshotHash);
        }

        public override bool Equals(object obj)
        {
            return obj is ProfileVersionToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Revision.GetHashCode() * 397) ^ SnapshotHash.GetHashCode();
            }
        }
    }

    public readonly struct CatalogVersionToken : IEquatable<CatalogVersionToken>
    {
        public CatalogVersionToken(long revision, Sha256Digest catalogHash)
        {
            LocalPersistenceGuard.PositiveRevision(revision, nameof(revision));
            LocalPersistenceGuard.Hash(catalogHash, nameof(catalogHash));
            Revision = revision;
            CatalogHash = catalogHash;
        }

        public long Revision { get; }

        public Sha256Digest CatalogHash { get; }

        public bool Equals(CatalogVersionToken other)
        {
            return Revision == other.Revision && CatalogHash.Equals(other.CatalogHash);
        }

        public override bool Equals(object obj)
        {
            return obj is CatalogVersionToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Revision.GetHashCode() * 397) ^ CatalogHash.GetHashCode();
            }
        }
    }

    public sealed class CareerIndexEntry
    {
        public CareerIndexEntry(
            SaveId saveId,
            string careerName,
            string playerDisplayName,
            CareerProgressionKind progressionKind,
            CareerPhase phase,
            int season,
            int week,
            long updatedAtUtcMs,
            CareerLoadability loadability)
        {
            LocalPersistenceGuard.SaveId(saveId, nameof(saveId));
            LocalPersistenceGuard.DefinedEnum(progressionKind, nameof(progressionKind));
            LocalPersistenceGuard.DefinedEnum(phase, nameof(phase));
            LocalPersistenceGuard.DefinedEnum(loadability, nameof(loadability));
            if ((season == 0) != (week == 0))
            {
                throw new ArgumentException(
                    "Season and week must both be zero or both describe a positive week.",
                    nameof(season));
            }

            if (season < 0 || season > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(season), season, "Season must be 0 through 6.");
            }

            if (week < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(week), week, "Week cannot be negative.");
            }

            LocalPersistenceGuard.NonNegativeTimestamp(updatedAtUtcMs, nameof(updatedAtUtcMs));
            SaveId = saveId;
            CareerName = LocalPersistenceGuard.RequiredText(careerName, nameof(careerName));
            PlayerDisplayName = LocalPersistenceGuard.RequiredText(
                playerDisplayName,
                nameof(playerDisplayName));
            ProgressionKind = progressionKind;
            Phase = phase;
            Season = season;
            Week = week;
            UpdatedAtUtcMs = updatedAtUtcMs;
            Loadability = loadability;
        }

        public SaveId SaveId { get; }

        public string CareerName { get; }

        public string PlayerDisplayName { get; }

        public CareerProgressionKind ProgressionKind { get; }

        public CareerPhase Phase { get; }

        public int Season { get; }

        public int Week { get; }

        public long UpdatedAtUtcMs { get; }

        public CareerLoadability Loadability { get; }

        public static CareerIndexEntry FromSnapshot(
            CareerSaveSnapshot snapshot,
            CareerLoadability loadability = CareerLoadability.Loadable)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var weekPlan = snapshot.Progression.WeekPlan;
            return new CareerIndexEntry(
                snapshot.Identity.SaveId,
                snapshot.CareerName,
                snapshot.Player == null
                    ? snapshot.PlayerDraft.DisplayName
                    : snapshot.Player.DisplayName,
                snapshot.Progression.Kind,
                snapshot.Progression.Phase,
                weekPlan == null ? 0 : weekPlan.Season,
                weekPlan == null ? 0 : weekPlan.Week,
                snapshot.Identity.UpdatedAtUtcMs,
                loadability);
        }
    }

    public sealed class LocalPlayerProfile
    {
        public const int CurrentSchemaVersion = 1;

        private readonly CareerIndexEntry[] _careerEntries;
        private readonly ReadOnlyCollection<CareerIndexEntry> _readOnlyCareerEntries;

        public LocalPlayerProfile(
            int schemaVersion,
            ProfileId profileId,
            long profileRevision,
            Sha256Digest profileSnapshotHash,
            string displayName,
            long createdAtUtcMs,
            long updatedAtUtcMs,
            IEnumerable<CareerIndexEntry> careerEntries)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    schemaVersion,
                    "Only local profile schema V1 is supported.");
            }

            LocalPersistenceGuard.ProfileId(profileId, nameof(profileId));
            LocalPersistenceGuard.PositiveRevision(profileRevision, nameof(profileRevision));
            LocalPersistenceGuard.Hash(profileSnapshotHash, nameof(profileSnapshotHash));
            LocalPersistenceGuard.NonNegativeTimestamp(createdAtUtcMs, nameof(createdAtUtcMs));
            LocalPersistenceGuard.NonNegativeTimestamp(updatedAtUtcMs, nameof(updatedAtUtcMs));
            if (updatedAtUtcMs < createdAtUtcMs)
            {
                throw new ArgumentException(
                    "The profile updated timestamp cannot precede its creation timestamp.",
                    nameof(updatedAtUtcMs));
            }

            if (careerEntries == null)
            {
                throw new ArgumentNullException(nameof(careerEntries));
            }

            var entries = new List<CareerIndexEntry>();
            var saveIds = new HashSet<SaveId>();
            foreach (var entry in careerEntries)
            {
                if (entry == null)
                {
                    throw new ArgumentException(
                        "Career index entries cannot contain null.",
                        nameof(careerEntries));
                }

                if (!saveIds.Add(entry.SaveId))
                {
                    throw new ArgumentException(
                        "Career index entries must use unique save IDs.",
                        nameof(careerEntries));
                }

                entries.Add(entry);
            }

            entries.Sort((left, right) => string.CompareOrdinal(
                left.SaveId.ToString(),
                right.SaveId.ToString()));

            SchemaVersion = schemaVersion;
            ProfileId = profileId;
            ProfileRevision = profileRevision;
            ProfileSnapshotHash = profileSnapshotHash;
            DisplayName = LocalPersistenceGuard.RequiredText(displayName, nameof(displayName));
            CreatedAtUtcMs = createdAtUtcMs;
            UpdatedAtUtcMs = updatedAtUtcMs;
            _careerEntries = entries.ToArray();
            _readOnlyCareerEntries = Array.AsReadOnly(_careerEntries);
        }

        public int SchemaVersion { get; }

        public ProfileId ProfileId { get; }

        public long ProfileRevision { get; }

        public Sha256Digest ProfileSnapshotHash { get; }

        public string DisplayName { get; }

        public long CreatedAtUtcMs { get; }

        public long UpdatedAtUtcMs { get; }

        public IReadOnlyList<CareerIndexEntry> CareerEntries => _readOnlyCareerEntries;

        public ProfileVersionToken VersionToken => new ProfileVersionToken(
            ProfileRevision,
            ProfileSnapshotHash);
    }

    public sealed class LocalProfileCatalogEntry
    {
        public LocalProfileCatalogEntry(
            ProfileId profileId,
            string displayName,
            long updatedAtUtcMs,
            ProfileLoadability loadability)
        {
            LocalPersistenceGuard.ProfileId(profileId, nameof(profileId));
            LocalPersistenceGuard.DefinedEnum(loadability, nameof(loadability));
            LocalPersistenceGuard.NonNegativeTimestamp(updatedAtUtcMs, nameof(updatedAtUtcMs));
            ProfileId = profileId;
            DisplayName = LocalPersistenceGuard.RequiredText(displayName, nameof(displayName));
            UpdatedAtUtcMs = updatedAtUtcMs;
            Loadability = loadability;
        }

        public ProfileId ProfileId { get; }

        public string DisplayName { get; }

        public long UpdatedAtUtcMs { get; }

        public ProfileLoadability Loadability { get; }
    }

    public sealed class LocalProfileCatalog
    {
        public const int CurrentSchemaVersion = 1;

        private readonly LocalProfileCatalogEntry[] _profiles;
        private readonly ReadOnlyCollection<LocalProfileCatalogEntry> _readOnlyProfiles;

        public LocalProfileCatalog(
            int schemaVersion,
            long catalogRevision,
            Sha256Digest catalogHash,
            IEnumerable<LocalProfileCatalogEntry> profiles)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    schemaVersion,
                    "Only local profile catalog schema V1 is supported.");
            }

            LocalPersistenceGuard.PositiveRevision(catalogRevision, nameof(catalogRevision));
            LocalPersistenceGuard.Hash(catalogHash, nameof(catalogHash));
            if (profiles == null)
            {
                throw new ArgumentNullException(nameof(profiles));
            }

            var entries = new List<LocalProfileCatalogEntry>();
            var profileIds = new HashSet<ProfileId>();
            foreach (var profile in profiles)
            {
                if (profile == null)
                {
                    throw new ArgumentException(
                        "Profile catalog entries cannot contain null.",
                        nameof(profiles));
                }

                if (!profileIds.Add(profile.ProfileId))
                {
                    throw new ArgumentException(
                        "Profile catalog entries must use unique profile IDs.",
                        nameof(profiles));
                }

                entries.Add(profile);
            }

            entries.Sort((left, right) => string.CompareOrdinal(
                left.ProfileId.ToString(),
                right.ProfileId.ToString()));
            SchemaVersion = schemaVersion;
            CatalogRevision = catalogRevision;
            CatalogHash = catalogHash;
            _profiles = entries.ToArray();
            _readOnlyProfiles = Array.AsReadOnly(_profiles);
        }

        public int SchemaVersion { get; }

        public long CatalogRevision { get; }

        public Sha256Digest CatalogHash { get; }

        public IReadOnlyList<LocalProfileCatalogEntry> Profiles => _readOnlyProfiles;

        public CatalogVersionToken VersionToken => new CatalogVersionToken(
            CatalogRevision,
            CatalogHash);
    }

    public enum PersistenceResultKind
    {
        Created = 0,
        Committed = 1,
        Loaded = 2,
        VersionConflict = 3,
        AlreadyExists = 4,
        NotFound = 5,
        RecoveryAvailable = 6,
        Corrupt = 7,
        UnsupportedVersion = 8,
        AmbiguousReplaceState = 9,
        AmbiguousRestoreState = 10,
        BackupDegraded = 11,
        LockUnavailable = 12,
        NotCommitted = 13,
        IoFailure = 14
    }

    public sealed class CareerPersistenceResult
    {
        public CareerPersistenceResult(
            PersistenceResultKind kind,
            CareerSaveSnapshot snapshot = null,
            CareerVersionToken? recoverableBackup = null,
            Sha256Digest? unreadableMainFingerprint = null)
        {
            LocalPersistenceGuard.DefinedEnum(kind, nameof(kind));
            var requiresSnapshot = kind == PersistenceResultKind.Created ||
                                   kind == PersistenceResultKind.Committed ||
                                   kind == PersistenceResultKind.Loaded ||
                                   kind == PersistenceResultKind.BackupDegraded;
            if (requiresSnapshot != (snapshot != null))
            {
                throw new ArgumentException(
                    "This persistence result kind requires exactly the documented snapshot payload.",
                    nameof(snapshot));
            }

            if ((kind == PersistenceResultKind.RecoveryAvailable) != recoverableBackup.HasValue)
            {
                throw new ArgumentException(
                    "Only RecoveryAvailable carries a recoverable backup token.",
                    nameof(recoverableBackup));
            }

            if (unreadableMainFingerprint.HasValue &&
                kind != PersistenceResultKind.RecoveryAvailable)
            {
                throw new ArgumentException(
                    "Only RecoveryAvailable can carry unreadable main-file evidence.",
                    nameof(unreadableMainFingerprint));
            }

            Kind = kind;
            Snapshot = snapshot;
            RecoverableBackup = recoverableBackup;
            UnreadableMainFingerprint = unreadableMainFingerprint;
        }

        public PersistenceResultKind Kind { get; }

        public CareerSaveSnapshot Snapshot { get; }

        public CareerVersionToken? RecoverableBackup { get; }

        public Sha256Digest? UnreadableMainFingerprint { get; }
    }

    public sealed class ProfilePersistenceResult
    {
        public ProfilePersistenceResult(
            PersistenceResultKind kind,
            LocalPlayerProfile profile = null)
        {
            LocalPersistenceGuard.DefinedEnum(kind, nameof(kind));
            var requiresProfile = kind == PersistenceResultKind.Created ||
                                  kind == PersistenceResultKind.Committed ||
                                  kind == PersistenceResultKind.Loaded ||
                                  kind == PersistenceResultKind.BackupDegraded;
            if (requiresProfile != (profile != null))
            {
                throw new ArgumentException(
                    "This persistence result kind requires exactly the documented profile payload.",
                    nameof(profile));
            }

            Kind = kind;
            Profile = profile;
        }

        public PersistenceResultKind Kind { get; }

        public LocalPlayerProfile Profile { get; }
    }

    public sealed class CatalogPersistenceResult
    {
        public CatalogPersistenceResult(
            PersistenceResultKind kind,
            LocalProfileCatalog catalog = null)
        {
            LocalPersistenceGuard.DefinedEnum(kind, nameof(kind));
            var requiresCatalog = kind == PersistenceResultKind.Created ||
                                  kind == PersistenceResultKind.Committed ||
                                  kind == PersistenceResultKind.Loaded ||
                                  kind == PersistenceResultKind.BackupDegraded;
            if (requiresCatalog != (catalog != null))
            {
                throw new ArgumentException(
                    "This persistence result kind requires exactly the documented catalog payload.",
                    nameof(catalog));
            }

            Kind = kind;
            Catalog = catalog;
        }

        public PersistenceResultKind Kind { get; }

        public LocalProfileCatalog Catalog { get; }
    }

    public interface ICareerSaveRepository
    {
        CareerPersistenceResult Create(CareerSaveSnapshot initialSnapshot, OperationId operationId);

        CareerPersistenceResult Load(ProfileId profileId, SaveId saveId);

        CareerPersistenceResult Commit(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            CareerSaveSnapshot nextSnapshot,
            OperationId operationId);

        CareerPersistenceResult RecoverFromBackup(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken confirmedBackupVersionToken,
            Sha256Digest? confirmedCorruptMainFingerprint,
            OperationId operationId,
            long recoveredAtUtcMs,
            LineageId newLineageId);
    }

    public interface ILocalProfileRepository
    {
        ProfilePersistenceResult Create(LocalPlayerProfile initialProfile, OperationId operationId);

        ProfilePersistenceResult Load(ProfileId profileId);

        ProfilePersistenceResult Commit(
            ProfileId profileId,
            ProfileVersionToken expectedVersionToken,
            LocalPlayerProfile nextProfile,
            OperationId operationId);

        ProfilePersistenceResult RebuildCareerIndex(ProfileId profileId, OperationId operationId);
    }

    public interface ILocalProfileCatalogRepository
    {
        CatalogPersistenceResult LoadOrRebuild(OperationId operationId);

        CatalogPersistenceResult Commit(
            CatalogVersionToken expectedVersionToken,
            LocalProfileCatalog nextCatalog,
            OperationId operationId);
    }

    internal static class LocalPersistenceGuard
    {
        private const long MaximumIJsonSafeInteger = 9007199254740991L;

        public static void ProfileId(ProfileId value, string parameterName)
        {
            if (value.Value == Guid.Empty)
            {
                throw new ArgumentException("A stable profile ID is required.", parameterName);
            }
        }

        public static void SaveId(SaveId value, string parameterName)
        {
            if (value.Value == Guid.Empty)
            {
                throw new ArgumentException("A stable save ID is required.", parameterName);
            }
        }

        public static long PositiveRevision(long value, string parameterName)
        {
            if (value < 1 || value > MaximumIJsonSafeInteger)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A revision must be a positive I-JSON safe integer.");
            }

            return value;
        }

        public static long NonNegativeTimestamp(long value, string parameterName)
        {
            if (value < 0 || value > MaximumIJsonSafeInteger)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A timestamp must be a non-negative I-JSON safe integer.");
            }

            return value;
        }

        public static string RequiredText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty text value is required.", parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        throw new ArgumentException("Text cannot contain an unpaired surrogate.", parameterName);
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(character))
                {
                    throw new ArgumentException("Text cannot contain an unpaired surrogate.", parameterName);
                }
            }

            return value;
        }

        public static void Hash(Sha256Digest hash, string parameterName)
        {
            if (string.IsNullOrEmpty(hash.Value))
            {
                throw new ArgumentException("A SHA-256 hash is required.", parameterName);
            }
        }

        public static void DefinedEnum<T>(T value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Unknown enum value.");
            }
        }
    }
}
