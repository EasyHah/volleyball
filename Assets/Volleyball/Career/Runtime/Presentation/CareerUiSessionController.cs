using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Presentation
{
    public enum CareerUiRoute
    {
        ProfileHub = 0,
        CareerHub = 1,
        Onboarding = 2,
        WeekHome = 3,
        PreMatch = 4,
        MatchSummary = 5,
        WeekendNotice = 6
    }

    public enum CareerUiSaveState
    {
        Ready = 0,
        Saving = 1,
        Saved = 2,
        Failed = 3,
        ReadOnly = 4
    }

    public sealed class CareerUiSessionController
    {
        private readonly ICareerUiUseCases _useCases;

        public CareerUiSessionController(ICareerUiUseCases useCases)
        {
            _useCases = useCases ?? throw new ArgumentNullException(nameof(useCases));
            Route = CareerUiRoute.ProfileHub;
            SaveState = CareerUiSaveState.Ready;
            Profiles = Array.Empty<LocalProfileCatalogEntry>();
            FeedbackCode = "ready";
        }

        public event Action Changed;

        public CareerUiRoute Route { get; private set; }
        public CareerUiSaveState SaveState { get; private set; }
        public IReadOnlyList<LocalProfileCatalogEntry> Profiles { get; private set; }
        public LocalPlayerProfile Profile { get; private set; }
        public CareerSaveSnapshot Snapshot { get; private set; }
        public CareerSettlementReceipt SettlementReceipt { get; private set; }
        public CareerUiPreMatchPreview PreMatchPreview { get; private set; }
        public string FeedbackCode { get; private set; }
        public bool IsBusy { get; private set; }
        public bool ShowsInitialResult { get; private set; }
        public bool ShowsEventModal =>
            Snapshot?.Progression?.Kind == CareerProgressionKind.AwaitingEventChoice;

        public void Initialize()
        {
            if (IsBusy)
            {
                return;
            }

            var result = _useCases.LoadProfiles();
            if (result.Succeeded)
            {
                Profiles = result.Profiles;
                FeedbackCode = result.Code;
                SaveState = CareerUiSaveState.Ready;
            }
            else
            {
                Fail(result.Code, readOnly: true);
            }

            Notify();
        }

        public bool CreateProfile(string displayName)
        {
            if (!BeginWrite())
            {
                return false;
            }

            var result = ExecuteWrite(
                () => _useCases.CreateProfile(displayName),
                "create_profile");
            EndWrite(result);
            if (result.Succeeded)
            {
                Profile = result.Profile;
                Profiles = result.Profiles;
                Route = CareerUiRoute.CareerHub;
            }

            Notify();
            return result.Succeeded;
        }

        public bool SelectProfile(ProfileId profileId)
        {
            if (IsBusy)
            {
                return false;
            }

            var result = _useCases.LoadProfile(profileId);
            if (!result.Succeeded)
            {
                Fail(result.Code, readOnly: true);
                Notify();
                return false;
            }

            Profile = result.Profile;
            Snapshot = null;
            SettlementReceipt = null;
            PreMatchPreview = null;
            ShowsInitialResult = false;
            FeedbackCode = result.Code;
            SaveState = CareerUiSaveState.Ready;
            Route = CareerUiRoute.CareerHub;
            Notify();
            return true;
        }

        public bool SelectCareer(ProfileId profileId, SaveId saveId)
        {
            if (IsBusy)
            {
                return false;
            }

            var result = _useCases.LoadCareer(profileId, saveId);
            if (!ApplyAuthority(result))
            {
                Notify();
                return false;
            }

            SettlementReceipt = LastReceipt(Snapshot);
            PreMatchPreview = null;
            ShowsInitialResult = false;
            Route = AuthorityRoute(Snapshot);
            if (Route == CareerUiRoute.PreMatch)
            {
                LoadPreMatchPreview();
            }
            Notify();
            return true;
        }

        public bool CreateCareer(
            string careerName,
            string playerName,
            int jerseyNumber)
        {
            if (Profile == null || !BeginWrite())
            {
                return false;
            }

            var result = ExecuteWrite(
                () => _useCases.CreateCareer(
                    Profile.ProfileId,
                    careerName,
                    playerName,
                    jerseyNumber),
                "create_career");
            EndWrite(result);
            if (result.Succeeded)
            {
                Snapshot = result.Snapshot;
                Route = CareerUiRoute.Onboarding;
                ShowsInitialResult = false;
            }
            else
            {
                AdoptAuthoritativeFailure(result);
            }

            Notify();
            return result.Succeeded;
        }

        public bool ConfirmTryout(string choiceId)
        {
            if (Snapshot == null || !BeginWrite())
            {
                return false;
            }

            var result = ExecuteWrite(
                () => _useCases.ConfirmTryout(Snapshot, choiceId),
                "confirm_tryout");
            EndWrite(result);
            if (result.Succeeded)
            {
                Snapshot = result.Snapshot;
                Route = CareerUiRoute.Onboarding;
                ShowsInitialResult = Snapshot.Onboarding.IsFormallyEnrolled;
            }
            else
            {
                AdoptAuthoritativeFailure(result);
            }

            Notify();
            return result.Succeeded;
        }

        public bool ContinueFromInitialResult()
        {
            if (!ShowsInitialResult || Snapshot == null ||
                !Snapshot.Onboarding.IsFormallyEnrolled)
            {
                return false;
            }

            ShowsInitialResult = false;
            Route = CareerUiRoute.WeekHome;
            FeedbackCode = "navigation_only";
            Notify();
            return true;
        }

        public bool ConfirmWeekPlan(string firstContentId, string secondContentId)
        {
            return ApplyCareerWrite(() => _useCases.ConfirmWeekPlan(
                Snapshot,
                firstContentId,
                secondContentId));
        }

        public bool ExecuteNextAction()
        {
            return ApplyCareerWrite(() => _useCases.ExecuteNextAction(Snapshot));
        }

        public bool ResolveEvent(string optionId)
        {
            return ApplyCareerWrite(() => _useCases.ResolveEvent(Snapshot, optionId));
        }

        public bool OpenPreMatch()
        {
            if (Snapshot == null || IsBusy || !IsMatchReady(Snapshot))
            {
                return false;
            }

            Route = CareerUiRoute.PreMatch;
            LoadPreMatchPreview();
            FeedbackCode = Snapshot.PendingMatch == null
                ? "choose_match_priority"
                : "pending_match_retry";
            Notify();
            return true;
        }

        public async Task<bool> PlayAndSettleAsync(
            CareerMatchPriority priority,
            CancellationToken cancellationToken)
        {
            if (Route != CareerUiRoute.PreMatch || Snapshot == null || !BeginWrite())
            {
                return false;
            }

            var receiptCountBeforePlay = Snapshot.SettlementReceipts.Count;
            CareerUiUseCaseResult result;
            try
            {
                result = await _useCases.PlayAndSettleAsync(
                    Snapshot,
                    priority,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                result = FailureWithReloadedAuthority("match_cancelled");
            }
            catch (Exception exception)
            {
                result = FailureWithReloadedAuthority(
                    "match_exception_" + exception.GetType().Name);
            }

            EndWrite(result);
            if (result.Succeeded && result.SettlementReceipt != null)
            {
                Snapshot = result.Snapshot;
                SettlementReceipt = result.SettlementReceipt;
                PreMatchPreview = null;
                Route = CareerUiRoute.MatchSummary;
            }
            else if (result.Snapshot != null)
            {
                Snapshot = result.Snapshot;
                SettlementReceipt = LastReceipt(Snapshot);
                if (SettlementReceipt != null &&
                    Snapshot.SettlementReceipts.Count > receiptCountBeforePlay)
                {
                    PreMatchPreview = null;
                    Route = CareerUiRoute.MatchSummary;
                }
                else
                {
                    Route = AuthorityRoute(Snapshot);
                    if (Route == CareerUiRoute.PreMatch)
                    {
                        LoadPreMatchPreview();
                    }
                }
            }

            Notify();
            return result.Succeeded;
        }

        public bool ContinueFromMatchSummary()
        {
            if (Route != CareerUiRoute.MatchSummary || SettlementReceipt == null)
            {
                return false;
            }

            Route = CareerUiRoute.WeekendNotice;
            FeedbackCode = "navigation_only";
            Notify();
            return true;
        }

        public bool OpenLastMatchSummary()
        {
            if (Route != CareerUiRoute.WeekHome || SettlementReceipt == null || IsBusy)
            {
                return false;
            }

            Route = CareerUiRoute.MatchSummary;
            FeedbackCode = "navigation_only";
            Notify();
            return true;
        }

        public bool CloseWeekendNotice()
        {
            if (Route != CareerUiRoute.WeekendNotice || Snapshot == null)
            {
                return false;
            }

            Route = CareerUiRoute.WeekHome;
            FeedbackCode = "navigation_only";
            Notify();
            return true;
        }

        public bool SaveNow()
        {
            if (Snapshot == null || !BeginWrite())
            {
                return false;
            }

            var result = ExecuteWrite(
                () => _useCases.SaveNow(Snapshot),
                "save_now");
            EndWrite(result);
            if (result.Succeeded && result.Snapshot != null)
            {
                Snapshot = result.Snapshot;
            }
            else
            {
                AdoptAuthoritativeFailure(result);
            }

            Notify();
            return result.Succeeded;
        }

        public bool Back()
        {
            if (IsBusy)
            {
                FeedbackCode = "operation_in_progress";
                Notify();
                return false;
            }

            switch (Route)
            {
                case CareerUiRoute.CareerHub:
                    Profile = null;
                    Snapshot = null;
                    SettlementReceipt = null;
                    PreMatchPreview = null;
                    Route = CareerUiRoute.ProfileHub;
                    break;
                case CareerUiRoute.Onboarding:
                    ReloadProfile();
                    Snapshot = null;
                    SettlementReceipt = null;
                    PreMatchPreview = null;
                    ShowsInitialResult = false;
                    Route = CareerUiRoute.CareerHub;
                    break;
                case CareerUiRoute.WeekHome:
                    if (!CanLeaveWeekHome())
                    {
                        FeedbackCode = "week_plan_requires_completion";
                        Notify();
                        return false;
                    }

                    ReloadProfile();
                    Snapshot = null;
                    SettlementReceipt = null;
                    PreMatchPreview = null;
                    Route = CareerUiRoute.CareerHub;
                    break;
                case CareerUiRoute.PreMatch:
                    if (Snapshot?.PendingMatch != null)
                    {
                        FeedbackCode = "pending_match_requires_retry";
                        Notify();
                        return false;
                    }

                    Route = CareerUiRoute.WeekHome;
                    break;
                case CareerUiRoute.MatchSummary:
                case CareerUiRoute.WeekendNotice:
                    FeedbackCode = "summary_requires_confirmation";
                    Notify();
                    return false;
                default:
                    return false;
            }

            FeedbackCode = "back";
            Notify();
            return true;
        }

        private bool ApplyCareerWrite(Func<CareerUiUseCaseResult> operation)
        {
            if (Snapshot == null || operation == null || !BeginWrite())
            {
                return false;
            }

            var result = ExecuteWrite(operation, "career_write");
            EndWrite(result);
            if (result.Succeeded)
            {
                Snapshot = result.Snapshot;
                Route = AuthorityRoute(Snapshot);
                if (Route == CareerUiRoute.PreMatch)
                {
                    LoadPreMatchPreview();
                }
            }
            else
            {
                AdoptAuthoritativeFailure(result);
            }

            Notify();
            return result.Succeeded;
        }

        private bool ApplyAuthority(CareerUiUseCaseResult result)
        {
            if (!result.Succeeded || result.Snapshot == null)
            {
                Fail(result.Code, readOnly: true);
                return false;
            }

            Snapshot = result.Snapshot;
            SaveState = CareerUiSaveState.Saved;
            FeedbackCode = result.Code;
            return true;
        }

        private bool BeginWrite()
        {
            if (IsBusy)
            {
                FeedbackCode = "operation_in_progress";
                Notify();
                return false;
            }

            IsBusy = true;
            SaveState = CareerUiSaveState.Saving;
            FeedbackCode = "saving";
            Notify();
            return true;
        }

        private void EndWrite(CareerUiUseCaseResult result)
        {
            IsBusy = false;
            if (result != null && result.Succeeded)
            {
                SaveState = CareerUiSaveState.Saved;
                FeedbackCode = result.Code;
            }
            else
            {
                Fail(result?.Code ?? "unknown_failure", readOnly: false);
            }
        }

        private CareerUiUseCaseResult ExecuteWrite(
            Func<CareerUiUseCaseResult> operation,
            string exceptionPrefix)
        {
            try
            {
                return operation() ?? CareerUiUseCaseResult.Failure(
                    exceptionPrefix + "_null_result",
                    Snapshot);
            }
            catch (Exception exception)
            {
                return CareerUiUseCaseResult.Failure(
                    exceptionPrefix + "_exception_" + exception.GetType().Name,
                    Snapshot);
            }
        }

        private void AdoptAuthoritativeFailure(CareerUiUseCaseResult result)
        {
            if (result?.Snapshot == null)
            {
                return;
            }

            Snapshot = result.Snapshot;
            Route = AuthorityRoute(Snapshot);
            if (Route == CareerUiRoute.PreMatch)
            {
                LoadPreMatchPreview();
            }
        }

        private void LoadPreMatchPreview()
        {
            try
            {
                PreMatchPreview = _useCases.GetPreMatchPreview(Snapshot);
            }
            catch (Exception)
            {
                PreMatchPreview = null;
            }
        }

        private CareerUiUseCaseResult FailureWithReloadedAuthority(string code)
        {
            try
            {
                var identity = Snapshot?.Identity;
                if (identity == null)
                {
                    return CareerUiUseCaseResult.Failure(code);
                }

                var loaded = _useCases.LoadCareer(identity.ProfileId, identity.SaveId);
                return loaded != null && loaded.Succeeded && loaded.Snapshot != null
                    ? CareerUiUseCaseResult.Failure(code, loaded.Snapshot)
                    : CareerUiUseCaseResult.Failure(code);
            }
            catch (Exception)
            {
                return CareerUiUseCaseResult.Failure(code);
            }
        }

        private static CareerSettlementReceipt LastReceipt(CareerSaveSnapshot snapshot)
        {
            var receipts = snapshot?.SettlementReceipts;
            return receipts == null || receipts.Count == 0
                ? null
                : receipts[receipts.Count - 1];
        }

        private void Fail(string code, bool readOnly)
        {
            IsBusy = false;
            SaveState = readOnly ? CareerUiSaveState.ReadOnly : CareerUiSaveState.Failed;
            FeedbackCode = string.IsNullOrWhiteSpace(code) ? "unknown_failure" : code;
        }

        private bool CanLeaveWeekHome()
        {
            return Snapshot?.Progression?.Kind == CareerProgressionKind.Planning &&
                   Snapshot.Progression.WeekPlan != null &&
                   Snapshot.Progression.WeekPlan.Week >= 2;
        }

        private void ReloadProfile()
        {
            if (Profile == null)
            {
                return;
            }

            var result = _useCases.LoadProfile(Profile.ProfileId);
            if (result.Succeeded && result.Profile != null)
            {
                Profile = result.Profile;
            }
        }

        private static bool IsMatchReady(CareerSaveSnapshot snapshot)
        {
            if (snapshot.PendingMatch != null ||
                snapshot.Progression.Kind == CareerProgressionKind.AwaitingMatch)
            {
                return true;
            }

            return snapshot.Progression.Kind == CareerProgressionKind.Planned &&
                   snapshot.Progression.NextSlotNumber == 3;
        }

        private static CareerUiRoute AuthorityRoute(CareerSaveSnapshot snapshot)
        {
            if (snapshot.Progression.Kind == CareerProgressionKind.Tryout)
            {
                return CareerUiRoute.Onboarding;
            }

            if (snapshot.PendingMatch != null ||
                snapshot.Progression.Kind == CareerProgressionKind.AwaitingMatch)
            {
                return CareerUiRoute.PreMatch;
            }

            return CareerUiRoute.WeekHome;
        }

        private void Notify()
        {
            Changed?.Invoke();
        }
    }
}
