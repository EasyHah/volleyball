using System;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    /// <summary>Career-owned consequences calculated only from a verified V5 report.</summary>
    public sealed class CareerV5MatchSettlement
    {
        public CareerV5MatchSettlement(CareerPlayerProfileV5 profile, int fatigueDelta,
            int mindsetDelta, int coachTrustDelta, string reportHash)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            FatigueDelta = fatigueDelta;
            MindsetDelta = mindsetDelta;
            CoachTrustDelta = coachTrustDelta;
            if (string.IsNullOrWhiteSpace(reportHash) || reportHash.Length != 64)
                throw new ArgumentException("A SHA-256 report hash is required.", nameof(reportHash));
            ReportHash = reportHash;
        }

        public CareerPlayerProfileV5 Profile { get; }
        public int FatigueDelta { get; }
        public int MindsetDelta { get; }
        public int CoachTrustDelta { get; }
        public string ReportHash { get; }
    }

    public static class CareerV5MatchSettlementRules
    {
        public static CareerV5MatchSettlement Apply(CareerPlayerProfileV5 profile,
            MatchContextV5 context, MatchResultV5 result, CareerMatchReportV1 report,
            MatchReplayV5 replay, QuickSimulationTraceV1 quickTrace)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (report == null) throw new ArgumentNullException(nameof(report));
            report.ValidateAgainst(context, result);
            if (report.EvidenceKind != CareerMatchEvidenceKindV1.PhysicalReplay &&
                report.EvidenceKind != CareerMatchEvidenceKindV1.QuickSimulationTrace)
                throw new ContractValidationException("Career V5 settlement requires recognized evidence.");
            if (report.EvidenceKind == CareerMatchEvidenceKindV1.PhysicalReplay)
            {
                if (replay == null || quickTrace != null ||
                    !string.Equals(replay.ContextHash, context.ContextHash, StringComparison.Ordinal) ||
                    !string.Equals(report.EvidenceHash, replay.ReplayHash, StringComparison.Ordinal))
                    throw new ContractValidationException("Career V5 settlement replay does not bind the report.");
            }
            else
            {
                if (quickTrace == null || replay != null)
                    throw new ContractValidationException("Career V5 settlement quick trace does not bind the report.");
                quickTrace.ValidateAgainst(context);
                if (!string.Equals(report.EvidenceHash, quickTrace.TraceHash, StringComparison.Ordinal))
                    throw new ContractValidationException("Career V5 settlement quick trace does not bind the report.");
                var rebuiltResult = DeterministicQuickSimulationRunnerV5.RebuildResult(context, quickTrace);
                var rebuiltReport = DeterministicQuickSimulationRunnerV5.RebuildReport(context, rebuiltResult, quickTrace);
                if (!string.Equals(rebuiltResult.ResultHash, result.ResultHash, StringComparison.Ordinal) ||
                    !string.Equals(rebuiltReport.ReportHash, report.ReportHash, StringComparison.Ordinal))
                    throw new ContractValidationException("Career V5 settlement quick trace cannot rebuild its result and report.");
            }
            CareerMatchPlayerReportV1 player = null;
            foreach (var candidate in report.PlayerReports)
                if (candidate.PlayerId.Equals(profile.PlayerId)) { player = candidate; break; }
            if (player == null) throw new ContractValidationException("Career V5 profile is absent from the report.");

            // Values are deliberately small, bounded, and derived solely from Match facts.
            var growth = Math.Min(4, (player.AttackPoints + player.ServeAces + player.BlockPoints +
                player.DefenseSuccesses + player.SetSuccesses) / 3);
            var bases = profile.Bases;
            var next = new CareerBaseAttributesV5(
                Grow(bases.Strength, growth), bases.HeightMillimeters, Grow(bases.Jump, player.Jumps > 0 ? 1 : 0),
                Grow(bases.Movement, player.DefenseAttempts > 0 ? 1 : 0), Grow(bases.Reaction, player.DefenseSuccesses > 0 ? 1 : 0),
                Grow(bases.Coordination, player.SetSuccesses > 0 ? 1 : 0), Grow(bases.Attack, growth),
                Grow(bases.Defense, player.DefenseSuccesses > 0 ? 1 : 0), Grow(bases.CourtIq, player.DecisionQualitySuccesses),
                Grow(bases.Block, player.BlockEffectiveTouches > 0 ? 1 : 0), Grow(bases.Serve, player.ServeAces),
                Grow(bases.Set, player.SetSuccesses > 0 ? 1 : 0));
            var fatigue = Math.Min(100, player.WorkloadBasisPoints / 100);
            var mindset = Clamp(player.CriticalSuccesses - player.CriticalErrors - player.AttackErrors - player.ServeErrors, -10, 10);
            var trust = Clamp((result.WinnerTeamId.Equals(context.Home.TeamId) == IsHome(context, profile.PlayerId) ? 2 : -1) +
                player.DecisionQualitySuccesses - player.DecisionQualityErrors, -10, 10);
            return new CareerV5MatchSettlement(new CareerPlayerProfileV5(profile.PlayerId, profile.DisplayName,
                profile.JerseyNumber, profile.DominantHand, next,
                Clamp(profile.Fatigue + fatigue, 0, 100), Clamp(profile.Mindset + mindset, 0, 100),
                Clamp(profile.CoachTrust + trust, 0, 100)), fatigue, mindset, trust, report.ReportHash);
        }

        private static bool IsHome(MatchContextV5 context, PlayerId playerId)
        {
            foreach (var player in context.Home.RotationOrder) if (player.PlayerId.Equals(playerId)) return true;
            return false;
        }
        private static int Grow(int value, int amount) => Math.Min(CareerBaseAttributesV5.MaximumBasisPoints, value + Math.Max(0, amount));
        private static int Clamp(int value, int minimum, int maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }
}
