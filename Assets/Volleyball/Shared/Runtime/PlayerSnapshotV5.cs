using System;

namespace Volleyball.Shared.Contracts
{
    public sealed class PlayerSnapshotV5
    {
        public PlayerSnapshotV5(
            PlayerId playerId,
            string displayName,
            int jerseyNumber,
            PlayerPosition position,
            DominantHandV5 dominantHand,
            CareerBaseAttributesV5 bases,
            DerivedMatchAttributesV5 suppliedDerived = null)
        {
            PlayerId = new PlayerId(ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            DisplayName = ContractGuard.RequiredText(displayName, nameof(displayName), 100);
            if (jerseyNumber < 0 || jerseyNumber > 99)
            {
                throw new ContractValidationException("jerseyNumber must be in the range [0, 99].");
            }

            ContractGuard.DefinedEnum(position, nameof(position));
            ContractGuard.DefinedEnum(dominantHand, nameof(dominantHand));
            Bases = bases ?? throw new ContractValidationException("bases are required.");
            var recomputed = MatchAttributeDerivationV5.Derive(Bases, dominantHand);
            if (suppliedDerived != null &&
                !string.Equals(suppliedDerived.ResultFingerprint, recomputed.ResultFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "The supplied derived fingerprint does not match the V5 player base attributes.");
            }

            JerseyNumber = jerseyNumber;
            Position = position;
            DominantHand = dominantHand;
            Derived = recomputed;
        }

        public PlayerId PlayerId { get; }
        public string DisplayName { get; }
        public int JerseyNumber { get; }
        public PlayerPosition Position { get; }
        public DominantHandV5 DominantHand { get; }
        public CareerBaseAttributesV5 Bases { get; }
        public DerivedMatchAttributesV5 Derived { get; }

        internal void Validate()
        {
            var recomputed = MatchAttributeDerivationV5.Derive(Bases, DominantHand);
            if (!string.Equals(Derived.ResultFingerprint, recomputed.ResultFingerprint,
                StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "The V5 derived fingerprint does not match the player base attributes.");
            }
        }
    }
}
