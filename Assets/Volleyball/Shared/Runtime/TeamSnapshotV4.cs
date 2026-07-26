using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Shared.Contracts
{
    public sealed class TeamSnapshotV4
    {
        private readonly PlayerSnapshotV4[] _rotationOrder;
        private readonly ReadOnlyCollection<PlayerSnapshotV4> _readOnlyRotationOrder;

        public TeamSnapshotV4(
            TeamId teamId,
            string displayName,
            TeamSide side,
            IReadOnlyList<PlayerSnapshotV4> rotationOrder)
        {
            TeamId = new TeamId(ContractGuard.RequiredId(teamId.Value, nameof(teamId)));
            DisplayName = ContractGuard.RequiredText(displayName, nameof(displayName), 100);
            ContractGuard.DefinedEnum(side, nameof(side));
            Side = side;
            _rotationOrder = CopyPlayers(rotationOrder);
            _readOnlyRotationOrder = new ReadOnlyCollection<PlayerSnapshotV4>(_rotationOrder);
            Validate();
        }

        public TeamId TeamId { get; }
        public string DisplayName { get; }
        public TeamSide Side { get; }
        public IReadOnlyList<PlayerSnapshotV4> RotationOrder => _readOnlyRotationOrder;
        public IReadOnlyList<PlayerSnapshotV4> Players => _readOnlyRotationOrder;

        internal void Validate()
        {
            ContractGuard.RequiredId(TeamId.Value, nameof(TeamId));
            ContractGuard.RequiredText(DisplayName, nameof(DisplayName), 100);
            ContractGuard.DefinedEnum(Side, nameof(Side));
            if (_rotationOrder == null || _rotationOrder.Length != 6)
            {
                throw new ContractValidationException(
                    "A V4 team must contain exactly six players in rotation order.");
            }

            var ids = new HashSet<PlayerId>();
            for (var index = 0; index < _rotationOrder.Length; index++)
            {
                var player = _rotationOrder[index];
                if (player == null)
                {
                    throw new ContractValidationException(
                        "The rotation order cannot contain null players.");
                }

                player.Validate();
                if (!ids.Add(player.PlayerId))
                {
                    throw new ContractValidationException(
                        "Player IDs must be unique inside a V4 team.");
                }
            }
        }

        private static PlayerSnapshotV4[] CopyPlayers(
            IReadOnlyList<PlayerSnapshotV4> rotationOrder)
        {
            if (rotationOrder == null)
            {
                throw new ContractValidationException("rotationOrder is required.");
            }

            var copy = new PlayerSnapshotV4[rotationOrder.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = rotationOrder[index];
            }

            return copy;
        }
    }
}
