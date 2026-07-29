using System;

namespace Volleyball.Career.Domain
{
    public readonly struct ProfileId : IEquatable<ProfileId>
    {
        public ProfileId(Guid value)
        {
            Value = CareerIdentifierGuard.NotEmpty(value, nameof(value));
        }

        public Guid Value { get; }

        public bool Equals(ProfileId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ProfileId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("D");
        }
    }

    public readonly struct SaveId : IEquatable<SaveId>
    {
        public SaveId(Guid value)
        {
            Value = CareerIdentifierGuard.NotEmpty(value, nameof(value));
        }

        public Guid Value { get; }

        public bool Equals(SaveId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is SaveId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("D");
        }
    }

    public readonly struct LineageId : IEquatable<LineageId>
    {
        public LineageId(Guid value)
        {
            Value = CareerIdentifierGuard.NotEmpty(value, nameof(value));
        }

        public Guid Value { get; }

        public bool Equals(LineageId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is LineageId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("D");
        }
    }

    public readonly struct OperationId : IEquatable<OperationId>
    {
        public OperationId(Guid value)
        {
            Value = CareerIdentifierGuard.NotEmpty(value, nameof(value));
        }

        public Guid Value { get; }

        public bool Equals(OperationId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is OperationId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString("D");
        }
    }

    public sealed class CareerSeed : IEquatable<CareerSeed>
    {
        public const int ByteLength = 32;
        public const int HexLength = ByteLength * 2;

        private readonly byte[] _bytes;

        public CareerSeed(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length != ByteLength)
            {
                throw new ArgumentException("A career seed must contain exactly 32 bytes.", nameof(bytes));
            }

            _bytes = (byte[])bytes.Clone();
        }

        public static CareerSeed Parse(string value)
        {
            CareerIdentifierGuard.LowercaseSha256Hex(value, nameof(value));
            var bytes = new byte[ByteLength];
            for (var index = 0; index < bytes.Length; index++)
            {
                var high = CareerIdentifierGuard.HexNibble(value[index * 2]);
                var low = CareerIdentifierGuard.HexNibble(value[(index * 2) + 1]);
                bytes[index] = (byte)((high << 4) | low);
            }

            return new CareerSeed(bytes);
        }

        public byte[] ToBytes()
        {
            return (byte[])_bytes.Clone();
        }

        public string ToHex()
        {
            var characters = new char[HexLength];
            for (var index = 0; index < _bytes.Length; index++)
            {
                var value = _bytes[index];
                characters[index * 2] = CareerIdentifierGuard.LowercaseHex(value >> 4);
                characters[(index * 2) + 1] = CareerIdentifierGuard.LowercaseHex(value & 0x0f);
            }

            return new string(characters);
        }

        public bool Equals(CareerSeed other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            for (var index = 0; index < _bytes.Length; index++)
            {
                if (_bytes[index] != other._bytes[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CareerSeed);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                for (var index = 0; index < _bytes.Length; index++)
                {
                    hash = (hash * 31) + _bytes[index];
                }

                return hash;
            }
        }

        public override string ToString()
        {
            return ToHex();
        }
    }

    public readonly struct Sha256Digest : IEquatable<Sha256Digest>
    {
        public Sha256Digest(string value)
        {
            Value = CareerIdentifierGuard.LowercaseSha256Hex(value, nameof(value));
        }

        public string Value { get; }

        public static Sha256Digest Parse(string value)
        {
            return new Sha256Digest(value);
        }

        public bool Equals(Sha256Digest other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Sha256Digest other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }

    internal static class CareerIdentifierGuard
    {
        public static Guid NotEmpty(Guid value, string parameterName)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("A stable non-empty identifier is required.", parameterName);
            }

            return value;
        }

        public static string LowercaseSha256Hex(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Length != CareerSeed.HexLength)
            {
                throw new ArgumentException("A SHA-256 value must contain exactly 64 hexadecimal characters.", parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (!IsLowercaseHex(value[index]))
                {
                    throw new ArgumentException("A SHA-256 value must use lowercase hexadecimal characters.", parameterName);
                }
            }

            return value;
        }

        public static int HexNibble(char value)
        {
            return value <= '9' ? value - '0' : value - 'a' + 10;
        }

        public static char LowercaseHex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + value - 10);
        }

        private static bool IsLowercaseHex(char value)
        {
            return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');
        }
    }
}
