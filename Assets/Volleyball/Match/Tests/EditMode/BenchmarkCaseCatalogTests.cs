using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Editor.AI;

namespace Volleyball.EditModeTests
{
    public sealed class BenchmarkCaseCatalogTests
    {
        private const string ApprovedFixturePath =
            "Assets/Volleyball/Match/Editor/AI/Fixtures/LlmDecisionBenchmarkV1.json";

        [Test]
        public void Load_ApprovedFixture_ContainsExactlyR01ThroughT10()
        {
            var catalog = BenchmarkCaseCatalog.Load(ApprovedFixturePath);

            Assert.That(catalog.Cases, Has.Count.EqualTo(20));
            CollectionAssert.AreEqual(
                Enumerable.Range(1, 10).Select(i => $"R{i:00}")
                    .Concat(Enumerable.Range(1, 10).Select(i => $"T{i:00}")),
                catalog.Cases.Select(item => item.Id));
            Assert.That(catalog.Cases.Count(item => item.Kind == BenchmarkCaseKind.Round), Is.EqualTo(10));
            Assert.That(catalog.Cases.Count(item => item.Kind == BenchmarkCaseKind.Touch), Is.EqualTo(10));
            Assert.That(catalog.Cases.Where(item => item.Kind == BenchmarkCaseKind.Round).Select(item => item.DeadlineMilliseconds), Is.All.EqualTo(2000));
            Assert.That(catalog.Cases.Where(item => item.Kind == BenchmarkCaseKind.Touch).Select(item => item.DeadlineMilliseconds), Is.All.EqualTo(500));
        }

        [Test]
        public void Load_ApprovedFixture_PreservesEveryPreferredJson()
        {
            var catalog = BenchmarkCaseCatalog.Load(ApprovedFixturePath);

            AssertPreferred(catalog, "R01", "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"middle_quick\",\"third_actor\":\"attacker\",\"attack_route\":\"deep_seam\"}");
            AssertPreferred(catalog, "R02", "{\"receiver\":\"setter\",\"second_actor\":\"defender\",\"set_route\":\"right_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"line\"}");
            AssertPreferred(catalog, "R03", "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"right_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"line\"}");
            AssertPreferred(catalog, "R04", "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"left_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"line\"}");
            AssertPreferred(catalog, "R05", "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"back_set\",\"third_actor\":\"attacker\",\"attack_route\":\"cross_court\"}");
            AssertPreferred(catalog, "R06", "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"back_set\",\"third_actor\":\"attacker\",\"attack_route\":\"roll_shot\"}");
            AssertPreferred(catalog, "R07", "{\"receiver\":\"attacker\",\"second_actor\":\"setter\",\"set_route\":\"middle_quick\",\"third_actor\":\"defender\",\"attack_route\":\"deep_seam\"}");
            AssertPreferred(catalog, "R08", "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"left_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"deep_seam\"}");
            AssertPreferred(catalog, "R09", "{\"receiver\":\"defender\",\"second_actor\":\"setter\",\"set_route\":\"right_pin\",\"third_actor\":\"attacker\",\"attack_route\":\"cross_court\"}");
            AssertPreferred(catalog, "R10", "{\"receiver\":\"attacker\",\"second_actor\":\"setter\",\"set_route\":\"middle_quick\",\"third_actor\":\"defender\",\"attack_route\":\"deep_seam\"}");
            AssertPreferred(catalog, "T01", "{\"next_actor\":\"setter\",\"action\":\"set\",\"target_zone\":\"left_front\",\"tempo\":\"quick\",\"risk\":\"balanced\"}");
            AssertPreferred(catalog, "T02", "{\"next_actor\":\"setter\",\"action\":\"set\",\"target_zone\":\"left_back\",\"tempo\":\"high\",\"risk\":\"safe\"}");
            AssertPreferred(catalog, "T03", "{\"next_actor\":\"defender\",\"action\":\"set\",\"target_zone\":\"left_front\",\"tempo\":\"high\",\"risk\":\"safe\"}");
            AssertPreferred(catalog, "T04", "{\"next_actor\":\"setter\",\"action\":\"set\",\"target_zone\":\"right_front\",\"tempo\":\"normal\",\"risk\":\"balanced\"}");
            AssertPreferred(catalog, "T05", "{\"next_actor\":\"defender\",\"action\":\"free_ball\",\"target_zone\":\"right_back\",\"tempo\":\"high\",\"risk\":\"safe\"}");
            AssertPreferred(catalog, "T06", "{\"next_actor\":\"attacker\",\"action\":\"set\",\"target_zone\":\"middle_front\",\"tempo\":\"high\",\"risk\":\"safe\"}");
            AssertPreferred(catalog, "T07", "{\"next_actor\":\"attacker\",\"action\":\"emergency_save\",\"target_zone\":\"middle_front\",\"tempo\":\"high\",\"risk\":\"safe\"}");
            AssertPreferred(catalog, "T08", "{\"next_actor\":\"defender\",\"action\":\"emergency_save\",\"target_zone\":\"middle_front\",\"tempo\":\"high\",\"risk\":\"safe\"}");
            AssertPreferred(catalog, "T09", "{\"next_actor\":\"attacker\",\"action\":\"attack\",\"target_zone\":\"middle_back\",\"tempo\":\"quick\",\"risk\":\"balanced\"}");
            AssertPreferred(catalog, "T10", "{\"next_actor\":\"attacker\",\"action\":\"attack\",\"target_zone\":\"right_back\",\"tempo\":\"quick\",\"risk\":\"aggressive\"}");
        }

        [Test]
        public void BuildPrompt_NeverContainsAnswerKeyOrScoringText()
        {
            var item = BenchmarkCaseCatalog.Load(ApprovedFixturePath).Cases.Single(c => c.Id == "R03");

            var prompt = BenchmarkPromptBuilder.Build(item);

            Assert.That(prompt, Does.Contain("对方后排两人整体向蓝队进攻视角的左侧移动"));
            Assert.That(prompt, Does.Not.Contain(item.PreferredJson));
            Assert.That(prompt, Does.Not.Contain(item.Rationale));
            Assert.That(prompt, Does.Not.Contain("首选答案"));
        }

        [Test]
        public void Load_T07_DeclaresBlockAsZeroCountedTouches()
        {
            var item = BenchmarkCaseCatalog.Load(ApprovedFixturePath).Cases.Single(c => c.Id == "T07");

            Assert.That(item.CountedTeamTouches, Is.Zero);
            Assert.That(item.State, Does.Contain("拦网触球不计入本队三次触球"));
            Assert.That(DecisionJsonCodec.ParseTouch(item.PreferredJson).NextActor,
                Is.EqualTo(PlayerRole.Attacker));
        }

        private static void AssertPreferred(BenchmarkCaseCatalog catalog, string id, string expected)
        {
            Assert.That(catalog.Cases.Single(item => item.Id == id).PreferredJson, Is.EqualTo(expected));
        }
    }
}
