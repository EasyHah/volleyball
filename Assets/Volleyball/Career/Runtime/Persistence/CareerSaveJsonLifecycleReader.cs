using System.Collections.Generic;

namespace Volleyball.Career.Persistence
{
    public static partial class CareerSaveJsonCodec
    {
        private static PendingCareerMatchDocumentV2 ReadPendingMatch(
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
                "sessionId",
                "creationOperationId",
                "createdLineageId",
                "createdRevision",
                "versions",
                "executionMode",
                "fixtureId",
                "fixtureVersion",
                "matchSeed",
                "competitionId",
                "scheduleItemId",
                "sourceWeekPlanId",
                "sourceSlotActionId",
                "sourceActionOccurrenceId",
                "preMatchPriority",
                "contextHash",
                "canonicalContextUtf8Base64",
                "homeTeamId",
                "awayTeamId",
                "orderedPlayerIds",
                "protagonistPlayerId",
                "frozenTrainingEmphases");
            return new PendingCareerMatchDocumentV2
            {
                sessionId = RequiredString(document.Get("sessionId"), path + ".sessionId"),
                creationOperationId = RequiredString(
                    document.Get("creationOperationId"),
                    path + ".creationOperationId"),
                createdLineageId = RequiredString(
                    document.Get("createdLineageId"),
                    path + ".createdLineageId"),
                createdRevision = Int64(document.Get("createdRevision"), path + ".createdRevision"),
                versions = ReadMatchLifecycleVersions(document.Get("versions"), path + ".versions"),
                executionMode = RequiredString(document.Get("executionMode"), path + ".executionMode"),
                fixtureId = NullableString(document.Get("fixtureId"), path + ".fixtureId"),
                fixtureVersion = NullableInt32(document.Get("fixtureVersion"), path + ".fixtureVersion"),
                matchSeed = Int64(document.Get("matchSeed"), path + ".matchSeed"),
                competitionId = RequiredString(document.Get("competitionId"), path + ".competitionId"),
                scheduleItemId = RequiredString(document.Get("scheduleItemId"), path + ".scheduleItemId"),
                sourceWeekPlanId = RequiredString(document.Get("sourceWeekPlanId"), path + ".sourceWeekPlanId"),
                sourceSlotActionId = RequiredString(document.Get("sourceSlotActionId"), path + ".sourceSlotActionId"),
                sourceActionOccurrenceId = RequiredString(
                    document.Get("sourceActionOccurrenceId"),
                    path + ".sourceActionOccurrenceId"),
                preMatchPriority = RequiredString(document.Get("preMatchPriority"), path + ".preMatchPriority"),
                contextHash = RequiredString(document.Get("contextHash"), path + ".contextHash"),
                canonicalContextUtf8Base64 = RequiredString(
                    document.Get("canonicalContextUtf8Base64"),
                    path + ".canonicalContextUtf8Base64"),
                homeTeamId = RequiredString(document.Get("homeTeamId"), path + ".homeTeamId"),
                awayTeamId = RequiredString(document.Get("awayTeamId"), path + ".awayTeamId"),
                orderedPlayerIds = ReadStringArray(
                    document.Get("orderedPlayerIds"),
                    path + ".orderedPlayerIds"),
                protagonistPlayerId = RequiredString(
                    document.Get("protagonistPlayerId"),
                    path + ".protagonistPlayerId"),
                frozenTrainingEmphases = ReadFrozenTrainingEmphases(
                    document.Get("frozenTrainingEmphases"),
                    path + ".frozenTrainingEmphases")
            };
        }

        private static CareerMatchLifecycleVersionsDocumentV2 ReadMatchLifecycleVersions(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "contractVersion",
                "contentVersion",
                "rulesetVersion",
                "careerRandomAlgorithmVersion",
                "matchSimulationVersion",
                "matchRandomAlgorithmVersion");
            return new CareerMatchLifecycleVersionsDocumentV2
            {
                contractVersion = Int32(document.Get("contractVersion"), path + ".contractVersion"),
                contentVersion = Int32(document.Get("contentVersion"), path + ".contentVersion"),
                rulesetVersion = Int32(document.Get("rulesetVersion"), path + ".rulesetVersion"),
                careerRandomAlgorithmVersion = Int32(
                    document.Get("careerRandomAlgorithmVersion"),
                    path + ".careerRandomAlgorithmVersion"),
                matchSimulationVersion = NullableInt32(
                    document.Get("matchSimulationVersion"),
                    path + ".matchSimulationVersion"),
                matchRandomAlgorithmVersion = NullableInt32(
                    document.Get("matchRandomAlgorithmVersion"),
                    path + ".matchRandomAlgorithmVersion")
            };
        }

        private static FrozenCareerTrainingEmphasisDocumentV2[] ReadFrozenTrainingEmphases(
            StrictJsonValue value,
            string path)
        {
            var values = RequiredArray(value, path);
            var results = new FrozenCareerTrainingEmphasisDocumentV2[values.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = ExactObject(
                    values[index],
                    itemPath,
                    "direction",
                    "sourceSlotActionIds",
                    "totalBonusBasisPoints");
                results[index] = new FrozenCareerTrainingEmphasisDocumentV2
                {
                    direction = RequiredString(document.Get("direction"), itemPath + ".direction"),
                    sourceSlotActionIds = ReadStringArray(
                        document.Get("sourceSlotActionIds"),
                        itemPath + ".sourceSlotActionIds"),
                    totalBonusBasisPoints = Int32(
                        document.Get("totalBonusBasisPoints"),
                        itemPath + ".totalBonusBasisPoints")
                };
            }

            return results;
        }

        private static string[] ReadStringArray(StrictJsonValue value, string path)
        {
            var values = RequiredArray(value, path);
            var results = new string[values.Count];
            for (var index = 0; index < results.Length; index++)
            {
                results[index] = RequiredString(values[index], path + "[" + index + "]");
            }

            return results;
        }

        private static CareerMatchHistoryEntryDocumentV2[] ReadMatchHistory(
            StrictJsonValue value,
            string path)
        {
            var values = RequiredArray(value, path);
            var results = new CareerMatchHistoryEntryDocumentV2[values.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = ExactObject(
                    values[index],
                    itemPath,
                    "sessionId",
                    "scheduleItemId",
                    "sourceWeekPlanId",
                    "sourceSlotActionId",
                    "contextHash",
                    "resultHash",
                    "canonicalContextUtf8Base64",
                    "canonicalResultUtf8Base64",
                    "appliedLineageId",
                    "appliedRevision",
                    "settledAtUtcMs",
                    "settlementSummary");
                results[index] = new CareerMatchHistoryEntryDocumentV2
                {
                    sessionId = RequiredString(document.Get("sessionId"), itemPath + ".sessionId"),
                    scheduleItemId = RequiredString(document.Get("scheduleItemId"), itemPath + ".scheduleItemId"),
                    sourceWeekPlanId = RequiredString(document.Get("sourceWeekPlanId"), itemPath + ".sourceWeekPlanId"),
                    sourceSlotActionId = RequiredString(document.Get("sourceSlotActionId"), itemPath + ".sourceSlotActionId"),
                    contextHash = RequiredString(document.Get("contextHash"), itemPath + ".contextHash"),
                    resultHash = RequiredString(document.Get("resultHash"), itemPath + ".resultHash"),
                    canonicalContextUtf8Base64 = RequiredString(
                        document.Get("canonicalContextUtf8Base64"),
                        itemPath + ".canonicalContextUtf8Base64"),
                    canonicalResultUtf8Base64 = RequiredString(
                        document.Get("canonicalResultUtf8Base64"),
                        itemPath + ".canonicalResultUtf8Base64"),
                    appliedLineageId = RequiredString(document.Get("appliedLineageId"), itemPath + ".appliedLineageId"),
                    appliedRevision = Int64(document.Get("appliedRevision"), itemPath + ".appliedRevision"),
                    settledAtUtcMs = Int64(document.Get("settledAtUtcMs"), itemPath + ".settledAtUtcMs"),
                    settlementSummary = ReadSettlementSummary(
                        document.Get("settlementSummary"),
                        itemPath + ".settlementSummary")
                };
            }

            return results;
        }

        private static CareerSettlementReceiptDocumentV2[] ReadSettlementReceipts(
            StrictJsonValue value,
            string path)
        {
            var values = RequiredArray(value, path);
            var results = new CareerSettlementReceiptDocumentV2[values.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = ExactObject(
                    values[index],
                    itemPath,
                    "sessionId",
                    "contextHash",
                    "resultHash",
                    "appliedLineageId",
                    "appliedRevision",
                    "settledAtUtcMs",
                    "settlementSummary");
                results[index] = new CareerSettlementReceiptDocumentV2
                {
                    sessionId = RequiredString(document.Get("sessionId"), itemPath + ".sessionId"),
                    contextHash = RequiredString(document.Get("contextHash"), itemPath + ".contextHash"),
                    resultHash = RequiredString(document.Get("resultHash"), itemPath + ".resultHash"),
                    appliedLineageId = RequiredString(document.Get("appliedLineageId"), itemPath + ".appliedLineageId"),
                    appliedRevision = Int64(document.Get("appliedRevision"), itemPath + ".appliedRevision"),
                    settledAtUtcMs = Int64(document.Get("settledAtUtcMs"), itemPath + ".settledAtUtcMs"),
                    settlementSummary = ReadSettlementSummary(
                        document.Get("settlementSummary"),
                        itemPath + ".settlementSummary")
                };
            }

            return results;
        }

        private static CareerSettlementSummaryDocumentV2 ReadSettlementSummary(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "sets",
                "protagonistFacts",
                "selectedPriority",
                "priorityExecuted",
                "won",
                "growthChanges",
                "matchFatigueChange",
                "matchMindsetChange",
                "matchCoachTrustChange",
                "weekendFatigueChange",
                "weekendMindsetChange",
                "weekendCoachTrustChange");
            return new CareerSettlementSummaryDocumentV2
            {
                sets = ReadSetSummaries(document.Get("sets"), path + ".sets"),
                protagonistFacts = ReadProtagonistFacts(
                    document.Get("protagonistFacts"),
                    path + ".protagonistFacts"),
                selectedPriority = RequiredString(
                    document.Get("selectedPriority"),
                    path + ".selectedPriority"),
                priorityExecuted = Boolean(
                    document.Get("priorityExecuted"),
                    path + ".priorityExecuted"),
                won = Boolean(document.Get("won"), path + ".won"),
                growthChanges = ReadGrowthChanges(
                    document.Get("growthChanges"),
                    path + ".growthChanges"),
                matchFatigueChange = ReadReasonedIntegerChange(
                    document.Get("matchFatigueChange"),
                    path + ".matchFatigueChange"),
                matchMindsetChange = ReadReasonedIntegerChange(
                    document.Get("matchMindsetChange"),
                    path + ".matchMindsetChange"),
                matchCoachTrustChange = ReadReasonedIntegerChange(
                    document.Get("matchCoachTrustChange"),
                    path + ".matchCoachTrustChange"),
                weekendFatigueChange = ReadReasonedIntegerChange(
                    document.Get("weekendFatigueChange"),
                    path + ".weekendFatigueChange"),
                weekendMindsetChange = ReadReasonedIntegerChange(
                    document.Get("weekendMindsetChange"),
                    path + ".weekendMindsetChange"),
                weekendCoachTrustChange = ReadReasonedIntegerChange(
                    document.Get("weekendCoachTrustChange"),
                    path + ".weekendCoachTrustChange")
            };
        }

        private static CareerMatchSetScoreSummaryDocumentV2[] ReadSetSummaries(
            StrictJsonValue value,
            string path)
        {
            var values = RequiredArray(value, path);
            var results = new CareerMatchSetScoreSummaryDocumentV2[values.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = ExactObject(
                    values[index],
                    itemPath,
                    "setNumber",
                    "homePoints",
                    "awayPoints",
                    "isComplete");
                results[index] = new CareerMatchSetScoreSummaryDocumentV2
                {
                    setNumber = Int32(document.Get("setNumber"), itemPath + ".setNumber"),
                    homePoints = Int32(document.Get("homePoints"), itemPath + ".homePoints"),
                    awayPoints = Int32(document.Get("awayPoints"), itemPath + ".awayPoints"),
                    isComplete = Boolean(document.Get("isComplete"), itemPath + ".isComplete")
                };
            }

            return results;
        }

        private static CareerProtagonistMatchFactsDocumentV2 ReadProtagonistFacts(
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
                "load",
                "stability");
            return new CareerProtagonistMatchFactsDocumentV2
            {
                spike = ReadSpikeFacts(document.Get("spike"), path + ".spike"),
                serve = ReadServeFacts(document.Get("serve"), path + ".serve"),
                reception = ReadReceptionFacts(document.Get("reception"), path + ".reception"),
                defense = ReadDefenseFacts(document.Get("defense"), path + ".defense"),
                block = ReadBlockFacts(document.Get("block"), path + ".block"),
                load = ReadLoadFacts(document.Get("load"), path + ".load"),
                stability = ReadStabilityFacts(document.Get("stability"), path + ".stability")
            };
        }

        private static CareerSpikeFactSummaryDocumentV2 ReadSpikeFacts(StrictJsonValue value, string path)
        {
            var document = ExactObject(value, path, "attempts", "points", "errors");
            return new CareerSpikeFactSummaryDocumentV2
            {
                attempts = Int32(document.Get("attempts"), path + ".attempts"),
                points = Int32(document.Get("points"), path + ".points"),
                errors = Int32(document.Get("errors"), path + ".errors")
            };
        }

        private static CareerServeFactSummaryDocumentV2 ReadServeFacts(StrictJsonValue value, string path)
        {
            var document = ExactObject(value, path, "attempts", "aces", "errors");
            return new CareerServeFactSummaryDocumentV2
            {
                attempts = Int32(document.Get("attempts"), path + ".attempts"),
                aces = Int32(document.Get("aces"), path + ".aces"),
                errors = Int32(document.Get("errors"), path + ".errors")
            };
        }

        private static CareerReceptionFactSummaryDocumentV2 ReadReceptionFacts(StrictJsonValue value, string path)
        {
            var document = ExactObject(
                value,
                path,
                "attempts",
                "perfect",
                "positive",
                "neutral",
                "negative",
                "errors");
            return new CareerReceptionFactSummaryDocumentV2
            {
                attempts = Int32(document.Get("attempts"), path + ".attempts"),
                perfect = Int32(document.Get("perfect"), path + ".perfect"),
                positive = Int32(document.Get("positive"), path + ".positive"),
                neutral = Int32(document.Get("neutral"), path + ".neutral"),
                negative = Int32(document.Get("negative"), path + ".negative"),
                errors = Int32(document.Get("errors"), path + ".errors")
            };
        }

        private static CareerDefenseFactSummaryDocumentV2 ReadDefenseFacts(StrictJsonValue value, string path)
        {
            var document = ExactObject(value, path, "attempts", "successes");
            return new CareerDefenseFactSummaryDocumentV2
            {
                attempts = Int32(document.Get("attempts"), path + ".attempts"),
                successes = Int32(document.Get("successes"), path + ".successes")
            };
        }

        private static CareerBlockFactSummaryDocumentV2 ReadBlockFacts(StrictJsonValue value, string path)
        {
            var document = ExactObject(value, path, "attempts", "effectiveTouches", "points");
            return new CareerBlockFactSummaryDocumentV2
            {
                attempts = Int32(document.Get("attempts"), path + ".attempts"),
                effectiveTouches = Int32(document.Get("effectiveTouches"), path + ".effectiveTouches"),
                points = Int32(document.Get("points"), path + ".points")
            };
        }

        private static CareerMatchLoadFactSummaryDocumentV2 ReadLoadFacts(StrictJsonValue value, string path)
        {
            var document = ExactObject(
                value,
                path,
                "ralliesPlayed",
                "activeDurationMilliseconds",
                "movementDistanceMillimeters",
                "jumpCount",
                "highLoadJumpCount",
                "landingLoadBasisPoints",
                "totalWorkloadBasisPoints");
            return new CareerMatchLoadFactSummaryDocumentV2
            {
                ralliesPlayed = Int32(document.Get("ralliesPlayed"), path + ".ralliesPlayed"),
                activeDurationMilliseconds = Int64(
                    document.Get("activeDurationMilliseconds"),
                    path + ".activeDurationMilliseconds"),
                movementDistanceMillimeters = Int64(
                    document.Get("movementDistanceMillimeters"),
                    path + ".movementDistanceMillimeters"),
                jumpCount = Int32(document.Get("jumpCount"), path + ".jumpCount"),
                highLoadJumpCount = Int32(document.Get("highLoadJumpCount"), path + ".highLoadJumpCount"),
                landingLoadBasisPoints = Int32(
                    document.Get("landingLoadBasisPoints"),
                    path + ".landingLoadBasisPoints"),
                totalWorkloadBasisPoints = Int32(
                    document.Get("totalWorkloadBasisPoints"),
                    path + ".totalWorkloadBasisPoints")
            };
        }

        private static CareerStabilityFactSummaryDocumentV2 ReadStabilityFacts(StrictJsonValue value, string path)
        {
            var document = ExactObject(
                value,
                path,
                "criticalActions",
                "criticalSuccesses",
                "criticalErrors",
                "errorStreakEpisodes",
                "longestErrorStreak");
            return new CareerStabilityFactSummaryDocumentV2
            {
                criticalActions = Int32(document.Get("criticalActions"), path + ".criticalActions"),
                criticalSuccesses = Int32(document.Get("criticalSuccesses"), path + ".criticalSuccesses"),
                criticalErrors = Int32(document.Get("criticalErrors"), path + ".criticalErrors"),
                errorStreakEpisodes = Int32(document.Get("errorStreakEpisodes"), path + ".errorStreakEpisodes"),
                longestErrorStreak = Int32(document.Get("longestErrorStreak"), path + ".longestErrorStreak")
            };
        }

        private static CareerAttributeGrowthChangeDocumentV2[] ReadGrowthChanges(
            StrictJsonValue value,
            string path)
        {
            var values = RequiredArray(value, path);
            var results = new CareerAttributeGrowthChangeDocumentV2[values.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = ExactObject(
                    values[index],
                    itemPath,
                    "attribute",
                    "reasonId",
                    "before",
                    "requestedDelta",
                    "actualDelta",
                    "after");
                results[index] = new CareerAttributeGrowthChangeDocumentV2
                {
                    attribute = RequiredString(document.Get("attribute"), itemPath + ".attribute"),
                    reasonId = RequiredString(document.Get("reasonId"), itemPath + ".reasonId"),
                    before = ReadAttributeProgress(document.Get("before"), itemPath + ".before"),
                    requestedDelta = Int64(document.Get("requestedDelta"), itemPath + ".requestedDelta"),
                    actualDelta = Int64(document.Get("actualDelta"), itemPath + ".actualDelta"),
                    after = ReadAttributeProgress(document.Get("after"), itemPath + ".after")
                };
            }

            return results;
        }

        private static CareerReasonedIntegerChangeDocumentV2 ReadReasonedIntegerChange(
            StrictJsonValue value,
            string path)
        {
            var document = ExactObject(
                value,
                path,
                "reasonId",
                "oldValue",
                "requestedDelta",
                "actualDelta",
                "newValue");
            return new CareerReasonedIntegerChangeDocumentV2
            {
                reasonId = RequiredString(document.Get("reasonId"), path + ".reasonId"),
                oldValue = Int32(document.Get("oldValue"), path + ".oldValue"),
                requestedDelta = Int32(document.Get("requestedDelta"), path + ".requestedDelta"),
                actualDelta = Int32(document.Get("actualDelta"), path + ".actualDelta"),
                newValue = Int32(document.Get("newValue"), path + ".newValue")
            };
        }
    }
}
