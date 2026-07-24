using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum ExecutionSampleClassificationKindV4
    {
        UnexpectedExecutionSample,
        Accepted,
        EnvelopeExpanded,
        EnvelopeExceeded
    }

    public sealed class ExecutionSampleClassificationV4
    {
        private readonly IReadOnlyList<string> _offendingDimensions;

        internal ExecutionSampleClassificationV4(
            ExecutionSampleClassificationKindV4 kind,
            ExecutionEnvelopeV4 testedEnvelope,
            ExecutionSampleV4 sample,
            IEnumerable<string> offendingDimensions,
            ExecutionEnvelopeV4 expandedEnvelope)
        {
            if (!Enum.IsDefined(typeof(ExecutionSampleClassificationKindV4), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Kind = kind;
            TestedEnvelope = testedEnvelope ?? throw new ArgumentNullException(nameof(testedEnvelope));
            Sample = sample;
            _offendingDimensions = new ReadOnlyCollection<string>(
                new List<string>(offendingDimensions ?? throw new ArgumentNullException(nameof(offendingDimensions))));
            ExpandedEnvelope = expandedEnvelope;
        }

        public ExecutionSampleClassificationKindV4 Kind { get; }

        public ExecutionEnvelopeV4 TestedEnvelope { get; }

        public string TestedEnvelopeIdentity => TestedEnvelope.Identity;

        public ExecutionSampleV4 Sample { get; }

        public IReadOnlyList<string> OffendingDimensions => _offendingDimensions;

        public ExecutionEnvelopeV4 ExpandedEnvelope { get; }

        public string ExpandedEnvelopeIdentity => ExpandedEnvelope?.Identity;
    }
}
