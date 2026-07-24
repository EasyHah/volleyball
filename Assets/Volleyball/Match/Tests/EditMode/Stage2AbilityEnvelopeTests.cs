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
    public sealed class Stage2AbilityProjectionTests
    {
        [Test]
        public void FromSnapshot_MarksAllLiveAxesAsActive()
        {
            var snapshot = CreateV3();
            var projection = AbilityProjectionV3.FromSnapshot(snapshot);

            Assert.That(projection.For(AbilityAxisName.Mobility).Status, Is.EqualTo(AbilityAxisStatus.Active));
            Assert.That(projection.For(AbilityAxisName.AttackControl).Status, Is.EqualTo(AbilityAxisStatus.Active));
            Assert.That(projection.For(AbilityAxisName.SoftTouch).Status, Is.EqualTo(AbilityAxisStatus.Reserved));
            Assert.That(projection.For(AbilityAxisName.BlockTechnique).Status, Is.EqualTo(AbilityAxisStatus.Reserved));
            Assert.That(projection.For(AbilityAxisName.CourtAwareness).Status, Is.EqualTo(AbilityAxisStatus.Reserved));
        }

        [Test]
        public void FromSnapshot_AttackControlCarriesAttackActionCategory()
        {
            var projection = AbilityProjectionV3.FromSnapshot(CreateV3(attackControl: 0.7f));

            var attackControl = projection.For(AbilityAxisName.AttackControl);
            Assert.That(attackControl.Status, Is.EqualTo(AbilityAxisStatus.Active));
            Assert.That(attackControl.ActionCategory, Is.EqualTo("attack"));
            Assert.That(attackControl.Value, Is.EqualTo(0.7f));
        }

        [Test]
        public void FromV2Snapshot_MarksAttackControlAsCompatibilityMapped()
        {
            var v2 = new PlayerAbilitySnapshotV2(
                mobility: 0.8f, reaction: 0.8f, jump: 0.8f,
                receiveTechnique: 0.8f, setTechnique: 0.8f,
                attackTechnique: 0.6f, attackPower: 0.7f, maxAttackReach: 3.42f);

            var projection = AbilityProjectionV3.FromV2Snapshot(v2);

            Assert.That(projection.For(AbilityAxisName.AttackControl).Status, Is.EqualTo(AbilityAxisStatus.CompatibilityMapped));
            Assert.That(projection.For(AbilityAxisName.AttackControl).Value, Is.EqualTo(0.6f));
            Assert.That(projection.For(AbilityAxisName.AttackControl).SourceName, Is.EqualTo("AttackTechnique"));
            Assert.That(projection.For(AbilityAxisName.Mobility).Status, Is.EqualTo(AbilityAxisStatus.Active));
        }

        [Test]
        public void ActiveValueFor_ThrowsOnReservedAxis()
        {
            var projection = AbilityProjectionV3.FromSnapshot(CreateV3());

            Assert.Throws<System.InvalidOperationException>(
                () => projection.ActiveValueFor(AbilityAxisName.SoftTouch));
        }

        [Test]
        public void AttackTechniqueValue_ReturnsActiveOrCompatibilityMappedValue()
        {
            var activeProjection = AbilityProjectionV3.FromSnapshot(CreateV3(attackControl: 0.65f));
            var v2 = new PlayerAbilitySnapshotV2(
                mobility: 0.8f, reaction: 0.8f, jump: 0.8f,
                receiveTechnique: 0.8f, setTechnique: 0.8f,
                attackTechnique: 0.55f, attackPower: 0.7f, maxAttackReach: 3.42f);
            var mappedProjection = AbilityProjectionV3.FromV2Snapshot(v2);

            Assert.That(activeProjection.AttackTechniqueValue(), Is.EqualTo(0.65f));
            Assert.That(mappedProjection.AttackTechniqueValue(), Is.EqualTo(0.55f));
        }

        [Test]
        public void Projection_DeterministicEquality()
        {
            var first = AbilityProjectionV3.FromSnapshot(CreateV3(attackControl: 0.5f));
            var second = AbilityProjectionV3.FromSnapshot(CreateV3(attackControl: 0.5f));

            Assert.That(second, Is.EqualTo(first));
            Assert.That(second.GetHashCode(), Is.EqualTo(first.GetHashCode()));
        }

        [Test]
        public void Projection_DifferentValuesAreNotEqual()
        {
            var low = AbilityProjectionV3.FromSnapshot(CreateV3(attackControl: 0.3f));
            var high = AbilityProjectionV3.FromSnapshot(CreateV3(attackControl: 0.9f));

            Assert.That(high, Is.Not.EqualTo(low));
        }

        private static PlayerAbilitySnapshotV3 CreateV3(float attackControl = 0.5f)
        {
            return new PlayerAbilitySnapshotV3(
                mobility: 0.8f, reaction: 0.8f, jump: 0.8f, maxAttackReach: 3.42f,
                receiveTechnique: 0.8f, setTechnique: 0.8f,
                attackControl: attackControl, attackPower: 0.7f,
                softTouch: 0.5f, blockTechnique: 0.5f, courtAwareness: 0.5f,
                sourceVersion: ContractVersions.MatchV3,
                migrationVersion: PlayerAbilitySnapshotV3.CurrentMigrationVersion,
                isCompatibilityEstimate: false,
                compatibilityCollapsedAxes: System.Array.Empty<string>());
        }
    }

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
        public void PhysicalExecutor_UsesExactEnvelopeSampleAndVelocity()
        {
            var derived = MatchV4TestFixture.CreateDerived();
            var envelope = CreateEnvelope(derived: derived);
            var sample = new ExecutionSampleV4(
                envelope.Identity,
                envelope.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Attack,
                envelope.BaselineTarget,
                new SimVector3(3f, 4f, 5f),
                envelope.RequestedEffort);
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
                    envelope,
                    sample,
                    noError,
                    contactGroupId: 171);
                var contacts = new System.Collections.Generic.List<BallContactCandidate>();
                player.CollectContacts(2f, 1f / 120f, contacts);

                Assert.That(player.ScheduledExecutionEnvelopeV4, Is.SameAs(envelope));
                Assert.That(player.ScheduledExecutionSampleV4, Is.SameAs(sample));
                Assert.That(contacts, Is.Not.Empty);
                Assert.That(contacts[0].TargetVelocity, Is.EqualTo(sample.Velocity));
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
        [Test]
        public void Predict_CachesByDeterministicKey()
        {
            var provider = new BallTrajectoryPredictionProviderV3();
            var source = new BallState(
                new SimVector3(1f, 3f, -2f),
                new SimVector3(2f, 4f, 5f),
                0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);

            var first = provider.Predict(source, parameters, "sample-key-1");
            var second = provider.Predict(source, parameters, "sample-key-1");

            Assert.That(second, Is.SameAs(first));
            Assert.That(provider.CacheCount, Is.EqualTo(1));
        }

        [Test]
        public void Predict_DifferentSampleKeysProduceDifferentArtifacts()
        {
            var provider = new BallTrajectoryPredictionProviderV3();
            var source = new BallState(
                new SimVector3(1f, 3f, -2f),
                new SimVector3(2f, 4f, 5f),
                0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);

            var first = provider.Predict(source, parameters, "sample-key-1");
            var second = provider.Predict(source, parameters, "sample-key-2");

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(provider.CacheCount, Is.EqualTo(2));
        }

        [Test]
        public void Predict_DifferentBallStateProducesDifferentArtifact()
        {
            var provider = new BallTrajectoryPredictionProviderV3();
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);

            var sourceA = new BallState(
                new SimVector3(1f, 3f, -2f),
                new SimVector3(2f, 4f, 5f),
                0.12f);
            var sourceB = new BallState(
                new SimVector3(2f, 3f, -2f),
                new SimVector3(2f, 4f, 5f),
                0.12f);

            var first = provider.Predict(sourceA, parameters, "sample-key-1");
            var second = provider.Predict(sourceB, parameters, "sample-key-1");

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void Predict_BothTeamsShareArtifactForSameInput()
        {
            var provider = new BallTrajectoryPredictionProviderV3();
            var source = new BallState(
                new SimVector3(1f, 3f, -2f),
                new SimVector3(2f, 4f, 5f),
                0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);

            var homeArtifact = provider.Predict(source, parameters, "gate-5-sample");
            var awayArtifact = provider.Predict(source, parameters, "gate-5-sample");

            Assert.That(awayArtifact, Is.SameAs(homeArtifact));
            Assert.That(awayArtifact.SampleKey, Is.EqualTo("gate-5-sample"));
            Assert.That(awayArtifact.PredictorVersion, Is.EqualTo(BallTrajectoryPredictionProviderV3.PredictorVersion));
        }

        [Test]
        public void Predict_DifferentDegradationModeProducesDifferentCachedArtifact()
        {
            var provider = new BallTrajectoryPredictionProviderV3();
            var source = new BallState(new SimVector3(1f, 3f, -2f), new SimVector3(2f, 4f, 5f), 0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);

            var normal = provider.Predict(source, parameters, "same-sample", degradationMode: "normal");
            var degraded = provider.Predict(source, parameters, "same-sample", degradationMode: "budget-step-1");

            Assert.That(degraded, Is.Not.SameAs(normal));
            Assert.That(degraded, Is.Not.EqualTo(normal));
            Assert.That(provider.CacheCount, Is.EqualTo(2));
        }

        [Test]
        public void CacheKey_ChangesWhenPredictorConfigurationChanges()
        {
            var source = new BallState(new SimVector3(1f, 3f, -2f), new SimVector3(2f, 4f, 5f), 0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);
            var provider = new BallTrajectoryPredictionProviderV3();
            var predict = RequiredVersionedPredict();

            var first = (BallTrajectoryArtifactV3)predict.Invoke(
                provider, new object[] { source, parameters, "same-sample", "predictor-v4-1", "config-a" });
            var second = (BallTrajectoryArtifactV3)predict.Invoke(
                provider, new object[] { source, parameters, "same-sample", "predictor-v4-1", "config-b" });

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(provider.CacheCount, Is.EqualTo(2));
        }

        [Test]
        public void PredictorVersion_ChangesArtifactIdentityAndRequiresAnIndependentCacheMiss()
        {
            var source = new BallState(new SimVector3(1f, 3f, -2f), new SimVector3(2f, 4f, 5f), 0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);
            var provider = new BallTrajectoryPredictionProviderV3();
            var predict = RequiredVersionedPredict();
            var first = (BallTrajectoryArtifactV3)predict.Invoke(
                provider, new object[] { source, parameters, "same-sample", "predictor-v4-1", "config-a" });
            var second = (BallTrajectoryArtifactV3)predict.Invoke(
                provider, new object[] { source, parameters, "same-sample", "predictor-v4-2", "config-a" });

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(provider.CacheCount, Is.EqualTo(2));
        }

        private static System.Reflection.MethodInfo RequiredVersionedPredict()
        {
            var predict = typeof(BallTrajectoryPredictionProviderV3).GetMethod(
                nameof(BallTrajectoryPredictionProviderV3.Predict),
                new[] { typeof(BallState), typeof(BallSimulationParameters), typeof(string), typeof(string), typeof(string) });
            Assert.That(predict, Is.Not.Null, "Prediction requests must carry predictor version and configuration independently.");
            return predict;
        }

        [Test]
        public void Predict_ProducesTrajectoryWithSamples()
        {
            var provider = new BallTrajectoryPredictionProviderV3();
            var source = new BallState(
                new SimVector3(0f, 3f, 0f),
                new SimVector3(1f, 2f, 1f),
                0.12f);
            var parameters = new BallSimulationParameters(-9.8f, 0.9995f);

            var artifact = provider.Predict(source, parameters, "trajectory-test");

            Assert.That(artifact.Prediction, Is.Not.Null);
            Assert.That(artifact.Prediction.Samples.Count, Is.GreaterThan(0));
            Assert.That(artifact.Prediction.Samples[0].Position, Is.EqualTo(source.Position));
        }

        [Test]
        public void BuildBallStateVersion_IsDeterministic()
        {
            var source = new BallState(
                new SimVector3(1f, 2f, 3f),
                new SimVector3(4f, 5f, 6f),
                0.12f);

            var first = BallTrajectoryPredictionProviderV3.BuildBallStateVersion(source);
            var second = BallTrajectoryPredictionProviderV3.BuildBallStateVersion(source);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Length, Is.EqualTo(64));
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
