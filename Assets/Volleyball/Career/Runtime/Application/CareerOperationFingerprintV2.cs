using System;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Career.Domain;

namespace Volleyball.Career.Application
{
    public static class CareerOperationFingerprintV2
    {
        public const int SchemaVersion = 2;

        public static byte[] Encode(CreateCareerCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var builder = new StringBuilder(600);
            builder.Append("{\"fingerprintSchemaVersion\":2,\"operationKind\":\"create_career\"");
            AppendString(builder, "profileId", command.ProfileId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "saveId", command.SaveId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "lineageId", command.LineageId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "playerId", command.PlayerStableId);
            AppendString(builder, "careerName", command.CareerName);
            AppendString(builder, "playerName", command.PlayerName);
            AppendInteger(builder, "jerseyNumber", command.JerseyNumber);
            builder.Append(",\"tryoutOccurrenceIds\":[");
            for (var index = 0; index < command.TryoutOccurrenceIds.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }

                AppendJsonString(
                    builder,
                    command.TryoutOccurrenceIds[index].Value.ToString("D").ToLowerInvariant());
            }

            builder.Append(']');
            AppendVersions(builder);
            builder.Append('}');
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        public static byte[] Encode(
            ConfirmTryoutStageCommand command,
            OccurrenceId persistedTryoutOccurrenceId)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (persistedTryoutOccurrenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "A persisted tryout occurrence ID is required.",
                    nameof(persistedTryoutOccurrenceId));
            }

            var builder = new StringBuilder(650);
            builder.Append("{\"fingerprintSchemaVersion\":2,\"operationKind\":\"confirm_tryout_stage\"");
            AppendString(builder, "profileId", command.ProfileId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "saveId", command.SaveId.Value.ToString("D").ToLowerInvariant());
            AppendString(
                builder,
                "expectedLineageId",
                command.ExpectedVersionToken.LineageId.Value.ToString("D").ToLowerInvariant());
            AppendInteger(builder, "expectedRevision", command.ExpectedVersionToken.Revision);
            AppendString(
                builder,
                "expectedSnapshotHash",
                command.ExpectedVersionToken.SnapshotHash.Value);
            AppendInteger(builder, "stageNumber", command.StageNumber);
            AppendString(builder, "choiceId", command.ChoiceId);
            AppendString(
                builder,
                "tryoutOccurrenceId",
                persistedTryoutOccurrenceId.Value.ToString("D").ToLowerInvariant());
            AppendNullableId(
                builder,
                "weekPlanId",
                command.EnrollmentIds == null
                    ? (Guid?)null
                    : command.EnrollmentIds.WeekPlanId.Value);
            AppendNullableId(
                builder,
                "matchSlotActionId",
                command.EnrollmentIds == null
                    ? (Guid?)null
                    : command.EnrollmentIds.MatchSlotActionId.Value);
            AppendNullableId(
                builder,
                "matchOccurrenceId",
                command.EnrollmentIds == null
                    ? (Guid?)null
                    : command.EnrollmentIds.MatchOccurrenceId.Value);
            AppendVersions(builder);
            builder.Append('}');
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        public static byte[] Encode(ConfirmWeekPlanCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var builder = new StringBuilder(1000);
            builder.Append("{\"fingerprintSchemaVersion\":2,\"operationKind\":\"confirm_week_plan\"");
            AppendString(builder, "profileId", command.ProfileId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "saveId", command.SaveId.Value.ToString("D").ToLowerInvariant());
            AppendString(
                builder,
                "expectedLineageId",
                command.ExpectedVersionToken.LineageId.Value.ToString("D").ToLowerInvariant());
            AppendInteger(builder, "expectedRevision", command.ExpectedVersionToken.Revision);
            AppendString(
                builder,
                "expectedSnapshotHash",
                command.ExpectedVersionToken.SnapshotHash.Value);

            var plan = command.CandidatePlan;
            AppendString(builder, "planId", plan.PlanId.Value.ToString("D").ToLowerInvariant());
            AppendInteger(builder, "season", plan.Season);
            AppendInteger(builder, "week", plan.Week);
            builder.Append(",\"slots\":[");
            for (var index = 0; index < plan.Slots.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }

                var slot = plan.Slots[index];
                builder.Append('{');
                builder.Append("\"slotActionId\":");
                AppendJsonString(
                    builder,
                    slot.SlotActionId.Value.ToString("D").ToLowerInvariant());
                AppendString(
                    builder,
                    "occurrenceId",
                    slot.OccurrenceId.Value.ToString("D").ToLowerInvariant());
                AppendString(builder, "kind", FormatActionKind(slot.Kind));
                AppendString(builder, "contentId", slot.ContentId);
                builder.Append('}');
            }

            builder.Append(']');
            AppendVersions(builder);
            builder.Append('}');
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        public static byte[] Encode(ExecuteWeekActionCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var builder = new StringBuilder(800);
            builder.Append("{\"fingerprintSchemaVersion\":2,\"operationKind\":\"execute_week_action\"");
            AppendString(builder, "profileId", command.ProfileId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "saveId", command.SaveId.Value.ToString("D").ToLowerInvariant());
            AppendString(
                builder,
                "expectedLineageId",
                command.ExpectedVersionToken.LineageId.Value.ToString("D").ToLowerInvariant());
            AppendInteger(builder, "expectedRevision", command.ExpectedVersionToken.Revision);
            AppendString(
                builder,
                "expectedSnapshotHash",
                command.ExpectedVersionToken.SnapshotHash.Value);
            AppendString(builder, "weekPlanId", command.WeekPlanId.Value.ToString("D").ToLowerInvariant());
            AppendInteger(builder, "slotNumber", command.SlotNumber);
            AppendString(builder, "slotActionId", command.SlotActionId.Value.ToString("D").ToLowerInvariant());
            AppendString(
                builder,
                "actionOccurrenceId",
                command.ActionOccurrenceId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "contentId", command.ContentId);
            AppendNullableId(
                builder,
                "triggeredEventOccurrenceId",
                command.TriggeredEventOccurrenceId.HasValue
                    ? new Guid?(command.TriggeredEventOccurrenceId.Value.Value)
                    : null);
            AppendVersions(builder);
            builder.Append('}');
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        public static byte[] Encode(ResolveEventChoiceCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var builder = new StringBuilder(850);
            builder.Append("{\"fingerprintSchemaVersion\":2,\"operationKind\":\"resolve_event_choice\"");
            AppendString(builder, "profileId", command.ProfileId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "saveId", command.SaveId.Value.ToString("D").ToLowerInvariant());
            AppendString(
                builder,
                "expectedLineageId",
                command.ExpectedVersionToken.LineageId.Value.ToString("D").ToLowerInvariant());
            AppendInteger(builder, "expectedRevision", command.ExpectedVersionToken.Revision);
            AppendString(
                builder,
                "expectedSnapshotHash",
                command.ExpectedVersionToken.SnapshotHash.Value);
            AppendString(builder, "weekPlanId", command.WeekPlanId.Value.ToString("D").ToLowerInvariant());
            AppendString(
                builder,
                "sourceSlotActionId",
                command.SourceSlotActionId.Value.ToString("D").ToLowerInvariant());
            AppendString(
                builder,
                "sourceActionOccurrenceId",
                command.SourceActionOccurrenceId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "eventId", command.EventId);
            AppendString(
                builder,
                "eventOccurrenceId",
                command.EventOccurrenceId.Value.ToString("D").ToLowerInvariant());
            AppendString(builder, "optionId", command.OptionId);
            AppendVersions(builder);
            builder.Append('}');
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        public static Sha256Digest Hash(CreateCareerCommand command)
        {
            return HashBytes(Encode(command));
        }

        public static Sha256Digest Hash(
            ConfirmTryoutStageCommand command,
            OccurrenceId persistedTryoutOccurrenceId)
        {
            return HashBytes(Encode(command, persistedTryoutOccurrenceId));
        }

        public static Sha256Digest Hash(ConfirmWeekPlanCommand command)
        {
            return HashBytes(Encode(command));
        }

        public static Sha256Digest Hash(ExecuteWeekActionCommand command)
        {
            return HashBytes(Encode(command));
        }

        public static Sha256Digest Hash(ResolveEventChoiceCommand command)
        {
            return HashBytes(Encode(command));
        }

        private static string FormatActionKind(CareerWeekActionKind kind)
        {
            switch (kind)
            {
                case CareerWeekActionKind.SpecializedTraining: return "specialized_training";
                case CareerWeekActionKind.StrengthTraining: return "strength_training";
                case CareerWeekActionKind.TeamPractice: return "team_practice";
                case CareerWeekActionKind.Rest: return "rest";
                case CareerWeekActionKind.Match: return "match";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static void AppendVersions(StringBuilder builder)
        {
            AppendInteger(builder, "schemaVersion", CareerSaveVersions.CurrentSchemaVersion);
            AppendInteger(builder, "contentVersion", CareerSaveVersions.CurrentContentVersion);
            AppendInteger(builder, "rulesetVersion", CareerSaveVersions.CurrentRulesetVersion);
            AppendInteger(builder, "contractVersion", CareerSaveVersions.CurrentContractVersion);
            AppendInteger(
                builder,
                "careerRandomAlgorithmVersion",
                CareerSaveVersions.CurrentCareerRandomAlgorithmVersion);
        }

        private static void AppendString(StringBuilder builder, string name, string value)
        {
            builder.Append(",\"").Append(name).Append("\":");
            AppendJsonString(builder, value);
        }

        private static void AppendInteger(StringBuilder builder, string name, long value)
        {
            builder.Append(",\"").Append(name).Append("\":");
            builder.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void AppendNullableId(
            StringBuilder builder,
            string name,
            Guid? value)
        {
            builder.Append(",\"").Append(name).Append("\":");
            if (!value.HasValue)
            {
                builder.Append("null");
                return;
            }

            AppendJsonString(builder, value.Value.ToString("D").ToLowerInvariant());
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            builder.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\r': builder.Append("\\r"); break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        }
                        else if (char.IsHighSurrogate(character))
                        {
                            if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                            {
                                throw new ArgumentException(
                                    "Canonical strings cannot contain unpaired surrogates.",
                                    nameof(value));
                            }

                            builder.Append(character).Append(value[++index]);
                        }
                        else if (char.IsLowSurrogate(character))
                        {
                            throw new ArgumentException(
                                "Canonical strings cannot contain unpaired surrogates.",
                                nameof(value));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private static Sha256Digest HashBytes(byte[] bytes)
        {
            byte[] digest;
            using (var sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            var characters = new char[digest.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (var index = 0; index < digest.Length; index++)
            {
                characters[index * 2] = alphabet[digest[index] >> 4];
                characters[(index * 2) + 1] = alphabet[digest[index] & 15];
            }

            return new Sha256Digest(new string(characters));
        }
    }
}
