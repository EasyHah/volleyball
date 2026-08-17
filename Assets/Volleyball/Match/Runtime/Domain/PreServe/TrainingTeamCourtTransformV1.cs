using System;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.PreServe
{
    /// <summary>
    /// Converts a team's net-facing local coordinates to the shared court.
    /// Away is a 180-degree point rotation of Home around court centre.
    /// </summary>
    public static class TrainingTeamCourtTransformV1
    {
        public static SimVector3 ToWorld(TeamSide side, SimVector3 local)
        {
            return side switch
            {
                TeamSide.Home => new SimVector3(local.X, local.Y, -local.Z),
                TeamSide.Away => new SimVector3(-local.X, local.Y, local.Z),
                _ => throw new ArgumentOutOfRangeException(nameof(side))
            };
        }

        public static SimVector3 ToLocal(TeamSide side, SimVector3 world)
        {
            return side switch
            {
                TeamSide.Home => new SimVector3(world.X, world.Y, -world.Z),
                TeamSide.Away => new SimVector3(-world.X, world.Y, world.Z),
                _ => throw new ArgumentOutOfRangeException(nameof(side))
            };
        }
    }
}
