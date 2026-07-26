using System;

namespace Volleyball.Match.Domain.FullRallyV3
{
    /// <summary>
    /// Immutable evidence emitted at the point where the execution-envelope
    /// authority reads a derived ability field.
    /// </summary>
    public sealed class ExecutionAbilityConsumptionV4
    {
        public const string FactoryReadEvidenceKind =
            "ExecutionEnvelopeFactoryRead";

        internal ExecutionAbilityConsumptionV4(
            string attributeName,
            float value)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new ArgumentException(
                    "Consumed attribute name is required.",
                    nameof(attributeName));
            }

            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            AttributeName = attributeName;
            Value = value;
        }

        public string AttributeName { get; }

        public float Value { get; }

        public string EvidenceKind => FactoryReadEvidenceKind;
    }
}
