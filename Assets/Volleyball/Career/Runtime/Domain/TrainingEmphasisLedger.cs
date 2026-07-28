using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Career.Domain
{
    public sealed class TrainingEmphasisContribution
    {
        public TrainingEmphasisContribution(
            SlotActionId sourceSlotActionId,
            CareerTrainingDirection direction,
            int bonusBasisPoints)
        {
            CareerSaveModelGuard.StableId(sourceSlotActionId.Value, nameof(sourceSlotActionId));
            CareerSaveModelGuard.DefinedEnum(direction, nameof(direction));
            CareerSaveModelGuard.InclusiveRange(
                bonusBasisPoints,
                1,
                1500,
                nameof(bonusBasisPoints));
            SourceSlotActionId = sourceSlotActionId;
            Direction = direction;
            BonusBasisPoints = bonusBasisPoints;
        }

        public SlotActionId SourceSlotActionId { get; }
        public CareerTrainingDirection Direction { get; }
        public int BonusBasisPoints { get; }

        internal TrainingEmphasisContribution Copy()
        {
            return new TrainingEmphasisContribution(
                SourceSlotActionId,
                Direction,
                BonusBasisPoints);
        }
    }

    public sealed class FrozenTrainingEmphasis
    {
        private readonly SlotActionId[] _sourceSlotActionIds;
        private readonly ReadOnlyCollection<SlotActionId> _readOnlySourceSlotActionIds;

        internal FrozenTrainingEmphasis(
            CareerTrainingDirection direction,
            IEnumerable<SlotActionId> sourceSlotActionIds,
            int totalBonusBasisPoints)
        {
            Direction = direction;
            _sourceSlotActionIds = new List<SlotActionId>(sourceSlotActionIds).ToArray();
            _readOnlySourceSlotActionIds = Array.AsReadOnly(_sourceSlotActionIds);
            TotalBonusBasisPoints = totalBonusBasisPoints;
        }

        public CareerTrainingDirection Direction { get; }
        public IReadOnlyList<SlotActionId> SourceSlotActionIds => _readOnlySourceSlotActionIds;
        public int TotalBonusBasisPoints { get; }
    }

    public sealed class TrainingEmphasisLedger
    {
        private static readonly TrainingEmphasisLedger EmptyValue =
            new TrainingEmphasisLedger(Array.Empty<TrainingEmphasisContribution>());

        private readonly TrainingEmphasisContribution[] _contributions;
        private readonly ReadOnlyCollection<TrainingEmphasisContribution> _readOnlyContributions;

        public TrainingEmphasisLedger(IEnumerable<TrainingEmphasisContribution> contributions)
        {
            if (contributions == null)
            {
                throw new ArgumentNullException(nameof(contributions));
            }

            var copied = new List<TrainingEmphasisContribution>();
            var sourceIds = new HashSet<SlotActionId>();
            foreach (var contribution in contributions)
            {
                if (contribution == null || !sourceIds.Add(contribution.SourceSlotActionId))
                {
                    throw new ArgumentException(
                        "Training emphasis sources must be non-null and unique.",
                        nameof(contributions));
                }

                copied.Add(contribution.Copy());
            }

            _contributions = copied.ToArray();
            _readOnlyContributions = Array.AsReadOnly(_contributions);
        }

        public static TrainingEmphasisLedger Empty => EmptyValue;
        public IReadOnlyList<TrainingEmphasisContribution> Contributions => _readOnlyContributions;

        public TrainingEmphasisLedger AddExecutedTraining(
            CareerWeekActionState action,
            CareerWeekActionCatalog catalog)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            for (var index = 0; index < _contributions.Length; index++)
            {
                if (_contributions[index].SourceSlotActionId.Equals(action.SlotActionId))
                {
                    throw new ArgumentException(
                        "A training source may contribute only once.",
                        nameof(action));
                }
            }

            var definition = catalog.Find(action.ContentId);
            if (definition == null || definition.Kind != action.Kind ||
                !definition.Direction.HasValue ||
                (action.Kind != CareerWeekActionKind.SpecializedTraining &&
                 action.Kind != CareerWeekActionKind.StrengthTraining))
            {
                throw new ArgumentException(
                    "Only a catalog-matched specialized or strength action contributes training emphasis.",
                    nameof(action));
            }

            var total = 0;
            for (var index = 0; index < _contributions.Length; index++)
            {
                if (_contributions[index].Direction == definition.Direction.Value)
                {
                    total += _contributions[index].BonusBasisPoints;
                }
            }

            var contribution = total == 0 ? 1000 : Math.Min(500, 1500 - total);
            if (contribution <= 0)
            {
                throw new InvalidOperationException(
                    "The training emphasis direction has already reached its V1 cap.");
            }

            var next = new TrainingEmphasisContribution[_contributions.Length + 1];
            for (var index = 0; index < _contributions.Length; index++)
            {
                next[index] = _contributions[index];
            }

            next[next.Length - 1] = new TrainingEmphasisContribution(
                action.SlotActionId,
                definition.Direction.Value,
                contribution);
            return new TrainingEmphasisLedger(next);
        }

        public IReadOnlyList<FrozenTrainingEmphasis> Freeze()
        {
            var result = new List<FrozenTrainingEmphasis>();
            for (var directionValue = 0; directionValue < 8; directionValue++)
            {
                var direction = (CareerTrainingDirection)directionValue;
                var sources = new List<SlotActionId>();
                var total = 0;
                for (var index = 0; index < _contributions.Length; index++)
                {
                    if (_contributions[index].Direction != direction)
                    {
                        continue;
                    }

                    sources.Add(_contributions[index].SourceSlotActionId);
                    total += _contributions[index].BonusBasisPoints;
                }

                if (sources.Count != 0)
                {
                    result.Add(new FrozenTrainingEmphasis(direction, sources, Math.Min(total, 1500)));
                }
            }

            return Array.AsReadOnly(result.ToArray());
        }

        public TrainingEmphasisLedger Consume()
        {
            return Empty;
        }
    }
}
