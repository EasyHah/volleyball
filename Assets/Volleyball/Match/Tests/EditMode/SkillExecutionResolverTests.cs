using System;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Players;

namespace Volleyball.EditModeTests
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
            var low = MatchV4TestFixture.CreateAbility(
                0.7f, 0.7f, 0.7f, 0.15f, 0.7f, 0.7f, 0.7f);
            var high = MatchV4TestFixture.CreateAbility(
                0.7f, 0.7f, 0.7f, 0.95f, 0.7f, 0.7f, 0.7f);
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
        public void PlayerAbilityProfile_RejectsMissingDerivedAttributes()
        {
            Assert.That(
                () => new PlayerAbilityProfile(null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void AttackAuthority_SeparatesControlErrorsFromPowerCapacity()
        {
            var baseline = new PlayerAbilityProfile(
                MatchV4TestFixture.CreateDerived(attackTechnique: 0.4f, attackPower: 0.4f));
            var greaterControl = new PlayerAbilityProfile(
                MatchV4TestFixture.CreateDerived(attackTechnique: 0.9f, attackPower: 0.4f));
            var greaterPower = new PlayerAbilityProfile(
                MatchV4TestFixture.CreateDerived(attackTechnique: 0.4f, attackPower: 0.9f));

            var baselineError = ResolveAttack(baseline);
            var controlError = ResolveAttack(greaterControl);
            var powerError = ResolveAttack(greaterPower);

            Assert.That(
                greaterControl.AttackDirectionControl,
                Is.GreaterThan(baseline.AttackDirectionControl));
            Assert.That(
                greaterControl.AttackSpeedControl,
                Is.GreaterThan(baseline.AttackSpeedControl));
            Assert.That(
                greaterControl.AttackPowerCapacity,
                Is.EqualTo(baseline.AttackPowerCapacity));
            Assert.That(
                controlError.ContactNormalErrorDegrees.Magnitude,
                Is.LessThan(baselineError.ContactNormalErrorDegrees.Magnitude));
            Assert.That(
                Math.Abs(1f - controlError.SurfaceSpeedScale),
                Is.LessThan(Math.Abs(1f - baselineError.SurfaceSpeedScale)));

            Assert.That(
                greaterPower.AttackPowerCapacity,
                Is.GreaterThan(baseline.AttackPowerCapacity));
            Assert.That(
                greaterPower.AttackDirectionControl,
                Is.EqualTo(baseline.AttackDirectionControl));
            Assert.That(
                greaterPower.AttackSpeedControl,
                Is.EqualTo(baseline.AttackSpeedControl));
            Assert.That(powerError, Is.EqualTo(baselineError));
        }

        private static SkillExecutionError ResolveAttack(PlayerAbilityProfile profile)
        {
            return SkillExecutionResolver.Resolve(
                profile,
                TechniqueAction.Attack,
                playerStableId: 42,
                rallyNumber: 3,
                actionIndex: 2,
                seed: 7351,
                difficulty: 1f);
        }
    }
}
