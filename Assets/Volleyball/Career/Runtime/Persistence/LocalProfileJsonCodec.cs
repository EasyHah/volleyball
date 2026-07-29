using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Volleyball.Career.Application;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    public static class LocalProfileJsonCodec
    {
        public static LocalPlayerProfile SealProfile(LocalPlayerProfile candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var document = ToDocument(candidate);
            document.profileSnapshotHash = ComputeProfileDocumentHash(document);
            return ToDomain(document);
        }

        public static Sha256Digest ComputeProfileHash(LocalPlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new Sha256Digest(ComputeProfileDocumentHash(ToDocument(profile)));
        }

        public static byte[] SerializeProfile(LocalPlayerProfile sealedProfile)
        {
            if (sealedProfile == null)
            {
                throw new ArgumentNullException(nameof(sealedProfile));
            }

            var document = ToDocument(sealedProfile);
            RequireMatchingHash(
                document.profileSnapshotHash,
                ComputeProfileDocumentHash(document),
                "The local profile is not sealed with its canonical hash.");
            return WriteProfile(document, true);
        }

        public static LocalPlayerProfile DeserializeProfile(byte[] utf8Json)
        {
            var document = ReadProfile(StrictJsonReader.Parse(RequiredBytes(utf8Json)));
            LocalPlayerProfile profile;
            try
            {
                profile = ToDomain(document);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("The local profile violates Schema V1 invariants.", exception);
            }

            RequireMatchingHash(
                document.profileSnapshotHash,
                ComputeProfileDocumentHash(document),
                "The local profile hash is invalid.");
            RequireCanonical(WriteProfile(document, true), utf8Json, "local profile");
            return profile;
        }

        public static LocalProfileCatalog SealCatalog(LocalProfileCatalog candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var document = ToDocument(candidate);
            document.catalogHash = ComputeCatalogDocumentHash(document);
            return ToDomain(document);
        }

        public static Sha256Digest ComputeCatalogHash(LocalProfileCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return new Sha256Digest(ComputeCatalogDocumentHash(ToDocument(catalog)));
        }

        public static byte[] SerializeCatalog(LocalProfileCatalog sealedCatalog)
        {
            if (sealedCatalog == null)
            {
                throw new ArgumentNullException(nameof(sealedCatalog));
            }

            var document = ToDocument(sealedCatalog);
            RequireMatchingHash(
                document.catalogHash,
                ComputeCatalogDocumentHash(document),
                "The local profile catalog is not sealed with its canonical hash.");
            return WriteCatalog(document, true);
        }

        public static LocalProfileCatalog DeserializeCatalog(byte[] utf8Json)
        {
            var document = ReadCatalog(StrictJsonReader.Parse(RequiredBytes(utf8Json)));
            LocalProfileCatalog catalog;
            try
            {
                catalog = ToDomain(document);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(
                    "The local profile catalog violates Schema V1 invariants.",
                    exception);
            }

            RequireMatchingHash(
                document.catalogHash,
                ComputeCatalogDocumentHash(document),
                "The local profile catalog hash is invalid.");
            RequireCanonical(WriteCatalog(document, true), utf8Json, "local profile catalog");
            return catalog;
        }

        private static LocalPlayerProfileDocumentV1 ToDocument(LocalPlayerProfile profile)
        {
            var entries = new CareerIndexEntryDocumentV1[profile.CareerEntries.Count];
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = profile.CareerEntries[index];
                entries[index] = new CareerIndexEntryDocumentV1
                {
                    saveId = entry.SaveId.ToString(),
                    careerName = entry.CareerName,
                    playerDisplayName = entry.PlayerDisplayName,
                    progressionKind = ProgressionName(entry.ProgressionKind),
                    phase = PhaseName(entry.Phase),
                    season = entry.Season,
                    week = entry.Week,
                    updatedAtUtcMs = entry.UpdatedAtUtcMs,
                    loadability = CareerLoadabilityName(entry.Loadability)
                };
            }

            return new LocalPlayerProfileDocumentV1
            {
                schemaVersion = profile.SchemaVersion,
                profileId = profile.ProfileId.ToString(),
                profileRevision = profile.ProfileRevision,
                profileSnapshotHash = profile.ProfileSnapshotHash.Value,
                displayName = profile.DisplayName,
                createdAtUtcMs = profile.CreatedAtUtcMs,
                updatedAtUtcMs = profile.UpdatedAtUtcMs,
                careerEntries = entries
            };
        }

        private static LocalPlayerProfile ToDomain(LocalPlayerProfileDocumentV1 document)
        {
            var entryDocuments = Required(document.careerEntries, "careerEntries");
            var entries = new CareerIndexEntry[entryDocuments.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = Required(entryDocuments[index], "careerEntries[" + index + "]");
                entries[index] = new CareerIndexEntry(
                    new SaveId(ParseGuid(entry.saveId, "careerEntries[" + index + "].saveId")),
                    entry.careerName,
                    entry.playerDisplayName,
                    ParseProgression(entry.progressionKind),
                    ParsePhase(entry.phase),
                    entry.season,
                    entry.week,
                    entry.updatedAtUtcMs,
                    ParseCareerLoadability(entry.loadability));
            }

            return new LocalPlayerProfile(
                document.schemaVersion,
                new ProfileId(ParseGuid(document.profileId, "profileId")),
                document.profileRevision,
                Sha256Digest.Parse(document.profileSnapshotHash),
                document.displayName,
                document.createdAtUtcMs,
                document.updatedAtUtcMs,
                entries);
        }

        private static LocalProfileCatalogDocumentV1 ToDocument(LocalProfileCatalog catalog)
        {
            var profiles = new LocalProfileCatalogEntryDocumentV1[catalog.Profiles.Count];
            for (var index = 0; index < profiles.Length; index++)
            {
                var profile = catalog.Profiles[index];
                profiles[index] = new LocalProfileCatalogEntryDocumentV1
                {
                    profileId = profile.ProfileId.ToString(),
                    displayName = profile.DisplayName,
                    updatedAtUtcMs = profile.UpdatedAtUtcMs,
                    loadability = ProfileLoadabilityName(profile.Loadability)
                };
            }

            return new LocalProfileCatalogDocumentV1
            {
                schemaVersion = catalog.SchemaVersion,
                catalogRevision = catalog.CatalogRevision,
                catalogHash = catalog.CatalogHash.Value,
                profiles = profiles
            };
        }

        private static LocalProfileCatalog ToDomain(LocalProfileCatalogDocumentV1 document)
        {
            var profileDocuments = Required(document.profiles, "profiles");
            var profiles = new LocalProfileCatalogEntry[profileDocuments.Length];
            for (var index = 0; index < profiles.Length; index++)
            {
                var profile = Required(profileDocuments[index], "profiles[" + index + "]");
                profiles[index] = new LocalProfileCatalogEntry(
                    new ProfileId(ParseGuid(profile.profileId, "profiles[" + index + "].profileId")),
                    profile.displayName,
                    profile.updatedAtUtcMs,
                    ParseProfileLoadability(profile.loadability));
            }

            return new LocalProfileCatalog(
                document.schemaVersion,
                document.catalogRevision,
                Sha256Digest.Parse(document.catalogHash),
                profiles);
        }

        private static byte[] WriteProfile(
            LocalPlayerProfileDocumentV1 document,
            bool includeHash)
        {
            var writer = new CanonicalJsonWriter();
            writer.WriteStartObject();
            writer.WritePropertyName("schemaVersion");
            writer.WriteInt64(document.schemaVersion);
            writer.WritePropertyName("profileId");
            writer.WriteString(document.profileId);
            writer.WritePropertyName("profileRevision");
            writer.WriteInt64(document.profileRevision);
            if (includeHash)
            {
                writer.WritePropertyName("profileSnapshotHash");
                writer.WriteString(document.profileSnapshotHash);
            }

            writer.WritePropertyName("displayName");
            writer.WriteString(document.displayName);
            writer.WritePropertyName("createdAtUtcMs");
            writer.WriteInt64(document.createdAtUtcMs);
            writer.WritePropertyName("updatedAtUtcMs");
            writer.WriteInt64(document.updatedAtUtcMs);
            writer.WritePropertyName("careerEntries");
            WriteCareerEntries(writer, document.careerEntries);
            writer.WriteEndObject();
            return writer.ToUtf8Bytes();
        }

        private static void WriteCareerEntries(
            CanonicalJsonWriter writer,
            CareerIndexEntryDocumentV1[] entries)
        {
            Required(entries, "careerEntries");
            writer.WriteStartArray();
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = Required(entries[index], "careerEntries[" + index + "]");
                writer.WriteStartObject();
                writer.WritePropertyName("saveId");
                writer.WriteString(entry.saveId);
                writer.WritePropertyName("careerName");
                writer.WriteString(entry.careerName);
                writer.WritePropertyName("playerDisplayName");
                writer.WriteString(entry.playerDisplayName);
                writer.WritePropertyName("progressionKind");
                writer.WriteString(entry.progressionKind);
                writer.WritePropertyName("phase");
                writer.WriteString(entry.phase);
                writer.WritePropertyName("season");
                writer.WriteInt64(entry.season);
                writer.WritePropertyName("week");
                writer.WriteInt64(entry.week);
                writer.WritePropertyName("updatedAtUtcMs");
                writer.WriteInt64(entry.updatedAtUtcMs);
                writer.WritePropertyName("loadability");
                writer.WriteString(entry.loadability);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static byte[] WriteCatalog(
            LocalProfileCatalogDocumentV1 document,
            bool includeHash)
        {
            var writer = new CanonicalJsonWriter();
            writer.WriteStartObject();
            writer.WritePropertyName("schemaVersion");
            writer.WriteInt64(document.schemaVersion);
            writer.WritePropertyName("catalogRevision");
            writer.WriteInt64(document.catalogRevision);
            if (includeHash)
            {
                writer.WritePropertyName("catalogHash");
                writer.WriteString(document.catalogHash);
            }

            writer.WritePropertyName("profiles");
            Required(document.profiles, "profiles");
            writer.WriteStartArray();
            for (var index = 0; index < document.profiles.Length; index++)
            {
                var profile = Required(document.profiles[index], "profiles[" + index + "]");
                writer.WriteStartObject();
                writer.WritePropertyName("profileId");
                writer.WriteString(profile.profileId);
                writer.WritePropertyName("displayName");
                writer.WriteString(profile.displayName);
                writer.WritePropertyName("updatedAtUtcMs");
                writer.WriteInt64(profile.updatedAtUtcMs);
                writer.WritePropertyName("loadability");
                writer.WriteString(profile.loadability);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            return writer.ToUtf8Bytes();
        }

        private static LocalPlayerProfileDocumentV1 ReadProfile(StrictJsonValue root)
        {
            var value = Object(root, "$profile");
            value.RequireExactly(
                "$profile",
                "schemaVersion",
                "profileId",
                "profileRevision",
                "profileSnapshotHash",
                "displayName",
                "createdAtUtcMs",
                "updatedAtUtcMs",
                "careerEntries");
            return new LocalPlayerProfileDocumentV1
            {
                schemaVersion = Int32(value.Get("schemaVersion"), "schemaVersion"),
                profileId = String(value.Get("profileId"), "profileId"),
                profileRevision = Integer(value.Get("profileRevision"), "profileRevision"),
                profileSnapshotHash = String(value.Get("profileSnapshotHash"), "profileSnapshotHash"),
                displayName = String(value.Get("displayName"), "displayName"),
                createdAtUtcMs = Integer(value.Get("createdAtUtcMs"), "createdAtUtcMs"),
                updatedAtUtcMs = Integer(value.Get("updatedAtUtcMs"), "updatedAtUtcMs"),
                careerEntries = ReadCareerEntries(value.Get("careerEntries"))
            };
        }

        private static CareerIndexEntryDocumentV1[] ReadCareerEntries(StrictJsonValue value)
        {
            var array = Array(value, "careerEntries");
            var entries = new CareerIndexEntryDocumentV1[array.Count];
            for (var index = 0; index < entries.Length; index++)
            {
                var path = "careerEntries[" + index + "]";
                var entry = Object(array[index], path);
                entry.RequireExactly(
                    path,
                    "saveId",
                    "careerName",
                    "playerDisplayName",
                    "progressionKind",
                    "phase",
                    "season",
                    "week",
                    "updatedAtUtcMs",
                    "loadability");
                entries[index] = new CareerIndexEntryDocumentV1
                {
                    saveId = String(entry.Get("saveId"), path + ".saveId"),
                    careerName = String(entry.Get("careerName"), path + ".careerName"),
                    playerDisplayName = String(
                        entry.Get("playerDisplayName"),
                        path + ".playerDisplayName"),
                    progressionKind = String(
                        entry.Get("progressionKind"),
                        path + ".progressionKind"),
                    phase = String(entry.Get("phase"), path + ".phase"),
                    season = Int32(entry.Get("season"), path + ".season"),
                    week = Int32(entry.Get("week"), path + ".week"),
                    updatedAtUtcMs = Integer(
                        entry.Get("updatedAtUtcMs"),
                        path + ".updatedAtUtcMs"),
                    loadability = String(entry.Get("loadability"), path + ".loadability")
                };
            }

            return entries;
        }

        private static LocalProfileCatalogDocumentV1 ReadCatalog(StrictJsonValue root)
        {
            var value = Object(root, "$catalog");
            value.RequireExactly(
                "$catalog",
                "schemaVersion",
                "catalogRevision",
                "catalogHash",
                "profiles");
            var profiles = Array(value.Get("profiles"), "profiles");
            var profileDocuments = new LocalProfileCatalogEntryDocumentV1[profiles.Count];
            for (var index = 0; index < profileDocuments.Length; index++)
            {
                var path = "profiles[" + index + "]";
                var profile = Object(profiles[index], path);
                profile.RequireExactly(
                    path,
                    "profileId",
                    "displayName",
                    "updatedAtUtcMs",
                    "loadability");
                profileDocuments[index] = new LocalProfileCatalogEntryDocumentV1
                {
                    profileId = String(profile.Get("profileId"), path + ".profileId"),
                    displayName = String(profile.Get("displayName"), path + ".displayName"),
                    updatedAtUtcMs = Integer(
                        profile.Get("updatedAtUtcMs"),
                        path + ".updatedAtUtcMs"),
                    loadability = String(profile.Get("loadability"), path + ".loadability")
                };
            }

            return new LocalProfileCatalogDocumentV1
            {
                schemaVersion = Int32(value.Get("schemaVersion"), "schemaVersion"),
                catalogRevision = Integer(value.Get("catalogRevision"), "catalogRevision"),
                catalogHash = String(value.Get("catalogHash"), "catalogHash"),
                profiles = profileDocuments
            };
        }

        private static string ComputeProfileDocumentHash(LocalPlayerProfileDocumentV1 document)
        {
            return Sha256(WriteProfile(document, false));
        }

        private static string ComputeCatalogDocumentHash(LocalProfileCatalogDocumentV1 document)
        {
            return Sha256(WriteCatalog(document, false));
        }

        private static string Sha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                var characters = new char[hash.Length * 2];
                const string digits = "0123456789abcdef";
                for (var index = 0; index < hash.Length; index++)
                {
                    characters[index * 2] = digits[hash[index] >> 4];
                    characters[(index * 2) + 1] = digits[hash[index] & 0x0f];
                }

                return new string(characters);
            }
        }

        private static void RequireMatchingHash(string actual, string expected, string message)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new FormatException(message);
            }
        }

        private static void RequireCanonical(byte[] canonical, byte[] actual, string documentName)
        {
            if (canonical.Length != actual.Length)
            {
                throw new FormatException("The " + documentName + " is not canonical Schema V1 JSON.");
            }

            var difference = 0;
            for (var index = 0; index < canonical.Length; index++)
            {
                difference |= canonical[index] ^ actual[index];
            }

            if (difference != 0)
            {
                throw new FormatException("The " + documentName + " is not canonical Schema V1 JSON.");
            }
        }

        private static byte[] RequiredBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return bytes;
        }

        private static StrictJsonObject Object(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.Object)
            {
                throw new FormatException(path + " must be a JSON object.");
            }

            return value.ObjectValue;
        }

        private static IReadOnlyList<StrictJsonValue> Array(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.Array)
            {
                throw new FormatException(path + " must be a JSON array.");
            }

            return value.ArrayValue;
        }

        private static string String(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.String)
            {
                throw new FormatException(path + " must be a JSON string.");
            }

            return value.StringValue;
        }

        private static long Integer(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.Integer)
            {
                throw new FormatException(path + " must be a JSON integer.");
            }

            return value.IntegerValue;
        }

        private static int Int32(StrictJsonValue value, string path)
        {
            var integer = Integer(value, path);
            if (integer < int.MinValue || integer > int.MaxValue)
            {
                throw new FormatException(path + " must fit in a signed 32-bit integer.");
            }

            return (int)integer;
        }

        private static Guid ParseGuid(string value, string path)
        {
            if (!Guid.TryParseExact(value, "D", out var parsed) ||
                parsed == Guid.Empty ||
                !string.Equals(parsed.ToString("D"), value, StringComparison.Ordinal))
            {
                throw new FormatException(path + " must be a canonical lowercase non-empty GUID.");
            }

            return parsed;
        }

        private static T Required<T>(T value, string path) where T : class
        {
            if (value == null)
            {
                throw new FormatException(path + " is required by local persistence Schema V1.");
            }

            return value;
        }

        private static string ProgressionName(CareerProgressionKind value)
        {
            switch (value)
            {
                case CareerProgressionKind.CareerCreated:
                    return "career_created";
                case CareerProgressionKind.Tryout:
                    return "tryout";
                case CareerProgressionKind.Planning:
                    return "planning";
                case CareerProgressionKind.Planned:
                    return "planned";
                case CareerProgressionKind.AwaitingEventChoice:
                    return "awaiting_event_choice";
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown progression kind.");
            }
        }

        private static CareerProgressionKind ParseProgression(string value)
        {
            switch (value)
            {
                case "career_created":
                    return CareerProgressionKind.CareerCreated;
                case "tryout":
                    return CareerProgressionKind.Tryout;
                case "planning":
                    return CareerProgressionKind.Planning;
                case "planned":
                    return CareerProgressionKind.Planned;
                case "awaiting_event_choice":
                    return CareerProgressionKind.AwaitingEventChoice;
                default:
                    throw new FormatException("Unknown Career progression kind.");
            }
        }

        private static string PhaseName(CareerPhase value)
        {
            if (value != CareerPhase.University)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Career phase.");
            }

            return "university";
        }

        private static CareerPhase ParsePhase(string value)
        {
            if (!string.Equals(value, "university", StringComparison.Ordinal))
            {
                throw new FormatException("Unknown Career phase.");
            }

            return CareerPhase.University;
        }

        private static string CareerLoadabilityName(CareerLoadability value)
        {
            switch (value)
            {
                case CareerLoadability.Loadable:
                    return "loadable";
                case CareerLoadability.RecoveryAvailable:
                    return "recovery_available";
                case CareerLoadability.Missing:
                    return "missing";
                case CareerLoadability.Corrupt:
                    return "corrupt";
                case CareerLoadability.UnsupportedVersion:
                    return "unsupported_version";
                case CareerLoadability.Ambiguous:
                    return "ambiguous";
                case CareerLoadability.BackupDegraded:
                    return "backup_degraded";
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Career loadability.");
            }
        }

        private static CareerLoadability ParseCareerLoadability(string value)
        {
            switch (value)
            {
                case "loadable":
                    return CareerLoadability.Loadable;
                case "recovery_available":
                    return CareerLoadability.RecoveryAvailable;
                case "missing":
                    return CareerLoadability.Missing;
                case "corrupt":
                    return CareerLoadability.Corrupt;
                case "unsupported_version":
                    return CareerLoadability.UnsupportedVersion;
                case "ambiguous":
                    return CareerLoadability.Ambiguous;
                case "backup_degraded":
                    return CareerLoadability.BackupDegraded;
                default:
                    throw new FormatException("Unknown Career loadability.");
            }
        }

        private static string ProfileLoadabilityName(ProfileLoadability value)
        {
            switch (value)
            {
                case ProfileLoadability.Loadable:
                    return "loadable";
                case ProfileLoadability.Missing:
                    return "missing";
                case ProfileLoadability.Corrupt:
                    return "corrupt";
                case ProfileLoadability.UnsupportedVersion:
                    return "unsupported_version";
                case ProfileLoadability.Ambiguous:
                    return "ambiguous";
                case ProfileLoadability.RecoveryAvailable:
                    return "recovery_available";
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown profile loadability.");
            }
        }

        private static ProfileLoadability ParseProfileLoadability(string value)
        {
            switch (value)
            {
                case "loadable":
                    return ProfileLoadability.Loadable;
                case "missing":
                    return ProfileLoadability.Missing;
                case "corrupt":
                    return ProfileLoadability.Corrupt;
                case "unsupported_version":
                    return ProfileLoadability.UnsupportedVersion;
                case "ambiguous":
                    return ProfileLoadability.Ambiguous;
                case "recovery_available":
                    return ProfileLoadability.RecoveryAvailable;
                default:
                    throw new FormatException("Unknown profile loadability.");
            }
        }
    }
}
