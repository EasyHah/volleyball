using System;
using System.Collections.Generic;
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
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.EditModeTests
{
    public sealed class AttackDefenseAuthorityControllerTests
    {
        [Test]
        public void Preflight_InvalidLastCommandMutatesNoPlayer()
        {
            using var fixture = new Fixture();
            var valid = fixture.Command(fixture.Attacker.StableId);
            var bench = new StablePlayerId("home-bench");
            var invalid = fixture.Command(bench);

            Assert.That(() => fixture.Controller.PreflightAndCommit(fixture.Batch(valid, invalid)),
                Throws.InvalidOperationException);
            Assert.That(fixture.Players, Has.All.Matches<PrototypePlayerAgent>(
                x => x.ScheduledExecutionEnvelopeV4 == null));
        }

        [Test]
        public void Preflight_RejectsCancelOfCommittedJump()
        {
            using var fixture = new Fixture();
            fixture.Controller.PreflightAndCommit(fixture.Batch(fixture.Command(fixture.Attacker.StableId, true)));
            var cancel = new AttackDefenseAuthorityCommand(7, 2,
                AttackDefenseCommandKind.CancelUncommitted, fixture.Attacker.StableId, false);

            Assert.That(() => fixture.Controller.PreflightAndCommit(fixture.Batch(cancel, 2)),
                Throws.InvalidOperationException);
        }

        [Test]
        public void CancelUncommitted_RequiresAndUsesExactGateICommandIdentity()
        {
            using var fixture = new Fixture();
            fixture.Controller.PreflightAndCommit(fixture.Batch(
                fixture.Command(fixture.Attacker.StableId, committed: false)));
            var cancel = new AttackDefenseAuthorityCommand(7, 2,
                AttackDefenseCommandKind.CancelUncommitted, fixture.Attacker.StableId,
                false, cancelTargetSourceSequence: 1,
                cancelTargetKind: AttackDefenseCommandKind.AttackContact);

            fixture.Controller.PreflightAndCommit(fixture.Batch(cancel, 2));

            Assert.That(fixture.Attacker.ScheduledExecutionEnvelopeV4, Is.Null);
        }

        [Test]
        public void Commit_UsesExactEnvelopeAndTrajectory()
        {
            using var fixture = new Fixture();
            var receipt = fixture.Controller.PreflightAndCommit(
                fixture.Batch(fixture.Command(fixture.Attacker.StableId)));

            Assert.That(receipt.ExecutionClassification.ExecutableEnvelope.Identity,
                Is.EqualTo(fixture.Execution.ExecutionClassification.ExecutableEnvelope.Identity));
            Assert.That(receipt.TrajectoryArtifact.ArtifactIdentity,
                Is.EqualTo(fixture.Execution.TrajectoryArtifact.ArtifactIdentity));
            Assert.That(fixture.Attacker.ScheduledTrajectoryPredictionArtifactV4,
                Is.SameAs(receipt.TrajectoryArtifact));
            Assert.That(receipt.Perception,
                Is.SameAs(fixture.Perception));
        }

        [Test]
        public void Commit_AllowsGateISoftAttackButKeepsExactEvidence()
        {
            using var fixture = new Fixture(ExecutionCandidateCategoryV4.SoftAction);

            fixture.Controller.PreflightAndCommit(fixture.Batch(
                fixture.Command(fixture.Attacker.StableId)));

            Assert.That(fixture.Attacker.ScheduledExecutionEnvelopeV4.CandidateCategory,
                Is.EqualTo(ExecutionCandidateCategoryV4.SoftAction));
        }

        [Test]
        public void Controller_HasNoSetContactCommandSurface()
        {
            Assert.That(Enum.GetNames(typeof(AttackDefenseCommandKind)),
                Does.Not.Contain("SetTargetPreparation"));
            Assert.That(Enum.GetNames(typeof(AttackDefenseCommandKind)),
                Does.Not.Contain("SetContact"));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> _objects = new List<GameObject>();
            public Fixture(ExecutionCandidateCategoryV4 category = ExecutionCandidateCategoryV4.Attack)
            {
                var players = new List<PrototypePlayerAgent>();
                var roles = new[] { PlayerRole.Attacker, PlayerRole.Setter, PlayerRole.Defender, PlayerRole.OutsideHitter, PlayerRole.MiddleBlocker, PlayerRole.Opposite };
                for (var index = 0; index < roles.Length; index++)
                {
                    var gameObject = new GameObject("GateI-" + index); _objects.Add(gameObject);
                    var player = gameObject.AddComponent<PrototypePlayerAgent>();
                    player.Initialize(new RuntimePlayerId(Volleyball.Domain.Prototype.TeamId.Blue, roles[index], index),
                        new Volleyball.Shared.Contracts.PlayerId("home-" + index), Color.blue, index.ToString());
                    players.Add(player);
                }
                Players = players; Attacker = players[0]; Execution = CreateExecution(category);
                var candidate = new AttackCandidateV3("attack", Attacker.StableId, AttackActionClassV3.Tip,
                    new SimVector3(1f, 3f, -2f), new SimVector3(1f, 1f, 3f), 1f, 1f, false,
                    string.Empty, Execution.ExecutionClassification.ExecutableEnvelope.Identity,
                    Execution.TrajectoryArtifact.ArtifactIdentity);
                var responsibilities = new List<DefenseResponsibilityV3>();
                for (var index = 0; index < players.Count; index++)
                    responsibilities.Add(new DefenseResponsibilityV3(players[index].StableId,
                        index == 0 ? DefenseResponsibilityKindV3.PrimaryBlock : DefenseResponsibilityKindV3.LineDefense,
                        "z" + index, RallyPlanBranchV3.Primary));
                var set = new GateISetIntentV3(7, 1, players[1].StableId, Attacker.StableId,
                    new SimVector3(0f, 2f, -1f), 1f, Execution.ExecutionClassification, Execution.TrajectoryArtifact);
                Plan = new AttackDefensePlanV3(TeamSide.Home, 7, "plan", set, new[] { candidate },
                    new PublicAttackThreatV3("threat", new[] { new PublicAttackThreatEntryV3(AttackActionClassV3.Tip, "z", 1f, 1f) }),
                    new JointDefensePlanV3("threat", responsibilities, new[] { new ReorganizationExitV3("exit", Attacker.StableId, "cover") }, Array.Empty<string>(), Array.Empty<string>()),
                    candidate, new[] { new ReorganizationExitV3("exit", Attacker.StableId, "cover") });
                Perception = CreatePerception(players[2].StableId);
                Controller = new AttackDefenseAuthorityController(players);
            }
            public IReadOnlyList<PrototypePlayerAgent> Players { get; }
            public PrototypePlayerAgent Attacker { get; }
            public AttackDefenseCommandExecutionV4 Execution { get; }
            public AttackDefensePlanV3 Plan { get; }
            public PerceptionReceiptV3 Perception { get; }
            public AttackDefenseAuthorityController Controller { get; }
            public AttackDefenseAuthorityCommand Command(StablePlayerId actor, bool committed = false) =>
                new AttackDefenseAuthorityCommand(7, 1, AttackDefenseCommandKind.AttackContact, actor, committed, Execution,
                    candidateIdentity: "attack");
            public AttackDefenseCommandBatch Batch(AttackDefenseAuthorityCommand command, long sequence = 1) => Batch(new[] { command }, sequence);
            public AttackDefenseCommandBatch Batch(AttackDefenseAuthorityCommand first, AttackDefenseAuthorityCommand second) => Batch(new[] { first, second }, 1);
            private AttackDefenseCommandBatch Batch(IReadOnlyList<AttackDefenseAuthorityCommand> commands, long sequence) =>
                new AttackDefenseCommandBatch(commands, new AttackDefenseAuthorityEvidenceV3(7, sequence,
                    AttackDefenseAuthorityPhaseV3.AttackCommitted, Plan,
                    PlanCoverageDecision.Covered("7", PlanCoverageReason.RallyOpen),
                    Perception));
            public void Dispose() { foreach (var item in _objects) UnityEngine.Object.DestroyImmediate(item); }
            private static AttackDefenseCommandExecutionV4 CreateExecution(ExecutionCandidateCategoryV4 category)
            {
                var envelope = ExecutionEnvelopeFactoryV4.Create(MatchV4TestFixture.CreateDerived(),
                    new ExecutionIntentV4("attack", category,
                        new SimVector3(1f, 3f, -2f), new SimVector3(1f, 4f, 2f), .5f), "sample", ExecutionEnvelopePolicyV4.GateI);
                var sample = new ExecutionSampleV4(envelope.Identity, envelope.Sampling.SamplingKey,
                    category, envelope.BaselineTarget, envelope.BaselineVelocity, envelope.RequestedEffort);
                var context = MatchV4TestFixture.CreateContext();
                var artifact = PhysicalMatchRallyDirector.CreateTrajectoryPredictionProviderV4(context).Predict(
                    new BallTrajectoryPredictionRequestV4(TeamSide.Home, 7,
                        new BallState(new SimVector3(0f, 3f, -2f), new SimVector3(0f, 4f, 1f), .12f),
                        new BallSimulationParameters(-9.8f, .9995f), context.PhysicsConfigurationHash,
                        "trajectory", context.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                        context.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                        envelope.Identity, ExecutionDegradationStepV4.FullSampling));
                var approach = new AttackApproachPlan(new SimVector3(1f, 0f, -3f), new SimVector3(2f, 0f, -2.45f), 1f, .8f, 0f);
                var contact = AttackContactPlanner.Plan(new AttackContactInput(3.2f, .8f, 1f, SetQualityGrade.A, approach.Takeoff, .6f, 1f));
                return new AttackDefenseCommandExecutionV4(2f, 0f, default, 77, envelope.Classify(sample), artifact,
                    new SimVector3(1f, 0f, -3f), approach, contact);
            }

            private static PerceptionReceiptV3 CreatePerception(
                StablePlayerId selected)
            {
                var view = new TeamPerceptionSnapshotV3(
                    "gate-j-defense-view", "gate-j-defense-artifact",
                    TeamSide.Away, 7, 1,
                    new[]
                    {
                        new PlayerPerceptionSnapshotV3(selected, .8f, .1f)
                    },
                    new[]
                    {
                        new PerceivedThreatEntryV3(
                            "threat-0", "z", .8f, 1f)
                    },
                    new[]
                    {
                        new PerceivedSupportCandidateV3(selected, .8f, .4f,
                            false)
                    });
                return new PerceptionReceiptV3("gate-j-v1", view,
                    new PerceptionSupportDecisionV3(selected, false, .8f));
            }
        }
    }
}
