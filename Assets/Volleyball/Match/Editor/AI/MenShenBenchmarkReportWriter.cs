using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Volleyball.Editor.AI
{
    public static class MenShenBenchmarkReportWriter
    {
        public static string Write(MenShenBenchmarkRunResult result, string outputRoot)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new ArgumentException("Output root is required.", nameof(outputRoot));
            }

            var directory = Path.Combine(
                outputRoot,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "raw-results.json"), BuildRawJson(result), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "review.md"), BuildReviewMarkdown(result), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "review.csv"), BuildReviewCsv(result), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "model-map.json"), BuildModelMapJson(result), Encoding.UTF8);

            return directory;
        }

        private static string BuildRawJson(MenShenBenchmarkRunResult result)
        {
            var attempts = new JArray(result.Attempts.Select(attempt => new JObject
            {
                ["model_id"] = attempt.ModelId,
                ["case_id"] = attempt.CaseId,
                ["kind"] = attempt.Kind.ToString(),
                ["repetition"] = attempt.Repetition,
                ["deadline_ms"] = attempt.DeadlineMilliseconds,
                ["counted_team_touches"] = attempt.CountedTeamTouches,
                ["case_prompt"] = attempt.CasePrompt,
                ["preferred_json"] = attempt.PreferredJson,
                ["status"] = attempt.Status.ToString(),
                ["content"] = attempt.ChatResult.Content,
                ["parsed_decision_json"] = attempt.ParsedDecisionJson,
                ["used_markdown_fence_repair"] = attempt.UsedMarkdownFenceRepair,
                ["format_score"] = attempt.FormatScore,
                ["preferred_match_score"] = attempt.PreferredMatchScore,
                ["hard_zero_reasons"] = new JArray(attempt.HardZeroReasons),
                ["http_status_code"] = attempt.ChatResult.HttpStatusCode,
                ["first_content_ms"] = attempt.ChatResult.FirstContentMilliseconds,
                ["total_ms"] = attempt.ChatResult.TotalMilliseconds,
                ["prompt_tokens"] = attempt.ChatResult.PromptTokens,
                ["completion_tokens"] = attempt.ChatResult.CompletionTokens,
                ["total_tokens"] = attempt.ChatResult.TotalTokens,
                ["reasoning_character_count"] = attempt.ChatResult.ReasoningCharacterCount,
                ["retry_after"] = attempt.ChatResult.RetryAfter,
                ["error_category"] = attempt.ChatResult.ErrorCategory
            }));

            var root = new JObject
            {
                ["metrics"] = BuildMetrics(result.Attempts),
                ["attempts"] = attempts
            };
            return root.ToString(Formatting.Indented);
        }

        private static JObject BuildMetrics(IReadOnlyList<MenShenBenchmarkAttempt> attempts)
        {
            var successful = attempts
                .Where(attempt => attempt.Status == MenShenChatStatus.Success)
                .Select(attempt => (double)attempt.ChatResult.TotalMilliseconds)
                .OrderBy(value => value)
                .ToArray();
            var timeoutCount = attempts.Count(attempt => attempt.Status == MenShenChatStatus.Timeout);

            return new JObject
            {
                ["attempt_count"] = attempts.Count,
                ["success_count"] = successful.Length,
                ["timeout_count"] = timeoutCount,
                ["timeout_rate"] = attempts.Count == 0 ? 0d : (double)timeoutCount / attempts.Count,
                ["p50_total_ms"] = Percentile(successful, 0.50d),
                ["p95_total_ms"] = Percentile(successful, 0.95d)
            };
        }

        private static double Percentile(double[] sorted, double percentile)
        {
            if (sorted.Length == 0)
            {
                return 0d;
            }

            var index = (int)Math.Ceiling(sorted.Length * percentile) - 1;
            index = Math.Max(0, Math.Min(sorted.Length - 1, index));
            return sorted[index];
        }

        private static string BuildReviewMarkdown(MenShenBenchmarkRunResult result)
        {
            var modelToAlias = result.AliasToModel.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
            var builder = new StringBuilder();
            builder.AppendLine("# MenShen Volleyball Decision Review");
            builder.AppendLine();
            foreach (var group in result.Attempts.GroupBy(attempt => attempt.CaseId).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                builder.AppendLine("## " + group.Key);
                foreach (var attempt in group)
                {
                    builder.AppendLine();
                    builder.AppendLine("Model " + modelToAlias[attempt.ModelId] + " repetition " + attempt.Repetition);
                    builder.AppendLine();
                    builder.AppendLine("```json");
                    builder.AppendLine(attempt.ChatResult.Content);
                    builder.AppendLine("```");
                }

                builder.AppendLine();
                builder.AppendLine("Answer key:");
                builder.AppendLine();
                builder.AppendLine("```json");
                builder.AppendLine(group.First().PreferredJson);
                builder.AppendLine("```");
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string BuildReviewCsv(MenShenBenchmarkRunResult result)
        {
            var modelToAlias = result.AliasToModel.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
            var builder = new StringBuilder();
            builder.AppendLine("case_id,model_alias,repetition,status,total_ms,format_score,preferred_match_score,role_score,space_score,risk_score,manual_note");
            foreach (var attempt in result.Attempts)
            {
                builder.Append(Csv(attempt.CaseId)).Append(',')
                    .Append(Csv(modelToAlias[attempt.ModelId])).Append(',')
                    .Append(attempt.Repetition).Append(',')
                    .Append(Csv(attempt.Status.ToString())).Append(',')
                    .Append(attempt.ChatResult.TotalMilliseconds).Append(',')
                    .Append(attempt.FormatScore).Append(',')
                    .Append(attempt.PreferredMatchScore).Append(",,,,")
                    .AppendLine();
            }

            return builder.ToString();
        }

        private static string BuildModelMapJson(MenShenBenchmarkRunResult result)
        {
            var root = new JObject();
            foreach (var pair in result.AliasToModel.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                root[pair.Key] = pair.Value;
            }

            return root.ToString(Formatting.Indented);
        }

        private static string Csv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
