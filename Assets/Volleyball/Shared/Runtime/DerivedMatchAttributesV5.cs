using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Shared.Contracts
{
    public sealed class DerivedMatchAttributesV5 : IEquatable<DerivedMatchAttributesV5>
    {
        internal DerivedMatchAttributesV5(
            int attackControl,
            int attackPower,
            int attackReachMillimeters,
            int blockControl,
            int blockReachMillimeters,
            int defenseControl,
            int receiveControl,
            int setControl,
            int serveControl,
            int jump,
            int movement,
            int reaction,
            int courtIq,
            DominantHandV5 dominantHand,
            int formulaVersion,
            int coefficientVersion,
            string inputFingerprint,
            string resultFingerprint,
            IReadOnlyList<MatchAttributeExplanationV5> explanations)
        {
            AttackControl = CareerBaseAttributesV5.BasisPoints(attackControl, nameof(attackControl));
            AttackPower = CareerBaseAttributesV5.BasisPoints(attackPower, nameof(attackPower));
            AttackReachMillimeters = GeometryMillimeters(attackReachMillimeters, nameof(attackReachMillimeters));
            BlockControl = CareerBaseAttributesV5.BasisPoints(blockControl, nameof(blockControl));
            BlockReachMillimeters = GeometryMillimeters(blockReachMillimeters, nameof(blockReachMillimeters));
            DefenseControl = CareerBaseAttributesV5.BasisPoints(defenseControl, nameof(defenseControl));
            ReceiveControl = CareerBaseAttributesV5.BasisPoints(receiveControl, nameof(receiveControl));
            SetControl = CareerBaseAttributesV5.BasisPoints(setControl, nameof(setControl));
            ServeControl = CareerBaseAttributesV5.BasisPoints(serveControl, nameof(serveControl));
            Jump = CareerBaseAttributesV5.BasisPoints(jump, nameof(jump));
            Movement = CareerBaseAttributesV5.BasisPoints(movement, nameof(movement));
            Reaction = CareerBaseAttributesV5.BasisPoints(reaction, nameof(reaction));
            CourtIq = CareerBaseAttributesV5.BasisPoints(courtIq, nameof(courtIq));
            ContractGuard.DefinedEnum(dominantHand, nameof(dominantHand));
            if (formulaVersion <= 0 || coefficientVersion <= 0)
            {
                throw new ContractValidationException("V5 derivation versions must be positive.");
            }

            ContractGuard.Hash(inputFingerprint, nameof(inputFingerprint));
            ContractGuard.Hash(resultFingerprint, nameof(resultFingerprint));
            if (explanations == null || explanations.Count != 10)
            {
                throw new ContractValidationException(
                    "V5 derived attributes require ten formal consumption explanations.");
            }
            DominantHand = dominantHand;
            FormulaVersion = formulaVersion;
            CoefficientVersion = coefficientVersion;
            InputFingerprint = inputFingerprint;
            ResultFingerprint = resultFingerprint;
            Explanations = new ReadOnlyCollection<MatchAttributeExplanationV5>(
                new List<MatchAttributeExplanationV5>(explanations));
        }

        public int AttackControl { get; }
        public int AttackPower { get; }
        public int AttackReachMillimeters { get; }
        public int BlockControl { get; }
        public int BlockReachMillimeters { get; }
        public int DefenseControl { get; }
        public int ReceiveControl { get; }
        public int SetControl { get; }
        public int ServeControl { get; }
        public int Jump { get; }
        public int Movement { get; }
        public int Reaction { get; }
        public int CourtIq { get; }
        public DominantHandV5 DominantHand { get; }
        public int FormulaVersion { get; }
        public int CoefficientVersion { get; }
        public string InputFingerprint { get; }
        public string ResultFingerprint { get; }
        public IReadOnlyList<MatchAttributeExplanationV5> Explanations { get; }

        public bool Equals(DerivedMatchAttributesV5 other)
        {
            return other != null && string.Equals(ResultFingerprint, other.ResultFingerprint, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as DerivedMatchAttributesV5);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ResultFingerprint);

        private static int GeometryMillimeters(int value, string name)
        {
            if (value < CareerBaseAttributesV5.MinimumHeightMillimeters || value > 4000)
            {
                throw new ContractValidationException(
                    name + " must be in the range [1400, 4000].");
            }

            return value;
        }
    }
}
