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
    public sealed class TrainingLabWorkbenchControllerV2 : IDisposable
    {
        private MatchSetupEditorV1 _editor;
        private readonly ITrainingRallySimulationControllerV5 _runtime;
        private readonly TrainingLabLocalScenarioRepositoryV2 _localRepository;
        private TrainingLabLocalScenarioV2 _localScenario;
        private string _savedPersistenceFingerprint = string.Empty;
        private bool _hasSavedLocalScenario;
        private bool _pendingLeaveToHub;
        private string _pendingSwitchEntryKey;
        private bool _disposed;

        public TrainingLabWorkbenchControllerV2(MatchSetupDraftV1 setup,
            ITrainingRallySimulationControllerV5 runtime = null)
        {
            MatchSetup = setup ?? throw new ArgumentNullException(nameof(setup));
            _editor = new MatchSetupEditorV1(setup);
            _runtime = runtime;
            if (_runtime != null)
            {
                _runtime.Completed += OnRuntimeCompleted;
                _runtime.Faulted += OnRuntimeFaulted;
            }
            CurrentStep = setup.RotationLocked
                ? TrainingLabStepV1.Positioning
                : TrainingLabStepV1.Rotation;
            SelectedObjectId = "ball";
        }

        public TrainingLabWorkbenchControllerV2(
            TrainingLabLocalScenarioRepositoryV2 repository,
            TrainingLabLocalScenarioV2 localScenario,
            ITrainingRallySimulationControllerV5 runtime = null,
            bool hasSavedCopy = false)
            : this(localScenario?.MatchSetup ?? throw new ArgumentNullException(
                nameof(localScenario)), runtime)
        {
            _localRepository = repository ?? throw new ArgumentNullException(
                nameof(repository));
            _localScenario = localScenario;
            _hasSavedLocalScenario = hasSavedCopy;
            RestoreUiState(localScenario);
            _savedPersistenceFingerprint = hasSavedCopy
                ? PersistenceFingerprint()
                : string.Empty;
        }

        public event Action Changed;

        public MatchSetupDraftV1 MatchSetup { get; private set; }
        public TrainingScenarioLabStateV1 State { get; private set; } =
            TrainingScenarioLabStateV1.Editing;
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
        public MatchSetupSnapshotV1 PreflightSnapshot { get; private set; }
        public MatchSetupSnapshotV1 RunSnapshot { get; private set; }
        public TrainingRallyOutcomeV1 Outcome { get; private set; }
        public string PreflightError { get; private set; } = string.Empty;
        public string RuntimeError { get; private set; } = string.Empty;
        public bool EditingLocked => State == TrainingScenarioLabStateV1.Running ||
            State == TrainingScenarioLabStateV1.Paused;
        public TrainingLabLocalScenarioV2 LocalScenario => _localScenario;
        public bool IsDirty => _localScenario != null &&
            (!_hasSavedLocalScenario || !string.Equals(
                PersistenceFingerprint(), _savedPersistenceFingerprint,
                StringComparison.Ordinal));

        public void SaveCurrentLocalScenario()
        {
            EnsureLocalScenario();
            SyncLocalUiState();
            _localRepository.Save(_localScenario);
            _hasSavedLocalScenario = true;
            _savedPersistenceFingerprint = PersistenceFingerprint();
            Changed?.Invoke();
        }

        public TrainingLabLeaveRequestV1 RequestLeaveToHub()
        {
            return RequestLeave(true, null);
        }

        public TrainingLabLeaveRequestV1 RequestSwitch(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
                throw new ArgumentException("Entry key is required.",
                    nameof(entryKey));
            return RequestLeave(false, entryKey);
        }

        public TrainingLabLeaveRequestV1 ResolveLeave(
            TrainingLabLeaveDecisionV1 decision)
        {
            EnsureLocalScenario();
            if (!_pendingLeaveToHub &&
                string.IsNullOrWhiteSpace(_pendingSwitchEntryKey))
                throw new InvalidOperationException(
                    "There is no pending TrainingLab leave request.");
            var toHub = _pendingLeaveToHub;
            var target = _pendingSwitchEntryKey;
            if (decision == TrainingLabLeaveDecisionV1.Cancel)
            {
                ClearPendingLeave();
                return LeaveRequest(false, false, false, toHub, target,
                    "Leave cancelled.");
            }
            if (decision == TrainingLabLeaveDecisionV1.Save)
                SaveCurrentLocalScenario();
            else if (decision == TrainingLabLeaveDecisionV1.Discard)
            {
                if (_hasSavedLocalScenario)
                    RestoreLocalScenario(_localRepository.Load(
                        _localScenario.LocalId));
            }
            else
                throw new ArgumentOutOfRangeException(nameof(decision));
            ClearPendingLeave();
            Changed?.Invoke();
            return LeaveRequest(true, false, false, toHub, target,
                string.Empty);
        }

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

        public bool TrySetExactBallPosition(SimVector3 position)
        {
            EnsureServeSetup();
            if (ServeTool != TrainingServeToolV1.MoveBall)
                throw new InvalidOperationException(
                    "Select Move Ball before editing exact position fields.");
            return TryEdit(() => _editor.SetBallPosition(position));
        }

        public bool TrySetExactBallVelocity(SimVector3 velocity)
        {
            EnsureServeSetup();
            if (ServeTool != TrainingServeToolV1.AdjustVelocity)
                throw new InvalidOperationException(
                    "Select Adjust Velocity before editing exact velocity fields.");
            return TryEdit(() => _editor.SetBallVelocity(velocity));
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

        public void SetPlayerAttributeOverride(PlayerId playerId,
            TrainingPlayerAttributeFieldV2 field, int value)
        {
            EnsureNotRunning();
            var item = OverrideFor(playerId, true);
            item.Set(field, value);
            Changed?.Invoke();
        }

        public void SetPlayerDominantHandOverride(PlayerId playerId,
            DominantHandV5 hand)
        {
            EnsureNotRunning();
            var item = OverrideFor(playerId, true);
            item.SetDominantHand(hand);
            Changed?.Invoke();
        }

        public void ClearPlayerAttributeOverride(PlayerId playerId,
            TrainingPlayerAttributeFieldV2 field)
        {
            EnsureNotRunning();
            var item = OverrideFor(playerId, false);
            if (item == null) return;
            item.Clear(field);
            RemoveEmptyOverride(playerId, item);
            Changed?.Invoke();
        }

        public void ResetPlayerAttributeOverrides(PlayerId playerId)
        {
            EnsureNotRunning();
            if (!MatchSetup.AttributeOverrides.Remove(playerId)) return;
            Changed?.Invoke();
        }

        public CareerBaseAttributesV5 EffectiveBases(PlayerId playerId)
        {
            var player = SnapshotFor(playerId);
            var item = OverrideFor(playerId, false);
            return item == null ? player.Bases : item.ApplyTo(player.Bases);
        }

        public DominantHandV5 EffectiveDominantHand(PlayerId playerId)
        {
            var player = SnapshotFor(playerId);
            return OverrideFor(playerId, false)?.DominantHand ??
                   player.DominantHand;
        }

        public PlayerSnapshotV5 BasePlayer(PlayerId playerId)
        {
            return SnapshotFor(playerId);
        }

        public TrainingPlayerAttributeOverrideV2 ExplicitOverride(
            PlayerId playerId)
        {
            return OverrideFor(playerId, false);
        }

        public bool EnterPreflight()
        {
            EnsureNotRunning();
            CurrentStep = TrainingLabStepV1.Validation;
            PreflightSnapshot = null;
            PreflightError = string.Empty;
            try
            {
                PreflightSnapshot = _editor.Freeze();
                State = TrainingScenarioLabStateV1.Ready;
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                              exception is InvalidOperationException)
            {
                PreflightError = exception.Message;
                State = TrainingScenarioLabStateV1.Editing;
                Changed?.Invoke();
                return false;
            }
        }

        public bool Run()
        {
            if (_runtime == null) throw new InvalidOperationException(
                "No native V5 training runtime is bound.");
            if (PreflightSnapshot == null && !EnterPreflight()) return false;
            if (State != TrainingScenarioLabStateV1.Ready)
                throw new InvalidOperationException(
                    "A valid automatic preflight is required before Run.");
            Outcome = null;
            RuntimeError = string.Empty;
            RunSnapshot = PreflightSnapshot;
            _runtime.Start(RunSnapshot);
            State = TrainingScenarioLabStateV1.Running;
            CurrentStep = TrainingLabStepV1.Running;
            Changed?.Invoke();
            return true;
        }

        public void Pause()
        {
            if (State != TrainingScenarioLabStateV1.Running)
                throw new InvalidOperationException(
                    "Only a running V5 training rally can be paused.");
            _runtime.Pause();
            State = TrainingScenarioLabStateV1.Paused;
            Changed?.Invoke();
        }

        public void Resume()
        {
            if (State != TrainingScenarioLabStateV1.Paused)
                throw new InvalidOperationException(
                    "Only a paused V5 training rally can resume.");
            _runtime.Resume();
            State = TrainingScenarioLabStateV1.Running;
            Changed?.Invoke();
        }

        public void StepRuntime()
        {
            if (State != TrainingScenarioLabStateV1.Paused)
                throw new InvalidOperationException(
                    "Single-step requires a paused V5 training rally.");
            _runtime.Step();
        }

        public void RerunSameSnapshot()
        {
            if (State != TrainingScenarioLabStateV1.Completed ||
                RunSnapshot == null)
                throw new InvalidOperationException(
                    "A completed frozen V5 rally is required for rerun.");
            _runtime.Reset();
            Outcome = null;
            RuntimeError = string.Empty;
            _runtime.Start(RunSnapshot);
            State = TrainingScenarioLabStateV1.Running;
            CurrentStep = TrainingLabStepV1.Running;
            Changed?.Invoke();
        }

        public void ReturnToEditing()
        {
            if (State == TrainingScenarioLabStateV1.Running ||
                State == TrainingScenarioLabStateV1.Paused)
                throw new InvalidOperationException(
                    "Stop the active V5 training rally before editing.");
            _runtime?.Reset();
            Outcome = null;
            RuntimeError = string.Empty;
            PreflightSnapshot = null;
            RunSnapshot = null;
            State = TrainingScenarioLabStateV1.Editing;
            CurrentStep = TrainingLabStepV1.ServeBall;
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_runtime != null)
            {
                _runtime.Completed -= OnRuntimeCompleted;
                _runtime.Faulted -= OnRuntimeFaulted;
                _runtime.Reset();
            }
            _disposed = true;
        }

        public void SelectObject(PlayerId playerId)
        {
            SelectedObjectId = string.IsNullOrWhiteSpace(playerId.Value)
                ? throw new ArgumentException("Player ID is required.",
                    nameof(playerId))
                : playerId.Value;
            Changed?.Invoke();
        }

        private TrainingLabLeaveRequestV1 RequestLeave(bool toHub,
            string targetEntryKey)
        {
            EnsureLocalScenario();
            if (EditingLocked)
            {
                ClearPendingLeave();
                return LeaveRequest(false, false, true, toHub,
                    targetEntryKey,
                    "Stop the active V5 training rally before leaving.");
            }
            if (!IsDirty)
                return LeaveRequest(true, false, false, toHub,
                    targetEntryKey, string.Empty);
            _pendingLeaveToHub = toHub;
            _pendingSwitchEntryKey = targetEntryKey;
            return LeaveRequest(false, true, false, toHub,
                targetEntryKey,
                "The local training scenario has unsaved changes.");
        }

        private void EnsureLocalScenario()
        {
            if (_disposed) throw new ObjectDisposedException(
                nameof(TrainingLabWorkbenchControllerV2));
            if (_localRepository == null || _localScenario == null)
                throw new InvalidOperationException(
                    "No native V5 local scenario is open.");
        }

        private void SyncLocalUiState()
        {
            _localScenario.MatchSetup = MatchSetup;
            _localScenario.ActiveStep = CurrentStep.ToString();
            _localScenario.ActiveView = ActiveServeView.ToString();
            _localScenario.ActiveTool = ServeTool.ToString();
            _localScenario.SelectedObjectId = SelectedObjectId;
        }

        private void RestoreLocalScenario(TrainingLabLocalScenarioV2 local)
        {
            _localScenario = local ?? throw new ArgumentNullException(nameof(local));
            MatchSetup = local.MatchSetup;
            _editor = new MatchSetupEditorV1(MatchSetup);
            RestoreUiState(local);
            PreflightSnapshot = null;
            RunSnapshot = null;
            Outcome = null;
            RuntimeError = string.Empty;
            PreflightError = string.Empty;
            State = TrainingScenarioLabStateV1.Editing;
            _savedPersistenceFingerprint = PersistenceFingerprint();
        }

        private void RestoreUiState(TrainingLabLocalScenarioV2 local)
        {
            if (!Enum.TryParse(local.ActiveStep, out TrainingLabStepV1 step))
                step = MatchSetup.RotationLocked
                    ? TrainingLabStepV1.Positioning
                    : TrainingLabStepV1.Rotation;
            if (!Enum.TryParse(local.ActiveView, out TrainingServeViewV1 view))
                view = TrainingServeViewV1.Top;
            if (!Enum.TryParse(local.ActiveTool, out TrainingServeToolV1 tool))
                tool = TrainingServeToolV1.MoveBall;
            CurrentStep = step;
            ActiveServeView = view;
            ServeTool = tool;
            SelectedObjectId = string.IsNullOrWhiteSpace(local.SelectedObjectId)
                ? "ball"
                : local.SelectedObjectId;
        }

        private string PersistenceFingerprint()
        {
            return string.Join("\n",
                MatchSetupJsonV1.Serialize(MatchSetup),
                _localScenario.DisplayName ?? string.Empty,
                CurrentStep.ToString(),
                ActiveServeView.ToString(),
                ServeTool.ToString(),
                SelectedObjectId ?? string.Empty,
                _localScenario.BookmarksJson ?? string.Empty);
        }

        private void ClearPendingLeave()
        {
            _pendingLeaveToHub = false;
            _pendingSwitchEntryKey = null;
        }

        private static TrainingLabLeaveRequestV1 LeaveRequest(bool canLeave,
            bool requiresDecision, bool isBlocked, bool toHub,
            string targetEntryKey, string message)
        {
            return new TrainingLabLeaveRequestV1(canLeave,
                requiresDecision, isBlocked, toHub, targetEntryKey, message);
        }

        private void EnsureRotationEditable()
        {
            if (MatchSetup.RotationLocked ||
                CurrentStep != TrainingLabStepV1.Rotation)
                throw new InvalidOperationException(
                    "Reopen the rotation page before changing Match slots.");
        }

        private void EnsureNotRunning()
        {
            if (_disposed) throw new ObjectDisposedException(
                nameof(TrainingLabWorkbenchControllerV2));
            if (EditingLocked) throw new InvalidOperationException(
                "The frozen V5 training runtime cannot be edited.");
        }

        private void OnRuntimeCompleted(TrainingRallyOutcomeV1 outcome)
        {
            Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            State = TrainingScenarioLabStateV1.Completed;
            Changed?.Invoke();
        }

        private void OnRuntimeFaulted(string message)
        {
            RuntimeError = string.IsNullOrWhiteSpace(message)
                ? "Native V5 training runtime faulted."
                : message;
            State = TrainingScenarioLabStateV1.Faulted;
            Changed?.Invoke();
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

        private TrainingPlayerAttributeOverrideV2 OverrideFor(
            PlayerId playerId, bool create)
        {
            SnapshotFor(playerId);
            if (MatchSetup.AttributeOverrides.TryGetValue(playerId,
                    out var item)) return item;
            if (!create) return null;
            item = new TrainingPlayerAttributeOverrideV2();
            MatchSetup.AttributeOverrides.Add(playerId, item);
            return item;
        }

        private void RemoveEmptyOverride(PlayerId playerId,
            TrainingPlayerAttributeOverrideV2 item)
        {
            if (!item.HasAny) MatchSetup.AttributeOverrides.Remove(playerId);
        }

        private PlayerSnapshotV5 SnapshotFor(PlayerId playerId)
        {
            foreach (var player in MatchSetup.BaseContext.Home.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player;
            foreach (var player in MatchSetup.BaseContext.Away.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player;
            throw new ArgumentException(
                "Player is not a member of the V5 Match setup.",
                nameof(playerId));
        }
    }
}
