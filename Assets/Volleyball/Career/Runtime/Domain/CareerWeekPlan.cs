using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Career.Domain
{
    public sealed class CareerWeekPlan
    {
        public const int SlotCount = 3;

        private readonly CareerWeekAction[] _slots;
        private readonly ReadOnlyCollection<CareerWeekAction> _readOnlySlots;

        public CareerWeekPlan(WeekPlanId planId, int season, int week)
        {
            CareerIdentityGuard.NotEmpty(planId.Value, nameof(planId));
            if (season < 1 || season > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(season), season, "A career contains seasons 1 through 6.");
            }

            if (week < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(week), week, "Week must be positive.");
            }

            PlanId = planId;
            Season = season;
            Week = week;
            _slots = new CareerWeekAction[SlotCount];
            _readOnlySlots = Array.AsReadOnly(_slots);
        }

        public WeekPlanId PlanId { get; }

        public int Season { get; }

        public int Week { get; }

        public IReadOnlyList<CareerWeekAction> Slots => _readOnlySlots;

        public bool IsConfirmed { get; private set; }

        public bool CanConfirm
        {
            get
            {
                if (IsConfirmed)
                {
                    return false;
                }

                for (var index = 0; index < _slots.Length; index++)
                {
                    if (_slots[index] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void ReserveMatch(int slotIndex, CareerWeekAction matchAction)
        {
            EnsureEditable();
            ValidateSlotIndex(slotIndex);
            if (matchAction == null)
            {
                throw new ArgumentNullException(nameof(matchAction));
            }

            if (!matchAction.IsMatch)
            {
                throw new ArgumentException("Only a match action can reserve a match slot.", nameof(matchAction));
            }

            if (_slots[slotIndex] != null)
            {
                throw new InvalidOperationException("A match cannot reserve an occupied action slot.");
            }

            EnsureUniqueIdentity(matchAction, slotIndex);
            _slots[slotIndex] = matchAction;
        }

        public void ScheduleAction(int slotIndex, CareerWeekAction action)
        {
            EnsureEditable();
            ValidateSlotIndex(slotIndex);
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (action.IsMatch)
            {
                throw new ArgumentException("Match actions must be added through ReserveMatch.", nameof(action));
            }

            if (_slots[slotIndex]?.IsMatch == true)
            {
                throw new InvalidOperationException("A reserved match slot cannot be overwritten.");
            }

            EnsureUniqueIdentity(action, slotIndex);
            _slots[slotIndex] = action;
        }

        public CareerWeekAction RemoveAction(int slotIndex)
        {
            EnsureEditable();
            ValidateSlotIndex(slotIndex);
            var action = _slots[slotIndex];
            if (action?.IsMatch == true)
            {
                throw new InvalidOperationException("A reserved match slot cannot be removed.");
            }

            _slots[slotIndex] = null;
            return action;
        }

        public void MoveAction(int sourceSlotIndex, int destinationSlotIndex)
        {
            EnsureEditable();
            ValidateSlotIndex(sourceSlotIndex);
            ValidateSlotIndex(destinationSlotIndex);
            if (sourceSlotIndex == destinationSlotIndex)
            {
                return;
            }

            var source = _slots[sourceSlotIndex];
            var destination = _slots[destinationSlotIndex];
            if (source == null)
            {
                throw new InvalidOperationException("The source action slot is empty.");
            }

            if (source.IsMatch || destination?.IsMatch == true)
            {
                throw new InvalidOperationException("A reserved match slot cannot be moved or swapped.");
            }

            _slots[sourceSlotIndex] = destination;
            _slots[destinationSlotIndex] = source;
        }

        public void Confirm()
        {
            EnsureEditable();
            if (!CanConfirm)
            {
                throw new InvalidOperationException("All three action slots must be filled before confirmation.");
            }

            IsConfirmed = true;
        }

        private void EnsureUniqueIdentity(CareerWeekAction action, int targetSlotIndex)
        {
            for (var index = 0; index < _slots.Length; index++)
            {
                if (index == targetSlotIndex || _slots[index] == null)
                {
                    continue;
                }

                if (_slots[index].SlotActionId.Equals(action.SlotActionId))
                {
                    throw new ArgumentException("Slot action IDs must be unique inside a week plan.", nameof(action));
                }

                if (_slots[index].OccurrenceId.Equals(action.OccurrenceId))
                {
                    throw new ArgumentException("Occurrence IDs must be unique inside a week plan.", nameof(action));
                }
            }
        }

        private void EnsureEditable()
        {
            if (IsConfirmed)
            {
                throw new InvalidOperationException("A confirmed week plan cannot be changed.");
            }
        }

        private static void ValidateSlotIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Action slot index must be 0, 1, or 2.");
            }
        }
    }
}
