using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    [DataContract]
    public sealed class PlayerSnapshotV1
    {
        [DataMember(Name = "playerId", Order = 1)]
        private string _playerId;

        [DataMember(Name = "displayName", Order = 2)]
        private string _displayName;

        [DataMember(Name = "jerseyNumber", Order = 3)]
        private int _jerseyNumber;

        [DataMember(Name = "position", Order = 4)]
        private PlayerPosition _position;

        [DataMember(Name = "ability", Order = 5)]
        private PlayerAbilitySnapshotV1 _ability;

        public PlayerSnapshotV1(
            PlayerId playerId,
            string displayName,
            int jerseyNumber,
            PlayerPosition position,
            PlayerAbilitySnapshotV1 ability)
        {
            _playerId = ContractGuard.RequiredId(playerId.Value, nameof(playerId));
            _displayName = ContractGuard.RequiredText(displayName, nameof(displayName), 100);
            if (jerseyNumber < 0 || jerseyNumber > 99)
            {
                throw new ContractValidationException("jerseyNumber must be in the range [0, 99].");
            }

            ContractGuard.DefinedEnum(position, nameof(position));
            _jerseyNumber = jerseyNumber;
            _position = position;
            _ability = ability ?? throw new ArgumentNullException(nameof(ability));
            _ability.Validate();
        }

        public PlayerId PlayerId => new PlayerId(_playerId);

        public string DisplayName => _displayName;

        public int JerseyNumber => _jerseyNumber;

        public PlayerPosition Position => _position;

        public PlayerAbilitySnapshotV1 Ability => _ability;

        internal void Validate()
        {
            ContractGuard.RequiredId(_playerId, nameof(PlayerId));
            ContractGuard.RequiredText(_displayName, nameof(DisplayName), 100);
            if (_jerseyNumber < 0 || _jerseyNumber > 99)
            {
                throw new ContractValidationException("JerseyNumber must be in the range [0, 99].");
            }

            ContractGuard.DefinedEnum(_position, nameof(Position));
            if (_ability == null)
            {
                throw new ContractValidationException("Ability is required.");
            }

            _ability.Validate();
        }
    }

    [DataContract]
    public sealed class TeamSnapshotV1
    {
        [DataMember(Name = "teamId", Order = 1)]
        private string _teamId;

        [DataMember(Name = "displayName", Order = 2)]
        private string _displayName;

        [DataMember(Name = "side", Order = 3)]
        private TeamSide _side;

        [DataMember(Name = "players", Order = 4)]
        private PlayerSnapshotV1[] _players;

        public TeamSnapshotV1(
            TeamId teamId,
            string displayName,
            TeamSide side,
            IReadOnlyList<PlayerSnapshotV1> players)
        {
            _teamId = ContractGuard.RequiredId(teamId.Value, nameof(teamId));
            _displayName = ContractGuard.RequiredText(displayName, nameof(displayName), 100);
            ContractGuard.DefinedEnum(side, nameof(side));
            _side = side;
            _players = CopyPlayers(players);
            Validate();
        }

        public TeamId TeamId => new TeamId(_teamId);

        public string DisplayName => _displayName;

        public TeamSide Side => _side;

        public IReadOnlyList<PlayerSnapshotV1> Players =>
            new ReadOnlyCollection<PlayerSnapshotV1>(_players);

        internal void Validate()
        {
            ContractGuard.RequiredId(_teamId, nameof(TeamId));
            ContractGuard.RequiredText(_displayName, nameof(DisplayName), 100);
            ContractGuard.DefinedEnum(_side, nameof(Side));
            if (_players == null || _players.Length < 3 || _players.Length > 14)
            {
                throw new ContractValidationException("A team must contain 3 to 14 player snapshots.");
            }

            var ids = new HashSet<PlayerId>();
            for (var index = 0; index < _players.Length; index++)
            {
                var player = _players[index];
                if (player == null)
                {
                    throw new ContractValidationException("Player snapshots cannot contain null.");
                }

                player.Validate();
                if (!ids.Add(player.PlayerId))
                {
                    throw new ContractValidationException("Player IDs must be unique inside a team.");
                }
            }
        }

        private static PlayerSnapshotV1[] CopyPlayers(IReadOnlyList<PlayerSnapshotV1> players)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            var copy = new PlayerSnapshotV1[players.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = players[index];
            }

            return copy;
        }
    }

    [DataContract]
    public sealed class MatchContextV1
    {
        [DataMember(Name = "contractVersion", Order = 1)]
        private int _contractVersion;

        [DataMember(Name = "sessionId", Order = 2)]
        private string _sessionId;

        [DataMember(Name = "seed", Order = 3)]
        private int _seed;

        [DataMember(Name = "home", Order = 4)]
        private TeamSnapshotV1 _home;

        [DataMember(Name = "away", Order = 5)]
        private TeamSnapshotV1 _away;

        [DataMember(Name = "contextHash", Order = 6)]
        private string _contextHash;

        private MatchContextV1(
            Guid sessionId,
            int seed,
            TeamSnapshotV1 home,
            TeamSnapshotV1 away)
        {
            _contractVersion = ContractVersions.MatchV1;
            _sessionId = sessionId.ToString("D");
            _seed = seed;
            _home = home ?? throw new ArgumentNullException(nameof(home));
            _away = away ?? throw new ArgumentNullException(nameof(away));
            _contextHash = string.Empty;
            ValidatePayload();
            _contextHash = MatchContextHash.Compute(this);
            Validate();
        }

        public int ContractVersion => _contractVersion;

        public Guid SessionId => Guid.ParseExact(_sessionId, "D");

        public int Seed => _seed;

        public TeamSnapshotV1 Home => _home;

        public TeamSnapshotV1 Away => _away;

        public string ContextHash => _contextHash;

        public static MatchContextV1 Create(
            Guid sessionId,
            int seed,
            TeamSnapshotV1 home,
            TeamSnapshotV1 away)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ContractValidationException("sessionId cannot be empty.");
            }

            return new MatchContextV1(sessionId, seed, home, away);
        }

        internal void Validate()
        {
            ValidatePayload();
            ContractGuard.Hash(_contextHash, nameof(ContextHash));
            var expected = MatchContextHash.Compute(this);
            if (!string.Equals(expected, _contextHash, StringComparison.Ordinal))
            {
                throw new ContractValidationException("contextHash does not match the context payload.");
            }
        }

        private void ValidatePayload()
        {
            if (!ContractVersions.SupportsMatch(_contractVersion))
            {
                throw new ContractValidationException(
                    "Unsupported match contract version: " + _contractVersion + ".");
            }

            if (!Guid.TryParseExact(_sessionId, "D", out var sessionId) || sessionId == Guid.Empty)
            {
                throw new ContractValidationException("sessionId must be a non-empty canonical GUID.");
            }

            if (_home == null || _away == null)
            {
                throw new ContractValidationException("Both home and away snapshots are required.");
            }

            _home.Validate();
            _away.Validate();
            if (_home.Side != TeamSide.Home || _away.Side != TeamSide.Away)
            {
                throw new ContractValidationException("Team snapshots must use their matching home and away sides.");
            }

            if (_home.TeamId.Equals(_away.TeamId))
            {
                throw new ContractValidationException("Home and away teams must have different IDs.");
            }

            var playerIds = new HashSet<PlayerId>();
            AddPlayerIds(_home, playerIds);
            AddPlayerIds(_away, playerIds);
        }

        private static void AddPlayerIds(TeamSnapshotV1 team, ISet<PlayerId> playerIds)
        {
            foreach (var player in team.Players)
            {
                if (!playerIds.Add(player.PlayerId))
                {
                    throw new ContractValidationException("Player IDs must be unique across the match context.");
                }
            }
        }
    }

    internal static class MatchContextHash
    {
        public static string Compute(MatchContextV1 context)
        {
            var canonical = new StringBuilder(1024);
            canonical.Append("{\"contractVersion\":");
            canonical.Append(context.ContractVersion.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"sessionId\":");
            AppendString(canonical, context.SessionId.ToString("D"));
            canonical.Append(",\"seed\":");
            canonical.Append(context.Seed.ToString(CultureInfo.InvariantCulture));
            canonical.Append(",\"home\":");
            AppendTeam(canonical, context.Home);
            canonical.Append(",\"away\":");
            AppendTeam(canonical, context.Away);
            canonical.Append('}');

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var hex = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++)
            {
                hex.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        private static void AppendTeam(StringBuilder output, TeamSnapshotV1 team)
        {
            output.Append("{\"teamId\":");
            AppendString(output, team.TeamId.Value);
            output.Append(",\"displayName\":");
            AppendString(output, team.DisplayName);
            output.Append(",\"side\":");
            output.Append(((int)team.Side).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"players\":[");
            for (var index = 0; index < team.Players.Count; index++)
            {
                if (index > 0)
                {
                    output.Append(',');
                }

                AppendPlayer(output, team.Players[index]);
            }

            output.Append("]}");
        }

        private static void AppendPlayer(StringBuilder output, PlayerSnapshotV1 player)
        {
            output.Append("{\"playerId\":");
            AppendString(output, player.PlayerId.Value);
            output.Append(",\"displayName\":");
            AppendString(output, player.DisplayName);
            output.Append(",\"jerseyNumber\":");
            output.Append(player.JerseyNumber.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"position\":");
            output.Append(((int)player.Position).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"ability\":{");
            AppendAbility(output, player.Ability);
            output.Append("}}");
        }

        private static void AppendAbility(StringBuilder output, PlayerAbilitySnapshotV1 ability)
        {
            output.Append("\"mobility\":");
            AppendFloat(output, ability.Mobility);
            output.Append(",\"reaction\":");
            AppendFloat(output, ability.Reaction);
            output.Append(",\"jump\":");
            AppendFloat(output, ability.Jump);
            output.Append(",\"receiveTechnique\":");
            AppendFloat(output, ability.ReceiveTechnique);
            output.Append(",\"setTechnique\":");
            AppendFloat(output, ability.SetTechnique);
            output.Append(",\"attackTechnique\":");
            AppendFloat(output, ability.AttackTechnique);
            output.Append(",\"attackPower\":");
            AppendFloat(output, ability.AttackPower);
        }

        private static void AppendFloat(StringBuilder output, float value)
        {
            output.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder output, string value)
        {
            output.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"':
                        output.Append("\\\"");
                        break;
                    case '\\':
                        output.Append("\\\\");
                        break;
                    case '\b':
                        output.Append("\\b");
                        break;
                    case '\f':
                        output.Append("\\f");
                        break;
                    case '\n':
                        output.Append("\\n");
                        break;
                    case '\r':
                        output.Append("\\r");
                        break;
                    case '\t':
                        output.Append("\\t");
                        break;
                    default:
                        if (character < 32)
                        {
                            output.Append("\\u");
                            output.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            output.Append(character);
                        }

                        break;
                }
            }

            output.Append('"');
        }
    }
}
