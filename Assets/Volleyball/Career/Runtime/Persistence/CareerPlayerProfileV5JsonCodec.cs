using System;
using System.Globalization;
using System.Text;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Persistence
{
    /// <summary>
    /// A separate profile artifact for new V5 careers. V2 save documents stay
    /// historical/recoverable and are never filled with invented V5 bases.
    /// </summary>
    public static class CareerPlayerProfileV5JsonCodec
    {
        private const int SchemaVersion = 6;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] Serialize(CareerPlayerProfileV5 profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return Utf8.GetBytes(Payload(profile));
        }

        public static CareerPlayerProfileV5 Deserialize(byte[] canonicalUtf8)
        {
            if (canonicalUtf8 == null) throw new ArgumentNullException(nameof(canonicalUtf8));
            var json = Utf8.GetString(canonicalUtf8);
            var root = StrictJsonReader.Parse(canonicalUtf8);
            if (root.Kind != StrictJsonKind.Object) throw new FormatException("V5 Career profile must be an object.");
            var document = root.ObjectValue;
            var schemaVersion = RequiredInt(document, "schemaVersion");
            if (schemaVersion != 5 && schemaVersion != SchemaVersion)
                throw new FormatException("Unsupported V5 Career profile schema.");
            if (schemaVersion == 5
                ? document.ContainsUnknownProperty("schemaVersion", "playerId", "displayName", "jerseyNumber", "dominantHand", "bases")
                : document.ContainsUnknownProperty("schemaVersion", "playerId", "displayName", "jerseyNumber", "dominantHand", "bases", "fatigue", "mindset", "coachTrust"))
                throw new FormatException("V5 Career profile has an unknown field.");
            var bases = RequiredObject(document, "bases");
            if (bases.ContainsUnknownProperty("strength", "heightMillimeters", "jump", "movement", "reaction", "coordination", "attack", "defense", "courtIq", "block", "serve", "set"))
                throw new FormatException("V5 Career profile bases have an unknown field.");
            var profile = new CareerPlayerProfileV5(new PlayerId(RequiredString(document, "playerId")),
                RequiredString(document, "displayName"), RequiredInt(document, "jerseyNumber"),
                (DominantHandV5)RequiredInt(document, "dominantHand"), new CareerBaseAttributesV5(
                    RequiredInt(bases, "strength"), RequiredInt(bases, "heightMillimeters"),
                    RequiredInt(bases, "jump"), RequiredInt(bases, "movement"), RequiredInt(bases, "reaction"),
                    RequiredInt(bases, "coordination"), RequiredInt(bases, "attack"), RequiredInt(bases, "defense"),
                    RequiredInt(bases, "courtIq"), RequiredInt(bases, "block"), RequiredInt(bases, "serve"), RequiredInt(bases, "set")),
                schemaVersion == 5 ? 0 : RequiredInt(document, "fatigue"),
                schemaVersion == 5 ? 50 : RequiredInt(document, "mindset"),
                schemaVersion == 5 ? 50 : RequiredInt(document, "coachTrust"));
            if (schemaVersion == SchemaVersion && !string.Equals(json, Utf8.GetString(Serialize(profile)), StringComparison.Ordinal))
                throw new FormatException("V5 Career profile is not canonical.");
            return profile;
        }

        private static string Payload(CareerPlayerProfileV5 value)
        {
            var b = value.Bases;
            return string.Format(CultureInfo.InvariantCulture,
                "{{\"schemaVersion\":6,\"playerId\":{0},\"displayName\":{1},\"jerseyNumber\":{2},\"dominantHand\":{3},\"bases\":{{\"strength\":{4},\"heightMillimeters\":{5},\"jump\":{6},\"movement\":{7},\"reaction\":{8},\"coordination\":{9},\"attack\":{10},\"defense\":{11},\"courtIq\":{12},\"block\":{13},\"serve\":{14},\"set\":{15}}},\"fatigue\":{16},\"mindset\":{17},\"coachTrust\":{18}}}",
                Quote(value.PlayerId.Value), Quote(value.DisplayName), value.JerseyNumber, (int)value.DominantHand,
                b.Strength, b.HeightMillimeters, b.Jump, b.Movement, b.Reaction, b.Coordination,
                b.Attack, b.Defense, b.CourtIq, b.Block, b.Serve, b.Set,
                value.Fatigue, value.Mindset, value.CoachTrust);
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20) builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }

        private static StrictJsonObject RequiredObject(StrictJsonObject document, string property) =>
            document.Get(property).Kind == StrictJsonKind.Object ? document.Get(property).ObjectValue :
                throw new FormatException(property + " must be an object.");
        private static string RequiredString(StrictJsonObject document, string property) =>
            document.Get(property).Kind == StrictJsonKind.String ? document.Get(property).StringValue :
                throw new FormatException(property + " must be a string.");
        private static int RequiredInt(StrictJsonObject document, string property)
        {
            var value = document.Get(property);
            if (value.Kind != StrictJsonKind.Integer || value.IntegerValue < int.MinValue || value.IntegerValue > int.MaxValue)
                throw new FormatException(property + " must be an Int32.");
            return (int)value.IntegerValue;
        }
    }
}
