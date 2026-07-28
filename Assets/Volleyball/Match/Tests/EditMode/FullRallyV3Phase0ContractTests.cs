using NUnit.Framework;
using Volleyball.Match.Domain.FullRallyV3;

namespace Volleyball.Match.EditModeTests
{
    public sealed class FullRallyV3Phase0ContractTests
    {
        [Test]
        public void PlanCoverageDecision_IsDeterministicValueObject()
        {
            var first = PlanCoverageDecision.Covered("plan-1", PlanCoverageReason.WithinConditionalEnvelope);
            var second = PlanCoverageDecision.Covered("plan-1", PlanCoverageReason.WithinConditionalEnvelope);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Kind, Is.EqualTo(PlanCoverageDecisionKind.CoveredActivateBranch));
        }

    }
}
