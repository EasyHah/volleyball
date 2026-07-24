using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

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

        [Test]
        public void FixedInput_HigherAttackPowerIncreasesAttackDecisionScore()
        {
            var low = AttackDecisionFor(Profile(attackPower: 0.1f));
            var high = AttackDecisionFor(Profile(attackPower: 0.9f));

            Assert.That(AttackDecisionFor(Profile(attackPower: 0.1f)).Score.Total, Is.EqualTo(low.Score.Total));
            Assert.That(high.Score.Total, Is.GreaterThan(low.Score.Total));
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
            return new PlayerAbilityProfile(
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
