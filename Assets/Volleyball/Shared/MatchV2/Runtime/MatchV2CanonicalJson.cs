using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Volleyball.Shared.Contracts.V2
{
    internal sealed class MatchV2CanonicalJsonWriter
    {
        private readonly StringBuilder _builder = new StringBuilder();
        private readonly Stack<Frame> _frames = new Stack<Frame>();
        private bool _root;

        public void StartObject() { BeginValue(); _builder.Append('{'); _frames.Push(new Frame(true)); }
        public void EndObject() { var f = Require(true); if (f.Expecting) throw new InvalidOperationException(); _frames.Pop(); _builder.Append('}'); }
        public void StartArray() { BeginValue(); _builder.Append('['); _frames.Push(new Frame(false)); }
        public void EndArray() { Require(false); _frames.Pop(); _builder.Append(']'); }
        public void Property(string name)
        {
            ValidateScalars(name);
            var f = Require(true);
            if (f.Expecting) throw new InvalidOperationException();
            if (f.Count++ > 0) _builder.Append(',');
            AppendString(name); _builder.Append(':'); f.Expecting = true;
        }
        public void String(string value) { if (value == null) throw new ArgumentNullException(nameof(value)); ValidateScalars(value); BeginValue(); AppendString(value); }
        public void NullableString(string value) { if (value == null) Null(); else String(value); }
        public void Integer(long value)
        {
            if (value < -MatchV2Guard.MaximumSafeInteger || value > MatchV2Guard.MaximumSafeInteger)
                throw new MatchV2ContractException("Integer is outside the I-JSON safe range.");
            BeginValue(); _builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }
        public void NullableInteger(int? value) { if (value.HasValue) Integer(value.Value); else Null(); }
        public void Boolean(bool value) { BeginValue(); _builder.Append(value ? "true" : "false"); }
        public void Null() { BeginValue(); _builder.Append("null"); }
        public byte[] Bytes()
        {
            if (!_root || _frames.Count != 0) throw new InvalidOperationException("JSON document is incomplete.");
            return new UTF8Encoding(false, true).GetBytes(_builder.ToString());
        }

        private void BeginValue()
        {
            if (_frames.Count == 0)
            {
                if (_root) throw new InvalidOperationException();
                _root = true; return;
            }
            var f = _frames.Peek();
            if (f.Object)
            {
                if (!f.Expecting) throw new InvalidOperationException();
                f.Expecting = false; return;
            }
            if (f.Count++ > 0) _builder.Append(',');
        }

        private Frame Require(bool isObject)
        {
            if (_frames.Count == 0 || _frames.Peek().Object != isObject) throw new InvalidOperationException();
            return _frames.Peek();
        }

        private void AppendString(string value)
        {
            _builder.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': _builder.Append("\\\""); break;
                    case '\\': _builder.Append("\\\\"); break;
                    case '\b': _builder.Append("\\b"); break;
                    case '\t': _builder.Append("\\t"); break;
                    case '\n': _builder.Append("\\n"); break;
                    case '\f': _builder.Append("\\f"); break;
                    case '\r': _builder.Append("\\r"); break;
                    default:
                        if (c < 0x20) _builder.Append("\\u00").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
                        else _builder.Append(c);
                        break;
                }
            }
            _builder.Append('"');
        }

        internal static void ValidateScalars(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsHighSurrogate(value[i]))
                {
                    if (++i >= value.Length || !char.IsLowSurrogate(value[i]))
                        throw new MatchV2ContractException("JSON strings cannot contain a lone surrogate.");
                }
                else if (char.IsLowSurrogate(value[i]))
                    throw new MatchV2ContractException("JSON strings cannot contain a lone surrogate.");
            }
        }

        private sealed class Frame
        {
            public Frame(bool isObject) { Object = isObject; }
            public bool Object { get; }
            public int Count { get; set; }
            public bool Expecting { get; set; }
        }
    }

    internal static class MatchV2StrictJsonReader
    {
        public static MatchV2JsonValue Parse(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException ex) { throw new MatchV2ContractException("JSON is not valid UTF-8.", ex); }
            if (text.Length > 0 && text[0] == '\ufeff') throw new MatchV2ContractException("A BOM is not permitted.");
            try { return new Parser(text).Document(); }
            catch (MatchV2ContractException) { throw; }
            catch (Exception ex) { throw new MatchV2ContractException("JSON is malformed.", ex); }
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _i;
            public Parser(string text) { _text = text; }
            public MatchV2JsonValue Document()
            {
                White(); var value = Value(); White();
                if (_i != _text.Length) Error("Trailing JSON tokens are not permitted.");
                return value;
            }
            private MatchV2JsonValue Value()
            {
                if (_i >= _text.Length) Error("JSON value expected.");
                switch (_text[_i])
                {
                    case '{': return Object();
                    case '[': return Array();
                    case '"': return MatchV2JsonValue.String(String());
                    case 't': Literal("true"); return MatchV2JsonValue.Boolean(true);
                    case 'f': Literal("false"); return MatchV2JsonValue.Boolean(false);
                    case 'n': Literal("null"); return MatchV2JsonValue.Null();
                    default:
                        if (_text[_i] == '-' || Digit(_text[_i])) return Integer();
                        Error("Invalid JSON token."); return null;
                }
            }
            private MatchV2JsonValue Object()
            {
                _i++; White(); var values = new List<MatchV2JsonProperty>(); var names = new HashSet<string>(StringComparer.Ordinal);
                if (Take('}')) return MatchV2JsonValue.Object(values);
                while (true)
                {
                    if (_i >= _text.Length || _text[_i] != '"') Error("Property name expected.");
                    var name = String(); if (!names.Add(name)) Error("Duplicate property: " + name);
                    White(); Need(':'); White(); values.Add(new MatchV2JsonProperty(name, Value())); White();
                    if (Take('}')) return MatchV2JsonValue.Object(values);
                    Need(','); White();
                }
            }
            private MatchV2JsonValue Array()
            {
                _i++; White(); var values = new List<MatchV2JsonValue>();
                if (Take(']')) return MatchV2JsonValue.Array(values);
                while (true)
                {
                    values.Add(Value()); White();
                    if (Take(']')) return MatchV2JsonValue.Array(values);
                    Need(','); White();
                }
            }
            private string String()
            {
                Need('"'); var b = new StringBuilder();
                while (_i < _text.Length)
                {
                    var c = _text[_i++];
                    if (c == '"') return b.ToString();
                    if (c == '\\') { Escape(b); continue; }
                    if (c < 0x20) Error("Unescaped control character.");
                    if (char.IsHighSurrogate(c))
                    {
                        if (_i >= _text.Length || !char.IsLowSurrogate(_text[_i])) Error("Lone surrogate.");
                        b.Append(c).Append(_text[_i++]);
                    }
                    else if (char.IsLowSurrogate(c)) Error("Lone surrogate.");
                    else b.Append(c);
                }
                Error("Unterminated string."); return null;
            }
            private void Escape(StringBuilder b)
            {
                if (_i >= _text.Length) Error("Incomplete escape.");
                switch (_text[_i++])
                {
                    case '"': b.Append('"'); return;
                    case '\\': b.Append('\\'); return;
                    case '/': b.Append('/'); return;
                    case 'b': b.Append('\b'); return;
                    case 'f': b.Append('\f'); return;
                    case 'n': b.Append('\n'); return;
                    case 'r': b.Append('\r'); return;
                    case 't': b.Append('\t'); return;
                    case 'u': Unicode(b); return;
                    default: Error("Invalid escape."); return;
                }
            }
            private void Unicode(StringBuilder b)
            {
                var first = HexUnit();
                if (char.IsHighSurrogate(first))
                {
                    if (_i + 1 >= _text.Length || _text[_i] != '\\' || _text[_i + 1] != 'u') Error("Lone surrogate escape.");
                    _i += 2; var second = HexUnit(); if (!char.IsLowSurrogate(second)) Error("Invalid surrogate pair.");
                    b.Append(first).Append(second); return;
                }
                if (char.IsLowSurrogate(first)) Error("Lone surrogate escape.");
                b.Append(first);
            }
            private char HexUnit()
            {
                if (_i + 4 > _text.Length) Error("Short Unicode escape.");
                var value = 0;
                for (var n = 0; n < 4; n++) { var h = Hex(_text[_i++]); if (h < 0) Error("Invalid Unicode escape."); value = value * 16 + h; }
                return (char)value;
            }
            private MatchV2JsonValue Integer()
            {
                var start = _i;
                if (_text[_i] == '-') { _i++; if (_i >= _text.Length) Error("Invalid integer."); }
                if (_text[_i] == '0') { _i++; if (_i < _text.Length && Digit(_text[_i])) Error("Leading zero."); }
                else if (_text[_i] >= '1' && _text[_i] <= '9') while (_i < _text.Length && Digit(_text[_i])) _i++;
                else Error("Invalid integer.");
                if (_i < _text.Length && (_text[_i] == '.' || _text[_i] == 'e' || _text[_i] == 'E')) Error("Floats and exponents are forbidden.");
                var token = _text.Substring(start, _i - start);
                if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ||
                    value < -MatchV2Guard.MaximumSafeInteger || value > MatchV2Guard.MaximumSafeInteger)
                    Error("Integer outside I-JSON safe range.");
                return MatchV2JsonValue.Integer(value);
            }
            private void Literal(string value) { if (_i + value.Length > _text.Length || _text.Substring(_i, value.Length) != value) Error("Invalid literal."); _i += value.Length; }
            private void White() { while (_i < _text.Length && (_text[_i] == ' ' || _text[_i] == '\t' || _text[_i] == '\r' || _text[_i] == '\n')) _i++; }
            private void Need(char c) { if (!Take(c)) Error("Expected " + c); }
            private bool Take(char c) { if (_i >= _text.Length || _text[_i] != c) return false; _i++; return true; }
            private void Error(string message) { throw new MatchV2ContractException(message + " Offset " + _i + "."); }
            private static bool Digit(char c) => c >= '0' && c <= '9';
            private static int Hex(char c) => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
        }
    }

    internal enum MatchV2JsonKind { Object, Array, String, Integer, Boolean, Null }
    internal sealed class MatchV2JsonValue
    {
        private MatchV2JsonValue(MatchV2JsonKind kind, MatchV2JsonObject o, IReadOnlyList<MatchV2JsonValue> a, string s, long i, bool b)
        { Kind = kind; ObjectValue = o; ArrayValue = a; StringValue = s; IntegerValue = i; BooleanValue = b; }
        public MatchV2JsonKind Kind { get; }
        public MatchV2JsonObject ObjectValue { get; }
        public IReadOnlyList<MatchV2JsonValue> ArrayValue { get; }
        public string StringValue { get; }
        public long IntegerValue { get; }
        public bool BooleanValue { get; }
        public static MatchV2JsonValue Object(IReadOnlyList<MatchV2JsonProperty> v) => new MatchV2JsonValue(MatchV2JsonKind.Object, new MatchV2JsonObject(v), null, null, 0, false);
        public static MatchV2JsonValue Array(IReadOnlyList<MatchV2JsonValue> v) => new MatchV2JsonValue(MatchV2JsonKind.Array, null, v, null, 0, false);
        public static MatchV2JsonValue String(string v) => new MatchV2JsonValue(MatchV2JsonKind.String, null, null, v, 0, false);
        public static MatchV2JsonValue Integer(long v) => new MatchV2JsonValue(MatchV2JsonKind.Integer, null, null, null, v, false);
        public static MatchV2JsonValue Boolean(bool v) => new MatchV2JsonValue(MatchV2JsonKind.Boolean, null, null, null, 0, v);
        public static MatchV2JsonValue Null() => new MatchV2JsonValue(MatchV2JsonKind.Null, null, null, null, 0, false);
    }
    internal sealed class MatchV2JsonObject
    {
        private readonly IReadOnlyList<MatchV2JsonProperty> _properties;
        private readonly Dictionary<string, MatchV2JsonValue> _values;
        public MatchV2JsonObject(IReadOnlyList<MatchV2JsonProperty> properties)
        { _properties = properties; _values = new Dictionary<string, MatchV2JsonValue>(StringComparer.Ordinal); foreach (var p in properties) _values.Add(p.Name, p.Value); }
        public MatchV2JsonValue Get(string name) { if (!_values.TryGetValue(name, out var value)) throw new MatchV2ContractException("Missing property: " + name); return value; }
        public void Require(params string[] names)
        {
            if (_properties.Count != names.Length) throw new MatchV2ContractException("Missing or unknown property.");
            for (var i = 0; i < names.Length; i++)
                if (!string.Equals(_properties[i].Name, names[i], StringComparison.Ordinal))
                    throw new MatchV2ContractException("Properties are missing, unknown, or reordered.");
        }
    }
    internal sealed class MatchV2JsonProperty
    {
        public MatchV2JsonProperty(string name, MatchV2JsonValue value) { Name = name; Value = value; }
        public string Name { get; }
        public MatchV2JsonValue Value { get; }
    }
}
