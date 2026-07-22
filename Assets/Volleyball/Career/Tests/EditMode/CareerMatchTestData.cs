using System;
using System.Collections.Generic;
using System.Linq;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.EditModeTests
{
    internal static class CareerMatchTestData
    {
        public const string FixtureId = "fixture.career.u1w1.6v6";
        public const int FixtureVersion = 1;
        public const uint MatchSeed = 25649701u;
        public const string CompetitionId = "competition.university.v1";
        public const string ScheduleItemId = "schedule.u1w1.match.01";
        public static readonly Guid SessionId =
            Guid.Parse("55555555-5555-5555-5555-555555555555");

        public static CareerMatchVersions Versions(
            int contractVersion = 2,
            int contentVersion = 1,
            int rulesetVersion = 1,
            int careerRandomAlgorithmVersion = 1,
            int? matchSimulationVersion = null,
            int? matchRandomAlgorithmVersion = null)
        {
            return new CareerMatchVersions(
                contractVersion,
                contentVersion,
                rulesetVersion,
                careerRandomAlgorithmVersion,
                matchSimulationVersion,
                matchRandomAlgorithmVersion);
        }

        public static CareerMatchLaunch Launch(
            CareerMatchVersions versions = null,
            Guid? sessionId = null,
            CareerMatchExecutionMode executionMode = CareerMatchExecutionMode.Fixture,
            string fixtureId = FixtureId,
            int? fixtureVersion = FixtureVersion,
            uint matchSeed = MatchSeed,
            string competitionId = CompetitionId,
            string scheduleItemId = ScheduleItemId,
            int importanceBasisPoints = 7500,
            CareerMatchFormat format = null,
            CareerPreMatchPriority priority = CareerPreMatchPriority.AttackFirst,
            IReadOnlyList<CareerMatchTeamLaunch> teams = null)
        {
            return new CareerMatchLaunch(
                versions ?? Versions(),
                sessionId ?? SessionId,
                executionMode,
                fixtureId,
                fixtureVersion,
                matchSeed,
                competitionId,
                scheduleItemId,
                importanceBasisPoints,
                format ?? Format(),
                priority,
                teams ?? Teams());
        }

        public static CareerMatchFormat Format(
            string kind = "indoor_6v6",
            int teamSize = 6,
            int setsToWin = 1,
            int target = 25,
            int lead = 2)
        {
            return new CareerMatchFormat(kind, teamSize, setsToWin, target, lead);
        }

        public static CareerMatchTeamLaunch[] Teams(
            string homeTeamId = "team.university.first",
            string awayTeamId = "team.university.rival",
            string homePrefix = "dynamic.home",
            string awayPrefix = "dynamic.away")
        {
            return new[]
            {
                Team(new TeamId(homeTeamId), CareerMatchTeamSide.Home, homePrefix, true),
                Team(new TeamId(awayTeamId), CareerMatchTeamSide.Away, awayPrefix, false)
            };
        }

        public static CareerMatchTeamLaunch Team(
            TeamId teamId,
            CareerMatchTeamSide side,
            string prefix,
            bool includeProtagonist,
            IReadOnlyList<CareerMatchPlayerLaunch> players = null)
        {
            return new CareerMatchTeamLaunch(
                teamId,
                side,
                players ?? Players(prefix, includeProtagonist));
        }

        public static CareerMatchPlayerLaunch[] Players(string prefix, bool includeProtagonist)
        {
            return new[]
            {
                Player(prefix + ".opposite", 1, CareerMatchPlayerPosition.Opposite, 1, 10, 6100),
                Player(includeProtagonist ? "player.career.protagonist" : prefix + ".outside.a",
                    2, CareerMatchPlayerPosition.OutsideHitter, 2, includeProtagonist ? 12 : 11,
                    includeProtagonist ? 7100 : 6200),
                Player(prefix + ".middle", 3, CareerMatchPlayerPosition.MiddleBlocker, 3, 20, 6300),
                Player(prefix + ".setter", 4, CareerMatchPlayerPosition.Setter, 4, 50, 6400),
                Player(prefix + ".outside.b", 5, CareerMatchPlayerPosition.OutsideHitter, 5, 75, 6500),
                Player(prefix + ".libero", 6, CareerMatchPlayerPosition.Libero, 6, 100, 6600)
            };
        }

        public static CareerMatchPlayerLaunch Player(
            string id,
            int jersey,
            CareerMatchPlayerPosition position,
            int rotation,
            int fatigue,
            int abilityStart)
        {
            var attributes = id == "player.career.protagonist"
                ? Attributes(7123, 6234, 7345, 6456, 7567, 6678, 7789, 6890)
                : Attributes(
                    abilityStart,
                    abilityStart + 1,
                    abilityStart + 2,
                    abilityStart + 3,
                    abilityStart + 4,
                    abilityStart + 5,
                    abilityStart + 6,
                    abilityStart + 7);
            return new CareerMatchPlayerLaunch(
                new PlayerId(id), jersey, position, rotation, fatigue, attributes);
        }

        public static CareerPlayerAttributes Attributes(
            int spike,
            int serve,
            int reception,
            int defense,
            int block,
            int movement,
            int jump,
            int stamina)
        {
            return new CareerPlayerAttributes(
                Progress(spike, 101),
                Progress(serve, 202),
                Progress(reception, 303),
                Progress(defense, 404),
                Progress(block, 505),
                Progress(movement, 606),
                Progress(jump, 707),
                Progress(stamina, 808));
        }

        public static CareerMatchPlayerFacts ZeroFacts(PlayerId playerId)
        {
            return new CareerMatchPlayerFacts(
                playerId,
                new CareerSpikeFacts(0, 0, 0),
                new CareerServeFacts(0, 0, 0),
                new CareerReceptionFacts(0, 0, 0, 0, 0, 0),
                new CareerDefenseFacts(0, 0),
                new CareerBlockFacts(0, 0, 0),
                new CareerMatchLoadFacts(0, 0, 0, 0, 0, 0, 0),
                new CareerStabilityFacts(0, 0, 0, 0, 0));
        }

        public static CareerMatchFacts Facts(CareerMatchLaunch launch = null)
        {
            launch = launch ?? Launch();
            var playerFacts = launch.Teams
                .SelectMany(team => team.Players)
                .Select(player => ZeroFacts(player.PlayerId))
                .ToArray();
            return new CareerMatchFacts(
                launch.Versions,
                launch.SessionId,
                new Sha256Digest(new string('a', 64)),
                CareerMatchResultStatus.Completed,
                launch.Teams[0].TeamId,
                new[] { new CareerMatchSetScore(1, 25, 21, true) },
                46,
                playerFacts,
                new Sha256Digest(new string('b', 64)));
        }

        private static CareerAttributeProgress Progress(int ability, long growth)
        {
            return new CareerAttributeProgress(ability, growth);
        }
    }
}
