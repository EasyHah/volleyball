using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum AbilityAxisStatus
    {
        Active,
        CompatibilityMapped,
        Reserved
    }

    public enum AbilityAxisName
    {
        Mobility,
        Reaction,
        Jump,
        MaxAttackReach,
        ReceiveTechnique,
        SetTechnique,
        AttackControl,
        AttackPower,
        SoftTouch,
        BlockTechnique,
        CourtAwareness
    }

    public sealed class AbilityAxisProjectionV3 : IEquatable<AbilityAxisProjectionV3>
    {
        public AbilityAxisProjectionV3(
            AbilityAxisName name,
            AbilityAxisStatus status,
            float value,
            string sourceName,
            string actionCategory)
        {
            Name = RequireDefinedEnum(name, nameof(name));
            Status = RequireDefinedEnum(status, nameof(status));
            Value = RequireAxisValue(name, value, nameof(value));
            SourceName = Required(sourceName, nameof(sourceName));
            ActionCategory = actionCategory ?? string.Empty;
        }

        public AbilityAxisName Name { get; }

        public AbilityAxisStatus Status { get; }

        public float Value { get; }

        public string SourceName { get; }

        public string ActionCategory { get; }

        public bool Equals(AbilityAxisProjectionV3 other)
        {
            return other != null
                && Name == other.Name
                && Status == other.Status
                && Value.Equals(other.Value)
                && SourceName == other.SourceName
                && ActionCategory == other.ActionCategory;
        }

        public override bool Equals(object obj) => Equals(obj as AbilityAxisProjectionV3);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Name;
                hashCode = (hashCode * 397) ^ (int)Status;
                hashCode = (hashCode * 397) ^ Value.GetHashCode();
                hashCode = (hashCode * 397) ^ SourceName.GetHashCode();
                hashCode = (hashCode * 397) ^ ActionCategory.GetHashCode();
                return hashCode;
            }
        }

        internal static T RequireDefinedEnum<T>(T value, string paramName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(paramName, "Enum value is not supported.");
            }

            return value;
        }

        internal static float RequireUnit(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Ability value must be finite and in the range [0, 1].");
            }

            return value;
        }

        internal static float RequireAxisValue(AbilityAxisName name, float value, string paramName)
        {
            if (name == AbilityAxisName.MaxAttackReach)
            {
                return RequireAttackReach(value, paramName);
            }

            return RequireUnit(value, paramName);
        }

        internal static float RequireAttackReach(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 3.20f || value > 3.55f)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Attack reach must be finite and in the range [3.20, 3.55].");
            }

            return value;
        }

        internal static string Required(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", paramName);
            }

            return value;
        }
    }

    public sealed class AbilityProjectionV3 : IEquatable<AbilityProjectionV3>
    {
        private readonly IReadOnlyDictionary<AbilityAxisName, AbilityAxisProjectionV3> _axes;

        public AbilityProjectionV3(IReadOnlyList<AbilityAxisProjectionV3> axes)
        {
            if (axes == null)
            {
                throw new ArgumentNullException(nameof(axes));
            }

            var byName = new Dictionary<AbilityAxisName, AbilityAxisProjectionV3>();
            var copy = new AbilityAxisProjectionV3[axes.Count];
            for (var index = 0; index < axes.Count; index++)
            {
                var axis = axes[index];
                if (axis == null)
                {
                    throw new ArgumentException("Axes cannot contain null.", nameof(axes));
                }

                if (!byName.TryAdd(axis.Name, axis))
                {
                    throw new ArgumentException("Axes must contain distinct names.", nameof(axes));
                }

                copy[index] = axis;
            }

            _axes = new ReadOnlyDictionary<AbilityAxisName, AbilityAxisProjectionV3>(byName);
            Axes = new ReadOnlyCollection<AbilityAxisProjectionV3>(copy);
        }

        public IReadOnlyList<AbilityAxisProjectionV3> Axes { get; }

        public AbilityAxisProjectionV3 For(AbilityAxisName name)
        {
            if (!_axes.TryGetValue(name, out var axis))
            {
                throw new KeyNotFoundException("Axis " + name + " is not present in the projection.");
            }

            return axis;
        }

        public bool Equals(AbilityProjectionV3 other)
        {
            if (other == null || Axes.Count != other.Axes.Count)
            {
                return false;
            }

            for (var index = 0; index < Axes.Count; index++)
            {
                if (!Axes[index].Equals(other.Axes[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as AbilityProjectionV3);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                for (var index = 0; index < Axes.Count; index++)
                {
                    hashCode = (hashCode * 397) ^ Axes[index].GetHashCode();
                }

                return hashCode;
            }
        }

        public static AbilityProjectionV3 FromSnapshot(PlayerAbilitySnapshotV3 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var axes = new AbilityAxisProjectionV3[]
            {
                new AbilityAxisProjectionV3(AbilityAxisName.Mobility, AbilityAxisStatus.Active, snapshot.Mobility, nameof(PlayerAbilitySnapshotV3.Mobility), "locomotion"),
                new AbilityAxisProjectionV3(AbilityAxisName.Reaction, AbilityAxisStatus.Active, snapshot.Reaction, nameof(PlayerAbilitySnapshotV3.Reaction), "locomotion"),
                new AbilityAxisProjectionV3(AbilityAxisName.Jump, AbilityAxisStatus.Active, snapshot.Jump, nameof(PlayerAbilitySnapshotV3.Jump), "locomotion"),
                new AbilityAxisProjectionV3(AbilityAxisName.MaxAttackReach, AbilityAxisStatus.Active, snapshot.MaxAttackReach, nameof(PlayerAbilitySnapshotV3.MaxAttackReach), "attack"),
                new AbilityAxisProjectionV3(AbilityAxisName.ReceiveTechnique, AbilityAxisStatus.Active, snapshot.ReceiveTechnique, nameof(PlayerAbilitySnapshotV3.ReceiveTechnique), "receive"),
                new AbilityAxisProjectionV3(AbilityAxisName.SetTechnique, AbilityAxisStatus.Active, snapshot.SetTechnique, nameof(PlayerAbilitySnapshotV3.SetTechnique), "set"),
                new AbilityAxisProjectionV3(AbilityAxisName.AttackControl, AbilityAxisStatus.Active, snapshot.AttackControl, nameof(PlayerAbilitySnapshotV3.AttackControl), "attack"),
                new AbilityAxisProjectionV3(AbilityAxisName.AttackPower, AbilityAxisStatus.Active, snapshot.AttackPower, nameof(PlayerAbilitySnapshotV3.AttackPower), "attack"),
                new AbilityAxisProjectionV3(AbilityAxisName.SoftTouch, AbilityAxisStatus.Reserved, snapshot.SoftTouch, nameof(PlayerAbilitySnapshotV3.SoftTouch), string.Empty),
                new AbilityAxisProjectionV3(AbilityAxisName.BlockTechnique, AbilityAxisStatus.Reserved, snapshot.BlockTechnique, nameof(PlayerAbilitySnapshotV3.BlockTechnique), string.Empty),
                new AbilityAxisProjectionV3(AbilityAxisName.CourtAwareness, AbilityAxisStatus.Reserved, snapshot.CourtAwareness, nameof(PlayerAbilitySnapshotV3.CourtAwareness), string.Empty)
            };

            return new AbilityProjectionV3(axes);
        }

        public static AbilityProjectionV3 FromV2Snapshot(PlayerAbilitySnapshotV2 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var attackControl = snapshot.AttackTechnique;
            var axes = new AbilityAxisProjectionV3[]
            {
                new AbilityAxisProjectionV3(AbilityAxisName.Mobility, AbilityAxisStatus.Active, snapshot.Mobility, nameof(PlayerAbilitySnapshotV2.Mobility), "locomotion"),
                new AbilityAxisProjectionV3(AbilityAxisName.Reaction, AbilityAxisStatus.Active, snapshot.Reaction, nameof(PlayerAbilitySnapshotV2.Reaction), "locomotion"),
                new AbilityAxisProjectionV3(AbilityAxisName.Jump, AbilityAxisStatus.Active, snapshot.Jump, nameof(PlayerAbilitySnapshotV2.Jump), "locomotion"),
                new AbilityAxisProjectionV3(AbilityAxisName.MaxAttackReach, AbilityAxisStatus.Active, snapshot.MaxAttackReach, nameof(PlayerAbilitySnapshotV2.MaxAttackReach), "attack"),
                new AbilityAxisProjectionV3(AbilityAxisName.ReceiveTechnique, AbilityAxisStatus.Active, snapshot.ReceiveTechnique, nameof(PlayerAbilitySnapshotV2.ReceiveTechnique), "receive"),
                new AbilityAxisProjectionV3(AbilityAxisName.SetTechnique, AbilityAxisStatus.Active, snapshot.SetTechnique, nameof(PlayerAbilitySnapshotV2.SetTechnique), "set"),
                new AbilityAxisProjectionV3(AbilityAxisName.AttackControl, AbilityAxisStatus.CompatibilityMapped, attackControl, nameof(PlayerAbilitySnapshotV2.AttackTechnique), "attack"),
                new AbilityAxisProjectionV3(AbilityAxisName.AttackPower, AbilityAxisStatus.Active, snapshot.AttackPower, nameof(PlayerAbilitySnapshotV2.AttackPower), "attack"),
                new AbilityAxisProjectionV3(AbilityAxisName.SoftTouch, AbilityAxisStatus.Reserved, 0.5f, "SoftTouchDefault", string.Empty),
                new AbilityAxisProjectionV3(AbilityAxisName.BlockTechnique, AbilityAxisStatus.Reserved, 0.5f, "BlockTechniqueDefault", string.Empty),
                new AbilityAxisProjectionV3(AbilityAxisName.CourtAwareness, AbilityAxisStatus.Reserved, 0.5f, "CourtAwarenessDefault", string.Empty)
            };

            return new AbilityProjectionV3(axes);
        }

        public float ActiveValueFor(AbilityAxisName name)
        {
            var axis = For(name);
            return axis.Status == AbilityAxisStatus.Active ? axis.Value : throw new InvalidOperationException("Axis " + name + " is not active.");
        }

        public float AttackTechniqueValue()
        {
            var attackControl = For(AbilityAxisName.AttackControl);
            if (attackControl.Status == AbilityAxisStatus.Active)
            {
                return attackControl.Value;
            }

            if (attackControl.Status == AbilityAxisStatus.CompatibilityMapped)
            {
                return attackControl.Value;
            }

            throw new InvalidOperationException("AttackControl is reserved and cannot provide an attack technique value.");
        }
    }
}
