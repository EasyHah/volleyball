using System;
using VolleyballMatch.Shared.Contracts;
using PrototypePlayerId = VolleyballMatch.Domain.Prototype.PlayerId;
using StablePlayerId = VolleyballMatch.Shared.Contracts.PlayerId;

namespace VolleyballMatch.Domain.Players
{
    public readonly struct MatchPlayerBinding : IEquatable<MatchPlayerBinding>
    {
        public MatchPlayerBinding(
            StablePlayerId stablePlayerId,
            PrototypePlayerId slot,
            PlayerAbilitySnapshotV1 abilitySnapshot)
        {
            if (string.IsNullOrEmpty(stablePlayerId.Value))
            {
                throw new ArgumentException("A stable player ID is required.", nameof(stablePlayerId));
            }

            if (!Enum.IsDefined(typeof(VolleyballMatch.Domain.Prototype.TeamId), slot.Team) ||
                !Enum.IsDefined(typeof(VolleyballMatch.Domain.Prototype.PlayerRole), slot.Role))
            {
                throw new ArgumentException("The prototype slot contains an unsupported value.", nameof(slot));
            }

            StablePlayerId = stablePlayerId;
            Slot = slot;
            Ability = new PlayerAbilityProfile(
                abilitySnapshot ?? throw new ArgumentNullException(nameof(abilitySnapshot)));
        }

        public StablePlayerId StablePlayerId { get; }

        public PrototypePlayerId Slot { get; }

        public PlayerAbilityProfile Ability { get; }

        public bool Equals(MatchPlayerBinding other)
        {
            return StablePlayerId.Equals(other.StablePlayerId) && Slot.Equals(other.Slot);
        }

        public override bool Equals(object obj)
        {
            return obj is MatchPlayerBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StablePlayerId.GetHashCode() * 397) ^ Slot.GetHashCode();
            }
        }
    }
}
