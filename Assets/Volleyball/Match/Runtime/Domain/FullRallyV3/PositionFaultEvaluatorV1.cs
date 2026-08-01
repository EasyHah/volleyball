using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum PositionFaultRuleV1
    {
        Slot4BehindSlot5 = 0,
        Slot3BehindSlot6 = 1,
        Slot2BehindSlot1 = 2,
        Slot4RightOfSlot3 = 3,
        Slot3RightOfSlot2 = 4,
        Slot5RightOfSlot6 = 5,
        Slot6RightOfSlot1 = 6
    }

    // Frozen foot projection of one player at the instant a serve is contacted.
    public sealed class ServePositionSlotV1
    {
        public ServePositionSlotV1(TeamSide side, int slot, PlayerId playerId,
            SimVector3 footProjection)
        {
            if (!Enum.IsDefined(typeof(TeamSide), side))
                throw new ArgumentOutOfRangeException(nameof(side));
            if (slot < 1 || slot > 6)
                throw new ArgumentOutOfRangeException(nameof(slot));
            if (string.IsNullOrWhiteSpace(playerId.Value))
                throw new ArgumentException("A position slot requires a player ID.", nameof(playerId));
            if (!footProjection.IsFinite)
                throw new ArgumentException("A position slot requires a finite foot projection.",
                    nameof(footProjection));

            Side = side;
            Slot = slot;
            PlayerId = playerId;
            FootProjection = footProjection;
        }

        public TeamSide Side { get; }
        public int Slot { get; }
        public PlayerId PlayerId { get; }
        public SimVector3 FootProjection { get; }
    }

    public sealed class PositionFaultV1
    {
        public PositionFaultV1(TeamSide side, PositionFaultRuleV1 rule,
            ServePositionSlotV1 requiredAheadOrLeft,
            ServePositionSlotV1 violatingBehindOrRight)
        {
            Side = side;
            Rule = rule;
            RequiredAheadOrLeft = requiredAheadOrLeft ??
                throw new ArgumentNullException(nameof(requiredAheadOrLeft));
            ViolatingBehindOrRight = violatingBehindOrRight ??
                throw new ArgumentNullException(nameof(violatingBehindOrRight));
        }

        public TeamSide Side { get; }
        public PositionFaultRuleV1 Rule { get; }
        public ServePositionSlotV1 RequiredAheadOrLeft { get; }
        public ServePositionSlotV1 ViolatingBehindOrRight { get; }
    }

    // Applies the strict serve-contact ordering rule. Equality is legal; epsilon only absorbs float noise.
    public static class PositionFaultEvaluatorV1
    {
        private const float ProjectionComparisonEpsilon = 0.00001f;

        public static IReadOnlyList<PositionFaultV1> Evaluate(
            IReadOnlyList<ServePositionSlotV1> slots)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (slots.Count != 12)
                throw new ArgumentException("Exactly six position slots per team are required.",
                    nameof(slots));

            var home = ValidateAndIndex(slots, TeamSide.Home);
            var away = ValidateAndIndex(slots, TeamSide.Away);
            var faults = new List<PositionFaultV1>();
            AddFaults(faults, TeamSide.Home, home);
            AddFaults(faults, TeamSide.Away, away);
            return new ReadOnlyCollection<PositionFaultV1>(faults);
        }

        private static ServePositionSlotV1[] ValidateAndIndex(
            IReadOnlyList<ServePositionSlotV1> slots, TeamSide side)
        {
            var indexed = new ServePositionSlotV1[7];
            var players = new HashSet<PlayerId>();
            foreach (var value in slots)
            {
                if (value == null)
                    throw new ArgumentException("Position slots cannot contain null.", nameof(slots));
                if (value.Side != side) continue;
                if (indexed[value.Slot] != null)
                    throw new ArgumentException("Each team must contain unique position slots.", nameof(slots));
                if (!players.Add(value.PlayerId))
                    throw new ArgumentException("Each team must contain unique player IDs.", nameof(slots));
                if (!value.FootProjection.IsFinite)
                    throw new ArgumentException("Position projections must be finite.", nameof(slots));
                indexed[value.Slot] = value;
            }

            for (var slot = 1; slot <= 6; slot++)
            {
                if (indexed[slot] == null)
                    throw new ArgumentException("Each team must contain slots one through six.",
                        nameof(slots));
            }

            return indexed;
        }

        private static void AddFaults(ICollection<PositionFaultV1> faults,
            TeamSide side, ServePositionSlotV1[] slots)
        {
            // A front-row player must be closer to the net than its paired back-row player.
            AddDepthFault(faults, side, PositionFaultRuleV1.Slot4BehindSlot5, slots[4], slots[5]);
            AddDepthFault(faults, side, PositionFaultRuleV1.Slot3BehindSlot6, slots[3], slots[6]);
            AddDepthFault(faults, side, PositionFaultRuleV1.Slot2BehindSlot1, slots[2], slots[1]);

            // X is the shared court-left/right axis for both teams' frozen court projections.
            AddLateralFault(faults, side, PositionFaultRuleV1.Slot4RightOfSlot3, slots[4], slots[3]);
            AddLateralFault(faults, side, PositionFaultRuleV1.Slot3RightOfSlot2, slots[3], slots[2]);
            AddLateralFault(faults, side, PositionFaultRuleV1.Slot5RightOfSlot6, slots[5], slots[6]);
            AddLateralFault(faults, side, PositionFaultRuleV1.Slot6RightOfSlot1, slots[6], slots[1]);
        }

        private static void AddDepthFault(ICollection<PositionFaultV1> faults,
            TeamSide side, PositionFaultRuleV1 rule, ServePositionSlotV1 front,
            ServePositionSlotV1 back)
        {
            var frontDepthTowardNet = side == TeamSide.Home
                ? front.FootProjection.Z : -front.FootProjection.Z;
            var backDepthTowardNet = side == TeamSide.Home
                ? back.FootProjection.Z : -back.FootProjection.Z;
            if (frontDepthTowardNet + ProjectionComparisonEpsilon < backDepthTowardNet)
                faults.Add(new PositionFaultV1(side, rule, front, back));
        }

        private static void AddLateralFault(ICollection<PositionFaultV1> faults,
            TeamSide side, PositionFaultRuleV1 rule, ServePositionSlotV1 left,
            ServePositionSlotV1 right)
        {
            if (left.FootProjection.X > right.FootProjection.X + ProjectionComparisonEpsilon)
                faults.Add(new PositionFaultV1(side, rule, left, right));
        }
    }
}
