using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Persistence;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerSaveV2LifecycleValidationTests
    {
        [Test]
        public void PendingMatchV4_RoundTripsThroughSaveSchemaV2()
        {
            var source = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();

            var restored = CareerSaveSnapshotMapper.ToDomain(
                CareerSaveSnapshotMapper.ToDocument(source));

            Assert.That(restored.Versions.ContractVersion, Is.EqualTo(2));
            Assert.That(restored.PendingMatch.Versions.ContractVersion, Is.EqualTo(4));
            Assert.That(
                restored.PendingMatch.CanonicalContextUtf8,
                Is.EqualTo(source.PendingMatch.CanonicalContextUtf8));
        }

        [Test]
        public void SettledMatchV3_RoundTripsCanonicalEvidence()
        {
            var source = CareerSaveV2LifecycleTestData.SettledSnapshot();

            var restored = CareerSaveSnapshotMapper.ToDomain(
                CareerSaveSnapshotMapper.ToDocument(source));

            Assert.That(restored.MatchHistory, Has.Count.EqualTo(1));
            Assert.That(restored.SettlementReceipts, Has.Count.EqualTo(1));
            Assert.That(
                restored.MatchHistory[0].CanonicalResultUtf8,
                Is.EqualTo(source.MatchHistory[0].CanonicalResultUtf8));
        }

        [Test]
        public void PendingMatchV3_RejectsNonCanonicalContextBytes()
        {
            var document = AwaitingDocument();
            document.pendingMatch.canonicalContextUtf8Base64 = AppendSpace(
                document.pendingMatch.canonicalContextUtf8Base64);

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void PendingMatchV3_RejectsMetadataContradiction()
        {
            var document = AwaitingDocument();
            document.pendingMatch.contextHash = new string('0', 64);

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void PendingMatchV3_RejectsSeedContradiction()
        {
            var document = AwaitingDocument();
            document.pendingMatch.matchSeed++;

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void SettledMatchV3_RejectsNonCanonicalResultBytes()
        {
            var document = CareerSaveSnapshotMapper.ToDocument(
                CareerSaveV2LifecycleTestData.SettledSnapshot());
            document.matchHistory[0].canonicalResultUtf8Base64 = AppendSpace(
                document.matchHistory[0].canonicalResultUtf8Base64);

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        private static CareerSaveDocumentV2 AwaitingDocument()
        {
            return CareerSaveSnapshotMapper.ToDocument(
                CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot());
        }

        private static string AppendSpace(string canonicalBase64)
        {
            return Convert.ToBase64String(
                Convert.FromBase64String(canonicalBase64)
                    .Concat(new[] { (byte)' ' })
                    .ToArray());
        }
    }
}
