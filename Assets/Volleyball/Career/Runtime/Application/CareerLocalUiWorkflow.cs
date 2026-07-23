using System;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public enum CareerLocalUiWorkflowStatus
    {
        Completed = 0,
        UpToDate = 1,
        UnsafeState = 2,
        PersistenceFailure = 3
    }

    public sealed class CareerUiCommandEnvelope
    {
        public CareerUiCommandEnvelope(OperationId operationId, long completedAtUtcMs)
        {
            CareerUiCommandGuard.OperationId(operationId, nameof(operationId));
            if (completedAtUtcMs < 0 || completedAtUtcMs > 9007199254740991L)
            {
                throw new ArgumentOutOfRangeException(nameof(completedAtUtcMs));
            }

            OperationId = operationId;
            CompletedAtUtcMs = completedAtUtcMs;
        }

        public OperationId OperationId { get; }

        public long CompletedAtUtcMs { get; }
    }

    public sealed class CreateLocalProfileUiCommand
    {
        public CreateLocalProfileUiCommand(
            CareerUiCommandEnvelope envelope,
            ProfileId profileId,
            string displayName)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            CareerUiCommandGuard.ProfileId(profileId, nameof(profileId));
            ProfileId = profileId;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException("A profile display name is required.", nameof(displayName))
                : displayName;
        }

        public CareerUiCommandEnvelope Envelope { get; }

        public ProfileId ProfileId { get; }

        public string DisplayName { get; }
    }

    public sealed class LocalProfileUiCommand
    {
        public LocalProfileUiCommand(CareerUiCommandEnvelope envelope, ProfileId profileId)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            CareerUiCommandGuard.ProfileId(profileId, nameof(profileId));
            ProfileId = profileId;
        }

        public CareerUiCommandEnvelope Envelope { get; }

        public ProfileId ProfileId { get; }
    }

    public sealed class LocalCareerUiCommand
    {
        public LocalCareerUiCommand(
            CareerUiCommandEnvelope envelope,
            ProfileId profileId,
            SaveId saveId)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            CareerUiCommandGuard.ProfileId(profileId, nameof(profileId));
            CareerUiCommandGuard.SaveId(saveId, nameof(saveId));
            ProfileId = profileId;
            SaveId = saveId;
        }

        public CareerUiCommandEnvelope Envelope { get; }

        public ProfileId ProfileId { get; }

        public SaveId SaveId { get; }
    }

    public sealed class RecoverLocalCareerUiCommand
    {
        public RecoverLocalCareerUiCommand(
            CareerUiCommandEnvelope envelope,
            ProfileId profileId,
            SaveId saveId,
            LineageId newLineageId)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            CareerUiCommandGuard.ProfileId(profileId, nameof(profileId));
            CareerUiCommandGuard.SaveId(saveId, nameof(saveId));
            if (newLineageId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A new recovery lineage is required.",
                    nameof(newLineageId));
            }

            ProfileId = profileId;
            SaveId = saveId;
            NewLineageId = newLineageId;
        }

        public CareerUiCommandEnvelope Envelope { get; }
        public ProfileId ProfileId { get; }
        public SaveId SaveId { get; }
        public LineageId NewLineageId { get; }
    }

    public sealed class CareerLocalUiWorkflowResult
    {
        internal CareerLocalUiWorkflowResult(
            CareerLocalUiWorkflowStatus status,
            PersistenceResultKind primaryPersistenceKind,
            PersistenceResultKind? catalogPersistenceKind = null,
            LocalProfileCatalog catalog = null,
            LocalPlayerProfile profile = null,
            CareerSaveSnapshot snapshot = null)
        {
            Status = status;
            PrimaryPersistenceKind = primaryPersistenceKind;
            CatalogPersistenceKind = catalogPersistenceKind;
            Catalog = catalog;
            Profile = profile;
            Snapshot = snapshot;
        }

        public CareerLocalUiWorkflowStatus Status { get; }

        public PersistenceResultKind PrimaryPersistenceKind { get; }

        public PersistenceResultKind? CatalogPersistenceKind { get; }

        public LocalProfileCatalog Catalog { get; }

        public LocalPlayerProfile Profile { get; }

        public CareerSaveSnapshot Snapshot { get; }
    }

    public sealed class CareerLocalUiWorkflow
    {
        private static readonly Sha256Digest UnsealedHash =
            new Sha256Digest(new string('0', CareerSeed.HexLength));

        private readonly ILocalProfileRepository _profiles;
        private readonly ILocalProfileCatalogRepository _catalog;
        private readonly ICareerSaveRepository _careers;

        public CareerLocalUiWorkflow(
            ILocalProfileRepository profiles,
            ILocalProfileCatalogRepository catalog,
            ICareerSaveRepository careers)
        {
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _careers = careers ?? throw new ArgumentNullException(nameof(careers));
        }

        public CareerLocalUiWorkflowResult ListProfiles(CareerUiCommandEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            var result = _catalog.LoadOrRebuild(envelope.OperationId);
            return new CareerLocalUiWorkflowResult(
                IsReadable(result.Kind)
                    ? CareerLocalUiWorkflowStatus.Completed
                    : CareerLocalUiWorkflowStatus.PersistenceFailure,
                result.Kind,
                catalog: result.Catalog);
        }

        public CareerLocalUiWorkflowResult CreateProfile(CreateLocalProfileUiCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var profile = new LocalPlayerProfile(
                LocalPlayerProfile.CurrentSchemaVersion,
                command.ProfileId,
                1,
                UnsealedHash,
                command.DisplayName,
                command.Envelope.CompletedAtUtcMs,
                command.Envelope.CompletedAtUtcMs,
                Array.Empty<CareerIndexEntry>());
            var created = _profiles.Create(profile, command.Envelope.OperationId);
            LocalPlayerProfile authoritativeProfile;
            if (IsCreated(created.Kind))
            {
                authoritativeProfile = created.Profile;
            }
            else if (created.Kind == PersistenceResultKind.AlreadyExists)
            {
                var loaded = _profiles.Load(command.ProfileId);
                if (!IsReadable(loaded.Kind) ||
                    !MatchesCreateCommand(loaded.Profile, command))
                {
                    return new CareerLocalUiWorkflowResult(
                        CareerLocalUiWorkflowStatus.PersistenceFailure,
                        created.Kind,
                        profile: loaded.Profile);
                }

                authoritativeProfile = loaded.Profile;
            }
            else
            {
                return new CareerLocalUiWorkflowResult(
                    CareerLocalUiWorkflowStatus.PersistenceFailure,
                    created.Kind,
                    profile: created.Profile);
            }

            var catalog = _catalog.LoadOrRebuild(command.Envelope.OperationId);
            return new CareerLocalUiWorkflowResult(
                IsReadable(catalog.Kind)
                    ? CareerLocalUiWorkflowStatus.Completed
                    : CareerLocalUiWorkflowStatus.PersistenceFailure,
                created.Kind,
                catalog.Kind,
                catalog.Catalog,
                authoritativeProfile);
        }

        public CareerLocalUiWorkflowResult LoadProfile(LocalProfileUiCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var result = _profiles.Load(command.ProfileId);
            return new CareerLocalUiWorkflowResult(
                IsReadable(result.Kind)
                    ? CareerLocalUiWorkflowStatus.Completed
                    : CareerLocalUiWorkflowStatus.PersistenceFailure,
                result.Kind,
                profile: result.Profile);
        }

        public CareerLocalUiWorkflowResult RefreshCareerIndex(LocalProfileUiCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var result = _profiles.RebuildCareerIndex(
                command.ProfileId,
                command.Envelope.OperationId);
            return new CareerLocalUiWorkflowResult(
                IsReadable(result.Kind)
                    ? CareerLocalUiWorkflowStatus.Completed
                    : CareerLocalUiWorkflowStatus.PersistenceFailure,
                result.Kind,
                profile: result.Profile);
        }

        public CareerLocalUiWorkflowResult LoadCareer(LocalCareerUiCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return LoadCareer(command, false);
        }

        public CareerLocalUiWorkflowResult SaveNow(LocalCareerUiCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return LoadCareer(command, true);
        }

        public CareerLocalUiWorkflowResult RecoverCareer(
            RecoverLocalCareerUiCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var inspected = _careers.Load(command.ProfileId, command.SaveId);
            if (inspected.Kind != PersistenceResultKind.RecoveryAvailable ||
                !inspected.RecoverableBackup.HasValue)
            {
                return new CareerLocalUiWorkflowResult(
                    CareerLocalUiWorkflowStatus.PersistenceFailure,
                    inspected.Kind,
                    snapshot: inspected.Snapshot);
            }

            var recovered = _careers.RecoverFromBackup(
                command.ProfileId,
                command.SaveId,
                inspected.RecoverableBackup.Value,
                inspected.UnreadableMainFingerprint,
                command.Envelope.OperationId,
                command.Envelope.CompletedAtUtcMs,
                command.NewLineageId);
            return new CareerLocalUiWorkflowResult(
                IsReadable(recovered.Kind)
                    ? CareerLocalUiWorkflowStatus.Completed
                    : CareerLocalUiWorkflowStatus.PersistenceFailure,
                recovered.Kind,
                snapshot: recovered.Snapshot);
        }

        private CareerLocalUiWorkflowResult LoadCareer(
            LocalCareerUiCommand command,
            bool requirePlanning)
        {
            var result = _careers.Load(command.ProfileId, command.SaveId);
            if (!IsReadable(result.Kind))
            {
                return new CareerLocalUiWorkflowResult(
                    CareerLocalUiWorkflowStatus.PersistenceFailure,
                    result.Kind);
            }

            var status = requirePlanning
                ? result.Snapshot.Progression.Kind == CareerProgressionKind.Planning
                    ? CareerLocalUiWorkflowStatus.UpToDate
                    : CareerLocalUiWorkflowStatus.UnsafeState
                : CareerLocalUiWorkflowStatus.Completed;
            return new CareerLocalUiWorkflowResult(
                status,
                result.Kind,
                snapshot: result.Snapshot);
        }

        private static bool IsCreated(PersistenceResultKind kind)
        {
            return kind == PersistenceResultKind.Created ||
                   kind == PersistenceResultKind.BackupDegraded;
        }

        private static bool IsReadable(PersistenceResultKind kind)
        {
            return kind == PersistenceResultKind.Created ||
                   kind == PersistenceResultKind.Committed ||
                   kind == PersistenceResultKind.Loaded ||
                   kind == PersistenceResultKind.BackupDegraded;
        }

        private static bool MatchesCreateCommand(
            LocalPlayerProfile profile,
            CreateLocalProfileUiCommand command)
        {
            return profile != null &&
                   profile.ProfileId.Equals(command.ProfileId) &&
                   string.Equals(
                       profile.DisplayName,
                       command.DisplayName,
                       StringComparison.Ordinal) &&
                   profile.CreatedAtUtcMs == command.Envelope.CompletedAtUtcMs &&
                   profile.UpdatedAtUtcMs == command.Envelope.CompletedAtUtcMs;
        }
    }

    internal static class CareerUiCommandGuard
    {
        public static void OperationId(OperationId value, string parameterName)
        {
            if (value.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A stable operation ID is required.",
                    parameterName);
            }
        }

        public static void ProfileId(ProfileId value, string parameterName)
        {
            if (value.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A stable profile ID is required.",
                    parameterName);
            }
        }

        public static void SaveId(SaveId value, string parameterName)
        {
            if (value.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A stable save ID is required.",
                    parameterName);
            }
        }
    }
}
