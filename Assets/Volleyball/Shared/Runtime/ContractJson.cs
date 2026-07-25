using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public static class ContractJson
    {
        public static string SerializeV4(MatchContextV4 value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            value.Validate();
            return CanonicalMatchJsonV4.SerializeContext(value);
        }

        public static MatchContextV4 DeserializeMatchContextV4(string json)
        {
            try
            {
                var root = StrictJsonV4.ParseObject(json);
                StrictJsonV4.RequireExactProperties(
                    root,
                    "contractVersion",
                    "rulesVersion",
                    "sessionId",
                    "seed",
                    "physicsConfigurationHash",
                    "trajectoryPredictionProviderConfiguration",
                    "formulaVersion",
                    "coefficientVersion",
                    "home",
                    "away",
                    "contextHash");
                var contractVersion = StrictJsonV4.RequiredInt(root, "contractVersion");
                if (contractVersion != ContractVersions.MatchV4)
                {
                    throw new ContractValidationException(
                        "Unsupported match contract version: " + contractVersion + ".");
                }

                var rulesVersion = StrictJsonV4.RequiredInt(root, "rulesVersion");
                var sessionId = StrictJsonV4.RequiredGuid(root, "sessionId");
                var seed = StrictJsonV4.RequiredInt(root, "seed");
                var physicsConfigurationHash =
                    StrictJsonV4.RequiredString(root, "physicsConfigurationHash");
                var predictionConfigurationValue = StrictJsonV4.RequiredObject(
                    root,
                    "trajectoryPredictionProviderConfiguration");
                StrictJsonV4.RequireExactProperties(
                    predictionConfigurationValue,
                    "cacheCapacity",
                    "cacheEvictionPolicy",
                    "predictorVersion",
                    "predictorConfigurationHash");
                var predictionConfiguration =
                    new TrajectoryPredictionProviderConfigurationV4(
                        StrictJsonV4.RequiredInt(
                            predictionConfigurationValue,
                            "cacheCapacity"),
                        (TrajectoryPredictionCacheEvictionPolicyV4)
                        StrictJsonV4.RequiredInt(
                            predictionConfigurationValue,
                            "cacheEvictionPolicy"),
                        StrictJsonV4.RequiredInt(
                            predictionConfigurationValue,
                            "predictorVersion"),
                        StrictJsonV4.RequiredString(
                            predictionConfigurationValue,
                            "predictorConfigurationHash"));
                var formulaVersion = StrictJsonV4.RequiredInt(root, "formulaVersion");
                var coefficientVersion =
                    StrictJsonV4.RequiredInt(root, "coefficientVersion");
                var home = DeserializeTeamV4(
                    StrictJsonV4.RequiredObject(root, "home"),
                    formulaVersion,
                    coefficientVersion);
                var away = DeserializeTeamV4(
                    StrictJsonV4.RequiredObject(root, "away"),
                    formulaVersion,
                    coefficientVersion);
                var suppliedHash = StrictJsonV4.RequiredString(root, "contextHash");
                var context = MatchContextV4.Create(
                    sessionId,
                    seed,
                    home,
                    away,
                    physicsConfigurationHash,
                    predictionConfiguration,
                    rulesVersion);
                if (context.FormulaVersion != formulaVersion ||
                    context.CoefficientVersion != coefficientVersion)
                {
                    throw new ContractValidationException(
                        "Context derivation versions do not match its players.");
                }

                if (!string.Equals(
                        suppliedHash,
                        context.ContextHash,
                        StringComparison.Ordinal))
                {
                    throw new ContractValidationException(
                        "contextHash does not match the native V4 context payload.");
                }

                return context;
            }
            catch (ContractValidationException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is ArgumentException)
            {
                throw new ContractValidationException(
                    "Native V4 context JSON is malformed.",
                    exception);
            }
        }

        public static string SerializeV4(MatchResultV4 value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            value.Validate();
            return CanonicalMatchResultJsonV4.SerializeResult(value);
        }

        public static MatchResultV4 DeserializeMatchResultV4(string json)
        {
            try
            {
                var root = StrictJsonV4.ParseObject(json);
                StrictJsonV4.RequireExactProperties(
                    root,
                    "contractVersion",
                    "sessionId",
                    "contextHash",
                    "winnerTeamId",
                    "homeScore",
                    "awayScore",
                    "ralliesPlayed",
                    "acceptedContacts",
                    "v3RuleTransitionCount",
                    "playerStats",
                    "resultHash");
                var contractVersion = StrictJsonV4.RequiredInt(root, "contractVersion");
                if (contractVersion != ContractVersions.MatchV4)
                {
                    throw new ContractValidationException(
                        "Unsupported match contract version: " + contractVersion + ".");
                }

                var statsValues = StrictJsonV4.RequiredArray(root, "playerStats");
                var stats = new PlayerMatchStatsV4[statsValues.Count];
                for (var index = 0; index < stats.Length; index++)
                {
                    var value = StrictJsonV4.AsObject(
                        statsValues[index],
                        "playerStats[" + index + "]");
                    StrictJsonV4.RequireExactProperties(
                        value,
                        "playerId",
                        "points",
                        "contacts",
                        "errors",
                        "workload");
                    stats[index] = new PlayerMatchStatsV4(
                        new PlayerId(StrictJsonV4.RequiredString(value, "playerId")),
                        StrictJsonV4.RequiredInt(value, "points"),
                        StrictJsonV4.RequiredInt(value, "contacts"),
                        StrictJsonV4.RequiredInt(value, "errors"),
                        StrictJsonV4.RequiredFloat(value, "workload"));
                }

                var suppliedHash = StrictJsonV4.RequiredString(root, "resultHash");
                var result = MatchResultV4.Restore(
                    StrictJsonV4.RequiredGuid(root, "sessionId"),
                    StrictJsonV4.RequiredString(root, "contextHash"),
                    new TeamId(StrictJsonV4.RequiredString(root, "winnerTeamId")),
                    StrictJsonV4.RequiredInt(root, "homeScore"),
                    StrictJsonV4.RequiredInt(root, "awayScore"),
                    StrictJsonV4.RequiredInt(root, "ralliesPlayed"),
                    StrictJsonV4.RequiredInt(root, "acceptedContacts"),
                    StrictJsonV4.RequiredInt(root, "v3RuleTransitionCount"),
                    stats);
                if (!string.Equals(
                        suppliedHash,
                        result.ResultHash,
                        StringComparison.Ordinal))
                {
                    throw new ContractValidationException(
                        "resultHash does not match the native V4 result payload.");
                }

                return result;
            }
            catch (ContractValidationException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is ArgumentException)
            {
                throw new ContractValidationException(
                    "Native V4 result JSON is malformed.",
                    exception);
            }
        }

        public static string SerializeV4(MatchReplayV4 value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            value.Validate();
            return CanonicalMatchReplayJsonV4.Serialize(value);
        }

        public static MatchReplayV4 DeserializeMatchReplayV4(string json)
        {
            try
            {
                return CanonicalMatchReplayJsonV4.Deserialize(
                    StrictJsonV4.ParseObject(json));
            }
            catch (ContractValidationException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is OverflowException ||
                exception is ArgumentException)
            {
                throw new ContractValidationException(
                    "Native V4 replay JSON is malformed.",
                    exception);
            }
        }

        private static TeamSnapshotV4 DeserializeTeamV4(
            StrictJsonObjectV4 value,
            int formulaVersion,
            int coefficientVersion)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "teamId",
                "displayName",
                "side",
                "rotationOrder");
            var playerValues = StrictJsonV4.RequiredArray(value, "rotationOrder");
            var players = new PlayerSnapshotV4[playerValues.Count];
            for (var index = 0; index < players.Length; index++)
            {
                players[index] = DeserializePlayerV4(
                    StrictJsonV4.AsObject(
                        playerValues[index],
                        "rotationOrder[" + index + "]"),
                    formulaVersion,
                    coefficientVersion);
            }

            return new TeamSnapshotV4(
                new TeamId(StrictJsonV4.RequiredString(value, "teamId")),
                StrictJsonV4.RequiredString(value, "displayName"),
                (TeamSide)StrictJsonV4.RequiredInt(value, "side"),
                players);
        }

        private static PlayerSnapshotV4 DeserializePlayerV4(
            StrictJsonObjectV4 value,
            int contextFormulaVersion,
            int contextCoefficientVersion)
        {
            StrictJsonV4.RequireExactProperties(
                value,
                "playerId",
                "displayName",
                "jerseyNumber",
                "position",
                "dominantHand",
                "physical",
                "technical",
                "derived");
            var physical = StrictJsonV4.RequiredObject(value, "physical");
            StrictJsonV4.RequireExactProperties(
                physical,
                "heightMeters",
                "standingReachMeters",
                "jump",
                "mobility",
                "reaction",
                "coordination");
            var technical = StrictJsonV4.RequiredObject(value, "technical");
            StrictJsonV4.RequireExactProperties(
                technical,
                "attackTechnique",
                "attackPower",
                "blockTechnique",
                "defenseTechnique",
                "receiveTechnique",
                "setTechnique",
                "serveTechnique",
                "softTouch",
                "courtAwareness");
            var derived = StrictJsonV4.RequiredObject(value, "derived");
            StrictJsonV4.RequireExactProperties(
                derived,
                "formulaVersion",
                "coefficientVersion",
                "inputFingerprint",
                "resultFingerprint");
            var formulaVersion = StrictJsonV4.RequiredInt(derived, "formulaVersion");
            var coefficientVersion =
                StrictJsonV4.RequiredInt(derived, "coefficientVersion");
            if (formulaVersion != contextFormulaVersion ||
                coefficientVersion != contextCoefficientVersion)
            {
                throw new ContractValidationException(
                    "Player derivation versions do not match the V4 context.");
            }

            var physicalAttributes = new PhysicalBaseAttributesV4(
                StrictJsonV4.RequiredFloat(physical, "heightMeters"),
                StrictJsonV4.RequiredFloat(physical, "standingReachMeters"),
                StrictJsonV4.RequiredFloat(physical, "jump"),
                StrictJsonV4.RequiredFloat(physical, "mobility"),
                StrictJsonV4.RequiredFloat(physical, "reaction"),
                StrictJsonV4.RequiredFloat(physical, "coordination"));
            var technicalAttributes = new TechnicalBaseAttributesV4(
                StrictJsonV4.RequiredFloat(technical, "attackTechnique"),
                StrictJsonV4.RequiredFloat(technical, "attackPower"),
                StrictJsonV4.RequiredFloat(technical, "blockTechnique"),
                StrictJsonV4.RequiredFloat(technical, "defenseTechnique"),
                StrictJsonV4.RequiredFloat(technical, "receiveTechnique"),
                StrictJsonV4.RequiredFloat(technical, "setTechnique"),
                StrictJsonV4.RequiredFloat(technical, "serveTechnique"),
                StrictJsonV4.RequiredFloat(technical, "softTouch"),
                StrictJsonV4.RequiredFloat(technical, "courtAwareness"));
            var config = new MatchAttributeDerivationConfigV4(
                formulaVersion,
                coefficientVersion,
                MatchAttributeDerivationConfigV4.Version1.Coefficients);
            var player = new PlayerSnapshotV4(
                new PlayerId(StrictJsonV4.RequiredString(value, "playerId")),
                StrictJsonV4.RequiredString(value, "displayName"),
                StrictJsonV4.RequiredInt(value, "jerseyNumber"),
                (PlayerPosition)StrictJsonV4.RequiredInt(value, "position"),
                (DominantHandV4)StrictJsonV4.RequiredInt(value, "dominantHand"),
                physicalAttributes,
                technicalAttributes,
                config);
            if (!string.Equals(
                    player.Derived.InputFingerprint,
                    StrictJsonV4.RequiredString(derived, "inputFingerprint"),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    player.Derived.ResultFingerprint,
                    StrictJsonV4.RequiredString(derived, "resultFingerprint"),
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "Serialized derived fingerprints do not match the recomputed V4 player.");
            }

            return player;
        }
    }

    internal enum StrictJsonKindV4
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        Null
    }

    internal sealed class StrictJsonValueV4
    {
        public StrictJsonValueV4(
            StrictJsonKindV4 kind,
            object value)
        {
            Kind = kind;
            Value = value;
        }

        public StrictJsonKindV4 Kind { get; }
        public object Value { get; }
    }

    internal sealed class StrictJsonObjectV4
    {
        public StrictJsonObjectV4(
            Dictionary<string, StrictJsonValueV4> properties)
        {
            Properties = properties;
        }

        public Dictionary<string, StrictJsonValueV4> Properties { get; }
    }

    internal static class StrictJsonV4
    {
        public static StrictJsonObjectV4 ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ContractValidationException(
                    "Native V4 contract JSON is required.");
            }

            var parser = new Parser(json);
            var value = parser.Parse();
            return AsObject(value, "root");
        }

        public static void RequireExactProperties(
            StrictJsonObjectV4 value,
            params string[] expected)
        {
            if (value == null)
            {
                throw new ContractValidationException("JSON object is required.");
            }

            if (value.Properties.Count != expected.Length)
            {
                throw new ContractValidationException(
                    "JSON object fields do not match the native V4 schema.");
            }

            foreach (var name in expected)
            {
                if (!value.Properties.ContainsKey(name))
                {
                    throw new ContractValidationException(
                        "Required native V4 JSON field is missing: " + name + ".");
                }
            }
        }

        public static StrictJsonObjectV4 RequiredObject(
            StrictJsonObjectV4 value,
            string name)
        {
            return AsObject(Required(value, name), name);
        }

        public static List<StrictJsonValueV4> RequiredArray(
            StrictJsonObjectV4 value,
            string name)
        {
            var result = Required(value, name);
            if (result.Kind != StrictJsonKindV4.Array)
            {
                throw new ContractValidationException(
                    name + " must be a JSON array.");
            }

            return (List<StrictJsonValueV4>)result.Value;
        }

        public static StrictJsonObjectV4 RequiredNullableObject(
            StrictJsonObjectV4 value,
            string name)
        {
            var result = Required(value, name);
            if (result.Kind == StrictJsonKindV4.Null)
            {
                return null;
            }

            return AsObject(result, name);
        }

        public static StrictJsonObjectV4 OptionalNullableObject(
            StrictJsonObjectV4 value,
            string name)
        {
            if (value == null ||
                !value.Properties.TryGetValue(name, out var result) ||
                result.Kind == StrictJsonKindV4.Null)
            {
                return null;
            }

            return AsObject(result, name);
        }

        public static string RequiredString(
            StrictJsonObjectV4 value,
            string name)
        {
            var result = Required(value, name);
            if (result.Kind != StrictJsonKindV4.String)
            {
                throw new ContractValidationException(
                    name + " must be a JSON string.");
            }

            return (string)result.Value;
        }

        public static string RequiredNullableString(
            StrictJsonObjectV4 value,
            string name)
        {
            if (value == null || !value.Properties.TryGetValue(name, out var result))
            {
                throw new ContractValidationException(name + " is required.");
            }

            if (result.Kind == StrictJsonKindV4.Null)
            {
                return null;
            }

            if (result.Kind != StrictJsonKindV4.String)
            {
                throw new ContractValidationException(
                    name + " must be a JSON string or null.");
            }

            return (string)result.Value;
        }

        public static int RequiredInt(
            StrictJsonObjectV4 value,
            string name)
        {
            var result = Required(value, name);
            if (result.Kind != StrictJsonKindV4.Number)
            {
                throw new ContractValidationException(
                    name + " must be a JSON integer.");
            }

            var text = (string)result.Value;
            if (text.IndexOf('.') >= 0 ||
                text.IndexOf('e') >= 0 ||
                text.IndexOf('E') >= 0 ||
                !int.TryParse(
                    text,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                throw new ContractValidationException(
                    name + " must be a 32-bit JSON integer.");
            }

            return parsed;
        }

        public static long RequiredLong(
            StrictJsonObjectV4 value,
            string name)
        {
            var result = Required(value, name);
            if (result.Kind != StrictJsonKindV4.Number)
            {
                throw new ContractValidationException(
                    name + " must be a JSON integer.");
            }

            var text = (string)result.Value;
            if (text.IndexOf('.') >= 0 ||
                text.IndexOf('e') >= 0 ||
                text.IndexOf('E') >= 0 ||
                !long.TryParse(
                    text,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                throw new ContractValidationException(
                    name + " must be a 64-bit JSON integer.");
            }

            return parsed;
        }

        public static bool RequiredBoolean(
            StrictJsonObjectV4 value,
            string name)
        {
            var result = Required(value, name);
            if (result.Kind != StrictJsonKindV4.Boolean)
            {
                throw new ContractValidationException(
                    name + " must be a JSON boolean.");
            }

            return (bool)result.Value;
        }

        public static string ToJson(StrictJsonObjectV4 value)
        {
            var output = new StringBuilder();
            AppendJson(
                output,
                new StrictJsonValueV4(StrictJsonKindV4.Object, value));
            return output.ToString();
        }

        public static float RequiredFloat(
            StrictJsonObjectV4 value,
            string name)
        {
            var result = Required(value, name);
            if (result.Kind != StrictJsonKindV4.Number ||
                !float.TryParse(
                    (string)result.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed) ||
                float.IsNaN(parsed) ||
                float.IsInfinity(parsed))
            {
                throw new ContractValidationException(
                    name + " must be a finite JSON number.");
            }

            return parsed;
        }

        public static Guid RequiredGuid(
            StrictJsonObjectV4 value,
            string name)
        {
            var text = RequiredString(value, name);
            if (!Guid.TryParseExact(text, "D", out var parsed) ||
                parsed == Guid.Empty)
            {
                throw new ContractValidationException(
                    name + " must be a non-empty canonical GUID.");
            }

            return parsed;
        }

        public static StrictJsonObjectV4 AsObject(
            StrictJsonValueV4 value,
            string name)
        {
            if (value == null || value.Kind != StrictJsonKindV4.Object)
            {
                throw new ContractValidationException(
                    name + " must be a JSON object.");
            }

            return (StrictJsonObjectV4)value.Value;
        }

        private static StrictJsonValueV4 Required(
            StrictJsonObjectV4 value,
            string name)
        {
            if (value == null ||
                !value.Properties.TryGetValue(name, out var result))
            {
                throw new ContractValidationException(
                    "Required native V4 JSON field is missing: " + name + ".");
            }

            return result;
        }

        private static void AppendJson(
            StringBuilder output,
            StrictJsonValueV4 value)
        {
            switch (value.Kind)
            {
                case StrictJsonKindV4.Object:
                {
                    output.Append('{');
                    var first = true;
                    foreach (var pair in
                             ((StrictJsonObjectV4)value.Value).Properties)
                    {
                        if (!first) output.Append(',');
                        first = false;
                        AppendJsonString(output, pair.Key);
                        output.Append(':');
                        AppendJson(output, pair.Value);
                    }

                    output.Append('}');
                    break;
                }
                case StrictJsonKindV4.Array:
                {
                    output.Append('[');
                    var values = (List<StrictJsonValueV4>)value.Value;
                    for (var index = 0; index < values.Count; index++)
                    {
                        if (index > 0) output.Append(',');
                        AppendJson(output, values[index]);
                    }

                    output.Append(']');
                    break;
                }
                case StrictJsonKindV4.String:
                    AppendJsonString(output, (string)value.Value);
                    break;
                case StrictJsonKindV4.Number:
                    output.Append((string)value.Value);
                    break;
                case StrictJsonKindV4.Boolean:
                    output.Append((bool)value.Value ? "true" : "false");
                    break;
                case StrictJsonKindV4.Null:
                    output.Append("null");
                    break;
                default:
                    throw new ContractValidationException(
                        "Native V4 JSON contains an unsupported value.");
            }
        }

        private static void AppendJsonString(
            StringBuilder output,
            string value)
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
                                .Append(((int)character).ToString(
                                    "x4",
                                    CultureInfo.InvariantCulture));
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

        private sealed class Parser
        {
            private readonly string _json;
            private int _index;

            public Parser(string json)
            {
                _json = json;
            }

            public StrictJsonValueV4 Parse()
            {
                SkipWhitespace();
                var value = ParseValue();
                SkipWhitespace();
                if (_index != _json.Length)
                {
                    Fail("Unexpected content after the JSON value.");
                }

                return value;
            }

            private StrictJsonValueV4 ParseValue()
            {
                if (_index >= _json.Length)
                {
                    Fail("Unexpected end of JSON.");
                }

                switch (_json[_index])
                {
                    case '{':
                        return ParseObjectValue();
                    case '[':
                        return ParseArrayValue();
                    case '"':
                        return new StrictJsonValueV4(
                            StrictJsonKindV4.String,
                            ParseString());
                    case 't':
                        ParseLiteral("true");
                        return new StrictJsonValueV4(
                            StrictJsonKindV4.Boolean,
                            true);
                    case 'f':
                        ParseLiteral("false");
                        return new StrictJsonValueV4(
                            StrictJsonKindV4.Boolean,
                            false);
                    case 'n':
                        ParseLiteral("null");
                        return new StrictJsonValueV4(
                            StrictJsonKindV4.Null,
                            null);
                    default:
                        if (_json[_index] == '-' ||
                            (_json[_index] >= '0' && _json[_index] <= '9'))
                        {
                            return new StrictJsonValueV4(
                                StrictJsonKindV4.Number,
                                ParseNumber());
                        }

                        Fail("Unexpected JSON token.");
                        return null;
                }
            }

            private StrictJsonValueV4 ParseObjectValue()
            {
                _index++;
                SkipWhitespace();
                var properties =
                    new Dictionary<string, StrictJsonValueV4>(StringComparer.Ordinal);
                if (Consume('}'))
                {
                    return new StrictJsonValueV4(
                        StrictJsonKindV4.Object,
                        new StrictJsonObjectV4(properties));
                }

                while (true)
                {
                    if (_index >= _json.Length || _json[_index] != '"')
                    {
                        Fail("JSON object property name must be a string.");
                    }

                    var name = ParseString();
                    SkipWhitespace();
                    Require(':');
                    SkipWhitespace();
                    if (!properties.TryAdd(name, ParseValue()))
                    {
                        Fail("Duplicate JSON property: " + name + ".");
                    }

                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        break;
                    }

                    Require(',');
                    SkipWhitespace();
                }

                return new StrictJsonValueV4(
                    StrictJsonKindV4.Object,
                    new StrictJsonObjectV4(properties));
            }

            private StrictJsonValueV4 ParseArrayValue()
            {
                _index++;
                SkipWhitespace();
                var values = new List<StrictJsonValueV4>();
                if (Consume(']'))
                {
                    return new StrictJsonValueV4(
                        StrictJsonKindV4.Array,
                        values);
                }

                while (true)
                {
                    values.Add(ParseValue());
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        break;
                    }

                    Require(',');
                    SkipWhitespace();
                }

                return new StrictJsonValueV4(
                    StrictJsonKindV4.Array,
                    values);
            }

            private string ParseString()
            {
                Require('"');
                var output = new StringBuilder();
                while (_index < _json.Length)
                {
                    var character = _json[_index++];
                    if (character == '"')
                    {
                        return output.ToString();
                    }

                    if (character == '\\')
                    {
                        if (_index >= _json.Length)
                        {
                            Fail("Unterminated JSON escape sequence.");
                        }

                        var escaped = _json[_index++];
                        switch (escaped)
                        {
                            case '"': output.Append('"'); break;
                            case '\\': output.Append('\\'); break;
                            case '/': output.Append('/'); break;
                            case 'b': output.Append('\b'); break;
                            case 'f': output.Append('\f'); break;
                            case 'n': output.Append('\n'); break;
                            case 'r': output.Append('\r'); break;
                            case 't': output.Append('\t'); break;
                            case 'u':
                                output.Append(ParseUnicodeEscape());
                                break;
                            default:
                                Fail("Unsupported JSON escape sequence.");
                                break;
                        }
                    }
                    else
                    {
                        if (character < 32)
                        {
                            Fail("Unescaped control character in JSON string.");
                        }

                        output.Append(character);
                    }
                }

                Fail("Unterminated JSON string.");
                return null;
            }

            private char ParseUnicodeEscape()
            {
                if (_index + 4 > _json.Length)
                {
                    Fail("Incomplete JSON unicode escape.");
                }

                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var character = _json[_index++];
                    value <<= 4;
                    if (character >= '0' && character <= '9')
                    {
                        value += character - '0';
                    }
                    else if (character >= 'a' && character <= 'f')
                    {
                        value += character - 'a' + 10;
                    }
                    else if (character >= 'A' && character <= 'F')
                    {
                        value += character - 'A' + 10;
                    }
                    else
                    {
                        Fail("Invalid JSON unicode escape.");
                    }
                }

                return (char)value;
            }

            private string ParseNumber()
            {
                var start = _index;
                Consume('-');
                if (_index >= _json.Length)
                {
                    Fail("Incomplete JSON number.");
                }

                if (Consume('0'))
                {
                    if (_index < _json.Length &&
                        _json[_index] >= '0' &&
                        _json[_index] <= '9')
                    {
                        Fail("JSON numbers cannot contain leading zeroes.");
                    }
                }
                else
                {
                    RequireDigits();
                }

                if (Consume('.'))
                {
                    RequireDigits();
                }

                if (_index < _json.Length &&
                    (_json[_index] == 'e' || _json[_index] == 'E'))
                {
                    _index++;
                    if (_index < _json.Length &&
                        (_json[_index] == '+' || _json[_index] == '-'))
                    {
                        _index++;
                    }

                    RequireDigits();
                }

                return _json.Substring(start, _index - start);
            }

            private void RequireDigits()
            {
                var start = _index;
                while (_index < _json.Length &&
                       _json[_index] >= '0' &&
                       _json[_index] <= '9')
                {
                    _index++;
                }

                if (_index == start)
                {
                    Fail("JSON number requires digits.");
                }
            }

            private void ParseLiteral(string literal)
            {
                if (_index + literal.Length > _json.Length ||
                    !string.Equals(
                        _json.Substring(_index, literal.Length),
                        literal,
                        StringComparison.Ordinal))
                {
                    Fail("Invalid JSON literal.");
                }

                _index += literal.Length;
            }

            private void Require(char expected)
            {
                if (!Consume(expected))
                {
                    Fail("Expected '" + expected + "'.");
                }
            }

            private bool Consume(char expected)
            {
                if (_index < _json.Length && _json[_index] == expected)
                {
                    _index++;
                    return true;
                }

                return false;
            }

            private void SkipWhitespace()
            {
                while (_index < _json.Length)
                {
                    var character = _json[_index];
                    if (character != ' ' &&
                        character != '\t' &&
                        character != '\r' &&
                        character != '\n')
                    {
                        break;
                    }

                    _index++;
                }
            }

            private static void Fail(string message)
            {
                throw new ContractValidationException(
                    "Native V4 contract JSON is malformed: " + message);
            }
        }
    }
}
