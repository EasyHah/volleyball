using System;

namespace Volleyball.Shared.Contracts
{
    public sealed class AttackAttributesV4 : IEquatable<AttackAttributesV4>
    {
        public AttackAttributesV4(
            float directionControl,
            float speedControl,
            float powerCapacity,
            float contactHeightMeters,
            float approachMobility)
        {
            DirectionControl = ContractGuard.Unit(directionControl, nameof(directionControl));
            SpeedControl = ContractGuard.Unit(speedControl, nameof(speedControl));
            PowerCapacity = ContractGuard.Unit(powerCapacity, nameof(powerCapacity));
            ContactHeightMeters = MatchAttributeOutputGuard.Meters(
                contactHeightMeters,
                nameof(contactHeightMeters),
                1.95f,
                3.95f);
            ApproachMobility = ContractGuard.Unit(approachMobility, nameof(approachMobility));
        }

        public float DirectionControl { get; }
        public float SpeedControl { get; }
        public float PowerCapacity { get; }
        public float ContactHeightMeters { get; }
        public float ApproachMobility { get; }

        public bool Equals(AttackAttributesV4 other)
        {
            return other != null &&
                DirectionControl.Equals(other.DirectionControl) &&
                SpeedControl.Equals(other.SpeedControl) &&
                PowerCapacity.Equals(other.PowerCapacity) &&
                ContactHeightMeters.Equals(other.ContactHeightMeters) &&
                ApproachMobility.Equals(other.ApproachMobility);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AttackAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = DirectionControl.GetHashCode();
                hash = (hash * 397) ^ SpeedControl.GetHashCode();
                hash = (hash * 397) ^ PowerCapacity.GetHashCode();
                hash = (hash * 397) ^ ContactHeightMeters.GetHashCode();
                hash = (hash * 397) ^ ApproachMobility.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class BlockAttributesV4 : IEquatable<BlockAttributesV4>
    {
        public BlockAttributesV4(
            float timing,
            float handControl,
            float reachHeightMeters,
            float lateralMobility)
        {
            Timing = ContractGuard.Unit(timing, nameof(timing));
            HandControl = ContractGuard.Unit(handControl, nameof(handControl));
            ReachHeightMeters = MatchAttributeOutputGuard.Meters(
                reachHeightMeters,
                nameof(reachHeightMeters),
                1.90f,
                3.85f);
            LateralMobility = ContractGuard.Unit(lateralMobility, nameof(lateralMobility));
        }

        public float Timing { get; }
        public float HandControl { get; }
        public float ReachHeightMeters { get; }
        public float LateralMobility { get; }

        public bool Equals(BlockAttributesV4 other)
        {
            return other != null &&
                Timing.Equals(other.Timing) &&
                HandControl.Equals(other.HandControl) &&
                ReachHeightMeters.Equals(other.ReachHeightMeters) &&
                LateralMobility.Equals(other.LateralMobility);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BlockAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Timing.GetHashCode();
                hash = (hash * 397) ^ HandControl.GetHashCode();
                hash = (hash * 397) ^ ReachHeightMeters.GetHashCode();
                hash = (hash * 397) ^ LateralMobility.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class DefenseAttributesV4 : IEquatable<DefenseAttributesV4>
    {
        public DefenseAttributesV4(
            float reaction,
            float platformControl,
            float coverageMobility,
            float awareness)
        {
            Reaction = ContractGuard.Unit(reaction, nameof(reaction));
            PlatformControl = ContractGuard.Unit(platformControl, nameof(platformControl));
            CoverageMobility = ContractGuard.Unit(coverageMobility, nameof(coverageMobility));
            Awareness = ContractGuard.Unit(awareness, nameof(awareness));
        }

        public float Reaction { get; }
        public float PlatformControl { get; }
        public float CoverageMobility { get; }
        public float Awareness { get; }

        public bool Equals(DefenseAttributesV4 other)
        {
            return other != null &&
                Reaction.Equals(other.Reaction) &&
                PlatformControl.Equals(other.PlatformControl) &&
                CoverageMobility.Equals(other.CoverageMobility) &&
                Awareness.Equals(other.Awareness);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DefenseAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Reaction.GetHashCode();
                hash = (hash * 397) ^ PlatformControl.GetHashCode();
                hash = (hash * 397) ^ CoverageMobility.GetHashCode();
                hash = (hash * 397) ^ Awareness.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class ReceiveAttributesV4 : IEquatable<ReceiveAttributesV4>
    {
        public ReceiveAttributesV4(
            float firstTouchControl,
            float reaction,
            float movement,
            float awareness)
        {
            FirstTouchControl = ContractGuard.Unit(firstTouchControl, nameof(firstTouchControl));
            Reaction = ContractGuard.Unit(reaction, nameof(reaction));
            Movement = ContractGuard.Unit(movement, nameof(movement));
            Awareness = ContractGuard.Unit(awareness, nameof(awareness));
        }

        public float FirstTouchControl { get; }
        public float Reaction { get; }
        public float Movement { get; }
        public float Awareness { get; }

        public bool Equals(ReceiveAttributesV4 other)
        {
            return other != null &&
                FirstTouchControl.Equals(other.FirstTouchControl) &&
                Reaction.Equals(other.Reaction) &&
                Movement.Equals(other.Movement) &&
                Awareness.Equals(other.Awareness);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ReceiveAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = FirstTouchControl.GetHashCode();
                hash = (hash * 397) ^ Reaction.GetHashCode();
                hash = (hash * 397) ^ Movement.GetHashCode();
                hash = (hash * 397) ^ Awareness.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class SetAttributesV4 : IEquatable<SetAttributesV4>
    {
        public SetAttributesV4(
            float placementControl,
            float tempoControl,
            float softTouch,
            float movement,
            float awareness)
        {
            PlacementControl = ContractGuard.Unit(placementControl, nameof(placementControl));
            TempoControl = ContractGuard.Unit(tempoControl, nameof(tempoControl));
            SoftTouch = ContractGuard.Unit(softTouch, nameof(softTouch));
            Movement = ContractGuard.Unit(movement, nameof(movement));
            Awareness = ContractGuard.Unit(awareness, nameof(awareness));
        }

        public float PlacementControl { get; }
        public float TempoControl { get; }
        public float SoftTouch { get; }
        public float Movement { get; }
        public float Awareness { get; }

        public bool Equals(SetAttributesV4 other)
        {
            return other != null &&
                PlacementControl.Equals(other.PlacementControl) &&
                TempoControl.Equals(other.TempoControl) &&
                SoftTouch.Equals(other.SoftTouch) &&
                Movement.Equals(other.Movement) &&
                Awareness.Equals(other.Awareness);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SetAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PlacementControl.GetHashCode();
                hash = (hash * 397) ^ TempoControl.GetHashCode();
                hash = (hash * 397) ^ SoftTouch.GetHashCode();
                hash = (hash * 397) ^ Movement.GetHashCode();
                hash = (hash * 397) ^ Awareness.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class ServeAttributesV4 : IEquatable<ServeAttributesV4>
    {
        public ServeAttributesV4(
            float directionControl,
            float speedControl,
            float powerCapacity,
            float consistency)
        {
            DirectionControl = ContractGuard.Unit(directionControl, nameof(directionControl));
            SpeedControl = ContractGuard.Unit(speedControl, nameof(speedControl));
            PowerCapacity = ContractGuard.Unit(powerCapacity, nameof(powerCapacity));
            Consistency = ContractGuard.Unit(consistency, nameof(consistency));
        }

        public float DirectionControl { get; }
        public float SpeedControl { get; }
        public float PowerCapacity { get; }
        public float Consistency { get; }

        public bool Equals(ServeAttributesV4 other)
        {
            return other != null &&
                DirectionControl.Equals(other.DirectionControl) &&
                SpeedControl.Equals(other.SpeedControl) &&
                PowerCapacity.Equals(other.PowerCapacity) &&
                Consistency.Equals(other.Consistency);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ServeAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = DirectionControl.GetHashCode();
                hash = (hash * 397) ^ SpeedControl.GetHashCode();
                hash = (hash * 397) ^ PowerCapacity.GetHashCode();
                hash = (hash * 397) ^ Consistency.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class MatchAttributesV4 : IEquatable<MatchAttributesV4>
    {
        public MatchAttributesV4(
            AttackAttributesV4 attack,
            BlockAttributesV4 block,
            DefenseAttributesV4 defense,
            ReceiveAttributesV4 receive,
            SetAttributesV4 set,
            ServeAttributesV4 serve,
            DominantHandV4 dominantHand)
        {
            Attack = attack ?? throw new ContractValidationException("attack is required.");
            Block = block ?? throw new ContractValidationException("block is required.");
            Defense = defense ?? throw new ContractValidationException("defense is required.");
            Receive = receive ?? throw new ContractValidationException("receive is required.");
            Set = set ?? throw new ContractValidationException("set is required.");
            Serve = serve ?? throw new ContractValidationException("serve is required.");
            ContractGuard.DefinedEnum(dominantHand, nameof(dominantHand));
            DominantHand = dominantHand;
        }

        public AttackAttributesV4 Attack { get; }
        public BlockAttributesV4 Block { get; }
        public DefenseAttributesV4 Defense { get; }
        public ReceiveAttributesV4 Receive { get; }
        public SetAttributesV4 Set { get; }
        public ServeAttributesV4 Serve { get; }
        public DominantHandV4 DominantHand { get; }

        public bool Equals(MatchAttributesV4 other)
        {
            return other != null &&
                Attack.Equals(other.Attack) &&
                Block.Equals(other.Block) &&
                Defense.Equals(other.Defense) &&
                Receive.Equals(other.Receive) &&
                Set.Equals(other.Set) &&
                Serve.Equals(other.Serve) &&
                DominantHand == other.DominantHand;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MatchAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Attack.GetHashCode();
                hash = (hash * 397) ^ Block.GetHashCode();
                hash = (hash * 397) ^ Defense.GetHashCode();
                hash = (hash * 397) ^ Receive.GetHashCode();
                hash = (hash * 397) ^ Set.GetHashCode();
                hash = (hash * 397) ^ Serve.GetHashCode();
                hash = (hash * 397) ^ (int)DominantHand;
                return hash;
            }
        }
    }

    internal static class MatchAttributeOutputGuard
    {
        public static float Meters(float value, string name, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
            {
                throw new ContractValidationException(
                    name + " must be finite and in the range [" + minimum + ", " + maximum + "].");
            }

            return value;
        }
    }
}
