using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Volleyball.Shared.Contracts;

namespace Volleyball.Shared.EditModeTests
{
    public sealed class MatchContractTests
    {
        [Test]
        public void ProductionContractSurface_RequiresConcreteV4AndV5Only()
        {
            var assembly = typeof(MatchContextV4).Assembly;
            var legacyNames = new[]
            {
                "PlayerAbilitySnapshotV1", "PlayerAbilitySnapshotV2", "PlayerAbilitySnapshotV3",
                "MatchContextV1", "MatchContextV2", "MatchContextV3",
                "MatchResultV1", "MatchResultV2", "MatchResultV3",
                "MatchReplayV1", "MatchReplayV2", "IMatchContext", "IMatchResult"
            };

            Assert.That(assembly.GetType("Volleyball.Shared.Contracts.MatchContextV4"), Is.Not.Null);
            Assert.That(assembly.GetType("Volleyball.Shared.Contracts.MatchResultV4"), Is.Not.Null);
            Assert.That(assembly.GetType("Volleyball.Shared.Contracts.MatchReplayV4"), Is.Not.Null);
            Assert.That(assembly.GetType("Volleyball.Shared.Contracts.CareerBaseAttributesV5"), Is.Not.Null);
            Assert.That(assembly.GetType("Volleyball.Shared.Contracts.DerivedMatchAttributesV5"), Is.Not.Null);
            Assert.That(assembly.GetType("Volleyball.Shared.Contracts.MatchContextV5"), Is.Not.Null);
            Assert.That(assembly.GetType("Volleyball.Shared.Contracts.MatchResultV5"), Is.Not.Null);
            Assert.That(assembly.GetType("Volleyball.Shared.Contracts.MatchReplayV5"), Is.Not.Null);
            foreach (var legacyName in legacyNames)
            {
                Assert.That(assembly.GetType("Volleyball.Shared.Contracts." + legacyName), Is.Null, legacyName);
            }
        }

        [Test]
        public void CareerBaseAttributesV5_PreservesBoundariesAndRejectsEveryOutOfRangeValue()
        {
            var minimum = CreateV5Bases(0, 1400);
            var maximum = CreateV5Bases(10000, 2300);

            Assert.That(minimum.Strength, Is.Zero);
            Assert.That(minimum.HeightMillimeters, Is.EqualTo(1400));
            Assert.That(maximum.Set, Is.EqualTo(10000));
            Assert.That(maximum.HeightMillimeters, Is.EqualTo(2300));
            Assert.That(() => CreateV5Bases(-1, 1400), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => CreateV5Bases(10001, 1400), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => CreateV5Bases(0, 1399), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => CreateV5Bases(0, 2301), Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchAttributeDerivationV5_IsStableAndEveryBaseChangesItsDeclaredDerivedSurface()
        {
            var baseline = CreateV5Bases(5000, 1850);
            var derived = MatchAttributeDerivationV5.Derive(baseline, DominantHandV5.Right);
            var repeated = MatchAttributeDerivationV5.Derive(baseline, DominantHandV5.Right);

            Assert.That(repeated.ResultFingerprint, Is.EqualTo(derived.ResultFingerprint));
            Assert.That(repeated.InputFingerprint, Is.EqualTo(derived.InputFingerprint));
            Assert.That(derived.Explanations.Select(value => value.OutputField),
                Does.Contain("AttackControl").And.Contain("SetControl"));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5001, 1850, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000),
                DominantHandV5.Right).AttackPower, Is.GreaterThan(derived.AttackPower));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1851, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000),
                DominantHandV5.Right).AttackReachMillimeters, Is.GreaterThan(derived.AttackReachMillimeters));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5001, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000),
                DominantHandV5.Right).Jump, Is.GreaterThan(derived.Jump));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5001, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000),
                DominantHandV5.Right).Movement, Is.GreaterThan(derived.Movement));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5000, 5001, 5000, 5000, 5000, 5000, 5000, 5000, 5000),
                DominantHandV5.Right).Reaction, Is.GreaterThan(derived.Reaction));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5000, 5000, 5001, 5000, 5000, 5000, 5000, 5000, 5000),
                DominantHandV5.Right).AttackControl, Is.GreaterThan(derived.AttackControl));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5000, 5000, 5000, 5001, 5000, 5000, 5000, 5000, 5000),
                DominantHandV5.Right).AttackControl, Is.GreaterThan(derived.AttackControl));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5000, 5000, 5000, 5000, 5001, 5000, 5000, 5000, 5000),
                DominantHandV5.Right).DefenseControl, Is.GreaterThan(derived.DefenseControl));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5000, 5000, 5000, 5000, 5000, 5001, 5000, 5000, 5000),
                DominantHandV5.Right).CourtIq, Is.GreaterThan(derived.CourtIq));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5001, 5000, 5000),
                DominantHandV5.Right).BlockControl, Is.GreaterThan(derived.BlockControl));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5001, 5000),
                DominantHandV5.Right).ServeControl, Is.GreaterThan(derived.ServeControl));
            Assert.That(MatchAttributeDerivationV5.Derive(
                new CareerBaseAttributesV5(5000, 1850, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5000, 5001),
                DominantHandV5.Right).SetControl, Is.GreaterThan(derived.SetControl));
        }

        [Test]
        public void MatchAttributeDerivationV5_RejectsUnknownVersionsAndChangesIdentityForHandedness()
        {
            var bases = CreateV5Bases(5000, 1850);
            var left = MatchAttributeDerivationV5.Derive(bases, DominantHandV5.Left);
            var right = MatchAttributeDerivationV5.Derive(bases, DominantHandV5.Right);

            Assert.That(left.ResultFingerprint, Is.Not.EqualTo(right.ResultFingerprint));
            Assert.That(() => MatchAttributeDerivationV5.Derive(bases, DominantHandV5.Right, 2),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchContextV5_BindsBothOrderedTeamsAndRejectsSuppliedDerivedMismatch()
        {
            var home = CreateV5Team("home", TeamSide.Home, 5000);
            var away = CreateV5Team("away", TeamSide.Away, 6000);
            var context = MatchContextV5.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                99, home, away, new string('a', 64), CreateV5TrajectoryConfiguration());
            var repeated = MatchContextV5.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                99, home, away, new string('a', 64), CreateV5TrajectoryConfiguration());

            Assert.That(context.ContractVersion, Is.EqualTo(ContractVersions.MatchV5));
            Assert.That(context.ContextHash, Is.EqualTo(repeated.ContextHash));
            Assert.That(ContractJson.SerializeV5(context), Is.EqualTo(ContractJson.SerializeV5(repeated)));
            Assert.That(ContractJson.SerializeV5(ContractJson.DeserializeMatchContextV5(
                ContractJson.SerializeV5(context))), Is.EqualTo(ContractJson.SerializeV5(context)));
            Assert.That(context.Home.RotationOrder, Has.Count.EqualTo(6));
            var correct = MatchAttributeDerivationV5.Derive(CreateV5Bases(5000, 1800), DominantHandV5.Right);
            var different = MatchAttributeDerivationV5.Derive(CreateV5Bases(5001, 1800), DominantHandV5.Right);
            Assert.That(() => new PlayerSnapshotV5(new PlayerId("player"), "Player", 1,
                PlayerPosition.Setter, DominantHandV5.Right, CreateV5Bases(5000, 1800), different),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(() => new PlayerSnapshotV5(new PlayerId("player"), "Player", 1,
                PlayerPosition.Setter, DominantHandV5.Right, CreateV5Bases(5000, 1800), correct),
                Throws.Nothing);
        }

        [Test]
        public void MatchResultAndReplayV5_BindToTheirFrozenContext()
        {
            var context = MatchContextV5.Create(Guid.NewGuid(), 99,
                CreateV5Team("home", TeamSide.Home, 5000),
                CreateV5Team("away", TeamSide.Away, 6000), new string('b', 64), CreateV5TrajectoryConfiguration());
            var result = MatchResultV5.Create(context, context.Home.TeamId, 25, 20, 45);
            var replay = MatchReplayV5.Create("formal-v5", context);

            result.ValidateAgainst(context);
            Assert.That(ContractJson.SerializeV5(result), Does.Contain(result.ResultHash));
            Assert.That(ContractJson.SerializeV5(ContractJson.DeserializeMatchResultV5(
                ContractJson.SerializeV5(result), context)), Is.EqualTo(ContractJson.SerializeV5(result)));
            Assert.That(replay.ContextHash, Is.EqualTo(context.ContextHash));
            Assert.That(replay.DerivedAttributeFingerprints, Has.Count.EqualTo(12));
            var evidence = new MatchReplayAttributeEvidenceV5(
                0, context.Home.RotationOrder[0].PlayerId, "Attack", 1234,
                context.Home.RotationOrder[0].Derived.ResultFingerprint);
            var replayWithEvidence = MatchReplayV5.Create("formal-v5-evidence", context,
                new[] { evidence });
            Assert.That(replayWithEvidence.AttributeEvidence, Has.Count.EqualTo(1));
            var reportFact = new MatchReplayReportFactV1(0, context.Home.RotationOrder[0].PlayerId,
                "Contact", "Attack", true, false, 90, attributeEvidenceSequenceNumber: 0);
            var replayWithReportFacts = MatchReplayV5.Create("formal-v5-report-facts", context,
                new[] { evidence }, new[] { reportFact });
            Assert.That(replayWithReportFacts.ReportFacts, Has.Count.EqualTo(1));
            Assert.That(() => MatchReplayV5.Create("formal-v5-duplicate-evidence", context,
                new[] { evidence }, new[]
                {
                    reportFact,
                    new MatchReplayReportFactV1(1, context.Home.RotationOrder[0].PlayerId,
                        "Contact", "Attack", true, false, 90, attributeEvidenceSequenceNumber: 0)
                }), Throws.TypeOf<ContractValidationException>());
            var decisionFact = new MatchReplayReportFactV1(0, context.Home.RotationOrder[0].PlayerId,
                "Decision", "Attack", true, false, 0, executableChoices: 2,
                selectedChoice: "Attack", decisionReason: "HighestExecutableScore");
            Assert.That(decisionFact.ExecutableChoices, Is.EqualTo(2));
            Assert.That(() => new MatchReplayReportFactV1(0, context.Home.RotationOrder[0].PlayerId,
                "Decision", "Attack", true, false, 0, executableChoices: 1,
                selectedChoice: "Attack", decisionReason: "Invalid"),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(() => MatchReplayV5.Create("formal-v5-unproven-result", context,
                new[] { evidence }, new[] { new MatchReplayReportFactV1(0,
                    context.Home.RotationOrder[0].PlayerId, "RallyResult", "Attack", true, false, 0) }),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(() => MatchReplayV5.Create("formal-v5-invalid", context,
                new[] { new MatchReplayAttributeEvidenceV5(0,
                    context.Home.RotationOrder[0].PlayerId, "Attack", 1234,
                    context.Away.RotationOrder[0].Derived.ResultFingerprint) }),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(() => MatchResultV5.Create(context, context.Home.TeamId, 20, 25, 45),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void V5PositionFaultEvidence_IsCanonicalAndRejectsUnknownRulesOrSlotRelations()
        {
            var context = MatchContextV5.Create(Guid.NewGuid(), 99,
                CreateV5Team("home", TeamSide.Home, 5000),
                CreateV5Team("away", TeamSide.Away, 6000), new string('b', 64),
                CreateV5TrajectoryConfiguration());
            var fault = new MatchPositionFaultV5(1, TeamSide.Home, TeamSide.Away,
                TeamSide.Home, "Slot4BehindSlot5", context.Home.RotationOrder[3].PlayerId, 4,
                0, -1000, context.Home.RotationOrder[4].PlayerId, 5, 0, -2000);
            var result = MatchResultV5.Create(context, context.Away.TeamId, 0, 1, 1,
                new[] { fault });
            var baseline = MatchResultV5.Create(context, context.Away.TeamId, 0, 1, 1);
            var replay = MatchReplayV5.Create("faulted-v5", context,
                Array.Empty<MatchReplayAttributeEvidenceV5>(),
                Array.Empty<MatchReplayReportFactV1>(), new[] { fault });

            Assert.That(result.PositionFaults.Single().Rule, Is.EqualTo("Slot4BehindSlot5"));
            Assert.That(result.ResultHash, Is.Not.EqualTo(baseline.ResultHash));
            Assert.That(ContractJson.DeserializeMatchResultV5(ContractJson.SerializeV5(result), context)
                .PositionFaults.Single().ViolatingPlayerId, Is.EqualTo(fault.ViolatingPlayerId));
            Assert.That(replay.PositionFaults.Single().RequiredSlot, Is.EqualTo(4));
            Assert.That(replay.ReplayHash, Is.Not.EqualTo(MatchReplayV5.Create("faulted-v5", context)
                .ReplayHash));
            var replayJson = ContractJson.SerializeV5(replay);
            Assert.That(ContractJson.SerializeV5(ContractJson.DeserializeMatchReplayV5(
                replayJson, context)), Is.EqualTo(replayJson));
            Assert.That(() => new MatchPositionFaultV5(1, TeamSide.Home, TeamSide.Away,
                TeamSide.Home, "UnknownRule", context.Home.RotationOrder[3].PlayerId, 4,
                0, -1000, context.Home.RotationOrder[4].PlayerId, 5, 0, -2000),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(() => new MatchPositionFaultV5(1, TeamSide.Home, TeamSide.Away,
                TeamSide.Home, "Slot4BehindSlot5", context.Home.RotationOrder[3].PlayerId, 3,
                0, -1000, context.Home.RotationOrder[4].PlayerId, 5, 0, -2000),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void CareerMatchReportV1_IsCanonicalAndRejectsInvalidBindingsAndCounters()
        {
            var context = MatchContextV5.Create(Guid.Parse("77777777-7777-7777-7777-777777777777"), 99,
                CreateV5Team("home", TeamSide.Home, 5000), CreateV5Team("away", TeamSide.Away, 6000),
                new string('d', 64), CreateV5TrajectoryConfiguration());
            var result = MatchResultV5.Create(context, context.Home.TeamId, 25, 20, 45);
            var reports = context.Home.RotationOrder.Concat(context.Away.RotationOrder)
                .Select((player, index) => CreateCareerReport(player.PlayerId, index == 0)).ToArray();
            var report = CareerMatchReportV1.Create(context, result, CareerMatchEvidenceKindV1.PhysicalReplay,
                new string('e', 64), reports);

            var json = ContractJson.SerializeV1(report);
            Assert.That(ContractJson.SerializeV1(ContractJson.DeserializeCareerMatchReportV1(json, context, result)), Is.EqualTo(json));
            Assert.That(report.PlayerReports, Has.Count.EqualTo(12));
            Assert.That(() => CareerMatchReportV1.Create(context, result, CareerMatchEvidenceKindV1.PhysicalReplay,
                new string('e', 64), reports.Reverse().ToArray()), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => new CareerMatchPlayerReportV1(reports[0].PlayerId,
                attackAttempts: 1, attackPoints: 1, attackErrors: 1,
                serveAttempts: 0, serveAces: 0, serveErrors: 0,
                receiveAttempts: 0, receivePerfect: 0, receivePositive: 0, receiveNeutral: 0, receiveNegative: 0, receiveErrors: 0,
                defenseAttempts: 0, defenseSuccesses: 0, blockAttempts: 0, blockEffectiveTouches: 0, blockPoints: 0,
                setAttempts: 0, setSuccesses: 0, setErrors: 0, rallies: 0, movementMillimeters: 0, jumps: 0,
                workloadBasisPoints: 1, workloadFormulaVersion: 1, criticalActions: 0, criticalSuccesses: 0,
                criticalErrors: 0, streakEpisodes: 0, longestStreak: 0, decisionQualitySuccesses: 0, decisionQualityErrors: 0),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeCareerMatchReportV1(json.Replace(report.ResultHash, new string('f', 64)), context, result),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void QuickSimulationTraceV1_IsByteStableAndRejectsOutOfContextPlayers()
        {
            var context = MatchContextV5.Create(Guid.Parse("88888888-8888-8888-8888-888888888888"), 77,
                CreateV5Team("home", TeamSide.Home, 5000), CreateV5Team("away", TeamSide.Away, 6000),
                new string('f', 64), CreateV5TrajectoryConfiguration());
            var entry = new QuickSimulationTraceEntryV1(0, context.Home.RotationOrder[0].PlayerId,
                "Attack", "Success", true, 100, 2, "Attack", "Test");
            var first = QuickSimulationTraceV1.Create(context, new[] { entry });
            var repeated = QuickSimulationTraceV1.Create(context, new[] { entry });
            first.ValidateAgainst(context);
            Assert.That(first.TraceHash, Is.EqualTo(repeated.TraceHash));
            Assert.That(ContractJson.SerializeV1(first), Is.EqualTo(ContractJson.SerializeV1(repeated)));
            Assert.That(ContractJson.SerializeV1(ContractJson.DeserializeQuickSimulationTraceV1(
                ContractJson.SerializeV1(first), context)), Is.EqualTo(ContractJson.SerializeV1(first)));
            Assert.That(() => QuickSimulationTraceV1.Create(context, new[] {
                new QuickSimulationTraceEntryV1(0, new PlayerId("not-on-court"), "Attack", "Success", false, 0, 0, null, null) }),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void ContractVersions_ReserveV4MatchAndReplayIndependentlyOfV3Rules()
        {
            Assert.That(RulesVersions.FullRallyV3, Is.EqualTo(3));
            Assert.That(ContractVersions.MatchV4, Is.EqualTo(4));
            Assert.That(ContractVersions.ReplayV4, Is.EqualTo(4));
        }

        [Test]
        public void ReplayOrganizationAuthority_UsesStrictGateHValueSets()
        {
            var hash = new string('a', 64);
            var coverage = new ReplayCoverageDecisionRecordV4(
                "Covered",
                0f,
                "WithinConditionalEnvelope",
                Array.Empty<string>(),
                0,
                "Primary");
            var authority = new ReplayOrganizationAuthorityRecordV4(
                7,
                3,
                "Receive",
                new ReplayVector3RecordV4(1.5f, 0f, -1.1f),
                null,
                "Best",
                "home-setter",
                "Reachable",
                1.2f,
                0.04f,
                0.3f,
                "home-setter",
                "None",
                "Primary",
                hash,
                hash,
                hash,
                hash,
                coverage);

            Assert.That(authority.PlanRevision, Is.EqualTo(7));
            Assert.That(authority.Coverage, Is.SameAs(coverage));
            Assert.That(
                () => new ReplayOrganizationAuthorityRecordV4(
                    7,
                    3,
                    "Unknown",
                    authority.OrganizationTarget,
                    null,
                    authority.ZoneGrade,
                    authority.RegisteredSetterPlayerId,
                    authority.SetterStatus,
                    authority.SetterMovementMeters,
                    authority.SetterReactionDelaySeconds,
                    authority.SetterReachMarginMeters,
                    authority.OrganizerPlayerId,
                    authority.FallbackReason,
                    authority.ActivatedBranch,
                    hash,
                    hash,
                    hash,
                    hash,
                    coverage),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void ReplayAttackDefenseAuthority_RequiresStrictSortedEventOwnedEvidence()
        {
            var hash = new string('b', 64);
            var coverage = new ReplayCoverageDecisionRecordV4(
                "Covered", 0f, "WithinConditionalEnvelope", Array.Empty<string>(), 0, "Primary");
            var candidate = new ReplayAttackDefenseCandidateRecordV4(
                "attack-7-line", "home-attacker", "PowerLine",
                new ReplayVector3RecordV4(1f, 3f, -1f),
                0.7f, 0.9f, true, string.Empty, hash, hash, string.Empty);
            var fallback = new ReplayAttackDefenseCandidateRecordV4(
                "attack-7-tip", "home-attacker", "Tip",
                new ReplayVector3RecordV4(1f, 2.8f, -1f),
                0.5f, 1f, false, "NotPower", hash, hash, string.Empty);
            var authority = new ReplayAttackDefenseAuthorityRecordV4(
                7, 19, "AttackCommitted", "Primary",
                new ReplayVector3RecordV4(1f, 3f, -1f),
                new[] { fallback, candidate },
                new[] { new ReplayPublicAttackThreatRecordV4("PowerLine", "Line", 1f, 1.2f) },
                new[] { new ReplayDefenseResponsibilityRecordV4(
                    "away-blocker", "PrimaryBlock", "Line", "Primary") },
                "attack-7-line", hash, hash, hash, hash, null, coverage);

            Assert.That(authority.SelectedCandidateIdentity, Is.EqualTo("attack-7-line"));
            Assert.That(authority.Candidates.First().ActorPlayerId, Is.EqualTo("home-attacker"));
            Assert.That(authority.Candidates.Select(value => value.CandidateIdentity),
                Is.Ordered.Ascending);
            Assert.That(
                () => new ReplayAttackDefenseAuthorityRecordV4(
                    7, 19, "AttackCommitted", "Primary",
                    authority.SetTarget, new[] { candidate, candidate }, authority.PublicThreat,
                    authority.DefenseResponsibilities, "attack-7-line", hash, hash, hash,
                    hash, null, coverage),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void ReplayAttackDefenseAuthority_SetCoexistsWithGateHAndRoundTripsCanonically()
        {
            var context = CreateContextV4(Guid.NewGuid(), 7351);
            var baseline = CreateGateISetEvent(context);
            var authority = CreateSetIntentAuthority(baseline, 7);
            var organization = CreateSetOrganizationAuthority(baseline);
            var replay = MatchReplayV4.Create("gate-i-set-replay", context,
                new[] { WithGateIAuthority(baseline, organization, authority) });
            var json = ContractJson.SerializeV4(replay);
            var restored = ContractJson.DeserializeMatchReplayV4(json);

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(json));
            Assert.That(restored.Events[0].OrganizationAuthority, Is.Not.Null);
            Assert.That(restored.Events[0].AttackDefenseAuthority, Is.Not.Null);
            Assert.That(restored.Events[0].OrganizationAuthority.OrganizerPlayerId,
                Is.EqualTo(restored.Events[0].ActorPlayerId));
            Assert.That(restored.Events[0].AttackDefenseAuthority.TestedEnvelopeIdentity,
                Is.EqualTo(restored.Events[0].TestedEnvelope.Identity));
            Assert.That(restored.Events[0].AttackDefenseAuthority.TrajectoryArtifactIdentity,
                Is.EqualTo(restored.Events[0].Trajectory.ArtifactIdentity));

            var changed = MatchReplayV4.Create("gate-i-set-replay", context,
                new[] { WithGateIAuthority(baseline, organization,
                    CreateSetIntentAuthority(baseline, 8)) });
            Assert.That(changed.ReplayHash, Is.Not.EqualTo(replay.ReplayHash));
        }

        [Test]
        public void ReplayAttackDefenseAuthority_PreservesHistoricalJsonAndRejectsEventIdentityMismatch()
        {
            var context = CreateContextV4(Guid.NewGuid(), 7351);
            var baseline = CreateGateISetEvent(context);
            var historical = ContractJson.SerializeV4(
                MatchReplayV4.Create("historical-gate-i-absent", context,
                    new[] { baseline }));
            var restored = ContractJson.DeserializeMatchReplayV4(historical);

            Assert.That(restored.Events[0].AttackDefenseAuthority, Is.Null);
            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(historical));

            var mismatched = new ReplayAttackDefenseAuthorityRecordV4(
                7, 19, "SetIntentPlanned", "Primary",
                new ReplayVector3RecordV4(1f, 3f, -1f),
                Array.Empty<ReplayAttackDefenseCandidateRecordV4>(),
                Array.Empty<ReplayPublicAttackThreatRecordV4>(),
                Array.Empty<ReplayDefenseResponsibilityRecordV4>(), string.Empty,
                PredictorConfigurationHashV4, baseline.ExecutableEnvelope.Identity,
                baseline.Classification.ActualSample.EnvelopeIdentity,
                baseline.Trajectory.ArtifactIdentity, null,
                new ReplayCoverageDecisionRecordV4("Covered", 0f));
            Assert.That(
                () => WithGateIAuthority(baseline,
                    CreateSetOrganizationAuthority(baseline), mismatched),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void ReplayPublicAttackThreat_HasNoFinalRouteOrSampleSurface()
        {
            var names = typeof(ReplayPublicAttackThreatRecordV4).GetProperties()
                .Select(property => property.Name).ToArray();

            Assert.That(names, Has.None.Matches<string>(name =>
                name.IndexOf("route", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("sample", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("candidate", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void ReplayAttackDefenseAuthority_DefenseEvidenceKeepsPublicPlanWithoutFinalCandidateBinding()
        {
            var hash = new string('c', 64);
            var candidate = new ReplayAttackDefenseCandidateRecordV4(
                "attack-8-cross", "home-attacker", "PowerCross",
                new ReplayVector3RecordV4(2f, 3f, -1f), .6f, .8f, true,
                string.Empty, hash, hash, string.Empty);
            var authority = new ReplayAttackDefenseAuthorityRecordV4(
                8, 20, "DefenseCommitted", "Primary",
                new ReplayVector3RecordV4(1f, 3f, -1f), new[] { candidate },
                new[] { new ReplayPublicAttackThreatRecordV4("PowerCross", "Cross", 1f, 1.1f) },
                new[] { new ReplayDefenseResponsibilityRecordV4(
                    "away-defender", "CrossDefense", "Cross", "Primary") },
                string.Empty, hash, hash, hash, hash, null,
                new ReplayCoverageDecisionRecordV4("Covered", 0f));

            Assert.That(authority.SelectedCandidateIdentity, Is.Empty);
            Assert.That(authority.Candidates, Has.Count.EqualTo(1));
            Assert.That(authority.PublicThreat, Has.Count.EqualTo(1));
            Assert.That(authority.DefenseResponsibilities.Single().ActorPlayerId,
                Is.EqualTo("away-defender"));
        }

        [Test]
        public void ReplayAttackDefenseAuthority_AwaitingDefenseReceiveRetainsSelectedPlanCandidate()
        {
            var replayEvent = CreateGateIDefenseEvent("away-defender");
            var authority = CreateObservedDefenseAuthority(replayEvent, "away-defender");

            var recorded = WithGateIAuthority(replayEvent, null, authority);

            Assert.That(recorded.AttackDefenseAuthority.SelectedCandidateIdentity,
                Is.EqualTo("attack-selected"));
            Assert.That(recorded.AttackDefenseAuthority.DefenseResponsibilities
                .Single().ActorPlayerId, Is.EqualTo(recorded.ActorPlayerId));
        }

        [Test]
        public void ReplayAttackDefenseAuthority_AttackCoverRetainsExplicitAttackingSideResponsibility()
        {
            var replayEvent = CreateGateIDefenseEvent(
                "blue-player-2",
                sequenceNumber: 0);
            var authority = CreateObservedDefenseAuthority(
                replayEvent,
                "orange-player-2",
                attackCoveragePlayerId: "blue-player-2");
            var recorded = WithGateIAuthority(
                replayEvent,
                null,
                authority);

            Assert.That(
                recorded.AttackDefenseAuthority
                    .AttackCoverageResponsibilities.Single()
                    .ActorPlayerId,
                Is.EqualTo("blue-player-2"));
        }

        [Test]
        public void ReplayAttackDefenseAuthority_AwaitingDefenseRejectsUnknownActor()
        {
            var replayEvent = CreateGateIDefenseEvent("away-unknown");

            Assert.That(() => WithGateIAuthority(replayEvent, null,
                CreateObservedDefenseAuthority(replayEvent, "away-defender")),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void ReplayAttackDefenseAuthority_DefenseCommittedAllowsUnselectedCommandEvidence()
        {
            var replayEvent = CreateGateIDefenseEvent("away-defender");
            var authority = CreateObservedDefenseAuthority(replayEvent,
                "away-defender", "DefenseCommitted", string.Empty);

            Assert.That(WithGateIAuthority(replayEvent, null, authority)
                .AttackDefenseAuthority.SelectedCandidateIdentity, Is.Empty);
        }

        [Test]
        public void ReplayEventCategoryCompatibility_AllowsOnlyGateISoftAndDefensePairs()
        {
            var hash = new string('a', 64);
            Assert.That(
                new ReplayActualSampleRecordV4(
                    hash,
                    "gate-i-sample",
                    "SoftAction",
                    new ReplayVector3RecordV4(1f, 2f, 3f),
                    new ReplayVector3RecordV4(1f, 1f, 1f),
                    .1f).CandidateCategory,
                Is.EqualTo("SoftAction"));
            Assert.That(
                new ReplayActualSampleRecordV4(
                    hash,
                    "gate-i-defense-sample",
                    "Defense",
                    new ReplayVector3RecordV4(1f, 2f, 3f),
                    new ReplayVector3RecordV4(1f, 1f, 1f),
                    .1f).CandidateCategory,
                Is.EqualTo("Defense"));
            Assert.That(
                MatchReplayEventV4.IsCandidateCategoryCompatibleWithEventKind(
                    "Attack", "SoftAction"),
                Is.True);
            Assert.That(
                MatchReplayEventV4.IsCandidateCategoryCompatibleWithEventKind(
                    "Receive", "Defense"),
                Is.True);
            Assert.That(
                MatchReplayEventV4.IsCandidateCategoryCompatibleWithEventKind(
                    "Serve", "Defense"),
                Is.False);
            Assert.That(
                MatchReplayEventV4.IsCandidateCategoryCompatibleWithEventKind(
                    "Set", "SoftAction"),
                Is.False);
        }

        [Test]
        public void DominantHandV4_DeclaresExactlyBothValidHands()
        {
            Assert.That(DominantHandV4.Left, Is.EqualTo((DominantHandV4)0));
            Assert.That(DominantHandV4.Right, Is.EqualTo((DominantHandV4)1));
            Assert.That(Enum.IsDefined(typeof(DominantHandV4), DominantHandV4.Left), Is.True);
            Assert.That(Enum.IsDefined(typeof(DominantHandV4), DominantHandV4.Right), Is.True);
            Assert.That(Enum.IsDefined(typeof(DominantHandV4), (DominantHandV4)2), Is.False);
        }

        [Test]
        public void PhysicalBaseAttributesV4_PreservesEveryValidBoundaryAndUsesValueEquality()
        {
            var minimum = new PhysicalBaseAttributesV4(1.40f, 1.70f, 0f, 0f, 0f, 0f);
            var maximum = new PhysicalBaseAttributesV4(2.30f, 3.10f, 1f, 1f, 1f, 1f);
            var sameAsMinimum = new PhysicalBaseAttributesV4(1.40f, 1.70f, 0f, 0f, 0f, 0f);

            Assert.That(minimum.HeightMeters, Is.EqualTo(1.40f));
            Assert.That(minimum.StandingReachMeters, Is.EqualTo(1.70f));
            Assert.That(minimum.Jump, Is.Zero);
            Assert.That(minimum.Mobility, Is.Zero);
            Assert.That(minimum.Reaction, Is.Zero);
            Assert.That(minimum.Coordination, Is.Zero);
            Assert.That(maximum.HeightMeters, Is.EqualTo(2.30f));
            Assert.That(maximum.StandingReachMeters, Is.EqualTo(3.10f));
            Assert.That(maximum.Jump, Is.EqualTo(1f));
            Assert.That(maximum.Mobility, Is.EqualTo(1f));
            Assert.That(maximum.Reaction, Is.EqualTo(1f));
            Assert.That(maximum.Coordination, Is.EqualTo(1f));
            Assert.That(sameAsMinimum, Is.EqualTo(minimum));
            Assert.That(sameAsMinimum.GetHashCode(), Is.EqualTo(minimum.GetHashCode()));

            var baseline = CreatePhysicalBaseAttributes(0f, -1);
            for (var field = 0; field < 6; field++)
            {
                Assert.That(CreatePhysicalBaseAttributes(field < 2 ? baseline.HeightMeters + 0.01f : 0.51f, field),
                    Is.Not.EqualTo(baseline), "Physical equality must include field " + field + ".");
            }
        }

        [Test]
        public void PhysicalBaseAttributesV4_RejectsNonFiniteAndOutOfRangeValuesForEveryField()
        {
            var minimums = new[] { 1.40f, 1.70f, 0f, 0f, 0f, 0f };
            var maximums = new[] { 2.30f, 3.10f, 1f, 1f, 1f, 1f };

            for (var field = 0; field < minimums.Length; field++)
            {
                AssertPhysicalBaseAttributeRejected(field, float.NaN);
                AssertPhysicalBaseAttributeRejected(field, float.PositiveInfinity);
                AssertPhysicalBaseAttributeRejected(field, float.NegativeInfinity);
                AssertPhysicalBaseAttributeRejected(field, minimums[field] - 0.001f);
                AssertPhysicalBaseAttributeRejected(field, maximums[field] + 0.001f);
            }
        }

        [Test]
        public void PhysicalBaseAttributesV4_RejectsStandingReachBelowHeightWithoutCorrectingIt()
        {
            Assert.That(
                () => new PhysicalBaseAttributesV4(2.0f, 1.99f, 0.5f, 0.5f, 0.5f, 0.5f),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void TechnicalBaseAttributesV4_PreservesEveryValidBoundaryAndUsesValueEquality()
        {
            var minimum = CreateTechnicalBaseAttributes(0f, -1);
            var maximum = CreateTechnicalBaseAttributes(1f, -1);
            var sameAsMinimum = CreateTechnicalBaseAttributes(0f, -1);

            Assert.That(minimum.AttackTechnique, Is.Zero);
            Assert.That(minimum.AttackPower, Is.Zero);
            Assert.That(minimum.BlockTechnique, Is.Zero);
            Assert.That(minimum.DefenseTechnique, Is.Zero);
            Assert.That(minimum.ReceiveTechnique, Is.Zero);
            Assert.That(minimum.SetTechnique, Is.Zero);
            Assert.That(minimum.ServeTechnique, Is.Zero);
            Assert.That(minimum.SoftTouch, Is.Zero);
            Assert.That(minimum.CourtAwareness, Is.Zero);
            Assert.That(maximum.AttackTechnique, Is.EqualTo(1f));
            Assert.That(maximum.AttackPower, Is.EqualTo(1f));
            Assert.That(maximum.BlockTechnique, Is.EqualTo(1f));
            Assert.That(maximum.DefenseTechnique, Is.EqualTo(1f));
            Assert.That(maximum.ReceiveTechnique, Is.EqualTo(1f));
            Assert.That(maximum.SetTechnique, Is.EqualTo(1f));
            Assert.That(maximum.ServeTechnique, Is.EqualTo(1f));
            Assert.That(maximum.SoftTouch, Is.EqualTo(1f));
            Assert.That(maximum.CourtAwareness, Is.EqualTo(1f));
            Assert.That(sameAsMinimum, Is.EqualTo(minimum));
            Assert.That(sameAsMinimum.GetHashCode(), Is.EqualTo(minimum.GetHashCode()));

            var baseline = CreateTechnicalBaseAttributes(0.5f, -1);
            for (var field = 0; field < 9; field++)
            {
                Assert.That(CreateTechnicalBaseAttributes(0.51f, field),
                    Is.Not.EqualTo(baseline), "Technical equality must include field " + field + ".");
            }
        }

        [Test]
        public void TechnicalBaseAttributesV4_RejectsNonFiniteAndOutOfRangeValuesForEveryField()
        {
            for (var field = 0; field < 9; field++)
            {
                AssertTechnicalBaseAttributeRejected(field, float.NaN);
                AssertTechnicalBaseAttributeRejected(field, float.PositiveInfinity);
                AssertTechnicalBaseAttributeRejected(field, float.NegativeInfinity);
                AssertTechnicalBaseAttributeRejected(field, -0.001f);
                AssertTechnicalBaseAttributeRejected(field, 1.001f);
            }
        }

        [Test]
        public void MatchAttributeDerivationV4_ProducesByteIdenticalCanonicalResultsForIdenticalInputs()
        {
            var physical = CreateDerivationPhysical();
            var technical = CreateDerivationTechnical();

            var first = MatchAttributeDerivationV4.Derive(
                physical,
                technical,
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
            var second = MatchAttributeDerivationV4.Derive(
                physical,
                technical,
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);

            CollectionAssert.AreEqual(first.ToCanonicalBytes(), second.ToCanonicalBytes());
            Assert.That(second.Attributes, Is.EqualTo(first.Attributes));
            Assert.That(second.InputFingerprint, Is.EqualTo(first.InputFingerprint));
            Assert.That(second.ResultFingerprint, Is.EqualTo(first.ResultFingerprint));
            Assert.That(second.InputFingerprint, Has.Length.EqualTo(64));
            Assert.That(second.ResultFingerprint, Has.Length.EqualTo(64));
        }

        [Test]
        public void MatchAttributeDerivationV4_UsesFrozenVersionOneFormulasAndExplanationOrder()
        {
            var derived = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);

            Assert.That(derived.FormulaVersion, Is.EqualTo(1));
            Assert.That(derived.CoefficientVersion, Is.EqualTo(1));
            Assert.That(derived.Attributes.Attack.DirectionControl, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Attack.SpeedControl, Is.EqualTo(0.6425f).Within(0.000001f));
            Assert.That(derived.Attributes.Attack.PowerCapacity, Is.EqualTo(0.65f).Within(0.000001f));
            Assert.That(derived.Attributes.Attack.ContactHeightMeters, Is.EqualTo(2.96f).Within(0.000001f));
            Assert.That(derived.Attributes.Attack.ApproachMobility, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Block.Timing, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Block.HandControl, Is.EqualTo(0.62f).Within(0.000001f));
            Assert.That(derived.Attributes.Block.ReachHeightMeters, Is.EqualTo(2.8925f).Within(0.000001f));
            Assert.That(derived.Attributes.Block.LateralMobility, Is.EqualTo(0.70f).Within(0.000001f));
            Assert.That(derived.Attributes.Defense.Reaction, Is.EqualTo(0.67f).Within(0.000001f));
            Assert.That(derived.Attributes.Defense.PlatformControl, Is.EqualTo(0.5875f).Within(0.000001f));
            Assert.That(derived.Attributes.Defense.CoverageMobility, Is.EqualTo(0.69f).Within(0.000001f));
            Assert.That(derived.Attributes.Defense.Awareness, Is.EqualTo(0.6f));
            Assert.That(derived.Attributes.Receive.FirstTouchControl, Is.EqualTo(0.6225f).Within(0.000001f));
            Assert.That(derived.Attributes.Receive.Reaction, Is.EqualTo(0.67f).Within(0.000001f));
            Assert.That(derived.Attributes.Receive.Movement, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Receive.Awareness, Is.EqualTo(0.6f));
            Assert.That(derived.Attributes.Set.PlacementControl, Is.EqualTo(0.6425f).Within(0.000001f));
            Assert.That(derived.Attributes.Set.TempoControl, Is.EqualTo(0.68f).Within(0.000001f));
            Assert.That(derived.Attributes.Set.SoftTouch, Is.EqualTo(0.6175f).Within(0.000001f));
            Assert.That(derived.Attributes.Set.Movement, Is.EqualTo(0.655f).Within(0.000001f));
            Assert.That(derived.Attributes.Set.Awareness, Is.EqualTo(0.6f));
            Assert.That(derived.Attributes.Serve.DirectionControl, Is.EqualTo(0.6225f).Within(0.000001f));
            Assert.That(derived.Attributes.Serve.SpeedControl, Is.EqualTo(0.615f).Within(0.000001f));
            Assert.That(derived.Attributes.Serve.PowerCapacity, Is.EqualTo(0.695f).Within(0.000001f));
            Assert.That(derived.Attributes.Serve.Consistency, Is.EqualTo(0.6325f).Within(0.000001f));
            Assert.That(
                derived.Explanations.Select(explanation => explanation.OutputName),
                Is.EqualTo(new[]
                {
                    "Attack.DirectionControl",
                    "Attack.SpeedControl",
                    "Attack.PowerCapacity",
                    "Attack.ContactHeightMeters",
                    "Attack.ApproachMobility",
                    "Block.Timing",
                    "Block.HandControl",
                    "Block.ReachHeightMeters",
                    "Block.LateralMobility",
                    "Defense.Reaction",
                    "Defense.PlatformControl",
                    "Defense.CoverageMobility",
                    "Defense.Awareness",
                    "Receive.FirstTouchControl",
                    "Receive.Reaction",
                    "Receive.Movement",
                    "Receive.Awareness",
                    "Set.PlacementControl",
                    "Set.TempoControl",
                    "Set.SoftTouch",
                    "Set.Movement",
                    "Set.Awareness",
                    "Serve.DirectionControl",
                    "Serve.SpeedControl",
                    "Serve.PowerCapacity",
                    "Serve.Consistency"
                }));
            Assert.That(derived.Explanations[0].InputNames, Is.EqualTo(new[]
            {
                "AttackTechnique", "Coordination", "CourtAwareness"
            }));
            Assert.That(derived.Explanations[0].Coefficients, Is.EqualTo(new[] { 0.65f, 0.20f, 0.15f }));
            Assert.That(derived.Explanations[0].Result, Is.EqualTo(derived.Attributes.Attack.DirectionControl));
        }

        [Test]
        public void MatchAttributeDerivationV4_EveryFormulaInputChangesItsDocumentedOutput()
        {
            var baseline = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical());

            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(standingReachMeters: 2.51f), CreateDerivationTechnical()),
                attributes => attributes.Attack.ContactHeightMeters);
            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(jump: 0.61f), CreateDerivationTechnical()),
                attributes => attributes.Attack.PowerCapacity);
            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(mobility: 0.61f), CreateDerivationTechnical()),
                attributes => attributes.Attack.ApproachMobility);
            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(reaction: 0.71f), CreateDerivationTechnical()),
                attributes => attributes.Defense.Reaction);
            AssertDerivedOutputChanges(
                baseline,
                DeriveV4(CreateDerivationPhysical(coordination: 0.56f), CreateDerivationTechnical()),
                attributes => attributes.Attack.DirectionControl);

            for (var field = 0; field < 9; field++)
            {
                var changed = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical(0.01f, field));
                Assert.That(
                    changed.Attributes,
                    Is.Not.EqualTo(baseline.Attributes),
                    "Technical base field " + field + " must affect at least one derived output.");
            }

            var changedHeight = DeriveV4(
                CreateDerivationPhysical(heightMeters: 1.91f),
                CreateDerivationTechnical());
            Assert.That(changedHeight.Attributes, Is.EqualTo(baseline.Attributes));
            Assert.That(changedHeight.InputFingerprint, Is.Not.EqualTo(baseline.InputFingerprint));
        }

        [Test]
        public void MatchAttributeDerivationV4_SeparatesAttackTechniqueFromPowerAuthority()
        {
            var baseline = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical());
            var greaterPower = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 1));
            var greaterTechnique = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 0));

            Assert.That(greaterPower.Attributes.Attack.PowerCapacity,
                Is.GreaterThan(baseline.Attributes.Attack.PowerCapacity));
            Assert.That(greaterPower.Attributes.Serve.PowerCapacity,
                Is.GreaterThan(baseline.Attributes.Serve.PowerCapacity));
            Assert.That(greaterPower.Attributes.Attack.DirectionControl,
                Is.EqualTo(baseline.Attributes.Attack.DirectionControl));
            Assert.That(greaterTechnique.Attributes.Attack.DirectionControl,
                Is.GreaterThan(baseline.Attributes.Attack.DirectionControl));
            Assert.That(greaterTechnique.Attributes.Attack.SpeedControl,
                Is.GreaterThan(baseline.Attributes.Attack.SpeedControl));
            Assert.That(greaterTechnique.Attributes.Attack.PowerCapacity,
                Is.EqualTo(baseline.Attributes.Attack.PowerCapacity));
        }

        [Test]
        public void MatchAttributeDerivationV4_PreservesSpecialistV4AuthoritiesInDeclaredGroups()
        {
            var baseline = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical());
            var softTouch = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 7));
            var blockTechnique = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 2));
            var awareness = DeriveV4(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(0.01f, technicalField: 8));

            Assert.That(softTouch.Attributes.Set.SoftTouch, Is.GreaterThan(baseline.Attributes.Set.SoftTouch));
            Assert.That(softTouch.Attributes.Receive.FirstTouchControl,
                Is.GreaterThan(baseline.Attributes.Receive.FirstTouchControl));
            Assert.That(blockTechnique.Attributes.Block.Timing, Is.GreaterThan(baseline.Attributes.Block.Timing));
            Assert.That(blockTechnique.Attributes.Block.HandControl,
                Is.GreaterThan(baseline.Attributes.Block.HandControl));
            Assert.That(awareness.Attributes.Defense.Awareness, Is.GreaterThan(baseline.Attributes.Defense.Awareness));
            Assert.That(awareness.Attributes.Receive.Awareness, Is.GreaterThan(baseline.Attributes.Receive.Awareness));
            Assert.That(awareness.Attributes.Set.Awareness, Is.GreaterThan(baseline.Attributes.Set.Awareness));
        }

        [Test]
        public void MatchAttributeDerivationV4_HandednessChangesInputAndResultIdentity()
        {
            var left = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Left,
                MatchAttributeDerivationConfigV4.Version1);
            var right = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);

            Assert.That(left.Attributes.DominantHand, Is.EqualTo(DominantHandV4.Left));
            Assert.That(right.Attributes.DominantHand, Is.EqualTo(DominantHandV4.Right));
            Assert.That(right.InputFingerprint, Is.Not.EqualTo(left.InputFingerprint));
            Assert.That(right.ResultFingerprint, Is.Not.EqualTo(left.ResultFingerprint));
        }

        [Test]
        public void MatchAttributeDerivationV4_VersionsChangeResultIdentityWithoutChangingNumericOutputs()
        {
            var baseline = DeriveV4(CreateDerivationPhysical(), CreateDerivationTechnical());
            var formulaVersion = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Right,
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 2,
                    coefficientVersion: 1,
                    MatchAttributeDerivationConfigV4.Version1.Coefficients));
            var coefficientVersion = MatchAttributeDerivationV4.Derive(
                CreateDerivationPhysical(),
                CreateDerivationTechnical(),
                DominantHandV4.Right,
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 1,
                    coefficientVersion: 2,
                    MatchAttributeDerivationConfigV4.Version1.Coefficients));

            Assert.That(formulaVersion.Attributes, Is.EqualTo(baseline.Attributes));
            Assert.That(coefficientVersion.Attributes, Is.EqualTo(baseline.Attributes));
            Assert.That(formulaVersion.InputFingerprint, Is.EqualTo(baseline.InputFingerprint));
            Assert.That(coefficientVersion.InputFingerprint, Is.EqualTo(baseline.InputFingerprint));
            Assert.That(formulaVersion.ResultFingerprint, Is.Not.EqualTo(baseline.ResultFingerprint));
            Assert.That(coefficientVersion.ResultFingerprint, Is.Not.EqualTo(baseline.ResultFingerprint));
        }

        [Test]
        public void MatchAttributeDerivationConfigV4_RejectsIncompleteDuplicateNonFiniteAndNonUnitWeights()
        {
            var coefficients = MatchAttributeDerivationConfigV4.Version1.Coefficients;
            Assert.That(
                () => new MatchAttributeDerivationConfigV4(1, 1, coefficients.Skip(1)),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("missing"));
            Assert.That(
                () => new MatchAttributeDerivationConfigV4(1, 1, coefficients.Concat(new[] { coefficients[0] })),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("duplicate"));
            Assert.That(
                () => new MatchAttributeDerivationConfigV4(
                    1,
                    1,
                    ReplaceCoefficient(coefficients, 0, float.NaN)),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("finite"));
            Assert.That(
                () => new MatchAttributeDerivationConfigV4(
                    1,
                    1,
                    ReplaceCoefficient(coefficients, 0, 0.64f)),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("sum to 1"));
        }

        [Test]
        public void MatchAttributeDerivationV4_RejectsInvalidOutputsWithoutClamping()
        {
            var coefficients = MatchAttributeDerivationConfigV4.Version1.Coefficients;
            var invalid = ReplaceCoefficient(coefficients, 0, -2f).ToArray();
            invalid = ReplaceCoefficient(invalid, 1, 3f).ToArray();
            invalid = ReplaceCoefficient(invalid, 2, 0f).ToArray();
            var config = new MatchAttributeDerivationConfigV4(1, 2, invalid);
            var technical = new TechnicalBaseAttributesV4(
                1f, 0.75f, 0.65f, 0.6f, 0.65f, 0.7f, 0.65f, 0.6f, 0.6f);

            Assert.That(
                () => MatchAttributeDerivationV4.Derive(
                    CreateDerivationPhysical(),
                    technical,
                    DominantHandV4.Right,
                    config),
                Throws.TypeOf<ContractValidationException>()
                    .With.Message.Contains("directionControl"));
        }

        [Test]
        public void MatchContextV4_CanonicalJsonIsByteStableAndPreservesExplicitRotationOrder()
        {
            var context = CreateContextV4(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351);
            var expected = ContractJson.SerializeV4(context);

            for (var repetition = 0; repetition < 100; repetition++)
            {
                Assert.That(ContractJson.SerializeV4(context), Is.EqualTo(expected));
            }

            var restored = ContractJson.DeserializeMatchContextV4(expected);

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(expected));
            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(restored.RulesVersion, Is.EqualTo(RulesVersions.FullRallyV3));
            Assert.That(restored.FormulaVersion, Is.EqualTo(1));
            Assert.That(restored.CoefficientVersion, Is.EqualTo(1));
            Assert.That(restored.PhysicsConfigurationHash, Is.EqualTo(PhysicsConfigurationHashV4));
            Assert.That(
                restored.TrajectoryPredictionProviderConfiguration.CacheCapacity,
                Is.EqualTo(128));
            Assert.That(
                restored.TrajectoryPredictionProviderConfiguration.CacheEvictionPolicy,
                Is.EqualTo(TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut));
            Assert.That(
                restored.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                Is.EqualTo(4));
            Assert.That(
                restored.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                Is.EqualTo(PredictorConfigurationHashV4));
            Assert.That(
                restored.Home.RotationOrder.Select(player => player.PlayerId.Value),
                Is.EqualTo(new[]
                {
                    "blue-player-1",
                    "blue-player-2",
                    "blue-player-3",
                    "blue-player-4",
                    "blue-player-5",
                    "blue-player-6"
                }));
            Assert.That(restored.ContextHash, Is.EqualTo(context.ContextHash));
        }

        [Test]
        public void TrajectoryPredictionProviderConfigurationV4_RequiresBoundedFifoAndVersionedHash()
        {
            var configuration = CreatePredictionProviderConfigurationV4();

            Assert.That(configuration.CacheCapacity, Is.EqualTo(128));
            Assert.That(
                configuration.CacheEvictionPolicy,
                Is.EqualTo(TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut));
            Assert.That(configuration.PredictorVersion, Is.EqualTo(4));
            Assert.That(
                configuration.PredictorConfigurationHash,
                Is.EqualTo(PredictorConfigurationHashV4));
            Assert.That(
                () => new TrajectoryPredictionProviderConfigurationV4(
                    0,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    PredictorConfigurationHashV4),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    (TrajectoryPredictionCacheEvictionPolicyV4)999,
                    4,
                    PredictorConfigurationHashV4),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    0,
                    PredictorConfigurationHashV4),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    "not-a-hash"),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void NativeV4ContextHashIncludesEveryPredictionProviderConfigurationField()
        {
            var sessionId = new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4");
            var baseline = CreateContextV4(sessionId, 7351);
            var changedCapacity = CreateContextV4(
                sessionId,
                7351,
                predictionConfiguration: new TrajectoryPredictionProviderConfigurationV4(
                    64,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    PredictorConfigurationHashV4));
            var changedVersion = CreateContextV4(
                sessionId,
                7351,
                predictionConfiguration: new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    5,
                    PredictorConfigurationHashV4));
            var changedHash = CreateContextV4(
                sessionId,
                7351,
                predictionConfiguration: new TrajectoryPredictionProviderConfigurationV4(
                    128,
                    TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                    4,
                    AlternatePredictorConfigurationHashV4));

            Assert.That(changedCapacity.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(changedVersion.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(changedHash.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
        }

        [Test]
        public void NativeV4FingerprintsCoverBaseHandSeedAndRulesIdentity()
        {
            var baseline = CreateContextV4(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351);
            var changedBase = CreateContextV4(
                baseline.SessionId,
                baseline.Seed,
                firstPhysical: new PhysicalBaseAttributesV4(1.90f, 2.50f, 0.36f, 0.70f, 0.70f, 0.55f));
            var changedHand = CreateContextV4(
                baseline.SessionId,
                baseline.Seed,
                firstHand: DominantHandV4.Left);
            var changedSeed = CreateContextV4(baseline.SessionId, 7352);

            Assert.That(
                changedBase.Home.RotationOrder[0].Derived.InputFingerprint,
                Is.Not.EqualTo(baseline.Home.RotationOrder[0].Derived.InputFingerprint));
            Assert.That(
                changedBase.Home.RotationOrder[0].Derived.ResultFingerprint,
                Is.Not.EqualTo(baseline.Home.RotationOrder[0].Derived.ResultFingerprint));
            Assert.That(
                changedHand.Home.RotationOrder[0].Derived.ResultFingerprint,
                Is.Not.EqualTo(baseline.Home.RotationOrder[0].Derived.ResultFingerprint));
            Assert.That(changedSeed.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(changedBase.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(changedHand.ContextHash, Is.Not.EqualTo(baseline.ContextHash));
            Assert.That(
                () => MatchContextV4.Create(
                    baseline.SessionId,
                    baseline.Seed,
                    baseline.Home,
                    baseline.Away,
                    baseline.PhysicsConfigurationHash,
                    baseline.TrajectoryPredictionProviderConfiguration,
                    rulesVersion: 4),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("rulesVersion"));
        }

        [Test]
        public void PlayerSnapshotV4_RejectsEveryNonAuthoritativeDerivationConfigAtBoundary()
        {
            var published = MatchAttributeDerivationConfigV4.Version1;
            var reweighted = ReplaceCoefficient(
                published.Coefficients,
                0,
                0.64f).ToArray();
            reweighted = ReplaceCoefficient(reweighted, 1, 0.21f).ToArray();
            var unsupported = new[]
            {
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 2,
                    coefficientVersion: 1,
                    published.Coefficients),
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 1,
                    coefficientVersion: 2,
                    published.Coefficients),
                new MatchAttributeDerivationConfigV4(
                    formulaVersion: 1,
                    coefficientVersion: 1,
                    reweighted)
            };

            foreach (var config in unsupported)
            {
                Assert.That(
                    () => new PlayerSnapshotV4(
                        new PlayerId("non-authoritative"),
                        "Non Authoritative",
                        1,
                        PlayerPosition.Setter,
                        DominantHandV4.Right,
                        CreateDerivationPhysical(),
                        CreateDerivationTechnical(),
                        config),
                    Throws.TypeOf<ContractValidationException>()
                        .With.Message.Contains("published V1"));
            }
        }

        [Test]
        public void PlayerSnapshotV4_PublishedV1PreservesDerivedFingerprintThroughContextRoundTrip()
        {
            var context = CreateContextV4(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351,
                config: MatchAttributeDerivationConfigV4.Version1);
            var expected = context.Home.RotationOrder[0].Derived.ResultFingerprint;

            var restored = ContractJson.DeserializeMatchContextV4(
                ContractJson.SerializeV4(context));

            Assert.That(restored.FormulaVersion, Is.EqualTo(1));
            Assert.That(restored.CoefficientVersion, Is.EqualTo(1));
            Assert.That(
                restored.Home.RotationOrder[0].Derived.ResultFingerprint,
                Is.EqualTo(expected));
        }

        [Test]
        public void PlayerSnapshotV4_RecomputesDerivationAndRejectsSuppliedFingerprintMismatch()
        {
            var physical = CreateDerivationPhysical();
            var technical = CreateDerivationTechnical();
            var supplied = MatchAttributeDerivationV4.Derive(
                physical,
                technical,
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);

            Assert.That(
                () => new PlayerSnapshotV4(
                    new PlayerId("player-one"),
                    "Player One",
                    1,
                    PlayerPosition.Setter,
                    DominantHandV4.Left,
                    physical,
                    technical,
                    MatchAttributeDerivationConfigV4.Version1,
                    supplied),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("fingerprint"));
        }

        [Test]
        public void TeamSnapshotV4_RequiresExactlySixUniquePlayersInRotationOrder()
        {
            var players = CreatePlayersV4("blue");
            var team = new TeamSnapshotV4(new TeamId("team-blue"), "Blue Team", TeamSide.Home, players);

            Assert.That(team.RotationOrder, Has.Count.EqualTo(6));
            Assert.That(team.Players, Is.EqualTo(team.RotationOrder));
            Assert.That(
                () => new TeamSnapshotV4(
                    new TeamId("too-small"),
                    "Too Small",
                    TeamSide.Home,
                    players.Take(5).ToArray()),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("exactly six"));
            Assert.That(
                () => new TeamSnapshotV4(
                    new TeamId("duplicates"),
                    "Duplicates",
                    TeamSide.Home,
                    players.Take(5).Concat(new[] { players[0] }).ToArray()),
                Throws.TypeOf<ContractValidationException>().With.Message.Contains("unique"));
        }

        [Test]
        public void MatchContextV4DeserializerRejectsMissingLegacyUnknownAndWrongVersionPayloads()
        {
            var context = CreateContextV4(Guid.NewGuid(), 19);
            var json = ContractJson.SerializeV4(context);
            var missing = json.Replace(
                "\"physicsConfigurationHash\":\"" + PhysicsConfigurationHashV4 + "\",",
                string.Empty);
            var missingPredictionConfiguration = json.Replace(
                "\"trajectoryPredictionProviderConfiguration\":" +
                "{\"cacheCapacity\":128,\"cacheEvictionPolicy\":0,\"predictorVersion\":4," +
                "\"predictorConfigurationHash\":\"" + PredictorConfigurationHashV4 + "\"},",
                string.Empty);
            var unknownPredictionConfigurationField = json.Replace(
                "\"cacheCapacity\":128,",
                "\"cacheCapacity\":128,\"requestingTeam\":0,");
            var legacyAbility = json.Replace("\"derived\":{", "\"ability\":{},\"derived\":{");
            var wrongVersion = json.Replace("\"contractVersion\":4", "\"contractVersion\":3");

            Assert.That(missing, Is.Not.EqualTo(json));
            Assert.That(missingPredictionConfiguration, Is.Not.EqualTo(json));
            Assert.That(unknownPredictionConfigurationField, Is.Not.EqualTo(json));
            Assert.That(legacyAbility, Is.Not.EqualTo(json));
            Assert.That(wrongVersion, Is.Not.EqualTo(json));
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(missing),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(missingPredictionConfiguration),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(unknownPredictionConfigurationField),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(legacyAbility),
                Throws.TypeOf<ContractValidationException>());
            Assert.That(
                () => ContractJson.DeserializeMatchContextV4(wrongVersion),
                Throws.TypeOf<ContractValidationException>());
        }

        [Test]
        public void MatchResultV4_CanonicalJsonRoundTripsWithAuthoritySummary()
        {
            var context = CreateContextV4(
                new Guid("42e99cf4-b7bf-449e-9281-f82dbe0f6aa4"),
                7351);
            var result = MatchResultV4.Create(
                context,
                context.Home.TeamId,
                homeScore: 15,
                awayScore: 12,
                ralliesPlayed: 27,
                acceptedContacts: 81,
                v3RuleTransitionCount: 81,
                new[]
                {
                    new PlayerMatchStatsV4(
                        context.Home.RotationOrder[0].PlayerId,
                        points: 6,
                        contacts: 12,
                        errors: 1,
                        workload: 16.5f)
                });
            var expected = ContractJson.SerializeV4(result);

            for (var repetition = 0; repetition < 100; repetition++)
            {
                Assert.That(ContractJson.SerializeV4(result), Is.EqualTo(expected));
            }

            var restored = ContractJson.DeserializeMatchResultV4(expected);

            Assert.That(ContractJson.SerializeV4(restored), Is.EqualTo(expected));
            Assert.DoesNotThrow(() => restored.ValidateAgainst(context));
            Assert.That(restored.ContractVersion, Is.EqualTo(ContractVersions.MatchV4));
            Assert.That(restored.WinnerTeamId, Is.EqualTo(context.Home.TeamId));
            Assert.That(restored.HomeScore, Is.EqualTo(15));
            Assert.That(restored.AwayScore, Is.EqualTo(12));
            Assert.That(restored.RalliesPlayed, Is.EqualTo(27));
            Assert.That(restored.AcceptedContacts, Is.EqualTo(81));
            Assert.That(restored.V3RuleTransitionCount, Is.EqualTo(81));
            Assert.That(restored.ResultHash, Is.EqualTo(result.ResultHash));
        }

        [Test]
        public void MatchResultV4DeserializerRejectsMissingUnknownWrongVersionAndLegacyJson()
        {
            var context = CreateContextV4(Guid.NewGuid(), 17);
            var result = MatchResultV4.Create(
                context,
                context.Home.TeamId,
                15,
                9,
                24,
                72,
                72,
                Array.Empty<PlayerMatchStatsV4>());
            var json = ContractJson.SerializeV4(result);
            var missing = json.Replace("\"acceptedContacts\":72,", string.Empty);
            var unknown = json.Replace("\"winnerTeamId\":", "\"ability\":{},\"winnerTeamId\":");
            var wrongVersion = json.Replace("\"contractVersion\":4", "\"contractVersion\":3");
            Assert.That(() => ContractJson.DeserializeMatchResultV4(missing), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeMatchResultV4(unknown), Throws.TypeOf<ContractValidationException>());
            Assert.That(() => ContractJson.DeserializeMatchResultV4(wrongVersion), Throws.TypeOf<ContractValidationException>());
        }

        private static void AssertPhysicalBaseAttributeRejected(int field, float value)
        {
            Assert.That(
                () => CreatePhysicalBaseAttributes(value, field),
                Throws.TypeOf<ContractValidationException>(),
                "Physical field " + field + " must reject " + value + ".");
        }

        private static PhysicalBaseAttributesV4 CreatePhysicalBaseAttributes(float value, int field)
        {
            var values = new[] { 1.80f, 2.35f, 0.5f, 0.5f, 0.5f, 0.5f };
            if (field >= 0)
            {
                values[field] = value;
            }

            return new PhysicalBaseAttributesV4(
                values[0], values[1], values[2], values[3], values[4], values[5]);
        }

        private static void AssertTechnicalBaseAttributeRejected(int field, float value)
        {
            Assert.That(
                () => CreateTechnicalBaseAttributes(value, field),
                Throws.TypeOf<ContractValidationException>(),
                "Technical field " + field + " must reject " + value + ".");
        }

        private static TechnicalBaseAttributesV4 CreateTechnicalBaseAttributes(float value, int field)
        {
            var values = new[] { value, value, value, value, value, value, value, value, value };
            if (field >= 0)
            {
                values = new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
                values[field] = value;
            }

            return new TechnicalBaseAttributesV4(
                values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
        }

        private static PhysicalBaseAttributesV4 CreateDerivationPhysical(
            float heightMeters = 1.90f,
            float standingReachMeters = 2.50f,
            float jump = 0.35f,
            float mobility = 0.70f,
            float reaction = 0.70f,
            float coordination = 0.55f)
        {
            return new PhysicalBaseAttributesV4(
                heightMeters,
                standingReachMeters,
                jump,
                mobility,
                reaction,
                coordination);
        }

        private static TechnicalBaseAttributesV4 CreateDerivationTechnical(
            float delta = 0f,
            int technicalField = -1)
        {
            var values = new[] { 0.70f, 0.75f, 0.65f, 0.60f, 0.65f, 0.70f, 0.65f, 0.60f, 0.60f };
            if (technicalField >= 0)
            {
                values[technicalField] += delta;
            }

            return new TechnicalBaseAttributesV4(
                values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
        }

        private static DerivedMatchAttributesV4 DeriveV4(
            PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical)
        {
            return MatchAttributeDerivationV4.Derive(
                physical,
                technical,
                DominantHandV4.Right,
                MatchAttributeDerivationConfigV4.Version1);
        }

        private static void AssertDerivedOutputChanges(
            DerivedMatchAttributesV4 baseline,
            DerivedMatchAttributesV4 changed,
            Func<MatchAttributesV4, float> select)
        {
            Assert.That(select(changed.Attributes), Is.Not.EqualTo(select(baseline.Attributes)));
            Assert.That(changed.ResultFingerprint, Is.Not.EqualTo(baseline.ResultFingerprint));
        }

        private static IEnumerable<MatchAttributeCoefficientV4> ReplaceCoefficient(
            IEnumerable<MatchAttributeCoefficientV4> source,
            int index,
            float value)
        {
            return source.Select((coefficient, current) =>
                current == index
                    ? new MatchAttributeCoefficientV4(
                        coefficient.OutputName,
                        coefficient.InputName,
                        value)
                    : coefficient);
        }

        private const string PhysicsConfigurationHashV4 =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private const string PredictorConfigurationHashV4 =
            "123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0";

        private const string AlternatePredictorConfigurationHashV4 =
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        private static MatchReplayEventV4 CreateGateISetEvent(MatchContextV4 context)
        {
            var actor = context.Home.RotationOrder[0];
            var envelope = new ReplayExecutionEnvelopeRecordV4(
                4, PhysicsConfigurationHashV4, actor.Derived.ResultFingerprint,
                PredictorConfigurationHashV4, "gate-i-set-intent", "Set",
                new ReplayVector3RecordV4(1f, 3f, -1f),
                new ReplayVector3RecordV4(1f, 2f, -1f),
                new ReplayVector3RecordV4(2f, 4f, 2f),
                new ReplayBoundedErrorRecordV4("BoundedUniform",
                    new ReplayVector3RecordV4(-.1f, -.1f, -.1f),
                    new ReplayVector3RecordV4(.1f, .1f, .1f)),
                new ReplayBoundedErrorRecordV4("SymmetricTriangular",
                    new ReplayVector3RecordV4(-.1f, -.1f, -.1f),
                    new ReplayVector3RecordV4(.1f, .1f, .1f)),
                .7f, .8f, "gate-i-set-sampling", 1, 1,
                new[] { "Set" }, new[] { "FullSampling" }, 0, 0, 0, 1f);
            var trajectory = new ReplayTrajectoryArtifactRecordV4(
                AlternatePredictorConfigurationHashV4, "formal-v4",
                context.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                context.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                new ReplayTrajectoryCacheKeyRecordV4(
                    PredictorConfigurationHashV4, 42, PhysicsConfigurationHashV4,
                    context.PhysicsConfigurationHash, "gate-i-set-sampling",
                    context.TrajectoryPredictionProviderConfiguration.PredictorVersion,
                    context.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash,
                    PhysicsConfigurationHashV4, "FullSampling"));
            var classification = new ReplaySampleClassificationRecordV4(
                "Accepted", PhysicsConfigurationHashV4, string.Empty,
                new ReplayActualSampleRecordV4(PhysicsConfigurationHashV4,
                    "gate-i-set-sampling", "Set",
                    new ReplayVector3RecordV4(1f, 3f, -1f),
                    new ReplayVector3RecordV4(1f, 2f, -1f), .7f),
                Array.Empty<string>());
            return new MatchReplayEventV4(0, "Set", actor.PlayerId.Value, 1f, 0, 0,
                envelope, envelope, trajectory,
                new[] { new ReplayAbilityConsumptionRecordV4(actor.PlayerId.Value,
                    actor.Derived.ResultFingerprint, "Set.PlacementControl", .7f,
                    "ExecutionEnvelopeFactoryRead") }, classification, null,
                new ReplayRuleDecisionRecordV4(RulesVersions.FullRallyV3, true, "None"));
        }

        private static ReplayOrganizationAuthorityRecordV4 CreateSetOrganizationAuthority(
            MatchReplayEventV4 replayEvent)
        {
            return new ReplayOrganizationAuthorityRecordV4(7, 3, "Organize",
                new ReplayVector3RecordV4(1f, 3f, -1f),
                new ReplayVector3RecordV4(0f, 2f, -2f), "Best",
                replayEvent.ActorPlayerId, "Reachable", 0f, 0f, .3f,
                replayEvent.ActorPlayerId, "None", "Primary",
                replayEvent.TestedEnvelope.Identity, replayEvent.ExecutableEnvelope.Identity,
                replayEvent.Classification.ActualSample.EnvelopeIdentity,
                replayEvent.Trajectory.ArtifactIdentity,
                new ReplayCoverageDecisionRecordV4("Covered", 0f));
        }

        private static ReplayAttackDefenseAuthorityRecordV4 CreateSetIntentAuthority(
            MatchReplayEventV4 replayEvent, int revision)
        {
            return new ReplayAttackDefenseAuthorityRecordV4(revision, 19,
                "SetIntentPlanned", "Primary", new ReplayVector3RecordV4(1f, 3f, -1f),
                Array.Empty<ReplayAttackDefenseCandidateRecordV4>(),
                Array.Empty<ReplayPublicAttackThreatRecordV4>(),
                Array.Empty<ReplayDefenseResponsibilityRecordV4>(), string.Empty,
                replayEvent.TestedEnvelope.Identity, replayEvent.ExecutableEnvelope.Identity,
                replayEvent.Classification.ActualSample.EnvelopeIdentity,
                replayEvent.Trajectory.ArtifactIdentity, null,
                new ReplayCoverageDecisionRecordV4("Covered", 0f));
        }

        private static MatchReplayEventV4 CreateGateIDefenseEvent(
            string actorPlayerId,
            int sequenceNumber = 4)
        {
            var identity = new string('d', 64);
            var fingerprint = new string('e', 64);
            var envelope = new ReplayExecutionEnvelopeRecordV4(
                4, identity, fingerprint, PredictorConfigurationHashV4,
                "gate-i-defense", "Defense",
                new ReplayVector3RecordV4(2f, 1f, -5f),
                new ReplayVector3RecordV4(0f, 5f, 1f),
                new ReplayVector3RecordV4(8f, 8f, 8f),
                new ReplayBoundedErrorRecordV4("BoundedUniform",
                    new ReplayVector3RecordV4(-.1f, -.1f, -.1f),
                    new ReplayVector3RecordV4(.1f, .1f, .1f)),
                new ReplayBoundedErrorRecordV4("SymmetricTriangular",
                    new ReplayVector3RecordV4(-.1f, -.1f, -.1f),
                    new ReplayVector3RecordV4(.1f, .1f, .1f)),
                .5f, .8f, "gate-i-defense-sampling", 1, 1,
                new[] { "Defense" }, new[] { "FullSampling" },
                0, 0, 0, 1f);
            var trajectory = new ReplayTrajectoryArtifactRecordV4(
                identity, "formal-v4", 4, PredictorConfigurationHashV4,
                new ReplayTrajectoryCacheKeyRecordV4(
                    fingerprint, 43, identity, PhysicsConfigurationHashV4,
                    "gate-i-defense-sampling", 4,
                    PredictorConfigurationHashV4, identity, "FullSampling"));
            var classification = new ReplaySampleClassificationRecordV4(
                "Accepted", identity, string.Empty,
                new ReplayActualSampleRecordV4(identity,
                    "gate-i-defense-sampling", "Defense",
                    new ReplayVector3RecordV4(2f, 1f, -5f),
                    new ReplayVector3RecordV4(0f, 5f, 1f), .5f),
                Array.Empty<string>());
            return new MatchReplayEventV4(
                sequenceNumber, "Receive", actorPlayerId, 2f, 0, 0,
                envelope, envelope, trajectory,
                new[]
                {
                    new ReplayAbilityConsumptionRecordV4(
                        actorPlayerId,
                        fingerprint,
                        "Defense.Reaction",
                        .5f,
                        "ExecutionEnvelopeFactoryRead")
                },
                classification, null,
                new ReplayRuleDecisionRecordV4(
                    RulesVersions.FullRallyV3, true, "None"));
        }

        private static ReplayAttackDefenseAuthorityRecordV4
            CreateObservedDefenseAuthority(
                MatchReplayEventV4 replayEvent,
                string defenderPlayerId,
                string phase = "AwaitingActualContact",
                string selectedCandidateIdentity = "attack-selected",
                string attackCoveragePlayerId = null)
        {
            var candidate = new ReplayAttackDefenseCandidateRecordV4(
                "attack-selected", "home-attacker", "PowerLine",
                new ReplayVector3RecordV4(2f, .12f, -5.25f),
                .7f, 1f, true, string.Empty,
                replayEvent.ExecutableEnvelope.Identity,
                replayEvent.Trajectory.ArtifactIdentity,
                string.Empty);
            return new ReplayAttackDefenseAuthorityRecordV4(
                9, 21, phase, "Primary",
                new ReplayVector3RecordV4(2f, 2.8f, 2f),
                new[] { candidate },
                new[]
                {
                    new ReplayPublicAttackThreatRecordV4(
                        "PowerLine", "Line", 1f, 2f)
                },
                new[]
                {
                    new ReplayDefenseResponsibilityRecordV4(
                        defenderPlayerId, "LineDefense", "Line", "Primary")
                },
                selectedCandidateIdentity,
                replayEvent.TestedEnvelope.Identity,
                replayEvent.ExecutableEnvelope.Identity,
                replayEvent.Classification.ActualSample.EnvelopeIdentity,
                replayEvent.Trajectory.ArtifactIdentity,
                null,
                new ReplayCoverageDecisionRecordV4(
                    phase == "DefenseCommitted" ? "Covered" : "Local",
                    phase == "DefenseCommitted" ? 0f : 1f,
                    phase == "DefenseCommitted"
                        ? "WithinConditionalEnvelope"
                        : "ResponsibleActorChanged",
                    Array.Empty<string>(),
                    phase == "DefenseCommitted" ? 0 : 1,
                    "Primary"),
                attackCoveragePlayerId == null
                    ? Array.Empty<
                        ReplayAttackCoverageResponsibilityRecordV4>()
                    : new[]
                    {
                        new ReplayAttackCoverageResponsibilityRecordV4(
                            attackCoveragePlayerId,
                            "Primary")
                    });
        }

        private static MatchReplayEventV4 WithGateIAuthority(
            MatchReplayEventV4 replayEvent,
            ReplayOrganizationAuthorityRecordV4 organization,
            ReplayAttackDefenseAuthorityRecordV4 authority)
        {
            return new MatchReplayEventV4(replayEvent.SequenceNumber,
                replayEvent.EventKind, replayEvent.ActorPlayerId,
                replayEvent.SimulationTimeSeconds, replayEvent.HomeScore,
                replayEvent.AwayScore, replayEvent.TestedEnvelope,
                replayEvent.ExecutableEnvelope, replayEvent.Trajectory,
                replayEvent.AbilityConsumptions, replayEvent.Classification,
                replayEvent.ObservedP6Geometry, replayEvent.RuleDecision,
                replayEvent.Shadow, organization, authority);
        }

        private static MatchContextV4 CreateContextV4(
            Guid sessionId,
            int seed,
            PhysicalBaseAttributesV4 firstPhysical = null,
            DominantHandV4 firstHand = DominantHandV4.Right,
            MatchAttributeDerivationConfigV4 config = null,
            TrajectoryPredictionProviderConfigurationV4 predictionConfiguration = null)
        {
            var derivationConfig = config ?? MatchAttributeDerivationConfigV4.Version1;
            return MatchContextV4.Create(
                sessionId,
                seed,
                CreateTeamV4(
                    "team-blue",
                    TeamSide.Home,
                    "blue",
                    firstPhysical,
                    firstHand,
                    derivationConfig),
                CreateTeamV4(
                    "team-orange",
                    TeamSide.Away,
                    "orange",
                    config: derivationConfig),
                PhysicsConfigurationHashV4,
                predictionConfiguration ?? CreatePredictionProviderConfigurationV4(),
                rulesVersion: RulesVersions.FullRallyV3);
        }

        private static TrajectoryPredictionProviderConfigurationV4
            CreatePredictionProviderConfigurationV4()
        {
            return new TrajectoryPredictionProviderConfigurationV4(
                128,
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut,
                4,
                PredictorConfigurationHashV4);
        }

        private static TeamSnapshotV4 CreateTeamV4(
            string teamId,
            TeamSide side,
            string playerPrefix,
            PhysicalBaseAttributesV4 firstPhysical = null,
            DominantHandV4 firstHand = DominantHandV4.Right,
            MatchAttributeDerivationConfigV4 config = null)
        {
            return new TeamSnapshotV4(
                new TeamId(teamId),
                side == TeamSide.Home ? "Blue Team" : "Orange Team",
                side,
                CreatePlayersV4(playerPrefix, firstPhysical, firstHand, config));
        }

        private static PlayerSnapshotV4[] CreatePlayersV4(
            string playerPrefix,
            PhysicalBaseAttributesV4 firstPhysical = null,
            DominantHandV4 firstHand = DominantHandV4.Right,
            MatchAttributeDerivationConfigV4 config = null)
        {
            var derivationConfig = config ?? MatchAttributeDerivationConfigV4.Version1;
            var positions = new[]
            {
                PlayerPosition.Setter,
                PlayerPosition.OutsideHitter,
                PlayerPosition.MiddleBlocker,
                PlayerPosition.Opposite,
                PlayerPosition.Libero,
                PlayerPosition.Defender
            };
            var players = new PlayerSnapshotV4[6];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = new PlayerSnapshotV4(
                    new PlayerId(playerPrefix + "-player-" + (index + 1)),
                    playerPrefix + " Player " + (index + 1),
                    index + 1,
                    positions[index],
                    index == 0 ? firstHand : DominantHandV4.Right,
                    index == 0 && firstPhysical != null ? firstPhysical : CreateDerivationPhysical(),
                    CreateDerivationTechnical(),
                    derivationConfig);
            }

            return players;
        }

        private static CareerBaseAttributesV5 CreateV5Bases(
            int basisPoints,
            int heightMillimeters)
        {
            return new CareerBaseAttributesV5(
                basisPoints,
                heightMillimeters,
                basisPoints,
                basisPoints,
                basisPoints,
                basisPoints,
                basisPoints,
                basisPoints,
                basisPoints,
                basisPoints,
                basisPoints,
                basisPoints);
        }

        private static CareerMatchPlayerReportV1 CreateCareerReport(PlayerId playerId, bool nonZero)
        {
            return new CareerMatchPlayerReportV1(playerId,
                nonZero ? 3 : 0, nonZero ? 1 : 0, nonZero ? 1 : 0,
                nonZero ? 2 : 0, nonZero ? 1 : 0, 0,
                nonZero ? 4 : 0, nonZero ? 1 : 0, nonZero ? 1 : 0, nonZero ? 1 : 0, 0, nonZero ? 1 : 0,
                nonZero ? 2 : 0, nonZero ? 1 : 0, nonZero ? 2 : 0, nonZero ? 1 : 0, 0,
                nonZero ? 2 : 0, nonZero ? 1 : 0, nonZero ? 1 : 0,
                nonZero ? 45 : 0, nonZero ? 2000 : 0, nonZero ? 2 : 0, nonZero ? 2500 : 0, 1,
                nonZero ? 2 : 0, nonZero ? 1 : 0, nonZero ? 1 : 0, nonZero ? 1 : 0, nonZero ? 1 : 0,
                nonZero ? 1 : 0, nonZero ? 1 : 0);
        }

        private static TeamSnapshotV5 CreateV5Team(
            string prefix,
            TeamSide side,
            int basisPoints)
        {
            var players = new PlayerSnapshotV5[6];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = new PlayerSnapshotV5(
                    new PlayerId(prefix + "-player-" + index),
                    prefix + " Player " + index,
                    index + 1,
                    PlayerPosition.Setter,
                    DominantHandV5.Right,
                    CreateV5Bases(basisPoints, 1800));
            }

            return new TeamSnapshotV5(new TeamId(prefix + "-team"), prefix + " Team", side, players);
        }

        private static TrajectoryPredictionProviderConfigurationV5 CreateV5TrajectoryConfiguration()
        {
            return new TrajectoryPredictionProviderConfigurationV5(
                128, TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut, 1, new string('c', 64));
        }

    }
}
