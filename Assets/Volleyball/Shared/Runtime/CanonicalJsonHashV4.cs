using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    internal static class CanonicalJsonHashV4
    {
        public static void AppendString(StringBuilder output, string value)
        {
            output.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"':
                        output.Append("\\\"");
                        break;
                    case '\\':
                        output.Append("\\\\");
                        break;
                    case '\b':
                        output.Append("\\b");
                        break;
                    case '\f':
                        output.Append("\\f");
                        break;
                    case '\n':
                        output.Append("\\n");
                        break;
                    case '\r':
                        output.Append("\\r");
                        break;
                    case '\t':
                        output.Append("\\t");
                        break;
                    default:
                        if (character < 32)
                        {
                            output.Append("\\u").Append(
                                ((int)character).ToString(
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

        public static string Sha256(string canonical)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var hex = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++)
            {
                hex.Append(
                    hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }
    }
}
