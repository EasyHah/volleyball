using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.Domain.Prototype
{
    public readonly struct TeamCourtFrame : IEquatable<TeamCourtFrame>
    {
        public TeamCourtFrame(TeamId team)
        {
            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(nameof(team));
            }

            Team = team;
        }

        public TeamId Team { get; }

        public int WorldDepthSign => Team == TeamId.Blue ? -1 : 1;

        public SimVector3 ToLocal(SimVector3 world)
        {
            return new SimVector3(world.X, world.Y, ToLocalDepth(world.Z));
        }

        public SimVector3 ToWorld(SimVector3 local)
        {
            return new SimVector3(local.X, local.Y, ToWorldDepth(local.Z));
        }

        public float ToLocalDepth(float worldDepth)
        {
            return -WorldDepthSign * worldDepth;
        }

        public float ToWorldDepth(float localDepth)
        {
            return -WorldDepthSign * localDepth;
        }

        public bool Equals(TeamCourtFrame other)
        {
            return Team == other.Team;
        }

        public override bool Equals(object obj)
        {
            return obj is TeamCourtFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Team;
        }
    }
}
