using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Shared.MatchV2.EditModeTests
{
    public sealed class MatchV2BoundaryTests
    {
        [Test]
        public void RuntimeAssembly_IsNoEngineAndReferencesOnlyLegacyShared()
        {
            var root = Directory.GetCurrentDirectory();
            var asmdef = File.ReadAllText(Path.Combine(root,
                "Assets/Volleyball/Shared/MatchV2/Runtime/Volleyball.Shared.MatchV2.asmdef"));
            Assert.That(asmdef, Does.Contain("\"noEngineReferences\": true"));
            Assert.That(asmdef, Does.Contain("\"references\": [\"Volleyball.Shared\"]"));
            var references = typeof(MatchContextV2).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();
            Assert.That(references, Does.Not.Contain("UnityEngine"));
            Assert.That(references.Any(name => name != null && name.StartsWith("Volleyball.Match", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void RuntimeSources_ContainNoV1DtoSerializerFloatRandomOrFilesystemWriteDependency()
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Volleyball/Shared/MatchV2/Runtime");
            var source = string.Join("\n", Directory.GetFiles(directory, "*.cs").Select(File.ReadAllText));
            Assert.That(source, Does.Not.Contain("MatchContextV1"));
            Assert.That(source, Does.Not.Contain("MatchResultV1"));
            Assert.That(source, Does.Not.Contain("PlayerAbilitySnapshotV1"));
            Assert.That(source, Does.Not.Contain("DataContractJsonSerializer"));
            Assert.That(source, Does.Not.Contain("Newtonsoft"));
            Assert.That(source, Does.Not.Contain("UnityEngine"));
            Assert.That(source, Does.Not.Contain("System.Random"));
            Assert.That(source, Does.Not.Contain("UnityEngine.Random"));
            Assert.That(source, Does.Not.Contain("File.Write"));
            Assert.That(source, Does.Not.Contain("float "));
            Assert.That(source, Does.Not.Contain("double "));
        }

        [Test]
        public void PublicTypes_AreProducerNeutralAndImmutableAtTheirBoundaries()
        {
            var publicTypes = typeof(MatchContextV2).Assembly.GetExportedTypes();
            Assert.That(publicTypes.Any(type => type.Namespace != "Volleyball.Shared.Contracts.V2"), Is.False);
            Assert.That(publicTypes.Where(type => !type.IsEnum)
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                .Any(field => !field.IsStatic && !field.IsInitOnly), Is.False);
            Assert.That(publicTypes.Where(type => !typeof(Exception).IsAssignableFrom(type))
                .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                .Any(property => property.SetMethod != null), Is.False);
        }
    }
}
