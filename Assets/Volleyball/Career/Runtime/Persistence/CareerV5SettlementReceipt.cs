using System;
using System.Text;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Persistence
{
    /// <summary>Single durable commit point for a V5 profile consequence and its audited evidence.</summary>
    internal sealed class CareerV5SettlementReceipt
    {
        public CareerV5SettlementReceipt(Guid sessionId, string reportHash, byte[] profileUtf8,
            byte[] contextUtf8, byte[] resultUtf8, byte[] reportUtf8, byte[] traceUtf8)
        {
            if (sessionId == Guid.Empty) throw new ArgumentException("Session is required.", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(reportHash) || reportHash.Length != 64)
                throw new ArgumentException("Report hash is required.", nameof(reportHash));
            SessionId = sessionId; ReportHash = reportHash;
            ProfileUtf8 = profileUtf8 ?? throw new ArgumentNullException(nameof(profileUtf8));
            ContextUtf8 = contextUtf8 ?? throw new ArgumentNullException(nameof(contextUtf8));
            ResultUtf8 = resultUtf8 ?? throw new ArgumentNullException(nameof(resultUtf8));
            ReportUtf8 = reportUtf8 ?? throw new ArgumentNullException(nameof(reportUtf8));
            TraceUtf8 = traceUtf8 ?? Array.Empty<byte>();
        }
        public Guid SessionId { get; }
        public string ReportHash { get; }
        public byte[] ProfileUtf8 { get; }
        public byte[] ContextUtf8 { get; }
        public byte[] ResultUtf8 { get; }
        public byte[] ReportUtf8 { get; }
        public byte[] TraceUtf8 { get; }
    }

    internal static class CareerV5SettlementReceiptCodec
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static byte[] Serialize(CareerV5SettlementReceipt value)
        {
            var json = "{\"schemaVersion\":1,\"sessionId\":\"" + value.SessionId.ToString("D") +
                "\",\"reportHash\":\"" + value.ReportHash + "\",\"profileUtf8\":\"" +
                Convert.ToBase64String(value.ProfileUtf8) + "\",\"contextUtf8\":\"" +
                Convert.ToBase64String(value.ContextUtf8) + "\",\"resultUtf8\":\"" +
                Convert.ToBase64String(value.ResultUtf8) + "\",\"reportUtf8\":\"" +
                Convert.ToBase64String(value.ReportUtf8) + "\",\"traceUtf8\":\"" +
                Convert.ToBase64String(value.TraceUtf8) + "\"}";
            return Utf8.GetBytes(json);
        }
        public static CareerV5SettlementReceipt Deserialize(byte[] bytes)
        {
            var root = StrictJsonReader.Parse(bytes);
            if (root.Kind != StrictJsonKind.Object) throw new FormatException("V5 settlement receipt must be an object.");
            var objectValue = root.ObjectValue;
            objectValue.RequireExactly("V5 settlement receipt", "schemaVersion", "sessionId", "reportHash",
                "profileUtf8", "contextUtf8", "resultUtf8", "reportUtf8", "traceUtf8");
            var schema = RequireInt(objectValue, "schemaVersion");
            if (schema != 1) throw new FormatException("Unsupported V5 settlement receipt schema.");
            var receipt = new CareerV5SettlementReceipt(Guid.Parse(RequireString(objectValue, "sessionId")),
                RequireString(objectValue, "reportHash"), Convert.FromBase64String(RequireString(objectValue, "profileUtf8")),
                Convert.FromBase64String(RequireString(objectValue, "contextUtf8")),
                Convert.FromBase64String(RequireString(objectValue, "resultUtf8")),
                Convert.FromBase64String(RequireString(objectValue, "reportUtf8")), Convert.FromBase64String(RequireString(objectValue, "traceUtf8")));
            CareerPlayerProfileV5JsonCodec.Deserialize(receipt.ProfileUtf8);
            var context = ContractJson.DeserializeMatchContextV5(Utf8.GetString(receipt.ContextUtf8));
            var result = ContractJson.DeserializeMatchResultV5(Utf8.GetString(receipt.ResultUtf8), context);
            var report = ContractJson.DeserializeCareerMatchReportV1(Utf8.GetString(receipt.ReportUtf8), context, result);
            if (receipt.SessionId != context.SessionId || receipt.SessionId != report.SessionId ||
                !string.Equals(receipt.ReportHash, report.ReportHash, StringComparison.Ordinal))
                throw new FormatException("V5 settlement receipt bindings are invalid.");
            if (report.EvidenceKind == CareerMatchEvidenceKindV1.QuickSimulationTrace)
            {
                var trace = ContractJson.DeserializeQuickSimulationTraceV1(Utf8.GetString(receipt.TraceUtf8), context);
                if (!string.Equals(trace.TraceHash, report.EvidenceHash, StringComparison.Ordinal))
                    throw new FormatException("V5 settlement receipt quick trace does not bind its report.");
                var rebuiltResult = Volleyball.Career.MatchIntegration.DeterministicQuickSimulationRunnerV5.RebuildResult(context, trace);
                var rebuiltReport = Volleyball.Career.MatchIntegration.DeterministicQuickSimulationRunnerV5.RebuildReport(context, rebuiltResult, trace);
                if (!string.Equals(rebuiltResult.ResultHash, result.ResultHash, StringComparison.Ordinal) ||
                    !string.Equals(rebuiltReport.ReportHash, report.ReportHash, StringComparison.Ordinal))
                    throw new FormatException("V5 settlement receipt quick trace cannot rebuild its result and report.");
            }
            else if (receipt.TraceUtf8.Length != 0)
            {
                throw new FormatException("Physical V5 settlement receipt cannot contain a quick trace.");
            }
            if (!ByteEqual(bytes, Serialize(receipt))) throw new FormatException("V5 settlement receipt is not canonical.");
            return receipt;
        }
        private static string RequireString(StrictJsonObject value, string name)
        {
            var item = value.Get(name);
            if (item.Kind != StrictJsonKind.String) throw new FormatException(name + " must be a string.");
            return item.StringValue;
        }
        private static int RequireInt(StrictJsonObject value, string name)
        {
            var item = value.Get(name);
            if (item.Kind != StrictJsonKind.Integer || item.IntegerValue < int.MinValue || item.IntegerValue > int.MaxValue)
                throw new FormatException(name + " must be an integer.");
            return (int)item.IntegerValue;
        }
        private static bool ByteEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }
    }
}
