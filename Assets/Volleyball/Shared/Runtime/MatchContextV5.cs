using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class MatchContextV5
    {
        private MatchContextV5(Guid sessionId, int seed, TeamSnapshotV5 home, TeamSnapshotV5 away,
            string physicsConfigurationHash, int rulesVersion)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ContractValidationException("sessionId cannot be empty.");
            }

            ContractGuard.Hash(physicsConfigurationHash, nameof(physicsConfigurationHash));
            if (rulesVersion != RulesVersions.FullRallyV3)
            {
                throw new ContractValidationException("V5 must use the independently versioned V3 rules.");
            }

            Home = home ?? throw new ContractValidationException("home is required.");
            Away = away ?? throw new ContractValidationException("away is required.");
            if (Home.Side != TeamSide.Home || Away.Side != TeamSide.Away || Home.TeamId.Equals(Away.TeamId))
            {
                throw new ContractValidationException("V5 home and away teams must have distinct matching sides.");
            }

            ContractVersion = ContractVersions.MatchV5;
            RulesVersion = rulesVersion;
            SessionId = sessionId;
            Seed = seed;
            PhysicsConfigurationHash = physicsConfigurationHash;
            ValidateTeams();
            ContextHash = Hash(Payload(this));
        }

        public int ContractVersion { get; }
        public int RulesVersion { get; }
        public Guid SessionId { get; }
        public int Seed { get; }
        public string PhysicsConfigurationHash { get; }
        public TeamSnapshotV5 Home { get; }
        public TeamSnapshotV5 Away { get; }
        public string ContextHash { get; }

        public static MatchContextV5 Create(Guid sessionId, int seed, TeamSnapshotV5 home, TeamSnapshotV5 away,
            string physicsConfigurationHash, int rulesVersion = RulesVersions.FullRallyV3)
        {
            return new MatchContextV5(sessionId, seed, home, away, physicsConfigurationHash, rulesVersion);
        }

        internal void Validate()
        {
            if (ContractVersion != ContractVersions.MatchV5 ||
                !string.Equals(ContextHash, Hash(Payload(this)), StringComparison.Ordinal))
            {
                throw new ContractValidationException("The V5 context identity does not match its payload.");
            }
        }

        private void ValidateTeams()
        {
            Home.Validate();
            Away.Validate();
            var players = new HashSet<PlayerId>();
            AddPlayers(Home, players);
            AddPlayers(Away, players);
        }

        private static void AddPlayers(TeamSnapshotV5 team, ISet<PlayerId> players)
        {
            foreach (var player in team.RotationOrder)
            {
                if (!players.Add(player.PlayerId))
                {
                    throw new ContractValidationException("Player IDs must be unique across a V5 context.");
                }
            }
        }

        private static string Payload(MatchContextV5 context)
        {
            var builder = new StringBuilder();
            builder.Append("v5|").Append(context.RulesVersion.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(context.SessionId.ToString("D"))
                .Append('|').Append(context.Seed.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(context.PhysicsConfigurationHash);
            AppendTeam(builder, context.Home);
            AppendTeam(builder, context.Away);
            return builder.ToString();
        }

        private static void AppendTeam(StringBuilder builder, TeamSnapshotV5 team)
        {
            builder.Append('|').Append(team.TeamId.Value).Append('|').Append((int)team.Side);
            foreach (var player in team.RotationOrder)
            {
                builder.Append('|').Append(player.PlayerId.Value).Append('|')
                    .Append(player.Derived.ResultFingerprint);
            }
        }

        private static string Hash(string payload)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var value in bytes) builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
