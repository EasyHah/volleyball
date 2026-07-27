using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public enum SetQualityGrade
    {
        A,
        B,
        C,
        D,
        E
    }

    public enum AttackContactOutcome
    {
        FullAttack,
        AdjustedAttack,
        Handling
    }

    public readonly struct AttackContactInput
    {
        public AttackContactInput(
            float maxAttackReach,
            float approachCompletion,
            float jumpTiming,
            SetQualityGrade setQuality,
            SimVector3 takeoff,
            float requiredApproachSeconds,
            float availableApproachSeconds)
        {
            MaxAttackReach = ValidateRange(maxAttackReach, 3.20f, 3.55f, nameof(maxAttackReach));
            ApproachCompletion = ValidateRange(approachCompletion, 0f, 1f, nameof(approachCompletion));
            JumpTiming = ValidateRange(jumpTiming, 0f, 1f, nameof(jumpTiming));
            if (!Enum.IsDefined(typeof(SetQualityGrade), setQuality))
            {
                throw new ArgumentOutOfRangeException(nameof(setQuality));
            }

            if (!takeoff.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(takeoff));
            }

            RequiredApproachSeconds = ValidateNonNegative(
                requiredApproachSeconds,
                nameof(requiredApproachSeconds));
            AvailableApproachSeconds = ValidateNonNegative(
                availableApproachSeconds,
                nameof(availableApproachSeconds));
            SetQuality = setQuality;
            Takeoff = takeoff;
            Validate();
        }

        public float MaxAttackReach { get; }

        public float ApproachCompletion { get; }

        public float JumpTiming { get; }

        public SetQualityGrade SetQuality { get; }

        public SimVector3 Takeoff { get; }

        public float RequiredApproachSeconds { get; }

        public float AvailableApproachSeconds { get; }

        internal void Validate()
        {
            ValidateRange(MaxAttackReach, 3.20f, 3.55f, nameof(MaxAttackReach));
            ValidateRange(ApproachCompletion, 0f, 1f, nameof(ApproachCompletion));
            ValidateRange(JumpTiming, 0f, 1f, nameof(JumpTiming));
            if (!Enum.IsDefined(typeof(SetQualityGrade), SetQuality))
            {
                throw new ArgumentOutOfRangeException(nameof(SetQuality));
            }

            if (!Takeoff.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(Takeoff));
            }

            ValidateNonNegative(RequiredApproachSeconds, nameof(RequiredApproachSeconds));
            ValidateNonNegative(AvailableApproachSeconds, nameof(AvailableApproachSeconds));
        }

        private static float ValidateRange(float value, float minimum, float maximum, string parameterName)
        {
            if (!IsFinite(value) || value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static float ValidateNonNegative(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct AttackContactPlan : IEquatable<AttackContactPlan>
    {
        public AttackContactPlan(
            SimVector3 takeoff,
            SimVector3 contactCenter,
            float approachCompletion,
            float jumpTiming,
            float requiredApproachSeconds,
            float availableApproachSeconds,
            AttackContactOutcome outcome)
        {
            if (!takeoff.IsFinite || !contactCenter.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(takeoff));
            }

            ApproachCompletion = ValidateUnit(approachCompletion, nameof(approachCompletion));
            JumpTiming = ValidateUnit(jumpTiming, nameof(jumpTiming));
            RequiredApproachSeconds = ValidateNonNegative(
                requiredApproachSeconds,
                nameof(requiredApproachSeconds));
            AvailableApproachSeconds = ValidateNonNegative(
                availableApproachSeconds,
                nameof(availableApproachSeconds));
            if (!Enum.IsDefined(typeof(AttackContactOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            Takeoff = takeoff;
            ContactCenter = contactCenter;
            Outcome = outcome;
            Validate();
        }

        public SimVector3 Takeoff { get; }

        public SimVector3 ContactCenter { get; }

        public float ApproachCompletion { get; }

        public float JumpTiming { get; }

        public float RequiredApproachSeconds { get; }

        public float AvailableApproachSeconds { get; }

        public AttackContactOutcome Outcome { get; }

        public void Validate()
        {
            var minimumHeight = Outcome == AttackContactOutcome.Handling
                ? 0f
                : AttackContactPlanner.MinimumAttackReach;
            if (!Takeoff.IsFinite || !ContactCenter.IsFinite ||
                ContactCenter.Y < minimumHeight ||
                ContactCenter.Y > 3.55f)
            {
                throw new ArgumentOutOfRangeException(nameof(ContactCenter));
            }

            ValidateUnit(ApproachCompletion, nameof(ApproachCompletion));
            ValidateUnit(JumpTiming, nameof(JumpTiming));
            ValidateNonNegative(RequiredApproachSeconds, nameof(RequiredApproachSeconds));
            ValidateNonNegative(AvailableApproachSeconds, nameof(AvailableApproachSeconds));
            if (!Enum.IsDefined(typeof(AttackContactOutcome), Outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(Outcome));
            }
        }

        public bool Equals(AttackContactPlan other)
        {
            return Takeoff.Equals(other.Takeoff) &&
                   ContactCenter.Equals(other.ContactCenter) &&
                   ApproachCompletion.Equals(other.ApproachCompletion) &&
                   JumpTiming.Equals(other.JumpTiming) &&
                   RequiredApproachSeconds.Equals(other.RequiredApproachSeconds) &&
                   AvailableApproachSeconds.Equals(other.AvailableApproachSeconds) &&
                   Outcome == other.Outcome;
        }

        public override bool Equals(object obj)
        {
            return obj is AttackContactPlan other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Takeoff.GetHashCode();
                hashCode = (hashCode * 397) ^ ContactCenter.GetHashCode();
                hashCode = (hashCode * 397) ^ ApproachCompletion.GetHashCode();
                hashCode = (hashCode * 397) ^ JumpTiming.GetHashCode();
                hashCode = (hashCode * 397) ^ RequiredApproachSeconds.GetHashCode();
                hashCode = (hashCode * 397) ^ AvailableApproachSeconds.GetHashCode();
                return (hashCode * 397) ^ (int)Outcome;
            }
        }

        private static float ValidateUnit(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static float ValidateNonNegative(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    public static class AttackContactPlanner
    {
        public const float MinimumAttackReach = 3.20f;

        private const float MinimumNormalAttackReadiness = 0.20f;

        public static AttackContactPlan Plan(AttackContactInput input)
        {
            input.Validate();
            var timeCompletion = input.RequiredApproachSeconds <= 0.00001f
                ? 1f
                : Clamp(input.AvailableApproachSeconds / input.RequiredApproachSeconds, 0f, 1f);
            var readiness = Math.Min(
                input.ApproachCompletion,
                Math.Min(input.JumpTiming, timeCompletion));
            var handling = input.SetQuality >= SetQualityGrade.D ||
                           timeCompletion < MinimumNormalAttackReadiness ||
                           readiness < MinimumNormalAttackReadiness;
            var height = handling
                ? MinimumAttackReach
                : MinimumAttackReach +
                  ((input.MaxAttackReach - MinimumAttackReach) * readiness * QualityFactor(input.SetQuality));
            height = Clamp(height, MinimumAttackReach, input.MaxAttackReach);
            var outcome = handling
                ? AttackContactOutcome.Handling
                : input.SetQuality == SetQualityGrade.A && readiness >= 0.95f
                    ? AttackContactOutcome.FullAttack
                    : AttackContactOutcome.AdjustedAttack;

            return new AttackContactPlan(
                input.Takeoff,
                new SimVector3(input.Takeoff.X, height, input.Takeoff.Z),
                input.ApproachCompletion,
                input.JumpTiming,
                input.RequiredApproachSeconds,
                input.AvailableApproachSeconds,
                outcome);
        }

        private static float QualityFactor(SetQualityGrade grade)
        {
            return grade switch
            {
                SetQualityGrade.A => 1f,
                SetQualityGrade.B => 0.92f,
                SetQualityGrade.C => 0.78f,
                SetQualityGrade.D => 0.55f,
                SetQualityGrade.E => 0f,
                _ => throw new ArgumentOutOfRangeException(nameof(grade))
            };
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
