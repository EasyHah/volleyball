using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.EditModeTests
{
    public sealed class TryoutNumericRulesTests
    {
        [TestCase(4499, PotentialGrade.D)]
        [TestCase(4500, PotentialGrade.C)]
        [TestCase(4999, PotentialGrade.C)]
        [TestCase(5000, PotentialGrade.B)]
        [TestCase(5499, PotentialGrade.B)]
        [TestCase(5500, PotentialGrade.A)]
        [TestCase(5999, PotentialGrade.A)]
        [TestCase(6000, PotentialGrade.S)]
        public void DerivePotential_UsesEveryDocumentedThresholdEdge(
            int ability,
            PotentialGrade expected)
        {
            Assert.That(
                TryoutNumericRulesV1.DerivePotential(
                    ability,
                    ability,
                    ability,
                    ability,
                    ability,
                    ability,
                    ability,
                    ability),
                Is.EqualTo(expected));
        }

        [TestCase(TryoutOutputKind.Spike, 9990, 100, 10, 10000)]
        [TestCase(TryoutOutputKind.Spike, 5, -100, -5, 0)]
        [TestCase(TryoutOutputKind.Fatigue, 0, -99, 0, 0)]
        [TestCase(TryoutOutputKind.Mindset, 52, -19, -1, 51)]
        [TestCase(TryoutOutputKind.CoachTrust, 95, 100, 5, 100)]
        public void Explain_UsesTruncationClampingAndActualAppliedDelta(
            TryoutOutputKind kind,
            int baseValue,
            int perturbation,
            int expectedDelta,
            int expectedFinal)
        {
            var definition = new TryoutOutputDefinition("tryout.output.fixture", kind);
            var output = new TryoutResolvedOutput(definition.OutputId, perturbation);

            var explanation = TryoutNumericRulesV1.Explain(
                "tryout.fixture",
                definition,
                baseValue,
                output);

            Assert.That(explanation.ReasonId, Is.EqualTo("tryout.fixture"));
            Assert.That(explanation.OutputId, Is.EqualTo(definition.OutputId));
            Assert.That(explanation.BaseValue, Is.EqualTo(baseValue));
            Assert.That(explanation.AppliedDelta, Is.EqualTo(expectedDelta));
            Assert.That(explanation.FinalValue, Is.EqualTo(expectedFinal));
        }
    }
}
