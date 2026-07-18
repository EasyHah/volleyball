using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Editor.AI;

namespace Volleyball.EditModeTests
{
    public sealed class SseStreamAccumulatorTests
    {
        [Test]
        public void Accept_FragmentedContent_RecordsFirstContentAndAssemblesJson()
        {
            var accumulator = new SseStreamAccumulator();

            accumulator.Accept("data: {\"choices\":[{\"delta\":{\"content\":\"{\\\"receiver\\\":\"}}]}", 120);
            accumulator.Accept("data: {\"choices\":[{\"delta\":{\"content\":\"\\\"defender\\\"}\"}}]}", 145);
            accumulator.Accept("data: [DONE]", 150);

            Assert.That(accumulator.Content, Is.EqualTo("{\"receiver\":\"defender\"}"));
            Assert.That(accumulator.FirstContentMilliseconds, Is.EqualTo(120));
            Assert.That(accumulator.IsComplete, Is.True);
        }

        [Test]
        public void Accept_ReasoningOnly_DoesNotCountAsVisibleFirstContent()
        {
            var accumulator = new SseStreamAccumulator();

            accumulator.Accept("data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"think\"}}]}", 80);
            accumulator.Accept("data: {\"choices\":[{\"delta\":{\"content\":\"{}\"}}]}", 210);

            Assert.That(accumulator.FirstContentMilliseconds, Is.EqualTo(210));
        }

        [Test]
        public void Accept_FinalUsage_PreservesTokenCounts()
        {
            var accumulator = new SseStreamAccumulator();
            accumulator.Accept("data: {\"choices\":[],\"usage\":{\"prompt_tokens\":90,\"completion_tokens\":20,\"total_tokens\":110}}", 300);

            Assert.That(accumulator.PromptTokens, Is.EqualTo(90));
            Assert.That(accumulator.CompletionTokens, Is.EqualTo(20));
            Assert.That(accumulator.TotalTokens, Is.EqualTo(110));
        }

        [Test]
        [Timeout(2000)]
        public void CompleteAsync_DeadlineExceeded_ReturnsTimeoutWithNoContent()
        {
            var httpClient = new HttpClient(new DelayedHandler(TimeSpan.FromMilliseconds(200)));
            var client = new MenShenChatClient(
                httpClient,
                new Uri("https://menshen-code.test.xdf.cn/v1/chat/completions"));

            var result = client.CompleteAsync(
                MenShenModelProfile.DoubaoMini,
                "system",
                "case",
                "sentinel-secret",
                TimeSpan.FromMilliseconds(50),
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Status, Is.EqualTo(MenShenChatStatus.Timeout));
            Assert.That(result.Content, Is.Empty);
        }

        [Test]
        public void CompleteAsync_UsesBearerAuthorizationForV1Endpoint()
        {
            var handler = new HeaderCaptureHandler();
            var httpClient = new HttpClient(handler);
            var client = new MenShenChatClient(
                httpClient,
                new Uri("https://menshen-code.test.xdf.cn/v1/chat/completions"));

            var result = client.CompleteAsync(
                MenShenModelProfile.DoubaoMini,
                "system",
                "case",
                "sentinel-secret",
                TimeSpan.FromSeconds(1),
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Status, Is.EqualTo(MenShenChatStatus.Success));
            Assert.That(handler.AuthorizationHeader, Is.EqualTo("Bearer sentinel-secret"));
        }

        private sealed class DelayedHandler : HttpMessageHandler
        {
            private readonly TimeSpan delay;

            public DelayedHandler(TimeSpan delay)
            {
                this.delay = delay;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("data: [DONE]\n\n")
                };
            }
        }

        private sealed class HeaderCaptureHandler : HttpMessageHandler
        {
            public string AuthorizationHeader { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                AuthorizationHeader = request.Headers.Authorization?.ToString();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"{}\"}}]}\n\ndata: [DONE]\n\n")
                });
            }
        }
    }
}
