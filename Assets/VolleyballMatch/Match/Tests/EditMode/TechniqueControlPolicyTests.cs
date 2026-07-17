using NUnit.Framework;
using VolleyballMatch.AI;
using VolleyballMatch.Domain.Players;
using VolleyballMatch.Domain.Simulation;

namespace VolleyballMatch.EditModeTests
{
    public sealed class TechniqueControlPolicyTests
    {
        [Test]
        public void Apply_LeavesPhysicalVelocityUntouchedWithoutRealContactQuality()
        {
            var physical = new SimVector3(1f, 4f, 2f);
            var result = TechniqueControlPolicy.Apply(new TechniqueControlInput(
                TechniqueAction.Set,
                physical,
                new SimVector3(0f, 8f, 3f),
                SimVector3.Up,
                1f,
                0f));

            Assert.That(result.FinalOutgoing, Is.EqualTo(physical));
            Assert.That(result.AppliedControl, Is.Zero);
        }

        [TestCase(TechniqueAction.Receive, 1f)]
        [TestCase(TechniqueAction.Set, 1f)]
        [TestCase(TechniqueAction.Attack, 1f)]
        [TestCase(TechniqueAction.Block, 0.05f)]
        [TestCase(TechniqueAction.Serve, 0.35f)]
        public void Apply_UsesActionSpecificMaximumControl(TechniqueAction action, float expected)
        {
            var result = TechniqueControlPolicy.Apply(new TechniqueControlInput(
                action,
                new SimVector3(0f, 5f, 0f),
                new SimVector3(0f, 7f, 1f),
                SimVector3.Up,
                1f,
                1f));

            Assert.That(result.AppliedControl, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Apply_HigherTechniqueMovesCloserToConstrainedTarget()
        {
            var physical = new SimVector3(0f, 5f, 0f);
            var target = new SimVector3(0f, 7f, 2f);
            var low = TechniqueControlPolicy.Apply(new TechniqueControlInput(
                TechniqueAction.Set, physical, target, SimVector3.Up, 0.2f, 1f));
            var high = TechniqueControlPolicy.Apply(new TechniqueControlInput(
                TechniqueAction.Set, physical, target, SimVector3.Up, 0.9f, 1f));

            Assert.That((high.FinalOutgoing - high.ConstrainedTarget).Magnitude,
                Is.LessThan((low.FinalOutgoing - low.ConstrainedTarget).Magnitude));
        }

        [Test]
        public void Apply_DoesNotUseTargetThatOpposesActualStrikeDirection()
        {
            var physical = new SimVector3(0f, 8f, 0f);
            var result = TechniqueControlPolicy.Apply(new TechniqueControlInput(
                TechniqueAction.Attack,
                physical,
                new SimVector3(0f, -30f, 0f),
                SimVector3.Up,
                1f,
                1f));

            Assert.That(SimVector3.Dot(result.FinalOutgoing, SimVector3.Up), Is.GreaterThan(0f));
            Assert.That(result.FinalOutgoing.Magnitude, Is.LessThanOrEqualTo(30f));
        }
    }
}
