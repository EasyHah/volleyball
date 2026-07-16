using System;

namespace VolleyballMatch.Domain.Simulation
{
    public enum EnvironmentContactKind
    {
        Ground,
        Net
    }

    public readonly struct NetCollisionGeometry
    {
        public NetCollisionGeometry(float halfWidth, float height, float thickness, float bottomHeight)
        {
            HalfWidth = ValidatePositive(halfWidth, nameof(halfWidth));
            Height = ValidatePositive(height, nameof(height));
            Thickness = ValidatePositive(thickness, nameof(thickness));
            if (!IsFinite(bottomHeight) || bottomHeight < 0f || bottomHeight >= height)
            {
                throw new ArgumentOutOfRangeException(nameof(bottomHeight), bottomHeight, "Bottom height must be finite and below net height.");
            }

            BottomHeight = bottomHeight;
        }

        public float HalfWidth { get; }

        public float Height { get; }

        public float Thickness { get; }

        public float BottomHeight { get; }

        private static float ValidatePositive(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and positive.");
            }

            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct EnvironmentCollisionHit
    {
        public EnvironmentCollisionHit(
            EnvironmentContactKind kind,
            float timeFraction,
            SimVector3 impactCenter,
            SimVector3 contactPoint,
            SimVector3 normal)
        {
            Kind = kind;
            TimeFraction = timeFraction;
            ImpactCenter = impactCenter;
            ContactPoint = contactPoint;
            Normal = normal;
        }

        public EnvironmentContactKind Kind { get; }

        public float TimeFraction { get; }

        public SimVector3 ImpactCenter { get; }

        public SimVector3 ContactPoint { get; }

        public SimVector3 Normal { get; }
    }

    public static class EnvironmentCollision
    {
        private const float Epsilon = 0.000001f;
        private const int GroundContactGroup = -1001;
        private const int NetContactGroup = -1002;

        public static bool TryGround(BallState ball, float groundHeight, out EnvironmentCollisionHit hit)
        {
            ValidateBall(ball);
            if (!IsFinite(groundHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(groundHeight));
            }

            hit = default;
            if (!ball.CanContact(GroundContactGroup) || ball.Velocity.Y >= 0f)
            {
                return false;
            }

            var contactCenterHeight = groundHeight + ball.Radius;
            var previousDistance = ball.PreviousPosition.Y - contactCenterHeight;
            var currentDistance = ball.Position.Y - contactCenterHeight;
            if (previousDistance < 0f || currentDistance > 0f)
            {
                return false;
            }

            var denominator = previousDistance - currentDistance;
            var timeFraction = denominator <= Epsilon ? 0f : Clamp01(previousDistance / denominator);
            var center = SimVector3.Lerp(ball.PreviousPosition, ball.Position, timeFraction);
            center = new SimVector3(center.X, contactCenterHeight, center.Z);
            hit = new EnvironmentCollisionHit(
                EnvironmentContactKind.Ground,
                timeFraction,
                center,
                new SimVector3(center.X, groundHeight, center.Z),
                SimVector3.Up);
            return true;
        }

        public static bool TryNet(
            BallState ball,
            NetCollisionGeometry geometry,
            out EnvironmentCollisionHit hit)
        {
            ValidateBall(ball);
            hit = default;
            if (!ball.CanContact(NetContactGroup))
            {
                return false;
            }

            var radius = ball.Radius;
            var minimum = new SimVector3(
                -geometry.HalfWidth - radius,
                geometry.BottomHeight - radius,
                (-geometry.Thickness * 0.5f) - radius);
            var maximum = new SimVector3(
                geometry.HalfWidth + radius,
                geometry.Height + radius,
                (geometry.Thickness * 0.5f) + radius);
            if (!TrySegmentAabb(ball.PreviousPosition, ball.Position, minimum, maximum, out var fraction, out var normal))
            {
                return false;
            }

            var center = SimVector3.Lerp(ball.PreviousPosition, ball.Position, fraction);
            hit = new EnvironmentCollisionHit(
                EnvironmentContactKind.Net,
                fraction,
                center,
                center - (normal * radius),
                normal);
            return true;
        }

        public static void ApplyResponse(
            BallState ball,
            EnvironmentCollisionHit hit,
            float restitution,
            float tangentialFriction)
        {
            ValidateBall(ball);
            ValidateUnit(restitution, nameof(restitution));
            ValidateUnit(tangentialFriction, nameof(tangentialFriction));
            var normalSpeed = SimVector3.Dot(ball.Velocity, hit.Normal);
            var normalVelocity = hit.Normal * normalSpeed;
            var tangentVelocity = ball.Velocity - normalVelocity;
            ball.Position = hit.ImpactCenter;
            ball.PreviousPosition = hit.ImpactCenter;
            ball.Velocity = normalSpeed < 0f
                ? (-normalVelocity * restitution) + (tangentVelocity * (1f - tangentialFriction))
                : tangentVelocity * (1f - tangentialFriction);
            ball.RegisterContact(
                hit.Kind == EnvironmentContactKind.Ground ? GroundContactGroup : NetContactGroup,
                0.04f);
        }

        private static bool TrySegmentAabb(
            SimVector3 start,
            SimVector3 end,
            SimVector3 minimum,
            SimVector3 maximum,
            out float timeFraction,
            out SimVector3 normal)
        {
            timeFraction = 0f;
            normal = SimVector3.Zero;
            if (IsInside(start, minimum, maximum))
            {
                return false;
            }

            var direction = end - start;
            var earliest = 0f;
            var latest = 1f;
            if (!ClipAxis(start.X, direction.X, minimum.X, maximum.X, new SimVector3(-1f, 0f, 0f), new SimVector3(1f, 0f, 0f), ref earliest, ref latest, ref normal) ||
                !ClipAxis(start.Y, direction.Y, minimum.Y, maximum.Y, new SimVector3(0f, -1f, 0f), new SimVector3(0f, 1f, 0f), ref earliest, ref latest, ref normal) ||
                !ClipAxis(start.Z, direction.Z, minimum.Z, maximum.Z, new SimVector3(0f, 0f, -1f), new SimVector3(0f, 0f, 1f), ref earliest, ref latest, ref normal))
            {
                return false;
            }

            timeFraction = earliest;
            return earliest >= 0f && earliest <= 1f && normal.SqrMagnitude > 0f;
        }

        private static bool ClipAxis(
            float start,
            float direction,
            float minimum,
            float maximum,
            SimVector3 minimumNormal,
            SimVector3 maximumNormal,
            ref float earliest,
            ref float latest,
            ref SimVector3 hitNormal)
        {
            if (Math.Abs(direction) <= Epsilon)
            {
                return start >= minimum && start <= maximum;
            }

            var near = (minimum - start) / direction;
            var far = (maximum - start) / direction;
            var nearNormal = minimumNormal;
            if (near > far)
            {
                var temporary = near;
                near = far;
                far = temporary;
                nearNormal = maximumNormal;
            }

            if (near > earliest)
            {
                earliest = near;
                hitNormal = nearNormal;
            }

            latest = Math.Min(latest, far);
            return earliest <= latest;
        }

        private static bool IsInside(SimVector3 point, SimVector3 minimum, SimVector3 maximum)
        {
            return point.X >= minimum.X && point.X <= maximum.X &&
                   point.Y >= minimum.Y && point.Y <= maximum.Y &&
                   point.Z >= minimum.Z && point.Z <= maximum.Z;
        }

        private static void ValidateBall(BallState ball)
        {
            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }
        }

        private static void ValidateUnit(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be in the range [0, 1].");
            }
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
