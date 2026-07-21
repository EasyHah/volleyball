using System;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Persistence
{
    public static class CareerSaveSnapshotMapper
    {
        private const long IJsonSafeIntegerMaximum = 9007199254740991L;

        public static CareerSaveDocumentV1 ToDocument(CareerSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ValidateCurrentVersions(
                snapshot.Versions.SchemaVersion,
                snapshot.Versions.ContentVersion,
                snapshot.Versions.RulesetVersion,
                snapshot.Versions.CareerRandomAlgorithmVersion,
                nameof(snapshot));

            EnsureIJsonSafe(snapshot.Identity.Revision, "identity.revision");
            EnsureIJsonSafe(snapshot.Identity.CreatedAtUtcMs, "identity.createdAtUtcMs");
            EnsureIJsonSafe(snapshot.Identity.UpdatedAtUtcMs, "identity.updatedAtUtcMs");

            var receipts = new OperationReceiptDocumentV1[snapshot.OperationReceipts.Count];
            for (var index = 0; index < receipts.Length; index++)
            {
                receipts[index] = ToDocument(snapshot.OperationReceipts[index]);
            }

            return new CareerSaveDocumentV1
            {
                versions = new CareerSaveVersionsDocumentV1
                {
                    schemaVersion = snapshot.Versions.SchemaVersion,
                    contentVersion = snapshot.Versions.ContentVersion,
                    rulesetVersion = snapshot.Versions.RulesetVersion,
                    careerRandomAlgorithmVersion = snapshot.Versions.CareerRandomAlgorithmVersion
                },
                identity = new CareerSaveIdentityDocumentV1
                {
                    profileId = CanonicalGuid(snapshot.Identity.ProfileId.Value),
                    saveId = CanonicalGuid(snapshot.Identity.SaveId.Value),
                    lineageId = CanonicalGuid(snapshot.Identity.LineageId.Value),
                    revision = snapshot.Identity.Revision,
                    createdAtUtcMs = snapshot.Identity.CreatedAtUtcMs,
                    updatedAtUtcMs = snapshot.Identity.UpdatedAtUtcMs
                },
                integrity = new CareerSaveIntegrityDocumentV1
                {
                    snapshotHash = snapshot.Identity.SnapshotHash.Value
                },
                careerSeed = snapshot.CareerSeed.ToHex(),
                careerName = snapshot.CareerName,
                playerDraft = ToDocument(snapshot.PlayerDraft),
                onboarding = ToDocument(snapshot.Onboarding),
                progression = ToDocument(snapshot.Progression),
                player = ToDocument(snapshot.Player),
                teamId = snapshot.TeamId.HasValue ? snapshot.TeamId.Value.Value : null,
                potentialGrade = snapshot.PotentialGrade.HasValue
                    ? FormatPotentialGrade(snapshot.PotentialGrade.Value)
                    : null,
                fatigue = snapshot.Fatigue,
                mindset = snapshot.Mindset,
                coachTrust = snapshot.CoachTrust,
                operationReceipts = receipts
            };
        }

        public static CareerSaveSnapshot ToDomain(CareerSaveDocumentV1 document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var versionsDocument = Required(document.versions, "versions");
            ValidateCurrentVersions(
                versionsDocument.schemaVersion,
                versionsDocument.contentVersion,
                versionsDocument.rulesetVersion,
                versionsDocument.careerRandomAlgorithmVersion,
                "versions");

            var versions = new CareerSaveVersions(
                versionsDocument.schemaVersion,
                versionsDocument.contentVersion,
                versionsDocument.rulesetVersion,
                versionsDocument.careerRandomAlgorithmVersion);

            var identityDocument = Required(document.identity, "identity");
            var integrityDocument = Required(document.integrity, "integrity");
            EnsureIJsonSafe(identityDocument.revision, "identity.revision");
            EnsureIJsonSafe(identityDocument.createdAtUtcMs, "identity.createdAtUtcMs");
            EnsureIJsonSafe(identityDocument.updatedAtUtcMs, "identity.updatedAtUtcMs");
            var identity = new CareerSaveIdentity(
                new ProfileId(ParseCanonicalGuid(identityDocument.profileId, "identity.profileId")),
                new SaveId(ParseCanonicalGuid(identityDocument.saveId, "identity.saveId")),
                new LineageId(ParseCanonicalGuid(identityDocument.lineageId, "identity.lineageId")),
                identityDocument.revision,
                identityDocument.createdAtUtcMs,
                identityDocument.updatedAtUtcMs,
                Sha256Digest.Parse(integrityDocument.snapshotHash));

            var receiptDocuments = Required(document.operationReceipts, "operationReceipts");
            var receipts = new OperationReceipt[receiptDocuments.Length];
            for (var index = 0; index < receipts.Length; index++)
            {
                receipts[index] = ToDomain(
                    Required(receiptDocuments[index], "operationReceipts[" + index + "]"),
                    index);
            }

            var player = ToDomain(document.player);
            TeamId? teamId = document.teamId == null
                ? (TeamId?)null
                : new TeamId(document.teamId);
            PotentialGrade? potentialGrade = document.potentialGrade == null
                ? (PotentialGrade?)null
                : ParsePotentialGrade(document.potentialGrade);

            return new CareerSaveSnapshot(
                versions,
                identity,
                CareerSeed.Parse(document.careerSeed),
                document.careerName,
                ToDomain(Required(document.playerDraft, "playerDraft")),
                ToDomain(Required(document.onboarding, "onboarding")),
                ToDomain(Required(document.progression, "progression")),
                player,
                teamId,
                potentialGrade,
                document.fatigue,
                document.mindset,
                document.coachTrust,
                receipts);
        }

        private static CareerPlayerDraftDocumentV1 ToDocument(CareerPlayerDraft draft)
        {
            return new CareerPlayerDraftDocumentV1
            {
                playerId = draft.PlayerId.Value,
                displayName = draft.DisplayName,
                jerseyNumber = draft.JerseyNumber
            };
        }

        private static CareerPlayerDraft ToDomain(CareerPlayerDraftDocumentV1 document)
        {
            return new CareerPlayerDraft(
                new PlayerId(document.playerId),
                document.displayName,
                document.jerseyNumber);
        }

        private static TryoutOnboardingDocumentV1 ToDocument(
            TryoutOnboardingState onboarding)
        {
            var stages = new TryoutStageDocumentV1[onboarding.Stages.Count];
            for (var index = 0; index < stages.Length; index++)
            {
                stages[index] = ToDocument(onboarding.Stages[index]);
            }

            return new TryoutOnboardingDocumentV1
            {
                stages = stages,
                nextStageNumber = onboarding.NextStageNumber,
                isFormallyEnrolled = onboarding.IsFormallyEnrolled
            };
        }

        private static TryoutOnboardingState ToDomain(
            TryoutOnboardingDocumentV1 document)
        {
            var stageDocuments = Required(document.stages, "onboarding.stages");
            if (stageDocuments.Length != 3)
            {
                throw new ArgumentException(
                    "Schema V1 onboarding requires exactly three ordered stages.",
                    "onboarding.stages");
            }

            var stages = new TryoutStageState[stageDocuments.Length];
            for (var index = 0; index < stages.Length; index++)
            {
                stages[index] = ToDomain(
                    Required(stageDocuments[index], "onboarding.stages[" + index + "]"),
                    index);
            }

            return new TryoutOnboardingState(
                stages,
                document.nextStageNumber,
                document.isFormallyEnrolled);
        }

        private static TryoutStageDocumentV1 ToDocument(TryoutStageState stage)
        {
            var outputs = new TryoutResolvedOutputDocumentV1[stage.ResolvedOutputs.Count];
            for (var index = 0; index < outputs.Length; index++)
            {
                outputs[index] = ToDocument(stage.ResolvedOutputs[index]);
            }

            return new TryoutStageDocumentV1
            {
                stageNumber = stage.StageNumber,
                occurrenceId = CanonicalGuid(stage.OccurrenceId.Value),
                randomVersion = stage.RandomVersion,
                choiceId = stage.ChoiceId,
                resolvedOutputs = outputs
            };
        }

        private static TryoutStageState ToDomain(
            TryoutStageDocumentV1 document,
            int index)
        {
            var prefix = "onboarding.stages[" + index + "]";
            var outputDocuments = Required(
                document.resolvedOutputs,
                prefix + ".resolvedOutputs");
            var outputs = new TryoutResolvedOutput[outputDocuments.Length];
            for (var outputIndex = 0; outputIndex < outputs.Length; outputIndex++)
            {
                var output = Required(
                    outputDocuments[outputIndex],
                    prefix + ".resolvedOutputs[" + outputIndex + "]");
                outputs[outputIndex] = new TryoutResolvedOutput(
                    output.outputId,
                    output.perturbation);
            }

            return new TryoutStageState(
                document.stageNumber,
                new OccurrenceId(ParseCanonicalGuid(
                    document.occurrenceId,
                    prefix + ".occurrenceId")),
                document.randomVersion,
                document.choiceId,
                outputs);
        }

        private static TryoutResolvedOutputDocumentV1 ToDocument(
            TryoutResolvedOutput output)
        {
            return new TryoutResolvedOutputDocumentV1
            {
                outputId = output.OutputId,
                perturbation = output.Perturbation
            };
        }

        private static CareerProgressionDocumentV1 ToDocument(CareerProgressionState progression)
        {
            return new CareerProgressionDocumentV1
            {
                kind = FormatProgressionKind(progression.Kind),
                phase = FormatPhase(progression.Phase),
                tryoutStage = progression.TryoutStage,
                weekPlan = ToDocument(progression.WeekPlan),
                nextSlotNumber = progression.NextSlotNumber,
                pendingEvent = ToDocument(progression.PendingEvent)
            };
        }

        private static CareerProgressionState ToDomain(CareerProgressionDocumentV1 document)
        {
            return new CareerProgressionState(
                ParseProgressionKind(document.kind),
                ParsePhase(document.phase),
                document.tryoutStage,
                ToDomain(document.weekPlan),
                document.nextSlotNumber,
                ToDomain(document.pendingEvent));
        }

        private static CareerWeekPlanDocumentV1 ToDocument(CareerWeekPlanState weekPlan)
        {
            if (weekPlan == null)
            {
                return null;
            }

            var slots = new CareerWeekActionDocumentV1[weekPlan.Slots.Count];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = ToDocument(weekPlan.Slots[index]);
            }

            return new CareerWeekPlanDocumentV1
            {
                planId = CanonicalGuid(weekPlan.PlanId.Value),
                season = weekPlan.Season,
                week = weekPlan.Week,
                slots = slots,
                isConfirmed = weekPlan.IsConfirmed
            };
        }

        private static CareerWeekPlanState ToDomain(CareerWeekPlanDocumentV1 document)
        {
            if (document == null)
            {
                return null;
            }

            var slotDocuments = Required(document.slots, "progression.weekPlan.slots");
            if (slotDocuments.Length != CareerWeekPlan.SlotCount)
            {
                throw new ArgumentException(
                    "A persisted week plan must contain exactly three ordered slots.",
                    "progression.weekPlan.slots");
            }

            var slots = new CareerWeekActionState[slotDocuments.Length];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = ToDomain(slotDocuments[index]);
            }

            return new CareerWeekPlanState(
                new WeekPlanId(ParseCanonicalGuid(document.planId, "progression.weekPlan.planId")),
                document.season,
                document.week,
                slots,
                document.isConfirmed);
        }

        private static CareerWeekActionDocumentV1 ToDocument(CareerWeekActionState action)
        {
            if (action == null)
            {
                return null;
            }

            return new CareerWeekActionDocumentV1
            {
                slotActionId = CanonicalGuid(action.SlotActionId.Value),
                occurrenceId = CanonicalGuid(action.OccurrenceId.Value),
                kind = FormatWeekActionKind(action.Kind)
            };
        }

        private static CareerWeekActionState ToDomain(CareerWeekActionDocumentV1 document)
        {
            if (document == null)
            {
                return null;
            }

            return new CareerWeekActionState(
                new SlotActionId(ParseCanonicalGuid(
                    document.slotActionId,
                    "progression.weekPlan.slots[].slotActionId")),
                new OccurrenceId(ParseCanonicalGuid(
                    document.occurrenceId,
                    "progression.weekPlan.slots[].occurrenceId")),
                ParseWeekActionKind(document.kind));
        }

        private static PendingCareerEventDocumentV1 ToDocument(PendingCareerEvent pendingEvent)
        {
            if (pendingEvent == null)
            {
                return null;
            }

            var options = new CareerEventOptionEffectDocumentV1[pendingEvent.Options.Count];
            for (var index = 0; index < options.Length; index++)
            {
                options[index] = ToDocument(pendingEvent.Options[index]);
            }

            return new PendingCareerEventDocumentV1
            {
                sourceWeekPlanId = CanonicalGuid(pendingEvent.SourceWeekPlanId.Value),
                sourceSlotActionId = CanonicalGuid(pendingEvent.SourceSlotActionId.Value),
                sourceActionOccurrenceId = CanonicalGuid(
                    pendingEvent.SourceActionOccurrenceId.Value),
                eventId = pendingEvent.EventId,
                occurrenceId = CanonicalGuid(pendingEvent.OccurrenceId.Value),
                randomVersion = pendingEvent.RandomVersion,
                resumeAtSlotNumber = pendingEvent.ResumeAtSlotNumber,
                options = options
            };
        }

        private static PendingCareerEvent ToDomain(PendingCareerEventDocumentV1 document)
        {
            if (document == null)
            {
                return null;
            }

            var optionDocuments = Required(
                document.options,
                "progression.pendingEvent.options");
            if (optionDocuments.Length != 2)
            {
                throw new ArgumentException(
                    "A schema V1 pending event requires exactly two ordered options.",
                    "progression.pendingEvent.options");
            }

            return new PendingCareerEvent(
                new WeekPlanId(ParseCanonicalGuid(
                    document.sourceWeekPlanId,
                    "progression.pendingEvent.sourceWeekPlanId")),
                new SlotActionId(ParseCanonicalGuid(
                    document.sourceSlotActionId,
                    "progression.pendingEvent.sourceSlotActionId")),
                new OccurrenceId(ParseCanonicalGuid(
                    document.sourceActionOccurrenceId,
                    "progression.pendingEvent.sourceActionOccurrenceId")),
                document.eventId,
                new OccurrenceId(ParseCanonicalGuid(
                    document.occurrenceId,
                    "progression.pendingEvent.occurrenceId")),
                document.randomVersion,
                ToDomain(Required(
                    optionDocuments[0],
                    "progression.pendingEvent.options[0]"),
                    0),
                ToDomain(Required(
                    optionDocuments[1],
                    "progression.pendingEvent.options[1]"),
                    1),
                document.resumeAtSlotNumber);
        }

        private static CareerEventOptionEffectDocumentV1 ToDocument(
            CareerEventOptionEffect effect)
        {
            return new CareerEventOptionEffectDocumentV1
            {
                optionId = effect.OptionId,
                growthExperienceDelta = ToDocument(effect.GrowthExperienceDelta),
                fatigueDelta = effect.FatigueDelta,
                mindsetDelta = effect.MindsetDelta,
                coachTrustDelta = effect.CoachTrustDelta
            };
        }

        private static CareerEventOptionEffect ToDomain(
            CareerEventOptionEffectDocumentV1 document,
            int index)
        {
            return new CareerEventOptionEffect(
                document.optionId,
                ToDomain(Required(
                    document.growthExperienceDelta,
                    "progression.pendingEvent.options[" + index + "].growthExperienceDelta")),
                document.fatigueDelta,
                document.mindsetDelta,
                document.coachTrustDelta);
        }

        private static CareerAttributeGrowthDeltaDocumentV1 ToDocument(
            CareerAttributeGrowthDelta delta)
        {
            EnsureIJsonSafe(delta.Spike, "progression.pendingEvent.options[].growthExperienceDelta.spike");
            EnsureIJsonSafe(delta.Serve, "progression.pendingEvent.options[].growthExperienceDelta.serve");
            EnsureIJsonSafe(delta.Reception, "progression.pendingEvent.options[].growthExperienceDelta.reception");
            EnsureIJsonSafe(delta.Defense, "progression.pendingEvent.options[].growthExperienceDelta.defense");
            EnsureIJsonSafe(delta.Block, "progression.pendingEvent.options[].growthExperienceDelta.block");
            EnsureIJsonSafe(delta.Movement, "progression.pendingEvent.options[].growthExperienceDelta.movement");
            EnsureIJsonSafe(delta.Jump, "progression.pendingEvent.options[].growthExperienceDelta.jump");
            EnsureIJsonSafe(delta.Stamina, "progression.pendingEvent.options[].growthExperienceDelta.stamina");
            return new CareerAttributeGrowthDeltaDocumentV1
            {
                spike = delta.Spike,
                serve = delta.Serve,
                reception = delta.Reception,
                defense = delta.Defense,
                block = delta.Block,
                movement = delta.Movement,
                jump = delta.Jump,
                stamina = delta.Stamina
            };
        }

        private static CareerAttributeGrowthDelta ToDomain(
            CareerAttributeGrowthDeltaDocumentV1 document)
        {
            EnsureIJsonSafe(document.spike, "progression.pendingEvent.options[].growthExperienceDelta.spike");
            EnsureIJsonSafe(document.serve, "progression.pendingEvent.options[].growthExperienceDelta.serve");
            EnsureIJsonSafe(document.reception, "progression.pendingEvent.options[].growthExperienceDelta.reception");
            EnsureIJsonSafe(document.defense, "progression.pendingEvent.options[].growthExperienceDelta.defense");
            EnsureIJsonSafe(document.block, "progression.pendingEvent.options[].growthExperienceDelta.block");
            EnsureIJsonSafe(document.movement, "progression.pendingEvent.options[].growthExperienceDelta.movement");
            EnsureIJsonSafe(document.jump, "progression.pendingEvent.options[].growthExperienceDelta.jump");
            EnsureIJsonSafe(document.stamina, "progression.pendingEvent.options[].growthExperienceDelta.stamina");
            return new CareerAttributeGrowthDelta(
                document.spike,
                document.serve,
                document.reception,
                document.defense,
                document.block,
                document.movement,
                document.jump,
                document.stamina);
        }

        private static CareerPlayerDocumentV1 ToDocument(CareerPlayerRecord player)
        {
            if (player == null)
            {
                return null;
            }

            return new CareerPlayerDocumentV1
            {
                playerId = player.PlayerId.Value,
                displayName = player.DisplayName,
                jerseyNumber = player.JerseyNumber,
                attributes = ToDocument(player.Attributes)
            };
        }

        private static CareerPlayerRecord ToDomain(CareerPlayerDocumentV1 document)
        {
            if (document == null)
            {
                return null;
            }

            return new CareerPlayerRecord(
                new PlayerId(document.playerId),
                document.displayName,
                document.jerseyNumber,
                ToDomain(Required(document.attributes, "player.attributes")));
        }

        private static CareerPlayerAttributesDocumentV1 ToDocument(
            CareerPlayerAttributes attributes)
        {
            return new CareerPlayerAttributesDocumentV1
            {
                spike = ToDocument(attributes.Spike),
                serve = ToDocument(attributes.Serve),
                reception = ToDocument(attributes.Reception),
                defense = ToDocument(attributes.Defense),
                block = ToDocument(attributes.Block),
                movement = ToDocument(attributes.Movement),
                jump = ToDocument(attributes.Jump),
                stamina = ToDocument(attributes.Stamina)
            };
        }

        private static CareerPlayerAttributes ToDomain(CareerPlayerAttributesDocumentV1 document)
        {
            return new CareerPlayerAttributes(
                ToDomain(Required(document.spike, "player.attributes.spike")),
                ToDomain(Required(document.serve, "player.attributes.serve")),
                ToDomain(Required(document.reception, "player.attributes.reception")),
                ToDomain(Required(document.defense, "player.attributes.defense")),
                ToDomain(Required(document.block, "player.attributes.block")),
                ToDomain(Required(document.movement, "player.attributes.movement")),
                ToDomain(Required(document.jump, "player.attributes.jump")),
                ToDomain(Required(document.stamina, "player.attributes.stamina")));
        }

        private static CareerAttributeProgressDocumentV1 ToDocument(
            CareerAttributeProgress progress)
        {
            EnsureIJsonSafe(progress.GrowthExperience, "player.attributes[].growthExperience");
            return new CareerAttributeProgressDocumentV1
            {
                abilityBasisPoints = progress.AbilityBasisPoints,
                growthExperience = progress.GrowthExperience
            };
        }

        private static CareerAttributeProgress ToDomain(
            CareerAttributeProgressDocumentV1 document)
        {
            EnsureIJsonSafe(document.growthExperience, "player.attributes[].growthExperience");
            return new CareerAttributeProgress(
                document.abilityBasisPoints,
                document.growthExperience);
        }

        private static OperationReceiptDocumentV1 ToDocument(OperationReceipt receipt)
        {
            EnsureIJsonSafe(receipt.AppliedRevision, "operationReceipts[].appliedRevision");
            EnsureIJsonSafe(receipt.CompletedAtUtcMs, "operationReceipts[].completedAtUtcMs");
            return new OperationReceiptDocumentV1
            {
                operationId = CanonicalGuid(receipt.OperationId.Value),
                operationKind = FormatOperationKind(receipt.OperationKind),
                target = ToDocument(receipt.Target),
                inputFingerprint = receipt.InputFingerprint.Value,
                appliedLineageId = CanonicalGuid(receipt.AppliedLineageId.Value),
                appliedRevision = receipt.AppliedRevision,
                completedAtUtcMs = receipt.CompletedAtUtcMs,
                outcomeKind = FormatOperationOutcomeKind(receipt.OutcomeKind),
                outcomeSummary = ToDocument(receipt.OutcomeSummary)
            };
        }

        private static OperationReceipt ToDomain(
            OperationReceiptDocumentV1 document,
            int index)
        {
            var prefix = "operationReceipts[" + index + "]";
            EnsureIJsonSafe(document.appliedRevision, prefix + ".appliedRevision");
            EnsureIJsonSafe(document.completedAtUtcMs, prefix + ".completedAtUtcMs");
            var operationKind = ParseOperationKind(document.operationKind);
            var outcomeKind = ParseOperationOutcomeKind(document.outcomeKind);
            return new OperationReceipt(
                new OperationId(ParseCanonicalGuid(document.operationId, prefix + ".operationId")),
                operationKind,
                ToDomain(
                    Required(document.target, prefix + ".target"),
                    operationKind,
                    prefix + ".target"),
                Sha256Digest.Parse(document.inputFingerprint),
                new LineageId(ParseCanonicalGuid(
                    document.appliedLineageId,
                    prefix + ".appliedLineageId")),
                document.appliedRevision,
                document.completedAtUtcMs,
                outcomeKind,
                ToDomain(
                    Required(document.outcomeSummary, prefix + ".outcomeSummary"),
                    outcomeKind,
                    prefix + ".outcomeSummary"));
        }

        private static OperationReceiptTargetDocumentV1 ToDocument(
            OperationReceiptTarget target)
        {
            return new OperationReceiptTargetDocumentV1
            {
                tryoutStage = target.TryoutStage,
                tryoutOccurrenceId = target.TryoutOccurrenceId.HasValue
                    ? CanonicalGuid(target.TryoutOccurrenceId.Value.Value)
                    : null,
                choiceId = target.ChoiceId,
                weekPlanId = target.WeekPlanId.HasValue
                    ? CanonicalGuid(target.WeekPlanId.Value.Value)
                    : null,
                slotActionId = target.SlotActionId.HasValue
                    ? CanonicalGuid(target.SlotActionId.Value.Value)
                    : null,
                actionOccurrenceId = target.ActionOccurrenceId.HasValue
                    ? CanonicalGuid(target.ActionOccurrenceId.Value.Value)
                    : null,
                eventOccurrenceId = target.EventOccurrenceId.HasValue
                    ? CanonicalGuid(target.EventOccurrenceId.Value.Value)
                    : null,
                optionId = target.OptionId
            };
        }

        private static OperationReceiptTarget ToDomain(
            OperationReceiptTargetDocumentV1 document,
            OperationKind operationKind,
            string prefix)
        {
            switch (operationKind)
            {
                case OperationKind.CreateCareer:
                    RequireTargetShape(
                        document,
                        prefix,
                        allowTryout: false,
                        allowWeekPlan: false,
                        allowAction: false,
                        allowEvent: false);
                    return OperationReceiptTarget.ForCreateCareer();

                case OperationKind.ConfirmTryoutStage:
                    RequireTargetShape(
                        document,
                        prefix,
                        allowTryout: true,
                        allowWeekPlan: false,
                        allowAction: false,
                        allowEvent: false);
                    return OperationReceiptTarget.ForTryoutStage(
                        document.tryoutStage,
                        new OccurrenceId(ParseCanonicalGuid(
                            document.tryoutOccurrenceId,
                            prefix + ".tryoutOccurrenceId")),
                        document.choiceId);

                case OperationKind.ConfirmWeekPlan:
                    RequireTargetShape(
                        document,
                        prefix,
                        allowTryout: false,
                        allowWeekPlan: true,
                        allowAction: false,
                        allowEvent: false);
                    return OperationReceiptTarget.ForWeekPlanConfirmation(
                        new WeekPlanId(ParseCanonicalGuid(
                            document.weekPlanId,
                            prefix + ".weekPlanId")));

                case OperationKind.ExecuteWeekAction:
                    RequireTargetShape(
                        document,
                        prefix,
                        allowTryout: false,
                        allowWeekPlan: true,
                        allowAction: true,
                        allowEvent: false);
                    return OperationReceiptTarget.ForWeekAction(
                        new WeekPlanId(ParseCanonicalGuid(
                            document.weekPlanId,
                            prefix + ".weekPlanId")),
                        new SlotActionId(ParseCanonicalGuid(
                            document.slotActionId,
                            prefix + ".slotActionId")),
                        new OccurrenceId(ParseCanonicalGuid(
                            document.actionOccurrenceId,
                            prefix + ".actionOccurrenceId")));

                case OperationKind.ResolveEventChoice:
                    RequireTargetShape(
                        document,
                        prefix,
                        allowTryout: false,
                        allowWeekPlan: true,
                        allowAction: true,
                        allowEvent: true);
                    return OperationReceiptTarget.ForEventChoice(
                        new WeekPlanId(ParseCanonicalGuid(
                            document.weekPlanId,
                            prefix + ".weekPlanId")),
                        new SlotActionId(ParseCanonicalGuid(
                            document.slotActionId,
                            prefix + ".slotActionId")),
                        new OccurrenceId(ParseCanonicalGuid(
                            document.actionOccurrenceId,
                            prefix + ".actionOccurrenceId")),
                        new OccurrenceId(ParseCanonicalGuid(
                            document.eventOccurrenceId,
                            prefix + ".eventOccurrenceId")),
                        document.optionId);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(operationKind),
                        operationKind,
                        "Unknown operation kind.");
            }
        }

        private static OperationOutcomeSummaryDocumentV1 ToDocument(
            OperationOutcomeSummary summary)
        {
            var outputs = new TryoutResolvedOutputDocumentV1[
                summary.TryoutResolvedOutputs.Count];
            for (var index = 0; index < outputs.Length; index++)
            {
                outputs[index] = ToDocument(summary.TryoutResolvedOutputs[index]);
            }

            return new OperationOutcomeSummaryDocumentV1
            {
                tryoutResolvedOutputs = outputs,
                growthExperienceDelta = summary.GrowthExperienceDelta == null
                    ? null
                    : ToDocument(summary.GrowthExperienceDelta),
                fatigueDelta = summary.FatigueDelta,
                mindsetDelta = summary.MindsetDelta,
                coachTrustDelta = summary.CoachTrustDelta
            };
        }

        private static OperationOutcomeSummary ToDomain(
            OperationOutcomeSummaryDocumentV1 document,
            OperationOutcomeKind outcomeKind,
            string prefix)
        {
            var outputDocuments = Required(
                document.tryoutResolvedOutputs,
                prefix + ".tryoutResolvedOutputs");

            switch (outcomeKind)
            {
                case OperationOutcomeKind.CareerCreated:
                    RequireEmptyOutcomeSummary(document, outputDocuments, prefix);
                    return OperationOutcomeSummary.ForCareerCreated();

                case OperationOutcomeKind.TryoutAdvanced:
                    if (outputDocuments.Length == 0 ||
                        document.growthExperienceDelta != null ||
                        document.fatigueDelta.HasValue ||
                        document.mindsetDelta.HasValue ||
                        document.coachTrustDelta.HasValue)
                    {
                        throw new ArgumentException(
                            "A tryout summary requires only a non-empty ordered output array.",
                            prefix);
                    }

                    var outputs = new TryoutResolvedOutput[outputDocuments.Length];
                    for (var index = 0; index < outputs.Length; index++)
                    {
                        var output = Required(
                            outputDocuments[index],
                            prefix + ".tryoutResolvedOutputs[" + index + "]");
                        outputs[index] = new TryoutResolvedOutput(
                            output.outputId,
                            output.perturbation);
                    }

                    return OperationOutcomeSummary.ForTryoutAdvanced(outputs);

                case OperationOutcomeKind.WeekPlanConfirmed:
                    RequireEmptyOutcomeSummary(document, outputDocuments, prefix);
                    return OperationOutcomeSummary.ForWeekPlanConfirmed();

                case OperationOutcomeKind.SlotCompleted:
                    RequireAppliedDeltaSummary(document, outputDocuments, prefix);
                    return OperationOutcomeSummary.ForSlotCompleted(
                        ToDomain(document.growthExperienceDelta),
                        document.fatigueDelta.Value,
                        document.mindsetDelta.Value,
                        document.coachTrustDelta.Value);

                case OperationOutcomeKind.EventChoiceApplied:
                    RequireAppliedDeltaSummary(document, outputDocuments, prefix);
                    return OperationOutcomeSummary.ForEventChoiceApplied(
                        ToDomain(document.growthExperienceDelta),
                        document.fatigueDelta.Value,
                        document.mindsetDelta.Value,
                        document.coachTrustDelta.Value);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(outcomeKind),
                        outcomeKind,
                        "Unknown operation outcome kind.");
            }
        }

        private static void RequireEmptyOutcomeSummary(
            OperationOutcomeSummaryDocumentV1 document,
            TryoutResolvedOutputDocumentV1[] outputDocuments,
            string prefix)
        {
            if (outputDocuments.Length != 0 || document.growthExperienceDelta != null ||
                document.fatigueDelta.HasValue || document.mindsetDelta.HasValue ||
                document.coachTrustDelta.HasValue)
            {
                throw new ArgumentException(
                    "This outcome kind requires an explicitly empty summary.",
                    prefix);
            }
        }

        private static void RequireAppliedDeltaSummary(
            OperationOutcomeSummaryDocumentV1 document,
            TryoutResolvedOutputDocumentV1[] outputDocuments,
            string prefix)
        {
            if (outputDocuments.Length != 0 || document.growthExperienceDelta == null ||
                !document.fatigueDelta.HasValue || !document.mindsetDelta.HasValue ||
                !document.coachTrustDelta.HasValue)
            {
                throw new ArgumentException(
                    "An applied action or event outcome requires all delta fields and no tryout outputs.",
                    prefix);
            }
        }

        private static void RequireTargetShape(
            OperationReceiptTargetDocumentV1 document,
            string prefix,
            bool allowTryout,
            bool allowWeekPlan,
            bool allowAction,
            bool allowEvent)
        {
            if (!allowTryout &&
                (document.tryoutStage != 0 || document.tryoutOccurrenceId != null ||
                 document.choiceId != null))
            {
                throw new ArgumentException(
                    "The receipt target contains unexpected tryout fields.",
                    prefix);
            }

            if (!allowWeekPlan && document.weekPlanId != null)
            {
                throw new ArgumentException(
                    "The receipt target contains an unexpected week plan ID.",
                    prefix);
            }

            if (!allowAction &&
                (document.slotActionId != null || document.actionOccurrenceId != null))
            {
                throw new ArgumentException(
                    "The receipt target contains unexpected action fields.",
                    prefix);
            }

            if (!allowEvent &&
                (document.eventOccurrenceId != null || document.optionId != null))
            {
                throw new ArgumentException(
                    "The receipt target contains unexpected event fields.",
                    prefix);
            }

            if (allowTryout &&
                (document.tryoutStage == 0 || document.tryoutOccurrenceId == null ||
                 document.choiceId == null))
            {
                throw new ArgumentException(
                    "A tryout receipt target is incomplete.",
                    prefix);
            }

            if (allowWeekPlan && document.weekPlanId == null)
            {
                throw new ArgumentException(
                    "A week-plan receipt target requires a week plan ID.",
                    prefix);
            }

            if (allowAction &&
                (document.slotActionId == null || document.actionOccurrenceId == null))
            {
                throw new ArgumentException(
                    "An action receipt target is incomplete.",
                    prefix);
            }

            if (allowEvent &&
                (document.eventOccurrenceId == null || document.optionId == null))
            {
                throw new ArgumentException(
                    "An event receipt target is incomplete.",
                    prefix);
            }
        }

        private static Guid ParseCanonicalGuid(string value, string fieldName)
        {
            Guid parsed;
            if (value == null ||
                !Guid.TryParseExact(value, "D", out parsed) ||
                !string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    fieldName + " must be a canonical lowercase GUID in D format.",
                    fieldName);
            }

            if (parsed == Guid.Empty)
            {
                throw new ArgumentException(fieldName + " cannot be empty.", fieldName);
            }

            return parsed;
        }

        private static string CanonicalGuid(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A persisted stable ID cannot be empty.", nameof(value));
            }

            return value.ToString("D");
        }

        private static T Required<T>(T value, string fieldName) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(fieldName, fieldName + " is required by schema V1.");
            }

            return value;
        }

        private static void EnsureIJsonSafe(long value, string fieldName)
        {
            if (value < -IJsonSafeIntegerMaximum || value > IJsonSafeIntegerMaximum)
            {
                throw new ArgumentOutOfRangeException(
                    fieldName,
                    value,
                    fieldName + " must be inside the I-JSON interoperable integer range.");
            }
        }

        private static void ValidateCurrentVersions(
            int schemaVersion,
            int contentVersion,
            int rulesetVersion,
            int careerRandomAlgorithmVersion,
            string parameterName)
        {
            if (schemaVersion != CareerSaveVersions.CurrentSchemaVersion ||
                contentVersion != CareerSaveVersions.CurrentContentVersion ||
                rulesetVersion != CareerSaveVersions.CurrentRulesetVersion ||
                careerRandomAlgorithmVersion !=
                CareerSaveVersions.CurrentCareerRandomAlgorithmVersion)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "The schema V1 mapper only accepts the current value for all four version axes.");
            }
        }

        private static string FormatProgressionKind(CareerProgressionKind value)
        {
            switch (value)
            {
                case CareerProgressionKind.CareerCreated:
                    return "career_created";
                case CareerProgressionKind.Tryout:
                    return "tryout";
                case CareerProgressionKind.Planning:
                    return "planning";
                case CareerProgressionKind.Planned:
                    return "planned";
                case CareerProgressionKind.AwaitingEventChoice:
                    return "awaiting_event_choice";
                default:
                    throw UnknownEnum(nameof(value), value);
            }
        }

        private static CareerProgressionKind ParseProgressionKind(string value)
        {
            switch (value)
            {
                case "career_created":
                    return CareerProgressionKind.CareerCreated;
                case "tryout":
                    return CareerProgressionKind.Tryout;
                case "planning":
                    return CareerProgressionKind.Planning;
                case "planned":
                    return CareerProgressionKind.Planned;
                case "awaiting_event_choice":
                    return CareerProgressionKind.AwaitingEventChoice;
                default:
                    throw UnknownTextEnum("progression.kind", value);
            }
        }

        private static string FormatPhase(CareerPhase value)
        {
            switch (value)
            {
                case CareerPhase.University:
                    return "university";
                default:
                    throw UnknownEnum(nameof(value), value);
            }
        }

        private static CareerPhase ParsePhase(string value)
        {
            if (value == "university")
            {
                return CareerPhase.University;
            }

            throw UnknownTextEnum("progression.phase", value);
        }

        private static string FormatWeekActionKind(CareerWeekActionKind value)
        {
            switch (value)
            {
                case CareerWeekActionKind.SpecializedTraining:
                    return "specialized_training";
                case CareerWeekActionKind.StrengthTraining:
                    return "strength_training";
                case CareerWeekActionKind.TeamPractice:
                    return "team_practice";
                case CareerWeekActionKind.Rest:
                    return "rest";
                case CareerWeekActionKind.Match:
                    return "match";
                default:
                    throw UnknownEnum(nameof(value), value);
            }
        }

        private static CareerWeekActionKind ParseWeekActionKind(string value)
        {
            switch (value)
            {
                case "specialized_training":
                    return CareerWeekActionKind.SpecializedTraining;
                case "strength_training":
                    return CareerWeekActionKind.StrengthTraining;
                case "team_practice":
                    return CareerWeekActionKind.TeamPractice;
                case "rest":
                    return CareerWeekActionKind.Rest;
                case "match":
                    return CareerWeekActionKind.Match;
                default:
                    throw UnknownTextEnum("progression.weekPlan.slots[].kind", value);
            }
        }

        private static string FormatPotentialGrade(PotentialGrade value)
        {
            switch (value)
            {
                case PotentialGrade.D:
                    return "d";
                case PotentialGrade.C:
                    return "c";
                case PotentialGrade.B:
                    return "b";
                case PotentialGrade.A:
                    return "a";
                case PotentialGrade.S:
                    return "s";
                default:
                    throw UnknownEnum(nameof(value), value);
            }
        }

        private static PotentialGrade ParsePotentialGrade(string value)
        {
            switch (value)
            {
                case "d":
                    return PotentialGrade.D;
                case "c":
                    return PotentialGrade.C;
                case "b":
                    return PotentialGrade.B;
                case "a":
                    return PotentialGrade.A;
                case "s":
                    return PotentialGrade.S;
                default:
                    throw UnknownTextEnum("potentialGrade", value);
            }
        }

        private static string FormatOperationKind(OperationKind value)
        {
            switch (value)
            {
                case OperationKind.CreateCareer:
                    return "create_career";
                case OperationKind.ConfirmTryoutStage:
                    return "confirm_tryout_stage";
                case OperationKind.ConfirmWeekPlan:
                    return "confirm_week_plan";
                case OperationKind.ExecuteWeekAction:
                    return "execute_week_action";
                case OperationKind.ResolveEventChoice:
                    return "resolve_event_choice";
                default:
                    throw UnknownEnum(nameof(value), value);
            }
        }

        private static OperationKind ParseOperationKind(string value)
        {
            switch (value)
            {
                case "create_career":
                    return OperationKind.CreateCareer;
                case "confirm_tryout_stage":
                    return OperationKind.ConfirmTryoutStage;
                case "confirm_week_plan":
                    return OperationKind.ConfirmWeekPlan;
                case "execute_week_action":
                    return OperationKind.ExecuteWeekAction;
                case "resolve_event_choice":
                    return OperationKind.ResolveEventChoice;
                default:
                    throw UnknownTextEnum("operationReceipts[].operationKind", value);
            }
        }

        private static string FormatOperationOutcomeKind(OperationOutcomeKind value)
        {
            switch (value)
            {
                case OperationOutcomeKind.CareerCreated:
                    return "career_created";
                case OperationOutcomeKind.TryoutAdvanced:
                    return "tryout_advanced";
                case OperationOutcomeKind.WeekPlanConfirmed:
                    return "week_plan_confirmed";
                case OperationOutcomeKind.SlotCompleted:
                    return "slot_completed";
                case OperationOutcomeKind.EventChoiceApplied:
                    return "event_choice_applied";
                default:
                    throw UnknownEnum(nameof(value), value);
            }
        }

        private static OperationOutcomeKind ParseOperationOutcomeKind(string value)
        {
            switch (value)
            {
                case "career_created":
                    return OperationOutcomeKind.CareerCreated;
                case "tryout_advanced":
                    return OperationOutcomeKind.TryoutAdvanced;
                case "week_plan_confirmed":
                    return OperationOutcomeKind.WeekPlanConfirmed;
                case "slot_completed":
                    return OperationOutcomeKind.SlotCompleted;
                case "event_choice_applied":
                    return OperationOutcomeKind.EventChoiceApplied;
                default:
                    throw UnknownTextEnum("operationReceipts[].outcomeKind", value);
            }
        }

        private static ArgumentOutOfRangeException UnknownEnum<T>(string name, T value)
            where T : struct
        {
            return new ArgumentOutOfRangeException(name, value, "Unsupported enum value.");
        }

        private static ArgumentException UnknownTextEnum(string fieldName, string value)
        {
            return new ArgumentException(
                fieldName + " contains an unknown schema V1 enum value: " +
                (value ?? "<null>"),
                fieldName);
        }
    }
}
