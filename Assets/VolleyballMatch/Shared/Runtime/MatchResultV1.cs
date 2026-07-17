using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace VolleyballMatch.Shared.Contracts
{
    [DataContract]
    public sealed class PlayerMatchStatsV1
    {
        [DataMember(Name = "playerId", Order = 1)]
        private string _playerId;

        [DataMember(Name = "points", Order = 2)]
        private int _points;

        [DataMember(Name = "contacts", Order = 3)]
        private int _contacts;

        [DataMember(Name = "errors", Order = 4)]
        private int _errors;

        [DataMember(Name = "workload", Order = 5)]
        private float _workload;

        public PlayerMatchStatsV1(
            PlayerId playerId,
            int points,
            int contacts,
            int errors,
            float workload)
        {
            _playerId = ContractGuard.RequiredId(playerId.Value, nameof(playerId));
            _points = points;
            _contacts = contacts;
            _errors = errors;
            _workload = workload;
            Validate();
        }

        public PlayerId PlayerId => new PlayerId(_playerId);

        public int Points => _points;

        public int Contacts => _contacts;

        public int Errors => _errors;

        public float Workload => _workload;

        internal void Validate()
        {
            ContractGuard.RequiredId(_playerId, nameof(PlayerId));
            ContractGuard.NonNegative(_points, nameof(Points));
            ContractGuard.NonNegative(_contacts, nameof(Contacts));
            ContractGuard.NonNegative(_errors, nameof(Errors));
            if (float.IsNaN(_workload) || float.IsInfinity(_workload) || _workload < 0f)
            {
                throw new ContractValidationException("Workload must be finite and non-negative.");
            }
        }
    }

    [DataContract]
    public sealed class MatchResultV1
    {
        [DataMember(Name = "contractVersion", Order = 1)]
        private int _contractVersion;

        [DataMember(Name = "sessionId", Order = 2)]
        private string _sessionId;

        [DataMember(Name = "contextHash", Order = 3)]
        private string _contextHash;

        [DataMember(Name = "winnerTeamId", Order = 4)]
        private string _winnerTeamId;

        [DataMember(Name = "homeScore", Order = 5)]
        private int _homeScore;

        [DataMember(Name = "awayScore", Order = 6)]
        private int _awayScore;

        [DataMember(Name = "playerStats", Order = 7)]
        private PlayerMatchStatsV1[] _playerStats;

        private MatchResultV1(
            MatchContextV1 context,
            TeamId winnerTeamId,
            int homeScore,
            int awayScore,
            IReadOnlyList<PlayerMatchStatsV1> playerStats)
        {
            _contractVersion = context.ContractVersion;
            _sessionId = context.SessionId.ToString("D");
            _contextHash = context.ContextHash;
            _winnerTeamId = ContractGuard.RequiredId(winnerTeamId.Value, nameof(winnerTeamId));
            _homeScore = homeScore;
            _awayScore = awayScore;
            _playerStats = CopyStats(playerStats);
            Validate();
            ValidateAgainst(context);
        }

        public int ContractVersion => _contractVersion;

        public Guid SessionId => Guid.ParseExact(_sessionId, "D");

        public string ContextHash => _contextHash;

        public TeamId WinnerTeamId => new TeamId(_winnerTeamId);

        public int HomeScore => _homeScore;

        public int AwayScore => _awayScore;

        public IReadOnlyList<PlayerMatchStatsV1> PlayerStats =>
            new ReadOnlyCollection<PlayerMatchStatsV1>(_playerStats);

        public static MatchResultV1 Create(
            MatchContextV1 context,
            TeamId winnerTeamId,
            int homeScore,
            int awayScore,
            IReadOnlyList<PlayerMatchStatsV1> playerStats)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Validate();
            return new MatchResultV1(context, winnerTeamId, homeScore, awayScore, playerStats);
        }

        public void ValidateAgainst(MatchContextV1 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Validate();
            if (_contractVersion != context.ContractVersion ||
                !string.Equals(_sessionId, context.SessionId.ToString("D"), StringComparison.Ordinal) ||
                !string.Equals(_contextHash, context.ContextHash, StringComparison.Ordinal))
            {
                throw new ContractValidationException("The match result does not belong to this context.");
            }

            var winnerIsHome = WinnerTeamId.Equals(context.Home.TeamId);
            var winnerIsAway = WinnerTeamId.Equals(context.Away.TeamId);
            if (!winnerIsHome && !winnerIsAway)
            {
                throw new ContractValidationException("winnerTeamId is not part of the match context.");
            }

            if ((winnerIsHome && _homeScore <= _awayScore) ||
                (winnerIsAway && _awayScore <= _homeScore))
            {
                throw new ContractValidationException("winnerTeamId must match the higher final score.");
            }

            var contextPlayers = new HashSet<PlayerId>();
            AddContextPlayers(context.Home, contextPlayers);
            AddContextPlayers(context.Away, contextPlayers);
            foreach (var stats in _playerStats)
            {
                if (!contextPlayers.Contains(stats.PlayerId))
                {
                    throw new ContractValidationException("Player stats contain an ID outside the match context.");
                }
            }
        }

        internal void Validate()
        {
            if (!ContractVersions.SupportsMatch(_contractVersion))
            {
                throw new ContractValidationException(
                    "Unsupported match contract version: " + _contractVersion + ".");
            }

            if (!Guid.TryParseExact(_sessionId, "D", out var sessionId) || sessionId == Guid.Empty)
            {
                throw new ContractValidationException("sessionId must be a non-empty canonical GUID.");
            }

            ContractGuard.Hash(_contextHash, nameof(ContextHash));
            ContractGuard.RequiredId(_winnerTeamId, nameof(WinnerTeamId));
            ContractGuard.NonNegative(_homeScore, nameof(HomeScore));
            ContractGuard.NonNegative(_awayScore, nameof(AwayScore));
            if (_homeScore == _awayScore)
            {
                throw new ContractValidationException("A completed match result cannot be tied.");
            }

            if (_playerStats == null)
            {
                throw new ContractValidationException("playerStats is required.");
            }

            var ids = new HashSet<PlayerId>();
            for (var index = 0; index < _playerStats.Length; index++)
            {
                var stats = _playerStats[index];
                if (stats == null)
                {
                    throw new ContractValidationException("playerStats cannot contain null.");
                }

                stats.Validate();
                if (!ids.Add(stats.PlayerId))
                {
                    throw new ContractValidationException("playerStats must contain unique player IDs.");
                }
            }
        }

        private static PlayerMatchStatsV1[] CopyStats(IReadOnlyList<PlayerMatchStatsV1> stats)
        {
            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            var copy = new PlayerMatchStatsV1[stats.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = stats[index];
            }

            return copy;
        }

        private static void AddContextPlayers(TeamSnapshotV1 team, ISet<PlayerId> players)
        {
            foreach (var player in team.Players)
            {
                players.Add(player.PlayerId);
            }
        }
    }
}
