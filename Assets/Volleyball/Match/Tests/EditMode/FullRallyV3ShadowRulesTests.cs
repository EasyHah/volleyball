using System;
using System.Globalization;
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
    }
}
