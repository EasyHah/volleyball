using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum RallyCommitmentStateV3
    {
        Uncommitted,
        Preparing,
        Committed,
        Recovering
    }

    public readonly struct BallWorldSnapshotV3
    {
        public BallWorldSnapshotV3(
            SimVector3 position,
            SimVector3 velocity,
            SimVector3 spin,
            float radius,
            float physicalTimeSeconds)
        {
            RequireFinite(position, nameof(position));
            RequireFinite(velocity, nameof(velocity));
            RequireFinite(spin, nameof(spin));
            Radius = RequireNonNegativeFinite(radius, nameof(radius));
            PhysicalTimeSeconds = RequireNonNegativeFinite(physicalTimeSeconds, nameof(physicalTimeSeconds));
            Position = position;
            Velocity = velocity;
            Spin = spin;
        }

        public SimVector3 Position { get; }

        public SimVector3 Velocity { get; }

        public SimVector3 Spin { get; }

        public float Radius { get; }

        public float PhysicalTimeSeconds { get; }

        internal static void RequireFinite(SimVector3 value, string paramName)
        {
            if (!value.IsFinite)
            {
                throw new ArgumentOutOfRangeException(paramName, "Vector must be finite.");
            }
        }

        internal static float RequireNonNegativeFinite(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(paramName, "Value must be finite and non-negative.");
            }

            return value;
        }
    }

    public sealed class PlayerWorldSnapshotV3
    {
        public PlayerWorldSnapshotV3(
            PlayerId playerId,
            TeamSide side,
            PlayerPosition registeredPosition,
            SimVector3 position,
            SimVector3 velocity,
            SimVector3 facing,
            string pose,
            RallyCommitmentStateV3 commitment,
            float recoverySeconds)
        {
            PlayerId = RequirePlayerId(playerId, nameof(playerId));
            Side = RequireDefinedEnum(side, nameof(side));
            RegisteredPosition = RequireDefinedEnum(registeredPosition, nameof(registeredPosition));
            BallWorldSnapshotV3.RequireFinite(position, nameof(position));
            BallWorldSnapshotV3.RequireFinite(velocity, nameof(velocity));
            BallWorldSnapshotV3.RequireFinite(facing, nameof(facing));
            Pose = RequireText(pose, nameof(pose));
            Commitment = RequireDefinedEnum(commitment, nameof(commitment));
            RecoverySeconds = BallWorldSnapshotV3.RequireNonNegativeFinite(recoverySeconds, nameof(recoverySeconds));
            Position = position;
            Velocity = velocity;
            Facing = facing;
        }

        public PlayerId PlayerId { get; }

        public TeamSide Side { get; }

        public PlayerPosition RegisteredPosition { get; }

        public SimVector3 Position { get; }

        public SimVector3 Velocity { get; }

        public SimVector3 Facing { get; }

        public string Pose { get; }

        public RallyCommitmentStateV3 Commitment { get; }

        public float RecoverySeconds { get; }

        internal static PlayerId RequirePlayerId(PlayerId value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value.Value))
            {
                throw new ArgumentException("Player ID is required.", paramName);
            }

            return value;
        }

        internal static T RequireDefinedEnum<T>(T value, string paramName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(paramName, "Enum value is not supported.");
            }

            return value;
        }

        internal static string RequireText(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Text is required.", paramName);
            }

            return value;
        }
    }

    public sealed class RallyWorldSnapshotV3
    {
        public RallyWorldSnapshotV3(
            BallWorldSnapshotV3 ball,
            IReadOnlyList<PlayerWorldSnapshotV3> players,
            TouchSequenceStateV3 touchSequence,
            OnCourtEligibilitySnapshot eligibility,
            CourtConfigurationV3 court,
            AcceptedRuleEventV3 latestEvent,
            long eventSequence)
        {
            ValidateBall(ball);
            Players = new ReadOnlyCollection<PlayerWorldSnapshotV3>(CopyPlayers(players));
            TouchSequence = RequireReference(touchSequence, nameof(touchSequence));
            Eligibility = RequireReference(eligibility, nameof(eligibility));
            Court = RequireReference(court, nameof(court));
            LatestEvent = RequireReference(latestEvent, nameof(latestEvent));
            if (eventSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(eventSequence), "Event sequence must be non-negative.");
            }

            Ball = ball;
            EventSequence = eventSequence;
        }

        public BallWorldSnapshotV3 Ball { get; }

        public IReadOnlyList<PlayerWorldSnapshotV3> Players { get; }

        public TouchSequenceStateV3 TouchSequence { get; }

        public OnCourtEligibilitySnapshot Eligibility { get; }

        public CourtConfigurationV3 Court { get; }

        public AcceptedRuleEventV3 LatestEvent { get; }

        public long EventSequence { get; }

        private static PlayerWorldSnapshotV3[] CopyPlayers(IReadOnlyList<PlayerWorldSnapshotV3> players)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            if (players.Count != 12)
            {
                throw new ArgumentException("Exactly twelve players are required.", nameof(players));
            }

            var copy = new PlayerWorldSnapshotV3[players.Count];
            var playerIds = new HashSet<PlayerId>();
            var homeCount = 0;
            var awayCount = 0;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                if (player == null)
                {
                    throw new ArgumentException("Player facts are required.", nameof(players));
                }

                PlayerWorldSnapshotV3.RequirePlayerId(player.PlayerId, nameof(players));
                PlayerWorldSnapshotV3.RequireDefinedEnum(player.Side, nameof(players));
                PlayerWorldSnapshotV3.RequireDefinedEnum(player.RegisteredPosition, nameof(players));
                BallWorldSnapshotV3.RequireFinite(player.Position, nameof(players));
                BallWorldSnapshotV3.RequireFinite(player.Velocity, nameof(players));
                BallWorldSnapshotV3.RequireFinite(player.Facing, nameof(players));
                PlayerWorldSnapshotV3.RequireText(player.Pose, nameof(players));
                PlayerWorldSnapshotV3.RequireDefinedEnum(player.Commitment, nameof(players));
                BallWorldSnapshotV3.RequireNonNegativeFinite(player.RecoverySeconds, nameof(players));
                if (!playerIds.Add(player.PlayerId))
                {
                    throw new ArgumentException("Player IDs must be distinct.", nameof(players));
                }

                if (player.Side == TeamSide.Home)
                {
                    homeCount++;
                }
                else
                {
                    awayCount++;
                }

                copy[index] = player;
            }

            if (homeCount != 6 || awayCount != 6)
            {
                throw new ArgumentException("Exactly six players per side are required.", nameof(players));
            }

            return copy;
        }

        private static void ValidateBall(BallWorldSnapshotV3 ball)
        {
            BallWorldSnapshotV3.RequireFinite(ball.Position, nameof(ball));
            BallWorldSnapshotV3.RequireFinite(ball.Velocity, nameof(ball));
            BallWorldSnapshotV3.RequireFinite(ball.Spin, nameof(ball));
            BallWorldSnapshotV3.RequireNonNegativeFinite(ball.Radius, nameof(ball));
            BallWorldSnapshotV3.RequireNonNegativeFinite(ball.PhysicalTimeSeconds, nameof(ball));
        }

        private static T RequireReference<T>(T value, string paramName) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }

            return value;
        }
    }

    public sealed class TouchSequenceStateV3
    {
        private TouchSequenceStateV3()
        {
        }

        public static TouchSequenceStateV3 Initial { get; } = new TouchSequenceStateV3();
    }

    public sealed class CourtConfigurationV3
    {
        public CourtConfigurationV3()
        {
        }
    }

    public sealed class AcceptedRuleEventV3
    {
        public AcceptedRuleEventV3()
        {
        }
    }
}
