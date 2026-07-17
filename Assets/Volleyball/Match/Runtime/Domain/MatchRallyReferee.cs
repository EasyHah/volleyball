using System;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Domain
{
    public readonly struct RallyOutcome
    {
        public RallyOutcome(TeamSide winner, bool isFault, string reason)
        {
            Winner = winner;
            IsFault = isFault;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public TeamSide Winner { get; }

        public bool IsFault { get; }

        public string Reason { get; }
    }

    public static class MatchRallyReferee
    {
        public static RallyOutcome ResolveGroundLanding(
            TeamSide finalTouchSide,
            SimVector3 landingPoint,
            float halfWidth,
            float halfLength)
        {
            ValidateSide(finalTouchSide);
            ValidateCourt(halfWidth, halfLength);
            if (!landingPoint.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(landingPoint));
            }

            var inBounds = Math.Abs(landingPoint.X) <= halfWidth &&
                           Math.Abs(landingPoint.Z) <= halfLength;
            var opponentHalf = finalTouchSide == TeamSide.Home
                ? landingPoint.Z > 0f
                : landingPoint.Z < 0f;
            if (inBounds && opponentHalf)
            {
                return new RallyOutcome(finalTouchSide, false, "legal opponent-court landing");
            }

            return new RallyOutcome(OpponentOf(finalTouchSide), true, inBounds
                ? "own-court landing"
                : "out-of-bounds landing");
        }

        public static RallyOutcome? ResolveNetCrossing(
            TeamSide finalTouchSide,
            SimVector3 crossingPoint,
            float antennaHalfWidth,
            float netHeight)
        {
            ValidateSide(finalTouchSide);
            ValidateCourt(antennaHalfWidth, netHeight);
            if (!crossingPoint.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(crossingPoint));
            }

            if (Math.Abs(crossingPoint.X) <= antennaHalfWidth)
            {
                return null;
            }

            return new RallyOutcome(OpponentOf(finalTouchSide), true, "illegal net crossing");
        }

        private static TeamSide OpponentOf(TeamSide side)
        {
            return side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        }

        private static void ValidateSide(TeamSide side)
        {
            if (!Enum.IsDefined(typeof(TeamSide), side))
            {
                throw new ArgumentOutOfRangeException(nameof(side));
            }
        }

        private static void ValidateCourt(float first, float second)
        {
            if (!IsPositiveFinite(first) || !IsPositiveFinite(second))
            {
                throw new ArgumentOutOfRangeException("Court dimensions must be finite and positive.");
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
