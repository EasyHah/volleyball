using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.MatchIntegration;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchBoundaryTests
    {
        private const string ApplicationDirectory =
            "Assets/Volleyball/Career/Runtime/Application";
        private const string DomainDirectory =
            "Assets/Volleyball/Career/Runtime/Domain";
        private const string IntegrationDirectory =
            "Assets/Volleyball/Career/Runtime/MatchIntegration";

        [Test]
        public void RuntimeAssemblies_KeepV2InsideTheNoEngineMatchIntegrationBoundary()
        {
            var domainReferences = References(typeof(CareerPlayerAttributes).Assembly);
            var applicationReferences = References(typeof(CareerMatchLaunch).Assembly);
            var integrationReferences = References(typeof(CareerMatchV2Mapper).Assembly);

            Assert.That(domainReferences, Does.Contain("Volleyball.Shared"));
            Assert.That(domainReferences, Does.Not.Contain("Volleyball.Shared.MatchV2"));
            Assert.That(applicationReferences, Does.Contain("Volleyball.Shared"));
            Assert.That(applicationReferences, Does.Contain("Volleyball.Career.Domain"));
            Assert.That(applicationReferences, Does.Not.Contain("Volleyball.Shared.MatchV2"));
            Assert.That(applicationReferences, Does.Not.Contain("Volleyball.Career.MatchIntegration"));
            Assert.That(integrationReferences, Does.Contain("Volleyball.Shared"));
            Assert.That(integrationReferences, Does.Contain("Volleyball.Shared.MatchV2"));
            Assert.That(integrationReferences, Does.Contain("Volleyball.Career.Domain"));
            Assert.That(integrationReferences, Does.Contain("Volleyball.Career.Application"));
            Assert.That(domainReferences.Where(IsVolleyballAssembly),
                Is.EquivalentTo(new[] { "Volleyball.Shared" }));
            Assert.That(applicationReferences.Where(IsVolleyballAssembly),
                Is.EquivalentTo(new[] { "Volleyball.Shared", "Volleyball.Career.Domain" }));
            Assert.That(integrationReferences.Where(IsVolleyballAssembly),
                Is.EquivalentTo(new[]
                {
                    "Volleyball.Shared", "Volleyball.Shared.MatchV2",
                    "Volleyball.Career.Domain", "Volleyball.Career.Application"
                }));

            foreach (var references in new[] { domainReferences, applicationReferences, integrationReferences })
            {
                Assert.That(references, Does.Not.Contain("UnityEngine.CoreModule"));
                Assert.That(references.Any(name => name.StartsWith("Volleyball.Match", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(references, Does.Not.Contain("Volleyball.Career.Persistence"));
                Assert.That(references, Does.Not.Contain("Volleyball.Bootstrap"));
            }
        }

        [Test]
        public void DomainAndApplicationPublicSignatures_DoNotLeakV2OrIntegrationTypes()
        {
            foreach (var assembly in new[]
                     {
                         typeof(CareerPlayerAttributes).Assembly,
                         typeof(CareerMatchLaunch).Assembly
                     })
            {
                foreach (var signatureType in PublicSignatureTypes(assembly))
                {
                    var name = signatureType.FullName ?? signatureType.Name;
                    Assert.That(name.StartsWith("Volleyball.Shared.Contracts.V2", StringComparison.Ordinal),
                        Is.False, assembly.GetName().Name + " exposes " + name + ".");
                    Assert.That(name.StartsWith("Volleyball.Career.MatchIntegration", StringComparison.Ordinal),
                        Is.False, assembly.GetName().Name + " exposes " + name + ".");
                }
            }

            Assert.That(typeof(CareerMatchLaunch).GetProperties()
                .Any(property => property.Name.IndexOf("Training", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void ProductionSources_EnforceThePureBoundaryAndExplicitRebindingAlgorithm()
        {
            var domain = ProductionSources(DomainDirectory);
            var application = ProductionSources(ApplicationDirectory);
            var integration = ProductionSources(IntegrationDirectory);

            Assert.That(domain, Does.Not.Contain("Volleyball.Shared.Contracts.V2"));
            Assert.That(domain, Does.Not.Contain("IMatchRunnerV2"));
            Assert.That(application, Does.Not.Contain("Volleyball.Shared.Contracts.V2"));
            Assert.That(application, Does.Not.Contain("IMatchRunnerV2"));
            Assert.That(application, Does.Not.Contain("Volleyball.Career.MatchIntegration"));

            var forbiddenIntegrationTokens = new[]
            {
                "using System.IO",
                "File.",
                "Directory.",
                "Path.",
                "UnityEngine",
                "Volleyball.Match.Domain",
                "Volleyball.Match.AI",
                "Volleyball.Match.Presentation",
                "Volleyball.Career.Persistence",
                "Volleyball.Career.Presentation",
                "Volleyball.Bootstrap",
                "System.Random",
                "Random.Shared",
                "UnityEngine.Random",
                "System.IO.",
                "global::System.IO",
                "FileStream",
                "FileInfo",
                "FileMode",
                "FileAccess",
                "StreamReader",
                "StreamWriter",
                "BinaryReader",
                "BinaryWriter",
                "Guid.NewGuid",
                "RandomNumberGenerator",
                "RNGCryptoServiceProvider",
                "GetRandomBytes",
                "Task.Run",
                "DateTime.Now",
                "DateTime.UtcNow",
                "DateTimeOffset",
                "Stopwatch",
                "Environment.TickCount"
            };
            foreach (var token in forbiddenIntegrationTokens)
            {
                Assert.That(integration, Does.Not.Contain(token), "Forbidden integration token: " + token);
            }

            Assert.That(integration, Does.Contain("new FixtureFactKey(team.Side, player.RotationSlot)"));
            Assert.That(integration, Does.Contain("ResolveTemplateWinnerSide"));
            Assert.That(integration, Does.Contain("ResolveIncomingTeamId"));
            Assert.That(integration, Does.Contain("templateResult.WinnerTeamId"));
            Assert.That(integration, Does.Contain("team.Side == winningSide"));
            Assert.That(integration, Does.Not.Contain("GrowthExperience"));
            Assert.That(integration, Does.Not.Contain("(PlayerPositionV2)"));

            var runner = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                IntegrationDirectory,
                "FixtureMatchRunnerV2.cs"));
            Assert.That(runner, Does.Contain("ExecuteCore(context, cancellationToken)"));
            Assert.That(runner, Does.Contain("catch (OperationCanceledException"));
            var copySetsIndex = runner.IndexOf("CopySets(definition.Result.Sets)", StringComparison.Ordinal);
            var secondCancellationIndex = runner.IndexOf(
                "cancellationToken.ThrowIfCancellationRequested()",
                copySetsIndex,
                StringComparison.Ordinal);
            var resultConstructionIndex = runner.IndexOf(
                "MatchResultV2 result;",
                copySetsIndex,
                StringComparison.Ordinal);
            Assert.That(secondCancellationIndex, Is.GreaterThan(copySetsIndex));
            Assert.That(secondCancellationIndex, Is.LessThan(resultConstructionIndex));
            Assert.That(runner.IndexOf(
                    "cancellationToken.ThrowIfCancellationRequested()",
                    resultConstructionIndex,
                    StringComparison.Ordinal),
                Is.EqualTo(-1), "Late cancellation must not rewrite a completed validated result.");

            var repository = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                IntegrationDirectory,
                "VersionedMatchFixtureRepository.cs"));
            Assert.That(repository.IndexOf("canonicalContextBytes.Clone", StringComparison.Ordinal),
                Is.LessThan(repository.IndexOf("DeserializeContext", StringComparison.Ordinal)));
            Assert.That(repository.IndexOf("canonicalResultBytes.Clone", StringComparison.Ordinal),
                Is.LessThan(repository.IndexOf("DeserializeResult", StringComparison.Ordinal)));
        }

        [Test]
        public void AssemblyDefinitionsAndRepositoryPolicy_RegisterTheExactNewDependencies()
        {
            var root = Directory.GetCurrentDirectory();
            var applicationAsmdef = File.ReadAllText(Path.Combine(
                root, ApplicationDirectory, "Volleyball.Career.Application.asmdef"));
            var integrationAsmdef = File.ReadAllText(Path.Combine(
                root, IntegrationDirectory, "Volleyball.Career.MatchIntegration.asmdef"));
            var testsAsmdef = File.ReadAllText(Path.Combine(
                root,
                "Assets/Volleyball/Career/Tests/EditMode/Volleyball.Career.EditModeTests.asmdef"));
            var policy = File.ReadAllText(Path.Combine(root, "tools/repository_policy.json"));

            var applicationDefinition = ParseJson<AssemblyDefinitionJson>(applicationAsmdef);
            var integrationDefinition = ParseJson<AssemblyDefinitionJson>(integrationAsmdef);
            var testsDefinition = ParseJson<AssemblyDefinitionJson>(testsAsmdef);
            var policyDefinition = ParseJson<RepositoryPolicyJson>(policy);
            var expectedApplication = new[] { "Volleyball.Shared", "Volleyball.Career.Domain" };
            var expectedIntegration = new[]
            {
                "Volleyball.Shared", "Volleyball.Shared.MatchV2",
                "Volleyball.Career.Domain", "Volleyball.Career.Application"
            };
            var expectedTests = new[]
            {
                "Volleyball.Shared", "Volleyball.Shared.MatchV2", "Volleyball.Career.Domain",
                "Volleyball.Career.Application", "Volleyball.Career.MatchIntegration",
                "Volleyball.Career.Persistence", "Volleyball.Career.Presentation",
                "Volleyball.Match.Presentation", "Volleyball.Bootstrap", "Unity.InputSystem"
            };

            Assert.That(applicationDefinition.References, Is.EqualTo(expectedApplication));
            Assert.That(applicationDefinition.NoEngineReferences, Is.True);
            Assert.That(integrationDefinition.References, Is.EqualTo(expectedIntegration));
            Assert.That(integrationDefinition.NoEngineReferences, Is.True);
            Assert.That(testsDefinition.References, Is.EqualTo(expectedTests));
            Assert.That(policyDefinition.Assemblies.Application.References,
                Is.EqualTo(expectedApplication));
            Assert.That(policyDefinition.Assemblies.Integration.References,
                Is.EqualTo(expectedIntegration));
            Assert.That(policyDefinition.Assemblies.Tests.References, Is.EqualTo(expectedTests));

            Assert.That(applicationAsmdef, Does.Contain("\"Volleyball.Shared\""));
            Assert.That(applicationAsmdef, Does.Not.Contain("Volleyball.Shared.MatchV2"));
            Assert.That(applicationAsmdef, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(integrationAsmdef, Does.Contain("\"Volleyball.Shared\""));
            Assert.That(integrationAsmdef, Does.Contain("\"Volleyball.Shared.MatchV2\""));
            Assert.That(integrationAsmdef, Does.Contain("\"Volleyball.Career.Domain\""));
            Assert.That(integrationAsmdef, Does.Contain("\"Volleyball.Career.Application\""));
            Assert.That(integrationAsmdef, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(policy, Does.Contain("\"Volleyball.Career.MatchIntegration\""));
            Assert.That(policy, Does.Contain(
                "Assets/Volleyball/Career/Runtime/MatchIntegration/Volleyball.Career.MatchIntegration.asmdef"));
        }

        private static bool IsVolleyballAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("Volleyball.", StringComparison.Ordinal);
        }

        private static T ParseJson<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return (T)serializer.ReadObject(stream);
        }

        private static string[] References(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        }

        private static string ProductionSources(string relativeDirectory)
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), relativeDirectory);
            return string.Join("\n", Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        }

        private static IEnumerable<Type> PublicSignatureTypes(Assembly assembly)
        {
            const BindingFlags flags = BindingFlags.Public |
                                       BindingFlags.Instance |
                                       BindingFlags.Static |
                                       BindingFlags.DeclaredOnly;
            foreach (var type in assembly.GetExportedTypes())
            {
                foreach (var constructor in type.GetConstructors(flags))
                {
                    foreach (var parameter in constructor.GetParameters())
                    {
                        foreach (var referenced in Expand(parameter.ParameterType))
                        {
                            yield return referenced;
                        }
                    }
                }

                foreach (var method in type.GetMethods(flags))
                {
                    foreach (var referenced in Expand(method.ReturnType))
                    {
                        yield return referenced;
                    }

                    foreach (var parameter in method.GetParameters())
                    {
                        foreach (var referenced in Expand(parameter.ParameterType))
                        {
                            yield return referenced;
                        }
                    }
                }

                foreach (var property in type.GetProperties(flags))
                {
                    foreach (var referenced in Expand(property.PropertyType))
                    {
                        yield return referenced;
                    }
                }
            }
        }

        private static IEnumerable<Type> Expand(Type type)
        {
            yield return type;
            if (type.HasElementType)
            {
                foreach (var element in Expand(type.GetElementType()))
                {
                    yield return element;
                }
            }

            if (!type.IsGenericType)
            {
                yield break;
            }

            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var nested in Expand(argument))
                {
                    yield return nested;
                }
            }
        }

        [DataContract]
        private sealed class AssemblyDefinitionJson
        {
            [DataMember(Name = "references")]
            public string[] References { get; set; }

            [DataMember(Name = "noEngineReferences")]
            public bool NoEngineReferences { get; set; }
        }

        [DataContract]
        private sealed class RepositoryPolicyJson
        {
            [DataMember(Name = "assemblies")]
            public RepositoryAssemblyEntriesJson Assemblies { get; set; }
        }

        [DataContract]
        private sealed class RepositoryAssemblyEntriesJson
        {
            [DataMember(Name = "Volleyball.Career.Application")]
            public AssemblyDefinitionJson Application { get; set; }

            [DataMember(Name = "Volleyball.Career.MatchIntegration")]
            public AssemblyDefinitionJson Integration { get; set; }

            [DataMember(Name = "Volleyball.Career.EditModeTests")]
            public AssemblyDefinitionJson Tests { get; set; }
        }
    }
}
