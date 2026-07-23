namespace Volleyball.Career.Persistence
{
    public static partial class CareerSaveJsonCodec
    {
        private static void WritePendingMatch(
            CanonicalJsonWriter writer,
            PendingCareerMatchDocumentV2 document)
        {
            if (document == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("sessionId");
            writer.WriteString(document.sessionId);
            writer.WritePropertyName("creationOperationId");
            writer.WriteString(document.creationOperationId);
            writer.WritePropertyName("createdLineageId");
            writer.WriteString(document.createdLineageId);
            writer.WritePropertyName("createdRevision");
            writer.WriteInt64(document.createdRevision);
            writer.WritePropertyName("versions");
            WriteMatchLifecycleVersions(writer, document.versions);
            writer.WritePropertyName("executionMode");
            writer.WriteString(document.executionMode);
            writer.WritePropertyName("fixtureId");
            writer.WriteNullableString(document.fixtureId);
            writer.WritePropertyName("fixtureVersion");
            writer.WriteNullableInt32(document.fixtureVersion);
            writer.WritePropertyName("matchSeed");
            writer.WriteInt64(document.matchSeed);
            writer.WritePropertyName("competitionId");
            writer.WriteString(document.competitionId);
            writer.WritePropertyName("scheduleItemId");
            writer.WriteString(document.scheduleItemId);
            writer.WritePropertyName("sourceWeekPlanId");
            writer.WriteString(document.sourceWeekPlanId);
            writer.WritePropertyName("sourceSlotActionId");
            writer.WriteString(document.sourceSlotActionId);
            writer.WritePropertyName("sourceActionOccurrenceId");
            writer.WriteString(document.sourceActionOccurrenceId);
            writer.WritePropertyName("preMatchPriority");
            writer.WriteString(document.preMatchPriority);
            writer.WritePropertyName("contextHash");
            writer.WriteString(document.contextHash);
            writer.WritePropertyName("canonicalContextUtf8Base64");
            writer.WriteString(document.canonicalContextUtf8Base64);
            writer.WritePropertyName("homeTeamId");
            writer.WriteString(document.homeTeamId);
            writer.WritePropertyName("awayTeamId");
            writer.WriteString(document.awayTeamId);
            writer.WritePropertyName("orderedPlayerIds");
            WriteStringArray(writer, document.orderedPlayerIds, "pendingMatch.orderedPlayerIds");
            writer.WritePropertyName("protagonistPlayerId");
            writer.WriteString(document.protagonistPlayerId);
            writer.WritePropertyName("frozenTrainingEmphases");
            WriteFrozenTrainingEmphases(writer, document.frozenTrainingEmphases);
            writer.WriteEndObject();
        }

        private static void WriteMatchLifecycleVersions(
            CanonicalJsonWriter writer,
            CareerMatchLifecycleVersionsDocumentV2 document)
        {
            RequireWriteValue(document, "pendingMatch.versions");
            writer.WriteStartObject();
            writer.WritePropertyName("contractVersion");
            writer.WriteInt64(document.contractVersion);
            writer.WritePropertyName("contentVersion");
            writer.WriteInt64(document.contentVersion);
            writer.WritePropertyName("rulesetVersion");
            writer.WriteInt64(document.rulesetVersion);
            writer.WritePropertyName("careerRandomAlgorithmVersion");
            writer.WriteInt64(document.careerRandomAlgorithmVersion);
            writer.WritePropertyName("matchSimulationVersion");
            writer.WriteNullableInt32(document.matchSimulationVersion);
            writer.WritePropertyName("matchRandomAlgorithmVersion");
            writer.WriteNullableInt32(document.matchRandomAlgorithmVersion);
            writer.WriteEndObject();
        }

        private static void WriteFrozenTrainingEmphases(
            CanonicalJsonWriter writer,
            FrozenCareerTrainingEmphasisDocumentV2[] documents)
        {
            RequireWriteValue(documents, "pendingMatch.frozenTrainingEmphases");
            writer.WriteStartArray();
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                RequireWriteValue(document, "pendingMatch.frozenTrainingEmphases[]");
                writer.WriteStartObject();
                writer.WritePropertyName("direction");
                writer.WriteString(document.direction);
                writer.WritePropertyName("sourceSlotActionIds");
                WriteStringArray(
                    writer,
                    document.sourceSlotActionIds,
                    "pendingMatch.frozenTrainingEmphases[].sourceSlotActionIds");
                writer.WritePropertyName("totalBonusBasisPoints");
                writer.WriteInt64(document.totalBonusBasisPoints);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteStringArray(
            CanonicalJsonWriter writer,
            string[] values,
            string path)
        {
            RequireWriteValue(values, path);
            writer.WriteStartArray();
            for (var index = 0; index < values.Length; index++)
            {
                writer.WriteString(values[index]);
            }

            writer.WriteEndArray();
        }

        private static void WriteMatchHistory(
            CanonicalJsonWriter writer,
            CareerMatchHistoryEntryDocumentV2[] documents)
        {
            RequireWriteValue(documents, "matchHistory");
            writer.WriteStartArray();
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                RequireWriteValue(document, "matchHistory[]");
                writer.WriteStartObject();
                writer.WritePropertyName("sessionId");
                writer.WriteString(document.sessionId);
                writer.WritePropertyName("scheduleItemId");
                writer.WriteString(document.scheduleItemId);
                writer.WritePropertyName("sourceWeekPlanId");
                writer.WriteString(document.sourceWeekPlanId);
                writer.WritePropertyName("sourceSlotActionId");
                writer.WriteString(document.sourceSlotActionId);
                writer.WritePropertyName("contextHash");
                writer.WriteString(document.contextHash);
                writer.WritePropertyName("resultHash");
                writer.WriteString(document.resultHash);
                writer.WritePropertyName("canonicalContextUtf8Base64");
                writer.WriteString(document.canonicalContextUtf8Base64);
                writer.WritePropertyName("canonicalResultUtf8Base64");
                writer.WriteString(document.canonicalResultUtf8Base64);
                writer.WritePropertyName("appliedLineageId");
                writer.WriteString(document.appliedLineageId);
                writer.WritePropertyName("appliedRevision");
                writer.WriteInt64(document.appliedRevision);
                writer.WritePropertyName("settledAtUtcMs");
                writer.WriteInt64(document.settledAtUtcMs);
                writer.WritePropertyName("settlementSummary");
                WriteSettlementSummary(writer, document.settlementSummary);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteSettlementReceipts(
            CanonicalJsonWriter writer,
            CareerSettlementReceiptDocumentV2[] documents)
        {
            RequireWriteValue(documents, "settlementReceipts");
            writer.WriteStartArray();
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                RequireWriteValue(document, "settlementReceipts[]");
                writer.WriteStartObject();
                writer.WritePropertyName("sessionId");
                writer.WriteString(document.sessionId);
                writer.WritePropertyName("contextHash");
                writer.WriteString(document.contextHash);
                writer.WritePropertyName("resultHash");
                writer.WriteString(document.resultHash);
                writer.WritePropertyName("appliedLineageId");
                writer.WriteString(document.appliedLineageId);
                writer.WritePropertyName("appliedRevision");
                writer.WriteInt64(document.appliedRevision);
                writer.WritePropertyName("settledAtUtcMs");
                writer.WriteInt64(document.settledAtUtcMs);
                writer.WritePropertyName("settlementSummary");
                WriteSettlementSummary(writer, document.settlementSummary);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteSettlementSummary(
            CanonicalJsonWriter writer,
            CareerSettlementSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "settlementSummary");
            writer.WriteStartObject();
            writer.WritePropertyName("sets");
            WriteSetSummaries(writer, document.sets);
            writer.WritePropertyName("protagonistFacts");
            WriteProtagonistFacts(writer, document.protagonistFacts);
            writer.WritePropertyName("selectedPriority");
            writer.WriteString(document.selectedPriority);
            writer.WritePropertyName("priorityExecuted");
            writer.WriteBoolean(document.priorityExecuted);
            writer.WritePropertyName("won");
            writer.WriteBoolean(document.won);
            writer.WritePropertyName("growthChanges");
            WriteGrowthChanges(writer, document.growthChanges);
            writer.WritePropertyName("matchFatigueChange");
            WriteReasonedIntegerChange(writer, document.matchFatigueChange);
            writer.WritePropertyName("matchMindsetChange");
            WriteReasonedIntegerChange(writer, document.matchMindsetChange);
            writer.WritePropertyName("matchCoachTrustChange");
            WriteReasonedIntegerChange(writer, document.matchCoachTrustChange);
            writer.WritePropertyName("weekendFatigueChange");
            WriteReasonedIntegerChange(writer, document.weekendFatigueChange);
            writer.WritePropertyName("weekendMindsetChange");
            WriteReasonedIntegerChange(writer, document.weekendMindsetChange);
            writer.WritePropertyName("weekendCoachTrustChange");
            WriteReasonedIntegerChange(writer, document.weekendCoachTrustChange);
            writer.WriteEndObject();
        }

        private static void WriteSetSummaries(
            CanonicalJsonWriter writer,
            CareerMatchSetScoreSummaryDocumentV2[] documents)
        {
            RequireWriteValue(documents, "settlementSummary.sets");
            writer.WriteStartArray();
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                RequireWriteValue(document, "settlementSummary.sets[]");
                writer.WriteStartObject();
                writer.WritePropertyName("setNumber");
                writer.WriteInt64(document.setNumber);
                writer.WritePropertyName("homePoints");
                writer.WriteInt64(document.homePoints);
                writer.WritePropertyName("awayPoints");
                writer.WriteInt64(document.awayPoints);
                writer.WritePropertyName("isComplete");
                writer.WriteBoolean(document.isComplete);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteProtagonistFacts(
            CanonicalJsonWriter writer,
            CareerProtagonistMatchFactsDocumentV2 document)
        {
            RequireWriteValue(document, "settlementSummary.protagonistFacts");
            writer.WriteStartObject();
            writer.WritePropertyName("spike");
            WriteSpikeFacts(writer, document.spike);
            writer.WritePropertyName("serve");
            WriteServeFacts(writer, document.serve);
            writer.WritePropertyName("reception");
            WriteReceptionFacts(writer, document.reception);
            writer.WritePropertyName("defense");
            WriteDefenseFacts(writer, document.defense);
            writer.WritePropertyName("block");
            WriteBlockFacts(writer, document.block);
            writer.WritePropertyName("load");
            WriteLoadFacts(writer, document.load);
            writer.WritePropertyName("stability");
            WriteStabilityFacts(writer, document.stability);
            writer.WriteEndObject();
        }

        private static void WriteSpikeFacts(CanonicalJsonWriter writer, CareerSpikeFactSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "protagonistFacts.spike");
            writer.WriteStartObject();
            WriteNamedInt(writer, "attempts", document.attempts);
            WriteNamedInt(writer, "points", document.points);
            WriteNamedInt(writer, "errors", document.errors);
            writer.WriteEndObject();
        }

        private static void WriteServeFacts(CanonicalJsonWriter writer, CareerServeFactSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "protagonistFacts.serve");
            writer.WriteStartObject();
            WriteNamedInt(writer, "attempts", document.attempts);
            WriteNamedInt(writer, "aces", document.aces);
            WriteNamedInt(writer, "errors", document.errors);
            writer.WriteEndObject();
        }

        private static void WriteReceptionFacts(CanonicalJsonWriter writer, CareerReceptionFactSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "protagonistFacts.reception");
            writer.WriteStartObject();
            WriteNamedInt(writer, "attempts", document.attempts);
            WriteNamedInt(writer, "perfect", document.perfect);
            WriteNamedInt(writer, "positive", document.positive);
            WriteNamedInt(writer, "neutral", document.neutral);
            WriteNamedInt(writer, "negative", document.negative);
            WriteNamedInt(writer, "errors", document.errors);
            writer.WriteEndObject();
        }

        private static void WriteDefenseFacts(CanonicalJsonWriter writer, CareerDefenseFactSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "protagonistFacts.defense");
            writer.WriteStartObject();
            WriteNamedInt(writer, "attempts", document.attempts);
            WriteNamedInt(writer, "successes", document.successes);
            writer.WriteEndObject();
        }

        private static void WriteBlockFacts(CanonicalJsonWriter writer, CareerBlockFactSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "protagonistFacts.block");
            writer.WriteStartObject();
            WriteNamedInt(writer, "attempts", document.attempts);
            WriteNamedInt(writer, "effectiveTouches", document.effectiveTouches);
            WriteNamedInt(writer, "points", document.points);
            writer.WriteEndObject();
        }

        private static void WriteLoadFacts(CanonicalJsonWriter writer, CareerMatchLoadFactSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "protagonistFacts.load");
            writer.WriteStartObject();
            WriteNamedInt(writer, "ralliesPlayed", document.ralliesPlayed);
            WriteNamedLong(writer, "activeDurationMilliseconds", document.activeDurationMilliseconds);
            WriteNamedLong(writer, "movementDistanceMillimeters", document.movementDistanceMillimeters);
            WriteNamedInt(writer, "jumpCount", document.jumpCount);
            WriteNamedInt(writer, "highLoadJumpCount", document.highLoadJumpCount);
            WriteNamedInt(writer, "landingLoadBasisPoints", document.landingLoadBasisPoints);
            WriteNamedInt(writer, "totalWorkloadBasisPoints", document.totalWorkloadBasisPoints);
            writer.WriteEndObject();
        }

        private static void WriteStabilityFacts(CanonicalJsonWriter writer, CareerStabilityFactSummaryDocumentV2 document)
        {
            RequireWriteValue(document, "protagonistFacts.stability");
            writer.WriteStartObject();
            WriteNamedInt(writer, "criticalActions", document.criticalActions);
            WriteNamedInt(writer, "criticalSuccesses", document.criticalSuccesses);
            WriteNamedInt(writer, "criticalErrors", document.criticalErrors);
            WriteNamedInt(writer, "errorStreakEpisodes", document.errorStreakEpisodes);
            WriteNamedInt(writer, "longestErrorStreak", document.longestErrorStreak);
            writer.WriteEndObject();
        }

        private static void WriteGrowthChanges(
            CanonicalJsonWriter writer,
            CareerAttributeGrowthChangeDocumentV2[] documents)
        {
            RequireWriteValue(documents, "settlementSummary.growthChanges");
            writer.WriteStartArray();
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                RequireWriteValue(document, "settlementSummary.growthChanges[]");
                writer.WriteStartObject();
                writer.WritePropertyName("attribute");
                writer.WriteString(document.attribute);
                writer.WritePropertyName("reasonId");
                writer.WriteString(document.reasonId);
                writer.WritePropertyName("before");
                WriteAttributeProgress(writer, document.before);
                writer.WritePropertyName("requestedDelta");
                writer.WriteInt64(document.requestedDelta);
                writer.WritePropertyName("actualDelta");
                writer.WriteInt64(document.actualDelta);
                writer.WritePropertyName("after");
                WriteAttributeProgress(writer, document.after);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteReasonedIntegerChange(
            CanonicalJsonWriter writer,
            CareerReasonedIntegerChangeDocumentV2 document)
        {
            RequireWriteValue(document, "settlementSummary.statusChange");
            writer.WriteStartObject();
            writer.WritePropertyName("reasonId");
            writer.WriteString(document.reasonId);
            WriteNamedInt(writer, "oldValue", document.oldValue);
            WriteNamedInt(writer, "requestedDelta", document.requestedDelta);
            WriteNamedInt(writer, "actualDelta", document.actualDelta);
            WriteNamedInt(writer, "newValue", document.newValue);
            writer.WriteEndObject();
        }

        private static void WriteNamedInt(CanonicalJsonWriter writer, string name, int value)
        {
            writer.WritePropertyName(name);
            writer.WriteInt64(value);
        }

        private static void WriteNamedLong(CanonicalJsonWriter writer, string name, long value)
        {
            writer.WritePropertyName(name);
            writer.WriteInt64(value);
        }
    }
}
