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
        }
    }
}
