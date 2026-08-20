using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation.TrainingLab;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class TrainingLabCourtProjectionV1Tests
    {
        [Test]
        public void CourtToBoard_UsesNetCenteredDepthAndBottomOriginLateralAxes()
        {
            var board = new Rect(0f, 0f, 900f, 450f);

            Assert.That(TrainingLabCourtProjectionV1.CourtToBoard(
                    board, new SimVector3(-4.5f, 0f, -9f)),
                Is.EqualTo(new Vector2(0f, 450f)));
            Assert.That(TrainingLabCourtProjectionV1.CourtToBoard(
                    board, new SimVector3(4.5f, 0f, 9f)),
                Is.EqualTo(new Vector2(900f, 0f)));
            Assert.That(TrainingLabCourtProjectionV1.CourtToBoard(
                    board, SimVector3.Zero),
                Is.EqualTo(new Vector2(450f, 225f)));
            Assert.That(TrainingLabCourtProjectionV1.CourtToBoard(
                    board, new SimVector3(4.5f, 0f, 0f)),
                Is.EqualTo(new Vector2(450f, 0f)));
            Assert.That(TrainingLabCourtProjectionV1.CourtToBoard(
                    board, new SimVector3(0f, 0f, 9f)),
                Is.EqualTo(new Vector2(900f, 225f)));
        }

        [Test]
        public void BoardToPlayerPosition_ClampsToThePlayerTeamHalfAndSnapsToTenthMeter()
        {
            var board = new Rect(0f, 0f, 900f, 450f);

            var home = TrainingLabCourtProjectionV1.BoardToPlayerPosition(
                board, new Vector2(899f, 0f), TeamSide.Home);
            var away = TrainingLabCourtProjectionV1.BoardToPlayerPosition(
                board, new Vector2(1f, 450f), TeamSide.Away);

            Assert.That(home.Z, Is.EqualTo(-.2f).Within(.001f));
            Assert.That(home.X, Is.EqualTo(4.2f).Within(.001f));
            Assert.That(away.Z, Is.EqualTo(.2f).Within(.001f));
            Assert.That(away.X, Is.EqualTo(-4.2f).Within(.001f));
        }

        [Test]
        public void SnapToTenth_PreservesTheHiddenHeightAxis()
        {
            var value = TrainingLabCourtProjectionV1.SnapPlayerPosition(
                new SimVector3(1.24f, .03f, -3.26f), TeamSide.Home);

            Assert.That(value, Is.EqualTo(new SimVector3(1.2f, .03f, -3.3f)));
        }

        [Test]
        public void CourtAndRulerDrags_UseOneSnappedMatchCoordinateSystem()
        {
            var board = new Rect(0f, 0f, 900f, 450f);
            var pointer = new Vector2(650f, 100f);
            var court = TrainingLabCourtProjectionV1.BoardToPlayerPosition(
                board, pointer, TeamSide.Away);
            var horizontal = TrainingLabCourtProjectionV1
                .HorizontalRulerToPlayerPosition(board, pointer.x,
                    new SimVector3(court.X, 0f, 2f), TeamSide.Away);
            var vertical = TrainingLabCourtProjectionV1
                .VerticalRulerToPlayerPosition(board, pointer.y,
                    new SimVector3(1f, 0f, court.Z), TeamSide.Away);

            Assert.That(horizontal, Is.EqualTo(court));
            Assert.That(vertical, Is.EqualTo(court));
            Assert.That(horizontal.X, Is.EqualTo(court.X));
            Assert.That(vertical.Z, Is.EqualTo(court.Z));
        }

        [Test]
        public void ShortestLegalCorrection_MovesTheViolatingPlayerToTheRequiredRelation()
        {
            var required = new ServePositionSlotV1(TeamSide.Home, 4,
                new PlayerId("home-4"), new SimVector3(-1f, 0f, -4f));
            var violating = new ServePositionSlotV1(TeamSide.Home, 5,
                new PlayerId("home-5"), new SimVector3(2f, 0f, -3f));
            var depthFault = new PositionFaultV1(TeamSide.Home,
                PositionFaultRuleV1.Slot4BehindSlot5, required, violating);
            var lateralFault = new PositionFaultV1(TeamSide.Home,
                PositionFaultRuleV1.Slot4RightOfSlot3, required, violating);

            Assert.That(TrainingLabCourtProjectionV1.ShortestLegalCorrection(
                    depthFault),
                Is.EqualTo(new SimVector3(2f, 0f, -4f)));
            Assert.That(TrainingLabCourtProjectionV1.ShortestLegalCorrection(
                    lateralFault),
                Is.EqualTo(new SimVector3(-1f, 0f, -3f)));
        }

        [Test]
        public void AwayCorrection_UsesTeamLocalLateralAxisThenReturnsWorldPoint()
        {
            var required = new ServePositionSlotV1(TeamSide.Away, 4,
                new PlayerId("away-4"), new SimVector3(1f, 0f, 4f));
            var violating = new ServePositionSlotV1(TeamSide.Away, 3,
                new PlayerId("away-3"), new SimVector3(-2f, 0f, 3f));
            var fault = new PositionFaultV1(TeamSide.Away,
                PositionFaultRuleV1.Slot4RightOfSlot3,
                required, violating);

            var result = TrainingLabCourtProjectionV1
                .ShortestLegalCorrection(fault);

            Assert.That(result, Is.EqualTo(new SimVector3(1f, 0f, 3f)));
        }

        [Test]
        public void ClampServeBallPosition_KeepsTheBallInTheCurrentServerBand()
        {
            var home = TrainingLabCourtProjectionV1.ClampServeBallPosition(
                new SimVector3(7f, 2f, 2f), TeamSide.Home);
            var away = TrainingLabCourtProjectionV1.ClampServeBallPosition(
                new SimVector3(-7f, 2f, -2f), TeamSide.Away);

            Assert.That(home, Is.EqualTo(new SimVector3(4.5f, 2f, -9f)));
            Assert.That(away, Is.EqualTo(new SimVector3(-4.5f, 2f, 9f)));
        }

        [Test]
        public void ServeBoard_UsesTheVisibleThreeMeterBandsOutsideTheCourt()
        {
            var board = new Rect(0f, 0f, 900f, 450f);
            var point = TrainingLabCourtProjectionV1.CourtToServeBoard(board,
                new SimVector3(0f, 2f, -12f));
            var result = TrainingLabCourtProjectionV1.ServeBoardToCourtPosition(
                board, point, 2f, TeamSide.Home);

            Assert.That(point, Is.EqualTo(new Vector2(-150f, 225f)));
            Assert.That(result, Is.EqualTo(new SimVector3(0f, 2f, -12f)));
        }
    }
}
