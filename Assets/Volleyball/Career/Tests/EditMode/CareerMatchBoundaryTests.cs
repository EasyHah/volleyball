using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.MatchIntegration;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchBoundaryTests
    {
        [Test]
        public void RuntimeAssemblies_KeepSharedV4InsidePureMatchIntegration()
        {
            var domain = References(typeof(CareerPlayerAttributes).Assembly);
            var application = References(typeof(CareerMatchLaunch).Assembly);
            var integration = References(typeof(CareerMatchV4Mapper).Assembly);

            Assert.That(domain.Where(IsVolleyballAssembly),
                Is.EquivalentTo(new[] { "Volleyball.Shared" }));
            Assert.That(application.Where(IsVolleyballAssembly),
                Is.EquivalentTo(new[] { "Volleyball.Shared", "Volleyball.Career.Domain" }));
            Assert.That(integration.Where(IsVolleyballAssembly),
                Is.EquivalentTo(new[]
                {
                    "Volleyball.Shared",
                    "Volleyball.Career.Domain",
                    "Volleyball.Career.Application"
                }));
            Assert.That(integration, Does.Not.Contain("UnityEngine.CoreModule"));
            Assert.That(integration.Any(name =>
                name.StartsWith("Volleyball.Match", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void DomainAndApplicationPublicSignatures_DoNotExposeSharedMatchDtos()
        {
            var forbiddenPrefixes = new[]
            {
                "Volleyball.Shared.Contracts.MatchContextV",
                "Volleyball.Shared.Contracts.MatchResultV",
                "Volleyball.Shared.Contracts.PlayerAbilitySnapshotV"
            };

            foreach (var assembly in new[]
                     {
                         typeof(CareerPlayerAttributes).Assembly,
                         typeof(CareerMatchLaunch).Assembly
                     })
            {
                foreach (var signatureType in PublicSignatureTypes(assembly))
                {
                    var name = signatureType.FullName ?? signatureType.Name;
                    Assert.That(
                        forbiddenPrefixes.Any(name.StartsWith),
                        Is.False,
                        assembly.GetName().Name + " exposes " + name + ".");
                }
            }
        }

        [Test]
        public void AssemblyDefinitions_UseOnlyAuthoritativeSharedAssembly()
        {
            var root = Directory.GetCurrentDirectory();
            var integration = File.ReadAllText(Path.Combine(
                root,
                "Assets/Volleyball/Career/Runtime/MatchIntegration/Volleyball.Career.MatchIntegration.asmdef"));
            var persistence = File.ReadAllText(Path.Combine(
                root,
                "Assets/Volleyball/Career/Runtime/Persistence/Volleyball.Career.Persistence.asmdef"));
            var policy = File.ReadAllText(Path.Combine(root, "tools/repository_policy.json"));

            Assert.That(integration, Does.Contain("\"Volleyball.Shared\""));
            Assert.That(integration, Does.Not.Contain("Volleyball.Shared.MatchV2"));
            Assert.That(integration, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(persistence, Does.Not.Contain("Volleyball.Shared.MatchV2"));
            Assert.That(policy, Does.Not.Contain("Volleyball.Shared.MatchV2"));
        }

        private static string[] References(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        }

        private static bool IsVolleyballAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("Volleyball.", StringComparison.Ordinal);
        }

        private static System.Collections.Generic.IEnumerable<Type> PublicSignatureTypes(
            Assembly assembly)
        {
            const BindingFlags flags = BindingFlags.Public |
                                       BindingFlags.Instance |
                                       BindingFlags.Static |
                                       BindingFlags.DeclaredOnly;
            foreach (var type in assembly.GetExportedTypes())
            {
                foreach (var memberType in MemberTypes(type, flags))
                {
                    yield return memberType;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<Type> MemberTypes(
            Type type,
            BindingFlags flags)
        {
            if (type.BaseType != null) yield return type.BaseType;
            foreach (var contract in type.GetInterfaces()) yield return contract;
            foreach (var constructor in type.GetConstructors(flags))
                foreach (var parameter in constructor.GetParameters()) yield return parameter.ParameterType;
            foreach (var method in type.GetMethods(flags))
            {
                yield return method.ReturnType;
                foreach (var parameter in method.GetParameters()) yield return parameter.ParameterType;
            }
            foreach (var property in type.GetProperties(flags)) yield return property.PropertyType;
            foreach (var field in type.GetFields(flags)) yield return field.FieldType;
            foreach (var eventInfo in type.GetEvents(flags)) yield return eventInfo.EventHandlerType;
        }
    }
}
