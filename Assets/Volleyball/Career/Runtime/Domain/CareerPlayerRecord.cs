using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Domain
{
    public sealed class CareerPlayerRecord
    {
        public CareerPlayerRecord(
            PlayerId playerId,
            string displayName,
            int jerseyNumber,
            CareerPlayerAttributes attributes)
        {
            if (string.IsNullOrWhiteSpace(playerId.Value))
            {
                throw new ArgumentException("A career player requires a stable player ID.", nameof(playerId));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A career player requires a display name.", nameof(displayName));
            }

            if (jerseyNumber < 1 || jerseyNumber > 99)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(jerseyNumber),
                    jerseyNumber,
                    "A career player jersey number must be in the range [1, 99].");
            }

            PlayerId = playerId;
            DisplayName = displayName;
            JerseyNumber = jerseyNumber;
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }

        public PlayerId PlayerId { get; }

        public string DisplayName { get; }

        public int JerseyNumber { get; }

        public CareerPlayerAttributes Attributes { get; }
    }
}
