using System;
using System.Collections.Generic;
using System.IO;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    public sealed class LocalProfileCatalogRepository : ILocalProfileCatalogRepository
    {
        private static readonly object ProcessGate = new object();

        private readonly CareerStoragePaths _paths;
        private readonly IAtomicFileSystem _fileSystem;

        public LocalProfileCatalogRepository(
            CareerStoragePaths paths,
            IAtomicFileSystem fileSystem)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public CatalogPersistenceResult LoadOrRebuild(OperationId operationId)
        {
            RequireOperationId(operationId);
            lock (ProcessGate)
            {
                try
                {
                    _fileSystem.CreateDirectory(_paths.ProfilesDirectory);
                }
                catch (IOException)
                {
                    return Result(PersistenceResultKind.IoFailure);
                }
                catch (UnauthorizedAccessException)
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                var lockResult = TryAcquireLock(out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    try
                    {
                        return LoadOrRebuildLocked(operationId);
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

        public CatalogPersistenceResult Commit(
            CatalogVersionToken expectedVersionToken,
            LocalProfileCatalog nextCatalog,
            OperationId operationId)
        {
            RequireOperationId(operationId);
            if (nextCatalog == null)
            {
                throw new ArgumentNullException(nameof(nextCatalog));
            }

            if (nextCatalog.CatalogRevision != expectedVersionToken.Revision + 1)
            {
                throw new ArgumentException(
                    "A catalog commit must advance exactly one revision.",
                    nameof(nextCatalog));
            }

            var catalog = LocalProfileJsonCodec.SealCatalog(nextCatalog);
            var bytes = LocalProfileJsonCodec.SerializeCatalog(catalog);
            lock (ProcessGate)
            {
                if (!_fileSystem.DirectoryExists(_paths.ProfilesDirectory))
                {
                    return Result(PersistenceResultKind.NotFound);
                }

                var lockResult = TryAcquireLock(out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    return CommitLocked(
                        expectedVersionToken,
                        catalog,
                        bytes,
                        operationId);
                }
            }
        }

        private CatalogPersistenceResult LoadOrRebuildLocked(OperationId operationId)
        {
            var interrupted = FindOperationBackups();
            if (interrupted.Count > 1)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            if (interrupted.Count == 1)
            {
                var resolved = ResolveInterruptedUpdate(interrupted[0]);
                if (resolved != null &&
                    resolved.Kind != PersistenceResultKind.Loaded &&
                    resolved.Kind != PersistenceResultKind.NotCommitted)
                {
                    return resolved;
                }
            }

            if (!TryCleanNonAuthoritativeOperationFiles())
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var current = ReadCandidate(_paths.ProfilesIndexPath);
            if (current.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            var rebuiltEntries = ScanProfiles(
                current.Kind == CandidateKind.Valid ? current.Catalog : null);
            if (rebuiltEntries == null)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            if (current.Kind == CandidateKind.Valid &&
                Equivalent(current.Catalog.Profiles, rebuiltEntries))
            {
                return Result(PersistenceResultKind.Loaded, current.Catalog);
            }

            var revision = current.Kind == CandidateKind.Valid
                ? current.Catalog.CatalogRevision + 1
                : 1;
            var candidate = new LocalProfileCatalog(
                LocalProfileCatalog.CurrentSchemaVersion,
                revision,
                new Sha256Digest(new string('0', 64)),
                rebuiltEntries);
            var sealedCatalog = LocalProfileJsonCodec.SealCatalog(candidate);
            var bytes = LocalProfileJsonCodec.SerializeCatalog(sealedCatalog);
            if (current.Kind == CandidateKind.Valid)
            {
                return CommitLocked(
                    current.Catalog.VersionToken,
                    sealedCatalog,
                    bytes,
                    operationId);
            }

            return PublishRebuiltCatalogOverMissingOrCorrupt(
                current,
                sealedCatalog,
                bytes,
                operationId);
        }

        private CatalogPersistenceResult PublishRebuiltCatalogOverMissingOrCorrupt(
            Candidate current,
            LocalProfileCatalog catalog,
            byte[] bytes,
            OperationId operationId)
        {
            var temporaryPath = _paths.ProfilesIndexTemporaryPath(operationId);
            var operationBackupPath = _paths.ProfilesIndexReplaceBackupPath(operationId);
            if (_fileSystem.FileExists(temporaryPath) ||
                _fileSystem.FileExists(operationBackupPath))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            try
            {
                _fileSystem.CreateFileDurably(temporaryPath, bytes);
                if (!ReadCandidate(temporaryPath).HasToken(catalog.VersionToken))
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                if (current.Kind == CandidateKind.Missing)
                {
                    _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                        temporaryPath,
                        _paths.ProfilesIndexPath);
                }
                else
                {
                    _fileSystem.ReplaceFileWithOperationBackup(
                        temporaryPath,
                        _paths.ProfilesIndexPath,
                        operationBackupPath);
                }
            }
            catch (IOException)
            {
                // Rescan below determines whether the rebuilt catalog was published.
            }
            catch (UnauthorizedAccessException)
            {
                // Rescan below determines whether the rebuilt catalog was published.
            }

            var published = ReadCandidate(_paths.ProfilesIndexPath);
            if (published.HasToken(catalog.VersionToken))
            {
                TryDelete(operationBackupPath);
                return Result(PersistenceResultKind.Created, published.Catalog);
            }

            return published.Kind == CandidateKind.Missing
                ? Result(PersistenceResultKind.NotCommitted)
                : Result(PersistenceResultKind.AmbiguousReplaceState);
        }

        private CatalogPersistenceResult CommitLocked(
            CatalogVersionToken expected,
            LocalProfileCatalog nextCatalog,
            byte[] bytes,
            OperationId operationId)
        {
            try
            {
                if (FindOperationBackups().Count != 0)
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                if (!TryCleanNonAuthoritativeOperationFiles())
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                var current = ReadCandidate(_paths.ProfilesIndexPath);
                if (current.Kind == CandidateKind.Unsupported)
                {
                    return Result(PersistenceResultKind.UnsupportedVersion);
                }

                if (current.Kind != CandidateKind.Valid)
                {
                    return current.Kind == CandidateKind.Missing
                        ? Result(PersistenceResultKind.NotFound)
                        : Result(PersistenceResultKind.Corrupt);
                }

                if (!current.Catalog.VersionToken.Equals(expected))
                {
                    return Result(PersistenceResultKind.VersionConflict);
                }

                var temporaryPath = _paths.ProfilesIndexTemporaryPath(operationId);
                var operationBackupPath = _paths.ProfilesIndexReplaceBackupPath(operationId);
                if (_fileSystem.FileExists(temporaryPath) ||
                    _fileSystem.FileExists(operationBackupPath))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                _fileSystem.CreateFileDurably(temporaryPath, bytes);
                if (!ReadCandidate(temporaryPath).HasToken(nextCatalog.VersionToken))
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                try
                {
                    _fileSystem.ReplaceFileWithOperationBackup(
                        temporaryPath,
                        _paths.ProfilesIndexPath,
                        operationBackupPath);
                }
                catch (IOException)
                {
                    // The following complete rescan is authoritative.
                }
                catch (UnauthorizedAccessException)
                {
                    // The following complete rescan is authoritative.
                }

                return ResolveUpdate(expected, nextCatalog.VersionToken, operationId);
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

        private CatalogPersistenceResult ResolveUpdate(
            CatalogVersionToken expected,
            CatalogVersionToken next,
            OperationId operationId)
        {
            var main = ReadCandidate(_paths.ProfilesIndexPath);
            var operationBackupPath = _paths.ProfilesIndexReplaceBackupPath(operationId);
            var backup = ReadCandidate(operationBackupPath);
            if (main.HasToken(next))
            {
                TryDelete(operationBackupPath);
                return Result(PersistenceResultKind.Committed, main.Catalog);
            }

            if (main.HasToken(expected))
            {
                return Result(PersistenceResultKind.NotCommitted);
            }

            if (main.Kind == CandidateKind.Missing && backup.HasToken(expected))
            {
                return RestoreExpectedCatalog(expected, operationId, backup);
            }

            return Result(PersistenceResultKind.AmbiguousReplaceState);
        }

        private CatalogPersistenceResult RestoreExpectedCatalog(
            CatalogVersionToken expected,
            OperationId operationId,
            Candidate operationBackup)
        {
            var repairPath = _paths.ProfilesIndexRepairTemporaryPath(operationId);
            if (_fileSystem.FileExists(repairPath))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            try
            {
                _fileSystem.CreateFileDurably(
                    repairPath,
                    LocalProfileJsonCodec.SerializeCatalog(operationBackup.Catalog));
                if (!ReadCandidate(repairPath).HasToken(expected))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                try
                {
                    _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                        repairPath,
                        _paths.ProfilesIndexPath);
                }
                catch (IOException)
                {
                    // Main verification below decides repair publication.
                }
                catch (UnauthorizedAccessException)
                {
                    // Main verification below decides repair publication.
                }

                if (!ReadCandidate(_paths.ProfilesIndexPath).HasToken(expected))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                TryDelete(_paths.ProfilesIndexReplaceBackupPath(operationId));
                TryDelete(_paths.ProfilesIndexTemporaryPath(operationId));
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

        private CatalogPersistenceResult ResolveInterruptedUpdate(string operationBackupPath)
        {
            if (!TryOperationId(operationBackupPath, out var operationId))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var main = ReadCandidate(_paths.ProfilesIndexPath);
            var backup = ReadCandidate(operationBackupPath);
            if (main.Kind == CandidateKind.Missing && backup.Kind == CandidateKind.Valid)
            {
                return RestoreExpectedCatalog(
                    backup.Catalog.VersionToken,
                    operationId,
                    backup);
            }

            if (main.Kind == CandidateKind.Valid && backup.Kind == CandidateKind.Valid &&
                main.Catalog.VersionToken.Equals(backup.Catalog.VersionToken))
            {
                TryDelete(operationBackupPath);
                TryDelete(_paths.ProfilesIndexTemporaryPath(operationId));
                return Result(PersistenceResultKind.Loaded, main.Catalog);
            }

            if (main.Kind == CandidateKind.Valid && backup.Kind == CandidateKind.Valid &&
                main.Catalog.CatalogRevision == backup.Catalog.CatalogRevision + 1)
            {
                TryDelete(operationBackupPath);
                return Result(PersistenceResultKind.Loaded, main.Catalog);
            }

            return Result(PersistenceResultKind.AmbiguousReplaceState);
        }

        private IReadOnlyList<LocalProfileCatalogEntry> ScanProfiles(
            LocalProfileCatalog previousCatalog)
        {
            var previous = new Dictionary<ProfileId, LocalProfileCatalogEntry>();
            if (previousCatalog != null)
            {
                for (var index = 0; index < previousCatalog.Profiles.Count; index++)
                {
                    previous.Add(
                        previousCatalog.Profiles[index].ProfileId,
                        previousCatalog.Profiles[index]);
                }
            }

            var discovered = new HashSet<ProfileId>();
            var result = new List<LocalProfileCatalogEntry>();
            var directories = _fileSystem.EnumerateDirectories(_paths.ProfilesDirectory);
            for (var index = 0; index < directories.Count; index++)
            {
                if (!TryProfileId(Path.GetFileName(directories[index]), out var profileId))
                {
                    continue;
                }

                discovered.Add(profileId);
                var mainPath = _paths.ProfilePath(profileId);
                if (HasUnsupportedProfileSchema(mainPath))
                {
                    return null;
                }

                var main = ReadProfile(mainPath, profileId);
                if (main != null)
                {
                    result.Add(new LocalProfileCatalogEntry(
                        profileId,
                        main.DisplayName,
                        main.UpdatedAtUtcMs,
                        ProfileLoadability.Loadable));
                    continue;
                }

                var backupPath = _paths.ProfileBackupPath(profileId);
                if (HasUnsupportedProfileSchema(backupPath))
                {
                    return null;
                }

                var backup = ReadProfile(backupPath, profileId);
                if (backup != null)
                {
                    result.Add(new LocalProfileCatalogEntry(
                        profileId,
                        backup.DisplayName,
                        backup.UpdatedAtUtcMs,
                        ProfileLoadability.RecoveryAvailable));
                    continue;
                }

                if (previous.TryGetValue(profileId, out var prior))
                {
                    result.Add(Clone(prior, ProfileLoadability.Corrupt));
                }
            }

            foreach (var pair in previous)
            {
                if (!discovered.Contains(pair.Key))
                {
                    result.Add(Clone(pair.Value, ProfileLoadability.Missing));
                }
            }

            result.Sort((left, right) => string.CompareOrdinal(
                left.ProfileId.ToString(),
                right.ProfileId.ToString()));
            return result.AsReadOnly();
        }

        private bool HasUnsupportedProfileSchema(string path)
        {
            if (!_fileSystem.FileExists(path))
            {
                return false;
            }

            try
            {
                var root = StrictJsonReader.Parse(_fileSystem.ReadAllBytes(path));
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

        private LocalPlayerProfile ReadProfile(string path, ProfileId profileId)
        {
            if (!_fileSystem.FileExists(path))
            {
                return null;
            }

            try
            {
                var profile = LocalProfileJsonCodec.DeserializeProfile(
                    _fileSystem.ReadAllBytes(path));
                return profile.ProfileId.Equals(profileId) ? profile : null;
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

        private Candidate ReadCandidate(string path)
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
                return Candidate.Valid(LocalProfileJsonCodec.DeserializeCatalog(bytes));
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
                       LocalProfileCatalog.CurrentSchemaVersion;
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

        private IReadOnlyList<string> FindOperationBackups()
        {
            return FindOperationFiles("profiles-index.replace-backup.");
        }

        private IReadOnlyList<string> FindOperationFiles(string prefix)
        {
            var result = new List<string>();
            var files = _fileSystem.EnumerateFiles(_paths.ProfilesDirectory);
            for (var index = 0; index < files.Count; index++)
            {
                if (Path.GetFileName(files[index]).StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                {
                    result.Add(files[index]);
                }
            }

            return result.AsReadOnly();
        }

        private bool TryCleanNonAuthoritativeOperationFiles()
        {
            var temporaryFiles = FindOperationFiles("profiles-index.tmp.");
            var repairFiles = FindOperationFiles("profiles-index.repair.tmp.");
            for (var index = 0; index < temporaryFiles.Count; index++)
            {
                TryDelete(temporaryFiles[index]);
            }

            for (var index = 0; index < repairFiles.Count; index++)
            {
                TryDelete(repairFiles[index]);
            }

            return FindOperationFiles("profiles-index.tmp.").Count == 0 &&
                   FindOperationFiles("profiles-index.repair.tmp.").Count == 0;
        }

        private CatalogPersistenceResult TryAcquireLock(out IAtomicFileLock fileLock)
        {
            try
            {
                fileLock = _fileSystem.AcquireExclusiveLock(_paths.ProfilesIndexLockPath);
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

        private static bool Equivalent(
            IReadOnlyList<LocalProfileCatalogEntry> left,
            IReadOnlyList<LocalProfileCatalogEntry> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!left[index].ProfileId.Equals(right[index].ProfileId) ||
                    !string.Equals(
                        left[index].DisplayName,
                        right[index].DisplayName,
                        StringComparison.Ordinal) ||
                    left[index].UpdatedAtUtcMs != right[index].UpdatedAtUtcMs ||
                    left[index].Loadability != right[index].Loadability)
                {
                    return false;
                }
            }

            return true;
        }

        private static LocalProfileCatalogEntry Clone(
            LocalProfileCatalogEntry source,
            ProfileLoadability loadability)
        {
            return new LocalProfileCatalogEntry(
                source.ProfileId,
                source.DisplayName,
                source.UpdatedAtUtcMs,
                loadability);
        }

        private static bool TryProfileId(string value, out ProfileId profileId)
        {
            if (Guid.TryParseExact(value, "D", out var parsed) && parsed != Guid.Empty &&
                string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal))
            {
                profileId = new ProfileId(parsed);
                return true;
            }

            profileId = default;
            return false;
        }

        private static bool TryOperationId(string path, out OperationId operationId)
        {
            const string prefix = "profiles-index.replace-backup.";
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

        private static void RequireOperationId(OperationId operationId)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException("A stable operation ID is required.", nameof(operationId));
            }
        }

        private static CatalogPersistenceResult Result(
            PersistenceResultKind kind,
            LocalProfileCatalog catalog = null)
        {
            return new CatalogPersistenceResult(kind, catalog);
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
            private Candidate(CandidateKind kind, LocalProfileCatalog catalog)
            {
                Kind = kind;
                Catalog = catalog;
            }

            public CandidateKind Kind { get; }

            public LocalProfileCatalog Catalog { get; }

            public bool HasToken(CatalogVersionToken token)
            {
                return Kind == CandidateKind.Valid && Catalog.VersionToken.Equals(token);
            }

            public static Candidate Missing()
            {
                return new Candidate(CandidateKind.Missing, null);
            }

            public static Candidate Valid(LocalProfileCatalog catalog)
            {
                return new Candidate(CandidateKind.Valid, catalog);
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
