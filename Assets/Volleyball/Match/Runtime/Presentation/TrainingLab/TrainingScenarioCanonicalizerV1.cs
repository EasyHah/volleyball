using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Domain.Simulation;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    public static class TrainingScenarioCanonicalizerV1
    {
        public static byte[] ToCanonicalBytes(TrainingScenarioV1 value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var output = new StringBuilder();
            AppendInt(output, "format", value.FormatVersionValue);
            AppendText(output, "scenarioId", value.ScenarioId);
            AppendText(output, "displayName", value.DisplayName);
            AppendText(output, "source", value.Source);
            AppendText(output, "context", ContractJson.SerializeV4(value.Context));
            AppendInt(output, "firstServingSide", (int)value.FirstServingSide);
            AppendInt(output, "homeRotationOffset", value.HomeInitialRotationOffset);
            AppendInt(output, "awayRotationOffset", value.AwayInitialRotationOffset);
            AppendTactic(output, "homeTactics", value.HomeTactics);
            AppendTactic(output, "awayTactics", value.AwayTactics);
            AppendFloat(output, "ai.rolePreference", value.Ai.RolePreference);
            AppendFloat(output, "ai.reachability", value.Ai.Reachability);
            AppendFloat(output, "ai.approachDistance", value.Ai.ApproachDistance);
            AppendFloat(output, "ai.directionTolerance", value.Ai.DirectionTolerance);
            AppendInt(output, "playerCount", value.Players.Count);
            for (var index = 0; index < value.Players.Count; index++)
            {
                var player = value.Players[index];
                var prefix = "players." + index.ToString(CultureInfo.InvariantCulture);
                AppendText(output, prefix + ".id", player.PlayerId.Value);
                AppendVector(output, prefix + ".position", player.Position);
                AppendVector(output, prefix + ".forward", player.Forward);
                AppendInt(output, prefix + ".pose", (int)player.Pose);
            }

            AppendVector(output, "ball.position", value.BallPosition);
            AppendVector(output, "ball.velocity", value.BallVelocity);
            AppendInt(output, "start.recipe", (int)value.StartState.Recipe);
            AppendInt(output, "start.sourceTeam", (int)value.StartState.SourceTeam);
            AppendText(
                output,
                "start.lastActor",
                value.StartState.LastLegalActor?.Value ?? string.Empty);
            AppendInt(output, "access", (int)value.AccessLevel);
            return Encoding.UTF8.GetBytes(output.ToString());
        }

        public static string ComputeContentHash(TrainingScenarioV1 value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(ToCanonicalBytes(value));
            var result = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++)
            {
                result.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static void AppendTactic(
            StringBuilder output,
            string prefix,
            TrainingTeamTacticV1 value)
        {
            AppendInt(output, prefix + ".setRoute", (int)value.SetRoute);
            AppendInt(output, prefix + ".spikeRoute", (int)value.SpikeRoute);
            AppendFloat(output, prefix + ".setterX", value.SetterX);
            AppendFloat(output, prefix + ".setterZ", value.SetterZ);
            AppendFloat(output, prefix + ".attackerX", value.AttackerX);
            AppendFloat(output, prefix + ".attackerZ", value.AttackerZ);
            AppendFloat(output, prefix + ".defenderX", value.DefenderX);
            AppendFloat(output, prefix + ".defenderZ", value.DefenderZ);
            AppendInt(output, prefix + ".blocker", (int)value.Blocker);
            AppendFloat(output, prefix + ".blockX", value.BlockX);
            AppendFloat(output, prefix + ".blockZ", value.BlockZ);
            AppendInt(output, prefix + ".coverReceiver", (int)value.CoverReceiver);
            AppendFloat(output, prefix + ".coverX", value.CoverX);
            AppendFloat(output, prefix + ".coverZ", value.CoverZ);
            AppendInt(output, prefix + ".setRhythm", (int)value.SetRhythm);
            AppendFloat(output, prefix + ".attackFlight", value.AttackFlightSeconds);
        }

        private static void AppendVector(
            StringBuilder output,
            string key,
            SimVector3 value)
        {
            AppendFloat(output, key + ".x", value.X);
            AppendFloat(output, key + ".y", value.Y);
            AppendFloat(output, key + ".z", value.Z);
        }

        private static void AppendFloat(StringBuilder output, string key, float value)
        {
            AppendText(output, key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendInt(StringBuilder output, string key, int value)
        {
            AppendText(output, key, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendText(StringBuilder output, string key, string value)
        {
            var safe = value ?? string.Empty;
            output.Append(key)
                .Append('=')
                .Append(safe.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append('\n');
        }
    }
}
