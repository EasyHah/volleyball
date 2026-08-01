using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volleyball.Domain.Simulation;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    // The entire mutable pre-serve state is frozen as one unit before a TrainingLab run.
    public sealed class TrainingServeStartV1
    {
        private readonly PlayerId[] _homeRotation;
        private readonly PlayerId[] _awayRotation;

        public TrainingServeStartV1(TeamSide servingSide,
            IReadOnlyList<PlayerId> homeRotation, IReadOnlyList<PlayerId> awayRotation,
            IReadOnlyList<TrainingPlayerPoseV1> playerPoses,
            SimVector3 ballPosition, SimVector3 ballVelocity)
        {
            if (!Enum.IsDefined(typeof(TeamSide), servingSide))
                throw new ArgumentOutOfRangeException(nameof(servingSide));
            _homeRotation = CopyRotation(homeRotation, nameof(homeRotation));
            _awayRotation = CopyRotation(awayRotation, nameof(awayRotation));
            if (playerPoses == null || playerPoses.Count != 12)
                throw new ArgumentException("A serve start requires twelve frozen player poses.",
                    nameof(playerPoses));
            if (!ballPosition.IsFinite || !ballVelocity.IsFinite)
                throw new ArgumentException("Serve ball values must be finite.");

            ServingSide = servingSide;
            PlayerPoses = new ReadOnlyCollection<TrainingPlayerPoseV1>(
                playerPoses.ToArray());
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
        }

        public TeamSide ServingSide { get; }
        public IReadOnlyList<PlayerId> HomeRotation => _homeRotation;
        public IReadOnlyList<PlayerId> AwayRotation => _awayRotation;
        public IReadOnlyList<TrainingPlayerPoseV1> PlayerPoses { get; }
        public SimVector3 BallPosition { get; }
        public SimVector3 BallVelocity { get; }

        public IReadOnlyList<ServePositionSlotV1> CreatePositionSlots()
        {
            var poses = PlayerPoses.ToDictionary(value => value.PlayerId);
            var slots = new List<ServePositionSlotV1>(12);
            AddSlots(slots, TeamSide.Home, _homeRotation, poses);
            AddSlots(slots, TeamSide.Away, _awayRotation, poses);
            return slots;
        }

        private static PlayerId[] CopyRotation(IReadOnlyList<PlayerId> input, string name)
        {
            if (input == null || input.Count != 6)
                throw new ArgumentException("A rotation requires exactly six players.", name);
            var copy = input.ToArray();
            if (copy.Any(value => string.IsNullOrWhiteSpace(value.Value)) ||
                copy.Distinct().Count() != copy.Length)
                throw new ArgumentException("Rotation players must be unique IDs.", name);
            return copy;
        }

        private static void AddSlots(ICollection<ServePositionSlotV1> output,
            TeamSide side, IReadOnlyList<PlayerId> rotation,
            IReadOnlyDictionary<PlayerId, TrainingPlayerPoseV1> poses)
        {
            for (var index = 0; index < rotation.Count; index++)
            {
                if (!poses.TryGetValue(rotation[index], out var pose))
                    throw new ArgumentException("A rotation player is absent from the frozen pose list.");
                output.Add(new ServePositionSlotV1(side, index + 1,
                    rotation[index], pose.Position));
            }
        }
    }
}
