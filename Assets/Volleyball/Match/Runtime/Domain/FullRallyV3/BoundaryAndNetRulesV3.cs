using Volleyball.Domain;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public static class BoundaryAndNetRulesV3
    {
        public static RallyOutcome ResolveGroundLanding(
            TeamSide finalTouchSide,
            SimVector3 landingPoint,
            float halfWidth,
            float halfLength)
        {
            return MatchRallyReferee.ResolveGroundLanding(finalTouchSide, landingPoint, halfWidth, halfLength);
        }

        public static RallyOutcome? ResolveNetCrossing(
            TeamSide finalTouchSide,
            SimVector3 crossingPoint,
            float antennaHalfWidth,
            float netHeight)
        {
            return MatchRallyReferee.ResolveNetCrossing(finalTouchSide, crossingPoint, antennaHalfWidth, netHeight);
        }
    }
}
