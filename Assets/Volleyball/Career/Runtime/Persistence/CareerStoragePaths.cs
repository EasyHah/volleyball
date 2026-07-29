using System;
using System.IO;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    public enum CareerQuarantineSourceKind
    {
        Main = 1,
        Backup = 2,
        Temporary = 3,
        ReplaceBackup = 4,
        RecoveryIntent = 5,
        Corrupt = 6
    }

    public sealed class CareerStoragePaths
    {
        private const string ProfilesDirectoryName = "Profiles";
        private const string CareersDirectoryName = "Careers";
        private const string QuarantineDirectoryName = "Quarantine";

        public CareerStoragePaths(string persistentDataPath)
        {
            PersistentDataPath = NormalizeRoot(persistentDataPath);
            ProfilesDirectory = WithinRoot(Path.Combine(PersistentDataPath, ProfilesDirectoryName));
            ProfilesIndexPath = WithinRoot(Path.Combine(ProfilesDirectory, "profiles-index.json"));
            ProfilesIndexLockPath = WithinRoot(Path.Combine(ProfilesDirectory, "profiles-index.lock"));
        }

        public string PersistentDataPath { get; }

        public string ProfilesDirectory { get; }

        public string ProfilesIndexPath { get; }

        public string ProfilesIndexLockPath { get; }

        public string ProfilesIndexTemporaryPath(OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                ProfilesDirectory,
                "profiles-index.tmp." + OperationComponent(operationId)));
        }

        public string ProfilesIndexReplaceBackupPath(OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                ProfilesDirectory,
                "profiles-index.replace-backup." + OperationComponent(operationId)));
        }

        public string ProfilesIndexRepairTemporaryPath(OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                ProfilesDirectory,
                "profiles-index.repair.tmp." + OperationComponent(operationId)));
        }

        public string ProfileDirectory(ProfileId profileId)
        {
            return WithinRoot(Path.Combine(ProfilesDirectory, ProfileComponent(profileId)));
        }

        public string ProfilePath(ProfileId profileId)
        {
            return WithinRoot(Path.Combine(ProfileDirectory(profileId), "profile.json"));
        }

        public string ProfileBackupPath(ProfileId profileId)
        {
            return WithinRoot(Path.Combine(ProfileDirectory(profileId), "profile.bak"));
        }

        public string ProfileTemporaryPath(ProfileId profileId, OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                ProfileDirectory(profileId),
                "profile.tmp." + OperationComponent(operationId)));
        }

        public string ProfileReplaceBackupPath(ProfileId profileId, OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                ProfileDirectory(profileId),
                "profile.replace-backup." + OperationComponent(operationId)));
        }

        public string ProfileBackupConvergencePath(ProfileId profileId, OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                ProfileDirectory(profileId),
                "profile.backup-convergence." + OperationComponent(operationId)));
        }

        public string ProfileRepairTemporaryPath(ProfileId profileId, OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                ProfileDirectory(profileId),
                "profile.repair.tmp." + OperationComponent(operationId)));
        }

        public string ProfileLockPath(ProfileId profileId)
        {
            return WithinRoot(Path.Combine(ProfileDirectory(profileId), "profile.lock"));
        }

        public string CareersDirectory(ProfileId profileId)
        {
            return WithinRoot(Path.Combine(ProfileDirectory(profileId), CareersDirectoryName));
        }

        public string CareerPath(ProfileId profileId, SaveId saveId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".json"));
        }

        public string CareerBackupPath(ProfileId profileId, SaveId saveId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".bak"));
        }

        public string CareerTemporaryPath(
            ProfileId profileId,
            SaveId saveId,
            OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".tmp." + OperationComponent(operationId)));
        }

        public string CareerReplaceBackupPath(
            ProfileId profileId,
            SaveId saveId,
            OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".replace-backup." + OperationComponent(operationId)));
        }

        public string CareerBackupConvergencePath(
            ProfileId profileId,
            SaveId saveId,
            OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".backup-convergence." + OperationComponent(operationId)));
        }

        public string CareerRecoveryIntentPath(
            ProfileId profileId,
            SaveId saveId,
            OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".recovery-intent." + OperationComponent(operationId) + ".json"));
        }

        public string CareerRepairTemporaryPath(
            ProfileId profileId,
            SaveId saveId,
            OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".repair.tmp." + OperationComponent(operationId)));
        }

        public string CareerRecoveryTemporaryPath(
            ProfileId profileId,
            SaveId saveId,
            OperationId operationId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".recovery.tmp." + OperationComponent(operationId)));
        }

        public string CareerLockPath(ProfileId profileId, SaveId saveId)
        {
            return WithinRoot(Path.Combine(
                CareersDirectory(profileId),
                SaveComponent(saveId) + ".lock"));
        }

        public string CareerQuarantineDirectory(ProfileId profileId)
        {
            return WithinRoot(Path.Combine(CareersDirectory(profileId), QuarantineDirectoryName));
        }

        public string CareerQuarantinePath(
            ProfileId profileId,
            SaveId saveId,
            CareerQuarantineSourceKind sourceKind,
            OperationId operationId,
            int conflictIndex)
        {
            if (conflictIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(conflictIndex),
                    conflictIndex,
                    "A quarantine conflict index cannot be negative.");
            }

            var fileName = SaveComponent(saveId) + "." + SourceComponent(sourceKind) + "." +
                           OperationComponent(operationId) + "." + conflictIndex + ".json";
            return WithinRoot(Path.Combine(CareerQuarantineDirectory(profileId), fileName));
        }

        private static string NormalizeRoot(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new ArgumentException(
                    "A non-empty persistent data path is required.",
                    nameof(persistentDataPath));
            }

            if (!Path.IsPathRooted(persistentDataPath))
            {
                throw new ArgumentException(
                    "The persistent data path must be absolute.",
                    nameof(persistentDataPath));
            }

            var normalized = Path.GetFullPath(persistentDataPath);
            var pathRoot = Path.GetPathRoot(normalized);
            while (normalized.Length > pathRoot.Length && IsDirectorySeparator(normalized[normalized.Length - 1]))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private string WithinRoot(string candidate)
        {
            var normalized = Path.GetFullPath(candidate);
            var rootPrefix = IsDirectorySeparator(PersistentDataPath[PersistentDataPath.Length - 1])
                ? PersistentDataPath
                : PersistentDataPath + Path.DirectorySeparatorChar;
            if (!normalized.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A storage path escaped the persistent data root.");
            }

            return normalized;
        }

        private static string ProfileComponent(ProfileId profileId)
        {
            return IdentifierComponent(profileId.Value, nameof(profileId));
        }

        private static string SaveComponent(SaveId saveId)
        {
            return IdentifierComponent(saveId.Value, nameof(saveId));
        }

        private static string OperationComponent(OperationId operationId)
        {
            return IdentifierComponent(operationId.Value, nameof(operationId));
        }

        private static string IdentifierComponent(Guid value, string parameterName)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A non-default stable identifier is required for a storage path.",
                    parameterName);
            }

            return value.ToString("D");
        }

        private static string SourceComponent(CareerQuarantineSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case CareerQuarantineSourceKind.Main:
                    return "main";
                case CareerQuarantineSourceKind.Backup:
                    return "backup";
                case CareerQuarantineSourceKind.Temporary:
                    return "temporary";
                case CareerQuarantineSourceKind.ReplaceBackup:
                    return "replace-backup";
                case CareerQuarantineSourceKind.RecoveryIntent:
                    return "recovery-intent";
                case CareerQuarantineSourceKind.Corrupt:
                    return "corrupt";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(sourceKind),
                        sourceKind,
                        "An explicit quarantine source kind is required.");
            }
        }

        private static bool IsDirectorySeparator(char value)
        {
            return value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;
        }
    }
}
