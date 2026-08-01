using System;

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
