using System;

namespace Volleyball.Domain.Simulation
{
    public readonly struct ContactCapsuleFrame
    {
        public ContactCapsuleFrame(SimVector3 start, SimVector3 end, float radius)
        {
            if (!start.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (!end.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(end));
            }

            if (!IsFinite(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            Start = start;
            End = end;
            Radius = radius;
        }

        public SimVector3 Start { get; }

        public SimVector3 End { get; }

        public float Radius { get; }

        public static ContactCapsuleFrame Lerp(
            ContactCapsuleFrame previous,
            ContactCapsuleFrame current,
            float alpha)
        {
            var clamped = Clamp01(alpha);
            return new ContactCapsuleFrame(
                SimVector3.Lerp(previous.Start, current.Start, clamped),
                SimVector3.Lerp(previous.End, current.End, clamped),
                previous.Radius + ((current.Radius - previous.Radius) * clamped));
        }

        public SimVector3 ClosestPoint(SimVector3 point, out float segmentFraction)
        {
            if (!point.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }

            var axis = End - Start;
            var axisLengthSquared = axis.SqrMagnitude;
            if (axisLengthSquared <= 0.000001f)
            {
                segmentFraction = 0f;
                return Start;
            }

            segmentFraction = Clamp01(SimVector3.Dot(point - Start, axis) / axisLengthSquared);
            return Start + (axis * segmentFraction);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    public readonly struct ContactCapsuleSnapshot
    {
        private const float MaximumHumanSurfaceSpeed = 25f;

        public ContactCapsuleSnapshot(
            ContactCapsuleFrame previous,
            ContactCapsuleFrame current,
            bool active,
            int contactGroupId)
        {
            Previous = previous;
            Current = current;
            Active = active;
            ContactGroupId = contactGroupId;
        }

        public ContactCapsuleFrame Previous { get; }

        public ContactCapsuleFrame Current { get; }

        public bool Active { get; }

        public int ContactGroupId { get; }

        public ContactCapsuleFrame At(float alpha)
        {
            return ContactCapsuleFrame.Lerp(Previous, Current, alpha);
        }

        public SimVector3 VelocityAt(float segmentFraction, float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            var fraction = Math.Max(0f, Math.Min(1f, segmentFraction));
            var previousPoint = Previous.Start + ((Previous.End - Previous.Start) * fraction);
            var currentPoint = Current.Start + ((Current.End - Current.Start) * fraction);
            var velocity = (currentPoint - previousPoint) / deltaSeconds;
            return velocity.Magnitude <= MaximumHumanSurfaceSpeed
                ? velocity
                : velocity.Normalized * MaximumHumanSurfaceSpeed;
        }
    }
}
