using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using DerivedMatchAttributesV4 = Volleyball.Shared.Contracts.DerivedMatchAttributesV4;
using DominantHandV4 = Volleyball.Shared.Contracts.DominantHandV4;
using MatchAttributeDerivationConfigV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationConfigV4;
using MatchAttributeDerivationV4 = Volleyball.Shared.Contracts.MatchAttributeDerivationV4;
using PhysicalBaseAttributesV4 = Volleyball.Shared.Contracts.PhysicalBaseAttributesV4;
using TechnicalBaseAttributesV4 = Volleyball.Shared.Contracts.TechnicalBaseAttributesV4;

namespace Volleyball.EditModeTests
{
    public sealed class CurrentAbilityBenchmarkTests
    {
        private const int FixedSeed = 73421;

        [Test]
        public void FixedSeed_ReceiveTechniqueReducesAggregateReceiveError()
        {
            AssertTechniqueReducesError(TechniqueAction.Receive, Profile(receiveTechnique: 0.1f), Profile(receiveTechnique: 0.9f));
        }

        [Test]
        public void FixedSeed_SetTechniqueReducesAggregateSetError()
        {
            AssertTechniqueReducesError(TechniqueAction.Set, Profile(setTechnique: 0.1f), Profile(setTechnique: 0.9f));
        }

        [Test]
        public void FixedSeed_AttackTechniqueReducesAggregateAttackError()
        {
            AssertTechniqueReducesError(TechniqueAction.Attack, Profile(attackTechnique: 0.1f), Profile(attackTechnique: 0.9f));
        }

        [Test]
        public void FixedSeed_HigherReactionReducesAggregateReactionDelay()
        {
            var slow = AggregateReactionDelay(Profile(reaction: 0.1f));
            var fast = AggregateReactionDelay(Profile(reaction: 0.9f));

            Assert.That(AggregateReactionDelay(Profile(reaction: 0.1f)), Is.EqualTo(slow));
            Assert.That(fast, Is.LessThan(slow));
        }

        [Test]
        public void FixedInput_HigherMobilityIncreasesPlannerReachability()
        {
            var low = ReachabilityFor(Profile(mobility: 0.1f));
            var high = ReachabilityFor(Profile(mobility: 0.9f));

            Assert.That(ReachabilityFor(Profile(mobility: 0.1f)), Is.EqualTo(low));
            Assert.That(high, Is.GreaterThan(low));
        }

        [TestCase(RallyDecisionStage.Receive, ExecutionCandidateCategoryV4.Receive)]
        [TestCase(RallyDecisionStage.Organize, ExecutionCandidateCategoryV4.Set)]
        public void FixedInput_MovementRaisesReachWithoutChangingControlErrorBounds(
            RallyDecisionStage stage,
            ExecutionCandidateCategoryV4 category)
        {
            var low = Profile(mobility: 0.1f);
            var high = Profile(mobility: 0.9f);
            var lowEnvelope = Envelope(low, category);
            var highEnvelope = Envelope(high, category);

            Assert.That(
                ReachMargin(low, stage),
                Is.LessThan(ReachMargin(high, stage)));
            Assert.That(
                highEnvelope.TargetError,
                Is.EqualTo(lowEnvelope.TargetError));
            Assert.That(
                highEnvelope.VelocityError,
                Is.EqualTo(lowEnvelope.VelocityError));
            Assert.That(
                highEnvelope.MaximumVelocity.Magnitude,
                Is.GreaterThan(lowEnvelope.MaximumVelocity.Magnitude));
        }

        [Test]
        public void FixedInput_FirstTouchControlReducesReceiveErrorsWithoutChangingReach()
        {
            var low = Profile(receiveTechnique: 0.1f);
            var high = Profile(receiveTechnique: 0.9f);
            var lowEnvelope = Envelope(
                low,
                ExecutionCandidateCategoryV4.Receive);
            var highEnvelope = Envelope(
                high,
                ExecutionCandidateCategoryV4.Receive);

            Assert.That(
                ReachMargin(high, RallyDecisionStage.Receive),
                Is.EqualTo(ReachMargin(low, RallyDecisionStage.Receive)));
            Assert.That(
                highEnvelope.TargetError.MaximumAbsoluteError.Magnitude,
                Is.LessThan(
                    lowEnvelope.TargetError.MaximumAbsoluteError.Magnitude));
            Assert.That(
                highEnvelope.VelocityError.MaximumAbsoluteError.Magnitude,
                Is.LessThan(
                    lowEnvelope.VelocityError.MaximumAbsoluteError.Magnitude));
            Assert.That(
                highEnvelope.MaximumVelocity,
                Is.EqualTo(lowEnvelope.MaximumVelocity));
        }

        [Test]
        public void FixedInput_SetControlChangesOnlySetErrorBounds()
        {
            var low = Profile(setTechnique: 0.1f);
            var high = Profile(setTechnique: 0.9f);
            var lowEnvelope = Envelope(low, ExecutionCandidateCategoryV4.Set);
            var highEnvelope = Envelope(high, ExecutionCandidateCategoryV4.Set);

            Assert.That(
                ReachMargin(high, RallyDecisionStage.Organize),
                Is.EqualTo(ReachMargin(low, RallyDecisionStage.Organize)));
            Assert.That(
                highEnvelope.TargetError.MaximumAbsoluteError.Magnitude,
                Is.LessThan(
                    lowEnvelope.TargetError.MaximumAbsoluteError.Magnitude));
            Assert.That(
                highEnvelope.VelocityError.MaximumAbsoluteError.Magnitude,
                Is.LessThan(
                    lowEnvelope.VelocityError.MaximumAbsoluteError.Magnitude));
            Assert.That(
                highEnvelope.MaximumVelocity,
                Is.EqualTo(lowEnvelope.MaximumVelocity));
        }

        [Test]
        public void FixedInput_HigherAttackPowerDoesNotChangeAttackDecisionScore()
        {
            var lowAbility = Profile(attackPower: 0.1f);
            var highAbility = Profile(attackPower: 0.9f);
            var low = AttackDecisionFor(lowAbility);
            var high = AttackDecisionFor(highAbility);

            Assert.That(AttackDecisionFor(lowAbility).Score.Total, Is.EqualTo(low.Score.Total));
            Assert.That(highAbility.AttackPowerCapacity, Is.GreaterThan(lowAbility.AttackPowerCapacity));
            Assert.That(high.Score.Total, Is.EqualTo(low.Score.Total));
        }

        [Test]
        public void FixedInput_HigherJumpIncreasesAttackApproachScore()
        {
            var low = AttackDecisionFor(Profile(jump: 0.1f));
            var high = AttackDecisionFor(Profile(jump: 0.9f));

            Assert.That(AttackDecisionFor(Profile(jump: 0.1f)).Score.Approach, Is.EqualTo(low.Score.Approach));
            Assert.That(high.Score.Approach, Is.GreaterThan(low.Score.Approach));
        }

        [Test]
        public void FixedInput_HigherMaxAttackReachRaisesPlannerContactHeight()
        {
            var low = AttackDecisionFor(Profile(maxAttackReach: 3.20f));
            var high = AttackDecisionFor(Profile(maxAttackReach: 3.55f));

            Assert.That(AttackDecisionFor(Profile(maxAttackReach: 3.20f)).AttackContactPlan,
                Is.EqualTo(low.AttackContactPlan));
            Assert.That(high.AttackContactPlan.Value.ContactCenter.Y,
                Is.GreaterThan(low.AttackContactPlan.Value.ContactCenter.Y));
        }

        private static void AssertTechniqueReducesError(
            TechniqueAction action,
            PlayerAbilityProfile lowProfile,
            PlayerAbilityProfile highProfile)
        {
            var low = AggregateError(lowProfile, action);
            var high = AggregateError(highProfile, action);

            Assert.That(AggregateError(lowProfile, action), Is.EqualTo(low));
            Assert.That(high, Is.LessThan(low));
        }

        private static float AggregateError(PlayerAbilityProfile profile, TechniqueAction action)
        {
            var total = 0f;
            for (var offset = 0; offset < 32; offset++)
            {
                total += SkillExecutionResolver.Resolve(
                    profile,
                    action,
                    playerStableId: 5,
                    rallyNumber: 11,
                    actionIndex: 2,
                    seed: FixedSeed + offset,
                    difficulty: 0.75f).Magnitude;
            }

            return total;
        }

        private static float AggregateReactionDelay(PlayerAbilityProfile profile)
        {
            var total = 0f;
            for (var offset = 0; offset < 32; offset++)
            {
                total += SkillExecutionResolver.Resolve(
                    profile,
                    TechniqueAction.Receive,
                    playerStableId: 5,
                    rallyNumber: 11,
                    actionIndex: 2,
                    seed: FixedSeed + offset,
                    difficulty: 0.75f).ReactionDelay;
            }

            return total;
        }

        private static float ReachabilityFor(PlayerAbilityProfile attackerAbility)
        {
            var decision = new TeamRallyDecisionPlanner(FixedSeed).Plan(CreateAttackInput(attackerAbility));
            return CandidateFor(decision, PlayerRole.Attacker).Score.Reachability;
        }

        private static float ReachMargin(
            PlayerAbilityProfile ability,
            RallyDecisionStage stage)
        {
            var actor = new PlayerId(TeamId.Blue, PlayerRole.Attacker);
            var input = new TeamRallyDecisionInput(
                TeamId.Blue,
                CreateAttackInput(ability).Tactic,
                new[]
                {
                    new RallyPlayerSnapshot(
                        actor,
                        new SimVector3(0f, 0f, -4f),
                        ability),
                    new RallyPlayerSnapshot(
                        new PlayerId(TeamId.Blue, PlayerRole.Setter),
                        new SimVector3(8f, 0f, -5f),
                        ability),
                    new RallyPlayerSnapshot(
                        new PlayerId(TeamId.Blue, PlayerRole.Defender),
                        new SimVector3(8f, 0f, -4f),
                        ability)
                },
                new SimVector3(2.5f, 2f, -1.5f),
                0.5f,
                5f,
                stage == RallyDecisionStage.Receive ? 0 : 1,
                null,
                0,
                0,
                stage,
                RallyTacticalWeights.Default);
            return new TeamRallyDecisionPlanner(FixedSeed)
                .OrderedCandidates(input)
                .Single(candidate => candidate.Actor.Equals(actor))
                .Score.Reachability;
        }

        private static ExecutionEnvelopeV4 Envelope(
            PlayerAbilityProfile ability,
            ExecutionCandidateCategoryV4 category)
        {
            return ExecutionEnvelopeFactoryV4.Create(
                ability.Derived,
                new ExecutionIntentV4(
                    "gate-h-ability-" + category,
                    category,
                    new SimVector3(1f, 2f, 3f),
                    new SimVector3(1f, 1f, 1f),
                    0.1f),
                "gate-h-fixed-key-" + category,
                ExecutionEnvelopePolicyV4.Default);
        }

        [Test]
        public void FixedKey_SoftTouchChangesOnlySoftActionError()
        {
            var low = GateIDerived(softTouch: 0.1f);
            var high = GateIDerived(softTouch: 0.9f);
            var lowSoft = GateIEnvelope(low, ExecutionCandidateCategoryV4.SoftAction);
            var highSoft = GateIEnvelope(high, ExecutionCandidateCategoryV4.SoftAction);

            Assert.That(
                highSoft.TargetError.MaximumAbsoluteError.Magnitude,
                Is.LessThan(lowSoft.TargetError.MaximumAbsoluteError.Magnitude));
            Assert.That(
                GateIEnvelope(high, ExecutionCandidateCategoryV4.Attack).TargetError,
                Is.EqualTo(GateIEnvelope(low, ExecutionCandidateCategoryV4.Attack).TargetError));
        }

        [Test]
        public void FixedKey_DefensePlatformChangesDefenseErrorNotCapacity()
        {
            var low = GateIDerived(defenseTechnique: 0.1f);
            var high = GateIDerived(defenseTechnique: 0.9f);
            var lowDefense = GateIEnvelope(low, ExecutionCandidateCategoryV4.Defense);
            var highDefense = GateIEnvelope(high, ExecutionCandidateCategoryV4.Defense);

            Assert.That(
                highDefense.TargetError.MaximumAbsoluteError.Magnitude,
                Is.LessThan(lowDefense.TargetError.MaximumAbsoluteError.Magnitude));
            Assert.That(highDefense.MaximumVelocity, Is.EqualTo(lowDefense.MaximumVelocity));
        }

        private static ExecutionEnvelopeV4 GateIEnvelope(
            DerivedMatchAttributesV4 derived,
            ExecutionCandidateCategoryV4 category)
        {
            return ExecutionEnvelopeFactoryV4.Create(
                derived,
                new ExecutionIntentV4(
                    "gate-i-benchmark-" + category,
                    category,
                    new SimVector3(1f, 2f, 3f),
                    new SimVector3(1f, 1f, 1f),
                    0.1f),
                "gate-i-benchmark-key-" + category,
                ExecutionEnvelopePolicyV4.GateI);
        }

        private static DerivedMatchAttributesV4 GateIDerived(
            float softTouch = .72f,
            float defenseTechnique = .72f)
        {
            return MatchAttributeDerivationV4.Derive(
                new PhysicalBaseAttributesV4(1.91f, 2.43f, .73f, .71f, .72f, .70f),
                new TechnicalBaseAttributesV4(.76f, .77f, .72f, defenseTechnique, .74f, .75f, .76f, softTouch, .78f),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
        }

        private static TeamRallyDecision AttackDecisionFor(PlayerAbilityProfile attackerAbility)
        {
            return new TeamRallyDecisionPlanner(FixedSeed).Plan(CreateAttackInput(attackerAbility));
        }

        private static TeamRallyDecisionInput CreateAttackInput(PlayerAbilityProfile attackerAbility)
        {
            var tactic = new TeamRallyTactic(
                SetRoute.LeftPin,
                SpikeRoute.Line,
                new CourtPoint(0f, -3.35f),
                new CourtPoint(2f, -2.45f),
                new CourtPoint(0f, -5.25f),
                new BlockCoveragePlan(
                    PlayerRole.Attacker,
                    new CourtPoint(0f, -0.65f),
                    PlayerRole.Setter,
                    new CourtPoint(0f, -4.15f)),
                SetRhythm.FastPin,
                0.45f);
            return new TeamRallyDecisionInput(
                TeamId.Blue,
                tactic,
                new List<RallyPlayerSnapshot>
                {
                    new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Setter), new SimVector3(8f, 0f, -5f), Profile()),
                    new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Attacker), new SimVector3(2f, 0f, -2.45f), attackerAbility),
                    new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Defender), new SimVector3(7f, 0f, -5f), Profile())
                },
                new SimVector3(0f, 3f, -1f),
                availableSeconds: 2f,
                baseMovementSpeed: 5f,
                countedTouches: 1,
                lastCountedActor: new PlayerId(TeamId.Blue, PlayerRole.Setter),
                tacticRevision: 0,
                decisionIndex: 0,
                stage: RallyDecisionStage.Attack,
                weights: RallyTacticalWeights.Default);
        }

        private static RallyDecisionCandidate CandidateFor(TeamRallyDecision decision, PlayerRole role)
        {
            for (var index = 0; index < decision.Candidates.Count; index++)
            {
                if (decision.Candidates[index].Actor.Role == role)
                {
                    return decision.Candidates[index];
                }
            }

            throw new AssertionException("Expected candidate role was not present.");
        }

        private static PlayerAbilityProfile Profile(
            float mobility = 0.8f,
            float reaction = 0.8f,
            float jump = 0.8f,
            float receiveTechnique = 0.8f,
            float setTechnique = 0.8f,
            float attackTechnique = 0.8f,
            float attackPower = 0.8f,
            float maxAttackReach = 3.42f)
        {
            return MatchV4TestFixture.CreateAbility(
                mobility,
                reaction,
                jump,
                receiveTechnique,
                setTechnique,
                attackTechnique,
                attackPower,
                maxAttackReach);
        }
    }
}
