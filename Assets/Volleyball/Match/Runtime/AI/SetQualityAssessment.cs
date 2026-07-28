using System;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.AI
{
    public enum AttackOutcome
    {
        InPlay,
        Out,
        NoNormalAttack
    }

    public enum AttackResponsibility
    {
        None,
        Setter,
        Attacker
    }

    public static class SetChainAttribution
    {
        public static PlayerId? ResponsiblePlayer(
            AttackResponsibility responsibility,
            PlayerId? setter,
            PlayerId? attacker,
            PlayerId? fallback)
        {
            return responsibility switch
            {
                AttackResponsibility.Setter => setter ?? fallback,
                AttackResponsibility.Attacker => attacker ?? fallback,
                AttackResponsibility.None => fallback,
                _ => throw new ArgumentOutOfRangeException(nameof(responsibility))
            };
        }
    }

    public readonly struct SetQualityInput
    {
        public SetQualityInput(
            float horizontalError,
            float heightError,
            float arrivalTimeError,
            float netDistance,
            float remainingApproachSeconds)
        {
            HorizontalError = ValidateNonNegative(horizontalError, nameof(horizontalError));
            HeightError = ValidateNonNegative(heightError, nameof(heightError));
            ArrivalTimeError = ValidateNonNegative(arrivalTimeError, nameof(arrivalTimeError));
            NetDistance = ValidateNonNegative(netDistance, nameof(netDistance));
            RemainingApproachSeconds = ValidateNonNegative(
                remainingApproachSeconds,
                nameof(remainingApproachSeconds));
        }

        public float HorizontalError { get; }
        public float HeightError { get; }
        public float ArrivalTimeError { get; }
        public float NetDistance { get; }
        public float RemainingApproachSeconds { get; }

        private static float ValidateNonNegative(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    public readonly struct SetQualityAssessment
    {
        private SetQualityAssessment(SetQualityInput input, SetQualityGrade grade, string reason)
        {
            HorizontalError = input.HorizontalError;
            HeightError = input.HeightError;
            ArrivalTimeError = input.ArrivalTimeError;
            NetDistance = input.NetDistance;
            RemainingApproachSeconds = input.RemainingApproachSeconds;
            Grade = grade;
            IsAdjustable = grade <= SetQualityGrade.C;
            Reason = reason;
        }

        public float HorizontalError { get; }
        public float HeightError { get; }
        public float ArrivalTimeError { get; }
        public float NetDistance { get; }
        public bool IsAdjustable { get; }
        public float RemainingApproachSeconds { get; }
        public SetQualityGrade Grade { get; }
        public string Reason { get; }

        public static SetQualityAssessment Evaluate(SetQualityInput input)
        {
            var grade = GradeFor(input);
            return new SetQualityAssessment(input, grade, BuildReason(input, grade));
        }

        public static AttackResponsibility PrimaryResponsibility(
            SetQualityGrade grade,
            AttackOutcome outcome)
        {
            if (!Enum.IsDefined(typeof(SetQualityGrade), grade))
            {
                throw new ArgumentOutOfRangeException(nameof(grade));
            }

            if (!Enum.IsDefined(typeof(AttackOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            if (outcome == AttackOutcome.NoNormalAttack)
            {
                return grade >= SetQualityGrade.D
                    ? AttackResponsibility.Setter
                    : AttackResponsibility.None;
            }

            if (outcome == AttackOutcome.Out)
            {
                return grade <= SetQualityGrade.B
                    ? AttackResponsibility.Attacker
                    : AttackResponsibility.Setter;
            }

            return AttackResponsibility.None;
        }

        private static SetQualityGrade GradeFor(SetQualityInput input)
        {
            if (Within(input, 0.08f, 0.08f, 0.06f, 0.60f, 0.35f))
            {
                return SetQualityGrade.A;
            }

            if (Within(input, 0.20f, 0.15f, 0.15f, 0.45f, 0.25f))
            {
                return SetQualityGrade.B;
            }

            if (Within(input, 0.35f, 0.30f, 0.25f, 0.30f, 0.18f))
            {
                return SetQualityGrade.C;
            }

            if (Within(input, 0.60f, 0.50f, 0.40f, 0.15f, 0.08f))
            {
                return SetQualityGrade.D;
            }

            return SetQualityGrade.E;
        }

        private static bool Within(
            SetQualityInput input,
            float maximumHorizontal,
            float maximumHeight,
            float maximumTime,
            float minimumNetDistance,
            float minimumApproachSeconds)
        {
            return input.HorizontalError <= maximumHorizontal &&
                   input.HeightError <= maximumHeight &&
                   input.ArrivalTimeError <= maximumTime &&
                   input.NetDistance >= minimumNetDistance &&
                   input.RemainingApproachSeconds >= minimumApproachSeconds;
        }

        private static string BuildReason(SetQualityInput input, SetQualityGrade grade)
        {
            return $"grade={grade}; horizontal={input.HorizontalError:0.000}; " +
                   $"height={input.HeightError:0.000}; arrival={input.ArrivalTimeError:0.000}; " +
                   $"net={input.NetDistance:0.000}; approach={input.RemainingApproachSeconds:0.000}";
        }
    }

    public readonly struct SetAttackReplan
    {
        public SetAttackReplan(
            AttackApproachPlan approach,
            AttackContactPlan contactPlan,
            AttackOutcome outcome,
            bool opensSpikeContactWindow)
        {
            Approach = approach;
            ContactPlan = contactPlan;
            Outcome = outcome;
            OpensSpikeContactWindow = opensSpikeContactWindow;
        }

        public AttackApproachPlan Approach { get; }
        public AttackContactPlan ContactPlan { get; }
        public AttackOutcome Outcome { get; }
        public bool OpensSpikeContactWindow { get; }
    }

    public static class SetAttackReplanner
    {
        private const float MaximumAttackHorizontalReach = 1.25f;

        public static SetAttackReplan Replan(
            AttackApproachPlan provisionalApproach,
            AttackContactPlan provisionalContact,
            SimVector3 actualContactCenter,
            float actualArrivalSeconds,
            float maxAttackReach,
            PlayerRole attackerRole,
            TeamId attackingTeam,
            float setterDepthFromNet,
            SetQualityAssessment quality)
        {
            provisionalContact.Validate();
            if (!actualContactCenter.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(actualContactCenter));
            }

            if (float.IsNaN(actualArrivalSeconds) || float.IsInfinity(actualArrivalSeconds) ||
                actualArrivalSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(actualArrivalSeconds));
            }

            if (float.IsNaN(maxAttackReach) || float.IsInfinity(maxAttackReach) ||
                maxAttackReach < 1.95f || maxAttackReach > 3.95f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttackReach));
            }

            var takeoff = AttackBandPolicy.Resolve(attackerRole, setterDepthFromNet)
                .ConstrainTakeoff(attackingTeam, actualContactCenter);
            var shift = takeoff - provisionalApproach.Takeoff;
            var approach = new AttackApproachPlan(
                provisionalApproach.ApproachStart + shift,
                takeoff,
                provisionalApproach.Distance,
                provisionalApproach.JumpQuality,
                provisionalApproach.AnglePenalty);
            var requiredSeconds = provisionalContact.RequiredApproachSeconds;
            var jumpTiming = requiredSeconds <= 0.00001f
                ? 1f
                : Clamp(actualArrivalSeconds / requiredSeconds, 0f, 1f);
            var planned = AttackContactPlanner.Plan(new AttackContactInput(
                maxAttackReach,
                approach.JumpQuality,
                jumpTiming,
                quality.Grade,
                takeoff,
                requiredSeconds,
                actualArrivalSeconds));
            var actualHorizontalReach = GroundDistance(actualContactCenter, takeoff);
            var isHorizontallyAttackable = actualHorizontalReach <= MaximumAttackHorizontalReach;
            var outcome = isHorizontallyAttackable
                ? planned.Outcome
                : AttackContactOutcome.Handling;
            var reachableCenter = new SimVector3(
                actualContactCenter.X,
                Clamp(
                    actualContactCenter.Y,
                    outcome == AttackContactOutcome.Handling
                        ? 0f
                        : AttackContactPlanner.MinimumAttackReach,
                    maxAttackReach),
                actualContactCenter.Z);
            var contact = new AttackContactPlan(
                takeoff,
                reachableCenter,
                planned.ApproachCompletion,
                planned.JumpTiming,
                planned.RequiredApproachSeconds,
                planned.AvailableApproachSeconds,
                outcome);
            var opensSpike = quality.IsAdjustable && outcome != AttackContactOutcome.Handling;
            return new SetAttackReplan(
                approach,
                contact,
                opensSpike ? AttackOutcome.InPlay : AttackOutcome.NoNormalAttack,
                opensSpike);
        }

        private static float GroundDistance(SimVector3 a, SimVector3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
