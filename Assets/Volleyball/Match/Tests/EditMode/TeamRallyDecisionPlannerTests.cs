using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Presentation;

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
        public void Plan_SixPlayerRosterSupportsDistinctSlotsAndDuplicateRoles()
        {
            var players = new[]
            {
                Snapshot(PlayerRole.Opposite, 0, -3f),
                Snapshot(PlayerRole.OutsideHitter, 1, -2f),
                Snapshot(PlayerRole.MiddleBlocker, 2, -1f),
                Snapshot(PlayerRole.Setter, 3, 1f),
                Snapshot(PlayerRole.OutsideHitter, 4, 2f),
                Snapshot(PlayerRole.Defender, 5, 0f)
            };

            var decision = new TeamRallyDecisionPlanner(17).Plan(CreateRawInput(players));

            Assert.That(decision.HasDecision, Is.True);
            Assert.That(decision.Candidates, Has.Count.EqualTo(6));
            Assert.That(decision.Actor.Role, Is.EqualTo(PlayerRole.Setter));
            Assert.That(decision.Actor.RosterSlot, Is.EqualTo(3));
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
        public void Plan_OrganizeReachableSetterWinsEvenWhenBackupHasTheHigherScore()
        {
            var setter = new PlayerId(TeamId.Blue, PlayerRole.Setter);
            var defender = new PlayerId(TeamId.Blue, PlayerRole.Defender);
            var decision = new TeamRallyDecisionPlanner(17).Plan(CreateOrganizeInput(
                new RallyPlayerSnapshot(setter, new SimVector3(1f, 0f, -2f), Ability(0f)),
                new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Attacker), new SimVector3(3f, 0f, -2f), Ability(0.8f)),
                new RallyPlayerSnapshot(defender, new SimVector3(0f, 0f, -2f), Ability(1f)),
                availableSeconds: 2f));

            var defenderCandidate = FindCandidate(decision, PlayerRole.Defender);
            var setterCandidate = FindCandidate(decision, PlayerRole.Setter);
            Assert.That(defenderCandidate.Score.Total, Is.GreaterThan(setterCandidate.Score.Total));
            Assert.That(decision.Actor, Is.EqualTo(setter));
        }

        [Test]
        public void Plan_OrganizeFallsBackWhenTheSetterIsUnreachable()
        {
            var setter = new PlayerId(TeamId.Blue, PlayerRole.Setter);
            var defender = new PlayerId(TeamId.Blue, PlayerRole.Defender);
            var decision = new TeamRallyDecisionPlanner(17).Plan(CreateOrganizeInput(
                new RallyPlayerSnapshot(setter, new SimVector3(20f, 0f, -2f), Ability(0.8f)),
                new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Attacker), new SimVector3(3f, 0f, -2f), Ability(0.8f)),
                new RallyPlayerSnapshot(defender, new SimVector3(0f, 0f, -2f), Ability(0.8f)),
                availableSeconds: 0.5f));

            Assert.That(FindCandidate(decision, PlayerRole.Setter).IsFeasible, Is.False);
            Assert.That(decision.Actor, Is.EqualTo(defender));
        }

        [Test]
        public void Plan_OrganizeFallsBackWhenTheSetterIsExcludedByThePreviousTouch()
        {
            var setter = new PlayerId(TeamId.Blue, PlayerRole.Setter);
            var defender = new PlayerId(TeamId.Blue, PlayerRole.Defender);
            var decision = new TeamRallyDecisionPlanner(17).Plan(CreateOrganizeInput(
                new RallyPlayerSnapshot(setter, new SimVector3(0f, 0f, -2f), Ability(0.8f)),
                new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Attacker), new SimVector3(3f, 0f, -2f), Ability(0.8f)),
                new RallyPlayerSnapshot(defender, new SimVector3(0f, 0f, -2f), Ability(0.8f)),
                lastCountedActor: setter));

            Assert.That(FindCandidate(decision, PlayerRole.Setter).IsFeasible, Is.False);
            Assert.That(decision.Actor, Is.EqualTo(defender));
        }

        [TestCase(TeamId.Blue)]
        [TestCase(TeamId.Orange)]
        public void Plan_OrganizeTargetsTheFutureAttackerContactPointInTheTeamCourtFrame(TeamId team)
        {
            var tactic = CreateTactic(SpikeRoute.Line, team);
            var currentBall = new SimVector3(-1.5f, 2.1f, 0.35f * new TeamCourtFrame(team).WorldDepthSign);
            var decision = new TeamRallyDecisionPlanner(17).Plan(CreateInput(
                team,
                RallyDecisionStage.Organize,
                currentBall,
                new SimVector3(0f, 0f, tactic.SetterPosition.Z),
                new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z),
                new SimVector3(1f, 0f, tactic.DefenderPosition.Z),
                tactic: tactic));

            var expectedTakeoff = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            Assert.That(decision.BallTarget.X, Is.EqualTo(expectedTakeoff.X).Within(0.00001f));
            Assert.That(decision.BallTarget.Z, Is.EqualTo(expectedTakeoff.Z).Within(0.00001f));
            Assert.That(decision.BallTarget.Z, Is.Not.EqualTo(currentBall.Z).Within(0.00001f));
            Assert.That(decision.BallTarget.Y, Is.GreaterThan(expectedTakeoff.Y));
            Assert.That(new TeamCourtFrame(team).ToLocal(decision.BallTarget).Z,
                Is.EqualTo(new TeamCourtFrame(team).ToLocal(expectedTakeoff).Z).Within(0.00001f));
        }

        [Test]
        public void SetterPreparedFacing_IsIdenticalLocallyAndMirroredInWorldDepth()
        {
            var blueFrame = new TeamCourtFrame(TeamId.Blue);
            var orangeFrame = new TeamCourtFrame(TeamId.Orange);
            var blueWorld = PrototypePlayerAgent.PreparedForwardFor(blueFrame);
            var orangeWorld = PrototypePlayerAgent.PreparedForwardFor(orangeFrame);
            var blueLocal = blueFrame.ToLocal(blueWorld);
            var orangeLocal = orangeFrame.ToLocal(orangeWorld);

            Assert.That(blueLocal, Is.EqualTo(orangeLocal));
            Assert.That(blueLocal.X, Is.LessThan(0f));
            Assert.That(blueLocal.Z, Is.GreaterThan(0f));
            Assert.That(blueWorld.X, Is.EqualTo(orangeWorld.X).Within(0.00001f));
            Assert.That(blueWorld.Z, Is.EqualTo(-orangeWorld.Z).Within(0.00001f));
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
            Assert.That(decision.ContactTarget, Is.EqualTo(decision.AttackContactPlan.Value.ContactCenter));
            Assert.That(decision.AttackApproach.HasValue, Is.True);
            Assert.That(decision.AttackContactPlan.HasValue, Is.True);
            Assert.That(decision.AttackContactPlan.Value.Takeoff, Is.EqualTo(takeoff));
            Assert.That(decision.AttackContactPlan.Value.ContactCenter.Y, Is.InRange(3.20f, 3.55f));
        }

        [Test]
        public void Plan_AttackSelectionIsIndependentOfExecutionControlAndPowerCapacity()
        {
            var lowExecutionRatings = AbilityWithAttack(attackTechnique: 0.2f, attackPower: 0.2f);
            var highExecutionRatings = AbilityWithAttack(attackTechnique: 0.95f, attackPower: 0.95f);
            var firstLow = new TeamRallyDecisionPlanner(17).Plan(
                CreateAttackSelectionInput(lowExecutionRatings, highExecutionRatings));
            var firstHigh = new TeamRallyDecisionPlanner(17).Plan(
                CreateAttackSelectionInput(highExecutionRatings, lowExecutionRatings));

            Assert.That(
                highExecutionRatings.AttackDirectionControl,
                Is.GreaterThan(lowExecutionRatings.AttackDirectionControl));
            Assert.That(
                highExecutionRatings.AttackSpeedControl,
                Is.GreaterThan(lowExecutionRatings.AttackSpeedControl));
            Assert.That(
                highExecutionRatings.AttackPowerCapacity,
                Is.GreaterThan(lowExecutionRatings.AttackPowerCapacity));
            Assert.That(firstHigh.Actor, Is.EqualTo(firstLow.Actor));
            Assert.That(firstLow.Candidates[0].Score, Is.EqualTo(firstHigh.Candidates[0].Score));
            Assert.That(firstLow.Candidates[1].Score, Is.EqualTo(firstHigh.Candidates[1].Score));
            Assert.That(firstLow.Candidates[0].Score, Is.EqualTo(firstLow.Candidates[1].Score));
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

        [Test]
        public void Plan_AttackApproachQualityIsLimitedByTimeRemainingAfterReachingTakeoff()
        {
            var tactic = CreateTactic(SpikeRoute.Line);
            var takeoff = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            var shortTime = new TeamRallyDecisionPlanner(17).Plan(CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Attack,
                new SimVector3(0f, 3f, -1f),
                new SimVector3(9f, 0f, -5f),
                takeoff,
                new SimVector3(8f, 0f, -5f),
                tactic: tactic,
                availableSeconds: 0.25f));
            var ampleTime = new TeamRallyDecisionPlanner(17).Plan(CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Attack,
                new SimVector3(0f, 3f, -1f),
                new SimVector3(9f, 0f, -5f),
                takeoff,
                new SimVector3(8f, 0f, -5f),
                tactic: tactic,
                availableSeconds: 2f));

            Assert.That(shortTime.HasDecision, Is.True);
            Assert.That(shortTime.AttackApproach.Value.JumpQuality, Is.LessThan(0.1f));
            Assert.That(ampleTime.AttackApproach.Value.JumpQuality,
                Is.GreaterThan(shortTime.AttackApproach.Value.JumpQuality));
            Assert.That(ampleTime.AttackApproach.Value.JumpQuality, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void AttackApproachStaging_AdvancesQuickHitterBeforeTheSetContact()
        {
            var approach = new AttackApproachPlan(
                new SimVector3(0f, 0f, -3.2f),
                new SimVector3(0f, 0f, -2f),
                1.2f,
                1f,
                0f);

            var target = AttackApproachStaging.TargetAtSetContact(
                approach,
                0.425f,
                6f,
                0.38f);

            Assert.That(target.Z, Is.EqualTo(-2.27f).Within(0.00001f));
            Assert.That(target.X, Is.Zero.Within(0.00001f));
        }

        [Test]
        public void AttackApproachStaging_LeavesSlowHitterAtTheApproachStart()
        {
            var approach = new AttackApproachPlan(
                new SimVector3(0f, 0f, -3.2f),
                new SimVector3(0f, 0f, -2f),
                1.2f,
                1f,
                0f);

            Assert.That(
                AttackApproachStaging.TargetAtSetContact(approach, 1f, 6f, 0.38f),
                Is.EqualTo(approach.ApproachStart));
        }

        [Test]
        public void Plan_AttackApproachQualityEasesSmoothlyIntoItsExplicitCap()
        {
            var below = AttackDecisionForApproachDistance(1.3f).AttackApproach.Value.JumpQuality;
            var near = AttackDecisionForApproachDistance(1.4f).AttackApproach.Value.JumpQuality;
            var at = AttackDecisionForApproachDistance(1.5f).AttackApproach.Value.JumpQuality;
            var above = AttackDecisionForApproachDistance(1.6f).AttackApproach.Value.JumpQuality;

            var earlyGain = near - below;
            var capGain = at - near;
            Assert.That(below, Is.GreaterThan(0f));
            Assert.That(near, Is.GreaterThan(below));
            Assert.That(at, Is.GreaterThan(near));
            Assert.That(at, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(above, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(capGain, Is.LessThan(earlyGain));
            Assert.That(capGain, Is.LessThan(0.02f));
            Assert.That(above - at, Is.LessThanOrEqualTo(capGain));
        }

        [TestCase(TeamId.Blue)]
        [TestCase(TeamId.Orange)]
        public void Plan_AttackUsesCorrectWorldAndLocalApproachGeometryForBothTeams(TeamId team)
        {
            var tactic = CreateTactic(SpikeRoute.CrossCourt, team);
            var takeoff = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            var decision = new TeamRallyDecisionPlanner(17).Plan(CreateInput(
                team,
                RallyDecisionStage.Attack,
                new SimVector3(0f, 3f, -new TeamCourtFrame(team).WorldDepthSign),
                new SimVector3(8f, 0f, tactic.SetterPosition.Z),
                takeoff,
                new SimVector3(7f, 0f, tactic.DefenderPosition.Z),
                tactic: tactic,
                availableSeconds: 2f));

            var approach = decision.AttackApproach.Value;
            var frame = new TeamCourtFrame(team);
            var localTakeoff = frame.ToLocal(approach.Takeoff);
            var localStart = frame.ToLocal(approach.ApproachStart);
            var localBallTarget = frame.ToLocal(decision.BallTarget);
            Assert.That(approach.Takeoff, Is.EqualTo(takeoff));
            Assert.That(localTakeoff.X, Is.EqualTo(approach.Takeoff.X).Within(0.00001f));
            Assert.That(localTakeoff.Y, Is.EqualTo(approach.Takeoff.Y).Within(0.00001f));
            Assert.That(localTakeoff.Z, Is.EqualTo(frame.ToLocal(takeoff).Z).Within(0.00001f));
            Assert.That(approach.ApproachStart.X, Is.EqualTo(takeoff.X).Within(0.00001f));
            Assert.That(approach.ApproachStart.Y, Is.EqualTo(takeoff.Y).Within(0.00001f));
            Assert.That(localStart.X, Is.EqualTo(approach.ApproachStart.X).Within(0.00001f));
            Assert.That(localStart.Y, Is.EqualTo(approach.ApproachStart.Y).Within(0.00001f));
            Assert.That(localStart.Z,
                Is.EqualTo(localTakeoff.Z - approach.Distance).Within(0.00001f));
            Assert.That(localBallTarget.X, Is.EqualTo(decision.BallTarget.X).Within(0.00001f));
            Assert.That(localBallTarget.Y, Is.EqualTo(decision.BallTarget.Y).Within(0.00001f));
            Assert.That(localBallTarget.Z, Is.EqualTo(5.25f).Within(0.00001f));
        }

        [Test]
        public void Plan_IsDeterministicAndCandidatesAreNotExternallyMutable()
        {
            var players = new List<RallyPlayerSnapshot>
            {
                new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Setter), new SimVector3(0f, 0f, -2f), Ability(0.8f)),
                new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Attacker), new SimVector3(1f, 0f, -2f), Ability(0.8f)),
                new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Defender), new SimVector3(2f, 0f, -2f), Ability(0.8f))
            };
            var tactic = CreateTactic(SpikeRoute.Line);
            var input = new TeamRallyDecisionInput(
                TeamId.Blue,
                tactic,
                players,
                new SimVector3(0f, 3f, -1f),
                2f,
                5f,
                0,
                null,
                2,
                3,
                RallyDecisionStage.Attack,
                RallyTacticalWeights.Default);
            players[0] = new RallyPlayerSnapshot(new PlayerId(TeamId.Blue, PlayerRole.Setter), new SimVector3(99f, 0f, -2f), Ability(0.8f));

            var planner = new TeamRallyDecisionPlanner(17);
            var first = planner.Plan(input);
            var second = planner.Plan(input);

            Assert.That(second.HasDecision, Is.EqualTo(first.HasDecision));
            Assert.That(second.Actor, Is.EqualTo(first.Actor));
            Assert.That(second.Action, Is.EqualTo(first.Action));
            Assert.That(second.Score, Is.EqualTo(first.Score));
            Assert.That(second.ContactTarget, Is.EqualTo(first.ContactTarget));
            Assert.That(second.MovementTarget, Is.EqualTo(first.MovementTarget));
            Assert.That(second.BallTarget, Is.EqualTo(first.BallTarget));
            Assert.That(second.AttackApproach, Is.EqualTo(first.AttackApproach));
            Assert.That(second.AttackContactPlan, Is.EqualTo(first.AttackContactPlan));
            Assert.That(input.Players[0].WorldPosition.X, Is.Not.EqualTo(99f));
            Assert.Throws<NotSupportedException>(() => ((IList<RallyDecisionCandidate>)first.Candidates)[0] = default);
            Assert.That(second.Candidates.Count, Is.EqualTo(first.Candidates.Count));
            for (var index = 0; index < first.Candidates.Count; index++)
            {
                Assert.That(second.Candidates[index].Actor, Is.EqualTo(first.Candidates[index].Actor));
                Assert.That(second.Candidates[index].IsFeasible, Is.EqualTo(first.Candidates[index].IsFeasible));
                Assert.That(second.Candidates[index].Score, Is.EqualTo(first.Candidates[index].Score));
            }
        }

        [Test]
        public void OrderedCandidates_ReversedInputProducesTheSameStableOrder()
        {
            var players = new[]
            {
                Snapshot(PlayerRole.OutsideHitter, 4, 0.25f),
                Snapshot(PlayerRole.Setter, 3, 1f),
                Snapshot(PlayerRole.Defender, 5, 0f),
                Snapshot(PlayerRole.Attacker, 1, 0.5f)
            };
            var reversed = players.Reverse().ToArray();
            var planner = new TeamRallyDecisionPlanner(17);

            var first = planner.OrderedCandidates(CreateRawInput(players));
            var second = planner.OrderedCandidates(CreateRawInput(reversed));

            Assert.That(
                second.Select(candidate => candidate.Actor),
                Is.EqualTo(first.Select(candidate => candidate.Actor)));
            Assert.That(
                ((IList<RallyDecisionCandidate>)first).IsReadOnly,
                Is.True);
        }

        [Test]
        public void TeamRallyDecisionInput_RejectsInvalidTeamsCountsActorsAndFiniteValues()
        {
            var blueSetter = new RallyPlayerSnapshot(
                new PlayerId(TeamId.Blue, PlayerRole.Setter),
                new SimVector3(0f, 0f, -2f),
                Ability(0.8f));
            var blueAttacker = new RallyPlayerSnapshot(
                new PlayerId(TeamId.Blue, PlayerRole.Attacker),
                new SimVector3(1f, 0f, -2f),
                Ability(0.8f));
            var blueDefender = new RallyPlayerSnapshot(
                new PlayerId(TeamId.Blue, PlayerRole.Defender),
                new SimVector3(2f, 0f, -2f),
                Ability(0.8f));
            Assert.Throws<ArgumentException>(() => CreateRawInput(new[]
            {
                blueSetter,
                blueAttacker,
                new RallyPlayerSnapshot(new PlayerId(TeamId.Orange, PlayerRole.Defender), new SimVector3(2f, 0f, -2f), Ability(0.8f))
            }));
            Assert.Throws<ArgumentException>(() => CreateRawInput(new[] { blueSetter, blueSetter, blueDefender }));
            Assert.Throws<ArgumentException>(() => CreateRawInput(new[] { blueSetter, blueAttacker }));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateRawInput(
                new[] { blueSetter, blueAttacker, blueDefender },
                lastCountedActor: new PlayerId(TeamId.Orange, PlayerRole.Setter)));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateRawInput(
                new[] { blueSetter, blueAttacker, blueDefender },
                predictedBallCenter: new SimVector3(float.NaN, 1f, -2f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateRawInput(
                new[] { blueSetter, blueAttacker, blueDefender }, availableSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateRawInput(
                new[] { blueSetter, blueAttacker, blueDefender }, availableSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamRallyDecisionInput(
                TeamId.Blue,
                CreateTactic(SpikeRoute.Line),
                new[] { blueSetter, blueAttacker, blueDefender },
                new SimVector3(0f, 2f, -2f),
                2f,
                float.PositiveInfinity,
                0,
                null,
                0,
                0,
                RallyDecisionStage.Organize,
                RallyTacticalWeights.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyPlayerSnapshot(
                new PlayerId((TeamId)99, PlayerRole.Setter),
                SimVector3.Zero,
                Ability(0.8f)));
        }

        [Test]
        public void Plan_OrganizeSetterWinsWhenAnotherReachableRoleScoresHigher()
        {
            var decision = new TeamRallyDecisionPlanner(17).Plan(CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Organize,
                new SimVector3(0f, 2f, -2f),
                new SimVector3(1.5f, 0f, -2f),
                new SimVector3(0.5f, 0f, -2f),
                new SimVector3(0f, 0f, -2f),
                availableSeconds: 0.5f));

            Assert.That(decision.Actor, Is.EqualTo(new PlayerId(TeamId.Blue, PlayerRole.Setter)));
            Assert.That(decision.Score.NominalRole, Is.GreaterThan(0f));
            var setter = FindCandidate(decision, PlayerRole.Setter);
            Assert.That(setter.IsFeasible, Is.True);
            var defender = FindCandidate(decision, PlayerRole.Defender);
            Assert.That(defender.Score.Total, Is.GreaterThan(setter.Score.Total));
        }

        [Test]
        public void RallyDecisionCandidate_RejectsInvalidActorAndPreservesFiniteZeroScore()
        {
            var validScore = ValidScore();

            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyDecisionCandidate(
                new PlayerId((TeamId)99, PlayerRole.Setter),
                true,
                validScore));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyDecisionCandidate(
                new PlayerId(TeamId.Blue, (PlayerRole)99),
                true,
                validScore));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RallyDecisionScore(
                float.NaN,
                0f,
                0f,
                0f,
                0f));
            var finiteZero = new RallyDecisionCandidate(
                new PlayerId(TeamId.Blue, PlayerRole.Setter),
                true,
                default);
            Assert.That(finiteZero.Score.Total, Is.Zero);
        }

        [Test]
        public void TeamRallyDecision_RejectsInvalidPublicOutputsAndPreservesValidNoDecision()
        {
            var actor = new PlayerId(TeamId.Blue, PlayerRole.Attacker);
            var candidate = new RallyDecisionCandidate(actor, true, ValidScore());
            var attackApproach = new AttackApproachPlan(
                new SimVector3(2f, 0f, -3.5f),
                new SimVector3(2f, 0f, -2.45f),
                1.05f,
                0.5f,
                0.1f);
            var attackContactPlan = AttackContactPlanner.Plan(new AttackContactInput(
                3.42f,
                0.5f,
                1f,
                SetQualityGrade.A,
                attackApproach.Takeoff,
                0.5f,
                1f));

            Assert.That(TeamRallyDecision.NoDecision.HasDecision, Is.False);
            Assert.That(TeamRallyDecision.NoDecision.Candidates, Is.Empty);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<RallyDecisionCandidate>)TeamRallyDecision.NoDecision.Candidates).Add(candidate));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamRallyDecision(
                new PlayerId((TeamId)99, PlayerRole.Attacker),
                TechniqueAction.Attack,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                new[] { candidate },
                attackApproach));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamRallyDecision(
                actor,
                (TechniqueAction)99,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                new[] { candidate },
                attackApproach));
            Assert.Throws<ArgumentException>(() => new TeamRallyDecision(
                actor,
                TechniqueAction.Receive,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                new[] { candidate },
                attackApproach));
            Assert.Throws<ArgumentException>(() => new TeamRallyDecision(
                actor,
                TechniqueAction.Attack,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                new[] { candidate },
                null));
            Assert.Throws<ArgumentException>(() => new TeamRallyDecision(
                actor,
                TechniqueAction.Receive,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                Array.Empty<RallyDecisionCandidate>(),
                null));
            Assert.Throws<ArgumentNullException>(() => new TeamRallyDecision(
                actor,
                TechniqueAction.Receive,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                null,
                null));
            Assert.Throws<ArgumentException>(() => new TeamRallyDecision(
                actor,
                TechniqueAction.Receive,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                new[] { new RallyDecisionCandidate(new PlayerId(TeamId.Blue, PlayerRole.Setter), true, ValidScore()) },
                null));
            Assert.Throws<ArgumentException>(() => new TeamRallyDecision(
                actor,
                TechniqueAction.Receive,
                SimVector3.Zero,
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                new[] { new RallyDecisionCandidate(new PlayerId(TeamId.Orange, PlayerRole.Attacker), true, ValidScore()), candidate },
                null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TeamRallyDecision(
                actor,
                TechniqueAction.Receive,
                new SimVector3(float.NaN, 0f, 0f),
                SimVector3.Zero,
                SimVector3.Zero,
                ValidScore(),
                new[] { candidate },
                null));

            var valid = new TeamRallyDecision(
                actor,
                TechniqueAction.Attack,
                new SimVector3(2f, 2.7f, -2.45f),
                new SimVector3(2f, 0f, -2.45f),
                new SimVector3(2f, 0f, 5.25f),
                ValidScore(),
                new[] { candidate },
                attackApproach,
                attackContactPlan);

            Assert.That(valid.HasDecision, Is.True);
            Assert.That(valid.Actor, Is.EqualTo(actor));
            Assert.That(valid.AttackApproach, Is.EqualTo(attackApproach));
            Assert.That(valid.AttackContactPlan, Is.EqualTo(attackContactPlan));
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

        private static TeamRallyDecisionInput CreateAttackSelectionInput(
            PlayerAbilityProfile first,
            PlayerAbilityProfile second)
        {
            var tactic = CreateTactic(SpikeRoute.Line);
            var takeoff = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            return new TeamRallyDecisionInput(
                TeamId.Blue,
                tactic,
                new[]
                {
                    new RallyPlayerSnapshot(
                        new PlayerId(TeamId.Blue, PlayerRole.OutsideHitter, 0),
                        takeoff,
                        first),
                    new RallyPlayerSnapshot(
                        new PlayerId(TeamId.Blue, PlayerRole.OutsideHitter, 1),
                        takeoff,
                        second),
                    new RallyPlayerSnapshot(
                        new PlayerId(TeamId.Blue, PlayerRole.Defender, 2),
                        new SimVector3(20f, 0f, -6f),
                        Ability(0.8f))
                },
                new SimVector3(0f, 3f, -1f),
                2f,
                5f,
                0,
                null,
                0,
                0,
                RallyDecisionStage.Attack,
                RallyTacticalWeights.Default);
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

        private static TeamRallyDecisionInput CreateRawInput(
            IEnumerable<RallyPlayerSnapshot> players,
            SimVector3 predictedBallCenter = default,
            float availableSeconds = 2f,
            PlayerId? lastCountedActor = null)
        {
            if (predictedBallCenter.Equals(default(SimVector3)))
            {
                predictedBallCenter = new SimVector3(0f, 2f, -2f);
            }

            return new TeamRallyDecisionInput(
                TeamId.Blue,
                CreateTactic(SpikeRoute.Line),
                players,
                predictedBallCenter,
                availableSeconds,
                5f,
                0,
                lastCountedActor,
                0,
                0,
                RallyDecisionStage.Organize,
                RallyTacticalWeights.Default);
        }

        private static TeamRallyDecisionInput CreateOrganizeInput(
            RallyPlayerSnapshot setter,
            RallyPlayerSnapshot attacker,
            RallyPlayerSnapshot defender,
            float availableSeconds = 2f,
            PlayerId? lastCountedActor = null)
        {
            return new TeamRallyDecisionInput(
                TeamId.Blue,
                CreateTactic(SpikeRoute.Line),
                new[] { setter, attacker, defender },
                new SimVector3(0f, 2f, -2f),
                availableSeconds,
                5f,
                0,
                lastCountedActor,
                0,
                0,
                RallyDecisionStage.Organize,
                RallyTacticalWeights.Default);
        }

        private static TeamRallyDecision AttackDecisionForApproachDistance(float desiredDistance)
        {
            var tactic = CreateTactic(SpikeRoute.Line);
            var takeoff = new SimVector3(tactic.AttackerPosition.X, 0f, tactic.AttackerPosition.Z);
            var mobility = (desiredDistance - 0.6f) / 1.4f;
            return new TeamRallyDecisionPlanner(17).Plan(CreateInput(
                TeamId.Blue,
                RallyDecisionStage.Attack,
                new SimVector3(0f, 3f, -1f),
                new SimVector3(8f, 0f, -5f),
                takeoff,
                new SimVector3(7f, 0f, -5f),
                tactic: tactic,
                availableSeconds: 2f,
                attackerAbility: Ability(mobility)));
        }

        private static RallyDecisionScore ValidScore()
        {
            return new RallyDecisionScore(1f, 0.5f, 0.2f, -0.1f, 1.6f);
        }

        private static RallyDecisionCandidate FindCandidate(TeamRallyDecision decision, PlayerRole role)
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
                SetRhythm.FastPin,
                0.45f);
        }

        private static PlayerAbilityProfile Ability(float mobility)
        {
            return MatchV4TestFixture.CreateAbility(mobility, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f);
        }

        private static PlayerAbilityProfile AbilityWithAttack(
            float attackTechnique,
            float attackPower)
        {
            return MatchV4TestFixture.CreateAbility(
                0.8f,
                0.8f,
                0.8f,
                0.8f,
                0.8f,
                attackTechnique,
                attackPower);
        }

        private static RallyPlayerSnapshot Snapshot(PlayerRole role, int slot, float x)
        {
            return new RallyPlayerSnapshot(
                new PlayerId(TeamId.Blue, role, slot),
                new SimVector3(x, 0f, -2f),
                Ability(0.8f));
        }
    }
}
