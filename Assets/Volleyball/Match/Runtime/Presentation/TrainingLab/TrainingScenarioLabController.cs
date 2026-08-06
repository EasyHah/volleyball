using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.AI;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Presentation.TrainingLab
{
    public enum TrainingScenarioLabStateV1
    {
        Editing,
        Validating,
        Ready,
        Running,
        Paused,
        Completed,
        Faulted
    }

    public enum TrainingLabStepV1 { Rotation, Positioning, ServeBall, Validation, Running }
    public enum TrainingServeToolV1 { MoveBall, AdjustVelocity, ViewTrajectory }

    public sealed class TrainingScenarioLabController : IDisposable
    {
        private readonly TrainingScenarioDraftStoreV1 _store;
        private readonly ITrainingSimulationControllerV1 _simulation;
        private int _sessionSequence;
        private bool _disposed;

        public TrainingScenarioLabController(
            TrainingScenarioDraftStoreV1 store,
            ITrainingSimulationControllerV1 simulation,
            string initialEntryKey = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _simulation = simulation ??
                          throw new ArgumentNullException(nameof(simulation));
            _simulation.Completed += OnCompleted;
            _simulation.Faulted += OnFaulted;
            SelectedEntryKey = string.IsNullOrWhiteSpace(initialEntryKey)
                ? _store.FirstBuiltInKey
                : initialEntryKey;
            Draft = _store.Load(SelectedEntryKey);
            Validation = TrainingScenarioValidatorV1.Validate(Draft);
            State = TrainingScenarioLabStateV1.Editing;
            SelectedObjectId = "ball";
            SelectedPropertyPath = "ball.position";
            CurrentStep = Draft.RotationLocked ? TrainingLabStepV1.Positioning : TrainingLabStepV1.Rotation;
        }

        public event Action Changed;

        public TrainingScenarioLabStateV1 State { get; private set; }
        public TrainingScenarioDraftV1 Draft { get; private set; }
        public TrainingScenarioValidationResultV1 Validation { get; private set; }
        public TrainingScenarioV1 ReadyScenario { get; private set; }
        public TrainingRunEvidenceV1 LastEvidence { get; private set; }
        public TrainingRunEvidenceV1 PreviousEvidence { get; private set; }
        public TrainingRunEvidenceV1 VisibleEvidence =>
            LastEvidence ?? _simulation.CurrentEvidence;
        public string SelectedEntryKey { get; private set; }
        public string SelectedObjectId { get; private set; }
        public string SelectedPropertyPath { get; private set; }
        public string FailureMessage { get; private set; } = string.Empty;
        public string RunComparisonSummary { get; private set; } =
            "尚未运行";
        public TrainingLabStepV1 CurrentStep { get; private set; }
        public TrainingServeToolV1 ServeTool { get; private set; } = TrainingServeToolV1.MoveBall;
        public IReadOnlyList<PositionFaultV1> PositionFaultPreview => CreatePositionFaultPreview();
        public bool CanEnterServeSetup =>
            Draft.RotationLocked && PositionFaultPreview.Count == 0;
        public string ServeSetupBlockReason => !Draft.RotationLocked
            ? "Confirm rotation before configuring the serve."
            : PositionFaultPreview.Count > 0
                ? "Resolve every position fault before configuring the serve."
                : string.Empty;
        public IReadOnlyList<TrainingScenarioDraftEntryV1> Entries =>
            _store.Entries;
        public bool EditingLocked =>
            State == TrainingScenarioLabStateV1.Running ||
            State == TrainingScenarioLabStateV1.Paused ||
            State == TrainingScenarioLabStateV1.Completed ||
            State == TrainingScenarioLabStateV1.Faulted;

        public void SelectDraftEntry(string key)
        {
            EnsureEditable();
            SelectedEntryKey = key;
            Draft = _store.Load(key);
            ResetAuthoringState();
        }

        public void NewDraft()
        {
            EnsureEditable();
            var draft = _store.Load(_store.FirstBuiltInKey);
            var suffix = (++_sessionSequence).ToString();
            draft.ScenarioId =
                TrainingScenarioV1.ScenarioIdPrefix + "session-" + suffix;
            draft.DisplayName = "新建草稿 " + suffix;
            draft.Source = "session";
            SelectedEntryKey = _store.AddSession(draft);
            Draft = draft;
            ResetAuthoringState();
        }

        public void DuplicateDraft()
        {
            EnsureEditable();
            var copy = Draft.DeepCopy();
            var suffix = (++_sessionSequence).ToString();
            copy.ScenarioId =
                TrainingScenarioV1.ScenarioIdPrefix + "copy-" + suffix;
            copy.DisplayName = Draft.DisplayName + "（副本）";
            copy.Source = "session";
            SelectedEntryKey = _store.AddSession(copy);
            Draft = copy;
            ResetAuthoringState();
        }

        public void ResetDraft()
        {
            EnsureEditable();
            Draft = _store.Load(SelectedEntryKey);
            ResetAuthoringState();
        }

        public void SetDisplayName(string value)
        {
            Mutate(() => Draft.DisplayName = value);
        }

        public void SetBallPosition(SimVector3 value)
        {
            Mutate(() => Draft.BallPosition = value);
        }

        public void SetBallVelocity(SimVector3 value)
        {
            Mutate(() => Draft.BallVelocity = value);
        }

        public void SetRotation(TeamSide side, IReadOnlyList<StablePlayerId> playerIds)
        {
            if (CurrentStep != TrainingLabStepV1.Rotation)
                throw new InvalidOperationException("Rotation can only be edited in the rotation step.");
            Mutate(() =>
            {
                var target = side == TeamSide.Home ? Draft.HomeRotation : Draft.AwayRotation;
                target.Clear();
                if (playerIds != null) target.AddRange(playerIds);
                Draft.RotationLocked = false;
            });
        }

        public void ConfirmRotation()
        {
            EnsureEditable();
            Draft.RotationLocked = true;
            Validation = TrainingScenarioValidatorV1.Validate(Draft);
            if (Validation.Issues.Any(value => value.Code ==
                    TrainingScenarioIssueCodesV1.InvalidRotationMembership))
            {
                Draft.RotationLocked = false;
                throw new InvalidOperationException("Both rotations must contain each on-court player exactly once.");
            }
            CurrentStep = TrainingLabStepV1.Positioning;
            Changed?.Invoke();
        }

        public void ReopenRotation()
        {
            EnsureEditable();
            Draft.RotationLocked = false;
            ReadyScenario = null;
            CurrentStep = TrainingLabStepV1.Rotation;
            Validation = TrainingScenarioValidatorV1.Validate(Draft);
            Changed?.Invoke();
        }

        public void SelectServeTool(TrainingServeToolV1 tool)
        {
            EnsureEditable();
            if (!Enum.IsDefined(typeof(TrainingServeToolV1), tool))
                throw new ArgumentOutOfRangeException(nameof(tool));
            if (!CanEnterServeSetup)
                throw new InvalidOperationException(ServeSetupBlockReason);
            ServeTool = tool;
            CurrentStep = TrainingLabStepV1.ServeBall;
            Changed?.Invoke();
        }

        public void GoToPositioning()
        {
            EnsureEditable();
            if (!Draft.RotationLocked)
                throw new InvalidOperationException("Confirm rotation before positioning players.");
            CurrentStep = TrainingLabStepV1.Positioning;
            Changed?.Invoke();
        }

        public void GoToValidation()
        {
            EnsureEditable();
            if (!Draft.RotationLocked)
                throw new InvalidOperationException("Confirm rotation before validation.");
            CurrentStep = TrainingLabStepV1.Validation;
            Changed?.Invoke();
        }

        public void SetPlayerPosition(
            StablePlayerId playerId,
            SimVector3 value)
        {
            if (!Draft.RotationLocked || CurrentStep == TrainingLabStepV1.Rotation)
                throw new InvalidOperationException("Confirm rotation before positioning players.");
            Mutate(() =>
            {
                var pose = Draft.Players.Single(player =>
                    player != null && player.PlayerId.Equals(playerId));
                pose.Position = value;
            });
        }

        public void SetRallyStart(
            RallyStartRecipeV3 recipe,
            TeamSide sourceTeam,
            StablePlayerId? lastLegalActor)
        {
            Mutate(() =>
            {
                Draft.StartRecipe = recipe;
                Draft.SourceTeam = sourceTeam;
                Draft.LastLegalActor = lastLegalActor;
            });
        }

        public void SetTrainingAttributeOverride(StablePlayerId playerId,
            TrainingPlayerAttributeOverrideV1 value)
        {
            Mutate(() =>
            {
                if (Draft.AccessLevel != TrainingScenarioAccessLevelV1.Developer)
                    throw new InvalidOperationException("Only administrators can edit training test attributes.");
                if (!Draft.Players.Any(player => player != null && player.PlayerId.Equals(playerId)))
                    throw new ArgumentException("The player is not in this training scenario.", nameof(playerId));
                if (value == null) Draft.AttributeOverrides.Remove(playerId);
                else Draft.AttributeOverrides[playerId] = value;
            });
        }

        public void SaveCameraBookmark(string name, SimVector3 position,
            SimVector3 forward, float orthographicSize, bool orthographic)
        {
            Mutate(() =>
            {
                if (Draft.CameraBookmarks.Any(value => value.Name == name))
                    throw new InvalidOperationException("Camera bookmark names must be unique.");
                Draft.CameraBookmarks.Add(new TrainingCameraBookmarkV1(name,
                    position, forward, orthographicSize, orthographic));
            });
        }

        public void SetMatchSeed(int seed)
        {
            Mutate(() =>
            {
                var context = Draft.Context ??
                              throw new InvalidOperationException(
                                  "A complete match context is required.");
                Draft.Context = MatchContextV4.Create(
                    context.SessionId,
                    seed,
                    context.Home,
                    context.Away,
                    context.PhysicsConfigurationHash,
                    context.TrajectoryPredictionProviderConfiguration,
                    context.RulesVersion);
            });
        }

        public void SetTacticRoutes(
            TeamSide side,
            SetRoute setRoute,
            SpikeRoute spikeRoute)
        {
            Mutate(() =>
            {
                var tactic = TrainingScenarioDraftV1.Clone(
                    side == TeamSide.Home
                        ? Draft.HomeTactics
                        : Draft.AwayTactics);
                if (tactic == null)
                    throw new InvalidOperationException(
                        "A complete tactic is required.");
                tactic.SetRoute = setRoute;
                tactic.SpikeRoute = spikeRoute;
                if (side == TeamSide.Home)
                    Draft.HomeTactics = tactic;
                else
                    Draft.AwayTactics = tactic;
            });
        }

        public void SelectObject(string objectId, string propertyPath = "")
        {
            SelectedObjectId = objectId ?? string.Empty;
            SelectedPropertyPath = propertyPath ?? string.Empty;
            Changed?.Invoke();
        }

        public void FocusIssue(int index)
        {
            if (index < 0 || index >= Validation.Issues.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var issue = Validation.Issues[index];
            SelectedObjectId = string.IsNullOrWhiteSpace(issue.ObjectId) &&
                               issue.PropertyPath.StartsWith(
                                   "ball",
                                   StringComparison.Ordinal)
                ? "ball"
                : issue.ObjectId;
            SelectedPropertyPath = issue.PropertyPath;
            Changed?.Invoke();
        }

        public bool Validate()
        {
            EnsureEditable();
            State = TrainingScenarioLabStateV1.Validating;
            Changed?.Invoke();
            Validation = TrainingScenarioValidatorV1.Validate(Draft);
            if (!Validation.IsValid)
            {
                ReadyScenario = null;
                State = TrainingScenarioLabStateV1.Editing;
                Changed?.Invoke();
                return false;
            }

            ReadyScenario = TrainingScenarioValidatorV1.Build(Draft);
            State = TrainingScenarioLabStateV1.Ready;
            CurrentStep = TrainingLabStepV1.Validation;
            Changed?.Invoke();
            return true;
        }

        public bool Run()
        {
            EnsureEditable();
            if (State != TrainingScenarioLabStateV1.Ready && !Validate())
                return false;
            StartReadyScenario();
            return true;
        }

        public void Pause()
        {
            if (State != TrainingScenarioLabStateV1.Running)
                throw new InvalidOperationException(
                    "Only a running training scenario can be paused.");
            _simulation.Pause();
            State = TrainingScenarioLabStateV1.Paused;
            Changed?.Invoke();
        }

        public void Resume()
        {
            if (State != TrainingScenarioLabStateV1.Paused)
                throw new InvalidOperationException(
                    "Only a paused training scenario can resume.");
            _simulation.Resume();
            State = TrainingScenarioLabStateV1.Running;
            Changed?.Invoke();
        }

        public void Step()
        {
            if (State != TrainingScenarioLabStateV1.Paused)
                throw new InvalidOperationException(
                    "Fixed-step advance requires Paused state.");
            _simulation.Step();
            Changed?.Invoke();
        }

        public void RerunSameSeed()
        {
            if (State != TrainingScenarioLabStateV1.Completed ||
                ReadyScenario == null)
            {
                throw new InvalidOperationException(
                    "A completed frozen scenario is required for rerun.");
            }

            _simulation.Reset();
            PreviousEvidence = LastEvidence;
            LastEvidence = null;
            FailureMessage = string.Empty;
            _simulation.Start(ReadyScenario);
            State = TrainingScenarioLabStateV1.Running;
            CurrentStep = TrainingLabStepV1.Running;
            Changed?.Invoke();
        }

        public void ReturnToEditing()
        {
            if (State == TrainingScenarioLabStateV1.Running ||
                State == TrainingScenarioLabStateV1.Paused)
            {
                throw new InvalidOperationException(
                    "Stop the active run before returning to editing.");
            }

            _simulation.Reset();
            ReadyScenario = null;
            FailureMessage = string.Empty;
            Validation = TrainingScenarioValidatorV1.Validate(Draft);
            State = TrainingScenarioLabStateV1.Editing;
            CurrentStep = Draft.RotationLocked ? TrainingLabStepV1.Positioning : TrainingLabStepV1.Rotation;
            Changed?.Invoke();
        }

        public string DerivedRuleSummary()
        {
            var hits = Draft.StartRecipe switch
            {
                RallyStartRecipeV3.ServeFlight => 0,
                RallyStartRecipeV3.AfterReceive => 1,
                RallyStartRecipeV3.AfterSet => 2,
                RallyStartRecipeV3.AfterAttack => 3,
                RallyStartRecipeV3.AfterAcceptedBlock => 0,
                _ => -1
            };
            return "派生触球数 " + hits +
                   " · 起点 " + Draft.StartRecipe +
                   " · 来源 " + Draft.SourceTeam +
                   " · 最后触球 " +
                   (Draft.LastLegalActor?.Value ?? "无");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _simulation.Completed -= OnCompleted;
            _simulation.Faulted -= OnFaulted;
            _simulation.Reset();
            _disposed = true;
        }

        private void Mutate(Action action)
        {
            EnsureEditable();
            action();
            ReadyScenario = null;
            State = TrainingScenarioLabStateV1.Editing;
            Validation = TrainingScenarioValidatorV1.Validate(Draft);
            if (_store.IsSession(SelectedEntryKey))
                _store.UpdateSession(SelectedEntryKey, Draft);
            Changed?.Invoke();
        }

        private void StartReadyScenario()
        {
            try
            {
                PreviousEvidence = null;
                LastEvidence = null;
                FailureMessage = string.Empty;
                _simulation.Start(ReadyScenario);
            State = TrainingScenarioLabStateV1.Running;
            CurrentStep = TrainingLabStepV1.Running;
            Changed?.Invoke();
            }
            catch (Exception exception)
            {
                OnFaulted(exception.Message);
            }
        }

        private void OnCompleted(TrainingRunEvidenceV1 evidence)
        {
            PreviousEvidence ??= LastEvidence;
            LastEvidence = evidence ??
                           throw new ArgumentNullException(nameof(evidence));
            RunComparisonSummary = Compare(PreviousEvidence, LastEvidence);
            State = TrainingScenarioLabStateV1.Completed;
            Changed?.Invoke();
        }

        private void OnFaulted(string message)
        {
            FailureMessage = string.IsNullOrWhiteSpace(message)
                ? "正式训练运行异常"
                : message;
            State = TrainingScenarioLabStateV1.Faulted;
            Changed?.Invoke();
        }

        private void ResetAuthoringState()
        {
            ReadyScenario = null;
            LastEvidence = null;
            PreviousEvidence = null;
            FailureMessage = string.Empty;
            RunComparisonSummary = "尚未运行";
            Validation = TrainingScenarioValidatorV1.Validate(Draft);
            State = TrainingScenarioLabStateV1.Editing;
            CurrentStep = Draft.RotationLocked ? TrainingLabStepV1.Positioning : TrainingLabStepV1.Rotation;
            SelectedObjectId = "ball";
            SelectedPropertyPath = "ball.position";
            Changed?.Invoke();
        }

        private void EnsureEditable()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(TrainingScenarioLabController));
            if (EditingLocked)
                throw new InvalidOperationException(
                    "The frozen training runtime cannot be edited.");
        }

        private static string Compare(
            TrainingRunEvidenceV1 previous,
            TrainingRunEvidenceV1 current)
        {
            if (previous == null) return "首轮运行已记录";
            var same = previous.ScenarioHash == current.ScenarioHash &&
                       previous.MatchSeed == current.MatchSeed &&
                       previous.WinningTeam == current.WinningTeam &&
                       previous.ResolutionReason == current.ResolutionReason &&
                       previous.Decisions.Select(value => value.SnapshotHash)
                           .SequenceEqual(current.Decisions.Select(value =>
                               value.SnapshotHash)) &&
                       previous.Timeline.Select(TimelineSignature)
                           .SequenceEqual(current.Timeline.Select(
                               TimelineSignature));
            return same
                ? "同 seed 双跑一致"
                : "同 seed 双跑不一致";
        }

        private static string TimelineSignature(
            TrainingTimelineEventV1 value)
        {
            return value.AuthoritySourceSequence + "|" +
                   value.SimulationTimeSeconds.ToString("R",
                       System.Globalization.CultureInfo.InvariantCulture) +
                   "|" + value.Kind + "|" + value.Team + "|" +
                   (value.PlayerId?.Value ?? string.Empty) + "|" +
                   value.Summary + "|" +
                   (value.Decision?.SnapshotHash ?? string.Empty);
        }

        private IReadOnlyList<PositionFaultV1> CreatePositionFaultPreview()
        {
            try
            {
                var poses = Draft.Players.OrderBy(value => value.PlayerId.Value,
                        StringComparer.Ordinal)
                    .Select(value => new TrainingPlayerPoseV1(value.PlayerId,
                        value.Position, value.Forward, value.Pose)).ToArray();
                return PositionFaultEvaluatorV1.Evaluate(new TrainingServeStartV1(
                    Draft.FirstServingSide, Draft.HomeRotation, Draft.AwayRotation,
                    poses, Draft.BallPosition, Draft.BallVelocity).CreatePositionSlots());
            }
            catch (Exception)
            {
                return Array.Empty<PositionFaultV1>();
            }
        }
    }
}
