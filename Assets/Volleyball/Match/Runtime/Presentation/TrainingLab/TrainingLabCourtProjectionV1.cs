using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    // Maps the one authoritative court coordinate system to the editable 2D board.
    public static class TrainingLabCourtProjectionV1
    {
        private const float SnapStep = .1f;
        private const float PlayerXLimit = 4.2f;
        private const float PlayerZLimit = 8.7f;
        private const float NetClearance = .2f;
        private const float ServeBandDepth = 3f;

        public static Vector2 CourtToBoard(Rect board, SimVector3 position)
        {
            return new Vector2(
                Mathf.Lerp(board.xMin, board.xMax,
                    Mathf.InverseLerp(-CourtBuilder.FormalHalfLength,
                        CourtBuilder.FormalHalfLength, position.Z)),
                Mathf.Lerp(board.yMax, board.yMin,
                    Mathf.InverseLerp(-CourtBuilder.HalfWidth,
                        CourtBuilder.HalfWidth, position.X)));
        }

        public static SimVector3 BoardToPlayerPosition(Rect board, Vector2 point,
            TeamSide side)
        {
            return SnapPlayerPosition(BoardToCourtPosition(board, point, 0f), side);
        }

        public static SimVector3 HorizontalRulerToPlayerPosition(
            Rect board,
            float pointerX,
            SimVector3 current,
            TeamSide side)
        {
            var depth = Mathf.Lerp(-PlayerZLimit, PlayerZLimit,
                Mathf.Clamp01((pointerX - board.xMin) / board.width));
            return SnapPlayerPosition(
                new SimVector3(current.X, current.Y, depth), side);
        }

        public static SimVector3 VerticalRulerToPlayerPosition(
            Rect board,
            float pointerY,
            SimVector3 current,
            TeamSide side)
        {
            var lateral = Mathf.Lerp(-PlayerXLimit, PlayerXLimit,
                Mathf.Clamp01((board.yMax - pointerY) / board.height));
            return SnapPlayerPosition(
                new SimVector3(lateral, current.Y, current.Z), side);
        }

        public static SimVector3 BoardToCourtPosition(Rect board, Vector2 point,
            float height)
        {
            var z = Mathf.Lerp(-PlayerZLimit, PlayerZLimit,
                Mathf.Clamp01((point.x - board.xMin) / board.width));
            var x = Mathf.Lerp(-PlayerXLimit, PlayerXLimit,
                Mathf.Clamp01((board.yMax - point.y) / board.height));
            return new SimVector3(x, height, z);
        }

        public static Vector2 CourtToServeBoard(Rect board, SimVector3 position)
        {
            var zScale = board.width / (CourtBuilder.FormalHalfLength * 2f);
            return new Vector2(
                board.xMin + (position.Z + CourtBuilder.FormalHalfLength) * zScale,
                Mathf.Lerp(board.yMax, board.yMin,
                    Mathf.InverseLerp(-CourtBuilder.HalfWidth,
                        CourtBuilder.HalfWidth, position.X)));
        }

        public static SimVector3 ServeBoardToCourtPosition(Rect board,
            Vector2 point, float height, TeamSide servingSide)
        {
            var zScale = board.width / (CourtBuilder.FormalHalfLength * 2f);
            var z = (point.x - board.xMin) / zScale -
                CourtBuilder.FormalHalfLength;
            var x = Mathf.Lerp(-CourtBuilder.HalfWidth, CourtBuilder.HalfWidth,
                Mathf.Clamp01((board.yMax - point.y) / board.height));
            return ClampServeBallPosition(new SimVector3(x, height, z),
                servingSide);
        }

        public static SimVector3 SnapPlayerPosition(SimVector3 position,
            TeamSide side)
        {
            var x = Mathf.Clamp(Snap(position.X), -PlayerXLimit, PlayerXLimit);
            var z = side == TeamSide.Home
                ? Mathf.Clamp(Snap(position.Z), -PlayerZLimit,
                    -NetClearance)
                : Mathf.Clamp(Snap(position.Z),
                    NetClearance, PlayerZLimit);
            return new SimVector3(x, position.Y, z);
        }

        // Equality is legal in the authoritative position evaluator, so this is
        // the shortest single-player move that resolves the selected relation.
        public static SimVector3 ShortestLegalCorrection(PositionFaultV1 fault)
        {
            if (fault == null) throw new System.ArgumentNullException(nameof(fault));
            var violating = TrainingTeamCourtTransformV1.ToLocal(fault.Side,
                fault.ViolatingBehindOrRight.FootProjection);
            var required = TrainingTeamCourtTransformV1.ToLocal(fault.Side,
                fault.RequiredAheadOrLeft.FootProjection);
            var local = fault.Rule switch
            {
                PositionFaultRuleV1.Slot4BehindSlot5 or
                PositionFaultRuleV1.Slot3BehindSlot6 or
                PositionFaultRuleV1.Slot2BehindSlot1 =>
                    new SimVector3(violating.X, violating.Y, required.Z),
                _ => new SimVector3(required.X, violating.Y, violating.Z)
            };
            return TrainingTeamCourtTransformV1.ToWorld(fault.Side, local);
        }

        public static SimVector3 ClampServeBallPosition(SimVector3 position,
            TeamSide servingSide)
        {
            var x = Mathf.Clamp(Snap(position.X), -CourtBuilder.HalfWidth,
                CourtBuilder.HalfWidth);
            var z = servingSide == TeamSide.Home
                ? Mathf.Clamp(Snap(position.Z),
                    -CourtBuilder.FormalHalfLength - ServeBandDepth,
                    -CourtBuilder.FormalHalfLength)
                : Mathf.Clamp(Snap(position.Z),
                    CourtBuilder.FormalHalfLength,
                    CourtBuilder.FormalHalfLength + ServeBandDepth);
            return new SimVector3(x, position.Y, z);
        }

        private static float Snap(float value)
        {
            return Mathf.Round(value / SnapStep) * SnapStep;
        }
    }
}
