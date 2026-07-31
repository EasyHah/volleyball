using System;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.MatchIntegration
{
    /// <summary>Creates the initial formal V5 roster from a complete V5 Career profile.</summary>
    public sealed class CareerFirstMatchLaunchFactoryV5
    {
        public CareerMatchLaunchV5 Create(CareerPlayerProfileV5 protagonist,
            TeamId homeTeamId, int fatigue, Guid sessionId, uint matchSeed)
        {
            if (protagonist == null) throw new ArgumentNullException(nameof(protagonist));
            var home = new CareerMatchTeamLaunchV5(homeTeamId, homeTeamId.Value,
                CareerMatchTeamSide.Home, new[]
                {
                    Player("v5.home.opp", "Home Opp", 11, CareerMatchPlayerPosition.Opposite, 1, 6100),
                    new CareerMatchPlayerLaunchV5(protagonist.PlayerId, protagonist.DisplayName,
                        protagonist.JerseyNumber, CareerMatchPlayerPosition.OutsideHitter, 2, fatigue,
                        protagonist.DominantHand, protagonist.Bases),
                    Player("v5.home.middle", "Home Middle", 12, CareerMatchPlayerPosition.MiddleBlocker, 3, 6300),
                    Player("v5.home.setter", "Home Setter", 13, CareerMatchPlayerPosition.Setter, 4, 6400),
                    Player("v5.home.outside", "Home Outside", 14, CareerMatchPlayerPosition.OutsideHitter, 5, 6500),
                    Player("v5.home.libero", "Home Libero", 15, CareerMatchPlayerPosition.Libero, 6, 6600)
                });
            var away = new CareerMatchTeamLaunchV5(new TeamId("team.university.rival.v5"), "Rival",
                CareerMatchTeamSide.Away, new[]
                {
                    Player("v5.away.opp", "Away Opp", 21, CareerMatchPlayerPosition.Opposite, 1, 6100),
                    Player("v5.away.outside.a", "Away Outside A", 22, CareerMatchPlayerPosition.OutsideHitter, 2, 6200),
                    Player("v5.away.middle", "Away Middle", 23, CareerMatchPlayerPosition.MiddleBlocker, 3, 6300),
                    Player("v5.away.setter", "Away Setter", 24, CareerMatchPlayerPosition.Setter, 4, 6400),
                    Player("v5.away.outside.b", "Away Outside B", 25, CareerMatchPlayerPosition.OutsideHitter, 5, 6500),
                    Player("v5.away.libero", "Away Libero", 26, CareerMatchPlayerPosition.Libero, 6, 6600)
                });
            return new CareerMatchLaunchV5(sessionId, matchSeed, new[] { home, away });
        }

        private static CareerMatchPlayerLaunchV5 Player(string id, string name, int jersey,
            CareerMatchPlayerPosition position, int rotationSlot, int ability)
        {
            return new CareerMatchPlayerLaunchV5(new PlayerId(id), name, jersey, position,
                rotationSlot, 0, DominantHandV5.Right, new CareerBaseAttributesV5(
                    ability, 1900, ability, ability, ability, ability,
                    ability, ability, ability, ability, ability, ability));
        }
    }
}
