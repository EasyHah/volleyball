using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchLifecycleDomainTests
    {
        [Test]
        public void WeekTwoPlanning_AllowsOnlyTheCompletelyEmptyUnconfirmedContainer()
        {
            var empty = new CareerWeekPlanState(
                new WeekPlanId(Guid.NewGuid()),
                1,
                2,
                new CareerWeekActionState[] { null, null, null },
                false);

            var planning = CareerProgressionState.Planning(empty);

            Assert.That(planning.WeekPlan.Slots, Is.All.Null);
            Assert.That(planning.WeekPlan, Is.Not.SameAs(empty));
            Assert.That(
                () => CareerProgressionState.Planned(empty, 1),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerWeekPlanState(
                    empty.PlanId,
                    1,
                    2,
                    new[]
                    {
                        Action(CareerWeekActionKind.Rest, "week_action.rest.standard"),
                        null,
                        null
                    },
                    false),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerWeekPlanState(
                    empty.PlanId,
                    1,
                    2,
                    new CareerWeekActionState[] { null, null, null },
                    true),
                Throws.ArgumentException);
        }

        [Test]
        public void AwaitingMatch_RequiresFirstWeekConfirmedPlanAndPreservesSessionOnCopy()
        {
            var plan = ConfirmedFirstWeekPlan();
            var sessionId = Guid.NewGuid();

            var state = CareerProgressionState.AwaitingMatch(plan, sessionId);

            Assert.That(state.Kind, Is.EqualTo(CareerProgressionKind.AwaitingMatch));
            Assert.That(state.MatchSessionId, Is.EqualTo(sessionId));
            Assert.That(state.NextSlotNumber, Is.Zero);
            Assert.That(state.PendingEvent, Is.Null);
            Assert.That(state.WeekPlan, Is.Not.SameAs(plan));
            Assert.That(
                () => CareerProgressionState.AwaitingMatch(plan, Guid.Empty),
                Throws.ArgumentException);
            Assert.That(
                () => CareerProgressionState.AwaitingMatch(
                    new CareerWeekPlanState(
                        plan.PlanId,
                        1,
                        2,
                        plan.Slots,
                        true),
                    sessionId),
                Throws.ArgumentException);
        }

        [Test]
        public void Progression_RejectsMatchSessionAndNextSlotFieldsOutsideTheirExactShape()
        {
            var plan = ConfirmedFirstWeekPlan();
            var sessionId = Guid.NewGuid();

            Assert.That(
                () => new CareerProgressionState(
                    CareerProgressionKind.AwaitingMatch,
                    CareerPhase.University,
                    0,
                    plan,
                    1,
                    null,
                    sessionId),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerProgressionState(
                    CareerProgressionKind.Planned,
                    CareerPhase.University,
                    0,
                    plan,
                    3,
                    null,
                    sessionId),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerProgressionState(
                    CareerProgressionKind.AwaitingMatch,
                    CareerPhase.University,
                    0,
                    plan,
                    0,
                    null,
                    null),
                Throws.ArgumentException);
        }

        [Test]
        public void PendingMatch_FreezesCanonicalBytesRosterAndOrderedEmphases()
        {
            var canonical = new byte[] { (byte)'{', (byte)'}' };
            var players = Players();
            var firstSource = new SlotActionId(Guid.NewGuid());
            var secondSource = new SlotActionId(Guid.NewGuid());
            var pending = Pending(
                canonical,
                players,
                new[]
                {
                    new FrozenCareerTrainingEmphasis(
                        CareerTrainingDirection.Jump,
                        new[] { firstSource, secondSource },
                        1500),
                    new FrozenCareerTrainingEmphasis(
                        CareerTrainingDirection.Spike,
                        new[] { new SlotActionId(Guid.NewGuid()) },
                        1000)
                });

            canonical[0] = (byte)'x';
            players[0] = new PlayerId("mutated-player");
            var returned = pending.CanonicalContextUtf8;
            returned[0] = (byte)'y';

            Assert.That(pending.CanonicalContextUtf8, Is.EqualTo(new byte[] { (byte)'{', (byte)'}' }));
            Assert.That(pending.OrderedPlayerIds[0].Value, Is.EqualTo("player-01"));
            Assert.That(pending.FrozenTrainingEmphases[0].Direction, Is.EqualTo(CareerTrainingDirection.Spike));
            Assert.That(pending.FrozenTrainingEmphases[1].Direction, Is.EqualTo(CareerTrainingDirection.Jump));
            Assert.That(pending.FrozenTrainingEmphases[1].SourceSlotActionIds,
                Is.EqualTo(new[] { firstSource, secondSource }));
        }

        [Test]
        public void PendingMatch_RejectsModeRosterIdentityAndEmphasisContradictions()
        {
            var players = Players();
            Assert.That(
                () => new CareerMatchLifecycleVersions(1, 1, 1, 1, null, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new CareerMatchLifecycleVersions(2, 1, 1, 1, 1, null),
                Throws.ArgumentException);
            Assert.That(
                () => Pending(
                    new byte[] { (byte)'{', (byte)'}' },
                    players,
                    Array.Empty<FrozenCareerTrainingEmphasis>(),
                    mode: CareerMatchLifecycleExecutionMode.Direct,
                    fixtureId: "fixture.career.u1w1.6v6",
                    fixtureVersion: 1),
                Throws.ArgumentException);
            Assert.That(
                () => Pending(new byte[0], players, Array.Empty<FrozenCareerTrainingEmphasis>()),
                Throws.ArgumentException);
            Assert.That(
                () => Pending(new byte[] { 0xff }, players, Array.Empty<FrozenCareerTrainingEmphasis>()),
                Throws.ArgumentException);

            players[11] = players[0];
            Assert.That(
                () => Pending(new byte[] { (byte)'{', (byte)'}' }, players, Array.Empty<FrozenCareerTrainingEmphasis>()),
                Throws.ArgumentException);
            players = Players();
            Assert.That(
                () => Pending(
                    new byte[] { (byte)'{', (byte)'}' },
                    players,
                    Array.Empty<FrozenCareerTrainingEmphasis>(),
                    protagonist: new PlayerId("not-in-roster")),
                Throws.ArgumentException);
            Assert.That(
                () => new FrozenCareerTrainingEmphasis(
                    CareerTrainingDirection.Spike,
                    new[] { new SlotActionId(Guid.NewGuid()), new SlotActionId(Guid.NewGuid()) },
                    1000),
                Throws.ArgumentException);
        }

        [Test]
        public void PendingMatch_AcceptsAllThreeExactVersionModeShapesAndFullSeedRange()
        {
            var fixture = PendingInput.Valid();
            fixture.MatchSeed = 0u;
            Assert.That(() => fixture.Create(), Throws.Nothing);

            var direct = PendingInput.Valid();
            direct.Mode = CareerMatchLifecycleExecutionMode.Direct;
            direct.FixtureId = null;
            direct.FixtureVersion = null;
            direct.MatchSeed = uint.MaxValue;
            Assert.That(() => direct.Create(), Throws.Nothing);

            var quick = PendingInput.Valid();
            quick.Mode = CareerMatchLifecycleExecutionMode.QuickSimulation;
            quick.FixtureId = null;
            quick.FixtureVersion = null;
            quick.Versions = new CareerMatchLifecycleVersions(2, 1, 1, 1, 1, 1);
            Assert.That(() => quick.Create(), Throws.Nothing);
        }

        [TestCase("session")]
        [TestCase("operation")]
        [TestCase("lineage")]
        [TestCase("revision-zero")]
        [TestCase("revision-unsafe")]
        [TestCase("mode-invalid")]
        [TestCase("fixture-id")]
        [TestCase("fixture-version")]
        [TestCase("fixture-algorithms")]
        [TestCase("direct-fixture")]
        [TestCase("direct-algorithms")]
        [TestCase("quick-fixture")]
        [TestCase("quick-algorithms")]
        [TestCase("competition")]
        [TestCase("schedule")]
        [TestCase("plan")]
        [TestCase("slot")]
        [TestCase("occurrence")]
        [TestCase("priority")]
        [TestCase("digest")]
        [TestCase("bytes-null")]
        [TestCase("bytes-empty")]
        [TestCase("bytes-invalid")]
        [TestCase("home-team")]
        [TestCase("away-team")]
        [TestCase("same-team")]
        [TestCase("roster-null")]
        [TestCase("roster-eleven")]
        [TestCase("roster-thirteen")]
        [TestCase("roster-duplicate")]
        [TestCase("roster-id")]
        [TestCase("protagonist")]
        [TestCase("emphases-null")]
        [TestCase("emphases-duplicate")]
        public void PendingMatch_RejectsEveryBoundStructuralContradiction(string contradiction)
        {
            var input = PendingInput.Valid();
            MutatePending(input, contradiction);

            Assert.That(() => input.Create(), Throws.Exception, contradiction);
        }

        [TestCase("contract")]
        [TestCase("content")]
        [TestCase("rules")]
        [TestCase("career-random")]
        [TestCase("simulation")]
        [TestCase("match-random")]
        [TestCase("unpaired-simulation")]
        [TestCase("unpaired-random")]
        public void MatchLifecycleVersions_RejectEveryInvalidAxis(string contradiction)
        {
            Assert.That(
                () => InvalidVersions(contradiction),
                Throws.Exception,
                contradiction);
        }

        [TestCase("null-sources")]
        [TestCase("zero-sources")]
        [TestCase("three-sources")]
        [TestCase("duplicate-sources")]
        [TestCase("one-wrong-total")]
        [TestCase("two-wrong-total")]
        [TestCase("invalid-direction")]
        public void FrozenEmphasis_RejectsEverySourceTotalContradiction(string contradiction)
        {
            Assert.That(
                () => InvalidFrozenEmphasis(contradiction),
                Throws.Exception,
                contradiction);
        }

        [TestCase("set-number")]
        [TestCase("spike-subset")]
        [TestCase("serve-subset")]
        [TestCase("reception-sum")]
        [TestCase("defense-subset")]
        [TestCase("block-subset")]
        [TestCase("load-jumps")]
        [TestCase("load-basis-points")]
        [TestCase("stability-actions")]
        [TestCase("stability-streak")]
        [TestCase("status-range")]
        [TestCase("weekend-nonzero")]
        public void SettlementEvidence_RejectsEveryBoundFactAndChangeContradiction(
            string contradiction)
        {
            Assert.That(
                () => InvalidSettlementEvidence(contradiction),
                Throws.Exception,
                contradiction);
        }

        [TestCase("session")]
        [TestCase("lineage")]
        [TestCase("revision")]
        [TestCase("versions")]
        [TestCase("plan")]
        [TestCase("slot")]
        [TestCase("occurrence")]
        [TestCase("schedule")]
        [TestCase("context")]
        [TestCase("creation-operation")]
        [TestCase("emphasis")]
        public void Snapshot_RejectsEveryPendingCorrelationContradiction(string contradiction)
        {
            var valid = AwaitingSnapshot(BeforePending());
            var pending = ContradictPending(valid.PendingMatch, contradiction);

            Assert.That(
                () => FullSnapshot(
                    valid,
                    valid.Progression,
                    pending,
                    valid.MatchHistory,
                    valid.SettlementReceipts),
                Throws.ArgumentException,
                contradiction);
        }

        [Test]
        public void SettlementSummary_IsTypedValueEqualAndRejectsInconsistentChanges()
        {
            var first = Summary();
            var second = Summary();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.GrowthChanges, Has.Count.EqualTo(8));
            Assert.That(first.BeforeAttributes.Spike.GrowthExperience, Is.EqualTo(10));
            Assert.That(first.AppliedGrowthExperienceDelta.Spike, Is.EqualTo(5));
            Assert.That(first.AfterAttributes.Stamina.GrowthExperience, Is.EqualTo(15));
            Assert.That(first.WeekendFatigueChange.ActualDelta, Is.Zero);
            Assert.That(first.ProtagonistFacts.Reception.Attempts, Is.EqualTo(5));
            Assert.That(
                () => new CareerReasonedIntegerChange("match.fatigue", 50, 8, 7, 58),
                Throws.ArgumentException);
            Assert.That(
                () => new CareerAttributeGrowthChange(
                    CareerAttributeKind.Spike,
                    "match.growth.spike",
                    new CareerAttributeProgress(5000, 10),
                    5,
                    4,
                    new CareerAttributeProgress(5000, 15)),
                Throws.ArgumentException);
        }

        [Test]
        public void HistoryAndSettlementReceipt_DefensivelyCopyRawEvidenceAndEnforceSafeMetadata()
        {
            var context = new byte[] { (byte)'{', (byte)'}' };
            var result = new byte[] { (byte)'[', (byte)']' };
            var summary = Summary();
            var sessionId = Guid.NewGuid();
            var history = new CareerMatchHistoryEntry(
                sessionId,
                "schedule.u1w1.match.01",
                new WeekPlanId(Guid.NewGuid()),
                new SlotActionId(Guid.NewGuid()),
                new Sha256Digest(new string('a', 64)),
                new Sha256Digest(new string('b', 64)),
                context,
                result,
                new LineageId(Guid.NewGuid()),
                10,
                1234,
                summary);
            var receipt = new CareerSettlementReceipt(
                sessionId,
                history.ContextDigest,
                history.ResultDigest,
                new LineageId(Guid.NewGuid()),
                10,
                history.SettledAtUtcMs,
                Summary());

            context[0] = (byte)'x';
            result[0] = (byte)'y';
            var returnedContext = history.CanonicalContextUtf8;
            returnedContext[0] = (byte)'z';

            Assert.That(history.CanonicalContextUtf8[0], Is.EqualTo((byte)'{'));
            Assert.That(history.CanonicalResultUtf8[0], Is.EqualTo((byte)'['));
            Assert.That(receipt.SettlementSummary, Is.EqualTo(history.SettlementSummary));
            Assert.That(
                () => new CareerSettlementReceipt(
                    sessionId,
                    history.ContextDigest,
                    history.ResultDigest,
                    receipt.AppliedLineageId,
                    0,
                    0,
                    summary),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void PendingCreationReceipt_IsStronglyTypedAndKeepsSettlementRevisionsIndependent()
        {
            var sessionId = Guid.NewGuid();
            var contextDigest = new Sha256Digest(new string('a', 64));
            var planId = new WeekPlanId(Guid.NewGuid());
            var slotId = new SlotActionId(Guid.NewGuid());
            var occurrenceId = new OccurrenceId(Guid.NewGuid());
            var target = OperationReceiptTarget.ForPendingMatch(
                planId,
                slotId,
                occurrenceId,
                sessionId,
                "schedule.u1w1.match.01",
                contextDigest);
            var outcome = OperationOutcomeSummary.ForPendingMatchCreated(sessionId, contextDigest);
            var lineage = new LineageId(Guid.NewGuid());
            var receipt = new OperationReceipt(
                new OperationId(Guid.NewGuid()),
                OperationKind.CreatePendingMatch,
                target,
                new Sha256Digest(new string('b', 64)),
                lineage,
                9,
                100,
                OperationOutcomeKind.PendingMatchCreated,
                outcome);

            Assert.That(receipt.Target.MatchSessionId, Is.EqualTo(sessionId));
            Assert.That(receipt.Target.WeekPlanId, Is.EqualTo(planId));
            Assert.That(receipt.Target.SlotActionId, Is.EqualTo(slotId));
            Assert.That(receipt.Target.ActionOccurrenceId, Is.EqualTo(occurrenceId));
            Assert.That(receipt.Target.ScheduleItemId, Is.EqualTo("schedule.u1w1.match.01"));
            Assert.That(receipt.Target.ContextDigest, Is.EqualTo(contextDigest));
            Assert.That(receipt.OutcomeSummary.MatchSessionId, Is.EqualTo(sessionId));
            Assert.That(receipt.OutcomeSummary.ContextDigest, Is.EqualTo(contextDigest));

            var settlement = new CareerSettlementReceipt(
                sessionId,
                contextDigest,
                new Sha256Digest(new string('c', 64)),
                lineage,
                9,
                101,
                Summary());
            Assert.That(settlement.AppliedRevision, Is.EqualTo(receipt.AppliedRevision));

            var legacyTarget = OperationReceiptTarget.ForWeekPlanConfirmation(planId);
            var legacyOutcome = OperationOutcomeSummary.ForWeekPlanConfirmed();
            Assert.That(legacyTarget.MatchSessionId, Is.Null);
            Assert.That(legacyTarget.ScheduleItemId, Is.Null);
            Assert.That(legacyTarget.ContextDigest, Is.Null);
            Assert.That(legacyOutcome.MatchSessionId, Is.Null);
            Assert.That(legacyOutcome.ContextDigest, Is.Null);
        }

        [Test]
        public void Snapshot_RequiresCorrelatedAwaitingMatchPendingLedgerAndCreationReceipt()
        {
            var before = BeforePending();
            var valid = AwaitingSnapshot(before);

            Assert.That(valid.PendingMatch.SessionId, Is.EqualTo(valid.Progression.MatchSessionId));
            Assert.That(valid.MatchHistory, Is.Empty);
            Assert.That(valid.SettlementReceipts, Is.Empty);
            Assert.That(
                () => FullSnapshot(
                    valid,
                    CareerProgressionState.AwaitingMatch(valid.Progression.WeekPlan, Guid.NewGuid()),
                    valid.PendingMatch,
                    valid.MatchHistory,
                    valid.SettlementReceipts),
                Throws.ArgumentException);
            Assert.That(
                () => FullSnapshot(
                    valid,
                    CareerProgressionState.Planned(valid.Progression.WeekPlan, 3),
                    valid.PendingMatch,
                    valid.MatchHistory,
                    valid.SettlementReceipts),
                Throws.ArgumentException);
        }

        [Test]
        public void AwaitingSnapshot_RejectsPendingProtagonistThatIsNotTheCareerPlayer()
        {
            var awaiting = AwaitingSnapshot(BeforePending());
            var pending = awaiting.PendingMatch;
            var mismatched = PendingWithCareerIdentity(
                pending,
                pending.HomeTeamId,
                pending.AwayTeamId,
                pending.OrderedPlayerIds,
                pending.OrderedPlayerIds[2]);

            Assert.That(
                () => FullSnapshot(
                    awaiting,
                    awaiting.Progression,
                    mismatched,
                    awaiting.MatchHistory,
                    awaiting.SettlementReceipts),
                Throws.ArgumentException);
        }

        [Test]
        public void AwaitingSnapshot_RejectsPendingWhenCareerTeamIsNotPlaying()
        {
            var awaiting = AwaitingSnapshot(BeforePending());
            var pending = awaiting.PendingMatch;
            var mismatched = PendingWithCareerIdentity(
                pending,
                new TeamId("university-green"),
                new TeamId("university-red"),
                pending.OrderedPlayerIds,
                pending.ProtagonistPlayerId);

            Assert.That(
                () => FullSnapshot(
                    awaiting,
                    awaiting.Progression,
                    mismatched,
                    awaiting.MatchHistory,
                    awaiting.SettlementReceipts),
                Throws.ArgumentException);
        }

        [Test]
        public void AwaitingSnapshot_RejectsCareerPlayerOnTheOpponentRosterSegment()
        {
            var awaiting = AwaitingSnapshot(BeforePending());
            var pending = awaiting.PendingMatch;
            var roster = pending.OrderedPlayerIds.ToArray();
            var homeIndex = Array.IndexOf(roster, awaiting.Player.PlayerId);
            var opponent = roster[6];
            roster[6] = roster[homeIndex];
            roster[homeIndex] = opponent;
            var mismatched = PendingWithCareerIdentity(
                pending,
                pending.HomeTeamId,
                pending.AwayTeamId,
                roster,
                awaiting.Player.PlayerId);

            Assert.That(
                () => FullSnapshot(
                    awaiting,
                    awaiting.Progression,
                    mismatched,
                    awaiting.MatchHistory,
                    awaiting.SettlementReceipts),
                Throws.ArgumentException);
        }

        [Test]
        public void AwaitingSnapshot_RejectsStalePendingRevisionAfterTheImmediateRestoreWindow()
        {
            var awaiting = AwaitingSnapshot(BeforePending());
            var restoredFrom = new CareerVersionToken(
                new LineageId(Guid.NewGuid()),
                awaiting.Identity.Revision - 1,
                new Sha256Digest(new string('d', 64)));
            var laterIdentity = new CareerSaveIdentity(
                awaiting.Identity.ProfileId,
                awaiting.Identity.SaveId,
                awaiting.Identity.LineageId,
                awaiting.Identity.Revision + 1,
                awaiting.Identity.CreatedAtUtcMs,
                awaiting.Identity.UpdatedAtUtcMs + 1,
                awaiting.Identity.SnapshotHash,
                restoredFrom);

            Assert.That(
                () => new CareerSaveSnapshot(
                    awaiting.Versions,
                    laterIdentity,
                    awaiting.CareerSeed,
                    awaiting.CareerName,
                    awaiting.PlayerDraft,
                    awaiting.Onboarding,
                    awaiting.Progression,
                    awaiting.TrainingEmphases,
                    awaiting.Player,
                    awaiting.TeamId,
                    awaiting.PotentialGrade,
                    awaiting.Fatigue,
                    awaiting.Mindset,
                    awaiting.CoachTrust,
                    awaiting.OperationReceipts,
                    awaiting.PendingMatch,
                    awaiting.MatchHistory,
                    awaiting.SettlementReceipts),
                Throws.ArgumentException);
        }

        [Test]
        public void Snapshot_MatchesHistoryAndSettlementReceiptsOneToOneWithoutOperationRevisionCollision()
        {
            var awaiting = AwaitingSnapshot(BeforePending());
            var settled = SettledWeekTwoSnapshot(awaiting);

            Assert.That(settled.PendingMatch, Is.Null);
            Assert.That(settled.Progression.Kind, Is.EqualTo(CareerProgressionKind.Planning));
            Assert.That(settled.Progression.WeekPlan.Week, Is.EqualTo(2));
            Assert.That(settled.Progression.WeekPlan.Slots, Is.All.Null);
            Assert.That(settled.TrainingEmphases.Contributions, Is.Empty);
            Assert.That(settled.MatchHistory, Has.Count.EqualTo(1));
            Assert.That(settled.SettlementReceipts, Has.Count.EqualTo(1));
            Assert.That(
                settled.OperationReceipts.Any(item => item.AppliedRevision == 10),
                Is.False,
                "Settlement receipts have their own revision namespace.");

            var duplicateReceipts = new[]
            {
                settled.SettlementReceipts[0],
                settled.SettlementReceipts[0]
            };
            Assert.That(
                () => FullSnapshot(
                    settled,
                    settled.Progression,
                    null,
                    settled.MatchHistory,
                    duplicateReceipts),
                Throws.ArgumentException);

            var wrongReceipt = new CareerSettlementReceipt(
                settled.SettlementReceipts[0].SessionId,
                settled.SettlementReceipts[0].ContextDigest,
                new Sha256Digest(new string('f', 64)),
                settled.Identity.LineageId,
                settled.Identity.Revision,
                settled.SettlementReceipts[0].SettledAtUtcMs,
                settled.SettlementReceipts[0].SettlementSummary);
            Assert.That(
                () => FullSnapshot(
                    settled,
                    settled.Progression,
                    null,
                    settled.MatchHistory,
                    new[] { wrongReceipt }),
                Throws.ArgumentException);
        }

        [Test]
        public void SettledSnapshot_RejectsPlayerAttributesThatDoNotMatchSettlementAfterValues()
        {
            var settled = SettledWeekTwoSnapshot(AwaitingSnapshot(BeforePending()));
            var attributes = settled.Player.Attributes;
            var wrongAttributes = new CareerPlayerAttributes(
                new CareerAttributeProgress(
                    attributes.Spike.AbilityBasisPoints,
                    attributes.Spike.GrowthExperience + 1),
                attributes.Serve,
                attributes.Reception,
                attributes.Defense,
                attributes.Block,
                attributes.Movement,
                attributes.Jump,
                attributes.Stamina);
            var wrongPlayer = new CareerPlayerRecord(
                settled.Player.PlayerId,
                settled.Player.DisplayName,
                settled.Player.JerseyNumber,
                wrongAttributes);

            Assert.That(
                () => SnapshotWithCareerState(
                    settled,
                    wrongPlayer,
                    settled.TeamId,
                    settled.Fatigue,
                    settled.Mindset,
                    settled.CoachTrust),
                Throws.ArgumentException);
        }

        [Test]
        public void SettledSnapshot_RejectsFatigueThatDoesNotMatchSettlementAfterValue()
        {
            var settled = SettledWeekTwoSnapshot(AwaitingSnapshot(BeforePending()));

            Assert.That(
                () => SnapshotWithCareerState(
                    settled,
                    settled.Player,
                    settled.TeamId,
                    DifferentStatus(settled.Fatigue.Value),
                    settled.Mindset,
                    settled.CoachTrust),
                Throws.ArgumentException);
        }

        [Test]
        public void SettledSnapshot_RejectsMindsetThatDoesNotMatchSettlementAfterValue()
        {
            var settled = SettledWeekTwoSnapshot(AwaitingSnapshot(BeforePending()));

            Assert.That(
                () => SnapshotWithCareerState(
                    settled,
                    settled.Player,
                    settled.TeamId,
                    settled.Fatigue,
                    DifferentStatus(settled.Mindset.Value),
                    settled.CoachTrust),
                Throws.ArgumentException);
        }

        [Test]
        public void SettledSnapshot_RejectsCoachTrustThatDoesNotMatchSettlementAfterValue()
        {
            var settled = SettledWeekTwoSnapshot(AwaitingSnapshot(BeforePending()));

            Assert.That(
                () => SnapshotWithCareerState(
                    settled,
                    settled.Player,
                    settled.TeamId,
                    settled.Fatigue,
                    settled.Mindset,
                    DifferentStatus(settled.CoachTrust.Value)),
                Throws.ArgumentException);
        }

        [Test]
        public void Snapshot_OldConstructorStartsEmptyAndCopierPreservesSettledLifecycleEvidence()
        {
            var old = BeforePending();
            Assert.That(old.PendingMatch, Is.Null);
            Assert.That(old.MatchHistory, Is.Empty);
            Assert.That(old.SettlementReceipts, Is.Empty);

            var settled = SettledWeekTwoSnapshot(AwaitingSnapshot(old));
            var advanced = CareerWeekSnapshotFactory.Advance(
                settled,
                settled.Identity.UpdatedAtUtcMs + 1,
                settled.Progression,
                settled.TrainingEmphases,
                settled.Player.Attributes,
                settled.Fatigue.Value,
                settled.Mindset.Value,
                settled.CoachTrust.Value,
                settled.OperationReceipts);

            Assert.That(advanced.MatchHistory, Has.Count.EqualTo(1));
            Assert.That(advanced.SettlementReceipts, Has.Count.EqualTo(1));
            Assert.That(advanced.MatchHistory[0].CanonicalResultUtf8,
                Is.EqualTo(settled.MatchHistory[0].CanonicalResultUtf8));
        }

        [Test]
        public void SettledSnapshot_RequiresTheOriginalPendingCreationReceiptForEveryHistoryEntry()
        {
            var settled = SettledWeekTwoSnapshot(AwaitingSnapshot(BeforePending()));
            var withoutCreation = settled.OperationReceipts
                .Where(item => item.OperationKind != OperationKind.CreatePendingMatch)
                .ToArray();

            Assert.That(
                () => FullSnapshot(
                    settled,
                    settled.Progression,
                    null,
                    settled.MatchHistory,
                    settled.SettlementReceipts,
                    withoutCreation),
                Throws.ArgumentException);
        }

        [Test]
        public void SettledSnapshot_RejectsPendingCreationThatDidNotFollowTheFirstWeekChain()
        {
            var settled = SettledWeekTwoSnapshot(AwaitingSnapshot(BeforePending()));
            var original = settled.OperationReceipts.Single(
                item => item.OperationKind == OperationKind.CreatePendingMatch);
            var broken = settled.OperationReceipts
                .Where(item => item.OperationKind != OperationKind.CreatePendingMatch &&
                               item.OperationKind != OperationKind.ResolveEventChoice)
                .Concat(new[]
                {
                    new OperationReceipt(
                        original.OperationId,
                        original.OperationKind,
                        original.Target,
                        original.InputFingerprint,
                        original.AppliedLineageId,
                        7,
                        original.CompletedAtUtcMs,
                        original.OutcomeKind,
                        original.OutcomeSummary)
                })
                .ToArray();

            Assert.That(
                () => FullSnapshot(
                    settled,
                    settled.Progression,
                    null,
                    settled.MatchHistory,
                    settled.SettlementReceipts,
                    broken),
                Throws.ArgumentException);
        }

        [Test]
        public void Snapshot_RejectsOrphanPendingCreationReceiptBeforeAwaitingMatch()
        {
            var planning = CareerPersistenceTestData.PlanningSnapshot(
                new ProfileId(Guid.NewGuid()),
                new SaveId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()));
            var identity = NextIdentity(planning, 5);
            var sessionId = Guid.NewGuid();
            var context = new Sha256Digest(new string('d', 64));
            var receipts = planning.OperationReceipts.Concat(new[]
            {
                new OperationReceipt(
                    new OperationId(Guid.NewGuid()),
                    OperationKind.CreatePendingMatch,
                    OperationReceiptTarget.ForPendingMatch(
                        new WeekPlanId(Guid.NewGuid()),
                        new SlotActionId(Guid.NewGuid()),
                        new OccurrenceId(Guid.NewGuid()),
                        sessionId,
                        "schedule.u1w1.match.01",
                        context),
                    new Sha256Digest(new string('e', 64)),
                    identity.LineageId,
                    identity.Revision,
                    100,
                    OperationOutcomeKind.PendingMatchCreated,
                    OperationOutcomeSummary.ForPendingMatchCreated(sessionId, context))
            });

            Assert.That(
                () => new CareerSaveSnapshot(
                    planning.Versions,
                    identity,
                    planning.CareerSeed,
                    planning.CareerName,
                    planning.PlayerDraft,
                    planning.Onboarding,
                    planning.Progression,
                    planning.TrainingEmphases,
                    planning.Player,
                    planning.TeamId,
                    planning.PotentialGrade,
                    planning.Fatigue,
                    planning.Mindset,
                    planning.CoachTrust,
                    receipts),
                Throws.ArgumentException);
        }

        private static CareerWeekPlanState ConfirmedFirstWeekPlan()
        {
            return new CareerWeekPlanState(
                new WeekPlanId(Guid.NewGuid()),
                1,
                1,
                new[]
                {
                    Action(CareerWeekActionKind.SpecializedTraining, "week_action.specialized.spike"),
                    Action(CareerWeekActionKind.StrengthTraining, "week_action.strength.jump"),
                    Action(CareerWeekActionKind.Match, "schedule.u1w1.match.01")
                },
                true);
        }

        private static CareerWeekActionState Action(CareerWeekActionKind kind, string contentId)
        {
            return new CareerWeekActionState(
                new SlotActionId(Guid.NewGuid()),
                new OccurrenceId(Guid.NewGuid()),
                kind,
                contentId);
        }

        private static PendingCareerMatch Pending(
            byte[] canonical,
            PlayerId[] players,
            FrozenCareerTrainingEmphasis[] emphases,
            CareerMatchLifecycleExecutionMode mode = CareerMatchLifecycleExecutionMode.Fixture,
            string fixtureId = "fixture.career.u1w1.6v6",
            int? fixtureVersion = 1,
            PlayerId? protagonist = null)
        {
            return new PendingCareerMatch(
                Guid.NewGuid(),
                new OperationId(Guid.NewGuid()),
                new LineageId(Guid.NewGuid()),
                9,
                new CareerMatchLifecycleVersions(
                    CareerMatchLifecycleVersions.ContractV4,
                    1,
                    1,
                    1,
                    null,
                    null),
                mode,
                fixtureId,
                fixtureVersion,
                123u,
                "competition.university",
                "schedule.u1w1.match.01",
                new WeekPlanId(Guid.NewGuid()),
                new SlotActionId(Guid.NewGuid()),
                new OccurrenceId(Guid.NewGuid()),
                CareerMatchPriority.AttackFirst,
                new Sha256Digest(new string('a', 64)),
                canonical,
                new TeamId("home-team"),
                new TeamId("away-team"),
                players,
                protagonist ?? players[1],
                emphases);
        }

        private static PlayerId[] Players()
        {
            var result = new PlayerId[12];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new PlayerId("player-" + (index + 1).ToString("00"));
            }

            return result;
        }

        private static CareerSettlementSummary Summary()
        {
            var growth = new CareerAttributeGrowthChange[8];
            for (var index = 0; index < growth.Length; index++)
            {
                var kind = (CareerAttributeKind)index;
                growth[index] = new CareerAttributeGrowthChange(
                    kind,
                    "match.growth." + kind.ToString().ToLowerInvariant(),
                    new CareerAttributeProgress(5000, 10),
                    5,
                    5,
                    new CareerAttributeProgress(5000, 15));
            }

            return new CareerSettlementSummary(
                new[] { new CareerMatchSetScoreSummary(1, 25, 20, true) },
                new CareerProtagonistMatchFacts(
                    new CareerSpikeFactSummary(10, 5, 1),
                    new CareerServeFactSummary(8, 2, 1),
                    new CareerReceptionFactSummary(5, 2, 1, 1, 1, 0),
                    new CareerDefenseFactSummary(6, 4),
                    new CareerBlockFactSummary(5, 3, 1),
                    new CareerMatchLoadFactSummary(45, 60000, 12000, 8, 2, 4000, 7000),
                    new CareerStabilityFactSummary(4, 3, 1, 1, 2)),
                CareerMatchPriority.AttackFirst,
                true,
                true,
                growth,
                new CareerReasonedIntegerChange("match.fatigue", 50, 8, 8, 58),
                new CareerReasonedIntegerChange("match.mindset", 50, 4, 4, 54),
                new CareerReasonedIntegerChange("match.coach_trust", 50, 5, 5, 55),
                new CareerReasonedIntegerChange("weekend.fatigue", 58, 0, 0, 58),
                new CareerReasonedIntegerChange("weekend.mindset", 54, 0, 0, 54),
                new CareerReasonedIntegerChange("weekend.coach_trust", 55, 0, 0, 55));
        }

        private static CareerSaveSnapshot BeforePending()
        {
            return CareerPersistenceTestData.AfterSecondTraining(
                CareerPersistenceTestData.PlannedAfterFirstTraining(
                    new ProfileId(Guid.NewGuid()),
                    new SaveId(Guid.NewGuid()),
                    new LineageId(Guid.NewGuid())));
        }

        internal static CareerSaveSnapshot AwaitingSnapshot(CareerSaveSnapshot before)
        {
            var identity = NextIdentity(before, 9);
            var sessionId = Guid.NewGuid();
            var operationId = new OperationId(Guid.NewGuid());
            var plan = before.Progression.WeekPlan;
            var contextDigest = new Sha256Digest(new string('a', 64));
            var frozen = before.TrainingEmphases.Freeze()
                .Select(item => new FrozenCareerTrainingEmphasis(
                    item.Direction,
                    item.SourceSlotActionIds,
                    item.TotalBonusBasisPoints))
                .ToArray();
            var pending = new PendingCareerMatch(
                sessionId,
                operationId,
                identity.LineageId,
                identity.Revision,
                new CareerMatchLifecycleVersions(
                    CareerMatchLifecycleVersions.ContractV4,
                    1,
                    1,
                    1,
                    null,
                    null),
                CareerMatchLifecycleExecutionMode.Fixture,
                "fixture.career.u1w1.6v6",
                1,
                123u,
                "competition.university",
                plan.Slots[2].ContentId,
                plan.PlanId,
                plan.Slots[2].SlotActionId,
                plan.Slots[2].OccurrenceId,
                CareerMatchPriority.AttackFirst,
                contextDigest,
                new byte[] { (byte)'{', (byte)'}' },
                before.TeamId.Value,
                new TeamId("university-red"),
                RosterWith(before.Player.PlayerId),
                before.Player.PlayerId,
                frozen);
            var receipts = new List<OperationReceipt>(before.OperationReceipts)
            {
                new OperationReceipt(
                    operationId,
                    OperationKind.CreatePendingMatch,
                    OperationReceiptTarget.ForPendingMatch(
                        plan.PlanId,
                        plan.Slots[2].SlotActionId,
                        plan.Slots[2].OccurrenceId,
                        sessionId,
                        plan.Slots[2].ContentId,
                        contextDigest),
                    new Sha256Digest(new string('b', 64)),
                    identity.LineageId,
                    identity.Revision,
                    100,
                    OperationOutcomeKind.PendingMatchCreated,
                    OperationOutcomeSummary.ForPendingMatchCreated(sessionId, contextDigest))
            };
            return new CareerSaveSnapshot(
                before.Versions,
                identity,
                before.CareerSeed,
                before.CareerName,
                before.PlayerDraft,
                before.Onboarding,
                CareerProgressionState.AwaitingMatch(plan, sessionId),
                before.TrainingEmphases,
                before.Player,
                before.TeamId,
                before.PotentialGrade,
                before.Fatigue,
                before.Mindset,
                before.CoachTrust,
                receipts,
                pending,
                Array.Empty<CareerMatchHistoryEntry>(),
                Array.Empty<CareerSettlementReceipt>());
        }

        internal static CareerSaveSnapshot SettledWeekTwoSnapshot(CareerSaveSnapshot awaiting)
        {
            var identity = NextIdentity(awaiting, 10);
            var summary = Summary();
            var resultDigest = new Sha256Digest(new string('c', 64));
            var history = new CareerMatchHistoryEntry(
                awaiting.PendingMatch.SessionId,
                awaiting.PendingMatch.ScheduleItemId,
                awaiting.PendingMatch.SourceWeekPlanId,
                awaiting.PendingMatch.SourceSlotActionId,
                awaiting.PendingMatch.ContextDigest,
                resultDigest,
                awaiting.PendingMatch.CanonicalContextUtf8,
                new byte[] { (byte)'[', (byte)']' },
                identity.LineageId,
                identity.Revision,
                200,
                summary);
            var receipt = new CareerSettlementReceipt(
                history.SessionId,
                history.ContextDigest,
                history.ResultDigest,
                identity.LineageId,
                identity.Revision,
                history.SettledAtUtcMs,
                summary);
            var weekTwo = new CareerWeekPlanState(
                new WeekPlanId(Guid.NewGuid()),
                1,
                2,
                new CareerWeekActionState[] { null, null, null },
                false);
            var settledPlayer = new CareerPlayerRecord(
                awaiting.Player.PlayerId,
                awaiting.Player.DisplayName,
                awaiting.Player.JerseyNumber,
                summary.AfterAttributes);
            return new CareerSaveSnapshot(
                awaiting.Versions,
                identity,
                awaiting.CareerSeed,
                awaiting.CareerName,
                awaiting.PlayerDraft,
                awaiting.Onboarding,
                CareerProgressionState.Planning(weekTwo),
                TrainingEmphasisLedger.Empty,
                settledPlayer,
                awaiting.TeamId,
                awaiting.PotentialGrade,
                summary.WeekendFatigueChange.NewValue,
                summary.WeekendMindsetChange.NewValue,
                summary.WeekendCoachTrustChange.NewValue,
                awaiting.OperationReceipts,
                null,
                new[] { history },
                new[] { receipt });
        }

        private static CareerSaveSnapshot SnapshotWithCareerState(
            CareerSaveSnapshot source,
            CareerPlayerRecord player,
            TeamId? teamId,
            int? fatigue,
            int? mindset,
            int? coachTrust)
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
                player,
                teamId,
                source.PotentialGrade,
                fatigue,
                mindset,
                coachTrust,
                source.OperationReceipts,
                source.PendingMatch,
                source.MatchHistory,
                source.SettlementReceipts);
        }

        private static CareerSaveSnapshot FullSnapshot(
            CareerSaveSnapshot source,
            CareerProgressionState progression,
            PendingCareerMatch pending,
            IEnumerable<CareerMatchHistoryEntry> history,
            IEnumerable<CareerSettlementReceipt> settlementReceipts,
            IEnumerable<OperationReceipt> operationReceipts = null)
        {
            return new CareerSaveSnapshot(
                source.Versions,
                source.Identity,
                source.CareerSeed,
                source.CareerName,
                source.PlayerDraft,
                source.Onboarding,
                progression,
                source.TrainingEmphases,
                source.Player,
                source.TeamId,
                source.PotentialGrade,
                source.Fatigue,
                source.Mindset,
                source.CoachTrust,
                operationReceipts ?? source.OperationReceipts,
                pending,
                history,
                settlementReceipts);
        }

        private static CareerSaveIdentity NextIdentity(CareerSaveSnapshot source, long revision)
        {
            return new CareerSaveIdentity(
                source.Identity.ProfileId,
                source.Identity.SaveId,
                source.Identity.LineageId,
                revision,
                source.Identity.CreatedAtUtcMs,
                source.Identity.UpdatedAtUtcMs + 1,
                new Sha256Digest(new string('0', 64)));
        }

        private static PlayerId[] RosterWith(PlayerId protagonist)
        {
            var players = Players();
            players[1] = protagonist;
            return players;
        }

        private static PendingCareerMatch PendingWithCareerIdentity(
            PendingCareerMatch source,
            TeamId homeTeamId,
            TeamId awayTeamId,
            IEnumerable<PlayerId> orderedPlayerIds,
            PlayerId protagonistPlayerId)
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
                source.CanonicalContextUtf8,
                homeTeamId,
                awayTeamId,
                orderedPlayerIds,
                protagonistPlayerId,
                source.FrozenTrainingEmphases);
        }

        private static int DifferentStatus(int value)
        {
            return value == 100 ? 99 : value + 1;
        }

        private static void MutatePending(PendingInput input, string contradiction)
        {
            switch (contradiction)
            {
                case "session": input.SessionId = Guid.Empty; return;
                case "operation": input.OperationId = default; return;
                case "lineage": input.LineageId = default; return;
                case "revision-zero": input.Revision = 0; return;
                case "revision-unsafe": input.Revision = long.MaxValue; return;
                case "mode-invalid": input.Mode = (CareerMatchLifecycleExecutionMode)99; return;
                case "fixture-id": input.FixtureId = "bad fixture"; return;
                case "fixture-version": input.FixtureVersion = 0; return;
                case "fixture-algorithms": input.Versions = new CareerMatchLifecycleVersions(2, 1, 1, 1, 1, 1); return;
                case "direct-fixture": input.Mode = CareerMatchLifecycleExecutionMode.Direct; return;
                case "direct-algorithms": input.Mode = CareerMatchLifecycleExecutionMode.Direct; input.FixtureId = null; input.FixtureVersion = null; input.Versions = new CareerMatchLifecycleVersions(2, 1, 1, 1, 1, 1); return;
                case "quick-fixture": input.Mode = CareerMatchLifecycleExecutionMode.QuickSimulation; input.Versions = new CareerMatchLifecycleVersions(2, 1, 1, 1, 1, 1); return;
                case "quick-algorithms": input.Mode = CareerMatchLifecycleExecutionMode.QuickSimulation; input.FixtureId = null; input.FixtureVersion = null; return;
                case "competition": input.CompetitionId = "bad competition"; return;
                case "schedule": input.ScheduleItemId = "bad schedule"; return;
                case "plan": input.PlanId = default; return;
                case "slot": input.SlotId = default; return;
                case "occurrence": input.OccurrenceId = default; return;
                case "priority": input.Priority = (CareerMatchPriority)99; return;
                case "digest": input.ContextDigest = default; return;
                case "bytes-null": input.Canonical = null; return;
                case "bytes-empty": input.Canonical = Array.Empty<byte>(); return;
                case "bytes-invalid": input.Canonical = new byte[] { 0xff }; return;
                case "home-team": input.HomeTeam = default; return;
                case "away-team": input.AwayTeam = default; return;
                case "same-team": input.AwayTeam = input.HomeTeam; return;
                case "roster-null": input.Roster = null; return;
                case "roster-eleven": input.Roster = input.Roster.Take(11).ToArray(); return;
                case "roster-thirteen": input.Roster = input.Roster.Concat(new[] { new PlayerId("player-13") }).ToArray(); return;
                case "roster-duplicate": input.Roster[11] = input.Roster[0]; return;
                case "roster-id": input.Roster[11] = default; return;
                case "protagonist": input.Protagonist = new PlayerId("not-present"); return;
                case "emphases-null": input.Emphases = null; return;
                case "emphases-duplicate": input.Emphases = new[]
                {
                    new FrozenCareerTrainingEmphasis(CareerTrainingDirection.Spike, new[] { new SlotActionId(Guid.NewGuid()) }, 1000),
                    new FrozenCareerTrainingEmphasis(CareerTrainingDirection.Spike, new[] { new SlotActionId(Guid.NewGuid()) }, 1000)
                }; return;
                default: throw new ArgumentOutOfRangeException(nameof(contradiction));
            }
        }

        private static CareerMatchLifecycleVersions InvalidVersions(string contradiction)
        {
            switch (contradiction)
            {
                case "contract": return new CareerMatchLifecycleVersions(1, 1, 1, 1, null, null);
                case "content": return new CareerMatchLifecycleVersions(2, 0, 1, 1, null, null);
                case "rules": return new CareerMatchLifecycleVersions(2, 1, 0, 1, null, null);
                case "career-random": return new CareerMatchLifecycleVersions(2, 1, 1, 0, null, null);
                case "simulation": return new CareerMatchLifecycleVersions(2, 1, 1, 1, 0, 1);
                case "match-random": return new CareerMatchLifecycleVersions(2, 1, 1, 1, 1, 0);
                case "unpaired-simulation": return new CareerMatchLifecycleVersions(2, 1, 1, 1, 1, null);
                case "unpaired-random": return new CareerMatchLifecycleVersions(2, 1, 1, 1, null, 1);
                default: throw new ArgumentOutOfRangeException(nameof(contradiction));
            }
        }

        private static FrozenCareerTrainingEmphasis InvalidFrozenEmphasis(string contradiction)
        {
            var one = new SlotActionId(Guid.NewGuid());
            var two = new SlotActionId(Guid.NewGuid());
            switch (contradiction)
            {
                case "null-sources": return new FrozenCareerTrainingEmphasis(CareerTrainingDirection.Spike, null, 1000);
                case "zero-sources": return new FrozenCareerTrainingEmphasis(CareerTrainingDirection.Spike, Array.Empty<SlotActionId>(), 1000);
                case "three-sources": return new FrozenCareerTrainingEmphasis(CareerTrainingDirection.Spike, new[] { one, two, new SlotActionId(Guid.NewGuid()) }, 1500);
                case "duplicate-sources": return new FrozenCareerTrainingEmphasis(CareerTrainingDirection.Spike, new[] { one, one }, 1500);
                case "one-wrong-total": return new FrozenCareerTrainingEmphasis(CareerTrainingDirection.Spike, new[] { one }, 1500);
                case "two-wrong-total": return new FrozenCareerTrainingEmphasis(CareerTrainingDirection.Spike, new[] { one, two }, 1000);
                case "invalid-direction": return new FrozenCareerTrainingEmphasis((CareerTrainingDirection)99, new[] { one }, 1000);
                default: throw new ArgumentOutOfRangeException(nameof(contradiction));
            }
        }

        private static object InvalidSettlementEvidence(string contradiction)
        {
            switch (contradiction)
            {
                case "set-number": return new CareerMatchSetScoreSummary(0, 0, 0, true);
                case "spike-subset": return new CareerSpikeFactSummary(1, 1, 1);
                case "serve-subset": return new CareerServeFactSummary(1, 1, 1);
                case "reception-sum": return new CareerReceptionFactSummary(1, 1, 1, 0, 0, 0);
                case "defense-subset": return new CareerDefenseFactSummary(1, 2);
                case "block-subset": return new CareerBlockFactSummary(1, 2, 1);
                case "load-jumps": return new CareerMatchLoadFactSummary(1, 1, 1, 1, 2, 1, 1);
                case "load-basis-points": return new CareerMatchLoadFactSummary(1, 1, 1, 1, 1, 10001, 1);
                case "stability-actions": return new CareerStabilityFactSummary(1, 1, 1, 0, 0);
                case "stability-streak": return new CareerStabilityFactSummary(1, 0, 0, 1, 1);
                case "status-range": return new CareerReasonedIntegerChange("match.status", 101, 0, 0, 101);
                case "weekend-nonzero":
                    var summary = Summary();
                    return new CareerSettlementSummary(
                        summary.Sets,
                        summary.ProtagonistFacts,
                        summary.SelectedPriority,
                        summary.PriorityExecuted,
                        summary.Won,
                        summary.GrowthChanges,
                        summary.MatchFatigueChange,
                        summary.MatchMindsetChange,
                        summary.MatchCoachTrustChange,
                        new CareerReasonedIntegerChange("weekend.fatigue", 58, -1, -1, 57),
                        summary.WeekendMindsetChange,
                        summary.WeekendCoachTrustChange);
                default: throw new ArgumentOutOfRangeException(nameof(contradiction));
            }
        }

        private static PendingCareerMatch ContradictPending(
            PendingCareerMatch source,
            string contradiction)
        {
            var session = source.SessionId;
            var operation = source.CreationOperationId;
            var lineage = source.CreatedLineageId;
            var revision = source.CreatedRevision;
            var versions = source.Versions;
            var plan = source.SourceWeekPlanId;
            var slot = source.SourceSlotActionId;
            var occurrence = source.SourceActionOccurrenceId;
            var schedule = source.ScheduleItemId;
            var context = source.ContextDigest;
            IEnumerable<FrozenCareerTrainingEmphasis> emphases = source.FrozenTrainingEmphases;
            switch (contradiction)
            {
                case "session": session = Guid.NewGuid(); break;
                case "lineage": lineage = new LineageId(Guid.NewGuid()); break;
                case "revision": revision--; break;
                case "versions":
                    versions = new CareerMatchLifecycleVersions(
                        CareerMatchLifecycleVersions.ContractV4,
                        2,
                        1,
                        1,
                        null,
                        null);
                    break;
                case "plan": plan = new WeekPlanId(Guid.NewGuid()); break;
                case "slot": slot = new SlotActionId(Guid.NewGuid()); break;
                case "occurrence": occurrence = new OccurrenceId(Guid.NewGuid()); break;
                case "schedule": schedule = "schedule.other"; break;
                case "context": context = new Sha256Digest(new string('f', 64)); break;
                case "creation-operation": operation = new OperationId(Guid.NewGuid()); break;
                case "emphasis": emphases = Array.Empty<FrozenCareerTrainingEmphasis>(); break;
                default: throw new ArgumentOutOfRangeException(nameof(contradiction));
            }

            return new PendingCareerMatch(
                session,
                operation,
                lineage,
                revision,
                versions,
                source.ExecutionMode,
                source.FixtureId,
                source.FixtureVersion,
                source.MatchSeed,
                source.CompetitionId,
                schedule,
                plan,
                slot,
                occurrence,
                source.PreMatchPriority,
                context,
                source.CanonicalContextUtf8,
                source.HomeTeamId,
                source.AwayTeamId,
                source.OrderedPlayerIds,
                source.ProtagonistPlayerId,
                emphases);
        }

        private sealed class PendingInput
        {
            public Guid SessionId;
            public OperationId OperationId;
            public LineageId LineageId;
            public long Revision;
            public CareerMatchLifecycleVersions Versions;
            public CareerMatchLifecycleExecutionMode Mode;
            public string FixtureId;
            public int? FixtureVersion;
            public uint MatchSeed;
            public string CompetitionId;
            public string ScheduleItemId;
            public WeekPlanId PlanId;
            public SlotActionId SlotId;
            public OccurrenceId OccurrenceId;
            public CareerMatchPriority Priority;
            public Sha256Digest ContextDigest;
            public byte[] Canonical;
            public TeamId HomeTeam;
            public TeamId AwayTeam;
            public PlayerId[] Roster;
            public PlayerId Protagonist;
            public FrozenCareerTrainingEmphasis[] Emphases;

            public static PendingInput Valid()
            {
                var roster = Players();
                return new PendingInput
                {
                    SessionId = Guid.NewGuid(),
                    OperationId = new OperationId(Guid.NewGuid()),
                    LineageId = new LineageId(Guid.NewGuid()),
                    Revision = 9,
                    Versions = new CareerMatchLifecycleVersions(
                        CareerMatchLifecycleVersions.ContractV4,
                        1,
                        1,
                        1,
                        null,
                        null),
                    Mode = CareerMatchLifecycleExecutionMode.Fixture,
                    FixtureId = "fixture.career.u1w1.6v6",
                    FixtureVersion = 1,
                    MatchSeed = 123u,
                    CompetitionId = "competition.university",
                    ScheduleItemId = "schedule.u1w1.match.01",
                    PlanId = new WeekPlanId(Guid.NewGuid()),
                    SlotId = new SlotActionId(Guid.NewGuid()),
                    OccurrenceId = new OccurrenceId(Guid.NewGuid()),
                    Priority = CareerMatchPriority.AttackFirst,
                    ContextDigest = new Sha256Digest(new string('a', 64)),
                    Canonical = new byte[] { (byte)'{', (byte)'}' },
                    HomeTeam = new TeamId("home-team"),
                    AwayTeam = new TeamId("away-team"),
                    Roster = roster,
                    Protagonist = roster[1],
                    Emphases = Array.Empty<FrozenCareerTrainingEmphasis>()
                };
            }

            public PendingCareerMatch Create()
            {
                return new PendingCareerMatch(
                    SessionId, OperationId, LineageId, Revision, Versions, Mode,
                    FixtureId, FixtureVersion, MatchSeed, CompetitionId, ScheduleItemId,
                    PlanId, SlotId, OccurrenceId, Priority, ContextDigest, Canonical,
                    HomeTeam, AwayTeam, Roster, Protagonist, Emphases);
            }
        }
    }
}
