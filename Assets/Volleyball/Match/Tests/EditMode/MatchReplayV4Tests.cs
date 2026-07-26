using System;
using System.IO;
using NUnit.Framework;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;
using PrototypeTeamId = Volleyball.Domain.Prototype.TeamId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;
using TechniqueAction = Volleyball.Domain.Players.TechniqueAction;

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
            Assert.That(restored.Context.RulesVersion, Is.EqualTo(RulesVersions.FullRallyV3));
            Assert.That(restored.Context.FormulaVersion, Is.EqualTo(1));
            Assert.That(restored.Context.CoefficientVersion, Is.EqualTo(1));
            Assert.That(restored.Context.Home.RotationOrder[0].DominantHand, Is.EqualTo(DominantHandV4.Right));
            Assert.That(restored.Context.Home.RotationOrder[0].Derived.InputFingerprint, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(restored.Context.Home.RotationOrder[0].Derived.ResultFingerprint, Does.Match("^[0-9a-f]{64}$"));

            Assert.That(replayEvent.TestedEnvelope.Identity, Is.EqualTo(HashA));
            Assert.That(replayEvent.ExecutableEnvelope.Identity, Is.EqualTo(HashB));
            Assert.That(replayEvent.TestedEnvelope.PolicyIdentity, Is.EqualTo(HashB));
            Assert.That(replayEvent.TestedEnvelope.SourceIntentIdentity, Is.EqualTo("attack-intent-0"));
            Assert.That(replayEvent.TestedEnvelope.TargetError.Minimum.X, Is.EqualTo(-0.1f));
            Assert.That(replayEvent.TestedEnvelope.VelocityError.Maximum.Z, Is.EqualTo(0.3f));
            Assert.That(replayEvent.TestedEnvelope.MaximumEffort, Is.EqualTo(0.95f));
            Assert.That(replayEvent.TestedEnvelope.DegradationLadder, Is.EqualTo(
                new[] { "FullSampling", "ReducedSampleCount", "CachedCoarseDistribution", "DeterministicSafeFallback" }));

            Assert.That(replayEvent.AbilityConsumptions, Has.Count.EqualTo(2));
            Assert.That(
                replayEvent.AbilityConsumptions[0].EvidenceKind,
                Is.EqualTo("ExecutionEnvelopeFactoryRead"));
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
        public void CanonicalJson_RoundTripsOptionalShadowPlansAfterRuleDecision()
        {
            var original = EventWithShadow(0, "Attack", 0);
            var json = ContractJson.SerializeV4(CreateReplay(original));
            var restored = ContractJson.DeserializeMatchReplayV4(json);

            Assert.That(json, Does.Contain(
                "\"ruleDecision\":{\"rulesVersion\":3,\"accepted\":true,\"reasonCode\":\"None\"},\"shadow\":{"));
            Assert.That(json, Does.Contain(
                "\"rank\":1,\"playerId\":\"home-opposite\",\"task\":\"Receive\",\"condition\":\"Always\",\"spatialClaim\":\"CourtZone\",\"declaredBranch\":\"Primary\",\"value\":0.5"));
            Assert.That(restored.Events[0].Shadow.Revision, Is.Zero);
            Assert.That(restored.Events[0].Shadow.SourceSequenceNumber, Is.EqualTo(1));
            Assert.That(restored.Events[0].Shadow.ArtifactIdentity, Is.EqualTo(HashC));
            Assert.That(restored.Events[0].Shadow.Home.TeamSide, Is.EqualTo("Home"));
            Assert.That(restored.Events[0].Shadow.Away.TeamSide, Is.EqualTo("Away"));
            Assert.That(restored.Events[0].Shadow.Home.PrimaryAssignments[0].Rank, Is.EqualTo(1));
            Assert.That(restored.Events[0].Shadow.Home.PrimaryAssignments[0].Task, Is.EqualTo("Receive"));
            Assert.That(restored.Events[0].Shadow.Home.PrimaryAssignments[0].Condition, Is.EqualTo("Always"));
            Assert.That(restored.Events[0].Shadow.Home.PrimaryAssignments[0].SpatialClaim, Is.EqualTo("CourtZone"));
            Assert.That(restored.Events[0].Shadow.Home.PrimaryAssignments[0].DeclaredBranch, Is.EqualTo("Primary"));
            Assert.That(restored.Events[0].Shadow.Home.PrimaryAssignments[0].Value, Is.EqualTo(0.5f));
            Assert.That(restored.Events[0].Shadow.Coverage.Decision, Is.EqualTo("Covered"));
            Assert.That(restored.Events[0].Shadow.Coverage.Reason, Is.EqualTo("WithinConditionalEnvelope"));
            Assert.That(restored.Events[0].Shadow.Coverage.InvalidationSet, Is.Empty);
            Assert.That(restored.Events[0].Shadow.Coverage.ExpansionDepth, Is.Zero);
            Assert.That(restored.Events[0].Shadow.Coverage.ActivatedDeclaredBranch, Is.Null);
            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(json));
        }

        [Test]
        public void CanonicalJson_PreservesCompleteUncoveredShadowCoverageDecision()
        {
            var shadow = new ReplayShadowRecordV4(
                7,
                1,
                HashC,
                TeamPlan("Home", "home"),
                TeamPlan("Away", "away"),
                new ReplayCoverageDecisionRecordV4(
                    "Scoped",
                    0f,
                    "BallEnvelopeExceeded",
                    new[] { "condition=Always", "player=home-opposite" },
                    2,
                    null));
            var json = ContractJson.SerializeV4(CreateReplay(EventWithShadow(Event(0, "Attack"), shadow)));
            var coverage = ContractJson.DeserializeMatchReplayV4(json).Events[0].Shadow.Coverage;

            Assert.That(json, Does.Contain("\"decision\":\"Scoped\",\"score\":0,\"reason\":\"BallEnvelopeExceeded\",\"invalidationSet\":[\"condition=Always\",\"player=home-opposite\"],\"expansionDepth\":2,\"activatedDeclaredBranch\":null"));
            Assert.That(coverage.Decision, Is.EqualTo("Scoped"));
            Assert.That(coverage.Reason, Is.EqualTo("BallEnvelopeExceeded"));
            Assert.That(coverage.InvalidationSet, Is.EqualTo(new[] { "condition=Always", "player=home-opposite" }));
            Assert.That(coverage.ExpansionDepth, Is.EqualTo(2));
            Assert.That(coverage.ActivatedDeclaredBranch, Is.Null);
        }

        [Test]
        public void CanonicalJson_DeserializesF4EraShadowCoverageWithOnlyUncoveredDecisionAndScore()
        {
            var canonical = ContractJson.SerializeV4(CreateReplay(EventWithShadow(0, "Attack", 0)));
            var coverageStart = canonical.IndexOf("\"coverage\":", StringComparison.Ordinal);
            var coverageEnd = canonical.IndexOf("}", coverageStart);
            var legacy = canonical.Substring(0, coverageStart) +
                "\"coverage\":{\"decision\":\"Uncovered\",\"score\":0" +
                canonical.Substring(coverageEnd);

            var restored = ContractJson.DeserializeMatchReplayV4(legacy);
            var coverage = restored.Events[0].Shadow.Coverage;
            var normalized = ContractJson.SerializeV4(restored);

            Assert.That(coverage.Decision, Is.EqualTo("Terminal"));
            Assert.That(coverage.Score, Is.Zero);
            Assert.That(coverage.Reason, Is.EqualTo("RallyEnd"));
            Assert.That(coverage.InvalidationSet, Is.Empty);
            Assert.That(coverage.ExpansionDepth, Is.Zero);
            Assert.That(coverage.ActivatedDeclaredBranch, Is.Null);
            Assert.That(normalized, Does.Contain("\"decision\":\"Terminal\",\"score\":0,\"reason\":\"RallyEnd\",\"invalidationSet\":[],\"expansionDepth\":0,\"activatedDeclaredBranch\":null"));
        }

        [Test]
        public void CanonicalJson_DeserializesF4EraShadowCoverageWithOnlyCoveredDecisionAndScore()
        {
            var canonical = ContractJson.SerializeV4(CreateReplay(EventWithShadow(0, "Attack", 0)));
            var coverageStart = canonical.IndexOf("\"coverage\":", StringComparison.Ordinal);
            var coverageEnd = canonical.IndexOf("}", coverageStart);
            var legacy = canonical.Substring(0, coverageStart) +
                "\"coverage\":{\"decision\":\"Covered\",\"score\":0.75" +
                canonical.Substring(coverageEnd);

            var coverage = ContractJson.DeserializeMatchReplayV4(legacy).Events[0].Shadow.Coverage;

            Assert.That(coverage.Decision, Is.EqualTo("Covered"));
            Assert.That(coverage.Score, Is.EqualTo(0.75f));
            Assert.That(coverage.Reason, Is.EqualTo("WithinConditionalEnvelope"));
        }

        [Test]
        public void ShadowRecord_RequiresBothSidesAndValidAssignmentsAndCoverage()
        {
            Assert.That(
                () => new ReplayShadowRecordV4(
                    0, 0, HashC, null, TeamPlan("Away", "away"),
                    new ReplayCoverageDecisionRecordV4("Covered", 0.75f)),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayShadowRecordV4(
                    0, 0, HashC, TeamPlan("Home", "home"), null,
                    new ReplayCoverageDecisionRecordV4("Covered", 0.75f)),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayTeamRallyPlanRecordV4(
                    "Home",
                    DuplicatePlayerAssignments()),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayTeamRallyPlanRecordV4(
                    "Home",
                    DuplicateRankAssignments()),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayShadowAssignmentRecordV4(
                    1, "home-player-1", "InvalidTask", "Always", "CourtZone", "Primary", 0.5f),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayShadowAssignmentRecordV4(
                    1, "home-player-1", "Receive", "InvalidCondition", "CourtZone",
                    "Primary", 0.5f),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayShadowAssignmentRecordV4(
                    1, "home-player-1", "Receive", "Always", "InvalidClaim", "Primary",
                    0.5f),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayShadowAssignmentRecordV4(
                    1, "home-player-1", "Receive", "Always", "CourtZone", "InvalidBranch",
                    0.5f),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayCoverageDecisionRecordV4("InvalidCoverage", 0.75f),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void ShadowRecord_RejectsNonFiniteAssignmentAndCoverageScores()
        {
            Assert.That(
                () => new ReplayShadowAssignmentRecordV4(
                    1, "home-player-1", "Receive", "Always", "CourtZone", "Primary", float.NaN),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new ReplayCoverageDecisionRecordV4(
                    "Covered", float.PositiveInfinity),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void Create_RejectsShadowAssignmentsSwappedAcrossTeamSides()
        {
            var swappedHome = new ReplayTeamRallyPlanRecordV4(
                "Home",
                TeamPlan("Away", "away").PrimaryAssignments);
            var shadow = new ReplayShadowRecordV4(
                7,
                1,
                HashC,
                swappedHome,
                TeamPlan("Away", "away"),
                new ReplayCoverageDecisionRecordV4("Covered", 0.75f));

            Assert.That(
                () => CreateReplay(EventWithShadow(Event(0, "Attack"), shadow)),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void Create_RejectsShadowAssignmentsForNonexistentPlayer()
        {
            var shadow = new ReplayShadowRecordV4(
                7,
                1,
                HashC,
                TeamPlanWithPlayer("Home", "home", 0, "missing-player"),
                TeamPlan("Away", "away"),
                new ReplayCoverageDecisionRecordV4("Covered", 0.75f));

            Assert.That(
                () => CreateReplay(EventWithShadow(Event(0, "Attack"), shadow)),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void Create_RejectsShadowPlayerDuplicatedAcrossTeamPlans()
        {
            var shadow = new ReplayShadowRecordV4(
                7,
                1,
                HashC,
                TeamPlan("Home", "home"),
                TeamPlanWithPlayer("Away", "away", 0, "home-opposite"),
                new ReplayCoverageDecisionRecordV4("Covered", 0.75f));

            Assert.That(
                () => CreateReplay(EventWithShadow(Event(0, "Attack"), shadow)),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void Deserialize_LegacyV4EventWithoutShadowReturnsNull()
        {
            var json = ContractJson.SerializeV4(CreateReplay(Event(0, "Attack")));
            var restored = ContractJson.DeserializeMatchReplayV4(json);

            Assert.That(restored.Events[0].Shadow, Is.Null);
        }

        [Test]
        public void Create_RejectsShadowWhoseArtifactDiffersFromTrajectory()
        {
            var baseline = Event(0, "Attack");
            var shadow = new ReplayShadowRecordV4(
                7,
                1,
                HashA,
                TeamPlan("Home", "home"),
                TeamPlan("Away", "away"),
                new ReplayCoverageDecisionRecordV4("Covered", 0.75f));

            Assert.That(
                () => new MatchReplayEventV4(
                    baseline.SequenceNumber,
                    baseline.EventKind,
                    baseline.ActorPlayerId,
                    baseline.SimulationTimeSeconds,
                    baseline.HomeScore,
                    baseline.AwayScore,
                    baseline.TestedEnvelope,
                    baseline.ExecutableEnvelope,
                    baseline.Trajectory,
                    baseline.AbilityConsumptions,
                    baseline.Classification,
                    baseline.ObservedP6Geometry,
                    baseline.RuleDecision,
                    shadow),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("artifact"));
        }

        [Test]
        public void RecorderMapping_ExpandedContactPersistsTestedAndExecutableEnvelopes()
        {
            var derived = MatchV4TestFixture.CreateDerived();
            var policy = new ExecutionEnvelopePolicyV4(
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
                1,
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
            var tested = ExecutionEnvelopeFactoryV4.Create(
                derived,
                new ExecutionIntentV4(
                    "expanded-recording-contact",
                    ExecutionCandidateCategoryV4.Receive,
                    new SimVector3(1f, 2f, 3f),
                    new SimVector3(4f, 5f, 6f),
                    0.5f),
                "expanded-recording-sample",
                policy);
            var sample = new ExecutionSampleV4(
                tested.Identity,
                tested.Sampling.SamplingKey,
                ExecutionCandidateCategoryV4.Receive,
                new SimVector3(
                    tested.BaselineTarget.X +
                    (tested.TargetError.MaximumAbsoluteError.X * 1.2f),
                    tested.BaselineTarget.Y,
                    tested.BaselineTarget.Z),
                tested.BaselineVelocity,
                tested.RequestedEffort);
            var classification = tested.Classify(sample);
            var context = MatchV4TestFixture.CreateContext();
            var parameters =
                new BallSimulationParameters(-9.8f, 0.9995f);
            var trajectory =
                PhysicalMatchRallyDirector
                    .CreateTrajectoryPredictionProviderV4(context)
                    .Predict(
                        new BallTrajectoryPredictionRequestV4(
                            TeamSide.Home,
                            7,
                            new BallState(
                                new SimVector3(0f, 3f, -2f),
                                new SimVector3(1f, 4f, 5f),
                                0.12f),
                            parameters,
                            context.PhysicsConfigurationHash,
                            "expanded-recording-trajectory",
                            context
                                .TrajectoryPredictionProviderConfiguration
                                .PredictorVersion,
                            context
                                .TrajectoryPredictionProviderConfiguration
                                .PredictorConfigurationHash,
                            tested.Identity,
                            ExecutionDegradationStepV4.FullSampling));
            var actor = new StablePlayerId("home-opposite");
            var transition = RallyRulesEngineV3.Open(TeamSide.Home).Apply(
                new ActualContactEventV3(
                    actor,
                    TeamSide.Home,
                    RallyContactClassificationV3.TeamContact,
                    17));
            var replayEvent = MatchReplayRecorder.CreateContactRecordV4(
                0,
                new ReplayContactEvent(
                    "Contact",
                    1.25f,
                    PrototypeTeamId.Blue,
                    actor,
                    TechniqueAction.Receive,
                    ruleTransition: transition,
                    executionClassification: classification,
                    trajectoryArtifact: trajectory),
                0,
                0);

            Assert.That(
                classification.Kind,
                Is.EqualTo(
                    ExecutionSampleClassificationKindV4.EnvelopeExpanded));
            Assert.That(
                replayEvent.TestedEnvelope.Identity,
                Is.EqualTo(classification.TestedEnvelopeIdentity));
            Assert.That(
                replayEvent.ExecutableEnvelope.Identity,
                Is.EqualTo(classification.ExpandedEnvelopeIdentity));
            Assert.That(
                replayEvent.ExecutableEnvelope.CurrentExpansionCount,
                Is.EqualTo(1));
            Assert.That(
                replayEvent.AbilityConsumptions,
                Has.All.Property("EvidenceKind")
                    .EqualTo("ExecutionEnvelopeFactoryRead"));
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
                    attack.ExecutableEnvelope,
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
                    receive.TestedEnvelope,
                    receive.ExecutableEnvelope,
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
                    attack.TestedEnvelope,
                    attack.ExecutableEnvelope,
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
                    .With.Message.Contains(
                        "ExecutionEnvelopeFactoryRead"));
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
                        "ExecutionEnvelopeFactoryRead"),
                    new ReplayAbilityConsumptionRecordV4(
                        "home-opposite",
                        DerivedFingerprint,
                        "Attack.DirectionControl",
                        0.76f,
                        "ExecutionEnvelopeFactoryRead")
                }
                : new[]
                {
                    new ReplayAbilityConsumptionRecordV4(
                        "home-opposite",
                        DerivedFingerprint,
                        "Receive.FirstTouchControl",
                        0.74f,
                        "ExecutionEnvelopeFactoryRead")
                };

            return new MatchReplayEventV4(
                sequence,
                kind,
                "home-opposite",
                1.25f + sequence,
                4,
                3,
                Envelope(kind, HashA, 0),
                Envelope(kind, HashB, 1),
                Trajectory(),
                consumptions,
                Classification(kind),
                attack ? Geometry() : null,
                new ReplayRuleDecisionRecordV4(3, true, "None"));
        }

        private static MatchReplayEventV4 EventWithShadow(
            int sequence,
            string kind,
            int revision = 7)
        {
            var baseline = Event(sequence, kind);
            return new MatchReplayEventV4(
                baseline.SequenceNumber,
                baseline.EventKind,
                baseline.ActorPlayerId,
                baseline.SimulationTimeSeconds,
                baseline.HomeScore,
                baseline.AwayScore,
                baseline.TestedEnvelope,
                baseline.ExecutableEnvelope,
                baseline.Trajectory,
                baseline.AbilityConsumptions,
                baseline.Classification,
                baseline.ObservedP6Geometry,
                baseline.RuleDecision,
                Shadow(sequence + 1, revision));
        }

        private static MatchReplayEventV4 EventWithShadow(
            MatchReplayEventV4 baseline,
            ReplayShadowRecordV4 shadow)
        {
            return new MatchReplayEventV4(
                baseline.SequenceNumber,
                baseline.EventKind,
                baseline.ActorPlayerId,
                baseline.SimulationTimeSeconds,
                baseline.HomeScore,
                baseline.AwayScore,
                baseline.TestedEnvelope,
                baseline.ExecutableEnvelope,
                baseline.Trajectory,
                baseline.AbilityConsumptions,
                baseline.Classification,
                baseline.ObservedP6Geometry,
                baseline.RuleDecision,
                shadow);
        }

        private static ReplayShadowRecordV4 Shadow(
            int sourceSequenceNumber,
            int revision = 7)
        {
            return new ReplayShadowRecordV4(
                revision,
                sourceSequenceNumber,
                HashC,
                TeamPlan("Home", "home"),
                TeamPlan("Away", "away"),
                new ReplayCoverageDecisionRecordV4("Covered", 0.75f));
        }

        private static ReplayTeamRallyPlanRecordV4 TeamPlan(
            string teamSide,
            string prefix)
        {
            var assignments = new ReplayShadowAssignmentRecordV4[6];
            for (var index = 0; index < assignments.Length; index++)
            {
                assignments[index] = new ReplayShadowAssignmentRecordV4(
                    index + 1,
                    PlayerIdFor(teamSide, index),
                    "Receive",
                    "Always",
                    "CourtZone",
                    "Primary",
                    0.5f + (index * 0.01f));
            }

            return new ReplayTeamRallyPlanRecordV4(
                teamSide,
                assignments);
        }

        private static ReplayTeamRallyPlanRecordV4 TeamPlanWithPlayer(
            string teamSide,
            string prefix,
            int assignmentIndex,
            string playerId)
        {
            var assignments = TeamPlan(teamSide, prefix).PrimaryAssignments;
            var replacement = new ReplayShadowAssignmentRecordV4(
                assignments[assignmentIndex].Rank,
                playerId,
                assignments[assignmentIndex].Task,
                assignments[assignmentIndex].Condition,
                assignments[assignmentIndex].SpatialClaim,
                assignments[assignmentIndex].DeclaredBranch,
                assignments[assignmentIndex].Value);
            var copy = new ReplayShadowAssignmentRecordV4[assignments.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = index == assignmentIndex
                    ? replacement
                    : assignments[index];
            }

            return new ReplayTeamRallyPlanRecordV4(teamSide, copy);
        }

        private static string PlayerIdFor(string teamSide, int index)
        {
            var prefix = teamSide == "Home" ? "home" : "away";
            var role = new[]
            {
                "opposite",
                "outside-a",
                "middle-a",
                "setter",
                "outside-b",
                "libero"
            }[index];
            return prefix + "-" + role;
        }

        private static ReplayShadowAssignmentRecordV4[] DuplicatePlayerAssignments()
        {
            var assignments = TeamPlan("Home", "home").PrimaryAssignments;
            var duplicate = new ReplayShadowAssignmentRecordV4[6];
            for (var index = 0; index < duplicate.Length; index++)
            {
                duplicate[index] = assignments[index];
            }

            duplicate[5] = new ReplayShadowAssignmentRecordV4(
                6,
                duplicate[0].PlayerId,
                "Receive",
                "Always",
                "CourtZone",
                "Primary",
                0.55f);
            return duplicate;
        }

        private static ReplayShadowAssignmentRecordV4[] DuplicateRankAssignments()
        {
            var assignments = TeamPlan("Home", "home").PrimaryAssignments;
            var duplicate = new ReplayShadowAssignmentRecordV4[6];
            for (var index = 0; index < duplicate.Length; index++)
            {
                duplicate[index] = assignments[index];
            }

            duplicate[5] = new ReplayShadowAssignmentRecordV4(
                5,
                "home-libero",
                "Receive",
                "Always",
                "CourtZone",
                "Primary",
                0.55f);
            return duplicate;
        }

        private static ReplayExecutionEnvelopeRecordV4 Envelope(
            string kind,
            string identity,
            int currentExpansionCount)
        {
            return new ReplayExecutionEnvelopeRecordV4(
                4,
                identity,
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
                currentExpansionCount,
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
