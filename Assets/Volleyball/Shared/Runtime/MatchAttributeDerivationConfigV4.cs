using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Volleyball.Shared.Contracts
{
    public sealed class MatchAttributeCoefficientV4 : IEquatable<MatchAttributeCoefficientV4>
    {
        public MatchAttributeCoefficientV4(string outputName, string inputName, float coefficient)
        {
            OutputName = RequiredName(outputName, nameof(outputName));
            InputName = RequiredName(inputName, nameof(inputName));
            if (float.IsNaN(coefficient) || float.IsInfinity(coefficient))
            {
                throw new ContractValidationException("coefficient must be finite.");
            }

            Coefficient = coefficient;
        }

        public string OutputName { get; }
        public string InputName { get; }
        public float Coefficient { get; }

        public bool Equals(MatchAttributeCoefficientV4 other)
        {
            return other != null &&
                string.Equals(OutputName, other.OutputName, StringComparison.Ordinal) &&
                string.Equals(InputName, other.InputName, StringComparison.Ordinal) &&
                Coefficient.Equals(other.Coefficient);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MatchAttributeCoefficientV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(OutputName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(InputName);
                hash = (hash * 397) ^ Coefficient.GetHashCode();
                return hash;
            }
        }

        private static string RequiredName(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ContractValidationException(name + " is required.");
            }

            return value;
        }
    }

    public sealed class MatchAttributeDerivationConfigV4
    {
        private static readonly FormulaDefinitionV4[] Definitions =
        {
            Formula("Attack.DirectionControl", true, "AttackTechnique", "Coordination", "CourtAwareness"),
            Formula("Attack.SpeedControl", true, "AttackTechnique", "Coordination", "SoftTouch"),
            Formula("Attack.PowerCapacity", true, "AttackPower", "Jump", "Coordination"),
            Formula("Attack.ContactHeightMeters", false, "StandingReachMeters", "BaseOffsetMeters", "Jump"),
            Formula("Attack.ApproachMobility", true, "Mobility", "Coordination"),
            Formula("Block.Timing", true, "BlockTechnique", "Reaction", "CourtAwareness"),
            Formula("Block.HandControl", true, "BlockTechnique", "Coordination", "SoftTouch"),
            Formula("Block.ReachHeightMeters", false, "StandingReachMeters", "BaseOffsetMeters", "Jump"),
            Formula("Block.LateralMobility", true, "Mobility", "Reaction"),
            Formula("Defense.Reaction", true, "Reaction", "CourtAwareness"),
            Formula("Defense.PlatformControl", true, "DefenseTechnique", "Coordination", "SoftTouch"),
            Formula("Defense.CoverageMobility", true, "Mobility", "Reaction", "CourtAwareness"),
            Formula("Defense.Awareness", true, "CourtAwareness"),
            Formula("Receive.FirstTouchControl", true, "ReceiveTechnique", "Coordination", "SoftTouch"),
            Formula("Receive.Reaction", true, "Reaction", "CourtAwareness"),
            Formula("Receive.Movement", true, "Mobility", "Coordination"),
            Formula("Receive.Awareness", true, "CourtAwareness"),
            Formula("Set.PlacementControl", true, "SetTechnique", "Coordination", "CourtAwareness"),
            Formula("Set.TempoControl", true, "SetTechnique", "Reaction", "CourtAwareness"),
            Formula("Set.SoftTouch", true, "SoftTouch", "SetTechnique", "Coordination"),
            Formula("Set.Movement", true, "Mobility", "Coordination"),
            Formula("Set.Awareness", true, "CourtAwareness"),
            Formula("Serve.DirectionControl", true, "ServeTechnique", "Coordination", "CourtAwareness"),
            Formula("Serve.SpeedControl", true, "ServeTechnique", "Coordination", "SoftTouch"),
            Formula("Serve.PowerCapacity", true, "AttackPower", "ServeTechnique", "Coordination"),
            Formula("Serve.Consistency", true, "ServeTechnique", "Coordination", "Reaction")
        };

        private static readonly MatchAttributeDerivationConfigV4 VersionOne =
            new MatchAttributeDerivationConfigV4(1, 1, CreateVersionOneCoefficients());

        private readonly Dictionary<string, MatchAttributeCoefficientV4> _coefficientsByTerm;

        public MatchAttributeDerivationConfigV4(
            int formulaVersion,
            int coefficientVersion,
            IEnumerable<MatchAttributeCoefficientV4> coefficients)
        {
            if (formulaVersion <= 0)
            {
                throw new ContractValidationException("formulaVersion must be positive.");
            }

            if (coefficientVersion <= 0)
            {
                throw new ContractValidationException("coefficientVersion must be positive.");
            }

            if (coefficients == null)
            {
                throw new ContractValidationException("coefficients are required.");
            }

            FormulaVersion = formulaVersion;
            CoefficientVersion = coefficientVersion;
            var supplied = coefficients.ToArray();
            if (supplied.Any(coefficient => coefficient == null))
            {
                throw new ContractValidationException("coefficients cannot contain null values.");
            }

            _coefficientsByTerm = new Dictionary<string, MatchAttributeCoefficientV4>(StringComparer.Ordinal);
            foreach (var coefficient in supplied)
            {
                var key = Key(coefficient.OutputName, coefficient.InputName);
                if (_coefficientsByTerm.ContainsKey(key))
                {
                    throw new ContractValidationException(
                        "Coefficient set contains duplicate term " + coefficient.OutputName + "/" + coefficient.InputName + ".");
                }

                _coefficientsByTerm.Add(key, coefficient);
            }

            var ordered = new List<MatchAttributeCoefficientV4>();
            foreach (var definition in Definitions)
            {
                var formulaSum = 0d;
                foreach (var inputName in definition.InputNames)
                {
                    var key = Key(definition.OutputName, inputName);
                    if (!_coefficientsByTerm.TryGetValue(key, out var coefficient))
                    {
                        throw new ContractValidationException(
                            "Coefficient set is missing term " + definition.OutputName + "/" + inputName + ".");
                    }

                    formulaSum += coefficient.Coefficient;
                    ordered.Add(coefficient);
                }

                if (definition.RequiresUnitWeight && Math.Abs(formulaSum - 1d) > 0.000001d)
                {
                    throw new ContractValidationException(
                        definition.OutputName + " coefficients must sum to 1.");
                }
            }

            if (ordered.Count != supplied.Length)
            {
                throw new ContractValidationException("Coefficient set contains unsupported terms.");
            }

            Coefficients = new ReadOnlyCollection<MatchAttributeCoefficientV4>(ordered);
        }

        public static MatchAttributeDerivationConfigV4 Version1 => VersionOne;

        public int FormulaVersion { get; }
        public int CoefficientVersion { get; }
        public IReadOnlyList<MatchAttributeCoefficientV4> Coefficients { get; }

        internal static IReadOnlyList<FormulaDefinitionV4> FormulaDefinitions => Definitions;

        internal float GetCoefficient(string outputName, string inputName)
        {
            return _coefficientsByTerm[Key(outputName, inputName)].Coefficient;
        }

        private static FormulaDefinitionV4 Formula(
            string outputName,
            bool requiresUnitWeight,
            params string[] inputNames)
        {
            return new FormulaDefinitionV4(outputName, inputNames, requiresUnitWeight);
        }

        private static MatchAttributeCoefficientV4[] CreateVersionOneCoefficients()
        {
            return new[]
            {
                Coefficient("Attack.DirectionControl", "AttackTechnique", .65f),
                Coefficient("Attack.DirectionControl", "Coordination", .20f),
                Coefficient("Attack.DirectionControl", "CourtAwareness", .15f),
                Coefficient("Attack.SpeedControl", "AttackTechnique", .55f),
                Coefficient("Attack.SpeedControl", "Coordination", .25f),
                Coefficient("Attack.SpeedControl", "SoftTouch", .20f),
                Coefficient("Attack.PowerCapacity", "AttackPower", .70f),
                Coefficient("Attack.PowerCapacity", "Jump", .20f),
                Coefficient("Attack.PowerCapacity", "Coordination", .10f),
                Coefficient("Attack.ContactHeightMeters", "StandingReachMeters", 1f),
                Coefficient("Attack.ContactHeightMeters", "BaseOffsetMeters", .25f),
                Coefficient("Attack.ContactHeightMeters", "Jump", .60f),
                Coefficient("Attack.ApproachMobility", "Mobility", .70f),
                Coefficient("Attack.ApproachMobility", "Coordination", .30f),
                Coefficient("Block.Timing", "BlockTechnique", .50f),
                Coefficient("Block.Timing", "Reaction", .30f),
                Coefficient("Block.Timing", "CourtAwareness", .20f),
                Coefficient("Block.HandControl", "BlockTechnique", .65f),
                Coefficient("Block.HandControl", "Coordination", .25f),
                Coefficient("Block.HandControl", "SoftTouch", .10f),
                Coefficient("Block.ReachHeightMeters", "StandingReachMeters", 1f),
                Coefficient("Block.ReachHeightMeters", "BaseOffsetMeters", .20f),
                Coefficient("Block.ReachHeightMeters", "Jump", .55f),
                Coefficient("Block.LateralMobility", "Mobility", .70f),
                Coefficient("Block.LateralMobility", "Reaction", .30f),
                Coefficient("Defense.Reaction", "Reaction", .70f),
                Coefficient("Defense.Reaction", "CourtAwareness", .30f),
                Coefficient("Defense.PlatformControl", "DefenseTechnique", .65f),
                Coefficient("Defense.PlatformControl", "Coordination", .25f),
                Coefficient("Defense.PlatformControl", "SoftTouch", .10f),
                Coefficient("Defense.CoverageMobility", "Mobility", .70f),
                Coefficient("Defense.CoverageMobility", "Reaction", .20f),
                Coefficient("Defense.CoverageMobility", "CourtAwareness", .10f),
                Coefficient("Defense.Awareness", "CourtAwareness", 1f),
                Coefficient("Receive.FirstTouchControl", "ReceiveTechnique", .65f),
                Coefficient("Receive.FirstTouchControl", "Coordination", .20f),
                Coefficient("Receive.FirstTouchControl", "SoftTouch", .15f),
                Coefficient("Receive.Reaction", "Reaction", .70f),
                Coefficient("Receive.Reaction", "CourtAwareness", .30f),
                Coefficient("Receive.Movement", "Mobility", .70f),
                Coefficient("Receive.Movement", "Coordination", .30f),
                Coefficient("Receive.Awareness", "CourtAwareness", 1f),
                Coefficient("Set.PlacementControl", "SetTechnique", .55f),
                Coefficient("Set.PlacementControl", "Coordination", .25f),
                Coefficient("Set.PlacementControl", "CourtAwareness", .20f),
                Coefficient("Set.TempoControl", "SetTechnique", .50f),
                Coefficient("Set.TempoControl", "Reaction", .30f),
                Coefficient("Set.TempoControl", "CourtAwareness", .20f),
                Coefficient("Set.SoftTouch", "SoftTouch", .60f),
                Coefficient("Set.SoftTouch", "SetTechnique", .25f),
                Coefficient("Set.SoftTouch", "Coordination", .15f),
                Coefficient("Set.Movement", "Mobility", .70f),
                Coefficient("Set.Movement", "Coordination", .30f),
                Coefficient("Set.Awareness", "CourtAwareness", 1f),
                Coefficient("Serve.DirectionControl", "ServeTechnique", .65f),
                Coefficient("Serve.DirectionControl", "Coordination", .20f),
                Coefficient("Serve.DirectionControl", "CourtAwareness", .15f),
                Coefficient("Serve.SpeedControl", "ServeTechnique", .55f),
                Coefficient("Serve.SpeedControl", "Coordination", .25f),
                Coefficient("Serve.SpeedControl", "SoftTouch", .20f),
                Coefficient("Serve.PowerCapacity", "AttackPower", .60f),
                Coefficient("Serve.PowerCapacity", "ServeTechnique", .25f),
                Coefficient("Serve.PowerCapacity", "Coordination", .15f),
                Coefficient("Serve.Consistency", "ServeTechnique", .60f),
                Coefficient("Serve.Consistency", "Coordination", .25f),
                Coefficient("Serve.Consistency", "Reaction", .15f)
            };
        }

        private static MatchAttributeCoefficientV4 Coefficient(
            string outputName,
            string inputName,
            float coefficient)
        {
            return new MatchAttributeCoefficientV4(outputName, inputName, coefficient);
        }

        private static string Key(string outputName, string inputName)
        {
            return outputName + "\n" + inputName;
        }
    }

    internal sealed class FormulaDefinitionV4
    {
        public FormulaDefinitionV4(
            string outputName,
            IReadOnlyList<string> inputNames,
            bool requiresUnitWeight)
        {
            OutputName = outputName;
            InputNames = inputNames;
            RequiresUnitWeight = requiresUnitWeight;
        }

        public string OutputName { get; }
        public IReadOnlyList<string> InputNames { get; }
        public bool RequiresUnitWeight { get; }
    }
}
