using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    public sealed class LocalPlayerProfileRepository : ILocalProfileRepository
    {
        private static readonly ConcurrentDictionary<string, object> ProcessGates =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly CareerStoragePaths _paths;
        private readonly IAtomicFileSystem _fileSystem;

        public LocalPlayerProfileRepository(
            CareerStoragePaths paths,
            IAtomicFileSystem fileSystem)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public ProfilePersistenceResult Create(
            LocalPlayerProfile initialProfile,
            OperationId operationId)
        {
            if (initialProfile == null)
            {
                throw new ArgumentNullException(nameof(initialProfile));
            }

            RequireOperationId(operationId);
            if (initialProfile.ProfileRevision != 1)
            {
                throw new ArgumentException(
                    "A new local profile must begin at revision 1.",
                    nameof(initialProfile));
            }

            var profile = LocalProfileJsonCodec.SealProfile(initialProfile);
            var bytes = LocalProfileJsonCodec.SerializeProfile(profile);
            var gate = Gate(profile.ProfileId);
            lock (gate)
            {
                try
                {
                    EnsureProfileDirectories(profile.ProfileId);
                }
                catch (IOException)
                {
                    return Result(PersistenceResultKind.IoFailure);
                }
                catch (UnauthorizedAccessException)
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                var lockResult = TryAcquireLock(profile.ProfileId, out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    return CreateLocked(profile, bytes, operationId);
                }
            }
        }

        public ProfilePersistenceResult Load(ProfileId profileId)
        {
            RequireProfileId(profileId);
            if (!_fileSystem.DirectoryExists(_paths.ProfileDirectory(profileId)))
            {
                return Result(PersistenceResultKind.NotFound);
            }

            var gate = Gate(profileId);
            lock (gate)
            {
                var lockResult = TryAcquireLock(profileId, out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    try
                    {
                        return LoadLocked(profileId);
                    }
                    catch (IOException)
                    {
                        return Result(PersistenceResultKind.IoFailure);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return Result(PersistenceResultKind.IoFailure);
                    }
                }
            }
        }

        public ProfilePersistenceResult Commit(
            ProfileId profileId,
            ProfileVersionToken expectedVersionToken,
            LocalPlayerProfile nextProfile,
            OperationId operationId)
        {
            RequireProfileId(profileId);
            RequireOperationId(operationId);
            if (nextProfile == null)
            {
                throw new ArgumentNullException(nameof(nextProfile));
            }

            if (!nextProfile.ProfileId.Equals(profileId) ||
                nextProfile.ProfileRevision != expectedVersionToken.Revision + 1)
            {
                throw new ArgumentException(
                    "A profile commit must keep ownership and advance one revision.",
                    nameof(nextProfile));
            }

            var profile = LocalProfileJsonCodec.SealProfile(nextProfile);
            var bytes = LocalProfileJsonCodec.SerializeProfile(profile);
            if (!_fileSystem.DirectoryExists(_paths.ProfileDirectory(profileId)))
            {
                return Result(PersistenceResultKind.NotFound);
            }

            var gate = Gate(profileId);
            lock (gate)
            {
                var lockResult = TryAcquireLock(profileId, out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    return CommitLocked(
                        profileId,
                        expectedVersionToken,
                        profile,
                        bytes,
                        operationId);
                }
            }
        }

        public ProfilePersistenceResult RebuildCareerIndex(
            ProfileId profileId,
            OperationId operationId)
        {
            RequireProfileId(profileId);
            RequireOperationId(operationId);
            var loaded = Load(profileId);
            if (loaded.Kind != PersistenceResultKind.Loaded)
            {
                return loaded;
            }

            IReadOnlyList<CareerIndexEntry> rebuilt;
            try
            {
                rebuilt = BuildCareerIndex(loaded.Profile);
                if (rebuilt == null)
                {
                    return Result(PersistenceResultKind.UnsupportedVersion);
                }
            }
            catch (IOException)
            {
                return Result(PersistenceResultKind.IoFailure);
            }
            catch (UnauthorizedAccessException)
            {
                return Result(PersistenceResultKind.IoFailure);
            }

            if (Equivalent(loaded.Profile.CareerEntries, rebuilt))
            {
                return loaded;
            }

            var updatedAtUtcMs = loaded.Profile.UpdatedAtUtcMs;
            for (var index = 0; index < rebuilt.Count; index++)
            {
                updatedAtUtcMs = Math.Max(updatedAtUtcMs, rebuilt[index].UpdatedAtUtcMs);
            }

            var candidate = new LocalPlayerProfile(
                LocalPlayerProfile.CurrentSchemaVersion,
                profileId,
                loaded.Profile.ProfileRevision + 1,
                loaded.Profile.ProfileSnapshotHash,
                loaded.Profile.DisplayName,
                loaded.Profile.CreatedAtUtcMs,
                updatedAtUtcMs,
                rebuilt);
            return Commit(
                profileId,
                loaded.Profile.VersionToken,
                candidate,
                operationId);
        }

        private ProfilePersistenceResult CreateLocked(
            LocalPlayerProfile profile,
            byte[] bytes,
            OperationId operationId)
        {
            var mainPath = _paths.ProfilePath(profile.ProfileId);
            var temporaryPath = _paths.ProfileTemporaryPath(profile.ProfileId, operationId);
            if (_fileSystem.FileExists(mainPath))
            {
                var current = ReadCandidate(mainPath, profile.ProfileId);
                if (current.Kind == CandidateKind.Unsupported)
                {
                    return Result(PersistenceResultKind.UnsupportedVersion);
                }

                return current.Kind == CandidateKind.Invalid
                    ? Result(PersistenceResultKind.Corrupt)
                    : Result(PersistenceResultKind.AlreadyExists);
            }

            if (HasUnresolvedProfileOperation(profile.ProfileId))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var fixedBackup = ReadCandidate(
                _paths.ProfileBackupPath(profile.ProfileId),
                profile.ProfileId);
            if (fixedBackup.Kind == CandidateKind.Valid)
            {
                return Result(PersistenceResultKind.RecoveryAvailable);
            }

            if (fixedBackup.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            if (fixedBackup.Kind == CandidateKind.Invalid)
            {
                return Result(PersistenceResultKind.Corrupt);
            }

            if (_fileSystem.FileExists(temporaryPath))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            try
            {
                _fileSystem.CreateFileDurably(temporaryPath, bytes);
                var temporary = ReadCandidate(temporaryPath, profile.ProfileId);
                if (!temporary.HasToken(profile.VersionToken))
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                    temporaryPath,
                    mainPath);
            }
            catch (IOException)
            {
                // Rescan below decides whether the atomic move published the profile.
            }
            catch (UnauthorizedAccessException)
            {
                // Rescan below decides whether the atomic move published the profile.
            }

            var published = ReadCandidate(mainPath, profile.ProfileId);
            if (published.HasToken(profile.VersionToken))
            {
                return Result(PersistenceResultKind.Created, published.Profile);
            }

            return published.Kind == CandidateKind.Missing
                ? Result(PersistenceResultKind.NotCommitted)
                : Result(PersistenceResultKind.AlreadyExists);
        }

        private ProfilePersistenceResult CommitLocked(
            ProfileId profileId,
            ProfileVersionToken expectedVersionToken,
            LocalPlayerProfile nextProfile,
            byte[] bytes,
            OperationId operationId)
        {
            try
            {
                if (HasUnresolvedProfileOperation(profileId))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                var current = ReadCandidate(_paths.ProfilePath(profileId), profileId);
                if (current.Kind != CandidateKind.Valid)
                {
                    return ResultForUnreadable(current, profileId);
                }

                if (!current.Profile.VersionToken.Equals(expectedVersionToken))
                {
                    return Result(PersistenceResultKind.VersionConflict);
                }

                var temporaryPath = _paths.ProfileTemporaryPath(profileId, operationId);
                var operationBackupPath = _paths.ProfileReplaceBackupPath(profileId, operationId);
                if (_fileSystem.FileExists(temporaryPath) ||
                    _fileSystem.FileExists(operationBackupPath))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                _fileSystem.CreateFileDurably(temporaryPath, bytes);
                if (!ReadCandidate(temporaryPath, profileId).HasToken(nextProfile.VersionToken))
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                try
                {
                    _fileSystem.ReplaceFileWithOperationBackup(
                        temporaryPath,
                        _paths.ProfilePath(profileId),
                        operationBackupPath);
                }
                catch (IOException)
                {
                    // The complete rescan below is authoritative.
                }
                catch (UnauthorizedAccessException)
                {
                    // The complete rescan below is authoritative.
                }

                return ResolveUpdateLocked(
                    profileId,
                    expectedVersionToken,
                    nextProfile.VersionToken,
                    operationId);
            }
            catch (IOException)
            {
                return Result(PersistenceResultKind.IoFailure);
            }
            catch (UnauthorizedAccessException)
            {
                return Result(PersistenceResultKind.IoFailure);
            }
        }

        private bool HasUnresolvedProfileOperation(ProfileId profileId)
        {
            return FindProfileCandidates(profileId, "profile.replace-backup.").Count != 0 ||
                   FindProfileCandidates(profileId, "profile.backup-convergence.").Count != 0 ||
                   FindProfileCandidates(profileId, "profile.tmp.").Count != 0 ||
                   FindProfileCandidates(profileId, "profile.repair.tmp.").Count != 0;
        }

        private ProfilePersistenceResult ResolveUpdateLocked(
            ProfileId profileId,
            ProfileVersionToken expected,
            ProfileVersionToken next,
            OperationId operationId)
        {
            var main = ReadCandidate(_paths.ProfilePath(profileId), profileId);
            var operationBackupPath = _paths.ProfileReplaceBackupPath(profileId, operationId);
            var operationBackup = ReadCandidate(operationBackupPath, profileId);
            if (main.HasToken(next))
            {
                var healthy = ReadCandidate(
                    _paths.ProfileBackupPath(profileId),
                    profileId).HasToken(expected);
                if (!healthy)
                {
                    healthy = TryConvergeBackup(
                        profileId,
                        expected,
                        operationId,
                        operationBackup);
                }

                return Result(
                    healthy
                        ? PersistenceResultKind.Committed
                        : PersistenceResultKind.BackupDegraded,
                    main.Profile);
            }

            if (main.HasToken(expected))
            {
                return Result(PersistenceResultKind.NotCommitted);
            }

            if (main.Kind == CandidateKind.Missing && operationBackup.HasToken(expected))
            {
                return RestoreExpectedMain(profileId, expected, operationId, operationBackup);
            }

            return Result(PersistenceResultKind.AmbiguousReplaceState);
        }

        private bool TryConvergeBackup(
            ProfileId profileId,
            ProfileVersionToken expected,
            OperationId operationId,
            Candidate operationBackup)
        {
            if (!operationBackup.HasToken(expected))
            {
                return false;
            }

            var operationBackupPath = _paths.ProfileReplaceBackupPath(profileId, operationId);
            var fixedBackupPath = _paths.ProfileBackupPath(profileId);
            try
            {
                if (!_fileSystem.FileExists(fixedBackupPath))
                {
                    _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                        operationBackupPath,
                        fixedBackupPath);
                }
                else
                {
                    var convergencePath = _paths.ProfileBackupConvergencePath(
                        profileId,
                        operationId);
                    if (_fileSystem.FileExists(convergencePath))
                    {
                        return false;
                    }

                    try
                    {
                        _fileSystem.ReplaceFileWithOperationBackup(
                            operationBackupPath,
                            fixedBackupPath,
                            convergencePath);
                    }
                    catch (IOException)
                    {
                        // Fixed-backup verification below decides convergence.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Fixed-backup verification below decides convergence.
                    }
                }
            }
            catch (IOException)
            {
                // The fixed backup is rescanned below because the move/replace may have committed.
            }
            catch (UnauthorizedAccessException)
            {
                // The fixed backup is rescanned below because the move/replace may have committed.
            }

            if (!ReadCandidate(fixedBackupPath, profileId).HasToken(expected))
            {
                return false;
            }

            TryDelete(operationBackupPath);
            TryDelete(_paths.ProfileBackupConvergencePath(profileId, operationId));
            return true;
        }

        private ProfilePersistenceResult RestoreExpectedMain(
            ProfileId profileId,
            ProfileVersionToken expected,
            OperationId operationId,
            Candidate operationBackup)
        {
            var repairPath = _paths.ProfileRepairTemporaryPath(profileId, operationId);
            if (_fileSystem.FileExists(repairPath))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            try
            {
                _fileSystem.CreateFileDurably(
                    repairPath,
                    LocalProfileJsonCodec.SerializeProfile(operationBackup.Profile));
                if (!ReadCandidate(repairPath, profileId).HasToken(expected))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                try
                {
                    _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                        repairPath,
                        _paths.ProfilePath(profileId));
                }
                catch (IOException)
                {
                    // Main-file verification below decides repair publication.
                }
                catch (UnauthorizedAccessException)
                {
                    // Main-file verification below decides repair publication.
                }

                if (!ReadCandidate(_paths.ProfilePath(profileId), profileId).HasToken(expected))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                TryDelete(_paths.ProfileReplaceBackupPath(profileId, operationId));
                TryDelete(_paths.ProfileTemporaryPath(profileId, operationId));
                return Result(PersistenceResultKind.NotCommitted);
            }
            catch (IOException)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }
            catch (UnauthorizedAccessException)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }
        }

        private ProfilePersistenceResult LoadLocked(ProfileId profileId)
        {
            var convergenceFiles = FindProfileCandidates(
                profileId,
                "profile.backup-convergence.");
            if (convergenceFiles.Count > 1)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var operationBackups = FindProfileCandidates(profileId, "profile.replace-backup.");
            if (operationBackups.Count > 1)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            if (convergenceFiles.Count == 1 && operationBackups.Count == 0)
            {
                var convergenceResult = ResolveStandaloneBackupConvergence(
                    profileId,
                    convergenceFiles[0]);
                if (convergenceResult != null)
                {
                    return convergenceResult;
                }
            }

            if (operationBackups.Count == 1)
            {
                var interrupted = ResolveInterruptedUpdate(profileId, operationBackups[0]);
                if (interrupted != null)
                {
                    return interrupted;
                }
            }

            var main = ReadCandidate(_paths.ProfilePath(profileId), profileId);
            if (main.Kind == CandidateKind.Valid)
            {
                return Result(PersistenceResultKind.Loaded, main.Profile);
            }

            if (main.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            var backup = ReadCandidate(_paths.ProfileBackupPath(profileId), profileId);
            if (backup.Kind == CandidateKind.Valid)
            {
                return Result(PersistenceResultKind.RecoveryAvailable);
            }

            if (backup.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            if (main.Kind == CandidateKind.Missing && backup.Kind == CandidateKind.Missing)
            {
                return Result(PersistenceResultKind.NotFound);
            }

            return Result(PersistenceResultKind.Corrupt);
        }

        private ProfilePersistenceResult ResolveStandaloneBackupConvergence(
            ProfileId profileId,
            string convergencePath)
        {
            if (!TryOperationId(
                    convergencePath,
                    "profile.backup-convergence.",
                    out _))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var main = ReadCandidate(_paths.ProfilePath(profileId), profileId);
            var fixedBackup = ReadCandidate(_paths.ProfileBackupPath(profileId), profileId);
            var displacedBackup = ReadCandidate(convergencePath, profileId);
            var uniquelyCompleted = main.Kind == CandidateKind.Valid &&
                                    fixedBackup.Kind == CandidateKind.Valid &&
                                    displacedBackup.Kind == CandidateKind.Valid &&
                                    main.Profile.ProfileRevision ==
                                    fixedBackup.Profile.ProfileRevision + 1 &&
                                    displacedBackup.Profile.ProfileRevision <=
                                    fixedBackup.Profile.ProfileRevision;
            if (!uniquelyCompleted)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            TryDelete(convergencePath);
            return _fileSystem.FileExists(convergencePath)
                ? Result(PersistenceResultKind.AmbiguousReplaceState)
                : null;
        }

        private ProfilePersistenceResult ResolveInterruptedUpdate(
            ProfileId profileId,
            string operationBackupPath)
        {
            if (!TryOperationId(operationBackupPath, "profile.replace-backup.", out var operationId))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var main = ReadCandidate(_paths.ProfilePath(profileId), profileId);
            var backup = ReadCandidate(operationBackupPath, profileId);
            if (main.Kind == CandidateKind.Missing && backup.Kind == CandidateKind.Valid)
            {
                return RestoreExpectedMain(
                    profileId,
                    backup.Profile.VersionToken,
                    operationId,
                    backup);
            }

            if (main.Kind == CandidateKind.Valid && backup.Kind == CandidateKind.Valid &&
                main.Profile.VersionToken.Equals(backup.Profile.VersionToken))
            {
                TryDelete(operationBackupPath);
                TryDelete(_paths.ProfileTemporaryPath(profileId, operationId));
                return Result(PersistenceResultKind.Loaded, main.Profile);
            }

            if (main.Kind == CandidateKind.Valid && backup.Kind == CandidateKind.Valid &&
                main.Profile.ProfileRevision == backup.Profile.ProfileRevision + 1)
            {
                var healthy = TryConvergeBackup(
                    profileId,
                    backup.Profile.VersionToken,
                    operationId,
                    backup);
                return Result(
                    healthy
                        ? PersistenceResultKind.Loaded
                        : PersistenceResultKind.BackupDegraded,
                    main.Profile);
            }

            return Result(PersistenceResultKind.AmbiguousReplaceState);
        }

        private ProfilePersistenceResult ResultForUnreadable(
            Candidate current,
            ProfileId profileId)
        {
            if (current.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            var backup = ReadCandidate(_paths.ProfileBackupPath(profileId), profileId);
            if (backup.Kind == CandidateKind.Valid)
            {
                return Result(PersistenceResultKind.RecoveryAvailable);
            }

            return current.Kind == CandidateKind.Missing
                ? Result(PersistenceResultKind.NotFound)
                : Result(PersistenceResultKind.Corrupt);
        }

        private IReadOnlyList<CareerIndexEntry> BuildCareerIndex(LocalPlayerProfile profile)
        {
            var existing = new Dictionary<SaveId, CareerIndexEntry>();
            for (var index = 0; index < profile.CareerEntries.Count; index++)
            {
                existing.Add(profile.CareerEntries[index].SaveId, profile.CareerEntries[index]);
            }

            var discoveredIds = new HashSet<SaveId>();
            var careersDirectory = _paths.CareersDirectory(profile.ProfileId);
            if (_fileSystem.DirectoryExists(careersDirectory))
            {
                var files = _fileSystem.EnumerateFiles(careersDirectory);
                for (var index = 0; index < files.Count; index++)
                {
                    if (TrySaveId(Path.GetFileName(files[index]), out var saveId))
                    {
                        discoveredIds.Add(saveId);
                    }
                }
            }

            var entries = new List<CareerIndexEntry>();
            foreach (var saveId in discoveredIds)
            {
                var mainPath = _paths.CareerPath(profile.ProfileId, saveId);
                if (HasUnsupportedCareerVersions(mainPath))
                {
                    return null;
                }

                var main = ReadCareer(
                    mainPath,
                    profile.ProfileId,
                    saveId);
                if (main != null)
                {
                    entries.Add(CareerIndexEntry.FromSnapshot(main));
                    continue;
                }

                var backupPath = _paths.CareerBackupPath(profile.ProfileId, saveId);
                if (HasUnsupportedCareerVersions(backupPath))
                {
                    return null;
                }

                var backup = ReadCareer(
                    backupPath,
                    profile.ProfileId,
                    saveId);
                if (backup != null)
                {
                    entries.Add(CareerIndexEntry.FromSnapshot(
                        backup,
                        CareerLoadability.RecoveryAvailable));
                    continue;
                }

                if (existing.TryGetValue(saveId, out var prior))
                {
                    entries.Add(Clone(prior, CareerLoadability.Corrupt));
                }
            }

            foreach (var pair in existing)
            {
                if (!discoveredIds.Contains(pair.Key))
                {
                    entries.Add(Clone(pair.Value, CareerLoadability.Missing));
                }
            }

            entries.Sort((left, right) => string.CompareOrdinal(
                left.SaveId.ToString(),
                right.SaveId.ToString()));
            return entries.AsReadOnly();
        }

        private bool HasUnsupportedCareerVersions(string path)
        {
            if (!_fileSystem.FileExists(path))
            {
                return false;
            }

            try
            {
                var root = StrictJsonReader.Parse(_fileSystem.ReadAllBytes(path));
                if (root.Kind != StrictJsonKind.Object)
                {
                    return false;
                }

                var versionsValue = root.ObjectValue.Get("versions");
                if (versionsValue.Kind != StrictJsonKind.Object)
                {
                    return false;
                }

                var versions = versionsValue.ObjectValue;
                return VersionInteger(versions.Get("schemaVersion")) !=
                       CareerSaveVersions.Current.SchemaVersion ||
                       VersionInteger(versions.Get("contentVersion")) !=
                       CareerSaveVersions.Current.ContentVersion ||
                       VersionInteger(versions.Get("rulesetVersion")) !=
                       CareerSaveVersions.Current.RulesetVersion ||
                       VersionInteger(versions.Get("careerRandomAlgorithmVersion")) !=
                       CareerSaveVersions.Current.CareerRandomAlgorithmVersion;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private static long VersionInteger(StrictJsonValue value)
        {
            if (value.Kind != StrictJsonKind.Integer)
            {
                throw new FormatException("A version field must be an integer.");
            }

            return value.IntegerValue;
        }

        private CareerSaveSnapshot ReadCareer(string path, ProfileId profileId, SaveId saveId)
        {
            if (!_fileSystem.FileExists(path))
            {
                return null;
            }

            try
            {
                var snapshot = CareerSaveJsonCodec.Deserialize(_fileSystem.ReadAllBytes(path));
                return snapshot.Identity.ProfileId.Equals(profileId) &&
                       snapshot.Identity.SaveId.Equals(saveId)
                    ? snapshot
                    : null;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private Candidate ReadCandidate(string path, ProfileId profileId)
        {
            if (!_fileSystem.FileExists(path))
            {
                return Candidate.Missing();
            }

            var bytes = _fileSystem.ReadAllBytes(path);
            if (HasUnsupportedSchema(bytes))
            {
                return Candidate.Unsupported();
            }

            try
            {
                var profile = LocalProfileJsonCodec.DeserializeProfile(bytes);
                return profile.ProfileId.Equals(profileId)
                    ? Candidate.Valid(profile)
                    : Candidate.Invalid();
            }
            catch (FormatException)
            {
                return Candidate.Invalid();
            }
            catch (ArgumentException)
            {
                return Candidate.Invalid();
            }
        }

        private static bool HasUnsupportedSchema(byte[] bytes)
        {
            try
            {
                var root = StrictJsonReader.Parse(bytes);
                return root.Kind == StrictJsonKind.Object &&
                       root.ObjectValue.Get("schemaVersion").Kind == StrictJsonKind.Integer &&
                       root.ObjectValue.Get("schemaVersion").IntegerValue !=
                       LocalPlayerProfile.CurrentSchemaVersion;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private IReadOnlyList<string> FindProfileCandidates(ProfileId profileId, string prefix)
        {
            var directory = _paths.ProfileDirectory(profileId);
            if (!_fileSystem.DirectoryExists(directory))
            {
                return Array.Empty<string>();
            }

            var matches = new List<string>();
            var files = _fileSystem.EnumerateFiles(directory);
            for (var index = 0; index < files.Count; index++)
            {
                if (Path.GetFileName(files[index]).StartsWith(prefix, StringComparison.Ordinal))
                {
                    matches.Add(files[index]);
                }
            }

            return matches.AsReadOnly();
        }

        private ProfilePersistenceResult TryAcquireLock(
            ProfileId profileId,
            out IAtomicFileLock fileLock)
        {
            try
            {
                fileLock = _fileSystem.AcquireExclusiveLock(_paths.ProfileLockPath(profileId));
                return null;
            }
            catch (IOException)
            {
                fileLock = null;
                return Result(PersistenceResultKind.LockUnavailable);
            }
            catch (UnauthorizedAccessException)
            {
                fileLock = null;
                return Result(PersistenceResultKind.LockUnavailable);
            }
        }

        private void EnsureProfileDirectories(ProfileId profileId)
        {
            _fileSystem.CreateDirectory(_paths.ProfilesDirectory);
            _fileSystem.CreateDirectory(_paths.ProfileDirectory(profileId));
            _fileSystem.CreateDirectory(_paths.CareersDirectory(profileId));
        }

        private void TryDelete(string path)
        {
            try
            {
                if (_fileSystem.FileExists(path))
                {
                    _fileSystem.DeleteFile(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static CareerIndexEntry Clone(
            CareerIndexEntry source,
            CareerLoadability loadability)
        {
            return new CareerIndexEntry(
                source.SaveId,
                source.CareerName,
                source.PlayerDisplayName,
                source.ProgressionKind,
                source.Phase,
                source.Season,
                source.Week,
                source.UpdatedAtUtcMs,
                loadability);
        }

        private static bool Equivalent(
            IReadOnlyList<CareerIndexEntry> left,
            IReadOnlyList<CareerIndexEntry> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                var first = left[index];
                var second = right[index];
                if (!first.SaveId.Equals(second.SaveId) ||
                    !string.Equals(first.CareerName, second.CareerName, StringComparison.Ordinal) ||
                    !string.Equals(
                        first.PlayerDisplayName,
                        second.PlayerDisplayName,
                        StringComparison.Ordinal) ||
                    first.ProgressionKind != second.ProgressionKind ||
                    first.Phase != second.Phase ||
                    first.Season != second.Season ||
                    first.Week != second.Week ||
                    first.UpdatedAtUtcMs != second.UpdatedAtUtcMs ||
                    first.Loadability != second.Loadability)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TrySaveId(string fileName, out SaveId saveId)
        {
            string candidate = null;
            if (fileName.Length == 41 && fileName.EndsWith(".json", StringComparison.Ordinal))
            {
                candidate = fileName.Substring(0, 36);
            }
            else if (fileName.Length == 40 && fileName.EndsWith(".bak", StringComparison.Ordinal))
            {
                candidate = fileName.Substring(0, 36);
            }

            if (Guid.TryParseExact(candidate, "D", out var parsed) && parsed != Guid.Empty &&
                string.Equals(parsed.ToString("D"), candidate, StringComparison.Ordinal))
            {
                saveId = new SaveId(parsed);
                return true;
            }

            saveId = default;
            return false;
        }

        private static bool TryOperationId(
            string path,
            string prefix,
            out OperationId operationId)
        {
            var fileName = Path.GetFileName(path);
            var value = fileName.StartsWith(prefix, StringComparison.Ordinal)
                ? fileName.Substring(prefix.Length)
                : null;
            if (Guid.TryParseExact(value, "D", out var parsed) && parsed != Guid.Empty &&
                string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal))
            {
                operationId = new OperationId(parsed);
                return true;
            }

            operationId = default;
            return false;
        }

        private static object Gate(ProfileId profileId)
        {
            return ProcessGates.GetOrAdd(profileId.ToString(), _ => new object());
        }

        private static void RequireProfileId(ProfileId profileId)
        {
            if (profileId.Value == Guid.Empty)
            {
                throw new ArgumentException("A stable profile ID is required.", nameof(profileId));
            }
        }

        private static void RequireOperationId(OperationId operationId)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException("A stable operation ID is required.", nameof(operationId));
            }
        }

        private static ProfilePersistenceResult Result(
            PersistenceResultKind kind,
            LocalPlayerProfile profile = null)
        {
            return new ProfilePersistenceResult(kind, profile);
        }

        private enum CandidateKind
        {
            Missing,
            Valid,
            Unsupported,
            Invalid
        }

        private sealed class Candidate
        {
            private Candidate(CandidateKind kind, LocalPlayerProfile profile)
            {
                Kind = kind;
                Profile = profile;
            }

            public CandidateKind Kind { get; }

            public LocalPlayerProfile Profile { get; }

            public bool HasToken(ProfileVersionToken token)
            {
                return Kind == CandidateKind.Valid && Profile.VersionToken.Equals(token);
            }

            public static Candidate Missing()
            {
                return new Candidate(CandidateKind.Missing, null);
            }

            public static Candidate Valid(LocalPlayerProfile profile)
            {
                return new Candidate(CandidateKind.Valid, profile);
            }

            public static Candidate Unsupported()
            {
                return new Candidate(CandidateKind.Unsupported, null);
            }

            public static Candidate Invalid()
            {
                return new Candidate(CandidateKind.Invalid, null);
            }
        }
    }
}
