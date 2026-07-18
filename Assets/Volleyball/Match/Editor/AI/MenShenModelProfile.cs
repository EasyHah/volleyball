using System;

namespace Volleyball.Editor.AI
{
    public enum ModelParameterStyle
    {
        Doubao,
        Qwen,
        Gpt5
    }

    public readonly struct MenShenModelProfile : IEquatable<MenShenModelProfile>
    {
        public static readonly MenShenModelProfile DoubaoMini =
            new MenShenModelProfile("doubao-seed-2.0-mini", 128, ModelParameterStyle.Doubao);

        public static readonly MenShenModelProfile QwenPlus =
            new MenShenModelProfile("qwen3.7-plus", 128, ModelParameterStyle.Qwen);

        public static readonly MenShenModelProfile Gpt5Chat =
            new MenShenModelProfile("gpt-5-chat", 128, ModelParameterStyle.Gpt5);

        public MenShenModelProfile(string modelId, int maxTokens, ModelParameterStyle parameterStyle)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model id is required.", nameof(modelId));
            }

            if (maxTokens <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTokens));
            }

            if (!Enum.IsDefined(typeof(ModelParameterStyle), parameterStyle))
            {
                throw new ArgumentOutOfRangeException(nameof(parameterStyle));
            }

            ModelId = modelId;
            MaxTokens = maxTokens;
            ParameterStyle = parameterStyle;
        }

        public string ModelId { get; }

        public int MaxTokens { get; }

        public ModelParameterStyle ParameterStyle { get; }

        public bool Equals(MenShenModelProfile other)
        {
            return ModelId == other.ModelId &&
                   MaxTokens == other.MaxTokens &&
                   ParameterStyle == other.ParameterStyle;
        }

        public override bool Equals(object obj)
        {
            return obj is MenShenModelProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ModelId.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxTokens;
                return (hashCode * 397) ^ (int)ParameterStyle;
            }
        }
    }
}
