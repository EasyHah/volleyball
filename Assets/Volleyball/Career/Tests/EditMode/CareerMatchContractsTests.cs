using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    public sealed class CareerMatchContractsTests
    {
        [Test]
        public void MatchPort_HasTheExactCareerOwnedPersistedExecutionSignature()
        {
            var methods = typeof(ICareerMatchExecutor).GetMethods()
                .OrderBy(method => method.Name)
                .ToArray();

            Assert.That(methods.Select(method => method.Name), Is.EqualTo(new[]
            {
                nameof(ICareerMatchExecutor.DecodeAndValidate),
                nameof(ICareerMatchExecutor.Encode),
                nameof(ICareerMatchExecutor.ExecuteAsync)
            }));
            Assert.That(methods[0].ReturnType, Is.EqualTo(typeof(CareerMatchExecutionOutcome)));
            Assert.That(methods[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(byte[]), typeof(byte[]) }));
            Assert.That(methods[1].ReturnType, Is.EqualTo(typeof(CareerCanonicalMatchContext)));
            Assert.That(methods[1].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(CareerMatchLaunch) }));
            Assert.That(methods[2].ReturnType, Is.EqualTo(typeof(Task<CareerMatchExecutionOutcome>)));
            Assert.That(methods[2].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(CareerCanonicalMatchContext), typeof(CancellationToken) }));
            Assert.That(typeof(ICareerMatchExecutor).Assembly, Is.EqualTo(typeof(CareerMatchLaunch).Assembly));
            Assert.That(typeof(CareerMatchLaunch).Assembly, Is.EqualTo(typeof(CareerMatchFacts).Assembly));
            Assert.That(typeof(CareerCanonicalMatchContext).Assembly,
                Is.EqualTo(typeof(CareerMatchLaunch).Assembly));
            Assert.That(typeof(CareerMatchExecutionOutcome).Assembly,
                Is.EqualTo(typeof(CareerMatchLaunch).Assembly));
        }

        [Test]
        public void PersistedExecutionContracts_DefensivelyCopyBytesAndPreserveCorrelatedEvidence()
        {
            var contextBytes = new byte[] { 1, 2, 3 };
            var resultBytes = new byte[] { 4, 5, 6 };
            var contextDigest = new Sha256Digest(new string('a', 64));
            var resultDigest = new Sha256Digest(new string('b', 64));
            var context = new CareerCanonicalMatchContext(
                CareerMatchTestData.SessionId,
                contextDigest,
                contextBytes);
            var facts = CareerMatchTestData.Facts();
            var outcome = new CareerMatchExecutionOutcome(
                context,
                resultDigest,
                resultBytes,
                facts);

            contextBytes[0] = 99;
            resultBytes[0] = 99;
            var exposedContext = context.CanonicalContextUtf8;
            var exposedResult = outcome.CanonicalResultUtf8;
            exposedContext[1] = 88;
            exposedResult[1] = 88;

            Assert.That(context.SessionId, Is.EqualTo(CareerMatchTestData.SessionId));
            Assert.That(context.ContextDigest, Is.EqualTo(contextDigest));
            Assert.That(context.CanonicalContextUtf8, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(outcome.Context, Is.SameAs(context));
            Assert.That(outcome.ResultDigest, Is.EqualTo(resultDigest));
            Assert.That(outcome.CanonicalResultUtf8, Is.EqualTo(new byte[] { 4, 5, 6 }));
            Assert.That(outcome.Facts, Is.SameAs(facts));
        }

        [Test]
        public void PersistedExecutionContracts_RejectInvalidBytesDigestsAndCorrelations()
        {
            var contextDigest = new Sha256Digest(new string('a', 64));
            var resultDigest = new Sha256Digest(new string('b', 64));
            var context = new CareerCanonicalMatchContext(
                CareerMatchTestData.SessionId,
                contextDigest,
                new byte[] { 1 });
            var facts = CareerMatchTestData.Facts();

            Assert.Catch<ArgumentException>(() => new CareerCanonicalMatchContext(
                Guid.Empty, contextDigest, new byte[] { 1 }));
            Assert.Catch<ArgumentException>(() => new CareerCanonicalMatchContext(
                CareerMatchTestData.SessionId, default(Sha256Digest), new byte[] { 1 }));
            Assert.Throws<ArgumentNullException>(() => new CareerCanonicalMatchContext(
                CareerMatchTestData.SessionId, contextDigest, null));
            Assert.Catch<ArgumentException>(() => new CareerCanonicalMatchContext(
                CareerMatchTestData.SessionId, contextDigest, Array.Empty<byte>()));

            Assert.Throws<ArgumentNullException>(() => new CareerMatchExecutionOutcome(
                null, resultDigest, new byte[] { 1 }, facts));
            Assert.Catch<ArgumentException>(() => new CareerMatchExecutionOutcome(
                context, default(Sha256Digest), new byte[] { 1 }, facts));
            Assert.Throws<ArgumentNullException>(() => new CareerMatchExecutionOutcome(
                context, resultDigest, null, facts));
            Assert.Catch<ArgumentException>(() => new CareerMatchExecutionOutcome(
                context, resultDigest, Array.Empty<byte>(), facts));
            Assert.Throws<ArgumentNullException>(() => new CareerMatchExecutionOutcome(
                context, resultDigest, new byte[] { 1 }, null));

            var wrongSessionFacts = CareerMatchTestData.Facts(CareerMatchTestData.Launch(
                sessionId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
            Assert.Catch<ArgumentException>(() => new CareerMatchExecutionOutcome(
                context, resultDigest, new byte[] { 1 }, wrongSessionFacts));
            Assert.Catch<ArgumentException>(() => new CareerMatchExecutionOutcome(
                context,
                new Sha256Digest(new string('c', 64)),
                new byte[] { 1 },
                facts));
        }

        [Test]
        public void Launch_DefensivelyCopiesTeamsAndPlayersAndContainsOneProtagonist()
        {
            var homePlayers = CareerMatchTestData.Players("copy.home", true);
            var home = CareerMatchTestData.Team(
                new TeamId("team.copy.home"), CareerMatchTeamSide.Home, "ignored", true, homePlayers);
            var away = CareerMatchTestData.Team(
                new TeamId("team.copy.away"), CareerMatchTeamSide.Away, "copy.away", false);
            var teams = new[] { home, away };
            var launch = CareerMatchTestData.Launch(teams: teams);

            homePlayers[0] = null;
            teams[0] = away;

            Assert.That(launch.Teams[0].TeamId, Is.EqualTo(new TeamId("team.copy.home")));
            Assert.That(launch.Teams[0].Players[0].PlayerId.Value, Is.EqualTo("copy.home.opposite"));
            Assert.That(launch.Teams.SelectMany(team => team.Players).Count(), Is.EqualTo(12));
            Assert.That(launch.Teams.SelectMany(team => team.Players)
                .Count(player => player.PlayerId.Value == "player.career.protagonist"), Is.EqualTo(1));
            Assert.That(launch.Teams.SelectMany(team => team.Players).Select(player => player.PlayerId)
                .Distinct().Count(), Is.EqualTo(12));
        }

        [Test]
        public void Launch_PreservesAllInputFieldsWithoutDefaultsOrInference()
        {
            var versions = CareerMatchTestData.Versions(2, 7, 8, 9, null, null);
            var format = CareerMatchTestData.Format("indoor_6v6", 6, 3, 15, 2);
            var launch = CareerMatchTestData.Launch(
                versions: versions,
                sessionId: Guid.Parse("77777777-7777-7777-7777-777777777777"),
                matchSeed: uint.MaxValue,
                competitionId: "competition.custom.v7",
                scheduleItemId: "schedule.custom.09",
                importanceBasisPoints: 9876,
                format: format,
                priority: CareerPreMatchPriority.StaminaControl);

            Assert.That(launch.Versions, Is.SameAs(versions));
            Assert.That(launch.SessionId, Is.EqualTo(Guid.Parse("77777777-7777-7777-7777-777777777777")));
            Assert.That(launch.ExecutionMode, Is.EqualTo(CareerMatchExecutionMode.Fixture));
            Assert.That(launch.FixtureId, Is.EqualTo(CareerMatchTestData.FixtureId));
            Assert.That(launch.FixtureVersion, Is.EqualTo(CareerMatchTestData.FixtureVersion));
            Assert.That(launch.MatchSeed, Is.EqualTo(uint.MaxValue));
            Assert.That(launch.CompetitionId, Is.EqualTo("competition.custom.v7"));
            Assert.That(launch.ScheduleItemId, Is.EqualTo("schedule.custom.09"));
            Assert.That(launch.ImportanceBasisPoints, Is.EqualTo(9876));
            Assert.That(launch.Format, Is.SameAs(format));
            Assert.That(launch.PreMatchPriority, Is.EqualTo(CareerPreMatchPriority.StaminaControl));
        }

        [TestCase(0, 1, 1, 1)]
        [TestCase(2, 0, 1, 1)]
        [TestCase(2, 1, 0, 1)]
        [TestCase(2, 1, 1, 0)]
        [TestCase(3, 1, 1, 1)]
        public void Versions_RejectUnsupportedOrNonPositiveAxes(
            int contract, int content, int ruleset, int careerRandom)
        {
            Assert.Catch<ArgumentException>(() =>
                CareerMatchTestData.Versions(contract, content, ruleset, careerRandom));
        }

        [Test]
        public void Versions_RejectNonPositiveNullableMatchAxes()
        {
            Assert.Catch<ArgumentException>(() =>
                CareerMatchTestData.Versions(matchSimulationVersion: 0));
            Assert.Catch<ArgumentException>(() =>
                CareerMatchTestData.Versions(matchRandomAlgorithmVersion: -1));
        }

        [Test]
        public void Launch_EnforcesEveryExecutionModeVersionAndFixturePairing()
        {
            Assert.DoesNotThrow(() => CareerMatchTestData.Launch());
            Assert.DoesNotThrow(() => CareerMatchTestData.Launch(
                executionMode: CareerMatchExecutionMode.Direct,
                fixtureId: null,
                fixtureVersion: null));
            Assert.DoesNotThrow(() => CareerMatchTestData.Launch(
                versions: CareerMatchTestData.Versions(
                    matchSimulationVersion: 1,
                    matchRandomAlgorithmVersion: 2),
                executionMode: CareerMatchExecutionMode.QuickSimulation,
                fixtureId: null,
                fixtureVersion: null));

            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                fixtureId: null, fixtureVersion: null));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                versions: CareerMatchTestData.Versions(matchSimulationVersion: 1)));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                executionMode: CareerMatchExecutionMode.Direct));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                versions: CareerMatchTestData.Versions(matchSimulationVersion: 1),
                executionMode: CareerMatchExecutionMode.QuickSimulation,
                fixtureId: null,
                fixtureVersion: null));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                versions: CareerMatchTestData.Versions(
                    matchSimulationVersion: 1,
                    matchRandomAlgorithmVersion: 2),
                executionMode: CareerMatchExecutionMode.QuickSimulation));
        }

        [Test]
        public void Player_RejectsRangesUnknownPositionAndNullAttributes()
        {
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Player(
                "player.bad.jersey", 0, CareerMatchPlayerPosition.Setter, 1, 0, 1000));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Player(
                "player.bad.slot", 1, CareerMatchPlayerPosition.Setter, 7, 0, 1000));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Player(
                "player.bad.fatigue", 1, CareerMatchPlayerPosition.Setter, 1, -1, 1000));
            Assert.Catch<ArgumentException>(() => new CareerMatchPlayerLaunch(
                new PlayerId("player.bad.position"),
                1,
                (CareerMatchPlayerPosition)99,
                1,
                0,
                CareerMatchTestData.Attributes(1, 2, 3, 4, 5, 6, 7, 8)));
            Assert.Throws<ArgumentNullException>(() => new CareerMatchPlayerLaunch(
                new PlayerId("player.null.attributes"),
                1,
                CareerMatchPlayerPosition.Setter,
                1,
                0,
                null));
        }

        [Test]
        public void Team_RejectsDuplicateIdsJerseysSlotsAndInvalidTopology()
        {
            var players = CareerMatchTestData.Players("invalid.team", false);

            AssertTeamRejected(players, 1, CareerMatchTestData.Player(
                players[0].PlayerId.Value, 2, CareerMatchPlayerPosition.OutsideHitter, 2, 0, 2000));
            AssertTeamRejected(players, 1, CareerMatchTestData.Player(
                "invalid.team.other", 1, CareerMatchPlayerPosition.OutsideHitter, 2, 0, 2000));
            AssertTeamRejected(players, 1, CareerMatchTestData.Player(
                "invalid.team.other", 2, CareerMatchPlayerPosition.OutsideHitter, 1, 0, 2000));
            AssertTeamRejected(players, 0, CareerMatchTestData.Player(
                "invalid.team.extra.setter", 1, CareerMatchPlayerPosition.Setter, 1, 0, 2000));
        }

        [Test]
        public void Launch_RejectsTeamOrderDuplicateTeamAndGlobalPlayerIdsAndInvalidRanges()
        {
            var teams = CareerMatchTestData.Teams();
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                teams: new[] { teams[1], teams[0] }));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                teams: new[]
                {
                    teams[0],
                    CareerMatchTestData.Team(teams[0].TeamId, CareerMatchTeamSide.Away, "other", false)
                }));

            var awayPlayers = CareerMatchTestData.Players("global.away", false);
            awayPlayers[0] = CareerMatchTestData.Player(
                teams[0].Players[0].PlayerId.Value,
                1,
                CareerMatchPlayerPosition.Opposite,
                1,
                0,
                1000);
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                teams: new[]
                {
                    teams[0],
                    CareerMatchTestData.Team(
                        new TeamId("team.global.away"),
                        CareerMatchTeamSide.Away,
                        "ignored",
                        false,
                        awayPlayers)
                }));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(sessionId: Guid.Empty));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(importanceBasisPoints: 10001));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(competitionId: "bad id"));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Launch(
                priority: (CareerPreMatchPriority)99));
        }

        [Test]
        public void Launch_AllowsTheSameJerseyNumbersOnOpposingTeams()
        {
            var launch = CareerMatchTestData.Launch();

            Assert.That(launch.Teams[0].Players.Select(player => player.JerseyNumber),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(launch.Teams[1].Players.Select(player => player.JerseyNumber),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
        }

        [Test]
        public void Contracts_RejectDefaultStableIdsAndDigests()
        {
            Assert.Catch<ArgumentException>(() => new CareerMatchPlayerLaunch(
                default(PlayerId),
                1,
                CareerMatchPlayerPosition.Setter,
                1,
                0,
                CareerMatchTestData.Attributes(1, 2, 3, 4, 5, 6, 7, 8)));
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Team(
                default(TeamId),
                CareerMatchTeamSide.Home,
                "default.team",
                false));

            var launch = CareerMatchTestData.Launch();
            var facts = launch.Teams.SelectMany(team => team.Players)
                .Select(player => CareerMatchTestData.ZeroFacts(player.PlayerId))
                .ToArray();
            Assert.Catch<ArgumentException>(() => new CareerMatchFacts(
                launch.Versions,
                launch.SessionId,
                default(Sha256Digest),
                CareerMatchResultStatus.Completed,
                launch.Teams[0].TeamId,
                new[] { new CareerMatchSetScore(1, 25, 21, true) },
                46,
                facts,
                new Sha256Digest(new string('b', 64))));
            Assert.Catch<ArgumentException>(() => new CareerMatchFacts(
                launch.Versions,
                launch.SessionId,
                new Sha256Digest(new string('a', 64)),
                CareerMatchResultStatus.Completed,
                new TeamId? (default(TeamId)),
                new[] { new CareerMatchSetScore(1, 25, 21, true) },
                46,
                facts,
                new Sha256Digest(new string('b', 64))));
        }

        [Test]
        public void Facts_DefensivelyCopySetsAndAllTwelveDetailedPlayerFacts()
        {
            var launch = CareerMatchTestData.Launch();
            var sets = new[] { new CareerMatchSetScore(1, 25, 21, true) };
            var playerFacts = launch.Teams.SelectMany(team => team.Players)
                .Select(player => CareerMatchTestData.ZeroFacts(player.PlayerId))
                .ToArray();
            playerFacts[1] = new CareerMatchPlayerFacts(
                playerFacts[1].PlayerId,
                new CareerSpikeFacts(12, 7, 1),
                new CareerServeFacts(5, 1, 1),
                new CareerReceptionFacts(8, 3, 2, 1, 1, 1),
                new CareerDefenseFacts(6, 4),
                new CareerBlockFacts(3, 2, 1),
                new CareerMatchLoadFacts(44, 505000, 254000, 28, 9, 5400, 7200),
                new CareerStabilityFacts(5, 3, 1, 1, 2));
            var facts = new CareerMatchFacts(
                launch.Versions,
                launch.SessionId,
                new Sha256Digest(new string('a', 64)),
                CareerMatchResultStatus.Completed,
                launch.Teams[0].TeamId,
                sets,
                46,
                playerFacts,
                new Sha256Digest(new string('b', 64)));

            sets[0] = null;
            playerFacts[1] = null;

            Assert.That(facts.Sets.Single().HomePoints, Is.EqualTo(25));
            Assert.That(facts.PlayerFacts, Has.Count.EqualTo(12));
            Assert.That(facts.PlayerFacts[1].Spike.Attempts, Is.EqualTo(12));
            Assert.That(facts.PlayerFacts[1].Serve.Aces, Is.EqualTo(1));
            Assert.That(facts.PlayerFacts[1].Reception.Perfect, Is.EqualTo(3));
            Assert.That(facts.PlayerFacts[1].Defense.Successes, Is.EqualTo(4));
            Assert.That(facts.PlayerFacts[1].Block.EffectiveTouches, Is.EqualTo(2));
            Assert.That(facts.PlayerFacts[1].Load.MovementDistanceMillimeters, Is.EqualTo(254000));
            Assert.That(facts.PlayerFacts[1].Stability.ErrorStreakEpisodes, Is.EqualTo(1));
            Assert.That(facts.ContextDigest, Is.EqualTo(new Sha256Digest(new string('a', 64))));
            Assert.That(facts.ResultDigest, Is.EqualTo(new Sha256Digest(new string('b', 64))));
        }

        [Test]
        public void DetailedFacts_RejectContradictoryOrNegativeCounts()
        {
            Assert.Catch<ArgumentException>(() => new CareerSpikeFacts(1, 1, 1));
            Assert.Catch<ArgumentException>(() => new CareerServeFacts(1, 1, 1));
            Assert.Catch<ArgumentException>(() => new CareerReceptionFacts(2, 1, 1, 1, 0, 0));
            Assert.Catch<ArgumentException>(() => new CareerDefenseFacts(1, 2));
            Assert.Catch<ArgumentException>(() => new CareerBlockFacts(1, 2, 1));
            Assert.Catch<ArgumentException>(() =>
                new CareerMatchLoadFacts(1, 1, 1, 1, 2, 0, 0));
            Assert.Catch<ArgumentException>(() =>
                new CareerStabilityFacts(1, 1, 1, 0, 0));
            Assert.Catch<ArgumentException>(() =>
                new CareerStabilityFacts(0, 0, 0, 1, 1));
        }

        [Test]
        public void MatchFacts_RejectWrongCountDuplicateIdsAndWinnerStatusMismatch()
        {
            var launch = CareerMatchTestData.Launch();
            var facts = launch.Teams.SelectMany(team => team.Players)
                .Select(player => CareerMatchTestData.ZeroFacts(player.PlayerId))
                .ToArray();
            Assert.Catch<ArgumentException>(() => new CareerMatchFacts(
                launch.Versions,
                launch.SessionId,
                new Sha256Digest(new string('a', 64)),
                CareerMatchResultStatus.Completed,
                launch.Teams[0].TeamId,
                new[] { new CareerMatchSetScore(1, 25, 21, true) },
                46,
                facts.Take(11).ToArray(),
                new Sha256Digest(new string('b', 64))));

            facts[11] = CareerMatchTestData.ZeroFacts(facts[0].PlayerId);
            Assert.Catch<ArgumentException>(() => new CareerMatchFacts(
                launch.Versions,
                launch.SessionId,
                new Sha256Digest(new string('a', 64)),
                CareerMatchResultStatus.Completed,
                launch.Teams[0].TeamId,
                new[] { new CareerMatchSetScore(1, 25, 21, true) },
                46,
                facts,
                new Sha256Digest(new string('b', 64))));

            Assert.Catch<ArgumentException>(() => new CareerMatchFacts(
                launch.Versions,
                launch.SessionId,
                new Sha256Digest(new string('a', 64)),
                CareerMatchResultStatus.Abandoned,
                launch.Teams[0].TeamId,
                Array.Empty<CareerMatchSetScore>(),
                0,
                launch.Teams.SelectMany(team => team.Players)
                    .Select(player => CareerMatchTestData.ZeroFacts(player.PlayerId)).ToArray(),
                new Sha256Digest(new string('b', 64))));
        }

        private static void AssertTeamRejected(
            CareerMatchPlayerLaunch[] source,
            int replaceIndex,
            CareerMatchPlayerLaunch replacement)
        {
            var players = (CareerMatchPlayerLaunch[])source.Clone();
            players[replaceIndex] = replacement;
            Assert.Catch<ArgumentException>(() => CareerMatchTestData.Team(
                new TeamId("team.invalid"),
                CareerMatchTeamSide.Home,
                "ignored",
                false,
                players));
        }
    }
}
