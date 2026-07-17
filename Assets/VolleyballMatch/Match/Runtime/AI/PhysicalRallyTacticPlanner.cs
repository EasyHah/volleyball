using System;
using VolleyballMatch.Domain.Prototype;

namespace VolleyballMatch.AI
{
    public enum SetRoute
    {
        LeftPin,
        MiddleQuick,
        RightPin,
        BackSet
    }

    public enum SpikeRoute
    {
        Line,
        CrossCourt,
        DeepSeam,
        RollShot
    }

    public readonly struct TeamRallyTactic : IEquatable<TeamRallyTactic>
    {
        public TeamRallyTactic(
            SetRoute setRoute,
            SpikeRoute spikeRoute,
            CourtPoint setterPosition,
            CourtPoint attackerPosition,
            CourtPoint defenderPosition,
            float setFlightSeconds,
            float attackFlightSeconds)
        {
            SetRoute = setRoute;
            SpikeRoute = spikeRoute;
            SetterPosition = setterPosition;
            AttackerPosition = attackerPosition;
            DefenderPosition = defenderPosition;
            SetFlightSeconds = setFlightSeconds;
            AttackFlightSeconds = attackFlightSeconds;
        }

        public SetRoute SetRoute { get; }

        public SpikeRoute SpikeRoute { get; }

        public CourtPoint SetterPosition { get; }

        public CourtPoint AttackerPosition { get; }

        public CourtPoint DefenderPosition { get; }

        public float SetFlightSeconds { get; }

        public float AttackFlightSeconds { get; }

        public bool Equals(TeamRallyTactic other)
        {
            return SetRoute == other.SetRoute &&
                   SpikeRoute == other.SpikeRoute &&
                   SetterPosition.Equals(other.SetterPosition) &&
                   AttackerPosition.Equals(other.AttackerPosition) &&
                   DefenderPosition.Equals(other.DefenderPosition) &&
                   SetFlightSeconds.Equals(other.SetFlightSeconds) &&
                   AttackFlightSeconds.Equals(other.AttackFlightSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is TeamRallyTactic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)SetRoute;
                hashCode = (hashCode * 397) ^ (int)SpikeRoute;
                hashCode = (hashCode * 397) ^ SetterPosition.GetHashCode();
                hashCode = (hashCode * 397) ^ AttackerPosition.GetHashCode();
                hashCode = (hashCode * 397) ^ DefenderPosition.GetHashCode();
                hashCode = (hashCode * 397) ^ SetFlightSeconds.GetHashCode();
                return (hashCode * 397) ^ AttackFlightSeconds.GetHashCode();
            }
        }
    }

    public readonly struct PhysicalRallyTactics : IEquatable<PhysicalRallyTactics>
    {
        public PhysicalRallyTactics(TeamRallyTactic blue, TeamRallyTactic orange)
        {
            Blue = blue;
            Orange = orange;
        }

        public TeamRallyTactic Blue { get; }

        public TeamRallyTactic Orange { get; }

        public bool Equals(PhysicalRallyTactics other)
        {
            return Blue.Equals(other.Blue) && Orange.Equals(other.Orange);
        }

        public override bool Equals(object obj)
        {
            return obj is PhysicalRallyTactics other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Blue.GetHashCode() * 397) ^ Orange.GetHashCode();
            }
        }
    }

    public sealed class PhysicalRallyTacticPlanner
    {
        private readonly int _seed;

        public PhysicalRallyTacticPlanner(int seed)
        {
            _seed = seed;
        }

        public PhysicalRallyTactics Create(int revision)
        {
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            var random = new Random(unchecked(_seed + (revision * 104729)));
            var blueSet = (SetRoute)random.Next(0, 4);
            var blueSpike = (SpikeRoute)random.Next(0, 4);
            var orangeSet = (SetRoute)random.Next(0, 4);
            var orangeSpike = (SpikeRoute)random.Next(0, 4);
            var blueAttack = AttackPosition(blueSet, -1f);
            var orangeAttack = AttackPosition(orangeSet, 1f);
            var blueDefense = DefensePosition(orangeSpike, orangeAttack, -1f);
            var orangeDefense = DefensePosition(blueSpike, blueAttack, 1f);

            return new PhysicalRallyTactics(
                CreateTeam(blueSet, blueSpike, blueAttack, blueDefense, -1f),
                CreateTeam(orangeSet, orangeSpike, orangeAttack, orangeDefense, 1f));
        }

        private static TeamRallyTactic CreateTeam(
            SetRoute setRoute,
            SpikeRoute spikeRoute,
            CourtPoint attackPosition,
            CourtPoint defensePosition,
            float sideSign)
        {
            var setterX = setRoute switch
            {
                SetRoute.LeftPin => -0.45f,
                SetRoute.RightPin => 0.45f,
                SetRoute.BackSet => 0.65f,
                _ => 0f
            };
            var setFlight = setRoute switch
            {
                SetRoute.MiddleQuick => 0.55f,
                SetRoute.BackSet => 0.70f,
                _ => 0.80f
            };
            var attackFlight = spikeRoute == SpikeRoute.RollShot ? 0.60f : 0.45f;
            return new TeamRallyTactic(
                setRoute,
                spikeRoute,
                new CourtPoint(setterX, sideSign * 3.35f),
                attackPosition,
                defensePosition,
                setFlight,
                attackFlight);
        }

        private static CourtPoint AttackPosition(SetRoute route, float sideSign)
        {
            return route switch
            {
                SetRoute.LeftPin => new CourtPoint(-3.15f, sideSign * 2.45f),
                SetRoute.MiddleQuick => new CourtPoint(-0.35f, sideSign * 2.05f),
                SetRoute.RightPin => new CourtPoint(3.15f, sideSign * 2.45f),
                SetRoute.BackSet => new CourtPoint(1.65f, sideSign * 4.15f),
                _ => throw new ArgumentOutOfRangeException(nameof(route), route, null)
            };
        }

        private static CourtPoint DefensePosition(
            SpikeRoute route,
            CourtPoint attacker,
            float sideSign)
        {
            var x = route switch
            {
                SpikeRoute.Line => attacker.X,
                SpikeRoute.CrossCourt => -attacker.X * 0.78f,
                SpikeRoute.DeepSeam => 0f,
                SpikeRoute.RollShot => -attacker.X * 0.35f,
                _ => throw new ArgumentOutOfRangeException(nameof(route), route, null)
            };
            var z = sideSign * (route == SpikeRoute.RollShot ? 4.05f : 5.25f);
            return new CourtPoint(Math.Max(-3.6f, Math.Min(3.6f, x)), z);
        }
    }
}
