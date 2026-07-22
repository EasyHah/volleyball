using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class SetTargetSelectorTests
    {
        [Test]
        public void Select_NearNetSetterTargetsTheLegalBandPointWithLargestArmClearance()
        {
            var input = Request(setterDepthFromNet: 1f);

            var selected = SetTargetSelector.Select(input);

            Assert.That(selected.Target.X, Is.Not.EqualTo(0f));
            Assert.That(-selected.Target.Z, Is.InRange(0.75f, 1.50f));
            Assert.That(selected.MinimumArmClearance, Is.GreaterThan(0f));
        }

        [Test]
        public void Select_OrdinarySetterDepthKeepsTargetInTheStandardAttackBand()
        {
            var selected = SetTargetSelector.Select(Request(setterDepthFromNet: 2.5f));

            Assert.That(-selected.Target.Z, Is.InRange(0.75f, 1.50f));
        }

        [Test]
        public void Select_DeepSetterUsesTheShiftedAttackBand()
        {
            var selected = SetTargetSelector.Select(Request(setterDepthFromNet: 6f));

            Assert.That(-selected.Target.Z, Is.InRange(1.75f, 2.50f));
        }

        private static SetTargetSelectionInput Request(float setterDepthFromNet)
        {
            return new SetTargetSelectionInput(
                TeamId.Blue,
                PlayerRole.Attacker,
                setterDepthFromNet,
                targetHeight: 3.35f,
                preferredX: 0f,
                predictedArms: ArmsAtCenterX(),
                lateralCandidates: new[] { -1f, 0f, 1f });
        }

        private static ContactCapsuleFrame[] ArmsAtCenterX()
        {
            return new[]
            {
                new ContactCapsuleFrame(
                    new SimVector3(0f, 2.75f, -1.125f),
                    new SimVector3(0f, 3.85f, -1.125f),
                    0.18f)
            };
        }
    }
}
