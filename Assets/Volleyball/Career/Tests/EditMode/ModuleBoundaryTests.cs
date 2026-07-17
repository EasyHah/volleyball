using System.Linq;
using NUnit.Framework;
using Volleyball.Bootstrap;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Presentation;
using Volleyball.Presentation;

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
