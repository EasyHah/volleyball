using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Career.Domain
{
    public sealed class CareerMatchSetScoreSummary : IEquatable<CareerMatchSetScoreSummary>
    {
        public CareerMatchSetScoreSummary(int setNumber, int homePoints, int awayPoints, bool isComplete)
        {
            SetNumber = CareerMatchLifecycleGuard.Positive(setNumber, nameof(setNumber));
            HomePoints = CareerMatchLifecycleGuard.NonNegative(homePoints, nameof(homePoints));
            AwayPoints = CareerMatchLifecycleGuard.NonNegative(awayPoints, nameof(awayPoints));
            IsComplete = isComplete;
        }

        public int SetNumber { get; }
        public int HomePoints { get; }
        public int AwayPoints { get; }
        public bool IsComplete { get; }

        public bool Equals(CareerMatchSetScoreSummary other) =>
            other != null && SetNumber == other.SetNumber && HomePoints == other.HomePoints &&
            AwayPoints == other.AwayPoints && IsComplete == other.IsComplete;
        public override bool Equals(object obj) => Equals(obj as CareerMatchSetScoreSummary);
        public override int GetHashCode() =>
            (((SetNumber * 397) ^ HomePoints) * 397 ^ AwayPoints) * 397 ^ IsComplete.GetHashCode();
        internal CareerMatchSetScoreSummary Copy() =>
            new CareerMatchSetScoreSummary(SetNumber, HomePoints, AwayPoints, IsComplete);
    }

    public sealed class CareerSpikeFactSummary : IEquatable<CareerSpikeFactSummary>
    {
        public CareerSpikeFactSummary(int attempts, int points, int errors)
        {
            Attempts = CareerMatchLifecycleGuard.NonNegative(attempts, nameof(attempts));
            Points = CareerMatchLifecycleGuard.NonNegative(points, nameof(points));
            Errors = CareerMatchLifecycleGuard.NonNegative(errors, nameof(errors));
            if ((long)Points + Errors > Attempts) throw new ArgumentException("Spike points and errors cannot exceed attempts.");
        }
        public int Attempts { get; }
        public int Points { get; }
        public int Errors { get; }
        public bool Equals(CareerSpikeFactSummary other) => other != null && Attempts == other.Attempts && Points == other.Points && Errors == other.Errors;
        public override bool Equals(object obj) => Equals(obj as CareerSpikeFactSummary);
        public override int GetHashCode() => ((Attempts * 397) ^ Points) * 397 ^ Errors;
        internal CareerSpikeFactSummary Copy() => new CareerSpikeFactSummary(Attempts, Points, Errors);
    }

    public sealed class CareerServeFactSummary : IEquatable<CareerServeFactSummary>
    {
        public CareerServeFactSummary(int attempts, int aces, int errors)
        {
            Attempts = CareerMatchLifecycleGuard.NonNegative(attempts, nameof(attempts));
            Aces = CareerMatchLifecycleGuard.NonNegative(aces, nameof(aces));
            Errors = CareerMatchLifecycleGuard.NonNegative(errors, nameof(errors));
            if ((long)Aces + Errors > Attempts) throw new ArgumentException("Serve aces and errors cannot exceed attempts.");
        }
        public int Attempts { get; }
        public int Aces { get; }
        public int Errors { get; }
        public bool Equals(CareerServeFactSummary other) => other != null && Attempts == other.Attempts && Aces == other.Aces && Errors == other.Errors;
        public override bool Equals(object obj) => Equals(obj as CareerServeFactSummary);
        public override int GetHashCode() => ((Attempts * 397) ^ Aces) * 397 ^ Errors;
        internal CareerServeFactSummary Copy() => new CareerServeFactSummary(Attempts, Aces, Errors);
    }

    public sealed class CareerReceptionFactSummary : IEquatable<CareerReceptionFactSummary>
    {
        public CareerReceptionFactSummary(int attempts, int perfect, int positive, int neutral, int negative, int errors)
        {
            Attempts = CareerMatchLifecycleGuard.NonNegative(attempts, nameof(attempts));
            Perfect = CareerMatchLifecycleGuard.NonNegative(perfect, nameof(perfect));
            Positive = CareerMatchLifecycleGuard.NonNegative(positive, nameof(positive));
            Neutral = CareerMatchLifecycleGuard.NonNegative(neutral, nameof(neutral));
            Negative = CareerMatchLifecycleGuard.NonNegative(negative, nameof(negative));
            Errors = CareerMatchLifecycleGuard.NonNegative(errors, nameof(errors));
            if ((long)Perfect + Positive + Neutral + Negative + Errors != Attempts)
                throw new ArgumentException("Reception buckets must sum exactly to attempts.");
        }
        public int Attempts { get; }
        public int Perfect { get; }
        public int Positive { get; }
        public int Neutral { get; }
        public int Negative { get; }
        public int Errors { get; }
        public bool Equals(CareerReceptionFactSummary other) => other != null && Attempts == other.Attempts && Perfect == other.Perfect && Positive == other.Positive && Neutral == other.Neutral && Negative == other.Negative && Errors == other.Errors;
        public override bool Equals(object obj) => Equals(obj as CareerReceptionFactSummary);
        public override int GetHashCode()
        {
            unchecked { var hash = Attempts; hash = hash * 397 ^ Perfect; hash = hash * 397 ^ Positive; hash = hash * 397 ^ Neutral; hash = hash * 397 ^ Negative; return hash * 397 ^ Errors; }
        }
        internal CareerReceptionFactSummary Copy() => new CareerReceptionFactSummary(Attempts, Perfect, Positive, Neutral, Negative, Errors);
    }

    public sealed class CareerDefenseFactSummary : IEquatable<CareerDefenseFactSummary>
    {
        public CareerDefenseFactSummary(int attempts, int successes)
        {
            Attempts = CareerMatchLifecycleGuard.NonNegative(attempts, nameof(attempts));
            Successes = CareerMatchLifecycleGuard.NonNegative(successes, nameof(successes));
            if (Successes > Attempts) throw new ArgumentException("Defense successes cannot exceed attempts.");
        }
        public int Attempts { get; }
        public int Successes { get; }
        public bool Equals(CareerDefenseFactSummary other) => other != null && Attempts == other.Attempts && Successes == other.Successes;
        public override bool Equals(object obj) => Equals(obj as CareerDefenseFactSummary);
        public override int GetHashCode() => Attempts * 397 ^ Successes;
        internal CareerDefenseFactSummary Copy() => new CareerDefenseFactSummary(Attempts, Successes);
    }

    public sealed class CareerBlockFactSummary : IEquatable<CareerBlockFactSummary>
    {
        public CareerBlockFactSummary(int attempts, int effectiveTouches, int points)
        {
            Attempts = CareerMatchLifecycleGuard.NonNegative(attempts, nameof(attempts));
            EffectiveTouches = CareerMatchLifecycleGuard.NonNegative(effectiveTouches, nameof(effectiveTouches));
            Points = CareerMatchLifecycleGuard.NonNegative(points, nameof(points));
            if (Points > EffectiveTouches || EffectiveTouches > Attempts)
                throw new ArgumentException("Block points, touches and attempts must be nested subsets.");
        }
        public int Attempts { get; }
        public int EffectiveTouches { get; }
        public int Points { get; }
        public bool Equals(CareerBlockFactSummary other) => other != null && Attempts == other.Attempts && EffectiveTouches == other.EffectiveTouches && Points == other.Points;
        public override bool Equals(object obj) => Equals(obj as CareerBlockFactSummary);
        public override int GetHashCode() => ((Attempts * 397) ^ EffectiveTouches) * 397 ^ Points;
        internal CareerBlockFactSummary Copy() => new CareerBlockFactSummary(Attempts, EffectiveTouches, Points);
    }

    public sealed class CareerMatchLoadFactSummary : IEquatable<CareerMatchLoadFactSummary>
    {
        public CareerMatchLoadFactSummary(int ralliesPlayed, long activeDurationMilliseconds, long movementDistanceMillimeters, int jumpCount, int highLoadJumpCount, int landingLoadBasisPoints, int totalWorkloadBasisPoints)
        {
            RalliesPlayed = CareerMatchLifecycleGuard.NonNegative(ralliesPlayed, nameof(ralliesPlayed));
            ActiveDurationMilliseconds = CareerMatchLifecycleGuard.NonNegativeSafe(activeDurationMilliseconds, nameof(activeDurationMilliseconds));
            MovementDistanceMillimeters = CareerMatchLifecycleGuard.NonNegativeSafe(movementDistanceMillimeters, nameof(movementDistanceMillimeters));
            JumpCount = CareerMatchLifecycleGuard.NonNegative(jumpCount, nameof(jumpCount));
            HighLoadJumpCount = CareerMatchLifecycleGuard.NonNegative(highLoadJumpCount, nameof(highLoadJumpCount));
            if (HighLoadJumpCount > JumpCount) throw new ArgumentException("High-load jumps cannot exceed all jumps.");
            LandingLoadBasisPoints = CareerSaveModelGuard.InclusiveRange(landingLoadBasisPoints, 0, 10000, nameof(landingLoadBasisPoints));
            TotalWorkloadBasisPoints = CareerSaveModelGuard.InclusiveRange(totalWorkloadBasisPoints, 0, 10000, nameof(totalWorkloadBasisPoints));
        }
        public int RalliesPlayed { get; }
        public long ActiveDurationMilliseconds { get; }
        public long MovementDistanceMillimeters { get; }
        public int JumpCount { get; }
        public int HighLoadJumpCount { get; }
        public int LandingLoadBasisPoints { get; }
        public int TotalWorkloadBasisPoints { get; }
        public bool Equals(CareerMatchLoadFactSummary other) => other != null && RalliesPlayed == other.RalliesPlayed && ActiveDurationMilliseconds == other.ActiveDurationMilliseconds && MovementDistanceMillimeters == other.MovementDistanceMillimeters && JumpCount == other.JumpCount && HighLoadJumpCount == other.HighLoadJumpCount && LandingLoadBasisPoints == other.LandingLoadBasisPoints && TotalWorkloadBasisPoints == other.TotalWorkloadBasisPoints;
        public override bool Equals(object obj) => Equals(obj as CareerMatchLoadFactSummary);
        public override int GetHashCode()
        {
            unchecked { var hash = RalliesPlayed; hash = hash * 397 ^ ActiveDurationMilliseconds.GetHashCode(); hash = hash * 397 ^ MovementDistanceMillimeters.GetHashCode(); hash = hash * 397 ^ JumpCount; hash = hash * 397 ^ HighLoadJumpCount; hash = hash * 397 ^ LandingLoadBasisPoints; return hash * 397 ^ TotalWorkloadBasisPoints; }
        }
        internal CareerMatchLoadFactSummary Copy() => new CareerMatchLoadFactSummary(RalliesPlayed, ActiveDurationMilliseconds, MovementDistanceMillimeters, JumpCount, HighLoadJumpCount, LandingLoadBasisPoints, TotalWorkloadBasisPoints);
    }

    public sealed class CareerStabilityFactSummary : IEquatable<CareerStabilityFactSummary>
    {
        public CareerStabilityFactSummary(int criticalActions, int criticalSuccesses, int criticalErrors, int errorStreakEpisodes, int longestErrorStreak)
        {
            CriticalActions = CareerMatchLifecycleGuard.NonNegative(criticalActions, nameof(criticalActions));
            CriticalSuccesses = CareerMatchLifecycleGuard.NonNegative(criticalSuccesses, nameof(criticalSuccesses));
            CriticalErrors = CareerMatchLifecycleGuard.NonNegative(criticalErrors, nameof(criticalErrors));
            ErrorStreakEpisodes = CareerMatchLifecycleGuard.NonNegative(errorStreakEpisodes, nameof(errorStreakEpisodes));
            LongestErrorStreak = CareerMatchLifecycleGuard.NonNegative(longestErrorStreak, nameof(longestErrorStreak));
            if ((long)CriticalSuccesses + CriticalErrors > CriticalActions) throw new ArgumentException("Critical successes and errors cannot exceed actions.");
            if ((ErrorStreakEpisodes == 0 && LongestErrorStreak != 0) || (ErrorStreakEpisodes > 0 && LongestErrorStreak < 2)) throw new ArgumentException("Error streak facts are inconsistent.");
        }
        public int CriticalActions { get; }
        public int CriticalSuccesses { get; }
        public int CriticalErrors { get; }
        public int ErrorStreakEpisodes { get; }
        public int LongestErrorStreak { get; }
        public bool Equals(CareerStabilityFactSummary other) => other != null && CriticalActions == other.CriticalActions && CriticalSuccesses == other.CriticalSuccesses && CriticalErrors == other.CriticalErrors && ErrorStreakEpisodes == other.ErrorStreakEpisodes && LongestErrorStreak == other.LongestErrorStreak;
        public override bool Equals(object obj) => Equals(obj as CareerStabilityFactSummary);
        public override int GetHashCode()
        {
            unchecked { var hash = CriticalActions; hash = hash * 397 ^ CriticalSuccesses; hash = hash * 397 ^ CriticalErrors; hash = hash * 397 ^ ErrorStreakEpisodes; return hash * 397 ^ LongestErrorStreak; }
        }
        internal CareerStabilityFactSummary Copy() => new CareerStabilityFactSummary(CriticalActions, CriticalSuccesses, CriticalErrors, ErrorStreakEpisodes, LongestErrorStreak);
    }

    public sealed class CareerProtagonistMatchFacts : IEquatable<CareerProtagonistMatchFacts>
    {
        public CareerProtagonistMatchFacts(CareerSpikeFactSummary spike, CareerServeFactSummary serve, CareerReceptionFactSummary reception, CareerDefenseFactSummary defense, CareerBlockFactSummary block, CareerMatchLoadFactSummary load, CareerStabilityFactSummary stability)
        {
            Spike = (spike ?? throw new ArgumentNullException(nameof(spike))).Copy();
            Serve = (serve ?? throw new ArgumentNullException(nameof(serve))).Copy();
            Reception = (reception ?? throw new ArgumentNullException(nameof(reception))).Copy();
            Defense = (defense ?? throw new ArgumentNullException(nameof(defense))).Copy();
            Block = (block ?? throw new ArgumentNullException(nameof(block))).Copy();
            Load = (load ?? throw new ArgumentNullException(nameof(load))).Copy();
            Stability = (stability ?? throw new ArgumentNullException(nameof(stability))).Copy();
        }
        public CareerSpikeFactSummary Spike { get; }
        public CareerServeFactSummary Serve { get; }
        public CareerReceptionFactSummary Reception { get; }
        public CareerDefenseFactSummary Defense { get; }
        public CareerBlockFactSummary Block { get; }
        public CareerMatchLoadFactSummary Load { get; }
        public CareerStabilityFactSummary Stability { get; }
        public bool Equals(CareerProtagonistMatchFacts other) => other != null && Spike.Equals(other.Spike) && Serve.Equals(other.Serve) && Reception.Equals(other.Reception) && Defense.Equals(other.Defense) && Block.Equals(other.Block) && Load.Equals(other.Load) && Stability.Equals(other.Stability);
        public override bool Equals(object obj) => Equals(obj as CareerProtagonistMatchFacts);
        public override int GetHashCode()
        {
            unchecked { var hash = Spike.GetHashCode(); hash = hash * 397 ^ Serve.GetHashCode(); hash = hash * 397 ^ Reception.GetHashCode(); hash = hash * 397 ^ Defense.GetHashCode(); hash = hash * 397 ^ Block.GetHashCode(); hash = hash * 397 ^ Load.GetHashCode(); return hash * 397 ^ Stability.GetHashCode(); }
        }
        internal CareerProtagonistMatchFacts Copy() => new CareerProtagonistMatchFacts(Spike, Serve, Reception, Defense, Block, Load, Stability);
    }

    public sealed class CareerAttributeGrowthChange : IEquatable<CareerAttributeGrowthChange>
    {
        public CareerAttributeGrowthChange(CareerAttributeKind attribute, string reasonId, CareerAttributeProgress before, long requestedDelta, long actualDelta, CareerAttributeProgress after)
        {
            CareerSaveModelGuard.DefinedEnum(attribute, nameof(attribute));
            ReasonId = CareerSaveModelGuard.BusinessId(reasonId, nameof(reasonId));
            RequestedDelta = CareerMatchLifecycleGuard.NonNegativeSafe(requestedDelta, nameof(requestedDelta));
            ActualDelta = CareerMatchLifecycleGuard.NonNegativeSafe(actualDelta, nameof(actualDelta));
            if (ActualDelta > RequestedDelta || before.AbilityBasisPoints != after.AbilityBasisPoints ||
                !CareerMatchLifecycleGuard.SafeAddEquals(before.GrowthExperience, ActualDelta, after.GrowthExperience))
                throw new ArgumentException("An attribute change must preserve ability and apply its exact actual XP delta.");
            Attribute = attribute;
            Before = before;
            After = after;
        }
        public CareerAttributeKind Attribute { get; }
        public string ReasonId { get; }
        public CareerAttributeProgress Before { get; }
        public long RequestedDelta { get; }
        public long ActualDelta { get; }
        public CareerAttributeProgress After { get; }
        public bool Equals(CareerAttributeGrowthChange other) => other != null && Attribute == other.Attribute && string.Equals(ReasonId, other.ReasonId, StringComparison.Ordinal) && Before.Equals(other.Before) && RequestedDelta == other.RequestedDelta && ActualDelta == other.ActualDelta && After.Equals(other.After);
        public override bool Equals(object obj) => Equals(obj as CareerAttributeGrowthChange);
        public override int GetHashCode()
        {
            unchecked { var hash = (int)Attribute; hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(ReasonId); hash = hash * 397 ^ Before.GetHashCode(); hash = hash * 397 ^ RequestedDelta.GetHashCode(); hash = hash * 397 ^ ActualDelta.GetHashCode(); return hash * 397 ^ After.GetHashCode(); }
        }
        internal CareerAttributeGrowthChange Copy() => new CareerAttributeGrowthChange(Attribute, ReasonId, Before, RequestedDelta, ActualDelta, After);
    }

    public sealed class CareerReasonedIntegerChange : IEquatable<CareerReasonedIntegerChange>
    {
        public CareerReasonedIntegerChange(string reasonId, int oldValue, int requestedDelta, int actualDelta, int newValue)
        {
            ReasonId = CareerSaveModelGuard.BusinessId(reasonId, nameof(reasonId));
            OldValue = CareerSaveModelGuard.InclusiveRange(oldValue, 0, 100, nameof(oldValue));
            RequestedDelta = CareerSaveModelGuard.InclusiveRange(requestedDelta, -100, 100, nameof(requestedDelta));
            ActualDelta = CareerSaveModelGuard.InclusiveRange(actualDelta, -100, 100, nameof(actualDelta));
            NewValue = CareerSaveModelGuard.InclusiveRange(newValue, 0, 100, nameof(newValue));
            if (NewValue != OldValue + ActualDelta)
                throw new ArgumentException("A reasoned integer change must apply its exact actual delta.");
        }
        public string ReasonId { get; }
        public int OldValue { get; }
        public int RequestedDelta { get; }
        public int ActualDelta { get; }
        public int NewValue { get; }
        public bool Equals(CareerReasonedIntegerChange other) => other != null && string.Equals(ReasonId, other.ReasonId, StringComparison.Ordinal) && OldValue == other.OldValue && RequestedDelta == other.RequestedDelta && ActualDelta == other.ActualDelta && NewValue == other.NewValue;
        public override bool Equals(object obj) => Equals(obj as CareerReasonedIntegerChange);
        public override int GetHashCode()
        {
            unchecked { var hash = StringComparer.Ordinal.GetHashCode(ReasonId); hash = hash * 397 ^ OldValue; hash = hash * 397 ^ RequestedDelta; hash = hash * 397 ^ ActualDelta; return hash * 397 ^ NewValue; }
        }
        internal CareerReasonedIntegerChange Copy() => new CareerReasonedIntegerChange(ReasonId, OldValue, RequestedDelta, ActualDelta, NewValue);
    }

    public sealed class CareerSettlementSummary : IEquatable<CareerSettlementSummary>
    {
        private readonly CareerMatchSetScoreSummary[] _sets;
        private readonly ReadOnlyCollection<CareerMatchSetScoreSummary> _readOnlySets;
        private readonly CareerAttributeGrowthChange[] _growthChanges;
        private readonly ReadOnlyCollection<CareerAttributeGrowthChange> _readOnlyGrowthChanges;

        public CareerSettlementSummary(IEnumerable<CareerMatchSetScoreSummary> sets, CareerProtagonistMatchFacts protagonistFacts, CareerMatchPriority selectedPriority, bool priorityExecuted, bool won, IEnumerable<CareerAttributeGrowthChange> growthChanges, CareerReasonedIntegerChange matchFatigueChange, CareerReasonedIntegerChange matchMindsetChange, CareerReasonedIntegerChange matchCoachTrustChange, CareerReasonedIntegerChange weekendFatigueChange, CareerReasonedIntegerChange weekendMindsetChange, CareerReasonedIntegerChange weekendCoachTrustChange)
        {
            CareerSaveModelGuard.DefinedEnum(selectedPriority, nameof(selectedPriority));
            _sets = CopySets(sets);
            _readOnlySets = Array.AsReadOnly(_sets);
            ProtagonistFacts = (protagonistFacts ?? throw new ArgumentNullException(nameof(protagonistFacts))).Copy();
            _growthChanges = CopyGrowth(growthChanges);
            _readOnlyGrowthChanges = Array.AsReadOnly(_growthChanges);
            BeforeAttributes = AttributesFrom(_growthChanges, useAfter: false);
            AppliedGrowthExperienceDelta = new CareerAttributeGrowthDelta(
                _growthChanges[0].ActualDelta,
                _growthChanges[1].ActualDelta,
                _growthChanges[2].ActualDelta,
                _growthChanges[3].ActualDelta,
                _growthChanges[4].ActualDelta,
                _growthChanges[5].ActualDelta,
                _growthChanges[6].ActualDelta,
                _growthChanges[7].ActualDelta);
            AfterAttributes = AttributesFrom(_growthChanges, useAfter: true);
            MatchFatigueChange = CopyChange(matchFatigueChange, nameof(matchFatigueChange));
            MatchMindsetChange = CopyChange(matchMindsetChange, nameof(matchMindsetChange));
            MatchCoachTrustChange = CopyChange(matchCoachTrustChange, nameof(matchCoachTrustChange));
            WeekendFatigueChange = RequireZeroWeekend(weekendFatigueChange, MatchFatigueChange, nameof(weekendFatigueChange));
            WeekendMindsetChange = RequireZeroWeekend(weekendMindsetChange, MatchMindsetChange, nameof(weekendMindsetChange));
            WeekendCoachTrustChange = RequireZeroWeekend(weekendCoachTrustChange, MatchCoachTrustChange, nameof(weekendCoachTrustChange));
            SelectedPriority = selectedPriority;
            PriorityExecuted = priorityExecuted;
            Won = won;
        }

        public IReadOnlyList<CareerMatchSetScoreSummary> Sets => _readOnlySets;
        public CareerProtagonistMatchFacts ProtagonistFacts { get; }
        public CareerMatchPriority SelectedPriority { get; }
        public bool PriorityExecuted { get; }
        public bool Won { get; }
        public IReadOnlyList<CareerAttributeGrowthChange> GrowthChanges => _readOnlyGrowthChanges;
        public CareerPlayerAttributes BeforeAttributes { get; }
        public CareerAttributeGrowthDelta AppliedGrowthExperienceDelta { get; }
        public CareerPlayerAttributes AfterAttributes { get; }
        public CareerReasonedIntegerChange MatchFatigueChange { get; }
        public CareerReasonedIntegerChange MatchMindsetChange { get; }
        public CareerReasonedIntegerChange MatchCoachTrustChange { get; }
        public CareerReasonedIntegerChange WeekendFatigueChange { get; }
        public CareerReasonedIntegerChange WeekendMindsetChange { get; }
        public CareerReasonedIntegerChange WeekendCoachTrustChange { get; }

        public bool Equals(CareerSettlementSummary other)
        {
            return other != null && SelectedPriority == other.SelectedPriority && PriorityExecuted == other.PriorityExecuted && Won == other.Won && ProtagonistFacts.Equals(other.ProtagonistFacts) && SequenceEqual(_sets, other._sets) && SequenceEqual(_growthChanges, other._growthChanges) && MatchFatigueChange.Equals(other.MatchFatigueChange) && MatchMindsetChange.Equals(other.MatchMindsetChange) && MatchCoachTrustChange.Equals(other.MatchCoachTrustChange) && WeekendFatigueChange.Equals(other.WeekendFatigueChange) && WeekendMindsetChange.Equals(other.WeekendMindsetChange) && WeekendCoachTrustChange.Equals(other.WeekendCoachTrustChange);
        }
        public override bool Equals(object obj) => Equals(obj as CareerSettlementSummary);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)SelectedPriority;
                hash = hash * 397 ^ PriorityExecuted.GetHashCode(); hash = hash * 397 ^ Won.GetHashCode(); hash = hash * 397 ^ ProtagonistFacts.GetHashCode();
                for (var i = 0; i < _sets.Length; i++) hash = hash * 397 ^ _sets[i].GetHashCode();
                for (var i = 0; i < _growthChanges.Length; i++) hash = hash * 397 ^ _growthChanges[i].GetHashCode();
                hash = hash * 397 ^ MatchFatigueChange.GetHashCode(); hash = hash * 397 ^ MatchMindsetChange.GetHashCode(); hash = hash * 397 ^ MatchCoachTrustChange.GetHashCode(); hash = hash * 397 ^ WeekendFatigueChange.GetHashCode(); hash = hash * 397 ^ WeekendMindsetChange.GetHashCode(); return hash * 397 ^ WeekendCoachTrustChange.GetHashCode();
            }
        }
        internal CareerSettlementSummary Copy() => new CareerSettlementSummary(_sets, ProtagonistFacts, SelectedPriority, PriorityExecuted, Won, _growthChanges, MatchFatigueChange, MatchMindsetChange, MatchCoachTrustChange, WeekendFatigueChange, WeekendMindsetChange, WeekendCoachTrustChange);

        private static CareerMatchSetScoreSummary[] CopySets(IEnumerable<CareerMatchSetScoreSummary> sets)
        {
            if (sets == null) throw new ArgumentNullException(nameof(sets));
            var copied = new List<CareerMatchSetScoreSummary>();
            foreach (var set in sets)
            {
                if (set == null || !set.IsComplete || set.SetNumber != copied.Count + 1) throw new ArgumentException("Settlement sets must be non-null, completed and sequential.", nameof(sets));
                copied.Add(set.Copy());
            }
            if (copied.Count == 0) throw new ArgumentException("A completed settlement requires at least one set.", nameof(sets));
            return copied.ToArray();
        }

        private static CareerAttributeGrowthChange[] CopyGrowth(IEnumerable<CareerAttributeGrowthChange> growthChanges)
        {
            if (growthChanges == null) throw new ArgumentNullException(nameof(growthChanges));
            var copied = new List<CareerAttributeGrowthChange>(8);
            foreach (var change in growthChanges)
            {
                if (change == null || (int)change.Attribute != copied.Count) throw new ArgumentException("Growth changes must contain all eight axes in enum order.", nameof(growthChanges));
                copied.Add(change.Copy());
            }
            if (copied.Count != 8) throw new ArgumentException("Growth changes must contain all eight axes.", nameof(growthChanges));
            return copied.ToArray();
        }

        private static CareerPlayerAttributes AttributesFrom(
            IReadOnlyList<CareerAttributeGrowthChange> changes,
            bool useAfter)
        {
            return new CareerPlayerAttributes(
                useAfter ? changes[0].After : changes[0].Before,
                useAfter ? changes[1].After : changes[1].Before,
                useAfter ? changes[2].After : changes[2].Before,
                useAfter ? changes[3].After : changes[3].Before,
                useAfter ? changes[4].After : changes[4].Before,
                useAfter ? changes[5].After : changes[5].Before,
                useAfter ? changes[6].After : changes[6].Before,
                useAfter ? changes[7].After : changes[7].Before);
        }

        private static CareerReasonedIntegerChange CopyChange(CareerReasonedIntegerChange value, string name) => (value ?? throw new ArgumentNullException(name)).Copy();
        private static CareerReasonedIntegerChange RequireZeroWeekend(CareerReasonedIntegerChange weekend, CareerReasonedIntegerChange match, string name)
        {
            var copied = CopyChange(weekend, name);
            if (copied.OldValue != match.NewValue || copied.RequestedDelta != 0 || copied.ActualDelta != 0 || copied.NewValue != copied.OldValue)
                throw new ArgumentException("Weekend V1 status changes must be explicitly zero and continue the match value.", name);
            return copied;
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> left, IReadOnlyList<T> right) where T : IEquatable<T>
        {
            if (left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++) if (!left[i].Equals(right[i])) return false;
            return true;
        }
    }

    public sealed class CareerMatchHistoryEntry
    {
        private readonly byte[] _canonicalContextUtf8;
        private readonly byte[] _canonicalResultUtf8;
        public CareerMatchHistoryEntry(Guid sessionId, string scheduleItemId, WeekPlanId sourceWeekPlanId, SlotActionId sourceSlotActionId, Sha256Digest contextDigest, Sha256Digest resultDigest, byte[] canonicalContextUtf8, byte[] canonicalResultUtf8, LineageId appliedLineageId, long appliedRevision, long settledAtUtcMs, CareerSettlementSummary settlementSummary)
        {
            CareerSaveModelGuard.StableId(sessionId, nameof(sessionId));
            ScheduleItemId = CareerSaveModelGuard.BusinessId(scheduleItemId, nameof(scheduleItemId));
            CareerSaveModelGuard.StableId(sourceWeekPlanId.Value, nameof(sourceWeekPlanId)); CareerSaveModelGuard.StableId(sourceSlotActionId.Value, nameof(sourceSlotActionId));
            PendingCareerMatch.RequireDigest(contextDigest, nameof(contextDigest)); PendingCareerMatch.RequireDigest(resultDigest, nameof(resultDigest));
            _canonicalContextUtf8 = PendingCareerMatch.CopyUtf8(canonicalContextUtf8, nameof(canonicalContextUtf8));
            _canonicalResultUtf8 = PendingCareerMatch.CopyUtf8(canonicalResultUtf8, nameof(canonicalResultUtf8));
            CareerSaveModelGuard.StableId(appliedLineageId.Value, nameof(appliedLineageId));
            AppliedRevision = CareerSaveModelGuard.PositiveRevision(appliedRevision, nameof(appliedRevision));
            SettledAtUtcMs = CareerSaveModelGuard.NonNegativeUtcMilliseconds(settledAtUtcMs, nameof(settledAtUtcMs));
            SettlementSummary = (settlementSummary ?? throw new ArgumentNullException(nameof(settlementSummary))).Copy();
            SessionId = sessionId; SourceWeekPlanId = sourceWeekPlanId; SourceSlotActionId = sourceSlotActionId; ContextDigest = contextDigest; ResultDigest = resultDigest; AppliedLineageId = appliedLineageId;
        }
        public Guid SessionId { get; }
        public string ScheduleItemId { get; }
        public WeekPlanId SourceWeekPlanId { get; }
        public SlotActionId SourceSlotActionId { get; }
        public Sha256Digest ContextDigest { get; }
        public Sha256Digest ResultDigest { get; }
        public byte[] CanonicalContextUtf8 => (byte[])_canonicalContextUtf8.Clone();
        public byte[] CanonicalResultUtf8 => (byte[])_canonicalResultUtf8.Clone();
        public LineageId AppliedLineageId { get; }
        public long AppliedRevision { get; }
        public long SettledAtUtcMs { get; }
        public CareerSettlementSummary SettlementSummary { get; }
        internal CareerMatchHistoryEntry Copy() => new CareerMatchHistoryEntry(SessionId, ScheduleItemId, SourceWeekPlanId, SourceSlotActionId, ContextDigest, ResultDigest, _canonicalContextUtf8, _canonicalResultUtf8, AppliedLineageId, AppliedRevision, SettledAtUtcMs, SettlementSummary);
    }

    public sealed class CareerSettlementReceipt
    {
        public CareerSettlementReceipt(Guid sessionId, Sha256Digest contextDigest, Sha256Digest resultDigest, LineageId appliedLineageId, long appliedRevision, long settledAtUtcMs, CareerSettlementSummary settlementSummary)
        {
            CareerSaveModelGuard.StableId(sessionId, nameof(sessionId)); PendingCareerMatch.RequireDigest(contextDigest, nameof(contextDigest)); PendingCareerMatch.RequireDigest(resultDigest, nameof(resultDigest)); CareerSaveModelGuard.StableId(appliedLineageId.Value, nameof(appliedLineageId));
            AppliedRevision = CareerSaveModelGuard.PositiveRevision(appliedRevision, nameof(appliedRevision)); SettledAtUtcMs = CareerSaveModelGuard.NonNegativeUtcMilliseconds(settledAtUtcMs, nameof(settledAtUtcMs)); SettlementSummary = (settlementSummary ?? throw new ArgumentNullException(nameof(settlementSummary))).Copy();
            SessionId = sessionId; ContextDigest = contextDigest; ResultDigest = resultDigest; AppliedLineageId = appliedLineageId;
        }
        public Guid SessionId { get; }
        public Sha256Digest ContextDigest { get; }
        public Sha256Digest ResultDigest { get; }
        public LineageId AppliedLineageId { get; }
        public long AppliedRevision { get; }
        public long SettledAtUtcMs { get; }
        public CareerSettlementSummary SettlementSummary { get; }
        internal CareerSettlementReceipt Copy() => new CareerSettlementReceipt(SessionId, ContextDigest, ResultDigest, AppliedLineageId, AppliedRevision, SettledAtUtcMs, SettlementSummary);
    }

    internal static class CareerMatchLifecycleGuard
    {
        public static int Positive(int value, string parameterName)
        {
            if (value < 1) throw new ArgumentOutOfRangeException(parameterName, value, "The value must be positive.");
            return value;
        }
        public static int NonNegative(int value, string parameterName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(parameterName, value, "The value cannot be negative.");
            return value;
        }
        public static long NonNegativeSafe(long value, string parameterName)
        {
            if (value < 0 || value > CareerSaveModelGuard.MaximumIJsonSafeInteger) throw new ArgumentOutOfRangeException(parameterName, value, "The value must be a non-negative I-JSON safe integer.");
            return value;
        }
        public static bool SafeAddEquals(long left, long right, long expected)
        {
            if (left > CareerSaveModelGuard.MaximumIJsonSafeInteger - right) return false;
            return left + right == expected;
        }
    }
}
