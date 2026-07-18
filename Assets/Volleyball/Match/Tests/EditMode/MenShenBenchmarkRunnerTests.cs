using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Editor.AI;

namespace Volleyball.EditModeTests
{
    public sealed class MenShenBenchmarkRunnerTests
    {
        private const string ApprovedFixturePath =
            "Assets/Volleyball/Match/Editor/AI/Fixtures/LlmDecisionBenchmarkV1.json";

        private static BenchmarkCaseCatalog Catalog => BenchmarkCaseCatalog.Load(ApprovedFixturePath);

        [Test]
        public void RunAsync_ThreeProfilesTwentyCasesThreeRepetitions_Produces180Attempts()
        {
            var runner = CreateRunner(new SuccessfulFakeClient());

            var result = runner.RunAsync(Catalog, repetitions: 3, seed: 7351, CancellationToken.None)
                .GetAwaiter().GetResult();

            Assert.That(result.Attempts, Has.Count.EqualTo(180));
            Assert.That(result.Attempts.Select(a => a.CaseId).Distinct().Count(), Is.EqualTo(20));
            Assert.That(result.Attempts.Select(a => a.ModelId).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void RunAsync_SameSeed_ReplaysAttemptOrderExactly()
        {
            var first = CreateRunner(new SuccessfulFakeClient())
                .RunAsync(Catalog, 3, 7351, CancellationToken.None).GetAwaiter().GetResult();
            var second = CreateRunner(new SuccessfulFakeClient())
                .RunAsync(Catalog, 3, 7351, CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(
                first.Attempts.Select(a => (a.ModelId, a.CaseId, a.Repetition)),
                second.Attempts.Select(a => (a.ModelId, a.CaseId, a.Repetition)));
        }

        [Test]
        public void RunAsync_Timeout_IsRecordedAndNextAttemptContinues()
        {
            var result = CreateRunner(new FirstCallTimeoutFakeClient())
                .RunAsync(Catalog, 1, 7351, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Attempts[0].Status, Is.EqualTo(MenShenChatStatus.Timeout));
            Assert.That(result.Attempts, Has.Count.EqualTo(60));
        }

        [Test]
        public void Write_CreatesAnonymousReviewAndSeparateModelMap()
        {
            var result = CreateRunner(new SuccessfulFakeClient())
                .RunAsync(Catalog, 3, 7351, CancellationToken.None).GetAwaiter().GetResult();
            var outputRoot = Path.Combine("TestResults", "MenShenRunnerTests", Guid.NewGuid().ToString("N"));

            var output = MenShenBenchmarkReportWriter.Write(result, outputRoot);

            Assert.That(File.Exists(Path.Combine(output, "raw-results.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "review.md")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "review.csv")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "model-map.json")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(output, "review.md")), Does.Contain("Model A"));
            Assert.That(File.ReadAllText(Path.Combine(output, "review.md")), Does.Not.Contain("doubao-seed-2.0-mini"));
            Assert.That(File.ReadAllText(Path.Combine(output, "raw-results.json")), Does.Contain("doubao-seed-2.0-mini"));
            Assert.That(File.ReadAllText(Path.Combine(output, "raw-results.json")), Does.Not.Contain("sentinel-fake-key"));
            Assert.That(File.ReadAllText(Path.Combine(output, "review.csv")).Split('\n').Count(line => line.Contains(",")), Is.GreaterThanOrEqualTo(181));
        }

        private static MenShenBenchmarkRunner CreateRunner(IMenShenChatClient client)
        {
            return new MenShenBenchmarkRunner(
                client,
                new[]
                {
                    MenShenModelProfile.DoubaoMini,
                    MenShenModelProfile.QwenPlus,
                    MenShenModelProfile.Gpt5Chat
                },
                apiKey: "sentinel-fake-key",
                pacingInterval: TimeSpan.Zero);
        }

        private sealed class SuccessfulFakeClient : IMenShenChatClient
        {
            public Task<MenShenChatResult> CompleteAsync(
                MenShenModelProfile profile,
                string systemPrompt,
                string casePrompt,
                string apiKey,
                TimeSpan deadline,
                CancellationToken cancellationToken)
            {
                var content = deadline <= TimeSpan.FromMilliseconds(500)
                    ? "{\"next_actor\":\"setter\",\"action\":\"set\",\"target_zone\":\"right_front\",\"tempo\":\"high\",\"risk\":\"safe\"}"
                    : "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"right_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"line\"}";

                return Task.FromResult(new MenShenChatResult(
                    MenShenChatStatus.Success,
                    content,
                    httpStatusCode: 200,
                    firstContentMilliseconds: 25,
                    totalMilliseconds: 50,
                    promptTokens: 90,
                    completionTokens: 20,
                    totalTokens: 110,
                    reasoningCharacterCount: 0,
                    retryAfter: string.Empty,
                    errorCategory: string.Empty));
            }
        }

        private sealed class FirstCallTimeoutFakeClient : IMenShenChatClient
        {
            private int calls;

            public Task<MenShenChatResult> CompleteAsync(
                MenShenModelProfile profile,
                string systemPrompt,
                string casePrompt,
                string apiKey,
                TimeSpan deadline,
                CancellationToken cancellationToken)
            {
                calls++;
                if (calls == 1)
                {
                    return Task.FromResult(new MenShenChatResult(
                        MenShenChatStatus.Timeout,
                        string.Empty,
                        httpStatusCode: 0,
                        firstContentMilliseconds: -1,
                        totalMilliseconds: (long)deadline.TotalMilliseconds,
                        promptTokens: 0,
                        completionTokens: 0,
                        totalTokens: 0,
                        reasoningCharacterCount: 0,
                        retryAfter: string.Empty,
                        errorCategory: "deadline"));
                }

                return new SuccessfulFakeClient().CompleteAsync(
                    profile,
                    systemPrompt,
                    casePrompt,
                    apiKey,
                    deadline,
                    cancellationToken);
            }
        }
    }
}
