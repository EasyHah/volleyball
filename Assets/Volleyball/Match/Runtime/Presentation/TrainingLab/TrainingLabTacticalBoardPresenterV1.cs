using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    // Owns direct 2D player placement; no camera or collider is part of this path.
    public sealed class TrainingLabTacticalBoardPresenterV1
    {
        private const float TokenWidth = 94f;
        private const float TokenHeight = 38f;
        private readonly VisualElement _board;
        private readonly VisualElement _tokens;
        private readonly VisualElement _faults;
        private readonly TrainingScenarioLabController _controller;
        private int _dragPointer = -1;
        private string _dragPlayerId;

        public TrainingLabTacticalBoardPresenterV1(
            VisualElement board,
            VisualElement tokens,
            VisualElement faults,
            TrainingScenarioLabController controller)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _faults = faults ?? throw new ArgumentNullException(nameof(faults));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _board.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _board.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void Render()
        {
            _tokens.Clear();
            _faults.Clear();
            if (_board.contentRect.width <= 0f ||
                _board.contentRect.height <= 0f)
                return;
            var faultIds = new HashSet<string>(
                _controller.PositionFaultPreview.SelectMany(value => new[]
                {
                    value.RequiredAheadOrLeft.PlayerId.Value,
                    value.ViolatingBehindOrRight.PlayerId.Value
                }), StringComparer.Ordinal);

            foreach (var pose in _controller.Draft.Players.Where(value => value != null))
                _tokens.Add(CreatePlayerToken(pose, faultIds.Contains(pose.PlayerId.Value)));
            foreach (var diagnostic in TrainingLabPositionFaultDiagnosticV1.DescribeAll(
                         _controller.PositionFaultPreview))
                RenderFaultRelation(diagnostic);
        }

        private Button CreatePlayerToken(TrainingPlayerPoseDraftV1 pose,
            bool hasFault)
        {
            var token = new Button
            {
                name = "tactical-token-" + pose.PlayerId.Value,
                text = LabelFor(pose.PlayerId),
                tooltip = "拖动摆位 · " + LabelFor(pose.PlayerId)
            };
            token.AddToClassList("tactical-token");
            token.EnableInClassList("selected",
                pose.PlayerId.Value == _controller.SelectedObjectId);
            token.EnableInClassList("fault", hasFault);
            var point = ToBoard(pose.Position);
            token.style.left = point.x - TokenWidth * .5f;
            token.style.top = point.y - TokenHeight * .5f;
            token.clicked += () => _controller.SelectObject(
                pose.PlayerId.Value, "players.position");
            token.RegisterCallback<PointerDownEvent>(value =>
            {
                if (_controller.EditingLocked ||
                    _controller.CurrentStep == TrainingLabStepV1.Rotation)
                    return;
                _dragPointer = value.pointerId;
                _dragPlayerId = pose.PlayerId.Value;
                _board.CapturePointer(value.pointerId);
                value.StopPropagation();
            });
            return token;
        }

        private void OnPointerMove(PointerMoveEvent value)
        {
            if (_dragPointer != value.pointerId ||
                string.IsNullOrWhiteSpace(_dragPlayerId) ||
                !_board.HasPointerCapture(value.pointerId))
                return;
            var position = TrainingLabTacticalBoardGeometryV1.BoardToCourt(
                BoardRect(), BoardPoint(value.position), 0f);
            _controller.SetPlayerPosition(new StablePlayerId(_dragPlayerId),
                position);
            value.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent value)
        {
            if (_dragPointer != value.pointerId) return;
            if (_board.HasPointerCapture(value.pointerId))
                _board.ReleasePointer(value.pointerId);
            _dragPointer = -1;
            _dragPlayerId = null;
            value.StopPropagation();
        }

        private void RenderFaultRelation(
            TrainingLabPositionFaultDiagnosticV1 diagnostic)
        {
            var required = ToBoard(
                diagnostic.Fault.RequiredAheadOrLeft.FootProjection);
            var violating = ToBoard(
                diagnostic.Fault.ViolatingBehindOrRight.FootProjection);
            var midpoint = (required + violating) * .5f;
            var relation = new Label("────▶ " + diagnostic.Text)
            {
                name = "position-fault-relation-" + diagnostic.Fault.Rule
            };
            relation.AddToClassList("position-fault-relation");
            relation.style.left = midpoint.x;
            relation.style.top = midpoint.y;
            _faults.Add(relation);

            var arrow = new Label(ArrowFor(diagnostic))
            {
                name = "position-fault-arrow-" + diagnostic.Fault.Rule
            };
            arrow.AddToClassList("position-fault-arrow");
            arrow.style.left = violating.x + 28f;
            arrow.style.top = violating.y - 22f;
            _faults.Add(arrow);
        }

        private string LabelFor(StablePlayerId playerId)
        {
            var player = _controller.Draft.Context.Home.Players
                .Concat(_controller.Draft.Context.Away.Players)
                .Single(value => value.PlayerId.Equals(playerId));
            var rotation = _controller.Draft.HomeRotation.Contains(playerId)
                ? _controller.Draft.HomeRotation
                : _controller.Draft.AwayRotation;
            var slot = _controller.Draft.RotationLocked
                ? rotation.IndexOf(playerId) + 1
                : 0;
            return PositionName(player.Position) + " · " +
                   (slot > 0 ? slot + "号位" : "待锁定");
        }

        private Vector2 ToBoard(SimVector3 position)
        {
            return TrainingLabTacticalBoardGeometryV1.CourtToBoard(
                BoardRect(), position);
        }

        private Rect BoardRect()
        {
            return new Rect(0f, 0f, _board.contentRect.width,
                _board.contentRect.height);
        }

        private Vector2 BoardPoint(Vector2 panelPosition)
        {
            return panelPosition - _board.worldBound.position;
        }

        private static string ArrowFor(
            TrainingLabPositionFaultDiagnosticV1 diagnostic)
        {
            if (diagnostic.Axis == TrainingLabCorrectionAxisV1.Lateral)
                return diagnostic.CourtDirection > 0 ? "→" : "←";
            return diagnostic.CourtDirection > 0 ? "↑" : "↓";
        }

        private static string PositionName(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Setter => "二传",
                PlayerPosition.OutsideHitter => "主攻",
                PlayerPosition.MiddleBlocker => "副攻",
                PlayerPosition.Opposite => "接应",
                PlayerPosition.Libero => "自由人",
                _ => "防守"
            };
        }
    }
}
