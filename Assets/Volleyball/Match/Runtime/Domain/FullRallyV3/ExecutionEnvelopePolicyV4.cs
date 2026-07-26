using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum ExecutionCandidateCategoryV4
    {
        Receive,
        Set,
        Attack,
        Block,
        Serve,
        SoftAction,
        Defense
    }

    public enum ExecutionDegradationStepV4
    {
        FullSampling,
        ReducedSampleCount,
        CachedCoarseDistribution,
        DeterministicSafeFallback
    }

    public enum BoundedErrorDistributionKindV4
    {
        BoundedUniform,
        SymmetricTriangular
    }

    public sealed class ExecutionEnvelopePolicyV4 : IEquatable<ExecutionEnvelopePolicyV4>
    {
        private static readonly ExecutionCandidateCategoryV4[] FrozenCandidateOrder =
        {
            ExecutionCandidateCategoryV4.Receive,
            ExecutionCandidateCategoryV4.Set,
            ExecutionCandidateCategoryV4.Attack,
            ExecutionCandidateCategoryV4.Block,
            ExecutionCandidateCategoryV4.Serve
        };

        private static readonly ExecutionDegradationStepV4[] FrozenDegradationLadder =
        {
            ExecutionDegradationStepV4.FullSampling,
            ExecutionDegradationStepV4.ReducedSampleCount,
            ExecutionDegradationStepV4.CachedCoarseDistribution,
            ExecutionDegradationStepV4.DeterministicSafeFallback
        };

        /// <summary>
        /// The frozen Task 7 policy: V4 envelope schema, policy v1, candidate order
        /// Receive/Set/Attack/Block/Serve, seven samples, two possible expansions,
        /// no implicitly authorized expansion, a 1.5x expansion step, and the
        /// full/reduced/cached/safe degradation ladder.
        /// </summary>
        public static ExecutionEnvelopePolicyV4 Default { get; } =
            new ExecutionEnvelopePolicyV4(
                ExecutionEnvelopeV4.CurrentVersion,
                policyVersion: 1,
                FrozenCandidateOrder,
                sampleCount: 7,
                maximumExpansionCount: 2,
                allowedExpansionCount: 0,
                perStepExpansionFactor: 1.5f,
                FrozenDegradationLadder,
                BoundedErrorDistributionKindV4.BoundedUniform,
                BoundedErrorDistributionKindV4.BoundedUniform);

        /// <summary>
        /// Gate I extends the frozen V4 policy with soft-contact and floor-defense
        /// envelopes. The historical default intentionally remains byte-stable.
        /// </summary>
        public static ExecutionEnvelopePolicyV4 GateI { get; } =
            new ExecutionEnvelopePolicyV4(
                ExecutionEnvelopeV4.CurrentVersion,
                policyVersion: 2,
                new[]
                {
                    ExecutionCandidateCategoryV4.Receive,
                    ExecutionCandidateCategoryV4.Set,
                    ExecutionCandidateCategoryV4.Attack,
                    ExecutionCandidateCategoryV4.Block,
                    ExecutionCandidateCategoryV4.Serve,
                    ExecutionCandidateCategoryV4.SoftAction,
                    ExecutionCandidateCategoryV4.Defense
                },
                sampleCount: 7,
                maximumExpansionCount: 2,
                allowedExpansionCount: 0,
                perStepExpansionFactor: 1.5f,
                FrozenDegradationLadder,
                BoundedErrorDistributionKindV4.BoundedUniform,
                BoundedErrorDistributionKindV4.BoundedUniform);

        private readonly IReadOnlyList<ExecutionCandidateCategoryV4> _candidateCategoryOrder;
        private readonly IReadOnlyList<ExecutionDegradationStepV4> _degradationLadder;

        public ExecutionEnvelopePolicyV4(
            int envelopeVersion,
            int policyVersion,
            IEnumerable<ExecutionCandidateCategoryV4> candidateCategoryOrder,
            int sampleCount,
            int maximumExpansionCount,
            int allowedExpansionCount,
            float perStepExpansionFactor,
            IEnumerable<ExecutionDegradationStepV4> degradationLadder,
            BoundedErrorDistributionKindV4 targetDistributionKind,
            BoundedErrorDistributionKindV4 velocityDistributionKind)
        {
            if (envelopeVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(envelopeVersion));
            }

            if (policyVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(policyVersion));
            }

            if (sampleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            if (maximumExpansionCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumExpansionCount));
            }

            if (allowedExpansionCount < 0 || allowedExpansionCount > maximumExpansionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(allowedExpansionCount));
            }

            if (!IsFinite(perStepExpansionFactor) ||
                perStepExpansionFactor < 1f ||
                (maximumExpansionCount > 0 && perStepExpansionFactor <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(perStepExpansionFactor));
            }

            ValidateDefined(targetDistributionKind, nameof(targetDistributionKind));
            ValidateDefined(velocityDistributionKind, nameof(velocityDistributionKind));

            EnvelopeVersion = envelopeVersion;
            PolicyVersion = policyVersion;
            _candidateCategoryOrder = CopyCandidateOrder(candidateCategoryOrder);
            SampleCount = sampleCount;
            MaximumExpansionCount = maximumExpansionCount;
            AllowedExpansionCount = allowedExpansionCount;
            PerStepExpansionFactor = perStepExpansionFactor;
            _degradationLadder = CopyDegradationLadder(degradationLadder);
            TargetDistributionKind = targetDistributionKind;
            VelocityDistributionKind = velocityDistributionKind;
        }

        public int EnvelopeVersion { get; }

        public int PolicyVersion { get; }

        public IReadOnlyList<ExecutionCandidateCategoryV4> CandidateCategoryOrder =>
            _candidateCategoryOrder;

        public int SampleCount { get; }

        public int MaximumExpansionCount { get; }

        public int AllowedExpansionCount { get; }

        public float PerStepExpansionFactor { get; }

        public IReadOnlyList<ExecutionDegradationStepV4> DegradationLadder =>
            _degradationLadder;

        public BoundedErrorDistributionKindV4 TargetDistributionKind { get; }

        public BoundedErrorDistributionKindV4 VelocityDistributionKind { get; }

        public bool ContainsCandidateCategory(ExecutionCandidateCategoryV4 category)
        {
            for (var index = 0; index < _candidateCategoryOrder.Count; index++)
            {
                if (_candidateCategoryOrder[index] == category)
                {
                    return true;
                }
            }

            return false;
        }

        public byte[] ToCanonicalBytes()
        {
            var canonical = new StringBuilder(512);
            AppendCanonical(canonical);
            return Encoding.UTF8.GetBytes(canonical.ToString());
        }

        public bool Equals(ExecutionEnvelopePolicyV4 other)
        {
            if (other == null ||
                EnvelopeVersion != other.EnvelopeVersion ||
                PolicyVersion != other.PolicyVersion ||
                SampleCount != other.SampleCount ||
                MaximumExpansionCount != other.MaximumExpansionCount ||
                AllowedExpansionCount != other.AllowedExpansionCount ||
                !PerStepExpansionFactor.Equals(other.PerStepExpansionFactor) ||
                TargetDistributionKind != other.TargetDistributionKind ||
                VelocityDistributionKind != other.VelocityDistributionKind ||
                _candidateCategoryOrder.Count != other._candidateCategoryOrder.Count ||
                _degradationLadder.Count != other._degradationLadder.Count)
            {
                return false;
            }

            for (var index = 0; index < _candidateCategoryOrder.Count; index++)
            {
                if (_candidateCategoryOrder[index] != other._candidateCategoryOrder[index])
                {
                    return false;
                }
            }

            for (var index = 0; index < _degradationLadder.Count; index++)
            {
                if (_degradationLadder[index] != other._degradationLadder[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ExecutionEnvelopePolicyV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = EnvelopeVersion;
                hash = (hash * 397) ^ PolicyVersion;
                hash = (hash * 397) ^ SampleCount;
                hash = (hash * 397) ^ MaximumExpansionCount;
                hash = (hash * 397) ^ AllowedExpansionCount;
                hash = (hash * 397) ^ PerStepExpansionFactor.GetHashCode();
                hash = (hash * 397) ^ (int)TargetDistributionKind;
                hash = (hash * 397) ^ (int)VelocityDistributionKind;
                for (var index = 0; index < _candidateCategoryOrder.Count; index++)
                {
                    hash = (hash * 397) ^ (int)_candidateCategoryOrder[index];
                }

                for (var index = 0; index < _degradationLadder.Count; index++)
                {
                    hash = (hash * 397) ^ (int)_degradationLadder[index];
                }

                return hash;
            }
        }

        internal void AppendCanonical(StringBuilder output)
        {
            ExecutionEnvelopeCanonicalV4.AppendInt(output, "policy.envelopeVersion", EnvelopeVersion);
            ExecutionEnvelopeCanonicalV4.AppendInt(output, "policy.version", PolicyVersion);
            ExecutionEnvelopeCanonicalV4.AppendInt(output, "policy.sampleCount", SampleCount);
            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "policy.maximumExpansionCount",
                MaximumExpansionCount);
            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "policy.allowedExpansionCount",
                AllowedExpansionCount);
            ExecutionEnvelopeCanonicalV4.AppendFloat(
                output,
                "policy.perStepExpansionFactor",
                PerStepExpansionFactor);
            ExecutionEnvelopeCanonicalV4.AppendEnum(
                output,
                "policy.targetDistributionKind",
                TargetDistributionKind);
            ExecutionEnvelopeCanonicalV4.AppendEnum(
                output,
                "policy.velocityDistributionKind",
                VelocityDistributionKind);

            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "policy.candidateCategoryOrder.count",
                _candidateCategoryOrder.Count);
            for (var index = 0; index < _candidateCategoryOrder.Count; index++)
            {
                ExecutionEnvelopeCanonicalV4.AppendEnum(
                    output,
                    "policy.candidateCategoryOrder." + index,
                    _candidateCategoryOrder[index]);
            }

            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "policy.degradationLadder.count",
                _degradationLadder.Count);
            for (var index = 0; index < _degradationLadder.Count; index++)
            {
                ExecutionEnvelopeCanonicalV4.AppendEnum(
                    output,
                    "policy.degradationLadder." + index,
                    _degradationLadder[index]);
            }
        }

        private static IReadOnlyList<ExecutionCandidateCategoryV4> CopyCandidateOrder(
            IEnumerable<ExecutionCandidateCategoryV4> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<ExecutionCandidateCategoryV4>(source);
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "Candidate order cannot be empty.",
                    nameof(source));
            }

            var unique = new HashSet<ExecutionCandidateCategoryV4>();
            for (var index = 0; index < copy.Count; index++)
            {
                ValidateDefined(copy[index], nameof(source));
                if (!unique.Add(copy[index]))
                {
                    throw new ArgumentException(
                        "Candidate order cannot contain duplicate categories.",
                        nameof(source));
                }
            }

            return new ReadOnlyCollection<ExecutionCandidateCategoryV4>(copy);
        }

        private static IReadOnlyList<ExecutionDegradationStepV4> CopyDegradationLadder(
            IEnumerable<ExecutionDegradationStepV4> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<ExecutionDegradationStepV4>(source);
            if (copy.Count == 0 ||
                copy[0] != ExecutionDegradationStepV4.FullSampling ||
                copy[copy.Count - 1] != ExecutionDegradationStepV4.DeterministicSafeFallback)
            {
                throw new ArgumentException(
                    "Degradation must start at full sampling and end at the deterministic safe fallback.",
                    nameof(source));
            }

            var unique = new HashSet<ExecutionDegradationStepV4>();
            for (var index = 0; index < copy.Count; index++)
            {
                ValidateDefined(copy[index], nameof(source));
                if (!unique.Add(copy[index]))
                {
                    throw new ArgumentException(
                        "Degradation ladder cannot contain duplicate steps.",
                        nameof(source));
                }
            }

            return new ReadOnlyCollection<ExecutionDegradationStepV4>(copy);
        }

        private static void ValidateDefined<T>(T value, string parameterName)
            where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class SamplingContractV4 : IEquatable<SamplingContractV4>
    {
        private readonly IReadOnlyList<ExecutionCandidateCategoryV4> _candidateCategoryOrder;
        private readonly IReadOnlyList<ExecutionDegradationStepV4> _degradationLadder;

        internal SamplingContractV4(string samplingKey, ExecutionEnvelopePolicyV4 policy)
        {
            if (string.IsNullOrWhiteSpace(samplingKey))
            {
                throw new ArgumentException("Sampling key is required.", nameof(samplingKey));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            SamplingKey = samplingKey;
            PolicyVersion = policy.PolicyVersion;
            SampleCount = policy.SampleCount;
            _candidateCategoryOrder = Copy(policy.CandidateCategoryOrder);
            _degradationLadder = Copy(policy.DegradationLadder);
        }

        public string SamplingKey { get; }

        public int PolicyVersion { get; }

        public int SampleCount { get; }

        public IReadOnlyList<ExecutionCandidateCategoryV4> CandidateCategoryOrder =>
            _candidateCategoryOrder;

        public IReadOnlyList<ExecutionDegradationStepV4> DegradationLadder =>
            _degradationLadder;

        public bool Equals(SamplingContractV4 other)
        {
            if (other == null ||
                SamplingKey != other.SamplingKey ||
                PolicyVersion != other.PolicyVersion ||
                SampleCount != other.SampleCount ||
                _candidateCategoryOrder.Count != other._candidateCategoryOrder.Count ||
                _degradationLadder.Count != other._degradationLadder.Count)
            {
                return false;
            }

            for (var index = 0; index < _candidateCategoryOrder.Count; index++)
            {
                if (_candidateCategoryOrder[index] != other._candidateCategoryOrder[index])
                {
                    return false;
                }
            }

            for (var index = 0; index < _degradationLadder.Count; index++)
            {
                if (_degradationLadder[index] != other._degradationLadder[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SamplingContractV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SamplingKey.GetHashCode();
                hash = (hash * 397) ^ PolicyVersion;
                hash = (hash * 397) ^ SampleCount;
                for (var index = 0; index < _candidateCategoryOrder.Count; index++)
                {
                    hash = (hash * 397) ^ (int)_candidateCategoryOrder[index];
                }

                for (var index = 0; index < _degradationLadder.Count; index++)
                {
                    hash = (hash * 397) ^ (int)_degradationLadder[index];
                }

                return hash;
            }
        }

        internal void AppendCanonical(StringBuilder output)
        {
            ExecutionEnvelopeCanonicalV4.AppendString(output, "sampling.key", SamplingKey);
            ExecutionEnvelopeCanonicalV4.AppendInt(output, "sampling.policyVersion", PolicyVersion);
            ExecutionEnvelopeCanonicalV4.AppendInt(output, "sampling.sampleCount", SampleCount);
            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "sampling.candidateCategoryOrder.count",
                _candidateCategoryOrder.Count);
            for (var index = 0; index < _candidateCategoryOrder.Count; index++)
            {
                ExecutionEnvelopeCanonicalV4.AppendEnum(
                    output,
                    "sampling.candidateCategoryOrder." + index,
                    _candidateCategoryOrder[index]);
            }

            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "sampling.degradationLadder.count",
                _degradationLadder.Count);
            for (var index = 0; index < _degradationLadder.Count; index++)
            {
                ExecutionEnvelopeCanonicalV4.AppendEnum(
                    output,
                    "sampling.degradationLadder." + index,
                    _degradationLadder[index]);
            }
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    public sealed class EnvelopeExpansionPolicyV4 : IEquatable<EnvelopeExpansionPolicyV4>
    {
        internal EnvelopeExpansionPolicyV4(
            int maximumExpansionCount,
            int allowedExpansionCount,
            int currentExpansionCount,
            float perStepExpansionFactor)
        {
            MaximumExpansionCount = maximumExpansionCount;
            AllowedExpansionCount = allowedExpansionCount;
            CurrentExpansionCount = currentExpansionCount;
            PerStepExpansionFactor = perStepExpansionFactor;
        }

        public int MaximumExpansionCount { get; }

        public int AllowedExpansionCount { get; }

        public int CurrentExpansionCount { get; }

        public float PerStepExpansionFactor { get; }

        public bool IsNextExpansionExplicitlyAllowed =>
            CurrentExpansionCount < MaximumExpansionCount &&
            CurrentExpansionCount < AllowedExpansionCount;

        public bool Equals(EnvelopeExpansionPolicyV4 other)
        {
            return other != null &&
                MaximumExpansionCount == other.MaximumExpansionCount &&
                AllowedExpansionCount == other.AllowedExpansionCount &&
                CurrentExpansionCount == other.CurrentExpansionCount &&
                PerStepExpansionFactor.Equals(other.PerStepExpansionFactor);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EnvelopeExpansionPolicyV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = MaximumExpansionCount;
                hash = (hash * 397) ^ AllowedExpansionCount;
                hash = (hash * 397) ^ CurrentExpansionCount;
                return (hash * 397) ^ PerStepExpansionFactor.GetHashCode();
            }
        }

        internal void AppendCanonical(StringBuilder output)
        {
            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "expansion.maximumCount",
                MaximumExpansionCount);
            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "expansion.allowedCount",
                AllowedExpansionCount);
            ExecutionEnvelopeCanonicalV4.AppendInt(
                output,
                "expansion.currentCount",
                CurrentExpansionCount);
            ExecutionEnvelopeCanonicalV4.AppendFloat(
                output,
                "expansion.perStepFactor",
                PerStepExpansionFactor);
        }
    }
}
