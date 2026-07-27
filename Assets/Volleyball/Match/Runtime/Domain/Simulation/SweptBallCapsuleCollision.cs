using System;

namespace Volleyball.Domain.Simulation
{
    public static class SweptBallCapsuleCollision
    {
        private const int TimeSubdivisions = 16;
        private const int BisectionIterations = 10;
        private const float DirectionEpsilon = 0.000001f;

        public static bool TryFindContact(
            BallState ball,
            ContactCapsuleSnapshot capsule,
            float deltaSeconds,
            out SweptBallHit hit)
        {
            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }

            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            hit = default;
            if (!ball.IsActive || !capsule.Active || !ball.CanContact(capsule.ContactGroupId))
            {
                return false;
            }

            var previousAlpha = 0f;
            if (Clearance(ball, capsule, previousAlpha) <= 0f)
            {
                hit = BuildHit(ball, capsule, deltaSeconds, previousAlpha);
                return true;
            }

            for (var sample = 1; sample <= TimeSubdivisions; sample++)
            {
                var alpha = (float)sample / TimeSubdivisions;
                if (Clearance(ball, capsule, alpha) <= 0f)
                {
                    var lower = previousAlpha;
                    var upper = alpha;
                    for (var iteration = 0; iteration < BisectionIterations; iteration++)
                    {
                        var middle = (lower + upper) * 0.5f;
                        if (Clearance(ball, capsule, middle) <= 0f)
                        {
                            upper = middle;
                        }
                        else
                        {
                            lower = middle;
                        }
                    }

                    hit = BuildHit(ball, capsule, deltaSeconds, upper);
                    return true;
                }

                previousAlpha = alpha;
            }

            return false;
        }

        private static float Clearance(
            BallState ball,
            ContactCapsuleSnapshot capsule,
            float alpha)
        {
            var ballCenter = SimVector3.Lerp(ball.PreviousPosition, ball.Position, alpha);
            var frame = capsule.At(alpha);
            var closest = frame.ClosestPoint(ballCenter, out _);
            return (ballCenter - closest).Magnitude - (ball.Radius + frame.Radius);
        }

        private static SweptBallHit BuildHit(
            BallState ball,
            ContactCapsuleSnapshot capsule,
            float deltaSeconds,
            float alpha)
        {
            var ballCenter = SimVector3.Lerp(ball.PreviousPosition, ball.Position, alpha);
            var frame = capsule.At(alpha);
            var closest = frame.ClosestPoint(ballCenter, out var segmentFraction);
            var surfaceVelocity = capsule.VelocityAt(segmentFraction, deltaSeconds);
            var outward = ballCenter - closest;
            var normal = outward.SqrMagnitude > DirectionEpsilon
                ? outward.Normalized
                : FallbackNormal(ball, surfaceVelocity, deltaSeconds);
            var contactPoint = closest + (normal * frame.Radius);
            var impactCenter = contactPoint + (normal * ball.Radius);
            return new SweptBallHit(
                alpha,
                impactCenter,
                contactPoint,
                normal,
                surfaceVelocity,
                capsule.ContactGroupId,
                1f);
        }

        private static SimVector3 FallbackNormal(
            BallState ball,
            SimVector3 surfaceVelocity,
            float deltaSeconds)
        {
            var ballVelocity = (ball.Position - ball.PreviousPosition) / deltaSeconds;
            var relativeVelocity = ballVelocity - surfaceVelocity;
            return relativeVelocity.SqrMagnitude > DirectionEpsilon
                ? -relativeVelocity.Normalized
                : SimVector3.Up;
        }
    }
}
