using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;

namespace Volleyball.Career.EditModeTests
{
    public sealed class LocalPersistenceRepositoryTests
    {
        private string _root;
        private CareerStoragePaths _paths;
        private SystemAtomicFileSystem _system;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "volleyball-career-repository-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _paths = new CareerStoragePaths(_root);
            _system = new SystemAtomicFileSystem();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void CareerRepository_CreateLoadCommitAndFullTokenCasRoundTrip()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));

            var created = repository.Create(initial, new OperationId(Guid.NewGuid()));
            var loaded = repository.Load(
                initial.Identity.ProfileId,
                initial.Identity.SaveId);
            var next = CareerPersistenceTestData.AfterFirstTryoutStage(created.Snapshot);
            var committed = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                created.Snapshot.Identity.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));

            Assert.That(created.Kind, Is.EqualTo(PersistenceResultKind.Created));
            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(
                loaded.Snapshot.Identity.VersionToken,
                Is.EqualTo(created.Snapshot.Identity.VersionToken));
            Assert.That(committed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(committed.Snapshot.Identity.Revision, Is.EqualTo(2));
            Assert.That(
                CareerSaveJsonCodec.Deserialize(
                    _system.ReadAllBytes(
                        _paths.CareerBackupPath(
                            initial.Identity.ProfileId,
                            initial.Identity.SaveId))).Identity.VersionToken,
                Is.EqualTo(created.Snapshot.Identity.VersionToken));

            var wrongHashToken = new CareerVersionToken(
                created.Snapshot.Identity.LineageId,
                created.Snapshot.Identity.Revision,
                CareerPersistenceTestData.Hash('f'));
            var conflict = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                wrongHashToken,
                next,
                new OperationId(Guid.NewGuid()));
            Assert.That(conflict.Kind, Is.EqualTo(PersistenceResultKind.VersionConflict));
            Assert.That(
                repository.Load(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId).Snapshot.Identity.VersionToken,
                Is.EqualTo(committed.Snapshot.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_CommitAndRecoveryPreserveTrainingEmphasisLedger()
        {
            var profileId = new ProfileId(Guid.NewGuid());
            var saveId = new SaveId(Guid.NewGuid());
            var lineageId = new LineageId(Guid.NewGuid());
            var prior = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.PlannedAfterFirstTraining(
                    profileId,
                    saveId,
                    lineageId));
            var primaryPath = _paths.CareerPath(profileId, saveId);
            _system.CreateDirectory(_paths.CareersDirectory(profileId));
            _system.CreateFileDurably(primaryPath, CareerSaveJsonCodec.Serialize(prior));
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var loaded = repository.Load(profileId, saveId);
            var next = CareerPersistenceTestData.AfterSecondTraining(loaded.Snapshot);
            var operationId = next.OperationReceipts[next.OperationReceipts.Count - 1].OperationId;

            var committed = repository.Commit(
                profileId,
                saveId,
                loaded.Snapshot.Identity.VersionToken,
                next,
                operationId);

            Assert.That(committed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(committed.Snapshot.TrainingEmphases.Contributions, Has.Count.EqualTo(2));
            Assert.That(committed.Snapshot.TrainingEmphases.Contributions[1].Direction,
                Is.EqualTo(CareerTrainingDirection.Jump));

            var corruptBytes = new byte[] { 0xff };
            _system.OverwriteFileDurably(primaryPath, corruptBytes);
            var recoveryAvailable = repository.Load(profileId, saveId);
            var recovered = repository.RecoverFromBackup(
                profileId,
                saveId,
                recoveryAvailable.RecoverableBackup.Value,
                RawHash(corruptBytes),
                new OperationId(Guid.NewGuid()),
                committed.Snapshot.Identity.UpdatedAtUtcMs + 1,
                new LineageId(Guid.NewGuid()));

            Assert.That(recoveryAvailable.Kind, Is.EqualTo(PersistenceResultKind.RecoveryAvailable));
            Assert.That(recovered.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(recovered.Snapshot.TrainingEmphases.Contributions, Has.Count.EqualTo(1));
            Assert.That(recovered.Snapshot.TrainingEmphases.Contributions[0].Direction,
                Is.EqualTo(CareerTrainingDirection.Spike));
        }

        [Test]
        public void CareerRepository_ReplaceThrowAfterPublishUsesRescanAndCommits()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CreateCareer(repository);
            var next = CareerPersistenceTestData.AfterFirstTryoutStage(initial);
            faults.Mode = FaultMode.ThrowAfterReplace;

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            faults.Mode = FaultMode.None;
            Assert.That(
                repository.Load(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId).Snapshot.Identity.Revision,
                Is.EqualTo(2));
        }

        [Test]
        public void CareerRepository_CommitDoesNotBypassAnotherReplaceOperation()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            _system.CreateFileDurably(
                _paths.CareerReplaceBackupPath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                CareerSaveJsonCodec.Serialize(initial));

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(
                CareerSaveJsonCodec.Deserialize(
                    _system.ReadAllBytes(
                        _paths.CareerPath(
                            initial.Identity.ProfileId,
                            initial.Identity.SaveId))).Identity.VersionToken,
                Is.EqualTo(initial.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_CommitDoesNotBypassAnotherBackupConvergenceOperation()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            _system.CreateFileDurably(
                _paths.CareerBackupConvergencePath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                CareerSaveJsonCodec.Serialize(initial));

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(
                CareerSaveJsonCodec.Deserialize(
                    _system.ReadAllBytes(
                        _paths.CareerPath(
                            initial.Identity.ProfileId,
                            initial.Identity.SaveId))).Identity.VersionToken,
                Is.EqualTo(initial.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_CommitDoesNotBypassAnotherRecoveryOperation()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            _system.CreateFileDurably(
                _paths.CareerRecoveryIntentPath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                new byte[] { (byte)'{', (byte)'}' });

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousRestoreState));
            Assert.That(
                repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId).Kind,
                Is.EqualTo(PersistenceResultKind.AmbiguousRestoreState));
        }

        [Test]
        public void CareerRepository_CommitPreservesAndRejectsUnsupportedFixedBackup()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            var mainPath = _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId);
            var backupPath = _paths.CareerBackupPath(initial.Identity.ProfileId, initial.Identity.SaveId);
            var mainBytes = _system.ReadAllBytes(mainPath);
            var unsupportedBackupBytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(mainBytes)
                    .Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
            _system.CreateFileDurably(backupPath, unsupportedBackupBytes);

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.UnsupportedVersion));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(mainBytes));
            Assert.That(_system.ReadAllBytes(backupPath), Is.EqualTo(unsupportedBackupBytes));
        }

        [Test]
        public void CareerRepository_CommitPreservesAndRejectsANewerValidFixedBackup()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var current = CreateCareer(repository);
            var mainPath = _paths.CareerPath(current.Identity.ProfileId, current.Identity.SaveId);
            var backupPath = _paths.CareerBackupPath(
                current.Identity.ProfileId,
                current.Identity.SaveId);
            var mainBytes = _system.ReadAllBytes(mainPath);
            var alternateCurrent = CareerPersistenceTestData.CreatedSnapshot(
                current.Identity.ProfileId,
                current.Identity.SaveId,
                current.Identity.LineageId,
                "Ahead Backup");
            var aheadBackup = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.AfterFirstTryoutStage(alternateCurrent));
            var backupBytes = CareerSaveJsonCodec.Serialize(aheadBackup);
            _system.CreateFileDurably(backupPath, backupBytes);
            var operationId = new OperationId(Guid.NewGuid());

            var result = repository.Commit(
                current.Identity.ProfileId,
                current.Identity.SaveId,
                current.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(current),
                operationId);

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(mainBytes));
            Assert.That(_system.ReadAllBytes(backupPath), Is.EqualTo(backupBytes));
            Assert.That(
                _system.FileExists(
                    _paths.CareerTemporaryPath(
                        current.Identity.ProfileId,
                        current.Identity.SaveId,
                        operationId)),
                Is.False);
            Assert.That(
                _system.FileExists(
                    _paths.CareerReplaceBackupPath(
                        current.Identity.ProfileId,
                        current.Identity.SaveId,
                        operationId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_CommitPreservesAndRejectsAConflictingSameRevisionBackup()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var current = CreateCareer(repository);
            var mainPath = _paths.CareerPath(current.Identity.ProfileId, current.Identity.SaveId);
            var backupPath = _paths.CareerBackupPath(
                current.Identity.ProfileId,
                current.Identity.SaveId);
            var mainBytes = _system.ReadAllBytes(mainPath);
            var conflictingBackup = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.CreatedSnapshot(
                    current.Identity.ProfileId,
                    current.Identity.SaveId,
                    new LineageId(Guid.NewGuid()),
                    "Conflicting Backup"));
            var backupBytes = CareerSaveJsonCodec.Serialize(conflictingBackup);
            _system.CreateFileDurably(backupPath, backupBytes);
            var operationId = new OperationId(Guid.NewGuid());

            var result = repository.Commit(
                current.Identity.ProfileId,
                current.Identity.SaveId,
                current.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(current),
                operationId);

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(mainBytes));
            Assert.That(_system.ReadAllBytes(backupPath), Is.EqualTo(backupBytes));
            Assert.That(
                _system.FileExists(
                    _paths.CareerTemporaryPath(
                        current.Identity.ProfileId,
                        current.Identity.SaveId,
                        operationId)),
                Is.False);
            Assert.That(
                _system.FileExists(
                    _paths.CareerReplaceBackupPath(
                        current.Identity.ProfileId,
                        current.Identity.SaveId,
                        operationId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_CommitAllowsAStrictPreviousRevisionFixedBackup()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var saveId = new SaveId(Guid.NewGuid());
            var lineageId = new LineageId(Guid.NewGuid());
            var backup = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.PlanningSnapshot(
                    profileId,
                    saveId,
                    lineageId,
                    4,
                    "Previous Backup",
                    10));
            var current = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.PlanningSnapshot(
                    profileId,
                    saveId,
                    lineageId,
                    5,
                    "Current Main",
                    11));
            Directory.CreateDirectory(_paths.CareersDirectory(profileId));
            _system.CreateFileDurably(
                _paths.CareerBackupPath(profileId, saveId),
                CareerSaveJsonCodec.Serialize(backup));
            _system.CreateFileDurably(
                _paths.CareerPath(profileId, saveId),
                CareerSaveJsonCodec.Serialize(current));

            var result = repository.Commit(
                profileId,
                saveId,
                current.Identity.VersionToken,
                CareerPersistenceTestData.NextPlanningSnapshot(current),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(
                CareerSaveJsonCodec.Deserialize(
                    _system.ReadAllBytes(_paths.CareerBackupPath(profileId, saveId)))
                    .Identity.VersionToken,
                Is.EqualTo(current.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_CommitAllowsFixedBackupMatchingRestoreSource()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var saveId = new SaveId(Guid.NewGuid());
            var backup = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.PlanningSnapshot(
                    profileId,
                    saveId,
                    new LineageId(Guid.NewGuid()),
                    4,
                    "Recovery Source",
                    10));
            Directory.CreateDirectory(_paths.CareersDirectory(profileId));
            _system.CreateFileDurably(
                _paths.CareerBackupPath(profileId, saveId),
                CareerSaveJsonCodec.Serialize(backup));
            var recovered = repository.RecoverFromBackup(
                profileId,
                saveId,
                backup.Identity.VersionToken,
                null,
                new OperationId(Guid.NewGuid()),
                11,
                new LineageId(Guid.NewGuid()));

            var result = repository.Commit(
                profileId,
                saveId,
                recovered.Snapshot.Identity.VersionToken,
                CareerPersistenceTestData.NextPlanningSnapshot(recovered.Snapshot),
                new OperationId(Guid.NewGuid()));

            Assert.That(recovered.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(
                CareerSaveJsonCodec.Deserialize(
                    _system.ReadAllBytes(_paths.CareerBackupPath(profileId, saveId)))
                    .Identity.VersionToken,
                Is.EqualTo(recovered.Snapshot.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_CreateMoveThrowAfterPublishUsesRescan()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system)
            {
                Mode = FaultMode.ThrowAfterMove
            };
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.Created));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(1));
        }

        [Test]
        public void CareerRepository_CreateWithOnlyFixedBackupOffersRecovery()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));
            var sealedInitial = CareerSaveJsonCodec.Seal(initial);
            Directory.CreateDirectory(_paths.CareersDirectory(initial.Identity.ProfileId));
            _system.CreateFileDurably(
                _paths.CareerBackupPath(initial.Identity.ProfileId, initial.Identity.SaveId),
                CareerSaveJsonCodec.Serialize(sealedInitial));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.RecoveryAvailable));
            Assert.That(
                result.RecoverableBackup,
                Is.EqualTo(sealedInitial.Identity.VersionToken));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_CreateWithStaleTemporaryDoesNotPublishNewAuthority()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));
            Directory.CreateDirectory(_paths.CareersDirectory(initial.Identity.ProfileId));
            _system.CreateFileDurably(
                _paths.CareerTemporaryPath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                CareerSaveJsonCodec.Serialize(CareerSaveJsonCodec.Seal(initial)));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_CreateWithUnresolvedReplaceBackupDoesNotPublishNewAuthority()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));
            Directory.CreateDirectory(_paths.CareersDirectory(initial.Identity.ProfileId));
            _system.CreateFileDurably(
                _paths.CareerReplaceBackupPath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                CareerSaveJsonCodec.Serialize(CareerSaveJsonCodec.Seal(initial)));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_CreateWithRecoveryIntentDoesNotBypassRestoreResolution()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));
            Directory.CreateDirectory(_paths.CareersDirectory(initial.Identity.ProfileId));
            _system.CreateFileDurably(
                _paths.CareerRecoveryIntentPath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                new byte[] { (byte)'{', (byte)'}' });

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousRestoreState));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_CreateWithBackupConvergenceDoesNotPublishNewAuthority()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));
            Directory.CreateDirectory(_paths.CareersDirectory(initial.Identity.ProfileId));
            _system.CreateFileDurably(
                _paths.CareerBackupConvergencePath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                CareerSaveJsonCodec.Serialize(CareerSaveJsonCodec.Seal(initial)));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_CreateWithRepairTemporaryDoesNotPublishNewAuthority()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));
            Directory.CreateDirectory(_paths.CareersDirectory(initial.Identity.ProfileId));
            _system.CreateFileDurably(
                _paths.CareerRepairTemporaryPath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                CareerSaveJsonCodec.Serialize(CareerSaveJsonCodec.Seal(initial)));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_CreateWithRecoveryTemporaryDoesNotPublishNewAuthority()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));
            Directory.CreateDirectory(_paths.CareersDirectory(initial.Identity.ProfileId));
            _system.CreateFileDurably(
                _paths.CareerRecoveryTemporaryPath(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId,
                    new OperationId(Guid.NewGuid())),
                CareerSaveJsonCodec.Serialize(CareerSaveJsonCodec.Seal(initial)));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousRestoreState));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_DurableTempFailureDoesNotCreateAuthority()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system)
            {
                Mode = FaultMode.ThrowBeforeDurableCreate
            };
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.NotCommitted));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void CareerRepository_ReplaceThrowBeforePublishKeepsOldAuthority()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CreateCareer(repository);
            var next = CareerPersistenceTestData.AfterFirstTryoutStage(initial);
            faults.Mode = FaultMode.ThrowBeforeReplace;

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.NotCommitted));
            faults.Mode = FaultMode.None;
            var loaded = repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId);
            Assert.That(loaded.Snapshot.Identity.VersionToken, Is.EqualTo(initial.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_PartialReplaceRestoresExpectedMainWithoutPublishingTemp()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CreateCareer(repository);
            var next = CareerPersistenceTestData.AfterFirstTryoutStage(initial);
            faults.Mode = FaultMode.MoveMainToOperationBackupThenThrow;

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.NotCommitted));
            faults.Mode = FaultMode.None;
            var loaded = repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId);
            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(loaded.Snapshot.Identity.VersionToken, Is.EqualTo(initial.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_BackupConvergenceFailureKeepsNewMainReadOnly()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CreateCareer(repository);
            var next = CareerPersistenceTestData.AfterFirstTryoutStage(initial);
            faults.Mode = FaultMode.ThrowMovingOperationBackupToFixedBackup;

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.BackupDegraded));
            Assert.That(result.Snapshot.Identity.Revision, Is.EqualTo(2));
        }

        [Test]
        public void CareerRepository_BackupConvergenceLostSuccessRescansFixedBackup()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CreateCareer(repository);
            var next = CareerPersistenceTestData.AfterFirstTryoutStage(initial);
            faults.Mode = FaultMode.ThrowAfterMove;

            var result = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(
                CareerSaveJsonCodec.Deserialize(
                    _system.ReadAllBytes(
                        _paths.CareerBackupPath(
                            initial.Identity.ProfileId,
                            initial.Identity.SaveId))).Identity.VersionToken,
                Is.EqualTo(initial.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_LoadConvergesACompletedStandaloneBackupReplacement()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            var committed = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));
            var convergencePath = _paths.CareerBackupConvergencePath(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(
                convergencePath,
                _system.ReadAllBytes(
                    _paths.CareerBackupPath(initial.Identity.ProfileId, initial.Identity.SaveId)));

            var loaded = repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId);

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(
                loaded.Snapshot.Identity.VersionToken,
                Is.EqualTo(committed.Snapshot.Identity.VersionToken));
            Assert.That(_system.FileExists(convergencePath), Is.False);
        }

        [Test]
        public void CareerRepository_LoadConvergesDisplacedBackupFromPreRecoveryLineage()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var saveId = new SaveId(Guid.NewGuid());
            var displaced = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.PlanningSnapshot(
                    profileId,
                    saveId,
                    new LineageId(Guid.NewGuid()),
                    4));
            var fixedBackup = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.PlanningSnapshot(
                    profileId,
                    saveId,
                    new LineageId(Guid.NewGuid()),
                    5));
            var main = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.NextPlanningSnapshot(fixedBackup));
            Directory.CreateDirectory(_paths.CareersDirectory(profileId));
            _system.CreateFileDurably(
                _paths.CareerPath(profileId, saveId),
                CareerSaveJsonCodec.Serialize(main));
            _system.CreateFileDurably(
                _paths.CareerBackupPath(profileId, saveId),
                CareerSaveJsonCodec.Serialize(fixedBackup));
            var convergencePath = _paths.CareerBackupConvergencePath(
                profileId,
                saveId,
                new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(
                convergencePath,
                CareerSaveJsonCodec.Serialize(displaced));

            var loaded = repository.Load(profileId, saveId);

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(loaded.Snapshot.Identity.VersionToken, Is.EqualTo(main.Identity.VersionToken));
            Assert.That(_system.FileExists(convergencePath), Is.False);
        }

        [Test]
        public void CareerRepository_RecoveryRejectsAHealthyMainEvenWhenItsFingerprintMatches()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            var committed = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));
            var mainPath = _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId);
            var healthyMainBytes = _system.ReadAllBytes(mainPath);

            var recovered = repository.RecoverFromBackup(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                RawHash(healthyMainBytes),
                new OperationId(Guid.NewGuid()),
                committed.Snapshot.Identity.UpdatedAtUtcMs + 1,
                new LineageId(Guid.NewGuid()));

            Assert.That(recovered.Kind, Is.EqualTo(PersistenceResultKind.VersionConflict));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(healthyMainBytes));
        }

        [Test]
        public void CareerRepository_RecoveryPreservesAndRejectsAnUnsupportedMain()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));
            var mainPath = _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId);
            var unsupportedBytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(_system.ReadAllBytes(mainPath))
                    .Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
            _system.OverwriteFileDurably(mainPath, unsupportedBytes);

            var recovered = repository.RecoverFromBackup(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                RawHash(unsupportedBytes),
                new OperationId(Guid.NewGuid()),
                initial.Identity.UpdatedAtUtcMs + 2,
                new LineageId(Guid.NewGuid()));

            Assert.That(recovered.Kind, Is.EqualTo(PersistenceResultKind.UnsupportedVersion));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(unsupportedBytes));
        }

        [Test]
        public void CareerRepository_CorruptMainOffersConfirmedBackupRecoveryWithNewLineage()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            var next = CareerPersistenceTestData.AfterFirstTryoutStage(initial);
            var committed = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));
            Assert.That(committed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            var mainPath = _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId);
            var corruptBytes = new byte[] { 0xff, 0x00, 0x7b, 0x01 };
            _system.OverwriteFileDurably(mainPath, corruptBytes);

            var offered = repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId);
            var newLineage = new LineageId(Guid.NewGuid());
            var recovered = repository.RecoverFromBackup(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                offered.RecoverableBackup.Value,
                RawHash(corruptBytes),
                new OperationId(Guid.NewGuid()),
                100,
                newLineage);

            Assert.That(offered.Kind, Is.EqualTo(PersistenceResultKind.RecoveryAvailable));
            Assert.That(recovered.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(recovered.Snapshot.Identity.LineageId, Is.EqualTo(newLineage));
            Assert.That(
                recovered.Snapshot.Identity.RestoredFromVersionToken,
                Is.EqualTo(initial.Identity.VersionToken));
            Assert.That(
                recovered.Snapshot.OperationReceipts,
                Has.All.Matches<OperationReceipt>(receipt =>
                    receipt.AppliedLineageId.Equals(newLineage)));
            Assert.That(
                CareerSaveJsonCodec.Deserialize(
                    _system.ReadAllBytes(
                        _paths.CareerBackupPath(
                            initial.Identity.ProfileId,
                            initial.Identity.SaveId))).Identity.VersionToken,
                Is.EqualTo(initial.Identity.VersionToken));
            Assert.That(
                _system.EnumerateFiles(
                    _paths.CareerQuarantineDirectory(initial.Identity.ProfileId)),
                Has.Some.Matches<string>(path => path.Contains(".corrupt.")));
        }

        [Test]
        public void CareerRepository_RecoveryThrowBeforePublishKeepsPromptAndNeverPromotesTemp()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CreateCareer(repository);
            var committed = repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));
            Assert.That(committed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            var mainPath = _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId);
            var corrupt = new byte[] { 0x7b, 0x01 };
            _system.OverwriteFileDurably(mainPath, corrupt);
            var offered = repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId);
            faults.Mode = FaultMode.ThrowBeforeReplace;

            var result = repository.RecoverFromBackup(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                offered.RecoverableBackup.Value,
                RawHash(corrupt),
                new OperationId(Guid.NewGuid()),
                100,
                new LineageId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.RecoveryAvailable));
            faults.Mode = FaultMode.None;
            var reloaded = repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId);
            Assert.That(reloaded.Kind, Is.EqualTo(PersistenceResultKind.RecoveryAvailable));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(corrupt));
        }

        [Test]
        public void CareerRepository_RecoveryThrowAfterPublishAcceptsVerifiedNewMain()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CreateCareer(repository);
            repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));
            var mainPath = _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId);
            var corrupt = new byte[] { 0xff, 0xfe, 0x00 };
            _system.OverwriteFileDurably(mainPath, corrupt);
            var offered = repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId);
            faults.Mode = FaultMode.ThrowAfterReplace;

            var result = repository.RecoverFromBackup(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                offered.RecoverableBackup.Value,
                RawHash(corrupt),
                new OperationId(Guid.NewGuid()),
                100,
                new LineageId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(result.Snapshot.Identity.RestoredFromVersionToken, Is.Not.Null);
        }

        [Test]
        public void CareerRepository_MissingMainCanRecoverOnlyFromConfirmedFixedBackup()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var initial = CreateCareer(repository);
            repository.Commit(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                initial.Identity.VersionToken,
                CareerPersistenceTestData.AfterFirstTryoutStage(initial),
                new OperationId(Guid.NewGuid()));
            File.Delete(_paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId));
            var offered = repository.Load(initial.Identity.ProfileId, initial.Identity.SaveId);

            var recovered = repository.RecoverFromBackup(
                initial.Identity.ProfileId,
                initial.Identity.SaveId,
                offered.RecoverableBackup.Value,
                null,
                new OperationId(Guid.NewGuid()),
                100,
                new LineageId(Guid.NewGuid()));

            Assert.That(offered.Kind, Is.EqualTo(PersistenceResultKind.RecoveryAvailable));
            Assert.That(recovered.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(
                repository.Load(
                    initial.Identity.ProfileId,
                    initial.Identity.SaveId).Snapshot.Identity.VersionToken,
                Is.EqualTo(recovered.Snapshot.Identity.VersionToken));
        }

        [Test]
        public void CareerRepository_LockFailureDoesNotTouchAuthority()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system)
            {
                Mode = FaultMode.ThrowOnLock
            };
            var repository = new LocalCareerSaveRepository(_paths, faults);
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));

            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.LockUnavailable));
            Assert.That(
                _system.FileExists(
                    _paths.CareerPath(initial.Identity.ProfileId, initial.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void ProfileIndexAndCatalog_RebuildMultipleProfilesAndOrphanCareers()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            var careerRepository = new LocalCareerSaveRepository(_paths, _system);
            var catalogRepository = new LocalProfileCatalogRepository(_paths, _system);
            var firstProfileId = new ProfileId(Guid.NewGuid());
            var secondProfileId = new ProfileId(Guid.NewGuid());
            Assert.That(
                profileRepository.Create(
                    CareerPersistenceTestData.Profile(firstProfileId),
                    new OperationId(Guid.NewGuid())).Kind,
                Is.EqualTo(PersistenceResultKind.Created));
            Assert.That(
                profileRepository.Create(
                    CareerPersistenceTestData.Profile(secondProfileId),
                    new OperationId(Guid.NewGuid())).Kind,
                Is.EqualTo(PersistenceResultKind.Created));

            CreateTwoCareers(careerRepository, firstProfileId, "First");
            CreateTwoCareers(careerRepository, secondProfileId, "Second");
            var firstRebuilt = profileRepository.RebuildCareerIndex(
                firstProfileId,
                new OperationId(Guid.NewGuid()));
            var secondRebuilt = profileRepository.RebuildCareerIndex(
                secondProfileId,
                new OperationId(Guid.NewGuid()));
            var catalog = catalogRepository.LoadOrRebuild(new OperationId(Guid.NewGuid()));

            Assert.That(firstRebuilt.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(firstRebuilt.Profile.CareerEntries, Has.Count.EqualTo(2));
            Assert.That(secondRebuilt.Profile.CareerEntries, Has.Count.EqualTo(2));
            Assert.That(
                firstRebuilt.Profile.CareerEntries,
                Has.All.Matches<CareerIndexEntry>(entry =>
                    entry.CareerName.StartsWith("First", StringComparison.Ordinal)));
            Assert.That(catalog.Catalog.Profiles, Has.Count.EqualTo(2));

            _system.OverwriteFileDurably(_paths.ProfilesIndexPath, new byte[] { 0x7b, 0x00 });
            var repairedCatalog = catalogRepository.LoadOrRebuild(new OperationId(Guid.NewGuid()));
            Assert.That(repairedCatalog.Kind, Is.EqualTo(PersistenceResultKind.Created));
            Assert.That(repairedCatalog.Catalog.Profiles, Has.Count.EqualTo(2));
        }

        [Test]
        public void ProfileRepository_UsesFullHashCasAndKeepsPreviousRevisionBackup()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var candidate = new LocalPlayerProfile(
                1,
                profileId,
                2,
                CareerPersistenceTestData.Hash('0'),
                "Renamed Local Player",
                created.Profile.CreatedAtUtcMs,
                created.Profile.UpdatedAtUtcMs + 1,
                created.Profile.CareerEntries);

            var committed = repository.Commit(
                profileId,
                created.Profile.VersionToken,
                candidate,
                new OperationId(Guid.NewGuid()));
            var wrongHash = new ProfileVersionToken(
                created.Profile.ProfileRevision,
                CareerPersistenceTestData.Hash('f'));
            var conflict = repository.Commit(
                profileId,
                wrongHash,
                candidate,
                new OperationId(Guid.NewGuid()));

            Assert.That(committed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(committed.Profile.DisplayName, Is.EqualTo("Renamed Local Player"));
            Assert.That(conflict.Kind, Is.EqualTo(PersistenceResultKind.VersionConflict));
            Assert.That(
                LocalProfileJsonCodec.DeserializeProfile(
                    _system.ReadAllBytes(_paths.ProfileBackupPath(profileId))).VersionToken,
                Is.EqualTo(created.Profile.VersionToken));
        }

        [Test]
        public void ProfileRepository_CreateWithOnlyFixedBackupOffersRecovery()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var backup = LocalProfileJsonCodec.SealProfile(
                CareerPersistenceTestData.Profile(profileId));
            Directory.CreateDirectory(_paths.ProfileDirectory(profileId));
            _system.CreateFileDurably(
                _paths.ProfileBackupPath(profileId),
                LocalProfileJsonCodec.SerializeProfile(backup));

            var result = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.RecoveryAvailable));
            Assert.That(_system.FileExists(_paths.ProfilePath(profileId)), Is.False);
        }

        [Test]
        public void ProfileRepository_CreateDoesNotBypassAnotherReplaceOperation()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var profile = LocalProfileJsonCodec.SealProfile(
                CareerPersistenceTestData.Profile(profileId));
            Directory.CreateDirectory(_paths.ProfileDirectory(profileId));
            _system.CreateFileDurably(
                _paths.ProfileReplaceBackupPath(profileId, new OperationId(Guid.NewGuid())),
                LocalProfileJsonCodec.SerializeProfile(profile));

            var result = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(_system.FileExists(_paths.ProfilePath(profileId)), Is.False);
        }

        [Test]
        public void ProfileRepository_CommitDoesNotBypassAnotherReplaceOperation()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(
                _paths.ProfileReplaceBackupPath(profileId, new OperationId(Guid.NewGuid())),
                LocalProfileJsonCodec.SerializeProfile(created.Profile));

            var result = repository.Commit(
                profileId,
                created.Profile.VersionToken,
                UpdatedProfile(created.Profile, "Must Not Commit"),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(
                LocalProfileJsonCodec.DeserializeProfile(
                    _system.ReadAllBytes(_paths.ProfilePath(profileId))).VersionToken,
                Is.EqualTo(created.Profile.VersionToken));
        }

        [Test]
        public void ProfileRepository_CommitPreservesAndRejectsUnsupportedFixedBackup()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var mainPath = _paths.ProfilePath(profileId);
            var backupPath = _paths.ProfileBackupPath(profileId);
            var mainBytes = _system.ReadAllBytes(mainPath);
            var unsupportedBackupBytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(mainBytes)
                    .Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
            _system.CreateFileDurably(backupPath, unsupportedBackupBytes);

            var result = repository.Commit(
                profileId,
                created.Profile.VersionToken,
                UpdatedProfile(created.Profile, "Must Not Replace Backup"),
                new OperationId(Guid.NewGuid()));

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.UnsupportedVersion));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(mainBytes));
            Assert.That(_system.ReadAllBytes(backupPath), Is.EqualTo(unsupportedBackupBytes));
        }

        [Test]
        public void ProfileRepository_CommitPreservesAndRejectsANewerValidFixedBackup()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var current = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var mainPath = _paths.ProfilePath(profileId);
            var backupPath = _paths.ProfileBackupPath(profileId);
            var mainBytes = _system.ReadAllBytes(mainPath);
            var aheadBackup = LocalProfileJsonCodec.SealProfile(
                UpdatedProfile(current.Profile, "Ahead Backup"));
            var backupBytes = LocalProfileJsonCodec.SerializeProfile(aheadBackup);
            _system.CreateFileDurably(backupPath, backupBytes);
            var operationId = new OperationId(Guid.NewGuid());

            var result = repository.Commit(
                profileId,
                current.Profile.VersionToken,
                UpdatedProfile(current.Profile, "Must Not Commit"),
                operationId);

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(mainBytes));
            Assert.That(_system.ReadAllBytes(backupPath), Is.EqualTo(backupBytes));
            Assert.That(
                _system.FileExists(_paths.ProfileTemporaryPath(profileId, operationId)),
                Is.False);
            Assert.That(
                _system.FileExists(_paths.ProfileReplaceBackupPath(profileId, operationId)),
                Is.False);
        }

        [Test]
        public void ProfileRepository_CommitPreservesAndRejectsAConflictingSameRevisionBackup()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var current = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var mainPath = _paths.ProfilePath(profileId);
            var backupPath = _paths.ProfileBackupPath(profileId);
            var mainBytes = _system.ReadAllBytes(mainPath);
            var conflictingBackup = LocalProfileJsonCodec.SealProfile(
                new LocalPlayerProfile(
                    LocalPlayerProfile.CurrentSchemaVersion,
                    profileId,
                    current.Profile.ProfileRevision,
                    CareerPersistenceTestData.Hash('0'),
                    "Conflicting Backup",
                    current.Profile.CreatedAtUtcMs,
                    current.Profile.UpdatedAtUtcMs + 1,
                    current.Profile.CareerEntries));
            var backupBytes = LocalProfileJsonCodec.SerializeProfile(conflictingBackup);
            _system.CreateFileDurably(backupPath, backupBytes);
            var operationId = new OperationId(Guid.NewGuid());

            var result = repository.Commit(
                profileId,
                current.Profile.VersionToken,
                UpdatedProfile(current.Profile, "Must Not Commit"),
                operationId);

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(_system.ReadAllBytes(mainPath), Is.EqualTo(mainBytes));
            Assert.That(_system.ReadAllBytes(backupPath), Is.EqualTo(backupBytes));
            Assert.That(
                _system.FileExists(_paths.ProfileTemporaryPath(profileId, operationId)),
                Is.False);
            Assert.That(
                _system.FileExists(_paths.ProfileReplaceBackupPath(profileId, operationId)),
                Is.False);
        }

        [Test]
        public void ProfileRepository_CommitAllowsAStrictPreviousRevisionFixedBackup()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var second = repository.Commit(
                profileId,
                created.Profile.VersionToken,
                UpdatedProfile(created.Profile, "Second Revision"),
                new OperationId(Guid.NewGuid()));

            var third = repository.Commit(
                profileId,
                second.Profile.VersionToken,
                UpdatedProfile(second.Profile, "Third Revision"),
                new OperationId(Guid.NewGuid()));

            Assert.That(second.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(third.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(
                LocalProfileJsonCodec.DeserializeProfile(
                    _system.ReadAllBytes(_paths.ProfileBackupPath(profileId))).VersionToken,
                Is.EqualTo(second.Profile.VersionToken));
        }

        [Test]
        public void ProfileRepository_BackupConvergenceLostSuccessRescansFixedBackup()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalPlayerProfileRepository(_paths, faults);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var candidate = UpdatedProfile(created.Profile, "Converged Profile");
            faults.Mode = FaultMode.ThrowAfterMove;

            var committed = repository.Commit(
                profileId,
                created.Profile.VersionToken,
                candidate,
                new OperationId(Guid.NewGuid()));

            Assert.That(committed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(
                LocalProfileJsonCodec.DeserializeProfile(
                    _system.ReadAllBytes(_paths.ProfileBackupPath(profileId))).VersionToken,
                Is.EqualTo(created.Profile.VersionToken));
        }

        [Test]
        public void ProfileRepository_LoadConvergesACompletedStandaloneBackupReplacement()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var committed = repository.Commit(
                profileId,
                created.Profile.VersionToken,
                UpdatedProfile(created.Profile, "Committed Profile"),
                new OperationId(Guid.NewGuid()));
            var convergencePath = _paths.ProfileBackupConvergencePath(
                profileId,
                new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(
                convergencePath,
                _system.ReadAllBytes(_paths.ProfileBackupPath(profileId)));

            var loaded = repository.Load(profileId);

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(loaded.Profile.VersionToken, Is.EqualTo(committed.Profile.VersionToken));
            Assert.That(_system.FileExists(convergencePath), Is.False);
        }

        [Test]
        public void ProfileRepository_PartialReplaceRestoresExpectedAndCleansOperationFiles()
        {
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalPlayerProfileRepository(_paths, faults);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var operationId = new OperationId(Guid.NewGuid());
            faults.Mode = FaultMode.MoveMainToOperationBackupThenThrow;

            var result = repository.Commit(
                profileId,
                created.Profile.VersionToken,
                UpdatedProfile(created.Profile, "Uncommitted Profile"),
                operationId);

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.NotCommitted));
            Assert.That(
                _system.FileExists(_paths.ProfileReplaceBackupPath(profileId, operationId)),
                Is.False);
            Assert.That(
                _system.FileExists(_paths.ProfileTemporaryPath(profileId, operationId)),
                Is.False);
            faults.Mode = FaultMode.None;
            Assert.That(
                repository.Load(profileId).Profile.VersionToken,
                Is.EqualTo(created.Profile.VersionToken));
        }

        [Test]
        public void ProfileRepository_DuplicateInterruptedBackupOfMainIsCleanedDeterministically()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var operationId = new OperationId(Guid.NewGuid());
            var mainPath = _paths.ProfilePath(profileId);
            var operationBackupPath = _paths.ProfileReplaceBackupPath(profileId, operationId);
            var temporaryPath = _paths.ProfileTemporaryPath(profileId, operationId);
            _system.CopyFileWhenDestinationDoesNotExist(mainPath, operationBackupPath);
            _system.CreateFileDurably(temporaryPath, _system.ReadAllBytes(mainPath));

            var loaded = repository.Load(profileId);

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(loaded.Profile.VersionToken, Is.EqualTo(created.Profile.VersionToken));
            Assert.That(_system.FileExists(operationBackupPath), Is.False);
            Assert.That(_system.FileExists(temporaryPath), Is.False);
        }

        [Test]
        public void ProfileRepository_LoadCleansOrphanTemporaryFilesBesideValidMain()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var bytes = _system.ReadAllBytes(_paths.ProfilePath(profileId));
            var temporaryPath = _paths.ProfileTemporaryPath(
                profileId,
                new OperationId(Guid.NewGuid()));
            var repairPath = _paths.ProfileRepairTemporaryPath(
                profileId,
                new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(temporaryPath, bytes);
            _system.CreateFileDurably(repairPath, bytes);

            var loaded = repository.Load(profileId);

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(loaded.Profile.VersionToken, Is.EqualTo(created.Profile.VersionToken));
            Assert.That(_system.FileExists(temporaryPath), Is.False);
            Assert.That(_system.FileExists(repairPath), Is.False);
        }

        [Test]
        public void ProfileRepository_LoadCleansOnlyOrphanTemporaryFilesAndUnblocksCreate()
        {
            var repository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var mainPath = _paths.ProfilePath(profileId);
            var bytes = _system.ReadAllBytes(mainPath);
            _system.DeleteFile(mainPath);
            var temporaryPath = _paths.ProfileTemporaryPath(
                profileId,
                new OperationId(Guid.NewGuid()));
            var repairPath = _paths.ProfileRepairTemporaryPath(
                profileId,
                new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(temporaryPath, bytes);
            _system.CreateFileDurably(repairPath, bytes);

            var loaded = repository.Load(profileId);
            var created = repository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.NotFound));
            Assert.That(_system.FileExists(temporaryPath), Is.False);
            Assert.That(_system.FileExists(repairPath), Is.False);
            Assert.That(created.Kind, Is.EqualTo(PersistenceResultKind.Created));
        }

        [Test]
        public void CatalogRepository_PartialReplaceRestoresExpectedAndCleansOperationFiles()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            profileRepository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var faults = new FaultInjectingAtomicFileSystem(_system);
            var repository = new LocalProfileCatalogRepository(_paths, faults);
            var current = repository.LoadOrRebuild(new OperationId(Guid.NewGuid()));
            var next = LocalProfileJsonCodec.SealCatalog(new LocalProfileCatalog(
                LocalProfileCatalog.CurrentSchemaVersion,
                current.Catalog.CatalogRevision + 1,
                CareerPersistenceTestData.Hash('0'),
                current.Catalog.Profiles));
            var operationId = new OperationId(Guid.NewGuid());
            faults.Mode = FaultMode.MoveMainToOperationBackupThenThrow;

            var result = repository.Commit(
                current.Catalog.VersionToken,
                next,
                operationId);

            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.NotCommitted));
            Assert.That(
                _system.FileExists(_paths.ProfilesIndexReplaceBackupPath(operationId)),
                Is.False);
            Assert.That(
                _system.FileExists(_paths.ProfilesIndexTemporaryPath(operationId)),
                Is.False);
            faults.Mode = FaultMode.None;
            Assert.That(
                repository.LoadOrRebuild(new OperationId(Guid.NewGuid())).Catalog.VersionToken,
                Is.EqualTo(current.Catalog.VersionToken));
        }

        [Test]
        public void CatalogRepository_DuplicateInterruptedBackupOfMainIsCleanedDeterministically()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            profileRepository.Create(
                CareerPersistenceTestData.Profile(new ProfileId(Guid.NewGuid())),
                new OperationId(Guid.NewGuid()));
            var repository = new LocalProfileCatalogRepository(_paths, _system);
            var current = repository.LoadOrRebuild(new OperationId(Guid.NewGuid()));
            var operationId = new OperationId(Guid.NewGuid());
            var operationBackupPath = _paths.ProfilesIndexReplaceBackupPath(operationId);
            var temporaryPath = _paths.ProfilesIndexTemporaryPath(operationId);
            _system.CopyFileWhenDestinationDoesNotExist(
                _paths.ProfilesIndexPath,
                operationBackupPath);
            _system.CreateFileDurably(
                temporaryPath,
                _system.ReadAllBytes(_paths.ProfilesIndexPath));

            var loaded = repository.LoadOrRebuild(new OperationId(Guid.NewGuid()));

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(loaded.Catalog.VersionToken, Is.EqualTo(current.Catalog.VersionToken));
            Assert.That(_system.FileExists(operationBackupPath), Is.False);
            Assert.That(_system.FileExists(temporaryPath), Is.False);
        }

        [Test]
        public void CatalogRepository_LoadCleansForeignTemporaryAndRepairFiles()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            profileRepository.Create(
                CareerPersistenceTestData.Profile(new ProfileId(Guid.NewGuid())),
                new OperationId(Guid.NewGuid()));
            var repository = new LocalProfileCatalogRepository(_paths, _system);
            var current = repository.LoadOrRebuild(new OperationId(Guid.NewGuid()));
            var bytes = _system.ReadAllBytes(_paths.ProfilesIndexPath);
            var temporaryPath = _paths.ProfilesIndexTemporaryPath(new OperationId(Guid.NewGuid()));
            var repairPath = _paths.ProfilesIndexRepairTemporaryPath(new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(temporaryPath, bytes);
            _system.CreateFileDurably(repairPath, bytes);

            var loaded = repository.LoadOrRebuild(new OperationId(Guid.NewGuid()));

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.Loaded));
            Assert.That(loaded.Catalog.VersionToken, Is.EqualTo(current.Catalog.VersionToken));
            Assert.That(_system.FileExists(temporaryPath), Is.False);
            Assert.That(_system.FileExists(repairPath), Is.False);
        }

        [Test]
        public void CatalogRepository_CommitCleansForeignTemporaryAndRepairFiles()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            profileRepository.Create(
                CareerPersistenceTestData.Profile(new ProfileId(Guid.NewGuid())),
                new OperationId(Guid.NewGuid()));
            var repository = new LocalProfileCatalogRepository(_paths, _system);
            var current = repository.LoadOrRebuild(new OperationId(Guid.NewGuid()));
            var bytes = _system.ReadAllBytes(_paths.ProfilesIndexPath);
            var temporaryPath = _paths.ProfilesIndexTemporaryPath(new OperationId(Guid.NewGuid()));
            var repairPath = _paths.ProfilesIndexRepairTemporaryPath(new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(temporaryPath, bytes);
            _system.CreateFileDurably(repairPath, bytes);
            var next = LocalProfileJsonCodec.SealCatalog(new LocalProfileCatalog(
                LocalProfileCatalog.CurrentSchemaVersion,
                current.Catalog.CatalogRevision + 1,
                CareerPersistenceTestData.Hash('0'),
                current.Catalog.Profiles));

            var committed = repository.Commit(
                current.Catalog.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));

            Assert.That(committed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(_system.FileExists(temporaryPath), Is.False);
            Assert.That(_system.FileExists(repairPath), Is.False);
        }

        [Test]
        public void CatalogRepository_CommitDoesNotBypassForeignReplaceBackup()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            profileRepository.Create(
                CareerPersistenceTestData.Profile(new ProfileId(Guid.NewGuid())),
                new OperationId(Guid.NewGuid()));
            var repository = new LocalProfileCatalogRepository(_paths, _system);
            var current = repository.LoadOrRebuild(new OperationId(Guid.NewGuid()));
            var mainBytes = _system.ReadAllBytes(_paths.ProfilesIndexPath);
            var unrelated = LocalProfileJsonCodec.SealCatalog(new LocalProfileCatalog(
                LocalProfileCatalog.CurrentSchemaVersion,
                current.Catalog.CatalogRevision + 5,
                CareerPersistenceTestData.Hash('0'),
                current.Catalog.Profiles));
            var foreignBackupPath = _paths.ProfilesIndexReplaceBackupPath(
                new OperationId(Guid.NewGuid()));
            _system.CreateFileDurably(
                foreignBackupPath,
                LocalProfileJsonCodec.SerializeCatalog(unrelated));
            var next = LocalProfileJsonCodec.SealCatalog(new LocalProfileCatalog(
                LocalProfileCatalog.CurrentSchemaVersion,
                current.Catalog.CatalogRevision + 1,
                CareerPersistenceTestData.Hash('0'),
                current.Catalog.Profiles));

            var committed = repository.Commit(
                current.Catalog.VersionToken,
                next,
                new OperationId(Guid.NewGuid()));

            Assert.That(committed.Kind, Is.EqualTo(PersistenceResultKind.AmbiguousReplaceState));
            Assert.That(_system.ReadAllBytes(_paths.ProfilesIndexPath), Is.EqualTo(mainBytes));
            Assert.That(_system.FileExists(foreignBackupPath), Is.True);
        }

        [Test]
        public void ProfileIndex_RebuildMarksMissingEntryWithoutCreatingReplacementCareer()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            var careerRepository = new LocalCareerSaveRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            profileRepository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var career = CreateCareer(careerRepository, profileId, "Career");
            var indexed = profileRepository.RebuildCareerIndex(
                profileId,
                new OperationId(Guid.NewGuid()));
            Assert.That(indexed.Profile.CareerEntries, Has.Count.EqualTo(1));
            File.Delete(_paths.CareerPath(profileId, career.Identity.SaveId));

            var rebuilt = profileRepository.RebuildCareerIndex(
                profileId,
                new OperationId(Guid.NewGuid()));

            Assert.That(rebuilt.Profile.CareerEntries, Has.Count.EqualTo(1));
            Assert.That(
                rebuilt.Profile.CareerEntries[0].Loadability,
                Is.EqualTo(CareerLoadability.Missing));
            Assert.That(
                _system.FileExists(_paths.CareerPath(profileId, career.Identity.SaveId)),
                Is.False);
        }

        [Test]
        public void ProfileIndex_RebuildPreservesAndRejectsAnUnsupportedCareerAuthority()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            var careerRepository = new LocalCareerSaveRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            profileRepository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var career = CreateCareer(careerRepository, profileId, "Unsupported Career");
            var indexed = profileRepository.RebuildCareerIndex(
                profileId,
                new OperationId(Guid.NewGuid()));
            var profileBytes = _system.ReadAllBytes(_paths.ProfilePath(profileId));
            var careerPath = _paths.CareerPath(profileId, career.Identity.SaveId);
            var unsupportedBytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(_system.ReadAllBytes(careerPath))
                    .Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
            _system.OverwriteFileDurably(careerPath, unsupportedBytes);

            var rebuilt = profileRepository.RebuildCareerIndex(
                profileId,
                new OperationId(Guid.NewGuid()));

            Assert.That(indexed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(rebuilt.Kind, Is.EqualTo(PersistenceResultKind.UnsupportedVersion));
            Assert.That(_system.ReadAllBytes(careerPath), Is.EqualTo(unsupportedBytes));
            Assert.That(_system.ReadAllBytes(_paths.ProfilePath(profileId)), Is.EqualTo(profileBytes));
        }

        [Test]
        public void CareerRepository_LoadPreservesAndRejectsAnUnknownCareerVersionAxis()
        {
            var repository = new LocalCareerSaveRepository(_paths, _system);
            var career = CreateCareer(repository);
            var careerPath = _paths.CareerPath(
                career.Identity.ProfileId,
                career.Identity.SaveId);
            var unsupportedBytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(_system.ReadAllBytes(careerPath))
                    .Replace(
                        "\"careerRandomAlgorithmVersion\":1}",
                        "\"careerRandomAlgorithmVersion\":1,\"matchSimulationVersion\":1}"));
            _system.OverwriteFileDurably(careerPath, unsupportedBytes);

            var loaded = repository.Load(
                career.Identity.ProfileId,
                career.Identity.SaveId);

            Assert.That(loaded.Kind, Is.EqualTo(PersistenceResultKind.UnsupportedVersion));
            Assert.That(_system.ReadAllBytes(careerPath), Is.EqualTo(unsupportedBytes));
        }

        [Test]
        public void ProfileIndex_RebuildPreservesAndRejectsAnUnknownCareerVersionAxis()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            var careerRepository = new LocalCareerSaveRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            profileRepository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            var career = CreateCareer(careerRepository, profileId, "Unknown Axis Career");
            var indexed = profileRepository.RebuildCareerIndex(
                profileId,
                new OperationId(Guid.NewGuid()));
            var profileBytes = _system.ReadAllBytes(_paths.ProfilePath(profileId));
            var careerPath = _paths.CareerPath(profileId, career.Identity.SaveId);
            var unsupportedBytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(_system.ReadAllBytes(careerPath))
                    .Replace(
                        "\"careerRandomAlgorithmVersion\":1}",
                        "\"careerRandomAlgorithmVersion\":1,\"matchSimulationVersion\":1}"));
            _system.OverwriteFileDurably(careerPath, unsupportedBytes);

            var rebuilt = profileRepository.RebuildCareerIndex(
                profileId,
                new OperationId(Guid.NewGuid()));

            Assert.That(indexed.Kind, Is.EqualTo(PersistenceResultKind.Committed));
            Assert.That(rebuilt.Kind, Is.EqualTo(PersistenceResultKind.UnsupportedVersion));
            Assert.That(_system.ReadAllBytes(careerPath), Is.EqualTo(unsupportedBytes));
            Assert.That(_system.ReadAllBytes(_paths.ProfilePath(profileId)), Is.EqualTo(profileBytes));
        }

        [Test]
        public void CatalogRebuild_PreservesAndRejectsAnUnsupportedProfileAuthority()
        {
            var profileRepository = new LocalPlayerProfileRepository(_paths, _system);
            var catalogRepository = new LocalProfileCatalogRepository(_paths, _system);
            var profileId = new ProfileId(Guid.NewGuid());
            profileRepository.Create(
                CareerPersistenceTestData.Profile(profileId),
                new OperationId(Guid.NewGuid()));
            catalogRepository.LoadOrRebuild(new OperationId(Guid.NewGuid()));
            var catalogBytes = _system.ReadAllBytes(_paths.ProfilesIndexPath);
            var profilePath = _paths.ProfilePath(profileId);
            var unsupportedBytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(_system.ReadAllBytes(profilePath))
                    .Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
            _system.OverwriteFileDurably(profilePath, unsupportedBytes);

            var rebuilt = catalogRepository.LoadOrRebuild(new OperationId(Guid.NewGuid()));

            Assert.That(rebuilt.Kind, Is.EqualTo(PersistenceResultKind.UnsupportedVersion));
            Assert.That(_system.ReadAllBytes(profilePath), Is.EqualTo(unsupportedBytes));
            Assert.That(_system.ReadAllBytes(_paths.ProfilesIndexPath), Is.EqualTo(catalogBytes));
        }

        private CareerSaveSnapshot CreateCareer(LocalCareerSaveRepository repository)
        {
            return CreateCareer(repository, new ProfileId(Guid.NewGuid()), "Career");
        }

        private CareerSaveSnapshot CreateCareer(
            LocalCareerSaveRepository repository,
            ProfileId profileId,
            string careerName)
        {
            var initial = CareerPersistenceTestData.CreatedSnapshot(
                profileId,
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()),
                careerName);
            var result = repository.Create(initial, new OperationId(Guid.NewGuid()));
            Assert.That(result.Kind, Is.EqualTo(PersistenceResultKind.Created));
            return result.Snapshot;
        }

        private void CreateTwoCareers(
            LocalCareerSaveRepository repository,
            ProfileId profileId,
            string prefix)
        {
            CreateCareer(repository, profileId, prefix + " One");
            CreateCareer(repository, profileId, prefix + " Two");
        }

        private static LocalPlayerProfile UpdatedProfile(
            LocalPlayerProfile current,
            string displayName)
        {
            return new LocalPlayerProfile(
                LocalPlayerProfile.CurrentSchemaVersion,
                current.ProfileId,
                current.ProfileRevision + 1,
                CareerPersistenceTestData.Hash('0'),
                displayName,
                current.CreatedAtUtcMs,
                current.UpdatedAtUtcMs + 1,
                current.CareerEntries);
        }

        private static Sha256Digest RawHash(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
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

        private enum FaultMode
        {
            None,
            ThrowOnLock,
            ThrowBeforeDurableCreate,
            ThrowAfterMove,
            ThrowBeforeReplace,
            ThrowAfterReplace,
            MoveMainToOperationBackupThenThrow,
            ThrowMovingOperationBackupToFixedBackup
        }

        private sealed class FaultInjectingAtomicFileSystem : IAtomicFileSystem
        {
            private readonly IAtomicFileSystem _inner;

            public FaultInjectingAtomicFileSystem(IAtomicFileSystem inner)
            {
                _inner = inner;
            }

            public FaultMode Mode { get; set; }

            public void CreateDirectory(string directoryPath) =>
                _inner.CreateDirectory(directoryPath);

            public bool DirectoryExists(string directoryPath) =>
                _inner.DirectoryExists(directoryPath);

            public bool FileExists(string filePath) => _inner.FileExists(filePath);

            public byte[] ReadAllBytes(string filePath) => _inner.ReadAllBytes(filePath);

            public void CreateFileDurably(string filePath, byte[] contents) =>
                CreateFileDurablyWithFault(filePath, contents);

            private void CreateFileDurablyWithFault(string filePath, byte[] contents)
            {
                if (Mode == FaultMode.ThrowBeforeDurableCreate)
                {
                    throw new IOException("Injected durable-create failure.");
                }

                _inner.CreateFileDurably(filePath, contents);
            }

            public void OverwriteFileDurably(string filePath, byte[] contents) =>
                _inner.OverwriteFileDurably(filePath, contents);

            public void MoveFileAtomicallyWhenDestinationDoesNotExist(
                string sourceFilePath,
                string destinationFilePath)
            {
                if (Mode == FaultMode.ThrowMovingOperationBackupToFixedBackup &&
                    sourceFilePath.Contains(".replace-backup.") &&
                    destinationFilePath.EndsWith(".bak", StringComparison.Ordinal))
                {
                    throw new IOException("Injected backup convergence failure.");
                }

                _inner.MoveFileAtomicallyWhenDestinationDoesNotExist(
                    sourceFilePath,
                    destinationFilePath);
                if (Mode == FaultMode.ThrowAfterMove)
                {
                    throw new IOException("Injected lost move success response.");
                }
            }

            public void ReplaceFileWithOperationBackup(
                string replacementFilePath,
                string destinationFilePath,
                string operationBackupFilePath)
            {
                if (Mode == FaultMode.ThrowBeforeReplace)
                {
                    throw new IOException("Injected pre-replace failure.");
                }

                if (Mode == FaultMode.MoveMainToOperationBackupThenThrow)
                {
                    _inner.MoveFileAtomicallyWhenDestinationDoesNotExist(
                        destinationFilePath,
                        operationBackupFilePath);
                    throw new IOException("Injected partial ReplaceFileW state.");
                }

                _inner.ReplaceFileWithOperationBackup(
                    replacementFilePath,
                    destinationFilePath,
                    operationBackupFilePath);
                if (Mode == FaultMode.ThrowAfterReplace)
                {
                    throw new IOException("Injected lost success response.");
                }
            }

            public void CopyFileWhenDestinationDoesNotExist(
                string sourceFilePath,
                string destinationFilePath) =>
                _inner.CopyFileWhenDestinationDoesNotExist(
                    sourceFilePath,
                    destinationFilePath);

            public IReadOnlyList<string> EnumerateFiles(string directoryPath) =>
                _inner.EnumerateFiles(directoryPath);

            public IReadOnlyList<string> EnumerateDirectories(string directoryPath) =>
                _inner.EnumerateDirectories(directoryPath);

            public void DeleteFile(string filePath) => _inner.DeleteFile(filePath);

            public IAtomicFileLock AcquireExclusiveLock(string lockFilePath)
            {
                if (Mode == FaultMode.ThrowOnLock)
                {
                    throw new IOException("Injected lock contention.");
                }

                return _inner.AcquireExclusiveLock(lockFilePath);
            }
        }
    }
}
