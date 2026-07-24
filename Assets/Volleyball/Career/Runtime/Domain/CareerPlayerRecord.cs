using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Domain
{
    public sealed class CareerPlayerRecord
    {
        public CareerPlayerRecord(
            string playerId,
            PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical,
            DominantHandV4 dominantHand)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("A career player requires an ID.", nameof(playerId));
            }

            PlayerId = playerId;
            Physical = physical ?? throw new ArgumentNullException(nameof(physical));
            Technical = technical ?? throw new ArgumentNullException(nameof(technical));
            if (!Enum.IsDefined(typeof(DominantHandV4), dominantHand))
            {
                throw new ArgumentOutOfRangeException(nameof(dominantHand));
            }

            DominantHand = dominantHand;
        }

        public string PlayerId { get; }
        public PhysicalBaseAttributesV4 Physical { get; }
        public TechnicalBaseAttributesV4 Technical { get; }
        public DominantHandV4 DominantHand { get; }
    }
}
