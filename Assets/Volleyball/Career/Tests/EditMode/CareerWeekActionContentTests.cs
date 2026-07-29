using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerWeekActionContentTests
    {
        [Test]
        public void Action_PreservesStableContentIdentity()
        {
            var action = new CareerWeekAction(
                new SlotActionId(Guid.Parse("11000000-0000-0000-0000-000000000001")),
                new OccurrenceId(Guid.Parse("12000000-0000-0000-0000-000000000001")),
                CareerWeekActionKind.SpecializedTraining,
                "week_action.specialized.spike");

            Assert.That(action.ContentId, Is.EqualTo("week_action.specialized.spike"));
            Assert.That(
                () => new CareerWeekAction(
                    new SlotActionId(Guid.Parse("11000000-0000-0000-0000-000000000002")),
                    new OccurrenceId(Guid.Parse("12000000-0000-0000-0000-000000000002")),
                    CareerWeekActionKind.SpecializedTraining,
                    "week_action.specialized.扣球"),
                Throws.ArgumentException);
        }

        [Test]
        public void CatalogV1_ContainsExactClosedActionIdentityAndTuning()
        {
            var catalog = CareerWeekActionCatalogV1.Create();
            var expected = new[]
            {
                Tuple("week_action.specialized.spike", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Spike, 120, 8, 0, 0, null, 0),
                Tuple("week_action.specialized.serve", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Serve, 120, 8, 0, 0, null, 0),
                Tuple("week_action.specialized.reception", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Reception, 120, 8, 0, 0, null, 0),
                Tuple("week_action.specialized.defense", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Defense, 120, 8, 0, 0, null, 0),
                Tuple("week_action.specialized.block", CareerWeekActionKind.SpecializedTraining, CareerTrainingDirection.Block, 120, 8, 0, 0, null, 0),
                Tuple("week_action.strength.movement", CareerWeekActionKind.StrengthTraining, CareerTrainingDirection.Movement, 100, 12, 0, 0, null, 0),
                Tuple("week_action.strength.jump", CareerWeekActionKind.StrengthTraining, CareerTrainingDirection.Jump, 100, 12, 0, 0, null, 0),
                Tuple("week_action.strength.stamina", CareerWeekActionKind.StrengthTraining, CareerTrainingDirection.Stamina, 100, 12, 0, 0, null, 0),
                Tuple("week_action.team_practice.standard", CareerWeekActionKind.TeamPractice, null, 20, 6, 0, 5, null, 0),
                Tuple("week_action.rest.standard", CareerWeekActionKind.Rest, null, 0, -18, 0, 0, 50, 5),
                Tuple("schedule.u1w1.match.01", CareerWeekActionKind.Match, null, 0, 0, 0, 0, null, 0)
            };

            Assert.That(catalog.Actions.Select(ToTuple), Is.EqualTo(expected));
        }

        [Test]
        public void CatalogV1_RejectsIdentityOrderKindDirectionAndTuningDrift()
        {
            var canonical = CareerWeekActionCatalogV1.Create().Actions.ToArray();

            Assert.That(() => NewCatalog(Replace(canonical, 0, Copy(canonical[0], contentId: "week_action.specialized.changed"))), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Swap(canonical, 0, 1)), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Replace(canonical, 0, Copy(canonical[0], kind: CareerWeekActionKind.StrengthTraining))), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Replace(canonical, 0, Copy(canonical[0], direction: CareerTrainingDirection.Serve))), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Replace(canonical, 0, Copy(canonical[0], baseGrowthExperience: 121))), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Replace(canonical, 0, Copy(canonical[0], fatigueDelta: 9))), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Replace(canonical, 0, Copy(canonical[0], mindsetDelta: 1))), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Replace(canonical, 0, Copy(canonical[0], coachTrustDelta: 1))), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Replace(canonical, 9, Copy(canonical[9], mindsetTarget: 49, replaceMindsetTarget: true))), Throws.ArgumentException);
            Assert.That(() => NewCatalog(Replace(canonical, 9, Copy(canonical[9], mindsetMaximumStep: 4))), Throws.ArgumentException);
        }

        [Test]
        public void WeekPlanScheduleReplaceMoveAndConfirmPreserveContentIdentity()
        {
            var plan = new CareerWeekPlan(
                new WeekPlanId(Guid.Parse("13000000-0000-0000-0000-000000000001")),
                1,
                1);
            var first = Action(1, CareerWeekActionKind.SpecializedTraining, "week_action.specialized.spike");
            var replacement = Action(2, CareerWeekActionKind.SpecializedTraining, "week_action.specialized.serve");
            var second = Action(3, CareerWeekActionKind.Rest, "week_action.rest.standard");
            var match = Action(4, CareerWeekActionKind.Match, "schedule.u1w1.match.01");

            plan.ScheduleAction(0, first);
            plan.ScheduleAction(0, replacement);
            plan.ScheduleAction(1, second);
            plan.ReserveMatch(2, match);
            plan.MoveAction(0, 1);
            plan.Confirm();
            var state = new CareerWeekPlanState(plan);

            Assert.That(state.Slots[0].ContentId, Is.EqualTo("week_action.rest.standard"));
            Assert.That(state.Slots[1].ContentId, Is.EqualTo("week_action.specialized.serve"));
            Assert.That(state.Slots[2].ContentId, Is.EqualTo("schedule.u1w1.match.01"));
        }

        [Test]
        public void GenericPlanAllowsSameCategoryWithDifferentValidDirectionContentIds()
        {
            var plan = new CareerWeekPlan(
                new WeekPlanId(Guid.Parse("13000000-0000-0000-0000-000000000002")),
                1,
                1);

            plan.ScheduleAction(0, Action(5, CareerWeekActionKind.SpecializedTraining, "week_action.specialized.spike"));
            plan.ScheduleAction(1, Action(6, CareerWeekActionKind.SpecializedTraining, "week_action.specialized.serve"));
            plan.ReserveMatch(2, Action(7, CareerWeekActionKind.Match, "schedule.u1w1.match.01"));

            Assert.That(plan.Slots[0].Kind, Is.EqualTo(plan.Slots[1].Kind));
            Assert.That(plan.Slots[0].ContentId, Is.Not.EqualTo(plan.Slots[1].ContentId));
        }

        private static CareerWeekAction Action(
            int value,
            CareerWeekActionKind kind,
            string contentId)
        {
            return new CareerWeekAction(
                new SlotActionId(Guid.Parse($"14000000-0000-0000-0000-{value:D12}")),
                new OccurrenceId(Guid.Parse($"15000000-0000-0000-0000-{value:D12}")),
                kind,
                contentId);
        }

        private static CareerWeekActionCatalog NewCatalog(CareerWeekActionContentDefinition[] actions)
        {
            return new CareerWeekActionCatalog(1, 1, actions);
        }

        private static CareerWeekActionContentDefinition Copy(
            CareerWeekActionContentDefinition value,
            string contentId = null,
            CareerWeekActionKind? kind = null,
            CareerTrainingDirection? direction = null,
            int? baseGrowthExperience = null,
            int? fatigueDelta = null,
            int? mindsetDelta = null,
            int? coachTrustDelta = null,
            int? mindsetTarget = null,
            bool replaceMindsetTarget = false,
            int? mindsetMaximumStep = null)
        {
            return new CareerWeekActionContentDefinition(
                contentId ?? value.ContentId,
                kind ?? value.Kind,
                direction ?? value.Direction,
                baseGrowthExperience ?? value.BaseGrowthExperience,
                fatigueDelta ?? value.FatigueDelta,
                mindsetDelta ?? value.MindsetDelta,
                coachTrustDelta ?? value.CoachTrustDelta,
                replaceMindsetTarget ? mindsetTarget : value.MindsetTarget,
                mindsetMaximumStep ?? value.MindsetMaximumStep);
        }

        private static object[] Tuple(
            string contentId,
            CareerWeekActionKind kind,
            CareerTrainingDirection? direction,
            int baseGrowthExperience,
            int fatigueDelta,
            int mindsetDelta,
            int coachTrustDelta,
            int? mindsetTarget,
            int mindsetMaximumStep)
        {
            return new object[]
            {
                contentId,
                kind,
                direction,
                baseGrowthExperience,
                fatigueDelta,
                mindsetDelta,
                coachTrustDelta,
                mindsetTarget,
                mindsetMaximumStep
            };
        }

        private static object[] ToTuple(CareerWeekActionContentDefinition value)
        {
            return Tuple(
                value.ContentId,
                value.Kind,
                value.Direction,
                value.BaseGrowthExperience,
                value.FatigueDelta,
                value.MindsetDelta,
                value.CoachTrustDelta,
                value.MindsetTarget,
                value.MindsetMaximumStep);
        }

        private static CareerWeekActionContentDefinition[] Replace(
            CareerWeekActionContentDefinition[] source,
            int index,
            CareerWeekActionContentDefinition replacement)
        {
            var result = (CareerWeekActionContentDefinition[])source.Clone();
            result[index] = replacement;
            return result;
        }

        private static CareerWeekActionContentDefinition[] Swap(
            CareerWeekActionContentDefinition[] source,
            int left,
            int right)
        {
            var result = (CareerWeekActionContentDefinition[])source.Clone();
            var temporary = result[left];
            result[left] = result[right];
            result[right] = temporary;
            return result;
        }
    }
}
