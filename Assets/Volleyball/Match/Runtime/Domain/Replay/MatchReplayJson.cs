using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Volleyball.Domain.Replay
{
    public static class MatchReplayJson
    {
        public static string Serialize(MatchReplayV1 replay)
        {
            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            replay.Validate();
            return SerializeValue(replay);
        }

        public static MatchReplayV1 Deserialize(string json)
        {
            var replay = DeserializeValue<MatchReplayV1>(json);
            replay.Validate();
            return replay;
        }

        internal static string SerializeCanonicalPayload(MatchReplayCanonicalPayloadV1 payload)
        {
            return SerializeValue(payload);
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
                throw new MatchReplayValidationException("Replay serialization failed.", exception);
            }
        }

        private static T DeserializeValue<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new MatchReplayValidationException("Replay JSON is required.");
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var value = serializer.ReadObject(stream) as T;
                return value ?? throw new MatchReplayValidationException("Replay JSON produced no value.");
            }
            catch (MatchReplayValidationException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is SerializationException ||
                exception is FormatException ||
                exception is ArgumentException)
            {
                throw new MatchReplayValidationException("Replay JSON is malformed.", exception);
            }
        }
    }
}
