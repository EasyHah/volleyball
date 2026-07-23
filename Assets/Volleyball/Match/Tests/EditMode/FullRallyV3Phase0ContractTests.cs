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

        [Test]
        public void DeterministicWorkBudget_DoesNotContainWallClockDecisionFields()
        {
            var budget = DeterministicWorkBudgetV3.DefaultPhase0();

            Assert.That(budget.BeamWidth, Is.GreaterThan(0));
            Assert.That(budget.CandidatesPerResponsibility, Is.GreaterThan(0));
            Assert.That(budget.UsesWallClockForDecision, Is.False);
        }

        [Test]
        public void ExecutionEnvelopeIdentity_IsSharedBetweenPlanningAndExecution()
        {
            var plannerEnvelope = new ExecutionEnvelopeV3(
                "envelope-v3",
                "ability-hash-1",
                "source-v3",
                "attack",
                "target-baseline-1",
                "distribution-1",
                "sample-1");
            var executorEnvelope = new ExecutionEnvelopeV3(
                "envelope-v3",
                "ability-hash-1",
                "source-v3",
                "attack",
                "target-baseline-1",
                "distribution-1",
                "sample-1");

            Assert.That(executorEnvelope, Is.EqualTo(plannerEnvelope));
            Assert.That(executorEnvelope.DeterministicSampleKey, Is.EqualTo("sample-1"));
        }

        [Test]
        public void TrajectoryArtifactIdentity_MatchesForSameDeterministicInputs()
        {
            var first = BallTrajectoryArtifactV3.CreateIdentity("ball-1", "physics-1", "sample-1", "predictor-1", "normal");
            var second = BallTrajectoryArtifactV3.CreateIdentity("ball-1", "physics-1", "sample-1", "predictor-1", "normal");

            Assert.That(second, Is.EqualTo(first));
        }
    }
}
