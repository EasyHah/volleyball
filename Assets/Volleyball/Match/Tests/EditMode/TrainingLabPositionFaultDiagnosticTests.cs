using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingLabPositionFaultDiagnosticTests
    {
        [TestCase(TeamSide.Home, PositionFaultRuleV1.Slot2BehindSlot1, -1)]
        [TestCase(TeamSide.Away, PositionFaultRuleV1.Slot2BehindSlot1, 1)]
        [TestCase(TeamSide.Home, PositionFaultRuleV1.Slot4RightOfSlot3, 1)]
        [TestCase(TeamSide.Away, PositionFaultRuleV1.Slot4RightOfSlot3, 1)]
        public void Describe_NamesSlotsAndProvidesDeterministicDirection(
            TeamSide side, PositionFaultRuleV1 rule, int direction)
        {
            var value = TrainingLabPositionFaultDiagnosticV1.Describe(
                Fault(side, rule));

            Assert.That(value.Text, Does.Contain("号位"));
            Assert.That(value.Text, Does.Contain(rule.ToString()));
            Assert.That(value.CourtDirection, Is.EqualTo(direction));
            Assert.That(value.Axis, Is.EqualTo(
                rule == PositionFaultRuleV1.Slot4RightOfSlot3
                    ? TrainingLabCorrectionAxisV1.Lateral
                    : TrainingLabCorrectionAxisV1.Depth));
        }

        [Test]
        public void DescribeAll_PreservesEvaluatorOrder()
        {
            var faults = new List<PositionFaultV1>
            {
                Fault(TeamSide.Home, PositionFaultRuleV1.Slot4BehindSlot5),
                Fault(TeamSide.Away, PositionFaultRuleV1.Slot6RightOfSlot1)
            };

            var values = TrainingLabPositionFaultDiagnosticV1.DescribeAll(faults);

            Assert.That(values.Select(value => value.Fault.Rule),
                Is.EqualTo(faults.Select(value => value.Rule)));
        }

        private static PositionFaultV1 Fault(TeamSide side,
            PositionFaultRuleV1 rule)
        {
            var requiredSlot = rule switch
            {
                PositionFaultRuleV1.Slot4BehindSlot5 => 4,
                PositionFaultRuleV1.Slot3BehindSlot6 => 3,
                PositionFaultRuleV1.Slot2BehindSlot1 => 2,
                PositionFaultRuleV1.Slot4RightOfSlot3 => 4,
                PositionFaultRuleV1.Slot3RightOfSlot2 => 3,
                PositionFaultRuleV1.Slot5RightOfSlot6 => 5,
                PositionFaultRuleV1.Slot6RightOfSlot1 => 6,
                _ => throw new System.ArgumentOutOfRangeException(nameof(rule))
            };
            var violatingSlot = rule switch
            {
                PositionFaultRuleV1.Slot4BehindSlot5 => 5,
                PositionFaultRuleV1.Slot3BehindSlot6 => 6,
                PositionFaultRuleV1.Slot2BehindSlot1 => 1,
                PositionFaultRuleV1.Slot4RightOfSlot3 => 3,
                PositionFaultRuleV1.Slot3RightOfSlot2 => 2,
                PositionFaultRuleV1.Slot5RightOfSlot6 => 6,
                PositionFaultRuleV1.Slot6RightOfSlot1 => 1,
                _ => throw new System.ArgumentOutOfRangeException(nameof(rule))
            };
            return new PositionFaultV1(side, rule,
                Slot(side, requiredSlot), Slot(side, violatingSlot));
        }

        private static ServePositionSlotV1 Slot(TeamSide side, int slot)
        {
            return new ServePositionSlotV1(side, slot,
                new PlayerId(side + "-" + slot), SimVector3.Zero);
        }
    }
}
