using System;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Shared.Contracts;

namespace Volleyball.Presentation.TrainingLab
{
    public static class TrainingScenarioCanonicalizerV2
    {
        public static string ComputeTemplateHash(
            string scenarioId,
            string displayName,
            MatchContextV5 context)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
                throw new ArgumentException("Scenario ID is required.", nameof(scenarioId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name is required.", nameof(displayName));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var canonical = "volleyball.training-template.v2\n" +
                Quote(scenarioId) + "\n" + Quote(displayName) + "\n" +
                ContractJson.SerializeV5(context);
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var output = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes) output.Append(value.ToString("x2"));
            return output.ToString();
        }

        private static string Quote(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\n", "\\n");
        }
    }
}
