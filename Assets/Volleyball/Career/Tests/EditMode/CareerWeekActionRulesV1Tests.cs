using System;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerWeekActionRulesV1Tests
    {
        [Test]
        public void Calculate_CoversAllSelectableActionsAtAllPotentialMultipliers()
        {
            var catalog = CareerWeekActionCatalogV1.Create();
            var grades = new[] { PotentialGrade.D, PotentialGrade.C, PotentialGrade.B, PotentialGrade.A, PotentialGrade.S };
            var multipliers = new[] { 8000, 9000, 10000, 11000, 12000 };

            for (var actionIndex = 0; actionIndex < 10; actionIndex++)
            {
                var action = catalog.Actions[actionIndex];
                for (var gradeIndex = 0; gradeIndex < grades.Length; gradeIndex++)
                {
                    var result = CareerWeekActionRulesV1.Calculate(
                        "reason.week_action",
                        action.ContentId,
                        grades[gradeIndex],
                        Attributes(),
                        40,
                        50,
                        40);
                    var expected = action.BaseGrowthExperience * multipliers[gradeIndex] / 10000;

                    if (action.Kind == CareerWeekActionKind.TeamPractice)
                    {
                        AssertAllGrowth(result.GrowthExperienceDelta, expected);
                    }
                    else if (action.Direction.HasValue)
                    {
                        Assert.That(
                            result.GrowthExperienceDelta.Get(action.Direction.Value),
                            Is.EqualTo(expected),
                            action.ContentId + " / " + grades[gradeIndex]);
                        Assert.That(result.GrowthExperienceDelta.Total, Is.EqualTo(expected));
                    }
                    else
                    {
                        Assert.That(result.GrowthExperienceDelta.Total, Is.Zero);
                    }

                    Assert.That(result.ReasonId, Is.EqualTo("reason.week_action"));
                    Assert.That(result.ContentId, Is.EqualTo(action.ContentId));
                }
            }
        }

        [Test]
        public void Calculate_MapsGrowthAndStatusTuningExactly()
        {
            var specialized = Calculate("week_action.specialized.serve", attributes: Attributes());
            var strength = Calculate("week_action.strength.jump", attributes: Attributes());
            var team = Calculate("week_action.team_practice.standard", attributes: Attributes());

            Assert.That(specialized.GrowthExperienceDelta.Serve, Is.EqualTo(120));
            Assert.That(specialized.Fatigue.Delta, Is.EqualTo(8));
            Assert.That(strength.GrowthExperienceDelta.Jump, Is.EqualTo(100));
            Assert.That(strength.Fatigue.Delta, Is.EqualTo(12));
            AssertAllGrowth(team.GrowthExperienceDelta, 20);
            Assert.That(team.Fatigue.Delta, Is.EqualTo(6));
            Assert.That(team.CoachTrust.Delta, Is.EqualTo(5));
        }

        [TestCase(45, 50, 5)]
        [TestCase(49, 50, 1)]
        [TestCase(50, 50, 0)]
        [TestCase(51, 50, -1)]
        [TestCase(60, 55, -5)]
        public void Rest_MovesMindsetTowardFiftyWithoutCrossing(
            int initial,
            int expected,
            int expectedDelta)
        {
            var result = Calculate("week_action.rest.standard", mindset: initial);

            Assert.That(result.Mindset.NewValue, Is.EqualTo(expected));
            Assert.That(result.Mindset.Delta, Is.EqualTo(expectedDelta));
            Assert.That(result.Fatigue.Delta, Is.EqualTo(-18));
        }

        [Test]
        public void Calculate_ClampsStatusesAndGrowthAndNeverChangesAbility()
        {
            var nearMaximum = Attributes(
                ability: 4321,
                growth: CareerAttributeProgress.MaximumGrowthExperience - 10);
            var team = CareerWeekActionRulesV1.Calculate(
                "reason.boundary",
                "week_action.team_practice.standard",
                PotentialGrade.S,
                nearMaximum,
                98,
                100,
                99);
            var rest = CareerWeekActionRulesV1.Calculate(
                "reason.boundary",
                "week_action.rest.standard",
                PotentialGrade.B,
                Attributes(ability: 4321),
                3,
                0,
                0);

            AssertAllGrowth(team.GrowthExperienceDelta, 10);
            Assert.That(team.Fatigue.Delta, Is.EqualTo(2));
            Assert.That(team.CoachTrust.Delta, Is.EqualTo(1));
            Assert.That(rest.Fatigue.Delta, Is.EqualTo(-3));
            Assert.That(rest.Mindset.Delta, Is.EqualTo(5));
            for (var kind = CareerAttributeKind.Spike; kind <= CareerAttributeKind.Stamina; kind++)
            {
                Assert.That(team.NextAttributes.Get(kind).AbilityBasisPoints, Is.EqualTo(4321));
            }
        }

        [Test]
        public void Calculate_RejectsMatchUnknownContentAndInvalidCurrentState()
        {
            Assert.That(() => Calculate("schedule.u1w1.match.01"), Throws.ArgumentException);
            Assert.That(() => Calculate("week_action.unknown"), Throws.ArgumentException);
            Assert.That(
                () => CareerWeekActionRulesV1.Calculate(
                    "reason",
                    "week_action.rest.standard",
                    PotentialGrade.B,
                    Attributes(),
                    -1,
                    50,
                    50),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static CareerWeekActionCalculation Calculate(
            string contentId,
            CareerPlayerAttributes attributes = null,
            int fatigue = 40,
            int mindset = 50,
            int trust = 40)
        {
            return CareerWeekActionRulesV1.Calculate(
                "reason.week_action",
                contentId,
                PotentialGrade.B,
                attributes ?? Attributes(),
                fatigue,
                mindset,
                trust);
        }

        private static CareerPlayerAttributes Attributes(int ability = 5000, long growth = 0)
        {
            var value = new CareerAttributeProgress(ability, growth);
            return new CareerPlayerAttributes(value, value, value, value, value, value, value, value);
        }

        private static void AssertAllGrowth(CareerAttributeGrowthDelta delta, long expected)
        {
            Assert.That(delta.Spike, Is.EqualTo(expected));
            Assert.That(delta.Serve, Is.EqualTo(expected));
            Assert.That(delta.Reception, Is.EqualTo(expected));
            Assert.That(delta.Defense, Is.EqualTo(expected));
            Assert.That(delta.Block, Is.EqualTo(expected));
            Assert.That(delta.Movement, Is.EqualTo(expected));
            Assert.That(delta.Jump, Is.EqualTo(expected));
            Assert.That(delta.Stamina, Is.EqualTo(expected));
        }
    }
}
