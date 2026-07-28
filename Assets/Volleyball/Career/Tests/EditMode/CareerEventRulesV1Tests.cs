using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerEventRulesV1Tests
    {
        [TestCase("event.team_meal.option.attend", 0, 0, 2, 8, 2)]
        [TestCase("event.team_meal.option.attend", 4999, 0, 2, 8, 2)]
        [TestCase("event.team_meal.option.attend", 5000, 0, 4, 6, 3)]
        [TestCase("event.team_meal.option.attend", 9999, 0, 4, 6, 3)]
        [TestCase("event.team_meal.option.extra_practice", 0, 60, 8, 1, 5)]
        [TestCase("event.team_meal.option.extra_practice", 4999, 60, 8, 1, 5)]
        [TestCase("event.team_meal.option.extra_practice", 5000, 80, 10, -2, 6)]
        [TestCase("event.team_meal.option.extra_practice", 9999, 80, 10, -2, 6)]
        public void Resolve_UsesExactIntervalBoundaries(
            string optionId,
            int roll,
            long expectedSpike,
            int expectedFatigue,
            int expectedMindset,
            int expectedTrust)
        {
            var effect = Resolve(optionId, roll);

            Assert.That(effect.GrowthExperienceDelta.Spike, Is.EqualTo(expectedSpike));
            Assert.That(effect.GrowthExperienceDelta.Total, Is.EqualTo(expectedSpike));
            Assert.That(effect.FatigueDelta, Is.EqualTo(expectedFatigue));
            Assert.That(effect.MindsetDelta, Is.EqualTo(expectedMindset));
            Assert.That(effect.CoachTrustDelta, Is.EqualTo(expectedTrust));
        }

        [Test]
        public void Resolve_GoldenAttendRoll6791HasNoPermanentXp()
        {
            var effect = Resolve("event.team_meal.option.attend", 6791);

            Assert.That(effect.GrowthExperienceDelta.Total, Is.Zero);
            Assert.That(effect.FatigueDelta, Is.EqualTo(4));
            Assert.That(effect.MindsetDelta, Is.EqualTo(6));
            Assert.That(effect.CoachTrustDelta, Is.EqualTo(3));
        }

        [TestCase(PotentialGrade.D, 64)]
        [TestCase(PotentialGrade.C, 72)]
        [TestCase(PotentialGrade.B, 80)]
        [TestCase(PotentialGrade.A, 88)]
        [TestCase(PotentialGrade.S, 96)]
        public void Resolve_ScalesExtraPracticeXpByPotential(PotentialGrade grade, long expected)
        {
            var effect = CareerEventRulesV1.Resolve(
                1, 1,
                "event.team_meal",
                "event.team_meal.option.extra_practice",
                5000,
                grade,
                Attributes(),
                40, 50, 40);

            Assert.That(effect.GrowthExperienceDelta.Spike, Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_ReturnsActualClampedDeltas()
        {
            var effect = CareerEventRulesV1.Resolve(
                1, 1,
                "event.team_meal",
                "event.team_meal.option.extra_practice",
                5000,
                PotentialGrade.S,
                Attributes(CareerAttributeProgress.MaximumGrowthExperience - 10),
                95, 1, 98);

            Assert.That(effect.GrowthExperienceDelta.Spike, Is.EqualTo(10));
            Assert.That(effect.FatigueDelta, Is.EqualTo(5));
            Assert.That(effect.MindsetDelta, Is.EqualTo(-1));
            Assert.That(effect.CoachTrustDelta, Is.EqualTo(2));
        }

        [Test]
        public void Resolve_RejectsUnknownIdsInvalidRollVersionAndState()
        {
            Assert.That(() => Resolve("event.unknown", 0), Throws.ArgumentException);
            Assert.That(
                () => CareerEventRulesV1.Resolve(1, 1, "event.unknown", "event.team_meal.option.attend", 0, PotentialGrade.B, Attributes(), 40, 50, 40),
                Throws.ArgumentException);
            Assert.That(() => Resolve("event.team_meal.option.attend", -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => Resolve("event.team_meal.option.attend", 10000), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CareerEventRulesV1.Resolve(2, 1, "event.team_meal", "event.team_meal.option.attend", 0, PotentialGrade.B, Attributes(), 40, 50, 40),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CareerEventRulesV1.Resolve(1, 2, "event.team_meal", "event.team_meal.option.attend", 0, PotentialGrade.B, Attributes(), 40, 50, 40),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CareerEventRulesV1.Resolve(1, 1, "event.team_meal", "event.team_meal.option.attend", 0, PotentialGrade.B, Attributes(), 101, 50, 40),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void EventCatalogV1_RejectsIdRangeAndEffectDrift()
        {
            var canonical = CareerSocialEventCatalogV1.Create().Events.Single();
            var attend = canonical.Options[0];
            var extra = canonical.Options[1];

            Assert.That(
                () => NewCatalog(new CareerSocialEventDefinition("event.changed", new[] { attend, extra })),
                Throws.ArgumentException);
            Assert.That(
                () => NewCatalog(new CareerSocialEventDefinition(canonical.EventId, new[] { extra, attend })),
                Throws.ArgumentException);
            Assert.That(
                () => NewCatalog(new CareerSocialEventDefinition(canonical.EventId, new[]
                {
                    new CareerEventOptionDefinition(attend.OptionId, new[]
                    {
                        Copy(attend.Ranges[0], maximumRollInclusive: 4998),
                        attend.Ranges[1]
                    }),
                    extra
                })),
                Throws.ArgumentException);
            Assert.That(
                () => NewCatalog(new CareerSocialEventDefinition(canonical.EventId, new[]
                {
                    new CareerEventOptionDefinition(attend.OptionId, new[]
                    {
                        Copy(attend.Ranges[0], fatigueDelta: 3),
                        attend.Ranges[1]
                    }),
                    extra
                })),
                Throws.ArgumentException);
        }

        private static CareerEventOptionEffect Resolve(string optionId, int roll)
        {
            return CareerEventRulesV1.Resolve(
                1, 1,
                "event.team_meal",
                optionId,
                roll,
                PotentialGrade.B,
                Attributes(),
                40, 50, 40);
        }

        private static CareerPlayerAttributes Attributes(long spikeGrowth = 0)
        {
            var spike = new CareerAttributeProgress(5000, spikeGrowth);
            var other = new CareerAttributeProgress(5000, 0);
            return new CareerPlayerAttributes(spike, other, other, other, other, other, other, other);
        }

        private static CareerEventRollRangeDefinition Copy(
            CareerEventRollRangeDefinition value,
            int? maximumRollInclusive = null,
            int? fatigueDelta = null)
        {
            return new CareerEventRollRangeDefinition(
                value.MinimumRollInclusive,
                maximumRollInclusive ?? value.MaximumRollInclusive,
                value.BaseGrowthExperienceDelta,
                fatigueDelta ?? value.FatigueDelta,
                value.MindsetDelta,
                value.CoachTrustDelta);
        }

        private static CareerSocialEventCatalog NewCatalog(CareerSocialEventDefinition definition)
        {
            return new CareerSocialEventCatalog(1, 1, new[] { definition });
        }
    }
}
