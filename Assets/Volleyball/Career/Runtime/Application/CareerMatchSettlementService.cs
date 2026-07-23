using System;
using System.Linq;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Application
{
    public interface ICareerMatchSettlementCalculator
    {
        CareerSettlementSummary Calculate(
            PendingCareerMatch pendingMatch,
            CareerMatchFacts completedFacts,
            CareerPlayerRecord currentPlayer,
            PotentialGrade potentialGrade,
            int currentFatigue,
            int currentMindset,
            int currentCoachTrust);
    }

    public sealed class CareerMatchSettlementRulesV1Calculator :
        ICareerMatchSettlementCalculator
    {
        public CareerSettlementSummary Calculate(
            PendingCareerMatch pendingMatch,
            CareerMatchFacts completedFacts,
            CareerPlayerRecord currentPlayer,
            PotentialGrade potentialGrade,
            int currentFatigue,
            int currentMindset,
            int currentCoachTrust)
        {
            return CareerMatchSettlementRulesV1.Calculate(
                pendingMatch,
                completedFacts,
                currentPlayer,
                potentialGrade,
                currentFatigue,
                currentMindset,
                currentCoachTrust);
        }
    }

    public sealed class CareerMatchSettlementService
    {
        private const long MaximumIJsonSafeInteger = 9007199254740991L;
        private readonly ICareerSaveRepository _repository;
        private readonly ICareerMatchExecutor _executor;
        private readonly ICareerMatchSettlementCalculator _calculator;

        public CareerMatchSettlementService(
            ICareerSaveRepository repository,
            ICareerMatchExecutor executor,
            ICareerMatchSettlementCalculator calculator)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        public CareerMatchSettlementResult Settle(SettleCareerMatchCommand command)
        {
            var phase = SettlementPhase.Command;
            CareerMatchExecutionOutcome incoming = null;
            CareerSaveSnapshot loadedSnapshot = null;
            try
            {
                if (!IsValid(command))
                {
                    return Failure(
                        CareerMatchSettlementStatus.ValidationFailed,
                        "invalid_command",
                        CareerMatchSettlementFailureKind.Command);
                }

                phase = SettlementPhase.CanonicalPair;
                incoming = _executor.DecodeAndValidate(
                    command.CanonicalContextUtf8,
                    command.CanonicalResultUtf8);
                ValidateDecodedPair(command, incoming);

                phase = SettlementPhase.Persistence;
                var loaded = _repository.Load(command.ProfileId, command.SaveId);
                if (!HasSnapshot(loaded))
                {
                    return LoadFailure(loaded, incoming);
                }

                loadedSnapshot = loaded.Snapshot;
                if (!MatchesIdentity(loadedSnapshot, command.ProfileId, command.SaveId))
                {
                    return Failure(
                        CareerMatchSettlementStatus.ValidationFailed,
                        "loaded_identity_mismatch",
                        CareerMatchSettlementFailureKind.Persistence,
                        loaded.Kind,
                        loadedSnapshot,
                        incoming);
                }

                CareerMatchHistoryEntry storedHistory;
                CareerSettlementReceipt storedReceipt;
                if (TryFindStored(
                        loadedSnapshot,
                        command.SessionId,
                        out storedHistory,
                        out storedReceipt))
                {
                    return ResolveStored(
                        loaded.Kind,
                        loadedSnapshot,
                        incoming,
                        storedHistory,
                        storedReceipt);
                }

                if (!loadedSnapshot.Identity.VersionToken.Equals(
                        command.ExpectedVersionToken))
                {
                    return Failure(
                        CareerMatchSettlementStatus.RevisionConflict,
                        "expected_version_mismatch",
                        CareerMatchSettlementFailureKind.None,
                        loaded.Kind,
                        loadedSnapshot,
                        incoming);
                }

                if (command.SettledAtUtcMs < loadedSnapshot.Identity.UpdatedAtUtcMs)
                {
                    return Failure(
                        CareerMatchSettlementStatus.ValidationFailed,
                        "settlement_timestamp_precedes_snapshot",
                        CareerMatchSettlementFailureKind.Command,
                        loaded.Kind,
                        loadedSnapshot,
                        incoming);
                }

                var pending = loadedSnapshot.PendingMatch;
                if (pending == null ||
                    loadedSnapshot.Progression.Kind != CareerProgressionKind.AwaitingMatch ||
                    loadedSnapshot.Progression.MatchSessionId != command.SessionId ||
                    pending.SessionId != command.SessionId ||
                    !pending.ContextDigest.Equals(incoming.Context.ContextDigest) ||
                    !pending.CanonicalContextUtf8.SequenceEqual(
                        incoming.Context.CanonicalContextUtf8))
                {
                    return Failure(
                        CareerMatchSettlementStatus.InvalidState,
                        "pending_match_mismatch",
                        CareerMatchSettlementFailureKind.None,
                        loaded.Kind,
                        loadedSnapshot,
                        incoming);
                }

                if (incoming.Facts.Status == CareerMatchResultStatus.Abandoned)
                {
                    return Failure(
                        CareerMatchSettlementStatus.Abandoned,
                        "match_abandoned",
                        CareerMatchSettlementFailureKind.None,
                        loaded.Kind,
                        loadedSnapshot,
                        incoming);
                }

                phase = SettlementPhase.Rules;
                var summary = _calculator.Calculate(
                    pending,
                    incoming.Facts,
                    loadedSnapshot.Player,
                    loadedSnapshot.PotentialGrade.Value,
                    loadedSnapshot.Fatigue.Value,
                    loadedSnapshot.Mindset.Value,
                    loadedSnapshot.CoachTrust.Value);
                var next = CareerSettlementSnapshotFactory.SettleFirstMatch(
                    loadedSnapshot,
                    command.SettledAtUtcMs,
                    new WeekPlanId(command.SessionId),
                    incoming.ResultDigest,
                    incoming.CanonicalResultUtf8,
                    summary);

                phase = SettlementPhase.Persistence;
                var committed = _repository.Commit(
                    command.ProfileId,
                    command.SaveId,
                    command.ExpectedVersionToken,
                    next,
                    new OperationId(command.SessionId));
                if (committed.Kind == PersistenceResultKind.VersionConflict)
                {
                    return ResolveCasConflict(command, incoming);
                }

                if (!IsCommitSuccess(committed))
                {
                    return Failure(
                        CareerMatchSettlementStatus.ValidationFailed,
                        "settlement_commit_failed",
                        CareerMatchSettlementFailureKind.Persistence,
                        committed.Kind,
                        loadedSnapshot,
                        incoming);
                }

                CareerMatchHistoryEntry committedHistory;
                CareerSettlementReceipt committedReceipt;
                if (!TryFindStored(
                        committed.Snapshot,
                        command.SessionId,
                        out committedHistory,
                        out committedReceipt) ||
                    !PairMatches(committedReceipt, incoming))
                {
                    return Failure(
                        CareerMatchSettlementStatus.ValidationFailed,
                        "committed_settlement_evidence_missing",
                        CareerMatchSettlementFailureKind.Persistence,
                        committed.Kind,
                        committed.Snapshot,
                        incoming);
                }

                return Result(
                    CareerMatchSettlementStatus.Settled,
                    committed.Kind,
                    committed.Snapshot,
                    incoming,
                    committedReceipt.AppliedRevision,
                    committedReceipt,
                    committedHistory);
            }
            catch (Exception)
            {
                var failureKind = phase == SettlementPhase.CanonicalPair
                    ? CareerMatchSettlementFailureKind.CanonicalPair
                    : phase == SettlementPhase.Persistence
                        ? CareerMatchSettlementFailureKind.Persistence
                        : phase == SettlementPhase.Rules
                            ? CareerMatchSettlementFailureKind.Rules
                            : CareerMatchSettlementFailureKind.Command;
                return Failure(
                    CareerMatchSettlementStatus.ValidationFailed,
                    phase == SettlementPhase.CanonicalPair
                        ? "canonical_pair_invalid"
                        : phase == SettlementPhase.Persistence
                            ? "persistence_failed"
                            : phase == SettlementPhase.Rules
                                ? "settlement_rules_failed"
                                : "invalid_command",
                    failureKind,
                    null,
                    loadedSnapshot,
                    incoming);
            }
        }

        private CareerMatchSettlementResult ResolveCasConflict(
            SettleCareerMatchCommand command,
            CareerMatchExecutionOutcome incoming)
        {
            var reloaded = _repository.Load(command.ProfileId, command.SaveId);
            if (!HasSnapshot(reloaded))
            {
                return Failure(
                    CareerMatchSettlementStatus.RevisionConflict,
                    "cas_conflict_reload_failed",
                    CareerMatchSettlementFailureKind.Persistence,
                    reloaded?.Kind,
                    null,
                    incoming);
            }

            CareerMatchHistoryEntry history;
            CareerSettlementReceipt receipt;
            if (TryFindStored(reloaded.Snapshot, command.SessionId, out history, out receipt))
            {
                return ResolveStored(
                    reloaded.Kind,
                    reloaded.Snapshot,
                    incoming,
                    history,
                    receipt);
            }

            return Failure(
                CareerMatchSettlementStatus.RevisionConflict,
                "cas_conflict",
                CareerMatchSettlementFailureKind.None,
                PersistenceResultKind.VersionConflict,
                reloaded.Snapshot,
                incoming);
        }

        private static CareerMatchSettlementResult ResolveStored(
            PersistenceResultKind persistenceKind,
            CareerSaveSnapshot snapshot,
            CareerMatchExecutionOutcome incoming,
            CareerMatchHistoryEntry history,
            CareerSettlementReceipt receipt)
        {
            if (PairMatches(receipt, incoming))
            {
                return Result(
                    CareerMatchSettlementStatus.Existing,
                    persistenceKind,
                    snapshot,
                    incoming,
                    receipt.AppliedRevision,
                    receipt,
                    history);
            }

            return new CareerMatchSettlementResult(
                CareerMatchSettlementStatus.SessionResultConflict,
                persistenceKind,
                snapshot,
                incoming.Context.SessionId,
                incoming.Context.ContextDigest,
                incoming.ResultDigest,
                null,
                receipt,
                history,
                new CareerMatchSettlementConflictEvidence(
                    receipt.ContextDigest,
                    receipt.ResultDigest,
                    incoming.Context.ContextDigest,
                    incoming.ResultDigest),
                CareerMatchSettlementFailureKind.None,
                null);
        }

        private static bool TryFindStored(
            CareerSaveSnapshot snapshot,
            Guid sessionId,
            out CareerMatchHistoryEntry history,
            out CareerSettlementReceipt receipt)
        {
            history = snapshot.MatchHistory.FirstOrDefault(item => item.SessionId == sessionId);
            receipt = snapshot.SettlementReceipts.FirstOrDefault(item => item.SessionId == sessionId);
            if ((history == null) != (receipt == null))
            {
                throw new InvalidOperationException(
                    "Settlement history and receipt indexes are inconsistent.");
            }

            return receipt != null;
        }

        private static bool PairMatches(
            CareerSettlementReceipt receipt,
            CareerMatchExecutionOutcome incoming)
        {
            return receipt.ContextDigest.Equals(incoming.Context.ContextDigest) &&
                   receipt.ResultDigest.Equals(incoming.ResultDigest);
        }

        private static void ValidateDecodedPair(
            SettleCareerMatchCommand command,
            CareerMatchExecutionOutcome incoming)
        {
            if (incoming == null ||
                incoming.Context.SessionId != command.SessionId ||
                !incoming.Context.CanonicalContextUtf8.SequenceEqual(
                    command.CanonicalContextUtf8) ||
                !incoming.CanonicalResultUtf8.SequenceEqual(
                    command.CanonicalResultUtf8))
            {
                throw new ArgumentException(
                    "Decoded canonical evidence does not match the settlement command.");
            }
        }

        private static bool IsValid(SettleCareerMatchCommand command)
        {
            return command != null &&
                   command.ProfileId.Value != Guid.Empty &&
                   command.SaveId.Value != Guid.Empty &&
                   command.ExpectedVersionToken.LineageId.Value != Guid.Empty &&
                   command.ExpectedVersionToken.Revision >= 1 &&
                   !string.IsNullOrEmpty(command.ExpectedVersionToken.SnapshotHash.Value) &&
                   command.SettledAtUtcMs >= 0 &&
                   command.SettledAtUtcMs <= MaximumIJsonSafeInteger &&
                   command.SessionId != Guid.Empty &&
                   command.CanonicalContextUtf8 != null &&
                   command.CanonicalContextUtf8.Length != 0 &&
                   command.CanonicalResultUtf8 != null &&
                   command.CanonicalResultUtf8.Length != 0;
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

        private static bool HasSnapshot(CareerPersistenceResult persistence)
        {
            return persistence != null &&
                   (persistence.Kind == PersistenceResultKind.Loaded ||
                    persistence.Kind == PersistenceResultKind.Created ||
                    persistence.Kind == PersistenceResultKind.Committed ||
                    persistence.Kind == PersistenceResultKind.BackupDegraded) &&
                   persistence.Snapshot != null;
        }

        private static bool IsCommitSuccess(CareerPersistenceResult persistence)
        {
            return persistence != null &&
                   (persistence.Kind == PersistenceResultKind.Committed ||
                    persistence.Kind == PersistenceResultKind.BackupDegraded) &&
                   persistence.Snapshot != null;
        }

        private static CareerMatchSettlementResult LoadFailure(
            CareerPersistenceResult persistence,
            CareerMatchExecutionOutcome incoming)
        {
            return Failure(
                persistence != null && persistence.Kind == PersistenceResultKind.NotFound
                    ? CareerMatchSettlementStatus.NotFound
                    : CareerMatchSettlementStatus.ValidationFailed,
                "load_failed",
                CareerMatchSettlementFailureKind.Persistence,
                persistence?.Kind,
                null,
                incoming);
        }

        private static CareerMatchSettlementResult Failure(
            CareerMatchSettlementStatus status,
            string failureCode,
            CareerMatchSettlementFailureKind failureKind,
            PersistenceResultKind? persistenceKind = null,
            CareerSaveSnapshot snapshot = null,
            CareerMatchExecutionOutcome incoming = null)
        {
            return new CareerMatchSettlementResult(
                status,
                persistenceKind,
                snapshot,
                incoming?.Context.SessionId,
                incoming?.Context.ContextDigest,
                incoming?.ResultDigest,
                null,
                null,
                null,
                null,
                failureKind,
                failureCode);
        }

        private static CareerMatchSettlementResult Result(
            CareerMatchSettlementStatus status,
            PersistenceResultKind persistenceKind,
            CareerSaveSnapshot snapshot,
            CareerMatchExecutionOutcome incoming,
            long committedRevision,
            CareerSettlementReceipt receipt,
            CareerMatchHistoryEntry history)
        {
            return new CareerMatchSettlementResult(
                status,
                persistenceKind,
                snapshot,
                incoming.Context.SessionId,
                incoming.Context.ContextDigest,
                incoming.ResultDigest,
                committedRevision,
                receipt,
                history,
                null,
                CareerMatchSettlementFailureKind.None,
                null);
        }

        private enum SettlementPhase
        {
            Command = 0,
            CanonicalPair = 1,
            Persistence = 2,
            Rules = 3
        }
    }
}
