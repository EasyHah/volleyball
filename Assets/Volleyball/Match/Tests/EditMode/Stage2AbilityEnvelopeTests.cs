using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using PrototypePlayerId = Volleyball.Domain.Prototype.PlayerId;
using PrototypePlayerRole = Volleyball.Domain.Prototype.PlayerRole;
using PrototypeTeamId = Volleyball.Domain.Prototype.TeamId;

namespace Volleyball.EditModeTests
{
    public sealed class Stage2ExecutionEnvelopeTests
    {
        [Test]
        public void Create_SameInputsProduceEqualIdentityAndCanonicalBytes()
        {
            var first = CreateEnvelope();
            var second = CreateEnvelope();

            Assert.That(second.Identity, Is.EqualTo(first.Identity));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(second.GetHashCode(), Is.EqualTo(first.GetHashCode()));
            CollectionAssert.AreEqual(first.ToCanonicalBytes(), second.ToCanonicalBytes());
        }

        [Test]
        public void PlannerAndExecutor_UseTheExactSameEnvelopeInstance()
        {
            var planned = PhysicalMatchRallyDirector.PlanExecutionEnvelopeV4(
                MatchV4TestFixture.CreateDerived(),
                CreateIntent(),
                "sample-17",
                CreatePolicy());
            var sample = SampleAtBaseline(planned);

            var executed = PhysicalMatchRallyDirector.ExecuteExecutionSampleV4(planned, sample);

            Assert.That(executed.TestedEnvelope, Is.SameAs(planned));
            Assert.That(executed.Sample, Is.SameAs(sample));
            Assert.That(executed.Kind, Is.EqualTo(ExecutionSampleClassificationKindV4.Accepted));
        }

        [Test]
        public void Create_EmitsExactFactoryReadEvidenceAndExpansionPreservesIt()
        {
            var envelope = CreateEnvelope(
                policy: CreatePolicy(allowedExpansionCount: 1));
            var classification = envelope.Classify(
                SampleWithTargetErrorScale(envelope, 1.2f));

            Assert.That(
                envelope.AbilityConsumptions,
                Has.Count.EqualTo(3));
            Assert.That(
                envelope.AbilityConsumptions[0].AttributeName,
                Is.EqualTo("Attack.DirectionControl"));
            Assert.That(
                envelope.AbilityConsumptions[0].Value,
                Is.EqualTo(
                    MatchV4TestFixture.CreateDerived()
                        .Attributes.Attack.DirectionControl));
            Assert.That(
                envelope.AbilityConsumptions[1].AttributeName,
                Is.EqualTo("Attack.SpeedControl"));
            Assert.That(
                envelope.AbilityConsumptions[2].AttributeName,
                Is.EqualTo("Attack.PowerCapacity"));
            Assert.That(
                envelope.AbilityConsumptions[0].EvidenceKind,
                Is.EqualTo("ExecutionEnvelopeFactoryRead"));
            Assert.That(
                classification.Kind,
                Is.EqualTo(
                    ExecutionSampleClassificationKindV4.EnvelopeExpanded));
            Assert.That(
                classification.ExecutableEnvelope
                    .AbilityConsumptions[0],
                Is.SameAs(envelope.AbilityConsumptions[0]));
        }

        [TestCase(
            ExecutionCandidateCategoryV4.SoftAction,
            "Set.SoftTouch")]
        [TestCase(
            ExecutionCandidateCategoryV4.Defense,
            "Defense.PlatformControl")]
        public void Create_GateICategoryConsumesOnlyDeclaredControl(
            ExecutionCandidateCategoryV4 category,
            string expectedControl)
        {
            var envelope = ExecutionEnvelopeFactoryV4.Create(
                MatchV4TestFixture.CreateDerived(),
                new ExecutionIntentV4(
                    "gate-i-" + category,
                    category,
                    new SimVector3(1f, 2f, 3f),
                    new SimVector3(1f, 1f, 1f),
                    0.1f),
                "gate-i-category-" + category,
                ExecutionEnvelopePolicyV4.GateI);

            Assert.That(
                envelope.AbilityConsumptions.Select(value => value.AttributeName),
                Does.Contain(expectedControl));
            Assert.That(
                envelope.AbilityConsumptions.Select(value => value.AttributeName),
                Does.Not.Contain("Receive.FirstTouchControl"));
        }

        [Test]
        public void HistoricalDefaultPolicyIdentityRemainsStable()
        {
            var historical = new ExecutionEnvelopePolicyV4(
                ExecutionEnvelopeV4.CurrentVersion,
                1,
                new[]
                {
                    ExecutionCandidateCategoryV4.Receive,
                    ExecutionCandidateCategoryV4.Set,
                    ExecutionCandidateCategoryV4.Attack,
                    ExecutionCandidateCategoryV4.Block,
                    ExecutionCandidateCategoryV4.Serve
                },
                7,
                2,
                0,
                1.5f,
                new[]
                {
                    ExecutionDegradationStepV4.FullSampling,
                    ExecutionDegradationStepV4.ReducedSampleCount,
                    ExecutionDegradationStepV4.CachedCoarseDistribution,
                    ExecutionDegradationStepV4.DeterministicSafeFallback
                },
                BoundedErrorDistributionKindV4.BoundedUniform,
                BoundedErrorDistributionKindV4.BoundedUniform);

            Assert.That(ExecutionEnvelopePolicyV4.Default, Is.EqualTo(historical));
            CollectionAssert.AreEqual(
                ExecutionEnvelopePolicyV4.Default.ToCanonicalBytes(),
                historical.ToCanonicalBytes());
        }

        [Test]
        public void PhysicalExecutor_UsesExactEnvelopeSampleAndVelocity()
        {
            var derived = MatchV4TestFixture.CreateDerived();
            var envelope = CreateEnvelope(derived: derived);
            var sample = new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                envelope.BaselineTarget,
                envelope.BaselineVelocity,
                envelope.RequestedEffort);
            var classification = envelope.Classify(sample);
            var noError = new SkillExecutionError(
                0f,
                SimVector3.Zero,
                SimVector3.Zero,
                0f,
                1f,
                SimVector3.Zero,
                1f);
            var playerObject = new GameObject("V4EnvelopeExecutor");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(
                    new PrototypePlayerId(
                        PrototypeTeamId.Blue,
                        PrototypePlayerRole.Attacker),
                    Color.blue,
                    "2");
                player.SetAbility(new PlayerAbilityProfile(derived));

                player.ScheduleContact(
                    TechniqueAction.Attack,
                    2f,
                    classification,
                    noError,
                    contactGroupId: 171);
                var contacts = new System.Collections.Generic.List<BallContactCandidate>();
                player.CollectContacts(2f, 1f / 120f, contacts);

                Assert.That(player.ScheduledExecutionEnvelopeV4, Is.SameAs(envelope));
                Assert.That(player.ScheduledExecutionSampleV4, Is.SameAs(sample));
                Assert.That(
                    player.ScheduledExecutionClassificationV4,
                    Is.SameAs(classification));
                Assert.That(contacts, Is.Not.Empty);
                Assert.That(contacts[0].TargetVelocity, Is.EqualTo(sample.Velocity));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void PhysicalExecutor_ExceededSampleIsNotScheduledOrApplied()
        {
            var derived = MatchV4TestFixture.CreateDerived();
            var envelope = CreateEnvelope(derived: derived);
            var sample = new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                envelope.BaselineTarget,
                new SimVector3(envelope.MaximumVelocity.X + 1f, 0f, 0f),
                envelope.RequestedEffort);
            var classification = envelope.Classify(sample);
            var noError = new SkillExecutionError(
                0f,
                SimVector3.Zero,
                SimVector3.Zero,
                0f,
                1f,
                SimVector3.Zero,
                1f);
            var playerObject = new GameObject("V4ExceededExecutor");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(
                    new PrototypePlayerId(
                        PrototypeTeamId.Blue,
                        PrototypePlayerRole.Attacker),
                    Color.blue,
                    "2");
                player.SetAbility(new PlayerAbilityProfile(derived));

                Assert.That(
                    classification.Kind,
                    Is.EqualTo(ExecutionSampleClassificationKindV4.EnvelopeExceeded));
                Assert.Throws<System.InvalidOperationException>(
                    () => player.ScheduleContact(
                        TechniqueAction.Attack,
                        2f,
                        classification,
                        noError,
                        contactGroupId: 172));
                var contacts = new System.Collections.Generic.List<BallContactCandidate>();
                player.CollectContacts(2f, 1f / 120f, contacts);

                Assert.That(contacts, Is.Empty);
                Assert.That(player.ScheduledExecutionEnvelopeV4, Is.Null);
                Assert.That(player.ScheduledExecutionSampleV4, Is.Null);
                Assert.That(player.ScheduledExecutionClassificationV4, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void PhysicalExecutor_ExpandedSampleUsesNewEnvelopeIdentity()
        {
            var derived = MatchV4TestFixture.CreateDerived();
            var envelope = CreateEnvelope(
                derived: derived,
                policy: CreatePolicy(allowedExpansionCount: 1));
            var sample = SampleWithTargetErrorScale(envelope, 1.2f);
            var classification = envelope.Classify(sample);
            var noError = new SkillExecutionError(
                0f,
                SimVector3.Zero,
                SimVector3.Zero,
                0f,
                1f,
                SimVector3.Zero,
                1f);
            var playerObject = new GameObject("V4ExpandedExecutor");
            try
            {
                var player = playerObject.AddComponent<PrototypePlayerAgent>();
                player.Initialize(
                    new PrototypePlayerId(
                        PrototypeTeamId.Blue,
                        PrototypePlayerRole.Attacker),
                    Color.blue,
                    "2");
                player.SetAbility(new PlayerAbilityProfile(derived));

                player.ScheduleContact(
                    TechniqueAction.Attack,
                    2f,
                    classification,
                    noError,
                    contactGroupId: 173);
                var contacts = new System.Collections.Generic.List<BallContactCandidate>();
                player.CollectContacts(2f, 1f / 120f, contacts);

                Assert.That(
                    classification.Kind,
                    Is.EqualTo(ExecutionSampleClassificationKindV4.EnvelopeExpanded));
                Assert.That(
                    player.ScheduledExecutionClassificationV4,
                    Is.SameAs(classification));
                Assert.That(
                    player.ScheduledExecutionEnvelopeV4,
                    Is.SameAs(classification.ExpandedEnvelope));
                Assert.That(
                    player.ScheduledExecutionSampleV4,
                    Is.SameAs(classification.ExecutableSample));
                Assert.That(
                    player.ScheduledExecutionSampleV4.EnvelopeIdentity,
                    Is.EqualTo(classification.ExpandedEnvelopeIdentity));
                Assert.That(player.ScheduledExecutionSampleV4.Target, Is.EqualTo(sample.Target));
                Assert.That(player.ScheduledExecutionSampleV4.Velocity, Is.EqualTo(sample.Velocity));
                Assert.That(contacts, Is.Not.Empty);
                Assert.That(
                    contacts[0].TargetVelocity,
                    Is.EqualTo(player.ScheduledExecutionSampleV4.Velocity));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void Identity_IncludesEveryBoundaryEffortDistributionSamplingSourceAndVersion()
        {
            var baseline = CreateEnvelope();
            var variants = new[]
            {
                CreateEnvelope(derived: MatchV4TestFixture.CreateDerived(attackTechnique: 0.42f)),
                CreateEnvelope(derived: MatchV4TestFixture.CreateDerived(attackPower: 0.42f)),
                CreateEnvelope(intent: CreateIntent(sourceIdentity: "intent-2")),
                CreateEnvelope(intent: CreateIntent(target: new SimVector3(1.01f, 2f, 3f))),
                CreateEnvelope(intent: CreateIntent(velocity: new SimVector3(4.01f, 5f, 6f))),
                CreateEnvelope(intent: CreateIntent(requestedEffort: 0.51f)),
                CreateEnvelope(samplingKey: "sample-18"),
                CreateEnvelope(policy: CreatePolicy(envelopeVersion: 5)),
                CreateEnvelope(policy: CreatePolicy(policyVersion: 2)),
                CreateEnvelope(policy: CreatePolicy(sampleCount: 9)),
                CreateEnvelope(policy: CreatePolicy(
                    candidateOrder: new[]
                    {
                        ExecutionCandidateCategoryV4.Attack,
                        ExecutionCandidateCategoryV4.Receive,
                        ExecutionCandidateCategoryV4.Set,
                        ExecutionCandidateCategoryV4.Block,
                        ExecutionCandidateCategoryV4.Serve
                    })),
                CreateEnvelope(policy: CreatePolicy(
                    degradationLadder: new[]
                    {
                        ExecutionDegradationStepV4.FullSampling,
                        ExecutionDegradationStepV4.DeterministicSafeFallback
                    })),
                CreateEnvelope(policy: CreatePolicy(maximumExpansionCount: 3)),
                CreateEnvelope(policy: CreatePolicy(allowedExpansionCount: 1)),
                CreateEnvelope(policy: CreatePolicy(perStepExpansionFactor: 1.4f)),
                CreateEnvelope(policy: CreatePolicy(
                    targetDistributionKind: BoundedErrorDistributionKindV4.SymmetricTriangular)),
                CreateEnvelope(policy: CreatePolicy(
                    velocityDistributionKind: BoundedErrorDistributionKindV4.SymmetricTriangular))
            };

            foreach (var variant in variants)
            {
                Assert.That(variant.Identity, Is.Not.EqualTo(baseline.Identity));
                CollectionAssert.AreNotEqual(variant.ToCanonicalBytes(), baseline.ToCanonicalBytes());
            }
        }

        [Test]
        public void AttackDirectionControl_ChangesOnlyTargetErrorBounds()
        {
            var lowDirection = CreateEnvelope(
                derived: CreateDerivedForIndependentControls(courtAwareness: 0.2f));
            var highDirection = CreateEnvelope(
                derived: CreateDerivedForIndependentControls(courtAwareness: 0.9f));

            Assert.That(
                highDirection.TargetError.MaximumAbsoluteError.Magnitude,
                Is.LessThan(lowDirection.TargetError.MaximumAbsoluteError.Magnitude));
            Assert.That(highDirection.VelocityError, Is.EqualTo(lowDirection.VelocityError));
            Assert.That(highDirection.MaximumVelocity, Is.EqualTo(lowDirection.MaximumVelocity));
            Assert.That(highDirection.MaximumEffort, Is.EqualTo(lowDirection.MaximumEffort));
        }

        [Test]
        public void AttackSpeedControl_ChangesOnlyVelocityErrorBounds()
        {
            var lowSpeed = CreateEnvelope(
                derived: CreateDerivedForIndependentControls(softTouch: 0.2f));
            var highSpeed = CreateEnvelope(
                derived: CreateDerivedForIndependentControls(softTouch: 0.9f));

            Assert.That(
                highSpeed.VelocityError.MaximumAbsoluteError.Magnitude,
                Is.LessThan(lowSpeed.VelocityError.MaximumAbsoluteError.Magnitude));
            Assert.That(highSpeed.TargetError, Is.EqualTo(lowSpeed.TargetError));
            Assert.That(highSpeed.MaximumVelocity, Is.EqualTo(lowSpeed.MaximumVelocity));
            Assert.That(highSpeed.MaximumEffort, Is.EqualTo(lowSpeed.MaximumEffort));
        }

        [Test]
        public void AttackPower_ChangesMaximumVelocityAndEffortWithoutChangingErrorBounds()
        {
            var lowPower = CreateEnvelope(
                derived: MatchV4TestFixture.CreateDerived(attackTechnique: 0.7f, attackPower: 0.2f));
            var highPower = CreateEnvelope(
                derived: MatchV4TestFixture.CreateDerived(attackTechnique: 0.7f, attackPower: 0.9f));

            Assert.That(highPower.MaximumVelocity.Magnitude, Is.GreaterThan(lowPower.MaximumVelocity.Magnitude));
            Assert.That(highPower.MaximumEffort, Is.GreaterThan(lowPower.MaximumEffort));
            Assert.That(highPower.TargetError, Is.EqualTo(lowPower.TargetError));
            Assert.That(highPower.VelocityError, Is.EqualTo(lowPower.VelocityError));
        }

        [Test]
        public void Classify_NonFiniteSampleIsUnexpected()
        {
            var envelope = CreateEnvelope();
            var sample = new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                new SimVector3(float.NaN, 2f, 3f),
                envelope.BaselineVelocity,
                envelope.RequestedEffort);

            var result = envelope.Classify(sample);

            Assert.That(result.Kind, Is.EqualTo(ExecutionSampleClassificationKindV4.UnexpectedExecutionSample));
            CollectionAssert.Contains(result.OffendingDimensions, "target.x");
            Assert.That(result.TestedEnvelopeIdentity, Is.EqualTo(envelope.Identity));
        }

        [Test]
        public void Classify_FiniteSampleOutsideMaximumVelocityIsExceeded()
        {
            var envelope = CreateEnvelope();
            var sample = new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                envelope.BaselineTarget,
                new SimVector3(envelope.MaximumVelocity.X + 0.01f, 0f, 0f),
                envelope.RequestedEffort);

            var result = envelope.Classify(sample);

            Assert.That(result.Kind, Is.EqualTo(ExecutionSampleClassificationKindV4.EnvelopeExceeded));
            CollectionAssert.Contains(result.OffendingDimensions, "velocity.maximum.x");
        }

        [Test]
        public void Classify_ExpansionRequiresExplicitAllowedPolicyStepAndRecordsBothIdentities()
        {
            var prohibited = CreateEnvelope(policy: CreatePolicy(allowedExpansionCount: 0));
            var allowed = CreateEnvelope(policy: CreatePolicy(allowedExpansionCount: 1));
            var outsideCurrent = new SimVector3(
                allowed.BaselineTarget.X + (allowed.TargetError.MaximumAbsoluteError.X * 1.2f),
                allowed.BaselineTarget.Y,
                allowed.BaselineTarget.Z);
            var prohibitedSample = new ExecutionSampleV4(
                prohibited.Identity,
                prohibited.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                outsideCurrent,
                prohibited.BaselineVelocity,
                prohibited.RequestedEffort);
            var allowedSample = new ExecutionSampleV4(
                allowed.Identity,
                allowed.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                outsideCurrent,
                allowed.BaselineVelocity,
                allowed.RequestedEffort);

            var withoutPolicyStep = prohibited.Classify(prohibitedSample);
            var withPolicyStep = allowed.Classify(allowedSample);

            Assert.That(
                withoutPolicyStep.Kind,
                Is.EqualTo(ExecutionSampleClassificationKindV4.EnvelopeExceeded));
            Assert.That(
                withPolicyStep.Kind,
                Is.EqualTo(ExecutionSampleClassificationKindV4.EnvelopeExpanded));
            Assert.That(withPolicyStep.TestedEnvelopeIdentity, Is.EqualTo(allowed.Identity));
            Assert.That(withPolicyStep.ExpandedEnvelope, Is.Not.Null);
            Assert.That(
                withPolicyStep.ExpandedEnvelopeIdentity,
                Is.EqualTo(withPolicyStep.ExpandedEnvelope.Identity));
            Assert.That(
                withPolicyStep.ExpandedEnvelope.Expansion.CurrentExpansionCount,
                Is.EqualTo(1));
            CollectionAssert.AreNotEqual(
                allowed.ToCanonicalBytes(),
                withPolicyStep.ExpandedEnvelope.ToCanonicalBytes(),
                "Expansion count must be part of canonical identity.");
        }

        [Test]
        public void Classify_SequentialExplicitExpansionsProduceNewImmutableEnvelopes()
        {
            var initial = CreateEnvelope(policy: CreatePolicy(allowedExpansionCount: 2));
            var firstSample = SampleWithTargetErrorScale(initial, 1.2f);

            var first = initial.Classify(firstSample);
            var secondEnvelope = first.ExpandedEnvelope;
            var secondSample = SampleWithTargetErrorScale(secondEnvelope, 1.8f);
            var second = secondEnvelope.Classify(secondSample);

            Assert.That(first.Kind, Is.EqualTo(ExecutionSampleClassificationKindV4.EnvelopeExpanded));
            Assert.That(second.Kind, Is.EqualTo(ExecutionSampleClassificationKindV4.EnvelopeExpanded));
            Assert.That(second.ExpandedEnvelope.Expansion.CurrentExpansionCount, Is.EqualTo(2));
            Assert.That(second.ExpandedEnvelopeIdentity, Is.Not.EqualTo(first.ExpandedEnvelopeIdentity));
            var acceptedSample = SampleWithTargetErrorScale(
                second.ExpandedEnvelope,
                1.8f);
            Assert.That(
                second.ExpandedEnvelope.Classify(acceptedSample).Kind,
                Is.EqualTo(ExecutionSampleClassificationKindV4.Accepted));
        }

        [Test]
        public void Classify_ReturnsOriginalSampleWithoutClampOrRepair()
        {
            var envelope = CreateEnvelope();
            var sample = new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                new SimVector3(envelope.BaselineTarget.X + 10f, 2f, 3f),
                new SimVector3(envelope.MaximumVelocity.X + 10f, 5f, 6f),
                envelope.MaximumEffort + 0.1f);

            var result = envelope.Classify(sample);

            Assert.That(result.Kind, Is.EqualTo(ExecutionSampleClassificationKindV4.EnvelopeExceeded));
            Assert.That(result.Sample, Is.SameAs(sample));
            Assert.That(result.Sample.Target, Is.EqualTo(sample.Target));
            Assert.That(result.Sample.Velocity, Is.EqualTo(sample.Velocity));
            Assert.That(result.Sample.Effort, Is.EqualTo(sample.Effort));
        }

        [Test]
        public void Classify_WrongEnvelopeIdentityIsUnexpectedBeforeBoundsChecks()
        {
            var envelope = CreateEnvelope();
            var sample = new ExecutionSampleV4(
                "wrong-envelope",
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                new SimVector3(envelope.BaselineTarget.X + 100f, 2f, 3f),
                envelope.BaselineVelocity,
                envelope.RequestedEffort);

            var result = envelope.Classify(sample);

            Assert.That(result.Kind, Is.EqualTo(ExecutionSampleClassificationKindV4.UnexpectedExecutionSample));
            CollectionAssert.Contains(result.OffendingDimensions, "envelopeIdentity");
        }

        [Test]
        public void Create_RejectsRequestedEffortAbovePowerCapacity()
        {
            var derived = MatchV4TestFixture.CreateDerived(
                attackTechnique: 0.7f,
                attackPower: 0f);

            Assert.Throws<System.ArgumentException>(
                () => CreateEnvelope(
                    derived: derived,
                    intent: CreateIntent(requestedEffort: 1f)));
        }

        [Test]
        public void Create_RejectsBaselineVelocityAbovePowerCapacity()
        {
            Assert.Throws<System.ArgumentException>(
                () => CreateEnvelope(
                    intent: CreateIntent(
                        velocity: new SimVector3(100f, 0f, 0f))));
        }

        private static ExecutionEnvelopeV4 CreateEnvelope(
            DerivedMatchAttributesV4 derived = null,
            ExecutionIntentV4 intent = null,
            string samplingKey = "sample-17",
            ExecutionEnvelopePolicyV4 policy = null)
        {
            return ExecutionEnvelopeFactoryV4.Create(
                derived ?? MatchV4TestFixture.CreateDerived(),
                intent ?? CreateIntent(),
                samplingKey,
                policy ?? CreatePolicy());
        }

        private static ExecutionIntentV4 CreateIntent(
            string sourceIdentity = "intent-1",
            SimVector3? target = null,
            SimVector3? velocity = null,
            float requestedEffort = 0.5f)
        {
            return new ExecutionIntentV4(
                sourceIdentity,
                ExecutionCandidateCategoryV4.Attack,
                target ?? new SimVector3(1f, 2f, 3f),
                velocity ?? new SimVector3(4f, 5f, 6f),
                requestedEffort);
        }

        private static ExecutionEnvelopePolicyV4 CreatePolicy(
            int envelopeVersion = ExecutionEnvelopeV4.CurrentVersion,
            int policyVersion = 1,
            ExecutionCandidateCategoryV4[] candidateOrder = null,
            int sampleCount = 7,
            int maximumExpansionCount = 2,
            int allowedExpansionCount = 0,
            float perStepExpansionFactor = 1.5f,
            ExecutionDegradationStepV4[] degradationLadder = null,
            BoundedErrorDistributionKindV4 targetDistributionKind =
                BoundedErrorDistributionKindV4.BoundedUniform,
            BoundedErrorDistributionKindV4 velocityDistributionKind =
                BoundedErrorDistributionKindV4.BoundedUniform)
        {
            return new ExecutionEnvelopePolicyV4(
                envelopeVersion,
                policyVersion,
                candidateOrder ?? new[]
                {
                    ExecutionCandidateCategoryV4.Receive,
                    ExecutionCandidateCategoryV4.Set,
                    ExecutionCandidateCategoryV4.Attack,
                    ExecutionCandidateCategoryV4.Block,
                    ExecutionCandidateCategoryV4.Serve
                },
                sampleCount,
                maximumExpansionCount,
                allowedExpansionCount,
                perStepExpansionFactor,
                degradationLadder ?? new[]
                {
                    ExecutionDegradationStepV4.FullSampling,
                    ExecutionDegradationStepV4.ReducedSampleCount,
                    ExecutionDegradationStepV4.CachedCoarseDistribution,
                    ExecutionDegradationStepV4.DeterministicSafeFallback
                },
                targetDistributionKind,
                velocityDistributionKind);
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

        private static ExecutionSampleV4 SampleWithTargetErrorScale(
            ExecutionEnvelopeV4 envelope,
            float scale)
        {
            return new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                new SimVector3(
                    envelope.BaselineTarget.X +
                    (envelope.TargetError.MaximumAbsoluteError.X * scale),
                    envelope.BaselineTarget.Y,
                    envelope.BaselineTarget.Z),
                envelope.BaselineVelocity,
                envelope.RequestedEffort);
        }

        private static DerivedMatchAttributesV4 CreateDerivedForIndependentControls(
            float softTouch = 0.7f,
            float courtAwareness = 0.7f)
        {
            return MatchAttributeDerivationV4.Derive(
                new PhysicalBaseAttributesV4(
                    1.91f,
                    2.43f,
                    0.73f,
                    0.71f,
                    0.72f,
                    0.70f),
                new TechnicalBaseAttributesV4(
                    attackTechnique: 0.7f,
                    attackPower: 0.7f,
                    blockTechnique: 0.7f,
                    defenseTechnique: 0.7f,
                    receiveTechnique: 0.7f,
                    setTechnique: 0.7f,
                    serveTechnique: 0.7f,
                    softTouch: softTouch,
                    courtAwareness: courtAwareness),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
        }
    }

    public sealed class Stage2TrajectoryPredictionProviderTests
    {
        private const string PredictorHashA =
            BallTrajectoryPredictionProviderV4.DefaultPredictorConfigurationHash;
        private const string PredictorHashB =
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        private const string EnvelopeA =
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private const string EnvelopeB =
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

        [Test]
        public void CacheKey_EveryBehaviorFieldChangesArtifactIdentityAndRequiresCacheMiss()
        {
            var provider = Provider(capacity: 32);
            var baselineRequest = Request();
            var requests = new[]
            {
                Request(ballStateVersion: 8),
                Request(source: Source(positionX: 1.25f)),
                Request(
                    parameters: new BallSimulationParameters(-9.7f, 0.9995f),
                    physicsConfigurationHash:
                        BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(
                            new BallSimulationParameters(-9.7f, 0.9995f))),
                Request(samplingKey: "sample-b"),
                Request(envelopeIdentity: EnvelopeB),
                Request(degradationStep: ExecutionDegradationStepV4.ReducedSampleCount)
            };

            var baselineArtifact = provider.Predict(baselineRequest);
            foreach (var request in requests)
            {
                var artifact = provider.Predict(request);
                Assert.That(request.Key, Is.Not.EqualTo(baselineRequest.Key));
                Assert.That(request.Key.Identity, Is.Not.EqualTo(baselineRequest.Key.Identity));
                Assert.That(artifact, Is.Not.SameAs(baselineArtifact));
                Assert.That(artifact.ArtifactIdentity, Is.Not.EqualTo(baselineArtifact.ArtifactIdentity));
            }

            var changedVersionArtifact = Provider(
                    predictorVersion: 5,
                    predictorConfigurationHash: PredictorHashA)
                .Predict(Request(predictorVersion: 5));
            var changedConfigurationArtifact = Provider(
                    predictorVersion: 4,
                    predictorConfigurationHash: PredictorHashB)
                .Predict(Request(predictorConfigurationHash: PredictorHashB));
            Assert.That(
                changedVersionArtifact.ArtifactIdentity,
                Is.Not.EqualTo(baselineArtifact.ArtifactIdentity));
            Assert.That(
                changedConfigurationArtifact.ArtifactIdentity,
                Is.Not.EqualTo(baselineArtifact.ArtifactIdentity));

            Assert.That(provider.CacheCount, Is.EqualTo(7));
            Assert.That(baselineRequest.Key.BallStateVersion, Is.EqualTo(7));
            Assert.That(
                baselineRequest.Key.BallStateFingerprint,
                Is.EqualTo(BallTrajectoryPredictionRequestV4.BuildBallStateFingerprint(Source())));
            Assert.That(
                baselineRequest.Key.PhysicsConfigurationHash,
                Is.EqualTo(
                    BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(
                        new BallSimulationParameters(-9.8f, 0.9995f))));
            Assert.That(baselineRequest.Key.SamplingKey, Is.EqualTo("sample-a"));
            Assert.That(baselineRequest.Key.PredictorVersion, Is.EqualTo(4));
            Assert.That(baselineRequest.Key.PredictorConfigurationHash, Is.EqualTo(PredictorHashA));
            Assert.That(baselineRequest.Key.EnvelopeIdentity, Is.EqualTo(EnvelopeA));
            Assert.That(
                baselineRequest.Key.DegradationStep,
                Is.EqualTo((int)ExecutionDegradationStepV4.FullSampling));
        }

        [Test]
        public void Predict_HomeAndAwayExactKeyAreOrderIndependentAcrossFreshProviders()
        {
            var homeRequest = Request(requestingTeam: TeamSide.Home);
            var awayRequest = Request(requestingTeam: TeamSide.Away);
            var configuration = new TrajectoryPredictionProviderConfigurationV4(
                16,
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                4,
                PredictorHashA);
            var homeFirstProvider = new BallTrajectoryPredictionProviderV4(
                configuration,
                new ProvenanceSensitiveTrajectoryPredictorV4());
            var awayFirstProvider = new BallTrajectoryPredictionProviderV4(
                configuration,
                new ProvenanceSensitiveTrajectoryPredictorV4());

            var homeFirst = homeFirstProvider.Predict(homeRequest);
            var awayAfterHome = homeFirstProvider.Predict(awayRequest);
            var awayFirst = awayFirstProvider.Predict(awayRequest);
            var homeAfterAway = awayFirstProvider.Predict(homeRequest);

            Assert.That(awayRequest.Key, Is.EqualTo(homeRequest.Key));
            Assert.That(awayAfterHome, Is.SameAs(homeFirst));
            Assert.That(homeAfterAway, Is.SameAs(awayFirst));
            Assert.That(awayFirst.ArtifactIdentity, Is.EqualTo(homeFirst.ArtifactIdentity));
            Assert.That(awayFirst.ToCanonicalBytes(), Is.EqualTo(homeFirst.ToCanonicalBytes()));
            Assert.That(homeFirstProvider.CacheCount, Is.EqualTo(1));
            Assert.That(awayFirstProvider.CacheCount, Is.EqualTo(1));
        }

        [Test]
        public void PredictorContract_DoesNotExposeRequesterProvenance()
        {
            var predictMethod =
                typeof(IBallTrajectoryPredictorV4).GetMethod("Predict");
            var inputType = predictMethod.GetParameters()[0].ParameterType;

            Assert.That(
                inputType,
                Is.Not.EqualTo(typeof(BallTrajectoryPredictionRequestV4)));
            Assert.That(inputType.GetProperty("RequestingTeam"), Is.Null);
            Assert.That(
                System.Array.ConvertAll(
                    inputType.GetProperties(
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public),
                    property => property.Name),
                Is.EquivalentTo(new[]
                {
                    "BallPosition",
                    "BallRadius",
                    "BallVelocity",
                    "DegradationStep",
                    "Key",
                    "MaximumSamples",
                    "MaximumTimeSeconds",
                    "Parameters",
                    "StepSeconds"
                }));
        }

        [Test]
        public void Artifact_SnapshotsMutablePredictorSamplesBeforeIdentityIsFrozen()
        {
            var strategy = new MutableSampleTrajectoryPredictorV4();
            var provider = new BallTrajectoryPredictionProviderV4(
                new TrajectoryPredictionProviderConfigurationV4(
                    16,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    PredictorHashA),
                strategy);
            var artifact = provider.Predict(Request());
            var canonicalBeforeMutation = artifact.ToCanonicalBytes();
            var identityBeforeMutation = artifact.ArtifactIdentity;
            var sampleBeforeMutation = artifact.PredictionSnapshot.Samples[0];

            strategy.Samples[0] = new TrajectorySample(
                99f,
                new SimVector3(99f, 99f, 99f),
                new SimVector3(99f, 99f, 99f));
            strategy.Samples.Add(new TrajectorySample(
                100f,
                new SimVector3(100f, 100f, 100f),
                new SimVector3(100f, 100f, 100f)));

            Assert.That(
                artifact.PredictionSnapshot.Samples,
                Has.Count.EqualTo(1));
            Assert.That(
                artifact.PredictionSnapshot.Samples[0],
                Is.EqualTo(sampleBeforeMutation));
            Assert.That(artifact.ArtifactIdentity, Is.EqualTo(identityBeforeMutation));
            Assert.That(artifact.ToCanonicalBytes(), Is.EqualTo(canonicalBeforeMutation));
        }

        [Test]
        public void Request_RejectsPhysicsHashThatDoesNotMatchSimulationParameters()
        {
            Assert.That(
                () => Request(
                    parameters: new BallSimulationParameters(-9.7f, 0.9995f),
                    physicsConfigurationHash:
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                Throws.TypeOf<System.ArgumentException>()
                    .With.Message.Contains("physicsConfigurationHash"));
        }

        [Test]
        public void CacheKey_RejectsMalformedHashIdentities()
        {
            Assert.That(
                () => Key(ballStateFingerprint: "not-a-hash"),
                Throws.TypeOf<System.ArgumentException>());
            Assert.That(
                () => Key(physicsConfigurationHash: "not-a-hash"),
                Throws.TypeOf<System.ArgumentException>());
            Assert.That(
                () => Key(predictorConfigurationHash: "not-a-hash"),
                Throws.TypeOf<System.ArgumentException>());
            Assert.That(
                () => Key(envelopeIdentity: "not-a-hash"),
                Throws.TypeOf<System.ArgumentException>());
        }

        [Test]
        public void Predict_RejectsRequestWhosePredictorIdentityDoesNotMatchProviderConfiguration()
        {
            var provider = Provider();

            Assert.That(
                () => provider.Predict(Request(predictorVersion: 5)),
                Throws.TypeOf<System.ArgumentException>()
                    .With.Message.Contains("PredictorVersion"));
            Assert.That(
                () => provider.Predict(
                    Request(predictorConfigurationHash: PredictorHashB)),
                Throws.TypeOf<System.ArgumentException>()
                    .With.Message.Contains("PredictorConfigurationHash"));
            Assert.That(provider.CacheCount, Is.Zero);
        }

        [Test]
        public void Provider_DefaultStrategyRejectsUnsupportedPredictorIdentity()
        {
            Assert.That(
                () => new BallTrajectoryPredictionProviderV4(
                    new TrajectoryPredictionProviderConfigurationV4(
                        16,
                        TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                        5,
                        PredictorHashA)),
                Throws.TypeOf<System.ArgumentException>());
            Assert.That(
                () => new BallTrajectoryPredictionProviderV4(
                    new TrajectoryPredictionProviderConfigurationV4(
                        16,
                        TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                        4,
                        PredictorHashB)),
                Throws.TypeOf<System.ArgumentException>());
        }

        [Test]
        public void DirectorGate5_FailureAdvancesToNextDegradationKey()
        {
            var strategy = new RecordingTrajectoryPredictorV4(
                4,
                PredictorHashA,
                failStep: ExecutionDegradationStepV4.FullSampling);
            var provider = new BallTrajectoryPredictionProviderV4(
                new TrajectoryPredictionProviderConfigurationV4(
                    16,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    PredictorHashA),
                strategy);
            var request = Request();

            var artifact = PhysicalMatchRallyDirector.PredictSharedGate5TrajectoryV4(
                provider,
                request,
                ExecutionEnvelopePolicyV4.Default);

            Assert.That(
                strategy.Attempts,
                Is.EqualTo(new[]
                {
                    ExecutionDegradationStepV4.FullSampling,
                    ExecutionDegradationStepV4.ReducedSampleCount
                }));
            Assert.That(
                artifact.Key.DegradationStep,
                Is.EqualTo((int)ExecutionDegradationStepV4.ReducedSampleCount));
            Assert.That(
                provider.TryGetCached(request.Key, out _),
                Is.False);
            Assert.That(
                provider.TryGetCached(
                    request.WithDegradationStep(
                        ExecutionDegradationStepV4.ReducedSampleCount).Key,
                    out var cached),
                Is.True);
            Assert.That(cached, Is.SameAs(artifact));
        }

        [Test]
        public void Predict_ArtifactRecordsCompleteKeyPredictorAndSampleProvenance()
        {
            var provider = Provider();
            var request = Request();

            var artifact = provider.Predict(request);

            Assert.That(artifact.Key, Is.EqualTo(request.Key));
            Assert.That(artifact.KeyIdentity, Is.EqualTo(request.Key.Identity));
            Assert.That(artifact.PredictorSource, Is.EqualTo(BallTrajectoryPredictionProviderV4.PredictorSource));
            Assert.That(artifact.PredictorVersion, Is.EqualTo(request.Key.PredictorVersion));
            Assert.That(
                artifact.PredictorConfigurationHash,
                Is.EqualTo(request.Key.PredictorConfigurationHash));
            Assert.That(artifact.SampleTimestamps, Has.Count.GreaterThan(0));
            Assert.That(artifact.SamplePositions, Has.Count.EqualTo(artifact.SampleTimestamps.Count));
            Assert.That(artifact.SamplePositions[0], Is.EqualTo(Source().Position));
            Assert.That(artifact.ArtifactIdentity, Has.Length.EqualTo(64));
            Assert.That(artifact.ToCanonicalBytes(), Is.Not.Empty);
        }

        [Test]
        public void Cache_EvictsOldestInsertedKeyAtContextConfiguredCapacity()
        {
            var provider = Provider(capacity: 2);
            var firstRequest = Request(samplingKey: "sample-1");
            var secondRequest = Request(samplingKey: "sample-2");
            var thirdRequest = Request(samplingKey: "sample-3");
            var firstArtifact = provider.Predict(firstRequest);
            provider.Predict(secondRequest);

            Assert.That(provider.Predict(firstRequest), Is.SameAs(firstArtifact));
            provider.Predict(thirdRequest);

            Assert.That(provider.CacheCount, Is.EqualTo(2));
            Assert.That(provider.TryGetCached(firstRequest.Key, out _), Is.False);
            Assert.That(provider.TryGetCached(secondRequest.Key, out _), Is.True);
            Assert.That(provider.TryGetCached(thirdRequest.Key, out _), Is.True);

            var regenerated = provider.Predict(firstRequest);
            Assert.That(regenerated, Is.Not.SameAs(firstArtifact));
            Assert.That(regenerated.ArtifactIdentity, Is.EqualTo(firstArtifact.ArtifactIdentity));
            Assert.That(regenerated.ToCanonicalBytes(), Is.EqualTo(firstArtifact.ToCanonicalBytes()));
        }

        [Test]
        public void Director_CreatesPerRallyProviderFromV4MatchContextConfiguration()
        {
            var context = MatchV4TestFixture.CreateContext(predictionCacheCapacity: 3);

            var provider = PhysicalMatchRallyDirector.CreateTrajectoryPredictionProviderV4(context);
            var nextRallyProvider =
                PhysicalMatchRallyDirector.CreateTrajectoryPredictionProviderV4(context);

            Assert.That(provider.CacheCapacity, Is.EqualTo(3));
            Assert.That(nextRallyProvider, Is.Not.SameAs(provider));
            Assert.That(
                provider.CacheEvictionPolicy,
                Is.EqualTo(TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut));
            Assert.That(
                provider.PredictorVersion,
                Is.EqualTo(context.TrajectoryPredictionProviderConfiguration.PredictorVersion));
            Assert.That(
                provider.PredictorConfigurationHash,
                Is.EqualTo(
                    context.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash));
        }

        [Test]
        public void DirectorFactory_RejectsContextWhosePhysicsHashDoesNotMatchRuntimePhysics()
        {
            var baseline = MatchV4TestFixture.CreateContext();
            var mismatched = MatchContextV4.Create(
                baseline.SessionId,
                baseline.Seed,
                baseline.Home,
                baseline.Away,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                baseline.TrajectoryPredictionProviderConfiguration,
                baseline.RulesVersion);

            Assert.That(
                () => PhysicalMatchRallyDirector
                    .CreateTrajectoryPredictionProviderV4(mismatched),
                Throws.TypeOf<System.ArgumentException>()
                    .With.Message.Contains("PhysicsConfigurationHash"));
        }

        private static BallTrajectoryPredictionProviderV4 Provider(
            int capacity = 16,
            int predictorVersion = 4,
            string predictorConfigurationHash = PredictorHashA)
        {
            var configuration = new TrajectoryPredictionProviderConfigurationV4(
                capacity,
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                predictorVersion,
                predictorConfigurationHash);
            return predictorVersion ==
                    BallTrajectoryPredictionProviderV4.CurrentPredictorVersion &&
                predictorConfigurationHash ==
                    BallTrajectoryPredictionProviderV4.DefaultPredictorConfigurationHash
                ? new BallTrajectoryPredictionProviderV4(configuration)
                : new BallTrajectoryPredictionProviderV4(
                    configuration,
                    new RecordingTrajectoryPredictorV4(
                        predictorVersion,
                        predictorConfigurationHash));
        }

        private static BallTrajectoryPredictionCacheKeyV4 Key(
            string ballStateFingerprint = EnvelopeA,
            string physicsConfigurationHash = EnvelopeA,
            string predictorConfigurationHash = PredictorHashA,
            string envelopeIdentity = EnvelopeA)
        {
            return new BallTrajectoryPredictionCacheKeyV4(
                7,
                ballStateFingerprint,
                physicsConfigurationHash,
                "sample-a",
                4,
                predictorConfigurationHash,
                envelopeIdentity,
                (int)ExecutionDegradationStepV4.FullSampling);
        }

        private static BallTrajectoryPredictionRequestV4 Request(
            TeamSide requestingTeam = TeamSide.Home,
            long ballStateVersion = 7,
            BallState source = null,
            BallSimulationParameters? parameters = null,
            string physicsConfigurationHash = null,
            string samplingKey = "sample-a",
            int predictorVersion = 4,
            string predictorConfigurationHash = PredictorHashA,
            string envelopeIdentity = EnvelopeA,
            ExecutionDegradationStepV4 degradationStep =
                ExecutionDegradationStepV4.FullSampling)
        {
            var resolvedParameters =
                parameters ?? new BallSimulationParameters(-9.8f, 0.9995f);
            return new BallTrajectoryPredictionRequestV4(
                requestingTeam,
                ballStateVersion,
                source ?? Source(),
                resolvedParameters,
                physicsConfigurationHash ??
                    BallTrajectoryPredictionProviderV4.BuildPhysicsConfigurationHash(
                        resolvedParameters),
                samplingKey,
                predictorVersion,
                predictorConfigurationHash,
                envelopeIdentity,
                degradationStep);
        }

        private static BallState Source(float positionX = 1f)
        {
            return new BallState(
                new SimVector3(positionX, 3f, -2f),
                new SimVector3(2f, 4f, 5f),
                0.12f);
        }

        private sealed class RecordingTrajectoryPredictorV4 :
            IBallTrajectoryPredictorV4
        {
            private readonly ExecutionDegradationStepV4? _failStep;
            private readonly System.Collections.Generic.List<
                ExecutionDegradationStepV4> _attempts =
                new System.Collections.Generic.List<ExecutionDegradationStepV4>();

            public RecordingTrajectoryPredictorV4(
                int predictorVersion,
                string predictorConfigurationHash,
                ExecutionDegradationStepV4? failStep = null)
            {
                PredictorVersion = predictorVersion;
                PredictorConfigurationHash = predictorConfigurationHash;
                _failStep = failStep;
            }

            public string PredictorSource =>
                "Volleyball.EditModeTests.RecordingTrajectoryPredictorV4";

            public int PredictorVersion { get; }

            public string PredictorConfigurationHash { get; }

            public System.Collections.Generic.IReadOnlyList<
                ExecutionDegradationStepV4> Attempts => _attempts;

            public TrajectoryPrediction Predict(
                BallTrajectoryPredictorInputV4 input)
            {
                _attempts.Add(input.DegradationStep);
                if (_failStep.HasValue &&
                    _failStep.Value == input.DegradationStep)
                {
                    throw new System.InvalidOperationException(
                        "Injected deterministic predictor failure.");
                }

                return TrajectoryPredictor.Predict(
                    new BallState(
                        input.BallPosition,
                        input.BallVelocity,
                        input.BallRadius),
                    input.Parameters,
                    input.StepSeconds,
                    input.MaximumTimeSeconds,
                    input.MaximumSamples);
            }
        }

        private sealed class ProvenanceSensitiveTrajectoryPredictorV4 :
            IBallTrajectoryPredictorV4
        {
            public string PredictorSource =>
                "Volleyball.EditModeTests.ProvenanceSensitiveTrajectoryPredictorV4";

            public int PredictorVersion => 4;

            public string PredictorConfigurationHash => PredictorHashA;

            public TrajectoryPrediction Predict(
                BallTrajectoryPredictorInputV4 input)
            {
                var exposedRequester = FindRequesterTeam(input, depth: 3);
                var position = exposedRequester == TeamSide.Away
                    ? new SimVector3(4f, 5f, 6f)
                    : new SimVector3(1f, 2f, 3f);
                return new TrajectoryPrediction(
                    new System.Collections.Generic.List<TrajectorySample>
                    {
                        new TrajectorySample(
                            0f,
                            position,
                            SimVector3.Zero)
                    },
                    null);
            }

            private static TeamSide? FindRequesterTeam(
                object value,
                int depth)
            {
                if (value is TeamSide side)
                {
                    return side;
                }

                if (value == null || depth <= 0)
                {
                    return null;
                }

                var type = value.GetType();
                if (type.IsPrimitive ||
                    type.IsEnum ||
                    type.IsValueType ||
                    type == typeof(string))
                {
                    return null;
                }

                foreach (var property in type.GetProperties(
                             System.Reflection.BindingFlags.Instance |
                             System.Reflection.BindingFlags.Public))
                {
                    if (!property.CanRead ||
                        property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    var requester = FindRequesterTeam(
                        property.GetValue(value, null),
                        depth - 1);
                    if (requester.HasValue)
                    {
                        return requester;
                    }
                }

                return null;
            }
        }

        private sealed class MutableSampleTrajectoryPredictorV4 :
            IBallTrajectoryPredictorV4
        {
            public MutableSampleTrajectoryPredictorV4()
            {
                Samples = new System.Collections.Generic.List<TrajectorySample>
                {
                    new TrajectorySample(
                        0f,
                        new SimVector3(1f, 2f, 3f),
                        new SimVector3(4f, 5f, 6f))
                };
            }

            public string PredictorSource =>
                "Volleyball.EditModeTests.MutableSampleTrajectoryPredictorV4";

            public int PredictorVersion => 4;

            public string PredictorConfigurationHash => PredictorHashA;

            public System.Collections.Generic.List<TrajectorySample> Samples
            {
                get;
            }

            public TrajectoryPrediction Predict(
                BallTrajectoryPredictorInputV4 input)
            {
                return new TrajectoryPrediction(Samples, null);
            }
        }
    }

    public sealed class Stage2AttackGeometryFactTests
    {
        private static readonly PlayerId Attacker = new PlayerId("home-attacker");

        [Test]
        public void ContactAboveNet_DetectedFromGeometry()
        {
            var fact = new AttackGeometryFactV3(
                Attacker,
                TeamSide.Home,
                new SimVector3(0f, 1f, -0.5f),
                new SimVector3(0f, 2.5f, -0.2f),
                attackLineDistanceFromCenter: 3f,
                netHeight: 2.43f);

            Assert.That(fact.IsContactAboveNet, Is.True);
        }

        [Test]
        public void ContactBelowNet_NotAboveNet()
        {
            var fact = new AttackGeometryFactV3(
                Attacker,
                TeamSide.Home,
                new SimVector3(0f, 1f, -0.5f),
                new SimVector3(0f, 1.5f, -0.2f),
                attackLineDistanceFromCenter: 3f,
                netHeight: 2.43f);

            Assert.That(fact.IsContactAboveNet, Is.False);
        }

        [Test]
        public void TakeoffInFrontZone_DetectedForHome()
        {
            var fact = new AttackGeometryFactV3(
                Attacker,
                TeamSide.Home,
                new SimVector3(0f, 1f, -2.5f),
                new SimVector3(0f, 3f, -0.5f),
                attackLineDistanceFromCenter: 3f,
                netHeight: 2.43f);

            Assert.That(fact.IsTakeoffInFrontZone, Is.True);
        }

        [Test]
        public void TakeoffBehindAttackLine_NotInFrontZone()
        {
            var fact = new AttackGeometryFactV3(
                Attacker,
                TeamSide.Home,
                new SimVector3(0f, 1f, -5f),
                new SimVector3(0f, 3f, -0.5f),
                attackLineDistanceFromCenter: 3f,
                netHeight: 2.43f);

            Assert.That(fact.IsTakeoffInFrontZone, Is.False);
        }

        [Test]
        public void TakeoffInFrontZone_MirroredForAway()
        {
            var fact = new AttackGeometryFactV3(
                new PlayerId("away-attacker"),
                TeamSide.Away,
                new SimVector3(0f, 1f, 2.5f),
                new SimVector3(0f, 3f, 0.5f),
                attackLineDistanceFromCenter: 3f,
                netHeight: 2.43f);

            Assert.That(fact.IsTakeoffInFrontZone, Is.True);
        }

        [Test]
        public void GeometryFact_EqualityByValues()
        {
            var first = new AttackGeometryFactV3(
                Attacker, TeamSide.Home,
                new SimVector3(0f, 1f, -2.5f),
                new SimVector3(0f, 3f, -0.5f),
                attackLineDistanceFromCenter: 3f, netHeight: 2.43f);
            var second = new AttackGeometryFactV3(
                Attacker, TeamSide.Home,
                new SimVector3(0f, 1f, -2.5f),
                new SimVector3(0f, 3f, -0.5f),
                attackLineDistanceFromCenter: 3f, netHeight: 2.43f);

            Assert.That(second.Actor, Is.EqualTo(first.Actor));
            Assert.That(second.ContactPoint, Is.EqualTo(first.ContactPoint));
            Assert.That(second.TakeoffPoint, Is.EqualTo(first.TakeoffPoint));
        }
    }
}
