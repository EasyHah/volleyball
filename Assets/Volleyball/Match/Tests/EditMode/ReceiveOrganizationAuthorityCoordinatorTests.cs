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
using RuntimeTeamId = Volleyball.Domain.Prototype.TeamId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class ReceiveOrganizationAuthorityCoordinatorTests
    {
        [Test]
        public void AcceptReceive_AdvancesToOrganizationWithActualLanding()
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            var receive = coordinator.PlanReceive(fixture.Request(revision: 3, sourceSequence: 1));
            var landing = new SimVector3(1.4f, 2.2f, -1.3f);

            var next = coordinator.AcceptReceive(new AcceptedReceiveV3(
                revision: 3,
                sourceSequence: 2,
                receive.PrimaryActor,
                landing,
                PlanCoverageReason.WithinConditionalEnvelope,
                "trajectory-3",
                "classification-3"));

            Assert.That(
                next.Phase,
                Is.EqualTo(ReceiveOrganizationAuthorityPhaseV3.OrganizationPlanned));
            Assert.That(next.ActualFirstPassLanding, Is.EqualTo(landing));
            Assert.That(next.CoverageDecision.Kind, Is.EqualTo(PlanCoverageDecisionKind.CoveredActivateBranch));
        }

        [Test]
        public void AcceptReceive_RejectsStaleRevisionWithoutPublishingCommands()
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            var receive = coordinator.PlanReceive(fixture.Request(revision: 7, sourceSequence: 1));
            var publishedBefore = fixture.Sink.PublishedBatches.Count;

            Assert.That(
                () => coordinator.AcceptReceive(new AcceptedReceiveV3(
                    6,
                    2,
                    receive.PrimaryActor,
                    new SimVector3(1.4f, 2.2f, -1.3f),
                    PlanCoverageReason.WithinConditionalEnvelope,
                    "trajectory-7",
                    "classification-7")),
                Throws.InvalidOperationException);
            Assert.That(fixture.Sink.PublishedBatches, Has.Count.EqualTo(publishedBefore));
        }

        [Test]
        public void MissPrimary_ActivatesOnlyDeclaredEmergencyBranch()
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            var planned = coordinator.PlanReceive(fixture.Request(revision: 8, sourceSequence: 1));

            var branch = coordinator.ActivateEmergency(
                revision: 8,
                sourceSequence: 2,
                planned.Plan.EmergencyReceivers[0]);

            Assert.That(branch.ActiveBranch, Is.EqualTo(RallyPlanBranchV3.Contingency));
            Assert.That(
                () => coordinator.ActivateEmergency(
                    8,
                    3,
                    new StablePlayerId("undeclared-player")),
                Throws.InvalidOperationException);
        }

        [Test]
        public void AcceptReceive_RejectsDuplicateAcceptedEventBeforePublishing()
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            var receive = coordinator.PlanReceive(fixture.Request(4, 1));
            var accepted = new AcceptedReceiveV3(
                4,
                2,
                receive.PrimaryActor,
                new SimVector3(1.4f, 2.2f, -1.3f),
                PlanCoverageReason.WithinConditionalEnvelope,
                "trajectory-4",
                "classification-4");
            coordinator.AcceptReceive(accepted);
            var publishedBefore = fixture.Sink.PublishedBatches.Count;

            Assert.That(
                () => coordinator.AcceptReceive(accepted),
                Throws.InvalidOperationException);
            Assert.That(fixture.Sink.PublishedBatches, Has.Count.EqualTo(publishedBefore));
        }

        [Test]
        public void AcceptReceive_RejectsDeclaredButInactiveEmergencyActor()
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            var receive = coordinator.PlanReceive(fixture.Request(4, 1));
            var publishedBefore = fixture.Sink.PublishedBatches.Count;

            Assert.That(
                () => coordinator.AcceptReceive(new AcceptedReceiveV3(
                    4,
                    2,
                    receive.Plan.EmergencyReceivers[0],
                    new SimVector3(1.4f, 2.2f, -1.3f),
                    PlanCoverageReason.WithinConditionalEnvelope,
                    "trajectory-4",
                    "classification-4")),
                Throws.InvalidOperationException);
            Assert.That(fixture.Sink.PublishedBatches, Has.Count.EqualTo(publishedBefore));
        }

        [Test]
        public void ActivateEmergency_RejectsAfterPrimaryIsCommitted()
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            var receive = coordinator.PlanReceive(fixture.Request(4, 1));
            coordinator.CommitReceive(4, 2);
            var publishedBefore = fixture.Sink.PublishedBatches.Count;

            Assert.That(
                () => coordinator.ActivateEmergency(
                    4,
                    3,
                    receive.Plan.EmergencyReceivers[0]),
                Throws.InvalidOperationException);
            Assert.That(fixture.Sink.PublishedBatches, Has.Count.EqualTo(publishedBefore));
        }

        [Test]
        public void InvalidateCommittedReceive_PreservesCommittedActorAndUsesLocalRevision()
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            var receive = coordinator.PlanReceive(fixture.Request(5, 1));
            coordinator.CommitReceive(5, 2);

            var state = coordinator.Invalidate(
                5,
                3,
                PlanCoverageReason.ResponsibleActorChanged);
            var batch = fixture.Sink.PublishedBatches.Last();

            Assert.That(state.CommittedActor, Is.EqualTo(receive.PrimaryActor));
            Assert.That(state.CoverageDecision.Kind, Is.EqualTo(PlanCoverageDecisionKind.LocalRevision));
            Assert.That(
                batch.Commands.Any(command =>
                    command.Kind == ReceiveOrganizationCommandKind.CancelUncommitted &&
                    command.Actor.Equals(receive.PrimaryActor)),
                Is.False);
        }

        [TestCase(PlanCoverageReason.ResponsibleActorChanged, PlanCoverageDecisionKind.LocalRevision)]
        [TestCase(PlanCoverageReason.BallEnvelopeExceeded, PlanCoverageDecisionKind.ScopedReplan)]
        public void Invalidate_MapsOnlyBoundedGateHReplans(
            PlanCoverageReason reason,
            PlanCoverageDecisionKind expected)
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            coordinator.PlanReceive(fixture.Request(6, 1));

            var state = coordinator.Invalidate(6, 2, reason);

            Assert.That(state.CoverageDecision.Kind, Is.EqualTo(expected));
            Assert.That(state.Phase, Is.EqualTo(ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned));
        }

        [Test]
        public void Invalidate_RejectsGlobalRebuildBeforePublishing()
        {
            var fixture = CreateFixture();
            var coordinator = fixture.Coordinator();
            coordinator.PlanReceive(fixture.Request(6, 1));
            var publishedBefore = fixture.Sink.PublishedBatches.Count;

            Assert.That(
                () => coordinator.Invalidate(
                    6,
                    2,
                    PlanCoverageReason.EnvelopeExceeded),
                Throws.InvalidOperationException);
            Assert.That(fixture.Sink.PublishedBatches, Has.Count.EqualTo(publishedBefore));
        }

        [Test]
        public void AcceptReceive_NoLegalOrganizerBecomesTerminal()
        {
            var fixture = CreateFixture(organizationReachable: false);
            var coordinator = fixture.Coordinator();
            var receive = coordinator.PlanReceive(fixture.Request(9, 1));

            var state = coordinator.AcceptReceive(new AcceptedReceiveV3(
                9,
                2,
                receive.PrimaryActor,
                new SimVector3(0f, 2f, -2f),
                PlanCoverageReason.WithinConditionalEnvelope,
                "trajectory-9",
                "classification-9"));

            Assert.That(state.Phase, Is.EqualTo(ReceiveOrganizationAuthorityPhaseV3.Terminal));
            Assert.That(state.FallbackReason, Is.EqualTo(OrganizationFallbackReasonV3.NoLegalOrganizer));
            Assert.That(
                fixture.Sink.PublishedBatches.Last().Commands.Any(command =>
                    command.Kind == ReceiveOrganizationCommandKind.OrganizationContact),
                Is.False);
        }

        [Test]
        public void PlanCoverageDecision_RejectsUndefinedEnums()
        {
            Assert.That(
                () => new PlanCoverageDecision(
                    (PlanCoverageDecisionKind)99,
                    "4",
                    PlanCoverageReason.RallyOpen,
                    Array.Empty<string>(),
                    0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new PlanCoverageDecision(
                    PlanCoverageDecisionKind.LocalRevision,
                    "4",
                    (PlanCoverageReason)99,
                    Array.Empty<string>(),
                    0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static AuthorityFixture CreateFixture(bool organizationReachable = true)
        {
            return new AuthorityFixture(organizationReachable);
        }

        private sealed class AuthorityFixture
        {
            private readonly OnCourtEligibilitySnapshot _eligibility;
            private readonly IReadOnlyList<ReceiveOrganizationPlayerBindingV3> _bindings;
            private readonly TeamRallyDecisionInput _receive;
            private readonly TeamRallyDecisionInput _organization;
            private readonly TeamRallyDecisionInput _attack;

            public AuthorityFixture(bool organizationReachable)
            {
                var context = MatchV4TestFixture.CreateContext();
                var home = context.Home.Players.Select(player => player.PlayerId).ToArray();
                var away = context.Away.Players.Select(player => player.PlayerId).ToArray();
                _eligibility = OnCourtLineupRulesV3.Create(
                    context,
                    home,
                    away,
                    home[0],
                    away[0],
                    Array.Empty<LiberoReplacementV3>());
                var runtime = new[]
                {
                    new RuntimePlayerId(RuntimeTeamId.Blue, PlayerRole.Setter, 0),
                    new RuntimePlayerId(RuntimeTeamId.Blue, PlayerRole.Attacker, 1),
                    new RuntimePlayerId(RuntimeTeamId.Blue, PlayerRole.Defender, 2)
                };
                var stable = new[]
                {
                    home.Single(id => id.Value == "home-setter"),
                    home.Single(id => id.Value == "home-outside-a"),
                    home.Single(id => id.Value == "home-libero")
                };
                _bindings = runtime
                    .Select((id, index) =>
                        new ReceiveOrganizationPlayerBindingV3(id, stable[index]))
                    .ToArray();
                var receivePlayers = new[]
                {
                    Snapshot(runtime[0], 1f, -2f),
                    Snapshot(runtime[1], 0.4f, -2f),
                    Snapshot(runtime[2], 0f, -2f)
                };
                var organizationPlayers = organizationReachable
                    ? receivePlayers
                    : receivePlayers.Select(player => Snapshot(
                        player.Id,
                        20f + player.Id.RosterSlot,
                        -5f)).ToArray();
                _receive = Input(RallyDecisionStage.Receive, receivePlayers, 1f);
                _organization = Input(
                    RallyDecisionStage.Organize,
                    organizationPlayers,
                    organizationReachable ? 1f : 0.25f);
                _attack = Input(
                    RallyDecisionStage.Attack,
                    organizationPlayers,
                    organizationReachable ? 2f : 0.25f);
                Sink = new RecordingAuthorityCommandSink();
            }

            public RecordingAuthorityCommandSink Sink { get; }

            public ReceiveOrganizationAuthorityCoordinator Coordinator()
            {
                return new ReceiveOrganizationAuthorityCoordinator(
                    new ReceiveOrganizationResponsibilityPlanner(
                        new TeamRallyDecisionPlanner(17)),
                    Sink);
            }

            public ReceiveOrganizationAuthorityRequestV3 Request(
                long revision,
                long sourceSequence)
            {
                return new ReceiveOrganizationAuthorityRequestV3(
                    revision,
                    sourceSequence,
                    _receive,
                    _organization,
                    _attack,
                    _eligibility,
                    _bindings);
            }

            private static RallyPlayerSnapshot Snapshot(
                RuntimePlayerId id,
                float x,
                float z)
            {
                return new RallyPlayerSnapshot(
                    id,
                    new SimVector3(x, 0f, z),
                    MatchV4TestFixture.CreateAbility(
                        0.8f,
                        0.8f,
                        0.8f,
                        0.8f,
                        0.8f,
                        0.8f,
                        0.8f));
            }

            private static TeamRallyDecisionInput Input(
                RallyDecisionStage stage,
                IReadOnlyList<RallyPlayerSnapshot> players,
                float availableSeconds)
            {
                return new TeamRallyDecisionInput(
                    RuntimeTeamId.Blue,
                    Tactic(),
                    players,
                    new SimVector3(0f, 2f, -2f),
                    availableSeconds,
                    5f,
                    stage == RallyDecisionStage.Receive ? 0 : 1,
                    null,
                    1,
                    0,
                    stage,
                    RallyTacticalWeights.Default);
            }

            private static TeamRallyTactic Tactic()
            {
                return new TeamRallyTactic(
                    SetRoute.LeftPin,
                    SpikeRoute.Line,
                    new CourtPoint(1.5f, -1.1f),
                    new CourtPoint(2f, -2.45f),
                    new CourtPoint(0f, -5.25f),
                    new BlockCoveragePlan(
                        PlayerRole.Attacker,
                        new CourtPoint(0f, -0.65f),
                        PlayerRole.Setter,
                        new CourtPoint(0f, -4.15f)),
                    SetRhythm.FastPin,
                    0.45f);
            }
        }

        private sealed class RecordingAuthorityCommandSink :
            IReceiveOrganizationAuthorityCommandSink
        {
            public List<ReceiveOrganizationCommandBatch> PublishedBatches { get; } =
                new List<ReceiveOrganizationCommandBatch>();

            public void Publish(ReceiveOrganizationCommandBatch batch)
            {
                PublishedBatches.Add(batch);
            }
        }
    }
}
