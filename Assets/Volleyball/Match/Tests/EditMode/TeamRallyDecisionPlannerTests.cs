using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;

namespace Volleyball.EditModeTests
{
    public sealed class TeamRallyDecisionPlannerTests
    {
        [Test]
        public void Plan_ReceivePrefersTheDefenderWhenReachabilityIsEqual()
        {
            var input = CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Receive,
                new SimVector3(0f, 2f, -2f),
                new SimVector3(0f, 0f, -2f),
                new SimVector3(0f, 0f, -2f),
                new SimVector3(0f, 0f, -2f));

            var decision = new TeamRallyDecisionPlanner(17).Plan(input);

            Assert.That(decision.HasDecision, Is.True);
            Assert.That(decision.Actor, Is.EqualTo(new PlayerId(TeamId.Blue, PlayerRole.Defender)));
            Assert.That(decision.Action, Is.EqualTo(TechniqueAction.Receive));
            Assert.That(decision.MovementTarget.Y, Is.Zero);
            Assert.That(decision.Score.Reachability, Is.GreaterThan(0f));
        }

        [Test]
        public void Plan_OrganizeUsesReachableDefenderWhenTheSetterCannotArrive()
        {
            var input = CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Organize,
                new SimVector3(0f, 2f, -2f),
                new SimVector3(20f, 0f, -2f),
                new SimVector3(1f, 0f, -2f),
                new SimVector3(0f, 0f, -2f),
                availableSeconds: 0.5f);

            var decision = new TeamRallyDecisionPlanner(17).Plan(input);

            Assert.That(decision.HasDecision, Is.True);
            Assert.That(decision.Actor, Is.EqualTo(new PlayerId(TeamId.Blue, PlayerRole.Defender)));
            Assert.That(decision.Action, Is.EqualTo(TechniqueAction.Set));
        }

        [Test]
        public void Plan_AttackUsesReachableDefenderWhenTheAttackerCannotArrive()
        {
            var tactic = CreateTactic(SpikeRoute.Line);
            var takeoff = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            var input = CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Attack,
                new SimVector3(0f, 3f, -1f),
                new SimVector3(8f, 0f, -5f),
                new SimVector3(20f, 0f, -2f),
                takeoff,
                tactic: tactic,
                availableSeconds: 0.5f);

            var decision = new TeamRallyDecisionPlanner(17).Plan(input);

            Assert.That(decision.HasDecision, Is.True);
            Assert.That(decision.Actor, Is.EqualTo(new PlayerId(TeamId.Blue, PlayerRole.Defender)));
            Assert.That(decision.Action, Is.EqualTo(TechniqueAction.Attack));
            Assert.That(decision.ContactTarget, Is.EqualTo(takeoff));
            Assert.That(decision.AttackApproach.HasValue, Is.True);
        }

        [Test]
        public void Plan_ExcludesTheLastCountedActor()
        {
            var setter = new PlayerId(TeamId.Blue, PlayerRole.Setter);
            var input = CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Organize,
                new SimVector3(0f, 2f, -2f),
                new SimVector3(0f, 0f, -2f),
                new SimVector3(0f, 0f, -2f),
                new SimVector3(0f, 0f, -2f),
                lastCountedActor: setter);

            var decision = new TeamRallyDecisionPlanner(17).Plan(input);

            Assert.That(decision.HasDecision, Is.True);
            Assert.That(decision.Actor, Is.Not.EqualTo(setter));
        }

        [Test]
        public void Plan_ReceiveDoesNotExcludeThePreviousPossessionActor()
        {
            var defender = new PlayerId(TeamId.Blue, PlayerRole.Defender);
            var input = CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Receive,
                new SimVector3(0f, 2f, -2f),
                new SimVector3(0f, 0f, -2f),
                new SimVector3(0f, 0f, -2f),
                new SimVector3(0f, 0f, -2f),
                lastCountedActor: defender);

            var decision = new TeamRallyDecisionPlanner(17).Plan(input);

            Assert.That(decision.Actor, Is.EqualTo(defender));
        }

        [Test]
        public void Plan_ReturnsNoDecisionWhenEveryEligiblePlayerIsUnreachable()
        {
            var input = CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Receive,
                new SimVector3(0f, 2f, -2f),
                new SimVector3(20f, 0f, -2f),
                new SimVector3(21f, 0f, -2f),
                new SimVector3(22f, 0f, -2f),
                availableSeconds: 0.3f);

            var decision = new TeamRallyDecisionPlanner(17).Plan(input);

            Assert.That(decision, Is.SameAs(TeamRallyDecision.NoDecision));
            Assert.That(decision.HasDecision, Is.False);
        }

        [Test]
        public void Plan_BlockReturnsTheSharedNoDecision()
        {
            var decision = new TeamRallyDecisionPlanner(17).Plan(CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Block,
                new SimVector3(0f, 2f, -2f),
                new SimVector3(0f, 0f, -2f),
                new SimVector3(1f, 0f, -2f),
                new SimVector3(2f, 0f, -2f)));

            Assert.That(decision, Is.SameAs(TeamRallyDecision.NoDecision));
        }

        [Test]
        public void Plan_AttackRoutesToTheOpponentDepthAndVariesLandingAcrossRoutes()
        {
            var line = new TeamRallyDecisionPlanner(17).Plan(CreateOrangeAttackInput(SpikeRoute.Line));
            var cross = new TeamRallyDecisionPlanner(17).Plan(CreateOrangeAttackInput(SpikeRoute.CrossCourt));

            Assert.That(line.HasDecision, Is.True);
            Assert.That(line.BallTarget.Z, Is.LessThan(0f));
            Assert.That(cross.BallTarget.Z, Is.LessThan(0f));
            Assert.That(cross.BallTarget.X, Is.Not.EqualTo(line.BallTarget.X).Within(0.00001f));
        }

        [Test]
        public void Plan_AttackApproachQualityIncreasesToItsCapAndAnglePenaltyGrowsForCrossCourt()
        {
            var shortApproach = new TeamRallyDecisionPlanner(17).Plan(CreateAttackInput(SpikeRoute.Line, 0f));
            var cappedApproach = new TeamRallyDecisionPlanner(17).Plan(CreateAttackInput(SpikeRoute.Line, 1f));
            var crossCourt = new TeamRallyDecisionPlanner(17).Plan(CreateAttackInput(SpikeRoute.CrossCourt, 1f));

            Assert.That(cappedApproach.AttackApproach.Value.JumpQuality,
                Is.GreaterThan(shortApproach.AttackApproach.Value.JumpQuality));
            Assert.That(cappedApproach.AttackApproach.Value.JumpQuality, Is.EqualTo(1f).Within(0.00001f));
            var frame = new TeamCourtFrame(TeamId.Blue);
            Assert.That(frame.ToLocal(cappedApproach.AttackApproach.Value.ApproachStart).Z,
                Is.LessThan(frame.ToLocal(cappedApproach.AttackApproach.Value.Takeoff).Z));
            Assert.That(crossCourt.AttackApproach.Value.AnglePenalty,
                Is.GreaterThan(cappedApproach.AttackApproach.Value.AnglePenalty));
        }

        private static TeamRallyDecisionInput CreateOrangeAttackInput(SpikeRoute route)
        {
            var tactic = CreateTactic(route, TeamId.Orange);
            var takeoff = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            return CreateInput(
                TeamId.Orange,
                RallyDecisionStage.Attack,
                new SimVector3(0f, 3f, 1f),
                new SimVector3(6f, 0f, 5f),
                new SimVector3(7f, 0f, 5f),
                takeoff,
                tactic: tactic,
                availableSeconds: 2f);
        }

        private static TeamRallyDecisionInput CreateAttackInput(SpikeRoute route, float attackerMobility)
        {
            var tactic = CreateTactic(route);
            var takeoff = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            return CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Attack,
                new SimVector3(0f, 3f, -1f),
                new SimVector3(8f, 0f, -5f),
                takeoff,
                new SimVector3(7f, 0f, -5f),
                tactic: tactic,
                availableSeconds: 2f,
                attackerAbility: Ability(attackerMobility));
        }

        private static TeamRallyDecisionInput CreateInput(
            TeamId team,
            RallyDecisionStage stage,
            SimVector3 ball,
            SimVector3 setterPosition,
            SimVector3 attackerPosition,
            SimVector3 defenderPosition,
            TeamRallyTactic tactic = default,
            float availableSeconds = 2f,
            PlayerId? lastCountedActor = null,
            PlayerAbilityProfile attackerAbility = default)
        {
            if (tactic.Equals(default(TeamRallyTactic)))
            {
                tactic = CreateTactic(SpikeRoute.Line, team);
            }

            if (attackerAbility.Equals(default(PlayerAbilityProfile)))
            {
                attackerAbility = Ability(0.8f);
            }

            var players = new List<RallyPlayerSnapshot>
            {
                new RallyPlayerSnapshot(new PlayerId(team, PlayerRole.Setter), setterPosition, Ability(0.8f)),
                new RallyPlayerSnapshot(new PlayerId(team, PlayerRole.Attacker), attackerPosition, attackerAbility),
                new RallyPlayerSnapshot(new PlayerId(team, PlayerRole.Defender), defenderPosition, Ability(0.8f))
            };
            return new TeamRallyDecisionInput(
                team,
                tactic,
                players,
                ball,
                availableSeconds,
                5f,
                0,
                lastCountedActor,
                0,
                0,
                stage,
                RallyTacticalWeights.Default);
        }

        private static TeamRallyTactic CreateTactic(SpikeRoute route, TeamId team = TeamId.Blue)
        {
            var sign = new TeamCourtFrame(team).WorldDepthSign;
            return new TeamRallyTactic(
                SetRoute.LeftPin,
                route,
                new CourtPoint(0f, sign * 3.35f),
                new CourtPoint(2f, sign * 2.45f),
                new CourtPoint(0f, sign * 5.25f),
                new BlockCoveragePlan(
                    PlayerRole.Attacker,
                    new CourtPoint(0f, sign * 0.65f),
                    PlayerRole.Setter,
                    new CourtPoint(0f, sign * 4.15f)),
                0.8f,
                0.45f);
        }

        private static PlayerAbilityProfile Ability(float mobility)
        {
            return new PlayerAbilityProfile(mobility, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f);
        }
    }
}
