using System;
using System.Collections.Generic;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Persistence
{
    public static partial class CareerSaveSnapshotMapper
    {
        private static OperationReceiptDocumentV2[] ToOperationReceiptDocuments(
            IReadOnlyList<OperationReceipt> receipts)
        {
            var ordered = new OperationReceipt[receipts.Count];
            for (var index = 0; index < ordered.Length; index++)
            {
                ordered[index] = receipts[index];
            }

            Array.Sort(ordered, (left, right) =>
                left.AppliedRevision.CompareTo(right.AppliedRevision));
            var documents = new OperationReceiptDocumentV2[ordered.Length];
            for (var index = 0; index < documents.Length; index++)
            {
                documents[index] = ToDocument(ordered[index]);
            }

            return documents;
        }

        private static void RequireAppliedRevisionOrder(
            OperationReceiptDocumentV2[] documents,
            string path)
        {
            long previous = 0;
            for (var index = 0; index < documents.Length; index++)
            {
                var document = Required(documents[index], path + "[" + index + "]");
                if (index != 0 && document.appliedRevision <= previous)
                {
                    throw new ArgumentException(
                        path + " must be ordered by strictly increasing appliedRevision.",
                        path);
                }

                previous = document.appliedRevision;
            }
        }

        private static PendingCareerMatchDocumentV2 ToDocument(PendingCareerMatch pending)
        {
            if (pending == null)
            {
                return null;
            }

            EnsureIJsonSafe(pending.CreatedRevision, "pendingMatch.createdRevision");
            var players = new string[pending.OrderedPlayerIds.Count];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = pending.OrderedPlayerIds[index].Value;
            }

            var emphases = new FrozenCareerTrainingEmphasisDocumentV2[
                pending.FrozenTrainingEmphases.Count];
            for (var index = 0; index < emphases.Length; index++)
            {
                emphases[index] = ToDocument(pending.FrozenTrainingEmphases[index]);
            }

            return new PendingCareerMatchDocumentV2
            {
                sessionId = CanonicalGuid(pending.SessionId),
                creationOperationId = CanonicalGuid(pending.CreationOperationId.Value),
                createdLineageId = CanonicalGuid(pending.CreatedLineageId.Value),
                createdRevision = pending.CreatedRevision,
                versions = new CareerMatchLifecycleVersionsDocumentV2
                {
                    contractVersion = pending.Versions.ContractVersion,
                    contentVersion = pending.Versions.ContentVersion,
                    rulesetVersion = pending.Versions.RulesetVersion,
                    careerRandomAlgorithmVersion = pending.Versions.CareerRandomAlgorithmVersion,
                    matchSimulationVersion = pending.Versions.MatchSimulationVersion,
                    matchRandomAlgorithmVersion = pending.Versions.MatchRandomAlgorithmVersion
                },
                executionMode = FormatExecutionMode(pending.ExecutionMode),
                fixtureId = pending.FixtureId,
                fixtureVersion = pending.FixtureVersion,
                matchSeed = pending.MatchSeed,
                competitionId = pending.CompetitionId,
                scheduleItemId = pending.ScheduleItemId,
                sourceWeekPlanId = CanonicalGuid(pending.SourceWeekPlanId.Value),
                sourceSlotActionId = CanonicalGuid(pending.SourceSlotActionId.Value),
                sourceActionOccurrenceId = CanonicalGuid(
                    pending.SourceActionOccurrenceId.Value),
                preMatchPriority = FormatPriority(pending.PreMatchPriority),
                contextHash = pending.ContextDigest.Value,
                canonicalContextUtf8Base64 = EncodeCanonicalBytes(
                    pending.CanonicalContextUtf8),
                homeTeamId = pending.HomeTeamId.Value,
                awayTeamId = pending.AwayTeamId.Value,
                orderedPlayerIds = players,
                protagonistPlayerId = pending.ProtagonistPlayerId.Value,
                frozenTrainingEmphases = emphases
            };
        }

        private static PendingCareerMatch ToDomain(
            PendingCareerMatchDocumentV2 document,
            string path)
        {
            if (document == null)
            {
                return null;
            }

            EnsureIJsonSafe(document.createdRevision, path + ".createdRevision");
            if (document.matchSeed < uint.MinValue || document.matchSeed > uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    path + ".matchSeed",
                    document.matchSeed,
                    "matchSeed must fit UInt32.");
            }

            var versions = Required(document.versions, path + ".versions");
            var playerDocuments = Required(document.orderedPlayerIds, path + ".orderedPlayerIds");
            var players = new PlayerId[playerDocuments.Length];
            var uniquePlayers = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < players.Length; index++)
            {
                var playerId = playerDocuments[index];
                if (playerId == null || !uniquePlayers.Add(playerId))
                {
                    throw new ArgumentException(
                        "pendingMatch.orderedPlayerIds must contain unique non-null IDs in business order.",
                        path);
                }

                players[index] = new PlayerId(playerId);
            }

            var emphasisDocuments = Required(
                document.frozenTrainingEmphases,
                path + ".frozenTrainingEmphases");
            var emphases = new FrozenCareerTrainingEmphasis[emphasisDocuments.Length];
            var previousDirection = -1;
            for (var index = 0; index < emphases.Length; index++)
            {
                var emphasis = ToDomain(
                    Required(
                        emphasisDocuments[index],
                        path + ".frozenTrainingEmphases[" + index + "]"),
                    path + ".frozenTrainingEmphases[" + index + "]");
                if ((int)emphasis.Direction <= previousDirection)
                {
                    throw new ArgumentException(
                        "Frozen emphases must be in unique direction enum order.",
                        path);
                }

                previousDirection = (int)emphasis.Direction;
                emphases[index] = emphasis;
            }

            return new PendingCareerMatch(
                ParseCanonicalGuid(document.sessionId, path + ".sessionId"),
                new OperationId(ParseCanonicalGuid(
                    document.creationOperationId,
                    path + ".creationOperationId")),
                new LineageId(ParseCanonicalGuid(
                    document.createdLineageId,
                    path + ".createdLineageId")),
                document.createdRevision,
                new CareerMatchLifecycleVersions(
                    versions.contractVersion,
                    versions.contentVersion,
                    versions.rulesetVersion,
                    versions.careerRandomAlgorithmVersion,
                    versions.matchSimulationVersion,
                    versions.matchRandomAlgorithmVersion),
                ParseExecutionMode(document.executionMode),
                document.fixtureId,
                document.fixtureVersion,
                (uint)document.matchSeed,
                document.competitionId,
                document.scheduleItemId,
                new WeekPlanId(ParseCanonicalGuid(
                    document.sourceWeekPlanId,
                    path + ".sourceWeekPlanId")),
                new SlotActionId(ParseCanonicalGuid(
                    document.sourceSlotActionId,
                    path + ".sourceSlotActionId")),
                new OccurrenceId(ParseCanonicalGuid(
                    document.sourceActionOccurrenceId,
                    path + ".sourceActionOccurrenceId")),
                ParsePriority(document.preMatchPriority, path + ".preMatchPriority"),
                Sha256Digest.Parse(document.contextHash),
                DecodeCanonicalBytes(
                    document.canonicalContextUtf8Base64,
                    path + ".canonicalContextUtf8Base64"),
                new TeamId(document.homeTeamId),
                new TeamId(document.awayTeamId),
                players,
                new PlayerId(document.protagonistPlayerId),
                emphases);
        }

        private static FrozenCareerTrainingEmphasisDocumentV2 ToDocument(
            FrozenCareerTrainingEmphasis emphasis)
        {
            var sources = new string[emphasis.SourceSlotActionIds.Count];
            for (var index = 0; index < sources.Length; index++)
            {
                sources[index] = CanonicalGuid(emphasis.SourceSlotActionIds[index].Value);
            }

            return new FrozenCareerTrainingEmphasisDocumentV2
            {
                direction = CareerTrainingDirectionIds.Format(emphasis.Direction),
                sourceSlotActionIds = sources,
                totalBonusBasisPoints = emphasis.TotalBonusBasisPoints
            };
        }

        private static FrozenCareerTrainingEmphasis ToDomain(
            FrozenCareerTrainingEmphasisDocumentV2 document,
            string path)
        {
            var sourceDocuments = Required(
                document.sourceSlotActionIds,
                path + ".sourceSlotActionIds");
            var sources = new SlotActionId[sourceDocuments.Length];
            var unique = new HashSet<Guid>();
            for (var index = 0; index < sources.Length; index++)
            {
                var parsed = ParseCanonicalGuid(
                    sourceDocuments[index],
                    path + ".sourceSlotActionIds[" + index + "]");
                if (!unique.Add(parsed))
                {
                    throw new ArgumentException(
                        "Frozen emphasis source IDs must be unique in original slot order.",
                        path);
                }

                sources[index] = new SlotActionId(parsed);
            }

            return new FrozenCareerTrainingEmphasis(
                CareerTrainingDirectionIds.Parse(document.direction),
                sources,
                document.totalBonusBasisPoints);
        }

        private static CareerMatchHistoryEntryDocumentV2[] ToDocument(
            IReadOnlyList<CareerMatchHistoryEntry> history)
        {
            var ordered = new CareerMatchHistoryEntry[history.Count];
            for (var index = 0; index < ordered.Length; index++) ordered[index] = history[index];
            Array.Sort(ordered, CompareHistory);
            var documents = new CareerMatchHistoryEntryDocumentV2[ordered.Length];
            for (var index = 0; index < documents.Length; index++)
            {
                var entry = ordered[index];
                EnsureIJsonSafe(entry.AppliedRevision, "matchHistory[].appliedRevision");
                EnsureIJsonSafe(entry.SettledAtUtcMs, "matchHistory[].settledAtUtcMs");
                documents[index] = new CareerMatchHistoryEntryDocumentV2
                {
                    sessionId = CanonicalGuid(entry.SessionId),
                    scheduleItemId = entry.ScheduleItemId,
                    sourceWeekPlanId = CanonicalGuid(entry.SourceWeekPlanId.Value),
                    sourceSlotActionId = CanonicalGuid(entry.SourceSlotActionId.Value),
                    contextHash = entry.ContextDigest.Value,
                    resultHash = entry.ResultDigest.Value,
                    canonicalContextUtf8Base64 = EncodeCanonicalBytes(entry.CanonicalContextUtf8),
                    canonicalResultUtf8Base64 = EncodeCanonicalBytes(entry.CanonicalResultUtf8),
                    appliedLineageId = CanonicalGuid(entry.AppliedLineageId.Value),
                    appliedRevision = entry.AppliedRevision,
                    settledAtUtcMs = entry.SettledAtUtcMs,
                    settlementSummary = ToDocument(entry.SettlementSummary)
                };
            }

            return documents;
        }

        private static CareerSettlementReceiptDocumentV2[] ToDocument(
            IReadOnlyList<CareerSettlementReceipt> receipts)
        {
            var ordered = new CareerSettlementReceipt[receipts.Count];
            for (var index = 0; index < ordered.Length; index++) ordered[index] = receipts[index];
            Array.Sort(ordered, CompareSettlementReceipt);
            var documents = new CareerSettlementReceiptDocumentV2[ordered.Length];
            for (var index = 0; index < documents.Length; index++)
            {
                var receipt = ordered[index];
                EnsureIJsonSafe(receipt.AppliedRevision, "settlementReceipts[].appliedRevision");
                EnsureIJsonSafe(receipt.SettledAtUtcMs, "settlementReceipts[].settledAtUtcMs");
                documents[index] = new CareerSettlementReceiptDocumentV2
                {
                    sessionId = CanonicalGuid(receipt.SessionId),
                    contextHash = receipt.ContextDigest.Value,
                    resultHash = receipt.ResultDigest.Value,
                    appliedLineageId = CanonicalGuid(receipt.AppliedLineageId.Value),
                    appliedRevision = receipt.AppliedRevision,
                    settledAtUtcMs = receipt.SettledAtUtcMs,
                    settlementSummary = ToDocument(receipt.SettlementSummary)
                };
            }

            return documents;
        }

        private static CareerMatchHistoryEntry[] ToDomain(
            CareerMatchHistoryEntryDocumentV2[] documents,
            string path)
        {
            var entries = new CareerMatchHistoryEntry[documents.Length];
            var sessions = new HashSet<Guid>();
            long previousRevision = 0;
            Guid previousSession = Guid.Empty;
            for (var index = 0; index < entries.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = Required(documents[index], itemPath);
                var session = ParseCanonicalGuid(document.sessionId, itemPath + ".sessionId");
                RequireLifecycleOrder(
                    index,
                    previousRevision,
                    previousSession,
                    document.appliedRevision,
                    session,
                    path);
                if (!sessions.Add(session))
                {
                    throw new ArgumentException("Match history session IDs must be unique.", path);
                }

                EnsureIJsonSafe(document.appliedRevision, itemPath + ".appliedRevision");
                EnsureIJsonSafe(document.settledAtUtcMs, itemPath + ".settledAtUtcMs");
                entries[index] = new CareerMatchHistoryEntry(
                    session,
                    document.scheduleItemId,
                    new WeekPlanId(ParseCanonicalGuid(
                        document.sourceWeekPlanId,
                        itemPath + ".sourceWeekPlanId")),
                    new SlotActionId(ParseCanonicalGuid(
                        document.sourceSlotActionId,
                        itemPath + ".sourceSlotActionId")),
                    Sha256Digest.Parse(document.contextHash),
                    Sha256Digest.Parse(document.resultHash),
                    DecodeCanonicalBytes(
                        document.canonicalContextUtf8Base64,
                        itemPath + ".canonicalContextUtf8Base64"),
                    DecodeCanonicalBytes(
                        document.canonicalResultUtf8Base64,
                        itemPath + ".canonicalResultUtf8Base64"),
                    new LineageId(ParseCanonicalGuid(
                        document.appliedLineageId,
                        itemPath + ".appliedLineageId")),
                    document.appliedRevision,
                    document.settledAtUtcMs,
                    ToDomain(Required(document.settlementSummary, itemPath + ".settlementSummary"),
                        itemPath + ".settlementSummary"));
                previousRevision = document.appliedRevision;
                previousSession = session;
            }

            return entries;
        }

        private static CareerSettlementReceipt[] ToDomain(
            CareerSettlementReceiptDocumentV2[] documents,
            string path)
        {
            var receipts = new CareerSettlementReceipt[documents.Length];
            var sessions = new HashSet<Guid>();
            long previousRevision = 0;
            Guid previousSession = Guid.Empty;
            for (var index = 0; index < receipts.Length; index++)
            {
                var itemPath = path + "[" + index + "]";
                var document = Required(documents[index], itemPath);
                var session = ParseCanonicalGuid(document.sessionId, itemPath + ".sessionId");
                RequireLifecycleOrder(
                    index,
                    previousRevision,
                    previousSession,
                    document.appliedRevision,
                    session,
                    path);
                if (!sessions.Add(session))
                {
                    throw new ArgumentException("Settlement receipt session IDs must be unique.", path);
                }

                EnsureIJsonSafe(document.appliedRevision, itemPath + ".appliedRevision");
                EnsureIJsonSafe(document.settledAtUtcMs, itemPath + ".settledAtUtcMs");
                receipts[index] = new CareerSettlementReceipt(
                    session,
                    Sha256Digest.Parse(document.contextHash),
                    Sha256Digest.Parse(document.resultHash),
                    new LineageId(ParseCanonicalGuid(
                        document.appliedLineageId,
                        itemPath + ".appliedLineageId")),
                    document.appliedRevision,
                    document.settledAtUtcMs,
                    ToDomain(Required(document.settlementSummary, itemPath + ".settlementSummary"),
                        itemPath + ".settlementSummary"));
                previousRevision = document.appliedRevision;
                previousSession = session;
            }

            return receipts;
        }

        private static CareerSettlementSummaryDocumentV2 ToDocument(
            CareerSettlementSummary summary)
        {
            var sets = new CareerMatchSetScoreSummaryDocumentV2[summary.Sets.Count];
            for (var index = 0; index < sets.Length; index++)
            {
                var set = summary.Sets[index];
                sets[index] = new CareerMatchSetScoreSummaryDocumentV2
                {
                    setNumber = set.SetNumber,
                    homePoints = set.HomePoints,
                    awayPoints = set.AwayPoints,
                    isComplete = set.IsComplete
                };
            }

            var growth = new CareerAttributeGrowthChangeDocumentV2[summary.GrowthChanges.Count];
            for (var index = 0; index < growth.Length; index++)
            {
                var change = summary.GrowthChanges[index];
                EnsureIJsonSafe(change.RequestedDelta, "settlementSummary.growthChanges[].requestedDelta");
                EnsureIJsonSafe(change.ActualDelta, "settlementSummary.growthChanges[].actualDelta");
                growth[index] = new CareerAttributeGrowthChangeDocumentV2
                {
                    attribute = FormatAttribute(change.Attribute),
                    reasonId = change.ReasonId,
                    before = ToDocument(change.Before),
                    requestedDelta = change.RequestedDelta,
                    actualDelta = change.ActualDelta,
                    after = ToDocument(change.After)
                };
            }

            return new CareerSettlementSummaryDocumentV2
            {
                sets = sets,
                protagonistFacts = ToDocument(summary.ProtagonistFacts),
                selectedPriority = FormatPriority(summary.SelectedPriority),
                priorityExecuted = summary.PriorityExecuted,
                won = summary.Won,
                growthChanges = growth,
                matchFatigueChange = ToDocument(summary.MatchFatigueChange),
                matchMindsetChange = ToDocument(summary.MatchMindsetChange),
                matchCoachTrustChange = ToDocument(summary.MatchCoachTrustChange),
                weekendFatigueChange = ToDocument(summary.WeekendFatigueChange),
                weekendMindsetChange = ToDocument(summary.WeekendMindsetChange),
                weekendCoachTrustChange = ToDocument(summary.WeekendCoachTrustChange)
            };
        }

        private static CareerSettlementSummary ToDomain(
            CareerSettlementSummaryDocumentV2 document,
            string path)
        {
            var setDocuments = Required(document.sets, path + ".sets");
            var sets = new CareerMatchSetScoreSummary[setDocuments.Length];
            for (var index = 0; index < sets.Length; index++)
            {
                var set = Required(setDocuments[index], path + ".sets[" + index + "]");
                if (set.setNumber != index + 1)
                {
                    throw new ArgumentException("Settlement sets must be in setNumber order.", path);
                }

                sets[index] = new CareerMatchSetScoreSummary(
                    set.setNumber,
                    set.homePoints,
                    set.awayPoints,
                    set.isComplete);
            }

            var growthDocuments = Required(document.growthChanges, path + ".growthChanges");
            var growth = new CareerAttributeGrowthChange[growthDocuments.Length];
            for (var index = 0; index < growth.Length; index++)
            {
                var changePath = path + ".growthChanges[" + index + "]";
                var change = Required(growthDocuments[index], changePath);
                var attribute = ParseAttribute(change.attribute, changePath + ".attribute");
                if ((int)attribute != index)
                {
                    throw new ArgumentException(
                        "Growth changes must contain all eight attributes in enum order.",
                        path);
                }

                EnsureIJsonSafe(change.requestedDelta, changePath + ".requestedDelta");
                EnsureIJsonSafe(change.actualDelta, changePath + ".actualDelta");
                growth[index] = new CareerAttributeGrowthChange(
                    attribute,
                    change.reasonId,
                    ToDomain(Required(change.before, changePath + ".before")),
                    change.requestedDelta,
                    change.actualDelta,
                    ToDomain(Required(change.after, changePath + ".after")));
            }

            return new CareerSettlementSummary(
                sets,
                ToDomain(Required(document.protagonistFacts, path + ".protagonistFacts")),
                ParsePriority(document.selectedPriority, path + ".selectedPriority"),
                document.priorityExecuted,
                document.won,
                growth,
                ToDomain(Required(document.matchFatigueChange, path + ".matchFatigueChange")),
                ToDomain(Required(document.matchMindsetChange, path + ".matchMindsetChange")),
                ToDomain(Required(document.matchCoachTrustChange, path + ".matchCoachTrustChange")),
                ToDomain(Required(document.weekendFatigueChange, path + ".weekendFatigueChange")),
                ToDomain(Required(document.weekendMindsetChange, path + ".weekendMindsetChange")),
                ToDomain(Required(document.weekendCoachTrustChange, path + ".weekendCoachTrustChange")));
        }

        private static CareerProtagonistMatchFactsDocumentV2 ToDocument(
            CareerProtagonistMatchFacts facts)
        {
            return new CareerProtagonistMatchFactsDocumentV2
            {
                spike = new CareerSpikeFactSummaryDocumentV2
                {
                    attempts = facts.Spike.Attempts,
                    points = facts.Spike.Points,
                    errors = facts.Spike.Errors
                },
                serve = new CareerServeFactSummaryDocumentV2
                {
                    attempts = facts.Serve.Attempts,
                    aces = facts.Serve.Aces,
                    errors = facts.Serve.Errors
                },
                reception = new CareerReceptionFactSummaryDocumentV2
                {
                    attempts = facts.Reception.Attempts,
                    perfect = facts.Reception.Perfect,
                    positive = facts.Reception.Positive,
                    neutral = facts.Reception.Neutral,
                    negative = facts.Reception.Negative,
                    errors = facts.Reception.Errors
                },
                defense = new CareerDefenseFactSummaryDocumentV2
                {
                    attempts = facts.Defense.Attempts,
                    successes = facts.Defense.Successes
                },
                block = new CareerBlockFactSummaryDocumentV2
                {
                    attempts = facts.Block.Attempts,
                    effectiveTouches = facts.Block.EffectiveTouches,
                    points = facts.Block.Points
                },
                load = new CareerMatchLoadFactSummaryDocumentV2
                {
                    ralliesPlayed = facts.Load.RalliesPlayed,
                    activeDurationMilliseconds = facts.Load.ActiveDurationMilliseconds,
                    movementDistanceMillimeters = facts.Load.MovementDistanceMillimeters,
                    jumpCount = facts.Load.JumpCount,
                    highLoadJumpCount = facts.Load.HighLoadJumpCount,
                    landingLoadBasisPoints = facts.Load.LandingLoadBasisPoints,
                    totalWorkloadBasisPoints = facts.Load.TotalWorkloadBasisPoints
                },
                stability = new CareerStabilityFactSummaryDocumentV2
                {
                    criticalActions = facts.Stability.CriticalActions,
                    criticalSuccesses = facts.Stability.CriticalSuccesses,
                    criticalErrors = facts.Stability.CriticalErrors,
                    errorStreakEpisodes = facts.Stability.ErrorStreakEpisodes,
                    longestErrorStreak = facts.Stability.LongestErrorStreak
                }
            };
        }

        private static CareerProtagonistMatchFacts ToDomain(
            CareerProtagonistMatchFactsDocumentV2 document)
        {
            var spike = Required(document.spike, "settlementSummary.protagonistFacts.spike");
            var serve = Required(document.serve, "settlementSummary.protagonistFacts.serve");
            var reception = Required(document.reception, "settlementSummary.protagonistFacts.reception");
            var defense = Required(document.defense, "settlementSummary.protagonistFacts.defense");
            var block = Required(document.block, "settlementSummary.protagonistFacts.block");
            var load = Required(document.load, "settlementSummary.protagonistFacts.load");
            var stability = Required(document.stability, "settlementSummary.protagonistFacts.stability");
            EnsureIJsonSafe(load.activeDurationMilliseconds, "protagonistFacts.load.activeDurationMilliseconds");
            EnsureIJsonSafe(load.movementDistanceMillimeters, "protagonistFacts.load.movementDistanceMillimeters");
            return new CareerProtagonistMatchFacts(
                new CareerSpikeFactSummary(spike.attempts, spike.points, spike.errors),
                new CareerServeFactSummary(serve.attempts, serve.aces, serve.errors),
                new CareerReceptionFactSummary(
                    reception.attempts,
                    reception.perfect,
                    reception.positive,
                    reception.neutral,
                    reception.negative,
                    reception.errors),
                new CareerDefenseFactSummary(defense.attempts, defense.successes),
                new CareerBlockFactSummary(block.attempts, block.effectiveTouches, block.points),
                new CareerMatchLoadFactSummary(
                    load.ralliesPlayed,
                    load.activeDurationMilliseconds,
                    load.movementDistanceMillimeters,
                    load.jumpCount,
                    load.highLoadJumpCount,
                    load.landingLoadBasisPoints,
                    load.totalWorkloadBasisPoints),
                new CareerStabilityFactSummary(
                    stability.criticalActions,
                    stability.criticalSuccesses,
                    stability.criticalErrors,
                    stability.errorStreakEpisodes,
                    stability.longestErrorStreak));
        }

        private static CareerReasonedIntegerChangeDocumentV2 ToDocument(
            CareerReasonedIntegerChange change)
        {
            return new CareerReasonedIntegerChangeDocumentV2
            {
                reasonId = change.ReasonId,
                oldValue = change.OldValue,
                requestedDelta = change.RequestedDelta,
                actualDelta = change.ActualDelta,
                newValue = change.NewValue
            };
        }

        private static CareerReasonedIntegerChange ToDomain(
            CareerReasonedIntegerChangeDocumentV2 document)
        {
            return new CareerReasonedIntegerChange(
                document.reasonId,
                document.oldValue,
                document.requestedDelta,
                document.actualDelta,
                document.newValue);
        }

        private static string EncodeCanonicalBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("Canonical payload bytes cannot be empty.", nameof(bytes));
            }

            return Convert.ToBase64String(bytes);
        }

        private static byte[] DecodeCanonicalBytes(string value, string path)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Canonical Base64 is required.", path);
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(value);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("Canonical Base64 is invalid.", path, exception);
            }

            if (bytes.Length == 0 ||
                !string.Equals(value, Convert.ToBase64String(bytes), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Base64 must use the canonical alphabet, padding and no whitespace.",
                    path);
            }

            return bytes;
        }

        private static int CompareHistory(CareerMatchHistoryEntry left, CareerMatchHistoryEntry right)
        {
            var revision = left.AppliedRevision.CompareTo(right.AppliedRevision);
            return revision != 0
                ? revision
                : string.CompareOrdinal(
                    CanonicalGuid(left.SessionId),
                    CanonicalGuid(right.SessionId));
        }

        private static int CompareSettlementReceipt(
            CareerSettlementReceipt left,
            CareerSettlementReceipt right)
        {
            var revision = left.AppliedRevision.CompareTo(right.AppliedRevision);
            return revision != 0
                ? revision
                : string.CompareOrdinal(
                    CanonicalGuid(left.SessionId),
                    CanonicalGuid(right.SessionId));
        }

        private static void RequireLifecycleOrder(
            int index,
            long previousRevision,
            Guid previousSession,
            long revision,
            Guid session,
            string path)
        {
            if (index == 0)
            {
                return;
            }

            if (revision < previousRevision ||
                (revision == previousRevision && string.CompareOrdinal(
                    CanonicalGuid(session),
                    CanonicalGuid(previousSession)) <= 0))
            {
                throw new ArgumentException(
                    path + " must be ordered by appliedRevision then lowercase session GUID.",
                    path);
            }
        }

        private static string FormatExecutionMode(CareerMatchLifecycleExecutionMode value)
        {
            switch (value)
            {
                case CareerMatchLifecycleExecutionMode.Fixture: return "fixture";
                case CareerMatchLifecycleExecutionMode.Direct: return "direct";
                case CareerMatchLifecycleExecutionMode.QuickSimulation: return "quick_simulation";
                default: throw UnknownEnum(nameof(value), value);
            }
        }

        private static CareerMatchLifecycleExecutionMode ParseExecutionMode(string value)
        {
            switch (value)
            {
                case "fixture": return CareerMatchLifecycleExecutionMode.Fixture;
                case "direct": return CareerMatchLifecycleExecutionMode.Direct;
                case "quick_simulation": return CareerMatchLifecycleExecutionMode.QuickSimulation;
                default: throw UnknownTextEnum("pendingMatch.executionMode", value);
            }
        }

        private static string FormatPriority(CareerMatchPriority value)
        {
            switch (value)
            {
                case CareerMatchPriority.AttackFirst: return "attack_first";
                case CareerMatchPriority.FirstContactSecurity: return "first_contact_security";
                case CareerMatchPriority.StaminaControl: return "stamina_control";
                default: throw UnknownEnum(nameof(value), value);
            }
        }

        private static CareerMatchPriority ParsePriority(string value, string path)
        {
            switch (value)
            {
                case "attack_first": return CareerMatchPriority.AttackFirst;
                case "first_contact_security": return CareerMatchPriority.FirstContactSecurity;
                case "stamina_control": return CareerMatchPriority.StaminaControl;
                default: throw UnknownTextEnum(path, value);
            }
        }

        private static string FormatAttribute(CareerAttributeKind value)
        {
            switch (value)
            {
                case CareerAttributeKind.Spike: return "spike";
                case CareerAttributeKind.Serve: return "serve";
                case CareerAttributeKind.Reception: return "reception";
                case CareerAttributeKind.Defense: return "defense";
                case CareerAttributeKind.Block: return "block";
                case CareerAttributeKind.Movement: return "movement";
                case CareerAttributeKind.Jump: return "jump";
                case CareerAttributeKind.Stamina: return "stamina";
                default: throw UnknownEnum(nameof(value), value);
            }
        }

        private static CareerAttributeKind ParseAttribute(string value, string path)
        {
            switch (value)
            {
                case "spike": return CareerAttributeKind.Spike;
                case "serve": return CareerAttributeKind.Serve;
                case "reception": return CareerAttributeKind.Reception;
                case "defense": return CareerAttributeKind.Defense;
                case "block": return CareerAttributeKind.Block;
                case "movement": return CareerAttributeKind.Movement;
                case "jump": return CareerAttributeKind.Jump;
                case "stamina": return CareerAttributeKind.Stamina;
                default: throw UnknownTextEnum(path, value);
            }
        }
    }
}
