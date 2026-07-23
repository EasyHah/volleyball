using System;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public readonly struct AttackBand
    {
        public AttackBand(float nearDepth, float farDepth)
        {
            if (!IsFinite(nearDepth) || !IsFinite(farDepth) || nearDepth < 0f || farDepth < nearDepth)
            {
                throw new ArgumentOutOfRangeException(nameof(nearDepth));
            }

            NearDepth = nearDepth;
            FarDepth = farDepth;
        }

        public float NearDepth { get; }
        public float FarDepth { get; }

        public SimVector3 ConstrainTakeoff(TeamId attackingTeam, SimVector3 actualContactCenter)
        {
            if (!actualContactCenter.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(actualContactCenter));
            }

            var frame = new TeamCourtFrame(attackingTeam);
            var local = frame.ToLocal(actualContactCenter);
            var ownSideDepth = -local.Z;
            var depth = Math.Max(NearDepth, Math.Min(FarDepth, ownSideDepth));
            return frame.ToWorld(new SimVector3(actualContactCenter.X, 0f, -depth));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public static class AttackBandPolicy
    {
        private const float StandardNearDepth = 0.75f;
        private const float StandardFarDepth = 1.50f;
        private const float MiddleNearDepth = 0.50f;
        private const float MiddleFarDepth = 0.75f;
        private const float SetterDepthWithoutShift = 4f;
        private const float MaximumBandShift = 1.50f;
        private const float ShiftPerExcessMeter = 0.50f;

        public static AttackBand Resolve(PlayerRole attackerRole, float setterDepthFromNet)
        {
            if (!Enum.IsDefined(typeof(PlayerRole), attackerRole))
            {
                throw new ArgumentOutOfRangeException(nameof(attackerRole));
            }

            if (float.IsNaN(setterDepthFromNet) || float.IsInfinity(setterDepthFromNet) ||
                setterDepthFromNet < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(setterDepthFromNet));
            }

            var nearDepth = attackerRole == PlayerRole.MiddleBlocker
                ? MiddleNearDepth
                : StandardNearDepth;
            var farDepth = attackerRole == PlayerRole.MiddleBlocker
                ? MiddleFarDepth
                : StandardFarDepth;
            var shift = Math.Min(
                MaximumBandShift,
                Math.Max(0f, setterDepthFromNet - SetterDepthWithoutShift) * ShiftPerExcessMeter);
            return new AttackBand(nearDepth + shift, farDepth + shift);
        }
    }
}
