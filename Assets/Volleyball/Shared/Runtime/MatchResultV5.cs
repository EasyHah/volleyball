using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class MatchResultV5
    {
        private readonly MatchPositionFaultV5[] _positionFaults;

        private MatchResultV5(MatchContextV5 context, TeamId winnerTeamId, int homeScore,
            int awayScore, int ralliesPlayed, IReadOnlyList<MatchPositionFaultV5> positionFaults)
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
            PositionFaultEvidenceVersion = ContractVersions.PositionFaultEvidenceV5;
            SessionId = context.SessionId;
            ContextHash = context.ContextHash;
            WinnerTeamId = new TeamId(ContractGuard.RequiredId(winnerTeamId.Value, nameof(winnerTeamId)));
            HomeScore = homeScore;
            AwayScore = awayScore;
            RalliesPlayed = ralliesPlayed;
            _positionFaults = (positionFaults ?? throw new ContractValidationException(
                "V5 position-fault evidence is required.")).ToArray();
            for (var index = 0; index < _positionFaults.Length; index++)
            {
                var fault = _positionFaults[index] ?? throw new ContractValidationException(
                    "V5 position-fault evidence cannot contain null.");
                if (fault.RuleVersionValue != ContractVersions.PositionFaultEvidenceV5 ||
                    fault.RallyNumber > ralliesPlayed || !Contains(context, fault.RequiredPlayerId) ||
                    !Contains(context, fault.ViolatingPlayerId))
                    throw new ContractValidationException("V5 position-fault evidence does not bind its result.");
                if (index > 0 && Compare(_positionFaults[index - 1], fault) >= 0)
                    throw new ContractValidationException("V5 position-fault evidence must be in canonical order.");
            }
            ResultHash = CanonicalMatchResultHashV5.Compute(this);
        }

        public int ContractVersion { get; }
        public int PositionFaultEvidenceVersion { get; }
        public Guid SessionId { get; }
        public string ContextHash { get; }
        public TeamId WinnerTeamId { get; }
        public int HomeScore { get; }
        public int AwayScore { get; }
        public int RalliesPlayed { get; }
        public IReadOnlyList<MatchPositionFaultV5> PositionFaults =>
            new ReadOnlyCollection<MatchPositionFaultV5>(_positionFaults);
        public string ResultHash { get; }

        public static MatchResultV5 Create(MatchContextV5 context, TeamId winnerTeamId,
            int homeScore, int awayScore, int ralliesPlayed)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new MatchResultV5(context, winnerTeamId, homeScore, awayScore, ralliesPlayed,
                Array.Empty<MatchPositionFaultV5>());
        }

        public static MatchResultV5 Create(MatchContextV5 context, TeamId winnerTeamId,
            int homeScore, int awayScore, int ralliesPlayed,
            IReadOnlyList<MatchPositionFaultV5> positionFaults)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new MatchResultV5(context, winnerTeamId, homeScore, awayScore, ralliesPlayed,
                positionFaults);
        }

        private static bool Contains(MatchContextV5 context, PlayerId playerId)
        {
            return context.Home.RotationOrder.Concat(context.Away.RotationOrder)
                .Any(value => value.PlayerId.Equals(playerId));
        }

        private static int Compare(MatchPositionFaultV5 left, MatchPositionFaultV5 right)
        {
            var rally = left.RallyNumber.CompareTo(right.RallyNumber);
            return rally != 0 ? rally : string.CompareOrdinal(left.Rule, right.Rule);
        }

        public void ValidateAgainst(MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            if (ContractVersion != ContractVersions.MatchV5 ||
                PositionFaultEvidenceVersion != ContractVersions.PositionFaultEvidenceV5 ||
                SessionId != context.SessionId ||
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
                "{{\"contractVersion\":{0},\"positionFaultEvidenceVersion\":{1},\"sessionId\":{2},\"contextHash\":{3},\"winnerTeamId\":{4},\"homeScore\":{5},\"awayScore\":{6},\"ralliesPlayed\":{7},\"positionFaults\":{8}}}",
                result.ContractVersion, result.PositionFaultEvidenceVersion, CanonicalMatchJsonV4.Quote(result.SessionId.ToString("D")),
                CanonicalMatchJsonV4.Quote(result.ContextHash), CanonicalMatchJsonV4.Quote(result.WinnerTeamId.Value),
                result.HomeScore, result.AwayScore, result.RalliesPlayed,
                PositionFaults(result.PositionFaults));
        }

        private static string PositionFaults(IReadOnlyList<MatchPositionFaultV5> faults)
        {
            return "[" + string.Join(",", faults.Select(fault => string.Format(CultureInfo.InvariantCulture,
                "{{\"rallyNumber\":{0},\"ruleVersion\":{1},\"violatingSide\":{2},\"awardedSide\":{3},\"servingSide\":{4},\"rule\":{5},\"requiredPlayerId\":{6},\"requiredSlot\":{7},\"requiredXMillimeters\":{8},\"requiredZMillimeters\":{9},\"violatingPlayerId\":{10},\"violatingSlot\":{11},\"violatingXMillimeters\":{12},\"violatingZMillimeters\":{13}}}",
                fault.RallyNumber, fault.RuleVersionValue, (int)fault.ViolatingSide,
                (int)fault.AwardedSide, (int)fault.ServingSide, CanonicalMatchJsonV4.Quote(fault.Rule),
                CanonicalMatchJsonV4.Quote(fault.RequiredPlayerId.Value), fault.RequiredSlot,
                fault.RequiredXMillimeters, fault.RequiredZMillimeters,
                CanonicalMatchJsonV4.Quote(fault.ViolatingPlayerId.Value), fault.ViolatingSlot,
                fault.ViolatingXMillimeters, fault.ViolatingZMillimeters))) + "]";
        }
    }
}
