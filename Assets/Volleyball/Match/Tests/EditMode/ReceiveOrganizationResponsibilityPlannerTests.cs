using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;
using RuntimePlayerId = Volleyball.Domain.Prototype.PlayerId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using RuntimeTeamId = Volleyball.Domain.Prototype.TeamId;

namespace Volleyball.EditModeTests
{
    public sealed class ReceiveOrganizationResponsibilityPlannerTests
    {
        [TestCase(RuntimeTeamId.Blue, TeamSide.Home)]
        [TestCase(RuntimeTeamId.Orange, TeamSide.Away)]
        public void PlanOrganization_ReachableRegisteredSetterBeatsHigherScoreBackupAndUsesMirroredTarget(
            RuntimeTeamId runtimeTeam,
            TeamSide side)
        {
            var fixture = CreateFixture(runtimeTeam, side);
            var registeredSetter = fixture.Bindings[3];
            var input = CreateInput(
                runtimeTeam,
                RallyDecisionStage.Organize,
                fixture.RuntimePlayers,
                registeredSetter.RuntimePlayerId,
                registeredSetterPosition: OrganizationBall(runtimeTeam),
                availableSeconds: 1f);

            var result = CreatePlanner().PlanOrganization(
                input,
                CreateAttackInput(runtimeTeam, fixture.RuntimePlayers),
                fixture.Eligibility,
                fixture.Bindings,
                revision: 9);
            var ordered = new TeamRallyDecisionPlanner().OrderedCandidates(input);
            var setterCandidate = FindCandidate(
                ordered,
                registeredSetter.RuntimePlayerId);
            var higherScoreBackup = ordered.First(candidate =>
                !candidate.Actor.Equals(registeredSetter.RuntimePlayerId));

            Assert.That(
                result.Decision.Actor,
                Is.EqualTo(registeredSetter.RuntimePlayerId));
            Assert.That(
                higherScoreBackup.Score.Total,
                Is.GreaterThan(setterCandidate.Score.Total));
            Assert.That(result.SetterEvidence.IsReachable, Is.True);
            Assert.That(result.FallbackReason, Is.EqualTo(OrganizationFallbackReasonV3.None));
            Assert.That(
                result.Plan.OrganizationTarget,
                Is.EqualTo(SetterOrganizationZone.DefaultWorldTarget(runtimeTeam)));
            Assert.That(
                result.SetterEvidence.ReachMarginMeters,
                Is.EqualTo(setterCandidate.Score.Reachability));
            Assert.That(result.SetterEvidence.MovementMeters, Is.Zero);
            Assert.That(
                result.SetterEvidence.ReactionDelaySeconds,
                Is.EqualTo(0.044f).Within(0.00001f));
        }

        [Test]
        public void PlanOrganization_PreviousTouchSetterUsesFirstOrderedLegalBackup()
        {
            var fixture = CreateFixture(RuntimeTeamId.Blue, TeamSide.Home);
            var setter = fixture.Bindings[3].RuntimePlayerId;
            var input = CreateInput(
                RuntimeTeamId.Blue,
                RallyDecisionStage.Organize,
                fixture.RuntimePlayers,
                setter,
                registeredSetterPosition: OrganizationBall(RuntimeTeamId.Blue),
                availableSeconds: 1f,
                previousActor: setter);
            var ordered = new TeamRallyDecisionPlanner().OrderedCandidates(input);
            var expected = ordered.First(candidate => candidate.IsFeasible).Actor;

            var result = CreatePlanner().PlanOrganization(
                input,
                CreateAttackInput(RuntimeTeamId.Blue, fixture.RuntimePlayers),
                fixture.Eligibility,
                fixture.Bindings,
                revision: 10);

            Assert.That(result.Decision.Actor, Is.EqualTo(expected));
            Assert.That(
                result.FallbackReason,
                Is.EqualTo(OrganizationFallbackReasonV3.SetterPreviousTouch));
            Assert.That(result.Plan.BackupOrganizers[0], Is.EqualTo(fixture.StableFor(expected)));
            Assert.That(
                result.AttackPreparationDecision.Actor,
                Is.Not.EqualTo(result.Decision.Actor),
                "The fallback organizer cannot also own the next counted attack.");
        }

        [Test]
        public void PlanOrganization_NoReachableBackupReturnsNoLegalOrganizerWithSetterEvidence()
        {
            var fixture = CreateFixture(RuntimeTeamId.Blue, TeamSide.Home);
            var setter = fixture.Bindings[3].RuntimePlayerId;
            var unreachable = fixture.RuntimePlayers
                .Select(player => new RallyPlayerSnapshot(
                    player.Id,
                    new SimVector3(20f + player.Id.RosterSlot, 0f, -5f),
                    player.Ability))
                .ToArray();
            var input = CreateInput(
                RuntimeTeamId.Blue,
                RallyDecisionStage.Organize,
                unreachable,
                setter,
                registeredSetterPosition: unreachable[3].WorldPosition,
                availableSeconds: 0.25f);

            var result = CreatePlanner().PlanOrganization(
                input,
                CreateAttackInput(RuntimeTeamId.Blue, unreachable, 0.25f),
                fixture.Eligibility,
                fixture.Bindings,
                revision: 11);

            Assert.That(result.Decision, Is.SameAs(TeamRallyDecision.NoDecision));
            Assert.That(result.FallbackReason, Is.EqualTo(OrganizationFallbackReasonV3.NoLegalOrganizer));
            Assert.That(result.SetterEvidence.IsReachable, Is.False);
            Assert.That(result.SetterEvidence.ReachMarginMeters, Is.LessThan(0f));
        }

        [Test]
        public void PlanReceive_PublishesStablePrimaryAndTwoEmergencyReceiversInFeasibleOrder()
        {
            var fixture = CreateFixture(RuntimeTeamId.Blue, TeamSide.Home);
            var players = fixture.RuntimePlayers.ToArray();
            players[5] = new RallyPlayerSnapshot(
                players[5].Id,
                new SimVector3(0f, 0f, -2f),
                players[5].Ability);
            players[4] = new RallyPlayerSnapshot(
                players[4].Id,
                new SimVector3(0.15f, 0f, -2f),
                players[4].Ability);
            players[1] = new RallyPlayerSnapshot(
                players[1].Id,
                new SimVector3(0.3f, 0f, -2f),
                players[1].Ability);
            var input = CreateInput(
                RuntimeTeamId.Blue,
                RallyDecisionStage.Receive,
                players,
                fixture.Bindings[3].RuntimePlayerId,
                new SimVector3(4f, 0f, -4f),
                1f);
            var expected = new TeamRallyDecisionPlanner()
                .OrderedCandidates(input)
                .Where(candidate => candidate.IsFeasible)
                .Take(3)
                .Select(candidate => fixture.StableFor(candidate.Actor))
                .ToArray();

            var result = CreatePlanner().PlanReceive(
                input,
                CreateAttackInput(RuntimeTeamId.Blue, players),
                fixture.Eligibility,
                fixture.Bindings,
                revision: 12);

            Assert.That(result.Plan.PrimaryReceiver, Is.EqualTo(expected[0]));
            Assert.That(result.Plan.EmergencyReceivers, Is.EqualTo(expected.Skip(1)));
            Assert.That(result.Plan.RegisteredSetter, Is.EqualTo(fixture.Bindings[3].StablePlayerId));
            Assert.That(result.AttackPreparationDecision.HasDecision, Is.True);
        }

        [Test]
        public void PlanReceive_CommittedContinuationReceiverBecomesPrimaryWithoutInventingActor()
        {
            var fixture = CreateFixture(RuntimeTeamId.Blue, TeamSide.Home);
            var input = CreateInput(
                RuntimeTeamId.Blue,
                RallyDecisionStage.Receive,
                fixture.RuntimePlayers,
                fixture.Bindings[3].RuntimePlayerId,
                new SimVector3(0f, 2f, -2f),
                1f);
            var feasible = new TeamRallyDecisionPlanner()
                .OrderedCandidates(input)
                .Where(candidate => candidate.IsFeasible)
                .ToArray();
            Assert.That(feasible.Length, Is.GreaterThanOrEqualTo(2));
            var committed = fixture.StableFor(feasible[1].Actor);

            var result = CreatePlanner().PlanReceive(
                input,
                CreateAttackInput(RuntimeTeamId.Blue, fixture.RuntimePlayers),
                fixture.Eligibility,
                fixture.Bindings,
                revision: 13,
                committedContinuationReceiver: committed);

            Assert.That(result.Plan.PrimaryReceiver, Is.EqualTo(committed));
            Assert.That(result.Decision.Actor, Is.EqualTo(feasible[1].Actor));
            Assert.That(
                result.Plan.EmergencyReceivers.Contains(committed),
                Is.False);
        }

        [Test]
        public void PlanReceive_RejectsBindingToOffCourtPlayerBeforePublishingResponsibilities()
        {
            var fixture = CreateFixture(RuntimeTeamId.Blue, TeamSide.Home);
            var invalid = fixture.Bindings.ToArray();
            invalid[0] = new ReceiveOrganizationPlayerBindingV3(
                invalid[0].RuntimePlayerId,
                new StablePlayerId("bench-player"));

            Assert.That(
                () => CreatePlanner().PlanReceive(
                    CreateInput(
                        RuntimeTeamId.Blue,
                        RallyDecisionStage.Receive,
                        fixture.RuntimePlayers,
                        fixture.Bindings[3].RuntimePlayerId,
                        new SimVector3(4f, 0f, -4f),
                        1f),
                    CreateAttackInput(RuntimeTeamId.Blue, fixture.RuntimePlayers),
                    fixture.Eligibility,
                    invalid,
                    revision: 13),
                Throws.ArgumentException);
        }

        private static ReceiveOrganizationResponsibilityPlanner CreatePlanner()
        {
            return new ReceiveOrganizationResponsibilityPlanner(
                new TeamRallyDecisionPlanner());
        }

        private static PlannerFixture CreateFixture(RuntimeTeamId runtimeTeam, TeamSide side)
        {
            var context = MatchV4TestFixture.CreateContext();
            var homeIds = context.Home.Players.Select(player => player.PlayerId).ToArray();
            var awayIds = context.Away.Players.Select(player => player.PlayerId).ToArray();
            var eligibility = OnCourtLineupRulesV3.Create(
                context,
                homeIds,
                awayIds,
                homeIds[0],
                awayIds[0],
                Array.Empty<LiberoReplacementV3>());
            var stableIds = (side == TeamSide.Home ? homeIds : awayIds).ToArray();
            var roles = new[]
            {
                PlayerRole.Setter,
                PlayerRole.Attacker,
                PlayerRole.MiddleBlocker,
                PlayerRole.OutsideHitter,
                PlayerRole.OutsideHitter,
                PlayerRole.Defender
            };
            var players = new RallyPlayerSnapshot[6];
            var bindings = new ReceiveOrganizationPlayerBindingV3[6];
            for (var index = 0; index < players.Length; index++)
            {
                var runtimeId = new RuntimePlayerId(runtimeTeam, roles[index], index);
                players[index] = new RallyPlayerSnapshot(
                    runtimeId,
                    new SimVector3(index * 0.4f, 0f, OrganizationBall(runtimeTeam).Z),
                    Ability());
                bindings[index] = new ReceiveOrganizationPlayerBindingV3(runtimeId, stableIds[index]);
            }

            return new PlannerFixture(eligibility, players, bindings);
        }

        private static TeamRallyDecisionInput CreateInput(
            RuntimeTeamId team,
            RallyDecisionStage stage,
            IReadOnlyList<RallyPlayerSnapshot> source,
            RuntimePlayerId registeredSetter,
            SimVector3 registeredSetterPosition,
            float availableSeconds,
            RuntimePlayerId? previousActor = null)
        {
            var players = source
                .Select(player => player.Id.Equals(registeredSetter)
                    ? new RallyPlayerSnapshot(player.Id, registeredSetterPosition, player.Ability)
                    : player)
                .ToArray();
            return new TeamRallyDecisionInput(
                team,
                Tactic(team),
                players,
                new SimVector3(0f, 2f, OrganizationBall(team).Z),
                availableSeconds,
                5f,
                stage == RallyDecisionStage.Receive ? 0 : 1,
                previousActor,
                1,
                0,
                stage,
                RallyTacticalWeights.Default);
        }

        private static TeamRallyDecisionInput CreateAttackInput(
            RuntimeTeamId team,
            IReadOnlyList<RallyPlayerSnapshot> players,
            float availableSeconds = 2f)
        {
            return new TeamRallyDecisionInput(
                team,
                Tactic(team),
                players,
                new SimVector3(0f, 3f, OrganizationBall(team).Z),
                availableSeconds,
                5f,
                2,
                null,
                1,
                1,
                RallyDecisionStage.Attack,
                RallyTacticalWeights.Default);
        }

        private static TeamRallyTactic Tactic(RuntimeTeamId team)
        {
            var sign = new TeamCourtFrame(team).WorldDepthSign;
            return new TeamRallyTactic(
                SetRoute.LeftPin,
                SpikeRoute.Line,
                new CourtPoint(1.5f, sign * 1.1f),
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

        private static SimVector3 OrganizationBall(RuntimeTeamId team)
        {
            return new SimVector3(
                0f,
                0f,
                team == RuntimeTeamId.Blue ? -2f : 2f);
        }

        private static PlayerAbilityProfile Ability()
        {
            return MatchV4TestFixture.CreateAbility(0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f);
        }

        private static RallyDecisionCandidate FindCandidate(
            IReadOnlyList<RallyDecisionCandidate> candidates,
            RuntimePlayerId actor)
        {
            return candidates.Single(candidate => candidate.Actor.Equals(actor));
        }

        private sealed class PlannerFixture
        {
            public PlannerFixture(
                OnCourtEligibilitySnapshot eligibility,
                IReadOnlyList<RallyPlayerSnapshot> runtimePlayers,
                IReadOnlyList<ReceiveOrganizationPlayerBindingV3> bindings)
            {
                Eligibility = eligibility;
                RuntimePlayers = runtimePlayers;
                Bindings = bindings;
            }

            public OnCourtEligibilitySnapshot Eligibility { get; }

            public IReadOnlyList<RallyPlayerSnapshot> RuntimePlayers { get; }

            public IReadOnlyList<ReceiveOrganizationPlayerBindingV3> Bindings { get; }

            public StablePlayerId StableFor(RuntimePlayerId runtimeId)
            {
                return Bindings.Single(binding => binding.RuntimePlayerId.Equals(runtimeId)).StablePlayerId;
            }
        }
    }
}
