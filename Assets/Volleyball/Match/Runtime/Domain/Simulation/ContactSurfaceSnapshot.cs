using System;

namespace Volleyball.Domain.Simulation
{
    public readonly struct ContactSurfaceFrame
    {
        private const float OrthogonalTolerance = 0.01f;

        public ContactSurfaceFrame(
            SimVector3 origin,
            SimVector3 normal,
            SimVector3 right,
            SimVector3 up,
            float width,
            float height)
        {
            ValidateVector(origin, nameof(origin));
            ValidateDirection(normal, nameof(normal));
            ValidateDirection(right, nameof(right));
            ValidateDirection(up, nameof(up));
            ValidateSize(width, nameof(width));
            ValidateSize(height, nameof(height));

            Normal = normal.Normalized;
            Right = right.Normalized;
            Up = up.Normalized;
            if (Math.Abs(SimVector3.Dot(Normal, Right)) > OrthogonalTolerance ||
                Math.Abs(SimVector3.Dot(Normal, Up)) > OrthogonalTolerance ||
                Math.Abs(SimVector3.Dot(Right, Up)) > OrthogonalTolerance)
            {
                throw new ArgumentException("Contact surface basis directions must be mutually orthogonal.");
            }

            Origin = origin;
            Width = width;
            Height = height;
        }

        public SimVector3 Origin { get; }

        public SimVector3 Normal { get; }

        public SimVector3 Right { get; }

        public SimVector3 Up { get; }

        public float Width { get; }

        public float Height { get; }

        public static ContactSurfaceFrame Lerp(ContactSurfaceFrame previous, ContactSurfaceFrame current, float alpha)
        {
            var normal = SimVector3.Lerp(previous.Normal, current.Normal, alpha).Normalized;
            var blendedRight = SimVector3.Lerp(previous.Right, current.Right, alpha);
            var right = (blendedRight - (normal * SimVector3.Dot(blendedRight, normal))).Normalized;
            var up = SimVector3.Cross(normal, right).Normalized;
            var blendedUp = SimVector3.Lerp(previous.Up, current.Up, alpha);
            if (SimVector3.Dot(up, blendedUp) < 0f)
            {
                up = -up;
            }

            return new ContactSurfaceFrame(
                SimVector3.Lerp(previous.Origin, current.Origin, alpha),
                normal,
                right,
                up,
                previous.Width + ((current.Width - previous.Width) * alpha),
                previous.Height + ((current.Height - previous.Height) * alpha));
        }

        private static void ValidateVector(SimVector3 value, string parameterName)
        {
            if (!value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Vector components must be finite.");
            }
        }

        private static void ValidateDirection(SimVector3 value, string parameterName)
        {
            ValidateVector(value, parameterName);
            if (value.SqrMagnitude <= 0.000001f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Direction must have non-zero length.");
            }
        }

        private static void ValidateSize(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Surface size must be finite and positive.");
            }
        }
    }

    public readonly struct ContactSurfaceSnapshot
    {
        private const float MaximumHumanSurfaceSpeed = 25f;

        public ContactSurfaceSnapshot(
            ContactSurfaceFrame previous,
            ContactSurfaceFrame current,
            bool active,
            int contactGroupId,
            bool twoSided = false)
        {
            Previous = previous;
            Current = current;
            Active = active;
            ContactGroupId = contactGroupId;
            TwoSided = twoSided;
        }

        public ContactSurfaceFrame Previous { get; }

        public ContactSurfaceFrame Current { get; }

        public bool Active { get; }

        public int ContactGroupId { get; }

        public bool TwoSided { get; }

        public ContactSurfaceFrame At(float alpha)
        {
            return ContactSurfaceFrame.Lerp(Previous, Current, alpha);
        }

        public SimVector3 VelocityAt(float rightOffset, float upOffset, float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Step duration must be finite and positive.");
            }

            var previousPoint = Previous.Origin + (Previous.Right * rightOffset) + (Previous.Up * upOffset);
            var currentPoint = Current.Origin + (Current.Right * rightOffset) + (Current.Up * upOffset);
            var velocity = (currentPoint - previousPoint) / deltaSeconds;
            return velocity.Magnitude <= MaximumHumanSurfaceSpeed
                ? velocity
                : velocity.Normalized * MaximumHumanSurfaceSpeed;
        }
    }
}
