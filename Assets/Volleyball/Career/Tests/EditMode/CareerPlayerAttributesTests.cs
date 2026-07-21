using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerPlayerAttributesTests
    {
        [Test]
        public void AttributeKinds_ExposeExactlyTheEightCareerAbilities()
        {
            Assert.That(
                (CareerAttributeKind[])Enum.GetValues(typeof(CareerAttributeKind)),
                Is.EqualTo(
                    new[]
                    {
                        CareerAttributeKind.Spike,
                        CareerAttributeKind.Serve,
                        CareerAttributeKind.Reception,
                        CareerAttributeKind.Defense,
                        CareerAttributeKind.Block,
                        CareerAttributeKind.Movement,
                        CareerAttributeKind.Jump,
                        CareerAttributeKind.Stamina
                    }));
        }

        [Test]
        public void Attributes_ExposeEveryExplicitValueAndGetByKind()
        {
            var attributes = CreateAttributes();

            Assert.That(attributes.Spike, Is.EqualTo(new CareerAttributeProgress(1000, 1)));
            Assert.That(attributes.Serve, Is.EqualTo(new CareerAttributeProgress(2000, 2)));
            Assert.That(attributes.Reception, Is.EqualTo(new CareerAttributeProgress(3000, 3)));
            Assert.That(attributes.Defense, Is.EqualTo(new CareerAttributeProgress(4000, 4)));
            Assert.That(attributes.Block, Is.EqualTo(new CareerAttributeProgress(5000, 5)));
            Assert.That(attributes.Movement, Is.EqualTo(new CareerAttributeProgress(6000, 6)));
            Assert.That(attributes.Jump, Is.EqualTo(new CareerAttributeProgress(7000, 7)));
            Assert.That(attributes.Stamina, Is.EqualTo(new CareerAttributeProgress(8000, 8)));

            foreach (CareerAttributeKind kind in Enum.GetValues(typeof(CareerAttributeKind)))
            {
                Assert.That(attributes.Get(kind), Is.EqualTo(ProgressFor(kind)));
            }

            Assert.That(
                () => attributes.Get((CareerAttributeKind)99),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0, 0L)]
        [TestCase(10000, 9007199254740991L)]
        public void Progress_AcceptsInclusiveStorageBoundaries(int abilityBasisPoints, long growthExperience)
        {
            var progress = new CareerAttributeProgress(abilityBasisPoints, growthExperience);

            Assert.That(progress.AbilityBasisPoints, Is.EqualTo(abilityBasisPoints));
            Assert.That(progress.GrowthExperience, Is.EqualTo(growthExperience));
        }

        [TestCase(-1)]
        [TestCase(10001)]
        public void Progress_RejectsAbilityOutsideBasisPointRange(int abilityBasisPoints)
        {
            Assert.That(
                () => new CareerAttributeProgress(abilityBasisPoints, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0, 1)]
        [TestCase(49, 1)]
        [TestCase(50, 1)]
        [TestCase(149, 1)]
        [TestCase(150, 2)]
        [TestCase(9949, 99)]
        [TestCase(9950, 100)]
        [TestCase(10000, 100)]
        public void DisplayValue_RoundsThenClampsToOneThroughOneHundred(
            int abilityBasisPoints,
            int expectedDisplayValue)
        {
            var progress = new CareerAttributeProgress(abilityBasisPoints, 0);

            Assert.That(progress.DisplayValue, Is.EqualTo(expectedDisplayValue));
        }

        [Test]
        public void GrowthExperience_RejectsNegativeAndSafeIntegerOverflow()
        {
            Assert.That(
                () => new CareerAttributeProgress(5000, -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            var progress = new CareerAttributeProgress(
                5000,
                CareerAttributeProgress.MaximumGrowthExperience);
            Assert.That(
                () => progress.AddGrowthExperience(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => progress.AddGrowthExperience(1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => progress.AddGrowthExperience(long.MaxValue),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void AddGrowthExperience_ReturnsANewCheckedValueWithoutChangingAbility()
        {
            var original = new CareerAttributeProgress(4321, 10);

            var updated = original.AddGrowthExperience(25);

            Assert.That(updated, Is.EqualTo(new CareerAttributeProgress(4321, 35)));
            Assert.That(original, Is.EqualTo(new CareerAttributeProgress(4321, 10)));
        }

        [Test]
        public void Attributes_UseDeepValueEquality()
        {
            var first = CreateAttributes();
            var equal = CreateAttributes();
            var different = new CareerPlayerAttributes(
                new CareerAttributeProgress(1001, 1),
                equal.Serve,
                equal.Reception,
                equal.Defense,
                equal.Block,
                equal.Movement,
                equal.Jump,
                equal.Stamina);

            Assert.That(first, Is.Not.SameAs(equal));
            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first.Equals(null), Is.False);
        }

        [Test]
        public void StableIdentifiers_RejectEmptyGuidsAndUseDFormatValueEquality()
        {
            var value = Guid.Parse("9ef1dc4a-1e95-46ef-9234-b2f4cdb409cf");

            AssertGuidIdentifier(new ProfileId(value), new ProfileId(value), value);
            AssertGuidIdentifier(new SaveId(value), new SaveId(value), value);
            AssertGuidIdentifier(new LineageId(value), new LineageId(value), value);
            AssertGuidIdentifier(new OperationId(value), new OperationId(value), value);
            Assert.That(() => new ProfileId(Guid.Empty), Throws.ArgumentException);
            Assert.That(() => new SaveId(Guid.Empty), Throws.ArgumentException);
            Assert.That(() => new LineageId(Guid.Empty), Throws.ArgumentException);
            Assert.That(() => new OperationId(Guid.Empty), Throws.ArgumentException);
        }

        [Test]
        public void CareerSeed_IsExactlyThirtyTwoBytesAndDefensivelyCopiesData()
        {
            var source = Enumerable.Range(0, CareerSeed.ByteLength).Select(value => (byte)value).ToArray();
            var seed = new CareerSeed(source);
            source[0] = 255;

            var firstCopy = seed.ToBytes();
            firstCopy[1] = 255;
            var secondCopy = seed.ToBytes();

            Assert.That(secondCopy[0], Is.Zero);
            Assert.That(secondCopy[1], Is.EqualTo(1));
            Assert.That(seed.ToHex(), Is.EqualTo("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"));
            Assert.That(CareerSeed.Parse(seed.ToHex()), Is.EqualTo(seed));
            Assert.That(() => new CareerSeed(null), Throws.ArgumentNullException);
            Assert.That(() => new CareerSeed(new byte[31]), Throws.ArgumentException);
            Assert.That(() => new CareerSeed(new byte[33]), Throws.ArgumentException);
            Assert.That(() => CareerSeed.Parse(new string('0', 63)), Throws.ArgumentException);
            Assert.That(() => CareerSeed.Parse(new string('A', 64)), Throws.ArgumentException);
        }

        [Test]
        public void Sha256Digest_AcceptsOnlySixtyFourLowercaseHexCharacters()
        {
            var value = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

            var first = Sha256Digest.Parse(value);
            var equal = new Sha256Digest(value);

            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.ToString(), Is.EqualTo(value));
            Assert.That(first.Value, Is.EqualTo(value));
            Assert.That(() => new Sha256Digest(null), Throws.ArgumentNullException);
            Assert.That(() => new Sha256Digest(new string('0', 63)), Throws.ArgumentException);
            Assert.That(() => new Sha256Digest(new string('A', 64)), Throws.ArgumentException);
            Assert.That(() => new Sha256Digest(new string('g', 64)), Throws.ArgumentException);
        }

        [Test]
        public void CareerPlayerRecord_OwnsCareerAttributesAndNoLongerExposesSharedV1Ability()
        {
            var playerId = new PlayerId("career-player-1");
            var attributes = CreateAttributes();

            var player = new CareerPlayerRecord(playerId, "Lin", 18, attributes);

            Assert.That(player.PlayerId, Is.EqualTo(playerId));
            Assert.That(player.DisplayName, Is.EqualTo("Lin"));
            Assert.That(player.JerseyNumber, Is.EqualTo(18));
            Assert.That(player.Attributes, Is.SameAs(attributes));
            Assert.That(typeof(CareerPlayerRecord).GetProperty("Ability"), Is.Null);
            Assert.That(
                typeof(CareerPlayerRecord)
                    .GetMembers()
                    .SelectMany(member =>
                    {
                        if (member is System.Reflection.PropertyInfo property)
                        {
                            return new[] { property.PropertyType };
                        }

                        if (member is System.Reflection.ConstructorInfo constructor)
                        {
                            return constructor.GetParameters().Select(parameter => parameter.ParameterType);
                        }

                        return Array.Empty<Type>();
                    }),
                Has.None.EqualTo(typeof(PlayerAbilitySnapshotV1)));
        }

        [Test]
        public void CareerPlayerRecord_RejectsInvalidIdentityNameJerseyAndAttributes()
        {
            var playerId = new PlayerId("career-player-1");
            var attributes = CreateAttributes();

            Assert.That(
                () => new CareerPlayerRecord(default, "Lin", 18, attributes),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerPlayerRecord(playerId, " ", 18, attributes),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerPlayerRecord(playerId, "Lin", 0, attributes),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new CareerPlayerRecord(playerId, "Lin", 100, attributes),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new CareerPlayerRecord(playerId, "Lin", 18, null),
                Throws.ArgumentNullException);
        }

        private static CareerPlayerAttributes CreateAttributes()
        {
            return new CareerPlayerAttributes(
                new CareerAttributeProgress(1000, 1),
                new CareerAttributeProgress(2000, 2),
                new CareerAttributeProgress(3000, 3),
                new CareerAttributeProgress(4000, 4),
                new CareerAttributeProgress(5000, 5),
                new CareerAttributeProgress(6000, 6),
                new CareerAttributeProgress(7000, 7),
                new CareerAttributeProgress(8000, 8));
        }

        private static CareerAttributeProgress ProgressFor(CareerAttributeKind kind)
        {
            var ordinal = (int)kind + 1;
            return new CareerAttributeProgress(ordinal * 1000, ordinal);
        }

        private static void AssertGuidIdentifier<T>(T first, T equal, Guid value)
        {
            Assert.That(first, Is.EqualTo(equal));
            Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(first.ToString(), Is.EqualTo(value.ToString("D")));
        }
    }
}
