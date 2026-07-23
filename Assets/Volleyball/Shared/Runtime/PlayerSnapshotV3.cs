using System;
using System.Runtime.Serialization;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class PlayerSnapshotV3
    {
        [DataMember(Name = "playerId", Order = 1)] private string _playerId;
        [DataMember(Name = "displayName", Order = 2)] private string _displayName;
        [DataMember(Name = "jerseyNumber", Order = 3)] private int _jerseyNumber;
        [DataMember(Name = "position", Order = 4)] private PlayerPosition _position;
        [DataMember(Name = "ability", Order = 5)] private PlayerAbilitySnapshotV3 _ability;

        public PlayerSnapshotV3(PlayerId playerId, string displayName, int jerseyNumber,
            PlayerPosition position, PlayerAbilitySnapshotV3 ability)
        {
            _playerId = ContractGuard.RequiredId(playerId.Value, nameof(playerId));
            _displayName = ContractGuard.RequiredText(displayName, nameof(displayName), 100);
            if (jerseyNumber < 0 || jerseyNumber > 99) throw new ContractValidationException("jerseyNumber must be in the range [0, 99].");
            ContractGuard.DefinedEnum(position, nameof(position));
            _jerseyNumber = jerseyNumber;
            _position = position;
            _ability = ability ?? throw new ArgumentNullException(nameof(ability));
            _ability.Validate();
        }

        public PlayerId PlayerId => new PlayerId(_playerId);
        public string DisplayName => _displayName;
        public int JerseyNumber => _jerseyNumber;
        public PlayerPosition Position => _position;
        public PlayerAbilitySnapshotV3 Ability => _ability;

        internal void Validate()
        {
            ContractGuard.RequiredId(_playerId, nameof(PlayerId));
            ContractGuard.RequiredText(_displayName, nameof(DisplayName), 100);
            if (_jerseyNumber < 0 || _jerseyNumber > 99) throw new ContractValidationException("JerseyNumber must be in the range [0, 99].");
            ContractGuard.DefinedEnum(_position, nameof(Position));
            if (_ability == null) throw new ContractValidationException("Ability is required.");
            _ability.Validate();
        }
    }
}
