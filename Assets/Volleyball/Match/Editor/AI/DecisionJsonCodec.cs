using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Volleyball.AI;
using Volleyball.Domain.Prototype;

namespace Volleyball.Editor.AI
{
    public sealed class DecisionFormatException : Exception
    {
        public DecisionFormatException(string message)
            : base(message)
        {
        }

        public DecisionFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public static class DecisionJsonCodec
    {
        private static readonly string[] RoundKeys =
        {
            "attack_route",
            "receiver",
            "second_actor",
            "set_route",
            "third_actor"
        };

        private static readonly string[] TouchKeys =
        {
            "action",
            "next_actor",
            "risk",
            "target_zone",
            "tempo"
        };

        private static readonly Dictionary<string, PlayerRole> PlayerRoles =
            new Dictionary<string, PlayerRole>(StringComparer.Ordinal)
            {
                ["defender"] = PlayerRole.Defender,
                ["setter"] = PlayerRole.Setter,
                ["attacker"] = PlayerRole.Attacker
            };

        private static readonly Dictionary<string, SetRoute> SetRoutes =
            new Dictionary<string, SetRoute>(StringComparer.Ordinal)
            {
                ["left_pin"] = SetRoute.LeftPin,
                ["middle_quick"] = SetRoute.MiddleQuick,
                ["right_pin"] = SetRoute.RightPin,
                ["back_set"] = SetRoute.BackSet
            };

        private static readonly Dictionary<string, SpikeRoute> SpikeRoutes =
            new Dictionary<string, SpikeRoute>(StringComparer.Ordinal)
            {
                ["line"] = SpikeRoute.Line,
                ["cross_court"] = SpikeRoute.CrossCourt,
                ["deep_seam"] = SpikeRoute.DeepSeam,
                ["roll_shot"] = SpikeRoute.RollShot
            };

        private static readonly Dictionary<string, TouchDecisionAction> TouchActions =
            new Dictionary<string, TouchDecisionAction>(StringComparer.Ordinal)
            {
                ["receive"] = TouchDecisionAction.Receive,
                ["set"] = TouchDecisionAction.Set,
                ["attack"] = TouchDecisionAction.Attack,
                ["free_ball"] = TouchDecisionAction.FreeBall,
                ["emergency_save"] = TouchDecisionAction.EmergencySave
            };

        private static readonly Dictionary<string, TargetZone> TargetZones =
            new Dictionary<string, TargetZone>(StringComparer.Ordinal)
            {
                ["left_front"] = TargetZone.LeftFront,
                ["middle_front"] = TargetZone.MiddleFront,
                ["right_front"] = TargetZone.RightFront,
                ["left_back"] = TargetZone.LeftBack,
                ["middle_back"] = TargetZone.MiddleBack,
                ["right_back"] = TargetZone.RightBack
            };

        private static readonly Dictionary<string, DecisionTempo> Tempos =
            new Dictionary<string, DecisionTempo>(StringComparer.Ordinal)
            {
                ["quick"] = DecisionTempo.Quick,
                ["normal"] = DecisionTempo.Normal,
                ["high"] = DecisionTempo.High
            };

        private static readonly Dictionary<string, DecisionRisk> Risks =
            new Dictionary<string, DecisionRisk>(StringComparer.Ordinal)
            {
                ["safe"] = DecisionRisk.Safe,
                ["balanced"] = DecisionRisk.Balanced,
                ["aggressive"] = DecisionRisk.Aggressive
            };

        public static RoundDecisionV1 ParseRound(string json)
        {
            var obj = LoadExactObject(json);
            RequireExactKeys(obj, RoundKeys);

            try
            {
                return new RoundDecisionV1(
                    ReadMapped(obj, "receiver", PlayerRoles),
                    ReadMapped(obj, "second_actor", PlayerRoles),
                    ReadMapped(obj, "set_route", SetRoutes),
                    ReadMapped(obj, "third_actor", PlayerRoles),
                    ReadMapped(obj, "attack_route", SpikeRoutes));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is ArgumentOutOfRangeException)
            {
                throw new DecisionFormatException("Round decision violates protocol constraints.", exception);
            }
        }

        public static TouchDecisionV1 ParseTouch(string json)
        {
            var obj = LoadExactObject(json);
            RequireExactKeys(obj, TouchKeys);

            try
            {
                return new TouchDecisionV1(
                    ReadMapped(obj, "next_actor", PlayerRoles),
                    ReadMapped(obj, "action", TouchActions),
                    ReadMapped(obj, "target_zone", TargetZones),
                    ReadMapped(obj, "tempo", Tempos),
                    ReadMapped(obj, "risk", Risks));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is ArgumentOutOfRangeException)
            {
                throw new DecisionFormatException("Touch decision violates protocol constraints.", exception);
            }
        }

        private static JObject LoadExactObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new DecisionFormatException("Decision JSON is empty.");
            }

            try
            {
                using (var stringReader = new StringReader(json))
                using (var jsonReader = new JsonTextReader(stringReader))
                {
                    var settings = new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Ignore,
                        LineInfoHandling = LineInfoHandling.Load
                    };

                    var obj = JObject.Load(jsonReader, settings);
                    if (jsonReader.Read())
                    {
                        throw new DecisionFormatException("Decision JSON contains trailing content.");
                    }

                    return obj;
                }
            }
            catch (DecisionFormatException)
            {
                throw;
            }
            catch (JsonException exception)
            {
                throw new DecisionFormatException("Decision JSON is not a valid object.", exception);
            }
        }

        private static void RequireExactKeys(JObject obj, string[] expectedKeys)
        {
            var actual = obj.Properties().Select(property => property.Name).ToArray();
            if (actual.Length != expectedKeys.Length ||
                !new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expectedKeys))
            {
                throw new DecisionFormatException("Decision JSON has missing or extra fields.");
            }
        }

        private static TValue ReadMapped<TValue>(
            JObject obj,
            string propertyName,
            IReadOnlyDictionary<string, TValue> map)
        {
            var token = obj[propertyName];
            if (token == null || token.Type != JTokenType.String)
            {
                throw new DecisionFormatException("Decision JSON values must be strings.");
            }

            var value = token.Value<string>();
            if (value == null || !map.TryGetValue(value, out var mapped))
            {
                throw new DecisionFormatException("Decision JSON contains an unsupported enum value.");
            }

            return mapped;
        }
    }

    public static class DecisionJsonRepair
    {
        public static bool TryStripSingleMarkdownFence(string input, out string repaired)
        {
            repaired = string.Empty;
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            const string JsonFence = "```json\n";
            const string PlainFence = "```\n";
            const string ClosingFence = "\n```";

            string interior;
            if (input.StartsWith(JsonFence, StringComparison.Ordinal) &&
                input.EndsWith(ClosingFence, StringComparison.Ordinal))
            {
                interior = input.Substring(JsonFence.Length, input.Length - JsonFence.Length - ClosingFence.Length);
            }
            else if (input.StartsWith(PlainFence, StringComparison.Ordinal) &&
                     input.EndsWith(ClosingFence, StringComparison.Ordinal))
            {
                interior = input.Substring(PlainFence.Length, input.Length - PlainFence.Length - ClosingFence.Length);
            }
            else
            {
                return false;
            }

            if (interior.Contains("```"))
            {
                return false;
            }

            repaired = interior;
            return true;
        }
    }
}
