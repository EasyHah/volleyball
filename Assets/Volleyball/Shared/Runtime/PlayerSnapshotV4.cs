using System;

namespace Volleyball.Shared.Contracts
{
    public sealed class PlayerSnapshotV4
    {
        public PlayerSnapshotV4(
            PlayerId playerId,
            string displayName,
            int jerseyNumber,
            PlayerPosition position,
            DominantHandV4 dominantHand,
            PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical,
            MatchAttributeDerivationConfigV4 derivationConfig)
            : this(
                playerId,
                displayName,
                jerseyNumber,
                position,
                dominantHand,
                physical,
                technical,
                derivationConfig,
                suppliedDerived: null)
        {
        }

        public PlayerSnapshotV4(
            PlayerId playerId,
            string displayName,
            int jerseyNumber,
            PlayerPosition position,
            DominantHandV4 dominantHand,
            PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical,
            MatchAttributeDerivationConfigV4 derivationConfig,
            DerivedMatchAttributesV4 suppliedDerived)
        {
            PlayerId = new PlayerId(ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            DisplayName = ContractGuard.RequiredText(displayName, nameof(displayName), 100);
            if (jerseyNumber < 0 || jerseyNumber > 99)
            {
                throw new ContractValidationException("jerseyNumber must be in the range [0, 99].");
            }

            ContractGuard.DefinedEnum(position, nameof(position));
            ContractGuard.DefinedEnum(dominantHand, nameof(dominantHand));
            Physical = physical ?? throw new ContractValidationException("physical is required.");
            Technical = technical ?? throw new ContractValidationException("technical is required.");
            if (derivationConfig == null)
            {
                throw new ContractValidationException("derivationConfig is required.");
            }

            ValidatePublishedConfig(derivationConfig);
            JerseyNumber = jerseyNumber;
            Position = position;
            DominantHand = dominantHand;
            var recomputed = MatchAttributeDerivationV4.Derive(
                Physical,
                Technical,
                DominantHand,
                derivationConfig);
            if (suppliedDerived != null &&
                (suppliedDerived.FormulaVersion != recomputed.FormulaVersion ||
                 suppliedDerived.CoefficientVersion != recomputed.CoefficientVersion ||
                 !string.Equals(
                     suppliedDerived.InputFingerprint,
                     recomputed.InputFingerprint,
                     StringComparison.Ordinal) ||
                 !string.Equals(
                     suppliedDerived.ResultFingerprint,
                     recomputed.ResultFingerprint,
                     StringComparison.Ordinal)))
            {
                throw new ContractValidationException(
                    "The supplied derived fingerprint does not match the recomputed V4 derivation.");
            }

            Derived = recomputed;
        }

        public PlayerId PlayerId { get; }
        public string DisplayName { get; }
        public int JerseyNumber { get; }
        public PlayerPosition Position { get; }
        public DominantHandV4 DominantHand { get; }
        public PhysicalBaseAttributesV4 Physical { get; }
        public TechnicalBaseAttributesV4 Technical { get; }
        public DerivedMatchAttributesV4 Derived { get; }

        internal void Validate()
        {
            ContractGuard.RequiredId(PlayerId.Value, nameof(PlayerId));
            ContractGuard.RequiredText(DisplayName, nameof(DisplayName), 100);
            if (JerseyNumber < 0 || JerseyNumber > 99)
            {
                throw new ContractValidationException("JerseyNumber must be in the range [0, 99].");
            }

            ContractGuard.DefinedEnum(Position, nameof(Position));
            ContractGuard.DefinedEnum(DominantHand, nameof(DominantHand));
            if (Physical == null || Technical == null || Derived == null)
            {
                throw new ContractValidationException(
                    "Physical, technical, and derived V4 attributes are required.");
            }

            if (Derived.FormulaVersion != 1 || Derived.CoefficientVersion != 1)
            {
                throw new ContractValidationException(
                    "Derived attributes must use the published V1 formula and coefficient table.");
            }

            var recomputed = MatchAttributeDerivationV4.Derive(
                Physical,
                Technical,
                DominantHand,
                MatchAttributeDerivationConfigV4.Version1);
            if (!string.Equals(
                    Derived.InputFingerprint,
                    recomputed.InputFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    Derived.ResultFingerprint,
                    recomputed.ResultFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "The derived fingerprint does not match the V4 player base attributes.");
            }
        }

        private static void ValidatePublishedConfig(
            MatchAttributeDerivationConfigV4 derivationConfig)
        {
            var published = MatchAttributeDerivationConfigV4.Version1;
            if (derivationConfig.FormulaVersion != published.FormulaVersion ||
                derivationConfig.CoefficientVersion != published.CoefficientVersion ||
                derivationConfig.Coefficients.Count != published.Coefficients.Count)
            {
                throw new ContractValidationException(
                    "derivationConfig must use the published V1 formula and coefficient table.");
            }

            for (var index = 0; index < published.Coefficients.Count; index++)
            {
                if (!derivationConfig.Coefficients[index].Equals(
                        published.Coefficients[index]))
                {
                    throw new ContractValidationException(
                        "derivationConfig must use the published V1 formula and coefficient table.");
                }
            }
        }
    }
}
