using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public void ApplyPerception_RejectsWrongArtifactAndRetainsPublishedThreatEvent()
        {
            var coordinator = new AttackDefenseAuthorityCoordinator(
                new AttackDefensePlanner(), new Sink());
            var result = coordinator.PlanSetIntent(Fixture.Request(4, 1));
            var accepted = new AcceptedSetEvidenceV3(result.Intent.Organizer,
                result.Intent.ExecutionClassification.ExecutableEnvelope.Identity,
                result.Intent.TrajectoryArtifact.ArtifactIdentity);
            coordinator.AcceptSet(new GateIAcceptedSetV3(4, 2, accepted),
                new AttackPlanningRequestV3(4, result.Intent, accepted,
                    Fixture.Players()));
            coordinator.PublishThreat(4, 3);
            var selected = Fixture.Players().First(value =>
                value.Side == Volleyball.Shared.Contracts.TeamSide.Away).Player;

            Assert.That(() => coordinator.ApplyPerception(Fixture.Perception(
                    4, 3, "wrong-artifact", selected)),
                Throws.InvalidOperationException);

            var matching = Fixture.Perception(4, 3,
                result.Intent.TrajectoryArtifact.ArtifactIdentity, selected);
            coordinator.ApplyPerception(matching);
            Assert.That(coordinator.CurrentPerception, Is.SameAs(matching));
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
        public void ToolRecoveryContactEvidence_RetainsActualReboundAndFreshTouchFacts()
        {
            var evidence = new GateIContactEvidenceV3(4, 10, Fixture.Organizer,
                PlanCoverageReason.WithinConditionalEnvelope,
                AttackDefenseCommandKind.BlockContact, RallyPlanBranchV3.Primary,
                "block-envelope", "block-trajectory", true, "defense-exit",
                ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, 3);

            Assert.That(evidence.ToolRecoveryRebound,
                Is.EqualTo(ToolRecoveryReboundObservationV3.ReturnsToAttackingSide));
            Assert.That(evidence.RemainingTouchesAfterContact, Is.EqualTo(3));
            Assert.That(() => new GateIContactEvidenceV3(4, 10, Fixture.Organizer,
                PlanCoverageReason.WithinConditionalEnvelope,
                AttackDefenseCommandKind.BlockContact, RallyPlanBranchV3.Primary,
                "block-envelope", "block-trajectory", true, "defense-exit",
                ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, 4),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void AcceptedAttack_WaitsForDefenseBeforePublishingReorganization()
        {
            var sink = new Sink();
            var coordinator = Fixture.CommittedAttack(sink, out var plan);

            coordinator.AcceptContact(Fixture.Contact(coordinator,
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
            coordinator.AcceptContact(Fixture.Contact(coordinator,
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
        public void AcceptedBlock_UsesThePublishedExecutionEnvelopeIdentity()
        {
            var sink = new Sink();
            var coordinator = Fixture.CommittedAttack(sink, out var plan);
            coordinator.AcceptContact(Fixture.Contact(coordinator, plan, 6, plan.SelectedAction.Actor,
                AttackDefenseCommandKind.AttackContact, string.Empty));
            var block = sink.Batches.First().Commands.First(command =>
                command.Kind == AttackDefenseCommandKind.BlockContact);

            coordinator.AcceptContact(new GateIContactEvidenceV3(plan.Revision, 7,
                block.Actor, PlanCoverageReason.WithinConditionalEnvelope,
                AttackDefenseCommandKind.BlockContact, block.Branch,
                block.Execution.ExecutionClassification.ExecutableEnvelope.Identity,
                block.Execution.TrajectoryArtifact.ArtifactIdentity, true,
                plan.ReorganizationExits[0].Identity));

            Assert.That(coordinator.State.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned));
        }

        [Test]
        public void CommitDefense_BlockCommandsReserveTheirPublicNetCorridor()
        {
            var sink = new Sink();
            Fixture.CommittedAttack(sink, out var plan);
            var blocks = sink.Batches.First().Commands.Where(command =>
                command.Kind == AttackDefenseCommandKind.BlockContact).ToArray();

            Assert.That(blocks, Has.Length.EqualTo(2));
            Assert.That(blocks.Select(command => command.Execution.MovementTarget.X),
                Is.EqualTo(new[] { -1f, .55f }));
            Assert.That(blocks.Select(command => command.Execution.MovementTarget.X)
                .Distinct().Count(), Is.EqualTo(2));
            Assert.That(blocks.Select(command => command.Execution.MovementTarget.Z),
                Has.All.EqualTo(.35f));
            Assert.That(blocks.Select(command => command.Execution.ContactGroupId)
                .Distinct().Count(), Is.EqualTo(1));
            var attackGroup = sink.Batches.Last().Commands.Single(command =>
                command.Kind == AttackDefenseCommandKind.AttackContact)
                .Execution.ContactGroupId;
            Assert.That(blocks[0].Execution.ContactGroupId,
                Is.Not.EqualTo(attackGroup));
            Assert.That(blocks[0].Execution.ContactGroupId,
                Is.EqualTo(1000000066));
            Assert.That(attackGroup, Is.EqualTo(1000000065));
            foreach (var block in blocks)
            {
                var zone = block.Execution.MovementTarget.X < 0f ? "Line" : "Cross";
                var publishedArrival = plan.PublicThreat.Entries
                    .Where(entry => entry.Zone == zone)
                    .Select(entry => entry.ArrivalTime)
                    .DefaultIfEmpty(plan.PublicThreat.Entries.Min(entry =>
                        entry.ArrivalTime))
                    .Min();
                Assert.That(block.Execution.ScheduledSimulationTime,
                    Is.EqualTo(publishedArrival).Within(.00001f));
                Assert.That(block.Execution.ScheduledSimulationTime,
                    Is.GreaterThan(plan.SetIntent.AttackReadyArrivalTime));
            }
        }

        [Test]
        public void CompleteReorganization_ResetsOpportunityButRetainsSequenceFloor()
        {
            var sink = new Sink();
            var coordinator = Fixture.CommittedAttack(sink, out var plan);
            coordinator.AcceptContact(Fixture.Contact(coordinator, plan, 6, plan.SelectedAction.Actor,
                AttackDefenseCommandKind.AttackContact, ""));
            var defender = plan.Defense.Responsibilities.First(value =>
                value.Kind == DefenseResponsibilityKindV3.LineDefense);
            coordinator.AcceptContact(Fixture.Contact(coordinator, plan, 7, defender.Actor,
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

        [Test]
        public void IncidentalDefensePreview_ProducesLocalRevisionWithoutMutation()
        {
            var coordinator = Fixture.CommittedAttack(new Sink(), out var plan);
            coordinator.AcceptContact(Fixture.Contact(coordinator, plan, 6, plan.SelectedAction.Actor,
                AttackDefenseCommandKind.AttackContact, ""));
            var blocker = plan.Defense.Responsibilities.First(value =>
                value.Kind == DefenseResponsibilityKindV3.PrimaryBlock);

            var preview = coordinator.PreviewIncidentalDefenseContact(
                plan.Revision, 7, blocker.Actor, blocker.Branch,
                "actual-dig-envelope", "actual-dig-trajectory", true);

            Assert.That(preview.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.AwaitingActualContact));
            Assert.That(preview.Plan, Is.SameAs(plan));
            Assert.That(preview.CoverageDecision.Kind,
                Is.EqualTo(PlanCoverageDecisionKind.LocalRevision));
            Assert.That(coordinator.State.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.AwaitingActualContact));
        }

        [Test]
        public void IncidentalDefensePreview_RejectsActorOutsideDefenseRoster()
        {
            var coordinator = Fixture.AwaitingDefense(out var plan);

            Assert.That(() => coordinator.PreviewIncidentalDefenseContact(
                plan.Revision, 7,
                new Volleyball.Shared.Contracts.PlayerId("not-on-defense"),
                RallyPlanBranchV3.Primary, "actual", "trajectory", true),
                Throws.InvalidOperationException);
        }

        [Test]
        public void IncidentalDefensePreview_RejectsUnacceptedV3Contact()
        {
            var coordinator = Fixture.AwaitingDefense(out var plan);
            var blocker = plan.Defense.Responsibilities.First(value =>
                value.Kind == DefenseResponsibilityKindV3.PrimaryBlock);

            Assert.That(() => coordinator.PreviewIncidentalDefenseContact(
                plan.Revision, 7, blocker.Actor, blocker.Branch,
                "actual", "trajectory", false),
                Throws.InvalidOperationException);
        }

        [Test]
        public void ToolRecovery_ExpectedBlockThenDeclaredReceive_UsesTwoStageLifecycle()
        {
            var coordinator = Fixture.ToolRecoveryAwaitingAttack(out var plan, out var sink);

            coordinator.AcceptContact(Fixture.ToolContact(plan, 6, plan.SelectedAction.Actor,
                AttackDefenseCommandKind.AttackContact));
            Assert.That(coordinator.State.Phase, Is.EqualTo(AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock));
            coordinator.AcceptContact(Fixture.ToolContact(plan, 7,
                plan.SelectedAction.ToolRecoveryEvidence.Blocker,
                AttackDefenseCommandKind.BlockContact,
                ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, 3,
                Fixture.ToolRecoveryExecution(coordinator, plan)));
            Assert.That(coordinator.State.Phase, Is.EqualTo(AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive));
            Assert.That(sink.Batches, Has.Count.EqualTo(1),
                "A successful rebound must publish the exact declared recovery contact.");
            var recoveryCommand = sink.Batches.Single().Commands.Single();
            Assert.That(recoveryCommand.Kind, Is.EqualTo(AttackDefenseCommandKind.AttackCover));
            Assert.That(recoveryCommand.Actor,
                Is.EqualTo(plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor));
            Assert.That(recoveryCommand.Execution, Is.Not.Null);
            coordinator.AcceptContact(Fixture.ToolContact(plan, 8,
                plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor,
                AttackDefenseCommandKind.AttackCover,
                ToolRecoveryReboundObservationV3.NotApplicable, 2,
                recoveryCommand.Execution));
            Assert.That(coordinator.State.Phase, Is.EqualTo(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned));
            Assert.That(sink.Batches.Last().Commands.Single().Kind, Is.EqualTo(AttackDefenseCommandKind.Reorganization));
        }

        [Test]
        public void ToolRecovery_NonExpectedCommittedBlockerOrFailedRebound_UsesOrdinaryDefenseExit()
        {
            foreach (var rebound in new[] { ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, ToolRecoveryReboundObservationV3.ReturnsAway })
            {
                var coordinator = Fixture.ToolRecoveryAwaitingAttack(out var plan, out var sink);
                coordinator.AcceptContact(Fixture.ToolContact(plan, 6, plan.SelectedAction.Actor, AttackDefenseCommandKind.AttackContact));
                var actor = rebound == ToolRecoveryReboundObservationV3.ReturnsToAttackingSide
                    ? plan.Defense.Responsibilities.Single(value => value.Kind == DefenseResponsibilityKindV3.SupportingBlock).Actor
                    : plan.SelectedAction.ToolRecoveryEvidence.Blocker;
                coordinator.AcceptContact(Fixture.ToolContact(plan, 7, actor, AttackDefenseCommandKind.BlockContact, rebound, rebound == ToolRecoveryReboundObservationV3.ReturnsAway ? 3 : 0));
                Assert.That(coordinator.State.Phase, Is.EqualTo(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned));
                Assert.That(sink.Batches.Single().Commands.Single().Kind, Is.EqualTo(AttackDefenseCommandKind.Reorganization));
            }
        }

        [Test]
        public void ToolRecovery_RejectsUncommittedBlockerWrongOrStaleReceiverWithoutPublishing()
        {
            var coordinator = Fixture.ToolRecoveryAwaitingAttack(out var plan, out var sink);
            coordinator.AcceptContact(Fixture.ToolContact(plan, 6, plan.SelectedAction.Actor, AttackDefenseCommandKind.AttackContact));
            Assert.That(() => coordinator.AcceptContact(Fixture.ToolContact(plan, 7,
                new Volleyball.Shared.Contracts.PlayerId("not-committed"), AttackDefenseCommandKind.BlockContact,
                ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, 3)), Throws.InvalidOperationException);
            Assert.That(sink.Batches, Is.Empty);
            coordinator.AcceptContact(Fixture.ToolContact(plan, 7, plan.SelectedAction.ToolRecoveryEvidence.Blocker,
                AttackDefenseCommandKind.BlockContact, ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, 3,
                Fixture.ToolRecoveryExecution(coordinator, plan)));
            Assert.That(() => coordinator.AcceptContact(Fixture.ToolContact(plan, 8, plan.SelectedAction.Actor,
                AttackDefenseCommandKind.FloorDefense, ToolRecoveryReboundObservationV3.NotApplicable, 2)), Throws.InvalidOperationException);
            Assert.That(() => coordinator.AcceptContact(Fixture.ToolContact(plan, 7, plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor,
                AttackDefenseCommandKind.FloorDefense, ToolRecoveryReboundObservationV3.NotApplicable, 2)), Throws.InvalidOperationException);
            Assert.That(sink.Batches, Has.Count.EqualTo(1));
        }

        [Test]
        public void ToolRecovery_RejectsZeroTouchDeclaredReceive()
        {
            var coordinator = Fixture.ToolRecoveryAwaitingAttack(out var plan, out var sink);
            coordinator.AcceptContact(Fixture.ToolContact(plan, 6, plan.SelectedAction.Actor, AttackDefenseCommandKind.AttackContact));
            coordinator.AcceptContact(Fixture.ToolContact(plan, 7, plan.SelectedAction.ToolRecoveryEvidence.Blocker,
                AttackDefenseCommandKind.BlockContact, ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, 3,
                Fixture.ToolRecoveryExecution(coordinator, plan)));
            Assert.That(() => coordinator.AcceptContact(Fixture.ToolContact(plan, 8, plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor,
                AttackDefenseCommandKind.FloorDefense, ToolRecoveryReboundObservationV3.NotApplicable, 0)), Throws.InvalidOperationException);
            Assert.That(coordinator.State.Phase, Is.EqualTo(AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive));
            Assert.That(sink.Batches, Has.Count.EqualTo(1));
        }

        [Test]
        public void ToolRecovery_RejectsReceiveWithDifferentPublishedExecution()
        {
            var coordinator = Fixture.ToolRecoveryAwaitingAttack(out var plan, out var sink);
            coordinator.AcceptContact(Fixture.ToolContact(plan, 6, plan.SelectedAction.Actor,
                AttackDefenseCommandKind.AttackContact));
            coordinator.AcceptContact(Fixture.ToolContact(plan, 7,
                plan.SelectedAction.ToolRecoveryEvidence.Blocker,
                AttackDefenseCommandKind.BlockContact,
                ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, 3,
                Fixture.ToolRecoveryExecution(coordinator, plan)));

            var published = sink.Batches.Single().Commands.Single().Execution;
            var mismatched = new AttackDefenseCommandExecutionV4(
                published.ScheduledSimulationTime, published.MovementStartSimulationTime,
                published.ExecutionError, published.ContactGroupId + 1,
                published.ExecutionClassification, published.TrajectoryArtifact,
                published.MovementTarget, published.AttackApproach, published.AttackContactPlan,
                published.PhysicalContactCenter);
            Assert.That(() => coordinator.AcceptContact(Fixture.ToolContact(plan, 8,
                plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor,
                AttackDefenseCommandKind.FloorDefense,
                ToolRecoveryReboundObservationV3.NotApplicable, 2, mismatched)),
                Throws.InvalidOperationException);
            Assert.That(coordinator.State.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive));
        }

        [Test]
        public void ToolRecovery_RejectsSuccessfulBlockWithoutActualRecoveryExecution()
        {
            var coordinator = Fixture.ToolRecoveryAwaitingAttack(out var plan, out var sink);
            coordinator.AcceptContact(Fixture.ToolContact(plan, 6, plan.SelectedAction.Actor,
                AttackDefenseCommandKind.AttackContact));

            coordinator.AcceptContact(Fixture.ToolContact(plan, 7,
                plan.SelectedAction.ToolRecoveryEvidence.Blocker,
                AttackDefenseCommandKind.BlockContact,
                ToolRecoveryReboundObservationV3.ReturnsToAttackingSide, 3));
            Assert.That(coordinator.State.Phase,
                Is.EqualTo(AttackDefenseAuthorityPhaseV3.ReorganizationPlanned));
            Assert.That(sink.Batches.Single().Commands.Single().Kind,
                Is.EqualTo(AttackDefenseCommandKind.Reorganization));
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
                        index == 0 ? "Line" : index == 1 ? "Cross" : "Deep",
                        RallyPlanBranchV3.Primary)).ToArray();
                var exits = new[] { new ReorganizationExitV3(
                    "defense-exit", defenders[2].Player, "organize") };
                coordinator.CommitDefense(4, 4, new JointDefensePlanV3(
                    coordinator.PublicThreat.ThreatIdentity,
                    responsibilities,
                    exits,
                    new[] { "Line" },
                    new[] { "Cross" }));
                coordinator.CommitFinalAttack(4, 5);
                plan = coordinator.State.Plan;
                return coordinator;
            }

            public static AttackDefenseAuthorityCoordinator AwaitingDefense(
                out AttackDefensePlanV3 plan)
            {
                var coordinator = CommittedAttack(new Sink(), out plan);
                coordinator.AcceptContact(Contact(coordinator, plan, 6, plan.SelectedAction.Actor,
                    AttackDefenseCommandKind.AttackContact, ""));
                return coordinator;
            }

            public static GateIContactEvidenceV3 Contact(
                AttackDefenseAuthorityCoordinator coordinator,
                AttackDefensePlanV3 plan,
                long sequence,
                Volleyball.Shared.Contracts.PlayerId actor,
                AttackDefenseCommandKind kind,
                string exit)
            {
                var candidate = plan.SelectedAction;
                var execution = kind == AttackDefenseCommandKind.AttackContact
                    ? null
                    : (AttackDefenseCommandExecutionV4)typeof(AttackDefenseAuthorityCoordinator)
                        .GetMethod("ExecutionFor", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(coordinator, new object[] { actor, kind, null, null, 0 });
                var envelope = execution?.ExecutionClassification.ExecutableEnvelope.Identity ??
                    candidate.EnvelopeIdentity;
                var trajectory = execution?.TrajectoryArtifact.ArtifactIdentity ??
                    candidate.TrajectoryArtifactIdentity;
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

            public static AttackDefenseAuthorityCoordinator ToolRecoveryAwaitingAttack(
                out AttackDefensePlanV3 plan, out Sink sink)
            {
                sink = new Sink();
                var coordinator = new AttackDefenseAuthorityCoordinator(new AttackDefensePlanner(), sink);
                var intent = new AttackDefensePlanner().PlanSetIntent(Request(4, 1));
                var attacker = new Volleyball.Shared.Contracts.PlayerId("home-attacker");
                var blocker = new Volleyball.Shared.Contracts.PlayerId("away-0");
                var recovery = new Volleyball.Shared.Contracts.PlayerId("home-1");
                var exit = new ReorganizationExitV3("tool-exit", recovery, "organize");
                var recoveryEvidence = new ToolRecoveryEvidenceV3("tool", blocker,
                    Volleyball.Shared.Contracts.TeamSide.Home, recovery, 3, exit.Identity,
                    "tool-envelope", "tool-outbound", "tool-rebound", "tool-sample", "tool-contact");
                var candidate = new AttackCandidateV3("tool", attacker, AttackActionClassV3.BlockToolRecovery,
                    new SimVector3(0f, 2f, 1f), new SimVector3(0f, 1f, 3f), 1f, 1f, false,
                    string.Empty, "tool-envelope", "tool-outbound", exit.Identity, recoveryEvidence);
                var responsibilities = new[] {
                    new DefenseResponsibilityV3(blocker, DefenseResponsibilityKindV3.PrimaryBlock, "z0", RallyPlanBranchV3.Primary),
                    new DefenseResponsibilityV3(new Volleyball.Shared.Contracts.PlayerId("away-1"), DefenseResponsibilityKindV3.SupportingBlock, "z1", RallyPlanBranchV3.Primary),
                    new DefenseResponsibilityV3(new Volleyball.Shared.Contracts.PlayerId("away-2"), DefenseResponsibilityKindV3.LineDefense, "z2", RallyPlanBranchV3.Primary),
                    new DefenseResponsibilityV3(new Volleyball.Shared.Contracts.PlayerId("away-3"), DefenseResponsibilityKindV3.CrossDefense, "z3", RallyPlanBranchV3.Primary),
                    new DefenseResponsibilityV3(new Volleyball.Shared.Contracts.PlayerId("away-4"), DefenseResponsibilityKindV3.DeepDefense, "z4", RallyPlanBranchV3.Primary),
                    new DefenseResponsibilityV3(new Volleyball.Shared.Contracts.PlayerId("away-5"), DefenseResponsibilityKindV3.ReboundCoverage, "z5", RallyPlanBranchV3.Primary) };
                plan = new AttackDefensePlanV3(Volleyball.Shared.Contracts.TeamSide.Home, 4, "tool-plan", intent,
                    new[] { candidate }, new PublicAttackThreatV3("tool-threat", new[] { new PublicAttackThreatEntryV3(AttackActionClassV3.BlockToolRecovery, "z", 1f, 1f) }),
                    new JointDefensePlanV3("tool-threat", responsibilities, new[] { exit }, new[] { "z" }, new[] { "z" }), candidate, new[] { exit });
                var coverage = new PlanCoverageDecision(PlanCoverageDecisionKind.CoveredActivateBranch, "4", PlanCoverageReason.RallyOpen, new string[0], 0);
                var state = (AttackDefenseAuthorityStateV3)Activator.CreateInstance(
                    typeof(AttackDefenseAuthorityStateV3), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { AttackDefenseAuthorityPhaseV3.AttackCommitted, 4L,
                        Volleyball.Shared.Contracts.TeamSide.Home, plan, coverage }, null);
                typeof(AttackDefenseAuthorityCoordinator).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(coordinator, state);
                typeof(AttackDefenseAuthorityCoordinator).GetField("_lastSequence", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(coordinator, 5L);
                typeof(AttackDefenseAuthorityCoordinator).GetField("_intent", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(coordinator, intent);
                typeof(AttackDefenseAuthorityCoordinator).GetField("_players", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(coordinator, Players().ToDictionary(value => value.Player));
                return coordinator;
            }

            public static AttackDefenseCommandExecutionV4 ToolRecoveryExecution(
                AttackDefenseAuthorityCoordinator coordinator, AttackDefensePlanV3 plan) =>
                (AttackDefenseCommandExecutionV4)typeof(AttackDefenseAuthorityCoordinator)
                    .GetMethod("ExecutionFor", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(coordinator, new object[] {
                        plan.SelectedAction.ToolRecoveryEvidence.RecoveryActor,
                        AttackDefenseCommandKind.FloorDefense, null, null, 0
                    });

            public static GateIContactEvidenceV3 ToolContact(AttackDefensePlanV3 plan, long sequence,
                Volleyball.Shared.Contracts.PlayerId actor, AttackDefenseCommandKind kind,
                ToolRecoveryReboundObservationV3 rebound = ToolRecoveryReboundObservationV3.NotApplicable,
                int remainingTouches = -1,
                AttackDefenseCommandExecutionV4 recoveryExecution = null) => new GateIContactEvidenceV3(plan.Revision, sequence, actor,
                PlanCoverageReason.WithinConditionalEnvelope, kind, RallyPlanBranchV3.Primary,
                kind == AttackDefenseCommandKind.AttackContact ? plan.SelectedAction.EnvelopeIdentity : "actual-" + sequence,
                kind == AttackDefenseCommandKind.AttackContact ? plan.SelectedAction.TrajectoryArtifactIdentity : "actual-trajectory-" + sequence,
                true, string.Empty, rebound, remainingTouches, recoveryExecution);

            public static GateITacticalPlayerV3[] Players()
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

            public static PerceptionReceiptV3 Perception(long revision,
                long sourceSequence, string artifact,
                Volleyball.Shared.Contracts.PlayerId selected)
            {
                var view = new TeamPerceptionSnapshotV3(
                    "defense-view-" + revision, artifact,
                    Volleyball.Shared.Contracts.TeamSide.Away, revision,
                    sourceSequence,
                    new[] { new PlayerPerceptionSnapshotV3(selected, .8f, .1f) },
                    new[]
                    {
                        new PerceivedThreatEntryV3("published-0", "Line",
                            .8f, .4f)
                    },
                    new[]
                    {
                        new PerceivedSupportCandidateV3(selected, .8f, .5f,
                            false)
                    });
                return new PerceptionReceiptV3("gate-j-v1", view,
                    new PerceptionSupportDecisionV3(selected, false, .8f));
            }
        }
    }
}
