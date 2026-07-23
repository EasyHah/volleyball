using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class PlayerAbilitySnapshotV3 : IEquatable<PlayerAbilitySnapshotV3>
    {
        public const int CurrentMigrationVersion = 1;

        [DataMember(Name = "mobility", Order = 1)] private float _mobility;
        [DataMember(Name = "reaction", Order = 2)] private float _reaction;
        [DataMember(Name = "jump", Order = 3)] private float _jump;
        [DataMember(Name = "maxAttackReach", Order = 4)] private float _maxAttackReach;
        [DataMember(Name = "receiveTechnique", Order = 5)] private float _receiveTechnique;
        [DataMember(Name = "setTechnique", Order = 6)] private float _setTechnique;
        [DataMember(Name = "attackControl", Order = 7)] private float _attackControl;
        [DataMember(Name = "attackPower", Order = 8)] private float _attackPower;
        [DataMember(Name = "softTouch", Order = 9)] private float _softTouch;
        [DataMember(Name = "blockTechnique", Order = 10)] private float _blockTechnique;
        [DataMember(Name = "courtAwareness", Order = 11)] private float _courtAwareness;
        [DataMember(Name = "sourceVersion", Order = 12)] private int _sourceVersion;
        [DataMember(Name = "migrationVersion", Order = 13)] private int _migrationVersion;
        [DataMember(Name = "isCompatibilityEstimate", Order = 14)] private bool _isCompatibilityEstimate;
        [DataMember(Name = "compatibilityCollapsedAxes", Order = 15)] private string[] _compatibilityCollapsedAxes;

        public PlayerAbilitySnapshotV3(
            float mobility,
            float reaction,
            float jump,
            float maxAttackReach,
            float receiveTechnique,
            float setTechnique,
            float attackControl,
            float attackPower,
            float softTouch,
            float blockTechnique,
            float courtAwareness,
            int sourceVersion,
            int migrationVersion,
            bool isCompatibilityEstimate,
            string[] compatibilityCollapsedAxes)
        {
            _mobility = ContractGuard.Unit(mobility, nameof(mobility));
            _reaction = ContractGuard.Unit(reaction, nameof(reaction));
            _jump = ContractGuard.Unit(jump, nameof(jump));
            _maxAttackReach = ContractGuard.AttackReach(maxAttackReach, nameof(maxAttackReach));
            _receiveTechnique = ContractGuard.Unit(receiveTechnique, nameof(receiveTechnique));
            _setTechnique = ContractGuard.Unit(setTechnique, nameof(setTechnique));
            _attackControl = ContractGuard.Unit(attackControl, nameof(attackControl));
            _attackPower = ContractGuard.Unit(attackPower, nameof(attackPower));
            _softTouch = ContractGuard.Unit(softTouch, nameof(softTouch));
            _blockTechnique = ContractGuard.Unit(blockTechnique, nameof(blockTechnique));
            _courtAwareness = ContractGuard.Unit(courtAwareness, nameof(courtAwareness));
            _sourceVersion = sourceVersion;
            _migrationVersion = migrationVersion;
            _isCompatibilityEstimate = isCompatibilityEstimate;
            _compatibilityCollapsedAxes = CopyCollapsedAxes(compatibilityCollapsedAxes);
            Validate();
        }

        public float Mobility => _mobility;
        public float Reaction => _reaction;
        public float Jump => _jump;
        public float MaxAttackReach => _maxAttackReach;
        public float ReceiveTechnique => _receiveTechnique;
        public float SetTechnique => _setTechnique;
        public float AttackControl => _attackControl;
        public float AttackPower => _attackPower;
        public float SoftTouch => _softTouch;
        public float BlockTechnique => _blockTechnique;
        public float CourtAwareness => _courtAwareness;
        public int SourceVersion => _sourceVersion;
        public int MigrationVersion => _migrationVersion;
        public bool IsCompatibilityEstimate => _isCompatibilityEstimate;
        public IReadOnlyList<string> CompatibilityCollapsedAxes => Array.AsReadOnly(_compatibilityCollapsedAxes);

        public static PlayerAbilitySnapshotV3 LegacyV2ToPlayerAbilitySnapshotV3(
            PlayerAbilitySnapshotV2 source,
            PlayerPosition position)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source.Validate();
            ContractGuard.DefinedEnum(position, nameof(position));

            var attackControl = Clamp01(source.AttackTechnique + AttackControlRoleOffset(position));
            var softTouch = Clamp01(source.AttackTechnique + SoftTouchRoleOffset(position));
            var blockTechnique = Clamp01((source.Jump * 0.6f) + (source.ReceiveTechnique * 0.4f));
            var courtAwareness = Clamp01((source.Reaction * 0.7f) + (source.SetTechnique * 0.3f));

            return new PlayerAbilitySnapshotV3(
                source.Mobility,
                source.Reaction,
                source.Jump,
                source.MaxAttackReach,
                source.ReceiveTechnique,
                source.SetTechnique,
                attackControl,
                source.AttackPower,
                softTouch,
                blockTechnique,
                courtAwareness,
                ContractVersions.MatchV2,
                CurrentMigrationVersion,
                true,
                Array.Empty<string>());
        }

        internal void Validate()
        {
            ContractGuard.Unit(_mobility, nameof(Mobility));
            ContractGuard.Unit(_reaction, nameof(Reaction));
            ContractGuard.Unit(_jump, nameof(Jump));
            ContractGuard.AttackReach(_maxAttackReach, nameof(MaxAttackReach));
            ContractGuard.Unit(_receiveTechnique, nameof(ReceiveTechnique));
            ContractGuard.Unit(_setTechnique, nameof(SetTechnique));
            ContractGuard.Unit(_attackControl, nameof(AttackControl));
            ContractGuard.Unit(_attackPower, nameof(AttackPower));
            ContractGuard.Unit(_softTouch, nameof(SoftTouch));
            ContractGuard.Unit(_blockTechnique, nameof(BlockTechnique));
            ContractGuard.Unit(_courtAwareness, nameof(CourtAwareness));
            ContractGuard.NonNegative(_sourceVersion, nameof(SourceVersion));
            ContractGuard.NonNegative(_migrationVersion, nameof(MigrationVersion));

            if (_compatibilityCollapsedAxes == null)
            {
                throw new ContractValidationException("compatibilityCollapsedAxes is required.");
            }

            for (var index = 0; index < _compatibilityCollapsedAxes.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(_compatibilityCollapsedAxes[index]))
                {
                    throw new ContractValidationException("compatibilityCollapsedAxes cannot contain empty values.");
                }
            }
        }

        private static float AttackControlRoleOffset(PlayerPosition position)
        {
            switch (position)
            {
                case PlayerPosition.OutsideHitter:
                case PlayerPosition.Opposite:
                    return 0.03f;
                case PlayerPosition.Setter:
                case PlayerPosition.Libero:
                    return -0.01f;
                default:
                    return 0.01f;
            }
        }

        private static float SoftTouchRoleOffset(PlayerPosition position)
        {
            switch (position)
            {
                case PlayerPosition.Setter:
                case PlayerPosition.Libero:
                    return 0.03f;
                case PlayerPosition.OutsideHitter:
                case PlayerPosition.Opposite:
                    return -0.02f;
                default:
                    return 0.01f;
            }
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static string[] CopyCollapsedAxes(string[] compatibilityCollapsedAxes)
        {
            if (compatibilityCollapsedAxes == null || compatibilityCollapsedAxes.Length == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[compatibilityCollapsedAxes.Length];
            Array.Copy(compatibilityCollapsedAxes, copy, compatibilityCollapsedAxes.Length);
            return copy;
        }

        public bool Equals(PlayerAbilitySnapshotV3 other)
        {
            return other != null &&
                _mobility.Equals(other._mobility) &&
                _reaction.Equals(other._reaction) &&
                _jump.Equals(other._jump) &&
                _maxAttackReach.Equals(other._maxAttackReach) &&
                _receiveTechnique.Equals(other._receiveTechnique) &&
                _setTechnique.Equals(other._setTechnique) &&
                _attackControl.Equals(other._attackControl) &&
                _attackPower.Equals(other._attackPower) &&
                _softTouch.Equals(other._softTouch) &&
                _blockTechnique.Equals(other._blockTechnique) &&
                _courtAwareness.Equals(other._courtAwareness) &&
                _sourceVersion == other._sourceVersion &&
                _migrationVersion == other._migrationVersion &&
                _isCompatibilityEstimate == other._isCompatibilityEstimate &&
                CollapsedAxesEqual(other._compatibilityCollapsedAxes);
        }

        public override bool Equals(object obj) => Equals(obj as PlayerAbilitySnapshotV3);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _mobility.GetHashCode();
                hash = (hash * 397) ^ _reaction.GetHashCode();
                hash = (hash * 397) ^ _jump.GetHashCode();
                hash = (hash * 397) ^ _maxAttackReach.GetHashCode();
                hash = (hash * 397) ^ _receiveTechnique.GetHashCode();
                hash = (hash * 397) ^ _setTechnique.GetHashCode();
                hash = (hash * 397) ^ _attackControl.GetHashCode();
                hash = (hash * 397) ^ _attackPower.GetHashCode();
                hash = (hash * 397) ^ _softTouch.GetHashCode();
                hash = (hash * 397) ^ _blockTechnique.GetHashCode();
                hash = (hash * 397) ^ _courtAwareness.GetHashCode();
                hash = (hash * 397) ^ _sourceVersion;
                hash = (hash * 397) ^ _migrationVersion;
                hash = (hash * 397) ^ _isCompatibilityEstimate.GetHashCode();

                for (var index = 0; index < _compatibilityCollapsedAxes.Length; index++)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(_compatibilityCollapsedAxes[index]);
                }

                return hash;
            }
        }

        private bool CollapsedAxesEqual(string[] otherCollapsedAxes)
        {
            if (otherCollapsedAxes == null || _compatibilityCollapsedAxes.Length != otherCollapsedAxes.Length)
            {
                return false;
            }

            for (var index = 0; index < _compatibilityCollapsedAxes.Length; index++)
            {
                if (!string.Equals(
                    _compatibilityCollapsedAxes[index],
                    otherCollapsedAxes[index],
                    StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
