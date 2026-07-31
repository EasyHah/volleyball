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

    public interface IMatchSetRuntime
    {
        int HomeScore { get; }
        int AwayScore { get; }
        TeamSide ServingSide { get; }
        TeamSide ReceivingSide { get; }
        bool IsComplete { get; }
        int SetTargetScore { get; }
        int RosterSize { get; }
        int RotationOffsetFor(TeamSide side);
        StablePlayerId ServerFor(TeamSide side);
        StablePlayerId PlayerAtRotationPosition(TeamSide side, int position);
        int RotationPositionFor(StablePlayerId playerId);
        bool IsFrontRow(StablePlayerId playerId);
        void RecordContact(StablePlayerId playerId, float movementDistance);
        void ResolveRally(
            TeamSide winner,
            StablePlayerId? pointScorer,
            StablePlayerId? errorPlayer);
    }

    internal readonly struct RuntimePlayerStats
    {
        public RuntimePlayerStats(
            StablePlayerId playerId,
            int points,
            int contacts,
            int errors,
            float workload)
        {
            PlayerId = playerId;
            Points = points;
            Contacts = contacts;
            Errors = errors;
            Workload = workload;
        }

        public StablePlayerId PlayerId { get; }
        public int Points { get; }
        public int Contacts { get; }
        public int Errors { get; }
        public float Workload { get; }
    }

    public sealed class MatchSet : IMatchSetRuntime
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
            MatchContextV4 context,
            TeamSide firstServer,
            MatchSetRules rules = null,
            int homeInitialRotationOffset = 0,
            int awayInitialRotationOffset = 0)
            : this(
                (context ?? throw new ArgumentNullException(nameof(context))).Home.Players
                    .Select(player => player.PlayerId),
                context.Away.Players.Select(player => player.PlayerId),
                firstServer,
                rules,
                homeInitialRotationOffset,
                awayInitialRotationOffset)
        {
            Context = context;
        }

        public MatchSet(
            MatchContextV4 context,
            IEnumerable<StablePlayerId> activeHomePlayers,
            IEnumerable<StablePlayerId> activeAwayPlayers,
            TeamSide firstServer,
            MatchSetRules rules = null)
            : this(
                CreateActiveRosterSnapshot(
                    context,
                    activeHomePlayers,
                    activeAwayPlayers),
                firstServer,
                rules)
        {
        }

        private MatchSet(
            ActiveRosterSnapshot activeRoster,
            TeamSide firstServer,
            MatchSetRules rules)
            : this(
                activeRoster.HomePlayers,
                activeRoster.AwayPlayers,
                firstServer,
                rules)
        {
            Context = activeRoster.Context;
        }

        public MatchSet(
            IEnumerable<StablePlayerId> homePlayers,
            IEnumerable<StablePlayerId> awayPlayers,
            TeamSide firstServer,
            MatchSetRules rules = null,
            int homeInitialRotationOffset = 0,
            int awayInitialRotationOffset = 0)
        {
            if (!Enum.IsDefined(typeof(TeamSide), firstServer))
            {
                throw new ArgumentOutOfRangeException(nameof(firstServer));
            }

            var home = (homePlayers ?? throw new ArgumentNullException(nameof(homePlayers))).ToArray();
            var away = (awayPlayers ?? throw new ArgumentNullException(nameof(awayPlayers))).ToArray();
            if (home.Length != away.Length)
            {
                throw new ArgumentException("Both court rosters must have the same size.");
            }

            _statsByPlayer = new Dictionary<StablePlayerId, MutablePlayerStats>();
            _sideByPlayer = new Dictionary<StablePlayerId, TeamSide>();
            AddTeam(home, TeamSide.Home);
            AddTeam(away, TeamSide.Away);
            _rules = rules ?? MatchSetRules.ForRosterSize(home.Length);
            _homeRotation = new TeamRotation(home, homeInitialRotationOffset);
            _awayRotation = new TeamRotation(away, awayInitialRotationOffset);
            _homeRotationOffset = _homeRotation.Offset;
            _awayRotationOffset = _awayRotation.Offset;
            ServingSide = firstServer;
        }

        public MatchContextV4 Context { get; }

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

        public MatchResultV4 CreateResult(
            int acceptedContacts,
            int v3RuleTransitionCount)
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("A result is available only after the set completes.");
            }

            var winner = WinnerSide == TeamSide.Home ? Context.Home.TeamId : Context.Away.TeamId;
            var maximumWorkload = _statsByPlayer.Values
                .Select(entry => entry.Workload)
                .DefaultIfEmpty(0f)
                .Max();
            var stats = _statsByPlayer
                .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
                .Select(entry => entry.Value.ToContractV4(entry.Key, maximumWorkload))
                .ToArray();
            return MatchResultV4.Create(
                Context,
                winner,
                HomeScore,
                AwayScore,
                HomeScore + AwayScore,
                acceptedContacts,
                v3RuleTransitionCount,
                stats);
        }

        internal RuntimePlayerStats[] CreateRuntimeStats()
        {
            return _statsByPlayer
                .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
                .Select(entry => entry.Value.ToRuntime(entry.Key))
                .ToArray();
        }

        private void AddTeam(
            IEnumerable<StablePlayerId> playerIds,
            TeamSide side)
        {
            foreach (var playerId in playerIds)
            {
                _statsByPlayer.Add(playerId, new MutablePlayerStats());
                _sideByPlayer.Add(playerId, side);
            }
        }

        private static ActiveRosterSnapshot CreateActiveRosterSnapshot(
            MatchContextV4 context,
            IEnumerable<StablePlayerId> activeHomePlayers,
            IEnumerable<StablePlayerId> activeAwayPlayers)
        {
            var matchContext =
                context ?? throw new ArgumentNullException(nameof(context));
            var homePlayers =
                (activeHomePlayers ??
                 throw new ArgumentNullException(nameof(activeHomePlayers)))
                .ToArray();
            var awayPlayers =
                (activeAwayPlayers ??
                 throw new ArgumentNullException(nameof(activeAwayPlayers)))
                .ToArray();
            if (homePlayers.Length != awayPlayers.Length)
            {
                throw new ArgumentException(
                    "Both active court rosters must have the same size.");
            }

            if (homePlayers.Length < 1)
            {
                throw new ArgumentException(
                    "Active court rosters must contain at least one player.");
            }

            ValidateActivePlayers(
                matchContext.Home,
                homePlayers,
                nameof(activeHomePlayers));
            ValidateActivePlayers(
                matchContext.Away,
                awayPlayers,
                nameof(activeAwayPlayers));
            if (homePlayers.Intersect(awayPlayers).Any())
            {
                throw new ArgumentException(
                    "Active players cannot appear for both teams.");
            }

            return new ActiveRosterSnapshot(
                matchContext,
                homePlayers,
                awayPlayers);
        }

        private static void ValidateActivePlayers(
            TeamSnapshotV4 team,
            IReadOnlyCollection<StablePlayerId> activePlayers,
            string parameterName)
        {
            if (activePlayers.Distinct().Count() != activePlayers.Count)
            {
                throw new ArgumentException(
                    "Active players must be unique within each team.",
                    parameterName);
            }

            var rosterIds = new HashSet<StablePlayerId>(
                team.Players.Select(player => player.PlayerId));
            foreach (var playerId in activePlayers)
            {
                if (!rosterIds.Contains(playerId))
                {
                    throw new ArgumentException(
                        "Every active player must belong to the matching V4 team.",
                        parameterName);
                }
            }
        }

        private sealed class ActiveRosterSnapshot
        {
            public ActiveRosterSnapshot(
                MatchContextV4 context,
                StablePlayerId[] homePlayers,
                StablePlayerId[] awayPlayers)
            {
                Context = context;
                HomePlayers = homePlayers;
                AwayPlayers = awayPlayers;
            }

            public MatchContextV4 Context { get; }
            public IReadOnlyList<StablePlayerId> HomePlayers { get; }
            public IReadOnlyList<StablePlayerId> AwayPlayers { get; }
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

            public RuntimePlayerStats ToRuntime(StablePlayerId playerId)
            {
                return new RuntimePlayerStats(
                    playerId,
                    Points,
                    Contacts,
                    Errors,
                    Workload);
            }

            public PlayerMatchStatsV4 ToContractV4(
                StablePlayerId playerId,
                float maximumWorkload)
            {
                var normalizedWorkload = maximumWorkload <= 0f
                    ? 0f
                    : Math.Min(1f, Workload / maximumWorkload);
                return new PlayerMatchStatsV4(
                    playerId,
                    Points,
                    Contacts,
                    Errors,
                    normalizedWorkload);
            }
        }
    }

    public sealed class MatchSetRules
    {
        public const int AbsoluteMaximumScore = 50;

        public static MatchSetRules ThreeVsThree { get; } = new MatchSetRules(15, 2);

        public static MatchSetRules FormalIndoor { get; } = new MatchSetRules(25, 2);

        public MatchSetRules(
            int targetScore,
            int minimumLead,
            int maximumScore = AbsoluteMaximumScore)
        {
            if (targetScore <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetScore));
            }

            if (minimumLead <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumLead));
            }

            if (maximumScore < targetScore || maximumScore > AbsoluteMaximumScore)
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

        public TeamRotation(IEnumerable<StablePlayerId> players,
            int initialOffset = 0)
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

            if (initialOffset < 0 || initialOffset >= _initialOrder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(initialOffset));
            }

            Offset = initialOffset;
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
