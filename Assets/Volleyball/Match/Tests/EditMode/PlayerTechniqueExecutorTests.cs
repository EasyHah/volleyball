using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Volleyball.AI;
using Volleyball.Domain.Players;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using PrototypePlayerId = Volleyball.Domain.Prototype.PlayerId;
using PrototypePlayerRole = Volleyball.Domain.Prototype.PlayerRole;
using PrototypeTeamId = Volleyball.Domain.Prototype.TeamId;

namespace Volleyball.EditModeTests
{
    public sealed class PlayerTechniqueExecutorTests
    {
        [Test]
        public void ScheduleV4_StoresExecutableEnvelopeAndRejectsUnacceptedSample()
        {
            var executor = new PlayerTechniqueExecutor();
            var classification = CreateAcceptedClassification();

            executor.ScheduleV4(TechniqueAction.Attack, 2f, classification, default, 7, null, false, null);

            Assert.That(executor.ExecutionEnvelope, Is.SameAs(classification.ExecutableEnvelope));
            Assert.That(executor.ExecutionSample, Is.SameAs(classification.ExecutableSample));
            Assert.That(() => executor.ScheduleV4(
                TechniqueAction.Attack, 2f, CreateRejectedClassification(), default, 7, null, false, null),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ScheduleV4_RejectsUnknownClassificationKind()
        {
            var classification = CreateAcceptedClassification();
            typeof(ExecutionSampleClassificationV4)
                .GetField("<Kind>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(classification, (ExecutionSampleClassificationKindV4)999);

            Assert.That(
                () => new PlayerTechniqueExecutor().ScheduleV4(
                    TechniqueAction.Attack, 2f, classification, default, 7, null, false, null),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ValidateV4_RejectsCategoryMismatchWithoutMutatingExecutor()
        {
            var executor = new PlayerTechniqueExecutor();
            var accepted = CreateAcceptedClassification();
            var sample = accepted.ExecutableSample;
            typeof(ExecutionSampleV4)
                .GetField(
                    "<CandidateCategory>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(sample, ExecutionCandidateCategoryV4.Receive);

            Assert.That(
                () => PlayerTechniqueExecutor.ValidateV4(accepted),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(executor.ExecutionEnvelope, Is.Null);
            Assert.That(executor.ExecutionSample, Is.Null);
        }

        [Test]
        public void Facade_ProjectsV4EvidenceFromExecutorWithoutWritableCopies()
        {
            Assert.That(
                typeof(PrototypePlayerAgent)
                    .GetProperty(nameof(PrototypePlayerAgent.ScheduledExecutionEnvelopeV4))
                    .CanWrite,
                Is.False);
            Assert.That(
                typeof(PrototypePlayerAgent)
                    .GetProperty(nameof(PrototypePlayerAgent.ScheduledExecutionSampleV4))
                    .CanWrite,
                Is.False);
            Assert.That(
                typeof(PrototypePlayerAgent)
                    .GetProperty(nameof(PrototypePlayerAgent.ScheduledExecutionClassificationV4))
                    .CanWrite,
                Is.False);
        }

        [Test]
        public void ScheduleControlledHandlingContact_PreservesV4Evidence()
        {
            var playerObject = new GameObject("V4ControlledHandling");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(
                    new PrototypePlayerId(PrototypeTeamId.Blue, PrototypePlayerRole.Attacker),
                    Color.blue,
                    "2");
                var classification = CreateAcceptedClassification();
                var takeoff = new SimVector3(0f, 0f, -1f);
                var approach = new AttackApproachPlan(
                    new SimVector3(0f, 0f, -2f),
                    takeoff,
                    1f,
                    1f,
                    0f);
                var handlingPlan = new AttackContactPlan(
                    takeoff,
                    new SimVector3(0f, 0.5f, -1f),
                    1f,
                    1f,
                    0f,
                    1f,
                    AttackContactOutcome.Handling);

                player.ScheduleControlledHandlingContact(
                    2f,
                    classification,
                    default,
                    7,
                    approach,
                    handlingPlan,
                    0f);

                Assert.That(player.ScheduledExecutionEnvelopeV4, Is.SameAs(classification.ExecutableEnvelope));
                Assert.That(player.ScheduledExecutionSampleV4, Is.SameAs(classification.ExecutableSample));
                Assert.That(
                    player.ScheduledExecutionClassificationV4,
                    Is.SameAs(classification));
                Assert.That(player.ReplayScheduledAction, Is.EqualTo("Handling"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void FailedV4Reschedule_PreservesExistingExecutorTimelineAndProviderState()
        {
            var playerObject = new GameObject("AtomicV4Reschedule");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(new PrototypePlayerId(PrototypeTeamId.Blue, PrototypePlayerRole.Attacker), Color.blue, "2");
                var original = CreateAcceptedClassification();
                player.ScheduleContact(TechniqueAction.Attack, 2f, original, default, 71);

                var beforeEnvelope = player.ScheduledExecutionEnvelopeV4;
                var beforeSample = player.ScheduledExecutionSampleV4;
                var beforeClassification = player.ScheduledExecutionClassificationV4;
                var beforeAction = player.ReplayScheduledAction;
                var beforeMovement = player.ScheduledMovementTarget;
                var invalidApproach = new AttackApproachPlan(
                    new SimVector3(0f, 0f, -2f), new SimVector3(0f, 0f, -1f), 1f, 1f, 0f);

                Assert.That(() => player.ScheduleContact(
                        TechniqueAction.Receive, 3f, CreateAcceptedClassification(), default, 72,
                        attackApproach: invalidApproach),
                    Throws.TypeOf<ArgumentException>());

                Assert.That(player.ScheduledExecutionEnvelopeV4, Is.SameAs(beforeEnvelope));
                Assert.That(player.ScheduledExecutionSampleV4, Is.SameAs(beforeSample));
                Assert.That(player.ScheduledExecutionClassificationV4, Is.SameAs(beforeClassification));
                Assert.That(player.ReplayScheduledAction, Is.EqualTo(beforeAction));
                Assert.That(player.ScheduledMovementTarget, Is.EqualTo(beforeMovement));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }

        private static ExecutionSampleClassificationV4 CreateAcceptedClassification()
        {
            var envelope = CreateEnvelope();
            return envelope.Classify(SampleAtBaseline(envelope));
        }

        private static ExecutionSampleClassificationV4 CreateRejectedClassification()
        {
            var envelope = CreateEnvelope();
            return envelope.Classify(new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                envelope.BaselineTarget,
                new SimVector3(envelope.MaximumVelocity.X + 1f, 0f, 0f),
                envelope.RequestedEffort));
        }

        private static ExecutionEnvelopeV4 CreateEnvelope()
        {
            return ExecutionEnvelopeFactoryV4.Create(
                MatchV4TestFixture.CreateDerived(),
                new ExecutionIntentV4(
                    "player-technique-executor",
                    ExecutionCandidateCategoryV4.Attack,
                    new SimVector3(1f, 2f, 3f),
                    new SimVector3(4f, 5f, 6f),
                    0.5f),
                "player-technique-sample",
                ExecutionEnvelopePolicyV4.Default);
        }

        private static ExecutionSampleV4 SampleAtBaseline(ExecutionEnvelopeV4 envelope)
        {
            return new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                envelope.BaselineTarget,
                envelope.BaselineVelocity,
                envelope.RequestedEffort);
        }
    }
}
