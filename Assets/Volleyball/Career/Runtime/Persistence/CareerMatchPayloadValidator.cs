using System;
using System.Collections.Generic;
using System.Text;
using Volleyball.Career.Domain;
using Volleyball.Shared.Contracts;

namespace Volleyball.Career.Persistence
{
    internal static class CareerMatchPayloadValidator
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static void Validate(CareerSaveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            try
            {
                if (snapshot.PendingMatch != null)
                {
                    ValidatePending(snapshot, snapshot.PendingMatch);
                }

                for (var index = 0; index < snapshot.MatchHistory.Count; index++)
                {
                    ValidateHistory(snapshot, snapshot.MatchHistory[index], index);
                }
            }
            catch (Exception exception) when (
                exception is ContractValidationException ||
                exception is DecoderFallbackException)
            {
                throw new ArgumentException(
                    "Career match payload failed the authoritative Shared Match V3 contract.",
                    nameof(snapshot),
                    exception);
            }
        }

        private static void ValidatePending(
            CareerSaveSnapshot snapshot,
            PendingCareerMatch pending)
        {
            var context = DecodeContext(
                pending.CanonicalContextUtf8,
                "pendingMatch.canonicalContextUtf8");
            RequireEqual(
                pending.Versions.ContractVersion,
                CareerMatchLifecycleVersions.ContractV3,
                "pendingMatch.versions.contractVersion");
            RequireEqual(context.ContractVersion, pending.Versions.ContractVersion, "pendingMatch.contractVersion");
            RequireEqual(context.ContextHash, pending.ContextDigest.Value, "pendingMatch.contextHash");
            RequireEqual(context.SessionId, pending.SessionId, "pendingMatch.sessionId");
            RequireEqual(
                context.Seed,
                unchecked((int)pending.MatchSeed),
                "pendingMatch.matchSeed");
            RequireEqual(context.Home.TeamId.Value, pending.HomeTeamId.Value, "pendingMatch.homeTeamId");
            RequireEqual(context.Away.TeamId.Value, pending.AwayTeamId.Value, "pendingMatch.awayTeamId");

            var players = FlattenPlayers(context);
            RequireEqual(players.Count, pending.OrderedPlayerIds.Count, "pendingMatch.orderedPlayerIds.count");
            for (var index = 0; index < players.Count; index++)
            {
                RequireEqual(
                    players[index].PlayerId.Value,
                    pending.OrderedPlayerIds[index].Value,
                    "pendingMatch.orderedPlayerIds[" + index + "]");
            }

            RequireCurrentPlayer(context, snapshot, "pendingMatch");
        }

        private static void ValidateHistory(
            CareerSaveSnapshot snapshot,
            CareerMatchHistoryEntry history,
            int index)
        {
            var path = "matchHistory[" + index + "]";
            var context = DecodeContext(history.CanonicalContextUtf8, path + ".canonicalContextUtf8");
            var result = DecodeResult(
                history.CanonicalResultUtf8,
                context,
                path + ".canonicalResultUtf8");

            RequireEqual(context.SessionId, history.SessionId, path + ".sessionId");
            RequireEqual(context.ContextHash, history.ContextDigest.Value, path + ".contextHash");
            RequireEqual(result.SessionId, history.SessionId, path + ".result.sessionId");
            RequireEqual(result.ContextHash, history.ContextDigest.Value, path + ".result.contextHash");
            RequireEqual(result.ResultHash, history.ResultDigest.Value, path + ".resultHash");
            RequireEqual(history.SettlementSummary.Sets.Count, 1, path + ".settlementSummary.sets.count");
            RequireEqual(
                history.SettlementSummary.Sets[0].HomePoints,
                result.HomeScore,
                path + ".settlementSummary.sets[0].homePoints");
            RequireEqual(
                history.SettlementSummary.Sets[0].AwayPoints,
                result.AwayScore,
                path + ".settlementSummary.sets[0].awayPoints");
            RequireEqual(
                result.WinnerTeamId.Value == snapshot.TeamId.Value.Value,
                history.SettlementSummary.Won,
                path + ".settlementSummary.won");

            var hasProtagonistStats = false;
            foreach (var stats in result.PlayerStats)
            {
                if (stats.PlayerId.Equals(snapshot.Player.PlayerId))
                {
                    hasProtagonistStats = true;
                    break;
                }
            }

            if (!hasProtagonistStats)
            {
                throw Contradiction(path + " result omits the current Career player's V3 stats.");
            }

            RequireCurrentPlayer(context, snapshot, path);
        }

        private static MatchContextV3 DecodeContext(byte[] bytes, string path)
        {
            var context = ContractJson.DeserializeContextV3(StrictUtf8.GetString(bytes));
            RequireSameBytes(bytes, Serialize(context), path);
            return context;
        }

        private static MatchResultV3 DecodeResult(
            byte[] bytes,
            MatchContextV3 context,
            string path)
        {
            var result = ContractJson.DeserializeResultV3(StrictUtf8.GetString(bytes));
            RequireSameBytes(bytes, Serialize(result), path);
            result.ValidateAgainst(context);
            return result;
        }

        private static byte[] Serialize(MatchContextV3 context)
        {
            return StrictUtf8.GetBytes(ContractJson.SerializeV3(context));
        }

        private static byte[] Serialize(MatchResultV3 result)
        {
            return StrictUtf8.GetBytes(ContractJson.SerializeV3(result));
        }

        private static List<PlayerSnapshotV3> FlattenPlayers(MatchContextV3 context)
        {
            var players = new List<PlayerSnapshotV3>(
                context.Home.Players.Count + context.Away.Players.Count);
            players.AddRange(context.Home.Players);
            players.AddRange(context.Away.Players);
            return players;
        }

        private static void RequireCurrentPlayer(
            MatchContextV3 context,
            CareerSaveSnapshot snapshot,
            string path)
        {
            if (snapshot.Player == null || !snapshot.TeamId.HasValue)
            {
                throw Contradiction(path + " requires the current Career player and team.");
            }

            TeamSnapshotV3 careerTeam = null;
            if (context.Home.TeamId.Equals(snapshot.TeamId.Value))
            {
                careerTeam = context.Home;
            }
            else if (context.Away.TeamId.Equals(snapshot.TeamId.Value))
            {
                careerTeam = context.Away;
            }

            if (careerTeam == null)
            {
                throw Contradiction(path + " current Career team is absent from context.");
            }

            PlayerSnapshotV3 protagonist = null;
            foreach (var player in careerTeam.Players)
            {
                if (player.PlayerId.Equals(snapshot.Player.PlayerId))
                {
                    protagonist = player;
                    break;
                }
            }

            if (protagonist == null)
            {
                throw Contradiction(path + " current Career player is absent from its team segment.");
            }

            RequireEqual(
                protagonist.JerseyNumber,
                snapshot.Player.JerseyNumber,
                path + ".protagonist.jerseyNumber");
        }

        private static void RequireSameBytes(byte[] actual, byte[] expected, string path)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
            {
                throw Contradiction(path + " does not round-trip byte-for-byte.");
            }

            for (var index = 0; index < actual.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    throw Contradiction(path + " does not round-trip byte-for-byte.");
                }
            }
        }

        private static void RequireEqual<T>(T actual, T expected, string path)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                throw Contradiction(path + " contradicts the canonical Shared Match V3 payload.");
            }
        }

        private static ArgumentException Contradiction(string message)
        {
            return new ArgumentException(message, "snapshot");
        }
    }
}
