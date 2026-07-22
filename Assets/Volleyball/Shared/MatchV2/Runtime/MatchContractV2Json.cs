using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using Volleyball.Shared.Contracts;

namespace Volleyball.Shared.Contracts.V2
{
    public static class MatchContractV2Json
    {
        public static byte[] SerializeContext(MatchContextV2 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            return WriteContext(context, true);
        }

        public static MatchContextV2 DeserializeContext(byte[] bytes)
        {
            var root = Object(MatchV2StrictJsonReader.Parse(bytes), "context");
            root.Require("versions", "sessionId", "executionMode", "fixtureId", "fixtureVersion",
                "matchSeed", "competitionId", "scheduleItemId", "importanceBasisPoints", "format",
                "preMatchPriority", "teams", "contextHash");
            var versions = ReadVersions(root.Get("versions"));
            var session = CanonicalGuid(String(root.Get("sessionId"), "sessionId"), "sessionId");
            var mode = ReadExecutionMode(String(root.Get("executionMode"), "executionMode"));
            var fixtureId = NullableString(root.Get("fixtureId"), "fixtureId");
            var fixtureVersion = NullableInt32(root.Get("fixtureVersion"), "fixtureVersion");
            var seedValue = Integer(root.Get("matchSeed"), "matchSeed");
            if (seedValue < 0 || seedValue > uint.MaxValue) throw new MatchV2ContractException("matchSeed is outside uint32 range.");
            var format = ReadFormat(root.Get("format"));
            var teams = ReadTeams(root.Get("teams"));
            var parsedHash = String(root.Get("contextHash"), "contextHash");
            MatchV2Guard.Hash(parsedHash, "contextHash");
            var context = MatchContextV2.Create(
                versions, session, mode, fixtureId, fixtureVersion, (uint)seedValue,
                String(root.Get("competitionId"), "competitionId"),
                String(root.Get("scheduleItemId"), "scheduleItemId"),
                Int32(root.Get("importanceBasisPoints"), "importanceBasisPoints"), format,
                ReadPriority(String(root.Get("preMatchPriority"), "preMatchPriority")), teams);
            if (!MatchV2Hash.FixedTimeEquals(parsedHash, context.ContextHash))
                throw new MatchV2ContractException("contextHash does not match the context payload.");
            RequireCanonical(bytes, SerializeContext(context));
            return context;
        }

        public static byte[] SerializeResult(MatchResultV2 result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.ValidateLocal();
            return WriteResult(result, true);
        }

        public static MatchResultV2 DeserializeResult(byte[] bytes, MatchContextV2 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var root = Object(MatchV2StrictJsonReader.Parse(bytes), "result");
            root.Require("versions", "sessionId", "contextHash", "status", "winnerTeamId", "sets",
                "rallyCount", "playerFacts", "resultHash");
            var versions = ReadVersions(root.Get("versions"));
            if (!versions.Equals(context.Versions)) throw new MatchV2ContractException("Result versions do not match context.");
            var session = CanonicalGuid(String(root.Get("sessionId"), "sessionId"), "sessionId");
            if (session != context.SessionId) throw new MatchV2ContractException("Result session does not match context.");
            var contextHash = String(root.Get("contextHash"), "contextHash");
            MatchV2Guard.Hash(contextHash, "contextHash");
            if (!MatchV2Hash.FixedTimeEquals(contextHash, context.ContextHash))
                throw new MatchV2ContractException("Result contextHash does not match context.");
            var status = ReadStatus(String(root.Get("status"), "status"));
            var winner = NullableString(root.Get("winnerTeamId"), "winnerTeamId");
            var sets = ReadSets(root.Get("sets"));
            var facts = ReadPlayerFacts(root.Get("playerFacts"));
            var rallyCount = Int32(root.Get("rallyCount"), "rallyCount");
            MatchResultV2 result;
            if (status == MatchStatusV2.Completed)
            {
                if (winner == null) throw new MatchV2ContractException("Completed result requires winnerTeamId.");
                result = MatchResultV2.CreateCompleted(context, new TeamId(winner), sets, rallyCount, facts);
            }
            else
            {
                if (winner != null) throw new MatchV2ContractException("Abandoned result must have null winnerTeamId.");
                result = MatchResultV2.CreateAbandoned(context, sets, rallyCount, facts);
            }
            var parsedHash = String(root.Get("resultHash"), "resultHash");
            MatchV2Guard.Hash(parsedHash, "resultHash");
            if (!MatchV2Hash.FixedTimeEquals(parsedHash, result.ResultHash))
                throw new MatchV2ContractException("resultHash does not match the result payload.");
            RequireCanonical(bytes, SerializeResult(result));
            result.ValidateAgainst(context);
            return result;
        }

        internal static string ComputeContextHash(MatchContextV2 context) => MatchV2Hash.Sha256(WriteContext(context, false));
        internal static string ComputeResultHash(MatchResultV2 result) => MatchV2Hash.Sha256(WriteResult(result, false));

        private static byte[] WriteContext(MatchContextV2 c, bool includeHash)
        {
            var w = new MatchV2CanonicalJsonWriter(); w.StartObject();
            w.Property("versions"); WriteVersions(w, c.Versions);
            w.Property("sessionId"); w.String(c.SessionId.ToString("D").ToLowerInvariant());
            w.Property("executionMode"); w.String(ExecutionMode(c.ExecutionMode));
            w.Property("fixtureId"); w.NullableString(c.FixtureId);
            w.Property("fixtureVersion"); w.NullableInteger(c.FixtureVersion);
            w.Property("matchSeed"); w.Integer(c.MatchSeed);
            w.Property("competitionId"); w.String(c.CompetitionId);
            w.Property("scheduleItemId"); w.String(c.ScheduleItemId);
            w.Property("importanceBasisPoints"); w.Integer(c.ImportanceBasisPoints);
            w.Property("format"); WriteFormat(w, c.Format);
            w.Property("preMatchPriority"); w.String(Priority(c.PreMatchPriority));
            w.Property("teams"); w.StartArray(); foreach (var team in c.Teams) WriteTeam(w, team); w.EndArray();
            if (includeHash) { w.Property("contextHash"); w.String(c.ContextHash); }
            w.EndObject(); return w.Bytes();
        }

        private static byte[] WriteResult(MatchResultV2 r, bool includeHash)
        {
            var w = new MatchV2CanonicalJsonWriter(); w.StartObject();
            w.Property("versions"); WriteVersions(w, r.Versions);
            w.Property("sessionId"); w.String(r.SessionId.ToString("D").ToLowerInvariant());
            w.Property("contextHash"); w.String(r.ContextHash);
            w.Property("status"); w.String(Status(r.Status));
            w.Property("winnerTeamId"); if (r.WinnerTeamId.HasValue) w.String(r.WinnerTeamId.Value.Value); else w.Null();
            w.Property("sets"); w.StartArray(); foreach (var set in r.Sets) WriteSet(w, set); w.EndArray();
            w.Property("rallyCount"); w.Integer(r.RallyCount);
            w.Property("playerFacts"); w.StartArray(); foreach (var fact in r.PlayerFacts) WriteFacts(w, fact); w.EndArray();
            if (includeHash) { w.Property("resultHash"); w.String(r.ResultHash); }
            w.EndObject(); return w.Bytes();
        }

        private static void WriteVersions(MatchV2CanonicalJsonWriter w, MatchVersionSetV2 v)
        {
            w.StartObject();
            w.Property("contractVersion"); w.Integer(v.ContractVersion);
            w.Property("contentVersion"); w.Integer(v.ContentVersion);
            w.Property("rulesetVersion"); w.Integer(v.RulesetVersion);
            w.Property("careerRandomAlgorithmVersion"); w.Integer(v.CareerRandomAlgorithmVersion);
            w.Property("matchSimulationVersion"); w.NullableInteger(v.MatchSimulationVersion);
            w.Property("matchRandomAlgorithmVersion"); w.NullableInteger(v.MatchRandomAlgorithmVersion);
            w.EndObject();
        }

        private static void WriteFormat(MatchV2CanonicalJsonWriter w, MatchFormatV2 f)
        {
            w.StartObject();
            w.Property("kind"); w.String(f.Kind);
            w.Property("teamSize"); w.Integer(f.TeamSize);
            w.Property("setsToWin"); w.Integer(f.SetsToWin);
            w.Property("setTargetPoints"); w.Integer(f.SetTargetPoints);
            w.Property("minimumLeadPoints"); w.Integer(f.MinimumLeadPoints);
            w.EndObject();
        }

        private static void WriteTeam(MatchV2CanonicalJsonWriter w, MatchTeamSnapshotV2 team)
        {
            w.StartObject();
            w.Property("teamId"); w.String(team.TeamId.Value);
            w.Property("side"); w.String(Side(team.Side));
            w.Property("players"); w.StartArray(); foreach (var player in team.Players) WritePlayer(w, player); w.EndArray();
            w.EndObject();
        }

        private static void WritePlayer(MatchV2CanonicalJsonWriter w, MatchPlayerSnapshotV2 p)
        {
            w.StartObject();
            w.Property("playerId"); w.String(p.PlayerId.Value);
            w.Property("jerseyNumber"); w.Integer(p.JerseyNumber);
            w.Property("position"); w.String(Position(p.Position));
            w.Property("rotationSlot"); w.Integer(p.RotationSlot);
            w.Property("fitnessBasisPoints"); w.Integer(p.FitnessBasisPoints);
            w.Property("abilities"); w.StartObject();
            w.Property("spikeBasisPoints"); w.Integer(p.Abilities.SpikeBasisPoints);
            w.Property("serveBasisPoints"); w.Integer(p.Abilities.ServeBasisPoints);
            w.Property("receptionBasisPoints"); w.Integer(p.Abilities.ReceptionBasisPoints);
            w.Property("defenseBasisPoints"); w.Integer(p.Abilities.DefenseBasisPoints);
            w.Property("blockBasisPoints"); w.Integer(p.Abilities.BlockBasisPoints);
            w.Property("movementBasisPoints"); w.Integer(p.Abilities.MovementBasisPoints);
            w.Property("jumpBasisPoints"); w.Integer(p.Abilities.JumpBasisPoints);
            w.Property("staminaBasisPoints"); w.Integer(p.Abilities.StaminaBasisPoints);
            w.EndObject(); w.EndObject();
        }

        private static void WriteSet(MatchV2CanonicalJsonWriter w, SetScoreV2 s)
        {
            w.StartObject(); w.Property("setNumber"); w.Integer(s.SetNumber);
            w.Property("homePoints"); w.Integer(s.HomePoints); w.Property("awayPoints"); w.Integer(s.AwayPoints);
            w.Property("isComplete"); w.Boolean(s.IsComplete); w.EndObject();
        }

        private static void WriteFacts(MatchV2CanonicalJsonWriter w, MatchPlayerFactsV2 f)
        {
            w.StartObject(); w.Property("playerId"); w.String(f.PlayerId.Value);
            w.Property("spike"); w.StartObject(); w.Property("attempts"); w.Integer(f.Spike.Attempts); w.Property("points"); w.Integer(f.Spike.Points); w.Property("errors"); w.Integer(f.Spike.Errors); w.EndObject();
            w.Property("serve"); w.StartObject(); w.Property("attempts"); w.Integer(f.Serve.Attempts); w.Property("aces"); w.Integer(f.Serve.Aces); w.Property("errors"); w.Integer(f.Serve.Errors); w.EndObject();
            w.Property("reception"); w.StartObject(); w.Property("attempts"); w.Integer(f.Reception.Attempts); w.Property("perfect"); w.Integer(f.Reception.Perfect); w.Property("positive"); w.Integer(f.Reception.Positive); w.Property("neutral"); w.Integer(f.Reception.Neutral); w.Property("negative"); w.Integer(f.Reception.Negative); w.Property("errors"); w.Integer(f.Reception.Errors); w.EndObject();
            w.Property("defense"); w.StartObject(); w.Property("attempts"); w.Integer(f.Defense.Attempts); w.Property("successes"); w.Integer(f.Defense.Successes); w.EndObject();
            w.Property("block"); w.StartObject(); w.Property("attempts"); w.Integer(f.Block.Attempts); w.Property("effectiveTouches"); w.Integer(f.Block.EffectiveTouches); w.Property("points"); w.Integer(f.Block.Points); w.EndObject();
            w.Property("load"); w.StartObject(); w.Property("ralliesPlayed"); w.Integer(f.Load.RalliesPlayed); w.Property("activeDurationMilliseconds"); w.Integer(f.Load.ActiveDurationMilliseconds); w.Property("movementDistanceMillimeters"); w.Integer(f.Load.MovementDistanceMillimeters); w.Property("jumpCount"); w.Integer(f.Load.JumpCount); w.Property("highLoadJumpCount"); w.Integer(f.Load.HighLoadJumpCount); w.Property("landingLoadBasisPoints"); w.Integer(f.Load.LandingLoadBasisPoints); w.Property("totalWorkloadBasisPoints"); w.Integer(f.Load.TotalWorkloadBasisPoints); w.EndObject();
            w.Property("stability"); w.StartObject(); w.Property("criticalActions"); w.Integer(f.Stability.CriticalActions); w.Property("criticalSuccesses"); w.Integer(f.Stability.CriticalSuccesses); w.Property("criticalErrors"); w.Integer(f.Stability.CriticalErrors); w.Property("errorStreakEpisodes"); w.Integer(f.Stability.ErrorStreakEpisodes); w.Property("longestErrorStreak"); w.Integer(f.Stability.LongestErrorStreak); w.EndObject();
            w.EndObject();
        }

        private static MatchVersionSetV2 ReadVersions(MatchV2JsonValue value)
        {
            var o = Object(value, "versions"); o.Require("contractVersion", "contentVersion", "rulesetVersion", "careerRandomAlgorithmVersion", "matchSimulationVersion", "matchRandomAlgorithmVersion");
            return new MatchVersionSetV2(Int32(o.Get("contractVersion"), "contractVersion"), Int32(o.Get("contentVersion"), "contentVersion"), Int32(o.Get("rulesetVersion"), "rulesetVersion"), Int32(o.Get("careerRandomAlgorithmVersion"), "careerRandomAlgorithmVersion"), NullableInt32(o.Get("matchSimulationVersion"), "matchSimulationVersion"), NullableInt32(o.Get("matchRandomAlgorithmVersion"), "matchRandomAlgorithmVersion"));
        }

        private static MatchFormatV2 ReadFormat(MatchV2JsonValue value)
        {
            var o = Object(value, "format"); o.Require("kind", "teamSize", "setsToWin", "setTargetPoints", "minimumLeadPoints");
            return new MatchFormatV2(String(o.Get("kind"), "kind"), Int32(o.Get("teamSize"), "teamSize"), Int32(o.Get("setsToWin"), "setsToWin"), Int32(o.Get("setTargetPoints"), "setTargetPoints"), Int32(o.Get("minimumLeadPoints"), "minimumLeadPoints"));
        }

        private static MatchTeamSnapshotV2[] ReadTeams(MatchV2JsonValue value)
        {
            var a = Array(value, "teams"); var teams = new MatchTeamSnapshotV2[a.Count];
            for (var i = 0; i < a.Count; i++)
            {
                var o = Object(a[i], "team"); o.Require("teamId", "side", "players");
                teams[i] = new MatchTeamSnapshotV2(new TeamId(String(o.Get("teamId"), "teamId")), ReadSide(String(o.Get("side"), "side")), ReadPlayers(o.Get("players")));
            }
            return teams;
        }

        private static MatchPlayerSnapshotV2[] ReadPlayers(MatchV2JsonValue value)
        {
            var a = Array(value, "players"); var players = new MatchPlayerSnapshotV2[a.Count];
            for (var i = 0; i < a.Count; i++)
            {
                var o = Object(a[i], "player"); o.Require("playerId", "jerseyNumber", "position", "rotationSlot", "fitnessBasisPoints", "abilities");
                var b = Object(o.Get("abilities"), "abilities"); b.Require("spikeBasisPoints", "serveBasisPoints", "receptionBasisPoints", "defenseBasisPoints", "blockBasisPoints", "movementBasisPoints", "jumpBasisPoints", "staminaBasisPoints");
                var abilities = new MatchAbilitySnapshotV2(Int32(b.Get("spikeBasisPoints"), "spikeBasisPoints"), Int32(b.Get("serveBasisPoints"), "serveBasisPoints"), Int32(b.Get("receptionBasisPoints"), "receptionBasisPoints"), Int32(b.Get("defenseBasisPoints"), "defenseBasisPoints"), Int32(b.Get("blockBasisPoints"), "blockBasisPoints"), Int32(b.Get("movementBasisPoints"), "movementBasisPoints"), Int32(b.Get("jumpBasisPoints"), "jumpBasisPoints"), Int32(b.Get("staminaBasisPoints"), "staminaBasisPoints"));
                players[i] = new MatchPlayerSnapshotV2(new PlayerId(String(o.Get("playerId"), "playerId")), Int32(o.Get("jerseyNumber"), "jerseyNumber"), ReadPosition(String(o.Get("position"), "position")), Int32(o.Get("rotationSlot"), "rotationSlot"), Int32(o.Get("fitnessBasisPoints"), "fitnessBasisPoints"), abilities);
            }
            return players;
        }

        private static SetScoreV2[] ReadSets(MatchV2JsonValue value)
        {
            var a = Array(value, "sets"); var sets = new SetScoreV2[a.Count];
            for (var i = 0; i < a.Count; i++) { var o = Object(a[i], "set"); o.Require("setNumber", "homePoints", "awayPoints", "isComplete"); sets[i] = new SetScoreV2(Int32(o.Get("setNumber"), "setNumber"), Int32(o.Get("homePoints"), "homePoints"), Int32(o.Get("awayPoints"), "awayPoints"), Boolean(o.Get("isComplete"), "isComplete")); }
            return sets;
        }

        private static MatchPlayerFactsV2[] ReadPlayerFacts(MatchV2JsonValue value)
        {
            var a = Array(value, "playerFacts"); var facts = new MatchPlayerFactsV2[a.Count];
            for (var i = 0; i < a.Count; i++)
            {
                var o = Object(a[i], "playerFact"); o.Require("playerId", "spike", "serve", "reception", "defense", "block", "load", "stability");
                var spike = Object(o.Get("spike"), "spike"); spike.Require("attempts", "points", "errors");
                var serve = Object(o.Get("serve"), "serve"); serve.Require("attempts", "aces", "errors");
                var reception = Object(o.Get("reception"), "reception"); reception.Require("attempts", "perfect", "positive", "neutral", "negative", "errors");
                var defense = Object(o.Get("defense"), "defense"); defense.Require("attempts", "successes");
                var block = Object(o.Get("block"), "block"); block.Require("attempts", "effectiveTouches", "points");
                var load = Object(o.Get("load"), "load"); load.Require("ralliesPlayed", "activeDurationMilliseconds", "movementDistanceMillimeters", "jumpCount", "highLoadJumpCount", "landingLoadBasisPoints", "totalWorkloadBasisPoints");
                var stability = Object(o.Get("stability"), "stability"); stability.Require("criticalActions", "criticalSuccesses", "criticalErrors", "errorStreakEpisodes", "longestErrorStreak");
                facts[i] = new MatchPlayerFactsV2(new PlayerId(String(o.Get("playerId"), "playerId")),
                    new SpikeFactsV2(Int32(spike.Get("attempts"), "attempts"), Int32(spike.Get("points"), "points"), Int32(spike.Get("errors"), "errors")),
                    new ServeFactsV2(Int32(serve.Get("attempts"), "attempts"), Int32(serve.Get("aces"), "aces"), Int32(serve.Get("errors"), "errors")),
                    new ReceptionFactsV2(Int32(reception.Get("attempts"), "attempts"), Int32(reception.Get("perfect"), "perfect"), Int32(reception.Get("positive"), "positive"), Int32(reception.Get("neutral"), "neutral"), Int32(reception.Get("negative"), "negative"), Int32(reception.Get("errors"), "errors")),
                    new DefenseFactsV2(Int32(defense.Get("attempts"), "attempts"), Int32(defense.Get("successes"), "successes")),
                    new BlockFactsV2(Int32(block.Get("attempts"), "attempts"), Int32(block.Get("effectiveTouches"), "effectiveTouches"), Int32(block.Get("points"), "points")),
                    new MatchLoadFactsV2(Int32(load.Get("ralliesPlayed"), "ralliesPlayed"), Integer(load.Get("activeDurationMilliseconds"), "activeDurationMilliseconds"), Integer(load.Get("movementDistanceMillimeters"), "movementDistanceMillimeters"), Int32(load.Get("jumpCount"), "jumpCount"), Int32(load.Get("highLoadJumpCount"), "highLoadJumpCount"), Int32(load.Get("landingLoadBasisPoints"), "landingLoadBasisPoints"), Int32(load.Get("totalWorkloadBasisPoints"), "totalWorkloadBasisPoints")),
                    new StabilityFactsV2(Int32(stability.Get("criticalActions"), "criticalActions"), Int32(stability.Get("criticalSuccesses"), "criticalSuccesses"), Int32(stability.Get("criticalErrors"), "criticalErrors"), Int32(stability.Get("errorStreakEpisodes"), "errorStreakEpisodes"), Int32(stability.Get("longestErrorStreak"), "longestErrorStreak")));
            }
            return facts;
        }

        private static MatchV2JsonObject Object(MatchV2JsonValue v, string path) { if (v == null || v.Kind != MatchV2JsonKind.Object) throw new MatchV2ContractException(path + " must be an object."); return v.ObjectValue; }
        private static IReadOnlyList<MatchV2JsonValue> Array(MatchV2JsonValue v, string path) { if (v == null || v.Kind != MatchV2JsonKind.Array) throw new MatchV2ContractException(path + " must be an array."); return v.ArrayValue; }
        private static string String(MatchV2JsonValue v, string path) { if (v == null || v.Kind != MatchV2JsonKind.String) throw new MatchV2ContractException(path + " must be a string."); return v.StringValue; }
        private static string NullableString(MatchV2JsonValue v, string path) { if (v.Kind == MatchV2JsonKind.Null) return null; return String(v, path); }
        private static long Integer(MatchV2JsonValue v, string path) { if (v == null || v.Kind != MatchV2JsonKind.Integer) throw new MatchV2ContractException(path + " must be an integer."); return v.IntegerValue; }
        private static int Int32(MatchV2JsonValue v, string path) { var n = Integer(v, path); if (n < int.MinValue || n > int.MaxValue) throw new MatchV2ContractException(path + " is outside Int32 range."); return (int)n; }
        private static int? NullableInt32(MatchV2JsonValue v, string path) { return v.Kind == MatchV2JsonKind.Null ? null : Int32(v, path); }
        private static bool Boolean(MatchV2JsonValue v, string path) { if (v == null || v.Kind != MatchV2JsonKind.Boolean) throw new MatchV2ContractException(path + " must be boolean."); return v.BooleanValue; }
        private static Guid CanonicalGuid(string value, string path) { if (!Guid.TryParseExact(value, "D", out var guid) || guid == Guid.Empty || value != guid.ToString("D").ToLowerInvariant()) throw new MatchV2ContractException(path + " must be a non-empty lowercase canonical UUID."); return guid; }
        private static void RequireCanonical(byte[] actual, byte[] expected) { if (!MatchV2Hash.FixedTimeEquals(actual, expected)) throw new MatchV2ContractException("Document bytes are not canonical."); }

        internal static string ExecutionMode(MatchExecutionModeV2 v) => v == MatchExecutionModeV2.Fixture ? "fixture" : v == MatchExecutionModeV2.Direct ? "direct" : v == MatchExecutionModeV2.QuickSimulation ? "quick_simulation" : throw new MatchV2ContractException("Unsupported executionMode.");
        internal static string Priority(PreMatchPriorityV2 v) => v == PreMatchPriorityV2.AttackFirst ? "attack_first" : v == PreMatchPriorityV2.FirstContactSecurity ? "first_contact_security" : v == PreMatchPriorityV2.StaminaControl ? "stamina_control" : throw new MatchV2ContractException("Unsupported priority.");
        internal static string Side(TeamSideV2 v) => v == TeamSideV2.Home ? "home" : v == TeamSideV2.Away ? "away" : throw new MatchV2ContractException("Unsupported side.");
        internal static string Position(PlayerPositionV2 v) => v == PlayerPositionV2.Setter ? "setter" : v == PlayerPositionV2.OutsideHitter ? "outside_hitter" : v == PlayerPositionV2.MiddleBlocker ? "middle_blocker" : v == PlayerPositionV2.Opposite ? "opposite" : v == PlayerPositionV2.Libero ? "libero" : throw new MatchV2ContractException("Unsupported position.");
        internal static string Status(MatchStatusV2 v) => v == MatchStatusV2.Completed ? "completed" : v == MatchStatusV2.Abandoned ? "abandoned" : throw new MatchV2ContractException("Unsupported status.");
        private static MatchExecutionModeV2 ReadExecutionMode(string v) => v == "fixture" ? MatchExecutionModeV2.Fixture : v == "direct" ? MatchExecutionModeV2.Direct : v == "quick_simulation" ? MatchExecutionModeV2.QuickSimulation : throw new MatchV2ContractException("Unsupported executionMode.");
        private static PreMatchPriorityV2 ReadPriority(string v) => v == "attack_first" ? PreMatchPriorityV2.AttackFirst : v == "first_contact_security" ? PreMatchPriorityV2.FirstContactSecurity : v == "stamina_control" ? PreMatchPriorityV2.StaminaControl : throw new MatchV2ContractException("Unsupported priority.");
        private static TeamSideV2 ReadSide(string v) => v == "home" ? TeamSideV2.Home : v == "away" ? TeamSideV2.Away : throw new MatchV2ContractException("Unsupported side.");
        private static PlayerPositionV2 ReadPosition(string v) => v == "setter" ? PlayerPositionV2.Setter : v == "outside_hitter" ? PlayerPositionV2.OutsideHitter : v == "middle_blocker" ? PlayerPositionV2.MiddleBlocker : v == "opposite" ? PlayerPositionV2.Opposite : v == "libero" ? PlayerPositionV2.Libero : throw new MatchV2ContractException("Unsupported position.");
        private static MatchStatusV2 ReadStatus(string v) => v == "completed" ? MatchStatusV2.Completed : v == "abandoned" ? MatchStatusV2.Abandoned : throw new MatchV2ContractException("Unsupported status.");
    }

    internal static class MatchV2Hash
    {
        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create(); var hash = sha.ComputeHash(bytes); var chars = new char[hash.Length * 2];
            const string digits = "0123456789abcdef"; for (var i = 0; i < hash.Length; i++) { chars[i * 2] = digits[hash[i] >> 4]; chars[i * 2 + 1] = digits[hash[i] & 15]; }
            return new string(chars);
        }
        public static bool FixedTimeEquals(string left, string right) => left != null && right != null && FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(left), System.Text.Encoding.ASCII.GetBytes(right));
        public static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }
}
