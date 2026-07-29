using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Career.Domain
{
    public sealed class CareerEventRollRangeDefinition
    {
        public CareerEventRollRangeDefinition(
            int minimumRollInclusive,
            int maximumRollInclusive,
            CareerAttributeGrowthDelta baseGrowthExperienceDelta,
            int fatigueDelta,
            int mindsetDelta,
            int coachTrustDelta)
        {
            if (minimumRollInclusive < 0 || maximumRollInclusive > 9999 ||
                minimumRollInclusive > maximumRollInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumRollInclusive));
            }

            MinimumRollInclusive = minimumRollInclusive;
            MaximumRollInclusive = maximumRollInclusive;
            BaseGrowthExperienceDelta =
                (baseGrowthExperienceDelta ?? throw new ArgumentNullException(nameof(baseGrowthExperienceDelta))).Copy();
            FatigueDelta = CareerSaveModelGuard.InclusiveRange(fatigueDelta, -100, 100, nameof(fatigueDelta));
            MindsetDelta = CareerSaveModelGuard.InclusiveRange(mindsetDelta, -100, 100, nameof(mindsetDelta));
            CoachTrustDelta = CareerSaveModelGuard.InclusiveRange(coachTrustDelta, -100, 100, nameof(coachTrustDelta));
        }

        public int MinimumRollInclusive { get; }
        public int MaximumRollInclusive { get; }
        public CareerAttributeGrowthDelta BaseGrowthExperienceDelta { get; }
        public int FatigueDelta { get; }
        public int MindsetDelta { get; }
        public int CoachTrustDelta { get; }

        internal CareerEventRollRangeDefinition Copy()
        {
            return new CareerEventRollRangeDefinition(
                MinimumRollInclusive,
                MaximumRollInclusive,
                BaseGrowthExperienceDelta,
                FatigueDelta,
                MindsetDelta,
                CoachTrustDelta);
        }
    }

    public sealed class CareerEventOptionDefinition
    {
        private readonly CareerEventRollRangeDefinition[] _ranges;
        private readonly ReadOnlyCollection<CareerEventRollRangeDefinition> _readOnlyRanges;

        public CareerEventOptionDefinition(
            string optionId,
            IEnumerable<CareerEventRollRangeDefinition> ranges)
        {
            OptionId = CareerSaveModelGuard.BusinessId(optionId, nameof(optionId));
            if (ranges == null)
            {
                throw new ArgumentNullException(nameof(ranges));
            }

            var copied = new List<CareerEventRollRangeDefinition>();
            foreach (var range in ranges)
            {
                if (range == null)
                {
                    throw new ArgumentException("Event ranges cannot contain null.", nameof(ranges));
                }

                copied.Add(range.Copy());
            }

            if (copied.Count == 0)
            {
                throw new ArgumentException("An event option requires roll ranges.", nameof(ranges));
            }

            _ranges = copied.ToArray();
            _readOnlyRanges = Array.AsReadOnly(_ranges);
        }

        public string OptionId { get; }
        public IReadOnlyList<CareerEventRollRangeDefinition> Ranges => _readOnlyRanges;

        internal CareerEventOptionDefinition Copy()
        {
            return new CareerEventOptionDefinition(OptionId, _ranges);
        }
    }

    public sealed class CareerSocialEventDefinition
    {
        private readonly CareerEventOptionDefinition[] _options;
        private readonly ReadOnlyCollection<CareerEventOptionDefinition> _readOnlyOptions;

        public CareerSocialEventDefinition(
            string eventId,
            IEnumerable<CareerEventOptionDefinition> options)
        {
            EventId = CareerSaveModelGuard.BusinessId(eventId, nameof(eventId));
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var copied = new List<CareerEventOptionDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in options)
            {
                if (option == null || !ids.Add(option.OptionId))
                {
                    throw new ArgumentException(
                        "Event options must be non-null and globally unique within the event.",
                        nameof(options));
                }

                copied.Add(option.Copy());
            }

            _options = copied.ToArray();
            _readOnlyOptions = Array.AsReadOnly(_options);
        }

        public string EventId { get; }
        public IReadOnlyList<CareerEventOptionDefinition> Options => _readOnlyOptions;

        internal CareerSocialEventDefinition Copy()
        {
            return new CareerSocialEventDefinition(EventId, _options);
        }
    }

    public sealed class CareerSocialEventCatalog
    {
        private readonly CareerSocialEventDefinition[] _events;
        private readonly ReadOnlyCollection<CareerSocialEventDefinition> _readOnlyEvents;

        public CareerSocialEventCatalog(
            int contentVersion,
            int rulesetVersion,
            IEnumerable<CareerSocialEventDefinition> events)
        {
            if (contentVersion != 1 || rulesetVersion != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(contentVersion), "Only social-event V1 is supported.");
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            var copied = new List<CareerSocialEventDefinition>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in events)
            {
                if (definition == null || !eventIds.Add(definition.EventId))
                {
                    throw new ArgumentException("Event IDs must be non-null and unique.", nameof(events));
                }

                for (var index = 0; index < definition.Options.Count; index++)
                {
                    if (!optionIds.Add(definition.Options[index].OptionId))
                    {
                        throw new ArgumentException("Event option IDs must be globally unique.", nameof(events));
                    }
                }

                copied.Add(definition.Copy());
            }

            ValidateV1Shape(copied);
            ContentVersion = contentVersion;
            RulesetVersion = rulesetVersion;
            _events = copied.ToArray();
            _readOnlyEvents = Array.AsReadOnly(_events);
        }

        public int ContentVersion { get; }
        public int RulesetVersion { get; }
        public IReadOnlyList<CareerSocialEventDefinition> Events => _readOnlyEvents;

        private static void ValidateV1Shape(IReadOnlyList<CareerSocialEventDefinition> events)
        {
            var expected = CareerSocialEventCatalogV1.CreateDefinitions();
            if (events.Count != expected.Length)
            {
                throw new ArgumentException("Social-event V1 requires exactly one event.", nameof(events));
            }

            for (var eventIndex = 0; eventIndex < expected.Length; eventIndex++)
            {
                var actualEvent = events[eventIndex];
                var expectedEvent = expected[eventIndex];
                if (!string.Equals(actualEvent.EventId, expectedEvent.EventId, StringComparison.Ordinal) ||
                    actualEvent.Options.Count != expectedEvent.Options.Count)
                {
                    throw Drift(nameof(events));
                }

                for (var optionIndex = 0; optionIndex < expectedEvent.Options.Count; optionIndex++)
                {
                    var actualOption = actualEvent.Options[optionIndex];
                    var expectedOption = expectedEvent.Options[optionIndex];
                    if (!string.Equals(actualOption.OptionId, expectedOption.OptionId, StringComparison.Ordinal) ||
                        actualOption.Ranges.Count != expectedOption.Ranges.Count)
                    {
                        throw Drift(nameof(events));
                    }

                    for (var rangeIndex = 0; rangeIndex < expectedOption.Ranges.Count; rangeIndex++)
                    {
                        if (!Same(actualOption.Ranges[rangeIndex], expectedOption.Ranges[rangeIndex]))
                        {
                            throw Drift(nameof(events));
                        }
                    }
                }
            }
        }

        private static bool Same(
            CareerEventRollRangeDefinition left,
            CareerEventRollRangeDefinition right)
        {
            return left.MinimumRollInclusive == right.MinimumRollInclusive &&
                   left.MaximumRollInclusive == right.MaximumRollInclusive &&
                   left.FatigueDelta == right.FatigueDelta &&
                   left.MindsetDelta == right.MindsetDelta &&
                   left.CoachTrustDelta == right.CoachTrustDelta &&
                   left.BaseGrowthExperienceDelta.Spike == right.BaseGrowthExperienceDelta.Spike &&
                   left.BaseGrowthExperienceDelta.Serve == right.BaseGrowthExperienceDelta.Serve &&
                   left.BaseGrowthExperienceDelta.Reception == right.BaseGrowthExperienceDelta.Reception &&
                   left.BaseGrowthExperienceDelta.Defense == right.BaseGrowthExperienceDelta.Defense &&
                   left.BaseGrowthExperienceDelta.Block == right.BaseGrowthExperienceDelta.Block &&
                   left.BaseGrowthExperienceDelta.Movement == right.BaseGrowthExperienceDelta.Movement &&
                   left.BaseGrowthExperienceDelta.Jump == right.BaseGrowthExperienceDelta.Jump &&
                   left.BaseGrowthExperienceDelta.Stamina == right.BaseGrowthExperienceDelta.Stamina;
        }

        private static ArgumentException Drift(string parameterName)
        {
            return new ArgumentException(
                "Social-event V1 identity, order, ranges, and effects are closed.",
                parameterName);
        }
    }

    public static class CareerSocialEventCatalogV1
    {
        public static CareerSocialEventCatalog Create()
        {
            return new CareerSocialEventCatalog(1, 1, CreateDefinitions());
        }

        internal static CareerSocialEventDefinition[] CreateDefinitions()
        {
            return new[]
            {
                new CareerSocialEventDefinition(
                    "event.team_meal",
                    new[]
                    {
                        new CareerEventOptionDefinition(
                            "event.team_meal.option.attend",
                            new[]
                            {
                                Range(0, 4999, 0, 2, 8, 2),
                                Range(5000, 9999, 0, 4, 6, 3)
                            }),
                        new CareerEventOptionDefinition(
                            "event.team_meal.option.extra_practice",
                            new[]
                            {
                                Range(0, 4999, 60, 8, 1, 5),
                                Range(5000, 9999, 80, 10, -2, 6)
                            })
                    })
            };
        }

        private static CareerEventRollRangeDefinition Range(
            int minimum,
            int maximum,
            long spikeGrowth,
            int fatigue,
            int mindset,
            int trust)
        {
            return new CareerEventRollRangeDefinition(
                minimum,
                maximum,
                new CareerAttributeGrowthDelta(spikeGrowth, 0, 0, 0, 0, 0, 0, 0),
                fatigue,
                mindset,
                trust);
        }
    }
}
