using System;
using System.Security.Cryptography;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Persistence
{
    internal sealed class CareerRecoveryIntentV1
    {
        public CareerRecoveryIntentV1(
            ProfileId profileId,
            SaveId saveId,
            OperationId operationId,
            bool mainWasMissing,
            Sha256Digest? corruptMainFingerprint,
            CareerVersionToken backupVersionToken,
            CareerVersionToken recoveryVersionToken,
            string temporaryFileName,
            string quarantineFileName)
        {
            if (profileId.Value == Guid.Empty)
            {
                throw new ArgumentException("A recovery intent requires a profile ID.", nameof(profileId));
            }

            if (saveId.Value == Guid.Empty)
            {
                throw new ArgumentException("A recovery intent requires a save ID.", nameof(saveId));
            }

            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException("A recovery intent requires an operation ID.", nameof(operationId));
            }

            if (mainWasMissing == corruptMainFingerprint.HasValue)
            {
                throw new ArgumentException(
                    "A missing main has no corrupt fingerprint; an existing corrupt main requires one.",
                    nameof(corruptMainFingerprint));
            }

            if (backupVersionToken.LineageId.Equals(recoveryVersionToken.LineageId) ||
                recoveryVersionToken.Revision != backupVersionToken.Revision + 1)
            {
                throw new ArgumentException(
                    "A recovery version must use a new lineage and advance one source revision.",
                    nameof(recoveryVersionToken));
            }

            var expectedTemporary = saveId + ".recovery.tmp." + operationId;
            if (!string.Equals(temporaryFileName, expectedTemporary, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The recovery temporary file name must be derived from the save and operation IDs.",
                    nameof(temporaryFileName));
            }

            ValidateQuarantineFileName(saveId, operationId, quarantineFileName);
            ProfileId = profileId;
            SaveId = saveId;
            OperationId = operationId;
            MainWasMissing = mainWasMissing;
            CorruptMainFingerprint = corruptMainFingerprint;
            BackupVersionToken = backupVersionToken;
            RecoveryVersionToken = recoveryVersionToken;
            TemporaryFileName = temporaryFileName;
            QuarantineFileName = quarantineFileName;
        }

        public ProfileId ProfileId { get; }

        public SaveId SaveId { get; }

        public OperationId OperationId { get; }

        public bool MainWasMissing { get; }

        public Sha256Digest? CorruptMainFingerprint { get; }

        public CareerVersionToken BackupVersionToken { get; }

        public CareerVersionToken RecoveryVersionToken { get; }

        public string TemporaryFileName { get; }

        public string QuarantineFileName { get; }

        private static void ValidateQuarantineFileName(
            SaveId saveId,
            OperationId operationId,
            string fileName)
        {
            if (string.IsNullOrEmpty(fileName) ||
                fileName.IndexOf('/') >= 0 ||
                fileName.IndexOf('\\') >= 0)
            {
                throw new ArgumentException(
                    "A recovery quarantine value must be a file name, not a path.",
                    nameof(fileName));
            }

            var prefix = saveId + ".corrupt." + operationId + ".";
            const string suffix = ".json";
            if (!fileName.StartsWith(prefix, StringComparison.Ordinal) ||
                !fileName.EndsWith(suffix, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The recovery quarantine file name must match the recovery operation.",
                    nameof(fileName));
            }

            var number = fileName.Substring(
                prefix.Length,
                fileName.Length - prefix.Length - suffix.Length);
            if (number.Length == 0 || (number.Length > 1 && number[0] == '0'))
            {
                throw new ArgumentException("The quarantine conflict index is not canonical.", nameof(fileName));
            }

            for (var index = 0; index < number.Length; index++)
            {
                if (number[index] < '0' || number[index] > '9')
                {
                    throw new ArgumentException("The quarantine conflict index is invalid.", nameof(fileName));
                }
            }
        }
    }

    internal static class CareerRecoveryIntentJsonCodec
    {
        private const int SchemaVersion = 1;

        public static byte[] Serialize(CareerRecoveryIntentV1 intent)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            var hash = ComputeHash(intent);
            return Write(intent, hash.Value, true);
        }

        public static CareerRecoveryIntentV1 Deserialize(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var root = StrictJsonReader.Parse(bytes);
            if (root.Kind != StrictJsonKind.Object)
            {
                throw new FormatException("A recovery intent must be a JSON object.");
            }

            var value = root.ObjectValue;
            value.RequireExactly(
                "$recoveryIntent",
                "schemaVersion",
                "profileId",
                "saveId",
                "operationId",
                "mainWasMissing",
                "corruptMainFingerprint",
                "backupVersionToken",
                "recoveryVersionToken",
                "temporaryFileName",
                "quarantineFileName",
                "intentHash");
            if (Int64(value.Get("schemaVersion"), "schemaVersion") != SchemaVersion)
            {
                throw new FormatException("The recovery intent schema version is unsupported.");
            }

            var intent = new CareerRecoveryIntentV1(
                new ProfileId(GuidValue(value.Get("profileId"), "profileId")),
                new SaveId(GuidValue(value.Get("saveId"), "saveId")),
                new OperationId(GuidValue(value.Get("operationId"), "operationId")),
                Boolean(value.Get("mainWasMissing"), "mainWasMissing"),
                NullableHash(value.Get("corruptMainFingerprint"), "corruptMainFingerprint"),
                VersionToken(value.Get("backupVersionToken"), "backupVersionToken"),
                VersionToken(value.Get("recoveryVersionToken"), "recoveryVersionToken"),
                String(value.Get("temporaryFileName"), "temporaryFileName"),
                String(value.Get("quarantineFileName"), "quarantineFileName"));
            var actualHash = new Sha256Digest(String(value.Get("intentHash"), "intentHash"));
            if (!actualHash.Equals(ComputeHash(intent)))
            {
                throw new FormatException("The recovery intent hash is invalid.");
            }

            var canonical = Write(intent, actualHash.Value, true);
            if (!BytesEqual(canonical, bytes))
            {
                throw new FormatException("The recovery intent is not canonical Schema V1 JSON.");
            }

            return intent;
        }

        private static Sha256Digest ComputeHash(CareerRecoveryIntentV1 intent)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Write(intent, null, false));
                var characters = new char[hash.Length * 2];
                const string digits = "0123456789abcdef";
                for (var index = 0; index < hash.Length; index++)
                {
                    characters[index * 2] = digits[hash[index] >> 4];
                    characters[(index * 2) + 1] = digits[hash[index] & 0x0f];
                }

                return new Sha256Digest(new string(characters));
            }
        }

        private static byte[] Write(
            CareerRecoveryIntentV1 intent,
            string intentHash,
            bool includeHash)
        {
            var writer = new CanonicalJsonWriter();
            writer.WriteStartObject();
            writer.WritePropertyName("schemaVersion");
            writer.WriteInt64(SchemaVersion);
            writer.WritePropertyName("profileId");
            writer.WriteString(intent.ProfileId.ToString());
            writer.WritePropertyName("saveId");
            writer.WriteString(intent.SaveId.ToString());
            writer.WritePropertyName("operationId");
            writer.WriteString(intent.OperationId.ToString());
            writer.WritePropertyName("mainWasMissing");
            writer.WriteBoolean(intent.MainWasMissing);
            writer.WritePropertyName("corruptMainFingerprint");
            writer.WriteNullableString(
                intent.CorruptMainFingerprint.HasValue
                    ? intent.CorruptMainFingerprint.Value.Value
                    : null);
            writer.WritePropertyName("backupVersionToken");
            WriteVersionToken(writer, intent.BackupVersionToken);
            writer.WritePropertyName("recoveryVersionToken");
            WriteVersionToken(writer, intent.RecoveryVersionToken);
            writer.WritePropertyName("temporaryFileName");
            writer.WriteString(intent.TemporaryFileName);
            writer.WritePropertyName("quarantineFileName");
            writer.WriteString(intent.QuarantineFileName);
            if (includeHash)
            {
                writer.WritePropertyName("intentHash");
                writer.WriteString(intentHash);
            }

            writer.WriteEndObject();
            return writer.ToUtf8Bytes();
        }

        private static void WriteVersionToken(
            CanonicalJsonWriter writer,
            CareerVersionToken token)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("lineageId");
            writer.WriteString(token.LineageId.ToString());
            writer.WritePropertyName("revision");
            writer.WriteInt64(token.Revision);
            writer.WritePropertyName("snapshotHash");
            writer.WriteString(token.SnapshotHash.Value);
            writer.WriteEndObject();
        }

        private static CareerVersionToken VersionToken(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.Object)
            {
                throw new FormatException(path + " must be an object.");
            }

            var token = value.ObjectValue;
            token.RequireExactly(path, "lineageId", "revision", "snapshotHash");
            return new CareerVersionToken(
                new LineageId(GuidValue(token.Get("lineageId"), path + ".lineageId")),
                Int64(token.Get("revision"), path + ".revision"),
                new Sha256Digest(String(token.Get("snapshotHash"), path + ".snapshotHash")));
        }

        private static Sha256Digest? NullableHash(StrictJsonValue value, string path)
        {
            return value.Kind == StrictJsonKind.Null
                ? (Sha256Digest?)null
                : new Sha256Digest(String(value, path));
        }

        private static Guid GuidValue(StrictJsonValue value, string path)
        {
            var text = String(value, path);
            if (!Guid.TryParseExact(text, "D", out var parsed) ||
                parsed == Guid.Empty ||
                !string.Equals(parsed.ToString("D"), text, StringComparison.Ordinal))
            {
                throw new FormatException(path + " must be a canonical lowercase non-empty GUID.");
            }

            return parsed;
        }

        private static string String(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.String)
            {
                throw new FormatException(path + " must be a string.");
            }

            return value.StringValue;
        }

        private static long Int64(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.Integer)
            {
                throw new FormatException(path + " must be an integer.");
            }

            return value.IntegerValue;
        }

        private static bool Boolean(StrictJsonValue value, string path)
        {
            if (value.Kind != StrictJsonKind.Boolean)
            {
                throw new FormatException(path + " must be a boolean.");
            }

            return value.BooleanValue;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }
    }
}
