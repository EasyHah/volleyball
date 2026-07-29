using System;
using System.Collections.Generic;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public sealed class CareerOnboardingService
    {
        private readonly ICareerSaveRepository _repository;
        private readonly ICareerSeedSource _seedSource;
        private readonly IDeterministicCareerRandom _random;
        private readonly TryoutCatalog _catalog;

        public CareerOnboardingService(
            ICareerSaveRepository repository,
            ICareerSeedSource seedSource,
            IDeterministicCareerRandom random,
            TryoutCatalog catalog)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (_catalog.ContentVersion != CareerSaveVersions.CurrentContentVersion ||
                _catalog.RulesetVersion != CareerSaveVersions.CurrentRulesetVersion)
            {
                throw new ArgumentException(
                    "The tryout catalog does not match current content and rules versions.",
                    nameof(catalog));
            }
        }

        public CareerApplicationResult CreateCareer(CreateCareerCommand command)
        {
            Sha256Digest fingerprint;
            try
            {
                ValidateCreate(command);
                fingerprint = CareerOperationFingerprintV2.Hash(command);
            }
            catch (ArgumentException)
            {
                return Result(CareerApplicationStatus.InvalidInputOrState);
            }

            var loaded = _repository.Load(command.ProfileId, command.SaveId);
            if (HasSnapshot(loaded))
            {
                return ExistingCreateResult(loaded.Snapshot, command.OperationId, fingerprint);
            }

            if (loaded.Kind != PersistenceResultKind.NotFound)
            {
                return Result(
                    CareerApplicationStatus.PersistenceFailure,
                    loaded.Kind);
            }

            var seed = _seedSource.GenerateSeed();
            if (seed == null)
            {
                return Result(CareerApplicationStatus.PersistenceFailure);
            }

            CareerSaveSnapshot initial;
            try
            {
                initial = BuildInitialSnapshot(command, fingerprint, seed);
            }
            catch (ArgumentException)
            {
                return Result(CareerApplicationStatus.InvalidInputOrState);
            }

            var created = _repository.Create(initial, command.OperationId);
            if (created.Kind == PersistenceResultKind.Created ||
                created.Kind == PersistenceResultKind.BackupDegraded)
            {
                return Result(
                    CareerApplicationStatus.Applied,
                    created.Kind,
                    created.Snapshot);
            }

            if (created.Kind == PersistenceResultKind.AlreadyExists)
            {
                var authoritative = _repository.Load(command.ProfileId, command.SaveId);
                if (HasSnapshot(authoritative))
                {
                    return ExistingCreateResult(
                        authoritative.Snapshot,
                        command.OperationId,
                        fingerprint);
                }

                return Result(
                    CareerApplicationStatus.PersistenceFailure,
                    authoritative.Kind);
            }

            return Result(CareerApplicationStatus.PersistenceFailure, created.Kind);
        }

        public CareerApplicationResult ConfirmTryoutStage(
            ConfirmTryoutStageCommand command)
        {
            if (command == null)
            {
                return Result(CareerApplicationStatus.InvalidInputOrState);
            }

            var loaded = _repository.Load(command.ProfileId, command.SaveId);
            if (loaded.Kind == PersistenceResultKind.NotFound)
            {
                return Result(CareerApplicationStatus.NotFound, loaded.Kind);
            }

            if (!HasSnapshot(loaded))
            {
                return Result(CareerApplicationStatus.PersistenceFailure, loaded.Kind);
            }

            var snapshot = loaded.Snapshot;
            Sha256Digest fingerprint;
            try
            {
                ValidateConfirmCommand(command);
                fingerprint = CareerOperationFingerprintV2.Hash(
                    command,
                    snapshot.Onboarding.Stages[command.StageNumber - 1].OccurrenceId);
            }
            catch (ArgumentException)
            {
                return Result(
                    CareerApplicationStatus.InvalidInputOrState,
                    loaded.Kind,
                    snapshot);
            }

            var receiptLookup = new OperationReceiptIndex(snapshot.OperationReceipts)
                .Find(command.OperationId, fingerprint);
            if (receiptLookup.Kind == OperationReceiptLookupKind.Existing)
            {
                return ExistingStageResult(snapshot, receiptLookup.Receipt, loaded.Kind);
            }

            if (receiptLookup.Kind == OperationReceiptLookupKind.Conflict)
            {
                return Result(
                    CareerApplicationStatus.OperationConflict,
                    loaded.Kind,
                    snapshot,
                    receiptLookup.Receipt);
            }

            if (!snapshot.Identity.VersionToken.Equals(command.ExpectedVersionToken))
            {
                return Result(
                    CareerApplicationStatus.VersionConflict,
                    loaded.Kind,
                    snapshot);
            }

            TryoutStageDefinition stage;
            TryoutChoiceDefinition choice;
            try
            {
                ValidateSnapshotForConfirmation(snapshot, command);
                stage = _catalog.GetStage(command.StageNumber);
                choice = stage.FindChoice(command.ChoiceId);
                if (choice == null)
                {
                    return Result(
                        CareerApplicationStatus.InvalidInputOrState,
                        loaded.Kind,
                        snapshot);
                }
            }
            catch (ArgumentException)
            {
                return Result(
                    CareerApplicationStatus.InvalidInputOrState,
                    loaded.Kind,
                    snapshot);
            }

            var resolved = ResolveOutputs(snapshot, stage, choice);
            var explanations = Explain(stage, choice, resolved);
            CareerSaveSnapshot next;
            try
            {
                next = BuildNextSnapshot(
                    snapshot,
                    command,
                    fingerprint,
                    resolved);
            }
            catch (ArgumentException)
            {
                return Result(
                    CareerApplicationStatus.InvalidInputOrState,
                    loaded.Kind,
                    snapshot);
            }

            var committed = _repository.Commit(
                command.ProfileId,
                command.SaveId,
                command.ExpectedVersionToken,
                next,
                command.OperationId);
            if (committed.Kind == PersistenceResultKind.Committed ||
                committed.Kind == PersistenceResultKind.BackupDegraded)
            {
                return Result(
                    CareerApplicationStatus.Applied,
                    committed.Kind,
                    committed.Snapshot,
                    null,
                    resolved,
                    explanations);
            }

            if (committed.Kind == PersistenceResultKind.VersionConflict)
            {
                var latest = _repository.Load(command.ProfileId, command.SaveId);
                if (!HasSnapshot(latest))
                {
                    return Result(
                        CareerApplicationStatus.PersistenceFailure,
                        latest.Kind);
                }

                var latestLookup = new OperationReceiptIndex(
                    latest.Snapshot.OperationReceipts).Find(
                    command.OperationId,
                    fingerprint);
                if (latestLookup.Kind == OperationReceiptLookupKind.Existing)
                {
                    return ExistingStageResult(
                        latest.Snapshot,
                        latestLookup.Receipt,
                        latest.Kind);
                }

                if (latestLookup.Kind == OperationReceiptLookupKind.Conflict)
                {
                    return Result(
                        CareerApplicationStatus.OperationConflict,
                        latest.Kind,
                        latest.Snapshot,
                        latestLookup.Receipt);
                }

                return Result(
                    CareerApplicationStatus.VersionConflict,
                    committed.Kind,
                    latest.Snapshot);
            }

            return Result(
                CareerApplicationStatus.PersistenceFailure,
                committed.Kind,
                snapshot);
        }

        private CareerApplicationResult ExistingCreateResult(
            CareerSaveSnapshot snapshot,
            OperationId operationId,
            Sha256Digest fingerprint)
        {
            var lookup = new OperationReceiptIndex(snapshot.OperationReceipts)
                .Find(operationId, fingerprint);
            switch (lookup.Kind)
            {
                case OperationReceiptLookupKind.Existing:
                    return Result(CareerApplicationStatus.Existing, null, snapshot);
                case OperationReceiptLookupKind.Conflict:
                    return Result(
                        CareerApplicationStatus.OperationConflict,
                        null,
                        snapshot,
                        lookup.Receipt);
                default:
                    return Result(CareerApplicationStatus.InvalidInputOrState, null, snapshot);
            }
        }

        private CareerApplicationResult ExistingStageResult(
            CareerSaveSnapshot snapshot,
            OperationReceipt receipt,
            PersistenceResultKind persistenceKind)
        {
            try
            {
                RequireSupportedVersions(snapshot.Versions);
                var stage = _catalog.GetStage(receipt.Target.TryoutStage);
                var choice = stage.FindChoice(receipt.Target.ChoiceId);
                if (choice == null)
                {
                    return Result(
                        CareerApplicationStatus.InvalidInputOrState,
                        persistenceKind,
                        snapshot);
                }

                var outputs = receipt.OutcomeSummary.TryoutResolvedOutputs;
                return Result(
                    CareerApplicationStatus.Existing,
                    persistenceKind,
                    snapshot,
                    null,
                    outputs,
                    Explain(stage, choice, outputs));
            }
            catch (ArgumentException)
            {
                return Result(
                    CareerApplicationStatus.InvalidInputOrState,
                    persistenceKind,
                    snapshot);
            }
        }

        private static CareerSaveSnapshot BuildInitialSnapshot(
            CreateCareerCommand command,
            Sha256Digest fingerprint,
            CareerSeed seed)
        {
            var receipt = new OperationReceipt(
                command.OperationId,
                OperationKind.CreateCareer,
                OperationReceiptTarget.ForCreateCareer(),
                fingerprint,
                command.LineageId,
                1,
                command.CompletedAtUtcMs,
                OperationOutcomeKind.CareerCreated,
                OperationOutcomeSummary.ForCareerCreated());
            var stages = new TryoutStageState[3];
            for (var index = 0; index < stages.Length; index++)
            {
                stages[index] = new TryoutStageState(
                    index + 1,
                    command.TryoutOccurrenceIds[index],
                    CareerSaveVersions.CurrentCareerRandomAlgorithmVersion,
                    null,
                    Array.Empty<TryoutResolvedOutput>());
            }

            return CareerOnboardingSnapshotFactory.CreateInitial(
                command.ProfileId,
                command.SaveId,
                command.LineageId,
                seed,
                command.CareerName,
                command.PlayerStableId,
                command.PlayerName,
                command.JerseyNumber,
                new TryoutOnboardingState(stages, 1, false),
                receipt,
                command.CompletedAtUtcMs);
        }

        private CareerSaveSnapshot BuildNextSnapshot(
            CareerSaveSnapshot prior,
            ConfirmTryoutStageCommand command,
            Sha256Digest fingerprint,
            IReadOnlyList<TryoutResolvedOutput> resolved)
        {
            var stages = new TryoutStageState[3];
            for (var index = 0; index < stages.Length; index++)
            {
                var old = prior.Onboarding.Stages[index];
                stages[index] = old.StageNumber == command.StageNumber
                    ? new TryoutStageState(
                        old.StageNumber,
                        old.OccurrenceId,
                        old.RandomVersion,
                        command.ChoiceId,
                        resolved)
                    : new TryoutStageState(
                        old.StageNumber,
                        old.OccurrenceId,
                        old.RandomVersion,
                        old.ChoiceId,
                        old.ResolvedOutputs);
            }

            var nextRevision = prior.Identity.Revision + 1;
            var receipts = new List<OperationReceipt>(prior.OperationReceipts)
            {
                new OperationReceipt(
                    command.OperationId,
                    OperationKind.ConfirmTryoutStage,
                    OperationReceiptTarget.ForTryoutStage(
                        command.StageNumber,
                        stages[command.StageNumber - 1].OccurrenceId,
                        command.ChoiceId),
                    fingerprint,
                    prior.Identity.LineageId,
                    nextRevision,
                    command.CompletedAtUtcMs,
                    OperationOutcomeKind.TryoutAdvanced,
                    OperationOutcomeSummary.ForTryoutAdvanced(resolved))
            };
            if (command.StageNumber < 3)
            {
                return CareerOnboardingSnapshotFactory.Advance(
                    prior,
                    command.CompletedAtUtcMs,
                    new TryoutOnboardingState(stages, command.StageNumber + 1, false),
                    CareerProgressionState.Tryout(command.StageNumber + 1),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    receipts);
            }

            return BuildEnrolledSnapshot(prior, command, stages, receipts);
        }

        private CareerSaveSnapshot BuildEnrolledSnapshot(
            CareerSaveSnapshot prior,
            ConfirmTryoutStageCommand command,
            TryoutStageState[] stages,
            IReadOnlyList<OperationReceipt> receipts)
        {
            var values = new Dictionary<TryoutOutputKind, int>();
            for (var stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                var stageDefinition = _catalog.GetStage(stageIndex + 1);
                var choice = stageDefinition.FindChoice(stages[stageIndex].ChoiceId);
                var explanations = Explain(
                    stageDefinition,
                    choice,
                    stages[stageIndex].ResolvedOutputs);
                for (var outputIndex = 0; outputIndex < stageDefinition.Outputs.Count; outputIndex++)
                {
                    values.Add(
                        stageDefinition.Outputs[outputIndex].Kind,
                        explanations[outputIndex].FinalValue);
                }
            }

            var attributes = new CareerPlayerAttributes(
                Progress(values[TryoutOutputKind.Spike]),
                Progress(values[TryoutOutputKind.Serve]),
                Progress(values[TryoutOutputKind.Reception]),
                Progress(values[TryoutOutputKind.Defense]),
                Progress(values[TryoutOutputKind.Block]),
                Progress(values[TryoutOutputKind.Movement]),
                Progress(values[TryoutOutputKind.Jump]),
                Progress(values[TryoutOutputKind.Stamina]));
            var match = new CareerWeekActionState(
                command.EnrollmentIds.MatchSlotActionId,
                command.EnrollmentIds.MatchOccurrenceId,
                CareerWeekActionKind.Match,
                "schedule.u1w1.match.01");
            var plan = new CareerWeekPlanState(
                command.EnrollmentIds.WeekPlanId,
                1,
                1,
                new CareerWeekActionState[] { null, null, match },
                false);

            return CareerOnboardingSnapshotFactory.Advance(
                prior,
                command.CompletedAtUtcMs,
                new TryoutOnboardingState(stages, 0, true),
                CareerProgressionState.Planning(plan),
                attributes,
                _catalog.InitialTeamStableId,
                TryoutNumericRulesV1.DerivePotential(
                    attributes.Spike.AbilityBasisPoints,
                    attributes.Serve.AbilityBasisPoints,
                    attributes.Reception.AbilityBasisPoints,
                    attributes.Defense.AbilityBasisPoints,
                    attributes.Block.AbilityBasisPoints,
                    attributes.Movement.AbilityBasisPoints,
                    attributes.Jump.AbilityBasisPoints,
                    attributes.Stamina.AbilityBasisPoints),
                values[TryoutOutputKind.Fatigue],
                values[TryoutOutputKind.Mindset],
                values[TryoutOutputKind.CoachTrust],
                receipts);
        }

        private IReadOnlyList<TryoutResolvedOutput> ResolveOutputs(
            CareerSaveSnapshot snapshot,
            TryoutStageDefinition stage,
            TryoutChoiceDefinition choice)
        {
            var outputs = new TryoutResolvedOutput[stage.Outputs.Count];
            var occurrence = snapshot.Onboarding.Stages[stage.StageNumber - 1].OccurrenceId;
            for (var index = 0; index < outputs.Length; index++)
            {
                var request = new CareerRandomRequest(
                    snapshot.Versions.CareerRandomAlgorithmVersion,
                    snapshot.CareerSeed,
                    "tryout",
                    1,
                    0,
                    choice.ChoiceId,
                    occurrence,
                    index);
                var perturbation = checked((int)_random.NextInt64(request, 0, 201) - 100);
                outputs[index] = new TryoutResolvedOutput(
                    stage.Outputs[index].OutputId,
                    perturbation);
            }

            return outputs;
        }

        private static IReadOnlyList<TryoutOutputExplanation> Explain(
            TryoutStageDefinition stage,
            TryoutChoiceDefinition choice,
            IReadOnlyList<TryoutResolvedOutput> resolved)
        {
            if (choice == null || resolved == null || resolved.Count != stage.Outputs.Count)
            {
                throw new ArgumentException("Persisted tryout output shape does not match content V1.");
            }

            var explanations = new TryoutOutputExplanation[resolved.Count];
            for (var index = 0; index < resolved.Count; index++)
            {
                var definition = stage.Outputs[index];
                if (!string.Equals(
                    definition.OutputId,
                    resolved[index].OutputId,
                    StringComparison.Ordinal))
                {
                    throw new ArgumentException("Persisted tryout output order does not match content V1.");
                }

                explanations[index] = TryoutNumericRulesV1.Explain(
                    stage.StageId,
                    definition,
                    choice.BaseValues[index],
                    resolved[index]);
            }

            return explanations;
        }

        private static CareerAttributeProgress Progress(int ability)
        {
            return new CareerAttributeProgress(ability, 0);
        }

        private static void ValidateCreate(CreateCareerCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            RequireGuid(command.ProfileId.Value, nameof(command.ProfileId));
            RequireGuid(command.SaveId.Value, nameof(command.SaveId));
            RequireGuid(command.LineageId.Value, nameof(command.LineageId));
            RequireGuid(command.OperationId.Value, nameof(command.OperationId));
            RequireText(command.PlayerStableId, nameof(command.PlayerStableId));
            RequireText(command.CareerName, nameof(command.CareerName));
            RequireText(command.PlayerName, nameof(command.PlayerName));
            if (command.JerseyNumber < 1 || command.JerseyNumber > 99)
            {
                throw new ArgumentOutOfRangeException(nameof(command.JerseyNumber));
            }

            if (command.TryoutOccurrenceIds == null ||
                command.TryoutOccurrenceIds.Count != 3)
            {
                throw new ArgumentException("Exactly three tryout occurrences are required.");
            }

            var occurrences = new HashSet<OccurrenceId>();
            for (var index = 0; index < command.TryoutOccurrenceIds.Count; index++)
            {
                RequireGuid(command.TryoutOccurrenceIds[index].Value, "tryoutOccurrenceIds");
                if (!occurrences.Add(command.TryoutOccurrenceIds[index]))
                {
                    throw new ArgumentException("Tryout occurrence IDs must be distinct.");
                }
            }

            RequireTimestamp(command.CompletedAtUtcMs, nameof(command.CompletedAtUtcMs));
        }

        private static void ValidateConfirmCommand(ConfirmTryoutStageCommand command)
        {
            RequireGuid(command.ProfileId.Value, nameof(command.ProfileId));
            RequireGuid(command.SaveId.Value, nameof(command.SaveId));
            RequireGuid(command.OperationId.Value, nameof(command.OperationId));
            RequireGuid(
                command.ExpectedVersionToken.LineageId.Value,
                nameof(command.ExpectedVersionToken));
            if (command.ExpectedVersionToken.Revision < 1 ||
                string.IsNullOrEmpty(command.ExpectedVersionToken.SnapshotHash.Value))
            {
                throw new ArgumentException("A complete expected version token is required.");
            }

            if (command.StageNumber < 1 || command.StageNumber > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(command.StageNumber));
            }

            RequireText(command.ChoiceId, nameof(command.ChoiceId));
            RequireTimestamp(command.CompletedAtUtcMs, nameof(command.CompletedAtUtcMs));
            if (command.EnrollmentIds != null)
            {
                RequireGuid(command.EnrollmentIds.WeekPlanId.Value, "weekPlanId");
                RequireGuid(command.EnrollmentIds.MatchSlotActionId.Value, "matchSlotActionId");
                RequireGuid(command.EnrollmentIds.MatchOccurrenceId.Value, "matchOccurrenceId");
            }
        }

        private static void ValidateSnapshotForConfirmation(
            CareerSaveSnapshot snapshot,
            ConfirmTryoutStageCommand command)
        {
            RequireSupportedVersions(snapshot.Versions);
            if (command.CompletedAtUtcMs < snapshot.Identity.UpdatedAtUtcMs)
            {
                throw new ArgumentException("Completion time cannot precede the authoritative snapshot.");
            }

            if (snapshot.Onboarding.IsComplete ||
                snapshot.Onboarding.CurrentStageNumber != command.StageNumber)
            {
                throw new ArgumentException("Tryout stages cannot be skipped or reapplied.");
            }

            if (command.StageNumber < 3 && command.EnrollmentIds != null)
            {
                throw new ArgumentException("Only tryout stage 3 accepts enrollment IDs.");
            }

            if (command.StageNumber == 3)
            {
                if (command.EnrollmentIds == null)
                {
                    throw new ArgumentException("Tryout stage 3 requires enrollment IDs.");
                }

                for (var index = 0; index < snapshot.Onboarding.Stages.Count; index++)
                {
                    var onboardingOccurrence =
                        snapshot.Onboarding.Stages[index].OccurrenceId.Value;
                    if (onboardingOccurrence == command.EnrollmentIds.WeekPlanId.Value ||
                        onboardingOccurrence == command.EnrollmentIds.MatchSlotActionId.Value ||
                        onboardingOccurrence == command.EnrollmentIds.MatchOccurrenceId.Value)
                    {
                        throw new ArgumentException(
                            "Enrollment IDs cannot collide with a tryout occurrence.");
                    }
                }
            }
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

        private static void RequireGuid(Guid value, string name)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A stable non-empty ID is required.", name);
            }
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", name);
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        throw new ArgumentException("Text contains an unpaired surrogate.", name);
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(value[index]))
                {
                    throw new ArgumentException("Text contains an unpaired surrogate.", name);
                }
            }
        }

        private static void RequireTimestamp(long value, string name)
        {
            if (value < 0 || value > 9007199254740991L)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static bool HasSnapshot(CareerPersistenceResult result)
        {
            return result.Kind == PersistenceResultKind.Loaded ||
                   result.Kind == PersistenceResultKind.Created ||
                   result.Kind == PersistenceResultKind.Committed ||
                   result.Kind == PersistenceResultKind.BackupDegraded;
        }

        private static CareerApplicationResult Result(
            CareerApplicationStatus status,
            PersistenceResultKind? persistenceKind = null,
            CareerSaveSnapshot snapshot = null,
            OperationReceipt conflict = null,
            IEnumerable<TryoutResolvedOutput> outputs = null,
            IEnumerable<TryoutOutputExplanation> explanations = null)
        {
            return new CareerApplicationResult(
                status,
                persistenceKind,
                snapshot,
                conflict,
                outputs,
                explanations);
        }
    }
}
