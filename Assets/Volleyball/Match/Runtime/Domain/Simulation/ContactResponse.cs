using System;

namespace Volleyball.Domain.Simulation
{
    public readonly struct ContactResponseParameters
    {
        public ContactResponseParameters(
            float restitution,
            float velocityTransfer,
            float tangentialFriction,
            float cooldownSeconds)
        {
            Restitution = ValidateUnit(restitution, nameof(restitution));
            VelocityTransfer = ValidateUnit(velocityTransfer, nameof(velocityTransfer));
            TangentialFriction = ValidateUnit(tangentialFriction, nameof(tangentialFriction));
            if (!IsFinite(cooldownSeconds) || cooldownSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), cooldownSeconds, "Cooldown must be finite and non-negative.");
            }

            CooldownSeconds = cooldownSeconds;
        }

        public float Restitution { get; }

        public float VelocityTransfer { get; }

        public float TangentialFriction { get; }

        public float CooldownSeconds { get; }

        private static float ValidateUnit(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and in the range [0, 1].");
            }

            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct ContactResponseResult
    {
        public ContactResponseResult(SimVector3 physicalOutgoing)
        {
            PhysicalOutgoing = physicalOutgoing;
        }

        public SimVector3 PhysicalOutgoing { get; }
    }

    public static class ContactResponse
    {
        public static ContactResponseResult Apply(
            BallState ball,
            SweptBallHit hit,
            ContactResponseParameters parameters)
        {
            return ApplyWithSurfaceVelocity(ball, hit, hit.SurfaceVelocity, parameters);
        }

        public static ContactResponseResult ApplyWithSurfaceVelocity(
            BallState ball,
            SweptBallHit hit,
            SimVector3 responseSurfaceVelocity,
            ContactResponseParameters parameters)
        {
            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }

            if (!responseSurfaceVelocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(responseSurfaceVelocity),
                    responseSurfaceVelocity,
                    "Response surface velocity must be finite.");
            }

            var relativeIncoming = ball.Velocity - responseSurfaceVelocity;
            var incomingNormalSpeed = SimVector3.Dot(relativeIncoming, hit.Normal);
            var incomingNormal = hit.Normal * incomingNormalSpeed;
            var incomingTangent = relativeIncoming - incomingNormal;
            var outgoingNormal = incomingNormalSpeed < 0f
                ? -incomingNormal * parameters.Restitution
                : SimVector3.Zero;
            var outgoingTangent = incomingTangent * (1f - parameters.TangentialFriction);
            var physicalOutgoing = outgoingNormal + outgoingTangent +
                                   (responseSurfaceVelocity * parameters.VelocityTransfer);
            ball.Position = hit.ImpactCenter;
            ball.PreviousPosition = hit.ImpactCenter;
            ball.Velocity = physicalOutgoing;
            ball.RegisterContact(hit.ContactGroupId, parameters.CooldownSeconds);
            return new ContactResponseResult(physicalOutgoing);
        }

        public static void ApplyTechniqueVelocity(BallState ball, SweptBallHit hit, SimVector3 finalOutgoing)
        {
            if (ball == null)
            {
                throw new ArgumentNullException(nameof(ball));
            }

            if (!finalOutgoing.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(finalOutgoing));
            }

            if (ball.LastContactGroupId != hit.ContactGroupId || !ball.IsCoolingDown)
            {
                throw new InvalidOperationException("Technique velocity can only follow the matching committed physical contact.");
            }

            ball.Velocity = finalOutgoing;
        }
    }
}
