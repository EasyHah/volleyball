using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Domain
{
    public sealed class CareerPlayerRecord
    {
        public CareerPlayerRecord(
            PlayerId playerId,
            string displayName,
            PlayerAbilitySnapshotV1 ability)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A career player requires a display name.", nameof(displayName));
            }

            PlayerId = playerId;
            DisplayName = displayName;
            Ability = ability ?? throw new ArgumentNullException(nameof(ability));
        }

        public PlayerId PlayerId { get; }

        public string DisplayName { get; }

        public PlayerAbilitySnapshotV1 Ability { get; }
    }
}
