using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Volleyball.AI;
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
        public void CanonicalJson_AlwaysCarriesTheDefenseAttemptTimeline()
        {
            var replay = CreateReplay(Event(0, "Attack"));

            Assert.That(
                ContractJson.SerializeV4(replay),
                Does.Contain("\"defenseAttempts\":[]"));
        }

        [Test]
        public void ScenarioProvenance_RoundTripsAndChangesCanonicalHash()
        {
            var baseline = CreateReplay(Event(0, "Attack"));
            var scenario = new ReplayScenarioProvenanceV4(
                "reachable-floor-defense", 1, HashA);
            var replay = MatchReplayV4.Create(
                "formal-replay-7351", MatchV4TestFixture.CreateContext(),
                new[] { Event(0, "Attack") },
                Array.Empty<ReplayDefenseAttemptRecordV4>(),
                scenario,
                0);
            var json = ContractJson.SerializeV4(replay);

            Assert.That(json, Does.Contain("\"scenarioId\":\"reachable-floor-defense\""));
            Assert.That(ContractJson.SerializeV4(
                ContractJson.DeserializeMatchReplayV4(json)), Is.EqualTo(json));
            Assert.That(replay.Scenario.ContentHash, Is.EqualTo(HashA));
            Assert.That(replay.ReplayHash, Is.Not.EqualTo(baseline.ReplayHash));
        }

        [Test]
        public void HistoricalReplayWithoutScenario_RestoresWithDefaultProvenance()
        {
            var replay = CreateReplay(Event(0, "Attack"));
            var current = ContractJson.SerializeV4(replay);
            var historical = current.Replace(
                ",\"scenario\":{\"scenarioId\":\"formal-indoor-6v6-default\",\"formatVersion\":1,\"contentHash\":\"" +
                ReplayScenarioProvenanceV4.DefaultContentHash + "\"}",
                string.Empty);
            historical = historical.Replace(replay.ReplayHash,
                CanonicalHashWithoutScenario(replay));

            var restored = ContractJson.DeserializeMatchReplayV4(historical);

            Assert.That(restored.Scenario.ScenarioId,
                Is.EqualTo(ReplayScenarioProvenanceV4.DefaultScenarioId));
        }

        [Test]
        public void DefenseAttemptTimeline_RoundTripsAndChangesCanonicalHash()
        {
            var baseline = CreateReplay(Event(0, "Attack"));
            var attempt = new ReplayDefenseAttemptRecordV4(
                "1:2:FloorDefense:away-defender:7101",
                "DefenseAttemptOpened", "FloorDefense", "away-defender",
                "Orange", 1, 2, HashA, HashB, 1f, 1.2f, 1f,
                new ReplayVector3RecordV4(0f, 1f, 2f),
                new ReplayVector3RecordV4(0f, -2f, 4f),
                "DefendingSideFloorDefense", "CommittedWindow");
            var replay = MatchReplayV4.Create(
                "formal-replay-7351", MatchV4TestFixture.CreateContext(),
                new[] { Event(0, "Attack") }, new[] { attempt }, 0);
            var json = ContractJson.SerializeV4(replay);

            Assert.That(ContractJson.SerializeV4(
                ContractJson.DeserializeMatchReplayV4(json)), Is.EqualTo(json));
            Assert.That(replay.DefenseAttempts[0].AttemptIdentity,
                Is.EqualTo(attempt.AttemptIdentity));
            Assert.That(replay.ReplayHash, Is.Not.EqualTo(baseline.ReplayHash));
        }

        [Test]
        public void HistoricalReplayWithoutDefenseAttempts_RestoresWithItsLegacyHash()
        {
            var replay = CreateReplay(Event(0, "Attack"));
            var current = ContractJson.SerializeV4(replay);
            var historical = current.Replace(",\"defenseAttempts\":[]", string.Empty);
            var legacyHash = CanonicalHashWithoutDefenseAttempts(replay);
            historical = historical.Replace(replay.ReplayHash, legacyHash);

            var restored = ContractJson.DeserializeMatchReplayV4(historical);

            Assert.That(restored.DefenseAttempts, Is.Empty);
        }

        [Test]
        public void HistoricalLegacyShadowWithoutDefenseAttempts_Restores()
        {
            var replay = CreateReplay(EventWithShadow(Event(0, "Attack"),
                Shadow(1)));
            var historical = ContractJson.SerializeV4(replay)
                .Replace(",\"defenseAttempts\":[]", string.Empty);
            historical = historical.Replace(replay.ReplayHash,
                CanonicalHashWithoutDefenseAttempts(replay, true));

            Assert.That(() => ContractJson.DeserializeMatchReplayV4(historical),
                Throws.Nothing);
        }

        [Test]
        public void GateJPerception_RoundTripsAndStrictReaderRejectsHiddenRouteField()
        {
            var baseline = Event(0, "Attack");
            var recorded = EventWithPerception(baseline,
                PerceptionAuthority());
            var json = ContractJson.SerializeV4(CreateReplay(recorded));
            var restored = ContractJson.DeserializeMatchReplayV4(json);

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(json));
            Assert.That(restored.Events[0].PerceptionAuthority.ViewIdentity,
                Is.EqualTo("gate-j-view-7"));
            Assert.That(restored.Events[0].PerceptionAuthority.VisibleThreats
                    .Select(value => value.Identity),
                Is.EqualTo(new[] { "threat-a", "threat-b" }));
            Assert.That(() => ContractJson.DeserializeMatchReplayV4(
                    json.Replace("\"confidence\":0.8",
                        "\"selectedRoute\":\"line\"")),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void HistoricalEventWithoutGateJ_RetainsCanonicalShape()
        {
            var json = ContractJson.SerializeV4(
                CreateReplay(Event(0, "Attack")));

            Assert.That(json, Does.Not.Contain("perceptionAuthority"));
            Assert.That(ContractJson.SerializeV4(
                    ContractJson.DeserializeMatchReplayV4(json)),
                Is.EqualTo(json));
        }

        [Test]
        public void GateKWorkBudget_RoundTripsAndChangesCanonicalHash()
        {
            var baseline = Event(0, "Attack");
            var recorded = EventWithWorkBudget(baseline,
                new ReplayWorkBudgetRecordV4(
                    HashB, 5, 7, 1, 70,
                    "ReducedSampleCount", "Degraded"));
            var replay = CreateReplay(recorded);
            var json = ContractJson.SerializeV4(replay);
            var restored = ContractJson.DeserializeMatchReplayV4(json);

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(json));
            Assert.That(restored.Events[0].WorkBudget.DeterministicWorkUnits,
                Is.EqualTo(70));
            Assert.That(replay.ReplayHash,
                Is.Not.EqualTo(CreateReplay(baseline).ReplayHash));
            Assert.That(json, Does.Not.Contain("wallClock"));
            Assert.That(json, Does.Not.Contain("cacheHit"));
        }

        [Test]
        public void GateKWorkBudget_StrictReaderRejectsProfilerField()
        {
            var recorded = EventWithWorkBudget(Event(0, "Attack"),
                new ReplayWorkBudgetRecordV4(
                    HashB, 5, 7, 1, 70,
                    "ReducedSampleCount", "Degraded"));
            var json = ContractJson.SerializeV4(CreateReplay(recorded));

            Assert.That(() => ContractJson.DeserializeMatchReplayV4(
                    json.Replace("\"budgetOutcome\":\"Degraded\"",
                        "\"budgetOutcome\":\"Degraded\"," +
                        "\"wallClockMilliseconds\":1")),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void GateIToolRecoveryMapper_RejectsMissingExactReboundEvidence()
        {
            var mapper = typeof(MatchReplayRecorder).GetMethod(
                "ToReplayRecovery",
                BindingFlags.Static | BindingFlags.NonPublic);
            var selected = new AttackCandidateV3(
                "tool-candidate",
                new StablePlayerId("home-attacker"),
                AttackActionClassV3.BlockToolRecovery,
                new SimVector3(0f, 3f, 0f),
                new SimVector3(0f, 1f, 3f),
                0.5f,
                1f,
                false,
                string.Empty,
                "envelope",
                "trajectory",
                "exit");

            var exception = Assert.Throws<TargetInvocationException>(
                () => mapper.Invoke(null, new object[] { null, selected, null }));

            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException.Message,
                Does.Contain("event-owned rebound evidence"));
        }

        [Test]
        public void GateIToolRecoveryMapper_UsesExactQualifiedEvidence()
        {
            var mapper = typeof(MatchReplayRecorder).GetMethod(
                "ToReplayRecovery",
                BindingFlags.Static | BindingFlags.NonPublic);
            var evidence = new ToolRecoveryEvidenceV3(
                "tool-candidate",
                new StablePlayerId("away-blocker"),
                TeamSide.Away,
                new StablePlayerId("away-saver"),
                1,
                "away-reorganize",
                "envelope",
                HashA,
                HashB,
                "rebound-sample",
                "block-contact");
            var selected = new AttackCandidateV3(
                "tool-candidate",
                new StablePlayerId("away-attacker"),
                AttackActionClassV3.BlockToolRecovery,
                new SimVector3(0f, 3f, 0f),
                new SimVector3(0f, 1f, -3f),
                .5f,
                1f,
                false,
                string.Empty,
                "envelope",
                HashA,
                "away-reorganize",
                evidence);

            var replay = (ReplayToolRecoveryRecordV4)mapper.Invoke(
                null, new object[] { null, selected, null });

            Assert.That(replay.BlockerPlayerId, Is.EqualTo("away-blocker"));
            Assert.That(replay.ReboundSide, Is.EqualTo("Away"));
            Assert.That(replay.RecoveryPlayerId, Is.EqualTo("away-saver"));
            Assert.That(replay.ReorganizationExitIdentity,
                Is.EqualTo("away-reorganize"));
            Assert.That(replay.ReboundTrajectoryArtifactIdentity,
                Is.EqualTo(HashB));
            Assert.That(replay.ReboundSampleIdentity, Is.EqualTo("rebound-sample"));
            Assert.That(replay.BlockContactIdentity, Is.EqualTo("block-contact"));
            Assert.That(replay.RemainingTouches, Is.EqualTo(1));
        }

        [Test]
        public void GateIToolRecoveryMapper_BlockObservationOverridesPlannedReboundEvidence()
        {
            var mapper = typeof(MatchReplayRecorder).GetMethod("ToReplayRecovery", BindingFlags.Static | BindingFlags.NonPublic);
            var evidence = new ToolRecoveryEvidenceV3("tool-candidate", new StablePlayerId("away-blocker"), TeamSide.Home,
                new StablePlayerId("home-saver"), 3, "tool-exit", "envelope", HashA, HashB, "planned-sample", "planned-contact");
            var selected = new AttackCandidateV3("tool-candidate", new StablePlayerId("home-attacker"),
                AttackActionClassV3.BlockToolRecovery, new SimVector3(0f, 3f, 0f), new SimVector3(0f, 1f, 3f),
                .5f, 1f, false, string.Empty, "envelope", HashA, "tool-exit", evidence);
            var actual = new ToolRecoveryActualObservationV3(TeamSide.Home, HashC, "actual-sample", "actual-contact", 2);
            var receipt = new AttackDefenseAuthorityReceipt(4, 7,
                AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive, AttackDefenseCommandKind.BlockContact,
                new StablePlayerId("away-blocker"), RallyPlanBranchV3.Primary, null, null,
                new AttackDefenseAuthorityEvidenceV3(4, 7, AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingReceive, null, null), actual);

            var replay = (ReplayToolRecoveryRecordV4)mapper.Invoke(null, new object[] { null, selected, receipt });

            Assert.That(replay.ReboundTrajectoryArtifactIdentity, Is.EqualTo(HashC));
            Assert.That(replay.ReboundSampleIdentity, Is.EqualTo("actual-sample"));
            Assert.That(replay.BlockContactIdentity, Is.EqualTo("actual-contact"));
            Assert.That(replay.RemainingTouches, Is.EqualTo(2));
        }

        [Test]
        public void GateIToolRecoveryMapper_BlockObservationMissing_FailsClosed()
        {
            var mapper = typeof(MatchReplayRecorder).GetMethod("ToReplayRecovery", BindingFlags.Static | BindingFlags.NonPublic);
            var evidence = new ToolRecoveryEvidenceV3("tool-candidate", new StablePlayerId("away-blocker"), TeamSide.Home,
                new StablePlayerId("home-saver"), 3, "tool-exit", "envelope", HashA, HashB, "planned-sample", "planned-contact");
            var selected = new AttackCandidateV3("tool-candidate", new StablePlayerId("home-attacker"),
                AttackActionClassV3.BlockToolRecovery, new SimVector3(0f, 3f, 0f), new SimVector3(0f, 1f, 3f),
                .5f, 1f, false, string.Empty, "envelope", HashA, "tool-exit", evidence);
            var receipt = new AttackDefenseAuthorityReceipt(4, 7,
                AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock, AttackDefenseCommandKind.BlockContact,
                new StablePlayerId("away-blocker"), RallyPlanBranchV3.Primary, null, null,
                new AttackDefenseAuthorityEvidenceV3(4, 7, AttackDefenseAuthorityPhaseV3.ToolRecoveryAwaitingBlock, null, null));

            var exception = Assert.Throws<TargetInvocationException>(() => mapper.Invoke(null, new object[] { null, selected, receipt }));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException.Message, Does.Contain("event-owned actual rebound"));
        }

        [Test]
        public void GateIToolRecoveryReplay_RoundTripsIndependentReboundEvidence()
        {
            var baseline = Event(0, "Attack");
            var candidate = new ReplayAttackDefenseCandidateRecordV4("tool-candidate", baseline.ActorPlayerId,
                "BlockToolRecovery", new ReplayVector3RecordV4(0f, 3f, 0f), .5f, 1f, false,
                string.Empty, baseline.ExecutableEnvelope.Identity, baseline.Trajectory.ArtifactIdentity, "tool-exit");
            var recovery = new ReplayToolRecoveryRecordV4("tool-candidate", "away-blocker", "Home",
                "home-saver", "tool-exit", HashA, "rebound-sample", "block-contact", 3);
            var authority = new ReplayAttackDefenseAuthorityRecordV4(7, 9, "AttackCommitted", "Primary",
                new ReplayVector3RecordV4(0f, 3f, -1f), new[] { candidate },
                new[] { new ReplayPublicAttackThreatRecordV4("BlockToolRecovery", "Line", 1f, 1f) },
                new[] { new ReplayDefenseResponsibilityRecordV4("away-blocker", "PrimaryBlock", "Line", "Primary") },
                "tool-candidate", baseline.TestedEnvelope.Identity, baseline.ExecutableEnvelope.Identity,
                baseline.Classification.ActualSample.EnvelopeIdentity, baseline.Trajectory.ArtifactIdentity,
                recovery, new ReplayCoverageDecisionRecordV4("Covered", 0f, "WithinConditionalEnvelope",
                    System.Array.Empty<string>(), 0, "Primary"));
            var recorded = new MatchReplayEventV4(baseline.SequenceNumber, baseline.EventKind,
                baseline.ActorPlayerId, baseline.SimulationTimeSeconds, baseline.HomeScore, baseline.AwayScore,
                baseline.TestedEnvelope, baseline.ExecutableEnvelope, baseline.Trajectory,
                baseline.AbilityConsumptions, baseline.Classification, baseline.ObservedP6Geometry,
                baseline.RuleDecision, baseline.Shadow, null, authority);
            var json = ContractJson.SerializeV4(CreateReplay(recorded));
            var restored = ContractJson.DeserializeMatchReplayV4(json);

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(json));
            Assert.That(restored.Events[0].AttackDefenseAuthority.Recovery.ReboundTrajectoryArtifactIdentity,
                Is.EqualTo(HashA));
            Assert.That(restored.Events[0].AttackDefenseAuthority.Recovery.ReboundSampleIdentity,
                Is.EqualTo("rebound-sample"));
            Assert.That(restored.Events[0].AttackDefenseAuthority.Recovery.BlockContactIdentity,
                Is.EqualTo("block-contact"));
            Assert.That(restored.Events[0].AttackDefenseAuthority.Recovery.RemainingTouches, Is.EqualTo(3));
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
            var terminalShadow = new ReplayShadowRecordV4(
                0,
                1,
                HashC,
                TeamPlan("Home", "home"),
                TeamPlan("Away", "away"),
                new ReplayCoverageDecisionRecordV4(
                    "Terminal",
                    0f,
                    "RallyEnd",
                    Array.Empty<string>(),
                    0,
                    null));
            var canonical = ContractJson.SerializeV4(
                CreateReplay(EventWithShadow(Event(0, "Attack"), terminalShadow)));
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
        public void Deserialize_LegacyShadowCoverageStillVerifiesReplayHash()
        {
            var canonical = ContractJson.SerializeV4(
                CreateReplay(EventWithShadow(0, "Attack", 0)));
            var coverageStart = canonical.IndexOf("\"coverage\":", StringComparison.Ordinal);
            var coverageEnd = canonical.IndexOf("}", coverageStart);
            var legacy = canonical.Substring(0, coverageStart) +
                "\"coverage\":{\"decision\":\"Covered\",\"score\":0.75" +
                canonical.Substring(coverageEnd);
            var tampered = legacy.Replace(
                "\"homeScore\":4",
                "\"homeScore\":5");

            Assert.That(tampered, Is.Not.EqualTo(legacy));
            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4(tampered),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("replayHash"));
        }

        [Test]
        public void Deserialize_AllLegacyShadowCoverageStillVerifiesHistoricalHash()
        {
            var replay = CreateReplay(
                EventWithShadow(0, "Attack", 0),
                EventWithShadow(1, "Receive", 1));
            var canonical = ContractJson.SerializeV4(replay);
            var legacy = ReplaceAllShadowCoverageWithLegacy(canonical);
            var historicalHash = ComputeLegacyShadowCoverageHash(replay);
            legacy = legacy.Replace(replay.ReplayHash, historicalHash);

            var restored = ContractJson.DeserializeMatchReplayV4(legacy);

            Assert.That(restored.Events, Has.Count.EqualTo(2));
            Assert.That(restored.Events[0].Shadow.Coverage.Decision, Is.EqualTo("Covered"));
            Assert.That(restored.Events[1].Shadow.Coverage.Decision, Is.EqualTo("Covered"));
        }

        [Test]
        public void Deserialize_RejectsMixedLegacyAndCurrentShadowCoverage()
        {
            var replay = CreateReplay(
                EventWithShadow(0, "Attack", 0),
                EventWithShadow(1, "Receive", 1));
            var canonical = ContractJson.SerializeV4(replay);
            var mixed = ReplaceShadowCoverageWithLegacy(canonical, 0)
                .Replace(replay.ReplayHash, ComputeLegacyShadowCoverageHash(replay));

            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4(mixed),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("Mixed"));
        }

        [Test]
        public void Deserialize_RejectsDetailedCoverageTamperingHiddenByMixedLegacyHash()
        {
            var replay = CreateReplay(
                EventWithShadow(0, "Attack", 0),
                EventWithShadow(Event(1, "Receive"), new ReplayShadowRecordV4(
                    8,
                    2,
                    HashC,
                    TeamPlan("Home", "home"),
                    TeamPlan("Away", "away"),
                    new ReplayCoverageDecisionRecordV4(
                        "Scoped",
                        0f,
                        "BallEnvelopeExceeded",
                        new[] { "condition=Always" },
                        1,
                        null))));
            var canonical = ContractJson.SerializeV4(replay);
            var tampered = ReplaceShadowCoverageWithLegacy(canonical, 0)
                .Replace("\"reason\":\"BallEnvelopeExceeded\"", "\"reason\":\"RallyEnd\"")
                .Replace(replay.ReplayHash, ComputeLegacyShadowCoverageHash(replay));

            Assert.That(
                () => ContractJson.DeserializeMatchReplayV4(tampered),
                Throws.TypeOf<ContractValidationException>());
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
        public void OrganizationAuthority_RoundTripsCanonicallyAndChangesReplayHash()
        {
            var baseline = Event(0, "Receive");
            var authority = OrganizationAuthority(baseline);
            var replay = CreateReplay(EventWithAuthority(baseline, authority));
            var json = ContractJson.SerializeV4(replay);

            var restored = ContractJson.DeserializeMatchReplayV4(json);
            var restoredAuthority = restored.Events[0].OrganizationAuthority;

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(json));
            Assert.That(restoredAuthority.PlanRevision, Is.EqualTo(7));
            Assert.That(restoredAuthority.SourceSequenceNumber, Is.EqualTo(3));
            Assert.That(restoredAuthority.OrganizationTarget.X, Is.EqualTo(1.5f));
            Assert.That(restoredAuthority.RegisteredSetterPlayerId, Is.EqualTo("home-setter"));
            Assert.That(
                restoredAuthority.ExecutableEnvelopeIdentity,
                Is.EqualTo(baseline.ExecutableEnvelope.Identity));
            Assert.That(
                restoredAuthority.TrajectoryArtifactIdentity,
                Is.EqualTo(baseline.Trajectory.ArtifactIdentity));

            var fallback = OrganizationAuthority(
                baseline,
                organizer: "home-outside-a",
                fallbackReason: "SetterUnreachable",
                setterStatus: "Unreachable");
            Assert.That(
                CreateReplay(EventWithAuthority(baseline, fallback)).ReplayHash,
                Is.Not.EqualTo(replay.ReplayHash));
        }

        [Test]
        public void OrganizationAuthority_RejectsIdentityMismatchAndNonReceiveSetEvent()
        {
            var receive = Event(0, "Receive");
            var mismatched = OrganizationAuthority(
                receive,
                executableEnvelopeIdentity: HashC);

            Assert.That(
                () => EventWithAuthority(receive, mismatched),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => EventWithAuthority(
                    Event(0, "Attack"),
                    OrganizationAuthority(receive)),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void Deserialize_HistoricalV4WithoutOrganizationAuthorityPreservesBytes()
        {
            var historical = ContractJson.SerializeV4(
                CreateReplay(Event(0, "Receive")));

            var restored = ContractJson.DeserializeMatchReplayV4(historical);

            Assert.That(restored.Events[0].OrganizationAuthority, Is.Null);
            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(historical));
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
        public void ShadowRecord_RequiresPositiveSourceSequence()
        {
            Assert.That(
                () => Shadow(0),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("sourceSequenceNumber"));
        }

        [Test]
        public void Create_RejectsDuplicateDecreasingAndUnrelatedShadowSources()
        {
            Assert.That(
                () => CreateReplay(
                    EventWithShadow(Event(0, "Attack"), Shadow(8)),
                    EventWithShadow(Event(1, "Receive"), Shadow(8))),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => CreateReplay(
                    EventWithShadow(Event(0, "Attack"), Shadow(9)),
                    EventWithShadow(Event(1, "Receive"), Shadow(8))),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => CreateReplay(
                    EventWithShadow(Event(0, "Attack"), Shadow(8)),
                    EventWithShadow(Event(1, "Receive"), Shadow(10))),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("anchor"));
        }

        [Test]
        public void Create_PreservesMidRallyCaptureSourceAnchor()
        {
            var replay = MatchReplayV4.Create(
                "mid-rally-replay",
                MatchV4TestFixture.CreateContext(),
                new[]
                {
                    EventWithShadow(Event(0, "Attack"), Shadow(18)),
                    EventWithShadow(Event(1, "Receive"), Shadow(19))
                },
                17);

            var restored = ContractJson.DeserializeMatchReplayV4(
                ContractJson.SerializeV4(replay));

            Assert.That(restored.SourceSequenceAnchor, Is.EqualTo(17));
            Assert.That(restored.Events[1].Shadow.SourceSequenceNumber, Is.EqualTo(19));
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
            var setter = new StablePlayerId("home-setter");
            var plan = new ReceiveOrganizationPlanV3(
                TeamSide.Home,
                7,
                actor,
                setter,
                new[] { new StablePlayerId("home-outside") },
                new[] { new StablePlayerId("home-libero") },
                new StablePlayerId("home-middle"),
                new SimVector3(1.5f, 0f, -1.1f));
            var evidence = new ReceiveOrganizationAuthorityEvidenceV3(
                7,
                9,
                ReceiveOrganizationAuthorityPhaseV3.ReceivePlanned,
                plan,
                new SetterReachabilityEvidenceV3(
                    new Volleyball.Domain.Prototype.PlayerId(
                        PrototypeTeamId.Blue,
                        Volleyball.Domain.Prototype.PlayerRole.Setter),
                    true,
                    true,
                    false,
                    true,
                    1.25f,
                    0.04f,
                    0.35f),
                OrganizationFallbackReasonV3.None,
                new PlanCoverageDecision(
                    PlanCoverageDecisionKind.CoveredActivateBranch,
                    "7",
                    PlanCoverageReason.WithinConditionalEnvelope,
                    Array.Empty<string>(),
                    0,
                    RallyPlanBranchV3.Primary),
                null);
            var receipt = new ReceiveOrganizationAuthorityReceipt(
                7,
                9,
                ReceiveOrganizationCommandKind.PrimaryReceive,
                actor,
                RallyPlanBranchV3.Primary,
                TechniqueAction.Receive,
                classification,
                trajectory,
                evidence);
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
                    trajectoryArtifact: trajectory,
                    organizationAuthority: receipt),
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
            Assert.That(replayEvent.OrganizationAuthority.PlanRevision, Is.EqualTo(7));
            Assert.That(replayEvent.OrganizationAuthority.SourceSequenceNumber, Is.EqualTo(9));
            Assert.That(
                replayEvent.OrganizationAuthority.TestedEnvelopeIdentity,
                Is.EqualTo(classification.TestedEnvelope.Identity));
            Assert.That(
                replayEvent.OrganizationAuthority.ExecutableEnvelopeIdentity,
                Is.EqualTo(classification.ExecutableEnvelope.Identity));
            Assert.That(
                replayEvent.OrganizationAuthority.SampleEnvelopeIdentity,
                Is.EqualTo(classification.Sample.EnvelopeIdentity));
            Assert.That(
                replayEvent.OrganizationAuthority.TrajectoryArtifactIdentity,
                Is.EqualTo(trajectory.ArtifactIdentity));
            Assert.That(
                replayEvent.OrganizationAuthority.Coverage.Reason,
                Is.EqualTo("WithinConditionalEnvelope"));
            Assert.That(
                replayEvent.OrganizationAuthority.Coverage.ActivatedDeclaredBranch,
                Is.EqualTo("Primary"));
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

        [Test]
        public void GateKHtml_RendersAuthoritativeAndEventOwnedPerspectivesDeterministically()
        {
            var withPerception = EventWithPerception(
                Event(0, "Attack"), PerceptionAuthority());
            var replay = CreateReplay(EventWithWorkBudget(
                withPerception,
                new ReplayWorkBudgetRecordV4(
                    HashB, 5, 7, 1, 70,
                    "ReducedSampleCount", "Degraded")));

            var first = MatchReplayArtifactWriter.Render(replay);
            var second = MatchReplayArtifactWriter.Render(replay);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("AUTHORITATIVE / ACTUAL"));
            Assert.That(first, Does.Contain("HOME PERCEIVED"));
            Assert.That(first, Does.Contain("AWAY PERCEIVED"));
            Assert.That(first, Does.Contain("gate-j-view-7"));
            Assert.That(first, Does.Contain("No event-owned view"));
            Assert.That(first, Does.Contain("Deterministic work units"));
            Assert.That(first, Does.Not.Contain("selectedRoute"));
            Assert.That(first, Does.Not.Contain("futureSample"));
        }

        private static MatchReplayV4 CreateReplay(params MatchReplayEventV4[] events)
        {
            return MatchReplayV4.Create(
                "formal-replay-7351",
                MatchV4TestFixture.CreateContext(),
                events);
        }

        private static string CanonicalHashWithoutDefenseAttempts(
            MatchReplayV4 replay, bool legacyShadow = false)
        {
            var method = typeof(MatchReplayV4).Assembly.GetType(
                "Volleyball.Shared.Contracts.CanonicalMatchReplayJsonV4")
                .GetMethod(legacyShadow
                        ? "ComputeLegacyShadowAndDefenseAttemptHash"
                        : "ComputeLegacyDefenseAttemptHash",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return (string)method.Invoke(null, new object[] { replay });
        }

        private static string CanonicalHashWithoutScenario(MatchReplayV4 replay)
        {
            var method = typeof(MatchReplayV4).Assembly.GetType(
                "Volleyball.Shared.Contracts.CanonicalMatchReplayJsonV4")
                .GetMethod(
                    "ComputeLegacyWithoutScenarioHash",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return (string)method.Invoke(null, new object[] { replay });
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

        private static MatchReplayEventV4 EventWithAuthority(
            MatchReplayEventV4 baseline,
            ReplayOrganizationAuthorityRecordV4 authority)
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
                baseline.Shadow,
                authority);
        }

        private static MatchReplayEventV4 EventWithPerception(
            MatchReplayEventV4 baseline,
            ReplayPerceptionAuthorityRecordV4 perception)
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
                baseline.Shadow,
                baseline.OrganizationAuthority,
                baseline.AttackDefenseAuthority,
                perception);
        }

        private static MatchReplayEventV4 EventWithWorkBudget(
            MatchReplayEventV4 baseline,
            ReplayWorkBudgetRecordV4 workBudget)
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
                baseline.Shadow,
                baseline.OrganizationAuthority,
                baseline.AttackDefenseAuthority,
                baseline.PerceptionAuthority,
                workBudget);
        }

        private static ReplayPerceptionAuthorityRecordV4
            PerceptionAuthority()
        {
            return new ReplayPerceptionAuthorityRecordV4(
                "gate-j-v1", "gate-j-view-7", "Away", HashA,
                1.1f, .15f, "uncertainty-7", .2f, .8f,
                new[]
                {
                    new ReplayPerceivedThreatRecordV4(
                        "threat-b", "Cross", .6f, .5f),
                    new ReplayPerceivedThreatRecordV4(
                        "threat-a", "Line", .8f, .4f)
                },
                "away-libero", "Cross", false, 7, 9);
        }

        private static ReplayOrganizationAuthorityRecordV4 OrganizationAuthority(
            MatchReplayEventV4 replayEvent,
            string organizer = "home-setter",
            string fallbackReason = "None",
            string setterStatus = "Reachable",
            string executableEnvelopeIdentity = null)
        {
            return new ReplayOrganizationAuthorityRecordV4(
                7,
                3,
                "Receive",
                new ReplayVector3RecordV4(1.5f, 0f, -1.1f),
                null,
                "Best",
                "home-setter",
                setterStatus,
                1.2f,
                0.04f,
                0.3f,
                organizer,
                fallbackReason,
                "Primary",
                replayEvent.TestedEnvelope.Identity,
                executableEnvelopeIdentity ??
                replayEvent.ExecutableEnvelope.Identity,
                replayEvent.Classification.ActualSample.EnvelopeIdentity,
                replayEvent.Trajectory.ArtifactIdentity,
                new ReplayCoverageDecisionRecordV4(
                    "Covered",
                    0f,
                    "WithinConditionalEnvelope",
                    Array.Empty<string>(),
                    0,
                    "Primary"));
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

        private static string ReplaceAllShadowCoverageWithLegacy(string json)
        {
            var replacement = json;
            var shadows = 0;
            var searchOffset = 0;
            while (true)
            {
                var shadowIndex = replacement.IndexOf(
                    "\"shadow\":",
                    searchOffset,
                    StringComparison.Ordinal);
                if (shadowIndex < 0)
                {
                    break;
                }

                shadows++;
                searchOffset = shadowIndex + "\"shadow\":".Length;
            }

            for (var shadowOrdinal = shadows - 1;
                 shadowOrdinal >= 0;
                 shadowOrdinal--)
            {
                replacement = ReplaceShadowCoverageWithLegacy(
                    replacement,
                    shadowOrdinal);
            }

            return replacement;
        }

        private static string ReplaceShadowCoverageWithLegacy(
            string json,
            int shadowOrdinal)
        {
            var searchOffset = 0;
            var shadowIndex = -1;
            for (var index = 0; index <= shadowOrdinal; index++)
            {
                shadowIndex = json.IndexOf("\"shadow\":", searchOffset, StringComparison.Ordinal);
                searchOffset = shadowIndex + 1;
            }

            var coverageStart = json.IndexOf("\"coverage\":", shadowIndex, StringComparison.Ordinal);
            var coverageEnd = json.IndexOf("}", coverageStart, StringComparison.Ordinal);
            return json.Substring(0, coverageStart) +
                "\"coverage\":{\"decision\":\"Covered\",\"score\":0.75" +
                json.Substring(coverageEnd);
        }

        private static string ComputeLegacyShadowCoverageHash(MatchReplayV4 replay)
        {
            var serializer = typeof(ContractJson).Assembly.GetType(
                "Volleyball.Shared.Contracts.CanonicalMatchReplayJsonV4");
            var method = serializer.GetMethod(
                "ComputeLegacyShadowCoverageHash",
                BindingFlags.Static | BindingFlags.Public);
            return (string)method.Invoke(null, new object[] { replay });
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
