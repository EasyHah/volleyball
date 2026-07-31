using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Domain.Prototype;
using Volleyball.Domain.Simulation;
using Volleyball.AI;
using Volleyball.Presentation.TrainingLab;
using StablePlayerId = Volleyball.Shared.Contracts.PlayerId;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterTeacherCandidateV1
    {
        internal SetterTeacherCandidateV1(DecisionCandidateSnapshotV1 value)
        {
            PlayerId = value.PlayerId;
            Reachability = value.Reachability;
            NominalRole = value.NominalRole;
            Approach = value.Approach;
            Angle = value.Angle;
            Total = value.Total;
        }

        public StablePlayerId PlayerId { get; }
        public float Reachability { get; }
        public float NominalRole { get; }
        public float Approach { get; }
        public float Angle { get; }
        public float Total { get; }
    }

    public sealed class SetterTeacherRequestV1
    {
        public const int SchemaVersion = 1;
        public const string PromptVersion = "setter-teacher-prompt-v1";

        private readonly IReadOnlyList<SetterTeacherCandidateV1> _candidates;

        private SetterTeacherRequestV1(SetterTargetSnapshotV1 snapshot)
        {
            SnapshotHash = snapshot.SnapshotHash;
            RulesVersion = snapshot.RulesVersion;
            FeatureVersion = snapshot.FeatureVersionValue;
            SourceSequence = snapshot.SourceSequence;
            Team = snapshot.Team;
            AvailableSeconds = snapshot.AvailableSeconds;
            BallPosition = snapshot.BallPosition;
            BallVelocity = snapshot.BallVelocity;
            PredictedBallTarget = snapshot.PredictedBallTarget;
            CountedHits = snapshot.CountedHits;
            LastLegalActor = snapshot.LastLegalActor;
            SelectedAttacker = snapshot.SelectedAttacker;
            _candidates = new ReadOnlyCollection<SetterTeacherCandidateV1>(
                snapshot.Candidates
                    .Where(value => value.IsFeasible)
                    .OrderBy(value => value.PlayerId.Value, StringComparer.Ordinal)
                    .Select(value => new SetterTeacherCandidateV1(value))
                    .ToArray());
            CandidateSetHash = Hash(CanonicalCandidates(this));
            RequestHash = Hash(CanonicalRequest(this));
        }

        public string SnapshotHash { get; }
        public int RulesVersion { get; }
        public string FeatureVersion { get; }
        public long SourceSequence { get; }
        public TeamId Team { get; }
        public float AvailableSeconds { get; }
        public SimVector3 BallPosition { get; }
        public SimVector3 BallVelocity { get; }
        public SimVector3 PredictedBallTarget { get; }
        public int CountedHits { get; }
        public StablePlayerId? LastLegalActor { get; }
        public StablePlayerId SelectedAttacker { get; }
        public IReadOnlyList<SetterTeacherCandidateV1> Candidates => _candidates;
        public string CandidateSetHash { get; }
        public string RequestHash { get; }

        public static SetterTeacherRequestV1 Create(SetterTargetSnapshotV1 snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            return new SetterTeacherRequestV1(snapshot);
        }

        private static string CanonicalCandidates(SetterTeacherRequestV1 value)
        {
            var output = new StringBuilder();
            foreach (var candidate in value.Candidates)
            {
                Append(output, "playerId", candidate.PlayerId.Value);
                Append(output, "reachability", candidate.Reachability);
                Append(output, "nominalRole", candidate.NominalRole);
                Append(output, "approach", candidate.Approach);
                Append(output, "angle", candidate.Angle);
                Append(output, "total", candidate.Total);
            }
            return output.ToString();
        }

        private static string CanonicalRequest(SetterTeacherRequestV1 value)
        {
            var output = new StringBuilder();
            Append(output, "schema", SchemaVersion);
            Append(output, "prompt", PromptVersion);
            Append(output, "snapshotHash", value.SnapshotHash);
            Append(output, "candidateSetHash", value.CandidateSetHash);
            Append(output, "rulesVersion", value.RulesVersion);
            Append(output, "featureVersion", value.FeatureVersion);
            return output.ToString();
        }

        private static void Append(StringBuilder output, string key, object value)
        {
            var text = value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value?.ToString() ?? string.Empty;
            output.Append(key).Append('=').Append(text.Length).Append(':')
                .Append(text).Append('\n');
        }

        private static string Hash(string text)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text))
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
