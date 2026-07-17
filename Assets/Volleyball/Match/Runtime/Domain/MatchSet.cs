using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Domain
{
    public sealed class MatchSet
    {
        public const int TargetScore = 15;
        public const int MinimumLead = 2;

        private readonly Dictionary<StablePlayerId, MutablePlayerStats> _statsByPlayer;
        private readonly Dictionary<StablePlayerId, TeamSide> _sideByPlayer;
        private int _homeRotationOffset;
        private int _awayRotationOffset;

        public MatchSet(MatchContextV1 context, TeamSide firstServer)
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
            ServingSide = firstServer;
        }

        public MatchContextV1 Context { get; }

        public int HomeScore { get; private set; }

        public int AwayScore { get; private set; }

        public TeamSide ServingSide { get; private set; }

        public TeamSide ReceivingSide => ServingSide == TeamSide.Home ? TeamSide.Away : TeamSide.Home;

        public bool IsComplete { get; private set; }

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

            IsComplete = Math.Max(HomeScore, AwayScore) >= TargetScore &&
                         Math.Abs(HomeScore - AwayScore) >= MinimumLead;
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

        private void AddTeam(TeamSnapshotV1 team)
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
                _homeRotationOffset = (_homeRotationOffset + 1) % 3;
            }
            else
            {
                _awayRotationOffset = (_awayRotationOffset + 1) % 3;
            }
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
        }
    }
}
