using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class PositionFaultEvaluatorV1Tests
    {
        [Test]
        public void Evaluate_EqualFootProjectionsAreLegal()
        {
            var slots = AllLegalSlots();
            slots[3] = Slot(TeamSide.Home, 4, -3f, -2f);
            slots[4] = Slot(TeamSide.Home, 5, -3f, -2f);

            Assert.That(PositionFaultEvaluatorV1.Evaluate(slots), Is.Empty);
        }

        [TestCase(TeamSide.Home, 4, -7f, PositionFaultRuleV1.Slot4BehindSlot5)]
        [TestCase(TeamSide.Home, 3, -7f, PositionFaultRuleV1.Slot3BehindSlot6)]
        [TestCase(TeamSide.Home, 2, -7f, PositionFaultRuleV1.Slot2BehindSlot1)]
        [TestCase(TeamSide.Away, 4, 7f, PositionFaultRuleV1.Slot4BehindSlot5)]
        [TestCase(TeamSide.Away, 3, 7f, PositionFaultRuleV1.Slot3BehindSlot6)]
        [TestCase(TeamSide.Away, 2, 7f, PositionFaultRuleV1.Slot2BehindSlot1)]
        public void Evaluate_FrontBehindPairedBackRowReportsFault(TeamSide side, int slot,
            float z, PositionFaultRuleV1 expected)
        {
            var slots = AllLegalSlots();
            var index = slots.FindIndex(value => value.Side == side && value.Slot == slot);
            slots[index] = Slot(side, slot, slots[index].FootProjection.X, z);

            var fault = PositionFaultEvaluatorV1.Evaluate(slots).Single();
            Assert.That(fault.Rule, Is.EqualTo(expected));
            Assert.That(fault.Side, Is.EqualTo(side));
        }

        [TestCase(4, 2f, PositionFaultRuleV1.Slot4RightOfSlot3)]
        [TestCase(3, 4f, PositionFaultRuleV1.Slot3RightOfSlot2)]
        [TestCase(5, 2f, PositionFaultRuleV1.Slot5RightOfSlot6)]
        [TestCase(6, 4f, PositionFaultRuleV1.Slot6RightOfSlot1)]
        public void Evaluate_LeftPlayerRightOfRequiredNeighborReportsFault(int slot, float x,
            PositionFaultRuleV1 expected)
        {
            var slots = AllLegalSlots();
            var index = slots.FindIndex(value => value.Slot == slot);
            slots[index] = Slot(TeamSide.Home, slot, x, slots[index].FootProjection.Z);

            Assert.That(PositionFaultEvaluatorV1.Evaluate(slots).Single().Rule,
                Is.EqualTo(expected));
        }

        [Test]
        public void Evaluate_ReturnsTeamThenRuleOrderForMultipleFaults()
        {
            var slots = AllLegalSlots();
            Replace(slots, TeamSide.Home, 4, -3f, -7f);
            Replace(slots, TeamSide.Home, 3, 4f, -7f);
            Replace(slots, TeamSide.Away, 2, -3f, 7f);

            Assert.That(PositionFaultEvaluatorV1.Evaluate(slots).Select(value => value.Rule),
                Is.EqualTo(new[]
                {
                    PositionFaultRuleV1.Slot4BehindSlot5,
                    PositionFaultRuleV1.Slot3BehindSlot6,
                    PositionFaultRuleV1.Slot3RightOfSlot2,
                    PositionFaultRuleV1.Slot2BehindSlot1
                }));
        }

        [Test]
        public void Evaluate_RejectsDuplicateSlotsPlayersAndNonFiniteProjections()
        {
            var slots = AllLegalSlots();
            slots[1] = Slot(TeamSide.Home, 1, 3f, -6f, "home-duplicate");
            Assert.That(() => PositionFaultEvaluatorV1.Evaluate(slots),
                Throws.ArgumentException.With.Message.Contains("unique position slots"));

            Assert.That(() => new ServePositionSlotV1(TeamSide.Home, 1,
                    new PlayerId("home-1"), new SimVector3(float.NaN, 0f, -6f)),
                Throws.ArgumentException.With.Message.Contains("finite"));
        }

        private static List<ServePositionSlotV1> LegalSlots(TeamSide side)
        {
            var sign = side == TeamSide.Home ? -1f : 1f;
            var lateralSign = side == TeamSide.Home ? 1f : -1f;
            return new List<ServePositionSlotV1>
            {
                Slot(side, 1, 3f * lateralSign, 6f * sign),
                Slot(side, 2, 3f * lateralSign, 2f * sign),
                Slot(side, 3, 0f, 2f * sign),
                Slot(side, 4, -3f * lateralSign, 2f * sign),
                Slot(side, 5, -3f * lateralSign, 6f * sign),
                Slot(side, 6, 0f, 6f * sign)
            };
        }

        private static List<ServePositionSlotV1> AllLegalSlots()
        {
            var slots = LegalSlots(TeamSide.Home);
            slots.AddRange(LegalSlots(TeamSide.Away));
            return slots;
        }

        private static ServePositionSlotV1 Slot(TeamSide side, int slot, float x, float z,
            string id = null)
        {
            return new ServePositionSlotV1(side, slot,
                new PlayerId(id ?? (side == TeamSide.Home ? "home-" : "away-") + slot),
                new SimVector3(x, 0f, z));
        }

        private static void Replace(List<ServePositionSlotV1> slots, TeamSide side,
            int slot, float x, float z)
        {
            var index = slots.FindIndex(value => value.Side == side && value.Slot == slot);
            slots[index] = Slot(side, slot, x, z);
        }
    }
}
