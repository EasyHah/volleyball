using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Shared.Contracts
{
    public sealed class ReplayAbilityConsumptionRecordV4
    {
        private const string FactoryReadEvidence =
            "ExecutionEnvelopeFactoryRead";

        private static readonly string[] FrozenFieldNames =
        {
            "Attack.DirectionControl",
            "Attack.SpeedControl",
            "Attack.PowerCapacity",
            "Attack.ContactHeightMeters",
            "Attack.ApproachMobility",
            "Block.Timing",
            "Block.HandControl",
            "Block.ReachHeightMeters",
            "Block.LateralMobility",
            "Defense.Reaction",
            "Defense.PlatformControl",
            "Defense.CoverageMobility",
            "Defense.Awareness",
            "Receive.FirstTouchControl",
            "Receive.Reaction",
            "Receive.Movement",
            "Receive.Awareness",
            "Set.PlacementControl",
            "Set.TempoControl",
            "Set.SoftTouch",
            "Set.Movement",
            "Set.Awareness",
            "Serve.DirectionControl",
            "Serve.SpeedControl",
            "Serve.PowerCapacity",
            "Serve.Consistency"
        };

        private static readonly Dictionary<string, int> FrozenIndexes =
            CreateFrozenIndexes();

        public ReplayAbilityConsumptionRecordV4(
            string playerId,
            string derivedAttributesFingerprint,
            string attributeName,
            float value,
            string evidenceKind)
        {
            PlayerId = ReplayContractGuardV4.Required(playerId, nameof(playerId));
            DerivedAttributesFingerprint = ReplayContractGuardV4.Hash(
                derivedAttributesFingerprint,
                nameof(derivedAttributesFingerprint));
            AttributeName = ReplayContractGuardV4.Required(
                attributeName,
                nameof(attributeName));
            if (!FrozenIndexes.ContainsKey(AttributeName))
            {
                throw new ContractValidationException(
                    "attributeName is not in the frozen V4 derived-field order.");
            }

            Value = ReplayContractGuardV4.Finite(value, nameof(value));
            EvidenceKind = ReplayContractGuardV4.Required(
                evidenceKind,
                nameof(evidenceKind));
            if (!string.Equals(
                    EvidenceKind,
                    FactoryReadEvidence,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "Replay ability consumption requires ExecutionEnvelopeFactoryRead evidence.");
            }
        }

        public string PlayerId { get; }

        public string DerivedAttributesFingerprint { get; }

        public string AttributeName { get; }

        public float Value { get; }

        public string EvidenceKind { get; }

        public static IReadOnlyList<string> FrozenDerivedFieldOrder { get; } =
            new ReadOnlyCollection<string>((string[])FrozenFieldNames.Clone());

        internal static int FrozenIndex(string attributeName)
        {
            return FrozenIndexes.TryGetValue(attributeName, out var index)
                ? index
                : int.MaxValue;
        }

        private static Dictionary<string, int> CreateFrozenIndexes()
        {
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < FrozenFieldNames.Length; index++)
            {
                indexes.Add(FrozenFieldNames[index], index);
            }

            return indexes;
        }
    }
}
