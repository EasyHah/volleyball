using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    // Scenario-private Match test values. This type must never cross into Career or V5 contracts.
    public sealed class TrainingPlayerAttributeOverrideV1
    {
        public TrainingPlayerAttributeOverrideV1(int heightMillimeters,
            DominantHandV4 dominantHand, PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical)
        {
            if (heightMillimeters < 1400 || heightMillimeters > 2300 ||
                !Enum.IsDefined(typeof(DominantHandV4), dominantHand))
                throw new ArgumentOutOfRangeException(nameof(heightMillimeters));
            HeightMillimeters = heightMillimeters;
            DominantHand = dominantHand;
            Physical = physical ?? throw new ArgumentNullException(nameof(physical));
            Technical = technical ?? throw new ArgumentNullException(nameof(technical));
        }

        public int HeightMillimeters { get; }
        public DominantHandV4 DominantHand { get; }
        public PhysicalBaseAttributesV4 Physical { get; }
        public TechnicalBaseAttributesV4 Technical { get; }
    }
}
