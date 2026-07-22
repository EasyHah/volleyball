using System;
using System.Collections.Generic;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public sealed class CareerWeekCommandService
    {
        private const long MaximumIJsonSafeInteger = 9007199254740991L;
        private const string FirstWeekMatchContentId = "schedule.u1w1.match.01";

        private readonly ICareerSaveRepository _repository;

        public CareerWeekCommandService(ICareerSaveRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public CareerWeekCommandResult ConfirmWeekPlan(ConfirmWeekPlanCommand command)
        {
            Sha256Digest fingerprint;
            try
            {
                ValidateCommandShape(command);
                fingerprint = CareerOperationFingerprintV1.Hash(command);
            }
            catch (ArgumentException)
            {
                return Result(CareerApplicationStatus.InvalidInputOrState);
            }
            catch (InvalidOperationException)
            {
                return Result(CareerApplicationStatus.InvalidInputOrState);
            }

            if (!TryLoad(command.ProfileId, command.SaveId, out var loaded))
            {
                return Result(CareerApplicationStatus.PersistenceFailure);
            }

            if (loaded.Kind == PersistenceResultKind.NotFound)
            {
                return Result(CareerApplicationStatus.NotFound, loaded.Kind);
            }

            if (!HasSnapshot(loaded))
            {
                return Result(CareerApplicationStatus.PersistenceFailure, loaded.Kind);
            }

            var authoritative = loaded.Snapshot;
            var lookup = FindReceipt(authoritative, command.OperationId, fingerprint);
            if (lookup.Kind == OperationReceiptLookupKind.Existing)
            {
                return Existing(authoritative, lookup.Receipt, loaded.Kind);
            }

            if (lookup.Kind == OperationReceiptLookupKind.Conflict)
            {
                return Result(
                    CareerApplicationStatus.OperationConflict,
                    loaded.Kind,
                    authoritative,
                    lookup.Receipt);
            }

            if (!authoritative.Identity.VersionToken.Equals(command.ExpectedVersionToken))
            {
                return Result(
                    CareerApplicationStatus.VersionConflict,
                    loaded.Kind,
                    authoritative);
            }

            CareerSaveSnapshot next;
            try
            {
                ValidateAuthoritativeTransition(authoritative, command);
                next = BuildNextSnapshot(authoritative, command, fingerprint);
            }
            catch (ArgumentException)
            {
                return Result(
                    CareerApplicationStatus.InvalidInputOrState,
                    loaded.Kind,
                    authoritative);
            }
            catch (InvalidOperationException)
            {
                return Result(
                    CareerApplicationStatus.InvalidInputOrState,
                    loaded.Kind,
                    authoritative);
            }
            catch (OverflowException)
            {
                return Result(
                    CareerApplicationStatus.InvalidInputOrState,
                    loaded.Kind,
                    authoritative);
            }

            if (!TryCommit(command, next, out var committed))
            {
                return Result(
                    CareerApplicationStatus.PersistenceFailure,
                    null,
                    authoritative);
            }

            if (committed.Kind == PersistenceResultKind.Committed ||
                committed.Kind == PersistenceResultKind.BackupDegraded)
            {
                var committedLookup = FindReceipt(
                    committed.Snapshot,
                    command.OperationId,
                    fingerprint);
                if (committedLookup.Kind != OperationReceiptLookupKind.Existing)
                {
                    return Result(
                        CareerApplicationStatus.PersistenceFailure,
                        committed.Kind,
                        authoritative);
                }

                return Result(
                    CareerApplicationStatus.Applied,
                    committed.Kind,
                    committed.Snapshot,
                    null,
                    committedLookup.Receipt.OutcomeSummary);
            }

            if (committed.Kind == PersistenceResultKind.VersionConflict)
            {
                return ResolveCommitRace(command, fingerprint, committed.Kind);
            }

            return Result(
                CareerApplicationStatus.PersistenceFailure,
                committed.Kind,
                authoritative);
        }

        private CareerWeekCommandResult ResolveCommitRace(
            ConfirmWeekPlanCommand command,
            Sha256Digest fingerprint,
            PersistenceResultKind commitKind)
        {
            if (!TryLoad(command.ProfileId, command.SaveId, out var latest))
            {
                return Result(CareerApplicationStatus.PersistenceFailure);
            }

            if (!HasSnapshot(latest))
            {
                return Result(CareerApplicationStatus.PersistenceFailure, latest.Kind);
            }

            var lookup = FindReceipt(latest.Snapshot, command.OperationId, fingerprint);
            if (lookup.Kind == OperationReceiptLookupKind.Existing)
            {
                return Existing(latest.Snapshot, lookup.Receipt, latest.Kind);
            }

            if (lookup.Kind == OperationReceiptLookupKind.Conflict)
            {
                return Result(
                    CareerApplicationStatus.OperationConflict,
                    latest.Kind,
                    latest.Snapshot,
                    lookup.Receipt);
            }

            return Result(
                CareerApplicationStatus.VersionConflict,
                commitKind,
                latest.Snapshot);
        }

        private static CareerSaveSnapshot BuildNextSnapshot(
            CareerSaveSnapshot prior,
            ConfirmWeekPlanCommand command,
            Sha256Digest fingerprint)
        {
            var outcome = OperationOutcomeSummary.ForWeekPlanConfirmed();
            var nextRevision = checked(prior.Identity.Revision + 1);
            var receipts = new List<OperationReceipt>(prior.OperationReceipts)
            {
                new OperationReceipt(
                    command.OperationId,
                    OperationKind.ConfirmWeekPlan,
                    OperationReceiptTarget.ForWeekPlanConfirmation(command.CandidatePlan.PlanId),
                    fingerprint,
                    prior.Identity.LineageId,
                    nextRevision,
                    command.CompletedAtUtcMs,
                    OperationOutcomeKind.WeekPlanConfirmed,
                    outcome)
            };
            return CareerWeekSnapshotFactory.Advance(
                prior,
                command.CompletedAtUtcMs,
                CareerProgressionState.Planned(command.CandidatePlan, 1),
                prior.TrainingEmphases,
                prior.Player.Attributes,
                prior.Fatigue.Value,
                prior.Mindset.Value,
                prior.CoachTrust.Value,
                receipts);
        }

        private static void ValidateCommandShape(ConfirmWeekPlanCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            RequireGuid(command.ProfileId.Value, nameof(command.ProfileId));
            RequireGuid(command.SaveId.Value, nameof(command.SaveId));
            RequireGuid(command.OperationId.Value, nameof(command.OperationId));
            RequireGuid(
                command.ExpectedVersionToken.LineageId.Value,
                nameof(command.ExpectedVersionToken));
            if (command.ExpectedVersionToken.Revision < 1 ||
                command.ExpectedVersionToken.Revision > MaximumIJsonSafeInteger ||
                string.IsNullOrEmpty(command.ExpectedVersionToken.SnapshotHash.Value))
            {
                throw new ArgumentException("A complete expected version token is required.");
            }

            RequireTimestamp(command.CompletedAtUtcMs, nameof(command.CompletedAtUtcMs));
            var plan = command.CandidatePlan;
            if (plan == null || plan.Slots == null || plan.Slots.Count != CareerWeekPlan.SlotCount)
            {
                throw new ArgumentException("A complete candidate week plan is required.");
            }

            RequireGuid(plan.PlanId.Value, nameof(command.CandidatePlan));
            for (var index = 0; index < plan.Slots.Count; index++)
            {
                var slot = plan.Slots[index];
                if (slot == null)
                {
                    throw new ArgumentException("Every candidate slot must be populated.");
                }

                RequireGuid(slot.SlotActionId.Value, nameof(command.CandidatePlan));
                RequireGuid(slot.OccurrenceId.Value, nameof(command.CandidatePlan));
                if (string.IsNullOrWhiteSpace(slot.ContentId))
                {
                    throw new ArgumentException("Every candidate slot requires content identity.");
                }
            }
        }

        private static void ValidateAuthoritativeTransition(
            CareerSaveSnapshot snapshot,
            ConfirmWeekPlanCommand command)
        {
            RequireSupportedVersions(snapshot.Versions);
            if (!snapshot.Identity.ProfileId.Equals(command.ProfileId) ||
                !snapshot.Identity.SaveId.Equals(command.SaveId))
            {
                throw new ArgumentException("The authoritative save ownership does not match the command.");
            }

            if (command.CompletedAtUtcMs < snapshot.Identity.UpdatedAtUtcMs)
            {
                throw new ArgumentException("Completion time cannot precede the authoritative snapshot.");
            }

            if (snapshot.Progression.Kind != CareerProgressionKind.Planning ||
                snapshot.Progression.Phase != CareerPhase.University ||
                !snapshot.Onboarding.IsComplete ||
                !snapshot.Onboarding.IsFormallyEnrolled ||
                !snapshot.HasCompletePlayer ||
                !snapshot.PotentialGrade.HasValue ||
                !snapshot.Fatigue.HasValue ||
                !snapshot.Mindset.HasValue ||
                !snapshot.CoachTrust.HasValue ||
                snapshot.TrainingEmphases.Contributions.Count != 0)
            {
                throw new ArgumentException("Only a complete enrolled Planning snapshot can confirm a plan.");
            }

            var current = snapshot.Progression.WeekPlan;
            var candidate = command.CandidatePlan;
            if (current == null || current.IsConfirmed || !candidate.IsConfirmed ||
                !candidate.PlanId.Equals(current.PlanId) ||
                candidate.Season != current.Season ||
                candidate.Week != current.Week ||
                candidate.Slots.Count != CareerWeekPlan.SlotCount)
            {
                throw new ArgumentException("The candidate does not match the current unconfirmed plan.");
            }

            var currentMatch = current.Slots[2];
            var candidateMatch = candidate.Slots[2];
            if (currentMatch == null || candidateMatch == null ||
                currentMatch.Kind != CareerWeekActionKind.Match ||
                candidateMatch.Kind != CareerWeekActionKind.Match ||
                !candidateMatch.SlotActionId.Equals(currentMatch.SlotActionId) ||
                !candidateMatch.OccurrenceId.Equals(currentMatch.OccurrenceId) ||
                !string.Equals(
                    candidateMatch.ContentId,
                    FirstWeekMatchContentId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    currentMatch.ContentId,
                    FirstWeekMatchContentId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException("The authoritative match reservation must be preserved exactly.");
            }

            var catalog = CareerWeekActionCatalogV1.Create();
            var slotActionIds = new HashSet<SlotActionId>();
            var occurrenceIds = new HashSet<OccurrenceId>();
            for (var index = 0; index < candidate.Slots.Count; index++)
            {
                var action = candidate.Slots[index];
                if (action == null ||
                    !slotActionIds.Add(action.SlotActionId) ||
                    !occurrenceIds.Add(action.OccurrenceId))
                {
                    throw new ArgumentException("Candidate action and occurrence IDs must be unique.");
                }

                var definition = catalog.Find(action.ContentId);
                if (definition == null || definition.Kind != action.Kind)
                {
                    throw new ArgumentException("Candidate actions must exactly match the closed catalog.");
                }

                if (index < 2 && action.Kind == CareerWeekActionKind.Match)
                {
                    throw new ArgumentException("Free action slots cannot contain a match.");
                }
            }

            for (var index = 0; index < snapshot.Onboarding.Stages.Count; index++)
            {
                if (occurrenceIds.Contains(snapshot.Onboarding.Stages[index].OccurrenceId))
                {
                    throw new ArgumentException("Candidate occurrences cannot collide with onboarding.");
                }
            }
        }

        private static OperationReceiptLookup FindReceipt(
            CareerSaveSnapshot snapshot,
            OperationId operationId,
            Sha256Digest fingerprint)
        {
            return new OperationReceiptIndex(snapshot.OperationReceipts)
                .Find(operationId, fingerprint);
        }

        private bool TryLoad(
            ProfileId profileId,
            SaveId saveId,
            out CareerPersistenceResult result)
        {
            try
            {
                result = _repository.Load(profileId, saveId);
                return result != null;
            }
            catch (Exception)
            {
                result = null;
                return false;
            }
        }

        private bool TryCommit(
            ConfirmWeekPlanCommand command,
            CareerSaveSnapshot next,
            out CareerPersistenceResult result)
        {
            try
            {
                result = _repository.Commit(
                    command.ProfileId,
                    command.SaveId,
                    command.ExpectedVersionToken,
                    next,
                    command.OperationId);
                return result != null;
            }
            catch (Exception)
            {
                result = null;
                return false;
            }
        }

        private static CareerWeekCommandResult Existing(
            CareerSaveSnapshot snapshot,
            OperationReceipt receipt,
            PersistenceResultKind persistenceKind)
        {
            return Result(
                CareerApplicationStatus.Existing,
                persistenceKind,
                snapshot,
                null,
                receipt.OutcomeSummary);
        }

        private static void RequireSupportedVersions(CareerSaveVersions versions)
        {
            if (versions.SchemaVersion != CareerSaveVersions.CurrentSchemaVersion ||
                versions.ContentVersion != CareerSaveVersions.CurrentContentVersion ||
                versions.RulesetVersion != CareerSaveVersions.CurrentRulesetVersion ||
                versions.CareerRandomAlgorithmVersion !=
                CareerSaveVersions.CurrentCareerRandomAlgorithmVersion)
            {
                throw new ArgumentException("The career version combination is unsupported.");
            }
        }

        private static void RequireGuid(Guid value, string parameterName)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A stable non-empty ID is required.", parameterName);
            }
        }

        private static void RequireTimestamp(long value, string parameterName)
        {
            if (value < 0 || value > MaximumIJsonSafeInteger)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool HasSnapshot(CareerPersistenceResult result)
        {
            return result != null &&
                   (result.Kind == PersistenceResultKind.Loaded ||
                   result.Kind == PersistenceResultKind.Created ||
                   result.Kind == PersistenceResultKind.Committed ||
                   result.Kind == PersistenceResultKind.BackupDegraded);
        }

        private static CareerWeekCommandResult Result(
            CareerApplicationStatus status,
            PersistenceResultKind? persistenceKind = null,
            CareerSaveSnapshot snapshot = null,
            OperationReceipt conflict = null,
            OperationOutcomeSummary outcomeSummary = null)
        {
            return new CareerWeekCommandResult(
                status,
                persistenceKind,
                snapshot,
                conflict,
                outcomeSummary);
        }
    }
}
