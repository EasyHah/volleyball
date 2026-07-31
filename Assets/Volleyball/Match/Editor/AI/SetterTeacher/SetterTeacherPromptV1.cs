using System.Globalization;
using System.Text;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public static class SetterTeacherPromptV1
    {
        public const string SystemPrompt =
            "You rank only the supplied legal volleyball attackers for a setter target review. " +
            "Use only supplied facts. Return one JSON object and no markdown.";

        public static string BuildCasePrompt(SetterTeacherRequestV1 request)
        {
            if (request == null) throw new System.ArgumentNullException(nameof(request));
            var output = new StringBuilder();
            output.Append("{\"schemaVersion\":1,\"snapshotHash\":\"")
                .Append(request.SnapshotHash)
                .Append("\",\"candidateSetHash\":\"")
                .Append(request.CandidateSetHash)
                .Append("\",\"team\":\"")
                .Append(request.Team)
                .Append("\",\"availableSeconds\":")
                .Append(request.AvailableSeconds.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"countedHits\":")
                .Append(request.CountedHits)
                .Append(",\"candidates\":[");
            for (var index = 0; index < request.Candidates.Count; index++)
            {
                if (index > 0) output.Append(',');
                var candidate = request.Candidates[index];
                output.Append("{\"playerId\":\"").Append(candidate.PlayerId.Value)
                    .Append("\",\"reachability\":")
                    .Append(candidate.Reachability.ToString("R", CultureInfo.InvariantCulture))
                    .Append(",\"nominalRole\":")
                    .Append(candidate.NominalRole.ToString("R", CultureInfo.InvariantCulture))
                    .Append(",\"approach\":")
                    .Append(candidate.Approach.ToString("R", CultureInfo.InvariantCulture))
                    .Append(",\"angle\":")
                    .Append(candidate.Angle.ToString("R", CultureInfo.InvariantCulture))
                    .Append(",\"total\":")
                    .Append(candidate.Total.ToString("R", CultureInfo.InvariantCulture))
                    .Append('}');
            }

            return output.Append("]}. Return exactly {\"schemaVersion\":1,\"ranking\":[candidate IDs],\"reason\":\"review reason\"}.")
                .ToString();
        }
    }
}
