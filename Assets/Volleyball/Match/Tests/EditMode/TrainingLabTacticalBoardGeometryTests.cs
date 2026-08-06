using System;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingLabTacticalBoardGeometryTests
    {
        [Test]
        public void BoardToCourt_MapsEdgesToFormalPlayerBounds()
        {
            var board = new Rect(100f, 40f, 900f, 600f);

            Assert.That(
                TrainingLabTacticalBoardGeometryV1.BoardToCourt(
                    board, board.min, 0f),
                Is.EqualTo(new SimVector3(-4.25f, 0f, 8.75f)));
            Assert.That(
                TrainingLabTacticalBoardGeometryV1.BoardToCourt(
                    board, board.max, 0f),
                Is.EqualTo(new SimVector3(4.25f, 0f, -8.75f)));
        }

        [Test]
        public void BoardToCourt_ClampsOutsidePointer()
        {
            var board = new Rect(0f, 0f, 100f, 100f);
            var result = TrainingLabTacticalBoardGeometryV1.BoardToCourt(
                board, new Vector2(-10f, 120f), 0f);

            Assert.That(result, Is.EqualTo(new SimVector3(-4.25f, 0f, -8.75f)));
        }

        [Test]
        public void CourtToBoard_RoundTripsBoardToCourt()
        {
            var board = new Rect(100f, 40f, 900f, 600f);
            var point = new Vector2(280f, 130f);
            var y = 1.2f;

            var court = TrainingLabTacticalBoardGeometryV1.BoardToCourt(
                board, point, y);
            var mapped = TrainingLabTacticalBoardGeometryV1.CourtToBoard(
                board, court);

            Assert.That(mapped.x, Is.EqualTo(point.x).Within(0.001f));
            Assert.That(mapped.y, Is.EqualTo(point.y).Within(0.001f));
        }

        [Test]
        public void CourtToBoard_ClampsOutsidePlayerBounds()
        {
            var board = new Rect(0f, 0f, 100f, 100f);

            var mapped = TrainingLabTacticalBoardGeometryV1.CourtToBoard(
                board, new SimVector3(99f, 0f, -99f));

            Assert.That(mapped, Is.EqualTo(new Vector2(100f, 100f)));
        }

        [Test]
        public void ReplaceVisibleAxes_PreservesTheHiddenAxis()
        {
            var source = new SimVector3(1f, 2f, 3f);
            Assert.That(
                TrainingLabTacticalBoardGeometryV1.ReplaceVisibleAxes(
                    TrainingLabPrecisionPlaneV1.XY, source, 7f, 8f),
                Is.EqualTo(new SimVector3(7f, 8f, 3f)));
            Assert.That(
                TrainingLabTacticalBoardGeometryV1.ReplaceVisibleAxes(
                    TrainingLabPrecisionPlaneV1.ZY, source, 7f, 8f),
                Is.EqualTo(new SimVector3(1f, 8f, 7f)));
            Assert.That(
                TrainingLabTacticalBoardGeometryV1.ReplaceVisibleAxes(
                    TrainingLabPrecisionPlaneV1.XZ, source, 7f, 8f),
                Is.EqualTo(new SimVector3(7f, 2f, 8f)));
        }

        [Test]
        public void BoardToCourt_ThrowsOnInvalidBoardDimensions()
        {
            Assert.That(() => TrainingLabTacticalBoardGeometryV1.BoardToCourt(
                    new Rect(0f, 0f, 0f, 100f), Vector2.zero, 0f),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName")
                    .EqualTo("board"));
            Assert.That(() => TrainingLabTacticalBoardGeometryV1.BoardToCourt(
                    new Rect(0f, 0f, 100f, -1f), Vector2.zero, 0f),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName")
                    .EqualTo("board"));
            Assert.That(() => TrainingLabTacticalBoardGeometryV1.CourtToBoard(
                    new Rect(0f, 0f, 0f, 0f), SimVector3.Zero),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Property("ParamName")
                    .EqualTo("board"));
        }
    }
}
