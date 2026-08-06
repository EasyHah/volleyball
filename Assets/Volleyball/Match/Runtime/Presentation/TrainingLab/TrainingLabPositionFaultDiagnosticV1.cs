using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    public enum TrainingLabCorrectionAxisV1 { Depth, Lateral }

    // Presentation-only guidance for correcting one existing rule evaluation.
    public sealed class TrainingLabPositionFaultDiagnosticV1
    {
        public TrainingLabPositionFaultDiagnosticV1(
            PositionFaultV1 fault,
            TrainingLabCorrectionAxisV1 axis,
            int courtDirection,
            string text)
        {
            Fault = fault ?? throw new ArgumentNullException(nameof(fault));
            if (courtDirection == 0)
                throw new ArgumentOutOfRangeException(nameof(courtDirection));
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException(
                    "A correction description is required.", nameof(text));

            Axis = axis;
            CourtDirection = courtDirection;
            Text = text;
        }

        public PositionFaultV1 Fault { get; }
        public TrainingLabCorrectionAxisV1 Axis { get; }
        public int CourtDirection { get; }
        public string Text { get; }

        public static TrainingLabPositionFaultDiagnosticV1 Describe(
            PositionFaultV1 fault)
        {
            if (fault == null) throw new ArgumentNullException(nameof(fault));

            var lateral = IsLateral(fault.Rule);
            var axis = lateral
                ? TrainingLabCorrectionAxisV1.Lateral
                : TrainingLabCorrectionAxisV1.Depth;
            var direction = lateral
                ? 1
                : fault.Side == TeamSide.Home ? -1 : 1;
            var action = lateral ? "向右侧移动" : "向后场移动";
            var required = fault.RequiredAheadOrLeft;
            var violating = fault.ViolatingBehindOrRight;
            var text = fault.Side + " 的 " + violating.Slot + "号位应" +
                       action + "，以恢复与 " + required.Slot +
                       "号位的 " + fault.Rule + " 关系。";
            return new TrainingLabPositionFaultDiagnosticV1(
                fault, axis, direction, text);
        }

        public static IReadOnlyList<TrainingLabPositionFaultDiagnosticV1>
            DescribeAll(IReadOnlyList<PositionFaultV1> faults)
        {
            if (faults == null) throw new ArgumentNullException(nameof(faults));
            return new ReadOnlyCollection<TrainingLabPositionFaultDiagnosticV1>(
                faults.Select(Describe).ToArray());
        }

        private static bool IsLateral(PositionFaultRuleV1 rule)
        {
            return rule == PositionFaultRuleV1.Slot4RightOfSlot3 ||
                   rule == PositionFaultRuleV1.Slot3RightOfSlot2 ||
                   rule == PositionFaultRuleV1.Slot5RightOfSlot6 ||
                   rule == PositionFaultRuleV1.Slot6RightOfSlot1;
        }
    }
}
