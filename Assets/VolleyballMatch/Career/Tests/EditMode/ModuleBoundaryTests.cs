using System.Linq;
using NUnit.Framework;
using VolleyballMatch.Bootstrap;
using VolleyballMatch.Career.Application;
using VolleyballMatch.Career.Domain;
using VolleyballMatch.Career.Presentation;
using VolleyballMatch.Presentation;

namespace VolleyballMatch.Career.EditModeTests
{
    public sealed class ModuleBoundaryTests
    {
        [Test]
        public void RuntimeAssemblies_FollowTheOneWayModuleDependencyGraph()
        {
            AssertReferences(typeof(CareerPlayerRecord), "VolleyballMatch.Shared");
            AssertDoesNotReference(typeof(CareerPlayerRecord), "VolleyballMatch.Match.Domain");
            AssertDoesNotReference(typeof(CareerPlayerRecord), "VolleyballMatch.Match.Presentation");

            AssertReferences(typeof(CareerMatchRequest), "VolleyballMatch.Shared");
            AssertReferences(typeof(CareerMatchRequest), "VolleyballMatch.Career.Domain");
            AssertDoesNotReference(typeof(CareerMatchRequest), "VolleyballMatch.Match.Domain");

            AssertReferences(typeof(CareerPresentationModule), "VolleyballMatch.Career.Application");
            AssertDoesNotReference(typeof(ThreeVsThreeRallyBootstrap), "VolleyballMatch.Career.Domain");

            AssertReferences(typeof(GameBootstrapModule), "VolleyballMatch.Match.Presentation");
            AssertReferences(typeof(GameBootstrapModule), "VolleyballMatch.Career.Presentation");
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
