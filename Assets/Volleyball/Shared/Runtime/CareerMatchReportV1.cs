using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public enum CareerMatchEvidenceKindV1
    {
        PhysicalReplay = 0,
        QuickSimulationTrace = 1
    }

    /// <summary>Immutable, Match-owned facts that Career may settle but never rewrite.</summary>
    public sealed class CareerMatchPlayerReportV1
    {
        public CareerMatchPlayerReportV1(PlayerId playerId,
            int attackAttempts, int attackPoints, int attackErrors,
            int serveAttempts, int serveAces, int serveErrors,
            int receiveAttempts, int receivePerfect, int receivePositive, int receiveNeutral, int receiveNegative, int receiveErrors,
            int defenseAttempts, int defenseSuccesses, int blockAttempts, int blockEffectiveTouches, int blockPoints,
            int setAttempts, int setSuccesses, int setErrors, int rallies, int movementMillimeters, int jumps,
            int workloadBasisPoints, int workloadFormulaVersion, int criticalActions, int criticalSuccesses,
            int criticalErrors, int streakEpisodes, int longestStreak, int decisionQualitySuccesses, int decisionQualityErrors)
        {
            PlayerId = new PlayerId(ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            int[] values = { attackAttempts, attackPoints, attackErrors, serveAttempts, serveAces, serveErrors,
                receiveAttempts, receivePerfect, receivePositive, receiveNeutral, receiveNegative, receiveErrors,
                defenseAttempts, defenseSuccesses, blockAttempts, blockEffectiveTouches, blockPoints, setAttempts,
                setSuccesses, setErrors, rallies, movementMillimeters, jumps, workloadBasisPoints, criticalActions,
                criticalSuccesses, criticalErrors, streakEpisodes, longestStreak, decisionQualitySuccesses, decisionQualityErrors };
            foreach (var value in values) ContractGuard.NonNegative(value, nameof(value));
            if (attackPoints + attackErrors > attackAttempts || serveAces + serveErrors > serveAttempts ||
                receivePerfect + receivePositive + receiveNeutral + receiveNegative + receiveErrors != receiveAttempts ||
                defenseSuccesses > defenseAttempts || blockPoints > blockEffectiveTouches || blockEffectiveTouches > blockAttempts ||
                setSuccesses + setErrors > setAttempts || criticalSuccesses + criticalErrors > criticalActions ||
                workloadBasisPoints > 10000 || workloadFormulaVersion <= 0)
                throw new ContractValidationException("Career V5 player report invariants are invalid.");
            AttackAttempts = attackAttempts; AttackPoints = attackPoints; AttackErrors = attackErrors;
            ServeAttempts = serveAttempts; ServeAces = serveAces; ServeErrors = serveErrors;
            ReceiveAttempts = receiveAttempts; ReceivePerfect = receivePerfect; ReceivePositive = receivePositive; ReceiveNeutral = receiveNeutral; ReceiveNegative = receiveNegative; ReceiveErrors = receiveErrors;
            DefenseAttempts = defenseAttempts; DefenseSuccesses = defenseSuccesses; BlockAttempts = blockAttempts; BlockEffectiveTouches = blockEffectiveTouches; BlockPoints = blockPoints;
            SetAttempts = setAttempts; SetSuccesses = setSuccesses; SetErrors = setErrors; Rallies = rallies; MovementMillimeters = movementMillimeters; Jumps = jumps;
            WorkloadBasisPoints = workloadBasisPoints; WorkloadFormulaVersion = workloadFormulaVersion; CriticalActions = criticalActions; CriticalSuccesses = criticalSuccesses; CriticalErrors = criticalErrors; StreakEpisodes = streakEpisodes; LongestStreak = longestStreak; DecisionQualitySuccesses = decisionQualitySuccesses; DecisionQualityErrors = decisionQualityErrors;
        }
        public PlayerId PlayerId { get; }
        public int AttackAttempts { get; } public int AttackPoints { get; } public int AttackErrors { get; }
        public int ServeAttempts { get; } public int ServeAces { get; } public int ServeErrors { get; }
        public int ReceiveAttempts { get; } public int ReceivePerfect { get; } public int ReceivePositive { get; } public int ReceiveNeutral { get; } public int ReceiveNegative { get; } public int ReceiveErrors { get; }
        public int DefenseAttempts { get; } public int DefenseSuccesses { get; } public int BlockAttempts { get; } public int BlockEffectiveTouches { get; } public int BlockPoints { get; }
        public int SetAttempts { get; } public int SetSuccesses { get; } public int SetErrors { get; } public int Rallies { get; } public int MovementMillimeters { get; } public int Jumps { get; } public int WorkloadBasisPoints { get; } public int WorkloadFormulaVersion { get; }
        public int CriticalActions { get; } public int CriticalSuccesses { get; } public int CriticalErrors { get; } public int StreakEpisodes { get; } public int LongestStreak { get; } public int DecisionQualitySuccesses { get; } public int DecisionQualityErrors { get; }
    }

    public sealed class CareerMatchReportV1
    {
        private readonly CareerMatchPlayerReportV1[] _playerReports;
        private CareerMatchReportV1(MatchContextV5 context, MatchResultV5 result, CareerMatchEvidenceKindV1 evidenceKind, string evidenceHash, IReadOnlyList<CareerMatchPlayerReportV1> reports)
        {
            context.Validate(); result.ValidateAgainst(context); ContractGuard.DefinedEnum(evidenceKind, nameof(evidenceKind)); ContractGuard.Hash(evidenceHash, nameof(evidenceHash));
            if (reports == null || reports.Count != 12) throw new ContractValidationException("A V5 report requires twelve player reports.");
            _playerReports = new CareerMatchPlayerReportV1[12];
            var expected = OrderedPlayers(context);
            for (var index = 0; index < 12; index++)
            {
                var report = reports[index] ?? throw new ContractValidationException("Player report cannot be null.");
                if (!report.PlayerId.Equals(expected[index])) throw new ContractValidationException("Player reports must be in frozen context order.");
                _playerReports[index] = report;
            }
            ReportVersion = ContractVersions.CareerMatchReportV1; SessionId = context.SessionId; ContextHash = context.ContextHash; ResultHash = result.ResultHash; EvidenceKind = evidenceKind; EvidenceHash = evidenceHash;
            ReportHash = CanonicalCareerMatchReportHashV1.Compute(this);
        }
        public int ReportVersion { get; } public Guid SessionId { get; } public string ContextHash { get; } public string ResultHash { get; }
        public CareerMatchEvidenceKindV1 EvidenceKind { get; } public string EvidenceHash { get; } public IReadOnlyList<CareerMatchPlayerReportV1> PlayerReports => new ReadOnlyCollection<CareerMatchPlayerReportV1>(_playerReports); public string ReportHash { get; }
        public static CareerMatchReportV1 Create(MatchContextV5 context, MatchResultV5 result, CareerMatchEvidenceKindV1 evidenceKind, string evidenceHash, IReadOnlyList<CareerMatchPlayerReportV1> reports) => new CareerMatchReportV1(context, result, evidenceKind, evidenceHash, reports);
        public void ValidateAgainst(MatchContextV5 context, MatchResultV5 result)
        {
            if (context == null || result == null) throw new ArgumentNullException(context == null ? nameof(context) : nameof(result));
            result.ValidateAgainst(context);
            if (ReportVersion != ContractVersions.CareerMatchReportV1 || SessionId != context.SessionId || ContextHash != context.ContextHash || ResultHash != result.ResultHash || ReportHash != CanonicalCareerMatchReportHashV1.Compute(this)) throw new ContractValidationException("V5 report binding is invalid.");
        }

        internal static PlayerId[] OrderedPlayersForValidation(MatchContextV5 context)
        {
            return OrderedPlayers(context);
        }
        private static PlayerId[] OrderedPlayers(MatchContextV5 context)
        {
            var result = new PlayerId[12]; var index = 0;
            foreach (var player in context.Home.RotationOrder) result[index++] = player.PlayerId;
            foreach (var player in context.Away.RotationOrder) result[index++] = player.PlayerId;
            return result;
        }
    }

    public static class CanonicalCareerMatchReportHashV1
    {
        public static string Compute(CareerMatchReportV1 report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var text = new StringBuilder("volleyball.career-match-report.v1\n");
            text.Append(report.ReportVersion).Append('|').Append(report.SessionId.ToString("D")).Append('|').Append(report.ContextHash).Append('|').Append(report.ResultHash).Append('|').Append((int)report.EvidenceKind).Append('|').Append(report.EvidenceHash);
            foreach (var p in report.PlayerReports) text.Append('|').Append(p.PlayerId.Value).Append(':').Append(p.AttackAttempts).Append(':').Append(p.AttackPoints).Append(':').Append(p.AttackErrors).Append(':').Append(p.ServeAttempts).Append(':').Append(p.ServeAces).Append(':').Append(p.ServeErrors).Append(':').Append(p.ReceiveAttempts).Append(':').Append(p.ReceivePerfect).Append(':').Append(p.ReceivePositive).Append(':').Append(p.ReceiveNeutral).Append(':').Append(p.ReceiveNegative).Append(':').Append(p.ReceiveErrors).Append(':').Append(p.DefenseAttempts).Append(':').Append(p.DefenseSuccesses).Append(':').Append(p.BlockAttempts).Append(':').Append(p.BlockEffectiveTouches).Append(':').Append(p.BlockPoints).Append(':').Append(p.SetAttempts).Append(':').Append(p.SetSuccesses).Append(':').Append(p.SetErrors).Append(':').Append(p.Rallies).Append(':').Append(p.MovementMillimeters).Append(':').Append(p.Jumps).Append(':').Append(p.WorkloadBasisPoints).Append(':').Append(p.WorkloadFormulaVersion).Append(':').Append(p.CriticalActions).Append(':').Append(p.CriticalSuccesses).Append(':').Append(p.CriticalErrors).Append(':').Append(p.StreakEpisodes).Append(':').Append(p.LongestStreak).Append(':').Append(p.DecisionQualitySuccesses).Append(':').Append(p.DecisionQualityErrors);
            return CanonicalJsonHashV4.Sha256(text.ToString());
        }
    }

    internal static class CanonicalCareerMatchReportJsonV1
    {
        public static string Serialize(CareerMatchReportV1 report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var output = new StringBuilder(4096);
            output.Append("{\"reportVersion\":").Append(report.ReportVersion);
            output.Append(",\"sessionId\":").Append(CanonicalMatchJsonV4.Quote(report.SessionId.ToString("D")));
            output.Append(",\"contextHash\":").Append(CanonicalMatchJsonV4.Quote(report.ContextHash));
            output.Append(",\"resultHash\":").Append(CanonicalMatchJsonV4.Quote(report.ResultHash));
            output.Append(",\"evidenceKind\":").Append((int)report.EvidenceKind);
            output.Append(",\"evidenceHash\":").Append(CanonicalMatchJsonV4.Quote(report.EvidenceHash));
            output.Append(",\"playerReports\":[");
            for (var index = 0; index < report.PlayerReports.Count; index++)
            {
                if (index > 0) output.Append(',');
                AppendPlayer(output, report.PlayerReports[index]);
            }
            output.Append("]");
            output.Append(",\"reportHash\":").Append(CanonicalMatchJsonV4.Quote(report.ReportHash));
            output.Append('}');
            return output.ToString();
        }

        private static void AppendPlayer(StringBuilder output, CareerMatchPlayerReportV1 value)
        {
            output.Append("{\"playerId\":").Append(CanonicalMatchJsonV4.Quote(value.PlayerId.Value));
            Append(output, "attackAttempts", value.AttackAttempts); Append(output, "attackPoints", value.AttackPoints); Append(output, "attackErrors", value.AttackErrors);
            Append(output, "serveAttempts", value.ServeAttempts); Append(output, "serveAces", value.ServeAces); Append(output, "serveErrors", value.ServeErrors);
            Append(output, "receiveAttempts", value.ReceiveAttempts); Append(output, "receivePerfect", value.ReceivePerfect); Append(output, "receivePositive", value.ReceivePositive); Append(output, "receiveNeutral", value.ReceiveNeutral); Append(output, "receiveNegative", value.ReceiveNegative); Append(output, "receiveErrors", value.ReceiveErrors);
            Append(output, "defenseAttempts", value.DefenseAttempts); Append(output, "defenseSuccesses", value.DefenseSuccesses);
            Append(output, "blockAttempts", value.BlockAttempts); Append(output, "blockEffectiveTouches", value.BlockEffectiveTouches); Append(output, "blockPoints", value.BlockPoints);
            Append(output, "setAttempts", value.SetAttempts); Append(output, "setSuccesses", value.SetSuccesses); Append(output, "setErrors", value.SetErrors);
            Append(output, "rallies", value.Rallies); Append(output, "movementMillimeters", value.MovementMillimeters); Append(output, "jumps", value.Jumps);
            Append(output, "workloadBasisPoints", value.WorkloadBasisPoints); Append(output, "workloadFormulaVersion", value.WorkloadFormulaVersion);
            Append(output, "criticalActions", value.CriticalActions); Append(output, "criticalSuccesses", value.CriticalSuccesses); Append(output, "criticalErrors", value.CriticalErrors);
            Append(output, "streakEpisodes", value.StreakEpisodes); Append(output, "longestStreak", value.LongestStreak);
            Append(output, "decisionQualitySuccesses", value.DecisionQualitySuccesses); Append(output, "decisionQualityErrors", value.DecisionQualityErrors);
            output.Append('}');
        }

        private static void Append(StringBuilder output, string name, int value)
        {
            output.Append(",\"").Append(name).Append("\":").Append(value);
        }
    }
}
