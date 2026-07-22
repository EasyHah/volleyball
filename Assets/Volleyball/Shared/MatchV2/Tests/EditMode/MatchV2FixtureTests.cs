using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Volleyball.Shared.Contracts;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Shared.MatchV2.EditModeTests
{
    public sealed class MatchV2FixtureTests
    {
        private const string V1 = "Assets/Volleyball/Shared/MatchV2/Fixtures/LegacyV1/physical-3v3-v1";
        private const string V2 = "Assets/Volleyball/Shared/MatchV2/Fixtures/V2/career-u1w1-6v6-v1";
        private const string V1ContextHash = "c15741e3e6509130e9249a6ccaf9f9ef05f9593403571c6feb57b33f6e9291da";
        private const string V1ContextFileHash = "c042a2af98d9dbcaa5b455924e9e8bb1ca6ea2b9a46a06a4c86efe890bd4d646";
        private const string V1ResultFileHash = "f1de13c7e040fd454875cbcef6545d1c40f242dd0063f64682a190a2fa23af8a";
        private const string V2ContextHash = "da570cff972d280acb9307edb715bcef88a0f958e75ea615072a5be25edf0527";
        private const string V2ResultHash = "3fbb03380ce766a7695c7ad7d0697c5c631893d714b229abcf8c7c1017182d98";
        private const string V2ContextFileHash = "a33aefaef5860e68803fa0d3910638da661e777704d9981e3ffd910719126b93";
        private const string V2ResultFileHash = "301df25404a1358f7a56fdc22008f9f7515b3954e75296c3cf5ffe92a959ad12";

        [Test]
        public void LegacyPhysical3v3Fixture_RoundTripsWithFrozenV1Only()
        {
            var contextBytes = CanonicalFixture(V1 + "/context.json");
            var resultBytes = CanonicalFixture(V1 + "/result.json");
            Assert.That(contextBytes, Has.Length.EqualTo(1665));
            Assert.That(resultBytes, Has.Length.EqualTo(711));
            Assert.That(Hash(contextBytes), Is.EqualTo(V1ContextFileHash));
            Assert.That(Hash(resultBytes), Is.EqualTo(V1ResultFileHash));

            var contextJson = Encoding.UTF8.GetString(contextBytes);
            var resultJson = Encoding.UTF8.GetString(resultBytes);
            var context = ContractJson.DeserializeContext(contextJson);
            var result = ContractJson.DeserializeResult(resultJson);

            Assert.That(ContractJson.Serialize(context), Is.EqualTo(contextJson));
            Assert.That(ContractJson.Serialize(result), Is.EqualTo(resultJson));
            Assert.That(context.ContextHash, Is.EqualTo(V1ContextHash));
            Assert.That(context.SessionId, Is.EqualTo(Guid.Parse("22222222-2222-2222-2222-222222222222")));
            Assert.That(context.Seed, Is.EqualTo(7351));
            Assert.That(context.Home.TeamId, Is.EqualTo(new TeamId("sandbox-home")));
            Assert.That(context.Away.TeamId, Is.EqualTo(new TeamId("sandbox-away")));
            Assert.That(context.Home.Players.Select(player => player.PlayerId.Value),
                Is.EqualTo(new[] { "home-setter", "home-attacker", "home-defender" }));
            Assert.That(context.Away.Players.Select(player => player.PlayerId.Value),
                Is.EqualTo(new[] { "away-setter", "away-attacker", "away-defender" }));
            Assert.That(context.Home.Players.All(player => player.Ability.AttackPower == 0.85f), Is.True);
            Assert.DoesNotThrow(() => result.ValidateAgainst(context));
            Assert.That(ContractVersions.SupportsMatch(2), Is.False);
        }

        [Test]
        public void Career6v6Fixture_LocksCanonicalBytesHashesAndTwelveOrderedFacts()
        {
            var contextBytes = CanonicalFixture(V2 + "/golden-context.json");
            var resultBytes = CanonicalFixture(V2 + "/golden-result.json");
            Assert.That(contextBytes, Has.Length.EqualTo(4820));
            Assert.That(resultBytes, Has.Length.EqualTo(8082));
            Assert.That(Hash(contextBytes), Is.EqualTo(V2ContextFileHash));
            Assert.That(Hash(resultBytes), Is.EqualTo(V2ResultFileHash));

            var context = MatchContractV2Json.DeserializeContext(contextBytes);
            var result = MatchContractV2Json.DeserializeResult(resultBytes, context);

            Assert.That(MatchContractV2Json.SerializeContext(context), Is.EqualTo(contextBytes));
            Assert.That(MatchContractV2Json.SerializeResult(result), Is.EqualTo(resultBytes));
            Assert.That(context.ContextHash, Is.EqualTo(V2ContextHash));
            Assert.That(result.ResultHash, Is.EqualTo(V2ResultHash));
            Assert.That(result.RallyCount, Is.EqualTo(46));
            Assert.That(result.Sets.Single().HomePoints, Is.EqualTo(25));
            Assert.That(result.Sets.Single().AwayPoints, Is.EqualTo(21));
            Assert.That(result.PlayerFacts, Has.Count.EqualTo(12));
            Assert.That(result.PlayerFacts.Select(fact => fact.PlayerId).Distinct().ToArray(), Has.Length.EqualTo(12));
            Assert.That(result.PlayerFacts.Select(fact => fact.PlayerId.Value),
                Is.EqualTo(context.Teams.SelectMany(team => team.Players).Select(player => player.PlayerId.Value)));
            var protagonists = result.PlayerFacts.Where(fact => fact.PlayerId.Value == "player.career.protagonist").ToArray();
            Assert.That(protagonists, Has.Length.EqualTo(1));
            var protagonist = protagonists[0];
            Assert.That(protagonist.Spike.Attempts, Is.EqualTo(12));
            Assert.That(protagonist.Serve.Attempts, Is.EqualTo(5));
            Assert.That(protagonist.Reception.Attempts, Is.EqualTo(8));
            Assert.That(protagonist.Defense.Successes, Is.EqualTo(4));
            Assert.That(protagonist.Block.EffectiveTouches, Is.EqualTo(2));
            Assert.That(protagonist.Load.MovementDistanceMillimeters, Is.EqualTo(254000));
            Assert.That(protagonist.Stability.ErrorStreakEpisodes, Is.EqualTo(1));
        }

        internal static byte[] CanonicalFixture(string relativePath)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
            Assert.That(bytes, Is.Not.Empty);
            Assert.That(bytes[bytes.Length - 1], Is.EqualTo((byte)'\n'), "Repository text carrier must have exactly one LF terminator.");
            Assert.That(bytes.Length < 2 || bytes[bytes.Length - 2] != (byte)'\r');
            var canonical = new byte[bytes.Length - 1];
            Buffer.BlockCopy(bytes, 0, canonical, 0, canonical.Length);
            return canonical;
        }

        internal static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }
    }
}
