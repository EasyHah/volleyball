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
        private Label _state;
        private Label _hash;
        private Label _rules;
        private Label _selection;
        private Label _comparison;
        private Label _feedback;
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
        private bool _rendering;
        private bool _ownsController;
        private bool _initialized;
        private int _dragPointer = -1;
        private string _dragObjectId;
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
            _scenarioList = _root.Q<VisualElement>("scenario-list");
            _issues = _root.Q<VisualElement>("issue-list");
            _timeline = _root.Q<VisualElement>("timeline-list");
            _viewport = _root.Q<VisualElement>("world-viewport");
            _editorControls = _root.Q<VisualElement>("editor-controls");
            _state = _root.Q<Label>("state-label");
            _hash = _root.Q<Label>("hash-label");
            _rules = _root.Q<Label>("rules-label");
            _selection = _root.Q<Label>("selection-label");
            _comparison = _root.Q<Label>("comparison-label");
            _feedback = _root.Q<Label>("feedback-label");
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

            _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
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
                RenderIssues();
                RenderTimeline();
                RenderControls();
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
            if (_markers.TryGetValue("ball", out var ball))
                ball.transform.localPosition = ToUnity(draft.BallPosition);
            foreach (var pose in draft.Players)
            {
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

        private void OnPointerDown(PointerDownEvent value)
        {
            if (_controller.EditingLocked || _worldCamera == null) return;
            var ray = ScreenRay(value.position);
            if (!Physics.Raycast(ray, out var hit, 100f)) return;
            var marker =
                hit.collider.GetComponent<TrainingLabPreviewMarkerV1>();
            if (marker == null) return;
            _dragPointer = value.pointerId;
            _dragObjectId = marker.ObjectId;
            _controller.SelectObject(_dragObjectId, "position");
            _viewport.CapturePointer(value.pointerId);
            value.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent value)
        {
            if (_dragPointer != value.pointerId ||
                string.IsNullOrWhiteSpace(_dragObjectId) ||
                !_viewport.HasPointerCapture(value.pointerId))
                return;
            var plane = new Plane(Vector3.up, Vector3.zero);
            var ray = ScreenRay(value.position);
            if (!plane.Raycast(ray, out var distance)) return;
            var point = ray.GetPoint(distance);
            if (_dragObjectId == "ball")
            {
                var current = _controller.Draft.BallPosition;
                _controller.SetBallPosition(
                    new SimVector3(point.x, current.Y, point.z));
            }
            else
            {
                _controller.SetPlayerPosition(
                    new StablePlayerId(_dragObjectId),
                    new SimVector3(point.x, 0f, point.z));
            }
            value.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent value)
        {
            if (_dragPointer != value.pointerId) return;
            if (_viewport.HasPointerCapture(value.pointerId))
                _viewport.ReleasePointer(value.pointerId);
            _dragPointer = -1;
            _dragObjectId = null;
            value.StopPropagation();
        }

        private Ray ScreenRay(Vector2 panelPosition)
        {
            return _worldCamera.ScreenPointToRay(new Vector3(
                panelPosition.x,
                Screen.height - panelPosition.y,
                0f));
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
