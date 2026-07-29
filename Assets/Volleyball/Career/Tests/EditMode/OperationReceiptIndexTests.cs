using System;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class OperationReceiptIndexTests
    {
        [Test]
        public void Find_ReturnsExistingReceiptForTheSameOperationAndFingerprint()
        {
            var receipt = CreateReceipt();
            var index = new OperationReceiptIndex(new[] { receipt });

            var lookup = index.Find(receipt.OperationId, receipt.InputFingerprint);

            Assert.That(lookup.Kind, Is.EqualTo(OperationReceiptLookupKind.Existing));
            Assert.That(lookup.Receipt, Is.SameAs(receipt));
            Assert.That(
                lookup.Receipt.OutcomeSummary.OutcomeKind,
                Is.EqualTo(OperationOutcomeKind.WeekPlanConfirmed));
        }

        [Test]
        public void Find_ReturnsConflictWhenAnOperationIdIsReusedWithDifferentInput()
        {
            var receipt = CreateReceipt();
            var index = new OperationReceiptIndex(new[] { receipt });

            var lookup = index.Find(receipt.OperationId, Digest('b'));

            Assert.That(lookup.Kind, Is.EqualTo(OperationReceiptLookupKind.Conflict));
            Assert.That(lookup.Receipt, Is.SameAs(receipt));
        }

        [Test]
        public void Find_ReturnsMissingForAValidUnknownOperation()
        {
            var index = new OperationReceiptIndex(Array.Empty<OperationReceipt>());

            var lookup = index.Find(new OperationId(Guid.NewGuid()), Digest('c'));

            Assert.That(lookup.Kind, Is.EqualTo(OperationReceiptLookupKind.Missing));
            Assert.That(lookup.Receipt, Is.Null);
        }

        [Test]
        public void Constructor_RejectsNullEntriesAndDuplicateOperationIds()
        {
            var receipt = CreateReceipt();
            var duplicate = new OperationReceipt(
                receipt.OperationId,
                receipt.OperationKind,
                receipt.Target,
                Digest('d'),
                receipt.AppliedLineageId,
                receipt.AppliedRevision,
                receipt.CompletedAtUtcMs,
                receipt.OutcomeKind,
                receipt.OutcomeSummary);

            Assert.That(
                () => new OperationReceiptIndex(null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new OperationReceiptIndex(new OperationReceipt[] { null }),
                Throws.ArgumentException);
            Assert.That(
                () => new OperationReceiptIndex(new[] { receipt, duplicate }),
                Throws.ArgumentException.With.Message.Contains("unique"));
        }

        [Test]
        public void Find_RejectsDefaultIdentityAndFingerprintValues()
        {
            var index = new OperationReceiptIndex(Array.Empty<OperationReceipt>());

            Assert.That(
                () => index.Find(default, Digest('e')),
                Throws.ArgumentException);
            Assert.That(
                () => index.Find(new OperationId(Guid.NewGuid()), default),
                Throws.ArgumentException);
        }

        private static OperationReceipt CreateReceipt()
        {
            return new OperationReceipt(
                new OperationId(Guid.NewGuid()),
                OperationKind.ConfirmWeekPlan,
                OperationReceiptTarget.ForWeekPlanConfirmation(
                    new WeekPlanId(Guid.NewGuid())),
                Digest('a'),
                new LineageId(Guid.NewGuid()),
                appliedRevision: 2,
                completedAtUtcMs: 1000,
                OperationOutcomeKind.WeekPlanConfirmed,
                OperationOutcomeSummary.ForWeekPlanConfirmed());
        }

        private static Sha256Digest Digest(char character)
        {
            return new Sha256Digest(new string(character, CareerSeed.HexLength));
        }
    }
}
