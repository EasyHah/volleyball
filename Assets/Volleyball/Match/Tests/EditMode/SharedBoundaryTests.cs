using System;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain;
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
        public void MatchDomain_ReferencesSharedAndAbilityProfileWrapsOnlyDerivedV4Attributes()
        {
            var references = typeof(PlayerAbilityProfile).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var derived = MatchV4TestFixture.CreateDerived();
            var profile = new PlayerAbilityProfile(derived);

            Assert.That(references, Does.Contain("Volleyball.Shared"));
            Assert.That(profile.Derived, Is.SameAs(derived));
            Assert.That(
                typeof(PlayerAbilityProfile).GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(DerivedMatchAttributesV4) }));
            Assert.That(typeof(PlayerAbilityProfile).GetProperty("AttackTechnique"), Is.Null);
            Assert.That(typeof(PlayerAbilityProfile).GetProperty("AttackPower"), Is.Null);
            Assert.That(typeof(PlayerAbilityProfile).GetProperty("MaxAttackReach"), Is.Null);
        }

        [Test]
        public void MatchPlayerBinding_KeepsStableCareerIdentitySeparateFromPrototypeSlot()
        {
            var snapshot = MatchV4TestFixture.CreatePlayer();
            var binding = new MatchPlayerBinding(
                snapshot,
                new Volleyball.Domain.Prototype.PlayerId(
                    PrototypeTeamId.Blue,
                    PrototypePlayerRole.Setter),
                TeamSide.Home,
                rotationPosition: 4);

            Assert.That(binding.StablePlayerId.Value, Is.EqualTo("career-player-0042"));
            Assert.That(binding.Slot.Team, Is.EqualTo(PrototypeTeamId.Blue));
            Assert.That(binding.Slot.Role, Is.EqualTo(PrototypePlayerRole.Setter));
            Assert.That(binding.Side, Is.EqualTo(TeamSide.Home));
            Assert.That(binding.RotationPosition, Is.EqualTo(4));
            Assert.That(binding.DominantHand, Is.EqualTo(DominantHandV4.Right));
            Assert.That(binding.Derived, Is.SameAs(snapshot.Derived));
            Assert.That(typeof(MatchPlayerBinding).GetProperty("Ability"), Is.Null);
        }

        [Test]
        public void MatchPlayerBinding_RejectsInvalidPrototypeSlot()
        {
            var snapshot = MatchV4TestFixture.CreatePlayer();

            Assert.That(
                () => new MatchPlayerBinding(
                    snapshot,
                    new Volleyball.Domain.Prototype.PlayerId(
                        (PrototypeTeamId)99,
                        PrototypePlayerRole.Setter),
                    TeamSide.Home,
                    rotationPosition: 4),
                Throws.TypeOf<System.ArgumentException>());
        }

        [Test]
        public void PhysicalDirector_ExposesOnlyV4FormalInitializationAndResultContracts()
        {
            var publicDeclaredMethods = typeof(PhysicalMatchRallyDirector)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var configure = publicDeclaredMethods.SingleOrDefault(method =>
                method.Name == "ConfigureV3Rules");
            var initialize = publicDeclaredMethods.SingleOrDefault(method =>
                method.Name == "InitializeV4");

            Assert.That(publicDeclaredMethods.Count(method => method.Name == "Initialize"), Is.Zero);
            Assert.That(publicDeclaredMethods.Count(method => method.Name == "InitializeV2"), Is.Zero);
            Assert.That(initialize, Is.Not.Null);
            Assert.That(initialize.GetParameters()[2].ParameterType, Is.EqualTo(typeof(MatchContextV4)));
            Assert.That(configure, Is.Not.Null);
            Assert.That(
                configure.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(V3RulesMode) }));
            Assert.That(
                typeof(PhysicalMatchRallyDirector).GetProperty("MatchContext").PropertyType,
                Is.EqualTo(typeof(MatchContextV4)));
            Assert.That(
                typeof(PhysicalMatchRallyDirector).GetProperty("Result").PropertyType,
                Is.EqualTo(typeof(MatchResultV4)));
            Assert.That(typeof(PhysicalMatchRallyDirector).GetProperty("MatchContextV2"), Is.Null);
            Assert.That(typeof(PhysicalMatchRallyDirector).GetProperty("ResultV2"), Is.Null);
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

        [Test]
        public void PlannedAttackGeometry_IsNeverExposedAsObservedGeometry()
        {
            Assert.That(
                typeof(PhysicalMatchRallyDirector).GetProperty("LastActualAttackContactCenter"),
                Is.Null);
            Assert.That(
                typeof(PhysicalMatchRallyDirector).GetProperty("LastReplannedAttackContactCenter"),
                Is.Not.Null);
            Assert.That(
                typeof(ReplaySetChainEvent).GetProperty("ActualAttackContactCenter"),
                Is.Null);
            Assert.That(
                typeof(ReplaySetChainEvent).GetProperty("ReplannedAttackContactCenter"),
                Is.Not.Null);
        }

        [Test]
        public void FormalMatchBoundary_HasNoLegacyContextResultOrInitializationTypes()
        {
            var legacyTypes = new[]
            {
                typeof(MatchContextV1),
                typeof(MatchContextV2),
                typeof(MatchContextV3),
                typeof(MatchResultV1),
                typeof(MatchResultV2),
                typeof(MatchResultV3),
                typeof(PlayerAbilitySnapshotV1),
                typeof(PlayerAbilitySnapshotV2),
                typeof(PlayerAbilitySnapshotV3)
            };
            var formalTypes = new[]
            {
                typeof(MatchSet),
                typeof(PhysicalMatchRallyDirector),
                typeof(FormalSixVsSixRallyDirector),
                typeof(FormalSixVsSixRallyBootstrap)
            };

            foreach (var type in formalTypes)
            {
                var members = type.GetMembers(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly);
                foreach (var member in members)
                {
                    var exposedTypes = member switch
                    {
                        MethodInfo method => method.GetParameters()
                            .Select(parameter => parameter.ParameterType)
                            .Append(method.ReturnType),
                        ConstructorInfo constructor => constructor.GetParameters()
                            .Select(parameter => parameter.ParameterType),
                        PropertyInfo property => new[] { property.PropertyType },
                        FieldInfo field => new[] { field.FieldType },
                        _ => Array.Empty<Type>()
                    };
                    Assert.That(
                        exposedTypes.Any(candidate => legacyTypes.Contains(candidate)),
                        Is.False,
                        type.Name + "." + member.Name);
                }
            }
        }

        [Test]
        public void V3RulesAdapter_SelectsRulesVersionWithoutOwningALegacyMatchContext()
        {
            var constructor = typeof(FullRallyV3RulesRuntimeAdapter)
                .GetConstructors()
                .Single();

            Assert.That(
                constructor.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[]
                {
                    typeof(int),
                    typeof(Volleyball.Match.Domain.FullRallyV3.OnCourtEligibilitySnapshot),
                    typeof(TeamSide),
                    typeof(V3RulesMode)
                }));
            Assert.That(
                typeof(FullRallyV3RulesRuntimeAdapter)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Select(field => field.FieldType),
                Has.None.EqualTo(typeof(MatchContextV3)));
        }
    }
}
