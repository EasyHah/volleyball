using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Shared.Contracts
{
    public sealed class TeamSnapshotV5
    {
        private readonly PlayerSnapshotV5[] _rotationOrder;
        private readonly ReadOnlyCollection<PlayerSnapshotV5> _readOnlyRotationOrder;

        public TeamSnapshotV5(
            TeamId teamId,
            string displayName,
            TeamSide side,
            IReadOnlyList<PlayerSnapshotV5> rotationOrder)
        {
            TeamId = new TeamId(ContractGuard.RequiredId(teamId.Value, nameof(teamId)));
            DisplayName = ContractGuard.RequiredText(displayName, nameof(displayName), 100);
            ContractGuard.DefinedEnum(side, nameof(side));
            Side = side;
            if (rotationOrder == null || rotationOrder.Count != 6)
            {
                throw new ContractValidationException(
                    "A V5 team must contain exactly six players in rotation order.");
            }

            _rotationOrder = new PlayerSnapshotV5[rotationOrder.Count];
            for (var index = 0; index < _rotationOrder.Length; index++)
            {
                _rotationOrder[index] = rotationOrder[index] ??
                    throw new ContractValidationException("The rotation order cannot contain null players.");
            }

            _readOnlyRotationOrder = new ReadOnlyCollection<PlayerSnapshotV5>(_rotationOrder);
            Validate();
        }

        public TeamId TeamId { get; }
        public string DisplayName { get; }
        public TeamSide Side { get; }
        public IReadOnlyList<PlayerSnapshotV5> RotationOrder => _readOnlyRotationOrder;

        internal void Validate()
        {
            var playerIds = new HashSet<PlayerId>();
            foreach (var player in _rotationOrder)
            {
                player.Validate();
                if (!playerIds.Add(player.PlayerId))
                {
                    throw new ContractValidationException(
                        "Player IDs must be unique inside a V5 team.");
                }
            }
        }
    }
}
