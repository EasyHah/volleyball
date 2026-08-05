using System;
using System.Collections.Generic;
using NUnit.Framework;
using Volleyball.Presentation;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class CareerMatchReportAggregatorV1Tests
    {
        [Test]
        public void Aggregate_ProducesCompleteGoldenReportFromBoundPhysicalFacts()
        {
            var context = MatchContextV5.Create(new Guid("12121212-3434-5656-7878-909090909090"), 44,
                Team("home", TeamSide.Home), Team("away", TeamSide.Away), new string('a', 64), Configuration());
            var player = context.Home.RotationOrder[0].PlayerId;
            var evidence = new List<MatchReplayAttributeEvidenceV5>();
            var facts = new List<MatchReplayReportFactV1>();
            AddContact(context, evidence, facts, player, "Receive", 0, 40);
            AddContact(context, evidence, facts, player, "Receive", 500, 40);
            AddContact(context, evidence, facts, player, "Receive", 1000, 40);
            AddContact(context, evidence, facts, player, "Receive", 2000, 40);
            AddContact(context, evidence, facts, player, "Receive", 100, 40);
            facts.Add(Fact(facts, player, "RallyResult", "Receive", false, false, 0, related: 4));
            AddContact(context, evidence, facts, player, "Attack", 50, 90);
            facts.Add(Fact(facts, player, "RallyResult", "Attack", true, true, 0, related: 6));
            AddContact(context, evidence, facts, player, "Serve", 0, 25);
            facts.Add(Fact(facts, player, "RallyResult", "Serve", false, true, 0, related: 8));
            AddContact(context, evidence, facts, player, "Block", 0, 75);
            facts.Add(Fact(facts, player, "RallyResult", "Block", true, false, 0, related: 10));
            AddContact(context, evidence, facts, player, "Set", 0, 25);
            facts.Add(Fact(facts, player, "RallyResult", "Set", false, false, 0, related: 12));
            facts.Add(new MatchReplayReportFactV1(facts.Count, player, "Decision", "Attack", true, false, 0,
                executableChoices: 2, selectedChoice: "Attack", decisionReason: "HighestExecutableScore"));

            var replay = MatchReplayV5.Create("physical-golden", context, evidence, facts);
            var result = MatchResultV5.Create(context, context.Home.TeamId, 25, 20, 45);
            var report = CareerMatchReportAggregatorV1.Aggregate(context, result, replay);
            var actual = report.PlayerReports[0];

            Assert.That(report.PlayerReports, Has.Count.EqualTo(12));
            Assert.That(report.EvidenceHash, Is.EqualTo(replay.ReplayHash));
            Assert.That(actual.AttackAttempts, Is.EqualTo(1));
            Assert.That(actual.AttackPoints, Is.EqualTo(1));
            Assert.That(actual.ServeAttempts, Is.EqualTo(1));
            Assert.That(actual.ServeErrors, Is.EqualTo(1));
            Assert.That(actual.ReceiveAttempts, Is.EqualTo(5));
            Assert.That(actual.ReceivePerfect, Is.EqualTo(1));
            Assert.That(actual.ReceivePositive, Is.EqualTo(1));
            Assert.That(actual.ReceiveNeutral, Is.EqualTo(1));
            Assert.That(actual.ReceiveNegative, Is.EqualTo(1));
            Assert.That(actual.ReceiveErrors, Is.EqualTo(1));
            Assert.That(actual.BlockAttempts, Is.EqualTo(1));
            Assert.That(actual.BlockEffectiveTouches, Is.EqualTo(1));
            Assert.That(actual.BlockPoints, Is.EqualTo(1));
            Assert.That(actual.SetAttempts, Is.EqualTo(1));
            Assert.That(actual.SetSuccesses, Is.Zero);
            Assert.That(actual.SetErrors, Is.EqualTo(1));
            Assert.That(actual.MovementMillimeters, Is.EqualTo(3650));
            Assert.That(actual.Jumps, Is.EqualTo(2));
            Assert.That(actual.WorkloadBasisPoints, Is.EqualTo(415));
            Assert.That(actual.CriticalActions, Is.EqualTo(2));
            Assert.That(actual.CriticalSuccesses, Is.EqualTo(1));
            Assert.That(actual.CriticalErrors, Is.EqualTo(1));
            Assert.That(actual.DecisionQualitySuccesses, Is.EqualTo(1));
            Assert.That(report.PlayerReports[1].AttackAttempts, Is.Zero);
            Assert.That(report.PlayerReports[11].WorkloadBasisPoints, Is.Zero);
        }

        [Test]
        public void Aggregate_RejectsReplayWhosePositionFaultFactsDifferFromResult()
        {
            var context = MatchContextV5.Create(Guid.NewGuid(), 44,
                Team("home", TeamSide.Home), Team("away", TeamSide.Away), new string('a', 64), Configuration());
            var resultFault = Fault(context, "Slot4BehindSlot5");
            var replayFault = Fault(context, "Slot3BehindSlot6");
            var result = MatchResultV5.Create(context, context.Away.TeamId, 0, 1, 1,
                new[] { resultFault });
            var replay = MatchReplayV5.Create("mismatched-faults", context,
                Array.Empty<MatchReplayAttributeEvidenceV5>(),
                Array.Empty<MatchReplayReportFactV1>(), new[] { replayFault });

            Assert.That(() => CareerMatchReportAggregatorV1.Aggregate(context, result, replay),
                Throws.TypeOf<ContractValidationException>());
        }

        private static MatchPositionFaultV5 Fault(MatchContextV5 context, string rule)
        {
            var requiredSlot = rule == "Slot4BehindSlot5" ? 4 : 3;
            var violatingSlot = rule == "Slot4BehindSlot5" ? 5 : 6;
            return new MatchPositionFaultV5(1, TeamSide.Home, TeamSide.Away, TeamSide.Home, rule,
                context.Home.RotationOrder[requiredSlot - 1].PlayerId, requiredSlot, 0, -1000,
                context.Home.RotationOrder[violatingSlot - 1].PlayerId, violatingSlot, 0, -2000);
        }

        private static void AddContact(MatchContextV5 context, ICollection<MatchReplayAttributeEvidenceV5> evidence,
            ICollection<MatchReplayReportFactV1> facts, PlayerId player, string action, int movement, int workload)
        {
            var evidenceSequence = evidence.Count;
            evidence.Add(new MatchReplayAttributeEvidenceV5(evidenceSequence, player, action, evidenceSequence * 100,
                Fingerprint(context, player)));
            facts.Add(new MatchReplayReportFactV1(facts.Count, player, "Contact", action, true, false, workload,
                movement, attributeEvidenceSequenceNumber: evidenceSequence));
        }

        private static MatchReplayReportFactV1 Fact(ICollection<MatchReplayReportFactV1> facts, PlayerId player,
            string kind, string action, bool success, bool critical, int workload, int movement = 0, int related = -1)
        {
            return new MatchReplayReportFactV1(facts.Count, player, kind, action, success, critical, workload,
                movement, relatedContactSequenceNumber: related);
        }

        private static string Fingerprint(MatchContextV5 context, PlayerId playerId)
        {
            foreach (var player in context.Home.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player.Derived.ResultFingerprint;
            foreach (var player in context.Away.RotationOrder)
                if (player.PlayerId.Equals(playerId)) return player.Derived.ResultFingerprint;
            throw new AssertionException("Player is absent from the test context.");
        }

        private static TeamSnapshotV5 Team(string prefix, TeamSide side)
        {
            var players = new PlayerSnapshotV5[6];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = new PlayerSnapshotV5(new PlayerId(prefix + ".p" + index), prefix + " Player " + index,
                    index + 1, PlayerPosition.Setter, DominantHandV5.Right,
                    new CareerBaseAttributesV5(5000, 1800, 5000, 5000, 5000, 5000,
                        5000, 5000, 5000, 5000, 5000, 5000));
            }
            return new TeamSnapshotV5(new TeamId(prefix + ".team"), prefix + " Team", side, players);
        }

        private static TrajectoryPredictionProviderConfigurationV5 Configuration() =>
            new TrajectoryPredictionProviderConfigurationV5(64,
                TrajectoryPredictionCacheEvictionPolicyV4.FirstInFirstOut, 1, new string('b', 64));
    }
}
