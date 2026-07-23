using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Runtime.Serialization;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class PlayerMatchStatsV3
    {
        [DataMember(Name = "playerId", Order = 1)] private string _playerId;
        [DataMember(Name = "points", Order = 2)] private int _points;
        [DataMember(Name = "contacts", Order = 3)] private int _contacts;
        [DataMember(Name = "errors", Order = 4)] private int _errors;
        [DataMember(Name = "workload", Order = 5)] private float _workload;

        public PlayerMatchStatsV3(PlayerId playerId, int points, int contacts, int errors, float workload)
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
                throw new ContractValidationException("Workload must be finite and non-negative.");
        }
    }

    [DataContract]
    public sealed class MatchResultV3 : IMatchResult
    {
        [DataMember(Name = "contractVersion", Order = 1)] private int _contractVersion;
        [DataMember(Name = "sessionId", Order = 2)] private string _sessionId;
        [DataMember(Name = "contextHash", Order = 3)] private string _contextHash;
        [DataMember(Name = "resultHash", Order = 4)] private string _resultHash;
        [DataMember(Name = "winnerTeamId", Order = 5)] private string _winnerTeamId;
        [DataMember(Name = "homeScore", Order = 6)] private int _homeScore;
        [DataMember(Name = "awayScore", Order = 7)] private int _awayScore;
        [DataMember(Name = "playerStats", Order = 8)] private PlayerMatchStatsV3[] _playerStats;

        private MatchResultV3(MatchContextV3 context, TeamId winnerTeamId, int homeScore, int awayScore,
            IReadOnlyList<PlayerMatchStatsV3> playerStats)
        {
            _contractVersion = context.ContractVersion;
            _sessionId = context.SessionId.ToString("D");
            _contextHash = context.ContextHash;
            _resultHash = string.Empty;
            _winnerTeamId = ContractGuard.RequiredId(winnerTeamId.Value, nameof(winnerTeamId));
            _homeScore = homeScore;
            _awayScore = awayScore;
            _playerStats = CopyStats(playerStats);
            ValidatePayload();
            ValidateAgainst(context);
            _resultHash = CanonicalMatchResultHashV3.Compute(this);
            Validate();
        }

        public int ContractVersion => _contractVersion;
        public Guid SessionId => Guid.ParseExact(_sessionId, "D");
        public string ContextHash => _contextHash;
        public string ResultHash => _resultHash;
        public TeamId WinnerTeamId => new TeamId(_winnerTeamId);
        public int HomeScore => _homeScore;
        public int AwayScore => _awayScore;
        public IReadOnlyList<PlayerMatchStatsV3> PlayerStats => new ReadOnlyCollection<PlayerMatchStatsV3>(_playerStats);

        public static MatchResultV3 Create(MatchContextV3 context, TeamId winnerTeamId, int homeScore,
            int awayScore, IReadOnlyList<PlayerMatchStatsV3> playerStats)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            return new MatchResultV3(context, winnerTeamId, homeScore, awayScore, playerStats);
        }

        public void ValidateAgainst(MatchContextV3 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            if (_contractVersion != context.ContractVersion ||
                !string.Equals(_sessionId, context.SessionId.ToString("D"), StringComparison.Ordinal) ||
                !string.Equals(_contextHash, context.ContextHash, StringComparison.Ordinal))
                throw new ContractValidationException("The match result does not belong to this context.");
            var winnerIsHome = WinnerTeamId.Equals(context.Home.TeamId);
            var winnerIsAway = WinnerTeamId.Equals(context.Away.TeamId);
            if (!winnerIsHome && !winnerIsAway) throw new ContractValidationException("winnerTeamId is not part of the match context.");
            if ((winnerIsHome && _homeScore <= _awayScore) || (winnerIsAway && _awayScore <= _homeScore))
                throw new ContractValidationException("winnerTeamId must match the higher final score.");
            var playerIds = new HashSet<PlayerId>();
            AddPlayers(context.Home, playerIds);
            AddPlayers(context.Away, playerIds);
            foreach (var stats in _playerStats)
                if (!playerIds.Contains(stats.PlayerId)) throw new ContractValidationException("Player stats contain an ID outside the match context.");
        }

        internal void Validate()
        {
            ValidatePayload();
            ContractGuard.Hash(_resultHash, nameof(ResultHash));
            if (!string.Equals(CanonicalMatchResultHashV3.Compute(this), _resultHash, StringComparison.Ordinal))
                throw new ContractValidationException("resultHash does not match the result payload.");
        }

        private void ValidatePayload()
        {
            if (_contractVersion != ContractVersions.MatchV3) throw new ContractValidationException("Unsupported match contract version: " + _contractVersion + ".");
            if (!Guid.TryParseExact(_sessionId, "D", out var sessionId) || sessionId == Guid.Empty) throw new ContractValidationException("sessionId must be a non-empty canonical GUID.");
            ContractGuard.Hash(_contextHash, nameof(ContextHash));
            ContractGuard.RequiredId(_winnerTeamId, nameof(WinnerTeamId));
            ContractGuard.NonNegative(_homeScore, nameof(HomeScore));
            ContractGuard.NonNegative(_awayScore, nameof(AwayScore));
            if (_homeScore == _awayScore) throw new ContractValidationException("A completed match result cannot be tied.");
            if (_playerStats == null) throw new ContractValidationException("playerStats is required.");
            var ids = new HashSet<PlayerId>();
            foreach (var stats in _playerStats)
            {
                if (stats == null) throw new ContractValidationException("playerStats cannot contain null.");
                stats.Validate();
                if (!ids.Add(stats.PlayerId)) throw new ContractValidationException("playerStats must contain unique player IDs.");
            }
        }

        private static PlayerMatchStatsV3[] CopyStats(IReadOnlyList<PlayerMatchStatsV3> stats)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            var copy = new PlayerMatchStatsV3[stats.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = stats[index];
            return copy;
        }

        private static void AddPlayers(TeamSnapshotV3 team, ISet<PlayerId> playerIds)
        {
            foreach (var player in team.Players) playerIds.Add(player.PlayerId);
        }
    }

    public static class CanonicalMatchResultHashV3
    {
        public static string Compute(MatchResultV3 result)
        {
            var canonical = new StringBuilder(1024);
            canonical.Append("{\"contractVersion\":").Append(result.ContractVersion.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"sessionId\":"); CanonicalJsonHashV3.AppendString(canonical, result.SessionId.ToString("D"));
            canonical.Append(",\"contextHash\":"); CanonicalJsonHashV3.AppendString(canonical, result.ContextHash);
            canonical.Append(",\"winnerTeamId\":"); CanonicalJsonHashV3.AppendString(canonical, result.WinnerTeamId.Value);
            canonical.Append(",\"homeScore\":").Append(result.HomeScore.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"awayScore\":").Append(result.AwayScore.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"playerStats\":[");
            for (var index = 0; index < result.PlayerStats.Count; index++)
            {
                if (index > 0) canonical.Append(',');
                var stats = result.PlayerStats[index];
                canonical.Append("{\"playerId\":"); CanonicalJsonHashV3.AppendString(canonical, stats.PlayerId.Value);
                canonical.Append(",\"points\":").Append(stats.Points.ToString(CultureInfo.InvariantCulture));
                canonical.Append(",\"contacts\":").Append(stats.Contacts.ToString(CultureInfo.InvariantCulture));
                canonical.Append(",\"errors\":").Append(stats.Errors.ToString(CultureInfo.InvariantCulture));
                CanonicalJsonHashV3.AppendFloat(canonical, "workload", stats.Workload);
                canonical.Append('}');
            }
            canonical.Append("]}");
            return CanonicalJsonHashV3.Sha256(canonical.ToString());
        }
    }
}
