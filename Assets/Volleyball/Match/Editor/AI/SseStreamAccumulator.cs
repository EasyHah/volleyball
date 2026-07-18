using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Volleyball.Editor.AI
{
    public sealed class SseStreamAccumulator
    {
        private readonly StringBuilder content = new StringBuilder();

        public string Content => content.ToString();

        public long FirstContentMilliseconds { get; private set; } = -1;

        public bool IsComplete { get; private set; }

        public bool IsMalformed { get; private set; }

        public string ErrorCategory { get; private set; } = string.Empty;

        public int PromptTokens { get; private set; }

        public int CompletionTokens { get; private set; }

        public int TotalTokens { get; private set; }

        public int ReasoningCharacterCount { get; private set; }

        public void Accept(string line, long elapsedMilliseconds)
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith(":", StringComparison.Ordinal))
            {
                return;
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                MarkMalformed("missing-data-prefix");
                return;
            }

            var payloadText = line.Substring("data: ".Length);
            if (payloadText == "[DONE]")
            {
                IsComplete = true;
                return;
            }

            JObject payload;
            try
            {
                payload = JObject.Parse(payloadText);
            }
            catch (JsonException)
            {
                MarkMalformed("invalid-json-event");
                return;
            }

            CaptureUsage(payload["usage"] as JObject);
            var choices = payload["choices"] as JArray;
            if (choices == null || choices.Count == 0)
            {
                return;
            }

            var delta = choices[0]?["delta"] as JObject;
            if (delta == null)
            {
                return;
            }

            var reasoning = delta["reasoning_content"]?.Value<string>();
            if (!string.IsNullOrEmpty(reasoning))
            {
                ReasoningCharacterCount += reasoning.Length;
            }

            var part = delta["content"]?.Value<string>();
            if (string.IsNullOrEmpty(part))
            {
                return;
            }

            if (FirstContentMilliseconds < 0)
            {
                FirstContentMilliseconds = elapsedMilliseconds;
            }

            content.Append(part);
        }

        private void CaptureUsage(JObject usage)
        {
            if (usage == null)
            {
                return;
            }

            PromptTokens = usage["prompt_tokens"]?.Value<int>() ?? PromptTokens;
            CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? CompletionTokens;
            TotalTokens = usage["total_tokens"]?.Value<int>() ?? TotalTokens;
        }

        private void MarkMalformed(string category)
        {
            IsMalformed = true;
            if (string.IsNullOrEmpty(ErrorCategory))
            {
                ErrorCategory = category;
            }
        }
    }
}
