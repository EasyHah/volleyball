using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Shared.Contracts
{
    public sealed class ReplayVector3RecordV4
    {
        public ReplayVector3RecordV4(float x, float y, float z)
        {
            X = ReplayContractGuardV4.Finite(x, nameof(x));
            Y = ReplayContractGuardV4.Finite(y, nameof(y));
            Z = ReplayContractGuardV4.Finite(z, nameof(z));
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public sealed class ReplayBoundedErrorRecordV4
    {
        public ReplayBoundedErrorRecordV4(
            string distributionKind,
            ReplayVector3RecordV4 minimum,
            ReplayVector3RecordV4 maximum)
        {
            DistributionKind = ReplayContractGuardV4.OneOf(
                distributionKind,
                nameof(distributionKind),
                "BoundedUniform",
                "SymmetricTriangular");
            Minimum = minimum ??
                throw new ContractValidationException("minimum is required.");
            Maximum = maximum ??
                throw new ContractValidationException("maximum is required.");
            if (Minimum.X > Maximum.X ||
                Minimum.Y > Maximum.Y ||
                Minimum.Z > Maximum.Z)
            {
                throw new ContractValidationException(
                    "Bounded error minimum cannot exceed maximum.");
            }
        }

        public string DistributionKind { get; }
        public ReplayVector3RecordV4 Minimum { get; }
        public ReplayVector3RecordV4 Maximum { get; }
    }

    public sealed class ReplayExecutionEnvelopeRecordV4
    {
        private readonly string[] _candidateCategoryOrder;
        private readonly string[] _degradationLadder;

        public ReplayExecutionEnvelopeRecordV4(
            int version,
            string identity,
            string derivedAttributesFingerprint,
            string policyIdentity,
            string sourceIntentIdentity,
            string candidateCategory,
            ReplayVector3RecordV4 baselineTarget,
            ReplayVector3RecordV4 baselineVelocity,
            ReplayVector3RecordV4 maximumVelocity,
            ReplayBoundedErrorRecordV4 targetError,
            ReplayBoundedErrorRecordV4 velocityError,
            float requestedEffort,
            float maximumEffort,
            string samplingKey,
            int policyVersion,
            int sampleCount,
            IReadOnlyList<string> candidateCategoryOrder,
            IReadOnlyList<string> degradationLadder,
            int maximumExpansionCount,
            int allowedExpansionCount,
            int currentExpansionCount,
            float perStepExpansionFactor)
        {
            if (version != ContractVersions.ReplayV4)
            {
                throw new ContractValidationException(
                    "Execution envelope version must be 4.");
            }

            Version = version;
            Identity = ReplayContractGuardV4.Hash(identity, nameof(identity));
            DerivedAttributesFingerprint = ReplayContractGuardV4.Hash(
                derivedAttributesFingerprint,
                nameof(derivedAttributesFingerprint));
            PolicyIdentity = ReplayContractGuardV4.Hash(
                policyIdentity,
                nameof(policyIdentity));
            SourceIntentIdentity = ReplayContractGuardV4.Required(
                sourceIntentIdentity,
                nameof(sourceIntentIdentity));
            CandidateCategory = ReplayContractGuardV4.EventKind(
                candidateCategory,
                nameof(candidateCategory));
            BaselineTarget = baselineTarget ??
                throw new ContractValidationException("baselineTarget is required.");
            BaselineVelocity = baselineVelocity ??
                throw new ContractValidationException("baselineVelocity is required.");
            MaximumVelocity = maximumVelocity ??
                throw new ContractValidationException("maximumVelocity is required.");
            if (MaximumVelocity.X < 0f ||
                MaximumVelocity.Y < 0f ||
                MaximumVelocity.Z < 0f)
            {
                throw new ContractValidationException(
                    "maximumVelocity components must be non-negative.");
            }

            TargetError = targetError ??
                throw new ContractValidationException("targetError is required.");
            VelocityError = velocityError ??
                throw new ContractValidationException("velocityError is required.");
            RequestedEffort = ReplayContractGuardV4.PositiveUnit(
                requestedEffort,
                nameof(requestedEffort));
            MaximumEffort = ReplayContractGuardV4.PositiveUnit(
                maximumEffort,
                nameof(maximumEffort));
            if (RequestedEffort > MaximumEffort)
            {
                throw new ContractValidationException(
                    "requestedEffort cannot exceed maximumEffort.");
            }

            SamplingKey = ReplayContractGuardV4.Required(
                samplingKey,
                nameof(samplingKey));
            PolicyVersion = ReplayContractGuardV4.Positive(
                policyVersion,
                nameof(policyVersion));
            SampleCount = ReplayContractGuardV4.Positive(
                sampleCount,
                nameof(sampleCount));
            _candidateCategoryOrder = CopyRequired(
                candidateCategoryOrder,
                nameof(candidateCategoryOrder),
                ReplayContractGuardV4.EventKind);
            _degradationLadder = CopyRequired(
                degradationLadder,
                nameof(degradationLadder),
                ReplayContractGuardV4.DegradationStep);
            MaximumExpansionCount = ReplayContractGuardV4.NonNegative(
                maximumExpansionCount,
                nameof(maximumExpansionCount));
            AllowedExpansionCount = ReplayContractGuardV4.NonNegative(
                allowedExpansionCount,
                nameof(allowedExpansionCount));
            CurrentExpansionCount = ReplayContractGuardV4.NonNegative(
                currentExpansionCount,
                nameof(currentExpansionCount));
            if (AllowedExpansionCount > MaximumExpansionCount ||
                CurrentExpansionCount > AllowedExpansionCount)
            {
                throw new ContractValidationException(
                    "Expansion counts must satisfy current <= allowed <= maximum.");
            }

            PerStepExpansionFactor = ReplayContractGuardV4.Finite(
                perStepExpansionFactor,
                nameof(perStepExpansionFactor));
            if (PerStepExpansionFactor < 1f ||
                (MaximumExpansionCount > 0 && PerStepExpansionFactor <= 1f))
            {
                throw new ContractValidationException(
                    "perStepExpansionFactor is invalid.");
            }
        }

        public int Version { get; }
        public string Identity { get; }
        public string DerivedAttributesFingerprint { get; }
        public string PolicyIdentity { get; }
        public string SourceIntentIdentity { get; }
        public string CandidateCategory { get; }
        public ReplayVector3RecordV4 BaselineTarget { get; }
        public ReplayVector3RecordV4 BaselineVelocity { get; }
        public ReplayVector3RecordV4 MaximumVelocity { get; }
        public ReplayBoundedErrorRecordV4 TargetError { get; }
        public ReplayBoundedErrorRecordV4 VelocityError { get; }
        public float RequestedEffort { get; }
        public float MaximumEffort { get; }
        public string SamplingKey { get; }
        public int PolicyVersion { get; }
        public int SampleCount { get; }
        public IReadOnlyList<string> CandidateCategoryOrder =>
            new ReadOnlyCollection<string>(_candidateCategoryOrder);
        public IReadOnlyList<string> DegradationLadder =>
            new ReadOnlyCollection<string>(_degradationLadder);
        public int MaximumExpansionCount { get; }
        public int AllowedExpansionCount { get; }
        public int CurrentExpansionCount { get; }
        public float PerStepExpansionFactor { get; }

        private static string[] CopyRequired(
            IReadOnlyList<string> source,
            string name,
            Func<string, string, string> validate)
        {
            if (source == null || source.Count == 0)
            {
                throw new ContractValidationException(name + " is required.");
            }

            var copy = new string[source.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = validate(source[index], name + "[" + index + "]");
                if (!seen.Add(copy[index]))
                {
                    throw new ContractValidationException(
                        name + " must not contain duplicates.");
                }
            }

            return copy;
        }
    }
}
