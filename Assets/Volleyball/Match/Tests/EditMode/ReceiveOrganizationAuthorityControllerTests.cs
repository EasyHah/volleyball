using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using RuntimePlayerId = Volleyball.Domain.Prototype.PlayerId;
using RuntimeTeamId = Volleyball.Domain.Prototype.TeamId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class ReceiveOrganizationAuthorityControllerTests
    {
        [Test]
        public void CommitReceive_StoresTheExactV4EvidenceOnTheFacade()
        {
            using var fixture = new ControllerFixture();
            var batch = fixture.ValidReceiveBatch();

            var receipt = fixture.Controller.PreflightAndCommit(batch);

            Assert.That(
                fixture.Primary.ScheduledExecutionEnvelopeV4,
                Is.SameAs(receipt.ExecutionClassification.ExecutableEnvelope));
            Assert.That(
                fixture.Primary.ScheduledExecutionSampleV4,
                Is.SameAs(receipt.ExecutionClassification.ExecutableSample));
            Assert.That(receipt.PlanRevision, Is.EqualTo(batch.PlanRevision));
            Assert.That(
                receipt.TrajectoryArtifact,
                Is.SameAs(fixture.Execution.TrajectoryArtifact));
            Assert.That(receipt.Perception,
                Is.SameAs(batch.Evidence.Perception));
        }

        [Test]
        public void PreflightFailure_LeavesEveryPlayerUnscheduled()
        {
            using var fixture = new ControllerFixture();
            var valid = fixture.ValidReceiveCommand(sourceSequence: 1);
            var invalid = new ReceiveOrganizationAuthorityCommand(
                7,
                1,
                ReceiveOrganizationCommandKind.EmergencyReceive,
                fixture.UndeclaredEmergency.StableId,
                RallyPlanBranchV3.Contingency,
                TeamRallyDecision.NoDecision,
                false,
                fixture.Execution);
            var batch = fixture.Batch(valid, invalid);

            Assert.That(
                () => fixture.Controller.PreflightAndCommit(batch),
                Throws.InvalidOperationException);
            Assert.That(
                fixture.Players.All(player =>
                    player.ScheduledExecutionEnvelopeV4 == null),
                Is.True);
            Assert.That(
                fixture.Players.All(player =>
                    !player.EmergencyReceiveWindowEnabled),
                Is.True);
        }

        [Test]
        public void PreparationCommands_UseOnlyDeclaredSetterAndAttacker()
        {
            using var fixture = new ControllerFixture();
            var setter = new ReceiveOrganizationAuthorityCommand(
                7,
                1,
                ReceiveOrganizationCommandKind.SetterPreparation,
                fixture.Setter.StableId,
                RallyPlanBranchV3.Primary,
                TeamRallyDecision.NoDecision,
                false,
                fixture.Execution);
            var attack = new ReceiveOrganizationAuthorityCommand(
                7,
                1,
                ReceiveOrganizationCommandKind.AttackPreparation,
                fixture.Attacker.StableId,
                RallyPlanBranchV3.Primary,
                fixture.AttackDecision,
                false,
                fixture.Execution);

            fixture.Controller.PreflightAndCommit(fixture.Batch(setter, attack));

            Assert.That(fixture.Setter.ReplayScheduledAction, Is.EqualTo("Set"));
            Assert.That(fixture.Attacker.ReplayScheduledAction, Is.EqualTo("Attack"));
            Assert.That(fixture.Primary.ReplayScheduledAction, Is.EqualTo("Ready"));
        }

        [Test]
        public void CancelCommittedContact_RejectsStaleOrCommittedCancellation()
        {
            using var fixture = new ControllerFixture();
            var committed = new ReceiveOrganizationAuthorityCommand(
                7,
                1,
                ReceiveOrganizationCommandKind.PrimaryReceive,
                fixture.Primary.StableId,
                RallyPlanBranchV3.Primary,
                fixture.ReceiveDecision,
                true,
                fixture.Execution);
            fixture.Controller.PreflightAndCommit(fixture.Batch(committed));
            var cancel = new ReceiveOrganizationAuthorityCommand(
                7,
                2,
                ReceiveOrganizationCommandKind.CancelUncommitted,
                fixture.Primary.StableId,
                RallyPlanBranchV3.Primary,
                TeamRallyDecision.NoDecision,
                false);

            Assert.That(
                () => fixture.Controller.PreflightAndCommit(
                    fixture.Batch(2, cancel)),
                Throws.InvalidOperationException);
            Assert.That(
                fixture.Primary.ScheduledExecutionEnvelopeV4,
                Is.SameAs(fixture.Execution.ExecutionClassification.ExecutableEnvelope));
        }

        private sealed class ControllerFixture : IDisposable
        {
            private readonly List<GameObject> _objects = new List<GameObject>();

            public ControllerFixture()
            {
                var stableIds = new[]
                {
                    "home-libero",
                    "home-setter",
                    "home-outside-a",
                    "home-outside-b",
                    "home-middle-a",
                    "home-opposite"
                };
                var roles = new[]
                {
                    PlayerRole.Defender,
                    PlayerRole.Setter,
                    PlayerRole.Attacker,
                    PlayerRole.OutsideHitter,
                    PlayerRole.MiddleBlocker,
                    PlayerRole.Opposite
                };
                var players = new List<PrototypePlayerAgent>();
                for (var index = 0; index < stableIds.Length; index++)
                {
                    var gameObject = new GameObject("GateH-" + stableIds[index]);
                    _objects.Add(gameObject);
                    var player = gameObject.AddComponent<PrototypePlayerAgent>();
                    player.Initialize(
                        new RuntimePlayerId(RuntimeTeamId.Blue, roles[index], index),
                        new StablePlayerId(stableIds[index]),
                        Color.blue,
                        (index + 1).ToString());
                    players.Add(player);
                }

                Players = players;
                Primary = players[0];
                Setter = players[1];
                Attacker = players[2];
                UndeclaredEmergency = players[4];
                Plan = new ReceiveOrganizationPlanV3(
                    TeamSide.Home,
                    7,
                    Primary.StableId,
                    Setter.StableId,
                    new[] { players[3].StableId, players[5].StableId },
                    new[] { Attacker.StableId, players[3].StableId },
                    Attacker.StableId,
                    new SimVector3(1.5f, 0f, -1.1f));
                ReceiveDecision = Decision(
                    Primary.Id,
                    TechniqueAction.Receive,
                    attack: false);
                AttackDecision = Decision(
                    Attacker.Id,
                    TechniqueAction.Attack,
                    attack: true);
                Execution = CreateExecution();
                Perception = CreatePerception(Plan, Primary.StableId);
                Evidence = new ReceiveOrganizationAuthorityEvidenceV3(
                    7,
                    1,
                    ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned,
                    Plan,
                    new SetterReachabilityEvidenceV3(
                        Setter.Id,
                        true,
                        true,
                        false,
                        true,
                        1f,
                        0.04f,
                        0.3f),
                    OrganizationFallbackReasonV3.None,
                    new PlanCoverageDecision(
                        PlanCoverageDecisionKind.CoveredActivateBranch,
                        "7",
                        PlanCoverageReason.RallyOpen,
                        Array.Empty<string>(),
                        0,
                        RallyPlanBranchV3.Primary),
                    null,
                    Perception);
                Controller = new ReceiveOrganizationAuthorityController(players);
            }

            public IReadOnlyList<PrototypePlayerAgent> Players { get; }

            public PrototypePlayerAgent Primary { get; }

            public PrototypePlayerAgent Setter { get; }

            public PrototypePlayerAgent Attacker { get; }

            public PrototypePlayerAgent UndeclaredEmergency { get; }

            public ReceiveOrganizationPlanV3 Plan { get; }

            public TeamRallyDecision ReceiveDecision { get; }

            public TeamRallyDecision AttackDecision { get; }

            public ReceiveOrganizationCommandExecutionV4 Execution { get; }

            public ReceiveOrganizationAuthorityEvidenceV3 Evidence { get; }
            public PerceptionReceiptV3 Perception { get; }

            public ReceiveOrganizationAuthorityController Controller { get; }

            public ReceiveOrganizationCommandBatch ValidReceiveBatch()
            {
                return Batch(ValidReceiveCommand(1));
            }

            public ReceiveOrganizationAuthorityCommand ValidReceiveCommand(
                long sourceSequence)
            {
                return new ReceiveOrganizationAuthorityCommand(
                    7,
                    sourceSequence,
                    ReceiveOrganizationCommandKind.PrimaryReceive,
                    Primary.StableId,
                    RallyPlanBranchV3.Primary,
                    ReceiveDecision,
                    false,
                    Execution);
            }

            public ReceiveOrganizationCommandBatch Batch(
                params ReceiveOrganizationAuthorityCommand[] commands)
            {
                return Batch(1, commands);
            }

            public ReceiveOrganizationCommandBatch Batch(
                long sourceSequence,
                params ReceiveOrganizationAuthorityCommand[] commands)
            {
                var evidence = sourceSequence == Evidence.SourceSequence
                    ? Evidence
                    : new ReceiveOrganizationAuthorityEvidenceV3(
                        7,
                        sourceSequence,
                        Evidence.Phase,
                        Plan,
                        Evidence.SetterEvidence,
                        Evidence.FallbackReason,
                        Evidence.CoverageDecision,
                        Evidence.ActualFirstPassLanding,
                        Evidence.Perception);
                return new ReceiveOrganizationCommandBatch(
                    7,
                    sourceSequence,
                    commands,
                    evidence);
            }

            public void Dispose()
            {
                foreach (var gameObject in _objects)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            private static TeamRallyDecision Decision(
                RuntimePlayerId actor,
                TechniqueAction action,
                bool attack)
            {
                var score = new RallyDecisionScore(1f, 1f, 0f, 0f, 2f);
                var candidate = new RallyDecisionCandidate(actor, true, score);
                if (!attack)
                {
                    return new TeamRallyDecision(
                        actor,
                        action,
                        new SimVector3(0f, 1f, -2f),
                        new SimVector3(0f, 0f, -2f),
                        new SimVector3(1.5f, 2f, -1.1f),
                        score,
                        new[] { candidate },
                        null);
                }

                var approach = new AttackApproachPlan(
                    new SimVector3(1f, 0f, -3f),
                    new SimVector3(2f, 0f, -2.45f),
                    1.14f,
                    0.8f,
                    0f);
                var contact = AttackContactPlanner.Plan(new AttackContactInput(
                    3.2f,
                    0.8f,
                    1f,
                    SetQualityGrade.A,
                    approach.Takeoff,
                    0.6f,
                    1f));
                return new TeamRallyDecision(
                    actor,
                    action,
                    contact.ContactCenter,
                    approach.ApproachStart,
                    new SimVector3(2f, 0f, 5f),
                    score,
                    new[] { candidate },
                    approach,
                    contact);
            }

            private static PerceptionReceiptV3 CreatePerception(
                ReceiveOrganizationPlanV3 plan, StablePlayerId selected)
            {
                var view = new TeamPerceptionSnapshotV3(
                    "gate-j-receive-view", "gate-j-receive-artifact",
                    plan.Side, plan.Revision, 1,
                    new[]
                    {
                        new PlayerPerceptionSnapshotV3(selected, .8f, .1f)
                    },
                    Array.Empty<PerceivedThreatEntryV3>(),
                    new[]
                    {
                        new PerceivedSupportCandidateV3(selected, .8f, .4f,
                            true)
                    });
                return new PerceptionReceiptV3("gate-j-v1", view,
                    new PerceptionSupportDecisionV3(selected, false, .8f));
            }

            private static ReceiveOrganizationCommandExecutionV4 CreateExecution()
            {
                var envelope = ExecutionEnvelopeFactoryV4.Create(
                    MatchV4TestFixture.CreateDerived(),
                    new ExecutionIntentV4(
                        "gate-h-receive",
                        ExecutionCandidateCategoryV4.Receive,
                        new SimVector3(0f, 1f, -2f),
                        new SimVector3(0f, 4f, -1f),
                        0.5f),
                    "gate-h-receive-sample",
                    ExecutionEnvelopePolicyV4.Default);
                var sample = new ExecutionSampleV4(
                    envelope.Identity,
                    envelope.Sampling.SamplingKey,
                    ExecutionCandidateCategoryV4.Receive,
                    envelope.BaselineTarget,
                    envelope.BaselineVelocity,
                    envelope.RequestedEffort);
                var context = MatchV4TestFixture.CreateContext();
                var parameters = new BallSimulationParameters(-9.8f, 0.9995f);
                var artifact = PhysicalMatchRallyDirector
                    .CreateTrajectoryPredictionProviderV4(context)
                    .Predict(new BallTrajectoryPredictionRequestV4(
                        TeamSide.Home,
                        7,
                        new BallState(
                            new SimVector3(0f, 3f, -2f),
                            new SimVector3(0f, 4f, 1f),
                            0.12f),
                        parameters,
                        context.PhysicsConfigurationHash,
                        "gate-h-trajectory",
                        context.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                        context.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                        envelope.Identity,
                        ExecutionDegradationStepV4.FullSampling));
                return new ReceiveOrganizationCommandExecutionV4(
                    2f,
                    0f,
                    default,
                    77,
                    envelope.Classify(sample),
                    artifact,
                    1.8f,
                    2.2f,
                    envelope.BaselineVelocity);
            }
        }
    }
}
