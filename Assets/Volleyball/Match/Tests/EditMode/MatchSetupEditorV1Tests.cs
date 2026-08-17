using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class MatchSetupEditorV1Tests
    {
        [Test]
        public void ExchangeRotation_ChangesOnlyTheRequestedTeamSlots()
        {
            var draft = Draft();
            var homeOne = draft.HomeRotation[0];
            var homeFour = draft.HomeRotation[3];
            var away = draft.AwayRotation.ToArray();

            new MatchSetupEditorV1(draft).ExchangeRotation(TeamSide.Home, 1, 4);

            Assert.That(draft.HomeRotation[0], Is.EqualTo(homeFour));
            Assert.That(draft.HomeRotation[3], Is.EqualTo(homeOne));
            Assert.That(draft.AwayRotation, Is.EqualTo(away));
            Assert.That(() => new MatchSetupEditorV1(draft)
                    .ExchangeRotation(TeamSide.Home, 1, 7),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void DefaultPoses_ArePointSymmetricInTeamLocalCoordinates()
        {
            var draft = Draft();
            for (var slot = 0; slot < 6; slot++)
            {
                var home = draft.Players.Single(value =>
                    value.PlayerId.Equals(draft.HomeRotation[slot])).Position;
                var away = draft.Players.Single(value =>
                    value.PlayerId.Equals(draft.AwayRotation[slot])).Position;
                Assert.That(away, Is.EqualTo(
                    new SimVector3(-home.X, home.Y, -home.Z)));
            }
        }

        [Test]
        public void SetPlayerPosition_SnapsAndClampsToThePlayersOwnHalf()
        {
            var draft = Draft();
            var editor = new MatchSetupEditorV1(draft);

            var actual = editor.SetPlayerPosition(
                draft.HomeRotation[0], new SimVector3(9.94f, 7f, 3.2f));

            Assert.That(actual, Is.EqualTo(new SimVector3(4.5f, 0f, 0f)));
            Assert.That(draft.Players.Single(value =>
                value.PlayerId.Equals(draft.HomeRotation[0])).Position,
                Is.EqualTo(actual));
        }

        [Test]
        public void SetBallPosition_UsesCurrentServersThreeMeterBand()
        {
            var draft = Draft();
            var editor = new MatchSetupEditorV1(draft);

            Assert.That(editor.SetBallPosition(new SimVector3(8f, .01f, 4f)),
                Is.EqualTo(new SimVector3(4.5f,
                    FormalCourtGeometryV1.BallRadiusMeters, -9f)));

            draft.FirstServingSide = TeamSide.Away;
            Assert.That(editor.SetBallPosition(new SimVector3(-8f, 3f, -4f)),
                Is.EqualTo(new SimVector3(-4.5f, 3f, 9f)));
        }

        [Test]
        public void SetBallVelocity_RejectsNonFiniteInputWithoutMutation()
        {
            var draft = Draft();
            var before = draft.BallVelocity;

            Assert.That(() => new MatchSetupEditorV1(draft).SetBallVelocity(
                    new SimVector3(float.NaN, 0f, 1f)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(draft.BallVelocity, Is.EqualTo(before));
        }

        [Test]
        public void PositionFaults_UseTheSamePointSymmetricTeamLocalFrame()
        {
            var draft = Draft();
            var editor = new MatchSetupEditorV1(draft);
            Assert.That(editor.EvaluatePositionFaults(), Is.Empty);

            var awaySlotFour = draft.AwayRotation[3];
            editor.SetPlayerPosition(awaySlotFour,
                new SimVector3(-3.5f, 0f, 2.4f));
            var fault = editor.EvaluatePositionFaults().Single(value =>
                value.Side == TeamSide.Away);
            var corrected = editor.ShortestLegalCorrection(fault);
            editor.SetPlayerPosition(fault.ViolatingBehindOrRight.PlayerId,
                corrected);

            Assert.That(editor.EvaluatePositionFaults().Where(value =>
                value.Side == TeamSide.Away && value.Rule == fault.Rule), Is.Empty);
        }

        [Test]
        public void Validate_RejectsRestoredOutOfBoundsSetupAndServeKinematics()
        {
            var draft = Draft();
            var players = draft.Players.Select((value, index) =>
                new MatchPlayerPoseDraftV1(value.PlayerId, index == 0
                    ? new SimVector3(0f, .1f, 2f)
                    : value.Position)).ToArray();
            Assert.That(() => Restore(draft, players: players),
                Throws.InvalidOperationException
                .With.Message.Contains("player pose"));

            draft = Draft();
            Assert.That(() => Restore(draft,
                    ballPosition: new SimVector3(0f, 2f, 0f)),
                Throws.InvalidOperationException
                .With.Message.Contains("three-metre band"));

            draft = Draft();
            Assert.That(() => Restore(draft,
                    ballVelocity: new SimVector3(0f, 0f, -1f)),
                Throws.InvalidOperationException
                .With.Message.Contains("toward the opponent"));

            Assert.That(() => Restore(draft,
                    ballVelocity: new SimVector3(0f, 0f, 41f)),
                Throws.InvalidOperationException
                .With.Message.Contains("40 m/s"));
        }

        private static MatchSetupDraftV1 Restore(MatchSetupDraftV1 source,
            MatchPlayerPoseDraftV1[] players = null,
            SimVector3? ballPosition = null,
            SimVector3? ballVelocity = null)
        {
            return MatchSetupDraftV1.Restore(source.BaseContext,
                source.FirstServingSide, source.HomeRotation,
                source.AwayRotation, players ?? source.Players.ToArray(),
                ballPosition ?? source.BallPosition,
                ballVelocity ?? source.BallVelocity,
                source.AttributeOverrides, source.RotationLocked);
        }

        private static MatchSetupDraftV1 Draft()
        {
            return MatchSetupDraftV1.CreateDefault(
                FormalSixVsSixRallyBootstrap.CreateDefaultFormalContextV5(),
                TeamSide.Home);
        }
    }
}
