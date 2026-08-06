using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Volleyball.Domain.Simulation;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public enum TrainingLabPrecisionVectorModeV1 { Position, Velocity }

    // Three UI-only panes edit one selected draft vector and preserve its hidden axis.
    public sealed class TrainingLabPrecisionAdjustmentPresenterV1
    {
        private const float PositionHorizontalLimit = 9f;
        private const float PositionVerticalLimit = 12f;
        private const float VelocityLimit = 15f;
        private readonly TrainingScenarioLabController _controller;
        private readonly Dictionary<VisualElement, TrainingLabPrecisionPlaneV1>
            _planes = new Dictionary<VisualElement, TrainingLabPrecisionPlaneV1>();
        private int _dragPointer = -1;
        private VisualElement _dragPane;

        public TrainingLabPrecisionAdjustmentPresenterV1(
            TrainingScenarioLabController controller,
            VisualElement xy,
            VisualElement zy,
            VisualElement xz)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            AddPane(xy, TrainingLabPrecisionPlaneV1.XY);
            AddPane(zy, TrainingLabPrecisionPlaneV1.ZY);
            AddPane(xz, TrainingLabPrecisionPlaneV1.XZ);
        }

        public TrainingLabPrecisionVectorModeV1 VectorMode { get; private set; } =
            TrainingLabPrecisionVectorModeV1.Position;

        public void SetVectorMode(TrainingLabPrecisionVectorModeV1 mode)
        {
            if (!Enum.IsDefined(typeof(TrainingLabPrecisionVectorModeV1), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (_controller.SelectedObjectId != "ball" &&
                mode == TrainingLabPrecisionVectorModeV1.Velocity)
                throw new InvalidOperationException(
                    "Only the serve ball has a velocity vector.");
            VectorMode = mode;
        }

        public void Render()
        {
            foreach (var pair in _planes)
            {
                pair.Key.Clear();
                var label = new Label(PaneLabel(pair.Value));
                label.AddToClassList("precision-pane-label");
                pair.Key.Add(label);
            }
        }

        private void AddPane(VisualElement pane,
            TrainingLabPrecisionPlaneV1 plane)
        {
            if (pane == null) throw new ArgumentNullException(nameof(pane));
            _planes.Add(pane, plane);
            pane.RegisterCallback<PointerDownEvent>(value =>
            {
                if (_controller.EditingLocked) return;
                _dragPointer = value.pointerId;
                _dragPane = pane;
                pane.CapturePointer(value.pointerId);
                ApplyPoint(pane, plane, value.position);
                value.StopPropagation();
            });
            pane.RegisterCallback<PointerMoveEvent>(value =>
            {
                if (_dragPointer != value.pointerId || _dragPane != pane ||
                    !pane.HasPointerCapture(value.pointerId))
                    return;
                ApplyPoint(pane, plane, value.position);
                value.StopPropagation();
            });
            pane.RegisterCallback<PointerUpEvent>(value =>
            {
                if (_dragPointer != value.pointerId || _dragPane != pane) return;
                if (pane.HasPointerCapture(value.pointerId))
                    pane.ReleasePointer(value.pointerId);
                _dragPointer = -1;
                _dragPane = null;
                value.StopPropagation();
            });
        }

        private void ApplyPoint(VisualElement pane,
            TrainingLabPrecisionPlaneV1 plane, Vector2 panelPoint)
        {
            var bounds = pane.worldBound;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            var point = panelPoint - bounds.position;
            var horizontal = Mathf.Lerp(-HorizontalLimit(), HorizontalLimit(),
                Mathf.Clamp01(point.x / bounds.width));
            var vertical = Mathf.Lerp(-VerticalLimit(plane), VerticalLimit(plane),
                Mathf.Clamp01((bounds.height - point.y) / bounds.height));
            ApplyDrag(plane, horizontal, vertical);
        }

        public void ApplyDrag(TrainingLabPrecisionPlaneV1 plane,
            float horizontal, float vertical)
        {
            var next = TrainingLabTacticalBoardGeometryV1.ReplaceVisibleAxes(
                plane, SelectedVector(), horizontal, vertical);
            if (_controller.SelectedObjectId == "ball" &&
                VectorMode == TrainingLabPrecisionVectorModeV1.Velocity)
                _controller.SetBallVelocity(next);
            else if (_controller.SelectedObjectId == "ball")
                _controller.SetBallPosition(next);
            else if (!string.IsNullOrWhiteSpace(_controller.SelectedObjectId))
                _controller.SetPlayerPosition(new StablePlayerId(
                    _controller.SelectedObjectId), next);
        }

        private SimVector3 SelectedVector()
        {
            if (_controller.SelectedObjectId == "ball")
                return VectorMode == TrainingLabPrecisionVectorModeV1.Velocity
                    ? _controller.Draft.BallVelocity
                    : _controller.Draft.BallPosition;
            return _controller.Draft.Players.Single(value =>
                value != null && value.PlayerId.Value ==
                _controller.SelectedObjectId).Position;
        }

        private float HorizontalLimit()
        {
            return VectorMode == TrainingLabPrecisionVectorModeV1.Velocity
                ? VelocityLimit
                : PositionHorizontalLimit;
        }

        private float VerticalLimit(TrainingLabPrecisionPlaneV1 plane)
        {
            if (VectorMode == TrainingLabPrecisionVectorModeV1.Velocity)
                return VelocityLimit;
            return plane == TrainingLabPrecisionPlaneV1.XZ
                ? PositionHorizontalLimit
                : PositionVerticalLimit;
        }

        private string PaneLabel(TrainingLabPrecisionPlaneV1 plane)
        {
            var vector = VectorMode == TrainingLabPrecisionVectorModeV1.Velocity
                ? "速度"
                : "位置";
            return vector + " · " + plane +
                   (plane == TrainingLabPrecisionPlaneV1.XY ? " (X / Y)" :
                    plane == TrainingLabPrecisionPlaneV1.ZY ? " (Z / Y)" :
                    " (X / Z)");
        }
    }
}
