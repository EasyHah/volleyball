using System;

namespace Volleyball.Career.Domain
{
    public interface ICareerSeedSource
    {
        CareerSeed GenerateSeed();
    }

    public interface IDeterministicCareerRandom
    {
        long NextInt64(CareerRandomRequest request, long minInclusive, long maxExclusive);
    }

    public sealed class CareerRandomRequest
    {
        public CareerRandomRequest(
            int algorithmVersion,
            CareerSeed seed,
            string streamId,
            int season,
            int week,
            string entityStableId,
            OccurrenceId occurrenceId,
            long drawIndex)
        {
            if (algorithmVersion != CareerSaveVersions.CurrentCareerRandomAlgorithmVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(algorithmVersion),
                    algorithmVersion,
                    "Only career random algorithm V1 is supported.");
            }

            if (seed == null)
            {
                throw new ArgumentNullException(nameof(seed));
            }

            ValidateStreamAndCalendar(streamId, season, week);
            ValidateStrictText(entityStableId, nameof(entityStableId));
            CareerSaveModelGuard.StableId(occurrenceId.Value, nameof(occurrenceId));
            if (drawIndex < 0 || drawIndex > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(drawIndex),
                    drawIndex,
                    "A draw index must fit an unsigned 32-bit integer.");
            }

            AlgorithmVersion = algorithmVersion;
            Seed = new CareerSeed(seed.ToBytes());
            StreamId = streamId;
            Season = season;
            Week = week;
            EntityStableId = entityStableId;
            OccurrenceId = occurrenceId;
            DrawIndex = drawIndex;
        }

        public int AlgorithmVersion { get; }

        public CareerSeed Seed { get; }

        public string StreamId { get; }

        public int Season { get; }

        public int Week { get; }

        public string EntityStableId { get; }

        public OccurrenceId OccurrenceId { get; }

        public long DrawIndex { get; }

        private static void ValidateStreamAndCalendar(string streamId, int season, int week)
        {
            if (streamId != "tryout" && streamId != "event" && streamId != "match_seed")
            {
                throw new ArgumentException(
                    "The random stream must be one of tryout, event, or match_seed.",
                    nameof(streamId));
            }

            if (streamId == "tryout")
            {
                if (season != 1 || week != 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(week),
                        week,
                        "The tryout stream is fixed to season 1, week 0.");
                }

                return;
            }

            if (season < 1 || season > 6)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(season),
                    season,
                    "Season must be in the range [1, 6].");
            }

            if (week < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(week),
                    week,
                    "Event and match streams require a positive week.");
            }
        }

        private static void ValidateStrictText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty stable ID is required.", parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        throw new ArgumentException(
                            "Stable IDs cannot contain an unpaired Unicode surrogate.",
                            parameterName);
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(value[index]))
                {
                    throw new ArgumentException(
                        "Stable IDs cannot contain an unpaired Unicode surrogate.",
                        parameterName);
                }
            }
        }
    }
}
