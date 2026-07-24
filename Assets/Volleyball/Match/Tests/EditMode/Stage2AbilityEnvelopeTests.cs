using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

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
        public void FullEnvelope_SameInputProducesSameIdentity()
        {
            var first = CreateFullEnvelope();
            var second = CreateFullEnvelope();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(second.GetHashCode(), Is.EqualTo(first.GetHashCode()));
        }

        [Test]
        public void ClassifySample_WithinEnvelope()
        {
            var envelope = CreateFullEnvelope();
            var sample = new ExecutionSampleV3(
                envelope.DeterministicSampleKey,
                new SimVector3(0.05f, 0.05f, 0.05f),
                velocityScale: 0.95f,
                effort: 0.9f,
                sampleClass: "normal");

            Assert.That(envelope.ClassifySample(sample), Is.EqualTo(ExecutionSampleClassification.WithinEnvelope));
        }

        [Test]
        public void ClassifySample_EnvelopeExceeded()
        {
            var envelope = CreateFullEnvelope();
            var sample = new ExecutionSampleV3(
                envelope.DeterministicSampleKey,
                new SimVector3(2f, 2f, 2f),
                velocityScale: 1.5f,
                effort: 1f,
                sampleClass: "out-of-bounds");

            Assert.That(envelope.ClassifySample(sample), Is.EqualTo(ExecutionSampleClassification.EnvelopeExceeded));
        }

        [Test]
        public void EnvelopeIdentity_ChangesWhenEffortOrMaximumEffortChanges()
        {
            var baseline = CreateEnvelope(effort: 0.5f, maximumEffort: 0.8f);
            var changedEffort = CreateEnvelope(effort: 0.6f, maximumEffort: 0.8f);
            var changedMaximum = CreateEnvelope(effort: 0.5f, maximumEffort: 0.9f);

            Assert.That(changedEffort, Is.Not.EqualTo(baseline));
            Assert.That(changedMaximum, Is.Not.EqualTo(baseline));
        }

        [Test]
        public void ClassifySample_DoesNotClampVelocityBackIntoEnvelope()
        {
            var envelope = CreateFullEnvelope();
            var sample = new ExecutionSampleV3(
                envelope.DeterministicSampleKey,
                new SimVector3(0.05f, 0f, 0f),
                velocityScale: envelope.Bounds.MaxVelocityScale + 0.01f,
                effort: envelope.Effort,
                sampleClass: "invalid-velocity");

            Assert.That(
                envelope.ClassifySample(sample),
                Is.EqualTo(ExecutionSampleClassification.EnvelopeExceeded));
        }

        [Test]
        public void ClassifySample_UnexpectedExecutionSample_WrongKey()
        {
            var envelope = CreateFullEnvelope();
            var sample = new ExecutionSampleV3(
                "different-sample-key",
                new SimVector3(0.05f, 0.05f, 0.05f),
                velocityScale: 0.95f,
                effort: 0.9f,
                sampleClass: "normal");

            Assert.That(envelope.ClassifySample(sample), Is.EqualTo(ExecutionSampleClassification.UnexpectedExecutionSample));
        }

        [Test]
        public void IdentityConstructor_RemainsCompatibleWithPhase0Contract()
        {
            var envelope = new ExecutionEnvelopeV3(
                "envelope-v3",
                "ability-hash-1",
                "source-v3",
                "attack",
                "target-baseline-1",
                "distribution-1",
                "sample-1");

            Assert.That(envelope.Version, Is.EqualTo("envelope-v3"));
            Assert.That(envelope.Samples.Count, Is.EqualTo(0));
            Assert.That(envelope.Effort, Is.EqualTo(1f));
        }

        internal static ExecutionEnvelopeV3 CreateFullEnvelope()
        {
            return CreateEnvelope(effort: 0.9f, maximumEffort: 1f);
        }

        private static ExecutionEnvelopeV3 CreateEnvelope(float effort, float maximumEffort)
        {
            return new ExecutionEnvelopeV3(
                "envelope-v3",
                "ability-hash-1",
                "source-v3",
                "attack",
                "target-baseline-1",
                "distribution-1",
                "sample-1",
                new SimVector3(0f, 3f, -1f),
                new SimVector3(5f, 8f, 3f),
                new EnvelopeBoundsV3(
                    minTargetDeviationMeters: 0f,
                    maxTargetDeviationMeters: 0.15f,
                    minVelocityScale: 0.7f,
                    maxVelocityScale: 1.2f,
                    maxEffort: maximumEffort),
                effort: effort,
                samples: System.Array.Empty<ExecutionSampleV3>(),
                provenance: "test-envelope",
                lastSampleClassification: null);
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
