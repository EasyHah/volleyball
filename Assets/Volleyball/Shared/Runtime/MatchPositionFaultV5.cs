using System;
using System.Collections.Generic;

namespace Volleyball.Shared.Contracts
{
    // Canonical Match fact captured at serve contact. Coordinates are millimetres
    // so replay hashes do not depend on platform float formatting.
    public sealed class MatchPositionFaultV5
    {
        public const int RuleVersion = 1;

        public MatchPositionFaultV5(int rallyNumber, TeamSide violatingSide,
            TeamSide awardedSide, TeamSide servingSide, string rule,
            PlayerId requiredPlayerId, int requiredSlot, int requiredXMillimeters,
            int requiredZMillimeters, PlayerId violatingPlayerId, int violatingSlot,
            int violatingXMillimeters, int violatingZMillimeters)
        {
            if (rallyNumber < 1 || requiredSlot < 1 || requiredSlot > 6 ||
                violatingSlot < 1 || violatingSlot > 6 ||
                !Enum.IsDefined(typeof(TeamSide), violatingSide) ||
                !Enum.IsDefined(typeof(TeamSide), awardedSide) ||
                !Enum.IsDefined(typeof(TeamSide), servingSide) ||
                violatingSide == awardedSide)
                throw new ContractValidationException("V5 position-fault fields are invalid.");
            RallyNumber = rallyNumber;
            ViolatingSide = violatingSide;
            AwardedSide = awardedSide;
            ServingSide = servingSide;
            Rule = ValidateRule(rule, requiredSlot, violatingSlot);
            RequiredPlayerId = new PlayerId(ContractGuard.RequiredId(requiredPlayerId.Value,
                nameof(requiredPlayerId)));
            RequiredSlot = requiredSlot;
            RequiredXMillimeters = requiredXMillimeters;
            RequiredZMillimeters = requiredZMillimeters;
            ViolatingPlayerId = new PlayerId(ContractGuard.RequiredId(violatingPlayerId.Value,
                nameof(violatingPlayerId)));
            ViolatingSlot = violatingSlot;
            ViolatingXMillimeters = violatingXMillimeters;
            ViolatingZMillimeters = violatingZMillimeters;
        }

        public int RallyNumber { get; }
        public int RuleVersionValue => RuleVersion;
        public TeamSide ViolatingSide { get; }
        public TeamSide AwardedSide { get; }
        public TeamSide ServingSide { get; }
        public string Rule { get; }
        public PlayerId RequiredPlayerId { get; }
        public int RequiredSlot { get; }
        public int RequiredXMillimeters { get; }
        public int RequiredZMillimeters { get; }
        public PlayerId ViolatingPlayerId { get; }
        public int ViolatingSlot { get; }
        public int ViolatingXMillimeters { get; }
        public int ViolatingZMillimeters { get; }

        public void ValidateAgainst(MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            var team = ViolatingSide == TeamSide.Home ? context.Home : context.Away;
            if (AwardedSide == ViolatingSide ||
                !team.RotationOrder[RequiredSlot - 1].PlayerId.Equals(RequiredPlayerId) ||
                !team.RotationOrder[ViolatingSlot - 1].PlayerId.Equals(ViolatingPlayerId))
                throw new ContractValidationException(
                    "V5 position-fault evidence does not match its context rotation.");
        }

        public bool EqualsFact(MatchPositionFaultV5 other)
        {
            return other != null && RallyNumber == other.RallyNumber &&
                ViolatingSide == other.ViolatingSide && AwardedSide == other.AwardedSide &&
                ServingSide == other.ServingSide && string.Equals(Rule, other.Rule, StringComparison.Ordinal) &&
                RequiredPlayerId.Equals(other.RequiredPlayerId) && RequiredSlot == other.RequiredSlot &&
                RequiredXMillimeters == other.RequiredXMillimeters &&
                RequiredZMillimeters == other.RequiredZMillimeters &&
                ViolatingPlayerId.Equals(other.ViolatingPlayerId) && ViolatingSlot == other.ViolatingSlot &&
                ViolatingXMillimeters == other.ViolatingXMillimeters &&
                ViolatingZMillimeters == other.ViolatingZMillimeters;
        }

        public static bool SequencesEqual(IReadOnlyList<MatchPositionFaultV5> left,
            IReadOnlyList<MatchPositionFaultV5> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (var index = 0; index < left.Count; index++)
                if (!left[index].EqualsFact(right[index])) return false;
            return true;
        }

        private static string ValidateRule(string rule, int requiredSlot, int violatingSlot)
        {
            var expected = rule switch
            {
                "Slot4BehindSlot5" => (4, 5),
                "Slot3BehindSlot6" => (3, 6),
                "Slot2BehindSlot1" => (2, 1),
                "Slot4RightOfSlot3" => (4, 3),
                "Slot3RightOfSlot2" => (3, 2),
                "Slot5RightOfSlot6" => (5, 6),
                "Slot6RightOfSlot1" => (6, 1),
                _ => throw new ContractValidationException("V5 position-fault rule is unsupported.")
            };
            if (requiredSlot != expected.Item1 || violatingSlot != expected.Item2)
                throw new ContractValidationException("V5 position-fault slots do not match its rule.");
            return rule;
        }
    }
}
