using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class MatchContextV3 : IMatchContext
    {
        [DataMember(Name = "contractVersion", Order = 1)] private int _contractVersion;
        [DataMember(Name = "sessionId", Order = 2)] private string _sessionId;
        [DataMember(Name = "seed", Order = 3)] private int _seed;
        [DataMember(Name = "home", Order = 4)] private TeamSnapshotV3 _home;
        [DataMember(Name = "away", Order = 5)] private TeamSnapshotV3 _away;
        [DataMember(Name = "contextHash", Order = 6)] private string _contextHash;

        private MatchContextV3(Guid sessionId, int seed, TeamSnapshotV3 home, TeamSnapshotV3 away)
        {
            _contractVersion = ContractVersions.MatchV3;
            _sessionId = sessionId.ToString("D");
            _seed = seed;
            _home = home ?? throw new ArgumentNullException(nameof(home));
            _away = away ?? throw new ArgumentNullException(nameof(away));
            _contextHash = string.Empty;
            ValidatePayload();
            _contextHash = CanonicalMatchContextHashV3.Compute(this);
            Validate();
        }

        public int ContractVersion => _contractVersion;
        public Guid SessionId => Guid.ParseExact(_sessionId, "D");
        public int Seed => _seed;
        public TeamSnapshotV3 Home => _home;
        public TeamSnapshotV3 Away => _away;
        public string ContextHash => _contextHash;

        public static MatchContextV3 Create(Guid sessionId, int seed, TeamSnapshotV3 home, TeamSnapshotV3 away)
        {
            if (sessionId == Guid.Empty) throw new ContractValidationException("sessionId cannot be empty.");
            return new MatchContextV3(sessionId, seed, home, away);
        }

        public static MatchContextV3 UpgradeFromV2(MatchContextV2 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Validate();
            return Create(context.SessionId, context.Seed, UpgradeTeam(context.Home), UpgradeTeam(context.Away));
        }

        internal void Validate()
        {
            ValidatePayload();
            ContractGuard.Hash(_contextHash, nameof(ContextHash));
            if (!string.Equals(CanonicalMatchContextHashV3.Compute(this), _contextHash, StringComparison.Ordinal))
                throw new ContractValidationException("contextHash does not match the context payload.");
        }

        private void ValidatePayload()
        {
            if (_contractVersion != ContractVersions.MatchV3)
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

        private static TeamSnapshotV3 UpgradeTeam(TeamSnapshotV2 team)
        {
            var players = new PlayerSnapshotV3[team.Players.Count];
            for (var index = 0; index < players.Length; index++)
            {
                var player = team.Players[index];
                players[index] = new PlayerSnapshotV3(
                    player.PlayerId,
                    player.DisplayName,
                    player.JerseyNumber,
                    player.Position,
                    PlayerAbilitySnapshotV3.LegacyV2ToPlayerAbilitySnapshotV3(player.Ability, player.Position));
            }

            return new TeamSnapshotV3(team.TeamId, team.DisplayName, team.Side, players);
        }

        private static void AddPlayerIds(TeamSnapshotV3 team, ISet<PlayerId> playerIds)
        {
            foreach (var player in team.Players)
                if (!playerIds.Add(player.PlayerId)) throw new ContractValidationException("Player IDs must be unique across the match context.");
        }
    }

    public static class CanonicalMatchContextHashV3
    {
        public static string Compute(MatchContextV3 context)
        {
            var canonical = new StringBuilder(2048);
            canonical.Append("{\"contractVersion\":").Append(context.ContractVersion.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"sessionId\":"); CanonicalJsonHashV3.AppendString(canonical, context.SessionId.ToString("D"));
            canonical.Append(",\"seed\":").Append(context.Seed.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"home\":"); AppendTeam(canonical, context.Home);
            canonical.Append(",\"away\":"); AppendTeam(canonical, context.Away);
            canonical.Append('}');
            return CanonicalJsonHashV3.Sha256(canonical.ToString());
        }

        private static void AppendTeam(StringBuilder output, TeamSnapshotV3 team)
        {
            output.Append("{\"teamId\":"); CanonicalJsonHashV3.AppendString(output, team.TeamId.Value);
            output.Append(",\"displayName\":"); CanonicalJsonHashV3.AppendString(output, team.DisplayName);
            output.Append(",\"side\":").Append(((int)team.Side).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"players\":[");
            for (var index = 0; index < team.Players.Count; index++)
            {
                if (index > 0) output.Append(',');
                AppendPlayer(output, team.Players[index]);
            }
            output.Append("]}");
        }

        private static void AppendPlayer(StringBuilder output, PlayerSnapshotV3 player)
        {
            output.Append("{\"playerId\":"); CanonicalJsonHashV3.AppendString(output, player.PlayerId.Value);
            output.Append(",\"displayName\":"); CanonicalJsonHashV3.AppendString(output, player.DisplayName);
            output.Append(",\"jerseyNumber\":").Append(player.JerseyNumber.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"position\":").Append(((int)player.Position).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"ability\":{");
            var ability = player.Ability;
            CanonicalJsonHashV3.AppendFloat(output, "mobility", ability.Mobility); CanonicalJsonHashV3.AppendFloat(output, "reaction", ability.Reaction);
            CanonicalJsonHashV3.AppendFloat(output, "jump", ability.Jump); CanonicalJsonHashV3.AppendFloat(output, "maxAttackReach", ability.MaxAttackReach);
            CanonicalJsonHashV3.AppendFloat(output, "receiveTechnique", ability.ReceiveTechnique); CanonicalJsonHashV3.AppendFloat(output, "setTechnique", ability.SetTechnique);
            CanonicalJsonHashV3.AppendFloat(output, "attackControl", ability.AttackControl); CanonicalJsonHashV3.AppendFloat(output, "attackPower", ability.AttackPower);
            CanonicalJsonHashV3.AppendFloat(output, "softTouch", ability.SoftTouch); CanonicalJsonHashV3.AppendFloat(output, "blockTechnique", ability.BlockTechnique);
            CanonicalJsonHashV3.AppendFloat(output, "courtAwareness", ability.CourtAwareness);
            output.Append(",\"sourceVersion\":").Append(ability.SourceVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"migrationVersion\":").Append(ability.MigrationVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"isCompatibilityEstimate\":").Append(ability.IsCompatibilityEstimate ? "true" : "false");
            output.Append(",\"compatibilityCollapsedAxes\":[");
            for (var index = 0; index < ability.CompatibilityCollapsedAxes.Count; index++)
            {
                if (index > 0) output.Append(',');
                CanonicalJsonHashV3.AppendString(output, ability.CompatibilityCollapsedAxes[index]);
            }
            output.Append("]}}");
        }
    }

    internal static class CanonicalJsonHashV3
    {
        public static void AppendFloat(StringBuilder output, string name, float value)
        {
            if (output[output.Length - 1] != '{') output.Append(',');
            output.Append('"').Append(name).Append("\":").Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        public static void AppendString(StringBuilder output, string value)
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

        public static string Sha256(string canonical)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var hex = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++) hex.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }
    }
}
