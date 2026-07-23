using System;
using System.Collections.Generic;
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
            int? observedSchemaVersion)
        {
            Kind = kind;
            ObservedSchemaVersion = observedSchemaVersion;
        }

        public CareerSaveVersionClassification Kind { get; }

        public int? ObservedSchemaVersion { get; }
    }

    public sealed class CareerSaveVersionNotSupportedException : FormatException
    {
        public CareerSaveVersionNotSupportedException(int? observedSchemaVersion)
            : base(FormatMessage(observedSchemaVersion))
        {
            ObservedSchemaVersion = observedSchemaVersion;
        }

        public int? ObservedSchemaVersion { get; }

        private static string FormatMessage(int? observedSchemaVersion)
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
                    schemaValue.IntegerValue < 1 ||
                    schemaValue.IntegerValue > int.MaxValue)
                {
                    return Malformed();
                }

                var schemaVersion = (int)schemaValue.IntegerValue;
                if (schemaVersion == 1 || schemaVersion > CareerSaveVersions.CurrentSchemaVersion)
                {
                    return new CareerSaveVersionClassificationResult(
                        CareerSaveVersionClassification.Unsupported,
                        schemaVersion);
                }

                if (schemaVersion != CareerSaveVersions.CurrentSchemaVersion ||
                    versions.ContainsUnknownProperty(
                        "schemaVersion",
                        "contentVersion",
                        "rulesetVersion",
                        "contractVersion",
                        "careerRandomAlgorithmVersion"))
                {
                    return Malformed(schemaVersion);
                }

                if (!TryPositiveInt(versions, "contentVersion", out var contentVersion) ||
                    !TryPositiveInt(versions, "rulesetVersion", out var rulesetVersion) ||
                    !TryPositiveInt(versions, "contractVersion", out var contractVersion) ||
                    !TryPositiveInt(
                        versions,
                        "careerRandomAlgorithmVersion",
                        out var careerRandomAlgorithmVersion))
                {
                    return Malformed(schemaVersion);
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
                    schemaVersion);
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

        private static CareerSaveVersionClassificationResult Malformed(
            int? observedSchemaVersion = null)
        {
            return new CareerSaveVersionClassificationResult(
                CareerSaveVersionClassification.Malformed,
                observedSchemaVersion);
        }
    }
}
