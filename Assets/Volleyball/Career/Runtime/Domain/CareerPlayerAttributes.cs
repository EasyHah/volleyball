using System;

namespace Volleyball.Career.Domain
{
    public enum CareerAttributeKind
    {
        Spike = 0,
        Serve = 1,
        Reception = 2,
        Defense = 3,
        Block = 4,
        Movement = 5,
        Jump = 6,
        Stamina = 7
    }

    public readonly struct CareerAttributeProgress : IEquatable<CareerAttributeProgress>
    {
        public const int MinimumAbilityBasisPoints = 0;
        public const int MaximumAbilityBasisPoints = 10000;
        public const long MinimumGrowthExperience = 0L;
        public const long MaximumGrowthExperience = 9007199254740991L;

        public CareerAttributeProgress(int abilityBasisPoints, long growthExperience)
        {
            if (abilityBasisPoints < MinimumAbilityBasisPoints ||
                abilityBasisPoints > MaximumAbilityBasisPoints)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(abilityBasisPoints),
                    abilityBasisPoints,
                    "Ability basis points must be in the range [0, 10000].");
            }

            if (growthExperience < MinimumGrowthExperience ||
                growthExperience > MaximumGrowthExperience)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(growthExperience),
                    growthExperience,
                    "Growth experience must be in the I-JSON safe range [0, 9007199254740991].");
            }

            AbilityBasisPoints = abilityBasisPoints;
            GrowthExperience = growthExperience;
        }

        public int AbilityBasisPoints { get; }

        public long GrowthExperience { get; }

        public int DisplayValue
        {
            get
            {
                var rounded = (AbilityBasisPoints + 50) / 100;
                return Math.Max(1, Math.Min(100, rounded));
            }
        }

        public CareerAttributeProgress AddGrowthExperience(long amount)
        {
            if (amount < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Growth experience increments must be non-negative.");
            }

            long result;
            checked
            {
                result = GrowthExperience + amount;
            }

            if (result > MaximumGrowthExperience)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "The resulting growth experience exceeds the I-JSON safe maximum.");
            }

            return new CareerAttributeProgress(AbilityBasisPoints, result);
        }

        public bool Equals(CareerAttributeProgress other)
        {
            return AbilityBasisPoints == other.AbilityBasisPoints &&
                   GrowthExperience == other.GrowthExperience;
        }

        public override bool Equals(object obj)
        {
            return obj is CareerAttributeProgress other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (AbilityBasisPoints * 397) ^ GrowthExperience.GetHashCode();
            }
        }
    }

    public sealed class CareerPlayerAttributes : IEquatable<CareerPlayerAttributes>
    {
        public CareerPlayerAttributes(
            CareerAttributeProgress spike,
            CareerAttributeProgress serve,
            CareerAttributeProgress reception,
            CareerAttributeProgress defense,
            CareerAttributeProgress block,
            CareerAttributeProgress movement,
            CareerAttributeProgress jump,
            CareerAttributeProgress stamina)
        {
            Spike = spike;
            Serve = serve;
            Reception = reception;
            Defense = defense;
            Block = block;
            Movement = movement;
            Jump = jump;
            Stamina = stamina;
        }

        public CareerAttributeProgress Spike { get; }

        public CareerAttributeProgress Serve { get; }

        public CareerAttributeProgress Reception { get; }

        public CareerAttributeProgress Defense { get; }

        public CareerAttributeProgress Block { get; }

        public CareerAttributeProgress Movement { get; }

        public CareerAttributeProgress Jump { get; }

        public CareerAttributeProgress Stamina { get; }

        public CareerAttributeProgress Get(CareerAttributeKind kind)
        {
            switch (kind)
            {
                case CareerAttributeKind.Spike:
                    return Spike;
                case CareerAttributeKind.Serve:
                    return Serve;
                case CareerAttributeKind.Reception:
                    return Reception;
                case CareerAttributeKind.Defense:
                    return Defense;
                case CareerAttributeKind.Block:
                    return Block;
                case CareerAttributeKind.Movement:
                    return Movement;
                case CareerAttributeKind.Jump:
                    return Jump;
                case CareerAttributeKind.Stamina:
                    return Stamina;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown career attribute kind.");
            }
        }

        public bool Equals(CareerPlayerAttributes other)
        {
            return other != null &&
                   Spike.Equals(other.Spike) &&
                   Serve.Equals(other.Serve) &&
                   Reception.Equals(other.Reception) &&
                   Defense.Equals(other.Defense) &&
                   Block.Equals(other.Block) &&
                   Movement.Equals(other.Movement) &&
                   Jump.Equals(other.Jump) &&
                   Stamina.Equals(other.Stamina);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CareerPlayerAttributes);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Spike.GetHashCode();
                hash = (hash * 397) ^ Serve.GetHashCode();
                hash = (hash * 397) ^ Reception.GetHashCode();
                hash = (hash * 397) ^ Defense.GetHashCode();
                hash = (hash * 397) ^ Block.GetHashCode();
                hash = (hash * 397) ^ Movement.GetHashCode();
                hash = (hash * 397) ^ Jump.GetHashCode();
                return (hash * 397) ^ Stamina.GetHashCode();
            }
        }
    }
}
