using System;
using System.Collections.Generic;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Application
{
    public sealed class CareerMatchSetScore
    {
        public CareerMatchSetScore(int setNumber, int homePoints, int awayPoints, bool isComplete)
        {
            SetNumber = CareerMatchGuard.Positive(setNumber, nameof(setNumber));
            HomePoints = CareerMatchGuard.NonNegative(homePoints, nameof(homePoints));
            AwayPoints = CareerMatchGuard.NonNegative(awayPoints, nameof(awayPoints));
            IsComplete = isComplete;
        }

        public int SetNumber { get; }

        public int HomePoints { get; }

        public int AwayPoints { get; }

        public bool IsComplete { get; }
    }

    public sealed class CareerSpikeFacts
    {
        public CareerSpikeFacts(int attempts, int points, int errors)
        {
            Attempts = CareerMatchGuard.NonNegative(attempts, nameof(attempts));
            Points = CareerMatchGuard.NonNegative(points, nameof(points));
            Errors = CareerMatchGuard.NonNegative(errors, nameof(errors));
            if ((long)points + errors > attempts)
            {
                throw new ArgumentException("Spike points and errors cannot exceed attempts.");
            }
        }

        public int Attempts { get; }

        public int Points { get; }

        public int Errors { get; }
    }

    public sealed class CareerServeFacts
    {
        public CareerServeFacts(int attempts, int aces, int errors)
        {
            Attempts = CareerMatchGuard.NonNegative(attempts, nameof(attempts));
            Aces = CareerMatchGuard.NonNegative(aces, nameof(aces));
            Errors = CareerMatchGuard.NonNegative(errors, nameof(errors));
            if ((long)aces + errors > attempts)
            {
                throw new ArgumentException("Serve aces and errors cannot exceed attempts.");
            }
        }

        public int Attempts { get; }

        public int Aces { get; }

        public int Errors { get; }
    }

    public sealed class CareerReceptionFacts
    {
        public CareerReceptionFacts(
            int attempts,
            int perfect,
            int positive,
            int neutral,
            int negative,
            int errors)
        {
            Attempts = CareerMatchGuard.NonNegative(attempts, nameof(attempts));
            Perfect = CareerMatchGuard.NonNegative(perfect, nameof(perfect));
            Positive = CareerMatchGuard.NonNegative(positive, nameof(positive));
            Neutral = CareerMatchGuard.NonNegative(neutral, nameof(neutral));
            Negative = CareerMatchGuard.NonNegative(negative, nameof(negative));
            Errors = CareerMatchGuard.NonNegative(errors, nameof(errors));
            if ((long)perfect + positive + neutral + negative + errors != attempts)
            {
                throw new ArgumentException("Reception quality buckets must sum exactly to attempts.");
            }
        }

        public int Attempts { get; }

        public int Perfect { get; }

        public int Positive { get; }

        public int Neutral { get; }

        public int Negative { get; }

        public int Errors { get; }
    }

    public sealed class CareerDefenseFacts
    {
        public CareerDefenseFacts(int attempts, int successes)
        {
            Attempts = CareerMatchGuard.NonNegative(attempts, nameof(attempts));
            Successes = CareerMatchGuard.NonNegative(successes, nameof(successes));
            if (successes > attempts)
            {
                throw new ArgumentException("Defense successes cannot exceed attempts.");
            }
        }

        public int Attempts { get; }

        public int Successes { get; }
    }

    public sealed class CareerBlockFacts
    {
        public CareerBlockFacts(int attempts, int effectiveTouches, int points)
        {
            Attempts = CareerMatchGuard.NonNegative(attempts, nameof(attempts));
            EffectiveTouches = CareerMatchGuard.NonNegative(effectiveTouches, nameof(effectiveTouches));
            Points = CareerMatchGuard.NonNegative(points, nameof(points));
            if (points > effectiveTouches || effectiveTouches > attempts)
            {
                throw new ArgumentException(
                    "Block points must be a subset of effective touches and touches of attempts.");
            }
        }

        public int Attempts { get; }

        public int EffectiveTouches { get; }

        public int Points { get; }
    }

    public sealed class CareerMatchLoadFacts
    {
        public CareerMatchLoadFacts(
            int ralliesPlayed,
            long activeDurationMilliseconds,
            long movementDistanceMillimeters,
            int jumpCount,
            int highLoadJumpCount,
            int landingLoadBasisPoints,
            int totalWorkloadBasisPoints)
        {
            RalliesPlayed = CareerMatchGuard.NonNegative(ralliesPlayed, nameof(ralliesPlayed));
            ActiveDurationMilliseconds = CareerMatchGuard.NonNegativeSafe(
                activeDurationMilliseconds, nameof(activeDurationMilliseconds));
            MovementDistanceMillimeters = CareerMatchGuard.NonNegativeSafe(
                movementDistanceMillimeters, nameof(movementDistanceMillimeters));
            JumpCount = CareerMatchGuard.NonNegative(jumpCount, nameof(jumpCount));
            HighLoadJumpCount = CareerMatchGuard.NonNegative(highLoadJumpCount, nameof(highLoadJumpCount));
            if (highLoadJumpCount > jumpCount)
            {
                throw new ArgumentException("High-load jump count cannot exceed jump count.");
            }

            LandingLoadBasisPoints = CareerMatchGuard.BasisPoints(
                landingLoadBasisPoints, nameof(landingLoadBasisPoints));
            TotalWorkloadBasisPoints = CareerMatchGuard.BasisPoints(
                totalWorkloadBasisPoints, nameof(totalWorkloadBasisPoints));
        }

        public int RalliesPlayed { get; }

        public long ActiveDurationMilliseconds { get; }

        public long MovementDistanceMillimeters { get; }

        public int JumpCount { get; }

        public int HighLoadJumpCount { get; }

        public int LandingLoadBasisPoints { get; }

        public int TotalWorkloadBasisPoints { get; }
    }

    public sealed class CareerStabilityFacts
    {
        public CareerStabilityFacts(
            int criticalActions,
            int criticalSuccesses,
            int criticalErrors,
            int errorStreakEpisodes,
            int longestErrorStreak)
        {
            CriticalActions = CareerMatchGuard.NonNegative(criticalActions, nameof(criticalActions));
            CriticalSuccesses = CareerMatchGuard.NonNegative(criticalSuccesses, nameof(criticalSuccesses));
            CriticalErrors = CareerMatchGuard.NonNegative(criticalErrors, nameof(criticalErrors));
            ErrorStreakEpisodes = CareerMatchGuard.NonNegative(
                errorStreakEpisodes, nameof(errorStreakEpisodes));
            LongestErrorStreak = CareerMatchGuard.NonNegative(longestErrorStreak, nameof(longestErrorStreak));
            if ((long)criticalSuccesses + criticalErrors > criticalActions)
            {
                throw new ArgumentException("Critical successes and errors cannot exceed critical actions.");
            }

            if ((errorStreakEpisodes == 0 && longestErrorStreak != 0) ||
                (errorStreakEpisodes > 0 && longestErrorStreak < 2))
            {
                throw new ArgumentException("Error streak fields are inconsistent.");
            }
        }

        public int CriticalActions { get; }

        public int CriticalSuccesses { get; }

        public int CriticalErrors { get; }

        public int ErrorStreakEpisodes { get; }

        public int LongestErrorStreak { get; }
    }

    public sealed class CareerMatchPlayerFacts
    {
        public CareerMatchPlayerFacts(
            PlayerId playerId,
            CareerSpikeFacts spike,
            CareerServeFacts serve,
            CareerReceptionFacts reception,
            CareerDefenseFacts defense,
            CareerBlockFacts block,
            CareerMatchLoadFacts load,
            CareerStabilityFacts stability)
        {
            CareerMatchGuard.StableId(playerId.Value, nameof(playerId));
            PlayerId = playerId;
            Spike = spike ?? throw new ArgumentNullException(nameof(spike));
            Serve = serve ?? throw new ArgumentNullException(nameof(serve));
            Reception = reception ?? throw new ArgumentNullException(nameof(reception));
            Defense = defense ?? throw new ArgumentNullException(nameof(defense));
            Block = block ?? throw new ArgumentNullException(nameof(block));
            Load = load ?? throw new ArgumentNullException(nameof(load));
            Stability = stability ?? throw new ArgumentNullException(nameof(stability));
        }

        public PlayerId PlayerId { get; }

        public CareerSpikeFacts Spike { get; }

        public CareerServeFacts Serve { get; }

        public CareerReceptionFacts Reception { get; }

        public CareerDefenseFacts Defense { get; }

        public CareerBlockFacts Block { get; }

        public CareerMatchLoadFacts Load { get; }

        public CareerStabilityFacts Stability { get; }
    }

    public sealed class CareerMatchFacts
    {
        private readonly CareerMatchSetScore[] _sets;
        private readonly CareerMatchPlayerFacts[] _playerFacts;

        public CareerMatchFacts(
            CareerMatchVersions versions,
            Guid sessionId,
            Sha256Digest contextDigest,
            CareerMatchResultStatus status,
            TeamId? winnerTeamId,
            IReadOnlyList<CareerMatchSetScore> sets,
            int rallyCount,
            IReadOnlyList<CareerMatchPlayerFacts> playerFacts,
            Sha256Digest resultDigest)
        {
            Versions = versions ?? throw new ArgumentNullException(nameof(versions));
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("A non-empty match session ID is required.", nameof(sessionId));
            }

            CareerMatchGuard.Digest(contextDigest, nameof(contextDigest));
            CareerMatchGuard.Enum(status, nameof(status));
            if (status == CareerMatchResultStatus.Completed && !winnerTeamId.HasValue)
            {
                throw new ArgumentException("Completed facts require a winner team ID.", nameof(winnerTeamId));
            }

            if (status == CareerMatchResultStatus.Abandoned && winnerTeamId.HasValue)
            {
                throw new ArgumentException("Abandoned facts cannot have a winner team ID.", nameof(winnerTeamId));
            }

            if (winnerTeamId.HasValue)
            {
                CareerMatchGuard.StableId(winnerTeamId.Value.Value, nameof(winnerTeamId));
            }

            _sets = CareerMatchGuard.Copy(sets, nameof(sets));
            RallyCount = CareerMatchGuard.NonNegative(rallyCount, nameof(rallyCount));
            _playerFacts = CareerMatchGuard.Copy(playerFacts, nameof(playerFacts));
            CareerMatchGuard.Digest(resultDigest, nameof(resultDigest));
            ValidateSets(_sets, status, RallyCount);
            ValidatePlayerFacts(_playerFacts, RallyCount);
            SessionId = sessionId;
            ContextDigest = contextDigest;
            Status = status;
            WinnerTeamId = winnerTeamId;
            ResultDigest = resultDigest;
        }

        public CareerMatchVersions Versions { get; }

        public Guid SessionId { get; }

        public Sha256Digest ContextDigest { get; }

        public CareerMatchResultStatus Status { get; }

        public TeamId? WinnerTeamId { get; }

        public IReadOnlyList<CareerMatchSetScore> Sets => Array.AsReadOnly(_sets);

        public int RallyCount { get; }

        public IReadOnlyList<CareerMatchPlayerFacts> PlayerFacts => Array.AsReadOnly(_playerFacts);

        public Sha256Digest ResultDigest { get; }

        private static void ValidateSets(
            IReadOnlyList<CareerMatchSetScore> sets,
            CareerMatchResultStatus status,
            int rallyCount)
        {
            if (status == CareerMatchResultStatus.Completed && sets.Count == 0)
            {
                throw new ArgumentException("Completed facts require at least one completed set.", nameof(sets));
            }

            if (status == CareerMatchResultStatus.Abandoned && sets.Count > 1)
            {
                throw new ArgumentException("Abandoned facts may contain at most one set.", nameof(sets));
            }

            long totalPoints = 0;
            for (var index = 0; index < sets.Count; index++)
            {
                var set = sets[index] ?? throw new ArgumentException("Sets cannot contain null.", nameof(sets));
                if (set.SetNumber != index + 1)
                {
                    throw new ArgumentException("Set numbers must be sequential.", nameof(sets));
                }

                if (status == CareerMatchResultStatus.Completed && !set.IsComplete)
                {
                    throw new ArgumentException("Completed facts cannot contain an incomplete set.", nameof(sets));
                }

                if (status == CareerMatchResultStatus.Abandoned && set.IsComplete)
                {
                    throw new ArgumentException("Abandoned facts cannot contain a completed set.", nameof(sets));
                }

                totalPoints += (long)set.HomePoints + set.AwayPoints;
            }

            if (totalPoints != rallyCount)
            {
                throw new ArgumentException("Rally count must equal the sum of set points.", nameof(rallyCount));
            }
        }

        private static void ValidatePlayerFacts(
            IReadOnlyList<CareerMatchPlayerFacts> playerFacts,
            int rallyCount)
        {
            if (playerFacts.Count != 12)
            {
                throw new ArgumentException("Career match facts require exactly twelve players.", nameof(playerFacts));
            }

            var ids = new HashSet<PlayerId>();
            foreach (var facts in playerFacts)
            {
                if (facts == null)
                {
                    throw new ArgumentException("Player facts cannot contain null.", nameof(playerFacts));
                }

                if (!ids.Add(facts.PlayerId))
                {
                    throw new ArgumentException("Player fact IDs must be unique.", nameof(playerFacts));
                }

                if (facts.Load.RalliesPlayed > rallyCount)
                {
                    throw new ArgumentException(
                        "A player's rallies cannot exceed the match rally count.", nameof(playerFacts));
                }
            }
        }
    }
}
