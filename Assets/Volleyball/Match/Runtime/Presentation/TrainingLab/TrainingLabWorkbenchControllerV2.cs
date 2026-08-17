using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    public enum TrainingServeViewV1 { Top, Side }

    /// <summary>
    /// Native V5 authoring state for the unified workbench. UI gestures are
    /// translated into Match-owned editor commands here; UI never mutates a
    /// rotation list directly.
    /// </summary>
    public sealed class TrainingLabWorkbenchControllerV2
    {
        private readonly MatchSetupEditorV1 _editor;

        public TrainingLabWorkbenchControllerV2(MatchSetupDraftV1 setup)
        {
            MatchSetup = setup ?? throw new ArgumentNullException(nameof(setup));
            _editor = new MatchSetupEditorV1(setup);
            CurrentStep = setup.RotationLocked
                ? TrainingLabStepV1.Positioning
                : TrainingLabStepV1.Rotation;
            SelectedObjectId = "ball";
        }

        public event Action Changed;

        public MatchSetupDraftV1 MatchSetup { get; }
        public TrainingLabStepV1 CurrentStep { get; private set; }
        public string SelectedObjectId { get; private set; }
        public IReadOnlyList<PlayerId> FocusedPlayerIds { get; private set; } =
            Array.Empty<PlayerId>();
        public IReadOnlyList<PositionFaultV1> PositionFaults =>
            _editor.EvaluatePositionFaults();
        public bool CanEnterServeSetup =>
            MatchSetup.RotationLocked && PositionFaults.Count == 0;
        public string ServeSetupBlockReason => !MatchSetup.RotationLocked
            ? "Confirm rotation before configuring the serve."
            : PositionFaults.Count > 0
                ? "Resolve all position faults before configuring the serve."
                : string.Empty;
        public TrainingServeViewV1 ActiveServeView { get; private set; } =
            TrainingServeViewV1.Top;
        public TrainingServeToolV1 ServeTool { get; private set; } =
            TrainingServeToolV1.MoveBall;
        public string LastEditFailure { get; private set; } = string.Empty;

        public bool TryDropRotationCard(
            TeamSide sourceSide,
            int sourceSlot,
            TeamSide? targetSide,
            int? targetSlot)
        {
            if (!targetSide.HasValue || !targetSlot.HasValue ||
                sourceSide != targetSide.Value)
                return false;
            EnsureRotationEditable();
            _editor.ExchangeRotation(sourceSide, sourceSlot,
                targetSlot.Value);
            Changed?.Invoke();
            return true;
        }

        public void ExchangeRotation(
            TeamSide side,
            int firstSlot,
            int secondSlot)
        {
            EnsureRotationEditable();
            _editor.ExchangeRotation(side, firstSlot, secondSlot);
            Changed?.Invoke();
        }

        public void ConfirmRotation()
        {
            EnsureRotationEditable();
            _editor.Validate();
            MatchSetup.RotationLocked = true;
            CurrentStep = TrainingLabStepV1.Positioning;
            Changed?.Invoke();
        }

        public void ReopenRotation()
        {
            MatchSetup.RotationLocked = false;
            CurrentStep = TrainingLabStepV1.Rotation;
            Changed?.Invoke();
        }

        public SimVector3 SetPlayerPosition(PlayerId playerId,
            SimVector3 position)
        {
            EnsurePositioning();
            var result = _editor.SetPlayerPosition(playerId, position);
            SelectedObjectId = playerId.Value;
            FocusedPlayerIds = new[] { playerId };
            Changed?.Invoke();
            return result;
        }

        public SimVector3 SetPlayerPositionFromCourt(
            PlayerId playerId,
            Rect board,
            Vector2 pointer)
        {
            return SetPlayerPosition(playerId,
                TrainingLabCourtProjectionV1.BoardToPlayerPosition(
                    board, pointer, SideFor(playerId)));
        }

        public SimVector3 SetPlayerDepthFromHorizontalRuler(
            PlayerId playerId,
            Rect board,
            float pointerX)
        {
            EnsurePositioning();
            var current = PoseFor(playerId).Position;
            return SetPlayerPosition(playerId,
                TrainingLabCourtProjectionV1.HorizontalRulerToPlayerPosition(
                    board, pointerX, current, SideFor(playerId)));
        }

        public SimVector3 SetPlayerLateralFromVerticalRuler(
            PlayerId playerId,
            Rect board,
            float pointerY)
        {
            EnsurePositioning();
            var current = PoseFor(playerId).Position;
            return SetPlayerPosition(playerId,
                TrainingLabCourtProjectionV1.VerticalRulerToPlayerPosition(
                    board, pointerY, current, SideFor(playerId)));
        }

        public void FocusPositionFault(int index)
        {
            var faults = PositionFaults;
            if (index < 0 || index >= faults.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var fault = faults[index];
            FocusedPlayerIds = new[]
            {
                fault.RequiredAheadOrLeft.PlayerId,
                fault.ViolatingBehindOrRight.PlayerId
            };
            SelectedObjectId = fault.ViolatingBehindOrRight.PlayerId.Value;
            Changed?.Invoke();
        }

        public void ContinueToServeSetup()
        {
            EnsurePositioning();
            if (!CanEnterServeSetup)
                throw new InvalidOperationException(ServeSetupBlockReason);
            CurrentStep = TrainingLabStepV1.ServeBall;
            Changed?.Invoke();
        }

        public void SetServeView(TrainingServeViewV1 view)
        {
            if (!Enum.IsDefined(typeof(TrainingServeViewV1), view))
                throw new ArgumentOutOfRangeException(nameof(view));
            EnsureServeSetup();
            ActiveServeView = view;
            Changed?.Invoke();
        }

        public void SetServeTool(TrainingServeToolV1 tool)
        {
            if (!Enum.IsDefined(typeof(TrainingServeToolV1), tool))
                throw new ArgumentOutOfRangeException(nameof(tool));
            EnsureServeSetup();
            ServeTool = tool;
            Changed?.Invoke();
        }

        public bool TrySetBallFromTop(float x, float z)
        {
            EnsureServeEditable(TrainingServeViewV1.Top,
                TrainingServeToolV1.MoveBall);
            return TryEdit(() => _editor.SetBallPosition(new SimVector3(
                x, MatchSetup.BallPosition.Y, z)));
        }

        public bool TrySetBallFromSide(float z, float y)
        {
            EnsureServeEditable(TrainingServeViewV1.Side,
                TrainingServeToolV1.MoveBall);
            return TryEdit(() => _editor.SetBallPosition(new SimVector3(
                MatchSetup.BallPosition.X, y, z)));
        }

        public bool TrySetVelocityFromTop(float vx, float vz)
        {
            EnsureServeEditable(TrainingServeViewV1.Top,
                TrainingServeToolV1.AdjustVelocity);
            return TryEdit(() => _editor.SetBallVelocity(new SimVector3(
                vx, MatchSetup.BallVelocity.Y, vz)));
        }

        public bool TrySetVelocityFromSide(float vz, float vy)
        {
            EnsureServeEditable(TrainingServeViewV1.Side,
                TrainingServeToolV1.AdjustVelocity);
            return TryEdit(() => _editor.SetBallVelocity(new SimVector3(
                MatchSetup.BallVelocity.X, vy, vz)));
        }

        public void SetFirstServingSide(TeamSide side)
        {
            if (!Enum.IsDefined(typeof(TeamSide), side))
                throw new ArgumentOutOfRangeException(nameof(side));
            EnsureServeSetup();
            if (side == MatchSetup.FirstServingSide) return;
            var previous = MatchSetup.BallPosition;
            MatchSetup.FirstServingSide = side;
            _editor.SetBallPosition(new SimVector3(previous.X, previous.Y,
                -previous.Z));
            Changed?.Invoke();
        }

        public IReadOnlyList<SimVector3> PredictTrajectory(int maximumSteps = 180)
        {
            EnsureServeSetup();
            if (maximumSteps <= 0) throw new ArgumentOutOfRangeException(
                nameof(maximumSteps));
            var state = new BallState(MatchSetup.BallPosition,
                MatchSetup.BallVelocity, SimulatedBall.DefaultRadius);
            var parameters = new BallSimulationParameters(-9.8f, .9995f);
            var points = new List<SimVector3> { state.Position };
            for (var step = 0; step < maximumSteps; step++)
            {
                BallIntegrator.Step(state, SimulatedBall.DefaultFixedStep,
                    parameters);
                points.Add(state.Position);
                if (state.Position.Y <= SimulatedBall.DefaultRadius) break;
            }
            return points;
        }

        public void SelectObject(PlayerId playerId)
        {
            SelectedObjectId = string.IsNullOrWhiteSpace(playerId.Value)
                ? throw new ArgumentException("Player ID is required.",
                    nameof(playerId))
                : playerId.Value;
            Changed?.Invoke();
        }

        private void EnsureRotationEditable()
        {
            if (MatchSetup.RotationLocked ||
                CurrentStep != TrainingLabStepV1.Rotation)
                throw new InvalidOperationException(
                    "Reopen the rotation page before changing Match slots.");
        }

        private void EnsurePositioning()
        {
            if (!MatchSetup.RotationLocked ||
                CurrentStep != TrainingLabStepV1.Positioning)
                throw new InvalidOperationException(
                    "Player positions are editable only on the top positioning page.");
        }

        private void EnsureServeSetup()
        {
            if (CurrentStep != TrainingLabStepV1.ServeBall)
                throw new InvalidOperationException(
                    "The serve setup page is not active.");
        }

        private void EnsureServeEditable(
            TrainingServeViewV1 requiredView,
            TrainingServeToolV1 requiredTool)
        {
            EnsureServeSetup();
            if (ActiveServeView != requiredView || ServeTool != requiredTool)
                throw new InvalidOperationException(
                    "The selected serve view and tool do not allow this edit.");
        }

        private bool TryEdit(Action edit)
        {
            var position = MatchSetup.BallPosition;
            var velocity = MatchSetup.BallVelocity;
            try
            {
                edit();
                LastEditFailure = string.Empty;
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                              exception is InvalidOperationException)
            {
                _editor.SetBallPosition(position);
                _editor.SetBallVelocity(velocity);
                LastEditFailure = exception.Message;
                Changed?.Invoke();
                return false;
            }
        }

        private MatchPlayerPoseDraftV1 PoseFor(PlayerId playerId)
        {
            return MatchSetup.Players.Single(value =>
                value.PlayerId.Equals(playerId));
        }

        private TeamSide SideFor(PlayerId playerId)
        {
            if (MatchSetup.BaseContext.Home.RotationOrder.Any(value =>
                    value.PlayerId.Equals(playerId))) return TeamSide.Home;
            if (MatchSetup.BaseContext.Away.RotationOrder.Any(value =>
                    value.PlayerId.Equals(playerId))) return TeamSide.Away;
            throw new ArgumentException(
                "Player is not a member of the V5 Match setup.",
                nameof(playerId));
        }
    }
}
