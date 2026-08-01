using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    public interface ICareerMatchRunnerV5
    {
        Task<CareerMatchRunOutcomeV5> ExecuteAsync(MatchContextV5 context,
            CancellationToken cancellationToken);
    }

    public sealed class CareerMatchRunOutcomeV5
    {
        public CareerMatchRunOutcomeV5(MatchResultV5 result, MatchReplayV5 replay,
            CareerMatchReportV1 report = null, QuickSimulationTraceV1 quickTrace = null)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Replay = replay ?? throw new ArgumentNullException(nameof(replay));
            Report = report;
            QuickTrace = quickTrace;
            if (result.SessionId != replay.Context.SessionId ||
                !string.Equals(result.ContextHash, replay.ContextHash, StringComparison.Ordinal))
                throw new ArgumentException("V5 result and replay must bind the same context.");
            if (report != null)
            {
                report.ValidateAgainst(replay.Context, result);
                if (report.EvidenceKind == CareerMatchEvidenceKindV1.PhysicalReplay &&
                    !string.Equals(report.EvidenceHash, replay.ReplayHash, StringComparison.Ordinal))
                    throw new ArgumentException("V5 report must bind the supplied physical replay.");
            }
            if (quickTrace != null)
            {
                quickTrace.ValidateAgainst(replay.Context);
                if (report == null || report.EvidenceKind != CareerMatchEvidenceKindV1.QuickSimulationTrace ||
                    !string.Equals(report.EvidenceHash, quickTrace.TraceHash, StringComparison.Ordinal))
                    throw new ArgumentException("V5 quick report must bind the supplied quick trace.");
            }
        }

        public MatchResultV5 Result { get; }
        public MatchReplayV5 Replay { get; }
        public CareerMatchReportV1 Report { get; }
        public QuickSimulationTraceV1 QuickTrace { get; }
    }

    /// <summary>
    /// Native V5 lifecycle boundary. V5-A returns Match artifacts only: it
    /// deliberately has no V4 fact conversion or Career settlement output.
    /// </summary>
    public sealed class CareerMatchExecutorV5
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly CareerMatchV5Mapper _mapper;
        private readonly ICareerMatchRunnerV5 _runner;

        public CareerMatchExecutorV5(CareerMatchV5Mapper mapper, ICareerMatchRunnerV5 runner)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public async Task<CareerMatchRunOutcomeV5> ExecuteAsync(CareerMatchLaunchV5 launch,
            CancellationToken cancellationToken)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            return await ExecuteContextAsync(_mapper.ToContext(launch), cancellationToken);
        }

        public async Task<CareerMatchRunOutcomeV5> ExecuteContextAsync(MatchContextV5 context,
            CancellationToken cancellationToken)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var bytes = StrictUtf8.GetBytes(ContractJson.SerializeV5(context));
            var decoded = ContractJson.DeserializeMatchContextV5(StrictUtf8.GetString(bytes));
            RequireExact(bytes, StrictUtf8.GetBytes(ContractJson.SerializeV5(decoded)), "context");
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await _runner.ExecuteAsync(decoded, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (outcome == null) throw new InvalidOperationException("The V5 match runner returned no outcome.");
            outcome.Result.ValidateAgainst(decoded);
            if (!string.Equals(outcome.Replay.ContextHash, decoded.ContextHash, StringComparison.Ordinal))
                throw new ContractValidationException("V5 replay does not bind the canonical context.");
            if (outcome.Report != null) outcome.Report.ValidateAgainst(decoded, outcome.Result);
            if (outcome.QuickTrace != null) outcome.QuickTrace.ValidateAgainst(decoded);
            return outcome;
        }

        private static void RequireExact(byte[] actual, byte[] expected, string name)
        {
            if (actual.Length != expected.Length) throw new ContractValidationException("V5 " + name + " bytes are not canonical.");
            for (var index = 0; index < actual.Length; index++)
                if (actual[index] != expected[index]) throw new ContractValidationException("V5 " + name + " bytes are not canonical.");
        }
    }
}
