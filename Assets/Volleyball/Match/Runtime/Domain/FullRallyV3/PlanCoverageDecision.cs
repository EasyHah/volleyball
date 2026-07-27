using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum PlanCoverageDecisionKind
    {
        CoveredActivateBranch,
        LocalRevision,
        ScopedReplan,
        GlobalReplan,
        TerminalNoPlan
    }

    public enum PlanCoverageReason
    {
        WithinConditionalEnvelope,
        ResponsibleActorChanged,
        BallEnvelopeExceeded,
        EnvelopeExceeded,
        EnvelopeExpanded,
        UnexpectedExecutionSample,
        RulesStateChanged,
        CommittedResponsibilityInvalidated,
        DependencyCascadeExceeded,
        BudgetDegradationRequired,
        RallyOpen,
        RallyEnd
    }

    public sealed class PlanCoverageDecision : IEquatable<PlanCoverageDecision>
    {
        public PlanCoverageDecision(
            PlanCoverageDecisionKind kind,
            string planRevision,
            PlanCoverageReason reason,
            IReadOnlyList<string> invalidationSet,
            int expansionDepth)
        {
            if (expansionDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expansionDepth), "Expansion depth cannot be negative.");
            }

            Kind = kind;
            PlanRevision = Required(planRevision, nameof(planRevision));
            Reason = reason;
            InvalidationSet = new ReadOnlyCollection<string>(CopyInvalidationSet(invalidationSet));
            ExpansionDepth = expansionDepth;
        }

        public PlanCoverageDecisionKind Kind { get; }

        public string PlanRevision { get; }

        public PlanCoverageReason Reason { get; }

        public IReadOnlyList<string> InvalidationSet { get; }

        public int ExpansionDepth { get; }

        public static PlanCoverageDecision Covered(string planRevision, PlanCoverageReason reason)
        {
            return new PlanCoverageDecision(
                PlanCoverageDecisionKind.CoveredActivateBranch,
                planRevision,
                reason,
                Array.Empty<string>(),
                0);
        }

        public bool Equals(PlanCoverageDecision other)
        {
            if (other == null
                || Kind != other.Kind
                || PlanRevision != other.PlanRevision
                || Reason != other.Reason
                || ExpansionDepth != other.ExpansionDepth
                || InvalidationSet.Count != other.InvalidationSet.Count)
            {
                return false;
            }

            for (var index = 0; index < InvalidationSet.Count; index++)
            {
                if (InvalidationSet[index] != other.InvalidationSet[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlanCoverageDecision);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ PlanRevision.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Reason;
                foreach (var invalidation in InvalidationSet)
                {
                    hashCode = (hashCode * 397) ^ invalidation.GetHashCode();
                }

                hashCode = (hashCode * 397) ^ ExpansionDepth;
                return hashCode;
            }
        }

        private static string[] CopyInvalidationSet(IReadOnlyList<string> invalidationSet)
        {
            if (invalidationSet == null)
            {
                return Array.Empty<string>();
            }

            var copy = new string[invalidationSet.Count];
            for (var index = 0; index < invalidationSet.Count; index++)
            {
                copy[index] = Required(invalidationSet[index], nameof(invalidationSet));
            }

            return copy;
        }

        private static string Required(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", paramName);
            }

            return value;
        }
    }
}
