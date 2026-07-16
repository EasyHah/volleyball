using NUnit.Framework;
using VolleyballMatch.AI;
using VolleyballMatch.Domain.Players;

namespace VolleyballMatch.EditModeTests
{
    public sealed class SkillExecutionResolverTests
    {
        [Test]
        public void Resolve_ReplaysExactlyForSameIdentityAndSeed()
        {
            var profile = PlayerAbilityProfile.Default;

            var first = SkillExecutionResolver.Resolve(profile, TechniqueAction.Receive, 4, 8, 2, 12345, 0.7f);
            var second = SkillExecutionResolver.Resolve(profile, TechniqueAction.Receive, 4, 8, 2, 12345, 0.7f);
            var changedSeed = SkillExecutionResolver.Resolve(profile, TechniqueAction.Receive, 4, 8, 2, 54321, 0.7f);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(changedSeed, Is.Not.EqualTo(first));
        }

        [Test]
        public void Resolve_HighTechniqueProducesSmallerVisibleExecutionErrors()
        {
            var low = new PlayerAbilityProfile(0.7f, 0.7f, 0.7f, 0.15f, 0.7f, 0.7f, 0.7f);
            var high = new PlayerAbilityProfile(0.7f, 0.7f, 0.7f, 0.95f, 0.7f, 0.7f, 0.7f);
            var lowError = 0f;
            var highError = 0f;

            for (var seed = 0; seed < 64; seed++)
            {
                lowError += SkillExecutionResolver.Resolve(low, TechniqueAction.Receive, 1, 1, 1, seed, 1f).Magnitude;
                highError += SkillExecutionResolver.Resolve(high, TechniqueAction.Receive, 1, 1, 1, seed, 1f).Magnitude;
            }

            Assert.That(highError, Is.LessThan(lowError * 0.4f));
        }

        [Test]
        public void PlayerAbilityProfile_RejectsValuesOutsideUnitRange()
        {
            Assert.That(
                () => new PlayerAbilityProfile(1.1f, 1f, 1f, 1f, 1f, 1f, 1f),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
