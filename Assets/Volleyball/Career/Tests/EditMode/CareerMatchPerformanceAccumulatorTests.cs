using System;
using System.Linq;
using NUnit.Framework;
using Volleyball.Bootstrap;
using Volleyball.Career.MatchIntegration;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchPerformanceAccumulatorTests
    {
        [Test]
        public void DirectServeResolution_ProducesAceAndFullRosterLoad()
        {
            var context = Context();
            var server = context.Home.RotationOrder[0].PlayerId;
            var accumulator = new MatchPerformanceAccumulatorV1(context);
            accumulator.StartCapture();

            accumulator.RecordServe(
                server,
                TeamSide.Home,
                10d,
                0,
                0);
            accumulator.RecordRallyResolved(
                TeamSide.Home,
                null,
                12.25d);

            var report = accumulator.Complete(Result(context, 1, 0, 1));
            var serverReport = Player(report, server);

            Assert.That(serverReport.Serve.Attempts, Is.EqualTo(1));
            Assert.That(serverReport.Serve.Aces, Is.EqualTo(1));
            Assert.That(serverReport.Serve.Errors, Is.Zero);
            Assert.That(
                report.PlayerReports.All(
                    player =>
                        player.Load.RalliesOnCourt == 1 &&
                        player.Load.ActiveDurationMilliseconds == 2250),
                Is.True);
        }

        [Test]
        public void AcceptedActionChain_UsesConservativeProvenFacts()
        {
            var context = Context();
            var server = context.Home.RotationOrder[0].PlayerId;
            var receiver = context.Away.RotationOrder[0].PlayerId;
            var setter = context.Away.RotationOrder[1].PlayerId;
            var attacker = context.Away.RotationOrder[2].PlayerId;
            var blocker = context.Home.RotationOrder[1].PlayerId;
            var accumulator = new MatchPerformanceAccumulatorV1(context);
            accumulator.StartCapture();

            accumulator.RecordServe(server, TeamSide.Home, 0d, 24, 23);
            accumulator.RecordAcceptedContact(
                receiver,
                TeamSide.Away,
                "Receive",
                0.5d,
                24,
                23);
            accumulator.RecordReceptionQuality(TeamSide.Away, 0.75d);
            accumulator.RecordAcceptedContact(
                setter,
                TeamSide.Away,
                "Set",
                1d,
                24,
                23);
            accumulator.RecordAcceptedContact(
                attacker,
                TeamSide.Away,
                "Attack",
                1.5d,
                24,
                23);
            accumulator.RecordAcceptedContact(
                blocker,
                TeamSide.Home,
                "Block",
                1.6d,
                24,
                23);
            accumulator.RecordRallyResolved(
                TeamSide.Home,
                null,
                2d);

            var report = accumulator.Complete(Result(context, 1, 0, 1));

            Assert.That(Player(report, server).Serve.Aces, Is.Zero);
            Assert.That(Player(report, receiver).Reception.Perfect, Is.EqualTo(1));
            Assert.That(Player(report, setter).Setting.Successful, Is.EqualTo(1));
            Assert.That(Player(report, attacker).Spike.Points, Is.Zero);
            Assert.That(Player(report, blocker).Block.Attempts, Is.EqualTo(1));
            Assert.That(
                Player(report, blocker).Block.EffectiveTouches,
                Is.EqualTo(1));
            Assert.That(Player(report, blocker).Block.Points, Is.EqualTo(1));
        }

        [Test]
        public void DefenseLifecycle_DeduplicatesAttemptAndSuccess()
        {
            var context = Context();
            var defender = context.Home.RotationOrder[4].PlayerId;
            var accumulator = new MatchPerformanceAccumulatorV1(context);
            accumulator.StartCapture();

            Assert.That(
                MatchPerformanceAccumulatorV1.NormalizeAction(
                    "Receive",
                    "FloorDefense",
                    true,
                    false),
                Is.EqualTo("Defense"));
            Assert.That(
                MatchPerformanceAccumulatorV1.NormalizeAction(
                    "Receive",
                    "AttackCover",
                    false,
                    false),
                Is.EqualTo("Other"));
            Assert.That(
                MatchPerformanceAccumulatorV1.NormalizeAction(
                    "Receive",
                    null,
                    true,
                    false),
                Is.EqualTo("Other"));
            Assert.That(
                MatchPerformanceAccumulatorV1.NormalizeAction(
                    "Receive",
                    null,
                    true,
                    true),
                Is.EqualTo("Receive"));
            accumulator.RecordAcceptedContact(
                defender,
                TeamSide.Home,
                MatchPerformanceAccumulatorV1.NormalizeAction(
                    "Receive",
                    "FloorDefense",
                    true,
                    false),
                0.5d,
                10,
                10);
            accumulator.RecordAcceptedContact(
                defender,
                TeamSide.Home,
                MatchPerformanceAccumulatorV1.NormalizeAction(
                    "Receive",
                    null,
                    true,
                    false),
                0.6d,
                10,
                10);
            accumulator.RecordDefenseAttempt(
                "defense:1",
                defender,
                TeamSide.Home,
                "FloorDefense",
                "DefenseAttemptOpened",
                10,
                10);
            accumulator.RecordDefenseAttempt(
                "defense:1",
                defender,
                TeamSide.Home,
                "FloorDefense",
                "DefenseAttemptOpened",
                10,
                10);
            accumulator.RecordDefenseAttempt(
                "defense:1",
                defender,
                TeamSide.Home,
                "FloorDefense",
                "DefenseContactAccepted",
                10,
                10);
            accumulator.RecordDefenseAttempt(
                "defense:1",
                defender,
                TeamSide.Home,
                "FloorDefense",
                "DefenseContactAccepted",
                10,
                10);
            accumulator.RecordRallyResolved(TeamSide.Home, null, 1d);

            var report = accumulator.Complete(Result(context, 1, 0, 1));
            var facts = Player(report, defender).Defense;

            Assert.That(facts.Attempts, Is.EqualTo(1));
            Assert.That(facts.Successes, Is.EqualTo(1));
            Assert.That(Player(report, defender).Reception.Attempts, Is.Zero);
        }

        [Test]
        public void ReceptionQuality_UsesFrozenHorizontalThresholds()
        {
            var context = Context();
            var receiver = context.Away.RotationOrder[0].PlayerId;
            var accumulator = new MatchPerformanceAccumulatorV1(context);
            accumulator.StartCapture();
            var distances = new[] { 0.75d, 1.50d, 2.75d, 2.7501d };
            for (var index = 0; index < distances.Length; index++)
            {
                accumulator.RecordAcceptedContact(
                    receiver,
                    TeamSide.Away,
                    "Receive",
                    index + 0.1d,
                    0,
                    0);
                accumulator.RecordReceptionQuality(
                    TeamSide.Away,
                    distances[index]);
            }

            accumulator.RecordRallyResolved(TeamSide.Home, null, 5d);
            var facts = Player(
                accumulator.Complete(Result(context, 1, 0, 1)),
                receiver).Reception;

            Assert.That(facts.Attempts, Is.EqualTo(4));
            Assert.That(facts.Perfect, Is.EqualTo(1));
            Assert.That(facts.Positive, Is.EqualTo(1));
            Assert.That(facts.Neutral, Is.EqualTo(1));
            Assert.That(facts.Negative, Is.EqualTo(1));
            Assert.That(facts.Errors, Is.Zero);
        }

        private static MatchContextV4 Context()
        {
            return new CareerMatchV4Mapper().ToContext(
                CareerMatchTestData.Launch());
        }

        private static MatchResultV4 Result(
            MatchContextV4 context,
            int homeScore,
            int awayScore,
            int rallies)
        {
            return MatchResultV4.Create(
                context,
                homeScore > awayScore
                    ? context.Home.TeamId
                    : context.Away.TeamId,
                homeScore,
                awayScore,
                rallies,
                0,
                0,
                Array.Empty<PlayerMatchStatsV4>());
        }

        private static PlayerPerformanceReportV1 Player(
            MatchPerformanceReportV1 report,
            PlayerId playerId)
        {
            return report.PlayerReports.Single(
                value => value.PlayerId.Equals(playerId));
        }
    }
}
