using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Bootstrap;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Presentation;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class ModuleBoundaryTests
    {
        [Test]
        public void RuntimeAssemblies_FollowTheOneWayModuleDependencyGraph()
        {
            AssertReferences(typeof(CareerPlayerRecord), "Volleyball.Shared");
            AssertDoesNotReference(typeof(CareerPlayerRecord), "Volleyball.Match.Domain");
            AssertDoesNotReference(typeof(CareerPlayerRecord), "Volleyball.Match.Presentation");

            AssertReferences(typeof(CareerMatchRequest), "Volleyball.Shared");
            AssertDoesNotReference(typeof(CareerMatchRequest), "Volleyball.Career.Domain");
            AssertDoesNotReference(typeof(CareerMatchRequest), "Volleyball.Match.Domain");

            AssertReferences(typeof(CareerPresentationModule), "Volleyball.Career.Application");
            AssertDoesNotReference(typeof(ThreeVsThreeRallyBootstrap), "Volleyball.Career.Domain");

            AssertReferences(typeof(GameBootstrapModule), "Volleyball.Match.Presentation");
            AssertReferences(typeof(GameBootstrapModule), "Volleyball.Career.Presentation");
        }

        [Test]
        public void CareerPlayerBoundary_ExposesOnlyNativeV4Attributes()
        {
            var playerType = typeof(CareerPlayerRecord);
            var properties = playerType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(
                properties.Select(property => property.Name),
                Is.EquivalentTo(new[]
                {
                    "PlayerId", "Physical", "Technical", "DominantHand"
                }));
            Assert.That(
                playerType.GetProperty("PlayerId")?.PropertyType,
                Is.EqualTo(typeof(string)));
            Assert.That(
                playerType.GetProperty("Physical")?.PropertyType,
                Is.EqualTo(typeof(PhysicalBaseAttributesV4)));
            Assert.That(
                playerType.GetProperty("Technical")?.PropertyType,
                Is.EqualTo(typeof(TechnicalBaseAttributesV4)));
            Assert.That(
                playerType.GetProperty("DominantHand")?.PropertyType,
                Is.EqualTo(typeof(DominantHandV4)));

            var constructors = playerType.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(
                constructors[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[]
                {
                    typeof(string),
                    typeof(PhysicalBaseAttributesV4),
                    typeof(TechnicalBaseAttributesV4),
                    typeof(DominantHandV4)
                }));
        }

        [Test]
        public void CareerMatchBoundary_UsesConcreteV4ContextAndCompletion()
        {
            Assert.That(
                typeof(CareerMatchRequest).GetProperty("Context")?.PropertyType,
                Is.EqualTo(typeof(MatchContextV4)));
            Assert.That(
                typeof(CareerMatchRequest).GetProperty("Complete")?.PropertyType,
                Is.EqualTo(typeof(System.Action<MatchResultV4>)));
        }

        [Test]
        public void ProductionCareerEntrypoints_RejectLegacyAbilityAndContextContracts()
        {
            var prohibited = new[]
            {
                "PlayerAbilitySnapshotV1", "PlayerAbilitySnapshotV2",
                "PlayerAbilitySnapshotV3", "MatchContextV2", "MatchContextV3",
                "IMatchContext", "IMatchResult"
            };

            AssertNoPublicEntryPointAccepts(typeof(CareerPlayerRecord).Assembly, prohibited);
            AssertNoPublicEntryPointAccepts(typeof(CareerMatchRequest).Assembly, prohibited);
            AssertNoPublicEntryPointAccepts(typeof(CareerPresentationModule).Assembly, prohibited);
        }

        private static void AssertNoPublicEntryPointAccepts(
            Assembly assembly,
            string[] prohibited)
        {
            var parameters = assembly.GetTypes()
                .Where(type => type.IsPublic)
                .SelectMany(type => type.GetConstructors().Cast<MethodBase>().Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)))
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                    .Concat(new[] { method is MethodInfo methodInfo ? methodInfo.ReturnType : null }))
                .Concat(assembly.GetTypes().Where(type => type.IsPublic)
                    .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    .Select(property => property.PropertyType))
                .Where(type => type != null)
                .ToArray();
            foreach (var prohibitedName in prohibited)
            {
                Assert.That(
                    parameters.Select(type => type.Name),
                    Has.None.EqualTo(prohibitedName),
                    prohibitedName);
            }
        }

        private static void AssertReferences(System.Type type, string assemblyName)
        {
            Assert.That(References(type), Does.Contain(assemblyName));
        }

        private static void AssertDoesNotReference(System.Type type, string assemblyName)
        {
            Assert.That(References(type), Does.Not.Contain(assemblyName));
        }

        private static string[] References(System.Type type)
        {
            return type.Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
        }
    }
}
