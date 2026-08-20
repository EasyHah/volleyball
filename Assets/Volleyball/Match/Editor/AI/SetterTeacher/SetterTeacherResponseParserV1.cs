using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Volleyball.Shared.Contracts;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public static class SetterTeacherResponseParserV1
    {
        private static readonly string[] Keys =
            { "schemaVersion", "ranking", "reason" };

        public static SetterTeacherResponseV1 Parse(
            string json,
            SetterTeacherRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var value = Load(json);
            if (value.Properties().Select(item => item.Name).Count() != Keys.Length ||
                !new HashSet<string>(value.Properties().Select(item => item.Name),
                    StringComparer.Ordinal).SetEquals(Keys))
                throw new ArgumentException("Setter teacher response has missing or extra fields.", nameof(json));
            if (value["schemaVersion"]?.Type != JTokenType.Integer ||
                value["schemaVersion"].Value<int>() != SetterTeacherRequestV1.SchemaVersion ||
                value["ranking"]?.Type != JTokenType.Array ||
                value["reason"]?.Type != JTokenType.String ||
                string.IsNullOrWhiteSpace(value["reason"].Value<string>()))
                throw new ArgumentException("Setter teacher response has invalid fields.", nameof(json));

            var ranking = value["ranking"].Values<string>()
                .Select(item => new PlayerId(item))
                .ToArray();
            var expected = request.Candidates.Select(item => item.PlayerId)
                .ToArray();
            if (ranking.Length != expected.Length ||
                ranking.Any(item => string.IsNullOrWhiteSpace(item.Value)) ||
                ranking.Distinct().Count() != ranking.Length ||
                !new HashSet<PlayerId>(ranking).SetEquals(expected))
                throw new ArgumentException("Setter teacher ranking must exactly cover feasible candidates.", nameof(json));
            return new SetterTeacherResponseV1(ranking, value["reason"].Value<string>());
        }

        private static JObject Load(string json)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(json));
                var settings = new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Ignore
                };
                var value = JObject.Load(reader, settings);
                if (reader.Read()) throw new ArgumentException("Setter teacher response has trailing content.", nameof(json));
                return value;
            }
            catch (JsonException exception)
            {
                throw new ArgumentException("Setter teacher response is not valid JSON.", nameof(json), exception);
            }
        }
    }
}
