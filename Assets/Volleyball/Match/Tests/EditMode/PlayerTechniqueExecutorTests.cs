using System;
using NUnit.Framework;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;

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
