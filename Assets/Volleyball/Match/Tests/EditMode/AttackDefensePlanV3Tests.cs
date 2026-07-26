using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Match.Domain.FullRallyV3;

namespace Volleyball.EditModeTests
{
    public sealed class AttackDefensePlanV3Tests
    {
        [Test]
        public void PublicThreatShape_ExposesNoFinalRouteOrFutureSample()
        {
            var names = typeof(PublicAttackThreatV3)
                .GetProperties()
                .Select(value => value.Name)
                .Concat(typeof(PublicAttackThreatEntryV3)
                    .GetProperties()
                    .Select(value => value.Name))
                .ToArray();

            Assert.That(names, Has.None.Matches<string>(
                value => value.Contains("Route") || value.Contains("Sample")));
        }

        [Test]
        public void DomainAssembly_DoesNotReferencePresentationOrUnity()
        {
            var references = typeof(AttackDefensePlanV3).Assembly
                .GetReferencedAssemblies()
                .Select(value => value.Name);

            Assert.That(references, Does.Not.Contain("Volleyball.Match.Presentation"));
            Assert.That(references, Does.Not.Contain("UnityEngine"));
        }

        [Test]
        public void ActionAndDefenseEnums_ContainOnlyDefinedAuthorityCategories()
        {
            Assert.That(Enum.GetValues(typeof(AttackActionClassV3)).Length,
                Is.EqualTo(10));
            Assert.That(Enum.GetValues(typeof(DefenseResponsibilityKindV3)).Length,
                Is.EqualTo(8));
        }
    }
}
