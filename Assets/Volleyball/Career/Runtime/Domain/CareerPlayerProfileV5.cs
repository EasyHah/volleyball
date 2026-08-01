using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Domain
{
    /// <summary>
    /// Versioned Career identity and bases required for a new V5 match. This
    /// is not derivable from the historical eight-axis player record.
    /// </summary>
    public sealed class CareerPlayerProfileV5
    {
        public CareerPlayerProfileV5(PlayerId playerId, string displayName,
            int jerseyNumber, DominantHandV5 dominantHand,
            CareerBaseAttributesV5 bases, int fatigue = 0, int mindset = 50,
            int coachTrust = 50)
        {
            if (string.IsNullOrWhiteSpace(playerId.Value))
                throw new ArgumentException("A player ID is required.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A display name is required.", nameof(displayName));
            if (jerseyNumber < 1 || jerseyNumber > 99)
                throw new ArgumentOutOfRangeException(nameof(jerseyNumber));
            if (!Enum.IsDefined(typeof(DominantHandV5), dominantHand))
                throw new ArgumentOutOfRangeException(nameof(dominantHand));
            PlayerId = playerId;
            DisplayName = displayName;
            JerseyNumber = jerseyNumber;
            DominantHand = dominantHand;
            Bases = bases ?? throw new ArgumentNullException(nameof(bases));
            if (fatigue < 0 || fatigue > 100 || mindset < 0 || mindset > 100 || coachTrust < 0 || coachTrust > 100)
                throw new ArgumentOutOfRangeException("V5 profile state must be in [0, 100].");
            Fatigue = fatigue;
            Mindset = mindset;
            CoachTrust = coachTrust;
        }

        public PlayerId PlayerId { get; }
        public string DisplayName { get; }
        public int JerseyNumber { get; }
        public DominantHandV5 DominantHand { get; }
        public CareerBaseAttributesV5 Bases { get; }
        public int Fatigue { get; }
        public int Mindset { get; }
        public int CoachTrust { get; }
    }
}
