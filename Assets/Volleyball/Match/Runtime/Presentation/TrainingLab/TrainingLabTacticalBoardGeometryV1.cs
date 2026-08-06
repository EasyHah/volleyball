using System;
using UnityEngine;
using Volleyball.Domain.Simulation;

namespace Volleyball.Presentation.TrainingLab
{
    /// <summary>
    /// The orthographic planes offered by the precise-adjustment surface.
    /// Each plane names the two visible axes; the third is preserved.
    /// </summary>
    public enum TrainingLabPrecisionPlaneV1 { XY, ZY, XZ }

    /// <summary>
    /// Pure mapping between board-local UI coordinates and formal-court
    /// coordinates. Board authoring never depends on a camera or physics.
    /// </summary>
    public static class TrainingLabTacticalBoardGeometryV1
    {
        private const float PlayerXLimit =
            CourtBuilder.HalfWidth - PrototypePlayerAgent.BoundaryClearance;
        private const float PlayerZLimit =
            CourtBuilder.FormalHalfLength - PrototypePlayerAgent.BoundaryClearance;

        /// <summary>
        /// Maps a point inside <paramref name="board"/> to a court position at
        /// height <paramref name="y"/>. Board top (yMax) is the far court
        /// (positive Z); pointers outside the board clamp to the player bounds.
        /// </summary>
        public static SimVector3 BoardToCourt(Rect board, Vector2 point, float y)
        {
            EnsureBoard(board);
            var x = Mathf.Lerp(-PlayerXLimit, PlayerXLimit,
                Mathf.Clamp01((point.x - board.xMin) / board.width));
            var z = Mathf.Lerp(-PlayerZLimit, PlayerZLimit,
                Mathf.Clamp01((board.yMax - point.y) / board.height));
            return new SimVector3(x, y, z);
        }

        /// <summary>
        /// Inverse of <see cref="BoardToCourt"/>: maps a court position back to
        /// board-local coordinates.
        /// </summary>
        public static Vector2 CourtToBoard(Rect board, SimVector3 point)
        {
            EnsureBoard(board);
            return new Vector2(
                Mathf.Lerp(board.xMin, board.xMax,
                    Mathf.InverseLerp(-PlayerXLimit, PlayerXLimit, point.X)),
                Mathf.Lerp(board.yMax, board.yMin,
                    Mathf.InverseLerp(-PlayerZLimit, PlayerZLimit, point.Z)));
        }

        /// <summary>
        /// Replaces the two visible axes of <paramref name="current"/> with the
        /// supplied values, preserving the hidden axis.
        /// </summary>
        public static SimVector3 ReplaceVisibleAxes(
            TrainingLabPrecisionPlaneV1 plane, SimVector3 current,
            float horizontal, float vertical)
        {
            return plane switch
            {
                TrainingLabPrecisionPlaneV1.XY =>
                    new SimVector3(horizontal, vertical, current.Z),
                TrainingLabPrecisionPlaneV1.ZY =>
                    new SimVector3(current.X, vertical, horizontal),
                TrainingLabPrecisionPlaneV1.XZ =>
                    new SimVector3(horizontal, current.Y, vertical),
                _ => throw new ArgumentOutOfRangeException(nameof(plane))
            };
        }

        private static void EnsureBoard(Rect board)
        {
            if (board.width <= 0f || board.height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(board));
        }
    }
}
