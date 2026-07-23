using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    public static partial class CareerSaveJsonCodec
    {
        public static CareerSaveSnapshot Seal(CareerSaveSnapshot candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var document = CareerSaveSnapshotMapper.ToDocument(candidate);
            document.integrity.snapshotHash = ComputeDocumentHash(document);
            return CareerSaveSnapshotMapper.ToDomain(document);
        }

        public static Sha256Digest ComputeSnapshotHash(CareerSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new Sha256Digest(
                ComputeDocumentHash(CareerSaveSnapshotMapper.ToDocument(snapshot)));
        }

        public static byte[] Serialize(CareerSaveSnapshot sealedSnapshot)
        {
            if (sealedSnapshot == null)
            {
                throw new ArgumentNullException(nameof(sealedSnapshot));
            }

            var document = CareerSaveSnapshotMapper.ToDocument(sealedSnapshot);
            var expectedHash = ComputeDocumentHash(document);
            if (!string.Equals(
                    document.integrity.snapshotHash,
                    expectedHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The Career snapshot is not sealed with its canonical snapshot hash.");
            }

            return WriteDocument(document, true);
        }

        public static CareerSaveSnapshot Deserialize(byte[] utf8Json)
        {
            if (utf8Json == null)
            {
                throw new ArgumentNullException(nameof(utf8Json));
            }

            var classification = CareerSaveVersionClassifier.Classify(utf8Json);
            if (classification.Kind == CareerSaveVersionClassification.Unsupported)
            {
                throw new CareerSaveVersionNotSupportedException(
                    classification.ObservedSchemaVersion);
            }

            if (classification.Kind == CareerSaveVersionClassification.Malformed)
            {
                throw new FormatException("The Career save version envelope is malformed.");
            }

            var root = StrictJsonReader.Parse(utf8Json);
            var document = ReadDocument(root);

            CareerSaveSnapshot snapshot;
            try
            {
                snapshot = CareerSaveSnapshotMapper.ToDomain(document);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(
                    "The Career save violates Schema V2 domain invariants.",
                    exception);
            }

            var expectedHash = ComputeDocumentHash(document);
            if (!string.Equals(
                    document.integrity.snapshotHash,
                    expectedHash,
                    StringComparison.Ordinal))
            {
                throw new FormatException("The Career save snapshot hash is invalid.");
            }

            var canonicalBytes = WriteDocument(document, true);
            if (!BytesEqual(canonicalBytes, utf8Json))
            {
                throw new FormatException(
                    "The Career save is valid JSON but is not the canonical Schema V2 byte sequence.");
            }

            return snapshot;
        }

        private static string ComputeDocumentHash(CareerSaveDocumentV2 document)
        {
            var canonicalBytes = WriteDocument(document, false);
            using (var sha256 = SHA256.Create())
            {
                return LowercaseHex(sha256.ComputeHash(canonicalBytes));
            }
        }

        private static byte[] WriteDocument(
            CareerSaveDocumentV2 document,
            bool includeSnapshotHash)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var writer = new CanonicalJsonWriter();
            writer.WriteStartObject();
            writer.WritePropertyName("versions");
            WriteVersions(writer, document.versions);
            writer.WritePropertyName("identity");
            WriteIdentity(writer, document.identity);
            writer.WritePropertyName("integrity");
            WriteIntegrity(writer, document.integrity, includeSnapshotHash);
            writer.WritePropertyName("careerSeed");
            writer.WriteString(document.careerSeed);
            writer.WritePropertyName("careerName");
            writer.WriteString(document.careerName);
            writer.WritePropertyName("playerDraft");
            WritePlayerDraft(writer, document.playerDraft);
            writer.WritePropertyName("onboarding");
            WriteOnboarding(writer, document.onboarding);
            writer.WritePropertyName("progression");
            WriteProgression(writer, document.progression);
            writer.WritePropertyName("trainingEmphases");
            WriteTrainingEmphases(writer, document.trainingEmphases);
            writer.WritePropertyName("pendingMatch");
            WritePendingMatch(writer, document.pendingMatch);
            writer.WritePropertyName("player");
            WritePlayer(writer, document.player);
            writer.WritePropertyName("teamId");
            writer.WriteNullableString(document.teamId);
            writer.WritePropertyName("potentialGrade");
            writer.WriteNullableString(document.potentialGrade);
            writer.WritePropertyName("fatigue");
            writer.WriteNullableInt32(document.fatigue);
            writer.WritePropertyName("mindset");
            writer.WriteNullableInt32(document.mindset);
            writer.WritePropertyName("coachTrust");
            writer.WriteNullableInt32(document.coachTrust);
            writer.WritePropertyName("matchHistory");
            WriteMatchHistory(writer, document.matchHistory);
            writer.WritePropertyName("operationReceipts");
            WriteOperationReceipts(writer, document.operationReceipts);
            writer.WritePropertyName("settlementReceipts");
            WriteSettlementReceipts(writer, document.settlementReceipts);
            writer.WriteEndObject();
            return writer.ToUtf8Bytes();
        }

        private static void WriteVersions(
            CanonicalJsonWriter writer,
            CareerSaveVersionsDocumentV2 document)
        {
            RequireWriteValue(document, "versions");
            writer.WriteStartObject();
            writer.WritePropertyName("schemaVersion");
            writer.WriteInt64(document.schemaVersion);
            writer.WritePropertyName("contentVersion");
            writer.WriteInt64(document.contentVersion);
            writer.WritePropertyName("rulesetVersion");
            writer.WriteInt64(document.rulesetVersion);
            writer.WritePropertyName("contractVersion");
            writer.WriteInt64(document.contractVersion);
            writer.WritePropertyName("careerRandomAlgorithmVersion");
            writer.WriteInt64(document.careerRandomAlgorithmVersion);
            writer.WriteEndObject();
        }

        private static void WriteIdentity(
            CanonicalJsonWriter writer,
            CareerSaveIdentityDocumentV2 document)
        {
            RequireWriteValue(document, "identity");
            writer.WriteStartObject();
            writer.WritePropertyName("profileId");
            writer.WriteString(document.profileId);
            writer.WritePropertyName("saveId");
            writer.WriteString(document.saveId);
            writer.WritePropertyName("lineageId");
            writer.WriteString(document.lineageId);
            writer.WritePropertyName("revision");
            writer.WriteInt64(document.revision);
            writer.WritePropertyName("restoredFromVersionToken");
            WriteVersionToken(writer, document.restoredFromVersionToken);
            writer.WritePropertyName("createdAtUtcMs");
            writer.WriteInt64(document.createdAtUtcMs);
            writer.WritePropertyName("updatedAtUtcMs");
            writer.WriteInt64(document.updatedAtUtcMs);
            writer.WriteEndObject();
        }

        private static void WriteVersionToken(
            CanonicalJsonWriter writer,
            CareerVersionTokenDocumentV2 document)
        {
            if (document == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("lineageId");
            writer.WriteString(document.lineageId);
            writer.WritePropertyName("revision");
            writer.WriteInt64(document.revision);
            writer.WritePropertyName("snapshotHash");
            writer.WriteString(document.snapshotHash);
            writer.WriteEndObject();
        }

        private static void WriteIntegrity(
            CanonicalJsonWriter writer,
            CareerSaveIntegrityDocumentV2 document,
            bool includeSnapshotHash)
        {
            RequireWriteValue(document, "integrity");
            writer.WriteStartObject();
            if (includeSnapshotHash)
            {
                writer.WritePropertyName("snapshotHash");
                writer.WriteString(document.snapshotHash);
            }

            writer.WriteEndObject();
        }

        private static void WritePlayerDraft(
            CanonicalJsonWriter writer,
            CareerPlayerDraftDocumentV2 document)
        {
            RequireWriteValue(document, "playerDraft");
            writer.WriteStartObject();
            writer.WritePropertyName("playerId");
            writer.WriteString(document.playerId);
            writer.WritePropertyName("displayName");
            writer.WriteString(document.displayName);
            writer.WritePropertyName("jerseyNumber");
            writer.WriteInt64(document.jerseyNumber);
            writer.WriteEndObject();
        }

        private static void WriteOnboarding(
            CanonicalJsonWriter writer,
            TryoutOnboardingDocumentV2 document)
        {
            RequireWriteValue(document, "onboarding");
            writer.WriteStartObject();
            writer.WritePropertyName("stages");
            RequireWriteValue(document.stages, "onboarding.stages");
            writer.WriteStartArray();
            for (var index = 0; index < document.stages.Length; index++)
            {
                WriteTryoutStage(writer, document.stages[index]);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("nextStageNumber");
            writer.WriteInt64(document.nextStageNumber);
            writer.WritePropertyName("isFormallyEnrolled");
            writer.WriteBoolean(document.isFormallyEnrolled);
            writer.WriteEndObject();
        }

        private static void WriteTryoutStage(
            CanonicalJsonWriter writer,
            TryoutStageDocumentV2 document)
        {
            RequireWriteValue(document, "onboarding.stages[]");
            writer.WriteStartObject();
            writer.WritePropertyName("stageNumber");
            writer.WriteInt64(document.stageNumber);
            writer.WritePropertyName("occurrenceId");
            writer.WriteString(document.occurrenceId);
            writer.WritePropertyName("randomVersion");
            writer.WriteInt64(document.randomVersion);
            writer.WritePropertyName("choiceId");
            writer.WriteNullableString(document.choiceId);
            writer.WritePropertyName("resolvedOutputs");
            WriteResolvedOutputs(writer, document.resolvedOutputs);
            writer.WriteEndObject();
        }

        private static void WriteResolvedOutputs(
            CanonicalJsonWriter writer,
            TryoutResolvedOutputDocumentV2[] documents)
        {
            RequireWriteValue(documents, "tryoutResolvedOutputs");
            writer.WriteStartArray();
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                RequireWriteValue(document, "tryoutResolvedOutputs[]");
                writer.WriteStartObject();
                writer.WritePropertyName("outputId");
                writer.WriteString(document.outputId);
                writer.WritePropertyName("perturbation");
                writer.WriteInt64(document.perturbation);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteProgression(
            CanonicalJsonWriter writer,
            CareerProgressionDocumentV2 document)
        {
            RequireWriteValue(document, "progression");
            writer.WriteStartObject();
            writer.WritePropertyName("kind");
            writer.WriteString(document.kind);
            writer.WritePropertyName("phase");
            writer.WriteString(document.phase);
            writer.WritePropertyName("tryoutStage");
            writer.WriteInt64(document.tryoutStage);
            writer.WritePropertyName("weekPlan");
            WriteWeekPlan(writer, document.weekPlan);
            writer.WritePropertyName("nextSlotNumber");
            writer.WriteInt64(document.nextSlotNumber);
            writer.WritePropertyName("pendingEvent");
            WritePendingEvent(writer, document.pendingEvent);
            writer.WritePropertyName("matchSessionId");
            writer.WriteNullableString(document.matchSessionId);
            writer.WriteEndObject();
        }

        private static void WriteWeekPlan(
            CanonicalJsonWriter writer,
            CareerWeekPlanDocumentV2 document)
        {
            if (document == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("planId");
            writer.WriteString(document.planId);
            writer.WritePropertyName("season");
            writer.WriteInt64(document.season);
            writer.WritePropertyName("week");
            writer.WriteInt64(document.week);
            writer.WritePropertyName("slots");
            RequireWriteValue(document.slots, "progression.weekPlan.slots");
            writer.WriteStartArray();
            for (var index = 0; index < document.slots.Length; index++)
            {
                WriteWeekAction(writer, document.slots[index]);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("isConfirmed");
            writer.WriteBoolean(document.isConfirmed);
            writer.WriteEndObject();
        }

        private static void WriteWeekAction(
            CanonicalJsonWriter writer,
            CareerWeekActionDocumentV2 document)
        {
            if (document == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("slotActionId");
            writer.WriteString(document.slotActionId);
            writer.WritePropertyName("occurrenceId");
            writer.WriteString(document.occurrenceId);
            writer.WritePropertyName("kind");
            writer.WriteString(document.kind);
            writer.WritePropertyName("contentId");
            writer.WriteString(document.contentId);
            writer.WriteEndObject();
        }

        private static void WriteTrainingEmphases(
            CanonicalJsonWriter writer,
            TrainingEmphasisContributionDocumentV2[] documents)
        {
            RequireWriteValue(documents, "trainingEmphases");
            writer.WriteStartArray();
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                RequireWriteValue(document, "trainingEmphases[" + index + "]");
                writer.WriteStartObject();
                writer.WritePropertyName("sourceSlotActionId");
                writer.WriteString(document.sourceSlotActionId);
                writer.WritePropertyName("direction");
                writer.WriteString(document.direction);
                writer.WritePropertyName("bonusBasisPoints");
                writer.WriteInt64(document.bonusBasisPoints);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WritePendingEvent(
            CanonicalJsonWriter writer,
            PendingCareerEventDocumentV2 document)
        {
            if (document == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("sourceWeekPlanId");
            writer.WriteString(document.sourceWeekPlanId);
            writer.WritePropertyName("sourceSlotActionId");
            writer.WriteString(document.sourceSlotActionId);
            writer.WritePropertyName("sourceActionOccurrenceId");
            writer.WriteString(document.sourceActionOccurrenceId);
            writer.WritePropertyName("occurrenceId");
            writer.WriteString(document.occurrenceId);
            writer.WritePropertyName("eventId");
            writer.WriteString(document.eventId);
            writer.WritePropertyName("randomVersion");
            writer.WriteInt64(document.randomVersion);
            writer.WritePropertyName("resumeAtSlotNumber");
            writer.WriteInt64(document.resumeAtSlotNumber);
            writer.WritePropertyName("options");
            RequireWriteValue(document.options, "progression.pendingEvent.options");
            writer.WriteStartArray();
            for (var index = 0; index < document.options.Length; index++)
            {
                WriteEventOption(writer, document.options[index]);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteEventOption(
            CanonicalJsonWriter writer,
            CareerEventOptionEffectDocumentV2 document)
        {
            RequireWriteValue(document, "progression.pendingEvent.options[]");
            writer.WriteStartObject();
            writer.WritePropertyName("optionId");
            writer.WriteString(document.optionId);
            writer.WritePropertyName("growthExperienceDelta");
            WriteGrowthDelta(writer, document.growthExperienceDelta);
            writer.WritePropertyName("fatigueDelta");
            writer.WriteInt64(document.fatigueDelta);
            writer.WritePropertyName("mindsetDelta");
            writer.WriteInt64(document.mindsetDelta);
            writer.WritePropertyName("coachTrustDelta");
            writer.WriteInt64(document.coachTrustDelta);
            writer.WriteEndObject();
        }

        private static void WriteGrowthDelta(
            CanonicalJsonWriter writer,
            CareerAttributeGrowthDeltaDocumentV2 document)
        {
            if (document == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("spike");
            writer.WriteInt64(document.spike);
            writer.WritePropertyName("serve");
            writer.WriteInt64(document.serve);
            writer.WritePropertyName("reception");
            writer.WriteInt64(document.reception);
            writer.WritePropertyName("defense");
            writer.WriteInt64(document.defense);
            writer.WritePropertyName("block");
            writer.WriteInt64(document.block);
            writer.WritePropertyName("movement");
            writer.WriteInt64(document.movement);
            writer.WritePropertyName("jump");
            writer.WriteInt64(document.jump);
            writer.WritePropertyName("stamina");
            writer.WriteInt64(document.stamina);
            writer.WriteEndObject();
        }

        private static void WritePlayer(
            CanonicalJsonWriter writer,
            CareerPlayerDocumentV2 document)
        {
            if (document == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("playerId");
            writer.WriteString(document.playerId);
            writer.WritePropertyName("displayName");
            writer.WriteString(document.displayName);
            writer.WritePropertyName("jerseyNumber");
            writer.WriteInt64(document.jerseyNumber);
            writer.WritePropertyName("attributes");
            WriteAttributes(writer, document.attributes);
            writer.WriteEndObject();
        }

        private static void WriteAttributes(
            CanonicalJsonWriter writer,
            CareerPlayerAttributesDocumentV2 document)
        {
            RequireWriteValue(document, "player.attributes");
            writer.WriteStartObject();
            writer.WritePropertyName("spike");
            WriteAttributeProgress(writer, document.spike);
            writer.WritePropertyName("serve");
            WriteAttributeProgress(writer, document.serve);
            writer.WritePropertyName("reception");
            WriteAttributeProgress(writer, document.reception);
            writer.WritePropertyName("defense");
            WriteAttributeProgress(writer, document.defense);
            writer.WritePropertyName("block");
            WriteAttributeProgress(writer, document.block);
            writer.WritePropertyName("movement");
            WriteAttributeProgress(writer, document.movement);
            writer.WritePropertyName("jump");
            WriteAttributeProgress(writer, document.jump);
            writer.WritePropertyName("stamina");
            WriteAttributeProgress(writer, document.stamina);
            writer.WriteEndObject();
        }

        private static void WriteAttributeProgress(
            CanonicalJsonWriter writer,
            CareerAttributeProgressDocumentV2 document)
        {
            RequireWriteValue(document, "player.attributes[]");
            writer.WriteStartObject();
            writer.WritePropertyName("abilityBasisPoints");
            writer.WriteInt64(document.abilityBasisPoints);
            writer.WritePropertyName("growthExperience");
            writer.WriteInt64(document.growthExperience);
            writer.WriteEndObject();
        }

        private static void WriteOperationReceipts(
            CanonicalJsonWriter writer,
            OperationReceiptDocumentV2[] documents)
        {
            RequireWriteValue(documents, "operationReceipts");
            writer.WriteStartArray();
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                RequireWriteValue(document, "operationReceipts[]");
                writer.WriteStartObject();
                writer.WritePropertyName("operationId");
                writer.WriteString(document.operationId);
                writer.WritePropertyName("operationKind");
                writer.WriteString(document.operationKind);
                writer.WritePropertyName("target");
                WriteReceiptTarget(writer, document.target);
                writer.WritePropertyName("inputFingerprint");
                writer.WriteString(document.inputFingerprint);
                writer.WritePropertyName("appliedLineageId");
                writer.WriteString(document.appliedLineageId);
                writer.WritePropertyName("appliedRevision");
                writer.WriteInt64(document.appliedRevision);
                writer.WritePropertyName("completedAtUtcMs");
                writer.WriteInt64(document.completedAtUtcMs);
                writer.WritePropertyName("outcomeKind");
                writer.WriteString(document.outcomeKind);
                writer.WritePropertyName("outcomeSummary");
                WriteOutcomeSummary(writer, document.outcomeSummary);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteReceiptTarget(
            CanonicalJsonWriter writer,
            OperationReceiptTargetDocumentV2 document)
        {
            RequireWriteValue(document, "operationReceipts[].target");
            writer.WriteStartObject();
            writer.WritePropertyName("tryoutStage");
            writer.WriteInt64(document.tryoutStage);
            writer.WritePropertyName("tryoutOccurrenceId");
            writer.WriteNullableString(document.tryoutOccurrenceId);
            writer.WritePropertyName("choiceId");
            writer.WriteNullableString(document.choiceId);
            writer.WritePropertyName("weekPlanId");
            writer.WriteNullableString(document.weekPlanId);
            writer.WritePropertyName("slotActionId");
            writer.WriteNullableString(document.slotActionId);
            writer.WritePropertyName("actionOccurrenceId");
            writer.WriteNullableString(document.actionOccurrenceId);
            writer.WritePropertyName("eventOccurrenceId");
            writer.WriteNullableString(document.eventOccurrenceId);
            writer.WritePropertyName("optionId");
            writer.WriteNullableString(document.optionId);
            writer.WritePropertyName("matchSessionId");
            writer.WriteNullableString(document.matchSessionId);
            writer.WritePropertyName("scheduleItemId");
            writer.WriteNullableString(document.scheduleItemId);
            writer.WritePropertyName("contextHash");
            writer.WriteNullableString(document.contextHash);
            writer.WriteEndObject();
        }

        private static void WriteOutcomeSummary(
            CanonicalJsonWriter writer,
            OperationOutcomeSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "operationReceipts[].outcomeSummary");
            writer.WriteStartObject();
            writer.WritePropertyName("tryoutResolvedOutputs");
            WriteResolvedOutputs(writer, document.tryoutResolvedOutputs);
            writer.WritePropertyName("growthExperienceDelta");
            WriteGrowthDelta(writer, document.growthExperienceDelta);
            writer.WritePropertyName("fatigueDelta");
            writer.WriteNullableInt32(document.fatigueDelta);
            writer.WritePropertyName("mindsetDelta");
            writer.WriteNullableInt32(document.mindsetDelta);
            writer.WritePropertyName("coachTrustDelta");
            writer.WriteNullableInt32(document.coachTrustDelta);
            writer.WritePropertyName("matchSessionId");
            writer.WriteNullableString(document.matchSessionId);
            writer.WritePropertyName("contextHash");
            writer.WriteNullableString(document.contextHash);
            writer.WriteEndObject();
        }

        private static CareerSaveDocumentV2 ReadDocument(StrictJsonValue root)
        {
            var document = ExactObject(
                root,
                "$",
                "versions",
                "identity",
                "integrity",
                "careerSeed",
                "careerName",
                "playerDraft",
                "onboarding",
                "progression",
                "trainingEmphases",
                "pendingMatch",
                "player",
                "teamId",
                "potentialGrade",
                "fatigue",
                "mindset",
                "coachTrust",
                "matchHistory",
                "operationReceipts",
                "settlementReceipts");

            return new CareerSaveDocumentV2
            {
                versions = ReadVersions(document.Get("versions"), "$.versions"),
                identity = ReadIdentity(document.Get("identity"), "$.identity"),
                integrity = ReadIntegrity(document.Get("integrity"), "$.integrity"),
                careerSeed = RequiredString(document.Get("careerSeed"), "$.careerSeed"),
                careerName = RequiredString(document.Get("careerName"), "$.careerName"),
                playerDraft = ReadPlayerDraft(document.Get("playerDraft"), "$.playerDraft"),
                onboarding = ReadOnboarding(document.Get("onboarding"), "$.onboarding"),
                progression = ReadProgression(document.Get("progression"), "$.progression"),
                trainingEmphases = ReadTrainingEmphases(
                    document.Get("trainingEmphases"),
                    "$.trainingEmphases"),
                pendingMatch = ReadPendingMatch(document.Get("pendingMatch"), "$.pendingMatch"),
                player = ReadPlayer(document.Get("player"), "$.player"),
                teamId = NullableString(document.Get("teamId"), "$.teamId"),
                potentialGrade = NullableString(
                    document.Get("potentialGrade"),
                    "$.potentialGrade"),
                fatigue = NullableInt32(document.Get("fatigue"), "$.fatigue"),
                mindset = NullableInt32(document.Get("mindset"), "$.mindset"),
                coachTrust = NullableInt32(document.Get("coachTrust"), "$.coachTrust"),
                matchHistory = ReadMatchHistory(
                    document.Get("matchHistory"),
                    "$.matchHistory"),
                operationReceipts = ReadOperationReceipts(
                    document.Get("operationReceipts"),
                    "$.operationReceipts"),
                settlementReceipts = ReadSettlementReceipts(
                    document.Get("settlementReceipts"),
                    "$.settlementReceipts")
            };
        }

        private static CareerSaveVersionsDocumentV2 ReadVersions(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "schemaVersion",
                "contentVersion",
                "rulesetVersion",
                "contractVersion",
                "careerRandomAlgorithmVersion");
            return new CareerSaveVersionsDocumentV2
            {
                schemaVersion = Int32(document.Get("schemaVersion"), path + ".schemaVersion"),
                contentVersion = Int32(document.Get("contentVersion"), path + ".contentVersion"),
                rulesetVersion = Int32(document.Get("rulesetVersion"), path + ".rulesetVersion"),
                contractVersion = Int32(document.Get("contractVersion"), path + ".contractVersion"),
                careerRandomAlgorithmVersion = Int32(
                    document.Get("careerRandomAlgorithmVersion"),
                    path + ".careerRandomAlgorithmVersion")
            };
        }

        private static CareerSaveIdentityDocumentV2 ReadIdentity(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "profileId",
                "saveId",
                "lineageId",
                "revision",
                "restoredFromVersionToken",
                "createdAtUtcMs",
                "updatedAtUtcMs");
            return new CareerSaveIdentityDocumentV2
            {
                profileId = RequiredString(document.Get("profileId"), path + ".profileId"),
                saveId = RequiredString(document.Get("saveId"), path + ".saveId"),
                lineageId = RequiredString(document.Get("lineageId"), path + ".lineageId"),
                revision = Int64(document.Get("revision"), path + ".revision"),
                restoredFromVersionToken = ReadVersionToken(
                    document.Get("restoredFromVersionToken"),
                    path + ".restoredFromVersionToken"),
                createdAtUtcMs = Int64(
                    document.Get("createdAtUtcMs"),
                    path + ".createdAtUtcMs"),
                updatedAtUtcMs = Int64(
                    document.Get("updatedAtUtcMs"),
                    path + ".updatedAtUtcMs")
            };
        }

        private static CareerVersionTokenDocumentV2 ReadVersionToken(
            StrictJsonValue value,
            string path)
        {
            if (value.Kind == StrictJsonKind.Null)
            {
                return null;
            }

            var document = ExactObject(
                value,
                path,
                "lineageId",
                "revision",
                "snapshotHash");
            return new CareerVersionTokenDocumentV2
            {
                lineageId = RequiredString(document.Get("lineageId"), path + ".lineageId"),
                revision = Int64(document.Get("revision"), path + ".revision"),
                snapshotHash = RequiredString(
                    document.Get("snapshotHash"),
                    path + ".snapshotHash")
            };
        }

        private static CareerSaveIntegrityDocumentV2 ReadIntegrity(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(value, path, "snapshotHash");
            return new CareerSaveIntegrityDocumentV2
            {
                snapshotHash = RequiredString(
                    document.Get("snapshotHash"),
                    path + ".snapshotHash")
            };
        }

        private static CareerPlayerDraftDocumentV2 ReadPlayerDraft(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(value, path, "playerId", "displayName", "jerseyNumber");
            return new CareerPlayerDraftDocumentV2
            {
                playerId = RequiredString(document.Get("playerId"), path + ".playerId"),
                displayName = RequiredString(document.Get("displayName"), path + ".displayName"),
                jerseyNumber = Int32(document.Get("jerseyNumber"), path + ".jerseyNumber")
            };
        }

        private static TryoutOnboardingDocumentV2 ReadOnboarding(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "stages",
                "nextStageNumber",
                "isFormallyEnrolled");
            var stageValues = RequiredArray(document.Get("stages"), path + ".stages");
            var stages = new TryoutStageDocumentV2[stageValues.Count];
            for (var index = 0; index < stages.Length; index++)
            {
                stages[index] = ReadTryoutStage(
                    stageValues[index],
                    path + ".stages[" + index + "]");
            }

            return new TryoutOnboardingDocumentV2
            {
                stages = stages,
                nextStageNumber = Int32(
                    document.Get("nextStageNumber"),
                    path + ".nextStageNumber"),
                isFormallyEnrolled = Boolean(
                    document.Get("isFormallyEnrolled"),
                    path + ".isFormallyEnrolled")
            };
        }

        private static TryoutStageDocumentV2 ReadTryoutStage(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "stageNumber",
                "occurrenceId",
                "randomVersion",
                "choiceId",
                "resolvedOutputs");
            return new TryoutStageDocumentV2
            {
                stageNumber = Int32(document.Get("stageNumber"), path + ".stageNumber"),
                occurrenceId = RequiredString(
                    document.Get("occurrenceId"),
                    path + ".occurrenceId"),
                randomVersion = Int32(
                    document.Get("randomVersion"),
                    path + ".randomVersion"),
                choiceId = NullableString(document.Get("choiceId"), path + ".choiceId"),
                resolvedOutputs = ReadResolvedOutputs(
                    document.Get("resolvedOutputs"),
                    path + ".resolvedOutputs")
            };
        }

        private static TryoutResolvedOutputDocumentV2[] ReadResolvedOutputs(
            StrictJsonValue value,
            string path)
        {
            var values = RequiredArray(value, path);
            var results = new TryoutResolvedOutputDocumentV2[values.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = ExactObject(values[index], itemPath, "outputId", "perturbation");
                results[index] = new TryoutResolvedOutputDocumentV2
                {
                    outputId = RequiredString(document.Get("outputId"), itemPath + ".outputId"),
                    perturbation = Int32(
                        document.Get("perturbation"),
                        itemPath + ".perturbation")
                };
            }

            return results;
        }

        private static CareerProgressionDocumentV2 ReadProgression(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "kind",
                "phase",
                "tryoutStage",
                "weekPlan",
                "nextSlotNumber",
                "pendingEvent",
                "matchSessionId");
            return new CareerProgressionDocumentV2
            {
                kind = RequiredString(document.Get("kind"), path + ".kind"),
                phase = RequiredString(document.Get("phase"), path + ".phase"),
                tryoutStage = Int32(document.Get("tryoutStage"), path + ".tryoutStage"),
                weekPlan = ReadWeekPlan(document.Get("weekPlan"), path + ".weekPlan"),
                nextSlotNumber = Int32(
                    document.Get("nextSlotNumber"),
                    path + ".nextSlotNumber"),
                pendingEvent = ReadPendingEvent(
                    document.Get("pendingEvent"),
                    path + ".pendingEvent"),
                matchSessionId = NullableString(
                    document.Get("matchSessionId"),
                    path + ".matchSessionId")
            };
        }

        private static CareerWeekPlanDocumentV2 ReadWeekPlan(
            StrictJsonValue value,
            string path)
        {
            if (value.Kind == StrictJsonKind.Null)
            {
                return null;
            }

            var document = ExactObject(
                value,
                path,
                "planId",
                "season",
                "week",
                "slots",
                "isConfirmed");
            var slotValues = RequiredArray(document.Get("slots"), path + ".slots");
            var slots = new CareerWeekActionDocumentV2[slotValues.Count];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = ReadWeekAction(
                    slotValues[index],
                    path + ".slots[" + index + "]");
            }

            return new CareerWeekPlanDocumentV2
            {
                planId = RequiredString(document.Get("planId"), path + ".planId"),
                season = Int32(document.Get("season"), path + ".season"),
                week = Int32(document.Get("week"), path + ".week"),
                slots = slots,
                isConfirmed = Boolean(document.Get("isConfirmed"), path + ".isConfirmed")
            };
        }

        private static CareerWeekActionDocumentV2 ReadWeekAction(
            StrictJsonValue value,
            string path)
        {
            if (value.Kind == StrictJsonKind.Null)
            {
                return null;
            }

            var document = ExactObject(
                value,
                path,
                "slotActionId",
                "occurrenceId",
                "kind",
                "contentId");
            return new CareerWeekActionDocumentV2
            {
                slotActionId = RequiredString(
                    document.Get("slotActionId"),
                    path + ".slotActionId"),
                occurrenceId = RequiredString(
                    document.Get("occurrenceId"),
                    path + ".occurrenceId"),
                kind = RequiredString(document.Get("kind"), path + ".kind"),
                contentId = RequiredString(document.Get("contentId"), path + ".contentId")
            };
        }

        private static TrainingEmphasisContributionDocumentV2[] ReadTrainingEmphases(
            StrictJsonValue value,
            string path)
        {
            var values = RequiredArray(value, path);
            var result = new TrainingEmphasisContributionDocumentV2[values.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = ExactObject(
                    values[index],
                    itemPath,
                    "sourceSlotActionId",
                    "direction",
                    "bonusBasisPoints");
                result[index] = new TrainingEmphasisContributionDocumentV2
                {
                    sourceSlotActionId = RequiredString(
                        document.Get("sourceSlotActionId"),
                        itemPath + ".sourceSlotActionId"),
                    direction = RequiredString(
                        document.Get("direction"),
                        itemPath + ".direction"),
                    bonusBasisPoints = Int32(
                        document.Get("bonusBasisPoints"),
                        itemPath + ".bonusBasisPoints")
                };
            }

            return result;
        }

        private static PendingCareerEventDocumentV2 ReadPendingEvent(
            StrictJsonValue value,
            string path)
        {
            if (value.Kind == StrictJsonKind.Null)
            {
                return null;
            }

            var document = ExactObject(
                value,
                path,
                "sourceWeekPlanId",
                "sourceSlotActionId",
                "sourceActionOccurrenceId",
                "occurrenceId",
                "eventId",
                "randomVersion",
                "resumeAtSlotNumber",
                "options");
            var optionValues = RequiredArray(document.Get("options"), path + ".options");
            var options = new CareerEventOptionEffectDocumentV2[optionValues.Count];
            for (var index = 0; index < options.Length; index++)
            {
                options[index] = ReadEventOption(
                    optionValues[index],
                    path + ".options[" + index + "]");
            }

            return new PendingCareerEventDocumentV2
            {
                sourceWeekPlanId = RequiredString(
                    document.Get("sourceWeekPlanId"),
                    path + ".sourceWeekPlanId"),
                sourceSlotActionId = RequiredString(
                    document.Get("sourceSlotActionId"),
                    path + ".sourceSlotActionId"),
                sourceActionOccurrenceId = RequiredString(
                    document.Get("sourceActionOccurrenceId"),
                    path + ".sourceActionOccurrenceId"),
                occurrenceId = RequiredString(
                    document.Get("occurrenceId"),
                    path + ".occurrenceId"),
                eventId = RequiredString(document.Get("eventId"), path + ".eventId"),
                randomVersion = Int32(
                    document.Get("randomVersion"),
                    path + ".randomVersion"),
                resumeAtSlotNumber = Int32(
                    document.Get("resumeAtSlotNumber"),
                    path + ".resumeAtSlotNumber"),
                options = options
            };
        }

        private static CareerEventOptionEffectDocumentV2 ReadEventOption(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "optionId",
                "growthExperienceDelta",
                "fatigueDelta",
                "mindsetDelta",
                "coachTrustDelta");
            return new CareerEventOptionEffectDocumentV2
            {
                optionId = RequiredString(document.Get("optionId"), path + ".optionId"),
                growthExperienceDelta = ReadGrowthDelta(
                    document.Get("growthExperienceDelta"),
                    path + ".growthExperienceDelta"),
                fatigueDelta = Int32(document.Get("fatigueDelta"), path + ".fatigueDelta"),
                mindsetDelta = Int32(document.Get("mindsetDelta"), path + ".mindsetDelta"),
                coachTrustDelta = Int32(
                    document.Get("coachTrustDelta"),
                    path + ".coachTrustDelta")
            };
        }

        private static CareerAttributeGrowthDeltaDocumentV2 ReadGrowthDelta(
            StrictJsonValue value,
            string path)
        {
            if (value.Kind == StrictJsonKind.Null)
            {
                return null;
            }

            var document = ExactObject(
                value,
                path,
                "spike",
                "serve",
                "reception",
                "defense",
                "block",
                "movement",
                "jump",
                "stamina");
            return new CareerAttributeGrowthDeltaDocumentV2
            {
                spike = Int64(document.Get("spike"), path + ".spike"),
                serve = Int64(document.Get("serve"), path + ".serve"),
                reception = Int64(document.Get("reception"), path + ".reception"),
                defense = Int64(document.Get("defense"), path + ".defense"),
                block = Int64(document.Get("block"), path + ".block"),
                movement = Int64(document.Get("movement"), path + ".movement"),
                jump = Int64(document.Get("jump"), path + ".jump"),
                stamina = Int64(document.Get("stamina"), path + ".stamina")
            };
        }

        private static CareerPlayerDocumentV2 ReadPlayer(StrictJsonValue value, string path)
        {
            if (value.Kind == StrictJsonKind.Null)
            {
                return null;
            }

            var document = ExactObject(
                value,
                path,
                "playerId",
                "displayName",
                "jerseyNumber",
                "attributes");
            return new CareerPlayerDocumentV2
            {
                playerId = RequiredString(document.Get("playerId"), path + ".playerId"),
                displayName = RequiredString(document.Get("displayName"), path + ".displayName"),
                jerseyNumber = Int32(document.Get("jerseyNumber"), path + ".jerseyNumber"),
                attributes = ReadAttributes(document.Get("attributes"), path + ".attributes")
            };
        }

        private static CareerPlayerAttributesDocumentV2 ReadAttributes(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "spike",
                "serve",
                "reception",
                "defense",
                "block",
                "movement",
                "jump",
                "stamina");
            return new CareerPlayerAttributesDocumentV2
            {
                spike = ReadAttributeProgress(document.Get("spike"), path + ".spike"),
                serve = ReadAttributeProgress(document.Get("serve"), path + ".serve"),
                reception = ReadAttributeProgress(
                    document.Get("reception"),
                    path + ".reception"),
                defense = ReadAttributeProgress(document.Get("defense"), path + ".defense"),
                block = ReadAttributeProgress(document.Get("block"), path + ".block"),
                movement = ReadAttributeProgress(document.Get("movement"), path + ".movement"),
                jump = ReadAttributeProgress(document.Get("jump"), path + ".jump"),
                stamina = ReadAttributeProgress(document.Get("stamina"), path + ".stamina")
            };
        }

        private static CareerAttributeProgressDocumentV2 ReadAttributeProgress(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "abilityBasisPoints",
                "growthExperience");
            return new CareerAttributeProgressDocumentV2
            {
                abilityBasisPoints = Int32(
                    document.Get("abilityBasisPoints"),
                    path + ".abilityBasisPoints"),
                growthExperience = Int64(
                    document.Get("growthExperience"),
                    path + ".growthExperience")
            };
        }

        private static OperationReceiptDocumentV2[] ReadOperationReceipts(
            StrictJsonValue value,
            string path)
        {
            var values = RequiredArray(value, path);
            var receipts = new OperationReceiptDocumentV2[values.Count];
            for (var index = 0; index < receipts.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = ExactObject(
                    values[index],
                    itemPath,
                    "operationId",
                    "operationKind",
                    "target",
                    "inputFingerprint",
                    "appliedLineageId",
                    "appliedRevision",
                    "completedAtUtcMs",
                    "outcomeKind",
                    "outcomeSummary");
                receipts[index] = new OperationReceiptDocumentV2
                {
                    operationId = RequiredString(
                        document.Get("operationId"),
                        itemPath + ".operationId"),
                    operationKind = RequiredString(
                        document.Get("operationKind"),
                        itemPath + ".operationKind"),
                    target = ReadReceiptTarget(
                        document.Get("target"),
                        itemPath + ".target"),
                    inputFingerprint = RequiredString(
                        document.Get("inputFingerprint"),
                        itemPath + ".inputFingerprint"),
                    appliedLineageId = RequiredString(
                        document.Get("appliedLineageId"),
                        itemPath + ".appliedLineageId"),
                    appliedRevision = Int64(
                        document.Get("appliedRevision"),
                        itemPath + ".appliedRevision"),
                    completedAtUtcMs = Int64(
                        document.Get("completedAtUtcMs"),
                        itemPath + ".completedAtUtcMs"),
                    outcomeKind = RequiredString(
                        document.Get("outcomeKind"),
                        itemPath + ".outcomeKind"),
                    outcomeSummary = ReadOutcomeSummary(
                        document.Get("outcomeSummary"),
                        itemPath + ".outcomeSummary")
                };
            }

            return receipts;
        }

        private static OperationReceiptTargetDocumentV2 ReadReceiptTarget(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "tryoutStage",
                "tryoutOccurrenceId",
                "choiceId",
                "weekPlanId",
                "slotActionId",
                "actionOccurrenceId",
                "eventOccurrenceId",
                "optionId",
                "matchSessionId",
                "scheduleItemId",
                "contextHash");
            return new OperationReceiptTargetDocumentV2
            {
                tryoutStage = Int32(document.Get("tryoutStage"), path + ".tryoutStage"),
                tryoutOccurrenceId = NullableString(
                    document.Get("tryoutOccurrenceId"),
                    path + ".tryoutOccurrenceId"),
                choiceId = NullableString(document.Get("choiceId"), path + ".choiceId"),
                weekPlanId = NullableString(document.Get("weekPlanId"), path + ".weekPlanId"),
                slotActionId = NullableString(
                    document.Get("slotActionId"),
                    path + ".slotActionId"),
                actionOccurrenceId = NullableString(
                    document.Get("actionOccurrenceId"),
                    path + ".actionOccurrenceId"),
                eventOccurrenceId = NullableString(
                    document.Get("eventOccurrenceId"),
                    path + ".eventOccurrenceId"),
                optionId = NullableString(document.Get("optionId"), path + ".optionId"),
                matchSessionId = NullableString(
                    document.Get("matchSessionId"),
                    path + ".matchSessionId"),
                scheduleItemId = NullableString(
                    document.Get("scheduleItemId"),
                    path + ".scheduleItemId"),
                contextHash = NullableString(
                    document.Get("contextHash"),
                    path + ".contextHash")
            };
        }

        private static OperationOutcomeSummaryDocumentV2 ReadOutcomeSummary(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "tryoutResolvedOutputs",
                "growthExperienceDelta",
                "fatigueDelta",
                "mindsetDelta",
                "coachTrustDelta",
                "matchSessionId",
                "contextHash");
            return new OperationOutcomeSummaryDocumentV2
            {
                tryoutResolvedOutputs = ReadResolvedOutputs(
                    document.Get("tryoutResolvedOutputs"),
                    path + ".tryoutResolvedOutputs"),
                growthExperienceDelta = ReadGrowthDelta(
                    document.Get("growthExperienceDelta"),
                    path + ".growthExperienceDelta"),
                fatigueDelta = NullableInt32(
                    document.Get("fatigueDelta"),
                    path + ".fatigueDelta"),
                mindsetDelta = NullableInt32(
                    document.Get("mindsetDelta"),
                    path + ".mindsetDelta"),
                coachTrustDelta = NullableInt32(
                    document.Get("coachTrustDelta"),
                    path + ".coachTrustDelta"),
                matchSessionId = NullableString(
                    document.Get("matchSessionId"),
                    path + ".matchSessionId"),
                contextHash = NullableString(
                    document.Get("contextHash"),
                    path + ".contextHash")
            };
        }

        private static StrictJsonObject ExactObject(
            StrictJsonValue value,
            string path,
            params string[] propertyNames)
        {
            if (value.Kind != StrictJsonKind.Object)
            {
                throw new FormatException(path + " must be a JSON object.");
            }

            value.ObjectValue.RequireExactly(path, propertyNames);
            return value.ObjectValue;
        }

        private static IReadOnlyList<StrictJsonValue> RequiredArray(
            StrictJsonValue value,
            string path)
        {
            if (value.Kind != StrictJsonKind.Array)
            {
                throw new FormatException(path + " must be a JSON array.");
            }

            return value.ArrayValue;
        }

        private static string RequiredString(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.String)
            {
                throw new FormatException(path + " must be a JSON string.");
            }

            return value.StringValue;
        }

        private static string NullableString(StrictJsonValue value, string path)
        {
            return value.Kind == StrictJsonKind.Null ? null : RequiredString(value, path);
        }

        private static int Int32(StrictJsonValue value, string path)
        {
            var number = Int64(value, path);
            if (number < int.MinValue || number > int.MaxValue)
            {
                throw new FormatException(path + " must fit in a signed 32-bit integer.");
            }

            return (int)number;
        }

        private static int? NullableInt32(StrictJsonValue value, string path)
        {
            return value.Kind == StrictJsonKind.Null ? (int?)null : Int32(value, path);
        }

        private static long Int64(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.Integer)
            {
                throw new FormatException(path + " must be a JSON integer.");
            }

            return value.IntegerValue;
        }

        private static bool Boolean(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.Boolean)
            {
                throw new FormatException(path + " must be a JSON boolean.");
            }

            return value.BooleanValue;
        }

        private static void RequireWriteValue(object value, string path)
        {
            if (value == null)
            {
                throw new InvalidOperationException(path + " is required by Career Schema V2.");
            }
        }

        private static string LowercaseHex(byte[] bytes)
        {
            var characters = new char[bytes.Length * 2];
            const string digits = "0123456789abcdef";
            for (var index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = digits[bytes[index] >> 4];
                characters[(index * 2) + 1] = digits[bytes[index] & 0x0f];
            }

            return new string(characters);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }
    }

    internal sealed class CanonicalJsonWriter
    {
        private const long MaximumIJsonSafeInteger = 9007199254740991L;

        private readonly StringBuilder _builder = new StringBuilder();
        private readonly Stack<ContainerFrame> _containers = new Stack<ContainerFrame>();
        private bool _hasRootValue;

        public void WriteStartObject()
        {
            BeginValue();
            _builder.Append('{');
            _containers.Push(new ContainerFrame(ContainerKind.Object));
        }

        public void WriteEndObject()
        {
            var frame = RequireContainer(ContainerKind.Object);
            if (frame.ExpectingValue)
            {
                throw new InvalidOperationException("A JSON object property is missing its value.");
            }

            _containers.Pop();
            _builder.Append('}');
        }

        public void WriteStartArray()
        {
            BeginValue();
            _builder.Append('[');
            _containers.Push(new ContainerFrame(ContainerKind.Array));
        }

        public void WriteEndArray()
        {
            RequireContainer(ContainerKind.Array);
            _containers.Pop();
            _builder.Append(']');
        }

        public void WritePropertyName(string value)
        {
            ValidateUnicodeScalars(value, nameof(value));
            var frame = RequireContainer(ContainerKind.Object);
            if (frame.ExpectingValue)
            {
                throw new InvalidOperationException("The previous JSON property is missing its value.");
            }

            if (frame.Count != 0)
            {
                _builder.Append(',');
            }

            AppendString(value);
            _builder.Append(':');
            frame.Count++;
            frame.ExpectingValue = true;
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            ValidateUnicodeScalars(value, nameof(value));
            BeginValue();
            AppendString(value);
        }

        public void WriteNullableString(string value)
        {
            if (value == null)
            {
                WriteNull();
            }
            else
            {
                WriteString(value);
            }
        }

        public void WriteInt64(long value)
        {
            if (value < -MaximumIJsonSafeInteger || value > MaximumIJsonSafeInteger)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Canonical JSON integers must be in the I-JSON safe range.");
            }

            BeginValue();
            _builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void WriteNullableInt32(int? value)
        {
            if (value.HasValue)
            {
                WriteInt64(value.Value);
            }
            else
            {
                WriteNull();
            }
        }

        public void WriteBoolean(bool value)
        {
            BeginValue();
            _builder.Append(value ? "true" : "false");
        }

        public void WriteNull()
        {
            BeginValue();
            _builder.Append("null");
        }

        public byte[] ToUtf8Bytes()
        {
            if (!_hasRootValue || _containers.Count != 0)
            {
                throw new InvalidOperationException("The canonical JSON document is incomplete.");
            }

            return new UTF8Encoding(false, true).GetBytes(_builder.ToString());
        }

        private void BeginValue()
        {
            if (_containers.Count == 0)
            {
                if (_hasRootValue)
                {
                    throw new InvalidOperationException("A JSON document may contain only one root value.");
                }

                _hasRootValue = true;
                return;
            }

            var frame = _containers.Peek();
            if (frame.Kind == ContainerKind.Object)
            {
                if (!frame.ExpectingValue)
                {
                    throw new InvalidOperationException(
                        "A JSON object value requires a preceding property name.");
                }

                frame.ExpectingValue = false;
                return;
            }

            if (frame.Count != 0)
            {
                _builder.Append(',');
            }

            frame.Count++;
        }

        private ContainerFrame RequireContainer(ContainerKind kind)
        {
            if (_containers.Count == 0 || _containers.Peek().Kind != kind)
            {
                throw new InvalidOperationException("The JSON container nesting is invalid.");
            }

            return _containers.Peek();
        }

        private void AppendString(string value)
        {
            _builder.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"':
                        _builder.Append("\\\"");
                        break;
                    case '\\':
                        _builder.Append("\\\\");
                        break;
                    case '\b':
                        _builder.Append("\\b");
                        break;
                    case '\t':
                        _builder.Append("\\t");
                        break;
                    case '\n':
                        _builder.Append("\\n");
                        break;
                    case '\f':
                        _builder.Append("\\f");
                        break;
                    case '\r':
                        _builder.Append("\\r");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            const string digits = "0123456789abcdef";
                            _builder.Append("\\u00");
                            _builder.Append(digits[(character >> 4) & 0x0f]);
                            _builder.Append(digits[character & 0x0f]);
                        }
                        else
                        {
                            _builder.Append(character);
                        }

                        break;
                }
            }

            _builder.Append('"');
        }

        private static void ValidateUnicodeScalars(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        throw new ArgumentException(
                            "JSON strings cannot contain a lone surrogate.",
                            parameterName);
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(character))
                {
                    throw new ArgumentException(
                        "JSON strings cannot contain a lone surrogate.",
                        parameterName);
                }
            }
        }

        private enum ContainerKind
        {
            Object,
            Array
        }

        private sealed class ContainerFrame
        {
            public ContainerFrame(ContainerKind kind)
            {
                Kind = kind;
            }

            public ContainerKind Kind { get; }

            public int Count { get; set; }

            public bool ExpectingValue { get; set; }
        }
    }

    internal static class StrictJsonReader
    {
        public static StrictJsonValue Parse(byte[] utf8Json)
        {
            if (utf8Json == null)
            {
                throw new ArgumentNullException(nameof(utf8Json));
            }

            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(utf8Json);
            }
            catch (DecoderFallbackException exception)
            {
                throw new FormatException("The JSON input is not valid UTF-8.", exception);
            }

            if (text.Length != 0 && text[0] == '\ufeff')
            {
                throw new FormatException("A UTF-8 BOM is not permitted in canonical JSON.");
            }

            return new Parser(text).ParseDocument();
        }

        private sealed class Parser
        {
            private const long MaximumIJsonSafeInteger = 9007199254740991L;

            private readonly string _text;
            private int _index;

            public Parser(string text)
            {
                _text = text;
            }

            public StrictJsonValue ParseDocument()
            {
                SkipWhitespace();
                var value = ParseValue();
                SkipWhitespace();
                if (_index != _text.Length)
                {
                    throw Error("Trailing JSON tokens are not permitted.");
                }

                return value;
            }

            private StrictJsonValue ParseValue()
            {
                if (_index >= _text.Length)
                {
                    throw Error("A JSON value was expected.");
                }

                switch (_text[_index])
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return StrictJsonValue.FromString(ParseString());
                    case 't':
                        ConsumeLiteral("true");
                        return StrictJsonValue.FromBoolean(true);
                    case 'f':
                        ConsumeLiteral("false");
                        return StrictJsonValue.FromBoolean(false);
                    case 'n':
                        ConsumeLiteral("null");
                        return StrictJsonValue.Null();
                    default:
                        if (_text[_index] == '-' || IsDigit(_text[_index]))
                        {
                            return ParseInteger();
                        }

                        throw Error("An invalid JSON token was encountered.");
                }
            }

            private StrictJsonValue ParseObject()
            {
                _index++;
                SkipWhitespace();
                var properties = new List<StrictJsonProperty>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                if (TryConsume('}'))
                {
                    return StrictJsonValue.FromObject(new StrictJsonObject(properties));
                }

                while (true)
                {
                    if (_index >= _text.Length || _text[_index] != '"')
                    {
                        throw Error("A JSON object property name was expected.");
                    }

                    var name = ParseString();
                    if (!names.Add(name))
                    {
                        throw Error("Duplicate JSON object property: " + name);
                    }

                    SkipWhitespace();
                    Require(':');
                    SkipWhitespace();
                    properties.Add(new StrictJsonProperty(name, ParseValue()));
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return StrictJsonValue.FromObject(new StrictJsonObject(properties));
                    }

                    Require(',');
                    SkipWhitespace();
                }
            }

            private StrictJsonValue ParseArray()
            {
                _index++;
                SkipWhitespace();
                var values = new List<StrictJsonValue>();
                if (TryConsume(']'))
                {
                    return StrictJsonValue.FromArray(values);
                }

                while (true)
                {
                    values.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return StrictJsonValue.FromArray(values);
                    }

                    Require(',');
                    SkipWhitespace();
                }
            }

            private string ParseString()
            {
                Require('"');
                var builder = new StringBuilder();
                while (_index < _text.Length)
                {
                    var character = _text[_index++];
                    if (character == '"')
                    {
                        return builder.ToString();
                    }

                    if (character == '\\')
                    {
                        AppendEscape(builder);
                        continue;
                    }

                    if (character < 0x20)
                    {
                        throw Error("A JSON string contains an unescaped control character.");
                    }

                    if (char.IsHighSurrogate(character))
                    {
                        if (_index >= _text.Length || !char.IsLowSurrogate(_text[_index]))
                        {
                            throw Error("A JSON string contains a lone surrogate.");
                        }

                        builder.Append(character);
                        builder.Append(_text[_index++]);
                    }
                    else if (char.IsLowSurrogate(character))
                    {
                        throw Error("A JSON string contains a lone surrogate.");
                    }
                    else
                    {
                        builder.Append(character);
                    }
                }

                throw Error("A JSON string is unterminated.");
            }

            private void AppendEscape(StringBuilder builder)
            {
                if (_index >= _text.Length)
                {
                    throw Error("A JSON escape sequence is incomplete.");
                }

                var escape = _text[_index++];
                switch (escape)
                {
                    case '"':
                        builder.Append('"');
                        return;
                    case '\\':
                        builder.Append('\\');
                        return;
                    case '/':
                        builder.Append('/');
                        return;
                    case 'b':
                        builder.Append('\b');
                        return;
                    case 'f':
                        builder.Append('\f');
                        return;
                    case 'n':
                        builder.Append('\n');
                        return;
                    case 'r':
                        builder.Append('\r');
                        return;
                    case 't':
                        builder.Append('\t');
                        return;
                    case 'u':
                        AppendUnicodeEscape(builder);
                        return;
                    default:
                        throw Error("A JSON string contains an invalid escape sequence.");
                }
            }

            private void AppendUnicodeEscape(StringBuilder builder)
            {
                var first = ReadHexCodeUnit();
                if (char.IsHighSurrogate(first))
                {
                    if (_index + 1 >= _text.Length ||
                        _text[_index] != '\\' ||
                        _text[_index + 1] != 'u')
                    {
                        throw Error("A JSON string contains a lone surrogate escape.");
                    }

                    _index += 2;
                    var second = ReadHexCodeUnit();
                    if (!char.IsLowSurrogate(second))
                    {
                        throw Error("A JSON string contains an invalid surrogate pair.");
                    }

                    builder.Append(first);
                    builder.Append(second);
                    return;
                }

                if (char.IsLowSurrogate(first))
                {
                    throw Error("A JSON string contains a lone surrogate escape.");
                }

                builder.Append(first);
            }

            private char ReadHexCodeUnit()
            {
                if (_index + 4 > _text.Length)
                {
                    throw Error("A Unicode escape must contain four hexadecimal digits.");
                }

                var value = 0;
                for (var count = 0; count < 4; count++)
                {
                    var digit = HexValue(_text[_index++]);
                    if (digit < 0)
                    {
                        throw Error("A Unicode escape contains a non-hexadecimal digit.");
                    }

                    value = (value << 4) | digit;
                }

                return (char)value;
            }

            private StrictJsonValue ParseInteger()
            {
                var start = _index;
                if (_text[_index] == '-')
                {
                    _index++;
                    if (_index >= _text.Length)
                    {
                        throw Error("A minus sign must be followed by an integer.");
                    }
                }

                if (_text[_index] == '0')
                {
                    _index++;
                    if (_index < _text.Length && IsDigit(_text[_index]))
                    {
                        throw Error("A JSON integer cannot contain a leading zero.");
                    }
                }
                else if (_text[_index] >= '1' && _text[_index] <= '9')
                {
                    while (_index < _text.Length && IsDigit(_text[_index]))
                    {
                        _index++;
                    }
                }
                else
                {
                    throw Error("An invalid JSON integer was encountered.");
                }

                if (_index < _text.Length &&
                    (_text[_index] == '.' || _text[_index] == 'e' || _text[_index] == 'E'))
                {
                    throw Error("Floating-point and scientific-notation numbers are not supported.");
                }

                var token = _text.Substring(start, _index - start);
                long value;
                if (!long.TryParse(
                        token,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out value) ||
                    value < -MaximumIJsonSafeInteger ||
                    value > MaximumIJsonSafeInteger)
                {
                    throw Error("A JSON integer is outside the I-JSON safe range.");
                }

                return StrictJsonValue.FromInteger(value);
            }

            private void ConsumeLiteral(string literal)
            {
                if (_index + literal.Length > _text.Length ||
                    !string.Equals(
                        _text.Substring(_index, literal.Length),
                        literal,
                        StringComparison.Ordinal))
                {
                    throw Error("An invalid JSON literal was encountered.");
                }

                _index += literal.Length;
            }

            private void SkipWhitespace()
            {
                while (_index < _text.Length)
                {
                    var character = _text[_index];
                    if (character != ' ' && character != '\t' &&
                        character != '\r' && character != '\n')
                    {
                        return;
                    }

                    _index++;
                }
            }

            private void Require(char character)
            {
                if (!TryConsume(character))
                {
                    throw Error("Expected JSON character '" + character + "'.");
                }
            }

            private bool TryConsume(char character)
            {
                if (_index >= _text.Length || _text[_index] != character)
                {
                    return false;
                }

                _index++;
                return true;
            }

            private FormatException Error(string message)
            {
                return new FormatException(message + " Character offset: " + _index + ".");
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9')
                {
                    return value - '0';
                }

                if (value >= 'a' && value <= 'f')
                {
                    return value - 'a' + 10;
                }

                if (value >= 'A' && value <= 'F')
                {
                    return value - 'A' + 10;
                }

                return -1;
            }
        }
    }

    internal enum StrictJsonKind
    {
        Object,
        Array,
        String,
        Integer,
        Boolean,
        Null
    }

    internal sealed class StrictJsonValue
    {
        private StrictJsonValue(
            StrictJsonKind kind,
            StrictJsonObject objectValue,
            IReadOnlyList<StrictJsonValue> arrayValue,
            string stringValue,
            long integerValue,
            bool booleanValue)
        {
            Kind = kind;
            ObjectValue = objectValue;
            ArrayValue = arrayValue;
            StringValue = stringValue;
            IntegerValue = integerValue;
            BooleanValue = booleanValue;
        }

        public StrictJsonKind Kind { get; }

        public StrictJsonObject ObjectValue { get; }

        public IReadOnlyList<StrictJsonValue> ArrayValue { get; }

        public string StringValue { get; }

        public long IntegerValue { get; }

        public bool BooleanValue { get; }

        public static StrictJsonValue FromObject(StrictJsonObject value)
        {
            return new StrictJsonValue(StrictJsonKind.Object, value, null, null, 0, false);
        }

        public static StrictJsonValue FromArray(IReadOnlyList<StrictJsonValue> value)
        {
            return new StrictJsonValue(StrictJsonKind.Array, null, value, null, 0, false);
        }

        public static StrictJsonValue FromString(string value)
        {
            return new StrictJsonValue(StrictJsonKind.String, null, null, value, 0, false);
        }

        public static StrictJsonValue FromInteger(long value)
        {
            return new StrictJsonValue(StrictJsonKind.Integer, null, null, null, value, false);
        }

        public static StrictJsonValue FromBoolean(bool value)
        {
            return new StrictJsonValue(StrictJsonKind.Boolean, null, null, null, 0, value);
        }

        public static StrictJsonValue Null()
        {
            return new StrictJsonValue(StrictJsonKind.Null, null, null, null, 0, false);
        }
    }

    internal sealed class StrictJsonObject
    {
        private readonly IReadOnlyList<StrictJsonProperty> _properties;
        private readonly Dictionary<string, StrictJsonValue> _byName;

        public StrictJsonObject(IReadOnlyList<StrictJsonProperty> properties)
        {
            _properties = properties;
            _byName = new Dictionary<string, StrictJsonValue>(StringComparer.Ordinal);
            for (var index = 0; index < properties.Count; index++)
            {
                _byName.Add(properties[index].Name, properties[index].Value);
            }
        }

        public StrictJsonValue Get(string propertyName)
        {
            return _byName[propertyName];
        }

        public bool ContainsUnknownProperty(params string[] propertyNames)
        {
            for (var propertyIndex = 0; propertyIndex < _properties.Count; propertyIndex++)
            {
                var known = false;
                for (var nameIndex = 0; nameIndex < propertyNames.Length; nameIndex++)
                {
                    if (string.Equals(
                            _properties[propertyIndex].Name,
                            propertyNames[nameIndex],
                            StringComparison.Ordinal))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                {
                    return true;
                }
            }

            return false;
        }

        public void RequireExactly(string path, params string[] propertyNames)
        {
            if (_properties.Count != propertyNames.Length)
            {
                throw new FormatException(
                    path + " contains a missing or unknown Schema V1 property.");
            }

            for (var index = 0; index < propertyNames.Length; index++)
            {
                if (!_byName.ContainsKey(propertyNames[index]))
                {
                    throw new FormatException(
                        path + " is missing required Schema V1 property '" +
                        propertyNames[index] + "'.");
                }
            }
        }
    }

    internal sealed class StrictJsonProperty
    {
        public StrictJsonProperty(string name, StrictJsonValue value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public StrictJsonValue Value { get; }
    }
}
