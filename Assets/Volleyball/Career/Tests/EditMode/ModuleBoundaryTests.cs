using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Bootstrap;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.MatchIntegration;
using Volleyball.Career.Persistence;
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

            AssertReferences(typeof(OperationReceiptIndex), "Volleyball.Career.Domain");
            AssertReferences(typeof(OperationReceiptIndex), "Volleyball.Shared");
            AssertDoesNotReference(typeof(OperationReceiptIndex), "Volleyball.Match.Domain");

            AssertReferences(typeof(CareerSaveSnapshotMapper), "Volleyball.Career.Domain");
            AssertReferences(typeof(CareerSaveSnapshotMapper), "Volleyball.Shared");
            AssertDoesNotReference(typeof(CareerSaveSnapshotMapper), "Volleyball.Match.Domain");
            AssertDoesNotReference(typeof(CareerSaveSnapshotMapper), "UnityEngine.CoreModule");

            AssertReferences(typeof(CareerMatchV4Mapper), "Volleyball.Shared");
            AssertDoesNotReference(typeof(CareerMatchV4Mapper), "Volleyball.Match.Domain");

            AssertReferences(typeof(CareerPresentationModule), "Volleyball.Career.Application");
            AssertDoesNotReference(typeof(ThreeVsThreeRallyBootstrap), "Volleyball.Career.Domain");

            AssertReferences(typeof(GameBootstrapModule), "Volleyball.Match.Presentation");
            AssertReferences(typeof(GameBootstrapModule), "Volleyball.Career.Presentation");
        }

        [Test]
        public void CareerMatchBoundary_UsesConcreteV4ContractsAndRejectsLegacyContracts()
        {
            Assert.That(
                typeof(CareerMatchV4Mapper).GetMethod(nameof(CareerMatchV4Mapper.ToContext))
                    ?.ReturnType,
                Is.EqualTo(typeof(MatchContextV4)));

            var prohibited = new[]
            {
                "PlayerAbilitySnapshotV1",
                "PlayerAbilitySnapshotV2",
                "PlayerAbilitySnapshotV3",
                "MatchContextV1",
                "MatchContextV2",
                "MatchContextV3",
                "MatchResultV1",
                "MatchResultV2",
                "MatchResultV3",
                "IMatchContext",
                "IMatchResult"
            };

            AssertNoPublicEntryPointUses(typeof(CareerPlayerRecord).Assembly, prohibited);
            AssertNoPublicEntryPointUses(typeof(CareerMatchV4Mapper).Assembly, prohibited);
            AssertNoPublicEntryPointUses(typeof(CareerPresentationModule).Assembly, prohibited);
        }

        private static void AssertNoPublicEntryPointUses(
            Assembly assembly,
            IReadOnlyCollection<string> prohibited)
        {
            foreach (var signatureType in PublicSignatureTypes(assembly))
            {
                Assert.That(
                    prohibited,
                    Has.None.EqualTo(signatureType.Name),
                    assembly.GetName().Name + " exposes legacy contract " +
                    signatureType.FullName + ".");
            }
        }

        private static void AssertReferences(Type type, string assemblyName)
        {
            Assert.That(References(type), Does.Contain(assemblyName));
        }

        private static void AssertDoesNotReference(Type type, string assemblyName)
        {
            Assert.That(References(type), Does.Not.Contain(assemblyName));
        }

        private static string[] References(Type type)
        {
            return type.Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
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

        private static IEnumerable<Type> Expand(Type type)
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
