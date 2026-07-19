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
        Defender,
        Opposite,
        OutsideHitter,
        MiddleBlocker
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
            : this(team, role, (int)role)
        {
        }

        public PlayerId(TeamId team, PlayerRole role, int rosterSlot)
        {
            if (rosterSlot < 0 || rosterSlot > 13)
            {
                throw new ArgumentOutOfRangeException(nameof(rosterSlot));
            }

            Team = team;
            Role = role;
            RosterSlot = rosterSlot;
        }

        public TeamId Team { get; }

        public PlayerRole Role { get; }

        public int RosterSlot { get; }

        public bool Equals(PlayerId other)
        {
            return Team == other.Team && Role == other.Role && RosterSlot == other.RosterSlot;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var legacyHashCode = ((int)Team * 397) ^ (int)Role;
                return RosterSlot == (int)Role
                    ? legacyHashCode
                    : (legacyHashCode * 397) ^ RosterSlot;
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
