using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Editor.AI;

namespace Volleyball.EditModeTests
{
    public sealed class DecisionJsonCodecTests
    {
        [Test]
        public void ParseRound_ExactApprovedObject_ReturnsDecision()
        {
            var json = "{\"receiver\":\"defender\",\"second_actor\":\"setter\"," +
                       "\"set_route\":\"right_pin\",\"third_actor\":\"attacker\"," +
                       "\"attack_route\":\"line\"}";

            var decision = DecisionJsonCodec.ParseRound(json);

            Assert.That(decision.Receiver, Is.EqualTo(PlayerRole.Defender));
            Assert.That(decision.SetRoute, Is.EqualTo(SetRoute.RightPin));
            Assert.That(decision.AttackRoute, Is.EqualTo(SpikeRoute.Line));
        }

        [TestCase("{\"receiver\":\"defender\"}")]
        [TestCase("{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"right_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"line\",\"reason\":\"open\"}")]
        [TestCase("```json\\n{\"receiver\":\"defender\"}\\n```")]
        public void ParseRound_MissingExtraOrWrappedContent_Throws(string json)
        {
            Assert.Throws<DecisionFormatException>(() => DecisionJsonCodec.ParseRound(json));
        }

        [Test]
        public void ParseTouch_DuplicateProperty_Throws()
        {
            var json = "{\"next_actor\":\"setter\",\"next_actor\":\"attacker\"," +
                       "\"action\":\"set\",\"target_zone\":\"right_front\"," +
                       "\"tempo\":\"high\",\"risk\":\"safe\"}";

            Assert.Throws<DecisionFormatException>(() => DecisionJsonCodec.ParseTouch(json));
        }

        [Test]
        public void TryStripSingleMarkdownFence_RepairsOnlyOneJsonFence()
        {
            var wrapped = "```json\n{\"next_actor\":\"setter\",\"action\":\"set\"," +
                          "\"target_zone\":\"right_front\",\"tempo\":\"high\",\"risk\":\"safe\"}\n```";

            Assert.That(DecisionJsonRepair.TryStripSingleMarkdownFence(wrapped, out var repaired), Is.True);
            Assert.DoesNotThrow(() => DecisionJsonCodec.ParseTouch(repaired));
            Assert.That(DecisionJsonRepair.TryStripSingleMarkdownFence("prefix " + wrapped, out _), Is.False);
        }
    }
}
