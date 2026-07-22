using System;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public static class CareerEventRulesV1
    {
        public static CareerEventOptionEffect Resolve(
            int contentVersion,
            int rulesetVersion,
            string eventId,
            string optionId,
            int roll,
            PotentialGrade potentialGrade,
            CareerPlayerAttributes currentAttributes,
            int currentFatigue,
            int currentMindset,
            int currentCoachTrust)
        {
            if (contentVersion != 1 || rulesetVersion != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(contentVersion), "Only social-event V1 is supported.");
            }

            if (roll < 0 || roll > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(roll), roll, "Event roll must be in [0, 9999].");
            }

            if (currentAttributes == null)
            {
                throw new ArgumentNullException(nameof(currentAttributes));
            }

            ValidateStatus(currentFatigue, nameof(currentFatigue));
            ValidateStatus(currentMindset, nameof(currentMindset));
            ValidateStatus(currentCoachTrust, nameof(currentCoachTrust));
            var multiplier = CareerWeekActionRulesV1.PotentialMultiplier(potentialGrade);
            var catalog = CareerSocialEventCatalogV1.Create();
            CareerSocialEventDefinition selectedEvent = null;
            for (var index = 0; index < catalog.Events.Count; index++)
            {
                if (string.Equals(catalog.Events[index].EventId, eventId, StringComparison.Ordinal))
                {
                    selectedEvent = catalog.Events[index];
                    break;
                }
            }

            if (selectedEvent == null)
            {
                throw new ArgumentException("Unknown social-event V1 ID.", nameof(eventId));
            }

            CareerEventOptionDefinition selectedOption = null;
            for (var index = 0; index < selectedEvent.Options.Count; index++)
            {
                if (string.Equals(selectedEvent.Options[index].OptionId, optionId, StringComparison.Ordinal))
                {
                    selectedOption = selectedEvent.Options[index];
                    break;
                }
            }

            if (selectedOption == null)
            {
                throw new ArgumentException("Unknown social-event V1 option ID.", nameof(optionId));
            }

            CareerEventRollRangeDefinition range = null;
            for (var index = 0; index < selectedOption.Ranges.Count; index++)
            {
                var candidate = selectedOption.Ranges[index];
                if (roll >= candidate.MinimumRollInclusive && roll <= candidate.MaximumRollInclusive)
                {
                    range = candidate;
                    break;
                }
            }

            if (range == null)
            {
                throw new InvalidOperationException("The closed event option does not cover the supplied roll.");
            }

            var actual = new long[8];
            for (var index = 0; index < actual.Length; index++)
            {
                var baseAmount = range.BaseGrowthExperienceDelta.Get((CareerTrainingDirection)index);
                long requested;
                checked
                {
                    requested = baseAmount * multiplier / 10000L;
                }

                var progress = currentAttributes.Get((CareerAttributeKind)index);
                actual[index] = Math.Min(
                    requested,
                    CareerAttributeProgress.MaximumGrowthExperience - progress.GrowthExperience);
            }

            return new CareerEventOptionEffect(
                optionId,
                new CareerAttributeGrowthDelta(
                    actual[0], actual[1], actual[2], actual[3],
                    actual[4], actual[5], actual[6], actual[7]),
                ActualDelta(currentFatigue, range.FatigueDelta),
                ActualDelta(currentMindset, range.MindsetDelta),
                ActualDelta(currentCoachTrust, range.CoachTrustDelta));
        }

        private static int ActualDelta(int current, int requested)
        {
            var next = Math.Max(0, Math.Min(100, current + requested));
            return next - current;
        }

        private static void ValidateStatus(int value, string parameterName)
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Status must be in [0, 100].");
            }
        }
    }
}
