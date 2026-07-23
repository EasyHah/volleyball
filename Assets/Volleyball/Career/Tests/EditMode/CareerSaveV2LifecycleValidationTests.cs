using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Domain;
using Volleyball.Career.Persistence;
using Volleyball.Shared.Contracts;
using Volleyball.Shared.Contracts.V2;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerSaveV2LifecycleValidationTests
    {
        [TestCase("whitespace")]
        [TestCase("missing_padding")]
        [TestCase("url_alphabet")]
        [TestCase("empty")]
        public void PendingBase64_RejectsEveryNonCanonicalEncoding(string mutation)
        {
            var document = AwaitingDocument();
            var value = document.pendingMatch.canonicalContextUtf8Base64;
            switch (mutation)
            {
                case "whitespace": value += " "; break;
                case "missing_padding": value = value.TrimEnd('='); break;
                case "url_alphabet": value = "-" + value.Substring(1); break;
                case "empty": value = string.Empty; break;
                default: throw new AssertionException("Unknown mutation.");
            }

            document.pendingMatch.canonicalContextUtf8Base64 = value;
            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("session")]
        [TestCase("contract_version")]
        [TestCase("content_version")]
        [TestCase("ruleset_version")]
        [TestCase("career_random_version")]
        [TestCase("match_simulation_version")]
        [TestCase("match_random_version")]
        [TestCase("execution_mode")]
        [TestCase("fixture_id")]
        [TestCase("fixture_version")]
        [TestCase("match_seed")]
        [TestCase("competition")]
        [TestCase("schedule")]
        [TestCase("priority")]
        [TestCase("context_hash")]
        [TestCase("home_team")]
        [TestCase("away_team")]
        public void PendingMetadata_RejectsEveryContradictionWithCanonicalContext(
            string mutation)
        {
            var document = AwaitingDocument();
            var pending = document.pendingMatch;
            switch (mutation)
            {
                case "session": pending.sessionId = "44444444-4444-4444-4444-444444444444"; break;
                case "contract_version": pending.versions.contractVersion = 3; break;
                case "content_version": pending.versions.contentVersion = 2; break;
                case "ruleset_version": pending.versions.rulesetVersion = 2; break;
                case "career_random_version": pending.versions.careerRandomAlgorithmVersion = 2; break;
                case "match_simulation_version": pending.versions.matchSimulationVersion = 1; break;
                case "match_random_version": pending.versions.matchRandomAlgorithmVersion = 1; break;
                case "execution_mode": pending.executionMode = "direct"; break;
                case "fixture_id": pending.fixtureId = "fixture.other"; break;
                case "fixture_version": pending.fixtureVersion = 2; break;
                case "match_seed": pending.matchSeed++; break;
                case "competition": pending.competitionId = "competition.other"; break;
                case "schedule": pending.scheduleItemId = "schedule.other"; break;
                case "priority": pending.preMatchPriority = "first_contact_security"; break;
                case "context_hash": pending.contextHash = new string('0', 64); break;
                case "home_team": pending.homeTeamId = "team.other.home"; break;
                case "away_team": pending.awayTeamId = "team.other.away"; break;
                default: throw new AssertionException("Unknown mutation.");
            }

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void PendingRoster_RejectsBusinessOrderContradiction()
        {
            var document = AwaitingDocument();
            Swap(document.pendingMatch.orderedPlayerIds, 0, 1);

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void FrozenEmphasis_RejectsReorderedOriginalSlotSources()
        {
            var document = AwaitingDocument();
            document.trainingEmphases[1].direction = "spike";
            document.trainingEmphases[1].bonusBasisPoints = 500;
            var first = document.pendingMatch.frozenTrainingEmphases[0];
            var second = document.pendingMatch.frozenTrainingEmphases[1];
            document.pendingMatch.frozenTrainingEmphases = new[]
            {
                new FrozenCareerTrainingEmphasisDocumentV2
                {
                    direction = "spike",
                    sourceSlotActionIds = new[]
                    {
                        second.sourceSlotActionIds[0],
                        first.sourceSlotActionIds[0]
                    },
                    totalBonusBasisPoints = 1500
                }
            };

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void PendingProtagonist_RejectsCurrentPlayerContradiction()
        {
            var document = AwaitingDocument();
            document.pendingMatch.protagonistPlayerId =
                document.pendingMatch.orderedPlayerIds[0];

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("jersey")]
        [TestCase("ability")]
        public void PendingPlayer_RejectsCurrentCareerSnapshotContradiction(string mutation)
        {
            var document = AwaitingDocument();
            if (mutation == "jersey")
            {
                document.player.jerseyNumber++;
            }
            else
            {
                document.player.attributes.spike.abilityBasisPoints++;
            }

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void PendingFitness_RejectsContextThatIsInternallyCanonicalButContradictsFatigue()
        {
            var document = AwaitingDocument();
            var original = MatchContractV2Json.DeserializeContext(
                CareerSaveV2LifecycleTestData.ContextBytes());
            var changed = CopyContextWithFitness(original, 8700);
            var changedBytes = MatchContractV2Json.SerializeContext(changed);
            document.pendingMatch.canonicalContextUtf8Base64 =
                Convert.ToBase64String(changedBytes);
            document.pendingMatch.contextHash = changed.ContextHash;
            var receipt = document.operationReceipts[document.operationReceipts.Length - 1];
            receipt.target.contextHash = changed.ContextHash;
            receipt.outcomeSummary.contextHash = changed.ContextHash;

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ToDocument_RejectsCodeConstructedPendingWithNonCanonicalRawContext()
        {
            var source = CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot();
            var raw = source.PendingMatch.CanonicalContextUtf8.Concat(new[] { (byte)' ' }).ToArray();
            var pending = CopyPending(source.PendingMatch, raw);
            var invalid = CopySnapshot(source, pending);

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDocument(invalid),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("schedule")]
        [TestCase("context_hash")]
        [TestCase("result_hash")]
        [TestCase("session")]
        [TestCase("noncanonical_context")]
        [TestCase("noncanonical_result")]
        public void HistoryPayload_RejectsMetadataOrCanonicalByteContradiction(string mutation)
        {
            var document = SettledDocument();
            var history = document.matchHistory[0];
            switch (mutation)
            {
                case "schedule": history.scheduleItemId = "schedule.other"; break;
                case "context_hash":
                    history.contextHash = new string('1', 64);
                    document.settlementReceipts[0].contextHash = history.contextHash;
                    break;
                case "result_hash":
                    history.resultHash = new string('2', 64);
                    document.settlementReceipts[0].resultHash = history.resultHash;
                    break;
                case "session":
                    ChangeSettledSession(document, "44444444-4444-4444-4444-444444444444");
                    break;
                case "noncanonical_context":
                    history.canonicalContextUtf8Base64 = AppendSpaceAsCanonicalBase64(
                        history.canonicalContextUtf8Base64);
                    break;
                case "noncanonical_result":
                    history.canonicalResultUtf8Base64 = AppendSpaceAsCanonicalBase64(
                        history.canonicalResultUtf8Base64);
                    break;
                default: throw new AssertionException("Unknown mutation.");
            }

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("sets")]
        [TestCase("facts")]
        [TestCase("won")]
        [TestCase("priority")]
        public void HistorySummary_RejectsEveryContradictionWithCompletedResult(
            string mutation)
        {
            var document = SettledDocument();
            var history = document.matchHistory[0];
            var receipt = document.settlementReceipts[0];
            switch (mutation)
            {
                case "sets":
                    history.settlementSummary.sets[0].homePoints++;
                    receipt.settlementSummary.sets[0].homePoints++;
                    break;
                case "facts":
                    history.settlementSummary.protagonistFacts.load.activeDurationMilliseconds++;
                    receipt.settlementSummary.protagonistFacts.load.activeDurationMilliseconds++;
                    break;
                case "won":
                    history.settlementSummary.won = !history.settlementSummary.won;
                    receipt.settlementSummary.won = history.settlementSummary.won;
                    break;
                case "priority":
                    history.settlementSummary.selectedPriority = "first_contact_security";
                    receipt.settlementSummary.selectedPriority = "first_contact_security";
                    break;
                default: throw new AssertionException("Unknown mutation.");
            }

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("operation_out_of_order")]
        [TestCase("operation_duplicate")]
        [TestCase("emphasis_out_of_order")]
        [TestCase("emphasis_duplicate")]
        [TestCase("growth_out_of_order")]
        [TestCase("growth_duplicate")]
        [TestCase("sets_out_of_order")]
        [TestCase("sets_duplicate")]
        [TestCase("history_out_of_order")]
        [TestCase("history_duplicate")]
        [TestCase("settlement_out_of_order")]
        [TestCase("settlement_duplicate")]
        public void Parser_RejectsExternalArrayReorderingAndDuplicates(string mutation)
        {
            var document = mutation.StartsWith("operation", StringComparison.Ordinal) ||
                           mutation.StartsWith("emphasis", StringComparison.Ordinal)
                ? AwaitingDocument()
                : SettledDocument();
            switch (mutation)
            {
                case "operation_out_of_order":
                    Swap(document.operationReceipts, 0, 1);
                    break;
                case "operation_duplicate":
                    document.operationReceipts[1] = document.operationReceipts[0];
                    break;
                case "emphasis_out_of_order":
                    Swap(document.pendingMatch.frozenTrainingEmphases, 0, 1);
                    break;
                case "emphasis_duplicate":
                    document.pendingMatch.frozenTrainingEmphases[1] =
                        document.pendingMatch.frozenTrainingEmphases[0];
                    break;
                case "growth_out_of_order":
                    Swap(document.matchHistory[0].settlementSummary.growthChanges, 0, 1);
                    break;
                case "growth_duplicate":
                    document.matchHistory[0].settlementSummary.growthChanges[1] =
                        document.matchHistory[0].settlementSummary.growthChanges[0];
                    break;
                case "sets_out_of_order":
                    document.matchHistory[0].settlementSummary.sets = new[]
                    {
                        new CareerMatchSetScoreSummaryDocumentV2
                        {
                            setNumber = 2,
                            homePoints = 25,
                            awayPoints = 20,
                            isComplete = true
                        },
                        document.matchHistory[0].settlementSummary.sets[0]
                    };
                    break;
                case "sets_duplicate":
                    document.matchHistory[0].settlementSummary.sets = new[]
                    {
                        document.matchHistory[0].settlementSummary.sets[0],
                        document.matchHistory[0].settlementSummary.sets[0]
                    };
                    break;
                case "history_out_of_order":
                    document.matchHistory = new[]
                    {
                        document.matchHistory[0],
                        CloneHistoryForOrder(document.matchHistory[0], 9)
                    };
                    break;
                case "history_duplicate":
                    document.matchHistory = new[]
                    {
                        document.matchHistory[0],
                        document.matchHistory[0]
                    };
                    break;
                case "settlement_out_of_order":
                    document.settlementReceipts = new[]
                    {
                        document.settlementReceipts[0],
                        CloneReceiptForOrder(document.settlementReceipts[0], 9)
                    };
                    break;
                case "settlement_duplicate":
                    document.settlementReceipts = new[]
                    {
                        document.settlementReceipts[0],
                        document.settlementReceipts[0]
                    };
                    break;
                default: throw new AssertionException("Unknown mutation.");
            }

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("match_session")]
        [TestCase("context_hash")]
        public void TryoutOutcome_RejectsPendingMatchOnlyFields(string mutation)
        {
            var document = AwaitingDocument();
            var tryout = document.operationReceipts.First(
                receipt => receipt.operationKind == "confirm_tryout_stage");
            if (mutation == "match_session")
            {
                tryout.outcomeSummary.matchSessionId =
                    "44444444-4444-4444-4444-444444444444";
            }
            else
            {
                tryout.outcomeSummary.contextHash = new string('4', 64);
            }

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [TestCase("career_created")]
        [TestCase("week_plan_confirmed")]
        [TestCase("slot_completed")]
        [TestCase("event_choice_applied")]
        public void OtherNonPendingOutcomes_RejectPendingMatchOnlyFields(
            string outcomeKind)
        {
            foreach (var field in new[] { "match_session", "context_hash" })
            {
                var document = AwaitingDocument();
                var receipt = document.operationReceipts.First(
                    item => item.outcomeKind == outcomeKind);
                if (field == "match_session")
                {
                    receipt.outcomeSummary.matchSessionId =
                        "44444444-4444-4444-4444-444444444444";
                }
                else
                {
                    receipt.outcomeSummary.contextHash = new string('4', 64);
                }

                Assert.That(
                    () => CareerSaveSnapshotMapper.ToDomain(document),
                    Throws.InstanceOf<ArgumentException>(),
                    outcomeKind + " accepted " + field);
            }
        }

        [TestCase("create_career")]
        [TestCase("confirm_tryout_stage")]
        [TestCase("confirm_week_plan")]
        [TestCase("execute_week_action")]
        [TestCase("resolve_event_choice")]
        public void NonPendingTargets_RejectEveryPendingMatchOnlyField(
            string operationKind)
        {
            foreach (var field in new[] { "match_session", "schedule", "context_hash" })
            {
                var document = AwaitingDocument();
                var receipt = document.operationReceipts.First(
                    item => item.operationKind == operationKind);
                switch (field)
                {
                    case "match_session":
                        receipt.target.matchSessionId =
                            "44444444-4444-4444-4444-444444444444";
                        break;
                    case "schedule":
                        receipt.target.scheduleItemId = "schedule.other";
                        break;
                    case "context_hash":
                        receipt.target.contextHash = new string('4', 64);
                        break;
                }

                Assert.That(
                    () => CareerSaveSnapshotMapper.ToDomain(document),
                    Throws.InstanceOf<ArgumentException>(),
                    operationKind + " accepted " + field);
            }
        }

        [TestCase("target_session")]
        [TestCase("target_schedule")]
        [TestCase("target_hash")]
        [TestCase("outcome_session")]
        [TestCase("outcome_hash")]
        public void PendingOperation_RejectsMissingRequiredV2Fields(string field)
        {
            var document = AwaitingDocument();
            var receipt = document.operationReceipts.First(
                item => item.operationKind == "create_pending_match");
            switch (field)
            {
                case "target_session": receipt.target.matchSessionId = null; break;
                case "target_schedule": receipt.target.scheduleItemId = null; break;
                case "target_hash": receipt.target.contextHash = null; break;
                case "outcome_session": receipt.outcomeSummary.matchSessionId = null; break;
                case "outcome_hash": receipt.outcomeSummary.contextHash = null; break;
            }

            Assert.That(
                () => CareerSaveSnapshotMapper.ToDomain(document),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void PendingOperation_V2OnlyFieldsRoundTripExactly()
        {
            var document = AwaitingDocument();
            var expected = document.operationReceipts.First(
                item => item.operationKind == "create_pending_match");

            var roundTrip = CareerSaveSnapshotMapper.ToDocument(
                CareerSaveSnapshotMapper.ToDomain(document));
            var actual = roundTrip.operationReceipts.First(
                item => item.operationKind == "create_pending_match");

            Assert.That(actual.target.matchSessionId, Is.EqualTo(expected.target.matchSessionId));
            Assert.That(actual.target.scheduleItemId, Is.EqualTo(expected.target.scheduleItemId));
            Assert.That(actual.target.contextHash, Is.EqualTo(expected.target.contextHash));
            Assert.That(
                actual.outcomeSummary.matchSessionId,
                Is.EqualTo(expected.outcomeSummary.matchSessionId));
            Assert.That(
                actual.outcomeSummary.contextHash,
                Is.EqualTo(expected.outcomeSummary.contextHash));
        }

        private static CareerSaveDocumentV2 AwaitingDocument()
        {
            return CareerSaveSnapshotMapper.ToDocument(
                CareerSaveV2LifecycleTestData.AwaitingMatchSnapshot());
        }

        private static CareerSaveDocumentV2 SettledDocument()
        {
            return CareerSaveSnapshotMapper.ToDocument(
                CareerSaveV2LifecycleTestData.SettledSnapshot());
        }

        private static MatchContextV2 CopyContextWithFitness(
            MatchContextV2 context,
            int fitness)
        {
            var teams = context.Teams.Select(team => new MatchTeamSnapshotV2(
                team.TeamId,
                team.Side,
                team.Players.Select(player => new MatchPlayerSnapshotV2(
                    player.PlayerId,
                    player.JerseyNumber,
                    player.Position,
                    player.RotationSlot,
                    player.PlayerId.Value == "player.career.protagonist"
                        ? fitness
                        : player.FitnessBasisPoints,
                    player.Abilities)).ToArray())).ToArray();
            return MatchContextV2.Create(
                context.Versions,
                context.SessionId,
                context.ExecutionMode,
                context.FixtureId,
                context.FixtureVersion,
                context.MatchSeed,
                context.CompetitionId,
                context.ScheduleItemId,
                context.ImportanceBasisPoints,
                context.Format,
                context.PreMatchPriority,
                teams);
        }

        private static PendingCareerMatch CopyPending(
            PendingCareerMatch source,
            byte[] rawContext)
        {
            return new PendingCareerMatch(
                source.SessionId,
                source.CreationOperationId,
                source.CreatedLineageId,
                source.CreatedRevision,
                source.Versions,
                source.ExecutionMode,
                source.FixtureId,
                source.FixtureVersion,
                source.MatchSeed,
                source.CompetitionId,
                source.ScheduleItemId,
                source.SourceWeekPlanId,
                source.SourceSlotActionId,
                source.SourceActionOccurrenceId,
                source.PreMatchPriority,
                source.ContextDigest,
                rawContext,
                source.HomeTeamId,
                source.AwayTeamId,
                source.OrderedPlayerIds,
                source.ProtagonistPlayerId,
                source.FrozenTrainingEmphases);
        }

        private static CareerSaveSnapshot CopySnapshot(
            CareerSaveSnapshot source,
            PendingCareerMatch pending)
        {
            return new CareerSaveSnapshot(
                source.Versions,
                source.Identity,
                source.CareerSeed,
                source.CareerName,
                source.PlayerDraft,
                source.Onboarding,
                source.Progression,
                source.TrainingEmphases,
                source.Player,
                source.TeamId,
                source.PotentialGrade,
                source.Fatigue,
                source.Mindset,
                source.CoachTrust,
                source.OperationReceipts,
                pending,
                source.MatchHistory,
                source.SettlementReceipts);
        }

        private static string AppendSpaceAsCanonicalBase64(string base64)
        {
            return Convert.ToBase64String(
                Convert.FromBase64String(base64).Concat(new[] { (byte)' ' }).ToArray());
        }

        private static void ChangeSettledSession(CareerSaveDocumentV2 document, string session)
        {
            document.matchHistory[0].sessionId = session;
            document.settlementReceipts[0].sessionId = session;
            var creation = document.operationReceipts[document.operationReceipts.Length - 1];
            creation.target.matchSessionId = session;
            creation.outcomeSummary.matchSessionId = session;
        }

        private static CareerMatchHistoryEntryDocumentV2 CloneHistoryForOrder(
            CareerMatchHistoryEntryDocumentV2 source,
            long revision)
        {
            return new CareerMatchHistoryEntryDocumentV2
            {
                sessionId = "44444444-4444-4444-4444-444444444444",
                scheduleItemId = source.scheduleItemId,
                sourceWeekPlanId = source.sourceWeekPlanId,
                sourceSlotActionId = source.sourceSlotActionId,
                contextHash = source.contextHash,
                resultHash = source.resultHash,
                canonicalContextUtf8Base64 = source.canonicalContextUtf8Base64,
                canonicalResultUtf8Base64 = source.canonicalResultUtf8Base64,
                appliedLineageId = source.appliedLineageId,
                appliedRevision = revision,
                settledAtUtcMs = source.settledAtUtcMs,
                settlementSummary = source.settlementSummary
            };
        }

        private static CareerSettlementReceiptDocumentV2 CloneReceiptForOrder(
            CareerSettlementReceiptDocumentV2 source,
            long revision)
        {
            return new CareerSettlementReceiptDocumentV2
            {
                sessionId = "44444444-4444-4444-4444-444444444444",
                contextHash = source.contextHash,
                resultHash = source.resultHash,
                appliedLineageId = source.appliedLineageId,
                appliedRevision = revision,
                settledAtUtcMs = source.settledAtUtcMs,
                settlementSummary = source.settlementSummary
            };
        }

        private static void Swap<T>(T[] values, int left, int right)
        {
            var value = values[left];
            values[left] = values[right];
            values[right] = value;
        }
    }
}
