using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Volleyball.AI;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public enum TrainingLabAuthoringModeV1 { Board, Precision, Observation }

    public sealed class TrainingLabPreviewMarkerV1 : MonoBehaviour
    {
        public string ObjectId { get; private set; }

        public void Initialize(string objectId)
        {
            ObjectId = string.IsNullOrWhiteSpace(objectId)
                ? throw new ArgumentException("Preview object ID is required.")
                : objectId;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class TrainingScenarioLabView : MonoBehaviour
    {
        public TrainingRunEvidenceV1 VisibleEvidence =>
            _controller?.VisibleEvidence;
        private readonly Dictionary<string, TrainingLabPreviewMarkerV1>
            _markers =
                new Dictionary<string, TrainingLabPreviewMarkerV1>(
                    StringComparer.Ordinal);
        private UIDocument _document;
        private TrainingScenarioLabController _controller;
        private TrainingSimulationControllerV1 _simulation;
        private Transform _worldHost;
        private GameObject _previewRoot;
        private LineRenderer _trajectory;
        private Camera _worldCamera;
        private VisualElement _root;
        private VisualElement _scenarioList;
        private VisualElement _issues;
        private VisualElement _timeline;
        private VisualElement _viewport;
        private VisualElement _editorControls;
        private VisualElement _rotationList;
        private VisualElement _attributeTable;
        private VisualElement _bookmarkList;
        private Label _state;
        private Label _hash;
        private Label _rules;
        private Label _selection;
        private Label _comparison;
        private Label _feedback;
        private Label _monitor;
        private Label _boardInstruction;
        private TextField _displayName;
        private IntegerField _seed;
        private DropdownField _recipe;
        private DropdownField _sourceTeam;
        private DropdownField _lastActor;
        private DropdownField _homeSet;
        private DropdownField _homeSpike;
        private DropdownField _awaySet;
        private DropdownField _awaySpike;
        private FloatField _positionX;
        private FloatField _positionY;
        private FloatField _positionZ;
        private FloatField _velocityX;
        private FloatField _velocityY;
        private FloatField _velocityZ;
        private Button _validate;
        private Button _run;
        private Button _pause;
        private Button _step;
        private Button _rerun;
        private Button _returnToEdit;
        private Button _export;
        private Button _reviewSetter;
        private TextField _bookmarkName;
        private bool _rendering;
        private bool _ownsController;
        private bool _initialized;
        private Font _runtimeFont;
        private VisualElement _tacticalBoardElement;
        private VisualElement _precisionElement;
        private VisualElement _observationElement;
        private TrainingLabTacticalBoardPresenterV1 _tacticalBoard;
        private TrainingLabPrecisionAdjustmentPresenterV1 _precision;
        private TrainingLabFreeObservationPresenterV1 _observation;
        private TrainingLabAuthoringModeV1 _authoringMode;

        public TrainingScenarioLabController Controller => _controller;
        public TrainingLabPrecisionVectorModeV1 PrecisionVectorMode =>
            _precision?.VectorMode ?? TrainingLabPrecisionVectorModeV1.Position;
        public bool IsPrecisionAdjustmentOpen =>
            _authoringMode == TrainingLabAuthoringModeV1.Precision;
        public bool ObservationCameraChanged =>
            _observation != null && _observation.CameraChanged;
        public bool IsFreeObservationOpen =>
            _authoringMode == TrainingLabAuthoringModeV1.Observation;
        public bool ObservationOutputReady =>
            _observation != null && _observation.Output != null;

        public void Bind(TrainingScenarioLabController controller)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));
            if (_controller != null)
                _controller.Changed -= Render;
            if (_ownsController)
                _controller?.Dispose();
            _controller = controller;
            _ownsController = false;
            _tacticalBoard = null;
            _precision = null;
            _observation?.Dispose();
            _observation = null;
            if (isActiveAndEnabled)
            {
                _controller.Changed += Render;
                EnsureDocument();
                Render();
            }
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            EnsureDocument();
            if (_controller == null)
            {
                var host = new GameObject("TrainingWorldHostV1");
                host.transform.SetParent(transform, false);
                _worldHost = host.transform;
                _simulation =
                    new TrainingSimulationControllerV1(_worldHost);
                _controller = new TrainingScenarioLabController(
                    TrainingScenarioDraftStoreV1.LoadProjectCatalog(),
                    _simulation);
                _ownsController = true;
            }

            _controller.Changed -= Render;
            _controller.Changed += Render;
            Render();
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.Changed -= Render;
            _observation?.Dispose();
            _observation = null;
        }

        private void EnsureDocument()
        {
            if (_initialized) return;
            _document = _document != null
                ? _document
                : GetComponent<UIDocument>();
            _root = _document.rootVisualElement;
            _scenarioList = _root.Q<VisualElement>("scenario-list");
            _issues = _root.Q<VisualElement>("issue-list");
            _timeline = _root.Q<VisualElement>("timeline-list");
            _viewport = _root.Q<VisualElement>("world-viewport");
            _editorControls = _root.Q<VisualElement>("editor-controls");
            _rotationList = _root.Q<VisualElement>("rotation-list");
            _attributeTable = _root.Q<VisualElement>("attribute-table");
            _bookmarkList = _root.Q<VisualElement>("bookmark-list");
            _state = _root.Q<Label>("state-label");
            _hash = _root.Q<Label>("hash-label");
            _rules = _root.Q<Label>("rules-label");
            _selection = _root.Q<Label>("selection-label");
            _comparison = _root.Q<Label>("comparison-label");
            _feedback = _root.Q<Label>("feedback-label");
            _monitor = _root.Q<Label>("monitor-label");
            _boardInstruction = _root.Q<Label>("board-instruction");
            _tacticalBoardElement = _root.Q<VisualElement>("tactical-board");
            _tacticalBoardElement.RegisterCallback<GeometryChangedEvent>(
                _ => RenderTacticalBoardAfterLayout());
            _precisionElement = _root.Q<VisualElement>("precision-adjustment");
            _observationElement = _root.Q<VisualElement>("free-observation");
            _displayName = _root.Q<TextField>("display-name");
            _seed = _root.Q<IntegerField>("match-seed");
            _recipe = _root.Q<DropdownField>("start-recipe");
            _sourceTeam = _root.Q<DropdownField>("source-team");
            _lastActor = _root.Q<DropdownField>("last-actor");
            _homeSet = _root.Q<DropdownField>("home-set-route");
            _homeSpike = _root.Q<DropdownField>("home-spike-route");
            _awaySet = _root.Q<DropdownField>("away-set-route");
            _awaySpike = _root.Q<DropdownField>("away-spike-route");
            _positionX = _root.Q<FloatField>("position-x");
            _positionY = _root.Q<FloatField>("position-y");
            _positionZ = _root.Q<FloatField>("position-z");
            _velocityX = _root.Q<FloatField>("velocity-x");
            _velocityY = _root.Q<FloatField>("velocity-y");
            _velocityZ = _root.Q<FloatField>("velocity-z");
            _validate = _root.Q<Button>("validate-button");
            _run = _root.Q<Button>("run-button");
            _pause = _root.Q<Button>("pause-button");
            _step = _root.Q<Button>("step-button");
            _rerun = _root.Q<Button>("rerun-button");
            _returnToEdit = _root.Q<Button>("edit-button");
            _export = _root.Q<Button>("export-button");
            _reviewSetter = _root.Q<Button>("review-setter-button");
            _bookmarkName = _root.Q<TextField>("bookmark-name");

            ConfigureChoices();
            RegisterUiEvents();
            TryApplyChineseSystemFont(_root);
            _root.schedule.Execute(RenderTimeline).Every(200);
            _initialized = true;
        }

        private void ConfigureChoices()
        {
            _recipe.choices = Enum.GetNames(typeof(RallyStartRecipeV3)).ToList();
            _sourceTeam.choices = Enum.GetNames(typeof(TeamSide)).ToList();
            var setRoutes = Enum.GetNames(typeof(SetRoute)).ToList();
            var spikeRoutes = Enum.GetNames(typeof(SpikeRoute)).ToList();
            _homeSet.choices = setRoutes;
            _awaySet.choices = setRoutes;
            _homeSpike.choices = spikeRoutes;
            _awaySpike.choices = spikeRoutes;
        }

        private void RegisterUiEvents()
        {
            _root.Q<Button>("new-button").clicked += () =>
                _controller.NewDraft();
            _root.Q<Button>("duplicate-button").clicked += () =>
                _controller.DuplicateDraft();
            _root.Q<Button>("reset-draft-button").clicked += () =>
                _controller.ResetDraft();
            _validate.clicked += () => _controller.Validate();
            _run.clicked += () => _controller.Run();
            _pause.clicked += TogglePause;
            _step.clicked += () => _controller.Step();
            _rerun.clicked += () => _controller.RerunSameSeed();
            _returnToEdit.clicked += () => _controller.ReturnToEditing();
            _export.clicked += ExportEvidence;
            _reviewSetter.clicked += OpenSetterReview;
            _root.Q<Button>("confirm-rotation-button").clicked += () => _controller.ConfirmRotation();
            _root.Q<Button>("reopen-rotation-button").clicked += () => _controller.ReopenRotation();
            _root.Q<Button>("step-rotation").clicked += () => _controller.ReopenRotation();
            _root.Q<Button>("step-positioning").clicked += () =>
            {
                if (_controller.Draft.RotationLocked) _controller.GoToPositioning();
            };
            _root.Q<Button>("step-serve").clicked += () => _controller.SelectServeTool(TrainingServeToolV1.MoveBall);
            _root.Q<Button>("step-validation").clicked += () => _controller.GoToValidation();
            _root.Q<Button>("tool-move-ball").clicked += () => _controller.SelectServeTool(TrainingServeToolV1.MoveBall);
            _root.Q<Button>("tool-velocity").clicked += () => _controller.SelectServeTool(TrainingServeToolV1.AdjustVelocity);
            _root.Q<Button>("tool-trajectory").clicked += () => _controller.SelectServeTool(TrainingServeToolV1.ViewTrajectory);
            _root.Q<Button>("open-precision-button").clicked +=
                OpenPrecisionAdjustment;
            _root.Q<Button>("open-observation-button").clicked +=
                OpenFreeObservation;
            _root.Q<Button>("return-to-board-button").clicked +=
                ReturnToTacticalBoard;
            _root.Q<Button>("return-from-observation-button").clicked +=
                ReturnToTacticalBoard;
            _root.Q<Button>("precision-position-button").clicked += () =>
                SetPrecisionVectorMode(TrainingLabPrecisionVectorModeV1.Position);
            _root.Q<Button>("precision-velocity-button").clicked += () =>
                SetPrecisionVectorMode(TrainingLabPrecisionVectorModeV1.Velocity);
            _root.Q<Button>("save-bookmark-button").clicked += SaveCurrentCameraBookmark;

            _displayName.RegisterValueChangedCallback(value =>
            {
                if (!_rendering) _controller.SetDisplayName(value.newValue);
            });
            _seed.RegisterValueChangedCallback(value =>
            {
                if (!_rendering) _controller.SetMatchSeed(value.newValue);
            });
            _recipe.RegisterValueChangedCallback(_ => ApplyRallyStart());
            _sourceTeam.RegisterValueChangedCallback(_ => ApplyRallyStart());
            _lastActor.RegisterValueChangedCallback(_ => ApplyRallyStart());
            _homeSet.RegisterValueChangedCallback(_ => ApplyTactics(
                TeamSide.Home,
                _homeSet,
                _homeSpike));
            _homeSpike.RegisterValueChangedCallback(_ => ApplyTactics(
                TeamSide.Home,
                _homeSet,
                _homeSpike));
            _awaySet.RegisterValueChangedCallback(_ => ApplyTactics(
                TeamSide.Away,
                _awaySet,
                _awaySpike));
            _awaySpike.RegisterValueChangedCallback(_ => ApplyTactics(
                TeamSide.Away,
                _awaySet,
                _awaySpike));
            _positionX.RegisterValueChangedCallback(_ => ApplyPosition());
            _positionY.RegisterValueChangedCallback(_ => ApplyPosition());
            _positionZ.RegisterValueChangedCallback(_ => ApplyPosition());
            _velocityX.RegisterValueChangedCallback(_ => ApplyVelocity());
            _velocityY.RegisterValueChangedCallback(_ => ApplyVelocity());
            _velocityZ.RegisterValueChangedCallback(_ => ApplyVelocity());

            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void Render()
        {
            if (_controller == null || !_initialized) return;
            _rendering = true;
            try
            {
                RenderScenarioList();
                var draft = _controller.Draft;
                _state.text = StateText(_controller.State);
                _state.EnableInClassList(
                    "state-error",
                    _controller.State == TrainingScenarioLabStateV1.Faulted);
                _hash.text = _controller.ReadyScenario == null
                    ? "HASH · 待校验"
                    : "HASH · " +
                      _controller.ReadyScenario.ContentHash.Substring(0, 16);
                _rules.text = _controller.DerivedRuleSummary();
                _comparison.text = _controller.RunComparisonSummary;
                _feedback.text = _controller.FailureMessage;
                _displayName.SetValueWithoutNotify(draft.DisplayName);
                _seed.SetValueWithoutNotify(draft.Context?.Seed ?? 0);
                _recipe.SetValueWithoutNotify(draft.StartRecipe.ToString());
                _sourceTeam.SetValueWithoutNotify(draft.SourceTeam.ToString());
                _lastActor.choices = new[] { "无" }
                    .Concat(draft.Players
                        .Where(value => value != null)
                        .Select(value => value.PlayerId.Value)
                        .OrderBy(value => value, StringComparer.Ordinal))
                    .ToList();
                _lastActor.SetValueWithoutNotify(
                    draft.LastLegalActor?.Value ?? "无");
                SetTacticValues();
                RenderSelectedObject();
                RenderRotationAndAttributes();
                RenderIssues();
                RenderTimeline();
                RenderControls();
                EnsureAuthoringPresenters();
                RenderAuthoringSurfaces();
                SyncWorld();
            }
            finally
            {
                _rendering = false;
            }
        }

        private void RenderScenarioList()
        {
            _scenarioList.Clear();
            foreach (var entry in _controller.Entries)
            {
                var captured = entry.Key;
                var button = new Button(() =>
                    _controller.SelectDraftEntry(captured))
                {
                    text = (entry.IsBuiltIn ? "标准 · " : "草稿 · ") +
                           entry.DisplayName
                };
                button.AddToClassList("scenario-button");
                button.EnableInClassList(
                    "selected",
                    captured == _controller.SelectedEntryKey);
                button.SetEnabled(!_controller.EditingLocked);
                _scenarioList.Add(button);
            }
        }

        private void RenderSelectedObject()
        {
            var draft = _controller.Draft;
            var id = _controller.SelectedObjectId;
            var isBall = id == "ball";
            var player = isBall
                ? null
                : draft.Players.FirstOrDefault(value =>
                    value != null && value.PlayerId.Value == id);
            var position = isBall
                ? draft.BallPosition
                : player?.Position ?? SimVector3.Zero;
            _selection.text = isBall
                ? "球 · 位置与线速度"
                : player == null
                    ? "未选择对象"
                    : "球员 · " + player.PlayerId.Value;
            _positionX.SetValueWithoutNotify(position.X);
            _positionY.SetValueWithoutNotify(position.Y);
            _positionZ.SetValueWithoutNotify(position.Z);
            _velocityX.SetValueWithoutNotify(draft.BallVelocity.X);
            _velocityY.SetValueWithoutNotify(draft.BallVelocity.Y);
            _velocityZ.SetValueWithoutNotify(draft.BallVelocity.Z);
            var canEditVelocity = isBall && !_controller.EditingLocked;
            _velocityX.SetEnabled(canEditVelocity);
            _velocityY.SetEnabled(canEditVelocity);
            _velocityZ.SetEnabled(canEditVelocity);
        }

        private void RenderRotationAndAttributes()
        {
            _rotationList.Clear();
            foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
            {
                var rotation = side == TeamSide.Home
                    ? _controller.Draft.HomeRotation
                    : _controller.Draft.AwayRotation;
                var teamPlayers = (side == TeamSide.Home
                        ? _controller.Draft.Context.Home.Players
                        : _controller.Draft.Context.Away.Players)
                    .OrderBy(value => value.JerseyNumber)
                    .ThenBy(value => value.PlayerId.Value, StringComparer.Ordinal)
                    .ToArray();
                var choices = teamPlayers.Select(RotationChoice).ToList();
                for (var index = 0; index < rotation.Count; index++)
                {
                    var slot = index;
                    var selected = teamPlayers.Single(value =>
                        value.PlayerId.Equals(rotation[slot]));
                    var row = new VisualElement { name = "rotation-" + side + "-" + (slot + 1) };
                    row.AddToClassList("rotation-row");
                    row.Add(new Label(side + " " + (slot + 1)));
                    var picker = new DropdownField(choices, RotationChoice(selected));
                    picker.SetEnabled(!_controller.EditingLocked &&
                        !_controller.Draft.RotationLocked);
                    picker.RegisterValueChangedCallback(change =>
                    {
                        if (_rendering) return;
                        var selectedIndex = choices.IndexOf(change.newValue);
                        if (selectedIndex < 0) return;
                        var updated = rotation.ToArray();
                        updated[slot] = teamPlayers[selectedIndex].PlayerId;
                        _controller.SetRotation(side, updated);
                    });
                    row.Add(picker);
                    _rotationList.Add(row);
                }
            }

            _attributeTable.Clear();
            foreach (var player in _controller.Draft.Context.Home.Players.Concat(_controller.Draft.Context.Away.Players))
            {
                var row = new VisualElement();
                row.AddToClassList("attribute-row");
                row.Add(new Label(player.DisplayName + " · " + PositionName(player.Position)));
                var height = new IntegerField { value = _controller.Draft.AttributeOverrides.TryGetValue(player.PlayerId, out var value) ? value.HeightMillimeters : Mathf.RoundToInt(player.Physical.HeightMeters * 1000f) };
                var hand = new DropdownField(new List<string> { "Left", "Right" }, (_controller.Draft.AttributeOverrides.TryGetValue(player.PlayerId, out value) ? value.DominantHand : player.DominantHand).ToString());
                height.SetEnabled(!_controller.EditingLocked && _controller.Draft.AccessLevel == TrainingScenarioAccessLevelV1.Developer);
                hand.SetEnabled(height.enabledSelf);
                height.RegisterValueChangedCallback(change => SetOverride(player, change.newValue, hand.value));
                hand.RegisterValueChangedCallback(change => SetOverride(player, height.value, change.newValue));
                row.Add(height);
                row.Add(hand);
                _attributeTable.Add(row);
            }

            _bookmarkList.Clear();
            foreach (var bookmark in _controller.Draft.CameraBookmarks)
            {
                var target = bookmark;
                var button = new Button(() => ApplyCameraBookmark(target))
                {
                    text = bookmark.Name + " · " + (bookmark.Orthographic ? "正交" : "自由") + "视角"
                };
                button.AddToClassList("bookmark-row");
                _bookmarkList.Add(button);
            }
        }

        private static string RotationChoice(PlayerSnapshotV4 player)
        {
            return player.JerseyNumber + " · " + player.DisplayName +
                " · " + PositionName(player.Position);
        }

        private void SaveCurrentCameraBookmark()
        {
            if (_worldCamera == null)
            {
                _feedback.text = "预览场地尚未就绪，无法保存机位。";
                return;
            }

            var position = _worldCamera.transform.position;
            var forward = _worldCamera.transform.forward;
            _controller.SaveCameraBookmark(
                _bookmarkName.value,
                new SimVector3(position.x, position.y, position.z),
                new SimVector3(forward.x, forward.y, forward.z),
                _worldCamera.orthographicSize,
                _worldCamera.orthographic);
        }

        private void ApplyCameraBookmark(TrainingCameraBookmarkV1 bookmark)
        {
            if (_worldCamera == null) return;
            _worldCamera.transform.position = ToUnity(bookmark.Position);
            _worldCamera.transform.forward = ToUnity(bookmark.Forward);
            _worldCamera.orthographic = bookmark.Orthographic;
            _worldCamera.orthographicSize = bookmark.OrthographicSize;
        }

        private void SetOverride(PlayerSnapshotV4 player, int height, string hand)
        {
            if (_rendering) return;
            if (height < 1400 || height > 2300)
            {
                _feedback.text = "训练身高必须在 1400 到 2300 毫米之间。";
                return;
            }

            _controller.SetTrainingAttributeOverride(player.PlayerId,
                new TrainingPlayerAttributeOverrideV1(height,
                    Enum.Parse<DominantHandV4>(hand), player.Physical, player.Technical));
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

        private void RenderIssues()
        {
            _issues.Clear();
            if (_controller.Validation.Issues.Count == 0)
            {
                _issues.Add(new Label("校验通过：12 人、球、规则起点与权威配方合法。")
                {
                    name = "validation-ok"
                });
                return;
            }

            for (var index = 0;
                 index < _controller.Validation.Issues.Count;
                 index++)
            {
                var issue = _controller.Validation.Issues[index];
                var captured = index;
                var button = new Button(() => _controller.FocusIssue(captured))
                {
                    text = issue.Code + "\n" +
                           issue.PropertyPath + " · " + issue.Message
                };
                button.AddToClassList("issue-button");
                _issues.Add(button);
            }
        }

        private void RenderTimeline()
        {
            if (_timeline == null || _controller == null) return;
            var evidence = _controller.VisibleEvidence;
            _timeline.Clear();
            if (evidence == null || evidence.Timeline.Count == 0)
            {
                _timeline.Add(new Label("运行后将在这里显示权威事件。"));
                return;
            }

            foreach (var item in evidence.Timeline.TakeLast(80))
            {
                var row = new Label(
                    item.Sequence.ToString("000") + "  " +
                    item.SimulationTimeSeconds.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture) + "s  " +
                    item.Kind + "  " + item.Summary);
                row.AddToClassList("timeline-row");
                if (item.Decision != null)
                    row.tooltip = "snapshot " +
                                  item.Decision.SnapshotHash;
                _timeline.Add(row);
            }
        }

        private void RenderControls()
        {
            var state = _controller.State;
            var editable = !_controller.EditingLocked;
            _editorControls.SetEnabled(editable);
            _validate.SetEnabled(editable);
            _run.SetEnabled(
                editable && _controller.Validation.IsValid);
            _pause.SetEnabled(
                state == TrainingScenarioLabStateV1.Running ||
                state == TrainingScenarioLabStateV1.Paused);
            _pause.text = state == TrainingScenarioLabStateV1.Paused
                ? "继续"
                : "暂停";
            _step.SetEnabled(
                state == TrainingScenarioLabStateV1.Paused);
            _rerun.SetEnabled(
                state == TrainingScenarioLabStateV1.Completed);
            _returnToEdit.SetEnabled(
                state == TrainingScenarioLabStateV1.Completed ||
                state == TrainingScenarioLabStateV1.Faulted);
            _export.SetEnabled(
                _controller.VisibleEvidence?.Decisions.Count > 0);
            _reviewSetter.SetEnabled(
                _controller.VisibleEvidence?.SetterTargets.Count > 0);
            _monitor.text = "阶段 " + _controller.CurrentStep + " · 发球方 " +
                _controller.Draft.FirstServingSide + " · 触球 " +
                (_controller.VisibleEvidence?.Timeline.Count(value => value.Kind == TrainingTimelineEventKindV1.ContactAccepted) ?? 0) +
                " · 裁判 " + (_controller.PositionFaultPreview.Count == 0 ? "站位合法" : "位置错误 " + _controller.PositionFaultPreview.Count);
            _boardInstruction.text = _controller.CurrentStep ==
                TrainingLabStepV1.Rotation
                ? "步骤 1：确认轮转位次后才能在战术板摆位"
                : _controller.PositionFaultPreview.Count > 0
                    ? "先按红色连线和箭头修正位置错误，才能进入发球设置"
                    : "摆位：直接拖动球员；需要精确数值时打开精确调整";
            foreach (var step in Enum.GetValues(typeof(TrainingLabStepV1)).Cast<TrainingLabStepV1>())
                _root.Q<Button>("step-" + step.ToString().ToLowerInvariant())?.EnableInClassList("active-step", _controller.CurrentStep == step);
        }

        private void EnsureAuthoringPresenters()
        {
            if (_tacticalBoard != null || _controller == null) return;
            _tacticalBoard = new TrainingLabTacticalBoardPresenterV1(
                _tacticalBoardElement,
                _root.Q<VisualElement>("tactical-token-layer"),
                _root.Q<VisualElement>("position-fault-layer"), _controller);
            _precision = new TrainingLabPrecisionAdjustmentPresenterV1(
                _controller,
                _root.Q<VisualElement>("precision-xy-pane"),
                _root.Q<VisualElement>("precision-zy-pane"),
                _root.Q<VisualElement>("precision-xz-pane"));
        }

        private void RenderAuthoringSurfaces()
        {
            if (_tacticalBoard == null) return;
            _tacticalBoardElement.EnableInClassList("is-hidden",
                _authoringMode != TrainingLabAuthoringModeV1.Board);
            _precisionElement.EnableInClassList("is-hidden",
                _authoringMode != TrainingLabAuthoringModeV1.Precision);
            _observationElement.EnableInClassList("is-hidden",
                _authoringMode != TrainingLabAuthoringModeV1.Observation);
            if (_authoringMode == TrainingLabAuthoringModeV1.Board)
                _tacticalBoard.Render();
            else if (_authoringMode == TrainingLabAuthoringModeV1.Precision)
                _precision.Render();
            var velocity = _root.Q<Button>("precision-velocity-button");
            velocity.SetEnabled(_controller.SelectedObjectId == "ball" &&
                !_controller.EditingLocked);
            _root.Q<Button>("precision-position-button").EnableInClassList(
                "active-view", PrecisionVectorMode ==
                TrainingLabPrecisionVectorModeV1.Position);
            velocity.EnableInClassList("active-view", PrecisionVectorMode ==
                TrainingLabPrecisionVectorModeV1.Velocity);
        }

        private void RenderTacticalBoardAfterLayout()
        {
            if (_authoringMode != TrainingLabAuthoringModeV1.Board ||
                _tacticalBoard == null || _tacticalBoardElement.contentRect.width <= 0f ||
                _tacticalBoardElement.contentRect.height <= 0f)
                return;
            _tacticalBoard.Render();
        }

        public void OpenPrecisionAdjustment()
        {
            if (_controller == null || _controller.EditingLocked) return;
            _authoringMode = TrainingLabAuthoringModeV1.Precision;
            RenderAuthoringSurfaces();
        }

        public void ReturnToTacticalBoard()
        {
            _observation?.Dispose();
            _observation = null;
            _authoringMode = TrainingLabAuthoringModeV1.Board;
            RenderAuthoringSurfaces();
        }

        public void OpenFreeObservation()
        {
            if (_controller == null || _controller.EditingLocked ||
                _worldCamera == null)
                return;
            if (_observation == null)
                _observation = new TrainingLabFreeObservationPresenterV1(
                    _root.Q<VisualElement>("observation-surface"),
                    _worldCamera);
            _observation.Activate();
            _authoringMode = TrainingLabAuthoringModeV1.Observation;
            RenderAuthoringSurfaces();
        }

        public void SetPrecisionVectorMode(
            TrainingLabPrecisionVectorModeV1 mode)
        {
            if (_precision == null) return;
            _precision.SetVectorMode(mode);
            RenderAuthoringSurfaces();
        }

        public void ApplyPrecisionAdjustment(
            TrainingLabPrecisionPlaneV1 plane,
            float horizontal,
            float vertical)
        {
            if (_precision == null)
                throw new InvalidOperationException(
                    "Precision adjustment is not initialized.");
            _precision.ApplyDrag(plane, horizontal, vertical);
        }

        private void SyncWorld()
        {
            var preview = _controller.State ==
                          TrainingScenarioLabStateV1.Editing ||
                          _controller.State ==
                          TrainingScenarioLabStateV1.Validating ||
                          _controller.State ==
                          TrainingScenarioLabStateV1.Ready;
            if (!preview)
            {
                DestroyPreview();
                return;
            }

            if (_worldHost == null)
            {
                var existing = transform.Find("TrainingWorldHostV1");
                _worldHost = existing != null
                    ? existing
                    : new GameObject("TrainingWorldHostV1").transform;
                if (_worldHost.parent == null)
                    _worldHost.SetParent(transform, false);
            }

            if (_previewRoot == null ||
                _markers.Count != _controller.Draft.Players.Count + 1)
            {
                BuildPreview();
            }

            UpdatePreview();
        }

        private void BuildPreview()
        {
            DestroyPreview();
            _previewRoot = new GameObject("TrainingPreviewRootV1");
            _previewRoot.transform.SetParent(_worldHost, false);
            CourtBuilder.Build(
                _previewRoot.transform,
                CourtBuilder.FormalHalfLength);
            _worldCamera = _previewRoot.GetComponentInChildren<Camera>();
            CreateMarker("ball", PrimitiveType.Sphere, Color.yellow);
            var homeIds = new HashSet<string>(
                _controller.Draft.Context.Home.Players.Select(value =>
                    value.PlayerId.Value),
                StringComparer.Ordinal);
            foreach (var pose in _controller.Draft.Players)
            {
                CreateMarker(
                    pose.PlayerId.Value,
                    PrimitiveType.Capsule,
                    homeIds.Contains(pose.PlayerId.Value)
                        ? new Color(.12f, .45f, .95f)
                        : new Color(.95f, .32f, .18f));
            }

            var trajectoryObject = new GameObject("TrajectoryPreviewV1");
            trajectoryObject.transform.SetParent(_previewRoot.transform, false);
            _trajectory = trajectoryObject.AddComponent<LineRenderer>();
            _trajectory.widthMultiplier = .035f;
            _trajectory.useWorldSpace = false;
            _trajectory.material = new Material(
                Shader.Find("Sprites/Default"));
            _trajectory.startColor = new Color(1f, .85f, .2f, .9f);
            _trajectory.endColor = new Color(1f, .35f, .15f, .2f);
        }

        private void CreateMarker(
            string id,
            PrimitiveType primitive,
            Color color)
        {
            var markerObject = GameObject.CreatePrimitive(primitive);
            markerObject.name = id == "ball"
                ? "PreviewBall"
                : "PreviewPlayer-" + id;
            markerObject.transform.SetParent(_previewRoot.transform, false);
            markerObject.transform.localScale = id == "ball"
                ? Vector3.one * (SimulatedBall.DefaultRadius * 2f)
                : new Vector3(.42f, .72f, .42f);
            var marker = markerObject.AddComponent<TrainingLabPreviewMarkerV1>();
            marker.Initialize(id);
            markerObject.GetComponent<Renderer>().material.color = color;
            if (id != "ball")
                CreatePlayerLabel(markerObject.transform);
            _markers.Add(id, marker);
        }

        private void CreatePlayerLabel(Transform marker)
        {
            var labelObject = new GameObject("RoleAndSlotLabel");
            labelObject.transform.SetParent(marker, false);
            labelObject.transform.localPosition = Vector3.up * 1.2f;
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .075f;
            label.fontSize = 56;
            label.color = Color.white;
            if (_runtimeFont != null)
                label.font = _runtimeFont;
        }

        private void UpdatePreview()
        {
            var draft = _controller.Draft;
            if (draft.BallPosition.IsFinite &&
                _markers.TryGetValue("ball", out var ball))
                ball.transform.localPosition = ToUnity(draft.BallPosition);
            foreach (var pose in draft.Players)
            {
                if (pose == null ||
                    !pose.Position.IsFinite ||
                    !pose.Forward.IsFinite)
                    continue;
                if (!_markers.TryGetValue(
                        pose.PlayerId.Value,
                        out var marker))
                    continue;
                marker.transform.localPosition =
                    ToUnity(pose.Position) + Vector3.up * .72f;
                marker.transform.forward = ToUnity(pose.Forward);
                UpdatePlayerLabel(marker, pose);
            }

            foreach (var pair in _markers)
            {
                var selected = pair.Key == _controller.SelectedObjectId;
                var hasIssue = _controller.Validation.Issues.Any(value =>
                    value.ObjectId == pair.Key ||
                    (pair.Key == "ball" &&
                     value.PropertyPath.StartsWith(
                         "ball",
                         StringComparison.Ordinal)));
                var color = hasIssue
                    ? new Color(1f, .16f, .12f)
                    : selected
                        ? new Color(1f, .8f, .15f)
                        : pair.Key == "ball"
                            ? Color.yellow
                            : DefaultPlayerColor(pair.Key);
                pair.Value.GetComponent<Renderer>().material.color = color;
            }

            UpdateTrajectory();
        }

        private void UpdatePlayerLabel(
            TrainingLabPreviewMarkerV1 marker,
            TrainingPlayerPoseDraftV1 pose)
        {
            var label = marker.GetComponentInChildren<TextMesh>();
            if (label == null) return;
            var player = _controller.Draft.Context.Home.Players
                .Concat(_controller.Draft.Context.Away.Players)
                .Single(value => value.PlayerId.Equals(pose.PlayerId));
            var slot = RotationSlot(player.PlayerId);
            label.text = PositionName(player.Position) + " · " +
                (slot > 0 ? slot + "号位" : "待锁定");
            if (_worldCamera != null)
                label.transform.rotation = Quaternion.LookRotation(
                    -_worldCamera.transform.forward,
                    _worldCamera.transform.up);
        }

        private int RotationSlot(StablePlayerId playerId)
        {
            if (!_controller.Draft.RotationLocked) return 0;
            var rotation = _controller.Draft.HomeRotation.Contains(playerId)
                ? _controller.Draft.HomeRotation
                : _controller.Draft.AwayRotation;
            return rotation.IndexOf(playerId) + 1;
        }

        private Color DefaultPlayerColor(string id)
        {
            return _controller.Draft.Context.Home.Players.Any(value =>
                value.PlayerId.Value == id)
                ? new Color(.12f, .45f, .95f)
                : new Color(.95f, .32f, .18f);
        }

        private void UpdateTrajectory()
        {
            if (_trajectory == null) return;
            var draft = _controller.Draft;
            if (!draft.BallPosition.IsFinite ||
                !draft.BallVelocity.IsFinite)
            {
                _trajectory.positionCount = 0;
                return;
            }

            var state = new BallState(
                draft.BallPosition,
                draft.BallVelocity,
                SimulatedBall.DefaultRadius);
            var parameters = new BallSimulationParameters(-9.8f, .9995f);
            var points = new List<Vector3> { ToUnity(state.Position) };
            for (var step = 0; step < 180; step++)
            {
                BallIntegrator.Step(
                    state,
                    SimulatedBall.DefaultFixedStep,
                    parameters);
                points.Add(ToUnity(state.Position));
                if (state.Position.Y <= SimulatedBall.DefaultRadius)
                    break;
            }

            _trajectory.positionCount = points.Count;
            _trajectory.SetPositions(points.ToArray());
        }

        private void DestroyPreview()
        {
            _observation?.Dispose();
            _observation = null;
            _markers.Clear();
            _trajectory = null;
            _worldCamera = null;
            if (_previewRoot == null) return;
            if (Application.isPlaying)
                Destroy(_previewRoot);
            else
                DestroyImmediate(_previewRoot);
            _previewRoot = null;
        }


        private void OnKeyDown(KeyDownEvent value)
        {
            try
            {
                switch (value.keyCode)
                {
                    case KeyCode.P:
                    case KeyCode.Space:
                        TogglePause();
                        value.StopPropagation();
                        break;
                    case KeyCode.Period:
                        if (_controller.State ==
                            TrainingScenarioLabStateV1.Paused)
                            _controller.Step();
                        value.StopPropagation();
                        break;
                    case KeyCode.R:
                        if (_controller.State ==
                            TrainingScenarioLabStateV1.Completed)
                            _controller.RerunSameSeed();
                        value.StopPropagation();
                        break;
                    case KeyCode.Escape:
                        if (_controller.State ==
                                TrainingScenarioLabStateV1.Completed ||
                            _controller.State ==
                                TrainingScenarioLabStateV1.Faulted)
                            _controller.ReturnToEditing();
                        value.StopPropagation();
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                // A key may arrive on the exact frame the formal run resolves.
            }
        }

        private void TogglePause()
        {
            if (_controller.State == TrainingScenarioLabStateV1.Running)
                _controller.Pause();
            else if (_controller.State ==
                     TrainingScenarioLabStateV1.Paused)
                _controller.Resume();
        }

        private void ApplyPosition()
        {
            if (_rendering || _controller.EditingLocked) return;
            var position = new SimVector3(
                _positionX.value,
                _positionY.value,
                _positionZ.value);
            if (_controller.SelectedObjectId == "ball")
            {
                _controller.SetBallPosition(position);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_controller.SelectedObjectId))
                _controller.SetPlayerPosition(
                    new StablePlayerId(_controller.SelectedObjectId),
                    position);
        }

        private void ApplyVelocity()
        {
            if (_rendering ||
                _controller.EditingLocked ||
                _controller.SelectedObjectId != "ball")
                return;
            _controller.SetBallVelocity(new SimVector3(
                _velocityX.value,
                _velocityY.value,
                _velocityZ.value));
        }

        private void ApplyRallyStart()
        {
            if (_rendering || _controller.EditingLocked) return;
            var lastActor = _lastActor.value == "无"
                ? (StablePlayerId?)null
                : new StablePlayerId(_lastActor.value);
            _controller.SetRallyStart(
                Enum.Parse<RallyStartRecipeV3>(_recipe.value),
                Enum.Parse<TeamSide>(_sourceTeam.value),
                lastActor);
        }

        private void ApplyTactics(
            TeamSide side,
            DropdownField set,
            DropdownField spike)
        {
            if (_rendering || _controller.EditingLocked) return;
            _controller.SetTacticRoutes(
                side,
                Enum.Parse<SetRoute>(set.value),
                Enum.Parse<SpikeRoute>(spike.value));
        }

        private void SetTacticValues()
        {
            _homeSet.SetValueWithoutNotify(
                _controller.Draft.HomeTactics.SetRoute.ToString());
            _homeSpike.SetValueWithoutNotify(
                _controller.Draft.HomeTactics.SpikeRoute.ToString());
            _awaySet.SetValueWithoutNotify(
                _controller.Draft.AwayTactics.SetRoute.ToString());
            _awaySpike.SetValueWithoutNotify(
                _controller.Draft.AwayTactics.SpikeRoute.ToString());
        }

        private void ExportEvidence()
        {
#if UNITY_EDITOR
            var evidence = _controller.VisibleEvidence;
            if (evidence == null || evidence.Decisions.Count == 0) return;
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .First(value =>
                        value.GetName().Name ==
                        "Volleyball.Match.Editor")
                    .GetType(
                        "Volleyball.Editor." +
                        "TrainingDecisionSnapshotExporterV1",
                        true);
                var path = (string)type
                    .GetMethod(
                        "Export",
                        BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { evidence, null });
                _feedback.text = "已导出 · " + path;
            }
            catch (Exception exception)
            {
                _feedback.text = "导出失败 · " + exception.Message;
            }
#else
            _feedback.text = "Player 只显示快照摘要，不写数据集文件。";
#endif
        }

        private void OpenSetterReview()
        {
#if UNITY_EDITOR
            var snapshot = _controller.VisibleEvidence?.SetterTargets
                .LastOrDefault();
            if (snapshot == null) return;
            try
            {
                var editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(value => value.GetName().Name ==
                        "Volleyball.Match.AI.Editor");
                var type = editorAssembly?.GetType(
                    "Volleyball.Editor.AI.SetterTeacher." +
                    "SetterTeacherReviewWindowV1");
                type?.GetMethod(
                    "OpenForSnapshot",
                    BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { snapshot.SnapshotHash });
                if (type == null)
                    throw new InvalidOperationException(
                        "Setter teacher review editor tools are unavailable.");
            }
            catch (Exception exception)
            {
                _feedback.text = "无法打开二传审核 · " + exception.Message;
            }
#else
            _feedback.text = "二传审核只在 Unity Editor 训练室中可用。";
#endif
        }

        private void TryApplyChineseSystemFont(VisualElement root)
        {
            try
            {
                _runtimeFont = Font.CreateDynamicFontFromOSFont(
                    new[]
                    {
                        "PingFang SC",
                        "Microsoft YaHei UI",
                        "Microsoft YaHei",
                        "Noto Sans CJK SC"
                    },
                    16);
                if (_runtimeFont != null)
                    root.style.unityFont = _runtimeFont;
            }
            catch (Exception)
            {
                _runtimeFont = null;
            }
        }

        private static Vector3 ToUnity(SimVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static string StateText(
            TrainingScenarioLabStateV1 state)
        {
            return state switch
            {
                TrainingScenarioLabStateV1.Editing => "编辑中",
                TrainingScenarioLabStateV1.Validating => "校验中",
                TrainingScenarioLabStateV1.Ready => "已就绪",
                TrainingScenarioLabStateV1.Running => "正式运行中",
                TrainingScenarioLabStateV1.Paused => "已暂停",
                TrainingScenarioLabStateV1.Completed => "本回合已完成",
                TrainingScenarioLabStateV1.Faulted => "运行异常",
                _ => state.ToString()
            };
        }

        private void OnDestroy()
        {
            DestroyPreview();
            if (_controller != null)
                _controller.Changed -= Render;
            if (_ownsController)
            {
                _controller?.Dispose();
                _simulation?.Dispose();
            }
            if (_runtimeFont != null)
                Destroy(_runtimeFont);
        }
    }
}
