using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Application
{
    public sealed class CareerPendingMatchService
    {
        private const string MatchSeedStream = "match_seed";
        private const long MaximumIJsonSafeInteger = 9007199254740991L;
        private readonly ICareerSaveRepository _repository;
        private readonly IDeterministicCareerRandom _random;
        private readonly ICareerFirstMatchLaunchFactory _launchFactory;
        private readonly ICareerMatchExecutor _executor;

        public CareerPendingMatchService(
            ICareerSaveRepository repository,
            IDeterministicCareerRandom random,
            ICareerFirstMatchLaunchFactory launchFactory,
            ICareerMatchExecutor executor)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _launchFactory = launchFactory ?? throw new ArgumentNullException(nameof(launchFactory));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public async Task<CareerPendingMatchFlowResult> CreateAndExecuteAsync(
            CreatePendingMatchCommand command,
            CancellationToken cancellationToken)
        {
            CareerSaveSnapshot committedSnapshot = null;
            PersistenceResultKind? committedKind = null;
            try
            {
                if (!IsValid(command))
                {
                    return Failure(CareerPendingMatchFlowStatus.ValidationFailed, "invalid_command");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var fingerprint = CareerOperationFingerprintV2.Hash(command);
                var loaded = _repository.Load(command.ProfileId, command.SaveId);
                if (!HasSnapshot(loaded))
                {
                    return LoadFailure(loaded);
                }

                var snapshot = loaded.Snapshot;
                if (!MatchesIdentity(snapshot, command.ProfileId, command.SaveId))
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.ValidationFailed,
                        "loaded_identity_mismatch",
                        loaded.Kind);
                }

                var lookup = new OperationReceiptIndex(snapshot.OperationReceipts)
                    .Find(command.OperationId, fingerprint);
                if (lookup.Kind == OperationReceiptLookupKind.Existing)
                {
                    return Existing(snapshot, loaded.Kind, lookup.Receipt);
                }

                if (lookup.Kind == OperationReceiptLookupKind.Conflict)
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.OperationConflict,
                        "operation_fingerprint_conflict",
                        loaded.Kind,
                        snapshot,
                        lookup.Receipt);
                }

                if (!snapshot.Identity.VersionToken.Equals(command.ExpectedVersionToken))
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.RevisionConflict,
                        "expected_version_mismatch",
                        loaded.Kind,
                        snapshot);
                }

                CareerWeekActionState matchAction;
                if (!IsMatchReady(snapshot, command, out matchAction))
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.InvalidState,
                        "not_first_week_match_ready",
                        loaded.Kind,
                        snapshot);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var seedValue = _random.NextInt64(
                    new CareerRandomRequest(
                        snapshot.Versions.CareerRandomAlgorithmVersion,
                        snapshot.CareerSeed,
                        MatchSeedStream,
                        snapshot.Progression.WeekPlan.Season,
                        snapshot.Progression.WeekPlan.Week,
                        matchAction.ContentId,
                        new OccurrenceId(command.SessionId),
                        0),
                    0,
                    4294967296L);
                if (seedValue < 0 || seedValue > uint.MaxValue)
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.ValidationFailed,
                        "random_seed_out_of_range",
                        loaded.Kind,
                        snapshot,
                        null,
                        CareerPendingMatchFailureKind.Dependency);
                }

                var versions = new CareerMatchVersions(
                    snapshot.Versions.ContractVersion,
                    snapshot.Versions.ContentVersion,
                    snapshot.Versions.RulesetVersion,
                    snapshot.Versions.CareerRandomAlgorithmVersion,
                    null,
                    null);
                var launch = _launchFactory.Create(new CareerFirstMatchLaunchRequest(
                    versions,
                    command.SessionId,
                    (uint)seedValue,
                    snapshot.TeamId.Value,
                    snapshot.Player.PlayerId,
                    snapshot.Player.JerseyNumber,
                    snapshot.Fatigue.Value,
                    snapshot.Player.Attributes,
                    command.PreMatchPriority));
                ValidateLaunch(launch, command, snapshot, matchAction, (uint)seedValue);
                var encoded = _executor.Encode(launch);
                ValidateEncoded(encoded, command.SessionId);

                var appliedRevision = checked(snapshot.Identity.Revision + 1);
                var lifecycleVersions = new CareerMatchLifecycleVersions(
                    launch.Versions.ContractVersion,
                    launch.Versions.ContentVersion,
                    launch.Versions.RulesetVersion,
                    launch.Versions.CareerRandomAlgorithmVersion,
                    launch.Versions.MatchSimulationVersion,
                    launch.Versions.MatchRandomAlgorithmVersion);
                var frozen = snapshot.TrainingEmphases.Freeze()
                    .Select(item => new FrozenCareerTrainingEmphasis(
                        item.Direction,
                        item.SourceSlotActionIds,
                        item.TotalBonusBasisPoints))
                    .ToArray();
                var pending = new PendingCareerMatch(
                    command.SessionId,
                    command.OperationId,
                    snapshot.Identity.LineageId,
                    appliedRevision,
                    lifecycleVersions,
                    MapExecutionMode(launch.ExecutionMode),
                    launch.FixtureId,
                    launch.FixtureVersion,
                    launch.MatchSeed,
                    launch.CompetitionId,
                    launch.ScheduleItemId,
                    command.WeekPlanId,
                    command.SlotActionId,
                    command.ActionOccurrenceId,
                    command.PreMatchPriority,
                    encoded.ContextDigest,
                    encoded.CanonicalContextUtf8,
                    launch.Teams[0].TeamId,
                    launch.Teams[1].TeamId,
                    launch.Teams.SelectMany(team => team.Players).Select(player => player.PlayerId),
                    snapshot.Player.PlayerId,
                    frozen);
                var outcomeSummary = OperationOutcomeSummary.ForPendingMatchCreated(
                    command.SessionId,
                    encoded.ContextDigest);
                var receipts = new List<OperationReceipt>(snapshot.OperationReceipts)
                {
                    new OperationReceipt(
                        command.OperationId,
                        OperationKind.CreatePendingMatch,
                        OperationReceiptTarget.ForPendingMatch(
                            command.WeekPlanId,
                            command.SlotActionId,
                            command.ActionOccurrenceId,
                            command.SessionId,
                            launch.ScheduleItemId,
                            encoded.ContextDigest),
                        fingerprint,
                        snapshot.Identity.LineageId,
                        appliedRevision,
                        command.CompletedAtUtcMs,
                        OperationOutcomeKind.PendingMatchCreated,
                        outcomeSummary)
                };
                var next = CareerWeekSnapshotFactory.Advance(
                    snapshot,
                    command.CompletedAtUtcMs,
                    CareerProgressionState.AwaitingMatch(
                        snapshot.Progression.WeekPlan,
                        command.SessionId),
                    snapshot.TrainingEmphases,
                    snapshot.Player.Attributes,
                    snapshot.Fatigue.Value,
                    snapshot.Mindset.Value,
                    snapshot.CoachTrust.Value,
                    receipts,
                    pending);

                cancellationToken.ThrowIfCancellationRequested();
                var committed = _repository.Commit(
                    command.ProfileId,
                    command.SaveId,
                    command.ExpectedVersionToken,
                    next,
                    command.OperationId);
                if (committed.Kind == PersistenceResultKind.VersionConflict)
                {
                    return ResolveCasConflict(command, fingerprint);
                }

                if (!IsCommitSuccess(committed))
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.ValidationFailed,
                        "pending_commit_failed",
                        committed.Kind,
                        snapshot,
                        null,
                        CareerPendingMatchFailureKind.Persistence);
                }

                committedSnapshot = committed.Snapshot;
                committedKind = committed.Kind;
                var persisted = RequireCommittedPending(committedSnapshot, command, fingerprint);
                var persistedContext = ContextFrom(persisted);
                cancellationToken.ThrowIfCancellationRequested();
                var execution = await _executor.ExecuteAsync(persistedContext, cancellationToken);
                ValidateExecution(execution, persistedContext);
                return Success(
                    CareerPendingCreationDisposition.Created,
                    committed.Kind,
                    committedSnapshot,
                    execution);
            }
            catch (OperationCanceledException)
            {
                return Failure(
                    CareerPendingMatchFlowStatus.Cancelled,
                    "cancelled",
                    committedKind,
                    committedSnapshot,
                    null,
                    CareerPendingMatchFailureKind.Cancellation,
                    committedSnapshot?.PendingMatch);
            }
            catch (Exception)
            {
                var status = committedSnapshot == null
                    ? CareerPendingMatchFlowStatus.ValidationFailed
                    : CareerPendingMatchFlowStatus.ExecutionFailed;
                return Failure(
                    status,
                    committedSnapshot == null ? "pending_creation_failed" : "match_execution_failed",
                    committedKind,
                    committedSnapshot,
                    null,
                    committedSnapshot == null
                        ? CareerPendingMatchFailureKind.Dependency
                        : CareerPendingMatchFailureKind.Execution,
                    committedSnapshot?.PendingMatch);
            }
        }

        public async Task<CareerPendingMatchFlowResult> RetryExecutionAsync(
            RetryPendingMatchExecutionCommand command,
            CancellationToken cancellationToken)
        {
            CareerSaveSnapshot snapshot = null;
            PersistenceResultKind? persistenceKind = null;
            var loadCompleted = false;
            var executionStarted = false;
            try
            {
                if (command == null || command.ProfileId.Value == Guid.Empty ||
                    command.SaveId.Value == Guid.Empty || command.SessionId == Guid.Empty)
                {
                    return Failure(CareerPendingMatchFlowStatus.ValidationFailed, "invalid_command");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var loaded = _repository.Load(command.ProfileId, command.SaveId);
                loadCompleted = true;
                if (!HasSnapshot(loaded))
                {
                    return LoadFailure(loaded);
                }

                snapshot = loaded.Snapshot;
                persistenceKind = loaded.Kind;
                if (!MatchesIdentity(snapshot, command.ProfileId, command.SaveId))
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.ValidationFailed,
                        "loaded_identity_mismatch",
                        loaded.Kind);
                }

                if (snapshot.PendingMatch == null ||
                    snapshot.PendingMatch.SessionId != command.SessionId)
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.NotFound,
                        "pending_session_not_found",
                        loaded.Kind,
                        snapshot);
                }

                if (snapshot.Progression.Kind != CareerProgressionKind.AwaitingMatch ||
                    snapshot.Progression.MatchSessionId != command.SessionId)
                {
                    return Failure(
                        CareerPendingMatchFlowStatus.InvalidState,
                        "pending_session_not_retryable",
                        loaded.Kind,
                        snapshot);
                }

                var context = ContextFrom(snapshot.PendingMatch);
                cancellationToken.ThrowIfCancellationRequested();
                executionStarted = true;
                var execution = await _executor.ExecuteAsync(context, cancellationToken);
                ValidateExecution(execution, context);
                return Success(null, loaded.Kind, snapshot, execution);
            }
            catch (OperationCanceledException)
            {
                return Failure(
                    CareerPendingMatchFlowStatus.Cancelled,
                    "cancelled",
                    persistenceKind,
                    snapshot,
                    null,
                    CareerPendingMatchFailureKind.Cancellation,
                    snapshot?.PendingMatch);
            }
            catch (Exception)
            {
                return Failure(
                    executionStarted
                        ? CareerPendingMatchFlowStatus.ExecutionFailed
                        : CareerPendingMatchFlowStatus.ValidationFailed,
                    executionStarted
                        ? "match_execution_failed"
                        : loadCompleted
                            ? "persisted_context_invalid"
                            : "load_failed",
                    persistenceKind,
                    snapshot,
                    null,
                    executionStarted
                        ? CareerPendingMatchFailureKind.Execution
                        : loadCompleted
                            ? CareerPendingMatchFailureKind.Command
                            : CareerPendingMatchFailureKind.Persistence,
                    snapshot?.PendingMatch);
            }
        }

        private CareerPendingMatchFlowResult ResolveCasConflict(
            CreatePendingMatchCommand command,
            Sha256Digest fingerprint)
        {
            var reloaded = _repository.Load(command.ProfileId, command.SaveId);
            if (!HasSnapshot(reloaded))
            {
                return Failure(
                    CareerPendingMatchFlowStatus.RevisionConflict,
                    "cas_conflict_reload_failed",
                    reloaded.Kind);
            }

            var lookup = new OperationReceiptIndex(reloaded.Snapshot.OperationReceipts)
                .Find(command.OperationId, fingerprint);
            if (lookup.Kind == OperationReceiptLookupKind.Existing)
            {
                return Existing(reloaded.Snapshot, reloaded.Kind, lookup.Receipt);
            }

            if (lookup.Kind == OperationReceiptLookupKind.Conflict)
            {
                return Failure(
                    CareerPendingMatchFlowStatus.OperationConflict,
                    "operation_fingerprint_conflict",
                    reloaded.Kind,
                    reloaded.Snapshot,
                    lookup.Receipt);
            }

            return Failure(
                CareerPendingMatchFlowStatus.RevisionConflict,
                "cas_conflict",
                PersistenceResultKind.VersionConflict,
                reloaded.Snapshot);
        }

        private static CareerPendingMatchFlowResult Existing(
            CareerSaveSnapshot snapshot,
            PersistenceResultKind kind,
            OperationReceipt receipt)
        {
            var pending = snapshot.PendingMatch != null &&
                          snapshot.PendingMatch.SessionId == receipt.Target.MatchSessionId
                ? snapshot.PendingMatch
                : null;
            if (pending != null)
            {
                return new CareerPendingMatchFlowResult(
                    CareerPendingMatchFlowStatus.AwaitingSettlement,
                    CareerPendingCreationDisposition.Existing,
                    kind,
                    snapshot,
                    receipt.AppliedRevision,
                    null,
                    pending.SessionId,
                    pending.ContextDigest,
                    pending.CanonicalContextUtf8,
                    null,
                    null,
                    CareerPendingMatchFailureKind.None,
                    null);
            }

            var history = snapshot.MatchHistory.FirstOrDefault(
                item => item.SessionId == receipt.Target.MatchSessionId);
            return new CareerPendingMatchFlowResult(
                CareerPendingMatchFlowStatus.AwaitingSettlement,
                CareerPendingCreationDisposition.Existing,
                kind,
                snapshot,
                receipt.AppliedRevision,
                null,
                receipt.Target.MatchSessionId,
                history == null ? receipt.Target.ContextDigest : history.ContextDigest,
                history?.CanonicalContextUtf8,
                history == null ? (Sha256Digest?)null : history.ResultDigest,
                history?.CanonicalResultUtf8,
                CareerPendingMatchFailureKind.None,
                null);
        }

        private static CareerPendingMatchFlowResult Success(
            CareerPendingCreationDisposition? disposition,
            PersistenceResultKind kind,
            CareerSaveSnapshot snapshot,
            CareerMatchExecutionOutcome execution)
        {
            return new CareerPendingMatchFlowResult(
                CareerPendingMatchFlowStatus.AwaitingSettlement,
                disposition,
                kind,
                snapshot,
                snapshot.Identity.Revision,
                null,
                execution.Context.SessionId,
                execution.Context.ContextDigest,
                execution.Context.CanonicalContextUtf8,
                execution.ResultDigest,
                execution.CanonicalResultUtf8,
                CareerPendingMatchFailureKind.None,
                null);
        }

        private static CareerPendingMatchFlowResult Failure(
            CareerPendingMatchFlowStatus status,
            string code,
            PersistenceResultKind? kind = null,
            CareerSaveSnapshot snapshot = null,
            OperationReceipt conflict = null,
            CareerPendingMatchFailureKind failureKind = CareerPendingMatchFailureKind.Command,
            PendingCareerMatch pending = null)
        {
            var committedByCreate = snapshot != null &&
                                    (kind == PersistenceResultKind.Committed ||
                                     kind == PersistenceResultKind.BackupDegraded);
            return new CareerPendingMatchFlowResult(
                status,
                committedByCreate
                    ? new CareerPendingCreationDisposition?(
                        CareerPendingCreationDisposition.Created)
                    : null,
                kind,
                snapshot,
                committedByCreate
                    ? new long?(snapshot.Identity.Revision)
                    : null,
                conflict,
                pending?.SessionId,
                pending?.ContextDigest,
                pending?.CanonicalContextUtf8,
                null,
                null,
                failureKind,
                code);
        }

        private static CareerPendingMatchFlowResult LoadFailure(CareerPersistenceResult result)
        {
            return Failure(
                result != null && result.Kind == PersistenceResultKind.NotFound
                    ? CareerPendingMatchFlowStatus.NotFound
                    : CareerPendingMatchFlowStatus.ValidationFailed,
                "load_failed",
                result?.Kind,
                null,
                null,
                CareerPendingMatchFailureKind.Persistence);
        }

        private static bool IsValid(CreatePendingMatchCommand command)
        {
            if (command == null || command.ProfileId.Value == Guid.Empty ||
                command.SaveId.Value == Guid.Empty ||
                command.ExpectedVersionToken.LineageId.Value == Guid.Empty ||
                command.ExpectedVersionToken.Revision < 1 ||
                string.IsNullOrEmpty(command.ExpectedVersionToken.SnapshotHash.Value) ||
                command.OperationId.Value == Guid.Empty ||
                command.CompletedAtUtcMs < 0 ||
                command.CompletedAtUtcMs > MaximumIJsonSafeInteger ||
                command.SessionId == Guid.Empty ||
                command.WeekPlanId.Value == Guid.Empty ||
                command.SlotActionId.Value == Guid.Empty ||
                command.ActionOccurrenceId.Value == Guid.Empty)
            {
                return false;
            }

            return Enum.IsDefined(typeof(CareerMatchPriority), command.PreMatchPriority);
        }

        private static bool IsMatchReady(
            CareerSaveSnapshot snapshot,
            CreatePendingMatchCommand command,
            out CareerWeekActionState matchAction)
        {
            matchAction = null;
            if (!snapshot.Versions.Equals(CareerSaveVersions.Current) ||
                snapshot.Progression.Kind != CareerProgressionKind.Planned ||
                snapshot.Progression.NextSlotNumber != 3 ||
                snapshot.PendingMatch != null ||
                !snapshot.TeamId.HasValue ||
                snapshot.Player == null ||
                !snapshot.Fatigue.HasValue ||
                !snapshot.Mindset.HasValue ||
                !snapshot.CoachTrust.HasValue)
            {
                return false;
            }

            var plan = snapshot.Progression.WeekPlan;
            if (plan == null || !plan.IsConfirmed || plan.Season != 1 || plan.Week != 1 ||
                plan.Slots.Count != 3 || !plan.PlanId.Equals(command.WeekPlanId))
            {
                return false;
            }

            matchAction = plan.Slots[2];
            return matchAction.Kind == CareerWeekActionKind.Match &&
                   matchAction.SlotActionId.Equals(command.SlotActionId) &&
                   matchAction.OccurrenceId.Equals(command.ActionOccurrenceId) &&
                   string.Equals(
                       matchAction.ContentId,
                       "schedule.u1w1.match.01",
                       StringComparison.Ordinal);
        }

        private static void ValidateLaunch(
            CareerMatchLaunch launch,
            CreatePendingMatchCommand command,
            CareerSaveSnapshot snapshot,
            CareerWeekActionState action,
            uint seed)
        {
            if (launch == null || launch.SessionId != command.SessionId ||
                launch.MatchSeed != seed ||
                !string.Equals(launch.ScheduleItemId, action.ContentId, StringComparison.Ordinal) ||
                !launch.Teams[0].TeamId.Equals(snapshot.TeamId.Value) ||
                !launch.Teams[0].Players.Any(
                    player => player.PlayerId.Equals(snapshot.Player.PlayerId) &&
                              player.JerseyNumber == snapshot.Player.JerseyNumber &&
                              player.Fatigue == snapshot.Fatigue.Value &&
                              player.Attributes.Equals(snapshot.Player.Attributes)) ||
                launch.PreMatchPriority != MapPriority(command.PreMatchPriority))
            {
                throw new InvalidOperationException("The first-match launch did not preserve authoritative inputs.");
            }
        }

        private static void ValidateEncoded(CareerCanonicalMatchContext encoded, Guid sessionId)
        {
            if (encoded == null || encoded.SessionId != sessionId)
            {
                throw new InvalidOperationException("Encoded context identity is invalid.");
            }
        }

        private static PendingCareerMatch RequireCommittedPending(
            CareerSaveSnapshot snapshot,
            CreatePendingMatchCommand command,
            Sha256Digest fingerprint)
        {
            if (snapshot == null || snapshot.PendingMatch == null ||
                snapshot.PendingMatch.SessionId != command.SessionId ||
                snapshot.Progression.Kind != CareerProgressionKind.AwaitingMatch ||
                snapshot.Progression.MatchSessionId != command.SessionId)
            {
                throw new InvalidOperationException("The committed snapshot did not contain the pending match.");
            }

            var lookup = new OperationReceiptIndex(snapshot.OperationReceipts)
                .Find(command.OperationId, fingerprint);
            if (lookup.Kind != OperationReceiptLookupKind.Existing)
            {
                throw new InvalidOperationException("The committed snapshot did not contain the creation receipt.");
            }

            return snapshot.PendingMatch;
        }

        private static void ValidateExecution(
            CareerMatchExecutionOutcome execution,
            CareerCanonicalMatchContext context)
        {
            if (execution == null || execution.Context.SessionId != context.SessionId ||
                !execution.Context.ContextDigest.Equals(context.ContextDigest) ||
                !execution.Context.CanonicalContextUtf8.SequenceEqual(context.CanonicalContextUtf8))
            {
                throw new InvalidOperationException("Execution did not preserve the committed context.");
            }
        }

        private static CareerCanonicalMatchContext ContextFrom(PendingCareerMatch pending)
        {
            return new CareerCanonicalMatchContext(
                pending.SessionId,
                pending.ContextDigest,
                pending.CanonicalContextUtf8);
        }

        private static bool MatchesIdentity(
            CareerSaveSnapshot snapshot,
            ProfileId profileId,
            SaveId saveId)
        {
            return snapshot != null &&
                   snapshot.Identity.ProfileId.Equals(profileId) &&
                   snapshot.Identity.SaveId.Equals(saveId);
        }

        private static bool HasSnapshot(CareerPersistenceResult result)
        {
            return result != null &&
                   (result.Kind == PersistenceResultKind.Loaded ||
                    result.Kind == PersistenceResultKind.Created ||
                    result.Kind == PersistenceResultKind.Committed ||
                    result.Kind == PersistenceResultKind.BackupDegraded) &&
                   result.Snapshot != null;
        }

        private static bool IsCommitSuccess(CareerPersistenceResult result)
        {
            return result != null &&
                   (result.Kind == PersistenceResultKind.Committed ||
                    result.Kind == PersistenceResultKind.BackupDegraded) &&
                   result.Snapshot != null;
        }

        private static CareerPreMatchPriority MapPriority(CareerMatchPriority priority)
        {
            switch (priority)
            {
                case CareerMatchPriority.AttackFirst: return CareerPreMatchPriority.AttackFirst;
                case CareerMatchPriority.FirstContactSecurity:
                    return CareerPreMatchPriority.FirstContactSecurity;
                case CareerMatchPriority.StaminaControl:
                    return CareerPreMatchPriority.StaminaControl;
                default:
                    throw new ArgumentOutOfRangeException(nameof(priority), priority, null);
            }
        }

        private static CareerMatchLifecycleExecutionMode MapExecutionMode(
            CareerMatchExecutionMode mode)
        {
            switch (mode)
            {
                case CareerMatchExecutionMode.Fixture:
                    return CareerMatchLifecycleExecutionMode.Fixture;
                case CareerMatchExecutionMode.Direct:
                    return CareerMatchLifecycleExecutionMode.Direct;
                case CareerMatchExecutionMode.QuickSimulation:
                    return CareerMatchLifecycleExecutionMode.QuickSimulation;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
