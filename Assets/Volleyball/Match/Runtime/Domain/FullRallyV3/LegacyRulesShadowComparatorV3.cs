using System;
using System.Globalization;

namespace Volleyball.Match.Domain.FullRallyV3
{
    public enum LegacyRuleDispositionV3
    {
        Ignore,
        Accept,
        Fault
    }

    public enum ShadowScenarioV3
    {
        Other,
        BlockerFirstCountedContact,
        IncidentalCountedContact
    }

    public enum RulesShadowDifferenceKindV3
    {
        ExactParity,
        IntentionalV3Correction,
        UnexpectedMismatch
    }

    public sealed class LegacyRuleOutcomeV3
    {
        public LegacyRuleOutcomeV3(LegacyRuleDispositionV3 disposition, string reason)
        {
            if (!Enum.IsDefined(typeof(LegacyRuleDispositionV3), disposition))
            {
                throw new ArgumentOutOfRangeException(nameof(disposition), "Legacy disposition is not supported.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Legacy reason is required.", nameof(reason));
            }

            Disposition = disposition;
            Reason = reason;
        }

        public LegacyRuleDispositionV3 Disposition { get; }

        public string Reason { get; }

        public static LegacyRuleOutcomeV3 Accept()
        {
            return new LegacyRuleOutcomeV3(LegacyRuleDispositionV3.Accept, "None");
        }

        public static LegacyRuleOutcomeV3 Ignore(string reason)
        {
            return new LegacyRuleOutcomeV3(LegacyRuleDispositionV3.Ignore, reason);
        }

        public static LegacyRuleOutcomeV3 Fault(string reason)
        {
            return new LegacyRuleOutcomeV3(LegacyRuleDispositionV3.Fault, reason);
        }
    }

    public sealed class RulesShadowComparisonV3
    {
        internal RulesShadowComparisonV3(RulesShadowDifferenceKindV3 differenceKind, string diagnostic)
        {
            DifferenceKind = differenceKind;
            Diagnostic = diagnostic;
        }

        public RulesShadowDifferenceKindV3 DifferenceKind { get; }

        public bool IsParity => DifferenceKind == RulesShadowDifferenceKindV3.ExactParity;

        public string Diagnostic { get; }
    }

    public static class LegacyRulesShadowComparatorV3
    {
        public static RulesShadowComparisonV3 Compare(
            LegacyRuleOutcomeV3 legacyOutcome,
            RuleTransitionV3 v3Transition,
            ShadowScenarioV3 scenario)
        {
            if (legacyOutcome == null)
            {
                throw new ArgumentNullException(nameof(legacyOutcome));
            }

            if (v3Transition == null)
            {
                throw new ArgumentNullException(nameof(v3Transition));
            }

            if (!Enum.IsDefined(typeof(ShadowScenarioV3), scenario))
            {
                throw new ArgumentOutOfRangeException(nameof(scenario), "Shadow scenario is not supported.");
            }

            var differenceKind = IsExactParity(legacyOutcome, v3Transition)
                ? RulesShadowDifferenceKindV3.ExactParity
                : IsIntentionalV3Correction(legacyOutcome, v3Transition, scenario)
                    ? RulesShadowDifferenceKindV3.IntentionalV3Correction
                    : RulesShadowDifferenceKindV3.UnexpectedMismatch;

            return new RulesShadowComparisonV3(
                differenceKind,
                CreateDiagnostic(legacyOutcome, v3Transition, scenario));
        }

        private static bool IsExactParity(LegacyRuleOutcomeV3 legacyOutcome, RuleTransitionV3 v3Transition)
        {
            var expectedLegacyOutcome = ExpectedLegacyOutcome(v3Transition);
            return legacyOutcome.Disposition == expectedLegacyOutcome.Disposition
                && legacyOutcome.Reason == expectedLegacyOutcome.Reason;
        }

        private static LegacyRuleOutcomeV3 ExpectedLegacyOutcome(RuleTransitionV3 v3Transition)
        {
            if (v3Transition.Accepted)
            {
                return LegacyRuleOutcomeV3.Accept();
            }

            switch (v3Transition.RejectionReason)
            {
                case RuleRejectionReasonV3.DuplicateContactGroup:
                    return LegacyRuleOutcomeV3.Ignore("DuplicateContactGroup");
                case RuleRejectionReasonV3.RallyClosed:
                    return LegacyRuleOutcomeV3.Ignore("WindowClosed");
                case RuleRejectionReasonV3.ConsecutiveCountedContact:
                    return LegacyRuleOutcomeV3.Fault("ConsecutiveCountedTouch");
                case RuleRejectionReasonV3.FourthCountedContact:
                    return LegacyRuleOutcomeV3.Fault("FourthCountedTouch");
                case RuleRejectionReasonV3.ActorNotOnCourt:
                    return LegacyRuleOutcomeV3.Fault("WrongActor");
                case RuleRejectionReasonV3.ActionIneligible:
                    return LegacyRuleOutcomeV3.Fault("WrongAction");
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(v3Transition),
                        "V3 rejection reason is not supported.");
            }
        }

        private static bool IsIntentionalV3Correction(
            LegacyRuleOutcomeV3 legacyOutcome,
            RuleTransitionV3 v3Transition,
            ShadowScenarioV3 scenario)
        {
            if (!v3Transition.Accepted || v3Transition.RejectionReason != RuleRejectionReasonV3.None)
            {
                return false;
            }

            return scenario == ShadowScenarioV3.BlockerFirstCountedContact
                    && legacyOutcome.Disposition == LegacyRuleDispositionV3.Fault
                    && legacyOutcome.Reason == "ConsecutiveCountedTouch"
                || scenario == ShadowScenarioV3.IncidentalCountedContact
                    && legacyOutcome.Disposition == LegacyRuleDispositionV3.Ignore
                    && legacyOutcome.Reason == "WrongAction";
        }

        private static string CreateDiagnostic(
            LegacyRuleOutcomeV3 legacyOutcome,
            RuleTransitionV3 v3Transition,
            ShadowScenarioV3 scenario)
        {
            var contactState = v3Transition.Accepted ? v3Transition.After : null;
            var actor = contactState != null && contactState.LastCountedActor.HasValue
                ? contactState.LastCountedActor.Value.Value
                : "none";
            var classification = contactState != null && contactState.LastContactClassification.HasValue
                ? contactState.LastContactClassification.Value.ToString()
                : "none";
            var contactGroup = contactState != null && contactState.LastContactGroup.HasValue
                ? contactState.LastContactGroup.Value.ToString(CultureInfo.InvariantCulture)
                : "none";

            return "scenario=" + scenario
                + ";legacyDisposition=" + legacyOutcome.Disposition
                + ";legacyReason=" + legacyOutcome.Reason
                + ";v3Accepted=" + (v3Transition.Accepted ? "True" : "False")
                + ";v3Reason=" + v3Transition.RejectionReason
                + ";actor=" + actor
                + ";classification=" + classification
                + ";contactGroup=" + contactGroup;
        }
    }
}
