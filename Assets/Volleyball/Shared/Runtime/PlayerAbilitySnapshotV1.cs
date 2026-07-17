using System;
using System.Runtime.Serialization;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class PlayerAbilitySnapshotV1 : IEquatable<PlayerAbilitySnapshotV1>
    {
        [DataMember(Name = "mobility", Order = 1)]
        private float _mobility;

        [DataMember(Name = "reaction", Order = 2)]
        private float _reaction;

        [DataMember(Name = "jump", Order = 3)]
        private float _jump;

        [DataMember(Name = "receiveTechnique", Order = 4)]
        private float _receiveTechnique;

        [DataMember(Name = "setTechnique", Order = 5)]
        private float _setTechnique;

        [DataMember(Name = "attackTechnique", Order = 6)]
        private float _attackTechnique;

        [DataMember(Name = "attackPower", Order = 7)]
        private float _attackPower;

        public PlayerAbilitySnapshotV1(
            float mobility,
            float reaction,
            float jump,
            float receiveTechnique,
            float setTechnique,
            float attackTechnique,
            float attackPower)
        {
            _mobility = ContractGuard.Unit(mobility, nameof(mobility));
            _reaction = ContractGuard.Unit(reaction, nameof(reaction));
            _jump = ContractGuard.Unit(jump, nameof(jump));
            _receiveTechnique = ContractGuard.Unit(receiveTechnique, nameof(receiveTechnique));
            _setTechnique = ContractGuard.Unit(setTechnique, nameof(setTechnique));
            _attackTechnique = ContractGuard.Unit(attackTechnique, nameof(attackTechnique));
            _attackPower = ContractGuard.Unit(attackPower, nameof(attackPower));
        }

        public float Mobility => _mobility;

        public float Reaction => _reaction;

        public float Jump => _jump;

        public float ReceiveTechnique => _receiveTechnique;

        public float SetTechnique => _setTechnique;

        public float AttackTechnique => _attackTechnique;

        public float AttackPower => _attackPower;

        public bool Equals(PlayerAbilitySnapshotV1 other)
        {
            return other != null &&
                   _mobility.Equals(other._mobility) &&
                   _reaction.Equals(other._reaction) &&
                   _jump.Equals(other._jump) &&
                   _receiveTechnique.Equals(other._receiveTechnique) &&
                   _setTechnique.Equals(other._setTechnique) &&
                   _attackTechnique.Equals(other._attackTechnique) &&
                   _attackPower.Equals(other._attackPower);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerAbilitySnapshotV1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _mobility.GetHashCode();
                hash = (hash * 397) ^ _reaction.GetHashCode();
                hash = (hash * 397) ^ _jump.GetHashCode();
                hash = (hash * 397) ^ _receiveTechnique.GetHashCode();
                hash = (hash * 397) ^ _setTechnique.GetHashCode();
                hash = (hash * 397) ^ _attackTechnique.GetHashCode();
                return (hash * 397) ^ _attackPower.GetHashCode();
            }
        }

        internal void Validate()
        {
            ContractGuard.Unit(_mobility, nameof(Mobility));
            ContractGuard.Unit(_reaction, nameof(Reaction));
            ContractGuard.Unit(_jump, nameof(Jump));
            ContractGuard.Unit(_receiveTechnique, nameof(ReceiveTechnique));
            ContractGuard.Unit(_setTechnique, nameof(SetTechnique));
            ContractGuard.Unit(_attackTechnique, nameof(AttackTechnique));
            ContractGuard.Unit(_attackPower, nameof(AttackPower));
        }
    }
}
