using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.PreServe
{
    public sealed class MatchSetupEditorV1
    {
        private const float MaximumServeLaunchSpeedMetersPerSecond = 40f;
        private readonly MatchSetupDraftV1 _draft;

        public MatchSetupEditorV1(MatchSetupDraftV1 draft)
        {
            _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        }

        public void ExchangeRotation(TeamSide side, int firstSlot, int secondSlot)
        {
            var rotation = Rotation(side);
            ValidateSlot(firstSlot);
            ValidateSlot(secondSlot);
            if (firstSlot == secondSlot) return;
            (rotation[firstSlot - 1], rotation[secondSlot - 1]) =
                (rotation[secondSlot - 1], rotation[firstSlot - 1]);
        }

        public SimVector3 SetPlayerPosition(PlayerId playerId, SimVector3 value)
        {
            if (!value.IsFinite) throw new ArgumentOutOfRangeException(nameof(value));
            var side = SideFor(playerId);
            var result = new SimVector3(
                Snap(Clamp(value.X, -FormalCourtGeometryV1.HalfWidthMeters,
                    FormalCourtGeometryV1.HalfWidthMeters)),
                0f,
                Snap(side == TeamSide.Home
                    ? Clamp(value.Z, -FormalCourtGeometryV1.HalfLengthMeters, 0f)
                    : Clamp(value.Z, 0f, FormalCourtGeometryV1.HalfLengthMeters)));
            var player = _draft.Players.Single(candidate =>
                candidate.PlayerId.Equals(playerId));
            player.Position = result;
            return result;
        }

        public SimVector3 SetBallPosition(SimVector3 value)
        {
            if (!value.IsFinite) throw new ArgumentOutOfRangeException(nameof(value));
            var minimumDepth = _draft.FirstServingSide == TeamSide.Home
                ? -FormalCourtGeometryV1.HalfLengthMeters -
                  FormalCourtGeometryV1.ServeBandDepthMeters
                : FormalCourtGeometryV1.HalfLengthMeters;
            var maximumDepth = _draft.FirstServingSide == TeamSide.Home
                ? -FormalCourtGeometryV1.HalfLengthMeters
                : FormalCourtGeometryV1.HalfLengthMeters +
                  FormalCourtGeometryV1.ServeBandDepthMeters;
            _draft.BallPosition = new SimVector3(
                Clamp(value.X, -FormalCourtGeometryV1.HalfWidthMeters,
                    FormalCourtGeometryV1.HalfWidthMeters),
                Math.Max(FormalCourtGeometryV1.BallRadiusMeters, value.Y),
                Clamp(value.Z, minimumDepth, maximumDepth));
            return _draft.BallPosition;
        }

        public SimVector3 SetBallVelocity(SimVector3 value)
        {
            if (!value.IsFinite) throw new ArgumentOutOfRangeException(nameof(value));
            _draft.BallVelocity = value;
            return value;
        }

        public MatchSetupSnapshotV1 Freeze()
        {
            Validate();
            return MatchSetupSnapshotV1.Create(_draft);
        }

        public IReadOnlyList<PositionFaultV1> EvaluatePositionFaults()
        {
            var slots = new List<ServePositionSlotV1>(12);
            AddSlots(slots, TeamSide.Home, _draft.HomeRotation);
            AddSlots(slots, TeamSide.Away, _draft.AwayRotation);
            return PositionFaultEvaluatorV1.Evaluate(slots,
                PositionFaultCoordinateFrameV1.TeamLocalPointSymmetric);
        }

        public SimVector3 ShortestLegalCorrection(PositionFaultV1 fault)
        {
            if (fault == null) throw new ArgumentNullException(nameof(fault));
            var required = TrainingTeamCourtTransformV1.ToLocal(
                fault.Side, fault.RequiredAheadOrLeft.FootProjection);
            var violating = TrainingTeamCourtTransformV1.ToLocal(
                fault.Side, fault.ViolatingBehindOrRight.FootProjection);
            SimVector3 corrected;
            switch (fault.Rule)
            {
                case PositionFaultRuleV1.Slot4BehindSlot5:
                case PositionFaultRuleV1.Slot3BehindSlot6:
                case PositionFaultRuleV1.Slot2BehindSlot1:
                    corrected = new SimVector3(violating.X, violating.Y,
                        Snap(required.Z + .1f));
                    break;
                default:
                    corrected = new SimVector3(Snap(required.X + .1f),
                        violating.Y, violating.Z);
                    break;
            }
            return TrainingTeamCourtTransformV1.ToWorld(fault.Side, corrected);
        }

        public void Validate()
        {
            ValidateRotation(_draft.BaseContext.Home, _draft.HomeRotation);
            ValidateRotation(_draft.BaseContext.Away, _draft.AwayRotation);
            if (_draft.Players.Count != 12 ||
                _draft.Players.Select(value => value.PlayerId).Distinct().Count() != 12)
                throw new InvalidOperationException(
                    "A Match setup requires exactly twelve unique player poses.");
            foreach (var player in _draft.Players)
            {
                var side = SideFor(player.PlayerId);
                if (!player.Position.IsFinite ||
                    player.Position.X < -FormalCourtGeometryV1.HalfWidthMeters ||
                    player.Position.X > FormalCourtGeometryV1.HalfWidthMeters ||
                    Math.Abs(player.Position.Y) > 0.00001f ||
                    player.Position.Z < -FormalCourtGeometryV1.HalfLengthMeters ||
                    player.Position.Z > FormalCourtGeometryV1.HalfLengthMeters ||
                    (side == TeamSide.Home && player.Position.Z > 0f) ||
                    (side == TeamSide.Away && player.Position.Z < 0f))
                    throw new InvalidOperationException("A player pose is outside the formal court.");
            }
            if (!_draft.BallPosition.IsFinite || !_draft.BallVelocity.IsFinite)
                throw new InvalidOperationException("Ball state must be finite.");
            var minimumDepth = _draft.FirstServingSide == TeamSide.Home
                ? -FormalCourtGeometryV1.HalfLengthMeters -
                  FormalCourtGeometryV1.ServeBandDepthMeters
                : FormalCourtGeometryV1.HalfLengthMeters;
            var maximumDepth = _draft.FirstServingSide == TeamSide.Home
                ? -FormalCourtGeometryV1.HalfLengthMeters
                : FormalCourtGeometryV1.HalfLengthMeters +
                  FormalCourtGeometryV1.ServeBandDepthMeters;
            if (_draft.BallPosition.X < -FormalCourtGeometryV1.HalfWidthMeters ||
                _draft.BallPosition.X > FormalCourtGeometryV1.HalfWidthMeters ||
                _draft.BallPosition.Y < FormalCourtGeometryV1.BallRadiusMeters ||
                _draft.BallPosition.Z < minimumDepth ||
                _draft.BallPosition.Z > maximumDepth)
                throw new InvalidOperationException(
                    "The serve ball must be inside the current server's legal three-metre band.");
            var towardOpponent = _draft.FirstServingSide == TeamSide.Home
                ? _draft.BallVelocity.Z
                : -_draft.BallVelocity.Z;
            if (towardOpponent <= 0f)
                throw new InvalidOperationException(
                    "The serve velocity must travel toward the opponent.");
            if (_draft.BallVelocity.Magnitude >
                MaximumServeLaunchSpeedMetersPerSecond)
                throw new InvalidOperationException(
                    "The serve launch speed exceeds the supported 40 m/s maximum.");
            foreach (var pair in _draft.AttributeOverrides)
            {
                SideFor(pair.Key);
                if (pair.Value == null)
                    throw new InvalidOperationException("Attribute overrides cannot be null.");
            }
        }

        private void AddSlots(ICollection<ServePositionSlotV1> output,
            TeamSide side, IReadOnlyList<PlayerId> rotation)
        {
            for (var index = 0; index < rotation.Count; index++)
            {
                var player = _draft.Players.Single(value =>
                    value.PlayerId.Equals(rotation[index]));
                output.Add(new ServePositionSlotV1(
                    side, index + 1, rotation[index], player.Position));
            }
        }

        private List<PlayerId> Rotation(TeamSide side)
        {
            return side switch
            {
                TeamSide.Home => _draft.HomeRotation,
                TeamSide.Away => _draft.AwayRotation,
                _ => throw new ArgumentOutOfRangeException(nameof(side))
            };
        }

        private TeamSide SideFor(PlayerId playerId)
        {
            if (_draft.BaseContext.Home.RotationOrder.Any(value =>
                    value.PlayerId.Equals(playerId))) return TeamSide.Home;
            if (_draft.BaseContext.Away.RotationOrder.Any(value =>
                    value.PlayerId.Equals(playerId))) return TeamSide.Away;
            throw new ArgumentException("Player is not a member of the V5 context.",
                nameof(playerId));
        }

        private static void ValidateRotation(
            TeamSnapshotV5 team,
            IReadOnlyList<PlayerId> rotation)
        {
            if (rotation == null || rotation.Count != 6 ||
                rotation.Distinct().Count() != 6)
                throw new InvalidOperationException(
                    "Each Match setup rotation must contain six unique players.");
            var members = new HashSet<PlayerId>(team.RotationOrder.Select(value =>
                value.PlayerId));
            if (rotation.Any(value => !members.Contains(value)))
                throw new InvalidOperationException(
                    "A Match setup rotation contains a player from another team.");
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 1 || slot > 6) throw new ArgumentOutOfRangeException(nameof(slot));
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static float Snap(float value)
        {
            return (float)Math.Round(value * 10f, MidpointRounding.AwayFromZero) / 10f;
        }
    }
}
