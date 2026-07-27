using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class TeamSnapshotV2
    {
        [DataMember(Name = "teamId", Order = 1)] private string _teamId;
        [DataMember(Name = "displayName", Order = 2)] private string _displayName;
        [DataMember(Name = "side", Order = 3)] private TeamSide _side;
        [DataMember(Name = "players", Order = 4)] private PlayerSnapshotV2[] _players;

        public TeamSnapshotV2(TeamId teamId, string displayName, TeamSide side, IReadOnlyList<PlayerSnapshotV2> players)
        {
            _teamId = ContractGuard.RequiredId(teamId.Value, nameof(teamId));
            _displayName = ContractGuard.RequiredText(displayName, nameof(displayName), 100);
            ContractGuard.DefinedEnum(side, nameof(side));
            _side = side;
            _players = CopyPlayers(players);
            Validate();
        }

        public TeamId TeamId => new TeamId(_teamId);
        public string DisplayName => _displayName;
        public TeamSide Side => _side;
        public IReadOnlyList<PlayerSnapshotV2> Players => new ReadOnlyCollection<PlayerSnapshotV2>(_players);

        internal void Validate()
        {
            ContractGuard.RequiredId(_teamId, nameof(TeamId));
            ContractGuard.RequiredText(_displayName, nameof(DisplayName), 100);
            ContractGuard.DefinedEnum(_side, nameof(Side));
            if (_players == null || _players.Length < 3 || _players.Length > 14) throw new ContractValidationException("A team must contain 3 to 14 player snapshots.");
            var ids = new HashSet<PlayerId>();
            for (var index = 0; index < _players.Length; index++)
            {
                var player = _players[index];
                if (player == null) throw new ContractValidationException("Player snapshots cannot contain null.");
                player.Validate();
                if (!ids.Add(player.PlayerId)) throw new ContractValidationException("Player IDs must be unique inside a team.");
            }
        }

        private static PlayerSnapshotV2[] CopyPlayers(IReadOnlyList<PlayerSnapshotV2> players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            var copy = new PlayerSnapshotV2[players.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = players[index];
            return copy;
        }
    }
}
