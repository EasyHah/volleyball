using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Shared.MatchV2.EditModeTests
{
    public sealed class MatchV2CanonicalJsonTests
    {
        [Test]
        public void Writer_LocksEscapingUnicodeNullEmptyOrderAndSafeIntegers()
        {
            var writer = new MatchV2CanonicalJsonWriter();
            writer.StartObject();
            writer.Property("value"); writer.String("\"\\/\b\t\n\f\r\0\u001f汉😀é");
            writer.Property("decomposed"); writer.String("e\u0301");
            writer.Property("null"); writer.Null();
            writer.Property("empty"); writer.StartArray(); writer.EndArray();
            writer.Property("min"); writer.Integer(-9007199254740991L);
            writer.Property("max"); writer.Integer(9007199254740991L);
            writer.EndObject();

            const string expected = "{\"value\":\"\\\"\\\\/\\b\\t\\n\\f\\r\\u0000\\u001f汉😀é\",\"decomposed\":\"é\",\"null\":null,\"empty\":[],\"min\":-9007199254740991,\"max\":9007199254740991}";
            Assert.That(writer.Bytes(), Is.EqualTo(new UTF8Encoding(false, true).GetBytes(expected)));
            Assert.That(MatchV2FixtureTests.Hash(writer.Bytes()),
                Is.EqualTo("569995cdbc583f35d53a3087b14537c0b3605b081da1697852c9d392b410b4c2"));
        }

        [Test]
        public void Writer_PreservesComposedAndDecomposedUnicodeWithoutNormalization()
        {
            var composed = ScalarDocument("é");
            var decomposed = ScalarDocument("e\u0301");
            Assert.That(composed, Is.Not.EqualTo(decomposed));
            Assert.That(Encoding.UTF8.GetString(composed), Is.EqualTo("{\"v\":\"é\"}"));
            Assert.That(Encoding.UTF8.GetString(decomposed), Is.EqualTo("{\"v\":\"é\"}"));
        }

        [Test]
        public void Writer_UsesTheLockedEscapeForEveryControlCharacter()
        {
            var expectedEscapes = new[]
            {
                "\\u0000", "\\u0001", "\\u0002", "\\u0003",
                "\\u0004", "\\u0005", "\\u0006", "\\u0007",
                "\\b", "\\t", "\\n", "\\u000b", "\\f", "\\r",
                "\\u000e", "\\u000f", "\\u0010", "\\u0011",
                "\\u0012", "\\u0013", "\\u0014", "\\u0015",
                "\\u0016", "\\u0017", "\\u0018", "\\u0019",
                "\\u001a", "\\u001b", "\\u001c", "\\u001d",
                "\\u001e", "\\u001f"
            };

            for (var value = 0; value < expectedEscapes.Length; value++)
            {
                var writer = new MatchV2CanonicalJsonWriter();
                writer.String(((char)value).ToString());
                Assert.That(Encoding.UTF8.GetString(writer.Bytes()),
                    Is.EqualTo("\"" + expectedEscapes[value] + "\""),
                    "U+" + value.ToString("X4"));
            }
        }

        [Test]
        public void PublicCodec_LocksEveryLegalWireTokenInBothDirections()
        {
            var fixture = MatchV2TestFactory.CreateContext(
                MatchV2TestFactory.CreateTeams(), priority: PreMatchPriorityV2.AttackFirst);
            var direct = MatchV2TestFactory.CreateContext(
                MatchV2TestFactory.CreateTeams(), MatchExecutionModeV2.Direct,
                null, null, null, null, priority: PreMatchPriorityV2.FirstContactSecurity);
            var quick = MatchV2TestFactory.CreateContext(
                MatchV2TestFactory.CreateTeams(), MatchExecutionModeV2.QuickSimulation,
                null, null, 1, 1, priority: PreMatchPriorityV2.StaminaControl);

            var contextVectors = new[]
            {
                (fixture, "fixture", PreMatchPriorityV2.AttackFirst, "attack_first"),
                (direct, "direct", PreMatchPriorityV2.FirstContactSecurity, "first_contact_security"),
                (quick, "quick_simulation", PreMatchPriorityV2.StaminaControl, "stamina_control")
            };
            foreach (var vector in contextVectors)
            {
                var bytes = MatchContractV2Json.SerializeContext(vector.Item1);
                var text = Encoding.UTF8.GetString(bytes);
                Assert.That(text, Does.Contain("\"executionMode\":\"" + vector.Item2 + "\""));
                Assert.That(text, Does.Contain("\"preMatchPriority\":\"" + vector.Item4 + "\""));

                var decoded = MatchContractV2Json.DeserializeContext(bytes);
                Assert.That(decoded.ExecutionMode, Is.EqualTo(vector.Item1.ExecutionMode));
                Assert.That(decoded.PreMatchPriority, Is.EqualTo(vector.Item3));
            }

            var fixtureText = Encoding.UTF8.GetString(MatchContractV2Json.SerializeContext(fixture));
            Assert.That(fixtureText, Does.Contain("\"kind\":\"indoor_6v6\""));
            Assert.That(fixtureText, Does.Contain("\"side\":\"home\""));
            Assert.That(fixtureText, Does.Contain("\"side\":\"away\""));
            foreach (var token in new[]
                     {
                         "setter", "outside_hitter", "middle_blocker", "opposite", "libero"
                     })
                Assert.That(fixtureText, Does.Contain("\"position\":\"" + token + "\""));

            var decodedFixture = MatchContractV2Json.DeserializeContext(
                MatchContractV2Json.SerializeContext(fixture));
            Assert.That(decodedFixture.Format.Kind, Is.EqualTo("indoor_6v6"));
            Assert.That(decodedFixture.Teams.Select(team => team.Side),
                Is.EqualTo(new[] { TeamSideV2.Home, TeamSideV2.Away }));
            Assert.That(decodedFixture.Teams.SelectMany(team => team.Players)
                    .Select(player => player.Position).Distinct(),
                Is.EquivalentTo(new[]
                {
                    PlayerPositionV2.Setter,
                    PlayerPositionV2.OutsideHitter,
                    PlayerPositionV2.MiddleBlocker,
                    PlayerPositionV2.Opposite,
                    PlayerPositionV2.Libero
                }));

            var facts = MatchV2TestFactory.ZeroFacts(fixture);
            var completed = MatchResultV2.CreateCompleted(
                fixture, fixture.Teams[0].TeamId,
                new[] { new SetScoreV2(1, 25, 21, true) }, 46, facts);
            var abandoned = MatchResultV2.CreateAbandoned(
                fixture, Array.Empty<SetScoreV2>(), 0, MatchV2TestFactory.ZeroFacts(fixture));
            foreach (var vector in new[]
                     {
                         (completed, MatchStatusV2.Completed, "completed"),
                         (abandoned, MatchStatusV2.Abandoned, "abandoned")
                     })
            {
                var bytes = MatchContractV2Json.SerializeResult(vector.Item1);
                Assert.That(Encoding.UTF8.GetString(bytes),
                    Does.Contain("\"status\":\"" + vector.Item3 + "\""));
                Assert.That(MatchContractV2Json.DeserializeResult(bytes, fixture).Status,
                    Is.EqualTo(vector.Item2));
            }
        }

        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("missing")]
        [TestCase("reordered")]
        [TestCase("whitespace")]
        [TestCase("float")]
        [TestCase("exponent")]
        [TestCase("leading_zero")]
        [TestCase("trailing")]
        [TestCase("illegal_escape")]
        [TestCase("lone_surrogate")]
        [TestCase("uppercase_uuid")]
        [TestCase("empty_uuid")]
        [TestCase("uppercase_hash")]
        [TestCase("negative_seed")]
        [TestCase("overflow_seed")]
        [TestCase("tampered_payload")]
        public void ContextDeserializer_RejectsNoncanonicalAndTamperedBytes(string vector)
        {
            var canonical = MatchV2FixtureTests.CanonicalFixture(
                "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1/golden-context.json");
            var text = Encoding.UTF8.GetString(canonical);
            byte[] bytes;
            switch (vector)
            {
                case "duplicate":
                    text = text.Substring(0, text.Length - 1) + ",\"contextHash\":\"da570cff972d280acb9307edb715bcef88a0f958e75ea615072a5be25edf0527\"}";
                    bytes = Encoding.UTF8.GetBytes(text); break;
                case "unknown":
                    text = text.Replace("{\"versions\":", "{\"unknown\":0,\"versions\":"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "missing":
                    text = text.Replace("\"importanceBasisPoints\":7500,", string.Empty); bytes = Encoding.UTF8.GetBytes(text); break;
                case "reordered":
                    text = text.Replace("\"sessionId\":\"55555555-5555-5555-5555-555555555555\",\"executionMode\":\"fixture\"", "\"executionMode\":\"fixture\",\"sessionId\":\"55555555-5555-5555-5555-555555555555\""); bytes = Encoding.UTF8.GetBytes(text); break;
                case "whitespace": bytes = Encoding.UTF8.GetBytes(" " + text); break;
                case "float": text = text.Replace("\"matchSeed\":25649701", "\"matchSeed\":25649701.0"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "exponent": text = text.Replace("\"matchSeed\":25649701", "\"matchSeed\":25649701e0"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "leading_zero": text = text.Replace("\"matchSeed\":25649701", "\"matchSeed\":025649701"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "trailing": bytes = Encoding.UTF8.GetBytes(text + "null"); break;
                case "illegal_escape": text = text.Replace("competition.university.v1", "competition\\xuniversity"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "lone_surrogate": text = text.Replace("competition.university.v1", "competition\\ud800"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "uppercase_uuid": text = text.Replace("55555555-5555-5555-5555-555555555555", "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "empty_uuid": text = text.Replace("55555555-5555-5555-5555-555555555555", "00000000-0000-0000-0000-000000000000"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "uppercase_hash": text = text.Replace("da570cff", "DA570CFF"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "negative_seed": text = text.Replace("\"matchSeed\":25649701", "\"matchSeed\":-1"); bytes = Encoding.UTF8.GetBytes(text); break;
                case "overflow_seed": text = text.Replace("\"matchSeed\":25649701", "\"matchSeed\":4294967296"); bytes = Encoding.UTF8.GetBytes(text); break;
                default: text = text.Replace("\"matchSeed\":25649701", "\"matchSeed\":25649702"); bytes = Encoding.UTF8.GetBytes(text); break;
            }
            Assert.That(() => MatchContractV2Json.DeserializeContext(bytes),
                Throws.TypeOf<MatchV2ContractException>(), vector);
        }

        [Test]
        public void Deserializer_RejectsInvalidUtf8AndBom()
        {
            Assert.That(() => MatchContractV2Json.DeserializeContext(new byte[] { 0x7b, 0xff, 0x7d }),
                Throws.TypeOf<MatchV2ContractException>());
            var canonical = MatchV2FixtureTests.CanonicalFixture(
                "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1/golden-context.json");
            var bom = new byte[] { 0xef, 0xbb, 0xbf }.Concat(canonical).ToArray();
            Assert.That(() => MatchContractV2Json.DeserializeContext(bom),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [TestCase("session")]
        [TestCase("context")]
        [TestCase("player_order")]
        [TestCase("foreign_player")]
        [TestCase("duplicate_player")]
        [TestCase("contract_version")]
        [TestCase("content_version")]
        [TestCase("result_hash")]
        public void ResultDeserializer_RejectsMismatchedOrTamperedIdentity(string vector)
        {
            var context = MatchContractV2Json.DeserializeContext(MatchV2FixtureTests.CanonicalFixture(
                "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1/golden-context.json"));
            var bytes = MatchV2FixtureTests.CanonicalFixture(
                "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1/golden-result.json");
            var text = Encoding.UTF8.GetString(bytes);
            switch (vector)
            {
                case "session": text = text.Replace("55555555-5555-5555-5555-555555555555", "66666666-6666-6666-6666-666666666666"); break;
                case "context": text = text.Replace("da570cff", "ba570cff"); break;
                case "player_order": text = text.Replace("player.home.opposite", "player.swap.marker").Replace("player.career.protagonist", "player.home.opposite").Replace("player.swap.marker", "player.career.protagonist"); break;
                case "foreign_player": text = text.Replace("player.home.opposite", "player.foreign"); break;
                case "duplicate_player": text = text.Replace("player.career.protagonist", "player.home.opposite"); break;
                case "contract_version": text = text.Replace("\"contractVersion\":2", "\"contractVersion\":3"); break;
                case "content_version": text = text.Replace("\"contentVersion\":1", "\"contentVersion\":2"); break;
                default: text = text.Replace("3fbb0338", "4fbb0338"); break;
            }
            Assert.That(() => MatchContractV2Json.DeserializeResult(Encoding.UTF8.GetBytes(text), context),
                Throws.TypeOf<MatchV2ContractException>());
        }

        [Test]
        public void ResultDeserializer_RejectsEmptyContextHashAsContractError()
        {
            var context = MatchContractV2Json.DeserializeContext(MatchV2FixtureTests.CanonicalFixture(
                "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1/golden-context.json"));
            var text = Encoding.UTF8.GetString(MatchV2FixtureTests.CanonicalFixture(
                "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1/golden-result.json"));
            text = text.Replace(context.ContextHash, string.Empty);

            Assert.That(() => MatchContractV2Json.DeserializeResult(Encoding.UTF8.GetBytes(text), context),
                Throws.TypeOf<MatchV2ContractException>());
        }

        private static byte[] ScalarDocument(string value)
        {
            var writer = new MatchV2CanonicalJsonWriter(); writer.StartObject();
            writer.Property("v"); writer.String(value); writer.EndObject(); return writer.Bytes();
        }
    }
}
