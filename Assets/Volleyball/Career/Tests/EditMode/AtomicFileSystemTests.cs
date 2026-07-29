using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;

namespace Volleyball.Career.EditModeTests
{
    public sealed class AtomicFileSystemTests
    {
        private string _temporaryRoot;
        private SystemAtomicFileSystem _fileSystem;

        [SetUp]
        public void SetUp()
        {
            _temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "volleyball-career-files-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryRoot);
            _fileSystem = new SystemAtomicFileSystem();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, true);
            }
        }

        [Test]
        public void StoragePaths_UseTheFixedProfileAndCareerLayout()
        {
            var profileId = new ProfileId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var saveId = new SaveId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
            var operationId = new OperationId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
            var paths = new CareerStoragePaths(_temporaryRoot);
            var profileDirectory = Path.Combine(
                Path.GetFullPath(_temporaryRoot),
                "Profiles",
                profileId.ToString());
            var careersDirectory = Path.Combine(profileDirectory, "Careers");

            Assert.That(paths.ProfilesIndexPath,
                Is.EqualTo(Path.Combine(Path.GetFullPath(_temporaryRoot), "Profiles", "profiles-index.json")));
            Assert.That(paths.ProfilesIndexTemporaryPath(operationId),
                Is.EqualTo(Path.Combine(
                    Path.GetFullPath(_temporaryRoot),
                    "Profiles",
                    "profiles-index.tmp." + operationId)));
            Assert.That(paths.ProfilesIndexReplaceBackupPath(operationId),
                Is.EqualTo(Path.Combine(
                    Path.GetFullPath(_temporaryRoot),
                    "Profiles",
                    "profiles-index.replace-backup." + operationId)));
            Assert.That(paths.ProfilesIndexRepairTemporaryPath(operationId),
                Is.EqualTo(Path.Combine(
                    Path.GetFullPath(_temporaryRoot),
                    "Profiles",
                    "profiles-index.repair.tmp." + operationId)));
            Assert.That(paths.ProfilePath(profileId),
                Is.EqualTo(Path.Combine(profileDirectory, "profile.json")));
            Assert.That(paths.ProfileBackupPath(profileId),
                Is.EqualTo(Path.Combine(profileDirectory, "profile.bak")));
            Assert.That(paths.ProfileTemporaryPath(profileId, operationId),
                Is.EqualTo(Path.Combine(profileDirectory, "profile.tmp." + operationId)));
            Assert.That(paths.ProfileReplaceBackupPath(profileId, operationId),
                Is.EqualTo(Path.Combine(profileDirectory, "profile.replace-backup." + operationId)));
            Assert.That(paths.ProfileBackupConvergencePath(profileId, operationId),
                Is.EqualTo(Path.Combine(profileDirectory, "profile.backup-convergence." + operationId)));
            Assert.That(paths.ProfileRepairTemporaryPath(profileId, operationId),
                Is.EqualTo(Path.Combine(profileDirectory, "profile.repair.tmp." + operationId)));
            Assert.That(paths.CareerPath(profileId, saveId),
                Is.EqualTo(Path.Combine(careersDirectory, saveId + ".json")));
            Assert.That(paths.CareerBackupPath(profileId, saveId),
                Is.EqualTo(Path.Combine(careersDirectory, saveId + ".bak")));
            Assert.That(paths.CareerTemporaryPath(profileId, saveId, operationId),
                Is.EqualTo(Path.Combine(careersDirectory, saveId + ".tmp." + operationId)));
            Assert.That(paths.CareerReplaceBackupPath(profileId, saveId, operationId),
                Is.EqualTo(Path.Combine(careersDirectory, saveId + ".replace-backup." + operationId)));
            Assert.That(paths.CareerBackupConvergencePath(profileId, saveId, operationId),
                Is.EqualTo(Path.Combine(careersDirectory, saveId + ".backup-convergence." + operationId)));
            Assert.That(paths.CareerRepairTemporaryPath(profileId, saveId, operationId),
                Is.EqualTo(Path.Combine(careersDirectory, saveId + ".repair.tmp." + operationId)));
            Assert.That(paths.CareerRecoveryTemporaryPath(profileId, saveId, operationId),
                Is.EqualTo(Path.Combine(careersDirectory, saveId + ".recovery.tmp." + operationId)));
            Assert.That(paths.CareerRecoveryIntentPath(profileId, saveId, operationId),
                Is.EqualTo(Path.Combine(careersDirectory, saveId + ".recovery-intent." + operationId + ".json")));
            Assert.That(paths.CareerQuarantinePath(
                    profileId,
                    saveId,
                    CareerQuarantineSourceKind.Corrupt,
                    operationId,
                    4),
                Is.EqualTo(Path.Combine(
                    careersDirectory,
                    "Quarantine",
                    saveId + ".corrupt." + operationId + ".4.json")));
        }

        [Test]
        public void StoragePaths_KeepTemporaryAndReplaceBackupBesideTheirTargets()
        {
            var paths = new CareerStoragePaths(_temporaryRoot);
            var profileId = NewProfileId();
            var saveId = NewSaveId();
            var operationId = NewOperationId();

            Assert.That(
                Path.GetDirectoryName(paths.ProfileTemporaryPath(profileId, operationId)),
                Is.EqualTo(Path.GetDirectoryName(paths.ProfilePath(profileId))));
            Assert.That(
                Path.GetDirectoryName(paths.ProfileReplaceBackupPath(profileId, operationId)),
                Is.EqualTo(Path.GetDirectoryName(paths.ProfilePath(profileId))));
            Assert.That(
                Path.GetDirectoryName(paths.CareerTemporaryPath(profileId, saveId, operationId)),
                Is.EqualTo(Path.GetDirectoryName(paths.CareerPath(profileId, saveId))));
            Assert.That(
                Path.GetDirectoryName(paths.CareerReplaceBackupPath(profileId, saveId, operationId)),
                Is.EqualTo(Path.GetDirectoryName(paths.CareerPath(profileId, saveId))));
        }

        [Test]
        public void StoragePaths_RejectRelativeRootsDefaultIdsAndInvalidQuarantineValues()
        {
            var paths = new CareerStoragePaths(_temporaryRoot);
            var profileId = NewProfileId();
            var saveId = NewSaveId();
            var operationId = NewOperationId();

            Assert.That(() => new CareerStoragePaths("relative"), Throws.ArgumentException);
            Assert.That(() => paths.ProfilePath(default), Throws.ArgumentException);
            Assert.That(() => paths.CareerPath(profileId, default), Throws.ArgumentException);
            Assert.That(
                () => paths.CareerTemporaryPath(profileId, saveId, default),
                Throws.ArgumentException);
            Assert.That(
                () => paths.CareerQuarantinePath(profileId, saveId, 0, operationId, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => paths.CareerQuarantinePath(
                    profileId,
                    saveId,
                    CareerQuarantineSourceKind.Main,
                    operationId,
                    -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void DurableCreateAndOverwrite_RoundTripExactBytes()
        {
            var filePath = Path.Combine(_temporaryRoot, "durable.bin");
            var initial = Encoding.UTF8.GetBytes("first");
            var updated = Encoding.UTF8.GetBytes("second");

            _fileSystem.CreateFileDurably(filePath, initial);
            Assert.That(_fileSystem.ReadAllBytes(filePath), Is.EqualTo(initial));
            Assert.That(
                () => _fileSystem.CreateFileDurably(filePath, updated),
                Throws.TypeOf<IOException>());

            _fileSystem.OverwriteFileDurably(filePath, updated);
            Assert.That(_fileSystem.ReadAllBytes(filePath), Is.EqualTo(updated));
        }

        [Test]
        public void AtomicMove_DoesNotOverwriteAnExistingDestination()
        {
            var source = Path.Combine(_temporaryRoot, "source.bin");
            var destination = Path.Combine(_temporaryRoot, "destination.bin");
            var sourceBytes = Encoding.UTF8.GetBytes("source");
            var destinationBytes = Encoding.UTF8.GetBytes("destination");
            _fileSystem.CreateFileDurably(source, sourceBytes);
            _fileSystem.CreateFileDurably(destination, destinationBytes);

            Assert.That(
                () => _fileSystem.MoveFileAtomicallyWhenDestinationDoesNotExist(source, destination),
                Throws.TypeOf<IOException>());
            Assert.That(_fileSystem.ReadAllBytes(source), Is.EqualTo(sourceBytes));
            Assert.That(_fileSystem.ReadAllBytes(destination), Is.EqualTo(destinationBytes));
        }

        [Test]
        public void ReplaceFile_PublishesReplacementAndCreatesOperationBackup()
        {
            var main = Path.Combine(_temporaryRoot, "career.json");
            var replacement = Path.Combine(_temporaryRoot, "career.tmp.operation");
            var operationBackup = Path.Combine(_temporaryRoot, "career.replace-backup.operation");
            var oldBytes = Encoding.UTF8.GetBytes("old");
            var newBytes = Encoding.UTF8.GetBytes("new");
            _fileSystem.CreateFileDurably(main, oldBytes);
            _fileSystem.CreateFileDurably(replacement, newBytes);

            _fileSystem.ReplaceFileWithOperationBackup(replacement, main, operationBackup);

            Assert.That(_fileSystem.ReadAllBytes(main), Is.EqualTo(newBytes));
            Assert.That(_fileSystem.ReadAllBytes(operationBackup), Is.EqualTo(oldBytes));
            Assert.That(_fileSystem.FileExists(replacement), Is.False);
        }

        [Test]
        public void CopyEnumerateAndDelete_PreserveExplicitQuarantineEvidence()
        {
            var quarantine = Path.Combine(_temporaryRoot, "Quarantine");
            var source = Path.Combine(_temporaryRoot, "source.json");
            var evidence = Path.Combine(quarantine, "evidence.json");
            _fileSystem.CreateDirectory(quarantine);
            _fileSystem.CreateFileDurably(source, Encoding.UTF8.GetBytes("evidence"));

            _fileSystem.CopyFileWhenDestinationDoesNotExist(source, evidence);

            Assert.That(_fileSystem.EnumerateFiles(quarantine), Is.EqualTo(new[] { evidence }));
            Assert.That(_fileSystem.FileExists(source), Is.True);
            _fileSystem.DeleteFile(evidence);
            Assert.That(_fileSystem.FileExists(evidence), Is.False);
        }

        [Test]
        public void EnumerateDirectories_ReturnsOnlyImmediateDirectoriesInOrdinalOrder()
        {
            var profiles = Path.Combine(_temporaryRoot, "Profiles");
            var first = Path.Combine(profiles, "11111111-1111-1111-1111-111111111111");
            var second = Path.Combine(profiles, "22222222-2222-2222-2222-222222222222");
            var nested = Path.Combine(first, "Careers");
            _fileSystem.CreateDirectory(nested);
            _fileSystem.CreateDirectory(second);
            _fileSystem.CreateFileDurably(
                Path.Combine(profiles, "profiles-index.json"),
                Encoding.UTF8.GetBytes("index"));

            Assert.That(
                _fileSystem.EnumerateDirectories(profiles),
                Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void ExclusiveLock_RejectsCompetitionUntilTheHandleIsReleased()
        {
            var lockPath = Path.Combine(_temporaryRoot, "career.lock");
            var first = _fileSystem.AcquireExclusiveLock(lockPath);
            try
            {
                Assert.That(
                    () => _fileSystem.AcquireExclusiveLock(lockPath),
                    Throws.TypeOf<IOException>());
            }
            finally
            {
                first.Dispose();
            }

            using (var acquiredAfterRelease = _fileSystem.AcquireExclusiveLock(lockPath))
            {
                Assert.That(acquiredAfterRelease.LockFilePath, Is.EqualTo(lockPath));
            }
        }

        private static ProfileId NewProfileId()
        {
            return new ProfileId(Guid.NewGuid());
        }

        private static SaveId NewSaveId()
        {
            return new SaveId(Guid.NewGuid());
        }

        private static OperationId NewOperationId()
        {
            return new OperationId(Guid.NewGuid());
        }
    }
}
