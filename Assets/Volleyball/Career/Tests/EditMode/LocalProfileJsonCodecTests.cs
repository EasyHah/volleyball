using System;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;

namespace Volleyball.Career.EditModeTests
{
    public sealed class LocalProfileJsonCodecTests
    {
        private const string ProfileGoldenHash =
            "d15cf9bcfe03e32bba4657d5f8348771518501fbbcd9b1d3086bfcc2d3cd617e";
        private const string ProfileGoldenBase64 =
            "eyJzY2hlbWFWZXJzaW9uIjoxLCJwcm9maWxlSWQiOiIxMTExMTExMS0xMTExLTExMTEtMTExMS0xMTExMTExMTExMTEiLCJwcm9maWxlUmV2aXNpb24iOjEsInByb2ZpbGVTbmFwc2hvdEhhc2giOiJkMTVjZjliY2ZlMDNlMzJiYmE0NjU3ZDVmODM0ODc3MTUxODUwMWZiYmNkOWIxZDMwODZiZmNjMmQzY2Q2MTdlIiwiZGlzcGxheU5hbWUiOiJBXCJcXC9cYlx0XG5cZlxyXHUwMDAx5rGJ8J+YgGXMgSIsImNyZWF0ZWRBdFV0Y01zIjowLCJ1cGRhdGVkQXRVdGNNcyI6MSwiY2FyZWVyRW50cmllcyI6W119";
        private const string CatalogGoldenHash =
            "d3a523984238503376f49873a044579bc395ff76cb4c73d880a94c22cece363f";
        private const string CatalogGoldenBase64 =
            "eyJzY2hlbWFWZXJzaW9uIjoxLCJjYXRhbG9nUmV2aXNpb24iOjEsImNhdGFsb2dIYXNoIjoiZDNhNTIzOTg0MjM4NTAzMzc2ZjQ5ODczYTA0NDU3OWJjMzk1ZmY3NmNiNGM3M2Q4ODBhOTRjMjJjZWNlMzYzZiIsInByb2ZpbGVzIjpbXX0=";

        [Test]
        public void Profile_GoldenBytesAndHashAreStable()
        {
            var displayName = "A\"\\/\b\t\n\f\r\u0001汉😀e\u0301";
            var candidate = new LocalPlayerProfile(
                1,
                new ProfileId(new Guid("11111111-1111-1111-1111-111111111111")),
                1,
                CareerPersistenceTestData.Hash('0'),
                displayName,
                0,
                1,
                Array.Empty<CareerIndexEntry>());

            var sealedProfile = LocalProfileJsonCodec.SealProfile(candidate);
            var bytes = LocalProfileJsonCodec.SerializeProfile(sealedProfile);

            Assert.That(sealedProfile.ProfileSnapshotHash.Value, Is.EqualTo(ProfileGoldenHash));
            Assert.That(bytes, Is.EqualTo(Convert.FromBase64String(ProfileGoldenBase64)));
            var restored = LocalProfileJsonCodec.DeserializeProfile(bytes);
            Assert.That(restored.DisplayName, Is.EqualTo(displayName));
            Assert.That(restored.CareerEntries, Is.Empty);
        }

        [Test]
        public void Catalog_GoldenBytesAndHashAreStable()
        {
            var candidate = new LocalProfileCatalog(
                1,
                1,
                CareerPersistenceTestData.Hash('0'),
                Array.Empty<LocalProfileCatalogEntry>());

            var sealedCatalog = LocalProfileJsonCodec.SealCatalog(candidate);
            var bytes = LocalProfileJsonCodec.SerializeCatalog(sealedCatalog);

            Assert.That(sealedCatalog.CatalogHash.Value, Is.EqualTo(CatalogGoldenHash));
            Assert.That(bytes, Is.EqualTo(Convert.FromBase64String(CatalogGoldenBase64)));
            Assert.That(LocalProfileJsonCodec.DeserializeCatalog(bytes).Profiles, Is.Empty);
        }

        [Test]
        public void ProfileAndCatalog_RoundTripSortedSummaryEntries()
        {
            var profileId = new ProfileId(Guid.NewGuid());
            var profile = CareerPersistenceTestData.Profile(
                profileId,
                1,
                new CareerIndexEntry(
                    new SaveId(Guid.NewGuid()),
                    "Career",
                    "Player",
                    CareerProgressionKind.Planning,
                    CareerPhase.University,
                    1,
                    1,
                    10,
                    CareerLoadability.RecoveryAvailable));
            profile = LocalProfileJsonCodec.SealProfile(profile);
            var restoredProfile = LocalProfileJsonCodec.DeserializeProfile(
                LocalProfileJsonCodec.SerializeProfile(profile));
            Assert.That(restoredProfile.CareerEntries, Has.Count.EqualTo(1));
            Assert.That(
                restoredProfile.CareerEntries[0].Loadability,
                Is.EqualTo(CareerLoadability.RecoveryAvailable));

            var catalog = new LocalProfileCatalog(
                1,
                1,
                CareerPersistenceTestData.Hash('0'),
                new[]
                {
                    new LocalProfileCatalogEntry(
                        profileId,
                        "Local Player",
                        10,
                        ProfileLoadability.RecoveryAvailable)
                });
            catalog = LocalProfileJsonCodec.SealCatalog(catalog);
            var restoredCatalog = LocalProfileJsonCodec.DeserializeCatalog(
                LocalProfileJsonCodec.SerializeCatalog(catalog));
            Assert.That(
                restoredCatalog.Profiles[0].Loadability,
                Is.EqualTo(ProfileLoadability.RecoveryAvailable));
        }

        [TestCase("{\"schemaVersion\":1,\"schemaVersion\":1}")]
        [TestCase("{\"schemaVersion\":1,\"unknown\":0}")]
        [TestCase("{\"schemaVersion\":2}")]
        [TestCase("{} trailing")]
        public void StrictProfileParser_RejectsInvalidShapes(string json)
        {
            Assert.That(
                () => LocalProfileJsonCodec.DeserializeProfile(Encoding.UTF8.GetBytes(json)),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void ProfileAndCatalog_RejectHashTamperingAndNonCanonicalWhitespace()
        {
            var profile = LocalProfileJsonCodec.SealProfile(
                CareerPersistenceTestData.Profile(new ProfileId(Guid.NewGuid())));
            var profileJson = Encoding.UTF8.GetString(
                LocalProfileJsonCodec.SerializeProfile(profile));
            Assert.That(
                () => LocalProfileJsonCodec.DeserializeProfile(
                    Encoding.UTF8.GetBytes(profileJson.Replace("\"displayName\":", " \"displayName\":"))),
                Throws.TypeOf<FormatException>());

            var catalog = LocalProfileJsonCodec.SealCatalog(
                new LocalProfileCatalog(
                    1,
                    1,
                    CareerPersistenceTestData.Hash('0'),
                    Array.Empty<LocalProfileCatalogEntry>()));
            var catalogBytes = LocalProfileJsonCodec.SerializeCatalog(catalog);
            catalogBytes[catalogBytes.Length - 2] ^= 1;
            Assert.That(
                () => LocalProfileJsonCodec.DeserializeCatalog(catalogBytes),
                Throws.TypeOf<FormatException>());
        }
    }
}
