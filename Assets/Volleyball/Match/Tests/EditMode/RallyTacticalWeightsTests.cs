using NUnit.Framework;
using Volleyball.AI;

namespace Volleyball.EditModeTests
{
    public sealed class RallyTacticalWeightsTests
    {
        [Test]
        public void ResolveOrDefault_PreservesAValidProposal()
        {
            var proposal = new RallyTacticalWeightProposal(1.2f, 0.8f, 1.6f, 0.4f);

            var resolved = RallyTacticalWeights.ResolveOrDefault(proposal);

            Assert.That(resolved.RolePreference, Is.EqualTo(1.2f));
            Assert.That(resolved.Reachability, Is.EqualTo(0.8f));
            Assert.That(resolved.ApproachDistance, Is.EqualTo(1.6f));
            Assert.That(resolved.DirectionTolerance, Is.EqualTo(0.4f));
            Assert.That(resolved, Is.EqualTo(new RallyTacticalWeights(1.2f, 0.8f, 1.6f, 0.4f)));
        }

        [Test]
        public void ResolveOrDefault_UsesDefaultsForNonFiniteProposalValues()
        {
            var resolved = RallyTacticalWeights.ResolveOrDefault(
                new RallyTacticalWeightProposal(float.NaN, 1f, 1f, 1f));

            Assert.That(resolved, Is.EqualTo(RallyTacticalWeights.Default));
        }

        [Test]
        public void ResolveOrDefault_UsesDefaultsForOutOfRangeProposalValues()
        {
            var aboveRange = RallyTacticalWeights.ResolveOrDefault(
                new RallyTacticalWeightProposal(2.01f, 1f, 1f, 1f));
            var negative = RallyTacticalWeights.ResolveOrDefault(
                new RallyTacticalWeightProposal(1f, -0.01f, 1f, 1f));

            Assert.That(aboveRange, Is.EqualTo(RallyTacticalWeights.Default));
            Assert.That(negative, Is.EqualTo(RallyTacticalWeights.Default));
        }
    }
}
