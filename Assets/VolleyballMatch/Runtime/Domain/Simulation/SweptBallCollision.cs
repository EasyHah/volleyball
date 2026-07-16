using System;

namespace VolleyballMatch.Domain.Simulation
{
    public readonly struct SweptBallHit
    {
        public SweptBallHit(
            float timeFraction,
            SimVector3 impactCenter,
            SimVector3 contactPoint,
            SimVector3 normal,
            SimVector3 surfaceVelocity,
            int contactGroupId,
            float centeredness)
        {
            TimeFraction = timeFraction;
            ImpactCenter = impactCenter;
            ContactPoint = contactPoint;
            Normal = normal;
            SurfaceVelocity = surfaceVelocity;
            ContactGroupId = contactGroupId;
            Centeredness = centeredness;
        }

        public float TimeFraction { get; }

        public SimVector3 ImpactCenter { get; }

        public SimVector3 ContactPoint { get; }

        public SimVector3 Normal { get; }

        public SimVector3 SurfaceVelocity { get; }

        public int ContactGroupId { get; }

        public float Centeredness { get; }
    }

    public static class SweptBallCollision
    {
        private const float DirectionEpsilon = 0.000001f;

        public static bool TryFindContact(
            BallState ball,
            ContactSurfaceSnapshot surface,
            float deltaSeconds,
            out SweptBallHit hit)
        {
            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }

            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Step duration must be finite and positive.");
            }

            hit = default;
            if (!ball.IsActive || !surface.Active || !ball.CanContact(surface.ContactGroupId))
            {
                return false;
            }

            var ballDisplacement = ball.Position - ball.PreviousPosition;
            var surfaceDisplacement = surface.Current.Origin - surface.Previous.Origin;
            var relativeDisplacement = ballDisplacement - surfaceDisplacement;
            var averageNormal = (surface.Previous.Normal + surface.Current.Normal).Normalized;
            if (SimVector3.Dot(relativeDisplacement, averageNormal) >= -DirectionEpsilon)
            {
                return false;
            }

            var previousCenterDistance = SimVector3.Dot(
                ball.PreviousPosition - surface.Previous.Origin,
                surface.Previous.Normal);
            var currentCenterDistance = SimVector3.Dot(
                ball.Position - surface.Current.Origin,
                surface.Current.Normal);
            if (previousCenterDistance < -ball.Radius || currentCenterDistance - ball.Radius > 0f)
            {
                return false;
            }

            var previousOffsetDistance = previousCenterDistance - ball.Radius;
            var currentOffsetDistance = currentCenterDistance - ball.Radius;
            var denominator = previousOffsetDistance - currentOffsetDistance;
            var timeFraction = previousOffsetDistance <= 0f || denominator <= DirectionEpsilon
                ? 0f
                : Clamp01(previousOffsetDistance / denominator);

            var frame = surface.At(timeFraction);
            var sweptCenter = SimVector3.Lerp(ball.PreviousPosition, ball.Position, timeFraction);
            var signedCenterDistance = SimVector3.Dot(sweptCenter - frame.Origin, frame.Normal);
            var impactCenter = sweptCenter + (frame.Normal * (ball.Radius - signedCenterDistance));
            var contactPoint = impactCenter - (frame.Normal * ball.Radius);
            var fromOrigin = contactPoint - frame.Origin;
            var rightOffset = SimVector3.Dot(fromOrigin, frame.Right);
            var upOffset = SimVector3.Dot(fromOrigin, frame.Up);
            var expandedHalfWidth = (frame.Width * 0.5f) + ball.Radius;
            var expandedHalfHeight = (frame.Height * 0.5f) + ball.Radius;
            if (Math.Abs(rightOffset) > expandedHalfWidth || Math.Abs(upOffset) > expandedHalfHeight)
            {
                return false;
            }

            var outsidePalmRight = Math.Max(0f, Math.Abs(rightOffset) - (frame.Width * 0.5f));
            var outsidePalmUp = Math.Max(0f, Math.Abs(upOffset) - (frame.Height * 0.5f));
            var glancingDistance = Math.Max(outsidePalmRight, outsidePalmUp);
            var centeredness = Clamp01(1f - (glancingDistance / ball.Radius));
            hit = new SweptBallHit(
                timeFraction,
                impactCenter,
                contactPoint,
                frame.Normal,
                surface.VelocityAt(rightOffset, upOffset, deltaSeconds),
                surface.ContactGroupId,
                centeredness);
            return true;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
