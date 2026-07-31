using System;

namespace Volleyball.Shared.Contracts
{
    /// <summary>
    /// Career-owned attributes frozen before a V5 match. Values are never
    /// normalized by Match, so invalid Career data fails at the boundary.
    /// </summary>
    public sealed class CareerBaseAttributesV5 : IEquatable<CareerBaseAttributesV5>
    {
        public const int MinimumBasisPoints = 0;
        public const int MaximumBasisPoints = 10000;
        public const int MinimumHeightMillimeters = 1400;
        public const int MaximumHeightMillimeters = 2300;

        public CareerBaseAttributesV5(
            int strength,
            int heightMillimeters,
            int jump,
            int movement,
            int reaction,
            int coordination,
            int attack,
            int defense,
            int courtIq,
            int block,
            int serve,
            int set)
        {
            Strength = BasisPoints(strength, nameof(strength));
            HeightMillimeters = Height(heightMillimeters, nameof(heightMillimeters));
            Jump = BasisPoints(jump, nameof(jump));
            Movement = BasisPoints(movement, nameof(movement));
            Reaction = BasisPoints(reaction, nameof(reaction));
            Coordination = BasisPoints(coordination, nameof(coordination));
            Attack = BasisPoints(attack, nameof(attack));
            Defense = BasisPoints(defense, nameof(defense));
            CourtIq = BasisPoints(courtIq, nameof(courtIq));
            Block = BasisPoints(block, nameof(block));
            Serve = BasisPoints(serve, nameof(serve));
            Set = BasisPoints(set, nameof(set));
        }

        public int Strength { get; }
        public int HeightMillimeters { get; }
        public int Jump { get; }
        public int Movement { get; }
        public int Reaction { get; }
        public int Coordination { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int CourtIq { get; }
        public int Block { get; }
        public int Serve { get; }
        public int Set { get; }

        public bool Equals(CareerBaseAttributesV5 other)
        {
            return other != null &&
                Strength == other.Strength &&
                HeightMillimeters == other.HeightMillimeters &&
                Jump == other.Jump && Movement == other.Movement &&
                Reaction == other.Reaction && Coordination == other.Coordination &&
                Attack == other.Attack && Defense == other.Defense &&
                CourtIq == other.CourtIq && Block == other.Block &&
                Serve == other.Serve && Set == other.Set;
        }

        public override bool Equals(object obj) => Equals(obj as CareerBaseAttributesV5);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Strength;
                hash = (hash * 397) ^ HeightMillimeters;
                hash = (hash * 397) ^ Jump;
                hash = (hash * 397) ^ Movement;
                hash = (hash * 397) ^ Reaction;
                hash = (hash * 397) ^ Coordination;
                hash = (hash * 397) ^ Attack;
                hash = (hash * 397) ^ Defense;
                hash = (hash * 397) ^ CourtIq;
                hash = (hash * 397) ^ Block;
                hash = (hash * 397) ^ Serve;
                return (hash * 397) ^ Set;
            }
        }

        internal static int BasisPoints(int value, string name)
        {
            if (value < MinimumBasisPoints || value > MaximumBasisPoints)
            {
                throw new ContractValidationException(
                    name + " must be in the range [0, 10000].");
            }

            return value;
        }

        internal static int Height(int value, string name)
        {
            if (value < MinimumHeightMillimeters || value > MaximumHeightMillimeters)
            {
                throw new ContractValidationException(
                    name + " must be in the range [1400, 2300].");
            }

            return value;
        }
    }
}
