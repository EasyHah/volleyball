using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class OnCourtPlayerEligibilityV3
    {
        internal OnCourtPlayerEligibilityV3(
            PlayerId playerId,
            TeamSide side,
            PlayerPosition registeredPosition,
            int rotationPosition,
            bool isCurrentServer,
            PlayerId? replacedPlayerId)
        {
            PlayerId = PlayerWorldSnapshotV3.RequirePlayerId(playerId, nameof(playerId));
            Side = PlayerWorldSnapshotV3.RequireDefinedEnum(side, nameof(side));
            RegisteredPosition = PlayerWorldSnapshotV3.RequireDefinedEnum(registeredPosition, nameof(registeredPosition));
            if (rotationPosition < 1 || rotationPosition > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(rotationPosition), "Rotation position must be from 1 through 6.");
            }

            RotationPosition = rotationPosition;
            IsFrontRow = rotationPosition >= 2 && rotationPosition <= 4;
            IsCurrentServer = isCurrentServer;
            CanBlock = registeredPosition != PlayerPosition.Libero && IsFrontRow;
            CanAttackAboveNetFromFrontZone = registeredPosition != PlayerPosition.Libero && IsFrontRow;
            ReplacedPlayerId = replacedPlayerId;
        }

        public PlayerId PlayerId { get; }

        public TeamSide Side { get; }

        public PlayerPosition RegisteredPosition { get; }

        public int RotationPosition { get; }

        public bool IsFrontRow { get; }

        public bool IsCurrentServer { get; }

        public bool CanBlock { get; }

        public bool CanAttackAboveNetFromFrontZone { get; }

        public PlayerId? ReplacedPlayerId { get; }
    }

    public sealed class LiberoReplacementV3
    {
        public LiberoReplacementV3(PlayerId liberoPlayerId, PlayerId replacedPlayerId)
        {
            LiberoPlayerId = PlayerWorldSnapshotV3.RequirePlayerId(liberoPlayerId, nameof(liberoPlayerId));
            ReplacedPlayerId = PlayerWorldSnapshotV3.RequirePlayerId(replacedPlayerId, nameof(replacedPlayerId));
            if (LiberoPlayerId.Equals(ReplacedPlayerId))
            {
                throw new ArgumentException("A libero cannot replace itself.", nameof(replacedPlayerId));
            }
        }

        public PlayerId LiberoPlayerId { get; }

        public PlayerId ReplacedPlayerId { get; }
    }

    public sealed class OnCourtEligibilitySnapshot
    {
        private readonly IReadOnlyList<OnCourtPlayerEligibilityV3> _players;
        private readonly IReadOnlyDictionary<PlayerId, OnCourtPlayerEligibilityV3> _playersById;

        public OnCourtEligibilitySnapshot()
            : this(Array.Empty<OnCourtPlayerEligibilityV3>(), false)
        {
        }

        internal OnCourtEligibilitySnapshot(IReadOnlyList<OnCourtPlayerEligibilityV3> players)
            : this(players, true)
        {
        }

        public IReadOnlyList<OnCourtPlayerEligibilityV3> Players => _players;

        public OnCourtPlayerEligibilityV3 For(PlayerId playerId)
        {
            PlayerWorldSnapshotV3.RequirePlayerId(playerId, nameof(playerId));
            if (!_playersById.TryGetValue(playerId, out var player))
            {
                throw new KeyNotFoundException("The player is not on court.");
            }

            return player;
        }

        private OnCourtEligibilitySnapshot(IReadOnlyList<OnCourtPlayerEligibilityV3> players, bool requireFormalSix)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            if (requireFormalSix && players.Count != 12)
            {
                throw new ArgumentException("Exactly twelve on-court players are required.", nameof(players));
            }

            var copy = new OnCourtPlayerEligibilityV3[players.Count];
            var byId = new Dictionary<PlayerId, OnCourtPlayerEligibilityV3>();
            var homeCount = 0;
            var awayCount = 0;
            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                if (player == null)
                {
                    throw new ArgumentException("On-court eligibility is required.", nameof(players));
                }

                if (!byId.TryAdd(player.PlayerId, player))
                {
                    throw new ArgumentException("On-court player IDs must be distinct.", nameof(players));
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

            if (requireFormalSix && (homeCount != 6 || awayCount != 6))
            {
                throw new ArgumentException("Exactly six on-court players per side are required.", nameof(players));
            }

            _players = new ReadOnlyCollection<OnCourtPlayerEligibilityV3>(copy);
            _playersById = new ReadOnlyDictionary<PlayerId, OnCourtPlayerEligibilityV3>(byId);
        }
    }
}
