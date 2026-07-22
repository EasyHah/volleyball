using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    public sealed class LocalCareerSaveRepository : ICareerSaveRepository
    {
        private static readonly ConcurrentDictionary<string, object> ProcessGates =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly CareerStoragePaths _paths;
        private readonly IAtomicFileSystem _fileSystem;

        public LocalCareerSaveRepository(
            CareerStoragePaths paths,
            IAtomicFileSystem fileSystem)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public CareerPersistenceResult Create(
            CareerSaveSnapshot initialSnapshot,
            OperationId operationId)
        {
            if (initialSnapshot == null)
            {
                throw new ArgumentNullException(nameof(initialSnapshot));
            }

            RequireOperationId(operationId);
            if (initialSnapshot.Identity.Revision != 1 ||
                initialSnapshot.Identity.RestoredFromVersionToken.HasValue)
            {
                throw new ArgumentException(
                    "A newly created Career save must begin at revision 1 without a restore source.",
                    nameof(initialSnapshot));
            }

            var sealedSnapshot = CareerSaveJsonCodec.Seal(initialSnapshot);
            var bytes = CareerSaveJsonCodec.Serialize(sealedSnapshot);
            var profileId = sealedSnapshot.Identity.ProfileId;
            var saveId = sealedSnapshot.Identity.SaveId;
            var gate = Gate(profileId, saveId);
            lock (gate)
            {
                try
                {
                    EnsureCareerDirectories(profileId);
                }
                catch (IOException)
                {
                    return Result(PersistenceResultKind.IoFailure);
                }
                catch (UnauthorizedAccessException)
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                var lockResult = TryAcquireLock(profileId, saveId, out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    return CreateLocked(sealedSnapshot, bytes, operationId);
                }
            }
        }

        public CareerPersistenceResult Load(ProfileId profileId, SaveId saveId)
        {
            RequireProfileAndSave(profileId, saveId);
            if (!_fileSystem.DirectoryExists(_paths.CareersDirectory(profileId)))
            {
                return Result(PersistenceResultKind.NotFound);
            }

            var gate = Gate(profileId, saveId);
            lock (gate)
            {
                var lockResult = TryAcquireLock(profileId, saveId, out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    try
                    {
                        return LoadLocked(profileId, saveId);
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

        public CareerPersistenceResult Commit(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            CareerSaveSnapshot nextSnapshot,
            OperationId operationId)
        {
            RequireProfileAndSave(profileId, saveId);
            RequireOperationId(operationId);
            if (nextSnapshot == null)
            {
                throw new ArgumentNullException(nameof(nextSnapshot));
            }

            if (!nextSnapshot.Identity.ProfileId.Equals(profileId) ||
                !nextSnapshot.Identity.SaveId.Equals(saveId))
            {
                throw new ArgumentException(
                    "The next snapshot ownership must match the repository target.",
                    nameof(nextSnapshot));
            }

            if (!nextSnapshot.Identity.LineageId.Equals(expectedVersionToken.LineageId) ||
                nextSnapshot.Identity.Revision != expectedVersionToken.Revision + 1)
            {
                throw new ArgumentException(
                    "A normal commit must preserve lineage and advance exactly one revision.",
                    nameof(nextSnapshot));
            }

            var sealedSnapshot = CareerSaveJsonCodec.Seal(nextSnapshot);
            var bytes = CareerSaveJsonCodec.Serialize(sealedSnapshot);
            if (!_fileSystem.DirectoryExists(_paths.CareersDirectory(profileId)))
            {
                return Result(PersistenceResultKind.NotFound);
            }

            var gate = Gate(profileId, saveId);
            lock (gate)
            {
                var lockResult = TryAcquireLock(profileId, saveId, out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    return CommitLocked(
                        profileId,
                        saveId,
                        expectedVersionToken,
                        sealedSnapshot,
                        bytes,
                        operationId);
                }
            }
        }

        public CareerPersistenceResult RecoverFromBackup(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken confirmedBackupVersionToken,
            Sha256Digest? confirmedCorruptMainFingerprint,
            OperationId operationId,
            long recoveredAtUtcMs,
            LineageId newLineageId)
        {
            RequireProfileAndSave(profileId, saveId);
            RequireOperationId(operationId);
            if (newLineageId.Value == Guid.Empty)
            {
                throw new ArgumentException("A new non-empty lineage is required.", nameof(newLineageId));
            }

            if (recoveredAtUtcMs < 0 || recoveredAtUtcMs > 9007199254740991L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recoveredAtUtcMs),
                    recoveredAtUtcMs,
                    "The recovery timestamp must be an I-JSON safe non-negative integer.");
            }

            if (!_fileSystem.DirectoryExists(_paths.CareersDirectory(profileId)))
            {
                return Result(PersistenceResultKind.NotFound);
            }

            var gate = Gate(profileId, saveId);
            lock (gate)
            {
                var lockResult = TryAcquireLock(profileId, saveId, out var fileLock);
                if (lockResult != null)
                {
                    return lockResult;
                }

                using (fileLock)
                {
                    try
                    {
                        return RecoverLocked(
                            profileId,
                            saveId,
                            confirmedBackupVersionToken,
                            confirmedCorruptMainFingerprint,
                            operationId,
                            recoveredAtUtcMs,
                            newLineageId);
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

        private CareerPersistenceResult CreateLocked(
            CareerSaveSnapshot snapshot,
            byte[] bytes,
            OperationId operationId)
        {
            var profileId = snapshot.Identity.ProfileId;
            var saveId = snapshot.Identity.SaveId;
            var mainPath = _paths.CareerPath(profileId, saveId);
            var temporaryPath = _paths.CareerTemporaryPath(profileId, saveId, operationId);
            if (_fileSystem.FileExists(mainPath))
            {
                var current = ReadCandidate(mainPath, profileId, saveId);
                if (current.Kind == CandidateKind.Unsupported)
                {
                    return Result(PersistenceResultKind.UnsupportedVersion);
                }

                return current.Kind == CandidateKind.Invalid
                    ? Result(PersistenceResultKind.Corrupt)
                    : Result(PersistenceResultKind.AlreadyExists);
            }

            if (FindCandidates(profileId, saveId, ".recovery-intent.").Count != 0 ||
                FindCandidates(profileId, saveId, ".recovery.tmp.").Count != 0)
            {
                return Result(PersistenceResultKind.AmbiguousRestoreState);
            }

            if (FindCandidates(profileId, saveId, ".replace-backup.").Count != 0 ||
                FindCandidates(profileId, saveId, ".backup-convergence.").Count != 0 ||
                FindCandidates(profileId, saveId, ".tmp.").Count != 0 ||
                FindCandidates(profileId, saveId, ".repair.tmp.").Count != 0)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var fixedBackup = ReadCandidate(
                _paths.CareerBackupPath(profileId, saveId),
                profileId,
                saveId);
            if (fixedBackup.Kind == CandidateKind.Valid)
            {
                return Result(
                    PersistenceResultKind.RecoveryAvailable,
                    recoverableBackup: fixedBackup.Snapshot.Identity.VersionToken);
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
                var temporary = ReadCandidate(temporaryPath, profileId, saveId);
                if (temporary.Kind != CandidateKind.Valid ||
                    !temporary.Snapshot.Identity.VersionToken.Equals(snapshot.Identity.VersionToken))
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                    temporaryPath,
                    mainPath);
            }
            catch (IOException)
            {
                // The following rescan is the only authority for whether publication happened.
            }
            catch (UnauthorizedAccessException)
            {
                // The following rescan is the only authority for whether publication happened.
            }

            var published = ReadCandidate(mainPath, profileId, saveId);
            if (published.Kind == CandidateKind.Valid &&
                published.Snapshot.Identity.VersionToken.Equals(snapshot.Identity.VersionToken))
            {
                return Result(PersistenceResultKind.Created, published.Snapshot);
            }

            if (published.Kind == CandidateKind.Valid)
            {
                return Result(PersistenceResultKind.AlreadyExists);
            }

            return published.Kind == CandidateKind.Missing
                ? Result(PersistenceResultKind.NotCommitted)
                : Result(PersistenceResultKind.AmbiguousReplaceState);
        }

        private CareerPersistenceResult CommitLocked(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            CareerSaveSnapshot nextSnapshot,
            byte[] bytes,
            OperationId operationId)
        {
            try
            {
                var unresolvedOperation = ResultForUnresolvedOperation(profileId, saveId);
                if (unresolvedOperation != null)
                {
                    return unresolvedOperation;
                }

                var current = ReadCandidate(_paths.CareerPath(profileId, saveId), profileId, saveId);
                var invalidCurrent = ResultForUnreadableCurrent(profileId, saveId, current);
                if (invalidCurrent != null)
                {
                    return invalidCurrent;
                }

                if (!current.Snapshot.Identity.VersionToken.Equals(expectedVersionToken))
                {
                    return Result(PersistenceResultKind.VersionConflict);
                }

                var fixedBackup = ReadCandidate(
                    _paths.CareerBackupPath(profileId, saveId),
                    profileId,
                    saveId);
                if (fixedBackup.Kind == CandidateKind.Unsupported)
                {
                    return Result(PersistenceResultKind.UnsupportedVersion);
                }

                if (fixedBackup.Kind == CandidateKind.Valid &&
                    !HasSafeFixedBackupRelationship(current.Snapshot, fixedBackup.Snapshot))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                var temporaryPath = _paths.CareerTemporaryPath(profileId, saveId, operationId);
                var operationBackupPath = _paths.CareerReplaceBackupPath(
                    profileId,
                    saveId,
                    operationId);
                if (_fileSystem.FileExists(temporaryPath) ||
                    _fileSystem.FileExists(operationBackupPath))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                _fileSystem.CreateFileDurably(temporaryPath, bytes);
                var temporary = ReadCandidate(temporaryPath, profileId, saveId);
                if (temporary.Kind != CandidateKind.Valid ||
                    !temporary.Snapshot.Identity.VersionToken.Equals(
                        nextSnapshot.Identity.VersionToken))
                {
                    return Result(PersistenceResultKind.IoFailure);
                }

                try
                {
                    _fileSystem.ReplaceFileWithOperationBackup(
                        temporaryPath,
                        _paths.CareerPath(profileId, saveId),
                        operationBackupPath);
                }
                catch (IOException)
                {
                    // ResolveUpdateLocked rescans every candidate before deciding.
                }
                catch (UnauthorizedAccessException)
                {
                    // ResolveUpdateLocked rescans every candidate before deciding.
                }

                return ResolveUpdateLocked(
                    profileId,
                    saveId,
                    expectedVersionToken,
                    nextSnapshot.Identity.VersionToken,
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

        private CareerPersistenceResult ResultForUnresolvedOperation(
            ProfileId profileId,
            SaveId saveId)
        {
            if (FindCandidates(profileId, saveId, ".recovery-intent.").Count != 0 ||
                FindCandidates(profileId, saveId, ".recovery.tmp.").Count != 0)
            {
                return Result(PersistenceResultKind.AmbiguousRestoreState);
            }

            if (FindCandidates(profileId, saveId, ".replace-backup.").Count != 0 ||
                FindCandidates(profileId, saveId, ".backup-convergence.").Count != 0 ||
                FindCandidates(profileId, saveId, ".tmp.").Count != 0 ||
                FindCandidates(profileId, saveId, ".repair.tmp.").Count != 0)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            return null;
        }

        private static bool HasSafeFixedBackupRelationship(
            CareerSaveSnapshot current,
            CareerSaveSnapshot fixedBackup)
        {
            var currentIdentity = current.Identity;
            var backupToken = fixedBackup.Identity.VersionToken;
            var strictPreviousRevision =
                backupToken.LineageId.Equals(currentIdentity.LineageId) &&
                backupToken.Revision == currentIdentity.Revision - 1;
            if (strictPreviousRevision)
            {
                return true;
            }

            return currentIdentity.RestoredFromVersionToken.HasValue &&
                   backupToken.Equals(currentIdentity.RestoredFromVersionToken.Value);
        }

        private CareerPersistenceResult ResolveUpdateLocked(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            CareerVersionToken nextVersionToken,
            OperationId operationId)
        {
            var main = ReadCandidate(_paths.CareerPath(profileId, saveId), profileId, saveId);
            var operationBackup = ReadCandidate(
                _paths.CareerReplaceBackupPath(profileId, saveId, operationId),
                profileId,
                saveId);
            var temporary = ReadCandidate(
                _paths.CareerTemporaryPath(profileId, saveId, operationId),
                profileId,
                saveId);

            if (main.HasToken(nextVersionToken))
            {
                var backupHealthy = HasExpectedBackup(
                    profileId,
                    saveId,
                    expectedVersionToken,
                    operationBackup);
                if (!backupHealthy)
                {
                    backupHealthy = TryConvergeBackup(
                        profileId,
                        saveId,
                        expectedVersionToken,
                        operationId,
                        operationBackup);
                }

                return Result(
                    backupHealthy
                        ? PersistenceResultKind.Committed
                        : PersistenceResultKind.BackupDegraded,
                    main.Snapshot);
            }

            if (main.HasToken(expectedVersionToken))
            {
                return Result(PersistenceResultKind.NotCommitted);
            }

            if (main.Kind == CandidateKind.Missing &&
                operationBackup.HasToken(expectedVersionToken))
            {
                return RestoreExpectedMainAfterPartialReplace(
                    profileId,
                    saveId,
                    expectedVersionToken,
                    operationId,
                    operationBackup);
            }

            if (main.Kind == CandidateKind.Missing &&
                temporary.HasToken(nextVersionToken) &&
                operationBackup.Kind == CandidateKind.Missing)
            {
                return Result(PersistenceResultKind.NotCommitted);
            }

            return Result(PersistenceResultKind.AmbiguousReplaceState);
        }

        private bool HasExpectedBackup(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            Candidate operationBackup)
        {
            if (operationBackup.HasToken(expectedVersionToken))
            {
                return false;
            }

            var fixedBackup = ReadCandidate(
                _paths.CareerBackupPath(profileId, saveId),
                profileId,
                saveId);
            return fixedBackup.HasToken(expectedVersionToken);
        }

        private bool TryConvergeBackup(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            OperationId operationId,
            Candidate operationBackup)
        {
            if (!operationBackup.HasToken(expectedVersionToken))
            {
                return false;
            }

            var operationBackupPath = _paths.CareerReplaceBackupPath(
                profileId,
                saveId,
                operationId);
            var fixedBackupPath = _paths.CareerBackupPath(profileId, saveId);
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
                    var convergenceBackupPath = _paths.CareerBackupConvergencePath(
                        profileId,
                        saveId,
                        operationId);
                    if (_fileSystem.FileExists(convergenceBackupPath))
                    {
                        return false;
                    }

                    try
                    {
                        _fileSystem.ReplaceFileWithOperationBackup(
                            operationBackupPath,
                            fixedBackupPath,
                            convergenceBackupPath);
                    }
                    catch (IOException)
                    {
                        // Verification below decides whether convergence crossed its commit point.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Verification below decides whether convergence crossed its commit point.
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

            var fixedBackup = ReadCandidate(fixedBackupPath, profileId, saveId);
            if (!fixedBackup.HasToken(expectedVersionToken))
            {
                return false;
            }

            TryDelete(operationBackupPath);
            TryDelete(_paths.CareerBackupConvergencePath(profileId, saveId, operationId));
            return true;
        }

        private CareerPersistenceResult RestoreExpectedMainAfterPartialReplace(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken expectedVersionToken,
            OperationId operationId,
            Candidate operationBackup)
        {
            var repairPath = _paths.CareerRepairTemporaryPath(profileId, saveId, operationId);
            if (_fileSystem.FileExists(repairPath))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            try
            {
                _fileSystem.CreateFileDurably(
                    repairPath,
                    CareerSaveJsonCodec.Serialize(operationBackup.Snapshot));
                var verifiedRepair = ReadCandidate(repairPath, profileId, saveId);
                if (!verifiedRepair.HasToken(expectedVersionToken))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                try
                {
                    _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                        repairPath,
                        _paths.CareerPath(profileId, saveId));
                }
                catch (IOException)
                {
                    // Rescan below decides whether repair was published.
                }
                catch (UnauthorizedAccessException)
                {
                    // Rescan below decides whether repair was published.
                }

                var restored = ReadCandidate(
                    _paths.CareerPath(profileId, saveId),
                    profileId,
                    saveId);
                if (!restored.HasToken(expectedVersionToken))
                {
                    return Result(PersistenceResultKind.AmbiguousReplaceState);
                }

                TryMoveToQuarantine(
                    _paths.CareerReplaceBackupPath(profileId, saveId, operationId),
                    profileId,
                    saveId,
                    CareerQuarantineSourceKind.ReplaceBackup,
                    operationId);
                TryMoveToQuarantine(
                    _paths.CareerTemporaryPath(profileId, saveId, operationId),
                    profileId,
                    saveId,
                    CareerQuarantineSourceKind.Temporary,
                    operationId);
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

        private CareerPersistenceResult LoadLocked(ProfileId profileId, SaveId saveId)
        {
            var recoveryIntents = FindCandidates(profileId, saveId, ".recovery-intent.");
            if (recoveryIntents.Count > 1)
            {
                return Result(PersistenceResultKind.AmbiguousRestoreState);
            }

            if (recoveryIntents.Count == 1)
            {
                try
                {
                    var intent = CareerRecoveryIntentJsonCodec.Deserialize(
                        _fileSystem.ReadAllBytes(recoveryIntents[0]));
                    if (!intent.ProfileId.Equals(profileId) ||
                        !intent.SaveId.Equals(saveId))
                    {
                        return Result(PersistenceResultKind.AmbiguousRestoreState);
                    }

                    return ResolveRecoveryIntentLocked(recoveryIntents[0], intent);
                }
                catch (FormatException)
                {
                    return Result(PersistenceResultKind.AmbiguousRestoreState);
                }
                catch (ArgumentException)
                {
                    return Result(PersistenceResultKind.AmbiguousRestoreState);
                }
            }

            var backupConvergence = FindCandidates(
                profileId,
                saveId,
                ".backup-convergence.");
            if (backupConvergence.Count > 1)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var replaceBackups = FindCandidates(profileId, saveId, ".replace-backup.");
            if (replaceBackups.Count > 1)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            if (backupConvergence.Count == 1 && replaceBackups.Count == 0)
            {
                var convergenceResult = ResolveStandaloneBackupConvergenceOnLoad(
                    profileId,
                    saveId,
                    backupConvergence[0]);
                if (convergenceResult != null)
                {
                    return convergenceResult;
                }
            }

            if (replaceBackups.Count == 1)
            {
                var recovered = ResolveInterruptedUpdateOnLoad(
                    profileId,
                    saveId,
                    replaceBackups[0]);
                if (recovered != null)
                {
                    return recovered;
                }
            }

            var main = ReadCandidate(_paths.CareerPath(profileId, saveId), profileId, saveId);
            if (main.Kind == CandidateKind.Valid)
            {
                TryQuarantineTemporaryFiles(profileId, saveId);
                return Result(PersistenceResultKind.Loaded, main.Snapshot);
            }

            if (main.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            var backup = ReadCandidate(
                _paths.CareerBackupPath(profileId, saveId),
                profileId,
                saveId);
            if (backup.Kind == CandidateKind.Valid)
            {
                return Result(
                    PersistenceResultKind.RecoveryAvailable,
                    recoverableBackup: backup.Snapshot.Identity.VersionToken);
            }

            if (backup.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            if (main.Kind == CandidateKind.Missing && backup.Kind == CandidateKind.Missing)
            {
                TryQuarantineTemporaryFiles(profileId, saveId);
                return Result(PersistenceResultKind.NotFound);
            }

            TryQuarantineDoubleCorruption(profileId, saveId, main, backup);
            return Result(PersistenceResultKind.Corrupt);
        }

        private CareerPersistenceResult ResolveStandaloneBackupConvergenceOnLoad(
            ProfileId profileId,
            SaveId saveId,
            string convergencePath)
        {
            if (!TryOperationIdFromPath(
                    convergencePath,
                    ".backup-convergence.",
                    out _))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var main = ReadCandidate(_paths.CareerPath(profileId, saveId), profileId, saveId);
            var fixedBackup = ReadCandidate(
                _paths.CareerBackupPath(profileId, saveId),
                profileId,
                saveId);
            var displacedBackup = ReadCandidate(convergencePath, profileId, saveId);
            var uniquelyCompleted = main.Kind == CandidateKind.Valid &&
                                    fixedBackup.Kind == CandidateKind.Valid &&
                                    displacedBackup.Kind == CandidateKind.Valid &&
                                    main.Snapshot.Identity.LineageId.Equals(
                                        fixedBackup.Snapshot.Identity.LineageId) &&
                                    main.Snapshot.Identity.Revision ==
                                    fixedBackup.Snapshot.Identity.Revision + 1 &&
                                    displacedBackup.Snapshot.Identity.Revision <=
                                    fixedBackup.Snapshot.Identity.Revision;
            if (!uniquelyCompleted)
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            TryDelete(convergencePath);
            return _fileSystem.FileExists(convergencePath)
                ? Result(PersistenceResultKind.AmbiguousReplaceState)
                : null;
        }

        private CareerPersistenceResult ResolveInterruptedUpdateOnLoad(
            ProfileId profileId,
            SaveId saveId,
            string operationBackupPath)
        {
            if (!TryOperationIdFromPath(operationBackupPath, ".replace-backup.", out var operationId))
            {
                return Result(PersistenceResultKind.AmbiguousReplaceState);
            }

            var main = ReadCandidate(_paths.CareerPath(profileId, saveId), profileId, saveId);
            var backup = ReadCandidate(operationBackupPath, profileId, saveId);
            if (main.Kind == CandidateKind.Missing && backup.Kind == CandidateKind.Valid)
            {
                return RestoreExpectedMainAfterPartialReplace(
                    profileId,
                    saveId,
                    backup.Snapshot.Identity.VersionToken,
                    operationId,
                    backup);
            }

            if (main.Kind == CandidateKind.Valid && backup.Kind == CandidateKind.Valid &&
                main.Snapshot.Identity.VersionToken.Equals(
                    backup.Snapshot.Identity.VersionToken))
            {
                TryMoveToQuarantine(
                    operationBackupPath,
                    profileId,
                    saveId,
                    CareerQuarantineSourceKind.ReplaceBackup,
                    operationId);
                TryQuarantineTemporaryFiles(profileId, saveId);
                return Result(PersistenceResultKind.Loaded, main.Snapshot);
            }

            if (main.Kind == CandidateKind.Valid && backup.Kind == CandidateKind.Valid &&
                main.Snapshot.Identity.LineageId.Equals(backup.Snapshot.Identity.LineageId) &&
                main.Snapshot.Identity.Revision == backup.Snapshot.Identity.Revision + 1)
            {
                var healthy = TryConvergeBackup(
                    profileId,
                    saveId,
                    backup.Snapshot.Identity.VersionToken,
                    operationId,
                    backup);
                return Result(
                    healthy
                        ? PersistenceResultKind.Loaded
                        : PersistenceResultKind.BackupDegraded,
                    main.Snapshot);
            }

            return Result(PersistenceResultKind.AmbiguousReplaceState);
        }

        private CareerPersistenceResult ResultForUnreadableCurrent(
            ProfileId profileId,
            SaveId saveId,
            Candidate current)
        {
            if (current.Kind == CandidateKind.Valid)
            {
                return null;
            }

            if (current.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            var backup = ReadCandidate(
                _paths.CareerBackupPath(profileId, saveId),
                profileId,
                saveId);
            if (backup.Kind == CandidateKind.Valid)
            {
                return Result(
                    PersistenceResultKind.RecoveryAvailable,
                    recoverableBackup: backup.Snapshot.Identity.VersionToken);
            }

            return current.Kind == CandidateKind.Missing
                ? Result(PersistenceResultKind.NotFound)
                : Result(PersistenceResultKind.Corrupt);
        }

        private CareerPersistenceResult RecoverLocked(
            ProfileId profileId,
            SaveId saveId,
            CareerVersionToken confirmedBackupVersionToken,
            Sha256Digest? confirmedCorruptMainFingerprint,
            OperationId operationId,
            long recoveredAtUtcMs,
            LineageId newLineageId)
        {
            if (FindCandidates(profileId, saveId, ".recovery-intent.").Count != 0)
            {
                return Result(PersistenceResultKind.AmbiguousRestoreState);
            }

            var backupPath = _paths.CareerBackupPath(profileId, saveId);
            var backup = ReadCandidate(backupPath, profileId, saveId);
            if (!backup.HasToken(confirmedBackupVersionToken))
            {
                return Result(PersistenceResultKind.VersionConflict);
            }

            var mainPath = _paths.CareerPath(profileId, saveId);
            var mainExists = _fileSystem.FileExists(mainPath);
            var main = ReadCandidate(mainPath, profileId, saveId);
            if (main.Kind == CandidateKind.Unsupported)
            {
                return Result(PersistenceResultKind.UnsupportedVersion);
            }

            if (main.Kind == CandidateKind.Valid)
            {
                return Result(PersistenceResultKind.VersionConflict);
            }

            if (confirmedCorruptMainFingerprint.HasValue)
            {
                if (!mainExists ||
                    !RawFingerprint(_fileSystem.ReadAllBytes(mainPath)).Equals(
                        confirmedCorruptMainFingerprint.Value))
                {
                    return Result(PersistenceResultKind.VersionConflict);
                }
            }
            else if (mainExists)
            {
                return Result(PersistenceResultKind.VersionConflict);
            }

            var restored = CreateRestoredSnapshot(
                backup.Snapshot,
                confirmedBackupVersionToken,
                newLineageId,
                recoveredAtUtcMs);
            var recoveryBytes = CareerSaveJsonCodec.Serialize(restored);
            var recoveryPath = _paths.CareerRecoveryTemporaryPath(
                profileId,
                saveId,
                operationId);
            var intentPath = _paths.CareerRecoveryIntentPath(
                profileId,
                saveId,
                operationId);
            if (_fileSystem.FileExists(recoveryPath) || _fileSystem.FileExists(intentPath))
            {
                return Result(PersistenceResultKind.AmbiguousRestoreState);
            }

            var quarantinePath = NextQuarantinePath(
                profileId,
                saveId,
                CareerQuarantineSourceKind.Corrupt,
                operationId);
            var intent = new CareerRecoveryIntentV1(
                profileId,
                saveId,
                operationId,
                !mainExists,
                confirmedCorruptMainFingerprint,
                confirmedBackupVersionToken,
                restored.Identity.VersionToken,
                Path.GetFileName(recoveryPath),
                Path.GetFileName(quarantinePath));
            var intentBytes = CareerRecoveryIntentJsonCodec.Serialize(intent);
            _fileSystem.CreateFileDurably(intentPath, intentBytes);
            CareerRecoveryIntentJsonCodec.Deserialize(_fileSystem.ReadAllBytes(intentPath));
            _fileSystem.CreateFileDurably(recoveryPath, recoveryBytes);
            var verifiedRecovery = ReadCandidate(recoveryPath, profileId, saveId);
            if (!verifiedRecovery.HasToken(restored.Identity.VersionToken))
            {
                return Result(PersistenceResultKind.AmbiguousRestoreState);
            }

            try
            {
                if (mainExists)
                {
                    _fileSystem.CreateDirectory(_paths.CareerQuarantineDirectory(profileId));
                    _fileSystem.ReplaceFileWithOperationBackup(
                        recoveryPath,
                        mainPath,
                        quarantinePath);
                }
                else
                {
                    _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                        recoveryPath,
                        mainPath);
                }
            }
            catch (IOException)
            {
                // The persisted intent and rescan below decide the authoritative outcome.
            }
            catch (UnauthorizedAccessException)
            {
                // The persisted intent and rescan below decide the authoritative outcome.
            }

            return ResolveRecoveryIntentLocked(intentPath, intent);
        }

        private CareerPersistenceResult ResolveRecoveryIntentLocked(
            string intentPath,
            CareerRecoveryIntentV1 intent)
        {
            var profileId = intent.ProfileId;
            var saveId = intent.SaveId;
            var backup = ReadCandidate(
                _paths.CareerBackupPath(profileId, saveId),
                profileId,
                saveId);
            if (!backup.HasToken(intent.BackupVersionToken))
            {
                return Result(PersistenceResultKind.AmbiguousRestoreState);
            }

            var mainPath = _paths.CareerPath(profileId, saveId);
            var main = ReadCandidate(mainPath, profileId, saveId);
            if (main.HasToken(intent.RecoveryVersionToken))
            {
                TryDelete(intentPath);
                return Result(PersistenceResultKind.Loaded, main.Snapshot);
            }

            if (!intent.MainWasMissing && intent.CorruptMainFingerprint.HasValue)
            {
                if (_fileSystem.FileExists(mainPath) &&
                    RawFingerprint(_fileSystem.ReadAllBytes(mainPath)).Equals(
                        intent.CorruptMainFingerprint.Value))
                {
                    TryDelete(intentPath);
                    return Result(
                        PersistenceResultKind.RecoveryAvailable,
                        recoverableBackup: backup.Snapshot.Identity.VersionToken);
                }

                var quarantinePath = Path.Combine(
                    _paths.CareerQuarantineDirectory(profileId),
                    intent.QuarantineFileName);
                if (main.Kind == CandidateKind.Missing &&
                    _fileSystem.FileExists(quarantinePath) &&
                    RawFingerprint(_fileSystem.ReadAllBytes(quarantinePath)).Equals(
                        intent.CorruptMainFingerprint.Value))
                {
                    TryDelete(intentPath);
                    return Result(
                        PersistenceResultKind.RecoveryAvailable,
                        recoverableBackup: backup.Snapshot.Identity.VersionToken);
                }
            }
            else if (intent.MainWasMissing && main.Kind == CandidateKind.Missing)
            {
                TryDelete(intentPath);
                return Result(
                    PersistenceResultKind.RecoveryAvailable,
                    recoverableBackup: backup.Snapshot.Identity.VersionToken);
            }

            return Result(PersistenceResultKind.AmbiguousRestoreState);
        }

        private static CareerSaveSnapshot CreateRestoredSnapshot(
            CareerSaveSnapshot backup,
            CareerVersionToken backupVersionToken,
            LineageId newLineageId,
            long recoveredAtUtcMs)
        {
            var candidate = CreateRestoredSnapshotCandidate(
                backup,
                backupVersionToken,
                newLineageId,
                recoveredAtUtcMs);
            if (candidate.Versions.SchemaVersion == 1 &&
                (candidate.PendingMatch != null ||
                 candidate.MatchHistory.Count != 0 ||
                 candidate.SettlementReceipts.Count != 0))
            {
                throw new InvalidOperationException(
                    "Schema V1 cannot seal match lifecycle evidence; recovery requires the Schema V2 codec.");
            }

            return CareerSaveJsonCodec.Seal(candidate);
        }

        internal static CareerSaveSnapshot CreateRestoredSnapshotCandidate(
            CareerSaveSnapshot backup,
            CareerVersionToken backupVersionToken,
            LineageId newLineageId,
            long recoveredAtUtcMs)
        {
            if (backup == null)
            {
                throw new ArgumentNullException(nameof(backup));
            }

            if (!backup.Identity.VersionToken.Equals(backupVersionToken))
            {
                throw new ArgumentException(
                    "The restore source token must equal the backup snapshot token.",
                    nameof(backupVersionToken));
            }

            var receipts = new OperationReceipt[backup.OperationReceipts.Count];
            for (var index = 0; index < receipts.Length; index++)
            {
                var receipt = backup.OperationReceipts[index];
                receipts[index] = new OperationReceipt(
                    receipt.OperationId,
                    receipt.OperationKind,
                    receipt.Target,
                    receipt.InputFingerprint,
                    newLineageId,
                    receipt.AppliedRevision,
                    receipt.CompletedAtUtcMs,
                    receipt.OutcomeKind,
                    receipt.OutcomeSummary);
            }

            var identity = new CareerSaveIdentity(
                backup.Identity.ProfileId,
                backup.Identity.SaveId,
                newLineageId,
                backupVersionToken.Revision + 1,
                backup.Identity.CreatedAtUtcMs,
                recoveredAtUtcMs,
                backup.Identity.SnapshotHash,
                backupVersionToken);
            var pendingMatch = RebindPendingMatch(backup.PendingMatch, newLineageId);
            var history = new CareerMatchHistoryEntry[backup.MatchHistory.Count];
            for (var index = 0; index < history.Length; index++)
            {
                var entry = backup.MatchHistory[index];
                history[index] = new CareerMatchHistoryEntry(
                    entry.SessionId,
                    entry.ScheduleItemId,
                    entry.SourceWeekPlanId,
                    entry.SourceSlotActionId,
                    entry.ContextDigest,
                    entry.ResultDigest,
                    entry.CanonicalContextUtf8,
                    entry.CanonicalResultUtf8,
                    newLineageId,
                    entry.AppliedRevision,
                    entry.SettledAtUtcMs,
                    entry.SettlementSummary);
            }

            var settlementReceipts =
                new CareerSettlementReceipt[backup.SettlementReceipts.Count];
            for (var index = 0; index < settlementReceipts.Length; index++)
            {
                var receipt = backup.SettlementReceipts[index];
                settlementReceipts[index] = new CareerSettlementReceipt(
                    receipt.SessionId,
                    receipt.ContextDigest,
                    receipt.ResultDigest,
                    newLineageId,
                    receipt.AppliedRevision,
                    receipt.SettledAtUtcMs,
                    receipt.SettlementSummary);
            }

            return new CareerSaveSnapshot(
                backup.Versions,
                identity,
                backup.CareerSeed,
                backup.CareerName,
                backup.PlayerDraft,
                backup.Onboarding,
                backup.Progression,
                backup.TrainingEmphases,
                backup.Player,
                backup.TeamId,
                backup.PotentialGrade,
                backup.Fatigue,
                backup.Mindset,
                backup.CoachTrust,
                receipts,
                pendingMatch,
                history,
                settlementReceipts);
        }

        private static PendingCareerMatch RebindPendingMatch(
            PendingCareerMatch pendingMatch,
            LineageId newLineageId)
        {
            if (pendingMatch == null)
            {
                return null;
            }

            return new PendingCareerMatch(
                pendingMatch.SessionId,
                pendingMatch.CreationOperationId,
                newLineageId,
                pendingMatch.CreatedRevision,
                pendingMatch.Versions,
                pendingMatch.ExecutionMode,
                pendingMatch.FixtureId,
                pendingMatch.FixtureVersion,
                pendingMatch.MatchSeed,
                pendingMatch.CompetitionId,
                pendingMatch.ScheduleItemId,
                pendingMatch.SourceWeekPlanId,
                pendingMatch.SourceSlotActionId,
                pendingMatch.SourceActionOccurrenceId,
                pendingMatch.PreMatchPriority,
                pendingMatch.ContextDigest,
                pendingMatch.CanonicalContextUtf8,
                pendingMatch.HomeTeamId,
                pendingMatch.AwayTeamId,
                pendingMatch.OrderedPlayerIds,
                pendingMatch.ProtagonistPlayerId,
                pendingMatch.FrozenTrainingEmphases);
        }

        private Candidate ReadCandidate(string path, ProfileId profileId, SaveId saveId)
        {
            if (!_fileSystem.FileExists(path))
            {
                return Candidate.Missing();
            }

            var bytes = _fileSystem.ReadAllBytes(path);
            if (HasUnsupportedCareerVersions(bytes))
            {
                return Candidate.Unsupported();
            }

            try
            {
                var snapshot = CareerSaveJsonCodec.Deserialize(bytes);
                if (!snapshot.Identity.ProfileId.Equals(profileId) ||
                    !snapshot.Identity.SaveId.Equals(saveId))
                {
                    return Candidate.Invalid();
                }

                return Candidate.Valid(snapshot);
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

        private bool HasUnsupportedCareerVersions(byte[] bytes)
        {
            try
            {
                var root = StrictJsonReader.Parse(bytes);
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
                if (versions.ContainsUnknownProperty(
                        "schemaVersion",
                        "contentVersion",
                        "rulesetVersion",
                        "careerRandomAlgorithmVersion"))
                {
                    return true;
                }

                return Integer(versions.Get("schemaVersion")) !=
                       CareerSaveVersions.Current.SchemaVersion ||
                       Integer(versions.Get("contentVersion")) !=
                       CareerSaveVersions.Current.ContentVersion ||
                       Integer(versions.Get("rulesetVersion")) !=
                       CareerSaveVersions.Current.RulesetVersion ||
                       Integer(versions.Get("careerRandomAlgorithmVersion")) !=
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

        private IReadOnlyList<string> FindCandidates(
            ProfileId profileId,
            SaveId saveId,
            string marker)
        {
            var careersDirectory = _paths.CareersDirectory(profileId);
            if (!_fileSystem.DirectoryExists(careersDirectory))
            {
                return Array.Empty<string>();
            }

            var prefix = saveId + marker;
            var matches = new List<string>();
            var files = _fileSystem.EnumerateFiles(careersDirectory);
            for (var index = 0; index < files.Count; index++)
            {
                if (Path.GetFileName(files[index]).StartsWith(prefix, StringComparison.Ordinal))
                {
                    matches.Add(files[index]);
                }
            }

            return matches.AsReadOnly();
        }

        private void TryQuarantineTemporaryFiles(ProfileId profileId, SaveId saveId)
        {
            var temporaryFiles = new List<string>();
            var careersDirectory = _paths.CareersDirectory(profileId);
            if (_fileSystem.DirectoryExists(careersDirectory))
            {
                var files = _fileSystem.EnumerateFiles(careersDirectory);
                var prefix = saveId + ".";
                for (var index = 0; index < files.Count; index++)
                {
                    var fileName = Path.GetFileName(files[index]);
                    if (fileName.StartsWith(prefix, StringComparison.Ordinal) &&
                        fileName.IndexOf(".tmp.", StringComparison.Ordinal) >= 0)
                    {
                        temporaryFiles.Add(files[index]);
                    }
                }
            }

            for (var index = 0; index < temporaryFiles.Count; index++)
            {
                var source = temporaryFiles[index];
                if (!TryOperationIdFromPath(source, ".tmp.", out var operationId))
                {
                    continue;
                }

                TryMoveToQuarantine(
                    source,
                    profileId,
                    saveId,
                    CareerQuarantineSourceKind.Temporary,
                    operationId);
            }
        }

        private void TryQuarantineDoubleCorruption(
            ProfileId profileId,
            SaveId saveId,
            Candidate main,
            Candidate backup)
        {
            var operationId = new OperationId(Guid.NewGuid());
            if (main.Kind == CandidateKind.Invalid)
            {
                TryMoveToQuarantine(
                    _paths.CareerPath(profileId, saveId),
                    profileId,
                    saveId,
                    CareerQuarantineSourceKind.Main,
                    operationId);
            }

            if (backup.Kind == CandidateKind.Invalid)
            {
                TryMoveToQuarantine(
                    _paths.CareerBackupPath(profileId, saveId),
                    profileId,
                    saveId,
                    CareerQuarantineSourceKind.Backup,
                    operationId);
            }
        }

        private void TryMoveToQuarantine(
            string sourcePath,
            ProfileId profileId,
            SaveId saveId,
            CareerQuarantineSourceKind sourceKind,
            OperationId operationId)
        {
            try
            {
                if (!_fileSystem.FileExists(sourcePath))
                {
                    return;
                }

                _fileSystem.CreateDirectory(_paths.CareerQuarantineDirectory(profileId));
                _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(
                    sourcePath,
                    NextQuarantinePath(profileId, saveId, sourceKind, operationId));
            }
            catch (IOException)
            {
                // Quarantine failure never authorizes overwriting or deleting the source.
            }
            catch (UnauthorizedAccessException)
            {
                // Quarantine failure never authorizes overwriting or deleting the source.
            }
        }

        private string NextQuarantinePath(
            ProfileId profileId,
            SaveId saveId,
            CareerQuarantineSourceKind sourceKind,
            OperationId operationId)
        {
            for (var conflictIndex = 0; conflictIndex < 10000; conflictIndex++)
            {
                var candidate = _paths.CareerQuarantinePath(
                    profileId,
                    saveId,
                    sourceKind,
                    operationId,
                    conflictIndex);
                if (!_fileSystem.FileExists(candidate))
                {
                    return candidate;
                }
            }

            throw new IOException("No collision-free quarantine path could be allocated.");
        }

        private CareerPersistenceResult TryAcquireLock(
            ProfileId profileId,
            SaveId saveId,
            out IAtomicFileLock fileLock)
        {
            try
            {
                fileLock = _fileSystem.AcquireExclusiveLock(
                    _paths.CareerLockPath(profileId, saveId));
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

        private void EnsureCareerDirectories(ProfileId profileId)
        {
            _fileSystem.CreateDirectory(_paths.ProfilesDirectory);
            _fileSystem.CreateDirectory(_paths.ProfileDirectory(profileId));
            _fileSystem.CreateDirectory(_paths.CareersDirectory(profileId));
        }

        private static object Gate(ProfileId profileId, SaveId saveId)
        {
            return ProcessGates.GetOrAdd(profileId + "/" + saveId, _ => new object());
        }

        private static void RequireProfileAndSave(ProfileId profileId, SaveId saveId)
        {
            if (profileId.Value == Guid.Empty)
            {
                throw new ArgumentException("A stable profile ID is required.", nameof(profileId));
            }

            if (saveId.Value == Guid.Empty)
            {
                throw new ArgumentException("A stable save ID is required.", nameof(saveId));
            }
        }

        private static void RequireOperationId(OperationId operationId)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException("A stable operation ID is required.", nameof(operationId));
            }
        }

        private static bool TryOperationIdFromPath(
            string path,
            string marker,
            out OperationId operationId)
        {
            var fileName = Path.GetFileName(path);
            var markerIndex = fileName.LastIndexOf(marker, StringComparison.Ordinal);
            var value = markerIndex < 0
                ? null
                : fileName.Substring(markerIndex + marker.Length);
            if (value != null && value.EndsWith(".json", StringComparison.Ordinal))
            {
                value = value.Substring(0, value.Length - 5);
            }

            if (Guid.TryParseExact(value, "D", out var parsed) && parsed != Guid.Empty &&
                string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal))
            {
                operationId = new OperationId(parsed);
                return true;
            }

            operationId = default;
            return false;
        }

        private static long Integer(StrictJsonValue value)
        {
            if (value.Kind != StrictJsonKind.Integer)
            {
                throw new FormatException("A version field must be an integer.");
            }

            return value.IntegerValue;
        }

        private static Sha256Digest RawFingerprint(byte[] bytes)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                var characters = new char[hash.Length * 2];
                const string digits = "0123456789abcdef";
                for (var index = 0; index < hash.Length; index++)
                {
                    characters[index * 2] = digits[hash[index] >> 4];
                    characters[(index * 2) + 1] = digits[hash[index] & 0x0f];
                }

                return new Sha256Digest(new string(characters));
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
                // A leftover evidence file is safer than deleting the wrong candidate.
            }
            catch (UnauthorizedAccessException)
            {
                // A leftover evidence file is safer than deleting the wrong candidate.
            }
        }

        private static CareerPersistenceResult Result(
            PersistenceResultKind kind,
            CareerSaveSnapshot snapshot = null,
            CareerVersionToken? recoverableBackup = null)
        {
            return new CareerPersistenceResult(kind, snapshot, recoverableBackup);
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
            private Candidate(CandidateKind kind, CareerSaveSnapshot snapshot)
            {
                Kind = kind;
                Snapshot = snapshot;
            }

            public CandidateKind Kind { get; }

            public CareerSaveSnapshot Snapshot { get; }

            public bool HasToken(CareerVersionToken token)
            {
                return Kind == CandidateKind.Valid &&
                       Snapshot.Identity.VersionToken.Equals(token);
            }

            public static Candidate Missing()
            {
                return new Candidate(CandidateKind.Missing, null);
            }

            public static Candidate Valid(CareerSaveSnapshot snapshot)
            {
                return new Candidate(CandidateKind.Valid, snapshot);
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
