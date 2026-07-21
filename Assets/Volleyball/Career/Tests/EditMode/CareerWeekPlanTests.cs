using System;
using NUnit.Framework;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerWeekPlanTests
    {
        [Test]
        public void NewPlan_AlwaysContainsExactlyThreeEmptySlots()
        {
            var plan = CreatePlan();

            Assert.That(plan.Slots, Has.Count.EqualTo(CareerWeekPlan.SlotCount));
            Assert.That(plan.Slots, Is.All.Null);
            Assert.That(plan.CanConfirm, Is.False);
            Assert.That(plan.IsConfirmed, Is.False);
        }

        [TestCase(CareerWeekActionKind.SpecializedTraining)]
        [TestCase(CareerWeekActionKind.StrengthTraining)]
        [TestCase(CareerWeekActionKind.TeamPractice)]
        [TestCase(CareerWeekActionKind.Rest)]
        public void ScheduleAction_AcceptsEveryPlayerChoice(CareerWeekActionKind kind)
        {
            var plan = CreatePlan();
            var action = CreateAction(kind);

            plan.ScheduleAction(1, action);

            Assert.That(plan.Slots[1], Is.SameAs(action));
        }

        [Test]
        public void ActionKinds_ExposeOnlyFourPlayerChoicesAndSystemMatch()
        {
            Assert.That(
                (CareerWeekActionKind[])Enum.GetValues(typeof(CareerWeekActionKind)),
                Is.EqualTo(
                    new[]
                    {
                        CareerWeekActionKind.SpecializedTraining,
                        CareerWeekActionKind.StrengthTraining,
                        CareerWeekActionKind.TeamPractice,
                        CareerWeekActionKind.Rest,
                        CareerWeekActionKind.Match
                    }));
        }

        [Test]
        public void ReserveMatch_ProtectsTheScheduledMatchSlot()
        {
            var plan = CreatePlan();
            var training = CreateAction(CareerWeekActionKind.SpecializedTraining);
            var match = CreateAction(CareerWeekActionKind.Match);
            plan.ScheduleAction(0, training);
            plan.ReserveMatch(1, match);

            Assert.That(plan.Slots[1], Is.SameAs(match));
            Assert.That(
                () => plan.ScheduleAction(1, CreateAction(CareerWeekActionKind.Rest)),
                Throws.InvalidOperationException.With.Message.Contains("cannot be overwritten"));
            Assert.That(
                () => plan.RemoveAction(1),
                Throws.InvalidOperationException.With.Message.Contains("cannot be removed"));
            Assert.That(
                () => plan.MoveAction(1, 2),
                Throws.InvalidOperationException.With.Message.Contains("cannot be moved"));
            Assert.That(
                () => plan.MoveAction(0, 1),
                Throws.InvalidOperationException.With.Message.Contains("cannot be moved"));
        }

        [Test]
        public void MatchAction_CannotBypassReservationRules()
        {
            var plan = CreatePlan();

            Assert.That(
                () => plan.ScheduleAction(0, CreateAction(CareerWeekActionKind.Match)),
                Throws.ArgumentException.With.Message.Contains("ReserveMatch"));
            Assert.That(
                () => plan.ReserveMatch(0, CreateAction(CareerWeekActionKind.Rest)),
                Throws.ArgumentException.With.Message.Contains("match action"));
        }

        [Test]
        public void PlayerActions_CanBeReplacedBeforeConfirmation()
        {
            var plan = CreatePlan();
            var training = CreateAction(CareerWeekActionKind.SpecializedTraining);
            var rest = CreateAction(CareerWeekActionKind.Rest);
            plan.ScheduleAction(0, training);

            plan.ScheduleAction(0, rest);

            Assert.That(plan.Slots[0], Is.SameAs(rest));
        }

        [Test]
        public void MoveAction_PreservesStableActionAndOccurrenceIds()
        {
            var plan = CreatePlan();
            var first = CreateAction(CareerWeekActionKind.TeamPractice);
            var second = CreateAction(CareerWeekActionKind.Rest);
            plan.ScheduleAction(0, first);
            plan.ScheduleAction(2, second);

            plan.MoveAction(0, 2);

            Assert.That(plan.Slots[0], Is.SameAs(second));
            Assert.That(plan.Slots[2], Is.SameAs(first));
            Assert.That(plan.Slots[2].SlotActionId, Is.EqualTo(first.SlotActionId));
            Assert.That(plan.Slots[2].OccurrenceId, Is.EqualTo(first.OccurrenceId));
        }

        [Test]
        public void ScheduleAction_RejectsDuplicateStableIdsAcrossSlots()
        {
            var plan = CreatePlan();
            var first = CreateAction(CareerWeekActionKind.Rest);
            plan.ScheduleAction(0, first);

            var duplicateActionId = new CareerWeekAction(
                first.SlotActionId,
                new OccurrenceId(Guid.NewGuid()),
                CareerWeekActionKind.StrengthTraining);
            var duplicateOccurrenceId = new CareerWeekAction(
                new SlotActionId(Guid.NewGuid()),
                first.OccurrenceId,
                CareerWeekActionKind.TeamPractice);

            Assert.That(
                () => plan.ScheduleAction(1, duplicateActionId),
                Throws.ArgumentException.With.Message.Contains("Slot action IDs"));
            Assert.That(
                () => plan.ScheduleAction(1, duplicateOccurrenceId),
                Throws.ArgumentException.With.Message.Contains("Occurrence IDs"));
        }

        [Test]
        public void Confirm_RequiresThreeActionsAndLocksThePlan()
        {
            var plan = CreatePlan();
            plan.ScheduleAction(0, CreateAction(CareerWeekActionKind.StrengthTraining));
            plan.ReserveMatch(1, CreateAction(CareerWeekActionKind.Match));

            Assert.That(
                () => plan.Confirm(),
                Throws.InvalidOperationException.With.Message.Contains("All three"));

            plan.ScheduleAction(2, CreateAction(CareerWeekActionKind.Rest));
            plan.Confirm();

            Assert.That(plan.IsConfirmed, Is.True);
            Assert.That(plan.CanConfirm, Is.False);
            Assert.That(
                () => plan.RemoveAction(0),
                Throws.InvalidOperationException.With.Message.Contains("confirmed"));
            Assert.That(
                () => plan.ScheduleAction(2, CreateAction(CareerWeekActionKind.TeamPractice)),
                Throws.InvalidOperationException.With.Message.Contains("confirmed"));
            Assert.That(
                () => plan.ReserveMatch(0, CreateAction(CareerWeekActionKind.Match)),
                Throws.InvalidOperationException.With.Message.Contains("confirmed"));
            Assert.That(
                () => plan.MoveAction(0, 2),
                Throws.InvalidOperationException.With.Message.Contains("confirmed"));
            Assert.That(
                () => plan.Confirm(),
                Throws.InvalidOperationException.With.Message.Contains("confirmed"));
        }

        [Test]
        public void Constructors_RejectInvalidCalendarAndIdentityValues()
        {
            Assert.That(
                () => new CareerWeekPlan(new WeekPlanId(Guid.NewGuid()), 0, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new CareerWeekPlan(new WeekPlanId(Guid.NewGuid()), 7, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new CareerWeekPlan(new WeekPlanId(Guid.NewGuid()), 1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new WeekPlanId(Guid.Empty),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerWeekPlan(default, 1, 1),
                Throws.ArgumentException);
            Assert.That(
                () => new SlotActionId(Guid.Empty),
                Throws.ArgumentException);
            Assert.That(
                () => new OccurrenceId(Guid.Empty),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerWeekAction(
                    default,
                    new OccurrenceId(Guid.NewGuid()),
                    CareerWeekActionKind.Rest),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerWeekAction(
                    new SlotActionId(Guid.NewGuid()),
                    default,
                    CareerWeekActionKind.Rest),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerWeekAction(
                    new SlotActionId(Guid.NewGuid()),
                    new OccurrenceId(Guid.NewGuid()),
                    (CareerWeekActionKind)99),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static CareerWeekPlan CreatePlan()
        {
            return new CareerWeekPlan(new WeekPlanId(Guid.NewGuid()), season: 1, week: 1);
        }

        private static CareerWeekAction CreateAction(CareerWeekActionKind kind)
        {
            return new CareerWeekAction(
                new SlotActionId(Guid.NewGuid()),
                new OccurrenceId(Guid.NewGuid()),
                kind);
        }
    }
}
