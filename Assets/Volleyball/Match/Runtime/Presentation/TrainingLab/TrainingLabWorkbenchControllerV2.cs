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
