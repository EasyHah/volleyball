using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace Volleyball.Shared.Contracts
{
    public static class MatchAttributeDerivationV5
    {
        public const int FormulaVersion1 = 1;
        public const int CoefficientVersion1 = 1;

        public static DerivedMatchAttributesV5 Derive(
            CareerBaseAttributesV5 bases,
            DominantHandV5 dominantHand,
            int formulaVersion = FormulaVersion1,
            int coefficientVersion = CoefficientVersion1)
        {
            if (bases == null)
            {
                throw new ContractValidationException("bases are required.");
            }

            if (formulaVersion != FormulaVersion1 || coefficientVersion != CoefficientVersion1)
            {
                throw new ContractValidationException("Unsupported V5 derivation version.");
            }

            ContractGuard.DefinedEnum(dominantHand, nameof(dominantHand));
            var input = InputPayload(bases, dominantHand, formulaVersion, coefficientVersion);
            var inputFingerprint = Hash(input);
            var attackControl = Average(bases.Attack, bases.Coordination, bases.CourtIq);
            var attackPower = Average(bases.Attack, bases.Strength, bases.Jump, bases.Coordination);
            var attackReach = Reach(bases.HeightMillimeters, bases.Jump);
            var blockControl = Average(bases.Block, bases.Reaction, bases.Strength, bases.Coordination, bases.CourtIq);
            var blockReach = Reach(bases.HeightMillimeters, bases.Jump);
            var defenseControl = Average(bases.Defense, bases.Movement, bases.Reaction, bases.Coordination, bases.CourtIq);
            var receiveControl = Average(bases.Defense, bases.Movement, bases.Reaction, bases.Coordination, bases.CourtIq);
            var setControl = Average(bases.Set, bases.CourtIq, bases.Coordination, bases.Reaction, bases.Movement);
            var serveControl = Average(bases.Serve, bases.Strength, bases.Coordination, bases.CourtIq);
            var payload = ResultPayload(attackControl, attackPower, attackReach, blockControl, blockReach,
                defenseControl, receiveControl, setControl, serveControl, bases.Jump, bases.Movement, bases.Reaction,
                bases.CourtIq, dominantHand, formulaVersion, coefficientVersion, inputFingerprint);
            return new DerivedMatchAttributesV5(attackControl, attackPower, attackReach, blockControl,
                blockReach, defenseControl, receiveControl, setControl, serveControl, bases.Jump, bases.Movement,
                bases.Reaction, bases.CourtIq, dominantHand, formulaVersion, coefficientVersion,
                inputFingerprint, Hash(payload), Explanations());
        }

        private static IReadOnlyList<MatchAttributeExplanationV5> Explanations()
        {
            return new[]
            {
                new MatchAttributeExplanationV5("AttackControl", "Attack", "Coordination", "CourtIq"),
                new MatchAttributeExplanationV5("AttackPower", "Attack", "Strength", "Jump", "Coordination"),
                new MatchAttributeExplanationV5("AttackReachMillimeters", "HeightMillimeters", "Jump"),
                new MatchAttributeExplanationV5("BlockControl", "Block", "Reaction", "Strength", "Coordination", "CourtIq"),
                new MatchAttributeExplanationV5("BlockReachMillimeters", "HeightMillimeters", "Jump"),
                new MatchAttributeExplanationV5("DefenseControl", "Defense", "Movement", "Reaction", "Coordination", "CourtIq"),
                new MatchAttributeExplanationV5("ReceiveControl", "Defense", "Movement", "Reaction", "Coordination", "CourtIq"),
                new MatchAttributeExplanationV5("SetControl", "Set", "CourtIq", "Coordination", "Reaction", "Movement"),
                new MatchAttributeExplanationV5("ServeControl", "Serve", "Strength", "Coordination", "CourtIq"),
                new MatchAttributeExplanationV5("RuntimeIdentity", "DominantHand", "FormulaVersion", "CoefficientVersion")
            };
        }

        private static int Average(params int[] values)
        {
            var total = 0;
            foreach (var value in values) total += value;
            return (total + values.Length - 1) / values.Length;
        }

        private static int Reach(int heightMillimeters, int jump)
        {
            return heightMillimeters + (jump / 10);
        }

        private static string InputPayload(CareerBaseAttributesV5 value, DominantHandV5 hand, int formula, int coefficient)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "v5|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}",
                value.Strength, value.HeightMillimeters, value.Jump, value.Movement, value.Reaction,
                value.Coordination, value.Attack, value.Defense, value.CourtIq, value.Block, value.Serve,
                value.Set, (int)hand, formula, coefficient);
        }

        private static string ResultPayload(int attackControl, int attackPower, int attackReach, int blockControl,
            int blockReach, int defenseControl, int receiveControl, int setControl, int serveControl, int jump,
            int movement, int reaction, int courtIq, DominantHandV5 hand, int formula, int coefficient,
            string inputFingerprint)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "v5|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}",
                attackControl, attackPower, attackReach, blockControl, blockReach, defenseControl,
                receiveControl, setControl, serveControl, jump, movement, reaction, courtIq, (int)hand,
                formula, coefficient, inputFingerprint);
        }

        private static string Hash(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var output = new StringBuilder(bytes.Length * 2);
                foreach (var item in bytes) output.Append(item.ToString("x2"));
                return output.ToString();
            }
        }
    }
}
