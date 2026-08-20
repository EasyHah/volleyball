using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Match.Domain.PreServe;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class TrainingScenarioLabView : MonoBehaviour
    {
        private UIDocument _document;
        private TrainingLabWorkbenchControllerV2 _controller;
        private TrainingRallySimulationControllerV5 _runtime;
        private TrainingLabLocalScenarioRepositoryV2 _repository;
        private Transform _worldHost;
        private Camera _backgroundCamera;
        private GameObject _previewRoot;
        private Camera _previewCamera;
        private RenderTexture _previewTexture;
        private readonly Dictionary<string, Transform> _previewMarkers =
            new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly HashSet<PlayerId> _previewHomePlayers =
            new HashSet<PlayerId>();
        private readonly Dictionary<string, PreviewCameraBookmark> _bookmarks =
            new Dictionary<string, PreviewCameraBookmark>(StringComparer.Ordinal);
        private VisualElement _root, _hub, _workbench, _standard, _recent;
        private VisualElement _rotationBoard, _homeRotation, _awayRotation;
        private VisualElement _board, _court, _tokens, _faults;
        private VisualElement _horizontalRuler, _verticalRuler;
        private VisualElement _serveSelector, _serveSide, _preview3d;
        private VisualElement _topTrajectory, _sideTrajectory;
        private VisualElement _preview3dViewport;
        private VisualElement _bookmarkList;
        private VisualElement _leaveModal, _timeline;
        private Label _state, _hash, _rules, _feedback, _selection;
        private Label _scenarioName;
        private Label _inspectorTitle, _inspectorSummary, _serveBlock;
        private Label _serveToolSummary, _preflightSummary, _runningSummary;
        private Label _leaveMessage;
        private Label _seedLabel;
        private TextField _displayName;
        private TextField _bookmarkName;
        private FloatField _positionX, _positionY, _positionZ;
        private FloatField _servePositionX, _servePositionY, _servePositionZ;
        private FloatField _velocityX, _velocityY, _velocityZ;
        private Button _save, _returnHub, _run, _pause, _step, _rerun;
        private Button _returnEdit;
        private Rect _courtRect;
        private float _courtLeft, _courtTop;
        private bool _rendering;
        private bool _showingHub = true;
        private string _dragObjectId;
        private TeamSide? _rotationDragSide;
        private int _rotationDragSlot;
        private VisualElement _rotationDragCard;
        private int _rotationDragPointer = -1;
        private string _rulerDragAxis;
        private int _rulerDragPointer = -1;
        private int _previewPointer = -1;
        private Vector2 _previewPointerPosition;
        private float _previewYaw = 32f;
        private float _previewPitch = 28f;
        private float _previewDistance = 22f;

        // Compatibility probe only. The scene no longer owns a legacy controller.
        public object Controller => null;
        public TrainingLabWorkbenchControllerV2 V5Controller => _controller;

        private void Awake() => _document = GetComponent<UIDocument>();

        private void OnEnable()
        {
            InitializeVisualTree();
            EnsureBackgroundCamera();
            if (_controller == null)
                OpenTemplate("standard-rotation", false);
            else
            {
                _controller.Changed -= Render;
                _controller.Changed += Render;
                Render();
            }
        }

        private void OnDisable()
        {
            if (_controller != null) _controller.Changed -= Render;
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.Changed -= Render;
            _controller?.Dispose();
            _runtime?.Dispose();
            if (_backgroundCamera != null)
                Destroy(_backgroundCamera.gameObject);
            DestroyReadonly3dPreview();
        }

        private void EnsureBackgroundCamera()
        {
            if (_backgroundCamera != null) return;
            var cameraObject = new GameObject("TrainingLabBackgroundCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            _backgroundCamera = cameraObject.AddComponent<Camera>();
            _backgroundCamera.enabled = true;
            _backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            _backgroundCamera.backgroundColor = new Color32(5, 17, 24, 255);
            _backgroundCamera.cullingMask = 0;
            _backgroundCamera.depth = -100f;
        }

        private void InitializeVisualTree()
        {
            if (_root != null) return;
            _root = (_document ?? GetComponent<UIDocument>()).rootVisualElement;
            _hub = _root.Q("scenario-hub");
            _workbench = _root.Q("workbench-shell");
            _standard = _root.Q("standard-scenarios");
            _recent = _root.Q("continue-scenarios");
            _rotationBoard = _root.Q("rotation-board");
            _homeRotation = _root.Q("rotation-home-grid");
            _awayRotation = _root.Q("rotation-away-grid");
            _board = _root.Q("tactical-board");
            _court = _root.Q("court-surface");
            _tokens = _root.Q("tactical-token-layer");
            _faults = _root.Q("position-fault-layer");
            _horizontalRuler = _root.Q("horizontal-ruler");
            _verticalRuler = _root.Q("vertical-ruler");
            _serveSelector = _root.Q("serve-view-selector");
            _serveSide = _root.Q("serve-side-board");
            _topTrajectory = _root.Q("serve-top-trajectory-layer");
            _sideTrajectory = _root.Q("serve-side-trajectory-layer");
            _preview3d = _root.Q("preview-3d-modal");
            _preview3dViewport = _root.Q("preview-3d-viewport");
            _bookmarkList = _root.Q("preview-bookmark-list");
            _leaveModal = _root.Q("unsaved-leave-modal");
            _timeline = _root.Q("timeline-list");
            _state = _root.Q<Label>("state-label");
            _hash = _root.Q<Label>("hash-label");
            _scenarioName = _root.Q<Label>("scenario-name-label");
            _rules = _root.Q<Label>("rules-label");
            _feedback = _root.Q<Label>("feedback-label");
            _selection = _root.Q<Label>("selection-label");
            _inspectorTitle = _root.Q<Label>("inspector-title");
            _inspectorSummary = _root.Q<Label>("inspector-summary");
            _serveBlock = _root.Q<Label>("serve-setup-block");
            _serveToolSummary = _root.Q<Label>("serve-tool-summary");
            _preflightSummary = _root.Q<Label>("validation-summary");
            _runningSummary = _root.Q<Label>("running-summary");
            _leaveMessage = _root.Q<Label>("leave-message");
            _seedLabel = _root.Q<Label>("match-seed-label");
            _displayName = _root.Q<TextField>("display-name");
            _bookmarkName = _root.Q<TextField>("preview-bookmark-name");
            _velocityX = _root.Q<FloatField>("velocity-x");
            _velocityY = _root.Q<FloatField>("velocity-y");
            _velocityZ = _root.Q<FloatField>("velocity-z");
            _velocityX.label = "VX";
            _velocityY.label = "VY";
            _velocityZ.label = "VZ";
            _save = _root.Q<Button>("save-button");
            _returnHub = _root.Q<Button>("return-to-hub-button");
            _run = _root.Q<Button>("run-button");
            _pause = _root.Q<Button>("pause-button");
            _step = _root.Q<Button>("step-button");
            _rerun = _root.Q<Button>("rerun-button");
            _returnEdit = _root.Q<Button>("edit-button");
            CreatePositionFields();
            RegisterEvents();
        }

        private void CreatePositionFields()
        {
            var row = new VisualElement { name = "position-inputs" };
            row.AddToClassList("vector-row");
            _positionX = new FloatField("X") { name = "position-x" };
            _positionY = new FloatField("Y") { name = "position-y" };
            _positionZ = new FloatField("Z") { name = "position-z" };
            _positionY.style.display = DisplayStyle.None;
            foreach (var field in new[] { _positionX, _positionY, _positionZ })
            {
                field.AddToClassList("vector-field");
                row.Add(field);
                field.RegisterValueChangedCallback(_ => ApplyExactPosition());
            }
            _root.Q("position-input-host-positioning").Add(row);

            var serveRow = new VisualElement { name = "serve-position-inputs" };
            serveRow.AddToClassList("vector-row");
            _servePositionX = new FloatField("X") { name = "serve-position-x" };
            _servePositionY = new FloatField("Y") { name = "serve-position-y" };
            _servePositionZ = new FloatField("Z") { name = "serve-position-z" };
            foreach (var field in new[]
                     { _servePositionX, _servePositionY, _servePositionZ })
            {
                field.AddToClassList("vector-field");
                serveRow.Add(field);
                field.RegisterValueChangedCallback(_ =>
                    ApplyExactServePosition());
            }
            _root.Q("position-input-host-serve").Add(serveRow);
        }

        private void RegisterEvents()
        {
            _root.Q<Button>("hub-new-from-standard-button").clicked += () =>
                ShowWorkbench("builtin:standard-rotation");
            _save.clicked += () => _controller.SaveCurrentLocalScenario();
            _returnHub.clicked += ShowScenarioHub;
            _root.Q<Button>("more-button").clicked += () =>
            {
                var foldout = _root.Q<Foldout>("advanced-settings");
                foldout.value = !foldout.value;
            };
            _root.Q<Button>("confirm-rotation-button").clicked += () =>
                _controller.ConfirmRotation();
            _root.Q<Button>("reopen-rotation-button").clicked += () =>
                _controller.ReopenRotation();
            _root.Q<Button>("positioning-next-button").clicked += () =>
                _controller.ContinueToServeSetup();
            _root.Q<Button>("serve-next-button").clicked += () =>
                _controller.EnterPreflight();
            _root.Q<Button>("context-run-button").clicked += StartRun;
            _run.clicked += StartRun;
            _pause.clicked += TogglePause;
            _step.clicked += () => _controller.StepRuntime();
            _rerun.clicked += () => _controller.RerunSameSnapshot();
            _returnEdit.clicked += () => _controller.ReturnToEditing();
            _root.Q<Button>("step-rotation").clicked += () =>
                _controller.ReopenRotation();
            _root.Q<Button>("step-positioning").clicked += () =>
            {
                if (!_controller.MatchSetup.RotationLocked)
                    _controller.ConfirmRotation();
            };
            _root.Q<Button>("step-serve").clicked += () =>
            {
                if (_controller.CurrentStep == TrainingLabStepV1.Positioning &&
                    _controller.CanEnterServeSetup)
                    _controller.ContinueToServeSetup();
            };
            _root.Q<Button>("step-validation").clicked += () =>
                _controller.EnterPreflight();
            _root.Q<Button>("tool-move-ball").clicked += () =>
                _controller.SetServeTool(TrainingServeToolV1.MoveBall);
            _root.Q<Button>("tool-velocity").clicked += () =>
                _controller.SetServeTool(TrainingServeToolV1.AdjustVelocity);
            _root.Q<Button>("tool-trajectory").clicked += () =>
                _controller.SetServeTool(TrainingServeToolV1.ViewTrajectory);
            _root.Q<Button>("serve-top-view-button").clicked += () =>
                _controller.SetServeView(TrainingServeViewV1.Top);
            _root.Q<Button>("serve-side-view-button").clicked += () =>
                _controller.SetServeView(TrainingServeViewV1.Side);
            _root.Q<Button>("serve-3d-preview-button").clicked += () =>
                OpenReadonly3dPreview();
            _root.Q<Button>("preview-3d-close-button").clicked += () =>
                CloseReadonly3dPreview();
            _root.Q<Button>("preview-3d-reset-button").clicked += () =>
                ResetReadonly3dCamera();
            _root.Q<Button>("preview-bookmark-save-button").clicked +=
                SavePreviewBookmark;
            _displayName.RegisterValueChangedCallback(evt =>
            {
                if (_rendering || _controller.EditingLocked) return;
                var value = evt.newValue?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    _feedback.text = "情景名称不能为空。";
                    _displayName.SetValueWithoutNotify(
                        _controller.LocalScenario.DisplayName);
                    return;
                }
                _controller.LocalScenario.DisplayName = value;
                Render();
            });
            _preview3dViewport.RegisterCallback<PointerDownEvent>(
                OnPreviewPointerDown);
            _preview3dViewport.RegisterCallback<PointerMoveEvent>(
                OnPreviewPointerMove);
            _preview3dViewport.RegisterCallback<PointerUpEvent>(
                OnPreviewPointerUp);
            _preview3dViewport.RegisterCallback<WheelEvent>(OnPreviewWheel);
            _root.Q<Button>("reset-player-overrides-button").clicked += () =>
            {
                if (TrySelectedPlayer(out var id))
                    _controller.ResetPlayerAttributeOverrides(id);
            };
            _root.Q<Button>("leave-save-button").clicked += () =>
                ResolveLeave(TrainingLabLeaveDecisionV1.Save);
            _root.Q<Button>("leave-discard-button").clicked += () =>
                ResolveLeave(TrainingLabLeaveDecisionV1.Discard);
            _root.Q<Button>("leave-cancel-button").clicked += () =>
                ResolveLeave(TrainingLabLeaveDecisionV1.Cancel);
            _velocityX.RegisterValueChangedCallback(_ => ApplyExactVelocity());
            _velocityY.RegisterValueChangedCallback(_ => ApplyExactVelocity());
            _velocityZ.RegisterValueChangedCallback(_ => ApplyExactVelocity());
            _root.Q("serve-side-ball").RegisterCallback<PointerDownEvent>(_ =>
                _dragObjectId = "ball");
            _root.Q("serve-side-velocity-endpoint")
                .RegisterCallback<PointerDownEvent>(_ =>
                    _dragObjectId = "velocity");
            _serveSide.RegisterCallback<PointerMoveEvent>(OnSidePointerMove);
            _serveSide.RegisterCallback<PointerUpEvent>(_ =>
                _dragObjectId = null);
            _serveSide.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_controller.CurrentStep == TrainingLabStepV1.ServeBall &&
                    _controller.ActiveServeView == TrainingServeViewV1.Side)
                    RenderServeSide();
            });
            _board.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                LayoutCourt();
                RenderCourt();
            });
            _board.RegisterCallback<PointerMoveEvent>(OnBoardPointerMove);
            _board.RegisterCallback<PointerUpEvent>(_ => _dragObjectId = null);
            _root.RegisterCallback<PointerUpEvent>(OnRotationPointerUp);
            _root.RegisterCallback<PointerCancelEvent>(_ => ClearRotationDrag());
            _root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ClearRotationDrag();
                ClearRulerDrag();
            });
            _horizontalRuler.RegisterCallback<PointerMoveEvent>(OnRulerPointerMove);
            _verticalRuler.RegisterCallback<PointerMoveEvent>(OnRulerPointerMove);
            _horizontalRuler.RegisterCallback<PointerUpEvent>(OnRulerPointerUp);
            _verticalRuler.RegisterCallback<PointerUpEvent>(OnRulerPointerUp);
            _horizontalRuler.RegisterCallback<PointerCancelEvent>(_ =>
                ClearRulerDrag());
            _verticalRuler.RegisterCallback<PointerCancelEvent>(_ =>
                ClearRulerDrag());
        }

        public void ShowWorkbench(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
                throw new ArgumentException(
                    "TrainingLab entry key is required.", nameof(entryKey));
            const string builtInPrefix = "builtin:";
            const string localPrefix = "local:";
            if (entryKey.StartsWith(builtInPrefix, StringComparison.Ordinal))
            {
                var id = entryKey.Substring(builtInPrefix.Length);
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException(
                        "Built-in TrainingLab entry ID is required.",
                        nameof(entryKey));
                OpenTemplate(id, true);
                return;
            }
            if (entryKey.StartsWith(localPrefix, StringComparison.Ordinal))
            {
                var id = entryKey.Substring(localPrefix.Length);
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException(
                        "Local TrainingLab entry ID is required.",
                        nameof(entryKey));
                OpenLocal(id);
                return;
            }
            throw new ArgumentException(
                "TrainingLab entry key must use builtin: or local:.",
                nameof(entryKey));
        }

        public void ShowScenarioHub()
        {
            var request = _controller.RequestLeaveToHub();
            if (request.IsBlocked) { _feedback.text = request.Message; return; }
            if (request.RequiresDecision)
            {
                _leaveMessage.text = request.Message;
                _leaveModal.style.display = DisplayStyle.Flex;
                return;
            }
            NavigateHub();
        }

        private void ResolveLeave(TrainingLabLeaveDecisionV1 decision)
        {
            try
            {
                var result = _controller.ResolveLeave(decision);
                _leaveModal.style.display = DisplayStyle.None;
                if (result.CanLeave) NavigateHub();
            }
            catch (Exception exception) { _leaveMessage.text = exception.Message; }
        }

        private void NavigateHub() { _showingHub = true; Render(); }

        private void OpenTemplate(string id, bool showWorkbench)
        {
            DestroyReadonly3dPreview();
            if (_controller != null) _controller.Changed -= Render;
            _controller?.Dispose();
            _runtime?.Dispose();
            if (_worldHost == null)
            {
                var host = new GameObject("TrainingWorldHostV5");
                host.transform.SetParent(transform, false);
                _worldHost = host.transform;
            }
            _runtime = new TrainingRallySimulationControllerV5(_worldHost);
            _repository ??= new TrainingLabLocalScenarioRepositoryV2();
            var template = TrainingScenarioCatalogV2.Create(id);
            var local = TrainingLabLocalScenarioV2.Create(
                "training-" + Guid.NewGuid().ToString("N"),
                template.DisplayName,
                TrainingScenarioCatalogV2.CreateSetup(id),
                TrainingLabStepV1.Rotation.ToString(), "Top", "MoveBall", "ball");
            _controller = new TrainingLabWorkbenchControllerV2(
                _repository, local, _runtime);
            RestorePreviewBookmarks();
            _showingHub = !showWorkbench;
            _controller.Changed += Render;
            Render();
        }

        private void Render()
        {
            if (_root == null || _controller == null) return;
            _rendering = true;
            try
            {
                _hub.style.display = _showingHub ? DisplayStyle.Flex : DisplayStyle.None;
                _workbench.style.display = _showingHub ? DisplayStyle.None : DisplayStyle.Flex;
                RenderHub();
                if (_showingHub) return;
                _scenarioName.text = _controller.LocalScenario.DisplayName +
                                     " · 正式 6v6";
                _state.text = StateText(_controller.State);
                _hash.text = _controller.PreflightSnapshot == null
                    ? "HASH · 自动预检待执行"
                    : "HASH · " + _controller.PreflightSnapshot.SetupHash.Substring(0, 16);
                _rules.text = "原生 V5 · 单回合 · 发球方 " +
                    _controller.MatchSetup.FirstServingSide;
                _displayName.SetValueWithoutNotify(
                    _controller.LocalScenario.DisplayName);
                _displayName.SetEnabled(!_controller.EditingLocked);
                _seedLabel.text = "seed · " +
                    _controller.MatchSetup.BaseContext.Seed;
                _feedback.text = string.IsNullOrWhiteSpace(_controller.RuntimeError)
                    ? _controller.PreflightError : _controller.RuntimeError;
                var selected = SelectedPosition();
                _positionX.SetValueWithoutNotify(selected.X);
                _positionY.SetValueWithoutNotify(selected.Y);
                _positionZ.SetValueWithoutNotify(selected.Z);
                var ballPosition = _controller.MatchSetup.BallPosition;
                _servePositionX.SetValueWithoutNotify(ballPosition.X);
                _servePositionY.SetValueWithoutNotify(ballPosition.Y);
                _servePositionZ.SetValueWithoutNotify(ballPosition.Z);
                var velocity = _controller.MatchSetup.BallVelocity;
                _velocityX.SetValueWithoutNotify(velocity.X);
                _velocityY.SetValueWithoutNotify(velocity.Y);
                _velocityZ.SetValueWithoutNotify(velocity.Z);
                RenderRotation();
                RenderContext();
                LayoutCourt();
                RenderCourt();
                RenderOverrides();
                RenderOutcome();
                RenderControls();
                if (_preview3d.style.display == DisplayStyle.Flex)
                    UpdateReadonly3dPreview();
            }
            finally { _rendering = false; }
        }

        private void RenderHub()
        {
            _standard.Clear();
            _recent.Clear();
            foreach (var id in TrainingScenarioCatalogV2.ScenarioIds)
            {
                var captured = id;
                var template = TrainingScenarioCatalogV2.Create(id);
                var card = new VisualElement();
                card.AddToClassList("scenario-card");
                card.Add(new Label(template.DisplayName));
                card.Add(new Label("原生 V5 标准模板"));
                card.Add(new Button(() =>
                {
                    OpenTemplate(captured, true);
                }) { text = "打开" });
                _standard.Add(card);
            }
            var locals = _repository.List();
            foreach (var entry in locals)
            {
                var card = new VisualElement();
                card.AddToClassList("scenario-card");
                card.Add(new Label(entry.DisplayName));
                card.Add(new Label(entry.IsAvailable
                    ? "本地 V2 情景"
                    : entry.Diagnostic));
                if (entry.IsAvailable)
                {
                    var localId = entry.LocalId;
                    card.Add(new Button(() => OpenLocal(localId))
                    { text = "继续编辑" });
                }
                _recent.Add(card);
            }
            if (locals.Count == 0)
                _recent.Add(new Label(
                    "尚无已保存的本地情景。打开标准情景即可开始。"));
        }

        private void OpenLocal(string localId)
        {
            DestroyReadonly3dPreview();
            _controller.Changed -= Render;
            _controller.Dispose();
            _runtime.Dispose();
            _runtime = new TrainingRallySimulationControllerV5(_worldHost);
            _controller = new TrainingLabWorkbenchControllerV2(_repository,
                _repository.Load(localId), _runtime, true);
            RestorePreviewBookmarks();
            _controller.Changed += Render;
            _showingHub = false;
            Render();
        }

        private void RenderRotation()
        {
            _homeRotation.Clear();
            _awayRotation.Clear();
            RenderRotationSide(TeamSide.Home, _homeRotation,
                _controller.MatchSetup.HomeRotation);
            RenderRotationSide(TeamSide.Away, _awayRotation,
                _controller.MatchSetup.AwayRotation);
        }

        private void RenderRotationSide(TeamSide side, VisualElement grid,
            IReadOnlyList<PlayerId> rotation)
        {
            var displaySlots = new[] { 4, 5, 3, 6, 2, 1 };
            foreach (var slot in displaySlots)
            {
                var player = Player(rotation[slot - 1]);
                var card = new VisualElement
                {
                    name = "rotation-" + side.ToString().ToLowerInvariant() +
                           "-slot-" + slot,
                    userData = new RotationCardBinding(side, slot)
                };
                card.AddToClassList("rotation-card");
                card.AddToClassList("rotation-slot-" + slot);
                var slotLabel = new Label(slot + "号位");
                slotLabel.AddToClassList("rotation-card-slot");
                card.Add(slotLabel);
                var name = new Label(player.DisplayName + " · #" +
                                     player.JerseyNumber);
                name.AddToClassList("rotation-card-name");
                card.Add(name);
                var role = new Label("注册位置：" + PositionText(player.Position));
                role.AddToClassList("rotation-card-role");
                card.Add(role);
                card.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0 || _controller.EditingLocked) return;
                    ClearRotationDrag();
                    _rotationDragSide = side;
                    _rotationDragSlot = slot;
                    _rotationDragCard = card;
                    _rotationDragPointer = evt.pointerId;
                    card.AddToClassList("rotation-card-dragging");
                    card.CapturePointer(evt.pointerId);
                });
                card.RegisterCallback<PointerUpEvent>(OnRotationPointerUp);
                card.RegisterCallback<PointerCancelEvent>(_ =>
                    ClearRotationDrag());
                grid.Add(card);
            }
        }

        private void RenderContext()
        {
            var step = _controller.CurrentStep;
            SetVisible("context-rotation", step == TrainingLabStepV1.Rotation);
            SetVisible("context-positioning", step == TrainingLabStepV1.Positioning);
            SetVisible("context-serve-ball", step == TrainingLabStepV1.ServeBall);
            SetVisible("context-validation", step == TrainingLabStepV1.Validation);
            SetVisible("context-running", step == TrainingLabStepV1.Running);
            _rotationBoard.style.display = step == TrainingLabStepV1.Rotation
                ? DisplayStyle.Flex : DisplayStyle.None;
            _board.style.display = step == TrainingLabStepV1.Rotation
                ? DisplayStyle.None : DisplayStyle.Flex;
            var serve = step == TrainingLabStepV1.ServeBall;
            _serveSelector.style.display = serve ? DisplayStyle.Flex : DisplayStyle.None;
            _serveSide.style.display = serve &&
                _controller.ActiveServeView == TrainingServeViewV1.Side
                ? DisplayStyle.Flex : DisplayStyle.None;
            _inspectorTitle.text = StepText(step);
            _inspectorSummary.text = step == TrainingLabStepV1.Validation
                ? "进入本页即自动校验并冻结。" : string.Empty;
            _selection.text = SelectionText();
            _serveBlock.text = _controller.CanEnterServeSetup
                ? "站位合法。下一步：设置发球球。"
                : _controller.MatchSetup.RotationLocked
                    ? "请先修正全部位置错误，才能继续设置发球球。"
                    : "请先确认并锁定轮转位次。";
            RenderPositionFaultSummary();
            _serveToolSummary.text = _controller.ServeTool.ToString();
            var selectedPlayer = TrySelectedPlayer(out _);
            _positionX.SetEnabled(step == TrainingLabStepV1.Positioning &&
                                  selectedPlayer);
            _positionZ.SetEnabled(step == TrainingLabStepV1.Positioning &&
                                  selectedPlayer);
            var moveBall = serve && _controller.ServeTool ==
                TrainingServeToolV1.MoveBall;
            var top = _controller.ActiveServeView == TrainingServeViewV1.Top;
            _servePositionX.SetEnabled(moveBall && top);
            _servePositionY.SetEnabled(moveBall && !top);
            _servePositionZ.SetEnabled(moveBall);
            var velocity = serve && _controller.ServeTool ==
                TrainingServeToolV1.AdjustVelocity;
            _velocityX.SetEnabled(velocity && top);
            _velocityY.SetEnabled(velocity && !top);
            _velocityZ.SetEnabled(velocity);
            _preflightSummary.text = _controller.PreflightSnapshot == null
                ? "自动预检未通过：" + _controller.PreflightError
                : "自动预检通过 · " +
                  _controller.PreflightSnapshot.SetupHash.Substring(0, 16);
            _root.Q<Button>("positioning-next-button").SetEnabled(
                _controller.CanEnterServeSetup);
            if (serve && _controller.ActiveServeView == TrainingServeViewV1.Side)
                RenderServeSide();
        }

        private void LayoutCourt()
        {
            var width = _board.contentRect.width;
            var height = _board.contentRect.height;
            if (width < 10f || height < 10f) return;
            var courtWidth = Mathf.Min(Mathf.Max(80f, width - 154f),
                Mathf.Max(80f, height - 72f) * 2f);
            var courtHeight = courtWidth * .5f;
            var left = Mathf.Max(76f, (width - courtWidth) * .5f);
            var top = Mathf.Max(28f, (height - courtHeight - 30f) * .5f);
            _courtLeft = left;
            _courtTop = top;
            _court.style.left = left; _court.style.top = top;
            _court.style.width = courtWidth; _court.style.height = courtHeight;
            _horizontalRuler.style.left = left;
            _horizontalRuler.style.top = top + courtHeight + 8f;
            _horizontalRuler.style.width = courtWidth;
            _verticalRuler.style.left = left - 31f; _verticalRuler.style.top = top;
            _verticalRuler.style.height = courtHeight;
            _courtRect = new Rect(0f, 0f, courtWidth, courtHeight);
            RenderRulers();
        }

        private void RenderCourt()
        {
            if (_courtRect.width < 10f) return;
            _tokens.Clear(); _faults.Clear();
            _topTrajectory.Clear();
            var home = new HashSet<PlayerId>(_controller.MatchSetup.HomeRotation);
            var focused = new HashSet<PlayerId>(_controller.FocusedPlayerIds);
            foreach (var pose in _controller.MatchSetup.Players)
            {
                var id = pose.PlayerId;
                var token = new VisualElement { userData = id.Value };
                token.AddToClassList("tactical-token");
                token.AddToClassList(home.Contains(id) ? "home-token" : "away-token");
                if (_controller.SelectedObjectId == id.Value)
                    token.AddToClassList("selected-token");
                if (_controller.PositionFaults.Any(f =>
                        f.RequiredAheadOrLeft.PlayerId.Equals(id) ||
                        f.ViolatingBehindOrRight.PlayerId.Equals(id)))
                    token.AddToClassList("fault-token");
                if (focused.Contains(id))
                    token.AddToClassList("focused-fault-token");
                var point = TrainingLabCourtProjectionV1.CourtToBoard(
                    _courtRect, pose.Position);
                token.style.left = point.x; token.style.top = point.y;
                token.Add(new Label(Player(id).DisplayName));
                token.RegisterCallback<PointerDownEvent>(_ =>
                { _controller.SelectObject(id); _dragObjectId = id.Value; });
                _tokens.Add(token);
            }
            _board.Q("training-ball-token")?.RemoveFromHierarchy();
            _board.Q("training-velocity-endpoint")?.RemoveFromHierarchy();
            var ball = new VisualElement
            {
                name = "training-ball-token",
                userData = "ball"
            };
            ball.AddToClassList("tactical-token"); ball.AddToClassList("ball-token");
            var ballPoint = TrainingLabCourtProjectionV1.CourtToServeBoard(
                _courtRect, _controller.MatchSetup.BallPosition);
            ball.style.left = _courtLeft + ballPoint.x;
            ball.style.top = _courtTop + ballPoint.y;
            ball.Add(new Label("球"));
            ball.RegisterCallback<PointerDownEvent>(_ => _dragObjectId = "ball");
            _board.Add(ball);
            if (_controller.CurrentStep == TrainingLabStepV1.ServeBall &&
                _controller.ActiveServeView == TrainingServeViewV1.Top)
            {
                var velocityEndpoint = new VisualElement
                {
                    name = "training-velocity-endpoint",
                    userData = "velocity"
                };
                velocityEndpoint.AddToClassList("serve-velocity-endpoint");
                var velocity = _controller.MatchSetup.BallVelocity;
                var endpoint = TrainingLabCourtProjectionV1.CourtToServeBoard(
                    _courtRect, new SimVector3(
                        _controller.MatchSetup.BallPosition.X + velocity.X * .15f,
                        _controller.MatchSetup.BallPosition.Y,
                        _controller.MatchSetup.BallPosition.Z + velocity.Z * .15f));
                velocityEndpoint.style.left = _courtLeft + endpoint.x - 8f;
                velocityEndpoint.style.top = _courtTop + endpoint.y - 8f;
                velocityEndpoint.RegisterCallback<PointerDownEvent>(_ =>
                    _dragObjectId = "velocity");
                _board.Add(velocityEndpoint);
                if (_controller.ServeTool == TrainingServeToolV1.ViewTrajectory)
                    RenderTopTrajectory();
            }
            foreach (var fault in _controller.PositionFaults)
            {
                var required = TrainingLabCourtProjectionV1.CourtToBoard(
                    _courtRect, fault.RequiredAheadOrLeft.FootProjection);
                var violating = TrainingLabCourtProjectionV1.CourtToBoard(
                    _courtRect, fault.ViolatingBehindOrRight.FootProjection);
                AddFaultRelation(required, violating, fault);
                var correction = TrainingLabCourtProjectionV1.CourtToBoard(_courtRect,
                    TrainingLabCourtProjectionV1.ShortestLegalCorrection(fault));
                AddFaultArrow(violating, correction, fault);
            }
            RenderRulers();
        }

        private void RenderPositionFaultSummary()
        {
            var host = _root.Q("position-fault-summary");
            host.Clear();
            if (_controller.CurrentStep != TrainingLabStepV1.Positioning)
                return;
            var faults = _controller.PositionFaults;
            if (faults.Count == 0)
            {
                var legal = new Label("当前轮转站位合法，无位置错误。");
                legal.AddToClassList("position-legal-summary");
                host.Add(legal);
                return;
            }
            for (var index = 0; index < faults.Count; index++)
            {
                var captured = index;
                var fault = faults[index];
                var required = Player(fault.RequiredAheadOrLeft.PlayerId);
                var violating = Player(fault.ViolatingBehindOrRight.PlayerId);
                var card = new Button(() =>
                    _controller.FocusPositionFault(captured))
                {
                    text = SideText(fault.Side) + " · " +
                           fault.RequiredAheadOrLeft.Slot + "号位 " +
                           required.DisplayName + " / " +
                           fault.ViolatingBehindOrRight.Slot + "号位 " +
                           violating.DisplayName + "\n" +
                           RuleText(fault.Rule) + "；" +
                           CorrectionText(fault)
                };
                card.AddToClassList("position-fault-card");
                if (IsFocused(fault))
                    card.AddToClassList("focused-position-fault-card");
                host.Add(card);
            }
        }

        private void RenderRulers()
        {
            if (_horizontalRuler == null || _verticalRuler == null) return;
            _horizontalRuler.Clear();
            _verticalRuler.Clear();
            var positioning = _controller.CurrentStep ==
                              TrainingLabStepV1.Positioning &&
                              TrySelectedPlayer(out var selected);
            _horizontalRuler.style.display = positioning
                ? DisplayStyle.Flex : DisplayStyle.None;
            _verticalRuler.style.display = positioning
                ? DisplayStyle.Flex : DisplayStyle.None;
            if (!positioning || _courtRect.width < 10f) return;

            AddRulerTrack(_horizontalRuler);
            AddRulerTrack(_verticalRuler);
            for (var index = 0; index <= 18; index++)
            {
                var percent = index / 18f * 100f;
                AddRulerTick(_horizontalRuler, percent, index % 3 == 0
                    ? Mathf.Abs(index - 9).ToString()
                    : null, true);
            }
            for (var index = 0; index <= 9; index++)
            {
                var percent = index / 9f * 100f;
                AddRulerTick(_verticalRuler, percent,
                    index % 3 == 0 ? (9 - index).ToString() : null, false);
            }

            var pose = _controller.MatchSetup.Players.Single(player =>
                player.PlayerId.Equals(selected));
            var point = TrainingLabCourtProjectionV1.CourtToBoard(
                _courtRect, pose.Position);
            AddRulerPoint(_horizontalRuler, selected, point.x, 7f,
                "horizontal");
            AddRulerPoint(_verticalRuler, selected, 15f, point.y,
                "vertical");
        }

        private static void AddRulerTrack(VisualElement ruler)
        {
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("ruler-track");
            ruler.Add(track);
        }

        private static void AddRulerTick(VisualElement ruler, float percent,
            string text, bool horizontal)
        {
            var tick = new VisualElement { pickingMode = PickingMode.Ignore };
            tick.AddToClassList("ruler-tick");
            if (horizontal) tick.style.left = Length.Percent(percent);
            else tick.style.top = Length.Percent(percent);
            ruler.Add(tick);
            if (string.IsNullOrEmpty(text)) return;
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ruler-label");
            if (horizontal) label.style.left = Length.Percent(percent);
            else label.style.top = Length.Percent(percent);
            ruler.Add(label);
        }

        private void AddRulerPoint(VisualElement ruler, PlayerId playerId,
            float left, float top, string axis)
        {
            var point = new VisualElement { userData = playerId.Value };
            point.AddToClassList("selected-ruler-point");
            if (_controller.FocusedPlayerIds.Contains(playerId))
                point.AddToClassList("focused-ruler-point");
            point.style.left = left;
            point.style.top = top;
            point.RegisterCallback<PointerDownEvent>(evt =>
                BeginRulerDrag(axis, evt));
            ruler.Add(point);
        }

        private void BeginRulerDrag(string axis, PointerDownEvent evt)
        {
            if (evt.button != 0 || _controller.EditingLocked ||
                _controller.CurrentStep != TrainingLabStepV1.Positioning ||
                !TrySelectedPlayer(out _)) return;
            ClearRulerDrag();
            _rulerDragAxis = axis;
            _rulerDragPointer = evt.pointerId;
            var ruler = axis == "horizontal"
                ? _horizontalRuler : _verticalRuler;
            ruler.CapturePointer(evt.pointerId);
            ApplyRulerPointer(evt.position);
            evt.StopPropagation();
        }

        private void OnRulerPointerMove(PointerMoveEvent evt)
        {
            if (_rulerDragPointer != evt.pointerId) return;
            ApplyRulerPointer(evt.position);
        }

        private void OnRulerPointerUp(PointerUpEvent evt)
        {
            if (_rulerDragPointer != evt.pointerId) return;
            ApplyRulerPointer(evt.position);
            ClearRulerDrag();
        }

        private void ApplyRulerPointer(Vector3 worldPosition)
        {
            if (!TrySelectedPlayer(out var playerId)) return;
            if (_rulerDragAxis == "horizontal")
            {
                var point = _horizontalRuler.WorldToLocal(worldPosition);
                _controller.SetPlayerDepthFromHorizontalRuler(
                    playerId, _courtRect, point.x);
            }
            else if (_rulerDragAxis == "vertical")
            {
                var point = _verticalRuler.WorldToLocal(worldPosition);
                _controller.SetPlayerLateralFromVerticalRuler(
                    playerId, _courtRect, point.y);
            }
        }

        private void ClearRulerDrag()
        {
            var axis = _rulerDragAxis;
            var pointer = _rulerDragPointer;
            _rulerDragAxis = null;
            _rulerDragPointer = -1;
            var ruler = axis == "horizontal" ? _horizontalRuler :
                axis == "vertical" ? _verticalRuler : null;
            if (ruler != null && pointer >= 0 &&
                ruler.HasPointerCapture(pointer))
                ruler.ReleasePointer(pointer);
        }

        private void AddFaultRelation(Vector2 from, Vector2 to,
            PositionFaultV1 fault)
        {
            var delta = to - from;
            var relation = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            relation.AddToClassList("fault-relation");
            if (IsFocused(fault)) relation.AddToClassList("focused-fault-overlay");
            relation.style.left = from.x;
            relation.style.top = from.y;
            relation.style.width = delta.magnitude;
            relation.style.height = 3f;
            relation.style.rotate = new Rotate(Mathf.Atan2(delta.y, delta.x) *
                                               Mathf.Rad2Deg);
            for (var left = 0f; left < delta.magnitude; left += 12f)
            {
                var dash = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                dash.AddToClassList("fault-relation-dash");
                dash.style.left = left;
                dash.style.width = Mathf.Min(7f, delta.magnitude - left);
                relation.Add(dash);
            }
            _faults.Add(relation);
        }

        private void AddFaultArrow(Vector2 from, Vector2 to,
            PositionFaultV1 fault)
        {
            var delta = to - from;
            var arrow = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            arrow.AddToClassList("fault-arrow");
            if (IsFocused(fault)) arrow.AddToClassList("focused-fault-overlay");
            arrow.style.left = from.x;
            arrow.style.top = from.y;
            arrow.style.width = delta.magnitude;
            arrow.style.height = 3f;
            arrow.style.rotate = new Rotate(Mathf.Atan2(delta.y, delta.x) *
                                            Mathf.Rad2Deg);
            _faults.Add(arrow);
            var head = new Label("▶") { pickingMode = PickingMode.Ignore };
            head.AddToClassList("fault-arrow-head");
            head.style.left = to.x - 5f;
            head.style.top = to.y - 10f;
            _faults.Add(head);
        }

        private bool IsFocused(PositionFaultV1 fault)
        {
            return _controller.FocusedPlayerIds.Count == 2 &&
                   _controller.FocusedPlayerIds.Contains(
                       fault.RequiredAheadOrLeft.PlayerId) &&
                   _controller.FocusedPlayerIds.Contains(
                       fault.ViolatingBehindOrRight.PlayerId);
        }

        private string SelectionText()
        {
            if (!TrySelectedPlayer(out var id)) return "未选择球员";
            var player = Player(id);
            var slot = _controller.MatchSetup.HomeRotation.IndexOf(id) + 1;
            var side = TeamSide.Home;
            if (slot == 0)
            {
                slot = _controller.MatchSetup.AwayRotation.IndexOf(id) + 1;
                side = TeamSide.Away;
            }
            return SideText(side) + " · " + slot + "号位 · " +
                   player.DisplayName + " · #" + player.JerseyNumber +
                   " · " + PositionText(player.Position);
        }

        private static string SideText(TeamSide side) =>
            side == TeamSide.Home ? "主队" : "客队";

        private static string RuleText(PositionFaultRuleV1 rule) =>
            rule switch
            {
                PositionFaultRuleV1.Slot4BehindSlot5 => "4号位必须在5号位前方",
                PositionFaultRuleV1.Slot3BehindSlot6 => "3号位必须在6号位前方",
                PositionFaultRuleV1.Slot2BehindSlot1 => "2号位必须在1号位前方",
                PositionFaultRuleV1.Slot4RightOfSlot3 => "4号位必须在3号位左侧",
                PositionFaultRuleV1.Slot3RightOfSlot2 => "3号位必须在2号位左侧",
                PositionFaultRuleV1.Slot5RightOfSlot6 => "5号位必须在6号位左侧",
                PositionFaultRuleV1.Slot6RightOfSlot1 => "6号位必须在1号位左侧",
                _ => rule.ToString()
            };

        private static string CorrectionText(PositionFaultV1 fault)
        {
            var current = TrainingTeamCourtTransformV1.ToLocal(fault.Side,
                fault.ViolatingBehindOrRight.FootProjection);
            var target = TrainingTeamCourtTransformV1.ToLocal(fault.Side,
                TrainingLabCourtProjectionV1.ShortestLegalCorrection(fault));
            var deltaX = target.X - current.X;
            var deltaZ = target.Z - current.Z;
            var direction = Mathf.Abs(deltaZ) >= Mathf.Abs(deltaX)
                ? deltaZ < 0f ? "球网方向" : "本方底线方向"
                : deltaX < 0f ? "队伍局部左侧" : "队伍局部右侧";
            return "将" + fault.ViolatingBehindOrRight.Slot + "号位向" +
                   direction + "移动至蓝色箭头终点";
        }

        private void RenderOverrides()
        {
            var grid = _root.Q("v5-override-grid");
            if (!TrySelectedPlayer(out var id))
            {
                grid.Clear();
                _root.Q<Label>("override-player-identity").text = "选择一名球员";
                return;
            }
            var player = Player(id);
            _root.Q<Label>("override-player-identity").text =
                player.DisplayName + " · #" + player.JerseyNumber;
            TrainingLabV5OverrideInspectorV2.Render(grid, _controller, id);
        }

        private void RenderOutcome()
        {
            _timeline.Clear();
            var outcome = _controller.Outcome;
            if (outcome == null)
            { _timeline.Add(new Label("单回合结束后显示 V5 训练结果。")); return; }
            _runningSummary.text = outcome.WinningSide + " · " +
                outcome.HomeScoreDelta + ":" + outcome.AwayScoreDelta + " · " +
                outcome.CompletionReason + " · 触球 " + outcome.TouchCount +
                " · seed " + outcome.Seed + " · " +
                outcome.SetupHash.Substring(0, 12) + "/" +
                outcome.OutcomeHash.Substring(0, 12);
            foreach (var entry in outcome.Timeline)
                _timeline.Add(new Label(entry.Sequence + " · " + entry.Kind +
                                        " · " + entry.Detail));
        }

        private void RenderControls()
        {
            var state = _controller.State;
            var editable = !_controller.EditingLocked;
            _save.SetEnabled(editable && _controller.IsDirty);
            _returnHub.SetEnabled(editable);
            _run.SetEnabled(state == TrainingScenarioLabStateV1.Ready);
            _pause.SetEnabled(state == TrainingScenarioLabStateV1.Running ||
                              state == TrainingScenarioLabStateV1.Paused);
            _pause.text = state == TrainingScenarioLabStateV1.Paused ? "继续" : "暂停";
            _step.SetEnabled(state == TrainingScenarioLabStateV1.Paused);
            _rerun.SetEnabled(state == TrainingScenarioLabStateV1.Completed);
            _returnEdit.SetEnabled(state == TrainingScenarioLabStateV1.Completed ||
                                   state == TrainingScenarioLabStateV1.Faulted);
        }

        private void RenderServeSide()
        {
            var width = _serveSide.contentRect.width;
            var height = _serveSide.contentRect.height;
            if (width < 10f || height < 10f) return;
            var p = _controller.MatchSetup.BallPosition;
            var v = _controller.MatchSetup.BallVelocity;
            var x = Mathf.Lerp(width * .05f, width * .95f,
                Mathf.InverseLerp(-12f, 12f, p.Z));
            var y = Mathf.Lerp(height * .82f, height * .08f,
                Mathf.InverseLerp(0f, 5f, p.Y));
            var ball = _root.Q("serve-side-ball");
            var endpoint = _root.Q("serve-side-velocity-endpoint");
            ball.style.left = x - 12f; ball.style.top = y - 12f;
            endpoint.style.left = x + v.Z * 6f - 8f;
            endpoint.style.top = y - v.Y * 6f - 8f;
            _sideTrajectory.Clear();
            if (_controller.ServeTool == TrainingServeToolV1.ViewTrajectory)
                RenderSideTrajectory(width, height);
        }

        private void RenderTopTrajectory()
        {
            var points = _controller.PredictTrajectory();
            for (var index = 0; index < points.Count; index += 6)
            {
                var point = TrainingLabCourtProjectionV1.CourtToServeBoard(
                    _courtRect, points[index]);
                AddTrajectoryDot(_topTrajectory, _courtLeft + point.x,
                    _courtTop + point.y);
            }
        }

        private void RenderSideTrajectory(float width, float height)
        {
            var points = _controller.PredictTrajectory();
            for (var index = 0; index < points.Count; index += 6)
            {
                var point = points[index];
                AddTrajectoryDot(_sideTrajectory,
                    Mathf.Lerp(width * .05f, width * .95f,
                        Mathf.InverseLerp(-12f, 12f, point.Z)),
                    Mathf.Lerp(height * .82f, height * .08f,
                        Mathf.InverseLerp(0f, 5f, point.Y)));
            }
        }

        private static void AddTrajectoryDot(VisualElement layer, float x,
            float y)
        {
            var dot = new VisualElement();
            dot.AddToClassList("trajectory-dot");
            dot.style.left = x - 2.5f;
            dot.style.top = y - 2.5f;
            layer.Add(dot);
        }

        private void OnBoardPointerMove(PointerMoveEvent evt)
        {
            if (string.IsNullOrWhiteSpace(_dragObjectId) ||
                _controller.EditingLocked) return;
            var point = _court.WorldToLocal(evt.position);
            if (_dragObjectId == "ball")
            {
                if (_controller.CurrentStep != TrainingLabStepV1.ServeBall ||
                    _controller.ActiveServeView != TrainingServeViewV1.Top ||
                    _controller.ServeTool != TrainingServeToolV1.MoveBall) return;
                var next = TrainingLabCourtProjectionV1.ServeBoardToCourtPosition(
                    _courtRect, point, _controller.MatchSetup.BallPosition.Y,
                    _controller.MatchSetup.FirstServingSide);
                _controller.TrySetBallFromTop(next.X, next.Z);
            }
            else if (_dragObjectId == "velocity" &&
                     _controller.CurrentStep == TrainingLabStepV1.ServeBall &&
                     _controller.ActiveServeView == TrainingServeViewV1.Top &&
                     _controller.ServeTool ==
                     TrainingServeToolV1.AdjustVelocity)
            {
                var ball = TrainingLabCourtProjectionV1.CourtToServeBoard(
                    _courtRect, _controller.MatchSetup.BallPosition);
                var depthPixelsPerMeter = _courtRect.width /
                                          (CourtBuilder.FormalHalfLength * 2f);
                var lateralPixelsPerMeter = _courtRect.height /
                                            (CourtBuilder.HalfWidth * 2f);
                _controller.TrySetVelocityFromTop(
                    -(point.y - ball.y) / lateralPixelsPerMeter / .15f,
                    (point.x - ball.x) / depthPixelsPerMeter / .15f);
            }
            else if (_controller.CurrentStep == TrainingLabStepV1.Positioning)
                _controller.SetPlayerPositionFromCourt(new PlayerId(_dragObjectId),
                    _courtRect, point);
        }

        private void OnRotationPointerUp(PointerUpEvent evt)
        {
            if (!_rotationDragSide.HasValue) return;
            try
            {
                var picked = RotationCardAncestor(evt.target as VisualElement);
                if (picked == _rotationDragCard)
                    picked = RotationCardAncestor(_root.panel?.Pick(new Vector2(
                        evt.position.x, evt.position.y)));
                if (picked?.userData is RotationCardBinding target)
                    _controller.TryDropRotationCard(_rotationDragSide.Value,
                        _rotationDragSlot, target.Side, target.Slot);
            }
            finally
            {
                ClearRotationDrag();
            }
        }

        private VisualElement RotationCardAncestor(VisualElement element)
        {
            while (element != null && element != _root &&
                   element.userData is not RotationCardBinding)
                element = element.parent;
            return element?.userData is RotationCardBinding ? element : null;
        }

        private void ClearRotationDrag()
        {
            var card = _rotationDragCard;
            var pointer = _rotationDragPointer;
            _rotationDragSide = null;
            _rotationDragSlot = 0;
            _rotationDragCard = null;
            _rotationDragPointer = -1;
            if (card == null) return;
            card.RemoveFromClassList("rotation-card-dragging");
            if (pointer >= 0 && card.HasPointerCapture(pointer))
                card.ReleasePointer(pointer);
        }

        private void ApplyExactPosition()
        {
            if (_rendering || _controller.EditingLocked) return;
            var value = new SimVector3(_positionX.value, _positionY.value,
                _positionZ.value);
            if (_controller.SelectedObjectId != "ball" &&
                _controller.CurrentStep == TrainingLabStepV1.Positioning)
                _controller.SetPlayerPosition(new PlayerId(
                    _controller.SelectedObjectId), value);
        }

        private void ApplyExactServePosition()
        {
            if (_rendering || _controller.EditingLocked ||
                _controller.CurrentStep != TrainingLabStepV1.ServeBall ||
                _controller.ServeTool != TrainingServeToolV1.MoveBall) return;
            if (_controller.ActiveServeView == TrainingServeViewV1.Top)
                _controller.TrySetBallFromTop(_servePositionX.value,
                    _servePositionZ.value);
            else
                _controller.TrySetBallFromSide(_servePositionZ.value,
                    _servePositionY.value);
        }

        private void ApplyExactVelocity()
        {
            if (_rendering || _controller.EditingLocked ||
                _controller.CurrentStep != TrainingLabStepV1.ServeBall ||
                _controller.ServeTool != TrainingServeToolV1.AdjustVelocity) return;
            if (_controller.ActiveServeView == TrainingServeViewV1.Top)
                _controller.TrySetVelocityFromTop(_velocityX.value,
                    _velocityZ.value);
            else
                _controller.TrySetVelocityFromSide(_velocityZ.value,
                    _velocityY.value);
        }

        private void OnSidePointerMove(PointerMoveEvent evt)
        {
            if (string.IsNullOrWhiteSpace(_dragObjectId) ||
                _controller.EditingLocked ||
                _controller.CurrentStep != TrainingLabStepV1.ServeBall ||
                _controller.ActiveServeView != TrainingServeViewV1.Side)
                return;
            var point = _serveSide.WorldToLocal(evt.position);
            var width = _serveSide.contentRect.width;
            var height = _serveSide.contentRect.height;
            if (width < 10f || height < 10f) return;
            var z = Mathf.Lerp(-12f, 12f,
                Mathf.InverseLerp(width * .05f, width * .95f, point.x));
            var y = Mathf.Lerp(5f, 0f,
                Mathf.InverseLerp(height * .08f, height * .82f, point.y));
            if (_dragObjectId == "ball" && _controller.ServeTool ==
                TrainingServeToolV1.MoveBall)
                _controller.TrySetBallFromSide(z, y);
            else if (_dragObjectId == "velocity" && _controller.ServeTool ==
                     TrainingServeToolV1.AdjustVelocity)
            {
                var ball = _controller.MatchSetup.BallPosition;
                var ballX = Mathf.Lerp(width * .05f, width * .95f,
                    Mathf.InverseLerp(-12f, 12f, ball.Z));
                var ballY = Mathf.Lerp(height * .82f, height * .08f,
                    Mathf.InverseLerp(0f, 5f, ball.Y));
                _controller.TrySetVelocityFromSide(
                    (point.x - ballX) / 6f,
                    (ballY - point.y) / 6f);
            }
        }

        private void StartRun()
        {
            if (_controller.PreflightSnapshot == null &&
                !_controller.EnterPreflight()) return;
            _controller.Run();
        }

        private void TogglePause()
        {
            if (_controller.State == TrainingScenarioLabStateV1.Running)
                _controller.Pause();
            else if (_controller.State == TrainingScenarioLabStateV1.Paused)
                _controller.Resume();
        }

        public void OpenReadonly3dPreview()
        {
            if (_controller.CurrentStep != TrainingLabStepV1.ServeBall)
                return;
            EnsureReadonly3dPreview();
            _previewCamera.enabled = SystemInfo.graphicsDeviceType !=
                UnityEngine.Rendering.GraphicsDeviceType.Null;
            UpdateReadonly3dPreview();
            _preview3d.style.display = DisplayStyle.Flex;
        }

        public void CloseReadonly3dPreview()
        {
            _preview3d.style.display = DisplayStyle.None;
            if (_previewCamera != null) _previewCamera.enabled = false;
        }

        private void EnsureReadonly3dPreview()
        {
            if (_previewRoot != null) return;
            _previewRoot = new GameObject("TrainingLabReadonly3DPreviewV5");
            _previewRoot.transform.SetParent(transform, false);
            _previewRoot.transform.position = new Vector3(1000f, 0f, 1000f);
            CourtBuilder.Build(_previewRoot.transform,
                CourtBuilder.FormalHalfLength);
            _previewCamera = _previewRoot.GetComponentInChildren<Camera>();
            _previewCamera.tag = "Untagged";
            _previewCamera.orthographic = false;
            _previewCamera.fieldOfView = 48f;
            _previewCamera.nearClipPlane = .1f;
            _previewCamera.farClipPlane = 80f;
            _previewTexture = new RenderTexture(1024, 576, 24,
                RenderTextureFormat.ARGB32)
            {
                name = "TrainingLabReadonly3DPreviewTextureV5",
                antiAliasing = 2
            };
            _previewTexture.Create();
            _previewCamera.targetTexture = _previewTexture;
            _preview3dViewport.style.backgroundImage =
                new StyleBackground(Background.FromRenderTexture(
                    _previewTexture));
            CreatePreviewMarker("ball", PrimitiveType.Sphere, Color.yellow,
                Vector3.one * .22f);
            var home = new HashSet<PlayerId>(
                _controller.MatchSetup.HomeRotation);
            _previewHomePlayers.Clear();
            _previewHomePlayers.UnionWith(home);
            foreach (var pose in _controller.MatchSetup.Players)
                CreatePreviewMarker(pose.PlayerId.Value,
                    PrimitiveType.Capsule,
                    home.Contains(pose.PlayerId)
                        ? new Color(.12f, .45f, .95f)
                        : new Color(.95f, .32f, .18f),
                    new Vector3(.42f, .72f, .42f));
            ResetReadonly3dCamera();
        }

        private void CreatePreviewMarker(string id, PrimitiveType primitive,
            Color color, Vector3 scale)
        {
            var marker = GameObject.CreatePrimitive(primitive);
            marker.name = id == "ball" ? "PreviewBallV5" :
                "PreviewPlayerV5-" + id;
            marker.transform.SetParent(_previewRoot.transform, false);
            marker.transform.localScale = scale;
            var collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            marker.GetComponent<Renderer>().SetPropertyBlock(properties);
            _previewMarkers.Add(id, marker.transform);
        }

        private void UpdateReadonly3dPreview()
        {
            if (_previewRoot == null) return;
            var ball = _controller.MatchSetup.BallPosition;
            if (_previewMarkers.TryGetValue("ball", out var ballMarker))
                ballMarker.localPosition = ToUnity(ball);
            foreach (var pose in _controller.MatchSetup.Players)
            {
                if (!_previewMarkers.TryGetValue(pose.PlayerId.Value,
                        out var marker)) continue;
                marker.localPosition = ToUnity(pose.Position) +
                                       Vector3.up * .72f;
                marker.forward = _previewHomePlayers.Contains(pose.PlayerId)
                    ? Vector3.forward
                    : Vector3.back;
            }
        }

        private void ResetReadonly3dCamera()
        {
            if (_previewCamera == null) return;
            _previewYaw = 32f;
            _previewPitch = 28f;
            _previewDistance = 22f;
            UpdatePreviewCameraTransform();
        }

        private void UpdatePreviewCameraTransform()
        {
            if (_previewCamera == null) return;
            var rotation = Quaternion.Euler(_previewPitch, _previewYaw, 0f);
            _previewCamera.transform.localPosition =
                rotation * (Vector3.back * _previewDistance) +
                Vector3.up * .75f;
            _previewCamera.transform.LookAt(
                _previewRoot.transform.TransformPoint(
                    new Vector3(0f, .75f, 0f)));
        }

        private void OnPreviewPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            _previewPointer = evt.pointerId;
            _previewPointerPosition = new Vector2(evt.position.x, evt.position.y);
            _preview3dViewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPreviewPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _previewPointer) return;
            var current = new Vector2(evt.position.x, evt.position.y);
            var delta = current - _previewPointerPosition;
            _previewPointerPosition = current;
            _previewYaw += delta.x * .25f;
            _previewPitch = Mathf.Clamp(_previewPitch - delta.y * .25f,
                8f, 78f);
            UpdatePreviewCameraTransform();
            evt.StopPropagation();
        }

        private void OnPreviewPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _previewPointer) return;
            if (_preview3dViewport.HasPointerCapture(evt.pointerId))
                _preview3dViewport.ReleasePointer(evt.pointerId);
            _previewPointer = -1;
            evt.StopPropagation();
        }

        private void OnPreviewWheel(WheelEvent evt)
        {
            _previewDistance = Mathf.Clamp(
                _previewDistance + evt.delta.y * .02f, 8f, 36f);
            UpdatePreviewCameraTransform();
            evt.StopPropagation();
        }

        private void SavePreviewBookmark()
        {
            var name = _bookmarkName.value?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            _bookmarks[name] = new PreviewCameraBookmark(_previewYaw,
                _previewPitch, _previewDistance);
            PersistPreviewBookmarks();
            RenderPreviewBookmarks();
            Render();
        }

        private void RenderPreviewBookmarks()
        {
            _bookmarkList.Clear();
            foreach (var pair in _bookmarks.OrderBy(value => value.Key,
                         StringComparer.Ordinal))
            {
                var bookmark = pair.Value;
                var button = new Button(() =>
                {
                    _previewYaw = bookmark.Yaw;
                    _previewPitch = bookmark.Pitch;
                    _previewDistance = bookmark.Distance;
                    UpdatePreviewCameraTransform();
                }) { text = pair.Key };
                _bookmarkList.Add(button);
            }
        }

        private void DestroyReadonly3dPreview()
        {
            if (_previewCamera != null) _previewCamera.targetTexture = null;
            if (_preview3dViewport != null)
                _preview3dViewport.style.backgroundImage = StyleKeyword.None;
            if (_previewTexture != null)
            {
                _previewTexture.Release();
                Destroy(_previewTexture);
            }
            if (_previewRoot != null) Destroy(_previewRoot);
            _previewMarkers.Clear();
            _previewHomePlayers.Clear();
            _previewTexture = null;
            _previewCamera = null;
            _previewRoot = null;
        }

        private void PersistPreviewBookmarks()
        {
            var file = new PreviewBookmarkFile();
            foreach (var pair in _bookmarks.OrderBy(value => value.Key,
                         StringComparer.Ordinal))
                file.items.Add(new PreviewBookmarkData
                {
                    name = pair.Key,
                    yaw = pair.Value.Yaw,
                    pitch = pair.Value.Pitch,
                    distance = pair.Value.Distance
                });
            _controller.LocalScenario.BookmarksJson = JsonUtility.ToJson(file);
        }

        private void RestorePreviewBookmarks()
        {
            _bookmarks.Clear();
            var json = _controller?.LocalScenario?.BookmarksJson;
            if (!string.IsNullOrWhiteSpace(json) && json.StartsWith("{",
                    StringComparison.Ordinal))
            {
                try
                {
                    var file = JsonUtility.FromJson<PreviewBookmarkFile>(json);
                    if (file?.items != null)
                        foreach (var item in file.items)
                            if (item != null &&
                                !string.IsNullOrWhiteSpace(item.name))
                                _bookmarks[item.name] = new PreviewCameraBookmark(
                                    item.yaw, item.pitch, item.distance);
                }
                catch (ArgumentException)
                {
                    // The repository preserves opaque bookmark bytes. A malformed
                    // optional UI payload must not make the scenario unavailable.
                }
            }
            if (_bookmarkList != null) RenderPreviewBookmarks();
        }

        private static Vector3 ToUnity(SimVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);

        private PlayerSnapshotV5 Player(PlayerId id) =>
            _controller.MatchSetup.BaseContext.Home.RotationOrder
                .Concat(_controller.MatchSetup.BaseContext.Away.RotationOrder)
                .Single(value => value.PlayerId.Equals(id));

        private bool TrySelectedPlayer(out PlayerId id)
        {
            id = default;
            if (_controller.SelectedObjectId == "ball" ||
                string.IsNullOrWhiteSpace(_controller.SelectedObjectId)) return false;
            id = new PlayerId(_controller.SelectedObjectId);
            var selected = id;
            return _controller.MatchSetup.Players.Any(p =>
                p.PlayerId.Equals(selected));
        }

        private static string PositionText(PlayerPosition position) =>
            position switch
            {
                PlayerPosition.Setter => "二传",
                PlayerPosition.OutsideHitter => "主攻",
                PlayerPosition.MiddleBlocker => "副攻",
                PlayerPosition.Opposite => "接应",
                PlayerPosition.Libero => "自由人",
                PlayerPosition.Defender => "防守",
                _ => position.ToString()
            };

        private SimVector3 SelectedPosition()
        {
            if (_controller.SelectedObjectId == "ball")
                return _controller.MatchSetup.BallPosition;
            return _controller.MatchSetup.Players.FirstOrDefault(p =>
                       p.PlayerId.Value == _controller.SelectedObjectId)
                   ?.Position ?? SimVector3.Zero;
        }

        private void SetVisible(string name, bool visible) =>
            _root.Q(name).style.display = visible
                ? DisplayStyle.Flex : DisplayStyle.None;

        private sealed class RotationCardBinding
        {
            public RotationCardBinding(TeamSide side, int slot)
            {
                Side = side;
                Slot = slot;
            }

            public TeamSide Side { get; }
            public int Slot { get; }
        }

        private static string StateText(TrainingScenarioLabStateV1 state) =>
            state switch
            {
                TrainingScenarioLabStateV1.Editing => "编辑中",
                TrainingScenarioLabStateV1.Ready => "已就绪",
                TrainingScenarioLabStateV1.Running => "正式运行中",
                TrainingScenarioLabStateV1.Paused => "已暂停",
                TrainingScenarioLabStateV1.Completed => "本回合已完成",
                TrainingScenarioLabStateV1.Faulted => "运行异常",
                _ => state.ToString()
            };

        private static string StepText(TrainingLabStepV1 step) => step switch
        {
            TrainingLabStepV1.Rotation => "轮转",
            TrainingLabStepV1.Positioning => "摆位",
            TrainingLabStepV1.ServeBall => "发球球",
            TrainingLabStepV1.Validation => "自动预检",
            TrainingLabStepV1.Running => "运行结果",
            _ => step.ToString()
        };

        private readonly struct PreviewCameraBookmark
        {
            public PreviewCameraBookmark(float yaw, float pitch,
                float distance)
            {
                Yaw = yaw;
                Pitch = pitch;
                Distance = distance;
            }

            public float Yaw { get; }
            public float Pitch { get; }
            public float Distance { get; }
        }

        [Serializable]
        private sealed class PreviewBookmarkFile
        {
            public List<PreviewBookmarkData> items =
                new List<PreviewBookmarkData>();
        }

        [Serializable]
        private sealed class PreviewBookmarkData
        {
            public string name;
            public float yaw;
            public float pitch;
            public float distance;
        }
    }
}
