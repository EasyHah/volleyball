using System;
using System.IO;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerLocalUiWorkflowTests
    {
        private string _root;
        private CareerStoragePaths _paths;
        private SystemAtomicFileSystem _fileSystem;
        private LocalCareerSaveRepository _careerRepository;
        private CareerLocalUiWorkflow _workflow;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "volleyball-career-ui-workflow",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _paths = new CareerStoragePaths(_root);
            _fileSystem = new SystemAtomicFileSystem();
            _careerRepository = new LocalCareerSaveRepository(_paths, _fileSystem);
            _workflow = new CareerLocalUiWorkflow(
                new LocalPlayerProfileRepository(_paths, _fileSystem),
                new LocalProfileCatalogRepository(_paths, _fileSystem),
                _careerRepository);
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
        public void CreateProfile_RefreshIndex_AndLoadCareerShareTheLocalWorkflowBoundary()
        {
            var profileId = new ProfileId(Guid.NewGuid());
            var saveId = new SaveId(Guid.NewGuid());
            var create = _workflow.CreateProfile(new CreateLocalProfileUiCommand(
                Envelope(1, 100),
                profileId,
                "本地球员"));

            Assert.That(create.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.Completed));
            Assert.That(create.Profile.ProfileId, Is.EqualTo(profileId));
            Assert.That(create.Catalog.Profiles, Has.Count.EqualTo(1));
            Assert.That(create.Catalog.Profiles[0].ProfileId, Is.EqualTo(profileId));

            var initialCareer = CareerPersistenceTestData.PlanningSnapshot(
                profileId,
                saveId,
                new LineageId(Guid.NewGuid()));
            Publish(initialCareer);

            var refreshed = _workflow.RefreshCareerIndex(
                new LocalProfileUiCommand(Envelope(2, 101), profileId));
            var loadedProfile = _workflow.LoadProfile(
                new LocalProfileUiCommand(Envelope(3, 102), profileId));
            var loadedCareer = _workflow.LoadCareer(
                new LocalCareerUiCommand(Envelope(4, 103), profileId, saveId));

            Assert.That(refreshed.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.Completed));
            Assert.That(refreshed.Profile.CareerEntries, Has.Count.EqualTo(1));
            Assert.That(loadedProfile.Profile.CareerEntries[0].SaveId, Is.EqualTo(saveId));
            Assert.That(loadedCareer.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.Completed));
            Assert.That(loadedCareer.Snapshot.Identity.SaveId, Is.EqualTo(saveId));
        }

        [Test]
        public void CreateProfile_RetryRecoversOnlyTheExactAlreadyCreatedProfile()
        {
            var profileId = new ProfileId(Guid.NewGuid());
            var command = new CreateLocalProfileUiCommand(
                Envelope(7, 200),
                profileId,
                "可恢复档案");
            var first = _workflow.CreateProfile(command);
            var retry = _workflow.CreateProfile(command);
            var conflicting = _workflow.CreateProfile(new CreateLocalProfileUiCommand(
                Envelope(7, 200),
                profileId,
                "不同档案"));

            Assert.That(first.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.Completed));
            Assert.That(retry.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.Completed));
            Assert.That(retry.PrimaryPersistenceKind, Is.EqualTo(PersistenceResultKind.AlreadyExists));
            Assert.That(retry.Profile.DisplayName, Is.EqualTo("可恢复档案"));
            Assert.That(retry.Catalog.Profiles, Has.Count.EqualTo(1));
            Assert.That(conflicting.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.PersistenceFailure));
            Assert.That(conflicting.Catalog, Is.Null);
        }

        [Test]
        public void UiCommands_RejectDefaultIdentifiersAtConstruction()
        {
            var envelope = Envelope(8, 300);
            var profileId = new ProfileId(Guid.NewGuid());
            var saveId = new SaveId(Guid.NewGuid());

            Assert.Throws<ArgumentException>(() =>
                new CareerUiCommandEnvelope(default, 300));
            Assert.Throws<ArgumentException>(() =>
                new CreateLocalProfileUiCommand(envelope, default, "球员"));
            Assert.Throws<ArgumentException>(() =>
                new LocalProfileUiCommand(envelope, default));
            Assert.Throws<ArgumentException>(() =>
                new LocalCareerUiCommand(envelope, default, saveId));
            Assert.Throws<ArgumentException>(() =>
                new LocalCareerUiCommand(envelope, profileId, default));
        }

        [Test]
        public void SaveNow_IsUpToDateOnlyForPlanningAndNeverCreatesAnotherRevision()
        {
            var profileId = new ProfileId(Guid.NewGuid());
            var planningSaveId = new SaveId(Guid.NewGuid());
            var tryoutSaveId = new SaveId(Guid.NewGuid());
            var planning = CareerPersistenceTestData.PlanningSnapshot(
                profileId,
                planningSaveId,
                new LineageId(Guid.NewGuid()));
            var tryout = CareerPersistenceTestData.CreatedSnapshot(
                profileId,
                tryoutSaveId,
                new LineageId(Guid.NewGuid()));
            Publish(planning);
            Publish(tryout);
            var planningPath = _paths.CareerPath(profileId, planningSaveId);
            var tryoutPath = _paths.CareerPath(profileId, tryoutSaveId);
            var planningBytesBefore = _fileSystem.ReadAllBytes(planningPath);
            var tryoutBytesBefore = _fileSystem.ReadAllBytes(tryoutPath);
            var planningRevisionBefore = _careerRepository
                .Load(profileId, planningSaveId).Snapshot.Identity.Revision;
            var tryoutRevisionBefore = _careerRepository
                .Load(profileId, tryoutSaveId).Snapshot.Identity.Revision;

            var planningResult = _workflow.SaveNow(
                new LocalCareerUiCommand(Envelope(5, 104), profileId, planningSaveId));
            var unsafeResult = _workflow.SaveNow(
                new LocalCareerUiCommand(Envelope(6, 105), profileId, tryoutSaveId));

            Assert.That(planningResult.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.UpToDate));
            Assert.That(unsafeResult.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.UnsafeState));
            Assert.That(
                planningResult.Snapshot.Identity.Revision,
                Is.EqualTo(planningRevisionBefore));
            Assert.That(
                unsafeResult.Snapshot.Identity.Revision,
                Is.EqualTo(tryoutRevisionBefore));
            Assert.That(_fileSystem.ReadAllBytes(planningPath), Is.EqualTo(planningBytesBefore));
            Assert.That(_fileSystem.ReadAllBytes(tryoutPath), Is.EqualTo(tryoutBytesBefore));
        }

        [Test]
        public void RecoverCareer_UsesExactInspectedBackupAndCorruptMainEvidence()
        {
            var profileId = new ProfileId(Guid.NewGuid());
            var saveId = new SaveId(Guid.NewGuid());
            var originalLineage = new LineageId(Guid.NewGuid());
            var recoveryLineage = new LineageId(Guid.NewGuid());
            _workflow.CreateProfile(new CreateLocalProfileUiCommand(
                Envelope(9, 400),
                profileId,
                "恢复测试"));
            var backup = CareerSaveJsonCodec.Seal(
                CareerPersistenceTestData.PlanningSnapshot(
                    profileId,
                    saveId,
                    originalLineage));
            _fileSystem.CreateDirectory(_paths.CareersDirectory(profileId));
            _fileSystem.CreateFileDurably(
                _paths.CareerBackupPath(profileId, saveId),
                CareerSaveJsonCodec.Serialize(backup));
            _fileSystem.CreateFileDurably(
                _paths.CareerPath(profileId, saveId),
                new byte[] { 0x7b, 0x22, 0x62, 0x72, 0x6f, 0x6b, 0x65, 0x6e });

            var inspected = _careerRepository.Load(profileId, saveId);
            var recovered = _workflow.RecoverCareer(new RecoverLocalCareerUiCommand(
                Envelope(10, 500),
                profileId,
                saveId,
                recoveryLineage));

            Assert.That(inspected.Kind, Is.EqualTo(PersistenceResultKind.RecoveryAvailable));
            Assert.That(inspected.RecoverableBackup, Is.EqualTo(backup.Identity.VersionToken));
            Assert.That(inspected.UnreadableMainFingerprint.HasValue, Is.True);
            Assert.That(recovered.Status, Is.EqualTo(CareerLocalUiWorkflowStatus.Completed));
            Assert.That(recovered.Snapshot.Identity.LineageId, Is.EqualTo(recoveryLineage));
            Assert.That(recovered.Snapshot.Identity.RestoredFromVersionToken,
                Is.EqualTo(backup.Identity.VersionToken));
            Assert.That(_careerRepository.Load(profileId, saveId).Snapshot.Identity.LineageId,
                Is.EqualTo(recoveryLineage));
            Assert.That(Directory.GetFiles(
                _paths.CareerQuarantineDirectory(profileId)),
                Has.Length.EqualTo(1));
        }

        private static CareerUiCommandEnvelope Envelope(int suffix, long utcMs)
        {
            return new CareerUiCommandEnvelope(
                new OperationId(new Guid(
                    "10000000-0000-0000-0000-" + suffix.ToString("000000000000"))),
                utcMs);
        }

        private void Publish(CareerSaveSnapshot snapshot)
        {
            var sealedSnapshot = CareerSaveJsonCodec.Seal(snapshot);
            _fileSystem.CreateDirectory(_paths.CareersDirectory(snapshot.Identity.ProfileId));
            _fileSystem.CreateFileDurably(
                _paths.CareerPath(snapshot.Identity.ProfileId, snapshot.Identity.SaveId),
                CareerSaveJsonCodec.Serialize(sealedSnapshot));
        }
    }
}
