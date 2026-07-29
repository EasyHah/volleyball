using System;
using System.Collections.Generic;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public sealed class CareerWeekCommandService
    {
        private const long MaximumIJsonSafeInteger = 9007199254740991L;
        private const string FirstWeekMatchContentId = "schedule.u1w1.match.01";
        private const string WeekActionReasonId = "reason.week_action";
        private const string TeamMealEventId = "event.team_meal";

        private readonly ICareerSaveRepository _repository;
        private readonly IDeterministicCareerRandom _random;

        public CareerWeekCommandService(
            ICareerSaveRepository repository,
            IDeterministicCareerRandom random)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public CareerWeekCommandResult ConfirmWeekPlan(ConfirmWeekPlanCommand command)
        {
            Sha256Digest fingerprint;
            try
            {
                ValidateCommandShape(command);
                fingerprint = CareerOperationFingerprintV2.Hash(command);
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

        public CareerWeekCommandResult ExecuteWeekAction(ExecuteWeekActionCommand command)
        {
            Sha256Digest fingerprint;
            try
            {
                ValidateExecuteCommandShape(command);
                fingerprint = CareerOperationFingerprintV2.Hash(command);
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

            try
            {
                ValidateExecuteTransition(authoritative, command);
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

            CareerSaveSnapshot next;
            try
            {
                next = BuildExecutedSnapshot(authoritative, command, fingerprint);
            }
            catch (CareerRandomDependencyException)
            {
                return Result(
                    CareerApplicationStatus.PersistenceFailure,
                    null,
                    authoritative);
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

        public CareerWeekCommandResult ResolveEventChoice(ResolveEventChoiceCommand command)
        {
            Sha256Digest fingerprint;
            try
            {
                ValidateResolveCommandShape(command);
                fingerprint = CareerOperationFingerprintV2.Hash(command);
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
                ValidateResolveTransition(authoritative, command);
                next = BuildResolvedSnapshot(authoritative, command, fingerprint);
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

        private CareerWeekCommandResult ResolveCommitRace(
            ExecuteWeekActionCommand command,
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

        private CareerWeekCommandResult ResolveCommitRace(
            ResolveEventChoiceCommand command,
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

        private CareerSaveSnapshot BuildExecutedSnapshot(
            CareerSaveSnapshot prior,
            ExecuteWeekActionCommand command,
            Sha256Digest fingerprint)
        {
            var plan = prior.Progression.WeekPlan;
            var action = plan.Slots[command.SlotNumber - 1];
            var catalog = CareerWeekActionCatalogV1.Create();
            var definition = catalog.Find(action.ContentId);
            var calculation = CareerWeekActionRulesV1.Calculate(
                WeekActionReasonId,
                action.ContentId,
                prior.PotentialGrade.Value,
                prior.Player.Attributes,
                prior.Fatigue.Value,
                prior.Mindset.Value,
                prior.CoachTrust.Value);

            var emphases = prior.TrainingEmphases;
            if (definition.Kind == CareerWeekActionKind.SpecializedTraining ||
                definition.Kind == CareerWeekActionKind.StrengthTraining)
            {
                emphases = emphases.AddExecutedTraining(action, catalog);
            }

            CareerProgressionState progression;
            if (command.SlotNumber == 1)
            {
                var eventCatalog = CareerSocialEventCatalogV1.Create();
                var eventDefinition = eventCatalog.Events[0];
                if (!string.Equals(eventDefinition.EventId, TeamMealEventId, StringComparison.Ordinal) ||
                    eventDefinition.Options.Count != 2)
                {
                    throw new InvalidOperationException("The fixed team-meal event catalog has drifted.");
                }

                var effects = new CareerEventOptionEffect[2];
                for (var index = 0; index < eventDefinition.Options.Count; index++)
                {
                    var option = eventDefinition.Options[index];
                    var request = new CareerRandomRequest(
                        prior.Versions.CareerRandomAlgorithmVersion,
                        prior.CareerSeed,
                        "event",
                        plan.Season,
                        plan.Week,
                        option.OptionId,
                        command.TriggeredEventOccurrenceId.Value,
                        0);
                    var roll = checked((int)NextRandomInt64(request, 0, 10000));
                    effects[index] = CareerEventRulesV1.Resolve(
                        prior.Versions.ContentVersion,
                        prior.Versions.RulesetVersion,
                        eventDefinition.EventId,
                        option.OptionId,
                        roll,
                        prior.PotentialGrade.Value,
                        calculation.NextAttributes,
                        calculation.Fatigue.NewValue,
                        calculation.Mindset.NewValue,
                        calculation.CoachTrust.NewValue);
                }

                var pending = new PendingCareerEvent(
                    plan.PlanId,
                    action.SlotActionId,
                    action.OccurrenceId,
                    eventDefinition.EventId,
                    command.TriggeredEventOccurrenceId.Value,
                    prior.Versions.CareerRandomAlgorithmVersion,
                    effects[0],
                    effects[1],
                    2);
                progression = CareerProgressionState.AwaitingEventChoice(plan, pending);
            }
            else
            {
                progression = CareerProgressionState.Planned(plan, 3);
            }

            var outcome = OperationOutcomeSummary.ForSlotCompleted(
                calculation.GrowthExperienceDelta,
                calculation.Fatigue.Delta,
                calculation.Mindset.Delta,
                calculation.CoachTrust.Delta);
            var nextRevision = checked(prior.Identity.Revision + 1);
            var receipts = new List<OperationReceipt>(prior.OperationReceipts)
            {
                new OperationReceipt(
                    command.OperationId,
                    OperationKind.ExecuteWeekAction,
                    OperationReceiptTarget.ForWeekAction(
                        plan.PlanId,
                        action.SlotActionId,
                        action.OccurrenceId),
                    fingerprint,
                    prior.Identity.LineageId,
                    nextRevision,
                    command.CompletedAtUtcMs,
                    OperationOutcomeKind.SlotCompleted,
                    outcome)
            };
            return CareerWeekSnapshotFactory.Advance(
                prior,
                command.CompletedAtUtcMs,
                progression,
                emphases,
                calculation.NextAttributes,
                calculation.Fatigue.NewValue,
                calculation.Mindset.NewValue,
                calculation.CoachTrust.NewValue,
                receipts);
        }

        private static CareerSaveSnapshot BuildResolvedSnapshot(
            CareerSaveSnapshot prior,
            ResolveEventChoiceCommand command,
            Sha256Digest fingerprint)
        {
            var pending = prior.Progression.PendingEvent;
            CareerEventOptionEffect selected = null;
            for (var index = 0; index < pending.Options.Count; index++)
            {
                if (string.Equals(
                        pending.Options[index].OptionId,
                        command.OptionId,
                        StringComparison.Ordinal))
                {
                    selected = pending.Options[index];
                    break;
                }
            }

            if (selected == null)
            {
                throw new ArgumentException("The selected option is not frozen in the pending event.");
            }

            var priorAttributes = prior.Player.Attributes;
            var growth = selected.GrowthExperienceDelta;
            var nextAttributes = new CareerPlayerAttributes(
                priorAttributes.Spike.AddGrowthExperience(growth.Spike),
                priorAttributes.Serve.AddGrowthExperience(growth.Serve),
                priorAttributes.Reception.AddGrowthExperience(growth.Reception),
                priorAttributes.Defense.AddGrowthExperience(growth.Defense),
                priorAttributes.Block.AddGrowthExperience(growth.Block),
                priorAttributes.Movement.AddGrowthExperience(growth.Movement),
                priorAttributes.Jump.AddGrowthExperience(growth.Jump),
                priorAttributes.Stamina.AddGrowthExperience(growth.Stamina));
            var fatigue = checked(prior.Fatigue.Value + selected.FatigueDelta);
            var mindset = checked(prior.Mindset.Value + selected.MindsetDelta);
            var coachTrust = checked(prior.CoachTrust.Value + selected.CoachTrustDelta);
            RequireStatus(fatigue, nameof(fatigue));
            RequireStatus(mindset, nameof(mindset));
            RequireStatus(coachTrust, nameof(coachTrust));

            var outcome = OperationOutcomeSummary.ForEventChoiceApplied(
                growth,
                selected.FatigueDelta,
                selected.MindsetDelta,
                selected.CoachTrustDelta);
            var nextRevision = checked(prior.Identity.Revision + 1);
            var receipts = new List<OperationReceipt>(prior.OperationReceipts)
            {
                new OperationReceipt(
                    command.OperationId,
                    OperationKind.ResolveEventChoice,
                    OperationReceiptTarget.ForEventChoice(
                        command.WeekPlanId,
                        command.SourceSlotActionId,
                        command.SourceActionOccurrenceId,
                        command.EventOccurrenceId,
                        command.OptionId),
                    fingerprint,
                    prior.Identity.LineageId,
                    nextRevision,
                    command.CompletedAtUtcMs,
                    OperationOutcomeKind.EventChoiceApplied,
                    outcome)
            };
            return CareerWeekSnapshotFactory.Advance(
                prior,
                command.CompletedAtUtcMs,
                CareerProgressionState.Planned(prior.Progression.WeekPlan, 2),
                prior.TrainingEmphases,
                nextAttributes,
                fatigue,
                mindset,
                coachTrust,
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

        private static void ValidateExecuteCommandShape(ExecuteWeekActionCommand command)
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
            RequireGuid(command.WeekPlanId.Value, nameof(command.WeekPlanId));
            if (command.SlotNumber != 1 && command.SlotNumber != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(command.SlotNumber));
            }

            RequireGuid(command.SlotActionId.Value, nameof(command.SlotActionId));
            RequireGuid(command.ActionOccurrenceId.Value, nameof(command.ActionOccurrenceId));
            if (string.IsNullOrWhiteSpace(command.ContentId))
            {
                throw new ArgumentException("A strict action content ID is required.");
            }

            if (command.SlotNumber == 1)
            {
                if (!command.TriggeredEventOccurrenceId.HasValue)
                {
                    throw new ArgumentException("Slot 1 requires an event occurrence ID.");
                }

                RequireGuid(
                    command.TriggeredEventOccurrenceId.Value.Value,
                    nameof(command.TriggeredEventOccurrenceId));
            }
            else if (command.TriggeredEventOccurrenceId.HasValue)
            {
                throw new ArgumentException("Slot 2 cannot create an event occurrence.");
            }
        }

        private static void ValidateResolveCommandShape(ResolveEventChoiceCommand command)
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
            RequireGuid(command.WeekPlanId.Value, nameof(command.WeekPlanId));
            RequireGuid(command.SourceSlotActionId.Value, nameof(command.SourceSlotActionId));
            RequireGuid(
                command.SourceActionOccurrenceId.Value,
                nameof(command.SourceActionOccurrenceId));
            RequireGuid(command.EventOccurrenceId.Value, nameof(command.EventOccurrenceId));
            if (string.IsNullOrWhiteSpace(command.EventId) ||
                string.IsNullOrWhiteSpace(command.OptionId))
            {
                throw new ArgumentException("Strict event and option IDs are required.");
            }
        }

        private static void ValidateResolveTransition(
            CareerSaveSnapshot snapshot,
            ResolveEventChoiceCommand command)
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

            if (snapshot.Identity.Revision >= MaximumIJsonSafeInteger)
            {
                throw new ArgumentException("The authoritative snapshot revision cannot advance safely.");
            }

            if (snapshot.Progression.Kind != CareerProgressionKind.AwaitingEventChoice ||
                snapshot.Progression.Phase != CareerPhase.University ||
                !snapshot.Onboarding.IsComplete ||
                !snapshot.Onboarding.IsFormallyEnrolled ||
                !snapshot.HasCompletePlayer ||
                !snapshot.PotentialGrade.HasValue ||
                !snapshot.Fatigue.HasValue ||
                !snapshot.Mindset.HasValue ||
                !snapshot.CoachTrust.HasValue)
            {
                throw new ArgumentException(
                    "Only a complete enrolled AwaitingEventChoice snapshot can resolve an event.");
            }

            var plan = snapshot.Progression.WeekPlan;
            var pending = snapshot.Progression.PendingEvent;
            if (plan == null ||
                !plan.IsConfirmed ||
                plan.Season != 1 ||
                plan.Week != 1 ||
                plan.Slots == null ||
                plan.Slots.Count != CareerWeekPlan.SlotCount ||
                plan.Slots[0] == null ||
                pending == null ||
                !plan.PlanId.Equals(command.WeekPlanId) ||
                !pending.SourceWeekPlanId.Equals(plan.PlanId) ||
                !pending.SourceSlotActionId.Equals(plan.Slots[0].SlotActionId) ||
                !pending.SourceActionOccurrenceId.Equals(plan.Slots[0].OccurrenceId) ||
                !pending.SourceWeekPlanId.Equals(command.WeekPlanId) ||
                !pending.SourceSlotActionId.Equals(command.SourceSlotActionId) ||
                !pending.SourceActionOccurrenceId.Equals(command.SourceActionOccurrenceId) ||
                !pending.OccurrenceId.Equals(command.EventOccurrenceId) ||
                !string.Equals(pending.EventId, command.EventId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The command does not match the authoritative pending event.");
            }

            if (!string.Equals(pending.EventId, TeamMealEventId, StringComparison.Ordinal) ||
                pending.ResumeAtSlotNumber != 2 ||
                pending.RandomVersion != snapshot.Versions.CareerRandomAlgorithmVersion ||
                pending.Options == null ||
                pending.Options.Count != 2 ||
                !string.Equals(
                    pending.Options[0].OptionId,
                    "event.team_meal.option.attend",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    pending.Options[1].OptionId,
                    "event.team_meal.option.extra_practice",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException("The frozen schema V1 team-meal fixture is invalid.");
            }

            if (!string.Equals(command.OptionId, pending.Options[0].OptionId, StringComparison.Ordinal) &&
                !string.Equals(command.OptionId, pending.Options[1].OptionId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The selected option is not frozen in the pending event.");
            }

            var hasSlotOneReceipt = false;
            for (var index = 0; index < snapshot.OperationReceipts.Count; index++)
            {
                var receipt = snapshot.OperationReceipts[index];
                if (receipt.OperationKind == OperationKind.ExecuteWeekAction &&
                    receipt.Target.WeekPlanId.HasValue &&
                    receipt.Target.SlotActionId.HasValue &&
                    receipt.Target.ActionOccurrenceId.HasValue &&
                    receipt.Target.WeekPlanId.Value.Equals(plan.PlanId) &&
                    receipt.Target.SlotActionId.Value.Equals(plan.Slots[0].SlotActionId) &&
                    receipt.Target.ActionOccurrenceId.Value.Equals(plan.Slots[0].OccurrenceId))
                {
                    hasSlotOneReceipt = true;
                    break;
                }
            }

            if (!hasSlotOneReceipt)
            {
                throw new ArgumentException("The pending event requires its applied slot-1 frontier.");
            }
        }

        private static void ValidateExecuteTransition(
            CareerSaveSnapshot snapshot,
            ExecuteWeekActionCommand command)
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

            if (snapshot.Identity.Revision >= MaximumIJsonSafeInteger)
            {
                throw new ArgumentException("The authoritative snapshot revision cannot advance safely.");
            }

            if (snapshot.Progression.Kind != CareerProgressionKind.Planned ||
                snapshot.Progression.Phase != CareerPhase.University ||
                !snapshot.Onboarding.IsComplete ||
                !snapshot.Onboarding.IsFormallyEnrolled ||
                !snapshot.HasCompletePlayer ||
                !snapshot.PotentialGrade.HasValue ||
                !snapshot.Fatigue.HasValue ||
                !snapshot.Mindset.HasValue ||
                !snapshot.CoachTrust.HasValue)
            {
                throw new ArgumentException("Only a complete enrolled Planned snapshot can execute an action.");
            }

            var plan = snapshot.Progression.WeekPlan;
            if (plan == null ||
                !plan.IsConfirmed ||
                plan.Season != 1 ||
                plan.Week != 1 ||
                plan.Slots == null ||
                plan.Slots.Count != CareerWeekPlan.SlotCount ||
                snapshot.Progression.NextSlotNumber != command.SlotNumber ||
                !plan.PlanId.Equals(command.WeekPlanId))
            {
                throw new ArgumentException("The authoritative plan frontier does not match the command.");
            }

            var action = plan.Slots[command.SlotNumber - 1];
            if (action == null ||
                !action.SlotActionId.Equals(command.SlotActionId) ||
                !action.OccurrenceId.Equals(command.ActionOccurrenceId) ||
                !string.Equals(action.ContentId, command.ContentId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The command action does not match the authoritative slot.");
            }

            var definition = CareerWeekActionCatalogV1.Create().Find(action.ContentId);
            if (definition == null ||
                definition.Kind != action.Kind ||
                definition.Kind == CareerWeekActionKind.Match)
            {
                throw new ArgumentException("The selected action must exactly match a free-action catalog entry.");
            }

            if (command.SlotNumber == 1)
            {
                var eventOccurrence = command.TriggeredEventOccurrenceId.Value;
                for (var index = 0; index < snapshot.Onboarding.Stages.Count; index++)
                {
                    if (snapshot.Onboarding.Stages[index].OccurrenceId.Equals(eventOccurrence))
                    {
                        throw new ArgumentException("The event occurrence collides with onboarding.");
                    }
                }

                for (var index = 0; index < plan.Slots.Count; index++)
                {
                    if (plan.Slots[index] == null ||
                        plan.Slots[index].OccurrenceId.Equals(eventOccurrence))
                    {
                        throw new ArgumentException("The event occurrence collides with the plan.");
                    }
                }
            }
            else
            {
                var first = plan.Slots[0];
                var hasActionReceipt = false;
                var hasEventReceipt = false;
                for (var index = 0; index < snapshot.OperationReceipts.Count; index++)
                {
                    var receipt = snapshot.OperationReceipts[index];
                    if (receipt.OperationKind == OperationKind.ExecuteWeekAction &&
                        receipt.Target.WeekPlanId.Equals(plan.PlanId) &&
                        receipt.Target.SlotActionId.Equals(first.SlotActionId) &&
                        receipt.Target.ActionOccurrenceId.Equals(first.OccurrenceId))
                    {
                        hasActionReceipt = true;
                    }

                    if (receipt.OperationKind == OperationKind.ResolveEventChoice &&
                        receipt.Target.WeekPlanId.Equals(plan.PlanId) &&
                        receipt.Target.SlotActionId.Equals(first.SlotActionId) &&
                        receipt.Target.ActionOccurrenceId.Equals(first.OccurrenceId))
                    {
                        hasEventReceipt = true;
                    }
                }

                if (!hasActionReceipt || !hasEventReceipt)
                {
                    throw new ArgumentException("Slot 2 requires the completed slot-1 event frontier.");
                }
            }
        }

        private long NextRandomInt64(
            CareerRandomRequest request,
            long minInclusive,
            long maxExclusive)
        {
            try
            {
                return _random.NextInt64(request, minInclusive, maxExclusive);
            }
            catch (Exception exception)
            {
                throw new CareerRandomDependencyException(exception);
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

        private bool TryCommit(
            ExecuteWeekActionCommand command,
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

        private bool TryCommit(
            ResolveEventChoiceCommand command,
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
                versions.ContractVersion != CareerSaveVersions.CurrentContractVersion ||
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

        private static void RequireStatus(int value, string parameterName)
        {
            if (value < 0 || value > 100)
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

        private sealed class CareerRandomDependencyException : Exception
        {
            public CareerRandomDependencyException(Exception innerException)
                : base("The deterministic career random dependency failed.", innerException)
            {
            }
        }
    }
}
