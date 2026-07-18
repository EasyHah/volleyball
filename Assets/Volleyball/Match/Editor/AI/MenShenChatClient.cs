using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Volleyball.Editor.AI
{
    public interface IMenShenChatClient
    {
        Task<MenShenChatResult> CompleteAsync(
            MenShenModelProfile profile,
            string systemPrompt,
            string casePrompt,
            string apiKey,
            TimeSpan deadline,
            CancellationToken cancellationToken);
    }

    public enum MenShenChatStatus
    {
        Success,
        Timeout,
        RateLimited,
        Unauthorized,
        GatewayError,
        MalformedStream
    }

    public readonly struct MenShenChatResult
    {
        public MenShenChatResult(
            MenShenChatStatus status,
            string content,
            int httpStatusCode,
            long firstContentMilliseconds,
            long totalMilliseconds,
            int promptTokens,
            int completionTokens,
            int totalTokens,
            int reasoningCharacterCount,
            string retryAfter,
            string errorCategory)
        {
            Status = status;
            Content = content ?? string.Empty;
            HttpStatusCode = httpStatusCode;
            FirstContentMilliseconds = firstContentMilliseconds;
            TotalMilliseconds = totalMilliseconds;
            PromptTokens = promptTokens;
            CompletionTokens = completionTokens;
            TotalTokens = totalTokens;
            ReasoningCharacterCount = reasoningCharacterCount;
            RetryAfter = retryAfter ?? string.Empty;
            ErrorCategory = errorCategory ?? string.Empty;
        }

        public MenShenChatStatus Status { get; }

        public string Content { get; }

        public int HttpStatusCode { get; }

        public long FirstContentMilliseconds { get; }

        public long TotalMilliseconds { get; }

        public int PromptTokens { get; }

        public int CompletionTokens { get; }

        public int TotalTokens { get; }

        public int ReasoningCharacterCount { get; }

        public string RetryAfter { get; }

        public string ErrorCategory { get; }
    }

    public sealed class MenShenChatClient : IMenShenChatClient
    {
        public static readonly Uri DefaultEndpoint =
            new Uri("https://menshen-code.test.xdf.cn/v1/chat/completions");

        private static readonly HttpClient SharedHttpClient = new HttpClient();

        private readonly HttpClient httpClient;
        private readonly Uri endpoint;

        public MenShenChatClient()
            : this(SharedHttpClient, DefaultEndpoint)
        {
        }

        public MenShenChatClient(HttpClient httpClient, Uri endpoint)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        public async Task<MenShenChatResult> CompleteAsync(
            MenShenModelProfile profile,
            string systemPrompt,
            string casePrompt,
            string apiKey,
            TimeSpan deadline,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key is required.", nameof(apiKey));
            }

            if (deadline <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(deadline));
            }

            var stopwatch = Stopwatch.StartNew();
            using (var deadlineSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var request = CreateRequest(profile, systemPrompt, casePrompt, apiKey))
            {
                deadlineSource.CancelAfter(deadline);
                try
                {
                    using (var response = await httpClient.SendAsync(
                               request,
                               HttpCompletionOption.ResponseHeadersRead,
                               deadlineSource.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            return CreateHttpFailure(response, stopwatch.ElapsedMilliseconds);
                        }

                        var accumulator = new SseStreamAccumulator();
                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var reader = new StreamReader(stream))
                        {
                            while (!reader.EndOfStream)
                            {
                                deadlineSource.Token.ThrowIfCancellationRequested();
                                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                                accumulator.Accept(line, stopwatch.ElapsedMilliseconds);
                            }
                        }

                        if (accumulator.IsMalformed)
                        {
                            return new MenShenChatResult(
                                MenShenChatStatus.MalformedStream,
                                accumulator.Content,
                                (int)response.StatusCode,
                                accumulator.FirstContentMilliseconds,
                                stopwatch.ElapsedMilliseconds,
                                accumulator.PromptTokens,
                                accumulator.CompletionTokens,
                                accumulator.TotalTokens,
                                accumulator.ReasoningCharacterCount,
                                string.Empty,
                                accumulator.ErrorCategory);
                        }

                        return new MenShenChatResult(
                            MenShenChatStatus.Success,
                            accumulator.Content,
                            (int)response.StatusCode,
                            accumulator.FirstContentMilliseconds,
                            stopwatch.ElapsedMilliseconds,
                            accumulator.PromptTokens,
                            accumulator.CompletionTokens,
                            accumulator.TotalTokens,
                            accumulator.ReasoningCharacterCount,
                            string.Empty,
                            string.Empty);
                    }
                }
                catch (OperationCanceledException) when (deadlineSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    return new MenShenChatResult(
                        MenShenChatStatus.Timeout,
                        string.Empty,
                        0,
                        -1,
                        stopwatch.ElapsedMilliseconds,
                        0,
                        0,
                        0,
                        0,
                        string.Empty,
                        "deadline");
                }
                catch (HttpRequestException)
                {
                    return new MenShenChatResult(
                        MenShenChatStatus.GatewayError,
                        string.Empty,
                        0,
                        -1,
                        stopwatch.ElapsedMilliseconds,
                        0,
                        0,
                        0,
                        0,
                        string.Empty,
                        "request-error");
                }
            }
        }

        private HttpRequestMessage CreateRequest(
            MenShenModelProfile profile,
            string systemPrompt,
            string casePrompt,
            string apiKey)
        {
            var body = MenShenRequestBuilder.Build(profile, systemPrompt, casePrompt);
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            return request;
        }

        private static MenShenChatResult CreateHttpFailure(
            HttpResponseMessage response,
            long elapsedMilliseconds)
        {
            var status = response.StatusCode == HttpStatusCode.Unauthorized
                ? MenShenChatStatus.Unauthorized
                : response.StatusCode == (HttpStatusCode)429
                    ? MenShenChatStatus.RateLimited
                    : MenShenChatStatus.GatewayError;
            var retryAfter = response.Headers.RetryAfter?.ToString() ?? string.Empty;

            return new MenShenChatResult(
                status,
                string.Empty,
                (int)response.StatusCode,
                -1,
                elapsedMilliseconds,
                0,
                0,
                0,
                0,
                retryAfter,
                "http-" + (int)response.StatusCode);
        }
    }
}
