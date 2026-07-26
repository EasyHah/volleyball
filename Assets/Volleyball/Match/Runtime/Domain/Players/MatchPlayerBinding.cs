using System;
using Volleyball.Shared.Contracts;
using PrototypePlayerId = Volleyball.Domain.Prototype.PlayerId;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Domain.Players
{
    public readonly struct MatchPlayerBinding : IEquatable<MatchPlayerBinding>
    {
        public MatchPlayerBinding(
            PlayerSnapshotV4 player,
            PrototypePlayerId slot,
            TeamSide side,
            int rotationPosition)
        {
            var snapshot = player ?? throw new ArgumentNullException(nameof(player));
            if (!Enum.IsDefined(typeof(Volleyball.Domain.Prototype.TeamId), slot.Team) ||
                !Enum.IsDefined(typeof(Volleyball.Domain.Prototype.PlayerRole), slot.Role))
            {
                throw new ArgumentException(
                    "The prototype slot contains an unsupported value.",
                    nameof(slot));
            }

            if (!Enum.IsDefined(typeof(TeamSide), side))
            {
                throw new ArgumentOutOfRangeException(nameof(side));
            }

            if (rotationPosition < 1 || rotationPosition > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(rotationPosition));
            }

            var recomputed = MatchAttributeDerivationV4.Derive(
                snapshot.Physical,
                snapshot.Technical,
                snapshot.DominantHand,
                MatchAttributeDerivationConfigV4.Version1);
            if (!string.Equals(
                    snapshot.Derived.InputFingerprint,
                    recomputed.InputFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.Derived.ResultFingerprint,
                    recomputed.ResultFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "The player binding requires verified V4 derived fingerprints.");
            }

            StablePlayerId = snapshot.PlayerId;
            Slot = slot;
            Side = side;
            RotationPosition = rotationPosition;
            DominantHand = snapshot.DominantHand;
            Derived = snapshot.Derived;
        }

        public StablePlayerId StablePlayerId { get; }

        public PrototypePlayerId Slot { get; }

        public TeamSide Side { get; }

        public int RotationPosition { get; }

        public DominantHandV4 DominantHand { get; }

        public DerivedMatchAttributesV4 Derived { get; }

        public bool Equals(MatchPlayerBinding other)
        {
            return StablePlayerId.Equals(other.StablePlayerId) &&
                   Slot.Equals(other.Slot) &&
                   Side == other.Side &&
                   RotationPosition == other.RotationPosition;
        }

        public override bool Equals(object obj)
        {
            return obj is MatchPlayerBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (StablePlayerId.GetHashCode() * 397) ^ Slot.GetHashCode();
                hash = (hash * 397) ^ (int)Side;
                return (hash * 397) ^ RotationPosition;
            }
        }
    }
}
