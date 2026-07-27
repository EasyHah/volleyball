using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public static class ContractJson
    {
        public static string Serialize(MatchContextV1 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Validate();
            return SerializeValue(context);
        }

        public static string Serialize(MatchResultV1 result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Validate();
            return SerializeValue(result);
        }

        public static MatchContextV1 DeserializeContext(string json)
        {
            var context = DeserializeValue<MatchContextV1>(json);
            context.Validate();
            return context;
        }

        public static MatchResultV1 DeserializeResult(string json)
        {
            var result = DeserializeValue<MatchResultV1>(json);
            result.Validate();
            return result;
        }

        public static string SerializeV2(MatchContextV2 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Validate();
            return SerializeValue(context);
        }

        public static string SerializeV2(MatchResultV2 result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Validate();
            return SerializeValue(result);
        }

        public static MatchContextV2 DeserializeContextV2(string json)
        {
            var context = DeserializeValue<MatchContextV2>(json);
            context.Validate();
            return context;
        }

        public static MatchResultV2 DeserializeResultV2(string json)
        {
            var result = DeserializeValue<MatchResultV2>(json);
            result.Validate();
            return result;
        }

        public static string SerializeV3(MatchContextV3 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Validate();
            return SerializeValue(context);
        }

        public static string SerializeV3(MatchResultV3 result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Validate();
            return SerializeValue(result);
        }

        public static MatchContextV3 DeserializeContextV3(string json)
        {
            var context = DeserializeValue<MatchContextV3>(json);
            context.Validate();
            return context;
        }

        public static MatchResultV3 DeserializeResultV3(string json)
        {
            var result = DeserializeValue<MatchResultV3>(json);
            result.Validate();
            return result;
        }

        public static string SerializeReplayV2(MatchReplayV2 replay)
        {
            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            replay.Validate();
            return SerializeValue(replay);
        }

        public static MatchReplayV2 DeserializeReplayV2(string json)
        {
            var replay = DeserializeValue<MatchReplayV2>(json);
            replay.Validate();
            return replay;
        }

        private static string SerializeValue<T>(T value)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using var stream = new MemoryStream();
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
            catch (SerializationException exception)
            {
                throw new ContractValidationException("Contract serialization failed.", exception);
            }
        }

        private static T DeserializeValue<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ContractValidationException("Contract JSON is required.");
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var value = serializer.ReadObject(stream) as T;
                return value ?? throw new ContractValidationException("Contract JSON produced no value.");
            }
            catch (ContractValidationException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is SerializationException ||
                exception is FormatException ||
                exception is ArgumentException)
            {
                throw new ContractValidationException("Contract JSON is malformed.", exception);
            }
        }
    }
}
