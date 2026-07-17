using System;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.Domain.Players
{
    public readonly struct SkillExecutionError : IEquatable<SkillExecutionError>
    {
        public SkillExecutionError(
            float reactionDelay,
            SimVector3 contactPositionError,
            SimVector3 contactNormalErrorDegrees,
            float contactTimingError,
            float surfaceSpeedScale,
            SimVector3 targetVelocityError,
            float maximumTechniqueControl)
        {
            ReactionDelay = reactionDelay;
            ContactPositionError = contactPositionError;
            ContactNormalErrorDegrees = contactNormalErrorDegrees;
            ContactTimingError = contactTimingError;
            SurfaceSpeedScale = surfaceSpeedScale;
            TargetVelocityError = targetVelocityError;
            MaximumTechniqueControl = maximumTechniqueControl;
        }

        public float ReactionDelay { get; }

        public SimVector3 ContactPositionError { get; }

        public SimVector3 ContactNormalErrorDegrees { get; }

        public float ContactTimingError { get; }

        public float SurfaceSpeedScale { get; }

        public SimVector3 TargetVelocityError { get; }

        public float MaximumTechniqueControl { get; }

        public float Magnitude =>
            Math.Abs(ReactionDelay) + ContactPositionError.Magnitude +
            (ContactNormalErrorDegrees.Magnitude / 90f) + Math.Abs(ContactTimingError) +
            Math.Abs(1f - SurfaceSpeedScale) + (TargetVelocityError.Magnitude / 10f);

        public bool Equals(SkillExecutionError other)
        {
            return ReactionDelay.Equals(other.ReactionDelay) &&
                   ContactPositionError.Equals(other.ContactPositionError) &&
                   ContactNormalErrorDegrees.Equals(other.ContactNormalErrorDegrees) &&
                   ContactTimingError.Equals(other.ContactTimingError) &&
                   SurfaceSpeedScale.Equals(other.SurfaceSpeedScale) &&
                   TargetVelocityError.Equals(other.TargetVelocityError) &&
                   MaximumTechniqueControl.Equals(other.MaximumTechniqueControl);
        }

        public override bool Equals(object obj)
        {
            return obj is SkillExecutionError other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ReactionDelay.GetHashCode();
                hashCode = (hashCode * 397) ^ ContactPositionError.GetHashCode();
                hashCode = (hashCode * 397) ^ ContactNormalErrorDegrees.GetHashCode();
                hashCode = (hashCode * 397) ^ ContactTimingError.GetHashCode();
                hashCode = (hashCode * 397) ^ SurfaceSpeedScale.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetVelocityError.GetHashCode();
                return (hashCode * 397) ^ MaximumTechniqueControl.GetHashCode();
            }
        }
    }
}
