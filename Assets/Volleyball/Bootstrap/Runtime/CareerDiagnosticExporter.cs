using System;
using System.Globalization;
using System.IO;
using System.Text;
using Volleyball.Career.Domain;

namespace Volleyball.Bootstrap
{
    public readonly struct CareerDiagnosticExportResult
    {
        public CareerDiagnosticExportResult(bool succeeded, string fileName)
        {
            Succeeded = succeeded;
            FileName = fileName;
        }

        public bool Succeeded { get; }
        public string FileName { get; }
    }

    public sealed class CareerDiagnosticExporter
    {
        private readonly string _directory;

        public CareerDiagnosticExporter(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new ArgumentException(
                    "A persistent data root is required.",
                    nameof(persistentDataPath));
            }

            _directory = Path.Combine(
                Path.GetFullPath(persistentDataPath),
                "Diagnostics");
        }

        public CareerDiagnosticExportResult Export(
            CareerSaveSnapshot snapshot,
            string route,
            string feedbackCode,
            long generatedAtUtcMs,
            Guid reportId,
            string unityVersion,
            string productVersion,
            string platform)
        {
            if (reportId == Guid.Empty || generatedAtUtcMs < 0)
            {
                return new CareerDiagnosticExportResult(false, null);
            }

            var fileName = "career-diagnostic-" + generatedAtUtcMs + "-" +
                           reportId.ToString("N") + ".json";
            var path = Path.Combine(_directory, fileName);
            var temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_directory);
                var bytes = new UTF8Encoding(false).GetBytes(BuildJson(
                    snapshot,
                    route,
                    feedbackCode,
                    generatedAtUtcMs,
                    unityVersion,
                    productVersion,
                    platform));
                using (var stream = new FileStream(
                           temporary,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                File.Move(temporary, path);
                return new CareerDiagnosticExportResult(true, fileName);
            }
            catch (IOException)
            {
                TryDelete(temporary);
                return new CareerDiagnosticExportResult(false, null);
            }
            catch (UnauthorizedAccessException)
            {
                TryDelete(temporary);
                return new CareerDiagnosticExportResult(false, null);
            }
        }

        private static string BuildJson(
            CareerSaveSnapshot snapshot,
            string route,
            string feedbackCode,
            long generatedAtUtcMs,
            string unityVersion,
            string productVersion,
            string platform)
        {
            var builder = new StringBuilder(768);
            builder.Append("{\n  \"schemaVersion\": 1,");
            Append(builder, "generatedAtUtcMs", generatedAtUtcMs);
            Append(builder, "unityVersion", unityVersion);
            Append(builder, "productVersion", productVersion);
            Append(builder, "platform", platform);
            Append(builder, "route", route);
            Append(builder, "feedbackCode", feedbackCode);
            Append(builder, "hasSnapshot", snapshot != null);
            if (snapshot != null)
            {
                Append(builder, "saveSchemaVersion", snapshot.Versions.SchemaVersion);
                Append(builder, "contentVersion", snapshot.Versions.ContentVersion);
                Append(builder, "rulesetVersion", snapshot.Versions.RulesetVersion);
                Append(builder, "contractVersion", snapshot.Versions.ContractVersion);
                Append(builder, "careerRandomAlgorithmVersion",
                    snapshot.Versions.CareerRandomAlgorithmVersion);
                Append(builder, "revision", snapshot.Identity.Revision);
                Append(builder, "snapshotHash", snapshot.Identity.SnapshotHash.Value);
                Append(builder, "progressionKind", snapshot.Progression.Kind.ToString());
                Append(builder, "phase", snapshot.Progression.Phase.ToString());
                Append(builder, "season", snapshot.Progression.WeekPlan?.Season ?? 0);
                Append(builder, "week", snapshot.Progression.WeekPlan?.Week ?? 0);
                Append(builder, "hasPendingMatch", snapshot.PendingMatch != null);
                Append(builder, "settlementReceiptCount", snapshot.SettlementReceipts.Count);
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == ',')
            {
                builder.Length--;
            }

            builder.Append("\n}\n");
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string name, string value)
        {
            builder.Append("\n  ").Append(Quote(name)).Append(": ")
                .Append(Quote(value ?? string.Empty)).Append(',');
        }

        private static void Append(StringBuilder builder, string name, long value)
        {
            builder.Append("\n  ").Append(Quote(name)).Append(": ")
                .Append(value.ToString(CultureInfo.InvariantCulture)).Append(',');
        }

        private static void Append(StringBuilder builder, string name, bool value)
        {
            builder.Append("\n  ").Append(Quote(name)).Append(": ")
                .Append(value ? "true" : "false").Append(',');
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder((value?.Length ?? 0) + 2);
            builder.Append('"');
            if (value != null)
            {
                foreach (var character in value)
                {
                    switch (character)
                    {
                        case '"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (character < 0x20)
                            {
                                builder.Append("\\u").Append(((int)character).ToString("x4"));
                            }
                            else
                            {
                                builder.Append(character);
                            }

                            break;
                    }
                }
            }

            return builder.Append('"').ToString();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
