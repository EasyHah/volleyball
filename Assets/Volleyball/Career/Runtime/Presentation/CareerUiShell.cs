using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class CareerUiShell : MonoBehaviour
    {
        private readonly List<string> _weekActionIds = new List<string>();
        private UIDocument _document;
        private VisualElement _routeContent;
        private VisualElement _modalLayer;
        private VisualElement _modalContent;
        private ScrollView _routeScroll;
        private Label _routeTitle;
        private Label _saveState;
        private Label _feedback;
        private Button _backButton;
        private Button _saveButton;
        private CareerUiSessionController _controller;
        private CancellationTokenSource _lifetime;
        private string _firstActionId;
        private string _secondActionId;
        private string _focusBeforeModalName;
        private string _lastPageFocusName;
        private bool _modalOpen;
        private int _buttonOrdinal;
        private Font _runtimeFont;

        public CareerUiSessionController Controller => _controller;

        public void Bind(CareerUiSessionController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (ReferenceEquals(_controller, controller))
            {
                return;
            }

            if (_controller != null)
            {
                _controller.Changed -= Render;
            }

            _controller = controller;
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
            _lifetime = new CancellationTokenSource();
            EnsureDocument();
            if (_controller != null)
            {
                _controller.Changed -= Render;
                _controller.Changed += Render;
                Render();
            }
            else
            {
                RenderUnbound();
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.Changed -= Render;
            }

            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
        }

        private void EnsureDocument()
        {
            if (_routeContent != null)
            {
                return;
            }

            _document = _document != null ? _document : GetComponent<UIDocument>();
            var root = _document.rootVisualElement;
            _routeContent = root.Q<VisualElement>("route-content");
            _routeScroll = root.Q<ScrollView>("route-scroll");
            _modalLayer = root.Q<VisualElement>("modal-layer");
            _modalContent = root.Q<VisualElement>("modal-content");
            _routeTitle = root.Q<Label>("route-title");
            _saveState = root.Q<Label>("save-state-label");
            _feedback = root.Q<Label>("feedback-label");
            _backButton = root.Q<Button>("back-button");
            _saveButton = root.Q<Button>("save-button");

            root.Q<Label>("app-title").text = CareerTextTable.Get("app.title");
            root.Q<Label>("prototype-label").text = CareerTextTable.Get("app.prototype");
            _backButton.text = CareerTextTable.Get("action.back");
            _saveButton.text = CareerTextTable.Get("action.save");
            _backButton.clicked += OnBack;
            _saveButton.clicked += OnSave;
            TryApplyChineseSystemFont(root);
        }

        private void RenderUnbound()
        {
            if (_routeContent == null)
            {
                return;
            }

            _routeTitle.text = CareerTextTable.Route(CareerUiRoute.ProfileHub);
            _saveState.text = CareerTextTable.Get("save.label") + " · " +
                              CareerTextTable.Get("save.ready");
            _feedback.text = CareerTextTable.Feedback("ready");
            _backButton.SetEnabled(false);
            _saveButton.SetEnabled(false);
            _routeContent.Clear();
            _routeContent.Add(Title(CareerTextTable.Get("profile.create_title")));
        }

        private void Render()
        {
            if (_controller == null || _routeContent == null)
            {
                return;
            }

            var focusedBeforeRender = _document.rootVisualElement.focusController
                ?.focusedElement as VisualElement;
            if (!_modalOpen &&
                focusedBeforeRender != null &&
                !string.IsNullOrEmpty(focusedBeforeRender.name))
            {
                _lastPageFocusName = focusedBeforeRender.name;
            }

            var willShowModal = _controller.ShowsEventModal;
            if (!_modalOpen && willShowModal)
            {
                _focusBeforeModalName =
                    focusedBeforeRender != null &&
                    !string.IsNullOrEmpty(focusedBeforeRender.name)
                        ? focusedBeforeRender.name
                        : _lastPageFocusName;
            }

            _routeTitle.text = CareerTextTable.Route(_controller.Route);
            _saveState.text = CareerTextTable.Get("save.label") + " · " +
                              CareerTextTable.SaveState(_controller.SaveState);
            _feedback.text = CareerTextTable.Feedback(_controller.FeedbackCode);
            _backButton.SetEnabled(!_controller.IsBusy &&
                                   !willShowModal &&
                                   _controller.Route != CareerUiRoute.ProfileHub);
            _saveButton.SetEnabled(!_controller.IsBusy &&
                                   !willShowModal &&
                                   _controller.Snapshot != null &&
                                   _controller.Snapshot.Progression.Kind ==
                                   CareerProgressionKind.Planning);
            _routeContent.Clear();
            _routeContent.SetEnabled(!willShowModal);
            _buttonOrdinal = 0;

            switch (_controller.Route)
            {
                case CareerUiRoute.ProfileHub:
                    RenderProfiles();
                    break;
                case CareerUiRoute.CareerHub:
                    RenderCareers();
                    break;
                case CareerUiRoute.Onboarding:
                    RenderOnboarding();
                    break;
                case CareerUiRoute.WeekHome:
                    RenderWeekHome();
                    break;
                case CareerUiRoute.PreMatch:
                    RenderPreMatch();
                    break;
                case CareerUiRoute.MatchSummary:
                    RenderMatchSummary();
                    break;
                case CareerUiRoute.WeekendNotice:
                    RenderWeekendNotice();
                    break;
            }

            RenderEventModal();
            RenderDiagnostics();
            if (willShowModal)
            {
                ScheduleFocus(_modalContent, null);
            }
            else
            {
                ScheduleFocus(
                    _routeContent,
                    _modalOpen ? _focusBeforeModalName : null);
                _focusBeforeModalName = null;
            }

            _modalOpen = willShowModal;
        }

        private void RenderProfiles()
        {
            _routeContent.Add(Title(CareerTextTable.Route(CareerUiRoute.ProfileHub)));
            if (_controller.Profiles.Count == 0)
            {
                _routeContent.Add(Hint(CareerTextTable.Get("profile.empty")));
            }
            else
            {
                var row = CardRow();
                foreach (var profile in _controller.Profiles)
                {
                    var captured = profile;
                    var card = Card();
                    card.Add(CardTitle(profile.DisplayName));
                    card.Add(Detail(CareerTextTable.ProfileLoadability(
                        profile.Loadability)));
                    var select = ActionButton(
                        CareerTextTable.Get("action.select"),
                        () => _controller.SelectProfile(captured.ProfileId));
                    select.SetEnabled(
                        profile.Loadability == ProfileLoadability.Loadable);
                    card.Add(select);
                    row.Add(card);
                }

                _routeContent.Add(row);
            }

            _routeContent.Add(SectionTitle(CareerTextTable.Get("profile.create_title")));
            var form = FormCard();
            var name = new TextField(CareerTextTable.Get("field.profile_name"));
            name.AddToClassList("field");
            form.Add(name);
            form.Add(ActionButton(
                CareerTextTable.Get("action.create"),
                () => _controller.CreateProfile(name.value)));
            _routeContent.Add(form);
        }

        private void RenderCareers()
        {
            var profile = _controller.Profile;
            if (profile == null)
            {
                return;
            }

            _routeContent.Add(Title(CareerTextTable.Format(
                "profile.greeting",
                profile.DisplayName)));
            _routeContent.Add(SectionTitle(CareerTextTable.Get("career.list_title")));
            if (profile.CareerEntries.Count == 0)
            {
                _routeContent.Add(Hint(CareerTextTable.Get("career.empty")));
            }
            else
            {
                var row = CardRow();
                foreach (var career in profile.CareerEntries)
                {
                    var captured = career;
                    var card = Card();
                    card.Add(CardTitle(CareerTextTable.Format(
                        "career.card",
                        career.CareerName,
                        career.PlayerDisplayName)));
                    if (career.Season > 0)
                    {
                        card.Add(Detail(CareerTextTable.Format(
                            "career.progress",
                            career.Season,
                            career.Week)));
                    }

                    if (career.Loadability != CareerLoadability.Loadable)
                    {
                        card.Add(Warning(CareerTextTable.Get(
                            career.Loadability == CareerLoadability.RecoveryAvailable
                                ? "career.recovery_available"
                                : "career.unavailable")));
                    }

                    var select = ActionButton(
                        CareerTextTable.Get("action.select"),
                        () => _controller.SelectCareer(
                            profile.ProfileId,
                            captured.SaveId));
                    select.SetEnabled(career.Loadability == CareerLoadability.Loadable);
                    card.Add(select);
                    if (career.Loadability == CareerLoadability.RecoveryAvailable)
                    {
                        card.Add(ActionButton(
                            CareerTextTable.Get("action.recover"),
                            () => _controller.RecoverCareer(captured.SaveId)));
                    }
                    row.Add(card);
                }

                _routeContent.Add(row);
            }

            _routeContent.Add(SectionTitle(CareerTextTable.Get("career.create_title")));
            var form = FormCard();
            var careerName = Field(CareerTextTable.Get("field.career_name"));
            var playerName = Field(CareerTextTable.Get("field.player_name"));
            var jersey = new IntegerField(CareerTextTable.Get("field.jersey_number"))
            {
                value = 10
            };
            jersey.AddToClassList("field");
            form.Add(careerName);
            form.Add(playerName);
            form.Add(jersey);
            form.Add(ActionButton(
                CareerTextTable.Get("action.create"),
                () => _controller.CreateCareer(
                    careerName.value,
                    playerName.value,
                    jersey.value)));
            _routeContent.Add(form);
        }

        private void RenderOnboarding()
        {
            var snapshot = _controller.Snapshot;
            if (snapshot == null)
            {
                return;
            }

            if (_controller.ShowsInitialResult)
            {
                _routeContent.Add(Title(CareerTextTable.Get("tryout.result_title")));
                _routeContent.Add(Hint(CareerTextTable.Get("tryout.result_hint")));
                AddPlayerOverview(snapshot);
                _routeContent.Add(ActionButton(
                    CareerTextTable.Get("action.continue"),
                    () => _controller.ContinueFromInitialResult()));
                return;
            }

            var stageNumber = snapshot.Onboarding.CurrentStageNumber;
            _routeContent.Add(Title(CareerTextTable.Format(
                "tryout.stage_title",
                stageNumber)));
            _routeContent.Add(Hint(CareerTextTable.Get("tryout.stage_hint")));
            var stage = TryoutCatalogV1.Create().GetStage(stageNumber);
            var row = CardRow();
            foreach (var choice in stage.Choices)
            {
                var capturedId = choice.ChoiceId;
                var card = Card();
                card.Add(CardTitle(CareerTextTable.TryoutChoice(capturedId)));
                card.Add(ActionButton(
                    CareerTextTable.Get("action.confirm"),
                    () => _controller.ConfirmTryout(capturedId)));
                row.Add(card);
            }

            _routeContent.Add(row);
        }

        private void RenderWeekHome()
        {
            var snapshot = _controller.Snapshot;
            var weekPlan = snapshot?.Progression?.WeekPlan;
            if (snapshot == null || weekPlan == null)
            {
                return;
            }

            _routeContent.Add(Title(CareerTextTable.Format(
                "week.title",
                weekPlan.Season,
                weekPlan.Week)));
            AddPlayerOverview(snapshot);
            _routeContent.Add(SectionTitle(CareerTextTable.Get("week.plan")));

            if (weekPlan.Week > 1 &&
                snapshot.Progression.Kind == CareerProgressionKind.Planning)
            {
                _routeContent.Add(Hint(CareerTextTable.Get("week.milestone_complete")));
                if (_controller.SettlementReceipt != null)
                {
                    _routeContent.Add(ActionButton(
                        CareerTextTable.Get("action.last_summary"),
                        () => _controller.OpenLastMatchSummary()));
                }

                return;
            }

            if (!weekPlan.IsConfirmed)
            {
                RenderWeekPlanner();
            }
            else
            {
                _routeContent.Add(Hint(CareerTextTable.Get("week.plan_confirmed")));
                for (var index = 0; index < weekPlan.Slots.Count; index++)
                {
                    var slot = weekPlan.Slots[index];
                    _routeContent.Add(Detail(CareerTextTable.Format(
                        "week.slot",
                        index + 1,
                        slot == null
                            ? CareerTextTable.Get("week.slot_empty")
                            : CareerTextTable.WeekAction(slot.ContentId))));
                }
            }

            var progression = snapshot.Progression;
            if (progression.Kind == CareerProgressionKind.Planned &&
                progression.NextSlotNumber <= 2)
            {
                _routeContent.Add(ActionButton(
                    CareerTextTable.Get("action.execute"),
                    () => _controller.ExecuteNextAction()));
            }

            if (snapshot.PendingMatch != null ||
                progression.Kind == CareerProgressionKind.AwaitingMatch ||
                (progression.Kind == CareerProgressionKind.Planned &&
                 progression.NextSlotNumber == 3))
            {
                _routeContent.Add(ActionButton(
                    CareerTextTable.Get("action.open_match"),
                    () => _controller.OpenPreMatch()));
            }
        }

        private void RenderWeekPlanner()
        {
            _routeContent.Add(Hint(CareerTextTable.Get("week.plan_hint")));
            var catalog = CareerWeekActionCatalogV1.Create();
            _weekActionIds.Clear();
            var labels = new List<string>();
            foreach (var action in catalog.Actions)
            {
                if (action.Kind == CareerWeekActionKind.Match)
                {
                    continue;
                }

                _weekActionIds.Add(action.ContentId);
                labels.Add(CareerTextTable.WeekAction(action.ContentId));
            }

            _firstActionId = SelectExistingOrDefault(_firstActionId, 0);
            _secondActionId = SelectExistingOrDefault(
                _secondActionId,
                _weekActionIds.Count - 1);
            var first = new DropdownField(
                CareerTextTable.Get("field.first_action"),
                labels,
                _weekActionIds.IndexOf(_firstActionId));
            var second = new DropdownField(
                CareerTextTable.Get("field.second_action"),
                labels,
                _weekActionIds.IndexOf(_secondActionId));
            first.AddToClassList("field");
            second.AddToClassList("field");
            first.RegisterValueChangedCallback(evt =>
                _firstActionId = _weekActionIds[labels.IndexOf(evt.newValue)]);
            second.RegisterValueChangedCallback(evt =>
                _secondActionId = _weekActionIds[labels.IndexOf(evt.newValue)]);
            _routeContent.Add(first);
            _routeContent.Add(second);
            _routeContent.Add(Detail(CareerTextTable.Get("week.match_slot")));
            _routeContent.Add(ActionButton(
                CareerTextTable.Get("action.confirm"),
                () => _controller.ConfirmWeekPlan(
                    _firstActionId,
                    _secondActionId)));
        }

        private void RenderEventModal()
        {
            if (_modalLayer == null)
            {
                return;
            }

            _modalContent.Clear();
            if (!_controller.ShowsEventModal)
            {
                _modalLayer.AddToClassList("hidden");
                return;
            }

            _modalLayer.RemoveFromClassList("hidden");
            var pendingEvent = _controller.Snapshot.Progression.PendingEvent;
            _modalContent.Add(Title(CareerTextTable.Get("event.title")));
            _modalContent.Add(CardTitle(CareerTextTable.Format(
                "event.name",
                CareerTextTable.Event(pendingEvent.EventId))));
            _modalContent.Add(Hint(CareerTextTable.Get("event.hint")));
            foreach (var option in pendingEvent.Options)
            {
                var capturedId = option.OptionId;
                _modalContent.Add(ActionButton(
                    CareerTextTable.EventOption(capturedId),
                    () => _controller.ResolveEvent(capturedId)));
            }
        }

        private void RenderPreMatch()
        {
            var snapshot = _controller.Snapshot;
            if (snapshot == null)
            {
                return;
            }

            _routeContent.Add(Title(CareerTextTable.Get("prematch.title")));
            RenderPreMatchPreview();
            if (snapshot.PendingMatch != null)
            {
                _routeContent.Add(Hint(CareerTextTable.Get("prematch.pending_hint")));
                var frozenPriority = snapshot.PendingMatch.PreMatchPriority;
                _routeContent.Add(ActionButton(
                    CareerTextTable.Get("action.retry_match") + " · " +
                    CareerTextTable.MatchPriority(frozenPriority),
                    () => PlayAndSettle(frozenPriority)));
                return;
            }

            _routeContent.Add(Hint(CareerTextTable.Get("prematch.hint")));
            var row = CardRow();
            foreach (CareerMatchPriority priority in Enum.GetValues(
                         typeof(CareerMatchPriority)))
            {
                var captured = priority;
                var card = Card();
                card.Add(CardTitle(CareerTextTable.MatchPriority(priority)));
                card.Add(ActionButton(
                    CareerTextTable.Get("action.confirm"),
                    () => PlayAndSettle(captured)));
                row.Add(card);
            }

            _routeContent.Add(row);
        }

        private void RenderPreMatchPreview()
        {
            var preview = _controller.PreMatchPreview;
            if (preview == null)
            {
                _routeContent.Add(Hint(CareerTextTable.Get(
                    "prematch.preview_unavailable")));
                return;
            }

            _routeContent.Add(Detail(CareerTextTable.Format(
                "prematch.team",
                CareerTextTable.Get("prematch.opponent"),
                CareerTextTable.Team(preview.AwayTeamId))));
            AddRoster("prematch.home_roster", preview.HomePlayers);
            AddRoster("prematch.away_roster", preview.AwayPlayers);
        }

        private void AddRoster(
            string titleId,
            IReadOnlyList<CareerUiPreMatchPlayer> players)
        {
            _routeContent.Add(SectionTitle(CareerTextTable.Get(titleId)));
            foreach (var player in players)
            {
                _routeContent.Add(Detail(CareerTextTable.Format(
                    "prematch.player",
                    player.JerseyNumber,
                    CareerTextTable.Position(player.Position),
                    player.IsProtagonist ? " · 我的球员" : string.Empty)));
            }
        }

        private async void PlayAndSettle(CareerMatchPriority priority)
        {
            if (_controller == null || _lifetime == null)
            {
                return;
            }

            await _controller.PlayAndSettleAsync(priority, _lifetime.Token);
        }

        private void RenderMatchSummary()
        {
            var receipt = _controller.SettlementReceipt;
            if (receipt == null)
            {
                return;
            }

            var summary = receipt.SettlementSummary;
            _routeContent.Add(Title(CareerTextTable.Get("summary.title")));
            _routeContent.Add(CardTitle(CareerTextTable.Get(
                summary.Won ? "summary.win" : "summary.loss")));
            _routeContent.Add(SectionTitle(CareerTextTable.Get("summary.priority")));
            _routeContent.Add(Detail(CareerTextTable.MatchPriority(
                summary.SelectedPriority)));
            _routeContent.Add(Detail(CareerTextTable.Get(
                summary.PriorityExecuted
                    ? "summary.priority_executed"
                    : "summary.priority_not_executed")));
            _routeContent.Add(SectionTitle(CareerTextTable.Get("summary.sets")));
            foreach (var set in summary.Sets)
            {
                var line = new Label(CareerTextTable.Format(
                    "summary.set_score",
                    set.SetNumber,
                    set.HomePoints,
                    set.AwayPoints));
                line.AddToClassList("score");
                _routeContent.Add(line);
            }

            _routeContent.Add(SectionTitle(CareerTextTable.Get("summary.performance")));
            var facts = summary.ProtagonistFacts;
            _routeContent.Add(Detail(CareerTextTable.Format(
                "summary.spike",
                facts.Spike.Attempts,
                facts.Spike.Points,
                facts.Spike.Errors)));
            _routeContent.Add(Detail(CareerTextTable.Format(
                "summary.serve",
                facts.Serve.Attempts,
                facts.Serve.Aces,
                facts.Serve.Errors)));
            _routeContent.Add(Detail(CareerTextTable.Format(
                "summary.reception",
                facts.Reception.Attempts,
                facts.Reception.Perfect,
                facts.Reception.Errors)));

            _routeContent.Add(SectionTitle(CareerTextTable.Get("summary.growth")));
            foreach (var change in summary.GrowthChanges)
            {
                var label = new Label(CareerTextTable.Format(
                    "attribute.growth",
                    CareerTextTable.Attribute(change.Attribute),
                    change.ActualDelta));
                label.AddToClassList("growth-positive");
                _routeContent.Add(label);
            }

            _routeContent.Add(ActionButton(
                CareerTextTable.Get("action.continue"),
                () => _controller.ContinueFromMatchSummary()));
        }

        private void RenderWeekendNotice()
        {
            var receipt = _controller.SettlementReceipt;
            if (receipt == null)
            {
                return;
            }

            var summary = receipt.SettlementSummary;
            _routeContent.Add(Title(CareerTextTable.Get("weekend.title")));
            _routeContent.Add(Hint(CareerTextTable.Get("weekend.hint")));
            _routeContent.Add(Detail(ChangeText(
                CareerTextTable.Get("status.fatigue"),
                summary.MatchFatigueChange,
                summary.WeekendFatigueChange)));
            _routeContent.Add(Detail(ChangeText(
                CareerTextTable.Get("status.mindset"),
                summary.MatchMindsetChange,
                summary.WeekendMindsetChange)));
            _routeContent.Add(Detail(ChangeText(
                CareerTextTable.Get("status.coach_trust"),
                summary.MatchCoachTrustChange,
                summary.WeekendCoachTrustChange)));
            _routeContent.Add(ActionButton(
                CareerTextTable.Get("action.close_notice"),
                () => _controller.CloseWeekendNotice()));
        }

        private void AddPlayerOverview(CareerSaveSnapshot snapshot)
        {
            var player = snapshot.Player;
            if (player == null)
            {
                _routeContent.Add(CardTitle(CareerTextTable.Format(
                    "player.identity",
                    snapshot.PlayerDraft.DisplayName,
                    snapshot.PlayerDraft.JerseyNumber)));
                return;
            }

            _routeContent.Add(CardTitle(CareerTextTable.Format(
                "player.identity",
                player.DisplayName,
                player.JerseyNumber)));
            _routeContent.Add(SectionTitle(CareerTextTable.Get("week.attributes")));
            var grid = new VisualElement();
            grid.AddToClassList("attribute-grid");
            foreach (CareerAttributeKind kind in Enum.GetValues(
                         typeof(CareerAttributeKind)))
            {
                var label = new Label(CareerTextTable.Format(
                    "attribute.value",
                    CareerTextTable.Attribute(kind),
                    player.Attributes.Get(kind).DisplayValue));
                label.AddToClassList("attribute-item");
                grid.Add(label);
            }

            _routeContent.Add(grid);
            _routeContent.Add(SectionTitle(CareerTextTable.Get("week.status")));
            var status = new VisualElement();
            status.AddToClassList("status-row");
            status.Add(StatusPill(
                CareerTextTable.Get("status.fatigue"),
                snapshot.Fatigue));
            status.Add(StatusPill(
                CareerTextTable.Get("status.mindset"),
                snapshot.Mindset));
            status.Add(StatusPill(
                CareerTextTable.Get("status.coach_trust"),
                snapshot.CoachTrust));
            status.Add(StatusPill(
                CareerTextTable.Get("status.potential"),
                CareerTextTable.Potential(snapshot.PotentialGrade)));
            _routeContent.Add(status);
        }

        private void RenderDiagnostics()
        {
            if (!IsDevelopmentBuild())
            {
                return;
            }

            var diagnostics = new VisualElement();
            diagnostics.AddToClassList("diagnostics");
            diagnostics.Add(new Label(CareerTextTable.Get("diagnostics.title")));
            diagnostics.Add(new Label(
                CareerTextTable.Get("diagnostics.route") + ": " + _controller.Route));
            diagnostics.Add(new Label(
                CareerTextTable.Get("diagnostics.feedback") + ": " +
                _controller.FeedbackCode));
            if (_controller.Profile != null)
            {
                diagnostics.Add(new Label(
                    CareerTextTable.Get("diagnostics.profile_id") + ": " +
                    _controller.Profile.ProfileId));
            }

            if (_controller.Snapshot != null)
            {
                diagnostics.Add(new Label(
                    CareerTextTable.Get("diagnostics.save_id") + ": " +
                    _controller.Snapshot.Identity.SaveId));
                diagnostics.Add(new Label(
                    CareerTextTable.Get("diagnostics.revision") + ": " +
                    _controller.Snapshot.Identity.Revision));
            }

            _routeContent.Add(diagnostics);
        }

        private void OnBack()
        {
            _controller?.Back();
        }

        private void OnSave()
        {
            _controller?.SaveNow();
        }

        private string SelectExistingOrDefault(string current, int defaultIndex)
        {
            return _weekActionIds.Contains(current)
                ? current
                : _weekActionIds[Mathf.Clamp(defaultIndex, 0, _weekActionIds.Count - 1)];
        }

        private static string ChangeText(
            string title,
            CareerReasonedIntegerChange match,
            CareerReasonedIntegerChange weekend)
        {
            return CareerTextTable.Format(
                "summary.change",
                title,
                match.ActualDelta + weekend.ActualDelta,
                weekend.NewValue);
        }

        private static bool IsDevelopmentBuild()
        {
#if UNITY_EDITOR
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }

        private void TryApplyChineseSystemFont(VisualElement root)
        {
            if (_runtimeFont != null || root == null)
            {
                return;
            }

            try
            {
                _runtimeFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" },
                    16);
                if (_runtimeFont != null)
                {
                    root.style.unityFont = _runtimeFont;
                }
            }
            catch (Exception)
            {
                _runtimeFont = null;
            }
        }

        private void OnDestroy()
        {
            if (_runtimeFont != null)
            {
                Destroy(_runtimeFont);
                _runtimeFont = null;
            }
        }

        private void ScheduleFocus(VisualElement scope, string preferredName)
        {
            if (scope == null)
            {
                return;
            }

            scope.schedule.Execute(() =>
            {
                var target = string.IsNullOrEmpty(preferredName)
                    ? null
                    : scope.Q<VisualElement>(preferredName);
                if (target == null || !target.enabledInHierarchy || !target.focusable)
                {
                    target = FindFirstFocusable(scope);
                }

                if (target == null || target.panel == null)
                {
                    return;
                }

                target.Focus();
                if (ReferenceEquals(scope, _routeContent))
                {
                    if (!string.IsNullOrEmpty(target.name))
                    {
                        _lastPageFocusName = target.name;
                    }

                    _routeScroll?.ScrollTo(target);
                }
            });
        }

        private static VisualElement FindFirstFocusable(VisualElement root)
        {
            if (root.focusable && root.enabledInHierarchy)
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var result = FindFirstFocusable(root[index]);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static TextField Field(string label)
        {
            var field = new TextField(label);
            field.AddToClassList("field");
            return field;
        }

        private static Label Title(string text)
        {
            var label = new Label(text);
            label.AddToClassList("page-title");
            return label;
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("section-title");
            return label;
        }

        private static Label CardTitle(string text)
        {
            var label = new Label(text);
            label.AddToClassList("card-title");
            return label;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.AddToClassList("hint");
            return label;
        }

        private static Label Detail(string text)
        {
            var label = new Label(text);
            label.AddToClassList("card-detail");
            return label;
        }

        private static Label Warning(string text)
        {
            var label = Detail(text);
            label.AddToClassList("danger-note");
            return label;
        }

        private static VisualElement CardRow()
        {
            var row = new VisualElement();
            row.AddToClassList("card-row");
            return row;
        }

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            return card;
        }

        private static VisualElement FormCard()
        {
            var card = Card();
            card.AddToClassList("form-card");
            return card;
        }

        private Button ActionButton(string text, Action action)
        {
            var button = new Button(action)
            {
                name = "career-action-" + _buttonOrdinal++,
                text = text
            };
            button.SetEnabled(_controller == null || !_controller.IsBusy);
            return button;
        }

        private static Label StatusPill(string label, int? value)
        {
            var text = value.HasValue
                ? CareerTextTable.Format("status.value", label, value.Value)
                : label + "　" + CareerTextTable.Get("status.unknown");
            return StatusPill(label, text, false);
        }

        private static Label StatusPill(string label, string value)
        {
            return StatusPill(label, label + "　" + value, true);
        }

        private static Label StatusPill(string label, string text, bool unused)
        {
            var result = new Label(text);
            result.AddToClassList("status-pill");
            return result;
        }
    }
}
