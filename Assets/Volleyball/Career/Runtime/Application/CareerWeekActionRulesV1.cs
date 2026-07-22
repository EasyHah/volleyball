using System;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public sealed class CareerIntegerChange
    {
        public CareerIntegerChange(int oldValue, int delta, int newValue)
        {
            if (oldValue < 0 || oldValue > 100 || newValue < 0 || newValue > 100 ||
                newValue - oldValue != delta)
            {
                throw new ArgumentException("A status change requires consistent [0, 100] old/delta/new values.");
            }

            OldValue = oldValue;
            Delta = delta;
            NewValue = newValue;
        }

        public int OldValue { get; }
        public int Delta { get; }
        public int NewValue { get; }
    }

    public sealed class CareerWeekActionCalculation
    {
        public CareerWeekActionCalculation(
            string reasonId,
            string contentId,
            CareerPlayerAttributes nextAttributes,
            CareerAttributeGrowthDelta growthExperienceDelta,
            CareerIntegerChange fatigue,
            CareerIntegerChange mindset,
            CareerIntegerChange coachTrust)
        {
            if (string.IsNullOrWhiteSpace(reasonId))
            {
                throw new ArgumentException("An action calculation reason ID is required.", nameof(reasonId));
            }

            if (string.IsNullOrWhiteSpace(contentId))
            {
                throw new ArgumentException("An action content ID is required.", nameof(contentId));
            }

            ReasonId = reasonId;
            ContentId = contentId;
            NextAttributes = nextAttributes ?? throw new ArgumentNullException(nameof(nextAttributes));
            GrowthExperienceDelta = growthExperienceDelta ?? throw new ArgumentNullException(nameof(growthExperienceDelta));
            Fatigue = fatigue ?? throw new ArgumentNullException(nameof(fatigue));
            Mindset = mindset ?? throw new ArgumentNullException(nameof(mindset));
            CoachTrust = coachTrust ?? throw new ArgumentNullException(nameof(coachTrust));
        }

        public string ReasonId { get; }
        public string ContentId { get; }
        public CareerPlayerAttributes NextAttributes { get; }
        public CareerAttributeGrowthDelta GrowthExperienceDelta { get; }
        public CareerIntegerChange Fatigue { get; }
        public CareerIntegerChange Mindset { get; }
        public CareerIntegerChange CoachTrust { get; }
    }

    public static class CareerWeekActionRulesV1
    {
        public static CareerWeekActionCalculation Calculate(
            string reasonId,
            string contentId,
            PotentialGrade potentialGrade,
            CareerPlayerAttributes currentAttributes,
            int currentFatigue,
            int currentMindset,
            int currentCoachTrust)
        {
            if (string.IsNullOrWhiteSpace(reasonId))
            {
                throw new ArgumentException("An action calculation reason ID is required.", nameof(reasonId));
            }

            if (currentAttributes == null)
            {
                throw new ArgumentNullException(nameof(currentAttributes));
            }

            ValidateStatus(currentFatigue, nameof(currentFatigue));
            ValidateStatus(currentMindset, nameof(currentMindset));
            ValidateStatus(currentCoachTrust, nameof(currentCoachTrust));

            var multiplier = PotentialMultiplier(potentialGrade);
            var definition = CareerWeekActionCatalogV1.Create().Find(contentId);
            if (definition == null || definition.Kind == CareerWeekActionKind.Match)
            {
                throw new ArgumentException(
                    "A selectable first-week action content ID is required.",
                    nameof(contentId));
            }

            var requested = new long[8];
            if (definition.Kind == CareerWeekActionKind.TeamPractice)
            {
                var amount = Scale(definition.BaseGrowthExperience, multiplier);
                for (var index = 0; index < requested.Length; index++)
                {
                    requested[index] = amount;
                }
            }
            else if (definition.Direction.HasValue)
            {
                requested[(int)definition.Direction.Value] =
                    Scale(definition.BaseGrowthExperience, multiplier);
            }

            var actual = new long[8];
            var next = new CareerAttributeProgress[8];
            for (var index = 0; index < next.Length; index++)
            {
                var current = currentAttributes.Get((CareerAttributeKind)index);
                var available = CareerAttributeProgress.MaximumGrowthExperience -
                                current.GrowthExperience;
                actual[index] = Math.Min(requested[index], available);
                next[index] = new CareerAttributeProgress(
                    current.AbilityBasisPoints,
                    current.GrowthExperience + actual[index]);
            }

            var requestedMindsetDelta = definition.MindsetDelta;
            if (definition.MindsetTarget.HasValue)
            {
                requestedMindsetDelta = Toward(
                    currentMindset,
                    definition.MindsetTarget.Value,
                    definition.MindsetMaximumStep);
            }

            return new CareerWeekActionCalculation(
                reasonId,
                definition.ContentId,
                new CareerPlayerAttributes(
                    next[0], next[1], next[2], next[3],
                    next[4], next[5], next[6], next[7]),
                new CareerAttributeGrowthDelta(
                    actual[0], actual[1], actual[2], actual[3],
                    actual[4], actual[5], actual[6], actual[7]),
                Change(currentFatigue, definition.FatigueDelta),
                Change(currentMindset, requestedMindsetDelta),
                Change(currentCoachTrust, definition.CoachTrustDelta));
        }

        public static int PotentialMultiplier(PotentialGrade grade)
        {
            switch (grade)
            {
                case PotentialGrade.D: return 8000;
                case PotentialGrade.C: return 9000;
                case PotentialGrade.B: return 10000;
                case PotentialGrade.A: return 11000;
                case PotentialGrade.S: return 12000;
                default:
                    throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
            }
        }

        private static long Scale(int baseAmount, int multiplierBasisPoints)
        {
            checked
            {
                return (long)baseAmount * multiplierBasisPoints / 10000L;
            }
        }

        private static CareerIntegerChange Change(int oldValue, int requestedDelta)
        {
            var next = Clamp(oldValue + requestedDelta, 0, 100);
            return new CareerIntegerChange(oldValue, next - oldValue, next);
        }

        private static int Toward(int value, int target, int maximumStep)
        {
            if (value < target)
            {
                return Math.Min(maximumStep, target - value);
            }

            return value > target ? -Math.Min(maximumStep, value - target) : 0;
        }

        private static void ValidateStatus(int value, string parameterName)
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Status must be in [0, 100].");
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
