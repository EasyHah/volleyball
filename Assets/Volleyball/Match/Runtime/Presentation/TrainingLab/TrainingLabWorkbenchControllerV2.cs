using System;
using System.Collections.Generic;
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
        public IReadOnlyList<PositionFaultV1> PositionFaults =>
            _editor.EvaluatePositionFaults();

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
    }
}
