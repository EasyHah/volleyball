using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public static class MatchAttributeDerivationV4
    {
        public static DerivedMatchAttributesV4 Derive(
            PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical,
            DominantHandV4 dominantHand,
            MatchAttributeDerivationConfigV4 config)
        {
            if (physical == null)
            {
                throw new ContractValidationException("physical is required.");
            }

            if (technical == null)
            {
                throw new ContractValidationException("technical is required.");
            }

            if (config == null)
            {
                throw new ContractValidationException("config is required.");
            }

            ContractGuard.DefinedEnum(dominantHand, nameof(dominantHand));

            var inputs = CreateInputs(physical, technical);
            var results = new Dictionary<string, float>(StringComparer.Ordinal);
            var explanations = new List<MatchAttributeExplanationV4>();
            foreach (var definition in MatchAttributeDerivationConfigV4.FormulaDefinitions)
            {
                var coefficients = new float[definition.InputNames.Count];
                var result = 0f;
                for (var index = 0; index < definition.InputNames.Count; index++)
                {
                    var inputName = definition.InputNames[index];
                    var coefficient = config.GetCoefficient(definition.OutputName, inputName);
                    coefficients[index] = coefficient;
                    result += inputs[InputKey(definition.OutputName, inputName)] * coefficient;
                }

                results.Add(definition.OutputName, result);
                explanations.Add(new MatchAttributeExplanationV4(
                    definition.OutputName,
                    definition.InputNames,
                    coefficients,
                    result));
            }

            var attributes = CreateAttributes(results, dominantHand);
            var inputCanonical = CanonicalInput(physical, technical, dominantHand);
            var inputFingerprint = CanonicalMatchAttributeHashV4.Sha256(inputCanonical);
            var resultPayload = CanonicalResultPayload(
                attributes,
                config.FormulaVersion,
                config.CoefficientVersion,
                inputFingerprint,
                explanations);
            var resultFingerprint = CanonicalMatchAttributeHashV4.Sha256(resultPayload);
            var canonicalBytes = Encoding.UTF8.GetBytes(
                AppendResultFingerprint(resultPayload, resultFingerprint));

            return new DerivedMatchAttributesV4(
                attributes,
                config.FormulaVersion,
                config.CoefficientVersion,
                inputFingerprint,
                resultFingerprint,
                explanations,
                canonicalBytes);
        }

        private static Dictionary<string, float> CreateInputs(
            PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical)
        {
            var inputs = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["HeightMeters"] = physical.HeightMeters,
                ["StandingReachMeters"] = physical.StandingReachMeters,
                ["Jump"] = physical.Jump,
                ["Mobility"] = physical.Mobility,
                ["Reaction"] = physical.Reaction,
                ["Coordination"] = physical.Coordination,
                ["AttackTechnique"] = technical.AttackTechnique,
                ["AttackPower"] = technical.AttackPower,
                ["BlockTechnique"] = technical.BlockTechnique,
                ["DefenseTechnique"] = technical.DefenseTechnique,
                ["ReceiveTechnique"] = technical.ReceiveTechnique,
                ["SetTechnique"] = technical.SetTechnique,
                ["ServeTechnique"] = technical.ServeTechnique,
                ["SoftTouch"] = technical.SoftTouch,
                ["CourtAwareness"] = technical.CourtAwareness,
                [InputKey("Attack.ContactHeightMeters", "BaseOffsetMeters")] = 1f,
                [InputKey("Block.ReachHeightMeters", "BaseOffsetMeters")] = 1f
            };
            return inputs;
        }

        private static string InputKey(string outputName, string inputName)
        {
            return inputName == "BaseOffsetMeters" ? outputName + "/" + inputName : inputName;
        }

        private static MatchAttributesV4 CreateAttributes(
            IReadOnlyDictionary<string, float> values,
            DominantHandV4 dominantHand)
        {
            return new MatchAttributesV4(
                new AttackAttributesV4(
                    values["Attack.DirectionControl"],
                    values["Attack.SpeedControl"],
                    values["Attack.PowerCapacity"],
                    values["Attack.ContactHeightMeters"],
                    values["Attack.ApproachMobility"]),
                new BlockAttributesV4(
                    values["Block.Timing"],
                    values["Block.HandControl"],
                    values["Block.ReachHeightMeters"],
                    values["Block.LateralMobility"]),
                new DefenseAttributesV4(
                    values["Defense.Reaction"],
                    values["Defense.PlatformControl"],
                    values["Defense.CoverageMobility"],
                    values["Defense.Awareness"]),
                new ReceiveAttributesV4(
                    values["Receive.FirstTouchControl"],
                    values["Receive.Reaction"],
                    values["Receive.Movement"],
                    values["Receive.Awareness"]),
                new SetAttributesV4(
                    values["Set.PlacementControl"],
                    values["Set.TempoControl"],
                    values["Set.SoftTouch"],
                    values["Set.Movement"],
                    values["Set.Awareness"]),
                new ServeAttributesV4(
                    values["Serve.DirectionControl"],
                    values["Serve.SpeedControl"],
                    values["Serve.PowerCapacity"],
                    values["Serve.Consistency"]),
                dominantHand);
        }

        private static string CanonicalInput(
            PhysicalBaseAttributesV4 physical,
            TechnicalBaseAttributesV4 technical,
            DominantHandV4 dominantHand)
        {
            var output = new StringBuilder();
            output.Append("{\"physical\":{");
            AppendFloat(output, "heightMeters", physical.HeightMeters);
            AppendFloat(output, "standingReachMeters", physical.StandingReachMeters);
            AppendFloat(output, "jump", physical.Jump);
            AppendFloat(output, "mobility", physical.Mobility);
            AppendFloat(output, "reaction", physical.Reaction);
            AppendFloat(output, "coordination", physical.Coordination);
            output.Append("},\"technical\":{");
            AppendFloat(output, "attackTechnique", technical.AttackTechnique);
            AppendFloat(output, "attackPower", technical.AttackPower);
            AppendFloat(output, "blockTechnique", technical.BlockTechnique);
            AppendFloat(output, "defenseTechnique", technical.DefenseTechnique);
            AppendFloat(output, "receiveTechnique", technical.ReceiveTechnique);
            AppendFloat(output, "setTechnique", technical.SetTechnique);
            AppendFloat(output, "serveTechnique", technical.ServeTechnique);
            AppendFloat(output, "softTouch", technical.SoftTouch);
            AppendFloat(output, "courtAwareness", technical.CourtAwareness);
            output.Append("},\"dominantHand\":")
                .Append(((int)dominantHand).ToString(CultureInfo.InvariantCulture))
                .Append('}');
            return output.ToString();
        }

        private static string CanonicalResultPayload(
            MatchAttributesV4 attributes,
            int formulaVersion,
            int coefficientVersion,
            string inputFingerprint,
            IReadOnlyList<MatchAttributeExplanationV4> explanations)
        {
            var output = new StringBuilder();
            output.Append("{\"attributes\":{\"attack\":{");
            AppendFloat(output, "directionControl", attributes.Attack.DirectionControl);
            AppendFloat(output, "speedControl", attributes.Attack.SpeedControl);
            AppendFloat(output, "powerCapacity", attributes.Attack.PowerCapacity);
            AppendFloat(output, "contactHeightMeters", attributes.Attack.ContactHeightMeters);
            AppendFloat(output, "approachMobility", attributes.Attack.ApproachMobility);
            output.Append("},\"block\":{");
            AppendFloat(output, "timing", attributes.Block.Timing);
            AppendFloat(output, "handControl", attributes.Block.HandControl);
            AppendFloat(output, "reachHeightMeters", attributes.Block.ReachHeightMeters);
            AppendFloat(output, "lateralMobility", attributes.Block.LateralMobility);
            output.Append("},\"defense\":{");
            AppendFloat(output, "reaction", attributes.Defense.Reaction);
            AppendFloat(output, "platformControl", attributes.Defense.PlatformControl);
            AppendFloat(output, "coverageMobility", attributes.Defense.CoverageMobility);
            AppendFloat(output, "awareness", attributes.Defense.Awareness);
            output.Append("},\"receive\":{");
            AppendFloat(output, "firstTouchControl", attributes.Receive.FirstTouchControl);
            AppendFloat(output, "reaction", attributes.Receive.Reaction);
            AppendFloat(output, "movement", attributes.Receive.Movement);
            AppendFloat(output, "awareness", attributes.Receive.Awareness);
            output.Append("},\"set\":{");
            AppendFloat(output, "placementControl", attributes.Set.PlacementControl);
            AppendFloat(output, "tempoControl", attributes.Set.TempoControl);
            AppendFloat(output, "softTouch", attributes.Set.SoftTouch);
            AppendFloat(output, "movement", attributes.Set.Movement);
            AppendFloat(output, "awareness", attributes.Set.Awareness);
            output.Append("},\"serve\":{");
            AppendFloat(output, "directionControl", attributes.Serve.DirectionControl);
            AppendFloat(output, "speedControl", attributes.Serve.SpeedControl);
            AppendFloat(output, "powerCapacity", attributes.Serve.PowerCapacity);
            AppendFloat(output, "consistency", attributes.Serve.Consistency);
            output.Append("},\"dominantHand\":")
                .Append(((int)attributes.DominantHand).ToString(CultureInfo.InvariantCulture))
                .Append("},\"formulaVersion\":")
                .Append(formulaVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\"coefficientVersion\":")
                .Append(coefficientVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\"inputFingerprint\":");
            AppendString(output, inputFingerprint);
            output.Append(",\"explanations\":[");
            for (var explanationIndex = 0; explanationIndex < explanations.Count; explanationIndex++)
            {
                if (explanationIndex > 0)
                {
                    output.Append(',');
                }

                var explanation = explanations[explanationIndex];
                output.Append("{\"outputName\":");
                AppendString(output, explanation.OutputName);
                output.Append(",\"inputNames\":[");
                for (var inputIndex = 0; inputIndex < explanation.InputNames.Count; inputIndex++)
                {
                    if (inputIndex > 0)
                    {
                        output.Append(',');
                    }

                    AppendString(output, explanation.InputNames[inputIndex]);
                }

                output.Append("],\"coefficients\":[");
                for (var coefficientIndex = 0; coefficientIndex < explanation.Coefficients.Count; coefficientIndex++)
                {
                    if (coefficientIndex > 0)
                    {
                        output.Append(',');
                    }

                    output.Append(explanation.Coefficients[coefficientIndex]
                        .ToString("R", CultureInfo.InvariantCulture));
                }

                output.Append("],\"result\":")
                    .Append(explanation.Result.ToString("R", CultureInfo.InvariantCulture))
                    .Append('}');
            }

            output.Append("]}");
            return output.ToString();
        }

        private static string AppendResultFingerprint(string resultPayload, string resultFingerprint)
        {
            var output = new StringBuilder(resultPayload.Length + 90);
            output.Append(resultPayload, 0, resultPayload.Length - 1);
            output.Append(",\"resultFingerprint\":");
            AppendString(output, resultFingerprint);
            output.Append('}');
            return output.ToString();
        }

        private static void AppendFloat(StringBuilder output, string name, float value)
        {
            if (output[output.Length - 1] != '{')
            {
                output.Append(',');
            }

            output.Append('"')
                .Append(name)
                .Append("\":")
                .Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder output, string value)
        {
            output.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 32)
                        {
                            output.Append("\\u")
                                .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            output.Append(character);
                        }

                        break;
                }
            }

            output.Append('"');
        }
    }

    internal static class CanonicalMatchAttributeHashV4
    {
        public static string Sha256(string canonical)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var hex = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++)
            {
                hex.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }
    }
}
