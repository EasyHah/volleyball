using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.AI;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;

namespace Volleyball.EditModeTests
{
    public sealed class AttackDefenseAuthorityCoordinatorTests
    {
        [Test]
        public void PlanSetIntent_ProducesReceiptButPublishesNoBatch()
        {
            var sink = new Sink();
            var result = new AttackDefenseAuthorityCoordinator(new AttackDefensePlanner(), sink).PlanSetIntent(Fixture.Request(4, 1));

            Assert.That(result.Receipt.Intent, Is.SameAs(result.Intent));
            Assert.That(result.Receipt.PlanRevision, Is.EqualTo(4));
            Assert.That(sink.Batches, Is.Empty);
        }

        [Test]
        public void PlanSetIntent_AwayAttackingPlanRetainsExplicitAwaySide()
        {
            var coordinator = new AttackDefenseAuthorityCoordinator(new AttackDefensePlanner(), new Sink());

            coordinator.PlanSetIntent(Fixture.Request(4, 1, Volleyball.Shared.Contracts.TeamSide.Away));

            Assert.That(coordinator.State.AttackingSide, Is.EqualTo(Volleyball.Shared.Contracts.TeamSide.Away));
        }

        [Test]
        public void Coordinator_HasNoSetContactCommandSurface()
        {
            var names = System.Enum.GetNames(typeof(AttackDefenseCommandKind));
            Assert.That(names, Does.Not.Contain("SetContact"));
            Assert.That(names, Does.Not.Contain("SetTargetPreparation"));
        }

        [Test]
        public void PlanSetIntent_RejectsDuplicateOrStaleSourceSequence()
        {
            var coordinator = new AttackDefenseAuthorityCoordinator(new AttackDefensePlanner(), new Sink());
            coordinator.PlanSetIntent(Fixture.Request(4, 1));

            Assert.That(() => coordinator.PlanSetIntent(Fixture.Request(4, 1)), Throws.InvalidOperationException);
        }

        [Test]
        public void ContactBeforeCommittedAttack_PublishesNothing()
        {
            var sink = new Sink();
            var coordinator = new AttackDefenseAuthorityCoordinator(new AttackDefensePlanner(), sink);
            coordinator.PlanSetIntent(Fixture.Request(4, 1));

            Assert.That(() => coordinator.AcceptContact(new GateIContactEvidenceV3(4, 2, Fixture.Organizer, PlanCoverageReason.RallyOpen)), Throws.InvalidOperationException);
            Assert.That(sink.Batches, Is.Empty);
        }

        [Test]
        public void ContactEvidence_RetainsV3AcceptanceActionBranchAndExecutionIdentities()
        {
            var evidence = new GateIContactEvidenceV3(4, 9, Fixture.Organizer,
                PlanCoverageReason.RallyOpen, AttackDefenseCommandKind.AttackContact,
                RallyPlanBranchV3.Primary, "attack-envelope", "attack-trajectory", true,
                "declared-exit");

            Assert.That(evidence.V3Accepted, Is.True);
            Assert.That(evidence.ActionKind, Is.EqualTo(AttackDefenseCommandKind.AttackContact));
            Assert.That(evidence.Branch, Is.EqualTo(RallyPlanBranchV3.Primary));
            Assert.That(evidence.EnvelopeIdentity, Is.EqualTo("attack-envelope"));
            Assert.That(evidence.TrajectoryArtifactIdentity, Is.EqualTo("attack-trajectory"));
            Assert.That(evidence.ReorganizationExitIdentity, Is.EqualTo("declared-exit"));
        }

        [Test]
        public void AcceptedAttack_WaitsForDefenseBeforePublishingReorganization()
        {
            var sink = new Sink();
            var coordinator = Fixture.CommittedAttack(sink, out var plan);

            coordinator.AcceptContact(Fixture.Contact(
                plan,
                6,
                plan.SelectedAction.Actor,
                AttackDefenseCommandKind.AttackContact,
                ""));

            Assert.That(coordinator.State.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.AwaitingActualContact));
            Assert.That(sink.Batches, Has.Count.EqualTo(2));
            Assert.That(sink.Batches.Last().Commands.Single().Kind,
                Is.EqualTo(AttackDefenseCommandKind.AttackContact));

            var defender = plan.Defense.Responsibilities.First(value =>
                value.Kind == DefenseResponsibilityKindV3.LineDefense);
            coordinator.AcceptContact(Fixture.Contact(
                plan,
                7,
                defender.Actor,
                AttackDefenseCommandKind.FloorDefense,
                plan.ReorganizationExits[0].Identity));

            Assert.That(coordinator.State.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned));
            Assert.That(sink.Batches.Last().Commands.Single().Kind,
                Is.EqualTo(AttackDefenseCommandKind.Reorganization));
        }

        [Test]
        public void CompleteReorganization_ResetsOpportunityButRetainsSequenceFloor()
        {
            var sink = new Sink();
            var coordinator = Fixture.CommittedAttack(sink, out var plan);
            coordinator.AcceptContact(Fixture.Contact(plan, 6, plan.SelectedAction.Actor,
                AttackDefenseCommandKind.AttackContact, ""));
            var defender = plan.Defense.Responsibilities.First(value =>
                value.Kind == DefenseResponsibilityKindV3.LineDefense);
            coordinator.AcceptContact(Fixture.Contact(plan, 7, defender.Actor,
                AttackDefenseCommandKind.FloorDefense,
                plan.ReorganizationExits[0].Identity));

            Assert.That(() => coordinator.CompleteReorganizationAndReset(5, 8),
                Throws.InvalidOperationException);
            coordinator.CompleteReorganizationAndReset(4, 8);

            Assert.That(coordinator.State.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.Idle));
            Assert.That(() => coordinator.PlanSetIntent(Fixture.Request(5, 8)),
                Throws.InvalidOperationException);
            Assert.That(() => coordinator.PlanSetIntent(Fixture.Request(5, 9)),
                Throws.Nothing);
        }

        private sealed class Sink : IAttackDefenseAuthorityCommandSink
        {
            public List<AttackDefenseCommandBatch> Batches { get; } = new List<AttackDefenseCommandBatch>();
            public void Publish(AttackDefenseCommandBatch batch) => Batches.Add(batch);
        }

        private static class Fixture
        {
            public static readonly Volleyball.Shared.Contracts.PlayerId Organizer = new Volleyball.Shared.Contracts.PlayerId("home-setter");
            public static SetIntentPlanningRequestV3 Request(long revision, long sequence, Volleyball.Shared.Contracts.TeamSide attackingSide = Volleyball.Shared.Contracts.TeamSide.Home)
            {
                var envelope = ExecutionEnvelopeFactoryV4.Create(MatchV4TestFixture.CreateDerived(),
                    new ExecutionIntentV4("gate-i-set", ExecutionCandidateCategoryV4.Set, new SimVector3(0f, 2f, 1f), new SimVector3(0f, 3f, 2f), .5f),
                    "gate-i-set-sample", ExecutionEnvelopePolicyV4.GateI);
                var sample = new ExecutionSampleV4(envelope.Identity, envelope.Sampling.SamplingKey, ExecutionCandidateCategoryV4.Set,
                    envelope.BaselineTarget, envelope.BaselineVelocity, envelope.RequestedEffort);
                var context = MatchV4TestFixture.CreateContext();
                var artifact = Volleyball.Presentation.PhysicalMatchRallyDirector.CreateTrajectoryPredictionProviderV4(context).Predict(
                    new BallTrajectoryPredictionRequestV4(Volleyball.Shared.Contracts.TeamSide.Home, revision,
                        new BallState(new SimVector3(0f, 3f, -2f), new SimVector3(0f, 4f, 1f), .12f),
                        new BallSimulationParameters(-9.8f, .9995f), context.PhysicsConfigurationHash, "gate-i-trajectory",
                        context.TrajectoryPredictionProviderConfiguration.PredictorVersion, context.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                        envelope.Identity, ExecutionDegradationStepV4.FullSampling));
                var derived = MatchV4TestFixture.CreateDerived();
                return new SetIntentPlanningRequestV3(revision, sequence, attackingSide, Organizer, 1f,
                    new BallState(new SimVector3(0f, 3f, -2f), new SimVector3(0f, 4f, 1f), .12f),
                    new[] { new GateITacticalPlayerV3(new Volleyball.Shared.Contracts.PlayerId("home-attacker"), attackingSide,
                        new SimVector3(0f, 2f, 1f), true, derived) }, derived, artifact);
            }

            public static AttackDefenseAuthorityCoordinator CommittedAttack(
                Sink sink, out AttackDefensePlanV3 plan)
            {
                var coordinator = new AttackDefenseAuthorityCoordinator(
                    new AttackDefensePlanner(), sink);
                var result = coordinator.PlanSetIntent(Request(4, 1));
                var accepted = new AcceptedSetEvidenceV3(
                    result.Intent.Organizer,
                    result.Intent.ExecutionClassification.ExecutableEnvelope.Identity,
                    result.Intent.TrajectoryArtifact.ArtifactIdentity);
                coordinator.AcceptSet(new GateIAcceptedSetV3(4, 2, accepted),
                    new AttackPlanningRequestV3(4, result.Intent, accepted,
                        Players()));
                coordinator.PublishThreat(4, 3);
                var defenders = Players().Where(value =>
                    value.Side == Volleyball.Shared.Contracts.TeamSide.Away).ToArray();
                var responsibilities = defenders.Select((value, index) =>
                    new DefenseResponsibilityV3(value.Player,
                        index == 0 ? DefenseResponsibilityKindV3.PrimaryBlock :
                        index == 1 ? DefenseResponsibilityKindV3.SupportingBlock :
                        DefenseResponsibilityKindV3.LineDefense,
                        "zone-" + index,
                        RallyPlanBranchV3.Primary)).ToArray();
                var exits = new[] { new ReorganizationExitV3(
                    "defense-exit", defenders[2].Player, "organize") };
                coordinator.CommitDefense(4, 4, new JointDefensePlanV3(
                    coordinator.PublicThreat.ThreatIdentity,
                    responsibilities,
                    exits,
                    new[] { "zone-0" },
                    new[] { "zone-1" }));
                coordinator.CommitFinalAttack(4, 5);
                plan = coordinator.State.Plan;
                return coordinator;
            }

            public static GateIContactEvidenceV3 Contact(
                AttackDefensePlanV3 plan,
                long sequence,
                Volleyball.Shared.Contracts.PlayerId actor,
                AttackDefenseCommandKind kind,
                string exit)
            {
                var candidate = plan.SelectedAction;
                var envelope = kind == AttackDefenseCommandKind.AttackContact
                    ? candidate.EnvelopeIdentity
                    : "gate-i-" + plan.Revision + "-" + (int)kind + "-" + actor.Value;
                var trajectory = kind == AttackDefenseCommandKind.AttackContact
                    ? candidate.TrajectoryArtifactIdentity
                    : plan.SetIntent.TrajectoryArtifact.ArtifactIdentity;
                return new GateIContactEvidenceV3(
                    plan.Revision,
                    sequence,
                    actor,
                    PlanCoverageReason.WithinConditionalEnvelope,
                    kind,
                    RallyPlanBranchV3.Primary,
                    envelope,
                    trajectory,
                    true,
                    exit);
            }

            private static GateITacticalPlayerV3[] Players()
            {
                var attributes = MatchV4TestFixture.CreateDerived();
                return Enumerable.Range(0, 6)
                    .Select(index => new GateITacticalPlayerV3(
                        new Volleyball.Shared.Contracts.PlayerId(
                            index == 0 ? "home-attacker" : "home-" + index),
                        Volleyball.Shared.Contracts.TeamSide.Home,
                        new SimVector3(index - 2, 0f, -2f),
                        index == 0,
                        attributes))
                    .Concat(Enumerable.Range(0, 6).Select(index =>
                        new GateITacticalPlayerV3(
                            new Volleyball.Shared.Contracts.PlayerId("away-" + index),
                            Volleyball.Shared.Contracts.TeamSide.Away,
                            new SimVector3(index - 2, 0f, 2f),
                            false,
                            attributes)))
                    .ToArray();
            }
        }
    }
}
