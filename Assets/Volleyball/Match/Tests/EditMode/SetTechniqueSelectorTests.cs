using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class SetTechniqueSelectorTests
    {
        [Test]
        public void Select_UsesTargetRelativeToPlayerForFrontSideAndBackSets()
        {
            var front = SetTechniqueSelector.Select(new SimVector3(0.2f, 7f, 5f), 1f);
            var left = SetTechniqueSelector.Select(new SimVector3(-5f, 7f, 1f), 1f);
            var right = SetTechniqueSelector.Select(new SimVector3(5f, 7f, 1f), 1f);
            var back = SetTechniqueSelector.Select(new SimVector3(0f, 7f, -4f), 1f);

            Assert.That(front.ExecutedStyle, Is.EqualTo(SetTechniqueStyle.FrontTwoHand));
            Assert.That(left.ExecutedStyle, Is.EqualTo(SetTechniqueStyle.SideLeftTwoHand));
            Assert.That(right.ExecutedStyle, Is.EqualTo(SetTechniqueStyle.SideRightTwoHand));
            Assert.That(back.ExecutedStyle, Is.EqualTo(SetTechniqueStyle.BackTwoHand));
        }

        [Test]
        public void Select_LimitsAdvancedSetWhenTechniqueIsTooLow()
        {
            var lowBack = SetTechniqueSelector.Select(new SimVector3(0f, 7f, -4f), 0.4f);
            var mediumBack = SetTechniqueSelector.Select(new SimVector3(2f, 7f, -4f), 0.65f);
            var lowEmergency = SetTechniqueSelector.Select(
                new SimVector3(4f, 7f, 1f),
                0.6f,
                true);

            Assert.That(lowBack.RequestedStyle, Is.EqualTo(SetTechniqueStyle.BackTwoHand));
            Assert.That(lowBack.ExecutedStyle, Is.EqualTo(SetTechniqueStyle.FrontTwoHand));
            Assert.That(lowBack.WasLimited, Is.True);
            Assert.That(mediumBack.ExecutedStyle, Is.EqualTo(SetTechniqueStyle.SideRightTwoHand));
            Assert.That(lowEmergency.RequestedStyle, Is.EqualTo(SetTechniqueStyle.OneHandRight));
            Assert.That(lowEmergency.ExecutedStyle, Is.EqualTo(SetTechniqueStyle.SideRightTwoHand));
            Assert.That(lowEmergency.ControlScale, Is.LessThan(1f));
        }

        [Test]
        public void Select_AllowsOneHandOnlyForEliteSetter()
        {
            var elite = SetTechniqueSelector.Select(
                new SimVector3(-4f, 7f, 1f),
                0.95f,
                true);

            Assert.That(elite.ExecutedStyle, Is.EqualTo(SetTechniqueStyle.OneHandLeft));
            Assert.That(elite.WasLimited, Is.False);
        }
    }
}
