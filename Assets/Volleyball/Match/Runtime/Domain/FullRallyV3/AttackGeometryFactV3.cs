using System;
using Volleyball.Shared.Contracts;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class AttackGeometryFactV3
    {
        public AttackGeometryFactV3(
            PlayerId actor,
            TeamSide side,
            SimVector3 takeoffPoint,
            SimVector3 contactPoint,
            float attackLineDistanceFromCenter,
            float netHeight)
        {
            Actor = PlayerWorldSnapshotV3.RequirePlayerId(actor, nameof(actor));
            Side = PlayerWorldSnapshotV3.RequireDefinedEnum(side, nameof(side));
            TakeoffPoint = RequireFinite(takeoffPoint, nameof(takeoffPoint));
            ContactPoint = RequireFinite(contactPoint, nameof(contactPoint));
            AttackLineDistanceFromCenter = RequirePositiveFinite(attackLineDistanceFromCenter, nameof(attackLineDistanceFromCenter));
            NetHeight = RequirePositiveFinite(netHeight, nameof(netHeight));
        }

        public PlayerId Actor { get; }

        public TeamSide Side { get; }

        public SimVector3 TakeoffPoint { get; }

        public SimVector3 ContactPoint { get; }

        public float AttackLineDistanceFromCenter { get; }

        public float NetHeight { get; }

        public bool IsContactAboveNet => ContactPoint.Y > NetHeight;

        public bool IsTakeoffInFrontZone => Side == TeamSide.Home
            ? TakeoffPoint.Z >= -AttackLineDistanceFromCenter && TakeoffPoint.Z <= 0f
            : TakeoffPoint.Z <= AttackLineDistanceFromCenter && TakeoffPoint.Z >= 0f;

        internal static SimVector3 RequireFinite(SimVector3 value, string parameterName)
        {
            if (!value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static float RequirePositiveFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }
}
