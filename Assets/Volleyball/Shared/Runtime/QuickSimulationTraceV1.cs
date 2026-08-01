using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class QuickSimulationTraceEntryV1
    {
        public QuickSimulationTraceEntryV1(int sequence, PlayerId playerId, string action,
            string classification, bool critical, int workloadBasisPoints,
            int executableChoices, string selectedChoice, string decisionReason,
            TeamSide rallyWinner = TeamSide.Home)
        {
            if (sequence < 0 || workloadBasisPoints < 0 || workloadBasisPoints > 10000 ||
                executableChoices < 0)
                throw new ContractValidationException("Quick trace entry bounds are invalid.");
            if (executableChoices < 2 && (!string.IsNullOrEmpty(selectedChoice) || !string.IsNullOrEmpty(decisionReason)))
                throw new ContractValidationException("Quick trace decision evidence requires two executable choices.");
            if (action != "Serve" && action != "Receive" && action != "Set" && action != "Attack" && action != "Block")
                throw new ContractValidationException("Quick trace action is unsupported.");
            if (classification != "Success" && classification != "Error" && classification != "Perfect" &&
                classification != "Positive" && classification != "Neutral" && classification != "Negative")
                throw new ContractValidationException("Quick trace classification is unsupported.");
            if (action != "Receive" && classification != "Success" && classification != "Error")
                throw new ContractValidationException("Quick trace classification is invalid for its action.");
            if (rallyWinner != TeamSide.Home && rallyWinner != TeamSide.Away)
                throw new ContractValidationException("Quick trace rally winner is invalid.");
            Sequence = sequence;
            PlayerId = new PlayerId(ContractGuard.RequiredId(playerId.Value, nameof(playerId)));
            Action = ContractGuard.RequiredText(action, nameof(action), 32);
            Classification = ContractGuard.RequiredText(classification, nameof(classification), 32);
            Critical = critical;
            WorkloadBasisPoints = workloadBasisPoints;
            ExecutableChoices = executableChoices;
            SelectedChoice = executableChoices >= 2
                ? ContractGuard.RequiredText(selectedChoice, nameof(selectedChoice), 32) : string.Empty;
            DecisionReason = executableChoices >= 2
                ? ContractGuard.RequiredText(decisionReason, nameof(decisionReason), 64) : string.Empty;
            RallyWinner = rallyWinner;
        }
        public int Sequence { get; }
        public PlayerId PlayerId { get; }
        public string Action { get; }
        public string Classification { get; }
        public bool Critical { get; }
        public int WorkloadBasisPoints { get; }
        public int ExecutableChoices { get; }
        public string SelectedChoice { get; }
        public string DecisionReason { get; }
        public TeamSide RallyWinner { get; }
    }

    /// <summary>Canonical minimal evidence emitted by deterministic V5 quick simulation.</summary>
    public sealed class QuickSimulationTraceV1
    {
        private readonly QuickSimulationTraceEntryV1[] _entries;
        private QuickSimulationTraceV1(MatchContextV5 context, IReadOnlyList<QuickSimulationTraceEntryV1> entries)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            _entries = new QuickSimulationTraceEntryV1[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index] ?? throw new ContractValidationException("Quick trace entry cannot be null.");
                if (entry.Sequence != index || !Contains(context, entry.PlayerId))
                    throw new ContractValidationException("Quick trace entries must be ordered context players.");
                _entries[index] = entry;
            }
            TraceVersion = ContractVersions.QuickSimulationTraceV1;
            SessionId = context.SessionId;
            ContextHash = context.ContextHash;
            TraceHash = ComputeHash(this);
        }
        public int TraceVersion { get; }
        public Guid SessionId { get; }
        public string ContextHash { get; }
        public IReadOnlyList<QuickSimulationTraceEntryV1> Entries => new ReadOnlyCollection<QuickSimulationTraceEntryV1>(_entries);
        public string TraceHash { get; }
        public static QuickSimulationTraceV1 Create(MatchContextV5 context, IReadOnlyList<QuickSimulationTraceEntryV1> entries) => new QuickSimulationTraceV1(context, entries);
        public void ValidateAgainst(MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            if (TraceVersion != ContractVersions.QuickSimulationTraceV1 || SessionId != context.SessionId ||
                !string.Equals(ContextHash, context.ContextHash, StringComparison.Ordinal) ||
                !string.Equals(TraceHash, ComputeHash(this), StringComparison.Ordinal))
                throw new ContractValidationException("Quick trace binding is invalid.");
        }
        private static bool Contains(MatchContextV5 context, PlayerId playerId)
        {
            foreach (var player in context.Home.RotationOrder) if (player.PlayerId.Equals(playerId)) return true;
            foreach (var player in context.Away.RotationOrder) if (player.PlayerId.Equals(playerId)) return true;
            return false;
        }
        private static string ComputeHash(QuickSimulationTraceV1 trace)
        {
            var output = new StringBuilder("volleyball.quick-simulation-trace.v1\n");
            output.Append(trace.TraceVersion).Append('|').Append(trace.SessionId.ToString("D")).Append('|').Append(trace.ContextHash);
            foreach (var entry in trace.Entries) output.Append('|').Append(entry.Sequence).Append(':').Append(entry.PlayerId.Value).Append(':').Append(entry.Action).Append(':').Append(entry.Classification).Append(':').Append(entry.Critical ? 1 : 0).Append(':').Append(entry.WorkloadBasisPoints).Append(':').Append(entry.ExecutableChoices).Append(':').Append(entry.SelectedChoice).Append(':').Append(entry.DecisionReason).Append(':').Append((int)entry.RallyWinner);
            return CanonicalJsonHashV4.Sha256(output.ToString());
        }
    }

    internal static class CanonicalQuickSimulationTraceJsonV1
    {
        public static string Serialize(QuickSimulationTraceV1 trace)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            var output = new StringBuilder("{\"traceVersion\":");
            output.Append(trace.TraceVersion).Append(",\"sessionId\":")
                .Append(CanonicalMatchJsonV4.Quote(trace.SessionId.ToString("D")))
                .Append(",\"contextHash\":").Append(CanonicalMatchJsonV4.Quote(trace.ContextHash))
                .Append(",\"entries\":[");
            for (var index = 0; index < trace.Entries.Count; index++)
            {
                if (index > 0) output.Append(',');
                var entry = trace.Entries[index];
                output.Append("{\"sequence\":").Append(entry.Sequence)
                    .Append(",\"playerId\":").Append(CanonicalMatchJsonV4.Quote(entry.PlayerId.Value))
                    .Append(",\"action\":").Append(CanonicalMatchJsonV4.Quote(entry.Action))
                    .Append(",\"classification\":").Append(CanonicalMatchJsonV4.Quote(entry.Classification))
                    .Append(",\"critical\":").Append(entry.Critical ? "true" : "false")
                    .Append(",\"workloadBasisPoints\":").Append(entry.WorkloadBasisPoints)
                    .Append(",\"executableChoices\":").Append(entry.ExecutableChoices)
                    .Append(",\"selectedChoice\":").Append(CanonicalMatchJsonV4.Quote(entry.SelectedChoice))
                    .Append(",\"decisionReason\":").Append(CanonicalMatchJsonV4.Quote(entry.DecisionReason))
                    .Append(",\"rallyWinner\":").Append((int)entry.RallyWinner).Append('}');
            }
            return output.Append("],\"traceHash\":").Append(CanonicalMatchJsonV4.Quote(trace.TraceHash)).Append('}').ToString();
        }
    }
}
