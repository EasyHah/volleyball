using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class PlayerMatchStatsV4
    {
        public PlayerMatchStatsV4(
            PlayerId playerId,
            int points,
            int contacts,
            int errors,
            float workload)
        {
            PlayerId = new PlayerId(ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            Points = points;
            Contacts = contacts;
            Errors = errors;
            Workload = workload;
            Validate();
        }

        public PlayerId PlayerId { get; }
        public int Points { get; }
        public int Contacts { get; }
        public int Errors { get; }
        public float Workload { get; }

        internal void Validate()
        {
            ContractGuard.RequiredId(PlayerId.Value, nameof(PlayerId));
            ContractGuard.NonNegative(Points, nameof(Points));
            ContractGuard.NonNegative(Contacts, nameof(Contacts));
            ContractGuard.NonNegative(Errors, nameof(Errors));
            if (float.IsNaN(Workload) ||
                float.IsInfinity(Workload) ||
                Workload < 0f)
            {
                throw new ContractValidationException(
                    "Workload must be finite and non-negative.");
            }
        }
    }

    public sealed class MatchResultV4
    {
        private readonly PlayerMatchStatsV4[] _playerStats;
        private readonly ReadOnlyCollection<PlayerMatchStatsV4> _readOnlyPlayerStats;

        private MatchResultV4(
            Guid sessionId,
            string contextHash,
            TeamId winnerTeamId,
            int homeScore,
            int awayScore,
            int ralliesPlayed,
            int acceptedContacts,
            int v3RuleTransitionCount,
            IReadOnlyList<PlayerMatchStatsV4> playerStats)
        {
            ContractVersion = ContractVersions.MatchV4;
            SessionId = sessionId;
            ContextHash = contextHash;
            WinnerTeamId = new TeamId(
                ContractGuard.RequiredId(winnerTeamId.Value, nameof(winnerTeamId)));
            HomeScore = homeScore;
            AwayScore = awayScore;
            RalliesPlayed = ralliesPlayed;
            AcceptedContacts = acceptedContacts;
            V3RuleTransitionCount = v3RuleTransitionCount;
            _playerStats = CopyStats(playerStats);
            _readOnlyPlayerStats =
                new ReadOnlyCollection<PlayerMatchStatsV4>(_playerStats);
            ValidatePayload();
            ResultHash = CanonicalMatchResultHashV4.Compute(this);
            Validate();
        }

        public int ContractVersion { get; }
        public Guid SessionId { get; }
        public string ContextHash { get; }
        public string ResultHash { get; }
        public TeamId WinnerTeamId { get; }
        public int HomeScore { get; }
        public int AwayScore { get; }
        public int RalliesPlayed { get; }
        public int AcceptedContacts { get; }
        public int V3RuleTransitionCount { get; }
        public IReadOnlyList<PlayerMatchStatsV4> PlayerStats => _readOnlyPlayerStats;

        public static MatchResultV4 Create(
            MatchContextV4 context,
            TeamId winnerTeamId,
            int homeScore,
            int awayScore,
            int ralliesPlayed,
            int acceptedContacts,
            int v3RuleTransitionCount,
            IReadOnlyList<PlayerMatchStatsV4> playerStats)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Validate();
            var result = new MatchResultV4(
                context.SessionId,
                context.ContextHash,
                winnerTeamId,
                homeScore,
                awayScore,
                ralliesPlayed,
                acceptedContacts,
                v3RuleTransitionCount,
                playerStats);
            result.ValidateAgainst(context);
            return result;
        }

        internal static MatchResultV4 Restore(
            Guid sessionId,
            string contextHash,
            TeamId winnerTeamId,
            int homeScore,
            int awayScore,
            int ralliesPlayed,
            int acceptedContacts,
            int v3RuleTransitionCount,
            IReadOnlyList<PlayerMatchStatsV4> playerStats)
        {
            return new MatchResultV4(
                sessionId,
                contextHash,
                winnerTeamId,
                homeScore,
                awayScore,
                ralliesPlayed,
                acceptedContacts,
                v3RuleTransitionCount,
                playerStats);
        }

        public void ValidateAgainst(MatchContextV4 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Validate();
            if (ContractVersion != context.ContractVersion ||
                SessionId != context.SessionId ||
                !string.Equals(
                    ContextHash,
                    context.ContextHash,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "The V4 match result does not belong to this context.");
            }

            var winnerIsHome = WinnerTeamId.Equals(context.Home.TeamId);
            var winnerIsAway = WinnerTeamId.Equals(context.Away.TeamId);
            if (!winnerIsHome && !winnerIsAway)
            {
                throw new ContractValidationException(
                    "winnerTeamId is not part of the match context.");
            }

            if ((winnerIsHome && HomeScore <= AwayScore) ||
                (winnerIsAway && AwayScore <= HomeScore))
            {
                throw new ContractValidationException(
                    "winnerTeamId must match the higher final score.");
            }

            var playerIds = new HashSet<PlayerId>();
            AddPlayers(context.Home, playerIds);
            AddPlayers(context.Away, playerIds);
            foreach (var stats in _playerStats)
            {
                if (!playerIds.Contains(stats.PlayerId))
                {
                    throw new ContractValidationException(
                        "Player stats contain an ID outside the match context.");
                }
            }
        }

        internal void Validate()
        {
            ValidatePayload();
            ContractGuard.Hash(ResultHash, nameof(ResultHash));
            if (!string.Equals(
                    CanonicalMatchResultHashV4.Compute(this),
                    ResultHash,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "resultHash does not match the native V4 result payload.");
            }
        }

        private void ValidatePayload()
        {
            if (ContractVersion != ContractVersions.MatchV4)
            {
                throw new ContractValidationException(
                    "Unsupported match contract version: " + ContractVersion + ".");
            }

            if (SessionId == Guid.Empty)
            {
                throw new ContractValidationException(
                    "sessionId must be a non-empty canonical GUID.");
            }

            ContractGuard.Hash(ContextHash, nameof(ContextHash));
            ContractGuard.RequiredId(WinnerTeamId.Value, nameof(WinnerTeamId));
            ContractGuard.NonNegative(HomeScore, nameof(HomeScore));
            ContractGuard.NonNegative(AwayScore, nameof(AwayScore));
            if (HomeScore == AwayScore)
            {
                throw new ContractValidationException(
                    "A completed V4 match result cannot be tied.");
            }

            ContractGuard.NonNegative(RalliesPlayed, nameof(RalliesPlayed));
            if (RalliesPlayed != HomeScore + AwayScore)
            {
                throw new ContractValidationException(
                    "ralliesPlayed must equal the final score total.");
            }

            ContractGuard.NonNegative(AcceptedContacts, nameof(AcceptedContacts));
            ContractGuard.NonNegative(
                V3RuleTransitionCount,
                nameof(V3RuleTransitionCount));
            if (V3RuleTransitionCount > AcceptedContacts)
            {
                throw new ContractValidationException(
                    "V3 rule-transition count cannot exceed accepted contacts.");
            }

            if (_playerStats == null)
            {
                throw new ContractValidationException("playerStats is required.");
            }

            var ids = new HashSet<PlayerId>();
            foreach (var stats in _playerStats)
            {
                if (stats == null)
                {
                    throw new ContractValidationException(
                        "playerStats cannot contain null.");
                }

                stats.Validate();
                if (!ids.Add(stats.PlayerId))
                {
                    throw new ContractValidationException(
                        "playerStats must contain unique player IDs.");
                }
            }
        }

        private static PlayerMatchStatsV4[] CopyStats(
            IReadOnlyList<PlayerMatchStatsV4> playerStats)
        {
            if (playerStats == null)
            {
                throw new ContractValidationException("playerStats is required.");
            }

            var copy = new PlayerMatchStatsV4[playerStats.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = playerStats[index];
            }

            return copy;
        }

        private static void AddPlayers(
            TeamSnapshotV4 team,
            ISet<PlayerId> playerIds)
        {
            foreach (var player in team.RotationOrder)
            {
                playerIds.Add(player.PlayerId);
            }
        }
    }

    public static class CanonicalMatchResultHashV4
    {
        private const string HashFamily = "volleyball.match-result.v4\n";

        public static string Compute(MatchResultV4 result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return CanonicalJsonHashV4.Sha256(
                HashFamily + CanonicalMatchResultJsonV4.ResultPayload(result));
        }
    }

    internal static class CanonicalMatchResultJsonV4
    {
        public static string SerializeResult(MatchResultV4 result)
        {
            var payload = ResultPayload(result);
            return payload.Substring(0, payload.Length - 1) +
                ",\"resultHash\":" +
                CanonicalMatchJsonV4.Quote(result.ResultHash) +
                "}";
        }

        public static string ResultPayload(MatchResultV4 result)
        {
            var output = new StringBuilder(2048);
            output.Append("{\"contractVersion\":")
                .Append(result.ContractVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"sessionId\":")
                .Append(CanonicalMatchJsonV4.Quote(result.SessionId.ToString("D")));
            output.Append(",\"contextHash\":")
                .Append(CanonicalMatchJsonV4.Quote(result.ContextHash));
            output.Append(",\"winnerTeamId\":")
                .Append(CanonicalMatchJsonV4.Quote(result.WinnerTeamId.Value));
            output.Append(",\"homeScore\":")
                .Append(result.HomeScore.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"awayScore\":")
                .Append(result.AwayScore.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"ralliesPlayed\":")
                .Append(result.RalliesPlayed.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"acceptedContacts\":")
                .Append(result.AcceptedContacts.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"v3RuleTransitionCount\":")
                .Append(result.V3RuleTransitionCount.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"playerStats\":[");
            for (var index = 0; index < result.PlayerStats.Count; index++)
            {
                if (index > 0)
                {
                    output.Append(',');
                }

                var stats = result.PlayerStats[index];
                output.Append("{\"playerId\":")
                    .Append(CanonicalMatchJsonV4.Quote(stats.PlayerId.Value));
                output.Append(",\"points\":")
                    .Append(stats.Points.ToString(CultureInfo.InvariantCulture));
                output.Append(",\"contacts\":")
                    .Append(stats.Contacts.ToString(CultureInfo.InvariantCulture));
                output.Append(",\"errors\":")
                    .Append(stats.Errors.ToString(CultureInfo.InvariantCulture));
                CanonicalMatchJsonV4.AppendFloat(
                    output,
                    "workload",
                    stats.Workload);
                output.Append('}');
            }

            output.Append("]}");
            return output.ToString();
        }
    }
}
