using System;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.PreServe
{
    public enum TrainingPlayerAttributeFieldV2
    {
        Strength = 0,
        Height = 1,
        Jump = 2,
        Movement = 3,
        Reaction = 4,
        Coordination = 5,
        Attack = 6,
        Defense = 7,
        CourtIq = 8,
        Block = 9,
        Serve = 10,
        Set = 11,
        DominantHand = 12
    }

    /// <summary>
    /// Scenario-private overrides for a native V5 player. Null means that the
    /// immutable value from the base MatchContextV5 remains authoritative.
    /// </summary>
    public sealed class TrainingPlayerAttributeOverrideV2
    {
        public int? Strength { get; private set; }
        public int? HeightMillimeters { get; private set; }
        public int? Jump { get; private set; }
        public int? Movement { get; private set; }
        public int? Reaction { get; private set; }
        public int? Coordination { get; private set; }
        public int? Attack { get; private set; }
        public int? Defense { get; private set; }
        public int? CourtIq { get; private set; }
        public int? Block { get; private set; }
        public int? Serve { get; private set; }
        public int? Setting { get; private set; }
        public DominantHandV5? DominantHand { get; private set; }

        public bool HasAny => Strength.HasValue || HeightMillimeters.HasValue ||
            Jump.HasValue || Movement.HasValue || Reaction.HasValue ||
            Coordination.HasValue || Attack.HasValue || Defense.HasValue ||
            CourtIq.HasValue || Block.HasValue || Serve.HasValue ||
            Setting.HasValue || DominantHand.HasValue;

        public void Set(TrainingPlayerAttributeFieldV2 field, int value)
        {
            if (field == TrainingPlayerAttributeFieldV2.DominantHand)
                throw new ArgumentException(
                    "Use SetDominantHand for the dominant-hand override.",
                    nameof(field));

            if (field == TrainingPlayerAttributeFieldV2.Height)
            {
                if (value < CareerBaseAttributesV5.MinimumHeightMillimeters ||
                    value > CareerBaseAttributesV5.MaximumHeightMillimeters)
                    throw new ArgumentOutOfRangeException(nameof(value),
                        "Height must be in the range [1400, 2300].");
            }
            else if (value < CareerBaseAttributesV5.MinimumBasisPoints ||
                     value > CareerBaseAttributesV5.MaximumBasisPoints)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    "V5 attributes must be in the range [0, 10000].");
            }

            switch (field)
            {
                case TrainingPlayerAttributeFieldV2.Strength: Strength = value; break;
                case TrainingPlayerAttributeFieldV2.Height: HeightMillimeters = value; break;
                case TrainingPlayerAttributeFieldV2.Jump: Jump = value; break;
                case TrainingPlayerAttributeFieldV2.Movement: Movement = value; break;
                case TrainingPlayerAttributeFieldV2.Reaction: Reaction = value; break;
                case TrainingPlayerAttributeFieldV2.Coordination: Coordination = value; break;
                case TrainingPlayerAttributeFieldV2.Attack: Attack = value; break;
                case TrainingPlayerAttributeFieldV2.Defense: Defense = value; break;
                case TrainingPlayerAttributeFieldV2.CourtIq: CourtIq = value; break;
                case TrainingPlayerAttributeFieldV2.Block: Block = value; break;
                case TrainingPlayerAttributeFieldV2.Serve: Serve = value; break;
                case TrainingPlayerAttributeFieldV2.Set: Setting = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        public void SetDominantHand(DominantHandV5 value)
        {
            if (!Enum.IsDefined(typeof(DominantHandV5), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            DominantHand = value;
        }

        public void Clear(TrainingPlayerAttributeFieldV2 field)
        {
            switch (field)
            {
                case TrainingPlayerAttributeFieldV2.Strength: Strength = null; break;
                case TrainingPlayerAttributeFieldV2.Height: HeightMillimeters = null; break;
                case TrainingPlayerAttributeFieldV2.Jump: Jump = null; break;
                case TrainingPlayerAttributeFieldV2.Movement: Movement = null; break;
                case TrainingPlayerAttributeFieldV2.Reaction: Reaction = null; break;
                case TrainingPlayerAttributeFieldV2.Coordination: Coordination = null; break;
                case TrainingPlayerAttributeFieldV2.Attack: Attack = null; break;
                case TrainingPlayerAttributeFieldV2.Defense: Defense = null; break;
                case TrainingPlayerAttributeFieldV2.CourtIq: CourtIq = null; break;
                case TrainingPlayerAttributeFieldV2.Block: Block = null; break;
                case TrainingPlayerAttributeFieldV2.Serve: Serve = null; break;
                case TrainingPlayerAttributeFieldV2.Set: Setting = null; break;
                case TrainingPlayerAttributeFieldV2.DominantHand: DominantHand = null; break;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        public void Reset()
        {
            foreach (TrainingPlayerAttributeFieldV2 field in
                     Enum.GetValues(typeof(TrainingPlayerAttributeFieldV2)))
                Clear(field);
        }

        public CareerBaseAttributesV5 ApplyTo(CareerBaseAttributesV5 source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new CareerBaseAttributesV5(
                Strength ?? source.Strength,
                HeightMillimeters ?? source.HeightMillimeters,
                Jump ?? source.Jump,
                Movement ?? source.Movement,
                Reaction ?? source.Reaction,
                Coordination ?? source.Coordination,
                Attack ?? source.Attack,
                Defense ?? source.Defense,
                CourtIq ?? source.CourtIq,
                Block ?? source.Block,
                Serve ?? source.Serve,
                Setting ?? source.Set);
        }

        public TrainingPlayerAttributeOverrideV2 DeepCopy()
        {
            var copy = new TrainingPlayerAttributeOverrideV2();
            foreach (TrainingPlayerAttributeFieldV2 field in
                     Enum.GetValues(typeof(TrainingPlayerAttributeFieldV2)))
            {
                if (field == TrainingPlayerAttributeFieldV2.DominantHand)
                {
                    if (DominantHand.HasValue)
                        copy.SetDominantHand(DominantHand.Value);
                    continue;
                }

                var value = ValueFor(field);
                if (value.HasValue) copy.Set(field, value.Value);
            }
            return copy;
        }

        internal int? ValueFor(TrainingPlayerAttributeFieldV2 field)
        {
            return field switch
            {
                TrainingPlayerAttributeFieldV2.Strength => Strength,
                TrainingPlayerAttributeFieldV2.Height => HeightMillimeters,
                TrainingPlayerAttributeFieldV2.Jump => Jump,
                TrainingPlayerAttributeFieldV2.Movement => Movement,
                TrainingPlayerAttributeFieldV2.Reaction => Reaction,
                TrainingPlayerAttributeFieldV2.Coordination => Coordination,
                TrainingPlayerAttributeFieldV2.Attack => Attack,
                TrainingPlayerAttributeFieldV2.Defense => Defense,
                TrainingPlayerAttributeFieldV2.CourtIq => CourtIq,
                TrainingPlayerAttributeFieldV2.Block => Block,
                TrainingPlayerAttributeFieldV2.Serve => Serve,
                TrainingPlayerAttributeFieldV2.Set => Setting,
                _ => null
            };
        }
    }
}
