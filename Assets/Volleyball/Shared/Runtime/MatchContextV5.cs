using System;
using System.Collections.Generic;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class MatchContextV5
    {
        private MatchContextV5(Guid sessionId, int seed, TeamSnapshotV5 home, TeamSnapshotV5 away,
            string physicsConfigurationHash, TrajectoryPredictionProviderConfigurationV5 trajectoryConfiguration,
            int rulesVersion)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ContractValidationException("sessionId cannot be empty.");
            }

            ContractGuard.Hash(physicsConfigurationHash, nameof(physicsConfigurationHash));
            TrajectoryPredictionProviderConfiguration = trajectoryConfiguration ??
                throw new ContractValidationException("trajectoryConfiguration is required.");
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
            ContextHash = CanonicalMatchContextHashV5.Compute(this);
        }

        public int ContractVersion { get; }
        public int RulesVersion { get; }
        public Guid SessionId { get; }
        public int Seed { get; }
        public string PhysicsConfigurationHash { get; }
        public TrajectoryPredictionProviderConfigurationV5 TrajectoryPredictionProviderConfiguration { get; }
        public TeamSnapshotV5 Home { get; }
        public TeamSnapshotV5 Away { get; }
        public string ContextHash { get; }

        public static MatchContextV5 Create(Guid sessionId, int seed, TeamSnapshotV5 home, TeamSnapshotV5 away,
            string physicsConfigurationHash, TrajectoryPredictionProviderConfigurationV5 trajectoryConfiguration,
            int rulesVersion = RulesVersions.FullRallyV3)
        {
            return new MatchContextV5(sessionId, seed, home, away, physicsConfigurationHash,
                trajectoryConfiguration, rulesVersion);
        }

        internal void Validate()
        {
            if (ContractVersion != ContractVersions.MatchV5 ||
                !string.Equals(ContextHash, CanonicalMatchContextHashV5.Compute(this), StringComparison.Ordinal))
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

    }

    public static class CanonicalMatchContextHashV5
    {
        public static string Compute(MatchContextV5 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return CanonicalJsonHashV4.Sha256(
                "volleyball.match-context.v5\n" + CanonicalMatchJsonV5.ContextPayload(context));
        }
    }

    internal static class CanonicalMatchJsonV5
    {
        public static string SerializeContext(MatchContextV5 context)
        {
            var payload = ContextPayload(context);
            return payload.Substring(0, payload.Length - 1) +
                ",\"contextHash\":" + Quote(context.ContextHash) + "}";
        }

        public static string ContextPayload(MatchContextV5 context)
        {
            var output = new StringBuilder(8192);
            output.Append("{\"contractVersion\":").Append(context.ContractVersion);
            output.Append(",\"rulesVersion\":").Append(context.RulesVersion);
            output.Append(",\"sessionId\":").Append(Quote(context.SessionId.ToString("D")));
            output.Append(",\"seed\":").Append(context.Seed);
            output.Append(",\"physicsConfigurationHash\":").Append(Quote(context.PhysicsConfigurationHash));
            output.Append(",\"trajectoryPredictionProviderConfiguration\":{");
            output.Append("\"cacheCapacity\":").Append(context.TrajectoryPredictionProviderConfiguration.CacheCapacity);
            output.Append(",\"cacheEvictionPolicy\":").Append((int)context.TrajectoryPredictionProviderConfiguration.CacheEvictionPolicy);
            output.Append(",\"predictorVersion\":").Append(context.TrajectoryPredictionProviderConfiguration.PredictorVersion);
            output.Append(",\"predictorConfigurationHash\":").Append(Quote(context.TrajectoryPredictionProviderConfiguration.PredictorConfigurationHash));
            output.Append('}');
            output.Append(",\"home\":");
            AppendTeam(output, context.Home);
            output.Append(",\"away\":");
            AppendTeam(output, context.Away);
            output.Append('}');
            return output.ToString();
        }

        private static void AppendTeam(StringBuilder output, TeamSnapshotV5 team)
        {
            output.Append("{\"teamId\":").Append(Quote(team.TeamId.Value));
            output.Append(",\"displayName\":").Append(Quote(team.DisplayName));
            output.Append(",\"side\":").Append((int)team.Side);
            output.Append(",\"rotationOrder\":[");
            for (var index = 0; index < team.RotationOrder.Count; index++)
            {
                if (index > 0) output.Append(',');
                AppendPlayer(output, team.RotationOrder[index]);
            }
            output.Append("]}");
        }

        private static void AppendPlayer(StringBuilder output, PlayerSnapshotV5 player)
        {
            output.Append("{\"playerId\":").Append(Quote(player.PlayerId.Value));
            output.Append(",\"displayName\":").Append(Quote(player.DisplayName));
            output.Append(",\"jerseyNumber\":").Append(player.JerseyNumber);
            output.Append(",\"position\":").Append((int)player.Position);
            output.Append(",\"dominantHand\":").Append((int)player.DominantHand);
            output.Append(",\"bases\":{");
            AppendBases(output, player.Bases);
            output.Append("},\"derived\":{");
            output.Append("\"formulaVersion\":").Append(player.Derived.FormulaVersion);
            output.Append(",\"coefficientVersion\":").Append(player.Derived.CoefficientVersion);
            output.Append(",\"inputFingerprint\":").Append(Quote(player.Derived.InputFingerprint));
            output.Append(",\"resultFingerprint\":").Append(Quote(player.Derived.ResultFingerprint));
            output.Append("}}");
        }

        private static void AppendBases(StringBuilder output, CareerBaseAttributesV5 value)
        {
            output.Append("\"strength\":").Append(value.Strength);
            output.Append(",\"heightMillimeters\":").Append(value.HeightMillimeters);
            output.Append(",\"jump\":").Append(value.Jump);
            output.Append(",\"movement\":").Append(value.Movement);
            output.Append(",\"reaction\":").Append(value.Reaction);
            output.Append(",\"coordination\":").Append(value.Coordination);
            output.Append(",\"attack\":").Append(value.Attack);
            output.Append(",\"defense\":").Append(value.Defense);
            output.Append(",\"courtIq\":").Append(value.CourtIq);
            output.Append(",\"block\":").Append(value.Block);
            output.Append(",\"serve\":").Append(value.Serve);
            output.Append(",\"set\":").Append(value.Set);
        }

        private static string Quote(string value)
        {
            var output = new StringBuilder(value.Length + 2);
            CanonicalJsonHashV4.AppendString(output, value);
            return output.ToString();
        }
    }
}
