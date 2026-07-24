using System;

namespace Volleyball.Shared.Contracts
{
    public sealed class TechnicalBaseAttributesV4 : IEquatable<TechnicalBaseAttributesV4>
    {
        public TechnicalBaseAttributesV4(
            float attackTechnique,
            float attackPower,
            float blockTechnique,
            float defenseTechnique,
            float receiveTechnique,
            float setTechnique,
            float serveTechnique,
            float softTouch,
            float courtAwareness)
        {
            AttackTechnique = ContractGuard.Unit(attackTechnique, nameof(attackTechnique));
            AttackPower = ContractGuard.Unit(attackPower, nameof(attackPower));
            BlockTechnique = ContractGuard.Unit(blockTechnique, nameof(blockTechnique));
            DefenseTechnique = ContractGuard.Unit(defenseTechnique, nameof(defenseTechnique));
            ReceiveTechnique = ContractGuard.Unit(receiveTechnique, nameof(receiveTechnique));
            SetTechnique = ContractGuard.Unit(setTechnique, nameof(setTechnique));
            ServeTechnique = ContractGuard.Unit(serveTechnique, nameof(serveTechnique));
            SoftTouch = ContractGuard.Unit(softTouch, nameof(softTouch));
            CourtAwareness = ContractGuard.Unit(courtAwareness, nameof(courtAwareness));
        }

        public float AttackTechnique { get; }
        public float AttackPower { get; }
        public float BlockTechnique { get; }
        public float DefenseTechnique { get; }
        public float ReceiveTechnique { get; }
        public float SetTechnique { get; }
        public float ServeTechnique { get; }
        public float SoftTouch { get; }
        public float CourtAwareness { get; }

        public bool Equals(TechnicalBaseAttributesV4 other)
        {
            return other != null &&
                AttackTechnique.Equals(other.AttackTechnique) &&
                AttackPower.Equals(other.AttackPower) &&
                BlockTechnique.Equals(other.BlockTechnique) &&
                DefenseTechnique.Equals(other.DefenseTechnique) &&
                ReceiveTechnique.Equals(other.ReceiveTechnique) &&
                SetTechnique.Equals(other.SetTechnique) &&
                ServeTechnique.Equals(other.ServeTechnique) &&
                SoftTouch.Equals(other.SoftTouch) &&
                CourtAwareness.Equals(other.CourtAwareness);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TechnicalBaseAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = AttackTechnique.GetHashCode();
                hash = (hash * 397) ^ AttackPower.GetHashCode();
                hash = (hash * 397) ^ BlockTechnique.GetHashCode();
                hash = (hash * 397) ^ DefenseTechnique.GetHashCode();
                hash = (hash * 397) ^ ReceiveTechnique.GetHashCode();
                hash = (hash * 397) ^ SetTechnique.GetHashCode();
                hash = (hash * 397) ^ ServeTechnique.GetHashCode();
                hash = (hash * 397) ^ SoftTouch.GetHashCode();
                hash = (hash * 397) ^ CourtAwareness.GetHashCode();
                return hash;
            }
        }
    }
}
