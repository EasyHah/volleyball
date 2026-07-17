using System;

namespace Volleyball.Domain.Simulation
{
    public sealed class BallState
    {
        public BallState(SimVector3 position, SimVector3 velocity, float radius, bool active = true)
        {
            ValidateVector(position, nameof(position));
            ValidateVector(velocity, nameof(velocity));
            if (!IsFinite(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Ball radius must be finite and positive.");
            }

            Position = position;
            PreviousPosition = position;
            Velocity = velocity;
            Radius = radius;
            IsActive = active;
        }

        public SimVector3 Position { get; internal set; }

        public SimVector3 PreviousPosition { get; internal set; }

        public SimVector3 Velocity { get; internal set; }

        public float Radius { get; }

        public bool IsActive { get; private set; }

        public float CollisionCooldownSeconds { get; internal set; }

        public bool IsCoolingDown => CollisionCooldownSeconds > 0f;

        public int? LastContactGroupId { get; private set; }

        public void Reset(SimVector3 position, SimVector3 velocity, bool active = false)
        {
            ValidateVector(position, nameof(position));
            ValidateVector(velocity, nameof(velocity));
            Position = position;
            PreviousPosition = position;
            Velocity = velocity;
            CollisionCooldownSeconds = 0f;
            LastContactGroupId = null;
            IsActive = active;
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void MarkDead()
        {
            IsActive = false;
        }

        public void StartCollisionCooldown(float seconds)
        {
            if (!IsFinite(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Cooldown must be finite and non-negative.");
            }

            CollisionCooldownSeconds = Math.Max(CollisionCooldownSeconds, seconds);
        }

        public bool CanContact(int contactGroupId)
        {
            return !IsCoolingDown || LastContactGroupId != contactGroupId;
        }

        public void RegisterContact(int contactGroupId, float cooldownSeconds)
        {
            StartCollisionCooldown(cooldownSeconds);
            LastContactGroupId = contactGroupId;
        }

        public BallState Clone()
        {
            var clone = new BallState(Position, Velocity, Radius, IsActive)
            {
                PreviousPosition = PreviousPosition,
                CollisionCooldownSeconds = CollisionCooldownSeconds,
                LastContactGroupId = LastContactGroupId
            };
            return clone;
        }

        private static void ValidateVector(SimVector3 value, string parameterName)
        {
            if (!value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Vector components must be finite.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
