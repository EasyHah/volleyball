using System;
using System.Globalization;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class MatchResultV5
    {
        private MatchResultV5(MatchContextV5 context, TeamId winnerTeamId, int homeScore,
            int awayScore, int ralliesPlayed)
        {
            context.Validate();
            if (homeScore < 0 || awayScore < 0 || homeScore == awayScore ||
                ralliesPlayed != homeScore + awayScore)
            {
                throw new ContractValidationException("V5 result scores and rally total are inconsistent.");
            }

            var winnerIsHome = winnerTeamId.Equals(context.Home.TeamId);
            var winnerIsAway = winnerTeamId.Equals(context.Away.TeamId);
            if ((!winnerIsHome && !winnerIsAway) ||
                (winnerIsHome && homeScore <= awayScore) ||
                (winnerIsAway && awayScore <= homeScore))
            {
                throw new ContractValidationException("winnerTeamId must identify the higher-scoring V5 team.");
            }

            ContractVersion = ContractVersions.MatchV5;
            SessionId = context.SessionId;
            ContextHash = context.ContextHash;
            WinnerTeamId = new TeamId(ContractGuard.RequiredId(winnerTeamId.Value, nameof(winnerTeamId)));
            HomeScore = homeScore;
            AwayScore = awayScore;
            RalliesPlayed = ralliesPlayed;
            ResultHash = CanonicalMatchResultHashV5.Compute(this);
        }

        public int ContractVersion { get; }
        public Guid SessionId { get; }
        public string ContextHash { get; }
        public TeamId WinnerTeamId { get; }
        public int HomeScore { get; }
        public int AwayScore { get; }
        public int RalliesPlayed { get; }
        public string ResultHash { get; }

        public static MatchResultV5 Create(MatchContextV5 context, TeamId winnerTeamId,
            int homeScore, int awayScore, int ralliesPlayed)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new MatchResultV5(context, winnerTeamId, homeScore, awayScore, ralliesPlayed);
        }

        public void ValidateAgainst(MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            if (ContractVersion != ContractVersions.MatchV5 || SessionId != context.SessionId ||
                !string.Equals(ContextHash, context.ContextHash, StringComparison.Ordinal) ||
                !string.Equals(ResultHash, CanonicalMatchResultHashV5.Compute(this), StringComparison.Ordinal))
            {
                throw new ContractValidationException("The V5 result does not belong to its context.");
            }
        }
    }

    public static class CanonicalMatchResultHashV5
    {
        public static string Compute(MatchResultV5 result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return CanonicalJsonHashV4.Sha256("volleyball.match-result.v5\n" +
                CanonicalMatchResultJsonV5.ResultPayload(result));
        }
    }

    internal static class CanonicalMatchResultJsonV5
    {
        public static string Serialize(MatchResultV5 result)
        {
            var payload = ResultPayload(result);
            return payload.Substring(0, payload.Length - 1) + ",\"resultHash\":" +
                CanonicalMatchJsonV4.Quote(result.ResultHash) + "}";
        }

        public static string ResultPayload(MatchResultV5 result)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{{\"contractVersion\":{0},\"sessionId\":{1},\"contextHash\":{2},\"winnerTeamId\":{3},\"homeScore\":{4},\"awayScore\":{5},\"ralliesPlayed\":{6}}}",
                result.ContractVersion, CanonicalMatchJsonV4.Quote(result.SessionId.ToString("D")),
                CanonicalMatchJsonV4.Quote(result.ContextHash), CanonicalMatchJsonV4.Quote(result.WinnerTeamId.Value),
                result.HomeScore, result.AwayScore, result.RalliesPlayed);
        }
    }
}
