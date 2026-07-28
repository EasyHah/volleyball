using System;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public enum SetterOrganizationZoneGrade
    {
        Best,
        Secondary,
        Poor
    }

    public readonly struct SetterOrganizationZoneAssessment
    {
        public SetterOrganizationZoneAssessment(
            float lateralDistanceFromPositionFourSideline,
            float depthFromNet,
            SetterOrganizationZoneGrade lateralGrade,
            SetterOrganizationZoneGrade depthGrade)
        {
            LateralDistanceFromPositionFourSideline = lateralDistanceFromPositionFourSideline;
            DepthFromNet = depthFromNet;
            LateralGrade = lateralGrade;
            DepthGrade = depthGrade;
        }

        public float LateralDistanceFromPositionFourSideline { get; }
        public float DepthFromNet { get; }
        public SetterOrganizationZoneGrade LateralGrade { get; }
        public SetterOrganizationZoneGrade DepthGrade { get; }
    }

    public static class SetterOrganizationZone
    {
        private const float PositionFourSidelineX = -4.5f;
        private const float DefaultX = 1.5f;
        private const float DefaultDepthFromNet = 1.1f;

        public static SimVector3 DefaultLocalTarget => new SimVector3(DefaultX, 0f, -DefaultDepthFromNet);

        public static SimVector3 DefaultWorldTarget(TeamId attackingTeam)
        {
            return new TeamCourtFrame(attackingTeam).ToWorld(DefaultLocalTarget);
        }

        public static SetterOrganizationZoneAssessment AssessWorldTarget(
            TeamId attackingTeam,
            SimVector3 worldTarget)
        {
            return AssessLocalTarget(new TeamCourtFrame(attackingTeam).ToLocal(worldTarget));
        }

        public static SetterOrganizationZoneAssessment AssessLocalTarget(SimVector3 localTarget)
        {
            if (!localTarget.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(localTarget));
            }

            var lateralDistance = localTarget.X - PositionFourSidelineX;
            var depthFromNet = -localTarget.Z;
            return new SetterOrganizationZoneAssessment(
                lateralDistance,
                depthFromNet,
                GradeLateral(lateralDistance),
                GradeDepth(depthFromNet));
        }

        private static SetterOrganizationZoneGrade GradeLateral(float distance)
        {
            if (distance >= 5f && distance <= 7f)
            {
                return SetterOrganizationZoneGrade.Best;
            }

            return (distance >= 3f && distance < 5f) || (distance > 7f && distance <= 8f)
                ? SetterOrganizationZoneGrade.Secondary
                : SetterOrganizationZoneGrade.Poor;
        }

        private static SetterOrganizationZoneGrade GradeDepth(float depth)
        {
            if (depth >= 0f && depth <= 1.5f)
            {
                return SetterOrganizationZoneGrade.Best;
            }

            return depth > 1.5f && depth <= 4f
                ? SetterOrganizationZoneGrade.Secondary
                : SetterOrganizationZoneGrade.Poor;
        }
    }
}
