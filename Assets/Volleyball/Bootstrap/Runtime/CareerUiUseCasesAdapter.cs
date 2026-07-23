using System;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Career.MatchIntegration;
using Volleyball.Career.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Bootstrap
{
    public sealed class CareerUiUseCasesAdapter : ICareerUiUseCases
    {
        private readonly CareerLocalUiWorkflow _local;
        private readonly CareerOnboardingService _onboarding;
        private readonly CareerWeekCommandService _week;
        private readonly CareerPendingMatchService _pending;
        private readonly CareerMatchSettlementService _settlement;
        private readonly CareerWeekActionCatalog _actions;
        private readonly Func<Guid> _newGuid;
        private readonly Func<long> _utcNowMilliseconds;

        public CareerUiUseCasesAdapter(
            CareerLocalUiWorkflow local,
            CareerOnboardingService onboarding,
            CareerWeekCommandService week,
            CareerPendingMatchService pending,
            CareerMatchSettlementService settlement,
            Func<Guid> newGuid = null,
            Func<long> utcNowMilliseconds = null)
        {
            _local = local ?? throw new ArgumentNullException(nameof(local));
            _onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
            _week = week ?? throw new ArgumentNullException(nameof(week));
            _pending = pending ?? throw new ArgumentNullException(nameof(pending));
            _settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            _actions = CareerWeekActionCatalogV1.Create();
            _newGuid = newGuid ?? Guid.NewGuid;
            _utcNowMilliseconds = utcNowMilliseconds ??
                (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public CareerUiUseCaseResult LoadProfiles()
        {
            var result = _local.ListProfiles(Envelope());
            return result.Status == CareerLocalUiWorkflowStatus.Completed &&
                   result.Catalog != null
                ? CareerUiUseCaseResult.ForProfiles(
                    result.Catalog.Profiles,
                    Code(result.PrimaryPersistenceKind))
                : CareerUiUseCaseResult.Failure(LocalCode(result));
        }

        public CareerUiUseCaseResult CreateProfile(string displayName)
        {
            try
            {
                var result = _local.CreateProfile(new CreateLocalProfileUiCommand(
                    Envelope(),
                    new ProfileId(NewGuid()),
                    displayName));
                return result.Status == CareerLocalUiWorkflowStatus.Completed &&
                       result.Profile != null && result.Catalog != null
                    ? CareerUiUseCaseResult.ForProfile(
                        result.Profile,
                        result.Catalog.Profiles,
                        Code(result.PrimaryPersistenceKind))
                    : CareerUiUseCaseResult.Failure(LocalCode(result));
            }
            catch (ArgumentException)
            {
                return CareerUiUseCaseResult.Failure("invalid_profile");
            }
        }

        public CareerUiUseCaseResult LoadProfile(ProfileId profileId)
        {
            var result = _local.LoadProfile(new LocalProfileUiCommand(Envelope(), profileId));
            return result.Status == CareerLocalUiWorkflowStatus.Completed && result.Profile != null
                ? CareerUiUseCaseResult.ForProfile(
                    result.Profile,
                    code: Code(result.PrimaryPersistenceKind))
                : CareerUiUseCaseResult.Failure(LocalCode(result));
        }

        public CareerUiUseCaseResult LoadCareer(ProfileId profileId, SaveId saveId)
        {
            var result = _local.LoadCareer(new LocalCareerUiCommand(
                Envelope(),
                profileId,
                saveId));
            return result.Status == CareerLocalUiWorkflowStatus.Completed && result.Snapshot != null
                ? CareerUiUseCaseResult.ForCareer(
                    result.Snapshot,
                    Code(result.PrimaryPersistenceKind))
                : CareerUiUseCaseResult.Failure(LocalCode(result));
        }

        public CareerUiUseCaseResult CreateCareer(
            ProfileId profileId,
            string careerName,
            string playerName,
            int jerseyNumber)
        {
            var saveId = new SaveId(NewGuid());
            var result = _onboarding.CreateCareer(new CreateCareerCommand(
                profileId,
                saveId,
                new LineageId(NewGuid()),
                "career.player." + NewGuid().ToString("N"),
                careerName,
                playerName,
                jerseyNumber,
                new[]
                {
                    new OccurrenceId(NewGuid()),
                    new OccurrenceId(NewGuid()),
                    new OccurrenceId(NewGuid())
                },
                new OperationId(NewGuid()),
                Now()));
            if (!IsApplied(result.Status) || result.Snapshot == null)
            {
                return CareerUiUseCaseResult.Failure(
                    "create_career_" + result.Status.ToString().ToLowerInvariant());
            }

            var indexed = RefreshIndex(profileId);
            return CareerUiUseCaseResult.ForCareer(
                result.Snapshot,
                indexed ? "career_created" : "career_created_index_warning");
        }

        public CareerUiUseCaseResult ConfirmTryout(
            CareerSaveSnapshot snapshot,
            string choiceId)
        {
            if (snapshot == null)
            {
                return CareerUiUseCaseResult.Failure("missing_career");
            }

            var stage = snapshot.Onboarding.NextStageNumber;
            var enrollment = stage == 3
                ? new TryoutEnrollmentIds(
                    new WeekPlanId(NewGuid()),
                    new SlotActionId(NewGuid()),
                    new OccurrenceId(NewGuid()))
                : null;
            var result = _onboarding.ConfirmTryoutStage(new ConfirmTryoutStageCommand(
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId,
                snapshot.Identity.VersionToken,
                new OperationId(NewGuid()),
                Now(),
                stage,
                choiceId,
                enrollment));
            if (!IsApplied(result.Status) || result.Snapshot == null)
            {
                return CareerUiUseCaseResult.Failure(
                    "tryout_" + result.Status.ToString().ToLowerInvariant(),
                    result.Snapshot);
            }

            if (result.Snapshot.Onboarding.IsFormallyEnrolled)
            {
                RefreshIndex(snapshot.Identity.ProfileId);
            }

            return CareerUiUseCaseResult.ForCareer(result.Snapshot, "tryout_saved");
        }

        public CareerUiUseCaseResult ConfirmWeekPlan(
            CareerSaveSnapshot snapshot,
            string firstContentId,
            string secondContentId)
        {
            if (snapshot?.Progression?.WeekPlan == null)
            {
                return CareerUiUseCaseResult.Failure("missing_week_plan");
            }

            var first = Action(firstContentId);
            var second = Action(secondContentId);
            var existing = snapshot.Progression.WeekPlan;
            if (first == null || second == null || existing.Slots.Count != 3 ||
                existing.Slots[2] == null || !existing.Slots[2].IsMatch)
            {
                return CareerUiUseCaseResult.Failure("invalid_week_plan");
            }

            var candidate = new CareerWeekPlanState(
                existing.PlanId,
                existing.Season,
                existing.Week,
                new[]
                {
                    new CareerWeekActionState(
                        new SlotActionId(NewGuid()),
                        new OccurrenceId(NewGuid()),
                        first.Kind,
                        first.ContentId),
                    new CareerWeekActionState(
                        new SlotActionId(NewGuid()),
                        new OccurrenceId(NewGuid()),
                        second.Kind,
                        second.ContentId),
                    existing.Slots[2]
                },
                true);
            var result = _week.ConfirmWeekPlan(new ConfirmWeekPlanCommand(
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId,
                snapshot.Identity.VersionToken,
                new OperationId(NewGuid()),
                Now(),
                candidate));
            return WeekResult(result, "week_plan_saved");
        }

        public CareerUiUseCaseResult ExecuteNextAction(CareerSaveSnapshot snapshot)
        {
            if (snapshot?.Progression?.Kind != CareerProgressionKind.Planned ||
                snapshot.Progression.WeekPlan == null)
            {
                return CareerUiUseCaseResult.Failure("week_action_not_ready");
            }

            var slotNumber = snapshot.Progression.NextSlotNumber;
            if (slotNumber < 1 || slotNumber > 2)
            {
                return CareerUiUseCaseResult.Failure("week_action_not_free_slot");
            }

            var action = snapshot.Progression.WeekPlan.Slots[slotNumber - 1];
            var result = _week.ExecuteWeekAction(new ExecuteWeekActionCommand(
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId,
                snapshot.Identity.VersionToken,
                new OperationId(NewGuid()),
                Now(),
                snapshot.Progression.WeekPlan.PlanId,
                slotNumber,
                action.SlotActionId,
                action.OccurrenceId,
                action.ContentId,
                slotNumber == 1 ? new OccurrenceId(NewGuid()) : (OccurrenceId?)null));
            return WeekResult(result, "week_action_saved");
        }

        public CareerUiUseCaseResult ResolveEvent(
            CareerSaveSnapshot snapshot,
            string optionId)
        {
            var pendingEvent = snapshot?.Progression?.PendingEvent;
            if (pendingEvent == null)
            {
                return CareerUiUseCaseResult.Failure("missing_event");
            }

            var result = _week.ResolveEventChoice(new ResolveEventChoiceCommand(
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId,
                snapshot.Identity.VersionToken,
                new OperationId(NewGuid()),
                Now(),
                pendingEvent.SourceWeekPlanId,
                pendingEvent.SourceSlotActionId,
                pendingEvent.SourceActionOccurrenceId,
                pendingEvent.EventId,
                pendingEvent.OccurrenceId,
                optionId));
            return WeekResult(result, "event_saved");
        }

        public CareerUiPreMatchPreview GetPreMatchPreview(CareerSaveSnapshot snapshot)
        {
            if (snapshot?.Player == null || !snapshot.TeamId.HasValue)
            {
                return null;
            }

            var pending = snapshot.PendingMatch;
            var versions = pending == null
                ? new CareerMatchVersions(
                    snapshot.Versions.ContractVersion,
                    snapshot.Versions.ContentVersion,
                    snapshot.Versions.RulesetVersion,
                    snapshot.Versions.CareerRandomAlgorithmVersion,
                    null,
                    null)
                : new CareerMatchVersions(
                    pending.Versions.ContractVersion,
                    pending.Versions.ContentVersion,
                    pending.Versions.RulesetVersion,
                    pending.Versions.CareerRandomAlgorithmVersion,
                    pending.Versions.MatchSimulationVersion,
                    pending.Versions.MatchRandomAlgorithmVersion);
            var launch = new CareerFirstMatchLaunchFactoryV1().Create(
                new CareerFirstMatchLaunchRequest(
                    versions,
                    pending?.SessionId ?? Guid.Parse(
                        "88888888-8888-4888-8888-888888888888"),
                    pending?.MatchSeed ?? 0u,
                    pending?.HomeTeamId ?? snapshot.TeamId.Value,
                    snapshot.Player.PlayerId,
                    snapshot.Player.JerseyNumber,
                    snapshot.Fatigue ?? 0,
                    snapshot.Player.Attributes,
                    pending?.PreMatchPriority ?? CareerMatchPriority.AttackFirst));
            var home = launch.Teams[0];
            var away = launch.Teams[1];
            return new CareerUiPreMatchPreview(
                home.TeamId.Value,
                away.TeamId.Value,
                PreviewPlayers(home.Players, snapshot.Player.PlayerId),
                PreviewPlayers(away.Players, snapshot.Player.PlayerId));
        }

        public async Task<CareerUiUseCaseResult> PlayAndSettleAsync(
            CareerSaveSnapshot snapshot,
            CareerMatchPriority priority,
            CancellationToken cancellationToken)
        {
            if (snapshot == null)
            {
                return CareerUiUseCaseResult.Failure("missing_career");
            }

            CareerPendingMatchFlowResult execution;
            if (snapshot.PendingMatch == null)
            {
                var plan = snapshot.Progression.WeekPlan;
                var match = plan?.Slots.Count == 3 ? plan.Slots[2] : null;
                if (match == null || !match.IsMatch)
                {
                    return CareerUiUseCaseResult.Failure("match_not_ready");
                }

                execution = await _pending.CreateAndExecuteAsync(
                    new CreatePendingMatchCommand(
                        snapshot.Identity.ProfileId,
                        snapshot.Identity.SaveId,
                        snapshot.Identity.VersionToken,
                        new OperationId(NewGuid()),
                        Now(),
                        NewGuid(),
                        plan.PlanId,
                        match.SlotActionId,
                        match.OccurrenceId,
                        priority),
                    cancellationToken);
            }
            else
            {
                execution = await _pending.RetryExecutionAsync(
                    new RetryPendingMatchExecutionCommand(
                        snapshot.Identity.ProfileId,
                        snapshot.Identity.SaveId,
                        snapshot.PendingMatch.SessionId),
                    cancellationToken);
            }

            if (execution.Status != CareerPendingMatchFlowStatus.AwaitingSettlement ||
                execution.Snapshot == null || !execution.SessionId.HasValue ||
                execution.CanonicalContextUtf8 == null ||
                execution.CanonicalResultUtf8 == null)
            {
                return CareerUiUseCaseResult.Failure(
                    "match_" + execution.Status.ToString().ToLowerInvariant() +
                    (string.IsNullOrWhiteSpace(execution.FailureCode)
                        ? string.Empty
                        : "_" + execution.FailureCode),
                    execution.Snapshot);
            }

            var settled = _settlement.Settle(new SettleCareerMatchCommand(
                execution.Snapshot.Identity.ProfileId,
                execution.Snapshot.Identity.SaveId,
                execution.Snapshot.Identity.VersionToken,
                Now(),
                execution.SessionId.Value,
                execution.CanonicalContextUtf8,
                execution.CanonicalResultUtf8));
            if ((settled.Status != CareerMatchSettlementStatus.Settled &&
                 settled.Status != CareerMatchSettlementStatus.Existing) ||
                settled.Snapshot == null || settled.SettlementReceipt == null)
            {
                return CareerUiUseCaseResult.Failure(
                    "settlement_" + settled.Status.ToString().ToLowerInvariant() +
                    (string.IsNullOrWhiteSpace(settled.FailureCode)
                        ? string.Empty
                        : "_" + settled.FailureCode),
                    settled.Snapshot ?? execution.Snapshot);
            }

            var indexed = RefreshIndex(snapshot.Identity.ProfileId);
            return CareerUiUseCaseResult.ForSettlement(
                settled.Snapshot,
                settled.SettlementReceipt,
                indexed ? "match_settled" : "match_settled_index_warning");
        }

        public CareerUiUseCaseResult SaveNow(CareerSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return CareerUiUseCaseResult.Failure("missing_career");
            }

            var result = _local.SaveNow(new LocalCareerUiCommand(
                Envelope(),
                snapshot.Identity.ProfileId,
                snapshot.Identity.SaveId));
            return result.Status == CareerLocalUiWorkflowStatus.UpToDate &&
                   result.Snapshot != null
                ? CareerUiUseCaseResult.ForCareer(result.Snapshot, "up_to_date")
                : CareerUiUseCaseResult.Failure(LocalCode(result), result.Snapshot);
        }

        private CareerWeekActionContentDefinition Action(string contentId)
        {
            try
            {
                return _actions.Find(contentId);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static CareerUiPreMatchPlayer[] PreviewPlayers(
            System.Collections.Generic.IReadOnlyList<CareerMatchPlayerLaunch> players,
            PlayerId protagonistPlayerId)
        {
            var result = new CareerUiPreMatchPlayer[players.Count];
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                result[index] = new CareerUiPreMatchPlayer(
                    player.JerseyNumber,
                    player.Position,
                    player.PlayerId.Equals(protagonistPlayerId));
            }

            return result;
        }

        private CareerUiUseCaseResult WeekResult(
            CareerWeekCommandResult result,
            string successCode)
        {
            return IsApplied(result.Status) && result.Snapshot != null
                ? CareerUiUseCaseResult.ForCareer(result.Snapshot, successCode)
                : CareerUiUseCaseResult.Failure(
                    "week_" + result.Status.ToString().ToLowerInvariant(),
                    result.Snapshot);
        }

        private bool RefreshIndex(ProfileId profileId)
        {
            try
            {
                var result = _local.RefreshCareerIndex(new LocalProfileUiCommand(
                    Envelope(),
                    profileId));
                return result.Status == CareerLocalUiWorkflowStatus.Completed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private CareerUiCommandEnvelope Envelope()
        {
            return new CareerUiCommandEnvelope(new OperationId(NewGuid()), Now());
        }

        private Guid NewGuid()
        {
            var value = _newGuid();
            if (value == Guid.Empty)
            {
                throw new InvalidOperationException("The UI identity source returned an empty GUID.");
            }

            return value;
        }

        private long Now()
        {
            var value = _utcNowMilliseconds();
            if (value < 0 || value > 9007199254740991L)
            {
                throw new InvalidOperationException("The UI clock returned an invalid timestamp.");
            }

            return value;
        }

        private static bool IsApplied(CareerApplicationStatus status)
        {
            return status == CareerApplicationStatus.Applied ||
                   status == CareerApplicationStatus.Existing;
        }

        private static string Code(PersistenceResultKind kind)
        {
            return "persistence_" + kind.ToString().ToLowerInvariant();
        }

        private static string LocalCode(CareerLocalUiWorkflowResult result)
        {
            return result == null
                ? "local_unknown"
                : "local_" + result.Status.ToString().ToLowerInvariant() + "_" +
                  result.PrimaryPersistenceKind.ToString().ToLowerInvariant();
        }
    }
}
