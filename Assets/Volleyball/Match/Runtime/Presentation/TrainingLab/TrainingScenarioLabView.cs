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
        private VisualElement _scenarioHub;
        private VisualElement _continueScenarios;
        private VisualElement _standardScenarios;
        private VisualElement _workbenchShell;
        private VisualElement _issues;
        private VisualElement _timeline;
        private VisualElement _viewport;
        private VisualElement _tacticalBoard;
        private VisualElement _courtSurface;
        private VisualElement _tokenLayer;
        private VisualElement _faultLayer;
        private VisualElement _horizontalRuler;
        private VisualElement _verticalRuler;
        private VisualElement _contextualInspector;
        private VisualElement _rotationContext;
        private VisualElement _positioningContext;
        private VisualElement _serveContext;
        private VisualElement _validationContext;
        private VisualElement _runningContext;
        private VisualElement _positionInputs;
        private VisualElement _rotationIssues;
        private VisualElement _faultSummary;
        private VisualElement _rotationBoard;
        private VisualElement _homeRotationGrid;
        private VisualElement _awayRotationGrid;
        private VisualElement _attributeTable;
        private VisualElement _bookmarkList;
        private Label _state;
        private Label _hash;
        private Label _rules;
        private Label _selection;
        private Label _comparison;
        private Label _feedback;
        private Label _monitor;
        private Label _viewportHint;
        private Label _inspectorTitle;
        private Label _inspectorSummary;
        private Label _serveSetupBlock;
        private Label _serveToolSummary;
        private Label _validationSummary;
        private Label _runningSummary;
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
        private Button _save;
        private Button _returnToHub;
        private Button _run;
        private Button _pause;
        private Button _step;
        private Button _rerun;
        private Button _returnToEdit;
        private Button _export;
        private Button _reviewSetter;
        private TextField _bookmarkName;
        private bool _rendering;
        private bool _showingScenarioHub = true;
        private bool _ownsController;
        private bool _initialized;
        private int _dragPointer = -1;
        private string _dragObjectId;
        private TeamSide? _rotationDragSide;
        private int _rotationDragSlot;
        private Rect _courtBoardRect;
        private Font _runtimeFont;

        public TrainingScenarioLabController Controller => _controller;

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
        }

        private void EnsureDocument()
        {
            if (_initialized) return;
            _document = _document != null
                ? _document
                : GetComponent<UIDocument>();
            _root = _document.rootVisualElement;
            _scenarioHub = _root.Q<VisualElement>("scenario-hub");
            _continueScenarios = _root.Q<VisualElement>("continue-scenarios");
            _standardScenarios = _root.Q<VisualElement>("standard-scenarios");
            _workbenchShell = _root.Q<VisualElement>("workbench-shell");
            _issues = _root.Q<VisualElement>("issue-list");
            _timeline = _root.Q<VisualElement>("timeline-list");
            _viewport = _root.Q<VisualElement>("world-viewport");
            _tacticalBoard = _root.Q<VisualElement>("tactical-board");
            _courtSurface = _root.Q<VisualElement>("court-surface");
            _tokenLayer = _root.Q<VisualElement>("tactical-token-layer");
            _faultLayer = _root.Q<VisualElement>("position-fault-layer");
            _horizontalRuler = _root.Q<VisualElement>("horizontal-ruler");
            _verticalRuler = _root.Q<VisualElement>("vertical-ruler");
            _contextualInspector = _root.Q<VisualElement>("contextual-inspector");
            _rotationContext = _root.Q<VisualElement>("context-rotation");
            _positioningContext = _root.Q<VisualElement>("context-positioning");
            _serveContext = _root.Q<VisualElement>("context-serve-ball");
            _validationContext = _root.Q<VisualElement>("context-validation");
            _runningContext = _root.Q<VisualElement>("context-running");
            _rotationIssues = _root.Q<VisualElement>("rotation-issues");
            _faultSummary = _root.Q<VisualElement>("position-fault-summary");
            _rotationBoard = _root.Q<VisualElement>("rotation-board");
            _homeRotationGrid = _root.Q<VisualElement>("rotation-home-grid");
            _awayRotationGrid = _root.Q<VisualElement>("rotation-away-grid");
            _attributeTable = _root.Q<VisualElement>("attribute-table");
            _bookmarkList = _root.Q<VisualElement>("bookmark-list");
            _state = _root.Q<Label>("state-label");
            _hash = _root.Q<Label>("hash-label");
            _rules = _root.Q<Label>("rules-label");
            _selection = _root.Q<Label>("selection-label");
            _comparison = _root.Q<Label>("comparison-label");
            _feedback = _root.Q<Label>("feedback-label");
            _monitor = _root.Q<Label>("monitor-label");
            _viewportHint = _root.Q<Label>("viewport-hint");
            _inspectorTitle = _root.Q<Label>("inspector-title");
            _inspectorSummary = _root.Q<Label>("inspector-summary");
            _serveSetupBlock = _root.Q<Label>("serve-setup-block");
            _serveToolSummary = _root.Q<Label>("serve-tool-summary");
            _validationSummary = _root.Q<Label>("validation-summary");
            _runningSummary = _root.Q<Label>("running-summary");
            _displayName = _root.Q<TextField>("display-name");
            _seed = _root.Q<IntegerField>("match-seed");
            _recipe = _root.Q<DropdownField>("start-recipe");
            _sourceTeam = _root.Q<DropdownField>("source-team");
            _lastActor = _root.Q<DropdownField>("last-actor");
            _homeSet = _root.Q<DropdownField>("home-set-route");
            _homeSpike = _root.Q<DropdownField>("home-spike-route");
            _awaySet = _root.Q<DropdownField>("away-set-route");
            _awaySpike = _root.Q<DropdownField>("away-spike-route");
            CreatePositionInputs();
            _velocityX = _root.Q<FloatField>("velocity-x");
            _velocityY = _root.Q<FloatField>("velocity-y");
            _velocityZ = _root.Q<FloatField>("velocity-z");
            _save = _root.Q<Button>("save-button");
            _returnToHub = _root.Q<Button>("return-to-hub-button");
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

        private void CreatePositionInputs()
        {
            var host = _root.Q<VisualElement>("position-input-host-positioning");
            _positionInputs = new VisualElement { name = "position-inputs" };
            var label = new Label("位置 XYZ");
            label.AddToClassList("mini-label");
            _positionInputs.Add(label);
            var row = new VisualElement();
            row.AddToClassList("vector-row");
            _positionX = new FloatField { name = "position-x" };
            _positionY = new FloatField { name = "position-y" };
            _positionZ = new FloatField { name = "position-z" };
            _positionX.AddToClassList("vector-field");
            _positionY.AddToClassList("vector-field");
            _positionZ.AddToClassList("vector-field");
            row.Add(_positionX);
            row.Add(_positionY);
            row.Add(_positionZ);
            _positionInputs.Add(row);
            host.Add(_positionInputs);
        }

        private void RegisterUiEvents()
        {
            _root.Q<Button>("hub-new-from-standard-button").clicked += () =>
            {
                _controller.NewDraft();
                ShowWorkbench(_controller.SelectedEntryKey);
            };
            _save.clicked += SaveLocalScenario;
            _returnToHub.clicked += ShowScenarioHub;
            _root.Q<Button>("more-button").clicked += () =>
            {
                var advanced = _root.Q<Foldout>("advanced-settings");
                advanced.value = !advanced.value;
            };
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
            _root.Q<Button>("step-serve").clicked += () => TrySelectServeTool(TrainingServeToolV1.MoveBall);
            _root.Q<Button>("step-validation").clicked += () => _controller.GoToValidation();
            _root.Q<Button>("positioning-next-button").clicked += () =>
                TrySelectServeTool(TrainingServeToolV1.MoveBall);
            _root.Q<Button>("serve-next-button").clicked += () => _controller.GoToValidation();
            _root.Q<Button>("context-run-button").clicked += () => _controller.Run();
            _root.Q<Button>("tool-move-ball").clicked += () => TrySelectServeTool(TrainingServeToolV1.MoveBall);
            _root.Q<Button>("tool-velocity").clicked += () => TrySelectServeTool(TrainingServeToolV1.AdjustVelocity);
            _root.Q<Button>("tool-trajectory").clicked += () => TrySelectServeTool(TrainingServeToolV1.ViewTrajectory);
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

            _tacticalBoard.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _tacticalBoard.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _tacticalBoard.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                LayoutTacticalBoard();
                RenderTacticalBoard();
            });
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void Render()
        {
            if (_controller == null || !_initialized) return;
            _rendering = true;
            try
            {
                RenderScenarioHub();
                RenderScreenMode();
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
                RenderContextualInspector();
                RenderTimeline();
                RenderControls();
                LayoutTacticalBoard();
                RenderTacticalBoard();
                SyncWorld();
            }
            finally
            {
                _rendering = false;
            }
        }

        public void ShowWorkbench(string entryKey)
        {
            if (!string.IsNullOrWhiteSpace(entryKey) &&
                !string.Equals(entryKey, _controller.SelectedEntryKey,
                    StringComparison.Ordinal))
                _controller.SelectDraftEntry(entryKey);
            _showingScenarioHub = false;
            RenderScreenMode();
        }

        public void ShowScenarioHub()
        {
            _showingScenarioHub = true;
            RenderScenarioHub();
            RenderScreenMode();
        }

        private void SaveLocalScenario()
        {
            if (_controller.LocalScenario == null) return;
            _controller.SaveCurrentLocalScenario();
        }

        private void RenderScenarioHub()
        {
            if (_continueScenarios == null || _standardScenarios == null)
                return;
            _continueScenarios.Clear();
            _standardScenarios.Clear();
            foreach (var entry in _controller.Entries)
            {
                var captured = entry.Key;
                var card = new VisualElement();
                card.AddToClassList("scenario-card");
                var title = new Label(entry.DisplayName);
                title.AddToClassList("scenario-card-title");
                card.Add(title);
                var detail = new Label(entry.IsBuiltIn
                    ? "只读标准模板 · 打开后创建本地工作副本"
                    : "本地工作副本");
                detail.AddToClassList("scenario-card-detail");
                card.Add(detail);
                var button = new Button(() => ShowWorkbench(captured))
                {
                    text = entry.IsBuiltIn ? "打开" : "继续编辑"
                };
                button.SetEnabled(!_controller.EditingLocked);
                card.Add(button);
                (entry.IsBuiltIn ? _standardScenarios : _continueScenarios)
                    .Add(card);
            }
            if (_continueScenarios.childCount == 0)
            {
                var empty = new Label("尚无已保存的本地情景。打开标准情景即可开始。");
                empty.AddToClassList("hub-empty");
                _continueScenarios.Add(empty);
            }
        }

        private void RenderScreenMode()
        {
            if (_scenarioHub == null || _workbenchShell == null) return;
            _scenarioHub.style.display = _showingScenarioHub
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _workbenchShell.style.display = _showingScenarioHub
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void LayoutTacticalBoard()
        {
            if (_tacticalBoard == null || _courtSurface == null)
                return;
            var width = _tacticalBoard.contentRect.width;
            var height = _tacticalBoard.contentRect.height;
            if (width < 10f || height < 10f)
                return;

            var maxWidth = Mathf.Max(80f, width - 154f);
            var maxHeight = Mathf.Max(80f, height - 72f);
            var courtWidth = Mathf.Min(maxWidth, maxHeight * 2f);
            var courtHeight = courtWidth * .5f;
            var left = Mathf.Max(76f, (width - courtWidth) * .5f);
            var top = Mathf.Max(28f, (height - courtHeight - 30f) * .5f);

            _courtSurface.style.left = left;
            _courtSurface.style.top = top;
            _courtSurface.style.width = courtWidth;
            _courtSurface.style.height = courtHeight;
            _horizontalRuler.style.left = left;
            _horizontalRuler.style.top = top + courtHeight + 8f;
            _horizontalRuler.style.width = courtWidth;
            _verticalRuler.style.left = left - 31f;
            _verticalRuler.style.top = top;
            _verticalRuler.style.height = courtHeight;
            _courtBoardRect = new Rect(0f, 0f, courtWidth, courtHeight);
        }

        private void RenderTacticalBoard()
        {
            if (_courtSurface == null || _tokenLayer == null ||
                _courtBoardRect.width < 10f)
                return;

            _tokenLayer.Clear();
            _faultLayer.Clear();
            RenderRuler(_horizontalRuler, true);
            RenderRuler(_verticalRuler, false);
            RenderSelectedPlayerRulerPoints();

            var homeIds = new HashSet<string>(
                _controller.Draft.Context.Home.Players.Select(value => value.PlayerId.Value),
                StringComparer.Ordinal);
            var slots = BuildRotationSlots();
            foreach (var pose in _controller.Draft.Players)
            {
                if (pose == null || !pose.Position.IsFinite)
                    continue;
                var player = FindPlayer(pose.PlayerId.Value);
                var token = new VisualElement { userData = pose.PlayerId.Value };
                token.AddToClassList("tactical-token");
                token.AddToClassList(homeIds.Contains(pose.PlayerId.Value)
                    ? "home-token" : "away-token");
                if (_controller.SelectedObjectId == pose.PlayerId.Value)
                    token.AddToClassList("selected-token");
                if (_controller.PositionFaultPreview.Any(fault =>
                        fault.RequiredAheadOrLeft.PlayerId.Value == pose.PlayerId.Value ||
                        fault.ViolatingBehindOrRight.PlayerId.Value == pose.PlayerId.Value))
                    token.AddToClassList("fault-token");
                var boardPoint = TrainingLabCourtProjectionV1.CourtToBoard(
                    _courtBoardRect, pose.Position);
                token.style.left = boardPoint.x;
                token.style.top = boardPoint.y;
                var slotText = slots.TryGetValue(pose.PlayerId.Value, out var slot)
                    ? slot + "号位"
                    : "未锁定位次";
                token.Add(new Label(slotText));
                var label = new Label(player == null
                    ? pose.PlayerId.Value
                    : player.DisplayName + " · " + PositionName(player.Position));
                label.AddToClassList("token-label");
                if (!homeIds.Contains(pose.PlayerId.Value))
                    label.AddToClassList("away-label");
                token.Add(label);
                token.RegisterCallback<PointerDownEvent>(OnTokenPointerDown);
                _tokenLayer.Add(token);
            }

            if (_controller.Draft.BallPosition.IsFinite)
            {
                var ball = new VisualElement { userData = "ball" };
                ball.AddToClassList("tactical-token");
                ball.AddToClassList("ball-token");
                if (_controller.SelectedObjectId == "ball")
                    ball.AddToClassList("selected-token");
                var point = TrainingLabCourtProjectionV1.CourtToServeBoard(
                    _courtBoardRect, _controller.Draft.BallPosition);
                ball.style.left = point.x;
                ball.style.top = point.y;
                ball.Add(new Label("球"));
                ball.RegisterCallback<PointerDownEvent>(OnTokenPointerDown);
                _tokenLayer.Add(ball);
            }

            foreach (var fault in _controller.PositionFaultPreview)
            {
                var required = TrainingLabCourtProjectionV1.CourtToBoard(
                    _courtBoardRect, fault.RequiredAheadOrLeft.FootProjection);
                var violating = TrainingLabCourtProjectionV1.CourtToBoard(
                    _courtBoardRect, fault.ViolatingBehindOrRight.FootProjection);
                AddBoardLine(_faultLayer, required, violating, "fault-relation");
                var correction = TrainingLabCourtProjectionV1.CourtToBoard(
                    _courtBoardRect,
                    TrainingLabCourtProjectionV1.ShortestLegalCorrection(fault));
                AddBoardLine(_faultLayer, violating, correction, "fault-arrow");
                var arrow = new Label("→") { pickingMode = PickingMode.Ignore };
                arrow.AddToClassList("fault-arrow-head");
                arrow.style.left = correction.x - 5f;
                arrow.style.top = correction.y - 9f;
                _faultLayer.Add(arrow);
            }
        }

        private void RenderSelectedPlayerRulerPoints()
        {
            if (_controller.CurrentStep != TrainingLabStepV1.Positioning)
                return;
            var selected = _controller.Draft.Players.FirstOrDefault(value =>
                value != null && value.PlayerId.Value ==
                _controller.SelectedObjectId);
            if (selected == null) return;
            var point = TrainingLabCourtProjectionV1.CourtToBoard(
                _courtBoardRect, selected.Position);
            var depth = new VisualElement
            {
                name = "selected-player-depth-ruler-point",
                userData = selected.PlayerId.Value + "|depth"
            };
            depth.AddToClassList("selected-ruler-point");
            depth.style.left = point.x;
            depth.style.top = 7f;
            _horizontalRuler.Add(depth);
            var lateral = new VisualElement
            {
                name = "selected-player-lateral-ruler-point",
                userData = selected.PlayerId.Value + "|lateral"
            };
            lateral.AddToClassList("selected-ruler-point");
            lateral.style.left = 15f;
            lateral.style.top = point.y;
            _verticalRuler.Add(lateral);
        }

        private void RenderRuler(VisualElement ruler, bool horizontal)
        {
            if (ruler == null) return;
            ruler.Clear();
            var track = new VisualElement();
            track.AddToClassList("ruler-track");
            ruler.Add(track);
            var count = horizontal ? 19 : 10;
            for (var index = 0; index < count; index++)
            {
                var fraction = count == 1 ? 0f : index / (float)(count - 1);
                var tick = new VisualElement();
                tick.AddToClassList("ruler-tick");
                if (horizontal) tick.style.left = fraction * _courtBoardRect.width;
                else tick.style.top = (1f - fraction) * _courtBoardRect.height;
                ruler.Add(tick);
                var label = new Label(horizontal
                    ? (index <= 9 ? (9 - index).ToString() : (index - 9).ToString())
                    : index.ToString());
                label.AddToClassList("ruler-label");
                if (horizontal)
                {
                    label.style.left = fraction * _courtBoardRect.width - 8f;
                    if (index == 9) label.text = "0 网";
                }
                else
                {
                    label.style.top = (1f - fraction) * _courtBoardRect.height - 5f;
                }
                ruler.Add(label);
            }
        }

        private static void AddBoardLine(VisualElement parent, Vector2 from,
            Vector2 to, string className)
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList(className);
            var delta = to - from;
            var length = Mathf.Max(2f, delta.magnitude);
            line.style.width = length;
            line.style.height = 2f;
            line.style.left = (from.x + to.x) * .5f - length * .5f;
            line.style.top = (from.y + to.y) * .5f - 1f;
            line.style.rotate = new Rotate(new Angle(
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg,
                AngleUnit.Degree));
            parent.Add(line);
        }

        private Dictionary<string, int> BuildRotationSlots()
        {
            var slots = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < _controller.Draft.HomeRotation.Count; index++)
                slots[_controller.Draft.HomeRotation[index].Value] = index + 1;
            for (var index = 0; index < _controller.Draft.AwayRotation.Count; index++)
                slots[_controller.Draft.AwayRotation[index].Value] = index + 1;
            return slots;
        }

        private PlayerSnapshotV4 FindPlayer(string id)
        {
            return _controller.Draft.Context.Home.Players
                .Concat(_controller.Draft.Context.Away.Players)
                .FirstOrDefault(value => value.PlayerId.Value == id);
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
            _homeRotationGrid.Clear();
            _awayRotationGrid.Clear();
            foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
            {
                var rotation = side == TeamSide.Home
                    ? _controller.Draft.HomeRotation
                    : _controller.Draft.AwayRotation;
                var teamPlayers = (side == TeamSide.Home
                        ? _controller.Draft.Context.Home.Players
                        : _controller.Draft.Context.Away.Players)
                    .ToArray();
                var grid = side == TeamSide.Home
                    ? _homeRotationGrid
                    : _awayRotationGrid;
                for (var index = 0; index < rotation.Count; index++)
                {
                    var slot = index + 1;
                    var selected = teamPlayers.Single(value =>
                        value.PlayerId.Equals(rotation[index]));
                    grid.Add(CreateRotationCard(selected, side, slot));
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

        private VisualElement CreateRotationCard(
            PlayerSnapshotV4 player,
            TeamSide side,
            int slot)
        {
            var card = new VisualElement
            {
                name = "rotation-card-" + side + "-" + slot,
                userData = player.PlayerId.Value
            };
            card.AddToClassList("rotation-card");
            var slotLabel = new Label(slot + " 号位");
            slotLabel.AddToClassList("rotation-card-slot");
            card.Add(slotLabel);
            var name = new Label(player.DisplayName);
            name.AddToClassList("rotation-card-name");
            card.Add(name);
            var role = new Label(PositionName(player.Position));
            role.AddToClassList("rotation-card-role");
            card.Add(role);
            card.SetEnabled(!_controller.EditingLocked &&
                !_controller.Draft.RotationLocked);
            card.RegisterCallback<PointerDownEvent>(_ =>
            {
                _rotationDragSide = side;
                _rotationDragSlot = slot;
            });
            card.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (!_rotationDragSide.HasValue ||
                    _rotationDragSide.Value != side)
                {
                    _rotationDragSide = null;
                    return;
                }
                var rotation = (side == TeamSide.Home
                        ? _controller.Draft.HomeRotation
                        : _controller.Draft.AwayRotation)
                    .ToArray();
                (rotation[_rotationDragSlot - 1], rotation[slot - 1]) =
                    (rotation[slot - 1], rotation[_rotationDragSlot - 1]);
                _rotationDragSide = null;
                _controller.SetRotation(side, rotation);
            });
            return card;
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

        private void RenderContextualInspector()
        {
            var step = _controller.CurrentStep;
            _rotationBoard.style.display = step == TrainingLabStepV1.Rotation
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _tacticalBoard.style.display = step == TrainingLabStepV1.Rotation
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            SetContextVisible(_rotationContext, step == TrainingLabStepV1.Rotation);
            SetContextVisible(_positioningContext,
                step == TrainingLabStepV1.Positioning);
            SetContextVisible(_serveContext, step == TrainingLabStepV1.ServeBall);
            SetContextVisible(_validationContext,
                step == TrainingLabStepV1.Validation);
            SetContextVisible(_runningContext, step == TrainingLabStepV1.Running);

            var positionHost = _root.Q<VisualElement>(
                step == TrainingLabStepV1.ServeBall
                    ? "position-input-host-serve"
                    : "position-input-host-positioning");
            if (positionHost != null && _positionInputs.parent != positionHost)
                positionHost.Add(_positionInputs);

            _inspectorTitle.text = InspectorTitle(step);
            _inspectorSummary.text = InspectorSummary(step);
            _serveSetupBlock.text = _controller.CanEnterServeSetup
                ? "站位合法。下一步：设置发球球。"
                : _controller.ServeSetupBlockReason;
            _serveSetupBlock.EnableInClassList("has-block",
                !_controller.CanEnterServeSetup);
            _serveToolSummary.text = ServeToolSummary(_controller.ServeTool);
            _validationSummary.text = _controller.Validation.IsValid
                ? "当前草稿可冻结；运行将再次执行权威站位判定。"
                : "修复下列问题后才能冻结并运行。";
            _runningSummary.text = "发球方 " +
                _controller.Draft.FirstServingSide + " · 触球 " +
                (_controller.VisibleEvidence?.Timeline.Count(value =>
                    value.Kind == TrainingTimelineEventKindV1.ContactAccepted) ?? 0) +
                " · " + (_controller.PositionFaultPreview.Count == 0
                    ? "裁判：站位合法"
                    : "裁判：位置错误 " +
                      _controller.PositionFaultPreview.Count);
            RenderRotationIssues();
            RenderFaultSummary();
        }

        private static void SetContextVisible(VisualElement context, bool visible)
        {
            if (context == null) return;
            context.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            context.EnableInClassList("active-context", visible);
        }

        private void RenderRotationIssues()
        {
            _rotationIssues.Clear();
            var issues = _controller.Validation.Issues.Where(issue =>
                issue.Code == TrainingScenarioIssueCodesV1.InvalidRotationMembership)
                .ToArray();
            if (issues.Length == 0)
            {
                _rotationIssues.Add(new Label(_controller.Draft.RotationLocked
                    ? "轮转已锁定；重新编辑会使当前摆位重新进入编辑流程。"
                    : "请确认双方各有六名且不重复的轮转位次。"));
                return;
            }

            foreach (var issue in issues)
            {
                var label = new Label(issue.Message);
                label.AddToClassList("fault-card");
                _rotationIssues.Add(label);
            }
        }

        private void RenderFaultSummary()
        {
            _faultSummary.Clear();
            var faults = _controller.PositionFaultPreview;
            if (faults.Count == 0)
            {
                _faultSummary.Add(new Label("即时裁判：当前站位合法。"));
                return;
            }

            foreach (var fault in faults)
            {
                var required = FindPlayer(fault.RequiredAheadOrLeft.PlayerId.Value);
                var violating = FindPlayer(fault.ViolatingBehindOrRight.PlayerId.Value);
                var text = fault.Side + " 的 " +
                    fault.RequiredAheadOrLeft.Slot + " 号位 " +
                    (required?.DisplayName ?? fault.RequiredAheadOrLeft.PlayerId.Value) +
                    " 与 " + fault.ViolatingBehindOrRight.Slot + " 号位 " +
                    (violating?.DisplayName ?? fault.ViolatingBehindOrRight.PlayerId.Value) +
                    " 违反 " + fault.Rule + "；将后者拖向蓝色箭头即可修正。";
                var focusedPlayerId =
                    fault.ViolatingBehindOrRight.PlayerId.Value;
                var card = new Button(() => _controller.SelectObject(
                    focusedPlayerId, "position"))
                {
                    text = text,
                    name = "position-fault-focus-" + focusedPlayerId
                };
                card.AddToClassList("fault-card");
                _faultSummary.Add(card);
            }
        }

        private static string InspectorTitle(TrainingLabStepV1 step)
        {
            return step switch
            {
                TrainingLabStepV1.Rotation => "步骤 1 · 轮转",
                TrainingLabStepV1.Positioning => "步骤 2 · 摆位",
                TrainingLabStepV1.ServeBall => "步骤 3 · 发球球",
                TrainingLabStepV1.Validation => "步骤 4 · 校验",
                _ => "步骤 5 · 运行"
            };
        }

        private static string InspectorSummary(TrainingLabStepV1 step)
        {
            return step switch
            {
                TrainingLabStepV1.Rotation => "先确定法律位次；画布暂不允许拖动球员。",
                TrainingLabStepV1.Positioning => "在中间画布拖动 12 名球员，红色关系必须全部消除。",
                TrainingLabStepV1.ServeBall => "仅编辑发球前球的位置和初速度。",
                TrainingLabStepV1.Validation => "冻结输入并检查训练起点是否可运行。",
                _ => "正式回合已锁定；底部控制台提供完整运行证据。"
            };
        }

        private static string ServeToolSummary(TrainingServeToolV1 tool)
        {
            return tool switch
            {
                TrainingServeToolV1.AdjustVelocity => "当前工具：调整速度。红色速度箭头与 VX/VY/VZ 可编辑。",
                TrainingServeToolV1.ViewTrajectory => "当前工具：查看轨迹。画布只读，不改变发球球。",
                _ => "当前工具：移动球。黄色球只能位于当前发球方己方底线后。"
            };
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
            _contextualInspector.SetEnabled(editable);
            _save.SetEnabled(_controller.LocalScenario != null &&
                _controller.IsDirty && editable);
            _returnToHub.SetEnabled(
                state != TrainingScenarioLabStateV1.Running &&
                state != TrainingScenarioLabStateV1.Paused);
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
            _viewportHint.text = _controller.CurrentStep == TrainingLabStepV1.Rotation
                ? "步骤 1：确认轮转位次后才能拖动摆位"
                : _controller.ServeTool == TrainingServeToolV1.AdjustVelocity
                    ? "速度工具：拖动红色速度箭头或精确填写 VX/VY/VZ"
                    : _controller.ServeTool == TrainingServeToolV1.ViewTrajectory
                        ? "轨迹工具：只读预览"
                        : "摆位：球员头顶显示职业与锁定的 1-6 位次";
            foreach (var step in Enum.GetValues(typeof(TrainingLabStepV1)).Cast<TrainingLabStepV1>())
                _root.Q<Button>("step-" + step.ToString().ToLowerInvariant())?.EnableInClassList("active-step", _controller.CurrentStep == step);
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
            _markers.Add(id, marker);
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

        private void OnTokenPointerDown(PointerDownEvent value)
        {
            if (_controller.EditingLocked ||
                _controller.CurrentStep == TrainingLabStepV1.Rotation)
                return;
            var token = value.currentTarget as VisualElement;
            var id = token?.userData as string;
            if (string.IsNullOrWhiteSpace(id)) return;
            if (id == "ball" && (_controller.CurrentStep !=
                    TrainingLabStepV1.ServeBall ||
                _controller.ServeTool != TrainingServeToolV1.MoveBall))
                return;
            _dragPointer = value.pointerId;
            _dragObjectId = id;
            _controller.SelectObject(id, "position");
            _tacticalBoard.CapturePointer(value.pointerId);
            value.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent value)
        {
            if (_dragPointer != value.pointerId ||
                string.IsNullOrWhiteSpace(_dragObjectId) ||
                !_tacticalBoard.HasPointerCapture(value.pointerId))
                return;
            var point = _courtSurface.WorldToLocal(value.position);
            if (_dragObjectId == "ball")
            {
                if (_controller.ServeTool != TrainingServeToolV1.MoveBall) return;
                var current = _controller.Draft.BallPosition;
                _controller.SetBallPosition(
                    TrainingLabCourtProjectionV1.ServeBoardToCourtPosition(
                        _courtBoardRect, point, current.Y,
                        _controller.Draft.FirstServingSide));
            }
            else
            {
                if (!_courtBoardRect.Contains(point)) return;
                var side = _controller.Draft.Context.Home.Players.Any(player =>
                    player.PlayerId.Value == _dragObjectId)
                    ? TeamSide.Home : TeamSide.Away;
                _controller.SetPlayerPosition(
                    new StablePlayerId(_dragObjectId),
                    TrainingLabCourtProjectionV1.BoardToPlayerPosition(
                        _courtBoardRect, point, side));
            }
            value.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent value)
        {
            if (_dragPointer != value.pointerId) return;
            if (_tacticalBoard.HasPointerCapture(value.pointerId))
                _tacticalBoard.ReleasePointer(value.pointerId);
            _dragPointer = -1;
            _dragObjectId = null;
            value.StopPropagation();
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

        private void TrySelectServeTool(TrainingServeToolV1 tool)
        {
            try
            {
                _controller.SelectServeTool(tool);
            }
            catch (InvalidOperationException exception)
            {
                _feedback.text = exception.Message;
                _viewportHint.text = "发球设置已锁定：先修正全部站位错误";
            }
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
