using System.Reflection;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Players;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using PrototypePlayerRole = Volleyball.Domain.Prototype.PlayerRole;
using PrototypeTeamId = Volleyball.Domain.Prototype.TeamId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class SharedBoundaryTests
    {
        [Test]
        public void SharedAssembly_DoesNotReferenceMatchAssemblies()
        {
            var references = typeof(MatchContextV1).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Volleyball.Match.Domain"));
            Assert.That(references, Does.Not.Contain("Volleyball.Match.AI"));
            Assert.That(references, Does.Not.Contain("Volleyball.Match.Presentation"));
        }

        [Test]
        public void MatchDomain_ReferencesSharedAndAbilitySnapshotRoundTrips()
        {
            var references = typeof(PlayerAbilityProfile).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var original = new PlayerAbilityProfile(
                0.71f,
                0.72f,
                0.73f,
                0.74f,
                0.75f,
                0.76f,
                0.77f);

            var restored = new PlayerAbilityProfile(original.ToSnapshot());

            Assert.That(references, Does.Contain("Volleyball.Shared"));
            Assert.That(restored.Mobility, Is.EqualTo(original.Mobility));
            Assert.That(restored.Reaction, Is.EqualTo(original.Reaction));
            Assert.That(restored.Jump, Is.EqualTo(original.Jump));
            Assert.That(restored.ReceiveTechnique, Is.EqualTo(original.ReceiveTechnique));
            Assert.That(restored.SetTechnique, Is.EqualTo(original.SetTechnique));
            Assert.That(restored.AttackTechnique, Is.EqualTo(original.AttackTechnique));
            Assert.That(restored.AttackPower, Is.EqualTo(original.AttackPower));
        }

        [Test]
        public void MatchPlayerBinding_KeepsStableCareerIdentitySeparateFromPrototypeSlot()
        {
            var snapshot = new PlayerAbilitySnapshotV1(
                0.71f,
                0.72f,
                0.73f,
                0.74f,
                0.75f,
                0.76f,
                0.77f);
            var binding = new MatchPlayerBinding(
                new StablePlayerId("career-player-0042"),
                new Volleyball.Domain.Prototype.PlayerId(
                    PrototypeTeamId.Blue,
                    PrototypePlayerRole.Setter),
                snapshot);

            Assert.That(binding.StablePlayerId.Value, Is.EqualTo("career-player-0042"));
            Assert.That(binding.Slot.Team, Is.EqualTo(PrototypeTeamId.Blue));
            Assert.That(binding.Slot.Role, Is.EqualTo(PrototypePlayerRole.Setter));
            Assert.That(binding.Ability.SetTechnique, Is.EqualTo(0.75f));
        }

        [Test]
        public void PlayerAbilityProfile_ProjectsV1ToDeterministicSafeReachAndPreservesV2Reach()
        {
            var legacy = new PlayerAbilityProfile(new PlayerAbilitySnapshotV1(
                0.7f, 0.7f, 0.7f, 0.7f, 0.7f, 0.7f, 0.7f));
            var v2 = new PlayerAbilityProfile(new PlayerAbilitySnapshotV2(
                0.7f, 0.7f, 0.7f, 0.7f, 0.7f, 0.7f, 0.7f, 3.42f));

            Assert.That(legacy.MaxAttackReach, Is.EqualTo(3.20f));
            Assert.That(v2.MaxAttackReach, Is.EqualTo(3.42f));
            Assert.That(v2.ToSnapshotV2().MaxAttackReach, Is.EqualTo(3.42f));
        }

        [Test]
        public void MatchPlayerBinding_RejectsInvalidPrototypeSlot()
        {
            var snapshot = new PlayerAbilitySnapshotV1(
                0.7f,
                0.7f,
                0.7f,
                0.7f,
                0.7f,
                0.7f,
                0.7f);

            Assert.That(
                () => new MatchPlayerBinding(
                    new StablePlayerId("career-player-0042"),
                    new Volleyball.Domain.Prototype.PlayerId(
                        (PrototypeTeamId)99,
                        PrototypePlayerRole.Setter),
                    snapshot),
                Throws.TypeOf<System.ArgumentException>());
        }

        [Test]
        public void PhysicalDirector_AddsDistinctV3ConfigurationWithoutChangingInitializeApis()
        {
            var publicDeclaredMethods = typeof(PhysicalMatchRallyDirector)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var configure = publicDeclaredMethods.SingleOrDefault(method =>
                method.Name == "ConfigureV3Rules");

            Assert.That(publicDeclaredMethods.Count(method => method.Name == "Initialize"), Is.EqualTo(1));
            Assert.That(publicDeclaredMethods.Count(method => method.Name == "InitializeV2"), Is.EqualTo(1));
            Assert.That(configure, Is.Not.Null);
            Assert.That(
                configure.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(MatchContextV3), typeof(V3RulesMode) }));
        }

        [Test]
        public void PhysicalDirector_ExposesReadOnlyV3ShadowDiagnostics()
        {
            var expectedProperties = new[]
            {
                "V3RulesMode",
                "V3RuleTransitions",
                "V3RuleParityMatches",
                "V3RuleIntentionalCorrections",
                "V3RuleUnexpectedMismatches",
                "LastV3RuleDiagnostic"
            };

            foreach (var propertyName in expectedProperties)
            {
                var property = typeof(PhysicalMatchRallyDirector).GetProperty(propertyName);

                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(property.GetMethod, Is.Not.Null, propertyName);
                Assert.That(property.GetSetMethod(false), Is.Null, propertyName);
            }
        }
    }
}
