using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Bootstrap;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;
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

            AssertReferences(typeof(OperationReceiptIndex), "Volleyball.Career.Domain");
            AssertDoesNotReference(typeof(OperationReceiptIndex), "Volleyball.Shared");
            AssertDoesNotReference(typeof(OperationReceiptIndex), "Volleyball.Match.Domain");

            AssertReferences(typeof(CareerSaveSnapshotMapper), "Volleyball.Career.Domain");
            AssertReferences(typeof(CareerSaveSnapshotMapper), "Volleyball.Shared");
            AssertDoesNotReference(typeof(CareerSaveSnapshotMapper), "Volleyball.Match.Domain");
            AssertDoesNotReference(typeof(CareerSaveSnapshotMapper), "UnityEngine.CoreModule");

            AssertReferences(typeof(CareerPresentationModule), "Volleyball.Career.Application");
            AssertDoesNotReference(typeof(ThreeVsThreeRallyBootstrap), "Volleyball.Career.Domain");

            AssertReferences(typeof(GameBootstrapModule), "Volleyball.Match.Presentation");
            AssertReferences(typeof(GameBootstrapModule), "Volleyball.Career.Presentation");
        }

        [Test]
        public void CareerPublicSignatures_DoNotExposeLegacyMatchContracts()
        {
            var assemblies = new[]
            {
                typeof(CareerPlayerRecord).Assembly,
                typeof(OperationReceiptIndex).Assembly,
                typeof(CareerSaveSnapshotMapper).Assembly
            };
            var forbiddenTypePrefixes = new[]
            {
                "Volleyball.Shared.Contracts.MatchContextV",
                "Volleyball.Shared.Contracts.MatchResultV",
                "Volleyball.Shared.Contracts.PlayerAbilitySnapshotV"
            };

            foreach (var assembly in assemblies)
            {
                foreach (var signatureType in PublicSignatureTypes(assembly))
                {
                    var fullName = signatureType.FullName ?? signatureType.Name;
                    Assert.That(
                        forbiddenTypePrefixes.Any(fullName.StartsWith),
                        Is.False,
                        assembly.GetName().Name + " publicly exposes legacy match type " + fullName + ".");
                }
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

        private static IEnumerable<System.Type> PublicSignatureTypes(Assembly assembly)
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
                        foreach (var referencedType in Expand(parameter.ParameterType))
                        {
                            yield return referencedType;
                        }
                    }
                }

                foreach (var method in type.GetMethods(flags))
                {
                    foreach (var referencedType in Expand(method.ReturnType))
                    {
                        yield return referencedType;
                    }

                    foreach (var parameter in method.GetParameters())
                    {
                        foreach (var referencedType in Expand(parameter.ParameterType))
                        {
                            yield return referencedType;
                        }
                    }
                }

                foreach (var property in type.GetProperties(flags))
                {
                    foreach (var referencedType in Expand(property.PropertyType))
                    {
                        yield return referencedType;
                    }
                }

                foreach (var field in type.GetFields(flags))
                {
                    foreach (var referencedType in Expand(field.FieldType))
                    {
                        yield return referencedType;
                    }
                }
            }
        }

        private static IEnumerable<System.Type> Expand(System.Type type)
        {
            yield return type;

            if (type.HasElementType)
            {
                foreach (var elementType in Expand(type.GetElementType()))
                {
                    yield return elementType;
                }
            }

            if (!type.IsGenericType)
            {
                yield break;
            }

            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var argumentType in Expand(argument))
                {
                    yield return argumentType;
                }
            }
        }
    }
}
