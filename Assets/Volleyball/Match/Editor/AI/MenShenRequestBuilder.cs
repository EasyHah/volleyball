using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Volleyball.Editor.AI
{
    public static class MenShenRequestBuilder
    {
        public static string Build(
            MenShenModelProfile profile,
            string systemPrompt,
            string casePrompt)
        {
            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new ArgumentException("System prompt is required.", nameof(systemPrompt));
            }

            if (string.IsNullOrWhiteSpace(casePrompt))
            {
                throw new ArgumentException("Case prompt is required.", nameof(casePrompt));
            }

            var root = new JObject
            {
                ["model"] = profile.ModelId,
                ["stream"] = true,
                ["stream_options"] = new JObject
                {
                    ["include_usage"] = true
                },
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "system",
                        ["content"] = systemPrompt
                    },
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = casePrompt
                    }
                }
            };

            switch (profile.ParameterStyle)
            {
                case ModelParameterStyle.Doubao:
                    root["max_tokens"] = profile.MaxTokens;
                    root["thinking"] = new JObject
                    {
                        ["type"] = "disabled"
                    };
                    break;
                case ModelParameterStyle.Qwen:
                    root["max_tokens"] = profile.MaxTokens;
                    root["enable_thinking"] = false;
                    break;
                case ModelParameterStyle.Gpt5:
                    root["max_completion_tokens"] = profile.MaxTokens;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }

            return root.ToString(Formatting.None);
        }
    }
}
