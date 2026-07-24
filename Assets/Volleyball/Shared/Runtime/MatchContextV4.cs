using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Volleyball.Shared.Contracts
{
    public sealed class MatchContextV4
    {
        private MatchContextV4(
            Guid sessionId,
            int seed,
            TeamSnapshotV4 home,
            TeamSnapshotV4 away,
            string physicsConfigurationHash,
            int rulesVersion)
        {
            ContractVersion = ContractVersions.MatchV4;
            RulesVersion = rulesVersion;
            SessionId = sessionId;
            Seed = seed;
            Home = home ?? throw new ContractValidationException("home is required.");
            Away = away ?? throw new ContractValidationException("away is required.");
            PhysicsConfigurationHash = physicsConfigurationHash;
            FormulaVersion = FirstPlayer(Home).Derived.FormulaVersion;
            CoefficientVersion = FirstPlayer(Home).Derived.CoefficientVersion;
            ValidatePayload();
            ContextHash = CanonicalMatchContextHashV4.Compute(this);
            Validate();
        }

        public int ContractVersion { get; }
        public int RulesVersion { get; }
        public Guid SessionId { get; }
        public int Seed { get; }
        public string PhysicsConfigurationHash { get; }
        public int FormulaVersion { get; }
        public int CoefficientVersion { get; }
        public TeamSnapshotV4 Home { get; }
        public TeamSnapshotV4 Away { get; }
        public string ContextHash { get; }

        public static MatchContextV4 Create(
            Guid sessionId,
            int seed,
            TeamSnapshotV4 home,
            TeamSnapshotV4 away,
            string physicsConfigurationHash,
            int rulesVersion = ContractVersions.MatchV3)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ContractValidationException("sessionId cannot be empty.");
            }

            return new MatchContextV4(
                sessionId,
                seed,
                home,
                away,
                physicsConfigurationHash,
                rulesVersion);
        }

        internal void Validate()
        {
            ValidatePayload();
            ContractGuard.Hash(ContextHash, nameof(ContextHash));
            if (!string.Equals(
                    CanonicalMatchContextHashV4.Compute(this),
                    ContextHash,
                    StringComparison.Ordinal))
            {
                throw new ContractValidationException(
                    "contextHash does not match the native V4 context payload.");
            }
        }

        private void ValidatePayload()
        {
            if (ContractVersion != ContractVersions.MatchV4)
            {
                throw new ContractValidationException(
                    "Unsupported match contract version: " + ContractVersion + ".");
            }

            if (RulesVersion != ContractVersions.MatchV3)
            {
                throw new ContractValidationException(
                    "rulesVersion must identify the independently versioned V3 rules.");
            }

            if (SessionId == Guid.Empty)
            {
                throw new ContractValidationException("sessionId cannot be empty.");
            }

            ContractGuard.Hash(
                PhysicsConfigurationHash,
                nameof(PhysicsConfigurationHash));
            if (Home == null || Away == null)
            {
                throw new ContractValidationException(
                    "Both home and away V4 teams are required.");
            }

            Home.Validate();
            Away.Validate();
            if (Home.Side != TeamSide.Home || Away.Side != TeamSide.Away)
            {
                throw new ContractValidationException(
                    "Team snapshots must use their matching home and away sides.");
            }

            if (Home.TeamId.Equals(Away.TeamId))
            {
                throw new ContractValidationException(
                    "Home and away teams must have different IDs.");
            }

            var playerIds = new HashSet<PlayerId>();
            AddAndValidatePlayers(Home, playerIds);
            AddAndValidatePlayers(Away, playerIds);
        }

        private void AddAndValidatePlayers(
            TeamSnapshotV4 team,
            ISet<PlayerId> playerIds)
        {
            foreach (var player in team.RotationOrder)
            {
                if (!playerIds.Add(player.PlayerId))
                {
                    throw new ContractValidationException(
                        "Player IDs must be unique across the match context.");
                }

                if (player.Derived.FormulaVersion != FormulaVersion ||
                    player.Derived.CoefficientVersion != CoefficientVersion)
                {
                    throw new ContractValidationException(
                        "Every V4 player must use the context derivation versions.");
                }
            }
        }

        private static PlayerSnapshotV4 FirstPlayer(TeamSnapshotV4 team)
        {
            if (team == null ||
                team.RotationOrder == null ||
                team.RotationOrder.Count == 0 ||
                team.RotationOrder[0] == null)
            {
                throw new ContractValidationException(
                    "home must contain a valid V4 rotation order.");
            }

            return team.RotationOrder[0];
        }
    }

    public static class CanonicalMatchContextHashV4
    {
        private const string HashFamily = "volleyball.match-context.v4\n";

        public static string Compute(MatchContextV4 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return CanonicalJsonHashV3.Sha256(
                HashFamily + CanonicalMatchJsonV4.ContextPayload(context));
        }
    }

    internal static class CanonicalMatchJsonV4
    {
        public static string SerializeContext(MatchContextV4 context)
        {
            var payload = ContextPayload(context);
            return payload.Substring(0, payload.Length - 1) +
                ",\"contextHash\":" + Quote(context.ContextHash) + "}";
        }

        public static string ContextPayload(MatchContextV4 context)
        {
            var output = new StringBuilder(8192);
            output.Append("{\"contractVersion\":")
                .Append(context.ContractVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"rulesVersion\":")
                .Append(context.RulesVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"sessionId\":").Append(Quote(context.SessionId.ToString("D")));
            output.Append(",\"seed\":")
                .Append(context.Seed.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"physicsConfigurationHash\":")
                .Append(Quote(context.PhysicsConfigurationHash));
            output.Append(",\"formulaVersion\":")
                .Append(context.FormulaVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"coefficientVersion\":")
                .Append(context.CoefficientVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"home\":");
            AppendTeam(output, context.Home);
            output.Append(",\"away\":");
            AppendTeam(output, context.Away);
            output.Append('}');
            return output.ToString();
        }

        internal static void AppendTeam(StringBuilder output, TeamSnapshotV4 team)
        {
            output.Append("{\"teamId\":").Append(Quote(team.TeamId.Value));
            output.Append(",\"displayName\":").Append(Quote(team.DisplayName));
            output.Append(",\"side\":")
                .Append(((int)team.Side).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"rotationOrder\":[");
            for (var index = 0; index < team.RotationOrder.Count; index++)
            {
                if (index > 0)
                {
                    output.Append(',');
                }

                AppendPlayer(output, team.RotationOrder[index]);
            }

            output.Append("]}");
        }

        private static void AppendPlayer(
            StringBuilder output,
            PlayerSnapshotV4 player)
        {
            output.Append("{\"playerId\":").Append(Quote(player.PlayerId.Value));
            output.Append(",\"displayName\":").Append(Quote(player.DisplayName));
            output.Append(",\"jerseyNumber\":")
                .Append(player.JerseyNumber.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"position\":")
                .Append(((int)player.Position).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"dominantHand\":")
                .Append(((int)player.DominantHand).ToString(CultureInfo.InvariantCulture));
            output.Append(",\"physical\":{");
            AppendFloat(output, "heightMeters", player.Physical.HeightMeters);
            AppendFloat(output, "standingReachMeters", player.Physical.StandingReachMeters);
            AppendFloat(output, "jump", player.Physical.Jump);
            AppendFloat(output, "mobility", player.Physical.Mobility);
            AppendFloat(output, "reaction", player.Physical.Reaction);
            AppendFloat(output, "coordination", player.Physical.Coordination);
            output.Append("},\"technical\":{");
            AppendFloat(output, "attackTechnique", player.Technical.AttackTechnique);
            AppendFloat(output, "attackPower", player.Technical.AttackPower);
            AppendFloat(output, "blockTechnique", player.Technical.BlockTechnique);
            AppendFloat(output, "defenseTechnique", player.Technical.DefenseTechnique);
            AppendFloat(output, "receiveTechnique", player.Technical.ReceiveTechnique);
            AppendFloat(output, "setTechnique", player.Technical.SetTechnique);
            AppendFloat(output, "serveTechnique", player.Technical.ServeTechnique);
            AppendFloat(output, "softTouch", player.Technical.SoftTouch);
            AppendFloat(output, "courtAwareness", player.Technical.CourtAwareness);
            output.Append("},\"derived\":{\"formulaVersion\":")
                .Append(player.Derived.FormulaVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"coefficientVersion\":")
                .Append(player.Derived.CoefficientVersion.ToString(CultureInfo.InvariantCulture));
            output.Append(",\"inputFingerprint\":")
                .Append(Quote(player.Derived.InputFingerprint));
            output.Append(",\"resultFingerprint\":")
                .Append(Quote(player.Derived.ResultFingerprint));
            output.Append("}}");
        }

        internal static void AppendFloat(
            StringBuilder output,
            string name,
            float value)
        {
            if (output[output.Length - 1] != '{')
            {
                output.Append(',');
            }

            output.Append('"').Append(name).Append("\":")
                .Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        internal static string Quote(string value)
        {
            var output = new StringBuilder(value == null ? 4 : value.Length + 2);
            CanonicalJsonHashV3.AppendString(output, value ?? string.Empty);
            return output.ToString();
        }
    }
}
