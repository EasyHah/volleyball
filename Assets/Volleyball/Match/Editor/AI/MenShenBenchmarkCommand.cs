using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Volleyball.Editor.AI
{
    public readonly struct MenShenBenchmarkConfiguration
    {
        private MenShenBenchmarkConfiguration(
            bool canRun,
            string error,
            string apiKey,
            Uri endpoint)
        {
            CanRun = canRun;
            Error = error ?? string.Empty;
            ApiKey = apiKey ?? string.Empty;
            Endpoint = endpoint;
        }

        public bool CanRun { get; }

        public string Error { get; }

        public string ApiKey { get; }

        public Uri Endpoint { get; }

        public static MenShenBenchmarkConfiguration Resolve(Func<string, string> readEnvironment)
        {
            if (readEnvironment == null)
            {
                throw new ArgumentNullException(nameof(readEnvironment));
            }

            var apiKey = readEnvironment("MENSHEN_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Disabled("MENSHEN_API_KEY is not configured.");
            }

            var baseUrl = readEnvironment("MENSHEN_BASE_URL");
            var endpointText = string.IsNullOrWhiteSpace(baseUrl)
                ? MenShenChatClient.DefaultEndpoint.ToString()
                : baseUrl;

            if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
            {
                return Disabled("MENSHEN_BASE_URL is not a valid absolute URL.");
            }

            var isLocalHttp = endpoint.Scheme == Uri.UriSchemeHttp &&
                              (endpoint.Host == "localhost" || endpoint.Host == "127.0.0.1");
            if (endpoint.Scheme != Uri.UriSchemeHttps && !isLocalHttp)
            {
                return Disabled("MENSHEN_BASE_URL must use HTTPS unless it targets localhost.");
            }

            return new MenShenBenchmarkConfiguration(true, string.Empty, apiKey, endpoint);
        }

        private static MenShenBenchmarkConfiguration Disabled(string error)
        {
            return new MenShenBenchmarkConfiguration(false, error, string.Empty, null);
        }
    }

    public static class MenShenBenchmarkCommand
    {
        private const string FixturePath =
            "Assets/Volleyball/Match/Editor/AI/Fixtures/LlmDecisionBenchmarkV1.json";

        [MenuItem("Volleyball/AI/Run MenShen Decision Benchmark")]
        public static async void RunFromMenu()
        {
            await RunCoreAsync(exitEditor: false);
        }

        public static void Run()
        {
            RunCore(exitEditor: true);
        }

        private static async Task RunCoreAsync(bool exitEditor)
        {
            try
            {
                var reportPath = await ExecuteAsync().ConfigureAwait(true);
                Debug.Log("MenShen benchmark report: " + reportPath);
                ExitIfNeeded(exitEditor, 0);
            }
            catch (Exception exception)
            {
                Debug.LogError("MenShen benchmark failed: " + Sanitize(exception.Message));
                ExitIfNeeded(exitEditor, 1);
            }
        }

        private static void RunCore(bool exitEditor)
        {
            try
            {
                var reportPath = ExecuteAsync().GetAwaiter().GetResult();
                Debug.Log("MenShen benchmark report: " + reportPath);
                ExitIfNeeded(exitEditor, 0);
            }
            catch (Exception exception)
            {
                Debug.LogError("MenShen benchmark failed: " + Sanitize(exception.Message));
                ExitIfNeeded(exitEditor, 1);
            }
        }

        private static async Task<string> ExecuteAsync()
        {
            var config = MenShenBenchmarkConfiguration.Resolve(Environment.GetEnvironmentVariable);
            if (!config.CanRun)
            {
                throw new InvalidOperationException(config.Error);
            }

            var catalog = BenchmarkCaseCatalog.Load(FixturePath);
            var cases = catalog.Cases;
            var repetitions = 3;
            var smokeCase = MenShenBenchmarkCommandLine.SmokeCaseId(Environment.GetCommandLineArgs());
            if (!string.IsNullOrEmpty(smokeCase))
            {
                cases = catalog.Cases.Where(item => item.Id == smokeCase).ToArray();
                if (cases.Count == 0)
                {
                    throw new InvalidOperationException("Smoke case was not found: " + smokeCase);
                }

                repetitions = 1;
            }

            var runner = new MenShenBenchmarkRunner(
                new MenShenChatClient(new System.Net.Http.HttpClient(), config.Endpoint),
                new[]
                {
                    MenShenModelProfile.DoubaoMini,
                    MenShenModelProfile.QwenPlus,
                    MenShenModelProfile.Gpt5Chat
                },
                config.ApiKey,
                TimeSpan.FromMilliseconds(250));
            var result = await runner.RunAsync(cases, repetitions, 7351, CancellationToken.None)
                .ConfigureAwait(false);

            return MenShenBenchmarkReportWriter.Write(result, "TestResults/MenShen");
        }

        private static void ExitIfNeeded(bool exitEditor, int exitCode)
        {
            if (exitEditor)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static string Sanitize(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "unknown error";
            }

            var apiKey = Environment.GetEnvironmentVariable("MENSHEN_API_KEY");
            return string.IsNullOrEmpty(apiKey)
                ? message
                : message.Replace(apiKey, "[redacted]");
        }
    }

    public static class MenShenBenchmarkCommandLine
    {
        public static string SmokeCaseId(string[] args)
        {
            if (args == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == "-menshenSmokeCase" &&
                    !string.IsNullOrWhiteSpace(args[index + 1]) &&
                    !args[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            return string.Empty;
        }
    }
}
