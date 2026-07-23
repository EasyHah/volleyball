using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchSettlementRulesV1Tests
    {
        private const long MaximumSafeInteger = 9007199254740991L;
        private static readonly Guid SessionId =
            Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Sha256Digest ContextDigest =
            new Sha256Digest("da570cff972d280acb9307edb715bcef88a0f958e75ea615072a5be25edf0527");
        private static readonly Sha256Digest ResultDigest =
            new Sha256Digest("3fbb03380ce766a7695c7ad7d0697c5c631893d714b229abcf8c7c1017182d98");

        [Test]
        public void FixtureProtagonistFacts_ProduceIndependentlyDerivedBaseXpGoldenVector()
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var summary = Calculate(
                pending,
                Facts(pending, FixtureProtagonistFacts()),
                Player(pending.ProtagonistPlayerId));

            AssertRequested(summary, 67, 19, 30, 22, 15, 26, 52, 82);
        }

        [TestCase(0L, 0L)]
        [TestCase(1L, 1L)]
        [TestCase(9999L, 1L)]
        [TestCase(10000L, 1L)]
        [TestCase(10001L, 2L)]
        public void Movement_UsesExactCeilingBoundaries(long distanceMillimeters, long expected)
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var protagonist = Protagonist(
                load: new CareerMatchLoadFacts(0, 0, distanceMillimeters, 0, 0, 0, 0));

            var summary = Calculate(pending, Facts(pending, protagonist), Player(pending.ProtagonistPlayerId));

            Assert.That(summary.GrowthChanges[(int)CareerAttributeKind.Movement].RequestedDelta,
                Is.EqualTo(expected));
        }

        [Test]
        public void IsolatedFacts_UseTheBoundEightBaseFormulas()
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var protagonist = Protagonist(
                spike: new CareerSpikeFacts(5, 2, 1),
                serve: new CareerServeFacts(5, 2, 1),
                reception: new CareerReceptionFacts(15, 1, 2, 3, 4, 5),
                defense: new CareerDefenseFacts(5, 3),
                block: new CareerBlockFacts(5, 3, 2),
                load: new CareerMatchLoadFacts(5, 60001, 10001, 5, 2, 1001, 251));

            var summary = Calculate(pending, Facts(pending, protagonist), Player(pending.ProtagonistPlayerId));

            AssertRequested(summary, 23, 27, 29, 17, 26, 2, 11, 9);
        }

        [TestCase(PotentialGrade.D, 8L)]
        [TestCase(PotentialGrade.C, 9L)]
        [TestCase(PotentialGrade.B, 11L)]
        [TestCase(PotentialGrade.A, 12L)]
        [TestCase(PotentialGrade.S, 13L)]
        public void Potential_UsesV1MultiplierAndFloors(PotentialGrade grade, long expected)
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var protagonist = Protagonist(spike: new CareerSpikeFacts(2, 1, 1));

            var summary = Calculate(
                pending,
                Facts(pending, protagonist),
                Player(pending.ProtagonistPlayerId),
                grade);

            Assert.That(summary.GrowthChanges[(int)CareerAttributeKind.Spike].RequestedDelta,
                Is.EqualTo(expected));
        }

        [Test]
        public void Scaling_FloorsPotentialBeforeAddingEmphasisAndPriority()
        {
            var pending = Pending(
                priority: CareerMatchPriority.AttackFirst,
                emphases: new[] { Emphasis(CareerTrainingDirection.Serve, 1500) });
            var protagonist = Protagonist(serve: new CareerServeFacts(5, 1, 1));

            var summary = Calculate(
                pending,
                Facts(pending, protagonist),
                Player(pending.ProtagonistPlayerId),
                PotentialGrade.D);

            Assert.That(summary.GrowthChanges[(int)CareerAttributeKind.Serve].RequestedDelta,
                Is.EqualTo(18));
        }

        [TestCase(CareerMatchPriority.AttackFirst,
            73L, 20L, 30L, 22L, 15L, 26L, 52L, 82L)]
        [TestCase(CareerMatchPriority.FirstContactSecurity,
            67L, 19L, 33L, 24L, 15L, 26L, 52L, 82L)]
        [TestCase(CareerMatchPriority.StaminaControl,
            67L, 19L, 30L, 22L, 15L, 26L, 52L, 82L)]
        public void PriorityGrowth_AffectsOnlyItsBoundAxes(
            CareerMatchPriority priority,
            long spike,
            long serve,
            long reception,
            long defense,
            long block,
            long movement,
            long jump,
            long stamina)
        {
            var pending = Pending(priority: priority);

            var summary = Calculate(
                pending,
                Facts(pending, FixtureProtagonistFacts()),
                Player(pending.ProtagonistPlayerId));

            AssertRequested(summary, spike, serve, reception, defense, block, movement, jump, stamina);
            Assert.That(summary.PriorityExecuted, Is.True,
                "Every priority must be explicitly executed by the frozen formal fixture.");
        }

        [Test]
        public void Emphasis_AppliesOnlyToEachMatchingDirection()
        {
            var emphases = Enum.GetValues(typeof(CareerTrainingDirection))
                .Cast<CareerTrainingDirection>()
                .Select(direction => Emphasis(direction, 1000))
                .ToArray();
            var pending = Pending(priority: CareerMatchPriority.StaminaControl, emphases: emphases);

            var summary = Calculate(
                pending,
                Facts(pending, FixtureProtagonistFacts()),
                Player(pending.ProtagonistPlayerId));

            AssertRequested(summary, 73, 20, 33, 24, 16, 28, 57, 90);
        }

        [Test]
        public void Emphasis_DoesNotAffectANonmatchingFactAxis()
        {
            var pending = Pending(
                priority: CareerMatchPriority.StaminaControl,
                emphases: new[] { Emphasis(CareerTrainingDirection.Serve, 1500) });
            var protagonist = Protagonist(spike: new CareerSpikeFacts(2, 1, 1));

            var summary = Calculate(
                pending,
                Facts(pending, protagonist),
                Player(pending.ProtagonistPlayerId));

            Assert.That(summary.GrowthChanges[(int)CareerAttributeKind.Spike].RequestedDelta,
                Is.EqualTo(11));
            Assert.That(summary.GrowthChanges[(int)CareerAttributeKind.Serve].RequestedDelta,
                Is.Zero);
        }

        [Test]
        public void ZeroFacts_ProduceEightExplicitZeroGrowthChanges()
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);

            var summary = Calculate(
                pending,
                Facts(pending, Protagonist()),
                Player(pending.ProtagonistPlayerId));

            Assert.That(summary.GrowthChanges.Select(change => change.RequestedDelta),
                Is.All.Zero);
            Assert.That(summary.GrowthChanges.Select(change => change.ActualDelta),
                Is.All.Zero);
        }

        [Test]
        public void GrowthCap_PreservesRequestedDeltaAndAbilityWhileLimitingActualDelta()
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var attributes = Attributes(5000, CareerAttributeProgress.MaximumGrowthExperience - 5);

            var summary = Calculate(
                pending,
                Facts(pending, FixtureProtagonistFacts()),
                Player(pending.ProtagonistPlayerId, attributes));

            AssertRequested(summary, 67, 19, 30, 22, 15, 26, 52, 82);
            Assert.That(summary.GrowthChanges.Select(change => change.ActualDelta),
                Is.All.EqualTo(5L));
            Assert.That(summary.GrowthChanges.Select(change => change.After.AbilityBasisPoints),
                Is.All.EqualTo(5000));
            Assert.That(summary.GrowthChanges.Select(change => change.After.GrowthExperience),
                Is.All.EqualTo(CareerAttributeProgress.MaximumGrowthExperience));
        }

        [Test]
        public void GrowthCap_AtMaximumAppliesZeroButStillExplainsRequest()
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var attributes = Attributes(4321, CareerAttributeProgress.MaximumGrowthExperience);

            var summary = Calculate(
                pending,
                Facts(pending, FixtureProtagonistFacts()),
                Player(pending.ProtagonistPlayerId, attributes));

            Assert.That(summary.GrowthChanges.Select(change => change.RequestedDelta),
                Is.All.GreaterThan(0L));
            Assert.That(summary.GrowthChanges.Select(change => change.ActualDelta),
                Is.All.Zero);
            Assert.That(summary.GrowthChanges.Select(change => change.After.AbilityBasisPoints),
                Is.All.EqualTo(4321));
        }

        [TestCase(CareerMatchPriority.AttackFirst, 0, 5000, 0)]
        [TestCase(CareerMatchPriority.AttackFirst, 10000, 0, 20)]
        [TestCase(CareerMatchPriority.AttackFirst, 10000, 10000, 10)]
        [TestCase(CareerMatchPriority.AttackFirst, 1, 9999, 1)]
        [TestCase(CareerMatchPriority.StaminaControl, 10000, 0, 16)]
        [TestCase(CareerMatchPriority.StaminaControl, 500, 10000, 1)]
        [TestCase(CareerMatchPriority.StaminaControl, 1001, 10000, 2)]
        public void Fatigue_UsesStaminaAbilityExactCeilingsAndPriorityReduction(
            CareerMatchPriority priority,
            int workloadBasisPoints,
            int staminaAbilityBasisPoints,
            int expectedRequested)
        {
            var pending = Pending(priority: priority);
            var protagonist = Protagonist(
                load: new CareerMatchLoadFacts(0, 0, 0, 0, 0, 0, workloadBasisPoints));
            var attributes = Attributes(5000, 0, staminaAbilityBasisPoints);

            var summary = Calculate(
                pending,
                Facts(pending, protagonist),
                Player(pending.ProtagonistPlayerId, attributes));

            Assert.That(summary.MatchFatigueChange.RequestedDelta, Is.EqualTo(expectedRequested));
        }

        [Test]
        public void Fatigue_ClampsActualButRetainsRequested()
        {
            var pending = Pending(priority: CareerMatchPriority.AttackFirst);
            var protagonist = Protagonist(
                load: new CareerMatchLoadFacts(0, 0, 0, 0, 0, 0, 10000));

            var summary = Calculate(
                pending,
                Facts(pending, protagonist),
                Player(pending.ProtagonistPlayerId, Attributes(5000, 0, 0)),
                fatigue: 95);

            AssertChange(summary.MatchFatigueChange, 95, 20, 5, 100);
        }

        [TestCase(0, 0, 4)]
        [TestCase(1, 2, 3)]
        [TestCase(1, 3, 2)]
        [TestCase(3, 3, 1)]
        public void Mindset_UsesBoundStreakPenalty(int episodes, int longest, int expectedRequested)
        {
            var pending = Pending();
            var protagonist = Protagonist(
                stability: new CareerStabilityFacts(0, 0, 0, episodes, longest));

            var summary = Calculate(
                pending,
                Facts(pending, protagonist),
                Player(pending.ProtagonistPlayerId));

            Assert.That(summary.MatchMindsetChange.RequestedDelta, Is.EqualTo(expectedRequested));
        }

        [TestCase(5, 0, true, 6)]
        [TestCase(4, 2, true, 6)]
        [TestCase(3, 2, true, 5)]
        [TestCase(2, 2, true, 4)]
        [TestCase(2, 3, false, -4)]
        [TestCase(2, 4, false, -5)]
        [TestCase(0, 5, false, -5)]
        public void Mindset_ClampsCriticalDifferenceAndUsesWinLossTerm(
            int successes,
            int errors,
            bool won,
            int expectedRequested)
        {
            var pending = Pending();
            var protagonist = Protagonist(
                stability: new CareerStabilityFacts(
                    successes + errors, successes, errors, 0, 0));

            var summary = Calculate(
                pending,
                Facts(pending, protagonist, winnerTeamId: won ? pending.HomeTeamId : pending.AwayTeamId),
                Player(pending.ProtagonistPlayerId));

            Assert.That(summary.MatchMindsetChange.RequestedDelta, Is.EqualTo(expectedRequested));
        }

        [Test]
        public void Mindset_ClampsAtBothStatusBounds()
        {
            var pending = Pending();
            var positive = Protagonist(stability: new CareerStabilityFacts(5, 5, 0, 0, 0));
            var negative = Protagonist(stability: new CareerStabilityFacts(5, 0, 5, 3, 3));

            var upper = Calculate(
                pending,
                Facts(pending, positive),
                Player(pending.ProtagonistPlayerId),
                mindset: 99);
            var lower = Calculate(
                pending,
                Facts(pending, negative, winnerTeamId: pending.AwayTeamId),
                Player(pending.ProtagonistPlayerId),
                mindset: 1);

            AssertChange(upper.MatchMindsetChange, 99, 6, 1, 100);
            AssertChange(lower.MatchMindsetChange, 1, -8, -1, 0);
        }

        [Test]
        public void AttackPriority_UsesStrictComparisonAndTrustTerms()
        {
            var pending = Pending(priority: CareerMatchPriority.AttackFirst);
            var tie = Protagonist();
            var success = Protagonist(
                spike: new CareerSpikeFacts(1, 1, 0),
                stability: new CareerStabilityFacts(2, 2, 0, 0, 0));

            var failedSummary = Calculate(
                pending,
                Facts(pending, tie, winnerTeamId: pending.AwayTeamId),
                Player(pending.ProtagonistPlayerId));
            var successSummary = Calculate(
                pending,
                Facts(pending, success),
                Player(pending.ProtagonistPlayerId));

            Assert.That(failedSummary.PriorityExecuted, Is.False);
            Assert.That(failedSummary.MatchCoachTrustChange.RequestedDelta, Is.EqualTo(-2));
            Assert.That(successSummary.PriorityExecuted, Is.True);
            Assert.That(successSummary.MatchCoachTrustChange.RequestedDelta, Is.EqualTo(5));
        }

        [Test]
        public void FirstContactPriority_UsesInclusiveComparison()
        {
            var pending = Pending(priority: CareerMatchPriority.FirstContactSecurity);
            var tie = Protagonist();
            var failed = Protagonist(
                reception: new CareerReceptionFacts(1, 0, 0, 0, 1, 0));

            var tieSummary = Calculate(
                pending,
                Facts(pending, tie),
                Player(pending.ProtagonistPlayerId));
            var failedSummary = Calculate(
                pending,
                Facts(pending, failed),
                Player(pending.ProtagonistPlayerId));

            Assert.That(tieSummary.PriorityExecuted, Is.True);
            Assert.That(failedSummary.PriorityExecuted, Is.False);
        }

        [TestCase(7500, true)]
        [TestCase(7501, false)]
        public void StaminaPriority_UsesInclusive7500Boundary(int workload, bool expected)
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var protagonist = Protagonist(
                load: new CareerMatchLoadFacts(0, 0, 0, 0, 0, 0, workload));

            var summary = Calculate(
                pending,
                Facts(pending, protagonist),
                Player(pending.ProtagonistPlayerId));

            Assert.That(summary.PriorityExecuted, Is.EqualTo(expected));
        }

        [TestCase(2, 0, 5)]
        [TestCase(1, 1, 4)]
        [TestCase(0, 2, 3)]
        public void Trust_UsesPositiveZeroAndNegativeStabilityTerms(
            int criticalSuccesses,
            int criticalErrors,
            int expectedRequested)
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var protagonist = Protagonist(
                load: new CareerMatchLoadFacts(0, 0, 0, 0, 0, 0, 0),
                stability: new CareerStabilityFacts(
                    criticalSuccesses + criticalErrors,
                    criticalSuccesses,
                    criticalErrors,
                    0,
                    0));

            var summary = Calculate(
                pending,
                Facts(pending, protagonist),
                Player(pending.ProtagonistPlayerId));

            Assert.That(summary.PriorityExecuted, Is.True);
            Assert.That(summary.MatchCoachTrustChange.RequestedDelta, Is.EqualTo(expectedRequested));
        }

        [Test]
        public void Trust_ClampsAtBothStatusBounds()
        {
            var successPending = Pending(priority: CareerMatchPriority.StaminaControl);
            var success = Protagonist(
                stability: new CareerStabilityFacts(1, 1, 0, 0, 0));
            var failedPending = Pending(priority: CareerMatchPriority.AttackFirst);
            var failed = Protagonist(
                stability: new CareerStabilityFacts(1, 0, 1, 0, 0));

            var upper = Calculate(
                successPending,
                Facts(successPending, success),
                Player(successPending.ProtagonistPlayerId),
                coachTrust: 98);
            var lower = Calculate(
                failedPending,
                Facts(failedPending, failed, winnerTeamId: failedPending.AwayTeamId),
                Player(failedPending.ProtagonistPlayerId),
                coachTrust: 1);

            AssertChange(upper.MatchCoachTrustChange, 98, 5, 2, 100);
            AssertChange(lower.MatchCoachTrustChange, 1, -3, -1, 0);
        }

        [Test]
        public void Errors_AddOneLearningXpAndStabilityDoesNotChangeGrowth()
        {
            var pending = Pending(priority: CareerMatchPriority.StaminaControl);
            var baseline = Protagonist(
                spike: new CareerSpikeFacts(1, 0, 0),
                serve: new CareerServeFacts(1, 0, 0));
            var errorsWithNeutralStability = Protagonist(
                spike: new CareerSpikeFacts(1, 0, 1),
                serve: new CareerServeFacts(1, 0, 1),
                reception: new CareerReceptionFacts(1, 0, 0, 0, 0, 1));
            var errorsWithNegativeStability = Protagonist(
                spike: new CareerSpikeFacts(1, 0, 1),
                serve: new CareerServeFacts(1, 0, 1),
                reception: new CareerReceptionFacts(1, 0, 0, 0, 0, 1),
                stability: new CareerStabilityFacts(1, 0, 1, 0, 0));

            var baselineSummary = Calculate(
                pending,
                Facts(pending, baseline),
                Player(pending.ProtagonistPlayerId));
            var neutralSummary = Calculate(
                pending,
                Facts(pending, errorsWithNeutralStability),
                Player(pending.ProtagonistPlayerId));
            var negativeSummary = Calculate(
                pending,
                Facts(pending, errorsWithNegativeStability),
                Player(pending.ProtagonistPlayerId));

            Assert.That(
                neutralSummary.GrowthChanges[(int)CareerAttributeKind.Spike].RequestedDelta -
                baselineSummary.GrowthChanges[(int)CareerAttributeKind.Spike].RequestedDelta,
                Is.EqualTo(1));
            Assert.That(
                neutralSummary.GrowthChanges[(int)CareerAttributeKind.Serve].RequestedDelta -
                baselineSummary.GrowthChanges[(int)CareerAttributeKind.Serve].RequestedDelta,
                Is.EqualTo(1));
            Assert.That(
                neutralSummary.GrowthChanges[(int)CareerAttributeKind.Reception].RequestedDelta -
                baselineSummary.GrowthChanges[(int)CareerAttributeKind.Reception].RequestedDelta,
                Is.EqualTo(1));
            Assert.That(
                negativeSummary.GrowthChanges.Select(change => change.RequestedDelta),
                Is.EqualTo(neutralSummary.GrowthChanges.Select(change => change.RequestedDelta)));
            Assert.That(negativeSummary.MatchMindsetChange.RequestedDelta,
                Is.EqualTo(neutralSummary.MatchMindsetChange.RequestedDelta - 1));
            Assert.That(negativeSummary.MatchCoachTrustChange.RequestedDelta,
                Is.EqualTo(neutralSummary.MatchCoachTrustChange.RequestedDelta - 1));
        }

        [Test]
        public void Summary_MapsFactsReasonsPriorityChangesAndExplicitZeroWeekend()
        {
            var pending = Pending(priority: CareerMatchPriority.AttackFirst);
            var facts = Facts(
                pending,
                FixtureProtagonistFacts(),
                sets: new[]
                {
                    new CareerMatchSetScore(1, 25, 21, true),
                    new CareerMatchSetScore(2, 25, 23, true)
                },
                rallyCount: 94);
            var attributes = DistinctAttributes();

            var summary = Calculate(
                pending,
                facts,
                Player(pending.ProtagonistPlayerId, attributes),
                fatigue: 40,
                mindset: 50,
                coachTrust: 60);

            Assert.That(summary.Sets.Count, Is.EqualTo(2));
            Assert.That(summary.Sets[0].SetNumber, Is.EqualTo(1));
            Assert.That(summary.Sets[0].HomePoints, Is.EqualTo(25));
            Assert.That(summary.Sets[0].AwayPoints, Is.EqualTo(21));
            Assert.That(summary.Sets[0].IsComplete, Is.True);
            Assert.That(summary.Sets[1].SetNumber, Is.EqualTo(2));
            Assert.That(summary.Sets[1].HomePoints, Is.EqualTo(25));
            Assert.That(summary.Sets[1].AwayPoints, Is.EqualTo(23));
            Assert.That(summary.Sets[1].IsComplete, Is.True);
            AssertFixtureProtagonistFacts(summary.ProtagonistFacts);
            Assert.That(summary.SelectedPriority, Is.EqualTo(CareerMatchPriority.AttackFirst));
            Assert.That(summary.PriorityExecuted, Is.True);
            Assert.That(summary.Won, Is.True);
            Assert.That(summary.GrowthChanges.Select(change => change.ReasonId), Is.EqualTo(new[]
            {
                "reason.match.growth.spike",
                "reason.match.growth.serve",
                "reason.match.growth.reception",
                "reason.match.growth.defense",
                "reason.match.growth.block",
                "reason.match.growth.movement",
                "reason.match.growth.jump",
                "reason.match.growth.stamina"
            }));
            Assert.That(summary.MatchFatigueChange.ReasonId,
                Is.EqualTo("reason.match.fatigue.workload"));
            Assert.That(summary.MatchMindsetChange.ReasonId,
                Is.EqualTo("reason.match.mindset.result_stability"));
            Assert.That(summary.MatchCoachTrustChange.ReasonId,
                Is.EqualTo("reason.match.coach_trust.priority_stability_result"));
            AssertZeroWeekend(summary.WeekendFatigueChange, summary.MatchFatigueChange.NewValue);
            AssertZeroWeekend(summary.WeekendMindsetChange, summary.MatchMindsetChange.NewValue);
            AssertZeroWeekend(summary.WeekendCoachTrustChange, summary.MatchCoachTrustChange.NewValue);
            AssertCompleteGrowthMapping(
                summary,
                attributes,
                73, 20, 30, 22, 15, 26, 52, 82);
        }

        [Test]
        public void Calculate_RejectsMissingCurrentPlayerAttributesBeforeAnyConsequence()
        {
            var pending = Pending();
            var facts = Facts(pending, Protagonist());
            var corrupted = (CareerPlayerRecord)FormatterServices.GetUninitializedObject(
                typeof(CareerPlayerRecord));
            Assert.That(corrupted.Attributes, Is.Null);

            var exception = Assert.Throws<ArgumentNullException>(() =>
                CareerMatchSettlementRulesV1.Calculate(
                    pending,
                    facts,
                    corrupted,
                    PotentialGrade.B,
                    50,
                    50,
                    50));

            Assert.That(exception.ParamName, Is.EqualTo("currentPlayer"));
        }

        [TestCase(1, true, true)]
        [TestCase(1, false, false)]
        [TestCase(7, true, false)]
        [TestCase(7, false, true)]
        public void Won_IsDerivedFromFrozenRosterSide(
            int protagonistIndex,
            bool homeWinner,
            bool expectedWon)
        {
            var pending = Pending(protagonistIndex: protagonistIndex);
            var facts = Facts(
                pending,
                Protagonist(),
                winnerTeamId: homeWinner ? pending.HomeTeamId : pending.AwayTeamId);

            var summary = Calculate(
                pending,
                facts,
                Player(pending.ProtagonistPlayerId));

            Assert.That(summary.Won, Is.EqualTo(expectedWon));
        }

        [Test]
        public void Calculate_RejectsNullInputsInvalidPotentialAndStatuses()
        {
            var pending = Pending();
            var facts = Facts(pending, Protagonist());
            var player = Player(pending.ProtagonistPlayerId);

            Assert.Throws<ArgumentNullException>(() =>
                CareerMatchSettlementRulesV1.Calculate(null, facts, player, PotentialGrade.B, 50, 50, 50));
            Assert.Throws<ArgumentNullException>(() =>
                CareerMatchSettlementRulesV1.Calculate(pending, null, player, PotentialGrade.B, 50, 50, 50));
            Assert.Throws<ArgumentNullException>(() =>
                CareerMatchSettlementRulesV1.Calculate(pending, facts, null, PotentialGrade.B, 50, 50, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CareerMatchSettlementRulesV1.Calculate(pending, facts, player, (PotentialGrade)99, 50, 50, 50));

            foreach (var invalid in new[] { -1, 101 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CareerMatchSettlementRulesV1.Calculate(
                        pending, facts, player, PotentialGrade.B, invalid, 50, 50));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CareerMatchSettlementRulesV1.Calculate(
                        pending, facts, player, PotentialGrade.B, 50, invalid, 50));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CareerMatchSettlementRulesV1.Calculate(
                        pending, facts, player, PotentialGrade.B, 50, 50, invalid));
            }
        }

        [Test]
        public void Calculate_RejectsAbandonedFacts()
        {
            var pending = Pending();
            var abandoned = Facts(
                pending,
                Protagonist(),
                status: CareerMatchResultStatus.Abandoned);

            Assert.Throws<ArgumentException>(() => Calculate(
                pending,
                abandoned,
                Player(pending.ProtagonistPlayerId)));
        }

        [TestCase("content")]
        [TestCase("rules")]
        public void Calculate_RejectsUnsupportedPendingRuleVersions(string axis)
        {
            var versions = new CareerMatchLifecycleVersions(
                2,
                axis == "content" ? 2 : 1,
                axis == "rules" ? 2 : 1,
                1,
                null,
                null);
            var pending = Pending(versions: versions);
            var facts = Facts(pending, Protagonist());

            Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(
                pending,
                facts,
                Player(pending.ProtagonistPlayerId)));
        }

        [TestCase("content")]
        [TestCase("rules")]
        [TestCase("career-random")]
        [TestCase("simulation")]
        [TestCase("match-random")]
        public void Calculate_RejectsEveryFactsVersionAxisMismatch(string axis)
        {
            var pending = Pending();
            var versions = new CareerMatchVersions(
                2,
                axis == "content" ? 2 : 1,
                axis == "rules" ? 2 : 1,
                axis == "career-random" ? 2 : 1,
                axis == "simulation" ? 1 : (int?)null,
                axis == "match-random" ? 1 : (int?)null);
            var facts = Facts(pending, Protagonist(), versions: versions);

            Assert.Throws<ArgumentException>(() => Calculate(
                pending,
                facts,
                Player(pending.ProtagonistPlayerId)));
        }

        [Test]
        public void Calculate_RejectsSessionAndContextDigestMismatch()
        {
            var pending = Pending();
            var player = Player(pending.ProtagonistPlayerId);
            var wrongSession = Facts(
                pending,
                Protagonist(),
                sessionId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            var wrongContext = Facts(
                pending,
                Protagonist(),
                contextDigest: new Sha256Digest(new string('c', 64)));

            Assert.Throws<ArgumentException>(() => Calculate(pending, wrongSession, player));
            Assert.Throws<ArgumentException>(() => Calculate(pending, wrongContext, player));
        }

        [Test]
        public void Calculate_RejectsReorderedOrReplacedPlayerFacts()
        {
            var pending = Pending();
            var reordered = pending.OrderedPlayerIds.ToArray();
            Swap(reordered, 0, 2);
            var replaced = pending.OrderedPlayerIds.ToArray();
            replaced[11] = new PlayerId("player.replacement");
            var player = Player(pending.ProtagonistPlayerId);

            Assert.Throws<ArgumentException>(() => Calculate(
                pending,
                Facts(pending, Protagonist(), orderedPlayerIds: reordered),
                player));
            Assert.Throws<ArgumentException>(() => Calculate(
                pending,
                Facts(pending, Protagonist(), orderedPlayerIds: replaced),
                player));
        }

        [Test]
        public void Calculate_RejectsCurrentPlayerMismatchAndOutsideWinner()
        {
            var pending = Pending();
            var facts = Facts(pending, Protagonist());

            Assert.Throws<ArgumentException>(() => Calculate(
                pending,
                facts,
                Player(new PlayerId("player.someone-else"))));
            Assert.Throws<ArgumentException>(() => Calculate(
                pending,
                Facts(pending, Protagonist(), winnerTeamId: new TeamId("team.outside")),
                Player(pending.ProtagonistPlayerId)));
        }

        [Test]
        public void MaximumLegalFacts_StayCheckedAndIJsonSafe()
        {
            var emphases = Enum.GetValues(typeof(CareerTrainingDirection))
                .Cast<CareerTrainingDirection>()
                .Select(direction => Emphasis(direction, 1500))
                .ToArray();
            var pending = Pending(priority: CareerMatchPriority.AttackFirst, emphases: emphases);
            var protagonist = Protagonist(
                spike: new CareerSpikeFacts(int.MaxValue, int.MaxValue, 0),
                serve: new CareerServeFacts(int.MaxValue, int.MaxValue, 0),
                reception: new CareerReceptionFacts(
                    int.MaxValue, int.MaxValue, 0, 0, 0, 0),
                defense: new CareerDefenseFacts(int.MaxValue, int.MaxValue),
                block: new CareerBlockFacts(int.MaxValue, int.MaxValue, int.MaxValue),
                load: new CareerMatchLoadFacts(
                    int.MaxValue,
                    MaximumSafeInteger,
                    MaximumSafeInteger,
                    int.MaxValue,
                    int.MaxValue,
                    10000,
                    10000),
                stability: new CareerStabilityFacts(
                    int.MaxValue, int.MaxValue, 0, 0, 0));
            var facts = Facts(
                pending,
                protagonist,
                sets: new[] { new CareerMatchSetScore(1, int.MaxValue, 0, true) },
                rallyCount: int.MaxValue);

            var summary = Calculate(
                pending,
                facts,
                Player(pending.ProtagonistPlayerId),
                PotentialGrade.S);

            Assert.That(summary.GrowthChanges.Select(change => change.RequestedDelta),
                Is.All.InRange(0L, MaximumSafeInteger));
            Assert.That(summary.GrowthChanges.Select(change => change.ActualDelta),
                Is.All.InRange(0L, MaximumSafeInteger));
        }

        [Test]
        public void Calculate_IsDeterministicAndDoesNotMutateInputs()
        {
            var pending = Pending(
                priority: CareerMatchPriority.AttackFirst,
                emphases: new[] { Emphasis(CareerTrainingDirection.Spike, 1000) });
            var facts = Facts(pending, FixtureProtagonistFacts());
            var player = Player(pending.ProtagonistPlayerId);
            var contextBefore = pending.CanonicalContextUtf8;
            var rosterBefore = pending.OrderedPlayerIds.ToArray();

            var first = Calculate(pending, facts, player);
            var second = Calculate(pending, facts, player);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(pending.CanonicalContextUtf8, Is.EqualTo(contextBefore));
            Assert.That(pending.OrderedPlayerIds, Is.EqualTo(rosterBefore));
            Assert.That(player.Attributes.GrowthValues(), Is.EqualTo(new long[8]));
        }

        [Test]
        public void ApplicationAssembly_RemainsEngineAndSharedV2Free()
        {
            var references = typeof(CareerMatchSettlementRulesV1).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Has.None.EqualTo("UnityEngine"));
            Assert.That(references, Has.None.EqualTo("Volleyball.Shared.MatchV2"));
        }

        private static CareerSettlementSummary Calculate(
            PendingCareerMatch pending,
            CareerMatchFacts facts,
            CareerPlayerRecord player,
            PotentialGrade potentialGrade = PotentialGrade.B,
            int fatigue = 50,
            int mindset = 50,
            int coachTrust = 50)
        {
            return CareerMatchSettlementRulesV1.Calculate(
                pending,
                facts,
                player,
                potentialGrade,
                fatigue,
                mindset,
                coachTrust);
        }

        private static PendingCareerMatch Pending(
            CareerMatchPriority priority = CareerMatchPriority.AttackFirst,
            IEnumerable<FrozenCareerTrainingEmphasis> emphases = null,
            CareerMatchLifecycleVersions versions = null,
            int protagonistIndex = 1)
        {
            var players = PlayerIds(protagonistIndex);
            return new PendingCareerMatch(
                SessionId,
                new OperationId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                new LineageId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                9,
                versions ?? new CareerMatchLifecycleVersions(2, 1, 1, 1, null, null),
                CareerMatchLifecycleExecutionMode.Fixture,
                CareerMatchTestData.FixtureId,
                CareerMatchTestData.FixtureVersion,
                CareerMatchTestData.MatchSeed,
                CareerMatchTestData.CompetitionId,
                CareerMatchTestData.ScheduleItemId,
                new WeekPlanId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                new SlotActionId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                new OccurrenceId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
                priority,
                ContextDigest,
                Encoding.UTF8.GetBytes("{}"),
                new TeamId("team.university.first"),
                new TeamId("team.university.rival"),
                players,
                players[protagonistIndex],
                emphases ?? Array.Empty<FrozenCareerTrainingEmphasis>());
        }

        private static CareerMatchFacts Facts(
            PendingCareerMatch pending,
            CareerMatchPlayerFacts protagonist,
            CareerMatchResultStatus status = CareerMatchResultStatus.Completed,
            TeamId? winnerTeamId = null,
            CareerMatchVersions versions = null,
            Guid? sessionId = null,
            Sha256Digest? contextDigest = null,
            IReadOnlyList<PlayerId> orderedPlayerIds = null,
            IReadOnlyList<CareerMatchSetScore> sets = null,
            int? rallyCount = null)
        {
            var ids = orderedPlayerIds ?? pending.OrderedPlayerIds;
            var playerFacts = new CareerMatchPlayerFacts[ids.Count];
            for (var index = 0; index < ids.Count; index++)
            {
                playerFacts[index] = ids[index].Equals(pending.ProtagonistPlayerId)
                    ? WithPlayerId(protagonist, ids[index])
                    : CareerMatchTestData.ZeroFacts(ids[index]);
            }

            var completed = status == CareerMatchResultStatus.Completed;
            var actualSets = sets ?? (completed
                ? new[] { new CareerMatchSetScore(1, 25, 21, true) }
                : Array.Empty<CareerMatchSetScore>());
            var actualRallies = rallyCount ?? (completed ? 46 : 0);
            var actualWinner = completed
                ? winnerTeamId ?? pending.HomeTeamId
                : (TeamId?)null;

            return new CareerMatchFacts(
                versions ?? VersionsFrom(pending.Versions),
                sessionId ?? pending.SessionId,
                contextDigest ?? pending.ContextDigest,
                status,
                actualWinner,
                actualSets,
                actualRallies,
                playerFacts,
                ResultDigest);
        }

        private static CareerMatchVersions VersionsFrom(CareerMatchLifecycleVersions versions)
        {
            return new CareerMatchVersions(
                versions.ContractVersion,
                versions.ContentVersion,
                versions.RulesetVersion,
                versions.CareerRandomAlgorithmVersion,
                versions.MatchSimulationVersion,
                versions.MatchRandomAlgorithmVersion);
        }

        private static CareerMatchPlayerFacts Protagonist(
            CareerSpikeFacts spike = null,
            CareerServeFacts serve = null,
            CareerReceptionFacts reception = null,
            CareerDefenseFacts defense = null,
            CareerBlockFacts block = null,
            CareerMatchLoadFacts load = null,
            CareerStabilityFacts stability = null)
        {
            return new CareerMatchPlayerFacts(
                new PlayerId("player.career.protagonist"),
                spike ?? new CareerSpikeFacts(0, 0, 0),
                serve ?? new CareerServeFacts(0, 0, 0),
                reception ?? new CareerReceptionFacts(0, 0, 0, 0, 0, 0),
                defense ?? new CareerDefenseFacts(0, 0),
                block ?? new CareerBlockFacts(0, 0, 0),
                load ?? new CareerMatchLoadFacts(0, 0, 0, 0, 0, 0, 0),
                stability ?? new CareerStabilityFacts(0, 0, 0, 0, 0));
        }

        private static CareerMatchPlayerFacts FixtureProtagonistFacts()
        {
            return Protagonist(
                new CareerSpikeFacts(12, 7, 1),
                new CareerServeFacts(5, 1, 1),
                new CareerReceptionFacts(8, 3, 2, 1, 1, 1),
                new CareerDefenseFacts(6, 4),
                new CareerBlockFacts(3, 2, 1),
                new CareerMatchLoadFacts(44, 505000, 254000, 28, 9, 5400, 7200),
                new CareerStabilityFacts(5, 3, 1, 1, 2));
        }

        private static CareerMatchPlayerFacts WithPlayerId(
            CareerMatchPlayerFacts source,
            PlayerId playerId)
        {
            return new CareerMatchPlayerFacts(
                playerId,
                source.Spike,
                source.Serve,
                source.Reception,
                source.Defense,
                source.Block,
                source.Load,
                source.Stability);
        }

        private static CareerPlayerRecord Player(
            PlayerId playerId,
            CareerPlayerAttributes attributes = null)
        {
            return new CareerPlayerRecord(
                playerId,
                "Career Player",
                12,
                attributes ?? Attributes());
        }

        private static CareerPlayerAttributes Attributes(
            int abilityBasisPoints = 5000,
            long growthExperience = 0,
            int? staminaAbilityBasisPoints = null)
        {
            var standard = new CareerAttributeProgress(abilityBasisPoints, growthExperience);
            var stamina = new CareerAttributeProgress(
                staminaAbilityBasisPoints ?? abilityBasisPoints,
                growthExperience);
            return new CareerPlayerAttributes(
                standard,
                standard,
                standard,
                standard,
                standard,
                standard,
                standard,
                stamina);
        }

        private static CareerPlayerAttributes DistinctAttributes()
        {
            return new CareerPlayerAttributes(
                new CareerAttributeProgress(4100, 101),
                new CareerAttributeProgress(4200, 202),
                new CareerAttributeProgress(4300, 303),
                new CareerAttributeProgress(4400, 404),
                new CareerAttributeProgress(4500, 505),
                new CareerAttributeProgress(4600, 606),
                new CareerAttributeProgress(4700, 707),
                new CareerAttributeProgress(4800, 808));
        }

        private static FrozenCareerTrainingEmphasis Emphasis(
            CareerTrainingDirection direction,
            int totalBonusBasisPoints)
        {
            var ordinal = (int)direction + 1;
            var sources = new List<SlotActionId>
            {
                new SlotActionId(Guid.Parse(
                    "10000000-0000-0000-0000-" + ordinal.ToString("D12")))
            };
            if (totalBonusBasisPoints == 1500)
            {
                sources.Add(new SlotActionId(Guid.Parse(
                    "20000000-0000-0000-0000-" + ordinal.ToString("D12"))));
            }

            return new FrozenCareerTrainingEmphasis(direction, sources, totalBonusBasisPoints);
        }

        private static PlayerId[] PlayerIds(int protagonistIndex)
        {
            var result = new PlayerId[12];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = index == protagonistIndex
                    ? new PlayerId("player.career.protagonist")
                    : new PlayerId("player.fixture." + (index + 1).ToString("D2"));
            }

            return result;
        }

        private static void AssertRequested(
            CareerSettlementSummary summary,
            params long[] expected)
        {
            Assert.That(summary.GrowthChanges.Select(change => change.RequestedDelta),
                Is.EqualTo(expected));
        }

        private static void AssertFixtureProtagonistFacts(
            CareerProtagonistMatchFacts facts)
        {
            Assert.That(facts.Spike.Attempts, Is.EqualTo(12));
            Assert.That(facts.Spike.Points, Is.EqualTo(7));
            Assert.That(facts.Spike.Errors, Is.EqualTo(1));
            Assert.That(facts.Serve.Attempts, Is.EqualTo(5));
            Assert.That(facts.Serve.Aces, Is.EqualTo(1));
            Assert.That(facts.Serve.Errors, Is.EqualTo(1));
            Assert.That(facts.Reception.Attempts, Is.EqualTo(8));
            Assert.That(facts.Reception.Perfect, Is.EqualTo(3));
            Assert.That(facts.Reception.Positive, Is.EqualTo(2));
            Assert.That(facts.Reception.Neutral, Is.EqualTo(1));
            Assert.That(facts.Reception.Negative, Is.EqualTo(1));
            Assert.That(facts.Reception.Errors, Is.EqualTo(1));
            Assert.That(facts.Defense.Attempts, Is.EqualTo(6));
            Assert.That(facts.Defense.Successes, Is.EqualTo(4));
            Assert.That(facts.Block.Attempts, Is.EqualTo(3));
            Assert.That(facts.Block.EffectiveTouches, Is.EqualTo(2));
            Assert.That(facts.Block.Points, Is.EqualTo(1));
            Assert.That(facts.Load.RalliesPlayed, Is.EqualTo(44));
            Assert.That(facts.Load.ActiveDurationMilliseconds, Is.EqualTo(505000));
            Assert.That(facts.Load.MovementDistanceMillimeters, Is.EqualTo(254000));
            Assert.That(facts.Load.JumpCount, Is.EqualTo(28));
            Assert.That(facts.Load.HighLoadJumpCount, Is.EqualTo(9));
            Assert.That(facts.Load.LandingLoadBasisPoints, Is.EqualTo(5400));
            Assert.That(facts.Load.TotalWorkloadBasisPoints, Is.EqualTo(7200));
            Assert.That(facts.Stability.CriticalActions, Is.EqualTo(5));
            Assert.That(facts.Stability.CriticalSuccesses, Is.EqualTo(3));
            Assert.That(facts.Stability.CriticalErrors, Is.EqualTo(1));
            Assert.That(facts.Stability.ErrorStreakEpisodes, Is.EqualTo(1));
            Assert.That(facts.Stability.LongestErrorStreak, Is.EqualTo(2));
        }

        private static void AssertCompleteGrowthMapping(
            CareerSettlementSummary summary,
            CareerPlayerAttributes beforeAttributes,
            params long[] expectedApplied)
        {
            Assert.That(expectedApplied.Length, Is.EqualTo(8));
            Assert.That(summary.GrowthChanges.Count, Is.EqualTo(8));
            for (var index = 0; index < 8; index++)
            {
                var kind = (CareerAttributeKind)index;
                var before = beforeAttributes.Get(kind);
                var change = summary.GrowthChanges[index];
                Assert.That(change.Attribute, Is.EqualTo(kind));
                Assert.That(change.Before, Is.EqualTo(before));
                Assert.That(change.RequestedDelta, Is.EqualTo(expectedApplied[index]));
                Assert.That(change.ActualDelta, Is.EqualTo(expectedApplied[index]));
                Assert.That(change.After.AbilityBasisPoints,
                    Is.EqualTo(before.AbilityBasisPoints));
                Assert.That(change.After.GrowthExperience,
                    Is.EqualTo(before.GrowthExperience + expectedApplied[index]));
                Assert.That(summary.BeforeAttributes.Get(kind), Is.EqualTo(before));
                Assert.That(summary.AfterAttributes.Get(kind), Is.EqualTo(change.After));
            }

            Assert.That(new[]
            {
                summary.AppliedGrowthExperienceDelta.Spike,
                summary.AppliedGrowthExperienceDelta.Serve,
                summary.AppliedGrowthExperienceDelta.Reception,
                summary.AppliedGrowthExperienceDelta.Defense,
                summary.AppliedGrowthExperienceDelta.Block,
                summary.AppliedGrowthExperienceDelta.Movement,
                summary.AppliedGrowthExperienceDelta.Jump,
                summary.AppliedGrowthExperienceDelta.Stamina
            }, Is.EqualTo(expectedApplied));
        }

        private static void AssertChange(
            CareerReasonedIntegerChange change,
            int oldValue,
            int requested,
            int actual,
            int newValue)
        {
            Assert.That(change.OldValue, Is.EqualTo(oldValue));
            Assert.That(change.RequestedDelta, Is.EqualTo(requested));
            Assert.That(change.ActualDelta, Is.EqualTo(actual));
            Assert.That(change.NewValue, Is.EqualTo(newValue));
        }

        private static void AssertZeroWeekend(
            CareerReasonedIntegerChange change,
            int expectedValue)
        {
            Assert.That(change.ReasonId, Is.EqualTo("reason.weekend.no_numeric_change"));
            AssertChange(change, expectedValue, 0, 0, expectedValue);
        }

        private static void Swap<T>(T[] values, int first, int second)
        {
            var temporary = values[first];
            values[first] = values[second];
            values[second] = temporary;
        }
    }

    internal static class CareerMatchSettlementTestExtensions
    {
        public static long[] GrowthValues(this CareerPlayerAttributes attributes)
        {
            return Enum.GetValues(typeof(CareerAttributeKind))
                .Cast<CareerAttributeKind>()
                .Select(kind => attributes.Get(kind).GrowthExperience)
                .ToArray();
        }
    }
}
