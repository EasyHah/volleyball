using System;

namespace Volleyball.Domain.Prototype
{
    public enum TeamId
    {
        Blue,
        Orange
    }

    public enum PlayerRole
    {
        Setter,
        Attacker,
        Defender
    }

    public enum RallyActionKind
    {
        Serve,
        Receive,
        Set,
        Approach,
        Spike,
        Block,
        Dig
    }

    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(TeamId team, PlayerRole role)
        {
            Team = team;
            Role = role;
        }

        public TeamId Team { get; }

        public PlayerRole Role { get; }

        public bool Equals(PlayerId other)
        {
            return Team == other.Team && Role == other.Role;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Team * 397) ^ (int)Role;
            }
        }
    }

    public readonly struct CourtPoint : IEquatable<CourtPoint>
    {
        public CourtPoint(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float X { get; }

        public float Z { get; }

        public bool Equals(CourtPoint other)
        {
            return X.Equals(other.X) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is CourtPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Z.GetHashCode();
            }
        }
    }
}
