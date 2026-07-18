using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Volleyball.Editor.AI;

namespace Volleyball.EditModeTests
{
    public sealed class MenShenRequestBuilderTests
    {
        [Test]
        public void Build_DoubaoMini_DisablesThinkingAndUsesMaxTokens()
        {
            var json = JObject.Parse(MenShenRequestBuilder.Build(
                MenShenModelProfile.DoubaoMini, "system", "case"));

            Assert.That((string)json["model"], Is.EqualTo("doubao-seed-2.0-mini"));
            Assert.That((string)json["thinking"]?["type"], Is.EqualTo("disabled"));
            Assert.That((int)json["max_tokens"], Is.EqualTo(128));
            Assert.That(json["max_completion_tokens"], Is.Null);
            Assert.That((bool)json["stream"], Is.True);
            Assert.That((bool)json["stream_options"]?["include_usage"], Is.True);
        }

        [Test]
        public void Build_QwenPlus_DisablesThinkingWithoutDoubaoShape()
        {
            var json = JObject.Parse(MenShenRequestBuilder.Build(
                MenShenModelProfile.QwenPlus, "system", "case"));

            Assert.That((bool)json["enable_thinking"], Is.False);
            Assert.That(json["thinking"], Is.Null);
        }

        [Test]
        public void Build_Gpt5Chat_UsesCompletionLimitAndOmitsTemperature()
        {
            var json = JObject.Parse(MenShenRequestBuilder.Build(
                MenShenModelProfile.Gpt5Chat, "system", "case"));

            Assert.That((int)json["max_completion_tokens"], Is.EqualTo(128));
            Assert.That(json["max_tokens"], Is.Null);
            Assert.That(json["temperature"], Is.Null);
            Assert.That(json["thinking"], Is.Null);
            Assert.That(json["enable_thinking"], Is.Null);
        }
    }
}
