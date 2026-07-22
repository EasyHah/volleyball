using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class MatchContextV2 : IMatchContext
    {
        [DataMember(Name = "contractVersion", Order = 1)] private int _contractVersion;
        [DataMember(Name = "sessionId", Order = 2)] private string _sessionId;
        [DataMember(Name = "seed", Order = 3)] private int _seed;
        [DataMember(Name = "home", Order = 4)] private TeamSnapshotV2 _home;
        [DataMember(Name = "away", Order = 5)] private TeamSnapshotV2 _away;
        [DataMember(Name = "contextHash", Order = 6)] private string _contextHash;

        private MatchContextV2(Guid sessionId, int seed, TeamSnapshotV2 home, TeamSnapshotV2 away)
        {
            _contractVersion = ContractVersions.MatchV2;
            _sessionId = sessionId.ToString("D");
            _seed = seed;
            _home = home ?? throw new ArgumentNullException(nameof(home));
            _away = away ?? throw new ArgumentNullException(nameof(away));
            _contextHash = string.Empty;
            ValidatePayload();
            _contextHash = MatchContextHashV2.Compute(this);
            Validate();
        }

        public int ContractVersion => _contractVersion;
        public Guid SessionId => Guid.ParseExact(_sessionId, "D");
        public int Seed => _seed;
        public TeamSnapshotV2 Home => _home;
        public TeamSnapshotV2 Away => _away;
        public string ContextHash => _contextHash;

        public static MatchContextV2 Create(Guid sessionId, int seed, TeamSnapshotV2 home, TeamSnapshotV2 away)
        {
            if (sessionId == Guid.Empty) throw new ContractValidationException("sessionId cannot be empty.");
            return new MatchContextV2(sessionId, seed, home, away);
        }

        public static MatchContextV2 UpgradeFromV1(MatchContextV1 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            return Create(context.SessionId, context.Seed, UpgradeTeam(context.Home), UpgradeTeam(context.Away));
        }

        internal void Validate()
        {
            ValidatePayload();
            ContractGuard.Hash(_contextHash, nameof(ContextHash));
            if (!string.Equals(MatchContextHashV2.Compute(this), _contextHash, StringComparison.Ordinal))
                throw new ContractValidationException("contextHash does not match the context payload.");
        }

        private void ValidatePayload()
        {
            if (_contractVersion != ContractVersions.MatchV2)
                throw new ContractValidationException("Unsupported match contract version: " + _contractVersion + ".");
            if (!Guid.TryParseExact(_sessionId, "D", out var sessionId) || sessionId == Guid.Empty)
                throw new ContractValidationException("sessionId must be a non-empty canonical GUID.");
            if (_home == null || _away == null) throw new ContractValidationException("Both home and away snapshots are required.");
            _home.Validate();
            _away.Validate();
            if (_home.Side != TeamSide.Home || _away.Side != TeamSide.Away)
                throw new ContractValidationException("Team snapshots must use their matching home and away sides.");
            if (_home.TeamId.Equals(_away.TeamId)) throw new ContractValidationException("Home and away teams must have different IDs.");
            var playerIds = new HashSet<PlayerId>();
            AddPlayerIds(_home, playerIds);
            AddPlayerIds(_away, playerIds);
        }

        private static TeamSnapshotV2 UpgradeTeam(TeamSnapshotV1 team)
        {
            var players = new PlayerSnapshotV2[team.Players.Count];
            for (var index = 0; index < players.Length; index++)
            {
                var player = team.Players[index];
                var ability = player.Ability;
                players[index] = new PlayerSnapshotV2(player.PlayerId, player.DisplayName, player.JerseyNumber,
                    player.Position, new PlayerAbilitySnapshotV2(ability.Mobility, ability.Reaction, ability.Jump,
                    ability.ReceiveTechnique, ability.SetTechnique, ability.AttackTechnique, ability.AttackPower,
                    DefaultReach(player.Position)));
            }

            return new TeamSnapshotV2(team.TeamId, team.DisplayName, team.Side, players);
        }

        private static float DefaultReach(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.OutsideHitter => 3.42f,
                PlayerPosition.Opposite => 3.42f,
                PlayerPosition.MiddleBlocker => 3.48f,
                _ => 3.20f
            };
        }

        private static void AddPlayerIds(TeamSnapshotV2 team, ISet<PlayerId> playerIds)
        {
            foreach (var player in team.Players)
                if (!playerIds.Add(player.PlayerId)) throw new ContractValidationException("Player IDs must be unique across the match context.");
        }
    }

    internal static class MatchContextHashV2
    {
        public static string Compute(MatchContextV2 context)
        {
            var canonical = new StringBuilder(1024);
            canonical.Append("{\"contractVersion\":").Append(context.ContractVersion.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"sessionId\":"); AppendString(canonical, context.SessionId.ToString("D"));
            canonical.Append(",\"seed\":").Append(context.Seed.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"home\":"); AppendTeam(canonical, context.Home);
            canonical.Append(",\"away\":"); AppendTeam(canonical, context.Away);
            canonical.Append('}');
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var hex = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++) hex.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }

        private static void AppendTeam(StringBuilder output, TeamSnapshotV2 team)
        {
            output.Append("{\"teamId\":"); AppendString(output, team.TeamId.Value);
            output.Append(",\"displayName\":"); AppendString(output, team.DisplayName);
            output.Append(",\"side\":").Append(((int)team.Side).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"players\":[");
            for (var index = 0; index < team.Players.Count; index++)
            {
                if (index > 0) output.Append(',');
                AppendPlayer(output, team.Players[index]);
            }
            output.Append("]}");
        }

        private static void AppendPlayer(StringBuilder output, PlayerSnapshotV2 player)
        {
            output.Append("{\"playerId\":"); AppendString(output, player.PlayerId.Value);
            output.Append(",\"displayName\":"); AppendString(output, player.DisplayName);
            output.Append(",\"jerseyNumber\":").Append(player.JerseyNumber.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"position\":").Append(((int)player.Position).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"ability\":{");
            var ability = player.Ability;
            AppendFloat(output, "mobility", ability.Mobility); AppendFloat(output, "reaction", ability.Reaction);
            AppendFloat(output, "jump", ability.Jump); AppendFloat(output, "receiveTechnique", ability.ReceiveTechnique);
            AppendFloat(output, "setTechnique", ability.SetTechnique); AppendFloat(output, "attackTechnique", ability.AttackTechnique);
            AppendFloat(output, "attackPower", ability.AttackPower); AppendFloat(output, "maxAttackReach", ability.MaxAttackReach);
            output.Append("}}");
        }

        private static void AppendFloat(StringBuilder output, string name, float value)
        {
            if (output[output.Length - 1] != '{') output.Append(',');
            output.Append('"').Append(name).Append("\":").Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder output, string value)
        {
            output.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 32) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else output.Append(character);
                        break;
                }
            }
            output.Append('"');
        }
    }
}
