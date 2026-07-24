using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum ExecutionSampleClassification
    {
        WithinEnvelope,
        EnvelopeExceeded,
        EnvelopeExpanded,
        UnexpectedExecutionSample
    }

    public readonly struct EnvelopeBoundsV3
    {
        public EnvelopeBoundsV3(
            float minTargetDeviationMeters,
            float maxTargetDeviationMeters,
            float minVelocityScale,
            float maxVelocityScale,
            float maxEffort)
        {
            if (float.IsNaN(minTargetDeviationMeters) || float.IsInfinity(minTargetDeviationMeters))
                throw new ArgumentOutOfRangeException(nameof(minTargetDeviationMeters));
            if (float.IsNaN(maxTargetDeviationMeters) || float.IsInfinity(maxTargetDeviationMeters))
                throw new ArgumentOutOfRangeException(nameof(maxTargetDeviationMeters));
            if (minTargetDeviationMeters > maxTargetDeviationMeters)
                throw new ArgumentOutOfRangeException(nameof(minTargetDeviationMeters), "Min target deviation cannot exceed max.");
            if (float.IsNaN(minVelocityScale) || float.IsInfinity(minVelocityScale) || minVelocityScale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minVelocityScale), "Min velocity scale must be positive.");
            if (float.IsNaN(maxVelocityScale) || float.IsInfinity(maxVelocityScale) || maxVelocityScale < minVelocityScale)
                throw new ArgumentOutOfRangeException(nameof(maxVelocityScale), "Max velocity scale must be >= min.");
            if (float.IsNaN(maxEffort) || float.IsInfinity(maxEffort) || maxEffort <= 0f || maxEffort > 1f)
                throw new ArgumentOutOfRangeException(nameof(maxEffort), "Max effort must be in the range (0, 1].");

            MinTargetDeviationMeters = minTargetDeviationMeters;
            MaxTargetDeviationMeters = maxTargetDeviationMeters;
            MinVelocityScale = minVelocityScale;
            MaxVelocityScale = maxVelocityScale;
            MaxEffort = maxEffort;
        }

        public float MinTargetDeviationMeters { get; }
        public float MaxTargetDeviationMeters { get; }
        public float MinVelocityScale { get; }
        public float MaxVelocityScale { get; }
        public float MaxEffort { get; }

        public bool Contains(ExecutionSampleV3 sample)
        {
            if (sample == null) return false;
            var deviation = sample.TargetDeviation.Magnitude;
            if (deviation < MinTargetDeviationMeters || deviation > MaxTargetDeviationMeters)
                return false;
            return sample.VelocityScale >= MinVelocityScale && sample.VelocityScale <= MaxVelocityScale;
        }
    }

    public sealed class ExecutionSampleV3 : IEquatable<ExecutionSampleV3>
    {
        public ExecutionSampleV3(
            string sampleKey,
            Volleyball.Domain.Simulation.SimVector3 targetDeviation,
            float velocityScale,
            float effort,
            string sampleClass)
        {
            SampleKey = Required(sampleKey, nameof(sampleKey));
            TargetDeviation = RequireFinite(targetDeviation, nameof(targetDeviation));
            if (float.IsNaN(velocityScale) || float.IsInfinity(velocityScale) || velocityScale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(velocityScale), "Velocity scale must be positive.");
            VelocityScale = velocityScale;
            if (float.IsNaN(effort) || float.IsInfinity(effort) || effort <= 0f || effort > 1f)
                throw new ArgumentOutOfRangeException(nameof(effort), "Effort must be in the range (0, 1].");
            Effort = effort;
            SampleClass = Required(sampleClass, nameof(sampleClass));
        }

        public string SampleKey { get; }

        public Volleyball.Domain.Simulation.SimVector3 TargetDeviation { get; }

        public float VelocityScale { get; }

        public float Effort { get; }

        public string SampleClass { get; }

        public bool Equals(ExecutionSampleV3 other)
        {
            return other != null
                && SampleKey == other.SampleKey
                && TargetDeviation.Equals(other.TargetDeviation)
                && VelocityScale.Equals(other.VelocityScale)
                && Effort.Equals(other.Effort)
                && SampleClass == other.SampleClass;
        }

        public override bool Equals(object obj) => Equals(obj as ExecutionSampleV3);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SampleKey.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetDeviation.GetHashCode();
                hashCode = (hashCode * 397) ^ VelocityScale.GetHashCode();
                hashCode = (hashCode * 397) ^ Effort.GetHashCode();
                hashCode = (hashCode * 397) ^ SampleClass.GetHashCode();
                return hashCode;
            }
        }

        private static Volleyball.Domain.Simulation.SimVector3 RequireFinite(Volleyball.Domain.Simulation.SimVector3 value, string paramName)
        {
            if (!value.IsFinite) throw new ArgumentOutOfRangeException(paramName, "Vector must be finite.");
            return value;
        }

        private static string Required(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", paramName);
            return value;
        }
    }

    public sealed class ExecutionEnvelopeV3 : IEquatable<ExecutionEnvelopeV3>
    {
        private readonly string _version;
        private readonly string _abilitySnapshotHash;
        private readonly string _abilitySnapshotProvenance;
        private readonly string _actionKind;
        private readonly string _baselineTargetKey;
        private readonly string _distributionKey;
        private readonly string _deterministicSampleKey;
        private readonly Volleyball.Domain.Simulation.SimVector3 _baselineTarget;
        private readonly Volleyball.Domain.Simulation.SimVector3 _baselineVelocity;
        private readonly EnvelopeBoundsV3 _bounds;
        private readonly float _effort;
        private readonly IReadOnlyList<ExecutionSampleV3> _samples;
        private readonly string _provenance;
        private readonly ExecutionSampleClassification? _lastSampleClassification;

        public ExecutionEnvelopeV3(
            string version,
            string abilitySnapshotHash,
            string abilitySnapshotProvenance,
            string actionKind,
            string baselineTargetKey,
            string distributionKey,
            string deterministicSampleKey)
        {
            _version = Required(version, nameof(version));
            _abilitySnapshotHash = Required(abilitySnapshotHash, nameof(abilitySnapshotHash));
            _abilitySnapshotProvenance = Required(abilitySnapshotProvenance, nameof(abilitySnapshotProvenance));
            _actionKind = Required(actionKind, nameof(actionKind));
            _baselineTargetKey = Required(baselineTargetKey, nameof(baselineTargetKey));
            _distributionKey = Required(distributionKey, nameof(distributionKey));
            _deterministicSampleKey = Required(deterministicSampleKey, nameof(deterministicSampleKey));
            _baselineTarget = Volleyball.Domain.Simulation.SimVector3.Zero;
            _baselineVelocity = Volleyball.Domain.Simulation.SimVector3.Zero;
            _bounds = default;
            _effort = 1f;
            _samples = new ReadOnlyCollection<ExecutionSampleV3>(Array.Empty<ExecutionSampleV3>());
            _provenance = string.Empty;
            _lastSampleClassification = null;
        }

        public ExecutionEnvelopeV3(
            string version,
            string abilitySnapshotHash,
            string abilitySnapshotProvenance,
            string actionKind,
            string baselineTargetKey,
            string distributionKey,
            string deterministicSampleKey,
            Volleyball.Domain.Simulation.SimVector3 baselineTarget,
            Volleyball.Domain.Simulation.SimVector3 baselineVelocity,
            EnvelopeBoundsV3 bounds,
            float effort,
            IReadOnlyList<ExecutionSampleV3> samples,
            string provenance,
            ExecutionSampleClassification? lastSampleClassification)
        {
            _version = Required(version, nameof(version));
            _abilitySnapshotHash = Required(abilitySnapshotHash, nameof(abilitySnapshotHash));
            _abilitySnapshotProvenance = Required(abilitySnapshotProvenance, nameof(abilitySnapshotProvenance));
            _actionKind = Required(actionKind, nameof(actionKind));
            _baselineTargetKey = Required(baselineTargetKey, nameof(baselineTargetKey));
            _distributionKey = Required(distributionKey, nameof(distributionKey));
            _deterministicSampleKey = Required(deterministicSampleKey, nameof(deterministicSampleKey));
            _baselineTarget = RequireFinite(baselineTarget, nameof(baselineTarget));
            _baselineVelocity = RequireFinite(baselineVelocity, nameof(baselineVelocity));
            _bounds = bounds;
            if (float.IsNaN(effort) || float.IsInfinity(effort) || effort <= 0f || effort > 1f)
                throw new ArgumentOutOfRangeException(nameof(effort), "Effort must be in the range (0, 1].");
            _effort = effort;
            _samples = CopySamples(samples);
            _provenance = Required(provenance, nameof(provenance));
            _lastSampleClassification = lastSampleClassification;
        }

        public string Version => _version;

        public string AbilitySnapshotHash => _abilitySnapshotHash;

        public string AbilitySnapshotProvenance => _abilitySnapshotProvenance;

        public string ActionKind => _actionKind;

        public string BaselineTargetKey => _baselineTargetKey;

        public string DistributionKey => _distributionKey;

        public string DeterministicSampleKey => _deterministicSampleKey;

        public Volleyball.Domain.Simulation.SimVector3 BaselineTarget => _baselineTarget;

        public Volleyball.Domain.Simulation.SimVector3 BaselineVelocity => _baselineVelocity;

        public EnvelopeBoundsV3 Bounds => _bounds;

        public float Effort => _effort;

        public IReadOnlyList<ExecutionSampleV3> Samples => _samples;

        public string Provenance => _provenance;

        public ExecutionSampleClassification? LastSampleClassification => _lastSampleClassification;

        public bool IsExpanded => _lastSampleClassification == ExecutionSampleClassification.EnvelopeExpanded;

        public bool IsExceeded => _lastSampleClassification == ExecutionSampleClassification.EnvelopeExceeded;

        public ExecutionSampleClassification ClassifySample(ExecutionSampleV3 sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));
            if (sample.SampleKey != _deterministicSampleKey)
                return ExecutionSampleClassification.UnexpectedExecutionSample;
            if (_bounds.Contains(sample))
                return ExecutionSampleClassification.WithinEnvelope;
            var deviation = sample.TargetDeviation.Magnitude;
            if (deviation > _bounds.MaxTargetDeviationMeters)
                return ExecutionSampleClassification.EnvelopeExceeded;
            return ExecutionSampleClassification.EnvelopeExpanded;
        }

        public bool Equals(ExecutionEnvelopeV3 other)
        {
            return other != null
                && Version == other.Version
                && AbilitySnapshotHash == other.AbilitySnapshotHash
                && AbilitySnapshotProvenance == other.AbilitySnapshotProvenance
                && ActionKind == other.ActionKind
                && BaselineTargetKey == other.BaselineTargetKey
                && DistributionKey == other.DistributionKey
                && DeterministicSampleKey == other.DeterministicSampleKey;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ExecutionEnvelopeV3);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Version.GetHashCode();
                hashCode = (hashCode * 397) ^ AbilitySnapshotHash.GetHashCode();
                hashCode = (hashCode * 397) ^ AbilitySnapshotProvenance.GetHashCode();
                hashCode = (hashCode * 397) ^ ActionKind.GetHashCode();
                hashCode = (hashCode * 397) ^ BaselineTargetKey.GetHashCode();
                hashCode = (hashCode * 397) ^ DistributionKey.GetHashCode();
                hashCode = (hashCode * 397) ^ DeterministicSampleKey.GetHashCode();
                return hashCode;
            }
        }

        private static Volleyball.Domain.Simulation.SimVector3 RequireFinite(Volleyball.Domain.Simulation.SimVector3 value, string paramName)
        {
            if (!value.IsFinite) throw new ArgumentOutOfRangeException(paramName, "Vector must be finite.");
            return value;
        }

        private static IReadOnlyList<ExecutionSampleV3> CopySamples(IReadOnlyList<ExecutionSampleV3> samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var copy = new ExecutionSampleV3[samples.Count];
            for (var index = 0; index < samples.Count; index++)
            {
                copy[index] = samples[index] ?? throw new ArgumentException("Samples cannot contain null.", nameof(samples));
            }
            return new ReadOnlyCollection<ExecutionSampleV3>(copy);
        }

        private static string Required(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", paramName);
            return value;
        }
    }
}
