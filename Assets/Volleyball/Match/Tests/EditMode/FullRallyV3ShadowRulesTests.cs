using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using Volleyball.Match.Domain.FullRallyV3;
using Volleyball.Shared.Contracts;

namespace Volleyball.EditModeTests
{
    public sealed class FullRallyV3ShadowRulesTests
    {
        private static readonly PlayerId AwayBlocker = new PlayerId("away-blocker");
        private static readonly PlayerId HomeAttacker = new PlayerId("home-attacker");
        private static readonly PlayerId HomeServer = new PlayerId("home-server");
        private static readonly PlayerId HomeSetter = new PlayerId("home-setter");

        [Test]
        public void Compare_AcceptedLegacyAndV3Outcomes_AreExactParity()
        {
            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Accept(),
                AcceptedV3Transition(),
                ShadowScenarioV3.BlockerFirstCountedContact);

            Assert.That(result.IsParity, Is.True);
            Assert.That(result.DifferenceKind, Is.EqualTo(RulesShadowDifferenceKindV3.ExactParity));
        }

        [Test]
        public void Compare_DuplicateContactGroupIgnoredByBothEngines_IsExactParity()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 17));

            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Ignore("DuplicateContactGroup"),
                engine.CanAttempt(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 17)),
                ShadowScenarioV3.Other);

            Assert.That(result.DifferenceKind, Is.EqualTo(RulesShadowDifferenceKindV3.ExactParity));
        }

        [Test]
        public void Compare_ConsecutiveCountedTouchAlias_IsExactParity()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 1));

            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Fault("ConsecutiveCountedTouch"),
                engine.CanAttempt(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 2)),
                ShadowScenarioV3.Other);

            Assert.That(result.DifferenceKind, Is.EqualTo(RulesShadowDifferenceKindV3.ExactParity));
        }

        [TestCase(true, RuleRejectionReasonV3.None, LegacyRuleDispositionV3.Accept, "None")]
        [TestCase(false, RuleRejectionReasonV3.DuplicateContactGroup, LegacyRuleDispositionV3.Ignore, "DuplicateContactGroup")]
        [TestCase(false, RuleRejectionReasonV3.RallyClosed, LegacyRuleDispositionV3.Ignore, "WindowClosed")]
        [TestCase(false, RuleRejectionReasonV3.ConsecutiveCountedContact, LegacyRuleDispositionV3.Fault, "ConsecutiveCountedTouch")]
        [TestCase(false, RuleRejectionReasonV3.FourthCountedContact, LegacyRuleDispositionV3.Fault, "FourthCountedTouch")]
        [TestCase(false, RuleRejectionReasonV3.ActorNotOnCourt, LegacyRuleDispositionV3.Fault, "WrongActor")]
        [TestCase(false, RuleRejectionReasonV3.ActionIneligible, LegacyRuleDispositionV3.Fault, "WrongAction")]
        public void Compare_CurrentV3ReasonMappings_AreExactParity(
            bool accepted,
            RuleRejectionReasonV3 v3Reason,
            LegacyRuleDispositionV3 legacyDisposition,
            string legacyReason)
        {
            var result = LegacyRulesShadowComparatorV3.Compare(
                new LegacyRuleOutcomeV3(legacyDisposition, legacyReason),
                CreateUncheckedTransition(accepted, v3Reason),
                ShadowScenarioV3.Other);

            Assert.That(result.DifferenceKind, Is.EqualTo(RulesShadowDifferenceKindV3.ExactParity));
        }

        [Test]
        public void Compare_BlockSequenceCorrection_IsClassifiedIntentional()
        {
            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Fault("ConsecutiveCountedTouch"),
                AcceptedV3TransitionAfterBlock(),
                ShadowScenarioV3.BlockerFirstCountedContact);

            Assert.That(result.IsParity, Is.False);
            Assert.That(result.DifferenceKind,
                Is.EqualTo(RulesShadowDifferenceKindV3.IntentionalV3Correction));
        }

        [Test]
        public void Compare_IncidentalCountedContactCorrection_IsClassifiedIntentional()
        {
            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Ignore("WrongAction"),
                AcceptedV3Transition(),
                ShadowScenarioV3.IncidentalCountedContact);

            Assert.That(result.DifferenceKind,
                Is.EqualTo(RulesShadowDifferenceKindV3.IntentionalV3Correction));
        }

        [Test]
        public void Compare_DisagreeingOutcomesOutsideAllowList_AreUnexpectedMismatch()
        {
            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Fault("WrongPossessionTeam"),
                AcceptedV3Transition(),
                ShadowScenarioV3.Other);

            Assert.That(result.IsParity, Is.False);
            Assert.That(result.DifferenceKind,
                Is.EqualTo(RulesShadowDifferenceKindV3.UnexpectedMismatch));
        }

        [Test]
        public void Compare_CorrectionAllowList_DoesNotWaiveWrongScenario()
        {
            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Fault("ConsecutiveCountedTouch"),
                AcceptedV3TransitionAfterBlock(),
                ShadowScenarioV3.IncidentalCountedContact);

            Assert.That(result.DifferenceKind,
                Is.EqualTo(RulesShadowDifferenceKindV3.UnexpectedMismatch));
        }

        [Test]
        public void Compare_CorrectionAllowList_DoesNotWaivePartialLegacyReasonText()
        {
            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Fault("ConsecutiveCounted"),
                AcceptedV3TransitionAfterBlock(),
                ShadowScenarioV3.BlockerFirstCountedContact);

            Assert.That(result.DifferenceKind,
                Is.EqualTo(RulesShadowDifferenceKindV3.UnexpectedMismatch));
        }

        [Test]
        public void Compare_CorrectionAllowList_DoesNotWaiveWrongV3Reason()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 1));
            engine.Apply(Contact(HomeAttacker, TeamSide.Home, RallyContactClassificationV3.TeamContact, 2));
            engine.Apply(Contact(HomeServer, TeamSide.Home, RallyContactClassificationV3.TeamContact, 3));

            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Fault("ConsecutiveCountedTouch"),
                engine.CanAttempt(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 4)),
                ShadowScenarioV3.BlockerFirstCountedContact);

            Assert.That(result.DifferenceKind,
                Is.EqualTo(RulesShadowDifferenceKindV3.UnexpectedMismatch));
        }

        [Test]
        public void Compare_CorrectionAllowList_DoesNotWaiveAcceptedLegacyOutcome()
        {
            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Accept(),
                AcceptedV3TransitionAfterBlock(),
                ShadowScenarioV3.BlockerFirstCountedContact);

            Assert.That(result.DifferenceKind,
                Is.EqualTo(RulesShadowDifferenceKindV3.ExactParity));
        }

        [Test]
        public void Compare_DiagnosticHasInvariantFixedFieldOrder()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

                var result = LegacyRulesShadowComparatorV3.Compare(
                    LegacyRuleOutcomeV3.Fault("ConsecutiveCountedTouch"),
                    AcceptedV3TransitionAfterBlock(),
                    ShadowScenarioV3.BlockerFirstCountedContact);

                Assert.That(result.Diagnostic, Is.EqualTo(
                    "scenario=BlockerFirstCountedContact;legacyDisposition=Fault;legacyReason=ConsecutiveCountedTouch;v3Accepted=True;v3Reason=None;actor=away-blocker;classification=TeamContact;contactGroup=11"));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Test]
        public void Compare_RejectedTransitionDiagnosticUsesNoneContactFactsInFixedOrder()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 17));

            var result = LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Ignore("DuplicateContactGroup"),
                engine.CanAttempt(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 17)),
                ShadowScenarioV3.Other);

            Assert.That(result.Diagnostic, Is.EqualTo(
                "scenario=Other;legacyDisposition=Ignore;legacyReason=DuplicateContactGroup;v3Accepted=False;v3Reason=DuplicateContactGroup;actor=none;classification=none;contactGroup=none"));
        }

        [Test]
        public void Compare_RequiresLegacyOutcomeAndV3Transition()
        {
            Assert.Throws<ArgumentNullException>(() => LegacyRulesShadowComparatorV3.Compare(
                null,
                AcceptedV3Transition(),
                ShadowScenarioV3.Other));
            Assert.Throws<ArgumentNullException>(() => LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Accept(),
                null,
                ShadowScenarioV3.Other));
        }

        [Test]
        public void LegacyRuleOutcome_RequiresKnownDispositionAndReason()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyRuleOutcomeV3(
                (LegacyRuleDispositionV3)999,
                "None"));
            Assert.Throws<ArgumentException>(() => new LegacyRuleOutcomeV3(
                LegacyRuleDispositionV3.Accept,
                " "));
        }

        [Test]
        public void RuleTransition_RejectsInconsistentAcceptanceAndReasonCombinations()
        {
            Assert.That(ConstructorException(true, RuleRejectionReasonV3.ConsecutiveCountedContact),
                Is.TypeOf<ArgumentException>());
            Assert.That(ConstructorException(false, RuleRejectionReasonV3.None),
                Is.TypeOf<ArgumentException>());
        }

        [Test]
        public void RuleTransition_RejectsUndefinedRejectionReason()
        {
            Assert.That(ConstructorException(false, (RuleRejectionReasonV3)999),
                Is.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Compare_RejectsInconsistentTransitionFromAnotherAssemblyVersion()
        {
            Assert.Throws<ArgumentException>(() => LegacyRulesShadowComparatorV3.Compare(
                LegacyRuleOutcomeV3.Accept(),
                CreateMalformedTransition(true, RuleRejectionReasonV3.ConsecutiveCountedContact),
                ShadowScenarioV3.Other));
        }

        private static RuleTransitionV3 AcceptedV3Transition()
        {
            return RallyRulesEngineV3.Open(TeamSide.Home)
                .CanAttempt(Contact(HomeSetter, TeamSide.Home, RallyContactClassificationV3.TeamContact, 5));
        }

        private static RuleTransitionV3 AcceptedV3TransitionAfterBlock()
        {
            var engine = RallyRulesEngineV3.Open(TeamSide.Home);
            engine.Apply(Contact(AwayBlocker, TeamSide.Away, RallyContactClassificationV3.BlockContact, 10));
            return engine.CanAttempt(Contact(AwayBlocker, TeamSide.Away, RallyContactClassificationV3.TeamContact, 11));
        }

        private static ActualContactEventV3 Contact(
            PlayerId actor,
            TeamSide team,
            RallyContactClassificationV3 classification,
            long contactGroup)
        {
            return new ActualContactEventV3(actor, team, classification, contactGroup);
        }

        private static Exception ConstructorException(bool accepted, RuleRejectionReasonV3 reason)
        {
            try
            {
                CreateUncheckedTransition(accepted, reason);
                return null;
            }
            catch (TargetInvocationException exception)
            {
                return exception.InnerException;
            }
        }

        private static RuleTransitionV3 CreateUncheckedTransition(bool accepted, RuleRejectionReasonV3 reason)
        {
            var constructor = typeof(RuleTransitionV3).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(bool),
                    typeof(RuleRejectionReasonV3),
                    typeof(TouchSequenceStateV3),
                    typeof(TouchSequenceStateV3)
                },
                null);

            return (RuleTransitionV3)constructor.Invoke(new object[]
            {
                accepted,
                reason,
                TouchSequenceStateV3.Initial,
                TouchSequenceStateV3.Initial
            });
        }

        private static RuleTransitionV3 CreateMalformedTransition(bool accepted, RuleRejectionReasonV3 reason)
        {
            var transition = (RuleTransitionV3)FormatterServices.GetUninitializedObject(typeof(RuleTransitionV3));
            SetAutoProperty(transition, "Accepted", accepted);
            SetAutoProperty(transition, "RejectionReason", reason);
            SetAutoProperty(transition, "Before", TouchSequenceStateV3.Initial);
            SetAutoProperty(transition, "After", TouchSequenceStateV3.Initial);
            return transition;
        }

        private static void SetAutoProperty<T>(RuleTransitionV3 transition, string propertyName, T value)
        {
            var field = typeof(RuleTransitionV3).GetField(
                "<" + propertyName + ">k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(transition, value);
        }
    }
}
