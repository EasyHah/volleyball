using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class TrainingEmphasisLedgerTests
    {
        [Test]
        public void AddExecutedTraining_UsesFirstAndRepeatCoefficientsAndCap()
        {
            var ledger = TrainingEmphasisLedger.Empty;
            var first = Action(1, "week_action.specialized.spike", CareerWeekActionKind.SpecializedTraining);
            var second = Action(2, "week_action.specialized.spike", CareerWeekActionKind.SpecializedTraining);

            var afterFirst = ledger.AddExecutedTraining(first, CareerWeekActionCatalogV1.Create());
            var afterSecond = afterFirst.AddExecutedTraining(second, CareerWeekActionCatalogV1.Create());

            Assert.That(afterSecond.Contributions.Select(x => x.BonusBasisPoints), Is.EqualTo(new[] { 1000, 500 }));
            Assert.That(afterSecond.Freeze().Single().TotalBonusBasisPoints, Is.EqualTo(1500));
            Assert.That(ledger.Contributions, Is.Empty);
            Assert.That(afterFirst.Contributions.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddExecutedTraining_KeepsDifferentDirectionsAtFullFirstContribution()
        {
            var ledger = TrainingEmphasisLedger.Empty
                .AddExecutedTraining(Action(1, "week_action.specialized.spike", CareerWeekActionKind.SpecializedTraining), CareerWeekActionCatalogV1.Create())
                .AddExecutedTraining(Action(2, "week_action.strength.jump", CareerWeekActionKind.StrengthTraining), CareerWeekActionCatalogV1.Create());

            Assert.That(ledger.Contributions.Select(x => x.BonusBasisPoints), Is.EqualTo(new[] { 1000, 1000 }));
            Assert.That(ledger.Freeze().Select(x => x.Direction),
                Is.EqualTo(new[] { CareerTrainingDirection.Spike, CareerTrainingDirection.Jump }));
        }

        [Test]
        public void Ledger_RejectsNonTrainingAndDuplicateSources()
        {
            var catalog = CareerWeekActionCatalogV1.Create();
            Assert.That(
                () => TrainingEmphasisLedger.Empty.AddExecutedTraining(
                    Action(1, "week_action.team_practice.standard", CareerWeekActionKind.TeamPractice), catalog),
                Throws.ArgumentException);
            Assert.That(
                () => TrainingEmphasisLedger.Empty.AddExecutedTraining(
                    Action(1, "week_action.rest.standard", CareerWeekActionKind.Rest), catalog),
                Throws.ArgumentException);

            var first = Action(1, "week_action.specialized.spike", CareerWeekActionKind.SpecializedTraining);
            var once = TrainingEmphasisLedger.Empty.AddExecutedTraining(first, catalog);
            Assert.That(() => once.AddExecutedTraining(first, catalog), Throws.ArgumentException);
        }

        [Test]
        public void FreezeAndConsume_AreStableSortedAndDoNotMutateOriginals()
        {
            var jumpId = SlotId(1);
            var spikeFirstId = SlotId(2);
            var spikeSecondId = SlotId(3);
            var source = new[]
            {
                new TrainingEmphasisContribution(jumpId, CareerTrainingDirection.Jump, 1000),
                new TrainingEmphasisContribution(spikeFirstId, CareerTrainingDirection.Spike, 1000),
                new TrainingEmphasisContribution(spikeSecondId, CareerTrainingDirection.Spike, 500)
            };
            var ledger = new TrainingEmphasisLedger(source);
            source[0] = new TrainingEmphasisContribution(SlotId(9), CareerTrainingDirection.Stamina, 1000);

            var frozen = ledger.Freeze();
            var consumed = ledger.Consume();

            Assert.That(frozen.Select(x => x.Direction), Is.EqualTo(new[]
            {
                CareerTrainingDirection.Spike,
                CareerTrainingDirection.Jump
            }));
            Assert.That(frozen[0].SourceSlotActionIds, Is.EqualTo(new[] { spikeFirstId, spikeSecondId }));
            Assert.That(frozen[0].TotalBonusBasisPoints, Is.EqualTo(1500));
            Assert.That(ledger.Contributions[0].SourceSlotActionId, Is.EqualTo(jumpId));
            Assert.That(consumed.Contributions, Is.Empty);
            Assert.That(ledger.Contributions.Count, Is.EqualTo(3));
        }

        [Test]
        public void EmphasisTypes_DoNotAliasPreMatchPriority()
        {
            var names = typeof(TrainingEmphasisLedger).Assembly.GetTypes()
                .Where(x => x.Namespace == "Volleyball.Career.Domain")
                .SelectMany(x => x.GetMembers())
                .Select(x => x.Name)
                .ToArray();

            Assert.That(names.Any(x => x.IndexOf("preMatchPriority", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        private static CareerWeekActionState Action(
            int value,
            string contentId,
            CareerWeekActionKind kind)
        {
            return new CareerWeekActionState(SlotId(value), Occurrence(value), kind, contentId);
        }

        private static SlotActionId SlotId(int value)
        {
            return new SlotActionId(Guid.Parse($"21000000-0000-0000-0000-{value:D12}"));
        }

        private static OccurrenceId Occurrence(int value)
        {
            return new OccurrenceId(Guid.Parse($"22000000-0000-0000-0000-{value:D12}"));
        }
    }
}
