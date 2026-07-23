using System;
using System.Collections.Generic;
using Volleyball.Shared.Contracts;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public static class OnCourtLineupRulesV3
    {
        public static OnCourtEligibilitySnapshot Create(
            MatchContextV3 context,
            IReadOnlyList<PlayerId> homeRotationOrder,
            IReadOnlyList<PlayerId> awayRotationOrder,
            PlayerId homeServer,
            PlayerId awayServer,
            IReadOnlyList<LiberoReplacementV3> liberoReplacements)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (liberoReplacements == null)
            {
                throw new ArgumentNullException(nameof(liberoReplacements));
            }

            var replacementsByLibero = ValidateReplacements(context, liberoReplacements);
            var players = new List<OnCourtPlayerEligibilityV3>(12);
            AddSidePlayers(
                context.Home, TeamSide.Home, homeRotationOrder, homeServer, replacementsByLibero, players,
                nameof(homeRotationOrder), nameof(homeServer));
            AddSidePlayers(
                context.Away, TeamSide.Away, awayRotationOrder, awayServer, replacementsByLibero, players,
                nameof(awayRotationOrder), nameof(awayServer));
            return new OnCourtEligibilitySnapshot(players);
        }

        private static Dictionary<PlayerId, PlayerId> ValidateReplacements(
            MatchContextV3 context,
            IReadOnlyList<LiberoReplacementV3> replacements)
        {
            var playersById = BuildPlayerLookup(context.Home, context.Away);
            var replacementsByLibero = new Dictionary<PlayerId, PlayerId>();
            var replacedPlayerIds = new HashSet<PlayerId>();
            for (var index = 0; index < replacements.Count; index++)
            {
                var replacement = replacements[index];
                if (replacement == null)
                {
                    throw new ArgumentException("Libero replacements cannot contain null.", nameof(replacements));
                }

                if (!playersById.TryGetValue(replacement.LiberoPlayerId, out var libero) ||
                    !playersById.TryGetValue(replacement.ReplacedPlayerId, out var replacedPlayer))
                {
                    throw new ArgumentException("Libero replacements must reference players in the match context.", nameof(replacements));
                }

                if (libero.Position != PlayerPosition.Libero)
                {
                    throw new ArgumentException("Only a registered libero can make a libero replacement.", nameof(replacements));
                }

                if (replacedPlayer.Position == PlayerPosition.Libero ||
                    SideFor(context, replacement.LiberoPlayerId) != SideFor(context, replacement.ReplacedPlayerId))
                {
                    throw new ArgumentException("A libero replacement must replace a non-libero teammate.", nameof(replacements));
                }

                if (!replacementsByLibero.TryAdd(replacement.LiberoPlayerId, replacement.ReplacedPlayerId) ||
                    !replacedPlayerIds.Add(replacement.ReplacedPlayerId))
                {
                    throw new ArgumentException("Each libero and replaced player can appear in only one replacement.", nameof(replacements));
                }
            }

            return replacementsByLibero;
        }

        private static void AddSidePlayers(
            TeamSnapshotV3 team,
            TeamSide side,
            IReadOnlyList<PlayerId> rotationOrder,
            PlayerId server,
            IReadOnlyDictionary<PlayerId, PlayerId> replacementsByLibero,
            ICollection<OnCourtPlayerEligibilityV3> players,
            string rotationParamName,
            string serverParamName)
        {
            if (rotationOrder == null)
            {
                throw new ArgumentNullException(rotationParamName);
            }

            if (rotationOrder.Count != 6)
            {
                throw new ArgumentException("Exactly six rotation IDs are required.", rotationParamName);
            }

            PlayerWorldSnapshotV3.RequirePlayerId(server, serverParamName);
            var rosterById = BuildPlayerLookup(team);
            var rotationIds = new HashSet<PlayerId>();
            for (var index = 0; index < rotationOrder.Count; index++)
            {
                var playerId = PlayerWorldSnapshotV3.RequirePlayerId(rotationOrder[index], rotationParamName);
                if (!rotationIds.Add(playerId))
                {
                    throw new ArgumentException("Rotation IDs must be distinct.", rotationParamName);
                }

                if (!rosterById.TryGetValue(playerId, out var player))
                {
                    throw new ArgumentException("Rotation IDs must belong to the matching team.", rotationParamName);
                }

                if (replacementsByLibero.TryGetValue(playerId, out var replacedPlayerId) && rotationIds.Contains(replacedPlayerId))
                {
                    throw new ArgumentException("A libero and the player it replaces cannot both be on court.", rotationParamName);
                }

                players.Add(new OnCourtPlayerEligibilityV3(
                    playerId, side, player.Position, index + 1, playerId.Equals(server),
                    replacementsByLibero.TryGetValue(playerId, out replacedPlayerId) ? replacedPlayerId : (PlayerId?)null));
            }

            foreach (var replacement in replacementsByLibero)
            {
                if (rotationIds.Contains(replacement.Key) && rotationIds.Contains(replacement.Value))
                {
                    throw new ArgumentException("A libero and the player it replaces cannot both be on court.", rotationParamName);
                }
            }

            if (!rotationIds.Contains(server))
            {
                throw new ArgumentException("The current server must be in its supplied rotation.", serverParamName);
            }
        }

        private static Dictionary<PlayerId, PlayerSnapshotV3> BuildPlayerLookup(TeamSnapshotV3 team)
        {
            var playersById = new Dictionary<PlayerId, PlayerSnapshotV3>();
            for (var index = 0; index < team.Players.Count; index++)
            {
                var player = team.Players[index];
                playersById.Add(player.PlayerId, player);
            }

            return playersById;
        }

        private static Dictionary<PlayerId, PlayerSnapshotV3> BuildPlayerLookup(TeamSnapshotV3 home, TeamSnapshotV3 away)
        {
            var playersById = BuildPlayerLookup(home);
            for (var index = 0; index < away.Players.Count; index++)
            {
                var player = away.Players[index];
                playersById.Add(player.PlayerId, player);
            }

            return playersById;
        }

        private static TeamSide SideFor(MatchContextV3 context, PlayerId playerId)
        {
            return BuildPlayerLookup(context.Home).ContainsKey(playerId) ? TeamSide.Home : TeamSide.Away;
        }
    }
}
