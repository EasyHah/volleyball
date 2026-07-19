using System;

namespace Volleyball.AI
{
    public readonly struct RallyTacticalWeightProposal
    {
        public RallyTacticalWeightProposal(
            float rolePreference,
            float reachability,
            float approachDistance,
            float directionTolerance)
        {
            RolePreference = rolePreference;
            Reachability = reachability;
            ApproachDistance = approachDistance;
            DirectionTolerance = directionTolerance;
        }

        public float RolePreference { get; }

        public float Reachability { get; }

        public float ApproachDistance { get; }

        public float DirectionTolerance { get; }
    }

    public readonly struct RallyTacticalWeights : IEquatable<RallyTacticalWeights>
    {
        public const float Minimum = 0f;
        public const float Maximum = 2f;

        public RallyTacticalWeights(
            float rolePreference,
            float reachability,
            float approachDistance,
            float directionTolerance)
        {
            Validate(rolePreference, nameof(rolePreference));
            Validate(reachability, nameof(reachability));
            Validate(approachDistance, nameof(approachDistance));
            Validate(directionTolerance, nameof(directionTolerance));

            RolePreference = rolePreference;
            Reachability = reachability;
            ApproachDistance = approachDistance;
            DirectionTolerance = directionTolerance;
        }

        public static RallyTacticalWeights Default => new RallyTacticalWeights(1f, 1f, 1f, 1f);

        public float RolePreference { get; }

        public float Reachability { get; }

        public float ApproachDistance { get; }

        public float DirectionTolerance { get; }

        public static RallyTacticalWeights ResolveOrDefault(RallyTacticalWeightProposal proposal)
        {
            return IsBounded(proposal.RolePreference) &&
                   IsBounded(proposal.Reachability) &&
                   IsBounded(proposal.ApproachDistance) &&
                   IsBounded(proposal.DirectionTolerance)
                ? new RallyTacticalWeights(
                    proposal.RolePreference,
                    proposal.Reachability,
                    proposal.ApproachDistance,
                    proposal.DirectionTolerance)
                : Default;
        }

        public bool Equals(RallyTacticalWeights other)
        {
            return RolePreference.Equals(other.RolePreference) &&
                   Reachability.Equals(other.Reachability) &&
                   ApproachDistance.Equals(other.ApproachDistance) &&
                   DirectionTolerance.Equals(other.DirectionTolerance);
        }

        public override bool Equals(object obj)
        {
            return obj is RallyTacticalWeights other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RolePreference.GetHashCode();
                hashCode = (hashCode * 397) ^ Reachability.GetHashCode();
                hashCode = (hashCode * 397) ^ ApproachDistance.GetHashCode();
                return (hashCode * 397) ^ DirectionTolerance.GetHashCode();
            }
        }

        private static void Validate(float value, string parameterName)
        {
            if (!IsBounded(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Tactical weights must be finite and in the range [0, 2].");
            }
        }

        private static bool IsBounded(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= Minimum && value <= Maximum;
        }
    }
}
