using System;

namespace Volleyball.Shared.Contracts
{
    public sealed class PhysicalBaseAttributesV4 : IEquatable<PhysicalBaseAttributesV4>
    {
        public PhysicalBaseAttributesV4(
            float heightMeters,
            float standingReachMeters,
            float jump,
            float mobility,
            float reaction,
            float coordination)
        {
            HeightMeters = ContractGuard.HeightMeters(heightMeters, nameof(heightMeters));
            StandingReachMeters = ContractGuard.StandingReachMeters(standingReachMeters, nameof(standingReachMeters));
            Jump = ContractGuard.Unit(jump, nameof(jump));
            Mobility = ContractGuard.Unit(mobility, nameof(mobility));
            Reaction = ContractGuard.Unit(reaction, nameof(reaction));
            Coordination = ContractGuard.Unit(coordination, nameof(coordination));

            if (StandingReachMeters < HeightMeters)
            {
                throw new ContractValidationException("standingReachMeters must be greater than or equal to heightMeters.");
            }
        }

        public float HeightMeters { get; }
        public float StandingReachMeters { get; }
        public float Jump { get; }
        public float Mobility { get; }
        public float Reaction { get; }
        public float Coordination { get; }

        public bool Equals(PhysicalBaseAttributesV4 other)
        {
            return other != null &&
                HeightMeters.Equals(other.HeightMeters) &&
                StandingReachMeters.Equals(other.StandingReachMeters) &&
                Jump.Equals(other.Jump) &&
                Mobility.Equals(other.Mobility) &&
                Reaction.Equals(other.Reaction) &&
                Coordination.Equals(other.Coordination);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PhysicalBaseAttributesV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = HeightMeters.GetHashCode();
                hash = (hash * 397) ^ StandingReachMeters.GetHashCode();
                hash = (hash * 397) ^ Jump.GetHashCode();
                hash = (hash * 397) ^ Mobility.GetHashCode();
                hash = (hash * 397) ^ Reaction.GetHashCode();
                hash = (hash * 397) ^ Coordination.GetHashCode();
                return hash;
            }
        }
    }
}
