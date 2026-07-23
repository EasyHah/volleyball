using System;
using System.Collections.Generic;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    public sealed class CareerFirstMatchLaunchFactoryV1 : ICareerFirstMatchLaunchFactory
    {
        public const string FixtureId = "fixture.career.u1w1.6v6";
        public const int FixtureVersion = 1;
        public const string CompetitionId = "competition.university.v1";
        public const string ScheduleItemId = "schedule.u1w1.match.01";

        public CareerMatchLaunch Create(CareerFirstMatchLaunchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var npcJerseys = HomeNpcJerseys(request.ProtagonistJerseyNumber);
            var home = new CareerMatchTeamLaunch(
                request.HomeTeamId,
                CareerMatchTeamSide.Home,
                new[]
                {
                    Player(
                        "dynamic.home.opposite",
                        npcJerseys[0],
                        CareerMatchPlayerPosition.Opposite,
                        1,
                        10,
                        6100),
                    new CareerMatchPlayerLaunch(
                        request.ProtagonistPlayerId,
                        request.ProtagonistJerseyNumber,
                        CareerMatchPlayerPosition.OutsideHitter,
                        2,
                        request.ProtagonistFatigue,
                        request.ProtagonistAttributes),
                    Player(
                        "dynamic.home.middle",
                        npcJerseys[1],
                        CareerMatchPlayerPosition.MiddleBlocker,
                        3,
                        20,
                        6300),
                    Player(
                        "dynamic.home.setter",
                        npcJerseys[2],
                        CareerMatchPlayerPosition.Setter,
                        4,
                        50,
                        6400),
                    Player(
                        "dynamic.home.outside.b",
                        npcJerseys[3],
                        CareerMatchPlayerPosition.OutsideHitter,
                        5,
                        75,
                        6500),
                    Player(
                        "dynamic.home.libero",
                        npcJerseys[4],
                        CareerMatchPlayerPosition.Libero,
                        6,
                        100,
                        6600)
                });
            var away = new CareerMatchTeamLaunch(
                new TeamId("team.university.rival"),
                CareerMatchTeamSide.Away,
                new[]
                {
                    Player("dynamic.away.opposite", 1, CareerMatchPlayerPosition.Opposite, 1, 10, 6100),
                    Player("dynamic.away.outside.a", 2, CareerMatchPlayerPosition.OutsideHitter, 2, 11, 6200),
                    Player("dynamic.away.middle", 3, CareerMatchPlayerPosition.MiddleBlocker, 3, 20, 6300),
                    Player("dynamic.away.setter", 4, CareerMatchPlayerPosition.Setter, 4, 50, 6400),
                    Player("dynamic.away.outside.b", 5, CareerMatchPlayerPosition.OutsideHitter, 5, 75, 6500),
                    Player("dynamic.away.libero", 6, CareerMatchPlayerPosition.Libero, 6, 100, 6600)
                });

            return new CareerMatchLaunch(
                request.Versions,
                request.SessionId,
                CareerMatchExecutionMode.Fixture,
                FixtureId,
                FixtureVersion,
                request.MatchSeed,
                CompetitionId,
                ScheduleItemId,
                7500,
                new CareerMatchFormat("indoor_6v6", 6, 1, 25, 2),
                MapPriority(request.PreMatchPriority),
                new[] { home, away });
        }

        private static int[] HomeNpcJerseys(int protagonistJersey)
        {
            if (protagonistJersey < 1 || protagonistJersey > 99)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(protagonistJersey),
                    protagonistJersey,
                    "Jersey number must be in [1, 99].");
            }

            var values = new List<int>(5);
            for (var candidate = 1; candidate <= 99 && values.Count < 5; candidate++)
            {
                if (candidate != protagonistJersey)
                {
                    values.Add(candidate);
                }
            }

            return values.ToArray();
        }

        private static CareerMatchPlayerLaunch Player(
            string id,
            int jersey,
            CareerMatchPlayerPosition position,
            int rotation,
            int fatigue,
            int abilityStart)
        {
            return new CareerMatchPlayerLaunch(
                new PlayerId(id),
                jersey,
                position,
                rotation,
                fatigue,
                Attributes(abilityStart));
        }

        private static CareerPlayerAttributes Attributes(int start)
        {
            return new CareerPlayerAttributes(
                Progress(start, 101),
                Progress(start + 1, 202),
                Progress(start + 2, 303),
                Progress(start + 3, 404),
                Progress(start + 4, 505),
                Progress(start + 5, 606),
                Progress(start + 6, 707),
                Progress(start + 7, 808));
        }

        private static CareerAttributeProgress Progress(int ability, long growth)
        {
            return new CareerAttributeProgress(ability, growth);
        }

        private static CareerPreMatchPriority MapPriority(CareerMatchPriority priority)
        {
            switch (priority)
            {
                case CareerMatchPriority.AttackFirst:
                    return CareerPreMatchPriority.AttackFirst;
                case CareerMatchPriority.FirstContactSecurity:
                    return CareerPreMatchPriority.FirstContactSecurity;
                case CareerMatchPriority.StaminaControl:
                    return CareerPreMatchPriority.StaminaControl;
                default:
                    throw new ArgumentOutOfRangeException(nameof(priority), priority, null);
            }
        }
    }
}
