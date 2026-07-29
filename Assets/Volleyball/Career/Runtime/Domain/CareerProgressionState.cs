using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Career.Domain
{
    public enum CareerProgressionKind
    {
        CareerCreated = 0,
        Tryout = 1,
        Planning = 2,
        Planned = 3,
        AwaitingEventChoice = 4,
        AwaitingMatch = 5
    }

    public enum CareerPhase
    {
        University = 0
    }

    public sealed class CareerWeekActionState
    {
        public CareerWeekActionState(
            SlotActionId slotActionId,
            OccurrenceId occurrenceId,
            CareerWeekActionKind kind,
            string contentId)
        {
            CareerSaveModelGuard.StableId(slotActionId.Value, nameof(slotActionId));
            CareerSaveModelGuard.StableId(occurrenceId.Value, nameof(occurrenceId));
            CareerSaveModelGuard.DefinedEnum(kind, nameof(kind));

            SlotActionId = slotActionId;
            OccurrenceId = occurrenceId;
            Kind = kind;
            ContentId = CareerSaveModelGuard.BusinessId(contentId, nameof(contentId));
        }

        public SlotActionId SlotActionId { get; }

        public OccurrenceId OccurrenceId { get; }

        public CareerWeekActionKind Kind { get; }

        public string ContentId { get; }

        public bool IsMatch => Kind == CareerWeekActionKind.Match;

        internal CareerWeekActionState Copy()
        {
            return new CareerWeekActionState(SlotActionId, OccurrenceId, Kind, ContentId);
        }
    }

    public sealed class CareerWeekPlanState
    {
        private readonly CareerWeekActionState[] _slots;
        private readonly ReadOnlyCollection<CareerWeekActionState> _readOnlySlots;

        public CareerWeekPlanState(CareerWeekPlan source)
            : this(
                SourcePlanId(source),
                source.Season,
                source.Week,
                CopySourceSlots(source),
                source.IsConfirmed)
        {
        }

        public CareerWeekPlanState(
            WeekPlanId planId,
            int season,
            int week,
            IEnumerable<CareerWeekActionState> slots,
            bool isConfirmed)
        {
            CareerSaveModelGuard.StableId(planId.Value, nameof(planId));
            CareerSaveModelGuard.InclusiveRange(season, 1, 6, nameof(season));
            if (week < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(week), week, "Week must be positive.");
            }

            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            var copiedSlots = new List<CareerWeekActionState>(CareerWeekPlan.SlotCount);
            foreach (var slot in slots)
            {
                if (copiedSlots.Count == CareerWeekPlan.SlotCount)
                {
                    throw new ArgumentException(
                        "A week plan state must contain exactly three ordered slots.",
                        nameof(slots));
                }

                copiedSlots.Add(slot?.Copy());
            }

            if (copiedSlots.Count != CareerWeekPlan.SlotCount)
            {
                throw new ArgumentException(
                    "A week plan state must contain exactly three ordered slots.",
                    nameof(slots));
            }

            ValidateSlotIdentitiesAndLayout(copiedSlots, isConfirmed, nameof(slots));

            PlanId = planId;
            Season = season;
            Week = week;
            IsConfirmed = isConfirmed;
            _slots = copiedSlots.ToArray();
            _readOnlySlots = Array.AsReadOnly(_slots);
        }

        public WeekPlanId PlanId { get; }

        public int Season { get; }

        public int Week { get; }

        public IReadOnlyList<CareerWeekActionState> Slots => _readOnlySlots;

        public bool IsConfirmed { get; }

        internal CareerWeekPlanState Copy()
        {
            return new CareerWeekPlanState(PlanId, Season, Week, _slots, IsConfirmed);
        }

        private static void ValidateSlotIdentitiesAndLayout(
            IReadOnlyList<CareerWeekActionState> slots,
            bool isConfirmed,
            string parameterName)
        {
            var slotActionIds = new HashSet<SlotActionId>();
            var occurrenceIds = new HashSet<OccurrenceId>();
            var matchCount = 0;
            var nonNullCount = 0;
            for (var index = 0; index < slots.Count; index++)
            {
                var action = slots[index];
                if (action == null)
                {
                    if (isConfirmed)
                    {
                        throw new ArgumentException(
                            "A confirmed week plan cannot contain an empty slot.",
                            parameterName);
                    }

                    continue;
                }

                nonNullCount++;

                if (!slotActionIds.Add(action.SlotActionId))
                {
                    throw new ArgumentException(
                        "Slot action IDs must be unique inside a week plan state.",
                        parameterName);
                }

                if (!occurrenceIds.Add(action.OccurrenceId))
                {
                    throw new ArgumentException(
                        "Occurrence IDs must be unique inside a week plan state.",
                        parameterName);
                }

                if (action.IsMatch)
                {
                    matchCount++;
                    if (index != 2)
                    {
                        throw new ArgumentException(
                            "A match may only occupy slot 3 in schema V1.",
                            parameterName);
                    }
                }
            }

            if (matchCount > 1)
            {
                throw new ArgumentException(
                    "A week plan may contain at most one match.",
                    parameterName);
            }

            if (nonNullCount == 0 && !isConfirmed)
            {
                return;
            }

            if (matchCount != 1 || slots[2] == null || !slots[2].IsMatch)
            {
                throw new ArgumentException(
                    "Schema V1 requires the only match in slot 3.",
                    parameterName);
            }

            if (isConfirmed && (slots[0] == null || slots[1] == null))
            {
                throw new ArgumentException(
                    "A confirmed schema V1 plan requires non-match actions in slots 1 and 2.",
                    parameterName);
            }
        }

        private static WeekPlanId SourcePlanId(CareerWeekPlan source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.PlanId;
        }

        private static IEnumerable<CareerWeekActionState> CopySourceSlots(CareerWeekPlan source)
        {
            var slots = new CareerWeekActionState[CareerWeekPlan.SlotCount];
            for (var index = 0; index < slots.Length; index++)
            {
                var action = source.Slots[index];
                if (action != null)
                {
                    slots[index] = new CareerWeekActionState(
                        action.SlotActionId,
                        action.OccurrenceId,
                        action.Kind,
                        action.ContentId);
                }
            }

            return slots;
        }
    }

    public sealed class CareerAttributeGrowthDelta
    {
        public CareerAttributeGrowthDelta(
            long spike,
            long serve,
            long reception,
            long defense,
            long block,
            long movement,
            long jump,
            long stamina)
        {
            Spike = Validate(spike, nameof(spike));
            Serve = Validate(serve, nameof(serve));
            Reception = Validate(reception, nameof(reception));
            Defense = Validate(defense, nameof(defense));
            Block = Validate(block, nameof(block));
            Movement = Validate(movement, nameof(movement));
            Jump = Validate(jump, nameof(jump));
            Stamina = Validate(stamina, nameof(stamina));
        }

        public long Spike { get; }

        public long Serve { get; }

        public long Reception { get; }

        public long Defense { get; }

        public long Block { get; }

        public long Movement { get; }

        public long Jump { get; }

        public long Stamina { get; }

        public long Total
        {
            get
            {
                checked
                {
                    return Spike + Serve + Reception + Defense +
                           Block + Movement + Jump + Stamina;
                }
            }
        }

        public long Get(CareerTrainingDirection direction)
        {
            switch (direction)
            {
                case CareerTrainingDirection.Spike: return Spike;
                case CareerTrainingDirection.Serve: return Serve;
                case CareerTrainingDirection.Reception: return Reception;
                case CareerTrainingDirection.Defense: return Defense;
                case CareerTrainingDirection.Block: return Block;
                case CareerTrainingDirection.Movement: return Movement;
                case CareerTrainingDirection.Jump: return Jump;
                case CareerTrainingDirection.Stamina: return Stamina;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        internal CareerAttributeGrowthDelta Copy()
        {
            return new CareerAttributeGrowthDelta(
                Spike,
                Serve,
                Reception,
                Defense,
                Block,
                Movement,
                Jump,
                Stamina);
        }

        private static long Validate(long value, string parameterName)
        {
            if (value < 0 || value > CareerSaveModelGuard.MaximumIJsonSafeInteger)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A growth experience delta must be in the I-JSON safe non-negative range.");
            }

            return value;
        }
    }

    public sealed class CareerEventOptionEffect
    {
        public CareerEventOptionEffect(
            string optionId,
            CareerAttributeGrowthDelta growthExperienceDelta,
            int fatigueDelta,
            int mindsetDelta,
            int coachTrustDelta)
        {
            OptionId = CareerSaveModelGuard.BusinessId(optionId, nameof(optionId));
            if (growthExperienceDelta == null)
            {
                throw new ArgumentNullException(nameof(growthExperienceDelta));
            }

            GrowthExperienceDelta = growthExperienceDelta.Copy();
            FatigueDelta = CareerSaveModelGuard.InclusiveRange(
                fatigueDelta,
                -100,
                100,
                nameof(fatigueDelta));
            MindsetDelta = CareerSaveModelGuard.InclusiveRange(
                mindsetDelta,
                -100,
                100,
                nameof(mindsetDelta));
            CoachTrustDelta = CareerSaveModelGuard.InclusiveRange(
                coachTrustDelta,
                -100,
                100,
                nameof(coachTrustDelta));
        }

        public string OptionId { get; }

        public CareerAttributeGrowthDelta GrowthExperienceDelta { get; }

        public int FatigueDelta { get; }

        public int MindsetDelta { get; }

        public int CoachTrustDelta { get; }

        internal CareerEventOptionEffect Copy()
        {
            return new CareerEventOptionEffect(
                OptionId,
                GrowthExperienceDelta,
                FatigueDelta,
                MindsetDelta,
                CoachTrustDelta);
        }
    }

    public sealed class PendingCareerEvent
    {
        private readonly CareerEventOptionEffect[] _options;
        private readonly ReadOnlyCollection<CareerEventOptionEffect> _readOnlyOptions;

        public PendingCareerEvent(
            WeekPlanId sourceWeekPlanId,
            SlotActionId sourceSlotActionId,
            OccurrenceId sourceActionOccurrenceId,
            string eventId,
            OccurrenceId occurrenceId,
            int randomVersion,
            CareerEventOptionEffect firstOption,
            CareerEventOptionEffect secondOption,
            int resumeAtSlotNumber = 2)
        {
            CareerSaveModelGuard.StableId(sourceWeekPlanId.Value, nameof(sourceWeekPlanId));
            CareerSaveModelGuard.StableId(sourceSlotActionId.Value, nameof(sourceSlotActionId));
            CareerSaveModelGuard.StableId(
                sourceActionOccurrenceId.Value,
                nameof(sourceActionOccurrenceId));
            CareerSaveModelGuard.StableId(occurrenceId.Value, nameof(occurrenceId));
            if (firstOption == null)
            {
                throw new ArgumentNullException(nameof(firstOption));
            }

            if (secondOption == null)
            {
                throw new ArgumentNullException(nameof(secondOption));
            }

            if (string.Equals(firstOption.OptionId, secondOption.OptionId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A pending event must contain two different option IDs.",
                    nameof(secondOption));
            }

            if (resumeAtSlotNumber != 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resumeAtSlotNumber),
                    resumeAtSlotNumber,
                    "The schema V1 fixed event resumes at slot 2.");
            }

            SourceWeekPlanId = sourceWeekPlanId;
            SourceSlotActionId = sourceSlotActionId;
            SourceActionOccurrenceId = sourceActionOccurrenceId;
            EventId = CareerSaveModelGuard.BusinessId(eventId, nameof(eventId));
            OccurrenceId = occurrenceId;
            RandomVersion = CareerSaveModelGuard.PositiveVersion(randomVersion, nameof(randomVersion));
            ResumeAtSlotNumber = resumeAtSlotNumber;
            _options = new[] { firstOption.Copy(), secondOption.Copy() };
            _readOnlyOptions = Array.AsReadOnly(_options);
        }

        public WeekPlanId SourceWeekPlanId { get; }

        public SlotActionId SourceSlotActionId { get; }

        public OccurrenceId SourceActionOccurrenceId { get; }

        public string EventId { get; }

        public OccurrenceId OccurrenceId { get; }

        public int RandomVersion { get; }

        public int ResumeAtSlotNumber { get; }

        public IReadOnlyList<CareerEventOptionEffect> Options => _readOnlyOptions;

        internal PendingCareerEvent Copy()
        {
            return new PendingCareerEvent(
                SourceWeekPlanId,
                SourceSlotActionId,
                SourceActionOccurrenceId,
                EventId,
                OccurrenceId,
                RandomVersion,
                _options[0],
                _options[1],
                ResumeAtSlotNumber);
        }
    }

    public sealed class CareerProgressionState
    {
        public CareerProgressionState(
            CareerProgressionKind kind,
            CareerPhase phase,
            int tryoutStage,
            CareerWeekPlanState weekPlan,
            int nextSlotNumber,
            PendingCareerEvent pendingEvent)
            : this(
                kind,
                phase,
                tryoutStage,
                weekPlan,
                nextSlotNumber,
                pendingEvent,
                null)
        {
        }

        public CareerProgressionState(
            CareerProgressionKind kind,
            CareerPhase phase,
            int tryoutStage,
            CareerWeekPlanState weekPlan,
            int nextSlotNumber,
            PendingCareerEvent pendingEvent,
            Guid? matchSessionId)
        {
            CareerSaveModelGuard.DefinedEnum(kind, nameof(kind));
            CareerSaveModelGuard.DefinedEnum(phase, nameof(phase));

            var copiedWeekPlan = weekPlan?.Copy();
            var copiedPendingEvent = pendingEvent?.Copy();
            ValidateCombination(
                kind,
                tryoutStage,
                copiedWeekPlan,
                nextSlotNumber,
                copiedPendingEvent,
                matchSessionId);

            Kind = kind;
            Phase = phase;
            TryoutStage = tryoutStage;
            WeekPlan = copiedWeekPlan;
            NextSlotNumber = nextSlotNumber;
            PendingEvent = copiedPendingEvent;
            MatchSessionId = matchSessionId;
        }

        public CareerProgressionKind Kind { get; }

        public CareerPhase Phase { get; }

        public int TryoutStage { get; }

        public CareerWeekPlanState WeekPlan { get; }

        public int NextSlotNumber { get; }

        public PendingCareerEvent PendingEvent { get; }

        public Guid? MatchSessionId { get; }

        public static CareerProgressionState Created()
        {
            return new CareerProgressionState(
                CareerProgressionKind.CareerCreated,
                CareerPhase.University,
                0,
                null,
                0,
                null);
        }

        public static CareerProgressionState Tryout(int stage)
        {
            return new CareerProgressionState(
                CareerProgressionKind.Tryout,
                CareerPhase.University,
                stage,
                null,
                0,
                null);
        }

        public static CareerProgressionState Planning(CareerWeekPlanState weekPlan)
        {
            return new CareerProgressionState(
                CareerProgressionKind.Planning,
                CareerPhase.University,
                0,
                weekPlan,
                0,
                null);
        }

        public static CareerProgressionState Planned(
            CareerWeekPlanState weekPlan,
            int nextSlotNumber)
        {
            return new CareerProgressionState(
                CareerProgressionKind.Planned,
                CareerPhase.University,
                0,
                weekPlan,
                nextSlotNumber,
                null);
        }

        public static CareerProgressionState AwaitingEventChoice(
            CareerWeekPlanState weekPlan,
            PendingCareerEvent pendingEvent)
        {
            return new CareerProgressionState(
                CareerProgressionKind.AwaitingEventChoice,
                CareerPhase.University,
                0,
                weekPlan,
                0,
                pendingEvent);
        }

        public static CareerProgressionState AwaitingMatch(
            CareerWeekPlanState confirmedPlan,
            Guid sessionId)
        {
            return new CareerProgressionState(
                CareerProgressionKind.AwaitingMatch,
                CareerPhase.University,
                0,
                confirmedPlan,
                0,
                null,
                sessionId);
        }

        internal CareerProgressionState Copy()
        {
            return new CareerProgressionState(
                Kind,
                Phase,
                TryoutStage,
                WeekPlan,
                NextSlotNumber,
                PendingEvent,
                MatchSessionId);
        }

        private static void ValidateCombination(
            CareerProgressionKind kind,
            int tryoutStage,
            CareerWeekPlanState weekPlan,
            int nextSlotNumber,
            PendingCareerEvent pendingEvent,
            Guid? matchSessionId)
        {
            switch (kind)
            {
                case CareerProgressionKind.CareerCreated:
                    RequireNoPlanOrEvent(tryoutStage, weekPlan, nextSlotNumber, pendingEvent, matchSessionId, nameof(kind));
                    return;

                case CareerProgressionKind.Tryout:
                    CareerSaveModelGuard.InclusiveRange(tryoutStage, 1, 3, nameof(tryoutStage));
                    RequireNoPlanOrEvent(0, weekPlan, nextSlotNumber, pendingEvent, matchSessionId, nameof(kind));
                    return;

                case CareerProgressionKind.Planning:
                    RequireWeekPlan(weekPlan, expectedConfirmed: false, nameof(kind));
                    RequireDefaults(tryoutStage, nextSlotNumber, pendingEvent, matchSessionId, nameof(kind));
                    return;

                case CareerProgressionKind.Planned:
                    RequireWeekPlan(weekPlan, expectedConfirmed: true, nameof(kind));
                    CareerSaveModelGuard.InclusiveRange(
                        nextSlotNumber,
                        1,
                        CareerWeekPlan.SlotCount,
                        nameof(nextSlotNumber));
                    RequireDefaults(tryoutStage, 0, pendingEvent, matchSessionId, nameof(kind));
                    return;

                case CareerProgressionKind.AwaitingEventChoice:
                    RequireWeekPlan(weekPlan, expectedConfirmed: true, nameof(kind));
                    if (pendingEvent == null)
                    {
                        throw new ArgumentNullException(nameof(pendingEvent));
                    }

                    RequireDefaults(tryoutStage, nextSlotNumber, pendingEvent, matchSessionId, nameof(kind), true);
                    return;

                case CareerProgressionKind.AwaitingMatch:
                    RequireWeekPlan(weekPlan, expectedConfirmed: true, nameof(kind));
                    if (weekPlan.Season != 1 || weekPlan.Week != 1)
                    {
                        throw new ArgumentException(
                            "The first slice may await only the university season 1 week 1 match.",
                            nameof(weekPlan));
                    }

                    if (!matchSessionId.HasValue || matchSessionId.Value == Guid.Empty)
                    {
                        throw new ArgumentException(
                            "AwaitingMatch requires a non-empty match session ID.",
                            nameof(matchSessionId));
                    }

                    RequireDefaults(tryoutStage, nextSlotNumber, pendingEvent, matchSessionId, nameof(kind), false, true);
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown progression kind.");
            }
        }

        private static void RequireNoPlanOrEvent(
            int ignoredTryoutStage,
            CareerWeekPlanState weekPlan,
            int nextSlotNumber,
            PendingCareerEvent pendingEvent,
            Guid? matchSessionId,
            string parameterName)
        {
            RequireDefaults(ignoredTryoutStage, nextSlotNumber, pendingEvent, matchSessionId, parameterName);
            if (weekPlan != null)
            {
                throw new ArgumentException(
                    "This progression kind cannot contain a week plan.",
                    parameterName);
            }
        }

        private static void RequireDefaults(
            int tryoutStage,
            int nextSlotNumber,
            PendingCareerEvent pendingEvent,
            Guid? matchSessionId,
            string parameterName,
            bool allowPendingEvent = false,
            bool allowMatchSession = false)
        {
            if (tryoutStage != 0 || nextSlotNumber != 0 ||
                (!allowPendingEvent && pendingEvent != null) ||
                (!allowMatchSession && matchSessionId.HasValue))
            {
                throw new ArgumentException(
                    "The progression kind contains fields that do not belong to it.",
                    parameterName);
            }
        }

        private static void RequireWeekPlan(
            CareerWeekPlanState weekPlan,
            bool expectedConfirmed,
            string parameterName)
        {
            if (weekPlan == null)
            {
                throw new ArgumentNullException(nameof(weekPlan));
            }

            if (weekPlan.Season < 1 || weekPlan.Season > 2)
            {
                throw new ArgumentException(
                    "The current progression model covers university seasons 1 and 2.",
                    parameterName);
            }

            if (weekPlan.IsConfirmed != expectedConfirmed)
            {
                throw new ArgumentException(
                    expectedConfirmed
                        ? "This progression kind requires a confirmed week plan."
                        : "Planning requires an unconfirmed week plan.",
                    parameterName);
            }
        }
    }
}
