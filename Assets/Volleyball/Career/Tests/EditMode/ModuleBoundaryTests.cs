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
            AssertReferences(typeof(CareerMatchRequest), "Volleyball.Career.Domain");
            AssertDoesNotReference(typeof(CareerMatchRequest), "Volleyball.Match.Domain");

            AssertReferences(typeof(CareerPresentationModule), "Volleyball.Career.Application");
            AssertDoesNotReference(typeof(ThreeVsThreeRallyBootstrap), "Volleyball.Career.Domain");

            AssertReferences(typeof(GameBootstrapModule), "Volleyball.Match.Presentation");
            AssertReferences(typeof(GameBootstrapModule), "Volleyball.Career.Presentation");
        }

        [Test]
        public void CareerMatchBoundary_AcceptsOnlyConcreteV4Contracts()
        {
            var contextV4 = RequiredSharedType("MatchContextV4");
            var resultV4 = RequiredSharedType("MatchResultV4");
            Assert.That(
                typeof(CareerMatchRequest).GetProperty(nameof(CareerMatchRequest.Context))?.PropertyType,
                Is.EqualTo(contextV4));
            Assert.That(
                typeof(IMatchGateway).GetMethod(nameof(IMatchGateway.Play))?.ReturnType,
                Is.EqualTo(resultV4));
        }

        [Test]
        public void ProductionCareerAndMatchEntrypoints_RejectLegacyAbilityAndContextContracts()
        {
            var prohibited = new[]
            {
                typeof(PlayerAbilitySnapshotV1), typeof(PlayerAbilitySnapshotV2),
                typeof(PlayerAbilitySnapshotV3), typeof(MatchContextV2), typeof(MatchContextV3),
                typeof(IMatchContext), typeof(IMatchResult)
            };

            AssertNoPublicEntryPointAccepts(typeof(CareerMatchRequest).Assembly, prohibited);
            AssertNoPublicEntryPointAccepts(typeof(ThreeVsThreeRallyBootstrap).Assembly, prohibited);
        }

        private static System.Type RequiredSharedType(string name)
        {
            var type = typeof(MatchContextV1).Assembly.GetType("Volleyball.Shared.Contracts." + name);
            Assert.That(type, Is.Not.Null, name + " must be the production boundary contract.");
            return type;
        }

        private static void AssertNoPublicEntryPointAccepts(Assembly assembly, System.Type[] prohibited)
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
            Assert.That(parameters, Has.None.EqualTo(prohibited[0]));
            Assert.That(parameters, Has.None.EqualTo(prohibited[1]));
            Assert.That(parameters, Has.None.EqualTo(prohibited[2]));
            Assert.That(parameters, Has.None.EqualTo(prohibited[3]));
            Assert.That(parameters, Has.None.EqualTo(prohibited[4]));
            Assert.That(parameters, Has.None.EqualTo(prohibited[5]));
            Assert.That(parameters, Has.None.EqualTo(prohibited[6]));
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
