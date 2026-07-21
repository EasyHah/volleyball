using System;

namespace Volleyball.Career.Domain
{
    public enum CareerWeekActionKind
    {
        SpecializedTraining = 0,
        StrengthTraining = 1,
        TeamPractice = 2,
        Rest = 3,
        Match = 4
    }

    public readonly struct WeekPlanId : IEquatable<WeekPlanId>
    {
        public WeekPlanId(Guid value)
        {
            Value = CareerIdentityGuard.NotEmpty(value, nameof(value));
        }

        public Guid Value { get; }

        public bool Equals(WeekPlanId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is WeekPlanId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("D");
        }
    }

    public readonly struct SlotActionId : IEquatable<SlotActionId>
    {
        public SlotActionId(Guid value)
        {
            Value = CareerIdentityGuard.NotEmpty(value, nameof(value));
        }

        public Guid Value { get; }

        public bool Equals(SlotActionId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is SlotActionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("D");
        }
    }

    public readonly struct OccurrenceId : IEquatable<OccurrenceId>
    {
        public OccurrenceId(Guid value)
        {
            Value = CareerIdentityGuard.NotEmpty(value, nameof(value));
        }

        public Guid Value { get; }

        public bool Equals(OccurrenceId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is OccurrenceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("D");
        }
    }

    public sealed class CareerWeekAction
    {
        public CareerWeekAction(
            SlotActionId slotActionId,
            OccurrenceId occurrenceId,
            CareerWeekActionKind kind)
        {
            CareerIdentityGuard.NotEmpty(slotActionId.Value, nameof(slotActionId));
            CareerIdentityGuard.NotEmpty(occurrenceId.Value, nameof(occurrenceId));
            if (!Enum.IsDefined(typeof(CareerWeekActionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown career week action kind.");
            }

            SlotActionId = slotActionId;
            OccurrenceId = occurrenceId;
            Kind = kind;
        }

        public SlotActionId SlotActionId { get; }

        public OccurrenceId OccurrenceId { get; }

        public CareerWeekActionKind Kind { get; }

        public bool IsMatch => Kind == CareerWeekActionKind.Match;
    }

    internal static class CareerIdentityGuard
    {
        public static Guid NotEmpty(Guid value, string parameterName)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A stable non-empty identifier is required.", parameterName);
            }

            return value;
        }
    }
}
