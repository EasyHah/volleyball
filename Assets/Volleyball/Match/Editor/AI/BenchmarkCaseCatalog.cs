using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Volleyball.Editor.AI
{
    public enum BenchmarkCaseKind
    {
        Round,
        Touch
    }

    public sealed class BenchmarkCase
    {
        public BenchmarkCase(
            string id,
            BenchmarkCaseKind kind,
            int deadlineMilliseconds,
            int countedTeamTouches,
            string state,
            string preferredJson,
            IReadOnlyList<string> acceptableGuidance,
            IReadOnlyList<string> forbiddenGuidance,
            string rationale)
        {
            Id = id;
            Kind = kind;
            DeadlineMilliseconds = deadlineMilliseconds;
            CountedTeamTouches = countedTeamTouches;
            State = state;
            PreferredJson = preferredJson;
            AcceptableGuidance = acceptableGuidance;
            ForbiddenGuidance = forbiddenGuidance;
            Rationale = rationale;
        }

        public string Id { get; }

        public BenchmarkCaseKind Kind { get; }

        public int DeadlineMilliseconds { get; }

        public int CountedTeamTouches { get; }

        public string State { get; }

        public string PreferredJson { get; }

        public IReadOnlyList<string> AcceptableGuidance { get; }

        public IReadOnlyList<string> ForbiddenGuidance { get; }

        public string Rationale { get; }
    }

    public sealed class BenchmarkCaseCatalog
    {
        private static readonly string[] ExpectedIds =
            Enumerable.Range(1, 10).Select(index => $"R{index:00}")
                .Concat(Enumerable.Range(1, 10).Select(index => $"T{index:00}"))
                .ToArray();

        private BenchmarkCaseCatalog(IReadOnlyList<BenchmarkCase> cases)
        {
            Cases = cases;
        }

        public IReadOnlyList<BenchmarkCase> Cases { get; }

        public static BenchmarkCaseCatalog Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Fixture path is required.", nameof(path));
            }

            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(Directory.GetCurrentDirectory(), path);
            var root = JObject.Parse(File.ReadAllText(fullPath));
            var version = (int?)root["version"];
            if (version != 1)
            {
                throw new InvalidDataException("Benchmark fixture version must be 1.");
            }

            var caseTokens = root["cases"] as JArray;
            if (caseTokens == null)
            {
                throw new InvalidDataException("Benchmark fixture cases must be an array.");
            }

            var cases = caseTokens.Select(ParseCase).ToArray();
            ValidateCatalog(cases);
            return new BenchmarkCaseCatalog(Array.AsReadOnly(cases));
        }

        private static BenchmarkCase ParseCase(JToken token)
        {
            var obj = token as JObject;
            if (obj == null)
            {
                throw new InvalidDataException("Benchmark case must be an object.");
            }

            var id = RequiredString(obj, "id");
            var kind = ParseKind(RequiredString(obj, "kind"));
            var deadline = RequiredInt(obj, "deadline_ms");
            var countedTouches = RequiredInt(obj, "counted_team_touches");
            var state = RequiredString(obj, "state");
            var preferredJson = RequiredString(obj, "preferred_json");
            var acceptable = RequiredStringArray(obj, "acceptable_guidance");
            var forbidden = RequiredStringArray(obj, "forbidden_guidance");
            var rationale = RequiredString(obj, "rationale");

            return new BenchmarkCase(
                id,
                kind,
                deadline,
                countedTouches,
                state,
                preferredJson,
                acceptable,
                forbidden,
                rationale);
        }

        private static BenchmarkCaseKind ParseKind(string kind)
        {
            switch (kind)
            {
                case "round":
                    return BenchmarkCaseKind.Round;
                case "touch":
                    return BenchmarkCaseKind.Touch;
                default:
                    throw new InvalidDataException($"Unsupported benchmark case kind '{kind}'.");
            }
        }

        private static void ValidateCatalog(IReadOnlyList<BenchmarkCase> cases)
        {
            if (cases.Count != ExpectedIds.Length)
            {
                throw new InvalidDataException("Benchmark fixture must contain exactly 20 cases.");
            }

            CollectionEquals(cases.Select(item => item.Id), ExpectedIds, "Benchmark fixture IDs are not in the approved order.");
            if (cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != cases.Count)
            {
                throw new InvalidDataException("Benchmark fixture contains duplicate IDs.");
            }

            foreach (var item in cases)
            {
                ValidateCase(item);
            }

            var t07 = cases.Single(item => item.Id == "T07");
            if (t07.CountedTeamTouches != 0 ||
                !t07.State.Contains("拦网触球不计入本队三次触球"))
            {
                throw new InvalidDataException("T07 must preserve the block-touch exemption.");
            }
        }

        private static void ValidateCase(BenchmarkCase item)
        {
            var expectedDeadline = item.Kind == BenchmarkCaseKind.Round ? 2000 : 500;
            if (item.DeadlineMilliseconds != expectedDeadline)
            {
                throw new InvalidDataException($"{item.Id} has an invalid deadline.");
            }

            if (item.CountedTeamTouches < 0 || item.CountedTeamTouches > 2)
            {
                throw new InvalidDataException($"{item.Id} counted touches must be in 0..2.");
            }

            if (string.IsNullOrWhiteSpace(item.State) ||
                string.IsNullOrWhiteSpace(item.Rationale) ||
                item.AcceptableGuidance.Count == 0 ||
                item.ForbiddenGuidance.Count == 0)
            {
                throw new InvalidDataException($"{item.Id} has incomplete review guidance.");
            }

            if (item.AcceptableGuidance.Any(string.IsNullOrWhiteSpace) ||
                item.ForbiddenGuidance.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException($"{item.Id} contains empty guidance.");
            }

            if (item.Kind == BenchmarkCaseKind.Round)
            {
                DecisionJsonCodec.ParseRound(item.PreferredJson);
            }
            else
            {
                DecisionJsonCodec.ParseTouch(item.PreferredJson);
            }
        }

        private static void CollectionEquals(IEnumerable<string> actual, IEnumerable<string> expected, string message)
        {
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                throw new InvalidDataException(message);
            }
        }

        private static string RequiredString(JObject obj, string propertyName)
        {
            var token = obj[propertyName];
            if (token == null || token.Type != JTokenType.String)
            {
                throw new InvalidDataException($"{propertyName} must be a string.");
            }

            var value = token.Value<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"{propertyName} must be non-empty.");
            }

            return value;
        }

        private static int RequiredInt(JObject obj, string propertyName)
        {
            var token = obj[propertyName];
            if (token == null || token.Type != JTokenType.Integer)
            {
                throw new InvalidDataException($"{propertyName} must be an integer.");
            }

            return token.Value<int>();
        }

        private static IReadOnlyList<string> RequiredStringArray(JObject obj, string propertyName)
        {
            var token = obj[propertyName] as JArray;
            if (token == null)
            {
                throw new InvalidDataException($"{propertyName} must be an array.");
            }

            var values = token.Select(item =>
            {
                if (item.Type != JTokenType.String)
                {
                    throw new InvalidDataException($"{propertyName} must contain only strings.");
                }

                return item.Value<string>();
            }).ToArray();

            if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException($"{propertyName} must contain non-empty strings.");
            }

            return Array.AsReadOnly(values);
        }
    }

    public static class BenchmarkPromptBuilder
    {
        public static string Build(BenchmarkCase item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (item.Kind == BenchmarkCaseKind.Round)
            {
                return "你是 3v3 排球比赛 AI。只返回一个 JSON 对象，不要解释。\n" +
                       "字段必须恰好为 receiver, second_actor, set_route, third_actor, attack_route。\n" +
                       "receiver/second_actor/third_actor 可选 defender, setter, attacker；set_route 可选 left_pin, middle_quick, right_pin, back_set；attack_route 可选 line, cross_court, deep_seam, roll_shot。\n" +
                       "同一球员不得连续触球；第一次触球球员可以在第三次触球再次参与。\n" +
                       "局面：" + item.State;
            }

            return "你是 3v3 排球比赛 AI。只返回一个 JSON 对象，不要解释。\n" +
                   "字段必须恰好为 next_actor, action, target_zone, tempo, risk。\n" +
                   "next_actor 可选 defender, setter, attacker；action 可选 receive, set, attack, free_ball, emergency_save。\n" +
                   "target_zone 可选 left_front, middle_front, right_front, left_back, middle_back, right_back；tempo 可选 quick, normal, high；risk 可选 safe, balanced, aggressive。\n" +
                   "第三次计数触球必须选择 attack 或 free_ball；拦网触球不计入本队三次触球。\n" +
                   "当前已计数触球：" + item.CountedTeamTouches + "。\n" +
                   "局面：" + item.State;
        }
    }
}
