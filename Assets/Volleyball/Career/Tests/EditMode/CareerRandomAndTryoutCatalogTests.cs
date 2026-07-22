using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerRandomAndTryoutCatalogTests
    {
        private const string SeedHex =
            "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";

        [Test]
        public void AlgorithmV1_EncodesAndResolvesTheLockedGoldenVector()
        {
            var random = new CareerDeterministicRandom();
            var request = Request(
                "event",
                1,
                1,
                "event.team_meal",
                "00000000-0000-0000-0000-000000000001");

            Assert.That(
                Hex(random.EncodeHashInput(request, 0)),
                Is.EqualTo(
                    "766f6c6c657962616c6c2d6361726565722d726e6700010100000020" +
                    "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f" +
                    "02000000056576656e74030000000400000001040000000400000001050000000f" +
                    "6576656e742e7465616d5f6d65616c060000002430303030303030302d30303030" +
                    "2d303030302d303030302d30303030303030303030303107000000040000000008" +
                    "0000000400000000"));
            Assert.That(
                Hex(random.ComputeDigest(request, 0)),
                Is.EqualTo("0955c7f662dbf95ea75c1670c4888602c8844c9d01af69f7a3741bc3286104d4"));
            Assert.That(random.NextInt64(request, 0, 100), Is.EqualTo(14));
        }

        [TestCase(
            "tryout", 1, 0, "tryout.attack.choice.power",
            "00000000-0000-0000-0000-000000000002", 201,
            "99908aaaed0e6c837a7fafb7e21796b716ecc384f5c3f7bcb8ac9a0d3e2ab1d4", 39)]
        [TestCase(
            "event", 1, 1, "event.team_meal.option.attend",
            "00000000-0000-0000-0000-000000000003", 10000,
            "498a5ad33f7737a79b2d489870aa5b9c32a287a44c5af6d94bad45877cd9de8d", 6791)]
        [TestCase(
            "match_seed", 1, 1, "schedule.u1w1.match.01",
            "00000000-0000-0000-0000-000000000004", 4294967296L,
            "9983cd3901876225ebe7e0fec528093f18a90471be144c6c4bc1d43aaa896b7d", 25649701)]
        public void AlgorithmV1_ResolvesEveryRegisteredCallFixture(
            string stream,
            int season,
            int week,
            string entityId,
            string occurrence,
            long maximum,
            string digest,
            long expected)
        {
            var random = new CareerDeterministicRandom();
            var request = Request(stream, season, week, entityId, occurrence);

            Assert.That(Hex(random.ComputeDigest(request, 0)), Is.EqualTo(digest));
            Assert.That(random.NextInt64(request, 0, maximum), Is.EqualTo(expected));
        }

        [Test]
        public void RejectionMapper_CoversDivisorBoundariesAndIncrementsAttemptOnce()
        {
            var source = new SequenceDigestSource(
                0,
                0,
                ulong.MaxValue,
                5);
            var random = new CareerDeterministicRandom(source);
            var request = Request(
                "event",
                1,
                1,
                "event.team_meal",
                "00000000-0000-0000-0000-000000000001");

            Assert.That(random.NextInt64(request, 9, 10), Is.EqualTo(9));
            Assert.That(random.NextInt64(request, 0, 4294967296L), Is.EqualTo(0));
            Assert.That(random.NextInt64(request, 0, 3), Is.EqualTo(2));
            Assert.That(source.Attempts, Is.EqualTo(new uint[] { 0, 0, 0, 1 }));
        }

        [Test]
        public void AlgorithmV1_RejectsInvalidKeysVersionsAttemptsAndRanges()
        {
            Assert.That(
                () => Request("", 1, 1, "entity", ValidOccurrence),
                Throws.ArgumentException);
            Assert.That(
                () => Request("unknown", 1, 1, "entity", ValidOccurrence),
                Throws.ArgumentException);
            Assert.That(
                () => Request("event", 1, 1, "", ValidOccurrence),
                Throws.ArgumentException);
            Assert.That(
                () => Request("event", 1, 1, "bad\ud800", ValidOccurrence),
                Throws.ArgumentException);
            Assert.That(
                () => Request("event", 1, 0, "entity", ValidOccurrence),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => Request("event", 0, 1, "entity", ValidOccurrence),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => Request("tryout", 1, 1, "entity", ValidOccurrence),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new CareerRandomRequest(
                    2,
                    CareerSeed.Parse(SeedHex),
                    "event",
                    1,
                    1,
                    "entity",
                    new OccurrenceId(Guid.Parse(ValidOccurrence)),
                    0),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new CareerRandomRequest(
                    1,
                    CareerSeed.Parse(SeedHex),
                    "event",
                    1,
                    1,
                    "entity",
                    new OccurrenceId(Guid.Parse(ValidOccurrence)),
                    4294967296L),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new CareerRandomRequest(
                    1,
                    CareerSeed.Parse(SeedHex),
                    "event",
                    1,
                    1,
                    "entity",
                    default(OccurrenceId),
                    0),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new CareerRandomRequest(
                    1,
                    CareerSeed.Parse(SeedHex),
                    "event",
                    1,
                    1,
                    "entity",
                    new OccurrenceId(Guid.Parse(ValidOccurrence)),
                    -1),
                Throws.InstanceOf<ArgumentException>());

            var random = new CareerDeterministicRandom();
            var valid = Request("event", 1, 1, "entity", ValidOccurrence);
            Assert.That(
                () => random.EncodeHashInput(valid, -1),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => random.EncodeHashInput(valid, 4294967296L),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => random.NextInt64(valid, 1, 1),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => random.NextInt64(valid, 0, 4294967297L),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void CatalogV1_HasExactOrderedStableIdsValuesAndDefensiveCopies()
        {
            var catalog = TryoutCatalogV1.Create();

            Assert.That(catalog.ContentVersion, Is.EqualTo(1));
            Assert.That(catalog.RulesetVersion, Is.EqualTo(1));
            Assert.That(catalog.InitialTeamStableId, Is.EqualTo("team.university.first"));
            Assert.That(
                StageIds(catalog),
                Is.EqualTo(new[]
                {
                    "tryout.attack",
                    "tryout.reception_defense",
                    "tryout.scrimmage"
                }));
            Assert.That(
                ChoiceIds(catalog.Stages[0]),
                Is.EqualTo(new[]
                {
                    "tryout.attack.choice.power",
                    "tryout.attack.choice.serve",
                    "tryout.attack.choice.approach"
                }));
            Assert.That(
                OutputIds(catalog.Stages[0]),
                Is.EqualTo(new[]
                {
                    "tryout.output.spike",
                    "tryout.output.serve",
                    "tryout.output.jump"
                }));
            Assert.That(catalog.Stages[0].Choices[0].BaseValues, Is.EqualTo(new[] { 5800, 4800, 5600 }));
            Assert.That(catalog.Stages[0].Choices[1].BaseValues, Is.EqualTo(new[] { 5000, 5800, 5100 }));
            Assert.That(catalog.Stages[0].Choices[2].BaseValues, Is.EqualTo(new[] { 5400, 5100, 5400 }));

            Assert.That(
                ChoiceIds(catalog.Stages[1]),
                Is.EqualTo(new[]
                {
                    "tryout.reception_defense.choice.first_touch",
                    "tryout.reception_defense.choice.floor_defense",
                    "tryout.reception_defense.choice.net_read"
                }));
            Assert.That(
                OutputIds(catalog.Stages[1]),
                Is.EqualTo(new[]
                {
                    "tryout.output.reception",
                    "tryout.output.defense",
                    "tryout.output.block",
                    "tryout.output.movement"
                }));
            Assert.That(catalog.Stages[1].Choices[0].BaseValues, Is.EqualTo(new[] { 5800, 5200, 4600, 5300 }));
            Assert.That(catalog.Stages[1].Choices[1].BaseValues, Is.EqualTo(new[] { 5100, 5800, 4600, 5500 }));
            Assert.That(catalog.Stages[1].Choices[2].BaseValues, Is.EqualTo(new[] { 5000, 5100, 5700, 5400 }));

            Assert.That(
                ChoiceIds(catalog.Stages[2]),
                Is.EqualTo(new[]
                {
                    "tryout.scrimmage.choice.endurance",
                    "tryout.scrimmage.choice.composure",
                    "tryout.scrimmage.choice.initiative"
                }));
            Assert.That(
                OutputIds(catalog.Stages[2]),
                Is.EqualTo(new[]
                {
                    "tryout.output.stamina",
                    "tryout.output.fatigue",
                    "tryout.output.mindset",
                    "tryout.output.coach_trust"
                }));
            Assert.That(catalog.Stages[2].Choices[0].BaseValues, Is.EqualTo(new[] { 5800, 8, 52, 48 }));
            Assert.That(catalog.Stages[2].Choices[1].BaseValues, Is.EqualTo(new[] { 5200, 10, 60, 56 }));
            Assert.That(catalog.Stages[2].Choices[2].BaseValues, Is.EqualTo(new[] { 5400, 14, 56, 60 }));

            Assert.That(
                () => ((IList<TryoutStageDefinition>)catalog.Stages)[0] = catalog.Stages[1],
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IList<int>)catalog.Stages[0].Choices[0].BaseValues)[0] = 0,
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void CatalogV1_RejectsUnsupportedVersionsAndWrongStageOutputSemantics()
        {
            var valid = TryoutCatalogV1.Create();
            Assert.That(
                () => new TryoutCatalog(
                    2,
                    1,
                    valid.InitialTeamStableId,
                    valid.Stages),
                Throws.InstanceOf<ArgumentException>());

            var invalidStages = new[]
            {
                new TryoutStageDefinition(
                    1,
                    valid.Stages[0].StageId,
                    new[]
                    {
                        new TryoutOutputDefinition(
                            "tryout.output.spike",
                            TryoutOutputKind.Stamina),
                        valid.Stages[0].Outputs[1],
                        valid.Stages[0].Outputs[2]
                    },
                    valid.Stages[0].Choices),
                valid.Stages[1],
                valid.Stages[2]
            };
            Assert.That(
                () => new TryoutCatalog(
                    1,
                    1,
                    valid.InitialTeamStableId,
                    invalidStages),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void CareerPlayerRecord_HasTheDerivedFixedOutsideHitterPosition()
        {
            var progress = new CareerAttributeProgress(5000, 0);
            var player = new CareerPlayerRecord(
                new PlayerId("player.one"),
                "Player One",
                7,
                new CareerPlayerAttributes(
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress,
                    progress));

            Assert.That(player.Position, Is.EqualTo(CareerPlayerPosition.OutsideHitter));
        }

        [Test]
        public void DomainAndApplication_HaveNoEngineOrOrderDependentRandomReferences()
        {
            var assemblies = new[]
            {
                typeof(CareerPlayerRecord).Assembly,
                typeof(CareerOnboardingService).Assembly
            };
            foreach (var assembly in assemblies)
            {
                var references = assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
                Assert.That(references, Does.Not.Contain("UnityEngine.CoreModule"));
            }

            var runtime = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Volleyball",
                "Career",
                "Runtime");
            foreach (var layer in new[] { "Domain", "Application" })
            {
                foreach (var file in Directory.GetFiles(
                    Path.Combine(runtime, layer),
                    "*.cs",
                    SearchOption.AllDirectories))
                {
                    var source = File.ReadAllText(file);
                    Assert.That(source, Does.Not.Contain("System.Random"), file);
                    Assert.That(source, Does.Not.Contain("UnityEngine.Random"), file);
                }
            }
        }

        private const string ValidOccurrence = "00000000-0000-0000-0000-000000000001";

        private static CareerRandomRequest Request(
            string stream,
            int season,
            int week,
            string entityId,
            string occurrence)
        {
            return new CareerRandomRequest(
                1,
                CareerSeed.Parse(SeedHex),
                stream,
                season,
                week,
                entityId,
                new OccurrenceId(Guid.Parse(occurrence)),
                0);
        }

        private static string[] StageIds(TryoutCatalog catalog)
        {
            var result = new string[catalog.Stages.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = catalog.Stages[index].StageId;
            }

            return result;
        }

        private static string[] ChoiceIds(TryoutStageDefinition stage)
        {
            var result = new string[stage.Choices.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = stage.Choices[index].ChoiceId;
            }

            return result;
        }

        private static string[] OutputIds(TryoutStageDefinition stage)
        {
            var result = new string[stage.Outputs.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = stage.Outputs[index].OutputId;
            }

            return result;
        }

        private static string Hex(byte[] bytes)
        {
            var characters = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (var index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = alphabet[bytes[index] >> 4];
                characters[(index * 2) + 1] = alphabet[bytes[index] & 15];
            }

            return new string(characters);
        }

        private sealed class SequenceDigestSource : ICareerRandomDigestSource
        {
            private readonly Queue<ulong> _values;
            private readonly List<uint> _attempts = new List<uint>();

            public SequenceDigestSource(params ulong[] values)
            {
                _values = new Queue<ulong>(values);
            }

            public IReadOnlyList<uint> Attempts => _attempts;

            public byte[] ComputeDigest(byte[] hashInput)
            {
                var attemptOffset = hashInput.Length - 4;
                _attempts.Add(
                    ((uint)hashInput[attemptOffset] << 24) |
                    ((uint)hashInput[attemptOffset + 1] << 16) |
                    ((uint)hashInput[attemptOffset + 2] << 8) |
                    hashInput[attemptOffset + 3]);
                var value = _values.Dequeue();
                var digest = new byte[32];
                for (var index = 7; index >= 0; index--)
                {
                    digest[index] = (byte)value;
                    value >>= 8;
                }

                return digest;
            }
        }
    }
}
