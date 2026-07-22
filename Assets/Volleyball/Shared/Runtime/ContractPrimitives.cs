using System;

namespace Volleyball.Shared.Contracts
{
    public interface IMatchContext
    {
        int ContractVersion { get; }
        Guid SessionId { get; }
        int Seed { get; }
        string ContextHash { get; }
    }

    public interface IMatchResult
    {
        int ContractVersion { get; }
        Guid SessionId { get; }
        string ContextHash { get; }
        TeamId WinnerTeamId { get; }
        int HomeScore { get; }
        int AwayScore { get; }
    }

    public static class ContractVersions
    {
        public const int MatchV1 = 1;
        public const int MatchV2 = 2;

        public static bool SupportsMatch(int version)
        {
            return version == MatchV1;
        }
    }

    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(string value)
        {
            Value = ContractGuard.RequiredId(value, nameof(value));
        }

        public string Value { get; }

        public bool Equals(PlayerId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerId other && Equals(other);
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

    public readonly struct TeamId : IEquatable<TeamId>
    {
        public TeamId(string value)
        {
            Value = ContractGuard.RequiredId(value, nameof(value));
        }

        public string Value { get; }

        public bool Equals(TeamId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TeamId other && Equals(other);
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

    public enum TeamSide
    {
        Home = 0,
        Away = 1
    }

    public enum PlayerPosition
    {
        Setter = 0,
        OutsideHitter = 1,
        MiddleBlocker = 2,
        Opposite = 3,
        Libero = 4,
        Defender = 5
    }

    public sealed class ContractValidationException : Exception
    {
        public ContractValidationException(string message)
            : base(message)
        {
        }

        public ContractValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    internal static class ContractGuard
    {
        public static string RequiredId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            {
                throw new ContractValidationException(name + " must contain 1 to 64 characters.");
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var valid = char.IsLetterOrDigit(character) ||
                            character == '-' || character == '_' || character == '.' || character == ':';
                if (!valid)
                {
                    throw new ContractValidationException(name + " contains an unsupported character.");
                }
            }

            return value;
        }

        public static string RequiredText(string value, string name, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
            {
                throw new ContractValidationException(
                    name + " must contain 1 to " + maximumLength + " characters.");
            }

            return value;
        }

        public static float Unit(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
            {
                throw new ContractValidationException(name + " must be finite and in the range [0, 1].");
            }

            return value;
        }

        public static float AttackReach(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 3.20f || value > 3.55f)
            {
                throw new ContractValidationException(name + " must be finite and in the range [3.20, 3.55].");
            }

            return value;
        }

        public static void DefinedEnum<T>(T value, string name) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ContractValidationException(name + " has an unsupported value.");
            }
        }

        public static void NonNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ContractValidationException(name + " must be non-negative.");
            }
        }

        public static void Hash(string value, string name)
        {
            if (value == null || value.Length != 64)
            {
                throw new ContractValidationException(name + " must be a 64-character SHA-256 value.");
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    throw new ContractValidationException(name + " must use lowercase hexadecimal characters.");
                }
            }
        }
    }
}
