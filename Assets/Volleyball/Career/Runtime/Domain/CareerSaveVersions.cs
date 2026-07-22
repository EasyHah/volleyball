using System;

namespace Volleyball.Career.Domain
{
    public sealed class CareerSaveVersions : IEquatable<CareerSaveVersions>
    {
        public const int CurrentSchemaVersion = 1;
        public const int CurrentContentVersion = 1;
        public const int CurrentRulesetVersion = 1;
        public const int CurrentCareerRandomAlgorithmVersion = 1;

        public CareerSaveVersions(
            int schemaVersion,
            int contentVersion,
            int rulesetVersion,
            int careerRandomAlgorithmVersion)
        {
            SchemaVersion = CareerSaveModelGuard.PositiveVersion(
                schemaVersion,
                nameof(schemaVersion));
            ContentVersion = CareerSaveModelGuard.PositiveVersion(
                contentVersion,
                nameof(contentVersion));
            RulesetVersion = CareerSaveModelGuard.PositiveVersion(
                rulesetVersion,
                nameof(rulesetVersion));
            CareerRandomAlgorithmVersion = CareerSaveModelGuard.PositiveVersion(
                careerRandomAlgorithmVersion,
                nameof(careerRandomAlgorithmVersion));
        }

        public static CareerSaveVersions Current => new CareerSaveVersions(
            CurrentSchemaVersion,
            CurrentContentVersion,
            CurrentRulesetVersion,
            CurrentCareerRandomAlgorithmVersion);

        public int SchemaVersion { get; }

        public int ContentVersion { get; }

        public int RulesetVersion { get; }

        public int CareerRandomAlgorithmVersion { get; }

        public bool Equals(CareerSaveVersions other)
        {
            return other != null &&
                   SchemaVersion == other.SchemaVersion &&
                   ContentVersion == other.ContentVersion &&
                   RulesetVersion == other.RulesetVersion &&
                   CareerRandomAlgorithmVersion == other.CareerRandomAlgorithmVersion;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CareerSaveVersions);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SchemaVersion;
                hashCode = (hashCode * 397) ^ ContentVersion;
                hashCode = (hashCode * 397) ^ RulesetVersion;
                hashCode = (hashCode * 397) ^ CareerRandomAlgorithmVersion;
                return hashCode;
            }
        }
    }

    internal static class CareerSaveModelGuard
    {
        public const long MaximumIJsonSafeInteger = 9007199254740991L;

        public static int PositiveVersion(int value, string parameterName)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A version must be at least 1.");
            }

            return value;
        }

        public static long PositiveRevision(long value, string parameterName)
        {
            if (value < 1 || value > MaximumIJsonSafeInteger)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A revision must be in the I-JSON safe range [1, 9007199254740991].");
            }

            return value;
        }

        public static long NonNegativeUtcMilliseconds(long value, string parameterName)
        {
            if (value < 0 || value > MaximumIJsonSafeInteger)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A UTC millisecond timestamp must be in the I-JSON safe range [0, 9007199254740991].");
            }

            return value;
        }

        public static Guid StableId(Guid value, string parameterName)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A stable non-empty identifier is required.",
                    parameterName);
            }

            return value;
        }

        public static string RequiredText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }

        public static string BusinessId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            {
                throw new ArgumentException(
                    "A business ID must contain 1 to 64 characters.",
                    parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var valid = (character >= 'a' && character <= 'z') ||
                            (character >= 'A' && character <= 'Z') ||
                            (character >= '0' && character <= '9') ||
                            character == '-' ||
                            character == '_' ||
                            character == '.' ||
                            character == ':';
                if (!valid)
                {
                    throw new ArgumentException(
                        "A business ID contains an unsupported character.",
                        parameterName);
                }
            }

            return value;
        }

        public static void DefinedEnum<T>(T value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "An unsupported enum value was provided.");
            }
        }

        public static int InclusiveRange(
            int value,
            int minimum,
            int maximum,
            string parameterName)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "The value must be in the range [" + minimum + ", " + maximum + "].");
            }

            return value;
        }
    }
}
