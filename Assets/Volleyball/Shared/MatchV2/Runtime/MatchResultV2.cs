using System;
using System.Collections.Generic;
using Volleyball.Shared.Contracts;

namespace Volleyball.Shared.Contracts.V2
{
    public sealed class SetScoreV2
    {
        public SetScoreV2(int setNumber, int homePoints, int awayPoints, bool isComplete)
        {
            SetNumber = MatchV2Guard.Positive(setNumber, nameof(setNumber));
            HomePoints = MatchV2Guard.NonNegative(homePoints, nameof(homePoints));
            AwayPoints = MatchV2Guard.NonNegative(awayPoints, nameof(awayPoints));
            IsComplete = isComplete;
        }
        public int SetNumber { get; }
        public int HomePoints { get; }
        public int AwayPoints { get; }
        public bool IsComplete { get; }
    }

    public sealed class SpikeFactsV2
    {
        public SpikeFactsV2(int attempts, int points, int errors)
        {
            Attempts = MatchV2Guard.NonNegative(attempts, nameof(attempts));
            Points = MatchV2Guard.NonNegative(points, nameof(points));
            Errors = MatchV2Guard.NonNegative(errors, nameof(errors));
            if ((long)points + errors > attempts) throw new MatchV2ContractException("Spike points and errors cannot exceed attempts.");
        }
        public int Attempts { get; }
        public int Points { get; }
        public int Errors { get; }
    }

    public sealed class ServeFactsV2
    {
        public ServeFactsV2(int attempts, int aces, int errors)
        {
            Attempts = MatchV2Guard.NonNegative(attempts, nameof(attempts));
            Aces = MatchV2Guard.NonNegative(aces, nameof(aces));
            Errors = MatchV2Guard.NonNegative(errors, nameof(errors));
            if ((long)aces + errors > attempts) throw new MatchV2ContractException("Serve aces and errors cannot exceed attempts.");
        }
        public int Attempts { get; }
        public int Aces { get; }
        public int Errors { get; }
    }

    public sealed class ReceptionFactsV2
    {
        public ReceptionFactsV2(int attempts, int perfect, int positive, int neutral, int negative, int errors)
        {
            Attempts = MatchV2Guard.NonNegative(attempts, nameof(attempts));
            Perfect = MatchV2Guard.NonNegative(perfect, nameof(perfect));
            Positive = MatchV2Guard.NonNegative(positive, nameof(positive));
            Neutral = MatchV2Guard.NonNegative(neutral, nameof(neutral));
            Negative = MatchV2Guard.NonNegative(negative, nameof(negative));
            Errors = MatchV2Guard.NonNegative(errors, nameof(errors));
            if ((long)perfect + positive + neutral + negative + errors != attempts)
                throw new MatchV2ContractException("Reception quality buckets must sum exactly to attempts.");
        }
        public int Attempts { get; }
        public int Perfect { get; }
        public int Positive { get; }
        public int Neutral { get; }
        public int Negative { get; }
        public int Errors { get; }
    }

    public sealed class DefenseFactsV2
    {
        public DefenseFactsV2(int attempts, int successes)
        {
            Attempts = MatchV2Guard.NonNegative(attempts, nameof(attempts));
            Successes = MatchV2Guard.NonNegative(successes, nameof(successes));
            if (successes > attempts) throw new MatchV2ContractException("Defense successes cannot exceed attempts.");
        }
        public int Attempts { get; }
        public int Successes { get; }
    }

    public sealed class BlockFactsV2
    {
        public BlockFactsV2(int attempts, int effectiveTouches, int points)
        {
            Attempts = MatchV2Guard.NonNegative(attempts, nameof(attempts));
            EffectiveTouches = MatchV2Guard.NonNegative(effectiveTouches, nameof(effectiveTouches));
            Points = MatchV2Guard.NonNegative(points, nameof(points));
            if (points > effectiveTouches || effectiveTouches > attempts)
                throw new MatchV2ContractException("Block points must be a subset of touches and touches of attempts.");
        }
        public int Attempts { get; }
        public int EffectiveTouches { get; }
        public int Points { get; }
    }

    public sealed class MatchLoadFactsV2
    {
        public MatchLoadFactsV2(
            int ralliesPlayed, long activeDurationMilliseconds, long movementDistanceMillimeters,
            int jumpCount, int highLoadJumpCount, int landingLoadBasisPoints,
            int totalWorkloadBasisPoints)
        {
            RalliesPlayed = MatchV2Guard.NonNegative(ralliesPlayed, nameof(ralliesPlayed));
            ActiveDurationMilliseconds = MatchV2Guard.NonNegative(activeDurationMilliseconds, nameof(activeDurationMilliseconds));
            MovementDistanceMillimeters = MatchV2Guard.NonNegative(movementDistanceMillimeters, nameof(movementDistanceMillimeters));
            JumpCount = MatchV2Guard.NonNegative(jumpCount, nameof(jumpCount));
            HighLoadJumpCount = MatchV2Guard.NonNegative(highLoadJumpCount, nameof(highLoadJumpCount));
            if (highLoadJumpCount > jumpCount) throw new MatchV2ContractException("High-load jumps cannot exceed jumps.");
            LandingLoadBasisPoints = MatchV2Guard.BasisPoints(landingLoadBasisPoints, nameof(landingLoadBasisPoints));
            TotalWorkloadBasisPoints = MatchV2Guard.BasisPoints(totalWorkloadBasisPoints, nameof(totalWorkloadBasisPoints));
        }
        public int RalliesPlayed { get; }
        public long ActiveDurationMilliseconds { get; }
        public long MovementDistanceMillimeters { get; }
        public int JumpCount { get; }
        public int HighLoadJumpCount { get; }
        public int LandingLoadBasisPoints { get; }
        public int TotalWorkloadBasisPoints { get; }
    }

    public sealed class StabilityFactsV2
    {
        public StabilityFactsV2(
            int criticalActions, int criticalSuccesses, int criticalErrors,
            int errorStreakEpisodes, int longestErrorStreak)
        {
            CriticalActions = MatchV2Guard.NonNegative(criticalActions, nameof(criticalActions));
            CriticalSuccesses = MatchV2Guard.NonNegative(criticalSuccesses, nameof(criticalSuccesses));
            CriticalErrors = MatchV2Guard.NonNegative(criticalErrors, nameof(criticalErrors));
            ErrorStreakEpisodes = MatchV2Guard.NonNegative(errorStreakEpisodes, nameof(errorStreakEpisodes));
            LongestErrorStreak = MatchV2Guard.NonNegative(longestErrorStreak, nameof(longestErrorStreak));
            if ((long)criticalSuccesses + criticalErrors > criticalActions)
                throw new MatchV2ContractException("Critical successes and errors cannot exceed actions.");
            if ((errorStreakEpisodes == 0 && longestErrorStreak != 0) ||
                (errorStreakEpisodes > 0 && longestErrorStreak < 2))
                throw new MatchV2ContractException("Error-streak episodes and longest streak are inconsistent.");
        }
        public int CriticalActions { get; }
        public int CriticalSuccesses { get; }
        public int CriticalErrors { get; }
        public int ErrorStreakEpisodes { get; }
        public int LongestErrorStreak { get; }
    }

    public sealed class MatchPlayerFactsV2
    {
        public MatchPlayerFactsV2(
            PlayerId playerId, SpikeFactsV2 spike, ServeFactsV2 serve,
            ReceptionFactsV2 reception, DefenseFactsV2 defense, BlockFactsV2 block,
            MatchLoadFactsV2 load, StabilityFactsV2 stability)
        {
            MatchV2Guard.StableId(playerId.Value, nameof(playerId));
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
        public SpikeFactsV2 Spike { get; }
        public ServeFactsV2 Serve { get; }
        public ReceptionFactsV2 Reception { get; }
        public DefenseFactsV2 Defense { get; }
        public BlockFactsV2 Block { get; }
        public MatchLoadFactsV2 Load { get; }
        public StabilityFactsV2 Stability { get; }
    }

    public sealed class MatchResultV2
    {
        private readonly SetScoreV2[] _sets;
        private readonly MatchPlayerFactsV2[] _playerFacts;

        private MatchResultV2(
            MatchContextV2 context, MatchStatusV2 status, TeamId? winnerTeamId,
            IReadOnlyList<SetScoreV2> sets, int rallyCount,
            IReadOnlyList<MatchPlayerFactsV2> playerFacts)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            MatchV2Guard.Enum(status, nameof(status));
            Versions = context.Versions;
            SessionId = context.SessionId;
            ContextHash = context.ContextHash;
            Status = status;
            WinnerTeamId = winnerTeamId;
            _sets = MatchV2Guard.Copy(sets, nameof(sets));
            RallyCount = MatchV2Guard.NonNegative(rallyCount, nameof(rallyCount));
            _playerFacts = MatchV2Guard.Copy(playerFacts, nameof(playerFacts));
            ValidateAgainst(context, validateHash: false);
            ResultHash = MatchContractV2Json.ComputeResultHash(this);
        }

        public MatchVersionSetV2 Versions { get; }
        public Guid SessionId { get; }
        public string ContextHash { get; }
        public MatchStatusV2 Status { get; }
        public TeamId? WinnerTeamId { get; }
        public IReadOnlyList<SetScoreV2> Sets => Array.AsReadOnly(_sets);
        public int RallyCount { get; }
        public IReadOnlyList<MatchPlayerFactsV2> PlayerFacts => Array.AsReadOnly(_playerFacts);
        public string ResultHash { get; }

        public static MatchResultV2 CreateCompleted(
            MatchContextV2 context, TeamId winnerTeamId, IReadOnlyList<SetScoreV2> sets,
            int rallyCount, IReadOnlyList<MatchPlayerFactsV2> playerFacts) =>
            new MatchResultV2(context, MatchStatusV2.Completed, winnerTeamId, sets, rallyCount, playerFacts);

        public static MatchResultV2 CreateAbandoned(
            MatchContextV2 context, IReadOnlyList<SetScoreV2> sets,
            int rallyCount, IReadOnlyList<MatchPlayerFactsV2> playerFacts) =>
            new MatchResultV2(context, MatchStatusV2.Abandoned, null, sets, rallyCount, playerFacts);

        public void ValidateAgainst(MatchContextV2 context) => ValidateAgainst(context, validateHash: true);

        internal void ValidateLocal()
        {
            MatchV2Guard.Hash(ContextHash, nameof(ContextHash));
            MatchV2Guard.Hash(ResultHash, nameof(ResultHash));
            if (!MatchV2Hash.FixedTimeEquals(ResultHash, MatchContractV2Json.ComputeResultHash(this)))
                throw new MatchV2ContractException("resultHash does not match the result payload.");
        }

        private void ValidateAgainst(MatchContextV2 context, bool validateHash)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            if (!Versions.Equals(context.Versions) || SessionId != context.SessionId ||
                !string.Equals(ContextHash, context.ContextHash, StringComparison.Ordinal))
                throw new MatchV2ContractException("The result does not belong to this context.");

            var expectedIds = new List<PlayerId>(12);
            foreach (var team in context.Teams)
                foreach (var player in team.Players) expectedIds.Add(player.PlayerId);
            if (_playerFacts.Length != expectedIds.Count)
                throw new MatchV2ContractException("The result must contain exactly twelve player facts.");
            for (var index = 0; index < expectedIds.Count; index++)
            {
                if (_playerFacts[index] == null || !_playerFacts[index].PlayerId.Equals(expectedIds[index]))
                    throw new MatchV2ContractException("Player facts must follow context player order exactly.");
                if (_playerFacts[index].Load.RalliesPlayed > RallyCount)
                    throw new MatchV2ContractException("Player rallies cannot exceed match rallyCount.");
            }

            ValidateScore(context);
            if (validateHash) ValidateLocal();
        }

        private void ValidateScore(MatchContextV2 context)
        {
            long homePoints = 0;
            long awayPoints = 0;
            var homeWins = 0;
            var awayWins = 0;
            for (var index = 0; index < _sets.Length; index++)
            {
                var set = _sets[index] ?? throw new MatchV2ContractException("sets cannot contain null.");
                if (set.SetNumber != index + 1) throw new MatchV2ContractException("Set numbers must be sequential.");
                homePoints += set.HomePoints;
                awayPoints += set.AwayPoints;
                if (set.IsComplete)
                {
                    var higher = Math.Max(set.HomePoints, set.AwayPoints);
                    if (higher < context.Format.SetTargetPoints ||
                        Math.Abs(set.HomePoints - set.AwayPoints) < context.Format.MinimumLeadPoints)
                        throw new MatchV2ContractException("A completed set must satisfy target and lead rules.");
                    if (set.HomePoints > set.AwayPoints) homeWins++; else awayWins++;
                }
            }

            if (Status == MatchStatusV2.Completed)
            {
                if (!WinnerTeamId.HasValue || _sets.Length == 0 ||
                    Array.Exists(_sets, set => set == null || !set.IsComplete) ||
                    RallyCount != homePoints + awayPoints)
                    throw new MatchV2ContractException("A completed result requires complete sets, winner, and exact rallyCount.");
                var homeWinner = WinnerTeamId.Value.Equals(context.Teams[0].TeamId);
                var awayWinner = WinnerTeamId.Value.Equals(context.Teams[1].TeamId);
                if ((!homeWinner && !awayWinner) ||
                    (homeWinner && (homeWins != context.Format.SetsToWin || awayWins >= homeWins)) ||
                    (awayWinner && (awayWins != context.Format.SetsToWin || homeWins >= awayWins)))
                    throw new MatchV2ContractException("winnerTeamId is inconsistent with completed set wins.");
            }
            else if (WinnerTeamId.HasValue || _sets.Length > 1)
            {
                throw new MatchV2ContractException("An abandoned result has no winner and at most one incomplete set.");
            }
            else if (_sets.Length == 0)
            {
                if (RallyCount != 0)
                    throw new MatchV2ContractException("An abandoned result without a set must have zero rallies.");
            }
            else
            {
                var set = _sets[0];
                var higher = Math.Max(set.HomePoints, set.AwayPoints);
                var reachedWinningScore = higher >= context.Format.SetTargetPoints &&
                                          Math.Abs(set.HomePoints - set.AwayPoints) >=
                                          context.Format.MinimumLeadPoints;
                if (set.IsComplete || reachedWinningScore || RallyCount != homePoints + awayPoints)
                    throw new MatchV2ContractException(
                        "An abandoned result requires one genuinely incomplete set and exact rallyCount.");
            }

            long homeTechnical = 0;
            long awayTechnical = 0;
            for (var index = 0; index < _playerFacts.Length; index++)
            {
                var technical = (long)_playerFacts[index].Spike.Points + _playerFacts[index].Serve.Aces +
                                _playerFacts[index].Block.Points;
                if (index < 6) homeTechnical += technical; else awayTechnical += technical;
            }
            if (homeTechnical > homePoints || awayTechnical > awayPoints)
                throw new MatchV2ContractException("Team technical points cannot exceed the team score.");
        }
    }
}
