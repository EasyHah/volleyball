using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    internal enum CareerSaveVersionClassification
    {
        Malformed = 0,
        Unsupported = 1,
        Supported = 2
    }

    internal readonly struct CareerSaveVersionClassificationResult
    {
        public CareerSaveVersionClassificationResult(
            CareerSaveVersionClassification kind,
            long? observedSchemaVersion)
        {
            Kind = kind;
            ObservedSchemaVersion = observedSchemaVersion;
        }

        public CareerSaveVersionClassification Kind { get; }

        public long? ObservedSchemaVersion { get; }
    }

    public sealed class CareerSaveVersionNotSupportedException : FormatException
    {
        public CareerSaveVersionNotSupportedException(long? observedSchemaVersion)
            : base(FormatMessage(observedSchemaVersion))
        {
            ObservedSchemaVersion = observedSchemaVersion;
        }

        public long? ObservedSchemaVersion { get; }

        private static string FormatMessage(long? observedSchemaVersion)
        {
            return observedSchemaVersion.HasValue
                ? "Career save schema/version tuple is unsupported (schema " +
                  observedSchemaVersion.Value + ")."
                : "Career save schema/version tuple is unsupported.";
        }
    }

    internal static class CareerSaveVersionClassifier
    {
        public static CareerSaveVersionClassificationResult Classify(byte[] utf8Json)
        {
            if (utf8Json == null)
            {
                throw new ArgumentNullException(nameof(utf8Json));
            }

            try
            {
                var envelope = StandardJsonVersionEnvelopeReader.Parse(utf8Json);
                if (envelope.SchemaVersionExceedsInt64)
                {
                    return Unsupported();
                }

                var schemaVersion = envelope.ObservedSchemaVersion.Value;
                if (schemaVersion == 1 ||
                    schemaVersion > CareerSaveVersions.CurrentSchemaVersion)
                {
                    return Unsupported(schemaVersion);
                }

                if (schemaVersion != CareerSaveVersions.CurrentSchemaVersion)
                {
                    return Malformed(schemaVersion);
                }

                return ClassifyCurrentSchema(utf8Json, schemaVersion);
            }
            catch (FormatException)
            {
                return Malformed();
            }
            catch (KeyNotFoundException)
            {
                return Malformed();
            }
        }

        private static CareerSaveVersionClassificationResult ClassifyCurrentSchema(
            byte[] utf8Json,
            long observedSchemaVersion)
        {
            var root = StrictJsonReader.Parse(utf8Json);
            if (root.Kind != StrictJsonKind.Object)
            {
                return Malformed();
            }

            var versionsValue = root.ObjectValue.Get("versions");
            if (versionsValue.Kind != StrictJsonKind.Object)
            {
                return Malformed();
            }

            var versions = versionsValue.ObjectValue;
            var schemaValue = versions.Get("schemaVersion");
            if (schemaValue.Kind != StrictJsonKind.Integer ||
                schemaValue.IntegerValue != CareerSaveVersions.CurrentSchemaVersion ||
                versions.ContainsUnknownProperty(
                    "schemaVersion",
                    "contentVersion",
                    "rulesetVersion",
                    "contractVersion",
                    "careerRandomAlgorithmVersion"))
            {
                return Malformed(observedSchemaVersion);
            }

            if (!TryPositiveInt(versions, "contentVersion", out var contentVersion) ||
                !TryPositiveInt(versions, "rulesetVersion", out var rulesetVersion) ||
                !TryPositiveInt(versions, "contractVersion", out var contractVersion) ||
                !TryPositiveInt(
                    versions,
                    "careerRandomAlgorithmVersion",
                    out var careerRandomAlgorithmVersion))
            {
                return Malformed(observedSchemaVersion);
            }

            var supported = contentVersion == CareerSaveVersions.CurrentContentVersion &&
                            rulesetVersion == CareerSaveVersions.CurrentRulesetVersion &&
                            contractVersion == CareerSaveVersions.CurrentContractVersion &&
                            careerRandomAlgorithmVersion ==
                            CareerSaveVersions.CurrentCareerRandomAlgorithmVersion;
            return new CareerSaveVersionClassificationResult(
                supported
                    ? CareerSaveVersionClassification.Supported
                    : CareerSaveVersionClassification.Unsupported,
                observedSchemaVersion);
        }

        private static bool TryPositiveInt(
            StrictJsonObject versions,
            string name,
            out int value)
        {
            value = 0;
            StrictJsonValue candidate;
            try
            {
                candidate = versions.Get(name);
            }
            catch (KeyNotFoundException)
            {
                return false;
            }

            if (candidate.Kind != StrictJsonKind.Integer ||
                candidate.IntegerValue < 1 ||
                candidate.IntegerValue > int.MaxValue)
            {
                return false;
            }

            value = (int)candidate.IntegerValue;
            return true;
        }

        private static CareerSaveVersionClassificationResult Unsupported(
            long? observedSchemaVersion = null)
        {
            return new CareerSaveVersionClassificationResult(
                CareerSaveVersionClassification.Unsupported,
                observedSchemaVersion);
        }

        private static CareerSaveVersionClassificationResult Malformed(
            long? observedSchemaVersion = null)
        {
            return new CareerSaveVersionClassificationResult(
                CareerSaveVersionClassification.Malformed,
                observedSchemaVersion);
        }

        private readonly struct StandardJsonVersionEnvelope
        {
            public StandardJsonVersionEnvelope(
                long? observedSchemaVersion,
                bool schemaVersionExceedsInt64)
            {
                ObservedSchemaVersion = observedSchemaVersion;
                SchemaVersionExceedsInt64 = schemaVersionExceedsInt64;
            }

            public long? ObservedSchemaVersion { get; }

            public bool SchemaVersionExceedsInt64 { get; }
        }

        private sealed class StandardJsonVersionEnvelopeReader
        {
            private const int MaximumNestingDepth = 256;
            private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

            private readonly string _json;
            private int _index;

            private StandardJsonVersionEnvelopeReader(string json)
            {
                _json = json;
            }

            public static StandardJsonVersionEnvelope Parse(byte[] utf8Json)
            {
                string json;
                try
                {
                    json = StrictUtf8.GetString(utf8Json);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new FormatException(
                        "Career save version envelope is not valid UTF-8.",
                        exception);
                }

                return new StandardJsonVersionEnvelopeReader(json).ParseDocument();
            }

            private StandardJsonVersionEnvelope ParseDocument()
            {
                SkipWhitespace();
                if (!HasCurrent('{'))
                {
                    throw InvalidJson();
                }

                var envelope = ParseRootObject(1);
                SkipWhitespace();
                if (_index != _json.Length)
                {
                    throw InvalidJson();
                }

                return envelope;
            }

            private StandardJsonVersionEnvelope ParseRootObject(int depth)
            {
                RequireDepth(depth);
                Expect('{');
                SkipWhitespace();
                var versionsSeen = false;
                var envelope = default(StandardJsonVersionEnvelope);
                if (TryConsume('}'))
                {
                    throw InvalidJson();
                }

                while (true)
                {
                    var propertyName = ParsePropertyName();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    if (string.Equals(propertyName, "versions", StringComparison.Ordinal))
                    {
                        if (versionsSeen || !HasCurrent('{'))
                        {
                            throw InvalidJson();
                        }

                        versionsSeen = true;
                        envelope = ParseVersionsObject(depth + 1);
                    }
                    else
                    {
                        ParseValue(depth + 1);
                    }

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        break;
                    }

                    Expect(',');
                    SkipWhitespace();
                }

                if (!versionsSeen)
                {
                    throw InvalidJson();
                }

                return envelope;
            }

            private StandardJsonVersionEnvelope ParseVersionsObject(int depth)
            {
                RequireDepth(depth);
                Expect('{');
                SkipWhitespace();
                var schemaSeen = false;
                long? observedSchemaVersion = null;
                var schemaVersionExceedsInt64 = false;
                if (TryConsume('}'))
                {
                    throw InvalidJson();
                }

                while (true)
                {
                    var propertyName = ParsePropertyName();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    if (string.Equals(
                            propertyName,
                            "schemaVersion",
                            StringComparison.Ordinal))
                    {
                        if (schemaSeen)
                        {
                            throw InvalidJson();
                        }

                        schemaSeen = true;
                        ParsePositiveSchemaVersion(
                            out observedSchemaVersion,
                            out schemaVersionExceedsInt64);
                    }
                    else
                    {
                        ParseValue(depth + 1);
                    }

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        break;
                    }

                    Expect(',');
                    SkipWhitespace();
                }

                if (!schemaSeen)
                {
                    throw InvalidJson();
                }

                return new StandardJsonVersionEnvelope(
                    observedSchemaVersion,
                    schemaVersionExceedsInt64);
            }

            private void ParsePositiveSchemaVersion(
                out long? observedSchemaVersion,
                out bool schemaVersionExceedsInt64)
            {
                var token = ParseNumber();
                if (token[0] == '-' ||
                    token.IndexOf('.') >= 0 ||
                    token.IndexOf('e') >= 0 ||
                    token.IndexOf('E') >= 0 ||
                    string.Equals(token, "0", StringComparison.Ordinal))
                {
                    throw InvalidJson();
                }

                if (long.TryParse(
                        token,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    if (value < 1)
                    {
                        throw InvalidJson();
                    }

                    observedSchemaVersion = value;
                    schemaVersionExceedsInt64 = false;
                    return;
                }

                observedSchemaVersion = null;
                schemaVersionExceedsInt64 = true;
            }

            private void ParseValue(int depth)
            {
                SkipWhitespace();
                if (_index >= _json.Length)
                {
                    throw InvalidJson();
                }

                switch (_json[_index])
                {
                    case '{':
                        ParseObject(depth);
                        return;
                    case '[':
                        ParseArray(depth);
                        return;
                    case '"':
                        ParseString();
                        return;
                    case 't':
                        ConsumeLiteral("true");
                        return;
                    case 'f':
                        ConsumeLiteral("false");
                        return;
                    case 'n':
                        ConsumeLiteral("null");
                        return;
                    default:
                        if (_json[_index] == '-' || IsDigit(_json[_index]))
                        {
                            ParseNumber();
                            return;
                        }

                        throw InvalidJson();
                }
            }

            private void ParseObject(int depth)
            {
                RequireDepth(depth);
                Expect('{');
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return;
                }

                while (true)
                {
                    ParsePropertyName();
                    SkipWhitespace();
                    Expect(':');
                    ParseValue(depth + 1);
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return;
                    }

                    Expect(',');
                    SkipWhitespace();
                }
            }

            private void ParseArray(int depth)
            {
                RequireDepth(depth);
                Expect('[');
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return;
                }

                while (true)
                {
                    ParseValue(depth + 1);
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return;
                    }

                    Expect(',');
                    SkipWhitespace();
                }
            }

            private string ParsePropertyName()
            {
                if (!HasCurrent('"'))
                {
                    throw InvalidJson();
                }

                return ParseString();
            }

            private string ParseString()
            {
                Expect('"');
                var value = new StringBuilder();
                while (_index < _json.Length)
                {
                    var character = _json[_index++];
                    if (character == '"')
                    {
                        return value.ToString();
                    }

                    if (character == '\\')
                    {
                        AppendEscapedCharacter(value);
                        continue;
                    }

                    if (character < 0x20 || char.IsLowSurrogate(character))
                    {
                        throw InvalidJson();
                    }

                    if (char.IsHighSurrogate(character))
                    {
                        if (_index >= _json.Length ||
                            !char.IsLowSurrogate(_json[_index]))
                        {
                            throw InvalidJson();
                        }

                        value.Append(character);
                        value.Append(_json[_index++]);
                        continue;
                    }

                    value.Append(character);
                }

                throw InvalidJson();
            }

            private void AppendEscapedCharacter(StringBuilder value)
            {
                if (_index >= _json.Length)
                {
                    throw InvalidJson();
                }

                var escape = _json[_index++];
                switch (escape)
                {
                    case '"':
                        value.Append('"');
                        return;
                    case '\\':
                        value.Append('\\');
                        return;
                    case '/':
                        value.Append('/');
                        return;
                    case 'b':
                        value.Append('\b');
                        return;
                    case 'f':
                        value.Append('\f');
                        return;
                    case 'n':
                        value.Append('\n');
                        return;
                    case 'r':
                        value.Append('\r');
                        return;
                    case 't':
                        value.Append('\t');
                        return;
                    case 'u':
                        AppendUnicodeEscape(value);
                        return;
                    default:
                        throw InvalidJson();
                }
            }

            private void AppendUnicodeEscape(StringBuilder value)
            {
                var character = (char)ReadHexQuad();
                if (char.IsLowSurrogate(character))
                {
                    throw InvalidJson();
                }

                if (!char.IsHighSurrogate(character))
                {
                    value.Append(character);
                    return;
                }

                if (_index + 2 > _json.Length ||
                    _json[_index] != '\\' ||
                    _json[_index + 1] != 'u')
                {
                    throw InvalidJson();
                }

                _index += 2;
                var lowSurrogate = (char)ReadHexQuad();
                if (!char.IsLowSurrogate(lowSurrogate))
                {
                    throw InvalidJson();
                }

                value.Append(character);
                value.Append(lowSurrogate);
            }

            private int ReadHexQuad()
            {
                if (_index + 4 > _json.Length)
                {
                    throw InvalidJson();
                }

                var value = 0;
                for (var offset = 0; offset < 4; offset++)
                {
                    var digit = HexValue(_json[_index++]);
                    if (digit < 0)
                    {
                        throw InvalidJson();
                    }

                    value = (value * 16) + digit;
                }

                return value;
            }

            private string ParseNumber()
            {
                var start = _index;
                if (TryConsume('-') && _index >= _json.Length)
                {
                    throw InvalidJson();
                }

                if (TryConsume('0'))
                {
                    if (_index < _json.Length && IsDigit(_json[_index]))
                    {
                        throw InvalidJson();
                    }
                }
                else
                {
                    if (_index >= _json.Length ||
                        _json[_index] < '1' ||
                        _json[_index] > '9')
                    {
                        throw InvalidJson();
                    }

                    _index++;
                    while (_index < _json.Length && IsDigit(_json[_index]))
                    {
                        _index++;
                    }
                }

                if (TryConsume('.'))
                {
                    RequireDigit();
                    while (_index < _json.Length && IsDigit(_json[_index]))
                    {
                        _index++;
                    }
                }

                if (TryConsume('e') || TryConsume('E'))
                {
                    if (!TryConsume('+'))
                    {
                        TryConsume('-');
                    }

                    RequireDigit();
                    while (_index < _json.Length && IsDigit(_json[_index]))
                    {
                        _index++;
                    }
                }

                return _json.Substring(start, _index - start);
            }

            private void RequireDigit()
            {
                if (_index >= _json.Length || !IsDigit(_json[_index]))
                {
                    throw InvalidJson();
                }

                _index++;
            }

            private void ConsumeLiteral(string literal)
            {
                if (_index + literal.Length > _json.Length ||
                    string.CompareOrdinal(_json, _index, literal, 0, literal.Length) != 0)
                {
                    throw InvalidJson();
                }

                _index += literal.Length;
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
                        return;
                    }

                    _index++;
                }
            }

            private bool HasCurrent(char expected)
            {
                return _index < _json.Length && _json[_index] == expected;
            }

            private bool TryConsume(char expected)
            {
                if (!HasCurrent(expected))
                {
                    return false;
                }

                _index++;
                return true;
            }

            private void Expect(char expected)
            {
                if (!TryConsume(expected))
                {
                    throw InvalidJson();
                }
            }

            private static bool IsDigit(char character)
            {
                return character >= '0' && character <= '9';
            }

            private static int HexValue(char character)
            {
                if (character >= '0' && character <= '9')
                {
                    return character - '0';
                }

                if (character >= 'a' && character <= 'f')
                {
                    return character - 'a' + 10;
                }

                if (character >= 'A' && character <= 'F')
                {
                    return character - 'A' + 10;
                }

                return -1;
            }

            private static void RequireDepth(int depth)
            {
                if (depth > MaximumNestingDepth)
                {
                    throw InvalidJson();
                }
            }

            private static FormatException InvalidJson()
            {
                return new FormatException(
                    "Career save version envelope is not valid standard JSON.");
            }
        }
    }
}
