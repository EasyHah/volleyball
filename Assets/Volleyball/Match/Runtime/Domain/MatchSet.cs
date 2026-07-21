using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Domain
{
    public enum IndoorRotationPosition
    {
        BackRightServer = 1,
        FrontRight = 2,
        FrontCenter = 3,
        FrontLeft = 4,
        BackLeft = 5,
        BackCenter = 6
    }

    public sealed class MatchSet
    {
        public const int TargetScore = 15;
        public const int MinimumLead = 2;

        private readonly Dictionary<StablePlayerId, MutablePlayerStats> _statsByPlayer;
        private readonly Dictionary<StablePlayerId, TeamSide> _sideByPlayer;
        private readonly TeamRotation _homeRotation;
        private readonly TeamRotation _awayRotation;
        private readonly MatchSetRules _rules;
        private int _homeRotationOffset;
        private int _awayRotationOffset;

        public MatchSet(
            MatchContextV1 context,
            TeamSide firstServer,
            MatchSetRules rules = null)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (!Enum.IsDefined(typeof(TeamSide), firstServer))
            {
                throw new ArgumentOutOfRangeException(nameof(firstServer));
            }

            _statsByPlayer = new Dictionary<StablePlayerId, MutablePlayerStats>();
            _sideByPlayer = new Dictionary<StablePlayerId, TeamSide>();
            AddTeam(Context.Home);
            AddTeam(Context.Away);
            if (Context.Home.Players.Count != Context.Away.Players.Count)
            {
                throw new ArgumentException("Both court rosters must have the same size.", nameof(context));
            }

            _rules = rules ?? MatchSetRules.ForRosterSize(Context.Home.Players.Count);
            _homeRotation = new TeamRotation(Context.Home.Players.Select(player => player.PlayerId));
            _awayRotation = new TeamRotation(Context.Away.Players.Select(player => player.PlayerId));
            ServingSide = firstServer;
        }

        public MatchSet(
            MatchContextV2 context,
            TeamSide firstServer,
            MatchSetRules rules = null)
        {
            ContextV2 = context ?? throw new ArgumentNullException(nameof(context));
            if (!Enum.IsDefined(typeof(TeamSide), firstServer))
            {
                throw new ArgumentOutOfRangeException(nameof(firstServer));
            }

            _statsByPlayer = new Dictionary<StablePlayerId, MutablePlayerStats>();
            _sideByPlayer = new Dictionary<StablePlayerId, TeamSide>();
            AddTeam(ContextV2.Home);
            AddTeam(ContextV2.Away);
            if (ContextV2.Home.Players.Count != ContextV2.Away.Players.Count)
            {
                throw new ArgumentException("Both court rosters must have the same size.", nameof(context));
            }

            _rules = rules ?? MatchSetRules.ForRosterSize(ContextV2.Home.Players.Count);
            _homeRotation = new TeamRotation(ContextV2.Home.Players.Select(player => player.PlayerId));
            _awayRotation = new TeamRotation(ContextV2.Away.Players.Select(player => player.PlayerId));
            ServingSide = firstServer;
        }

        public MatchContextV1 Context { get; }

        public MatchContextV2 ContextV2 { get; }

        public int HomeScore { get; private set; }

        public int AwayScore { get; private set; }

        public TeamSide ServingSide { get; private set; }

        public TeamSide ReceivingSide => ServingSide == TeamSide.Home ? TeamSide.Away : TeamSide.Home;

        public bool IsComplete { get; private set; }

        public int SetTargetScore => _rules.TargetScore;

        public int MinimumWinningLead => _rules.MinimumLead;

        public int MaximumScore => _rules.MaximumScore;

        public int RosterSize => _homeRotation.PlayerCount;

        public TeamSide WinnerSide
        {
            get
            {
                if (!IsComplete)
                {
                    throw new InvalidOperationException("The set does not have a winner before completion.");
                }

                return HomeScore > AwayScore ? TeamSide.Home : TeamSide.Away;
            }
        }

        public int RotationOffsetFor(TeamSide side)
        {
            return side switch
            {
                TeamSide.Home => _homeRotationOffset,
                TeamSide.Away => _awayRotationOffset,
                _ => throw new ArgumentOutOfRangeException(nameof(side))
            };
        }

        public StablePlayerId ServerFor(TeamSide side)
        {
            return RotationFor(side).PlayerAtPosition(1);
        }

        public StablePlayerId PlayerAtRotationPosition(TeamSide side, int position)
        {
            return RotationFor(side).PlayerAtPosition(position);
        }

        public StablePlayerId PlayerAtRotationPosition(
            TeamSide side,
            IndoorRotationPosition position)
        {
            if (RosterSize != 6)
            {
                throw new InvalidOperationException("Named indoor rotation positions require a six-player roster.");
            }

            return PlayerAtRotationPosition(side, (int)position);
        }

        public int RotationPositionFor(StablePlayerId playerId)
        {
            StatsFor(playerId);
            return RotationFor(_sideByPlayer[playerId]).PositionOf(playerId);
        }

        public bool IsFrontRow(StablePlayerId playerId)
        {
            var position = RotationPositionFor(playerId);
            return RosterSize == 6 && position >= 2 && position <= 4;
        }

        public void RecordContact(StablePlayerId playerId, float movementDistance)
        {
            EnsureActive();
            if (float.IsNaN(movementDistance) || float.IsInfinity(movementDistance) || movementDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(movementDistance));
            }

            var stats = StatsFor(playerId);
            stats.Contacts++;
            stats.Workload += 1f + movementDistance;
        }

        public void ResolveRally(
            TeamSide winner,
            StablePlayerId? pointScorer,
            StablePlayerId? errorPlayer)
        {
            EnsureActive();
            if (!Enum.IsDefined(typeof(TeamSide), winner))
            {
                throw new ArgumentOutOfRangeException(nameof(winner));
            }

            if (pointScorer.HasValue)
            {
                ValidatePlayerSide(pointScorer.Value, winner, nameof(pointScorer));
                StatsFor(pointScorer.Value).Points++;
            }

            if (errorPlayer.HasValue)
            {
                ValidatePlayerSide(errorPlayer.Value, OpponentOf(winner), nameof(errorPlayer));
                StatsFor(errorPlayer.Value).Errors++;
            }

            if (winner == TeamSide.Home)
            {
                HomeScore++;
            }
            else
            {
                AwayScore++;
            }

            if (ServingSide != winner)
            {
                ServingSide = winner;
                Rotate(winner);
            }

            var highestScore = Math.Max(HomeScore, AwayScore);
            IsComplete = highestScore >= _rules.MaximumScore ||
                         highestScore >= _rules.TargetScore &&
                         Math.Abs(HomeScore - AwayScore) >= _rules.MinimumLead;
        }

        public MatchResultV1 CreateResult()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("A result is available only after the set completes.");
            }

            var winner = WinnerSide == TeamSide.Home ? Context.Home.TeamId : Context.Away.TeamId;
            var stats = _statsByPlayer
                .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
                .Select(entry => entry.Value.ToContract(entry.Key))
                .ToArray();
            return MatchResultV1.Create(Context, winner, HomeScore, AwayScore, stats);
        }

        public MatchResultV2 CreateResultV2()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("A result is available only after the set completes.");
            }

            if (ContextV2 == null)
            {
                throw new InvalidOperationException("This set was created with a V1 context.");
            }

            var winner = WinnerSide == TeamSide.Home ? ContextV2.Home.TeamId : ContextV2.Away.TeamId;
            var stats = _statsByPlayer
                .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
                .Select(entry => entry.Value.ToContractV2(entry.Key))
                .ToArray();
            return MatchResultV2.Create(ContextV2, winner, HomeScore, AwayScore, stats);
        }

        private void AddTeam(TeamSnapshotV1 team)
        {
            foreach (var player in team.Players)
            {
                _statsByPlayer.Add(player.PlayerId, new MutablePlayerStats());
                _sideByPlayer.Add(player.PlayerId, team.Side);
            }
        }

        private void AddTeam(TeamSnapshotV2 team)
        {
            foreach (var player in team.Players)
            {
                _statsByPlayer.Add(player.PlayerId, new MutablePlayerStats());
                _sideByPlayer.Add(player.PlayerId, team.Side);
            }
        }

        private void EnsureActive()
        {
            if (IsComplete)
            {
                throw new InvalidOperationException("The set is already complete.");
            }
        }

        private MutablePlayerStats StatsFor(StablePlayerId playerId)
        {
            if (!_statsByPlayer.TryGetValue(playerId, out var stats))
            {
                throw new ArgumentException("The player is not in the match context.", nameof(playerId));
            }

            return stats;
        }

        private void ValidatePlayerSide(StablePlayerId playerId, TeamSide expectedSide, string parameterName)
        {
            StatsFor(playerId);
            if (_sideByPlayer[playerId] != expectedSide)
            {
                throw new ArgumentException("The player is on the wrong side for this rally attribution.", parameterName);
            }
        }

        private void Rotate(TeamSide side)
        {
            if (side == TeamSide.Home)
            {
                _homeRotation.RotateClockwise();
                _homeRotationOffset = _homeRotation.Offset;
            }
            else
            {
                _awayRotation.RotateClockwise();
                _awayRotationOffset = _awayRotation.Offset;
            }
        }

        private TeamRotation RotationFor(TeamSide side)
        {
            return side switch
            {
                TeamSide.Home => _homeRotation,
                TeamSide.Away => _awayRotation,
                _ => throw new ArgumentOutOfRangeException(nameof(side))
            };
        }

        private static TeamSide OpponentOf(TeamSide side)
        {
            return side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        }

        private sealed class MutablePlayerStats
        {
            public int Points;
            public int Contacts;
            public int Errors;
            public float Workload;

            public PlayerMatchStatsV1 ToContract(StablePlayerId playerId)
            {
                return new PlayerMatchStatsV1(playerId, Points, Contacts, Errors, Workload);
            }

            public PlayerMatchStatsV2 ToContractV2(StablePlayerId playerId)
            {
                return new PlayerMatchStatsV2(playerId, Points, Contacts, Errors, Workload);
            }
        }
    }

    public sealed class MatchSetRules
    {
        public static MatchSetRules ThreeVsThree { get; } = new MatchSetRules(15, 2);

        public static MatchSetRules FormalIndoor { get; } = new MatchSetRules(25, 2);

        public MatchSetRules(int targetScore, int minimumLead, int maximumScore = 50)
        {
            if (targetScore <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetScore));
            }

            if (minimumLead <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumLead));
            }

            if (maximumScore < targetScore)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumScore));
            }

            TargetScore = targetScore;
            MinimumLead = minimumLead;
            MaximumScore = maximumScore;
        }

        public int TargetScore { get; }

        public int MinimumLead { get; }

        public int MaximumScore { get; }

        public static MatchSetRules ForRosterSize(int rosterSize)
        {
            return rosterSize == 6 ? FormalIndoor : ThreeVsThree;
        }
    }

    internal sealed class TeamRotation
    {
        private readonly StablePlayerId[] _initialOrder;

        public TeamRotation(IEnumerable<StablePlayerId> players)
        {
            _initialOrder = players?.ToArray() ?? throw new ArgumentNullException(nameof(players));
            if (_initialOrder.Length < 1)
            {
                throw new ArgumentException("A rotation requires at least one player.", nameof(players));
            }

            if (_initialOrder.Distinct().Count() != _initialOrder.Length)
            {
                throw new ArgumentException("Rotation players must be unique.", nameof(players));
            }
        }

        public int PlayerCount => _initialOrder.Length;

        public int Offset { get; private set; }

        public void RotateClockwise()
        {
            Offset = (Offset + 1) % PlayerCount;
        }

        public StablePlayerId PlayerAtPosition(int position)
        {
            if (position < 1 || position > PlayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return _initialOrder[(position - 1 + Offset) % PlayerCount];
        }

        public int PositionOf(StablePlayerId playerId)
        {
            for (var position = 1; position <= PlayerCount; position++)
            {
                if (PlayerAtPosition(position).Equals(playerId))
                {
                    return position;
                }
            }

            throw new ArgumentException("The player is not in this rotation.", nameof(playerId));
        }
    }
}
