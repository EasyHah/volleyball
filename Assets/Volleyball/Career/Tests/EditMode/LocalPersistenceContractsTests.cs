using System;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class LocalPersistenceContractsTests
    {
        [Test]
        public void ProfileAndCatalog_SortStableIdsAndRejectDuplicates()
        {
            var first = Entry(new Guid("10000000-0000-0000-0000-000000000000"));
            var second = Entry(new Guid("20000000-0000-0000-0000-000000000000"));
            var profileId = new ProfileId(Guid.NewGuid());
            var profile = new LocalPlayerProfile(
                1,
                profileId,
                1,
                Hash('a'),
                "Local Player",
                10,
                10,
                new[] { second, first });

            Assert.That(profile.CareerEntries[0].SaveId, Is.EqualTo(first.SaveId));
            Assert.That(profile.VersionToken.Revision, Is.EqualTo(1));
            Assert.That(
                () => new LocalPlayerProfile(
                    1,
                    profileId,
                    1,
                    Hash('a'),
                    "Local Player",
                    10,
                    10,
                    new[] { first, first }),
                Throws.ArgumentException);

            var catalog = new LocalProfileCatalog(
                1,
                1,
                Hash('b'),
                new[]
                {
                    new LocalProfileCatalogEntry(
                        new ProfileId(new Guid("20000000-0000-0000-0000-000000000000")),
                        "Second",
                        20,
                        ProfileLoadability.Loadable),
                    new LocalProfileCatalogEntry(
                        new ProfileId(new Guid("10000000-0000-0000-0000-000000000000")),
                        "First",
                        10,
                        ProfileLoadability.Loadable)
                });

            Assert.That(
                catalog.Profiles[0].ProfileId.ToString(),
                Is.EqualTo("10000000-0000-0000-0000-000000000000"));
        }

        [Test]
        public void IndexEntry_FromSnapshotUsesAuthoritativeIdentityAndProgression()
        {
            var snapshot = CreatedSnapshot();

            var entry = CareerIndexEntry.FromSnapshot(snapshot);

            Assert.That(entry.SaveId, Is.EqualTo(snapshot.Identity.SaveId));
            Assert.That(entry.CareerName, Is.EqualTo("First Career"));
            Assert.That(entry.PlayerDisplayName, Is.EqualTo("Player One"));
            Assert.That(entry.ProgressionKind, Is.EqualTo(CareerProgressionKind.CareerCreated));
            Assert.That(entry.Season, Is.Zero);
            Assert.That(entry.Week, Is.Zero);
            Assert.That(entry.Loadability, Is.EqualTo(CareerLoadability.Loadable));
        }

        [Test]
        public void PersistenceResults_RequirePayloadOnlyForSuccessfulKinds()
        {
            var snapshot = CreatedSnapshot();
            Assert.DoesNotThrow(
                () => new CareerPersistenceResult(
                    PersistenceResultKind.Loaded,
                    snapshot));
            Assert.That(
                () => new CareerPersistenceResult(PersistenceResultKind.Loaded),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerPersistenceResult(
                    PersistenceResultKind.VersionConflict,
                    snapshot),
                Throws.ArgumentException);
            Assert.DoesNotThrow(
                () => new CareerPersistenceResult(
                    PersistenceResultKind.RecoveryAvailable,
                    recoverableBackup: snapshot.Identity.VersionToken));
        }

        [Test]
        public void ProfileText_RejectsUnpairedSurrogates()
        {
            Assert.That(
                () => new LocalProfileCatalogEntry(
                    new ProfileId(Guid.NewGuid()),
                    "broken\ud800",
                    0,
                    ProfileLoadability.Loadable),
                Throws.ArgumentException);
        }

        private static CareerIndexEntry Entry(Guid saveId)
        {
            return new CareerIndexEntry(
                new SaveId(saveId),
                "Career",
                "Player",
                CareerProgressionKind.Planning,
                CareerPhase.University,
                1,
                1,
                10,
                CareerLoadability.Loadable);
        }

        private static CareerSaveSnapshot CreatedSnapshot()
        {
            var profileId = new ProfileId(Guid.NewGuid());
            var saveId = new SaveId(Guid.NewGuid());
            var lineageId = new LineageId(Guid.NewGuid());
            var draft = new CareerPlayerDraft(
                new PlayerId("career-player-1"),
                "Player One",
                12);
            var onboarding = new TryoutOnboardingState(
                new[]
                {
                    UnconfirmedStage(1),
                    UnconfirmedStage(2),
                    UnconfirmedStage(3)
                },
                1,
                false);
            var receipt = new OperationReceipt(
                new OperationId(Guid.NewGuid()),
                OperationKind.CreateCareer,
                OperationReceiptTarget.ForCreateCareer(),
                Hash('c'),
                lineageId,
                1,
                10,
                OperationOutcomeKind.CareerCreated,
                OperationOutcomeSummary.ForCareerCreated());

            return new CareerSaveSnapshot(
                CareerSaveVersions.Current,
                new CareerSaveIdentity(
                    profileId,
                    saveId,
                    lineageId,
                    1,
                    10,
                    10,
                    Hash('d')),
                new CareerSeed(new byte[CareerSeed.ByteLength]),
                "First Career",
                draft,
                onboarding,
                CareerProgressionState.Created(),
                TrainingEmphasisLedger.Empty,
                null,
                null,
                null,
                null,
                null,
                null,
                new[] { receipt });
        }

        private static TryoutStageState UnconfirmedStage(int stageNumber)
        {
            return new TryoutStageState(
                stageNumber,
                new OccurrenceId(Guid.NewGuid()),
                CareerSaveVersions.Current.CareerRandomAlgorithmVersion,
                null,
                Array.Empty<TryoutResolvedOutput>());
        }

        private static Sha256Digest Hash(char value)
        {
            return new Sha256Digest(new string(value, 64));
        }
    }
}
