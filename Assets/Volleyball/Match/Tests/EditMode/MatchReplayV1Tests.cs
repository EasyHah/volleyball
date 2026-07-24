using System;
using System.IO;
using NUnit.Framework;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class MatchReplayV4Tests
    {
        private const string HashA =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string HashB =
            "123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0";
        private const string HashC =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        [Test]
        public void CanonicalJson_IsByteStableAndSortsEventsAndConsumption()
        {
            var first = CreateReplay(Event(1, "Receive"), Event(0, "Attack"));
            var second = CreateReplay(Event(0, "Attack"), Event(1, "Receive"));

            var firstJson = ContractJson.SerializeV4(first);
            var secondJson = ContractJson.SerializeV4(second);

            Assert.That(secondJson, Is.EqualTo(firstJson));
            Assert.That(first.Events[0].SequenceNumber, Is.Zero);
            Assert.That(first.Events[1].SequenceNumber, Is.EqualTo(1));
            Assert.That(
                first.Events[0].AbilityConsumptions[0].AttributeName,
                Is.EqualTo("Attack.DirectionControl"));
            Assert.That(
                first.Events[0].AbilityConsumptions[1].AttributeName,
                Is.EqualTo("Attack.PowerCapacity"));
            Assert.That(first.ReplayHash, Does.Match("^[0-9a-f]{64}$"));

            for (var repetition = 0; repetition < 100; repetition++)
            {
                Assert.That(ContractJson.SerializeV4(first), Is.EqualTo(firstJson));
            }

            var restored = ContractJson.DeserializeMatchReplayV4(firstJson);
            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(firstJson));
            Assert.That(restored.ReplayHash, Is.EqualTo(first.ReplayHash));
        }

        [Test]
        public void CanonicalJson_PersistsCompleteV4DiagnosticEvidence()
        {
            var replay = CreateReplay(Event(0, "Attack"));

            var json = ContractJson.SerializeV4(replay);
            var restored = ContractJson.DeserializeMatchReplayV4(json);
            var replayEvent = restored.Events[0];

            Assert.That(restored.FormatVersion, Is.EqualTo(ContractVersions.ReplayV4));
            Assert.That(restored.Context.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(restored.Context.RulesVersion, Is.EqualTo(ContractVersions.MatchV3));
            Assert.That(restored.Context.FormulaVersion, Is.EqualTo(1));
            Assert.That(restored.Context.CoefficientVersion, Is.EqualTo(1));
            Assert.That(restored.Context.Home.RotationOrder[0].DominantHand, Is.EqualTo(DominantHandV4.Right));
            Assert.That(restored.Context.Home.RotationOrder[0].Derived.InputFingerprint, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(restored.Context.Home.RotationOrder[0].Derived.ResultFingerprint, Does.Match("^[0-9a-f]{64}$"));

            Assert.That(replayEvent.Envelope.Identity, Is.EqualTo(HashA));
            Assert.That(replayEvent.Envelope.PolicyIdentity, Is.EqualTo(HashB));
            Assert.That(replayEvent.Envelope.SourceIntentIdentity, Is.EqualTo("attack-intent-0"));
            Assert.That(replayEvent.Envelope.TargetError.Minimum.X, Is.EqualTo(-0.1f));
            Assert.That(replayEvent.Envelope.VelocityError.Maximum.Z, Is.EqualTo(0.3f));
            Assert.That(replayEvent.Envelope.MaximumEffort, Is.EqualTo(0.95f));
            Assert.That(replayEvent.Envelope.DegradationLadder, Is.EqualTo(
                new[] { "FullSampling", "ReducedSampleCount", "CachedCoarseDistribution", "DeterministicSafeFallback" }));

            Assert.That(replayEvent.AbilityConsumptions, Has.Count.EqualTo(2));
            Assert.That(replayEvent.AbilityConsumptions[0].EvidenceKind, Is.EqualTo("RuntimeRead"));
            Assert.That(replayEvent.Trajectory.ArtifactIdentity, Is.EqualTo(HashC));
            Assert.That(replayEvent.Trajectory.PredictorSource, Is.EqualTo("formal-v4"));
            Assert.That(replayEvent.Trajectory.CacheKey.BallStateVersion, Is.EqualTo(42));
            Assert.That(replayEvent.Trajectory.CacheKey.EnvelopeIdentity, Is.EqualTo(HashA));
            Assert.That(replayEvent.Trajectory.CacheKey.DegradationStep, Is.EqualTo("ReducedSampleCount"));
            Assert.That(replayEvent.Classification.Kind, Is.EqualTo("EnvelopeExpanded"));
            Assert.That(replayEvent.Classification.OffendingDimensions, Is.EqualTo(new[] { "target.error.x" }));
            Assert.That(replayEvent.ObservedP6Geometry.IsTakeoffInFrontZone, Is.True);
            Assert.That(replayEvent.ObservedP6Geometry.IsContactAboveNet, Is.True);
            Assert.That(replayEvent.RuleDecision.RulesVersion, Is.EqualTo(3));
            Assert.That(replayEvent.RuleDecision.Accepted, Is.True);
            Assert.That(replayEvent.RuleDecision.ReasonCode, Is.EqualTo("None"));
        }

        [Test]
        public void Create_RejectsDuplicateAndGappedEventSequences()
        {
            Assert.That(
                () => CreateReplay(Event(0, "Attack"), Event(0, "Receive")),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("duplicate"));
            Assert.That(
                () => CreateReplay(Event(0, "Attack"), Event(2, "Receive")),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("gap"));
        }

        [Test]
        public void Deserialize_RejectsDuplicateAndGappedEventSequences()
        {
            var json = ContractJson.SerializeV4(
                CreateReplay(Event(0, "Attack"), Event(1, "Receive")));
            var duplicate = json.Replace(
                "\"sequenceNumber\":1,\"eventKind\":\"Receive\"",
                "\"sequenceNumber\":0,\"eventKind\":\"Receive\"");
            var gap = json.Replace(
                "\"sequenceNumber\":1,\"eventKind\":\"Receive\"",
                "\"sequenceNumber\":2,\"eventKind\":\"Receive\"");

            Assert.That(duplicate, Is.Not.EqualTo(json));
            Assert.That(gap, Is.Not.EqualTo(json));
            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4(duplicate),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4(gap),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void Create_RequiresEveryEvidenceRecordAndAttackOnlyP6Geometry()
        {
            var attack = Event(0, "Attack");
            var receive = Event(0, "Receive");

            Assert.That(
                () => new MatchReplayEventV4(
                    attack.SequenceNumber,
                    attack.EventKind,
                    attack.ActorPlayerId,
                    attack.SimulationTimeSeconds,
                    attack.HomeScore,
                    attack.AwayScore,
                    null,
                    attack.Trajectory,
                    attack.AbilityConsumptions,
                    attack.Classification,
                    attack.ObservedP6Geometry,
                    attack.RuleDecision),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new MatchReplayEventV4(
                    receive.SequenceNumber,
                    receive.EventKind,
                    receive.ActorPlayerId,
                    receive.SimulationTimeSeconds,
                    receive.HomeScore,
                    receive.AwayScore,
                    receive.Envelope,
                    receive.Trajectory,
                    receive.AbilityConsumptions,
                    receive.Classification,
                    Geometry(),
                    receive.RuleDecision),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("non-attack"));
            Assert.That(
                () => new MatchReplayEventV4(
                    attack.SequenceNumber,
                    attack.EventKind,
                    attack.ActorPlayerId,
                    attack.SimulationTimeSeconds,
                    attack.HomeScore,
                    attack.AwayScore,
                    attack.Envelope,
                    attack.Trajectory,
                    attack.AbilityConsumptions,
                    attack.Classification,
                    null,
                    attack.RuleDecision),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("Attack"));
        }

        [Test]
        public void Deserialize_RejectsMissingMandatoryEvidenceLegacyVersionsAndTamperedHash()
        {
            var json = ContractJson.SerializeV4(CreateReplay(Event(0, "Attack")));
            var missing = json.Replace("\"trajectory\":{", "\"missingTrajectory\":{");
            var tamperedHash = json.Replace(
                "\"replayHash\":\"" + CreateReplay(Event(0, "Attack")).ReplayHash + "\"",
                "\"replayHash\":\"" + new string('0', 64) + "\"");

            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4(missing),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4(tamperedHash),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("replayHash"));
            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4("{\"formatVersion\":1}"),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4("{\"formatVersion\":2}"),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4("{\"formatVersion\":3}"),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void AbilityConsumption_RejectsSerializationOnlyClaimsAndUnknownFields()
        {
            Assert.That(
                () => new ReplayAbilityConsumptionRecordV4(
                    "home-opposite",
                    HashA,
                    "Attack.DirectionControl",
                    0.75f,
                    "Serialized"),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("RuntimeRead"));
            Assert.That(
                () => new ReplayAbilityConsumptionRecordV4(
                    "home-opposite",
                    HashA,
                    "Unknown.Decoration",
                    0.75f,
                    "RuntimeRead"),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void HtmlWriter_RendersOnlyNativeV4Diagnostics()
        {
            var outputDirectory = Path.Combine(
                Path.GetTempPath(),
                "volleyball-match-replay-v4-tests",
                Guid.NewGuid().ToString("N"));

            MatchReplayArtifactWriter.Write(
                outputDirectory,
                CreateReplay(Event(0, "Attack")));

            var html = File.ReadAllText(Path.Combine(outputDirectory, "index.html"));
            Assert.That(html, Does.Contain("MatchReplayV4"));
            Assert.That(html, Does.Contain("Contract version 4"));
            Assert.That(html, Does.Contain("Rules version 3"));
            Assert.That(html, Does.Contain("Execution envelope"));
            Assert.That(html, Does.Contain("Trajectory artifact"));
            Assert.That(html, Does.Contain("Actual sample classification"));
            Assert.That(html, Does.Contain("Observed P6 geometry"));
            Assert.That(html, Does.Not.Contain("reserved V2"));
            Assert.That(
                File.ReadAllText(Path.Combine(outputDirectory, "replay.json")),
                Is.EqualTo(ContractJson.SerializeV4(CreateReplay(Event(0, "Attack")))));
        }

        private static MatchReplayV4 CreateReplay(params MatchReplayEventV4[] events)
        {
            return MatchReplayV4.Create(
                "formal-replay-7351",
                MatchV4TestFixture.CreateContext(),
                events);
        }

        private static MatchReplayEventV4 Event(int sequence, string kind)
        {
            var attack = string.Equals(kind, "Attack", StringComparison.Ordinal);
            var consumptions = attack
                ? new[]
                {
                    new ReplayAbilityConsumptionRecordV4(
                        "home-opposite",
                        DerivedFingerprint,
                        "Attack.PowerCapacity",
                        0.81f,
                        "RuntimeRead"),
                    new ReplayAbilityConsumptionRecordV4(
                        "home-opposite",
                        DerivedFingerprint,
                        "Attack.DirectionControl",
                        0.76f,
                        "RuntimeRead")
                }
                : new[]
                {
                    new ReplayAbilityConsumptionRecordV4(
                        "home-opposite",
                        DerivedFingerprint,
                        "Receive.FirstTouchControl",
                        0.74f,
                        "RuntimeRead")
                };

            return new MatchReplayEventV4(
                sequence,
                kind,
                "home-opposite",
                1.25f + sequence,
                4,
                3,
                Envelope(kind),
                Trajectory(),
                consumptions,
                Classification(kind),
                attack ? Geometry() : null,
                new ReplayRuleDecisionRecordV4(3, true, "None"));
        }

        private static ReplayExecutionEnvelopeRecordV4 Envelope(string kind)
        {
            return new ReplayExecutionEnvelopeRecordV4(
                4,
                HashA,
                DerivedFingerprint,
                HashB,
                string.Equals(kind, "Attack", StringComparison.Ordinal)
                    ? "attack-intent-0"
                    : "receive-intent-0",
                kind,
                new ReplayVector3RecordV4(1f, 2f, 3f),
                new ReplayVector3RecordV4(4f, 5f, 6f),
                new ReplayVector3RecordV4(8f, 9f, 10f),
                new ReplayBoundedErrorRecordV4(
                    "BoundedUniform",
                    new ReplayVector3RecordV4(-0.1f, -0.2f, -0.3f),
                    new ReplayVector3RecordV4(0.1f, 0.2f, 0.3f)),
                new ReplayBoundedErrorRecordV4(
                    "SymmetricTriangular",
                    new ReplayVector3RecordV4(-0.1f, -0.2f, -0.3f),
                    new ReplayVector3RecordV4(0.1f, 0.2f, 0.3f)),
                0.8f,
                0.95f,
                "sampling-7351-0",
                1,
                7,
                new[] { "Receive", "Set", "Attack", "Block", "Serve" },
                new[] { "FullSampling", "ReducedSampleCount", "CachedCoarseDistribution", "DeterministicSafeFallback" },
                2,
                1,
                1,
                1.5f);
        }

        private static ReplayTrajectoryArtifactRecordV4 Trajectory()
        {
            return new ReplayTrajectoryArtifactRecordV4(
                HashC,
                "formal-v4",
                TestContext.TrajectoryPredictionProviderConfiguration
                    .PredictorVersion,
                TestContext.TrajectoryPredictionProviderConfiguration
                    .PredictorConfigurationHash,
                new ReplayTrajectoryCacheKeyRecordV4(
                    HashB,
                    42,
                    HashA,
                    TestContext.PhysicsConfigurationHash,
                    "sampling-7351-0",
                    TestContext.TrajectoryPredictionProviderConfiguration
                        .PredictorVersion,
                    TestContext.TrajectoryPredictionProviderConfiguration
                        .PredictorConfigurationHash,
                    HashA,
                    "ReducedSampleCount"));
        }

        private static ReplaySampleClassificationRecordV4 Classification(
            string kind)
        {
            return new ReplaySampleClassificationRecordV4(
                "EnvelopeExpanded",
                HashA,
                HashB,
                new ReplayActualSampleRecordV4(
                    HashA,
                    "sampling-7351-0",
                    kind,
                    new ReplayVector3RecordV4(1.1f, 2f, 3f),
                    new ReplayVector3RecordV4(4f, 5f, 6f),
                    0.8f),
                new[] { "target.error.x" });
        }

        private static ReplayObservedP6GeometryRecordV4 Geometry()
        {
            return new ReplayObservedP6GeometryRecordV4(
                "home-opposite",
                "Home",
                new ReplayVector3RecordV4(0.4f, 0f, -2.5f),
                new ReplayVector3RecordV4(0.6f, 2.75f, -1.8f),
                3f,
                2.43f);
        }

        private static MatchContextV4 TestContext =>
            MatchV4TestFixture.CreateContext();

        private static string DerivedFingerprint =>
            TestContext.Home.RotationOrder[0].Derived.ResultFingerprint;
    }
}
