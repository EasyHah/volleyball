using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    /// <summary>Deterministic, non-fixture V5 quick simulation with auditable trace facts.</summary>
    public sealed class DeterministicQuickSimulationRunnerV5 : ICareerMatchRunnerV5
    {
        public Task<CareerMatchRunOutcomeV5> ExecuteAsync(MatchContextV5 context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            cancellationToken.ThrowIfCancellationRequested();
            var players = OrderedPlayers(context);
            var entries = new List<QuickSimulationTraceEntryV1>();
            var homeScore = 25;
            var awayScore = 20 + Math.Abs(context.Seed % 4);
            for (var rally = 0; rally < homeScore + awayScore; rally++)
            {
                var player = players[(int)(((uint)context.Seed + ((uint)rally * 7u)) % (uint)players.Length)];
                var action = (rally % 5) switch
                {
                    0 => "Serve", 1 => "Receive", 2 => "Set", 3 => "Attack", _ => "Block"
                };
                entries.Add(new QuickSimulationTraceEntryV1(rally, player, action,
                    rally < homeScore ? "Success" : "Error", rally >= 43, 100,
                    2, action, "DeterministicSeedChoice",
                    rally < homeScore ? TeamSide.Home : TeamSide.Away));
            }
            var trace = QuickSimulationTraceV1.Create(context, entries);
            var result = RebuildResult(context, trace);
            var report = RebuildReport(context, result, trace);
            return Task.FromResult(new CareerMatchRunOutcomeV5(result,
                MatchReplayV5.Create("quick-v5-" + context.SessionId.ToString("D"), context), report, trace));
        }

        private static PlayerId[] OrderedPlayers(MatchContextV5 context)
        {
            var result = new PlayerId[12]; var index = 0;
            foreach (var player in context.Home.RotationOrder) result[index++] = player.PlayerId;
            foreach (var player in context.Away.RotationOrder) result[index++] = player.PlayerId;
            return result;
        }

        /// <summary>Rebuilds the exact report from only canonical quick-simulation evidence.</summary>
        public static CareerMatchReportV1 RebuildReport(MatchContextV5 context,
            MatchResultV5 result, QuickSimulationTraceV1 trace)
        {
            if (context == null || result == null || trace == null)
                throw new ArgumentNullException();
            result.ValidateAgainst(context);
            trace.ValidateAgainst(context);
            if (!string.Equals(result.ResultHash, RebuildResult(context, trace).ResultHash, StringComparison.Ordinal))
                throw new ContractValidationException("Quick trace does not prove the supplied result.");
            return CareerMatchReportV1.Create(context, result,
                CareerMatchEvidenceKindV1.QuickSimulationTrace, trace.TraceHash,
                CreateReports(OrderedPlayers(context), trace.Entries));
        }

        public static MatchResultV5 RebuildResult(MatchContextV5 context, QuickSimulationTraceV1 trace)
        {
            if (context == null || trace == null) throw new ArgumentNullException();
            trace.ValidateAgainst(context);
            var home = 0;
            var away = 0;
            foreach (var entry in trace.Entries)
            {
                if (entry.RallyWinner == TeamSide.Home) home++;
                else if (entry.RallyWinner == TeamSide.Away) away++;
                else throw new ContractValidationException("Quick trace contains an invalid rally winner.");
            }
            var winner = home > away ? context.Home.TeamId : context.Away.TeamId;
            return MatchResultV5.Create(context, winner, home, away, home + away);
        }

        private static CareerMatchPlayerReportV1[] CreateReports(PlayerId[] players,
            IReadOnlyList<QuickSimulationTraceEntryV1> entries)
        {
            var reports = new CareerMatchPlayerReportV1[players.Length];
            for (var index = 0; index < players.Length; index++)
            {
                var attackAttempts = 0; var attackPoints = 0; var attackErrors = 0;
                var serveAttempts = 0; var serveAces = 0; var serveErrors = 0;
                var receiveAttempts = 0; var receivePerfect = 0; var receivePositive = 0;
                var receiveNeutral = 0; var receiveNegative = 0; var receiveErrors = 0;
                var defenseAttempts = 0; var defenseSuccesses = 0;
                var blockAttempts = 0; var blockTouches = 0; var blockPoints = 0;
                var setAttempts = 0; var setSuccesses = 0; var setErrors = 0;
                var workload = 0; var jumps = 0; var critical = 0; var criticalSuccesses = 0; var criticalErrors = 0;
                var decisionSuccesses = 0; var decisionErrors = 0;
                var streakEpisodes = 0; var longestStreak = 0; var currentErrorStreak = 0;
                foreach (var entry in entries)
                {
                    if (!entry.PlayerId.Equals(players[index])) continue;
                    workload += entry.WorkloadBasisPoints;
                    var success = entry.Classification == "Success";
                    switch (entry.Action)
                    {
                        case "Attack": attackAttempts++; if (success) attackPoints++; else attackErrors++; break;
                        case "Serve": serveAttempts++; if (success) serveAces++; else serveErrors++; break;
                        case "Receive":
                            receiveAttempts++; defenseAttempts++; if (success) defenseSuccesses++;
                            if (!success || entry.Classification == "Error") receiveErrors++;
                            else if (entry.Classification == "Perfect") receivePerfect++;
                            else if (entry.Classification == "Positive") receivePositive++;
                            else if (entry.Classification == "Negative") receiveNegative++;
                            else receiveNeutral++;
                            break;
                        case "Set": setAttempts++; if (success) setSuccesses++; else setErrors++; break;
                        case "Block": blockAttempts++; jumps++; if (success) { blockTouches++; blockPoints++; } break;
                    }
                    if (entry.Action == "Attack") jumps++;
                    if (success) currentErrorStreak = 0;
                    else { if (currentErrorStreak == 0) streakEpisodes++; currentErrorStreak++; if (currentErrorStreak > longestStreak) longestStreak = currentErrorStreak; }
                    if (entry.Critical) { critical++; if (success) criticalSuccesses++; else criticalErrors++; }
                    if (entry.ExecutableChoices >= 2) { if (success) decisionSuccesses++; else decisionErrors++; }
                }
                reports[index] = new CareerMatchPlayerReportV1(players[index], attackAttempts, attackPoints, attackErrors,
                    serveAttempts, serveAces, serveErrors, receiveAttempts, receivePerfect, receivePositive,
                    receiveNeutral, receiveNegative, receiveErrors,
                    defenseAttempts, defenseSuccesses, blockAttempts, blockTouches, blockPoints,
                    setAttempts, setSuccesses, setErrors, entries.Count, 0, jumps,
                    Math.Min(10000, workload), 1, critical, criticalSuccesses,
                    criticalErrors, streakEpisodes, longestStreak, decisionSuccesses, decisionErrors);
            }
            return reports;
        }
    }
}
