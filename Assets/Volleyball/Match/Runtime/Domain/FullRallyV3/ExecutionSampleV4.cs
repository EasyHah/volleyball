using System;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class ExecutionSampleV4 : IEquatable<ExecutionSampleV4>
    {
        // Deliberately permissive: malformed and non-finite executor output must
        // reach classification and become UnexpectedExecutionSample evidence.
        public ExecutionSampleV4(
            string envelopeIdentity,
            string samplingKey,
            ExecutionCandidateCategoryV4 candidateCategory,
            SimVector3 target,
            SimVector3 velocity,
            float effort)
        {
            EnvelopeIdentity = envelopeIdentity;
            SamplingKey = samplingKey;
            CandidateCategory = candidateCategory;
            Target = target;
            Velocity = velocity;
            Effort = effort;
        }

        public string EnvelopeIdentity { get; }

        public string SamplingKey { get; }

        public ExecutionCandidateCategoryV4 CandidateCategory { get; }

        public SimVector3 Target { get; }

        public SimVector3 Velocity { get; }

        public float Effort { get; }

        public bool Equals(ExecutionSampleV4 other)
        {
            return other != null &&
                EnvelopeIdentity == other.EnvelopeIdentity &&
                SamplingKey == other.SamplingKey &&
                CandidateCategory == other.CandidateCategory &&
                Target.Equals(other.Target) &&
                Velocity.Equals(other.Velocity) &&
                Effort.Equals(other.Effort);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ExecutionSampleV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = EnvelopeIdentity == null ? 0 : EnvelopeIdentity.GetHashCode();
                hash = (hash * 397) ^ (SamplingKey == null ? 0 : SamplingKey.GetHashCode());
                hash = (hash * 397) ^ (int)CandidateCategory;
                hash = (hash * 397) ^ Target.GetHashCode();
                hash = (hash * 397) ^ Velocity.GetHashCode();
                return (hash * 397) ^ Effort.GetHashCode();
            }
        }
    }
}
