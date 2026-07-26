using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Volleyball.Domain.Simulation;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public sealed class ExecutionIntentV4 : IEquatable<ExecutionIntentV4>
    {
        public ExecutionIntentV4(
            string identity,
            ExecutionCandidateCategoryV4 candidateCategory,
            SimVector3 baselineTarget,
            SimVector3 baselineVelocity,
            float requestedEffort)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                throw new ArgumentException("Intent identity is required.", nameof(identity));
            }

            if (!Enum.IsDefined(typeof(ExecutionCandidateCategoryV4), candidateCategory))
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCategory));
            }

            if (!baselineTarget.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(baselineTarget));
            }

            if (!baselineVelocity.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(baselineVelocity));
            }

            if (!IsFinite(requestedEffort) || requestedEffort <= 0f || requestedEffort > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedEffort));
            }

            Identity = identity;
            CandidateCategory = candidateCategory;
            BaselineTarget = baselineTarget;
            BaselineVelocity = baselineVelocity;
            RequestedEffort = requestedEffort;
        }

        public string Identity { get; }

        public ExecutionCandidateCategoryV4 CandidateCategory { get; }

        public SimVector3 BaselineTarget { get; }

        public SimVector3 BaselineVelocity { get; }

        public float RequestedEffort { get; }

        public bool Equals(ExecutionIntentV4 other)
        {
            return other != null &&
                Identity == other.Identity &&
                CandidateCategory == other.CandidateCategory &&
                BaselineTarget.Equals(other.BaselineTarget) &&
                BaselineVelocity.Equals(other.BaselineVelocity) &&
                RequestedEffort.Equals(other.RequestedEffort);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ExecutionIntentV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Identity.GetHashCode();
                hash = (hash * 397) ^ (int)CandidateCategory;
                hash = (hash * 397) ^ BaselineTarget.GetHashCode();
                hash = (hash * 397) ^ BaselineVelocity.GetHashCode();
                return (hash * 397) ^ RequestedEffort.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class BoundedErrorDistributionV4 : IEquatable<BoundedErrorDistributionV4>
    {
        internal BoundedErrorDistributionV4(
            BoundedErrorDistributionKindV4 kind,
            SimVector3 minimumError,
            SimVector3 maximumError)
        {
            if (!Enum.IsDefined(typeof(BoundedErrorDistributionKindV4), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (!minimumError.IsFinite || !maximumError.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumError));
            }

            if (minimumError.X > maximumError.X ||
                minimumError.Y > maximumError.Y ||
                minimumError.Z > maximumError.Z)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumError),
                    "Minimum error cannot exceed maximum error.");
            }

            Kind = kind;
            MinimumError = minimumError;
            MaximumError = maximumError;
            MaximumAbsoluteError = new SimVector3(
                Math.Max(Math.Abs(minimumError.X), Math.Abs(maximumError.X)),
                Math.Max(Math.Abs(minimumError.Y), Math.Abs(maximumError.Y)),
                Math.Max(Math.Abs(minimumError.Z), Math.Abs(maximumError.Z)));
        }

        public BoundedErrorDistributionKindV4 Kind { get; }

        public SimVector3 MinimumError { get; }

        public SimVector3 MaximumError { get; }

        public SimVector3 MaximumAbsoluteError { get; }

        public bool Equals(BoundedErrorDistributionV4 other)
        {
            return other != null &&
                Kind == other.Kind &&
                MinimumError.Equals(other.MinimumError) &&
                MaximumError.Equals(other.MaximumError);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BoundedErrorDistributionV4);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ MinimumError.GetHashCode();
                return (hash * 397) ^ MaximumError.GetHashCode();
            }
        }

        internal bool Contains(SimVector3 error, float scale)
        {
            return error.X >= MinimumError.X * scale &&
                error.X <= MaximumError.X * scale &&
                error.Y >= MinimumError.Y * scale &&
                error.Y <= MaximumError.Y * scale &&
                error.Z >= MinimumError.Z * scale &&
                error.Z <= MaximumError.Z * scale;
        }

        internal void AddViolations(
            SimVector3 error,
            float scale,
            string dimensionPrefix,
            ICollection<string> violations)
        {
            AddViolation(
                error.X,
                MinimumError.X * scale,
                MaximumError.X * scale,
                dimensionPrefix + ".x",
                violations);
            AddViolation(
                error.Y,
                MinimumError.Y * scale,
                MaximumError.Y * scale,
                dimensionPrefix + ".y",
                violations);
            AddViolation(
                error.Z,
                MinimumError.Z * scale,
                MaximumError.Z * scale,
                dimensionPrefix + ".z",
                violations);
        }

        internal void AppendCanonical(StringBuilder output, string prefix)
        {
            ExecutionEnvelopeCanonicalV4.AppendEnum(output, prefix + ".kind", Kind);
            ExecutionEnvelopeCanonicalV4.AppendVector(output, prefix + ".minimum", MinimumError);
            ExecutionEnvelopeCanonicalV4.AppendVector(output, prefix + ".maximum", MaximumError);
        }

        private static void AddViolation(
            float value,
            float minimum,
            float maximum,
            string dimension,
            ICollection<string> violations)
        {
            if (value < minimum || value > maximum)
            {
                violations.Add(dimension);
            }
        }
    }

    public sealed class ExecutionEnvelopeV4 : IEquatable<ExecutionEnvelopeV4>
    {
        public const int CurrentVersion = 4;

        private readonly byte[] _canonicalBytes;
        private readonly byte[] _derivedAttributesCanonicalBytes;
        private readonly ExecutionEnvelopePolicyV4 _policy;
        private readonly IReadOnlyList<ExecutionAbilityConsumptionV4>
            _abilityConsumptions;

        internal ExecutionEnvelopeV4(
            int version,
            string derivedAttributesFingerprint,
            byte[] derivedAttributesCanonicalBytes,
            string sourceIntentIdentity,
            ExecutionCandidateCategoryV4 candidateCategory,
            SimVector3 baselineTarget,
            SimVector3 baselineVelocity,
            SimVector3 maximumVelocity,
            BoundedErrorDistributionV4 targetError,
            BoundedErrorDistributionV4 velocityError,
            float requestedEffort,
            float maximumEffort,
            SamplingContractV4 sampling,
            EnvelopeExpansionPolicyV4 expansion,
            ExecutionEnvelopePolicyV4 policy,
            IReadOnlyList<ExecutionAbilityConsumptionV4> abilityConsumptions)
        {
            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (string.IsNullOrWhiteSpace(derivedAttributesFingerprint))
            {
                throw new ArgumentException(
                    "Derived attributes fingerprint is required.",
                    nameof(derivedAttributesFingerprint));
            }

            if (derivedAttributesCanonicalBytes == null ||
                derivedAttributesCanonicalBytes.Length == 0)
            {
                throw new ArgumentException(
                    "Derived attributes canonical bytes are required.",
                    nameof(derivedAttributesCanonicalBytes));
            }

            if (string.IsNullOrWhiteSpace(sourceIntentIdentity))
            {
                throw new ArgumentException(
                    "Source intent identity is required.",
                    nameof(sourceIntentIdentity));
            }

            if (!Enum.IsDefined(typeof(ExecutionCandidateCategoryV4), candidateCategory))
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCategory));
            }

            if (!baselineTarget.IsFinite ||
                !baselineVelocity.IsFinite ||
                !maximumVelocity.IsFinite ||
                maximumVelocity.X <= 0f ||
                maximumVelocity.Y <= 0f ||
                maximumVelocity.Z <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumVelocity));
            }

            if (!IsFinite(requestedEffort) || requestedEffort <= 0f || requestedEffort > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedEffort));
            }

            if (!IsFinite(maximumEffort) || maximumEffort <= 0f || maximumEffort > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEffort));
            }

            Version = version;
            DerivedAttributesFingerprint = derivedAttributesFingerprint;
            _derivedAttributesCanonicalBytes = (byte[])derivedAttributesCanonicalBytes.Clone();
            SourceIntentIdentity = sourceIntentIdentity;
            CandidateCategory = candidateCategory;
            BaselineTarget = baselineTarget;
            BaselineVelocity = baselineVelocity;
            MaximumVelocity = maximumVelocity;
            TargetError = targetError ?? throw new ArgumentNullException(nameof(targetError));
            VelocityError = velocityError ?? throw new ArgumentNullException(nameof(velocityError));
            RequestedEffort = requestedEffort;
            MaximumEffort = maximumEffort;
            Sampling = sampling ?? throw new ArgumentNullException(nameof(sampling));
            Expansion = expansion ?? throw new ArgumentNullException(nameof(expansion));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            if (abilityConsumptions == null || abilityConsumptions.Count == 0)
            {
                throw new ArgumentException(
                    "Execution ability consumptions are required.",
                    nameof(abilityConsumptions));
            }

            var consumptionCopy =
                new ExecutionAbilityConsumptionV4[abilityConsumptions.Count];
            for (var index = 0; index < consumptionCopy.Length; index++)
            {
                consumptionCopy[index] = abilityConsumptions[index] ??
                    throw new ArgumentException(
                        "Execution ability consumptions cannot contain null.",
                        nameof(abilityConsumptions));
            }

            _abilityConsumptions =
                new ReadOnlyCollection<ExecutionAbilityConsumptionV4>(
                    consumptionCopy);

            var canonical = new StringBuilder(2048);
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "schema",
                "volleyball.execution-envelope.v4");
            ExecutionEnvelopeCanonicalV4.AppendInt(canonical, "version", Version);
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "derivedAttributesFingerprint",
                DerivedAttributesFingerprint);
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "derivedAttributesCanonical",
                Convert.ToBase64String(_derivedAttributesCanonicalBytes));
            ExecutionEnvelopeCanonicalV4.AppendString(
                canonical,
                "sourceIntentIdentity",
                SourceIntentIdentity);
            ExecutionEnvelopeCanonicalV4.AppendEnum(
                canonical,
                "candidateCategory",
                CandidateCategory);
            ExecutionEnvelopeCanonicalV4.AppendVector(
                canonical,
                "baselineTarget",
                BaselineTarget);
            ExecutionEnvelopeCanonicalV4.AppendVector(
                canonical,
                "baselineVelocity",
                BaselineVelocity);
            ExecutionEnvelopeCanonicalV4.AppendVector(
                canonical,
                "maximumVelocity",
                MaximumVelocity);
            TargetError.AppendCanonical(canonical, "targetError");
            VelocityError.AppendCanonical(canonical, "velocityError");
            ExecutionEnvelopeCanonicalV4.AppendFloat(
                canonical,
                "requestedEffort",
                RequestedEffort);
            ExecutionEnvelopeCanonicalV4.AppendFloat(
                canonical,
                "maximumEffort",
                MaximumEffort);
            Sampling.AppendCanonical(canonical);
            Expansion.AppendCanonical(canonical);
            _policy.AppendCanonical(canonical);

            _canonicalBytes = Encoding.UTF8.GetBytes(canonical.ToString());
            Identity = ExecutionEnvelopeCanonicalV4.Sha256(_canonicalBytes);
        }

        public int Version { get; }

        public string Identity { get; }

        public string DerivedAttributesFingerprint { get; }

        public string SourceIntentIdentity { get; }

        public ExecutionCandidateCategoryV4 CandidateCategory { get; }

        public SimVector3 BaselineTarget { get; }

        public SimVector3 BaselineVelocity { get; }

        public SimVector3 MaximumVelocity { get; }

        public BoundedErrorDistributionV4 TargetError { get; }

        public BoundedErrorDistributionV4 VelocityError { get; }

        public float RequestedEffort { get; }

        public float MaximumEffort { get; }

        public SamplingContractV4 Sampling { get; }

        public EnvelopeExpansionPolicyV4 Expansion { get; }

        public IReadOnlyList<ExecutionAbilityConsumptionV4>
            AbilityConsumptions => _abilityConsumptions;

        public byte[] ToCanonicalBytes()
        {
            return (byte[])_canonicalBytes.Clone();
        }

        public ExecutionSampleClassificationV4 Classify(ExecutionSampleV4 sample)
        {
            var malformed = MalformedDimensions(sample);
            if (malformed.Count > 0)
            {
                return Classification(
                    ExecutionSampleClassificationKindV4.UnexpectedExecutionSample,
                    sample,
                    malformed);
            }

            var currentViolations = EnvelopeViolations(
                sample,
                Expansion.CurrentExpansionCount);
            if (currentViolations.Count == 0)
            {
                return Classification(
                    ExecutionSampleClassificationKindV4.Accepted,
                    sample,
                    currentViolations);
            }

            var nextExpansionCount = Expansion.CurrentExpansionCount + 1;
            if (Expansion.IsNextExpansionExplicitlyAllowed &&
                EnvelopeViolations(sample, nextExpansionCount).Count == 0)
            {
                var expandedEnvelope = ExecutionEnvelopeFactoryV4.ExpandOneStep(this);
                return Classification(
                    ExecutionSampleClassificationKindV4.EnvelopeExpanded,
                    sample,
                    currentViolations,
                    expandedEnvelope);
            }

            return Classification(
                ExecutionSampleClassificationKindV4.EnvelopeExceeded,
                sample,
                currentViolations);
        }

        public bool Equals(ExecutionEnvelopeV4 other)
        {
            return other != null && Identity == other.Identity;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ExecutionEnvelopeV4);
        }

        public override int GetHashCode()
        {
            return Identity.GetHashCode();
        }

        internal byte[] DerivedAttributesCanonicalBytes =>
            (byte[])_derivedAttributesCanonicalBytes.Clone();

        internal ExecutionEnvelopePolicyV4 Policy => _policy;

        private List<string> MalformedDimensions(ExecutionSampleV4 sample)
        {
            var malformed = new List<string>();
            if (sample == null)
            {
                malformed.Add("sample");
                return malformed;
            }

            if (string.IsNullOrWhiteSpace(sample.EnvelopeIdentity) ||
                sample.EnvelopeIdentity != Identity)
            {
                malformed.Add("envelopeIdentity");
            }

            if (string.IsNullOrWhiteSpace(sample.SamplingKey) ||
                sample.SamplingKey != Sampling.SamplingKey)
            {
                malformed.Add("samplingKey");
            }

            if (!Enum.IsDefined(
                    typeof(ExecutionCandidateCategoryV4),
                    sample.CandidateCategory) ||
                sample.CandidateCategory != CandidateCategory)
            {
                malformed.Add("candidateCategory");
            }

            AddNonFinite(sample.Target.X, "target.x", malformed);
            AddNonFinite(sample.Target.Y, "target.y", malformed);
            AddNonFinite(sample.Target.Z, "target.z", malformed);
            AddNonFinite(sample.Velocity.X, "velocity.x", malformed);
            AddNonFinite(sample.Velocity.Y, "velocity.y", malformed);
            AddNonFinite(sample.Velocity.Z, "velocity.z", malformed);
            AddNonFinite(sample.Effort, "effort", malformed);
            if (IsFinite(sample.Effort) && sample.Effort <= 0f)
            {
                malformed.Add("effort");
            }

            return malformed;
        }

        private List<string> EnvelopeViolations(
            ExecutionSampleV4 sample,
            int expansionCount)
        {
            var violations = new List<string>();
            var scale = ExpansionScale(expansionCount);
            TargetError.AddViolations(
                sample.Target - BaselineTarget,
                scale,
                "target.error",
                violations);
            VelocityError.AddViolations(
                sample.Velocity - BaselineVelocity,
                scale,
                "velocity.error",
                violations);
            AddMaximumVelocityViolations(sample.Velocity, violations);
            if (sample.Effort > MaximumEffort)
            {
                violations.Add("effort.maximum");
            }

            return violations;
        }

        private void AddMaximumVelocityViolations(
            SimVector3 velocity,
            ICollection<string> violations)
        {
            if (Math.Abs(velocity.X) > MaximumVelocity.X)
            {
                violations.Add("velocity.maximum.x");
            }

            if (Math.Abs(velocity.Y) > MaximumVelocity.Y)
            {
                violations.Add("velocity.maximum.y");
            }

            if (Math.Abs(velocity.Z) > MaximumVelocity.Z)
            {
                violations.Add("velocity.maximum.z");
            }
        }

        private float ExpansionScale(int expansionCount)
        {
            var scale = 1f;
            for (var index = 0; index < expansionCount; index++)
            {
                scale *= Expansion.PerStepExpansionFactor;
            }

            return scale;
        }

        private ExecutionSampleClassificationV4 Classification(
            ExecutionSampleClassificationKindV4 kind,
            ExecutionSampleV4 sample,
            IEnumerable<string> offendingDimensions,
            ExecutionEnvelopeV4 expandedEnvelope = null)
        {
            return new ExecutionSampleClassificationV4(
                kind,
                this,
                sample,
                offendingDimensions,
                expandedEnvelope);
        }

        private static void AddNonFinite(
            float value,
            string dimension,
            ICollection<string> malformed)
        {
            if (!IsFinite(value))
            {
                malformed.Add(dimension);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal static class ExecutionEnvelopeCanonicalV4
    {
        public static void AppendString(StringBuilder output, string name, string value)
        {
            var safeValue = value ?? string.Empty;
            output.Append(name.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(name)
                .Append('=')
                .Append(safeValue.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safeValue)
                .Append('\n');
        }

        public static void AppendInt(StringBuilder output, string name, int value)
        {
            AppendString(output, name, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendFloat(StringBuilder output, string name, float value)
        {
            var bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            AppendString(output, name, bits.ToString("x8", CultureInfo.InvariantCulture));
        }

        public static void AppendEnum<T>(StringBuilder output, string name, T value)
            where T : struct
        {
            AppendString(
                output,
                name,
                Convert.ToInt32(value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendVector(StringBuilder output, string name, SimVector3 value)
        {
            AppendFloat(output, name + ".x", value.X);
            AppendFloat(output, name + ".y", value.Y);
            AppendFloat(output, name + ".z", value.Z);
        }

        public static string Sha256(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(bytes);
            var output = new StringBuilder(hash.Length * 2);
            for (var index = 0; index < hash.Length; index++)
            {
                output.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return output.ToString();
        }
    }
}
